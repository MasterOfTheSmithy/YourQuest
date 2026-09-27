using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Converts accepted semantic world data into one deterministic, persisted spatial plan.
/// Terrain, routes, runtime sites, population, and quests consume this plan instead of
/// inventing unrelated coordinates.
/// </summary>
public static class YQGeneratedWorldSpatialPlanner
{
    public const string GenerationVersion = "spatial_world_plan_v1_causal";

    private const float WorldSize = 1024f;
    private const float WorldHalfExtent = 422f;
    private const float OriginReserve = 92f;
    private const int CandidateCount = 72;
    private const int RouteGridSize = 33;

    public static GeneratedSpatialWorldPlanRecord EnsureSpatialPlan(
        GeneratedWorldPlanRecord plan)
    {
        if (plan == null)
            return new GeneratedSpatialWorldPlanRecord();

        plan.EnsureCollections();
        string fingerprint = BuildSemanticFingerprint(plan);
        GeneratedSpatialWorldPlanRecord current = plan.spatialPlan;

        if (current != null &&
            string.Equals(current.generationVersion, GenerationVersion, StringComparison.Ordinal) &&
            string.Equals(current.worldSeed, SafeSeed(plan), StringComparison.Ordinal) &&
            string.Equals(current.semanticFingerprint, fingerprint, StringComparison.Ordinal) &&
            HasCompleteLocationAuthority(plan, current))
        {
            current.EnsureCollections();
            return current;
        }

        // note: A version or semantic change rebuilds spatial authority once; ordinary loads reuse the persisted result exactly.
        plan.spatialPlan = Build(plan, fingerprint);
        return plan.spatialPlan;
    }

    public static GeneratedSpatialWorldPlanRecord GetSpatialPlan(
        GeneratedWorldPlanRecord plan)
    {
        if (plan == null)
            return new GeneratedSpatialWorldPlanRecord();

        plan.EnsureCollections();
        GeneratedSpatialWorldPlanRecord current = plan.spatialPlan;
        if (current != null &&
            string.Equals(current.generationVersion, GenerationVersion, StringComparison.Ordinal) &&
            string.Equals(current.worldSeed, SafeSeed(plan), StringComparison.Ordinal) &&
            HasCompleteLocationAuthority(plan, current))
        {
            // note: Hot runtime consumers reuse accepted spatial data without rebuilding semantic fingerprint strings during loading or streaming.
            return current;
        }

        return EnsureSpatialPlan(plan);
    }

