using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ThreatContact
{
    public string id;
    public string name;
    public int level;
    public string faction;
    public bool hostile;
    public float distance;
    public bool hasLos;
    public bool targetingPlayer;
    public float score;
    public bool notable;
}

/// <summary>
/// Computes nearby entity contacts + a threat score.
/// Optimized for low GC and stable frame-time (console-ish targets).
/// </summary>
public class ThreatProbe : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;

    [Header("Scanning")]
    public float radius = 30f;
    public int maxThreats = 8;
    public int maxNotables = 5;

    [Tooltip("Layers that include NPCs/enemies with colliders.")]
    public LayerMask entityMask = ~0;

    [Tooltip("Layers that block line-of-sight (walls, terrain).")]
    public LayerMask occluderMask = ~0;

    [Tooltip("Max colliders to consider per tick (NonAlloc buffer size).")]
    public int maxColliders = 64;

    [Header("LOS Optimization")]
    [Tooltip("Only do LOS checks for entities closer than this distance, OR hostile/notable.")]
    public float losDistanceCutoff = 25f;

    [Tooltip("Max LOS raycasts per tick (hard cap).")]
    public int maxLosChecksPerTick = 12;

    [Header("Scoring Weights")]
    [Range(0f, 2f)] public float wHostile = 1.0f;
    [Range(0f, 2f)] public float wTargeting = 0.8f;
    [Range(0f, 2f)] public float wLevelDisparity = 0.12f; // per level diff
    [Range(0f, 2f)] public float wDistance = 0.02f;       // proximity influence
    [Range(0f, 2f)] public float wLos = 0.2f;

    public List<ThreatContact> Threats { get; private set; } = new List<ThreatContact>(16);
    public List<ThreatContact> Notables { get; private set; } = new List<ThreatContact>(12);

    public float ThreatScore01 { get; private set; }
    public int MaxHostileLevelDisparity { get; private set; }
    public float NearestHostileDistance { get; private set; }

    // NonAlloc physics buffers + caches
    private Collider[] overlapBuffer;
    private readonly HashSet<Transform> seenRoots = new HashSet<Transform>();
    private readonly HashSet<string> incomingSet = new HashSet<string>();

    private void Awake()
    {
        overlapBuffer = new Collider[Mathf.Max(16, maxColliders)];
    }

    public void Tick(int playerLevelGuess = 1)
    {
        if (playerTransform == null) return;

        Vector3 p = playerTransform.position;

        // Refresh incoming targeting set without allocating a new HashSet
        incomingSet.Clear();
        if (TargetingTracker.Instance != null)
        {
            var incoming = TargetingTracker.Instance.GetIncomingTargetersToPlayer();
            for (int i = 0; i < incoming.Count; i++)
            {
                var id = incoming[i];
                if (!string.IsNullOrWhiteSpace(id))
                    incomingSet.Add(id);
            }
        }

        Threats.Clear();
        Notables.Clear();

        float maxScore = 0f;
        int maxDisp = int.MinValue;
        float nearestHostile = float.MaxValue;

        seenRoots.Clear();

        // NonAlloc overlap
        int hitCount = Physics.OverlapSphereNonAlloc(
            p,
            radius,
            overlapBuffer,
            entityMask,
            QueryTriggerInteraction.Ignore
        );

        // Collect candidates (unique roots with EntityInfo)
        // We also keep a small list for LOS prioritization (hostiles first).
        List<EntityInfo> candidates = new List<EntityInfo>(Mathf.Min(hitCount, 32));

        for (int i = 0; i < hitCount; i++)
        {
            var col = overlapBuffer[i];
            if (col == null) continue;

            var root = col.transform.root;
            if (root == null || seenRoots.Contains(root)) continue;
            seenRoots.Add(root);

            var info = root.GetComponentInChildren<EntityInfo>();
            if (info == null) continue;

            candidates.Add(info);
        }

        // Sort candidates so we do LOS on the most important first
        candidates.Sort((a, b) =>
        {
            // hostiles first, then notables, then nearer
            int ah = a.IsHostile ? 1 : 0;
            int bh = b.IsHostile ? 1 : 0;
            int cmp = bh.CompareTo(ah);
            if (cmp != 0) return cmp;

            int an = a.isNotable ? 1 : 0;
            int bn = b.isNotable ? 1 : 0;
            cmp = bn.CompareTo(an);
            if (cmp != 0) return cmp;

            float da = Vector3.Distance(p, a.transform.position);
            float db = Vector3.Distance(p, b.transform.position);
            return da.CompareTo(db);
        });

        int losChecks = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            var e = candidates[i];
            if (e == null) continue;

            float d = Vector3.Distance(p, e.transform.position);
            bool targetingPlayer = incomingSet.Contains(e.entityId);

            int disparity = e.level - playerLevelGuess;
            if (e.IsHostile)
            {
                if (disparity > maxDisp) maxDisp = disparity;
                if (d < nearestHostile) nearestHostile = d;
            }

            // LOS only when it matters
            bool shouldLos =
                (losChecks < maxLosChecksPerTick) &&
                (e.IsHostile || e.isNotable || d <= losDistanceCutoff);

            bool los = false;
            if (shouldLos)
            {
                losChecks++;
                los = HasLineOfSight(p, e.transform.position);
            }

            float s = 0f;
            if (e.IsHostile) s += wHostile;
            if (targetingPlayer) s += wTargeting;
            s += Mathf.Max(0, disparity) * wLevelDisparity;

            // nearer -> higher score (normalized)
            s += Mathf.Max(0f, (radius - d)) * wDistance / Mathf.Max(1f, radius);

            if (los) s += wLos;

            var c = new ThreatContact
            {
                id = e.entityId,
                name = e.displayName,
                level = e.level,
                faction = e.factionId,
                hostile = e.IsHostile,
                distance = d,
                hasLos = los,
                targetingPlayer = targetingPlayer,
                score = s,
                notable = e.isNotable
            };

            if (e.isNotable)
                Notables.Add(c);

            // qualifies as threat if hostile OR targeting OR big disparity close-by
            bool qualifiesThreat = e.IsHostile || targetingPlayer || (disparity >= 5 && d <= radius * 0.75f);
            if (qualifiesThreat)
                Threats.Add(c);

            if (s > maxScore) maxScore = s;
        }

        Threats.Sort((a, b) => b.score.CompareTo(a.score));
        if (Threats.Count > maxThreats) Threats.RemoveRange(maxThreats, Threats.Count - maxThreats);

        Notables.Sort((a, b) =>
        {
            int cmp = a.distance.CompareTo(b.distance);
            if (cmp != 0) return cmp;
            return b.level.CompareTo(a.level);
        });
        if (Notables.Count > maxNotables) Notables.RemoveRange(maxNotables, Notables.Count - maxNotables);

        ThreatScore01 = 1f - Mathf.Exp(-maxScore); // squash to 0..1
        MaxHostileLevelDisparity = (maxDisp == int.MinValue) ? 0 : maxDisp;
        NearestHostileDistance = (nearestHostile == float.MaxValue) ? -1f : nearestHostile;
    }

    private bool HasLineOfSight(Vector3 fromPlayerPos, Vector3 toEntityPos)
    {
        Vector3 from = fromPlayerPos + Vector3.up * 1.6f;
        Vector3 to = toEntityPos + Vector3.up * 1.2f;
        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        if (dist <= 0.01f) return true;

        dir /= dist;
        return !Physics.Raycast(from, dir, dist, occluderMask, QueryTriggerInteraction.Ignore);
    }
}
