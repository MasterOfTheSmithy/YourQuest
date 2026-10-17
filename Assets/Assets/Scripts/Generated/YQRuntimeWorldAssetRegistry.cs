using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public sealed class YQRuntimeWorldMaterialOverride
{
    // Relative Transform path inside the instantiated prefab.
    // Empty means the prefab root itself.
    public string transformPath;

    // Renderer component index on that Transform.
    public int rendererIndex;

    // Material slot on that Renderer.
    public int materialIndex;

    // Serialized replacement material for runtime-safe repair.
    public Material replacementMaterial;
}

[Serializable]
public sealed class YQRuntimeWorldAssetEntry
{
    public string assetPath;
    public GameObject prefab;
    public Material material;

    public List<YQRuntimeWorldMaterialOverride> materialOverrides =
        new List<YQRuntimeWorldMaterialOverride>();
}

[CreateAssetMenu(
    fileName = "YQRuntimeWorldAssetRegistry",
    menuName = "YourQuest/Runtime World Asset Registry")]
public sealed class YQRuntimeWorldAssetRegistry : ScriptableObject
{
    private const string ResourcesAssetName =
        "YQRuntimeWorldAssetRegistry";

    [SerializeField]
    private List<YQRuntimeWorldAssetEntry> entries =
        new List<YQRuntimeWorldAssetEntry>();

    [SerializeField]
    private bool useLazyResourceShards;
    [SerializeField] private bool useGranularResourceShards;

    [Serializable] private sealed class GranularManifest
    {
        public int version;
        public List<GranularAsset> assets;
    }
    [Serializable] private sealed class GranularAsset
    {
        public string path;
        public string resource;
        public string entrySha256;
    }
    [Serializable] private sealed class PrefabDependencyManifest
    {
        public int version;
        public List<PrefabDependencySet> prefabs;
    }
    [Serializable] private sealed class PrefabDependencySet
    {
        public string path;
        public string entrySha256;
        public List<string> resources;
    }
    private Dictionary<string, GranularAsset> _granularAssets;
    private Dictionary<string, List<YQRuntimeWorldAssetEntry>> _granularPacks;
    [NonSerialized] private bool _granularLookupAttempted;
    private Dictionary<string, PrefabDependencySet> _prefabDependencies;
    private bool _dependencyLookupAttempted;
    private bool _loggedMissingDependencyMetadata;
    // note: Approved alpha-cutout grass is shared by native terrain detail batches without duplicating imported textures.
    [SerializeField] private Texture2D terrainGrassTexture;
    public Texture2D TerrainGrassTexture
    {
        get
        {
            // note: Recover the approved grass texture from the curated grass prefab when older registry shards have no serialized texture reference.
            if (terrainGrassTexture == null)
                terrainGrassTexture = ResolveTerrainGrassTextureFallback();
            return terrainGrassTexture;
        }
    }

    private Dictionary<string, GameObject> _prefabsByPath;
    private Dictionary<string, Material> _materialsByPath;
    private Dictionary<string, YQRuntimeWorldAssetEntry> _entriesByPath;
    private Dictionary<string, YQRuntimeWorldAssetRegistry> _loadedShards;
    private readonly Dictionary<string, float> _shardLastUse = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, YQRuntimePrefabDependency> _loadedDependencies =
        new Dictionary<string, YQRuntimePrefabDependency>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ResourceRequest> _pendingPreloads =
        new Dictionary<string, ResourceRequest>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _resourcePins =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private int _residencyEpoch;
    private int _activePreloads;
    private HashSet<string> _missingShardPaths;

    private static YQRuntimeWorldAssetRegistry _instance;

    private static bool _loggedRuntimeSummary;

    private static bool _loggedRegistryLoadFailure;

    // note: Inspect each source prefab once per runtime session, including editor-only recovery paths.
    private static readonly Dictionary<GameObject, bool> PrefabScriptValidity =
        new Dictionary<GameObject, bool>();

    public IReadOnlyList<YQRuntimeWorldAssetEntry> Entries
    {
        get
        {
            return entries;
        }
    }

    /*
     * This runs even when Enter Play Mode Options has Domain Reload
     * disabled. It prevents an editor-time/stale ScriptableObject
     * instance from carrying into runtime.
     */
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeStatics()
    {
        // note: Domain-reload-disabled Play must invalidate leases/callbacks on the previously loaded ScriptableObject as well as its global pointer.
        ClearCachedInstance();
    }

    public static YQRuntimeWorldAssetRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance =
                    LoadBestRuntimeRegistry();

                if (_instance == null)
                {
                    _instance =
                        CreateEmptyRuntimeFallbackRegistry();
                }

                if (_instance != null)
                {
                    // note: Resources loading invokes OnEnable, so reuse its dictionaries instead of rebuilding the entire root registry twice on the loading screen.
                    _instance.EnsureLookup();
                    _instance.LogRuntimeSummaryOnce();
                }
            }

            return _instance;
        }
    }

    public static bool IsAvailable
    {
        get
        {
            return Instance != null;
        }
    }

    public bool UsesLazyResourceShards =>
        useLazyResourceShards;
    public bool UsesGranularResourceShards => useGranularResourceShards;

#if UNITY_EDITOR
    // note: An opt-in resource census reads the existing session cache without calling Instance, loading shards, or changing their lifetime.
    internal static YQRuntimeWorldAssetRegistry CachedInstanceForDiagnostics => _instance;
    internal IReadOnlyDictionary<string, YQRuntimeWorldAssetRegistry> CachedShardsForDiagnostics => _loadedShards;