    public static bool TryGetLocation(
        GeneratedWorldPlanRecord plan,
        string locationId,
        out GeneratedSpatialLocationRecord location)
    {
        location = null;
        if (plan == null || string.IsNullOrWhiteSpace(locationId))
            return false;

        GeneratedSpatialWorldPlanRecord spatial = GetSpatialPlan(plan);
        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord candidate = spatial.locations[i];
            if (candidate != null &&
                string.Equals(candidate.locationId, locationId, StringComparison.OrdinalIgnoreCase))
            {
                location = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetRegion(
        GeneratedWorldPlanRecord plan,
        string regionId,
        out GeneratedSpatialRegionRecord region)
    {
        region = null;
        if (plan == null || string.IsNullOrWhiteSpace(regionId))
            return false;

        GeneratedSpatialWorldPlanRecord spatial = GetSpatialPlan(plan);
        for (int i = 0; i < spatial.regions.Count; i++)
        {
            GeneratedSpatialRegionRecord candidate = spatial.regions[i];
            if (candidate != null &&
                string.Equals(candidate.regionId, regionId, StringComparison.OrdinalIgnoreCase))
            {
                region = candidate;
                return true;
            }
        }

        return false;
    }

    private static GeneratedSpatialWorldPlanRecord Build(
        GeneratedWorldPlanRecord plan,
        string fingerprint)
    {
        string seed = SafeSeed(plan);
        string theme = ResolveStructuralTheme(plan);
        GeneratedSpatialWorldPlanRecord spatial = new GeneratedSpatialWorldPlanRecord
        {
            schemaVersion = "spatial_world_plan_v1",
            generationVersion = GenerationVersion,
            worldSeed = seed,
            semanticFingerprint = fingerprint,
            structuralTheme = theme,
            worldSize = WorldSize
        };

        // note: Geological intent exists before any location candidate is scored.
        BuildMacroFeatures(spatial, seed, theme);
        BuildRegions(plan, spatial, seed, theme);

        spatial.locations.Add(new GeneratedSpatialLocationRecord
        {
            locationId = "origin_vey",
            parentRegionId = plan.regions.Count > 0 ? plan.regions[0].regionId : string.Empty,
            locationKind = "origin",
            structuralArchetype = "curated_origin_courtyard",
            worldX = 0f,
            worldZ = 0f,
            footprintRadius = OriginReserve,
            entranceHeadingDegrees = 0f,
            terrainSuitability = 1f,
            routeAccess = 1f,
            finalScore = 1f,
            questEligible = true,
            requiresTerrainAdaptation = true,
            tags = new List<string> { "safe_spawn", "world_origin", "quest_anchor" }
        });

        BuildSettlements(plan, spatial, seed, theme);
        BuildHostileSites(plan, spatial, seed, theme);
        BuildPointsOfInterest(plan, spatial, seed, theme);
        BuildTravelNetwork(spatial, seed, theme);
        ValidateAndRepair(plan, spatial);
        PopulateMetrics(plan, spatial);
        return spatial;
    }

    private static void BuildMacroFeatures(
        GeneratedSpatialWorldPlanRecord spatial,
        string seed,
        string theme)
    {
        float ridgeHeading = Mathf.Lerp(18f, 162f, Hash01(seed + "|ridge_heading"));
        Vector2 ridgeCenter = HashPoint(seed + "|ridge_center", 120f);
        spatial.macroFeatures.Add(CreateFeature(
            "macro_ridge", string.Empty, "mountain_ridge", ridgeCenter,
            new Vector2(330f, theme == "high_fantasy" ? 82f : 58f), ridgeHeading,
            theme == "post_apocalypse" ? 0.68f : 0.9f));

        // note: The valley crosses the principal ridge and becomes the preferred inter-region travel corridor.
        spatial.macroFeatures.Add(CreateFeature(
            "macro_valley", string.Empty, "valley", -ridgeCenter * 0.35f,
            new Vector2(430f, 70f), ridgeHeading + 86f, 0.78f));

        Vector2 basinCenter = HashPoint(seed + "|primary_basin", 250f);
        spatial.macroFeatures.Add(CreateFeature(
            "macro_basin", string.Empty, "lake_basin", basinCenter,
            new Vector2(76f, 54f), Hash01(seed + "|basin_heading") * 180f, 0.72f));

        float beltStrength = theme == "noir_city" || theme == "cyberpunk" ? 0.25f : 0.82f;
        spatial.macroFeatures.Add(CreateFeature(
            "macro_forest_belt", string.Empty, "forest_belt", -basinCenter * 0.6f,
            new Vector2(310f, 135f), ridgeHeading + 25f, beltStrength));
    }

    private static GeneratedSpatialFeatureRecord CreateFeature(
        string id,
        string regionId,
        string kind,
        Vector2 center,
        Vector2 radii,
        float heading,
        float strength)
    {
        return new GeneratedSpatialFeatureRecord
        {
            featureId = id,
            parentRegionId = regionId,
            featureKind = kind,
            centerX = center.x,
            centerZ = center.y,
            radiusX = radii.x,
            radiusZ = radii.y,
            headingDegrees = heading,
            strength = strength,
            waterLevel = kind == "lake_basin" ? 0.112f : 0f
        };
    }

    private static void BuildRegions(
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialWorldPlanRecord spatial,
        string seed,
        string theme)
    {
        int count = Mathf.Max(1, plan.regions.Count);
        for (int i = 0; i < count; i++)
        {
            GeneratedRegionRecord semantic = plan.regions.Count > 0 ? plan.regions[i] : null;
            float angle = (360f / count) * i + HashSigned(seed + "|region_angle|" + i) * 22f;
            float radius = i == 0 ? 118f : Mathf.Lerp(190f, 340f, Hash01(seed + "|region_radius|" + i));
            Vector2 center = Rotate(Vector2.up * radius, angle);
            string corpus = RegionCorpus(semantic);

            GeneratedSpatialRegionRecord region = new GeneratedSpatialRegionRecord
            {
                regionId = semantic != null ? semantic.regionId : "region_0",
                centerX = center.x,
                centerZ = center.y,
                radius = Mathf.Clamp(285f - count * 9f, 155f, 245f),
                elevationBias = ContainsAny(corpus, "mountain", "alpine", "highland", "volcan") ? 0.7f : 0.32f,
                moisture = ContainsAny(corpus, "marsh", "wet", "river", "forest", "rain") ? 0.78f : 0.42f,
                ruggedness = ContainsAny(corpus, "mountain", "cliff", "ruin", "volcan") ? 0.82f : 0.38f,
                civilizationDensity = ResolveCivilizationDensity(semantic, theme),
                danger = semantic != null ? Mathf.Clamp01(semantic.dangerTier / 10f) : 0.25f,
                biome = ResolveBiome(corpus, theme),
                terrainArchetype = ResolveTerrainArchetype(corpus, theme),
                transitionProfile = "broad_blended_border"
            };

            region.tags.Add(theme);
            if (semantic != null && semantic.biomeTags != null)
                region.tags.AddRange(semantic.biomeTags);
            spatial.regions.Add(region);
        }
    }

    private static void BuildSettlements(
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialWorldPlanRecord spatial,
        string seed,
        string theme)
    {
        for (int i = 0; i < plan.settlements.Count; i++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[i];
            GeneratedSpatialRegionRecord region = FindSpatialRegion(spatial, settlement.regionId);
            float footprint = Mathf.Lerp(42f, 86f, Mathf.InverseLerp(4f, 220f, settlement.approxPopulation));
            Vector2 best = Vector2.zero;
            CandidateScore bestScore = new CandidateScore { total = float.NegativeInfinity };

            for (int candidateIndex = 0; candidateIndex < CandidateCount; candidateIndex++)
            {
                Vector2 candidate;
                if (i == 0)
                {
                    float angle = HashSigned(seed + "|starter_angle|" + candidateIndex) * 28f;
                    float distance = Mathf.Lerp(132f, 178f, Hash01(seed + "|starter_distance|" + candidateIndex));
                    candidate = Rotate(Vector2.up * distance, angle);
                }
                else
                {
                    candidate = CandidateAroundRegion(region, seed + "|settlement|" + settlement.settlementId, candidateIndex);
                    // note: Keep accepted V2 settlement anchors outside the same origin/foundation separation envelope enforced during terrain prepass.
                    float minimumOriginDistance = OriginReserve + footprint + 12f;
                    if (candidate.magnitude < minimumOriginDistance)
                        continue;
                }

                CandidateScore score = ScoreSettlementCandidate(spatial, candidate, footprint, settlement, region, i);
                spatial.metrics.candidateCount++;
                if (score.total > bestScore.total)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            GeneratedSpatialLocationRecord location = new GeneratedSpatialLocationRecord
            {
                locationId = settlement.settlementId,
                parentRegionId = settlement.regionId,
                locationKind = "settlement",
                structuralArchetype = ResolveSettlementArchetype(settlement, theme),
                worldX = best.x,
                worldZ = best.y,
                footprintRadius = footprint,
                entranceHeadingDegrees = HeadingToward(best, Vector2.zero),
                terrainSuitability = bestScore.terrain,
                waterAccess = bestScore.water,
                routeAccess = bestScore.route,
                defensibility = bestScore.defense,
                finalScore = bestScore.total,
                questEligible = true,
                requiresTerrainAdaptation = bestScore.terrain < 0.72f
            };
            location.tags.Add(settlement.kind ?? "settlement");
            location.tags.Add(theme);
            spatial.locations.Add(location);
        }
    }

    private static void BuildHostileSites(
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialWorldPlanRecord spatial,
        string seed,
        string theme)
    {
        for (int i = 0; i < plan.encampments.Count; i++)
        {
            GeneratedEncampmentRecord camp = plan.encampments[i];
            GeneratedSpatialRegionRecord region = FindSpatialRegion(spatial, camp.regionId);
            float footprint = Mathf.Lerp(28f, 58f, Mathf.InverseLerp(1f, 12f, camp.threatTier));
            Vector2 best = Vector2.zero;
            float bestScore = float.NegativeInfinity;

            for (int candidateIndex = 0; candidateIndex < CandidateCount; candidateIndex++)
            {
                Vector2 candidate = CandidateAroundRegion(region, seed + "|hostile|" + camp.encampmentId, candidateIndex);
                float slope = EstimateSlope(spatial, candidate);
                float settlementDistance = DistanceToKind(spatial, candidate, "settlement");
                float corridor = 1f - Mathf.Clamp01(Mathf.Abs(settlementDistance - 135f) / 150f);
                float score = (1f - slope) * 2.2f + corridor * 1.7f +
                              EdgeSafety(candidate, footprint) - WaterPenalty(spatial, candidate) * 1.4f;
                spatial.metrics.candidateCount++;
                if (settlementDistance < 88f || score <= bestScore)
                    continue;
                best = candidate;
                bestScore = score;
            }

            spatial.locations.Add(new GeneratedSpatialLocationRecord
            {
                locationId = camp.encampmentId,
                parentRegionId = camp.regionId,
                locationKind = "hostile_site",
                structuralArchetype = ResolveHostileArchetype(camp, theme),
                worldX = best.x,
                worldZ = best.y,
                footprintRadius = footprint,
                entranceHeadingDegrees = HeadingToward(best, NearestLocationPosition(spatial, best, "settlement")),
                terrainSuitability = 1f - EstimateSlope(spatial, best),
                routeAccess = Mathf.Clamp01(1f - DistanceToKind(spatial, best, "settlement") / 310f),
                defensibility = Mathf.Clamp01(EstimateSlope(spatial, best) * 2f + 0.35f),
                finalScore = bestScore,
                questEligible = true,
                requiresTerrainAdaptation = EstimateSlope(spatial, best) > 0.22f,
                tags = new List<string> { camp.kind ?? "hostile_site", "road_pressure", theme }
            });
        }
    }

    private static void BuildPointsOfInterest(
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialWorldPlanRecord spatial,
        string seed,
        string theme)
    {
        for (int i = 0; i < plan.pointsOfInterest.Count; i++)
        {
            GeneratedPointOfInterestRecord poi = plan.pointsOfInterest[i];
            GeneratedSpatialRegionRecord region = FindSpatialRegion(spatial, poi.regionId);
            Vector2 best = CandidateAroundRegion(region, seed + "|poi|" + poi.poiId, i * 7 + 3);
            string corpus = ((poi.kind ?? string.Empty) + " " + (poi.gameplayHook ?? string.Empty) + " " + (poi.lore ?? string.Empty)).ToLowerInvariant();

            // note: Contextual POIs bias toward the geology or travel condition that explains their existence.
            for (int c = 0; c < CandidateCount / 2; c++)
            {
                Vector2 candidate = CandidateAroundRegion(region, seed + "|poi|" + poi.poiId, c);
                float score = ScorePoiCandidate(spatial, candidate, corpus);
                if (score > ScorePoiCandidate(spatial, best, corpus))
                    best = candidate;
            }

            spatial.locations.Add(new GeneratedSpatialLocationRecord
            {
                locationId = poi.poiId,
                parentRegionId = poi.regionId,
                locationKind = "point_of_interest",
                structuralArchetype = poi.kind,
                worldX = best.x,
                worldZ = best.y,
                footprintRadius = 28f,
                entranceHeadingDegrees = HeadingToward(best, NearestLocationPosition(spatial, best, "settlement")),
                terrainSuitability = 1f - EstimateSlope(spatial, best),
                waterAccess = 1f - WaterPenalty(spatial, best),
                routeAccess = Mathf.Clamp01(1f - DistanceToKind(spatial, best, "settlement") / 360f),
                finalScore = ScorePoiCandidate(spatial, best, corpus),
                questEligible = true,
                requiresTerrainAdaptation = EstimateSlope(spatial, best) > 0.3f,
                tags = new List<string> { poi.kind ?? "landmark", theme }
            });
        }
    }

    private static CandidateScore ScoreSettlementCandidate(
        GeneratedSpatialWorldPlanRecord spatial,
        Vector2 candidate,
        float footprint,
        GeneratedSettlementRecord settlement,
        GeneratedSpatialRegionRecord region,
        int settlementIndex)
    {
        float slope = EstimateSlope(spatial, candidate);
        float terrain = Mathf.Clamp01(1f - slope * 2.8f);
        float water = Mathf.Clamp01(1f - Mathf.Abs(DistanceToWater(spatial, candidate) - 70f) / 190f);
        float route = Mathf.Clamp01(1f - DistanceToValley(spatial, candidate) / 220f);
        float defense = Mathf.Clamp01(EvaluateElevation(spatial, candidate) * 0.8f + slope * 0.7f);
        float spacing = Mathf.Clamp01((DistanceToKind(spatial, candidate, "settlement") - footprint) / 190f);
        float origin = settlementIndex == 0
            ? Mathf.Clamp01(1f - Mathf.Abs(candidate.magnitude - 150f) / 80f)
            : Mathf.Clamp01((candidate.magnitude - OriginReserve) / 170f);
        float regional = region == null
            ? 0.5f
            : Mathf.Clamp01(1f - Vector2.Distance(candidate, new Vector2(region.centerX, region.centerZ)) / Mathf.Max(1f, region.radius));
        float edge = EdgeSafety(candidate, footprint);
        float overlapPenalty = OverlapPenalty(spatial, candidate, footprint);
        float waterPenalty = WaterPenalty(spatial, candidate);
        float total = terrain * 2.6f + water * 0.85f + route * 1.35f + defense * 0.45f +
                      spacing * 1.6f + origin * 1.4f + regional + edge * 1.2f -
                      overlapPenalty * 4f - waterPenalty * 2.5f;

        return new CandidateScore
        {
            terrain = terrain,
            water = water,
            route = route,
            defense = defense,
            total = total
        };
    }

    private static float ScorePoiCandidate(
        GeneratedSpatialWorldPlanRecord spatial,
        Vector2 candidate,
        string corpus)
    {
        float slope = EstimateSlope(spatial, candidate);
        float elevation = EvaluateElevation(spatial, candidate);
        float waterDistance = DistanceToWater(spatial, candidate);
        float score = EdgeSafety(candidate, 28f) + (1f - OverlapPenalty(spatial, candidate, 28f)) * 1.5f;
        if (ContainsAny(corpus, "mine", "cave", "mountain", "fortress"))
            score += elevation * 1.8f + slope;
        else if (ContainsAny(corpus, "dock", "river", "lake", "marsh"))
            score += Mathf.Clamp01(1f - waterDistance / 130f) * 2f;
        else if (ContainsAny(corpus, "bandit", "checkpoint", "ambush"))
            score += Mathf.Clamp01(1f - DistanceToKind(spatial, candidate, "settlement") / 280f) * 1.5f;
        else
            score += (1f - slope) + Mathf.Clamp01(candidate.magnitude / WorldHalfExtent);
        return score - WaterPenalty(spatial, candidate) * 2f;
    }

    private static void BuildTravelNetwork(
        GeneratedSpatialWorldPlanRecord spatial,
        string seed,
        string theme)
    {
        List<GeneratedSpatialLocationRecord> settlements = LocationsOfKind(spatial, "settlement");
        GeneratedSpatialLocationRecord origin = FindLocation(spatial, "origin_vey");
        List<GeneratedSpatialLocationRecord> connected = new List<GeneratedSpatialLocationRecord>();
        if (origin != null)
            connected.Add(origin);

        // note: A deterministic minimum-spanning expansion guarantees every settlement joins the lived travel graph.
        while (settlements.Count > 0 && connected.Count > 0)
        {
            GeneratedSpatialLocationRecord bestFrom = null;
            GeneratedSpatialLocationRecord bestTo = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < connected.Count; i++)
            {
                for (int j = 0; j < settlements.Count; j++)
                {
                    float distance = Vector2.Distance(Position(connected[i]), Position(settlements[j]));
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestFrom = connected[i];
                        bestTo = settlements[j];
                    }
                }
            }

            AddCostAwareRoute(spatial, bestFrom, bestTo, "major_road", theme);
            connected.Add(bestTo);
            settlements.Remove(bestTo);
        }

        // note: Hostile sites and POIs receive discoverable spur routes without becoming equal nodes in the kingdom-road hierarchy.
        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord location = spatial.locations[i];
            if (location == null ||
                (location.locationKind != "hostile_site" && location.locationKind != "point_of_interest"))
            {
                continue;
            }

            GeneratedSpatialLocationRecord nearest = NearestLocation(spatial, Position(location), "settlement", "origin");
            if (nearest != null)
                AddCostAwareRoute(spatial, nearest, location, "minor_trail", theme);
        }
    }

