#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Export real production payloads for isolated timing; no request is submitted and no accepted profile is installed or changed.
public static class YQLlmTimingCaseExport
{
    private const string ReferenceRoot = "C:/Users/Garri/.codex/visualizations/2026/10/01/01a0f854-8032-7ae3-96e3-e0306efdc00a/ollama-yourquest-eval";
    public static void RunFromCommandLine()
    {
        GameObject host = null;
        LLMRuntimeConfig config = null;
        try
        {
            if (!Application.isBatchMode || Application.isPlaying) throw new InvalidOperationException("Isolated idle batch required.");
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "outputs/LlmTiming_20261005");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-yqRepairedTimingExport") >= 0) folder = Path.Combine(folder, "Repaired");
            Directory.CreateDirectory(folder);
            var settings = new JsonSerializerSettings { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
            var world = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(Path.Combine(Application.persistentDataPath, "world_state.json")), settings);
            var player = JsonConvert.DeserializeObject<PlayerState>(File.ReadAllText(Path.Combine(Application.persistentDataPath, "player_state.json")), settings);
            var plan = world.generatedWorldPlan;
            config = LLMRuntimeConfig.CreateRuntimeDefault();
            config.ollamaApiUrl = "http://127.0.0.1:11436";
            config.serverPort = 11437;
            host = new GameObject("Detached LLM timing payload exporter");
            var client = host.AddComponent<LLMClient>();
            var worldService = host.AddComponent<YQWorldGenerationService>();
            var progression = host.AddComponent<ProgressionThinkCycle>();
            var director = host.AddComponent<DirectorPromptBuilder>();
            BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
            var buildOptions = typeof(LLMClient).GetMethod("BuildEffectiveOptions", instance);
            var buildWire = typeof(LLMClient).GetMethod("BuildRequestJson", instance);
            var cases = new JArray();
            void Add(string name, LLMGenerationCategory role, string prompt, string source, Dictionary<string, object> overrides = null,
                Dictionary<string, object> schema = null, string tag = null, bool protect = false, bool diagnosticOnly = false)
            {
                var profile = config.GetProfile(role);
                var options = (Dictionary<string, object>)buildOptions.Invoke(client, new object[] { config, profile, overrides });
                options.Remove("request_timeout_seconds");
                int output = Convert.ToInt32(options["num_predict"]);
                LLMCompiledPrompt compiled;
                string error;
                bool valid = protect ? LLMContextCompiler.TryCompileProtected(prompt, config, profile, output, out compiled, out error)
                    : LLMContextCompiler.TryCompile(prompt, config, profile, output, out compiled, out error);
                if (!valid) throw new InvalidOperationException(name + ": " + error);
                var backend = config.GetBackend(role);
                string wire = (string)buildWire.Invoke(client, new object[] { config, backend, compiled.prompt, tag ?? name, options, profile, profile.preferJson, schema });
                cases.Add(new JObject { ["name"] = name, ["role"] = role.ToString(), ["backend"] = backend.ToString(),
                    ["model"] = backend == YQLlmBackend.LlamaCpp ? config.ggufModelPath : JObject.Parse(wire).Value<string>("model"),
                    ["source"] = source, ["diagnosticOnly"] = diagnosticOnly, ["estimatedInputTokens"] = compiled.estimatedInputTokens,
                    ["reservedOutputTokens"] = compiled.reservedOutputTokens, ["reduced"] = compiled.reduced, ["payload"] = JObject.Parse(wire) });
            }

            // note: Reuse the earlier acceptance fixtures for grounded dialogue; current profiles/compiler/transport supply the actual wire settings.
            var dialogue = JObject.Parse(File.ReadLines(Path.Combine(ReferenceRoot, "autorepair-refinement/refined_request_index.jsonl")).GetEnumerator().FirstLine());
            Add("Dialogue grounded uncertainty", LLMGenerationCategory.Dialogue, dialogue["body"].Value<string>("prompt"),
                "Earlier source-bound acceptance fixture: vey_unknown_box", tag: "DialogueRepair:timing", protect: true);
            var goddess = JObject.Parse(File.ReadLines(Path.Combine(ReferenceRoot, "goddess-structured-repair/native_qualified_defaults_idle_v11_index.jsonl")).GetEnumerator().FirstLine());
            var goddessWire = JObject.Parse(goddess.Value<string>("wire_json"));
            Add("Goddess qualified speech", LLMGenerationCategory.GoddessCommentary, goddessWire.Value<string>("prompt"),
                "Qualified native defaults idle v11 fixture", goddessWire["options"].ToObject<Dictionary<string, object>>(),
                goddessWire["format"].ToObject<Dictionary<string, object>>(), protect: true);
            Add("Origin questionnaire", LLMGenerationCategory.OriginGeneration,
                (string)typeof(YQOriginGenerationService).GetMethod("BuildPrompt", statics).Invoke(null,
                    new object[] { player, "Guided", new List<string> { "I protected an injured miner and carried them to safety.", "I want to learn defensive magic." }, "timing-origin-v1" }),
                "Current YQOriginGenerationService.BuildPrompt");
            Add("World initial plan", LLMGenerationCategory.WorldGeneration,
                (string)typeof(YQWorldGenerationService).GetMethod("BuildPrompt", instance).Invoke(worldService, new object[] { player, world, plan.worldSeed }),
                "Current YQWorldGenerationService.BuildPrompt");
            var batchType = typeof(YQGeneratedNpcPlanningService).GetNestedType("PopulationBatchTarget", BindingFlags.NonPublic);
            var batches = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(batchType));
            typeof(YQGeneratedNpcPlanningService).GetMethod("BuildBatchTargets", statics).Invoke(null, new object[] { plan, batches, 64, 64 });
            if (batches.Count == 0) throw new InvalidOperationException("No accepted base NPC population target.");
            Add("NPC location population", LLMGenerationCategory.NpcPopulation,
                (string)typeof(YQGeneratedNpcPlanningService).GetMethod("BuildPopulationPrompt", statics).Invoke(null,
                    new object[] { player, plan, batches[0], batches.Count > 1 ? batches[1] : null, 1, batches.Count }),
                "Current YQGeneratedNpcPlanningService.BuildPopulationPrompt");
            Add("Quest director", LLMGenerationCategory.QuestGeneration, director.BuildDirectorPrompt(),
                "Current DirectorPromptBuilder; detached owners, explicit empty snapshot");
            Add("Progression earned offer", LLMGenerationCategory.Progression,
                (string)typeof(ProgressionThinkCycle).GetMethod("BuildPrompt", instance).Invoke(progression, new object[] {
                    "skill", new ProgressionMath.Result { score = 48f, dominantVerb = "protect", dominantVerbCount = 5, totalEvents = 8, hasVariety = true, dominantRegionId = player.currentRegionId },
                    "{\"knownFacts\":[\"The player protected and carried an injured miner to safety.\"]}", "Protected, carried, bandaged and escorted an injured miner.", "protect:5;carry:2;aid:1", player }),
                "Current ProgressionThinkCycle.BuildPrompt");
            Add("Structured canonical proposal", LLMGenerationCategory.StructuredState,
                PromptContextBuilder.BuildContext("Propose one supported world canon note from witnessed rescue evidence. No new actors or outcomes.",
                    PromptContextBuilder.WrapJsonSchema("{\"canonLine\":\"...\",\"rationale\":\"...\"}"), "A miner was escorted to the chapel; subsequent recovery is unknown.", "rescue:1"),
                "Current InvestorDirector canon schema and PromptContextBuilder");
            Add("Summarization", LLMGenerationCategory.Summarization,
                "Summarize only these witnessed facts in two sentences. Preserve uncertainty and entity identity: Archivist Vey received a courier report of a miner rescue. Vey did not witness it. The sealed box contents are unknown.",
                "Diagnostic role probe; no direct Summarization domain caller exists in current source", diagnosticOnly: true);
            Add("Default fallback", LLMGenerationCategory.Default,
                "Summarize this accepted fact in one sentence: The player escorted an injured miner to the chapel. Subsequent recovery is unknown.",
                "Diagnostic fallback route probe", diagnosticOnly: true);

