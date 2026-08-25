using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQWorldPackReviewWorkbench
{
    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Review/Open Next Streaming Review")]
    public static void OpenNextStreamingReview()
    {
        YQWorldPackProductionCatalog catalog =
            LoadProductionCatalog();
        YQWorldPackProductionRecord next = catalog != null
            ? catalog.Records
                .Where(record => record != null &&
                    record.state ==
                    YQWorldPackProductionState.NeedsStreamingReview)
                .OrderBy(GetReviewComplexity)
                .ThenBy(record => record.displayName,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
            : null;

        if (next == null)
        {
            Debug.Log(
                "[YQWorldPackReviewWorkbench] No streaming-site review is pending.");
            return;
        }

        if (string.IsNullOrWhiteSpace(next.streamingReviewScenePath) ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(
                next.streamingReviewScenePath) == null)
        {
            Debug.LogError(
                "[YQWorldPackReviewWorkbench] Review scene is missing for " +
                next.displayName + ".");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // note: Reviews open one generated reconstruction at a time so visual approval cannot accidentally target a different pack.
        Scene scene = EditorSceneManager.OpenScene(
            next.streamingReviewScenePath,
            OpenSceneMode.Single);
        Selection.activeGameObject = scene.GetRootGameObjects()
            .FirstOrDefault();
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log(
            "[YQWorldPackReviewWorkbench] REVIEW OPENED\n" +
            "Site: " + next.displayName + "\n" +
            "Scene: " + next.streamingReviewScenePath + "\n" +
            "Inspect composition, materials, cell seams, scale, and missing geometry before approval.");
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Review/Approve Current Streaming Review")]
    public static void ApproveCurrentStreamingReview()
    {
        YQWorldPackProductionCatalog catalog =
            LoadProductionCatalog();
        Scene activeScene = SceneManager.GetActiveScene();
        YQWorldPackProductionRecord record = catalog != null
            ? catalog.Records.FirstOrDefault(candidate =>
                candidate != null && string.Equals(
                    candidate.streamingReviewScenePath,
                    activeScene.path,
                    StringComparison.OrdinalIgnoreCase))
            : null;

        if (record == null)
        {
            Debug.LogError(
                "[YQWorldPackReviewWorkbench] The active scene is not a queued streaming review.");
            return;
        }

        YQAuthoredSiteStreamingManifest manifest =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                record.streamingManifestPath);
        List<string> errors = ValidateReviewScene(activeScene, manifest);

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[YQWorldPackReviewWorkbench] REVIEW REJECTED\nSite: " +
                record.displayName + "\n- " +
                string.Join("\n- ", errors));
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Approve Authored Streaming Site",
            "Confirm that you visually inspected " + record.displayName +
            " and found its composition, materials, scale, geometry, and cell seams acceptable.\n\n" +
            BuildPresentationReviewText(record, manifest),
            "Approve",
            "Keep Pending");

        if (!confirmed)
            return;

        YQWorldSitePresentationMode presentationMode =
            ResolvePresentationMode(record, manifest);
        YQSemanticExtractionProfile semanticProfile =
            LoadSemanticProfile(record.kitId);
        // note: The explicit confirmation is the human visual-review gate; mechanical validation alone never promotes generated geometry.
        manifest.ConfigurePresentationPolicy(presentationMode);
        manifest.ConfigureStructureUsagePolicy(
            semanticProfile != null
                ? semanticProfile.structureUsagePolicy
                : YQWorldStructureUsagePolicy.Unspecified,
            semanticProfile != null
                ? semanticProfile.maximumEnterableStructures
                : 0);
        manifest.MarkReleaseEligible();
        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssetIfDirty(manifest);
        YQWorldPackProductionQueueBuilder.SyncQueue();
        Debug.Log(
            "[YQWorldPackReviewWorkbench] STREAMING REVIEW APPROVED\n" +
            "Site: " + record.displayName + "\n" +
            "Presentation: " + presentationMode + "\n" +
            "Structure usage: " + manifest.StructureUsagePolicy + "\n" +
            "Maximum enterable structures: " +
            manifest.MaximumEnterableStructures + "\n" +
            "Next state: semantic segmentation required");
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Review/Repair Current Streaming Review From Authored Source")]
    public static void RepairCurrentStreamingReviewFromAuthoredSource()
    {
        YQWorldPackProductionCatalog catalog = LoadProductionCatalog();
        Scene activeScene = SceneManager.GetActiveScene();
        YQWorldPackProductionRecord record = catalog != null
            ? catalog.Records.FirstOrDefault(candidate =>
                candidate != null && string.Equals(
                    candidate.streamingReviewScenePath,
                    activeScene.path,
                    StringComparison.OrdinalIgnoreCase))
            : null;

        if (record == null)
        {
            Debug.LogError(
                "[YQWorldPackReviewWorkbench] The active scene is not a queued streaming review.");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Repair Authored Streaming Site",
            "Re-extract " + record.displayName +
            " from its original authored scene, rebuild only this pack's streaming cells, and replace this review reconstruction?\n\n" +
            "Imported vendor assets and every other reviewed pack remain untouched.",
            "Repair This Site",
            "Cancel");

        if (!confirmed)
            return;

        string kitId = record.kitId;
        bool repairedFromCells =
            YQAuthoredSiteStreamingCompiler.TryRepairGeneratedContextCells(
                kitId,
                record.displayName,
                record.streamingManifestPath,
                record.streamingReviewScenePath);

        if (repairedFromCells)
            return;

        // note: The repair sequence is source-first so filtered showcase-only objects cannot survive in stale streaming cells.
        YQAllPackAuthoredSiteBatchBuilder.RebuildAuthoredPack(
            kitId,
            sourceReady =>
            {
                if (!sourceReady)
                {
                    Debug.LogError(
                        "[YQWorldPackReviewWorkbench] Source repair failed for " +
                        record.displayName + "; streaming output was left unchanged.");
                    return;
                }

                YQAuthoredSiteStreamingCompiler.RecompileAuthoredSite(kitId);
            });
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Review/Skip Current Review and Open Next")]
    public static void SkipCurrentReviewAndOpenNext()
    {
        YQWorldPackProductionCatalog catalog = LoadProductionCatalog();
        Scene activeScene = SceneManager.GetActiveScene();
        YQWorldPackProductionRecord record = catalog != null
            ? catalog.Records.FirstOrDefault(candidate =>
                candidate != null && string.Equals(
                    candidate.streamingReviewScenePath,
                    activeScene.path,
                    StringComparison.OrdinalIgnoreCase))
            : null;

        if (record == null)
        {
            Debug.LogError(
                "[YQWorldPackReviewWorkbench] The active scene is not a queued streaming review.");
            return;
        }

        YQAuthoredSiteStreamingManifest manifest =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                record.streamingManifestPath);

        if (manifest == null)
        {
            Debug.LogError(
                "[YQWorldPackReviewWorkbench] The current review has no streaming manifest to defer.");
            return;
        }

        // note: Skipping is a reversible production disposition, never approval or deletion; rebuilding the site resets it to Pending automatically.
        manifest.DeferForRepair(
            "Deferred during visual review: authored extraction requires repair before reconsideration.");
        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssetIfDirty(manifest);
        YQWorldPackProductionQueueBuilder.SyncQueue();
        Debug.Log(
            "[YQWorldPackReviewWorkbench] REVIEW DEFERRED\nSite: " +
            record.displayName +
            "\nRuntime eligible: 0\nOpening the next pending review.");
        OpenNextStreamingReview();
    }

    private static List<string> ValidateReviewScene(
        Scene scene,
        YQAuthoredSiteStreamingManifest manifest)
    {
        List<string> errors = new List<string>();

        if (manifest == null)
        {
            errors.Add("Streaming manifest is missing.");
            return errors;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        if (roots.Length != 1)
        {
            errors.Add("Review scene must contain exactly one generated root.");
            return errors;
        }

        Transform root = roots[0].transform;

        if (root.childCount != manifest.Cells.Count)
        {
            errors.Add("Review scene reconstructs " + root.childCount +
                " cells but the manifest requires " + manifest.Cells.Count + ".");
        }

        int cellsToValidate = Mathf.Min(
            root.childCount,
            manifest.Cells.Count);

        for (int index = 0; index < cellsToValidate; index++)
        {
            Transform cellRoot = root.GetChild(index);
            YQAuthoredSiteStreamingCellRecord expected =
                manifest.Cells[index];
            YQWorldAssemblyDescriptor assembly =
                cellRoot.GetComponent<YQWorldAssemblyDescriptor>();
            YQWorldStreamingCellDescriptor descriptor =
                cellRoot.GetComponent<YQWorldStreamingCellDescriptor>();

            if (assembly == null ||
                assembly.AssemblyKind != YQWorldAssemblyKind.StreamingCell ||
                descriptor == null)
            {
                errors.Add("Cell " + index +
                    " is missing its deterministic streaming descriptor.");
                continue;
            }

            if (!string.Equals(
                    descriptor.StableCellId,
                    expected.StableCellId,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Cell " + index +
                    " does not match its manifest identity.");
            }

            if ((cellRoot.localPosition - expected.AuthoredLocalPosition)
                    .sqrMagnitude > 0.0001f)
            {
                errors.Add("Cell " + descriptor.StableCellId +
                    " is not at its authored reconstruction offset.");
            }

            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    cellRoot.gameObject) > 0)
            {
                errors.Add("Cell " + descriptor.StableCellId +
                    " root has a missing script reference.");
            }

            if (cellRoot.GetComponentInChildren<Renderer>(true) == null)
            {
                errors.Add("Cell " + descriptor.StableCellId +
                    " contains no renderable geometry.");
            }
        }

        // note: Deep missing-script and LOD ownership checks run once during cell compilation/repair; approval verifies lightweight reconstruction contracts without allocating every nested component again.
        return errors;
    }

    private static int GetReviewComplexity(
        YQWorldPackProductionRecord record)
    {
        YQAuthoredSiteStreamingManifest manifest = record != null
            ? AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                record.streamingManifestPath)
            : null;

        if (manifest == null)
            return int.MaxValue;

        // note: Authored instance count predicts review load far better than the tiny YAML scene file that merely references those instances.
        int result = 0;

        for (int index = 0; index < manifest.Cells.Count; index++)
            result += manifest.Cells[index].SourceInstanceCount;

        return result;
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

    private static YQWorldSitePresentationMode ResolvePresentationMode(
        YQWorldPackProductionRecord record,
        YQAuthoredSiteStreamingManifest manifest)
    {
        if (record.topology ==
            YQSemanticExtractionTopology.InteriorRooms)
        {
            return YQWorldSitePresentationMode.InteriorTransitionOnly;
        }

        if (record.topology ==
            YQSemanticExtractionTopology.DungeonRooms)
        {
            return YQWorldSitePresentationMode.SubterraneanTransitionOnly;
        }

        return manifest.PresentationMode;
    }

    private static string BuildPresentationReviewText(
        YQWorldPackProductionRecord record,
        YQAuthoredSiteStreamingManifest manifest)
    {
        YQWorldSitePresentationMode mode =
            ResolvePresentationMode(record, manifest);

        switch (mode)
        {
            case YQWorldSitePresentationMode.InteriorTransitionOnly:
                return "Placement contract: INTERIOR TRANSITION ONLY. An unfinished exterior shell is acceptable and will never be exposed by world generation." + BuildStructureUsageReviewText(record);
            case YQWorldSitePresentationMode.SubterraneanTransitionOnly:
                return "Placement contract: SUBTERRANEAN TRANSITION ONLY. The site will be entered through a curated dungeon transition." + BuildStructureUsageReviewText(record);
            default:
                return "Placement contract: SEAMLESS EXTERIOR. The complete exterior silhouette must be visually acceptable." + BuildStructureUsageReviewText(record);
        }
    }

    private static string BuildStructureUsageReviewText(
        YQWorldPackProductionRecord record)
    {
        YQSemanticExtractionProfile profile =
            LoadSemanticProfile(record.kitId);

        if (profile == null ||
            profile.structureUsagePolicy ==
            YQWorldStructureUsagePolicy.Unspecified)
        {
            return string.Empty;
        }

        if (profile.structureUsagePolicy ==
            YQWorldStructureUsagePolicy.SingleFurnishedPrimaryWithExteriorShells)
        {
            return "\n\nStructure contract: exactly one furnished primary building is enterable; every remaining building is a non-enterable exterior shell.";
        }

        return "\n\nStructure contract: " +
            profile.structureUsagePolicy + ".";
    }

    private static YQSemanticExtractionProfile LoadSemanticProfile(
        string kitId)
    {
        YQSemanticExtractionProfileCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                YQSemanticExtractionProfileCatalog>(
                YQSemanticExtractionProfileBuilder.CatalogPath);
        YQSemanticExtractionProfile profile =
            catalog != null ? catalog.Find(kitId) : null;

        if (profile != null &&
            YQSemanticExtractionProfileBuilder
                .ApplyCurrentAuthoredReviewPolicy(profile))
        {
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        return profile;
    }
}
