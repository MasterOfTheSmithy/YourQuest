using System;
using UnityEngine;

/// <summary>
/// Stable quest/NPC-facing queries over accepted world structure.
/// Callers select existing valid locations instead of inventing coordinates or spawning
/// narrative sites beside the player.
/// </summary>
public static class YQGeneratedWorldQuery
{
    // note: Expose the versioned semantic cell contract through the existing gameplay query boundary.
    public static GeneratedSemanticCellPlanRecord GetSemanticCellPlan(
        GeneratedWorldPlanRecord plan,
        Vector2Int coordinate)
    {
        return plan == null ? null : YQSemanticWorldAuthority.QueryCell(plan, coordinate);
    }

    // note: Return a bounded semantic neighborhood so callers cannot expand an unloaded query into an unbounded scan.
    public static System.Collections.Generic.List<GeneratedSemanticCellPlanRecord> GetSemanticNeighborhood(
        GeneratedWorldPlanRecord plan,
        Vector2Int center,
        int radius = YQSemanticWorldAuthority.QueryNeighborhoodRadiusCells)
    {
        return plan == null
            ? new System.Collections.Generic.List<GeneratedSemanticCellPlanRecord>()
            : YQSemanticWorldAuthority.QueryNeighborhood(plan, center, radius);
    }

    // note: Resolve reserved sites from the same authority used by cell plans, including sites outside the opening envelope.
    public static System.Collections.Generic.List<GeneratedSemanticSiteReservationRecord> GetSemanticSites(
        GeneratedWorldPlanRecord plan,
        Vector2Int coordinate)
    {
        return plan == null
            ? new System.Collections.Generic.List<GeneratedSemanticSiteReservationRecord>()
            : YQSemanticWorldAuthority.GetSitesForCell(plan, coordinate);
    }

    // note: Resolve basin-owned water networks without allowing a consumer to roll an independent edge-water result.
    public static System.Collections.Generic.List<GeneratedSemanticWaterNetworkRecord> GetSemanticWater(
        GeneratedWorldPlanRecord plan,
        Vector2Int coordinate)
    {
        return plan == null
            ? new System.Collections.Generic.List<GeneratedSemanticWaterNetworkRecord>()
            : YQSemanticWorldAuthority.GetWaterForCell(plan, coordinate);
    }

    // note: Resolve a stable site identity through the public query boundary for unloaded destination lookup.
    public static bool TryGetSemanticSite(
        GeneratedWorldPlanRecord plan,
        string siteId,
        out GeneratedSemanticSiteReservationRecord site)
    {
        if (plan == null)
        {
            site = null;
            return false;
        }

        return YQSemanticWorldAuthority.TryGetSite(plan, siteId, out site);
    }

    public static GeneratedSpatialLocationRecord GetNearbySettlement(
        GeneratedWorldPlanRecord plan,
        Vector3 worldPosition,
        float maximumDistance = float.PositiveInfinity)
    {
        return FindBest(
            plan,
            location => string.Equals(location.locationKind, "settlement", StringComparison.OrdinalIgnoreCase),
            location => Distance(location, worldPosition),
            maximumDistance);
    }

    public static GeneratedSpatialLocationRecord GetLandmarkByTag(
        GeneratedWorldPlanRecord plan,
        string requiredTag)
    {
        return FindBest(
            plan,
            location => location.locationKind == "point_of_interest" && HasTag(location, requiredTag),
            location => -location.finalScore);
    }

    public static GeneratedSpatialLocationRecord GetDangerousLocation(
        GeneratedWorldPlanRecord plan,
        Vector3 nearPosition)
    {
        return FindBest(
            plan,
            location => location.locationKind == "hostile_site" && location.questEligible,
            location => Distance(location, nearPosition) - location.defensibility * 80f);
    }

    public static GeneratedSpatialLocationRecord GetFactionControlledLocation(
        GeneratedWorldPlanRecord plan,
        string factionId)
    {
        if (plan == null || string.IsNullOrWhiteSpace(factionId))
            return null;

        for (int i = 0; i < plan.settlements.Count; i++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[i];
            if (settlement == null || settlement.factionIds == null)
                continue;
            for (int j = 0; j < settlement.factionIds.Count; j++)
            {
                if (string.Equals(settlement.factionIds[j], factionId, StringComparison.OrdinalIgnoreCase) &&
                    YQGeneratedWorldSpatialPlanner.TryGetLocation(plan, settlement.settlementId, out GeneratedSpatialLocationRecord location))
                {
                    return ProjectToRuntimeAuthority(plan, location);
                }
            }
        }

        return null;
    }

