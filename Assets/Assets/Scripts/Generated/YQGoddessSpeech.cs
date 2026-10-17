using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// note: The existing scheduler owns inference; this transaction only proposes and reviews transient presentation.
public static class YQGoddessSpeech
{
    public sealed class Result
    {
        public readonly string line, disposition, evidenceHash, verifierDigest, generatorDigest;
        public readonly bool isFallback;
        public bool canDeliver => !string.IsNullOrWhiteSpace(line);
        internal Result(string spoken, string status, string hash, string generator, string verifier, bool fallback)
        { line = spoken; disposition = status; evidenceHash = hash; generatorDigest = generator; verifierDigest = verifier; isFallback = fallback; }
    }

    private sealed class Approval
    {
        public string line, evidenceKey;
        public int epoch;
        public Func<bool> stillCurrent;
    }
    private static readonly List<Approval> Approvals = new List<Approval>();

    // note: Review tickets bind exact accepted facts and identity; unrelated location/vitals revision ticks do not change those facts.
    private static string EvidenceKey(JObject evidence) => YQRepairEpisode.Hash(JsonConvert.SerializeObject(
        new { playerId = evidence?["scope"]?["playerId"], worldId = evidence?["scope"]?["worldId"],
            facts = evidence?["facts"], profile = YQGoddessAgentProfile.Hash }));

