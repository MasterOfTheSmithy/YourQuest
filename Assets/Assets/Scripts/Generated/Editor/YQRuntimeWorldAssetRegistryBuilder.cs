using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class YQRuntimeWorldAssetRegistryBuilder
{
    private const string RegistryFolder =
        "Assets/Assets/Resources";

    private const string RegistryPath =
        RegistryFolder +
        "/YQRuntimeWorldAssetRegistry.asset";

    private const string DiscoveredCatalogPath =
        RegistryFolder +
        "/YQDiscoveredWorldAssetCatalog.asset";

    private const string RuntimeShardFolder =
        RegistryFolder +
        "/YQWorldAssetShards";

    private const string HivemindUrpMaterialsFolder =
        "Assets/Assets/GeneratedAssets/HivemindUrpMaterials";

    private const string HivemindMissingMaterialPath =
        HivemindUrpMaterialsFolder +
        "/YQ_HivemindMissingMaterial.mat";

    private static YQRuntimeWorldAssetRegistry _hivemindBindingRegistry;
    private static List<YQRuntimeWorldAssetEntry> _hivemindBindingEntries;
    private static List<YQRuntimeWorldAssetEntry> _hivemindBindingTargets;
    private static Dictionary<int, Material> _hivemindConvertedMaterials;
    private static int _hivemindBindingIndex;
    private static int _hivemindBoundPrefabCount;
    private static int _hivemindBoundMaterialSlots;
    private static int _hivemindUnresolvedMaterialSlots;

    private static YQRuntimeWorldAssetRegistry _missingScriptPruneRegistry;
    private static List<YQRuntimeWorldAssetEntry> _missingScriptPruneSource;
    private static List<YQRuntimeWorldAssetEntry> _missingScriptPruneRetained;
    private static Dictionary<string, bool> _missingScriptDependencyCache;
    private static int _missingScriptPruneIndex;
    private static int _missingScriptPruneRemoved;

    [InitializeOnLoadMethod]
    private static void ScheduleOversizedRegistryOptimization()
    {
        // note: Existing release-prep registries migrate once after script reload, avoiding another expensive discovery/repair pass.
        EditorApplication.delayCall +=
            TryOptimizeOversizedExistingRegistry;
    }

    private static void TryOptimizeOversizedExistingRegistry()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorApplication.delayCall +=
                TryOptimizeOversizedExistingRegistry;

            return;
        }

        YQRuntimeWorldAssetRegistry registry =
            AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(
                RegistryPath);

        if (registry == null ||
            registry.UsesLazyResourceShards ||
            registry.Entries == null ||
            registry.Entries.Count < 512)
        {
            return;
        }

        OptimizeExistingRuntimeRegistry();
    }

    // note: Zero means scan every file under approved roots; curation happens through path/type filters below.
    private const int MaxDiscoveredPrefabsPerRoot =
        0;

    private const int MaxDiscoveredMaterialsPerRoot =
        0;

    private static readonly string[] PrefabDiscoveryRoots =
    {
        "Assets/BefourStudios/AncientDesertRuins",
        "Assets/BefourStudios/AsianDynastyEnvironment",
        "Assets/BefourStudios/BioHorrorSciFiEnvironment",
        "Assets/BefourStudios/ContainerDistrict",
        "Assets/BefourStudios/MedievalVikingVillage",
        "Assets/BefourStudios/NordicVillage",
        "Assets/BefourStudios/PersepolisEmpireEnvironment",
        "Assets/BefourStudios/SciFiEngineersRoom",
        "Assets/BefourStudios/VictorianMansionEnvironment",
        "Assets/BefourStudios/WesternDesertTown",
        "Assets/HIVEMIND",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets",
        "Assets/YughuesFreeBushes2018/Prefabs",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Rock Monster",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Spiders",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Dragons",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Demons",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Devils",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters",
        "Assets/HumbleBundleResources",
        "Assets/Assets/humblebundleresources"
    };

    private static readonly string[] MaterialDiscoveryRoots =
    {
        "Assets/Tom's Terrain Tools/Unity Terrain Assets",
        "Assets/ADG_Textures",
        "Assets/HIVEMIND",
        "Assets/HumbleBundleResources",
        "Assets/Assets/humblebundleresources"
    };

    public static string[] GetConfiguredPrefabDiscoveryRoots()
    {
        // note: WG1 reuses the authoritative discovery boundary instead of maintaining a second list that can silently drift.
        return (string[])PrefabDiscoveryRoots.Clone();
    }

    public static string[] GetConfiguredMaterialDiscoveryRoots()
    {
        // note: Material intake follows the same approved library boundary as registry repair.
        return (string[])MaterialDiscoveryRoots.Clone();
    }

    // note: Legacy registry commands remain callable for recovery but are isolated from the golden-assembly production workflow.
    [MenuItem(
        "Tools/YourQuest/Archived Tools/Legacy Runtime Registry/Rebuild Registry")]
    public static void RebuildRegistry()
    {
        RebuildRegistryInternal(
            false);
    }

    public static void DryRunProceduralAssetDiscovery()
    {
        // note: Kept callable from code, but removed from the Unity menu after the one-time asset import pass.
        List<GeneratedAssetReferenceRecord> discoveredReferences =
            BuildDiscoveredAssetReferences();

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] DRY RUN COMPLETE\n" +
            "No assets were written.\n" +
            "Discovered semantic entries: " +
            discoveredReferences.Count);
    }

    public static void RebuildRegistryWithDiscoveredAssets()
    {
        // note: Kept callable from code, but hidden so normal editor use cannot accidentally rescan imported packs.
        RebuildRegistryInternal(
            true);
    }

    [MenuItem(
        "Tools/YourQuest/Archived Tools/Legacy Runtime Registry/Rebuild and Repair All Procedural Assets")]
    // note: This release-prep entry point rebuilds semantic discovery, URP sibling migration, and persistent Hivemind material bindings in one deterministic editor pass.
    public static void RebuildAndRepairAllProceduralAssets()
    {
        RebuildRegistryInternal(
            true);

        RepairExistingRegistryToUrp();

        BindAllHivemindEntriesSynchronously();
    }

    [MenuItem(
        "Tools/YourQuest/Archived Tools/Legacy Runtime Registry/Optimize Existing Registry")]
    public static void OptimizeExistingRuntimeRegistry()
    {
        YQRuntimeWorldAssetRegistry registry =
            AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(
                RegistryPath);

        if (registry == null)
        {
            Debug.LogWarning(
                "[YQRuntimeWorldAssetRegistryBuilder] No runtime registry asset was found to optimize.");

            return;
        }

        if (registry.UsesLazyResourceShards)
        {
            // note: An empty root registry is correct once its entries have been split into Resources shards.
            Debug.Log(
                "[YQRuntimeWorldAssetRegistryBuilder] Runtime registry is already optimized with lazy resource shards.");

            return;
        }

        if (registry.Entries == null || registry.Entries.Count == 0)
        {
            Debug.LogWarning(
                "[YQRuntimeWorldAssetRegistryBuilder] No populated monolithic registry is available to shard.");

            return;
        }

        // note: This fast path converts an already repaired monolithic registry without rescanning or changing imported assets.
        WriteRuntimeShards(
            registry,
            new List<YQRuntimeWorldAssetEntry>(
                registry.Entries));
    }

    private static void BindAllHivemindEntriesSynchronously()
    {
        YQRuntimeWorldAssetRegistry registry =
            AssetDatabase.LoadAssetAtPath<
                YQRuntimeWorldAssetRegistry>(
                    RegistryPath);

        if (registry == null)
            return;

        // note: The synchronous release pass avoids leaving a partially bound catalog when Unity runs headless validation.
        _hivemindBindingRegistry = registry;
        _hivemindBindingEntries =
            new List<YQRuntimeWorldAssetEntry>(
                registry.Entries);
        _hivemindConvertedMaterials =
            new Dictionary<int, Material>();
        _hivemindBoundPrefabCount = 0;
        _hivemindBoundMaterialSlots = 0;
        _hivemindUnresolvedMaterialSlots = 0;

        for (int i = 0;
             i < _hivemindBindingEntries.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                _hivemindBindingEntries[i];

            if (IsHivemindHdrpEntry(entry))
                BindHivemindEntryMaterials(entry);
        }

        registry.SetEntries(
            _hivemindBindingEntries);
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();

        // note: Release output stores repaired assets in lazy pack shards after all binding work has completed.
        WriteRuntimeShards(
            registry,
            _hivemindBindingEntries);

        YQRuntimeWorldAssetRegistry.ClearCachedInstance();

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] Full Hivemind URP pass complete. Prefabs=" +
            _hivemindBoundPrefabCount +
            ", material slots=" +
            _hivemindBoundMaterialSlots +
            ", unresolved slots=" +
            _hivemindUnresolvedMaterialSlots + ".");

        _hivemindBindingRegistry = null;
        _hivemindBindingEntries = null;
        _hivemindConvertedMaterials = null;
    }

    private static void WriteRuntimeShards(
        YQRuntimeWorldAssetRegistry rootRegistry,
        List<YQRuntimeWorldAssetEntry> sourceEntries)
    {
        if (rootRegistry == null ||
            sourceEntries == null ||
            sourceEntries.Count == 0)
        {
            return;
        }

        EnsureFolderPath(
            RuntimeShardFolder);

        SortedDictionary<string, List<YQRuntimeWorldAssetEntry>> groups =
            new SortedDictionary<string, List<YQRuntimeWorldAssetEntry>>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < sourceEntries.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                sourceEntries[i];

            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.assetPath))
            {
                continue;
            }

            string resourcePath =
                YQRuntimeWorldAssetRegistry.BuildShardResourcePath(
                    entry.assetPath);

            if (string.IsNullOrWhiteSpace(resourcePath))
                continue;

            if (!groups.TryGetValue(
                    resourcePath,
                    out List<YQRuntimeWorldAssetEntry> group))
            {
                group =
                    new List<YQRuntimeWorldAssetEntry>();

                groups[resourcePath] =
                    group;
            }

            group.Add(
                entry);
        }

        // note: Empty obsolete generated shards release their old direct references without deleting any project asset.
        string[] existingShardGuids =
            AssetDatabase.FindAssets(
                "t:YQRuntimeWorldAssetRegistry",
                new[] { RuntimeShardFolder });

        for (int i = 0;
             i < existingShardGuids.Length;
             i++)
        {
            string existingPath =
                AssetDatabase.GUIDToAssetPath(
                    existingShardGuids[i]);

            YQRuntimeWorldAssetRegistry existing =
                AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(
                    existingPath);

            if (existing == null)
                continue;

            existing.SetLazyResourceShards(
                false);

            existing.SetEntries(
                new List<YQRuntimeWorldAssetEntry>());

            EditorUtility.SetDirty(
                existing);
        }

        int shardedEntries =
            0;

        foreach (KeyValuePair<string, List<YQRuntimeWorldAssetEntry>> pair in groups)
        {
            string assetPath =
                RegistryFolder +
                "/" +
                pair.Key +
                ".asset";

            YQRuntimeWorldAssetRegistry shard =
                AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(
                    assetPath);

            if (shard == null)
            {
                shard =
                    ScriptableObject.CreateInstance<YQRuntimeWorldAssetRegistry>();

                AssetDatabase.CreateAsset(
                    shard,
                    assetPath);
            }

            shard.SetLazyResourceShards(
                false);

            shard.SetEntries(
                pair.Value);

            EditorUtility.SetDirty(
                shard);

            shardedEntries +=
                pair.Value.Count;
        }

        // note: The root remains a tiny router; exact asset references live only in the on-demand pack registries.
        rootRegistry.SetEntries(
            new List<YQRuntimeWorldAssetEntry>());

        rootRegistry.SetLazyResourceShards(
            true);

        EditorUtility.SetDirty(
            rootRegistry);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        YQRuntimeWorldAssetRegistry.ClearCachedInstance();

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] LAZY RUNTIME SHARDS READY\n" +
            "Pack shards: " +
            groups.Count +
            "\nSharded entries: " +
            shardedEntries +
            "\nStartup root references: 0");
    }

    [MenuItem(
        "Tools/YourQuest/Archived Tools/Legacy Runtime Registry/Repair Existing Registry to URP")]
    public static void RepairExistingRegistryToUrp()
    {
        YQRuntimeWorldAssetRegistry registry =
            AssetDatabase.LoadAssetAtPath<
                YQRuntimeWorldAssetRegistry>(
                    RegistryPath);

        if (registry == null)
        {
            Debug.LogWarning(
                "[YQRuntimeWorldAssetRegistryBuilder] " +
                "No runtime registry exists to repair.");

            return;
        }

        bool restoreLazyShardsAfterRepair =
            registry.UsesLazyResourceShards;

        List<YQRuntimeWorldAssetEntry> sourceEntries =
            new List<YQRuntimeWorldAssetEntry>(
                registry.Entries);

        int restoredFromCatalog =
            RestoreMissingRegistryEntriesFromCatalog(
                sourceEntries);

        List<YQRuntimeWorldAssetEntry> repaired =
            new List<YQRuntimeWorldAssetEntry>();

        HashSet<string> seenPaths =
            new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);

        int migrated = 0;
        int missingUrpCounterpart = 0;
        int skippedMissingScripts = 0;

        for (int i = 0;
             i < sourceEntries.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                sourceEntries[i];

            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.assetPath))
            {
                continue;
            }

            string sourcePath =
                YQRuntimeWorldAssetRegistry.NormalizePath(
                    entry.assetPath);

            if (entry.prefab != null &&
                HasMissingScripts(
                    entry.prefab))
            {
                // note: Registry-held prefabs deserialize during Resources.Load, so entries with missing scripts must never reach Play Mode.
                skippedMissingScripts++;
                continue;
            }

            string repairedPath =
                BuildUrpSiblingPath(
                    sourcePath);

            if (!string.Equals(
                    sourcePath,
                    repairedPath,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                GameObject urpPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        repairedPath);

                Material urpMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        repairedPath);

                if (urpPrefab == null &&
                    urpMaterial == null)
                {
                    missingUrpCounterpart++;

                    // note: Keep source assets when a pack ships an empty URP folder; dropping a usable registry entry is never a valid repair.
                    repairedPath =
                        sourcePath;
                }
                else
                {
                    // note: Swap only verified HDRP siblings and clear old pipeline-specific override bindings.
                    entry.assetPath =
                        repairedPath;

                    entry.prefab =
                        urpPrefab;

                    entry.material =
                        urpMaterial;

                    entry.materialOverrides =
                        new List<
                            YQRuntimeWorldMaterialOverride>();

                    migrated++;
                }
            }

            if (seenPaths.Add(entry.assetPath))
            {
                repaired.Add(entry);
            }
        }

        registry.SetLazyResourceShards(
            false);

        registry.SetEntries(
            repaired);

        EditorUtility.SetDirty(
            registry);

        AssetDatabase.SaveAssets();

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] " +
            "URP registry repair complete. Migrated=" +
            migrated +
            ", restored from catalog=" +
            restoredFromCatalog +
            ", unresolved HDRP siblings=" +
            missingUrpCounterpart +
            ", skipped missing-script prefabs=" +
            skippedMissingScripts +
            ", retained entries=" +
            repaired.Count + ".");

        if (restoreLazyShardsAfterRepair &&
            repaired.Count > 0)
        {
            // note: A standalone repair started from production shards must finish in the same lazy runtime shape.
            WriteRuntimeShards(
                registry,
                repaired);
        }
    }

    [MenuItem(
        "Tools/YourQuest/Archived Tools/Legacy Runtime Registry/Prune Missing Scripts")]
    public static void PruneRuntimeRegistryMissingScripts()
    {
        if (_missingScriptPruneRegistry != null)
        {
            Debug.Log(
                "[YQRuntimeWorldAssetRegistryBuilder] Missing-script registry prune is already running.");
            return;
        }

        YQRuntimeWorldAssetRegistry registry =
            AssetDatabase.LoadAssetAtPath<
                YQRuntimeWorldAssetRegistry>(
                    RegistryPath);

        if (registry == null)
        {
            Debug.LogWarning(
                "[YQRuntimeWorldAssetRegistryBuilder] No runtime registry exists to prune.");
            return;
        }

        // note: This is intentionally incremental because imported prefab dependency graphs can be large and should not monopolize an editor frame.
        _missingScriptPruneRegistry = registry;
        _missingScriptPruneSource =
            new List<YQRuntimeWorldAssetEntry>(registry.Entries);
        _missingScriptPruneRetained =
            new List<YQRuntimeWorldAssetEntry>(_missingScriptPruneSource.Count);
        _missingScriptDependencyCache =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        _missingScriptPruneIndex = 0;
        _missingScriptPruneRemoved = 0;

        EditorApplication.update += ProcessRuntimeRegistryMissingScriptPrune;

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] Started missing-script registry prune for " +
            _missingScriptPruneSource.Count + " entries. Processing one entry per editor update.");
    }

    private static void ProcessRuntimeRegistryMissingScriptPrune()
    {
        if (_missingScriptPruneRegistry == null ||
            _missingScriptPruneSource == null ||
            _missingScriptPruneRetained == null)
        {
            FinishRuntimeRegistryMissingScriptPrune(false);
            return;
        }

        const int entriesPerEditorUpdate = 1;
        int processedThisUpdate = 0;
        while (processedThisUpdate < entriesPerEditorUpdate &&
               _missingScriptPruneIndex < _missingScriptPruneSource.Count)
        {
            YQRuntimeWorldAssetEntry entry =
                _missingScriptPruneSource[_missingScriptPruneIndex++];
            processedThisUpdate++;

            if (entry == null)
                continue;

            string path =
                YQRuntimeWorldAssetRegistry.NormalizePath(entry.assetPath);
            if (entry.prefab != null &&
                HasMissingScriptsInPrefabAsset(path, entry.prefab))
            {
                // note: These references deserialize transitively from Resources and can flood Play Mode before runtime code can reject them.
                _missingScriptPruneRemoved++;
                continue;
            }

            _missingScriptPruneRetained.Add(entry);
        }

        float progress = _missingScriptPruneSource.Count > 0
            ? (float)_missingScriptPruneIndex / _missingScriptPruneSource.Count
            : 1f;
        EditorUtility.DisplayProgressBar(
            "YourQuest Registry Cleanup",
            "Checking nested prefab dependencies...",
            progress);

        if (_missingScriptPruneIndex >= _missingScriptPruneSource.Count)
            FinishRuntimeRegistryMissingScriptPrune(true);
    }

    private static void FinishRuntimeRegistryMissingScriptPrune(bool complete)
    {
        EditorApplication.update -= ProcessRuntimeRegistryMissingScriptPrune;
        EditorUtility.ClearProgressBar();

        if (complete && _missingScriptPruneRegistry != null &&
            _missingScriptPruneRetained != null)
        {
            _missingScriptPruneRegistry.SetEntries(_missingScriptPruneRetained);
            EditorUtility.SetDirty(_missingScriptPruneRegistry);
            AssetDatabase.SaveAssets();
            YQRuntimeWorldAssetRegistry.ClearCachedInstance();

            Debug.Log(
                "[YQRuntimeWorldAssetRegistryBuilder] Missing-script registry prune complete. Removed=" +
                _missingScriptPruneRemoved + ", retained=" + _missingScriptPruneRetained.Count + ".");
        }

        _missingScriptPruneRegistry = null;
        _missingScriptPruneSource = null;
        _missingScriptPruneRetained = null;
        _missingScriptDependencyCache = null;
        _missingScriptPruneIndex = 0;
        _missingScriptPruneRemoved = 0;
    }

    [MenuItem(
        "Tools/YourQuest/Archived Tools/Legacy Runtime Registry/Build Hivemind URP Material Bindings")]
    public static void BeginHivemindUrpMaterialBindings()
    {
        if (_hivemindBindingTargets != null)
        {
            Debug.Log(
                "[YQRuntimeWorldAssetRegistryBuilder] " +
                "Hivemind URP material binding is already running.");

            return;
        }

        YQRuntimeWorldAssetRegistry registry =
            AssetDatabase.LoadAssetAtPath<
                YQRuntimeWorldAssetRegistry>(
                    RegistryPath);

        if (registry == null)
        {
            Debug.LogWarning(
                "[YQRuntimeWorldAssetRegistryBuilder] " +
                "No runtime registry exists to bind.");

            return;
        }

        EnsureFolderPath(
            HivemindUrpMaterialsFolder);

        _hivemindBindingRegistry =
            registry;

        _hivemindBindingEntries =
            new List<YQRuntimeWorldAssetEntry>(
                registry.Entries);

        _hivemindBindingTargets =
            new List<YQRuntimeWorldAssetEntry>();

        for (int i = 0;
             i < _hivemindBindingEntries.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                _hivemindBindingEntries[i];

            if (IsHivemindHdrpEntry(
                    entry))
            {
                _hivemindBindingTargets.Add(
                    entry);
            }
        }

        _hivemindConvertedMaterials =
            new Dictionary<int, Material>();

        _hivemindBindingIndex = 0;
        _hivemindBoundPrefabCount = 0;
        _hivemindBoundMaterialSlots = 0;
        _hivemindUnresolvedMaterialSlots = 0;

        EditorApplication.update +=
            ProcessHivemindUrpMaterialBindings;

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] " +
            "Started persistent Hivemind URP material binding for " +
            _hivemindBindingTargets.Count +
            " registry entries. Processing one prefab per editor update.");
    }

    private static void ProcessHivemindUrpMaterialBindings()
    {
        // note: Keep this deliberately small so a dense imported prefab cannot monopolize the editor frame.
        const int entriesPerEditorUpdate = 1;

        if (_hivemindBindingTargets == null ||
            _hivemindBindingRegistry == null)
        {
            FinishHivemindUrpMaterialBindings(
                false);

            return;
        }

        float progress =
            _hivemindBindingTargets.Count > 0
                ? (float)_hivemindBindingIndex /
                  _hivemindBindingTargets.Count
                : 1f;

        if (EditorUtility.DisplayCancelableProgressBar(
                "YourQuest Hivemind Compatibility",
                "Building persistent URP material bindings " +
                _hivemindBindingIndex +
                "/" +
                _hivemindBindingTargets.Count,
                progress))
        {
            // note: Cancelling leaves original imported assets untouched and discards only this unfinished registry update.
            FinishHivemindUrpMaterialBindings(
                false);

            return;
        }

        int end =
            Mathf.Min(
                _hivemindBindingIndex +
                entriesPerEditorUpdate,
                _hivemindBindingTargets.Count);

        for (; _hivemindBindingIndex < end;
             _hivemindBindingIndex++)
        {
            BindHivemindEntryMaterials(
                _hivemindBindingTargets[
                    _hivemindBindingIndex]);
        }

        if (_hivemindBindingIndex <
            _hivemindBindingTargets.Count)
        {
            if (_hivemindBindingIndex % 100 == 0)
            {
                Debug.Log(
                    "[YQRuntimeWorldAssetRegistryBuilder] " +
                    "Hivemind URP binding progress " +
                    _hivemindBindingIndex +
                    "/" +
                    _hivemindBindingTargets.Count + ".");
            }

            return;
        }

        FinishHivemindUrpMaterialBindings(
            true);
    }

    private static void BindHivemindEntryMaterials(
        YQRuntimeWorldAssetEntry entry)
    {
        if (entry == null)
            return;

        if (entry.material != null)
        {
            Material converted =
                GetOrCreatePersistentHivemindUrpMaterial(
                    entry.material,
                    null);

            if (converted != null)
            {
                entry.material =
                    converted;

                _hivemindBoundMaterialSlots++;
            }
            else
            {
                _hivemindUnresolvedMaterialSlots++;
            }

            return;
        }

        GameObject prefab =
            entry.prefab != null
                ? entry.prefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(
                    entry.assetPath);

        if (prefab == null)
            return;

        List<YQRuntimeWorldMaterialOverride> bindings =
            new List<YQRuntimeWorldMaterialOverride>();

        Renderer[] renderers =
            prefab.GetComponentsInChildren<Renderer>(
                true);

        for (int rendererGlobalIndex = 0;
             rendererGlobalIndex < renderers.Length;
             rendererGlobalIndex++)
        {
            Renderer renderer =
                renderers[rendererGlobalIndex];

            if (renderer == null ||
                renderer is ParticleSystemRenderer)
            {
                continue;
            }

            Material[] materials =
                renderer.sharedMaterials;

            int rendererIndex =
                GetRendererIndexOnTransform(
                    renderer);

            if (rendererIndex < 0 ||
                materials == null)
            {
                continue;
            }

            string transformPath =
                AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    prefab.transform);

            for (int materialIndex = 0;
                 materialIndex < materials.Length;
                 materialIndex++)
            {
                Material converted =
                    GetOrCreatePersistentHivemindUrpMaterial(
                        materials[materialIndex],
                        renderer);

                if (converted == null)
                {
                    _hivemindUnresolvedMaterialSlots++;
                    continue;
                }

                bindings.Add(
                    new YQRuntimeWorldMaterialOverride
                    {
                        transformPath = transformPath,
                        rendererIndex = rendererIndex,
                        materialIndex = materialIndex,
                        replacementMaterial = converted
                    });

                _hivemindBoundMaterialSlots++;
            }
        }

        entry.materialOverrides =
            bindings;

        _hivemindBoundPrefabCount++;
    }

    private static Material GetOrCreatePersistentHivemindUrpMaterial(
        Material source,
        Renderer renderer)
    {
        if (source == null)
            return GetOrCreateHivemindMissingMaterial();

        int sourceId =
            source.GetInstanceID();

        if (_hivemindConvertedMaterials.TryGetValue(
                sourceId,
                out Material cached) &&
            cached != null)
        {
            return cached;
        }

        string sourcePath =
            AssetDatabase.GetAssetPath(
                source);

        string sourceGuid =
            AssetDatabase.AssetPathToGUID(
                sourcePath);

        string safeId =
            string.IsNullOrWhiteSpace(sourceGuid)
                ? Mathf.Abs(sourceId).ToString("x")
                : sourceGuid;

        string outputPath =
            HivemindUrpMaterialsFolder +
            "/" +
            safeId +
            ".mat";

        Material converted =
            AssetDatabase.LoadAssetAtPath<Material>(
                outputPath);

        if (converted == null)
        {
            converted =
                YQRuntimeUrpMaterialRepair
                    .CreateEditorUrpLitMaterial(
                        source,
                        renderer);

            // note: A source shader can fail conversion; a persistent neutral URP material keeps that renderer spawnable without hiding the data failure.
            if (converted == null)
                converted = GetOrCreateHivemindMissingMaterial();

            if (converted == null)
                return null;

            if (!string.Equals(
                    AssetDatabase.GetAssetPath(converted),
                    HivemindMissingMaterialPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.CreateAsset(
                    converted,
                    outputPath);
            }
        }

        _hivemindConvertedMaterials[
            sourceId] =
            converted;

        return converted;
    }

    private static Material GetOrCreateHivemindMissingMaterial()
    {
        Material existing =
            AssetDatabase.LoadAssetAtPath<Material>(
                HivemindMissingMaterialPath);

        if (existing != null)
            return existing;

        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Standard");
        }

        if (shader == null)
            return null;

        // note: This asset is only used for missing or unconvertible source slots, making those rare prefabs visible and diagnosable instead of non-spawnable.
        Material fallback =
            new Material(shader)
            {
                name = "YQ Hivemind Missing Material"
            };

        fallback.color =
            new Color(
                0.45f,
                0.45f,
                0.45f,
                1f);

        AssetDatabase.CreateAsset(
            fallback,
            HivemindMissingMaterialPath);

        return fallback;
    }

    private static bool IsHivemindHdrpEntry(
        YQRuntimeWorldAssetEntry entry)
    {
        if (entry == null ||
            string.IsNullOrWhiteSpace(entry.assetPath))
        {
            return false;
        }

        string path =
            entry.assetPath.Replace(
                '\\',
                '/');

        return path.IndexOf(
                   "/HIVEMIND/",
                   System.StringComparison.OrdinalIgnoreCase) >= 0 &&
               path.IndexOf(
                   "/HDRP",
                   System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void FinishHivemindUrpMaterialBindings(
        bool complete)
    {
        EditorApplication.update -=
            ProcessHivemindUrpMaterialBindings;

        // note: Always clear the modal progress UI, including a user-cancelled or interrupted pass.
        EditorUtility.ClearProgressBar();

        if (complete &&
            _hivemindBindingRegistry != null &&
            _hivemindBindingEntries != null)
        {
            _hivemindBindingRegistry.SetEntries(
                _hivemindBindingEntries);

            EditorUtility.SetDirty(
                _hivemindBindingRegistry);

            AssetDatabase.SaveAssets();

            Debug.Log(
                "[YQRuntimeWorldAssetRegistryBuilder] " +
                "Hivemind URP bindings complete. Prefabs=" +
                _hivemindBoundPrefabCount +
                ", material slots=" +
                _hivemindBoundMaterialSlots +
                ", unresolved slots=" +
                _hivemindUnresolvedMaterialSlots + ".");
        }

        _hivemindBindingRegistry =
            null;
        _hivemindBindingEntries =
            null;
        _hivemindBindingTargets =
            null;
        _hivemindConvertedMaterials =
            null;
    }

    private static bool HasMissingScripts(
        GameObject prefab)
    {
        if (prefab == null)
            return false;

        try
        {
            return GameObjectUtility
                       .GetMonoBehavioursWithMissingScriptCount(
                           prefab) >
                   0;
        }
        catch
        {
            // note: Unknown imported prefab states stay available rather than being removed on an inconclusive editor check.
            return false;
        }
    }

    private static bool HasMissingScriptsInPrefabAsset(
        string assetPath,
        GameObject fallbackPrefab)
    {
        if (HasMissingScripts(fallbackPrefab))
            return true;

        if (!string.IsNullOrWhiteSpace(assetPath))
        {
            GameObject editablePrefab = null;
            try
            {
                // note: Loading prefab contents catches nested missing scripts that an already-deserialized registry reference can conceal.
                editablePrefab = PrefabUtility.LoadPrefabContents(assetPath);
                if (HasMissingScripts(editablePrefab))
                    return true;
            }
            catch
            {
                // note: Fall through to the serialized object when an imported package blocks editable prefab loading.
            }
            finally
            {
                if (editablePrefab != null)
                    PrefabUtility.UnloadPrefabContents(editablePrefab);
            }
        }

        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        string[] dependencies =
            AssetDatabase.GetDependencies(assetPath, true);
        for (int i = 0; i < dependencies.Length; i++)
        {
            string dependency = dependencies[i];
            if (string.IsNullOrWhiteSpace(dependency) ||
                !dependency.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (PrefabYamlContainsMissingScript(dependency))
                return true;
        }

        return false;
    }

    private static bool PrefabYamlContainsMissingScript(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        if (_missingScriptDependencyCache != null &&
            _missingScriptDependencyCache.TryGetValue(assetPath, out bool cached))
        {
            return cached;
        }

        bool hasMissingScript = false;
        try
        {
            if (File.Exists(assetPath))
            {
                // note: Unity serializes an unresolved MonoBehaviour as fileID 0 even when the missing component belongs to a nested prefab.
                string yaml = File.ReadAllText(assetPath);
                hasMissingScript = yaml.IndexOf(
                    "m_Script: {fileID: 0",
                    StringComparison.Ordinal) >= 0 ||
                    PrefabYamlReferencesMissingScriptGuid(yaml);
            }
        }
        catch
        {
            // note: An unreadable third-party asset is not removed solely because inspection was inconclusive.
            hasMissingScript = false;
        }

        if (_missingScriptDependencyCache != null)
            _missingScriptDependencyCache[assetPath] = hasMissingScript;
        return hasMissingScript;
    }

    private static bool PrefabYamlReferencesMissingScriptGuid(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return false;

        const string scriptMarker = "m_Script: {fileID:";
        const string guidMarker = "guid: ";
        int searchIndex = 0;
        while (searchIndex < yaml.Length)
        {
            int scriptIndex = yaml.IndexOf(
                scriptMarker,
                searchIndex,
                StringComparison.Ordinal);
            if (scriptIndex < 0)
                return false;

            int lineEnd = yaml.IndexOf('\n', scriptIndex);
            if (lineEnd < 0)
                lineEnd = yaml.Length;

            int guidIndex = yaml.IndexOf(
                guidMarker,
                scriptIndex,
                lineEnd - scriptIndex,
                StringComparison.Ordinal);
            if (guidIndex >= 0)
            {
                int guidStart = guidIndex + guidMarker.Length;
                int guidEnd = yaml.IndexOf(',', guidStart);
                if (guidEnd < 0 || guidEnd > lineEnd)
                    guidEnd = yaml.IndexOf('}', guidStart);
                if (guidEnd < 0 || guidEnd > lineEnd)
                    guidEnd = lineEnd;

                string guid = yaml.Substring(guidStart, guidEnd - guidStart).Trim();
                if (!string.IsNullOrWhiteSpace(guid) &&
                    string.IsNullOrWhiteSpace(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    // note: A nonzero script fileID with an unknown GUID is Unity's serialized form of a missing imported script.
                    return true;
                }
            }

            searchIndex = lineEnd + 1;
        }

        return false;
    }

    private static int RestoreMissingRegistryEntriesFromCatalog(
        List<YQRuntimeWorldAssetEntry> entries)
    {
        if (entries == null)
            return 0;

        YQDiscoveredWorldAssetCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                YQDiscoveredWorldAssetCatalog>(
                    DiscoveredCatalogPath);

        if (catalog == null)
            return 0;

        HashSet<string> knownPaths =
            new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < entries.Count;
             i++)
        {
            if (entries[i] != null)
            {
                knownPaths.Add(
                    YQRuntimeWorldAssetRegistry.NormalizePath(
                        entries[i].assetPath));
            }
        }

        int restored = 0;
        IReadOnlyList<GeneratedAssetReferenceRecord> discovered =
            catalog.Entries;

        for (int i = 0;
             i < discovered.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                discovered[i];

            if (reference == null ||
                string.IsNullOrWhiteSpace(reference.assetPath))
            {
                continue;
            }

            string path =
                YQRuntimeWorldAssetRegistry.NormalizePath(
                    reference.assetPath);

            if (string.IsNullOrWhiteSpace(path) ||
                !knownPaths.Add(path))
            {
                continue;
            }

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    path);

            GameObject prefab =
                material == null
                    ? AssetDatabase.LoadAssetAtPath<GameObject>(
                        path)
                    : null;

            if (material == null &&
                prefab == null)
            {
                continue;
            }

            // note: Catalog restoration preserves original imported references and never runs a broad asset search.
            entries.Add(
                new YQRuntimeWorldAssetEntry
                {
                    assetPath = path,
                    material = material,
                    prefab = prefab,
                    materialOverrides =
                        new List<
                            YQRuntimeWorldMaterialOverride>()
                });

            restored++;
        }

        return restored;
    }

    private static string BuildUrpSiblingPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        string repaired =
            path.Replace(
                "/HDRP(Default)/",
                "/URP/",
                System.StringComparison.OrdinalIgnoreCase);

        repaired =
            repaired.Replace(
                "/HDRP (Default)/",
                "/URP/",
                System.StringComparison.OrdinalIgnoreCase);

        return repaired.Replace(
            "/HDRP/",
            "/URP/",
            System.StringComparison.OrdinalIgnoreCase);
    }

    private static void RebuildRegistryInternal(
        bool includeDiscoveredAssets)
    {
        EnsureFolderPath(
            RegistryFolder);

        List<GeneratedAssetReferenceRecord> discoveredReferences =
            includeDiscoveredAssets
                ? BuildDiscoveredAssetReferences()
                : new List<GeneratedAssetReferenceRecord>();

        if (includeDiscoveredAssets)
        {
            SaveDiscoveredCatalog(
                discoveredReferences);
        }

        GeneratedWorldPlanRecord plan =
            BuildSyntheticPalettePlan();

        YQWorldAssetCatalog.EnsureAssetPalettes(
            plan);

        List<GeneratedAssetReferenceRecord> references =
            CollectUniqueAssetReferences(
                plan);

        if (includeDiscoveredAssets)
        {
            MergeUniqueAssetReferences(
                discoveredReferences,
                references);
        }

        HashSet<string> discoveredPaths =
            BuildAssetPathSet(
                discoveredReferences);

        List<YQRuntimeWorldAssetEntry> entries =
            new List<YQRuntimeWorldAssetEntry>();

        int prefabResolved = 0;
        int materialResolved = 0;
        int unresolved = 0;
        int bakedMaterialOverrides = 0;
        int prefabsWithMaterialOverrides = 0;

        for (int i = 0;
             i < references.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                references[i];

            if (reference == null)
                continue;

            string path =
                YQRuntimeWorldAssetRegistry.NormalizePath(
                    reference.assetPath);

            if (string.IsNullOrWhiteSpace(path))
                continue;

            YQRuntimeWorldAssetEntry entry =
                new YQRuntimeWorldAssetEntry
                {
                    assetPath = path,
                    materialOverrides =
                        new List<
                            YQRuntimeWorldMaterialOverride>()
                };

            bool wantsMaterial =
                string.Equals(
                    reference.assetType,
                    "material",
                    StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(
                    ".mat",
                    StringComparison.OrdinalIgnoreCase);

            if (wantsMaterial)
            {
                entry.material =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        path);

                if (entry.material != null)
                {
                    materialResolved++;
                }
                else
                {
                    unresolved++;

                    Debug.LogWarning(
                        "[YQRuntimeWorldAssetRegistryBuilder] " +
                        "Unresolved material: " +
                        path);
                }
            }
            else
            {
                entry.prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        path);

                if (entry.prefab != null)
                {
                    prefabResolved++;

                    bool isDiscoveredPath =
                        discoveredPaths.Contains(
                            path);

                    // note: Discovered prefabs skip expensive material-override baking; curated paths keep the existing repair behavior.
                    entry.materialOverrides =
                        isDiscoveredPath
                            ? new List<
                                YQRuntimeWorldMaterialOverride>()
                            : BuildMaterialOverrides(
                                entry.prefab);

                    if (entry.materialOverrides != null &&
                        entry.materialOverrides.Count > 0)
                    {
                        prefabsWithMaterialOverrides++;

                        bakedMaterialOverrides +=
                            entry.materialOverrides.Count;

                        Debug.Log(
                            "[YQRuntimeWorldAssetRegistryBuilder] " +
                            "Baked " +
                            entry.materialOverrides.Count +
                            " material override(s) for: " +
                            path);
                    }
                }
                else
                {
                    // Defensive fallback in case an asset was
                    // cataloged as a prefab but is actually a Material.
                    entry.material =
                        AssetDatabase.LoadAssetAtPath<Material>(
                            path);

                    if (entry.material != null)
                    {
                        materialResolved++;
                    }
                    else
                    {
                        unresolved++;

                        Debug.LogWarning(
                            "[YQRuntimeWorldAssetRegistryBuilder] " +
                            "Unresolved asset: " +
                            path);
                    }
                }
            }

            entries.Add(
                entry);
        }

        YQRuntimeWorldAssetRegistry registry =
            AssetDatabase.LoadAssetAtPath<
                YQRuntimeWorldAssetRegistry>(
                    RegistryPath);

        if (registry == null)
        {
            registry =
                ScriptableObject.CreateInstance<
                    YQRuntimeWorldAssetRegistry>();

            AssetDatabase.CreateAsset(
                registry,
                RegistryPath);
        }

        // note: Editor repair stages operate on the complete list; the final release step converts it back into lazy shards.
        registry.SetLazyResourceShards(
            false);

        registry.SetEntries(
            entries);

        EditorUtility.SetDirty(
            registry);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        YQRuntimeWorldAssetRegistry.ClearCachedInstance();

        LogPaletteCoverage(
            plan);

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] COMPLETE\n" +
            "Registry: " +
            RegistryPath +
            "\n" +
            "Unique referenced paths: " +
            references.Count +
            "\n" +
            "Discovered catalog entries: " +
            discoveredReferences.Count +
            "\n" +
            "Entries written: " +
            entries.Count +
            "\n" +
            "Prefabs resolved: " +
            prefabResolved +
            "\n" +
            "Materials resolved: " +
            materialResolved +
            "\n" +
            "Prefabs with baked material overrides: " +
            prefabsWithMaterialOverrides +
            "\n" +
            "Material overrides baked: " +
            bakedMaterialOverrides +
            "\n" +
            "Unresolved: " +
            unresolved);
    }

    private static List<GeneratedAssetReferenceRecord>
        BuildDiscoveredAssetReferences()
    {
        List<GeneratedAssetReferenceRecord> result =
            new List<GeneratedAssetReferenceRecord>();

        HashSet<string> seen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        string[] validPrefabRoots =
            GetValidDiscoveryRoots(
                PrefabDiscoveryRoots);

        string[] validMaterialRoots =
            GetValidDiscoveryRoots(
                MaterialDiscoveryRoots);

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] " +
            "Discovery roots available: prefab=" +
            validPrefabRoots.Length +
            ", material=" +
            validMaterialRoots.Length);

        // note: Editor discovery widens the approved asset library while still mapping everything to semantic slots.
        AddDiscoveredPrefabs(
            validPrefabRoots,
            result,
            seen);

        AddDiscoveredMaterials(
            validMaterialRoots,
            result,
            seen);

        result.Sort(
            (a, b) =>
                string.Compare(
                    a != null
                        ? a.assetPath
                        : string.Empty,

                    b != null
                        ? b.assetPath
                        : string.Empty,

                    StringComparison.OrdinalIgnoreCase));

        return result;
    }

    private static void AddDiscoveredPrefabs(
        string[] validRoots,
        List<GeneratedAssetReferenceRecord> result,
        HashSet<string> seen)
    {
        if (validRoots == null ||
            result == null ||
            seen == null)
        {
            return;
        }

        List<string> paths =
            FindAssetPathsByExtension(
                validRoots,
                ".prefab",
                MaxDiscoveredPrefabsPerRoot);

        int useful =
            0;

        int classified =
            0;

        int before =
            result.Count;

        for (int i = 0;
             i < paths.Count;
             i++)
        {
            string path =
                paths[i];

            if (!IsUsefulPrefabPath(
                    path))
            {
                continue;
            }

            useful++;

            string slot =
                ResolvePrefabSlot(
                    path);

            if (string.IsNullOrWhiteSpace(
                    slot))
            {
                continue;
            }

            classified++;

            string[] styles =
                ResolveStylesForAsset(
                    path,
                    slot);

            for (int styleIndex = 0;
                 styleIndex < styles.Length;
                 styleIndex++)
            {
                AddDiscoveredReference(
                    result,
                    seen,
                    path,
                    "prefab",
                    slot,
                    styles[styleIndex]);
            }
        }

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] " +
            "Prefab discovery: candidates=" +
            paths.Count +
            ", useful=" +
            useful +
            ", classified=" +
            classified +
            ", semantic entries added=" +
            (result.Count - before));
    }

    private static void AddDiscoveredMaterials(
        string[] validRoots,
        List<GeneratedAssetReferenceRecord> result,
        HashSet<string> seen)
    {
        if (validRoots == null ||
            result == null ||
            seen == null)
        {
            return;
        }

        List<string> paths =
            FindAssetPathsByExtension(
                validRoots,
                ".mat",
                MaxDiscoveredMaterialsPerRoot);

        int useful =
            0;

        int before =
            result.Count;

        for (int i = 0;
             i < paths.Count;
             i++)
        {
            string path =
                paths[i];

            if (!IsUsefulMaterialPath(
                    path))
            {
                continue;
            }

            useful++;

            string[] styles =
                ResolveStylesForAsset(
                    path,
                    YQWorldAssetCatalog.SlotTerrain);

            for (int styleIndex = 0;
                 styleIndex < styles.Length;
                 styleIndex++)
            {
                AddDiscoveredReference(
                    result,
                    seen,
                    path,
                    "material",
                    YQWorldAssetCatalog.SlotTerrain,
                    styles[styleIndex]);
            }
        }

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] " +
            "Material discovery: candidates=" +
            paths.Count +
            ", useful=" +
            useful +
            ", semantic entries added=" +
            (result.Count - before));
    }

    private static void AddDiscoveredReference(
        List<GeneratedAssetReferenceRecord> result,
        HashSet<string> seen,
        string path,
        string assetType,
        string slot,
        string style)
    {
        if (result == null ||
            seen == null ||
            string.IsNullOrWhiteSpace(path) ||
            string.IsNullOrWhiteSpace(slot) ||
            string.IsNullOrWhiteSpace(style))
        {
            return;
        }

        string normalizedPath =
            path.Replace(
                '\\',
                '/');

        string key =
            normalizedPath +
            "|" +
            slot +
            "|" +
            style;

        if (!seen.Add(
                key))
        {
            return;
        }

        float footprintX =
            ResolveFootprint(
                slot);

        float footprintZ =
            footprintX;

        if (string.Equals(
                assetType,
                "prefab",
                StringComparison.OrdinalIgnoreCase) &&
            IsFootprintCriticalSlot(
                slot) &&
            TryMeasurePrefabFootprint(
                normalizedPath,
                out Vector2 measuredFootprint))
        {
            // note: Buildings and sites persist their real rendered footprint so layout curation can reserve space from authored dimensions.
            footprintX = measuredFootprint.x;
            footprintZ = measuredFootprint.y;
        }

        GeneratedAssetReferenceRecord record =
            new GeneratedAssetReferenceRecord
            {
                assetKey = NormalizeKey(
                    normalizedPath),
                assetPath = normalizedPath,
                assetType = assetType,
                slotTag = slot,
                weight = ResolveWeight(
                    slot),
                scaleMin = ResolveScaleMin(
                    slot),
                scaleMax = ResolveScaleMax(
                    slot),
                footprintX = footprintX,
                footprintZ = footprintZ,
                placementRule = ResolvePlacementRule(
                    slot),
                rotationRule = ResolveRotationRule(
                    slot),
                allowRepeat = AllowsRepeat(
                    slot),
                blocksNav = BlocksNav(
                    slot),
                notes = "Editor-discovered procedural asset."
            };

        record.EnsureCollections();

        AddUnique(
            record.styleTags,
            style);

        AddSemanticTags(
            record,
            normalizedPath);

        result.Add(
            record);
    }

    private static bool IsFootprintCriticalSlot(
        string slot)
    {
        return string.Equals(slot, YQWorldAssetCatalog.SlotSettlementBuilding, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(slot, YQWorldAssetCatalog.SlotLargeStructure, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(slot, YQWorldAssetCatalog.SlotEnemySite, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMeasurePrefabFootprint(
        string assetPath,
        out Vector2 footprint)
    {
        footprint = Vector2.zero;

        if (!TryMeasurePrefabLocalBounds(
                assetPath,
                out Bounds aggregate))
        {
            return false;
        }

        // note: Clamp corrupt import bounds while retaining enough range for castles, hospitals, and other deliberate landmarks.
        footprint =
            new Vector2(
                Mathf.Clamp(Mathf.Abs(aggregate.size.x), 0.5f, 64f),
                Mathf.Clamp(Mathf.Abs(aggregate.size.z), 0.5f, 64f));

        return true;
    }

    public static bool TryMeasurePrefabLocalBounds(
        string assetPath,
        out Bounds aggregate)
    {
        aggregate =
            default;

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                assetPath);

        if (prefab == null)
            return false;

        bool hasBounds =
            false;

        Matrix4x4 rootWorldToLocal =
            prefab.transform.worldToLocalMatrix;

        MeshFilter[] filters =
            prefab.GetComponentsInChildren<MeshFilter>(
                true);

        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter == null || filter.sharedMesh == null)
                continue;

            EncapsulateTransformedBounds(
                ref aggregate,
                ref hasBounds,
                filter.sharedMesh.bounds,
                rootWorldToLocal * filter.transform.localToWorldMatrix);
        }

        SkinnedMeshRenderer[] skinned =
            prefab.GetComponentsInChildren<SkinnedMeshRenderer>(
                true);

        for (int i = 0; i < skinned.Length; i++)
        {
            SkinnedMeshRenderer renderer = skinned[i];
            if (renderer == null || renderer.sharedMesh == null)
                continue;

            EncapsulateTransformedBounds(
                ref aggregate,
                ref hasBounds,
                renderer.localBounds,
                rootWorldToLocal * renderer.transform.localToWorldMatrix);
        }

        if (!hasBounds)
            return false;

        return true;
    }

    private static void EncapsulateTransformedBounds(
        ref Bounds aggregate,
        ref bool hasBounds,
        Bounds source,
        Matrix4x4 localToRoot)
    {
        Vector3 min = source.min;
        Vector3 max = source.max;

        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    // note: Every mesh-bounds corner is converted to prefab-root space so nested authored transforms remain part of the measured lot.
                    Vector3 point =
                        localToRoot.MultiplyPoint3x4(
                            new Vector3(
                                x == 0 ? min.x : max.x,
                                y == 0 ? min.y : max.y,
                                z == 0 ? min.z : max.z));

                    if (!hasBounds)
                    {
                        aggregate = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        aggregate.Encapsulate(point);
                    }
                }
            }
        }
    }

    private static string[] GetValidDiscoveryRoots(
        string[] roots)
    {
        List<string> valid =
            new List<string>();

        if (roots == null)
            return valid.ToArray();

        for (int i = 0;
             i < roots.Length;
             i++)
        {
            string root =
                roots[i];

            if (AssetDatabase.IsValidFolder(
                    root))
            {
                valid.Add(
                    root);
            }
        }

        return valid.ToArray();
    }

    private static List<string> FindAssetPathsByExtension(
        string[] validRoots,
        string extension,
        int maxPerRoot)
    {
        List<string> result =
            new List<string>();

        HashSet<string> seen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (validRoots == null ||
            string.IsNullOrWhiteSpace(extension))
        {
            return result;
        }

        for (int i = 0;
             i < validRoots.Length;
             i++)
        {
            string root =
                validRoots[i];

            int rootAdded =
                0;

            if (!Directory.Exists(
                    root))
            {
                continue;
            }

            foreach (string rawPath in Directory.EnumerateFiles(
                         root,
                         "*" + extension,
                         SearchOption.AllDirectories))
            {
                string path =
                    rawPath.Replace(
                        '\\',
                        '/');

                if (string.IsNullOrWhiteSpace(path) ||
                    !path.EndsWith(
                        extension,
                        StringComparison.OrdinalIgnoreCase) ||
                    !seen.Add(
                        path))
                {
                    continue;
                }

                result.Add(
                    path);

                rootAdded++;

                if (maxPerRoot > 0 &&
                    rootAdded >= maxPerRoot)
                {
                    break;
                }
            }

            // note: Folder-level counts make discovery safe to inspect before any registry asset is written.
            Debug.Log(
                "[YQRuntimeWorldAssetRegistryBuilder] " +
                "Discovery scan " +
                root +
                " " +
                extension +
                ": kept=" +
                rootAdded +
                (maxPerRoot > 0 &&
                 rootAdded >= maxPerRoot
                    ? " (cap reached)"
                    : string.Empty));
        }

        return result;
    }

    private static void SaveDiscoveredCatalog(
        List<GeneratedAssetReferenceRecord> references)
    {
        YQDiscoveredWorldAssetCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                YQDiscoveredWorldAssetCatalog>(
                    DiscoveredCatalogPath);

        if (catalog == null)
        {
            catalog =
                ScriptableObject.CreateInstance<
                    YQDiscoveredWorldAssetCatalog>();

            AssetDatabase.CreateAsset(
                catalog,
                DiscoveredCatalogPath);
        }

        catalog.SetEntries(
            references);

        EditorUtility.SetDirty(
            catalog);

        YQDiscoveredWorldAssetCatalog.ClearCachedInstance();
    }

    private static void MergeUniqueAssetReferences(
        List<GeneratedAssetReferenceRecord> source,
        List<GeneratedAssetReferenceRecord> destination)
    {
        if (source == null ||
            destination == null)
        {
            return;
        }

        HashSet<string> seen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < destination.Count;
             i++)
        {
            if (destination[i] == null)
                continue;

            string path =
                YQRuntimeWorldAssetRegistry.NormalizePath(
                    destination[i].assetPath);

            if (!string.IsNullOrWhiteSpace(
                    path))
            {
                seen.Add(
                    path);
            }
        }

        for (int i = 0;
             i < source.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                source[i];

            if (reference == null)
                continue;

            string path =
                YQRuntimeWorldAssetRegistry.NormalizePath(
                    reference.assetPath);

            if (string.IsNullOrWhiteSpace(
                    path) ||
                !seen.Add(
                    path))
            {
                continue;
            }

            destination.Add(
                reference);
        }
    }

    private static HashSet<string> BuildAssetPathSet(
        List<GeneratedAssetReferenceRecord> references)
    {
        HashSet<string> result =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (references == null)
            return result;

        for (int i = 0;
             i < references.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                references[i];

            if (reference == null)
                continue;

            string path =
                YQRuntimeWorldAssetRegistry.NormalizePath(
                    reference.assetPath);

            if (!string.IsNullOrWhiteSpace(
                    path))
            {
                result.Add(
                    path);
            }
        }

        return result;
    }

    private static bool IsUsefulPrefabPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string normalized =
            path.Replace(
                '\\',
                '/');

        string search =
            NormalizeSearchText(
                normalized);

        if (ContainsAny(
                search,
                "demo",
                "showcase",
                "example",
                "sample",
                "preview",
                "readme",
                "scene",
                "editor",
                "audio",
                "sound",
                "music",
                "particle",
                "particles",
                "vfx",
                "sfx",
                "animation",
                "animator",
                "exported meshes",
                "no assigned materials",
                "weapon",
                "sword",
                "dagger",
                "axe",
                "bow",
                "staff",
                "armor",
                "helmet",
                "camera",
                "controller",
                "manager",
                "canvas",
                "eventsystem",
                "ui"))
        {
            return false;
        }

        return normalized.EndsWith(
            ".prefab",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUsefulMaterialPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string normalized =
            path.Replace(
                '\\',
                '/');

        string search =
            NormalizeSearchText(
                normalized);

        if (!normalized.EndsWith(
                ".mat",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ContainsAny(
                search,
                "demo",
                "showcase",
                "example",
                "sample",
                "preview",
                "readme",
                "editor",
                "audio",
                "sound",
                "icon",
                "ui"))
        {
            return false;
        }

        // note: Materials are only imported for procedural terrain when their names indicate ground or surface use.
        return ContainsAny(
            search,
            "ground",
            "terrain",
            "dirt",
            "sand",
            "stone",
            "rock",
            "grass",
            "mud",
            "floor",
            "road",
            "path");
    }

    private static string ResolvePrefabSlot(
        string path)
    {
        // note: Pack-folder names describe visual family, not placement; slot attribution must come from the prefab itself.
        string prefabName =
            Path.GetFileNameWithoutExtension(
                path ?? string.Empty);

        string search =
            NormalizeSearchText(
                prefabName);

        if (ContainsAny(
                search,
                "chest",
                "coffer",
                "loot"))
        {
            return YQWorldAssetCatalog.SlotLootContainer;
        }

        if (ContainsAny(
                search,
                "camp",
                "encampment",
                "outpost",
                "redoubt",
                "watchpost",
                "watchtower",
                "lair",
                "nest",
                "crypt",
                "cave",
                "mine",
                "ruin",
                "burrow",
                "shipwreck",
                "shrine"))
        {
            // note: Enemy sites are places and modules, not creature bodies, combat VFX, or equipment pieces.
            return YQWorldAssetCatalog.SlotEnemySite;
        }

        if (ContainsAny(
                search,
                "tree",
                "bush",
                "grass",
                "fern",
                "cactus",
                "flower",
                "foliage",
                "weed",
                "plant",
                "shroom",
                "mushroom"))
        {
            return YQWorldAssetCatalog.SlotVegetation;
        }

        if (ContainsAny(
                search,
                "boulder",
                "rock",
                "stone",
                "rubble",
                "debris",
                "mountain",
                "cliff") &&
            !ContainsAny(
                search,
                "building",
                "house",
                "hut",
                "shack",
                "church",
                "saloon",
                "stable",
                "tower",
                "barn",
                "cabin",
                "hall"))
        {
            return YQWorldAssetCatalog.SlotRock;
        }

        if (ContainsAny(
                search,
                "lamp",
                "lantern",
                "torch",
                "fire",
                "candle",
                "light",
                "brazier",
                "sconce"))
        {
            return YQWorldAssetCatalog.SlotLighting;
        }

        if (ContainsAny(
                search,
                "door",
                "gate",
                "portcullis"))
        {
            return YQWorldAssetCatalog.SlotDoor;
        }

        if (ContainsAny(
                search,
                "roof",
                "awning",
                "canopy"))
        {
            return YQWorldAssetCatalog.SlotRoof;
        }

        if (ContainsAny(
                search,
                "wall",
                "fence",
                "corner",
                "pillar",
                "column"))
        {
            return YQWorldAssetCatalog.SlotWall;
        }

        if (ContainsAny(
                search,
                "road",
                "path",
                "bridge",
                "stair",
                "steps",
                "walkway",
                "plank"))
        {
            return YQWorldAssetCatalog.SlotPath;
        }

        if (ContainsAny(
                search,
                "ground",
                "floor",
                "tile",
                "platform",
                "carpet",
                "rug"))
        {
            return YQWorldAssetCatalog.SlotFloor;
        }

        if (ContainsAny(
                search,
                "house",
                "hut",
                "shack",
                "building",
                "church",
                "saloon",
                "stable",
                "tower",
                "barn",
                "cabin",
                "hall"))
        {
            return YQWorldAssetCatalog.SlotSettlementBuilding;
        }

        if (ContainsAny(
                search,
                "ruin",
                "statue",
                "obelisk",
                "monument",
                "arch",
                "ship",
                "container",
                "biomass"))
        {
            return YQWorldAssetCatalog.SlotLargeStructure;
        }

        if (ContainsAny(
                search,
                "painting",
                "curtain",
                "banner",
                "shield",
                "sign",
                "plaque"))
        {
            return YQWorldAssetCatalog.SlotWallDeco;
        }

        if (ContainsAny(
                search,
                "chair",
                "table",
                "shelf",
                "cabinet",
                "bed",
                "book",
                "desk",
                "stool"))
        {
            return YQWorldAssetCatalog.SlotInteriorDeco;
        }

        if (ContainsAny(
                search,
                "barrel",
                "crate",
                "box",
                "sack",
                "vase",
                "pot",
                "cart",
                "wagon",
                "well",
                "bucket",
                "bench"))
        {
            return YQWorldAssetCatalog.SlotFloorDeco;
        }

        // note: Named modular packs contain many neutral set-dressing meshes; retain them as exterior decor instead of silently dropping half the spawnable library.
        return YQWorldAssetCatalog.SlotExteriorDeco;
    }

    private static string[] ResolveStylesForAsset(
        string path,
        string slot)
    {
        string search =
            NormalizeSearchText(
                path);

        if (ContainsAny(
                search,
                "nordic village"))
        {
            return One(
                "nordic_forest");
        }

        if (ContainsAny(
                search,
                "medieval viking village"))
        {
            return One(
                "viking_rural");
        }

        if (ContainsAny(
                search,
                "ancient desert ruins"))
        {
            return One(
                "ancient_desert_ruins");
        }

        if (ContainsAny(
                search,
                "western desert town"))
        {
            return One(
                "western_desert_town");
        }

        if (ContainsAny(
                search,
                "asian dynasty environment"))
        {
            return One(
                "asian_dynasty");
        }

        if (ContainsAny(
                search,
                "persepolis empire environment"))
        {
            return One(
                "persepolis_empire");
        }

        if (ContainsAny(
                search,
                "victorian mansion environment"))
        {
            return One(
                "victorian_mansion");
        }

        if (ContainsAny(
                search,
                "container district"))
        {
            return One(
                "container_district");
        }

        if (ContainsAny(
                search,
                "bio horror sci fi environment"))
        {
            return One(
                "bio_horror_scifi");
        }

        if (ContainsAny(
                search,
                "sci fi engineers room"))
        {
            return One(
                "scifi_engineers_room");
        }

        if (ContainsAny(
                search,
                "hivemind pirate island",
                "pirate island"))
        {
            // note: Hivemind pirate assets are kept as their own coastal modular style family.
            return One(
                "hivemind_pirate_island");
        }

        if (ContainsAny(
                search,
                "hivemind medieval kingdom",
                "medieval kingdom"))
        {
            // note: Medieval Kingdom is a broad castle/town kit with its own modular style bucket.
            return One(
                "hivemind_medieval_kingdom");
        }

        if (ContainsAny(
                search,
                "hivemind military camp",
                "military camp"))
        {
            // note: Military camp assets are procedural encampment modules, not generic village dressing.
            return One(
                "hivemind_military_camp");
        }

        if (ContainsAny(
                search,
                "hivemind gothic cathedral",
                "gothic cathedral"))
        {
            // note: Gothic Cathedral assets get a dedicated cathedral/crypt palette for holy or haunted sites.
            return One(
                "hivemind_gothic_cathedral");
        }

        if (ContainsAny(
                search,
                "hivemind cyberpunk city",
                "cyberpunk city"))
        {
            // note: Cyberpunk City supports dense neon/industrial regions without borrowing clean sci-fi rooms.
            return One(
                "hivemind_cyberpunk_city");
        }

        if (ContainsAny(
                search,
                "hivemind gladitor arena",
                "hivemind gladiator arena",
                "gladitor arena",
                "gladiator arena"))
        {
            // note: The imported folder misspells Gladiator, so both spellings map to the arena style.
            return One(
                "hivemind_gladiator_arena");
        }

        if (ContainsAny(
                search,
                "hivemind rural town",
                "rural town"))
        {
            // note: Rural town assets expand everyday settlement variety.
            return One(
                "hivemind_rural_town");
        }

        if (ContainsAny(
                search,
                "hivemind modular viking village",
                "modular viking village"))
        {
            // note: Modular Viking Village stays separate from the older Viking pack so both can be weighted distinctly.
            return One(
                "hivemind_modular_viking_village");
        }

        if (ContainsAny(
                search,
                "hivemind town smith",
                "town smith"))
        {
            // note: Town Smith contributes forge, shop, and craft props for settlement economies.
            return One(
                "hivemind_town_smith");
        }

        if (ContainsAny(
                search,
                "hivemind haunted village",
                "haunted village"))
        {
            // note: Haunted Village gets its own mood bucket instead of generic forest-village selection.
            return One(
                "hivemind_haunted_village");
        }

        if (ContainsAny(
                search,
                "hivemind mystic dungeon",
                "mystic dungeon"))
        {
            // note: Mystic Dungeon supplies dungeon rooms and ritual props for underground hostile sites.
            return One(
                "hivemind_mystic_dungeon");
        }

        if (ContainsAny(
                search,
                "hivemind mountain temple",
                "mountain temple"))
        {
            // note: Mountain Temple supports high-altitude shrine and ruin layouts.
            return One(
                "hivemind_mountain_temple");
        }

        if (ContainsAny(
                search,
                "hivemind native american village",
                "native american village"))
        {
            // note: The source pack name is preserved only for detection; generation sees a woodland village style.
            return One(
                "hivemind_woodland_village");
        }

        if (ContainsAny(
                search,
                "hivemind witch house",
                "witch house"))
        {
            // note: Witch House supports isolated cottage, occult interior, and swampy exterior requests.
            return One(
                "hivemind_witch_house");
        }

        if (ContainsAny(
                search,
                "hivemind cave of hidden tomb",
                "cave of hidden tomb",
                "hidden tomb"))
        {
            // note: Cave of Hidden Tomb is a cave/tomb encounter kit, not a settlement kit.
            return One(
                "hivemind_cave_tomb");
        }

        if (ContainsAny(
                search,
                "hivemind house ona hill",
                "house on a hill",
                "house ona hill"))
        {
            // note: House on a Hill supports manor/hilltop mystery regions.
            return One(
                "hivemind_house_on_hill");
        }

        if (ContainsAny(
                search,
                "hivemind villa forge",
                "villa forge"))
        {
            // note: Villa Forge fills workshop and craft-settlement themes.
            return One(
                "hivemind_villa_forge");
        }

        if (ContainsAny(
                search,
                "hivemind horror hospital",
                "horror hospital"))
        {
            // note: Horror Hospital has a dedicated abandoned-clinic style for modern horror spaces.
            return One(
                "hivemind_horror_hospital");
        }

        if (ContainsAny(
                search,
                "hivemind olympus temple",
                "olympus temple"))
        {
            // note: Olympus Temple supports marble shrine, divine ruin, and mountain sanctuary themes.
            return One(
                "hivemind_olympus_temple");
        }

        if (ContainsAny(
                search,
                "hivemind hallowed depths",
                "hallowed depths"))
        {
            // note: Hallowed Depths is a dungeon kit and should not be blended into village palettes.
            return One(
                "hivemind_hallowed_depths");
        }

        if (ContainsAny(
                search,
                "hivemind the sewers",
                "the sewers",
                "sewer"))
        {
            // note: Sewer modules stay in a wet underground utility style instead of generic industrial.
            return One(
                "hivemind_sewers");
        }

        if (ContainsAny(
                search,
                "hivemind the messenger",
                "the messenger",
                "messenger"))
        {
            // note: The Messenger pack reads as a mountain/ancient traversal kit for generation prompts.
            return One(
                "hivemind_mountain_messenger");
        }

        if (slot == YQWorldAssetCatalog.SlotLootContainer ||
            slot == YQWorldAssetCatalog.SlotEnemySite)
        {
            return One(
                "all");
        }

        if (ContainsAny(
                search,
                "tom s terrain tools",
                "yughues free bushes"))
        {
            return new[]
            {
                "nordic_forest",
                "viking_rural",
                "asian_dynasty"
            };
        }

        if (ContainsAny(
                search,
                "adg textures",
                "ground vol1"))
        {
            return new[]
            {
                "nordic_forest",
                "viking_rural",
                "ancient_desert_ruins",
                "western_desert_town",
                "asian_dynasty",
                "persepolis_empire",
                "container_district"
            };
        }

        return One(
            "all");
    }

    private static void AddSemanticTags(
        GeneratedAssetReferenceRecord record,
        string path)
    {
        if (record == null)
            return;

        string search =
            NormalizeSearchText(
                path);

        AddUnique(
            record.subTags,
            NormalizeKey(
                record.slotTag));

        string[] parts =
            search.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0;
             i < parts.Length &&
             i < 18;
             i++)
        {
            AddUnique(
                record.subTags,
                parts[i]);
        }
    }

    private static int ResolveWeight(
        string slot)
    {
        if (slot == YQWorldAssetCatalog.SlotTerrain ||
            slot == YQWorldAssetCatalog.SlotFloor ||
            slot == YQWorldAssetCatalog.SlotWall ||
            slot == YQWorldAssetCatalog.SlotPath)
        {
            return 5;
        }

        if (slot == YQWorldAssetCatalog.SlotSettlementBuilding ||
            slot == YQWorldAssetCatalog.SlotLargeStructure ||
            slot == YQWorldAssetCatalog.SlotEnemySite)
        {
            return 3;
        }

        return 2;
    }

    private static float ResolveScaleMin(
        string slot)
    {
        switch (slot)
        {
            case YQWorldAssetCatalog.SlotVegetation:
                return 0.85f;
            case YQWorldAssetCatalog.SlotRock:
                return 0.75f;
            case YQWorldAssetCatalog.SlotFloorDeco:
            case YQWorldAssetCatalog.SlotExteriorDeco:
                return 0.8f;
            default:
                return 1f;
        }
    }

    private static float ResolveScaleMax(
        string slot)
    {
        switch (slot)
        {
            case YQWorldAssetCatalog.SlotVegetation:
                return 1.35f;
            case YQWorldAssetCatalog.SlotRock:
                return 1.45f;
            case YQWorldAssetCatalog.SlotFloorDeco:
            case YQWorldAssetCatalog.SlotExteriorDeco:
                return 1.15f;
            default:
                return 1f;
        }
    }

    private static float ResolveFootprint(
        string slot)
    {
        switch (slot)
        {
            case YQWorldAssetCatalog.SlotSettlementBuilding:
                return 8f;
            case YQWorldAssetCatalog.SlotLargeStructure:
            case YQWorldAssetCatalog.SlotEnemySite:
                return 5f;
            case YQWorldAssetCatalog.SlotFloor:
            case YQWorldAssetCatalog.SlotPath:
                return 3f;
            case YQWorldAssetCatalog.SlotWall:
            case YQWorldAssetCatalog.SlotRoof:
                return 2f;
            case YQWorldAssetCatalog.SlotVegetation:
            case YQWorldAssetCatalog.SlotRock:
                return 1.5f;
            default:
                return 0.8f;
        }
    }

    private static string ResolvePlacementRule(
        string slot)
    {
        switch (slot)
        {
            case YQWorldAssetCatalog.SlotTerrain:
                return "terrain_layer_only";
            case YQWorldAssetCatalog.SlotFloor:
            case YQWorldAssetCatalog.SlotPath:
                return "snap_to_ground_grid";
            case YQWorldAssetCatalog.SlotWall:
                return "snap_to_floor_edge";
            case YQWorldAssetCatalog.SlotRoof:
                return "snap_above_matching_wall";
            case YQWorldAssetCatalog.SlotDoor:
                return "replace_one_wall_segment";
            case YQWorldAssetCatalog.SlotWallDeco:
                return "attach_to_valid_wall";
            case YQWorldAssetCatalog.SlotVegetation:
            case YQWorldAssetCatalog.SlotRock:
            case YQWorldAssetCatalog.SlotExteriorDeco:
                return "ground_scatter_outside_walk_path";
            case YQWorldAssetCatalog.SlotLighting:
                return "wall_or_ground_anchor_near_path";
            case YQWorldAssetCatalog.SlotLootContainer:
                return "ground_anchor_clear_interaction";
            default:
                return "ground_anchor_clear_nav";
        }
    }

    private static string ResolveRotationRule(
        string slot)
    {
        switch (slot)
        {
            case YQWorldAssetCatalog.SlotWall:
            case YQWorldAssetCatalog.SlotDoor:
            case YQWorldAssetCatalog.SlotWallDeco:
                return "align_to_wall_normal";
            case YQWorldAssetCatalog.SlotFloor:
            case YQWorldAssetCatalog.SlotPath:
            case YQWorldAssetCatalog.SlotRoof:
            case YQWorldAssetCatalog.SlotSettlementBuilding:
                return "grid_90";
            default:
                return "random_yaw";
        }
    }

    private static bool AllowsRepeat(
        string slot)
    {
        return
            slot == YQWorldAssetCatalog.SlotTerrain ||
            slot == YQWorldAssetCatalog.SlotFloor ||
            slot == YQWorldAssetCatalog.SlotWall ||
            slot == YQWorldAssetCatalog.SlotPath ||
            slot == YQWorldAssetCatalog.SlotVegetation ||
            slot == YQWorldAssetCatalog.SlotRock;
    }

    private static bool BlocksNav(
        string slot)
    {
        return
            slot == YQWorldAssetCatalog.SlotWall ||
            slot == YQWorldAssetCatalog.SlotDoor ||
            slot == YQWorldAssetCatalog.SlotSettlementBuilding ||
            slot == YQWorldAssetCatalog.SlotLargeStructure ||
            slot == YQWorldAssetCatalog.SlotRock ||
            slot == YQWorldAssetCatalog.SlotEnemySite ||
            slot == YQWorldAssetCatalog.SlotLootContainer;
    }

    private static string[] One(
        string value)
    {
        return new[] { value };
    }

    private static bool ContainsAny(
        string text,
        params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            needles == null)
        {
            return false;
        }

        string haystack =
            " " +
            NormalizeSearchText(
                text) +
            " ";

        for (int i = 0;
             i < needles.Length;
             i++)
        {
            string needle =
                NormalizeSearchText(
                    needles[i]);

            if (string.IsNullOrWhiteSpace(
                    needle))
            {
                continue;
            }

            if (haystack.IndexOf(
                    " " +
                    needle +
                    " ",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddUnique(
        List<string> list,
        string value)
    {
        if (list == null ||
            string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string clean =
            value.Trim();

        for (int i = 0;
             i < list.Count;
             i++)
        {
            if (string.Equals(
                    list[i],
                    clean,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        list.Add(
            clean);
    }

    private static string NormalizeKey(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string lower =
            value.Trim().ToLowerInvariant();

        char[] chars =
            lower.ToCharArray();

        for (int i = 0;
             i < chars.Length;
             i++)
        {
            char c =
                chars[i];

            if (!char.IsLetterOrDigit(c))
                chars[i] =
                    '_';
        }

        return new string(
                chars)
            .Trim(
                '_');
    }

    private static string NormalizeSearchText(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars =
            value
                .Trim()
                .ToLowerInvariant()
                .ToCharArray();

        for (int i = 0;
             i < chars.Length;
             i++)
        {
            if (!char.IsLetterOrDigit(
                    chars[i]))
            {
                chars[i] =
                    ' ';
            }
        }

        string normalized =
            new string(
                chars);

        string[] parts =
            normalized.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

        return string.Join(
            " ",
            parts);
    }

    private static List<YQRuntimeWorldMaterialOverride>
        BuildMaterialOverrides(
            GameObject prefab)
    {
        List<YQRuntimeWorldMaterialOverride> result =
            new List<YQRuntimeWorldMaterialOverride>();

        if (prefab == null)
            return result;

        Renderer[] renderers =
            prefab.GetComponentsInChildren<Renderer>(
                true);

        if (renderers == null ||
            renderers.Length == 0)
        {
            return result;
        }

        for (int rendererGlobalIndex = 0;
             rendererGlobalIndex < renderers.Length;
             rendererGlobalIndex++)
        {
            Renderer renderer =
                renderers[rendererGlobalIndex];

            if (renderer == null)
                continue;

            Material[] materials =
                renderer.sharedMaterials;

            if (materials == null ||
                materials.Length == 0)
            {
                continue;
            }

            int rendererIndex =
                GetRendererIndexOnTransform(
                    renderer);

            if (rendererIndex < 0)
                continue;

            string transformPath =
                AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    prefab.transform);

            for (int materialIndex = 0;
                 materialIndex < materials.Length;
                 materialIndex++)
            {
                Material source =
                    materials[materialIndex];

                Material replacement =
                    YQRuntimeUrpMaterialRepair
                        .ResolveEditorMaterialForRuntimeBake(
                            source,
                            renderer);

                if (replacement == null ||
                    replacement == source)
                {
                    continue;
                }

                result.Add(
                    new YQRuntimeWorldMaterialOverride
                    {
                        transformPath =
                            transformPath,

                        rendererIndex =
                            rendererIndex,

                        materialIndex =
                            materialIndex,

                        replacementMaterial =
                            replacement
                    });
            }
        }

        return result;
    }

    private static int GetRendererIndexOnTransform(
        Renderer target)
    {
        if (target == null ||
            target.transform == null)
        {
            return -1;
        }

        Renderer[] renderers =
            target.transform.GetComponents<Renderer>();

        if (renderers == null)
            return -1;

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            if (renderers[i] == target)
                return i;
        }

        return -1;
    }

    private static GeneratedWorldPlanRecord
        BuildSyntheticPalettePlan()
    {
        GeneratedWorldPlanRecord plan =
            new GeneratedWorldPlanRecord
            {
                schemaVersion =
                    "world_plan_registry_build",

                source =
                    "editor_runtime_asset_registry_builder",

                worldSeed =
                    "yourquest_runtime_asset_registry"
            };

        plan.EnsureCollections();

        AddSyntheticRegion(
            plan,
            "registry_nordic",
            "Temperate Forest Frontier",
            "forest woodland moss conifer");

        AddSyntheticRegion(
            plan,
            "registry_viking",
            "Viking Rural Highland",
            "viking rural farm stable highland");

        AddSyntheticRegion(
            plan,
            "registry_ancient_desert",
            "Ancient Desert Ruins",
            "desert sand ruin tomb dry");

        AddSyntheticRegion(
            plan,
            "registry_western",
            "Western Badland Town",
            "western saloon mine rail cactus badland");

        AddSyntheticRegion(
            plan,
            "registry_asian",
            "Asian Dynasty Quarter",
            "asian dynasty pavilion bazaar jade dragon temple");

        AddSyntheticRegion(
            plan,
            "registry_persepolis",
            "Persepolis Imperial Court",
            "persepolis empire column palace mural plinth");

        AddSyntheticRegion(
            plan,
            "registry_victorian",
            "Victorian Manor Estate",
            "victorian mansion manor library book study noble");

        AddSyntheticRegion(
            plan,
            "registry_container",
            "Container Industrial District",
            "container district industrial scrap antenna panel engineer");

        AddSyntheticRegion(
            plan,
            "registry_bio_horror",
            "Bio Horror Complex",
            "bio horror corrupt biomass flesh experiment");

        // note: Every Hivemind family receives a synthetic region so registry coverage and palette validation exercise its real spawn contract.
        AddSyntheticRegion(plan, "registry_gothic_cathedral", "Gothic Cathedral", "gothic cathedral chapel sanctum crypt church");
        AddSyntheticRegion(plan, "registry_hallowed_depths", "Hallowed Depths", "hallowed depths dungeon catacomb undercrypt");
        AddSyntheticRegion(plan, "registry_haunted_village", "Haunted Village", "haunted village abandoned village");
        AddSyntheticRegion(plan, "registry_cave_hidden_tomb", "Cave Of Hidden Tomb", "hidden tomb cave tomb buried tomb");
        AddSyntheticRegion(plan, "registry_cyberpunk_city", "Cyberpunk City", "cyberpunk neon megacity hologram street market");
        AddSyntheticRegion(plan, "registry_gladiator_arena", "Gladitor Arena", "gladiator arena colosseum bloodsport");
        AddSyntheticRegion(plan, "registry_messenger_mountain", "The Messenger Mountain", "messenger mountain cliff path high pass");
        AddSyntheticRegion(plan, "registry_horror_hospital", "Horror Hospital", "horror hospital clinic medical ward operating room");
        AddSyntheticRegion(plan, "registry_house_on_hill", "House Ona Hill", "house on a hill hilltop manor lonely house");
        AddSyntheticRegion(plan, "registry_medieval_kingdom", "Medieval Kingdom", "medieval kingdom castle keep fortress battlement");
        AddSyntheticRegion(plan, "registry_military_camp", "Military Camp", "military camp barracks war camp checkpoint fortified camp");
        AddSyntheticRegion(plan, "registry_modular_viking", "Modular Viking Village", "modular viking village viking rural farm hamlet");
        AddSyntheticRegion(plan, "registry_mountain_temple", "Mountain Temple", "mountain temple temple peak high shrine");
        AddSyntheticRegion(plan, "registry_mystic_dungeon", "Mystic Dungeon", "mystic dungeon ritual dungeon magic dungeon");
        AddSyntheticRegion(plan, "registry_woodland_village", "Native American Village", "woodland village tribal village forest camp woodland settlement");
        AddSyntheticRegion(plan, "registry_olympus_temple", "Olympus Temple", "olympus marble temple greek temple divine temple");
        AddSyntheticRegion(plan, "registry_pirate_island", "Pirate Island", "pirate island docks shipwreck coast coastal");
        AddSyntheticRegion(plan, "registry_rural_town", "Rural Town", "rural town cottage town market town farm town");
        AddSyntheticRegion(plan, "registry_sewers", "The Sewers", "sewers cistern drain tunnel water channel");
        AddSyntheticRegion(plan, "registry_town_smith", "Town Smith", "town smith blacksmith forge smithy workshop");
        AddSyntheticRegion(plan, "registry_villa_forge", "Villa Forge", "villa forge estate forge");
        AddSyntheticRegion(plan, "registry_witch_house", "Witch House", "witch house coven hag occult cottage");

        return plan;
    }

    private static void AddSyntheticRegion(
        GeneratedWorldPlanRecord plan,
        string regionId,
        string displayName,
        string styleText)
    {
        GeneratedRegionRecord region =
            new GeneratedRegionRecord
            {
                regionId =
                    regionId,

                displayName =
                    displayName,

                role =
                    styleText,

                scaleHint =
                    "registry_test",

                terrainProfile =
                    styleText,

                climateProfile =
                    styleText,

                playerPressure =
                    string.Empty,

                lore =
                    styleText,

                gameplayPremise =
                    styleText,

                traversalHook =
                    string.Empty,

                economyHook =
                    string.Empty,

                enemyPressureHook =
                    string.Empty,

                deterministicSeed =
                    regionId +
                    "_seed"
            };

        region.EnsureCollections();

        region.biomeTags.Add(
            styleText);

        plan.regions.Add(
            region);
    }

    private static List<GeneratedAssetReferenceRecord>
        CollectUniqueAssetReferences(
            GeneratedWorldPlanRecord plan)
    {
        List<GeneratedAssetReferenceRecord> result =
            new List<GeneratedAssetReferenceRecord>();

        HashSet<string> seen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (plan == null)
            return result;

        plan.EnsureCollections();

        for (int i = 0;
             i < plan.assetPalettes.Count;
             i++)
        {
            GeneratedRegionAssetPaletteRecord palette =
                plan.assetPalettes[i];

            if (palette == null)
                continue;

            palette.EnsureCollections();

            AddReferences(
                palette.terrainMaterials,
                result,
                seen);

            AddReferences(
                palette.floor,
                result,
                seen);

            AddReferences(
                palette.wall,
                result,
                seen);

            AddReferences(
                palette.roof,
                result,
                seen);

            AddReferences(
                palette.door,
                result,
                seen);

            AddReferences(
                palette.path,
                result,
                seen);

            AddReferences(
                palette.settlementBuilding,
                result,
                seen);

            AddReferences(
                palette.largeStructure,
                result,
                seen);

            AddReferences(
                palette.floorDeco,
                result,
                seen);

            AddReferences(
                palette.wallDeco,
                result,
                seen);

            AddReferences(
                palette.vegetation,
                result,
                seen);

            AddReferences(
                palette.rock,
                result,
                seen);

            AddReferences(
                palette.lighting,
                result,
                seen);

            AddReferences(
                palette.lootContainer,
                result,
                seen);

            AddReferences(
                palette.enemySite,
                result,
                seen);

            AddReferences(
                palette.interiorDeco,
                result,
                seen);

            AddReferences(
                palette.exteriorDeco,
                result,
                seen);
        }

        result.Sort(
            (a, b) =>
                string.Compare(
                    a != null
                        ? a.assetPath
                        : string.Empty,

                    b != null
                        ? b.assetPath
                        : string.Empty,

                    StringComparison.OrdinalIgnoreCase));

        return result;
    }

    private static void AddReferences(
        List<GeneratedAssetReferenceRecord> source,
        List<GeneratedAssetReferenceRecord> destination,
        HashSet<string> seen)
    {
        if (source == null)
            return;

        for (int i = 0;
             i < source.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                source[i];

            if (reference == null)
                continue;

            string normalized =
                YQRuntimeWorldAssetRegistry.NormalizePath(
                    reference.assetPath);

            if (string.IsNullOrWhiteSpace(
                    normalized))
            {
                continue;
            }

            if (!seen.Add(
                    normalized))
            {
                continue;
            }

            destination.Add(
                reference);
        }
    }

    private static void LogPaletteCoverage(
        GeneratedWorldPlanRecord plan)
    {
        HashSet<string> found =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (plan != null)
        {
            plan.EnsureCollections();

            for (int i = 0;
                 i < plan.assetPalettes.Count;
                 i++)
            {
                GeneratedRegionAssetPaletteRecord palette =
                    plan.assetPalettes[i];

                if (palette == null)
                    continue;

                Debug.Log(
                    "[YQRuntimeWorldAssetRegistryBuilder] " +
                    "Palette probe: " +
                    palette.regionId +
                    " -> " +
                    palette.styleKey);

                if (!string.IsNullOrWhiteSpace(
                        palette.styleKey))
                {
                    found.Add(
                        palette.styleKey);
                }
            }
        }

        string[] expected =
        {
            "nordic_forest",
            "viking_rural",
            "ancient_desert_ruins",
            "western_desert_town",
            "asian_dynasty",
            "persepolis_empire",
            "victorian_mansion",
            "container_district",
            "bio_horror_scifi"
        };

        for (int i = 0;
             i < expected.Length;
             i++)
        {
            if (!found.Contains(
                    expected[i]))
            {
                Debug.LogWarning(
                    "[YQRuntimeWorldAssetRegistryBuilder] " +
                    "Palette coverage missing style: " +
                    expected[i]);
            }
        }

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] " +
            "Palette styles discovered: " +
            string.Join(
                ", ",
                found));
    }

    private static void EnsureFolderPath(
        string path)
    {
        string normalized =
            path
                .Replace(
                    '\\',
                    '/')
                .Trim('/');

        if (AssetDatabase.IsValidFolder(
                normalized))
        {
            return;
        }

        string[] parts =
            normalized.Split('/');

        if (parts.Length == 0 ||
            !string.Equals(
                parts[0],
                "Assets",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Unity asset folder must begin with Assets/: " +
                path);
        }

        string current =
            "Assets";

        for (int i = 1;
             i < parts.Length;
             i++)
        {
            string next =
                current +
                "/" +
                parts[i];

            if (!AssetDatabase.IsValidFolder(
                    next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]);
            }

            current =
                next;
        }
    }
}

public static class YQWorldAssetIntakeBuilder
{
    private const string BenchmarkAutoScanAttemptedKey =
        "YourQuest.WG1.BenchmarkAutoScanAttempted";

    private const string UnattendedBenchmarkRequestFileName =
        "YQ_WG1_UNATTENDED_SCAN.request";

    public const string IntakeCatalogPath =
        "Assets/Assets/Resources/YQWorldAssetIntakeCatalog.asset";

    private const string IntakeReportFolder =
        "Assets/Assets/GeneratedAssets/WorldIntake";

    private const string BenchmarkMaterialAdapterFolder =
        IntakeReportFolder +
        "/Materials/MedievalVikingVillage";

    public const string IntakeReportPath =
        IntakeReportFolder +
        "/YQWorldAssetIntakeReport.md";

    private static readonly Dictionary<int, bool>
        UniversalShaderGraphTargetCache =
            new Dictionary<int, bool>();

    [InitializeOnLoadMethod]
    private static void ScheduleMissingBenchmarkScan()
    {
        // note: An unattended WG1 run waits for a safe Edit-mode boundary instead of interrupting Play mode or starting a competing Unity process.
        EditorApplication.playModeStateChanged -=
            HandlePlayModeStateChanged;

        EditorApplication.playModeStateChanged +=
            HandlePlayModeStateChanged;

        EditorApplication.delayCall +=
            TryRunMissingBenchmarkScan;

        EditorApplication.delayCall +=
            TryRunUnattendedBenchmarkRequest;
    }

    private static void HandlePlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (state !=
            PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        EditorApplication.delayCall +=
            TryRunMissingBenchmarkScan;

        EditorApplication.delayCall +=
            TryRunUnattendedBenchmarkRequest;
    }

    private static void TryRunUnattendedBenchmarkRequest()
    {
        string requestPath =
            GetUnattendedBenchmarkRequestPath();

        if (!File.Exists(requestPath))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: The user authorized unattended continuation; ask Unity to leave Play mode normally instead of terminating the editor process.
            Debug.Log(
                "[YQWorldAssetIntakeBuilder] " +
                "WG1 unattended request is exiting Play mode through " +
                "Unity's normal editor lifecycle.");

            EditorApplication.ExitPlaymode();
            return;
        }

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorApplication.delayCall +=
                TryRunUnattendedBenchmarkRequest;

            return;
        }

        // note: A deliberate unattended request may retry a prior session-local scan that never produced a catalog.
        SessionState.SetBool(
            BenchmarkAutoScanAttemptedKey,
            false);

        TryRunMissingBenchmarkScan();

        YQWorldAssetIntakeCatalog generated =
            AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(
                IntakeCatalogPath);

        if (generated != null &&
            File.Exists(requestPath))
        {
            File.Delete(requestPath);

            Debug.Log(
                "[YQWorldAssetIntakeBuilder] " +
                "WG1 unattended benchmark request completed and cleared.");
        }
    }

    private static string GetUnattendedBenchmarkRequestPath()
    {
        return Path.GetFullPath(
            Path.Combine(
                Application.dataPath,
                "../Temp/" +
                UnattendedBenchmarkRequestFileName));
    }

    private static void TryRunMissingBenchmarkScan()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            // note: Import and compilation own AssetDatabase consistency; retry only after their current transaction completes.
            EditorApplication.delayCall +=
                TryRunMissingBenchmarkScan;

            return;
        }

        YQWorldAssetIntakeCatalog existing =
            AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(
                IntakeCatalogPath);

        if (existing != null ||
            SessionState.GetBool(
                BenchmarkAutoScanAttemptedKey,
                false))
        {
            return;
        }

        SessionState.SetBool(
            BenchmarkAutoScanAttemptedKey,
            true);

        Debug.Log(
            "[YQWorldAssetIntakeBuilder] " +
            "Running the missing WG1 benchmark intake snapshot " +
            "at the first safe Edit-mode boundary.");

        ScanFirstBenchmarkKit();
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Archived Tools/Scan First Benchmark Kit")]
    public static void ScanFirstBenchmarkKit()
    {
        RunScan(
            new[]
            {
                YQWorldGenerationArchitecture
                    .FirstBenchmarkSourceRoot
            },
            "first_benchmark_kit");
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Asset Intake/Scan Approved Asset Libraries")]
    public static void ScanAllAssetLibraries()
    {
        // note: Prefab and material roots are merged so every approved library receives one intake manifest even when it supplies only dependencies.
        List<string> roots =
            new List<string>();

        AddUniqueRoots(
            roots,
            YQRuntimeWorldAssetRegistryBuilder
                .GetConfiguredPrefabDiscoveryRoots());

        AddUniqueRoots(
            roots,
            YQRuntimeWorldAssetRegistryBuilder
                .GetConfiguredMaterialDiscoveryRoots());

        RunScan(
            roots.ToArray(),
            "all_configured_asset_libraries");
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Archived Tools/Repair Benchmark Material Compatibility")]
    public static void RepairBenchmarkMaterialCompatibility()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQWorldAssetIntakeBuilder] " +
                "Material compatibility repair requires stable Edit mode.");

            return;
        }

        EnsureFolderPath(
            BenchmarkMaterialAdapterFolder);

        string[] materialGuids =
            AssetDatabase.FindAssets(
                "t:Material",
                new[]
                {
                    YQWorldGenerationArchitecture
                        .FirstBenchmarkSourceRoot
                });

        int existingCounterparts = 0;
        int createdAdapters = 0;
        int unresolved = 0;

        for (int index = 0;
             index < materialGuids.Length;
             index++)
        {
            string sourcePath =
                NormalizePath(
                    AssetDatabase.GUIDToAssetPath(
                        materialGuids[index]));

            Material source =
                AssetDatabase.LoadAssetAtPath<Material>(
                    sourcePath);

            YQMaterialCompatibilityState sourceState =
                EvaluateMaterial(
                    source,
                    out _);

            if (sourceState ==
                    YQMaterialCompatibilityState.VerifiedUrp)
            {
                continue;
            }

            if (TryFindExistingUrpCounterpart(
                    sourcePath,
                    out _))
            {
                existingCounterparts++;
                continue;
            }

            string adapterPath =
                BuildMaterialAdapterPath(
                    sourcePath);

            Material existingAdapter =
                AssetDatabase.LoadAssetAtPath<Material>(
                    adapterPath);

            if (existingAdapter != null)
            {
                continue;
            }

            // note: Persist a project-owned URP copy; never change imported vendor materials or their GUIDs.
            Material adapter =
                YQRuntimeUrpMaterialRepair
                    .CreateEditorUrpLitMaterial(
                        source,
                        null);

            if (adapter == null)
            {
                unresolved++;
                continue;
            }

            adapter.name =
                Path.GetFileNameWithoutExtension(
                    adapterPath);

            AssetDatabase.CreateAsset(
                adapter,
                adapterPath);

            createdAdapters++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // note: Rebuild the intake snapshot immediately so release gates consume counterpart/adapter mappings instead of stale source shader states.
        ScanFirstBenchmarkKit();

        Debug.Log(
            "[YQWorldAssetIntakeBuilder] BENCHMARK MATERIAL COMPATIBILITY READY\n" +
            "Existing URP counterparts: " +
            existingCounterparts +
            "\nCreated project adapters: " +
            createdAdapters +
            "\nUnresolved: " +
            unresolved);
    }

    private static void RunScan(
        string[] requestedRoots,
        string scanScope)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQWorldAssetIntakeBuilder] " +
                "Asset intake is editor-only and cannot run while " +
                "Unity is playing, compiling, or importing.");

            return;
        }

        List<string> roots =
            BuildValidUniqueRoots(
                requestedRoots);

        if (roots.Count == 0)
        {
            Debug.LogWarning(
                "[YQWorldAssetIntakeBuilder] " +
                "No configured asset-library roots are currently available.");

            return;
        }

        // note: A scan re-evaluates Shader Graph source so edits made during the current Unity session cannot leave compatibility results stale.
        UniversalShaderGraphTargetCache.Clear();

        Dictionary<string, GeneratedAssetReferenceRecord> semanticByPath =
            BuildSemanticReferenceLookup();

        List<YQAssetKitManifest> kits =
            new List<YQAssetKitManifest>();

        List<YQSpatialAssetRecord> spatialAssets =
            new List<YQSpatialAssetRecord>();

        List<YQMaterialAssetRecord> materials =
            new List<YQMaterialAssetRecord>();

        HashSet<string> scannedPrefabPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> scannedMaterialPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        try
        {
            for (int rootIndex = 0;
                 rootIndex < roots.Count;
                 rootIndex++)
            {
                string root =
                    roots[rootIndex];

                EditorUtility.DisplayProgressBar(
                    "YourQuest AAA asset intake",
                    "Scanning " + root,
                    rootIndex /
                    (float)Mathf.Max(
                        1,
                        roots.Count));

                YQAssetKitManifest kit =
                    BuildKitManifest(
                        root);

                string[] allGuids =
                    AssetDatabase.FindAssets(
                        string.Empty,
                        new[] { root });

                string[] prefabGuids =
                    AssetDatabase.FindAssets(
                        "t:Prefab",
                        new[] { root });

                string[] materialGuids =
                    AssetDatabase.FindAssets(
                        "t:Material",
                        new[] { root });

                Array.Sort(
                    prefabGuids,
                    StringComparer.Ordinal);

                Array.Sort(
                    materialGuids,
                    StringComparer.Ordinal);

                kit.totalDiscoveredAssetCount =
                    allGuids.Length;

                for (int i = 0;
                     i < prefabGuids.Length;
                     i++)
                {
                    string path =
                        NormalizePath(
                            AssetDatabase.GUIDToAssetPath(
                                prefabGuids[i]));

                    if (string.IsNullOrWhiteSpace(path) ||
                        !scannedPrefabPaths.Add(path))
                    {
                        continue;
                    }

                    semanticByPath.TryGetValue(
                        path,
                        out GeneratedAssetReferenceRecord semantic);

                    YQSpatialAssetRecord record =
                        BuildSpatialAssetRecord(
                            kit,
                            prefabGuids[i],
                            path,
                            semantic);

                    spatialAssets.Add(
                        record);

                    kit.prefabCount++;

                    CountSpatialDisposition(
                        kit,
                        record);
                }

                for (int i = 0;
                     i < materialGuids.Length;
                     i++)
                {
                    string path =
                        NormalizePath(
                            AssetDatabase.GUIDToAssetPath(
                                materialGuids[i]));

                    if (string.IsNullOrWhiteSpace(path) ||
                        !scannedMaterialPaths.Add(path))
                    {
                        continue;
                    }

                    YQMaterialAssetRecord record =
                        BuildMaterialAssetRecord(
                            kit.kitId,
                            materialGuids[i],
                            path);

                    materials.Add(
                        record);

                    kit.materialCount++;

                    if (record.releaseEligible)
                    {
                        kit.verifiedMaterialCount++;
                    }
                    else
                    {
                        kit.materialReviewOrRepairCount++;
                    }
                }

                // note: Kit release remains an authored decision; a clean automated scan is necessary evidence but never sufficient approval.
                kit.releaseEligible =
                    false;

                if (kit.prefabCount == 0)
                {
                    kit.validationIssues.Add(
                        "No prefabs were discovered under this configured root.");
                }

                kits.Add(
                    kit);
            }

            SortRecords(
                kits,
                spatialAssets,
                materials);

            SaveCatalog(
                scanScope,
                kits,
                spatialAssets,
                materials);

            WriteSummaryReport(
                scanScope,
                kits,
                spatialAssets,
                materials);

            Debug.Log(
                "[YQWorldAssetIntakeBuilder] INTAKE COMPLETE\n" +
                "Scope: " + scanScope + "\n" +
                "Kits: " + kits.Count + "\n" +
                "Prefabs recorded: " + spatialAssets.Count + "\n" +
                "Materials recorded: " + materials.Count + "\n" +
                "Catalog: " + IntakeCatalogPath + "\n" +
                "Report: " + IntakeReportPath + "\n" +
                "No prefab was marked compiled-world eligible without authored spatial review.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static YQAssetKitManifest BuildKitManifest(
        string root)
    {
        string displayName =
            root.Substring(
                root.LastIndexOf('/') + 1);

        YQAssetKitManifest kit =
            new YQAssetKitManifest
            {
                kitId = BuildStableKey(
                    root),
                displayName = displayName,
                sourceRoot = root,
                isFirstBenchmarkKit =
                    string.Equals(
                        root,
                        YQWorldGenerationArchitecture
                            .FirstBenchmarkSourceRoot,
                        StringComparison.OrdinalIgnoreCase),
                releaseEligible = false
            };

        kit.EnsureCollections();

        AddInferredKitTags(
            kit,
            root);

        return kit;
    }

    private static YQSpatialAssetRecord BuildSpatialAssetRecord(
        YQAssetKitManifest kit,
        string guid,
        string path,
        GeneratedAssetReferenceRecord semantic)
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                path);

        string semanticRole =
            semantic != null &&
            !string.IsNullOrWhiteSpace(
                semantic.slotTag)
                ? semantic.slotTag
                : InferSemanticRole(
                    path);

        YQSpatialAssetRecord record =
            new YQSpatialAssetRecord
            {
                stableAssetId =
                    "asset_" + guid,
                sourceGuid = guid,
                sourceAssetKey =
                    semantic != null
                        ? semantic.assetKey
                        : string.Empty,
                assetPath = path,
                kitId = kit.kitId,
                semanticRole = semanticRole,
                compositionScale =
                    InferCompositionScale(
                        path,
                        semanticRole,
                        prefab),
                disposition =
                    YQAssetIntakeDisposition
                        .NeedsSpatialReview,
                releaseEligible = false,
                frontDirection = Vector3.forward,
                frontDirectionAuthored = false,
                spatialMetadataAuthored = false,
                allowedSlopeDegrees = 0f,
                foundationProfile = "unassigned",
                roadRelationship = "unassigned",
                navigationProfile = "unassigned"
            };

        record.EnsureCollections();

        CopySemanticTags(
            semantic,
            record);

        if (prefab == null)
        {
            record.disposition =
                YQAssetIntakeDisposition.Quarantined;

            record.validationIssues.Add(
                "Prefab could not be loaded by AssetDatabase.");

            return record;
        }

        if (YQRuntimeWorldAssetRegistryBuilder
                .TryMeasurePrefabLocalBounds(
                    path,
                    out Bounds localBounds))
        {
            record.localBoundsCenter =
                localBounds.center;

            record.localBoundsSize =
                localBounds.size;

            record.clearanceSize =
                localBounds.size +
                new Vector3(
                    0.5f,
                    0.25f,
                    0.5f);

            record.footprintX =
                Mathf.Max(
                    0.01f,
                    Mathf.Abs(
                        localBounds.size.x));

            record.footprintZ =
                Mathf.Max(
                    0.01f,
                    Mathf.Abs(
                        localBounds.size.z));

            record.height =
                Mathf.Max(
                    0.01f,
                    Mathf.Abs(
                        localBounds.size.y));
        }
        else
        {
            record.validationIssues.Add(
                "No reliable mesh bounds were found.");
        }

        Renderer[] renderers =
            prefab.GetComponentsInChildren<Renderer>(
                true);

        Collider[] colliders =
            prefab.GetComponentsInChildren<Collider>(
                true);

        LODGroup[] lodGroups =
            prefab.GetComponentsInChildren<LODGroup>(
                true);

        record.rendererCount =
            renderers.Length;

        record.hasRenderer =
            renderers.Length > 0;

        record.colliderCount =
            colliders.Length;

        record.hasCollider =
            colliders.Length > 0;

        record.lodGroupCount =
            lodGroups.Length;

        record.estimatedRendererCost =
            renderers.Length;

        record.missingScriptCount =
            CountMissingScripts(
                prefab);

        InspectPrefabMaterials(
            renderers,
            record);

        FindSocketCandidates(
            prefab.transform,
            record);

        if (IsEditorOnlyPath(path))
        {
            record.disposition =
                YQAssetIntakeDisposition.EditorOrDemoOnly;

            record.validationIssues.Add(
                "Asset is stored under an Editor-only path.");
        }
        else if (record.missingScriptCount > 0)
        {
            record.disposition =
                YQAssetIntakeDisposition.MissingScriptRepair;

            record.validationIssues.Add(
                "Prefab contains " +
                record.missingScriptCount +
                " missing script component(s).");
        }
        else if (!record.hasRenderer)
        {
            record.disposition =
                YQAssetIntakeDisposition.MissingRenderer;

            record.validationIssues.Add(
                "Prefab contains no renderer and cannot be visually classified.");
        }
        else if (record.invalidMaterialSlotCount > 0)
        {
            record.disposition =
                YQAssetIntakeDisposition.NeedsMaterialRepair;
        }
        else
        {
            // note: Automated inference never promotes a prefab directly into the compiled-world pool; front, footprint, sockets, and role need authored confirmation.
            record.disposition =
                YQAssetIntakeDisposition.NeedsSpatialReview;
        }

        if (!record.hasCollider &&
            RequiresStructuralCollision(
                record.compositionScale))
        {
            record.validationIssues.Add(
                "Structural candidate has no collider profile.");
        }

        if (record.lodGroupCount == 0 &&
            RequiresLodReview(
                record.compositionScale,
                record.rendererCount))
        {
            record.validationIssues.Add(
                "Large or renderer-heavy candidate needs LOD/HLOD review.");
        }

        return record;
    }

    private static YQMaterialAssetRecord BuildMaterialAssetRecord(
        string kitId,
        string guid,
        string path)
    {
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(
                path);

        YQMaterialCompatibilityState state =
            EvaluateRuntimeReadyMaterial(
                material,
                out string issue,
                out string runtimeMaterialPath,
                out string compatibilityStrategy);

        YQMaterialAssetRecord record =
            new YQMaterialAssetRecord
            {
                stableAssetId =
                    "material_" + guid,
                sourceGuid = guid,
                assetPath = path,
                kitId = kitId,
                shaderName =
                    material != null &&
                    material.shader != null
                        ? material.shader.name
                        : string.Empty,
                runtimeMaterialPath = runtimeMaterialPath,
                compatibilityStrategy = compatibilityStrategy,
                compatibilityState = state,
                releaseEligible =
                    state ==
                        YQMaterialCompatibilityState.VerifiedUrp ||
                    state ==
                        YQMaterialCompatibilityState.VerifiedUrpAdapter
            };

        record.EnsureCollections();

        if (!string.IsNullOrWhiteSpace(issue))
        {
            record.validationIssues.Add(
                issue);
        }

        return record;
    }

    private static void InspectPrefabMaterials(
        Renderer[] renderers,
        YQSpatialAssetRecord record)
    {
        if (renderers == null ||
            record == null)
        {
            return;
        }

        for (int rendererIndex = 0;
             rendererIndex < renderers.Length;
             rendererIndex++)
        {
            Renderer renderer =
                renderers[rendererIndex];

            if (renderer == null)
                continue;

            Material[] sharedMaterials =
                renderer.sharedMaterials;

            record.materialSlotCount +=
                sharedMaterials.Length;

            for (int materialIndex = 0;
                 materialIndex < sharedMaterials.Length;
                 materialIndex++)
            {
                YQMaterialCompatibilityState state =
                    EvaluateRuntimeReadyMaterial(
                        sharedMaterials[materialIndex],
                        out string issue,
                        out _,
                        out _);

                if (state ==
                        YQMaterialCompatibilityState
                            .NeedsReview)
                {
                    record.materialReviewSlotCount++;
                }
                else if (state !=
                         YQMaterialCompatibilityState
                             .VerifiedUrp &&
                         state !=
                         YQMaterialCompatibilityState
                             .VerifiedUrpAdapter)
                {
                    record.invalidMaterialSlotCount++;
                }

                if (!string.IsNullOrWhiteSpace(issue))
                {
                    AddUnique(
                        record.validationIssues,
                        "Material: " + issue);
                }
            }
        }
    }

    private static YQMaterialCompatibilityState EvaluateRuntimeReadyMaterial(
        Material material,
        out string issue,
        out string runtimeMaterialPath,
        out string compatibilityStrategy)
    {
        YQMaterialCompatibilityState sourceState =
            EvaluateMaterial(
                material,
                out issue);

        runtimeMaterialPath =
            material != null
                ? NormalizePath(
                    AssetDatabase.GetAssetPath(
                        material))
                : string.Empty;

        compatibilityStrategy =
            "source_material";

        if (sourceState ==
                YQMaterialCompatibilityState.VerifiedUrp)
        {
            return sourceState;
        }

        if (TryFindExistingUrpCounterpart(
                runtimeMaterialPath,
                out string counterpartPath))
        {
            issue = string.Empty;
            runtimeMaterialPath = counterpartPath;
            compatibilityStrategy =
                "existing_vendor_urp_counterpart";

            return YQMaterialCompatibilityState
                .VerifiedUrpAdapter;
        }

        string adapterPath =
            BuildMaterialAdapterPath(
                runtimeMaterialPath);

        Material adapter =
            AssetDatabase.LoadAssetAtPath<Material>(
                adapterPath);

        if (adapter != null &&
            EvaluateMaterial(
                adapter,
                out _) ==
            YQMaterialCompatibilityState.VerifiedUrp)
        {
            issue = string.Empty;
            runtimeMaterialPath = adapterPath;
            compatibilityStrategy =
                "project_owned_urp_adapter";

            return YQMaterialCompatibilityState
                .VerifiedUrpAdapter;
        }

        return sourceState;
    }

    private static bool TryFindExistingUrpCounterpart(
        string sourcePath,
        out string counterpartPath)
    {
        counterpartPath =
            string.Empty;

        string sourceName =
            Path.GetFileNameWithoutExtension(
                sourcePath ?? string.Empty);

        if (string.IsNullOrWhiteSpace(sourceName) ||
            !sourceName.StartsWith(
                "MI_",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string expectedName =
            "M_" +
            sourceName.Substring(3);

        string candidatePath =
            NormalizePath(
                YQWorldGenerationArchitecture
                    .FirstBenchmarkSourceRoot +
                "/Art/Materials/" +
                expectedName +
                ".mat");

        Material candidate =
            AssetDatabase.LoadAssetAtPath<Material>(
                candidatePath);

        if (candidate == null ||
            EvaluateMaterial(
                candidate,
                out _) !=
            YQMaterialCompatibilityState.VerifiedUrp)
        {
            return false;
        }

        // note: The Viking kit ships URP M_* counterparts beside unused HDRP MI_* duplicates; bind the counterpart instead of cloning it.
        counterpartPath = candidatePath;
        return true;
    }

    private static string BuildMaterialAdapterPath(
        string sourcePath)
    {
        string guid =
            AssetDatabase.AssetPathToGUID(
                sourcePath ?? string.Empty);

        string sourceName =
            BuildStableKey(
                Path.GetFileNameWithoutExtension(
                    sourcePath ?? string.Empty));

        if (string.IsNullOrWhiteSpace(sourceName))
            sourceName = "material";

        if (string.IsNullOrWhiteSpace(guid))
            guid = "unresolved";

        return BenchmarkMaterialAdapterFolder +
               "/" +
               sourceName +
               "_" +
               guid +
               "_URP.mat";
    }

    private static YQMaterialCompatibilityState EvaluateMaterial(
        Material material,
        out string issue)
    {
        issue =
            string.Empty;

        if (material == null)
        {
            issue =
                "Missing material reference.";

            return YQMaterialCompatibilityState
                .MissingShader;
        }

        Shader shader =
            material.shader;

        if (shader == null)
        {
            issue =
                "Material has no shader.";

            return YQMaterialCompatibilityState
                .MissingShader;
        }

        string shaderName =
            shader.name ??
            string.Empty;

        if (!shader.isSupported ||
            shaderName.IndexOf(
                "InternalErrorShader",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            issue =
                "Unsupported shader: " +
                shaderName;

            return YQMaterialCompatibilityState
                .UnsupportedShader;
        }

        if (shaderName.IndexOf(
                "HDRP",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            shaderName.IndexOf(
                "High Definition",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            issue =
                "HDRP shader requires an approved URP replacement: " +
                shaderName;

            return YQMaterialCompatibilityState
                .UnsupportedShader;
        }

        if (string.Equals(
                shaderName,
                "Standard",
                StringComparison.OrdinalIgnoreCase) ||
            shaderName.StartsWith(
                "Legacy Shaders/",
                StringComparison.OrdinalIgnoreCase))
        {
            issue =
                "Legacy pipeline shader requires URP review: " +
                shaderName;

            return YQMaterialCompatibilityState
                .LegacyPipeline;
        }

        if (shaderName.IndexOf(
                "Universal Render Pipeline",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return YQMaterialCompatibilityState
                .VerifiedUrp;
        }

        if (shaderName.StartsWith(
                "Skybox/",
                StringComparison.OrdinalIgnoreCase))
        {
            // note: Unity's supported Skybox family remains valid under URP and should not be converted to a surface-lit material.
            return YQMaterialCompatibilityState
                .VerifiedUrp;
        }

        if (HasUniversalShaderGraphTarget(
                shader))
        {
            // note: A supported custom Shader Graph is URP-compatible when its source explicitly declares the Universal target, even if its display name omits "URP".
            return YQMaterialCompatibilityState
                .VerifiedUrp;
        }

        issue =
            "Custom or unrecognized shader needs visual URP review: " +
            shaderName;

        return YQMaterialCompatibilityState
            .NeedsReview;
    }

    private static bool HasUniversalShaderGraphTarget(
        Shader shader)
    {
        if (shader == null)
            return false;

        int instanceId =
            shader.GetInstanceID();

        if (UniversalShaderGraphTargetCache.TryGetValue(
                instanceId,
                out bool cached))
        {
            return cached;
        }

        bool hasUniversalTarget =
            false;

        string shaderPath =
            AssetDatabase.GetAssetPath(
                shader);

        if (!string.IsNullOrWhiteSpace(shaderPath) &&
            shaderPath.EndsWith(
                ".shadergraph",
                StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // note: The graph file is read once per shader and cached; this avoids repeated parsing across hundreds of prefab material slots.
                string source =
                    File.ReadAllText(
                        Path.GetFullPath(shaderPath));

                hasUniversalTarget =
                    source.IndexOf(
                        "UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget",
                        StringComparison.Ordinal) >= 0;
            }
            catch (IOException exception)
            {
                Debug.LogWarning(
                    "[YQWorldAssetIntakeBuilder] " +
                    "Could not inspect Shader Graph target for " +
                    shaderPath +
                    ": " +
                    exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning(
                    "[YQWorldAssetIntakeBuilder] " +
                    "Could not inspect Shader Graph target for " +
                    shaderPath +
                    ": " +
                    exception.Message);
            }
        }

        UniversalShaderGraphTargetCache[instanceId] =
            hasUniversalTarget;

        return hasUniversalTarget;
    }

    private static int CountMissingScripts(
        GameObject prefab)
    {
        if (prefab == null)
            return 0;

        int count =
            0;

        Transform[] transforms =
            prefab.GetComponentsInChildren<Transform>(
                true);

        for (int i = 0;
             i < transforms.Length;
             i++)
        {
            Transform child =
                transforms[i];

            if (child == null)
                continue;

            count +=
                GameObjectUtility
                    .GetMonoBehavioursWithMissingScriptCount(
                        child.gameObject);
        }

        return count;
    }

    private static void FindSocketCandidates(
        Transform root,
        YQSpatialAssetRecord record)
    {
        if (root == null ||
            record == null)
        {
            return;
        }

        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(
                true);

        for (int i = 0;
             i < transforms.Length;
             i++)
        {
            Transform child =
                transforms[i];

            if (child == null ||
                child == root)
            {
                continue;
            }

            string normalizedName =
                BuildStableKey(
                    child.name);

            string relativePath =
                BuildRelativePath(
                    root,
                    child);

            if (ContainsAny(
                    normalizedName,
                    "entrance",
                    "entry",
                    "door",
                    "spawn"))
            {
                AddUniqueLimited(
                    record.entranceSocketCandidates,
                    relativePath,
                    32);
            }

            if (ContainsAny(
                    normalizedName,
                    "socket",
                    "snap",
                    "connector",
                    "attach"))
            {
                AddUniqueLimited(
                    record.connectionSocketCandidates,
                    relativePath,
                    64);
            }

            if (ContainsAny(
                    normalizedName,
                    "prop",
                    "deco",
                    "dressing"))
            {
                AddUniqueLimited(
                    record.dressingSocketCandidates,
                    relativePath,
                    64);
            }
        }
    }

    private static string BuildRelativePath(
        Transform root,
        Transform child)
    {
        if (root == null ||
            child == null ||
            child == root)
        {
            return string.Empty;
        }

        List<string> parts =
            new List<string>();

        Transform current =
            child;

        while (current != null &&
               current != root)
        {
            parts.Add(
                current.name);

            current =
                current.parent;
        }

        parts.Reverse();

        return string.Join(
            "/",
            parts);
    }

    private static YQSpatialCompositionScale InferCompositionScale(
        string path,
        string semanticRole,
        GameObject prefab)
    {
        if (prefab != null &&
            prefab.GetComponentInChildren<SkinnedMeshRenderer>(
                true) != null)
        {
            return YQSpatialCompositionScale
                .CharacterOrCreature;
        }

        string fileName =
            BuildStableKey(
                Path.GetFileNameWithoutExtension(
                    path ?? string.Empty));

        string role =
            BuildStableKey(
                semanticRole);

        string compactRole =
            role.Replace(
                "_",
                string.Empty);

        if (ContainsNamePart(
                fileName,
                "wall",
                "roof",
                "floor",
                "door",
                "window",
                "pillar",
                "beam",
                "stair",
                "fence",
                "support",
                "foundation",
                "section",
                "connector",
                "body",
                "base",
                "head",
                "mid",
                "wing"))
        {
            return YQSpatialCompositionScale.Module;
        }

        if (ContainsNamePart(
                fileName,
                "house",
                "hut",
                "building",
                "inn",
                "tavern",
                "smith",
                "hall",
                "hospital",
                "villa"))
        {
            return YQSpatialCompositionScale
                .CompleteBuilding;
        }

        if (ContainsNamePart(
                fileName,
                "cathedral",
                "temple",
                "castle",
                "arena",
                "tower",
                "monument"))
        {
            return YQSpatialCompositionScale.Landmark;
        }

        if (string.Equals(
                compactRole,
                "settlementbuilding",
                StringComparison.Ordinal) ||
            string.Equals(
                compactRole,
                "largestructure",
                StringComparison.Ordinal) ||
            string.Equals(
                compactRole,
                "enemysite",
                StringComparison.Ordinal))
        {
            return YQSpatialCompositionScale
                .CompleteBuilding;
        }

        return YQSpatialCompositionScale.Prop;
    }

    private static string InferSemanticRole(
        string path)
    {
        string search =
            BuildStableKey(
                Path.GetFileNameWithoutExtension(
                    path ?? string.Empty));

        if (ContainsNamePart(search, "road", "path", "bridge"))
            return YQWorldAssetCatalog.SlotPath;

        if (ContainsNamePart(search, "wall", "fence"))
            return YQWorldAssetCatalog.SlotWall;

        if (ContainsNamePart(search, "roof"))
            return YQWorldAssetCatalog.SlotRoof;

        if (ContainsNamePart(search, "floor", "ground"))
            return YQWorldAssetCatalog.SlotFloor;

        if (ContainsNamePart(search, "door", "gate"))
            return YQWorldAssetCatalog.SlotDoor;

        if (ContainsNamePart(search, "tree", "bush", "grass", "plant"))
            return YQWorldAssetCatalog.SlotVegetation;

        if (ContainsNamePart(search, "rock", "boulder", "cliff"))
            return YQWorldAssetCatalog.SlotRock;

        if (ContainsNamePart(
                search,
                "house",
                "hut",
                "building",
                "inn",
                "smith",
                "hall",
                "cathedral",
                "hospital",
                "temple"))
        {
            return YQWorldAssetCatalog
                .SlotSettlementBuilding;
        }

        return YQWorldAssetCatalog.SlotExteriorDeco;
    }

    private static bool ContainsNamePart(
        string normalizedFileName,
        params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(normalizedFileName) ||
            terms == null)
        {
            return false;
        }

        string[] parts =
            normalizedFileName.Split(
                new[] { '_' },
                StringSplitOptions.RemoveEmptyEntries);

        for (int partIndex = 0;
             partIndex < parts.Length;
             partIndex++)
        {
            for (int termIndex = 0;
                 termIndex < terms.Length;
                 termIndex++)
            {
                string term =
                    terms[termIndex];

                if (!string.IsNullOrWhiteSpace(term) &&
                    parts[partIndex].IndexOf(
                        term,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // note: Matching only filename parts prevents source-folder names such as "MedievalVikingVillage" from falsely triggering the "villa" building rule.
                    return true;
                }
            }
        }

        return false;
    }

    private static void CountSpatialDisposition(
        YQAssetKitManifest kit,
        YQSpatialAssetRecord record)
    {
        if (kit == null ||
            record == null)
        {
            return;
        }

        switch (record.disposition)
        {
            case YQAssetIntakeDisposition.Candidate:
                kit.candidatePrefabCount++;
                break;

            case YQAssetIntakeDisposition.NeedsSpatialReview:
                kit.spatialReviewPrefabCount++;
                break;

            default:
                kit.repairRequiredPrefabCount++;
                break;
        }
    }

    private static void CopySemanticTags(
        GeneratedAssetReferenceRecord semantic,
        YQSpatialAssetRecord record)
    {
        if (semantic == null ||
            record == null)
        {
            return;
        }

        semantic.EnsureCollections();

        for (int i = 0;
             i < semantic.styleTags.Count;
             i++)
        {
            AddUnique(
                record.semanticTags,
                semantic.styleTags[i]);
        }

        for (int i = 0;
             i < semantic.subTags.Count;
             i++)
        {
            AddUnique(
                record.semanticTags,
                semantic.subTags[i]);
        }
    }

    private static Dictionary<string, GeneratedAssetReferenceRecord>
        BuildSemanticReferenceLookup()
    {
        Dictionary<string, GeneratedAssetReferenceRecord> result =
            new Dictionary<string, GeneratedAssetReferenceRecord>(
                StringComparer.OrdinalIgnoreCase);

        YQDiscoveredWorldAssetCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQDiscoveredWorldAssetCatalog>(
                "Assets/Assets/Resources/YQDiscoveredWorldAssetCatalog.asset");

        if (catalog == null ||
            catalog.Entries == null)
        {
            return result;
        }

        for (int i = 0;
             i < catalog.Entries.Count;
             i++)
        {
            GeneratedAssetReferenceRecord record =
                catalog.Entries[i];

            if (record == null)
                continue;

            string path =
                NormalizePath(
                    record.assetPath);

            if (!string.IsNullOrWhiteSpace(path) &&
                !result.ContainsKey(path))
            {
                result.Add(
                    path,
                    record);
            }
        }

        return result;
    }

    private static void SaveCatalog(
        string scanScope,
        List<YQAssetKitManifest> kits,
        List<YQSpatialAssetRecord> spatialAssets,
        List<YQMaterialAssetRecord> materials)
    {
        EnsureFolderPath(
            "Assets/Assets/Resources");

        YQWorldAssetIntakeCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(
                IntakeCatalogPath);

        if (catalog == null)
        {
            catalog =
                ScriptableObject.CreateInstance<YQWorldAssetIntakeCatalog>();

            AssetDatabase.CreateAsset(
                catalog,
                IntakeCatalogPath);
        }

        catalog.SetRecords(
            scanScope,
            DateTime.UtcNow.ToString("O"),
            kits,
            spatialAssets,
            materials);

        EditorUtility.SetDirty(
            catalog);

        AssetDatabase.SaveAssets();
    }

    private static void WriteSummaryReport(
        string scanScope,
        List<YQAssetKitManifest> kits,
        List<YQSpatialAssetRecord> spatialAssets,
        List<YQMaterialAssetRecord> materials)
    {
        EnsureFolderPath(
            IntakeReportFolder);

        System.Text.StringBuilder report =
            new System.Text.StringBuilder();

        report.AppendLine(
            "# YourQuest World Asset Intake Report");

        report.AppendLine();
        report.AppendLine(
            "- Scope: `" + scanScope + "`");
        report.AppendLine(
            "- Generated UTC: `" +
            DateTime.UtcNow.ToString("O") +
            "`");
        report.AppendLine(
            "- Kit manifests: " + kits.Count);
        report.AppendLine(
            "- Prefabs recorded: " + spatialAssets.Count);
        report.AppendLine(
            "- Materials recorded: " + materials.Count);
        report.AppendLine(
            "- Compiled-world eligible prefabs: 0 (authored review required)");
        report.AppendLine();
        report.AppendLine(
            "| Kit | Total assets | Prefabs | Materials | Spatial review | Repair/quarantine | Verified materials | Material review/repair |");
        report.AppendLine(
            "|---|---:|---:|---:|---:|---:|---:|---:|");

        for (int i = 0;
             i < kits.Count;
             i++)
        {
            YQAssetKitManifest kit =
                kits[i];

            report.Append("| ");
            report.Append(EscapeTable(kit.displayName));
            report.Append(" | ");
            report.Append(kit.totalDiscoveredAssetCount);
            report.Append(" | ");
            report.Append(kit.prefabCount);
            report.Append(" | ");
            report.Append(kit.materialCount);
            report.Append(" | ");
            report.Append(kit.spatialReviewPrefabCount);
            report.Append(" | ");
            report.Append(kit.repairRequiredPrefabCount);
            report.Append(" | ");
            report.Append(kit.verifiedMaterialCount);
            report.Append(" | ");
            report.Append(kit.materialReviewOrRepairCount);
            report.AppendLine(" |");
        }

        report.AppendLine();
        report.AppendLine(
            "Every discovered prefab remains attributable even when it is not spawnable. Repair and spatial-review states are deliberate quality gates, not silent exclusions.");

        File.WriteAllText(
            IntakeReportPath,
            report.ToString());

        AssetDatabase.ImportAsset(
            IntakeReportPath,
            ImportAssetOptions.ForceUpdate);
    }

    private static void SortRecords(
        List<YQAssetKitManifest> kits,
        List<YQSpatialAssetRecord> spatialAssets,
        List<YQMaterialAssetRecord> materials)
    {
        kits.Sort(
            (a, b) =>
                string.Compare(
                    a != null ? a.sourceRoot : string.Empty,
                    b != null ? b.sourceRoot : string.Empty,
                    StringComparison.OrdinalIgnoreCase));

        spatialAssets.Sort(
            (a, b) =>
                string.Compare(
                    a != null ? a.assetPath : string.Empty,
                    b != null ? b.assetPath : string.Empty,
                    StringComparison.OrdinalIgnoreCase));

        materials.Sort(
            (a, b) =>
                string.Compare(
                    a != null ? a.assetPath : string.Empty,
                    b != null ? b.assetPath : string.Empty,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> BuildValidUniqueRoots(
        string[] roots)
    {
        List<string> result =
            new List<string>();

        AddUniqueRoots(
            result,
            roots);

        result.Sort(
            StringComparer.OrdinalIgnoreCase);

        return result;
    }

    private static void AddUniqueRoots(
        List<string> destination,
        string[] roots)
    {
        if (destination == null ||
            roots == null)
        {
            return;
        }

        for (int i = 0;
             i < roots.Length;
             i++)
        {
            string root =
                NormalizePath(
                    roots[i]);

            if (string.IsNullOrWhiteSpace(root) ||
                !AssetDatabase.IsValidFolder(root) ||
                destination.Exists(
                    candidate =>
                        string.Equals(
                            candidate,
                            root,
                            StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            destination.Add(
                root);
        }
    }

    private static void AddInferredKitTags(
        YQAssetKitManifest kit,
        string root)
    {
        if (kit == null)
            return;

        string search =
            BuildStableKey(
                root);

        if (ContainsAny(search, "medieval", "viking", "nordic"))
        {
            AddUnique(kit.genreTags, "fantasy");
            AddUnique(kit.genreTags, "historical");
            AddUnique(kit.environmentTags, "settlement");
        }

        if (ContainsAny(search, "horror", "mansion"))
        {
            AddUnique(kit.genreTags, "horror");
        }

        if (ContainsAny(search, "scifi", "containerdistrict"))
        {
            AddUnique(kit.genreTags, "science_fiction");
            AddUnique(kit.environmentTags, "urban");
        }

        if (ContainsAny(search, "terrain", "bush", "texture"))
        {
            AddUnique(kit.environmentTags, "landscape_support");
        }
    }

    private static bool RequiresStructuralCollision(
        YQSpatialCompositionScale scale)
    {
        return scale == YQSpatialCompositionScale.Module ||
               scale == YQSpatialCompositionScale.CompleteBuilding ||
               scale == YQSpatialCompositionScale.ParcelAssembly ||
               scale == YQSpatialCompositionScale.StreetAssembly ||
               scale == YQSpatialCompositionScale.DistrictAssembly ||
               scale == YQSpatialCompositionScale.InteriorAssembly ||
               scale == YQSpatialCompositionScale.Landmark;
    }

    private static bool RequiresLodReview(
        YQSpatialCompositionScale scale,
        int rendererCount)
    {
        return rendererCount >= 8 ||
               scale == YQSpatialCompositionScale.CompleteBuilding ||
               scale == YQSpatialCompositionScale.ParcelAssembly ||
               scale == YQSpatialCompositionScale.DistrictAssembly ||
               scale == YQSpatialCompositionScale.Landmark;
    }

    private static bool IsEditorOnlyPath(
        string path)
    {
        string normalized =
            "/" +
            NormalizePath(path) +
            "/";

        return normalized.IndexOf(
                   "/Editor/",
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsAny(
        string value,
        params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            terms == null)
        {
            return false;
        }

        for (int i = 0;
             i < terms.Length;
             i++)
        {
            if (!string.IsNullOrWhiteSpace(terms[i]) &&
                value.IndexOf(
                    terms[i],
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildStableKey(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        bool previousUnderscore =
            false;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char character =
                char.ToLowerInvariant(
                    value[i]);

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(
                    character);

                previousUnderscore =
                    false;
            }
            else if (!previousUnderscore)
            {
                builder.Append('_');
                previousUnderscore = true;
            }
        }

        return builder
            .ToString()
            .Trim('_');
    }

    private static string NormalizePath(
        string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').Trim();
    }

    private static void AddUnique(
        List<string> destination,
        string value)
    {
        if (destination == null ||
            string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        for (int i = 0;
             i < destination.Count;
             i++)
        {
            if (string.Equals(
                    destination[i],
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        destination.Add(
            value);
    }

    private static void AddUniqueLimited(
        List<string> destination,
        string value,
        int limit)
    {
        if (destination == null ||
            destination.Count >= limit)
        {
            return;
        }

        AddUnique(
            destination,
            value);
    }

    private static string EscapeTable(
        string value)
    {
        return (value ?? string.Empty)
            .Replace("|", "\\|")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }

    private static void EnsureFolderPath(
        string path)
    {
        string normalized =
            NormalizePath(path)
                .Trim('/');

        if (AssetDatabase.IsValidFolder(normalized))
            return;

        string[] parts =
            normalized.Split('/');

        if (parts.Length == 0 ||
            !string.Equals(
                parts[0],
                "Assets",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Unity asset folder must begin with Assets/: " +
                path);
        }

        string current =
            "Assets";

        for (int i = 1;
             i < parts.Length;
             i++)
        {
            string next =
                current +
                "/" +
                parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]);
            }

            current =
                next;
        }
    }
}

public enum YQAssetIntakeWorkbenchFilter
{
    All = 0,
    NeedsSpatialReview = 1,
    Candidate = 2,
    NeedsMaterialRepair = 3,
    MissingRenderer = 4,
    MissingScriptRepair = 5,
    EditorOrDemoOnly = 6,
    Quarantined = 7
}

public sealed class YQWorldAssetIntakeWorkbench : EditorWindow
{
    private const int PageSize =
        80;

    private YQWorldAssetIntakeCatalog _catalog;

    private readonly List<YQSpatialAssetRecord> _filtered =
        new List<YQSpatialAssetRecord>();

    private Vector2 _listScroll;
    private Vector2 _detailScroll;
    private string _search =
        string.Empty;
    private int _kitPopupIndex;
    private int _page;
    private YQAssetIntakeWorkbenchFilter _filter =
        YQAssetIntakeWorkbenchFilter.All;
    private YQSpatialAssetRecord _selected;

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Asset Intake/Open Workbench")]
    public static void Open()
    {
        YQWorldAssetIntakeWorkbench window =
            GetWindow<YQWorldAssetIntakeWorkbench>();

        window.titleContent =
            new GUIContent(
                "YQ Asset Intake");

        window.minSize =
            new Vector2(
                880f,
                560f);

        window.Show();
    }

    private void OnEnable()
    {
        LoadCatalog();
    }

    private void OnGUI()
    {
        DrawHeader();

        if (_catalog == null)
        {
            EditorGUILayout.HelpBox(
                "No intake catalog exists yet. Run the first benchmark " +
                "or all-library scan after Unity enters Edit mode.",
                MessageType.Info);

            if (GUILayout.Button(
                    "Reload Catalog"))
            {
                LoadCatalog();
            }

            return;
        }

        DrawFilters();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        DrawAssetList();
        DrawSelectedRecord();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField(
            "AAA World Asset Intake",
            EditorStyles.boldLabel);

        EditorGUILayout.LabelField(
            "Review project-owned metadata only. Imported source prefabs " +
            "remain untouched.",
            EditorStyles.wordWrappedMiniLabel);

        if (_catalog != null)
        {
            EditorGUILayout.LabelField(
                "Scope: " +
                _catalog.ScanScope +
                "   Kits: " +
                _catalog.Kits.Count +
                "   Prefabs: " +
                _catalog.SpatialAssets.Count +
                "   Materials: " +
                _catalog.Materials.Count,
                EditorStyles.miniLabel);
        }
    }

    private void DrawFilters()
    {
        string[] kitOptions =
            BuildKitOptions();

        int previousKit =
            _kitPopupIndex;

        string previousSearch =
            _search;

        YQAssetIntakeWorkbenchFilter previousFilter =
            _filter;

        EditorGUILayout.BeginHorizontal();

        _kitPopupIndex =
            EditorGUILayout.Popup(
                "Kit",
                Mathf.Clamp(
                    _kitPopupIndex,
                    0,
                    Mathf.Max(
                        0,
                        kitOptions.Length - 1)),
                kitOptions);

        _filter =
            (YQAssetIntakeWorkbenchFilter)
            EditorGUILayout.EnumPopup(
                "Disposition",
                _filter);

        EditorGUILayout.EndHorizontal();

        _search =
            EditorGUILayout.TextField(
                "Search",
                _search ?? string.Empty);

        if (previousKit != _kitPopupIndex ||
            previousFilter != _filter ||
            !string.Equals(
                previousSearch,
                _search,
                StringComparison.Ordinal))
        {
            _page = 0;
            RebuildFiltered();
        }
    }

    private void DrawAssetList()
    {
        EditorGUILayout.BeginVertical(
            GUILayout.Width(
                370f));

        int pageCount =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    _filtered.Count /
                    (float)PageSize));

        _page =
            Mathf.Clamp(
                _page,
                0,
                pageCount - 1);

        EditorGUILayout.BeginHorizontal();

        GUI.enabled =
            _page > 0;

        if (GUILayout.Button(
                "Previous"))
        {
            _page--;
        }

        GUI.enabled =
            true;

        EditorGUILayout.LabelField(
            "Page " +
            (_page + 1) +
            "/" +
            pageCount +
            " (" +
            _filtered.Count +
            ")",
            EditorStyles.centeredGreyMiniLabel,
            GUILayout.Width(
                130f));

        GUI.enabled =
            _page <
            pageCount - 1;

        if (GUILayout.Button(
                "Next"))
        {
            _page++;
        }

        GUI.enabled =
            true;

        EditorGUILayout.EndHorizontal();

        _listScroll =
            EditorGUILayout.BeginScrollView(
                _listScroll);

        int start =
            _page *
            PageSize;

        int end =
            Mathf.Min(
                _filtered.Count,
                start +
                PageSize);

        for (int i = start;
             i < end;
             i++)
        {
            YQSpatialAssetRecord record =
                _filtered[i];

            if (record == null)
                continue;

            string label =
                record.disposition +
                " | " +
                System.IO.Path
                    .GetFileNameWithoutExtension(
                        record.assetPath);

            GUIStyle style =
                record == _selected
                    ? EditorStyles.miniButtonMid
                    : EditorStyles.miniButton;

            if (GUILayout.Button(
                    label,
                    style))
            {
                _selected =
                    record;

                GUI.FocusControl(
                    null);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSelectedRecord()
    {
        EditorGUILayout.BeginVertical();

        if (_selected == null)
        {
            EditorGUILayout.HelpBox(
                "Select a prefab record to inspect and author its spatial metadata.",
                MessageType.Info);

            EditorGUILayout.EndVertical();
            return;
        }

        _detailScroll =
            EditorGUILayout.BeginScrollView(
                _detailScroll);

        EditorGUILayout.LabelField(
            System.IO.Path.GetFileNameWithoutExtension(
                _selected.assetPath),
            EditorStyles.boldLabel);

        EditorGUILayout.SelectableLabel(
            _selected.assetPath,
            EditorStyles.textField,
            GUILayout.Height(
                EditorGUIUtility.singleLineHeight));

        EditorGUILayout.LabelField(
            "Stable ID",
            _selected.stableAssetId);

        EditorGUILayout.LabelField(
            "Disposition",
            _selected.disposition.ToString());

        EditorGUILayout.LabelField(
            "Bounds",
            _selected.localBoundsSize.ToString("F2"));

        EditorGUILayout.LabelField(
            "Renderers / materials / colliders / LODs",
            _selected.rendererCount +
            " / " +
            _selected.materialSlotCount +
            " / " +
            _selected.colliderCount +
            " / " +
            _selected.lodGroupCount);

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();

        string semanticRole =
            EditorGUILayout.TextField(
                "Semantic role",
                _selected.semanticRole ?? string.Empty);

        YQSpatialCompositionScale compositionScale =
            (YQSpatialCompositionScale)
            EditorGUILayout.EnumPopup(
                "Composition scale",
                _selected.compositionScale);

        bool spatialMetadataAuthored =
            EditorGUILayout.Toggle(
                "Spatial metadata authored",
                _selected.spatialMetadataAuthored);

        Vector3 frontDirection =
            EditorGUILayout.Vector3Field(
                "Front direction",
                _selected.frontDirection);

        bool frontDirectionAuthored =
            EditorGUILayout.Toggle(
                "Front confirmed",
                _selected.frontDirectionAuthored);

        float allowedSlopeDegrees =
            EditorGUILayout.Slider(
                "Allowed slope",
                _selected.allowedSlopeDegrees,
                0f,
                60f);

        string foundationProfile =
            EditorGUILayout.TextField(
                "Foundation profile",
                _selected.foundationProfile ?? string.Empty);

        string roadRelationship =
            EditorGUILayout.TextField(
                "Road relationship",
                _selected.roadRelationship ?? string.Empty);

        string navigationProfile =
            EditorGUILayout.TextField(
                "Navigation profile",
                _selected.navigationProfile ?? string.Empty);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(
                _catalog,
                "Edit YourQuest spatial asset metadata");

            _selected.semanticRole =
                semanticRole;

            _selected.compositionScale =
                compositionScale;

            _selected.spatialMetadataAuthored =
                spatialMetadataAuthored;

            _selected.frontDirection =
                frontDirection.sqrMagnitude > 0.0001f
                    ? frontDirection.normalized
                    : Vector3.forward;

            _selected.frontDirectionAuthored =
                frontDirectionAuthored;

            _selected.allowedSlopeDegrees =
                allowedSlopeDegrees;

            _selected.foundationProfile =
                foundationProfile;

            _selected.roadRelationship =
                roadRelationship;

            _selected.navigationProfile =
                navigationProfile;

            if (_selected.disposition ==
                YQAssetIntakeDisposition.Candidate)
            {
                // note: Editing approved metadata returns the record to review so stale release approval cannot survive a semantic change.
                _selected.disposition =
                    YQAssetIntakeDisposition
                        .NeedsSpatialReview;

                _selected.releaseEligible =
                    false;
            }

            EditorUtility.SetDirty(
                _catalog);
        }

        DrawStringList(
            "Entrance candidates",
            _selected.entranceSocketCandidates);

        DrawStringList(
            "Connection candidates",
            _selected.connectionSocketCandidates);

        DrawStringList(
            "Validation issues",
            _selected.validationIssues);

        EditorGUILayout.Space();

        if (TryValidateCandidate(
                _selected,
                out string blockingReason))
        {
            if (GUILayout.Button(
                    "Approve as Compiled-World Candidate"))
            {
                Undo.RecordObject(
                    _catalog,
                    "Approve YourQuest spatial asset");

                _selected.disposition =
                    YQAssetIntakeDisposition.Candidate;

                _selected.releaseEligible =
                    true;

                SaveCatalogChanges();
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                blockingReason,
                MessageType.Warning);
        }

        if (_selected.disposition ==
            YQAssetIntakeDisposition.Candidate &&
            GUILayout.Button(
                "Return Candidate to Spatial Review"))
        {
            Undo.RecordObject(
                _catalog,
                "Return YourQuest spatial asset to review");

            _selected.disposition =
                YQAssetIntakeDisposition
                    .NeedsSpatialReview;

            _selected.releaseEligible =
                false;

            SaveCatalogChanges();
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Select Source Prefab"))
        {
            UnityEngine.Object source =
                AssetDatabase.LoadMainAssetAtPath(
                    _selected.assetPath);

            Selection.activeObject =
                source;

            EditorGUIUtility.PingObject(
                source);
        }

        if (GUILayout.Button(
                "Save Metadata"))
        {
            SaveCatalogChanges();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static bool TryValidateCandidate(
        YQSpatialAssetRecord record,
        out string reason)
    {
        reason =
            string.Empty;

        if (record == null)
        {
            reason =
                "No record is selected.";

            return false;
        }

        if (!record.hasRenderer)
        {
            reason =
                "A visual candidate requires at least one renderer.";

            return false;
        }

        if (record.missingScriptCount > 0)
        {
            reason =
                "Missing scripts must be repaired before approval.";

            return false;
        }

        if (record.invalidMaterialSlotCount > 0 ||
            record.materialReviewSlotCount > 0)
        {
            reason =
                "Every material slot must be repaired or explicitly " +
                "verified for URP before approval.";

            return false;
        }

        if (record.localBoundsSize.sqrMagnitude <=
            0.0001f)
        {
            reason =
                "Reliable local bounds are required.";

            return false;
        }

        if (!record.spatialMetadataAuthored)
        {
            reason =
                "Confirm the authored spatial metadata before approval.";

            return false;
        }

        bool structural =
            record.compositionScale ==
                YQSpatialCompositionScale.Module ||
            record.compositionScale ==
                YQSpatialCompositionScale.CompleteBuilding ||
            record.compositionScale ==
                YQSpatialCompositionScale.ParcelAssembly ||
            record.compositionScale ==
                YQSpatialCompositionScale.StreetAssembly ||
            record.compositionScale ==
                YQSpatialCompositionScale.DistrictAssembly ||
            record.compositionScale ==
                YQSpatialCompositionScale.InteriorAssembly ||
            record.compositionScale ==
                YQSpatialCompositionScale.Landmark;

        if (structural &&
            !record.hasCollider)
        {
            reason =
                "Structural candidates require an approved collider profile.";

            return false;
        }

        if (structural &&
            string.Equals(
                record.foundationProfile,
                "unassigned",
                StringComparison.OrdinalIgnoreCase))
        {
            reason =
                "Structural candidates require a foundation profile.";

            return false;
        }

        bool needsFront =
            record.compositionScale ==
                YQSpatialCompositionScale.CompleteBuilding ||
            record.compositionScale ==
                YQSpatialCompositionScale.ParcelAssembly ||
            record.compositionScale ==
                YQSpatialCompositionScale.Landmark;

        if (needsFront &&
            !record.frontDirectionAuthored)
        {
            reason =
                "Buildings, parcels, and landmarks require a confirmed front direction.";

            return false;
        }

        if (needsFront &&
            string.Equals(
                record.roadRelationship,
                "unassigned",
                StringComparison.OrdinalIgnoreCase))
        {
            reason =
                "Buildings, parcels, and landmarks require a road/frontage relationship.";

            return false;
        }

        return true;
    }

    private void SaveCatalogChanges()
    {
        if (_catalog == null)
            return;

        // note: Derived kit counts are recalculated inside the same serialized transaction as candidate approval.
        _catalog.RecalculateKitSpatialCounts();
        EditorUtility.SetDirty(_catalog);
        AssetDatabase.SaveAssets();
        RebuildFiltered();
    }

    private void LoadCatalog()
    {
        _catalog =
            AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(
                YQWorldAssetIntakeBuilder
                    .IntakeCatalogPath);

        _selected =
            null;

        _kitPopupIndex =
            0;

        _page =
            0;

        RebuildFiltered();
    }

    private void RebuildFiltered()
    {
        _filtered.Clear();

        if (_catalog == null ||
            _catalog.SpatialAssets == null)
        {
            Repaint();
            return;
        }

        string selectedKitId =
            GetSelectedKitId();

        for (int i = 0;
             i < _catalog.SpatialAssets.Count;
             i++)
        {
            YQSpatialAssetRecord record =
                _catalog.SpatialAssets[i];

            if (record == null)
                continue;

            if (!string.IsNullOrWhiteSpace(selectedKitId) &&
                !string.Equals(
                    selectedKitId,
                    record.kitId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!MatchesFilter(
                    record.disposition,
                    _filter))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(_search) &&
                (record.assetPath == null ||
                 record.assetPath.IndexOf(
                     _search,
                     StringComparison.OrdinalIgnoreCase) < 0) &&
                (record.semanticRole == null ||
                 record.semanticRole.IndexOf(
                     _search,
                     StringComparison.OrdinalIgnoreCase) < 0))
            {
                continue;
            }

            _filtered.Add(
                record);
        }

        int pageCount =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    _filtered.Count /
                    (float)PageSize));

        _page =
            Mathf.Clamp(
                _page,
                0,
                pageCount - 1);

        Repaint();
    }

    private string[] BuildKitOptions()
    {
        if (_catalog == null ||
            _catalog.Kits == null)
        {
            return new[]
            {
                "All kits"
            };
        }

        string[] options =
            new string[
                _catalog.Kits.Count +
                1];

        options[0] =
            "All kits";

        for (int i = 0;
             i < _catalog.Kits.Count;
             i++)
        {
            YQAssetKitManifest kit =
                _catalog.Kits[i];

            options[i + 1] =
                kit != null
                    ? kit.displayName
                    : "<missing kit>";
        }

        return options;
    }

    private string GetSelectedKitId()
    {
        if (_catalog == null ||
            _kitPopupIndex <= 0 ||
            _kitPopupIndex >
            _catalog.Kits.Count)
        {
            return string.Empty;
        }

        YQAssetKitManifest kit =
            _catalog.Kits[
                _kitPopupIndex -
                1];

        return kit != null
            ? kit.kitId
            : string.Empty;
    }

    private static bool MatchesFilter(
        YQAssetIntakeDisposition disposition,
        YQAssetIntakeWorkbenchFilter filter)
    {
        if (filter ==
            YQAssetIntakeWorkbenchFilter.All)
        {
            return true;
        }

        return string.Equals(
            disposition.ToString(),
            filter.ToString(),
            StringComparison.Ordinal);
    }

    private static void DrawStringList(
        string label,
        IReadOnlyList<string> values)
    {
        EditorGUILayout.LabelField(
            label,
            EditorStyles.boldLabel);

        if (values == null ||
            values.Count == 0)
        {
            EditorGUILayout.LabelField(
                "None",
                EditorStyles.miniLabel);

            return;
        }

        int count =
            Mathf.Min(
                values.Count,
                24);

        for (int i = 0;
             i < count;
             i++)
        {
            EditorGUILayout.LabelField(
                "• " + values[i],
                EditorStyles.wordWrappedMiniLabel);
        }

        if (values.Count > count)
        {
            EditorGUILayout.LabelField(
                "+ " +
                (values.Count - count) +
                " more",
                EditorStyles.miniLabel);
        }
    }
}
