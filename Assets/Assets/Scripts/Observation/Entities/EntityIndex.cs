using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility: find nearby EntityInfo around a point.
/// Pure helper (no state) so it's swappable.
/// </summary>
public static class EntityIndex
{
    /// <summary>
    /// Returns EntityInfo components near a position, within radius, filtered and capped.
    /// </summary>
    public static List<EntityInfo> FindNearbyEntities(
        Vector3 center,
        float radius,
        LayerMask mask,
        int maxResults = 24,
        bool requireEntityInfo = true)
    {
        var results = new List<EntityInfo>(maxResults);

        Collider[] hits = Physics.OverlapSphere(center, radius, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return results;

        // Deduplicate by Transform root to avoid multiple colliders per entity
        var seen = new HashSet<Transform>();

        for (int i = 0; i < hits.Length && results.Count < maxResults; i++)
        {
            var col = hits[i];
            if (col == null) continue;

            var root = col.transform.root;
            if (root == null || seen.Contains(root)) continue;
            seen.Add(root);

            EntityInfo info = root.GetComponentInChildren<EntityInfo>();
            if (requireEntityInfo && info == null) continue;

            if (info != null)
                results.Add(info);
        }

        return results;
    }

    public static bool HasLineOfSight(Vector3 from, Vector3 to, LayerMask occluderMask)
    {
        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        if (dist <= 0.01f) return true;

        dir /= dist;
        // If ray hits occluder before target position, LOS blocked
        return !Physics.Raycast(from, dir, dist, occluderMask, QueryTriggerInteraction.Ignore);
    }
}
