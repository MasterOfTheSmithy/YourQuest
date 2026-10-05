#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

// note: An isolated headless service proves local startup without touching the player's accepted world or externally running servers.
public static class YQLlmLocalStartupVerification
{
    private static readonly List<object> checks = new List<object>();
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static AsyncOperation pending;
    private static OllamaServerProcess service;
    private static LlamaCppServerProcess llama;
    private static LLMRuntimeConfig config;
    private static GameObject clientObject;
    private static int failures;
    private static double deadline;

    public static void RunFromCommandLine()
    {
        if (!Application.isBatchMode || Application.isPlaying) throw new InvalidOperationException("Requires isolated batch Editor.");
        deadline = EditorApplication.timeSinceStartup + 240;
        routines.Push(Run());
        EditorApplication.update += Tick;
    }

    private static void Check(string name, bool pass)
    {
        checks.Add(new { name, pass });
        if (!pass) failures++;
        Debug.Log("[YQLlmLocalStartup] " + (pass ? "PASS " : "FAIL ") + name);
    }

    private static IEnumerator Run()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var localEndpoint = typeof(OllamaServerProcess).GetMethod("TryGetLocalEndpoint", flags);
        foreach (string url in new[] { "http://127.0.0.1:11434", "http://localhost:11434", "http://[::1]:11434" })
            Check("local endpoint accepted: " + url, (bool)localEndpoint.Invoke(null, new object[] { url, null }));
        foreach (string url in new[] { "http://192.0.2.1:11434", "https://localhost:11434", "http://localhost:11434/api", "bad-url" })
            Check("nonlocal or incompatible auto-start refused: " + url, !(bool)localEndpoint.Invoke(null, new object[] { url, null }));
        config = LLMRuntimeConfig.CreateRuntimeDefault();
        foreach (var category in new[] { LLMGenerationCategory.WorldGeneration, LLMGenerationCategory.NpcPopulation,
            LLMGenerationCategory.OriginGeneration, LLMGenerationCategory.StructuredState, LLMGenerationCategory.QuestGeneration, LLMGenerationCategory.Progression })
            Check("approved control route retained: " + category, config.GetBackend(category) == YQLlmBackend.LlamaCpp);

