using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Applies the LLM's progression decision.
/// Uses the EXISTING skill pipeline:
/// Decision -> EmergentSkill draft -> EventAccumulator.AddGhostSkillOrUpgradeCandidate(draft)
/// Titles/Quests can be added later using the same pattern.
/// </summary>
public class ProgressionDecisionApplier : MonoBehaviour
{
    [Header("Config")]
    [Range(0f, 1f)]
    public float minConfidence = 0.25f;

    [Header("Context Gate (Skill Only)")]
    public bool gateSkillsToCalmLowThreat = true;

    [Header("Refs")]
    public PlayerProfile playerProfile;
    public SituationSnapshotBuilder snapshotBuilder;

    [Serializable]
    private class ProgressionDecision
    {
        public string decision;   // none | skill | title | quest
        public float confidence;
        public string reason;
        public JObject payload;
    }

    public bool TryApply(string rawJson, out string appliedCategory, out string reason)
    {
        appliedCategory = "none";
        reason = "No decision applied.";

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            reason = "Empty LLM response.";
            return false;
        }

        if (!TryParseDecision(rawJson, out var d, out string parseErr))
        {
            reason = "Parse failed: " + parseErr;
            Debug.LogWarning($"[ProgressionDecisionApplier] {reason}\nRAW:\n{rawJson}");
            return false;
        }

        if (d == null)
        {
            reason = "Decision was null after parse.";
            return false;
        }

        if (d.confidence < minConfidence)
        {
            reason = $"Low confidence ({d.confidence:0.00} < {minConfidence:0.00}).";
            return false;
        }

        string dec = (d.decision ?? "none").Trim().ToLowerInvariant();
        if (dec == "none")
        {
            reason = string.IsNullOrWhiteSpace(d.reason) ? "LLM decided none." : d.reason;
            return false;
        }

        // Gate only skills (titles/quests later can have their own gates).
        if (dec == "skill" && gateSkillsToCalmLowThreat)
        {
            if (snapshotBuilder == null)
                snapshotBuilder = FindFirstObjectByType<SituationSnapshotBuilder>();

            if (snapshotBuilder != null)
            {
                var s = SituationSnapshot.Parse(snapshotBuilder.BuildSnapshot());

                bool combatCALM = s.combat == "CALM";
                bool lowThreat = s.flags.Contains("LOW_THREAT");
                bool inTarN0 = s.incomingTargets == 0;

                if (!combatCALM || !lowThreat || !inTarN0)
                {
                    reason =
                        "Skill blocked by context | " +
                        $"combatCALM={combatCALM}, lowThreat={lowThreat}, inTarN0={inTarN0}";
                    Debug.Log($"[ProgressionDecisionApplier] {reason}");
                    return false;
                }
            }
        }

