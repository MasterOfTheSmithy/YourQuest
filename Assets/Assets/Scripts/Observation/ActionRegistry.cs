using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Long-lived behavior ledger:
/// - stores recent raw history (ring-buffer)
/// - aggregates counts/significance by (verb + scene + region + targetName)
/// Keep separate from EventAccumulator (short-term LLM batching).
/// </summary>
public class ActionRegistry : MonoBehaviour
{
    public static ActionRegistry Instance { get; private set; }

    [Header("Raw History")]
    [SerializeField] private int maxHistory = 200;

    [Header("Context Bucketing")]
    [Tooltip("World-space grid size used to bucket actions into coarse regions.")]
    [SerializeField] private float regionCellSize = 20f;

    private readonly List<ActionEvent> history = new();

    // Aggregated stats (internal hash key -> stats)
    private readonly Dictionary<ActionKey, ActionStat> stats = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Record an event into raw history and aggregated stats.
    /// </summary>
    public void Record(ActionEvent ev)
    {
        if (ev == null) return;

        // Ensure scene name is filled
        if (string.IsNullOrWhiteSpace(ev.SceneName))
            ev.SceneName = SceneManager.GetActiveScene().name;

        // Ensure region is filled
        if (string.IsNullOrWhiteSpace(ev.RegionId) || ev.RegionId == "unknown")
            ev.RegionId = ComputeRegionId(ev.Position);

        history.Add(ev);
        if (history.Count > maxHistory)
            history.RemoveAt(0);

        var key = new ActionKey(ev.Verb, ev.SceneName, ev.RegionId, ev.TargetName);

        if (!stats.TryGetValue(key, out var stat))
        {
            stat = new ActionStat();
            stats[key] = stat;
        }

        stat.Count++;
        stat.TotalSignificance += ev.Significance;
        stat.LastUnixTime = ev.UnixTime;
        stat.LastPosition = ev.Position;
    }

    public void ClearAll()
    {
        history.Clear();
        stats.Clear();
    }

    public IReadOnlyList<ActionEvent> GetRecentHistory() => history;

    /// <summary>
    /// Returns a snapshot list of aggregated stats, sorted by "most impactful" first.
    /// Impact = TotalSignificance then Count.
    /// Public API returns a safe DTO (ActionStatSnapshot) and does not expose internal hashing keys.
    /// </summary>
    public List<ActionStatSnapshot> GetTopStats(int max = 30)
    {
        var list = new List<ActionStatSnapshot>(stats.Count);
        foreach (var kv in stats)
            list.Add(new ActionStatSnapshot(kv.Key, kv.Value));

        list.Sort((a, b) =>
        {
            int sig = b.TotalSignificance.CompareTo(a.TotalSignificance);
            if (sig != 0) return sig;
            return b.Count.CompareTo(a.Count);
        });

        if (max > 0 && list.Count > max)
            list.RemoveRange(max, list.Count - max);

        return list;
    }

    /// <summary>
    /// Quick helper for building an LLM-friendly “behavior memory” string.
    /// </summary>
    public string BuildBehaviorSummary(int top = 20)
    {
        var topStats = GetTopStats(top);
        if (topStats.Count == 0) return "No behavior recorded.";

        StringBuilder sb = new();
        sb.AppendLine("Behavior Ledger (most impactful):");

        foreach (var entry in topStats)
        {
            sb.Append("- ");
            sb.Append(entry.Verb);
            sb.Append(" | scene:");
            sb.Append(entry.Scene);
            sb.Append(" | region:");
            sb.Append(entry.Region);

            if (!string.IsNullOrWhiteSpace(entry.TargetName))
            {
                sb.Append(" | target:");
                sb.Append(entry.TargetName);
            }

            sb.Append(" | count:");
            sb.Append(entry.Count);
            sb.Append(" | sig:");
            sb.Append(entry.TotalSignificance.ToString("0.00"));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string ComputeRegionId(Vector3 pos)
    {
        if (regionCellSize <= 0.01f) regionCellSize = 20f;

        int cx = Mathf.FloorToInt(pos.x / regionCellSize);
        int cz = Mathf.FloorToInt(pos.z / regionCellSize);

        // Deliberately ignores Y to keep it stable across slopes/vertical gameplay.
        return $"x{cx}_z{cz}";
    }

    #region Data Types

    [Serializable]
    internal struct ActionKey : IEquatable<ActionKey>
    {
        public string Verb;
        public string Scene;
        public string Region;
        public string TargetName;

        public ActionKey(string verb, string scene, string region, string targetName)
        {
            Verb = verb ?? "unknown";
            Scene = scene ?? "unknown";
            Region = region ?? "unknown";
            TargetName = targetName ?? ""; // empty is valid for “no target”
        }

        public bool Equals(ActionKey other)
        {
            return Verb == other.Verb
                && Scene == other.Scene
                && Region == other.Region
                && TargetName == other.TargetName;
        }

        public override bool Equals(object obj) => obj is ActionKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Verb.GetHashCode();
                hash = hash * 31 + Scene.GetHashCode();
                hash = hash * 31 + Region.GetHashCode();
                hash = hash * 31 + TargetName.GetHashCode();
                return hash;
            }
        }
    }

    [Serializable]
    public class ActionStat
    {
        public int Count;
        public float TotalSignificance;

        public long LastUnixTime;
        public Vector3 LastPosition;
    }

    /// <summary>
    /// Public, safe-to-return snapshot of an aggregated behavior bucket.
    /// This avoids exposing internal ActionKey hashing/structure.
    /// </summary>
    [Serializable]
    public readonly struct ActionStatSnapshot
    {
        public readonly string Verb;
        public readonly string Scene;
        public readonly string Region;
        public readonly string TargetName;

        public readonly int Count;
        public readonly float TotalSignificance;

        public readonly long LastUnixTime;
        public readonly Vector3 LastPosition;

        internal ActionStatSnapshot(ActionKey key, ActionStat stat)
        {
            Verb = key.Verb;
            Scene = key.Scene;
            Region = key.Region;
            TargetName = key.TargetName;

            Count = stat.Count;
            TotalSignificance = stat.TotalSignificance;

            LastUnixTime = stat.LastUnixTime;
            LastPosition = stat.LastPosition;
        }
    }

    #endregion
}

