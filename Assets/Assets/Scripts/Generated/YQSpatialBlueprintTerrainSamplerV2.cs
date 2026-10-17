using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public struct YQSpatialTerrainSampleV2
{
    public float elevationNormalized;
    public float ruggedness;
    public float waterMask;
    public float waterSurfaceNormalized;
    public int waterFeatureIndex;
    public float bankSupportMask;
    public float connectedBasinSupportMask;
    public float routeMask;
    public float siteReserveMask;
    public float caveMassMask;
    internal bool constrainFinalChannelElevation;
    internal float minimumChannelElevation;
    internal float maximumChannelElevation;
}

/// <summary>
/// Converts an accepted V2 blueprint into compact arrays once, then samples
/// terrain, water, routes, and buildable reserves without per-sample garbage.
/// </summary>
public sealed class YQSpatialBlueprintTerrainSamplerV2
{
    private const float DefaultSurface = 0.36f;
    private const float ApproximateTerrainHeightMetres = YQGeneratedWorldTerrain.TerrainHeight;
    private static string cachedMaterializedInput;
    private static YQSpatialBlueprintTerrainSamplerV2 cachedMaterializedSampler;
    private static string cachedTerrainInput;
    private static YQSpatialBlueprintTerrainSamplerV2 cachedTerrainSampler;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMaterializedCache()
    {
        cachedMaterializedInput = null;
        cachedMaterializedSampler = null;
        cachedTerrainInput = null; cachedTerrainSampler = null;
    }

    private static string MaterializedInput(GeneratedWorldPlanRecord plan) =>
        plan.worldSeed + "|" + plan.spatialPlanV2.generationVersion + "|" + JsonUtility.ToJson(plan.spatialPlanV2.blueprint);