#endif

    public IReadOnlyList<YQRuntimeWorldAssetEntry> GetEntriesForAssetPath(
        string assetPath)
    {
        EnsureLookup();

        // note: Catalog enumeration reads only paths; resolving a selected entry loads its own existing prefab/material bindings.
        if (useGranularResourceShards)
        {
            EnsureGranularLookup();
            return _granularPacks != null && _granularPacks.TryGetValue(BuildShardResourcePath(assetPath), out var pack)
                ? pack : Array.Empty<YQRuntimeWorldAssetEntry>();
        }
        if (!useLazyResourceShards)
            return entries;

        if (TryGetLazyShard(
                assetPath,
                out YQRuntimeWorldAssetRegistry shard) &&
            shard != null)
        {
            // note: Semantic selectors enumerate only the requested pack shard instead of forcing every imported library into memory.
            return shard.Entries;
        }

        return entries;
    }

    public YQRuntimeWorldAssetEntry ResolveEntry(string assetPath)
    {
        // note: The same registry remains binding authority in both granular and legacy layouts; metadata enumeration never implies residency.
        EnsureLookup();
        string key = NormalizePath(assetPath);
        if (_entriesByPath.TryGetValue(key, out var entry)) return entry;
        return TryGetLazyShard(key, out var shard) && shard != null ? shard.ResolveEntry(key) : null;
    }

    public void SetGranularResourceShards(bool enabled)
    {
        useGranularResourceShards = enabled;
        _granularAssets = null;
        _granularPacks = null;
        _granularLookupAttempted = false;
        _loadedShards = null;
        _missingShardPaths = null;
        ResetDependencyResidency();
    }

    private void EnsureGranularLookup()
    {
        if (_granularLookupAttempted) return;
        _granularLookupAttempted = true;
        // note: This manifest contains strings only. It cannot drag a whole imported pack into RAM/VRAM through serialized object references.
        TextAsset text = Resources.Load<TextAsset>("YQWorldAssetManifest");
        GranularManifest manifest = text != null ? JsonUtility.FromJson<GranularManifest>(text.text) : null;
        if (manifest == null || manifest.version != 1 || manifest.assets == null)
        {
            Debug.LogError("[YQRuntimeWorldAssetRegistry] Granular resource manifest missing or unsupported; rebuild the asset bindings.");
            return;
        }
        var assets = new Dictionary<string, GranularAsset>(StringComparer.OrdinalIgnoreCase);
        var packs = new Dictionary<string, List<YQRuntimeWorldAssetEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (GranularAsset item in manifest.assets)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.path) || string.IsNullOrWhiteSpace(item.resource) ||
                !item.path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !item.resource.StartsWith("YQWorldAssetLeaves/", StringComparison.Ordinal) || item.resource.Contains("..") ||
                assets.ContainsKey(item.path))
            {
                Debug.LogError("[YQRuntimeWorldAssetRegistry] Invalid granular resource manifest; no legacy bulk-load fallback was attempted.");
                return;
            }
            assets.Add(item.path, item);
            string pack = BuildShardResourcePath(item.path);
            if (!packs.TryGetValue(pack, out var list)) packs.Add(pack, list = new List<YQRuntimeWorldAssetEntry>());
            list.Add(new YQRuntimeWorldAssetEntry { assetPath = item.path });
        }
        _granularAssets = assets;
        _granularPacks = packs;
    }

    private string ResolveResourcePath(string assetPath)
    {
        if (!useGranularResourceShards) return BuildShardResourcePath(assetPath);
        EnsureGranularLookup();
        return _granularAssets != null && _granularAssets.TryGetValue(NormalizePath(assetPath), out var item)
            ? item.resource : string.Empty;
    }

    private bool TryGetPrefabDependencies(string assetPath, out PrefabDependencySet dependencies)
    {
        dependencies = null;
        if (!useGranularResourceShards) return false;
        EnsureGranularLookup();
        if (!_dependencyLookupAttempted)
        {
            _dependencyLookupAttempted = true;
            // note: Dependency discovery belongs to the explicit Editor builder. Runtime reads strings before loading any prefab/controller.
            PrefabDependencyManifest manifest = null;
            try
            {
                TextAsset text = Resources.Load<TextAsset>("YQWorldPrefabDependencies");
                manifest = text != null ? JsonUtility.FromJson<PrefabDependencyManifest>(text.text) : null;
            }
            catch (Exception error)
            {
                // note: Optional preparation metadata cannot take down an accepted world. Keep legacy loading explicit and unqualified until the local rebuild succeeds.
                _loggedMissingDependencyMetadata = true;
                Debug.LogWarning("[YQRuntimeWorldAssetRegistry] Prefab dependency metadata could not be read; cold activation remains unqualified. " +
                    "Run YourQuest > Assets > Build Prefab Dependency Preloads. " + error.Message);
            }
            if (manifest != null && manifest.version == 1 && manifest.prefabs != null)
            {
                var lookup = new Dictionary<string, PrefabDependencySet>(StringComparer.OrdinalIgnoreCase);
                bool valid = true;
                foreach (PrefabDependencySet item in manifest.prefabs)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.path) || string.IsNullOrWhiteSpace(item.entrySha256) ||
                        item.resources == null || lookup.ContainsKey(item.path))
                    {
                        valid = false;
                        break;
                    }
                    foreach (string resource in item.resources)
                    {
                        if (string.IsNullOrWhiteSpace(resource) || !resource.StartsWith("YQWorldAssetDependencies/", StringComparison.Ordinal) ||
                            resource.Contains("..") || resource.Contains('\\'))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (!valid) break;
                    lookup.Add(item.path, item);
                }
                if (valid) _prefabDependencies = lookup;
            }
        }

        string key = NormalizePath(assetPath);
        // note: A binding rebuild invalidates only changed dependency rows; stale metadata never claims that a newly selected binding is warm.
        return _granularAssets != null && _granularAssets.TryGetValue(key, out GranularAsset binding) &&
               _prefabDependencies != null && _prefabDependencies.TryGetValue(key, out dependencies) &&
               string.Equals(binding.entrySha256, dependencies.entrySha256, StringComparison.Ordinal);
    }

    public bool HasPreparedPrefabDependencies(string assetPath)
    {
        if (!TryGetPrefabDependencies(assetPath, out PrefabDependencySet dependencies)) return false;
        foreach (string resource in dependencies.resources)
            if (!_loadedDependencies.TryGetValue(resource, out YQRuntimePrefabDependency descriptor) ||
                descriptor == null || descriptor.asset == null) return false;
        string prefabResource = ResolveResourcePath(assetPath);
        return _loadedShards != null && _loadedShards.TryGetValue(prefabResource, out var shard) && shard != null;
    }

    public sealed class AssetResidencyLease : IDisposable
    {
        private YQRuntimeWorldAssetRegistry _owner;
        private readonly string[] _resources;
        private readonly int _epoch;

        internal AssetResidencyLease(YQRuntimeWorldAssetRegistry owner, string[] resources, int epoch)
        {
            _owner = owner;
            _resources = resources;
            _epoch = epoch;
        }

        public void Dispose()
        {
            // note: Cancellation and successful publication share idempotent release; a lease from an old session cannot unpin the new one.
            YQRuntimeWorldAssetRegistry owner = _owner;
            _owner = null;
            if (ReferenceEquals(owner, null) || owner._residencyEpoch != _epoch) return;
            foreach (string resource in _resources)
            {
                if (!owner._resourcePins.TryGetValue(resource, out int count)) continue;
                if (count <= 1) owner._resourcePins.Remove(resource);
                else owner._resourcePins[resource] = count - 1;
            }
        }
    }

    public AssetResidencyLease AcquireAssetResidency(IEnumerable<string> assetPaths)
    {
        // note: The existing materialization iterator owns this lease through preparation/activation; registry eviction respects pending demand.
        var resources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (assetPaths != null)
        {
            foreach (string assetPath in assetPaths)
            {
                string resource = ResolveResourcePath(assetPath);
                if (!string.IsNullOrWhiteSpace(resource)) resources.Add(resource);
                if (TryGetPrefabDependencies(assetPath, out PrefabDependencySet dependencies))
                    foreach (string dependency in dependencies.resources) resources.Add(dependency);
            }
        }
        foreach (string resource in resources)
        {
            _resourcePins.TryGetValue(resource, out int count);
            _resourcePins[resource] = count + 1;
        }
        string[] retained = new string[resources.Count];
        resources.CopyTo(retained);
        return new AssetResidencyLease(this, retained, _residencyEpoch);
    }

    public GameObject ResolvePreloadedPrefab(string assetPath)
    {
        // note: Streamed callers resolve only their completed request. Editor AssetDatabase recovery must not hide a missing player binding.
        EnsureLookup();
        string key = NormalizePath(assetPath);
        if (_prefabsByPath.TryGetValue(key, out GameObject direct) && direct != null && HasValidPrefabScripts(direct, key)) return direct;
        string resource = ResolveResourcePath(key);
        if (_loadedShards == null || !_loadedShards.TryGetValue(resource, out YQRuntimeWorldAssetRegistry shard) || shard == null) return null;
        shard.EnsureLookup();
        _shardLastUse[resource] = Time.realtimeSinceStartup;
        return shard._prefabsByPath.TryGetValue(key, out GameObject prefab) && prefab != null && HasValidPrefabScripts(prefab, key)
            ? prefab : null;
    }

    private Texture2D ResolveTerrainGrassTextureFallback()
    {
        // note: Use an existing imported grass mesh as the runtime source so builds do not synthesize or hardcode a replacement visual asset.
        const string approvedGrassPrefab =
            "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_GrassMesh.prefab";
        GameObject grassPrefab = ResolvePrefab(approvedGrassPrefab);
        if (grassPrefab == null)
            return null;

        Renderer[] renderers = grassPrefab.GetComponentsInChildren<Renderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null)
                continue;

            Material material = renderer.sharedMaterial;
            Texture2D texture = null;
            if (material != null)
            {
                // note: URP/HDRP imported vegetation commonly stores its albedo in _BaseTexture rather than Unity's legacy mainTexture slot.
                texture = material.GetTexture("_BaseTexture") as Texture2D;
                if (texture == null)
                    texture = material.mainTexture as Texture2D;
            }
            if (texture != null)
                return texture;
        }

        return null;
    }

    public GameObject ResolvePrefab(
        string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        EnsureLookup();

        string key =
            NormalizePath(assetPath);

        if (string.IsNullOrWhiteSpace(key))
            return null;

#if UNITY_EDITOR
        string preferredKey =
            BuildEditorPreferredUrpAssetPath(
                key);

        if (!string.Equals(
                preferredKey,
                key,
                StringComparison.OrdinalIgnoreCase))
        {
            if (_prefabsByPath.TryGetValue(
                    preferredKey,
                    out GameObject preferredPrefab) &&
                preferredPrefab != null &&
                HasValidPrefabScripts(preferredPrefab, preferredKey))
            {
                // note: Prefer imported URP variants over HDRP(Default) paths to preserve material/shader compatibility in Play Mode.
                return preferredPrefab;
            }

            preferredPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    preferredKey);

            if (preferredPrefab != null && HasValidPrefabScripts(preferredPrefab, preferredKey))
                return preferredPrefab;
        }
