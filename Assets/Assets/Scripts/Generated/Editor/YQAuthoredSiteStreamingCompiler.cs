using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQAuthoredSiteStreamingCompiler
{
    private const string OutputRoot =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/StreamingSites";

    private const string ReportPath =
        OutputRoot + "/YQ_AuthoredSiteStreamingReport.md";

    // note: Runtime cloning is atomic per prefab; keeping authored chunks small prevents one cell from allocating and activating thousands of roots in a single frame.
    private const int MaximumInstancesPerCell = 256;

    private static Queue<YQAuthoredSiteSourceRecord> _pendingSites;
    private static SiteWork _currentSite;
    private static List<SiteResult> _results;
    private static int _totalSiteCount;
    private static Queue<string> _pendingLodRepairPaths;
    private static int _lodRepairTotal;
    private static int _lodRepairChanged;
    private static int _lodRepairFailed;

    private sealed class CellWork
    {
        public int gridX;
        public int gridZ;
        public int partIndex;
        public Vector3 authoredOffset;
        public List<GameObject> sourceObjects = new List<GameObject>();
    }

    private sealed class SiteWork
    {
        public YQAuthoredSiteSourceRecord record;
        public GameObject sourceSitePrefab;
        public GameObject loadedRoot;
        public string outputFolder;
        public List<CellWork> cells = new List<CellWork>();
        public int nextCellIndex;
        public List<YQAuthoredSiteStreamingCellRecord> compiledCells =
            new List<YQAuthoredSiteStreamingCellRecord>();
    }

    private sealed class SiteResult
    {
        public string displayName = string.Empty;
        public int sourceInstanceCount;
        public int cellCount;
        public string manifestPath = string.Empty;
        public string reviewScenePath = string.Empty;
        public string failure = string.Empty;
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Streaming/Compile All Authored Sites Into Streamable Cells")]
    public static void CompileAllAuthoredSitesIntoStreamableCells()
    {
        List<YQAuthoredSiteSourceRecord> buildable = GetBuildableCandidates();

        if (buildable.Count == 0)
        {
            Debug.LogWarning(
                "[YQAuthoredSiteStreamingCompiler] No built authored-site candidates are available for cell compilation.");
            return;
        }

        StartCompilation(buildable);
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Streaming/Compile Smallest Authored Site Pilot")]
    public static void CompileSmallestAuthoredSitePilot()
    {
        List<YQAuthoredSiteSourceRecord> buildable = GetBuildableCandidates();

        if (buildable.Count == 0)
        {
            Debug.LogWarning(
                "[YQAuthoredSiteStreamingCompiler] No built authored-site candidates are available for a pilot.");
            return;
        }

        // note: The pilot selects the smallest generated candidate so the complete cell/reconstruction contract is validated before another large batch is authorized.
        YQAuthoredSiteSourceRecord pilot = buildable
            .OrderBy(record => GetAssetFileLength(record.generatedPrefabPath))
            .ThenBy(record => record.kitId, StringComparer.OrdinalIgnoreCase)
            .First();
        StartCompilation(new List<YQAuthoredSiteSourceRecord> { pilot });
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Compile Missing Streaming Sites")]
    public static void CompileMissingProductionQueueSites()
    {
        YQWorldPackProductionCatalog productionCatalog =
            YQWorldPackProductionQueueBuilder.SyncQueue();

        if (productionCatalog == null)
            return;

        HashSet<string> pendingKitIds = new HashSet<string>(
            productionCatalog.Records
                .Where(record => record != null &&
                    record.state ==
                    YQWorldPackProductionState.NeedsStreamingCompilation)
                .Select(record => record.kitId),
            StringComparer.OrdinalIgnoreCase);
        List<YQAuthoredSiteSourceRecord> pending = GetBuildableCandidates()
            .Where(record => pendingKitIds.Contains(record.kitId))
            .ToList();

        if (pending.Count == 0)
        {
            Debug.Log(
                "[YQAuthoredSiteStreamingCompiler] No production-queue sites require streaming compilation.");
            return;
        }

        // note: Queue-aware compilation never rebuilds reviewed or already compiled packs, preserving approvals and avoiding unnecessary multi-gigabyte prefab work.
        StartCompilation(pending);
    }

    public static bool RecompileAuthoredSite(string kitId)
    {
        YQAuthoredSiteSourceRecord record = GetBuildableCandidates()
            .FirstOrDefault(candidate => string.Equals(
                candidate.kitId,
                kitId,
                StringComparison.OrdinalIgnoreCase));

        if (record == null)
        {
            Debug.LogError(
                "[YQAuthoredSiteStreamingCompiler] No rebuilt authored candidate is available for " +
                kitId + ".");
            return false;
        }

        // note: A rejected review recompiles only its own deterministic cells and manifest, preserving every unrelated pack and approval.
        StartCompilation(new List<YQAuthoredSiteSourceRecord> { record });
        return true;
    }

    public static bool TryRepairGeneratedContextCells(
        string kitId,
        string displayName,
        string manifestPath,
        string reviewScenePath)
    {
        YQAuthoredSiteStreamingManifest manifest =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                manifestPath);

        if (manifest == null)
            return false;

        List<YQAuthoredSiteStreamingCellRecord> retained =
            new List<YQAuthoredSiteStreamingCellRecord>();
        int removed = 0;

        for (int index = 0; index < manifest.Cells.Count; index++)
        {
            YQAuthoredSiteStreamingCellRecord cell = manifest.Cells[index];

            if (IsExcludedContextCell(kitId, cell))
            {
                removed++;
                continue;
            }

            retained.Add(cell);
        }

        if (removed == 0)
            return false;

        // note: Context-only cells are already deterministic isolation boundaries, so repairing their manifest does not require reopening a multi-gigabyte authored source scene.
        manifest.ConfigureCandidate(
            manifest.SemanticStyleKey,
            manifest.SiteKind,
            manifest.SourceScenePath,
            manifest.SourceSignature,
            manifest.SourceSitePrefab,
            retained);
        EditorUtility.SetDirty(manifest);
        string siteOutputFolder = Path.GetDirectoryName(manifestPath)
            ?.Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(siteOutputFolder))
            return false;

        BuildReviewScene(displayName, retained, reviewScenePath);
        int staleRemoved = RemoveStaleGeneratedCells(
            siteOutputFolder,
            retained);
        AssetDatabase.SaveAssetIfDirty(manifest);
        YQWorldPackProductionQueueBuilder.SyncQueue();
        Debug.Log(
            "[YQAuthoredSiteStreamingCompiler] LIGHTWEIGHT CONTEXT REPAIR COMPLETE\n" +
            "Site: " + displayName + "\n" +
            "Context cells removed: " + removed + "\n" +
            "Stale generated cells removed: " + staleRemoved + "\n" +
            "Remaining cells: " + retained.Count + "\n" +
            "Release eligible: 0 (visual review required)");
        return true;
    }

    private static bool IsExcludedContextCell(
        string kitId,
        YQAuthoredSiteStreamingCellRecord cell)
    {
        if (cell == null ||
            cell.CellPrefab == null ||
            cell.SourceInstanceCount != 1)
        {
            return false;
        }

        string cellPath = AssetDatabase.GetAssetPath(cell.CellPrefab);
        string[] dependencies = AssetDatabase.GetDependencies(cellPath, true);

        for (int index = 0; index < dependencies.Length; index++)
        {
            string dependency = dependencies[index];

            if (!dependency.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string sourceName = Path.GetFileNameWithoutExtension(dependency);

            if (YQAllPackAuthoredSiteBatchBuilder.IsExcludedSource(
                    kitId,
                    sourceName))
            {
                // note: Only a singleton cell whose prefab dependency is explicitly classified as context may be removed without source re-extraction.
                return true;
            }
        }

        return false;
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Archived Tools/Recovery/Repair Generated Streaming LOD Contracts")]
    public static void RepairGeneratedStreamingLodContracts()
    {
        if (_pendingLodRepairPaths != null)
        {
            Debug.LogWarning(
                "[YQAuthoredSiteStreamingCompiler] A generated-cell LOD repair is already running.");
            return;
        }

        string[] manifestGuids = AssetDatabase.FindAssets(
            "t:YQAuthoredSiteStreamingManifest",
            new[] { OutputRoot });
        HashSet<string> prefabPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < manifestGuids.Length; index++)
        {
            YQAuthoredSiteStreamingManifest manifest =
                AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                    AssetDatabase.GUIDToAssetPath(manifestGuids[index]));

            if (manifest == null)
                continue;

            for (int cellIndex = 0;
                 cellIndex < manifest.Cells.Count;
                 cellIndex++)
            {
                GameObject prefab = manifest.Cells[cellIndex].CellPrefab;
                string path = prefab != null
                    ? AssetDatabase.GetAssetPath(prefab)
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(path))
                    prefabPaths.Add(path);
            }
        }

        _pendingLodRepairPaths = new Queue<string>(
            prefabPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        _lodRepairTotal = _pendingLodRepairPaths.Count;
        _lodRepairChanged = 0;
        _lodRepairFailed = 0;

        if (_lodRepairTotal == 0)
        {
            _pendingLodRepairPaths = null;
            Debug.LogWarning(
                "[YQAuthoredSiteStreamingCompiler] No generated streaming cells are available for LOD repair.");
            return;
        }

        // note: One prefab is repaired per editor update so thousands of imported LOD groups cannot freeze the editor in one blocking operation.
        EditorApplication.update += ProcessNextGeneratedLodRepair;
        Debug.Log(
            "[YQAuthoredSiteStreamingCompiler] Started generated streaming LOD repair for " +
            _lodRepairTotal + " cell prefabs.");
    }

    private static void ProcessNextGeneratedLodRepair()
    {
        if (_pendingLodRepairPaths == null ||
            _pendingLodRepairPaths.Count == 0)
        {
            FinishGeneratedLodRepair(false);
            return;
        }

        int completed = _lodRepairTotal - _pendingLodRepairPaths.Count;

        if (EditorUtility.DisplayCancelableProgressBar(
                "YourQuest Streaming LOD Repair",
                "Repairing cell " + (completed + 1) + " of " +
                _lodRepairTotal,
                completed / (float)Mathf.Max(1, _lodRepairTotal)))
        {
            FinishGeneratedLodRepair(true);
            return;
        }

        string prefabPath = _pendingLodRepairPaths.Dequeue();
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);

            if (root != null && RepairDuplicateLodOwnership(root))
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                _lodRepairChanged++;
            }
        }
        catch (Exception exception)
        {
            _lodRepairFailed++;
            Debug.LogError(
                "[YQAuthoredSiteStreamingCompiler] LOD repair failed for " +
                prefabPath + ": " + exception.Message);
        }
        finally
        {
            if (root != null)
                PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void FinishGeneratedLodRepair(bool cancelled)
    {
        EditorApplication.update -= ProcessNextGeneratedLodRepair;
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        int completed = _lodRepairTotal -
            (_pendingLodRepairPaths != null
                ? _pendingLodRepairPaths.Count
                : 0);
        Debug.Log(
            "[YQAuthoredSiteStreamingCompiler] GENERATED STREAMING LOD REPAIR " +
            (cancelled ? "CANCELLED" : "COMPLETE") + "\n" +
            "Processed cells: " + completed + "/" + _lodRepairTotal + "\n" +
            "Changed cells: " + _lodRepairChanged + "\n" +
            "Failed cells: " + _lodRepairFailed);
        _pendingLodRepairPaths = null;
        _lodRepairTotal = 0;
        _lodRepairChanged = 0;
        _lodRepairFailed = 0;
    }

    private static List<YQAuthoredSiteSourceRecord> GetBuildableCandidates()
    {
        YQAuthoredSiteSourceCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteSourceCatalog>(
                YQAuthoredSiteSourceDiscovery.CatalogPath);

        if (catalog == null)
        {
            Debug.LogError(
                "[YQAuthoredSiteStreamingCompiler] Authored-site catalog is missing. Run authored pack detection first.");
            return new List<YQAuthoredSiteSourceRecord>();
        }

        return catalog.Records
            .Where(IsBuildableCandidate)
            .OrderBy(record => record.kitId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void StartCompilation(
        List<YQAuthoredSiteSourceRecord> buildable)
    {
        if (_pendingSites != null || _currentSite != null)
        {
            Debug.LogWarning(
                "[YQAuthoredSiteStreamingCompiler] A streaming-cell compilation is already running.");
            return;
        }

        EnsureFolderPath(OutputRoot);
        _pendingSites = new Queue<YQAuthoredSiteSourceRecord>(buildable);
        _results = new List<SiteResult>();
        _totalSiteCount = buildable.Count;

        // note: One cell is emitted per editor update so large packs remain cancellable and never monopolize the editor for an entire all-pack pass.
        EditorApplication.update += ProcessNextStep;
        Debug.Log(
            "[YQAuthoredSiteStreamingCompiler] Started streaming-cell compilation for " +
            _totalSiteCount + " authored sites.");
    }

    private static long GetAssetFileLength(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
            return long.MaxValue;

        return new FileInfo(assetPath).Length;
    }

    private static bool IsBuildableCandidate(YQAuthoredSiteSourceRecord record)
    {
        return record != null &&
               !string.IsNullOrWhiteSpace(record.generatedPrefabPath) &&
               AssetDatabase.LoadAssetAtPath<GameObject>(record.generatedPrefabPath) != null &&
               (record.state == YQAuthoredSiteSourceState.CandidateBuilt ||
                record.state == YQAuthoredSiteSourceState.Approved);
    }

    private static void ProcessNextStep()
    {
        try
        {
            int completedSites = _results != null ? _results.Count : 0;
            string currentName = _currentSite != null
                ? _currentSite.record.displayName
                : "Preparing next authored site";
            float progress = _totalSiteCount > 0
                ? (float)completedSites / _totalSiteCount
                : 0f;

            if (EditorUtility.DisplayCancelableProgressBar(
                    "YourQuest Authored Site Streaming",
                    currentName,
                    progress))
            {
                FinishBatch(true);
                return;
            }

            if (_currentSite == null)
            {
                if (_pendingSites == null || _pendingSites.Count == 0)
                {
                    FinishBatch(false);
                    return;
                }

                BeginSite(_pendingSites.Dequeue());
                return;
            }

            if (_currentSite.nextCellIndex < _currentSite.cells.Count)
            {
                BuildCell(
                    _currentSite,
                    _currentSite.cells[_currentSite.nextCellIndex]);
                _currentSite.nextCellIndex++;
                return;
            }

            FinishCurrentSite();
        }
        catch (Exception exception)
        {
            string displayName = _currentSite != null
                ? _currentSite.record.displayName
                : "Unknown authored site";
            _results.Add(
                new SiteResult
                {
                    displayName = displayName,
                    failure = exception.Message
                });
            Debug.LogException(exception);
            DisposeCurrentSite();
        }
    }

    private static void BeginSite(YQAuthoredSiteSourceRecord record)
    {
        GameObject loadedRoot =
            PrefabUtility.LoadPrefabContents(record.generatedPrefabPath);

        if (loadedRoot == null)
            throw new InvalidOperationException(
                "Could not load authored site candidate " +
                record.generatedPrefabPath + ".");

        YQWorldAuthoredSiteDescriptor site =
            loadedRoot.GetComponent<YQWorldAuthoredSiteDescriptor>();

        if (site == null)
        {
            PrefabUtility.UnloadPrefabContents(loadedRoot);
            throw new InvalidOperationException(
                record.displayName + " has no authored-site descriptor.");
        }

        NormalizeGeneratedAdapterSurfaces(loadedRoot);

        string outputFolder = OutputRoot + "/" + record.kitId;
        EnsureFolderPath(outputFolder);
        EnsureFolderPath(outputFolder + "/Cells");

        List<GameObject> sourceObjects = new List<GameObject>();

        for (int index = 0; index < loadedRoot.transform.childCount; index++)
        {
            GameObject child = loadedRoot.transform.GetChild(index).gameObject;

            if (child.name.Equals("Sockets", StringComparison.OrdinalIgnoreCase))
                continue;

            sourceObjects.Add(child);
        }

        if (sourceObjects.Count == 0)
        {
            PrefabUtility.UnloadPrefabContents(loadedRoot);
            throw new InvalidOperationException(
                record.displayName + " has no streamable authored instances.");
        }

        float cellSize = GetCellSize(site.SiteKind);
        Vector3 minimum = site.LocalBoundsCenter - site.LocalBoundsSize * 0.5f;
        Dictionary<string, List<GameObject>> buckets =
            new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);

        for (int index = 0; index < sourceObjects.Count; index++)
        {
            Vector3 position = sourceObjects[index].transform.localPosition;
            int gridX = Mathf.FloorToInt((position.x - minimum.x) / cellSize);
            int gridZ = Mathf.FloorToInt((position.z - minimum.z) / cellSize);
            string key = gridX + ":" + gridZ;

            if (!buckets.TryGetValue(key, out List<GameObject> bucket))
            {
                bucket = new List<GameObject>();
                buckets[key] = bucket;
            }

            bucket.Add(sourceObjects[index]);
        }

        List<CellWork> cells = new List<CellWork>();

        foreach (KeyValuePair<string, List<GameObject>> pair in buckets
                     .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            string[] keyParts = pair.Key.Split(':');
            int gridX = int.Parse(keyParts[0]);
            int gridZ = int.Parse(keyParts[1]);
            int partCount = Mathf.CeilToInt(
                (float)pair.Value.Count / MaximumInstancesPerCell);

            for (int partIndex = 0; partIndex < partCount; partIndex++)
            {
                List<GameObject> members = pair.Value
                    .Skip(partIndex * MaximumInstancesPerCell)
                    .Take(MaximumInstancesPerCell)
                    .ToList();
                cells.Add(
                    new CellWork
                    {
                        gridX = gridX,
                        gridZ = gridZ,
                        partIndex = partIndex,
                        authoredOffset = new Vector3(
                            minimum.x + (gridX + 0.5f) * cellSize,
                            0f,
                            minimum.z + (gridZ + 0.5f) * cellSize),
                        sourceObjects = members
                    });
            }
        }

        _currentSite = new SiteWork
        {
            record = record,
            sourceSitePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(record.generatedPrefabPath),
            loadedRoot = loadedRoot,
            outputFolder = outputFolder,
            cells = cells
        };
    }

    private static float GetCellSize(YQAuthoredSiteKind siteKind)
    {
        switch (siteKind)
        {
            case YQAuthoredSiteKind.Dungeon:
            case YQAuthoredSiteKind.Interior:
                return 64f;
            case YQAuthoredSiteKind.Wilderness:
                return 160f;
            default:
                return 96f;
        }
    }

    private static void BuildCell(SiteWork site, CellWork cell)
    {
        Scene previewScene = EditorSceneManager.NewPreviewScene();

        try
        {
            string cellId =
                "yq_cell_" + site.record.kitId + "_" +
                SignedToken(cell.gridX) + "_" + SignedToken(cell.gridZ) +
                "_p" + cell.partIndex.ToString("00");
            GameObject root = new GameObject(cellId);
            SceneManager.MoveGameObjectToScene(root, previewScene);

            for (int index = 0; index < cell.sourceObjects.Count; index++)
            {
                GameObject source = cell.sourceObjects[index];
                GameObject clone = CloneAuthoredObject(source, previewScene);

                if (clone == null)
                {
                    throw new InvalidOperationException(
                        "Failed to preserve authored object " + source.name +
                        " in " + cellId + ".");
                }

                clone.name = source.name;
                clone.transform.SetParent(root.transform, false);
                clone.transform.localPosition =
                    source.transform.localPosition - cell.authoredOffset;
                clone.transform.localRotation = source.transform.localRotation;
                clone.transform.localScale = source.transform.localScale;
                RemoveMissingScriptsRecursively(clone);
                bool repairedLodOwnership =
                    RepairDuplicateLodOwnership(clone);

                if (repairedLodOwnership &&
                    PrefabUtility.IsOutermostPrefabInstanceRoot(clone))
                {
                    // note: Generated cells bake only malformed nested LOD instances, preventing vendor ownership warnings from recurring during every stream load while retaining healthy prefab links.
                    PrefabUtility.UnpackPrefabInstance(
                        clone,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }
            }

            Bounds bounds = CalculateLocalRendererBounds(
                root,
                out bool hasStructuralFoundation,
                out float structuralFoundationY,
                out float structuralFoundationWeight);
            YQWorldAssemblyDescriptor assembly =
                root.AddComponent<YQWorldAssemblyDescriptor>();
            assembly.ConfigureExtractedCandidate(
                cellId,
                site.record.kitId,
                YQWorldAssemblyKind.StreamingCell,
                site.record.displayName,
                site.record.sourceSignature + ":" + cellId,
                1,
                bounds.center,
                bounds.size,
                bounds.size,
                Vector3.forward,
                string.Empty,
                new[]
                {
                    "authored-streaming-cell",
                    site.record.kitId,
                    site.record.siteKind.ToString().ToLowerInvariant()
                });

            YQWorldStreamingCellDescriptor descriptor =
                root.AddComponent<YQWorldStreamingCellDescriptor>();
            descriptor.Configure(
                cellId,
                site.record.kitId,
                cell.authoredOffset,
                cell.sourceObjects.Count);

            string prefabPath =
                site.outputFolder + "/Cells/" + cellId + ".prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            if (saved == null)
                throw new InvalidOperationException(
                    "Failed to save streaming cell " + prefabPath + ".");

            YQAuthoredSiteStreamingCellRecord record =
                new YQAuthoredSiteStreamingCellRecord();
            record.Configure(
                cellId,
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),
                cell.authoredOffset,
                bounds.center,
                bounds.size,
                cell.sourceObjects.Count,
                hasStructuralFoundation,
                cell.authoredOffset.y + structuralFoundationY,
                structuralFoundationWeight);
            site.compiledCells.Add(record);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static GameObject CloneAuthoredObject(
        GameObject source,
        Scene destinationScene)
    {
        GameObject sourcePrefab =
            PrefabUtility.GetCorrespondingObjectFromSource(source);

        if (sourcePrefab != null)
        {
            GameObject clone =
                PrefabUtility.InstantiatePrefab(
                    sourcePrefab,
                    destinationScene) as GameObject;

            if (clone == null)
                return null;

            PropertyModification[] modifications =
                PrefabUtility.GetPropertyModifications(source);

            if (modifications != null && modifications.Length > 0)
            {
                // note: Nested vendor prefab links and generated URP overrides survive cell extraction instead of being flattened into copied scene objects.
                PrefabUtility.SetPropertyModifications(clone, modifications);
            }

            return clone;
        }

        // note: A genuinely non-prefab authored root is copied only as a fallback; recognized vendor prefab instances always use the nested-prefab path above.
        GameObject fallback = UnityEngine.Object.Instantiate(source);
        fallback.transform.SetParent(null, true);
        SceneManager.MoveGameObjectToScene(fallback, destinationScene);
        return fallback;
    }

    private static void NormalizeGeneratedAdapterSurfaces(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        HashSet<Material> normalized = new HashSet<Material>();

        for (int rendererIndex = 0;
             rendererIndex < renderers.Length;
             rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;

            for (int materialIndex = 0;
                 materialIndex < materials.Length;
                 materialIndex++)
            {
                Material material = materials[materialIndex];

                if (material == null || !normalized.Add(material))
                    continue;

                string path = AssetDatabase.GetAssetPath(material)
                    .Replace('\\', '/');

                if (path.IndexOf(
                        "/GeneratedAssets/WorldAssemblies/AllPacks/",
                        StringComparison.OrdinalIgnoreCase) < 0 ||
                    path.IndexOf(
                        "/MaterialAdapters/",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                YQRuntimeUrpMaterialRepair
                    .NormalizeEditorGeneratedAdapterSurface(material, renderer);
            }
        }
    }

    private static void FinishCurrentSite()
    {
        SiteWork site = _currentSite;
        string manifestPath =
            site.outputFolder + "/YQ_" + site.record.kitId +
            "_StreamingManifest.asset";
        YQAuthoredSiteStreamingManifest manifest =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                manifestPath);

        if (manifest == null)
        {
            manifest = ScriptableObject.CreateInstance<
                YQAuthoredSiteStreamingManifest>();
            AssetDatabase.CreateAsset(manifest, manifestPath);
        }

        manifest.ConfigureCandidate(
            site.record.kitId,
            site.record.siteKind,
            site.record.selectedScenePath,
            site.record.sourceSignature,
            site.sourceSitePrefab,
            site.compiledCells);
        EditorUtility.SetDirty(manifest);

        string reviewScenePath =
            site.outputFolder + "/YQ_" + site.record.kitId +
            "_StreamingReview.unity";
        BuildReviewScene(site.record.displayName, site.compiledCells, reviewScenePath);
        int removedStaleCells = RemoveStaleGeneratedCells(
            site.outputFolder,
            site.compiledCells);
        AssetDatabase.SaveAssets();

        _results.Add(
            new SiteResult
            {
                displayName = site.record.displayName,
                sourceInstanceCount = site.cells.Sum(cell => cell.sourceObjects.Count),
                cellCount = site.compiledCells.Count,
                manifestPath = manifestPath,
                reviewScenePath = reviewScenePath
            });

        Debug.Log(
            "[YQAuthoredSiteStreamingCompiler] STREAMING SITE READY\n" +
            "Site: " + site.record.displayName + "\n" +
            "Authored instances: " +
            site.cells.Sum(cell => cell.sourceObjects.Count) + "\n" +
            "Streaming cells: " + site.compiledCells.Count + "\n" +
            "Stale generated cells removed: " + removedStaleCells + "\n" +
            "Review scene: " + reviewScenePath + "\n" +
            "Release eligible: 0 (visual review required)");

        DisposeCurrentSite();
    }

    private static int RemoveStaleGeneratedCells(
        string siteOutputFolder,
        IReadOnlyList<YQAuthoredSiteStreamingCellRecord> currentCells)
    {
        string cellFolder = siteOutputFolder + "/Cells";
        HashSet<string> retainedPaths = new HashSet<string>(
            currentCells
                .Where(cell => cell != null && cell.CellPrefab != null)
                .Select(cell => AssetDatabase.GetAssetPath(cell.CellPrefab)),
            StringComparer.OrdinalIgnoreCase);
        string[] prefabGuids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { cellFolder });
        int removed = 0;

        for (int index = 0; index < prefabGuids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);

            if (retainedPaths.Contains(path))
                continue;

            // note: Only unreferenced prefabs inside this site's generated Cells folder are removed; source assets and cells retained by the new manifest are never touched.
            if (path.StartsWith(
                    cellFolder + "/",
                    StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.DeleteAsset(path))
            {
                removed++;
            }
        }

        return removed;
    }

    private static void BuildReviewScene(
        string displayName,
        List<YQAuthoredSiteStreamingCellRecord> cells,
        string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool createdScene = !scene.IsValid() || !scene.isLoaded;

        if (createdScene)
        {
            scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
        }

        try
        {
            if (!createdScene)
            {
                GameObject[] existingRoots = scene.GetRootGameObjects();

                for (int index = 0; index < existingRoots.Length; index++)
                {
                    // note: Review scenes are deterministic generated artifacts; rebuilding an open review replaces only its generated contents and preserves the open scene tab.
                    UnityEngine.Object.DestroyImmediate(existingRoots[index]);
                }
            }

            GameObject root = new GameObject(displayName + " Streaming Review");
            SceneManager.MoveGameObjectToScene(root, scene);

            for (int index = 0; index < cells.Count; index++)
            {
                YQAuthoredSiteStreamingCellRecord cell = cells[index];
                GameObject instance = PrefabUtility.InstantiatePrefab(
                    cell.CellPrefab,
                    scene) as GameObject;

                if (instance == null)
                    continue;

                instance.transform.SetParent(root.transform, false);
                instance.transform.localPosition = cell.AuthoredLocalPosition;
            }

            // note: The review scene reconstructs the exact authored location from independent cell prefabs and does not bake lighting or rearrange content.
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException(
                    "Unity could not save streaming review scene " +
                    scenePath + ".");
            }
        }
        finally
        {
            if (createdScene && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static Bounds CalculateLocalRendererBounds(
        GameObject root,
        out bool hasStructuralFoundation,
        out float structuralFoundationY,
        out float structuralFoundationWeight)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
        List<Vector2> foundationSamples = new List<Vector2>();
        hasStructuralFoundation = false;
        structuralFoundationY = 0f;
        structuralFoundationWeight = 0f;

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];

            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 localCenter = root.transform.InverseTransformPoint(
                worldBounds.center);
            Vector3 localSize = root.transform.InverseTransformVector(
                worldBounds.size);
            localSize = new Vector3(
                Mathf.Abs(localSize.x),
                Mathf.Abs(localSize.y),
                Mathf.Abs(localSize.z));
            Bounds localBounds = new Bounds(localCenter, localSize);

            if (!initialized)
            {
                bounds = localBounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(localBounds);
            }
        }

        if (!initialized)
            return new Bounds(Vector3.zero, Vector3.one);

        float lowerBandCeiling = bounds.min.y + Mathf.Min(
            12f,
            Mathf.Max(2f, bounds.size.y * 0.35f));

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];

            if (renderer == null || !renderer.enabled ||
                renderer is ParticleSystemRenderer ||
                renderer is TrailRenderer ||
                renderer is LineRenderer)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;
            Vector3 localCenter = root.transform.InverseTransformPoint(
                worldBounds.center);
            Vector3 localSize = root.transform.InverseTransformVector(
                worldBounds.size);
            localSize = new Vector3(
                Mathf.Abs(localSize.x),
                Mathf.Abs(localSize.y),
                Mathf.Abs(localSize.z));
            float localBottom = localCenter.y - localSize.y * 0.5f;
            float footprint = localSize.x * localSize.z;

            if (float.IsNaN(localBottom) || float.IsInfinity(localBottom) ||
                float.IsNaN(footprint) || float.IsInfinity(footprint) ||
                footprint < 0.36f || localBottom > lowerBandCeiling)
            {
                continue;
            }

            // note: The compiler stores a footprint-weighted lower structural band, allowing floors and walls to outvote loose foliage without letting a single backdrop dominate the result.
            float weight = Mathf.Sqrt(footprint);
            foundationSamples.Add(new Vector2(localBottom, weight));
            structuralFoundationWeight += weight;
        }

        if (foundationSamples.Count > 0 &&
            structuralFoundationWeight > 0f)
        {
            foundationSamples.Sort(
                (left, right) => left.x.CompareTo(right.x));
            float targetWeight = structuralFoundationWeight * 0.5f;
            float accumulatedWeight = 0f;

            for (int index = 0; index < foundationSamples.Count; index++)
            {
                accumulatedWeight += foundationSamples[index].y;

                if (accumulatedWeight < targetWeight)
                    continue;

                structuralFoundationY = foundationSamples[index].x;
                hasStructuralFoundation = true;
                break;
            }
        }

        return bounds;
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

            // note: Only invalid vendor behaviours are removed from generated cells; source assets and valid authored components remain untouched.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(current);
            removed += missing;
        }

        return removed;
    }

    private static bool RepairDuplicateLodOwnership(GameObject root)
    {
        LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true)
            .OrderByDescending(group => GetTransformDepth(group.transform))
            .ToArray();
        HashSet<Renderer> claimed = new HashSet<Renderer>();
        bool repaired = false;

        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            LOD[] lods = groups[groupIndex].GetLODs();
            bool changed = false;

            for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                Renderer[] unique = lods[lodIndex].renderers
                    .Where(renderer => renderer != null && claimed.Add(renderer))
                    .ToArray();
                changed |= unique.Length != lods[lodIndex].renderers.Length;
                lods[lodIndex].renderers = unique;
            }

            changed |= NormalizeLodTransitionHeights(lods);

            if (changed)
            {
                // note: The deepest authored LOD group owns each renderer, and valid descending thresholds ensure Unity accepts the repaired ownership table.
                groups[groupIndex].SetLODs(lods);
                groups[groupIndex].RecalculateBounds();
                repaired = true;
            }
        }

        return repaired;
    }

    private static bool NormalizeLodTransitionHeights(LOD[] lods)
    {
        bool valid = true;
        float previous = 1.0001f;

        for (int index = 0; index < lods.Length; index++)
        {
            float height = lods[index].screenRelativeTransitionHeight;

            if (height < 0f || height > 1f || height >= previous)
            {
                valid = false;
                break;
            }

            previous = height;
        }

        if (valid)
            return false;

        // note: Invalid vendor thresholds receive a deterministic descending sequence on generated wrappers only; imported source prefabs remain untouched.
        for (int index = 0; index < lods.Length; index++)
        {
            lods[index].screenRelativeTransitionHeight =
                Mathf.Pow(0.5f, index + 1);
        }

        return true;
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

    private static string SignedToken(int value)
    {
        return value < 0 ? "n" + Mathf.Abs(value) : "p" + value;
    }

    private static void DisposeCurrentSite()
    {
        if (_currentSite != null && _currentSite.loadedRoot != null)
        {
            PrefabUtility.UnloadPrefabContents(_currentSite.loadedRoot);
        }

        _currentSite = null;
    }

    private static void FinishBatch(bool cancelled)
    {
        EditorApplication.update -= ProcessNextStep;
        EditorUtility.ClearProgressBar();
        DisposeCurrentSite();
        WriteReport(cancelled);
        AssetDatabase.SaveAssets();

        int succeeded = _results != null
            ? _results.Count(result => string.IsNullOrWhiteSpace(result.failure))
            : 0;
        int failed = _results != null
            ? _results.Count(result => !string.IsNullOrWhiteSpace(result.failure))
            : 0;

        Debug.Log(
            "[YQAuthoredSiteStreamingCompiler] STREAMING COMPILATION " +
            (cancelled ? "CANCELLED" : "COMPLETE") + "\n" +
            "Configured sites: " + _totalSiteCount + "\n" +
            "Succeeded: " + succeeded + "\n" +
            "Failed: " + failed + "\n" +
            "Report: " + ReportPath + "\n" +
            "Release eligible: 0 (per-site review required)");

        _pendingSites = null;
        _results = null;
        _totalSiteCount = 0;

        if (!cancelled)
        {
            // note: Successful candidate emission advances only queue metadata; it never promotes an unreviewed streaming site.
            YQWorldPackProductionQueueBuilder.SyncQueue();
        }
    }

    private static void WriteReport(bool cancelled)
    {
        EnsureFolderPath(OutputRoot);
        StringBuilder report = new StringBuilder();
        report.AppendLine("# YourQuest Authored Site Streaming Report");
        report.AppendLine();
        report.AppendLine("Status: " + (cancelled ? "Cancelled" : "Complete"));
        report.AppendLine();
        report.AppendLine("| Site | Instances | Cells | Result |");
        report.AppendLine("|---|---:|---:|---|");

        foreach (SiteResult result in _results ?? new List<SiteResult>())
        {
            string outcome = string.IsNullOrWhiteSpace(result.failure)
                ? "Candidate built"
                : "Failed: " + result.failure.Replace("|", "/");
            report.AppendLine(
                "| " + result.displayName + " | " +
                result.sourceInstanceCount + " | " + result.cellCount +
                " | " + outcome + " |");
        }

        report.AppendLine();
        report.AppendLine(
            "All cell manifests remain non-release candidates until their reconstructed review scenes are visually approved.");
        File.WriteAllText(ReportPath, report.ToString());
        AssetDatabase.ImportAsset(ReportPath);
    }

    private static void EnsureFolderPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        string[] parts = normalized.Split('/');
        string current = parts[0];

        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }
}