    private static bool ScopeCurrent(JObject evidence)
    {
        JObject scope = evidence?["scope"] as JObject;
        PlayerState player = PlayerStateManager.Instance?.state;
        WorldState world = WorldStateManager.Instance?.State;
        return player != null && world != null && scope != null &&
            string.Equals(world.worldIdentity?.ownerProfileId, player.playerId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(YQProfileSaveSystem.Instance?.ActiveProfileId, player.playerId, StringComparison.OrdinalIgnoreCase) &&
            scope.Value<string>("playerId") == player.playerId && scope.Value<string>("worldId") == world.worldIdentity?.worldId;
    }

    public static bool IsApproved(string line, JObject evidence)
    {
        // note: A lexical match cannot approve free prose. Only a reviewed, still-owned ticket for these exact facts can.
        if (string.IsNullOrWhiteSpace(line) || !ScopeCurrent(evidence)) return false;
        string key = EvidenceKey(evidence);
        for (int index = Approvals.Count - 1; index >= 0; index--)
        {
            Approval approval = Approvals[index];
            if (!YQServiceLifecycle.IsCurrent(approval.epoch) || (approval.stillCurrent != null && !approval.stillCurrent()))
            { Approvals.RemoveAt(index); continue; }
            if (approval.line == line.Trim() && approval.evidenceKey == key) return true;
        }
        return false;
    }

    public static bool TryDeliver(string line, JObject evidence, out string reason)
    {
        reason = "Speech no longer has a review for the current accepted facts.";
        if (!IsApproved(line, evidence)) return false;
        // note: Persist before display through the canonical paired profile owner, preserving its validation and save barriers.
        if (!YQGoddessSpeechHistory.TryRecord(PlayerStateManager.Instance?.state, line,
            () => YQProfileSaveSystem.Instance != null && YQProfileSaveSystem.Instance.SaveActiveProfile(), out reason)) return false;
        Approvals.RemoveAll(approval => approval.line == line.Trim());
        return true;
    }

    public static YQRepairEpisode Request(LLMClient client, JObject evidence, string task, string owner,
        Func<bool> stillCurrent, Action<Result> onComplete, YQGoddessSpeechPlan.Purpose purpose = YQGoddessSpeechPlan.Purpose.Commentary,
        string playerStatement = null) => Start(client, evidence, task, owner, stillCurrent, onComplete, purpose, playerStatement, null);

    public static YQRepairEpisode ReviewExisting(LLMClient client, JObject evidence, string task, string draft, string owner,
        Func<bool> stillCurrent, Action<Result> onComplete, YQGoddessSpeechPlan.Purpose purpose = YQGoddessSpeechPlan.Purpose.Commentary)
        => Start(client, evidence, task, owner, stillCurrent, onComplete, purpose, null, draft);

    private static YQRepairEpisode Start(LLMClient client, JObject evidence, string task, string owner,
        Func<bool> stillCurrent, Action<Result> onComplete, YQGoddessSpeechPlan.Purpose purpose, string playerStatement, string draft)
    {
        // note: Every correction and review retains one immutable accepted snapshot and the actual latest inquiry.
        JObject snapshot = evidence != null ? (JObject)evidence.DeepClone() : new JObject();
        snapshot["currentPlayerStatement"] = playerStatement ?? string.Empty;
        string hash = EvidenceKey(snapshot);
        if (!ScopeCurrent(snapshot) || (stillCurrent != null && !stillCurrent())) return null;
        if (client == null || !client.GoddessSpeechPlanEnabled || !client.GoddessSemanticRepairEnabled ||
            string.IsNullOrWhiteSpace(YQGoddessAgentProfile.Text))
        {
            onComplete?.Invoke(new Result(string.Empty, "QualifiedSpeechUnavailable", hash, string.Empty, string.Empty, true));
            return null;
        }
        JObject scope = (JObject)snapshot["scope"];
        // note: Loading speech joins the existing startup lease. Background admission would hold it until after its presentation window closed.
        bool startupSpeech = YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked &&
            (!client.IsExclusiveSequenceActive || client.ExclusiveSequenceOwner == "InitialWorldGeneration");
        YQLlmRequest binding = new YQLlmRequest {
            debugTag = "GoddessSpeech:" + owner, ownerId = "GoddessSpeech:" + owner,
            profileId = scope.Value<string>("playerId"), worldId = scope.Value<string>("worldId"),
            priority = startupSpeech ? YQLlmRequestPriority.StartupExclusive : YQLlmRequestPriority.Background,
            exclusiveOwner = startupSpeech ? "InitialWorldGeneration" : null,
            bindPlayerStateRevision = false, bindWorldStateRevision = false
        };
        YQRepairEpisode episode = client.CreateRepairEpisode(binding, "goddess:" + hash + ":" + (int)purpose + ":" +
            YQRepairEpisode.Hash(task) + ":" + YQRepairEpisode.Hash(playerStatement) + ":" + YQRepairEpisode.Hash(draft), double.PositiveInfinity);
        if (episode == null)
        {
            onComplete?.Invoke(new Result(string.Empty, "RepairUnavailable", hash, string.Empty, string.Empty, true));
            return null;
        }
        var session = new Session(client, snapshot, task ?? string.Empty, owner, hash, episode, stillCurrent, onComplete, purpose, playerStatement, startupSpeech);
        if (draft == null) session.Generate(null, null, 0);
        else session.CheckAndReview(JsonConvert.SerializeObject(new { goddessLine = draft, factKeys = new string[0] }), 0, null);
        return episode;
    }

    private static string[] BuildReturningJourneyFallbackAlternatives(JObject evidence, YQGoddessSpeechPlan.Purpose purpose, string owner)
    {
        // note: This pure formatter supplies no approval. Current/profile/epoch, novelty and durable delivery remain owned by Session and its existing ticket.
        if (purpose != YQGoddessSpeechPlan.Purpose.Welcome || !string.Equals(owner, "returning-journey", StringComparison.Ordinal)) return null;
        JObject facts = evidence?["facts"] as JObject;
        JToken name = facts?["world.name"], excerpt = facts?["world.name.isExcerpt"];
        string label = name?.Type == JTokenType.String ? (string)name : null;
        bool safe = !string.IsNullOrWhiteSpace(label) && label.Length <= 64 &&
            (excerpt == null || excerpt.Type == JTokenType.Boolean && !(bool)excerpt);
        if (safe)
        {
            // note: Never sanitize an unsafe name into different canon or quote a clipped fact; only a small plain-label alphabet may enter fallback speech.
            foreach (char character in label)
                if (!(char.IsLetterOrDigit(character) || character == ' ' || character == '-' || character == '\'' || character == '\u2019'))
                { safe = false; break; }
        }
        if (safe)
        {
            label = label.Trim();
            return new[] {
                "Welcome back. Your saved journey in " + label + " is still being prepared.",
                "I'm preparing your saved journey in " + label + ". Welcome back."
            };
        }
        // note: This exact caller's current lease proves ongoing preparation; an absent/unsafe label supplies no world name or completed-load claim.
        return new[] { "Welcome back. Your saved journey is still being prepared.", "I'm preparing your saved journey. Welcome back." };
    }

    private sealed class Session
    {
        private readonly LLMClient client;
        private readonly JObject evidence;
        private readonly string task, owner, evidenceHash, originalPrompt, generatorDigest, verifierDigest;
        private readonly YQRepairEpisode episode;
        private readonly Func<bool> stillCurrent;
        private readonly Action<Result> onComplete;
        private readonly YQGoddessSpeechPlan.Purpose purpose;
        private readonly bool startupSpeech;
        private readonly bool bestEffort;
        private readonly string playerStatement;

        public Session(LLMClient transport, JObject snapshot, string request, string ownerId, string hash,
            YQRepairEpisode transaction, Func<bool> current, Action<Result> callback, YQGoddessSpeechPlan.Purpose speechPurpose, string currentStatement, bool duringStartup)
        {
            client = transport; evidence = snapshot; task = request; owner = ownerId; evidenceHash = hash;
            episode = transaction; stillCurrent = current; onComplete = callback; purpose = speechPurpose; playerStatement = currentStatement;
            startupSpeech = duringStartup;
            bestEffort = client.BestEffortSpeechEnabled;
            generatorDigest = client.SpeechModelDigest(LLMGenerationCategory.GoddessCommentary);
            verifierDigest = client.SpeechModelDigest(LLMGenerationCategory.GoddessVerification);
            originalPrompt = YQGoddessSpeechPlan.BuildPrompt(evidence, task, purpose, playerStatement);
        }

        private bool Current()
        {
            if (episode.IsTerminal) return false;
            if (client != null && (!startupSpeech || YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked) &&
                YQServiceLifecycle.IsCurrent(episode.generationEpoch) && ScopeCurrent(evidence) &&
                (stillCurrent == null || stillCurrent())) return true;
            episode.Finish("Superseded"); return false;
        }

        private bool Qualified() => client.GoddessSpeechPlanEnabled && client.GoddessSemanticRepairEnabled &&
            client.BestEffortSpeechEnabled == bestEffort && client.SpeechModelDigest(LLMGenerationCategory.GoddessCommentary) == generatorDigest &&
            client.SpeechModelDigest(LLMGenerationCategory.GoddessVerification) == verifierDigest;

        private bool MatchesScope(YQLlmRequestResult result) => result.repairEpisodeKey == episode.key &&
            result.profileId == episode.profileId && result.worldId == episode.worldId &&
            result.generationEpoch == episode.generationEpoch && result.ownerId == episode.ownerId &&
            result.playerStateRevision == episode.playerRevision && result.worldStateRevision == episode.worldRevision;

        private bool Belongs(YQLlmRequestResult result, bool verification)
        {
            // note: Scope alone cannot accept a sibling response; require the exact last physical lane and request.
            if (!MatchesScope(result) || episode.Calls.Count == 0) return false;
            YQRepairEpisode.CallStamp call = episode.Calls[episode.Calls.Count - 1];
            return call.verification == verification && result.repairRequestKey == call.requestKey &&
                result.category == (verification ? LLMGenerationCategory.GoddessVerification : LLMGenerationCategory.GoddessCommentary);
        }

        private YQLlmRequest BuildRequest(string prompt, bool review, string parentKey)
        {
            var request = new YQLlmRequest {
                prompt = prompt, debugTag = "GoddessSpeech:" + owner,
                category = review ? LLMGenerationCategory.GoddessVerification : LLMGenerationCategory.GoddessCommentary,
                priority = startupSpeech ? YQLlmRequestPriority.StartupExclusive : YQLlmRequestPriority.Background,
                exclusiveOwner = startupSpeech ? "InitialWorldGeneration" : null,
                bindPlayerStateRevision = false, bindWorldStateRevision = false,
                requireJson = true, deferJsonValidationToCaller = true,
                jsonSchema = review ? YQSpeechPatch.Schema() : YQGoddessSpeechPlan.Schema(evidence, purpose, playerStatement),
                maxRetries = 0, parentRequestKey = parentKey,
                ownerStillCurrent = () => client != null && (!startupSpeech || YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked) &&
                    ScopeCurrent(evidence) && (stillCurrent == null || stillCurrent()),
                optionsOverride = new Dictionary<string, object> { { "presence_penalty", 0f },
                    { "num_ctx", review ? YQGoddessGrounding.ReviewContextTokens : YQGoddessSpeechPlan.ContextTokens } }
            };
            client.PinQualifiedModel(request, review ? verifierDigest : generatorDigest);
            if (client.GoddessSpeechPlanCpuOnly) request.optionsOverride["num_gpu"] = 0;
            episode.Bind(request, review);
            return request;
        }

        public void Generate(string diagnostic, string previous, int repairs, string parentKey = null)
        {
            if (!Current()) return;
            if (!Qualified()) { Finish("QualifiedSpeechUnavailable", null); return; }
            if (!episode.CanSubmit(false) || !episode.CanSubmit(true)) { Finish("SpeechWithheld", null); return; }
            string prompt = string.IsNullOrEmpty(diagnostic) ? originalPrompt : YQRepairEpisode.RepairPrompt(originalPrompt, diagnostic, previous);
            YQLlmRequest request = BuildRequest(prompt, false, parentKey);
            client.Submit(request, generated =>
            {
                if (!Receive(generated, false)) return;
                if (!generated.success) { Retry(generated.error, generated.text, repairs, generated.repairRequestKey); return; }
                CheckAndReview(generated.text, repairs, generated.repairRequestKey);
            });
        }

        public void CheckAndReview(string raw, int repairs, string parentKey, bool patchOnly = false)
        {
            if (!Current()) return;
            if (!Qualified()) { Finish("QualifiedSpeechUnavailable", null); return; }
            if (!YQGoddessSpeechPlan.TryCompose(raw, evidence, out string line, out string error, purpose, playerStatement) ||
                !YQGoddessSpeechHistory.IsNovel(PlayerStateManager.Instance?.state, line, out error))
            {
                if (patchOnly) Finish("SpeechPatchRejected", null);
                else Retry(error, raw, repairs, parentKey);
                return;
            }
            if (!episode.CanSubmit(true)) { Finish("SpeechWithheld", null); return; }
            YQLlmRequest review = BuildRequest(YQGoddessGrounding.BuildPatchPrompt(evidence, task, line), true, parentKey);
            client.Submit(review, result =>
            {
                if (!Receive(result, true)) return;
                // note: Semantic repairs edit only exact rejected spans. A repaired candidate must pass a fresh review before getting a delivery ticket.
                if (!result.success || !YQSpeechPatch.TryApply(result.text, line, out string patched, out bool changed, out _))
                { Finish("SpeechPatchUnavailable", null); return; }
                if (changed)
                {
                    if (bestEffort)
                    {
                        // note: Practical mode takes one bounded correction, retaining exact canon labels and all existing speech/novelty checks.
                        var practical = JObject.Parse(raw);
                        practical["goddessLine"] = patched;
                        if (!YQSpeechPatch.PreservesKnownLabels(evidence["facts"], line, patched) ||
                            !YQGoddessSpeechPlan.TryCompose(practical.ToString(Formatting.None), evidence, out string spoken, out _, purpose, playerStatement) ||
                            !YQGoddessSpeechHistory.IsNovel(PlayerStateManager.Instance?.state, spoken, out _))
                        { Finish("SpeechPatchRejected", null); return; }
                        Finish("BestEffortSpanRepaired", spoken);
                        return;
                    }
                    if (repairs >= 2 || !episode.CanSubmit(true)) { Finish("SpeechWithheld", null); return; }
                    var envelope = JObject.Parse(raw);
                    envelope["goddessLine"] = patched;
                    CheckAndReview(envelope.ToString(Formatting.None), repairs + 1, result.repairRequestKey, true);
                    return;
                }
                if (!YQGoddessSpeechHistory.IsNovel(PlayerStateManager.Instance?.state, line, out error))
                { Finish("SpeechWithheld", null); return; }
                Finish(bestEffort ? "BestEffortAccepted" : repairs == 0 ? "OriginalAccepted" : "RepairedAccepted", line);
            });
        }

        private bool Receive(YQLlmRequestResult result, bool review)
        {
            if (!Current()) return false;
            if (result.outcome == YQLlmTerminalOutcome.Cancelled || result.outcome == YQLlmTerminalOutcome.Superseded || result.outcome == YQLlmTerminalOutcome.Evicted)
            { episode.Finish("Superseded"); return false; }
            if (!Qualified()) { Finish("QualifiedSpeechUnavailable", null); return false; }
            if (!MatchesScope(result) || (result.success && !Belongs(result, review)))
            { Finish("ResponseBindingMismatch", null); return false; }
            return true;
        }

        private void Retry(string diagnostic, string raw, int repairs, string parentKey)
        {
            // note: Corrections remain bounded by both three-call lanes; silence is the only exhausted-budget fallback.
            if (repairs < (bestEffort ? 1 : 2) && episode.CanSubmit(false) && episode.CanSubmit(true) &&
                !(diagnostic ?? string.Empty).StartsWith("ContextOverflow", StringComparison.Ordinal))
                Generate(diagnostic, raw, repairs + 1, parentKey);
            else Finish("SpeechWithheld", null);
        }

        private void Finish(string disposition, string line)
        {
            if (!Current()) return;
            bool fallback = line == null;
            // note: Approved uncertainty is an explicit fallback, not invented canon or a qualified model result. Stale owners still terminate without speech.
            if (fallback && bestEffort && client.BestEffortSpeechEnabled)
            {
                // note: Only this code-owned returning lease can describe saved-journey preparation; all other fallback callers retain their wording.
                string[] returningAlternatives = BuildReturningJourneyFallbackAlternatives(evidence, purpose, owner);
                string[] alternatives = returningAlternatives ?? (purpose == YQGoddessSpeechPlan.Purpose.Welcome
                    ? new[] { "I'm here. Take your time.", "Welcome back. We can take this one step at a time." }
                    : new[] { "I'm not certain about that detail yet.", "I would rather leave that uncertain than mislead you.",
                        "Let us find out before we act on a guess.", "I haven't enough to say more with confidence.",
                        "That is still an open question for me.", "I'd prefer to hear more before drawing a conclusion." });
                foreach (string candidate in alternatives)
                    if (YQGoddessSpeechHistory.IsNovel(PlayerStateManager.Instance?.state, candidate, out _))
                    { line = candidate; disposition = (returningAlternatives != null ? "BestEffortReturningLoading:" : "BestEffortUncertainty:") + disposition; break; }
            }
            if (!episode.Finish(disposition)) return;
            if (line != null)
            {
                // note: Tickets are transient and bounded. Historical uniqueness remains in the player's canonical save.
                if (Approvals.Count >= 64) Approvals.RemoveAt(0);
                Approvals.Add(new Approval { line = line, evidenceKey = evidenceHash, epoch = episode.generationEpoch, stillCurrent = stillCurrent });
            }
            onComplete?.Invoke(new Result(line ?? string.Empty, disposition, evidenceHash, generatorDigest, verifierDigest, fallback));
        }
    }
}
