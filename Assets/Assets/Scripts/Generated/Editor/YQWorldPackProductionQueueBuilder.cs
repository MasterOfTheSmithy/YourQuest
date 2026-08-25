using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class YQWorldPackProductionQueueBuilder
{
    public const string CatalogPath =
        "Assets/Assets/GeneratedAssets/WorldIntake/YQWorldPackProductionCatalog.asset";

    private const string ReportPath =
        "Assets/Assets/GeneratedAssets/WorldIntake/YQWorldPackProductionQueue.md";

    private const string StreamingRoot =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/StreamingSites";

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Refresh All Pack Status")]
    public static void RefreshAllPackStatus()
    {
        YQWorldPackProductionCatalog catalog = SyncQueue();

        if (catalog == null)
            return;

        IReadOnlyList<YQWorldPackProductionRecord> records = catalog.Records;
        Debug.Log(
            "[YQWorldPackProductionQueueBuilder] WORLD PACK PRODUCTION QUEUE READY\n" +
            "Tracked packs: " + records.Count + "\n" +
            "Runtime ready: " + Count(records, YQWorldPackProductionState.RuntimeReady) + "\n" +
            "Needs streaming compilation: " + Count(records, YQWorldPackProductionState.NeedsStreamingCompilation) + "\n" +
            "Needs streaming review: " + Count(records, YQWorldPackProductionState.NeedsStreamingReview) + "\n" +
            "Needs semantic segmentation: " + Count(records, YQWorldPackProductionState.NeedsSemanticSegmentation) + "\n" +
            "Needs semantic review: " + Count(records, YQWorldPackProductionState.NeedsSemanticReview) + "\n" +
            "Source changed or blocked: " +
            (Count(records, YQWorldPackProductionState.SourceChanged) +
             Count(records, YQWorldPackProductionState.Blocked)) + "\n" +
            "Catalog: " + CatalogPath + "\n" +
            "Report: " + ReportPath);
    }

    public static YQWorldPackProductionCatalog SyncQueue()
    {
        YQAuthoredSiteSourceCatalog sourceCatalog =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteSourceCatalog>(
                YQAuthoredSiteSourceDiscovery.CatalogPath);
        YQSemanticExtractionProfileCatalog profileCatalog =
            AssetDatabase.LoadAssetAtPath<YQSemanticExtractionProfileCatalog>(
                YQSemanticExtractionProfileBuilder.CatalogPath);

        if (sourceCatalog == null || profileCatalog == null)
        {
            Debug.LogError(
                "[YQWorldPackProductionQueueBuilder] Source or semantic profile catalog is missing.");
            return null;
        }

        Dictionary<string, YQReviewedSemanticSiteManifest> semanticByKit =
            LoadSemanticManifests();
        Dictionary<string, string> semanticPaths =
            semanticByKit.ToDictionary(
                pair => pair.Key,
                pair => AssetDatabase.GetAssetPath(pair.Value),
                StringComparer.OrdinalIgnoreCase);
        List<YQWorldPackProductionRecord> records =
            new List<YQWorldPackProductionRecord>();

        foreach (YQSemanticExtractionProfile profile in
                 profileCatalog.Profiles
                     .Where(candidate => candidate != null)
                     .OrderBy(candidate => candidate.kitId,
                         StringComparer.OrdinalIgnoreCase))
        {
            YQAuthoredSiteSourceRecord source = sourceCatalog.Records
                .FirstOrDefault(candidate => candidate != null &&
                    string.Equals(
                        candidate.kitId,
                        profile.kitId,
                        StringComparison.OrdinalIgnoreCase));
            records.Add(BuildRecord(
                profile,
                source,
                semanticByKit,
                semanticPaths));
        }

        YQWorldPackProductionCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQWorldPackProductionCatalog>(
                CatalogPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<
                YQWorldPackProductionCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.Configure(records);
        EditorUtility.SetDirty(catalog);
        WriteReport(records);
        // note: Queue refresh saves only its own lightweight catalog instead of flushing every dirty project asset and triggering unrelated hot reloads.
        AssetDatabase.SaveAssetIfDirty(catalog);
        return catalog;
    }

    private static YQWorldPackProductionRecord BuildRecord(
        YQSemanticExtractionProfile profile,
        YQAuthoredSiteSourceRecord source,
        IReadOnlyDictionary<string, YQReviewedSemanticSiteManifest> semanticByKit,
        IReadOnlyDictionary<string, string> semanticPaths)
    {
        YQWorldPackProductionRecord record =
            new YQWorldPackProductionRecord
            {
                kitId = profile.kitId,
                displayName = profile.displayName,
                siteKind = profile.siteKind,
                topology = profile.topology
            };

        if (source == null ||
            string.IsNullOrWhiteSpace(source.generatedPrefabPath) ||
            !File.Exists(source.generatedPrefabPath))
        {
            record.state = YQWorldPackProductionState.MissingSourceCandidate;
            record.nextAction = "Build the authored-site source candidate.";
            return record;
        }

        record.sourceSignature = source.sourceSignature;
        record.sourcePrefabPath = source.generatedPrefabPath;

        if (source.state == YQAuthoredSiteSourceState.SourceChanged)
        {
            record.state = YQWorldPackProductionState.SourceChanged;
            record.nextAction =
                "Rebuild the authored candidate before retaining any prior approval.";
            return record;
        }

        semanticByKit.TryGetValue(
            profile.kitId,
            out YQReviewedSemanticSiteManifest semanticManifest);
        string streamingPath = StreamingRoot + "/" + profile.kitId +
            "/YQ_" + profile.kitId + "_StreamingManifest.asset";
        YQAuthoredSiteStreamingManifest streamingManifest =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                streamingPath);
        record.streamingManifestPath = streamingManifest != null
            ? streamingPath
            : string.Empty;
        record.streamingReviewScenePath = StreamingRoot + "/" +
            profile.kitId + "/YQ_" + profile.kitId +
            "_StreamingReview.unity";
        record.semanticManifestPath = semanticManifest != null
            ? semanticPaths[profile.kitId]
            : string.Empty;
        record.semanticReviewScenePath =
            "Assets/Assets/GeneratedAssets/WorldAssemblies/SemanticProfiles/" +
            profile.kitId + "/YQ_" + profile.kitId +
            "_SemanticReview.unity";
        bool legacyViking = semanticManifest != null &&
            semanticManifest.ReleaseEligible &&
            string.Equals(
                profile.kitId,
                "medieval_viking_village",
                StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(semanticManifest.SourceSignature);
        bool semanticMatchesSource = semanticManifest != null &&
            streamingManifest != null &&
            ((legacyViking) ||
             (string.Equals(
                  semanticManifest.SourceSignature,
                  source.sourceSignature,
                  StringComparison.Ordinal) &&
              semanticManifest.StreamingSite == streamingManifest &&
              HasLightweightCoverage(
                  semanticManifest,
                  streamingManifest)));

        // note: Old nested-prefab candidates deliberately fall back to segmentation so they are rebuilt into the lightweight cell-reference format before review.
        if (semanticMatchesSource && semanticManifest.ReleaseEligible)
        {
            record.state = YQWorldPackProductionState.RuntimeReady;
            record.nextAction = "No authoring action required.";
            return record;
        }

        if (streamingManifest == null)
        {
            record.state =
                YQWorldPackProductionState.NeedsStreamingCompilation;
            record.nextAction =
                "Compile the authored site into deterministic streaming cells.";
        }
        else if (streamingManifest.ReviewState ==
                 YQStreamingSiteReviewState.DeferredNeedsRepair)
        {
            record.state = YQWorldPackProductionState.Blocked;
            record.nextAction = string.IsNullOrWhiteSpace(
                    streamingManifest.ReviewNote)
                ? "Deferred during visual review; repair the authored extraction before reconsidering."
                : streamingManifest.ReviewNote;
        }
        else if (!streamingManifest.ReleaseEligible)
        {
            record.state = YQWorldPackProductionState.NeedsStreamingReview;
            record.nextAction =
                "Visually review the reconstructed streaming site; do not promote automatically.";
        }
        else
        {
            if (semanticMatchesSource && semanticManifest != null &&
                semanticManifest.ReviewState ==
                    YQSemanticSiteReviewState.DeferredNeedsRepair)
            {
                record.state = YQWorldPackProductionState.Blocked;
                record.nextAction = string.IsNullOrWhiteSpace(
                        semanticManifest.ReviewNote)
                    ? "Deferred during semantic review; repair or rebuild the candidate before reconsidering."
                    : semanticManifest.ReviewNote;
            }
            else if (semanticMatchesSource && semanticManifest != null)
            {
                record.state =
                    YQWorldPackProductionState.NeedsSemanticReview;
                record.nextAction =
                    "Review semantic zone coverage and roles, then promote the deterministic runtime site.";
            }
            else
            {
                record.state =
                    YQWorldPackProductionState.NeedsSemanticSegmentation;
                record.nextAction =
                    "Extract topology-specific semantic zones from approved streaming cells.";
            }
        }

        return record;
    }

    private static bool HasLightweightCoverage(
        YQReviewedSemanticSiteManifest semantic,
        YQAuthoredSiteStreamingManifest streaming)
    {
        if (semantic == null || streaming == null ||
            semantic.Zones.Count == 0)
        {
            return false;
        }

        HashSet<string> assigned = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (int zoneIndex = 0;
             zoneIndex < semantic.Zones.Count;
             zoneIndex++)
        {
            YQReviewedSemanticZoneRecord zone = semantic.Zones[zoneIndex];

            if (zone == null || zone.streamingCellIds.Count == 0)
                return false;

            for (int cellIndex = 0;
                 cellIndex < zone.streamingCellIds.Count;
                 cellIndex++)
            {
                if (!assigned.Add(zone.streamingCellIds[cellIndex]))
                    return false;
            }
        }

        // note: Exact cell coverage makes the lightweight semantic manifest a lossless view over the approved streaming source.
        return assigned.SetEquals(
            streaming.Cells.Select(cell => cell.StableCellId));
    }

    private static Dictionary<string, YQReviewedSemanticSiteManifest>
        LoadSemanticManifests()
    {
        Dictionary<string, YQReviewedSemanticSiteManifest> result =
            new Dictionary<string, YQReviewedSemanticSiteManifest>(
                StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets(
            "t:YQReviewedSemanticSiteManifest",
            new[]
            {
                "Assets/Assets/GeneratedAssets/WorldAssemblies/SemanticProfiles"
            });

        for (int index = 0; index < guids.Length; index++)
        {
            YQReviewedSemanticSiteManifest manifest =
                AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(
                    AssetDatabase.GUIDToAssetPath(guids[index]));

            if (manifest != null &&
                !string.IsNullOrWhiteSpace(manifest.KitId))
            {
                result[manifest.KitId] = manifest;
            }
        }

        return result;
    }

    private static int Count(
        IReadOnlyList<YQWorldPackProductionRecord> records,
        YQWorldPackProductionState state)
    {
        return records.Count(record => record.state == state);
    }

    private static void WriteReport(
        List<YQWorldPackProductionRecord> records)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("# YourQuest World Pack Production Queue");
        text.AppendLine();
        text.AppendLine(
            "Only RuntimeReady entries may be selected by compiled-world runtime logic.");
        text.AppendLine();
        text.AppendLine("| Pack | Topology | State | Next action |");
        text.AppendLine("|---|---|---|---|");

        for (int index = 0; index < records.Count; index++)
        {
            YQWorldPackProductionRecord record = records[index];
            text.AppendLine("| " + record.displayName + " | " +
                record.topology + " | " + record.state + " | " +
                record.nextAction + " |");
        }

        File.WriteAllText(ReportPath, text.ToString());
    }
}
