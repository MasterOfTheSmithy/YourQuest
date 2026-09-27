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
        StableHashWriter writer = new StableHashWriter();
        Append(writer, record.schemaVersion);
        Append(writer, record.generationVersion);
        Append(writer, record.worldSeed);
        Append(writer, record.semanticFingerprint);
        Append(writer, record.blueprint.worldSize);
        Append(writer, record.blueprint.originSiteId);

        AppendRegions(writer, record.blueprint.regions);
        AppendTerrain(writer, record.blueprint.terrainFields);
        AppendHydrology(writer, record.blueprint.hydrology);
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
        IReadOnlyList<YQHydrologyFeatureV2> values)
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

            List<int> functions = new List<int>();
            for (int functionIndex = 0;
                 functionIndex < (value.requiredFunctions != null ? value.requiredFunctions.Count : 0);
                 functionIndex++)
            {
                functions.Add((int)value.requiredFunctions[functionIndex]);
            }

            functions.Sort();
            for (int functionIndex = 0;
                 functionIndex < functions.Count;
                 functionIndex++)
            {
                Append(builder, functions[functionIndex]);
            }

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
        List<string> sorted = new List<string>();
        if (values != null)
        {
            for (int index = 0; index < values.Count; index++)
                sorted.Add(values[index] ?? string.Empty);
        }

        sorted.Sort(StringComparer.Ordinal);
        for (int index = 0; index < sorted.Count; index++)
            Append(builder, sorted[index]);
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
        builder.Append(safe.Length);
        builder.Append(':');
        builder.Append(safe);
        builder.Append('|');
    }

    private static void Append(StableHashWriter builder, float value)
    {
        Append(builder, value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static void Append(StableHashWriter builder, int value)
    {
        Append(builder, value.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class StableHashWriter
    {
        private ulong _hash = 14695981039346656037UL;

        public StableHashWriter Append(int value)
        {
            // note: StringBuilder.Append(int) used the current culture for this nonnegative length prefix; retain its exact character sequence.
            Append(value.ToString(CultureInfo.CurrentCulture));
            return this;
        }

        public StableHashWriter Append(char value)
        {
            AppendCharacter(value);
            return this;
        }

        public StableHashWriter Append(string value)
        {
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
                AppendCharacter(text[index]);
            return this;
        }

        public string ToHashString()
        {
            return _hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private void AppendCharacter(char value)
        {
            // note: Preserve the existing stable FNV-1a character stream and output format exactly.
            unchecked
            {
                _hash ^= value;
                _hash *= 1099511628211UL;
            }
        }
    }
}
