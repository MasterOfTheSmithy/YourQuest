using UnityEngine;

// note: Derived arm meshes retain the approved body's skinning and materials; they contain no player or equipment state.
public sealed class YQFirstPersonArmsAssets : ScriptableObject
{
    public const string ResourcePath = "Player/YQFirstPersonArms";
    public Mesh[] sourceMeshes;
    public Mesh[] armMeshes;
    public int geometryVersion;
    public bool hasReadyPose;
    public Vector3 rightReadyHand;
    public Vector3 leftReadyHand;

    public Mesh Resolve(Mesh source)
    {
        if (source == null || sourceMeshes == null || armMeshes == null) return null;
        for (int i = 0; i < sourceMeshes.Length && i < armMeshes.Length; i++)
            if (sourceMeshes[i] == source) return armMeshes[i];
        return null;
    }
}
