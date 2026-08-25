// Assets/Assets/Scripts/Observation/Situation/SituationSnapshotBuilder.cs
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

[DisallowMultipleComponent]
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
    public PlayerContext playerContext;
    public PlayerStateManager playerStateManager;

    [Header("Movement Pattern")]
    public float sampleInterval = 0.25f;
    public float travelSpeedThreshold = 1.2f;
    public float idleSpeedThreshold = 0.15f;

    [Header("Threat")]
    public LayerMask hostileMask = ~0;
    public LayerMask occluderMask = ~0;
    public float threatRadius = 18f;
    public int maxThreats = 8;
    public int maxNotables = 6;
    public bool requireLineOfSight = false;

    [Header("Debug")]
    public bool logEveryUpdate = false;

    private Vector3 _lastPos;
    private float _lastTime;
    private float _lastSpeed;
    private float _lastTurn;
    private float _nextSampleTime;
    private string _lastSnapshot;
    private Vector3 _lastMoveDir = Vector3.forward;

    private readonly Collider[] _overlapBuffer = new Collider[128];
    private readonly List<EntityInfo> _threats = new List<EntityInfo>(16);
    private readonly List<EntityInfo> _notables = new List<EntityInfo>(16);

    private void Awake()
    {
        ResolveRuntimeReferences();
    }

    private void OnEnable()
    {
        ResolveRuntimeReferences();
    }

    private void Start()
    {
        ResolvePlayer();
        if (player != null)
        {
            _lastPos = player.position;
            _lastTime = Time.time;
        }
    }

    private void Update()
    {
        ResolvePlayer();
        if (player == null)
            return;

        if (Time.time < _nextSampleTime)
            return;

        _nextSampleTime = Time.time + Mathf.Max(0.05f, sampleInterval);

        if (autoPopulateFromRuntime)
            PopulateFromRuntime();

        Vector3 pos = player.position;
        float now = Time.time;
        float dt = Mathf.Max(0.0001f, now - _lastTime);
        Vector3 delta = pos - _lastPos;
        Vector3 planarDelta = new Vector3(delta.x, 0f, delta.z);
        _lastSpeed = planarDelta.magnitude / dt;

        Vector3 moveDir = planarDelta.sqrMagnitude > 0.0001f ? planarDelta.normalized : _lastMoveDir;
        _lastTurn = Vector3.Angle(_lastMoveDir, moveDir) / 180f;
        _lastMoveDir = moveDir;
        _lastPos = pos;
        _lastTime = now;

        ThreatSnapshot threat = BuildThreatSnapshot(pos);
        _lastSnapshot = BuildSnapshotInternal(threat);
        if (logEveryUpdate)
            Debug.Log("[SITUATION_SNAPSHOT]\n" + _lastSnapshot);
    }

    public string BuildSnapshot()
    {
        ResolveRuntimeReferences();
        if (!string.IsNullOrWhiteSpace(_lastSnapshot))
            return _lastSnapshot;

        ResolvePlayer();
        if (player == null)
            return "{\"regId\":\"region_unknown\",\"regName\":\"Unknown\",\"pLv\":\"1\",\"combat\":\"CALM\",\"mvPat\":\"Idle\",\"mvSpd\":\"0.0\",\"mvTurn\":\"0.00\",\"thr\":\"0.00\",\"disMax\":\"0\",\"inTarN\":\"0\",\"sf\":[\"LOW_THREAT\"],\"thrList\":[],\"not\":[]}";

        if (autoPopulateFromRuntime)
            PopulateFromRuntime();

        return BuildSnapshotInternal(BuildThreatSnapshot(player.position));
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject found = GameObject.FindWithTag("Player");
        if (found != null)
            player = found.transform;
    }

    private void ResolveRuntimeReferences()
    {
        ResolvePlayer();
        if (playerContext == null)
            playerContext = PlayerContext.Instance != null ? PlayerContext.Instance : FindFirstObjectByType<PlayerContext>();
        if (playerStateManager == null)
            playerStateManager = PlayerStateManager.Instance != null ? PlayerStateManager.Instance : FindFirstObjectByType<PlayerStateManager>();
    }

    private void PopulateFromRuntime()
    {
        if (playerStateManager == null)
            playerStateManager = PlayerStateManager.Instance;
        if (playerContext == null)
            playerContext = PlayerContext.Instance;

        if (playerStateManager != null && playerStateManager.state != null)
        {
            playerLevel = playerStateManager.state.level;
            if (!string.IsNullOrWhiteSpace(playerStateManager.state.currentRegionId))
                regionId = playerStateManager.state.currentRegionId;
            if (!string.IsNullOrWhiteSpace(playerStateManager.state.currentRegionName))
                regionName = playerStateManager.state.currentRegionName;
        }

        if (playerContext != null)
        {
            if (!string.IsNullOrWhiteSpace(playerContext.SemanticRegionId))
                regionId = playerContext.SemanticRegionId;
            if (!string.IsNullOrWhiteSpace(playerContext.SemanticRegionName))
                regionName = playerContext.SemanticRegionName;
        }
    }

    private string BuildSnapshotInternal(ThreatSnapshot threat)
    {
        string movementPattern = "Idle";
        if (_lastSpeed >= travelSpeedThreshold)
            movementPattern = "Travel";
        else if (_lastSpeed >= idleSpeedThreshold)
            movementPattern = "Wander";

        string combat = threat.ThreatScore01 >= 0.25f ? "ENGAGED" : "CALM";
        List<string> flags = new List<string>(6);
        if (combat == "CALM")
            flags.Add("LOW_THREAT");
        if (threat.MaxHostileLevelDisparity >= 3)
            flags.Add("HIGH_DISPARITY");
        if (threat.IncomingTargetsCount > 0)
            flags.Add("INCOMING_TARGETS");
        if (threat.Notables.Count > 0)
            flags.Add("NOTABLES_NEARBY");

        StringBuilder sb = new StringBuilder(512);
        sb.Append("{");
        AppendJson(sb, "regId", regionId); sb.Append(",");
        AppendJson(sb, "regName", regionName); sb.Append(",");
        AppendJson(sb, "pLv", playerLevel.ToString()); sb.Append(",");
        AppendJson(sb, "combat", combat); sb.Append(",");
        AppendJson(sb, "mvPat", movementPattern); sb.Append(",");
        AppendJson(sb, "mvSpd", _lastSpeed.ToString("0.0")); sb.Append(",");
        AppendJson(sb, "mvTurn", _lastTurn.ToString("0.00")); sb.Append(",");
        AppendJson(sb, "thr", threat.ThreatScore01.ToString("0.00")); sb.Append(",");
        AppendJson(sb, "disMax", threat.MaxHostileLevelDisparity.ToString()); sb.Append(",");
        AppendJson(sb, "inTarN", threat.IncomingTargetsCount.ToString()); sb.Append(",");

        sb.Append("\"sf\":[");
        for (int i = 0; i < flags.Count; i++)
        {
            if (i > 0) sb.Append(",");
            AppendJsonString(sb, flags[i]);
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
        sb.Append("]}");
        return sb.ToString();
    }

    private ThreatSnapshot BuildThreatSnapshot(Vector3 origin)
    {
        _threats.Clear();
        _notables.Clear();

        int count = Physics.OverlapSphereNonAlloc(origin, threatRadius, _overlapBuffer, hostileMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider collider = _overlapBuffer[i];
            if (collider == null)
                continue;

            EntityInfo info = collider.GetComponentInParent<EntityInfo>();
            if (info == null)
                continue;
            if (player != null && (info.transform == player || info.transform.IsChildOf(player)))
                continue;

            if (requireLineOfSight && !HasLineOfSight(origin + Vector3.up * 1.2f, info.transform.position + Vector3.up, info.transform))
                continue;

            if (info.IsHostile)
            {
                if (!_threats.Contains(info))
                    _threats.Add(info);
            }
            else if (info.isNotable)
            {
                if (!_notables.Contains(info))
                    _notables.Add(info);
            }
        }

        _threats.Sort((a, b) => Vector3.SqrMagnitude(a.transform.position - origin).CompareTo(Vector3.SqrMagnitude(b.transform.position - origin)));
        _notables.Sort((a, b) => Vector3.SqrMagnitude(a.transform.position - origin).CompareTo(Vector3.SqrMagnitude(b.transform.position - origin)));

        ThreatSnapshot snapshot = new ThreatSnapshot();
        int threatTake = Mathf.Min(maxThreats, _threats.Count);
        int notableTake = Mathf.Min(maxNotables, _notables.Count);
        float threatScore = 0f;
        int incoming = 0;
        int maxDisparity = 0;

        for (int i = 0; i < threatTake; i++)
        {
            EntityInfo info = _threats[i];
            if (info == null)
                continue;

            float distance = Vector3.Distance(origin, info.transform.position);
            float distanceFactor = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, threatRadius));
            float levelFactor = Mathf.Clamp01((info.level - playerLevel + 4f) / 8f);
            float contribution = 0.2f + distanceFactor * 0.45f + levelFactor * 0.35f;
            threatScore += contribution;

            if (info.targetingPlayer)
                incoming++;

            maxDisparity = Mathf.Max(maxDisparity, Mathf.Max(0, info.level - playerLevel));
            snapshot.Hostiles.Add(info.displayName + "#" + info.level + "@" + distance.ToString("0.0"));
        }

        for (int i = 0; i < notableTake; i++)
        {
            EntityInfo info = _notables[i];
            if (info == null)
                continue;
            float distance = Vector3.Distance(origin, info.transform.position);
            snapshot.Notables.Add(info.displayName + "@" + distance.ToString("0.0"));
        }

        snapshot.ThreatScore01 = Mathf.Clamp01(threatScore / 3f);
        snapshot.IncomingTargetsCount = incoming;
        snapshot.MaxHostileLevelDisparity = maxDisparity;
        return snapshot;
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to, Transform target)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
            return true;

        direction /= distance;
        if (!Physics.Raycast(from, direction, out RaycastHit hit, distance, occluderMask, QueryTriggerInteraction.Ignore))
            return true;

        return hit.transform == target || hit.transform.IsChildOf(target);
    }

    private static void AppendJson(StringBuilder sb, string key, string value)
    {
        sb.Append('"').Append(key).Append("\":");
        AppendJsonString(sb, value);
    }

    private static void AppendJsonString(StringBuilder sb, string value)
    {
        if (value == null)
            value = string.Empty;
        sb.Append('"').Append(value.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
    }

    public static SituationSnapshot Parse(string raw)
    {
        SituationSnapshot snapshot = new SituationSnapshot();
        if (string.IsNullOrWhiteSpace(raw))
            return snapshot;

        try
        {
            JObject obj = JObject.Parse(raw);
            snapshot.combat = (obj.Value<string>("combat") ?? "CALM").Trim().ToUpperInvariant();
            snapshot.incomingTargets = ParseInt(obj["inTarN"]);
            JArray sf = obj["sf"] as JArray;
            if (sf != null)
            {
                for (int i = 0; i < sf.Count; i++)
                {
                    string flag = (sf[i]?.ToString() ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(flag))
                        snapshot.flags.Add(flag);
                }
            }
        }
        catch { }

        return snapshot;
    }

    private static int ParseInt(JToken token)
    {
        if (token == null)
            return 0;
        if (token.Type == JTokenType.Integer)
            return token.Value<int>();
        if (int.TryParse(token.ToString(), out int value))
            return value;
        return 0;
    }
}

public sealed class SituationSnapshot
{
    public string combat = "CALM";
    public int incomingTargets = 0;
    public readonly List<string> flags = new List<string>();

    public static SituationSnapshot Parse(string raw)
    {
        return SituationSnapshotBuilder.Parse(raw);
    }
}

public sealed class ThreatSnapshot
{
    public float ThreatScore01;
    public int MaxHostileLevelDisparity;
    public int IncomingTargetsCount;
    public readonly List<string> Hostiles = new List<string>();
    public readonly List<string> Notables = new List<string>();
}
