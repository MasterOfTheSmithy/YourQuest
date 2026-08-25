using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQSemanticBenchmarkZoneGizmo : MonoBehaviour
{
    [SerializeField]
    private string zoneLabel = string.Empty;

    [SerializeField]
    private Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);

    [SerializeField]
    private Color color = Color.cyan;

    public void Configure(string newZoneLabel, Bounds newLocalBounds, Color newColor)
    {
        // note: Benchmark zones are editor review evidence only; they do not alter or become parents of authored geometry.
        zoneLabel = newZoneLabel ?? string.Empty;
        localBounds = newLocalBounds;
        color = newColor;
        // note: The proxy object's stable cell name is preserved so on-demand geometry preview can resolve it without parsing presentation text.
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = color;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(localBounds.center, localBounds.size);
        Gizmos.matrix = previous;
    }
}
