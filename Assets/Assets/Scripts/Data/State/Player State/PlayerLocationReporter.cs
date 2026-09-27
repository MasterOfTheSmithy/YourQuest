using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLocationReporter : MonoBehaviour
{
    public float reportEverySeconds = 2f;
    public float regionCellSize = 20f;

    private float nextTime;

    private void Update()
    {
        if (Time.time < nextTime) return;
        nextTime = Time.time + reportEverySeconds;

        var psm = PlayerStateManager.Instance;
        if (psm == null) return;

        string scene = SceneManager.GetActiveScene().name;

        // Prefer semantic region (RegionVolume -> PlayerContext), fallback to grid id
        string region = GetBestRegionId(transform.position);

        psm.SetLocation(scene, region, transform.position);
        WorldStateManager.Instance?.SetCurrentRegion(region);
    }

    private string GetBestRegionId(Vector3 pos)
    {
        string semanticRegionId = PlayerContext.Instance != null ? PlayerContext.Instance.SemanticRegionId : string.Empty;
        if (IsKnownRegionId(semanticRegionId))
            return semanticRegionId.Trim();

        WorldState world = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        if (IsKnownRegionId(world != null ? world.currentRegionId : string.Empty))
            return world.currentRegionId.Trim();

        string computedRegionId = ComputeRegionId(pos);
        // note: A transient grid coordinate is not an accepted identity, so unknown positions remain safely unassigned.
        return IsKnownRegionId(computedRegionId) ? computedRegionId : "region_unknown";
    }

    private static bool IsKnownRegionId(string regionId)
    {
        if (string.IsNullOrWhiteSpace(regionId))
            return false;
        if (string.Equals(regionId.Trim(), "region_unknown", System.StringComparison.OrdinalIgnoreCase))
            return true;

        WorldState world = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        if (world == null || world.identityRecords == null)
            return false;

        for (int index = 0; index < world.identityRecords.Count; index++)
        {
            YQEntityIdentityRecord record = world.identityRecords[index];
            if (record != null && record.kind == YQStableEntityKind.Region &&
                string.Equals(record.id, regionId.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private string ComputeRegionId(Vector3 pos)
    {
        float cell = Mathf.Max(0.01f, regionCellSize);
        int cx = Mathf.FloorToInt(pos.x / cell);
        int cz = Mathf.FloorToInt(pos.z / cell);
        return $"x{cx}_z{cz}";
    }
}
