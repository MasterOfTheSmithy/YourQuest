using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class LlamaCppServerProcess : IDisposable
{
    private Process _ownedProcess;
    private bool _ownsProcess;
    private bool _disposed;

    public bool OwnsProcess => _ownsProcess;

    public void Dispose()
    {
        Dispose(true);
    }

    public void Dispose(bool stopOwnedProcess)
    {
        _disposed = true;
        if (stopOwnedProcess)
            StopOwnedProcess();
    }

    public void StopOwnedProcess()
    {
        if (!_ownsProcess || _ownedProcess == null)
            return;

        try
        {
            if (!_ownedProcess.HasExited)
            {
                // note: Only the process launched by YourQuest is terminated; external llama servers are left alone.
                // note: Terminate the complete owned process tree so a launcher or worker child cannot survive the game's shutdown.
                TerminateOwnedProcessTree(_ownedProcess);
                // note: Wait briefly for the operating system to reap the owned server before releasing its handle.
                _ownedProcess.WaitForExit(2000);
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("[LlamaCppServerProcess] Failed to stop owned llama-server: " + ex.Message);
        }
        finally
        {
            _ownedProcess.Dispose();
            _ownedProcess = null;
            _ownsProcess = false;
        }
    }

    internal static void TerminateOwnedProcessTree(Process process)
    {
        // note: Unity's supported Process API varies by editor runtime; prefer tree termination when available without binding the project to a newer overload.
        MethodInfo killTree = typeof(Process).GetMethod(
            "Kill",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(bool) },
            null);
        if (killTree != null)
        {
            // note: Reflection keeps the complete-tree shutdown behavior on runtimes that expose it while preserving compilation on older Unity profiles.
            killTree.Invoke(process, new object[] { true });
            return;
        }

        // note: Older Unity profiles can still terminate the owned server itself; the normal process handle cleanup follows immediately.
        process.Kill();
    }

    public IEnumerator EnsureReady(LLMRuntimeConfig config, Action<bool, string> onComplete, bool protectLivePresentation = false)
    {
        if (_disposed)
        {
            onComplete?.Invoke(false, "llama.cpp process manager has been disposed.");
            yield break;
        }

        if (config == null || !config.enableRuntimeLlm)
        {
            onComplete?.Invoke(false, "LLM runtime is disabled.");
            yield break;
        }

        string baseUrl = config.BuildBaseUrl();
        bool ready = false;
        bool reachable = false;
        string probeError = string.Empty;
        yield return ProbeServer(baseUrl, 2, (ok, error) =>
        {
            ready = ok;
            probeError = error;
        }, present => reachable = present);

        if (ready)
        {
            onComplete?.Invoke(true, "Connected to existing llama.cpp server.");
            yield break;
        }

        // note: A loading or busy existing server owns the port; wait for it instead of starting a duplicate model process.
        if (reachable)
        {
            yield return WaitForHealth(baseUrl, config.startupTimeoutSeconds, onComplete);
            yield break;
        }

        if (_ownedProcess != null && !_ownedProcess.HasExited)
        {
            yield return WaitForHealth(baseUrl, config.startupTimeoutSeconds, onComplete);
            yield break;
        }

        // note: Dispose an exited owned process before replacing it so retries never retain a stale handle or server state.
        if (_ownedProcess != null)
            StopOwnedProcess();

        if (!TryResolveExecutable(config.llamaServerExecutablePath, out string executablePath, out string executableError))
        {
            onComplete?.Invoke(false, executableError + " Last health probe: " + probeError);
            yield break;
        }

        if (!File.Exists(config.ggufModelPath))
        {
            onComplete?.Invoke(false, "Configured GGUF model was not found: " + config.ggufModelPath);
            yield break;
        }

        string helpText = string.Empty;
        yield return ReadHelpText(executablePath, Mathf.Max(1, config.helpProbeTimeoutSeconds), text => helpText = text);

        if (!TryBuildArguments(config, helpText, out string arguments, out string argumentError, protectLivePresentation))
        {
            onComplete?.Invoke(false, argumentError);
            yield break;
        }

        if (!TryStartProcess(
                executablePath,
                arguments,
                config.preserveGameResponsiveness,
                out string startError))
        {
            onComplete?.Invoke(false, startError);
            yield break;
        }

        yield return WaitForHealth(baseUrl, config.startupTimeoutSeconds, (ok, message) =>
        {
            if (!ok)
                StopOwnedProcess();
            onComplete?.Invoke(ok, message);
        });
    }

    public bool HasOwnedProcessExited()
    {
        if (_ownedProcess == null || !_ownsProcess)
            return false;

        try
        {
            return _ownedProcess.HasExited;
        }
        catch
        {
            return true;
        }
    }

    private IEnumerator WaitForHealth(string baseUrl, int timeoutSeconds, Action<bool, string> onComplete)
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(1, timeoutSeconds);
        string lastError = string.Empty;

        while (Time.realtimeSinceStartup < deadline)
        {
            bool ready = false;
            yield return ProbeServer(baseUrl, 2, (ok, error) =>
            {
                ready = ok;
                lastError = error;
            });

            if (ready)
            {
                onComplete?.Invoke(true, "llama.cpp server is ready.");
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.25f);
        }

        onComplete?.Invoke(false, "llama.cpp server did not become ready before timeout. Last health probe: " + lastError);
    }

    private static IEnumerator ProbeServer(string baseUrl, int timeoutSeconds, Action<bool, string> onComplete, Action<bool> onReachable = null)
    {
        string url = baseUrl.TrimEnd('/') + "/health";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Mathf.Max(1, timeoutSeconds);
            yield return request.SendWebRequest();

            bool ok =
                request.result == UnityWebRequest.Result.Success &&
                request.responseCode >= 200 &&
                request.responseCode < 300;

            onReachable?.Invoke(request.responseCode > 0);
            onComplete?.Invoke(ok, ok ? string.Empty : request.error);
        }
    }

    private static IEnumerator ReadHelpText(string executablePath, int timeoutSeconds, Action<string> onComplete)
    {
        StringBuilder output = new StringBuilder(8192);
        Process process = null;

        try
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--help",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                    output.AppendLine(args.Data);
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                    output.AppendLine(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            process?.Dispose();
            onComplete?.Invoke("HELP_PROBE_FAILED: " + ex.Message);
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + Mathf.Max(1, timeoutSeconds);
        while (!process.HasExited && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (!process.HasExited)
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // note: Help probing is best-effort; startup will fail cleanly later if arguments are wrong.
            }
        }

        process.Dispose();
        onComplete?.Invoke(output.ToString());
    }

    private static bool TryBuildArguments(LLMRuntimeConfig config, string helpText, out string arguments, out string error, bool protectLivePresentation = false)
    {
        arguments = string.Empty;
        error = string.Empty;

        bool helpAvailable = !string.IsNullOrWhiteSpace(helpText) && !helpText.StartsWith("HELP_PROBE_FAILED:", StringComparison.Ordinal);
        bool Supports(string flag) => helpAvailable && helpText.IndexOf(flag, StringComparison.OrdinalIgnoreCase) >= 0;
        bool RequiredFlag(string preferred, string fallback) => !helpAvailable || Supports(preferred) || Supports(fallback);

        // note: D19 tied a gameplay hitch to model-start residency paging. Keep the same model, context, sampling, and CPU limits while excluding GPU allocations for an automatic live-gameplay launch.
        bool useCpuForLivePresentation = protectLivePresentation && config.preserveGameResponsiveness &&
            config.gpuLayerCount < 0 && string.IsNullOrWhiteSpace(config.extraLlamaServerArguments);
        if (useCpuForLivePresentation && (!Supports("--device") || !Supports("--no-kv-offload") ||
            !Supports("--no-op-offload") || !Supports("--fit")))
        {
            error = "Installed llama-server cannot enforce the automatic gameplay CPU residency policy.";
            return false;
        }

        if (!RequiredFlag("--model", "-m"))
        {
            error = "Configured llama-server help output does not show a supported model flag.";
            return false;
        }

        StringBuilder args = new StringBuilder(512);
        AppendArgument(args, Supports("--model") || !helpAvailable ? "--model" : "-m", config.ggufModelPath);
        AppendArgument(args, Supports("--host") || !helpAvailable ? "--host" : null, config.serverHost);
        AppendArgument(args, Supports("--port") || !helpAvailable ? "--port" : null, Mathf.Clamp(config.serverPort, 1024, 65535).ToString());
        AppendArgument(args, Supports("--ctx-size") || !helpAvailable ? "--ctx-size" : null, Mathf.Clamp(config.contextSizeTokens, 2048, 32768).ToString());
        AppendArgument(args, Supports("--parallel") || !helpAvailable ? "--parallel" : null, Mathf.Clamp(config.serverParallelSlots, 1, 4).ToString());

        if (config.preserveGameResponsiveness)
        {
            int workerThreads =
                Mathf.Clamp(
                    SystemInfo.processorCount -
                    Mathf.Max(1, config.reservedCpuThreads),
                    1,
                    4);

            int logicalBatch =
                Mathf.Clamp(
                    config.promptBatchSize,
                    32,
                    2048);

            int physicalBatch =
                Mathf.Clamp(
                    config.promptMicroBatchSize,
                    16,
                    Mathf.Min(512, logicalBatch));

            // note: Reserve CPU capacity for Unity, cap inference at four workers, and use smaller prompt-evaluation slices so llama.cpp cannot monopolize gameplay frames.
            AppendArgument(args, Supports("--threads") ? "--threads" : Supports("-t") ? "-t" : null, workerThreads.ToString());
            AppendArgument(args, Supports("--threads-batch") ? "--threads-batch" : Supports("-tb") ? "-tb" : null, workerThreads.ToString());
            AppendArgument(args, Supports("--batch-size") ? "--batch-size" : Supports("-b") ? "-b" : null, logicalBatch.ToString());
            AppendArgument(args, Supports("--ubatch-size") ? "--ubatch-size" : Supports("-ub") ? "-ub" : null, physicalBatch.ToString());
            AppendArgument(args, Supports("--prio") ? "--prio" : null, "-1");
            AppendArgument(args, Supports("--poll") ? "--poll" : null, Mathf.Clamp(config.serverPollingPercent, 0, 100).ToString());

            if (Supports("--no-cache-prompt"))
                args.Append(" --no-cache-prompt");

            string cacheRamFlag = Supports("--cache-ram")
                ? "--cache-ram"
                : Supports("-cram")
                    ? "-cram"
                    : null;

            // note: Startup prompts differ substantially, so host prompt caching only evicted and reallocated hundreds of megabytes between Goddess stages without useful reuse.
            AppendArgument(args, cacheRamFlag, "0");

            // note: Retain errors while suppressing per-token timing output inherited by Unity's log stream during local generation.
            AppendArgument(args, Supports("--log-verbosity") ? "--log-verbosity" : Supports("-lv") ? "-lv" : null, "1");
        }

        if (useCpuForLivePresentation)
        {
            // note: Zero GPU layers alone can still offload cache or host operations; disable all three paths and automatic GPU fitting together.
            AppendArgument(args, "--device", "none");
            args.Append(" --no-op-offload");
        }

        if (config.gpuLayerCount >= 0 || useCpuForLivePresentation)
        {
            string layerFlag = Supports("--n-gpu-layers")
                ? "--n-gpu-layers"
                : Supports("-ngl")
                    ? "-ngl"
                    : null;
            AppendArgument(args, layerFlag, useCpuForLivePresentation ? "0" : config.gpuLayerCount.ToString());
        }

        if (config.enableFlashAttention && Supports("--flash-attn"))
            args.Append(" --flash-attn on");

        if ((config.keepKvCacheInSystemRam || useCpuForLivePresentation) && Supports("--no-kv-offload"))
            args.Append(" --no-kv-offload");

        if (Supports("--fit"))
            args.Append(useCpuForLivePresentation ? " --fit off" : " --fit on");

        if (!useCpuForLivePresentation && Supports("--fit-target"))
            AppendArgument(args, "--fit-target", Mathf.Max(512, config.targetGpuHeadroomMb).ToString());

        if (Supports("--no-webui"))
            args.Append(" --no-webui");

        if (Supports("--reasoning"))
            args.Append(" --reasoning off");

        if (!string.IsNullOrWhiteSpace(config.extraLlamaServerArguments))
            args.Append(' ').Append(config.extraLlamaServerArguments.Trim());

        arguments = args.ToString().Trim();
        return true;
    }

    private static void AppendArgument(StringBuilder args, string flag, string value)
    {
        if (string.IsNullOrWhiteSpace(flag) || string.IsNullOrWhiteSpace(value))
            return;

        args.Append(' ')
            .Append(flag)
            .Append(' ')
            .Append(Quote(value));
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private bool TryStartProcess(
        string executablePath,
        string arguments,
        bool lowerProcessPriority,
        out string error)
    {
        error = string.Empty;

        try
        {
            _ownedProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                },
                EnableRaisingEvents = true
            };

            _ownedProcess.Start();

            if (lowerProcessPriority)
            {
                // note: The owned model server is background work; idle priority makes every Unity render/input thread win scheduler contention before inference consumes spare CPU time.
                try
                {
                    _ownedProcess.PriorityClass =
                        ProcessPriorityClass.Idle;
                }
                catch
                {
                    // note: Priority changes are advisory and may be denied without affecting server correctness.
                }
            }

            _ownsProcess = true;
            UnityEngine.Debug.Log("[LlamaCppServerProcess] Started owned llama-server process; gpuDeviceNone=" + arguments.Contains("--device \"none\"") + ".");
            return true;
        }
        catch (Exception ex)
        {
            _ownedProcess?.Dispose();
            _ownedProcess = null;
            _ownsProcess = false;
            error = "Failed to start llama-server: " + ex.Message;
            return false;
        }
    }

    private static bool TryResolveExecutable(string configuredPath, out string resolvedPath, out string error)
    {
        resolvedPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            error = "llama-server executable path is empty.";
            return false;
        }

        string trimmed = configuredPath.Trim();
        if (File.Exists(trimmed))
        {
            resolvedPath = trimmed;
            return true;
        }

        string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string executableName = trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + ".exe";
        string[] paths = pathVariable.Split(Path.PathSeparator);
        for (int i = 0; i < paths.Length; i++)
        {
            string folder = paths[i];
            if (string.IsNullOrWhiteSpace(folder))
                continue;

            string candidate = Path.Combine(folder.Trim(), executableName);
            if (File.Exists(candidate))
            {
                resolvedPath = candidate;
                return true;
            }
        }

        error = "Could not find llama-server executable: " + configuredPath;
        return false;
    }
}
