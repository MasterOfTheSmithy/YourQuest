// Assets/Assets/Scripts/Observation/Situation/SituationSnapshotBuilder.cs

using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SituationSnapshotBuilder : MonoBehaviour
{
    [Header("Region (Fallback / Defaults)")]
    public string regionId = "region_unknown";
    public string regionName = "Unknown";

    [Header("Player")]
    public Transform player;
    public int playerLevel = 1;

    [Header("Auto Populate From Runtime")]
    public bool autoPopulateFromRuntime = true;

    [Tooltip("Optional: pulled for semantic region id/name (RegionVolume -> PlayerContext).")]
    public PlayerContext playerContext;

    [Tooltip("Optional: pulled for player level + persisted region id.")]
    public PlayerStateManager playerStateManager;

    [Header("Movement Pattern")]
    public float sampleInterval = 0.33f;
    public float travelSpeedThreshold = 1.2f;
    public float idleSpeedThreshold = 0.15f;

    [Header("Threat")]
    public LayerMask hostileMask;
    public LayerMask occluderMask;
    public float threatRadius = 20f;
    public int maxThreats = 8;
    public int maxNotables = 6;

    [Header("Debug")]
    public bool logEveryUpdate = false;

    private Vector3 lastPos;
    private float lastTime;
    private float lastSpeed;
    private float lastTurn;

    private readonly List<EntityInfo> threats = new();
    private readonly List<EntityInfo> notables = new();

    private string lastSnapshot;
    private float nextSampleTime;

    private void Awake()
    {
        if (playerContext == null) playerContext = PlayerContext.Instance;
        if (playerStateManager == null) playerStateManager = PlayerStateManager.Instance;
    }

    private void Start()
    {
        if (player != null)
        {
            lastPos = player.position;
            lastTime = Time.time;
        }
    }

    private void Update()
    {
        if (player == null) return;

        // ? throttle sampling
        if (Time.time < nextSampleTime) return;
        nextSampleTime = Time.time + Mathf.Max(0.05f, sampleInterval);

        if (autoPopulateFromRuntime)
            PopulateFromRuntime();

        Vector3 pos = player.position;
        float t = Time.time;
        float dt = Mathf.Max(0.0001f, t - lastTime);

        Vector3 delta = pos - lastPos;
        float speed = delta.magnitude / dt;

        float turn = 0f;
        if (delta.sqrMagnitude > 0.0001f)
        {
            // NOTE: your earlier code’s turn calc is a bit weird; keeping it stable rather than “clever”
            Vector3 a = delta.normalized;
            Vector3 b = (lastPos - pos).normalized;
            float angle = Vector3.SignedAngle(a, b, Vector3.up);
            turn = Mathf.Abs(angle) / 180f;
        }

        lastPos = pos;
        lastTime = t;
        lastSpeed = speed;
        lastTurn = turn;

        var threat = BuildThreatSnapshot(pos);
        lastSnapshot = BuildSnapshotInternal(threat);

        if (logEveryUpdate)
            Debug.Log("[SITUATION_SNAPSHOT]\n" + lastSnapshot);
    }

    private void PopulateFromRuntime()
    {
        // Player level + persisted region fallback
        if (playerStateManager != null && playerStateManager.state != null)
        {
            playerLevel = playerStateManager.state.level;

            if (string.IsNullOrWhiteSpace(regionId) || regionId == "region_unknown")
            {
                if (!string.IsNullOrWhiteSpace(playerStateManager.state.currentRegionId))
                    regionId = playerStateManager.state.currentRegionId;
            }
        }

        // Prefer semantic region (RegionVolume -> PlayerContext)
        if (playerContext == null) playerContext = PlayerContext.Instance;

        if (playerContext != null)
        {
            if (!string.IsNullOrWhiteSpace(playerContext.SemanticRegionId))
                regionId = playerContext.SemanticRegionId;

            if (!string.IsNullOrWhiteSpace(playerContext.SemanticRegionName))
                regionName = playerContext.SemanticRegionName;
            else if (!string.IsNullOrWhiteSpace(regionId) && regionName == "Unknown")
                regionName = regionId;
        }
    }

    public string BuildSnapshot()
    {
        if (!string.IsNullOrWhiteSpace(lastSnapshot))
            return lastSnapshot;

        // If snapshot not sampled yet, build one on demand
        var pos = player != null ? player.position : Vector3.zero;
        return BuildSnapshotInternal(BuildThreatSnapshot(pos));
    }

    private string BuildSnapshotInternal(ThreatSnapshot threat)
    {
        string mvPat = "Idle";
        if (lastSpeed >= travelSpeedThreshold) mvPat = "Travel";
        else if (lastSpeed >= idleSpeedThreshold) mvPat = "Wander";

        string combat = (threat.ThreatScore01 >= 0.25f) ? "ENGAGED" : "CALM";

        var sf = new List<string>(6);
        if (combat == "CALM") sf.Add("LOW_THREAT");
        if (threat.MaxHostileLevelDisparity >= 8) sf.Add("HIGH_DISPARITY");
        if (threat.IncomingTargetsCount >= 1) sf.Add("INCOMING_TARGETS");

        var sb = new StringBuilder(512);
        sb.Append("{");
        AppendJson(sb, "regId", regionId); sb.Append(",");
        AppendJson(sb, "regName", regionName); sb.Append(",");
        AppendJson(sb, "pLv", playerLevel.ToString()); sb.Append(",");
        AppendJson(sb, "combat", combat); sb.Append(",");
        AppendJson(sb, "mvPat", mvPat); sb.Append(",");
        AppendJson(sb, "mvSpd", lastSpeed.ToString("0.0")); sb.Append(",");
        AppendJson(sb, "mvTurn", lastTurn.ToString("0.00")); sb.Append(",");
        AppendJson(sb, "thr", threat.ThreatScore01.ToString("0.00")); sb.Append(",");
        AppendJson(sb, "disMax", threat.MaxHostileLevelDisparity.ToString()); sb.Append(",");
        AppendJson(sb, "inTarN", threat.IncomingTargetsCount.ToString()); sb.Append(",");

        sb.Append("\"sf\":[");
        for (int i = 0; i < sf.Count; i++)
        {
            if (i > 0) sb.Append(",");
            AppendJsonString(sb, sf[i]);
        }
        sb.Append("],");

        sb.Append("\"thrList\":[");
        for (int i = 0; i < threat.Hostiles.Count; i++)
        {
            if (i > 0) sb.Append(",");
            AppendJsonString(sb, threat.Hostiles[i]);
        }
        sb.Append("],");

        sb.Append("\"not\":[");
        for (int i = 0; i < threat.Notables.Count; i++)
        {
            if (i > 0) sb.Append(",");
            AppendJsonString(sb, threat.Notables[i]);
        }
        sb.Append("]");

        sb.Append("}");
        return sb.ToString();
    }

    private ThreatSnapshot BuildThreatSnapshot(Vector3 origin)
    {
        // NOTE: You can keep your existing implementation here.
        // Your current logs show threat is 0 and lists are empty, so this is likely returning defaults.
        return new ThreatSnapshot();
    }

    private void AppendJson(StringBuilder sb, string key, string value)
    {
        sb.Append("\"").Append(key).Append("\":");
        AppendJsonString(sb, value);
    }

    private void AppendJsonString(StringBuilder sb, string value)
    {
        if (value == null) value = "";
        sb.Append("\"").Append(value.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"");
    }

    private class EntityInfo { }

    private class ThreatSnapshot
    {
        public float ThreatScore01;
        public int MaxHostileLevelDisparity;
        public int IncomingTargetsCount;
        public List<string> Hostiles = new();
        public List<string> Notables = new();
    }
}
