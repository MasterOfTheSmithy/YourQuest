#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Explicit disposable fixtures inspect current GPU policy and content validators without selecting or publishing a profile.
[InitializeOnLoad]
public static class YQLlmRepairVerification
{
    private const string Folder = "outputs/LlmRepair_20261005";
    internal const string Marker = "Assets/Assets/EditorBuildRequests/RunLlmRepairVerification.request";
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    static YQLlmRepairVerification() { if (File.Exists(Marker)) QueueRequest(); }
    internal static void QueueRequest() { EditorApplication.update -= Consume; EditorApplication.update += Consume; }
    private static void Consume()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        // note: This disposable request hook retires once the stable import boundary is reached; it never polls during ordinary gameplay.
        EditorApplication.update -= Consume;
        if (!File.Exists(Marker)) return;
        string mode = File.ReadAllText(Marker).Trim(); AssetDatabase.DeleteAsset(Marker);
        if (mode == "planning") YQLlmSpeedVerification.VerifyPlanningInEditor();
        else if (mode == "review") Review();
        else { Export(); if (mode == "verify") Review(); }
    }
    [MenuItem("YourQuest/Verification/Export LLM Repair Fixtures")]
    public static void Export()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        Directory.CreateDirectory(Folder);
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        var checks = new List<object>();
        void Check(string name, bool pass) { checks.Add(new { name, pass }); if (!pass) throw new InvalidOperationException(name); }
        try
        {
            string help = "--model --host --port --ctx-size --parallel --threads --threads-batch --batch-size --ubatch-size --prio --poll --no-cache-prompt --cache-ram --log-verbosity --n-gpu-layers --flash-attn --no-kv-offload --fit --fit-target --no-webui --reasoning";
            object[] args = { config, help, null, null, true };
            Check("live control arguments build", (bool)typeof(LlamaCppServerProcess).GetMethod("TryBuildArguments", StaticPrivate).Invoke(null, args));
            string wireArgs = (string)args[2];
            Check("live control requests GPU fitting and rendering reserve", wireArgs.Contains("--n-gpu-layers \"999\"") && wireArgs.Contains("--fit on") && wireArgs.Contains("--fit-target \"768\"") && !wireArgs.Contains("--device \"none\""));
            var budget = typeof(LlamaCppServerProcess).GetMethod("LiveGpuLayerCount", StaticPrivate);
            long bytes = new FileInfo(config.ggufModelPath).Length;
            int Layers(int free) => (int)budget.Invoke(null, new object[] { free, config.targetGpuHeadroomMb, bytes });
            Check("occupied GPU uses bounded partial layers", Layers(1548) > 0 && Layers(1548) < 10);
            Check("no spare VRAM cannot trigger paging", Layers(512) == 0);
            Check("available GPU permits full generation offload", Layers(7895) == 999);
            File.WriteAllText(Folder + "/GpuBudget.json", JsonConvert.SerializeObject(new { measuredOccupiedFreeMb = 1548, layers = Layers(1548), bytes }));
            Check("qualified voice remains CPU and approved model", config.goddessSpeechPlanCpuOnly && config.GetProfile(LLMGenerationCategory.GoddessCommentary).ollamaModel == "smaller-test:latest");
            Check("new voice qualification pin matches current source", config.HasQualifiedGoddessSpeechPlan);
            foreach (bool locked in new[] { false, true })
            {
                bool current = true;
                var request = (YQLlmRequest)typeof(YQGeneratedNpcPlanningService).GetMethod("BuildPopulationRequest", StaticPrivate).Invoke(null,
                    new object[] { "fixture", "GeneratedNpcPopulation:fixture", new Dictionary<string, object>(), locked, new Func<bool>(() => current) });
                Check("NPC revisions independent; identity and owner guards retained: " + locked,
                    !request.bindPlayerStateRevision && !request.bindWorldStateRevision && request.generationEpoch == -1 && request.ownerStillCurrent());
                current = false;
                Check("NPC owner retirement remains observable: " + locked, !request.ownerStillCurrent());
                Check("NPC one scheduler, protected facts and bounded domain retries: " + locked, request.maxRetries == 0 && request.requireJson && request.protectPrompt &&
                    request.priority == (locked ? YQLlmRequestPriority.StartupExclusive : YQLlmRequestPriority.Background));
            }
            Check("fixed seed frontier continuity and construction", (int)typeof(YQSemanticWorldAuthorityTests).GetMethod("RunFrontierContinuationContracts", StaticPrivate).Invoke(null, null) == 0);
            var cases = new JArray();
            var wireHost = new GameObject("Detached exact Goddess wire exporter");
            try
            {
            var transport = wireHost.AddComponent<LLMClient>();
            var profile = config.GetProfile(LLMGenerationCategory.GoddessCommentary);
            foreach (JObject fixture in JArray.Parse(File.ReadAllText(Folder + "/VoiceFixtures.json")))
            {
                var evidence = YQGoddessLoadingVoice.CaptureKnownContext(fixture["player"].ToObject<PlayerState>(), fixture["world"].ToObject<WorldState>());
                var purpose = (YQGoddessSpeechPlan.Purpose)fixture.Value<int>("purpose");
                string statement = fixture.Value<string>("statement");
                // note: Rebuild every prompt and schema from current source; historical fixture replies supply no new acceptance.
                var overrides = new Dictionary<string, object> { { "num_gpu", 0 }, { "num_ctx", 4096 }, { "request_timeout_seconds", 15 }, { "presence_penalty", 0f } };
                var options = (Dictionary<string, object>)typeof(LLMClient).GetMethod("BuildEffectiveOptions", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(transport, new object[] { config, profile, overrides });
                options.Remove("request_timeout_seconds");
                string prompt = YQGoddessSpeechPlan.BuildPrompt(evidence, fixture.Value<string>("task"), purpose, statement);
                if (!LLMContextCompiler.TryCompileProtected(prompt, config, profile, Convert.ToInt32(options["num_predict"]), out var compiled, out string error))
                    throw new InvalidOperationException(error);
                string payload = (string)typeof(LLMClient).GetMethod("BuildRequestJson", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(transport,
                    new object[] { config, YQLlmBackend.Ollama, compiled.prompt, "GoddessSpeech:repair-fixture", options, profile, true,
                        YQGoddessSpeechPlan.Schema(evidence, purpose, statement) });
                cases.Add(new JObject { ["name"] = fixture["name"], ["evidence"] = evidence, ["purpose"] = (int)purpose, ["statement"] = statement,
                    ["payload"] = JObject.Parse(payload) });
            }
            }
            finally { UnityEngine.Object.DestroyImmediate(wireHost); }
            File.WriteAllText(Folder + "/VoiceCases.json", new JObject { ["contractHash"] = config.GoddessSpeechPlanContractHash, ["cases"] = cases }.ToString());
            File.WriteAllText(Folder + "/PureChecks.json", JsonConvert.SerializeObject(new { utc = DateTime.UtcNow, checks, controlArguments = wireArgs }, Formatting.Indented));
            ExportNpc(config);
            Debug.Log("[YQLlmRepair] Current fixtures exported; " + checks.Count + " pure checks passed.");
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }
    private static void ExportNpc(LLMRuntimeConfig config)
    {
        // note: Read copied canonical inputs only. No fixture manager is installed and no accepted population is replaced.
        var settings = new JsonSerializerSettings { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
        var world = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(Path.Combine(Application.persistentDataPath, "world_state.json")), settings);
        var player = JsonConvert.DeserializeObject<PlayerState>(File.ReadAllText(Path.Combine(Application.persistentDataPath, "player_state.json")), settings);
        var plan = world.generatedWorldPlan; plan.EnsureCollections();
        Type owner = typeof(YQGeneratedNpcPlanningService);
        Type targetType = owner.GetNestedType("PopulationBatchTarget", BindingFlags.NonPublic);
        var targets = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(targetType));
        owner.GetMethod("BuildBatchTargets", StaticPrivate).Invoke(null, new object[] { plan, targets, 64, 64 });
        object target = targets[0];
        string Binding() => (string)owner.GetMethod("PopulationSemanticBinding", StaticPrivate).Invoke(null, new[] { (object)plan, target });
        string initial = Binding();
        // note: These mutations belong only to the copied fixture and exercise the real NPC dependency stamp.
        plan.settlements.Add(new GeneratedSettlementRecord { settlementId = "detached-other-location" });
        bool unrelatedStable = initial == Binding(); plan.settlements.RemoveAt(plan.settlements.Count - 1);
        string summary = plan.summary; plan.summary += " changed canon"; bool canonRetires = initial != Binding(); plan.summary = summary;
        int originalCount = targets.Count;
        var extra = JsonConvert.DeserializeObject<GeneratedSettlementRecord>(JsonConvert.SerializeObject(plan.settlements[0]));
        extra.settlementId = "detached-appended-settlement"; plan.settlements.Add(extra);
        var retained = new List<GeneratedNpcPlanRecord> { new GeneratedNpcPlanRecord { npcId = "detached-pending" } };
        owner.GetMethod("AppendPopulationTargets", StaticPrivate).Invoke(null, new object[] { plan, targets, retained, 64, 64 });
        bool appendPreserves = targets.Count == originalCount + 1 && ReferenceEquals(targets[0], target) && retained.Count == 1;
        owner.GetMethod("AppendPopulationTargets", StaticPrivate).Invoke(null, new object[] { plan, targets, retained, 64, 64 });
        bool appendUnique = targets.Count == originalCount + 1;
        plan.settlements.RemoveAt(plan.settlements.Count - 1); targets.RemoveAt(targets.Count - 1);
        // note: Canonical records, including their equipment, must win over buffered copies when another frontier batch publishes.
        var mergePlan = new GeneratedWorldPlanRecord(); mergePlan.EnsureCollections();
        var accepted = new GeneratedNpcPlanRecord { npcId = "detached-pending" };
        var concurrent = new GeneratedNpcPlanRecord { npcId = "detached-concurrent" };
        mergePlan.generatedNpcs.Add(accepted); mergePlan.generatedNpcs.Add(concurrent);
        owner.GetMethod("MergeAcceptedPopulation", StaticPrivate).Invoke(null, new object[] { mergePlan, retained });
        bool mergePreserves = retained.Count == 2 && ReferenceEquals(retained[0], accepted) && ReferenceEquals(retained[1], concurrent);
        if (!unrelatedStable || !canonRetires || !appendPreserves || !appendUnique || !mergePreserves) throw new InvalidOperationException("NPC semantic dependency binding regression");
        File.WriteAllText(Folder + "/NpcBindingChecks.json", JsonConvert.SerializeObject(new { unrelatedStable, canonRetires, appendPreserves, appendUnique, mergePreserves, utc = DateTime.UtcNow }));
        int count = (int)owner.GetMethod("GetExpectedNpcCountForTarget", StaticPrivate).Invoke(null, new[] { (object)plan, target });
        string prompt = (string)owner.GetMethod("BuildPopulationPrompt", StaticPrivate).Invoke(null,
            new[] { (object)player, plan, target, targets.Count > 1 ? targets[1] : null, 1, targets.Count });
        prompt += (string)owner.GetMethod("BuildExistingCanonicalNameConstraint", StaticPrivate).Invoke(null,
            new object[] { plan.generatedNpcs, new List<string>() });
        prompt += (string)owner.GetMethod("BuildFinalCountConstraint", StaticPrivate).Invoke(null, new object[] { count });
        int output = Mathf.Clamp(620 + count * 280, 1000, 1800);
        File.WriteAllText(Folder + "/NpcRequiredPrompt.txt", prompt);
        if (!LLMContextCompiler.TryCompileProtected(prompt, config, config.GetProfile(LLMGenerationCategory.NpcPopulation), output, out var compiled, out string error))
            throw new InvalidOperationException(error);
        // note: A mode prefix may mark the compiled prompt as reduced; compare the complete required body instead of that flag.
        bool requiredPromptPreserved = compiled.prompt.EndsWith(prompt.Replace("\r\n", "\n").Replace('\r', '\n'), StringComparison.Ordinal);
        bool overflowRejects = !LLMContextCompiler.TryCompileProtected(new string('x', 40000) + "\nREQUIRED_LOCATION_ID=世界-sentinel\nOUTPUT_SCHEMA=required",
            config, config.GetProfile(LLMGenerationCategory.NpcPopulation), output, out _, out string overflowError) && overflowError.StartsWith("ContextOverflow", StringComparison.Ordinal);
        if (!requiredPromptPreserved || !overflowRejects) throw new InvalidOperationException("Required NPC prompt preservation regression");
        var host = new GameObject("Detached NPC wire fixture");
        try
        {
            var client = host.AddComponent<LLMClient>();
            var options = new Dictionary<string, object> { { "num_predict", output }, { "temperature", .55f }, { "top_p", .92f } };
            string payload = (string)typeof(LLMClient).GetMethod("BuildRequestJson", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(client,
                new object[] { config, YQLlmBackend.LlamaCpp, compiled.prompt, "GeneratedNpcPopulation:repair-fixture", options,
                    config.GetProfile(LLMGenerationCategory.NpcPopulation), true, null });
            File.WriteAllText(Folder + "/NpcCase.json", new JObject { ["payload"] = JObject.Parse(payload), ["plan"] = JObject.FromObject(plan),
                ["target"] = JObject.FromObject(target), ["expectedCount"] = count, ["reduced"] = compiled.reduced,
                ["estimatedInputTokens"] = compiled.estimatedInputTokens, ["requiredPromptPreserved"] = requiredPromptPreserved,
                ["overflowRejects"] = overflowRejects }.ToString());
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
    [MenuItem("YourQuest/Verification/Review LLM Repair Replies")]
    public static void Review()
    {
        File.WriteAllText(Folder + "/RenderMemory.json", JsonConvert.SerializeObject(new { totalMb = SystemInfo.graphicsMemorySize,
            allocatedDriverBytes = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver(), utc = DateTime.UtcNow }));
        var cases = JObject.Parse(File.ReadAllText(Folder + "/VoiceCases.json"));
        var replies = JArray.Parse(File.ReadAllText(Folder + "/VoiceReplies.json"));
        var activeConfig = LLMRuntimeConfig.CreateRuntimeDefault();
        bool currentContract = activeConfig.HasQualifiedGoddessSpeechPlan && cases.Value<string>("contractHash") == activeConfig.GoddessSpeechPlanContractHash;
        string digest = activeConfig.goddessSpeechPlanModelDigest;
        UnityEngine.Object.DestroyImmediate(activeConfig);
        var checks = new JArray(); int failed = 0;
        foreach (JObject fixture in (JArray)cases["cases"])
        {
            JObject reply = null; foreach (JObject candidate in replies) if (candidate.Value<string>("name") == fixture.Value<string>("name")) { reply = candidate; break; }
            string line = null, error = "Missing reply";
            bool pass = currentContract && reply != null && reply.Value<string>("model") == "smaller-test:latest" && reply.Value<string>("digest") == digest &&
                reply.Value<string>("error") == null && YQGoddessSpeechPlan.TryCompose(reply.Value<string>("raw"),
                (JObject)fixture["evidence"], out line, out error, (YQGoddessSpeechPlan.Purpose)fixture.Value<int>("purpose"), fixture.Value<string>("statement"));
            checks.Add(new JObject { ["name"] = fixture["name"], ["pass"] = pass, ["line"] = line, ["error"] = pass ? null : error });
            if (!pass) failed++;
        }
        File.WriteAllText(Folder + "/VoiceReview.json", new JObject { ["utc"] = DateTime.UtcNow, ["contractHash"] = cases["contractHash"],
            ["evidence"] = "Current production composition validators; exact production CPU request options; no player publication or audible delivery proof",
            // note: Bind validation to these actual replies and the freshly loaded production assembly, not an earlier fixture run.
            ["assemblyMvid"] = typeof(YQGoddessSpeechPlan).Module.ModuleVersionId.ToString(),
            ["casesHash"] = YQRepairEpisode.Hash(File.ReadAllText(Folder + "/VoiceCases.json")),
            ["repliesHash"] = YQRepairEpisode.Hash(File.ReadAllText(Folder + "/VoiceReplies.json")),
            ["currentContract"] = currentContract, ["checks"] = checks, ["failures"] = failed }.ToString());
        Debug.Log("[YQLlmRepair] Voice review: " + checks.Count + " cases; failures=" + failed);
        if (File.Exists(Folder + "/NpcReply.json"))
        {
            var fixture = JObject.Parse(File.ReadAllText(Folder + "/NpcCase.json"));
            var reply = JObject.Parse(File.ReadAllText(Folder + "/NpcReply.json"));
            Type owner = typeof(YQGeneratedNpcPlanningService);
            var plan = fixture["plan"].ToObject<GeneratedWorldPlanRecord>();
            var target = fixture["target"].ToObject(owner.GetNestedType("PopulationBatchTarget", BindingFlags.NonPublic));
            object[] args = { reply.Value<string>("raw"), plan, target, plan.generatedNpcs, null, null, null };
            bool pass = (bool)owner.GetMethod("TryParsePopulationBatch", StaticPrivate).Invoke(null, args);
            File.WriteAllText(Folder + "/NpcReview.json", JsonConvert.SerializeObject(new { utc = DateTime.UtcNow, pass, error = args[6],
                expectedCount = fixture["expectedCount"], acceptedCount = args[4] is IList npcs ? npcs.Count : 0,
                assemblyMvid = owner.Module.ModuleVersionId.ToString(),
                caseHash = YQRepairEpisode.Hash(File.ReadAllText(Folder + "/NpcCase.json")),
                replyHash = YQRepairEpisode.Hash(File.ReadAllText(Folder + "/NpcReply.json")),
                evidence = "Actual GPU-fit reply through current canonical NPC count, identity, role and coverage parser; no profile publication" }, Formatting.Indented));
            Debug.Log("[YQLlmRepair] NPC canonical review: " + pass + "; " + args[6]);
        }
    }
}
// note: Only an explicitly imported repair request installs the short-lived fixture callback; ordinary editor frames have no poller.
public sealed class YQLlmRepairRequestPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] previous)
    {
        if (Array.IndexOf(imported, YQLlmRepairVerification.Marker) >= 0) YQLlmRepairVerification.QueueRequest();
    }
}
#endif
