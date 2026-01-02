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

    [Header("Debug")]
    public bool logPrompt = false;
    public bool logRawResponse = false;

    private float nextThinkTime;
    private float nextSkillTime;
    private float nextTitleTime;
    private float nextQuestTime;

    private int failStreak = 0;

    private void Awake()
    {
        if (applier == null)
            applier = FindFirstObjectByType<ProgressionDecisionApplier>();

        if (balance == null)
            Debug.LogWarning("[ProgressionThinkCycle] No ProgressionBalanceConfig assigned.");

        nextThinkTime = Time.time + (balance != null ? balance.thinkEverySeconds : 10f);
    }

    private void Update()
    {
        if (balance == null) return;
        if (Time.time < nextThinkTime) return;

        // ? If the model is already working/queued, don’t pile on.
        if (LLMClient.Instance != null && LLMClient.Instance.IsBusy)
        {
            nextThinkTime = Time.time + 0.5f;
            return;
        }

        nextThinkTime = Time.time + Mathf.Max(0.25f, balance.thinkEverySeconds);
        TryThink();
    }

    private void TryThink()
    {
        if (LLMClient.Instance == null) return;

        var acc = EventAccumulator.Instance;
        if (acc == null) return;

        var events = acc.GetEvents();
        if (events == null || events.Count == 0) return;

        int take = Mathf.Clamp(balance.maxRecentEvents, 1, 5000);
        var recent = TakeLast(events, take);

        var math = ProgressionMath.Compute(recent, balance, fallbackRegionId: "region_unknown");
        if (math.score < balance.minScoreToConsider)
            return;

        bool canSkill = Time.time >= nextSkillTime;
        bool canTitle = Time.time >= nextTitleTime;
        bool canQuest = Time.time >= nextQuestTime;

        if (!canSkill && !canTitle && !canQuest)
            return;

        string category = "none";
        if (canQuest && math.score >= balance.scoreForQuestCandidate) category = "quest";
        else if (canTitle && math.score >= balance.scoreForTitleCandidate) category = "title";
        else if (canSkill && math.score >= balance.scoreForSkillCandidate) category = "skill";
        else return;

        string situation = SafeSituationSnapshot();
        string summary = SafeSummarize(recent);
        string ledger = SafeLedger();

        string prompt = BuildPrompt(category, math, situation, summary, ledger);
        if (logPrompt) Debug.Log("[ProgressionThinkCycle PROMPT]\n" + prompt);

        // With the new queued LLMClient, this will never “reject”.
        LLMClient.Instance.GenerateSkill(prompt, raw =>
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                failStreak = Mathf.Clamp(failStreak + 1, 0, 8);
                float backoff = Mathf.Min(120f, balance.thinkEverySeconds * Mathf.Pow(2f, failStreak));
                nextThinkTime = Time.time + backoff;
                Debug.LogWarning($"[ProgressionThinkCycle] LLM failed (streak {failStreak}). Backing off {backoff:0.0}s.");
                return;
            }

            failStreak = 0;

            if (logRawResponse) Debug.Log("[ProgressionThinkCycle RAW]\n" + raw);

            if (applier == null)
            {
                Debug.LogWarning("[ProgressionThinkCycle] Missing ProgressionDecisionApplier.");
                return;
            }

            if (applier.TryApply(raw, out var appliedCategory, out var reason))
            {
                if (appliedCategory == "skill") nextSkillTime = Time.time + balance.skillCooldown;
                if (appliedCategory == "title") nextTitleTime = Time.time + balance.titleCooldown;
                if (appliedCategory == "quest") nextQuestTime = Time.time + balance.questCooldown;

                acc.ClearEvents();

                Debug.Log($"[ProgressionThinkCycle] Applied {appliedCategory}: {reason}");
            }
        });
    }

    private static List<ActionEvent> TakeLast(IReadOnlyList<ActionEvent> src, int n)
    {
        var outList = new List<ActionEvent>(n);
        int start = Mathf.Max(0, src.Count - n);
        for (int i = start; i < src.Count; i++)
            outList.Add(src[i]);
        return outList;
    }

    private string SafeSituationSnapshot()
    {
        try
        {
            var s = FindFirstObjectByType<SituationSnapshotBuilder>();
            if (s == null) return "<no SituationSnapshotBuilder>";
            return s.BuildSnapshot();
        }
        catch { return "<situation unavailable>"; }
    }

    private string SafeSummarize(List<ActionEvent> events)
    {
        try { return EventSummarizer.Summarize(events); }
        catch { return $"Observed {events.Count} actions."; }
    }

    private string SafeLedger()
    {
        try { return ActionRegistry.Instance?.BuildBehaviorSummary(12) ?? "<none>"; }
        catch { return "<ledger unavailable>"; }
    }

    private string BuildPrompt(
        string category,
        ProgressionMath.Result math,
        string situation,
        string summary,
        string ledger)
    {
        var sb = new StringBuilder(4096);

        sb.AppendLine("You are an impartial progression judge in a living RPG world.");
        sb.AppendLine("Your job: decide ONE earned reward (or decide NONE) based strictly on evidence.");
        sb.AppendLine();

        sb.AppendLine("HARD RULES (balance + earned feeling):");
        sb.AppendLine("- Rewards must be proportional to evidence. No god-gifts.");
        sb.AppendLine("- If behavior is spammy/repetitive, prefer SMALLER rewards or NONE.");
        sb.AppendLine("- Prefer region-appropriate flavor.");
        sb.AppendLine("- Output must be ONE JSON object ONLY. No markdown.");
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
        sb.AppendLine($"- dominant_region: {math.dominantRegionId}");
        sb.AppendLine($"- has_variety: {math.hasVariety}");
        sb.AppendLine($"- allowed_category_preference: {category}");
        sb.AppendLine();

        sb.AppendLine("OUTPUT SCHEMA (choose one):");
        sb.AppendLine("{");
        sb.AppendLine(@"  ""decision"": ""none"" | ""skill"" | ""title"" | ""quest"",");
        sb.AppendLine(@"  ""confidence"": 0.0-1.0,");
        sb.AppendLine(@"  ""reason"": ""short explanation grounded in evidence"",");
        sb.AppendLine(@"  ""payload"": {");
        sb.AppendLine(@"     // if skill:");
        sb.AppendLine(@"     // { ""skillSeedName"": ""string"", ""skillType"": ""combat|movement|utility|craft|social"", ""hook"": ""one sentence"" }");
        sb.AppendLine(@"     // if title:");
        sb.AppendLine(@"     // { ""titleName"": ""string"", ""titleDesc"": ""string"" }");
        sb.AppendLine(@"     // if quest:");
        sb.AppendLine(@"     // { ""questName"": ""string"", ""questDesc"": ""string"", ""tags"": [""...""] }");
        sb.AppendLine(@"  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("If the player did not truly earn it, return decision=none.");

        return sb.ToString();
    }
}
