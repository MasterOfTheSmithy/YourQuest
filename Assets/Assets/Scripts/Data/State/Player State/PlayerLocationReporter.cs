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
        if (PlayerContext.Instance != null && !string.IsNullOrWhiteSpace(PlayerContext.Instance.SemanticRegionId))
            return PlayerContext.Instance.SemanticRegionId;

        return ComputeRegionId(pos);
    }

    private string ComputeRegionId(Vector3 pos)
    {
        float cell = Mathf.Max(0.01f, regionCellSize);
        int cx = Mathf.FloorToInt(pos.x / cell);
        int cz = Mathf.FloorToInt(pos.z / cell);
        return $"x{cx}_z{cz}";
    }
}