    public static GeneratedSpatialLocationRecord GetReachableDungeon(
        GeneratedWorldPlanRecord plan,
        Vector3 nearPosition)
    {
        return FindBest(
            plan,
            location => location.questEligible &&
                        ContainsAny(location.structuralArchetype, "dungeon", "cave", "crypt", "mine"),
            location => Distance(location, nearPosition));
    }

    public static GeneratedSpatialLocationRecord GetRoadsideLocation(
        GeneratedWorldPlanRecord plan,
        Vector3 nearPosition,
        float maximumRoadDistance = 42f)
    {
        GeneratedSpatialWorldPlanRecord spatial =
            plan != null ? YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan) : null;
        if (spatial == null)
            return null;

        YQPreparedSpatialMaterializationV2 preparedV2 = null;
        bool useV2 = YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan);
        // note: An active V2 world never answers gameplay queries from obsolete V1 coordinates if projection fails.
        if (useV2 && !YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan, out preparedV2, out _))
            return null;

        GeneratedSpatialLocationRecord best = null;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord location =
                ProjectToRuntimeAuthority(
                    spatial.locations[i],
                    useV2 ? preparedV2 : null);
            if (location == null || !location.questEligible)
                continue;
            float roadDistance = useV2
                ? DistanceToRoad(
                    preparedV2,
                    new Vector2(location.worldX, location.worldZ))
                : DistanceToRoad(
                    spatial,
                    new Vector2(location.worldX, location.worldZ));
            if (roadDistance > maximumRoadDistance)
                continue;
            float score = Distance(location, nearPosition) + roadDistance * 2f;
            if (score < bestScore)
            {
                bestScore = score;
                best = location;
            }
        }
        return best;
    }

    public static GeneratedSpatialRegionRecord GetRegionByBiome(
        GeneratedWorldPlanRecord plan,
        string biome)
    {
        GeneratedSpatialWorldPlanRecord spatial =
            plan != null ? YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan) : null;
        if (spatial == null || string.IsNullOrWhiteSpace(biome))
            return null;

        for (int i = 0; i < spatial.regions.Count; i++)
        {
            GeneratedSpatialRegionRecord region = spatial.regions[i];
            if (region != null &&
                (string.Equals(region.biome, biome, StringComparison.OrdinalIgnoreCase) || HasTag(region, biome)))
            {
                return region;
            }
        }
        return null;
    }

    public static GeneratedSpatialLocationRecord GetLocationSuitableForAmbush(
        GeneratedWorldPlanRecord plan,
        Vector3 nearPosition)
    {
        GeneratedSpatialWorldPlanRecord spatial =
            plan != null ? YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan) : null;
        if (spatial == null)
            return null;

        YQPreparedSpatialMaterializationV2 preparedV2 = null;
        bool useV2 = YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan);
        // note: Ambush selection must fail visibly at its caller instead of targeting a different spatial graph.
        if (useV2 && !YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan, out preparedV2, out _))
            return null;

        // note: An ambush site must be near a planned route, outside a settlement, defensible, and already reachable.
        return FindBest(
            plan,
            location => location.questEligible &&
                        location.locationKind != "settlement" &&
                        (useV2
                            ? DistanceToRoad(
                                preparedV2,
                                new Vector2(location.worldX, location.worldZ))
                            : DistanceToRoad(
                                spatial,
                                new Vector2(location.worldX, location.worldZ))) <= 34f,
            location => Distance(location, nearPosition) - location.defensibility * 90f);
    }

    private static GeneratedSpatialLocationRecord FindBest(
        GeneratedWorldPlanRecord plan,
        Predicate<GeneratedSpatialLocationRecord> accepted,
        Func<GeneratedSpatialLocationRecord, float> score,
        float maximumScore = float.PositiveInfinity)
    {
        GeneratedSpatialWorldPlanRecord spatial =
            plan != null ? YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan) : null;
        if (spatial == null)
            return null;

        YQPreparedSpatialMaterializationV2 preparedV2 = null;
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
        {
            if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out preparedV2,
                out _))
                return null;
        }

        GeneratedSpatialLocationRecord best = null;
        float bestScore = maximumScore;
        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord location =
                ProjectToRuntimeAuthority(
                    spatial.locations[i],
                    preparedV2);
            if (location == null || !accepted(location))
                continue;
            float candidate = score(location);
            if (candidate < bestScore)
            {
                bestScore = candidate;
                best = location;
            }
        }
        return best;
    }

    private static GeneratedSpatialLocationRecord ProjectToRuntimeAuthority(
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialLocationRecord source)
    {
        if (source == null ||
            !YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
            return source;

        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out YQPreparedSpatialMaterializationV2 prepared,
                out _))
        {
            return null;
        }

        return ProjectToRuntimeAuthority(source, prepared);
    }

    private static GeneratedSpatialLocationRecord ProjectToRuntimeAuthority(
        GeneratedSpatialLocationRecord source,
        YQPreparedSpatialMaterializationV2 prepared)
    {
        if (source == null || prepared == null)
            return source;

        if (!prepared.TryGetSiteBySemanticId(
                source.locationId,
                out YQSpatialMaterializationSiteV2 site))
        {
            // note: Semantic metadata without a corresponding V2 site cannot provide a usable destination in this world.
            return null;
        }

        // note: Quest and NPC selectors retain semantic V1 metadata but receive a detached view of the accepted V2 coordinates; persisted V1 records are never mutated during a runtime query.
        return new GeneratedSpatialLocationRecord
        {
            locationId = source.locationId,
            parentRegionId = source.parentRegionId,
            parentLocationId = source.parentLocationId,
            locationKind = source.locationKind,
            structuralArchetype = source.structuralArchetype,
            worldX = site.x,
            worldZ = site.z,
            footprintRadius = site.reservedRadius,
            entranceHeadingDegrees = site.headingDegrees,
            terrainSuitability = source.terrainSuitability,
            waterAccess = site.waterAccess,
            routeAccess = site.routeAccess,
            defensibility = source.defensibility,
            finalScore = source.finalScore,
            questEligible = source.questEligible,
            requiresTerrainAdaptation = source.requiresTerrainAdaptation,
            tags = source.tags != null
                ? new System.Collections.Generic.List<string>(source.tags)
                : new System.Collections.Generic.List<string>()
        };
    }

    private static float Distance(GeneratedSpatialLocationRecord location, Vector3 point)
    {
        return Vector2.Distance(new Vector2(location.worldX, location.worldZ), new Vector2(point.x, point.z));
    }

    private static float DistanceToRoad(GeneratedSpatialWorldPlanRecord spatial, Vector2 point)
    {
        float best = float.PositiveInfinity;
        for (int routeIndex = 0; routeIndex < spatial.routes.Count; routeIndex++)
        {
            GeneratedSpatialRouteRecord route = spatial.routes[routeIndex];
            if (route == null || route.waypoints == null)
                continue;
            for (int i = 1; i < route.waypoints.Count; i++)
            {
                Vector2 a = new Vector2(route.waypoints[i - 1].x, route.waypoints[i - 1].z);
                Vector2 b = new Vector2(route.waypoints[i].x, route.waypoints[i].z);
                best = Mathf.Min(best, DistanceToSegment(point, a, b));
            }
        }
        return best;
    }

    private static float DistanceToRoad(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2 point)
    {
        float best = float.PositiveInfinity;
        if (prepared == null)
            return best;

        for (int routeIndex = 0;
             routeIndex < prepared.RouteCount;
             routeIndex++)
        {
            int pointCount = prepared.GetRoutePointCount(routeIndex);
            for (int pointIndex = 1;
                 pointIndex < pointCount;
                 pointIndex++)
            {
                YQSpatialMaterializationRoutePointV2 from =
                    prepared.GetRoutePoint(routeIndex, pointIndex - 1);
                YQSpatialMaterializationRoutePointV2 to =
                    prepared.GetRoutePoint(routeIndex, pointIndex);
                best = Mathf.Min(
                    best,
                    DistanceToSegment(
                        point,
                        new Vector2(from.x, from.z),
                        new Vector2(to.x, to.z)));
            }
        }

        return best;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 segment = b - a;
        if (segment.sqrMagnitude < 0.001f)
            return Vector2.Distance(point, a);
        float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude);
        return Vector2.Distance(point, a + segment * t);
    }

    private static bool HasTag(GeneratedSpatialLocationRecord location, string tag)
    {
        if (location == null || location.tags == null || string.IsNullOrWhiteSpace(tag))
            return false;
        for (int i = 0; i < location.tags.Count; i++)
        {
            if (string.Equals(location.tags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool HasTag(GeneratedSpatialRegionRecord region, string tag)
    {
        if (region == null || region.tags == null || string.IsNullOrWhiteSpace(tag))
            return false;
        for (int i = 0; i < region.tags.Count; i++)
        {
            if (string.Equals(region.tags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        for (int i = 0; i < terms.Length; i++)
        {
            if (value.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }
}