        switch (dec)
        {
            case "skill":
                if (TryApplySkill(d.payload, out reason))
                {
                    appliedCategory = "skill";
                    return true;
                }
                return false;

            case "title":
                reason = "Title system not implemented yet (planned). Decision ignored safely.";
                return false;

            case "quest":
                reason = "Quest system not implemented yet (planned). Decision ignored safely.";
                return false;

            default:
                reason = $"Unknown decision '{d.decision}'.";
                return false;
        }
    }

    private bool TryApplySkill(JObject payload, out string reason)
    {
        reason = "Skill applied.";

        if (payload == null)
        {
            reason = "Missing payload for skill.";
            return false;
        }

        string skillSeedName = payload.Value<string>("skillSeedName") ?? "";
        string skillTypeStr = payload.Value<string>("skillType") ?? ""; // combat|movement|utility|craft|social
        string hook = payload.Value<string>("hook") ?? "";

        if (string.IsNullOrWhiteSpace(skillSeedName))
        {
            reason = "Skill payload missing skillSeedName.";
            return false;
        }

        var acc = EventAccumulator.Instance;
        if (acc == null)
        {
            reason = "EventAccumulator.Instance missing (cannot store skill draft).";
            return false;
        }

        // Create an in-memory draft ScriptableObject (not an asset yet).
        // Later, SkillCommitter.Commit(draft, playerProfile) can create the committed SkillData asset in Editor. :contentReference[oaicite:2]{index=2} :contentReference[oaicite:3]{index=3}
        var draft = ScriptableObject.CreateInstance<EmergentSkill>(); // :contentReference[oaicite:4]{index=4}

        draft.draftId = Guid.NewGuid().ToString("N");
        draft.skillName = skillSeedName;

        // Use hook as the primary short description; you can swap this later to use richer LLM output if desired.
        draft.description = string.IsNullOrWhiteSpace(hook) ? $"A nascent technique: {skillSeedName}." : hook;

        // Your SkillType enum is Passive/Active/Ultimate, while the prompt uses combat/movement/etc.
        // So: keep enum default (Active) and store prompt skillType in tags/context.
        draft.type = SkillType.Active;

        // Light grounding metadata
        draft.context = $"llm_seed:{skillTypeStr}";
        draft.environment = "runtime";

        // Tags drive SkillSimilarity matching vs committed skills in EventAccumulator.AddGhostSkillOrUpgradeCandidate. :contentReference[oaicite:5]{index=5}
        draft.contextTags = BuildTags(skillTypeStr);

        // Optional bookkeeping
        draft.fitScore = 0f;
        draft.committed = false;
        draft.committedSkillId = null;
        draft.createdUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // IMPORTANT: this is where your upgrade matching happens (committedSkills + SkillSimilarity).
        acc.AddGhostSkillOrUpgradeCandidate(draft);

        reason = $"Draft skill stored: '{draft.skillName}' (tag={skillTypeStr}).";
        return true;
    }

    private static string[] BuildTags(string skillTypeStr)
    {
        var tags = new List<string>(4);

        if (!string.IsNullOrWhiteSpace(skillTypeStr))
            tags.Add(skillTypeStr.Trim().ToLowerInvariant());

        // A couple general tags for filtering later
        tags.Add("emergent");
        tags.Add("llm");

        return tags.ToArray();
    }

    private bool TryParseDecision(string raw, out ProgressionDecision d, out string error)
    {
        d = null;
        error = null;

        try
        {
            string json = ExtractFirstJsonObject(raw);
            d = JsonConvert.DeserializeObject<ProgressionDecision>(json);
            if (d == null) { error = "Deserialized null decision."; return false; }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private string ExtractFirstJsonObject(string raw)
    {
        int start = raw.IndexOf('{');
        if (start < 0) return raw;

        int depth = 0;
        for (int i = start; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return raw.Substring(start, i - start + 1);
            }
        }

        return raw.Substring(start);
    }

    // Reads your snapshot JSON: keys include combat, inTarN, sf (string array).
    private class SituationSnapshot
    {
        public string combat;
        public int incomingTargets;
        public HashSet<string> flags = new HashSet<string>();

        public static SituationSnapshot Parse(string json)
        {
            var snap = new SituationSnapshot();

            try
            {
                var j = JObject.Parse(json);

                snap.combat = (j.Value<string>("combat") ?? "UNKNOWN").Trim();
                snap.incomingTargets = SafeInt(j["inTarN"]);

                if (j["sf"] is JArray sf)
                {
                    foreach (var f in sf)
                    {
                        var s = f?.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                            snap.flags.Add(s.Trim());
                    }
                }
            }
            catch
            {
                snap.combat = "UNKNOWN";
                snap.incomingTargets = 0;
            }

            return snap;
        }

        private static int SafeInt(JToken t)
        {
            if (t == null) return 0;
            if (t.Type == JTokenType.Integer) return t.Value<int>();
            if (t.Type == JTokenType.String && int.TryParse(t.Value<string>(), out int v)) return v;
            return 0;
        }
    }
}
