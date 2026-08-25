using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQSemanticSiteProductionCompiler
{
    private const string SemanticRoot =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/SemanticProfiles";

    private sealed class CellPlan
    {
        public YQAuthoredSiteStreamingCellRecord record;
        public Vector3 worldCenter;
    }

    private sealed class ZonePlan
    {
        public string stableId = string.Empty;
        public string role = string.Empty;
        public Vector3 origin;
        public Bounds worldBounds;
        public readonly List<CellPlan> cells = new List<CellPlan>();
        public readonly List<string> tags = new List<string>();
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Semantics/Compile Next Semantic Candidate")]
    public static void CompileNextSemanticCandidate()
    {
        YQWorldPackProductionCatalog catalog =
            YQWorldPackProductionQueueBuilder.SyncQueue();
        YQWorldPackProductionRecord record = catalog != null
            ? catalog.Records
                .Where(candidate => candidate != null &&
                    candidate.state ==
                        YQWorldPackProductionState.NeedsSemanticSegmentation)
                .OrderBy(GetSourceInstanceCount)
                .ThenBy(candidate => candidate.displayName,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
            : null;

        if (record == null)
        {
            Debug.Log(
                "[YQSemanticSiteProductionCompiler] No approved streaming site requires semantic segmentation.");
            return;
        }

        Compile(record);
    }

    public static bool Compile(
        YQWorldPackProductionRecord record,
        bool buildReviewScene = true,
        bool synchronizeQueue = true)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));

        YQSemanticExtractionProfileCatalog profiles =
            AssetDatabase.LoadAssetAtPath<YQSemanticExtractionProfileCatalog>(
                YQSemanticExtractionProfileBuilder.CatalogPath);
        YQSemanticExtractionProfile profile = profiles != null
            ? profiles.Find(record.kitId)
            : null;
        YQAuthoredSiteStreamingManifest streaming =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                record.streamingManifestPath);

        if (profile == null || streaming == null ||
            !streaming.ReleaseEligible)
        {
            Debug.LogError(
                "[YQSemanticSiteProductionCompiler] " + record.displayName +
                " does not have both an authored semantic profile and an approved streaming manifest.");
            return false;
        }

        List<string> errors = ValidateStreamingSource(record, streaming);

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[YQSemanticSiteProductionCompiler] SEMANTIC COMPILATION REJECTED\nSite: " +
                record.displayName + "\n- " + string.Join("\n- ", errors));
            return false;
        }

        // note: Segmentation consumes only lightweight approved cell metadata; it never reopens, moves, or rewrites the imported authored scene.
        List<CellPlan> cells = streaming.Cells
            .Select(cell => new CellPlan
            {
                record = cell,
                worldCenter = cell.AuthoredLocalPosition +
                    cell.LocalBoundsCenter
            })
            .OrderBy(cell => cell.record.StableCellId,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<ZonePlan> zones = BuildZones(profile, cells);
        string outputFolder = SemanticRoot + "/" + record.kitId;
        string manifestPath = outputFolder + "/YQ_" + record.kitId +
            "_ReviewedSemanticSite.asset";
        string reviewScenePath = outputFolder + "/YQ_" + record.kitId +
            "_SemanticReview.unity";
        string reportPath = outputFolder + "/YQ_" + record.kitId +
            "_SemanticCandidateReport.md";
        EnsureFolderPath(outputFolder);
        List<YQReviewedSemanticZoneRecord> zoneRecords =
            BuildZoneRecords(record, profile, zones);
        errors.AddRange(ValidateCandidate(profile, streaming, zoneRecords));

        YQReviewedSemanticSiteManifest manifest =
            AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(
                manifestPath);

        if (manifest == null)
        {
            manifest = ScriptableObject.CreateInstance<
                YQReviewedSemanticSiteManifest>();
            AssetDatabase.CreateAsset(manifest, manifestPath);
        }

        manifest.ConfigureCandidate(
            record.kitId,
            profile.semanticStyleKey,
            streaming.SourceSignature,
            profile.topology,
            streaming,
            zoneRecords.Sum(zone => zone.sourceInstanceCount),
            zoneRecords);

        if (errors.Count > 0)
        {
            // note: Mechanically invalid candidates are retained for diagnosis but bypass the visual queue and can never be promoted.
            manifest.DeferForRepair(
                "Semantic candidate validation failed; rebuild after correcting the reported coverage errors.");
        }

        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssetIfDirty(manifest);
        if (buildReviewScene)
        {
            BuildReviewScene(
                record.displayName,
                streaming,
                zoneRecords,
                reviewScenePath);
        }

        WriteReport(record, profile, streaming, zoneRecords, errors, reportPath);

        if (synchronizeQueue)
            YQWorldPackProductionQueueBuilder.SyncQueue();

        Debug.Log(
            "[YQSemanticSiteProductionCompiler] SEMANTIC CANDIDATE " +
            (errors.Count == 0 ? "READY" : "REJECTED") + "\n" +
            "Site: " + record.displayName + "\n" +
            "Topology: " + profile.topology + "\n" +
            "Zones: " + zoneRecords.Count + "\n" +
            "Streaming cells preserved: " + streaming.Cells.Count + "\n" +
            "Authored instances preserved: " + manifest.SourceInstanceCount +
            "\nValidation errors: " + errors.Count + "\n" +
            "Review scene: " + reviewScenePath + "\n" +
            "Runtime eligible: 0 (semantic review required)");
        return errors.Count == 0;
    }

    private static List<string> ValidateStreamingSource(
        YQWorldPackProductionRecord record,
        YQAuthoredSiteStreamingManifest streaming)
    {
        List<string> errors = new List<string>();
        HashSet<string> ids = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        if (!string.Equals(
                record.sourceSignature,
                streaming.SourceSignature,
                StringComparison.Ordinal))
        {
            errors.Add("The approved streaming manifest does not match the current authored source signature.");
        }

        if (streaming.Cells.Count == 0)
            errors.Add("The approved streaming manifest contains no cells.");

        for (int index = 0; index < streaming.Cells.Count; index++)
        {
            YQAuthoredSiteStreamingCellRecord cell = streaming.Cells[index];

            if (cell == null || cell.CellPrefab == null)
                errors.Add("Streaming cell " + index + " has no prefab.");
            else if (!ids.Add(cell.StableCellId))
                errors.Add("Duplicate streaming cell ID: " + cell.StableCellId + ".");
        }

        return errors;
    }

    private static List<ZonePlan> BuildZones(
        YQSemanticExtractionProfile profile,
        List<CellPlan> cells)
    {
        Bounds siteBounds = BoundsForCells(cells);
        int acrossX = Mathf.Max(1, Mathf.CeilToInt(
            siteBounds.size.x / Mathf.Max(10f, profile.targetHorizontalSpan)));
        int acrossZ = Mathf.Max(1, Mathf.CeilToInt(
            siteBounds.size.z / Mathf.Max(10f, profile.targetHorizontalSpan)));
        int verticalLayers = Mathf.Max(1, Mathf.CeilToInt(
            siteBounds.size.y / Mathf.Max(2f, profile.verticalLayerHeight)));
        int spatialTarget = Mathf.Max(acrossX * acrossZ, verticalLayers);
        int zoneCount = Mathf.Clamp(
            Mathf.Max(profile.minimumAssemblies, spatialTarget),
            1,
            Mathf.Min(profile.maximumAssemblies, cells.Count));
        List<Vector3> centers = SelectFarthestCenters(
            cells,
            zoneCount,
            profile.topology);
        int[] assignments = new int[cells.Count];

        // note: Farthest-point Voronoi partitioning produces deterministic spatially contiguous candidates instead of asset-order rows or random piles.
        for (int iteration = 0; iteration < 4; iteration++)
        {
            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                assignments[cellIndex] = FindNearestCenter(
                    cells[cellIndex].worldCenter,
                    centers,
                    profile.topology);
            }

            EnsureEveryZoneHasCell(assignments, cells, centers, profile.topology);
            centers = RecalculateCenters(assignments, cells, centers.Count);
        }

        List<ZonePlan> result = new List<ZonePlan>();

        for (int zoneIndex = 0; zoneIndex < zoneCount; zoneIndex++)
            result.Add(new ZonePlan());

        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            result[assignments[cellIndex]].cells.Add(cells[cellIndex]);

        result = result
            .Where(zone => zone.cells.Count > 0)
            .OrderBy(zone => zone.cells.Average(cell => cell.worldCenter.x))
            .ThenBy(zone => zone.cells.Average(cell => cell.worldCenter.z))
            .ThenBy(zone => zone.cells.Average(cell => cell.worldCenter.y))
            .ToList();
        ConfigureZoneIdentity(profile, result);
        return result;
    }

    private static void ConfigureZoneIdentity(
        YQSemanticExtractionProfile profile,
        List<ZonePlan> zones)
    {
        for (int zoneIndex = 0; zoneIndex < zones.Count; zoneIndex++)
        {
            ZonePlan zone = zones[zoneIndex];
            zone.role = profile.requiredSemanticOutputs.Count > 0
                ? profile.requiredSemanticOutputs[
                    zoneIndex % profile.requiredSemanticOutputs.Count]
                : "zone";
            zone.stableId = "yq_semantic_" + profile.kitId + "_" +
                Sanitize(zone.role) + "_" + (zoneIndex + 1).ToString("00");
            zone.worldBounds = BoundsForCells(zone.cells);
            zone.origin = zone.worldBounds.center;
            zone.origin.y = zone.worldBounds.min.y;
            zone.tags.Add("authored");
            zone.tags.Add("streaming_safe");
            zone.tags.Add(profile.topology.ToString().ToLowerInvariant());

            for (int outputIndex = zoneIndex;
                 outputIndex < profile.requiredSemanticOutputs.Count;
                 outputIndex += zones.Count)
            {
                zone.tags.Add(profile.requiredSemanticOutputs[outputIndex]);
            }

            if (!zone.tags.Contains(zone.role))
                zone.tags.Add(zone.role);
        }
    }

    private static List<Vector3> SelectFarthestCenters(
        List<CellPlan> cells,
        int count,
        YQSemanticExtractionTopology topology)
    {
        List<Vector3> centers = new List<Vector3>();
        HashSet<string> selectedIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        CellPlan first = cells
            .OrderByDescending(cell => cell.record.SourceInstanceCount)
            .ThenBy(cell => cell.record.StableCellId,
                StringComparer.OrdinalIgnoreCase)
            .First();
        centers.Add(first.worldCenter);
        selectedIds.Add(first.record.StableCellId);

        while (centers.Count < count)
        {
            CellPlan next = cells
                .Where(cell => !selectedIds.Contains(
                    cell.record.StableCellId))
                .OrderByDescending(cell => centers.Min(center =>
                    WeightedDistance(cell.worldCenter, center, topology)))
                .ThenBy(cell => cell.record.StableCellId,
                    StringComparer.OrdinalIgnoreCase)
                .First();
            centers.Add(next.worldCenter);
            selectedIds.Add(next.record.StableCellId);
        }

        return centers;
    }

    private static int FindNearestCenter(
        Vector3 point,
        List<Vector3> centers,
        YQSemanticExtractionTopology topology)
    {
        int nearest = 0;
        float best = float.MaxValue;

        for (int index = 0; index < centers.Count; index++)
        {
            float distance = WeightedDistance(point, centers[index], topology);

            if (distance < best)
            {
                best = distance;
                nearest = index;
            }
        }

        return nearest;
    }

    private static float WeightedDistance(
        Vector3 left,
        Vector3 right,
        YQSemanticExtractionTopology topology)
    {
        Vector3 delta = left - right;
        float verticalWeight = topology ==
                YQSemanticExtractionTopology.InteriorRooms ||
            topology == YQSemanticExtractionTopology.DungeonRooms ||
            topology == YQSemanticExtractionTopology.SciFiSectors
                ? 4f
                : topology == YQSemanticExtractionTopology.WildernessRegions
                    ? 1f
                    : 0.25f;
        return delta.x * delta.x + delta.z * delta.z +
            delta.y * delta.y * verticalWeight * verticalWeight;
    }

    private static void EnsureEveryZoneHasCell(
        int[] assignments,
        List<CellPlan> cells,
        List<Vector3> centers,
        YQSemanticExtractionTopology topology)
    {
        int[] counts = new int[centers.Count];

        for (int index = 0; index < assignments.Length; index++)
            counts[assignments[index]]++;

        for (int zoneIndex = 0; zoneIndex < counts.Length; zoneIndex++)
        {
            if (counts[zoneIndex] > 0)
                continue;

            int donorCell = Enumerable.Range(0, cells.Count)
                .Where(index => counts[assignments[index]] > 1)
                .OrderBy(index => WeightedDistance(
                    cells[index].worldCenter,
                    centers[zoneIndex],
                    topology))
                .First();
            counts[assignments[donorCell]]--;
            assignments[donorCell] = zoneIndex;
            counts[zoneIndex]++;
        }
    }

    private static List<Vector3> RecalculateCenters(
        int[] assignments,
        List<CellPlan> cells,
        int zoneCount)
    {
        Vector3[] sums = new Vector3[zoneCount];
        int[] counts = new int[zoneCount];

        for (int index = 0; index < cells.Count; index++)
        {
            sums[assignments[index]] += cells[index].worldCenter;
            counts[assignments[index]]++;
        }

        List<Vector3> result = new List<Vector3>(zoneCount);

        for (int index = 0; index < zoneCount; index++)
            result.Add(sums[index] / Mathf.Max(1, counts[index]));

        return result;
    }

    private static List<YQReviewedSemanticZoneRecord> BuildZoneRecords(
        YQWorldPackProductionRecord record,
        YQSemanticExtractionProfile profile,
        List<ZonePlan> zones)
    {
        List<YQReviewedSemanticZoneRecord> result =
            new List<YQReviewedSemanticZoneRecord>();

        for (int zoneIndex = 0; zoneIndex < zones.Count; zoneIndex++)
        {
            ZonePlan zone = zones[zoneIndex];
            int instanceCount = zone.cells.Sum(cell =>
                cell.record.SourceInstanceCount);

            // note: A semantic zone now stores stable references to approved streaming cells; no renderer hierarchy is copied into a second prefab.
            result.Add(new YQReviewedSemanticZoneRecord
            {
                stableId = zone.stableId,
                displayName = record.displayName + " " +
                    Humanize(zone.role),
                districtFunction = ResolveDistrictFunction(
                    profile.topology,
                    zone.tags),
                prefab = null,
                authoredSourceOrigin = zone.origin,
                localBoundsCenter = zone.worldBounds.center - zone.origin,
                localBoundsSize = zone.worldBounds.size,
                sourceInstanceCount = instanceCount,
                authoredBuildingCount = 0,
                authoredDressingCount = instanceCount,
                semanticTags = zone.tags
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                connectionSocketPaths = new List<string>(),
                streamingCellIds = zone.cells
                    .Select(cell => cell.record.StableCellId)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            });
        }

        return result;
    }

    private static List<string> ValidateCandidate(
        YQSemanticExtractionProfile profile,
        YQAuthoredSiteStreamingManifest streaming,
        List<YQReviewedSemanticZoneRecord> zones)
    {
        List<string> errors = new List<string>();
        HashSet<string> tags = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> assignedCells = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        if (zones.Count < Mathf.Min(
                profile.minimumAssemblies,
                streaming.Cells.Count) ||
            zones.Count > profile.maximumAssemblies)
        {
            errors.Add("Zone count violates the authored semantic profile.");
        }

        if (zones.Sum(zone => zone.sourceInstanceCount) !=
            streaming.Cells.Sum(cell => cell.SourceInstanceCount))
        {
            errors.Add("Semantic zones do not preserve the streaming manifest's authored instance count.");
        }

        for (int index = 0; index < zones.Count; index++)
        {
            YQReviewedSemanticZoneRecord zone = zones[index];

            if (zone.streamingCellIds.Count == 0 ||
                zone.sourceInstanceCount <= 0)
            {
                errors.Add(zone.stableId +
                    " has no approved streaming-cell evidence.");
            }

            for (int cellIndex = 0;
                 cellIndex < zone.streamingCellIds.Count;
                 cellIndex++)
            {
                if (!assignedCells.Add(zone.streamingCellIds[cellIndex]))
                {
                    errors.Add("Streaming cell is assigned more than once: " +
                        zone.streamingCellIds[cellIndex] + ".");
                }
            }

            tags.UnionWith(zone.semanticTags);
        }

        HashSet<string> expectedCells = new HashSet<string>(
            streaming.Cells.Select(cell => cell.StableCellId),
            StringComparer.OrdinalIgnoreCase);

        if (!expectedCells.SetEquals(assignedCells))
            errors.Add("Semantic zones do not cover the streaming cell set exactly once.");

        for (int index = 0;
             index < profile.requiredSemanticOutputs.Count;
             index++)
        {
            if (!tags.Contains(profile.requiredSemanticOutputs[index]))
            {
                errors.Add("Required semantic output is absent: " +
                    profile.requiredSemanticOutputs[index] + ".");
            }
        }

        return errors;
    }

    private static void BuildReviewScene(
        string displayName,
        YQAuthoredSiteStreamingManifest streaming,
        List<YQReviewedSemanticZoneRecord> zones,
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
                foreach (GameObject existingRoot in scene.GetRootGameObjects())
                    UnityEngine.Object.DestroyImmediate(existingRoot);
            }

            GameObject root = new GameObject(displayName + " Semantic Review");
            SceneManager.MoveGameObjectToScene(root, scene);
            Dictionary<string, YQAuthoredSiteStreamingCellRecord> cellsById =
                streaming.Cells.ToDictionary(
                    cell => cell.StableCellId,
                    StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < zones.Count; index++)
            {
                YQReviewedSemanticZoneRecord zone = zones[index];
                GameObject instance = new GameObject(zone.displayName);
                SceneManager.MoveGameObjectToScene(instance, scene);

                instance.transform.SetParent(root.transform, false);
                instance.transform.localPosition = zone.authoredSourceOrigin;
                YQSemanticZoneReviewDescriptor descriptor =
                    instance.AddComponent<YQSemanticZoneReviewDescriptor>();
                descriptor.Configure(
                    zone.stableId,
                    streaming.SourceSignature,
                    zone.streamingCellIds);
                Color color = Color.HSVToRGB(
                    Mathf.Repeat(index * 0.173f, 1f),
                    0.75f,
                    1f);

                for (int cellIndex = 0;
                     cellIndex < zone.streamingCellIds.Count;
                     cellIndex++)
                {
                    string cellId = zone.streamingCellIds[cellIndex];
                    YQAuthoredSiteStreamingCellRecord cell =
                        cellsById[cellId];
                    GameObject proxy = new GameObject(cellId);
                    proxy.transform.SetParent(instance.transform, false);
                    proxy.transform.localPosition =
                        cell.AuthoredLocalPosition - zone.authoredSourceOrigin;
                    YQSemanticZoneReviewDescriptor cellDescriptor =
                        proxy.AddComponent<YQSemanticZoneReviewDescriptor>();
                    cellDescriptor.ConfigureCell(cellId);
                    YQSemanticBenchmarkZoneGizmo gizmo =
                        proxy.AddComponent<YQSemanticBenchmarkZoneGizmo>();
                    gizmo.Configure(
                        zone.displayName,
                        new Bounds(
                            cell.LocalBoundsCenter,
                            MaxSize(cell.LocalBoundsSize)),
                        color);
                }
            }

            // note: The semantic review renders only cell-bound proxies, keeping even million-object source sites out of memory while preserving exact authored coverage.
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new InvalidOperationException(
                    "Unity could not save semantic review scene " +
                    scenePath + ".");
        }
        finally
        {
            if (createdScene && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Bounds BoundsForCells(List<CellPlan> cells)
    {
        Bounds result = new Bounds(
            cells[0].record.AuthoredLocalPosition +
                cells[0].record.LocalBoundsCenter,
            MaxSize(cells[0].record.LocalBoundsSize));

        for (int index = 1; index < cells.Count; index++)
        {
            CellPlan cell = cells[index];
            result.Encapsulate(new Bounds(
                cell.record.AuthoredLocalPosition +
                    cell.record.LocalBoundsCenter,
                MaxSize(cell.record.LocalBoundsSize)));
        }

        return result;
    }

    private static Vector3 MaxSize(Vector3 size)
    {
        return new Vector3(
            Mathf.Max(0.1f, size.x),
            Mathf.Max(0.1f, size.y),
            Mathf.Max(0.1f, size.z));
    }

    private static YQDistrictFunction ResolveDistrictFunction(
        YQSemanticExtractionTopology topology,
        List<string> tags)
    {
        if (topology != YQSemanticExtractionTopology.SettlementDistricts &&
            topology != YQSemanticExtractionTopology.CampZones)
        {
            return YQDistrictFunction.Unknown;
        }

        if (tags.Contains("residential") || tags.Contains("habitation"))
            return YQDistrictFunction.Residential;
        if (tags.Contains("service"))
            return YQDistrictFunction.Service;
        if (tags.Contains("defense") || tags.Contains("perimeter"))
            return YQDistrictFunction.Defensive;
        return YQDistrictFunction.MixedUse;
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

    private static string Sanitize(string value)
    {
        return new string((value ?? string.Empty)
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character)
                ? character
                : '_')
            .ToArray()).Trim('_');
    }

    private static string Humanize(string value)
    {
        string text = (value ?? string.Empty).Replace('_', ' ').Trim();
        return string.IsNullOrEmpty(text)
            ? "Zone"
            : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    private static void WriteReport(
        YQWorldPackProductionRecord record,
        YQSemanticExtractionProfile profile,
        YQAuthoredSiteStreamingManifest streaming,
        List<YQReviewedSemanticZoneRecord> zones,
        List<string> errors,
        string reportPath)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("# " + record.displayName + " Semantic Candidate");
        text.AppendLine();
        text.AppendLine("Result: " +
            (errors.Count == 0 ? "READY FOR REVIEW" : "REJECTED"));
        text.AppendLine("Topology: " + profile.topology);
        text.AppendLine("Source signature: " + streaming.SourceSignature);
        text.AppendLine("Spatial authority: approved authored streaming cells");
        text.AppendLine();
        text.AppendLine("| Zone | Origin | Cells/instances | Tags |");
        text.AppendLine("|---|---|---:|---|");

        for (int index = 0; index < zones.Count; index++)
        {
            YQReviewedSemanticZoneRecord zone = zones[index];
            text.AppendLine("| " + zone.displayName + " | " +
                Format(zone.authoredSourceOrigin) + " | " +
                zone.streamingCellIds.Count + "/" +
                zone.sourceInstanceCount + " | " +
                string.Join(", ", zone.semanticTags) + " |");
        }

        text.AppendLine();
        text.AppendLine("## Validation");

        if (errors.Count == 0)
            text.AppendLine("- Candidate contracts passed; visual semantic review is still required.");
        else
            errors.ForEach(error => text.AppendLine("- " + error));

        File.WriteAllText(reportPath, text.ToString());
    }

    private static string Format(Vector3 value)
    {
        return "(" + value.x.ToString("0.0") + ", " +
            value.y.ToString("0.0") + ", " +
            value.z.ToString("0.0") + ")";
    }

    private static void EnsureFolderPath(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];

        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);

            current = next;
        }
    }
}
