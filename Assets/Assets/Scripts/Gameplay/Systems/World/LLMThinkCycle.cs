// LLMThinkCycle.cs
using System.Text;
using UnityEngine;

public class LLMThinkCycle : MonoBehaviour
{
    [Header("References")]
    public WorldDeltaApplier worldDeltaApplier;

    [Header("Situation (Highly Recommended)")]
    public SituationSnapshotBuilder situationSnapshotBuilder;

    [Header("Timing")]
    public float thinkEverySeconds = 12f;
    public float minTotalSignificance = 4f;

    [Header("Safety")]
    [Range(0f, 1f)]
    public float minConfidenceToApply = 0.25f;

    [Header("Prompt Budgeting")]
    public int maxSituationChars = 2600;
    public int maxSummaryChars = 900;
    public int maxLedgerChars = 900;

    [Header("Debug")]
    public bool logPrompt = false;
    public bool logRawResponse = true;
    public bool clearEventsAfterApply = true;

    private float nextThinkTime;

    // Prevent retry spirals
    private bool inRepair = false;

    private void Awake()
    {
        if (worldDeltaApplier == null)
            worldDeltaApplier = FindFirstObjectByType<WorldDeltaApplier>();

        if (worldDeltaApplier != null)
            worldDeltaApplier.minConfidence = minConfidenceToApply;

        if (situationSnapshotBuilder == null)
            situationSnapshotBuilder = FindFirstObjectByType<SituationSnapshotBuilder>();

        nextThinkTime = Time.time + thinkEverySeconds;
    }

    private void Update()
    {
        if (Time.time < nextThinkTime) return;
        nextThinkTime = Time.time + thinkEverySeconds;
        TryThink();
    }

