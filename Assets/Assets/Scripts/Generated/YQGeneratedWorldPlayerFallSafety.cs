using UnityEngine;
using Unity.Profiling;

[DisallowMultipleComponent]
[DefaultExecutionOrder(300)]
public sealed class YQGeneratedWorldPlayerFallSafety : MonoBehaviour
{
    private static readonly ProfilerMarker G08LateUpdateMarker = new ProfilerMarker("G08FrameCost.YQGeneratedWorldPlayerFallSafety.LateUpdate()");
    private Terrain generatedTerrain;
    private Vector3 lastSafeGroundedPosition;
    private Vector3 acceptedSpawnPosition;
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
        safety.acceptedSpawnPosition = player.transform.position;
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
        Terrain terrainAtPosition = ResolveTerrainAtPosition(position);
        float terrainHeight = terrainAtPosition != null && terrainAtPosition.terrainData != null
            ? YQGeneratedWorldTerrain.SampleWorldHeight(terrainAtPosition, position) : float.NaN;
        if (Physics.Raycast(
                position + Vector3.up * 0.4f,
                Vector3.down,
                out RaycastHit hit,
                2.4f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore) &&
            !YQPhysicalChunkAdmissionBoundary.IsOwned(hit.collider) && Vector3.Dot(hit.normal, Vector3.up) >= 0.45f)
        {
            // note: A reviewed transition interior's real floor is valid support below outdoor terrain; only unsupported penetration remains a fall-through.
            if (YQOriginSitePortal.IsInsideReviewedInterior(position))
            {
                lastSafeGroundedPosition = position;
                hasSafePosition = true;
                return;
            }
            // note: A lower stray collider must not replace the last verified outdoor position after terrain penetration.
            if (!float.IsNaN(terrainHeight) && position.y >= terrainHeight - .5f)
            {
                lastSafeGroundedPosition = position;
                hasSafePosition = true;
            }
        }

        if (!hasSafePosition || generatedTerrain == null || generatedTerrain.terrainData == null)
            return;

        // note: Missing terrain alone is not a hole. Below the canonical terrain's minimum vertical domain, however, an unsupported player cannot keep falling through an unpublished tile indefinitely.
        bool belowWorldFloor = float.IsNaN(terrainHeight) && position.y < generatedTerrain.GetPosition().y - 6f;
        bool belowTerrain = !float.IsNaN(terrainHeight) && position.y < terrainHeight - 6f;
        bool catastrophicDrop = position.y < lastSafeGroundedPosition.y - 28f;
        if (catastrophicDrop && !belowTerrain && !belowWorldFloor)
        {
            // note: A stale grounded height can be far above a lower neighboring cell; record the old false-positive case without moving the player.
            historicalDropWithoutPenetrationCount++;
        }
        if (!belowTerrain && !belowWorldFloor)
            return;

        // note: Recover confirmed terrain penetration or an unsupported fall below the world's minimum domain; a historical height delta alone is not a hole.
        recoveryCount++;
        lastRecoveryPosition = position;
        lastRecoveryTerrainHeight = belowWorldFloor ? generatedTerrain.GetPosition().y : terrainHeight;
        lastRecoveryPenetrationDepth = lastRecoveryTerrainHeight - position.y;
        Vector3 recovery = lastSafeGroundedPosition;
        Terrain recoveryTerrain = ResolveTerrainAtPosition(recovery);
        if (recoveryTerrain == null)
        {
            // note: A previous streamed tile may have unloaded; the validated initial spawn remains the fallback inside the retained tutorial terrain.
            recovery = acceptedSpawnPosition;
            recoveryTerrain = ResolveTerrainAtPosition(recovery);
        }
        if (recoveryTerrain != null && !YQOriginSitePortal.IsInsideReviewedInterior(recovery))
            recovery.y = Mathf.Max(recovery.y, YQGeneratedWorldTerrain.SampleWorldHeight(recoveryTerrain, recovery));
        Restore(recovery + Vector3.up * 0.55f);
        Debug.LogError(
            "[WORLDGEN ERROR] Player fall-through recovered. " +
            "InvalidPosition=" + position +
            ", terrainHeight=" + lastRecoveryTerrainHeight.ToString("F2") +
            ", terrainOwner=" + (terrainAtPosition != null ? terrainAtPosition.name : "none") +
            ", missingTerrain=" + belowWorldFloor +
            ", penetrationDepth=" + lastRecoveryPenetrationDepth.ToString("F2") +
            ", historicalDrop=" + (lastSafeGroundedPosition.y - position.y).ToString("F2") +
            ", restored=" + transform.position + ".");
    }

    private void Restore(Vector3 position)
    {
        // note: Stop the existing skeletal simulation before translating its parent, and recover the capsule's original enabled state.
        GetComponent<YQPlayerRagdoll>()?.ResetForTerrainRecovery();
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
        GetComponent<YQInvestorPlayerMotor>()?.ResetAfterFallRecovery();
    }

    private Terrain ResolveTerrainAtPosition(Vector3 position)
    {
        // note: Only the streamer's published collider pair or same-authority safety floor can establish penetration; a hidden pending tile may have a different sampled height.
        YQPlayerFollowingSemanticChunkStreamer streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        if (streamer != null && streamer.isActiveAndEnabled)
            return streamer.TryGetTraversalTerrainAt(position, out Terrain published) ? published : null;

        // note: Non-streamed scenes retain their existing discovery fallback, restricted to live matching collider/data pairs.
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
        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null || !collider.enabled || collider.isTrigger ||
            collider.terrainData != terrain.terrainData || !terrain.gameObject.activeInHierarchy)
            return false;
        Vector3 origin = terrain.GetPosition();
        Vector3 size = terrain.terrainData.size;
        return position.x >= origin.x && position.x <= origin.x + size.x &&
               position.z >= origin.z && position.z <= origin.z + size.z;
    }
}
