using System;
using System.Collections.Generic;
using UnityEngine;

public struct YQSpatialTerrainSampleV2
{
    public float elevationNormalized;
    public float ruggedness;
    public float waterMask;
    public float waterSurfaceNormalized;
    public int waterFeatureIndex;
    public float bankSupportMask;
    public float routeMask;
    public float siteReserveMask;
    public float caveMassMask;
}

/// <summary>
/// Converts an accepted V2 blueprint into compact arrays once, then samples
/// terrain, water, routes, and buildable reserves without per-sample garbage.
/// </summary>
public sealed class YQSpatialBlueprintTerrainSamplerV2
{
    private const float DefaultSurface = 0.36f;
    private const float ApproximateTerrainHeightMetres = YQGeneratedWorldTerrain.TerrainHeight;

    private struct PreparedPoint
    {
        public float x;
        public float z;
        public float elevation;
        public float width;
        public Vector2 waterSide;
    }

    private struct PreparedRegion
    {
        public float x;
        public float z;
        public float radius;
        public float elevationBias;
        public float ruggedness;
    }

    private struct PreparedField
    {
        public YQTerrainFieldKindV2 kind;
        public float strength;
        public float radius;
        public float falloff;
        public int pointStart;
        public int pointCount;
        public int causalPhase;
        public int sourceOrder;
    }

    private struct PreparedWater
    {
        public YQHydrologyKindV2 kind;
        public float level;
        public float width;
        public float depth;
        public int pointStart;
        public int pointCount;
    }

    private struct PreparedRoute
    {
        public float width;
        public float shoulderWidth;
        public int pointStart;
        public int pointCount;
    }

    private struct PreparedReserve
    {
        public float x;
        public float z;
        public float radius;
        public float targetElevation;
    }

    private readonly PreparedRegion[] regions;
    private readonly PreparedField[] fields;
    private readonly PreparedPoint[] fieldPoints;
    private readonly PreparedWater[] waters;
    private readonly PreparedPoint[] waterPoints;
    private readonly PreparedRoute[] routes;
    private readonly PreparedPoint[] routePoints;
    private readonly PreparedReserve[] reserves;

    private YQSpatialBlueprintTerrainSamplerV2(
        PreparedRegion[] regions,
        PreparedField[] fields,
        PreparedPoint[] fieldPoints,
        PreparedWater[] waters,
        PreparedPoint[] waterPoints,
        PreparedRoute[] routes,
        PreparedPoint[] routePoints,
        PreparedReserve[] reserves)
    {
        this.regions = regions;
        this.fields = fields;
        this.fieldPoints = fieldPoints;
        this.waters = waters;
        this.waterPoints = waterPoints;
        this.routes = routes;
        this.routePoints = routePoints;
        this.reserves = reserves;
    }

