using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class Program
{
    public const string Folder = "outputs/GoddessGenerativeRepair_20261007";
    private static readonly List<object> Checks = new List<object>();
    private static void Check(string name, bool pass)
    {
        Checks.Add(new { name, pass });
        if (!pass) throw new InvalidOperationException(name);
    }
    public static JObject Evidence() => new JObject {
        ["scope"] = new JObject { ["playerId"] = PlayerStateManager.Instance.state.playerId,
            ["worldId"] = WorldStateManager.Instance.State.worldIdentity.worldId,
            ["playerRevision"] = PlayerStateManager.Instance.state.stateRevision, ["worldRevision"] = WorldStateManager.Instance.State.stateRevision },
        ["facts"] = new JObject { ["player.name"] = "Mira", ["objective.description"] = "Ask Archivist Vey about the missing ferry ledger" },
        ["priorSpeech"] = new JArray(), ["journalReports"] = new JArray()
    };
    private static string Envelope(string line) => JsonConvert.SerializeObject(new { goddessLine = line, factKeys = new[] { "objective.description" } });
    private static string Review(bool pass) => JsonConvert.SerializeObject(new { answersLatestTurn = pass,
        unsupportedClaims = pass ? Array.Empty<string>() : new[] { "Unsupported external outcome." },
        contradictions = Array.Empty<string>(), inventedKnowledge = Array.Empty<string>(), repeatMove = false,
        sourceFactReferences = new[] { "current_objective" } });

    public static void Main(string[] args)
    {
        if (args[0] == "pure") Pure();
        else if (args[0] == "npc-chain") NpcChainHarness.Run(args[1]);
        else
        {
            var input = JObject.Parse(File.ReadAllText(args[1]));
            if (input.Value<string>("domain") == "npc") NpcReviewPayload(input, args[2]);
            else Payload(input, args[2]);
        }
    }
    private static void NpcReviewPayload(JObject input, string output)
    {
        // note: Export the actual NPC review contract separately; Goddess qualification cannot certify NPC evidence handling.
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        var profile = config.GetProfile(LLMGenerationCategory.DialogueVerification);
        string prompt = YQDialogueGrounding.BuildReviewPrompt(input["evidence"].ToString(Formatting.None), input.Value<string>("task"), input.Value<string>("candidate"));
        if (!LLMContextCompiler.TryCompileProtected(prompt, config, profile, profile.maxOutputTokens, out var compiled, out string error))
            throw new InvalidOperationException(error);
        bool valid = true, accepted = false; string diagnostic = string.Empty;
        if (input["raw"] != null) valid = YQDialogueGrounding.TryReadReview(input.Value<string>("raw"), out accepted, out diagnostic);
        File.WriteAllText(output, JObject.FromObject(new { valid, accepted, diagnostic, endpoint = "/api/chat",
            modelDigest = config.dialogueVerifierModelDigest, reviewerHash = config.DialogueVerificationContractHash,
            payload = new { model = profile.ollamaModel, messages = new[] { new { role = "user", content = compiled.prompt } },
                stream = false, keep_alive = "45s", format = YQDialogueGrounding.ReviewSchema(), think = profile.reasoningMode,
                options = new { num_predict = profile.maxOutputTokens, temperature = profile.temperature, top_p = profile.topP,
                    top_k = profile.topK, repeat_penalty = profile.repeatPenalty, presence_penalty = 0f,
                    num_ctx = config.contextSizeTokens, num_gpu = 0, num_thread = 4, num_batch = config.promptBatchSize } } }).ToString());
    }

    private static void Pure()
    {
        PlayerState state = new PlayerState();
        string first = "The missing ferry ledger is worth asking Vey about.";
        Check("first delivery records only after persistence", YQGoddessSpeechHistory.TryRecord(state, first, () => true, out _));
        Check("punctuation and casing cannot replay a line", !YQGoddessSpeechHistory.IsNovel(state, first.ToUpperInvariant().Replace('.', '!'), out _));
        Check("substantial sentence cannot hide in a longer reply", !YQGoddessSpeechHistory.IsNovel(state, first + " I would listen carefully.", out _));
        var rolling = new PlayerState();
        for (int index = 0; index < 1000; index++)
        {
            // note: Use distinct test tokens so the stress check measures durable storage rather than deliberate near-duplicate rejection.
            string line = "Observation_" + index;
            Check("long-session distinct delivery " + index, YQGoddessSpeechHistory.TryRecord(rolling, line, () => true, out _));
        }
        Check("prompt memory remains bounded", rolling.goddessVoiceMemory.Count == 8);
        string serialized = JsonConvert.SerializeObject(rolling);
        var reloaded = JsonConvert.DeserializeObject<PlayerState>(serialized);
        Check("discarded prompt memory does not discard lifetime history", !YQGoddessSpeechHistory.IsNovel(reloaded, "Observation_0", out _));
        var legacy = JsonConvert.DeserializeObject<PlayerState>("{\"schemaVersion\":8,\"activeQuestId\":\"kept\",\"goddessVoiceMemory\":[\"The old spoken sentence remains in this save.\"]}");
        Check("absent additive field retains legacy state", legacy.goddessDeliveredSpeechHashes.Count == 0 && legacy.activeQuestId == "kept" && legacy.schemaVersion == 8);
        Check("legacy sentence duplication is rejected before seeding", !YQGoddessSpeechHistory.IsNovel(legacy, "The old spoken sentence remains in this save. Another thought follows.", out _));
        Check("legacy history seeds through the next successful commit", YQGoddessSpeechHistory.TryRecord(legacy, "Something entirely different.", () => true, out _) && legacy.goddessDeliveredSpeechHashes.Count >= 3);
        var failed = new PlayerState(); failed.goddessVoiceMemory.Add("A previous unrelated thought.");
        string preimage = JsonConvert.SerializeObject(failed);
        Check("failed commit restores exact history", !YQGoddessSpeechHistory.TryRecord(failed, first, () => false, out _) && JsonConvert.SerializeObject(failed) == preimage);
        Check("exceptional commit restores exact history", !YQGoddessSpeechHistory.TryRecord(failed, first, () => throw new IOException("fixture"), out _) && JsonConvert.SerializeObject(failed) == preimage);
        Check("failed commit permits a future retry", YQGoddessSpeechHistory.IsNovel(failed, first, out _));
        Check("histories remain independent per player", YQGoddessSpeechHistory.IsNovel(new PlayerState(), first, out _));
        Check("punctuation-only speech cannot bypass fingerprints", !YQGoddessSpeechHistory.IsNovel(new PlayerState(), "...", out _));
        JObject evidence = Evidence();
        Check("free original prose accepts structure without a sentence frame", YQGoddessSpeechPlan.TryCompose(Envelope(first), evidence, out _, out _));
        Check("one word can be a legitimate concise response", YQGoddessSpeechPlan.TryCompose("{\"goddessLine\":\"Perhaps.\",\"factKeys\":[]}", evidence, out _, out _));
        Check("duplicate JSON properties rejected", !YQGoddessSpeechPlan.TryCompose("{\"goddessLine\":\"Perhaps.\",\"goddessLine\":\"No.\",\"factKeys\":[]}", evidence, out _, out _));
        Check("foreign fact references rejected", !YQGoddessSpeechPlan.TryCompose("{\"goddessLine\":\"Perhaps.\",\"factKeys\":[\"other.player\"]}", evidence, out _, out _));
        Check("serialized evidence and stage directions cannot become speech", !YQGoddessSpeechPlan.TryCompose(Envelope("[The Goddess smiles.]"), evidence, out _, out _));
        Check("template placeholders rejected", !YQGoddessSpeechPlan.TryCompose(Envelope("Your way begins in {location}."), evidence, out _, out _));
        Check("invisible formatting cannot manufacture different visible dialogue", !YQGoddessSpeechPlan.TryCompose(Envelope("Ask Ve\u200by about the ledger."), evidence, out _, out _));
        var recent = (JObject)evidence.DeepClone(); recent["priorSpeech"] = new JArray(first);
        Check("recent small wording substitutions rejected", !YQGoddessSpeechPlan.TryCompose(Envelope(first.Replace("missing", "lost")), recent, out _, out _));
        Check("naked prose has no review ticket", !YQGoddessSpeech.IsApproved(first, evidence));

        PlayerStateManager.Instance.state = new PlayerState();
        var client = new LLMClient(); YQGoddessSpeech.Result outcome = null;
        YQRepairEpisode episode = YQGoddessSpeech.Request(client, evidence, "What should I do next?", "transaction", () => true, result => outcome = result);
        client.Complete(Envelope(first));
        Check("generation alone cannot deliver", outcome == null && !YQGoddessSpeech.IsApproved(first, evidence));
        Check("transient speech excludes passive revision guards", !client.pending.Peek().request.bindPlayerStateRevision && !client.pending.Peek().request.bindWorldStateRevision);
        PlayerStateManager.Instance.state.stateRevision++;
        WorldStateManager.Instance.State.stateRevision++;
        Check("independent review has the bound verification lane", client.pending.Peek().request.category == LLMGenerationCategory.GoddessVerification);
        client.Complete(Review(true));
        Check("accepted review produces both digest stamps", outcome != null && !outcome.isFallback && outcome.verifierDigest == "fixture-reviewer" && outcome.generatorDigest == "fixture-generator");
        var refreshed = (JObject)evidence.DeepClone();
        refreshed["scope"]["playerRevision"] = PlayerStateManager.Instance.state.stateRevision;
        refreshed["scope"]["worldRevision"] = WorldStateManager.Instance.State.stateRevision;
        Check("passive revisions preserve the reviewed exact facts", YQGoddessSpeech.IsApproved(first, refreshed));
        Check("approved proposals have not yet mutated history", PlayerStateManager.Instance.state.goddessDeliveredSpeechHashes.Count == 0);
        Check("appending an unsupported clause invalidates the ticket", !YQGoddessSpeech.IsApproved(first + " Your family is safe.", evidence));
        var changed = (JObject)evidence.DeepClone(); changed["facts"]["player.name"] = "Another player";
        Check("other evidence cannot borrow an approval", !YQGoddessSpeech.IsApproved(first, changed));
        WorldStateManager.Instance.State.worldIdentity.ownerProfileId = "other-profile";
        Check("another profile's world cannot authorize delivery", !YQGoddessSpeech.IsApproved(first, evidence));
        WorldStateManager.Instance.State.worldIdentity.ownerProfileId = "fixture-player";
        YQProfileSaveSystem.Instance.ActiveProfileId = "other-profile";
        Check("pending profile selection cannot authorize delivery", !YQGoddessSpeech.IsApproved(first, evidence));
        YQProfileSaveSystem.Instance.ActiveProfileId = "fixture-player";
        YQProfileSaveSystem.Instance.succeeds = false;
        Check("unsaved reviewed speech withheld", !YQGoddessSpeech.TryDeliver(first, evidence, out _) && PlayerStateManager.Instance.state.goddessVoiceMemory.Count == 0);
        YQProfileSaveSystem.Instance.succeeds = true;
        Check("successful canonical delivery boundary records history", YQGoddessSpeech.TryDeliver(first, evidence, out _) && YQProfileSaveSystem.Instance.commits == 1);
        Check("successful delivery consumes its approval", !YQGoddessSpeech.TryDeliver(first, evidence, out _));

        PlayerStateManager.Instance.state = new PlayerState(); outcome = null; client = new LLMClient();
        episode = YQGoddessSpeech.Request(client, evidence, "What next?", "binding", () => true, result => outcome = result);
        client.Complete(Envelope(first), wrongBinding: true);
        Check("wrong physical response cannot reach review", outcome?.isFallback == true && client.pending.Count == 0 && episode.Disposition == "ResponseBindingMismatch");
        outcome = null; client = new LLMClient();
        episode = YQGoddessSpeech.Request(client, evidence, "What next?", "reject", () => true, result => outcome = result);
        for (int index = 0; index < 3; index++) { client.Complete(Envelope(first)); client.Complete(Review(false)); }
        Check("three rejected attempts end in silence", outcome?.isFallback == true && outcome.line == "" && episode.GeneratorCalls == 3 && episode.VerifierCalls == 3 && client.pending.Count == 0);
        outcome = null; client = new LLMClient();
        episode = YQGoddessSpeech.Request(client, evidence, "What next?", "supersede", () => true, result => outcome = result);
        YQServiceLifecycle.RequestEpoch++; client.Complete(Envelope(first));
        Check("superseded owner cannot review or publish", outcome == null && episode.Disposition == "Superseded" && client.pending.Count == 0);
        YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked = true;
        client = new LLMClient { IsExclusiveSequenceActive = true, ExclusiveSequenceOwner = "InitialWorldGeneration" }; outcome = null;
        episode = YQGoddessSpeech.Request(client, evidence, "Welcome the player.", "loading", () => true, result => outcome = result);
        Check("loading generator joins the current startup lease", client.pending.Peek().request.priority == YQLlmRequestPriority.StartupExclusive && client.pending.Peek().request.exclusiveOwner == "InitialWorldGeneration");
        client.Complete(Envelope(first));
        Check("loading review retains the same startup lease", client.pending.Peek().request.priority == YQLlmRequestPriority.StartupExclusive && client.pending.Peek().request.exclusiveOwner == "InitialWorldGeneration");
        YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked = false;
        client.Complete(Review(true));
        Check("closing loading retires its speech transaction", outcome == null && episode.Disposition == "Superseded");
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        string hash = config.GoddessSpeechPlanContractHash;
        config.GetProfile(LLMGenerationCategory.GoddessCommentary).temperature += 0.1f;
        Check("generator sampling changes invalidate qualification", hash != config.GoddessSpeechPlanContractHash);
        string reviewHash = config.GoddessVerificationContractHash; config.goddessSpeechPlanCpuOnly = !config.goddessSpeechPlanCpuOnly;
        Check("review residency changes invalidate qualification", reviewHash != config.GoddessVerificationContractHash);
        File.WriteAllText(Folder + "/PureChecks.json", JsonConvert.SerializeObject(new { utc = DateTime.UtcNow,
            evidence = "Detached current-source transaction and history checks with explicit manager/transport adapters; no physical save or Unity UI claim", checks = Checks }, Formatting.Indented));
        Console.WriteLine(Checks.Count + " detached checks PASS");
    }

    private static void Payload(JObject input, string output)
    {
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        var evidence = (JObject)input["evidence"];
        bool review = input.Value<bool>("review");
        string candidate = input.Value<string>("candidate") ?? string.Empty;
        string task = input.Value<string>("task");
        string statement = input.Value<string>("statement");
        evidence["currentPlayerStatement"] = statement ?? string.Empty;
        var profile = config.GetProfile(review ? LLMGenerationCategory.GoddessVerification : LLMGenerationCategory.GoddessCommentary);
        string prompt = review ? YQGoddessGrounding.BuildReviewPrompt(evidence, task, candidate) :
            YQGoddessSpeechPlan.BuildPrompt(evidence, task, (YQGoddessSpeechPlan.Purpose)(input.Value<int?>("purpose") ?? 0), statement);
        if (!review && !string.IsNullOrEmpty(input.Value<string>("diagnostic")))
            prompt = YQRepairEpisode.RepairPrompt(prompt, input.Value<string>("diagnostic"), input.Value<string>("previous"));
        if (!LLMContextCompiler.TryCompileProtected(prompt, config, profile, profile.maxOutputTokens, out LLMCompiledPrompt compiled, out string error,
            review ? YQGoddessGrounding.ReviewContextTokens : YQGoddessSpeechPlan.ContextTokens)) throw new InvalidOperationException(error);
        var options = new Dictionary<string, object> { ["num_predict"] = profile.maxOutputTokens, ["temperature"] = profile.temperature,
            ["top_p"] = profile.topP, ["top_k"] = profile.topK, ["presence_penalty"] = 0f,
            ["repeat_penalty"] = profile.repeatPenalty, ["num_ctx"] = review ? YQGoddessGrounding.ReviewContextTokens : YQGoddessSpeechPlan.ContextTokens, ["num_gpu"] = 0,
            ["num_thread"] = 4, ["num_batch"] = config.promptBatchSize };
        bool valid = true, accepted = false; string parsedLine = "", diagnostic = "";
        if (input["raw"] != null)
        {
            if (review) valid = YQGoddessGrounding.TryReadReview(input.Value<string>("raw"), evidence, out accepted, out diagnostic);
            else
            {
                valid = YQGoddessSpeechPlan.TryCompose(input.Value<string>("raw"), evidence, out parsedLine, out diagnostic);
                var history = new PlayerState();
                if (input["delivered"] is JArray delivered)
                    foreach (JToken line in delivered) foreach (string key in YQGoddessSpeechHistory.Fingerprints(line.Value<string>())) history.goddessDeliveredSpeechHashes.Add(key);
                if (valid) valid = YQGoddessSpeechHistory.IsNovel(history, parsedLine, out diagnostic);
            }
        }
        // note: Match the production transport: reasoning uses chat, with the grammar applied only to its final answer.
        var payload = JObject.FromObject(new { model = profile.ollamaModel, stream = false, keep_alive = "45s",
            format = review ? YQGoddessGrounding.ReviewSchema(evidence) : YQGoddessGrounding.ReplySchema(evidence), think = profile.reasoningMode, options });
        if (profile.reasoningMode) payload["messages"] = JArray.FromObject(new[] { new { role = "user", content = compiled.prompt } });
        else payload["prompt"] = compiled.prompt;
        File.WriteAllText(output, new JObject {
            ["generatorHash"] = config.GoddessSpeechPlanContractHash, ["reviewerHash"] = config.GoddessVerificationContractHash,
            ["modelDigest"] = review ? config.goddessVerifierModelDigest : config.goddessSpeechPlanModelDigest,
            ["valid"] = valid, ["accepted"] = accepted, ["line"] = parsedLine, ["diagnostic"] = diagnostic,
            ["estimatedInputTokens"] = compiled.estimatedInputTokens, ["reservedOutputTokens"] = compiled.reservedOutputTokens,
            ["endpoint"] = profile.reasoningMode ? "/api/chat" : "/api/generate", ["payload"] = payload
        }.ToString());
    }
}
