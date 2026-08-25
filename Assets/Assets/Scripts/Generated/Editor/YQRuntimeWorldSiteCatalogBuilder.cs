using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class YQRuntimeWorldSiteCatalogBuilder
{
    public const string CatalogPath =
        "Assets/Assets/Resources/YQRuntimeWorldSiteCatalog.asset";

    private const string SemanticRoot =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/SemanticProfiles";

    private const string RuntimeSiteRoot =
        "Assets/Assets/Resources/YQWorldSites";

    [InitializeOnLoadMethod]
    private static void QueueSpatialCatalogSchemaMigration()
    {
        // note: This is a one-time project-owned data migration, not a recurring asset-pack scan; normal new-pack releases still rebuild explicitly through the production pipeline.
        EditorApplication.update -= RebuildStaleSpatialCatalogOnce;
        EditorApplication.update += RebuildStaleSpatialCatalogOnce;
    }

    private static void RebuildStaleSpatialCatalogOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Never launch a catalog migration inside gameplay; the normal production command remains available after leaving Play Mode.
            EditorApplication.update -= RebuildStaleSpatialCatalogOnce;
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        EditorApplication.update -= RebuildStaleSpatialCatalogOnce;

        YQRuntimeWorldSiteCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQRuntimeWorldSiteCatalog>(
                CatalogPath);
        bool requiresMigration = catalog == null ||
            catalog.Sites.Count == 0 ||
            catalog.FindByKitId("medieval_viking_village") == null;

        if (!requiresMigration && catalog != null)
        {
            for (int index = 0; index < catalog.Sites.Count; index++)
            {
                YQRuntimeWorldSiteRecord record = catalog.Sites[index];

                if (record == null || !string.Equals(
                        record.spatialMetadataVersion,
                        YQRuntimeWorldSiteSpatialMetadataCompiler.MetadataVersion,
                        StringComparison.Ordinal))
                {
                    requiresMigration = true;
                    break;
                }
            }
        }

        if (!requiresMigration)
            return;

        // note: Publishing through the authoritative builder also creates the Resources copy required by runtime streaming; hand-authored YAML aliases would bypass dependency validation.
        Rebuild(true);
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Runtime/Rebuild Approved Site Catalog")]
    public static void RebuildApprovedSiteCatalog()
    {
        YQRuntimeWorldSiteCatalog catalog = Rebuild(false);
        Debug.Log(
            "[YQRuntimeWorldSiteCatalogBuilder] RUNTIME SITE CATALOG READY\n" +
            "Approved sites: " + (catalog != null ? catalog.Sites.Count : 0) +
            "\nCatalog: " + CatalogPath);
    }

    public static YQRuntimeWorldSiteCatalog Rebuild(bool logResult)
    {
        // note: Runtime compilation scans only project-owned reviewed manifests and never imported vendor scenes or unreviewed candidates.
        string[] manifestGuids = AssetDatabase.FindAssets(
            "t:YQReviewedSemanticSiteManifest",
            new[] { SemanticRoot });
        YQSemanticExtractionProfileCatalog profiles =
            AssetDatabase.LoadAssetAtPath<YQSemanticExtractionProfileCatalog>(
                YQSemanticExtractionProfileBuilder.CatalogPath);
        List<YQRuntimeWorldSiteRecord> records =
            new List<YQRuntimeWorldSiteRecord>();
        int quarantinedSpatialSites = 0;

        for (int index = 0; index < manifestGuids.Length; index++)
        {
            EditorUtility.DisplayProgressBar(
                "YourQuest Runtime Catalog",
                "Indexing approved site " + (index + 1) + "/" +
                manifestGuids.Length,
                manifestGuids.Length > 0
                    ? (float)index / manifestGuids.Length
                    : 1f);
            YQReviewedSemanticSiteManifest semantic =
                AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(
                    AssetDatabase.GUIDToAssetPath(manifestGuids[index]));

            if (semantic == null || !semantic.ReleaseEligible ||
                semantic.Zones.Count == 0)
            {
                continue;
            }

            string streamingPath =
                "Assets/Assets/GeneratedAssets/WorldAssemblies/StreamingSites/" +
                semantic.KitId + "/YQ_" + semantic.KitId +
                "_StreamingManifest.asset";
            YQAuthoredSiteStreamingManifest streaming =
                AssetDatabase.LoadAssetAtPath<YQAuthoredSiteStreamingManifest>(
                    streamingPath);

            // note: Generic sites require an approved matching streaming source; the reviewed Viking golden source remains a supported legacy authority.
            bool legacyViking = string.Equals(
                semantic.KitId,
                "medieval_viking_village",
                StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(semantic.SourceSignature);
            bool currentSource = legacyViking ||
                (streaming != null &&
                 streaming.ReleaseEligible &&
                 string.Equals(
                     semantic.SourceSignature,
                     streaming.SourceSignature,
                     StringComparison.Ordinal));

            if (!currentSource)
                continue;

            YQSemanticExtractionProfile profile =
                profiles?.Find(semantic.KitId);

            string runtimeFolder = RuntimeSiteRoot + "/" + semantic.KitId;
            string runtimePath = runtimeFolder +
                "/YQRuntimeSemanticSite.asset";
            EnsureFolderPath(runtimeFolder);
            YQReviewedSemanticSiteManifest runtimeSite =
                AssetDatabase.LoadAssetAtPath<
                    YQReviewedSemanticSiteManifest>(runtimePath);

            if (runtimeSite == null)
            {
                runtimeSite = ScriptableObject.CreateInstance<
                    YQReviewedSemanticSiteManifest>();
                AssetDatabase.CreateAsset(runtimeSite, runtimePath);
            }

            // note: Each approved site is copied behind its own Resources key so selecting one theme never loads every pack's prefab dependencies.
            EditorUtility.CopySerialized(semantic, runtimeSite);
            runtimeSite.name = "YQRuntimeSemanticSite";
            EditorUtility.SetDirty(runtimeSite);
            AssetDatabase.SaveAssetIfDirty(runtimeSite);

            YQWorldStructureUsagePolicy runtimeStructurePolicy =
                ResolveRuntimeStructurePolicy(streaming, profile);
            int runtimeMaximumEnterableStructures =
                ResolveRuntimeMaximumEnterableStructures(
                    streaming,
                    profile,
                    runtimeStructurePolicy);
            YQRuntimeWorldSiteSpatialMetadata spatial =
                YQRuntimeWorldSiteSpatialMetadataCompiler.Analyze(
                    semantic,
                    streaming,
                    legacyViking);

            if (!spatial.SpatiallyValidated)
            {
                quarantinedSpatialSites++;
                // note: Keep malformed or over-budget authored packs in their review source, but never publish them into Resources where live generation could select them.
                Debug.LogWarning(
                    "[YQRuntimeWorldSiteCatalogBuilder] QUARANTINED RUNTIME SITE\n" +
                    "Site: " + semantic.KitId + "\n" +
                    "Reason: " + spatial.ValidationFailure);

                semantic = null;
                streaming = null;
                runtimeSite = null;

                // note: Reclaim imported dependency graphs in small batches; forcing a full unload for every individual pack made catalog refresh unnecessarily slow and memory-spiky.
                if ((index + 1) % 4 == 0)
                {
                    GC.Collect();
                    EditorUtility.UnloadUnusedAssetsImmediate(true);
                }

                continue;
            }

            records.Add(new YQRuntimeWorldSiteRecord
            {
                kitId = semantic.KitId,
                semanticStyleKey = semantic.SemanticStyleKey,
                // note: The reviewed semantic profile owns generative meaning; vendor/source classification remains streaming metadata and may intentionally differ.
                siteKind = profile != null &&
                    profile.siteKind != YQAuthoredSiteKind.Unknown
                        ? profile.siteKind
                        : streaming != null
                            ? streaming.SiteKind
                            : YQAuthoredSiteKind.Unknown,
                topology = semantic.Topology !=
                    YQSemanticExtractionTopology.Unknown
                        ? semantic.Topology
                        : profile?.topology ??
                            YQSemanticExtractionTopology.Unknown,
                presentationMode = legacyViking
                    ? YQWorldSitePresentationMode.SeamlessExterior
                    : streaming.PresentationMode,
                structureUsagePolicy = runtimeStructurePolicy,
                maximumEnterableStructures =
                    runtimeMaximumEnterableStructures,
                semanticTags = semantic.Zones
                    .Where(zone => zone != null)
                    .SelectMany(zone => zone.semanticTags)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                runtimeManifestResourceKey = "YQWorldSites/" +
                    semantic.KitId + "/YQRuntimeSemanticSite",
                // note: Runtime selection reads this compact authored footprint contract without pulling every cell prefab and its dependency graph into memory.
                spatialMetadataVersion =
                    YQRuntimeWorldSiteSpatialMetadataCompiler.MetadataVersion,
                spatiallyValidated = spatial.SpatiallyValidated,
                seamlessPlacementEligible =
                    spatial.SeamlessPlacementEligible,
                authoredFootprintCenter = spatial.FootprintCenter,
                authoredFootprintSize = spatial.FootprintSize,
                authoredFoundationY = spatial.FoundationY,
                authoredFootprintRadius = spatial.FootprintRadius,
                activeCellCount = spatial.ActiveCellCount,
                activeInstanceCount = spatial.ActiveInstanceCount,
                spatialSignature = spatial.Signature,
                spatialValidationFailure = spatial.ValidationFailure,
                streamingSite = null,
                semanticSite = null
            });

            // note: Runtime records retain only primitive semantic metadata; release source graphs in bounded batches so dozens of authored packs do not accumulate without paying one global unload per record.
            semantic = null;
            streaming = null;
            runtimeSite = null;

            if ((index + 1) % 4 == 0)
            {
                GC.Collect();
                EditorUtility.UnloadUnusedAssetsImmediate(true);
            }
        }


        EditorUtility.ClearProgressBar();

        records = records
            .OrderBy(record => record.kitId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        YQRuntimeWorldSiteCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQRuntimeWorldSiteCatalog>(
                CatalogPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<
                YQRuntimeWorldSiteCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.Configure(records);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);

        if (logResult)
        {
            Debug.Log(
                "[YQRuntimeWorldSiteCatalogBuilder] Runtime catalog contains " +
                records.Count + " approved sites; " +
                quarantinedSpatialSites +
                " spatial/performance-invalid sites remain quarantined.");
        }

        return catalog;
    }

    private static YQWorldStructureUsagePolicy ResolveRuntimeStructurePolicy(
        YQAuthoredSiteStreamingManifest streaming,
        YQSemanticExtractionProfile profile)
    {
        if (profile != null && profile.structureUsagePolicy !=
            YQWorldStructureUsagePolicy.Unspecified)
        {
            return profile.structureUsagePolicy;
        }

        if (streaming != null &&
            streaming.StructureUsagePolicy !=
            YQWorldStructureUsagePolicy.Unspecified)
        {
            return streaming.StructureUsagePolicy;
        }

        // note: Unreviewed exterior buildings remain shells, while transition-only interiors and dungeons expose the authored site itself as the single playable structure.
        return streaming == null ||
            streaming.PresentationMode ==
                YQWorldSitePresentationMode.SeamlessExterior
            ? YQWorldStructureUsagePolicy.ExteriorShellsOnly
            : YQWorldStructureUsagePolicy.FullyEnterable;
    }

    private static int ResolveRuntimeMaximumEnterableStructures(
        YQAuthoredSiteStreamingManifest streaming,
        YQSemanticExtractionProfile profile,
        YQWorldStructureUsagePolicy runtimePolicy)
    {
        if (profile != null && profile.structureUsagePolicy !=
            YQWorldStructureUsagePolicy.Unspecified)
        {
            return Mathf.Max(0, profile.maximumEnterableStructures);
        }

        if (streaming != null &&
            streaming.StructureUsagePolicy !=
            YQWorldStructureUsagePolicy.Unspecified)
        {
            return Mathf.Max(0, streaming.MaximumEnterableStructures);
        }

        return runtimePolicy == YQWorldStructureUsagePolicy.FullyEnterable
            ? 1
            : 0;
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

internal sealed class YQRuntimeWorldSiteSpatialMetadata
{
    public bool SpatiallyValidated;
    public bool SeamlessPlacementEligible;
    public Vector3 FootprintCenter;
    public Vector3 FootprintSize;
    public float FoundationY;
    public float FootprintRadius;
    public int ActiveCellCount;
    public int ActiveInstanceCount;
    public string Signature = string.Empty;
    public string ValidationFailure = string.Empty;
}

internal static class YQRuntimeWorldSiteSpatialMetadataCompiler
{
    public const string MetadataVersion = "reviewed-site-spatial-1.1.0";

    private const float MaximumRuntimeRadius = 225f;
    private const float MaximumSeamlessDimension = 461f;
    private const float MaximumSeamlessVerticalSpan = 140f;
    private const int MaximumRuntimeInstanceCount = 25000;
    private const float MinimumBoundsDimension = 0.001f;

    public static YQRuntimeWorldSiteSpatialMetadata Analyze(
        YQReviewedSemanticSiteManifest semantic,
        YQAuthoredSiteStreamingManifest streaming,
        bool useLegacyZones)
    {
        YQRuntimeWorldSiteSpatialMetadata result =
            new YQRuntimeWorldSiteSpatialMetadata();
        YQWorldSitePresentationMode presentationMode = useLegacyZones
            ? YQWorldSitePresentationMode.SeamlessExterior
            : streaming != null
                ? streaming.PresentationMode
                : YQWorldSitePresentationMode.Unknown;
        List<string> failures = new List<string>();
        List<float> foundationCandidates = new List<float>();
        StringBuilder signatureSource = new StringBuilder();
        Bounds aggregate = default;
        bool aggregateInitialized = false;
        long instanceTotal = 0;

        // note: The signature begins with selection authority, so changing topology or presentation invalidates stale cached geometry even when the raw positions happen to match.
        AppendSignatureValue(signatureSource, MetadataVersion);
        AppendSignatureValue(signatureSource,
            semantic != null ? semantic.KitId : string.Empty);
        AppendSignatureValue(signatureSource,
            ((int)presentationMode).ToString(
                CultureInfo.InvariantCulture));
        AppendSignatureValue(signatureSource,
            useLegacyZones ? "legacy-zones" : "streaming-cells");

        if (semantic == null)
        {
            failures.Add("reviewed semantic manifest is missing");
        }
        else if (useLegacyZones)
        {
            AnalyzeLegacyZones(
                semantic,
                signatureSource,
                failures,
                foundationCandidates,
                ref aggregate,
                ref aggregateInitialized,
                ref instanceTotal,
                result);
        }
        else
        {
            AnalyzeStreamingCells(
                semantic,
                streaming,
                signatureSource,
                failures,
                foundationCandidates,
                ref aggregate,
                ref aggregateInitialized,
                ref instanceTotal,
                result);
        }

        if (!aggregateInitialized)
            failures.Add("semantic selection contains no valid authored geometry");

        if (instanceTotal > int.MaxValue)
            failures.Add("active instance complexity exceeds Int32 capacity");

        result.ActiveInstanceCount = instanceTotal > int.MaxValue
            ? int.MaxValue
            : Mathf.Max(0, (int)instanceTotal);

        if (result.ActiveCellCount <= 0)
            failures.Add("active authored-cell complexity is zero");

        if (result.ActiveInstanceCount <= 0)
            failures.Add("active authored-instance complexity is zero");
        else if (result.ActiveInstanceCount > MaximumRuntimeInstanceCount)
        {
            // note: Spatially valid geometry can still be operationally unsafe; Nordic's 145k authored roots is quarantined until it receives purpose-built HLOD/cell reduction.
            failures.Add(
                "active instance count " +
                result.ActiveInstanceCount.ToString(
                    CultureInfo.InvariantCulture) +
                " exceeds the " +
                MaximumRuntimeInstanceCount.ToString(
                    CultureInfo.InvariantCulture) +
                " beta runtime budget");
        }

        if (aggregateInitialized)
        {
            foundationCandidates.Sort();
            result.FootprintCenter = aggregate.center;
            result.FootprintSize = aggregate.size;
            result.FoundationY = foundationCandidates[
                foundationCandidates.Count / 2];
            result.FootprintRadius = new Vector2(
                aggregate.extents.x,
                aggregate.extents.z).magnitude;

            if (!IsFinite(result.FootprintRadius) ||
                result.FootprintRadius <= 0f)
            {
                failures.Add("aggregate horizontal radius is invalid");
            }
            else if (result.FootprintRadius > MaximumRuntimeRadius)
            {
                failures.Add(
                    "authored footprint radius " +
                    result.FootprintRadius.ToString("F1",
                        CultureInfo.InvariantCulture) +
                    "m exceeds the 225m beta runtime envelope");
            }
        }

        bool geometryAccepted = failures.Count == 0;
        result.SpatiallyValidated = geometryAccepted;
        result.SeamlessPlacementEligible = geometryAccepted &&
            presentationMode ==
                YQWorldSitePresentationMode.SeamlessExterior &&
            result.FootprintSize.x <= MaximumSeamlessDimension &&
            result.FootprintSize.z <= MaximumSeamlessDimension &&
            result.FootprintSize.y <= MaximumSeamlessVerticalSpan;

        if (geometryAccepted &&
            presentationMode ==
                YQWorldSitePresentationMode.SeamlessExterior &&
            !result.SeamlessPlacementEligible)
        {
            failures.Add(
                "seamless footprint " +
                FormatVector(result.FootprintSize) +
                " exceeds the 461m horizontal or 140m vertical beta envelope");
            result.SpatiallyValidated = false;
        }

        result.ValidationFailure = string.Join("; ", failures);
        AppendSignatureValue(signatureSource,
            FormatVector(result.FootprintCenter));
        AppendSignatureValue(signatureSource,
            FormatVector(result.FootprintSize));
        AppendSignatureValue(signatureSource,
            FormatFloat(result.FoundationY));
        AppendSignatureValue(signatureSource,
            FormatFloat(result.FootprintRadius));
        AppendSignatureValue(signatureSource,
            result.ActiveCellCount.ToString(CultureInfo.InvariantCulture));
        AppendSignatureValue(signatureSource,
            result.ActiveInstanceCount.ToString(CultureInfo.InvariantCulture));
        result.Signature = Hash128.Compute(signatureSource.ToString())
            .ToString();
        return result;
    }

    private static void AnalyzeStreamingCells(
        YQReviewedSemanticSiteManifest semantic,
        YQAuthoredSiteStreamingManifest streaming,
        StringBuilder signatureSource,
        List<string> failures,
        List<float> foundationCandidates,
        ref Bounds aggregate,
        ref bool aggregateInitialized,
        ref long instanceTotal,
        YQRuntimeWorldSiteSpatialMetadata result)
    {
        if (streaming == null)
        {
            failures.Add("approved streaming manifest is missing");
            return;
        }

        HashSet<string> activeIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        bool duplicateAssignment = false;

        for (int zoneIndex = 0;
             zoneIndex < semantic.Zones.Count;
             zoneIndex++)
        {
            YQReviewedSemanticZoneRecord zone = semantic.Zones[zoneIndex];

            if (zone == null || zone.streamingCellIds == null)
            {
                failures.Add("semantic zone " + zoneIndex +
                    " has no streaming-cell selection");
                continue;
            }

            for (int cellIndex = 0;
                 cellIndex < zone.streamingCellIds.Count;
                 cellIndex++)
            {
                string id = zone.streamingCellIds[cellIndex];

                if (string.IsNullOrWhiteSpace(id))
                {
                    failures.Add("semantic zone " + zoneIndex +
                        " contains an empty active-cell ID");
                    continue;
                }

                if (!activeIds.Add(id))
                    duplicateAssignment = true;
            }
        }

        if (duplicateAssignment)
            failures.Add("an active streaming cell is assigned more than once");

        if (activeIds.Count == 0)
        {
            failures.Add("semantic zones select no active streaming cells");
            return;
        }

        Dictionary<string, YQAuthoredSiteStreamingCellRecord> available =
            new Dictionary<string, YQAuthoredSiteStreamingCellRecord>(
                StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < streaming.Cells.Count; index++)
        {
            YQAuthoredSiteStreamingCellRecord cell = streaming.Cells[index];

            if (cell == null || string.IsNullOrWhiteSpace(cell.StableCellId))
            {
                failures.Add("streaming manifest contains a null or unnamed cell");
                continue;
            }

            if (!available.TryAdd(cell.StableCellId, cell))
                failures.Add("streaming cell ID is duplicated: " +
                    cell.StableCellId);
        }

        List<string> orderedIds = activeIds
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        result.ActiveCellCount = orderedIds.Count;

        for (int index = 0; index < orderedIds.Count; index++)
        {
            string id = orderedIds[index];

            if (!available.TryGetValue(id, out
                    YQAuthoredSiteStreamingCellRecord cell))
            {
                failures.Add("semantic selection references missing cell " + id);
                continue;
            }

            IncludeStreamingCell(
                cell,
                signatureSource,
                failures,
                foundationCandidates,
                ref aggregate,
                ref aggregateInitialized,
                ref instanceTotal,
                result);
        }
    }

    private static void IncludeStreamingCell(
        YQAuthoredSiteStreamingCellRecord cell,
        StringBuilder signatureSource,
        List<string> failures,
        List<float> foundationCandidates,
        ref Bounds aggregate,
        ref bool aggregateInitialized,
        ref long instanceTotal,
        YQRuntimeWorldSiteSpatialMetadata result)
    {
        string prefabIdentity = GetAssetIdentity(cell.CellPrefab);
        AppendSignatureValue(signatureSource, cell.StableCellId);
        AppendSignatureValue(signatureSource, prefabIdentity);
        AppendSignatureValue(signatureSource,
            FormatVector(cell.AuthoredLocalPosition));
        AppendSignatureValue(signatureSource,
            FormatVector(cell.LocalBoundsCenter));
        AppendSignatureValue(signatureSource,
            FormatVector(cell.LocalBoundsSize));
        AppendSignatureValue(signatureSource,
            cell.SourceInstanceCount.ToString(CultureInfo.InvariantCulture));
        instanceTotal += Mathf.Max(0, cell.SourceInstanceCount);

        if (cell.CellPrefab == null)
        {
            failures.Add("active cell " + cell.StableCellId +
                " has no runtime prefab");
            return;
        }

        if (!IsValidBounds(
                cell.AuthoredLocalPosition,
                cell.LocalBoundsCenter,
                cell.LocalBoundsSize))
        {
            failures.Add("active cell " + cell.StableCellId +
                " has nonfinite or invalid authored bounds");
            return;
        }

        IncludeBounds(
            new Bounds(
                cell.AuthoredLocalPosition + cell.LocalBoundsCenter,
                cell.LocalBoundsSize),
            foundationCandidates,
            ref aggregate,
            ref aggregateInitialized);
    }

    private static void AnalyzeLegacyZones(
        YQReviewedSemanticSiteManifest semantic,
        StringBuilder signatureSource,
        List<string> failures,
        List<float> foundationCandidates,
        ref Bounds aggregate,
        ref bool aggregateInitialized,
        ref long instanceTotal,
        YQRuntimeWorldSiteSpatialMetadata result)
    {
        HashSet<string> zoneIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        List<YQReviewedSemanticZoneRecord> orderedZones = semantic.Zones
            .Where(zone => zone != null)
            .OrderBy(zone => zone.stableId,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedZones.Count != semantic.Zones.Count)
            failures.Add("legacy semantic selection contains a null zone");

        result.ActiveCellCount = semantic.Zones.Count;

        for (int index = 0; index < orderedZones.Count; index++)
        {
            YQReviewedSemanticZoneRecord zone = orderedZones[index];
            string id = zone.stableId ?? string.Empty;
            AppendSignatureValue(signatureSource, id);
            AppendSignatureValue(signatureSource, GetAssetIdentity(zone.prefab));
            AppendSignatureValue(signatureSource,
                FormatVector(zone.authoredSourceOrigin));
            AppendSignatureValue(signatureSource,
                FormatVector(zone.localBoundsCenter));
            AppendSignatureValue(signatureSource,
                FormatVector(zone.localBoundsSize));
            AppendSignatureValue(signatureSource,
                zone.sourceInstanceCount.ToString(
                    CultureInfo.InvariantCulture));
            instanceTotal += Mathf.Max(0, zone.sourceInstanceCount);

            if (zone.sourceInstanceCount < 0)
                failures.Add("legacy zone " + id +
                    " has a negative source-instance count");

            if (string.IsNullOrWhiteSpace(id) || !zoneIds.Add(id))
            {
                failures.Add("legacy zone has an empty or duplicate stable ID");
                continue;
            }

            if (zone.prefab == null)
            {
                failures.Add("legacy zone " + id + " has no runtime prefab");
                continue;
            }

            if (!IsValidBounds(
                    zone.authoredSourceOrigin,
                    zone.localBoundsCenter,
                    zone.localBoundsSize))
            {
                failures.Add("legacy zone " + id +
                    " has nonfinite or invalid authored bounds");
                continue;
            }

            IncludeBounds(
                new Bounds(
                    zone.authoredSourceOrigin + zone.localBoundsCenter,
                    zone.localBoundsSize),
                foundationCandidates,
                ref aggregate,
                ref aggregateInitialized);
        }
    }

    private static void IncludeBounds(
        Bounds candidate,
        List<float> foundationCandidates,
        ref Bounds aggregate,
        ref bool aggregateInitialized)
    {
        // note: One aggregate owns the cached footprint, floor, radius, and eligibility decision; no distant context cell can be omitted from only one of those calculations.
        foundationCandidates.Add(candidate.min.y);

        if (!aggregateInitialized)
        {
            aggregate = candidate;
            aggregateInitialized = true;
            return;
        }

        aggregate.Encapsulate(candidate);
    }

    private static bool IsValidBounds(
        Vector3 authoredPosition,
        Vector3 localCenter,
        Vector3 localSize)
    {
        Vector3 authoredCenter = authoredPosition + localCenter;
        return IsFinite(authoredPosition) && IsFinite(localCenter) &&
            IsFinite(localSize) && IsFinite(authoredCenter) &&
            localSize.x > MinimumBoundsDimension &&
            localSize.y > MinimumBoundsDimension &&
            localSize.z > MinimumBoundsDimension;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string GetAssetIdentity(UnityEngine.Object asset)
    {
        if (asset == null)
            return "missing";

        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                asset,
                out string guid,
                out long localId))
        {
            return guid + ":" +
                localId.ToString(CultureInfo.InvariantCulture);
        }

        return asset.name ?? string.Empty;
    }

    private static void AppendSignatureValue(
        StringBuilder builder,
        string value)
    {
        string safe = value ?? string.Empty;
        builder.Append(safe.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(safe);
        builder.Append('|');
    }

    private static string FormatVector(Vector3 value)
    {
        return FormatFloat(value.x) + "," +
            FormatFloat(value.y) + "," +
            FormatFloat(value.z);
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}

public sealed class YQRuntimeWorldSitePostflightResult
{
    public readonly List<string> errors = new List<string>();
    public readonly List<string> warnings = new List<string>();
    public int validatedSites;

    public bool Passed => errors.Count == 0;
}

public static class YQRuntimeWorldSitePostflightValidator
{
    public const string ReportPath =
        "Assets/Assets/GeneratedAssets/WorldIntake/YQRuntimeWorldSitePostflight.md";

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Production Queue/Runtime/Run Generative Readiness Postflight")]
    public static void RunGenerativeReadinessPostflight()
    {
        YQRuntimeWorldSitePostflightResult result = Run(true);

        if (!result.Passed)
        {
            Debug.LogError(
                "[YQRuntimeWorldSitePostflightValidator] GENERATIVE READINESS REJECTED\n" +
                string.Join("\n", result.errors));
        }
    }

    public static YQRuntimeWorldSitePostflightResult Run(bool logResult)
    {
        YQRuntimeWorldSitePostflightResult result =
            new YQRuntimeWorldSitePostflightResult();
        YQRuntimeWorldSiteCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQRuntimeWorldSiteCatalog>(
                YQRuntimeWorldSiteCatalogBuilder.CatalogPath);
        YQSemanticExtractionProfileCatalog profiles =
            AssetDatabase.LoadAssetAtPath<YQSemanticExtractionProfileCatalog>(
                YQSemanticExtractionProfileBuilder.CatalogPath);

        if (catalog == null)
        {
            result.errors.Add("Runtime world-site catalog is missing.");
            WriteReport(result, 0);
            return result;
        }

        int catalogEntryCount = catalog.Sites.Count;
        List<YQRuntimeWorldSiteRecord> catalogRecords =
            catalog.Sites.ToList();

        // note: An empty allow-list is never a valid release even though it has no individual records capable of producing validation errors.
        if (catalogEntryCount == 0)
            result.errors.Add("Runtime world-site catalog contains no approved sites.");

        HashSet<string> kitIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> resourceKeys = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        YQRuntimeWorldSiteQuery allSitesQuery =
            new YQRuntimeWorldSiteQuery();

        if (catalog.FindCompatibleSites(allSitesQuery).Count !=
            catalogEntryCount)
        {
            result.errors.Add(
                "The unfiltered semantic query does not return the complete runtime allow-list.");
        }

        for (int queryIndex = 0;
             queryIndex < catalogRecords.Count;
             queryIndex++)
        {
            YQRuntimeWorldSiteRecord queryRecord =
                catalogRecords[queryIndex];

            if (queryRecord != null)
                ValidateSemanticQuery(catalog, queryRecord,
                    queryRecord.kitId, result);
        }

        for (int index = 0; index < catalogRecords.Count; index++)
        {
            EditorUtility.DisplayProgressBar(
                "YourQuest Generative Readiness",
                "Auditing runtime site " + (index + 1) + "/" +
                catalogEntryCount,
                catalogEntryCount > 0
                    ? (float)index / catalogEntryCount
                    : 1f);
            YQRuntimeWorldSiteRecord record = catalogRecords[index];
            string label = record != null &&
                !string.IsNullOrWhiteSpace(record.kitId)
                    ? record.kitId
                    : "catalog entry " + index;

            if (record == null)
            {
                result.errors.Add(label + ": record is null.");
                continue;
            }

            ValidateRecord(
                record,
                label,
                profiles,
                kitIds,
                resourceKeys,
                result);

            // note: Postflight validates one dependency graph at a time and immediately releases it, preventing a full-catalog geometry load.
            GC.Collect();
            EditorUtility.UnloadUnusedAssetsImmediate(true);
        }

        EditorUtility.ClearProgressBar();

        WriteReport(result, catalogEntryCount);

        if (logResult)
        {
            string message =
                "[YQRuntimeWorldSitePostflightValidator] GENERATIVE READINESS " +
                (result.Passed ? "PASSED" : "REJECTED") + "\n" +
                "Validated runtime sites: " + result.validatedSites + "\n" +
                "Errors: " + result.errors.Count + "\n" +
                "Warnings: " + result.warnings.Count + "\n" +
                "Report: " + ReportPath;

            if (result.Passed)
                Debug.Log(message);
            else
                Debug.LogError(message);
        }

        return result;
    }

    private static void ValidateRecord(
        YQRuntimeWorldSiteRecord record,
        string label,
        YQSemanticExtractionProfileCatalog profiles,
        HashSet<string> kitIds,
        HashSet<string> resourceKeys,
        YQRuntimeWorldSitePostflightResult result)
    {
        int startingErrorCount = result.errors.Count;

        if (string.IsNullOrWhiteSpace(record.kitId))
            result.errors.Add(label + ": kit ID is empty.");
        else if (!kitIds.Add(record.kitId))
            result.errors.Add(label + ": kit ID is duplicated.");

        if (string.IsNullOrWhiteSpace(record.semanticStyleKey))
            result.errors.Add(label + ": semantic style key is empty.");

        if (record.siteKind == YQAuthoredSiteKind.Unknown)
            result.errors.Add(label + ": site kind is unknown.");

        if (record.topology == YQSemanticExtractionTopology.Unknown)
            result.errors.Add(label + ": semantic topology is unknown.");

        if (record.presentationMode == YQWorldSitePresentationMode.Unknown)
            result.errors.Add(label + ": presentation mode is unknown.");

        if (record.streamingSite != null || record.semanticSite != null)
        {
            // note: Direct references here would make loading the catalog pull every site's geometry into RAM.
            result.errors.Add(label +
                ": runtime catalog contains a forbidden direct manifest reference.");
        }

        if (string.IsNullOrWhiteSpace(record.runtimeManifestResourceKey) ||
            !record.runtimeManifestResourceKey.StartsWith(
                "YQWorldSites/",
                StringComparison.Ordinal) ||
            record.runtimeManifestResourceKey.Contains(".") ||
            record.runtimeManifestResourceKey.Contains("\\"))
        {
            result.errors.Add(label +
                ": runtime resource key is missing or unsafe.");
            return;
        }

        if (!resourceKeys.Add(record.runtimeManifestResourceKey))
            result.errors.Add(label + ": runtime resource key is duplicated.");

        string runtimePath = "Assets/Assets/Resources/" +
            record.runtimeManifestResourceKey + ".asset";
        YQReviewedSemanticSiteManifest semantic =
            AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(
                runtimePath);

        if (semantic == null)
        {
            result.errors.Add(label +
                ": runtime semantic manifest is missing at " + runtimePath + ".");
            return;
        }

        bool legacyViking = string.Equals(
            record.kitId,
            "medieval_viking_village",
            StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(semantic.SourceSignature);
        YQAuthoredSiteStreamingManifest streaming = semantic.StreamingSite;

        if (streaming == null && legacyViking)
        {
            streaming = AssetDatabase.LoadAssetAtPath<
                YQAuthoredSiteStreamingManifest>(
                    "Assets/Assets/GeneratedAssets/WorldAssemblies/StreamingSites/" +
                    record.kitId + "/YQ_" + record.kitId +
                    "_StreamingManifest.asset");
        }

        if (!semantic.ReleaseEligible)
            result.errors.Add(label + ": semantic manifest is not released.");

        if (streaming == null || !streaming.ReleaseEligible)
        {
            result.errors.Add(label +
                ": approved streaming source is missing or unreleased.");
            return;
        }

        if (!string.Equals(
                record.kitId,
                semantic.KitId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                record.semanticStyleKey,
                semantic.SemanticStyleKey,
                StringComparison.OrdinalIgnoreCase) ||
            record.topology != semantic.Topology)
        {
            result.errors.Add(label +
                ": catalog metadata does not match its semantic manifest.");
        }

        if (string.IsNullOrWhiteSpace(streaming.SemanticStyleKey) ||
            streaming.SiteKind == YQAuthoredSiteKind.Unknown)
        {
            result.errors.Add(label +
                ": streaming source identity is incomplete.");
        }

        if (record.presentationMode != streaming.PresentationMode)
        {
            result.errors.Add(label +
                ": runtime presentation mode does not match its streaming authority.");
        }

        // note: A released semantic descriptor must refer to the exact reviewed source revision, never merely a pack with the same display identity.
        if (!legacyViking && !string.Equals(
                semantic.SourceSignature,
                streaming.SourceSignature,
                StringComparison.Ordinal))
        {
            result.errors.Add(label +
                ": semantic descriptor was built from a stale streaming source revision.");
        }

        ValidateSpatialMetadata(
            record,
            semantic,
            streaming,
            label,
            legacyViking,
            result);
        ValidateCoverage(
            record,
            semantic,
            streaming,
            label,
            legacyViking,
            result);
        ValidateProfile(
            record,
            semantic,
            streaming,
            profiles,
            label,
            result);
        if (record.structureUsagePolicy ==
            YQWorldStructureUsagePolicy.Unspecified)
        {
            result.warnings.Add(label +
                ": structure usage remains unspecified; generation must treat structures as non-enterable by default.");
        }

        if (startingErrorCount == result.errors.Count)
            result.validatedSites++;
    }

    private static void ValidateSpatialMetadata(
        YQRuntimeWorldSiteRecord record,
        YQReviewedSemanticSiteManifest semantic,
        YQAuthoredSiteStreamingManifest streaming,
        string label,
        bool legacyViking,
        YQRuntimeWorldSitePostflightResult result)
    {
        // note: Postflight recomputes every reviewed active cell, proving the lightweight catalog cache still represents the exact geometry runtime will instantiate.
        YQRuntimeWorldSiteSpatialMetadata expected =
            YQRuntimeWorldSiteSpatialMetadataCompiler.Analyze(
                semantic,
                streaming,
                legacyViking);

        if (!string.Equals(
                record.spatialMetadataVersion,
                YQRuntimeWorldSiteSpatialMetadataCompiler.MetadataVersion,
                StringComparison.Ordinal))
        {
            result.errors.Add(label +
                ": cached spatial metadata version is missing or stale.");
        }

        if (!expected.SpatiallyValidated)
        {
            result.errors.Add(label +
                ": reviewed active geometry is not runtime-safe: " +
                (string.IsNullOrWhiteSpace(expected.ValidationFailure)
                    ? "unspecified spatial validation failure."
                    : expected.ValidationFailure + "."));
        }

        if (streaming.PresentationMode ==
                YQWorldSitePresentationMode.SeamlessExterior &&
            !expected.SeamlessPlacementEligible)
        {
            result.errors.Add(label +
                ": reviewed exterior is not eligible for seamless generated-terrain placement.");
        }

        if (record.spatiallyValidated != expected.SpatiallyValidated ||
            record.seamlessPlacementEligible !=
                expected.SeamlessPlacementEligible)
        {
            result.errors.Add(label +
                ": cached spatial eligibility flags do not match the reviewed active geometry.");
        }

        if (!Approximately(
                record.authoredFootprintCenter,
                expected.FootprintCenter) ||
            !Approximately(
                record.authoredFootprintSize,
                expected.FootprintSize) ||
            !Approximately(
                record.authoredFoundationY,
                expected.FoundationY) ||
            !Approximately(
                record.authoredFootprintRadius,
                expected.FootprintRadius))
        {
            result.errors.Add(label +
                ": cached authored footprint or foundation does not match the reviewed active-cell union.");
        }

        if (record.activeCellCount != expected.ActiveCellCount ||
            record.activeInstanceCount != expected.ActiveInstanceCount)
        {
            result.errors.Add(label +
                ": cached active cell/instance complexity is stale.");
        }

        if (string.IsNullOrWhiteSpace(record.spatialSignature) ||
            !string.Equals(
                record.spatialSignature,
                expected.Signature,
                StringComparison.Ordinal))
        {
            result.errors.Add(label +
                ": deterministic spatial signature is missing or stale.");
        }

        if (!string.Equals(
                record.spatialValidationFailure ?? string.Empty,
                expected.ValidationFailure ?? string.Empty,
                StringComparison.Ordinal))
        {
            result.errors.Add(label +
                ": cached spatial validation diagnostic is stale.");
        }
    }

    private static bool Approximately(Vector3 left, Vector3 right)
    {
        return Approximately(left.x, right.x) &&
            Approximately(left.y, right.y) &&
            Approximately(left.z, right.z);
    }

    private static bool Approximately(float left, float right)
    {
        if (float.IsNaN(left) || float.IsInfinity(left) ||
            float.IsNaN(right) || float.IsInfinity(right))
        {
            return false;
        }

        return Mathf.Abs(left - right) <= 0.001f;
    }

    private static void ValidateCoverage(
        YQRuntimeWorldSiteRecord record,
        YQReviewedSemanticSiteManifest semantic,
        YQAuthoredSiteStreamingManifest streaming,
        string label,
        bool legacyViking,
        YQRuntimeWorldSitePostflightResult result)
    {
        if (legacyViking)
        {
            int legacyInstances = 0;
            HashSet<string> legacyTags = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            for (int zoneIndex = 0;
                 zoneIndex < semantic.Zones.Count;
                 zoneIndex++)
            {
                YQReviewedSemanticZoneRecord zone = semantic.Zones[zoneIndex];

                if (zone == null || zone.prefab == null)
                    result.errors.Add(label +
                        ": legacy reviewed district is missing its prefab.");
                else
                {
                    legacyInstances += zone.sourceInstanceCount;
                    legacyTags.UnionWith(zone.semanticTags);
                }
            }

            // note: The original reviewed Viking district set predates streaming-cell IDs but remains valid through its reviewed prefab-backed district contract.
            if (legacyInstances != semantic.SourceInstanceCount)
                result.errors.Add(label +
                    ": legacy reviewed district instance totals are not preserved.");

            HashSet<string> legacyCatalogTags = new HashSet<string>(
                record.semanticTags ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            if (!legacyCatalogTags.SetEquals(legacyTags))
                result.errors.Add(label +
                    ": legacy runtime semantic tag index is stale or incomplete.");

            return;
        }

        HashSet<string> expected = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        int expectedInstances = 0;

        for (int index = 0; index < streaming.Cells.Count; index++)
        {
            YQAuthoredSiteStreamingCellRecord cell = streaming.Cells[index];

            if (cell == null || string.IsNullOrWhiteSpace(cell.StableCellId))
            {
                result.errors.Add(label +
                    ": streaming manifest contains an invalid cell record.");
                continue;
            }

            if (cell.CellPrefab == null)
                result.errors.Add(label + ": cell has no prefab: " +
                    cell.StableCellId + ".");

            if (!expected.Add(cell.StableCellId))
                result.errors.Add(label + ": duplicate streaming cell ID: " +
                    cell.StableCellId + ".");

            expectedInstances += cell.SourceInstanceCount;
        }

        HashSet<string> assigned = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> manifestTags = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        int assignedInstances = 0;

        for (int zoneIndex = 0;
             zoneIndex < semantic.Zones.Count;
             zoneIndex++)
        {
            YQReviewedSemanticZoneRecord zone = semantic.Zones[zoneIndex];

            if (zone == null || zone.streamingCellIds.Count == 0)
            {
                result.errors.Add(label +
                    ": semantic zone has no streaming-cell coverage.");
                continue;
            }

            assignedInstances += zone.sourceInstanceCount;
            manifestTags.UnionWith(zone.semanticTags);

            for (int cellIndex = 0;
                 cellIndex < zone.streamingCellIds.Count;
                 cellIndex++)
            {
                string cellId = zone.streamingCellIds[cellIndex];

                if (!assigned.Add(cellId))
                    result.errors.Add(label +
                        ": cell is assigned to multiple semantic zones: " +
                        cellId + ".");
            }
        }

        if (!expected.SetEquals(assigned))
            result.errors.Add(label +
                ": semantic zones do not exactly cover the streaming cells.");

        if (expectedInstances != assignedInstances ||
            semantic.SourceInstanceCount != assignedInstances)
        {
            result.errors.Add(label +
                ": authored instance totals are not preserved.");
        }

        HashSet<string> catalogTags = new HashSet<string>(
            record.semanticTags ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        if (!catalogTags.SetEquals(manifestTags))
            result.errors.Add(label +
                ": runtime semantic tag index is stale or incomplete.");
    }

    private static void ValidateProfile(
        YQRuntimeWorldSiteRecord record,
        YQReviewedSemanticSiteManifest semantic,
        YQAuthoredSiteStreamingManifest streaming,
        YQSemanticExtractionProfileCatalog profiles,
        string label,
        YQRuntimeWorldSitePostflightResult result)
    {
        YQSemanticExtractionProfile profile = profiles?.Find(record.kitId);

        if (profile == null)
        {
            result.errors.Add(label + ": semantic extraction profile is missing.");
            return;
        }

        if (profile.requiresManualProfileReview || !profile.authoredOverride)
            result.errors.Add(label + ": semantic profile is not authored and approved.");

        if (profile.topology != record.topology)
            result.errors.Add(label + ": runtime topology differs from its profile.");

        if (!string.Equals(
                profile.semanticStyleKey,
                record.semanticStyleKey,
                StringComparison.OrdinalIgnoreCase) ||
            profile.siteKind != record.siteKind)
        {
            result.errors.Add(label +
                ": generative style or site kind differs from its authored profile.");
        }

        YQWorldStructureUsagePolicy expectedStructurePolicy =
            ResolveExpectedStructurePolicy(streaming, profile);
        int expectedMaximumEnterable = ResolveExpectedMaximumEnterable(
            streaming,
            profile,
            expectedStructurePolicy);

        if (record.structureUsagePolicy != expectedStructurePolicy ||
            record.maximumEnterableStructures != expectedMaximumEnterable)
        {
            result.errors.Add(label +
                ": runtime structure safety policy is stale or inconsistent.");
        }

        HashSet<string> availableTags = new HashSet<string>(
            record.semanticTags ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        for (int index = 0;
             index < profile.requiredSemanticOutputs.Count;
             index++)
        {
            if (!availableTags.Contains(profile.requiredSemanticOutputs[index]))
            {
                result.errors.Add(label +
                    ": required generative role is unavailable: " +
                    profile.requiredSemanticOutputs[index] + ".");
            }
        }
    }

    private static YQWorldStructureUsagePolicy ResolveExpectedStructurePolicy(
        YQAuthoredSiteStreamingManifest streaming,
        YQSemanticExtractionProfile profile)
    {
        if (profile.structureUsagePolicy !=
            YQWorldStructureUsagePolicy.Unspecified)
        {
            return profile.structureUsagePolicy;
        }

        if (streaming.StructureUsagePolicy !=
            YQWorldStructureUsagePolicy.Unspecified)
        {
            return streaming.StructureUsagePolicy;
        }

        return streaming.PresentationMode ==
                YQWorldSitePresentationMode.SeamlessExterior
            ? YQWorldStructureUsagePolicy.ExteriorShellsOnly
            : YQWorldStructureUsagePolicy.FullyEnterable;
    }

    private static int ResolveExpectedMaximumEnterable(
        YQAuthoredSiteStreamingManifest streaming,
        YQSemanticExtractionProfile profile,
        YQWorldStructureUsagePolicy expectedPolicy)
    {
        if (profile.structureUsagePolicy !=
            YQWorldStructureUsagePolicy.Unspecified)
        {
            return Mathf.Max(0, profile.maximumEnterableStructures);
        }

        if (streaming.StructureUsagePolicy !=
            YQWorldStructureUsagePolicy.Unspecified)
        {
            return Mathf.Max(0, streaming.MaximumEnterableStructures);
        }

        return expectedPolicy == YQWorldStructureUsagePolicy.FullyEnterable
            ? 1
            : 0;
    }

    private static void ValidateSemanticQuery(
        YQRuntimeWorldSiteCatalog catalog,
        YQRuntimeWorldSiteRecord record,
        string label,
        YQRuntimeWorldSitePostflightResult result)
    {
        YQRuntimeWorldSiteQuery query = new YQRuntimeWorldSiteQuery
        {
            semanticStyleKey = record.semanticStyleKey,
            siteKind = record.siteKind,
            topology = record.topology,
            requiredSemanticTags = record.semanticTags != null &&
                record.semanticTags.Count > 0
                ? new List<string> { record.semanticTags[0] }
                : new List<string>()
        };
        IReadOnlyList<YQRuntimeWorldSiteRecord> matches =
            catalog.FindCompatibleSites(query);

        if (!matches.Any(match => string.Equals(
                match.kitId,
                record.kitId,
                StringComparison.OrdinalIgnoreCase)))
        {
            result.errors.Add(label +
                ": structured generative query cannot resolve this site.");
        }
    }

    private static void WriteReport(
        YQRuntimeWorldSitePostflightResult result,
        int catalogEntryCount)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("# YourQuest Runtime World-Site Postflight");
        report.AppendLine();
        report.AppendLine("- Status: **" +
            (result.Passed ? "PASS" : "REJECTED") + "**");
        report.AppendLine("- Runtime catalog entries: " +
            catalogEntryCount);
        report.AppendLine("- Fully validated sites: " +
            result.validatedSites);
        report.AppendLine("- Errors: " + result.errors.Count);
        report.AppendLine("- Warnings: " + result.warnings.Count);
        report.AppendLine();
        report.AppendLine(
            "The runtime catalog exposes semantic style, site kind, topology, role tags, and a deterministic cached footprint measured from every reviewed active cell. It stores no direct world-manifest references, so the generative system can reject unsafe placement without choosing asset paths or loading every pack at once.");

        AppendSection(report, "Errors", result.errors);
        AppendSection(report, "Warnings", result.warnings);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report.ToString());
        AssetDatabase.ImportAsset(ReportPath);
    }

    private static void AppendSection(
        StringBuilder report,
        string heading,
        IReadOnlyList<string> entries)
    {
        report.AppendLine();
        report.AppendLine("## " + heading);
        report.AppendLine();

        if (entries.Count == 0)
        {
            report.AppendLine("None.");
            return;
        }

        for (int index = 0; index < entries.Count; index++)
            report.AppendLine("- " + entries[index]);
    }
}
