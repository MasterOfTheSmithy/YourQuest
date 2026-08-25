using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class YQSemanticSiteBatchReleaseBuilder
{
    private const string SemanticRoot =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/SemanticProfiles";

    private static List<YQWorldPackProductionRecord> candidates;
    private static readonly List<string> released = new List<string>();
    private static readonly List<string> failed = new List<string>();
    private static int currentIndex;
    private static AsyncOperation unloadOperation;
    private static bool running;

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Semantics/One Click - Compile, Validate, and Release All Asset Maps _F12")]
    public static void BatchCompileAndReleaseAllApprovedStreamingSites()
    {
        if (running)
        {
            Debug.LogWarning(
                "[YQSemanticSiteBatchReleaseBuilder] A semantic runtime batch is already running.");
            return;
        }

        YQWorldPackProductionCatalog catalog =
            YQWorldPackProductionQueueBuilder.SyncQueue();
        candidates = catalog != null
            ? catalog.Records
                .Where(record => record != null &&
                    (record.state ==
                        YQWorldPackProductionState.NeedsSemanticSegmentation ||
                     record.state ==
                        YQWorldPackProductionState.NeedsSemanticReview))
                .OrderBy(record => record.displayName,
                    StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<YQWorldPackProductionRecord>();

        if (candidates.Count == 0)
        {
            // note: An already-compiled project still receives catalog rebuilding and the same strict generative-readiness audit from the one-click command.
            YQRuntimeWorldSiteCatalog readyCatalog =
                YQRuntimeWorldSiteCatalogBuilder.Rebuild(false);
            int readyCatalogSiteCount = readyCatalog != null
                ? readyCatalog.Sites.Count
                : 0;
            YQRuntimeWorldSitePostflightResult readyPostflight =
                YQRuntimeWorldSitePostflightValidator.Run(false);
            Debug.Log(
                "[YQSemanticSiteBatchReleaseBuilder] NO SEMANTIC RECOMPILATION REQUIRED\n" +
                "Runtime catalog sites: " +
                readyCatalogSiteCount +
                "\nGenerative postflight: " +
                (readyPostflight.Passed ? "PASS" : "REJECTED") +
                " (" + readyPostflight.errors.Count + " errors, " +
                readyPostflight.warnings.Count + " warnings)\n" +
                "Report: " +
                YQRuntimeWorldSitePostflightValidator.ReportPath);
            return;
        }

        released.Clear();
        failed.Clear();
        currentIndex = 0;
        // note: Queue discovery may touch many manifest assets; release that discovery set before the first pack is compiled.
        unloadOperation = Resources.UnloadUnusedAssets();
        running = true;
        EditorApplication.update -= ProcessNextStep;
        EditorApplication.update += ProcessNextStep;
        Debug.Log(
            "[YQSemanticSiteBatchReleaseBuilder] TRUSTED SEMANTIC RUNTIME BATCH STARTED\n" +
            "Approved streaming sites queued: " + candidates.Count +
            "\nProcessing: one pack at a time with asset unloading between packs");
    }

    private static void ProcessNextStep()
    {
        if (!running)
            return;

        if (unloadOperation != null)
        {
            if (!unloadOperation.isDone)
                return;

            // note: Completed pack assets are released before the next manifest is loaded, bounding editor memory across the full batch.
            unloadOperation = null;
            GC.Collect();
        }

        if (currentIndex >= candidates.Count)
        {
            FinishBatch(false);
            return;
        }

        YQWorldPackProductionRecord record = candidates[currentIndex];
        bool cancel = EditorUtility.DisplayCancelableProgressBar(
            "YourQuest Runtime Site Compilation",
            "Compiling " + record.displayName + " (" +
            (currentIndex + 1) + "/" + candidates.Count + ")",
            (float)currentIndex / candidates.Count);

        if (cancel)
        {
            FinishBatch(true);
            return;
        }

        try
        {
            if (record.state ==
                YQWorldPackProductionState.NeedsSemanticSegmentation)
            {
                // note: Trusted batch compilation skips review-scene creation and queue rescans; it writes only lightweight semantic metadata.
                if (!YQSemanticSiteProductionCompiler.Compile(
                        record,
                        false,
                        false))
                {
                    failed.Add(record.displayName +
                        ": semantic candidate compilation failed");
                    CompleteCurrentPack();
                    return;
                }
            }

            string manifestPath = ResolveManifestPath(record);
            YQReviewedSemanticSiteManifest semantic =
                AssetDatabase.LoadAssetAtPath<
                    YQReviewedSemanticSiteManifest>(manifestPath);
            YQAuthoredSiteStreamingManifest streaming =
                AssetDatabase.LoadAssetAtPath<
                    YQAuthoredSiteStreamingManifest>(
                        record.streamingManifestPath);
            YQSemanticExtractionProfile profile = LoadProfile(record.kitId);
            List<string> errors = ValidateReleaseCandidate(
                record,
                semantic,
                streaming,
                profile);

            if (errors.Count > 0)
            {
                failed.Add(record.displayName + ": " +
                    string.Join("; ", errors));
            }
            else
            {
                // note: The user-authorized trusted batch promotes only candidates retaining exact source identity, cell coverage, and required semantic roles.
                semantic.MarkReleaseEligible();
                EditorUtility.SetDirty(semantic);
                AssetDatabase.SaveAssetIfDirty(semantic);
                released.Add(record.displayName);
                Debug.Log(
                    "[YQSemanticSiteBatchReleaseBuilder] RUNTIME SITE READY " +
                    (currentIndex + 1) + "/" + candidates.Count +
                    ": " + record.displayName);
            }
        }
        catch (Exception exception)
        {
            failed.Add(record.displayName + ": " +
                exception.GetBaseException().Message);
            Debug.LogException(exception);
        }

        CompleteCurrentPack();
    }

    private static void CompleteCurrentPack()
    {
        currentIndex++;
        // note: Asynchronous unloading yields control back to Unity, keeping the editor responsive and preventing cross-pack asset accumulation.
        unloadOperation = Resources.UnloadUnusedAssets();
    }

    private static void FinishBatch(bool cancelled)
    {
        running = false;
        EditorApplication.update -= ProcessNextStep;
        EditorUtility.ClearProgressBar();
        YQRuntimeWorldSiteCatalog runtimeCatalog =
            YQRuntimeWorldSiteCatalogBuilder.Rebuild(false);
        int runtimeCatalogSiteCount = runtimeCatalog != null
            ? runtimeCatalog.Sites.Count
            : 0;
        YQWorldPackProductionCatalog finalCatalog =
            YQWorldPackProductionQueueBuilder.SyncQueue();
        // note: One-click completion includes a strict runtime postflight, so a green batch means semantic selection and on-demand loading are mechanically ready for the generative system.
        YQRuntimeWorldSitePostflightResult postflight =
            YQRuntimeWorldSitePostflightValidator.Run(false);
        int blocked = finalCatalog != null
            ? finalCatalog.Records.Count(record =>
                record.state == YQWorldPackProductionState.Blocked ||
                record.state == YQWorldPackProductionState.SourceChanged ||
                record.state ==
                    YQWorldPackProductionState.MissingSourceCandidate)
            : failed.Count;

        Debug.Log(
            "[YQSemanticSiteBatchReleaseBuilder] BATCH RUNTIME COMPILATION " +
            (cancelled ? "CANCELLED" : "COMPLETE") + "\n" +
            "Released this pass: " + released.Count + "\n" +
            "Runtime catalog sites: " +
            runtimeCatalogSiteCount +
            "\nDeferred/source-blocked sites retained: " + blocked + "\n" +
            "Failed validation: " + failed.Count +
            "\nGenerative postflight: " +
            (postflight.Passed ? "PASS" : "REJECTED") +
            " (" + postflight.errors.Count + " errors, " +
            postflight.warnings.Count + " warnings)" +
            "\nPostflight report: " +
            YQRuntimeWorldSitePostflightValidator.ReportPath +
            (failed.Count > 0
                ? "\n- " + string.Join("\n- ", failed)
                : string.Empty));
        Resources.UnloadUnusedAssets();
    }

    private static List<string> ValidateReleaseCandidate(
        YQWorldPackProductionRecord record,
        YQReviewedSemanticSiteManifest semantic,
        YQAuthoredSiteStreamingManifest streaming,
        YQSemanticExtractionProfile profile)
    {
        List<string> errors = new List<string>();

        if (semantic == null || streaming == null || profile == null)
        {
            errors.Add("semantic manifest, approved streaming manifest, or profile is missing");
            return errors;
        }

        if (!streaming.ReleaseEligible)
            errors.Add("streaming source is not approved");

        if (!string.Equals(
                semantic.SourceSignature,
                record.sourceSignature,
                StringComparison.Ordinal) ||
            semantic.StreamingSite != streaming)
        {
            errors.Add("semantic source identity is stale");
        }

        if (semantic.Topology != profile.topology)
            errors.Add("semantic topology does not match the authored profile");

        HashSet<string> expectedCells = new HashSet<string>(
            streaming.Cells.Select(cell => cell.StableCellId),
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> assignedCells = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> tags = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        int sourceInstances = 0;

        for (int zoneIndex = 0;
             zoneIndex < semantic.Zones.Count;
             zoneIndex++)
        {
            YQReviewedSemanticZoneRecord zone = semantic.Zones[zoneIndex];

            if (zone == null || zone.streamingCellIds.Count == 0)
            {
                errors.Add("semantic zone has no streaming-cell references");
                continue;
            }

            tags.UnionWith(zone.semanticTags);
            sourceInstances += zone.sourceInstanceCount;

            for (int cellIndex = 0;
                 cellIndex < zone.streamingCellIds.Count;
                 cellIndex++)
            {
                if (!assignedCells.Add(zone.streamingCellIds[cellIndex]))
                {
                    errors.Add("streaming cell is assigned more than once: " +
                        zone.streamingCellIds[cellIndex]);
                }
            }
        }

        if (!expectedCells.SetEquals(assignedCells))
            errors.Add("semantic zones do not exactly cover the streaming cell set");

        if (sourceInstances != streaming.Cells.Sum(cell =>
                cell.SourceInstanceCount))
        {
            errors.Add("authored instance count is not preserved");
        }

        for (int index = 0;
             index < profile.requiredSemanticOutputs.Count;
             index++)
        {
            if (!tags.Contains(profile.requiredSemanticOutputs[index]))
            {
                errors.Add("required semantic role is absent: " +
                    profile.requiredSemanticOutputs[index]);
            }
        }

        return errors;
    }

    private static string ResolveManifestPath(
        YQWorldPackProductionRecord record)
    {
        return !string.IsNullOrWhiteSpace(record.semanticManifestPath)
            ? record.semanticManifestPath
            : SemanticRoot + "/" + record.kitId + "/YQ_" +
                record.kitId + "_ReviewedSemanticSite.asset";
    }

    private static YQSemanticExtractionProfile LoadProfile(string kitId)
    {
        YQSemanticExtractionProfileCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQSemanticExtractionProfileCatalog>(
                YQSemanticExtractionProfileBuilder.CatalogPath);
        return catalog != null ? catalog.Find(kitId) : null;
    }
}
