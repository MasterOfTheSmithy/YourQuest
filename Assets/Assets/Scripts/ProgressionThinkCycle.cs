// Assets/Assets/Scripts/ProgressionThinkCycle.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ProgressionThinkCycle : MonoBehaviour
{
    [Header("Config")]
    public ProgressionBalanceConfig balance;

    [Header("Refs")]
    public ProgressionDecisionApplier applier;

    [Header("Live Limits")]
    public int maxPendingOffers = 2;
    public bool requireNoPendingOffersForNewQuest = true;

    [Header("Determinism")]
    // note: Live progression is authored by the local model; deterministic output remains an explicit emergency opt-in only.
    public bool preferDeterministicProgression = false;
    public bool allowLlmProgressionFallback = true;
    [Range(0f, 1f)] public float deterministicBaseConfidence = 0.86f;

    [Header("Debug")]
    public bool logPrompt = false;
    public bool logRawResponse = false;

    private float nextThinkTime;
    private float nextSkillTime;
    private float nextTitleTime;
    private float nextQuestTime;
    private float nextClassTime;
    private float nextItemTime;

    private int failStreak = 0;

    private void Awake()
    {
        ResolveReferences();

        if (balance == null)
            Debug.LogWarning("[ProgressionThinkCycle] No ProgressionBalanceConfig assigned.");

        nextThinkTime = Time.time + (balance != null ? balance.thinkEverySeconds : 10f);
    }

    private void Update()
    {
        ResolveReferences();
        if (balance == null)
            return;
        if (RuntimeModalUiBlocker.IsDialogueOpen)
        {
            nextThinkTime = Time.time + 0.2f;
            return;
        }
        if (Time.time < nextThinkTime)
            return;

        if (LLMClient.Instance != null)
        {
            if (LLMClient.Instance.HasPendingHighPriorityRequests)
            {
                nextThinkTime = Time.time + 0.12f;
                return;
            }

            if (LLMClient.Instance.IsBusy)
            {
                nextThinkTime = Time.time + 0.5f;
                return;
            }
        }

        nextThinkTime = Time.time + Mathf.Max(0.25f, balance.thinkEverySeconds);
        TryThink();
    }

    private void TryThink()
    {
        ResolveReferences();
        if (RuntimeModalUiBlocker.IsDialogueOpen)
        {
            nextThinkTime = Time.time + 0.2f;
            return;
        }

        if (allowLlmProgressionFallback && LLMClient.Instance != null && LLMClient.Instance.HasPendingHighPriorityRequests)
        {
            nextThinkTime = Time.time + 0.12f;
            return;
        }

        EventAccumulator acc = EventAccumulator.Instance;
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (acc == null || psm == null || psm.state == null)
            return;

        if (psm.state.GetPendingOfferCount() >= maxPendingOffers)
            return;

        IReadOnlyList<ActionEvent> events = acc.GetEvents();
        if (events == null || events.Count == 0)
            return;

        int take = Mathf.Clamp(balance.maxRecentEvents, 1, 5000);
        List<ActionEvent> recent = TakeLast(events, take);
        ProgressionMath.Result math = ProgressionMath.Compute(recent, balance, fallbackRegionId: "region_unknown");
        if (math.score < Mathf.Max(balance.minScoreToConsider, 12f))
            return;

        string preferred = DeterminePreferredCategory(psm.state, math);
        if (string.IsNullOrWhiteSpace(preferred))
            return;

        string situation = SafeSituationSnapshot();
        string summary = SafeSummarize(recent);
        string ledger = SafeLedger();

        if (preferDeterministicProgression && TryBuildDeterministicDecision(preferred, math, psm.state, out string deterministicJson))
        {
            if (applier == null)
            {
                Debug.LogWarning("[ProgressionThinkCycle] Missing ProgressionDecisionApplier.");
                return;
            }

            if (applier.TryApply(deterministicJson, out string appliedCategory, out string reason))
            {
                ApplyCooldown(appliedCategory);
                acc.ClearEvents();
                Debug.Log("[ProgressionThinkCycle] Applied deterministic " + appliedCategory + ": " + reason);
                return;
            }

            if (!allowLlmProgressionFallback)
            {
                Debug.Log("[ProgressionThinkCycle] Deterministic progression ignored: " + reason);
                return;
            }
        }

        if (!allowLlmProgressionFallback || LLMClient.Instance == null)
            return;

        string prompt = BuildPrompt(preferred, math, situation, summary, ledger, psm.state);
        if (logPrompt)
            Debug.Log("[ProgressionThinkCycle PROMPT]\n" + prompt);

        // note: Progression decisions are persisted gameplay contracts, never free-form model suggestions.
        LLMClient.Instance.Submit(new YQLlmRequest
        {
            prompt = prompt,
            debugTag = "ProgressionDecision",
            category = LLMGenerationCategory.StructuredState,
            priority = YQLlmRequestPriority.Background,
            requireJson = true,
            // note: One compact decision is sufficient; progression backs off deterministically instead of retrying a slow local request.
            maxRetries = 0,
            optionsOverride = new Dictionary<string, object>
            {
                { "num_predict", 420 },
                { "request_timeout_seconds", 40 }
            }
        }, result =>
        {
            // note: The decision applier runs only on a successful normalized JSON object.
            string raw = result.success ? result.text : null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                failStreak = Mathf.Clamp(failStreak + 1, 0, 8);
                float backoff = Mathf.Min(120f, balance.thinkEverySeconds * Mathf.Pow(2f, failStreak));
                nextThinkTime = Time.time + backoff;
                Debug.LogWarning($"[ProgressionThinkCycle] LLM failed (streak {failStreak}). Backing off {backoff:0.0}s.");
                return;
            }

            failStreak = 0;

            if (logRawResponse)
                Debug.Log("[ProgressionThinkCycle RAW]\n" + raw);

            if (applier == null)
            {
                Debug.LogWarning("[ProgressionThinkCycle] Missing ProgressionDecisionApplier.");
                return;
            }

            if (applier.TryApply(raw, out string appliedCategory, out string reason))
            {
                ApplyCooldown(appliedCategory);
                acc.ClearEvents();
                Debug.Log($"[ProgressionThinkCycle] Applied {appliedCategory}: {reason}");
            }
        });
    }

    private string DeterminePreferredCategory(PlayerState state, ProgressionMath.Result math)
    {
        float skillThreshold = Mathf.Max(balance.scoreForSkillCandidate, 24f);
        float titleThreshold = Mathf.Max(balance.scoreForTitleCandidate, 34f);
        float questThreshold = Mathf.Max(balance.scoreForQuestCandidate, 38f);
        float classThreshold = Mathf.Max(balance.scoreForTitleCandidate, 42f);
        float itemThreshold = Mathf.Max(balance.scoreForTitleCandidate, 34f);

        bool canSkill = Time.time >= nextSkillTime && math.score >= skillThreshold;
        bool canTitle = Time.time >= nextTitleTime && math.score >= titleThreshold;
        bool canQuest = Time.time >= nextQuestTime && math.score >= questThreshold;
        bool canClass = Time.time >= nextClassTime && math.score >= classThreshold && math.hasVariety;
        bool canItem = Time.time >= nextItemTime && math.score >= itemThreshold;

        if (requireNoPendingOffersForNewQuest && state.GetPendingOfferCount() > 0)
            canQuest = false;

        bool hasClass = state.classes != null && state.classes.Count > 0;
        bool manySkills = state.skills != null && state.skills.Count >= 3;

        if (!hasClass && canClass)
            return "class";
        if (canQuest)
            return "quest";
        if (canItem && IsItemWorthyPattern(math.dominantVerb))
            return "item";
        if (canTitle && manySkills)
            return "title";
        if (canSkill && math.dominantVerb == "combat")
            return "skill";
        if (canSkill && math.dominantVerb == "interact")
            return "skill";
        if (canSkill)
            return "skill";
        if (canTitle)
            return "title";
        if (canClass)
            return "class";
        return null;
    }

    private void ApplyCooldown(string appliedCategory)
    {
        switch ((appliedCategory ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "skill":
            case "spell":
                nextSkillTime = Time.time + Mathf.Max(balance.skillCooldown, 420f);
                break;
            case "title":
                nextTitleTime = Time.time + Mathf.Max(balance.titleCooldown, 900f);
                break;
            case "class":
                nextClassTime = Time.time + Mathf.Max(balance.titleCooldown, 900f);
                break;
            case "quest":
                nextQuestTime = Time.time + Mathf.Max(balance.questCooldown, 720f);
                break;
            case "item":
                // note: Equipment offers are deliberately sparse so generated upgrades remain notable rather than loot spam.
                nextItemTime = Time.time + Mathf.Max(balance.titleCooldown, 900f);
                break;
        }
    }

    private static bool IsItemWorthyPattern(string dominantVerb)
    {
        string verb = (dominantVerb ?? string.Empty).Trim().ToLowerInvariant();
        return verb == "combat" || verb == "attack" || verb == "kill" ||
               verb == "loot" || verb == "open" || verb == "interact" ||
               verb == "craft" || verb == "dodge" || verb == "move";
    }

    private bool TryBuildDeterministicDecision(string preferredCategory, ProgressionMath.Result math, PlayerState state, out string rawJson)
    {
        rawJson = string.Empty;
        if (state == null || string.IsNullOrWhiteSpace(preferredCategory))
            return false;

        string category = preferredCategory.Trim().ToLowerInvariant();
        string stimulus = BuildDeterministicStimulus(math, state);
        string evidenceText = BuildEvidenceText(math, state);
        bool nature = LooksLikeNatureContext(evidenceText);
        bool spell = category == "spell" || (category == "skill" && ShouldPreferSpell(evidenceText, math));
        float confidence = Mathf.Clamp01(deterministicBaseConfidence + Mathf.Clamp((math.score - balance.minScoreToConsider) * 0.002f, 0f, 0.08f));

        switch (category)
        {
            case "skill":
                rawJson = spell
                    ? BuildSkillDecisionJson("spell", nature ? "Auralith's Green Pulse" : "Threshold Pulse", "spell", stimulus,
                        nature
                            ? "When triggered by " + stimulus + ", it releases a readable green mana pulse that briefly controls space and gives the player a recovery window."
                            : "When triggered by " + stimulus + ", it releases a readable mana pulse that creates a short control and recovery window.",
                        nature ? YQGeneratedContentCuration.NaturePrecursorName : string.Empty,
                        confidence)
                    : BuildSkillDecisionJson("skill", BuildDeterministicSkillName(math, nature), BuildDeterministicSkillType(math, nature), stimulus,
                        BuildDeterministicSkillHook(math, nature, stimulus),
                        nature ? YQGeneratedContentCuration.NaturePrecursorName : string.Empty,
                        confidence);
                return true;

            case "title":
                rawJson = BuildSimpleDecisionJson("title", "The " + BuildDeterministicTitleName(math, nature), stimulus,
                    "A title earned because " + stimulus + " kept repeating under pressure. It matters by marking a pattern future offers can recognize.",
                    confidence);
                return true;

            case "class":
                rawJson = BuildSimpleDecisionJson("class", BuildDeterministicClassName(math, nature), stimulus,
                    "A class identity shaped by " + stimulus + ". It matters by steering future offers toward the choices the player repeatedly proves.",
                    confidence);
                return true;

            case "quest":
                rawJson = BuildQuestDecisionJson("Prove the Pattern", stimulus,
                    "Objective: take one concrete risk that repeats " + stimulus + ", survive the result, and return with proof. The stakes are whether this pattern becomes part of the player's identity or stays noise.",
                    confidence,
                    nature);
                return true;
        }

        return false;
    }

    private string BuildDeterministicStimulus(ProgressionMath.Result math, PlayerState state)
    {
        string verb = string.IsNullOrWhiteSpace(math.dominantVerb) ? "action" : math.dominantVerb.Trim().ToLowerInvariant();
        int count = Mathf.Max(1, math.dominantVerbCount);

        switch (verb)
        {
            case "combat":
            case "attack":
            case "kill":
                return "your repeated close-range pressure across " + count + " recent combat signals";
            case "interact":
            case "loot":
            case "open":
                return "your repeated habit of testing objects before committing";
            case "move":
            case "movement":
            case "jump":
            case "dodge":
                return "your repeated movement choices under pressure";
            case "dialogue":
            case "talk":
                return "your repeated habit of reading intent before committing";
            case "shrine":
            case "magic":
            case "cast":
                return "your repeated instinct to answer pressure with shaped mana";
            default:
                return "your repeated " + verb + " pattern across " + count + " recent signals";
        }
    }

    private static string BuildEvidenceText(ProgressionMath.Result math, PlayerState state)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(' ').Append(math.dominantVerb).Append(' ').Append(math.dominantRegionId);
        if (state != null)
        {
            state.EnsureCollections();
            if (state.identityKeywords != null)
            {
                for (int i = 0; i < state.identityKeywords.Count; i++)
                    sb.Append(' ').Append(state.identityKeywords[i]);
            }
            if (state.behaviorLedger != null)
            {
                int start = Mathf.Max(0, state.behaviorLedger.Count - 8);
                for (int i = start; i < state.behaviorLedger.Count; i++)
                    sb.Append(' ').Append(state.behaviorLedger[i]);
            }
        }
        return sb.ToString().ToLowerInvariant();
    }

    private static bool LooksLikeNatureContext(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string lower = text.ToLowerInvariant();
        return lower.Contains("jungle") ||
               lower.Contains("forest") ||
               lower.Contains("wild") ||
               lower.Contains("root") ||
               lower.Contains("thorn") ||
               lower.Contains("vine") ||
               lower.Contains("poison") ||
               lower.Contains("green") ||
               lower.Contains("auralith");
    }

    private static bool ShouldPreferSpell(string evidenceText, ProgressionMath.Result math)
    {
        string text = evidenceText ?? string.Empty;
        string verb = math.dominantVerb ?? string.Empty;
        return text.Contains("magic") ||
               text.Contains("spell") ||
               text.Contains("mana") ||
               text.Contains("shrine") ||
               verb.IndexOf("cast", StringComparison.OrdinalIgnoreCase) >= 0 ||
               verb.IndexOf("magic", StringComparison.OrdinalIgnoreCase) >= 0 ||
               verb.IndexOf("shrine", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string BuildDeterministicSkillName(ProgressionMath.Result math, bool nature)
    {
        if (nature)
            return "Rootguard Counter";

        string verb = (math.dominantVerb ?? string.Empty).Trim().ToLowerInvariant();
        switch (verb)
        {
            case "interact":
            case "loot":
            case "open":
                return "Careful Hand";
            case "move":
            case "movement":
            case "jump":
            case "dodge":
                return "Breakstep Recovery";
            case "dialogue":
            case "talk":
                return "Keenhand Read";
            default:
                return "Linebreaker Counter";
        }
    }

    private static string BuildDeterministicSkillType(ProgressionMath.Result math, bool nature)
    {
        if (nature)
            return "utility";

        string verb = (math.dominantVerb ?? string.Empty).Trim().ToLowerInvariant();
        switch (verb)
        {
            case "move":
            case "movement":
            case "jump":
            case "dodge":
                return "movement";
            case "interact":
            case "loot":
            case "open":
            case "dialogue":
            case "talk":
                return "utility";
            default:
                return "combat";
        }
    }

    private static string BuildDeterministicSkillHook(ProgressionMath.Result math, bool nature, string stimulus)
    {
        if (nature)
            return "When triggered by " + stimulus + ", it turns living-terrain pressure into a brief rootguard that improves the next guard, counter, or recovery.";

        string verb = (math.dominantVerb ?? string.Empty).Trim().ToLowerInvariant();
        switch (verb)
        {
            case "interact":
            case "loot":
            case "open":
                return "When triggered by " + stimulus + ", it reveals risk before the next chest, door, or shrine interaction.";
            case "move":
            case "movement":
            case "jump":
            case "dodge":
                return "When triggered by " + stimulus + ", it improves recovery timing after a committed dodge or reposition.";
            case "dialogue":
            case "talk":
                return "When triggered by " + stimulus + ", it improves the next read on intent, risk, or reward.";
            default:
                return "When triggered by " + stimulus + ", it turns the next committed action into a clearer strike, guard, or recovery.";
        }
    }

    private static string BuildDeterministicTitleName(ProgressionMath.Result math, bool nature)
    {
        if (nature)
            return "Green-Witnessed";

        string verb = (math.dominantVerb ?? string.Empty).Trim().ToLowerInvariant();
        switch (verb)
        {
            case "interact":
            case "loot":
            case "open":
                return "Careful Hand";
            case "move":
            case "movement":
            case "jump":
            case "dodge":
                return "Breakstep";
            case "dialogue":
            case "talk":
                return "Keen-Eared";
            default:
                return "Unbroken Pattern";
        }
    }

    private static string BuildDeterministicClassName(ProgressionMath.Result math, bool nature)
    {
        if (nature)
            return "Greenhand Warden";

        string verb = (math.dominantVerb ?? string.Empty).Trim().ToLowerInvariant();
        switch (verb)
        {
            case "interact":
            case "loot":
            case "open":
                return "Careful Hand";
            case "move":
            case "movement":
            case "jump":
            case "dodge":
                return "Breakstep Harrier";
            case "dialogue":
            case "talk":
                return "Keenhand Pilgrim";
            default:
                return "Linebreaker Vanguard";
        }
    }

    private string BuildSkillDecisionJson(string decision, string name, string type, string stimulus, string hook, string loreAnchor, float confidence)
    {
        return "{\"decision\":\"" + EscapeJson(decision) + "\",\"confidence\":" + FormatConfidence(confidence) +
               ",\"reason\":\"Deterministic progression from earned player stimulus.\",\"payload\":{\"skillSeedName\":\"" +
               EscapeJson(name) + "\",\"skillType\":\"" + EscapeJson(type) + "\",\"stimulus\":\"" + EscapeJson(stimulus) +
               "\",\"hook\":\"" + EscapeJson(hook) + "\",\"loreAnchor\":\"" + EscapeJson(loreAnchor) + "\"}}";
    }

    private string BuildSimpleDecisionJson(string decision, string name, string stimulus, string description, float confidence)
    {
        string nameKey = decision == "title" ? "titleName" : "className";
        return "{\"decision\":\"" + EscapeJson(decision) + "\",\"confidence\":" + FormatConfidence(confidence) +
               ",\"reason\":\"Deterministic progression from earned player stimulus.\",\"payload\":{\"" + nameKey + "\":\"" +
               EscapeJson(name) + "\",\"stimulus\":\"" + EscapeJson(stimulus) + "\",\"description\":\"" + EscapeJson(description) + "\"}}";
    }

    private string BuildQuestDecisionJson(string name, string stimulus, string description, float confidence, bool nature)
    {
        string natureTag = nature ? ",\"nature_precursor\",\"auralith\"" : string.Empty;
        return "{\"decision\":\"quest\",\"confidence\":" + FormatConfidence(confidence) +
               ",\"reason\":\"Deterministic progression from earned player stimulus.\",\"payload\":{\"questName\":\"" +
               EscapeJson(name) + "\",\"stimulus\":\"" + EscapeJson(stimulus) + "\",\"description\":\"" +
               EscapeJson(description) + "\",\"tags\":[\"player_response\",\"earned\",\"deterministic\",\"meaningful\"" + natureTag + "]}}";
    }

    private static string FormatConfidence(float confidence)
    {
        return Mathf.Clamp01(confidence).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static List<ActionEvent> TakeLast(IReadOnlyList<ActionEvent> src, int n)
    {
        List<ActionEvent> outList = new List<ActionEvent>(n);
        int start = Mathf.Max(0, src.Count - n);
        for (int i = start; i < src.Count; i++)
            outList.Add(src[i]);
        return outList;
    }

    private string SafeSituationSnapshot()
    {
        try
        {
            SituationSnapshotBuilder s = FindFirstObjectByType<SituationSnapshotBuilder>();
            if (s == null)
                return "<no SituationSnapshotBuilder>";
            return s.BuildSnapshot();
        }
        catch
        {
            return "<situation unavailable>";
        }
    }

    private string SafeSummarize(List<ActionEvent> events)
    {
        try
        {
            return EventSummarizer.Summarize(events);
        }
        catch
        {
            return $"Observed {events.Count} actions.";
        }
    }

    private string SafeLedger()
    {
        try
        {
            return ActionRegistry.Instance?.BuildBehaviorSummary(12) ?? "<none>";
        }
        catch
        {
            return "<ledger unavailable>";
        }
    }

    private string BuildPrompt(string preferredCategory, ProgressionMath.Result math, string situation, string summary, string ledger, PlayerState state)
    {
        StringBuilder sb = new StringBuilder(6144);

        sb.AppendLine("You are an impartial progression judge in a living RPG world.");
        sb.AppendLine("Your job: propose ONE earned, player-facing progression offer, or decide NONE.");
        sb.AppendLine("Do not auto-apply rewards. The player is reviewing offers manually.");
        sb.AppendLine();

        sb.AppendLine("HARD RULES:");
        sb.AppendLine("- Rewards must be proportional to evidence. No god-gifts.");
        sb.AppendLine("- Prefer offers, upgrades, and sidegrades over automatic grants.");
        sb.AppendLine("- If behavior is spammy/repetitive, prefer SMALLER rewards or NONE.");
        sb.AppendLine("- If something resembles an existing skill/title/class/quest, prefer an upgrade or refined variant.");
        sb.AppendLine("- Names must be specific in-world phrases. Never use placeholder names like Movement Step, Generated Skill, New Quest, or Unknown.");
        sb.AppendLine("- The reward must respond to the player's stimulus directly: what they did, endured, repeated, risked, protected, avoided, or improvised.");
        sb.AppendLine("- The dominant region is context only. Do not name skills, spells, classes, or titles after region ids or region names.");
        sb.AppendLine("- Do not write region-flavor fluff. A region can pressure the player, but the offer belongs to the player.");
        sb.AppendLine("- Do not reward raw input verbs by themselves. Repeated movement, jumping, attacking, or looting is evidence for practice or an upgrade, not a new named skill unless the pattern is distinctive.");
        sb.AppendLine("- Skill and spell names must imply a clear form, element, weapon, discipline, or profession. Invent a fresh name from this evidence; do not echo schema labels or prompt language.");
        sb.AppendLine("- For jungle, forest, poison, root, thorn, or living-terrain evidence: make the skill about the player's survival response. If lore helps, anchor it to Auralith, the First Green, an old natural precursor god. Do not name it after Verdant Maw or any region.");
        sb.AppendLine("- The hook must say what player stimulus caused the offer. Avoid phrases like generated from recent behavior, based on region, or current run seed.");
        sb.AppendLine("- Output must be ONE JSON object ONLY. No markdown.");
        sb.AppendLine();

        sb.AppendLine("CURRENT_PLAYER_STATE:");
        sb.AppendLine($"- level: {state.level}");
        sb.AppendLine($"- pending_offers: {state.GetPendingOfferCount()}");
        sb.AppendLine($"- known_classes: {CountSafe(state.classes)}");
        sb.AppendLine($"- known_titles: {CountSafe(state.titles)}");
        sb.AppendLine($"- known_skills: {CountSafe(state.skills)}");
        sb.AppendLine($"- active_quests: {CountSafe(state.quests)}");
        sb.AppendLine("- known_skill_names: " + JoinSkillNames(state.skills, 10));
        sb.AppendLine("- known_quest_names: " + JoinQuestNames(state.quests, 8));
        sb.AppendLine("- identity_keywords: " + JoinIdentityKeywords(state.identityKeywords, 12));
        sb.AppendLine();

        sb.AppendLine("EVIDENCE:");
        sb.AppendLine("SITUATION_SNAPSHOT (ground truth):");
        sb.AppendLine(situation);
        sb.AppendLine();
        sb.AppendLine("RECENT_ACTIONS_SUMMARY:");
        sb.AppendLine(summary);
        sb.AppendLine();
        sb.AppendLine("BEHAVIOR_LEDGER (longer-term aggregates):");
        sb.AppendLine(ledger);
        sb.AppendLine();

        sb.AppendLine("MATH (do not override):");
        sb.AppendLine($"- earned_score: {math.score:0.00}");
        sb.AppendLine($"- dominant_verb: {math.dominantVerb} (count {math.dominantVerbCount})");
        sb.AppendLine($"- dominant_region_context_only: {math.dominantRegionId}");
        sb.AppendLine($"- has_variety: {math.hasVariety}");
        sb.AppendLine($"- preferred_category: {preferredCategory}");
        sb.AppendLine();

        sb.AppendLine("OUTPUT SCHEMA:");
        sb.AppendLine("{");
        sb.AppendLine("  \"decision\": \"none\" | \"skill\" | \"spell\" | \"title\" | \"class\" | \"quest\" | \"item\",");
        sb.AppendLine("  \"confidence\": 0.0-1.0,");
        sb.AppendLine("  \"reason\": \"short explanation grounded in evidence\",");
        sb.AppendLine("  \"payload\": {");
        sb.AppendLine("     // if skill or spell:");
        sb.AppendLine("     // { \"skillSeedName\": \"string\", \"skillType\": \"combat|movement|utility|craft|social|spell\", \"stimulus\": \"what the player did\", \"hook\": \"one sentence\", \"loreAnchor\": \"optional\" }");
        sb.AppendLine("     // if title:");
        sb.AppendLine("     // { \"titleName\": \"string\", \"stimulus\": \"what the player did\", \"description\": \"string\" }");
        sb.AppendLine("     // if class:");
        sb.AppendLine("     // { \"className\": \"string\", \"stimulus\": \"what the player did\", \"description\": \"string\" }");
        sb.AppendLine("     // if quest:");
        sb.AppendLine("     // { \"questName\": \"string\", \"stimulus\": \"what the player did\", \"description\": \"string\", \"tags\": [\"player_response\", \"...\"], \"objective\": { \"type\": \"equip_item|talk_to_npc|cast_spell|defeat_enemy|loot_item|pickup_item|open_lock|mimic_reveal|use_shrine|enter_region|wait_seconds\", \"targetId\": \"stable ID or empty\", \"counterKey\": \"exact counter or empty\", \"counterPrefix\": \"stable counter prefix or empty\", \"requiredCount\": 1, \"description\": \"concrete instruction\" } }");
        sb.AppendLine("     // if item:");
        sb.AppendLine("     // { \"itemName\": \"string\", \"itemType\": \"weapon|offhand|head|chest|gloves|legs|boots|belt|cloak|ring|earring|necklace|trinket|consumable\", \"stimulus\": \"what the player did\", \"description\": \"string\", \"tags\": [\"player_response\", \"...\"] }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Return decision=none if nothing is clearly earned.");

        return sb.ToString();
    }

    private static int CountSafe<T>(List<T> list)
    {
        if (list == null)
            return 0;
        int count = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                count++;
        }
        return count;
    }

    private static string JoinSkillNames(List<SkillRecord> skills, int max)
    {
        if (skills == null || skills.Count == 0)
            return "<none>";

        List<string> names = new List<string>(Mathf.Clamp(max, 1, 24));
        for (int i = 0; i < skills.Count && names.Count < max; i++)
        {
            SkillRecord skill = skills[i];
            if (skill != null && !string.IsNullOrWhiteSpace(skill.name))
                names.Add(skill.name.Trim());
        }

        return names.Count == 0 ? "<none>" : string.Join(", ", names);
    }

    private static string JoinQuestNames(List<QuestRecord> quests, int max)
    {
        if (quests == null || quests.Count == 0)
            return "<none>";

        List<string> names = new List<string>(Mathf.Clamp(max, 1, 24));
        for (int i = 0; i < quests.Count && names.Count < max; i++)
        {
            QuestRecord quest = quests[i];
            if (quest != null && !string.IsNullOrWhiteSpace(quest.name))
                names.Add(quest.name.Trim());
        }

        return names.Count == 0 ? "<none>" : string.Join(", ", names);
    }

    private static string JoinIdentityKeywords(List<string> keywords, int max)
    {
        if (keywords == null || keywords.Count == 0)
            return "<none>";

        List<string> values = new List<string>(Mathf.Clamp(max, 1, 24));
        for (int i = 0; i < keywords.Count && values.Count < max; i++)
        {
            string keyword = keywords[i];
            if (!string.IsNullOrWhiteSpace(keyword))
                values.Add(keyword.Trim());
        }

        return values.Count == 0 ? "<none>" : string.Join(", ", values);
    }

    private void ResolveReferences()
    {
        if (applier == null)
            applier = FindFirstObjectByType<ProgressionDecisionApplier>();

        if (balance == null)
            balance = Resources.Load<ProgressionBalanceConfig>("ProgressionBalanceConfig");

        if (balance == null)
        {
            // note: Auto-bootstrapped scenes need a runtime-safe config even when the asset is not under Resources.
            ProgressionBalanceConfig fallback = ScriptableObject.CreateInstance<ProgressionBalanceConfig>();
            fallback.thinkEverySeconds = 14f;
            fallback.maxRecentEvents = 160;
            fallback.minScoreToConsider = 12f;
            fallback.scoreForSkillCandidate = 24f;
            fallback.scoreForTitleCandidate = 34f;
            fallback.scoreForQuestCandidate = 38f;
            fallback.skillCooldown = 420f;
            fallback.titleCooldown = 900f;
            fallback.questCooldown = 720f;
            balance = fallback;
        }
    }
}
