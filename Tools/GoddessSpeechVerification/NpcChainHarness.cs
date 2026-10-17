using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// note: The real DialogueThinkService controls every repair and review. Only scene context and network completion are explicit detached adapters.
public static class NpcChainHarness
{
    public static void Run(string inputPath)
    {
        JObject fixture = JObject.Parse(File.ReadAllText(inputPath));
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        // note: A comparison changes only the author model; the service, sampling, reviewer, facts and repair limits remain identical.
        if (!string.IsNullOrWhiteSpace(fixture.Value<string>("authorModel")))
            config.GetProfile(LLMGenerationCategory.Dialogue).ollamaModel = fixture.Value<string>("authorModel");
        var agent = new NpcDialogueAgent { persona = fixture.Value<string>("persona") };
        NpcFixtureScene.Director.CurrentObjective = fixture.Value<string>("objective") ?? string.Empty;
        var service = new DialogueThinkService { situationSnapshotBuilder = new SituationSnapshotBuilder {
            snapshot = fixture["snapshot"].ToString(Formatting.None) } };
        foreach (JObject turn in (JArray)fixture["turns"])
        {
            string question = turn.Value<string>("question"), line = null;
            bool completed = false;
            var client = new LLMClient { QualifiedDialogueVerifierDigest = config.dialogueVerifierModelDigest };
            LLMClient.Instance = client;
            service.RequestNpcReply(agent, question, reply => { line = reply; completed = true; }, () => true);
            YQRepairEpisode episode = null;
            int index = 0;
            while (client.pending.Count > 0)
            {
                if (++index > 6) throw new InvalidOperationException("NPC chain exceeded six requests.");
                var pending = client.pending.Peek();
                var request = pending.request;
                episode = request.repairEpisode;
                var profile = config.GetProfile(request.category);
                var options = new Dictionary<string, object> {
                    ["num_predict"] = profile.maxOutputTokens, ["temperature"] = profile.temperature,
                    ["top_p"] = profile.topP, ["top_k"] = profile.topK, ["repeat_penalty"] = profile.repeatPenalty,
                    ["presence_penalty"] = profile.presencePenalty, ["num_ctx"] = config.contextSizeTokens,
                    ["num_gpu"] = 0, ["num_thread"] = 4, ["num_batch"] = config.promptBatchSize
                };
                if (request.optionsOverride != null) foreach (var pair in request.optionsOverride) options[pair.Key] = pair.Value;
                if (!LLMContextCompiler.TryCompileProtected(request.prompt, config, profile,
                    Convert.ToInt32(options["num_predict"]), out var compiled, out string error)) throw new InvalidOperationException(error);
                var payload = JObject.FromObject(new { model = profile.ollamaModel, stream = false, keep_alive = "45s",
                    format = request.jsonSchema, options });
                // note: Match production: direct NPC authoring leaves think unspecified; reasoning review uses chat with think=true.
                if (profile.reasoningMode)
                { payload["think"] = true; payload["messages"] = JArray.FromObject(new[] { new { role = "user", content = compiled.prompt } }); }
                else payload["prompt"] = compiled.prompt;
                Console.WriteLine(JObject.FromObject(new { kind = "request", turn = turn.Value<string>("name"), question,
                    ordinal = index, review = request.repairVerification, requiredDigest = request.requiredOllamaModelDigest,
                    endpoint = profile.reasoningMode ? "/api/chat" : "/api/generate", payload,
                    reviewerHash = config.DialogueVerificationContractHash, requestKey = pending.key, parentKey = request.parentRequestKey,
                    injectedRaw = index == 1 ? turn.Value<string>("injectRaw") : null }).ToString(Formatting.None));
                Console.Out.Flush();
                string response = Console.ReadLine();
                if (response == null) throw new EndOfStreamException("Missing model completion.");
                var data = JObject.Parse(response);
                client.Complete(data.Value<string>("raw"), failed: data.Value<bool?>("failed") == true);
            }
            bool fallback = line == YQDialogueGrounding.UnknownReply;
            if (!completed) throw new InvalidOperationException("NPC chain ended without its callback.");
            if (line != null && !fallback) { agent.playerLines.Add(question); agent.npcLines.Add(line); }
            Console.WriteLine(JObject.FromObject(new { kind = "result", turn = turn.Value<string>("name"), question, line, fallback,
                generatedAndAccepted = line != null && !fallback, disposition = episode?.Disposition,
                generatorCalls = episode?.GeneratorCalls ?? 0, verifierCalls = episode?.VerifierCalls ?? 0,
                transcript = agent.RenderRecentDialogue(12), evidence = "DETACHED_ACTUAL_NPC_REPLY_REPAIR_CHAIN; NOT_UNITY_UI_OR_PHYSICAL_SAVE" }).ToString(Formatting.None));
            Console.Out.Flush();
        }
    }
}