    public static bool TryPrepare(
        GeneratedWorldPlanRecord plan,
        out YQSpatialBlueprintTerrainSamplerV2 sampler,
        out string failure)
    {
        sampler = null;
        if (!YQSpatialPlanVersionRouter.TryValidateAcceptedV2(
                plan,
                out failure))
        {
            return false;
        }

        YQSpatialBlueprintV2 blueprint = plan.spatialPlanV2.blueprint;
        blueprint.EnsureCollections();

        PreparedRegion[] preparedRegions =
            new PreparedRegion[blueprint.regions.Count];
        for (int index = 0; index < blueprint.regions.Count; index++)
        {
            YQRegionDomainV2 source = blueprint.regions[index];
            if (source == null)
                continue;
            preparedRegions[index] = new PreparedRegion
            {
                x = source.centerX,
                z = source.centerZ,
                radius = Mathf.Max(1f, source.radius),
                elevationBias = source.elevationBias,
                ruggedness = Mathf.Clamp01(source.ruggedness)
            };
        }

        PreparedPoint[] preparedFieldPoints =
            new PreparedPoint[CountTerrainPoints(blueprint)];
        PreparedField[] preparedFields =
            new PreparedField[blueprint.terrainFields.Count];
        int fieldPointCursor = 0;
        for (int index = 0; index < blueprint.terrainFields.Count; index++)
        {
            YQTerrainFieldV2 source = blueprint.terrainFields[index];
            if (source == null)
                continue;
            int pointStart = fieldPointCursor;
            CopyPoints(source.controlPoints, preparedFieldPoints,
                ref fieldPointCursor);
            preparedFields[index] = new PreparedField
            {
                kind = source.kind,
                strength = source.strength,
                radius = Mathf.Max(1f, source.radius),
                falloff = Mathf.Max(0.25f, source.falloff),
                pointStart = pointStart,
                pointCount = fieldPointCursor - pointStart,
                causalPhase = ResolveCausalPhase(source.kind),
                sourceOrder = index
            };
        }
        Array.Sort(preparedFields, ComparePreparedFields);

        PreparedPoint[] preparedWaterPoints =
            new PreparedPoint[CountWaterPoints(blueprint)];
        PreparedWater[] preparedWaters =
            new PreparedWater[blueprint.hydrology.Count];
        int waterPointCursor = 0;
        for (int index = 0; index < blueprint.hydrology.Count; index++)
        {
            YQHydrologyFeatureV2 source = blueprint.hydrology[index];
            if (source == null)
                continue;
            int pointStart = waterPointCursor;
            CopyPoints(source.controlPoints, preparedWaterPoints,
                ref waterPointCursor);
            // note: Keep accepted hydrology coordinates shared with waterfalls and road crossings; moving only rivers disconnects their dependent features.
            // note: Cache the same corner directions used by the rendered ribbon, rather than grading a different centreline footprint.
            for (int p = pointStart; p < waterPointCursor; p++)
            {
                var before = preparedWaterPoints[Mathf.Max(pointStart, p - 1)];
                var after = preparedWaterPoints[Mathf.Min(waterPointCursor - 1, p + 1)];
                Vector2 tangent = new Vector2(after.x - before.x, after.z - before.z);
                if (tangent.sqrMagnitude < .0001f) tangent = Vector2.up;
                tangent.Normalize();
                var current = preparedWaterPoints[p];
                current.waterSide = new Vector2(-tangent.y, tangent.x);
                preparedWaterPoints[p] = current;
            }
            preparedWaters[index] = new PreparedWater
            {
                kind = source.kind,
                level = Mathf.Clamp01(source.waterLevelNormalized),
                // note: A generated primary river has a real channel scale instead of collapsing to a creek-width strip; authored wider rivers remain authoritative.
                width = Mathf.Max(source.kind == YQHydrologyKindV2.River ? 14f : source.kind == YQHydrologyKindV2.Waterfall ? 10f : 1f, source.nominalWidth),
                depth = Mathf.Max(source.kind == YQHydrologyKindV2.River ? 4.5f : source.kind == YQHydrologyKindV2.Waterfall ? 3.5f : 0f, source.nominalDepth),
                pointStart = pointStart,
                pointCount = waterPointCursor - pointStart
            };
        }

        PreparedPoint[] preparedRoutePoints =
            new PreparedPoint[CountRoutePoints(blueprint)];
        PreparedRoute[] preparedRoutes =
            new PreparedRoute[blueprint.routes.Count];
        int routePointCursor = 0;
        for (int index = 0; index < blueprint.routes.Count; index++)
        {
            YQRouteCorridorV2 source = blueprint.routes[index];
            if (source == null)
                continue;
            int pointStart = routePointCursor;
            CopyPoints(source.controlPoints, preparedRoutePoints,
                ref routePointCursor);
            preparedRoutes[index] = new PreparedRoute
            {
                width = Mathf.Max(1f, source.width),
                shoulderWidth = Mathf.Max(0f, source.shoulderWidth),
                pointStart = pointStart,
                pointCount = routePointCursor - pointStart
            };
        }

        int reserveCount = 0;
        for (int index = 0; index < blueprint.sites.Count; index++)
        {
            YQSiteAnchorV2 source = blueprint.sites[index];
            if (source != null && source.requiresTerrainConformance &&
                source.kind != YQSiteKindV2.NaturalFeature)
            {
                reserveCount++;
            }
        }

        PreparedReserve[] preparedReserves =
            new PreparedReserve[reserveCount];
        int reserveCursor = 0;
        for (int index = 0; index < blueprint.sites.Count; index++)
        {
            YQSiteAnchorV2 source = blueprint.sites[index];
            if (source == null || !source.requiresTerrainConformance ||
                source.kind == YQSiteKindV2.NaturalFeature)
            {
                continue;
            }

            preparedReserves[reserveCursor++] = new PreparedReserve
            {
                x = source.x,
                z = source.z,
                radius = Mathf.Max(4f, source.reservedRadius)
            };
        }

        sampler = new YQSpatialBlueprintTerrainSamplerV2(
            preparedRegions,
            preparedFields,
            preparedFieldPoints,
            preparedWaters,
            preparedWaterPoints,
            preparedRoutes,
            preparedRoutePoints,
            preparedReserves);

        // note: Route grades and construction pads inherit the compiled macro surface instead of trusting placeholder elevations in semantic records.
        for (int index = 0; index < sampler.routePoints.Length; index++)
        {
            PreparedPoint point = sampler.routePoints[index];
            point.elevation = sampler.EvaluateRawSurface(
                point.x,
                point.z,
                out _);
            sampler.routePoints[index] = point;
        }

        for (int index = 0; index < sampler.reserves.Length; index++)
        {
            PreparedReserve reserve = sampler.reserves[index];
            reserve.targetElevation = sampler.EvaluateRawSurface(
                reserve.x,
                reserve.z,
                out _);
            sampler.reserves[index] = reserve;
        }

        failure = string.Empty;
        return true;
    }

