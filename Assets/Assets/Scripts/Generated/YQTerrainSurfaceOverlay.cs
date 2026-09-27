using UnityEngine;

// note: Terrain-conforming decoration uses its owning terrain's collision; it is never an independent floor or bridge.
public sealed class YQTerrainSurfaceOverlay : MonoBehaviour
{
    public TerrainCollider Support { get; private set; }

    public void Configure(Terrain terrain)
    {
        // note: Record the concrete support owner so validation does not rely on object names or collider-size exceptions.
        Support = terrain != null ? terrain.GetComponent<TerrainCollider>() : null;
    }

    public bool HasValidSupport => Support != null && Support.enabled && !Support.isTrigger &&
        Support.gameObject.activeInHierarchy && Support.terrainData != null && transform.IsChildOf(Support.transform);
}
