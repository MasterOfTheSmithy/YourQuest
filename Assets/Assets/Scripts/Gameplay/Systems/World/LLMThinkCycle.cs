// Assets/Assets/Scripts/Gameplay/Systems/World/LLMThinkCycle.cs

using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
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

    [Header("Deterministic Fallback")]
    [Tooltip("If true, meaningful action batches still update world memory when the LLM is unavailable or returns unusable output.")]
    public bool applyDeterministicFallback = true;

    [Range(0f, 1f)]
    public float fallbackConfidence = 0.55f;

    [Tooltip("How strongly fallback reactions raise world tension from action significance.")]
    public float fallbackTensionScale = 0.02f;

    [Tooltip("How strongly fallback reactions increase region importance from action significance.")]
    public float fallbackImportanceScale = 0.03f;

    [Header("Prompt Budgeting")]
    public int maxSituationChars = 2600;
    public int maxSummaryChars = 900;
    public int maxLedgerChars = 900;

    [Header("Queue / Contention Control")]
    [Tooltip("If true, this cycle won't enqueue while LLMClient is busy/queued.")]
    public bool requireIdleLLM = true;

    [Tooltip("If true, when busy, we retry soon instead of skipping an entire think interval.")]
    public bool retrySoonWhenBusy = true;

    [Tooltip("Delay (seconds) between retry attempts while busy.")]
    public float busyRetryDelay = 0.5f;

    [Header("Debug")]
    public bool logPrompt = false;
    // note: Raw generated JSON is opt-in diagnostics; logging it continuously bloats release logs and allocates large strings.
    public bool logRawResponse = false;
    public bool clearEventsAfterApply = true;

    private float nextThinkTime;

    // Prevent retry spirals
    private bool inRepair = false;

    private void Awake()
    {
        ResolveReferences();
        nextThinkTime = Time.time + thinkEverySeconds;
    }

    private void Update()
    {
        ResolveReferences();
        if (Time.time < nextThinkTime) return;

        if (Time.unscaledTime -
            YQGeneratedWorldRuntimeBuilder
                .LastInitialGenerationGameplayUnlockTime <
            90f)
        {
            // note: Do not compete with rendering/streaming immediately after the initial generated world unlocks.
            nextThinkTime =
                Time.time +
                10f;

            return;
        }

        if (RuntimeModalUiBlocker.IsDialogueOpen)
        {
            nextThinkTime = Time.time + 0.35f;
            return;
        }

        // ? If we're repairing, don't also schedule normal thinks.
        if (inRepair)
        {
            // Keep checking periodically rather than drifting forever.
            nextThinkTime = Time.time + Mathf.Max(0.1f, busyRetryDelay);
            return;
        }

        // ? If LLM is busy and we require idleness, retry soon (don't "burn" the whole interval).
        if (LLMClient.Instance != null && LLMClient.Instance.HasPendingHighPriorityRequests)
        {
            nextThinkTime = Time.time + Mathf.Max(0.1f, busyRetryDelay);
            return;
        }

        if (requireIdleLLM && LLMClient.Instance != null && LLMClient.Instance.IsBusy)
        {
            nextThinkTime = Time.time + (retrySoonWhenBusy ? Mathf.Max(0.1f, busyRetryDelay) : thinkEverySeconds);
            return;
        }

        // Only advance timer once we're actually going to try a think.
        nextThinkTime = Time.time + thinkEverySeconds;
        TryThink();
    }

    private void TryThink()
    {
        ResolveReferences();
        if (inRepair) return;

        if (worldDeltaApplier == null)
            return;

        var acc = EventAccumulator.Instance;
        if (acc == null) return;

        var events = acc.GetEvents();
        if (events == null || events.Count == 0) return;

        float sig = 0f;
        for (int i = 0; i < events.Count; i++)
            sig += Mathf.Max(0f, events[i].Significance);

        if (sig < minTotalSignificance) return;

        if (LLMClient.Instance == null)
        {
            TryApplyDeterministicFallback(acc, events, "LLM unavailable");
            return;
        }

        if (RuntimeModalUiBlocker.IsDialogueOpen)
        {
            nextThinkTime = Time.time + Mathf.Max(0.1f, busyRetryDelay);
            return;
        }

        if (LLMClient.Instance.HasPendingHighPriorityRequests)
        {
            nextThinkTime = Time.time + Mathf.Max(0.1f, busyRetryDelay);
            return;
        }

        // ? Second guard (race-safe).
        if (requireIdleLLM && LLMClient.Instance.IsBusy)
        {
            nextThinkTime = Time.time + Mathf.Max(0.1f, busyRetryDelay);
            return;
        }

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

        // ? Only enqueue if still safe to do so.
        if (requireIdleLLM && LLMClient.Instance.IsBusy)
        {
            nextThinkTime = Time.time + Mathf.Max(0.1f, busyRetryDelay);
            return;
        }

        // note: World deltas are structured state changes, never generic skill text.
        LLMClient.Instance.Submit(new YQLlmRequest
        {
            prompt = prompt,
            debugTag = "WorldDeltaGeneration",
            category = LLMGenerationCategory.StructuredState,
            priority = YQLlmRequestPriority.Background,
            requireJson = true,
            // note: Live world reactions yield to dialogue/startup and fail into the deterministic applier instead of monopolizing the local model.
            maxRetries = 0,
            optionsOverride = new Dictionary<string, object>
            {
                { "num_predict", 600 },
                { "request_timeout_seconds", 45 }
            }
        }, result =>
        {
            string raw = result.success ? result.text : null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                TryApplyDeterministicFallback(acc, events, string.IsNullOrWhiteSpace(result.error) ? "empty LLM response" : result.error);
                return;
            }

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
            {
                TryApplyDeterministicFallback(acc, events, "LLM returned no-op");
                return;
            }

            // ? One repair attempt max; and do not overlap with queued LLM requests.
            inRepair = true;

            // If LLM is currently busy due to the other think cycle, retry repair shortly.
            if (requireIdleLLM && LLMClient.Instance.IsBusy)
            {
                inRepair = false;
                nextThinkTime = Time.time + Mathf.Max(0.1f, busyRetryDelay);
                return;
            }

            RetryOnce(schema, raw, err, acc, events);
        });
    }

    private void RetryOnce(string schema, string raw, string error, EventAccumulator acc, IReadOnlyList<ActionEvent> events)
    {
        // ? If something else grabbed the LLM before we start repair, back off.
        if (LLMClient.Instance == null)
        {
            inRepair = false;
            return;
        }

        if (LLMClient.Instance.HasPendingHighPriorityRequests)
        {
            inRepair = false;
            nextThinkTime = Time.time + Mathf.Max(0.1f, busyRetryDelay);
            return;
        }

        if (requireIdleLLM && LLMClient.Instance.IsBusy)
        {
            inRepair = false;
            nextThinkTime = Time.time + Mathf.Max(0.1f, busyRetryDelay);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Return ONLY a single valid JSON object. No markdown. No headings. No explanation.");
        sb.AppendLine("You MUST match the schema exactly.");
        sb.AppendLine();
        sb.AppendLine($"CONFIDENCE RULE: confidence must be a number 0.0 to 1.0 (NOT percent). Must be >= {minConfidenceToApply:0.00}");
        sb.AppendLine();
        sb.AppendLine("ARRAY RULE: flags/factions/locations/regionStyles MUST be arrays of OBJECTS (not strings).");
        sb.AppendLine("If you have no changes for a category, use an empty array [].");
        sb.AppendLine();
        sb.AppendLine("Correct EXAMPLES:");
        sb.AppendLine(@"""flags"": [ { ""key"": ""tension"", ""op"": ""inc"", ""value"": 1 } ]");
        sb.AppendLine(@"""factions"": [ { ""factionId"": ""the_archives"", ""name"": ""The Archives"", ""op"": ""attitude_inc"", ""value"": -0.05, ""text"": ""Rumors spread in the stacks."" } ]");
        sb.AppendLine(@"""locations"": [ { ""locationId"": ""library_hall"", ""name"": ""Library Hall"", ""regionId"": ""region_library"", ""op"": ""state_set"", ""value"": ""alert"", ""text"": ""Footsteps hush; lanterns turn."" } ]");
        sb.AppendLine(@"""regionStyles"": []");
        sb.AppendLine();
        sb.AppendLine("Schema:");
        sb.AppendLine(schema);
        sb.AppendLine();
        sb.AppendLine("Parse error to fix:");
        sb.AppendLine(error);
        sb.AppendLine();
        sb.AppendLine("Bad previous output (for reference only):");
        sb.AppendLine(raw);

        // note: A repair remains a bounded structured-state request and cannot silently become generic generation.
        LLMClient.Instance.Submit(new YQLlmRequest
        {
            prompt = sb.ToString(),
            debugTag = "WorldDeltaRepair",
            category = LLMGenerationCategory.StructuredState,
            priority = YQLlmRequestPriority.Background,
            requireJson = true,
            maxRetries = 0,
            // note: Repair is smaller than first-pass generation and must remain bounded behind player-facing work.
            optionsOverride = new Dictionary<string, object>
            {
                { "num_predict", 500 },
                { "request_timeout_seconds", 40 }
            }
        }, result =>
        {
            string repaired = result.success ? result.text : null;
            if (logRawResponse)
                Debug.Log("[LLMThinkCycle] REPAIRED:\n" + repaired);

            inRepair = false;

            if (string.IsNullOrWhiteSpace(repaired))
            {
                TryApplyDeterministicFallback(acc, events, string.IsNullOrWhiteSpace(result.error) ? "empty LLM repair" : result.error);
                return;
            }

            if (worldDeltaApplier.TryApply(repaired, out var err2))
            {
                Debug.Log("[WORLD SHIFT] World updated (repaired).");
                if (clearEventsAfterApply)
                    TryClearEvents(acc);
                return;
            }

            Debug.LogWarning("[LLMThinkCycle] Repair failed: " + err2);
            TryApplyDeterministicFallback(acc, events, "LLM repair failed");
        });
    }

    private bool TryApplyDeterministicFallback(EventAccumulator acc, IReadOnlyList<ActionEvent> events, string source)
    {
        if (!applyDeterministicFallback || worldDeltaApplier == null || events == null || events.Count == 0)
            return false;

        string summary = BuildFallbackSummary(events, out string verb, out string regionId, out string regionName, out float totalSignificance, out int totalCount);
        if (string.IsNullOrWhiteSpace(verb) || string.IsNullOrWhiteSpace(regionId))
            return false;

        float tensionDelta = CalculateFallbackTensionDelta(verb, totalSignificance);
        float importanceDelta = Mathf.Clamp(totalSignificance * Mathf.Max(0f, fallbackImportanceScale), 0.01f, 0.18f);
        string stateText = BuildFallbackLocationState(verb);
        string rationale = "Fallback response (" + source + "): " + summary;

        JArray flags = new JArray
        {
            new JObject
            {
                ["key"] = "activity_" + SanitizeKey(verb),
                ["op"] = "add",
                ["value"] = Mathf.Max(1f, totalCount)
            }
        };

        if (tensionDelta > 0f)
        {
            flags.Add(new JObject
            {
                ["key"] = "tension",
                ["op"] = "add",
                ["value"] = tensionDelta
            });
        }

        JObject root = new JObject
        {
            ["rationale"] = rationale,
            ["confidence"] = Mathf.Clamp01(fallbackConfidence),
            ["flags"] = flags,
            ["factions"] = new JArray(),
            ["locations"] = new JArray
            {
                new JObject
                {
                    ["locationId"] = regionId,
                    ["name"] = regionName,
                    ["regionId"] = regionId,
                    ["op"] = "state_set",
                    ["value"] = stateText,
                    ["text"] = summary
                },
                new JObject
                {
                    ["locationId"] = regionId,
                    ["name"] = regionName,
                    ["regionId"] = regionId,
                    ["op"] = "importance_inc",
                    ["value"] = importanceDelta,
                    ["text"] = summary
                }
            },
            ["regionStyles"] = new JArray()
        };

        if (worldDeltaApplier.TryApply(root.ToString(Newtonsoft.Json.Formatting.None), out string err))
        {
            Debug.Log("[WORLD SHIFT] World updated by deterministic fallback.");
            if (clearEventsAfterApply)
                TryClearEvents(acc);
            return true;
        }

        Debug.LogWarning("[LLMThinkCycle] Deterministic fallback failed: " + err);
        return false;
    }

    private static string BuildFallbackSummary(
        IReadOnlyList<ActionEvent> events,
        out string dominantVerb,
        out string dominantRegionId,
        out string dominantRegionName,
        out float totalSignificance,
        out int totalCount)
    {
        Dictionary<string, float> verbWeight = new Dictionary<string, float>();
        Dictionary<string, int> verbCount = new Dictionary<string, int>();
        Dictionary<string, float> regionWeight = new Dictionary<string, float>();
        Dictionary<string, string> regionNames = new Dictionary<string, string>();

        dominantVerb = "unknown";
        dominantRegionId = CurrentWorldRegionId();
        dominantRegionName = CurrentWorldRegionName();
        totalSignificance = 0f;
        totalCount = 0;

        for (int i = 0; i < events.Count; i++)
        {
            ActionEvent ev = events[i];
            if (ev == null)
                continue;

            string verb = string.IsNullOrWhiteSpace(ev.Verb) ? "unknown" : ev.Verb.Trim().ToLowerInvariant();
            string regionId = !string.IsNullOrWhiteSpace(ev.RegionId) ? ev.RegionId.Trim() : dominantRegionId;
            string regionName = !string.IsNullOrWhiteSpace(ev.RegionName) ? ev.RegionName.Trim() : regionId;
            float sig = Mathf.Max(0f, ev.Significance);

            verbWeight.TryGetValue(verb, out float currentVerbWeight);
            verbWeight[verb] = currentVerbWeight + sig;
            verbCount.TryGetValue(verb, out int currentVerbCount);
            verbCount[verb] = currentVerbCount + 1;

            regionWeight.TryGetValue(regionId, out float currentRegionWeight);
            regionWeight[regionId] = currentRegionWeight + sig;
            regionNames[regionId] = regionName;

            totalSignificance += sig;
            totalCount++;
        }

        float bestVerbWeight = -1f;
        foreach (KeyValuePair<string, float> kvp in verbWeight)
        {
            if (kvp.Value > bestVerbWeight)
            {
                dominantVerb = kvp.Key;
                bestVerbWeight = kvp.Value;
            }
        }

        float bestRegionWeight = -1f;
        foreach (KeyValuePair<string, float> kvp in regionWeight)
        {
            if (kvp.Value > bestRegionWeight)
            {
                dominantRegionId = kvp.Key;
                bestRegionWeight = kvp.Value;
            }
        }

        if (regionNames.TryGetValue(dominantRegionId, out string resolvedRegionName) && !string.IsNullOrWhiteSpace(resolvedRegionName))
            dominantRegionName = resolvedRegionName;
        if (string.IsNullOrWhiteSpace(dominantRegionName))
            dominantRegionName = dominantRegionId;

        int count = verbCount.TryGetValue(dominantVerb, out int resolvedCount) ? resolvedCount : totalCount;
        return "The player repeated " + dominantVerb + " in " + dominantRegionName + " (" + count + " events, significance " + totalSignificance.ToString("0.00") + ").";
    }

    private float CalculateFallbackTensionDelta(string verb, float totalSignificance)
    {
        float weight;
        switch (SanitizeKey(verb))
        {
            case "combat":
                weight = 1f;
                break;
            case "dodge":
            case "crouch":
            case "climb":
                weight = 0.35f;
                break;
            case "interact":
                weight = 0.25f;
                break;
            case "movement":
            case "jump":
                weight = 0.12f;
                break;
            default:
                weight = 0.18f;
                break;
        }

        return Mathf.Clamp(totalSignificance * Mathf.Max(0f, fallbackTensionScale) * weight, 0.005f, 0.08f);
    }

    private static string BuildFallbackLocationState(string verb)
    {
        switch (SanitizeKey(verb))
        {
            case "combat":
                return "contested";
            case "interact":
                return "stirred";
            case "crouch":
                return "watched";
            case "dodge":
                return "disturbed";
            case "climb":
                return "scaled";
            case "movement":
            case "jump":
                return "traveled";
            default:
                return "active";
        }
    }

    private static string CurrentWorldRegionId()
    {
        WorldStateManager wsm = WorldStateManager.Instance;
        if (wsm != null && wsm.State != null && !string.IsNullOrWhiteSpace(wsm.State.currentRegionId))
            return wsm.State.currentRegionId;
        return "region_unknown";
    }

    private static string CurrentWorldRegionName()
    {
        WorldStateManager wsm = WorldStateManager.Instance;
        if (wsm != null && wsm.State != null && !string.IsNullOrWhiteSpace(wsm.State.currentRegionName))
            return wsm.State.currentRegionName;
        return CurrentWorldRegionId();
    }

    private static string SanitizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        string lower = value.Trim().ToLowerInvariant();
        StringBuilder sb = new StringBuilder(lower.Length);
        for (int i = 0; i < lower.Length; i++)
        {
            char c = lower[i];
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }
        return sb.Length == 0 ? "unknown" : sb.ToString();
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
            ResolveReferences();
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
        sb.AppendLine("- regionStyles items: { regionId, styleKey, reason }. Use at most one. Routine movement, combat, trading, or tension NEVER changes architecture; return [] for those.");
        sb.AppendLine("- A cross-genre or surface/interior style shift requires a concrete canonical physical cause such as a portal, invasion, disaster, construction, corruption, ritual, flood, or reality rupture, stated in reason.");
        sb.AppendLine("- Valid styleKey values: nordic_forest, viking_rural, ancient_desert_ruins, western_desert_town, asian_dynasty, persepolis_empire, victorian_mansion, container_district, bio_horror_scifi, scifi_engineers_room, hivemind_medieval_kingdom, hivemind_military_camp, hivemind_gothic_cathedral, hivemind_cyberpunk_city, hivemind_gladiator_arena, hivemind_rural_town, hivemind_modular_viking_village, hivemind_town_smith, hivemind_haunted_village, hivemind_mystic_dungeon, hivemind_mountain_temple, hivemind_woodland_village, hivemind_witch_house, hivemind_cave_tomb, hivemind_house_on_hill, hivemind_villa_forge, hivemind_horror_hospital, hivemind_olympus_temple, hivemind_pirate_island, hivemind_hallowed_depths, hivemind_sewers, hivemind_mountain_messenger.");
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
  ],

  ""regionStyles"": [
    { ""regionId"": ""region_library"", ""styleKey"": ""victorian_mansion"", ""reason"": ""A sustained canonical transformation made the archive physically dominant."" }
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

    private void ResolveReferences()
    {
        if (worldDeltaApplier == null)
            worldDeltaApplier = FindFirstObjectByType<WorldDeltaApplier>();

        if (worldDeltaApplier != null)
            worldDeltaApplier.minConfidence = minConfidenceToApply;

        if (situationSnapshotBuilder == null)
            situationSnapshotBuilder = FindFirstObjectByType<SituationSnapshotBuilder>();
    }
}