#endif

        if (_prefabsByPath.TryGetValue(
                key,
                out GameObject prefab) &&
            prefab != null && HasValidPrefabScripts(prefab, key))
        {
            return prefab;
        }

        if (TryGetLazyShard(
                key,
                out YQRuntimeWorldAssetRegistry shard))
        {
            // note: Only the pack containing the requested asset is deserialized; unrelated genre palettes remain unloaded.
            prefab =
                shard.ResolvePrefab(
                    key);

            if (prefab != null)
                return prefab;
        }

#if UNITY_EDITOR
        // note: Editor play mode can recover stale serialized registry references from the real imported path.
        prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                key);

        if (prefab != null && HasValidPrefabScripts(prefab, key))
            return prefab;
#endif

        return null;
    }

    private static bool HasValidPrefabScripts(GameObject prefab, string assetPath)
    {
        if (prefab == null)
            return false;
        if (PrefabScriptValidity.TryGetValue(prefab, out bool valid))
            return valid;

        // note: All resolve paths share this gate so direct asynchronous spawns cannot bypass registry script safety.
        MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                continue;
            PrefabScriptValidity[prefab] = false;
            Debug.LogWarning("[YQRuntimeWorldAssetRegistry] Rejected prefab with missing script reference: " + assetPath, prefab);
            return false;
        }

        PrefabScriptValidity[prefab] = true;
        return true;
    }

#if UNITY_EDITOR
    private static string BuildEditorPreferredUrpAssetPath(
        string key)
    {
        if (string.IsNullOrWhiteSpace(
                key))
        {
            return key;
        }

        string preferred =
            key.Replace(
                "/HDRP(Default)/",
                "/URP/",
                StringComparison.OrdinalIgnoreCase);

        preferred =
            preferred.Replace(
                "/HDRP (Default)/",
                "/URP/",
                StringComparison.OrdinalIgnoreCase);

        preferred =
            preferred.Replace(
                "/HDRP/",
                "/URP/",
                StringComparison.OrdinalIgnoreCase);

        return preferred;
    }
