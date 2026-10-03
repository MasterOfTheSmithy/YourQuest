using UnityEngine;

// note: A presentation attachment owns only its instantiated parts and restores the prior LOD membership when removed.
public sealed class YQDotModuleAttachment : MonoBehaviour
{
    private GameObject[] _parts;
    private LODGroup _group;
    private LOD[] _previous;
    public void Initialize(GameObject[] parts, LODGroup group, LOD[] previous)
    { _parts = parts; _group = group; _previous = previous; }
    private void OnDestroy()
    {
        if (_group != null && _previous != null) { _group.SetLODs(_previous); _group.RecalculateBounds(); }
        if (_parts == null) return;
        foreach (GameObject part in _parts)
            if (part != null) { part.SetActive(false); if (Application.isPlaying) Destroy(part); else DestroyImmediate(part); }
    }
}
