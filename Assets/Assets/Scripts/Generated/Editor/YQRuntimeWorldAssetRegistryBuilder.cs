using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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

    internal const string DiscoveredRuntimeAuditReportPath =
        "Assets/Assets/GeneratedAssets/WorldIntake/YQDiscoveredAssetRuntimeAudit.md";

    private const string RuntimeShardFolder =
        RegistryFolder +
        "/YQWorldAssetShards";

    internal const string HivemindUrpMaterialsFolder =
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
        // note: Approved URP conifers must enter runtime shards; editor-only AssetDatabase fallback previously hid their absence until a player build.
        "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs",
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
        "Tools/YourQuest/AAA World Generation/Asset Intake/Validate Discovered Runtime References")]
    public static void ValidateDiscoveredRuntimeReferences()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQRuntimeWorldAssetRegistryBuilder] Runtime reference validation requires stable Edit mode.");
            return;
        }

        YQDiscoveredWorldAssetCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQDiscoveredWorldAssetCatalog>(
                DiscoveredCatalogPath);

        System.Text.StringBuilder report =
            new System.Text.StringBuilder();
        report.AppendLine("# YourQuest Discovered Asset Runtime Audit");
        report.AppendLine();
        report.AppendLine("Generated UTC: `" + DateTime.UtcNow.ToString("O") + "`");

        if (catalog == null || catalog.Entries == null)
        {
            report.AppendLine("\n**Result:** FAILED — discovered asset catalog is missing.");
            WriteDiscoveredRuntimeAuditReport(report);
            Debug.LogError(
                "[YQRuntimeWorldAssetRegistryBuilder] Discovered asset catalog is missing.");
            return;
        }

        HashSet<string> seenPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int uniquePaths = 0;
        int prefabCount = 0;
        int materialCount = 0;
        int missingCount = 0;
        int missingScriptCount = 0;
        int noRendererCount = 0;
        int noColliderCount = 0;
        int invalidMaterialSlotCount = 0;
        int unsupportedShaderCount = 0;
        int runtimeMaterialReviewCount = 0;
        int runtimeMaterialFailureCount = 0;
        int quarantinedReferenceCount = 0;
        int invalidRendererBoundsCount = 0;
        int invalidColliderBoundsCount = 0;
        int emptyLodConfigurationCount = 0;
        int lodGroupCount = 0;

        for (int index = 0; index < catalog.Entries.Count; index++)
        {
            GeneratedAssetReferenceRecord reference =
                catalog.Entries[index];
            if (reference == null)
                continue;

            string path =
                YQRuntimeWorldAssetRegistry.NormalizePath(
                    reference.assetPath);
            if (string.IsNullOrWhiteSpace(path) || !seenPaths.Add(path))
                continue;

            uniquePaths++;
            if (!reference.runtimeEligible)
                quarantinedReferenceCount++;

            bool materialReference =
                string.Equals(reference.assetType, "material", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase);

            if (materialReference)
            {
                materialCount++;
                Material material =
                    AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null)
                {
                    missingCount++;
                }
                else
                {
                    // note: Record the imported shader risk separately from the final URP binding decision.
                    if (!material.shader.isSupported)
                        unsupportedShaderCount++;

                    YQMaterialCompatibilityState runtimeState =
                        YQWorldAssetIntakeBuilder
                            .EvaluateRuntimeReadyMaterialForAudit(
                            material);
                    RecordRuntimeMaterialAuditState(
                        runtimeState,
                        ref runtimeMaterialReviewCount,
                        ref runtimeMaterialFailureCount);
                }
                continue;
            }

            prefabCount++;
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                missingCount++;
                continue;
            }

            missingScriptCount +=
                HasMissingScripts(prefab) ? 1 : 0;

            Renderer[] renderers =
                prefab.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders =
                prefab.GetComponentsInChildren<Collider>(true);
            LODGroup[] lodGroups =
                prefab.GetComponentsInChildren<LODGroup>(true);

            if (renderers.Length == 0)
                noRendererCount++;
            if (colliders.Length == 0)
                noColliderCount++;
            lodGroupCount += lodGroups.Length;

            for (int colliderIndex = 0;
                 colliderIndex < colliders.Length;
                 colliderIndex++)
            {
                if (colliders[colliderIndex] == null ||
                    !HasFiniteBounds(colliders[colliderIndex].bounds))
                {
                    invalidColliderBoundsCount++;
                }
            }

            for (int lodIndex = 0;
                 lodIndex < lodGroups.Length;
                 lodIndex++)
            {
                if (lodGroups[lodIndex] == null ||
                    lodGroups[lodIndex].GetLODs().Length == 0)
                {
                    emptyLodConfigurationCount++;
                }
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || renderer.sharedMaterials == null)
                    continue;

                if (!HasFiniteBounds(renderer.bounds))
                    invalidRendererBoundsCount++;

                for (int materialIndex = 0; materialIndex < renderer.sharedMaterials.Length; materialIndex++)
                {
                    Material material = renderer.sharedMaterials[materialIndex];
                    if (material == null || material.shader == null)
                    {
                        invalidMaterialSlotCount++;
                    }
                    else
                    {
                        // note: Validate both the source shader and the resolved URP-compatible runtime material.
                        if (!material.shader.isSupported)
                            unsupportedShaderCount++;

                        YQMaterialCompatibilityState runtimeState =
                            YQWorldAssetIntakeBuilder
                                .EvaluateRuntimeReadyMaterialForAudit(
                                material);
                        RecordRuntimeMaterialAuditState(
                            runtimeState,
                            ref runtimeMaterialReviewCount,
                            ref runtimeMaterialFailureCount);
                    }
                }
            }
        }

        report.AppendLine();
        report.AppendLine("| Check | Count |");
        report.AppendLine("|---|---:|");
        report.AppendLine("| Unique catalog paths | " + uniquePaths + " |");
        report.AppendLine("| Prefabs | " + prefabCount + " |");
        report.AppendLine("| Materials | " + materialCount + " |");
        report.AppendLine("| Missing AssetDatabase references | " + missingCount + " |");
        report.AppendLine("| Prefabs with missing scripts | " + missingScriptCount + " |");
        report.AppendLine("| Prefabs without renderers | " + noRendererCount + " |");
        report.AppendLine("| Prefabs without colliders | " + noColliderCount + " |");
        report.AppendLine("| Invalid prefab material slots | " + invalidMaterialSlotCount + " |");
        report.AppendLine("| Source materials with unsupported shaders | " + unsupportedShaderCount + " |");
        report.AppendLine("| Runtime material bindings requiring visual review | " + runtimeMaterialReviewCount + " |");
        report.AppendLine("| Runtime material bindings without a usable URP path | " + runtimeMaterialFailureCount + " |");
        report.AppendLine("| Catalogued references quarantined from procedural selection | " + quarantinedReferenceCount + " |");
        report.AppendLine("| Renderers with invalid bounds | " + invalidRendererBoundsCount + " |");
        report.AppendLine("| Colliders with invalid bounds | " + invalidColliderBoundsCount + " |");
        report.AppendLine("| LOD groups with empty configurations | " + emptyLodConfigurationCount + " |");
        report.AppendLine("| LOD groups discovered | " + lodGroupCount + " |");
        report.AppendLine();
        report.AppendLine(
            "Missing colliders are reported for curation review because vegetation and dressing may intentionally be non-blocking; structural eligibility remains governed by the V2 intake contract.");

        WriteDiscoveredRuntimeAuditReport(report);

        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] Discovered runtime reference audit complete. " +
            "Unique=" + uniquePaths +
            ", missing=" + missingCount +
            ", missing scripts=" + missingScriptCount +
            ", invalid material slots=" + invalidMaterialSlotCount +
            ", source unsupported shaders=" + unsupportedShaderCount +
            ", runtime material review=" + runtimeMaterialReviewCount +
            ", runtime material failures=" + runtimeMaterialFailureCount + ".");
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Asset Intake/Rebuild Discovered Runtime Eligibility")]
    public static void RebuildDiscoveredRuntimeEligibility()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQRuntimeWorldAssetRegistryBuilder] Runtime eligibility migration requires stable Edit mode.");
            return;
        }

        YQDiscoveredWorldAssetCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQDiscoveredWorldAssetCatalog>(
                DiscoveredCatalogPath);
        if (catalog == null || catalog.Entries == null)
        {
            Debug.LogError(
                "[YQRuntimeWorldAssetRegistryBuilder] Discovered asset catalog is missing for eligibility migration.");
            return;
        }

        int eligibleCount = 0;
        int quarantinedCount = 0;
        for (int index = 0; index < catalog.Entries.Count; index++)
        {
            GeneratedAssetReferenceRecord reference =
                catalog.Entries[index];
            if (reference == null)
                continue;

            reference.EnsureCollections();
            reference.runtimeEligible =
                IsDiscoveredReferenceRuntimeEligible(
                    YQRuntimeWorldAssetRegistry.NormalizePath(
                        reference.assetPath),
                    reference.assetType,
                    reference.slotTag);

            if (reference.runtimeEligible)
                eligibleCount++;
            else
                quarantinedCount++;
        }

        // note: Persist the eligibility migration before rebuilding shards so catalog and runtime registry share one safety decision.
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        YQRuntimeWorldAssetRegistryBuilder.RebuildRegistryWithDiscoveredAssets();
        ValidateDiscoveredRuntimeReferences();
        Debug.Log(
            "[YQRuntimeWorldAssetRegistryBuilder] Runtime eligibility migration complete. " +
            "Eligible=" + eligibleCount +
            ", quarantined=" + quarantinedCount + ".");
    }

    private static void RecordRuntimeMaterialAuditState(
        YQMaterialCompatibilityState state,
        ref int reviewCount,
        ref int failureCount)
    {
        // note: Adapter-backed and native URP materials are safe; separate visual review from unusable bindings.
        if (state == YQMaterialCompatibilityState.NeedsReview)
        {
            reviewCount++;
        }
        else if (state != YQMaterialCompatibilityState.VerifiedUrp &&
                 state != YQMaterialCompatibilityState.VerifiedUrpAdapter)
        {
            failureCount++;
        }
    }

    private static bool HasFiniteBounds(Bounds bounds)
    {
        // note: Reject NaN or infinite geometry bounds before procedural placement can reserve invalid space.
        return IsFinite(bounds.center.x) &&
               IsFinite(bounds.center.y) &&
               IsFinite(bounds.center.z) &&
               IsFinite(bounds.size.x) &&
               IsFinite(bounds.size.y) &&
               IsFinite(bounds.size.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }

    private static void WriteDiscoveredRuntimeAuditReport(
        System.Text.StringBuilder report)
    {
        EnsureFolderPath("Assets/Assets/GeneratedAssets/WorldIntake");
        File.WriteAllText(
            DiscoveredRuntimeAuditReportPath,
            report != null ? report.ToString() : string.Empty);
        AssetDatabase.ImportAsset(
            DiscoveredRuntimeAuditReportPath,
            ImportAssetOptions.ForceUpdate);
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

            // note: Quarantined discovered references stay in the catalog for audit, but never re-enter the runtime registry.
            if (!reference.runtimeEligible)
                continue;

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

        // note: Generated bridge materials are system dependencies even when their prefabs are not scatter candidates; retain their bindings without adding them to unrelated palettes.
        foreach (string bridgePath in YQGeneratedRiverBridge.RequiredAssetPaths)
        {
            if (!references.Exists(reference => reference != null && string.Equals(reference.assetPath, bridgePath, StringComparison.OrdinalIgnoreCase)))
                references.Add(new GeneratedAssetReferenceRecord { assetPath = bridgePath, assetType = "prefab" });
        }

        if (includeDiscoveredAssets)
        {
            List<GeneratedAssetReferenceRecord> eligibleDiscoveredReferences =
                FilterRuntimeEligibleDiscoveredReferences(
                    discoveredReferences);

            MergeUniqueAssetReferences(
                eligibleDiscoveredReferences,
                references);
        }

        HashSet<string> discoveredPaths =
            BuildAssetPathSet(
                FilterRuntimeEligibleDiscoveredReferences(
                    discoveredReferences));

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

                // note: Bind a verified project adapter for an imported HDRP material; eligibility and the runtime reference must resolve to the same surface.
                if (discoveredPaths.Contains(path) &&
                    YQWorldAssetIntakeBuilder.TryResolveVerifiedRuntimeMaterial(
                        entry.material,
                        out Material runtimeMaterial))
                {
                    entry.material = runtimeMaterial;
                }

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

                    // note: Discovered Hivemind prefabs bind only verified pre-existing adapters; no material creation or broad fallback search occurs here.
                    entry.materialOverrides =
                        isDiscoveredPath
                            ? BuildVerifiedDiscoveredMaterialOverrides(
                                entry.prefab,
                                path)
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
                runtimeEligible = IsDiscoveredReferenceRuntimeEligible(
                    normalizedPath,
                    assetType,
                    slot),
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

        // note: Project reviewed spatial curation into the legacy runtime reference so palette assembly can consume authored context without flattening it to filename tokens.
        // note: The intake builder owns the reviewed spatial index; project that curation through its shared helper.
        YQWorldAssetIntakeBuilder.ApplyReviewedSpatialContract(record);

        result.Add(
            record);
    }

    private static bool IsDiscoveredReferenceRuntimeEligible(
        string path,
        string assetType,
        string slot)
    {
        bool diagnosticConifer =
            path.IndexOf(
                "Assets/Forst/Conifers [BOTD]/",
                StringComparison.OrdinalIgnoreCase) >= 0;
        // note: Complete human character prefabs are repaired to URP at runtime; keep them discoverable even when imported source slots still carry HDRP metadata.
        bool humanCharacter =
            path.IndexOf(
                "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters/",
                StringComparison.OrdinalIgnoreCase) >= 0;

        if (string.Equals(
                assetType,
                "material",
                StringComparison.OrdinalIgnoreCase))
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    path);

            YQMaterialCompatibilityState state =
                YQWorldAssetIntakeBuilder
                    .EvaluateRuntimeReadyMaterialForAudit(
                        material);

            return state == YQMaterialCompatibilityState.VerifiedUrp ||
                   state == YQMaterialCompatibilityState.VerifiedUrpAdapter;
        }

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                path);
        if (prefab == null)
        {
            if (diagnosticConifer)
                Debug.LogWarning("[YQRuntimeWorldAssetRegistryBuilder] Conifer eligibility rejected: prefab load failed: " + path);
            return false;
        }

        if (HasMissingScripts(prefab))
        {
            if (diagnosticConifer)
                Debug.LogWarning("[YQRuntimeWorldAssetRegistryBuilder] Conifer eligibility rejected: missing script: " + path);
            return false;
        }

        if (!TryMeasurePrefabLocalBounds(
                path,
                out _))
        {
            if (diagnosticConifer)
                Debug.LogWarning("[YQRuntimeWorldAssetRegistryBuilder] Conifer eligibility rejected: no mesh bounds: " + path);
            return false;
        }

        Renderer[] renderers =
            prefab.GetComponentsInChildren<Renderer>(
                true);
        if (renderers.Length == 0)
        {
            if (diagnosticConifer)
                Debug.LogWarning("[YQRuntimeWorldAssetRegistryBuilder] Conifer eligibility rejected: no renderers: " + path);
            return false;
        }

        for (int rendererIndex = 0;
             rendererIndex < renderers.Length;
             rendererIndex++)
        {
            Material[] materials =
                renderers[rendererIndex] != null
                    ? renderers[rendererIndex].sharedMaterials
                    : null;

            if (materials == null)
            {
                if (renderers[rendererIndex] is BillboardRenderer)
                {
                    // note: BillboardRenderer receives its material through the authored BillboardAsset and may expose no shared material slots.
                    continue;
                }

                return false;
            }

            for (int materialIndex = 0;
                 materialIndex < materials.Length;
                 materialIndex++)
            {
                Material material = materials[materialIndex];
                if (humanCharacter && material == null)
                    continue;
                if (material == null &&
                    renderers[rendererIndex] is BillboardRenderer)
                {
                    // note: A billboard LOD may retain an empty authored slot while its BillboardAsset supplies the visible material.
                    continue;
                }

                YQMaterialCompatibilityState state =
                    YQWorldAssetIntakeBuilder
                        .EvaluateRuntimeReadyMaterialForAudit(
                            material);

                if (!humanCharacter &&
                    state != YQMaterialCompatibilityState.VerifiedUrp &&
                    state != YQMaterialCompatibilityState.VerifiedUrpAdapter)
                {
                    if (diagnosticConifer)
                        Debug.LogWarning("[YQRuntimeWorldAssetRegistryBuilder] Conifer eligibility rejected: material " +
                                         (material != null ? material.name : "<null>") +
                                         " state=" + state + " path=" + path);
                    return false;
                }
            }
        }

        if (IsFootprintCriticalSlot(slot) &&
            prefab.GetComponentsInChildren<Collider>(
                true).Length == 0)
        {
            if (diagnosticConifer)
                Debug.LogWarning("[YQRuntimeWorldAssetRegistryBuilder] Conifer eligibility rejected: structural collider missing: " + path);
            return false;
        }

        // note: Vegetation and dressing may be non-blocking; structural slots require an authored collider before procedural selection.
        return true;
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

    private static List<GeneratedAssetReferenceRecord>
        FilterRuntimeEligibleDiscoveredReferences(
            List<GeneratedAssetReferenceRecord> references)
    {
        List<GeneratedAssetReferenceRecord> eligible =
            new List<GeneratedAssetReferenceRecord>();

        if (references == null)
            return eligible;

        for (int i = 0;
             i < references.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                references[i];

            // note: Only references that passed the runtime prefab/material gates can be selected by procedural spawning.
            if (reference != null &&
                reference.runtimeEligible)
            {
                eligible.Add(
                    reference);
            }
        }

        return eligible;
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

    internal static bool IsUsefulPrefabPath(
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

    internal static bool IsGenerationReadySpatialAsset(YQSpatialAssetRecord record)
    {
        // note: Inventory, shard publication, and exhaustive runtime validation share one definition of a generation-ready prefab.
        return record != null &&
               !string.IsNullOrWhiteSpace(record.assetPath) &&
               record.releaseEligible &&
               record.disposition == YQAssetIntakeDisposition.Candidate &&
               record.spatialMetadataAuthored &&
               record.curationV2 != null &&
               record.curationV2.contractVersion == YQAssetCurationContractV2.SupportedContractVersion &&
               IsUsefulPrefabPath(record.assetPath) &&
               !YQWorldAssetCatalog.IsRuntimeQuarantinedPath(record.assetPath);
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
        // note: Converted prefab palette integration reuses this filename classifier so slot ownership stays consistent with the existing discovery architecture.
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
                "forst conifers",
                "conifers botd"))
        {
            // note: The BOTD conifer family is shared dressing for the cold woodland palettes only.
            return new[]
            {
                "nordic_forest",
                "viking_rural",
                "hivemind_modular_viking_village",
                "hivemind_woodland_village",
                "hivemind_mountain_messenger"
            };
        }

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

        // note: Expand imported CamelCase and letter/number identities before tokenizing so SM_Building02 and SM_EagleFern classify by their actual semantic words.
        string expanded = Regex.Replace(value, @"([a-z])([A-Z])", "$1 $2");
        expanded = Regex.Replace(expanded, @"(?<=[A-Za-z])(?=[0-9])|(?<=[0-9])(?=[A-Za-z])", " ");

        char[] chars =
            expanded
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
        BuildVerifiedDiscoveredMaterialOverrides(
            GameObject prefab,
            string assetPath)
    {
        List<YQRuntimeWorldMaterialOverride> bindings =
            new List<YQRuntimeWorldMaterialOverride>();

        // note: Other discovered prefabs already use their native URP surfaces; only Hivemind's reviewed GUID adapters need explicit bindings.
        if (prefab == null ||
            string.IsNullOrWhiteSpace(assetPath) ||
            !assetPath.StartsWith("Assets/HIVEMIND/", StringComparison.OrdinalIgnoreCase))
            return bindings;

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null)
                continue;

            int rendererIndex = GetRendererIndexOnTransform(renderer);
            if (rendererIndex < 0)
                continue;

            string transformPath = AnimationUtility.CalculateTransformPath(
                renderer.transform, prefab.transform);
            Material[] sources = renderer.sharedMaterials;
            for (int slot = 0; sources != null && slot < sources.Length; slot++)
            {
                // note: Eligibility rejects unresolved slots; bind only the same verified material chosen by the intake resolver.
                if (!YQWorldAssetIntakeBuilder.TryResolveVerifiedRuntimeMaterial(
                        sources[slot], out Material replacement) ||
                    replacement == sources[slot])
                    continue;

                bindings.Add(new YQRuntimeWorldMaterialOverride
                {
                    transformPath = transformPath,
                    rendererIndex = rendererIndex,
                    materialIndex = slot,
                    replacementMaterial = replacement
                });
            }
        }

        return bindings;
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
    private static Dictionary<string, YQSpatialAssetRecord> _reviewedSpatialByPath;
    private static bool _reviewedSpatialIndexLoaded;

    internal static YQMaterialCompatibilityState EvaluateRuntimeReadyMaterialForAudit(
        Material material)
    {
        // note: Keep the registry audit on the same URP counterpart and adapter resolver used by intake.
        return EvaluateRuntimeReadyMaterial(
            material,
            out _,
            out _,
            out _);
    }

    internal static bool TryResolveVerifiedRuntimeMaterial(
        Material source,
        out Material runtimeMaterial)
    {
        runtimeMaterial = null;
        if (source == null)
            return false;

        // note: Resolve the exact persisted material path used by intake eligibility, so the registry never advertises an unbound HDRP slot.
        YQMaterialCompatibilityState state = EvaluateRuntimeReadyMaterial(
            source, out _, out string runtimePath, out _);
        if (state != YQMaterialCompatibilityState.VerifiedUrp &&
            state != YQMaterialCompatibilityState.VerifiedUrpAdapter)
            return false;

        runtimeMaterial = AssetDatabase.LoadAssetAtPath<Material>(runtimePath);
        return runtimeMaterial != null;
    }

    private const string BenchmarkAutoScanAttemptedKey =
        "YourQuest.WG1.BenchmarkAutoScanAttempted";

    private const string UnattendedBenchmarkRequestFileName =
        "YQ_WG1_UNATTENDED_SCAN.request";

    // note: This marker lets the already-open Unity editor run the authoritative all-library intake pass without starting a competing editor process.
    private const string UnattendedAllAssetScanRequestFileName =
        "YQ_ALL_ASSET_SCAN.request";

    // note: This marker reruns only the persisted-catalog runtime audit after editor code changes, avoiding a second multi-hour asset import.
    private const string UnattendedRuntimeAuditRequestFileName =
        "YQ_RUNTIME_ASSET_AUDIT.request";

    // note: This marker migrates the existing catalog and shards to the current runtime-eligibility contract without reimporting source packs.
    private const string UnattendedRuntimeEligibilityRequestFileName =
        "YQ_RUNTIME_ELIGIBILITY.request";

    public const string IntakeCatalogPath =
        "Assets/Assets/Resources/YQWorldAssetIntakeCatalog.asset";

    // note: Benchmark scans are disposable evidence and must never overwrite the canonical all-library inventory.
    private const string BenchmarkIntakeCatalogPath =
        "Assets/Assets/GeneratedAssets/WorldIntake/YQWorldAssetIntakeCatalog_Benchmark.asset";

    private const string IntakeReportFolder =
        "Assets/Assets/GeneratedAssets/WorldIntake";

    private const string BenchmarkMaterialAdapterFolder =
        IntakeReportFolder +
        "/Materials/MedievalVikingVillage";

    public const string IntakeReportPath =
        IntakeReportFolder +
        "/YQWorldAssetIntakeReport.md";

    private const string GenerationInventoryPath =
        IntakeReportFolder +
        "/YQWorldGenerationAssetInventory.json";

    private const string BenchmarkIntakeReportPath =
        IntakeReportFolder +
        "/YQWorldAssetIntakeReport_Benchmark.md";

    private const string IntakeCatalogScriptGuid =
        "e22f654b2f180f84e8037e73afde756d";

    private static readonly Dictionary<int, bool>
        UniversalShaderGraphTargetCache =
            new Dictionary<int, bool>();

    // note: The explicit all-library audit request stays on the editor update loop until Unity reaches a stable AssetDatabase boundary.
    private static bool _allAssetScanRequestPolling;
    private static double _nextAllAssetScanPollTime;

    internal enum GenerationInventoryReviewPolicyScope
    {
        LegacyWorldKit = 0,
        LibraryCoverage = 1
    }

    [Serializable]
    internal sealed class GenerationInventoryAsset
    {
        public string stableAssetId;
        public string sourceGuid;
        public string sourceAssetKey;
        public string assetPath;
        public string assetType;
        public string assetFamily;
        public string sourceRoot;
        public string registryState;
        public string slotTag;
        public bool runtimeEligible;
        public string finalState;
        public string finalStateReason;
        public string representedByAssetPath;
        public string intakeDisposition;
        public List<string> technicalIssues;
        public string classificationStatus;
        public string placementContextStatus;
        public string paletteAssignmentStatus;
        public string technicalValidationStatus;
        public string reviewDisposition;
        public string reviewPolicyVersion;
        public GenerationInventoryReviewPolicyScope reviewPolicyScope;
        public List<string> libraryEvidenceIds = new List<string>();
        public string librarySourceId;
        public string sourceVersionId;
        public string baselineSourceVersionId;
        public string declaredSourceSha256;
        public string installedSourceSha256;
        public string declaredSourceKind;
        public string sourceAvailability;
        public List<string> sourceAliases = new List<string>();
        public List<string> sourceDeclarationRefs = new List<string>();
    }

    [Serializable]
    internal sealed class GenerationInventoryKitEvidenceLinks
    {
        public string kitId;
        public List<string> libraryEvidenceIds = new List<string>();
    }

    [Serializable]
    internal sealed class GenerationInventoryDocument
    {
        public string schemaVersion = "yq_world_generation_asset_inventory_v7";
        public int libraryContractVersion = YQAssetLibraryEvidenceRecord.SupportedContractVersion;
        public string generatedUtc;
        public string scanScope;
        public string intakeCatalog;
        public string reviewPolicyVersion = "world-kit-policy-v1";
        public List<string> reviewPolicyNotes = new List<string>();
        public List<string> approvedRoots = new List<string>();
        public List<GenerationInventoryAsset> assets = new List<GenerationInventoryAsset>();
        public GenerationInventoryCounts counts = new GenerationInventoryCounts();
        public List<YQAssetLibraryEvidenceRecord> libraryEvidence = new List<YQAssetLibraryEvidenceRecord>();
        public List<GenerationInventoryKitEvidenceLinks> kitEvidenceLinks = new List<GenerationInventoryKitEvidenceLinks>();
        public int libraryCoverageVersion;
        public GenerationInventoryCounts legacyCounts;
        public List<string> libraryObservationRoots = new List<string>();
        public List<GenerationInventorySourceSnapshot> sourceSnapshots = new List<GenerationInventorySourceSnapshot>();
        public List<GenerationInventorySourceVersion> sourceVersions = new List<GenerationInventorySourceVersion>();
        public List<GenerationInventoryDomainBinding> domainBindings = new List<GenerationInventoryDomainBinding>();
        public List<GenerationInventoryCoverageConflict> coverageConflicts = new List<GenerationInventoryCoverageConflict>();
        public List<GenerationInventoryRootObservation> rootObservations = new List<GenerationInventoryRootObservation>();
        public string currentIntakeCatalogSnapshotId;
    }

    [Serializable]
    internal sealed class GenerationInventoryCounts
    {
        public int total;
        public int prefabs;
        public int materials;
        public int sourceAssets;
        public int generationReady;
        public int pendingReview;
        public int quarantined;
        public int intentionallyExcluded;
        public int representedVariants;
        public int notApplicable;
    }

    // note: Coverage DTOs account for sources and existing consumers; they are not selectable registry or approval records.
    [Serializable]
    internal sealed class GenerationInventorySourceSnapshot
    {
        public string path, sha256, identityBasis;
        public long bytes;
    }

    [Serializable]
    internal sealed class GenerationInventorySourceRelation
    {
        public string relationId, sourceId, sourceVersionId, installedSourceVersionId, declaredPath, declaredSha256, role, joinState, joinMethod;
    }

    [Serializable]
    internal sealed class GenerationInventorySourceVersion
    {
        public string sourceId, sourceVersionId, sha256, identityBasis;
        public List<string> declarationRefs = new List<string>();
    }

    [Serializable]
    internal sealed class GenerationInventoryDomainBinding
    {
        public string bindingId, bindingSnapshotId, domain, assetId, bindingKey, registryGuid, resolvedAssetPath;
        public string family, category, kind, species, moduleSlot, compatibilityId, eligibilityReason, catalogEvidenceRef;
        public bool? domainEligible;
        public List<GenerationInventorySourceRelation> sources = new List<GenerationInventorySourceRelation>();
        public List<YQAssetLibraryConsumerEvidenceRecord> consumerEvidence = new List<YQAssetLibraryConsumerEvidenceRecord>();
        public GenerationInventoryCurrentIntakeContract currentIntakeContract;
    }

    [Serializable]
    internal sealed class GenerationInventoryCurrentIntakeContract
    {
        // note: These are nullable observations of canonical intake, independent of historical review and art approval.
        public int contractVersion;
        public string catalogSnapshotId, intakeRecordSha256, sourceGuid, sourceAssetKey, semanticRole, technicalPredicateVersion;
        public int? dispositionValue, curationContractVersion;
        public string dispositionName;
        public bool? releaseEligible, spatialMetadataAuthored;
        public List<string> libraryEvidenceIds = new List<string>();
        public List<string> curationEvidenceIds = new List<string>();
    }

    [Serializable]
    internal sealed class GenerationInventoryCoverageConflict
    {
        public string kind, subject, reason;
    }

    [Serializable]
    internal sealed class GenerationInventoryRootObservation
    {
        public string root, availability, metadataSha256;
        public int files;
        public bool complete;
    }

    internal sealed class LibraryCoverageInputs
    {
        public GenerationInventoryDocument master;
        public string currentIntakeCatalogSnapshotId;
        public List<GenerationInventoryAsset> sources = new List<GenerationInventoryAsset>();
        public List<GenerationInventoryDomainBinding> bindings = new List<GenerationInventoryDomainBinding>();
        public List<YQAssetLibraryEvidenceRecord> evidence = new List<YQAssetLibraryEvidenceRecord>();
        public List<GenerationInventorySourceSnapshot> snapshots = new List<GenerationInventorySourceSnapshot>();
        public List<GenerationInventoryCoverageConflict> conflicts = new List<GenerationInventoryCoverageConflict>();
        public List<GenerationInventoryRootObservation> observations = new List<GenerationInventoryRootObservation>();
        public List<string> roots = new List<string>();
    }

    internal static readonly string[] DefaultLibraryObservationRoots =
    {
        "Assets/ADG_Textures", "Assets/BefourStudios", "Assets/Forst", "Assets/GabrielAguiarProductions",
        "Assets/Grass And Flowers Pack 1", "Assets/HIVEMIND", "Assets/HumbleBundleResources",
        "Assets/Magic Pig Games (Infinity PBR)", "Assets/Tom's Terrain Tools", "Assets/YughuesFreeBushes2018",
        "Assets/YourQuest DOT Creatures", "Assets/YourQuest DOT Equipment", "Assets/Assets/Art", "Assets/Assets/Terrain",
        "Assets/Assets/GeneratedAssets", "Assets/Assets/Resources", "SourceAssets/DOT"
    };

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

        // note: Schedule the broad audit only through an explicit request marker so ordinary editor launches remain unchanged.
        EditorApplication.delayCall +=
            TryRunUnattendedAllAssetScanRequest;

        if (!_allAssetScanRequestPolling)
        {
            _allAssetScanRequestPolling = true;
            EditorApplication.update +=
                TryRunUnattendedAllAssetScanRequest;
        }
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

        // note: Re-check an explicit all-library audit request after Unity returns to Edit mode.
        EditorApplication.delayCall +=
            TryRunUnattendedAllAssetScanRequest;
    }

    private static void TryRunUnattendedAllAssetScanRequest()
    {
        // note: Poll once per second so an unattended request is responsive without adding per-frame filesystem work to the editor.
        if (EditorApplication.timeSinceStartup < _nextAllAssetScanPollTime)
            return;

        _nextAllAssetScanPollTime =
            EditorApplication.timeSinceStartup + 1d;

        // note: Resolve the marker relative to the project so the request remains local and cannot alter imported asset contents.
        string requestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/" + UnattendedAllAssetScanRequestFileName));
        string auditRequestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/" + UnattendedRuntimeAuditRequestFileName));
        string eligibilityRequestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/" + UnattendedRuntimeEligibilityRequestFileName));
        bool scanRequested = File.Exists(requestPath);
        bool auditRequested = File.Exists(auditRequestPath);
        bool eligibilityRequested = File.Exists(eligibilityRequestPath);
        if (!scanRequested && !auditRequested && !eligibilityRequested)
        {
            // note: Keep the lightweight poll registered because the marker may be created after domain reload by an unattended audit launcher.
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            // note: AssetDatabase owns import consistency; retry only after the current editor transaction reaches a safe boundary.
            return;
        }

        if (!scanRequested && eligibilityRequested)
        {
            // note: Update persisted references and shards from the current catalog without restarting the source-library scan.
            YQRuntimeWorldAssetRegistryBuilder.RebuildDiscoveredRuntimeEligibility();
            if (File.Exists(eligibilityRequestPath))
            {
                File.Delete(eligibilityRequestPath);
                Debug.Log("[YQWorldAssetIntakeBuilder] Unattended runtime eligibility migration completed and cleared.");
            }

            _allAssetScanRequestPolling = false;
            EditorApplication.update -=
                TryRunUnattendedAllAssetScanRequest;
            return;
        }

        if (!scanRequested)
        {
            // note: Validate the existing catalog against the current resolver without rescanning imported libraries.
            YQRuntimeWorldAssetRegistryBuilder.ValidateDiscoveredRuntimeReferences();
            if (File.Exists(YQRuntimeWorldAssetRegistryBuilder.DiscoveredRuntimeAuditReportPath) &&
                File.Exists(auditRequestPath))
            {
                File.Delete(auditRequestPath);
                Debug.Log("[YQWorldAssetIntakeBuilder] Unattended runtime asset audit completed and cleared.");
            }

            _allAssetScanRequestPolling = false;
            EditorApplication.update -=
                TryRunUnattendedAllAssetScanRequest;
            return;
        }

        // note: Run the existing approved-root scanner so every library shares the same catalog, material, and metadata rules.
        ScanAllAssetLibraries();

        // note: Rebuild lazy runtime shards from the same corrected semantic pass so editor discovery and player builds resolve identical references.
        YQRuntimeWorldAssetRegistryBuilder.RebuildRegistryWithDiscoveredAssets();

        // note: Validate the persisted catalog after shard rebuild so a successful scan cannot hide a missing prefab, collider, shader, or dependency.
        YQRuntimeWorldAssetRegistryBuilder.ValidateDiscoveredRuntimeReferences();

        YQWorldAssetIntakeCatalog generated = AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(IntakeCatalogPath);
        if (generated != null &&
            string.Equals(generated.ScanScope, "all_configured_asset_libraries", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(requestPath))
        {
            // note: Clear the request only after a catalog exists so an interrupted scan can be retried safely.
            File.Delete(requestPath);
            Debug.Log("[YQWorldAssetIntakeBuilder] Unattended all-library asset scan completed and cleared.");

            _allAssetScanRequestPolling = false;
            EditorApplication.update -=
                TryRunUnattendedAllAssetScanRequest;
        }
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
                BenchmarkIntakeCatalogPath);

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
                BenchmarkIntakeCatalogPath);

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
            "first_benchmark_kit",
            BenchmarkIntakeCatalogPath,
            BenchmarkIntakeReportPath);
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Asset Intake/Approve Benchmark Decorative Candidates")]
    public static void ApproveBenchmarkDecorativeCandidates()
    {
        // note: This opt-in pass approves only measured, collider-backed, non-structural Viking dressing; buildings, routes, portals and ambiguous source prefabs remain in authored review.
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning("[YQWorldAssetIntakeBuilder] Benchmark candidate approval requires stable Edit mode.");
            return;
        }

        YQWorldAssetIntakeCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(BenchmarkIntakeCatalogPath);
        if (catalog == null)
        {
            // note: A clean batch editor may import the benchmark asset after startup; build the disposable snapshot once before treating it as missing.
            ScanFirstBenchmarkKit();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            catalog = AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(BenchmarkIntakeCatalogPath);
        }
        if (catalog == null || catalog.Kits == null || catalog.SpatialAssets == null)
        {
            Debug.LogError("[YQWorldAssetIntakeBuilder] Benchmark intake catalog is missing; scan it before approval.");
            return;
        }

        int approved = 0;
        bool hasCandidate = false;
        for (int index = 0; index < catalog.SpatialAssets.Count; index++)
        {
            YQSpatialAssetRecord record = catalog.SpatialAssets[index];
            if (record != null && record.disposition == YQAssetIntakeDisposition.Candidate && record.releaseEligible)
                hasCandidate = true;
            if (!CanApproveBenchmarkDecorative(record))
                continue;

            ApplyBenchmarkDecorativeReview(record);
            approved++;
        }

        for (int index = 0; index < catalog.Kits.Count; index++)
        {
            YQAssetKitManifest kit = catalog.Kits[index];
            if (kit != null && kit.isFirstBenchmarkKit && (approved > 0 || hasCandidate))
                kit.releaseEligible = true;
        }

        catalog.RecalculateKitSpatialCounts();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        // note: Rescan preserves the authored records, refreshes measured dependencies, and rewrites the machine-readable inventory in one transaction.
        ScanFirstBenchmarkKit();
        MergeBenchmarkApprovalsIntoCanonicalCatalog();
        Debug.Log("[YQWorldAssetIntakeBuilder] BENCHMARK DECORATIVE CANDIDATES APPROVED: " + approved);
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Asset Intake/Approve Safe Decorative Candidates")]
    public static void ApproveSafeDecorativeCandidates()
    {
        // note: This pass applies the same explicit spatial contract to small decorative roles in every kit while preserving each kit's own style and release boundary.
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning("[YQWorldAssetIntakeBuilder] Decorative candidate approval requires stable Edit mode.");
            return;
        }
        YQWorldAssetIntakeCatalog catalog = AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(IntakeCatalogPath);
        if (catalog == null || catalog.Kits == null || catalog.SpatialAssets == null)
        {
            Debug.LogError("[YQWorldAssetIntakeBuilder] Canonical intake catalog is missing; scan approved libraries first.");
            return;
        }

        int approved = 0;
        HashSet<string> approvedKits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < catalog.SpatialAssets.Count; index++)
        {
            YQSpatialAssetRecord record = catalog.SpatialAssets[index];
            if (!CanApproveDecorative(record))
                continue;
            ApplyBenchmarkDecorativeReview(record);
            approvedKits.Add(record.kitId);
            approved++;
        }
        for (int index = 0; index < catalog.Kits.Count; index++)
        {
            YQAssetKitManifest kit = catalog.Kits[index];
            if (kit != null && approvedKits.Contains(kit.kitId))
                kit.releaseEligible = true;
        }
        catalog.RecalculateKitSpatialCounts();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        ScanAllAssetLibraries();
        Debug.Log("[YQWorldAssetIntakeBuilder] SAFE DECORATIVE CANDIDATES APPROVED: " + approved + ", kits=" + approvedKits.Count);
    }

    private static void MergeBenchmarkApprovalsIntoCanonicalCatalog()
    {
        // note: The benchmark snapshot is evidence for one family; copy only its approved authored contracts into the canonical all-library catalog.
        YQWorldAssetIntakeCatalog benchmark = AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(BenchmarkIntakeCatalogPath);
        YQWorldAssetIntakeCatalog canonical = AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(IntakeCatalogPath);
        if (benchmark == null || canonical == null || benchmark.SpatialAssets == null || canonical.SpatialAssets == null)
        {
            Debug.LogWarning("[YQWorldAssetIntakeBuilder] Canonical catalog merge skipped because one intake snapshot is unavailable.");
            return;
        }

        Dictionary<string, YQSpatialAssetRecord> approvedByPath = new Dictionary<string, YQSpatialAssetRecord>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < benchmark.SpatialAssets.Count; index++)
        {
            YQSpatialAssetRecord record = benchmark.SpatialAssets[index];
            if (record != null && record.disposition == YQAssetIntakeDisposition.Candidate && record.releaseEligible && record.spatialMetadataAuthored)
                approvedByPath[NormalizePath(record.assetPath)] = record;
        }

        int merged = 0;
        for (int index = 0; index < canonical.SpatialAssets.Count; index++)
        {
            YQSpatialAssetRecord target = canonical.SpatialAssets[index];
            if (target == null || !approvedByPath.TryGetValue(NormalizePath(target.assetPath), out YQSpatialAssetRecord source))
                continue;
            CopyAuthoredSpatialReview(target, source);
            merged++;
        }
        for (int index = 0; index < canonical.Kits.Count; index++)
        {
            YQAssetKitManifest kit = canonical.Kits[index];
            if (kit != null && string.Equals(kit.kitId, "assets_befourstudios_medievalvikingvillage", StringComparison.OrdinalIgnoreCase) && merged > 0)
                kit.releaseEligible = true;
        }
        canonical.RecalculateKitSpatialCounts();
        EditorUtility.SetDirty(canonical);
        AssetDatabase.SaveAssets();
        ScanAllAssetLibraries();
        Debug.Log("[YQWorldAssetIntakeBuilder] CANONICAL BENCHMARK APPROVALS MERGED: " + merged);
    }

    private static bool CanApproveBenchmarkDecorative(YQSpatialAssetRecord record)
    {
        if (record == null || record.disposition != YQAssetIntakeDisposition.NeedsSpatialReview ||
            string.IsNullOrWhiteSpace(record.kitId) ||
            !record.kitId.Equals("assets_befourstudios_medievalvikingvillage", StringComparison.OrdinalIgnoreCase) ||
            record.validationIssues == null || record.validationIssues.Count != 0 ||
            !record.hasRenderer || !record.hasCollider || record.missingScriptCount != 0 ||
            record.invalidMaterialSlotCount != 0 || record.materialReviewSlotCount != 0 ||
            record.localBoundsSize.sqrMagnitude <= 0.0001f)
            return false;

        string role = (record.semanticRole ?? string.Empty).Trim().ToLowerInvariant();
        return role == "exterior_deco" || role == "floor_deco" || role == "vegetation" ||
               role == "rock" || role == "lighting";
    }

    private static bool CanApproveDecorative(YQSpatialAssetRecord record)
    {
        if (record == null || record.disposition != YQAssetIntakeDisposition.NeedsSpatialReview ||
            string.IsNullOrWhiteSpace(record.kitId) || record.validationIssues == null || record.validationIssues.Count != 0 ||
            !record.hasRenderer || !record.hasCollider || record.missingScriptCount != 0 ||
            record.invalidMaterialSlotCount != 0 || record.materialReviewSlotCount != 0 ||
            record.localBoundsSize.sqrMagnitude <= 0.0001f ||
            (record.compositionScale != YQSpatialCompositionScale.Atom &&
             record.compositionScale != YQSpatialCompositionScale.Prop &&
             record.compositionScale != YQSpatialCompositionScale.Module))
            return false;
        string role = (record.semanticRole ?? string.Empty).Trim().ToLowerInvariant();
        return role == "exterior_deco" || role == "floor_deco" || role == "vegetation" || role == "rock" || role == "lighting";
    }

    private static void ApplyBenchmarkDecorativeReview(YQSpatialAssetRecord record)
    {
        string role = (record.semanticRole ?? string.Empty).Trim().ToLowerInvariant();
        bool natural = role == "vegetation" || role == "rock";
        bool light = role == "lighting";
        float halfX = Mathf.Max(0.05f, record.footprintX * 0.5f);
        float halfZ = Mathf.Max(0.05f, record.footprintZ * 0.5f);

        record.spatialMetadataAuthored = true;
        record.allowedSlopeDegrees = natural ? 45f : 25f;
        record.foundationProfile = "ground";
        record.roadRelationship = "edge_or_open";
        record.navigationProfile = "walkable_clearance";
        record.frontDirection = Vector3.forward;
        record.frontDirectionAuthored = false;
        record.disposition = YQAssetIntakeDisposition.Candidate;
        record.releaseEligible = true;
        record.curationV2 = new YQAssetCurationContractV2
        {
            contractVersion = YQAssetCurationContractV2.SupportedContractVersion,
            primaryRole = natural
                ? (role == "rock" ? YQAssetRoleV2.NaturalFeature : YQAssetRoleV2.GroundCover)
                : (light ? YQAssetRoleV2.LightSource : YQAssetRoleV2.Dressing),
            primaryFunction = natural ? YQAssetFunctionV2.Ecology :
                (light ? YQAssetFunctionV2.Infrastructure : YQAssetFunctionV2.Civic),
            environments = new List<YQAssetEnvironmentV2> { YQAssetEnvironmentV2.Exterior },
            familyId = record.kitId,
            variantGroupId = record.stableAssetId,
            supportMode = YQAssetSupportModeV2.Ground,
            canonicalScale = Vector3.one,
            preserveAuthoredScale = true,
            minimumEmbedDepth = 0f,
            maximumEmbedDepth = 0.25f,
            maximumSupportRelief = 0.5f,
            maxUsesPerSite = natural ? 12 : 8,
            minimumRepeatDistance = Mathf.Max(2f, Mathf.Max(record.footprintX, record.footprintZ)),
            supportPolygon = new List<Vector2>
            {
                new Vector2(-halfX, -halfZ), new Vector2(-halfX, halfZ),
                new Vector2(halfX, halfZ), new Vector2(halfX, -halfZ)
            },
            ecology = role == "vegetation" ? new YQAssetEcologyContractV2
            {
                layer = YQEcologyLayerV2.GroundCover,
                speciesFamilyId = record.kitId + "_vegetation",
                minimumMoisture = 0f,
                maximumMoisture = 1f,
                minimumNormalizedElevation = 0f,
                maximumNormalizedElevation = 1f,
                minimumSpacing = Mathf.Max(2f, Mathf.Max(record.footprintX, record.footprintZ)),
                cohortMinimum = 1,
                cohortMaximum = natural ? 4 : 2,
                disturbanceTolerance = 0.5f
            } : null
        };
        record.curationV2.EnsureCollections();
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
            "all_configured_asset_libraries",
            IntakeCatalogPath,
            IntakeReportPath);
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
        string scanScope,
        string outputCatalogPath,
        string outputReportPath)
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

        // note: The canonical inventory also accounts for source models and prefab/material dependencies that are absent from typed prefab/material search results.
        List<GenerationInventoryAsset> sourceAssets =
            new List<GenerationInventoryAsset>();

        HashSet<string> scannedSourcePaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // note: Preserve authored spatial review by normalized path while the scan refreshes measured renderer, collider, and material evidence.
        Dictionary<string, YQSpatialAssetRecord> preservedSpatialReviews =
            LoadPreservedSpatialReviews(outputCatalogPath);
        Dictionary<string, bool> preservedKitEligibility =
            LoadPreservedKitEligibility(outputCatalogPath);

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
                PreserveKitEligibility(kit, preservedKitEligibility);

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

                    PreserveReviewedSpatialContract(
                        record,
                        preservedSpatialReviews);

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

                if (string.Equals(outputCatalogPath, IntakeCatalogPath, StringComparison.OrdinalIgnoreCase))
                {
                    // note: Every non-folder source path is retained; ambiguous models/scenes stay pending review instead of being silently dismissed as dependencies.
                    for (int i = 0; i < allGuids.Length; i++)
                    {
                        string path = NormalizePath(AssetDatabase.GUIDToAssetPath(allGuids[i]));
                        if (string.IsNullOrWhiteSpace(path) ||
                            AssetDatabase.IsValidFolder(path) ||
                            !scannedSourcePaths.Add(path))
                            continue;

                        sourceAssets.Add(BuildSourceInventoryAsset(allGuids[i], path, kit));
                    }
                }

                // note: Preserve an existing authored kit approval while leaving newly discovered kits unapproved.

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

            YQWorldAssetIntakeCatalog savedCatalog = SaveCatalog(
                scanScope,
                kits,
                spatialAssets,
                materials,
                outputCatalogPath);

            WriteSummaryReport(
                scanScope,
                kits,
                spatialAssets,
                materials,
                outputReportPath);

            if (string.Equals(outputCatalogPath, IntakeCatalogPath, StringComparison.OrdinalIgnoreCase))
            {
                // note: The canonical manifest is emitted from typed intake records so folded YAML, duplicate paths, and parser drift cannot hide assets.
                WriteGenerationInventory(
                    roots,
                    kits,
                    spatialAssets,
                    materials,
                    sourceAssets,
                    savedCatalog.LibraryEvidence);
            }

            Debug.Log(
                "[YQWorldAssetIntakeBuilder] INTAKE COMPLETE\n" +
                "Scope: " + scanScope + "\n" +
                "Kits: " + kits.Count + "\n" +
                "Prefabs recorded: " + spatialAssets.Count + "\n" +
                "Materials recorded: " + materials.Count + "\n" +
                "Catalog: " + outputCatalogPath + "\n" +
                "Report: " + outputReportPath + "\n" +
                "Authored release gates were preserved; eligible records are reported in the intake summary.");
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

        // note: Every intake kit receives explicit V2 visual axes during discovery; release approval remains a separate authored gate.
        kit.styleV2 =
            BuildKitStyleContract(
                root);

        return kit;
    }

    private static YQKitStyleContractV2 BuildKitStyleContract(
        string root)
    {
        string search =
            (root ?? string.Empty).ToLowerInvariant();
        YQKitStyleContractV2 contract =
            new YQKitStyleContractV2
            {
                contractVersion =
                    YQKitStyleContractV2.SupportedContractVersion
            };
        contract.EnsureCollections();

        // note: The conservative default covers outdoor dressing while pack-specific branches add construction and technology families.
        contract.environments.Add(YQAssetEnvironmentV2.Exterior);
        if (ContainsAny(search, "mansion", "cathedral", "hospital", "witch", "house", "dungeon", "tomb"))
            contract.environments.Add(YQAssetEnvironmentV2.Interior);

        if (ContainsAny(search, "cyberpunk", "scifi", "engineers", "container", "military"))
            contract.technologyBands.Add(YQTechnologyBandV2.Electrified);
        else if (ContainsAny(search, "biohorror", "horrorhospital"))
            contract.technologyBands.Add(YQTechnologyBandV2.Advanced);
        else
            contract.technologyBands.Add(YQTechnologyBandV2.Manual);

        if (ContainsAny(search, "viking", "nordic", "rural", "medieval", "nativeamerican", "pirate", "townsmith"))
            contract.constructionFamilies.Add(YQConstructionFamilyV2.Timber);
        if (ContainsAny(search, "desert", "persepolis", "cathedral", "temple", "dungeon", "tomb", "ruin", "olympus"))
            contract.constructionFamilies.Add(YQConstructionFamilyV2.Masonry);
        if (ContainsAny(search, "cyberpunk", "container", "military", "scifi", "hospital"))
            contract.constructionFamilies.Add(YQConstructionFamilyV2.Metal);
        if (ContainsAny(search, "terrain", "bush", "conifer", "forest", "nativeamerican"))
            contract.constructionFamilies.Add(YQConstructionFamilyV2.Organic);

        if (contract.constructionFamilies.Count == 0)
            contract.constructionFamilies.Add(YQConstructionFamilyV2.NotApplicable);

        if (ContainsAny(search, "desert", "western", "persepolis", "ancient"))
            contract.climateTags.Add("arid");
        else if (ContainsAny(search, "nordic", "viking", "mountain", "conifer", "medieval"))
            contract.climateTags.Add("temperate_cold");
        else if (ContainsAny(search, "pirate", "island", "sewer", "wet"))
            contract.climateTags.Add("coastal_wet");
        else
            contract.climateTags.Add("temperate");

        contract.shapeLanguageTags.Add(
            ContainsAny(search, "cyberpunk", "scifi", "container")
                ? "industrial_modular"
                : "authored_modular");
        return contract;
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

        // note: Reuse only source-GUID-matched Hivemind adapters already on disk; never create a generic replacement during eligibility scanning.
        if (runtimeMaterialPath.StartsWith(
                "Assets/HIVEMIND/", StringComparison.OrdinalIgnoreCase))
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(runtimeMaterialPath);
            if (!string.IsNullOrWhiteSpace(sourceGuid))
            {
                string hivemindPath =
                    YQRuntimeWorldAssetRegistryBuilder.HivemindUrpMaterialsFolder +
                    "/" + sourceGuid + ".mat";
                Material hivemindAdapter =
                    AssetDatabase.LoadAssetAtPath<Material>(hivemindPath);
                if (hivemindAdapter != null &&
                    EvaluateMaterial(hivemindAdapter, out _) ==
                        YQMaterialCompatibilityState.VerifiedUrp)
                {
                    issue = string.Empty;
                    runtimeMaterialPath = hivemindPath;
                    compatibilityStrategy = "existing_hivemind_guid_urp_adapter";
                    return YQMaterialCompatibilityState.VerifiedUrpAdapter;
                }
            }
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
                // note: Existing discovered records receive the same reviewed-contract projection as newly scanned records.
                ApplyReviewedSpatialContract(record);
                result.Add(
                    path,
                    record);
            }
        }

        return result;
    }

    internal static void ApplyReviewedSpatialContract(GeneratedAssetReferenceRecord reference)
    {
        if (reference == null || string.IsNullOrWhiteSpace(reference.assetPath))
            return;

        EnsureReviewedSpatialIndex();
        if (_reviewedSpatialByPath == null ||
            !_reviewedSpatialByPath.TryGetValue(NormalizePath(reference.assetPath), out YQSpatialAssetRecord spatial) ||
            spatial == null)
            return;

        spatial.EnsureCollections();
        YQAssetCurationContractV2 curation = spatial.curationV2;
        if (!spatial.releaseEligible ||
            spatial.disposition != YQAssetIntakeDisposition.Candidate ||
            !spatial.spatialMetadataAuthored ||
            curation == null ||
            curation.contractVersion != YQAssetCurationContractV2.SupportedContractVersion)
            return;

        // note: Mark the projection so palette slot contracts preserve authored scale, grounding, repetition, and navigation values.
        AddUnique(reference.subTags, "reviewed_spatial_contract");

        if (IsKnownSlot(spatial.semanticRole))
            reference.slotTag = spatial.semanticRole;

        if (spatial.footprintX > 0f)
            reference.footprintX = spatial.footprintX;
        if (spatial.footprintZ > 0f)
            reference.footprintZ = spatial.footprintZ;

        if (curation.canonicalScale.x > 0f && curation.canonicalScale.y > 0f && curation.canonicalScale.z > 0f)
        {
            float minimumScale = Mathf.Min(curation.canonicalScale.x, Mathf.Min(curation.canonicalScale.y, curation.canonicalScale.z));
            float maximumScale = Mathf.Max(curation.canonicalScale.x, Mathf.Max(curation.canonicalScale.y, curation.canonicalScale.z));
            reference.scaleMin = minimumScale;
            reference.scaleMax = maximumScale;
        }

        if (spatial.allowedSlopeDegrees > 0f)
            AddUnique(reference.subTags, "slope_" + Mathf.RoundToInt(spatial.allowedSlopeDegrees) + "deg");
        AddReviewedEnumTag(reference, "role", curation.primaryRole);
        AddReviewedEnumTag(reference, "function", curation.primaryFunction);
        AddReviewedEnumTag(reference, "support", curation.supportMode);
        AddReviewedEnumTags(reference, "role", curation.secondaryRoles);
        AddReviewedEnumTags(reference, "function", curation.secondaryFunctions);
        AddReviewedEnumTags(reference, "environment", curation.environments);
        AddReviewedEnumTags(reference, "affordance", curation.affordances);
        AddUnique(reference.subTags, NormalizeReviewTag(spatial.kitId));
        AddUnique(reference.subTags, NormalizeReviewTag(spatial.foundationProfile));
        AddUnique(reference.subTags, NormalizeReviewTag(spatial.roadRelationship));
        AddUnique(reference.subTags, NormalizeReviewTag(spatial.navigationProfile));
        AddUnique(reference.subTags, NormalizeReviewTag(curation.familyId));
        AddUnique(reference.subTags, NormalizeReviewTag(curation.variantGroupId));

        if (curation.maxUsesPerSite == 1)
            reference.allowRepeat = false;

        switch (curation.supportMode)
        {
            case YQAssetSupportModeV2.Foundation:
                reference.placementRule = "snap_to_foundation";
                break;
            case YQAssetSupportModeV2.Floor:
                reference.placementRule = "snap_to_floor";
                break;
            case YQAssetSupportModeV2.Wall:
                reference.placementRule = "attach_to_valid_wall";
                break;
            case YQAssetSupportModeV2.Ceiling:
                reference.placementRule = "attach_to_valid_ceiling";
                break;
            case YQAssetSupportModeV2.WaterSurface:
                reference.placementRule = "snap_to_water_surface";
                break;
            case YQAssetSupportModeV2.TerrainEmbedded:
                reference.placementRule = "embed_in_terrain";
                break;
        }
    }

    private static void EnsureReviewedSpatialIndex()
    {
        if (_reviewedSpatialIndexLoaded)
            return;

        _reviewedSpatialIndexLoaded = true;
        _reviewedSpatialByPath = new Dictionary<string, YQSpatialAssetRecord>(StringComparer.OrdinalIgnoreCase);
        YQWorldAssetIntakeCatalog catalog = AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(YQWorldAssetIntakeBuilder.IntakeCatalogPath);
        if (catalog == null || catalog.SpatialAssets == null)
            return;

        for (int index = 0; index < catalog.SpatialAssets.Count; index++)
        {
            YQSpatialAssetRecord spatial = catalog.SpatialAssets[index];
            if (spatial == null || string.IsNullOrWhiteSpace(spatial.assetPath))
                continue;
            _reviewedSpatialByPath[NormalizePath(spatial.assetPath)] = spatial;
        }
    }

    private static bool IsKnownSlot(string slot)
    {
        switch ((slot ?? string.Empty).Trim().ToLowerInvariant())
        {
            case YQWorldAssetCatalog.SlotTerrain:
            case YQWorldAssetCatalog.SlotFloor:
            case YQWorldAssetCatalog.SlotWall:
            case YQWorldAssetCatalog.SlotRoof:
            case YQWorldAssetCatalog.SlotDoor:
            case YQWorldAssetCatalog.SlotPath:
            case YQWorldAssetCatalog.SlotSettlementBuilding:
            case YQWorldAssetCatalog.SlotLargeStructure:
            case YQWorldAssetCatalog.SlotFloorDeco:
            case YQWorldAssetCatalog.SlotWallDeco:
            case YQWorldAssetCatalog.SlotVegetation:
            case YQWorldAssetCatalog.SlotRock:
            case YQWorldAssetCatalog.SlotLighting:
            case YQWorldAssetCatalog.SlotLootContainer:
            case YQWorldAssetCatalog.SlotEnemySite:
            case YQWorldAssetCatalog.SlotInteriorDeco:
            case YQWorldAssetCatalog.SlotExteriorDeco:
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeReviewTag(string value)
    {
        // note: Review tags use stable lowercase keys so serialized curation and runtime palette matching share one representation.
        return (value ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private static void AddReviewedEnumTag<T>(GeneratedAssetReferenceRecord reference, string prefix, T value) where T : struct
    {
        if (reference == null || string.IsNullOrWhiteSpace(prefix))
            return;
        string text = value.ToString();
        if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, "Unknown", StringComparison.OrdinalIgnoreCase) && !string.Equals(text, "None", StringComparison.OrdinalIgnoreCase) && !string.Equals(text, "Unspecified", StringComparison.OrdinalIgnoreCase))
            AddUnique(reference.subTags, prefix + "_" + NormalizeReviewTag(text));
    }

    private static void AddReviewedEnumTags<T>(GeneratedAssetReferenceRecord reference, string prefix, List<T> values) where T : struct
    {
        if (values == null)
            return;
        for (int index = 0; index < values.Count; index++)
            AddReviewedEnumTag(reference, prefix, values[index]);
    }

    private static Dictionary<string, YQSpatialAssetRecord> LoadPreservedSpatialReviews(string catalogPath)
    {
        Dictionary<string, YQSpatialAssetRecord> preserved =
            new Dictionary<string, YQSpatialAssetRecord>(StringComparer.OrdinalIgnoreCase);
        YQWorldAssetIntakeCatalog previous =
            AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(catalogPath);
        if (previous == null || previous.SpatialAssets == null)
            return preserved;

        for (int index = 0; index < previous.SpatialAssets.Count; index++)
        {
            YQSpatialAssetRecord record = previous.SpatialAssets[index];
            if (record == null || string.IsNullOrWhiteSpace(record.assetPath))
                continue;
            if (record.spatialMetadataAuthored || record.releaseEligible || record.disposition == YQAssetIntakeDisposition.Candidate)
                preserved[NormalizePath(record.assetPath)] = record;
        }
        return preserved;
    }

    private static Dictionary<string, bool> LoadPreservedKitEligibility(string catalogPath)
    {
        // note: Kit release approval is authored state and must survive rescans just like per-prefab spatial contracts.
        Dictionary<string, bool> preserved = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        YQWorldAssetIntakeCatalog previous = AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(catalogPath);
        if (previous == null || previous.Kits == null)
            return preserved;
        for (int index = 0; index < previous.Kits.Count; index++)
        {
            YQAssetKitManifest kit = previous.Kits[index];
            if (kit != null && !string.IsNullOrWhiteSpace(kit.kitId) && kit.releaseEligible)
                preserved[kit.kitId] = true;
        }
        return preserved;
    }

    private static void PreserveKitEligibility(YQAssetKitManifest refreshed, Dictionary<string, bool> preserved)
    {
        if (refreshed != null && preserved != null && !string.IsNullOrWhiteSpace(refreshed.kitId) &&
            preserved.TryGetValue(refreshed.kitId, out bool approved) && approved)
            refreshed.releaseEligible = true;
    }

    private static void PreserveReviewedSpatialContract(YQSpatialAssetRecord refreshed, Dictionary<string, YQSpatialAssetRecord> preserved)
    {
        if (refreshed == null || preserved == null || string.IsNullOrWhiteSpace(refreshed.assetPath) ||
            !preserved.TryGetValue(NormalizePath(refreshed.assetPath), out YQSpatialAssetRecord authored) || authored == null)
            return;

        CopyAuthoredSpatialReview(refreshed, authored);
        // note: Keep newly measured renderer/material/collider evidence on the refreshed record; only authored placement authority is restored.
    }

    private static void CopyAuthoredSpatialReview(YQSpatialAssetRecord refreshed, YQSpatialAssetRecord authored)
    {
        if (refreshed == null || authored == null)
            return;
        authored.EnsureCollections();
        refreshed.semanticRole = authored.semanticRole;
        refreshed.compositionScale = authored.compositionScale;
        refreshed.disposition = authored.disposition;
        refreshed.releaseEligible = authored.releaseEligible;
        refreshed.frontDirection = authored.frontDirection;
        refreshed.frontDirectionAuthored = authored.frontDirectionAuthored;
        refreshed.spatialMetadataAuthored = authored.spatialMetadataAuthored;
        refreshed.allowedSlopeDegrees = authored.allowedSlopeDegrees;
        refreshed.foundationProfile = authored.foundationProfile;
        refreshed.roadRelationship = authored.roadRelationship;
        refreshed.navigationProfile = authored.navigationProfile;
        refreshed.entranceSocketCandidates = new List<string>(authored.entranceSocketCandidates);
        refreshed.connectionSocketCandidates = new List<string>(authored.connectionSocketCandidates);
        refreshed.dressingSocketCandidates = new List<string>(authored.dressingSocketCandidates);
        refreshed.semanticTags = new List<string>(authored.semanticTags);
        refreshed.curationV2 = authored.curationV2;
    }

    private static YQWorldAssetIntakeCatalog SaveCatalog(
        string scanScope,
        List<YQAssetKitManifest> kits,
        List<YQSpatialAssetRecord> spatialAssets,
        List<YQMaterialAssetRecord> materials,
        string outputCatalogPath)
    {
        EnsureFolderPath(
            "Assets/Assets/Resources");

        YQWorldAssetIntakeCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(
                outputCatalogPath);

        if (catalog == null)
        {
            catalog =
                ScriptableObject.CreateInstance<YQWorldAssetIntakeCatalog>();

            AssetDatabase.CreateAsset(
                catalog,
                outputCatalogPath);
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

        // note: Some headless Unity imports serialize a newly created intake asset with a blank script field; restore the known class GUID before the next load.
        EnsureCatalogScriptReference(
            outputCatalogPath);
        return catalog;
    }

    private static void EnsureCatalogScriptReference(string catalogPath)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
            return;

        string fullPath =
            Path.GetFullPath(catalogPath);

        if (!File.Exists(fullPath))
            return;

        string yaml =
            File.ReadAllText(fullPath);

        const string blankScript =
            "m_Script: {fileID: 0}";

        if (!yaml.Contains(blankScript, StringComparison.Ordinal) ||
            !yaml.Contains(
                "m_EditorClassIdentifier: Assembly-CSharp::YQWorldAssetIntakeCatalog",
                StringComparison.Ordinal))
        {
            return;
        }

        string repaired =
            yaml.Replace(
                blankScript,
                "m_Script: {fileID: 11500000, guid: " + IntakeCatalogScriptGuid + ", type: 3}",
                StringComparison.Ordinal);

        File.WriteAllText(
            fullPath,
            repaired);

        AssetDatabase.ImportAsset(
            catalogPath,
            ImportAssetOptions.ForceUpdate);
    }

    private static void WriteGenerationInventory(
        List<string> roots,
        List<YQAssetKitManifest> kits,
        List<YQSpatialAssetRecord> spatialAssets,
        List<YQMaterialAssetRecord> materials,
        List<GenerationInventoryAsset> sourceAssets,
        IReadOnlyList<YQAssetLibraryEvidenceRecord> libraryEvidence)
    {
        // note: The pure projection is shared with documentary fixtures; asset publication remains in this existing editor boundary.
        GenerationInventoryDocument document = BuildGenerationInventoryDocument(roots, kits, spatialAssets,
            materials, sourceAssets, libraryEvidence, DateTime.UtcNow.ToString("O"));
        // note: Once coverage is explicitly published, later legacy scans retain its documentary rows and consumer history.
        if (File.Exists(GenerationInventoryPath))
        {
            GenerationInventoryDocument previous = Newtonsoft.Json.JsonConvert.DeserializeObject<GenerationInventoryDocument>(
                File.ReadAllText(GenerationInventoryPath));
            if (previous != null && previous.libraryCoverageVersion > 0)
                document = MergeGenerationInventoryCoverage(document, new LibraryCoverageInputs { master = previous });
        }
        EnsureFolderPath(Path.GetDirectoryName(GenerationInventoryPath));
        string fullPath = Path.GetFullPath(GenerationInventoryPath);
        File.WriteAllText(fullPath,
            Newtonsoft.Json.JsonConvert.SerializeObject(document, Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.ImportAsset(GenerationInventoryPath, ImportAssetOptions.ForceUpdate);
    }

    internal static GenerationInventoryDocument BuildGenerationInventoryDocument(
        List<string> roots,
        List<YQAssetKitManifest> kits,
        List<YQSpatialAssetRecord> spatialAssets,
        List<YQMaterialAssetRecord> materials,
        List<GenerationInventoryAsset> sourceAssets,
        IReadOnlyList<YQAssetLibraryEvidenceRecord> libraryEvidence,
        string generatedUtc)
    {
        GenerationInventoryDocument document =
            new GenerationInventoryDocument
            {
                generatedUtc = generatedUtc,
                scanScope = "approved_discovery_roots_typed_intake_snapshot",
                intakeCatalog = IntakeCatalogPath,
                approvedRoots = roots != null
                    ? new List<string>(roots)
                    : new List<string>()
            };

        Dictionary<string, string> kitRoots =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (kits != null)
        {
            for (int index = 0; index < kits.Count; index++)
            {
                YQAssetKitManifest kit = kits[index];
                if (kit != null && !string.IsNullOrWhiteSpace(kit.kitId))
                {
                    kitRoots[kit.kitId] = kit.sourceRoot ?? string.Empty;
                    document.kitEvidenceLinks.Add(new GenerationInventoryKitEvidenceLinks
                    {
                        kitId = kit.kitId,
                        libraryEvidenceIds = CopySortedEvidenceIds(kit.libraryEvidenceIds)
                    });
                }
            }
        }

        if (spatialAssets != null)
        {
            for (int index = 0; index < spatialAssets.Count; index++)
            {
                YQSpatialAssetRecord record = spatialAssets[index];
                if (record == null || string.IsNullOrWhiteSpace(record.assetPath))
                    continue;

                bool ready = YQRuntimeWorldAssetRegistryBuilder.IsGenerationReadySpatialAsset(record);

                document.assets.Add(
                    new GenerationInventoryAsset
                    {
                        stableAssetId = record.stableAssetId,
                        sourceGuid = record.sourceGuid,
                        sourceAssetKey = record.sourceAssetKey,
                        assetPath = NormalizePath(record.assetPath),
                        assetType = "prefab",
                        assetFamily = record.kitId,
                        sourceRoot = kitRoots.TryGetValue(record.kitId ?? string.Empty, out string root) ? root : string.Empty,
                        registryState = ready ? "generation_ready" : "catalogued_review_or_quarantine",
                        slotTag = record.semanticRole ?? string.Empty,
                        runtimeEligible = ready,
                        finalState = ready ? "generation_ready" : ResolveInventoryPrefabState(record),
                        finalStateReason = ready ? "Passed authored spatial and technical intake gates." : ResolveInventoryPrefabReason(record),
                        intakeDisposition = record.disposition.ToString(),
                        technicalIssues = record.validationIssues != null ? new List<string>(record.validationIssues) : new List<string>(),
                        classificationStatus = record.spatialMetadataAuthored && !string.IsNullOrWhiteSpace(record.semanticRole) ? "reviewed" : "inferred_pending_review",
                        placementContextStatus = record.spatialMetadataAuthored ? "reviewed" : "pending",
                        paletteAssignmentStatus = ready ? "assigned" : "pending",
                        technicalValidationStatus = ResolveInventoryPrefabTechnicalStatus(record),
                        libraryEvidenceIds = CopySortedEvidenceIds(record.libraryEvidenceIds)
                    });
            }
        }

        if (materials != null)
        {
            for (int index = 0; index < materials.Count; index++)
            {
                YQMaterialAssetRecord record = materials[index];
                if (record == null || string.IsNullOrWhiteSpace(record.assetPath))
                    continue;

                bool compatible = record.releaseEligible;
                document.assets.Add(
                    new GenerationInventoryAsset
                    {
                        stableAssetId = record.stableAssetId,
                        sourceGuid = record.sourceGuid,
                        assetPath = NormalizePath(record.assetPath),
                        assetType = "material",
                        assetFamily = record.kitId,
                        sourceRoot = kitRoots.TryGetValue(record.kitId ?? string.Empty, out string root) ? root : string.Empty,
                        registryState = compatible ? "compatible_material_dependency" : "material_review_or_quarantine",
                        slotTag = string.Empty,
                        runtimeEligible = false,
                        finalState = compatible ? "not_applicable_independent_generation_asset" : ResolveInventoryMaterialState(record),
                        finalStateReason = compatible ? "Validated material dependency; palette and placement context are inherited from its owning prefab." : ResolveInventoryMaterialReason(record),
                        intakeDisposition = record.compatibilityState.ToString(),
                        technicalIssues = record.validationIssues != null ? new List<string>(record.validationIssues) : new List<string>(),
                        classificationStatus = "material_dependency",
                        placementContextStatus = "not_applicable",
                        paletteAssignmentStatus = compatible ? "inherited_from_prefab" : "pending",
                        technicalValidationStatus = compatible ? "compatible_urp" : "review_or_repair",
                        libraryEvidenceIds = CopySortedEvidenceIds(record.libraryEvidenceIds)
                    });
            }
        }

        // note: A typed intake record owns the path when present; all other source files still receive an explicit dependency or pending-review entry.
        HashSet<string> accountedPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < document.assets.Count; index++)
            accountedPaths.Add(document.assets[index].assetPath);

        if (sourceAssets != null)
        {
            for (int index = 0; index < sourceAssets.Count; index++)
            {
                GenerationInventoryAsset source = sourceAssets[index];
                if (source != null && !string.IsNullOrWhiteSpace(source.assetPath) &&
                    accountedPaths.Add(source.assetPath))
                    document.assets.Add(Newtonsoft.Json.JsonConvert.DeserializeObject<GenerationInventoryAsset>(
                        Newtonsoft.Json.JsonConvert.SerializeObject(source)));
            }
        }

        // note: Match within the actual Art family, including the Hivemind sub-pack; a shared file stem across unrelated packs is not proof of representation.
        Dictionary<string, string> prefabByFamilyAndStem =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < document.assets.Count; index++)
        {
            GenerationInventoryAsset asset = document.assets[index];
            if (asset.assetType != "prefab")
                continue;
            string key = BuildPrefabVariantKey(asset);
            if (prefabByFamilyAndStem.ContainsKey(key))
                prefabByFamilyAndStem[key] = string.Empty;
            else
                prefabByFamilyAndStem.Add(key, asset.assetPath);
        }

        for (int index = 0; index < document.assets.Count; index++)
        {
            GenerationInventoryAsset asset = document.assets[index];
            if (asset.assetType != "source_model")
                continue;
            // note: Documentary coverage does not acquire representation authority from matching file names.
            if (asset.reviewPolicyScope != GenerationInventoryReviewPolicyScope.LegacyWorldKit)
                continue;
            string key = BuildPrefabVariantKey(asset);
            if (!prefabByFamilyAndStem.TryGetValue(key, out string authoredPrefab) ||
                string.IsNullOrEmpty(authoredPrefab))
                continue;

            asset.finalState = "duplicate_variant_represented_by_prefab";
            asset.finalStateReason = "Source model has an exact-name authored prefab in the same library; the prefab is the procedural registration unit.";
            asset.representedByAssetPath = authoredPrefab;
            asset.classificationStatus = "represented_by_prefab";
            asset.placementContextStatus = "inherited_from_prefab";
            asset.paletteAssignmentStatus = "inherited_from_prefab";
        }

        // note: Convert every unresolved workflow state into an auditable terminal exclusion so the canonical inventory never leaves a candidate in limbo.
        ApplyTerminalReviewPolicy(document);

        document.assets.Sort((left, right) =>
        {
            int pathOrder = string.Compare(left.assetPath, right.assetPath, StringComparison.OrdinalIgnoreCase);
            if (pathOrder != 0) return pathOrder;
            int guidOrder = string.Compare(left.sourceGuid, right.sourceGuid, StringComparison.Ordinal);
            return guidOrder != 0 ? guidOrder : string.Compare(left.stableAssetId, right.stableAssetId, StringComparison.Ordinal);
        });
        CopyDocumentaryExportEvidence(document, libraryEvidence);

        // note: Derive all summary counts from emitted records, so a root overlap or skipped subasset cannot silently inflate eligibility.
        for (int index = 0; index < document.assets.Count; index++)
        {
            GenerationInventoryAsset asset = document.assets[index];
            document.counts.total++;
            if (asset.assetType == "prefab") document.counts.prefabs++;
            else if (asset.assetType == "material") document.counts.materials++;
            else document.counts.sourceAssets++;

            if (asset.finalState == "generation_ready") document.counts.generationReady++;
            else if (asset.finalState.StartsWith("quarantined_", StringComparison.Ordinal)) document.counts.quarantined++;
            else if (asset.finalState.StartsWith("intentionally_excluded_", StringComparison.Ordinal)) document.counts.intentionallyExcluded++;
            else if (asset.finalState == "duplicate_variant_represented_by_prefab") document.counts.representedVariants++;
            else if (asset.finalState == "not_applicable_independent_generation_asset") document.counts.notApplicable++;
            else document.counts.pendingReview++;
        }

        // note: Current intake facts are a separate documentary binding, never replacements for the asset review tuple.
        List<Newtonsoft.Json.Linq.JObject> intakeRows = new List<Newtonsoft.Json.Linq.JObject>();
        foreach (YQSpatialAssetRecord record in spatialAssets ?? new List<YQSpatialAssetRecord>())
        {
            if (record == null || string.IsNullOrWhiteSpace(record.assetPath)) continue;
            intakeRows.Add(new Newtonsoft.Json.Linq.JObject
            {
                ["stableAssetId"] = record.stableAssetId, ["sourceGuid"] = record.sourceGuid,
                ["sourceAssetKey"] = record.sourceAssetKey, ["assetPath"] = record.assetPath,
                ["kitId"] = record.kitId, ["semanticRole"] = record.semanticRole,
                ["disposition"] = (int)record.disposition, ["releaseEligible"] = record.releaseEligible,
                ["spatialMetadataAuthored"] = record.spatialMetadataAuthored,
                ["curationV2"] = record.curationV2 == null ? null :
                    new Newtonsoft.Json.Linq.JObject { ["contractVersion"] = record.curationV2.contractVersion },
                ["libraryEvidenceIds"] = new Newtonsoft.Json.Linq.JArray(record.libraryEvidenceIds ?? new List<string>())
            });
        }
        List<string> orderedRows = new List<string>();
        foreach (Newtonsoft.Json.Linq.JObject row in intakeRows)
            orderedRows.Add(row.ToString(Newtonsoft.Json.Formatting.None));
        orderedRows.Sort(StringComparer.Ordinal);
        string projectionHash = LibraryHashText(string.Join("\n", orderedRows));
        LibraryCoverageInputs currentIntake = new LibraryCoverageInputs
        {
            currentIntakeCatalogSnapshotId = "typed-intake-projection:" + IntakeCatalogPath + "@sha256:" + projectionHash
        };
        currentIntake.snapshots.Add(new GenerationInventorySourceSnapshot { path = IntakeCatalogPath, sha256 = projectionHash,
            identityBasis = "typed_intake_curation_projection_v1_not_serialized_catalog_bytes" });
        currentIntake.bindings.AddRange(BuildLibraryCurrentIntakeBindings(intakeRows, currentIntake.currentIntakeCatalogSnapshotId));
        return MergeGenerationInventoryCoverage(document, currentIntake);
    }

    private static List<string> CopySortedEvidenceIds(List<string> ids)
    {
        List<string> copy = ids != null ? new List<string>(ids) : new List<string>();
        copy.Sort(StringComparer.Ordinal);
        return copy;
    }

    private static void CopyDocumentaryExportEvidence(GenerationInventoryDocument document,
        IReadOnlyList<YQAssetLibraryEvidenceRecord> libraryEvidence)
    {
        // note: Normalize and sort copies only; exported documentary states never alter the authored intake or imply admission.
        if (libraryEvidence != null)
            for (int i = 0; i < libraryEvidence.Count; i++)
            {
                YQAssetLibraryEvidenceRecord source = libraryEvidence[i];
                if (source == null)
                {
                    document.libraryEvidence.Add(null);
                    continue;
                }
                YQAssetLibraryEvidenceRecord copy = Newtonsoft.Json.JsonConvert.DeserializeObject<YQAssetLibraryEvidenceRecord>(
                    Newtonsoft.Json.JsonConvert.SerializeObject(source));
                copy.EnsureCollections();
                copy.provenanceEvidenceRefs.Sort(StringComparer.Ordinal);
                copy.licenseEvidenceRefs.Sort(StringComparer.Ordinal);
                copy.supersedesEvidenceIds.Sort(StringComparer.Ordinal);
                copy.supersedesVersionIds.Sort(StringComparer.Ordinal);
                copy.consumerEvidence.Sort((left, right) => StringComparer.Ordinal.Compare(
                    Newtonsoft.Json.JsonConvert.SerializeObject(left), Newtonsoft.Json.JsonConvert.SerializeObject(right)));
                document.libraryEvidence.Add(copy);
            }
        document.libraryEvidence.Sort((left, right) =>
        {
            int idOrder = string.Compare(left?.evidenceId, right?.evidenceId, StringComparison.Ordinal);
            return idOrder != 0 ? idOrder : StringComparer.Ordinal.Compare(
                Newtonsoft.Json.JsonConvert.SerializeObject(left), Newtonsoft.Json.JsonConvert.SerializeObject(right));
        });
        document.kitEvidenceLinks.Sort((left, right) => StringComparer.Ordinal.Compare(left.kitId, right.kitId));
        for (int i = 0; i < document.assets.Count; i++)
            document.assets[i].libraryEvidenceIds = CopySortedEvidenceIds(document.assets[i].libraryEvidenceIds);
    }

    private const string DotIndexPath = "Assets/Assets/GeneratedAssets/DOT Generated Assets/Catalogs/DOT_ASSET_INDEX.json";
    private const string DotLayoutPath = "Assets/Assets/GeneratedAssets/DOT Generated Assets/Catalogs/DOT_ASSET_LAYOUT.json";
    private const string EquipmentCatalogPath = "Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset";
    private const string CreatureCatalogPath = "Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset";
    private const string ArtTrackerPath = "Docs/ArtDirection_Production_Tracker.md";

    private sealed class LibraryTextReader
    {
        internal readonly string root;
        internal readonly Dictionary<string, string> texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal readonly List<GenerationInventorySourceSnapshot> snapshots = new List<GenerationInventorySourceSnapshot>();
        private readonly Dictionary<string, (long bytes, long ticks)> stamps = new Dictionary<string, (long, long)>(StringComparer.OrdinalIgnoreCase);

        internal LibraryTextReader(string projectRoot) { root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar); }

        internal string Full(string relative)
        {
            string full = Path.GetFullPath(Path.Combine(root, relative));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Library input escapes the project: " + relative);
            return full;
        }

        internal string Read(string relative)
        {
            relative = NormalizePath(relative);
            if (texts.TryGetValue(relative, out string cached)) return cached;
            string full = Full(relative);
            FileInfo before = new FileInfo(full);
            long length = before.Length, ticks = before.LastWriteTimeUtc.Ticks;
            if (length > 128L * 1024L * 1024L) throw new InvalidDataException("Text input exceeds bounded reader limit: " + relative);
            byte[] bytes = File.ReadAllBytes(full);
            FileInfo after = new FileInfo(full);
            if (after.Length != length || after.LastWriteTimeUtc.Ticks != ticks || bytes.LongLength != length)
                throw new InvalidDataException("Library input changed while reading: " + relative);
            string text = System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
            texts.Add(relative, text);
            stamps.Add(relative, (length, ticks));
            snapshots.Add(new GenerationInventorySourceSnapshot { path = relative, bytes = length,
                sha256 = LibraryHash(bytes), identityBasis = "actual_text_bytes_not_binary_payload_verification" });
            return text;
        }

        internal string Guid(string relative)
        {
            string meta = Full(relative + ".meta");
            if (!File.Exists(meta)) return null;
            FileInfo before = new FileInfo(meta);
            long bytes = before.Length, ticks = before.LastWriteTimeUtc.Ticks;
            // note: ModelImporter metadata can exceed a megabyte; Unity's GUID lives in its header, independent of importer payload size.
            byte[] prefix = new byte[4096];
            int read;
            using (FileStream stream = File.OpenRead(meta)) read = stream.Read(prefix, 0, prefix.Length);
            string text = System.Text.Encoding.UTF8.GetString(prefix, 0, read).TrimStart('\uFEFF');
            FileInfo after = new FileInfo(meta);
            if (after.Length != bytes || after.LastWriteTimeUtc.Ticks != ticks)
                throw new InvalidDataException("Source metadata changed: " + relative);
            stamps[NormalizePath(relative + ".meta")] = (bytes, ticks);
            Match match = Regex.Match(text, @"(?m)^guid: ([a-fA-F0-9]{32})\r?$");
            if (!match.Success) throw new InvalidDataException("Metadata GUID is absent from the bounded header: " + relative);
            return match.Groups[1].Value.ToLowerInvariant();
        }

        internal string Hash(string relative)
        {
            Read(relative);
            return snapshots.Find(snapshot => string.Equals(snapshot.path, NormalizePath(relative), StringComparison.OrdinalIgnoreCase)).sha256;
        }

        internal void VerifyStable()
        {
            foreach (KeyValuePair<string, (long bytes, long ticks)> pair in stamps)
            {
                FileInfo current = new FileInfo(Full(pair.Key));
                if (current.Length != pair.Value.bytes || current.LastWriteTimeUtc.Ticks != pair.Value.ticks)
                    throw new InvalidDataException("Library input changed before projection: " + pair.Key);
            }
        }
    }

    internal static string LibraryHash(byte[] bytes)
    {
        using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string LibraryHashText(string text) => LibraryHash(System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty));
    private static string LibrarySourceId(string guid, string path) => !string.IsNullOrWhiteSpace(guid)
        ? "guid:" + guid.ToLowerInvariant() : "path:" + LibraryHashText(NormalizePath(path));
    private static string LibraryVersionId(string sourceId, string sha) => string.IsNullOrWhiteSpace(sha) ? null : sourceId + "@sha256:" + sha.ToLowerInvariant();
    private static T LibraryCopy<T>(T value) => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Newtonsoft.Json.JsonConvert.SerializeObject(value));

    internal static GenerationInventoryDocument BuildLibraryCoverageCandidate(string projectRoot,
        IEnumerable<string> observationRoots = null, int maximumFilesPerRoot = 100000)
    {
        LibraryCoverageInputs inputs = ReadLibraryCoverageInputs(projectRoot, observationRoots, maximumFilesPerRoot);
        return MergeGenerationInventoryCoverage(inputs.master, inputs);
    }

    internal static LibraryCoverageInputs ReadLibraryCoverageInputs(string projectRoot,
        IEnumerable<string> observationRoots = null, int maximumFilesPerRoot = 100000)
    {
        // note: Read current canonical text and metadata only; no AssetDatabase, prefab loading, binary hashing, or report authority.
        if (maximumFilesPerRoot < 1) throw new ArgumentOutOfRangeException(nameof(maximumFilesPerRoot));
        LibraryTextReader reader = new LibraryTextReader(projectRoot);
        LibraryCoverageInputs inputs = new LibraryCoverageInputs();
        inputs.master = Newtonsoft.Json.JsonConvert.DeserializeObject<GenerationInventoryDocument>(reader.Read(GenerationInventoryPath));
        Newtonsoft.Json.Linq.JObject index = Newtonsoft.Json.Linq.JObject.Parse(reader.Read(DotIndexPath));
        Newtonsoft.Json.Linq.JObject layout = Newtonsoft.Json.Linq.JObject.Parse(reader.Read(DotLayoutPath));
        if ((string)index["schema"] != "yourquest.dot-asset-index.v1" || (string)layout["schema"] != "yourquest.dot-asset-layout.v1")
            throw new InvalidDataException("Unsupported DOT source schema.");
        foreach (Newtonsoft.Json.Linq.JObject row in (Newtonsoft.Json.Linq.JArray)index["files"])
        {
            GenerationInventoryAsset source = LibrarySource((string)row["guid"], (string)row["path"],
                "Assets/Assets/GeneratedAssets/DOT Generated Assets", (string)row["category"], null);
            source.installedSourceSha256 = (string)row["sha256"];
            source.sourceVersionId = LibraryVersionId(source.librarySourceId, source.installedSourceSha256);
            source.sourceAvailability = "declared";
            source.sourceDeclarationRefs.Add(DotIndexPath + "#" + source.assetPath);
            inputs.sources.Add(source);
        }
        foreach (Newtonsoft.Json.Linq.JObject row in (Newtonsoft.Json.Linq.JArray)layout["files"])
        {
            GenerationInventoryAsset source = LibrarySource((string)row["guid"], (string)row["path"],
                "Assets/Assets/GeneratedAssets/DOT Generated Assets", (string)row["pack"], (string)row["kind"]);
            source.declaredSourceSha256 = (string)row["sha256"];
            source.installedSourceSha256 = (string)row["installedSha256"] ?? (string)row["sha256"];
            source.baselineSourceVersionId = LibraryVersionId(source.librarySourceId, source.declaredSourceSha256);
            source.sourceVersionId = LibraryVersionId(source.librarySourceId, source.installedSourceSha256);
            source.sourceAvailability = "declared";
            source.sourceDeclarationRefs.Add(DotLayoutPath + "#" + source.assetPath);
            if (row["origin"] != null) source.sourceAliases.Add((string)row["origin"]);
            foreach (Newtonsoft.Json.Linq.JToken alias in (Newtonsoft.Json.Linq.JArray)row["aliases"] ?? new Newtonsoft.Json.Linq.JArray())
                source.sourceAliases.Add((string)alias);
            inputs.sources.Add(source);
        }
        ObserveLibraryRoots(reader, inputs, observationRoots ?? DefaultLibraryObservationRoots, maximumFilesPerRoot);
        BuildLibraryDomainBindings(reader, inputs, layout);
        ReadLibraryProvenance(reader, inputs, layout);
        ReadLibraryTrackerFacts(reader, inputs);
        reader.VerifyStable();
        inputs.snapshots.AddRange(reader.snapshots);
        return inputs;
    }

    private static GenerationInventoryAsset LibrarySource(string guid, string path, string root, string family, string kind)
    {
        string extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
        string id = LibrarySourceId(guid, path);
        return new GenerationInventoryAsset
        {
            stableAssetId = !string.IsNullOrWhiteSpace(guid) ? "source_" + guid : "source_" + LibraryHashText(NormalizePath(path)),
            librarySourceId = id, sourceGuid = guid, assetPath = NormalizePath(path), sourceRoot = root, assetFamily = family,
            assetType = extension == ".prefab" ? "prefab" : extension == ".mat" ? "material" :
                extension == ".fbx" || extension == ".obj" || extension == ".blend" || extension == ".gltf" || extension == ".glb" ? "source_model" :
                extension == ".unity" ? "authored_scene" : "source_dependency",
            declaredSourceKind = kind, sourceAvailability = "observed", registryState = "library_source_accounting_only",
            runtimeEligible = false, finalState = "pending_library_source_review",
            finalStateReason = "Documentary source accounting; world placement and independent consumer eligibility are unassessed.",
            intakeDisposition = "not_in_world_spatial_intake", classificationStatus = "unknown", placementContextStatus = "unknown",
            paletteAssignmentStatus = "unknown", technicalValidationStatus = "not_assessed_independently",
            reviewDisposition = "unknown", reviewPolicyVersion = "library-coverage-v1",
            reviewPolicyScope = GenerationInventoryReviewPolicyScope.LibraryCoverage, technicalIssues = new List<string>()
        };
    }

    private static void ObserveLibraryRoots(LibraryTextReader reader, LibraryCoverageInputs inputs,
        IEnumerable<string> roots, int maximumFiles)
    {
        List<string> configured = new List<string>();
        foreach (string root in roots)
            if (!configured.Contains(NormalizePath(root))) configured.Add(NormalizePath(root));
        configured.Sort(StringComparer.Ordinal);
        inputs.roots.AddRange(configured);
        foreach (string root in configured)
        {
            GenerationInventoryRootObservation observation = new GenerationInventoryRootObservation { root = root, availability = "missing" };
            inputs.observations.Add(observation);
            string fullRoot = reader.Full(root);
            if (!Directory.Exists(fullRoot)) continue;
            observation.availability = "present";
            observation.complete = true;
            Stack<string> directories = new Stack<string>();
            directories.Push(fullRoot);
            List<string> metadata = new List<string>();
            while (directories.Count > 0 && observation.complete)
            {
                string directory = directories.Pop();
                string[] entries = Directory.GetFileSystemEntries(directory);
                Array.Sort(entries, StringComparer.Ordinal);
                foreach (string full in entries)
                {
                    FileAttributes attributes = File.GetAttributes(full);
                    string path = full.Substring(reader.root.Length + 1).Replace('\\', '/');
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        observation.complete = false;
                        inputs.conflicts.Add(new GenerationInventoryCoverageConflict { kind = "skipped_link", subject = path, reason = "Explicit roots do not authorize traversal outside the project." });
                        continue;
                    }
                    if ((attributes & FileAttributes.Directory) != 0) { directories.Push(full); continue; }
                    if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    if (observation.files == maximumFiles)
                    {
                        observation.complete = false;
                        inputs.conflicts.Add(new GenerationInventoryCoverageConflict { kind = "root_cap", subject = root, reason = "Metadata enumeration cap reached." });
                        break;
                    }
                    string guid = reader.Guid(path);
                    FileInfo info = new FileInfo(full);
                    observation.files++;
                    metadata.Add(path + "|" + guid + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks);
                    inputs.sources.Add(LibrarySource(guid, path, root, root, null));
                    inputs.sources[inputs.sources.Count - 1].sourceDeclarationRefs.Add(root + "#metadata-observation");
                }
            }
            metadata.Sort(StringComparer.Ordinal);
            observation.metadataSha256 = LibraryHashText(string.Join("\n", metadata));
            inputs.snapshots.Add(new GenerationInventorySourceSnapshot { path = root, sha256 = observation.metadataSha256,
                identityBasis = "path_guid_size_mtime_metadata_only_no_binary_payload_hash", bytes = 0 });
        }
    }

    private static List<Newtonsoft.Json.Linq.JObject> ReadLibraryYamlRows(string text, string section, string recordKey)
    {
        // note: This bounded parser reads the inspected Unity catalog shape, including folded quoted scalars and source arrays.
        List<Newtonsoft.Json.Linq.JObject> rows = new List<Newtonsoft.Json.Linq.JObject>();
        string[] lines = text.Replace("\r", string.Empty).Split('\n');
        bool inSection = false;
        Newtonsoft.Json.Linq.JObject row = null;
        string key = null, scalar = null;
        Action flush = () =>
        {
            if (row != null && key != null && scalar != null) row[key] = LibraryYamlScalar(scalar);
            scalar = null;
        };
        foreach (string line in lines)
        {
            if (!inSection) { if (line == "  " + section + ":") inSection = true; continue; }
            Match start = Regex.Match(line, "^  - " + Regex.Escape(recordKey) + @":\s*(.*)$");
            if (start.Success)
            {
                flush();
                row = new Newtonsoft.Json.Linq.JObject(); rows.Add(row); key = recordKey; scalar = start.Groups[1].Value;
                continue;
            }
            if (Regex.IsMatch(line, @"^  [A-Za-z_][A-Za-z0-9_]*:")) { flush(); break; }
            if (row == null) continue;
            Match field = Regex.Match(line, @"^    ([A-Za-z_][A-Za-z0-9_]*):\s*(.*)$");
            if (field.Success)
            {
                flush(); key = field.Groups[1].Value; scalar = field.Groups[2].Value;
                if (scalar.Length == 0 && (key == "sourcePaths" || key == "sourceHashes" || key == "libraryEvidenceIds")) { row[key] = new Newtonsoft.Json.Linq.JArray(); scalar = null; }
                continue;
            }
            if (line.StartsWith("    - ", StringComparison.Ordinal) && row[key] is Newtonsoft.Json.Linq.JArray array)
            { array.Add(LibraryYamlScalar(line.Substring(6))); continue; }
            if (line.StartsWith("      ", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(line))
            {
                if (scalar != null) scalar += " " + line.Trim();
                else if (row[key] is Newtonsoft.Json.Linq.JArray current && current.Count > 0)
                    current[current.Count - 1] = (string)current[current.Count - 1] + " " + line.Trim();
            }
        }
        flush();
        return rows;
    }

    private static string LibraryYamlScalar(string value)
    {
        value = value.Trim();
        if (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))
            return value.Substring(1, value.Length - 2).Replace("''", "'");
        if (value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))
            return Newtonsoft.Json.JsonConvert.DeserializeObject<string>(value);
        return value;
    }

    internal static List<GenerationInventoryDomainBinding> BuildLibraryCurrentIntakeBindings(
        IEnumerable<Newtonsoft.Json.Linq.JObject> rows, string catalogSnapshotId,
        IReadOnlyDictionary<string, string> registryGuids = null)
    {
        // note: Project literal nullable facts; missing fields and technical curation never acquire art approval.
        List<GenerationInventoryDomainBinding> bindings = new List<GenerationInventoryDomainBinding>();
        foreach (Newtonsoft.Json.Linq.JObject row in rows)
        {
            string path = NormalizePath((string)row["assetPath"]), guid = (string)row["sourceGuid"];
            GenerationInventoryCurrentIntakeContract contract = new GenerationInventoryCurrentIntakeContract
            {
                contractVersion = 1, catalogSnapshotId = catalogSnapshotId, sourceGuid = guid,
                sourceAssetKey = (string)row["sourceAssetKey"], semanticRole = (string)row["semanticRole"],
                dispositionValue = LibraryIntakeInt(row["disposition"]),
                releaseEligible = LibraryIntakeBool(row["releaseEligible"]),
                spatialMetadataAuthored = LibraryIntakeBool(row["spatialMetadataAuthored"]),
                technicalPredicateVersion = "IsGenerationReadySpatialAsset:v1",
                libraryEvidenceIds = LibrarySortedStrings(row["libraryEvidenceIds"] is Newtonsoft.Json.Linq.JArray ids
                    ? ids.ToObject<List<string>>() : null)
            };
            Newtonsoft.Json.Linq.JToken curation = row["curationV2"];
            if (curation is Newtonsoft.Json.Linq.JObject structured)
                contract.curationContractVersion = LibraryIntakeInt(structured["contractVersion"]);
            else if (curation?.Type == Newtonsoft.Json.Linq.JTokenType.String)
            {
                Match version = Regex.Match((string)curation, @"(?:^|\s)contractVersion:\s*(-?\d+)(?:\s|$)");
                if (version.Success) contract.curationContractVersion = int.Parse(version.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            if (contract.dispositionValue.HasValue)
                contract.dispositionName = ((YQAssetIntakeDisposition)contract.dispositionValue.Value).ToString();
            // note: Hash this named projection only; the exact catalog byte identity is carried separately.
            Newtonsoft.Json.Linq.JObject projection = new Newtonsoft.Json.Linq.JObject
            {
                ["hashContract"] = "intake_curation_projection_v1", ["stableAssetId"] = row["stableAssetId"],
                ["sourceGuid"] = guid, ["sourceAssetKey"] = contract.sourceAssetKey, ["assetPath"] = path,
                ["kitId"] = row["kitId"], ["semanticRole"] = contract.semanticRole,
                ["disposition"] = contract.dispositionValue, ["releaseEligible"] = contract.releaseEligible,
                ["spatialMetadataAuthored"] = contract.spatialMetadataAuthored,
                ["curationContractVersion"] = contract.curationContractVersion,
                ["libraryEvidenceIds"] = row["libraryEvidenceIds"] == null ? null :
                    Newtonsoft.Json.Linq.JArray.FromObject(contract.libraryEvidenceIds)
            };
            contract.intakeRecordSha256 = LibraryHashText(projection.ToString(Newtonsoft.Json.Formatting.None));
            bool? eligible = null;
            if (!string.IsNullOrWhiteSpace(path) &&
                (!YQRuntimeWorldAssetRegistryBuilder.IsUsefulPrefabPath(path) || YQWorldAssetCatalog.IsRuntimeQuarantinedPath(path) ||
                 contract.releaseEligible == false || contract.spatialMetadataAuthored == false ||
                 (contract.dispositionValue.HasValue && contract.dispositionValue != (int)YQAssetIntakeDisposition.Candidate) ||
                 (contract.curationContractVersion.HasValue && contract.curationContractVersion != YQAssetCurationContractV2.SupportedContractVersion)))
                eligible = false;
            else if (!string.IsNullOrWhiteSpace(path) && contract.releaseEligible.HasValue && contract.spatialMetadataAuthored.HasValue &&
                     contract.dispositionValue.HasValue && contract.curationContractVersion.HasValue)
                eligible = YQRuntimeWorldAssetRegistryBuilder.IsGenerationReadySpatialAsset(new YQSpatialAssetRecord
                {
                    assetPath = path, releaseEligible = contract.releaseEligible.Value,
                    spatialMetadataAuthored = contract.spatialMetadataAuthored.Value,
                    disposition = (YQAssetIntakeDisposition)contract.dispositionValue.Value,
                    curationV2 = new YQAssetCurationContractV2 { contractVersion = contract.curationContractVersion.Value }
                });
            string registryGuid = null;
            registryGuids?.TryGetValue(path ?? string.Empty, out registryGuid);
            GenerationInventoryDomainBinding binding = new GenerationInventoryDomainBinding
            {
                bindingId = "world-intake:" + LibrarySourceId(guid, path), domain = "world_intake",
                assetId = (string)row["stableAssetId"], bindingKey = path, family = (string)row["kitId"],
                category = contract.semanticRole, kind = "spatial_asset", registryGuid = registryGuid,
                domainEligible = eligible, currentIntakeContract = contract,
                catalogEvidenceRef = IntakeCatalogPath + "#spatialAssets/" + (guid ?? string.Empty),
                eligibilityReason = "Current canonical intake technical predicate only; independent art approval and ordinary runtime use are not inferred."
            };
            AddLibraryRelation(binding, path, null, "current_intake_source");
            bindings.Add(binding);
        }
        return bindings;
    }

    private static int? LibraryIntakeInt(Newtonsoft.Json.Linq.JToken token)
    {
        if (token == null || token.Type == Newtonsoft.Json.Linq.JTokenType.Null) return null;
        return int.TryParse((string)token, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int value) ? value : (int?)null;
    }

    private static bool? LibraryIntakeBool(Newtonsoft.Json.Linq.JToken token)
    {
        if (token == null || token.Type == Newtonsoft.Json.Linq.JTokenType.Null) return null;
        if (token.Type == Newtonsoft.Json.Linq.JTokenType.Boolean) return (bool)token;
        string value = (string)token;
        return value == "1" || value == "true" ? true : value == "0" || value == "false" ? false : (bool?)null;
    }

    private static void BuildLibraryDomainBindings(LibraryTextReader reader, LibraryCoverageInputs inputs, Newtonsoft.Json.Linq.JObject layout)
    {
        // note: Join wrappers by metadata GUID, retaining ambiguity rather than selecting a same-name file.
        Dictionary<string, HashSet<string>> pathsByGuid = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Newtonsoft.Json.Linq.JObject> layoutByPath = new Dictionary<string, Newtonsoft.Json.Linq.JObject>(StringComparer.OrdinalIgnoreCase);
        foreach (GenerationInventoryAsset source in inputs.sources)
        {
            if (string.IsNullOrWhiteSpace(source.sourceGuid)) continue;
            if (!pathsByGuid.TryGetValue(source.sourceGuid, out HashSet<string> paths))
                pathsByGuid.Add(source.sourceGuid, paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            paths.Add(source.assetPath);
        }
        foreach (Newtonsoft.Json.Linq.JObject row in (Newtonsoft.Json.Linq.JArray)layout["files"])
            layoutByPath[(string)row["path"]] = row;
        Dictionary<string, string> registryGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string shards = reader.Full("Assets/Assets/Resources/YQWorldAssetShards");
        string[] shardFiles = Directory.GetFiles(shards, "YQWorldAssets_*.asset");
        Array.Sort(shardFiles, StringComparer.Ordinal);
        List<string> registryFiles = new List<string>(shardFiles);
        registryFiles.Insert(0, reader.Full("Assets/Assets/Resources/YQRuntimeWorldAssetRegistry.asset"));
        foreach (string full in registryFiles)
        {
            string relative = full.Substring(reader.root.Length + 1).Replace('\\', '/');
            foreach (Newtonsoft.Json.Linq.JObject row in ReadLibraryYamlRows(reader.Read(relative), "entries", "assetPath"))
            {
                Match guid = Regex.Match((string)row["prefab"] ?? string.Empty, @"\bguid: ([a-fA-F0-9]{32})\b");
                if (!guid.Success) continue;
                string key = (string)row["assetPath"];
                if (registryGuids.TryGetValue(key, out string previous) && previous != guid.Groups[1].Value)
                    inputs.conflicts.Add(new GenerationInventoryCoverageConflict { kind = "registry_key_conflict", subject = key, reason = "Multiple prefab GUIDs own the preserved key." });
                else registryGuids[key] = guid.Groups[1].Value.ToLowerInvariant();
            }
        }
        string pools = reader.Read("Assets/Assets/Resources/GeneratedRpgContentLibrary.asset");
        string poolsHash = reader.Hash("Assets/Assets/Resources/GeneratedRpgContentLibrary.asset");
        string creatureConsumerHash = reader.Hash("Assets/Assets/Scripts/Generated/YQDotCreatureCatalog.cs");
        foreach (string catalog in new[] { EquipmentCatalogPath, CreatureCatalogPath })
        {
            bool equipment = catalog == EquipmentCatalogPath;
            string catalogHash = reader.Hash(catalog);
            foreach (Newtonsoft.Json.Linq.JObject row in ReadLibraryYamlRows(reader.Read(catalog), "entries", "assetId"))
            {
                GenerationInventoryDomainBinding binding = new GenerationInventoryDomainBinding
                {
                    domain = equipment ? "equipment" : "creature", assetId = (string)row["assetId"], bindingKey = (string)row["prefabPath"],
                    family = (string)row["family"], category = (string)row["category"], kind = (string)row["kind"], species = (string)row["species"],
                    moduleSlot = (string)row["moduleSlot"], compatibilityId = (string)row["compatibilityId"],
                    domainEligible = equipment ? (string)row["generationEligible"] == "1" ? true : (string)row["generationEligible"] == "0" ? false : (bool?)null : null,
                    eligibilityReason = equipment ? (string)row["eligibilityReason"] : "No universal eligibility flag is declared by this catalog.",
                    catalogEvidenceRef = catalog
                };
                binding.bindingId = "dot-" + binding.domain + ":" + binding.assetId;
                if (string.IsNullOrWhiteSpace(binding.assetId)) throw new InvalidDataException("Domain entry has no stable assetId: " + catalog);
                registryGuids.TryGetValue(binding.bindingKey ?? string.Empty, out binding.registryGuid);
                string physical = null;
                if (!string.IsNullOrWhiteSpace(binding.registryGuid) && pathsByGuid.TryGetValue(binding.registryGuid, out HashSet<string> physicalPaths))
                {
                    if (physicalPaths.Count == 1) foreach (string path in physicalPaths) physical = path;
                    else inputs.conflicts.Add(new GenerationInventoryCoverageConflict { kind = "binding_guid_ambiguous", subject = binding.bindingId,
                        reason = "The registry GUID is declared at multiple source paths; no physical wrapper was selected." });
                }
                binding.resolvedAssetPath = physical;
                if (equipment)
                {
                    AddLibraryRelation(binding, (string)row["sourcePath"], (string)row["sourceSha256"], "declared_primary_source");
                    AddLibraryRelation(binding, (string)row["partsSourcePath"], null, "declared_parts_source");
                    AddEquipmentLodRelations(binding, row, layoutByPath);
                }
                else
                {
                    Newtonsoft.Json.Linq.JArray paths = row["sourcePaths"] as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray();
                    Newtonsoft.Json.Linq.JArray hashes = row["sourceHashes"] as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray();
                    if (paths.Count != hashes.Count) throw new InvalidDataException("Creature source/hash arity mismatch: " + binding.assetId);
                    for (int i = 0; i < paths.Count; i++) AddLibraryRelation(binding, (string)paths[i], (string)hashes[i], i == 0 ? "declared_primary_source" : "declared_additional_source");
                }
                string pool = equipment ? LibraryEquipmentPool(binding) : null;
                bool present = pool != null && LibraryPoolContains(pools, pool, binding.bindingKey);
                binding.consumerEvidence.Add(new YQAssetLibraryConsumerEvidenceRecord
                {
                    contractVersion = 1, consumerId = equipment ? "production_item_visual_pool" : "declared_creature_selector",
                    consumerSourcePath = equipment ? "Assets/Assets/Resources/GeneratedRpgContentLibrary.asset" : "Assets/Assets/Scripts/Generated/YQDotCreatureCatalog.cs",
                    bindingKey = binding.bindingKey, registryGuid = binding.registryGuid, resolvedAssetPath = physical,
                    paletteOrPoolId = pool, semanticRole = equipment ? binding.family : binding.kind,
                    evidenceLevel = equipment && !present ? YQAssetLibraryConsumerEvidenceLevel.Unknown : YQAssetLibraryConsumerEvidenceLevel.StaticReference,
                    evidenceRef = catalog + "#" + binding.assetId,
                    reason = equipment ? "Pool reference present=" + present + "; domain eligibility is copied separately; runtime use unverified." :
                        "Declared kind/species/module interface only; runtime use and general generation eligibility unverified."
                });
                binding.consumerEvidence[0].consumerSourceSha256 = equipment ? poolsHash : creatureConsumerHash;
                binding.consumerEvidence[0].evidenceSha256 = catalogHash;
                inputs.bindings.Add(binding);
            }
        }
        // note: Existing material adapters remain explicit original-to-runtime relations; observation does not regenerate materials.
        foreach (Newtonsoft.Json.Linq.JObject row in ReadLibraryYamlRows(reader.Read(IntakeCatalogPath), "materials", "stableAssetId"))
        {
            string adapter = (string)row["runtimeMaterialPath"], original = (string)row["assetPath"];
            if (string.IsNullOrWhiteSpace(adapter) || string.Equals(adapter, original, StringComparison.OrdinalIgnoreCase)) continue;
            GenerationInventoryDomainBinding binding = new GenerationInventoryDomainBinding
            {
                domain = "material_adapter", assetId = (string)row["stableAssetId"], bindingId = "material-adapter:" + (string)row["stableAssetId"],
                bindingKey = adapter, resolvedAssetPath = adapter, registryGuid = reader.Guid(adapter), catalogEvidenceRef = IntakeCatalogPath,
                domainEligible = (string)row["releaseEligible"] == "1" ? true : (string)row["releaseEligible"] == "0" ? false : (bool?)null,
                eligibilityReason = "Existing material compatibility flag and adapter strategy: " + (string)row["compatibilityStrategy"]
            };
            AddLibraryRelation(binding, original, null, "declared_material_source");
            AddLibraryRelation(binding, adapter, null, "declared_runtime_material_adapter");
            binding.consumerEvidence.Add(new YQAssetLibraryConsumerEvidenceRecord { contractVersion = 1,
                consumerId = "existing_material_adapter", consumerSourcePath = IntakeCatalogPath,
                consumerSourceSha256 = reader.Hash(IntakeCatalogPath), bindingKey = adapter, registryGuid = binding.registryGuid,
                resolvedAssetPath = adapter, evidenceRef = IntakeCatalogPath + "#materials/" + binding.assetId,
                evidenceSha256 = reader.Hash(IntakeCatalogPath), evidenceLevel = YQAssetLibraryConsumerEvidenceLevel.StaticReference,
                reason = "Literal intake adapter reference only; owning-prefab use and visual acceptance unverified." });
            inputs.bindings.Add(binding);
        }
        // note: World intake and serialized transport remain separate observations of their existing owners.
        string intakeHash = reader.Hash(IntakeCatalogPath);
        inputs.currentIntakeCatalogSnapshotId = "serialized-intake:" + IntakeCatalogPath + "@sha256:" + intakeHash;
        List<GenerationInventoryDomainBinding> current = BuildLibraryCurrentIntakeBindings(
            ReadLibraryYamlRows(reader.Read(IntakeCatalogPath), "spatialAssets", "stableAssetId"),
            inputs.currentIntakeCatalogSnapshotId, registryGuids);
        string paletteSource = "Assets/Assets/Scripts/Generated/YQWorldAssetCatalog.cs";
        string paletteHash = reader.Hash(paletteSource);
        foreach (GenerationInventoryDomainBinding binding in current)
            binding.consumerEvidence.Add(new YQAssetLibraryConsumerEvidenceRecord
            {
                contractVersion = 1, consumerId = "canonical_world_intake_gate", consumerSourcePath = paletteSource,
                consumerSourceSha256 = paletteHash, bindingKey = binding.bindingKey, registryGuid = binding.registryGuid,
                semanticRole = binding.category, evidenceLevel = YQAssetLibraryConsumerEvidenceLevel.StaticReference,
                evidenceRef = binding.catalogEvidenceRef, evidenceSha256 = intakeHash,
                reason = "The existing palette gate reads canonical intake flags. This is not a palette selection, art approval or ordinary runtime-use witness."
            });
        ReadLibraryIntakeCurationEvidence(reader, inputs, current);
        inputs.bindings.AddRange(current);
    }

    private static void ReadLibraryIntakeCurationEvidence(LibraryTextReader reader, LibraryCoverageInputs inputs,
        List<GenerationInventoryDomainBinding> current)
    {
        const string directory = "outputs/Asset_Contracts_20261002/Curation_20261003_003444/";
        const string reviewPath = directory + "Review.txt", beforePath = directory + "Intake_Before.asset";
        const string authorPath = "Assets/Assets/Scripts/Generated/Editor/YQFocusedConstructionLibraryCuration.cs";
        if (!File.Exists(reader.Full(reviewPath)) || !File.Exists(reader.Full(beforePath))) return;
        // note: Receipt facts link only exact named members with the same backed-up intake GUID; CURATED is technical evidence.
        var before = ReadLibraryYamlRows(reader.Read(beforePath), "spatialAssets", "stableAssetId");
        string reviewHash = reader.Hash(reviewPath);
        reader.Read(authorPath);
        string[] lines = reader.Read(reviewPath).Replace("\r", string.Empty).Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string[] fields = lines[index].Split('|');
            if (fields.Length < 3 || fields[0] != "CURATED") continue;
            string path = NormalizePath(fields[2]);
            var prior = before.Find(row => string.Equals((string)row["assetPath"], path, StringComparison.Ordinal));
            if (prior == null || string.IsNullOrWhiteSpace((string)prior["sourceGuid"])) continue;
            foreach (GenerationInventoryDomainBinding binding in current)
            {
                if (!string.Equals(binding.bindingKey, path, StringComparison.Ordinal) ||
                    !string.Equals(binding.currentIntakeContract.sourceGuid, (string)prior["sourceGuid"], StringComparison.OrdinalIgnoreCase))
                    continue;
                string id = "intake-curation:" + LibraryHashText(reviewHash + "|" + path + "|" + (string)prior["sourceGuid"] + "|" + index);
                binding.currentIntakeContract.curationEvidenceIds.Add(id);
                inputs.evidence.Add(new YQAssetLibraryEvidenceRecord
                {
                    contractVersion = 1, evidenceId = id, subjectKind = YQAssetLibrarySubjectKind.Asset,
                    subjectStableId = (string)prior["stableAssetId"], subjectSourceGuid = (string)prior["sourceGuid"],
                    subjectSourceAssetKey = (string)prior["sourceAssetKey"], familyId = (string)prior["kitId"],
                    approvalState = YQAssetLibraryApprovalState.Unknown, deliveryPermission = YQAssetLibraryDeliveryPermission.Unknown,
                    approvalScope = "technical_spatial_curation:" + fields[1],
                    evidenceRef = reviewPath + "#line=" + (index + 1), evidenceSha256 = reviewHash,
                    provenanceEvidenceRefs = new List<string>
                    {
                        beforePath + "#spatialAssets/" + (string)prior["sourceGuid"], authorPath + "#Run"
                    }
                });
            }
        }
    }

    private static void AddLibraryRelation(GenerationInventoryDomainBinding binding, string path, string hash, string role)
    {
        if (!string.IsNullOrWhiteSpace(path)) binding.sources.Add(new GenerationInventorySourceRelation { declaredPath = NormalizePath(path), declaredSha256 = hash, role = role });
    }

    internal static void AddEquipmentLodRelations(GenerationInventoryDomainBinding binding, Newtonsoft.Json.Linq.JObject catalogRow,
        Dictionary<string, Newtonsoft.Json.Linq.JObject> layoutByPath)
    {
        string json = (string)catalogRow["sourceContractJson"];
        if (string.IsNullOrWhiteSpace(json)) return;
        Newtonsoft.Json.Linq.JObject contract = Newtonsoft.Json.Linq.JObject.Parse(json);
        // note: Parts-only and revised entries can retain another version's contract; anchor its root to an exact export hash and declared alias.
        HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string declared in new[] { (string)catalogRow["sourcePath"], (string)catalogRow["partsSourcePath"] })
        {
            if (!layoutByPath.TryGetValue(declared ?? string.Empty, out Newtonsoft.Json.Linq.JObject row)) continue;
            List<string> aliases = new List<string> { (string)row["origin"], (string)row["path"] };
            foreach (Newtonsoft.Json.Linq.JToken alias in (Newtonsoft.Json.Linq.JArray)row["aliases"] ?? new Newtonsoft.Json.Linq.JArray()) aliases.Add((string)alias);
            aliases.Sort(StringComparer.Ordinal);
            bool anchored = false;
            foreach (string exportRole in new[] { "main", "parts", "lod1", "lod2" })
            {
                string path = (string)contract["exports"]?[exportRole]?["path"];
                string hash = (string)contract["exports"]?[exportRole]?["sha256"];
                if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(hash) ||
                    (!string.Equals(hash, (string)row["sha256"], StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(hash, (string)row["installedSha256"], StringComparison.OrdinalIgnoreCase))) continue;
                path = NormalizePath(path);
                foreach (string alias in aliases)
                {
                    string normalized = NormalizePath(alias);
                    if (normalized != null && normalized.EndsWith("/" + path, StringComparison.OrdinalIgnoreCase))
                    {
                        roots.Add(normalized.Substring(0, normalized.Length - path.Length));
                        anchored = true;
                        break;
                    }
                }
                if (anchored) break;
            }
        }
        string packRoot = roots.Count == 1 ? new List<string>(roots)[0] : null;
        foreach (string role in new[] { "lod1", "lod2", "parts" })
        {
            string path = NormalizePath((string)contract["exports"]?[role]?["path"]);
            if (!string.IsNullOrWhiteSpace(path)) AddLibraryRelation(binding, packRoot != null ? packRoot + path : path,
                (string)contract["exports"]?[role]?["sha256"], packRoot != null ? "declared_" + role : "unresolved_relative_" + role);
        }
    }

    private static string LibraryEquipmentPool(GenerationInventoryDomainBinding binding)
    {
        if (binding.category == "Weapons" || binding.category == "Special_Weapons" || binding.category == "weapon_assembly") return binding.family == "arrow" ? null : "weaponPrefabKeys";
        if (binding.family == "shield") return "offhandPrefabKeys";
        if (binding.category == "Consumables") return "consumablePrefabKeys";
        if (binding.family == "ring") return "ringPrefabKeys";
        if (binding.family == "amulet") return "necklacePrefabKeys";
        return binding.category == "Accessories" ? "trinketPrefabKeys" : null;
    }

    private static bool LibraryPoolContains(string text, string pool, string key)
    {
        Match block = Regex.Match(text, "(?ms)^  " + Regex.Escape(pool) + @":\s*\r?\n(?<values>(?:  - .*\r?\n(?:    [^\r\n]*\r?\n)*)*)");
        if (!block.Success) return false;
        string folded = Regex.Replace(block.Groups["values"].Value, @"\r?\n    ", " ");
        foreach (string line in folded.Split('\n'))
            if (line.StartsWith("  - ", StringComparison.Ordinal) && string.Equals(LibraryYamlScalar(line.Substring(4)), key, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void ReadLibraryProvenanceMembers(LibraryCoverageInputs inputs, string documentPath, string documentHash,
        Newtonsoft.Json.Linq.JToken token)
    {
        // note: Only exact declared payload hashes link source members; a family name never extends license or art approval scope.
        if (token is Newtonsoft.Json.Linq.JObject record && record["sha256"]?.Type == Newtonsoft.Json.Linq.JTokenType.String)
        {
            string hash = (string)record["sha256"];
            if (Regex.IsMatch(hash ?? string.Empty, "^[a-fA-F0-9]{64}$"))
            {
                HashSet<string> linked = new HashSet<string>(StringComparer.Ordinal);
                foreach (GenerationInventoryAsset source in inputs.sources)
                {
                    if (!string.Equals(hash, source.declaredSourceSha256, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(hash, source.installedSourceSha256, StringComparison.OrdinalIgnoreCase)) continue;
                    string id = "provenance-member:" + LibraryHashText(documentHash + "|" + token.Path + "|" + source.librarySourceId);
                    source.libraryEvidenceIds.Add(id);
                    if (!linked.Add(id)) continue;
                    YQAssetLibraryEvidenceRecord evidence = new YQAssetLibraryEvidenceRecord
                    {
                        contractVersion = 1, evidenceId = id, subjectKind = YQAssetLibrarySubjectKind.Asset,
                        subjectStableId = source.librarySourceId, subjectSourceGuid = source.sourceGuid,
                        sourceVersionId = LibraryVersionId(source.librarySourceId, hash), payloadSha256 = hash.ToLowerInvariant(),
                        evidenceRef = documentPath + "#" + token.Path, evidenceSha256 = documentHash,
                        approvalScope = "declared_provenance_member_hash_only"
                    };
                    evidence.provenanceEvidenceRefs.Add(evidence.evidenceRef);
                    if (record["source_url"]?.Type == Newtonsoft.Json.Linq.JTokenType.String)
                        evidence.provenanceEvidenceRefs.Add((string)record["source_url"]);
                    if (record["license_url"]?.Type == Newtonsoft.Json.Linq.JTokenType.String)
                        evidence.licenseEvidenceRefs.Add((string)record["license_url"]);
                    if (record["primary_license_evidence_urls"] is Newtonsoft.Json.Linq.JArray urls)
                        foreach (Newtonsoft.Json.Linq.JToken url in urls) evidence.licenseEvidenceRefs.Add((string)url);
                    inputs.evidence.Add(evidence);
                }
            }
        }
        if (token is Newtonsoft.Json.Linq.JContainer container)
            foreach (Newtonsoft.Json.Linq.JToken child in container.Children())
                ReadLibraryProvenanceMembers(inputs, documentPath, documentHash, child);
    }

    internal static GenerationInventoryDocument MergeGenerationInventoryCoverage(GenerationInventoryDocument baseline,
        LibraryCoverageInputs inputs)
    {
        // note: Merge copies into the existing master shape. Legacy classification, technical admission and registry keys remain owned by intake.
        if (baseline == null || inputs == null) throw new ArgumentNullException(baseline == null ? nameof(baseline) : nameof(inputs));
        GenerationInventoryDocument result = LibraryCopy(baseline);
        result.assets ??= new List<GenerationInventoryAsset>();
        result.libraryEvidence ??= new List<YQAssetLibraryEvidenceRecord>();
        result.kitEvidenceLinks ??= new List<GenerationInventoryKitEvidenceLinks>();
        result.sourceVersions ??= new List<GenerationInventorySourceVersion>();
        result.sourceSnapshots ??= new List<GenerationInventorySourceSnapshot>();
        result.domainBindings ??= new List<GenerationInventoryDomainBinding>();
        result.coverageConflicts ??= new List<GenerationInventoryCoverageConflict>();
        result.rootObservations ??= new List<GenerationInventoryRootObservation>();
        result.libraryObservationRoots ??= new List<string>();
        result.schemaVersion = "yq_world_generation_asset_inventory_v7";
        result.libraryCoverageVersion = 1;
        result.libraryContractVersion = YQAssetLibraryEvidenceRecord.SupportedContractVersion;

        List<GenerationInventoryAsset> observations = LibraryCopy(inputs.sources) ?? new List<GenerationInventoryAsset>();
        GenerationInventoryDocument previous = inputs.master;
        result.currentIntakeCatalogSnapshotId = inputs.currentIntakeCatalogSnapshotId ??
            result.currentIntakeCatalogSnapshotId ?? previous?.currentIntakeCatalogSnapshotId;
        if (previous != null && !ReferenceEquals(previous, baseline))
        {
            // note: A later legacy scan carries documentary rows/history even when its approved roots omit their source families.
            Dictionary<string, List<GenerationInventoryAsset>> currentIdentity = new Dictionary<string, List<GenerationInventoryAsset>>(StringComparer.Ordinal);
            foreach (GenerationInventoryAsset current in result.assets)
                IndexLibrarySource(currentIdentity, LibrarySourceId(current.sourceGuid, current.assetPath), current);
            foreach (GenerationInventoryAsset source in previous.assets ?? new List<GenerationInventoryAsset>())
            {
                // note: A current intake rescan records changed eligibility in bindings while retaining accepted historical tuples.
                if (source.reviewPolicyScope == GenerationInventoryReviewPolicyScope.LegacyWorldKit)
                {
                    GenerationInventoryAsset historical = null;
                    if (currentIdentity.TryGetValue(LibrarySourceId(source.sourceGuid, source.assetPath), out List<GenerationInventoryAsset> historicalRows))
                        historical = historicalRows.Find(row => string.Equals(row.assetPath, source.assetPath, StringComparison.Ordinal) &&
                            string.Equals(row.sourceGuid, source.sourceGuid, StringComparison.OrdinalIgnoreCase));
                    if (historical == null)
                    {
                        historical = LibraryCopy(source);
                        result.assets.Add(historical);
                        IndexLibrarySource(currentIdentity, LibrarySourceId(source.sourceGuid, source.assetPath), historical);
                    }
                    else RestoreHistoricalInventoryTuple(historical, source);
                }
                if (currentIdentity.TryGetValue(LibrarySourceId(source.sourceGuid, source.assetPath), out List<GenerationInventoryAsset> currentRows))
                    foreach (GenerationInventoryAsset current in currentRows)
                    {
                        MergeLibrarySourceMetadata(current, source, result.coverageConflicts);
                        if (source.reviewPolicyScope == GenerationInventoryReviewPolicyScope.LibraryCoverage && current.registryState == "source_asset_only")
                            RestoreLibraryCoverageClassification(current, source);
                    }
                if (source.reviewPolicyScope == GenerationInventoryReviewPolicyScope.LibraryCoverage) observations.Add(LibraryCopy(source));
            }
            result.libraryEvidence.AddRange(LibraryCopy(previous.libraryEvidence) ?? new List<YQAssetLibraryEvidenceRecord>());
            result.kitEvidenceLinks.AddRange(LibraryCopy(previous.kitEvidenceLinks) ?? new List<GenerationInventoryKitEvidenceLinks>());
            result.sourceVersions.AddRange(LibraryCopy(previous.sourceVersions) ?? new List<GenerationInventorySourceVersion>());
            result.sourceSnapshots.AddRange(LibraryCopy(previous.sourceSnapshots) ?? new List<GenerationInventorySourceSnapshot>());
            result.domainBindings.AddRange(LibraryCopy(previous.domainBindings) ?? new List<GenerationInventoryDomainBinding>());
            result.coverageConflicts.AddRange(LibraryCopy(previous.coverageConflicts) ?? new List<GenerationInventoryCoverageConflict>());
            result.rootObservations.AddRange(LibraryCopy(previous.rootObservations) ?? new List<GenerationInventoryRootObservation>());
            result.libraryObservationRoots.AddRange(previous.libraryObservationRoots ?? new List<string>());
        }
        result.libraryEvidence.AddRange(LibraryCopy(inputs.evidence) ?? new List<YQAssetLibraryEvidenceRecord>());
        result.sourceSnapshots.AddRange(LibraryCopy(inputs.snapshots) ?? new List<GenerationInventorySourceSnapshot>());
        result.coverageConflicts.AddRange(LibraryCopy(inputs.conflicts) ?? new List<GenerationInventoryCoverageConflict>());
        result.rootObservations.AddRange(LibraryCopy(inputs.observations) ?? new List<GenerationInventoryRootObservation>());
        result.libraryObservationRoots.AddRange(inputs.roots ?? new List<string>());

        Dictionary<string, List<GenerationInventoryAsset>> byGuid = new Dictionary<string, List<GenerationInventoryAsset>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<GenerationInventoryAsset>> byPath = new Dictionary<string, List<GenerationInventoryAsset>>(StringComparer.OrdinalIgnoreCase);
        foreach (GenerationInventoryAsset asset in result.assets)
        {
            asset.librarySourceId ??= LibrarySourceId(asset.sourceGuid, asset.assetPath);
            IndexLibrarySource(byGuid, asset.sourceGuid, asset);
            IndexLibrarySource(byPath, NormalizePath(asset.assetPath), asset);
        }
        if (observations.Exists(source => source == null || string.IsNullOrWhiteSpace(source.assetPath)))
            throw new InvalidDataException("Source observation has no path.");
        observations.Sort((a, b) =>
        {
            int identity = StringComparer.Ordinal.Compare(LibrarySourceId(a.sourceGuid, a.assetPath), LibrarySourceId(b.sourceGuid, b.assetPath));
            if (identity != 0) return identity;
            int authority = LibraryObservationStrength(b).CompareTo(LibraryObservationStrength(a));
            if (authority != 0) return authority;
            int path = StringComparer.Ordinal.Compare(a.assetPath, b.assetPath);
            return path != 0 ? path : StringComparer.Ordinal.Compare(a.installedSourceSha256, b.installedSourceSha256);
        });
        foreach (GenerationInventoryAsset observation in observations)
        {
            if (observation == null || string.IsNullOrWhiteSpace(observation.assetPath)) throw new InvalidDataException("Source observation has no path.");
            observation.librarySourceId = LibrarySourceId(observation.sourceGuid, observation.assetPath);
            List<GenerationInventoryAsset> matches = null;
            if (!string.IsNullOrWhiteSpace(observation.sourceGuid)) byGuid.TryGetValue(observation.sourceGuid, out matches);
            if (matches == null && byPath.TryGetValue(NormalizePath(observation.assetPath), out List<GenerationInventoryAsset> pathMatches))
            {
                matches = pathMatches.FindAll(asset => string.IsNullOrWhiteSpace(observation.sourceGuid) ||
                    string.IsNullOrWhiteSpace(asset.sourceGuid) || string.Equals(asset.sourceGuid, observation.sourceGuid, StringComparison.OrdinalIgnoreCase));
                if (matches.Count == 0) result.coverageConflicts.Add(new GenerationInventoryCoverageConflict { kind = "path_guid_conflict",
                    subject = observation.assetPath, reason = "Different GUIDs declare the same physical path; both identities are retained." });
                if (matches.Count != 1 && string.IsNullOrWhiteSpace(observation.sourceGuid)) matches = null;
            }
            if (matches == null || matches.Count == 0)
            {
                GenerationInventoryAsset added = LibraryCopy(observation);
                added.reviewPolicyScope = GenerationInventoryReviewPolicyScope.LibraryCoverage;
                added.runtimeEligible = false;
                result.assets.Add(added);
                IndexLibrarySource(byGuid, added.sourceGuid, added);
                IndexLibrarySource(byPath, NormalizePath(added.assetPath), added);
                matches = new List<GenerationInventoryAsset> { added };
            }
            foreach (GenerationInventoryAsset target in matches) MergeLibrarySourceMetadata(target, observation, result.coverageConflicts);
            AddLibraryVersion(result.sourceVersions, observation, observation.declaredSourceSha256, "supplied_baseline_hash_not_current_binary_verified");
            AddLibraryVersion(result.sourceVersions, observation, observation.installedSourceSha256, "supplied_installed_hash_not_current_binary_verified");
        }

        // note: Source relations use exact path/alias and optional declared hash; identical hashes alone do not identify independent files.
        Dictionary<string, List<GenerationInventoryAsset>> byAlias = new Dictionary<string, List<GenerationInventoryAsset>>(StringComparer.OrdinalIgnoreCase);
        foreach (GenerationInventoryAsset asset in result.assets)
        {
            IndexLibrarySource(byAlias, NormalizePath(asset.assetPath), asset);
            foreach (string alias in asset.sourceAliases ?? new List<string>()) IndexLibrarySource(byAlias, NormalizePath(alias), asset);
        }
        foreach (GenerationInventoryDomainBinding binding in LibraryCopy(inputs.bindings) ?? new List<GenerationInventoryDomainBinding>())
        {
            binding.sources ??= new List<GenerationInventorySourceRelation>();
            binding.consumerEvidence ??= new List<YQAssetLibraryConsumerEvidenceRecord>();
            foreach (GenerationInventorySourceRelation relation in binding.sources)
            {
                relation.relationId = binding.bindingId + ":source:" + LibraryHashText(relation.role + "|" + NormalizePath(relation.declaredPath) + "|" + relation.declaredSha256);
                relation.joinState = "unknown_source_identity";
                relation.joinMethod = "none";
                if (!byAlias.TryGetValue(NormalizePath(relation.declaredPath), out List<GenerationInventoryAsset> possible)) continue;
                Dictionary<string, GenerationInventoryAsset> identities = new Dictionary<string, GenerationInventoryAsset>(StringComparer.Ordinal);
                foreach (GenerationInventoryAsset source in possible)
                {
                    // note: World intake joins require both its literal GUID and current path, never a same-name alias.
                    if (binding.domain == "world_intake" &&
                        (string.IsNullOrWhiteSpace(binding.currentIntakeContract?.sourceGuid) ||
                         !string.Equals(source.sourceGuid, binding.currentIntakeContract.sourceGuid, StringComparison.OrdinalIgnoreCase) ||
                         !string.Equals(NormalizePath(source.assetPath), NormalizePath(relation.declaredPath), StringComparison.Ordinal)))
                        continue;
                    if (string.IsNullOrWhiteSpace(relation.declaredSha256) ||
                        string.Equals(relation.declaredSha256, source.declaredSourceSha256, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(relation.declaredSha256, source.installedSourceSha256, StringComparison.OrdinalIgnoreCase)) identities[source.librarySourceId] = source;
                }
                if (identities.Count != 1)
                {
                    relation.joinState = identities.Count == 0 ? "declared_hash_mismatch_or_unknown" : "ambiguous_source_identity";
                    result.coverageConflicts.Add(new GenerationInventoryCoverageConflict { kind = relation.joinState, subject = relation.relationId,
                        reason = "The exact path/alias and declared hash do not identify one canonical source; no filename inference is used." });
                    continue;
                }
                foreach (GenerationInventoryAsset source in identities.Values)
                {
                    relation.sourceId = source.librarySourceId;
                    relation.sourceVersionId = !string.IsNullOrWhiteSpace(relation.declaredSha256)
                        ? LibraryVersionId(source.librarySourceId, relation.declaredSha256) : source.sourceVersionId;
                    relation.installedSourceVersionId = source.sourceVersionId;
                    relation.joinState = "joined_declared_source";
                    relation.joinMethod = !string.IsNullOrWhiteSpace(relation.declaredSha256) ? "exact_path_or_layout_alias_and_declared_hash" : "exact_path_or_layout_alias_only";
                }
            }
            if (binding.domain == "world_intake")
            {
                GenerationInventorySourceRelation source = binding.sources.Find(row => row.role == "current_intake_source");
                bool joined = source?.joinState == "joined_declared_source";
                binding.resolvedAssetPath = joined ? source.declaredPath : null;
                if (!joined) binding.domainEligible = null;
                else source.joinMethod = "exact_intake_guid_and_path";
                if (binding.registryGuid != null &&
                    !string.Equals(binding.registryGuid, binding.currentIntakeContract?.sourceGuid, StringComparison.OrdinalIgnoreCase))
                    result.coverageConflicts.Add(new GenerationInventoryCoverageConflict { kind = "world_intake_registry_guid_mismatch",
                        subject = binding.bindingId, reason = "Serialized transport and current intake declare different GUIDs; technical eligibility is not runtime resolution." });
            }
            binding.sources = LibraryDistinctSorted(binding.sources);
            binding.consumerEvidence = LibraryDistinctSorted(binding.consumerEvidence);
            if (binding.currentIntakeContract != null)
            {
                binding.currentIntakeContract.libraryEvidenceIds = LibrarySortedStrings(binding.currentIntakeContract.libraryEvidenceIds);
                binding.currentIntakeContract.curationEvidenceIds = LibrarySortedStrings(binding.currentIntakeContract.curationEvidenceIds);
            }
            // note: Excluding the snapshot's own ID makes repeat projection idempotent.
            binding.bindingSnapshotId = null;
            binding.bindingSnapshotId = binding.bindingId + "@sha256:" + LibraryHashText(Newtonsoft.Json.JsonConvert.SerializeObject(binding));
            result.domainBindings.Add(binding);
        }
        NormalizeLibraryCoverage(result);
        result.legacyCounts = CountLibraryAssets(result.assets.FindAll(asset => asset.reviewPolicyScope == GenerationInventoryReviewPolicyScope.LegacyWorldKit));
        result.counts = CountLibraryAssets(result.assets);
        return result;
    }

    private static int LibraryObservationStrength(GenerationInventoryAsset source) =>
        !string.IsNullOrWhiteSpace(source.declaredSourceKind) ? 3 : !string.IsNullOrWhiteSpace(source.installedSourceSha256) ? 2 : 1;

    private static void IndexLibrarySource(Dictionary<string, List<GenerationInventoryAsset>> index, string key, GenerationInventoryAsset source)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (!index.TryGetValue(key, out List<GenerationInventoryAsset> records)) index.Add(key, records = new List<GenerationInventoryAsset>());
        if (!records.Contains(source)) records.Add(source);
    }

    private static void RestoreHistoricalInventoryTuple(GenerationInventoryAsset current, GenerationInventoryAsset previous)
    {
        // note: Documentary rescans must not turn later curation into a rewrite of the protected 22-field historical projection.
        current.stableAssetId = previous.stableAssetId; current.sourceGuid = previous.sourceGuid;
        current.sourceAssetKey = previous.sourceAssetKey; current.assetPath = previous.assetPath;
        current.assetType = previous.assetType; current.assetFamily = previous.assetFamily; current.sourceRoot = previous.sourceRoot;
        current.registryState = previous.registryState; current.slotTag = previous.slotTag; current.runtimeEligible = previous.runtimeEligible;
        current.finalState = previous.finalState; current.finalStateReason = previous.finalStateReason;
        current.representedByAssetPath = previous.representedByAssetPath; current.intakeDisposition = previous.intakeDisposition;
        current.technicalIssues = previous.technicalIssues == null ? null : new List<string>(previous.technicalIssues);
        current.classificationStatus = previous.classificationStatus; current.placementContextStatus = previous.placementContextStatus;
        current.paletteAssignmentStatus = previous.paletteAssignmentStatus; current.technicalValidationStatus = previous.technicalValidationStatus;
        current.reviewDisposition = previous.reviewDisposition; current.reviewPolicyVersion = previous.reviewPolicyVersion;
        current.reviewPolicyScope = previous.reviewPolicyScope;
    }

    private static void RestoreLibraryCoverageClassification(GenerationInventoryAsset current, GenerationInventoryAsset previous)
    {
        // note: A source-only rescan cannot reclassify previously documented unknown coverage using the old terminal kit policy.
        current.reviewPolicyScope = GenerationInventoryReviewPolicyScope.LibraryCoverage;
        current.registryState = previous.registryState;
        current.finalState = previous.finalState;
        current.finalStateReason = previous.finalStateReason;
        current.representedByAssetPath = previous.representedByAssetPath;
        current.intakeDisposition = previous.intakeDisposition;
        current.classificationStatus = previous.classificationStatus;
        current.placementContextStatus = previous.placementContextStatus;
        current.paletteAssignmentStatus = previous.paletteAssignmentStatus;
        current.technicalValidationStatus = previous.technicalValidationStatus;
        current.reviewDisposition = previous.reviewDisposition;
        current.reviewPolicyVersion = previous.reviewPolicyVersion;
    }

    private static void MergeLibrarySourceMetadata(GenerationInventoryAsset target, GenerationInventoryAsset source,
        List<GenerationInventoryCoverageConflict> conflicts)
    {
        // note: Historical path, GUID, stable ID, review tuple and runtime eligibility are never overwritten by observations.
        target.librarySourceId ??= LibrarySourceId(target.sourceGuid, target.assetPath);
        target.libraryEvidenceIds ??= new List<string>();
        target.sourceAliases ??= new List<string>();
        target.sourceDeclarationRefs ??= new List<string>();
        target.libraryEvidenceIds.AddRange(source.libraryEvidenceIds ?? new List<string>());
        target.sourceAliases.AddRange(source.sourceAliases ?? new List<string>());
        if (!string.Equals(target.assetPath, source.assetPath, StringComparison.OrdinalIgnoreCase)) target.sourceAliases.Add(source.assetPath);
        target.sourceDeclarationRefs.AddRange(source.sourceDeclarationRefs ?? new List<string>());
        if (!string.IsNullOrWhiteSpace(target.installedSourceSha256) && !string.IsNullOrWhiteSpace(source.installedSourceSha256) &&
            !string.Equals(target.installedSourceSha256, source.installedSourceSha256, StringComparison.OrdinalIgnoreCase))
            conflicts.Add(new GenerationInventoryCoverageConflict { kind = "source_version_declarations_differ", subject = target.librarySourceId,
                reason = "Multiple installed payload declarations exist; version history is retained and no binary verification is inferred." });
        target.declaredSourceSha256 ??= source.declaredSourceSha256;
        target.installedSourceSha256 ??= source.installedSourceSha256;
        target.baselineSourceVersionId ??= source.baselineSourceVersionId;
        target.sourceVersionId ??= source.sourceVersionId;
        target.declaredSourceKind ??= source.declaredSourceKind;
        if (source.sourceAvailability == "observed" || string.IsNullOrWhiteSpace(target.sourceAvailability)) target.sourceAvailability = source.sourceAvailability;
    }

    private static void AddLibraryVersion(List<GenerationInventorySourceVersion> versions, GenerationInventoryAsset source, string hash, string basis)
    {
        if (string.IsNullOrWhiteSpace(hash)) return;
        versions.Add(new GenerationInventorySourceVersion { sourceId = source.librarySourceId,
            sourceVersionId = LibraryVersionId(source.librarySourceId, hash), sha256 = hash.ToLowerInvariant(), identityBasis = basis,
            declarationRefs = LibrarySortedStrings(source.sourceDeclarationRefs) });
    }

    private static List<string> LibrarySortedStrings(IEnumerable<string> values)
    {
        SortedSet<string> unique = new SortedSet<string>(StringComparer.Ordinal);
        if (values != null) foreach (string value in values) if (!string.IsNullOrWhiteSpace(value)) unique.Add(value);
        return new List<string>(unique);
    }

    private static List<T> LibraryDistinctSorted<T>(IEnumerable<T> values)
    {
        SortedDictionary<string, T> unique = new SortedDictionary<string, T>(StringComparer.Ordinal);
        if (values != null) foreach (T value in values) unique[Newtonsoft.Json.JsonConvert.SerializeObject(value)] = value;
        return new List<T>(unique.Values);
    }

    private static void NormalizeLibraryCoverage(GenerationInventoryDocument document)
    {
        document.libraryObservationRoots = LibrarySortedStrings(document.libraryObservationRoots);
        document.sourceSnapshots = LibraryDistinctSorted(document.sourceSnapshots);
        SortedDictionary<string, GenerationInventorySourceVersion> versions = new SortedDictionary<string, GenerationInventorySourceVersion>(StringComparer.Ordinal);
        foreach (GenerationInventorySourceVersion version in document.sourceVersions)
        {
            if (!versions.TryGetValue(version.sourceVersionId, out GenerationInventorySourceVersion existing))
            {
                existing = LibraryCopy(version);
                existing.identityBasis = "supplied_payload_hash_not_current_binary_verified";
                existing.declarationRefs ??= new List<string>();
                versions.Add(version.sourceVersionId, existing);
            }
            existing.declarationRefs.AddRange(version.declarationRefs ?? new List<string>());
        }
        document.sourceVersions = new List<GenerationInventorySourceVersion>(versions.Values);
        foreach (GenerationInventorySourceVersion version in document.sourceVersions) version.declarationRefs = LibrarySortedStrings(version.declarationRefs);
        document.rootObservations = LibraryDistinctSorted(document.rootObservations);
        document.coverageConflicts = LibraryDistinctSorted(document.coverageConflicts);
        foreach (GenerationInventoryAsset asset in document.assets)
        {
            asset.libraryEvidenceIds = LibrarySortedStrings(asset.libraryEvidenceIds);
            asset.sourceAliases = LibrarySortedStrings(asset.sourceAliases);
            asset.sourceDeclarationRefs = LibrarySortedStrings(asset.sourceDeclarationRefs);
        }
        document.assets.Sort((a, b) =>
        {
            int path = StringComparer.OrdinalIgnoreCase.Compare(a.assetPath, b.assetPath);
            if (path != 0) return path;
            int guid = StringComparer.Ordinal.Compare(a.sourceGuid, b.sourceGuid);
            return guid != 0 ? guid : StringComparer.Ordinal.Compare(a.stableAssetId, b.stableAssetId);
        });
        foreach (GenerationInventoryDomainBinding binding in document.domainBindings)
        { binding.sources = LibraryDistinctSorted(binding.sources); binding.consumerEvidence = LibraryDistinctSorted(binding.consumerEvidence); }
        document.domainBindings = LibraryDistinctSorted(document.domainBindings);
        List<YQAssetLibraryEvidenceRecord> evidence = document.libraryEvidence;
        document.libraryEvidence = new List<YQAssetLibraryEvidenceRecord>();
        CopyDocumentaryExportEvidence(document, evidence);
        document.libraryEvidence = LibraryDistinctSorted(document.libraryEvidence);
        document.kitEvidenceLinks = LibraryDistinctSorted(document.kitEvidenceLinks);
    }

    private static GenerationInventoryCounts CountLibraryAssets(IEnumerable<GenerationInventoryAsset> assets)
    {
        GenerationInventoryCounts counts = new GenerationInventoryCounts();
        foreach (GenerationInventoryAsset asset in assets)
        {
            counts.total++;
            if (asset.assetType == "prefab") counts.prefabs++; else if (asset.assetType == "material") counts.materials++; else counts.sourceAssets++;
            string state = asset.finalState ?? string.Empty;
            if (state == "generation_ready") counts.generationReady++;
            else if (state.StartsWith("quarantined_", StringComparison.Ordinal)) counts.quarantined++;
            else if (state.StartsWith("intentionally_excluded_", StringComparison.Ordinal)) counts.intentionallyExcluded++;
            else if (state == "duplicate_variant_represented_by_prefab") counts.representedVariants++;
            else if (state == "not_applicable_independent_generation_asset") counts.notApplicable++;
            else counts.pendingReview++;
        }
        return counts;
    }

    private static void ReadLibraryProvenance(LibraryTextReader reader, LibraryCoverageInputs inputs, Newtonsoft.Json.Linq.JObject layout)
    {
        foreach (Newtonsoft.Json.Linq.JObject row in (Newtonsoft.Json.Linq.JArray)layout["files"])
        {
            string path = (string)row["path"], kind = (string)row["kind"] ?? string.Empty;
            bool license = Path.GetFileName(path).IndexOf("license", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!kind.Contains("Provenance") && !license) continue;
            string text = reader.Read(path), actualHash = reader.Hash(path);
            string declaredHash = (string)row["installedSha256"] ?? (string)row["sha256"];
            if (!string.Equals(actualHash, declaredHash, StringComparison.OrdinalIgnoreCase))
                inputs.conflicts.Add(new GenerationInventoryCoverageConflict { kind = "provenance_hash_mismatch", subject = path, reason = "Actual text differs from the supplied layout hash." });
            YQAssetLibraryEvidenceRecord evidence = new YQAssetLibraryEvidenceRecord
            {
                contractVersion = 1, evidenceId = "source-document:" + LibraryHashText(path + "|" + actualHash),
                subjectKind = YQAssetLibrarySubjectKind.Asset, subjectSourceGuid = (string)row["guid"],
                subjectStableId = LibrarySourceId((string)row["guid"], path), familyId = (string)row["pack"],
                sourceVersionId = LibraryVersionId(LibrarySourceId((string)row["guid"], path), actualHash),
                evidenceRef = path, evidenceSha256 = actualHash, approvalScope = "documentary_source_only"
            };
            if (license) evidence.licenseEvidenceRefs.Add(path); else evidence.provenanceEvidenceRefs.Add(path);
            inputs.evidence.Add(evidence);
            foreach (GenerationInventoryAsset source in inputs.sources)
                if (string.Equals(source.assetPath, path, StringComparison.OrdinalIgnoreCase)) source.libraryEvidenceIds.Add(evidence.evidenceId);
            if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                ReadLibraryProvenanceMembers(inputs, path, actualHash, Newtonsoft.Json.Linq.JToken.Parse(text));
        }
    }

    private static void ReadLibraryTrackerFacts(LibraryTextReader reader, LibraryCoverageInputs inputs)
    {
        string text = reader.Read(ArtTrackerPath), hash = reader.Hash(ArtTrackerPath);
        string[] lines = text.Replace("\r", string.Empty).Split('\n');
        foreach (string filename in new[] { "YourQuest_Fairy_v4_REVIEW_ONLY_UNAPPROVED_2026-10-03.zip",
            "YourQuest_Avian_V6_REVIEW_ONLY_UNAPPROVED_2026-10-03.zip", "YourQuest_Kitsune_AdultBase_v12_Runtime.zip", "YourQuest_Kitsune_AdultBase_v12_Editable_Modules.zip" })
        {
            int line = Array.FindIndex(lines, value => value.Contains(filename));
            if (line < 0) throw new InvalidDataException("Named tracker fact is missing: " + filename);
            int header = line;
            while (header >= 0 && !lines[header].StartsWith("**", StringComparison.Ordinal)) header--;
            string heading = header >= 0 ? lines[header] : string.Empty;
            Match payload = Regex.Match(lines[line], @"(?:SHA-256|sha256)[ `]*([a-fA-F0-9]{64})", RegexOptions.IgnoreCase);
            bool withdrawn = heading.Contains("Withdrawn from delivery"), held = heading.Contains("HELD");
            if (!payload.Success || (!withdrawn && !held)) throw new InvalidDataException("Unknown exact tracker state or payload: " + filename);
            inputs.evidence.Add(new YQAssetLibraryEvidenceRecord
            {
                contractVersion = 1, evidenceId = "tracker-package:" + LibraryHashText(filename + "|" + payload.Groups[1].Value + "|" + hash),
                subjectKind = YQAssetLibrarySubjectKind.Package, subjectStableId = "package:" + filename,
                packageId = filename, packageVersion = filename.Contains("v12") ? "v12" : filename.Contains("Fairy_v4") ? "v4" : "V6",
                payloadSha256 = payload.Groups[1].Value.ToLowerInvariant(), sourceVersionId = "package:sha256:" + payload.Groups[1].Value.ToLowerInvariant(),
                approvalState = withdrawn ? YQAssetLibraryApprovalState.Withdrawn : YQAssetLibraryApprovalState.Held,
                approvalScope = "delivery", deliveryPermission = YQAssetLibraryDeliveryPermission.Denied,
                evidenceRef = ArtTrackerPath + "#lines=" + (header + 1) + "-" + (line + 1), evidenceSha256 = hash
            });
        }
    }

    private static void ApplyTerminalReviewPolicy(GenerationInventoryDocument document)
    {
        if (document == null || document.assets == null)
            return;

        document.reviewPolicyNotes ??= new List<string>();
        document.reviewPolicyNotes.Clear();
        document.reviewPolicyNotes.Add("Only typed, technically validated, spatially curated records enter generation_ready.");
        document.reviewPolicyNotes.Add("Unreviewed source models are excluded when no approved runtime prefab binding exists.");
        document.reviewPolicyNotes.Add("Unreviewed prefab context or material compatibility is excluded until a family policy supplies the missing contract.");

        for (int index = 0; index < document.assets.Count; index++)
        {
            GenerationInventoryAsset asset = document.assets[index];
            if (asset == null)
                continue;

            // note: The historical world-kit policy cannot turn newly catalogued documentary unknowns into approvals or exclusions.
            if (asset.reviewPolicyScope != GenerationInventoryReviewPolicyScope.LegacyWorldKit)
                continue;

            asset.reviewPolicyVersion = document.reviewPolicyVersion;
            if (string.IsNullOrWhiteSpace(asset.reviewDisposition))
                asset.reviewDisposition = asset.finalState == "generation_ready"
                    ? "approved_for_generation"
                    : asset.finalState != null && asset.finalState.StartsWith("quarantined_", StringComparison.OrdinalIgnoreCase)
                        ? "technical_quarantine"
                        : asset.finalState == "duplicate_variant_represented_by_prefab"
                            ? "represented_by_prefab"
                            : asset.finalState == "not_applicable_independent_generation_asset"
                                ? "not_applicable"
                                : asset.finalState != null && asset.finalState.StartsWith("intentionally_excluded_", StringComparison.OrdinalIgnoreCase)
                                    ? "intentional_exclusion"
                                    : "review_required";

            if (string.IsNullOrWhiteSpace(asset.finalState) ||
                !asset.finalState.StartsWith("pending_", StringComparison.OrdinalIgnoreCase))
                continue;

            string previousReason = asset.finalStateReason ?? string.Empty;
            if (string.Equals(asset.assetType, "source_model", StringComparison.OrdinalIgnoreCase))
            {
                asset.finalState = "intentionally_excluded_source_without_runtime_binding";
                asset.finalStateReason = "No approved runtime prefab binding exists for this source model. " + previousReason;
                asset.technicalValidationStatus = "excluded_source_input";
            }
            else if (string.Equals(asset.assetType, "material", StringComparison.OrdinalIgnoreCase))
            {
                asset.finalState = "intentionally_excluded_unreviewed_material";
                asset.finalStateReason = "Material compatibility is not reviewed for independent runtime use; it remains a dependency only. " + previousReason;
                asset.technicalValidationStatus = "excluded_material_dependency";
            }
            else
            {
                asset.finalState = "intentionally_excluded_unreviewed_context";
                asset.finalStateReason = "Prefab has no authored V2 placement and palette contract, so it is excluded until family policy review. " + previousReason;
                asset.technicalValidationStatus = "excluded_missing_curation";
            }

            asset.reviewDisposition = "intentional_exclusion";
            asset.reviewPolicyVersion = document.reviewPolicyVersion;
            asset.classificationStatus = "reviewed_excluded";
            asset.placementContextStatus = "excluded_pending_policy";
            asset.paletteAssignmentStatus = "excluded_pending_policy";
        }
    }

    private static string BuildPrefabVariantKey(GenerationInventoryAsset asset)
    {
        string path = NormalizePath(asset.assetPath);
        int artBoundary = path.IndexOf("/Art/", StringComparison.OrdinalIgnoreCase);
        string family = artBoundary >= 0
            ? path.Substring(0, artBoundary)
            : asset.sourceRoot ?? string.Empty;

        // note: Only one exact-stem prefab inside this source family may represent a source model; ambiguous candidates stay pending review.
        return family + "|" + Path.GetFileNameWithoutExtension(path);
    }

    private static GenerationInventoryAsset BuildSourceInventoryAsset(
        string guid,
        string path,
        YQAssetKitManifest kit)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        bool potentialPrefab = extension == ".prefab";
        bool potentialMaterial = extension == ".mat";
        bool sourceModel = extension == ".fbx" || extension == ".obj" ||
                           extension == ".blend" || extension == ".gltf" || extension == ".glb";
        bool authoredScene = extension == ".unity";
        // note: Only prefabs and source models can become independent spawn records; materials, scenes, and data files are dependencies or authoring inputs.
        bool independentCandidate = potentialPrefab || sourceModel;

        // note: The file extension establishes only independent generation candidacy, never physical placement context for an unreviewed model.
        return new GenerationInventoryAsset
        {
            stableAssetId = string.IsNullOrWhiteSpace(guid) ? string.Empty : "source_" + guid,
            sourceGuid = guid,
            assetPath = path,
            assetType = potentialPrefab ? "prefab" : potentialMaterial ? "material" :
                sourceModel ? "source_model" : authoredScene ? "authored_scene" : "source_dependency",
            assetFamily = kit?.kitId ?? string.Empty,
            sourceRoot = kit?.sourceRoot ?? string.Empty,
            registryState = "source_asset_only",
            slotTag = string.Empty,
            runtimeEligible = false,
            finalState = independentCandidate ? "pending_source_asset_review" : "not_applicable_independent_generation_asset",
            finalStateReason = independentCandidate
                ? "Source model or prefab candidate has not been reviewed as an independent procedural spawn record."
                : "Source dependency or authored scene is consumed by the project and is not an independent spawn candidate.",
            intakeDisposition = "not_in_typed_prefab_material_intake",
            technicalIssues = new List<string>(),
            classificationStatus = independentCandidate ? "pending" : "dependency",
            placementContextStatus = independentCandidate ? "pending" : "not_applicable",
            paletteAssignmentStatus = independentCandidate ? "pending" : "not_applicable",
            technicalValidationStatus = "not_assessed_independently"
        };
    }

    private static string ResolveInventoryPrefabState(YQSpatialAssetRecord record)
    {
        if (record == null)
            return "pending_review";

        switch (record.disposition)
        {
            case YQAssetIntakeDisposition.NeedsMaterialRepair:
            case YQAssetIntakeDisposition.MissingRenderer:
            case YQAssetIntakeDisposition.MissingScriptRepair:
            case YQAssetIntakeDisposition.Quarantined:
                return "quarantined_technical_defect";
            case YQAssetIntakeDisposition.EditorOrDemoOnly:
                return "intentionally_excluded_editor_only";
            default:
                // note: Hard physical-safety failures are quarantined until repaired; only ambiguous placement remains pending review.
                return HasBlockingPrefabTechnicalIssue(record)
                    ? "quarantined_technical_defect"
                    : "pending_spatial_review";
        }
    }

    private static string ResolveInventoryPrefabTechnicalStatus(YQSpatialAssetRecord record)
    {
        if (record == null)
            return "pending";

        switch (record.disposition)
        {
            case YQAssetIntakeDisposition.NeedsMaterialRepair:
            case YQAssetIntakeDisposition.MissingRenderer:
            case YQAssetIntakeDisposition.MissingScriptRepair:
            case YQAssetIntakeDisposition.Quarantined:
                return "failed";
            default:
                if (HasBlockingPrefabTechnicalIssue(record))
                    return "failed";
                return record.validationIssues != null && record.validationIssues.Count > 0
                    ? "review_required"
                    : "automated_checks_passed";
        }
    }

    private static bool HasBlockingPrefabTechnicalIssue(YQSpatialAssetRecord record)
    {
        if (record == null || record.validationIssues == null)
            return false;

        for (int index = 0; index < record.validationIssues.Count; index++)
        {
            string issue = record.validationIssues[index] ?? string.Empty;
            if (issue.IndexOf("No reliable mesh bounds", StringComparison.OrdinalIgnoreCase) >= 0 ||
                issue.IndexOf("Structural candidate has no collider profile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                issue.IndexOf("Large or renderer-heavy candidate needs LOD/HLOD review", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static string ResolveInventoryMaterialState(YQMaterialAssetRecord record)
    {
        if (record == null || record.compatibilityState == YQMaterialCompatibilityState.NeedsReview ||
            record.compatibilityState == YQMaterialCompatibilityState.Unknown)
            return "pending_material_review";

        return "quarantined_technical_defect";
    }

    private static string ResolveInventoryMaterialReason(YQMaterialAssetRecord record)
    {
        if (record != null && record.validationIssues != null && record.validationIssues.Count > 0)
            return string.Join(" ", record.validationIssues);

        return record == null
            ? "Material intake record is missing."
            : "Material compatibility state: " + record.compatibilityState + ".";
    }

    private static string ResolveInventoryPrefabReason(YQSpatialAssetRecord record)
    {
        if (record == null)
            return "No intake record was available.";

        if (record.validationIssues != null && record.validationIssues.Count > 0)
            return string.Join(" ", record.validationIssues);

        return "Prefab lacks authored spatial release approval.";
    }

    private static void WriteSummaryReport(
        string scanScope,
        List<YQAssetKitManifest> kits,
        List<YQSpatialAssetRecord> spatialAssets,
        List<YQMaterialAssetRecord> materials,
        string outputReportPath)
    {
        EnsureFolderPath(
            Path.GetDirectoryName(outputReportPath).Replace('\\', '/'));

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
        int compiledEligiblePrefabs = 0;
        for (int index = 0; index < spatialAssets.Count; index++)
        {
            YQSpatialAssetRecord record = spatialAssets[index];
            if (record == null || !record.releaseEligible || record.disposition != YQAssetIntakeDisposition.Candidate ||
                !record.spatialMetadataAuthored || record.curationV2 == null ||
                record.curationV2.contractVersion != YQAssetCurationContractV2.SupportedContractVersion)
                continue;
            for (int kitIndex = 0; kitIndex < kits.Count; kitIndex++)
            {
                YQAssetKitManifest kit = kits[kitIndex];
                if (kit != null && kit.releaseEligible && string.Equals(kit.kitId, record.kitId, StringComparison.OrdinalIgnoreCase))
                {
                    compiledEligiblePrefabs++;
                    break;
                }
            }
        }
        report.AppendLine(
            "- Compiled-world eligible prefabs: " + compiledEligiblePrefabs);
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
        report.AppendLine(
            "Canonical inventory policy: world-kit-policy-v1. Every unresolved discovery record is assigned an explicit intentional-exclusion, technical-quarantine, duplicate/variant, or not-applicable disposition before it can be counted.");

        File.WriteAllText(
            outputReportPath,
            report.ToString());

        AssetDatabase.ImportAsset(
            outputReportPath,
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
// note: Automatic refresh verification touch for shared inventory discovery gate.
