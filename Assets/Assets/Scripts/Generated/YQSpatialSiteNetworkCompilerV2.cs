using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal static class YQSpatialSiteNetworkCompilerV2
{
    internal static bool TryAvoidUnrelatedSiteReserves(YQSpatialBlueprintV2 blueprint, YQRouteCorridorV2 route, out string failure)
    {
        failure = string.Empty;
        // note: Road endpoints may enter their own sites; all other construction reserves are obstacles to the road footprint.
        var sites = new List<YQSiteAnchorV2>();
        foreach (var site in blueprint.sites)
            if (site != null && site.kind != YQSiteKindV2.NaturalFeature &&
                site.siteId != route.fromSiteId && site.siteId != route.toSiteId)
                sites.Add(site);
        sites.Sort((a, b) => string.CompareOrdinal(a.siteId, b.siteId));
        float margin = route.width * .5f + route.shoulderWidth + 2f;
        // note: Intermediate steering points are not destinations; discard ones inside unrelated reserves before routing around those reserves.
        for (int index = route.controlPoints.Count - 2; index > 0; index--)
        {
            var point = route.controlPoints[index];
            foreach (var site in sites)
                if (Vector2.Distance(new Vector2(point.x, point.z), new Vector2(site.x, site.z)) < site.reservedRadius + margin)
                { route.controlPoints.RemoveAt(index); break; }
        }
        for (int pass = 0; pass < 64; pass++)
        {
            bool changed = false;
            for (int segment = 1; segment < route.controlPoints.Count && !changed; segment++)
            {
                var first = route.controlPoints[segment - 1];
                var last = route.controlPoints[segment];
                Vector2 a = new Vector2(first.x, first.z);
                Vector2 b = new Vector2(last.x, last.z);
                foreach (var site in sites)
                {
                    Vector2 center = new Vector2(site.x, site.z);
                    float radius = site.reservedRadius + margin;
                    if (SegmentReserveDistance(a, b, center) >= radius) continue;
                    if (Vector2.Distance(a, center) < radius || Vector2.Distance(b, center) < radius)
                    {
                        failure = route.routeId + " has a control point inside unrelated reserve " + site.siteId;
                        return false;
                    }
                    Vector2 tangent = (b - a).normalized;
                    Vector2 normal = new Vector2(-tangent.y, tangent.x);
                    Vector2 bestFirst = default, bestLast = default;
                    float bestLength = float.PositiveInfinity;
                    // note: Try both deterministic sides and increasing clearance, validating every new segment against every reserve before choosing the shortest feasible detour.
                    for (int expansion = 0; expansion < 4; expansion++)
                    {
                        float reach = radius * (1.5f + expansion * .5f);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector2 p = center - tangent * reach + normal * (side * reach);
                            Vector2 q = center + tangent * reach + normal * (side * reach);
                            float boundary = blueprint.worldSize * .5f - 3f;
                            if (Mathf.Abs(p.x) > boundary || Mathf.Abs(p.y) > boundary ||
                                Mathf.Abs(q.x) > boundary || Mathf.Abs(q.y) > boundary) continue;
                            bool clear = true;
                            foreach (var other in sites)
                            {
                                Vector2 obstacle = new Vector2(other.x, other.z);
                                float needed = other.reservedRadius + margin;
                                if (SegmentReserveDistance(a, p, obstacle) < needed ||
                                    SegmentReserveDistance(p, q, obstacle) < needed ||
                                    SegmentReserveDistance(q, b, obstacle) < needed) { clear = false; break; }
                            }
                            float length = Vector2.Distance(a, p) + Vector2.Distance(p, q) + Vector2.Distance(q, b);
                            if (!clear || length >= bestLength) continue;
                            bestLength = length; bestFirst = p; bestLast = q;
                        }
                    }
                    if (float.IsPositiveInfinity(bestLength))
                    {
                        // note: Fall back to a bounded visibility graph when another reserve blocks every two-bend candidate; this preserves exclusive reserves while allowing a valid multi-bend route.
                        if (!TryBuildVisibilityDetour(
                                a,
                                b,
                                sites,
                                margin,
                                blueprint.worldSize * .5f - 3f,
                                out List<Vector2> detour) &&
                            !TryBuildVisibilityDetour(
                                a, b, sites, margin,
                                blueprint.worldSize * .5f - 3f,
                                out detour, true))
                        {
                            failure = route.routeId + " cannot clear unrelated site reserve " + site.siteId;
                            return false;
                        }

                        for (int pointIndex = detour.Count - 2; pointIndex > 0; pointIndex--)
                        {
                            Vector2 point = detour[pointIndex];
                            float pointT = Vector2.Distance(a, point) /
                                           Mathf.Max(.001f, Vector2.Distance(a, b));
                            route.controlPoints.Insert(
                                segment,
                                YQSpatialBlueprintDeterminismV2.Point(
                                    point,
                                    Mathf.Lerp(
                                        first.normalizedElevation,
                                        last.normalizedElevation,
                                        Mathf.Clamp01(pointT)),
                                    route.width));
                        }

                        changed = true;
                        break;
                    }
                    // note: Preserve endpoints and interpolate the planned vertical profile; the existing terrain/grade gates still decide feasibility.
                    float firstT = Vector2.Distance(a, bestFirst) / bestLength;
                    float lastT = 1f - Vector2.Distance(bestLast, b) / bestLength;
                    route.controlPoints.Insert(segment, YQSpatialBlueprintDeterminismV2.Point(bestFirst,
                        Mathf.Lerp(first.normalizedElevation, last.normalizedElevation, firstT), route.width));
                    route.controlPoints.Insert(segment + 1, YQSpatialBlueprintDeterminismV2.Point(bestLast,
                        Mathf.Lerp(first.normalizedElevation, last.normalizedElevation, lastT), route.width));
                    changed = true;
                    break;
                }
            }
            if (!changed) return true;
        }
        failure = route.routeId + " exceeded the bounded site-clearance routing budget.";
        return false;
    }

    private static bool TryBuildVisibilityDetour(
        Vector2 start,
        Vector2 end,
        List<YQSiteAnchorV2> sites,
        float margin,
        float boundary,
        out List<Vector2> path,
        bool useCircumscribedRing = false)
    {
        path = null;
        var nodes = new List<Vector2> { start, end };
        const int Directions = 16;
        const float ClearancePadding = .5f;
        const float CorridorPadding = 96f;
        const int MaximumRelevantSites = 30;

        var relevantSites = new List<YQSiteAnchorV2>();
        for (int siteIndex = 0; siteIndex < sites.Count; siteIndex++)
        {
            YQSiteAnchorV2 site = sites[siteIndex];
            Vector2 center = new Vector2(site.x, site.z);
            float inflatedRadius = site.reservedRadius + margin +
                ClearancePadding;
            if (SegmentReserveDistance(start, end, center) <=
                inflatedRadius + CorridorPadding)
            {
                relevantSites.Add(site);
            }
        }

        // note: A long world may contain many valid reserves, but only reserves near this route can block its bounded detour graph; distant sites remain protected by the final all-reserve validation below.
        relevantSites.Sort((left, right) =>
        {
            float leftDistance = SegmentReserveDistance(
                start,
                end,
                new Vector2(left.x, left.z));
            float rightDistance = SegmentReserveDistance(
                start,
                end,
                new Vector2(right.x, right.z));
            int distanceOrder = leftDistance.CompareTo(rightDistance);
            return distanceOrder != 0
                ? distanceOrder
                : string.CompareOrdinal(left.siteId, right.siteId);
        });
        if (relevantSites.Count > MaximumRelevantSites)
            relevantSites.RemoveRange(
                MaximumRelevantSites,
                relevantSites.Count - MaximumRelevantSites);

        // note: Candidate nodes sit just outside every inflated reserve so the graph can route around multiple overlapping detour choices.
        for (int siteIndex = 0; siteIndex < relevantSites.Count; siteIndex++)
        {
            YQSiteAnchorV2 site = relevantSites[siteIndex];
            Vector2 center = new Vector2(site.x, site.z);
            float radius = site.reservedRadius + margin + ClearancePadding;
            // note: Adjacent ring vertices are joined by chords. Circumscribe the inflated reserve so those chords clear it too; retain legacy routing first to preserve successful replay geometry.
            if (useCircumscribedRing)
                radius /= Mathf.Cos(Mathf.PI / Directions);
            for (int directionIndex = 0; directionIndex < Directions; directionIndex++)
            {
                float angle = directionIndex * Mathf.PI * 2f / Directions;
                Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (Mathf.Abs(candidate.x) <= boundary && Mathf.Abs(candidate.y) <= boundary)
                    nodes.Add(candidate);
            }
        }

        int nodeCount = nodes.Count;
        if (nodeCount < 3)
            return false;

        var distance = new float[nodeCount];
        var previous = new int[nodeCount];
        var visited = new bool[nodeCount];
        for (int index = 0; index < nodeCount; index++)
        {
            distance[index] = float.PositiveInfinity;
            previous[index] = -1;
        }
        distance[0] = 0f;

        // note: Dijkstra's deterministic Euclidean graph search is bounded by the fixed candidate count and handles more than one reserve without changing endpoints.
        for (int iteration = 0; iteration < nodeCount; iteration++)
        {
            int current = -1;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < nodeCount; index++)
            {
                if (!visited[index] &&
                    (distance[index] < bestDistance ||
                     (Mathf.Approximately(distance[index], bestDistance) &&
                      (current < 0 || index < current))))
                {
                    current = index;
                    bestDistance = distance[index];
                }
            }

            if (current < 0 || float.IsPositiveInfinity(bestDistance))
                break;
            if (current == 1)
                break;
            visited[current] = true;

            for (int next = 0; next < nodeCount; next++)
            {
                if (visited[next] || next == current ||
                    !IsReserveClearSegment(nodes[current], nodes[next], sites, margin))
                    continue;

                float candidateDistance = bestDistance +
                    Vector2.Distance(nodes[current], nodes[next]);
                if (candidateDistance < distance[next])
                {
                    distance[next] = candidateDistance;
                    previous[next] = current;
                }
            }
        }

        if (previous[1] < 0)
            return false;

        path = new List<Vector2>();
        for (int current = 1; current >= 0; current = previous[current])
            path.Add(nodes[current]);
        path.Reverse();
        if (path.Count < 2)
        {
            path = null;
            return false;
        }

        // note: The graph uses a bounded nearby subset for speed, but publication still requires every final segment to clear every unrelated reserve.
        for (int pathIndex = 1; pathIndex < path.Count; pathIndex++)
        {
            if (!IsReserveClearSegment(
                    path[pathIndex - 1],
                    path[pathIndex],
                    sites,
                    margin))
            {
                path = null;
                return false;
            }
        }

        return true;
    }

    private static bool IsReserveClearSegment(
        Vector2 start,
        Vector2 end,
        List<YQSiteAnchorV2> sites,
        float margin)
    {
        for (int siteIndex = 0; siteIndex < sites.Count; siteIndex++)
        {
            YQSiteAnchorV2 site = sites[siteIndex];
            float required = site.reservedRadius + margin;
            if (SegmentReserveDistance(
                    start,
                    end,
                    new Vector2(site.x, site.z)) < required)
                return false;
        }

        return true;
    }

    private static float SegmentReserveDistance(Vector2 a, Vector2 b, Vector2 center)
    {
        // note: Continuous segment distance catches crossings between control points, including short and degenerate segments.
        Vector2 delta = b - a;
        float t = delta.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(center - a, delta) / delta.sqrMagnitude) : 0f;
        return Vector2.Distance(center, a + delta * t);
    }

    public static void Build(YQSpatialBlueprintBuildContextV2 context)
    {
        Builder builder = new Builder(context);
        builder.Build();
    }

    public static IEnumerator BuildRoutine(
        YQSpatialBlueprintBuildContextV2 context)
    {
        Builder builder = new Builder(context);
        return builder.BuildRoutine();
    }

    private sealed class Builder
    {
        private readonly YQSpatialBlueprintBuildContextV2 context;
        private readonly YQSpatialSiteAccessQueryV2 waterAccessQuery;
        private readonly HashSet<string> routePairs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public Builder(YQSpatialBlueprintBuildContextV2 context)
        {
            this.context = context;
            // note: Hydrology already exists when the site-network phase begins, so every bounded candidate reuses one validated bank index.
            if (!YQSpatialSiteAccessV2.TryCreateQuery(
                    context.blueprint,
                    out YQSpatialSiteAccessQueryV2 preparedWaterAccess,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "V2 site water-access preparation failed: " + failure);
            }
            waterAccessQuery = preparedWaterAccess;
        }

        public void Build()
        {
            // note: Site reservations are chosen against the causal terrain domains before structures or settlement prefabs exist.
            AddOrigin();
            AddSettlements();
            AddHostileSites();
            AddPointsOfInterest();
            AddWaterfallCaveRelationship();
            AddStreamedContinuationAnchors();
            if (ShouldAddDevelopmentTravelFixtures())
            {
                // note: The dense beta itinerary exists only for the named regression profile; ordinary generated worlds retain their semantic sites and four boundary continuation anchors.
                AddStreamedBetaFixtures();
                AddStreamedContinuationWaypoints();
            }
            AttachNaturalFeatureAccess();
            AddSiteReserveFields();

            // note: Roads are a connected graph with explicit water crossings, not decoration scattered after placement.
            BuildPrimaryRoadTree();
            BuildSemanticRegionRoutes();
            AttachAccessibleSecondarySites();
            OrientSitesToRoutes();
            PopulateMetrics();
        }

        public IEnumerator BuildRoutine()
        {
            // note: Shadow site planning is divided into bounded stages so settlements and route graphs never compile in one long frame.
            AddOrigin();
            yield return null;
            AddSettlements();
            yield return null;
            AddHostileSites();
            yield return null;
            AddPointsOfInterest();
            yield return null;
            AddWaterfallCaveRelationship();
            yield return null;
            AddStreamedContinuationAnchors();
            yield return null;
            if (ShouldAddDevelopmentTravelFixtures())
            {
                // note: Keep stress-only radial fixtures out of player worlds while preserving the deterministic canonical regression run.
                AddStreamedBetaFixtures();
                yield return null;
                AddStreamedContinuationWaypoints();
                yield return null;
            }
            AttachNaturalFeatureAccess();
            yield return null;
            AddSiteReserveFields();
            yield return null;
            BuildPrimaryRoadTree();
            yield return null;
            BuildSemanticRegionRoutes();
            yield return null;
            AttachAccessibleSecondarySites();
            yield return null;
            OrientSitesToRoutes();
            yield return null;
            PopulateMetrics();
        }

        private bool ShouldAddDevelopmentTravelFixtures()
        {
            // note: Production content comes from the accepted semantic plan; only the explicit beta profile may inject synthetic traversal fixtures used by editor/runtime stress checks.
            return context.plan != null &&
                   string.Equals(
                       context.plan.worldSeed,
                       YQBetaDevelopmentFixture.CanonicalWorldSeed,
                       StringComparison.Ordinal);
        }

        private void AddOrigin()
        {
            YQRegionDomainV2 region = NearestRegion(Vector2.zero);
            YQSiteAnchorV2 origin = new YQSiteAnchorV2
            {
                siteId = context.blueprint.originSiteId,
                sourceSemanticId = "origin_vey",
                parentRegionId = region != null
                    ? region.regionId
                    : string.Empty,
                kind = YQSiteKindV2.Origin,
                placementMode = YQSitePlacementModeV2.RouteJunction,
                x = 0f,
                z = 0f,
                preferredHeadingDegrees = 0f,
                reservedRadius = 96f,
                terrainSearchRadius = 32f,
                maximumSlopeDegrees = 4f,
                minimumRouteAccess = 1f,
                minimumWaterAccess = 0f,
                requiresTerrainConformance = true
            };
            origin.requiredFunctions.Add(YQAssetFunctionV2.CulturalFocus);
            origin.requiredFunctions.Add(YQAssetFunctionV2.Circulation);
            origin.tags.Add("safe_spawn");
            origin.tags.Add("curated_origin_stage");
            AddSite(origin);
        }

        private void AddSettlements()
        {
            List<GeneratedSettlementRecord> settlements =
                YQSpatialBlueprintDeterminismV2.SortedRecords(
                    context.plan.settlements,
                    value => value != null
                        ? value.settlementId
                        : string.Empty);

            for (int index = 0; index < settlements.Count; index++)
            {
                GeneratedSettlementRecord semantic = settlements[index];
                string semanticId =
                    YQSpatialBlueprintDeterminismV2.SafeId(
                        semantic.settlementId,
                        "settlement:" + index);
                string siteId = "site:settlement:" + semanticId;
                YQRegionDomainV2 region =
                    context.ResolveRegion(semantic.regionId);
                float reservedRadius = Mathf.Clamp(
                    34f + Mathf.Sqrt(Mathf.Max(0, semantic.approxPopulation)) *
                    1.05f,
                    36f,
                    68f);
                // note: Semantic water intent controls access only; settlements without it reserve a fully dry inland footprint instead.
                float waterNeed = ResolveSettlementWaterNeed(semantic);
                bool keepEntireReserveDry = waterNeed <= 0f;
                Vector2 position = FindSitePosition(
                    region,
                    siteId,
                    reservedRadius,
                    0.28f,
                    0.7f,
                    waterNeed,
                    keepEntireReserveDry);
                YQSiteAnchorV2 site = new YQSiteAnchorV2
                {
                    siteId = siteId,
                    sourceSemanticId = semanticId,
                    parentRegionId = region != null
                        ? region.regionId
                        : string.Empty,
                    kind = YQSiteKindV2.Settlement,
                    placementMode =
                        YQSitePlacementModeV2.RouteFrontage,
                    x = position.x,
                    z = position.y,
                    reservedRadius = reservedRadius,
                    terrainSearchRadius = reservedRadius * 0.65f,
                    maximumSlopeDegrees = 7f,
                    minimumRouteAccess = 0.85f,
                    minimumWaterAccess = waterNeed,
                    requiresTerrainConformance = true
                };
                site.requiredFunctions.Add(YQAssetFunctionV2.Habitation);
                site.requiredFunctions.Add(YQAssetFunctionV2.Circulation);
                site.requiredFunctions.Add(YQAssetFunctionV2.Service);
                if (semantic.serviceSlots != null &&
                    semantic.serviceSlots.Count > 0)
                {
                    site.requiredFunctions.Add(YQAssetFunctionV2.Commerce);
                }
                site.tags.Add("semantic_settlement");
                AddSemanticTags(semantic.serviceSlots, "service:", site.tags);
                AddSite(site);
            }
        }

        private void AddHostileSites()
        {
            List<GeneratedEncampmentRecord> encampments =
                YQSpatialBlueprintDeterminismV2.SortedRecords(
                    context.plan.encampments,
                    value => value != null
                        ? value.encampmentId
                        : string.Empty);

            for (int index = 0; index < encampments.Count; index++)
            {
                GeneratedEncampmentRecord semantic = encampments[index];
                string semanticId =
                    YQSpatialBlueprintDeterminismV2.SafeId(
                        semantic.encampmentId,
                        "hostile:" + index);
                string siteId = "site:hostile:" + semanticId;
                YQRegionDomainV2 region =
                    context.ResolveRegion(semantic.regionId);
                float radius = Mathf.Clamp(
                    28f + semantic.threatTier * 3f,
                    28f,
                    52f);
                Vector2 position = FindSitePosition(
                    region,
                    siteId,
                    radius,
                    0.56f,
                    0.9f,
                    0f,
                    false);
                YQSiteAnchorV2 site = new YQSiteAnchorV2
                {
                    siteId = siteId,
                    sourceSemanticId = semanticId,
                    parentRegionId = region != null
                        ? region.regionId
                        : string.Empty,
                    kind = YQSiteKindV2.HostileSite,
                    placementMode =
                        YQSitePlacementModeV2.DefensibleEdge,
                    x = position.x,
                    z = position.y,
                    reservedRadius = radius,
                    terrainSearchRadius = radius * 0.55f,
                    maximumSlopeDegrees = 13f,
                    minimumRouteAccess = 0.32f,
                    minimumWaterAccess = 0f,
                    requiresTerrainConformance = true
                };
                site.requiredFunctions.Add(YQAssetFunctionV2.Encounter);
                site.requiredFunctions.Add(YQAssetFunctionV2.Reward);
                site.requiredFunctions.Add(YQAssetFunctionV2.Security);
                site.tags.Add("semantic_hostile_site");
                if (!string.IsNullOrWhiteSpace(semantic.stealthApproach))
                    site.tags.Add("alternate_approach");
                AddSite(site);
            }
        }

        private void AddPointsOfInterest()
        {
            List<GeneratedPointOfInterestRecord> points =
                YQSpatialBlueprintDeterminismV2.SortedRecords(
                    context.plan.pointsOfInterest,
                    value => value != null ? value.poiId : string.Empty);

            for (int index = 0; index < points.Count; index++)
            {
                GeneratedPointOfInterestRecord semantic = points[index];
                string semanticId =
                    YQSpatialBlueprintDeterminismV2.SafeId(
                        semantic.poiId,
                        "poi:" + index);
                string siteId = "site:poi:" + semanticId;
                YQRegionDomainV2 region =
                    context.ResolveRegion(semantic.regionId);
                bool naturalInterior = IsNaturalInteriorPoi(semantic);
                YQSitePlacementModeV2 placementMode =
                    naturalInterior
                        ? YQSitePlacementModeV2.FeatureAdjacent
                        : ResolvePoiPlacement(semantic);
                bool hidden = naturalInterior || IsHiddenPoi(semantic, siteId);
                float radius = naturalInterior ? 18f : 24f;
                float waterNeed =
                    placementMode == YQSitePlacementModeV2.WaterAdjacent
                        ? 0.75f
                        : 0f;
                Vector2 position = FindSitePosition(
                    region,
                    siteId,
                    radius,
                    hidden ? 0.62f : 0.38f,
                    hidden ? 0.92f : 0.82f,
                    waterNeed,
                    false);
                YQSiteAnchorV2 site = new YQSiteAnchorV2
                {
                    siteId = siteId,
                    sourceSemanticId = semanticId,
                    parentRegionId = region != null
                        ? region.regionId
                        : string.Empty,
                    kind = naturalInterior
                        ? YQSiteKindV2.NaturalFeature
                        : YQSiteKindV2.PointOfInterest,
                    placementMode = hidden
                        ? YQSitePlacementModeV2.Secluded
                        : placementMode,
                    x = position.x,
                    z = position.y,
                    reservedRadius = radius,
                    terrainSearchRadius = 22f,
                    maximumSlopeDegrees = 11f,
                    minimumRouteAccess = hidden ? 0f : 0.42f,
                    minimumWaterAccess = waterNeed,
                    requiresTerrainConformance = true,
                    hiddenFromPrimaryRoute = hidden
                };
                if (naturalInterior)
                {
                    // note: Explicit cave-like POIs become typed traversable sites; their entrance, encounter, and reward contract remains data-driven.
                    site.requiredFunctions.Add(YQAssetFunctionV2.Transition);
                    site.requiredFunctions.Add(YQAssetFunctionV2.Encounter);
                    site.requiredFunctions.Add(YQAssetFunctionV2.Reward);
                    site.tags.Add("semantic_natural_feature");
                    site.tags.Add("concealed_access_candidate");
                    YQTerrainFieldV2 caveMass = new YQTerrainFieldV2
                    {
                        fieldId = "terrain:cave:poi:" + semanticId,
                        parentRegionId = site.parentRegionId,
                        kind = YQTerrainFieldKindV2.CaveMass,
                        strength = -0.42f,
                        radius = 24f,
                        falloff = 3.2f
                    };
                    caveMass.controlPoints.Add(
                        YQSpatialBlueprintDeterminismV2.Point(
                            position,
                            0.5f,
                            18f));
                    caveMass.tags.Add("subtractive_volume_candidate");
                    context.blueprint.terrainFields.Add(caveMass);
                }
                else
                {
                    // note: Ordinary POIs retain their cultural and transition requirements without being mistaken for an interior site.
                    site.requiredFunctions.Add(YQAssetFunctionV2.CulturalFocus);
                    site.requiredFunctions.Add(YQAssetFunctionV2.Transition);
                }
                if (hidden && !naturalInterior)
                {
                    site.requiredFunctions.Add(YQAssetFunctionV2.Reward);
                }
                site.tags.Add("semantic_point_of_interest");
                AddSemanticTags(semantic.tags, string.Empty, site.tags);
                AddSite(site);
            }
        }

        private void AddWaterfallCaveRelationship()
        {
            YQHydrologyFeatureV2 waterfall = FindHydrology(
                YQHydrologyKindV2.Waterfall);
            if (waterfall == null || waterfall.controlPoints.Count < 2)
                return;

            YQBlueprintPointV2 top = waterfall.controlPoints[0];
            YQBlueprintPointV2 bottom = waterfall.controlPoints[1];
            Vector2 flow = new Vector2(bottom.x - top.x, bottom.z - top.z);
            if (flow.sqrMagnitude < 0.001f)
                flow = Vector2.up;
            flow.Normalize();
            Vector2 lateral = new Vector2(-flow.y, flow.x);
            float side = YQSpatialBlueprintDeterminismV2.Hash01(
                context.seed + "|waterfall_cave_side") < 0.5f
                ? -1f
                : 1f;
            Vector2 position = context.Clamp(
                new Vector2(bottom.x, bottom.z) - flow * 16f +
                lateral * (side * 7f));
            YQRegionDomainV2 region = NearestRegion(position);
            YQSiteAnchorV2 cave = new YQSiteAnchorV2
            {
                siteId = "site:natural:waterfall_cave",
                sourceSemanticId = "generated:waterfall_cave",
                parentRegionId = region != null
                    ? region.regionId
                    : string.Empty,
                kind = YQSiteKindV2.NaturalFeature,
                placementMode = YQSitePlacementModeV2.FeatureAdjacent,
                x = position.x,
                z = position.y,
                preferredHeadingDegrees =
                    Mathf.Atan2(flow.x, flow.y) * Mathf.Rad2Deg,
                reservedRadius = 18f,
                terrainSearchRadius = 12f,
                maximumSlopeDegrees = 24f,
                minimumRouteAccess = 0f,
                // note: The cave is deliberately behind, not centered inside, the waterfall footprint; 0.90 preserves strict adjacency without fake relationship-based access.
                minimumWaterAccess = 0.9f,
                requiresTerrainConformance = true,
                hiddenFromPrimaryRoute = true
            };
            cave.requiredFunctions.Add(YQAssetFunctionV2.Transition);
            cave.requiredFunctions.Add(YQAssetFunctionV2.Encounter);
            cave.requiredFunctions.Add(YQAssetFunctionV2.Reward);
            cave.tags.Add("concealed_access_candidate");
            cave.tags.Add("waterfall_backed_space");
            AddSite(cave);

            YQTerrainFieldV2 caveMass = new YQTerrainFieldV2
            {
                fieldId = "terrain:cave:waterfall",
                parentRegionId = cave.parentRegionId,
                kind = YQTerrainFieldKindV2.CaveMass,
                strength = -0.58f,
                radius = 28f,
                falloff = 3.2f
            };
            caveMass.controlPoints.Add(
                YQSpatialBlueprintDeterminismV2.Point(
                    position,
                    Mathf.Clamp01(bottom.normalizedElevation + 0.08f),
                    24f));
            caveMass.tags.Add("subtractive_volume_candidate");
            context.blueprint.terrainFields.Add(caveMass);

            context.AddRelationship(
                "relationship:cave_behind_waterfall",
                cave.siteId,
                waterfall.hydrologyId,
                YQSpatialRelationshipKindV2.Behind,
                0f,
                38f,
                "concealed_reward_space");
            context.AddRelationship(
                "relationship:cave_mass_contains_access",
                caveMass.fieldId,
                cave.siteId,
                YQSpatialRelationshipKindV2.Contains,
                0f,
                caveMass.radius,
                "terrain_authored_access");
        }

        private void AddStreamedContinuationAnchors()
        {
            // note: Accepted streamed continuation needs a real topology endpoint at the authored boundary; otherwise terminal rays never reach the first physical cell outside the 1024m origin terrain.
            float boundary = Mathf.Min(
                508f,
                context.blueprint.worldSize * 0.5f - 4f);
            if (boundary <= 0f)
                return;

            AddStreamedContinuationAnchor(
                "east",
                new Vector2(boundary, 128f),
                90f);
            AddStreamedContinuationAnchor(
                "west",
                new Vector2(-boundary, -128f),
                270f);
            AddStreamedContinuationAnchor(
                "north",
                new Vector2(128f, boundary),
                0f);
            AddStreamedContinuationAnchor(
                "south",
                new Vector2(-128f, -boundary),
                180f);
        }

        private void AddStreamedContinuationAnchor(
            string direction,
            Vector2 position,
            float headingDegrees)
        {
            string siteId = "site:continuation:" + direction;
            if (context.sitesById.ContainsKey(siteId))
                return;

            YQRegionDomainV2 region = NearestRegion(position);
            YQSiteAnchorV2 anchor = new YQSiteAnchorV2
            {
                siteId = siteId,
                sourceSemanticId = "generated:streamed_continuation:" + direction,
                parentRegionId = region != null ? region.regionId : string.Empty,
                kind = YQSiteKindV2.NaturalFeature,
                placementMode = YQSitePlacementModeV2.FeatureAdjacent,
                x = position.x,
                z = position.y,
                preferredHeadingDegrees = headingDegrees,
                reservedRadius = 16f,
                terrainSearchRadius = 20f,
                maximumSlopeDegrees = 18f,
                minimumRouteAccess = 0f,
                minimumWaterAccess = 0f,
                requiresTerrainConformance = true,
                hiddenFromPrimaryRoute = false
            };
            anchor.requiredFunctions.Add(YQAssetFunctionV2.Transition);
            anchor.requiredFunctions.Add(YQAssetFunctionV2.CulturalFocus);
            anchor.tags.Add("accepted_streamed_continuation");
            anchor.tags.Add("beta_travel_fixture");
            AddSite(anchor);
        }

        private void AddStreamedBetaFixtures()
        {
            // note: These deterministic structural fixtures guarantee a reachable beta itinerary beyond the authored edge without inventing player-facing names or bypassing accepted V2 routing.
            float boundary = Mathf.Min(
                508f,
                context.blueprint.worldSize * 0.5f - 4f);
            if (boundary <= 0f)
                return;

            AddBetaSettlement(
                "east",
                new Vector2(boundary + 128f, 128f),
                48f,
                90f);
            AddBetaHostileSite(
                "east",
                new Vector2(boundary + 128f, -160f),
                38f,
                90f);
            AddBetaCave(
                "west",
                new Vector2(-boundary - 128f, -128f),
                24f,
                270f);
            AddBetaLandmark(
                "north",
                new Vector2(128f, boundary + 128f),
                30f,
                0f,
                false);
            AddBetaLandmark(
                "south",
                new Vector2(-128f, -boundary - 128f),
                72f,
                180f,
                true);
        }

        private void AddStreamedContinuationWaypoints()
        {
            // note: These accepted POIs extend the deterministic travel itinerary across the observed multi-kilometre travel envelope; they are persisted site anchors, not renderer-side markers or random scatter.
            float boundary = Mathf.Min(
                508f,
                context.blueprint.worldSize * 0.5f - 4f);
            float near = boundary + 384f;
            float far = boundary + 896f;
            float limit = context.UsableHalfExtent - 48f;
            if (near > limit || far > limit)
                return;

            AddContinuationWaypoint("east", "near", new Vector2(near, 256f), 28f, 90f);
            AddContinuationWaypoint("east", "far", new Vector2(far, 384f), 34f, 90f);
            AddContinuationWaypoint("west", "near", new Vector2(-near, -256f), 28f, 270f);
            AddContinuationWaypoint("west", "far", new Vector2(-far, -384f), 34f, 270f);
            AddContinuationWaypoint("north", "near", new Vector2(256f, near), 28f, 0f);
            AddContinuationWaypoint("north", "far", new Vector2(384f, far), 34f, 0f);
            AddContinuationWaypoint("south", "near", new Vector2(-256f, -near), 28f, 180f);
            AddContinuationWaypoint("south", "far", new Vector2(-384f, -far), 34f, 180f);

            // note: Fixed distance bands keep accepted POI and route demand present after the first 1.4 km continuation pair, while the streamer still decides which cells receive physical resources.
            float[] distanceBands = { 2048f, 4096f, 6144f };
            for (int bandIndex = 0; bandIndex < distanceBands.Length; bandIndex++)
            {
                float distance = distanceBands[bandIndex];
                if (distance > limit)
                    continue;

                float lateral = 256f + bandIndex * 128f;
                float radius = 30f + bandIndex * 2f;
                string band = "band_" + (bandIndex + 1).ToString("00");
                AddContinuationWaypoint("east", band, new Vector2(distance, lateral), radius, 90f);
                AddContinuationWaypoint("west", band, new Vector2(-distance, -lateral), radius, 270f);
                AddContinuationWaypoint("north", band, new Vector2(lateral, distance), radius, 0f);
                AddContinuationWaypoint("south", band, new Vector2(-lateral, -distance), radius, 180f);

                // note: Diagonal arms cover ordinary corner travel between the four cardinal corridors, including the northwest cells observed in the failing manual run.
                Vector2 diagonalDistance = new Vector2(distance, distance).normalized;
                AddContinuationWaypoint("northeast", band, diagonalDistance * distance, radius, 45f);
                AddContinuationWaypoint("northwest", band, new Vector2(-diagonalDistance.x, diagonalDistance.y) * distance, radius, 315f);
                AddContinuationWaypoint("southeast", band, new Vector2(diagonalDistance.x, -diagonalDistance.y) * distance, radius, 135f);
                AddContinuationWaypoint("southwest", band, -diagonalDistance * distance, radius, 225f);

                // note: Inner diagonal fan lanes cross the observed northwest manual-run cells (-45,23..24) without turning every wilderness cell into a site; each lane remains an accepted routed corridor.
                // note: The 0.4166667 ratio places the 6.144 km fan on the shared -45,23/-45,24 boundary at Z=2560, so route padding admits both cells.
                float fanLateral = distance * 0.4166667f;
                AddContinuationWaypoint("northeast_fan_x", band, new Vector2(distance, fanLateral), radius, 45f);
                AddContinuationWaypoint("northeast_fan_z", band, new Vector2(fanLateral, distance), radius, 45f);
                AddContinuationWaypoint("northwest_fan_x", band, new Vector2(-distance, fanLateral), radius, 315f);
                AddContinuationWaypoint("northwest_fan_z", band, new Vector2(-fanLateral, distance), radius, 315f);
                AddContinuationWaypoint("southeast_fan_x", band, new Vector2(distance, -fanLateral), radius, 135f);
                AddContinuationWaypoint("southeast_fan_z", band, new Vector2(fanLateral, -distance), radius, 135f);
                AddContinuationWaypoint("southwest_fan_x", band, new Vector2(-distance, -fanLateral), radius, 225f);
                AddContinuationWaypoint("southwest_fan_z", band, new Vector2(-fanLateral, -distance), radius, 225f);
            }
        }

        private void AddContinuationWaypoint(
            string direction,
            string distanceBand,
            Vector2 position,
            float radius,
            float heading)
        {
            YQRegionDomainV2 region = NearestRegion(position);
            YQSiteAnchorV2 site = new YQSiteAnchorV2
            {
                siteId = "site:continuation:poi:" + direction + ":" + distanceBand,
                sourceSemanticId = "generated:continuation:poi:" + direction + ":" + distanceBand,
                parentRegionId = region != null ? region.regionId : string.Empty,
                kind = YQSiteKindV2.PointOfInterest,
                placementMode = YQSitePlacementModeV2.RouteFrontage,
                x = position.x,
                z = position.y,
                preferredHeadingDegrees = heading,
                reservedRadius = radius,
                terrainSearchRadius = radius,
                maximumSlopeDegrees = 20f,
                minimumRouteAccess = 0f,
                minimumWaterAccess = 0f,
                requiresTerrainConformance = true
            };
            site.requiredFunctions.Add(YQAssetFunctionV2.CulturalFocus);
            site.requiredFunctions.Add(YQAssetFunctionV2.Transition);
            site.requiredFunctions.Add(YQAssetFunctionV2.Reward);
            site.tags.Add("accepted_streamed_continuation");
            site.tags.Add("continuation_poi");
            site.tags.Add("resource_area");
            AddSite(site);
        }

        private void AddBetaSettlement(
            string direction,
            Vector2 position,
            float radius,
            float heading)
        {
            YQRegionDomainV2 region = NearestRegion(position);
            YQSiteAnchorV2 site = new YQSiteAnchorV2
            {
                siteId = "site:beta:settlement:" + direction,
                sourceSemanticId = "generated:beta:settlement:" + direction,
                parentRegionId = region != null ? region.regionId : string.Empty,
                kind = YQSiteKindV2.Settlement,
                placementMode = YQSitePlacementModeV2.RouteFrontage,
                x = position.x,
                z = position.y,
                preferredHeadingDegrees = heading,
                reservedRadius = radius,
                terrainSearchRadius = radius,
                maximumSlopeDegrees = 18f,
                minimumRouteAccess = 0f,
                minimumWaterAccess = 0f,
                requiresTerrainConformance = true
            };
            site.requiredFunctions.Add(YQAssetFunctionV2.Habitation);
            site.requiredFunctions.Add(YQAssetFunctionV2.Circulation);
            site.requiredFunctions.Add(YQAssetFunctionV2.Service);
            site.requiredFunctions.Add(YQAssetFunctionV2.Commerce);
            site.tags.Add("beta_fixture_settlement");
            site.tags.Add("service:merchant");
            site.tags.Add("multi_cell_site");
            AddSite(site);
        }

        private void AddBetaHostileSite(
            string direction,
            Vector2 position,
            float radius,
            float heading)
        {
            YQRegionDomainV2 region = NearestRegion(position);
            YQSiteAnchorV2 site = new YQSiteAnchorV2
            {
                siteId = "site:beta:hostile:" + direction,
                sourceSemanticId = "generated:beta:hostile:" + direction,
                parentRegionId = region != null ? region.regionId : string.Empty,
                kind = YQSiteKindV2.HostileSite,
                placementMode = YQSitePlacementModeV2.DefensibleEdge,
                x = position.x,
                z = position.y,
                preferredHeadingDegrees = heading,
                reservedRadius = radius,
                terrainSearchRadius = radius,
                maximumSlopeDegrees = 20f,
                minimumRouteAccess = 0f,
                minimumWaterAccess = 0f,
                requiresTerrainConformance = true
            };
            site.requiredFunctions.Add(YQAssetFunctionV2.Encounter);
            site.requiredFunctions.Add(YQAssetFunctionV2.Reward);
            site.requiredFunctions.Add(YQAssetFunctionV2.Security);
            site.tags.Add("beta_fixture_hostile");
            site.tags.Add("ruin_or_encampment");
            AddSite(site);
        }

        private void AddBetaCave(
            string direction,
            Vector2 position,
            float radius,
            float heading)
        {
            YQRegionDomainV2 region = NearestRegion(position);
            YQSiteAnchorV2 site = new YQSiteAnchorV2
            {
                siteId = "site:beta:cave:" + direction,
                sourceSemanticId = "generated:beta:cave:" + direction,
                parentRegionId = region != null ? region.regionId : string.Empty,
                kind = YQSiteKindV2.NaturalFeature,
                placementMode = YQSitePlacementModeV2.Secluded,
                x = position.x,
                z = position.y,
                preferredHeadingDegrees = heading,
                reservedRadius = radius,
                terrainSearchRadius = radius,
                maximumSlopeDegrees = 24f,
                minimumRouteAccess = 0f,
                minimumWaterAccess = 0f,
                requiresTerrainConformance = true,
                hiddenFromPrimaryRoute = false
            };
            site.requiredFunctions.Add(YQAssetFunctionV2.Transition);
            site.requiredFunctions.Add(YQAssetFunctionV2.Encounter);
            site.requiredFunctions.Add(YQAssetFunctionV2.Reward);
            site.tags.Add("beta_fixture_cave");
            site.tags.Add("interior");
            site.tags.Add("entrance_inside_exit");
            AddSite(site);

            YQTerrainFieldV2 caveMass = new YQTerrainFieldV2
            {
                fieldId = "terrain:beta:cave:" + direction,
                parentRegionId = site.parentRegionId,
                kind = YQTerrainFieldKindV2.CaveMass,
                strength = -0.62f,
                radius = radius + 14f,
                falloff = 3.4f
            };
            caveMass.controlPoints.Add(
                YQSpatialBlueprintDeterminismV2.Point(
                    position,
                    0.34f,
                    radius));
            caveMass.tags.Add("subtractive_volume_candidate");
            caveMass.tags.Add("beta_fixture");
            context.blueprint.terrainFields.Add(caveMass);
            context.AddRelationship(
                "relationship:beta_cave_access:" + direction,
                site.siteId,
                caveMass.fieldId,
                YQSpatialRelationshipKindV2.Contains,
                0f,
                caveMass.radius,
                "entrance_inside_exit");
        }

        private void AddBetaLandmark(
            string direction,
            Vector2 position,
            float radius,
            float heading,
            bool multiCell)
        {
            YQRegionDomainV2 region = NearestRegion(position);
            YQSiteAnchorV2 site = new YQSiteAnchorV2
            {
                siteId = "site:beta:landmark:" + direction,
                sourceSemanticId = "generated:beta:landmark:" + direction,
                parentRegionId = region != null ? region.regionId : string.Empty,
                kind = YQSiteKindV2.PointOfInterest,
                placementMode = YQSitePlacementModeV2.RouteFrontage,
                x = position.x,
                z = position.y,
                preferredHeadingDegrees = heading,
                reservedRadius = radius,
                terrainSearchRadius = radius,
                maximumSlopeDegrees = 20f,
                minimumRouteAccess = 0f,
                minimumWaterAccess = 0f,
                requiresTerrainConformance = true
            };
            site.requiredFunctions.Add(YQAssetFunctionV2.CulturalFocus);
            site.requiredFunctions.Add(YQAssetFunctionV2.Transition);
            site.requiredFunctions.Add(YQAssetFunctionV2.Reward);
            site.tags.Add("beta_fixture_landmark");
            site.tags.Add("resource_area");
            if (multiCell)
            {
                site.tags.Add("multi_cell_site");
                // note: The south beta landmark owns one reviewed root but reserves four deterministic neighboring sectors for shared access and lifecycle checks.
                AddMultiCellMember(site, "west", position + new Vector2(-88f, 0f), 0);
                AddMultiCellMember(site, "east", position + new Vector2(88f, 0f), 1);
                AddMultiCellMember(site, "south", position + new Vector2(0f, -88f), 2);
                AddMultiCellMember(site, "north", position + new Vector2(0f, 88f), 3);
            }
            AddSite(site);
        }

        private static void AddMultiCellMember(
            YQSiteAnchorV2 site,
            string direction,
            Vector2 position,
            int sectorIndex)
        {
            if (site == null)
                return;
            site.memberFootprint.Add(
                new YQSiteMemberFootprintV2
                {
                    memberId = site.siteId + ":sector:" + direction,
                    x = position.x,
                    z = position.y,
                    reservedRadius = 24f,
                    sectorIndex = sectorIndex
                });
        }

        private void AttachNaturalFeatureAccess()
        {
            // note: Concealed natural features get one deterministic trail to the nearest primary hub; they remain off the primary road graph while retaining real physical access.
            List<YQSiteAnchorV2> primary = new List<YQSiteAnchorV2>();
            List<YQSiteAnchorV2> natural = new List<YQSiteAnchorV2>();
            foreach (YQSiteAnchorV2 site in context.sitesById.Values)
            {
                if (site == null)
                    continue;
                if (site.kind == YQSiteKindV2.Origin ||
                    site.kind == YQSiteKindV2.Settlement)
                    primary.Add(site);
                else if (site.kind == YQSiteKindV2.NaturalFeature &&
                         site.requiredFunctions.Contains(YQAssetFunctionV2.Transition))
                    natural.Add(site);
            }

            primary.Sort((left, right) => string.CompareOrdinal(left.siteId, right.siteId));
            natural.Sort((left, right) => string.CompareOrdinal(left.siteId, right.siteId));
            for (int index = 0; index < natural.Count; index++)
            {
                YQSiteAnchorV2 nearest = NearestSite(natural[index], primary);
                if (nearest == null)
                    continue;
                AddRoute(
                    nearest,
                    natural[index],
                    YQRouteClassV2.Trail,
                    string.Empty,
                    "concealed_site_access");
            }
        }

        private void AddSiteReserveFields()
        {
            for (int index = 0; index < context.blueprint.sites.Count; index++)
            {
                YQSiteAnchorV2 site = context.blueprint.sites[index];
                if (site == null || site.kind == YQSiteKindV2.NaturalFeature)
                    continue;

                YQTerrainFieldV2 reserve = new YQTerrainFieldV2
                {
                    fieldId = "terrain:reserve:" + site.siteId,
                    parentRegionId = site.parentRegionId,
                    kind = YQTerrainFieldKindV2.SiteReserve,
                    strength = -0.08f,
                    radius = site.reservedRadius * 1.12f,
                    falloff = 2.8f
                };
                reserve.controlPoints.Add(
                    YQSpatialBlueprintDeterminismV2.Point(
                        new Vector2(site.x, site.z),
                        0.4f,
                        reserve.radius));
                reserve.tags.Add("pre_terrain_buildable_reserve");
                reserve.tags.Add("preserve_perimeter_relief");
                context.blueprint.terrainFields.Add(reserve);

                context.AddRelationship(
                    "relationship:reserve_contains:" + site.siteId,
                    reserve.fieldId,
                    site.siteId,
                    YQSpatialRelationshipKindV2.Contains,
                    0f,
                    reserve.radius,
                    "terrain_before_site");
            }
        }

        private void BuildPrimaryRoadTree()
        {
            List<YQSiteAnchorV2> primary = new List<YQSiteAnchorV2>();
            primary.Add(context.sitesById[context.blueprint.originSiteId]);

            foreach (YQSiteAnchorV2 site in context.sitesById.Values)
            {
                if (site.kind == YQSiteKindV2.Settlement)
                    primary.Add(site);
            }

            primary.Sort((left, right) =>
            {
                if (left.kind == YQSiteKindV2.Origin)
                    return right.kind == YQSiteKindV2.Origin ? 0 : -1;
                if (right.kind == YQSiteKindV2.Origin)
                    return 1;
                return string.CompareOrdinal(left.siteId, right.siteId);
            });

            HashSet<string> connected =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    context.blueprint.originSiteId
                };

            while (connected.Count < primary.Count)
            {
                YQSiteAnchorV2 bestFrom = null;
                YQSiteAnchorV2 bestTo = null;
                float bestDistance = float.MaxValue;
                string bestPair = string.Empty;

                for (int fromIndex = 0;
                     fromIndex < primary.Count;
                     fromIndex++)
                {
                    YQSiteAnchorV2 from = primary[fromIndex];
                    if (!connected.Contains(from.siteId))
                        continue;

                    for (int toIndex = 0;
                         toIndex < primary.Count;
                         toIndex++)
                    {
                        YQSiteAnchorV2 to = primary[toIndex];
                        if (connected.Contains(to.siteId))
                            continue;

                        float distance = SiteDistance(from, to);
                        string pair = PairKey(from.siteId, to.siteId);
                        if (distance < bestDistance - 0.001f ||
                            (Mathf.Abs(distance - bestDistance) <= 0.001f &&
                             string.CompareOrdinal(pair, bestPair) < 0))
                        {
                            bestFrom = from;
                            bestTo = to;
                            bestDistance = distance;
                            bestPair = pair;
                        }
                    }
                }

                if (bestFrom == null || bestTo == null)
                    break;

                AddRoute(
                    bestFrom,
                    bestTo,
                    YQRouteClassV2.PrimaryRoad,
                    string.Empty,
                    "settlement_spine");
                connected.Add(bestTo.siteId);
            }
        }

        private void BuildSemanticRegionRoutes()
        {
            List<GeneratedWorldRouteRecord> semanticRoutes =
                YQSpatialBlueprintDeterminismV2.SortedRecords(
                    context.plan.routes,
                    value => value != null ? value.routeId : string.Empty);

            for (int index = 0; index < semanticRoutes.Count; index++)
            {
                GeneratedWorldRouteRecord semantic = semanticRoutes[index];
                YQSiteAnchorV2 from = RepresentativeSite(semantic.fromRegionId);
                YQSiteAnchorV2 to = RepresentativeSite(semantic.toRegionId);
                if (from == null || to == null || from == to)
                    continue;

                AddRoute(
                    from,
                    to,
                    ResolveSemanticRouteClass(semantic.routeKind),
                    semantic.routeId,
                    "semantic_region_connection");
            }
        }

        private void AttachAccessibleSecondarySites()
        {
            List<YQSiteAnchorV2> primary = new List<YQSiteAnchorV2>();
            List<YQSiteAnchorV2> secondary = new List<YQSiteAnchorV2>();

            foreach (YQSiteAnchorV2 site in context.sitesById.Values)
            {
                if (site.kind == YQSiteKindV2.Origin ||
                    site.kind == YQSiteKindV2.Settlement)
                {
                    primary.Add(site);
                }
                else if (!site.hiddenFromPrimaryRoute &&
                         site.kind != YQSiteKindV2.NaturalFeature)
                {
                    secondary.Add(site);
                }
            }

            primary.Sort(
                (left, right) => string.CompareOrdinal(
                    left.siteId,
                    right.siteId));
            secondary.Sort(
                (left, right) => string.CompareOrdinal(
                    left.siteId,
                    right.siteId));

            for (int index = 0; index < secondary.Count; index++)
            {
                YQSiteAnchorV2 site = secondary[index];
                YQSiteAnchorV2 nearest = NearestSite(site, primary);
                if (nearest == null)
                    continue;

                YQRouteClassV2 routeClass = site.kind == YQSiteKindV2.HostileSite
                    ? YQRouteClassV2.Trail
                    : YQRouteClassV2.SecondaryRoad;
                if (AddRoute(
                    nearest,
                    site,
                    routeClass,
                    string.Empty,
                    "site_access",
                    true) != null)
                    continue;

                // note: A rejected secondary edge does not invalidate its accepted site. Try other connected primary anchors in stable distance/ID order without moving any reserves.
                var alternatives = new List<YQSiteAnchorV2>(primary);
                alternatives.Sort((left, right) =>
                {
                    int order = SiteDistance(site, left).CompareTo(SiteDistance(site, right));
                    return order != 0 ? order : string.CompareOrdinal(left.siteId, right.siteId);
                });
                bool connected = false;
                foreach (YQSiteAnchorV2 alternative in alternatives)
                {
                    if (alternative == nearest)
                        continue;
                    if (AddRoute(alternative, site, routeClass, string.Empty,
                            "site_access", true) == null)
                        continue;
                    connected = true;
                    break;
                }
                // note: Never publish an inaccessible required site just to finish compilation; only fully validated replacement edges enter the graph.
                if (!connected)
                    throw new InvalidOperationException(
                        "No reserve-clear site access connection is available for " + site.siteId);
            }
        }

        private YQRouteCorridorV2 AddRoute(
            YQSiteAnchorV2 from,
            YQSiteAnchorV2 to,
            YQRouteClassV2 routeClass,
            string sourceSemanticRouteId,
            string tag,
            bool allowConnectionReplan = false)
        {
            string pair = PairKey(from.siteId, to.siteId);
            if (!routePairs.Add(pair))
            {
                YQRouteCorridorV2 existing = FindRoute(pair);
                if (existing != null &&
                    string.IsNullOrWhiteSpace(existing.sourceSemanticRouteId) &&
                    !string.IsNullOrWhiteSpace(sourceSemanticRouteId))
                {
                    existing.sourceSemanticRouteId = sourceSemanticRouteId;
                    existing.tags.Add("semantic_region_connection");
                }

                return existing;
            }

            YQRouteCorridorV2 route = new YQRouteCorridorV2
            {
                routeId = "route:" + routeClass.ToString().ToLowerInvariant() +
                          ":" + pair,
                sourceSemanticRouteId = sourceSemanticRouteId ?? string.Empty,
                fromSiteId = from.siteId,
                toSiteId = to.siteId,
                parentRegionId = string.Equals(
                    from.parentRegionId,
                    to.parentRegionId,
                    StringComparison.OrdinalIgnoreCase)
                    ? from.parentRegionId
                    : string.Empty,
                routeClass = routeClass,
                width = RouteWidth(routeClass),
                shoulderWidth = RouteShoulder(routeClass),
                maximumGradeDegrees = RouteMaximumGrade(routeClass)
            };
            BuildRouteControlPoints(route, from, to);
            if (!TryAvoidUnrelatedSiteReserves(context.blueprint, route, out string clearanceFailure))
            {
                // note: Roll back only this unpublished edge; crossings and frontage relationships are added only after reserve clearance succeeds.
                routePairs.Remove(pair);
                if (allowConnectionReplan)
                {
                    Debug.LogWarning("[YQSpatialSiteNetworkCompilerV2] Replanning rejected site access: " + clearanceFailure);
                    return null;
                }
                throw new InvalidOperationException(clearanceFailure);
            }
            AddWaterCrossings(route);
            if (!string.IsNullOrWhiteSpace(tag))
                route.tags.Add(tag);
            route.tags.Add("terrain_carved_corridor");
            context.blueprint.routes.Add(route);

            context.AddRelationship(
                "relationship:frontage:" + route.routeId + ":from",
                from.siteId,
                route.routeId,
                YQSpatialRelationshipKindV2.FrontsRoute,
                0f,
                from.reservedRadius,
                "frontage");
            context.AddRelationship(
                "relationship:frontage:" + route.routeId + ":to",
                to.siteId,
                route.routeId,
                YQSpatialRelationshipKindV2.FrontsRoute,
                0f,
                to.reservedRadius,
                "frontage");
            return route;
        }

        private void BuildRouteControlPoints(
            YQRouteCorridorV2 route,
            YQSiteAnchorV2 from,
            YQSiteAnchorV2 to)
        {
            Vector2 start = new Vector2(from.x, from.z);
            Vector2 end = new Vector2(to.x, to.z);
            Vector2 delta = end - start;
            float distance = Mathf.Max(0.001f, delta.magnitude);
            Vector2 perpendicular = new Vector2(-delta.y, delta.x) / distance;
            float bend = Mathf.Min(42f, distance * 0.16f) *
                         Mathf.Lerp(
                             -1f,
                             1f,
                             YQSpatialBlueprintDeterminismV2.Hash01(
                                 context.seed + "|route_bend|" + route.routeId));

            route.controlPoints.Add(
                YQSpatialBlueprintDeterminismV2.Point(
                    start,
                    0.42f,
                    route.width));
            route.controlPoints.Add(
                YQSpatialBlueprintDeterminismV2.Point(
                    context.Clamp(Vector2.Lerp(start, end, 0.34f) +
                                  perpendicular * bend),
                    0.41f,
                    route.width));
            route.controlPoints.Add(
                YQSpatialBlueprintDeterminismV2.Point(
                    context.Clamp(Vector2.Lerp(start, end, 0.67f) +
                                  perpendicular * bend * 0.65f),
                    0.4f,
                    route.width));
            route.controlPoints.Add(
                YQSpatialBlueprintDeterminismV2.Point(
                    end,
                    0.39f,
                    route.width));

            // note: A site-to-site route that follows a river must leave its channel before crossing generation; retain its endpoints and use a deterministic dry-bank corridor.
            RouteAlongBankWhenFollowingWater(route);
        }

        private void RouteAlongBankWhenFollowingWater(YQRouteCorridorV2 route)
        {
            if (route == null || route.controlPoints == null ||
                route.controlPoints.Count < 2 || context?.blueprint?.hydrology == null)
                return;

            YQHydrologyFeatureV2 selectedWater = null;
            float selectedScore = float.NegativeInfinity;
            for (int waterIndex = 0; waterIndex < context.blueprint.hydrology.Count; waterIndex++)
            {
                YQHydrologyFeatureV2 water = context.blueprint.hydrology[waterIndex];
                if (water == null || water.kind != YQHydrologyKindV2.River ||
                    water.controlPoints == null || water.controlPoints.Count < 2)
                    continue;

                MeasureRouteWaterOverlap(route, water, out float routeLength,
                    out float overlapLength, out float longestOverlap);
                bool startAligned = EndpointFollowsWater(route, water, true);
                bool endAligned = EndpointFollowsWater(route, water, false);
                bool sustainedOverlap = longestOverlap >= 64f && routeLength > 0.01f &&
                    overlapLength >= Mathf.Max(96f, routeLength * 0.25f);
                if (!sustainedOverlap && !startAligned && !endAligned)
                    continue;

                float score = (routeLength > 0.01f ? overlapLength / routeLength : 0f) +
                    (startAligned ? 0.5f : 0f) + (endAligned ? 0.5f : 0f);
                if (score > selectedScore ||
                    (Mathf.Approximately(score, selectedScore) && selectedWater != null &&
                     string.CompareOrdinal(water.hydrologyId, selectedWater.hydrologyId) < 0))
                {
                    selectedWater = water;
                    selectedScore = score;
                }
            }

            if (selectedWater != null)
                BuildDeterministicBankRoute(route, selectedWater);
        }

        private static void MeasureRouteWaterOverlap(
            YQRouteCorridorV2 route,
            YQHydrologyFeatureV2 water,
            out float routeLength,
            out float overlapLength,
            out float longestOverlap)
        {
            routeLength = 0f;
            overlapLength = 0f;
            longestOverlap = 0f;
            float currentOverlap = 0f;
            const float sampleSpacing = 8f;
            for (int segment = 1; segment < route.controlPoints.Count; segment++)
            {
                YQBlueprintPointV2 first = route.controlPoints[segment - 1];
                YQBlueprintPointV2 last = route.controlPoints[segment];
                Vector2 a = new Vector2(first.x, first.z);
                Vector2 b = new Vector2(last.x, last.z);
                float length = Vector2.Distance(a, b);
                if (length < 0.001f)
                    continue;

                routeLength += length;
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / sampleSpacing));
                float sampledLength = length / steps;
                for (int step = 0; step < steps; step++)
                {
                    Vector2 point = Vector2.Lerp(a, b, (step + 0.5f) / steps);
                    if (TryGetNearestWaterFrame(point, water.controlPoints,
                            out _, out _, out float localWidth, out float distance))
                    {
                        float corridor = Mathf.Max(water.nominalWidth, localWidth) * 0.5f +
                            route.width * 0.5f + route.shoulderWidth + 8f;
                        if (distance <= corridor)
                        {
                            overlapLength += sampledLength;
                            currentOverlap += sampledLength;
                            longestOverlap = Mathf.Max(longestOverlap, currentOverlap);
                            continue;
                        }
                    }

                    currentOverlap = 0f;
                }
            }

            longestOverlap = Mathf.Max(longestOverlap, currentOverlap);
        }

        private static bool EndpointFollowsWater(
            YQRouteCorridorV2 route,
            YQHydrologyFeatureV2 water,
            bool atStart)
        {
            int lastIndex = route.controlPoints.Count - 1;
            int endpointIndex = atStart ? 0 : lastIndex;
            int neighborIndex = atStart ? 1 : lastIndex - 1;
            YQBlueprintPointV2 endpointRecord = route.controlPoints[endpointIndex];
            YQBlueprintPointV2 neighborRecord = route.controlPoints[neighborIndex];
            Vector2 endpoint = new Vector2(endpointRecord.x, endpointRecord.z);
            Vector2 routeTangent = new Vector2(
                neighborRecord.x - endpointRecord.x,
                neighborRecord.z - endpointRecord.z);
            if (routeTangent.sqrMagnitude < 0.0001f ||
                !TryGetNearestWaterFrame(endpoint, water.controlPoints,
                    out _, out Vector2 waterTangent, out float localWidth, out float distance))
                return false;

            float endpointRange = Mathf.Max(24f,
                Mathf.Max(water.nominalWidth, localWidth) + route.width +
                route.shoulderWidth + 8f);
            return distance <= endpointRange &&
                   Mathf.Abs(Vector2.Dot(routeTangent.normalized, waterTangent)) >= 0.82f;
        }

        private void BuildDeterministicBankRoute(
            YQRouteCorridorV2 route,
            YQHydrologyFeatureV2 water)
        {
            List<YQBlueprintPointV2> source = new List<YQBlueprintPointV2>(route.controlPoints);
            YQBlueprintPointV2 first = source[0];
            YQBlueprintPointV2 last = source[source.Count - 1];
            Vector2 routeDirection = new Vector2(last.x - first.x, last.z - first.z).normalized;
            if (routeDirection.sqrMagnitude < 0.0001f)
                return;

            float widestBank = Mathf.Max(0f, water.nominalWidth);
            for (int index = 0; index < water.controlPoints.Count; index++)
                widestBank = Mathf.Max(widestBank, water.controlPoints[index].width);
            float bankOffset = Mathf.Max(32f,
                widestBank * 0.5f + route.width * 0.5f + route.shoulderWidth + 14f);
            float side = YQSpatialBlueprintDeterminismV2.Hash01(
                context.seed + "|route_water_bank|" + route.routeId + "|" + water.hydrologyId) < 0.5f
                ? -1f
                : 1f;
            float turnLead = Mathf.Min(48f, bankOffset * 0.65f);
            List<YQBlueprintPointV2> bankRoute = new List<YQBlueprintPointV2>(source.Count + 2);
            bankRoute.Add(first);
            AddBankApproachPoint(bankRoute, first, water, side, bankOffset,
                routeDirection * turnLead, route.width);

            for (int index = 1; index < source.Count - 1; index++)
            {
                YQBlueprintPointV2 original = source[index];
                Vector2 point = new Vector2(original.x, original.z);
                if (TryGetNearestWaterFrame(point, water.controlPoints,
                        out Vector2 nearest, out Vector2 tangent, out _, out float distance) &&
                    distance <= bankOffset * 2f)
                {
                    Vector2 normal = new Vector2(-tangent.y, tangent.x);
                    Vector2 bank = nearest + normal * (side * bankOffset);
                    AddBankRoutePoint(bankRoute, bank, original.normalizedElevation, route.width);
                }
                else
                {
                    AddBankRoutePoint(bankRoute, point, original.normalizedElevation, route.width);
                }
            }

            AddBankApproachPoint(bankRoute, last, water, side, bankOffset,
                -routeDirection * turnLead, route.width);
            bankRoute.Add(last);
            if (bankRoute.Count >= 4)
            {
                // note: Keep the accepted site endpoints exact while committing only the derived bank corridor between them.
                route.controlPoints.Clear();
                route.controlPoints.AddRange(bankRoute);
            }
        }

        private void AddBankApproachPoint(
            List<YQBlueprintPointV2> points,
            YQBlueprintPointV2 anchor,
            YQHydrologyFeatureV2 water,
            float side,
            float bankOffset,
            Vector2 alongRoute,
            float width)
        {
            Vector2 anchorPosition = new Vector2(anchor.x, anchor.z);
            if (!TryGetNearestWaterFrame(anchorPosition, water.controlPoints,
                    out Vector2 nearest, out Vector2 tangent, out _, out float distance) ||
                distance > Mathf.Max(96f, bankOffset * 2f))
                return;

            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            Vector2 position = nearest + normal * (side * bankOffset) + alongRoute;
            AddBankRoutePoint(points, context.Clamp(position), anchor.normalizedElevation, width);
        }

        private void AddBankRoutePoint(
            List<YQBlueprintPointV2> points,
            Vector2 position,
            float elevation,
            float width)
        {
            Vector2 clamped = context.Clamp(position);
            if (points.Count > 0)
            {
                YQBlueprintPointV2 previous = points[points.Count - 1];
                Vector2 previousPosition = new Vector2(previous.x, previous.z);
                if ((clamped - previousPosition).sqrMagnitude < 25f)
                    return;
            }
            points.Add(YQSpatialBlueprintDeterminismV2.Point(clamped, elevation, width));
        }

        private struct WaterCrossingCandidate
        {
            public Vector2 position;
        }

        private void AddWaterCrossings(YQRouteCorridorV2 route)
        {
            for (int waterIndex = 0;
                 waterIndex < context.blueprint.hydrology.Count;
                 waterIndex++)
            {
                YQHydrologyFeatureV2 water =
                    context.blueprint.hydrology[waterIndex];
                if (water.kind != YQHydrologyKindV2.River)
                    continue;

                List<WaterCrossingCandidate> candidates =
                    FindWaterCrossingCandidates(route, water);
                if (candidates.Count == 0)
                {
                    continue;
                }

                string baseCrossingId = "crossing:" + route.routeId + ":" + water.hydrologyId;
                for (int crossingIndex = 0; crossingIndex < candidates.Count; crossingIndex++)
                {
                    WaterCrossingCandidate candidate = candidates[crossingIndex];
                    string suffix = candidates.Count == 1
                        ? string.Empty
                        : ":span:" + crossingIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    YQRouteCrossingV2 crossing = new YQRouteCrossingV2
                    {
                        crossingId = baseCrossingId + suffix,
                        hydrologyId = water.hydrologyId,
                        kind = route.routeClass == YQRouteClassV2.PrimaryRoad ||
                               water.nominalWidth > 7f
                            ? YQRouteCrossingKindV2.Bridge
                            : YQRouteCrossingKindV2.Ford,
                        x = candidate.position.x,
                        z = candidate.position.y,
                        requiredSpan = water.nominalWidth + route.shoulderWidth * 2f
                    };
                    route.crossings.Add(crossing);
                    context.AddRelationship(
                        "relationship:crossing:" + route.routeId + ":" + water.hydrologyId + suffix,
                        route.routeId,
                        water.hydrologyId,
                        YQSpatialRelationshipKindV2.Crosses,
                        0f,
                        crossing.requiredSpan,
                        crossing.kind.ToString().ToLowerInvariant());
                }
            }
        }

        private static List<WaterCrossingCandidate> FindWaterCrossingCandidates(
            YQRouteCorridorV2 route,
            YQHydrologyFeatureV2 water)
        {
            List<WaterCrossingCandidate> candidates = new List<WaterCrossingCandidate>();
            if (route?.controlPoints == null || route.controlPoints.Count < 2 ||
                water?.controlPoints == null || water.controlPoints.Count < 2)
                return candidates;

            // note: Sample the actual accepted corridor so endpoint water access and repeated entry/exit spans receive stable, separately owned crossings.
            const float sampleSpacing = 4f;
            const float maximumDryGap = 6f;
            bool inRun = false;
            float lastWetDistance = 0f;
            float bestDistanceToWater = float.PositiveInfinity;
            WaterCrossingCandidate bestCandidate = default;
            float routeDistance = 0f;

            for (int segment = 1; segment < route.controlPoints.Count; segment++)
            {
                YQBlueprintPointV2 first = route.controlPoints[segment - 1];
                YQBlueprintPointV2 last = route.controlPoints[segment];
                Vector2 a = new Vector2(first.x, first.z);
                Vector2 b = new Vector2(last.x, last.z);
                float length = Vector2.Distance(a, b);
                if (length < 0.001f)
                    continue;

                int steps = Mathf.Max(1, Mathf.CeilToInt(length / sampleSpacing));
                float sampledLength = length / steps;
                for (int step = 0; step <= steps; step++)
                {
                    if (segment > 1 && step == 0)
                        continue;
                    float t = step / (float)steps;
                    float along = routeDistance + length * t;
                    Vector2 point = Vector2.Lerp(a, b, t);
                    bool nearWater = TryGetNearestWaterFrame(point, water.controlPoints,
                        out _, out _, out float localWidth, out float distance);
                    float corridor = nearWater
                        ? Mathf.Max(water.nominalWidth, localWidth) * 0.5f +
                          route.width * 0.5f + route.shoulderWidth + 7f
                        : 0f;
                    if (nearWater && distance <= corridor)
                    {
                        if (!inRun)
                        {
                            inRun = true;
                            bestDistanceToWater = distance;
                            bestCandidate = new WaterCrossingCandidate
                            {
                                position = point
                            };
                        }
                        else if (distance < bestDistanceToWater)
                        {
                            bestDistanceToWater = distance;
                            bestCandidate = new WaterCrossingCandidate
                            {
                                position = point
                            };
                        }
                        lastWetDistance = along;
                    }
                    else if (inRun && along - lastWetDistance > maximumDryGap)
                    {
                        candidates.Add(bestCandidate);
                        inRun = false;
                        bestDistanceToWater = float.PositiveInfinity;
                    }
                }

                routeDistance += length;
            }

            if (inRun)
                candidates.Add(bestCandidate);
            return candidates;
        }

        private static bool TryGetNearestWaterFrame(
            Vector2 point,
            IReadOnlyList<YQBlueprintPointV2> waterPoints,
            out Vector2 nearest,
            out Vector2 tangent,
            out float width,
            out float distance)
        {
            nearest = Vector2.zero;
            tangent = Vector2.zero;
            width = 0f;
            distance = float.PositiveInfinity;
            if (waterPoints == null || waterPoints.Count < 2)
                return false;

            float bestSquared = float.PositiveInfinity;
            for (int index = 1; index < waterPoints.Count; index++)
            {
                YQBlueprintPointV2 first = waterPoints[index - 1];
                YQBlueprintPointV2 last = waterPoints[index];
                Vector2 a = new Vector2(first.x, first.z);
                Vector2 b = new Vector2(last.x, last.z);
                Vector2 segment = b - a;
                float t = segment.sqrMagnitude > 0.0001f
                    ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude)
                    : 0f;
                Vector2 candidate = a + segment * t;
                float squared = (point - candidate).sqrMagnitude;
                if (squared >= bestSquared)
                    continue;

                bestSquared = squared;
                nearest = candidate;
                tangent = segment.sqrMagnitude > 0.0001f ? segment.normalized : Vector2.up;
                width = Mathf.Lerp(first.width, last.width, t);
            }

            distance = Mathf.Sqrt(bestSquared);
            return !float.IsInfinity(distance);
        }

        private void OrientSitesToRoutes()
        {
            List<YQRouteCorridorV2> routes = context.blueprint.routes;
            routes.Sort(
                (left, right) => string.CompareOrdinal(
                    left.routeId,
                    right.routeId));

            foreach (YQSiteAnchorV2 site in context.sitesById.Values)
            {
                if (site.kind == YQSiteKindV2.NaturalFeature)
                    continue;

                for (int index = 0; index < routes.Count; index++)
                {
                    YQRouteCorridorV2 route = routes[index];
                    Vector2 direction;
                    if (string.Equals(
                            route.fromSiteId,
                            site.siteId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        direction = new Vector2(
                            route.controlPoints[1].x - site.x,
                            route.controlPoints[1].z - site.z);
                    }
                    else if (string.Equals(
                                 route.toSiteId,
                                 site.siteId,
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        int pointIndex = route.controlPoints.Count - 2;
                        direction = new Vector2(
                            route.controlPoints[pointIndex].x - site.x,
                            route.controlPoints[pointIndex].z - site.z);
                    }
                    else
                    {
                        continue;
                    }

                    site.preferredHeadingDegrees =
                        Mathf.Repeat(
                            Mathf.Atan2(direction.x, direction.y) *
                            Mathf.Rad2Deg,
                            360f);
                    break;
                }
            }
        }

        private void PopulateMetrics()
        {
            YQSpatialBlueprintMetricsV2 metrics =
                context.blueprint.metrics;
            metrics.regionCount = context.blueprint.regions.Count;
            metrics.terrainFieldCount =
                context.blueprint.terrainFields.Count;
            metrics.hydrologyFeatureCount =
                context.blueprint.hydrology.Count;
            metrics.siteCount = context.blueprint.sites.Count;
            metrics.routeCount = context.blueprint.routes.Count;
            metrics.relationshipCount =
                context.blueprint.relationships.Count;
            metrics.crossingCount = 0;
            metrics.connectedSettlementCount = 0;
            metrics.primaryRouteLength = 0f;
            float reservedArea = 0f;

            for (int siteIndex = 0;
                 siteIndex < context.blueprint.sites.Count;
                 siteIndex++)
            {
                YQSiteAnchorV2 site = context.blueprint.sites[siteIndex];
                if (site.kind == YQSiteKindV2.Settlement)
                    metrics.connectedSettlementCount++;
                if (site.kind != YQSiteKindV2.NaturalFeature)
                    reservedArea += Mathf.PI * site.reservedRadius *
                                    site.reservedRadius;
            }

            for (int routeIndex = 0;
                 routeIndex < context.blueprint.routes.Count;
                 routeIndex++)
            {
                YQRouteCorridorV2 route =
                    context.blueprint.routes[routeIndex];
                metrics.crossingCount += route.crossings.Count;
                if (route.routeClass == YQRouteClassV2.PrimaryRoad)
                    metrics.primaryRouteLength += RouteLength(route);
            }

            float worldArea = context.blueprint.worldSize *
                              context.blueprint.worldSize;
            metrics.reservedWorldFraction = worldArea > 0f
                ? Mathf.Clamp01(reservedArea / worldArea)
                : 0f;
        }

        private Vector2 FindSitePosition(
            YQRegionDomainV2 region,
            string siteId,
            float siteRadius,
            float minimumRadiusFraction,
            float maximumRadiusFraction,
            float minimumWaterAccess,
            bool keepEntireReserveDry)
        {
            Vector2 regionCenter = region != null
                ? new Vector2(region.centerX, region.centerZ)
                : Vector2.zero;
            float regionRadius = region != null ? region.radius : 220f;
            float baseAngle = YQSpatialBlueprintDeterminismV2.Hash01(
                context.seed + "|site_angle|" + siteId) * 360f;
            Vector2 best = regionCenter;
            float bestScore = float.MinValue;

            for (int attempt = 0; attempt < 64; attempt++)
            {
                float ring = (attempt % 8) / 7f;
                float distance = regionRadius * Mathf.Lerp(
                    minimumRadiusFraction,
                    maximumRadiusFraction,
                    ring);
                float angle = baseAngle + attempt * 137.50776f;
                Vector2 direction =
                    YQSpatialBlueprintDeterminismV2.Direction(angle);
                Vector2 candidate;
                if (minimumWaterAccess > 0f &&
                    (attempt & 1) == 0 &&
                    TryFindDryBankCandidate(
                        regionCenter,
                        siteId,
                        siteRadius,
                        minimumWaterAccess,
                        attempt,
                        out Vector2 bankCandidate))
                {
                    candidate = bankCandidate;
                }
                else
                {
                    candidate = context.Clamp(
                        regionCenter + direction * distance);
                }
                float clearance = MinimumReserveClearance(
                    candidate,
                    siteRadius);
                float waterAccess = 1f;
                float bankDistance = float.MaxValue;
                bool centerInsideWater = false;
                // note: Inland settlements carry no invented water-access score, but still measure the prepared bank geometry so their complete construction reserve stays dry.
                if (minimumWaterAccess > 0f || keepEntireReserveDry)
                {
                    EvaluateWaterAccess(
                        candidate,
                        siteRadius,
                        siteId,
                        out waterAccess,
                        out bankDistance,
                        out centerInsideWater);
                }
                float waterDeficit = Mathf.Max(
                    0f,
                    minimumWaterAccess - waterAccess);
                float requiredDryBank = keepEntireReserveDry
                    ? siteRadius
                    : minimumWaterAccess > 0f ? 2f : 0f;
                float dryBankDeficit = centerInsideWater
                    ? siteRadius
                    : Mathf.Max(0f, requiredDryBank - bankDistance);
                float regionDistance = Vector2.Distance(
                    candidate,
                    regionCenter);
                float score = Mathf.Min(clearance, 180f) -
                              waterDeficit * 1000f -
                              dryBankDeficit * 100f -
                              regionDistance * 0.015f;

                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }

                if (clearance >= 0f &&
                    waterAccess + 0.001f >= minimumWaterAccess &&
                    !centerInsideWater &&
                    bankDistance + 0.001f >= requiredDryBank)
                {
                    return candidate;
                }
            }

            float fallbackRequiredDryBank = keepEntireReserveDry
                ? siteRadius
                : minimumWaterAccess > 0f ? 2f : 0f;
            // note: Neither an inland settlement reserve nor a waterfront anchor may use an underwater fallback; explicit access can still be rejected later if its bounded target is unmet.
            if (fallbackRequiredDryBank > 0f)
            {
                EvaluateWaterAccess(
                    best,
                    siteRadius,
                    siteId,
                    out _,
                    out float bestBankDistance,
                    out bool bestCenterInsideWater);
                if (bestCenterInsideWater ||
                    bestBankDistance + 0.001f < fallbackRequiredDryBank)
                {
                    throw new InvalidOperationException(
                        "V2 water-aware site placement could not keep its anchor dry: " +
                        siteId + ".");
                }
            }

            // note: Dense semantic plans retain the highest combined clearance/water-access candidate; acceptance rejects unmet explicit requirements before a plan can be persisted as accepted.
            return best;
        }

        private bool TryFindDryBankCandidate(
            Vector2 regionCenter,
            string siteId,
            float siteRadius,
            float minimumWaterAccess,
            int attempt,
            out Vector2 candidate)
        {
            candidate = Vector2.zero;
            float accessRange = Mathf.Max(24f, siteRadius * 2.5f);
            float maximumBankDistance =
                accessRange * Mathf.Clamp01(1f - minimumWaterAccess) - 0.5f;
            float minimumBankDistance = minimumWaterAccess <= 0.5f
                ? siteRadius + 1f
                : 2f;
            if (maximumBankDistance < minimumBankDistance)
                return false;

            float bankPhase = YQSpatialBlueprintDeterminismV2.Hash01(
                context.seed + "|site_bank_distance|" + siteId + "|" + attempt);
            float desiredBankDistance = Mathf.Lerp(
                minimumBankDistance,
                maximumBankDistance,
                Mathf.Lerp(0.65f, 0.98f, bankPhase));
            float bestDistanceSquared = float.MaxValue;
            for (int waterIndex = 0;
                 waterIndex < context.blueprint.hydrology.Count;
                 waterIndex++)
            {
                YQHydrologyFeatureV2 water =
                    context.blueprint.hydrology[waterIndex];
                if (water == null || water.controlPoints == null)
                    continue;

                if (water.controlPoints.Count == 1)
                {
                    YQBlueprintPointV2 point = water.controlPoints[0];
                    Vector2 centerline = new Vector2(point.x, point.z);
                    Vector2 outward = regionCenter - centerline;
                    if (outward.sqrMagnitude < 0.001f)
                        outward = YQSpatialBlueprintDeterminismV2.Direction(
                            YQSpatialBlueprintDeterminismV2.Hash01(
                                context.seed + "|site_bank_side|" + siteId) * 360f);
                    outward.Normalize();
                    float halfWidth = Mathf.Max(
                        water.nominalWidth, point.width) * 0.5f;
                    candidate = context.Clamp(
                        centerline + outward *
                        (halfWidth + desiredBankDistance));
                    bestDistanceSquared = (candidate - regionCenter).sqrMagnitude;
                    continue;
                }

                for (int segmentIndex = 1;
                     segmentIndex < water.controlPoints.Count;
                     segmentIndex++)
                {
                    YQBlueprintPointV2 from = water.controlPoints[segmentIndex - 1];
                    YQBlueprintPointV2 to = water.controlPoints[segmentIndex];
                    float sample = YQSpatialBlueprintDeterminismV2.Hash01(
                        context.seed + "|site_bank_sample|" + siteId + "|" +
                        attempt + "|" + water.hydrologyId + "|" + segmentIndex);
                    Vector2 centerline = Vector2.Lerp(
                        new Vector2(from.x, from.z),
                        new Vector2(to.x, to.z),
                        sample);
                    Vector2 outward = regionCenter - centerline;
                    if (outward.sqrMagnitude < 0.001f)
                    {
                        Vector2 tangent = new Vector2(
                            to.x - from.x,
                            to.z - from.z).normalized;
                        outward = new Vector2(-tangent.y, tangent.x);
                        if (YQSpatialBlueprintDeterminismV2.Hash01(
                                context.seed + "|site_bank_side|" + siteId) < 0.5f)
                            outward = -outward;
                    }
                    outward.Normalize();
                    float localWidth = Mathf.Lerp(from.width, to.width, sample);
                    float halfWidth = Mathf.Max(
                        water.nominalWidth, localWidth) * 0.5f;
                    Vector2 bankCandidate = context.Clamp(
                        centerline + outward *
                        (halfWidth + desiredBankDistance));
                    float distanceSquared =
                        (bankCandidate - regionCenter).sqrMagnitude;
                    if (distanceSquared >= bestDistanceSquared)
                        continue;
                    bestDistanceSquared = distanceSquared;
                    candidate = bankCandidate;
                }
            }

            return bestDistanceSquared < float.MaxValue;
        }

        private void EvaluateWaterAccess(
            Vector2 candidate,
            float siteRadius,
            string siteId,
            out float access,
            out float bankDistance,
            out bool centerInsideWater)
        {
            // note: Select against the exact prepared bank widths that acceptance and runtime evaluate, without allowing a structure center inside water.
            if (!waterAccessQuery.TryMeasure(
                    siteId,
                    candidate.x,
                    candidate.y,
                    siteRadius,
                    out access,
                    out bankDistance,
                    out _,
                    out centerInsideWater,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "V2 site water-access selection failed: " + failure);
            }
        }

        private float MinimumReserveClearance(
            Vector2 candidate,
            float siteRadius)
        {
            float clearance = float.MaxValue;
            foreach (YQSiteAnchorV2 existing in context.sitesById.Values)
            {
                if (existing.kind == YQSiteKindV2.NaturalFeature)
                    continue;
                float separation = Vector2.Distance(
                    candidate,
                    new Vector2(existing.x, existing.z));
                float required =
                    (siteRadius + existing.reservedRadius) * 0.72f;
                clearance = Mathf.Min(clearance, separation - required);
            }

            return clearance == float.MaxValue ? 0f : clearance;
        }

        private void AddSite(YQSiteAnchorV2 site)
        {
            site.EnsureCollections();
            context.blueprint.sites.Add(site);
            context.sitesById[site.siteId] = site;
        }

        private YQRegionDomainV2 NearestRegion(Vector2 point)
        {
            YQRegionDomainV2 nearest = null;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < context.blueprint.regions.Count; index++)
            {
                YQRegionDomainV2 region = context.blueprint.regions[index];
                float distance = Vector2.SqrMagnitude(
                    point - new Vector2(region.centerX, region.centerZ));
                if (distance < nearestDistance)
                {
                    nearest = region;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private YQHydrologyFeatureV2 FindHydrology(YQHydrologyKindV2 kind)
        {
            for (int index = 0;
                 index < context.blueprint.hydrology.Count;
                 index++)
            {
                YQHydrologyFeatureV2 feature =
                    context.blueprint.hydrology[index];
                if (feature != null && feature.kind == kind)
                    return feature;
            }

            return null;
        }

        private YQSiteAnchorV2 RepresentativeSite(string regionId)
        {
            YQSiteAnchorV2 fallback = null;
            foreach (YQSiteAnchorV2 site in context.sitesById.Values)
            {
                if (!string.Equals(
                        site.parentRegionId,
                        regionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (site.kind == YQSiteKindV2.Settlement)
                    return site;
                if (fallback == null &&
                    site.kind != YQSiteKindV2.NaturalFeature)
                {
                    fallback = site;
                }
            }

            return fallback ?? context.sitesById[context.blueprint.originSiteId];
        }

        private YQSiteAnchorV2 NearestSite(
            YQSiteAnchorV2 target,
            IReadOnlyList<YQSiteAnchorV2> candidates)
        {
            YQSiteAnchorV2 nearest = null;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < candidates.Count; index++)
            {
                float distance = SiteDistance(target, candidates[index]);
                if (distance < nearestDistance - 0.001f ||
                    (Mathf.Abs(distance - nearestDistance) <= 0.001f &&
                     nearest != null &&
                     string.CompareOrdinal(
                         candidates[index].siteId,
                         nearest.siteId) < 0))
                {
                    nearest = candidates[index];
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private YQRouteCorridorV2 FindRoute(string pair)
        {
            for (int index = 0; index < context.blueprint.routes.Count; index++)
            {
                YQRouteCorridorV2 route = context.blueprint.routes[index];
                if (string.Equals(
                        PairKey(route.fromSiteId, route.toSiteId),
                        pair,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return route;
                }
            }

            return null;
        }

        private bool IsHiddenPoi(
            GeneratedPointOfInterestRecord poi,
            string siteId)
        {
            string text = ((poi.kind ?? string.Empty) + " " +
                           (poi.gameplayHook ?? string.Empty) + " " +
                           string.Join(" ", poi.tags ?? new List<string>()))
                .ToLowerInvariant();
            return ContainsAny(text, "hidden", "secret", "cache", "concealed") ||
                   YQSpatialBlueprintDeterminismV2.Hash01(
                       context.seed + "|poi_hidden|" + siteId) < 0.18f;
        }

        private static bool IsNaturalInteriorPoi(
            GeneratedPointOfInterestRecord poi)
        {
            // note: Only structured semantic fields may promote a POI to a cave/interior site; narrative text never changes physical behavior.
            if (poi == null)
                return false;
            if (ContainsAnySemanticTerm(
                    poi.kind,
                    "cave", "cavern", "mine", "tunnel", "dungeon",
                    "crypt", "tomb", "grotto", "underground"))
                return true;
            if (poi.tags == null)
                return false;
            for (int index = 0; index < poi.tags.Count; index++)
            {
                if (ContainsAnySemanticTerm(
                        poi.tags[index],
                        "cave", "cavern", "mine", "tunnel", "dungeon",
                        "crypt", "tomb", "grotto", "underground"))
                    return true;
            }
            return false;
        }

        private static bool ContainsAnySemanticTerm(
            string value,
            params string[] terms)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length == 0 || terms == null)
                return false;
            for (int index = 0; index < terms.Length; index++)
            {
                string term = terms[index];
                if (!string.IsNullOrWhiteSpace(term) &&
                    normalized == term ||
                    normalized.StartsWith(term + "_", StringComparison.Ordinal) ||
                    normalized.StartsWith(term + "-", StringComparison.Ordinal) ||
                    normalized.EndsWith("_" + term, StringComparison.Ordinal) ||
                    normalized.EndsWith("-" + term, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static YQSitePlacementModeV2 ResolvePoiPlacement(
            GeneratedPointOfInterestRecord poi)
        {
            string text = ((poi.kind ?? string.Empty) + " " +
                           (poi.gameplayHook ?? string.Empty) + " " +
                           string.Join(" ", poi.tags ?? new List<string>()))
                .ToLowerInvariant();
            if (ContainsAny(text, "water", "lake", "river", "dock", "waterfall"))
                return YQSitePlacementModeV2.WaterAdjacent;
            if (ContainsAny(text, "cave", "mine", "ruin", "cliff", "ridge"))
                return YQSitePlacementModeV2.FeatureAdjacent;
            return YQSitePlacementModeV2.RouteFrontage;
        }

        private static float ResolveSettlementWaterNeed(
            GeneratedSettlementRecord settlement)
        {
            string text = ((settlement.kind ?? string.Empty) + " " +
                           (settlement.siteRoleIntent ?? string.Empty) + " " +
                           (settlement.marketBias ?? string.Empty))
                .ToLowerInvariant();
            // note: Water access is an authored semantic constraint only for clear shoreline economies/roles; ordinary settlements remain free to use the best dry inland site.
            return ContainsWholeSemanticTerm(
                    text,
                    "port",
                    "harbor",
                    "harbour",
                    "dock",
                    "dockyard",
                    "wharf",
                    "quay",
                    "marina",
                    "waterfront",
                    "riverside",
                    "riverfront",
                    "river",
                    "lakeside",
                    "lakefront",
                    "lake",
                    "coastal",
                    "coast",
                    "seaside",
                    "shoreline",
                    "canal",
                    "fishing",
                    "fishery")
                ? 0.78f
                : 0f;
        }

        private static YQRouteClassV2 ResolveSemanticRouteClass(string value)
        {
            string text = (value ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(text, "trail", "path", "wild"))
                return YQRouteClassV2.Trail;
            if (ContainsAny(text, "service", "utility"))
                return YQRouteClassV2.ServiceRoute;
            return YQRouteClassV2.SecondaryRoad;
        }

        private static float RouteWidth(YQRouteClassV2 routeClass)
        {
            switch (routeClass)
            {
                case YQRouteClassV2.PrimaryRoad: return 7.5f;
                case YQRouteClassV2.SecondaryRoad: return 5.2f;
                case YQRouteClassV2.ServiceRoute: return 4.2f;
                default: return 2.8f;
            }
        }

        private static float RouteShoulder(YQRouteClassV2 routeClass)
        {
            switch (routeClass)
            {
                case YQRouteClassV2.PrimaryRoad: return 2.4f;
                case YQRouteClassV2.SecondaryRoad: return 1.6f;
                default: return 0.8f;
            }
        }

        private static float RouteMaximumGrade(YQRouteClassV2 routeClass)
        {
            return routeClass == YQRouteClassV2.Trail ? 18f : 10f;
        }

        private static float SiteDistance(
            YQSiteAnchorV2 left,
            YQSiteAnchorV2 right)
        {
            float x = left.x - right.x;
            float z = left.z - right.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        private static float RouteLength(YQRouteCorridorV2 route)
        {
            float length = 0f;
            for (int index = 1; index < route.controlPoints.Count; index++)
            {
                YQBlueprintPointV2 previous = route.controlPoints[index - 1];
                YQBlueprintPointV2 current = route.controlPoints[index];
                float x = current.x - previous.x;
                float z = current.z - previous.z;
                length += Mathf.Sqrt(x * x + z * z);
            }

            return length;
        }

        private static string PairKey(string left, string right)
        {
            return string.CompareOrdinal(left, right) <= 0
                ? left + "--" + right
                : right + "--" + left;
        }

        private static void AddSemanticTags(
            IReadOnlyList<string> source,
            string prefix,
            List<string> destination)
        {
            if (source == null)
                return;

            for (int index = 0; index < source.Count; index++)
            {
                string value = (source[index] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                string tagged = prefix + value;
                if (!destination.Contains(tagged))
                    destination.Add(tagged);
            }
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            for (int index = 0; index < tokens.Length; index++)
            {
                if (value.Contains(tokens[index]))
                    return true;
            }

            return false;
        }

        private static bool ContainsWholeSemanticTerm(
            string value,
            params string[] terms)
        {
            // note: Word boundaries prevent unrelated cross-genre terms such as "airport" from accidentally turning an inland settlement into a seaport.
            for (int termIndex = 0; termIndex < terms.Length; termIndex++)
            {
                string term = terms[termIndex];
                int searchIndex = 0;
                while (searchIndex < value.Length)
                {
                    int matchIndex = value.IndexOf(
                        term,
                        searchIndex,
                        StringComparison.Ordinal);
                    if (matchIndex < 0)
                        break;

                    int afterIndex = matchIndex + term.Length;
                    bool startsAtBoundary = matchIndex == 0 ||
                                            !char.IsLetterOrDigit(value[matchIndex - 1]);
                    bool endsAtBoundary = afterIndex >= value.Length ||
                                          !char.IsLetterOrDigit(value[afterIndex]);
                    if (startsAtBoundary && endsAtBoundary)
                        return true;

                    searchIndex = matchIndex + 1;
                }
            }

            return false;
        }
    }
}
