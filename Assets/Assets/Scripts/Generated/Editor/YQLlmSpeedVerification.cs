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

    [MenuItem("YourQuest/Verification/Verify Streaming Planning Contracts")]
    public static void VerifyPlanningInEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || LLMClient.Instance != null)
            throw new InvalidOperationException("An idle Editor without a live model owner is required.");
        checks.Clear(); responses.Clear(); failures = 0;
        host = new GameObject("Detached LLM speed verification"); client = host.AddComponent<LLMClient>();
        config = LLMRuntimeConfig.CreateRuntimeDefault(); client.runtimeConfig = config;
        Set("_usingRuntimeDefaultConfig", false); Set("_activeConfig", config);
        try { VerifyPlanningContracts(); VerifyPopulationPublicationContracts(); }
        catch (Exception error) { Check("planning contract exception: " + error, false); }
        finally
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            host = null; client = null; config = null;
        }
        // note: Isolated in-memory fixtures exercise the actual scheduling/publication helpers without a profile, model call, scene import or gameplay claim.
        const string folder = "outputs/G08_Environment_Repair_20261005";
        Directory.CreateDirectory(folder);
        string path = folder + "/StreamingPlanning_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json";
        File.WriteAllText(path, JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow.ToString("O"), evidence = "DETACHED_PLANNING_AND_POPULATION_PUBLICATION_CONTRACTS",
            runtimeMvid = typeof(LLMClient).Assembly.ManifestModule.ModuleVersionId,
            editorMvid = typeof(YQLlmSpeedVerification).Assembly.ManifestModule.ModuleVersionId, checks, failures,
            limits = "No live model latency, paired disk publication, ordinary player traversal or NPC/equipment appearance acceptance."
        }, Formatting.Indented));
        Debug.Log("[YQLlmSpeed] Planning contracts failures=" + failures + " receipt=" + path);
    }

    private static void VerifyPlanningContracts()
    {
        // note: A later startup reveal must not relabel the launch policy captured before the asynchronous model load.
        MethodInfo residency = typeof(LLMClient).GetMethod("BuildLlamaResidencyKey", PrivateStatic);
        string startupResidency = (string)residency.Invoke(null, new object[] { config, false });
        string liveResidency = (string)residency.Invoke(null, new object[] { config, true });
        Check("startup and live GPU policies have distinct residency keys", startupResidency != liveResidency);
        Check("captured startup residency remains stable across later policy reads", startupResidency == (string)residency.Invoke(null, new object[] { config, false }));
        VerifyFrontierNetworkAdmissionContracts();
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
        Check("long frontier inference expands the retained planning horizon", Distance(5.4f, 667f) > Distance(5.4f, 300f));
        Check("cold NPC forecast accommodates the observed long partial-GPU work", client.GetPlanningLeadTimeSeconds(LLMGenerationCategory.NpcPopulation) >= 610f);
        Invoke("RecordServiceLatency", LLMGenerationCategory.NpcPopulation, "GeneratedNpcPopulation:test", 667f);
        Check("NPC observations are no longer truncated at 300 seconds", client.GetPlanningLeadTimeSeconds(LLMGenerationCategory.NpcPopulation) > 1000f);
        Check("overdue active work retains a planning margin", (float)typeof(LLMClient).GetMethod("RemainingPlanningServiceSeconds", PrivateStatic)
            .Invoke(null, new object[] { 180f, 667f }) >= 333f);
        Invoke("RecordServiceLatency", LLMGenerationCategory.NpcPopulation, "GeneratedNpcPopulation:test", 2000f);
        Check("long observations still use the rolling service average", Mathf.Abs(client.GetPlanningLeadTimeSeconds(LLMGenerationCategory.NpcPopulation) - 1605.325f) < .01f);
        Invoke("RecordServiceLatency", LLMGenerationCategory.NpcPopulation, "GeneratedNpcPopulation:test", 2000f);
        Check("planning horizon remains finite without expiring requests", client.GetPlanningLeadTimeSeconds(LLMGenerationCategory.NpcPopulation) == 1800f);
        float Buffer(float speed, float lead) => (float)typeof(YQGeneratedNpcPlanningService).GetMethod("PopulationPlanningDistance", PrivateStatic)
            .Invoke(null, new object[] { speed, lead, 512f });
        float buffer = Buffer(5.4f, 600f);
        Check("remote NPC work remains outside the ordinary origin buffer", buffer >= 1024f && buffer < 4800f);
        Check("NPC buffer remains bounded for extreme travel and inference", Buffer(1000f, 100000f) == 4096f);
        object site = new YQSpatialMaterializationSiteV2 { x = 4096f, z = 0f, reservedRadius = 72f };
        var memberField = typeof(YQSpatialMaterializationSiteV2).GetField("memberFootprint", BindingFlags.NonPublic | BindingFlags.Instance);
        memberField.SetValue(site, new[] { new YQSiteMemberFootprintV2 { x = 768f, z = 0f, reservedRadius = 32f } });
        float Clearance() => (float)typeof(YQGeneratedNpcPlanningService).GetMethod("PopulationSiteClearance", PrivateStatic)
            .Invoke(null, new object[] { Vector2.zero, site });
        Check("NPC planning includes the accepted member sector", Mathf.Approximately(Clearance(), 736f));
        memberField.SetValue(site, Array.Empty<YQSiteMemberFootprintV2>());
        Check("central reserve alone keeps a remote site outside a smaller buffer", Clearance() > 1024f);
    }

    private static void VerifyFrontierNetworkAdmissionContracts()
    {
        // note: Exercise the existing owner's bounded refusal cache without activating streaming, generation or persistence.
        var ownerObject = new GameObject("Detached frontier network admission");
        try
        {
            var owner = ownerObject.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
            Type type = typeof(YQPlayerFollowingSemanticChunkStreamer);
            var refusals = (Dictionary<Vector2Int, string>)type.GetField("_frontierNetworkRefusals", PrivateInstance).GetValue(owner);
            var attempts = (Dictionary<Vector2Int, int>)type.GetField("_frontierConstructionAttempts", PrivateInstance).GetValue(owner);
            MethodInfo track = type.GetMethod("TrackFrontierConstructionAttempt", PrivateInstance);
            MethodInfo current = type.GetMethod("IsFrontierNetworkRefusalCurrent", PrivateInstance);
            MethodInfo topology = type.GetMethod("IsTopologyDependentFrontierPlacementFailure", PrivateStatic);
            Vector2Int block = new Vector2Int(3, -4);
            track.Invoke(owner, new object[] { block, 1 }); refusals[block] = "accepted-network-1";
            Check("unchanged accepted network suppresses repeated disconnected preflight", (bool)current.Invoke(owner, new object[] { block, "accepted-network-1" }));
            Check("published parent network reconsiders its refused neighbor", !(bool)current.Invoke(owner, new object[] { block, "accepted-network-2" }) && !refusals.ContainsKey(block));
            Check("network reconsideration retains spent substantive model attempts", attempts[block] == 1);
            Check("only connecting-road failures use topology invalidation", (bool)topology.Invoke(null, new object[] { "frontier frontage has no real accepted road within 1536 metres" }) &&
                !(bool)topology.Invoke(null, new object[] { "frontier reserve exceeds its slope contract" }));
            refusals[block] = "accepted-network-2";
            for (int index = 0; index < 129; index++) track.Invoke(owner, new object[] { new Vector2Int(index + 10, 0), 0 });
            Check("physical refusals share the existing bounded opportunity lifetime", !refusals.ContainsKey(block) && attempts.Count == 128);
        }
        finally { UnityEngine.Object.DestroyImmediate(ownerObject); }
    }

    private static void VerifyPopulationPublicationContracts()
    {
        var plan = new GeneratedWorldPlanRecord { worldSeed = "planning-publication-fixture" };
        var world = new WorldState { generatedWorldPlan = plan, canonLedger = "Retained canon" };
        var retained = new GeneratedNpcPlanRecord { npcId = "npc:retained", displayName = "Retained", settlementId = "settlement:retained" };
        var existing = new WorldState.NpcRecord { npcId = retained.npcId, name = retained.displayName, status = "dead" };
        plan.generatedNpcs.Add(retained); world.npcs.Add(existing);
        var originalPopulation = plan.generatedNpcs; var originalRuntime = world.npcs; var originalIdentities = world.identityRecords;
        var proposed = new GeneratedNpcPlanRecord { npcId = "npc:new", displayName = "New", settlementId = "settlement:new" };
        var records = new List<GeneratedNpcPlanRecord> { retained, proposed };
        bool Publish(List<GeneratedNpcPlanRecord> values, Func<bool> publisher, string canon = null)
        {
            // note: Editor fixtures invoke the internal production helper without widening its runtime API or changing assembly visibility.
            object[] arguments = { plan, world, values, publisher, null, canon };
            return (bool)typeof(YQGeneratedNpcPlanningService).GetMethod("TryPublishPopulationAppend", PrivateStatic).Invoke(null, arguments);
        }
        bool Refuse()
        {
            YQStateIdentity.EnsureIdentity(world.identityRecords, proposed.npcId, YQStableEntityKind.Npc, "fixture-world", proposed.displayName, 0);
            return false;
        }
        Check("paired refusal leaves the accepted proposal available for retry",
            !Publish(records, Refuse) && records.Count == 2);
        Check("refused append restores canonical and runtime list owners",
            ReferenceEquals(plan.generatedNpcs, originalPopulation) && ReferenceEquals(world.npcs, originalRuntime) && world.npcs.Count == 1);
        Check("refused append restores the identity table and canon",
            ReferenceEquals(world.identityRecords, originalIdentities) && world.identityRecords.Count == 0 && world.canonLedger == "Retained canon");
        Check("refused append preserves retained NPC status and flags", existing.status == "dead" && !world.globalFlags.ContainsKey("worldplan:generated_npcs"));
        long priorRevision = world.stateRevision, priorUpdated = world.lastUpdatedUnix;
        Check("refused final canon restores revision and timestamp", !Publish(records, Refuse, "Completed fixture population") &&
            world.stateRevision == priorRevision && world.lastUpdatedUnix == priorUpdated && world.canonLedger == "Retained canon");
        bool Accept()
        {
            YQStateIdentity.EnsureIdentity(world.identityRecords, proposed.npcId, YQStableEntityKind.Npc, "fixture-world", proposed.displayName, 0);
            return plan.generatedNpcs.Count == 2 && world.npcs.Count == 2 && world.globalFlags["worldplan:generated_npcs"] == 2;
        }
        Check("completed batch exposes both canonical and runtime records to the paired publisher",
            Publish(records, Accept));
        Check("successful partial publication advances the canonical revision once", world.stateRevision == priorRevision + 1);
        Check("successful append retains accepted object identities and death state",
            ReferenceEquals(plan.generatedNpcs[0], retained) && ReferenceEquals(world.npcs[0], existing) && existing.status == "dead");
        bool called = false;
        var replacement = new GeneratedNpcPlanRecord { npcId = retained.npcId, displayName = "Unauthorized replacement" };
        Check("accepted ID matches cannot overwrite retained content",
            Publish(new List<GeneratedNpcPlanRecord> { replacement },
                () => { called = true; return true; }) && !called && retained.displayName == "Retained");
        Check("duplicate proposed IDs remain rejected", !Publish(new List<GeneratedNpcPlanRecord> { proposed, proposed }, () => true));
        var third = new GeneratedNpcPlanRecord { npcId = "npc:third", displayName = "Third", settlementId = "settlement:third" };
        Check("publisher exceptions roll back provisional NPCs", !Publish(new List<GeneratedNpcPlanRecord> { third },
            () => throw new InvalidOperationException("fixture publisher failure")) &&
            plan.generatedNpcs.Count == 2 && world.npcs.Count == 2);
        priorRevision = world.stateRevision; priorUpdated = world.lastUpdatedUnix;
        Check("throwing final publisher rolls back canon and mutation stamps", !Publish(new List<GeneratedNpcPlanRecord> { third },
            () => throw new InvalidOperationException("fixture final publisher failure"), "Final fixture canon") &&
            world.stateRevision == priorRevision && world.lastUpdatedUnix == priorUpdated && world.canonLedger == "Retained canon");

        // note: Scoped publication can certify a completed location without relaxing final whole-world coverage.
        var coveragePlan = new GeneratedWorldPlanRecord();
        coveragePlan.encampments.Add(new GeneratedEncampmentRecord { encampmentId = "camp:one", displayName = "One" });
        coveragePlan.encampments.Add(new GeneratedEncampmentRecord { encampmentId = "camp:two", displayName = "Two" });
        var commander = new GeneratedNpcPlanRecord { npcId = "npc:commander", displayName = "Commander", hostile = true, encampmentId = "camp:one" };
        var targetType = typeof(YQGeneratedNpcPlanningService).GetNestedType("PopulationBatchTarget", BindingFlags.NonPublic);
        var targetListType = typeof(List<>).MakeGenericType(targetType);
        var targets = (IList)Activator.CreateInstance(targetListType);
        object Target(string id)
        {
            var target = Activator.CreateInstance(targetType);
            targetType.GetField("kind").SetValue(target, Enum.Parse(targetType.GetField("kind").FieldType, "Encampment"));
            targetType.GetField("locationId").SetValue(target, id); return target;
        }
        targets.Add(Target("camp:one"));
        var validate = typeof(YQGeneratedNpcPlanningService).GetMethod("ValidateCoverage", PrivateStatic);
        object[] args = { coveragePlan, new List<GeneratedNpcPlanRecord> { commander }, targets, null };
        Check("completed location coverage stays strict and independently publishable", (bool)validate.Invoke(null, args));
        targets.Add(Target("camp:two"));
        Check("missing distant location still fails final full coverage", !(bool)validate.Invoke(null, args));
    }

    private static IEnumerator Run()
    {
        host = new GameObject("Detached LLM speed verification"); client = host.AddComponent<LLMClient>();
        config = LLMRuntimeConfig.CreateRuntimeDefault(); client.runtimeConfig = config;
        Set("_usingRuntimeDefaultConfig", false); Set("_activeConfig", config);
        VerifyPlanningContracts(); VerifyPopulationPublicationContracts();
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
