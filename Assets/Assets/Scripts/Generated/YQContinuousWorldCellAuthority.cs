using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// note: This authority supplies order-independent world-space continuation data for streamed cells outside the accepted origin blueprint.
public sealed class YQContinuousWorldCellAuthority
{
    // note: Each tile worker gets its own accepted-V2 sample cache while all authority inputs remain immutable and shared.
    internal sealed class HeightmapSamplingSession : IDisposable
    {
        private readonly YQContinuousWorldCellAuthority authority;
        private readonly YQGeneratedWorldTerrain.V2HeightSampler.SpatialSamplingSession spatialSession;

        internal int SpatialSampleEvaluations => spatialSession != null
            ? spatialSession.SpatialSampleEvaluations
            : 0;
        internal int SpatialSampleCacheHits => spatialSession != null
            ? spatialSession.SpatialSampleCacheHits
            : 0;
        internal int CachedSpatialSampleCount => spatialSession != null
            ? spatialSession.CachedSpatialSampleCount
            : 0;

        internal HeightmapSamplingSession(YQContinuousWorldCellAuthority authority)
        {
            this.authority = authority;
            spatialSession = authority.originSampler != null
                ? authority.originSampler.BeginSpatialSamplingSession()
                : null;
        }

        internal float SampleHeightNormalized(float worldX, float worldZ)
        {
            // note: Heightmap workers use the background-safe authority path with this session's exact-coordinate cache.
            return authority.SampleHeightNormalizedCore(worldX, worldZ, false, spatialSession);
        }

        public void Dispose()
        {
            spatialSession?.Dispose();
        }
    }

    // note: Cache accepted route envelopes once so continuation height sampling does not rescan distant route segments for every terrain vertex.
    private readonly struct AcceptedRouteBounds
    {
        public readonly float minX;
        public readonly float maxX;
        public readonly float minZ;
        public readonly float maxZ;
        public readonly float padding;

        public AcceptedRouteBounds(float minimumX, float maximumX, float minimumZ, float maximumZ, float routePadding)
        {
            minX = minimumX;
            maxX = maximumX;
            minZ = minimumZ;
            maxZ = maximumZ;
            padding = routePadding;
        }
    }

    // note: Accepted continuation carving follows the same terminal ray that builds streamed water geometry.
    // note: Cached continuation lookup keeps every height sample in a cell on the shared deterministic hydrology path.
    // note: Version five records the seeded relief handoff and per-cell continuation feature ownership so prior flat-cell records are refreshed safely.
    public const string SchemaVersion = "continuous_world_cell_v5";
    // note: Edge contracts must be regenerated with the accepted mountain and water elevations after the continuation authority change.
    public const string EdgeContractVersion = "continuous_edge_v3";
    public const int EdgeSampleCount = 9;
    public const float CollarWidth = 256f;
    public const string LandmarkFeaturePrefix = "continuous:landmark:";
    private const float InwardDerivativeSample = 4f;
    // note: Limit the authored-to-continuation collar to a walkable grade so a noisy four-metre edge sample cannot become a kilometre-scale Hermite overshoot.
    private const float MaximumCollarGradeDegrees = 28f;
    private static readonly string[] OrderedBiomeIds = { "forest", "grassland", "wetland" };

    private readonly string worldSeed;
    // note: Cell records use the persisted semantic authority when a world plan is available; null keeps the legacy editor-only fallback path.
    private readonly GeneratedWorldPlanRecord semanticPlan;
    private readonly uint seedHash;
    private readonly Terrain originTerrain;
    private readonly YQGeneratedWorldTerrain.V2HeightSampler originSampler;
    private readonly YQPreparedSpatialMaterializationV2 acceptedMaterialization;
    private readonly AcceptedRouteBounds[] acceptedRouteBounds;
    private readonly float originMinX;
    private readonly float originMaxX;
    private readonly float originMinZ;
    private readonly float originMaxZ;
    private readonly float originY;
    private readonly float originHeight;
    // note: Snapshot the accepted Unity heightfield once on the main thread so collar interpolation can run against immutable data without Terrain.SampleHeight.
    private readonly float[,] originHeightSamples;
    private readonly int originHeightResolution;
    private readonly float cellSize;
    private readonly Vector2 macroOffset;
    private readonly Vector2 ridgeOffset;
    private readonly Vector2 biomeOffset;
    // note: Cache the conservative finite-water envelope once so distant terrain samples do not rescan every authored channel segment.
    private readonly bool hasAcceptedWaterBounds;
    private readonly float acceptedWaterMinX;
    private readonly float acceptedWaterMaxX;
    private readonly float acceptedWaterMinZ;
    private readonly float acceptedWaterMaxZ;
    // note: A malformed optional authored sample cannot strand an otherwise deterministic continuation tile; report the first repair and use the finite seeded relief for that sample.
    private bool reportedNonFiniteSample;
    // note: The generated origin terrain is centered at world zero, so cell indices are measured from its -512 metre corner.
    private const float WorldGridOrigin = YQContinuousWorldFeatureAuthority.WorldGridOrigin;

    public IReadOnlyList<string> BiomeIds => OrderedBiomeIds;

