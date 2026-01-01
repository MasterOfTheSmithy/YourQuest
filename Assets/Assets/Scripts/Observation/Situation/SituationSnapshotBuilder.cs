using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Builds a token-efficient, bounded, structured situation snapshot string.
/// Designed to be cheap enough for 1 Hz (or higher) ticks.
/// </summary>
public class SituationSnapshotBuilder : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;
    public ThreatProbe threatProbe;
    public MovementPatternClassifier movementClassifier;

    [Header("Budgets")]
    [Tooltip("Hard cap for snapshot text length (characters). Proxy for token limit.")]
    public int maxChars = 2600;

    public int maxThreats = 8;
    public int maxNotables = 5;

    [Header("Player (temporary)")]
    public int playerLevelGuess = 1;

    [Header("Tick")]
    public float tickRateSeconds = 1.0f;

    [Header("Debug")]
    public bool logSnapshotEachTick = false;
    public bool logOnlyWhenChanged = true;

    private float nextTick;
    public string LastSnapshot { get; private set; } = "";

    // Change detection (prevents churn + log spam)
    private string lastFingerprint = "";

    private void Update()
    {
        if (Time.time < nextTick) return;
        nextTick = Time.time + Mathf.Max(0.1f, tickRateSeconds);

        string snap = BuildSnapshot();
        if (!logSnapshotEachTick) return;

        if (logOnlyWhenChanged)
        {
            string fp = Fingerprint(snap);
            if (fp == lastFingerprint) return;
            lastFingerprint = fp;
        }

        Debug.Log("[SITUATION_SNAPSHOT]\n" + snap);
    }

    public string BuildSnapshot()
    {
        if (playerTransform == null || threatProbe == null || movementClassifier == null)
        {
            LastSnapshot = "SITUATION: missing refs (playerTransform/threatProbe/movementClassifier)";
            return LastSnapshot;
        }

        threatProbe.Tick(playerLevelGuess);

        // Region context
        string regId = "region_unknown";
        string regName = "Unknown";
        string regTags = null;

        var ctx = PlayerContext.Instance;
        if (ctx != null)
        {
            if (!string.IsNullOrWhiteSpace(ctx.SemanticRegionId)) regId = ctx.SemanticRegionId;
            if (!string.IsNullOrWhiteSpace(ctx.SemanticRegionName)) regName = ctx.SemanticRegionName;
            if (ctx.RegionTags != null && ctx.RegionTags.Count > 0)
                regTags = string.Join(",", ctx.RegionTags);
        }

        // Targeting
        var incoming = TargetingTracker.Instance != null ? TargetingTracker.Instance.GetIncomingTargetersToPlayer() : new List<string>();
        var outgoing = TargetingTracker.Instance != null ? TargetingTracker.Instance.GetPlayerOutgoingTargetId() : null;

        // Movement
        string mvPat = movementClassifier.CurrentPattern.ToString();
        float mvSpd = movementClassifier.AvgSpeed;
        float mvTurn = movementClassifier.Turniness;

        // Combat state heuristic
        string combatState = "CALM";
        if (incoming.Count > 0 && threatProbe.ThreatScore01 >= 0.45f) combatState = "THREATENED";
        if (incoming.Count > 0 && threatProbe.ThreatScore01 >= 0.70f) combatState = "IN_COMBAT";

        // Situation flags
        var sf = new List<string>(10);
        if (threatProbe.ThreatScore01 >= 0.70f) sf.Add("HIGH_THREAT");
        else if (threatProbe.ThreatScore01 >= 0.45f) sf.Add("THREAT_PRESENT");
        else sf.Add("LOW_THREAT");

        if (threatProbe.MaxHostileLevelDisparity >= 5) sf.Add("OUTMATCHED");
        if (incoming.Count > 0) sf.Add("HOSTILE_TARGET_LOCK");
        if (!string.IsNullOrWhiteSpace(regTags))
        {
            if (regTags.Contains("indoors")) sf.Add("INDOORS");
            if (regTags.Contains("dark")) sf.Add("DARK");
            if (regTags.Contains("restricted")) sf.Add("RESTRICTED");
        }

        // Build compact JSON-like snapshot
        var sb = new StringBuilder(2048);
        sb.Append("{");
        AppendKV(sb, "regId", regId);
        AppendKV(sb, "regName", regName);
        if (!string.IsNullOrWhiteSpace(regTags)) AppendKV(sb, "regTags", regTags);

        AppendKV(sb, "pLv", playerLevelGuess.ToString());
        AppendKV(sb, "combat", combatState);

        AppendKV(sb, "mvPat", mvPat);
        AppendKV(sb, "mvSpd", mvSpd.ToString("0.0"));
        AppendKV(sb, "mvTurn", mvTurn.ToString("0.00"));

        AppendKV(sb, "thr", threatProbe.ThreatScore01.ToString("0.00"));
        AppendKV(sb, "disMax", threatProbe.MaxHostileLevelDisparity.ToString());
        AppendKV(sb, "inTarN", incoming.Count.ToString());
        if (!string.IsNullOrWhiteSpace(outgoing)) AppendKV(sb, "outTar", outgoing);

        sb.Append("\"sf\":[");
        for (int i = 0; i < sf.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("\"").Append(sf[i]).Append("\"");
        }
        sb.Append("],");

        sb.Append("\"thrList\":[");
        int tCount = Mathf.Min(maxThreats, threatProbe.Threats.Count);
        for (int i = 0; i < tCount; i++)
        {
            var t = threatProbe.Threats[i];
            if (i > 0) sb.Append(",");
            sb.Append("{");
            AppendKV(sb, "id", t.id, comma: true);
            AppendKV(sb, "n", t.name, comma: true);
            AppendKV(sb, "lv", t.level.ToString(), comma: true);
            AppendKV(sb, "fac", t.faction, comma: true);
            AppendKV(sb, "h", t.hostile ? "1" : "0", comma: true);
            AppendKV(sb, "d", t.distance.ToString("0.0"), comma: true);
            AppendKV(sb, "los", t.hasLos ? "1" : "0", comma: true);
            AppendKV(sb, "tarp", t.targetingPlayer ? "1" : "0", comma: true);
            AppendKV(sb, "s", t.score.ToString("0.00"), comma: false);
            sb.Append("}");
            if (sb.Length > maxChars) break;
        }
        sb.Append("],");

        sb.Append("\"not\":[");
        int nCount = Mathf.Min(maxNotables, threatProbe.Notables.Count);
        for (int i = 0; i < nCount; i++)
        {
            var n = threatProbe.Notables[i];
            if (i > 0) sb.Append(",");
            sb.Append("{");
            AppendKV(sb, "id", n.id, comma: true);
            AppendKV(sb, "n", n.name, comma: true);
            AppendKV(sb, "lv", n.level.ToString(), comma: true);
            AppendKV(sb, "fac", n.faction, comma: true);
            AppendKV(sb, "d", n.distance.ToString("0.0"), comma: false);
            sb.Append("}");
            if (sb.Length > maxChars) break;
        }
        sb.Append("]");

        sb.Append("}");

        LastSnapshot = (sb.Length > maxChars)
            ? sb.ToString(0, maxChars) + "..."
            : sb.ToString();

        return LastSnapshot;
    }

    private static void AppendKV(StringBuilder sb, string key, string value, bool comma = true)
    {
        sb.Append("\"").Append(key).Append("\":");
        sb.Append("\"").Append(Escape(value)).Append("\"");
        if (comma) sb.Append(",");
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string Fingerprint(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        // Cheap fingerprint: length + first/last few chars
        int len = s.Length;
        string head = s.Substring(0, Mathf.Min(64, len));
        string tail = s.Substring(Mathf.Max(0, len - 64), Mathf.Min(64, len));
        return len + "|" + head + "|" + tail;
    }
}
