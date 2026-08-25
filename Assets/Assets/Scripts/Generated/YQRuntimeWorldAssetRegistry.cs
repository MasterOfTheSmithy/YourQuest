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

    private Dictionary<string, GameObject> _prefabsByPath;
    private Dictionary<string, Material> _materialsByPath;
    private Dictionary<string, YQRuntimeWorldAssetEntry> _entriesByPath;
    private Dictionary<string, YQRuntimeWorldAssetRegistry> _loadedShards;
    private HashSet<string> _missingShardPaths;

    private static YQRuntimeWorldAssetRegistry _instance;

    private static bool _loggedRuntimeSummary;

    private static bool _loggedRegistryLoadFailure;

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
        _instance = null;
        _loggedRuntimeSummary = false;
        _loggedRegistryLoadFailure = false;
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
                    _instance.BuildLookup();
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

    public IReadOnlyList<YQRuntimeWorldAssetEntry> GetEntriesForAssetPath(
        string assetPath)
    {
        EnsureLookup();

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
                preferredPrefab != null)
            {
                // note: Prefer imported URP variants over HDRP(Default) paths to preserve material/shader compatibility in Play Mode.
                return preferredPrefab;
            }

            preferredPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    preferredKey);

            if (preferredPrefab != null)
                return preferredPrefab;
        }
#endif

        if (_prefabsByPath.TryGetValue(
                key,
                out GameObject prefab) &&
            prefab != null)
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

        if (prefab != null)
            return prefab;
#endif

        return null;
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

    public int ApplyMaterialOverrides(
        string assetPath,
        GameObject instance)
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
            return
                shard.ApplyMaterialOverrides(
                    key,
                    instance);
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

            materials[
                binding.materialIndex] =
                binding.replacementMaterial;

            renderer.sharedMaterials =
                materials;

            applied++;
        }

        // note: Persisted adapters are authoritative. A final scoped pass repairs only missing or unsupported residual slots instead of cloning the complete reviewed hierarchy.
        YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(instance);

        return applied;
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

        return
            useLazyResourceShards &&
            key.EndsWith(
                ".prefab",
                StringComparison.OrdinalIgnoreCase);
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

        // note: Changing registry mode invalidates the per-session shard cache without unloading live instantiated objects.
        _loadedShards =
            null;

        _missingShardPaths =
            null;
    }

    public void RebuildLookup()
    {
        BuildLookup();
    }

    public IEnumerator PreloadAssetPathsRoutine(
        IEnumerable<string> assetPaths)
    {
        if (!useLazyResourceShards ||
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

        HashSet<string> requiredShards =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (string assetPath in assetPaths)
        {
            string resourcePath =
                BuildShardResourcePath(
                    assetPath);

            if (!string.IsNullOrWhiteSpace(resourcePath))
            {
                requiredShards.Add(
                    resourcePath);
            }
        }

        foreach (string resourcePath in requiredShards)
        {
            if (_loadedShards.ContainsKey(resourcePath) ||
                _missingShardPaths.Contains(resourcePath))
            {
                continue;
            }

            // note: Active palette packs warm asynchronously before spawning so their first prefab cannot introduce a synchronous Resources hitch.
            ResourceRequest request =
                Resources.LoadAsync<YQRuntimeWorldAssetRegistry>(
                    resourcePath);

            yield return request;

            YQRuntimeWorldAssetRegistry shard =
                request.asset as YQRuntimeWorldAssetRegistry;

            if (shard == null)
            {
                _missingShardPaths.Add(
                    resourcePath);

                continue;
            }

            shard.BuildLookup();
            _loadedShards[resourcePath] =
                shard;
        }
    }

    private void OnEnable()
    {
        /*
         * Do not trust dictionaries to survive reload/play-mode
         * transitions. They are intentionally rebuilt from the
         * serialized list.
         */
        BuildLookup();
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

        string resourcePath =
            BuildShardResourcePath(
                assetPath);

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

        shard.BuildLookup();
        _loadedShards[resourcePath] =
            shard;

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
        _instance = null;
        _loggedRuntimeSummary = false;
        _loggedRegistryLoadFailure = false;
    }
}
