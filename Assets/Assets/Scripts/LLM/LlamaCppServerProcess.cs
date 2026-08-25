using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
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
        _disposed = true;
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
                _ownedProcess.Kill();
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

    public IEnumerator EnsureReady(LLMRuntimeConfig config, Action<bool, string> onComplete)
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
        string probeError = string.Empty;
        yield return ProbeServer(baseUrl, 2, (ok, error) =>
        {
            ready = ok;
            probeError = error;
        });

        if (ready)
        {
            onComplete?.Invoke(true, "Connected to existing llama.cpp server.");
            yield break;
        }

        if (_ownedProcess != null && !_ownedProcess.HasExited)
        {
            yield return WaitForHealth(baseUrl, config.startupTimeoutSeconds, onComplete);
            yield break;
        }

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

        if (!TryBuildArguments(config, helpText, out string arguments, out string argumentError))
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

        yield return WaitForHealth(baseUrl, config.startupTimeoutSeconds, onComplete);
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

    private static IEnumerator ProbeServer(string baseUrl, int timeoutSeconds, Action<bool, string> onComplete)
    {
        string url = baseUrl.TrimEnd('/') + "/health";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Mathf.Max(1, timeoutSeconds);
            yield return request.SendWebRequest();

            bool ok =
                request.result == UnityWebRequest.Result.Success &&
                request.responseCode >= 200 &&
                request.responseCode < 500;

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

    private static bool TryBuildArguments(LLMRuntimeConfig config, string helpText, out string arguments, out string error)
    {
        arguments = string.Empty;
        error = string.Empty;

        bool helpAvailable = !string.IsNullOrWhiteSpace(helpText) && !helpText.StartsWith("HELP_PROBE_FAILED:", StringComparison.Ordinal);
        bool Supports(string flag) => helpAvailable && helpText.IndexOf(flag, StringComparison.OrdinalIgnoreCase) >= 0;
        bool RequiredFlag(string preferred, string fallback) => !helpAvailable || Supports(preferred) || Supports(fallback);

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
        }

        if (config.gpuLayerCount >= 0)
        {
            string layerFlag = Supports("--n-gpu-layers")
                ? "--n-gpu-layers"
                : Supports("-ngl")
                    ? "-ngl"
                    : null;
            AppendArgument(args, layerFlag, config.gpuLayerCount.ToString());
        }

        if (config.enableFlashAttention && Supports("--flash-attn"))
            args.Append(" --flash-attn on");

        if (config.keepKvCacheInSystemRam && Supports("--no-kv-offload"))
            args.Append(" --no-kv-offload");

        if (Supports("--fit"))
            args.Append(" --fit on");

        if (Supports("--fit-target"))
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
                // note: Windows schedules the owned model server below the game so CPU-side token work yields before starving Unity's render loop.
                try
                {
                    _ownedProcess.PriorityClass =
                        ProcessPriorityClass.BelowNormal;
                }
                catch
                {
                    // note: Priority changes are advisory and may be denied without affecting server correctness.
                }
            }

            _ownsProcess = true;
            UnityEngine.Debug.Log("[LlamaCppServerProcess] Started owned llama-server process.");
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
