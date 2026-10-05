using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// note: This presentation transaction uses the existing scheduler and repair episode; it owns no player/world state.
public static class YQGoddessSpeech
{
    public sealed class Result
    {
        public readonly string line, disposition, evidenceHash, verifierDigest, generatorDigest;
        public readonly bool isFallback;
        internal Result(string spoken, string status, string hash, string digest, bool fallback)
        { line = spoken; disposition = status; evidenceHash = hash; verifierDigest = string.Empty; generatorDigest = digest; isFallback = fallback; }
    }

    public static YQRepairEpisode Request(LLMClient client, JObject evidence, string task, string owner,
        Func<bool> stillCurrent, Action<Result> onComplete, YQGoddessSpeechPlan.Purpose purpose = YQGoddessSpeechPlan.Purpose.Commentary,
        string playerStatement = null)
    {
        // note: Capture a separate immutable view before any asynchronous call; every repair uses this same evidence.
        JObject snapshot = evidence != null ? (JObject)evidence.DeepClone() : new JObject();
        string hash = YQRepairEpisode.Hash(snapshot.ToString());
        if (stillCurrent != null && !stillCurrent()) return null;
        if (client == null || !client.GoddessSpeechPlanEnabled)
        {
            onComplete?.Invoke(new Result(YQGoddessGrounding.UnknownLine, "SpeechPlanUnavailable", hash, string.Empty, true));
            return null;
        }
        JObject scope = snapshot["scope"] as JObject;
        YQLlmRequest binding = new YQLlmRequest {
            debugTag = "GoddessSpeech:" + owner, ownerId = "GoddessSpeech:" + owner,
            profileId = scope?.Value<string>("playerId"), worldId = scope?.Value<string>("worldId"),
            playerStateRevision = scope?.Value<long>("playerRevision") ?? -1,
            worldStateRevision = scope?.Value<long>("worldRevision") ?? -1
        };
        // note: Purpose and current-turn words participate in indexing; equal task prose cannot alias a different speech transaction.
        // note: Leave room for the existing scheduler backlog before the bounded three-call voice budget; queued world generation is not failed voice inference.
        double queueAllowance = client.GetPlanningLeadTimeSeconds(LLMGenerationCategory.GoddessCommentary, "GoddessSpeech");
        YQRepairEpisode episode = client.CreateRepairEpisode(binding, "goddess:" + hash + ":" + (int)purpose + ":" +
            YQRepairEpisode.Hash(task) + ":" + YQRepairEpisode.Hash(playerStatement), queueAllowance + 45d);
        if (episode == null)
        {
            onComplete?.Invoke(new Result(YQGoddessGrounding.UnknownLine, "RepairUnavailable", hash, string.Empty, true));
            return null;
        }
        new Session(client, snapshot, task ?? string.Empty, owner, hash, episode, stillCurrent, onComplete, purpose, playerStatement).Generate(null, null, 0);
        return episode;
    }

    private sealed class Session
    {
        private readonly LLMClient client;
        private readonly JObject evidence;
        private readonly string task, owner, evidenceHash, originalPrompt, digest;
        private readonly YQRepairEpisode episode;
        private readonly Func<bool> stillCurrent;
        private readonly Action<Result> onComplete;
        private readonly YQGoddessSpeechPlan.Purpose purpose;
        private readonly string playerStatement;

        public Session(LLMClient transport, JObject snapshot, string request, string ownerId, string hash,
            YQRepairEpisode transaction, Func<bool> current, Action<Result> callback, YQGoddessSpeechPlan.Purpose speechPurpose, string currentStatement)
        {
            client = transport; evidence = snapshot; task = request; owner = ownerId; evidenceHash = hash;
            episode = transaction; stillCurrent = current; onComplete = callback;
            purpose = speechPurpose; playerStatement = currentStatement;
            digest = client.QualifiedGoddessSpeechPlanDigest;
            originalPrompt = YQGoddessSpeechPlan.BuildPrompt(evidence, task, purpose, playerStatement);
        }

        private bool Current()
        {
            if (episode.IsTerminal) return false;
            if (client != null && YQServiceLifecycle.IsCurrent(episode.generationEpoch) && (stillCurrent == null || stillCurrent())) return true;
            episode.Finish("Superseded");
            return false;
        }

        private bool MatchesScope(YQLlmRequestResult result)
        {
            // note: A response from another episode, profile, world or revision cannot authorize this speech.
            return result.repairEpisodeKey == episode.key &&
                result.profileId == episode.profileId && result.worldId == episode.worldId &&
                result.generationEpoch == episode.generationEpoch && result.ownerId == episode.ownerId &&
                result.playerStateRevision == episode.playerRevision && result.worldStateRevision == episode.worldRevision;
        }