    internal static IEnumerator WarmMaterializedWaterRoutine(GeneratedWorldPlanRecord plan,
        Func<bool> isCurrent, Action<bool, string> completed)
    {
        bool success = false;
        string failure = "Materialized water preparation was cancelled.";
        Task<Tuple<YQSpatialBlueprintTerrainSamplerV2, YQSpatialBlueprintTerrainSamplerV2, string>> work = null;
        var cancellation = new CancellationTokenSource();
        try
        {
            // note: Validation and Unity serialization stay on the main thread. The worker owns only a detached numeric sampler; it cannot publish world state, read assets, or write the live blueprint.
            if (plan == null || isCurrent == null || !isCurrent()) yield break;
            string sourceInput = MaterializedInput(plan);
            // note: Match the saved nullable location branches used by startup replay and exact-input validation caching.
            GeneratedWorldPlanRecord snapshot = YQSpatialMaterializationCompilerV2.StartupPreparation.CopySavedPlan(plan);
            if (!YQSpatialPlanVersionRouter.TryValidateAcceptedV2(snapshot, out failure)) yield break;
            // note: Canonical null-to-empty collections have identical geometry. Normalize only the detached warmup copy, retaining the exact original source for the stale-input guard.
            snapshot.spatialPlanV2.blueprint.EnsureCollections();
            string input = MaterializedInput(snapshot);
            if (cachedMaterializedSampler != null && string.Equals(input, cachedMaterializedInput, StringComparison.Ordinal))
            { success = true; failure = string.Empty; yield break; }
            work = Task.Run(() =>
            {
                // note: Source-array/basin construction is numeric work too; keeping it before the cache lookup repeated seconds of work on otherwise warm queries.
                if (!TryPrepareBlueprint(snapshot.spatialPlanV2.blueprint,
                    snapshot.spatialPlanV2.generationVersion == GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion,
                    out var numericSampler, out string sourceFailure))
                    return Tuple.Create<YQSpatialBlueprintTerrainSamplerV2, YQSpatialBlueprintTerrainSamplerV2, string>(null, null, sourceFailure);
                var terrainSource = numericSampler.CopyTerrainSource();
                bool ready = CompleteMaterializedWater(snapshot, ref numericSampler, out string reason, cancellation.Token);
                return Tuple.Create(ready ? numericSampler : null, terrainSource, reason);
            }, cancellation.Token);
            while (!work.IsCompleted)
            {
                if (!isCurrent()) yield break;
                yield return null;
            }
            if (!isCurrent() || !string.Equals(sourceInput, MaterializedInput(plan), StringComparison.Ordinal))
            { failure = "The accepted water inputs changed during preparation."; yield break; }
            if (work.IsFaulted)
            { failure = "Materialized water preparation failed: " + work.Exception.GetBaseException().Message; yield break; }
            if (work.IsCanceled) yield break;
            var result = work.Result;
            failure = result.Item3;
            if (result.Item1 == null) yield break;
            // note: Cache only completed numeric geometry with an exact input snapshot. TryPrepare still repeats its existing accepted-artifact validation before reuse.
            cachedMaterializedSampler = result.Item1;
            cachedMaterializedInput = input;
            cachedTerrainInput = input; cachedTerrainSampler = result.Item2;
            success = true;
        }
        finally
        {
            cancellation.Cancel();
            if (work != null && !work.IsCompleted)
                _ = work.ContinueWith(finished => { _ = finished.Exception; cancellation.Dispose(); }, TaskScheduler.Default);
            else cancellation.Dispose();
            completed?.Invoke(success, failure);
        }
    }

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
        public YQPreparedConnectedBasinV2 basin;
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
    private readonly YQPreparedConnectedBasinV2[] joinedLakes;
    private readonly bool usesNativeReaches;
    private readonly List<YQNativeWaterJoinPatchV2> nativeJoins = new List<YQNativeWaterJoinPatchV2>();
    private YQChannelFootprintIndexV2 channelFootprint;
    // note: Readiness uses the same immutable lake and channel union as placement; no accepted records or query caches change.
    internal bool CoversVegetationRectangle(YQPreparedConnectedBasinV2 basin, Bounds bounds)
        => basin != null && basin.CoversVegetationRectangle(bounds, channelFootprint);
    internal bool TryFindVegetationDryCandidate(Bounds bounds, out Vector2 candidate)
    {
        // note: A bounded geometric candidate rescues thin dry remnants missed by hashed scatter. The caller still applies every grounding, habitat, route, and reserve check.
        for (int index = 0; index < waters.Length; index++)
        {
            var basin = GetMaterializedBasin(index);
            if (basin == null || basin.InspectVegetationRectangle(bounds, channelFootprint, out candidate) ||
                float.IsNaN(candidate.x) || float.IsNaN(candidate.y)) continue;
            if (Sample(candidate.x, candidate.y).waterMask < .12f) return true;
        }
        candidate = default; return false;
    }
    public int NativeJoinCount => nativeJoins.Count;
    public YQNativeWaterJoinPatchV2 GetNativeJoin(int index) => nativeJoins[index];
    internal bool UsesSharedChannelProjection { get; }
    internal bool UsesCanonicalChannelBanks { get; }
    internal bool UsesCanonicalWetFootprint { get; }

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
        joinedLakes = new YQPreparedConnectedBasinV2[waters.Length];
        // note: Pin the new shared projection to its persisted domain version, leaving accepted native-version-2 and legacy-version-1 worlds on their prior streaming behavior.
        for (int index = 0; index < waters.Length; index++)
        {
            if (waters[index].basin != null && waters[index].basin.Version >= YQPreparedConnectedBasinV2.NativeChannelReachVersion)
                usesNativeReaches = true;
            if (waters[index].basin != null && waters[index].basin.Version >= YQPreparedConnectedBasinV2.SharedChannelProjectionVersion)
                UsesSharedChannelProjection = true;
            if (waters[index].basin != null && waters[index].basin.Version >= YQPreparedConnectedBasinV2.CanonicalChannelBanksVersion)
                UsesCanonicalChannelBanks = true;
            if (waters[index].basin != null && waters[index].basin.Version >= YQPreparedConnectedBasinV2.CanonicalWetFootprintVersion)
                UsesCanonicalWetFootprint = true;
        }
    }

    private YQSpatialBlueprintTerrainSamplerV2 CopyTerrainSource()
    {
        // note: Source arrays are immutable after blueprint preparation. Separate join/mask buffers preserve the original height-source semantics and avoid reconstructing connected basins at every bind.
        return new YQSpatialBlueprintTerrainSamplerV2(regions, fields, fieldPoints, waters, waterPoints, routes, routePoints, reserves);
    }

    public static bool TryPrepare(
        GeneratedWorldPlanRecord plan,
        out YQSpatialBlueprintTerrainSamplerV2 sampler,
        out string failure)
    {
        sampler = null;
        // note: A cache hit still requires the complete existing acceptance gate, but must not rebuild the derived source arrays and connected basins before reuse.
        if (!YQSpatialPlanVersionRouter.TryValidateAcceptedV2(plan, out failure)) return false;
        // note: Preserve the source preparation's existing collection initialization before comparing numeric inputs; normalization cannot cause a false cold rebuild.
        plan.spatialPlanV2.blueprint.EnsureCollections();
        string input = MaterializedInput(plan);
        if (cachedMaterializedSampler != null && string.Equals(input, cachedMaterializedInput, StringComparison.Ordinal))
        {
            sampler = cachedMaterializedSampler;
            return true;
        }
        if (!TryPrepareBlueprint(plan.spatialPlanV2.blueprint,
            plan.spatialPlanV2.generationVersion == GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion,
            out sampler, out failure)) return false;
        var terrainSource = sampler.CopyTerrainSource();
        if (!CompleteMaterializedWater(plan, ref sampler, out failure, CancellationToken.None)) return false;
        cachedTerrainSampler = terrainSource; cachedTerrainInput = MaterializedInput(plan);
        cachedMaterializedSampler = sampler;
        cachedMaterializedInput = MaterializedInput(plan);
        return true;
    }

    private static bool CompleteMaterializedWater(GeneratedWorldPlanRecord plan,
        ref YQSpatialBlueprintTerrainSamplerV2 sampler, out string failure, CancellationToken cancellation)
    {
        failure = string.Empty;
        cancellation.ThrowIfCancellationRequested();
        YQSpatialBlueprintV2 blueprint = plan.spatialPlanV2.blueprint;
        // note: Freeze the native join only after routes/reserves and the seed detail profile are known; preparation never edits accepted records.
        var finalBed = YQGeneratedWorldTerrain.CreatePreparedV2HeightSampler(plan, sampler);
        for (int index = 0; index < sampler.waters.Length; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var basin = sampler.waters[index].basin;
            if (basin == null || basin.Version < YQPreparedConnectedBasinV2.JoinedNativeLakeVersion) continue;
            var domain = blueprint.hydrology[index].connectedDomain;
            if (!YQPreparedConnectedBasinV2.TryProjectNativeLake(basin, domain.seedX, domain.seedZ,
                finalBed.SampleNormalized, out sampler.joinedLakes[index], out failure))
            { sampler = null; return false; }
        }
        var channels = new List<int>();
        for (int index = 0; index < sampler.waters.Length; index++)
            if (sampler.waters[index].kind == YQHydrologyKindV2.River || sampler.waters[index].kind == YQHydrologyKindV2.Waterfall)
                channels.Add(index);
        bool nativeReaches = false;
        foreach (var water in sampler.waters)
            if (water.basin != null && water.basin.Version >= YQPreparedConnectedBasinV2.NativeChannelReachVersion) nativeReaches = true;
        if (nativeReaches)
        {
            if (!sampler.PrepareNativeReaches(channels.ToArray(), finalBed.SampleNormalized, out failure, cancellation))
            { sampler = null; return false; }
        }
        else
        {
            // note: Archived version-7 worlds keep their bounded join projection exactly; only new version-8 domains opt into complete finite reaches.
            for (int index = 0; index < sampler.joinedLakes.Length; index++)
                if (sampler.joinedLakes[index] != null && sampler.waters[index].basin.Version >= YQPreparedConnectedBasinV2.NativeChannelJoinVersion)
                    sampler.nativeJoins.Add(new YQNativeWaterJoinPatchV2(sampler.joinedLakes[index], channels.ToArray(), sampler.SampleNativeChannelNode));
        }
        if (sampler.nativeJoins.Count > 0) sampler.PrepareChannelFootprint();
        cancellation.ThrowIfCancellationRequested();
        return true;
    }

    private bool PrepareNativeReaches(int[] channels, Func<float, float, float> sourceBed, out string failure, CancellationToken cancellation)
    {
        // note: Representation tiles cover accepted bank segments and their native interpolation support. Global coordinates, channel order and source heights determine every tile independently of traversal or render-cell arrival.
        const float tileSize = 256f, support = 4f;
        var tiles = new Dictionary<Vector2Int, HashSet<int>>();
        foreach (int channel in channels)
        {
            cancellation.ThrowIfCancellationRequested();
            var water = waters[channel];
            for (int point = 1; point < water.pointCount; point++)
            {
                var a = waterPoints[water.pointStart + point - 1]; var b = waterPoints[water.pointStart + point];
                Vector3 sideA = GetAcceptedChannelBankOffset(channel, point - 1), sideB = GetAcceptedChannelBankOffset(channel, point);
                float minX = Mathf.Min(a.x - Mathf.Abs(sideA.x), b.x - Mathf.Abs(sideB.x)) - support;
                float maxX = Mathf.Max(a.x + Mathf.Abs(sideA.x), b.x + Mathf.Abs(sideB.x)) + support;
                float minZ = Mathf.Min(a.z - Mathf.Abs(sideA.z), b.z - Mathf.Abs(sideB.z)) - support;
                float maxZ = Mathf.Max(a.z + Mathf.Abs(sideA.z), b.z + Mathf.Abs(sideB.z)) + support;
                for (int z = Mathf.FloorToInt(minZ / tileSize); z <= Mathf.FloorToInt(maxZ / tileSize); z++)
                for (int x = Mathf.FloorToInt(minX / tileSize); x <= Mathf.FloorToInt(maxX / tileSize); x++)
                {
                    Vector2 first = new Vector2(a.x, a.z), second = new Vector2(b.x, b.z);
                    Vector2 offsetA = new Vector2(sideA.x, sideA.z), offsetB = new Vector2(sideB.x, sideB.z);
                    if (!TriangleTouchesRectangle(first - offsetA, second - offsetB, first + offsetA,
                        x * tileSize - support, z * tileSize - support, (x + 1) * tileSize + support, (z + 1) * tileSize + support) &&
                        !TriangleTouchesRectangle(first + offsetA, second - offsetB, second + offsetB,
                        x * tileSize - support, z * tileSize - support, (x + 1) * tileSize + support, (z + 1) * tileSize + support)) continue;
                    var key = new Vector2Int(x, z);
                    if (!tiles.TryGetValue(key, out var owners)) { owners = new HashSet<int>(); tiles.Add(key, owners); }
                    owners.Add(channel);
                }
            }
        }
        var ordered = new List<Vector2Int>(tiles.Keys);
        ordered.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        foreach (var tile in ordered)
        {
            cancellation.ThrowIfCancellationRequested();
            var owners = new List<int>(tiles[tile]); owners.Sort();
            nativeJoins.Add(new YQNativeWaterJoinPatchV2(129, tile.x * tileSize, tile.y * tileSize, 2f,
                sourceBed, joinedLakes, owners.ToArray(), SampleNativeChannelNode));
        }
        return YQNativeWaterJoinPatchV2.TryFreezeConnectedReaches(nativeJoins, out failure);
    }

    private static bool TriangleTouchesRectangle(Vector2 a, Vector2 b, Vector2 c, float minX, float minZ, float maxX, float maxZ)
    {
        // note: Exact separating axes discard dry corners of kilometre-long diagonal reach bounds without dropping their native support halo.
        if (Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < minX || Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > maxX ||
            Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < minZ || Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > maxZ) return false;
        bool Separated(Vector2 first, Vector2 second)
        {
            Vector2 axis = new Vector2(first.y - second.y, second.x - first.x);
            float pa = Vector2.Dot(axis, a), pb = Vector2.Dot(axis, b), pc = Vector2.Dot(axis, c);
            float center = axis.x * (minX + maxX) * .5f + axis.y * (minZ + maxZ) * .5f;
            float radius = Mathf.Abs(axis.x) * (maxX - minX) * .5f + Mathf.Abs(axis.y) * (maxZ - minZ) * .5f;
            return Mathf.Max(pa, Mathf.Max(pb, pc)) < center - radius || Mathf.Min(pa, Mathf.Min(pb, pc)) > center + radius;
        }
        return !Separated(a, b) && !Separated(b, c) && !Separated(c, a);
    }

    private void PrepareChannelFootprint()
    {
        // note: Freeze finite accepted bank quads outside native replacements and append their exact wet polygons. This cache cannot alter source heights or accepted records.
        var triangles = new List<YQChannelFootprintIndexV2.Triangle>();
        for (int index = 0; index < waters.Length; index++)
        {
            var water = waters[index];
            if (water.kind != YQHydrologyKindV2.River && water.kind != YQHydrologyKindV2.Waterfall) continue;
            if (usesNativeReaches) continue;
            for (int point = 1; point < water.pointCount; point++)
            {
                var first = waterPoints[water.pointStart + point - 1]; var second = waterPoints[water.pointStart + point];
                Vector3 offsetA = GetAcceptedChannelBankOffset(index, point - 1), offsetB = GetAcceptedChannelBankOffset(index, point);
                Vector2 a = new Vector2(first.x, first.z), b = new Vector2(second.x, second.z);
                Vector2 sideA = new Vector2(offsetA.x, offsetA.z), sideB = new Vector2(offsetB.x, offsetB.z);
                YQChannelFootprintIndexV2.AddSourceTriangle(triangles, a - sideA, b - sideB, a + sideA, index, nativeJoins);
                YQChannelFootprintIndexV2.AddSourceTriangle(triangles, a + sideA, b - sideB, b + sideB, index, nativeJoins);
            }
        }
        for (int join = 0; join < nativeJoins.Count; join++) nativeJoins[join].AppendChannelFootprint(triangles, nativeJoins, join + 1);
        channelFootprint = new YQChannelFootprintIndexV2(triangles);
    }

    internal bool TryMeasureMaterializedChannelDistance(float x, float z, out int waterIndex, out float distance)
    {
        waterIndex = -1; distance = float.PositiveInfinity;
        return channelFootprint != null && channelFootprint.TryMeasure(x, z, out waterIndex, out distance);
    }

    internal static bool TryPrepareTerrainSource(GeneratedWorldPlanRecord plan,
        out YQSpatialBlueprintTerrainSamplerV2 sampler, out string failure)
    {
        sampler = null;
        // note: Height publication needs accepted source geometry, not another materialized water cache. Retain the same router/validation gate while omitting the rebuildable join preparation.
        if (!YQSpatialPlanVersionRouter.TryValidateAcceptedV2(plan, out failure)) return false;
        plan.spatialPlanV2.blueprint.EnsureCollections();
        string input = MaterializedInput(plan);
        if (cachedTerrainSampler != null && string.Equals(input, cachedTerrainInput, StringComparison.Ordinal))
        { sampler = cachedTerrainSampler; return true; }
        if (!TryPrepareBlueprint(plan.spatialPlanV2.blueprint,
            plan.spatialPlanV2.generationVersion == GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion,
            out sampler, out failure)) return false;
        cachedTerrainSampler = sampler; cachedTerrainInput = MaterializedInput(plan);
        return true;
    }

    internal static YQSpatialBlueprintTerrainSamplerV2 PrepareDraftGeology(YQSpatialBlueprintV2 draft)
    {
        // note: The compiler alone samples its unaccepted geological stage; this cannot admit unvalidated hydrology or sites to the runtime path.
        var geology = new YQSpatialBlueprintV2 { worldSize = draft.worldSize,
            regions = draft.regions, terrainFields = draft.terrainFields };
        if (!TryPrepareBlueprint(geology, false, out var sampler, out string failure))
            throw new InvalidOperationException(failure);
        return sampler;
    }

    internal float SampleDraftGeology(float x, float z) => EvaluateRawSurface(x, z, out _);

    private static bool TryPrepareBlueprint(YQSpatialBlueprintV2 blueprint, bool connectedDomains,
        out YQSpatialBlueprintTerrainSamplerV2 sampler, out string failure)
    {
        sampler = null;
        failure = string.Empty;
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
            YQPreparedConnectedBasinV2 domain = null;
            if (connectedDomains && source.hasConnectedDomain &&
                !YQPreparedConnectedBasinV2.TryPrepare(source.connectedDomain, source.waterLevelNormalized, out domain, out failure))
                return false;
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
                width = YQSpatialMaterializationCompilerV2.ResolveWaterWidth(source.kind, source.nominalWidth),
                depth = YQSpatialMaterializationCompilerV2.ResolveWaterDepth(source.kind, source.nominalDepth),
                pointStart = pointStart,
                pointCount = waterPointCursor - pointStart,
                basin = domain
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
        var sample = SampleTerrainSource(worldX, worldZ);
        // note: Placement and materialization consume the same derived native wet polygons; retain the height-source mask separately to avoid geometry feeding back into detail/smoothing.
        for (int index = 0; index < joinedLakes.Length; index++)
        {
            var basin = joinedLakes[index];
            if (basin == null) continue;
            if (sample.waterFeatureIndex == index)
            { sample.waterMask = 0f; sample.waterFeatureIndex = -1; sample.waterSurfaceNormalized = 0f; }
            if (basin.TrySampleBed(worldX, worldZ, out _, out bool inside) && inside)
            { sample.waterMask = 1f; sample.waterFeatureIndex = index; sample.waterSurfaceNormalized = waters[index].level; }
        }
        bool sourceChannelCleared = false;
        foreach (var join in nativeJoins)
        {
            if (!join.Contains(worldX, worldZ)) continue;
            if ((!usesNativeReaches || !sourceChannelCleared) && sample.waterFeatureIndex >= 0 && (waters[sample.waterFeatureIndex].kind == YQHydrologyKindV2.River ||
                waters[sample.waterFeatureIndex].kind == YQHydrologyKindV2.Waterfall))
            { sample.waterMask = 0f; sample.waterFeatureIndex = -1; sample.waterSurfaceNormalized = 0f; }
            // note: Closed neighbouring reach tiles share edges. Clear the old source once, so a dry incident triangle cannot erase a wet polygon owned by the adjacent tile.
            sourceChannelCleared = true;
            if (sample.waterMask < .999f && join.TrySample(worldX, worldZ, out int owner, out float level))
            { sample.waterMask = 1f; sample.waterFeatureIndex = owner; sample.waterSurfaceNormalized = level; }
        }
        return sample;
    }

    public YQPreparedConnectedBasinV2 GetMaterializedBasin(int index)
        => joinedLakes[index] ?? waters[index].basin;

    internal YQPreparedConnectedBasinV2 GetTerrainSourceBasin(int index) => waters[index].basin;

    internal YQSpatialTerrainSampleV2 SampleTerrainSource(float worldX, float worldZ)
    {
        float height = EvaluateRawSurface(worldX, worldZ,
            out float ruggedness);
        float waterMask = 0f;
        float waterSurface = 0f;
        int waterFeatureIndex = -1;
        float waterBed = 0f;
        float bankSupportMask = 0f;
        float waterTerrainBlendMask = 0f;
        bool waterTerrainCore = false;
        float connectedBasinSupportMask = 0f;
        float connectedBasinBed = 0f;
        bool connectedBasinNativeBed = false;
        var riverEnvelope = new YQContinuousWorldFeatureAuthority.AcceptedWaterTerrainEnvelope();

        for (int index = 0; index < waters.Length; index++)
        {
            PreparedWater water = waters[index];
            if (water.pointCount <= 0)
            {
                continue;
            }

            if (water.basin != null)
            {
                // note: Connected water uses its accepted geological bed, including dry islands; it never acquires a radial lake trench.
                if (!water.basin.TrySampleBed(worldX, worldZ, out float basinBed, out bool insideBasin)) continue;
                float shore = water.basin.SignedShoreDistance(worldX, worldZ);
                float support = YQGeneratedWorldTerrain.WorldSize / (YQGeneratedWorldTerrain.HeightmapResolution - 1f) * 1.414214f;
                if (insideBasin || shore <= support + 2f)
                {
                    height = basinBed;
                    connectedBasinSupportMask = 1f;
                    connectedBasinBed = basinBed;
                    connectedBasinNativeBed = water.basin.Version >= YQPreparedConnectedBasinV2.NativeDiagonalVersion;
                }
                if (insideBasin)
                {
                    waterMask = 1f;
                    waterFeatureIndex = index;
                    waterSurface = water.level;
                    waterBed = basinBed;
                    waterTerrainBlendMask = 1f;
                    waterTerrainCore = true;
                    bankSupportMask = 0f;
                }
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
            float ribbonOutside = float.PositiveInfinity;
            if (water.pointCount > 1 && water.kind != YQHydrologyKindV2.Lake && water.kind != YQHydrologyKindV2.Wetland)
                SampleRibbonFootprint(water, worldX, worldZ, out distance, out radius, out pointElevation, out ribbonOutside);
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
            if (UsesCanonicalWetFootprint && (water.kind == YQHydrologyKindV2.River || water.kind == YQHydrologyKindV2.Waterfall))
                // note: Water exclusion follows actual accepted triangles, while bank-support metadata still protects the separate dry grade.
                footprintMask = ribbonOutside <= .001f ? 1f : 0f;
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
                featureSurface = ResolveReceivingSurface(worldX, worldZ, radius, featureSurface);
            }
            float depth = Mathf.Max(.6f, water.depth);
            float crossSection = Mathf.Clamp01(distance / Mathf.Max(.5f, radius - bankPadding));
            // note: A concave cross-section rises from a submerged centre to a bank just above the water plane.
            float bed = featureSurface + (-depth + (depth + .2f) * crossSection * crossSection) / ApproximateTerrainHeightMetres;
            // note: River water occupies the full accepted ribbon; an above-water rim inside that ribbon shortened its visible width and interrupted flow over hills.
            if (water.kind == YQHydrologyKindV2.River || water.kind == YQHydrologyKindV2.Waterfall)
                bed = YQContinuousWorldFeatureAuthority.SampleAcceptedWaterBed(
                    ApproximateTerrainHeightMetres, featureSurface, depth, radius, distance);
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
            float shorelineTransition = MaximumOfThree(10f, radius * .8f, slopeLimitedTransition);
            if (water.kind == YQHydrologyKindV2.River || water.kind == YQHydrologyKindV2.Waterfall)
            {
                // note: A river crossing a ridge receives a broad, deterministic ravine cut so the water has a continuous way through the terrain instead of a floating ribbon or sheer wall.
                float ridgeCutTransition = riseMetres / Mathf.Max(.1f, Mathf.Tan(12f * Mathf.Deg2Rad));
                shorelineTransition = MaximumOfThree(
                    shorelineTransition,
                    radius * 3.5f + 8f,
                    ridgeCutTransition);
            }
            float shorelineMask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius + bankPadding, radius + bankPadding + shorelineTransition, distance));
            if (water.kind == YQHydrologyKindV2.River || water.kind == YQHydrologyKindV2.Waterfall)
            {
                // note: Finite and streamed rivers use the identical cut/fill bank profile; keep the wet mask above independent of this wider dry terrain transition.
                if (UsesCanonicalWetFootprint)
                {
                    // note: A native interpolation cushion cannot own dry land beyond the rendered triangle footprint.
                    riverEnvelope.AddCanonicalChannel(ApproximateTerrainHeightMetres, featureSurface, depth, radius, distance, ribbonOutside);
                }
                else
                    riverEnvelope.Add(ApproximateTerrainHeightMetres, featureSurface, depth, radius, distance);
                continue;
            }
            bool featureCore = distance <= radius + bankPadding + 2f;
            if ((featureCore && !waterTerrainCore) || (featureCore == waterTerrainCore &&
                (shorelineMask > waterTerrainBlendMask || (shorelineMask >= .999f && bed < waterBed))))
            {
                // note: Keep the winning water bed paired with its blend mask when rivers and receiving bodies overlap.
                waterTerrainBlendMask = shorelineMask;
                waterBed = bed;
                waterTerrainCore = featureCore;
            }
            if (waterTerrainCore && !featureCore) continue;
            height = Mathf.Lerp(
                height,
                // note: Cut or fill to the accepted channel datum so the water cannot bridge an unrelated depression.
                waterTerrainBlendMask >= .999f ? Mathf.Min(bed, waterBed) : bed,
                shorelineMask);
        }

        bool applyRiverEnvelope = riverEnvelope.Influenced && (!waterTerrainCore || riverEnvelope.HasWetCore);
        if (applyRiverEnvelope)
        {
            // note: One projection joins river banks continuously; a dry river shoulder cannot replace an accepted lake's wet basin.
            height = riverEnvelope.Project(height);
        }

        float routeMask = 0f;
        float routeElevation = height;
        float strongestRouteCorrection = 0f;
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
            float grade = YQContinuousWorldFeatureAuthority.SampleAcceptedRouteGrade(height, elevation,
                ApproximateTerrainHeightMetres, route.width * .5f, route.shoulderWidth, distance);
            float correction = Mathf.Abs(grade - height);
            if (correction > strongestRouteCorrection)
            {
                strongestRouteCorrection = correction;
                routeElevation = grade;
            }
            if (mask > routeMask)
            {
                routeMask = mask;
            }
        }
        height = routeElevation;

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
        // note: Preserve the original final water owner, but constrain the current road/reserve surface instead of restoring its earlier height. Reapplying the lake blend here would change dry shores even where no road or reserve acted.
        if (applyRiverEnvelope)
            height = riverEnvelope.Project(height);
        else
            height = Mathf.Lerp(height, waterBed, waterTerrainBlendMask);
        // note: The accepted bed owns both sides of a connected shore. Generic reserve/road flattening and dry channel shoulders must not move that contour; explicitly wet channel cuts retain their existing projection.
        if (connectedBasinSupportMask > 0f && !riverEnvelope.HasWetCore)
            height = connectedBasinBed;
        else if (connectedBasinSupportMask > 0f && connectedBasinNativeBed)
            // note: Channel apertures may cut the new accepted basin, but a river's above-water bank projection must never fill its wet bed or raise its shared shore; stored version-1 geometry retains its previous projection.
            height = Mathf.Min(height, connectedBasinBed);

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
            connectedBasinSupportMask = connectedBasinSupportMask,
            routeMask = Mathf.Clamp01(routeMask),
            siteReserveMask = Mathf.Clamp01(reserveMask),
            caveMassMask = Mathf.Clamp01(caveMask),
            // note: New finite native reaches retain the existing accepted channel interval through final detail/smoothing. Rebuildable sample metadata does not change serialized controls or historical terrain.
            constrainFinalChannelElevation = usesNativeReaches && applyRiverEnvelope && connectedBasinSupportMask <= 0f,
            minimumChannelElevation = usesNativeReaches && applyRiverEnvelope ? riverEnvelope.Project(0f) : 0f,
            maximumChannelElevation = usesNativeReaches && applyRiverEnvelope ? riverEnvelope.Project(1f) : 1f
        };
    }

    // note: Match Mathf.Max(params float[]) in its original comparison order, including NaN and signed zero, without a temporary array per shoreline sample.
    private static float MaximumOfThree(float first, float second, float third)
    {
        if (second > first) first = second;
        if (third > first) first = third;
        return first;
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
            if (field.kind == YQTerrainFieldKindV2.Escarpment || field.kind == YQTerrainFieldKindV2.BasinRim)
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
            float mask = FalloffMask(distance, fieldRadius, roundedRelief || field.kind == YQTerrainFieldKindV2.Escarpment || field.kind == YQTerrainFieldKindV2.BasinRim ? 1f : field.falloff);
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
                case YQTerrainFieldKindV2.BasinRim:
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
        // note: Enclosing relief follows the geological valley/basin cut, avoiding a low corridor that silently breaches a draft lake before its explicit spill is selected.
        if (kind == YQTerrainFieldKindV2.BasinRim) return 6;
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

    public bool TrySampleAcceptedChannelSurface(float x, float z, float referenceLevel,
        out int channelIndex, out float surface)
    {
        bool native = false; channelIndex = -1; surface = 0f;
        if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z) ||
            float.IsNaN(referenceLevel) || float.IsInfinity(referenceLevel)) return false;
        foreach (var join in nativeJoins)
        {
            if (!join.Contains(x, z)) continue;
            native = true;
            if (join.TrySample(x, z, out channelIndex, out surface)) return true;
        }
        if (native) return false;
        return TrySampleTerrainSourceChannelSurface(x, z, referenceLevel, out channelIndex, out surface);
    }

    private YQNativeChannelNodeV2 SampleNativeChannelNode(int index, float x, float z)
    {
        // note: The accepted ribbon still supplies reach intent and the source level profile; only native contact and shared lake-edge ownership are derived after terrain interpolation.
        var water = waters[index];
        SampleRibbonFootprint(water, x, z, out _, out float radius, out float elevation, out float outside);
        return new YQNativeChannelNodeV2 { surface = ResolveReceivingSurface(x, z, radius, elevation), core = outside <= .001f };
    }

    public bool TrySampleTerrainSourceChannelSurface(float x, float z, float referenceLevel,
        out int channelIndex, out float surface)
    {
        // note: Query canonical ribbon triangles independently of the winning semantic mask. Lake ownership cannot hide a channel opening; terrain padding and rounded endpoint caps are not water coverage. Final materialized meshes still require their own verification.
        channelIndex = -1;
        surface = 0f;
        if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z) ||
            float.IsNaN(referenceLevel) || float.IsInfinity(referenceLevel)) return false;
        float nearestLevel = float.PositiveInfinity;
        for (int index = 0; index < waters.Length; index++)
        {
            var water = waters[index];
            if (water.pointCount < 2 || (water.kind != YQHydrologyKindV2.River && water.kind != YQHydrologyKindV2.Waterfall)) continue;
            SampleRibbonFootprint(water, x, z, out _, out float radius, out float elevation, out float outside);
            if (outside > .001f) continue;
            float level = ResolveReceivingSurface(x, z, radius, elevation);
            float difference = Mathf.Abs(level - referenceLevel);
            if (difference >= nearestLevel) continue;
            nearestLevel = difference;
            channelIndex = index;
            surface = level;
        }
        return channelIndex >= 0;
    }

    internal Vector3 GetAcceptedChannelBankOffset(int waterIndex, int pointIndex)
    {
        // note: All terrain and surface owners consume the same immutable control frame and effective width. Subdivision interpolates this offset, never recalculates a tangent.
        var water = waters[waterIndex];
        var point = waterPoints[water.pointStart + pointIndex];
        float radius = Mathf.Max(.75f, Mathf.Max(water.width, point.width) * .5f);
        return new Vector3(point.waterSide.x * radius, 0f, point.waterSide.y * radius);
    }

    internal void AccumulateAcceptedChannelEnvelope(int waterIndex, float x, float z, float terrainHeight,
        bool wetOnly, ref YQContinuousWorldFeatureAuthority.AcceptedWaterTerrainEnvelope envelope)
    {
        // note: The existing streaming owner consumes this sampler's immutable ribbon footprint and receiving datum. It must not reconstruct a radial channel or an unblended surface from control points.
        if (waterIndex < 0 || waterIndex >= waters.Length) return;
        var water = waters[waterIndex];
        if (water.pointCount < 2 || (water.kind != YQHydrologyKindV2.River && water.kind != YQHydrologyKindV2.Waterfall)) return;
        SampleRibbonFootprint(water, x, z, out float distance, out float radius, out float surface, out float outside);
        float padding = YQGeneratedWorldTerrain.WorldSize / (YQGeneratedWorldTerrain.HeightmapResolution - 1f) * 1.414214f;
        if (wetOnly && (UsesCanonicalWetFootprint ? outside > .001f : distance > radius + padding + 2f)) return;
        surface = ResolveReceivingSurface(x, z, radius, surface);
        if (UsesCanonicalWetFootprint)
            envelope.AddCanonicalChannel(terrainHeight, surface, Mathf.Max(.6f, water.depth), radius, distance, outside);
        else
            envelope.Add(terrainHeight, surface, Mathf.Max(.6f, water.depth), radius, distance);
    }

    private float ResolveReceivingSurface(float x, float z, float radius, float surface)
    {
        // note: Coverage diagnostics and terrain projection use the same receiving-level fade, retaining the existing feature order and arithmetic for accepted versions.
        float padding = YQGeneratedWorldTerrain.WorldSize / (YQGeneratedWorldTerrain.HeightmapResolution - 1f) * 1.414214f;
        for (int lakeIndex = 0; lakeIndex < waters.Length; lakeIndex++)
        {
            var lake = waters[lakeIndex];
            if (lake.pointCount == 0 || (lake.kind != YQHydrologyKindV2.Lake && lake.kind != YQHydrologyKindV2.Wetland)) continue;
            SampleAreaWaterFootprint(lake, x, z, out float distance, out float lakeRadius, Mathf.Max(20f, radius * 2f) + padding);
            float shoreDistance = Mathf.Max(0f, distance - lakeRadius - padding);
            float blend = 1f - Mathf.SmoothStep(0f, 1f, shoreDistance / Mathf.Max(20f, radius * 2f));
            float level = lake.level > 0f ? lake.level : waterPoints[lake.pointStart].elevation;
            surface = Mathf.Lerp(surface, level, blend);
        }
        return surface;
    }

    private void SampleRibbonFootprint(PreparedWater water, float x, float z, out float distance, out float radius, out float elevation)
        => SampleRibbonFootprint(water, x, z, out distance, out radius, out elevation, out _);

    private void SampleRibbonFootprint(PreparedWater water, float x, float z, out float distance, out float radius, out float elevation, out float outsideDistance)
    {
        // note: Barycentric sampling follows the exact two triangles rendered for each river segment, including their varying elevations and widths.
        Vector2 query = new Vector2(x, z);
        float best = float.PositiveInfinity;
        outsideDistance = float.PositiveInfinity;
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
                outsideDistance = outside;
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

    private void SampleAreaWaterFootprint(PreparedWater water, float x, float z, out float distance, out float radius, float fadeDistance = 0f)
    {
        if (water.basin != null)
        {
            // note: Existing confluence fades consume signed distance from the accepted contour instead of fitting another shore.
            radius = Mathf.Max(1f, water.width * .5f);
            float dx = Mathf.Max(0f, Mathf.Max(water.basin.MinimumX - x, x - water.basin.MaximumX));
            float dz = Mathf.Max(0f, Mathf.Max(water.basin.MinimumZ - z, z - water.basin.MaximumZ));
            if (dx * dx + dz * dz > fadeDistance * fadeDistance)
            {
                // note: Beyond the caller's complete fade range every shoreline contribution is zero; skip detailed contour walks for distant heightmap samples without limiting wide channels.
                distance = radius + Mathf.Sqrt(dx * dx + dz * dz);
                return;
            }
            distance = Mathf.Max(0f, radius + water.basin.SignedShoreDistance(x, z));
            return;
        }
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
