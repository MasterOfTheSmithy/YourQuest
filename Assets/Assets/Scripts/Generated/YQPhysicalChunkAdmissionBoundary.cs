using UnityEngine;

// note: This transient marker distinguishes admission contacts from accepted terrain/site/bridge support; only the exact existing streamer's live collider/epoch binding can qualify it.
[DisallowMultipleComponent]
public sealed class YQPhysicalChunkAdmissionBoundary : MonoBehaviour
{
    private YQPlayerFollowingSemanticChunkStreamer owner;
    private int epoch;
    private Vector2Int cell;
    internal void Bind(YQPlayerFollowingSemanticChunkStreamer authority, int capturedEpoch, Vector2Int coordinate)
    { owner = authority; epoch = capturedEpoch; cell = coordinate; }
    internal static bool IsOwned(Collider collider)
    {
        if (collider == null) return false;
        var marker = collider.GetComponent<YQPhysicalChunkAdmissionBoundary>();
        return marker != null && marker.owner != null && marker.owner.OwnsPhysicsAdmissionCollider(collider, marker.epoch, marker.cell);
    }
}
