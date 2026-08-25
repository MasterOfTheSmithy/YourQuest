using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQAllPackAuthoredSiteBatchBuilder
{
    private const string OutputRoot =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/AllPacks";

    private const string ReportPath =
        OutputRoot + "/YQ_AllPackAuthoredSiteBatchReport.md";

    private static readonly string[] ExcludedSourceTokens =
    {
        "camera", "postprocess", "post_process", "volume", "skybox",
        "directionallight", "directional_light", "reflectionprobe",
        "reflection_probe", "terrain", "landscape", "waterplane",
        "water_plane", "cloud", "mountain"
    };

    private static readonly HashSet<string> MountainOwningPackIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mountain_temple",
            "messenger_mountain",
            "the_messenger_mountain"
        };

    private static readonly PackDefinition[] Packs =
    {
        Pack("gothic_cathedral", "Gothic Cathedral", YQAuthoredSiteKind.Landmark, "Assets/HIVEMIND/GothicCathedral/HDRP(Default)/Scenes/LV_GothicCathedral.unity", true),
        Pack("hallowed_depths", "Hallowed Depths", YQAuthoredSiteKind.Dungeon, "Assets/HIVEMIND/HallowedDepths/HDRP(Default)/Scenes/LV_HallowedDepths_Main.unity", true),
        Pack("haunted_village", "Haunted Village", YQAuthoredSiteKind.Settlement, "Assets/HIVEMIND/HauntedVillage/HDRP (Default)/Scenes/LV_HauntedVillage.unity", true),
        Pack("cave_hidden_tomb", "Cave Of Hidden Tomb", YQAuthoredSiteKind.Dungeon, "Assets/HIVEMIND/CaveOfHiddenTomb/HDRP (Default)/Scenes/LV_CaveofHiddenTomb.unity", true),
        Pack("cyberpunk_city", "Cyberpunk City", YQAuthoredSiteKind.SciFiSite, "Assets/HIVEMIND/CyberpunkCity/HDRP(Default)/Scenes/LV_CyberAlley_WP.unity", true),
        Pack("gladiator_arena", "Gladiator Arena", YQAuthoredSiteKind.Landmark, "Assets/HIVEMIND/GladitorArena/HDRP(Default)/Scenes/LV_Showcase.unity", true),
        Pack("messenger_mountain", "The Messenger Mountain", YQAuthoredSiteKind.Wilderness, "Assets/HIVEMIND/HDRP/TheMessengerMountain/Scene/DemoMap.unity", true),
        Pack("horror_hospital", "Horror Hospital", YQAuthoredSiteKind.Interior, "Assets/HIVEMIND/HorrorHospital/HDRP(Default)/Scenes/L_Horror_Hospital.unity", true),
        Pack("house_on_a_hill", "House On A Hill", YQAuthoredSiteKind.Interior, "Assets/HIVEMIND/HouseOnaHill/HDRP/Scenes/FeatherHDRP.unity", true),
        Pack("medieval_kingdom", "Medieval Kingdom", YQAuthoredSiteKind.Settlement, "Assets/HIVEMIND/MedievalKingdom/HDRP(Default)/Scenes/L_CastleTown.unity", true),
        Pack("military_camp", "Military Camp", YQAuthoredSiteKind.Camp, "Assets/HIVEMIND/MilitaryCamp/HDRP(Default)/Scenes/Showcase/LV_Showcase.unity", true),
        Pack("modular_viking_village", "Modular Viking Village", YQAuthoredSiteKind.Settlement, "Assets/HIVEMIND/ModularVikingVillage/HDRP/Scene/LV_MainVillage.unity", true),
        Pack("mountain_temple", "Mountain Temple", YQAuthoredSiteKind.Landmark, "Assets/HIVEMIND/MountainTemple/HDRP(Default)/Scenes/LV_Showcase.unity", true),
        Pack("mystic_dungeon", "Mystic Dungeon", YQAuthoredSiteKind.Dungeon, "Assets/HIVEMIND/MysticDungeon/HDRP(Default)/Scenes/LV_MysticDungeon.unity", true),
        Pack("native_american_village", "Native American Village", YQAuthoredSiteKind.Settlement, "Assets/HIVEMIND/NativeAmericanVillage/HDRP(Default)/Scenes/LV_Showcase.unity", true),
        Pack("olympus_temple", "Olympus Temple", YQAuthoredSiteKind.Landmark, "Assets/HIVEMIND/OlympusTemple/HDRP/Scene/L_Olympus.unity", true),
        Pack("pirate_island", "Pirate Island", YQAuthoredSiteKind.Settlement, "Assets/HIVEMIND/PirateIsland/HDRP(Default)/Scenes/LV_PirateIslandShowcase.unity", true),
        Pack("rural_town", "Rural Town", YQAuthoredSiteKind.Settlement, "Assets/HIVEMIND/RuralTown/HDRP(Default)/Scenes/LV_Showcase.unity", true),
        Pack("the_sewers", "The Sewers", YQAuthoredSiteKind.Dungeon, "Assets/HIVEMIND/TheSewers/HDRP(Default)/Scenes/L_Sewer.unity", true),
        Pack("town_smith", "Town Smith", YQAuthoredSiteKind.Settlement, "Assets/HIVEMIND/TownSmith/HDRP(Default)/Scenes/L_MedievalTownDayLight.unity", true),
        Pack("villa_forge", "Villa Forge", YQAuthoredSiteKind.Landmark, "Assets/HIVEMIND/VillaForge/HDRP(Default)/Scenes/L_Showcase.unity", true),
        Pack("witch_house", "Witch House", YQAuthoredSiteKind.Interior, "Assets/HIVEMIND/WitchHouse/HDRP(Default)/Scene/Witch_House.unity", true),
        Pack("ancient_desert_ruins", "Ancient Desert Ruins", YQAuthoredSiteKind.Landmark, "Assets/BefourStudios/AncientDesertRuins/Art/Scenes/DesertRuinsDemoMap1.unity", false),
        Pack("asian_dynasty", "Asian Dynasty", YQAuthoredSiteKind.Settlement, "Assets/BefourStudios/AsianDynastyEnvironment/Scenes/DemoMap_URP.unity", false),
        Pack("bio_horror_scifi", "Bio Horror Sci-Fi", YQAuthoredSiteKind.SciFiSite, "Assets/BefourStudios/BioHorrorSciFiEnvironment/Scenes/SciFiDemoURP.unity", false),
        Pack("container_district", "Container District", YQAuthoredSiteKind.SciFiSite, "Assets/BefourStudios/ContainerDistrict/Art/Scenes/ContainerDistrict.unity", false),
        Pack("medieval_viking_village", "Medieval Viking Village", YQAuthoredSiteKind.Settlement, "Assets/BefourStudios/MedievalVikingVillage/Art/Scenes/VillageMapURP.unity", false),
        Pack("nordic_village", "Nordic Village", YQAuthoredSiteKind.Settlement, "Assets/BefourStudios/NordicVillage/Art/Scenes/Viking_Village.unity", false),
        Pack("persepolis_empire", "Persepolis Empire", YQAuthoredSiteKind.Settlement, "Assets/BefourStudios/PersepolisEmpireEnvironment/Art/Scenes/MAP_Demo.unity", false),
        Pack("scifi_engineers_room", "Sci-Fi Engineers Room", YQAuthoredSiteKind.SciFiSite, "Assets/BefourStudios/SciFiEngineersRoom/Art/Scenes/MainMap.unity", false),
        Pack("victorian_mansion", "Victorian Mansion", YQAuthoredSiteKind.Interior, "Assets/BefourStudios/VictorianMansionEnvironment/Art/Scenes/MansionDemoMap_URP.unity", false),
        Pack("western_desert_town", "Western Desert Town", YQAuthoredSiteKind.Settlement, "Assets/BefourStudios/WesternDesertTown/Art/Scenes/WesternDesertTown.unity", false)
    };

    private static Queue<PackDefinition> _pending;
    private static List<PackResult> _results;
    private static int _totalPackCount;
    private static Action<bool> _completion;

    private sealed class PackDefinition
    {
        public string kitId;
        public string displayName;
        public YQAuthoredSiteKind siteKind;
        public string sourceScenePath;
        public bool forceUrpConversion;
    }

    private sealed class SourceInstance
    {
        public GameObject sceneObject;
        public GameObject sourcePrefab;
        public string sourcePath;
        public string sourceName;
        public Bounds worldBounds;
    }

    private sealed class PackResult
    {
        public string kitId;
        public string displayName;
        public string sourceScenePath;
        public bool succeeded;
        public int sourceInstanceCount;
        public int repairedMaterialSlotCount;
        public int unresolvedMaterialSlotCount;
        public int removedMissingScriptCount;
        public string prefabPath;
        public string reviewScenePath;
        public string failure = string.Empty;
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Assembly Authoring/Build All Authored Pack Golden Sites")]
    public static void BuildAllAuthoredPackGoldenSites()
    {
        StartBatch(BuildDefinitions(false), null);
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Assembly Authoring/Build Pending Authored Pack Golden Sites")]
    public static void BuildPendingAuthoredPackGoldenSites()
    {
        StartBatch(BuildDefinitions(true), null);
    }

    public static bool RebuildAuthoredPack(
        string kitId,
        Action<bool> completion)
    {
        PackDefinition definition = BuildDefinitions(false)
            .FirstOrDefault(candidate => string.Equals(
                candidate.kitId,
                kitId,
                StringComparison.OrdinalIgnoreCase));

        if (definition == null)
        {
            Debug.LogError(
                "[YQAllPackAuthoredSiteBatchBuilder] No authored source is configured for " +
                kitId + ".");
            completion?.Invoke(false);
            return false;
        }

        // note: Review repair rebuilds only the selected pack from its immutable authored scene; it never reruns or overwrites unrelated approved packs.
        return StartBatch(
            new List<PackDefinition> { definition },
            completion);
    }

    private static bool StartBatch(
        List<PackDefinition> definitions,
        Action<bool> completion)
    {
        if (_pending != null)
        {
            Debug.LogWarning(
                "[YQAllPackAuthoredSiteBatchBuilder] The all-pack batch is already running.");
            completion?.Invoke(false);
            return false;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQAllPackAuthoredSiteBatchBuilder] Wait for Unity to be idle in Edit mode before starting the batch.");
            completion?.Invoke(false);
            return false;
        }

        if (definitions == null || definitions.Count == 0)
        {
            Debug.Log(
                "[YQAllPackAuthoredSiteBatchBuilder] No authored environment packs are pending extraction.");
            completion?.Invoke(false);
            return false;
        }

        // note: One pack is processed per editor update so a large authored library does not freeze the editor for the entire batch.
        _pending = new Queue<PackDefinition>(definitions);
        _results = new List<PackResult>();
        _totalPackCount = definitions.Count;
        _completion = completion;
        EditorApplication.update += ProcessNextPack;

        Debug.Log(
            "[YQAllPackAuthoredSiteBatchBuilder] Started authored-site extraction for " +
            _totalPackCount + " environment packs.");
        return true;
    }

    private static List<PackDefinition> BuildDefinitions(bool pendingOnly)
    {
        YQAuthoredSiteSourceCatalog catalog =
            YQAuthoredSiteSourceDiscovery.SyncCatalog(false);
        List<PackDefinition> result = new List<PackDefinition>();
        HashSet<string> includedScenes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!pendingOnly)
        {
            for (int index = 0; index < Packs.Length; index++)
            {
                result.Add(Packs[index]);
                includedScenes.Add(Packs[index].sourceScenePath);
            }
        }

        IReadOnlyList<YQAuthoredSiteSourceRecord> records = catalog.Records;

        for (int index = 0; index < records.Count; index++)
        {
            YQAuthoredSiteSourceRecord record = records[index];

            if (record == null || string.IsNullOrWhiteSpace(record.selectedScenePath))
                continue;

            bool isPending =
                record.state == YQAuthoredSiteSourceState.DetectedPendingBuild ||
                record.state == YQAuthoredSiteSourceState.SourceChanged ||
                record.state == YQAuthoredSiteSourceState.BuildFailed;

            if (pendingOnly && !isPending)
                continue;

            PackDefinition known = FindKnownDefinition(record.selectedScenePath);
            PackDefinition definition = known ?? new PackDefinition
            {
                kitId = record.kitId,
                displayName = record.displayName,
                siteKind = record.siteKind,
                sourceScenePath = record.selectedScenePath,
                forceUrpConversion = record.forceUrpConversion
            };

            if (includedScenes.Add(definition.sourceScenePath))
            {
                result.Add(definition);
            }
        }

        return result;
    }

    private static PackDefinition FindKnownDefinition(string sourceScenePath)
    {
        for (int index = 0; index < Packs.Length; index++)
        {
            if (string.Equals(
                    Packs[index].sourceScenePath,
                    sourceScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Packs[index];
            }
        }

        return null;
    }

    private static void ProcessNextPack()
    {
        if (_pending == null)
        {
            FinishBatch(true);
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        if (_pending.Count == 0)
        {
            FinishBatch(false);
            return;
        }

        int completed = _totalPackCount - _pending.Count;
        PackDefinition definition = _pending.Peek();
        bool cancelled = EditorUtility.DisplayCancelableProgressBar(
            "YourQuest Authored Site Library",
            "Preserving " + definition.displayName +
            " (" + (completed + 1) + "/" + _totalPackCount + ")",
            _totalPackCount > 0
                ? (float)completed / _totalPackCount
                : 1f);

        if (cancelled)
        {
            FinishBatch(true);
            return;
        }

        _pending.Dequeue();

        try
        {
            PackResult result = BuildPack(definition);
            _results.Add(result);
            MarkCatalogBuildResult(result);
        }
        catch (Exception exception)
        {
            _results.Add(
                new PackResult
                {
                    kitId = definition.kitId,
                    displayName = definition.displayName,
                    sourceScenePath = definition.sourceScenePath,
                    succeeded = false,
                    failure = exception.Message
                });
            MarkCatalogBuildFailure(definition.sourceScenePath, exception.Message);
            Debug.LogException(exception);
        }
    }

    private static PackResult BuildPack(PackDefinition definition)
    {
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(definition.sourceScenePath))
        {
            throw new InvalidOperationException(
                definition.displayName + " source scene is missing: " +
                definition.sourceScenePath);
        }

        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene sourceScene = SceneManager.GetSceneByPath(definition.sourceScenePath);
        bool openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;

        try
        {
            if (openedSource)
            {
                sourceScene = RunWithVendorWarningsMuted(
                    () => EditorSceneManager.OpenScene(
                        definition.sourceScenePath,
                        OpenSceneMode.Additive));
            }

            DisableSourceSceneRenderingSystems(sourceScene);

            List<SourceInstance> instances = CollectSourceInstances(
                sourceScene,
                definition.kitId);

            if (instances.Count == 0)
            {
                throw new InvalidOperationException(
                    definition.displayName +
                    " contains no eligible authored prefab instances.");
            }

            return BuildGoldenSite(definition, instances);
        }
        finally
        {
            if (openedSource && sourceScene.IsValid() && sourceScene.isLoaded)
            {
                EditorSceneManager.CloseScene(sourceScene, true);
            }

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }
        }
    }

    private static List<SourceInstance> CollectSourceInstances(
        Scene sourceScene,
        string kitId)
    {
        List<SourceInstance> result = new List<SourceInstance>();
        HashSet<int> seenRoots = new HashSet<int>();
        GameObject[] roots = sourceScene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] transforms =
                roots[rootIndex].GetComponentsInChildren<Transform>(true);

            for (int index = 0; index < transforms.Length; index++)
            {
                Transform current = transforms[index];

                if (current == null ||
                    !PrefabUtility.IsOutermostPrefabInstanceRoot(current.gameObject) ||
                    !seenRoots.Add(current.gameObject.GetInstanceID()))
                {
                    continue;
                }

                string sourcePath =
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                        current.gameObject);
                string sourceName =
                    Path.GetFileNameWithoutExtension(sourcePath ?? string.Empty);

                if (string.IsNullOrWhiteSpace(sourcePath) ||
                    IsExcludedSource(kitId, sourceName))
                {
                    continue;
                }

                GameObject sourcePrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

                if (sourcePrefab == null ||
                    !TryCalculateRendererBounds(current.gameObject, out Bounds bounds))
                {
                    continue;
                }

                result.Add(
                    new SourceInstance
                    {
                        sceneObject = current.gameObject,
                        sourcePrefab = sourcePrefab,
                        sourcePath = sourcePath,
                        sourceName = sourceName,
                        worldBounds = bounds
                    });
            }
        }

        return result
            .OrderBy(instance => instance.sourcePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(instance => instance.sceneObject.transform.position.x)
            .ThenBy(instance => instance.sceneObject.transform.position.z)
            .ToList();
    }

    public static bool IsExcludedSource(string kitId, string sourceName)
    {
        string compact =
            (sourceName ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty);

        for (int index = 0; index < ExcludedSourceTokens.Length; index++)
        {
            if (string.Equals(
                    ExcludedSourceTokens[index],
                    "mountain",
                    StringComparison.OrdinalIgnoreCase) &&
                MountainOwningPackIds.Contains(kitId ?? string.Empty))
            {
                // note: Mountains are world-terrain context for ordinary sites, but remain authored geometry when the pack's explicit identity and gameplay depend on the mountain itself.
                continue;
            }

            if (compact.IndexOf(
                    ExcludedSourceTokens[index],
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static PackResult BuildGoldenSite(
        PackDefinition definition,
        List<SourceInstance> instances)
    {
        string packFolder = OutputRoot + "/" + definition.kitId;
        string materialFolder = packFolder + "/MaterialAdapters";
        EnsureFolderPath(materialFolder);

        Scene previewScene = EditorSceneManager.NewPreviewScene();

        try
        {
            GameObject root = new GameObject(definition.displayName + " Golden Site");
            SceneManager.MoveGameObjectToScene(root, previewScene);

            Bounds sourceBounds = instances[0].worldBounds;

            for (int index = 1; index < instances.Count; index++)
            {
                sourceBounds.Encapsulate(instances[index].worldBounds);
            }

            Vector3 origin = new Vector3(
                sourceBounds.center.x,
                sourceBounds.min.y,
                sourceBounds.center.z);
            int missingScriptsRemoved = 0;
            int repairedMaterialSlots = 0;
            int unresolvedMaterialSlots = 0;
            Dictionary<string, Material> materialCache =
                new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < instances.Count; index++)
            {
                SourceInstance source = instances[index];
                GameObject clone =
                    RunWithVendorWarningsMuted(
                        () => PrefabUtility.InstantiatePrefab(
                            source.sourcePrefab,
                            previewScene) as GameObject);

                if (clone == null)
                    continue;

                clone.name = source.sceneObject.name;
                clone.transform.SetParent(root.transform, false);
                clone.transform.localPosition =
                    source.sceneObject.transform.position - origin;
                clone.transform.localRotation = source.sceneObject.transform.rotation;
                clone.transform.localScale = source.sceneObject.transform.lossyScale;
                missingScriptsRemoved += RemoveMissingScriptsRecursively(clone);
                bool repairedLodOwnership =
                    RepairDuplicateLodOwnership(clone);

                if (repairedLodOwnership &&
                    PrefabUtility.IsOutermostPrefabInstanceRoot(clone))
                {
                    // note: A malformed vendor prefab is flattened only in the generated candidate so its repaired LOD ownership loads cleanly at review/runtime without altering the imported source asset.
                    PrefabUtility.UnpackPrefabInstance(
                        clone,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }

                RepairMaterials(
                    clone,
                    definition.forceUrpConversion,
                    materialFolder,
                    materialCache,
                    ref repairedMaterialSlots,
                    ref unresolvedMaterialSlots);
            }

            Bounds localBounds = CalculateLocalRendererBounds(root);
            GameObject sockets = new GameObject("Sockets");
            sockets.transform.SetParent(root.transform, false);
            List<string> socketPaths = CreateSiteSockets(sockets.transform, localBounds);
            string stableId = "yq_site_" + definition.kitId;

            YQWorldAssemblyDescriptor assembly =
                root.AddComponent<YQWorldAssemblyDescriptor>();
            assembly.ConfigureExtractedCandidate(
                stableId,
                definition.kitId,
                YQWorldAssemblyKind.Site,
                definition.displayName,
                BuildCompositionSignature(instances),
                1,
                localBounds.center,
                localBounds.size,
                localBounds.size + new Vector3(8f, 4f, 8f),
                Vector3.forward,
                string.Empty,
                new[]
                {
                    "authored-site",
                    definition.kitId,
                    definition.siteKind.ToString().ToLowerInvariant()
                });
            assembly.ConfigureConnectionSockets(socketPaths);

            YQWorldAuthoredSiteDescriptor site =
                root.AddComponent<YQWorldAuthoredSiteDescriptor>();
            site.Configure(
                definition.kitId,
                definition.siteKind,
                definition.sourceScenePath,
                origin,
                instances.Count,
                repairedMaterialSlots,
                unresolvedMaterialSlots,
                localBounds.center,
                localBounds.size,
                socketPaths);

            string prefabPath = packFolder + "/" + stableId + ".prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            if (saved == null)
            {
                throw new InvalidOperationException(
                    "Failed to save golden site prefab for " + definition.displayName + ".");
            }

            string reviewScenePath =
                packFolder + "/YQ_" + definition.kitId + "_GoldenSiteReview.unity";
            BuildReviewScene(prefabPath, reviewScenePath, definition.displayName);

            return new PackResult
            {
                kitId = definition.kitId,
                displayName = definition.displayName,
                sourceScenePath = definition.sourceScenePath,
                succeeded = true,
                sourceInstanceCount = instances.Count,
                repairedMaterialSlotCount = repairedMaterialSlots,
                unresolvedMaterialSlotCount = unresolvedMaterialSlots,
                removedMissingScriptCount = missingScriptsRemoved,
                prefabPath = prefabPath,
                reviewScenePath = reviewScenePath
            };
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static void MarkCatalogBuildResult(PackResult result)
    {
        YQAuthoredSiteSourceCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteSourceCatalog>(
                YQAuthoredSiteSourceDiscovery.CatalogPath);

        if (catalog == null || result == null)
            return;

        catalog.MarkCandidateBuilt(
            result.sourceScenePath,
            result.prefabPath,
            result.reviewScenePath);
        EditorUtility.SetDirty(catalog);
    }

    private static void MarkCatalogBuildFailure(
        string sourceScenePath,
        string failure)
    {
        YQAuthoredSiteSourceCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteSourceCatalog>(
                YQAuthoredSiteSourceDiscovery.CatalogPath);

        if (catalog == null)
            return;

        catalog.MarkBuildFailed(sourceScenePath, failure);
        EditorUtility.SetDirty(catalog);
    }

    private static void RepairMaterials(
        GameObject root,
        bool forceUrpConversion,
        string materialFolder,
        Dictionary<string, Material> materialCache,
        ref int repairedSlotCount,
        ref int unresolvedSlotCount)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (renderer == null ||
                renderer.GetType().Name.IndexOf("VFX", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int slot = 0; slot < materials.Length; slot++)
            {
                Material source = materials[slot];

                if (source == null)
                {
                    // note: Empty renderer slots are intentional in many vendor prefabs and are not failed URP conversions.
                    continue;
                }

                if (!forceUrpConversion && IsUrpCompatible(source))
                    continue;

                Material replacement =
                    YQRuntimeUrpMaterialRepair
                        .ResolveEditorMaterialForRuntimeBake(source, renderer);

                if (replacement != null && !IsUrpCompatible(replacement))
                {
                    replacement = null;
                }

                if (replacement == null)
                {
                    replacement = GetOrCreateUrpAdapter(
                        source,
                        renderer,
                        materialFolder,
                        materialCache);
                }

                if (replacement == null)
                {
                    unresolvedSlotCount++;
                    continue;
                }

                if (replacement != source)
                {
                    materials[slot] = replacement;
                    repairedSlotCount++;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private static Material GetOrCreateUrpAdapter(
        Material source,
        Renderer renderer,
        string materialFolder,
        Dictionary<string, Material> materialCache)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        string identity = sourcePath + "|" + source.name + "|" + renderer.GetType().Name;
        string fileName =
            SanitizeFileName(source.name) + "_" + StableHash(identity) + "_URP.mat";
        string adapterPath = materialFolder + "/" + fileName;

        if (materialCache.TryGetValue(adapterPath, out Material cached))
            return cached;

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(adapterPath);

        if (existing != null)
        {
            materialCache[adapterPath] = existing;
            return existing;
        }

        Material converted = renderer is ParticleSystemRenderer
            ? CreateUrpParticleAdapter(source)
            : YQRuntimeUrpMaterialRepair.CreateEditorUrpLitMaterial(source, renderer);

        if (converted == null)
            return null;

        // note: Material adapters are project-owned assets; imported source materials and their GUIDs remain untouched and upgradeable.
        AssetDatabase.CreateAsset(converted, adapterPath);
        materialCache[adapterPath] = converted;
        return converted;
    }

    private static Material CreateUrpParticleAdapter(Material source)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (shader == null)
            return null;

        Material converted = new Material(shader)
        {
            name = source.name + "_YQ_URP_Particle"
        };

        // note: Particle adapters preserve the primary authored texture and tint while moving HDRP-only effects onto URP's supported transparent particle shader.
        Texture texture = null;

        if (source.HasProperty("_BaseMap"))
            texture = source.GetTexture("_BaseMap");
        else if (source.HasProperty("_MainTex"))
            texture = source.GetTexture("_MainTex");

        if (texture != null && converted.HasProperty("_BaseMap"))
            converted.SetTexture("_BaseMap", texture);

        Color tint = Color.white;

        if (source.HasProperty("_BaseColor"))
            tint = source.GetColor("_BaseColor");
        else if (source.HasProperty("_Color"))
            tint = source.GetColor("_Color");

        if (converted.HasProperty("_BaseColor"))
            converted.SetColor("_BaseColor", tint);

        if (converted.HasProperty("_Surface"))
            converted.SetFloat("_Surface", 1f);
        if (converted.HasProperty("_ZWrite"))
            converted.SetFloat("_ZWrite", 0f);

        converted.renderQueue = 3000;
        return converted;
    }

    private static bool IsUrpCompatible(Material material)
    {
        if (material == null || material.shader == null)
            return false;

        string shaderName = material.shader.name ?? string.Empty;
        return shaderName.IndexOf("Universal Render Pipeline", StringComparison.OrdinalIgnoreCase) >= 0 ||
               shaderName.IndexOf("URP", StringComparison.OrdinalIgnoreCase) >= 0 ||
               shaderName.IndexOf("TextMeshPro", StringComparison.OrdinalIgnoreCase) >= 0 ||
               shaderName.IndexOf("UI/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int RemoveMissingScriptsRecursively(GameObject root)
    {
        int removed = 0;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < transforms.Length; index++)
        {
            GameObject current = transforms[index].gameObject;
            int missing =
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current);

            if (missing <= 0)
                continue;

            // note: Only broken components are removed from generated wrappers; renderers, valid scripts, materials, and vendor source assets remain intact.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(current);
            removed += missing;
        }

        return removed;
    }

    private static void DisableSourceSceneRenderingSystems(Scene sourceScene)
    {
        GameObject[] roots = sourceScene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            // note: The additively opened vendor scene is a disposable extraction source; disabling its live render systems prevents HDRP components from entering the URP RenderGraph.
            Camera[] cameras = roots[rootIndex].GetComponentsInChildren<Camera>(true);
            Light[] lights = roots[rootIndex].GetComponentsInChildren<Light>(true);
            ReflectionProbe[] probes = roots[rootIndex].GetComponentsInChildren<ReflectionProbe>(true);

            for (int index = 0; index < cameras.Length; index++)
                cameras[index].enabled = false;
            for (int index = 0; index < lights.Length; index++)
                lights[index].enabled = false;
            for (int index = 0; index < probes.Length; index++)
                probes[index].enabled = false;
        }
    }

    private static bool RepairDuplicateLodOwnership(GameObject root)
    {
        LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true)
            .OrderByDescending(group => GetTransformDepth(group.transform))
            .ToArray();
        HashSet<Renderer> claimedRenderers = new HashSet<Renderer>();
        bool repaired = false;

        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            LODGroup group = groups[groupIndex];
            LOD[] lods = group.GetLODs();
            bool changed = false;

            for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                Renderer[] sourceRenderers = lods[lodIndex].renderers;
                List<Renderer> uniqueRenderers = new List<Renderer>(sourceRenderers.Length);

                for (int rendererIndex = 0; rendererIndex < sourceRenderers.Length; rendererIndex++)
                {
                    Renderer renderer = sourceRenderers[rendererIndex];

                    if (renderer != null && claimedRenderers.Add(renderer))
                    {
                        uniqueRenderers.Add(renderer);
                    }
                    else
                    {
                        changed = true;
                    }
                }

                lods[lodIndex].renderers = uniqueRenderers.ToArray();
            }

            if (changed)
            {
                // note: Unity permits one renderer owner; the deepest authored LOD group wins so generated wrappers cannot retain invalid overlapping vendor ownership.
                group.SetLODs(lods);
                group.RecalculateBounds();
                repaired = true;
            }
        }

        return repaired;
    }

    private static int GetTransformDepth(Transform transform)
    {
        int depth = 0;

        while (transform != null)
        {
            depth++;
            transform = transform.parent;
        }

        return depth;
    }

    private static List<string> CreateSiteSockets(
        Transform socketRoot,
        Bounds bounds)
    {
        string[] names =
        {
            "Connection_North", "Connection_East",
            "Connection_South", "Connection_West"
        };
        Vector3[] directions =
        {
            Vector3.forward, Vector3.right, Vector3.back, Vector3.left
        };
        List<string> result = new List<string>();

        for (int index = 0; index < names.Length; index++)
        {
            GameObject socket = new GameObject(names[index]);
            socket.transform.SetParent(socketRoot, false);
            socket.transform.localPosition = bounds.center + new Vector3(
                directions[index].x * bounds.extents.x,
                -bounds.center.y,
                directions[index].z * bounds.extents.z);
            socket.transform.localRotation =
                Quaternion.LookRotation(directions[index], Vector3.up);
            result.Add("Sockets/" + names[index]);
        }

        return result;
    }

    private static void BuildReviewScene(
        string prefabPath,
        string reviewScenePath,
        string displayName)
    {
        Scene existing = SceneManager.GetSceneByPath(reviewScenePath);

        if (existing.IsValid() && existing.isLoaded)
        {
            EditorSceneManager.CloseScene(existing, true);
        }

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Additive);
        scene.name = "YQ_" + displayName.Replace(" ", string.Empty) + "_GoldenSiteReview";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;

        if (instance == null)
        {
            EditorSceneManager.CloseScene(scene, true);
            throw new InvalidOperationException(
                "Failed to instantiate golden site review prefab " + prefabPath + ".");
        }

        instance.transform.position = Vector3.zero;
        GameObject lightObject = new GameObject("Review Directional Light");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;

        // note: Review scenes contain exactly one complete site so composition and material failures cannot hide among unrelated packs.
        EditorSceneManager.SaveScene(scene, reviewScenePath);
        EditorSceneManager.CloseScene(scene, true);
    }

    private static bool TryCalculateRendererBounds(
        GameObject root,
        out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        bounds = new Bounds(root.transform.position, Vector3.zero);

        for (int index = 0; index < renderers.Length; index++)
        {
            if (renderers[index] == null)
                continue;

            if (!found)
            {
                bounds = renderers[index].bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
        }

        return found;
    }

    private static Bounds CalculateLocalRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Matrix4x4 worldToLocal = root.transform.worldToLocalMatrix;
        bool found = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Bounds world = renderers[rendererIndex].bounds;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = worldToLocal.MultiplyPoint3x4(
                            world.center + Vector3.Scale(
                                world.extents,
                                new Vector3(x, y, z)));

                        if (!found)
                        {
                            result = new Bounds(corner, Vector3.zero);
                            found = true;
                        }
                        else
                        {
                            result.Encapsulate(corner);
                        }
                    }
                }
            }
        }

        return result;
    }

    private static string BuildCompositionSignature(
        List<SourceInstance> instances)
    {
        StringBuilder canonical = new StringBuilder();

        foreach (IGrouping<string, SourceInstance> group in
                 instances.GroupBy(instance => instance.sourceName)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            canonical.Append(group.Key.ToLowerInvariant());
            canonical.Append(':');
            canonical.Append(group.Count());
            canonical.Append('|');
        }

        return "fnv1a32_" + StableHash(canonical.ToString());
    }

    private static string StableHash(string value)
    {
        uint hash = 2166136261u;

        for (int index = 0; index < (value ?? string.Empty).Length; index++)
        {
            hash ^= value[index];
            hash *= 16777619u;
        }

        return hash.ToString("x8");
    }

    private static string SanitizeFileName(string value)
    {
        StringBuilder result = new StringBuilder();
        string source = string.IsNullOrWhiteSpace(value) ? "Material" : value;

        for (int index = 0; index < source.Length && result.Length < 48; index++)
        {
            char character = source[index];
            result.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return result.ToString();
    }

    private static T RunWithVendorWarningsMuted<T>(Func<T> operation)
    {
        LogType previousFilter = Debug.unityLogger.filterLogType;

        try
        {
            // note: Known malformed vendor LOD warnings are muted only while loading imported source data; errors remain visible and generated copies are repaired immediately afterward.
            Debug.unityLogger.filterLogType = LogType.Error;
            return operation();
        }
        finally
        {
            Debug.unityLogger.filterLogType = previousFilter;
        }
    }

    private static void FinishBatch(bool cancelled)
    {
        EditorApplication.update -= ProcessNextPack;
        EditorUtility.ClearProgressBar();

        List<PackResult> results = _results ?? new List<PackResult>();
        int succeeded = results.Count(result => result.succeeded);
        int failed = results.Count - succeeded;
        bool completedSuccessfully =
            !cancelled && failed == 0 && succeeded == _totalPackCount;
        Action<bool> completion = _completion;
        WriteReport(results, cancelled);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[YQAllPackAuthoredSiteBatchBuilder] ALL-PACK AUTHORING " +
            (cancelled ? "CANCELLED" : "COMPLETE") + "\n" +
            "Configured packs: " + _totalPackCount + "\n" +
            "Succeeded: " + succeeded + "\n" +
            "Failed: " + failed + "\n" +
            "Report: " + ReportPath + "\n" +
            "Release eligible: 0 (per-pack visual review required)");

        _pending = null;
        _results = null;
        _totalPackCount = 0;
        _completion = null;

        // note: A targeted review repair can continue directly into streaming compilation only after the replacement authored candidate saved successfully.
        completion?.Invoke(completedSuccessfully);
    }

    private static void WriteReport(List<PackResult> results, bool cancelled)
    {
        EnsureFolderPath(OutputRoot);
        StringBuilder report = new StringBuilder();
        report.AppendLine("# YourQuest All-Pack Authored Site Batch");
        report.AppendLine();
        report.AppendLine("Status: " + (cancelled ? "Cancelled" : "Complete"));
        report.AppendLine();
        report.AppendLine("| Pack | Result | Instances | URP repairs | Unresolved materials | Broken scripts removed | Review scene |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---|");

        for (int index = 0; index < results.Count; index++)
        {
            PackResult result = results[index];
            report.Append("| ").Append(result.displayName)
                .Append(" | ").Append(result.succeeded ? "Candidate ready" : "Failed")
                .Append(" | ").Append(result.sourceInstanceCount)
                .Append(" | ").Append(result.repairedMaterialSlotCount)
                .Append(" | ").Append(result.unresolvedMaterialSlotCount)
                .Append(" | ").Append(result.removedMissingScriptCount)
                .Append(" | ").Append(result.reviewScenePath).AppendLine(" |");

            if (!result.succeeded && !string.IsNullOrWhiteSpace(result.failure))
            {
                report.AppendLine();
                report.AppendLine("- **" + result.displayName + " failure:** " + result.failure);
            }
        }

        report.AppendLine();
        report.AppendLine("All outputs are extracted candidates. Visual approval is mandatory before runtime registration.");
        File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
    }

    private static PackDefinition Pack(
        string kitId,
        string displayName,
        YQAuthoredSiteKind siteKind,
        string sourceScenePath,
        bool forceUrpConversion)
    {
        return new PackDefinition
        {
            kitId = kitId,
            displayName = displayName,
            siteKind = siteKind,
            sourceScenePath = sourceScenePath,
            forceUrpConversion = forceUrpConversion
        };
    }

    private static void EnsureFolderPath(string folderPath)
    {
        string normalized = folderPath.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        string current = segments[0];

        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[index]);
            }

            current = next;
        }
    }
}