    public YQSpatialTerrainSampleV2 Sample(float worldX, float worldZ)
    {
        float height = EvaluateRawSurface(worldX, worldZ,
            out float ruggedness);
        float waterMask = 0f;
        float waterSurface = 0f;
        int waterFeatureIndex = -1;
        float waterBed = 0f;
        float bankSupportMask = 0f;
        float waterTerrainBlendMask = 0f;

        for (int index = 0; index < waters.Length; index++)
        {
            PreparedWater water = waters[index];
            if (water.pointCount <= 0)
            {
                continue;
            }

            NearestPolyline(
                waterPoints,
                water.pointStart,
                water.pointCount,
                worldX,
                worldZ,
                out float distance,
                out float pointElevation,
                out float pointWidth);
            float radius = Mathf.Max(
                1.5f,
                Mathf.Max(water.width, pointWidth) * 0.5f);
            if (water.pointCount > 1 && water.kind != YQHydrologyKindV2.Lake && water.kind != YQHydrologyKindV2.Wetland)
                SampleRibbonFootprint(water, worldX, worldZ, out distance, out radius, out pointElevation);
            // note: Area meshes use an ellipse, so their bed must use the identical footprint rather than a polyline trench.
            if (water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland)
            {
                SampleAreaWaterFootprint(water, worldX, worldZ, out distance, out radius);
                pointElevation = water.level > 0f ? water.level : waterPoints[water.pointStart].elevation;
            }
            // note: Keep the entire water footprint supported; only the dry outer bank blends back to existing terrain.
            // note: A shoreline needs a complete terrain-cell diagonal of support on either side; a sub-cell crest disappears during heightmap interpolation.
            float bankPadding = YQGeneratedWorldTerrain.WorldSize / (YQGeneratedWorldTerrain.HeightmapResolution - 1f) * 1.414214f;
            float footprintTransition = Mathf.Max(8f, radius * .6f);
            float footprintMask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius + bankPadding, radius + bankPadding + footprintTransition, distance));
            // note: Terrain shoulders must finish their full blend even outside the semantic wet footprint; stopping here produced a vertical edge at the mask boundary.
            bool ownsWater = footprintMask > waterMask;
            // note: Expose the actual shoreline band for the final construction pass, where pads and roads may otherwise cut away its support.
            // note: Support only the dry bank; the former symmetric band filled narrow river centres back above their water level.
            float featureBankSupport = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Mathf.Max(0f, radius - bankPadding), radius, distance)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius + bankPadding, radius + bankPadding + footprintTransition, distance)));
            float featureSurface = pointElevation > 0f
                ? pointElevation
                : water.level;
            if (water.kind != YQHydrologyKindV2.Lake && water.kind != YQHydrologyKindV2.Wetland)
            {
                // note: A receiving lake owns the confluence datum. Ease the river into that level over a bank-width transition instead of leaving a vertical seam.
                for (int lakeIndex = 0; lakeIndex < waters.Length; lakeIndex++)
                {
                    var lake = waters[lakeIndex];
                    if (lake.pointCount == 0 || (lake.kind != YQHydrologyKindV2.Lake && lake.kind != YQHydrologyKindV2.Wetland)) continue;
                    SampleAreaWaterFootprint(lake, worldX, worldZ, out float lakeDistance, out float lakeRadius);
                    float shoreDistance = Mathf.Max(0f, lakeDistance - lakeRadius - bankPadding);
                    float blend = 1f - Mathf.SmoothStep(0f, 1f, shoreDistance / Mathf.Max(20f, radius * 2f));
                    float lakeLevel = lake.level > 0f ? lake.level : waterPoints[lake.pointStart].elevation;
                    featureSurface = Mathf.Lerp(featureSurface, lakeLevel, blend);
                }
            }
            float depth = Mathf.Max(.6f, water.depth);
            float crossSection = Mathf.Clamp01(distance / Mathf.Max(.5f, radius - bankPadding));
            // note: A concave cross-section rises from a submerged centre to a bank just above the water plane.
            float bed = featureSurface + (-depth + (depth + .2f) * crossSection * crossSection) / ApproximateTerrainHeightMetres;
            if (ownsWater)
            {
                waterMask = footprintMask;
                waterFeatureIndex = index;
                waterSurface = featureSurface;
                bankSupportMask = featureBankSupport;
            }
            // note: A channel interior cuts through the receiving shoreline instead of letting the lake's dry rim dam the incoming river.
            if (distance < radius - bankPadding && bed < featureSurface)
                bankSupportMask *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distance - (radius - bankPadding) * .75f) / Mathf.Max(.5f, (radius - bankPadding) * .25f)));
            // note: The dry bank widens only as much as the local water-to-terrain rise requires, capping the generated shoreline grade without expanding the gameplay water mask.
            float riseMetres = Mathf.Max(0f, height - bed) * ApproximateTerrainHeightMetres;
            float slopeLimitedTransition = riseMetres / Mathf.Max(.1f, Mathf.Tan(18f * Mathf.Deg2Rad));
            float shorelineTransition = Mathf.Max(10f, radius * .8f, slopeLimitedTransition);
            if (water.kind == YQHydrologyKindV2.River || water.kind == YQHydrologyKindV2.Waterfall)
            {
                // note: A river crossing a ridge receives a broad, deterministic ravine cut so the water has a continuous way through the terrain instead of a floating ribbon or sheer wall.
                float ridgeCutTransition = riseMetres / Mathf.Max(.1f, Mathf.Tan(12f * Mathf.Deg2Rad));
                shorelineTransition = Mathf.Max(
                    shorelineTransition,
                    radius * 3.5f + 8f,
                    ridgeCutTransition);
            }
            float shorelineMask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius + bankPadding, radius + bankPadding + shorelineTransition, distance));
            if (shorelineMask > waterTerrainBlendMask || (shorelineMask >= .999f && bed < waterBed))
            {
                // note: Keep the winning water bed paired with its blend mask when rivers and receiving bodies overlap.
                waterTerrainBlendMask = shorelineMask;
                waterBed = bed;
            }
            height = Mathf.Lerp(
                height,
                // note: Cut or fill to the accepted channel datum so the water cannot bridge an unrelated depression.
                waterTerrainBlendMask >= .999f ? Mathf.Min(bed, waterBed) : bed,
                shorelineMask);
        }

        float routeMask = 0f;
        float routeElevation = height;
        for (int index = 0; index < routes.Length; index++)
        {
            PreparedRoute route = routes[index];
            if (route.pointCount <= 0)
                continue;

            NearestPolyline(
                routePoints,
                route.pointStart,
                route.pointCount,
                worldX,
                worldZ,
                out float distance,
                out float elevation,
                out _);
            float radius = route.width * 0.5f + route.shoulderWidth;
            float mask = FalloffMask(distance, Mathf.Max(1f, radius), 1.9f);
            if (mask > routeMask)
            {
                routeMask = mask;
                routeElevation = elevation;
            }
        }
        height = Mathf.Lerp(height, routeElevation, routeMask * 0.72f);

        float reserveMask = 0f;
        float reserveElevation = height;
        for (int index = 0; index < reserves.Length; index++)
        {
            PreparedReserve reserve = reserves[index];
            float dx = worldX - reserve.x;
            float dz = worldZ - reserve.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            float mask = FalloffMask(distance, reserve.radius, 2.8f);
            if (mask > reserveMask)
            {
                reserveMask = mask;
                reserveElevation = reserve.targetElevation;
            }
        }
        height = Mathf.Lerp(height, reserveElevation, reserveMask * 0.96f);
        // note: Generic route/reserve flattening must not remove an accepted channel's bed or enclosing banks; explicit crossing construction remains a later responsibility.
        height = Mathf.Lerp(height, waterBed, waterTerrainBlendMask);

        float caveMask = 0f;
        for (int index = 0; index < fields.Length; index++)
        {
            PreparedField field = fields[index];
            if (field.kind != YQTerrainFieldKindV2.CaveMass ||
                field.pointCount <= 0)
            {
                continue;
            }

            NearestPolyline(
                fieldPoints,
                field.pointStart,
                field.pointCount,
                worldX,
                worldZ,
                out float distance,
                out _,
                out float width);
            caveMask = Mathf.Max(
                caveMask,
                FalloffMask(
                    distance,
                    Mathf.Max(field.radius, width * 0.5f),
                    field.falloff));
        }

        return new YQSpatialTerrainSampleV2
        {
            elevationNormalized = Mathf.Clamp(height, 0.025f, 0.88f),
            ruggedness = Mathf.Clamp01(ruggedness),
            waterMask = Mathf.Clamp01(waterMask),
            waterSurfaceNormalized = Mathf.Clamp01(waterSurface),
            waterFeatureIndex = waterFeatureIndex,
            bankSupportMask = bankSupportMask,
            routeMask = Mathf.Clamp01(routeMask),
            siteReserveMask = Mathf.Clamp01(reserveMask),
            caveMassMask = Mathf.Clamp01(caveMask)
        };
    }

    private float EvaluateRawSurface(
        float worldX,
        float worldZ,
        out float ruggedness)
    {
        float regionWeight = 0f;
        float weightedBias = 0f;
        float weightedRuggedness = 0f;
        for (int index = 0; index < regions.Length; index++)
        {
            PreparedRegion region = regions[index];
            float dx = worldX - region.x;
            float dz = worldZ - region.z;
            float normalizedDistance =
                Mathf.Sqrt(dx * dx + dz * dz) / region.radius;
            float weight = 1f / (0.18f + normalizedDistance * normalizedDistance);
            regionWeight += weight;
            weightedBias += region.elevationBias * weight;
            weightedRuggedness += region.ruggedness * weight;
        }

        float height = DefaultSurface;
        ruggedness = 0.35f;
        if (regionWeight > 0.0001f)
        {
            height += weightedBias / regionWeight * 0.12f;
            ruggedness = weightedRuggedness / regionWeight;
        }

        // note: Preparation sorts fields into causal order once, avoiding six full field scans for every heightmap sample.
        for (int index = 0; index < fields.Length; index++)
        {
            PreparedField field = fields[index];
            if (field.pointCount <= 0 ||
                field.kind == YQTerrainFieldKindV2.CaveMass ||
                field.kind == YQTerrainFieldKindV2.SiteReserve ||
                field.kind == YQTerrainFieldKindV2.Unknown)
            {
                continue;
            }

            NearestPolyline(
                fieldPoints,
                field.pointStart,
                field.pointCount,
                worldX,
                worldZ,
                out float distance,
                out float targetElevation,
                out float pointWidth);
            float fieldRadius = Mathf.Max(field.radius, pointWidth * 0.5f);
            // note: Hills and escarpments need enough horizontal run; narrow high fields formerly made sheer walls and flat clipped summits.
            bool roundedRelief = field.kind == YQTerrainFieldKindV2.HillCluster || field.kind == YQTerrainFieldKindV2.RidgeChain;
            if (roundedRelief)
                fieldRadius = Mathf.Max(fieldRadius, Mathf.Abs(targetElevation - height) * ApproximateTerrainHeightMetres * 3f);
            if (field.kind == YQTerrainFieldKindV2.Escarpment)
            {
                // note: Keep a rock shelf's steepest exposed face below a natural walking-world grade while retaining its authored high ground.
                float riseMetres = Mathf.Abs(targetElevation - height) * ApproximateTerrainHeightMetres;
                float naturalRun = riseMetres / Mathf.Max(.1f, Mathf.Tan(18f * Mathf.Deg2Rad));
                fieldRadius = Mathf.Max(fieldRadius, naturalRun);
            }
            else if (field.kind == YQTerrainFieldKindV2.ValleyCorridor ||
                     field.kind == YQTerrainFieldKindV2.Basin)
            {
                // note: Downward valleys and basins receive the same physical runway as hills so a narrow authored cut cannot become a sheer terrain wall beside otherwise walkable ground.
                float fallMetres = Mathf.Abs(targetElevation - height) * ApproximateTerrainHeightMetres;
                float naturalRun = fallMetres / Mathf.Max(.1f, Mathf.Tan(24f * Mathf.Deg2Rad));
                fieldRadius = Mathf.Max(fieldRadius, naturalRun);
            }
            float mask = FalloffMask(distance, fieldRadius, roundedRelief || field.kind == YQTerrainFieldKindV2.Escarpment ? 1f : field.falloff);
            if (mask <= 0f)
                continue;

            float authority = mask * Mathf.Clamp01(0.45f + Mathf.Abs(field.strength) * 0.75f);
            switch (field.kind)
            {
                case YQTerrainFieldKindV2.BaseSurface:
                    height = Mathf.Lerp(height, targetElevation,
                        authority * 0.35f);
                    break;
                case YQTerrainFieldKindV2.RidgeChain:
                case YQTerrainFieldKindV2.HillCluster:
                case YQTerrainFieldKindV2.Escarpment:
                    height = Mathf.Lerp(
                        height,
                        Mathf.Max(height, targetElevation),
                        authority);
                    ruggedness = Mathf.Max(ruggedness, authority);
                    break;
                case YQTerrainFieldKindV2.ValleyCorridor:
                case YQTerrainFieldKindV2.Basin:
                    height = Mathf.Lerp(
                        height,
                        Mathf.Min(height, targetElevation),
                        authority);
                    ruggedness = Mathf.Lerp(ruggedness, 0.2f,
                        authority * 0.7f);
                    break;
            }
        }

        return Mathf.Clamp(height, 0.025f, 0.88f);
    }

    private static int ComparePreparedFields(
        PreparedField left,
        PreparedField right)
    {
        int phase = left.causalPhase.CompareTo(right.causalPhase);
        return phase != 0
            ? phase
            : left.sourceOrder.CompareTo(right.sourceOrder);
    }

    private static int ResolveCausalPhase(YQTerrainFieldKindV2 kind)
    {
        switch (kind)
        {
            case YQTerrainFieldKindV2.BaseSurface:
                return 0;
            case YQTerrainFieldKindV2.RidgeChain:
                return 1;
            case YQTerrainFieldKindV2.HillCluster:
                return 2;
            case YQTerrainFieldKindV2.Escarpment:
                return 3;
            case YQTerrainFieldKindV2.ValleyCorridor:
                return 4;
            case YQTerrainFieldKindV2.Basin:
                return 5;
            case YQTerrainFieldKindV2.CaveMass:
                return 6;
            case YQTerrainFieldKindV2.SiteReserve:
                return 7;
            default:
                return 8;
        }
    }

    public float SampleReceivingWaterFade(float x, float z)
    {
        // note: Fade flowing overlays only inside the actual area-water footprint, keeping the incoming channel opaque up to the shoreline.
        float fade = 1f;
        foreach (PreparedWater water in waters)
        {
            if (water.pointCount == 0 || (water.kind != YQHydrologyKindV2.Lake && water.kind != YQHydrologyKindV2.Wetland)) continue;
            SampleAreaWaterFootprint(water, x, z, out float distance, out float radius);
            fade = Mathf.Min(fade, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radius - distance) / 12f)));
        }
        return fade;
    }

    private void SampleRibbonFootprint(PreparedWater water, float x, float z, out float distance, out float radius, out float elevation)
    {
        // note: Barycentric sampling follows the exact two triangles rendered for each river segment, including their varying elevations and widths.
        Vector2 query = new Vector2(x, z);
        float best = float.PositiveInfinity;
        distance = radius = elevation = 0f;
        for (int i = 1; i < water.pointCount; i++)
        {
            var a = waterPoints[water.pointStart + i - 1];
            var b = waterPoints[water.pointStart + i];
            float ra = Mathf.Max(.75f, Mathf.Max(water.width, a.width) * .5f);
            float rb = Mathf.Max(.75f, Mathf.Max(water.width, b.width) * .5f);
            Vector2 ca = new Vector2(a.x, a.z), cb = new Vector2(b.x, b.z);
            Vector2 al = ca - a.waterSide * ra, ar = ca + a.waterSide * ra;
            Vector2 bl = cb - b.waterSide * rb, br = cb + b.waterSide * rb;
            for (int triangle = 0; triangle < 2; triangle++)
            {
                Vector2 p = triangle == 0 ? al : ar;
                Vector2 q = bl;
                Vector2 r = triangle == 0 ? ar : br;
                Vector3 weights = ClosestTriangleWeights(query, p, q, r);
                float outside = Vector2.Distance(query, p * weights.x + q * weights.y + r * weights.z);
                if (outside >= best) continue;
                best = outside;
                float along = weights.y + (triangle == 1 ? weights.z : 0f);
                float across = triangle == 0 ? weights.z : weights.x + weights.z;
                radius = Mathf.Lerp(ra, rb, along);
                distance = Mathf.Abs(across * 2f - 1f) * radius + outside;
                float ya = a.elevation > 0f ? a.elevation : water.level;
                float yb = b.elevation > 0f ? b.elevation : water.level;
                elevation = Mathf.Lerp(ya, yb, along);
            }
        }
    }

    private static Vector3 ClosestTriangleWeights(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        // note: Use interior barycentrics or the nearest boundary edge; this also handles degenerate segments without NaN terrain heights.
        Vector2 ab = b - a, ac = c - a, ap = point - a;
        float determinant = ab.x * ac.y - ab.y * ac.x;
        if (Mathf.Abs(determinant) > .00001f)
        {
            float v = (ap.x * ac.y - ap.y * ac.x) / determinant;
            float w = (ab.x * ap.y - ab.y * ap.x) / determinant;
            if (v >= 0f && w >= 0f && v + w <= 1f) return new Vector3(1f - v - w, v, w);
        }
        float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(.000001f, ab.sqrMagnitude));
        Vector3 result = new Vector3(1f - t, t, 0f);
        float best = (point - Vector2.Lerp(a, b, t)).sqrMagnitude;
        Vector2 bc = c - b;
        t = Mathf.Clamp01(Vector2.Dot(point - b, bc) / Mathf.Max(.000001f, bc.sqrMagnitude));
        float candidate = (point - Vector2.Lerp(b, c, t)).sqrMagnitude;
        if (candidate < best) { best = candidate; result = new Vector3(0f, 1f - t, t); }
        t = Mathf.Clamp01(Vector2.Dot(ap, ac) / Mathf.Max(.000001f, ac.sqrMagnitude));
        if ((point - Vector2.Lerp(a, c, t)).sqrMagnitude < best) result = new Vector3(1f - t, 0f, t);
        return result;
    }

    private void SampleAreaWaterFootprint(PreparedWater water, float x, float z, out float distance, out float radius)
    {
        // note: Match TryBuildPreparedAreaWaterBasin and its mesh's 0.94 inset, including elongated multi-point lakes.
        Vector2 center = Vector2.zero;
        for (int i = 0; i < water.pointCount; i++)
        {
            var point = waterPoints[water.pointStart + i];
            center += new Vector2(point.x, point.z);
        }
        center /= water.pointCount;
        var first = waterPoints[water.pointStart];
        var last = waterPoints[water.pointStart + water.pointCount - 1];
        Vector2 axis = new Vector2(last.x - first.x, last.z - first.z);
        if (axis.sqrMagnitude < .0001f) axis = Vector2.right;
        axis.Normalize();
        Vector2 side = new Vector2(-axis.y, axis.x);
        float longRadius = Mathf.Max(6f, water.width * .5f);
        float shortRadius = Mathf.Max(5f, water.width * .36f);
        for (int i = 0; i < water.pointCount; i++)
        {
            var point = waterPoints[water.pointStart + i];
            Vector2 delta = new Vector2(point.x, point.z) - center;
            float pointRadius = Mathf.Max(1f, Mathf.Max(water.width, point.width) * .5f);
            longRadius = Mathf.Max(longRadius, Mathf.Abs(Vector2.Dot(delta, axis)) + pointRadius);
            shortRadius = Mathf.Max(shortRadius, Mathf.Abs(Vector2.Dot(delta, side)) + pointRadius);
        }
        Vector2 offset = new Vector2(x, z) - center;
        float u = Vector2.Dot(offset, axis) / (longRadius * .94f);
        float v = Vector2.Dot(offset, side) / (shortRadius * .94f);
        radius = Mathf.Min(longRadius, shortRadius) * .94f;
        distance = Mathf.Sqrt(u * u + v * v) * radius;
    }

    private static void NearestPolyline(
        PreparedPoint[] points,
        int start,
        int count,
        float x,
        float z,
        out float distance,
        out float elevation,
        out float width)
    {
        PreparedPoint first = points[start];
        float firstDx = x - first.x;
        float firstDz = z - first.z;
        float bestDistanceSquared = firstDx * firstDx + firstDz * firstDz;
        elevation = first.elevation;
        width = first.width;

        for (int index = 1; index < count; index++)
        {
            PreparedPoint a = points[start + index - 1];
            PreparedPoint b = points[start + index];
            float segmentX = b.x - a.x;
            float segmentZ = b.z - a.z;
            float lengthSquared = segmentX * segmentX + segmentZ * segmentZ;
            float t = lengthSquared > 0.0001f
                ? Mathf.Clamp01(((x - a.x) * segmentX +
                                 (z - a.z) * segmentZ) / lengthSquared)
                : 0f;
            float nearestX = a.x + segmentX * t;
            float nearestZ = a.z + segmentZ * t;
            float dx = x - nearestX;
            float dz = z - nearestZ;
            float candidateDistanceSquared = dx * dx + dz * dz;
            if (candidateDistanceSquared >= bestDistanceSquared)
                continue;

            bestDistanceSquared = candidateDistanceSquared;
            elevation = Mathf.Lerp(a.elevation, b.elevation, t);
            width = Mathf.Lerp(a.width, b.width, t);
        }

        distance = Mathf.Sqrt(bestDistanceSquared);
    }

    private static float FalloffMask(
        float distance,
        float radius,
        float falloff)
    {
        float t = Mathf.Clamp01(distance / Mathf.Max(0.001f, radius));
        float smooth = t * t * (3f - 2f * t);
        return Mathf.Pow(1f - smooth, Mathf.Max(0.25f, falloff));
    }

    public static void ApplyRuntimeRiverShape(List<Vector3> points)
    {
        if (points == null || points.Count < 3)
            return;

        Vector2 start = new Vector2(points[0].x, points[0].z);
        Vector2 end = new Vector2(points[points.Count - 1].x, points[points.Count - 1].z);
        Vector2 axis = end - start;
        float length = axis.magnitude;
        if (length < 24f)
            return;
        axis /= length;
        Vector2 side = new Vector2(-axis.y, axis.x);
        float maximumDeviation = 0f;
        for (int index = 1; index < points.Count - 1; index++)
            maximumDeviation = Mathf.Max(maximumDeviation, Mathf.Abs(Vector2.Dot(new Vector2(points[index].x, points[index].z) - start, side)));
        if (maximumDeviation > Mathf.Max(6f, length * .025f))
            return;

        float phase = Mathf.Repeat(Mathf.Abs(start.x * .0137f + start.y * .0191f + end.x * .0071f), 1f) * Mathf.PI * 2f;
        float amplitude = Mathf.Clamp(length * .075f, 10f, 28f);
        for (int index = 1; index < points.Count - 1; index++)
        {
            float t = index / (float)(points.Count - 1);
            float offset = Mathf.Sin(t * Mathf.PI * 2.15f + phase) * Mathf.Sin(t * Mathf.PI) * amplitude;
            Vector2 xz = new Vector2(points[index].x, points[index].z) + side * offset;
            points[index] = new Vector3(xz.x, points[index].y, xz.y);
        }
    }

    private static void ApplyRuntimeRiverShape(PreparedPoint[] points, int start, int count)
    {
        if (points == null || count < 3)
            return;

        Vector2 origin = new Vector2(points[start].x, points[start].z);
        Vector2 end = new Vector2(points[start + count - 1].x, points[start + count - 1].z);
        Vector2 axis = end - origin;
        float length = axis.magnitude;
        if (length < 24f)
            return;
        axis /= length;
        Vector2 side = new Vector2(-axis.y, axis.x);
        float maximumDeviation = 0f;
        for (int index = 1; index < count - 1; index++)
            maximumDeviation = Mathf.Max(maximumDeviation, Mathf.Abs(Vector2.Dot(new Vector2(points[start + index].x, points[start + index].z) - origin, side)));
        if (maximumDeviation > Mathf.Max(6f, length * .025f))
            return;

        float phase = Mathf.Repeat(Mathf.Abs(origin.x * .0137f + origin.y * .0191f + end.x * .0071f), 1f) * Mathf.PI * 2f;
        float amplitude = Mathf.Clamp(length * .075f, 10f, 28f);
        for (int index = 1; index < count - 1; index++)
        {
            float t = index / (float)(count - 1);
            float offset = Mathf.Sin(t * Mathf.PI * 2.15f + phase) * Mathf.Sin(t * Mathf.PI) * amplitude;
            Vector2 xz = new Vector2(points[start + index].x, points[start + index].z) + side * offset;
            PreparedPoint point = points[start + index];
            point.x = xz.x;
            point.z = xz.y;
            points[start + index] = point;
        }
    }

    private static void CopyPoints(
        System.Collections.Generic.List<YQBlueprintPointV2> source,
        PreparedPoint[] destination,
        ref int cursor)
    {
        if (source == null)
            return;
        for (int index = 0; index < source.Count; index++)
        {
            YQBlueprintPointV2 point = source[index];
            if (point == null)
                continue;
            destination[cursor++] = new PreparedPoint
            {
                x = point.x,
                z = point.z,
                elevation = Mathf.Clamp01(point.normalizedElevation),
                width = Mathf.Max(0f, point.width)
            };
        }
    }

    private static int CountTerrainPoints(YQSpatialBlueprintV2 blueprint)
    {
        int count = 0;
        for (int index = 0; index < blueprint.terrainFields.Count; index++)
            count += CountPoints(blueprint.terrainFields[index]?.controlPoints);
        return count;
    }

    private static int CountWaterPoints(YQSpatialBlueprintV2 blueprint)
    {
        int count = 0;
        for (int index = 0; index < blueprint.hydrology.Count; index++)
            count += CountPoints(blueprint.hydrology[index]?.controlPoints);
        return count;
    }

    private static int CountRoutePoints(YQSpatialBlueprintV2 blueprint)
    {
        int count = 0;
        for (int index = 0; index < blueprint.routes.Count; index++)
            count += CountPoints(blueprint.routes[index]?.controlPoints);
        return count;
    }

    private static int CountPoints(
        System.Collections.Generic.List<YQBlueprintPointV2> points)
    {
        if (points == null)
            return 0;
        int count = 0;
        for (int index = 0; index < points.Count; index++)
        {
            if (points[index] != null)
                count++;
        }
        return count;
    }
}