#endif

    public Material ResolveMaterial(
        string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        EnsureLookup();

        string key =
            NormalizePath(assetPath);

        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (_materialsByPath.TryGetValue(
                key,
                out Material material) &&
            material != null)
        {
            return material;
        }

        if (TryGetLazyShard(
                key,
                out YQRuntimeWorldAssetRegistry shard))
        {
            // note: Material packs follow the same lazy boundary as prefabs so terrain binding cannot load the complete library.
            material =
                shard.ResolveMaterial(
                    key);

            if (material != null)
                return material;
        }

#if UNITY_EDITOR
        string preferredKey =
            BuildEditorPreferredUrpAssetPath(
                key);

        if (!string.Equals(
                preferredKey,
                key,
                StringComparison.OrdinalIgnoreCase))
        {
            if (_materialsByPath.TryGetValue(
                    preferredKey,
                    out material) &&
                material != null)
            {
                // note: A stale HDRP material reference must resolve to the matching URP material before runtime binding.
                return material;
            }

            material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    preferredKey);

            if (material != null)
                return material;
        }
#endif

#if UNITY_EDITOR
        // note: Editor play mode can recover stale serialized registry references from the real imported path.
        material =
            AssetDatabase.LoadAssetAtPath<Material>(
                key);

        if (material != null)
            return material;