            var factions = new List<GeneratedFactionPlanRecord>(plan.factions);
            string[] ids = new string[factions.Count + 1]; ids[0] = string.Empty;
            for (int i = 0; i < factions.Count; i++) ids[i + 1] = factions[i].factionId;
            var region = plan.regions[0];
            var added = new HashSet<YQSiteKindV2>();
            // note: Replay accepted current-world site footprints as read-only timing fixtures; do not ask a cold detached planner to rebuild cached continuation providers.
            foreach (var candidate in plan.spatialPlanV2.acceptedContinuation.locations)
            {
                if (!added.Add(candidate.anchor.kind)) continue;
                region = plan.regions.Find(entry => entry.regionId == candidate.anchor.parentRegionId);
                var schema = (JObject)typeof(YQWorldGenerationService).GetMethod("BuildFrontierLocationBriefSchema", statics).Invoke(null, new object[] { candidate.anchor.kind, region.assetStyleKey, ids });
                string prompt = (string)typeof(YQWorldGenerationService).GetMethod("BuildFrontierLocationBriefPrompt", statics).Invoke(null, new object[] { plan, region, factions, candidate, schema });
                Add("Frontier " + candidate.anchor.kind, LLMGenerationCategory.WorldGeneration, prompt,
                    "Read-only replay of accepted current footprint with current canonical brief schema and prompt", new Dictionary<string, object> {
                        { "num_predict", 1800 }, { "temperature", .36f }, { "top_p", .90f },
                        { "seed", YQGoddessGenerationDialogue.VoiceSamplingSeed(candidate.deterministicSeed) } }, schema.ToObject<Dictionary<string, object>>(), "FrontierLocationBrief");
            }
            if (added.Count != 3) throw new InvalidOperationException("Timing fixtures must cover all three frontier site kinds.");
            foreach (var role in new[] { LLMGenerationCategory.DialogueVerification, LLMGenerationCategory.GoddessVerification })
                Add(role.ToString(), role, "Review this grounded NPC reply. Facts: box contents are unknown. Reply: I do not know what is inside. Return JSON only: {\"accepted\":true,\"reason\":\"No unsupported claim.\"}",
                    "Disabled verifier diagnostic transport only; cannot qualify or enable the lane", diagnosticOnly: true);
            File.WriteAllText(Path.Combine(folder, "Cases.json"), JsonConvert.SerializeObject(new { createdAtUtc = DateTime.UtcNow,
                evidence = "Current production wire payloads with detached/reference fixtures; no gameplay acceptance or model requalification",
                referenceReport = ReferenceRoot + "/YourQuest-Local-Model-Results.html", llamaExecutable = config.llamaServerExecutablePath,
                ollamaExecutable = (string)typeof(OllamaServerProcess).GetMethod("ResolveExecutable", statics).Invoke(null, new object[] { config.ollamaExecutablePath }), ollamaModelDirectory = config.ollamaModelsDirectory,
                llamaPort = config.serverPort, ollamaPort = 11436, cases }, Formatting.Indented));
            Debug.Log("[YQLlmTimingCaseExport] exported " + cases.Count + " cases");
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(config);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); if (host != null) UnityEngine.Object.DestroyImmediate(host); if (config != null) UnityEngine.Object.DestroyImmediate(config); EditorApplication.Exit(1); }
    }

    private static string FirstLine(this IEnumerator<string> lines)
    {
        using (lines) { if (!lines.MoveNext()) throw new InvalidOperationException("Empty reference fixture."); return lines.Current; }
    }
}
#endif
