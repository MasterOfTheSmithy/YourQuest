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

// note: Explicit, disposable compatibility checks never apply an offer, select a save or qualify a voice.
[InitializeOnLoad]
public static class YQLlmRoleRoutingVerification
{
    private const string Folder = "outputs/LlmGpuOnly_20261008";
    private const string Marker = "Assets/Assets/EditorBuildRequests/YQ_LLM_ROLE_VERIFY.request";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly JsonSerializerSettings SnapshotSettings = new JsonSerializerSettings
    { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
    private static double nextCheck;
    // note: Asset import can precede this type's initialization. A throttled explicit-marker check avoids losing an authorized verification request.
    static YQLlmRoleRoutingVerification() { Queue(); }
    private static void Queue() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }
    internal static void RequestImported() => Queue();
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Marker) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action = File.ReadAllText(Marker).Trim();
        AssetDatabase.DeleteAsset(Marker);
        try { if (action == "practical") VerifyPracticalSpeech(); else if (action == "practicallive") YQLlmGameplayAcceptance.BeginPracticalSpeechResume(); else if (action == "patchreplay") ReplaySpeechPatches(); else if (action == "patch") VerifySpeechPatch(); else if (action == "npc") YQLlmGameplayAcceptance.BeginAssistedNpcResume(); else if (action == "reaction") VerifyWorldReaction(); else if (action == "reimplement") VerifyReimplementation(); else if (action == "feel") VerifyGameplayFeel(); else if (action == "review") Review(); else Export(); }
        catch (Exception error) { Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/EditorFailure.txt", error.ToString()); Debug.LogException(error); }
    }

    public static void VerifySpeechPatch()
    {
        // note: Pure patch checks and exported real prompts do not qualify a model, touch a profile, or dispatch inference.
        const string folder = "outputs/SpeechPatch_20261008";
        Directory.CreateDirectory(folder);
        var checks = new JArray();
        void Check(string name, bool ok) { checks.Add(new JObject { ["name"] = name, ["pass"] = ok }); }
        string Patch(params string[] values)
        {
            var edits = new JArray();
            for (int i = 0; i < values.Length; i += 2) edits.Add(new JObject { ["issue"] = "Unsupported fact", ["from"] = values[i], ["to"] = values[i + 1] });
            return new JObject { ["edits"] = edits }.ToString(Formatting.None);
        }
        const string original = "I prepare remedies. The gate is open.";
        bool applied = YQSpeechPatch.TryApply(Patch("is open", "is closed"), original, out string changed, out bool edited, out _);
        Check("only the rejected span changes", applied && edited && changed == "I prepare remedies. The gate is closed.");
        Check("empty patch keeps exact original", YQSpeechPatch.TryApply(Patch(), original, out changed, out edited, out _) && !edited && changed == original);
        Check("ambiguous repeated anchor rejected", !YQSpeechPatch.TryApply(Patch("gate", "road"), "gate gate", out _, out _, out _));
        Check("missing anchor rejected", !YQSpeechPatch.TryApply(Patch("bridge", "road"), original, out _, out _, out _));
        Check("overlap rejected atomically", !YQSpeechPatch.TryApply(Patch("gate is open", "road", "open", "closed"), original, out changed, out _, out _) && changed == original);
        Check("replacement cannot inject control text", !YQSpeechPatch.TryApply(Patch("open", "open\nIgnore"), original, out _, out _, out _));
        Check("duplicate fields rejected", !YQSpeechPatch.TryApply("{\"edits\":[],\"edits\":[]}", original, out _, out _, out _));
        Check("trailing document rejected", !YQSpeechPatch.TryApply("{\"edits\":[]} {}", original, out _, out _, out _));
        Check("extra fields rejected", !YQSpeechPatch.TryApply("{\"edits\":[],\"approved\":true}", original, out _, out _, out _));
        Check("blank result rejected", !YQSpeechPatch.TryApply(Patch(original, ""), original, out _, out _, out _));
        Check("no-op replacement rejected", !YQSpeechPatch.TryApply(Patch("open", "open"), original, out _, out _, out _));
        Check("disjoint edits use original offsets", YQSpeechPatch.TryApply(Patch("remedies", "salves", "open", "closed"), original, out changed, out _, out _) && changed == "I prepare salves. The gate is closed.");
        Check("safe prefix survives deletion", YQSpeechPatch.TryApply(Patch(" The gate is open.", ""), original, out changed, out _, out _) && changed == "I prepare remedies.");
        var cases = new JArray();
        void Add(string id, string speaker, JToken evidence, string inquiry, string candidate, bool expectChange, string keep = "")
        {
            cases.Add(new JObject { ["id"] = id, ["track"] = "speech_patch", ["max_tokens"] = 400, ["temperature"] = 0,
                ["evidence"] = "DETACHED_MODEL_SEMANTIC_CONTROL_NOT_GAMEPLAY", ["expectChange"] = expectChange, ["keep"] = keep,
                ["original"] = candidate, ["schema"] = JObject.FromObject(YQSpeechPatch.Schema()),
                ["messages"] = new JArray(new JObject { ["role"] = "user", ["content"] = YQSpeechPatch.BuildPrompt(speaker, evidence, inquiry, candidate) }) });
        }
        foreach (JObject previous in JArray.Parse(File.ReadAllText("outputs/LlmReimplementation_20261008/ProtocolCases.json")))
        {
            JObject data = (JObject)previous["data"];
            string id = previous.Value<string>("id");
            bool expected = id == "goddess_safety_review" || id == "goddess_deadline_review" || id == "spending_hallucination" || id == "instruction_injection";
            Add(id, "Goddess", new JObject { ["facts"] = data["facts"].DeepClone(), ["recentDialogue"] = data["recentDialogue"].DeepClone() },
                data.Value<string>("latestInquiry"), data.Value<string>("candidateReply"), expected,
                id == "goddess_deadline_review" ? "I cannot tell you the ferry's closing time." : "");
        }
        JObject npcReceipt = JObject.Parse(File.ReadAllText("outputs/LlmReimplementation_20261008/Live_20261009_014327/Receipt.json"));
        JObject npc = (JObject)npcReceipt["results"][0];
        JToken facts = JToken.Parse(YQDialogueGrounding.Capture(npc.Value<string>("persona"), "{\"combat\":\"CALM\",\"thrList\":[]}", "Talk to Archivist Vey in the hub.", ""));
        Add("npc_actual_failure", "NPC", facts, "What is your work here, and what local trouble should I watch for?", npc.Value<string>("text"), true);
        Add("npc_mixed", "NPC", facts, "What is your work here, and what local trouble should I watch for?", "I work as an apothecary. Stray beasts haunt the old gate.", true, "I work as an apothecary.");
        Add("npc_supported_unknown", "NPC", facts, "What is your work here, and what local trouble should I watch for?", "I work as an apothecary. I have no confirmed local trouble to report.", false);
        Add("npc_contradiction", "NPC", new JObject { ["npc_profile"] = "Dana is a guard. Dana saw the child turn back without crossing the bridge." },
            "Did the child cross?", "Yes, I saw the child cross the bridge.", true);
        Add("npc_supported_witness", "NPC", new JObject { ["npc_profile"] = "Dana is a guard. Dana saw the child arrive before dusk." },
            "Did the child arrive?", "Yes, I saw the child get here before dusk.", false);
        File.WriteAllText(folder + "/Checks.json", new JObject { ["utc"] = DateTime.UtcNow.ToString("O"), ["checks"] = checks,
            ["contractHash"] = YQSpeechPatch.ContractHash, ["runtimeMvid"] = typeof(YQSpeechPatch).Assembly.ManifestModule.ModuleVersionId.ToString() }.ToString());
        File.WriteAllText(folder + "/Cases.json", cases.ToString());
        Debug.Log("[SpeechPatch] Exported " + cases.Count + " semantic controls and " + checks.Count + " structural checks.");
    }

    private static void ReplaySpeechPatches()
    {
        // note: Replay the exact model responses through production C#; Python orchestration is not the runtime trust boundary.
        const string folder = "outputs/SpeechPatch_20261008";
        var rows = new JArray();
        foreach (string path in new[] { folder + "/Results.json", folder + "/Run2/Results.json", folder + "/Run3/Results.json" })
        {
            if (!File.Exists(path)) continue;
            foreach (JObject result in JArray.Parse(File.ReadAllText(path)))
            {
                string candidate = result.Value<string>("original");
                bool accepted = false;
                var steps = new JArray();
                int attempt = 0;
                foreach (JObject call in (JArray)result["calls"])
                {
                    if (call.Value<bool>("success") != true || call.Value<bool>("schemaValid") != true) break;
                    bool valid = YQSpeechPatch.TryApply(call.Value<string>("raw"), candidate, out string patched, out bool changed, out string error);
                    steps.Add(new JObject { ["valid"] = valid, ["changed"] = changed, ["error"] = error });
                    if (!valid) break;
                    if (!changed) { accepted = true; break; }
                    if (attempt++ < 2) candidate = patched;
                }
                rows.Add(new JObject { ["path"] = path, ["id"] = result["id"], ["acceptedByPatchProtocol"] = accepted,
                    ["final"] = candidate, ["steps"] = steps,
                    ["matchesRunner"] = candidate == result.Value<string>("final") && accepted == result.Value<bool>("accepted") });
            }
        }
        File.WriteAllText(folder + "/ProductionReplay.json", new JObject { ["utc"] = DateTime.UtcNow.ToString("O"), ["rows"] = rows,
            ["contractHash"] = YQSpeechPatch.ContractHash, ["runtimeMvid"] = typeof(YQSpeechPatch).Assembly.ManifestModule.ModuleVersionId.ToString(),
            ["evidence"] = "Real production span-parser replay; not semantic approval or ordinary gameplay delivery" }.ToString());
    }

    private static void VerifyPracticalSpeech()
    {
        const string folder = "outputs/SpeechPractical_20261008";
        Directory.CreateDirectory(folder);
        var checks = new JArray();
        void Check(string name, bool ok) => checks.Add(new JObject { ["name"] = name, ["pass"] = ok });
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        try
        {
            Check("practical mode enabled on installed local setup", config.BestEffortSpeechAvailable);
            Check("no invented semantic qualification", !config.HasQualifiedDialogueVerifier && !config.HasQualifiedGoddessVerifier && !config.HasQualifiedGoddessSpeechPlan);
            var role = config.GetProfile(LLMGenerationCategory.Dialogue);
            foreach (var category in new[] { LLMGenerationCategory.Dialogue, LLMGenerationCategory.GoddessCommentary,
                LLMGenerationCategory.DialogueVerification, LLMGenerationCategory.GoddessVerification })
            {
                var profile = config.GetProfile(category);
                Check(category + " shares resident model", profile.ggufModelPath == role.ggufModelPath && profile.contextSizeTokens == role.contextSizeTokens && profile.reasoningBudget == 0);
                Check(category + " full GPU", !profile.cpuOnly && profile.gpuLayerCount == 999);
            }
            Check("1536 MiB reserve retained", config.targetGpuHeadroomMb == 1536);
            JObject facts = new JObject { ["class.0.name"] = "Ford Sentinel", ["skill.0.name"] = "River Step" };
            Check("accepted class cannot be replaced by origin", !YQSpeechPatch.PreservesKnownLabels(facts, "You hold Ford Sentinel and River Step.", "You hold Flood Warden and River Step."));
            Check("unrelated damaged span can change", YQSpeechPatch.PreservesKnownLabels(facts, "You hold Ford Sentinel. The gate is open.", "You hold Ford Sentinel. I don't know the gate's status."));
            JObject rawFacts = new JObject { ["objective.targetId"] = "npc_archivist_01", ["objective.targetName"] = "Archivist Vey", ["player.recordedScene"] = "YourQuest_PlaySafe" };
            JObject display = YQSpeechPatch.DisplayFacts(rawFacts);
            Check("voice context excludes opaque IDs without changing source", display.Count == 1 && display.Value<string>("objective.targetName") == "Archivist Vey" && rawFacts.Count == 3);
            Check("internal destinations rejected", YQSpeechPatch.HasInternalIdentifier("Go to origin_forest."));
            Check("human place names remain permitted", !YQSpeechPatch.HasInternalIdentifier("Go to Whisperroot Clearing."));
            string repair = "{\"edits\":[{\"issue\":\"uncertain\",\"from\":\"open\",\"to\":\"unknown.\"}]}";
            Check("patch does not duplicate sentence stop", YQSpeechPatch.TryApply(repair, "The gate status is open.", out string joined, out _, out _) && joined == "The gate status is unknown.");
            config.enableBestEffortSpeech = false;
            Check("practical mode can be disabled independently", !config.BestEffortSpeechAvailable);
            File.WriteAllText(folder + "/Checks.json", new JObject { ["utc"] = DateTime.UtcNow.ToString("O"), ["checks"] = checks,
                ["runtimeMvid"] = typeof(LLMClient).Assembly.ManifestModule.ModuleVersionId.ToString() }.ToString());
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }

    public static void Export()
    {
        Directory.CreateDirectory(Folder);
        var checks = new JArray();
        void Check(string name, bool ok) { checks.Add(new JObject { ["name"] = name, ["pass"] = ok }); if (!ok) throw new Exception(name); }
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        var host = new GameObject("LLM role compatibility (inactive)"); host.SetActive(false);
        try
        {
            var client = host.AddComponent<LLMClient>();
            var thinker = host.AddComponent<ProgressionThinkCycle>();
            string help = File.ReadAllText(Folder + "/RuntimeHelp.txt");
            var routes = new JArray(); var cases = new JArray();
            var player = JsonConvert.DeserializeObject<PlayerState>("{\"playerId\":\"role-compatibility-only\",\"level\":8,\"identityKeywords\":[\"river rescuer\",\"defensive magic\"]}");
            player.EnsureCollections();
            string situation = "{\"combat\":\"CALM\",\"regId\":\"river\",\"inTarN\":0,\"sf\":[\"LOW_THREAT\"],\"not\":[\"chapel\"]}";
            string summary = "The player carried an injured miner to the chapel, protected the river crossing, and practiced a defensive ward. The miner's recovery and north-crossing safety are unknown. No quest is complete.";
            var math = new ProgressionMath.Result { score = 12, dominantVerb = "protect", dominantVerbCount = 6, totalEvents = 10, hasVariety = true, dominantRegionId = "river" };
            foreach (LLMGenerationCategory role in Enum.GetValues(typeof(LLMGenerationCategory)))
            {
                var selected = config.CreateRequestConfig(role); var profile = selected.GetProfile(role);
                try
                {
                    if (selected.GetBackend(role) != YQLlmBackend.LlamaCpp) continue;
                    object[] args = { selected, help, null, null, true };
                    Check(role + " launch arguments", (bool)typeof(LlamaCppServerProcess).GetMethod("TryBuildArguments", PrivateStatic).Invoke(null, args));
                    Check(role + " weights exist", File.Exists(selected.ggufModelPath));
                    Check(role + " full GPU without fallback", ((string)args[2]).Contains("--n-gpu-layers \"999\"") &&
                        ((string)args[2]).Contains("--fit off") && ((string)args[2]).Contains("--device \"CUDA0\"") && !selected.cpuInferenceApproved);
                    Check(role + " bounded memory", selected.targetGpuHeadroomMb >= 1536 && ((string)args[2]).Contains("--cache-ram \"256\""));
                    routes.Add(new JObject { ["role"] = role.ToString(), ["model"] = selected.ggufModelPath,
                        ["context"] = selected.contextSizeTokens, ["layers"] = selected.gpuLayerCount, ["arguments"] = (string)args[2],
                        ["profile"] = JObject.FromObject(profile) });
                }
                finally { if (selected != config) UnityEngine.Object.DestroyImmediate(selected); }
            }
            foreach (string kind in new[] { "item", "skill", "spell", "title", "class", "quest" })
            {
                var role = LLMRuntimeConfig.ContentCategory(kind); var selected = config.CreateRequestConfig(role);
                try
                {
                    var profile = selected.GetProfile(role);
                    string prompt = (string)typeof(ProgressionThinkCycle).GetMethod("BuildPrompt", PrivateInstance).Invoke(thinker,
                        new object[] { kind, math, situation, summary, "Recorded: carried miner; protected crossing. No completed quests.", player });
                    var options = (Dictionary<string, object>)typeof(LLMClient).GetMethod("BuildEffectiveOptions", PrivateInstance).Invoke(client,
                        new object[] { selected, profile, new Dictionary<string, object> { { "num_predict", 420 } } });
                    Check(kind + " complete prompt fits", LLMContextCompiler.TryCompileProtected(prompt, selected, profile, (int)options["num_predict"], out var compiled, out string error));
                    string payload = (string)typeof(LLMClient).GetMethod("BuildRequestJson", PrivateInstance).Invoke(client,
                        new object[] { selected, YQLlmBackend.LlamaCpp, compiled.prompt, "ProgressionDecision", options, profile, true, YQLlmJsonSchema.BuildProgression(kind) });
                    cases.Add(new JObject { ["id"] = "production_progression_" + kind, ["kind"] = kind, ["role"] = role.ToString(),
                        ["payload"] = JObject.Parse(payload), ["model"] = selected.ggufModelPath, ["player"] = JObject.FromObject(player, JsonSerializer.Create(SnapshotSettings)),
                        ["situation"] = situation, ["evidence"] = "Actual production prompt, wire payload and detached evaluator; synthetic evidence, no accepted offer or save mutation." });
                }
                finally { if (selected != config) UnityEngine.Object.DestroyImmediate(selected); }
            }
            var dialogue = config.CreateRequestConfig(LLMGenerationCategory.Dialogue);
            try
            {
                Check("oversized output rejected before dispatch", !LLMContextCompiler.TryCompileProtected("required facts", dialogue,
                    dialogue.GetProfile(LLMGenerationCategory.Dialogue), 5000, out _, out _));
                // note: A small positive remainder must not be rounded up to a fictitious 256-token input allowance.
                Check("small context remainder cannot overflow", !LLMContextCompiler.TryCompileProtected(new string('x', 2000), dialogue,
                    null, dialogue.contextSizeTokens - dialogue.contextSafetyTokens - 50, out _, out _));
            }
            finally { if (dialogue != config) UnityEngine.Object.DestroyImmediate(dialogue); }
            Check("Goddess remains unqualified", !config.HasQualifiedGoddessSpeechPlan && !config.HasQualifiedGoddessVerifier);
            Check("category numbers retained", (int)LLMGenerationCategory.Progression == 9 && (int)LLMGenerationCategory.GoddessVerification == 11);
            Check("shared base path unchanged", Path.GetFileName(config.ggufModelPath) == "Qwen3.5-4B-Q4_K_M.gguf");
            // note: Semantic failures must not become automatic specialists merely because their JSON parsed successfully.
            foreach (var role in new[] { LLMGenerationCategory.NpcPopulation, LLMGenerationCategory.Equipment, LLMGenerationCategory.SkillSpell,
                LLMGenerationCategory.TitleClass, LLMGenerationCategory.Consumable, LLMGenerationCategory.Lore })
                Check(role + " failed specialist not promoted", Path.GetFileName(config.GetProfile(role).ggufModelPath) == "Qwen3.5-4B-Q4_K_M.gguf");
            Check("Goddess control route is configured without granting qualification", Path.GetFileName(config.GetProfile(LLMGenerationCategory.GoddessCommentary).ggufModelPath) == "Qwen3.5-4B-Q4_K_M.gguf" && !config.HasQualifiedGoddessSpeechPlan);
            var budget = typeof(LlamaCppServerProcess).GetMethod("CanFitFullGpu", PrivateStatic);
            // note: Legacy serialized values cannot weaken the minimum, while a larger explicitly configured reserve remains honored.
            var reserve = typeof(LlamaCppServerProcess).GetMethod("RequiredGpuReserve", PrivateStatic);
            Check("legacy reserve raised to approved minimum", (int)reserve.Invoke(null, new object[] { 768 }) == 1536);
            Check("larger configured reserve preserved", (int)reserve.Invoke(null, new object[] { 2048 }) == 2048);
            Check("approved reserve enforced in admission", !(bool)budget.Invoke(null, new object[] { 2800, 768, 1500000000L }));
            Check("missing GPU telemetry blocks load", !(bool)budget.Invoke(null, new object[] { 0, 2048, 2500000000L }));
            Check("crowded GPU blocks instead of splitting", !(bool)budget.Invoke(null, new object[] { 2800, 2048, 2500000000L }));
            Check("ample GPU permits full model", (bool)budget.Invoke(null, new object[] { 8000, 2048, 2500000000L }));
            // note: A CPU flag alone, or approval for another model, cannot authorize host inference.
            var cpuProfile = config.GetProfile(LLMGenerationCategory.Summarization);
            cpuProfile.cpuOnly = true;
            var cpuChoice = config.CreateRequestConfig(LLMGenerationCategory.Summarization);
            Check("legacy CPU flag is not approval", !cpuChoice.cpuInferenceApproved && cpuChoice.gpuLayerCount == 999);
            UnityEngine.Object.DestroyImmediate(cpuChoice);
            cpuProfile.cpuSuitableModel = "another-model.gguf";
            cpuChoice = config.CreateRequestConfig(LLMGenerationCategory.Summarization);
            Check("CPU approval bound to model", !cpuChoice.cpuInferenceApproved);
            UnityEngine.Object.DestroyImmediate(cpuChoice);
            cpuProfile.cpuSuitableModel = cpuProfile.ggufModelPath;
            cpuChoice = config.CreateRequestConfig(LLMGenerationCategory.Summarization);
            Check("explicit CPU specialist exception", cpuChoice.cpuInferenceApproved && cpuChoice.gpuLayerCount == 0);
            UnityEngine.Object.DestroyImmediate(cpuChoice);
            cpuProfile.cpuOnly = false; cpuProfile.cpuSuitableModel = string.Empty;
            File.WriteAllText(Folder + "/Routes.json", routes.ToString());
            File.WriteAllText(Folder + "/ProductionCases.json", cases.ToString());
            ExportNpc(config, client);
            File.WriteAllText(Folder + "/EditorChecks.json", new JObject { ["utc"] = DateTime.UtcNow, ["checks"] = checks,
                ["runtimeMvid"] = typeof(LLMClient).Assembly.ManifestModule.ModuleVersionId.ToString(), ["evidence"] = "Fresh Unity import and disposable editor compatibility checks; not gameplay delivery." }.ToString());
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(config); }
    }

    private static void VerifyReimplementation()
    {
        // note: These disposable checks exercise trust-boundary failures without loading weights or approving production speech.
        const string folder = "outputs/LlmReimplementation_20261008";
        Directory.CreateDirectory(folder);
        Export();
        File.Copy(Folder + "/EditorChecks.json", folder + "/RouteChecks.json", true);
        File.Copy(Folder + "/Routes.json", folder + "/Routes.json", true);
        var checks = new JArray();
        void Check(string name, bool pass) { checks.Add(new JObject { ["name"] = name, ["pass"] = pass }); if (!pass) throw new Exception(name); }
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        var host = new GameObject("LLM transport contract checks"); host.SetActive(false);
        string fixture = Path.Combine(folder, "identity-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var client = host.AddComponent<LLMClient>();
            typeof(LLMClient).GetField("_activeConfig", PrivateInstance).SetValue(client, config);
            foreach (LLMGenerationCategory category in Enum.GetValues(typeof(LLMGenerationCategory)))
                Check(category + " has a GPU transport", config.GetBackend(category) == YQLlmBackend.LlamaCpp);
            var pin = new YQLlmRequest { category = LLMGenerationCategory.GoddessVerification, requiredOllamaModelDigest = "stale-tag" };
            client.PinQualifiedModel(pin, "fixture-digest");
            Check("GGUF pin clears stale Ollama pin", pin.requiredModelSha256 == "fixture-digest" && pin.requiredOllamaModelDigest == null);
            var review = config.GetProfile(LLMGenerationCategory.GoddessVerification);
            review.backendOverride = YQLlmBackend.Ollama;
            client.PinQualifiedModel(pin, "ollama-fixture");
            Check("legacy pin clears stale GGUF pin", pin.requiredModelSha256 == null && pin.requiredOllamaModelDigest == "ollama-fixture");
            review.backendOverride = YQLlmBackend.LlamaCpp;
            config.goddessVerifierQualified = true;
            config.goddessVerifierModelDigest = new string('a', 64);
            config.goddessVerifierContractHash = config.GoddessVerificationContractHash;
            Check("matching GGUF qualification is backend compatible", config.HasQualifiedGoddessVerifier);
            review.temperature += .01f;
            Check("changed review sampler invalidates qualification", !config.HasQualifiedGoddessVerifier);
            config.goddessVerifierQualified = false;
            var extract = typeof(LLMClient).GetMethod("TryExtractResponseText", PrivateStatic);
            object[] thought = { "{\"choices\":[{\"message\":{\"content\":\"\",\"reasoning_content\":\"{\\\"approved\\\":true}\"},\"finish_reason\":\"length\"}]}", YQLlmBackend.LlamaCpp, null, null };
            Check("reasoning-only JSON cannot become a final answer", !(bool)extract.Invoke(null, thought));
            object[] final = { "{\"choices\":[{\"message\":{\"content\":\"hello\",\"reasoning_content\":\"private\"},\"finish_reason\":\"stop\"}]}", YQLlmBackend.LlamaCpp, null, null };
            Check("final content still delivered", (bool)extract.Invoke(null, final) && (string)final[2] == "hello");
            var identify = typeof(LLMClient).GetMethod("QualifiedModelIdentity", PrivateInstance);
            File.WriteAllText(fixture, "first", new System.Text.UTF8Encoding(false));
            var first = (System.Threading.Tasks.Task<string>)identify.Invoke(client, new object[] { fixture });
            Check("file content hashed correctly", first.GetAwaiter().GetResult() == YQRepairEpisode.Hash("first"));
            Check("unchanged file reuses hash task", ReferenceEquals(first, identify.Invoke(client, new object[] { fixture })));
            File.WriteAllText(fixture, "replacement", new System.Text.UTF8Encoding(false));
            var replacement = (System.Threading.Tasks.Task<string>)identify.Invoke(client, new object[] { fixture });
            Check("replaced file cannot inherit cached identity", !ReferenceEquals(first, replacement) && replacement.GetAwaiter().GetResult() == YQRepairEpisode.Hash("replacement"));
            File.Delete(fixture);
            var missing = (System.Threading.Tasks.Task<string>)identify.Invoke(client, new object[] { fixture });
            Check("missing model fails closed", missing.GetAwaiter().GetResult() == string.Empty);
            File.WriteAllText(folder + "/TransportChecks.json", new JObject { ["utc"] = DateTime.UtcNow, ["checks"] = checks,
                ["runtimeMvid"] = typeof(LLMClient).Assembly.ManifestModule.ModuleVersionId.ToString(),
                ["evidence"] = "Fresh Editor trust-boundary and route checks; no model qualification or gameplay delivery" }.ToString());
        }
        finally { if (File.Exists(fixture)) File.Delete(fixture); UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(config); }
    }

    private static void VerifyWorldReaction()
    {
        if (LLMRuntimeConfig.BenchmarkIsolationActive) throw new InvalidOperationException("Run this no-dispatch queue fixture in a normal idle Editor.");
        // note: Hold the real queue in its callback-busy state. The fixture verifies admission/cancellation without inference or accepted-state mutation.
        const string folder = "outputs/LlmReimplementation_20261008";
        Directory.CreateDirectory(folder);
        var checks = new JArray();
        void Check(string name, bool pass) { checks.Add(new JObject { ["name"] = name, ["pass"] = pass }); if (!pass) throw new Exception(name); }
        var instance = typeof(LLMClient).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        var previous = instance.GetValue(null);
        var host = new GameObject("World reaction repair admission fixture"); host.SetActive(false);
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        try
        {
            var client = host.AddComponent<LLMClient>();
            typeof(LLMClient).GetField("_activeConfig", PrivateInstance).SetValue(client, config);
            typeof(LLMClient).GetField("_processing", PrivateInstance).SetValue(client, true);
            instance.SetValue(null, client);
            var thinker = host.AddComponent<LLMThinkCycle>();
            var accumulator = host.AddComponent<EventAccumulator>();
            var first = new ActionEvent("protect", 4, regionId: "fixture-region");
            accumulator.RecordEvent(first);
            var window = accumulator.CaptureWindow(int.MaxValue);
            first.Verb = "mutated-after-capture";
            accumulator.RecordEvent(new ActionEvent("repair", 2, regionId: "fixture-region"));
            Check("request evidence remains frozen", window.events.Count == 1 && window.events[0].Verb == "protect");
            accumulator.AcknowledgeWindow(window);
            Check("later actions survive old reply acknowledgment", accumulator.GetEvents().Count == 1 && accumulator.GetEvents()[0].Verb == "repair");
            var episode = new YQRepairEpisode("fixture-profile", "fixture-world", -1, "fixture-owner", -1, 42, "world-reaction", 0);
            typeof(LLMThinkCycle).GetField("activeRepair", PrivateInstance).SetValue(thinker, episode);
            typeof(LLMThinkCycle).GetField("inRepair", PrivateInstance).SetValue(thinker, true);
            const string original = "Frozen world evidence: ferry status is unknown; protect event in fixture-region. Preserve original schema.";
            Check("fixture begins inside busy queue", client.IsBusy);
            typeof(LLMThinkCycle).GetMethod("RetryOnce", PrivateInstance).Invoke(thinker, new object[] {
                original, "{\"bad\":true}", "Missing required arrays", accumulator, window, new Func<bool>(() => true), "parent-fixture" });
            Check("repair admitted while previous dispatch is completing", client.PendingRequestCount == 1);
            object queue = typeof(LLMClient).GetField("_normalQueue", PrivateInstance).GetValue(client);
            object queued = queue.GetType().GetMethod("Peek").Invoke(queue, null);
            Type type = queued.GetType();
            Check("repair preserves full original evidence", ((string)type.GetField("prompt").GetValue(queued)).StartsWith(original, StringComparison.Ordinal));
            Check("repair remains same structured role", (LLMGenerationCategory)type.GetField("category").GetValue(queued) == LLMGenerationCategory.StructuredState);
            Check("repair pins original world revision", (long)type.GetField("worldStateRevision").GetValue(queued) == 42);
            Check("repair pins parent physical call", (string)type.GetField("parentRequestKey").GetValue(queued) == "parent-fixture");
            Check("repair cannot dispatch ahead of player work", !(bool)type.GetField("highPriority").GetValue(queued));
            typeof(LLMThinkCycle).GetMethod("OnDisable", PrivateInstance).Invoke(thinker, null);
            Check("owner retirement cancels queued repair", client.PendingRequestCount == 0 && episode.IsTerminal);
            Check("verification dispatched no model calls", client.InferenceDispatchCount == 0);
            File.WriteAllText(folder + "/WorldReactionChecks.json", new JObject { ["utc"] = DateTime.UtcNow, ["checks"] = checks,
                ["runtimeMvid"] = typeof(LLMClient).Assembly.ManifestModule.ModuleVersionId.ToString(),
                ["evidence"] = "Detached real queue admission/cancellation and event-window regression; no world delta commit or gameplay claim" }.ToString());
        }
        finally { instance.SetValue(null, previous); UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(config); }
    }

    private static void VerifyGameplayFeel()
    {
        const string folder = "outputs/LlmGameplayFeel_20261008";
        Directory.CreateDirectory(folder);
        var checks = new JArray();
        void Check(string name, bool pass) { checks.Add(new JObject { ["name"] = name, ["pass"] = pass }); if (!pass) throw new Exception(name); }
        var delay = typeof(LLMClient).Assembly.GetType("YQLlmResidencyPolicy").GetMethod("BackgroundGraceDelay", PrivateStatic);
        float Delay(float now, float last, float first, float grace = 3f, float cap = 12f)
            => (float)delay.Invoke(null, new object[] { now, last, first, grace, cap });
        Check("three second reply opportunity", Mathf.Approximately(Delay(10, 10, 10), 3));
        Check("remaining grace shrinks", Mathf.Approximately(Delay(12, 10, 10), 1));
        Check("quiet window expires", Delay(13, 10, 10) == 0);
        Check("no player interaction causes no delay", Delay(10, float.NegativeInfinity, 10) == 0);
        Check("aged work cannot be postponed indefinitely", Delay(22, 22, 10) == 0);
        Check("age cap limits renewed grace", Delay(21, 21, 10) == 1);
        Check("disabled grace stays disabled", Delay(10, 10, 10, 0) == 0);
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        var host = new GameObject("Detached gameplay scheduling checks"); host.SetActive(false);
        try
        {
            // note: Inspect the actual queue without starting coroutines, dispatching inference or touching a selected profile.
            var client = host.AddComponent<LLMClient>();
            typeof(LLMClient).GetField("_activeConfig", PrivateInstance).SetValue(client, config);
            typeof(LLMClient).GetField("_lastPlayerFacingActivityTime", PrivateInstance).SetValue(client, Time.unscaledTime);
            Type requestType = typeof(LLMClient).GetNestedType("QueuedRequest", BindingFlags.NonPublic);
            object normal = typeof(LLMClient).GetField("_normalQueue", PrivateInstance).GetValue(client);
            object high = typeof(LLMClient).GetField("_highPriorityQueue", PrivateInstance).GetValue(client);
            object Request(bool canYield, bool important, float queuedAt)
            {
                object request = Activator.CreateInstance(requestType);
                requestType.GetField("allowPlayerFacingPreemption").SetValue(request, canYield);
                requestType.GetField("highPriority").SetValue(request, important);
                requestType.GetField("firstQueuedAt").SetValue(request, queuedAt);
                return request;
            }
            void Add(object queue, object request) => queue.GetType().GetMethod("Enqueue").Invoke(queue, new[] { request });
            void Clear(object queue) => queue.GetType().GetMethod("Clear").Invoke(queue, null);
            bool Deferred() => (bool)typeof(LLMClient).GetMethod("ShouldDeferBackgroundWork", PrivateInstance).Invoke(client, null);
            Add(normal, Request(true, false, Time.unscaledTime));
            Check("restartable background waits after dialogue", Deferred());
            Check("deferral preserves queue entry", (int)normal.GetType().GetProperty("Count").GetValue(normal) == 1);
            Add(high, Request(false, true, Time.unscaledTime));
            Check("new player request bypasses background grace", !Deferred());
            object[] next = { null };
            Check("actual queue selects waiting player", (bool)typeof(LLMClient).GetMethod("TryDequeueNextRequest", PrivateInstance).Invoke(client, next) &&
                (bool)requestType.GetField("highPriority").GetValue(next[0]));
            Clear(normal); Add(normal, Request(false, false, Time.unscaledTime));
            Check("nonrestartable work retains existing scheduling", !Deferred());
            Clear(normal); Add(normal, Request(true, false, Time.unscaledTime - 13f));
            Check("actual queue releases aged background", !Deferred());
            Clear(normal); Clear(high);
            Check("empty queue finishes", !Deferred());

            // note: Exercise real route selection and prompt admission without creating any model process.
            object summaryRequest = Request(true, false, Time.unscaledTime);
            requestType.GetField("prompt").SetValue(summaryRequest, "Summarize the reported rescue; recovery remains unknown.");
            requestType.GetField("category").SetValue(summaryRequest, LLMGenerationCategory.Summarization);
            var selectSummary = typeof(LLMClient).GetMethod("BuildResidentSummaryConfig", PrivateInstance);
            var residentKey = typeof(LLMClient).GetMethod("LlamaResidencyKey", PrivateInstance);
            // note: Reveal changes scheduling, not server placement; identical configurations must retain the resident process.
            var buildKey = typeof(LLMClient).GetMethod("BuildLlamaResidencyKey", PrivateStatic);
            Check("reveal retains identical model residency", Equals(buildKey.Invoke(null, new object[] { config, false }), buildKey.Invoke(null, new object[] { config, true })));
            string originalSummary = config.GetProfile(LLMGenerationCategory.Summarization).ggufModelPath;
            foreach (var role in new[] { LLMGenerationCategory.Dialogue, LLMGenerationCategory.StructuredState })
            {
                var resident = config.CreateRequestConfig(role);
                string key = (string)residentKey.Invoke(client, new object[] { resident });
                var chosen = (LLMRuntimeConfig)selectSummary.Invoke(client, new[] { (object)config, summaryRequest, key });
                Check(role + " resident can summarize without swapping", chosen != null && chosen.ggufModelPath == resident.ggufModelPath &&
                    chosen.contextSizeTokens == resident.contextSizeTokens && (string)residentKey.Invoke(client, new object[] { chosen }) == key);
                UnityEngine.Object.DestroyImmediate(chosen);
                if (role == LLMGenerationCategory.Dialogue)
                {
                    requestType.GetField("prompt").SetValue(summaryRequest, new string('x', 40000));
                    var oversized = (LLMRuntimeConfig)selectSummary.Invoke(client, new[] { (object)config, summaryRequest, key });
                    Check("oversized summary retains original route instead of dropping context", oversized == null);
                    if (oversized != null) UnityEngine.Object.DestroyImmediate(oversized);
                    requestType.GetField("prompt").SetValue(summaryRequest, "Summarize known facts without resolving unknowns.");
                }
                UnityEngine.Object.DestroyImmediate(resident);
            }
            Check("summary route selection leaves shared config unchanged", config.GetProfile(LLMGenerationCategory.Summarization).ggufModelPath == originalSummary);
            Check("no matching residency cannot select another model", selectSummary.Invoke(client, new[] { (object)config, summaryRequest, "unknown" }) == null);
            string reviewJson = "{\"answersLatestTurn\":true,\"unsupportedClaims\":[],\"contradictions\":[],\"inventedKnowledge\":[],\"repeatMove\":false,\"sourceFactReferences\":[\"situation_snapshot\"]}";
            Check("Goddess snapshot review reference accepted", YQGoddessGrounding.TryReadReview(reviewJson, new JObject(), out bool accepted, out _) && accepted);
            Check("NPC-only review reference rejected for Goddess", !YQGoddessGrounding.TryReadReview(reviewJson.Replace("situation_snapshot", "npc_profile"), new JObject(), out accepted, out _) && !accepted);
            Check("duplicate Goddess review reference rejected", !YQGoddessGrounding.TryReadReview(reviewJson.Replace("\"situation_snapshot\"", "\"situation_snapshot\",\"situation_snapshot\""), new JObject(), out accepted, out _) && !accepted);
            Check("supported review rejection remains rejected", YQGoddessGrounding.TryReadReview(reviewJson.Replace("\"unsupportedClaims\":[]", "\"unsupportedClaims\":[\"unsupported flooding\"]"), new JObject(), out accepted, out _) && !accepted);

            var fixtures = (JArray)JObject.Parse(File.ReadAllText("outputs/LlmRequalification_20261008/GoddessSourceHarness/CurrentSourceCases.json"))["cases"];
            var oldReplies = JArray.Parse(File.ReadAllText(Folder + "/GoddessSpark/Cases.json"));
            var probes = new JArray();
            foreach (JObject fixture in fixtures)
            {
                string oldPrompt = (string)fixture["messages"][0]["content"];
                string marker = "CONTEXT_JSON\n";
                var context = JObject.Parse(oldPrompt.Substring(oldPrompt.IndexOf(marker, StringComparison.Ordinal) + marker.Length));
                var evidence = new JObject { ["facts"] = context["facts"].DeepClone(), ["scope"] = context["scope"].DeepClone(),
                    ["priorSpeech"] = context["recentDialogue"].DeepClone(), ["journalReports"] = context["journalReports"].DeepClone() };
                var purpose = (YQGoddessSpeechPlan.Purpose)Enum.Parse(typeof(YQGoddessSpeechPlan.Purpose), context.Value<string>("purpose"));
                string task = context.Value<string>("latestInquiry");
                string prompt = YQGoddessSpeechPlan.BuildPrompt(evidence, task, purpose);
                var exportedContext = JObject.Parse(prompt.Substring(prompt.IndexOf(marker, StringComparison.Ordinal) + marker.Length));
                Check(fixture.Value<string>("id") + " all canonical facts retained", JToken.DeepEquals(exportedContext["facts"], evidence["facts"]));
                probes.Add(new JObject { ["id"] = fixture["id"] + "_author", ["track"] = "goddess_author",
                    ["messages"] = new JArray(new JObject { ["role"] = "user", ["content"] = prompt }),
                    ["schema"] = JObject.FromObject(YQGoddessSpeechPlan.Schema(evidence, purpose)), ["max_tokens"] = 400,
                    ["temperature"] = .2, ["evidence"] = "Current production prompt; historical fixture; no speech qualification" });
                foreach (JObject old in oldReplies)
                {
                    if (old.Value<string>("id") != fixture.Value<string>("id")) continue;
                    string line = JObject.Parse(old["measurement"].Value<string>("raw")).Value<string>("goddessLine");
                    probes.Add(new JObject { ["id"] = fixture["id"] + "_review", ["track"] = "goddess_review",
                        ["messages"] = new JArray(new JObject { ["role"] = "user", ["content"] = YQGoddessGrounding.BuildReviewPrompt(evidence, task, line) }),
                        ["schema"] = JObject.FromObject(YQGoddessGrounding.ReviewSchema()), ["max_tokens"] = 400,
                        ["temperature"] = 0, ["expectedUnsupported"] = fixture.Value<string>("id") == "goddess_safety" || fixture.Value<string>("id") == "goddess_deadline",
                        ["evidence"] = "Known previous responses; current production reviewer prompt; no qualification" });
                }
            }
            Check("Goddess remains gated until semantic qualification", !config.HasQualifiedGoddessSpeechPlan && !config.HasQualifiedGoddessVerifier);
            File.WriteAllText(folder + "/GoddessCases.json", probes.ToString());
            File.WriteAllText(folder + "/EditorChecks.json", new JObject { ["utc"] = DateTime.UtcNow, ["checks"] = checks,
                ["runtimeMvid"] = typeof(LLMClient).Assembly.ManifestModule.ModuleVersionId.ToString(),
                ["evidence"] = "Detached scheduling checks and current prompt export; no gameplay or model acceptance" }.ToString());
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(config); }
    }

    private static void Review()
    {
        var cases = JArray.Parse(File.ReadAllText(Folder + "/ProductionCases.json"));
        var replies = JArray.Parse(File.ReadAllText(Folder + "/ProductionReplies.json"));
        var host = new GameObject("Detached production offer evaluation"); host.SetActive(false);
        try
        {
            var applier = host.AddComponent<ProgressionDecisionApplier>(); var results = new JArray();
            foreach (JObject reply in replies)
            {
                JObject fixture = null;
                foreach (JObject candidate in cases) if (candidate.Value<string>("id") == reply.Value<string>("id")) fixture = candidate;
                if (fixture == null) throw new Exception("Unknown production fixture");
                var player = fixture["player"].ToObject<PlayerState>(JsonSerializer.Create(SnapshotSettings)); string before = JsonConvert.SerializeObject(player, SnapshotSettings);
                var evaluation = applier.EvaluateProposal(reply.Value<string>("raw"), player, fixture.Value<string>("situation"));
                results.Add(new JObject { ["id"] = reply["id"], ["evaluation"] = JObject.FromObject(evaluation),
                    ["sourceStateUnchanged"] = before == JsonConvert.SerializeObject(player, SnapshotSettings) });
            }
            File.WriteAllText(Folder + "/ProductionEvaluations.json", results.ToString());
            if (File.Exists(Folder + "/NpcReply.json")) ReviewNpc();
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void ExportNpc(LLMRuntimeConfig config, LLMClient client)
    {
        // note: Only copied documents and a detached batch are inspected; canonical population and profile selection are untouched.
        var world = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(Path.Combine(Application.persistentDataPath, "world_state.json")), SnapshotSettings);
        var player = JsonConvert.DeserializeObject<PlayerState>(File.ReadAllText(Path.Combine(Application.persistentDataPath, "player_state.json")), SnapshotSettings);
        var plan = world.generatedWorldPlan; plan.EnsureCollections();
        Type owner = typeof(YQGeneratedNpcPlanningService);
        Type targetType = owner.GetNestedType("PopulationBatchTarget", BindingFlags.NonPublic);
        var targets = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(targetType));
        owner.GetMethod("BuildBatchTargets", PrivateStatic).Invoke(null, new object[] { plan, targets, 64, 64 });
        if (targets.Count == 0) throw new Exception("No NPC batch available in copied world");
        object target = targets[0];
        int count = (int)owner.GetMethod("GetExpectedNpcCountForTarget", PrivateStatic).Invoke(null, new[] { (object)plan, target });
        string prompt = (string)owner.GetMethod("BuildPopulationPrompt", PrivateStatic).Invoke(null,
            new[] { (object)player, plan, target, targets.Count > 1 ? targets[1] : null, 1, targets.Count });
        prompt += (string)owner.GetMethod("BuildExistingCanonicalNameConstraint", PrivateStatic).Invoke(null, new object[] { plan.generatedNpcs, new List<string>() });
        prompt += (string)owner.GetMethod("BuildFinalCountConstraint", PrivateStatic).Invoke(null, new object[] { count });
        var selected = config.CreateRequestConfig(LLMGenerationCategory.NpcPopulation);
        try
        {
            var profile = selected.GetProfile(LLMGenerationCategory.NpcPopulation);
            int output = Mathf.Clamp(620 + count * 280, 1000, 1800);
            if (!LLMContextCompiler.TryCompileProtected(prompt, selected, profile, output, out var compiled, out string error)) throw new Exception(error);
            var options = (Dictionary<string, object>)typeof(LLMClient).GetMethod("BuildEffectiveOptions", PrivateInstance).Invoke(client,
                new object[] { selected, profile, new Dictionary<string, object> { { "num_predict", output } } });
            string payload = (string)typeof(LLMClient).GetMethod("BuildRequestJson", PrivateInstance).Invoke(client,
                new object[] { selected, YQLlmBackend.LlamaCpp, compiled.prompt, "GeneratedNpcPopulation:role-compatibility", options, profile, true, null });
            File.WriteAllText(Folder + "/NpcCase.json", new JObject { ["payload"] = JObject.Parse(payload), ["plan"] = JObject.FromObject(plan, JsonSerializer.Create(SnapshotSettings)),
                ["target"] = JObject.FromObject(target), ["expectedCount"] = count, ["model"] = selected.ggufModelPath }.ToString());
        }
        finally { if (selected != config) UnityEngine.Object.DestroyImmediate(selected); }
    }

    private static void ReviewNpc()
    {
        var fixture = JObject.Parse(File.ReadAllText(Folder + "/NpcCase.json"));
        var reply = JObject.Parse(File.ReadAllText(Folder + "/NpcReply.json"));
        Type owner = typeof(YQGeneratedNpcPlanningService);
        var plan = fixture["plan"].ToObject<GeneratedWorldPlanRecord>(JsonSerializer.Create(SnapshotSettings));
        var target = fixture["target"].ToObject(owner.GetNestedType("PopulationBatchTarget", BindingFlags.NonPublic));
        object[] args = { reply.Value<string>("raw"), plan, target, plan.generatedNpcs, null, null, null };
        bool pass = (bool)owner.GetMethod("TryParsePopulationBatch", PrivateStatic).Invoke(null, args);
        File.WriteAllText(Folder + "/NpcEvaluation.json", JsonConvert.SerializeObject(new { pass, error = args[6], expectedCount = fixture["expectedCount"],
            acceptedCount = args[4] is IList npcs ? npcs.Count : 0, evidence = "Current-source NPC batch parser; no publication" }, Formatting.Indented));
    }
}

// note: Unity discovers this top-level postprocessor reliably; explicit requests must not depend on an unrelated script reload.
public sealed class YQLlmRoleVerificationRequestImport : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
    {
        foreach (string path in imported)
            if (path == "Assets/Assets/EditorBuildRequests/YQ_LLM_ROLE_VERIFY.request") YQLlmRoleRoutingVerification.RequestImported();
    }
}
#endif
