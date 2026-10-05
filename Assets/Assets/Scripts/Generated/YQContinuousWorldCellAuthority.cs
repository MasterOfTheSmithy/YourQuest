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
    // note: Frontier planning is a bounded dry-ground proposal policy; it does not replace crossing acceptance or publish generated content.
    private const int FrontierPlanningSampleBudget = 8192;
    private const float FrontierPadShoulder = 24f;
    private const float FrontierOpeningCollarRadius = 1152f;
    private const float FrontierRouteWidth = 6f;
    private const float FrontierRouteShoulder = 12f;
    private const float FrontierMaximumEarthwork = 12f;
    private static readonly string[] OrderedBiomeIds = { "forest", "grassland", "wetland" };

    private readonly string worldSeed;
    // note: Cell records use the persisted semantic authority when a world plan is available; null keeps the legacy editor-only fallback path.
    private readonly GeneratedWorldPlanRecord semanticPlan;
    private readonly uint seedHash;
    private readonly Terrain originTerrain;
    private readonly YQGeneratedWorldTerrain.V2HeightSampler originSampler;
    private readonly YQPreparedSpatialMaterializationV2 acceptedMaterialization;
    private readonly bool pureAcceptedProjection;
    private readonly ContinuationPad[] continuationPads;
    private readonly Dictionary<Vector2Int, ContinuationPad[]> continuationPadIndex;
    private readonly ContinuationRoute[] candidateRoutes;
    private readonly HashSet<string> candidateRouteIds;
    private readonly int[] continuationRouteProjectionOrder;
    private readonly struct ContinuationPad
    {
        internal readonly float x, z, radius, elevation, shoulder;
        internal ContinuationPad(float x, float z, float radius, float elevation, float shoulder)
        { this.x = x; this.z = z; this.radius = radius; this.elevation = elevation; this.shoulder = shoulder; }
    }
    private sealed class ContinuationRoute
    {
        internal string id;
        internal float width, shoulder, maximumGrade;
        internal Vector3[] points;
    }
    private readonly struct FrontierSector
    {
        internal readonly string id;
        internal readonly float x, z, radius;
        internal FrontierSector(string id, float x, float z, float radius)
        { this.id = id; this.x = x; this.z = z; this.radius = radius; }
    }
    private readonly struct FrontierFrontage
    {
        internal readonly FrontierSector sector;
        internal readonly GeneratedSemanticEntranceRecord entrance;
        internal readonly string routeId;
        internal FrontierFrontage(FrontierSector sector, GeneratedSemanticEntranceRecord entrance, string routeId)
        { this.sector = sector; this.entrance = entrance; this.routeId = routeId; }
    }
    private sealed class FrontierPlanningBudget
    {
        internal int remaining = FrontierPlanningSampleBudget;
        internal bool exhausted;
        internal bool TryConsume()
        {
            if (remaining-- > 0) return true;
            exhausted = true;
            return false;
        }
    }
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
        : this(seed, terrain, sampler, plan, streamedCellSize, null, null, false)
    { }

    private YQContinuousWorldCellAuthority(string seed, Terrain terrain,
        YQGeneratedWorldTerrain.V2HeightSampler sampler, GeneratedWorldPlanRecord plan, float streamedCellSize,
        YQPreparedSpatialMaterializationV2 preparedOverride, GeneratedSpatialContinuationLocationV2Record candidate, bool pureProjection)
        : this(seed, terrain, sampler, plan, streamedCellSize, preparedOverride, candidate, pureProjection, null)
    { }

    internal YQContinuousWorldCellAuthority WithAcceptedMaterialization(YQPreparedSpatialMaterializationV2 prepared)
    {
        // note: Stage a prepared continuation against the same captured origin without publishing semantic state or recapturing Unity terrain.
        if (prepared == null) throw new ArgumentNullException(nameof(prepared));
        return new YQContinuousWorldCellAuthority(worldSeed, originTerrain, originSampler, semanticPlan,
            cellSize, prepared, null, pureAcceptedProjection, this);
    }

    private YQContinuousWorldCellAuthority(string seed, Terrain terrain,
        YQGeneratedWorldTerrain.V2HeightSampler sampler, GeneratedWorldPlanRecord plan, float streamedCellSize,
        YQPreparedSpatialMaterializationV2 preparedOverride, GeneratedSpatialContinuationLocationV2Record candidate,
        bool pureProjection, YQContinuousWorldCellAuthority capturedAuthority)
    {
        pureAcceptedProjection = pureProjection;
        worldSeed = capturedAuthority != null ? capturedAuthority.worldSeed
            : string.IsNullOrWhiteSpace(seed) ? "yourquest_default_world" : seed.Trim();
        semanticPlan = capturedAuthority != null ? capturedAuthority.semanticPlan : plan;
        // note: Detached staging must not rebuild the live semantic graph or replace the canonical resolver cache.
        if (capturedAuthority == null && semanticPlan != null)
            YQSemanticWorldAuthority.Ensure(semanticPlan);
        seedHash = capturedAuthority != null ? capturedAuthority.seedHash : StableHash(worldSeed + "|continuous-world-cell|v1");
        originTerrain = capturedAuthority != null ? capturedAuthority.originTerrain : terrain;
        originSampler = capturedAuthority != null ? capturedAuthority.originSampler : sampler;
        cellSize = capturedAuthority != null ? capturedAuthority.cellSize : Mathf.Max(32f, streamedCellSize);
        if (preparedOverride != null) acceptedMaterialization = preparedOverride;
        else if (capturedAuthority == null && plan != null)
        {
            YQPreparedSpatialMaterializationV2 prepared = null;
            YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out prepared, out _);
            acceptedMaterialization = prepared;
        }
        // note: Worker height sampling reads copied pad values; it never observes mutable save records during a tile build.
        var pads = new List<ContinuationPad>();
        if (candidate != null) CopyPads(candidate, pads);
        if (acceptedMaterialization != null)
        {
            var continuedOwners = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < acceptedMaterialization.ContinuationPadCount; i++)
            {
                var pad = acceptedMaterialization.GetContinuationPad(i);
                continuedOwners.Add(pad.ownerSiteId);
                if (candidate != null && pad.ownerSiteId == candidate.anchor.siteId) continue;
                pads.Add(new ContinuationPad(pad.x, pad.z, pad.reservedRadius, pad.elevationNormalized, pad.shoulderWidth));
            }
            // note: The accepted blueprint may already place settlements far beyond the origin collar. Their reviewed surface is authoritative too.
            for (int i = 0; i < acceptedMaterialization.SiteCount; i++)
            {
                var site = acceptedMaterialization.GetSite(i);
                if (!site.terrainReserveReady || continuedOwners.Contains(site.siteId) ||
                    (site.kind != YQSiteKindV2.Settlement && site.kind != YQSiteKindV2.HostileSite && site.kind != YQSiteKindV2.PointOfInterest)) continue;
                pads.Add(new ContinuationPad(site.x, site.z, site.reservedRadius, site.surfaceElevationNormalized,
                    YQContinuousWorldFeatureAuthority.AcceptedTerrainTransitionDistance));
                foreach (var member in site.MemberFootprint)
                    pads.Add(new ContinuationPad(member.x, member.z, member.reservedRadius, site.surfaceElevationNormalized,
                        YQContinuousWorldFeatureAuthority.AcceptedTerrainTransitionDistance));
            }
        }
        pads.Sort((a, b) => { int order = a.x.CompareTo(b.x); if (order != 0) return order;
            order = a.z.CompareTo(b.z); if (order != 0) return order; return a.elevation.CompareTo(b.elevation); });
        continuationPads = pads.ToArray();
        continuationPadIndex = BuildContinuationPadIndex(continuationPads);
        candidateRoutes = CopyRoutes(candidate?.physicalContext);
        if (candidateRoutes.Length > 0)
        {
            // note: Merge copied candidate roads into the compiler's canonical order once; final-union duplicates occupy the same logical slot rather than grading twice.
            candidateRouteIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var route in candidateRoutes) candidateRouteIds.Add(route.id);
            var routeOrder = new List<int>((acceptedMaterialization?.RouteCount ?? 0) + candidateRoutes.Length);
            int nextCandidate = 0;
            if (acceptedMaterialization != null)
                for (int index = 0; index < acceptedMaterialization.RouteCount; index++)
                {
                    var route = acceptedMaterialization.GetRoute(index);
                    if (!string.IsNullOrEmpty(route.continuationOwnerSiteId))
                        while (nextCandidate < candidateRoutes.Length)
                        {
                            int order = string.CompareOrdinal(candidate.anchor.siteId, route.continuationOwnerSiteId);
                            if (order == 0) order = string.CompareOrdinal(candidateRoutes[nextCandidate].id, route.routeId);
                            if (order > 0) break;
                            routeOrder.Add(-nextCandidate - 1); nextCandidate++;
                        }
                    if (!candidateRouteIds.Contains(route.routeId)) routeOrder.Add(index);
                }
            while (nextCandidate < candidateRoutes.Length) { routeOrder.Add(-nextCandidate - 1); nextCandidate++; }
            continuationRouteProjectionOrder = routeOrder.ToArray();
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
                // note: Permitted road continuations are real network terrain, so include their full bounded rays in the sampling envelope.
                if (route.permittedBoundaryContinuation && pointCount >= 2)
                    for (int endpointIndex = 0; endpointIndex < 2; endpointIndex++)
                    {
                        var endpoint = acceptedMaterialization.GetRoutePoint(routeIndex, endpointIndex == 0 ? 0 : pointCount - 1);
                        var adjacent = acceptedMaterialization.GetRoutePoint(routeIndex, endpointIndex == 0 ? 1 : pointCount - 2);
                        Vector2 direction = new Vector2(endpoint.x - adjacent.x, endpoint.z - adjacent.z).normalized;
                        if (!YQContinuousWorldFeatureAuthority.TryGetOutwardBoundaryDistance(endpoint.x, endpoint.z, direction, out _)) continue;
                        Vector2 end = new Vector2(endpoint.x, endpoint.z) + direction * YQContinuousWorldFeatureAuthority.AcceptedContinuationMaxDistance;
                        minimumX = Mathf.Min(minimumX, end.x); maximumX = Mathf.Max(maximumX, end.x);
                        minimumZ = Mathf.Min(minimumZ, end.y); maximumZ = Mathf.Max(maximumZ, end.y);
                    }
                float padding = Mathf.Max(2f, route.width * 0.5f + route.shoulderWidth) + YQContinuousWorldFeatureAuthority.AcceptedTerrainTransitionDistance;
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
        float waterMargin = Mathf.Max(1f, maximumWaterInfluence) + YQContinuousWorldFeatureAuthority.AcceptedTerrainTransitionDistance +
            YQGeneratedWorldTerrain.WorldSize / (YQGeneratedWorldTerrain.HeightmapResolution - 1f) * 1.414214f;
        acceptedWaterMinX = waterMinX - waterMargin;
        acceptedWaterMaxX = waterMaxX + waterMargin;
        acceptedWaterMinZ = waterMinZ - waterMargin;
        acceptedWaterMaxZ = waterMaxZ + waterMargin;
        if (capturedAuthority != null)
        {
            // note: The original snapshot remains immutable and shared by old workers and the staged replacement authority.
            originMinX = capturedAuthority.originMinX;
            originMaxX = capturedAuthority.originMaxX;
            originMinZ = capturedAuthority.originMinZ;
            originMaxZ = capturedAuthority.originMaxZ;
            originY = capturedAuthority.originY;
            originHeight = capturedAuthority.originHeight;
            originHeightResolution = capturedAuthority.originHeightResolution;
            originHeightSamples = capturedAuthority.originHeightSamples;
            macroOffset = capturedAuthority.macroOffset;
            ridgeOffset = capturedAuthority.ridgeOffset;
            biomeOffset = capturedAuthority.biomeOffset;
        }
        else
        {
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
        if (originSampler != null || pureAcceptedProjection)
        {
            normalizedHeight = ApplyContinuationPads(worldX, worldZ, normalizedHeight);
            // note: Continuation terrain and accepted route ribbons must share the same grade, otherwise the authored path can float above or disappear below the streamed heightfield.
            normalizedHeight = ApplyContinuationRouteModifier(worldX, worldZ, normalizedHeight);
            if (acceptedMaterialization == null)
                return YQContinuousWorldFeatureAuthority.ApplyTerrainModifiers(
                    worldSeed, worldX, worldZ, normalizedHeight, originHeight);
            // note: Resolve finite and continued banks together so a lower dry shoulder cannot take over abruptly at a higher channel's wet edge.
            bool insideFiniteWaterEnvelope = hasAcceptedWaterBounds &&
                worldX >= acceptedWaterMinX && worldX <= acceptedWaterMaxX &&
                worldZ >= acceptedWaterMinZ && worldZ <= acceptedWaterMaxZ;
            float result = YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterTerrainModifiers(acceptedMaterialization,
                worldX, worldZ, normalizedHeight, originHeight, out float waterHeight, insideFiniteWaterEnvelope) ? waterHeight : normalizedHeight;
            // note: Dry shore/road shoulders cannot move occupied construction pads. Actual wet cores keep their accepted water datum.
            var padCoordinate = new Vector2Int(Mathf.FloorToInt((worldX + 512f) / 128f), Mathf.FloorToInt((worldZ + 512f) / 128f));
            if (!continuationPadIndex.ContainsKey(padCoordinate)) return result;
            if (YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterCoreModifiers(acceptedMaterialization,
                worldX, worldZ, result, originHeight, out _)) return result;
            return ApplyContinuationPads(worldX, worldZ, result);
        }
        return ApplyFeatureModifier(worldX, worldZ, normalizedHeight);
    }

    private static void CopyPads(GeneratedSpatialContinuationLocationV2Record location, List<ContinuationPad> result)
    {
        if (location?.anchor == null || location.physicalContext?.terrainPads == null) return;
        foreach (var pad in location.physicalContext.terrainPads)
        {
            if (pad == null) continue;
            var anchor = location.anchor;
            if (pad.sectorId == anchor.siteId)
                result.Add(new ContinuationPad(anchor.x, anchor.z, anchor.reservedRadius, pad.elevationNormalized, pad.shoulderWidth));
            else if (anchor.memberFootprint != null)
                foreach (var member in anchor.memberFootprint)
                    if (member != null && member.memberId == pad.sectorId)
                        result.Add(new ContinuationPad(member.x, member.z, member.reservedRadius, pad.elevationNormalized, pad.shoulderWidth));
        }
    }

    private static ContinuationRoute[] CopyRoutes(GeneratedSpatialContinuationPhysicalContextV2Record context)
    {
        if (context?.routes == null) return Array.Empty<ContinuationRoute>();
        var result = new List<ContinuationRoute>();
        foreach (var route in context.routes)
        {
            var points = new Vector3[route.controlPoints.Count];
            for (int i = 0; i < points.Length; i++)
            { var point = route.controlPoints[i]; points[i] = new Vector3(point.x, point.normalizedElevation, point.z); }
            result.Add(new ContinuationRoute { id = route.routeId, width = route.width, shoulder = route.shoulderWidth,
                maximumGrade = route.maximumGradeDegrees, points = points });
        }
        result.Sort((first, second) => string.CompareOrdinal(first.id, second.id));
        return result.ToArray();
    }

    private float ApplyContinuationPads(float x, float z, float height)
    {
        float winningMask = 0f, elevation = height;
        var coordinate = new Vector2Int(Mathf.FloorToInt((x + 512f) / 128f), Mathf.FloorToInt((z + 512f) / 128f));
        if (!continuationPadIndex.TryGetValue(coordinate, out var localPads)) return height;
        foreach (var pad in localPads)
        {
            float distance = Vector2.Distance(new Vector2(x, z), new Vector2(pad.x, pad.z));
            float mask = 1f - Mathf.Clamp01((distance - pad.radius) / Mathf.Max(1f, pad.shoulder));
            // note: An absolute grade envelope is idempotent when reapplied after hydrology; repeated interpolation lifted or lowered previously graded shoulders.
            // note: Conformance measures a two-metre derivative at the reserve edge; keep that probe inside the flat floor before the existing shoulder grades outward.
            float allowance = Mathf.Max(0f, distance - pad.radius - 2f) * .45f / Mathf.Max(1f, originHeight);
            if (mask > winningMask) { winningMask = mask; elevation = Mathf.Clamp(height, pad.elevation - allowance, pad.elevation + allowance); }
        }
        return elevation;
    }

    private static Dictionary<Vector2Int, ContinuationPad[]> BuildContinuationPadIndex(ContinuationPad[] pads)
    {
        // note: Heightmap workers visit only local immutable reserves rather than scanning every accepted frontier sector for each vertex.
        var building = new Dictionary<Vector2Int, List<ContinuationPad>>();
        foreach (var pad in pads)
        {
            float extent = pad.radius + pad.shoulder;
            int minX = Mathf.FloorToInt((pad.x - extent + 512f) / 128f), maxX = Mathf.FloorToInt((pad.x + extent + 512f) / 128f);
            int minZ = Mathf.FloorToInt((pad.z - extent + 512f) / 128f), maxZ = Mathf.FloorToInt((pad.z + extent + 512f) / 128f);
            for (int x = minX; x <= maxX; x++) for (int z = minZ; z <= maxZ; z++)
            {
                var key = new Vector2Int(x, z);
                if (!building.TryGetValue(key, out var local)) building.Add(key, local = new List<ContinuationPad>());
                local.Add(pad);
            }
        }
        var result = new Dictionary<Vector2Int, ContinuationPad[]>(building.Count);
        foreach (var pair in building) result.Add(pair.Key, pair.Value.ToArray());
        return result;
    }

    private float ApplyContinuationRouteModifier(float x, float z, float height)
    {
        if (candidateRoutes.Length == 0) return ApplyAcceptedRouteModifier(x, z, height);
        // note: A road core owns its datum before distant cut/fill shoulders; the largest earthwork must not override another route's actual surface.
        float bestChange = 0f, bestCoreDistance = float.PositiveInfinity, result = height;
        foreach (int projection in continuationRouteProjectionOrder)
        {
            float distance, elevation, width, shoulder;
            if (projection >= 0)
            {
                MeasureAcceptedRoute(projection, x, z, height, out distance, out elevation);
                var route = acceptedMaterialization.GetRoute(projection); width = route.width; shoulder = route.shoulderWidth;
            }
            else
            {
                var route = candidateRoutes[-projection - 1];
                MeasureRoute(route.points, x, z, out distance, out Vector3 nearest);
                elevation = nearest.y; width = route.width; shoulder = route.shoulder;
            }
            float core = Mathf.Max(1f, width * .5f + shoulder);
            float grade = YQContinuousWorldFeatureAuthority.SampleAcceptedRouteGrade(height, elevation, originHeight, width * .5f, shoulder, distance);
            if (distance <= core)
            {
                float score = distance / core;
                if (score < bestCoreDistance) { bestCoreDistance = score; result = grade; }
            }
            else if (float.IsPositiveInfinity(bestCoreDistance) && Mathf.Abs(grade - height) > bestChange)
            { bestChange = Mathf.Abs(grade - height); result = grade; }
        }
        return result;
    }

    private static void MeasureRoute(Vector3[] points, float x, float z, out float distance, out Vector3 nearest)
    {
        distance = float.PositiveInfinity; nearest = default;
        Vector2 sample = new Vector2(x, z);
        for (int i = 0; i + 1 < points.Length; i++)
        {
            Vector2 start = new Vector2(points[i].x, points[i].z), delta = new Vector2(points[i + 1].x - points[i].x, points[i + 1].z - points[i].z);
            float t = delta.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(sample - start, delta) / delta.sqrMagnitude) : 0f;
            float candidate = Vector2.Distance(sample, start + t * delta);
            if (candidate < distance) { distance = candidate; nearest = Vector3.Lerp(points[i], points[i + 1], t); }
        }
    }

    public static bool TryBuildFrontierPhysicalContext(GeneratedWorldPlanRecord plan,
        YQPreparedSpatialMaterializationV2 basePrepared, GeneratedSpatialContinuationLocationV2Record locationCandidate,
        out GeneratedSpatialContinuationPhysicalContextV2Record physicalContext, out string failure)
    {
        physicalContext = null;
        failure = "frontier physical planning requires a staged candidate and a current accepted prepared context";
        var artifact = plan?.spatialPlanV2;
        var sourceAnchor = locationCandidate?.anchor;
        if (artifact == null || basePrepared == null || sourceAnchor == null ||
            locationCandidate.state != YQSpatialContinuationStateV2.Staged || string.IsNullOrWhiteSpace(plan.worldSeed) ||
            artifact.worldSeed != plan.worldSeed || artifact.acceptanceState != GeneratedSpatialPlanAcceptanceState.Accepted ||
            string.IsNullOrWhiteSpace(artifact.contentHash) || artifact.contentHash != artifact.validatedContentHash ||
            string.IsNullOrWhiteSpace(locationCandidate.deterministicSeed)) return false;
        string worldSeedSnapshot = plan.worldSeed, parentHashSnapshot = artifact.contentHash;
        string candidateSeedSnapshot = locationCandidate.deterministicSeed;
        long candidateRevisionSnapshot = locationCandidate.revision;
        if (!TrySnapshotFrontierGeometry(sourceAnchor, locationCandidate.entrances,
                out var anchor, out var sectors, out var entrances, out failure)) return false;
        if (!basePrepared.TryGetRegion(anchor.parentRegionId, out _))
        { failure = "frontier physical candidate has no existing canonical prepared region"; return false; }
        if (basePrepared.TryGetSiteBySiteId(anchor.siteId, out _) || basePrepared.TryGetSiteBySemanticId(anchor.sourceSemanticId, out _))
        { failure = "frontier physical candidate already has an accepted site or semantic identity"; return false; }
        foreach (var sector in sectors)
        {
            if (Mathf.Max(Mathf.Abs(sector.x), Mathf.Abs(sector.z)) - sector.radius - FrontierPadShoulder <= FrontierOpeningCollarRadius)
            { failure = "frontier physical sector intersects opening/collar terrain: " + sector.id; return false; }
            if (FrontierSegmentIntersectsAcceptedReserve(basePrepared, new Vector2(sector.x, sector.z),
                    new Vector2(sector.x, sector.z), sector.radius, out string otherId))
            { failure = "frontier physical sector overlaps another accepted owner: " + sector.id + " / " + otherId; return false; }
        }
        string planningSeed = worldSeedSnapshot + "|" + candidateSeedSnapshot + "|frontier_physical_context_v1";
        if (!TryMapFrontierFrontages(basePrepared, anchor, sectors, entrances, out var frontages, out failure)) return false;

        // note: Candidate records are never changed; transient geometry snapshots and copied scalar context feed the same pure sampler used by acceptance.
        var budget = new FrontierPlanningBudget();
        var unmodified = new YQContinuousWorldCellAuthority(worldSeedSnapshot, null, null, null, 128f, basePrepared, null, true);
        var context = new GeneratedSpatialContinuationPhysicalContextV2Record();
        foreach (var sector in sectors)
        {
            if (!TrySampleFrontierPlanningHeight(unmodified, sector.x, sector.z, budget, out float elevation, out failure) ||
                !TryCheckFrontierDryReserve(basePrepared, sector.x, sector.z, sector.radius + 2f, budget, out failure)) return false;
            for (int point = 0; point < 8; point++)
            {
                float angle = point * Mathf.PI * .25f;
                float x = sector.x + Mathf.Cos(angle) * sector.radius;
                float z = sector.z + Mathf.Sin(angle) * sector.radius;
                if (!TrySampleFrontierPlanningHeight(unmodified, x, z, budget, out float existing, out failure)) return false;
                if (Mathf.Abs(existing - elevation) * unmodified.originHeight > FrontierMaximumEarthwork)
                { failure = "frontier sector exceeds twelve metres of reserve earthwork: " + sector.id; return false; }
            }
            context.terrainPads.Add(new GeneratedSpatialContinuationTerrainPadV2Record
            { sectorId = sector.id, elevationNormalized = elevation, shoulderWidth = FrontierPadShoulder });
        }
        var measuredCandidate = new GeneratedSpatialContinuationLocationV2Record { anchor = anchor, entrances = entrances, physicalContext = context };
        var padOnly = new YQContinuousWorldCellAuthority(worldSeedSnapshot, null, null, null, 128f, basePrepared, measuredCandidate, true);
        int acceptedPointCount = 0, conformanceSamples = 0;
        var connections = new List<YQContinuousWorldFeatureAuthority.FrontierRouteConnection>(4);
        foreach (var frontage in frontages)
        {
            if (!YQContinuousWorldFeatureAuthority.TryCollectFrontierRouteConnections(basePrepared,
                    frontage.entrance.worldX, frontage.entrance.worldZ, planningSeed + "|" + frontage.entrance.entranceId,
                    connections, out failure)) return false;
            YQRouteCorridorV2 selected = null;
            int selectedSamples = 0;
            string lastRejection = "no bounded dry route connects this frontage";
            uint detourOrder = StableHash(planningSeed + "|detour|" + frontage.entrance.entranceId);
            foreach (var connection in connections)
            {
                for (int variant = 0; variant < 7; variant++)
                {
                    int pair = (variant + 1) / 2;
                    float offset = variant == 0 ? 0f : pair == 1 ? 64f : pair == 2 ? 128f : 256f;
                    bool negative = (variant & 1) == 0;
                    if ((detourOrder & 1u) != 0u) negative = !negative;
                    if (variant > 0 && negative) offset = -offset;
                    if (TryBuildFrontierRoute(basePrepared, padOnly, frontage, connection.point, offset, budget,
                            256 - acceptedPointCount, 512 - conformanceSamples, out selected, out selectedSamples, out lastRejection)) break;
                    if (budget.exhausted)
                    { failure = "frontier physical planning exhausted 8192 exploratory samples at " + frontage.entrance.entranceId; return false; }
                }
                if (selected != null) break;
            }
            if (selected == null)
            { failure = "frontier frontage rejected: " + frontage.entrance.entranceId + ": " + lastRejection; return false; }
            selected.parentRegionId = anchor.parentRegionId;
            selected.toSiteId = anchor.siteId;
            selected.routeClass = anchor.kind == YQSiteKindV2.Settlement ? YQRouteClassV2.SecondaryRoad : YQRouteClassV2.Trail;
            context.routes.Add(selected);
            acceptedPointCount += selected.controlPoints.Count;
            conformanceSamples += selectedSamples;
        }
        if (!YQSpatialContinuationValidatorV2.ValidatePhysicalContextOnly(measuredCandidate).IsStructurallyValid)
        { failure = "frontier planner produced unsupported physical context geometry"; return false; }
        if (!TryMeasureAcceptedContinuationSite(plan, basePrepared, measuredCandidate, out var measured, out failure)) return false;
        if (measured.routeAccess < anchor.minimumRouteAccess || measured.waterAccess < anchor.minimumWaterAccess)
        { failure = "frontier physical context does not meet the candidate's declared access scores"; return false; }
        if (plan.worldSeed != worldSeedSnapshot || plan.spatialPlanV2 != artifact || artifact.worldSeed != worldSeedSnapshot ||
            artifact.contentHash != parentHashSnapshot || artifact.validatedContentHash != parentHashSnapshot ||
            artifact.acceptanceState != GeneratedSpatialPlanAcceptanceState.Accepted || locationCandidate.state != YQSpatialContinuationStateV2.Staged ||
            locationCandidate.deterministicSeed != candidateSeedSnapshot || locationCandidate.revision != candidateRevisionSnapshot ||
            locationCandidate.anchor != sourceAnchor || !FrontierGeometryMatches(sourceAnchor, anchor, locationCandidate.entrances, entrances))
        { failure = "frontier candidate or parent changed during pure physical planning"; return false; }
        physicalContext = context;
        failure = string.Empty;
        return true;
    }

    internal static bool TryGetFrontierOpeningCollarRecoveryDistance(
        GeneratedSpatialContinuationLocationV2Record candidate, Vector2 direction, out float distance)
    {
        // note: Derive the minimum rigid shift that clears every reserved sector from the same collar enforced by physical preflight.
        distance = 0f;
        var anchor = candidate?.anchor;
        if (anchor == null || anchor.memberFootprint == null || anchor.memberFootprint.Count > 7 ||
            !FrontierFinite(direction.x) || !FrontierFinite(direction.y) ||
            Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1f) return false;
        for (int index = -1; index < anchor.memberFootprint.Count; index++)
        {
            var member = index < 0 ? null : anchor.memberFootprint[index];
            float x = member == null ? anchor.x : member.x;
            float z = member == null ? anchor.z : member.z;
            float radius = member == null ? anchor.reservedRadius : member.reservedRadius;
            if (!FrontierFinite(x) || !FrontierFinite(z) || !FrontierFinite(radius) || radius <= 0f) return false;
            float along = direction.x != 0f ? x : z;
            float across = direction.x != 0f ? z : x;
            if (Mathf.Max(Mathf.Abs(along), Mathf.Abs(across)) - radius - FrontierPadShoulder > FrontierOpeningCollarRadius)
                continue;
            float required = FrontierOpeningCollarRadius + FrontierPadShoulder + radius + 1f -
                (direction.x != 0f ? direction.x * x : direction.y * z);
            if (!FrontierFinite(required) || required <= 0f) return false;
            distance = Mathf.Max(distance, required);
        }
        return distance > 0f && distance <= YQSemanticWorldAuthority.FrontierPhysicalRecoveryDistance;
    }

    private static bool TrySnapshotFrontierGeometry(YQSiteAnchorV2 source, List<GeneratedSemanticEntranceRecord> sourceEntrances,
        out YQSiteAnchorV2 anchor, out List<FrontierSector> sectors, out List<GeneratedSemanticEntranceRecord> entrances, out string failure)
    {
        anchor = null; sectors = new List<FrontierSector>(8); entrances = new List<GeneratedSemanticEntranceRecord>(16);
        failure = "frontier candidate has an invalid bounded anchor or entrance contract";
        if (string.IsNullOrWhiteSpace(source.siteId) || string.IsNullOrWhiteSpace(source.sourceSemanticId) ||
            string.IsNullOrWhiteSpace(source.parentRegionId) ||
            source.kind != YQSiteKindV2.Settlement && source.kind != YQSiteKindV2.HostileSite && source.kind != YQSiteKindV2.PointOfInterest ||
            !IsFrontierCoordinate(source.x, source.z) || !FrontierFinite(source.reservedRadius) || source.reservedRadius <= 0f || source.reservedRadius > 256f ||
            !FrontierFinite(source.maximumSlopeDegrees) || source.maximumSlopeDegrees < 0f || source.maximumSlopeDegrees > 90f ||
            !FrontierFinite(source.minimumRouteAccess) || source.minimumRouteAccess < 0f || source.minimumRouteAccess > 1f ||
            !FrontierFinite(source.minimumWaterAccess) || source.minimumWaterAccess < 0f || source.minimumWaterAccess > 1f ||
            source.memberFootprint == null || source.memberFootprint.Count > 7 || sourceEntrances == null || sourceEntrances.Count == 0 || sourceEntrances.Count > 16)
            return false;
        anchor = new YQSiteAnchorV2 { siteId = source.siteId, sourceSemanticId = source.sourceSemanticId,
            parentRegionId = source.parentRegionId, kind = source.kind, x = source.x, z = source.z,
            reservedRadius = source.reservedRadius, maximumSlopeDegrees = source.maximumSlopeDegrees,
            minimumRouteAccess = source.minimumRouteAccess, minimumWaterAccess = source.minimumWaterAccess,
            requiresTerrainConformance = source.requiresTerrainConformance };
        var sectorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { source.siteId };
        sectors.Add(new FrontierSector(source.siteId, source.x, source.z, source.reservedRadius));
        foreach (var member in source.memberFootprint)
        {
            if (member == null || string.IsNullOrWhiteSpace(member.memberId) || !sectorIds.Add(member.memberId) ||
                !IsFrontierCoordinate(member.x, member.z) || !FrontierFinite(member.reservedRadius) ||
                member.reservedRadius <= 0f || member.reservedRadius > 256f || member.sectorIndex < 0)
            { failure = "frontier candidate has an invalid or duplicate physical member sector"; return false; }
            anchor.memberFootprint.Add(new YQSiteMemberFootprintV2 { memberId = member.memberId,
                x = member.x, z = member.z, reservedRadius = member.reservedRadius, sectorIndex = member.sectorIndex });
            sectors.Add(new FrontierSector(member.memberId, member.x, member.z, member.reservedRadius));
        }
        var entranceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entrance in sourceEntrances)
        {
            if (entrance == null || string.IsNullOrWhiteSpace(entrance.entranceId) || !entranceIds.Add(entrance.entranceId) ||
                !IsFrontierCoordinate(entrance.worldX, entrance.worldZ) || !FrontierFinite(entrance.headingDegrees)) return false;
            entrances.Add(new GeneratedSemanticEntranceRecord { entranceId = entrance.entranceId,
                worldX = entrance.worldX, worldZ = entrance.worldZ, headingDegrees = entrance.headingDegrees,
                permittedRouteId = entrance.permittedRouteId });
        }
        sectors.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        failure = string.Empty;
        return true;
    }

    private static bool TryMapFrontierFrontages(YQPreparedSpatialMaterializationV2 prepared, YQSiteAnchorV2 anchor,
        List<FrontierSector> sectors, List<GeneratedSemanticEntranceRecord> entrances,
        out List<FrontierFrontage> frontages, out string failure, bool excludeAcceptedRoutes = true)
    {
        // note: The existing scalar entrance contract has no sector ID; only unique containment can establish a physical frontage without inventing geometry.
        frontages = new List<FrontierFrontage>(entrances.Count);
        var covered = new HashSet<string>(StringComparer.Ordinal);
        var routeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (excludeAcceptedRoutes)
            for (int index = 0; index < prepared.RouteCount; index++) routeIds.Add(prepared.GetRoute(index).routeId);
        foreach (var entrance in entrances)
        {
            FrontierSector sector = default; int matches = 0;
            foreach (var possible in sectors)
                if (Vector2.Distance(new Vector2(entrance.worldX, entrance.worldZ), new Vector2(possible.x, possible.z)) <= possible.radius + .001f)
                { sector = possible; matches++; }
            if (matches != 1)
            { failure = "frontier entrance lacks a unique central/member reserve: " + entrance.entranceId; return false; }
            string routeId = entrance.permittedRouteId;
            if (string.IsNullOrWhiteSpace(routeId))
                routeId = "route:frontier:v1:" + FrontierIdentityPart(anchor.siteId) + FrontierIdentityPart(sector.id) + FrontierIdentityPart(entrance.entranceId);
            if (!routeIds.Add(routeId))
            { failure = "frontier entrance route ID collides with another proposed or accepted route: " + routeId; return false; }
            covered.Add(sector.id);
            frontages.Add(new FrontierFrontage(sector, entrance, routeId));
        }
        foreach (var sector in sectors)
            if (!covered.Contains(sector.id))
            { failure = "frontier physical member has no explicit connected entrance frontage: " + sector.id; return false; }
        frontages.Sort((a, b) => { int order = string.CompareOrdinal(a.sector.id, b.sector.id);
            return order != 0 ? order : string.CompareOrdinal(a.entrance.entranceId, b.entrance.entranceId); });
        failure = string.Empty;
        return true;
    }

    private static bool TryBuildFrontierRoute(YQPreparedSpatialMaterializationV2 prepared, YQContinuousWorldCellAuthority padOnly,
        FrontierFrontage frontage, Vector3 network, float detour, FrontierPlanningBudget budget,
        int remainingPoints, int remainingConformanceSamples, out YQRouteCorridorV2 route, out int samples, out string failure)
    {
        route = null; samples = 0; failure = string.Empty;
        Vector2 start = new Vector2(network.x, network.z), end = new Vector2(frontage.entrance.worldX, frontage.entrance.worldZ);
        Vector2 direction = end - start;
        if (direction.magnitude < .01f)
        { failure = "frontier frontage is degenerate with its accepted network projection"; return false; }
        // note: The final approach follows the entrance's outward heading, so a route cannot meet its coordinate through the rear of the opening.
        float heading = Mathf.Repeat(frontage.entrance.headingDegrees, 360f) * Mathf.Deg2Rad;
        Vector2 approach = end + new Vector2(Mathf.Sin(heading), Mathf.Cos(heading)) * 16f;
        Vector2 approachDirection = approach - start;
        var bends = new List<Vector2>(4) { start };
        if (detour != 0f) bends.Add((start + approach) * .5f + new Vector2(-approachDirection.y, approachDirection.x).normalized * detour);
        if (Vector2.Distance(bends[bends.Count - 1], approach) >= .01f) bends.Add(approach);
        bends.Add(end);
        var points = new List<YQBlueprintPointV2>(64)
        { new YQBlueprintPointV2 { x = network.x, z = network.z, normalizedElevation = network.y, width = FrontierRouteWidth } };
        for (int bend = 0; bend + 1 < bends.Count; bend++)
        {
            Vector2 first = bends[bend], second = bends[bend + 1];
            if (FrontierSegmentIntersectsAcceptedReserve(prepared, first, second, FrontierRouteWidth * .5f + FrontierRouteShoulder, out string otherId))
            { failure = "frontier route intrudes on another accepted reserve: " + otherId; return false; }
            if (!budget.TryConsume()) { failure = "frontier physical planning exhausted 8192 exploratory samples"; return false; }
            if (!YQContinuousWorldFeatureAuthority.TryValidateAcceptedDryRouteSegment(prepared, first, second,
                    FrontierRouteWidth * .5f + 2f, out failure)) return false;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(first, second) / 32f));
            if (points.Count + steps > 64 || points.Count + steps > remainingPoints)
            { failure = "frontier route exceeds its 64-point or shared 256-point budget"; return false; }
            for (int step = 1; step <= steps; step++)
            {
                Vector2 point = step == steps ? second : Vector2.Lerp(first, second, step / (float)steps);
                if (!TrySampleFrontierPlanningHeight(padOnly, point.x, point.y, budget, out float elevation, out failure)) return false;
                points.Add(new YQBlueprintPointV2 { x = point.x, z = point.y, normalizedElevation = elevation, width = FrontierRouteWidth });
            }
        }
        for (int index = 0; index + 1 < points.Count; index++)
        {
            var first = points[index]; var second = points[index + 1];
            Vector2 a = new Vector2(first.x, first.z), b = new Vector2(second.x, second.z), delta = b - a;
            float length = delta.magnitude;
            if (length < .01f || Mathf.Atan(Mathf.Abs(second.normalizedElevation - first.normalizedElevation) * padOnly.originHeight / length) * Mathf.Rad2Deg > MaximumCollarGradeDegrees)
            { failure = "frontier route exceeds twenty-eight degrees of longitudinal grade"; return false; }
            int steps = Mathf.CeilToInt(length / 16f);
            samples += steps + 1;
            if (samples > remainingConformanceSamples)
            { failure = "frontier routes exceed 512 shared conformance samples"; return false; }
            Vector2 perpendicular = new Vector2(-delta.y, delta.x) / length;
            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                Vector2 point = Vector2.Lerp(a, b, t);
                float elevation = Mathf.Lerp(first.normalizedElevation, second.normalizedElevation, t);
                if (!TrySampleFrontierPlanningHeight(padOnly, point.x, point.y, budget, out float existing, out failure) ||
                    !TryCheckFrontierDryReserve(prepared, point.x, point.y, FrontierRouteWidth * .5f + 2f, budget, out failure)) return false;
                if (Mathf.Abs(existing - elevation) * padOnly.originHeight > FrontierMaximumEarthwork)
                { failure = "frontier route exceeds twelve metres of earthwork"; return false; }
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 near = point + perpendicular * side * (FrontierRouteWidth * .5f + FrontierRouteShoulder * .5f - 2f);
                    Vector2 far = point + perpendicular * side * (FrontierRouteWidth * .5f + FrontierRouteShoulder * .5f + 2f);
                    if (!TrySampleFrontierPlanningHeight(padOnly, near.x, near.y, budget, out float nearHeight, out failure) ||
                        !TrySampleFrontierPlanningHeight(padOnly, far.x, far.y, budget, out float farHeight, out failure)) return false;
                    nearHeight = YQContinuousWorldFeatureAuthority.SampleAcceptedRouteGrade(nearHeight, elevation, padOnly.originHeight,
                        FrontierRouteWidth * .5f, FrontierRouteShoulder, FrontierRouteWidth * .5f + FrontierRouteShoulder * .5f - 2f);
                    farHeight = YQContinuousWorldFeatureAuthority.SampleAcceptedRouteGrade(farHeight, elevation, padOnly.originHeight,
                        FrontierRouteWidth * .5f, FrontierRouteShoulder, FrontierRouteWidth * .5f + FrontierRouteShoulder * .5f + 2f);
                    if (Mathf.Atan(Mathf.Abs(farHeight - nearHeight) * padOnly.originHeight / 4f) * Mathf.Rad2Deg > MaximumCollarGradeDegrees)
                    { failure = "frontier route has an impassable terrain shoulder"; return false; }
                }
            }
        }
        route = new YQRouteCorridorV2 { routeId = frontage.routeId, width = FrontierRouteWidth,
            shoulderWidth = FrontierRouteShoulder, maximumGradeDegrees = MaximumCollarGradeDegrees, controlPoints = points,
            tags = new List<string> { "frontier_physical_context_v1", "sector:" + frontage.sector.id, "entrance:" + frontage.entrance.entranceId } };
        return true;
    }

    private static bool TrySampleFrontierPlanningHeight(YQContinuousWorldCellAuthority authority, float x, float z,
        FrontierPlanningBudget budget, out float height, out string failure)
    {
        height = 0f; failure = string.Empty;
        if (!IsFrontierCoordinate(x, z) || Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) <= 1152f)
        { failure = "frontier route leaves signed coordinate bounds or intersects opening/collar terrain"; return false; }
        if (!budget.TryConsume()) { failure = "frontier physical planning exhausted 8192 exploratory samples"; return false; }
        height = authority.SampleHeightNormalizedOffMainThread(x, z);
        if (!FrontierFinite(height) || height < 0f || height > 1f)
        { failure = "frontier pure terrain sample is not finite and normalized"; return false; }
        return true;
    }

    private static bool TryCheckFrontierDryReserve(YQPreparedSpatialMaterializationV2 prepared, float x, float z,
        float clearance, FrontierPlanningBudget budget, out string failure)
    {
        if (!budget.TryConsume()) { failure = "frontier physical planning exhausted 8192 exploratory samples"; return false; }
        YQContinuousWorldFeatureAuthority.TryMeasureAcceptedWaterDistance(prepared, x, z, out string waterId, out float bank, out bool wet);
        if (wet || float.IsNaN(bank) || bank < clearance)
        { failure = "frontier physical route/reserve needs an unsupported accepted-water crossing or dry bank: " + waterId; return false; }
        failure = string.Empty;
        return true;
    }

    private static bool FrontierSegmentIntersectsAcceptedReserve(YQPreparedSpatialMaterializationV2 prepared,
        Vector2 first, Vector2 second, float padding, out string siteId, string ownSiteId = null)
    {
        for (int index = 0; index < prepared.SiteCount; index++)
        {
            var site = prepared.GetSite(index);
            if (string.Equals(site.siteId, ownSiteId, StringComparison.OrdinalIgnoreCase)) continue;
            for (int member = -1; member < site.MemberFootprint.Count; member++)
            {
                var sector = member < 0 ? null : site.MemberFootprint[member];
                if (member >= 0 && sector == null) continue;
                float x = sector != null ? sector.x : site.x, z = sector != null ? sector.z : site.z;
                float radius = (sector != null ? sector.reservedRadius : site.reservedRadius) + padding + 18f;
                double begin = 0d, end = 1d;
                if (FrontierClipAxis(first.x, second.x - first.x, x - radius, x + radius, ref begin, ref end) &&
                    FrontierClipAxis(first.y, second.y - first.y, z - radius, z + radius, ref begin, ref end))
                { siteId = site.siteId; return true; }
            }
        }
        siteId = string.Empty;
        return false;
    }

    private static bool FrontierClipAxis(double start, double delta, double minimum, double maximum, ref double begin, ref double end)
    {
        if (Math.Abs(delta) < .000001d) return start >= minimum && start <= maximum;
        double first = (minimum - start) / delta, second = (maximum - start) / delta;
        if (first > second) { double swap = first; first = second; second = swap; }
        begin = Math.Max(begin, first); end = Math.Min(end, second);
        return begin <= end;
    }

    private static bool FrontierGeometryMatches(YQSiteAnchorV2 source, YQSiteAnchorV2 snapshot,
        List<GeneratedSemanticEntranceRecord> sourceEntrances, List<GeneratedSemanticEntranceRecord> entrances)
    {
        if (source.siteId != snapshot.siteId || source.sourceSemanticId != snapshot.sourceSemanticId || source.parentRegionId != snapshot.parentRegionId ||
            source.kind != snapshot.kind || source.x != snapshot.x || source.z != snapshot.z || source.reservedRadius != snapshot.reservedRadius ||
            source.maximumSlopeDegrees != snapshot.maximumSlopeDegrees || source.minimumRouteAccess != snapshot.minimumRouteAccess ||
            source.minimumWaterAccess != snapshot.minimumWaterAccess || source.requiresTerrainConformance != snapshot.requiresTerrainConformance ||
            source.memberFootprint == null || source.memberFootprint.Count != snapshot.memberFootprint.Count ||
            sourceEntrances == null || sourceEntrances.Count != entrances.Count) return false;
        for (int index = 0; index < snapshot.memberFootprint.Count; index++)
        {
            var a = source.memberFootprint[index]; var b = snapshot.memberFootprint[index];
            if (a == null || a.memberId != b.memberId || a.x != b.x || a.z != b.z || a.reservedRadius != b.reservedRadius || a.sectorIndex != b.sectorIndex) return false;
        }
        for (int index = 0; index < entrances.Count; index++)
        {
            var a = sourceEntrances[index]; var b = entrances[index];
            if (a == null || a.entranceId != b.entranceId || a.worldX != b.worldX || a.worldZ != b.worldZ ||
                a.headingDegrees != b.headingDegrees || a.permittedRouteId != b.permittedRouteId) return false;
        }
        return true;
    }

    private static bool IsFrontierCoordinate(float x, float z)
    {
        if (!FrontierFinite(x) || !FrontierFinite(z) || Math.Abs(x) > 1048576f || Math.Abs(z) > 1048576f) return false;
        double cellX = Math.Floor(((double)x - WorldGridOrigin) / 128d), cellZ = Math.Floor(((double)z - WorldGridOrigin) / 128d);
        return cellX >= int.MinValue && cellX <= int.MaxValue && cellZ >= int.MinValue && cellZ <= int.MaxValue;
    }
    private static bool FrontierFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static string FrontierIdentityPart(string identity) => identity.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + identity + "|";

    private static bool TryValidateFrontierRouteFrontages(GeneratedSpatialContinuationPhysicalContextV2Record context,
        List<FrontierFrontage> frontages, out string failure)
    {
        // note: Replay validates endpoint geometry and the actual final approach, never planner tags or stored proof claims.
        var supportedEntrances = new HashSet<string>(StringComparer.Ordinal);
        foreach (var route in context.routes)
        {
            var endpoint = route.controlPoints[route.controlPoints.Count - 1];
            var adjacent = route.controlPoints[route.controlPoints.Count - 2];
            Vector2 approach = new Vector2(adjacent.x - endpoint.x, adjacent.z - endpoint.z);
            FrontierFrontage match = default; int matches = 0;
            foreach (var frontage in frontages)
            {
                var entrance = frontage.entrance;
                if (!string.IsNullOrWhiteSpace(entrance.permittedRouteId) &&
                    !string.Equals(entrance.permittedRouteId, route.routeId, StringComparison.Ordinal)) continue;
                if (Mathf.Abs(endpoint.x - entrance.worldX) > .001f || Mathf.Abs(endpoint.z - entrance.worldZ) > .001f || approach.magnitude < .01f) continue;
                float heading = Mathf.Repeat(entrance.headingDegrees, 360f) * Mathf.Deg2Rad;
                if (Vector2.Dot(approach.normalized, new Vector2(Mathf.Sin(heading), Mathf.Cos(heading))) < Mathf.Cos(5f * Mathf.Deg2Rad)) continue;
                match = frontage; matches++;
            }
            if (matches != 1 || !supportedEntrances.Add(match.entrance.entranceId))
            { failure = "frontier route must end at one exact, route-matched, heading-aligned entrance: " + route.routeId; return false; }
        }
        if (supportedEntrances.Count != frontages.Count)
        { failure = "frontier physical context does not connect every explicit central/member frontage"; return false; }
        failure = string.Empty;
        return true;
    }

    public static bool TryMeasureAcceptedContinuationSite(GeneratedWorldPlanRecord plan,
        YQPreparedSpatialMaterializationV2 basePrepared, GeneratedSpatialContinuationLocationV2Record location,
        out YQSpatialContinuationSiteSampleV2 sample, out string failure)
    {
        sample = default; failure = "frontier physical context is incomplete";
        if (plan == null || basePrepared == null || location?.anchor == null ||
            !YQSpatialContinuationValidatorV2.ValidatePhysicalContextOnly(location).IsStructurallyValid ||
            location.physicalContext == null) return false;
        var anchor = location.anchor;
        if (!TrySnapshotFrontierGeometry(anchor, location.entrances, out _, out var sectors, out var entrances, out failure) ||
            !TryMapFrontierFrontages(basePrepared, anchor, sectors, entrances, out var frontages, out failure, false) ||
            !TryValidateFrontierRouteFrontages(location.physicalContext, frontages, out failure)) return false;
        // note: This compiler preflight deliberately bypasses semantic Ensure/resolver recursion and Unity terrain; only immutable base projection and candidate scalars enter its sampling authority.
        var authority = new YQContinuousWorldCellAuthority(plan.worldSeed, null, null, null, 128f, basePrepared, location, true);
        var candidatePads = new List<ContinuationPad>(); CopyPads(location, candidatePads);
        if (candidatePads.Count == 0 && anchor.requiresTerrainConformance) return false;
        float maximumSlope = 0f;
        foreach (var pad in candidatePads)
        {
            if (FrontierSegmentIntersectsAcceptedReserve(basePrepared, new Vector2(pad.x, pad.z), new Vector2(pad.x, pad.z),
                    pad.radius, out string otherSiteId, anchor.siteId))
            { failure = "frontier reserve overlaps another accepted central/member owner: " + otherSiteId; return false; }
            if (Mathf.Max(Mathf.Abs(pad.x), Mathf.Abs(pad.z)) - pad.radius - pad.shoulder <= 1152f)
            { failure = "frontier reserve intersects the opening terrain or its collar"; return false; }
            YQContinuousWorldFeatureAuthority.TryMeasureAcceptedWaterDistance(basePrepared, pad.x, pad.z, out _, out float reserveBank, out bool reserveWet);
            if (reserveWet || reserveBank < pad.radius + 2f)
            { failure = "frontier physical sector lacks a complete dry reserve"; return false; }
            // note: A flat reserve cannot hide an extreme earthwork or impassable shoulder outside its sampled building floor.
            if (Mathf.Abs(authority.SampleContinuationBaseNormalized(pad.x, pad.z, null) - pad.elevation) * authority.originHeight > 12f)
            { failure = "frontier reserve exceeds the bounded earthwork contract"; return false; }
            for (int edge = 0; edge < 8; edge++)
            {
                float angle = edge * Mathf.PI * .25f;
                float x = pad.x + Mathf.Cos(angle) * (pad.radius + pad.shoulder * .5f);
                float z = pad.z + Mathf.Sin(angle) * (pad.radius + pad.shoulder * .5f);
                float dx = (authority.SampleHeightNormalizedOffMainThread(x + 2f, z) - authority.SampleHeightNormalizedOffMainThread(x - 2f, z)) * authority.originHeight / 4f;
                float dz = (authority.SampleHeightNormalizedOffMainThread(x, z + 2f) - authority.SampleHeightNormalizedOffMainThread(x, z - 2f)) * authority.originHeight / 4f;
                if (Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg > MaximumCollarGradeDegrees)
                { failure = "frontier reserve has an impassable terrain shoulder"; return false; }
            }
            for (int i = 0; i < 9; i++)
            {
                float angle = (i - 1) * Mathf.PI * .25f;
                float x = pad.x + (i == 0 ? 0f : Mathf.Cos(angle) * pad.radius);
                float z = pad.z + (i == 0 ? 0f : Mathf.Sin(angle) * pad.radius);
                float height = authority.SampleHeightNormalizedOffMainThread(x, z);
                if (Mathf.Abs(height - pad.elevation) * authority.originHeight > .5f)
                { failure = "frontier reserve cannot retain its approved elevation under route/water conformance"; return false; }
                float dx = (authority.SampleHeightNormalizedOffMainThread(x + 2f, z) - authority.SampleHeightNormalizedOffMainThread(x - 2f, z)) * authority.originHeight / 4f;
                float dz = (authority.SampleHeightNormalizedOffMainThread(x, z + 2f) - authority.SampleHeightNormalizedOffMainThread(x, z - 2f)) * authority.originHeight / 4f;
                maximumSlope = Mathf.Max(maximumSlope, Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg);
                YQContinuousWorldFeatureAuthority.TryMeasureAcceptedWaterDistance(basePrepared, x, z, out _, out float bank, out bool wet);
                if (wet || bank < 2f) { failure = "frontier reserve intersects accepted water or its dry bank"; return false; }
            }
        }
        if (maximumSlope > anchor.maximumSlopeDegrees)
        { failure = "frontier reserve exceeds its slope contract"; return false; }
        float routeDistance = float.PositiveInfinity; Vector3 frontage = default; string routeId = string.Empty;
        int routeSamples = 0;
        foreach (var route in authority.candidateRoutes)
        {
            if (!HasAcceptedRouteConnection(basePrepared, route.points[0], route.width))
            { failure = "frontier access route lacks an accepted network connection"; return false; }
            for (int i = 0; i + 1 < route.points.Length; i++)
            {
                Vector3 a = route.points[i], b = route.points[i + 1];
                if (FrontierSegmentIntersectsAcceptedReserve(basePrepared, new Vector2(a.x, a.z), new Vector2(b.x, b.z),
                        route.width * .5f + route.shoulder, out string otherSiteId, anchor.siteId))
                { failure = "frontier route intrudes on another accepted central/member reserve: " + otherSiteId; return false; }
                if (!YQContinuousWorldFeatureAuthority.TryValidateAcceptedDryRouteSegment(basePrepared,
                        new Vector2(a.x, a.z), new Vector2(b.x, b.z), route.width * .5f + 2f, out failure)) return false;
                float length = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                if (length < .01f || Mathf.Atan(Mathf.Abs(b.y - a.y) * authority.originHeight / length) * Mathf.Rad2Deg > route.maximumGrade)
                { failure = "frontier access route exceeds its grade contract"; return false; }
                int steps = Mathf.CeilToInt(length / 16f);
                routeSamples += steps + 1;
                if (routeSamples > 512) { failure = "frontier access route exceeds bounded conformance sampling"; return false; }
                for (int step = 0; step <= steps; step++)
                {
                    Vector3 point = Vector3.Lerp(a, b, step / (float)steps);
                    if (Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.z)) <= 1152f)
                    { failure = "frontier access route intersects unsampled opening/collar terrain"; return false; }
                    YQContinuousWorldFeatureAuthority.TryMeasureAcceptedWaterDistance(basePrepared, point.x, point.z, out _, out float bank, out bool wet);
                    if (wet || bank < route.width * .5f + 2f)
                    { failure = "frontier access route requires an unsupported water crossing"; return false; }
                    if (Mathf.Abs(authority.SampleHeightNormalizedOffMainThread(point.x, point.z) - point.y) * authority.originHeight > .5f)
                    { failure = "frontier access route does not conform to its approved terrain"; return false; }
                }
            }
            MeasureRoute(route.points, anchor.x, anchor.z, out float distance, out Vector3 nearest);
            if (distance < routeDistance) { routeDistance = distance; frontage = nearest; routeId = route.id; }
        }
        float access = float.IsPositiveInfinity(routeDistance) ? 0f : Mathf.Clamp01(1f - Mathf.Max(0f, routeDistance - anchor.reservedRadius) / 128f);
        YQContinuousWorldFeatureAuthority.TryMeasureAcceptedWaterDistance(basePrepared, anchor.x, anchor.z,
            out string waterId, out float waterDistance, out bool centerWet);
        float waterAccess = float.IsPositiveInfinity(waterDistance) ? 0f : Mathf.Clamp01(1f - Mathf.Max(0f, waterDistance) / 512f);
        var terrain = new YQSpatialTerrainSampleV2 { elevationNormalized = authority.SampleHeightNormalizedOffMainThread(anchor.x, anchor.z),
            siteReserveMask = authority.continuationPads.Length > 0 ? 1f : 0f, routeMask = access, waterMask = centerWet ? 1f : 0f, waterFeatureIndex = -1 };
        sample = new YQSpatialContinuationSiteSampleV2(terrain, maximumSlope, routeId, frontage.x, frontage.z,
            routeDistance, access, waterId, waterDistance, waterAccess, centerWet);
        failure = string.Empty; return true;
    }

    private static bool HasAcceptedRouteConnection(YQPreparedSpatialMaterializationV2 prepared, Vector3 endpoint, float width)
    {
        for (int i = 0; i < prepared.RouteCount; i++)
        {
            var route = prepared.GetRoute(i);
            var points = new Vector3[prepared.GetRoutePointCount(i)];
            for (int j = 0; j < points.Length; j++) { var point = prepared.GetRoutePoint(i, j); points[j] = new Vector3(point.x, point.surfaceElevationNormalized, point.z); }
            MeasureRoute(points, endpoint.x, endpoint.z, out float distance, out Vector3 nearest);
            if (distance <= (width + route.width) * .5f + 2f && Mathf.Abs(nearest.y - endpoint.y) * YQGeneratedWorldTerrain.TerrainHeight <= .5f) return true;
        }
        var rays = new List<YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation>();
        var coordinate = new Vector2Int(Mathf.FloorToInt((endpoint.x + 512f) / 128f), Mathf.FloorToInt((endpoint.z + 512f) / 128f));
        YQContinuousWorldFeatureAuthority.GetAcceptedRouteContinuations(prepared, coordinate, 128f, width * .5f + 2f, rays);
        foreach (var ray in rays)
        {
            Vector2 delta = new Vector2(endpoint.x, endpoint.z) - ray.origin;
            float along = Vector2.Dot(delta, ray.direction);
            float perpendicular = (delta - ray.direction * along).magnitude;
            if (along >= 0f && along <= YQContinuousWorldFeatureAuthority.AcceptedContinuationMaxDistance &&
                perpendicular <= (width + ray.width) * .5f + 2f &&
                Mathf.Abs(endpoint.y - Mathf.Clamp01(ray.elevation + along * ray.elevationSlope)) * YQGeneratedWorldTerrain.TerrainHeight <= .5f) return true;
        }
        return false;
    }

    private float ApplyAcceptedRouteModifier(
        float worldX,
        float worldZ,
        float normalizedHeight)
    {
        if (acceptedMaterialization == null || acceptedMaterialization.RouteCount <= 0)
            return normalizedHeight;

        // note: Use the same core-first projection as staged continuation routes, preserving deterministic canonical-order ties.
        float bestMask = 0f, bestCoreDistance = float.PositiveInfinity;
        float bestElevation = normalizedHeight;
        for (int routeIndex = 0; routeIndex < acceptedMaterialization.RouteCount; routeIndex++)
        {
            var route = acceptedMaterialization.GetRoute(routeIndex);
            if (pureAcceptedProjection && candidateRouteIds != null && candidateRouteIds.Contains(route.routeId)) continue;
            MeasureAcceptedRoute(routeIndex, worldX, worldZ, normalizedHeight, out float distance, out float elevation);
            float core = Mathf.Max(1f, route.width * .5f + route.shoulderWidth);
            float candidate = YQContinuousWorldFeatureAuthority.SampleAcceptedRouteGrade(normalizedHeight, elevation,
                originHeight, route.width * .5f, route.shoulderWidth, distance);
            if (distance <= core)
            {
                float score = distance / core;
                if (score < bestCoreDistance) { bestCoreDistance = score; bestElevation = candidate; }
            }
            else if (float.IsPositiveInfinity(bestCoreDistance) && Mathf.Abs(candidate - normalizedHeight) > bestMask)
            { bestMask = Mathf.Abs(candidate - normalizedHeight); bestElevation = candidate; }
        }

        return bestElevation;
    }

    private float SampleAcceptedRouteModifier(int routeIndex, float worldX, float worldZ, float originalHeight)
    {
        MeasureAcceptedRoute(routeIndex, worldX, worldZ, originalHeight, out float distance, out float elevation);
        var route = acceptedMaterialization.GetRoute(routeIndex);
        return YQContinuousWorldFeatureAuthority.SampleAcceptedRouteGrade(originalHeight,
            elevation, originHeight, route.width * .5f, route.shoulderWidth, distance);
    }

    internal float SampleRoadPaintWeight(float x, float z)
    {
        // note: Only accepted roads receive packed-earth paint, with a smooth two-metre terrain shoulder beyond the ribbon.
        if (acceptedMaterialization == null) return 0f;
        float weight = 0f;
        for (int i = 0; i < acceptedMaterialization.RouteCount; i++)
        {
            MeasureAcceptedRoute(i, x, z, 0f, out float distance, out _);
            float halfWidth = Mathf.Max(1f, acceptedMaterialization.GetRoute(i).width * .5f);
            weight = Mathf.Max(weight, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfWidth * .65f, halfWidth + 2f, distance)));
        }
        return weight;
    }

    private void MeasureAcceptedRoute(int routeIndex, float worldX, float worldZ, float originalHeight, out float routeDistance, out float routeElevation)
    {
        // note: Terrain and path paint share finite and permitted terminal footprints without allocating per sample.
        routeDistance = float.MaxValue; routeElevation = originalHeight;
        Vector2 sample = new Vector2(worldX, worldZ);
        if (acceptedRouteBounds != null && routeIndex < acceptedRouteBounds.Length)
        {
            AcceptedRouteBounds bounds = acceptedRouteBounds[routeIndex];
            if (sample.x < bounds.minX - bounds.padding || sample.x > bounds.maxX + bounds.padding ||
                sample.y < bounds.minZ - bounds.padding || sample.y > bounds.maxZ + bounds.padding)
                return;
        }
        int pointCount = acceptedMaterialization.GetRoutePointCount(routeIndex);
        if (pointCount < 2) return;
        for (int pointIndex = 0; pointIndex + 1 < pointCount; pointIndex++)
        {
            var first = acceptedMaterialization.GetRoutePoint(routeIndex, pointIndex);
            var second = acceptedMaterialization.GetRoutePoint(routeIndex, pointIndex + 1);
            Vector2 start = new Vector2(first.x, first.z);
            Vector2 delta = new Vector2(second.x - first.x, second.z - first.z);
            float denominator = delta.sqrMagnitude;
            float t = denominator > .0001f ? Mathf.Clamp01(Vector2.Dot(sample - start, delta) / denominator) : 0f;
            Vector2 nearest = start + delta * t;
            float distance = Vector2.Distance(sample, nearest);
            if (distance >= routeDistance) continue;
            routeDistance = distance;
            routeElevation = Mathf.Lerp(first.surfaceElevationNormalized, second.surfaceElevationNormalized, t);
        }
        var route = acceptedMaterialization.GetRoute(routeIndex);
        // note: Use the same terminal datum as network admission and ribbon projection; otherwise valid frontage routes fail terrain conformance.
        if (route.permittedBoundaryContinuation)
            for (int endpointIndex = 0; endpointIndex < 2; endpointIndex++)
            {
                var endpoint = acceptedMaterialization.GetRoutePoint(routeIndex, endpointIndex == 0 ? 0 : pointCount - 1);
                var adjacent = acceptedMaterialization.GetRoutePoint(routeIndex, endpointIndex == 0 ? 1 : pointCount - 2);
                Vector2 start = new Vector2(endpoint.x, endpoint.z);
                Vector2 delta = start - new Vector2(adjacent.x, adjacent.z);
                float length = delta.magnitude;
                if (length < .01f) continue;
                Vector2 direction = delta / length;
                if (!YQContinuousWorldFeatureAuthority.TryGetOutwardBoundaryDistance(start.x, start.y, direction, out _)) continue;
                float along = Mathf.Clamp(Vector2.Dot(sample - start, direction), 0f, YQContinuousWorldFeatureAuthority.AcceptedContinuationMaxDistance);
                float distance = Vector2.Distance(sample, start + direction * along);
                if (distance >= routeDistance) continue;
                routeDistance = distance;
                routeElevation = Mathf.Clamp01(endpoint.surfaceElevationNormalized + along * (endpoint.surfaceElevationNormalized - adjacent.surfaceElevationNormalized) / length);
            }
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
            // note: The compatibility sampler shares the same order-independent terrain envelope as the active V2 continuation path.
            return YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterTerrainModifiers(acceptedMaterialization,
                worldX, worldZ, normalizedHeight, originHeight, out float waterHeight) ? waterHeight : normalizedHeight;
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
        float mountainMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.82f, mountainNoise));
        float mountainRidges = Mathf.Pow(1f - Mathf.Abs(Mathf.PerlinNoise(worldX * 0.0024f + macroOffset.x - 3.1f, worldZ * 0.0024f + macroOffset.y + 8.6f) * 2f - 1f), 1.35f);
        float basins = Mathf.PerlinNoise(worldX * 0.0015f + biomeOffset.x + 5.2f, worldZ * 0.0015f + biomeOffset.y - 4.8f);
        float relief = mountainMask * mountainRidges * 0.24f - (1f - mountainMask) * (1f - basins) * 0.06f;
        return Mathf.Clamp01(0.20f + continental * 0.18f + hills * 0.09f + ridges * 0.12f + relief + SampleMountainRelief(worldX, worldZ));
    }

    private float SampleMountainRelief(float x, float z)
    {
        // note: One seeded massif per 3.84km region gives large-settlement-scale cadence without cell-local random state or origin regeneration.
        const float spacing = 3840f;
        int regionX = Mathf.FloorToInt(x / spacing), regionZ = Mathf.FloorToInt(z / spacing);
        float relief = 0f;
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
        {
            uint hash = StableHash(seedHash ^ unchecked((uint)(regionX + dx) * 73856093u) ^ unchecked((uint)(regionZ + dz) * 19349663u));
            float centerX = (regionX + dx + .25f + (hash & 65535u) / 65535f * .5f) * spacing;
            float centerZ = (regionZ + dz + .25f + (hash >> 16) / 65535f * .5f) * spacing;
            float radius = 640f + (StableHash(hash) & 65535u) / 65535f * 320f;
            float distance = Vector2.Distance(new Vector2(x, z), new Vector2(centerX, centerZ)) / radius;
            float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - distance));
            relief = Mathf.Max(relief, envelope * .48f);
        }
        return relief;
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

