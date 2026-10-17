using UnityEngine;

// note: One imported clip/controller per Resources descriptor lets the registry warm exact native dependencies before loading the selected prefab.
public sealed class YQRuntimePrefabDependency : ScriptableObject
{
    public Object asset;
    public string sourceIdentity;
}
