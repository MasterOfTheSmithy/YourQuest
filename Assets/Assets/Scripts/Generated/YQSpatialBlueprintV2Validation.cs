using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public enum YQSpatialBlueprintFailureV2
{
    None = 0,
    MissingBlueprint = 1,
    InvalidWorldBounds = 2,
    MissingOrigin = 3,
    DuplicateId = 4,
    InvalidRegion = 5,
    MissingTerrainLayer = 6,
    InvalidTerrainField = 7,
    InvalidHydrology = 8,
    UphillHydrology = 9,
    InvalidSite = 10,
    OverlappingSiteReserve = 11,
    InvalidRoute = 12,
    MissingWaterCrossing = 13,
    InvalidRelationship = 14,
    DisconnectedSettlement = 15,
    InsufficientWaterAccess = 16
}

public sealed class YQSpatialBlueprintValidationResultV2
{
    public readonly List<string> Errors = new List<string>();

    public bool Accepted => Errors.Count == 0;

    public void Add(YQSpatialBlueprintFailureV2 failure, string subjectId)
    {
        string value = failure + ":" + (subjectId ?? string.Empty);
        if (!Errors.Contains(value))
            Errors.Add(value);
    }
}

public static class YQSpatialBlueprintValidatorV2
{
    public static YQSpatialBlueprintValidationResultV2 Validate(
        GeneratedSpatialWorldPlanV2Record record)
    {
        YQSpatialBlueprintValidationResultV2 result =
            new YQSpatialBlueprintValidationResultV2();

        if (record == null || record.blueprint == null)
        {
            result.Add(YQSpatialBlueprintFailureV2.MissingBlueprint, "world");
            return result;
        }

        record.EnsureCollections();
        YQSpatialBlueprintV2 blueprint = record.blueprint;
        float halfExtent = blueprint.worldSize * 0.5f;

        if (!IsFinite(blueprint.worldSize) || blueprint.worldSize < 256f)
        {
            result.Add(YQSpatialBlueprintFailureV2.InvalidWorldBounds, "world");
            return result;
        }

        HashSet<string> regionIds = ValidateRegions(
            blueprint,
            halfExtent,
            result);
        HashSet<string> terrainIds = ValidateTerrain(
            blueprint,
            regionIds,
            halfExtent,
            result);
        Dictionary<string, YQHydrologyFeatureV2> hydrology =
            ValidateHydrology(
                blueprint,
                regionIds,
                terrainIds,
                halfExtent,
                result);
        // note: New drafts must meet the metre-based drainage gate, while reading accepted older versions preserves their stored physical contract and hash.
        bool basinContract = GeneratedSpatialWorldPlanV2Record.UsesBasinMetadata(record.generationVersion);
        bool physicalDrainageGate = basinContract || record.validationVersion == GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion;
        if (basinContract || record.generationVersion == GeneratedSpatialWorldPlanV2Record.CompatibleAcceptedGenerationVersion)
            ValidateReceivingWatershedCore(hydrology, result, physicalDrainageGate);
        if (physicalDrainageGate)
            ValidatePhysicalDrainage(hydrology, result);
        if (basinContract)
            ValidateBasinConnectivity(hydrology, result);
        if (record.generationVersion == GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion)
            ValidateConnectedBasins(hydrology, halfExtent, result);
        else
            foreach (var feature in hydrology.Values)
                if (feature.hasConnectedDomain)
                    result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, feature.hydrologyId + ":connected_domain_requires_topology_12");
        Dictionary<string, YQSiteAnchorV2> sites = ValidateSites(
            blueprint,
            regionIds,
            halfExtent,
            result);
        HashSet<string> routeIds = ValidateRoutes(
            blueprint,
            sites,
            hydrology,
            halfExtent,
            result);
        ValidateRelationships(
            blueprint,
            regionIds,
            terrainIds,
            hydrology,
            sites,
            routeIds,
            result);
        ValidateSettlementConnectivity(blueprint, sites, result);
        // note: A geometrically well-formed plan is not acceptable if its sites will fail the same water-access requirement during materialization.
        if (result.Accepted) ValidateWaterAccess(blueprint, result);
        return result;
    }

    private static void ValidateWaterAccess(YQSpatialBlueprintV2 blueprint, YQSpatialBlueprintValidationResultV2 result)
    {
        // note: One bounded prepared query keeps acceptance linear in site count and identical to planner/runtime bank measurements.
        if (!YQSpatialSiteAccessV2.TryCreateQuery(
                blueprint,
                out YQSpatialSiteAccessQueryV2 query,
                out string queryFailure))
        {
            result.Add(
                YQSpatialBlueprintFailureV2.InsufficientWaterAccess,
                queryFailure);
            return;
        }
        foreach (YQSiteAnchorV2 site in blueprint.sites)
        {
            if (site.kind != YQSiteKindV2.Settlement &&
                site.minimumWaterAccess <= 0f)
            {
                continue;
            }
            if (!query.TryMeasure(
                    site.siteId,
                    site.x,
                    site.z,
                    site.reservedRadius,
                    out float access,
                    out float distance,
                    out _,
                    out bool centerInsideWater,
                    out string failure))
            {
                result.Add(YQSpatialBlueprintFailureV2.InsufficientWaterAccess, site.siteId + "|" + failure);
                continue;
            }
            float requiredDryBank = site.kind == YQSiteKindV2.Settlement
                ? site.minimumWaterAccess > 0.5f
                    ? 2f
                    : site.reservedRadius
                : 0f;
            if (site.kind == YQSiteKindV2.Settlement &&
                (centerInsideWater || distance + 0.001f < requiredDryBank))
            {
                // note: Inland settlements keep their complete construction reserve dry; explicit waterfront settlements retain access while keeping the anchor out of the water footprint.
                result.Add(
                    YQSpatialBlueprintFailureV2.InsufficientWaterAccess,
                    site.siteId + "|dry-bank-m=" +
                    distance.ToString("F2", CultureInfo.InvariantCulture) +
                    "|required-dry-bank-m=" +
                    requiredDryBank.ToString("F2", CultureInfo.InvariantCulture));
                continue;
            }
            if (access + 0.001f < site.minimumWaterAccess)
                result.Add(YQSpatialBlueprintFailureV2.InsufficientWaterAccess, site.siteId + "|actual=" +
                    access.ToString("F3", CultureInfo.InvariantCulture) + "|required=" + site.minimumWaterAccess.ToString("F3", CultureInfo.InvariantCulture) +
                    "|nearest-bank-m=" + distance.ToString("F2", CultureInfo.InvariantCulture));
        }
    }

    private static HashSet<string> ValidateRegions(
        YQSpatialBlueprintV2 blueprint,
        float halfExtent,
        YQSpatialBlueprintValidationResultV2 result)
    {
        HashSet<string> ids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < blueprint.regions.Count; index++)
        {
            YQRegionDomainV2 region = blueprint.regions[index];
            string id = region != null ? region.regionId : string.Empty;
            bool duplicate = !string.IsNullOrWhiteSpace(id) && !ids.Add(id);

            if (region == null ||
                string.IsNullOrWhiteSpace(id) ||
                duplicate ||
                !IsPointInBounds(region.centerX, region.centerZ, halfExtent) ||
                !IsPositiveFinite(region.radius) ||
                !IsNormalized(region.moisture) ||
                !IsNormalized(region.ruggedness) ||
                !IsNormalized(region.civilizationDensity) ||
                !IsNormalized(region.danger))
            {
                result.Add(
                    duplicate
                        ? YQSpatialBlueprintFailureV2.DuplicateId
                        : YQSpatialBlueprintFailureV2.InvalidRegion,
                    id);
            }
        }

        return ids;
    }

    private static HashSet<string> ValidateTerrain(
        YQSpatialBlueprintV2 blueprint,
        HashSet<string> regionIds,
        float halfExtent,
        YQSpatialBlueprintValidationResultV2 result)
    {
        HashSet<string> ids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<YQTerrainFieldKindV2> required =
            new HashSet<YQTerrainFieldKindV2>
            {
                YQTerrainFieldKindV2.BaseSurface,
                YQTerrainFieldKindV2.RidgeChain,
                YQTerrainFieldKindV2.ValleyCorridor,
                YQTerrainFieldKindV2.Basin
            };

        for (int index = 0; index < blueprint.terrainFields.Count; index++)
        {
            YQTerrainFieldV2 field = blueprint.terrainFields[index];
            string id = field != null ? field.fieldId : string.Empty;
            bool duplicate = !string.IsNullOrWhiteSpace(id) && !ids.Add(id);

            if (field != null)
            {
                field.EnsureCollections();
                required.Remove(field.kind);
            }

            if (field == null ||
                string.IsNullOrWhiteSpace(id) ||
                duplicate ||
                field.kind == YQTerrainFieldKindV2.Unknown ||
                (!string.IsNullOrWhiteSpace(field.parentRegionId) &&
                 !regionIds.Contains(field.parentRegionId)) ||
                !IsFinite(field.strength) ||
                !IsPositiveFinite(field.radius) ||
                !IsPositiveFinite(field.falloff) ||
                !HasValidPoints(field.controlPoints, halfExtent, 1))
            {
                result.Add(
                    duplicate
                        ? YQSpatialBlueprintFailureV2.DuplicateId
                        : YQSpatialBlueprintFailureV2.InvalidTerrainField,
                    id);
            }
        }

        foreach (YQTerrainFieldKindV2 kind in required)
        {
            result.Add(
                YQSpatialBlueprintFailureV2.MissingTerrainLayer,
                kind.ToString());
        }

        return ids;
    }

    private static Dictionary<string, YQHydrologyFeatureV2> ValidateHydrology(
        YQSpatialBlueprintV2 blueprint,
        HashSet<string> regionIds,
        HashSet<string> terrainIds,
        float halfExtent,
        YQSpatialBlueprintValidationResultV2 result)
    {
        Dictionary<string, YQHydrologyFeatureV2> features =
            new Dictionary<string, YQHydrologyFeatureV2>(
                StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < blueprint.hydrology.Count; index++)
        {
            YQHydrologyFeatureV2 feature = blueprint.hydrology[index];
            string id = feature != null
                ? feature.hydrologyId
                : string.Empty;
            bool duplicate = !string.IsNullOrWhiteSpace(id) &&
                             features.ContainsKey(id);

            if (!duplicate && !string.IsNullOrWhiteSpace(id) && feature != null)
                features.Add(id, feature);

            if (feature != null)
                feature.EnsureCollections();

            int minimumPoints = feature != null &&
                                feature.kind == YQHydrologyKindV2.Lake
                ? 1
                : 2;

            if (feature == null ||
                string.IsNullOrWhiteSpace(id) ||
                duplicate ||
                feature.kind == YQHydrologyKindV2.Unknown ||
                (!string.IsNullOrWhiteSpace(feature.parentRegionId) &&
                 !regionIds.Contains(feature.parentRegionId)) ||
                (!string.IsNullOrWhiteSpace(feature.sourceTerrainFieldId) &&
                 !terrainIds.Contains(feature.sourceTerrainFieldId)) ||
                !IsNormalized(feature.waterLevelNormalized) ||
                !IsPositiveFinite(feature.nominalWidth) ||
                !IsPositiveFinite(feature.nominalDepth) ||
                !HasValidPoints(
                    feature.controlPoints,
                    halfExtent,
                    minimumPoints))
            {
                result.Add(
                    duplicate
                        ? YQSpatialBlueprintFailureV2.DuplicateId
                        : YQSpatialBlueprintFailureV2.InvalidHydrology,
                    id);
                continue;
            }

            if ((feature.kind == YQHydrologyKindV2.River ||
                 feature.kind == YQHydrologyKindV2.Waterfall) &&
                !FlowsDownhill(feature.controlPoints))
            {
                result.Add(
                    YQSpatialBlueprintFailureV2.UphillHydrology,
                    id);
            }
        }

        for (int index = 0; index < blueprint.hydrology.Count; index++)
        {
            YQHydrologyFeatureV2 feature = blueprint.hydrology[index];
            if (feature != null &&
                !string.IsNullOrWhiteSpace(feature.sinkHydrologyId) &&
                !features.ContainsKey(feature.sinkHydrologyId))
            {
                result.Add(
                    YQSpatialBlueprintFailureV2.InvalidHydrology,
                    feature.hydrologyId);
            }
        }

        return features;
    }

    internal static void ValidateReceivingWatershed(Dictionary<string, YQHydrologyFeatureV2> features,
        YQSpatialBlueprintValidationResultV2 result)
    {
        // note: Retain the existing numeric-verifier boundary for legacy receiving-water contracts.
        ValidateReceivingWatershedCore(features, result, false);
    }

    private static void ValidateReceivingWatershedCore(Dictionary<string, YQHydrologyFeatureV2> features,
        YQSpatialBlueprintValidationResultV2 result, bool physicalDrainageGate)
    {
        // note: New drainage networks are directed, downhill and finite. Every channel must physically meet its receiver and terminate in a body of water; old accepted layouts are kept version-pinned.
        foreach (var entry in features)
        {
            var feature = entry.Value;
            // note: Basic validation records malformed features before topology checks; never dereference their invalid points or throw instead of returning a rejection.
            if (!HasUsableWaterPoints(feature))
            { result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":invalid_physical_points"); continue; }
            if (feature.kind != YQHydrologyKindV2.River && feature.kind != YQHydrologyKindV2.Waterfall) continue;
            if (string.IsNullOrWhiteSpace(feature.sinkHydrologyId) || !features.TryGetValue(feature.sinkHydrologyId, out var sink) ||
                !HasUsableWaterPoints(sink))
            { result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, feature.hydrologyId + ":missing_receiver"); continue; }
            var end = feature.controlPoints[feature.controlPoints.Count - 1];
            float nearest = float.PositiveInfinity, receiverLevel = 0f, receiverWidth = sink.nominalWidth;
            for (int i = 0; i < sink.controlPoints.Count; i++)
            {
                var first = sink.controlPoints[i]; var second = sink.controlPoints[Mathf.Min(i + 1, sink.controlPoints.Count - 1)];
                Vector2 a = new Vector2(first.x, first.z), delta = new Vector2(second.x - first.x, second.z - first.z);
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(end.x, end.z) - a, delta) / Mathf.Max(.0001f, delta.sqrMagnitude));
                float distance = Vector2.Distance(new Vector2(end.x, end.z), a + delta * t);
                if (distance >= nearest) continue;
                nearest = distance;
                // note: New area-water joins use the same level plane as physical drainage; older accepted contracts retain their control-point interpretation.
                receiverLevel = physicalDrainageGate && (sink.kind == YQHydrologyKindV2.Lake ||
                    sink.kind == YQHydrologyKindV2.Wetland || sink.kind == YQHydrologyKindV2.Coastline)
                    ? sink.waterLevelNormalized : Mathf.Lerp(first.normalizedElevation, second.normalizedElevation, t);
                receiverWidth = Mathf.Max(sink.nominalWidth, Mathf.Lerp(first.width, second.width, t));
            }
            if (nearest > receiverWidth * .5f + .5f || Mathf.Abs(end.normalizedElevation - receiverLevel) > .005f)
                result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, feature.hydrologyId + ":disconnected_receiver");
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = feature;
            while (current != null && (current.kind == YQHydrologyKindV2.River || current.kind == YQHydrologyKindV2.Waterfall))
            {
                if (!visited.Add(current.hydrologyId)) { result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, feature.hydrologyId + ":drainage_cycle"); break; }
                if (string.IsNullOrEmpty(current.sinkHydrologyId) || !features.TryGetValue(current.sinkHydrologyId, out current))
                { result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, feature.hydrologyId + ":no_receiving_body"); break; }
            }
            if (current != null && current.kind != YQHydrologyKindV2.River && current.kind != YQHydrologyKindV2.Waterfall &&
                current.kind != YQHydrologyKindV2.Lake && current.kind != YQHydrologyKindV2.Wetland && current.kind != YQHydrologyKindV2.Coastline)
                result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, feature.hydrologyId + ":invalid_receiving_body");
        }
        // note: Separate channels can join at endpoints, but cannot cross through each other or overlap as independent parallel sheets.
        var rivers = new List<YQHydrologyFeatureV2>();
        foreach (var feature in features.Values)
            if (HasUsableWaterPoints(feature) && feature.kind == YQHydrologyKindV2.River) rivers.Add(feature);
        for (int firstIndex = 0; firstIndex < rivers.Count; firstIndex++)
        for (int secondIndex = firstIndex + 1; secondIndex < rivers.Count; secondIndex++)
        {
            var first = rivers[firstIndex]; var second = rivers[secondIndex];
            for (int aIndex = 0; aIndex + 1 < first.controlPoints.Count; aIndex++)
            for (int bIndex = 0; bIndex + 1 < second.controlPoints.Count; bIndex++)
            {
                var ap = first.controlPoints[aIndex]; var aq = first.controlPoints[aIndex + 1];
                var bp = second.controlPoints[bIndex]; var bq = second.controlPoints[bIndex + 1];
                Vector2 a = new Vector2(ap.x, ap.z), da = new Vector2(aq.x - ap.x, aq.z - ap.z);
                Vector2 b = new Vector2(bp.x, bp.z), db = new Vector2(bq.x - bp.x, bq.z - bp.z), offset = b - a;
                float cross = da.x * db.y - da.y * db.x;
                if (da.sqrMagnitude < .001f || db.sqrMagnitude < .001f) continue;
                if (Mathf.Abs(cross) > .0001f)
                {
                    float at = (offset.x * db.y - offset.y * db.x) / cross;
                    float bt = (offset.x * da.y - offset.y * da.x) / cross;
                    bool firstInterior = at > .001f && at < .999f;
                    bool secondInterior = bt > .001f && bt < .999f;
                    if (firstInterior && secondInterior)
                        result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, first.hydrologyId + ":unjoined_crossing:" + second.hydrologyId);
                }
                else
                {
                    Vector2 axis = da.normalized;
                    float lateral = Mathf.Abs(offset.x * axis.y - offset.y * axis.x);
                    float start = Vector2.Dot(offset, axis), end = Vector2.Dot(b + db - a, axis);
                    float overlap = Mathf.Min(da.magnitude, Mathf.Max(start, end)) - Mathf.Max(0f, Mathf.Min(start, end));
                    float width = Mathf.Max(first.nominalWidth, Mathf.Max(ap.width, aq.width)) + Mathf.Max(second.nominalWidth, Mathf.Max(bp.width, bq.width));
                    if (lateral < width * .5f && overlap > width)
                        result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, first.hydrologyId + ":parallel_overlap:" + second.hydrologyId);
                }
            }
        }
    }

    internal static void ValidatePhysicalDrainage(Dictionary<string, YQHydrologyFeatureV2> features,
        YQSpatialBlueprintValidationResultV2 result)
    {
        // note: One physical tolerance is expressed in world metres, independent of normalized blueprint elevations.
        const double elevationToleranceMetres = .01d;
        double heightMetres = YQGeneratedWorldTerrain.TerrainHeight;
        foreach (var entry in features)
        {
            YQHydrologyFeatureV2 feature = entry.Value;
            if (!HasUsableWaterPoints(feature))
            {
                result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":invalid_physical_points");
                continue;
            }

            bool channel = feature.kind == YQHydrologyKindV2.River || feature.kind == YQHydrologyKindV2.Waterfall;
            if (channel)
            {
                for (int pointIndex = 1; pointIndex < feature.controlPoints.Count; pointIndex++)
                {
                    double riseMetres = ((double)feature.controlPoints[pointIndex].normalizedElevation -
                        feature.controlPoints[pointIndex - 1].normalizedElevation) * heightMetres;
                    if (riseMetres > elevationToleranceMetres)
                        result.Add(YQSpatialBlueprintFailureV2.UphillHydrology, feature.hydrologyId + ":rise_over_1cm:" + pointIndex);
                }
                if (features.TryGetValue(feature.sinkHydrologyId ?? string.Empty, out var sink) && HasUsableWaterPoints(sink))
                {
                    YQBlueprintPointV2 end = feature.controlPoints[feature.controlPoints.Count - 1];
                    double receiverLevel = SampleReceivingLevel(sink, end.x, end.z);
                    double mismatchMetres = Math.Abs(end.normalizedElevation - receiverLevel) * heightMetres;
                    if (mismatchMetres > elevationToleranceMetres)
                        result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, feature.hydrologyId + ":receiver_level_over_1cm");
                }
            }

            // note: Follow every declared drainage edge, including lake/wetland outlets. A terminal area body is valid under the existing model; a terminal river is not.
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            YQHydrologyFeatureV2 current = feature;
            while (current != null)
            {
                if (!visited.Add(current.hydrologyId))
                {
                    result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, feature.hydrologyId + ":drainage_cycle");
                    break;
                }
                if (string.IsNullOrWhiteSpace(current.sinkHydrologyId))
                {
                    if (current.kind != YQHydrologyKindV2.Lake && current.kind != YQHydrologyKindV2.Wetland &&
                        current.kind != YQHydrologyKindV2.Coastline)
                        result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, feature.hydrologyId + ":no_receiving_body");
                    break;
                }
                if (!features.TryGetValue(current.sinkHydrologyId, out current) || !HasUsableWaterPoints(current))
                {
                    result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, feature.hydrologyId + ":missing_receiver");
                    break;
                }
            }
        }
    }

    private static void ValidateConnectedBasins(Dictionary<string, YQHydrologyFeatureV2> features, float halfExtent,
        YQSpatialBlueprintValidationResultV2 result)
    {
        // note: Recompute the bounded wet component at acceptance, including containment and shoreline spill agreement; declared triangle lists cannot bypass terrain connectivity.
        int samples = 0;
        bool sharedProjection = false, historicalProjection = false;
        int newProjectionVersion = 0;
        foreach (var entry in features)
        {
            var water = entry.Value;
            bool area = water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland;
            if (!area)
            {
                if (water.hasConnectedDomain) result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":channel_connected_domain");
                continue;
            }
            int count = water.connectedDomain?.bed?.Count ?? 0;
            if (count > YQPreparedConnectedBasinV2.MaximumSamples || samples > 65536 - count)
            { result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":basin_domain_budget_or_bounds"); continue; }
            if (!water.hasConnectedDomain || !YQPreparedConnectedBasinV2.TryPrepare(water.connectedDomain, water.waterLevelNormalized, out var domain, out _))
            {
                result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":invalid_connected_domain");
                continue;
            }
            samples += water.connectedDomain.bed.Count;
            sharedProjection |= domain.Version >= YQPreparedConnectedBasinV2.SharedChannelProjectionVersion;
            historicalProjection |= domain.Version < YQPreparedConnectedBasinV2.SharedChannelProjectionVersion;
            // note: Plan-wide channel modes cannot reinterpret an archived basin when a later bank or wet-footprint version is added.
            if (domain.Version >= YQPreparedConnectedBasinV2.SharedChannelProjectionVersion)
            {
                if (newProjectionVersion != 0 && newProjectionVersion != domain.Version)
                    result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, "mixed_connected_projection_versions");
                newProjectionVersion = domain.Version;
            }
            if (samples > 65536 || domain.MinimumX < -halfExtent || domain.MaximumX > halfExtent ||
                domain.MinimumZ < -halfExtent || domain.MaximumZ > halfExtent)
                result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":basin_domain_budget_or_bounds");
            if (water.basin != null && water.basin.hasSpill &&
                Mathf.Abs(domain.SignedShoreDistance(water.basin.spillPoint.x, water.basin.spillPoint.z)) > .01f)
                result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":spill_outside_connected_shore");
            foreach (var incoming in features.Values)
            {
                if (incoming.sinkHydrologyId != water.hydrologyId || !HasUsableWaterPoints(incoming)) continue;
                var end = incoming.controlPoints[incoming.controlPoints.Count - 1];
                if (domain.SignedShoreDistance(end.x, end.z) > .01f)
                    result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, incoming.hydrologyId + ":outside_connected_receiver");
            }
        }
        // note: The projection version governs all channels in this accepted artifact; a mixed new/historical lake set must not silently change an older basin's streaming behavior.
        if (sharedProjection && historicalProjection)
            result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, "mixed_connected_projection_versions");
    }

    internal static void ValidateBasinConnectivity(Dictionary<string, YQHydrologyFeatureV2> features,
        YQSpatialBlueprintValidationResultV2 result)
    {
        // note: New accepted drainage carries a bounded decreasing order and one catchment identity through channels and open basins; legacy artifacts never acquire inferred metadata on load.
        foreach (var entry in features)
        {
            YQHydrologyFeatureV2 feature = entry.Value;
            if (!HasUsableWaterPoints(feature)) continue;
            if (string.IsNullOrWhiteSpace(feature.catchmentId) || feature.drainageOrder < 0 || feature.drainageOrder >= features.Count)
                result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":invalid_catchment_order");
            bool hasOutlet = !string.IsNullOrWhiteSpace(feature.sinkHydrologyId);
            if (hasOutlet && features.TryGetValue(feature.sinkHydrologyId, out var sink) && sink != null &&
                (feature.drainageOrder <= sink.drainageOrder || !string.Equals(feature.catchmentId, sink.catchmentId, StringComparison.Ordinal)))
                result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":nondecreasing_catchment_edge");

            bool area = feature.kind == YQHydrologyKindV2.Lake || feature.kind == YQHydrologyKindV2.Wetland || feature.kind == YQHydrologyKindV2.Coastline;
            YQHydrologyBasinV2 basin = feature.basin;
            if (basin == null || basin.spillPoint == null)
            {
                result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":unspecified_basin");
                continue;
            }
            // note: Even inactive spill storage must roundtrip as finite default values; hidden nonfinite geometry cannot enter an accepted hash.
            YQBlueprintPointV2 storedSpill = basin.spillPoint;
            if (!IsFinite(storedSpill.x) || !IsFinite(storedSpill.z) || !IsNormalized(storedSpill.normalizedElevation) ||
                !IsFinite(storedSpill.width) || storedSpill.width < 0f ||
                (!basin.hasSpill && (storedSpill.x != 0f || storedSpill.z != 0f || storedSpill.normalizedElevation != 0f || storedSpill.width != 0f)))
                result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":invalid_stored_spill");
            if (!area)
            {
                if (basin.disposition != YQBasinDispositionV2.Unspecified || basin.hasSpill ||
                    basin.closedBalance != YQClosedBasinBalanceV2.Unspecified)
                    result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":channel_basin_metadata");
                continue;
            }
            switch (basin.disposition)
            {
                case YQBasinDispositionV2.Open:
                    if (!hasOutlet || !features.TryGetValue(feature.sinkHydrologyId, out var outlet) || !HasUsableWaterPoints(outlet) ||
                        (outlet.kind != YQHydrologyKindV2.River && outlet.kind != YQHydrologyKindV2.Waterfall) ||
                        feature.kind == YQHydrologyKindV2.Coastline || basin.closedBalance != YQClosedBasinBalanceV2.Unspecified)
                    {
                        result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":invalid_open_basin_outlet");
                        break;
                    }
                    ValidateBasinSpill(feature, outlet, result);
                    break;
                case YQBasinDispositionV2.Closed:
                    if (hasOutlet || feature.drainageOrder != 0 || basin.hasSpill || feature.kind == YQHydrologyKindV2.Coastline ||
                        basin.closedBalance != YQClosedBasinBalanceV2.SeasonalStorageEvaporationAndSeepage)
                        result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":invalid_closed_basin_balance");
                    break;
                case YQBasinDispositionV2.OceanConnected:
                    if (hasOutlet || feature.drainageOrder != 0 || basin.hasSpill ||
                        feature.kind != YQHydrologyKindV2.Coastline || basin.closedBalance != YQClosedBasinBalanceV2.Unspecified)
                        result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":invalid_ocean_sink");
                    break;
                default:
                    result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, entry.Key + ":unspecified_basin");
                    break;
            }
        }
    }

    private static void ValidateBasinSpill(YQHydrologyFeatureV2 lake, YQHydrologyFeatureV2 outlet,
        YQSpatialBlueprintValidationResultV2 result)
    {
        // note: A spill is a level lip on the actual outgoing channel, not merely a named downstream receiver.
        YQBlueprintPointV2 spill = lake.basin.spillPoint;
        if (!lake.basin.hasSpill || spill == null || !IsFinite(spill.x) || !IsFinite(spill.z) || !IsFinite(spill.width) || spill.width < 0f || !IsNormalized(spill.normalizedElevation) ||
            Math.Abs((double)spill.normalizedElevation - lake.waterLevelNormalized) * YQGeneratedWorldTerrain.TerrainHeight > .01d)
        {
            result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, lake.hydrologyId + ":invalid_spill_level");
            return;
        }
        bool connected = false;
        foreach (YQBlueprintPointV2 point in outlet.controlPoints)
        {
            double dx = (double)point.x - spill.x, dz = (double)point.z - spill.z;
            if (dx * dx + dz * dz <= .0001d &&
                Math.Abs((double)point.normalizedElevation - spill.normalizedElevation) * YQGeneratedWorldTerrain.TerrainHeight <= .01d)
                connected = true;
        }
        if (!connected)
            result.Add(YQSpatialBlueprintFailureV2.InvalidHydrology, lake.hydrologyId + ":disconnected_spill");
    }

    private static bool HasUsableWaterPoints(YQHydrologyFeatureV2 feature)
    {
        // note: Structural rejection remains safe even when topology validation is invoked with malformed external records.
        if (feature == null || string.IsNullOrWhiteSpace(feature.hydrologyId) || feature.controlPoints == null ||
            feature.controlPoints.Count == 0 || !IsNormalized(feature.waterLevelNormalized)) return false;
        foreach (YQBlueprintPointV2 point in feature.controlPoints)
            if (point == null || !IsFinite(point.x) || !IsFinite(point.z) || !IsNormalized(point.normalizedElevation) ||
                !IsFinite(point.width) || point.width < 0f) return false;
        return true;
    }

    private static double SampleReceivingLevel(YQHydrologyFeatureV2 sink, float x, float z)
    {
        // note: Area bodies have a fixed accepted water plane; their footprint control points do not define a sloping lake surface.
        if (sink.kind == YQHydrologyKindV2.Lake || sink.kind == YQHydrologyKindV2.Wetland || sink.kind == YQHydrologyKindV2.Coastline)
            return sink.waterLevelNormalized;
        double nearestSquared = double.PositiveInfinity;
        double elevation = sink.controlPoints[0].normalizedElevation;
        for (int index = 0; index < sink.controlPoints.Count; index++)
        {
            YQBlueprintPointV2 a = sink.controlPoints[index];
            YQBlueprintPointV2 b = sink.controlPoints[Math.Min(index + 1, sink.controlPoints.Count - 1)];
            double dx = (double)b.x - a.x, dz = (double)b.z - a.z;
            double lengthSquared = dx * dx + dz * dz;
            double t = lengthSquared > 0d ? Math.Max(0d, Math.Min(1d, (((double)x - a.x) * dx + ((double)z - a.z) * dz) / lengthSquared)) : 0d;
            double offsetX = x - (a.x + dx * t), offsetZ = z - (a.z + dz * t);
            double distanceSquared = offsetX * offsetX + offsetZ * offsetZ;
            if (distanceSquared >= nearestSquared) continue;
            nearestSquared = distanceSquared;
            elevation = a.normalizedElevation + ((double)b.normalizedElevation - a.normalizedElevation) * t;
        }
        return elevation;
    }

    private static Dictionary<string, YQSiteAnchorV2> ValidateSites(
        YQSpatialBlueprintV2 blueprint,
        HashSet<string> regionIds,
        float halfExtent,
        YQSpatialBlueprintValidationResultV2 result)
    {
        Dictionary<string, YQSiteAnchorV2> sites =
            new Dictionary<string, YQSiteAnchorV2>(
                StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < blueprint.sites.Count; index++)
        {
            YQSiteAnchorV2 site = blueprint.sites[index];
            string id = site != null ? site.siteId : string.Empty;
            bool duplicate = !string.IsNullOrWhiteSpace(id) &&
                             sites.ContainsKey(id);

            if (!duplicate && !string.IsNullOrWhiteSpace(id) && site != null)
                sites.Add(id, site);

            if (site != null)
                site.EnsureCollections();

            // note: Persisted sector geometry must pass the same finite-coordinate boundary before semantic projection or indexing can consume it.
            if (site != null)
            {
                HashSet<string> memberIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int memberIndex = 0; memberIndex < site.memberFootprint.Count; memberIndex++)
                {
                    YQSiteMemberFootprintV2 member = site.memberFootprint[memberIndex];
                    if (member == null || string.IsNullOrWhiteSpace(member.memberId) ||
                        !memberIds.Add(member.memberId) || member.sectorIndex < 0 ||
                        !IsPointInBounds(member.x, member.z, halfExtent) ||
                        !IsPositiveFinite(member.reservedRadius) || member.reservedRadius > blueprint.worldSize)
                        result.Add(YQSpatialBlueprintFailureV2.InvalidSite,
                            id + "/member:" + (member?.memberId ?? "<null>"));
                }
            }

            if (site == null ||
                string.IsNullOrWhiteSpace(id) ||
                duplicate ||
                site.kind == YQSiteKindV2.Unknown ||
                (!string.IsNullOrWhiteSpace(site.parentRegionId) &&
                 !regionIds.Contains(site.parentRegionId)) ||
                !IsPointInBounds(site.x, site.z, halfExtent) ||
                !IsPositiveFinite(site.reservedRadius) ||
                !IsPositiveFinite(site.terrainSearchRadius) ||
                !IsFinite(site.maximumSlopeDegrees) ||
                site.maximumSlopeDegrees < 0f ||
                !IsNormalized(site.minimumRouteAccess) ||
                !IsNormalized(site.minimumWaterAccess))
            {
                result.Add(
                    duplicate
                        ? YQSpatialBlueprintFailureV2.DuplicateId
                        : YQSpatialBlueprintFailureV2.InvalidSite,
                    id);
            }
        }

        if (string.IsNullOrWhiteSpace(blueprint.originSiteId) ||
            !sites.TryGetValue(
                blueprint.originSiteId,
                out YQSiteAnchorV2 origin) ||
            origin.kind != YQSiteKindV2.Origin)
        {
            result.Add(
                YQSpatialBlueprintFailureV2.MissingOrigin,
                blueprint.originSiteId);
        }

        List<YQSiteAnchorV2> reservingSites =
            new List<YQSiteAnchorV2>();
        foreach (YQSiteAnchorV2 site in sites.Values)
        {
            if (site.kind != YQSiteKindV2.NaturalFeature)
                reservingSites.Add(site);
        }

        reservingSites.Sort(
            (left, right) => string.CompareOrdinal(left.siteId, right.siteId));

        for (int leftIndex = 0;
             leftIndex < reservingSites.Count;
             leftIndex++)
        {
            for (int rightIndex = leftIndex + 1;
                 rightIndex < reservingSites.Count;
                 rightIndex++)
            {
                YQSiteAnchorV2 left = reservingSites[leftIndex];
                YQSiteAnchorV2 right = reservingSites[rightIndex];
                float minimumDistance =
                    (left.reservedRadius + right.reservedRadius) * 0.62f;

                if (Distance(left.x, left.z, right.x, right.z) <
                    minimumDistance)
                {
                    result.Add(
                        YQSpatialBlueprintFailureV2.OverlappingSiteReserve,
                        left.siteId + "|" + right.siteId);
                }
            }
        }

        return sites;
    }

    private static HashSet<string> ValidateRoutes(
        YQSpatialBlueprintV2 blueprint,
        Dictionary<string, YQSiteAnchorV2> sites,
        Dictionary<string, YQHydrologyFeatureV2> hydrology,
        float halfExtent,
        YQSpatialBlueprintValidationResultV2 result)
    {
        HashSet<string> routeIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < blueprint.routes.Count; index++)
        {
            YQRouteCorridorV2 route = blueprint.routes[index];
            string id = route != null ? route.routeId : string.Empty;
            bool duplicate = !string.IsNullOrWhiteSpace(id) &&
                             !routeIds.Add(id);

            if (route != null)
                route.EnsureCollections();

            bool endpointsValid = route != null &&
                                  sites.ContainsKey(route.fromSiteId) &&
                                  sites.ContainsKey(route.toSiteId) &&
                                  !string.Equals(
                                      route.fromSiteId,
                                      route.toSiteId,
                                      StringComparison.OrdinalIgnoreCase);

            if (route == null ||
                string.IsNullOrWhiteSpace(id) ||
                duplicate ||
                route.routeClass == YQRouteClassV2.Unknown ||
                !endpointsValid ||
                !IsPositiveFinite(route.width) ||
                !IsFinite(route.shoulderWidth) ||
                route.shoulderWidth < 0f ||
                !IsPositiveFinite(route.maximumGradeDegrees) ||
                !HasValidPoints(route.controlPoints, halfExtent, 2) ||
                (endpointsValid && !RouteTouchesEndpoints(route, sites)))
            {
                result.Add(
                    duplicate
                        ? YQSpatialBlueprintFailureV2.DuplicateId
                        : YQSpatialBlueprintFailureV2.InvalidRoute,
                    id);
                continue;
            }

            for (int crossingIndex = 0;
                 crossingIndex < route.crossings.Count;
                 crossingIndex++)
            {
                YQRouteCrossingV2 crossing = route.crossings[crossingIndex];
                if (crossing == null ||
                    string.IsNullOrWhiteSpace(crossing.crossingId) ||
                    crossing.kind == YQRouteCrossingKindV2.Unknown ||
                    !hydrology.ContainsKey(crossing.hydrologyId) ||
                    !IsPointInBounds(crossing.x, crossing.z, halfExtent) ||
                    !IsPositiveFinite(crossing.requiredSpan))
                {
                    result.Add(
                        YQSpatialBlueprintFailureV2.InvalidRoute,
                        id);
                }
            }

            foreach (KeyValuePair<string, YQHydrologyFeatureV2> pair in hydrology)
            {
                if (pair.Value.kind != YQHydrologyKindV2.River ||
                    !PolylinesIntersect(
                        route.controlPoints,
                        pair.Value.controlPoints))
                {
                    continue;
                }

                bool hasCrossing = false;
                for (int crossingIndex = 0;
                     crossingIndex < route.crossings.Count;
                     crossingIndex++)
                {
                    YQRouteCrossingV2 crossing =
                        route.crossings[crossingIndex];
                    if (crossing != null &&
                        string.Equals(
                            crossing.hydrologyId,
                            pair.Key,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        hasCrossing = true;
                        break;
                    }
                }

                if (!hasCrossing)
                {
                    result.Add(
                        YQSpatialBlueprintFailureV2.MissingWaterCrossing,
                        id + "|" + pair.Key);
                }
            }
        }

        return routeIds;
    }

    private static void ValidateRelationships(
        YQSpatialBlueprintV2 blueprint,
        HashSet<string> regionIds,
        HashSet<string> terrainIds,
        Dictionary<string, YQHydrologyFeatureV2> hydrology,
        Dictionary<string, YQSiteAnchorV2> sites,
        HashSet<string> routeIds,
        YQSpatialBlueprintValidationResultV2 result)
    {
        HashSet<string> entityIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        entityIds.UnionWith(regionIds);
        entityIds.UnionWith(terrainIds);
        entityIds.UnionWith(hydrology.Keys);
        entityIds.UnionWith(sites.Keys);
        entityIds.UnionWith(routeIds);
        HashSet<string> relationshipIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0;
             index < blueprint.relationships.Count;
             index++)
        {
            YQSpatialRelationshipV2 relationship =
                blueprint.relationships[index];
            string id = relationship != null
                ? relationship.relationshipId
                : string.Empty;
            bool duplicate = !string.IsNullOrWhiteSpace(id) &&
                             !relationshipIds.Add(id);

            if (relationship == null ||
                string.IsNullOrWhiteSpace(id) ||
                duplicate ||
                relationship.kind == YQSpatialRelationshipKindV2.Unknown ||
                !entityIds.Contains(relationship.subjectId) ||
                !entityIds.Contains(relationship.objectId) ||
                !IsFinite(relationship.minimumDistance) ||
                !IsFinite(relationship.maximumDistance) ||
                relationship.minimumDistance < 0f ||
                relationship.maximumDistance < relationship.minimumDistance)
            {
                result.Add(
                    duplicate
                        ? YQSpatialBlueprintFailureV2.DuplicateId
                        : YQSpatialBlueprintFailureV2.InvalidRelationship,
                    id);
            }
        }
    }

    private static void ValidateSettlementConnectivity(
        YQSpatialBlueprintV2 blueprint,
        Dictionary<string, YQSiteAnchorV2> sites,
        YQSpatialBlueprintValidationResultV2 result)
    {
        if (!sites.ContainsKey(blueprint.originSiteId))
            return;

        Dictionary<string, List<string>> adjacency =
            new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (string siteId in sites.Keys)
            adjacency[siteId] = new List<string>();

        for (int index = 0; index < blueprint.routes.Count; index++)
        {
            YQRouteCorridorV2 route = blueprint.routes[index];
            if (route == null ||
                !adjacency.ContainsKey(route.fromSiteId) ||
                !adjacency.ContainsKey(route.toSiteId))
            {
                continue;
            }

            adjacency[route.fromSiteId].Add(route.toSiteId);
            adjacency[route.toSiteId].Add(route.fromSiteId);
        }

        HashSet<string> visited =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Queue<string> pending = new Queue<string>();
        pending.Enqueue(blueprint.originSiteId);
        visited.Add(blueprint.originSiteId);

        while (pending.Count > 0)
        {
            string current = pending.Dequeue();
            List<string> neighbours = adjacency[current];
            for (int index = 0; index < neighbours.Count; index++)
            {
                if (visited.Add(neighbours[index]))
                    pending.Enqueue(neighbours[index]);
            }
        }

        foreach (KeyValuePair<string, YQSiteAnchorV2> pair in sites)
        {
            if (pair.Value.kind == YQSiteKindV2.Settlement &&
                !visited.Contains(pair.Key))
            {
                result.Add(
                    YQSpatialBlueprintFailureV2.DisconnectedSettlement,
                    pair.Key);
            }
        }
    }

    private static bool RouteTouchesEndpoints(
        YQRouteCorridorV2 route,
        Dictionary<string, YQSiteAnchorV2> sites)
    {
        YQBlueprintPointV2 first = route.controlPoints[0];
        YQBlueprintPointV2 last =
            route.controlPoints[route.controlPoints.Count - 1];
        YQSiteAnchorV2 from = sites[route.fromSiteId];
        YQSiteAnchorV2 to = sites[route.toSiteId];
        return Distance(first.x, first.z, from.x, from.z) <= 0.5f &&
               Distance(last.x, last.z, to.x, to.z) <= 0.5f;
    }

    private static bool FlowsDownhill(
        IReadOnlyList<YQBlueprintPointV2> points)
    {
        for (int index = 1; index < points.Count; index++)
        {
            if (points[index].normalizedElevation >
                points[index - 1].normalizedElevation + 0.001f)
            {
                return false;
            }
        }

        return true;
    }

    internal static bool PolylinesIntersect(
        IReadOnlyList<YQBlueprintPointV2> left,
        IReadOnlyList<YQBlueprintPointV2> right)
    {
        if (left == null || right == null)
            return false;

        for (int leftIndex = 1; leftIndex < left.Count; leftIndex++)
        {
            for (int rightIndex = 1;
                 rightIndex < right.Count;
                 rightIndex++)
            {
                if (TrySegmentIntersection(
                        left[leftIndex - 1],
                        left[leftIndex],
                        right[rightIndex - 1],
                        right[rightIndex],
                        out _,
                        out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool TrySegmentIntersection(
        YQBlueprintPointV2 a,
        YQBlueprintPointV2 b,
        YQBlueprintPointV2 c,
        YQBlueprintPointV2 d,
        out float x,
        out float z)
    {
        x = 0f;
        z = 0f;
        float abX = b.x - a.x;
        float abZ = b.z - a.z;
        float cdX = d.x - c.x;
        float cdZ = d.z - c.z;
        float denominator = abX * cdZ - abZ * cdX;

        if (Mathf.Abs(denominator) < 0.0001f)
            return false;

        float acX = c.x - a.x;
        float acZ = c.z - a.z;
        float alongAb = (acX * cdZ - acZ * cdX) / denominator;
        float alongCd = (acX * abZ - acZ * abX) / denominator;

        if (alongAb < 0f || alongAb > 1f ||
            alongCd < 0f || alongCd > 1f)
        {
            return false;
        }

        x = a.x + abX * alongAb;
        z = a.z + abZ * alongAb;
        return true;
    }

    private static bool HasValidPoints(
        IReadOnlyList<YQBlueprintPointV2> points,
        float halfExtent,
        int minimumCount)
    {
        if (points == null || points.Count < minimumCount)
            return false;

        for (int index = 0; index < points.Count; index++)
        {
            YQBlueprintPointV2 point = points[index];
            if (point == null ||
                !IsPointInBounds(point.x, point.z, halfExtent) ||
                !IsNormalized(point.normalizedElevation) ||
                !IsFinite(point.width) ||
                point.width < 0f)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPointInBounds(
        float x,
        float z,
        float halfExtent)
    {
        return IsFinite(x) &&
               IsFinite(z) &&
               Mathf.Abs(x) <= halfExtent &&
               Mathf.Abs(z) <= halfExtent;
    }

    private static bool IsNormalized(float value)
    {
        return IsFinite(value) && value >= 0f && value <= 1f;
    }

    private static bool IsPositiveFinite(float value)
    {
        return IsFinite(value) && value > 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static float Distance(
        float leftX,
        float leftZ,
        float rightX,
        float rightZ)
    {
        float deltaX = leftX - rightX;
        float deltaZ = leftZ - rightZ;
        return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
    }
}

public static class YQSpatialBlueprintHasherV2
{
    public static string ComputeMemberFootprintHashReadOnly(GeneratedSpatialWorldPlanV2Record record)
    {
        // note: Versioned derived projections cover sectors separately, preserving existing accepted V2 content hashes and saves.
        bool hasMembers = false;
        if (record?.blueprint?.sites != null)
            for (int index = 0; index < record.blueprint.sites.Count && !hasMembers; index++)
                hasMembers = record.blueprint.sites[index]?.memberFootprint?.Count > 0;
        if (!hasMembers)
            return string.Empty;
        StableHashWriter writer = new StableHashWriter();
        List<YQSiteAnchorV2> sites = SortById(record?.blueprint?.sites, site => site?.siteId ?? string.Empty);
        for (int siteIndex = 0; siteIndex < sites.Count; siteIndex++)
        {
            YQSiteAnchorV2 site = sites[siteIndex];
            if (site.memberFootprint == null || site.memberFootprint.Count == 0)
                continue;
            Append(writer, site.siteId);
            List<YQSiteMemberFootprintV2> members = SortById(site.memberFootprint, member => member?.memberId ?? string.Empty);
            for (int index = 0; index < members.Count; index++)
            {
                YQSiteMemberFootprintV2 member = members[index];
                Append(writer, member.memberId);
                Append(writer, member.x);
                Append(writer, member.z);
                Append(writer, member.reservedRadius);
                Append(writer, member.sectorIndex);
            }
        }
        return writer.ToHashString();
    }

    public static string ComputeContentHash(
        GeneratedSpatialWorldPlanV2Record record)
    {
        if (record == null || record.blueprint == null)
            return string.Empty;

        record.EnsureCollections();
        return ComputeContentHashReadOnly(record);
    }

    public static string ComputeContentHashReadOnly(GeneratedSpatialWorldPlanV2Record record)
    {
        // note: Runtime integrity checks treat missing collections as empty without normalizing accepted data or cloning its full JSON graph.
        if (record == null || record.blueprint == null)
            return string.Empty;
        // note: Stream the exact canonical character sequence into FNV so per-cell authority checks avoid building and copying a large temporary string.
        StableHashWriter writer = new StableHashWriter(
            GeneratedSpatialWorldPlanV2Record.UsesBasinMetadata(record.generationVersion));
        Append(writer, record.schemaVersion);
        Append(writer, record.generationVersion);
        Append(writer, record.worldSeed);
        Append(writer, record.semanticFingerprint);
        Append(writer, record.blueprint.worldSize);
        Append(writer, record.blueprint.originSiteId);

        AppendRegions(writer, record.blueprint.regions);
        AppendTerrain(writer, record.blueprint.terrainFields);
        AppendHydrology(writer, record.blueprint.hydrology,
            GeneratedSpatialWorldPlanV2Record.UsesBasinMetadata(record.generationVersion),
            record.generationVersion == GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion);
        AppendSites(writer, record.blueprint.sites);
        AppendRoutes(writer, record.blueprint.routes);
        AppendRelationships(writer, record.blueprint.relationships);
        return writer.ToHashString();
    }

    private static void AppendRegions(
        StableHashWriter builder,
        IReadOnlyList<YQRegionDomainV2> values)
    {
        List<YQRegionDomainV2> sorted = SortById(
            values,
            value => value != null ? value.regionId : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQRegionDomainV2 value = sorted[index];
            Append(builder, value.regionId);
            Append(builder, value.centerX);
            Append(builder, value.centerZ);
            Append(builder, value.radius);
            Append(builder, value.elevationBias);
            Append(builder, value.moisture);
            Append(builder, value.ruggedness);
            Append(builder, value.civilizationDensity);
            Append(builder, value.danger);
            AppendStrings(builder, value.biomeTags);
        }
    }

    private static void AppendTerrain(
        StableHashWriter builder,
        IReadOnlyList<YQTerrainFieldV2> values)
    {
        List<YQTerrainFieldV2> sorted = SortById(
            values,
            value => value != null ? value.fieldId : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQTerrainFieldV2 value = sorted[index];
            Append(builder, value.fieldId);
            Append(builder, value.parentRegionId);
            Append(builder, (int)value.kind);
            Append(builder, value.strength);
            Append(builder, value.radius);
            Append(builder, value.falloff);
            AppendPoints(builder, value.controlPoints);
            AppendStrings(builder, value.tags);
        }
    }

    private static void AppendHydrology(
        StableHashWriter builder,
        IReadOnlyList<YQHydrologyFeatureV2> values,
        bool includeBasinContract,
        bool includeConnectedDomain)
    {
        List<YQHydrologyFeatureV2> sorted = SortById(
            values,
            value => value != null ? value.hydrologyId : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQHydrologyFeatureV2 value = sorted[index];
            Append(builder, value.hydrologyId);
            Append(builder, value.parentRegionId);
            Append(builder, (int)value.kind);
            Append(builder, value.sourceTerrainFieldId);
            Append(builder, value.sinkHydrologyId);
            Append(builder, value.waterLevelNormalized);
            Append(builder, value.nominalWidth);
            Append(builder, value.nominalDepth);
            AppendPoints(builder, value.controlPoints);
            AppendStrings(builder, value.tags);
            if (includeBasinContract)
            {
                // note: Include all new drainage decisions only in the new generation contract; old canonical hash character sequences remain byte-for-byte compatible.
                Append(builder, value.catchmentId);
                Append(builder, value.drainageOrder);
                Append(builder, value.basin != null ? 1 : 0);
                if (value.basin != null)
                {
                    Append(builder, (int)value.basin.disposition);
                    Append(builder, (int)value.basin.closedBalance);
                    Append(builder, value.basin.hasSpill ? 1 : 0);
                    Append(builder, value.basin.spillPoint != null ? 1 : 0);
                    if (value.basin.spillPoint != null)
                    {
                        Append(builder, value.basin.spillPoint.x);
                        Append(builder, value.basin.spillPoint.z);
                        Append(builder, value.basin.spillPoint.normalizedElevation);
                        Append(builder, value.basin.spillPoint.width);
                    }
                }
            }
            if (includeConnectedDomain)
            {
                // note: Hash every persisted domain decision only in topology 12; topology 11 keeps its original bit-formatted checksum.
                Append(builder, value.hasConnectedDomain ? 1 : 0);
                if (value.hasConnectedDomain && value.connectedDomain != null)
                {
                    var domain = value.connectedDomain;
                    Append(builder, domain.version); Append(builder, domain.resolution); Append(builder, domain.spacing);
                    Append(builder, domain.minimumX); Append(builder, domain.minimumZ); Append(builder, domain.seedX); Append(builder, domain.seedZ);
                    Append(builder, domain.bed != null ? domain.bed.Count : -1);
                    if (domain.bed != null) foreach (float height in domain.bed) Append(builder, height);
                    Append(builder, domain.wetTriangles != null ? domain.wetTriangles.Count : -1);
                    if (domain.wetTriangles != null) foreach (int triangle in domain.wetTriangles) Append(builder, triangle);
                }
            }
        }
    }

    private static void AppendSites(
        StableHashWriter builder,
        IReadOnlyList<YQSiteAnchorV2> values)
    {
        List<YQSiteAnchorV2> sorted = SortById(
            values,
            value => value != null ? value.siteId : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQSiteAnchorV2 value = sorted[index];
            Append(builder, value.siteId);
            Append(builder, value.sourceSemanticId);
            Append(builder, value.parentRegionId);
            Append(builder, (int)value.kind);
            Append(builder, (int)value.placementMode);
            Append(builder, value.x);
            Append(builder, value.z);
            Append(builder, value.preferredHeadingDegrees);
            Append(builder, value.reservedRadius);
            Append(builder, value.terrainSearchRadius);
            Append(builder, value.maximumSlopeDegrees);
            Append(builder, value.minimumRouteAccess);
            Append(builder, value.minimumWaterAccess);
            Append(builder, value.requiresTerrainConformance ? 1 : 0);
            Append(builder, value.hiddenFromPrimaryRoute ? 1 : 0);

            AppendFunctions(builder, value.requiredFunctions);

            AppendStrings(builder, value.tags);
        }
    }

    private static void AppendRoutes(
        StableHashWriter builder,
        IReadOnlyList<YQRouteCorridorV2> values)
    {
        List<YQRouteCorridorV2> sorted = SortById(
            values,
            value => value != null ? value.routeId : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQRouteCorridorV2 value = sorted[index];
            Append(builder, value.routeId);
            Append(builder, value.sourceSemanticRouteId);
            Append(builder, value.fromSiteId);
            Append(builder, value.toSiteId);
            Append(builder, value.parentRegionId);
            Append(builder, (int)value.routeClass);
            Append(builder, value.width);
            Append(builder, value.shoulderWidth);
            Append(builder, value.maximumGradeDegrees);
            AppendPoints(builder, value.controlPoints);

            List<YQRouteCrossingV2> crossings = SortById(
                value.crossings,
                crossing => crossing != null
                    ? crossing.crossingId
                    : string.Empty);
            for (int crossingIndex = 0;
                 crossingIndex < crossings.Count;
                 crossingIndex++)
            {
                YQRouteCrossingV2 crossing = crossings[crossingIndex];
                Append(builder, crossing.crossingId);
                Append(builder, crossing.hydrologyId);
                Append(builder, (int)crossing.kind);
                Append(builder, crossing.x);
                Append(builder, crossing.z);
                Append(builder, crossing.requiredSpan);
            }

            AppendStrings(builder, value.tags);
        }
    }

    private static void AppendRelationships(
        StableHashWriter builder,
        IReadOnlyList<YQSpatialRelationshipV2> values)
    {
        List<YQSpatialRelationshipV2> sorted = SortById(
            values,
            value => value != null
                ? value.relationshipId
                : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQSpatialRelationshipV2 value = sorted[index];
            Append(builder, value.relationshipId);
            Append(builder, value.subjectId);
            Append(builder, value.objectId);
            Append(builder, (int)value.kind);
            Append(builder, value.minimumDistance);
            Append(builder, value.maximumDistance);
            Append(builder, value.required ? 1 : 0);
            AppendStrings(builder, value.tags);
        }
    }

    private static void AppendPoints(
        StableHashWriter builder,
        IReadOnlyList<YQBlueprintPointV2> points)
    {
        // note: This is the same canonical empty sequence produced by EnsureCollections, without writing into the accepted record.
        for (int index = 0; index < (points != null ? points.Count : 0); index++)
        {
            YQBlueprintPointV2 point = points[index];
            Append(builder, point.x);
            Append(builder, point.z);
            Append(builder, point.normalizedElevation);
            Append(builder, point.width);
        }
    }

    private static void AppendStrings(
        StableHashWriter builder,
        IReadOnlyList<string> values)
    {
        // note: Empty/singleton tags need no sorting storage. Larger sequences rent only scratch space, never cache accepted content or mutate its order.
        int count = values != null ? values.Count : 0;
        if (count == 0) return;
        if (count == 1) { Append(builder, values[0]); return; }
        string[] sorted = System.Buffers.ArrayPool<string>.Shared.Rent(count);
        try
        {
            for (int index = 0; index < count; index++) sorted[index] = values[index] ?? string.Empty;
            Array.Sort(sorted, 0, count, StringComparer.Ordinal);
            for (int index = 0; index < count; index++) Append(builder, sorted[index]);
        }
        finally { System.Buffers.ArrayPool<string>.Shared.Return(sorted, clearArray: true); }
    }

    private static void AppendFunctions(StableHashWriter builder, IReadOnlyList<YQAssetFunctionV2> values)
    {
        // note: Preserve signed numeric ordering and duplicates while avoiding one List and backing array per site on every integrity check.
        int count = values != null ? values.Count : 0;
        if (count == 0) return;
        if (count == 1) { Append(builder, (int)values[0]); return; }
        int[] sorted = System.Buffers.ArrayPool<int>.Shared.Rent(count);
        try
        {
            for (int index = 0; index < count; index++) sorted[index] = (int)values[index];
            Array.Sort(sorted, 0, count);
            for (int index = 0; index < count; index++) Append(builder, sorted[index]);
        }
        finally { System.Buffers.ArrayPool<int>.Shared.Return(sorted); }
    }

    private static List<T> SortById<T>(
        IReadOnlyList<T> values,
        Func<T, string> idSelector)
        where T : class
    {
        List<T> sorted = new List<T>();
        if (values != null)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] != null)
                    sorted.Add(values[index]);
            }
        }

        sorted.Sort(
            (left, right) => string.CompareOrdinal(
                idSelector(left),
                idSelector(right)));
        return sorted;
    }

    private static void Append(StableHashWriter builder, string value)
    {
        string safe = value ?? string.Empty;
        Append(builder, safe.AsSpan());
    }

    private static void Append(StableHashWriter builder, ReadOnlySpan<char> value)
    {
        // note: Preserve the length-delimited canonical token while consuming formatted stack storage directly.
        builder.AppendToken(value);
    }

    private static void Append(StableHashWriter builder, float value)
    {
        // note: New artifacts hash exact float bits, so Mono/CoreCLR formatting choices cannot change accepted identity; older artifacts keep their historical character sequence.
        if (builder.CanonicalFloatBits)
        {
            Span<char> bits = stackalloc char[8];
            int canonical = value == 0f ? 0 : BitConverter.SingleToInt32Bits(value);
            canonical.TryFormat(bits, out int length, "X8", CultureInfo.InvariantCulture);
            Append(builder, bits.Slice(0, length));
            return;
        }
        // note: The same round-trip invariant formatter avoids a temporary string on each runtime integrity check.
        Span<char> text = stackalloc char[64];
        if (value.TryFormat(text, out int written, "R", CultureInfo.InvariantCulture))
            Append(builder, text.Slice(0, written));
        else
            Append(builder, value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static void Append(StableHashWriter builder, int value)
    {
        // note: Eleven characters cover every invariant Int32 token, including its sign.
        Span<char> text = stackalloc char[11];
        if (value.TryFormat(text, out int written, default, CultureInfo.InvariantCulture))
            Append(builder, text.Slice(0, written));
        else
            Append(builder, value.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class StableHashWriter
    {
        private ulong _hash = 14695981039346656037UL;
        public bool CanonicalFloatBits { get; }

        public StableHashWriter(bool canonicalFloatBits = false)
        {
            CanonicalFloatBits = canonicalFloatBits;
        }

        public void AppendToken(ReadOnlySpan<char> value)
        {
            // note: Nonnegative default Int32 formatting emits these decimal digits without separators in every supported culture; ten slots cover any string length.
            Span<char> lengthDigits = stackalloc char[10];
            int remaining = value.Length;
            int firstDigit = lengthDigits.Length;
            do
            {
                lengthDigits[--firstDigit] = (char)('0' + remaining % 10);
                remaining /= 10;
            } while (remaining != 0);
            // note: Consume the identical length:value| UTF-16 stream with local FNV state, avoiding formatter dispatch and a field write for every character.
            unchecked
            {
                ulong hash = _hash;
                for (int index = firstDigit; index < lengthDigits.Length; index++)
                    hash = (hash ^ lengthDigits[index]) * 1099511628211UL;
                hash = (hash ^ ':') * 1099511628211UL;
                for (int index = 0; index < value.Length; index++)
                    hash = (hash ^ value[index]) * 1099511628211UL;
                _hash = (hash ^ '|') * 1099511628211UL;
            }
        }

        public string ToHashString()
        {
            return _hash.ToString("x16", CultureInfo.InvariantCulture);
        }

    }
}
