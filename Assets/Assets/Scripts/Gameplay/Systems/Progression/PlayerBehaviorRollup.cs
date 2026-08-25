using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// PlayerBehaviorRollup:
/// - Reads EventAccumulator ActionEvents
/// - Converts older events into a compact, long-term behavior ledger stored in PlayerState
/// - Prunes those older events from the short-term buffer
///
/// This keeps:
/// - buffer = "short-term, detailed evidence"
/// - ledger = "long-term, compressed evidence"
/// </summary>
public class PlayerBehaviorRollup : MonoBehaviour
{
    [Header("Cadence")]
    [Tooltip("How often to check whether old action events should be compressed into long-term player memory.")]
    public float rollupEverySeconds = 60f;

    [Header("Window")]
    [Tooltip("Keep the last N minutes in the short-term buffer. Older than this gets rolled into the ledger.")]
    public int keepMinutesInBuffer = 30;

    [Tooltip("Max ledger lines to keep in PlayerState.")]
    public int maxLedgerLines = 60;

    [Header("Refs")]
    public EventAccumulator accumulator;
    public PlayerStateManager playerStateManager;

    [Header("Debug")]
    public bool logRollups = false;

    private float _nextRollupTime;

    private void Awake()
    {
        if (accumulator == null) accumulator = FindFirstObjectByType<EventAccumulator>();
        if (playerStateManager == null) playerStateManager = FindFirstObjectByType<PlayerStateManager>();
        _nextRollupTime = Time.time + Mathf.Max(5f, rollupEverySeconds);
    }

    private void Update()
    {
        if (Time.time < _nextRollupTime)
            return;

        _nextRollupTime = Time.time + Mathf.Max(5f, rollupEverySeconds);
        RollupIfNeeded();
    }

    /// <summary>
    /// Call this periodically (or right before a progression/world think) to keep evidence clean.
    /// </summary>
    public void RollupIfNeeded()
    {
        if (accumulator == null || playerStateManager == null) return;
        if (playerStateManager.state == null) return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long cutoff = now - (keepMinutesInBuffer * 60L);

        var events = accumulator.GetEvents();
        if (events == null || events.Count == 0) return;

        // Collect older events
        var older = new List<ActionEvent>(256);
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e == null) continue;
            if (e.UnixTime < cutoff)
                older.Add(e);
        }

        if (older.Count == 0) return;

        string line = BuildLedgerLine(older);

        // Write ledger evidence
        var ps = playerStateManager.state;
        ps.AddLedgerLine(line, maxLedgerLines);

        // Update counters (optional but good for balancing)
        ApplyCounters(ps, older);

        ps.lastLedgerRollupUnix = now;
        ps.Touch();
        if (playerStateManager.autosave) playerStateManager.Save();

        // Prune rolled events out of buffer
        int removed = accumulator.PruneEventsBeforeUnix(cutoff);

        if (logRollups)
            Debug.Log($"[PlayerBehaviorRollup] Rolled {older.Count} events into ledger; pruned {removed} events.\nLedger: {line}");
    }

    private static string BuildLedgerLine(List<ActionEvent> older)
    {
        // Group by verb + region
        var counts = new Dictionary<string, int>(128);
        var regionCounts = new Dictionary<string, int>(64);

        long minT = long.MaxValue;
        long maxT = 0;

        for (int i = 0; i < older.Count; i++)
        {
            var e = older[i];
            if (e == null) continue;

            string verb = string.IsNullOrWhiteSpace(e.Verb) ? "unknown" : e.Verb.Trim().ToLowerInvariant();
            string region = string.IsNullOrWhiteSpace(e.RegionId) ? "region_unknown" : e.RegionId.Trim().ToLowerInvariant();

            string key = $"{verb}@{region}";
            counts.TryGetValue(key, out int cur);
            counts[key] = cur + 1;

            regionCounts.TryGetValue(region, out int rcur);
            regionCounts[region] = rcur + 1;

            if (e.UnixTime < minT) minT = e.UnixTime;
            if (e.UnixTime > maxT) maxT = e.UnixTime;
        }

        // pick top region
        string topRegion = "region_unknown";
        int topRegionCount = 0;
        foreach (var kv in regionCounts)
        {
            if (kv.Value > topRegionCount)
            {
                topRegion = kv.Key;
                topRegionCount = kv.Value;
            }
        }

        // pick top 3 behaviors
        var top = new List<KeyValuePair<string, int>>(counts);
        top.Sort((a, b) => b.Value.CompareTo(a.Value));

        var sb = new StringBuilder(512);
        sb.Append($"[{UnixToShortTime(minT)}-{UnixToShortTime(maxT)}] ");
        sb.Append($"Mostly in {topRegion}. ");

        int take = Mathf.Min(3, top.Count);
        for (int i = 0; i < take; i++)
        {
            var kv = top[i];
            sb.Append(kv.Key);
            sb.Append(" x");
            sb.Append(kv.Value);
            if (i < take - 1) sb.Append(", ");
        }

        return sb.ToString();
    }

    private static string UnixToShortTime(long unix)
    {
        if (unix <= 0) return "??:??";
        try
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
            return dt.ToString("HH:mm");
        }
        catch
        {
            return "??:??";
        }
    }

    private static void ApplyCounters(PlayerState ps, List<ActionEvent> older)
    {
        if (ps == null) return;

        for (int i = 0; i < older.Count; i++)
        {
            var e = older[i];
            if (e == null) continue;

            string verb = string.IsNullOrWhiteSpace(e.Verb) ? "unknown" : e.Verb.Trim().ToLowerInvariant();
            string region = string.IsNullOrWhiteSpace(e.RegionId) ? "region_unknown" : e.RegionId.Trim().ToLowerInvariant();

            if (!e.BehaviorCountersApplied)
            {
                // note: Events created outside ActionRecorder still become long-term evidence during the delayed rollup.
                ps.IncCounter($"verb:{verb}", 1f);
                ps.IncCounter($"region:{region}", 1f);
                e.BehaviorCountersApplied = true;
            }

            // If you later add richer tags (threat, stance, tool, etc.), increment them here too.
        }
    }
}