    public YQContinuousWorldCellAuthority(
        string seed,
        Terrain terrain,
        YQGeneratedWorldTerrain.V2HeightSampler sampler,
        GeneratedWorldPlanRecord plan = null,
        float streamedCellSize = 128f)
    {
        worldSeed = string.IsNullOrWhiteSpace(seed) ? "yourquest_default_world" : seed.Trim();
        semanticPlan = plan;
        if (semanticPlan != null)
            YQSemanticWorldAuthority.Ensure(semanticPlan);
        seedHash = StableHash(worldSeed + "|continuous-world-cell|v1");
        originTerrain = terrain;
        originSampler = sampler;
        cellSize = Mathf.Max(32f, streamedCellSize);
        if (plan != null)
        {
            YQPreparedSpatialMaterializationV2 prepared = null;
            YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out prepared, out _);
            acceptedMaterialization = prepared;
        }
        if (acceptedMaterialization != null && acceptedMaterialization.RouteCount > 0)
        {
            acceptedRouteBounds = new AcceptedRouteBounds[acceptedMaterialization.RouteCount];
            for (int routeIndex = 0; routeIndex < acceptedMaterialization.RouteCount; routeIndex++)
            {
                int pointCount = acceptedMaterialization.GetRoutePointCount(routeIndex);
                float minimumX = float.PositiveInfinity;
                float maximumX = float.NegativeInfinity;
                float minimumZ = float.PositiveInfinity;
                float maximumZ = float.NegativeInfinity;
                for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    YQSpatialMaterializationRoutePointV2 point = acceptedMaterialization.GetRoutePoint(routeIndex, pointIndex);
                    minimumX = Mathf.Min(minimumX, point.x);
                    maximumX = Mathf.Max(maximumX, point.x);
                    minimumZ = Mathf.Min(minimumZ, point.z);
                    maximumZ = Mathf.Max(maximumZ, point.z);
                }
                YQSpatialMaterializationRouteV2 route = acceptedMaterialization.GetRoute(routeIndex);
                float padding = Mathf.Max(2f, route.width * 0.5f + route.shoulderWidth);
                acceptedRouteBounds[routeIndex] = new AcceptedRouteBounds(
                    minimumX, maximumX, minimumZ, maximumZ, padding);
            }
        }
        float waterMinX = float.PositiveInfinity;
        float waterMaxX = float.NegativeInfinity;
        float waterMinZ = float.PositiveInfinity;
        float waterMaxZ = float.NegativeInfinity;
        float maximumWaterInfluence = 0f;
        if (acceptedMaterialization != null)
        {
            // note: Build a conservative envelope from accepted finite water points; terminal continuation rays remain evaluated separately outside it.
            for (int waterIndex = 0; waterIndex < acceptedMaterialization.WaterCount; waterIndex++)
            {
                YQSpatialMaterializationWaterV2 water = acceptedMaterialization.GetWater(waterIndex);
                maximumWaterInfluence = Mathf.Max(maximumWaterInfluence, water.nominalWidth);
                int pointCount = acceptedMaterialization.GetWaterPointCount(waterIndex);
                for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    YQSpatialMaterializationWaterPointV2 point = acceptedMaterialization.GetWaterPoint(waterIndex, pointIndex);
                    maximumWaterInfluence = Mathf.Max(maximumWaterInfluence, point.width);
                    waterMinX = Mathf.Min(waterMinX, point.x);
                    waterMaxX = Mathf.Max(waterMaxX, point.x);
                    waterMinZ = Mathf.Min(waterMinZ, point.z);
                    waterMaxZ = Mathf.Max(waterMaxZ, point.z);
                }
            }
        }
        hasAcceptedWaterBounds = !float.IsInfinity(waterMinX);
        // note: Include the shared heightmap-diagonal bank support used by the channel carve.
        float waterMargin = Mathf.Max(1f, maximumWaterInfluence) +
            YQGeneratedWorldTerrain.WorldSize / (YQGeneratedWorldTerrain.HeightmapResolution - 1f) * 1.414214f;
        acceptedWaterMinX = waterMinX - waterMargin;
        acceptedWaterMaxX = waterMaxX + waterMargin;
        acceptedWaterMinZ = waterMinZ - waterMargin;
        acceptedWaterMaxZ = waterMaxZ + waterMargin;
        Vector3 origin = terrain != null ? terrain.GetPosition() : new Vector3(-512f, 0f, -512f);
        Vector3 size = terrain != null && terrain.terrainData != null
            ? terrain.terrainData.size
            : new Vector3(1024f, YQGeneratedWorldTerrain.TerrainHeight, 1024f);
        originMinX = origin.x;
        originMaxX = origin.x + size.x;
        originMinZ = origin.z;
        originMaxZ = origin.z + size.z;
        originY = origin.y;
        originHeight = Mathf.Max(1f, size.y);
        if (terrain != null && terrain.terrainData != null)
        {
            originHeightResolution = terrain.terrainData.heightmapResolution;
            originHeightSamples = terrain.terrainData.GetHeights(0, 0, originHeightResolution, originHeightResolution);
        }
        macroOffset = Offset(seedHash, 0x31A7C9D1u);
        ridgeOffset = Offset(seedHash, 0x90B5F1E3u);
        biomeOffset = Offset(seedHash, 0xE42C7789u);
    }

    public float SampleHeightNormalized(float worldX, float worldZ)
    {
        return SampleHeightNormalizedCore(worldX, worldZ, true, null);
    }

    public float SampleHeightNormalizedOffMainThread(float worldX, float worldZ)
    {
        // note: This path uses only immutable accepted samples and pure deterministic math; it never accesses Terrain, TerrainData, or Debug.
        return SampleHeightNormalizedCore(worldX, worldZ, false, null);
    }

    internal HeightmapSamplingSession BeginHeightmapSamplingSession()
    {
        return new HeightmapSamplingSession(this);
    }

    private float SampleHeightNormalizedCore(
        float worldX,
        float worldZ,
        bool allowUnityApi,
        YQGeneratedWorldTerrain.V2HeightSampler.SpatialSamplingSession spatialSession)
    {
        if (ContainsOrigin(worldX, worldZ))
            return SampleOriginNormalized(worldX, worldZ, allowUnityApi, spatialSession);

        Vector2 nearest = new Vector2(
            Mathf.Clamp(worldX, originMinX, originMaxX),
            Mathf.Clamp(worldZ, originMinZ, originMaxZ));
        Vector2 outside = new Vector2(worldX, worldZ) - nearest;
        float distance = outside.magnitude;
        // note: Accepted V2 terrain fields own the near-origin continuation, while seeded macro relief takes over beyond their finite influence.
        float macro = SampleContinuationBaseNormalized(worldX, worldZ, spatialSession);
        if (distance <= 0.001f)
            return SanitizeHeightSample(worldX, worldZ, ApplyContinuationFeatureModifier(worldX, worldZ, macro), allowUnityApi);
        if (distance >= CollarWidth)
            return SanitizeHeightSample(worldX, worldZ, ApplyContinuationFeatureModifier(worldX, worldZ, macro), allowUnityApi);

        // note: A cubic Hermite collar matches both the final origin value and its one-sided outward slope before converging to the accepted V2 field or explicit legacy fallback.
        Vector2 outward = outside / distance;
        float baseValue = SampleOriginNormalized(nearest.x, nearest.y, allowUnityApi, spatialSession);
        Vector2 inwardPoint = nearest - outward * InwardDerivativeSample;
        if (!ContainsOrigin(inwardPoint.x, inwardPoint.y))
            inwardPoint = nearest + InwardDirection(nearest) * InwardDerivativeSample;
        float inwardValue = SampleOriginNormalized(inwardPoint.x, inwardPoint.y, allowUnityApi, spatialSession);
        float baseDerivative = (baseValue - inwardValue) / InwardDerivativeSample;
        float macroAtEnd = SampleContinuationBaseNormalized(
            nearest.x + outward.x * CollarWidth,
            nearest.y + outward.y * CollarWidth,
            spatialSession);
        float macroAfterEnd = SampleContinuationBaseNormalized(
            nearest.x + outward.x * (CollarWidth + InwardDerivativeSample),
            nearest.y + outward.y * (CollarWidth + InwardDerivativeSample),
            spatialSession);
        float macroDerivative = (macroAfterEnd - macroAtEnd) / InwardDerivativeSample;
        // note: Use a monotone Hermite collar; raw endpoint derivatives can be much steeper than the long collar and create clamped spike peaks.
        float collarDelta = macroAtEnd - baseValue;
        float maximumSlope = Mathf.Tan(MaximumCollarGradeDegrees * Mathf.Deg2Rad) / Mathf.Max(1f, originHeight);
        baseDerivative = LimitCollarDerivative(baseDerivative, collarDelta, CollarWidth, maximumSlope);
        macroDerivative = LimitCollarDerivative(macroDerivative, collarDelta, CollarWidth, maximumSlope);
        float secant = collarDelta / CollarWidth;
        if (Mathf.Abs(secant) > 0.000001f)
        {
            float alpha = baseDerivative / secant;
            float beta = macroDerivative / secant;
            float derivativeMagnitude = alpha * alpha + beta * beta;
            if (derivativeMagnitude > 9f)
            {
                float scale = 3f / Mathf.Sqrt(derivativeMagnitude);
                baseDerivative *= scale;
                macroDerivative *= scale;
            }
        }
        float t = Mathf.Clamp01(distance / CollarWidth);
        float t2 = t * t;
        float t3 = t2 * t;
        float h00 = 2f * t3 - 3f * t2 + 1f;
        float h10 = t3 - 2f * t2 + t;
        float h01 = -2f * t3 + 3f * t2;
        float h11 = t3 - t2;
        float collarHeight = h00 * baseValue +
            h10 * CollarWidth * baseDerivative +
            h01 * macroAtEnd +
            h11 * CollarWidth * macroDerivative;
        return SanitizeHeightSample(worldX, worldZ, ApplyContinuationFeatureModifier(
            worldX,
            worldZ,
            Mathf.Clamp01(collarHeight)), allowUnityApi);
    }

    private static float LimitCollarDerivative(
        float derivative,
        float endpointDelta,
        float collarLength,
        float maximumSlope)
    {
        float secant = endpointDelta / Mathf.Max(0.001f, collarLength);
        if (Mathf.Abs(secant) <= 0.000001f || Mathf.Sign(derivative) != Mathf.Sign(secant))
            return 0f;
        float magnitude = Mathf.Min(Mathf.Abs(derivative), Mathf.Abs(secant) * 3f, maximumSlope);
        return Mathf.Sign(secant) * magnitude;
    }

    public bool CanSampleRectangleOffMainThread(float minimumX, float maximumX, float minimumZ, float maximumZ)
    {
        // note: A captured authored heightfield makes every continuation rectangle pure; the geometric fallback covers legacy authorities without a snapshot.
        if (originHeightSamples != null || originSampler != null)
            return true;
        float deltaX = maximumX < originMinX
            ? originMinX - maximumX
            : minimumX > originMaxX ? minimumX - originMaxX : 0f;
        float deltaZ = maximumZ < originMinZ
            ? originMinZ - maximumZ
            : minimumZ > originMaxZ ? minimumZ - originMaxZ : 0f;
        return deltaX * deltaX + deltaZ * deltaZ >= CollarWidth * CollarWidth;
    }

    private float SanitizeHeightSample(float worldX, float worldZ, float sample, bool allowUnityApi)
    {
        if (!float.IsNaN(sample) && !float.IsInfinity(sample))
            return Mathf.Clamp01(sample);

        // note: The fallback is the same absolute-coordinate macro field used by every terminal continuation cell, preserving deterministic terrain instead of returning a flat or missing tile.
        float fallback = SampleMacroHeightNormalized(worldX, worldZ);
        if (float.IsNaN(fallback) || float.IsInfinity(fallback))
            fallback = 0.35f;
        if (allowUnityApi && !reportedNonFiniteSample)
        {
            reportedNonFiniteSample = true;
            Debug.LogWarning("[YQContinuousWorldCellAuthority] Repaired a non-finite accepted continuation sample at " + worldX.ToString("0.0") + "," + worldZ.ToString("0.0") + ".");
        }
        return Mathf.Clamp01(fallback);
    }

    private float SampleContinuationBaseNormalized(
        float worldX,
        float worldZ,
        YQGeneratedWorldTerrain.V2HeightSampler.SpatialSamplingSession spatialSession)
    {
        float generatedRelief = SampleMacroHeightNormalized(worldX, worldZ);
        if (originSampler == null)
            return generatedRelief;

        // note: Fade accepted V2 terrain into seeded continuation relief, and skip the costly accepted graph once its blend weight is exactly zero.
        Vector2 nearest = new Vector2(
            Mathf.Clamp(worldX, originMinX, originMaxX),
            Mathf.Clamp(worldZ, originMinZ, originMaxZ));
        float outsideDistance = Vector2.Distance(new Vector2(worldX, worldZ), nearest);
        float generatedWeight = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(CollarWidth * 0.5f, CollarWidth * 2.5f, outsideDistance));
        if (generatedWeight >= 1f)
            return generatedRelief;

        float acceptedRelief = spatialSession != null
            ? spatialSession.SampleNormalized(worldX, worldZ)
            : originSampler.SampleNormalized(worldX, worldZ);
        return Mathf.Lerp(acceptedRelief, generatedRelief, generatedWeight);
    }

    private float ApplyContinuationFeatureModifier(float worldX, float worldZ, float normalizedHeight)
    {
        // note: V2 finite waterways are already present in the accepted sampler; only terminal continuation needs an additional carve.
        if (originSampler != null)
        {
            // note: Continuation terrain and accepted route ribbons must share the same grade, otherwise the authored path can float above or disappear below the streamed heightfield.
            normalizedHeight = ApplyAcceptedRouteModifier(worldX, worldZ, normalizedHeight);
            if (acceptedMaterialization == null)
                return YQContinuousWorldFeatureAuthority.ApplyTerrainModifiers(
                    worldSeed, worldX, worldZ, normalizedHeight, originHeight);
            float acceptedHeight = normalizedHeight;
            bool insideFiniteWaterEnvelope = hasAcceptedWaterBounds &&
                worldX >= acceptedWaterMinX && worldX <= acceptedWaterMaxX &&
                worldZ >= acceptedWaterMinZ && worldZ <= acceptedWaterMaxZ;
            bool acceptedInfluence = insideFiniteWaterEnvelope &&
                YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterModifier(
                    acceptedMaterialization,
                    worldX,
                    worldZ,
                    acceptedHeight,
                    originHeight,
                    out acceptedHeight);
            float terminalHeight = normalizedHeight;
            acceptedInfluence |= YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterTerminalModifiers(
                acceptedMaterialization,
                worldX,
                worldZ,
                terminalHeight,
                originHeight,
                out terminalHeight);
            if (!acceptedInfluence)
            {
                // note: An accepted semantic plan owns the complete baseline; do not resurrect the legacy synthetic river outside its accepted feature graph.
                return normalizedHeight;
            }
            // note: Accepted finite and terminal hydrology are compared from one immutable base so overlapping features remain order-independent.
            return Mathf.Min(acceptedHeight, terminalHeight);
        }
        return ApplyFeatureModifier(worldX, worldZ, normalizedHeight);
    }

    private float ApplyAcceptedRouteModifier(
        float worldX,
        float worldZ,
        float normalizedHeight)
    {
        if (acceptedMaterialization == null || acceptedMaterialization.RouteCount <= 0)
            return normalizedHeight;

        float bestMask = 0f;
        float bestElevation = normalizedHeight;
        Vector2 sample = new Vector2(worldX, worldZ);
        for (int routeIndex = 0; routeIndex < acceptedMaterialization.RouteCount; routeIndex++)
        {
            YQSpatialMaterializationRouteV2 route = acceptedMaterialization.GetRoute(routeIndex);
            // note: The envelope is conservative, so this only removes impossible route candidates and cannot change the winning accepted segment.
            if (acceptedRouteBounds != null && routeIndex < acceptedRouteBounds.Length)
            {
                AcceptedRouteBounds bounds = acceptedRouteBounds[routeIndex];
                if (sample.x < bounds.minX - bounds.padding || sample.x > bounds.maxX + bounds.padding ||
                    sample.y < bounds.minZ - bounds.padding || sample.y > bounds.maxZ + bounds.padding)
                    continue;
            }
            int pointCount = acceptedMaterialization.GetRoutePointCount(routeIndex);
            if (pointCount < 2)
                continue;

            float routeDistance = float.MaxValue;
            float routeElevation = normalizedHeight;
            for (int pointIndex = 0; pointIndex + 1 < pointCount; pointIndex++)
            {
                YQSpatialMaterializationRoutePointV2 first =
                    acceptedMaterialization.GetRoutePoint(routeIndex, pointIndex);
                YQSpatialMaterializationRoutePointV2 second =
                    acceptedMaterialization.GetRoutePoint(routeIndex, pointIndex + 1);
                Vector2 start = new Vector2(first.x, first.z);
                Vector2 delta = new Vector2(second.x - first.x, second.z - first.z);
                float denominator = delta.sqrMagnitude;
                float t = denominator > 0.0001f
                    ? Mathf.Clamp01(Vector2.Dot(sample - start, delta) / denominator)
                    : 0f;
                Vector2 nearest = start + delta * t;
                float distance = Vector2.Distance(sample, nearest);
                if (distance >= routeDistance)
                    continue;
                routeDistance = distance;
                routeElevation = Mathf.Lerp(
                    first.surfaceElevationNormalized,
                    second.surfaceElevationNormalized,
                    t);
            }

            float radius = Mathf.Max(2f, route.width * 0.5f + route.shoulderWidth);
            float mask = 1f - Mathf.SmoothStep(0f, 1f, routeDistance / radius);
            if (mask > bestMask)
            {
                bestMask = mask;
                bestElevation = routeElevation;
            }
        }

        return Mathf.Lerp(normalizedHeight, bestElevation, bestMask * 0.96f);
    }

    public List<float> SampleBiomeWeights(float worldX, float worldZ)
    {
        // note: All cells use the same absolute-coordinate scalar fields, so biome influence cannot change when exploration order changes.
        SampleBiomeWeightsValues(worldX, worldZ, out float forest, out float grassland, out float wetland);
        return new List<float>
        {
            forest,
            grassland,
            wetland
        };
    }

    public bool TryGetOriginAlphaWeights(float worldX, float worldZ, out float[] weights)
    {
        weights = null;
        if (originTerrain == null || originTerrain.terrainData == null)
            return false;
        // note: Only expose the finite authored outer border so continuation tiles cannot inherit a clamped texel along an infinite boundary extension.
        bool onBoundary = Mathf.Abs(worldX - originMinX) < 0.01f || Mathf.Abs(worldX - originMaxX) < 0.01f ||
            Mathf.Abs(worldZ - originMinZ) < 0.01f || Mathf.Abs(worldZ - originMaxZ) < 0.01f;
        bool withinAuthoredX = worldX >= originMinX - 0.01f && worldX <= originMaxX + 0.01f;
        bool withinAuthoredZ = worldZ >= originMinZ - 0.01f && worldZ <= originMaxZ + 0.01f;
        // note: Require the coordinate along the edge to remain inside the authored terrain before copying its palette texel.
        if (!onBoundary || !withinAuthoredX || !withinAuthoredZ)
            return false;
        int resolution = Mathf.Max(1, originTerrain.terrainData.alphamapResolution);
        float normalizedX = Mathf.Clamp01((worldX - originMinX) / Mathf.Max(1f, originMaxX - originMinX));
        float normalizedZ = Mathf.Clamp01((worldZ - originMinZ) / Mathf.Max(1f, originMaxZ - originMinZ));
        int sampleX = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (resolution - 1)), 0, resolution - 1);
        int sampleZ = Mathf.Clamp(Mathf.RoundToInt(normalizedZ * (resolution - 1)), 0, resolution - 1);
        float[,,] sample = originTerrain.terrainData.GetAlphamaps(sampleX, sampleZ, 1, 1);
        weights = new float[sample.GetLength(2)];
        for (int layer = 0; layer < weights.Length; layer++)
            weights[layer] = sample[0, 0, layer];
        return true;
    }

    public void SampleBiomeWeightsValues(float worldX, float worldZ, out float forest, out float grassland, out float wetland)
    {
        if (semanticPlan != null)
        {
            // note: The semantic climate/ecology fields are shared with unloaded queries, so streamed alphamaps cannot invent a second biome authority.
            YQSemanticWorldAuthority.SampleMacroFields(
                worldSeed,
                worldX,
                worldZ,
                out _,
                out _,
                out _,
                out _,
                out forest,
                out grassland,
                out wetland,
                out _,
                out _,
                out _);
            return;
        }
        // note: Use a medium-scale macro frequency so distinct biome palettes emerge across streamed regions while eight-metre blends remain gradual.
        float rawForest = Mathf.PerlinNoise(worldX * 0.0036f + biomeOffset.x, worldZ * 0.0036f + biomeOffset.y);
        float rawGrassland = Mathf.PerlinNoise(worldX * 0.0036f + biomeOffset.y + 37.2f, worldZ * 0.0036f + biomeOffset.x - 19.7f);
        float rawWetland = Mathf.PerlinNoise(worldX * 0.0024f + biomeOffset.x - 71.4f, worldZ * 0.0024f + biomeOffset.y + 43.6f);
        float total = Mathf.Max(0.001f, rawForest + rawGrassland + rawWetland);
        forest = rawForest / total;
        grassland = rawGrassland / total;
        wetland = rawWetland / total;
    }

    public string ResolveDominantBiome(float worldX, float worldZ)
    {
        List<float> weights = SampleBiomeWeights(worldX, worldZ);
        int best = 0;
        for (int index = 1; index < weights.Count; index++)
            if (weights[index] > weights[best])
                best = index;
        return OrderedBiomeIds[best];
    }

    public void PopulateCellRecord(GeneratedSemanticChunkRecord record, Vector2Int coordinate, float chunkSize, GeneratedWorldPlanRecord plan = null)
    {
        if (record == null)
            return;
        // note: Reused save records must be normalized before deterministic continuation metadata is refreshed.
        record.EnsureCollections();
        float safeSize = Mathf.Max(32f, chunkSize);
        float centerX = WorldGridOrigin + coordinate.x * safeSize + safeSize * 0.5f;
        float centerZ = WorldGridOrigin + coordinate.y * safeSize + safeSize * 0.5f;
        record.continuationSchemaVersion = SchemaVersion;
        // note: Reuse one scalar-field sample for both persisted weights and dominant-biome selection so frontier admission does not recompute or allocate the same data.
        SampleBiomeWeightsValues(centerX, centerZ, out float forest, out float grassland, out float wetland);
        record.biomeWeights = new List<float> { forest, grassland, wetland };
        record.biome = forest >= grassland && forest >= wetland
            ? OrderedBiomeIds[0]
            : grassland >= wetland
                ? OrderedBiomeIds[1]
                : OrderedBiomeIds[2];
        record.edgeContracts = new List<GeneratedSemanticChunkEdgeContractRecord>
        {
            BuildEdgeContract(coordinate, "west", safeSize),
            BuildEdgeContract(coordinate, "east", safeSize),
            BuildEdgeContract(coordinate, "south", safeSize),
            BuildEdgeContract(coordinate, "north", safeSize)
        };
        // note: Portal records are derived from the same world-space splines as streamed feature meshes, so neighboring cells share one connector identity.
        // note: Accepted V2 routes/waterways and the deterministic continuation share one portal builder, so the persisted edge contract describes the mesh that will actually be published.
        // note: Reuse this authority's validated immutable projection, as height sampling already does; do not serialize and hash the full world ten times per new cell.
        YQContinuousWorldFeatureAuthority.PopulatePreparedPortals(
            record,
            coordinate,
            safeSize,
            worldSeed,
            acceptedMaterialization,
            semanticPlan == null);
        if (plan != null)
        {
            // note: The global authority decides feature/site existence once; a cell that looks empty cannot synthesize a competing landmark or road.
            // note: Streamed cells use the same public semantic query boundary as unloaded callers, preventing a second authority path.
            GeneratedSemanticCellPlanRecord semanticCell = YQGeneratedWorldQuery.GetSemanticCellPlan(plan, coordinate);
            record.parentRegionId = semanticCell.ownerRegionId;
            record.featureIds.RemoveAll(feature => feature != null && feature.StartsWith("continuous:landmark:", StringComparison.Ordinal));
            for (int featureIndex = 0; featureIndex < semanticCell.featureIds.Count; featureIndex++)
                AddUnique(record.featureIds, semanticCell.featureIds[featureIndex]);
            // note: Carry accepted site, route, and water identities into the streamed record; materializers and readiness demand use this one cell-local identity index.
            for (int siteIndex = 0; siteIndex < semanticCell.siteIds.Count; siteIndex++)
                AddUnique(record.featureIds, semanticCell.siteIds[siteIndex]);
            for (int routeIndex = 0; routeIndex < semanticCell.routeIds.Count; routeIndex++)
                AddUnique(record.featureIds, semanticCell.routeIds[routeIndex]);
            for (int waterIndex = 0; waterIndex < semanticCell.waterIds.Count; waterIndex++)
                AddUnique(record.featureIds, semanticCell.waterIds[waterIndex]);
            List<GeneratedSemanticSiteReservationRecord> reservations =
                YQGeneratedWorldQuery.GetSemanticSites(plan, coordinate);
            for (int siteIndex = 0; siteIndex < reservations.Count; siteIndex++)
            {
                GeneratedSemanticSiteReservationRecord reservation = reservations[siteIndex];
                if (reservation == null)
                    continue;
                GeneratedSemanticSiteRecord site = null;
                for (int existingIndex = 0; existingIndex < record.sites.Count; existingIndex++)
                {
                    if (record.sites[existingIndex] != null && string.Equals(record.sites[existingIndex].siteId, reservation.siteId, StringComparison.Ordinal))
                    {
                        site = record.sites[existingIndex];
                        break;
                    }
                }
                if (site == null)
                {
                    site = new GeneratedSemanticSiteRecord();
                    record.sites.Add(site);
                }
                site.siteId = reservation.siteId;
                site.semanticKind = reservation.siteKind;
                site.assetSlot = string.IsNullOrWhiteSpace(site.assetSlot) ? YQWorldAssetCatalog.SlotLargeStructure : site.assetSlot;
                site.biome = record.biome;
                site.lifecycleState = string.IsNullOrWhiteSpace(site.lifecycleState)
                    ? YQSemanticChunkLifecycle.SemanticallyPlanned.ToString()
                    : site.lifecycleState;
                site.worldX = reservation.worldX;
                site.worldZ = reservation.worldZ;
                site.ownerRegionId = reservation.ownerRegionId;
                site.ownerFeatureId = reservation.ownerFeatureId;
                // note: Copy the accepted footprint into each member sector while marking exactly one sector as the physical site owner.
                site.ownerCellId = reservation.ownerCellId;
                site.memberCellIds = reservation.memberCellIds != null
                    ? new List<string>(reservation.memberCellIds)
                    : new List<string>();
                site.isOwnerCell = YQSemanticWorldAuthority.IsOwnerCell(
                    reservation,
                    coordinate);
                // note: Keep accepted presentation and layout identity on the streamed site projection for unload/reload continuity.
                site.displayName = reservation.displayName;
                site.layoutBindingId = reservation.layoutBindingId;
                site.layoutBindingVersion = reservation.layoutBindingVersion;
                site.layoutSignature = reservation.layoutSignature;
                site.accessScore = reservation.accessScore;
                site.accessConstraint = reservation.accessConstraint;
                site.footprintRadius = reservation.footprintRadius;
                site.entranceId = reservation.entrances.Count > 0 ? reservation.entrances[0].entranceId : string.Empty;
                site.entranceX = reservation.entrances.Count > 0 ? reservation.entrances[0].worldX : reservation.worldX;
                site.entranceZ = reservation.entrances.Count > 0 ? reservation.entrances[0].worldZ : reservation.worldZ;
                site.immutableReservation = reservation.immutable;
                site.provenance = reservation.provenance;
            }
            record.sites.RemoveAll(site => site == null);
        }
        else
        {
            string landmarkId = BuildLandmarkFeatureId(coordinate);
            // note: The no-plan path is retained only for legacy editor callers; production planned worlds use the semantic authority above.
            record.featureIds.Remove(landmarkId);
            record.sites.RemoveAll(site => site == null);
            if (!ContainsOrigin(centerX, centerZ) && ShouldPlanLandmark(coordinate, worldSeed))
            {
                AddUnique(record.featureIds, landmarkId);
                GetLandmarkPosition(coordinate, safeSize, worldSeed, out float landmarkX, out float landmarkZ);
                record.sites.Add(new GeneratedSemanticSiteRecord
                {
                    siteId = landmarkId,
                    semanticKind = "landmark",
                    assetSlot = YQWorldAssetCatalog.SlotLargeStructure,
                    biome = record.biome,
                    lifecycleState = YQSemanticChunkLifecycle.SemanticallyPlanned.ToString(),
                    worldX = landmarkX,
                    worldZ = landmarkZ,
                    immutableReservation = false,
                    provenance = "legacy_editor_fallback"
                });
            }
        }
        record.seamFingerprint = BuildCellFingerprint(record.edgeContracts);
    }

    public static string BuildLandmarkFeatureId(Vector2Int coordinate)
    {
        return LandmarkFeaturePrefix + coordinate.x + ":" + coordinate.y;
    }

    public static bool ShouldPlanLandmark(Vector2Int coordinate, string seed)
    {
        // note: One deterministic anchor per five-cell region guarantees meaningful POI distribution; the hash adds varied secondary landmarks without making every cell a site.
        bool regionalAnchor = PositiveModulo(coordinate.x, 5) == 2 && PositiveModulo(coordinate.y, 5) == 2;
        return regionalAnchor || YQContinuousWorldFeatureAuthority.Hash01((seed ?? string.Empty) + "|continuous-landmark|" + coordinate.x + "|" + coordinate.y) < 0.18f;
    }

    public static void GetLandmarkPosition(Vector2Int coordinate, float cellSize, string seed, out float worldX, out float worldZ)
    {
        // note: Persist the deterministic semantic site position so save/reload never moves a generated landmark after its first accepted placement.
        float centerX = WorldGridOrigin + coordinate.x * cellSize + cellSize * 0.5f;
        float centerZ = WorldGridOrigin + coordinate.y * cellSize + cellSize * 0.5f;
        string positionSeed = (seed ?? string.Empty) + "|" + BuildLandmarkFeatureId(coordinate);
        worldX = centerX + Mathf.Sin(YQContinuousWorldFeatureAuthority.Hash01(positionSeed + "|x") * Mathf.PI * 2f) * cellSize * 0.22f;
        worldZ = centerZ + Mathf.Cos(YQContinuousWorldFeatureAuthority.Hash01(positionSeed + "|z") * Mathf.PI * 2f) * cellSize * 0.22f;
    }

    private static int PositiveModulo(int value, int modulus)
    {
        int remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value) || values.Contains(value))
            return;
        values.Add(value);
    }

    private GeneratedSemanticChunkEdgeContractRecord BuildEdgeContract(Vector2Int coordinate, string edge, float size)
    {
        float boundary = string.Equals(edge, "west", StringComparison.OrdinalIgnoreCase) || string.Equals(edge, "east", StringComparison.OrdinalIgnoreCase)
            ? (string.Equals(edge, "west", StringComparison.OrdinalIgnoreCase) ? WorldGridOrigin + coordinate.x * size : WorldGridOrigin + (coordinate.x + 1) * size)
            : (string.Equals(edge, "south", StringComparison.OrdinalIgnoreCase) ? WorldGridOrigin + coordinate.y * size : WorldGridOrigin + (coordinate.y + 1) * size);
        bool vertical = edge == "west" || edge == "east";
        float spanStart = vertical ? WorldGridOrigin + coordinate.y * size : WorldGridOrigin + coordinate.x * size;
        float spanStep = size / (EdgeSampleCount - 1f);
        GeneratedSemanticChunkEdgeContractRecord contract = new GeneratedSemanticChunkEdgeContractRecord
        {
            edge = edge,
            canonicalKey = BuildCanonicalEdgeKey(coordinate, edge, size),
            contractVersion = EdgeContractVersion,
            sampleCount = EdgeSampleCount
        };
        List<float> accumulatedWeights = new List<float> { 0f, 0f, 0f };
        for (int index = 0; index < EdgeSampleCount; index++)
        {
            float span = spanStart + spanStep * index;
            float worldX = vertical ? boundary : span;
            float worldZ = vertical ? span : boundary;
            contract.terrainHeights.Add(SampleHeightNormalized(worldX, worldZ));
            List<float> weights = SampleBiomeWeights(worldX, worldZ);
            for (int weightIndex = 0; weightIndex < accumulatedWeights.Count; weightIndex++)
                accumulatedWeights[weightIndex] += weights[weightIndex];
        }
        for (int index = 0; index < accumulatedWeights.Count; index++)
            contract.biomeWeights.Add(accumulatedWeights[index] / EdgeSampleCount);
        contract.contractFingerprint = Fingerprint(contract);
        return contract;
    }

    private string BuildCanonicalEdgeKey(Vector2Int coordinate, string edge, float size)
    {
        bool vertical = edge == "west" || edge == "east";
        int boundaryIndex = vertical
            ? (edge == "west" ? coordinate.x : coordinate.x + 1)
            : (edge == "south" ? coordinate.y : coordinate.y + 1);
        int spanIndex = vertical ? coordinate.y : coordinate.x;
        string axis = vertical ? "x" : "z";
        return worldSeed + "|" + SchemaVersion + "|" + axis + "|" + boundaryIndex + "|span|" + spanIndex + "|size|" + size.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private string BuildCellFingerprint(List<GeneratedSemanticChunkEdgeContractRecord> contracts)
    {
        StringBuilder builder = new StringBuilder(worldSeed).Append('|').Append(SchemaVersion);
        if (contracts != null)
        for (int index = 0; index < contracts.Count; index++)
        {
            GeneratedSemanticChunkEdgeContractRecord contract = contracts[index];
            builder.Append('|').Append(contract?.contractFingerprint ?? string.Empty);
            if (contract?.routePortals != null)
                for (int portalIndex = 0; portalIndex < contract.routePortals.Count; portalIndex++)
                    AppendPortal(builder, contract.routePortals[portalIndex]);
            if (contract?.waterPortals != null)
                for (int portalIndex = 0; portalIndex < contract.waterPortals.Count; portalIndex++)
                    AppendPortal(builder, contract.waterPortals[portalIndex]);
        }
        return StableHash(builder.ToString()).ToString("X8");
    }

    private static void AppendPortal(StringBuilder builder, GeneratedSemanticChunkPortalRecord portal)
    {
        if (portal == null)
            return;
        builder.Append('|').Append(portal.portalId).Append('|').Append(portal.featureId)
            .Append('|').Append(portal.edge).Append('|').Append(portal.worldX.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(portal.worldZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(portal.width.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(portal.surfaceElevation.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(portal.bedElevation.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string Fingerprint(GeneratedSemanticChunkEdgeContractRecord contract)
    {
        StringBuilder builder = new StringBuilder(contract.canonicalKey).Append('|').Append(contract.contractVersion);
        for (int index = 0; index < contract.terrainHeights.Count; index++)
            builder.Append('|').Append(contract.terrainHeights[index].ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        for (int index = 0; index < contract.biomeWeights.Count; index++)
            builder.Append('|').Append(contract.biomeWeights[index].ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        return StableHash(builder.ToString()).ToString("X8");
    }

    private float SampleOriginNormalized(
        float worldX,
        float worldZ,
        bool allowUnityApi,
        YQGeneratedWorldTerrain.V2HeightSampler.SpatialSamplingSession spatialSession)
    {
        if (allowUnityApi && originTerrain != null && originTerrain.terrainData != null)
            return Mathf.InverseLerp(originY, originY + originHeight, originTerrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + originY);
        if (originHeightSamples != null && originHeightResolution > 1)
        {
            // note: Bilinear lookup mirrors Terrain.SampleHeight over the captured normalized grid while remaining background-safe.
            float sampleX = Mathf.Clamp01((worldX - originMinX) / Mathf.Max(0.001f, originMaxX - originMinX)) * (originHeightResolution - 1);
            float sampleZ = Mathf.Clamp01((worldZ - originMinZ) / Mathf.Max(0.001f, originMaxZ - originMinZ)) * (originHeightResolution - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(sampleX), 0, originHeightResolution - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(sampleZ), 0, originHeightResolution - 1);
            int x1 = Mathf.Min(x0 + 1, originHeightResolution - 1);
            int z1 = Mathf.Min(z0 + 1, originHeightResolution - 1);
            float tx = sampleX - x0;
            float tz = sampleZ - z0;
            float lower = Mathf.Lerp(originHeightSamples[z0, x0], originHeightSamples[z0, x1], tx);
            float upper = Mathf.Lerp(originHeightSamples[z1, x0], originHeightSamples[z1, x1], tx);
            return Mathf.Lerp(lower, upper, tz);
        }
        if (originSampler != null)
            return spatialSession != null
                ? spatialSession.SampleNormalized(worldX, worldZ)
                : originSampler.SampleNormalized(worldX, worldZ);
        throw new InvalidOperationException("continuous world authority has no origin terrain or accepted sampler");
    }

    private float ApplyFeatureModifier(float worldX, float worldZ, float normalizedHeight)
    {
        // note: Accepted finite and terminal waterways are evaluated from global coordinates with no exploration-grown cell cache.
        if (acceptedMaterialization != null && acceptedMaterialization.WaterCount > 0)
        {
            float acceptedHeight = normalizedHeight;
            YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterModifier(
                acceptedMaterialization,
                worldX,
                worldZ,
                acceptedHeight,
                originHeight,
                out acceptedHeight);
            float terminalHeight = normalizedHeight;
            YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterTerminalModifiers(
                acceptedMaterialization,
                worldX,
                worldZ,
                terminalHeight,
                originHeight,
                out terminalHeight);
            // note: Finite and terminal candidates both start from the unmodified sample; the lower result is the shared order-independent carve.
            return Mathf.Min(acceptedHeight, terminalHeight);
        }
        // note: Legacy synthetic hydrology remains the deterministic fallback when no accepted water sample owns this coordinate.
        return YQContinuousWorldFeatureAuthority.ApplyTerrainModifiers(
            worldSeed,
            worldX,
            worldZ,
            normalizedHeight,
            originHeight);
    }

    private static float ApplyAcceptedWaterContinuationModifier(
        YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation continuation,
        float worldX,
        float worldZ,
        float normalizedHeight,
        float terrainHeight)
    {
        // note: Project the sample onto the shared terminal ray and carve only its bounded channel footprint with the accepted surface/depth contract.
        Vector2 offset = new Vector2(worldX, worldZ) - continuation.origin;
        float along = Vector2.Dot(offset, continuation.direction);
        // note: Accepted terminal water carving follows the shared versioned continuation envelope instead of stopping at the former 4.096 km legacy cap.
        if (along < -0.01f || along > YQContinuousWorldFeatureAuthority.AcceptedContinuationMaxDistance)
            return normalizedHeight;
        float distance = Mathf.Abs(continuation.direction.x * offset.y - continuation.direction.y * offset.x);
        float halfWidth = Mathf.Max(1f, continuation.width * 0.5f);
        if (distance >= halfWidth * 2f)
            return normalizedHeight;
        float falloff = 1f - Mathf.Clamp01(distance / (halfWidth * 2f));
        float targetBed = Mathf.Clamp01(continuation.elevation + continuation.elevationSlope * along) * terrainHeight - Mathf.Max(0.1f, continuation.depth);
        float currentHeight = normalizedHeight * terrainHeight;
        float blendedNormalized = Mathf.Lerp(normalizedHeight, targetBed / Mathf.Max(1f, terrainHeight), falloff);
        return Mathf.Clamp01(Mathf.Min(normalizedHeight, blendedNormalized));
    }

    private float SampleMacroHeightNormalized(float worldX, float worldZ)
    {
        float continental = Mathf.PerlinNoise(worldX * 0.0018f + macroOffset.x, worldZ * 0.0018f + macroOffset.y);
        float hills = Mathf.PerlinNoise(worldX * 0.0065f + macroOffset.y + 11.3f, worldZ * 0.0065f + macroOffset.x - 7.1f);
        float ridgeNoise = Mathf.PerlinNoise(worldX * 0.0032f + ridgeOffset.x, worldZ * 0.0032f + ridgeOffset.y);
        float ridges = 1f - Mathf.Abs(ridgeNoise * 2f - 1f);
        float mountainNoise = Mathf.PerlinNoise(worldX * 0.0011f + ridgeOffset.y + 17.7f, worldZ * 0.0011f + ridgeOffset.x - 9.4f);
        float mountainMask = Mathf.SmoothStep(0.48f, 0.82f, mountainNoise);
        float mountainRidges = Mathf.Pow(1f - Mathf.Abs(Mathf.PerlinNoise(worldX * 0.0024f + macroOffset.x - 3.1f, worldZ * 0.0024f + macroOffset.y + 8.6f) * 2f - 1f), 1.35f);
        float basins = Mathf.PerlinNoise(worldX * 0.0015f + biomeOffset.x + 5.2f, worldZ * 0.0015f + biomeOffset.y - 4.8f);
        float relief = mountainMask * mountainRidges * 0.24f - (1f - mountainMask) * (1f - basins) * 0.06f;
        return Mathf.Clamp01(0.20f + continental * 0.18f + hills * 0.09f + ridges * 0.12f + relief);
    }

    private bool ContainsOrigin(float worldX, float worldZ)
    {
        return worldX >= originMinX && worldX <= originMaxX && worldZ >= originMinZ && worldZ <= originMaxZ;
    }

    private Vector2 InwardDirection(Vector2 boundary)
    {
        Vector2 centre = new Vector2((originMinX + originMaxX) * 0.5f, (originMinZ + originMaxZ) * 0.5f);
        Vector2 direction = centre - boundary;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
    }

    private static Vector2 Offset(uint seed, uint salt)
    {
        return new Vector2(
            (StableHash(seed + salt) & 0xFFFFu) / 65535f * 2000f - 1000f,
            ((StableHash(seed ^ (salt * 16777619u)) >> 8) & 0xFFFFu) / 65535f * 2000f - 1000f);
    }

    private static uint StableHash(string value)
    {
        uint hash = 2166136261u;
        if (value != null)
            for (int index = 0; index < value.Length; index++)
                hash = (hash ^ value[index]) * 16777619u;
        return hash;
    }

    private static uint StableHash(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        return value ^ (value >> 16);
    }
}