        private bool Belongs(YQLlmRequestResult result, bool verification)
        {
            // note: Scope alone cannot accept a sibling response; the last physical call must be this exact lane and request.
            if (!MatchesScope(result) || episode.Calls.Count == 0) return false;
            YQRepairEpisode.CallStamp call = episode.Calls[episode.Calls.Count - 1];
            return call.verification == verification && result.repairRequestKey == call.requestKey &&
                result.category == (verification ? LLMGenerationCategory.GoddessVerification : LLMGenerationCategory.GoddessCommentary);
        }

        public void Generate(string diagnostic, string previous, int repairs, string parentKey = null)
        {
            if (!Current()) return;
            if (!client.GoddessSpeechPlanEnabled || !string.Equals(client.QualifiedGoddessSpeechPlanDigest, digest, StringComparison.Ordinal))
            { Finish("SpeechPlanUnavailable", null); return; }
            if (!episode.CanSubmit(false)) { Finish("FallbackUsed", null); return; }
            string prompt = repairs == 0 ? originalPrompt : YQRepairEpisode.RepairPrompt(originalPrompt, diagnostic, previous);
            YQLlmRequest request = new YQLlmRequest {
                prompt = prompt, debugTag = "GoddessSpeech:" + owner, category = LLMGenerationCategory.GoddessCommentary,
                priority = YQLlmRequestPriority.Background, requireJson = true, deferJsonValidationToCaller = true,
                jsonSchema = YQGoddessSpeechPlan.Schema(evidence, purpose, playerStatement), maxRetries = 0, parentRequestKey = parentKey,
                requiredOllamaModelDigest = digest,
                ownerStillCurrent = () => client != null && (stillCurrent == null || stillCurrent()),
                optionsOverride = new Dictionary<string, object> { { "request_timeout_seconds", 15 }, { "presence_penalty", 0f },
                    { "num_ctx", YQGoddessSpeechPlan.ContextTokens } }
            };
            // note: A qualified CPU route can leave occupied graphics memory available to the game; it is part of the contract signature.
            if (client.GoddessSpeechPlanCpuOnly) request.optionsOverride["num_gpu"] = 0;
            if (repairs > 0) request.optionsOverride["temperature"] = 0.28f;
            episode.Bind(request);
            client.Submit(request, generated =>
            {
                if (!Current()) return;
                if (Retired(generated)) { episode.Finish("Superseded"); return; }
                if (episode.RemainingSeconds < 1d) { Finish("DeadlineExhausted", null); return; }
                if (!client.GoddessSpeechPlanEnabled || !string.Equals(client.QualifiedGoddessSpeechPlanDigest, digest, StringComparison.Ordinal))
                { Finish("SpeechPlanUnavailable", null); return; }
                if (!MatchesScope(generated) || (generated.success && !Belongs(generated, false)))
                { Finish("ResponseBindingMismatch", null); return; }
                string raw = generated.success ? generated.text : null;
                string line = string.Empty, error = generated.error;
                if (!generated.success || !YQGoddessSpeechPlan.TryCompose(raw, evidence, out line, out error, purpose, playerStatement))
                {
                    Retry(generated.success ? error : generated.error, raw, repairs, generated.repairRequestKey);
                    return;
                }
                // note: Exact claim binding and the expression grammar establish acceptance; an unqualified classifier cannot overrule them.
                Finish(repairs == 0 ? "OriginalAccepted" : "RepairedAccepted", line);
            });
        }

        private void Retry(string diagnostic, string raw, int repairs, string parentKey)
        {
            // note: Initial generation plus two corrections share the episode's three physical-call and submission budgets.
            if (repairs < 2 && episode.CanSubmit(false) && !(diagnostic ?? string.Empty).StartsWith("ContextOverflow", StringComparison.Ordinal))
                Generate(diagnostic, raw, repairs + 1, parentKey);
            else Finish("FallbackUsed", null);
        }

        private void Finish(string disposition, string line)
        {
            if (!Current() || !episode.Finish(disposition)) return;
            onComplete?.Invoke(new Result(line ?? YQGoddessGrounding.UnknownLine, disposition, evidenceHash, digest, line == null));
        }

        private static bool Retired(YQLlmRequestResult result) => result.outcome == YQLlmTerminalOutcome.Cancelled ||
            result.outcome == YQLlmTerminalOutcome.Superseded || result.outcome == YQLlmTerminalOutcome.Evicted;
    }
}