#endif

        return null;
    }

    // note: Let simple generated geometry reuse the registry's baked first material slot without instantiating and repairing a full imported prefab.
    internal bool TryResolveFirstRuntimeMaterial(
        string assetPath,
        out Material material)
    {
        material = null;
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        EnsureLookup();
        string key = NormalizePath(assetPath);
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (TryGetLazyShard(key, out YQRuntimeWorldAssetRegistry shard) && shard != null)
            return shard.TryResolveFirstRuntimeMaterial(key, out material);

        if (_entriesByPath == null ||
            !_entriesByPath.TryGetValue(key, out YQRuntimeWorldAssetEntry entry) ||
            entry == null)
            return false;

        if (entry.material != null)
        {
            material = entry.material;
            return true;
        }

        if (entry.materialOverrides == null)
            return false;
        for (int index = 0; index < entry.materialOverrides.Count; index++)
        {
            YQRuntimeWorldMaterialOverride binding = entry.materialOverrides[index];
            if (binding == null || binding.replacementMaterial == null)
                continue;
            material = binding.replacementMaterial;
            return true;
        }
        return false;
    }

    public int ApplyMaterialOverrides(
        string assetPath,
        GameObject instance,
        bool repairHierarchy = true)
    {
        if (instance == null ||
            string.IsNullOrWhiteSpace(assetPath))
        {
            return 0;
        }

        EnsureLookup();

        string key =
            NormalizePath(assetPath);

        if (string.IsNullOrWhiteSpace(key))
            return 0;

        if (TryGetLazyShard(
                key,
                out YQRuntimeWorldAssetRegistry shard))
        {
            // note: Persisted renderer-slot repairs are keyed by the authored palette path; rewriting an HDRP key to an empty URP sibling previously bypassed both the shard binding and emergency hierarchy conversion.
            int shardApplied = shard.ApplyMaterialOverrides(key, instance, repairHierarchy);
            SanitizeRuntimeLodGroups(instance);
            return shardApplied;
        }

        if (_entriesByPath == null ||
            !_entriesByPath.TryGetValue(
                key,
                out YQRuntimeWorldAssetEntry entry) ||
            entry == null ||
            entry.materialOverrides == null ||
            entry.materialOverrides.Count == 0)
        {
            // note: Assets without persisted slot adapters receive only the safe unsupported-shader pass; forcing every material through a generic clone can destroy curated source assignments.
            if (repairHierarchy)
                YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(instance);

            return 0;
        }

        int applied = 0;

        for (int i = 0;
             i < entry.materialOverrides.Count;
             i++)
        {
            YQRuntimeWorldMaterialOverride binding =
                entry.materialOverrides[i];

            if (binding == null ||
                binding.replacementMaterial == null ||
                binding.rendererIndex < 0 ||
                binding.materialIndex < 0)
            {
                continue;
            }

            Transform targetTransform;

            if (string.IsNullOrWhiteSpace(
                    binding.transformPath))
            {
                targetTransform =
                    instance.transform;
            }
            else
            {
                targetTransform =
                    instance.transform.Find(
                        binding.transformPath);
            }

            if (targetTransform == null)
                continue;

            Renderer[] renderers =
                targetTransform.GetComponents<Renderer>();

            if (renderers == null ||
                binding.rendererIndex >=
                renderers.Length)
            {
                continue;
            }

            Renderer renderer =
                renderers[
                    binding.rendererIndex];

            if (renderer == null)
                continue;

            Material[] materials =
                renderer.sharedMaterials;

            if (materials == null ||
                binding.materialIndex >=
                materials.Length)
            {
                continue;
            }

            // note: A verified serialized source binding needs no runtime substitution. Persisted adapters remain recovery authority for unresolved source slots.
            if (!YQRuntimeUrpMaterialRepair.IsMaterialSlotRequired(renderer, binding.materialIndex) ||
                YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(materials[binding.materialIndex]) ||
                !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(binding.replacementMaterial))
                continue;

            materials[
                binding.materialIndex] =
                binding.replacementMaterial;

            renderer.sharedMaterials =
                materials;

            applied++;
        }

        // note: Persisted adapters are authoritative; streamed callers can defer the residual hierarchy pass to the cooperative repair coroutine.
        if (repairHierarchy)
            YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(instance);
        SanitizeRuntimeLodGroups(instance);

        return applied;
    }

    private static void SanitizeRuntimeLodGroups(GameObject instance)
    {
        if (instance == null)
            return;
        LODGroup[] groups = instance.GetComponentsInChildren<LODGroup>(true);
        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            LODGroup group = groups[groupIndex];
            if (group == null)
                continue;
            LOD[] source = group.GetLODs();
            if (source == null || source.Length == 0)
                continue;
            List<LOD> repaired = new List<LOD>(source.Length);
            bool changed = false;
            for (int lodIndex = 0; lodIndex < source.Length; lodIndex++)
            {
                Renderer[] renderers = source[lodIndex].renderers ?? Array.Empty<Renderer>();
                List<Renderer> valid = new List<Renderer>(renderers.Length);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    if (renderers[rendererIndex] != null)
                        valid.Add(renderers[rendererIndex]);
                if (valid.Count == 0)
                {
                    changed = true;
                    continue;
                }
                if (valid.Count != renderers.Length)
                    changed = true;
                LOD lod = source[lodIndex];
                lod.renderers = valid.ToArray();
                repaired.Add(lod);
            }
            SanitizeLodTransitionHeights(repaired, ref changed);
            if (changed && repaired.Count > 0)
            {
                // note: Imported LOD assets can contain null renderers or unordered transition heights; repair the runtime instance while preserving its authored renderer order.
                group.SetLODs(repaired.ToArray());
            }
        }
    }

    private static void SanitizeLodTransitionHeights(List<LOD> lods, ref bool changed)
    {
        if (lods == null || lods.Count == 0)
            return;

        const float minimumTransitionGap = 0.001f;
        float previousTransitionHeight = 1f;
        for (int index = 0; index < lods.Count; index++)
        {
            LOD lod = lods[index];
            float original = lod.screenRelativeTransitionHeight;
            float transition = original;
            int remainingLods = lods.Count - index - 1;
            float minimumAllowed = minimumTransitionGap * remainingLods;
            float maximumAllowed = index == 0 ? 1f : previousTransitionHeight - minimumTransitionGap;

            if (float.IsNaN(transition) || float.IsInfinity(transition))
                transition = maximumAllowed;
            transition = Mathf.Clamp(transition, minimumAllowed, Mathf.Max(minimumAllowed, maximumAllowed));
            if (Mathf.Abs(transition - original) > 0.000001f)
                changed = true;

            // note: Unity requires each lower-detail transition to be strictly below its higher-detail predecessor.
            lod.screenRelativeTransitionHeight = transition;
            lods[index] = lod;
            previousTransitionHeight = transition;
        }
    }

    public bool ContainsPath(
        string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        EnsureLookup();

        string key =
            NormalizePath(assetPath);

        if (string.IsNullOrWhiteSpace(key))
            return false;

        return
            _entriesByPath.ContainsKey(key) ||
            (useLazyResourceShards &&
             !string.IsNullOrWhiteSpace(
                 BuildShardResourcePath(
                     key)));
    }

    public bool ContainsPrefabPath(
        string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        EnsureLookup();

        string key =
            NormalizePath(assetPath);

        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (_prefabsByPath.TryGetValue(
                key,
                out GameObject prefab) &&
            prefab != null)
        {
            return true;
        }

        // note: A prefab suffix or pack name is not a binding. Granular membership is exact and does not deserialize the source prefab.
        if (useGranularResourceShards)
        {
            EnsureGranularLookup();
            return key.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
                   _granularAssets != null && _granularAssets.ContainsKey(key);
        }
        if (TryGetLazyShard(key, out YQRuntimeWorldAssetRegistry shard) && shard != null)
        {
            shard.EnsureLookup();
            return shard._prefabsByPath.TryGetValue(key, out prefab) && prefab != null;
        }
        return false;
    }

    public void SetEntries(
        List<YQRuntimeWorldAssetEntry> newEntries)
    {
        entries =
            newEntries ??
            new List<YQRuntimeWorldAssetEntry>();

        BuildLookup();
    }

    public void SetLazyResourceShards(
        bool enabled)
    {
        useLazyResourceShards =
            enabled;
        if (!enabled) SetGranularResourceShards(false);

        // note: Changing registry mode invalidates the per-session shard cache without unloading live instantiated objects.
        _loadedShards =
            null;

        _missingShardPaths =
            null;
        ResetDependencyResidency();
    }

    public void RebuildLookup()
    {
        BuildLookup();
    }

    public IEnumerator PreloadAssetPathsRoutine(
        IEnumerable<string> assetPaths)
    {
        // note: Memory reclamation never retires a shard while its asynchronous preload is still warming the spawn path.
        int epoch = _residencyEpoch;
        _activePreloads++;
        try { yield return PreloadAssetPathsCore(assetPaths, epoch); }
        finally { if (_residencyEpoch == epoch) _activePreloads--; }
    }

    public IEnumerator PreloadPrefabDependenciesRoutine(string assetPath)
    {
        // note: Population and ecology share the exact-path preload boundary; clips/controllers precede the prefab's first native load.
        yield return PreloadAssetPathsRoutine(new[] { assetPath });
    }

    private IEnumerator PreloadAssetPathsCore(IEnumerable<string> assetPaths, int epoch)
    {
        if (_residencyEpoch != epoch || !useLazyResourceShards ||
            assetPaths == null)
        {
            yield break;
        }

        _loadedShards ??=
            new Dictionary<string, YQRuntimeWorldAssetRegistry>(
                StringComparer.OrdinalIgnoreCase);

        _missingShardPaths ??=
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var requiredPaths = new List<string>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string assetPath in assetPaths)
        {
            string key = NormalizePath(assetPath);
            if (!string.IsNullOrWhiteSpace(key) && seenPaths.Add(key)) requiredPaths.Add(key);
        }

        foreach (string assetPath in requiredPaths)
        {
            if (_residencyEpoch != epoch) yield break;
            if (TryGetPrefabDependencies(assetPath, out PrefabDependencySet dependencies))
            {
                foreach (string dependency in dependencies.resources)
                {
                    yield return PreloadResourceRoutine(dependency, true, epoch);
                    if (_residencyEpoch != epoch) yield break;
                }
            }
            else if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && !_loggedMissingDependencyMetadata)
            {
                // note: Older source checkpoints remain loadable, but cannot silently claim the cold activation repair without rebuilt dependency bindings.
                _loggedMissingDependencyMetadata = true;
                Debug.LogWarning("[YQRuntimeWorldAssetRegistry] Selected prefab dependency metadata is missing/stale. " +
                    "Run YourQuest > Assets > Build Prefab Dependency Preloads in the complete Unity checkout. " +
                    "Legacy asynchronous prefab loading remains available; cold activation is not qualified. First path: " + assetPath);
            }

            string resourcePath = ResolveResourcePath(assetPath);
            if (!string.IsNullOrWhiteSpace(resourcePath))
                yield return PreloadResourceRoutine(resourcePath, false, epoch);
        }
    }

    private IEnumerator PreloadResourceRoutine(string resourcePath, bool dependency, int epoch)
    {
        if (_residencyEpoch != epoch) yield break;
        _shardLastUse[resourcePath] = Time.realtimeSinceStartup;
        if (dependency)
        {
            if ((_loadedDependencies.TryGetValue(resourcePath, out var loadedDependency) && loadedDependency != null && loadedDependency.asset != null) ||
                _missingDependencies.Contains(resourcePath)) yield break;
        }
        else if ((_loadedShards.TryGetValue(resourcePath, out var loadedShard) && loadedShard != null) ||
                 _missingShardPaths.Contains(resourcePath)) yield break;

        if (!_pendingPreloads.TryGetValue(resourcePath, out ResourceRequest request))
        {
            request = dependency ? Resources.LoadAsync<YQRuntimePrefabDependency>(resourcePath)
                                 : Resources.LoadAsync<YQRuntimeWorldAssetRegistry>(resourcePath);
            _pendingPreloads[resourcePath] = request;
            ResourceRequest pending = request;
            // note: A canceled consumer cannot cancel native Resources loading. Its completion still retires the request and enters the ordinary reclaimable cache.
            request.completed += _ => CompleteResourcePreload(resourcePath, pending, dependency, epoch);
        }
        // note: Both Unity coroutines and manual materialization drivers advance this iterator. A raw ResourceRequest yield is not a wait in every driver.
        while (_residencyEpoch == epoch && !request.isDone) yield return null;
        if (_residencyEpoch != epoch) yield break;
        // note: Custom coroutine drivers may resume as soon as isDone; install the result even if Unity has not dispatched its completion event yet.
        if (_pendingPreloads.TryGetValue(resourcePath, out var current) && ReferenceEquals(current, request))
            CompleteResourcePreload(resourcePath, request, dependency, epoch);
    }

    private void CompleteResourcePreload(string resourcePath, ResourceRequest request, bool dependency, int epoch)
    {
        if (_residencyEpoch != epoch || !_pendingPreloads.TryGetValue(resourcePath, out var current) || !ReferenceEquals(current, request)) return;
        _pendingPreloads.Remove(resourcePath);
        _shardLastUse[resourcePath] = Time.realtimeSinceStartup;
        if (dependency)
        {
            var descriptor = request.asset as YQRuntimePrefabDependency;
            if (descriptor == null || descriptor.asset == null)
            {
                _missingDependencies.Add(resourcePath);
                Debug.LogError("[YQRuntimeWorldAssetRegistry] Prefab dependency binding failed to load: " + resourcePath);
            }
            else _loadedDependencies[resourcePath] = descriptor;
            return;
        }
        var shard = request.asset as YQRuntimeWorldAssetRegistry;
        if (shard == null) _missingShardPaths.Add(resourcePath);
        else
        {
            shard.EnsureLookup();
            _loadedShards[resourcePath] = shard;
        }
    }

    private void OnEnable()
    {
        // note: Unity reloads private scalar fields but not dictionaries. Retire the manifest flag with its transient lookups to avoid a false warm cache.
        _granularLookupAttempted = false;
        _granularAssets = null;
        _granularPacks = null;
        _loadedShards = null;
        _missingShardPaths = null;
        _shardLastUse.Clear();
        ResetDependencyResidency();
        /*
         * Do not trust dictionaries to survive reload/play-mode
         * transitions. They are intentionally rebuilt from the
         * serialized list.
         */
        BuildLookup();
    }

    private void ResetDependencyResidency()
    {
        // note: Private dictionaries and lease identities are per session, including Enter Play Mode with domain reload disabled.
        _residencyEpoch++;
        _activePreloads = 0;
        _dependencyLookupAttempted = false;
        _prefabDependencies = null;
        _loggedMissingDependencyMetadata = false;
        _loadedDependencies.Clear();
        _pendingPreloads.Clear();
        _missingDependencies.Clear();
        _resourcePins.Clear();
    }

    private void EnsureLookup()
    {
        if (_prefabsByPath == null ||
            _materialsByPath == null ||
            _entriesByPath == null)
        {
            BuildLookup();
        }
    }

    private void BuildLookup()
    {
        _prefabsByPath =
            new Dictionary<string, GameObject>(
                StringComparer.OrdinalIgnoreCase);

        _materialsByPath =
            new Dictionary<string, Material>(
                StringComparer.OrdinalIgnoreCase);

        _entriesByPath =
            new Dictionary<
                string,
                YQRuntimeWorldAssetEntry>(
                    StringComparer.OrdinalIgnoreCase);

        if (entries == null)
        {
            entries =
                new List<YQRuntimeWorldAssetEntry>();

            return;
        }

        for (int i = 0;
             i < entries.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                entries[i];

            if (entry == null)
                continue;

            string key =
                NormalizePath(
                    entry.assetPath);

            if (string.IsNullOrWhiteSpace(key))
                continue;

            /*
             * Use assignment instead of Add so a duplicate path
             * cannot poison the entire lookup.
             */
            _entriesByPath[key] =
                entry;

            if (entry.prefab != null)
            {
                _prefabsByPath[key] =
                    entry.prefab;
            }

            if (entry.material != null)
            {
                _materialsByPath[key] =
                    entry.material;
            }
        }
    }

    private bool TryGetLazyShard(
        string assetPath,
        out YQRuntimeWorldAssetRegistry shard)
    {
        shard =
            null;

        if (!useLazyResourceShards ||
            string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        string resourcePath = ResolveResourcePath(assetPath);

        if (string.IsNullOrWhiteSpace(resourcePath))
            return false;

        _loadedShards ??=
            new Dictionary<string, YQRuntimeWorldAssetRegistry>(
                StringComparer.OrdinalIgnoreCase);

        _missingShardPaths ??=
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (_loadedShards.TryGetValue(
                resourcePath,
                out shard) &&
            shard != null)
        {
            _shardLastUse[resourcePath] = Time.realtimeSinceStartup;
            return true;
        }

        if (_missingShardPaths.Contains(
                resourcePath))
        {
            return false;
        }

        shard =
            Resources.Load<YQRuntimeWorldAssetRegistry>(
                resourcePath);

        if (shard == null)
        {
            // note: Remember absent generated shards so an invalid model/catalog path cannot cause repeated Resources scans.
            _missingShardPaths.Add(
                resourcePath);

            return false;
        }

        // note: A synchronous fallback load also invokes OnEnable, so avoid a second full dictionary allocation on the requesting frame.
        shard.EnsureLookup();
        _loadedShards[resourcePath] =
            shard;
        _shardLastUse[resourcePath] = Time.realtimeSinceStartup;

        return true;
    }

    public static string BuildShardResourcePath(
        string assetPath)
    {
        string normalized =
            NormalizePath(
                assetPath);

        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        string[] segments =
            normalized.Split('/');

        if (segments.Length < 2)
            return string.Empty;

        // note: Publisher plus pack name keeps one coherent visual library together while separating unrelated genres.
        int publisherIndex =
            string.Equals(
                segments[0],
                "Assets",
                StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;

        int packIndex =
            Mathf.Min(
                publisherIndex + 1,
                segments.Length - 1);

        string rawKey =
            segments[publisherIndex] +
            "_" +
            segments[packIndex];

        StringBuilder key =
            new StringBuilder(
                rawKey.Length);

        for (int i = 0;
             i < rawKey.Length;
             i++)
        {
            char character =
                rawKey[i];

            if (char.IsLetterOrDigit(character))
            {
                key.Append(
                    char.ToLowerInvariant(character));
            }
            else if (key.Length > 0 &&
                     key[key.Length - 1] != '_')
            {
                key.Append('_');
            }
        }

        return
            "YQWorldAssetShards/YQWorldAssets_" +
            key.ToString().Trim('_');
    }

    private static YQRuntimeWorldAssetRegistry
        LoadBestRuntimeRegistry()
    {
        // note: Prefer the exact resource name so unrelated broken Resources assets do not flood the console.
        YQRuntimeWorldAssetRegistry direct =
            Resources.Load<
                YQRuntimeWorldAssetRegistry>(
                    ResourcesAssetName);

        if (direct != null)
            return direct;

        /*
         * Keep LoadAll only as a repair fallback for renamed/duplicate
         * registries. It is intentionally off the hot path because Unity
         * reports missing scripts on every scanned Resources object.
         */
        YQRuntimeWorldAssetRegistry[] candidates =
            Resources.LoadAll<
                YQRuntimeWorldAssetRegistry>(
                    string.Empty);

        if (candidates == null ||
            candidates.Length == 0)
        {
            return
                Resources.Load<
                    YQRuntimeWorldAssetRegistry>(
                        ResourcesAssetName);
        }

        YQRuntimeWorldAssetRegistry best =
            null;

        int bestReferencedAssets = -1;
        int bestEntryCount = -1;

        int matchingRegistryCount = 0;

        for (int i = 0;
             i < candidates.Length;
             i++)
        {
            YQRuntimeWorldAssetRegistry candidate =
                candidates[i];

            if (candidate == null)
                continue;

            /*
             * Prefer the intended registry by asset name.
             * This also permits future unrelated registry assets.
             */
            if (!string.Equals(
                    candidate.name,
                    ResourcesAssetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matchingRegistryCount++;

            int referencedAssets =
                candidate.CountSerializedReferences();

            int entryCount =
                candidate.entries != null
                    ? candidate.entries.Count
                    : 0;

            if (best == null ||
                referencedAssets >
                    bestReferencedAssets ||
                (referencedAssets ==
                    bestReferencedAssets &&
                 entryCount >
                    bestEntryCount))
            {
                best =
                    candidate;

                bestReferencedAssets =
                    referencedAssets;

                bestEntryCount =
                    entryCount;
            }
        }

        /*
         * If naming changed somehow, fall back to the candidate
         * containing the most real serialized references.
         */
        if (best == null)
        {
            for (int i = 0;
                 i < candidates.Length;
                 i++)
            {
                YQRuntimeWorldAssetRegistry candidate =
                    candidates[i];

                if (candidate == null)
                    continue;

                int referencedAssets =
                    candidate.CountSerializedReferences();

                int entryCount =
                    candidate.entries != null
                        ? candidate.entries.Count
                        : 0;

                if (best == null ||
                    referencedAssets >
                        bestReferencedAssets ||
                    (referencedAssets ==
                        bestReferencedAssets &&
                     entryCount >
                        bestEntryCount))
                {
                    best =
                        candidate;

                    bestReferencedAssets =
                        referencedAssets;

                    bestEntryCount =
                        entryCount;
                }
            }
        }

        if (matchingRegistryCount > 1)
        {
            Debug.LogWarning(
                "[YQRuntimeWorldAssetRegistry] Found " +
                matchingRegistryCount +
                " Resources assets named '" +
                ResourcesAssetName +
                "'. Using the one with the most " +
                "serialized asset references.");
        }

        return best;
    }

    private static YQRuntimeWorldAssetRegistry CreateEmptyRuntimeFallbackRegistry()
    {
        if (!_loggedRegistryLoadFailure)
        {
            _loggedRegistryLoadFailure =
                true;

            Debug.LogWarning(
                "[YQRuntimeWorldAssetRegistry] " +
                "No runtime asset registry loaded from Resources. " +
                "Using an empty in-memory registry for this play session.");
        }

        YQRuntimeWorldAssetRegistry registry =
            CreateInstance<
                YQRuntimeWorldAssetRegistry>();

        registry.name =
            ResourcesAssetName +
            "_RuntimeFallback";

        // note: Empty registry prevents repeated Resources scans while preserving null-safe asset resolution.
        return registry;
    }

    private int CountSerializedReferences()
    {
        if (entries == null)
            return 0;

        int count = 0;

        for (int i = 0;
             i < entries.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                entries[i];

            if (entry == null)
                continue;

            if (entry.prefab != null)
                count++;

            if (entry.material != null)
                count++;

            if (entry.materialOverrides != null)
            {
                for (int j = 0;
                     j <
                     entry.materialOverrides.Count;
                     j++)
                {
                    YQRuntimeWorldMaterialOverride binding =
                        entry.materialOverrides[j];

                    if (binding != null &&
                        binding.replacementMaterial != null)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private void LogRuntimeSummaryOnce()
    {
        if (_loggedRuntimeSummary)
            return;

        _loggedRuntimeSummary = true;

        int entryCount =
            entries != null
                ? entries.Count
                : 0;

        int serializedPrefabs = 0;
        int serializedMaterials = 0;
        int serializedOverrides = 0;

        if (entries != null)
        {
            for (int i = 0;
                 i < entries.Count;
                 i++)
            {
                YQRuntimeWorldAssetEntry entry =
                    entries[i];

                if (entry == null)
                    continue;

                if (entry.prefab != null)
                    serializedPrefabs++;

                if (entry.material != null)
                    serializedMaterials++;

                if (entry.materialOverrides == null)
                    continue;

                for (int j = 0;
                     j <
                     entry.materialOverrides.Count;
                     j++)
                {
                    YQRuntimeWorldMaterialOverride binding =
                        entry.materialOverrides[j];

                    if (binding != null &&
                        binding.replacementMaterial != null)
                    {
                        serializedOverrides++;
                    }
                }
            }
        }

        Debug.Log(
            "[YQRuntimeWorldAssetRegistry] LOADED\n" +
            "Asset name: " +
            name +
            "\n" +
            "Mode: " +
            (useLazyResourceShards
                ? "lazy pack shards (empty root is expected)"
                : "monolithic registry") +
            "\n" +
            "Serialized entries: " +
            entryCount +
            "\n" +
            "Serialized prefab references: " +
            serializedPrefabs +
            "\n" +
            "Serialized material references: " +
            serializedMaterials +
            "\n" +
            "Serialized material overrides: " +
            serializedOverrides +
            "\n" +
            "Prefab lookup entries: " +
            (_prefabsByPath != null
                ? _prefabsByPath.Count
                : 0) +
            "\n" +
            "Material lookup entries: " +
            (_materialsByPath != null
                ? _materialsByPath.Count
                : 0));
    }

    public static string NormalizePath(
        string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return string.Empty;

        string normalized =
            assetPath
                .Trim()
                .Replace(
                    '\\',
                    '/');

        while (normalized.Contains("//"))
        {
            normalized =
                normalized.Replace(
                    "//",
                    "/");
        }

        return normalized;
    }

    public static void ClearCachedInstance()
    {
        // note: Destroyed scene-owner wrappers can still reference this router. Detach its asset graph before dropping the global pointer.
        if (!ReferenceEquals(_instance, null))
        {
            _instance._loadedShards?.Clear();
            _instance._missingShardPaths?.Clear();
            _instance._shardLastUse.Clear();
            _instance.ResetDependencyResidency();
        }
        PrefabScriptValidity.Clear();
        _instance = null;
        _loggedRuntimeSummary = false;
        _loggedRegistryLoadFailure = false;
    }

    internal static int ReleaseColdReloadableShards(float now, float keepWarmSeconds = 30f)
    {
        // note: Retire only dormant source-cache references. Live instances retain their meshes/materials; future demand uses the existing async preload and unchanged asset bindings.
        if (_instance == null || _instance._activePreloads != 0 || _instance._loadedShards == null) return 0;
        var retired = new List<string>();
        foreach (var pair in _instance._loadedShards)
            if (!_instance._resourcePins.ContainsKey(pair.Key) && !_instance._pendingPreloads.ContainsKey(pair.Key) &&
                _instance._shardLastUse.TryGetValue(pair.Key, out float used) &&
                YQLlmResidencyPolicy.CanRetireGameCache(now, used, keepWarmSeconds)) retired.Add(pair.Key);
        foreach (string key in retired)
        {
            var shard = _instance._loadedShards[key];
            if (shard != null && shard.entries != null)
                foreach (var entry in shard.entries)
                    if (entry?.prefab != null) PrefabScriptValidity.Remove(entry.prefab);
            _instance._loadedShards.Remove(key);
            _instance._shardLastUse.Remove(key);
        }
        var retiredDependencies = new List<string>();
        foreach (var pair in _instance._loadedDependencies)
            if (!_instance._resourcePins.ContainsKey(pair.Key) && !_instance._pendingPreloads.ContainsKey(pair.Key) &&
                _instance._shardLastUse.TryGetValue(pair.Key, out float used) &&
                YQLlmResidencyPolicy.CanRetireGameCache(now, used, keepWarmSeconds)) retiredDependencies.Add(pair.Key);
        foreach (string key in retiredDependencies)
        {
            _instance._loadedDependencies.Remove(key);
            _instance._shardLastUse.Remove(key);
        }
        return retired.Count + retiredDependencies.Count;
    }

#if UNITY_EDITOR
    private const string EditorCleanupPendingKey = "YourQuest.RuntimeWorldAssetRegistry.UnusedCleanupPending";

    [InitializeOnLoadMethod]
    private static void RegisterEditorSessionCleanup()
    {
        EditorApplication.playModeStateChanged -= OnEditorPlayModeChanged;
        EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= ClearCachedInstance;
        AssemblyReloadEvents.beforeAssemblyReload += ClearCachedInstance;
        // note: A Play-exit domain reload must not discard a collection requested before the Editor finished its transition.
        if (SessionState.GetBool(EditorCleanupPendingKey, false))
        {
            EditorApplication.update -= ReleaseUnusedEditorSessionAssets;
            EditorApplication.update += ReleaseUnusedEditorSessionAssets;
        }
    }

    private static void OnEditorPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        // note: Retain the request until the Editor is idle; a one-shot delay can run while the Play transition still reports busy.
        ClearCachedInstance();
        SessionState.SetBool(EditorCleanupPendingKey, true);
        EditorApplication.update -= ReleaseUnusedEditorSessionAssets;
        EditorApplication.update += ReleaseUnusedEditorSessionAssets;
    }

    private static void ReleaseUnusedEditorSessionAssets()
    {
        // note: Cancel if a new Play session started, otherwise wait through compilation/import/transition without losing the cleanup request.
        if (EditorApplication.isPlaying || !SessionState.GetBool(EditorCleanupPendingKey, false))
        {
            SessionState.SetBool(EditorCleanupPendingKey, false);
            EditorApplication.update -= ReleaseUnusedEditorSessionAssets;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        SessionState.SetBool(EditorCleanupPendingKey, false);
        EditorApplication.update -= ReleaseUnusedEditorSessionAssets;
        // note: Keep script/static references as roots; only genuinely unused imported assets are eligible for unloading.
        EditorUtility.UnloadUnusedAssetsImmediate(true);
        Debug.Log("[YQRuntimeWorldAssetRegistry] Released unused assets after Play teardown.");
    }
#endif
}
