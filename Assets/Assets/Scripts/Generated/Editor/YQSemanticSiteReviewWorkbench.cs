using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQSemanticSiteReviewWorkbench
{
    private const int AutomaticPreviewInstanceLimit = 2500;
    private const int AutomaticPreviewCellLimit = 32;
    private const string PreviewRootName =
        "Semantic Model Preview (Transient)";

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Semantics/Open Next Semantic Review")]
    public static void OpenNextSemanticReview()
    {
        YQWorldPackProductionCatalog catalog = LoadProductionCatalog();
        YQWorldPackProductionRecord next = catalog != null
            ? catalog.Records
                .Where(record => record != null &&
                    record.state ==
                        YQWorldPackProductionState.NeedsSemanticReview)
                .OrderBy(GetSourceInstanceCount)
                .ThenBy(record => record.displayName,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
            : null;

        if (next == null)
        {
            Debug.Log(
                "[YQSemanticSiteReviewWorkbench] No semantic review is pending. Compile the next semantic candidate first.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                next.semanticReviewScenePath) == null)
        {
            Debug.LogError(
                "[YQSemanticSiteReviewWorkbench] Semantic review scene is missing for " +
                next.displayName + ".");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // note: Opening one candidate at a time keeps visual approval bound to one exact manifest and source signature.
        Scene scene = EditorSceneManager.OpenScene(
            next.semanticReviewScenePath,
            OpenSceneMode.Single);
        YQAuthoredSiteStreamingManifest streaming =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                next.streamingManifestPath);
        GameObject previewRoot = streaming != null &&
            streaming.Cells.Count <= AutomaticPreviewCellLimit &&
            streaming.Cells.Sum(cell => cell.SourceInstanceCount) <=
                AutomaticPreviewInstanceLimit
                ? BuildGeometryPreview(scene, streaming, streaming.Cells)
                : null;
        Selection.activeGameObject = previewRoot != null
            ? previewRoot
            : scene.GetRootGameObjects().FirstOrDefault(root =>
                !string.Equals(
                    root.name,
                    PreviewRootName,
                    StringComparison.Ordinal));
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.drawGizmos = true;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log(
            "[YQSemanticSiteReviewWorkbench] SEMANTIC REVIEW OPENED\n" +
            "Site: " + next.displayName + "\n" +
            "Topology: " + next.topology + "\n" +
            "Model preview: " +
            (previewRoot != null
                ? "automatic lightweight-site preview loaded"
                : "large-site safety mode; select a colored cell and use Preview Selected Cell Models") +
            "\nInspect colored zone coverage, coherent boundaries, access routes, POI placement, and role labels before approval.");
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Semantics/Preview/Preview Selected Cell Models")]
    public static void PreviewSelectedCellModels()
    {
        Scene scene = SceneManager.GetActiveScene();
        YQWorldPackProductionRecord record = FindActiveReviewRecord(scene);
        YQSemanticZoneReviewDescriptor selected =
            Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<
                    YQSemanticZoneReviewDescriptor>()
                : null;
        string selectedCellId = selected != null &&
            !string.IsNullOrWhiteSpace(selected.StableCellId)
            ? selected.StableCellId
            : ResolveLegacySelectedCellId(Selection.activeGameObject);

        if (record == null || string.IsNullOrWhiteSpace(selectedCellId))
        {
            Debug.LogWarning(
                "[YQSemanticSiteReviewWorkbench] Select one colored semantic cell proxy before requesting its model preview.");
            return;
        }

        YQAuthoredSiteStreamingManifest streaming =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                record.streamingManifestPath);
        YQAuthoredSiteStreamingCellRecord cell = streaming != null
            ? streaming.Cells.FirstOrDefault(candidate => string.Equals(
                candidate.StableCellId,
                selectedCellId,
                StringComparison.OrdinalIgnoreCase))
            : null;

        if (cell == null)
        {
            Debug.LogError(
                "[YQSemanticSiteReviewWorkbench] The selected proxy no longer resolves to an approved streaming cell.");
            return;
        }

        // note: Large reviews instantiate only the selected approved cell, preventing a whole settlement hierarchy from returning to memory.
        GameObject previewRoot = BuildGeometryPreview(
            scene,
            streaming,
            new[] { cell });
        Selection.activeGameObject = previewRoot;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log(
            "[YQSemanticSiteReviewWorkbench] SELECTED CELL MODEL PREVIEW LOADED\n" +
            "Site: " + record.displayName + "\nCell: " +
            cell.StableCellId + "\nAuthored instances: " +
            cell.SourceInstanceCount);
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Semantics/Preview/Clear Model Preview")]
    public static void ClearModelPreview()
    {
        ClearGeometryPreview(SceneManager.GetActiveScene());
        Debug.Log(
            "[YQSemanticSiteReviewWorkbench] Transient semantic model preview cleared.");
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Semantics/Approve Current Semantic Review and Compile Runtime Site")]
    public static void ApproveCurrentSemanticReview()
    {
        YQWorldPackProductionCatalog catalog = LoadProductionCatalog();
        Scene activeScene = SceneManager.GetActiveScene();
        YQWorldPackProductionRecord record = catalog != null
            ? catalog.Records.FirstOrDefault(candidate =>
                candidate != null && string.Equals(
                    candidate.semanticReviewScenePath,
                    activeScene.path,
                    StringComparison.OrdinalIgnoreCase))
            : null;

        if (record == null)
        {
            Debug.LogError(
                "[YQSemanticSiteReviewWorkbench] The active scene is not a queued semantic review.");
            return;
        }

        YQReviewedSemanticSiteManifest semantic =
            AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(
                record.semanticManifestPath);
        YQAuthoredSiteStreamingManifest streaming =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                record.streamingManifestPath);
        YQSemanticExtractionProfile profile = LoadProfile(record.kitId);
        List<string> errors = ValidateReviewScene(
            activeScene,
            semantic,
            streaming,
            profile);

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[YQSemanticSiteReviewWorkbench] SEMANTIC REVIEW REJECTED\nSite: " +
                record.displayName + "\n- " + string.Join("\n- ", errors));
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Approve Semantic Runtime Site",
            "Confirm that you inspected " + record.displayName +
            " and its colored " + semantic.Zones.Count +
            " zone boundaries are coherent for " + profile.topology +
            ".\n\nThis promotes the exact authored composition into the runtime allow-list. The LLM may select its semantic roles, but cannot rearrange its geometry.",
            "Approve and Compile",
            "Keep Pending");

        if (!confirmed)
            return;

        // note: Every compiled zone prefab and its manifest are promoted together so runtime can never observe a half-approved site.
        for (int index = 0; index < semantic.Zones.Count; index++)
        {
            GameObject prefab = semantic.Zones[index].prefab;
            YQWorldAssemblyDescriptor assembly = prefab != null
                ? prefab.GetComponent<YQWorldAssemblyDescriptor>()
                : null;

            if (assembly != null)
            {
                assembly.MarkApprovedGolden();
                EditorUtility.SetDirty(assembly);
                EditorUtility.SetDirty(prefab);
            }
        }

        semantic.MarkReleaseEligible();
        EditorUtility.SetDirty(semantic);
        AssetDatabase.SaveAssets();
        YQRuntimeWorldSiteCatalog runtimeCatalog =
            YQRuntimeWorldSiteCatalogBuilder.Rebuild(false);
        YQWorldPackProductionQueueBuilder.SyncQueue();
        Debug.Log(
            "[YQSemanticSiteReviewWorkbench] SEMANTIC SITE APPROVED AND RUNTIME COMPILED\n" +
            "Site: " + record.displayName + "\n" +
            "Zones: " + semantic.Zones.Count + "\n" +
            "Authored instances: " + semantic.SourceInstanceCount + "\n" +
            "Runtime catalog sites: " +
            (runtimeCatalog != null ? runtimeCatalog.Sites.Count : 0) +
            "\nRuntime eligible: 1");
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Semantics/Defer Current Semantic Review and Open Next")]
    public static void DeferCurrentSemanticReview()
    {
        YQWorldPackProductionCatalog catalog = LoadProductionCatalog();
        Scene activeScene = SceneManager.GetActiveScene();
        YQWorldPackProductionRecord record = catalog != null
            ? catalog.Records.FirstOrDefault(candidate =>
                candidate != null && string.Equals(
                    candidate.semanticReviewScenePath,
                    activeScene.path,
                    StringComparison.OrdinalIgnoreCase))
            : null;

        if (record == null)
        {
            Debug.LogError(
                "[YQSemanticSiteReviewWorkbench] The active scene is not a queued semantic review.");
            return;
        }

        YQReviewedSemanticSiteManifest manifest =
            AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(
                record.semanticManifestPath);

        if (manifest == null)
            return;

        // note: Deferral is reversible and preserves generated evidence while guaranteeing that the candidate stays outside runtime selection.
        manifest.DeferForRepair(
            "Deferred during semantic review: zone boundaries or roles require a candidate rebuild.");
        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssetIfDirty(manifest);
        YQRuntimeWorldSiteCatalogBuilder.Rebuild(false);
        YQWorldPackProductionQueueBuilder.SyncQueue();
        Debug.Log(
            "[YQSemanticSiteReviewWorkbench] SEMANTIC REVIEW DEFERRED\n" +
            "Site: " + record.displayName + "\nRuntime eligible: 0");
        OpenNextSemanticReview();
    }

    private static List<string> ValidateReviewScene(
        Scene scene,
        YQReviewedSemanticSiteManifest semantic,
        YQAuthoredSiteStreamingManifest streaming,
        YQSemanticExtractionProfile profile)
    {
        List<string> errors = new List<string>();

        if (semantic == null || streaming == null || profile == null)
        {
            errors.Add("Semantic manifest, streaming manifest, or extraction profile is missing.");
            return errors;
        }

        if (!streaming.ReleaseEligible)
            errors.Add("The source streaming site is no longer approved.");

        if (!string.Equals(
                semantic.SourceSignature,
                streaming.SourceSignature,
                StringComparison.Ordinal))
        {
            errors.Add("The semantic candidate is stale relative to its streaming source.");
        }

        if (semantic.Topology != profile.topology)
            errors.Add("The semantic candidate topology no longer matches its authored profile.");

        GameObject[] roots = scene.GetRootGameObjects()
            .Where(root =>
                !string.Equals(
                    root.name,
                    PreviewRootName,
                    StringComparison.Ordinal))
            .ToArray();

        if (roots.Length != 1)
        {
            errors.Add("Review scene must contain exactly one generated root.");
            return errors;
        }

        Transform root = roots[0].transform;

        if (root.childCount != semantic.Zones.Count)
        {
            errors.Add("Review scene contains " + root.childCount +
                " zones but the semantic manifest requires " +
                semantic.Zones.Count + ".");
        }

        HashSet<string> expectedCells = new HashSet<string>(
            streaming.Cells.Select(cell => cell.StableCellId),
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> reviewedCells = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> allTags = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, YQAuthoredSiteStreamingCellRecord> cellsById =
            streaming.Cells.ToDictionary(
                cell => cell.StableCellId,
                StringComparer.OrdinalIgnoreCase);
        int reviewedInstances = 0;
        int zonesToValidate = Mathf.Min(
            root.childCount,
            semantic.Zones.Count);

        for (int zoneIndex = 0;
             zoneIndex < zonesToValidate;
             zoneIndex++)
        {
            Transform zoneRoot = root.GetChild(zoneIndex);
            YQReviewedSemanticZoneRecord expectedZone =
                semantic.Zones[zoneIndex];
            YQSemanticZoneReviewDescriptor descriptor =
                zoneRoot.GetComponent<YQSemanticZoneReviewDescriptor>();

            if ((zoneRoot.localPosition - expectedZone.authoredSourceOrigin)
                    .sqrMagnitude > 0.0001f)
            {
                errors.Add(expectedZone.stableId +
                    " is not at its authored source origin.");
            }

            if (descriptor == null ||
                !string.Equals(
                    descriptor.StableZoneId,
                    expectedZone.stableId,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    descriptor.SourceSignature,
                    streaming.SourceSignature,
                    StringComparison.Ordinal))
            {
                errors.Add(expectedZone.stableId +
                    " is missing its lightweight semantic-review contract.");
                continue;
            }

            if (zoneRoot.childCount != descriptor.StreamingCellIds.Count ||
                !new HashSet<string>(
                    descriptor.StreamingCellIds,
                    StringComparer.OrdinalIgnoreCase).SetEquals(
                        expectedZone.streamingCellIds))
            {
                errors.Add(expectedZone.stableId +
                    " proxy coverage does not match its manifest cell IDs.");
            }

            allTags.UnionWith(expectedZone.semanticTags);

            // note: Validation resolves lightweight stable IDs against the approved manifest and never traverses renderer-heavy prefab descendants.
            for (int cellIndex = 0;
                 cellIndex < descriptor.StreamingCellIds.Count;
                 cellIndex++)
            {
                string cellId = descriptor.StreamingCellIds[cellIndex];

                if (!cellsById.TryGetValue(
                        cellId,
                        out YQAuthoredSiteStreamingCellRecord cell))
                {
                    errors.Add(expectedZone.stableId +
                        " references a cell absent from the approved streaming manifest: " +
                        cellId + ".");
                    continue;
                }

                if (!reviewedCells.Add(cell.StableCellId))
                    errors.Add("Streaming cell appears in multiple semantic zones: " +
                        cell.StableCellId + ".");

                reviewedInstances += cell.SourceInstanceCount;
            }
        }

        foreach (string missing in expectedCells.Except(
                     reviewedCells,
                     StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Streaming cell is not assigned to a semantic zone: " +
                missing + ".");
        }

        foreach (string unexpected in reviewedCells.Except(
                     expectedCells,
                     StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Semantic candidate contains an unexpected streaming cell: " +
                unexpected + ".");
        }

        if (reviewedInstances != semantic.SourceInstanceCount)
        {
            errors.Add("Reviewed authored-instance count does not match the semantic manifest.");
        }

        for (int index = 0;
             index < profile.requiredSemanticOutputs.Count;
             index++)
        {
            if (!allTags.Contains(profile.requiredSemanticOutputs[index]))
            {
                errors.Add("Required semantic role is absent: " +
                    profile.requiredSemanticOutputs[index] + ".");
            }
        }

        return errors;
    }

    private static GameObject BuildGeometryPreview(
        Scene scene,
        YQAuthoredSiteStreamingManifest streaming,
        IEnumerable<YQAuthoredSiteStreamingCellRecord> cells)
    {
        ClearGeometryPreview(scene);
        GameObject previewRoot = new GameObject(PreviewRootName);
        previewRoot.hideFlags = HideFlags.DontSaveInEditor;
        SceneManager.MoveGameObjectToScene(previewRoot, scene);

        foreach (YQAuthoredSiteStreamingCellRecord cell in cells)
        {
            if (cell == null || cell.CellPrefab == null)
                continue;

            GameObject instance = PrefabUtility.InstantiatePrefab(
                cell.CellPrefab,
                scene) as GameObject;

            if (instance == null)
                continue;

            instance.hideFlags = HideFlags.DontSaveInEditor;
            instance.transform.SetParent(previewRoot.transform, false);
            instance.transform.localPosition = cell.AuthoredLocalPosition;
        }

        // note: Preview geometry is transient, references approved cell prefabs directly, and is never serialized into the semantic review scene.
        return previewRoot;
    }

    private static void ClearGeometryPreview(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0; index < roots.Length; index++)
        {
            if (string.Equals(
                    roots[index].name,
                    PreviewRootName,
                    StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(roots[index]);
            }
        }
    }

    private static YQWorldPackProductionRecord FindActiveReviewRecord(
        Scene scene)
    {
        YQWorldPackProductionCatalog catalog = LoadProductionCatalog();
        return catalog != null
            ? catalog.Records.FirstOrDefault(candidate =>
                candidate != null && string.Equals(
                    candidate.semanticReviewScenePath,
                    scene.path,
                    StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private static string ResolveLegacySelectedCellId(GameObject selected)
    {
        if (selected == null || selected.transform.parent == null)
            return string.Empty;

        YQSemanticZoneReviewDescriptor zone = selected.transform.parent
            .GetComponent<YQSemanticZoneReviewDescriptor>();
        int siblingIndex = selected.transform.GetSiblingIndex();

        // note: Existing lightweight review scenes remain previewable through their deterministic proxy child order.
        return zone != null && siblingIndex >= 0 &&
            siblingIndex < zone.StreamingCellIds.Count
                ? zone.StreamingCellIds[siblingIndex]
                : string.Empty;
    }

    private static int GetSourceInstanceCount(
        YQWorldPackProductionRecord record)
    {
        YQAuthoredSiteStreamingManifest manifest = record != null
            ? AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                record.streamingManifestPath)
            : null;
        return manifest != null
            ? manifest.Cells.Sum(cell => cell.SourceInstanceCount)
            : int.MaxValue;
    }

    private static YQSemanticExtractionProfile LoadProfile(string kitId)
    {
        YQSemanticExtractionProfileCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQSemanticExtractionProfileCatalog>(
                YQSemanticExtractionProfileBuilder.CatalogPath);
        return catalog != null ? catalog.Find(kitId) : null;
    }

    private static YQWorldPackProductionCatalog LoadProductionCatalog()
    {
        YQWorldPackProductionCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQWorldPackProductionCatalog>(
                YQWorldPackProductionQueueBuilder.CatalogPath);
        return catalog != null
            ? catalog
            : YQWorldPackProductionQueueBuilder.SyncQueue();
    }
}
