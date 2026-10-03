using UnityEngine;
using Unity.Profiling;

[DisallowMultipleComponent]
[DefaultExecutionOrder(300)]
public sealed class YQGeneratedWorldPlayerFallSafety : MonoBehaviour
{
    private static readonly ProfilerMarker G08LateUpdateMarker = new ProfilerMarker("G08FrameCost.YQGeneratedWorldPlayerFallSafety.LateUpdate()");
    private Terrain generatedTerrain;
    private Vector3 lastSafeGroundedPosition;
    private bool hasSafePosition;
    private float nextGroundCheck;
    private int recoveryCount;
    private int historicalDropWithoutPenetrationCount;
    private Vector3 lastRecoveryPosition;
    private float lastRecoveryTerrainHeight;
    private float lastRecoveryPenetrationDepth;

    public int RecoveryCount => recoveryCount;
    public int HistoricalDropWithoutPenetrationCount => historicalDropWithoutPenetrationCount;
    public Vector3 LastRecoveryPosition => lastRecoveryPosition;
    public float LastRecoveryTerrainHeight => lastRecoveryTerrainHeight;
    public float LastRecoveryPenetrationDepth => lastRecoveryPenetrationDepth;

    public static void EnsureInstalled(GameObject player, Terrain terrain)
    {
        if (player == null)
            return;
        YQGeneratedWorldPlayerFallSafety safety =
            player.GetComponent<YQGeneratedWorldPlayerFallSafety>() ??
            player.AddComponent<YQGeneratedWorldPlayerFallSafety>();
        safety.generatedTerrain = terrain;
        safety.lastSafeGroundedPosition = player.transform.position;
        safety.hasSafePosition = true;
    }

    // note: Attribute this project-owned callback during the focused G08 frame-budget witness.
    private void LateUpdate()
    {
        using (G08LateUpdateMarker.Auto())
            LateUpdateCore();
    }

    private void LateUpdateCore()
    {
        if (Time.unscaledTime < nextGroundCheck)
            return;
        nextGroundCheck = Time.unscaledTime + 0.12f;

        Vector3 position = transform.position;
        if (Physics.Raycast(
                position + Vector3.up * 0.4f,
                Vector3.down,
                out RaycastHit hit,
                2.4f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore) &&
            Vector3.Dot(hit.normal, Vector3.up) >= 0.45f)
        {
            lastSafeGroundedPosition = position;
            hasSafePosition = true;
        }

        if (!hasSafePosition || generatedTerrain == null || generatedTerrain.terrainData == null)
            return;

        Terrain terrainAtPosition = ResolveTerrainAtPosition(position);
        // note: If the streamer has not published a tile for this coordinate yet, do not reinterpret the missing surface as a boundary and teleport the player backward.
        if (terrainAtPosition == null || terrainAtPosition.terrainData == null)
            return;
        float terrainHeight = YQGeneratedWorldTerrain.SampleWorldHeight(
            terrainAtPosition,
            position);
        bool belowTerrain = position.y < terrainHeight - 6f;
        bool catastrophicDrop = position.y < lastSafeGroundedPosition.y - 28f;
        if (catastrophicDrop && !belowTerrain)
        {
            // note: A stale grounded height can be far above a lower neighboring cell; record the old false-positive case without moving the player.
            historicalDropWithoutPenetrationCount++;
        }
        if (!belowTerrain)
            return;

        // note: Recover only when the player's current cell confirms deep terrain penetration; a historical height delta alone is not a hole.
        recoveryCount++;
        lastRecoveryPosition = position;
        lastRecoveryTerrainHeight = terrainHeight;
        lastRecoveryPenetrationDepth = terrainHeight - position.y;
        Restore(lastSafeGroundedPosition + Vector3.up * 0.55f);
        Debug.LogError(
            "[WORLDGEN ERROR] Player fall-through recovered. " +
            "InvalidPosition=" + position +
            ", terrainHeight=" + terrainHeight.ToString("F2") +
            ", penetrationDepth=" + lastRecoveryPenetrationDepth.ToString("F2") +
            ", historicalDrop=" + (lastSafeGroundedPosition.y - position.y).ToString("F2") +
            ", restored=" + lastSafeGroundedPosition + ".");
    }

    private void Restore(Vector3 position)
    {
        CharacterController controller = GetComponent<CharacterController>();
        bool controllerEnabled = controller != null && controller.enabled;
        if (controller != null)
            controller.enabled = false;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
        }
        transform.position = position;

        if (controller != null)
            controller.enabled = controllerEnabled;
    }

    private Terrain ResolveTerrainAtPosition(Vector3 position)
    {
        // note: Prefer the authored terrain when it contains the player, then discover the player-following extension tile that owns the current horizontal coordinate.
        if (ContainsWorldPosition(generatedTerrain, position))
            return generatedTerrain;

        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        for (int index = 0; index < terrains.Length; index++)
        {
            Terrain candidate = terrains[index];
            if (ContainsWorldPosition(candidate, position))
                return candidate;
        }
        return null;
    }

    private static bool ContainsWorldPosition(Terrain terrain, Vector3 position)
    {
        if (terrain == null || terrain.terrainData == null)
            return false;
        Vector3 origin = terrain.GetPosition();
        Vector3 size = terrain.terrainData.size;
        return position.x >= origin.x && position.x <= origin.x + size.x &&
               position.z >= origin.z && position.z <= origin.z + size.z;
    }
}