    private void TryThink()
    {
        if (inRepair) return; // don't overlap repair with new think cycles

        if (LLMClient.Instance == null || worldDeltaApplier == null)
            return;

        var acc = EventAccumulator.Instance;
        if (acc == null) return;

        var events = acc.GetEvents();
        if (events == null || events.Count == 0) return;

        float sig = 0f;
        for (int i = 0; i < events.Count; i++)
            sig += Mathf.Max(0f, events[i].Significance);

        if (sig < minTotalSignificance) return;

        // Blocks
        string situation = Truncate(SafeSituationSnapshot(), maxSituationChars);
        string summary = Truncate(SafeSummarize(events), maxSummaryChars);
        string ledger = Truncate(SafeLedger(), maxLedgerChars);

        string combinedRecent = BuildCombinedRecentBlock(situation, summary);

        string task = BuildInstruction(sig, !string.IsNullOrWhiteSpace(situation));
        string schema = PromptContextBuilder.WrapJsonSchema(WorldDeltaSchemaWithExamples());

        string prompt = PromptContextBuilder.BuildContext(
            taskInstruction: task,
            outputSchemaBlock: schema,
            recentSummary: combinedRecent,
            behaviorLedger: ledger
        );

        if (logPrompt)
            Debug.Log(prompt);

        LLMClient.Instance.GenerateSkill(prompt, raw =>
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            if (logRawResponse)
                Debug.Log("[LLMThinkCycle] RAW RESPONSE:\n" + raw);

            if (worldDeltaApplier.TryApply(raw, out var err))
            {
                Debug.Log("[WORLD SHIFT] World updated.");
                if (clearEventsAfterApply)
                    TryClearEvents(acc);
                return;
            }

            Debug.LogWarning("[LLMThinkCycle] Delta failed: " + err);

            // If applier says no-op, do not spam repair; just wait for better evidence
            if (!string.IsNullOrEmpty(err) && err.ToLowerInvariant().Contains("no-op"))
                return;

            // One repair attempt max
            inRepair = true;
            RetryOnce(schema, raw, err, acc);
        });
    }

    private void RetryOnce(string schema, string raw, string error, EventAccumulator acc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Return ONLY a single valid JSON object. No markdown. No headings. No explanation.");
        sb.AppendLine("You MUST match the schema exactly.");
        sb.AppendLine();
        sb.AppendLine($"CONFIDENCE RULE: confidence must be a number 0.0 to 1.0 (NOT percent). Must be >= {minConfidenceToApply:0.00}");
        sb.AppendLine();
        sb.AppendLine("ARRAY RULE: flags/factions/locations MUST be arrays of OBJECTS (not strings).");
        sb.AppendLine("If you have no changes for a category, use an empty array [].");
        sb.AppendLine();
        sb.AppendLine("Correct EXAMPLES:");
        sb.AppendLine(@"""flags"": [ { ""key"": ""tension"", ""op"": ""inc"", ""value"": 1 } ]");
        sb.AppendLine(@"""factions"": [ { ""factionId"": ""the_archives"", ""name"": ""The Archives"", ""op"": ""attitude_inc"", ""value"": -0.05, ""text"": ""Rumors spread in the stacks."" } ]");
        sb.AppendLine(@"""locations"": [ { ""locationId"": ""library_hall"", ""name"": ""Library Hall"", ""regionId"": ""region_library"", ""op"": ""state_set"", ""value"": ""alert"", ""text"": ""Footsteps hush; lanterns turn."" } ]");
        sb.AppendLine();
        sb.AppendLine("Schema:");
        sb.AppendLine(schema);
        sb.AppendLine();
        sb.AppendLine("Parse error to fix:");
        sb.AppendLine(error);
        sb.AppendLine();
        sb.AppendLine("Bad previous output (for reference only):");
        sb.AppendLine(raw);

        LLMClient.Instance.GenerateSkill(sb.ToString(), repaired =>
        {
            if (logRawResponse)
                Debug.Log("[LLMThinkCycle] REPAIRED:\n" + repaired);

            inRepair = false;

            if (worldDeltaApplier.TryApply(repaired, out var err2))
            {
                Debug.Log("[WORLD SHIFT] World updated (repaired).");
                if (clearEventsAfterApply)
                    TryClearEvents(acc);
                return;
            }

            Debug.LogWarning("[LLMThinkCycle] Repair failed: " + err2);
        });
    }

    private string BuildCombinedRecentBlock(string situation, string summary)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(situation))
        {
            sb.AppendLine("SITUATION_SNAPSHOT (primary evidence; treat as ground truth):");
            sb.AppendLine(situation);
            sb.AppendLine();
        }

        sb.AppendLine("RECENT_ACTIONS_SUMMARY:");
        sb.AppendLine(string.IsNullOrWhiteSpace(summary) ? "<none>" : summary);

        return sb.ToString();
    }

    private string SafeSituationSnapshot()
    {
        try
        {
            if (situationSnapshotBuilder == null)
                return "<no SituationSnapshotBuilder>";
            return situationSnapshotBuilder.BuildSnapshot();
        }
        catch
        {
            return "<situation unavailable>";
        }
    }

    private string SafeSummarize(System.Collections.Generic.IReadOnlyList<ActionEvent> events)
    {
        try
        {
            return EventSummarizer.Summarize(new System.Collections.Generic.List<ActionEvent>(events));
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

    private string BuildInstruction(float sig, bool situationPresent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Create a WorldDeltaDTO responding to player behavior.");
        sb.AppendLine($"Total significance: {sig:0.00}");
        sb.AppendLine();

        if (situationPresent)
        {
            sb.AppendLine("Critical relevance rules:");
            sb.AppendLine("- Use SITUATION_SNAPSHOT as PRIMARY evidence.");
            sb.AppendLine("- Prefer region-appropriate changes over generic flags.");
            sb.AppendLine("- Keep deltas small and concrete.");
            sb.AppendLine();
        }

        sb.AppendLine("Rules:");
        sb.AppendLine("- confidence must be 0.0..1.0");
        sb.AppendLine("- flags items: { key, op(set|inc|dec), value(number) }");
        sb.AppendLine("- factions items: { factionId, name, op(attitude_set|attitude_inc|status_set), value(number), text(string) }");
        sb.AppendLine("- locations items: { locationId, name, regionId, op(state_set|importance_set|importance_inc), value(string or number depending on op), text(string) }");
        sb.AppendLine("- No extra top-level keys.");
        sb.AppendLine("- Return JSON only.");
        return sb.ToString();
    }

    private string WorldDeltaSchemaWithExamples()
    {
        return @"
{
  ""rationale"": ""string"",
  ""confidence"": 0.0,

  ""flags"": [
    { ""key"": ""tension"", ""op"": ""inc"", ""value"": 1 }
  ],

  ""factions"": [
    { ""factionId"": ""the_archives"", ""name"": ""The Archives"", ""op"": ""attitude_inc"", ""value"": -0.05, ""text"": ""Rumors spread in the stacks."" }
  ],

  ""locations"": [
    { ""locationId"": ""library_hall"", ""name"": ""Library Hall"", ""regionId"": ""region_library"", ""op"": ""state_set"", ""value"": ""alert"", ""text"": ""Lanterns tilt toward the aisles."" }
  ]
}";
    }

    private void TryClearEvents(EventAccumulator acc)
    {
        var m = acc.GetType().GetMethod("ClearEvents");
        if (m != null)
            m.Invoke(acc, null);
    }

    private static string Truncate(string s, int maxChars)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (maxChars <= 0) return "";
        if (s.Length <= maxChars) return s;
        return s.Substring(0, maxChars) + "...";
    }
}