        // note: Exercise the exact queue admission decision against old but current work, then stale owner work; no profile or state is installed.
        clientObject = new GameObject("LlmQueueDetachedFixture");
        var client = clientObject.AddComponent<LLMClient>();
        var requestType = typeof(LLMClient).GetNestedType("QueuedRequest", BindingFlags.NonPublic);
        object queued = Activator.CreateInstance(requestType);
        void Set(string field, object value) => requestType.GetField(field, BindingFlags.Instance | BindingFlags.Public).SetValue(queued, value);
        Set("id", 98765L); Set("generationEpoch", -1); Set("playerStateRevision", -1L); Set("worldStateRevision", -1L);
        Set("queuedAt", Time.unscaledTime - 1000f); Set("firstQueuedAt", Time.unscaledTime - 1000f);
        Set("ownerStillCurrent", new Func<bool>(() => true));
        var abandon = typeof(LLMClient).GetMethod("ShouldAbandonQueuedRequest", BindingFlags.Instance | BindingFlags.NonPublic);
        Check("current work stays queued beyond former 90s limit", !(bool)abandon.Invoke(client, new[] { queued }));
        Set("ownerStillCurrent", new Func<bool>(() => false));
        Check("retired owner work is still superseded", (bool)abandon.Invoke(client, new[] { queued }));
        UnityEngine.Object.DestroyImmediate(clientObject); clientObject = null;
        var populationTimeout = typeof(YQGeneratedNpcPlanningService).GetMethod("ShouldTimeoutPopulationRequest", flags);
        Check("NPC budget waits while another role is active", !(bool)populationTimeout.Invoke(null, new object[] { "npc-a", "dialogue", 1000f, 180f }));
        Check("NPC budget waits while its request is queued", !(bool)populationTimeout.Invoke(null, new object[] { "npc-a", "", 1000f, 180f }));
        Check("active NPC call keeps its full execution budget", !(bool)populationTimeout.Invoke(null, new object[] { "npc-a", "npc-a", 179f, 180f }));
        Check("stalled active NPC call still times out", (bool)populationTimeout.Invoke(null, new object[] { "npc-a", "npc-a", 181f, 180f }));
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-yqLlmQueueOnly") >= 0) yield break;

        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        config.ollamaApiUrl = "http://127.0.0.1:" + port;
        service = new OllamaServerProcess();
        bool ready = false;
        string message = string.Empty;
        yield return service.EnsureReady(config, (ok, detail) => { ready = ok; message = detail; });
        Check("game adapter starts headless Ollama without desktop app: " + message, ready);
        if (ready)
        {
            var names = new HashSet<string>();
            foreach (var category in new[] { LLMGenerationCategory.Dialogue, LLMGenerationCategory.GoddessCommentary,
                LLMGenerationCategory.Summarization, LLMGenerationCategory.DialogueVerification })
            {
                string model = config.GetProfile(category).ollamaModel;
                Check("approved Ollama route retained: " + category, config.GetBackend(category) == YQLlmBackend.Ollama && !string.IsNullOrWhiteSpace(model));
                if (!names.Add(model)) continue;
                var payload = new { model, prompt = "Return only {\"status\":\"ok\"}.", stream = false, think = false, keep_alive = 0,
                    format = "json", options = new { num_ctx = 1024, num_predict = 32, num_gpu = 0, temperature = 0 } };
                using (var request = new UnityWebRequest(config.ollamaApiUrl + "/api/generate", "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload)));
                    request.downloadHandler = new DownloadHandlerBuffer(); request.SetRequestHeader("Content-Type", "application/json"); request.timeout = 60;
                    yield return request.SendWebRequest();
                    bool response = request.result == UnityWebRequest.Result.Success && !string.IsNullOrWhiteSpace(JObject.Parse(request.downloadHandler.text).Value<string>("response"));
                    Check("real approved model returns text: " + model, response);
                }
            }
        }
        // note: The selected control GGUF also starts through the project's own adapter; this tiny transport check is not NPC quality or latency certification.
        listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        config.serverPort = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        llama = new LlamaCppServerProcess(); ready = false;
        yield return llama.EnsureReady(config, (ok, detail) => { ready = ok; message = detail; });
        Check("owned approved control model becomes healthy: " + message, ready);
        if (ready)
        {
            using (var request = new UnityWebRequest(config.BuildBaseUrl() + "/v1/chat/completions", "POST"))
            {
                var payload = new { messages = new[] { new { role = "user", content = "Return only the word OK. /no_think" } }, max_tokens = 32, temperature = 0 };
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload)));
                request.downloadHandler = new DownloadHandlerBuffer(); request.SetRequestHeader("Content-Type", "application/json"); request.timeout = 60;
                yield return request.SendWebRequest();
                Check("approved control GGUF returns completion", request.result == UnityWebRequest.Result.Success &&
                    !string.IsNullOrWhiteSpace((string)JObject.Parse(request.downloadHandler.text)["choices"]?[0]?["message"]?["content"]));
            }
        }
    }

    private static void Tick()
    {
        try
        {
            // note: Editor updates pump only these isolated coroutine adapters; asynchronous HTTP never blocks Unity's main thread.
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Local startup verification deadline exceeded.");
            if (pending != null && !pending.isDone) return;
            pending = null;
            while (routines.Count > 0)
            {
                var routine = routines.Peek();
                if (!routine.MoveNext()) { routines.Pop(); continue; }
                if (routine.Current is AsyncOperation operation) { pending = operation; return; }
                if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Finish();
        }
        catch (Exception error) { Check("verification exception: " + error.Message, false); Finish(); }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        service?.Dispose(true); llama?.Dispose();
        if (clientObject != null) UnityEngine.Object.DestroyImmediate(clientObject);
        if (config != null) UnityEngine.Object.DestroyImmediate(config);
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "outputs", "LlmLocalStartup_20261005");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Receipt.json"), JsonConvert.SerializeObject(new {
            generatedAtUtc = DateTime.UtcNow, evidence = "Isolated real server startup and transport; detached queue checks; no ordinary gameplay certification", checks, failures }, Formatting.Indented));
        Debug.Log("[YQLlmLocalStartup] checks=" + checks.Count + "; failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
#endif
