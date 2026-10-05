using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// note: LLMClient remains the only scheduler. This adapter owns only the headless local service it starts.
public sealed class OllamaServerProcess
{
    private Process ownedProcess;
    private bool disposed;

    public IEnumerator EnsureReady(LLMRuntimeConfig config, Action<bool, string> completed)
    {
        if (disposed || config == null || !config.enableRuntimeLlm)
        {
            completed?.Invoke(false, "Ollama runtime is disabled or disposed.");
            yield break;
        }
        string baseUrl = (config.ollamaApiUrl ?? string.Empty).Trim().TrimEnd('/');
        bool ready = false, reachable = false;
        yield return Probe(baseUrl, (ok, present) => { ready = ok; reachable = present; });
        if (ready) { completed?.Invoke(true, "Ollama service is ready."); yield break; }

        // note: A responding service can be loading or temporarily busy; never launch a competing process on its port.
        if (!reachable && !HasLiveProcess())
        {
            if (!config.startLocalOllamaOnDemand || !TryGetLocalEndpoint(baseUrl, out string endpoint))
            {
                completed?.Invoke(false, "Configured Ollama service is unavailable: " + baseUrl);
                yield break;
            }
            string executable = ResolveExecutable(config.ollamaExecutablePath);
            if (string.IsNullOrWhiteSpace(executable))
            {
                completed?.Invoke(false, "Ollama executable not found. Configure ollamaExecutablePath or install the local Ollama CLI.");
                yield break;
            }
            if (!TryStart(executable, endpoint, config.ollamaModelsDirectory, out string error))
            {
                completed?.Invoke(false, error);
                yield break;
            }
        }
        // note: A loading/busy service can take arbitrarily long; only disposal or a known process exit ends readiness waiting.
        while (!disposed)
        {
            yield return Probe(baseUrl, (ok, present) => { ready = ok; reachable = present; });
            if (ready)
            {
                UnityEngine.Debug.Log("[OllamaServerProcess] Headless Ollama service ready at " + baseUrl + "; approved models load on request.");
                completed?.Invoke(true, "Ollama service is ready.");
                yield break;
            }
            if (ownedProcess != null && !HasLiveProcess())
            {
                // note: Another external service may win a simultaneous launch; give its health endpoint precedence over our exited process.
                if (!reachable)
                {
                    completed?.Invoke(false, "Owned Ollama service exited before becoming ready at " + baseUrl);
                    yield break;
                }
            }
            yield return new WaitForSecondsRealtime(.25f);
        }
        completed?.Invoke(false, "Ollama service owner was disposed while waiting at " + baseUrl);
    }

    internal static bool TryGetLocalEndpoint(string baseUrl, out string endpoint)
    {
        endpoint = string.Empty;
        // note: Auto-start binds only a configured loopback HTTP endpoint, never a remote service or public interface.
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp ||
            !uri.IsLoopback || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        endpoint = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    internal static string ResolveExecutable(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return File.Exists(configured.Trim()) ? configured.Trim() : null;
        string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe");
        if (File.Exists(local)) return local;
        foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            string candidate = Path.Combine(folder.Trim().Trim('"'), Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor ? "ollama.exe" : "ollama");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private bool TryStart(string executable, string endpoint, string modelDirectory, out string error)
    {
        error = string.Empty;
        try
        {
            ownedProcess?.Dispose();
            var info = new ProcessStartInfo { FileName = executable, Arguments = "serve", WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = false, CreateNoWindow = true };
            info.EnvironmentVariables["OLLAMA_HOST"] = endpoint;
            if (!string.IsNullOrWhiteSpace(modelDirectory)) info.EnvironmentVariables["OLLAMA_MODELS"] = modelDirectory;
            // note: Only the game-owned service is constrained to one runner; LLMClient remains the single request scheduler.
            info.EnvironmentVariables["OLLAMA_MAX_LOADED_MODELS"] = "1";
            info.EnvironmentVariables["OLLAMA_NUM_PARALLEL"] = "1";
            // note: Ollama itself treats a nonpositive load timeout as infinite; slow model loading must not terminate a game request.
            info.EnvironmentVariables["OLLAMA_LOAD_TIMEOUT"] = "0";
            ownedProcess = Process.Start(info);
            if (ownedProcess == null) { error = "Failed to start headless Ollama service."; return false; }
            UnityEngine.Debug.Log("[OllamaServerProcess] Started owned headless Ollama service at " + endpoint + ".");
            return true;
        }
        catch (Exception exception)
        {
            ownedProcess?.Dispose(); ownedProcess = null;
            error = "Failed to start headless Ollama: " + exception.Message;
            return false;
        }
    }

    private bool HasLiveProcess() => ownedProcess != null && !ownedProcess.HasExited;

    private static IEnumerator Probe(string baseUrl, Action<bool, bool> completed)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) { completed(false, false); yield break; }
        using (var request = UnityWebRequest.Get(baseUrl + "/api/tags"))
        {
            request.timeout = 2;
            yield return request.SendWebRequest();
            completed(request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300,
                request.responseCode > 0);
        }
    }

    public void Dispose(bool stopOwnedProcess)
    {
        disposed = true;
        if (ownedProcess == null) return;
        try
        {
            // note: Never terminate a user-managed service. Stop only this process and its model workers when the game owns them.
            if (stopOwnedProcess && !ownedProcess.HasExited) LlamaCppServerProcess.TerminateOwnedProcessTree(ownedProcess);
        }
        catch (Exception exception) { UnityEngine.Debug.LogWarning("[OllamaServerProcess] Owned service shutdown failed: " + exception.Message); }
        finally { ownedProcess.Dispose(); ownedProcess = null; }
    }
}
