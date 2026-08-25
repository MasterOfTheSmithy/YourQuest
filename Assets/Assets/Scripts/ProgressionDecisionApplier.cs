// Assets/Assets/Scripts/ProgressionDecisionApplier.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class ProgressionDecisionApplier : MonoBehaviour
{
    [Header("Base Confidence Gates")]
    [Range(0f, 1f)] public float minConfidence = 0.70f;
    [Range(0f, 1f)] public float minSkillConfidence = 0.78f;
    [Range(0f, 1f)] public float minTitleConfidence = 0.80f;
    [Range(0f, 1f)] public float minClassConfidence = 0.82f;
    [Range(0f, 1f)] public float minQuestConfidence = 0.82f;

    [Header("Similarity Gates")]
    [Range(0f, 1f)] public float duplicateSkillThreshold = 0.95f;
    [Range(0f, 1f)] public float upgradeSkillThreshold = 0.74f;
    [Range(0f, 1f)] public float duplicateTitleThreshold = 0.94f;
    [Range(0f, 1f)] public float duplicateClassThreshold = 0.94f;
    [Range(0f, 1f)] public float duplicateQuestThreshold = 0.90f;
    [Range(0f, 1f)] public float pendingDuplicateThreshold = 0.94f;

    [Header("Evolution / Near-Match Control")]
    [Range(0f, 1f)] public float evolutionSimilarityThreshold = 0.50f;
    [Min(1)] public int evolutionStepsRequired = 3;
    [Min(1)] public int evolutionBonusTier = 1;
    public bool logEvolutionToLedger = true;

    [Header("Oddity Incubation")]
    public bool incubateOdditiesBeforeOffering = true;
    [Min(1)] public int oddityEvolutionStepsRequired = 3;
    public bool logOdditiesToLedger = true;

    [Header("Context Gates")]
    public bool gateSkillsToCalmLowThreat = true;
    public bool gateQuestsToMeaningfulContext = true;

    [Header("Deterministic Player Evidence")]
    public bool requirePlayerEvidenceForSkills = true;
    public bool requirePlayerEvidenceForQuests = true;
    [Range(0f, 1f)] public float minSkillEvidenceScore = 0.34f;
    [Range(0f, 1f)] public float minQuestEvidenceScore = 0.30f;

    [Header("Refs")]
    public PlayerProfile playerProfile;
    public SituationSnapshotBuilder snapshotBuilder;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    [Serializable]
    private sealed class ProgressionDecision
    {
        public string decision = string.Empty;
        public float confidence = 0f;
        public string reason = string.Empty;
        public JObject payload = new JObject();
    }

    public bool TryApply(string rawJson, out string appliedCategory, out string reason)
    {
        ResolveReferences();
        appliedCategory = "none";
        reason = "No decision applied.";

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            reason = "Empty LLM response.";
            return false;
        }

        if (!TryParseDecision(rawJson, out ProgressionDecision decision, out string parseError))
        {
            reason = "Parse failed: " + parseError;
            return false;
        }

        float confidence = Mathf.Clamp01(decision.confidence);
        if (confidence < minConfidence)
        {
            reason = "Low confidence.";
            return false;
        }

        string kind = SafeLower(decision.decision);
        switch (kind)
        {
            case "skill":
            case "spell":
                if (confidence < minSkillConfidence)
                {
                    reason = "Skill confidence below threshold.";
                    return false;
                }
                if (gateSkillsToCalmLowThreat && !PassesSkillGate(out reason))
                    return false;
                if (TryQueueSkillOffer(decision.payload, decision.reason, confidence, kind == "spell", out reason))
                {
                    appliedCategory = kind;
                    return true;
                }
                return false;

            case "title":
                if (confidence < minTitleConfidence)
                {
                    reason = "Title confidence below threshold.";
                    return false;
                }
                if (TryQueueSimpleOffer("title", decision.payload, decision.reason, confidence, duplicateTitleThreshold, out reason))
                {
                    appliedCategory = kind;
                    return true;
                }
                return false;

            case "quest":
                if (confidence < minQuestConfidence)
                {
                    reason = "Quest confidence below threshold.";
                    return false;
                }
                if (gateQuestsToMeaningfulContext && !PassesQuestGate(out reason))
                    return false;
                if (TryQueueSimpleOffer("quest", decision.payload, decision.reason, confidence, duplicateQuestThreshold, out reason))
                {
                    appliedCategory = kind;
                    return true;
                }
                return false;

            case "class":
                if (confidence < minClassConfidence)
                {
                    reason = "Class confidence below threshold.";
                    return false;
                }
                if (TryQueueSimpleOffer("class", decision.payload, decision.reason, confidence, duplicateClassThreshold, out reason))
                {
                    appliedCategory = kind;
                    return true;
                }
                return false;

            case "item":
                if (confidence < minQuestConfidence)
                {
                    reason = "Item confidence below threshold.";
                    return false;
                }
                if (TryQueueItemOffer(decision.payload, decision.reason, confidence, out reason))
                {
                    appliedCategory = kind;
                    return true;
                }
                return false;

            default:
                reason = "Decision ignored.";
                return false;
        }
    }

    private bool PassesSkillGate(out string reason)
    {
        reason = string.Empty;
        SituationSnapshot snapshot = GetSnapshot();
        if (snapshot == null)
            return true;

        bool combatCalm = snapshot.combat == "CALM";
        bool lowThreat = snapshot.flags.Contains("LOW_THREAT");
        bool incomingZero = snapshot.incomingTargets == 0;
        if (combatCalm && lowThreat && incomingZero)
            return true;

        reason = "Skill blocked by context.";
        return false;
    }

    private bool PassesQuestGate(out string reason)
    {
        reason = string.Empty;
        SituationSnapshot snapshot = GetSnapshot();
        if (snapshot == null)
            return true;

        bool hasRegion = !string.IsNullOrWhiteSpace(snapshot.regId) && snapshot.regId != "region_unknown";
        bool hasNearby = snapshot.nearbyNotableCount > 0 || snapshot.nearbyHostileCount > 0;
        bool hasFlags = snapshot.flags.Count > 0;
        if (hasRegion && (hasNearby || hasFlags))
            return true;

        reason = "Quest blocked by low-context snapshot.";
        return false;
    }

    private SituationSnapshot GetSnapshot()
    {
        ResolveReferences();
        if (snapshotBuilder == null)
            return null;
        return SituationSnapshot.Parse(snapshotBuilder.BuildSnapshot());
    }

    private void ResolveReferences()
    {
        if (snapshotBuilder == null)
            snapshotBuilder = FindFirstObjectByType<SituationSnapshotBuilder>();

        if (playerProfile != null)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            playerProfile = player.GetComponent<PlayerProfile>();

        if (playerProfile == null)
            playerProfile = FindFirstObjectByType<PlayerProfile>();
    }

    private bool TryQueueSkillOffer(JObject payload, string modelReason, float confidence, bool isSpell, out string reason)
    {
        reason = "Skill offer queued.";
        PlayerStateManager manager = PlayerStateManager.Instance;
        if (manager == null || manager.state == null)
        {
            reason = "PlayerStateManager missing.";
            return false;
        }

        string name = GetTrimmed(payload, "skillSeedName", "skillName", "spellName", "name");
        string type = GetTrimmed(payload, "skillType", "type");
        string hook = GetTrimmed(payload, "hook", "description", "reason");
        string stimulus = GetTrimmed(payload, "stimulus", "trigger", "evidence");
        string loreAnchor = GetTrimmed(payload, "loreAnchor", "lore", "precursor", "source");
        if (string.IsNullOrWhiteSpace(name))
        {
            reason = "Skill payload missing name.";
            return false;
        }

        PlayerState state = manager.state;
        state.EnsureCollections();

        string loweredType = string.IsNullOrWhiteSpace(type) ? (isSpell ? "spell" : "combat") : type.Trim().ToLowerInvariant();
        name = YQGeneratedContentCuration.CuratePlayerFacingName(
            state,
            isSpell ? "spell" : "skill",
            name,
            loweredType,
            isSpell,
            stimulus);
        hook = YQGeneratedContentCuration.CuratePlayerFacingDescription(
            state,
            isSpell ? "spell" : "skill",
            name,
            hook,
            loweredType,
            isSpell,
            stimulus,
            loreAnchor);
        string[] offerTags = BuildTags(loweredType, isSpell, name + " " + hook + " " + stimulus + " " + loreAnchor);
        if (YQGeneratedContentCuration.IsOddityCandidate(name, hook, offerTags) &&
            !TryPromoteOdditySeed(state, isSpell ? "spell" : "skill", name, hook, stimulus, ref offerTags, out reason))
        {
            manager.Save();
            return false;
        }

        if (requirePlayerEvidenceForSkills &&
            !PassesPlayerEvidenceGate(
                state,
                isSpell ? "spell" : "skill",
                name,
                hook,
                offerTags,
                loweredType,
                stimulus,
                Mathf.Clamp01(minSkillEvidenceScore),
                out float skillEvidenceScore,
                out string skillEvidenceLabel))
        {
            reason = "Rejected " + (isSpell ? "spell" : "skill") + " without matching player evidence (" + skillEvidenceLabel + ", " + skillEvidenceScore.ToString("0.00") + ").";
            return false;
        }

        if (!YQGeneratedContentCuration.PassesBasicQuality(isSpell ? "spell" : "skill", name, hook, offerTags, confidence, out reason))
            return false;

        if (TryRejectNearMatch(state, "skill", name, hook, offerTags, confidence, out SkillRecord evolvedTarget, out reason))
            return false;

        SkillRecord bestMatch = evolvedTarget ?? state.FindBestSkillMatch(name, hook, offerTags, upgradeSkillThreshold);
        bool isUpgrade = bestMatch != null;
        int tierBonus = evolvedTarget != null ? Mathf.Max(0, evolutionBonusTier) : 0;
        int proposedTier = isUpgrade ? Mathf.Max(bestMatch.tier + 1 + tierBonus, 2 + tierBonus) : 1;
        string familyId = isUpgrade && !string.IsNullOrWhiteSpace(bestMatch.familyId) ? bestMatch.familyId : Guid.NewGuid().ToString("N");

        PendingProgressionOfferRecord offer = new PendingProgressionOfferRecord
        {
            offerKind = isSpell ? "spell" : "skill",
            name = name.Trim(),
            description = hook.Trim(),
            confidence = confidence,
            reason = BuildPlayerFacingReason(modelReason, stimulus),
            isUpgrade = isUpgrade,
            upgradeTargetId = isUpgrade ? bestMatch.skillId : null,
            upgradeTargetName = isUpgrade ? bestMatch.name : null,
            familyId = familyId,
            proposedTier = proposedTier,
            isSpell = isSpell || string.Equals(loweredType, "spell", StringComparison.OrdinalIgnoreCase),
            skillType = loweredType,
            context = "player_response:" + loweredType,
            environment = "player_profile",
            tags = offerTags,
            payloadJson = payload != null ? payload.ToString(Formatting.None) : string.Empty,
            offeredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        PendingProgressionOfferRecord queued = state.QueueOrRefreshOffer(offer, pendingDuplicateThreshold);
        manager.Save();
        reason = queued.isUpgrade
            ? "Queued skill upgrade offer: " + queued.name + " -> " + queued.upgradeTargetName
            : "Queued " + queued.offerKind + " offer: " + queued.name;
        return true;
    }

    private bool TryQueueSimpleOffer(string kind, JObject payload, string modelReason, float confidence, float duplicateThreshold, out string reason)
    {
        reason = kind + " offer queued.";
        PlayerStateManager manager = PlayerStateManager.Instance;
        if (manager == null || manager.state == null)
        {
            reason = "PlayerStateManager missing.";
            return false;
        }

        string name = GetTrimmed(payload,
            kind == "title" ? "titleName" : kind == "class" ? "className" : "questName",
            "name");
        string description = GetTrimmed(payload, "description", "hook", "reason");
        string stimulus = GetTrimmed(payload, "stimulus", "trigger", "evidence");
        string loreAnchor = GetTrimmed(payload, "loreAnchor", "lore", "precursor", "source");
        if (string.IsNullOrWhiteSpace(name))
        {
            reason = kind + " payload missing name.";
            return false;
        }

        PlayerState state = manager.state;
        state.EnsureCollections();

        name = YQGeneratedContentCuration.CuratePlayerFacingName(
            state,
            kind,
            name,
            kind,
            false,
            stimulus);
        description = YQGeneratedContentCuration.CuratePlayerFacingDescription(
            state,
            kind,
            name,
            description,
            kind,
            false,
            stimulus,
            loreAnchor);
        string[] tags = kind == "quest"
            ? YQGeneratedContentCuration.BuildPlayerResponseTags(ReadStringArray(payload, "tags"), kind, false, name + " " + description + " " + stimulus + " " + loreAnchor)
            : YQGeneratedContentCuration.BuildPlayerResponseTags(Array.Empty<string>(), kind, false, name + " " + description + " " + stimulus + " " + loreAnchor);
        if (kind == "quest" && !HasSupportedQuestObjective(payload, out reason))
            return false;
        if (YQGeneratedContentCuration.IsOddityCandidate(name, description, tags) &&
            !TryPromoteOdditySeed(state, kind, name, description, stimulus, ref tags, out reason))
        {
            manager.Save();
            return false;
        }

        if (kind == "quest" &&
            requirePlayerEvidenceForQuests &&
            !PassesPlayerEvidenceGate(
                state,
                kind,
                name,
                description,
                tags,
                kind,
                stimulus,
                Mathf.Clamp01(minQuestEvidenceScore),
                out float questEvidenceScore,
                out string questEvidenceLabel))
        {
            reason = "Rejected quest without matching player evidence (" + questEvidenceLabel + ", " + questEvidenceScore.ToString("0.00") + ").";
            return false;
        }

        if (!YQGeneratedContentCuration.PassesBasicQuality(kind, name, description, tags, confidence, out reason))
            return false;

        if (TryRejectNearMatch(state, kind, name, description, tags, confidence, out _, out reason))
            return false;

        PendingProgressionOfferRecord offer = new PendingProgressionOfferRecord
        {
            offerKind = kind,
            name = name.Trim(),
            description = description,
            confidence = confidence,
            reason = BuildPlayerFacingReason(modelReason, stimulus),
            tags = tags,
            payloadJson = payload != null ? payload.ToString(Formatting.None) : string.Empty,
            offeredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        PendingProgressionOfferRecord queued = state.QueueOrRefreshOffer(offer, Mathf.Max(pendingDuplicateThreshold, duplicateThreshold));
        manager.Save();
        reason = "Queued " + kind + " offer: " + queued.name;
        return true;
    }

    private static bool HasSupportedQuestObjective(JObject payload, out string reason)
    {
        reason = string.Empty;
        JObject objective = payload?["objective"] as JObject;
        string type = objective?["type"]?.ToString().Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(type))
        {
            reason = "Quest payload requires one structured objective.";
            return false;
        }

        bool supported = type == "equip_item" ||
            type == "talk_to_npc" ||
            type == "cast_spell" ||
            type == "defeat_enemy" ||
            type == "loot_item" ||
            type == "pickup_item" ||
            type == "open_lock" ||
            type == "mimic_reveal" ||
            type == "use_shrine" ||
            type == "enter_region" ||
            type == "wait_seconds";

        if (!supported)
        {
            // note: Generated narrative may name anything, but quest execution is restricted to the stable runtime objective contract.
            reason = "Unsupported quest objective type: " + type;
            return false;
        }

        if ((type == "talk_to_npc" || type == "enter_region") &&
            string.IsNullOrWhiteSpace(objective["targetId"]?.ToString()))
        {
            reason = "Quest objective " + type + " requires a stable targetId.";
            return false;
        }

        return true;
    }

    private bool TryQueueItemOffer(JObject payload, string modelReason, float confidence, out string reason)
    {
        reason = "Item offer queued.";
        PlayerStateManager manager = PlayerStateManager.Instance;
        if (manager == null || manager.state == null)
        {
            reason = "PlayerStateManager missing.";
            return false;
        }

        string name = GetTrimmed(payload, "itemName", "name");
        string itemType = NormalizeItemType(GetTrimmed(payload, "itemType", "type", "equipSlot"));
        string description = GetTrimmed(payload, "description", "hook", "reason");
        string stimulus = GetTrimmed(payload, "stimulus", "trigger", "evidence");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(itemType))
        {
            reason = "Item payload missing a usable name or type.";
            return false;
        }

        PlayerState state = manager.state;
        state.EnsureCollections();
        name = YQGeneratedContentCuration.CuratePlayerFacingName(state, "item", name, itemType, false, stimulus);
        description = YQGeneratedContentCuration.CuratePlayerFacingDescription(state, "item", name, description, itemType, false, stimulus);
        string[] tags = YQGeneratedContentCuration.BuildPlayerResponseTags(ReadStringArray(payload, "tags"), "item", false, name + " " + description + " " + stimulus);
        if (!YQGeneratedContentCuration.PassesOfferQuality(state, "item", name, description, tags, confidence, true, out reason))
            return false;

        PendingProgressionOfferRecord offer = new PendingProgressionOfferRecord
        {
            // note: The payload keeps semantic intent only; GeneratedRpgContentService owns the eventual prefab, material, and stat binding.
            offerKind = "item",
            name = name,
            description = description,
            confidence = confidence,
            reason = BuildPlayerFacingReason(modelReason, stimulus),
            skillType = itemType,
            context = "player_response:" + itemType,
            environment = "player_profile",
            tags = tags,
            payloadJson = payload != null ? payload.ToString(Formatting.None) : string.Empty,
            offeredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        PendingProgressionOfferRecord queued = state.QueueOrRefreshOffer(offer, pendingDuplicateThreshold);
        manager.Save();
        reason = "Queued item offer: " + queued.name;
        return true;
    }

    private static string NormalizeItemType(string raw)
    {
        string type = SafeLower(raw);
        switch (type)
        {
            case "weapon": case "offhand": case "head": case "chest": case "gloves": case "legs":
            case "boots": case "belt": case "cloak": case "ring": case "earring": case "necklace":
            case "trinket": case "consumable":
                return type;
            default:
                return string.Empty;
        }
    }

    private bool TryRejectNearMatch(
        PlayerState state,
        string kind,
        string candidateName,
        string candidateDescription,
        string[] tags,
        float confidence,
        out SkillRecord evolvedSkillTarget,
        out string reason)
    {
        evolvedSkillTarget = null;
        reason = string.Empty;

        if (state == null)
            return false;

        float bestScore = 0f;
        string bestName = string.Empty;
        string bestCounterKey = string.Empty;

        if (string.Equals(kind, "skill", StringComparison.OrdinalIgnoreCase) || string.Equals(kind, "spell", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < state.skills.Count; i++)
            {
                SkillRecord skill = state.skills[i];
                if (skill == null)
                    continue;

                float score = SkillSimilarity.Score(candidateName, candidateDescription, tags, skill.name, skill.description, BuildSkillTags(skill));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestName = skill.name;
                    bestCounterKey = BuildEvolutionCounterKey(kind, skill.name);
                    evolvedSkillTarget = skill;
                }
            }
        }
        else
        {
            switch (kind)
            {
                case "title":
                    for (int i = 0; i < state.titles.Count; i++)
                    {
                        TitleRecord record = state.titles[i];
                        if (record == null)
                            continue;
                        float score = SkillSimilarity.Score(candidateName, candidateDescription, tags, record.name, record.description, Array.Empty<string>());
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestName = record.name;
                            bestCounterKey = BuildEvolutionCounterKey(kind, record.name);
                        }
                    }
                    break;

                case "class":
                    for (int i = 0; i < state.classes.Count; i++)
                    {
                        ClassRecord record = state.classes[i];
                        if (record == null)
                            continue;
                        float score = SkillSimilarity.Score(candidateName, candidateDescription, tags, record.name, record.description, Array.Empty<string>());
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestName = record.name;
                            bestCounterKey = BuildEvolutionCounterKey(kind, record.name);
                        }
                    }
                    break;

                case "quest":
                    for (int i = 0; i < state.quests.Count; i++)
                    {
                        QuestRecord record = state.quests[i];
                        if (record == null)
                            continue;
                        float score = SkillSimilarity.Score(candidateName, candidateDescription, tags, record.name, record.description, record.tags ?? Array.Empty<string>());
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestName = record.name;
                            bestCounterKey = BuildEvolutionCounterKey(kind, record.name);
                        }
                    }
                    break;
            }
        }

        if (bestScore < evolutionSimilarityThreshold || string.IsNullOrWhiteSpace(bestCounterKey))
            return false;

        state.IncCounter(bestCounterKey, 1f);

        int steps = Mathf.RoundToInt(state.behaviorCounters.TryGetValue(bestCounterKey, out float current) ? current : 0f);
        if (logEvolutionToLedger && !string.IsNullOrWhiteSpace(bestName))
            state.AddLedgerLine($"Near-match {kind} '{candidateName}' advanced toward '{bestName}' ({bestScore:0.00}, step {steps}/{evolutionStepsRequired}).");

        if (steps >= evolutionStepsRequired &&
            evolvedSkillTarget != null &&
            (string.Equals(kind, "skill", StringComparison.OrdinalIgnoreCase) || string.Equals(kind, "spell", StringComparison.OrdinalIgnoreCase)) &&
            confidence >= minSkillConfidence)
        {
            reason = $"Near-match {kind} promoted into an evolution of {bestName}.";
            return false;
        }

        evolvedSkillTarget = null;
        reason = $"Rejected near-duplicate {kind}; evolution step logged for {bestName} ({bestScore:0.00}).";
        return true;
    }

    private bool TryPromoteOdditySeed(
        PlayerState state,
        string kind,
        string candidateName,
        string candidateDescription,
        string stimulus,
        ref string[] tags,
        out string reason)
    {
        reason = string.Empty;
        if (state == null)
            return false;

        if (!incubateOdditiesBeforeOffering)
        {
            tags = YQGeneratedContentCuration.AddProgressionTags(tags, "oddity_seed", "evolved_oddity");
            return true;
        }

        string key = BuildOddityCounterKey(kind, candidateName, stimulus);
        state.IncCounter(key, 1f);
        int steps = Mathf.RoundToInt(state.behaviorCounters.TryGetValue(key, out float current) ? current : 0f);
        int required = Mathf.Max(1, oddityEvolutionStepsRequired);

        if (logOdditiesToLedger)
        {
            string name = string.IsNullOrWhiteSpace(candidateName) ? "unnamed oddity" : candidateName.Trim();
            state.AddLedgerLine("Oddity seed '" + name + "' repeated (" + steps + "/" + required + "). It will not become progression until it proves itself.");
        }

        if (steps < required)
        {
            reason = "Oddity incubating (" + steps + "/" + required + ").";
            return false;
        }

        tags = YQGeneratedContentCuration.AddProgressionTags(tags, "oddity_seed", "evolved_oddity");
        reason = "Oddity evolved after repeated evidence.";
        return true;
    }

    private static string BuildOddityCounterKey(string kind, string candidateName, string stimulus)
    {
        string source = SafeLower(kind) + ":" + SafeLower(candidateName) + ":" + SafeLower(stimulus);
        return "oddity:" + StableHash(source).ToString("x8");
    }

    private static string BuildEvolutionCounterKey(string kind, string anchorName)
    {
        return "evolution:" + SafeLower(kind) + ":" + SafeLower(anchorName).Replace(' ', '_');
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
                hash = hash * 31 + text[i];
            return hash;
        }
    }

    private static bool TryParseDecision(string rawJson, out ProgressionDecision decision, out string parseError)
    {
        decision = null;
        parseError = string.Empty;
        try
        {
            string trimmed = rawJson.Trim();
            int start = trimmed.IndexOf('{');
            int end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
                trimmed = trimmed.Substring(start, end - start + 1);
            decision = JsonConvert.DeserializeObject<ProgressionDecision>(trimmed);
            return decision != null;
        }
        catch (Exception ex)
        {
            parseError = ex.Message;
            return false;
        }
    }

    private static string GetTrimmed(JObject payload, params string[] keys)
    {
        if (payload == null || keys == null)
            return string.Empty;
        for (int i = 0; i < keys.Length; i++)
        {
            string value = payload.Value<string>(keys[i]);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return string.Empty;
    }

    private static string[] ReadStringArray(JObject payload, string key)
    {
        if (payload == null || payload[key] == null)
            return Array.Empty<string>();
        try { return payload[key].ToObject<string[]>() ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    private static string SafeLower(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string BuildPlayerFacingReason(string modelReason, string stimulus)
    {
        string cleanReason = string.IsNullOrWhiteSpace(modelReason) ? string.Empty : modelReason.Trim();
        string cleanStimulus = string.IsNullOrWhiteSpace(stimulus) ? string.Empty : stimulus.Trim();

        if (string.IsNullOrWhiteSpace(cleanReason) ||
            cleanReason.IndexOf("generated", StringComparison.OrdinalIgnoreCase) >= 0 ||
            cleanReason.IndexOf("region", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return string.IsNullOrWhiteSpace(cleanStimulus)
                ? "Queued because recent play shows a repeatable player stimulus."
                : "Queued because recent play shows this repeatable stimulus: " + cleanStimulus;
        }

        return cleanReason;
    }

    private static string[] BuildTags(string loweredType, bool isSpell, string text)
    {
        List<string> tags = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(loweredType))
            tags.Add(loweredType);
        tags.Add(isSpell ? "spell" : "skill");
        return YQGeneratedContentCuration.BuildPlayerResponseTags(tags.ToArray(), loweredType, isSpell, text);
    }

    private static string[] BuildSkillTags(SkillRecord skill)
    {
        if (skill == null)
            return Array.Empty<string>();

        List<string> tags = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(skill.type))
            tags.Add(skill.type.Trim().ToLowerInvariant());
        if (skill.isSpell)
            tags.Add("spell");
        if (!string.IsNullOrWhiteSpace(skill.context))
            tags.Add(skill.context.Trim().ToLowerInvariant());
        return tags.ToArray();
    }

    private static bool PassesPlayerEvidenceGate(
        PlayerState state,
        string kind,
        string name,
        string description,
        string[] tags,
        string type,
        string stimulus,
        float minimumScore,
        out float score,
        out string label)
    {
        score = ComputePlayerEvidenceScore(state, kind, name, description, tags, type, stimulus, out label);
        return score >= Mathf.Clamp01(minimumScore);
    }

    private static float ComputePlayerEvidenceScore(
        PlayerState state,
        string kind,
        string name,
        string description,
        string[] tags,
        string type,
        string stimulus,
        out string label)
    {
        label = "no counters";
        if (state == null)
            return 0f;

        state.EnsureCollections();
        string text = SafeLower(kind) + " " +
                      SafeLower(type) + " " +
                      SafeLower(name) + " " +
                      SafeLower(description) + " " +
                      SafeLower(stimulus) + " " +
                      (tags != null ? SafeLower(string.Join(" ", tags)) : string.Empty);

        float combat = SumCountersByPrefix(state, "combat:hit", "combat:miss", "kill:", "damage:taken");
        float magic = SumCountersByPrefix(state, "cast:projectile", "cast:pulse", "interact:shrine", "shrine:");
        float precision = SumCountersByPrefix(state, "lockpick:", "mimic:revealed", "loot:chest", "loot:enemy", "pickup:item", "item:equip");
        float social = SumCountersByPrefix(state, "dialogue:");
        float recovery = SumCountersByPrefix(state, "item:consume", "interact:shrine", "damage:taken");
        float movement = SumCountersByPrefix(state, "verb:move", "verb:movement", "verb:jump", "verb:dodge", "verb:sprint");

        bool wantsCombat = ContainsAny(text, "combat", "strike", "attack", "blade", "counter", "guard", "block", "parry", "kill", "defeat", "hostile", "threat", "damage");
        bool wantsMagic = ContainsAny(text, "spell", "mana", "pulse", "ward", "rune", "arcane", "fireball", "green pulse", "threshold");
        bool wantsPrecision = ContainsAny(text, "lock", "pick", "chest", "mimic", "loot", "careful", "hand", "reveal", "cache", "item", "equip");
        bool wantsSocial = ContainsAny(text, "dialogue", "talk", "speak", "intent", "read", "merchant", "social", "archivist", "mentor");
        bool wantsRecovery = ContainsAny(text, "restore", "shrine", "survive", "survival", "stamina", "health", "breath");
        bool wantsMovement = ContainsAny(text, "movement", "move", "dash", "dodge", "step", "reposition", "footing", "speed", "breakstep", "tide");
        bool wantsNature = ContainsAny(text, "nature", "forest", "jungle", "root", "thorn", "vine", "poison", "green", "auralith", "living terrain");

        float matched = 0f;
        int requirements = 0;
        if (wantsCombat)
        {
            matched += EvidenceCurve(combat);
            requirements++;
        }
        if (wantsMagic)
        {
            matched += EvidenceCurve(magic);
            requirements++;
        }
        if (wantsPrecision)
        {
            matched += EvidenceCurve(precision);
            requirements++;
        }
        if (wantsSocial)
        {
            matched += EvidenceCurve(social);
            requirements++;
        }
        if (wantsRecovery)
        {
            matched += EvidenceCurve(recovery);
            requirements++;
        }
        if (wantsMovement)
        {
            matched += EvidenceCurve(Mathf.Max(movement, precision * 0.35f, combat * 0.25f));
            requirements++;
        }
        if (wantsNature)
        {
            float natureEvidence = Mathf.Max(
                CurrentRegionMatches(state, "region_jungle_south") ? 1f : 0f,
                EvidenceCurve(Mathf.Max(combat, recovery, precision)) * 0.85f);
            matched += natureEvidence;
            requirements++;
        }

        float broadEvidence = EvidenceCurve(combat + magic + precision + social + recovery + movement);
        float result = requirements > 0 ? matched / requirements : broadEvidence;

        if (!string.IsNullOrWhiteSpace(stimulus) && ContainsAny(SafeLower(stimulus), "player", "you", "your", "after", "when", "while", "under", "repeated"))
            result = Mathf.Clamp01(result + 0.08f);

        if (requirements == 0 && string.Equals(kind, "quest", StringComparison.OrdinalIgnoreCase))
            result = Mathf.Clamp01(result + 0.06f);

        label = "combat " + combat.ToString("0.#") +
                ", magic " + magic.ToString("0.#") +
                ", precision " + precision.ToString("0.#") +
                ", social " + social.ToString("0.#") +
                ", recovery " + recovery.ToString("0.#") +
                ", movement " + movement.ToString("0.#");
        return Mathf.Clamp01(result);
    }

    private static float SumCountersByPrefix(PlayerState state, params string[] prefixes)
    {
        if (state == null || state.behaviorCounters == null || prefixes == null)
            return 0f;

        float total = 0f;
        foreach (KeyValuePair<string, float> pair in state.behaviorCounters)
        {
            string key = pair.Key ?? string.Empty;
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (!string.IsNullOrWhiteSpace(prefix) && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    total += Mathf.Max(0f, pair.Value);
                    break;
                }
            }
        }

        return total;
    }

    private static float EvidenceCurve(float count)
    {
        if (count <= 0f)
            return 0f;
        return Mathf.Clamp01(count / 3f);
    }

    private static bool CurrentRegionMatches(PlayerState state, string regionId)
    {
        return state != null &&
               !string.IsNullOrWhiteSpace(regionId) &&
               string.Equals(state.currentRegionId, regionId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(text) || needles == null)
            return false;

        for (int i = 0; i < needles.Length; i++)
        {
            string needle = needles[i];
            if (!string.IsNullOrWhiteSpace(needle) && text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private sealed class SituationSnapshot
    {
        public string combat = "UNKNOWN";
        public string regId = "region_unknown";
        public int incomingTargets;
        public int nearbyNotableCount;
        public int nearbyHostileCount;
        public HashSet<string> flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static SituationSnapshot Parse(string json)
        {
            SituationSnapshot snap = new SituationSnapshot();
            if (string.IsNullOrWhiteSpace(json))
                return snap;
            try
            {
                JObject j = JObject.Parse(json);
                snap.combat = (j.Value<string>("combat") ?? "UNKNOWN").Trim();
                snap.regId = (j.Value<string>("regId") ?? "region_unknown").Trim();
                snap.incomingTargets = SafeInt(j["inTarN"]);
                snap.nearbyNotableCount = CountArray(j["not"]);
                snap.nearbyHostileCount = CountArray(j["thrList"]);
                if (j["sf"] is JArray sf)
                {
                    for (int i = 0; i < sf.Count; i++)
                    {
                        string value = sf[i]?.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                            snap.flags.Add(value.Trim());
                    }
                }
            }
            catch { }
            return snap;
        }

        private static int CountArray(JToken token) => token is JArray arr ? arr.Count : 0;
        private static int SafeInt(JToken token)
        {
            if (token == null) return 0;
            if (token.Type == JTokenType.Integer) return token.Value<int>();
            if (token.Type == JTokenType.Float) return Mathf.RoundToInt(token.Value<float>());
            if (token.Type == JTokenType.String && int.TryParse(token.Value<string>(), out int parsed)) return parsed;
            return 0;
        }
    }
}
