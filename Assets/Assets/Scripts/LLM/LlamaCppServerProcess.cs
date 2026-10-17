using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public sealed class LlamaCppServerProcess : IDisposable
{
    private Process _ownedProcess;
    private bool _ownsProcess;
    private bool _disposed;
    private bool _boundedPromptReuseEnabled;
    private string _ownedBaseUrl;
    // note: A failed residency measurement belongs to this process owner and load shape, never persistent world state or a time-based generation timeout.
    private readonly Dictionary<string, int> _reloadFreeMemoryMb = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public bool OwnsProcess => _ownsProcess;
    public bool BoundedPromptReuseEnabled => _ownsProcess && _boundedPromptReuseEnabled;

    public IEnumerator ConfirmOwnedInferenceIdle(Action<bool> completed)
    {
        // note: HTTP cancellation alone is insufficient. Two fresh single-slot acknowledgements allow safe reuse; unavailable/busy telemetry falls back to the existing process-exit barrier.
        Process expected = _ownedProcess;
        if (!_ownsProcess || expected == null || string.IsNullOrEmpty(_ownedBaseUrl) || HasOwnedProcessExited())
        { completed(false); yield break; }
        // note: This engine checks non-streaming HTTP disconnects on a one-second poll; permit that acknowledgement before taking the slower process-exit fallback.
        double deadline = Time.realtimeSinceStartupAsDouble + 1.5d;
        int idleObservations = 0;
        while (!_disposed && ReferenceEquals(expected, _ownedProcess) && !HasOwnedProcessExited() && Time.realtimeSinceStartupAsDouble < deadline)
        {
            using (var request = UnityWebRequest.Get(_ownedBaseUrl.TrimEnd('/') + "/slots"))
            {
                request.timeout = 1;
                yield return request.SendWebRequest();
                if (_disposed || !ReferenceEquals(expected, _ownedProcess) || HasOwnedProcessExited()) break;
                bool idle = request.result == UnityWebRequest.Result.Success && request.responseCode == 200 && SlotsConfirmIdle(request.downloadHandler.text);
                idleObservations = idle ? idleObservations + 1 : 0;
                if (idleObservations >= 2) { completed(true); yield break; }
                if (request.responseCode != 200) break;
            }
            yield return new WaitForSecondsRealtime(.05f);
        }
        completed(false);
    }

    internal static bool SlotsConfirmIdle(string json)
    {
        // note: Missing, malformed, busy or multi-slot responses never count as cancellation acknowledgement.
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 65536) return false;
            JArray slots = JArray.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            return slots.Count == 1 && slots[0] is JObject slot && slot["id"]?.Type == JTokenType.Integer &&
                slot.Value<int>("id") == 0 && slot["is_processing"]?.Type == JTokenType.Boolean && !slot.Value<bool>("is_processing");
        }
        catch (Exception) { return false; }
    }

    public IEnumerator StopOwnedProcessRoutine(Action<bool, string> onComplete)
    {
        // note: A model switch waits for this exact owned process to exit without blocking Unity's main thread or abandoning a still-live handle.
        if (!_ownsProcess || _ownedProcess == null) { onComplete?.Invoke(true, string.Empty); yield break; }
        Process retiring = _ownedProcess;
        try { if (!retiring.HasExited) TerminateOwnedProcessTree(retiring); }
        catch (Exception error) { onComplete?.Invoke(false, "Owned model termination failed: " + error.Message); yield break; }
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!retiring.HasExited && Time.realtimeSinceStartup < deadline) yield return null;
        if (!retiring.HasExited) { onComplete?.Invoke(false, "Owned model is still alive; replacement loading deferred."); yield break; }
        if (ReferenceEquals(_ownedProcess, retiring)) { retiring.Dispose(); _ownedProcess = null; _ownsProcess = false; }
        // note: Process exit releases its CUDA allocations; give the driver a frame boundary before a replacement can allocate.
        yield return null;
        onComplete?.Invoke(true, string.Empty);
    }

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

        bool exited = false;
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
            exited = _ownedProcess.HasExited;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("[LlamaCppServerProcess] Failed to stop owned llama-server: " + ex.Message);
        }
        finally
        {
            // note: A failed exit retains ownership/handle so a later model switch cannot mistake live GPU allocations for a clean unload.
            if (exited)
            {
                _ownedProcess.Dispose();
                _ownedProcess = null;
                _ownsProcess = false;
            }
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

        // note: Direct adapter callers cannot bypass the process-scoped benchmark guard.
        if (LLMRuntimeConfig.BenchmarkIsolationActive || config == null || !config.enableRuntimeLlm)
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

        // note: A health response does not prove model identity. Routed deployments never borrow an unowned port/server.
        if ((ready || reachable) && (config.requireOwnedLlamaServer || !config.cpuInferenceApproved) && !OwnsProcess)
        {
            onComplete?.Invoke(false, "Configured llama.cpp port belongs to an unowned server; model identity cannot be verified.");
            yield break;
        }
        if (ready)
        {
            // note: Rendering allocations can grow while a model stays resident; recheck the reserve before every reused request.
            yield return VerifyInferenceReserve(config, onComplete);
            yield break;
        }

        // note: A loading or busy existing server owns the port; wait for it instead of starting a duplicate model process.
        if (reachable)
        {
            yield return WaitForHealthAndReserve(config, baseUrl, onComplete);
            yield break;
        }

        if (_ownedProcess != null && !_ownedProcess.HasExited)
        {
            yield return WaitForHealthAndReserve(config, baseUrl, onComplete);
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

        // note: Full GPU is mandatory for generators. Insufficient or unknown memory is a resource rejection, never CPU/partial offload.
        if (!config.cpuInferenceApproved)
        {
            int freeMb = 0;
            yield return ReadGpuFreeMemory(value => freeMb = value);
            if (!CanAdmitGpuLoad(config, freeMb))
            {
                // note: Ask the existing game asset owner to reclaim cold reloadable caches before rejecting a serial model load; physical free VRAM is remeasured afterward.
                yield return YQGeneratedWorldPerformanceDirector.ReclaimReloadableGpuAssetsRoutine();
                yield return ReadGpuFreeMemory(value => freeMb = value);
            }
            if (!CanAdmitGpuLoad(config, freeMb) &&
                YQGeneratedWorldPerformanceDirector.TryRelieveGpuMemoryPressure())
            {
                // note: Let Unity release old texture residency, then repeat the unchanged full-GPU admission gate with fresh telemetry.
                yield return null;
                yield return null;
                yield return ReadGpuFreeMemory(value => freeMb = value);
            }
            if (!CanAdmitGpuLoad(config, freeMb))
            {
                onComplete?.Invoke(false, "FullGpuUnavailable: freeMb=" + freeMb + ", reserveMb=" + RequiredGpuReserve(config.targetGpuHeadroomMb) +
                    ", measuredReloadMinimumMb=" + MeasuredReloadMinimum(config) + ". Model not loaded; CPU and partial offload are disabled.");
                yield break;
            }
        }
        bool argumentsReady = TryBuildArguments(config, helpText, out string arguments, out string argumentError, protectLivePresentation);
        bool boundedReuse = CanUseBoundedPromptReuse(config, helpText);
        if (!argumentsReady)
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

        _ownedBaseUrl = baseUrl;
        bool loaded = false; string healthMessage = string.Empty;
        yield return WaitForHealth(baseUrl, config.startupTimeoutSeconds, (ok, message) => { loaded = ok; healthMessage = message; });
        if (loaded)
        {
            yield return VerifyInferenceReserve(config, (ok, message) => { loaded = ok; healthMessage = message; });
        }
        _boundedPromptReuseEnabled = loaded && boundedReuse;
        if (!loaded) StopOwnedProcess();
        onComplete?.Invoke(loaded, healthMessage);
    }

    internal static bool CanFitFullGpu(int freeMb, int reserveMb, long modelBytes)
    {
        // note: The measured 4B route admits with 256 MiB initial workspace; actual loaded free VRAM, not this estimate, owns acceptance.
        return freeMb > 0 && modelBytes > 0 && freeMb - RequiredGpuReserve(reserveMb) >=
            Math.Ceiling(modelBytes / (1024d * 1024d)) + 256d;
    }

    private static string LoadShape(LLMRuntimeConfig config) => config.ggufModelPath + "|" +
        config.contextSizeTokens + "|" + config.serverParallelSlots + "|" + config.useQuantizedKvCache + "|" +
        config.keepKvCacheInSystemRam + "|" + config.enableFlashAttention + "|" + config.extraLlamaServerArguments;

    private int MeasuredReloadMinimum(LLMRuntimeConfig config) =>
        _reloadFreeMemoryMb.TryGetValue(LoadShape(config), out int required) ? required : 0;

    private bool CanAdmitGpuLoad(LLMRuntimeConfig config, int freeMb) =>
        CanFitFullGpu(freeMb, config.targetGpuHeadroomMb, new FileInfo(config.ggufModelPath).Length) &&
        freeMb >= MeasuredReloadMinimum(config);

    internal static int RequiredReloadFreeMemory(int unloadedFreeMb, int loadedFreeMb, int reserveMb)
    {
        // note: Learn the actual resident footprint after a reserve failure, plus 128 MiB hysteresis; repeated requests cannot churn the same non-fitting model.
        if (unloadedFreeMb <= 0 || loadedFreeMb <= 0 || unloadedFreeMb <= loadedFreeMb) return 0;
        return unloadedFreeMb - loadedFreeMb + RequiredGpuReserve(reserveMb) + 128;
    }

    internal static int RequiredGpuReserve(int configuredMb)
    {
        // note: Older serialized configurations cannot lower the user's approved 1.5 GiB minimum.
        return Math.Max(1536, configuredMb);
    }

    private IEnumerator WaitForHealthAndReserve(LLMRuntimeConfig config, string baseUrl, Action<bool, string> onComplete)
    {
        // note: A loading/busy server must pass the same memory gate as a newly loaded or already healthy server.
        bool ready = false; string reason = string.Empty;
        yield return WaitForHealth(baseUrl, config.startupTimeoutSeconds, (ok, message) => { ready = ok; reason = message; });
        if (!ready) { onComplete?.Invoke(false, reason); yield break; }
        yield return VerifyInferenceReserve(config, onComplete);
    }

    private IEnumerator VerifyInferenceReserve(LLMRuntimeConfig config, Action<bool, string> onComplete)
    {
        // note: Explicitly approved CPU helpers do not allocate model VRAM. All GPU requests require fresh telemetry and fail closed.
        if (config.cpuInferenceApproved) { onComplete?.Invoke(true, "Approved CPU helper ready."); yield break; }
        int remainingMb = 0;
        yield return ReadGpuFreeMemory(value => remainingMb = value);
        int requiredMb = RequiredGpuReserve(config.targetGpuHeadroomMb);
        if (remainingMb < requiredMb)
        {
            // note: Game growth can consume headroom after a warm model load. Reclaim unused game assets before unloading the owned model.
            yield return YQGeneratedWorldPerformanceDirector.ReclaimReloadableGpuAssetsRoutine();
            yield return ReadGpuFreeMemory(value => remainingMb = value);
        }
        if (remainingMb < requiredMb && YQGeneratedWorldPerformanceDirector.TryRelieveGpuMemoryPressure())
        {
            // note: Ask the existing render-budget owner before unloading a healthy model; actual free VRAM still decides whether inference is allowed.
            yield return null;
            yield return null;
            yield return ReadGpuFreeMemory(value => remainingMb = value);
        }
        if (remainingMb < requiredMb)
        {
            bool stopped = false; string stopError = string.Empty;
            yield return StopOwnedProcessRoutine((ok, message) => { stopped = ok; stopError = message; });
            _boundedPromptReuseEnabled = false;
            if (stopped && remainingMb > 0)
            {
                int unloadedFreeMb = 0;
                yield return ReadGpuFreeMemory(value => unloadedFreeMb = value);
                int measuredMinimum = RequiredReloadFreeMemory(unloadedFreeMb, remainingMb, requiredMb);
                if (measuredMinimum > 0)
                {
                    string shape = LoadShape(config);
                    _reloadFreeMemoryMb[shape] = Math.Max(MeasuredReloadMinimum(config), measuredMinimum);
                    UnityEngine.Debug.Log("[LlamaCppServerProcess] Reload deferred until GPU memory improves: freeMb=" +
                        unloadedFreeMb + ", measuredReloadMinimumMb=" + _reloadFreeMemoryMb[shape] + ".");
                }
            }
            onComplete?.Invoke(false, "GPU reserve unavailable before inference: freeMb=" + remainingMb +
                ", requiredMb=" + requiredMb + (stopped ? ". Owned model unloaded." : ". " + stopError));
            yield break;
        }
        onComplete?.Invoke(true, "Full-GPU server ready; freeMb=" + remainingMb + ", reserveMb=" + requiredMb);
    }

    private static int LiveGpuLayerCount(int freeMb, int reserveMb, long modelBytes)
    {
        // note: This budget is scoped to the approved 32-block Qwen3.5 4B GGUF. Include cache/compute growth and leave existing rendering allocations resident.
        double perLayerMb = Math.Max(1d, modelBytes / (32d * 1024d * 1024d)) + 16d;
        int layers = Math.Max(0, (int)Math.Floor((freeMb - Math.Max(512, reserveMb) - 576d) / perLayerMb));
        return layers >= 33 ? 999 : layers;
    }

    private static IEnumerator ReadGpuFreeMemory(Action<int> completed)
    {
        Process probe = null;
        try
        {
            probe = Process.Start(new ProcessStartInfo("nvidia-smi", "--query-gpu=memory.free --format=csv,noheader,nounits") {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            });
        }
        catch (Exception error) { UnityEngine.Debug.LogWarning("[LlamaCppServerProcess] GPU memory telemetry unavailable: " + error.Message); }
        if (probe == null) { completed(0); yield break; }
        try
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!probe.HasExited && Time.realtimeSinceStartup < deadline) yield return null;
            if (!probe.HasExited)
            {
                // note: Only this short-lived telemetry probe is retired; unavailable memory proof blocks GPU loading until a later request.
                try { probe.Kill(); } catch { }
                completed(0); yield break;
            }
            string value = probe.StandardOutput.ReadToEnd().Trim();
            completed(probe.ExitCode == 0 && int.TryParse(value, out int freeMb) ? Math.Max(0, freeMb) : 0);
        }
        finally
        {
            // note: Disposing a Process handle alone does not stop its OS process when this nested coroutine is cancelled.
            DisposeOwnedProbe(probe);
        }
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
        string lastError = string.Empty;
        // note: Do not fail a queued generation request while its model is still loading. Known process death and owner disposal remain real failures.
        while (!_disposed)
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

            if (_ownedProcess != null && _ownedProcess.HasExited)
            {
                onComplete?.Invoke(false, "Owned llama.cpp process exited while loading. Last health probe: " + lastError);
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.25f);
        }

        onComplete?.Invoke(false, "llama.cpp service owner was disposed while waiting. Last health probe: " + lastError);
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
        object outputLock = new object();
        bool stdoutComplete = false, stderrComplete = false;
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
                // note: Exit can precede asynchronous pipe delivery; retain every line and its EOF under one lock.
                lock (outputLock)
                {
                    if (args.Data == null) stdoutComplete = true;
                    else output.AppendLine(args.Data);
                }
            };
            process.ErrorDataReceived += (_, args) =>
            {
                lock (outputLock)
                {
                    if (args.Data == null) stderrComplete = true;
                    else output.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            DisposeOwnedProbe(process);
            onComplete?.Invoke("HELP_PROBE_FAILED: " + ex.Message);
            yield break;
        }

        string result = "HELP_PROBE_FAILED: timed out before process exit and complete output delivery.";
        try
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(1, timeoutSeconds);
            // note: Keep the Editor responsive while both readers drain; partial help must never certify GPU capabilities.
            while (true)
            {
                lock (outputLock)
                {
                    if (process.HasExited && stdoutComplete && stderrComplete)
                    {
                        result = process.ExitCode == 0 ? output.ToString() : "HELP_PROBE_FAILED: exit code " + process.ExitCode;
                        break;
                    }
                }
                // note: A long game frame can outlast this deadline after the probe has already completed; check complete output before rejecting it as timed out.
                if (Time.realtimeSinceStartup >= deadline) break;
                yield return null;
            }
        }
        finally
        {
            // note: Explicit request cancellation disposes this iterator before normal completion; retire only the probe it started.
            DisposeOwnedProbe(process);
        }
        onComplete?.Invoke(result);
    }

    private static void DisposeOwnedProbe(Process process)
    {
        if (process == null) return;
        try
        {
            if (!process.HasExited) TerminateOwnedProcessTree(process);
        }
        catch (Exception error)
        {
            UnityEngine.Debug.LogWarning("[LlamaCppServerProcess] Owned probe cleanup failed: " + error.Message);
        }
        finally { process.Dispose(); }
    }

    private static bool TryBuildArguments(LLMRuntimeConfig config, string helpText, out string arguments, out string error, bool protectLivePresentation = false)
    {
        arguments = string.Empty;
        error = string.Empty;

        bool helpAvailable = !string.IsNullOrWhiteSpace(helpText) && !helpText.StartsWith("HELP_PROBE_FAILED:", StringComparison.Ordinal);
        bool Supports(string flag) => helpAvailable && helpText.IndexOf(flag, StringComparison.OrdinalIgnoreCase) >= 0;
        bool RequiredFlag(string preferred, string fallback) => !helpAvailable || Supports(preferred) || Supports(fallback);
        bool cpuOnly = config.cpuInferenceApproved;
        // note: Custom flags and host KV can bypass placement; reject incompatible overrides instead of silently weakening full-GPU policy.
        if (!string.IsNullOrWhiteSpace(config.extraLlamaServerArguments) || (!cpuOnly && config.keepKvCacheInSystemRam))
        { error = "Full-GPU placement cannot be guaranteed with custom arguments or host KV offload."; return false; }
        if (!cpuOnly && (!Supports("--n-gpu-layers") || !Supports("--fit") || !Supports("--device")))
        { error = "Runtime cannot enforce full GPU placement."; return false; }
        // note: Zero layers alone can still offload operations or KV; require a verifiable CPU-only launch when rendering consumes the GPU budget.
        if (cpuOnly && (!Supports("--device") || !Supports("--no-op-offload") ||
            !Supports("--no-kv-offload") || !Supports("--fit")))
        {
            error = "Configured llama-server cannot verify the required CPU-only rendering policy.";
            return false;
        }

        // note: Fit GPU layers within free memory while reserving the configured render budget. Do not force all live canonical generation onto CPU.
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

            if (!CanUseBoundedPromptReuse(config, helpText) && Supports("--no-cache-prompt"))
                args.Append(" --no-cache-prompt");

            string cacheRamFlag = Supports("--cache-ram")
                ? "--cache-ram"
                : Supports("-cram")
                    ? "-cram"
                    : null;

            // note: Bound recurrent-state snapshots; zero-cache prevented actual reuse in the measured hybrid models.
            AppendArgument(args, cacheRamFlag, CanUseBoundedPromptReuse(config, helpText) ? Mathf.Clamp(config.promptCacheRamMb, 0, 256).ToString() : "0");

            // note: Retain errors while suppressing per-token timing output inherited by Unity's log stream during local generation.
            AppendArgument(args, Supports("--log-verbosity") ? "--log-verbosity" : Supports("-lv") ? "-lv" : null, "1");
        }

        string layerFlag = Supports("--n-gpu-layers") ? "--n-gpu-layers" : Supports("-ngl") ? "-ngl" : null;
        AppendArgument(args, layerFlag, cpuOnly ? "0" : "999");

        if (config.enableFlashAttention && Supports("--flash-attn"))
            args.Append(" --flash-attn on");
        if (config.useQuantizedKvCache)
        {
            if (!Supports("--cache-type-k") || !Supports("--cache-type-v"))
            { error = "Configured llama-server cannot provide the requested Q8 KV cache."; return false; }
            args.Append(" --cache-type-k q8_0 --cache-type-v q8_0");
        }
        if (config.reasoningBudget >= 0)
        {
            if (!Supports("--reasoning-budget"))
            { error = "Configured llama-server cannot enforce the requested reasoning budget."; return false; }
            AppendArgument(args, "--reasoning-budget", config.reasoningBudget.ToString());
        }

        if ((config.keepKvCacheInSystemRam || cpuOnly) && Supports("--no-kv-offload"))
            args.Append(" --no-kv-offload");

        if (Supports("--fit"))
            args.Append(" --fit off");
        // note: This deployment targets the verified NVIDIA card; an unavailable CUDA device must fail, not select CPU.
        if (!cpuOnly) AppendArgument(args, "--device", "CUDA0");
        if (cpuOnly)
        {
            AppendArgument(args, "--device", "none");
            args.Append(" --no-op-offload");
        }

        if (Supports("--fit-target"))
            AppendArgument(args, "--fit-target", Mathf.Max(512, config.targetGpuHeadroomMb).ToString());

        if (Supports("--no-webui"))
            args.Append(" --no-webui");

        if (Supports("--reasoning"))
            args.Append(config.reasoningBudget > 0 ? " --reasoning on" : " --reasoning off");

        if (!string.IsNullOrWhiteSpace(config.extraLlamaServerArguments))
            args.Append(' ').Append(config.extraLlamaServerArguments.Trim());

        if (CanUseBoundedPromptReuse(config, helpText))
        {
            // note: These bounds also apply when the user disables cooperative CPU scheduling.
            if (!config.preserveGameResponsiveness)
                AppendArgument(args, Supports("--cache-ram") ? "--cache-ram" : "-cram", Mathf.Clamp(config.promptCacheRamMb, 0, 256).ToString());
            if (config.promptCacheRamMb > 0)
            {
                AppendArgument(args, "--ctx-checkpoints", Mathf.Clamp(config.promptCheckpointCount, 1, 2).ToString());
                AppendArgument(args, "--checkpoint-min-step", Mathf.Max(256, config.promptCheckpointMinTokens).ToString());
            }
            if (Supports("--no-cache-idle-slots")) args.Append(" --no-cache-idle-slots");
        }

        arguments = args.ToString().Trim();
        return true;
    }

    internal static bool CanUseBoundedPromptReuse(LLMRuntimeConfig config, string helpText)
    {
        // note: Unknown binaries, multiple slots and custom launch overrides keep the conservative non-reuse path.
        return config.enablePromptPrefixReuse && config.serverParallelSlots == 1 &&
            string.IsNullOrWhiteSpace(config.extraLlamaServerArguments) &&
            !string.IsNullOrWhiteSpace(helpText) && !helpText.StartsWith("HELP_PROBE_FAILED:", StringComparison.Ordinal) &&
            helpText.Contains("--parallel") &&
            (config.promptCacheRamMb <= 0 || (helpText.Contains("--ctx-checkpoints") && helpText.Contains("--checkpoint-min-step"))) &&
            (helpText.Contains("--cache-ram") || helpText.Contains("-cram"));
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