    private static void AddCostAwareRoute(
        GeneratedSpatialWorldPlanRecord spatial,
        GeneratedSpatialLocationRecord from,
        GeneratedSpatialLocationRecord to,
        string routeKind,
        string theme)
    {
        if (from == null || to == null)
            return;

        List<GeneratedSpatialPointRecord> points = FindGridRoute(spatial, Position(from), Position(to), routeKind);
        float length = 0f;
        bool crossesWater = false;
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = new Vector2(points[i - 1].x, points[i - 1].z);
            Vector2 b = new Vector2(points[i].x, points[i].z);
            length += Vector2.Distance(a, b);
            crossesWater |= WaterPenalty(spatial, b) > 0.5f;
        }

        string routeId = "spatial_route_" + StableHex(from.locationId + "|" + to.locationId);
        spatial.routes.Add(new GeneratedSpatialRouteRecord
        {
            routeId = routeId,
            fromLocationId = from.locationId,
            toLocationId = to.locationId,
            parentRegionId = !string.IsNullOrWhiteSpace(to.parentRegionId) ? to.parentRegionId : from.parentRegionId,
            routeKind = theme == "cyberpunk" && routeKind == "major_road" ? "transit_corridor" : routeKind,
            estimatedCost = points.Count > 0 ? points[points.Count - 1].cost : length,
            estimatedLength = length,
            requiresBridge = crossesWater,
            waypoints = points,
            tags = new List<string> { "cost_aware", theme }
        });
    }

    private static List<GeneratedSpatialPointRecord> FindGridRoute(
        GeneratedSpatialWorldPlanRecord spatial,
        Vector2 start,
        Vector2 end,
        string routeKind)
    {
        int count = RouteGridSize * RouteGridSize;
        float[] distances = new float[count];
        int[] previous = new int[count];
        bool[] closed = new bool[count];
        for (int i = 0; i < count; i++)
        {
            distances[i] = float.PositiveInfinity;
            previous[i] = -1;
        }

        int startIndex = GridIndex(start);
        int endIndex = GridIndex(end);
        distances[startIndex] = 0f;
        MinHeap open = new MinHeap(count);
        open.Push(startIndex, 0f);

        while (open.Count > 0)
        {
            int current = open.Pop();
            if (closed[current])
                continue;
            closed[current] = true;
            if (current == endIndex)
                break;

            int x = current % RouteGridSize;
            int z = current / RouteGridSize;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx == 0 && dz == 0) || Mathf.Abs(dx) + Mathf.Abs(dz) > 2)
                        continue;
                    int nx = x + dx;
                    int nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= RouteGridSize || nz >= RouteGridSize)
                        continue;
                    int next = nz * RouteGridSize + nx;
                    if (closed[next])
                        continue;

                    Vector2 a = GridPosition(current);
                    Vector2 b = GridPosition(next);
                    float slope = Mathf.Abs(EvaluateElevation(spatial, b) - EvaluateElevation(spatial, a));
                    float water = WaterPenalty(spatial, b);
                    float valley = DistanceToValley(spatial, b) / WorldSize;
                    float step = Vector2.Distance(a, b) *
                                 (1f + slope * 12f + water * (routeKind == "major_road" ? 7f : 3f) + valley * 0.8f);
                    float nextDistance = distances[current] + step;
                    if (nextDistance >= distances[next])
                        continue;
                    distances[next] = nextDistance;
                    previous[next] = current;
                    float heuristic = Vector2.Distance(b, end);
                    open.Push(next, nextDistance + heuristic);
                }
            }
        }

        List<int> indices = new List<int>();
        int cursor = endIndex;
        indices.Add(cursor);
        while (cursor != startIndex && cursor >= 0)
        {
            cursor = previous[cursor];
            if (cursor >= 0)
                indices.Add(cursor);
            if (indices.Count > count)
                break;
        }
        indices.Reverse();

        List<GeneratedSpatialPointRecord> result = new List<GeneratedSpatialPointRecord>();
        result.Add(new GeneratedSpatialPointRecord { x = start.x, z = start.y, cost = 0f });
        for (int i = 1; i < indices.Count - 1; i += 2)
        {
            Vector2 point = GridPosition(indices[i]);
            result.Add(new GeneratedSpatialPointRecord { x = point.x, z = point.y, cost = distances[indices[i]] });
        }
        result.Add(new GeneratedSpatialPointRecord { x = end.x, z = end.y, cost = distances[endIndex] });
        return result;
    }

    private static void ValidateAndRepair(
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialWorldPlanRecord spatial)
    {
        int repaired = 0;
        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord location = spatial.locations[i];
            if (location == null || location.locationKind == "origin")
                continue;

            Vector2 position = Position(location);
            float allowed = WorldHalfExtent - location.footprintRadius;
            Vector2 clamped = new Vector2(
                Mathf.Clamp(position.x, -allowed, allowed),
                Mathf.Clamp(position.y, -allowed, allowed));
            if (clamped != position)
            {
                location.worldX = clamped.x;
                location.worldZ = clamped.y;
                repaired++;
            }

            if (WaterPenalty(spatial, clamped) > 0.82f && location.locationKind != "point_of_interest")
            {
                Vector2 away = (clamped - NearestWaterCenter(spatial, clamped)).normalized;
                clamped += away * 68f;
                location.worldX = Mathf.Clamp(clamped.x, -allowed, allowed);
                location.worldZ = Mathf.Clamp(clamped.y, -allowed, allowed);
                location.requiresTerrainAdaptation = true;
                repaired++;
            }
        }
        spatial.metrics.repairedLocationCount = repaired;

        HashSet<string> reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "origin_vey" };
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < spatial.routes.Count; i++)
            {
                GeneratedSpatialRouteRecord route = spatial.routes[i];
                if (route == null)
                    continue;
                if (reachable.Contains(route.fromLocationId) && reachable.Add(route.toLocationId))
                    changed = true;
                if (reachable.Contains(route.toLocationId) && reachable.Add(route.fromLocationId))
                    changed = true;
            }
        } while (changed);

        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord location = spatial.locations[i];
            if (location != null && location.questEligible && !reachable.Contains(location.locationId))
                spatial.metrics.unreachableLocationCount++;
        }
    }

    private static void PopulateMetrics(
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialWorldPlanRecord spatial)
    {
        spatial.metrics.regionCount = spatial.regions.Count;
        spatial.metrics.settlementCount = plan.settlements.Count;
        spatial.metrics.hostileSiteCount = plan.encampments.Count;
        spatial.metrics.routeCount = spatial.routes.Count;
        float separation = 0f;
        int pairs = 0;
        List<GeneratedSpatialLocationRecord> settlements = LocationsOfKind(spatial, "settlement");
        for (int i = 0; i < settlements.Count; i++)
        {
            for (int j = i + 1; j < settlements.Count; j++)
            {
                separation += Vector2.Distance(Position(settlements[i]), Position(settlements[j]));
                pairs++;
            }
        }
        spatial.metrics.averageSettlementSeparation = pairs > 0 ? separation / pairs : 0f;
        for (int i = 0; i < spatial.routes.Count; i++)
            spatial.metrics.totalRouteLength += spatial.routes[i]?.estimatedLength ?? 0f;
        spatial.metrics.estimatedTraversableFraction = EstimateTraversableFraction(spatial);
        spatial.metrics.validationSummary = spatial.metrics.unreachableLocationCount == 0
            ? "All quest-eligible planned locations join the origin travel graph."
            : spatial.metrics.unreachableLocationCount + " planned locations are outside the origin travel graph.";
    }

    private static float EstimateTraversableFraction(GeneratedSpatialWorldPlanRecord spatial)
    {
        int traversable = 0;
        const int samples = 24;
        for (int z = 0; z < samples; z++)
        {
            for (int x = 0; x < samples; x++)
            {
                Vector2 p = new Vector2(
                    Mathf.Lerp(-WorldHalfExtent, WorldHalfExtent, x / (samples - 1f)),
                    Mathf.Lerp(-WorldHalfExtent, WorldHalfExtent, z / (samples - 1f)));
                if (EstimateSlope(spatial, p) < 0.34f && WaterPenalty(spatial, p) < 0.75f)
                    traversable++;
            }
        }
        return traversable / (float)(samples * samples);
    }

    public static float EvaluateElevation(
        GeneratedSpatialWorldPlanRecord spatial,
        Vector2 position)
    {
        float broad = 0.34f + ValueNoise(position.x * 0.0022f, position.y * 0.0022f, spatial.worldSeed) * 0.16f;
        for (int i = 0; i < spatial.macroFeatures.Count; i++)
        {
            GeneratedSpatialFeatureRecord feature = spatial.macroFeatures[i];
            if (feature == null)
                continue;
            float mask = FeatureMask(feature, position);
            if (feature.featureKind == "mountain_ridge")
                broad += mask * feature.strength * 0.34f;
            else if (feature.featureKind == "valley")
                broad -= mask * feature.strength * 0.18f;
            else if (feature.featureKind == "lake_basin")
                broad -= mask * feature.strength * 0.24f;
        }
        return Mathf.Clamp01(broad);
    }

    private static float EstimateSlope(GeneratedSpatialWorldPlanRecord spatial, Vector2 point)
    {
        const float delta = 10f;
        float x = Mathf.Abs(EvaluateElevation(spatial, point + Vector2.right * delta) -
                            EvaluateElevation(spatial, point - Vector2.right * delta));
        float z = Mathf.Abs(EvaluateElevation(spatial, point + Vector2.up * delta) -
                            EvaluateElevation(spatial, point - Vector2.up * delta));
        return Mathf.Clamp01((x + z) * 5f);
    }

    private static float FeatureMask(GeneratedSpatialFeatureRecord feature, Vector2 point)
    {
        Vector2 local = Rotate(point - new Vector2(feature.centerX, feature.centerZ), -feature.headingDegrees);
        float nx = local.x / Mathf.Max(1f, feature.radiusX);
        float nz = local.y / Mathf.Max(1f, feature.radiusZ);
        return Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + nz * nz));
    }

    private static float DistanceToWater(GeneratedSpatialWorldPlanRecord spatial, Vector2 point)
    {
        float result = float.PositiveInfinity;
        for (int i = 0; i < spatial.macroFeatures.Count; i++)
        {
            GeneratedSpatialFeatureRecord feature = spatial.macroFeatures[i];
            if (feature == null || feature.featureKind != "lake_basin")
                continue;
            float distance = Vector2.Distance(point, new Vector2(feature.centerX, feature.centerZ)) -
                             Mathf.Min(feature.radiusX, feature.radiusZ);
            result = Mathf.Min(result, Mathf.Abs(distance));
        }
        return float.IsPositiveInfinity(result) ? WorldSize : result;
    }

    private static float WaterPenalty(GeneratedSpatialWorldPlanRecord spatial, Vector2 point)
    {
        for (int i = 0; i < spatial.macroFeatures.Count; i++)
        {
            GeneratedSpatialFeatureRecord feature = spatial.macroFeatures[i];
            if (feature != null && feature.featureKind == "lake_basin")
                return FeatureMask(feature, point) > 0.35f ? FeatureMask(feature, point) : 0f;
        }
        return 0f;
    }

    private static Vector2 NearestWaterCenter(GeneratedSpatialWorldPlanRecord spatial, Vector2 point)
    {
        Vector2 result = Vector2.zero;
        float best = float.PositiveInfinity;
        for (int i = 0; i < spatial.macroFeatures.Count; i++)
        {
            GeneratedSpatialFeatureRecord feature = spatial.macroFeatures[i];
            if (feature == null || feature.featureKind != "lake_basin")
                continue;
            Vector2 center = new Vector2(feature.centerX, feature.centerZ);
            float distance = Vector2.SqrMagnitude(point - center);
            if (distance < best)
            {
                best = distance;
                result = center;
            }
        }
        return result;
    }

    private static float DistanceToValley(GeneratedSpatialWorldPlanRecord spatial, Vector2 point)
    {
        for (int i = 0; i < spatial.macroFeatures.Count; i++)
        {
            GeneratedSpatialFeatureRecord feature = spatial.macroFeatures[i];
            if (feature == null || feature.featureKind != "valley")
                continue;
            Vector2 local = Rotate(point - new Vector2(feature.centerX, feature.centerZ), -feature.headingDegrees);
            return Mathf.Abs(local.y);
        }
        return WorldSize;
    }

    private static Vector2 CandidateAroundRegion(
        GeneratedSpatialRegionRecord region,
        string seed,
        int index)
    {
        Vector2 center = region == null ? Vector2.zero : new Vector2(region.centerX, region.centerZ);
        float radius = region == null ? 300f : region.radius;
        float angle = Hash01(seed + "|angle|" + index) * 360f;
        float distance = Mathf.Sqrt(Hash01(seed + "|radius|" + index)) * radius * 0.92f;
        Vector2 candidate = center + Rotate(Vector2.up * distance, angle);
        return new Vector2(
            Mathf.Clamp(candidate.x, -WorldHalfExtent, WorldHalfExtent),
            Mathf.Clamp(candidate.y, -WorldHalfExtent, WorldHalfExtent));
    }

    private static float OverlapPenalty(
        GeneratedSpatialWorldPlanRecord spatial,
        Vector2 candidate,
        float radius)
    {
        float penalty = 0f;
        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord other = spatial.locations[i];
            if (other == null)
                continue;
            float required = radius + other.footprintRadius + 18f;
            float distance = Vector2.Distance(candidate, Position(other));
            penalty = Mathf.Max(penalty, Mathf.Clamp01((required - distance) / Mathf.Max(1f, required)));
        }
        return penalty;
    }

    private static float EdgeSafety(Vector2 point, float radius)
    {
        float remaining = WorldHalfExtent - radius - Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y));
        return Mathf.Clamp01(remaining / 90f);
    }

    private static float DistanceToKind(
        GeneratedSpatialWorldPlanRecord spatial,
        Vector2 point,
        string kind)
    {
        float best = float.PositiveInfinity;
        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord location = spatial.locations[i];
            if (location != null && string.Equals(location.locationKind, kind, StringComparison.OrdinalIgnoreCase))
                best = Mathf.Min(best, Vector2.Distance(point, Position(location)));
        }
        return float.IsPositiveInfinity(best) ? WorldSize : best;
    }

    private static Vector2 NearestLocationPosition(
        GeneratedSpatialWorldPlanRecord spatial,
        Vector2 point,
        string kind)
    {
        GeneratedSpatialLocationRecord location = NearestLocation(spatial, point, kind);
        return location != null ? Position(location) : Vector2.zero;
    }

    private static GeneratedSpatialLocationRecord NearestLocation(
        GeneratedSpatialWorldPlanRecord spatial,
        Vector2 point,
        params string[] kinds)
    {
        GeneratedSpatialLocationRecord best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord location = spatial.locations[i];
            if (location == null || !Contains(kinds, location.locationKind))
                continue;
            float distance = Vector2.SqrMagnitude(point - Position(location));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = location;
            }
        }
        return best;
    }

    private static List<GeneratedSpatialLocationRecord> LocationsOfKind(
        GeneratedSpatialWorldPlanRecord spatial,
        string kind)
    {
        List<GeneratedSpatialLocationRecord> result = new List<GeneratedSpatialLocationRecord>();
        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord location = spatial.locations[i];
            if (location != null && string.Equals(location.locationKind, kind, StringComparison.OrdinalIgnoreCase))
                result.Add(location);
        }
        return result;
    }

    private static GeneratedSpatialLocationRecord FindLocation(
        GeneratedSpatialWorldPlanRecord spatial,
        string id)
    {
        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord location = spatial.locations[i];
            if (location != null && string.Equals(location.locationId, id, StringComparison.OrdinalIgnoreCase))
                return location;
        }
        return null;
    }

    private static GeneratedSpatialRegionRecord FindSpatialRegion(
        GeneratedSpatialWorldPlanRecord spatial,
        string regionId)
    {
        for (int i = 0; i < spatial.regions.Count; i++)
        {
            GeneratedSpatialRegionRecord region = spatial.regions[i];
            if (region != null && string.Equals(region.regionId, regionId, StringComparison.OrdinalIgnoreCase))
                return region;
        }
        return spatial.regions.Count > 0 ? spatial.regions[0] : null;
    }

    private static bool HasCompleteLocationAuthority(
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialWorldPlanRecord spatial)
    {
        if (spatial.locations == null || spatial.routes == null || spatial.regions == null)
            return false;
        int required = 1 + plan.settlements.Count + plan.encampments.Count + plan.pointsOfInterest.Count;
        return spatial.locations.Count >= required && spatial.regions.Count >= Mathf.Max(1, plan.regions.Count);
    }

    private static string ResolveStructuralTheme(GeneratedWorldPlanRecord plan)
    {
        string corpus = ((plan.summary ?? string.Empty) + " " + (plan.designNotes ?? string.Empty)).ToLowerInvariant();
        for (int i = 0; i < plan.regions.Count; i++)
            corpus += " " + RegionCorpus(plan.regions[i]);
        if (ContainsAny(corpus, "cyberpunk", "neon", "corporate", "arcology", "megacity"))
            return "cyberpunk";
        if (ContainsAny(corpus, "zombie", "post-apocalypse", "post apocalypse", "wasteland", "survivor"))
            return "post_apocalypse";
        if (ContainsAny(corpus, "noir", "detective", "nightlife", "crime city"))
            return "noir_city";
        return "high_fantasy";
    }

    private static string ResolveSettlementArchetype(GeneratedSettlementRecord settlement, string theme)
    {
        string kind = (settlement.kind ?? "settlement").ToLowerInvariant();
        if (theme == "cyberpunk")
            return ContainsAny(kind, "capital", "city") ? "dense_vertical_core" : "transit_linked_district";
        if (theme == "post_apocalypse")
            return ContainsAny(kind, "fort", "camp") ? "barricaded_survivor_zone" : "reclaimed_roadside_enclave";
        if (theme == "noir_city")
            return ContainsAny(kind, "port", "dock") ? "industrial_dock_grid" : "street_block_neighborhood";
        if (ContainsAny(kind, "mine", "mining"))
            return "contour_mining_camp";
        if (ContainsAny(kind, "fort", "castle", "keep"))
            return "defensible_gate_and_courtyard";
        if (ContainsAny(kind, "farm", "hamlet"))
            return "main_road_agricultural_hamlet";
        if (ContainsAny(kind, "trade", "market", "town"))
            return "crossroads_market_town";
        return "main_street_village";
    }

    private static string ResolveHostileArchetype(GeneratedEncampmentRecord camp, string theme)
    {
        string corpus = ((camp.kind ?? string.Empty) + " " + (camp.layoutIntent ?? string.Empty)).ToLowerInvariant();
        if (theme == "cyberpunk")
            return "restricted_facility_overwatch";
        if (theme == "post_apocalypse")
            return "road_raid_barricade";
        if (ContainsAny(corpus, "cave", "mine", "crypt"))
            return "geology_anchored_dungeon_approach";
        return "route_pressure_camp";
    }

    private static float ResolveCivilizationDensity(GeneratedRegionRecord region, string theme)
    {
        if (theme == "cyberpunk" || theme == "noir_city")
            return 0.85f;
        if (theme == "post_apocalypse")
            return 0.24f;
        string corpus = RegionCorpus(region);
        return ContainsAny(corpus, "city", "trade", "farm", "kingdom") ? 0.68f : 0.42f;
    }

    private static string ResolveBiome(string corpus, string theme)
    {
        if (theme == "cyberpunk" || theme == "noir_city")
            return "urbanized";
        if (ContainsAny(corpus, "snow", "tundra", "alpine"))
            return "alpine";
        if (ContainsAny(corpus, "marsh", "swamp", "wetland"))
            return "wetland";
        if (ContainsAny(corpus, "desert", "wasteland", "arid"))
            return "arid";
        if (ContainsAny(corpus, "forest", "wood"))
            return "temperate_forest";
        return "temperate_mixed";
    }

    private static string ResolveTerrainArchetype(string corpus, string theme)
    {
        if (theme == "cyberpunk" || theme == "noir_city")
            return "graded_urban_basin";
        if (ContainsAny(corpus, "mountain", "alpine", "volcan"))
            return "ridge_and_pass";
        if (ContainsAny(corpus, "marsh", "river", "wet"))
            return "river_valley_basin";
        return "rolling_valley_and_upland";
    }

    private static string RegionCorpus(GeneratedRegionRecord region)
    {
        if (region == null)
            return string.Empty;
        string result = ((region.role ?? string.Empty) + " " + (region.terrainProfile ?? string.Empty) + " " +
                         (region.climateProfile ?? string.Empty) + " " + (region.gameplayPremise ?? string.Empty) + " " +
                         (region.assetStyleKey ?? string.Empty)).ToLowerInvariant();
        if (region.biomeTags != null)
            result += " " + string.Join(" ", region.biomeTags).ToLowerInvariant();
        return result;
    }

    private static string BuildSemanticFingerprint(GeneratedWorldPlanRecord plan)
    {
        string value = SafeSeed(plan) + "|" + (plan.summary ?? string.Empty) + "|" + (plan.designNotes ?? string.Empty);
        for (int i = 0; i < plan.regions.Count; i++)
            value += "|r:" + plan.regions[i]?.regionId + ":" + RegionCorpus(plan.regions[i]);
        for (int i = 0; i < plan.settlements.Count; i++)
            value += "|s:" + plan.settlements[i]?.settlementId + ":" + plan.settlements[i]?.kind;
        for (int i = 0; i < plan.encampments.Count; i++)
            value += "|e:" + plan.encampments[i]?.encampmentId + ":" + plan.encampments[i]?.kind;
        for (int i = 0; i < plan.pointsOfInterest.Count; i++)
            value += "|p:" + plan.pointsOfInterest[i]?.poiId + ":" + plan.pointsOfInterest[i]?.kind;
        return StableHex(value);
    }

    private static float ValueNoise(float x, float z, string seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);
        float tx = x - x0;
        float tz = z - z0;
        tx = tx * tx * (3f - 2f * tx);
        tz = tz * tz * (3f - 2f * tz);
        float a = Hash01(seed + "|noise|" + x0 + "|" + z0);
        float b = Hash01(seed + "|noise|" + (x0 + 1) + "|" + z0);
        float c = Hash01(seed + "|noise|" + x0 + "|" + (z0 + 1));
        float d = Hash01(seed + "|noise|" + (x0 + 1) + "|" + (z0 + 1));
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }

    private static Vector2 HashPoint(string seed, float radius)
    {
        float angle = Hash01(seed + "|angle") * 360f;
        float distance = Mathf.Sqrt(Hash01(seed + "|distance")) * radius;
        return Rotate(Vector2.up * distance, angle);
    }

    private static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
    }

    private static float HeadingToward(Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        return Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
    }

    private static Vector2 Position(GeneratedSpatialLocationRecord location)
    {
        return new Vector2(location.worldX, location.worldZ);
    }

    private static int GridIndex(Vector2 point)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(-WorldHalfExtent, WorldHalfExtent, point.x) * (RouteGridSize - 1)), 0, RouteGridSize - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(-WorldHalfExtent, WorldHalfExtent, point.y) * (RouteGridSize - 1)), 0, RouteGridSize - 1);
        return z * RouteGridSize + x;
    }

    private static Vector2 GridPosition(int index)
    {
        int x = index % RouteGridSize;
        int z = index / RouteGridSize;
        return new Vector2(
            Mathf.Lerp(-WorldHalfExtent, WorldHalfExtent, x / (RouteGridSize - 1f)),
            Mathf.Lerp(-WorldHalfExtent, WorldHalfExtent, z / (RouteGridSize - 1f)));
    }

    private static string SafeSeed(GeneratedWorldPlanRecord plan)
    {
        return plan != null && !string.IsNullOrWhiteSpace(plan.worldSeed)
            ? plan.worldSeed.Trim()
            : "yourquest_default_world";
    }

    private static bool Contains(string[] values, string value)
    {
        if (values == null)
            return false;
        for (int i = 0; i < values.Length; i++)
        {
            if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool ContainsAny(string corpus, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(corpus) || terms == null)
            return false;
        for (int i = 0; i < terms.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(terms[i]) &&
                corpus.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static float HashSigned(string value)
    {
        return Hash01(value) * 2f - 1f;
    }

    private static float Hash01(string value)
    {
        uint hash = 2166136261u;
        string safe = value ?? string.Empty;
        for (int i = 0; i < safe.Length; i++)
        {
            hash ^= safe[i];
            hash *= 16777619u;
        }
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    private static string StableHex(string value)
    {
        uint hash = 2166136261u;
        string safe = value ?? string.Empty;
        for (int i = 0; i < safe.Length; i++)
        {
            hash ^= safe[i];
            hash *= 16777619u;
        }
        return hash.ToString("x8");
    }

    private struct CandidateScore
    {
        public float terrain;
        public float water;
        public float route;
        public float defense;
        public float total;
    }

    private sealed class MinHeap
    {
        private readonly int[] nodes;
        private readonly float[] priorities;
        public int Count { get; private set; }

        public MinHeap(int capacity)
        {
            nodes = new int[Mathf.Max(4, capacity * 8)];
            priorities = new float[nodes.Length];
        }

        public void Push(int node, float priority)
        {
            if (Count >= nodes.Length)
                return;
            int index = Count++;
            nodes[index] = node;
            priorities[index] = priority;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (priorities[parent] <= priorities[index])
                    break;
                Swap(parent, index);
                index = parent;
            }
        }

        public int Pop()
        {
            int result = nodes[0];
            Count--;
            if (Count <= 0)
                return result;
            nodes[0] = nodes[Count];
            priorities[0] = priorities[Count];
            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                int right = left + 1;
                if (left >= Count)
                    break;
                int smallest = right < Count && priorities[right] < priorities[left] ? right : left;
                if (priorities[index] <= priorities[smallest])
                    break;
                Swap(index, smallest);
                index = smallest;
            }
            return result;
        }

        private void Swap(int a, int b)
        {
            int node = nodes[a];
            nodes[a] = nodes[b];
            nodes[b] = node;
            float priority = priorities[a];
            priorities[a] = priorities[b];
            priorities[b] = priority;
        }
    }
}
