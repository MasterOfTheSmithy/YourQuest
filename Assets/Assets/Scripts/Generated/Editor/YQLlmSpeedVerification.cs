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

// note: Detached timing/queue contracts and actual owned-server handoffs never install or publish a player profile.
public static class YQLlmSpeedVerification
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly List<object> checks = new List<object>();
    private static readonly List<object> responses = new List<object>();
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static AsyncOperation pending;
    private static GameObject host;
    private static LLMRuntimeConfig config;
    private static LLMClient client;
    private static int failures;
    private static double deadline;

    public static void RunFromCommandLine()
    {
        if (!Application.isBatchMode || Application.isPlaying) throw new InvalidOperationException("Isolated idle batch required.");
        deadline = EditorApplication.timeSinceStartup + 240;
        routines.Push(Run());
        EditorApplication.update += Tick;
    }

    private static object Invoke(string method, params object[] args)
        => typeof(LLMClient).GetMethod(method, PrivateInstance).Invoke(client, args);
    private static void Set(string field, object value) => typeof(LLMClient).GetField(field, PrivateInstance).SetValue(client, value);
    private static void Check(string name, bool pass)
    {
        checks.Add(new { name, pass }); if (!pass) failures++;
        Debug.Log("[YQLlmSpeed] " + (pass ? "PASS " : "FAIL ") + name);
    }

    private static IEnumerator Run()
    {
        host = new GameObject("Detached LLM speed verification"); client = host.AddComponent<LLMClient>();
        config = LLMRuntimeConfig.CreateRuntimeDefault(); client.runtimeConfig = config;
        Set("_usingRuntimeDefaultConfig", false); Set("_activeConfig", config);
        // note: Forecasts separate frontier work from full world generation and include all queues without changing their admission/order.
        Check("cold frontier includes measured CPU budget", client.GetPlanningLeadTimeSeconds(LLMGenerationCategory.WorldGeneration, "FrontierLocationBrief") >= 190f);
        Invoke("RecordServiceLatency", LLMGenerationCategory.WorldGeneration, "FrontierLocationBrief:test", 170f);
        Check("slow frontier expands lead", client.GetPlanningLeadTimeSeconds(LLMGenerationCategory.WorldGeneration, "FrontierLocationBrief") >= 265f);
        Check("full world estimate remains independent", Mathf.Approximately(client.GetPlanningLeadTimeSeconds(LLMGenerationCategory.WorldGeneration), 105f));
        Invoke("RecordServiceLatency", LLMGenerationCategory.WorldGeneration, "FrontierLocationBrief:test", 10f);
        float forecast = client.GetPlanningLeadTimeSeconds(LLMGenerationCategory.WorldGeneration, "FrontierLocationBrief");
        Check("rolling average avoids one fast sample erasing slow work", forecast > 170f && forecast < 265f);
        var requestType = typeof(LLMClient).GetNestedType("QueuedRequest", BindingFlags.NonPublic);
        object queued = Activator.CreateInstance(requestType);
        requestType.GetField("category").SetValue(queued, LLMGenerationCategory.WorldGeneration);
        requestType.GetField("debugTag").SetValue(queued, "WorldPlanGeneration:test");
        foreach (string field in new[] { "_exclusiveQueue", "_highPriorityQueue", "_normalQueue" })
        {
            object queue = typeof(LLMClient).GetField(field, PrivateInstance).GetValue(client);
            queue.GetType().GetMethod("Enqueue").Invoke(queue, new[] { queued });
            Check(field + " backlog expands bounded forecast", client.GetPlanningLeadTimeSeconds(LLMGenerationCategory.WorldGeneration, "FrontierLocationBrief") > forecast);
            queue.GetType().GetMethod("Clear").Invoke(queue, null);
        }
        Set("_activeRequest", queued); Set("_activeRequestValid", true); Set("_activeRequestStartedAt", Time.unscaledTime);
        Check("active inference remaining time included", client.GetPlanningLeadTimeSeconds(LLMGenerationCategory.WorldGeneration, "FrontierLocationBrief") > forecast);
        Set("_activeRequestValid", false);
        var distance = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod("FrontierPlanningDistance", PrivateStatic);
        float Distance(float speed, float lead) => (float)distance.Invoke(null, new object[] { speed, lead, 128f, 4 });
        Check("planning clears visible terrain footprint", Distance(5f, 130f) > 512f);
        Check("slower inference plans farther ahead", Distance(5f, 170f) > Distance(5f, 20f));
        Check("faster travel plans farther ahead", Distance(10f, 100f) > Distance(5f, 100f));
        Check("planning distance remains bounded", Distance(1000f, 1000f) == 3840f && Distance(-1f, -1f) >= 512f);
        var resident = typeof(LLMClient).GetMethod("TryOllamaResident", PrivateStatic);
        foreach (string bad in new[] { "{}", "bad", "{\"models\":[null]}", "{\"models\":[{}]}", "{\"models\":[{\"name\":{}}]}" })
        {
            object[] args = { bad, "small-test", false };
            Check("malformed residency cannot authorize reload: " + bad, !(bool)resident.Invoke(null, args));
        }
        object[] absent = { "{\"models\":[]}", "small-test", false };
        Check("valid empty residency confirms release", (bool)resident.Invoke(null, absent) && !(bool)absent[2]);
        object[] present = { "{\"models\":[{\"name\":\"small-test:latest\"}]}", "small-test", false };
        Check("default tag alias stays resident", (bool)resident.Invoke(null, present) && (bool)present[2]);
        var worldProfile = config.GetProfile(LLMGenerationCategory.WorldGeneration);
        var wireOptions = new Dictionary<string, object> { { "num_predict", 1800 } };
        JObject Wire(string tag) => JObject.Parse((string)Invoke("BuildRequestJson", config, YQLlmBackend.LlamaCpp, "fixture", tag,
            wireOptions, worldProfile, true, new Dictionary<string, object> { { "type", "object" } }));
        Check("frontier brief disables hidden reasoning", !(bool)Wire("FrontierLocationBrief:test")["chat_template_kwargs"]["enable_thinking"]);
        Check("full world plan preserves approved reasoning", (bool)Wire("WorldPlanGeneration:test")["chat_template_kwargs"]["enable_thinking"]);
        Check("frontier keeps grammar and output budget", Wire("FrontierLocationBrief:test")["json_schema"] != null && (int)Wire("FrontierLocationBrief:test")["max_tokens"] == 1800);
        string residencyKey = (string)Invoke("LlamaResidencyKey", config);
        string gguf = config.ggufModelPath; config.ggufModelPath += ".different";
        Check("different GGUF forces owned process handoff", residencyKey != (string)Invoke("LlamaResidencyKey", config)); config.ggufModelPath = gguf;
        int context = config.contextSizeTokens; config.contextSizeTokens += 128;
        Check("changed context forces owned process handoff", residencyKey != (string)Invoke("LlamaResidencyKey", config)); config.contextSizeTokens = context;
        ReviewTimingResponses();
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-yqLlmFrontierContracts") >= 0)
            Check("existing fixed-seed frontier/continuity/admission regressions", (int)typeof(YQSemanticWorldAuthorityTests)
                .GetMethod("RunConstructionContractSuites", PrivateStatic).Invoke(null, null) == 0);
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-yqLlmSpeedContractsOnly") >= 0) yield break;

        config.serverPort = FreePort(); config.ollamaApiUrl = "http://127.0.0.1:" + FreePort();
        config.gpuLayerCount = 0; config.extraLlamaServerArguments = "--device none --no-op-offload --no-kv-offload --fit off";
        bool ready = false; string failure = string.Empty;
        yield return (IEnumerator)Invoke("EnsureLlamaCppReady", config, new Action<bool, string>((ok, reason) => { ready = ok; failure = reason; }));
        Check("owned control healthy before switch: " + failure, ready);
        var llama = (LlamaCppServerProcess)typeof(LLMClient).GetField("_llamaServer", PrivateInstance).GetValue(client);
        Check("isolated control belongs to tested scheduler", llama != null && llama.OwnsProcess);
        var dialogue = config.GetProfile(LLMGenerationCategory.Dialogue);
        yield return Handoff(YQLlmBackend.Ollama, dialogue);
        Check("control process exited before Ollama load", !llama.OwnsProcess);
        yield return (IEnumerator)Invoke("EnsureOllamaReady", config, new Action<bool, string>((ok, reason) => { ready = ok; failure = reason; }));
        Check("headless owned Ollama ready: " + failure, ready);
        yield return Load(dialogue.ollamaModel);
        yield return Handoff(YQLlmBackend.Ollama, dialogue);
        yield return CheckResidency(dialogue.ollamaModel, true, "same-model request retains warm runner");
        yield return Handoff(YQLlmBackend.Ollama, config.GetProfile(LLMGenerationCategory.GoddessCommentary));
        yield return CheckResidency(dialogue.ollamaModel, false, "different-model handoff acknowledges former runner absent");
        string goddess = config.GetProfile(LLMGenerationCategory.GoddessCommentary).ollamaModel;
        yield return Load(goddess);
        yield return Handoff(YQLlmBackend.LlamaCpp, config.GetProfile(LLMGenerationCategory.Default));
        yield return CheckResidency(goddess, false, "Ollama runner absent before returning to control");
        yield return (IEnumerator)Invoke("EnsureLlamaCppReady", config, new Action<bool, string>((ok, reason) => { ready = ok; failure = reason; }));
        Check("control reload healthy after acknowledged Ollama release: " + failure, ready && llama.OwnsProcess);
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;
    }
    private static IEnumerator Handoff(YQLlmBackend backend, LLMGenerationProfile profile)
    {
        bool ready = false; string failure = string.Empty;
        yield return (IEnumerator)Invoke("PrepareRoutedModelResidency", config, backend, profile,
            new Action<bool, string>((ok, reason) => { ready = ok; failure = reason; }));
        Check("production handoff permits " + backend + ": " + failure, ready);
    }
    private static IEnumerator Load(string model)
    {
        // note: Tiny real transport probes only establish resident-runner lifecycle; these replies are not content-quality qualification.
        using (var request = new UnityWebRequest(config.ollamaApiUrl + "/api/generate", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { model, prompt = "Say OK.",
                stream = false, think = false, keep_alive = "5m", options = new { num_gpu = 0, num_ctx = 1024, num_predict = 8 } })));
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 60; request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest(); Check("real model loaded: " + model, request.result == UnityWebRequest.Result.Success);
            Set("_residentOllamaModel", model); Set("_residentOllamaBaseUrl", config.ollamaApiUrl);
        }
    }
    private static IEnumerator CheckResidency(string model, bool expected, string label)
    {
        using (var request = UnityWebRequest.Get(config.ollamaApiUrl + "/api/ps"))
        {
            yield return request.SendWebRequest(); object[] args = { request.downloadHandler.text, model, false };
            bool valid = request.result == UnityWebRequest.Result.Success && (bool)typeof(LLMClient).GetMethod("TryOllamaResident", PrivateStatic).Invoke(null, args);
            Check(label, valid && (bool)args[2] == expected);
        }
    }

    private static void ReviewTimingResponses()
    {
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "outputs/LlmTiming_20261005");
        var cases = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(folder, "Cases.json")))["cases"];
        var settings = new JsonSerializerSettings { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
        var world = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(Path.Combine(folder, "ProfileFixture/world_state.json")), settings);
        var normalize = typeof(LLMClient).GetMethod("TryNormalizeJsonObject", PrivateStatic);
        var prepare = typeof(YQWorldGenerationService).GetMethod("TryPrepareFrontierLocationBrief", PrivateStatic);
        foreach (JObject test in cases)
        {
            string name = test.Value<string>("name"); string safe = string.Empty;
            foreach (char c in name) safe += char.IsLetterOrDigit(c) ? c : '_';
            foreach (string path in Directory.GetFiles(folder, safe + "_*.json"))
            {
                var reply = JObject.Parse(File.ReadAllText(path));
                string raw = test.Value<string>("backend") == "LlamaCpp" ? (string)reply["choices"]?[0]?["message"]?["content"] : (string)reply["response"];
                object[] args = { raw, null, null }; bool normalized = (bool)normalize.Invoke(null, args);
                bool? prepared = null; string reason = (string)args[2];
                if (name.StartsWith("Frontier ", StringComparison.Ordinal) && normalized)
                {
                    string prompt = (string)test["payload"]["messages"][0]["content"];
                    if (Path.GetFileName(path).Contains("-production"))
                    {
                        var productionCases = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(folder, "Repaired/Cases.json")))["cases"];
                        foreach (JObject productionCase in productionCases)
                            if (productionCase.Value<string>("name") == name) prompt = (string)productionCase["payload"]["messages"][0]["content"];
                    }
                    if (Path.GetFileName(path).Contains("-direct")) prompt = prompt.Replace("Keep prose compact, preferably 3-8 words per field.",
                        "Prefer 2-3 distinct residents in an initial settlement brief. Keep prose compact, preferably 3-8 words per field.");
                    int start = prompt.IndexOf("ACCEPTED_CANON\n", StringComparison.Ordinal) + "ACCEPTED_CANON\n".Length;
                    int end = prompt.IndexOf("\nJSON_SCHEMA", start, StringComparison.Ordinal);
                    string id = JObject.Parse(prompt.Substring(start, end - start)).Value<string>("siteId");
                    var candidate = world.generatedWorldPlan.spatialPlanV2.acceptedContinuation.locations.Find(entry => entry.anchor.siteId == id);
                    if (candidate == null) throw new InvalidOperationException("Timing footprint fixture mismatch: " + id);
                    object[] proposal = { args[1], prompt, JsonConvert.SerializeObject(candidate, settings), (JObject)test["payload"]["json_schema"], null, null };
                    prepared = (bool)prepare.Invoke(null, proposal); reason = (string)proposal[5];
                    if (Path.GetFileName(path).Contains("-direct") || Path.GetFileName(path).Contains("-production"))
                        Check("optimized brief passes unchanged canonical preparation: " + Path.GetFileName(path) + ": " + reason, prepared == true);
                }
                responses.Add(new { file = Path.GetFileName(path), name, normalized, frontierProposalPrepared = prepared, reason });
            }
        }
        Check("timing responses reviewed with canonical normalization", responses.Count > 0);
    }

    private static void Tick()
    {
        try
        {
            // note: Editor updates drive the project's actual asynchronous adapters without entering Play Mode or writing saves.
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Verification deadline exceeded.");
            if (pending != null && !pending.isDone) return; pending = null;
            while (routines.Count > 0)
            {
                var routine = routines.Peek(); if (!routine.MoveNext()) { routines.Pop(); continue; }
                if (routine.Current is AsyncOperation operation) { pending = operation; return; }
                if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; } return;
            }
            Finish();
        }
        catch (Exception error) { Check("verification exception: " + error, false); Finish(); }
    }
    private static void Finish()
    {
        EditorApplication.update -= Tick;
        // note: Detached Editor components may never receive Awake/OnDestroy; release the exact adapters explicitly before exiting the batch.
        if (client != null) Invoke("DisposeOwnedRuntime");
        if (host != null) UnityEngine.Object.DestroyImmediate(host);
        if (config != null) UnityEngine.Object.DestroyImmediate(config);
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "outputs/LlmTiming_20261005/Verification.json"), JsonConvert.SerializeObject(new {
            generatedAtUtc = DateTime.UtcNow, evidence = "Detached planning contracts, canonical response preparation and real owned-server handoff; no gameplay frame-time or new role qualification", checks, responses, failures }, Formatting.Indented));
        Debug.Log("[YQLlmSpeed] checks=" + checks.Count + "; failures=" + failures); EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
#endif
