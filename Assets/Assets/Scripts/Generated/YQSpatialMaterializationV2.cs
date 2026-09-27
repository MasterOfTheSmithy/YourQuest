using System;
using System.Collections.Generic;
using UnityEngine;

public struct YQSpatialMaterializationRegionV2
{
    public string regionId;
    public float centerX;
    public float centerZ;
    public float radius;
    public float elevationBias;
    public float moisture;
    public float ruggedness;
    public float civilizationDensity;
    public float danger;
    public float forestDensity;
    public YQGeneratedWorldBiomeKind biome;
}

public readonly struct YQSpatialEcologySampleV2
{
    public readonly YQGeneratedWorldTileProfile tileProfile;
    public readonly YQSpatialTerrainSampleV2 terrain;
    public readonly float civilizationDensity;
    public readonly float danger;

    internal YQSpatialEcologySampleV2(
        YQGeneratedWorldTileProfile tileProfile,
        YQSpatialTerrainSampleV2 terrain,
        float civilizationDensity,
        float danger)
    {
        this.tileProfile = tileProfile;
        this.terrain = terrain;
        this.civilizationDensity = civilizationDensity;
        this.danger = danger;
    }
}

public struct YQSpatialMaterializationSiteV2
{
    public string siteId;
    public string sourceSemanticId;
    public string parentRegionId;
    public YQSiteKindV2 kind;
    public YQSitePlacementModeV2 placementMode;
    public float x;
    public float z;
    public float surfaceElevationNormalized;
    public float headingDegrees;
    public float reservedRadius;
    public float maximumSlopeDegrees;
    public string frontageRouteId;
    public float frontageX;
    public float frontageZ;
    public float routeAccess;
    public string nearestWaterId;
    public float nearestWaterDistance;
    public float waterAccess;
    public bool terrainReserveReady;
    public bool concealed;
    public bool behindFeature;
    public bool requiresReward;
    public bool requiresEncounter;
    public bool requiresTransition;
    public bool concealedAccess;

    // note: Carry the accepted multi-cell sector footprint into the immutable runtime projection without creating duplicate site owners.
    internal YQSiteMemberFootprintV2[] memberFootprint;
    public IReadOnlyList<YQSiteMemberFootprintV2> MemberFootprint =>
        memberFootprint ?? Array.Empty<YQSiteMemberFootprintV2>();

    // note: Carry accepted site tags into the prepared runtime projection so beta contracts keep their explicit category identity after compilation.
    internal string[] tags;
    public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();

    // note: Preserve every accepted functional requirement for settlement composition instead of dropping habitation, services and security after spatial planning.
    internal YQAssetFunctionV2[] requiredFunctions;
    public IReadOnlyList<YQAssetFunctionV2> RequiredFunctions =>
        requiredFunctions ?? Array.Empty<YQAssetFunctionV2>();
}

public struct YQSpatialMaterializationRoutePointV2
{
    public float x;
    public float z;
    public float surfaceElevationNormalized;
    public float width;
}

public struct YQSpatialMaterializationWaterPointV2
{
    public float x;
    public float z;
    public float waterSurfaceNormalized;
    public float width;
}

public struct YQSpatialMaterializationWaterV2
{
    public string hydrologyId;
    public string parentRegionId;
    public YQHydrologyKindV2 kind;
    public string sourceTerrainFieldId;
    public string sinkHydrologyId;
    public float waterLevelNormalized;
    public float nominalWidth;
    public float nominalDepth;
    internal int pointStart;
    internal int pointCount;
    // note: Persist broad accepted geometry bounds with the runtime projection so streamed cells can reject distant polylines without walking every point.
    internal bool hasSpatialBounds;
    internal float minimumX;
    internal float maximumX;
    internal float minimumZ;
    internal float maximumZ;
}

public struct YQSpatialMaterializationRouteV2
{
    public string routeId;
    public string sourceSemanticRouteId;
    public string fromSiteId;
    public string toSiteId;
    public YQRouteClassV2 routeClass;
    public float width;
    public float shoulderWidth;
    public float maximumGradeDegrees;
    internal int pointStart;
    internal int pointCount;
    // note: Keep route bounds beside the immutable point range for fast deterministic cell ownership queries.
    internal bool hasSpatialBounds;
    internal float minimumX;
    internal float maximumX;
    internal float minimumZ;
    internal float maximumZ;
}

public struct YQSpatialMaterializationCrossingV2
{
    public string crossingId;
    public string routeId;
    public string hydrologyId;
    public YQRouteCrossingKindV2 kind;
    public float x;
    public float z;
    public float requiredSpan;
}

/// <summary>
/// Immutable runtime projection of an accepted V2 blueprint. Materializers
/// consume this projection rather than independently rediscovering locations.
/// </summary>
public sealed class YQPreparedSpatialMaterializationV2
{
    private readonly struct SegmentRange
    {
        public readonly int firstSegment;
        public readonly int lastSegment;

        public SegmentRange(int firstSegment, int lastSegment)
        {
            this.firstSegment = firstSegment;
            this.lastSegment = lastSegment;
        }
    }

    // note: Index accepted geometry once so a newly streamed cell visits nearby segments instead of rescanning a long world-spanning river or road.
    private const float SegmentIndexCellSize = YQSemanticWorldAuthority.CellSizeMeters;
    private readonly YQSpatialMaterializationRegionV2[] regions;
    private readonly YQSpatialMaterializationSiteV2[] sites;
    private readonly YQSpatialMaterializationWaterV2[] waters;
    private readonly YQSpatialMaterializationWaterPointV2[] waterPoints;
    private readonly Vector2[] waterAreaCenters;
    private readonly YQSpatialMaterializationRouteV2[] routes;
    private readonly YQSpatialMaterializationRoutePointV2[] routePoints;
    private readonly HashSet<string> acceptedFeatureIdentityUniverse;
    private readonly YQSpatialMaterializationCrossingV2[] crossings;
    private readonly Dictionary<string, int> regionById;
    private readonly Dictionary<string, int> siteBySemanticId;
    private readonly Dictionary<string, int> siteBySiteId;
    private readonly YQSpatialBlueprintTerrainSamplerV2 terrainSampler;
    private readonly Dictionary<Vector2Int, SegmentRange>[] waterSegmentRangesByCell;
    private readonly Dictionary<Vector2Int, SegmentRange>[] routeSegmentRangesByCell;

    public int RegionCount => regions.Length;
    public int SiteCount => sites.Length;
    public int WaterCount => waters.Length;
    public int RouteCount => routes.Length;
    public int CrossingCount => crossings.Length;

    internal YQPreparedSpatialMaterializationV2(
        YQSpatialMaterializationRegionV2[] regions,
        YQSpatialMaterializationSiteV2[] sites,
        YQSpatialMaterializationWaterV2[] waters,
        YQSpatialMaterializationWaterPointV2[] waterPoints,
        YQSpatialMaterializationRouteV2[] routes,
        YQSpatialMaterializationRoutePointV2[] routePoints,
        YQSpatialMaterializationCrossingV2[] crossings,
        Dictionary<string, int> regionById,
        Dictionary<string, int> siteBySemanticId,
        Dictionary<string, int> siteBySiteId,
        YQSpatialBlueprintTerrainSamplerV2 terrainSampler)
    {
        this.regions = regions;
        this.sites = sites;
        this.waters = waters;
        this.waterPoints = waterPoints;
        waterAreaCenters = BuildWaterAreaCenters();
        this.routes = routes;
        this.routePoints = routePoints;
        acceptedFeatureIdentityUniverse = BuildAcceptedFeatureIdentityUniverse();
        this.crossings = crossings;
        this.regionById = regionById;
        this.siteBySemanticId = siteBySemanticId;
        this.siteBySiteId = siteBySiteId;
        this.terrainSampler = terrainSampler;
        waterSegmentRangesByCell = BuildWaterSegmentRangesByCell();
        routeSegmentRangesByCell = BuildRouteSegmentRangesByCell();
    }

    private HashSet<string> BuildAcceptedFeatureIdentityUniverse()
    {
        // note: Accepted V2 identities are immutable for this prepared plan; index them once so each streamed cell can canonicalize ownership without rebuilding a world-sized list.
        HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
        for (int routeIndex = 0; routeIndex < routes.Length; routeIndex++)
        {
            YQSpatialMaterializationRouteV2 route = routes[routeIndex];
            if (!string.IsNullOrWhiteSpace(route.routeId))
                identities.Add(route.routeId);
            if (!string.IsNullOrWhiteSpace(route.sourceSemanticRouteId))
                identities.Add(route.sourceSemanticRouteId);
        }
        for (int waterIndex = 0; waterIndex < waters.Length; waterIndex++)
        {
            string hydrologyId = waters[waterIndex].hydrologyId;
            if (!string.IsNullOrWhiteSpace(hydrologyId))
                identities.Add(hydrologyId);
        }
        return identities;
    }

    private Vector2[] BuildWaterAreaCenters()
    {
        // note: Cache the exact accepted-point average once; streamed area-water checks reuse it instead of rescanning every polygon per cell.
        Vector2[] centers = new Vector2[waters.Length];
        for (int waterIndex = 0; waterIndex < waters.Length; waterIndex++)
        {
            YQSpatialMaterializationWaterV2 water = waters[waterIndex];
            Vector2 center = Vector2.zero;
            for (int pointIndex = 0; pointIndex < water.pointCount; pointIndex++)
            {
                YQSpatialMaterializationWaterPointV2 point = waterPoints[water.pointStart + pointIndex];
                center += new Vector2(point.x, point.z);
            }
            if (water.pointCount > 0)
                center /= water.pointCount;
            centers[waterIndex] = center;
        }
        return centers;
    }

    internal bool TryGetWaterSegmentRangeForCell(
        int waterIndex,
        Vector2Int coordinate,
        float cellSize,
        out int firstSegment,
        out int lastSegment)
    {
        firstSegment = -1;
        lastSegment = -1;
        if (Mathf.Abs(cellSize - SegmentIndexCellSize) > 0.01f ||
            waterIndex < 0 || waterIndex >= waterSegmentRangesByCell.Length ||
            waterSegmentRangesByCell[waterIndex] == null ||
            !waterSegmentRangesByCell[waterIndex].TryGetValue(coordinate, out SegmentRange range))
            return false;
        firstSegment = range.firstSegment;
        lastSegment = range.lastSegment;
        return true;
    }

    internal bool TryGetRouteSegmentRangeForCell(
        int routeIndex,
        Vector2Int coordinate,
        float cellSize,
        out int firstSegment,
        out int lastSegment)
    {
        firstSegment = -1;
        lastSegment = -1;
        if (Mathf.Abs(cellSize - SegmentIndexCellSize) > 0.01f ||
            routeIndex < 0 || routeIndex >= routeSegmentRangesByCell.Length ||
            routeSegmentRangesByCell[routeIndex] == null ||
            !routeSegmentRangesByCell[routeIndex].TryGetValue(coordinate, out SegmentRange range))
            return false;
        firstSegment = range.firstSegment;
        lastSegment = range.lastSegment;
        return true;
    }

    private Dictionary<Vector2Int, SegmentRange>[] BuildWaterSegmentRangesByCell()
    {
        Dictionary<Vector2Int, SegmentRange>[] result =
            new Dictionary<Vector2Int, SegmentRange>[waters.Length];
        for (int featureIndex = 0; featureIndex < waters.Length; featureIndex++)
        {
            YQSpatialMaterializationWaterV2 water = waters[featureIndex];
            if (water.pointCount < 2)
                continue;
            Dictionary<Vector2Int, SegmentRange> ranges = null;
            float padding = Mathf.Max(8f, water.nominalWidth * 0.5f);
            for (int segmentIndex = 0; segmentIndex + 1 < water.pointCount; segmentIndex++)
            {
                YQSpatialMaterializationWaterPointV2 first =
                    waterPoints[water.pointStart + segmentIndex];
                YQSpatialMaterializationWaterPointV2 second =
                    waterPoints[water.pointStart + segmentIndex + 1];
                AddSegmentRange(
                    ref ranges,
                    first.x, first.z, second.x, second.z,
                    padding,
                    segmentIndex);
            }
            result[featureIndex] = ranges;
        }
        return result;
    }

    private Dictionary<Vector2Int, SegmentRange>[] BuildRouteSegmentRangesByCell()
    {
        Dictionary<Vector2Int, SegmentRange>[] result =
            new Dictionary<Vector2Int, SegmentRange>[routes.Length];
        for (int featureIndex = 0; featureIndex < routes.Length; featureIndex++)
        {
            YQSpatialMaterializationRouteV2 route = routes[featureIndex];
            if (route.pointCount < 2)
                continue;
            Dictionary<Vector2Int, SegmentRange> ranges = null;
            float padding = Mathf.Max(6f, route.width * 0.5f + route.shoulderWidth);
            for (int segmentIndex = 0; segmentIndex + 1 < route.pointCount; segmentIndex++)
            {
                YQSpatialMaterializationRoutePointV2 first =
                    routePoints[route.pointStart + segmentIndex];
                YQSpatialMaterializationRoutePointV2 second =
                    routePoints[route.pointStart + segmentIndex + 1];
                AddSegmentRange(
                    ref ranges,
                    first.x, first.z, second.x, second.z,
                    padding,
                    segmentIndex);
            }
            result[featureIndex] = ranges;
        }
        return result;
    }

    private static void AddSegmentRange(
        ref Dictionary<Vector2Int, SegmentRange> ranges,
        float x1,
        float z1,
        float x2,
        float z2,
        float padding,
        int segmentIndex)
    {
        // note: Every segment is indexed into every broad-phase cell its padded bounds touch; exact ownership predicates still reject false candidates.
        float origin = YQContinuousWorldFeatureAuthority.WorldGridOrigin;
        int minimumX = Mathf.FloorToInt((Mathf.Min(x1, x2) - padding - origin) / SegmentIndexCellSize);
        int maximumX = Mathf.FloorToInt((Mathf.Max(x1, x2) + padding - origin) / SegmentIndexCellSize);
        int minimumZ = Mathf.FloorToInt((Mathf.Min(z1, z2) - padding - origin) / SegmentIndexCellSize);
        int maximumZ = Mathf.FloorToInt((Mathf.Max(z1, z2) + padding - origin) / SegmentIndexCellSize);
        ranges ??= new Dictionary<Vector2Int, SegmentRange>();
        for (int z = minimumZ; z <= maximumZ; z++)
        {
            for (int x = minimumX; x <= maximumX; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                if (ranges.TryGetValue(coordinate, out SegmentRange existing))
                {
                    int firstSegment = Mathf.Min(existing.firstSegment, segmentIndex);
                    int lastSegment = Mathf.Max(existing.lastSegment, segmentIndex);
                    ranges[coordinate] = new SegmentRange(firstSegment, lastSegment);
                }
                else
                {
                    ranges.Add(coordinate, new SegmentRange(segmentIndex, segmentIndex));
                }
            }
        }
    }

    public bool TryGetRegion(
        string regionId,
        out YQSpatialMaterializationRegionV2 region)
    {
        region = default;
        if (string.IsNullOrWhiteSpace(regionId) ||
            !regionById.TryGetValue(regionId, out int index))
        {
            return false;
        }

        region = regions[index];
        return true;
    }

    public YQSpatialMaterializationSiteV2 GetSite(int index)
    {
        return sites[index];
    }

    public YQSpatialMaterializationWaterV2 GetWater(int index)
    {
        return waters[index];
    }

    internal Vector2 GetWaterAreaCenter(int waterIndex)
    {
        return waterAreaCenters[waterIndex];
    }

    internal bool IsAcceptedFeatureIdentity(string featureId)
    {
        return !string.IsNullOrWhiteSpace(featureId) && acceptedFeatureIdentityUniverse.Contains(featureId);
    }

    public int GetWaterPointCount(int waterIndex)
    {
        return waters[waterIndex].pointCount;
    }

    public YQSpatialMaterializationWaterPointV2 GetWaterPoint(
        int waterIndex,
        int pointIndex)
    {
        YQSpatialMaterializationWaterV2 water = waters[waterIndex];
        if (pointIndex < 0 || pointIndex >= water.pointCount)
            throw new ArgumentOutOfRangeException(nameof(pointIndex));
        return waterPoints[water.pointStart + pointIndex];
    }

    public YQSpatialTerrainSampleV2 SampleTerrain(float x, float z)
    {
        return terrainSampler.Sample(x, z);
    }

    public YQGeneratedWorldTileProfile SampleEcology(float x, float z)
    {
        return SampleEcologyContext(x, z).tileProfile;
    }

    public YQSpatialEcologySampleV2 SampleEcologyContext(float x, float z)
    {
        float totalWeight = 0f;
        float elevationBias = 0f;
        float moisture = 0f;
        float ruggedness = 0f;
        float forestDensity = 0f;
        float civilizationDensity = 0f;
        float danger = 0f;
        float strongestWeight = float.MinValue;
        YQGeneratedWorldBiomeKind biome =
            YQGeneratedWorldBiomeKind.TemperateLowland;

        for (int index = 0; index < regions.Length; index++)
        {
            YQSpatialMaterializationRegionV2 region = regions[index];
            float dx = x - region.centerX;
            float dz = z - region.centerZ;
            float normalizedDistance = Mathf.Sqrt(dx * dx + dz * dz) /
                                       Mathf.Max(1f, region.radius);
            float weight = 1f /
                           (0.18f + normalizedDistance * normalizedDistance);
            totalWeight += weight;
            elevationBias += region.elevationBias * weight;
            moisture += region.moisture * weight;
            ruggedness += region.ruggedness * weight;
            forestDensity += region.forestDensity * weight;
            civilizationDensity += region.civilizationDensity * weight;
            danger += region.danger * weight;
            if (weight > strongestWeight)
            {
                strongestWeight = weight;
                biome = region.biome;
            }
        }

        if (totalWeight > 0.0001f)
        {
            elevationBias /= totalWeight;
            moisture /= totalWeight;
            ruggedness /= totalWeight;
            forestDensity /= totalWeight;
            civilizationDensity /= totalWeight;
            danger /= totalWeight;
        }

        YQSpatialTerrainSampleV2 terrain = terrainSampler.Sample(x, z);
        ruggedness = Mathf.Max(ruggedness, terrain.ruggedness * 0.82f);
        float drainage = Mathf.Clamp01(
            terrain.waterMask + moisture * 0.42f);
        float mountain = Mathf.Clamp01(
            ruggedness * 0.72f +
            Mathf.InverseLerp(0.48f, 0.78f, terrain.elevationNormalized) *
            0.5f);
        int tileX = Mathf.FloorToInt(
            (x + YQGeneratedWorldTerrain.WorldSize * 0.5f) /
            YQGeneratedWorldTilePlan.TileWorldSize);
        int tileZ = Mathf.FloorToInt(
            (z + YQGeneratedWorldTerrain.WorldSize * 0.5f) /
            YQGeneratedWorldTilePlan.TileWorldSize);

        YQGeneratedWorldTileProfile tileProfile =
            new YQGeneratedWorldTileProfile(
                new Vector2Int(tileX, tileZ),
                biome,
                Mathf.Clamp(elevationBias, -1f, 1f),
                Mathf.Clamp01(ruggedness),
                Mathf.Clamp01(moisture),
                Mathf.Clamp01(forestDensity),
                mountain,
                drainage);

        // note: One V2 ecology sample carries semantic province data and the exact water, road, reserve, and cave masks used by terrain materialization.
        return new YQSpatialEcologySampleV2(
            tileProfile,
            terrain,
            Mathf.Clamp01(civilizationDensity),
            Mathf.Clamp01(danger));
    }

    public YQSpatialMaterializationRouteV2 GetRoute(int index)
    {
        return routes[index];
    }

    public int GetRoutePointCount(int routeIndex)
    {
        return routes[routeIndex].pointCount;
    }

    public YQSpatialMaterializationRoutePointV2 GetRoutePoint(
        int routeIndex,
        int pointIndex)
    {
        YQSpatialMaterializationRouteV2 route = routes[routeIndex];
        if (pointIndex < 0 || pointIndex >= route.pointCount)
            throw new ArgumentOutOfRangeException(nameof(pointIndex));
        return routePoints[route.pointStart + pointIndex];
    }

    public YQSpatialMaterializationCrossingV2 GetCrossing(int index)
    {
        return crossings[index];
    }

    public bool TryGetSiteBySemanticId(
        string semanticId,
        out YQSpatialMaterializationSiteV2 site)
    {
        site = default;
        if (string.IsNullOrWhiteSpace(semanticId) ||
            !siteBySemanticId.TryGetValue(semanticId, out int index))
        {
            return false;
        }

        site = sites[index];
        return true;
    }

    public bool TryGetSiteBySiteId(
        string siteId,
        out YQSpatialMaterializationSiteV2 site)
    {
        site = default;
        if (string.IsNullOrWhiteSpace(siteId) ||
            !siteBySiteId.TryGetValue(siteId, out int index))
        {
            return false;
        }

        site = sites[index];
        return true;
    }

    public bool TryValidateFootprint(
        string semanticId,
        float footprintRadius,
        out string failure)
    {
        failure = string.Empty;
        if (!TryGetSiteBySemanticId(semanticId, out YQSpatialMaterializationSiteV2 site))
        {
            failure = "The semantic location has no accepted V2 site reserve.";
            return false;
        }

        if (float.IsNaN(footprintRadius) ||
            float.IsInfinity(footprintRadius) ||
            footprintRadius <= 0f)
        {
            failure = "The selected runtime footprint is invalid.";
            return false;
        }

        if (footprintRadius > site.reservedRadius)
        {
            failure = "The selected runtime footprint needs " +
                      footprintRadius.ToString("F1") +
                      "m but the accepted site reserve provides " +
                      site.reservedRadius.ToString("F1") + "m.";
            return false;
        }

        // note: Asset choice may only consume accepted space; it cannot silently move or enlarge a V2 settlement after terrain and roads were compiled.
        return true;
    }
}

public static class YQSpatialMaterializationCompilerV2
{
    public static bool TryPrepareCandidate(
        GeneratedWorldPlanRecord source,
        GeneratedSpatialWorldPlanV2Record candidate,
        out YQPreparedSpatialMaterializationV2 prepared,
        out string failure)
    {
        prepared = null;
        if (source == null || candidate == null)
        {
            failure = "The semantic source or V2 candidate is missing.";
            return false;
        }

        // note: Validate a detached envelope of read-only semantic inputs; autosave and coroutine cancellation can never observe an unprepared candidate on the live world.
        GeneratedWorldPlanRecord detached = new GeneratedWorldPlanRecord
        {
            worldSeed = source.worldSeed,
            spatialPlan = source.spatialPlan,
            spatialPlanV2 = candidate,
            settlements = source.settlements,
            encampments = source.encampments
        };
        return TryPrepare(detached, out prepared, out failure);
    }

    public static bool TryPrepare(
        GeneratedWorldPlanRecord plan,
        out YQPreparedSpatialMaterializationV2 prepared,
        out string failure)
    {
        prepared = null;
        if (!YQSpatialBlueprintTerrainSamplerV2.TryPrepare(
                plan,
                out YQSpatialBlueprintTerrainSamplerV2 terrainSampler,
                out failure))
        {
            return false;
        }

        YQSpatialBlueprintV2 blueprint = plan.spatialPlanV2.blueprint;
        YQSpatialMaterializationRegionV2[] regions =
            new YQSpatialMaterializationRegionV2[blueprint.regions.Count];
        Dictionary<string, int> regionById =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < blueprint.regions.Count; index++)
        {
            YQRegionDomainV2 source = blueprint.regions[index];
            if (source == null ||
                string.IsNullOrWhiteSpace(source.regionId) ||
                !regionById.TryAdd(source.regionId, index))
            {
                failure = "The accepted V2 blueprint has an invalid materialization region.";
                return false;
            }

            regions[index] = new YQSpatialMaterializationRegionV2
            {
                regionId = source.regionId,
                centerX = source.centerX,
                centerZ = source.centerZ,
                radius = source.radius,
                elevationBias = source.elevationBias,
                moisture = source.moisture,
                ruggedness = source.ruggedness,
                civilizationDensity = source.civilizationDensity,
                danger = source.danger,
                forestDensity = ResolveForestDensity(source),
                biome = ResolveBiome(source)
            };
        }

        int waterPointCount = 0;
        for (int index = 0; index < blueprint.hydrology.Count; index++)
        {
            YQHydrologyFeatureV2 water = blueprint.hydrology[index];
            if (water != null)
                waterPointCount += CountPoints(water.controlPoints);
        }

        YQSpatialMaterializationWaterV2[] waters =
            new YQSpatialMaterializationWaterV2[blueprint.hydrology.Count];
        YQSpatialMaterializationWaterPointV2[] waterPoints =
            new YQSpatialMaterializationWaterPointV2[waterPointCount];
        int waterPointCursor = 0;
        for (int index = 0; index < blueprint.hydrology.Count; index++)
        {
            YQHydrologyFeatureV2 source = blueprint.hydrology[index];
            if (source == null)
                continue;
            int pointStart = waterPointCursor;
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumZ = float.PositiveInfinity;
            float maximumZ = float.NegativeInfinity;
            for (int pointIndex = 0;
                 pointIndex < source.controlPoints.Count;
                 pointIndex++)
            {
                YQBlueprintPointV2 point = source.controlPoints[pointIndex];
                if (point == null)
                    continue;
                minimumX = Mathf.Min(minimumX, point.x);
                maximumX = Mathf.Max(maximumX, point.x);
                minimumZ = Mathf.Min(minimumZ, point.z);
                maximumZ = Mathf.Max(maximumZ, point.z);
                waterPoints[waterPointCursor++] =
                    new YQSpatialMaterializationWaterPointV2
                    {
                        x = point.x,
                        z = point.z,
                        waterSurfaceNormalized =
                            Mathf.Clamp01(point.normalizedElevation),
                        width = Mathf.Max(source.nominalWidth, point.width)
                    };
            }

            waters[index] = new YQSpatialMaterializationWaterV2
            {
                hydrologyId = source.hydrologyId,
                parentRegionId = source.parentRegionId,
                kind = source.kind,
                sourceTerrainFieldId = source.sourceTerrainFieldId,
                sinkHydrologyId = source.sinkHydrologyId,
                waterLevelNormalized = source.waterLevelNormalized,
                nominalWidth = source.nominalWidth,
                nominalDepth = source.nominalDepth,
                pointStart = pointStart,
                pointCount = waterPointCursor - pointStart,
                hasSpatialBounds = waterPointCursor > pointStart,
                minimumX = minimumX,
                maximumX = maximumX,
                minimumZ = minimumZ,
                maximumZ = maximumZ
            };
        }

        int routePointCount = 0;
        int crossingCount = 0;
        for (int index = 0; index < blueprint.routes.Count; index++)
        {
            YQRouteCorridorV2 route = blueprint.routes[index];
            if (route == null)
                continue;
            routePointCount += CountPoints(route.controlPoints);
            if (route.crossings != null)
            {
                for (int crossingIndex = 0;
                     crossingIndex < route.crossings.Count;
                     crossingIndex++)
                {
                    if (route.crossings[crossingIndex] != null)
                        crossingCount++;
                }
            }
        }

        YQSpatialMaterializationRouteV2[] routes =
            new YQSpatialMaterializationRouteV2[blueprint.routes.Count];
        YQSpatialMaterializationRoutePointV2[] routePoints =
            new YQSpatialMaterializationRoutePointV2[routePointCount];
        YQSpatialMaterializationCrossingV2[] crossings =
            new YQSpatialMaterializationCrossingV2[crossingCount];
        int pointCursor = 0;
        int crossingCursor = 0;

        for (int index = 0; index < blueprint.routes.Count; index++)
        {
            YQRouteCorridorV2 source = blueprint.routes[index];
            if (source == null)
                continue;
            int pointStart = pointCursor;
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumZ = float.PositiveInfinity;
            float maximumZ = float.NegativeInfinity;
            for (int pointIndex = 0;
                 pointIndex < source.controlPoints.Count;
                 pointIndex++)
            {
                YQBlueprintPointV2 point = source.controlPoints[pointIndex];
                if (point == null)
                    continue;
                minimumX = Mathf.Min(minimumX, point.x);
                maximumX = Mathf.Max(maximumX, point.x);
                minimumZ = Mathf.Min(minimumZ, point.z);
                maximumZ = Mathf.Max(maximumZ, point.z);
                YQSpatialTerrainSampleV2 sample =
                    terrainSampler.Sample(point.x, point.z);
                routePoints[pointCursor++] =
                    new YQSpatialMaterializationRoutePointV2
                    {
                        x = point.x,
                        z = point.z,
                        surfaceElevationNormalized =
                            sample.elevationNormalized,
                        width = Mathf.Max(source.width, point.width)
                    };
            }

            routes[index] = new YQSpatialMaterializationRouteV2
            {
                routeId = source.routeId,
                sourceSemanticRouteId = source.sourceSemanticRouteId,
                fromSiteId = source.fromSiteId,
                toSiteId = source.toSiteId,
                routeClass = source.routeClass,
                width = source.width,
                shoulderWidth = source.shoulderWidth,
                maximumGradeDegrees = source.maximumGradeDegrees,
                pointStart = pointStart,
                pointCount = pointCursor - pointStart,
                hasSpatialBounds = pointCursor > pointStart,
                minimumX = minimumX,
                maximumX = maximumX,
                minimumZ = minimumZ,
                maximumZ = maximumZ
            };

            for (int crossingIndex = 0;
                 crossingIndex < source.crossings.Count;
                 crossingIndex++)
            {
                YQRouteCrossingV2 crossing =
                    source.crossings[crossingIndex];
                if (crossing == null)
                    continue;
                crossings[crossingCursor++] =
                    new YQSpatialMaterializationCrossingV2
                    {
                        crossingId = crossing.crossingId,
                        routeId = source.routeId,
                        hydrologyId = crossing.hydrologyId,
                        kind = crossing.kind,
                        x = crossing.x,
                        z = crossing.z,
                        requiredSpan = crossing.requiredSpan
                    };
            }
        }

        YQSpatialMaterializationSiteV2[] sites =
            new YQSpatialMaterializationSiteV2[blueprint.sites.Count];
        Dictionary<string, int> bySemanticId =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> bySiteId =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < blueprint.sites.Count; index++)
        {
            YQSiteAnchorV2 source = blueprint.sites[index];
            if (source == null)
            {
                failure = "The accepted V2 blueprint contains a null site.";
                return false;
            }

            YQSpatialTerrainSampleV2 terrain =
                terrainSampler.Sample(source.x, source.z);
            FindFrontage(
                blueprint,
                source,
                out string frontageRouteId,
                out float frontageX,
                out float frontageZ,
                out float routeDistance);
            // note: Runtime uses the same finite bank-width and semantic-access evidence that selected and accepted this site.
            if (!YQSpatialSiteAccessV2.TryMeasureWaterAccess(
                    blueprint, source, out float waterAccess,
                    out float nearestWaterDistance, out string nearestWaterId,
                    out failure))
            {
                return false;
            }

            float routeRange = Mathf.Max(12f, source.reservedRadius * 1.5f);
            float waterRange = Mathf.Max(24f, source.reservedRadius * 2.5f);
            float routeAccess = string.IsNullOrWhiteSpace(frontageRouteId)
                ? 0f
                : 1f - Mathf.Clamp01(routeDistance / routeRange);

            sites[index] = new YQSpatialMaterializationSiteV2
            {
                siteId = source.siteId,
                sourceSemanticId = source.sourceSemanticId,
                parentRegionId = source.parentRegionId,
                kind = source.kind,
                placementMode = source.placementMode,
                x = source.x,
                z = source.z,
                surfaceElevationNormalized = terrain.elevationNormalized,
                headingDegrees = Mathf.Repeat(
                    source.preferredHeadingDegrees,
                    360f),
                reservedRadius = source.reservedRadius,
                maximumSlopeDegrees = source.maximumSlopeDegrees,
                frontageRouteId = frontageRouteId,
                frontageX = frontageX,
                frontageZ = frontageZ,
                routeAccess = routeAccess,
                nearestWaterId = nearestWaterId,
                nearestWaterDistance = nearestWaterDistance,
                waterAccess = waterAccess,
                terrainReserveReady =
                    source.kind == YQSiteKindV2.NaturalFeature ||
                    !source.requiresTerrainConformance ||
                                      terrain.siteReserveMask >= 0.9f,
                concealed = source.hiddenFromPrimaryRoute ||
                            HasRelationship(
                                blueprint,
                                source.siteId,
                                YQSpatialRelationshipKindV2.ConcealedBy),
                behindFeature = HasRelationship(
                    blueprint,
                    source.siteId,
                    YQSpatialRelationshipKindV2.Behind),
                requiresReward = HasRequiredFunction(
                    source,
                    YQAssetFunctionV2.Reward),
                requiresEncounter = HasRequiredFunction(
                    source,
                    YQAssetFunctionV2.Encounter),
                requiresTransition = HasRequiredFunction(
                    source,
                    YQAssetFunctionV2.Transition),
                memberFootprint = source.memberFootprint != null
                    ? source.memberFootprint.ToArray()
                    : Array.Empty<YQSiteMemberFootprintV2>(),
                tags = source.tags != null
                    ? source.tags.ToArray()
                    : Array.Empty<string>(),
                requiredFunctions = source.requiredFunctions != null
                    ? source.requiredFunctions.ToArray()
                    : Array.Empty<YQAssetFunctionV2>(),
                concealedAccess = HasSiteTag(
                    source,
                    "concealed_access_candidate") ||
                    (source.kind == YQSiteKindV2.NaturalFeature &&
                     source.hiddenFromPrimaryRoute &&
                     HasRequiredFunction(
                         source,
                         YQAssetFunctionV2.Transition))
            };

            if (!bySiteId.TryAdd(source.siteId, index))
            {
                failure = "Duplicate V2 site materialization id: " +
                          source.siteId;
                return false;
            }
            if (!string.IsNullOrWhiteSpace(source.sourceSemanticId) &&
                !bySemanticId.TryAdd(source.sourceSemanticId, index))
            {
                failure = "Duplicate V2 semantic site binding: " +
                          source.sourceSemanticId;
                return false;
            }

            if (source.requiresTerrainConformance &&
                !sites[index].terrainReserveReady)
            {
                failure = "V2 site reserve is not present at " + source.siteId;
                return false;
            }
            if (source.minimumRouteAccess > 0f &&
                routeAccess + 0.001f < source.minimumRouteAccess)
            {
                failure = "V2 route access is insufficient at " +
                          source.siteId;
                return false;
            }
            if (source.minimumWaterAccess > 0f &&
                waterAccess + 0.001f < source.minimumWaterAccess)
            {
                // note: Include the measured constraint failure so an accepted-but-unmaterializable save can be diagnosed without relocating its sites or weakening validation.
                failure = "V2 water access is insufficient at " +
                          source.siteId + " (actual=" + waterAccess.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                          ", required=" + source.minimumWaterAccess.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                          ", nearest water=" + nearestWaterDistance.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                          "m, access range=" + waterRange.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "m)";
                return false;
            }
            if (source.kind == YQSiteKindV2.Settlement)
            {
                float requiredDryBank = source.minimumWaterAccess > 0.5f
                    ? 2f
                    : source.reservedRadius;
                // note: Runtime repeats the accepted dry-bank contract for both inland reserves and explicit waterfront anchors, so construction can never materialize inside rivers or lakes.
                if (nearestWaterDistance + 0.001f < requiredDryBank)
                {
                    failure = "V2 settlement dry-bank reserve is insufficient at " +
                              source.siteId + " (actual=" +
                              nearestWaterDistance.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                              "m, required=" +
                              requiredDryBank.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "m)";
                    return false;
                }
            }
        }

        if (!ContainsEverySemanticSite(plan, bySemanticId, out failure))
            return false;

        // note: One accepted projection now owns site pads, entrances, routes, crossings, and feature relationships for every downstream materializer.
        prepared = new YQPreparedSpatialMaterializationV2(
            regions,
            sites,
            waters,
            waterPoints,
            routes,
            routePoints,
            crossings,
            regionById,
            bySemanticId,
            bySiteId,
            terrainSampler);
        failure = string.Empty;
        return true;
    }

    private static void FindFrontage(
        YQSpatialBlueprintV2 blueprint,
        YQSiteAnchorV2 site,
        out string routeId,
        out float x,
        out float z,
        out float distance)
    {
        routeId = string.Empty;
        x = site.x;
        z = site.z;
        distance = float.MaxValue;

        for (int index = 0; index < blueprint.routes.Count; index++)
        {
            YQRouteCorridorV2 route = blueprint.routes[index];
            if (route == null || route.controlPoints.Count < 2)
                continue;
            bool connected = string.Equals(
                                 route.fromSiteId,
                                 site.siteId,
                                 StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(
                                 route.toSiteId,
                                 site.siteId,
                                 StringComparison.OrdinalIgnoreCase);
            NearestPointOnPolyline(
                route.controlPoints,
                site.x,
                site.z,
                out float candidateX,
                out float candidateZ,
                out float candidateDistance);
            if ((!connected && !string.IsNullOrWhiteSpace(routeId)) ||
                candidateDistance >= distance)
            {
                continue;
            }

            routeId = route.routeId;
            x = candidateX;
            z = candidateZ;
            distance = candidateDistance;
            if (connected && candidateDistance <= 0.05f)
                return;
        }

        if (distance == float.MaxValue)
            distance = 0f;
    }

    private static void NearestPointOnPolyline(
        List<YQBlueprintPointV2> points,
        float x,
        float z,
        out float nearestX,
        out float nearestZ,
        out float distance)
    {
        YQBlueprintPointV2 first = points[0];
        nearestX = first.x;
        nearestZ = first.z;
        float dx = x - nearestX;
        float dz = z - nearestZ;
        float bestSquared = dx * dx + dz * dz;

        for (int index = 1; index < points.Count; index++)
        {
            YQBlueprintPointV2 a = points[index - 1];
            YQBlueprintPointV2 b = points[index];
            if (a == null || b == null)
                continue;
            float segmentX = b.x - a.x;
            float segmentZ = b.z - a.z;
            float lengthSquared = segmentX * segmentX + segmentZ * segmentZ;
            float t = lengthSquared > 0.0001f
                ? Mathf.Clamp01(((x - a.x) * segmentX +
                                 (z - a.z) * segmentZ) / lengthSquared)
                : 0f;
            float candidateX = a.x + segmentX * t;
            float candidateZ = a.z + segmentZ * t;
            dx = x - candidateX;
            dz = z - candidateZ;
            float candidateSquared = dx * dx + dz * dz;
            if (candidateSquared >= bestSquared)
                continue;
            bestSquared = candidateSquared;
            nearestX = candidateX;
            nearestZ = candidateZ;
        }

        distance = Mathf.Sqrt(bestSquared);
    }

    private static bool HasRelationship(
        YQSpatialBlueprintV2 blueprint,
        string siteId,
        YQSpatialRelationshipKindV2 kind)
    {
        for (int index = 0; index < blueprint.relationships.Count; index++)
        {
            YQSpatialRelationshipV2 relationship =
                blueprint.relationships[index];
            if (relationship != null && relationship.kind == kind &&
                (string.Equals(
                     relationship.subjectId,
                     siteId,
                     StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                     relationship.objectId,
                     siteId,
                     StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsEverySemanticSite(
        GeneratedWorldPlanRecord plan,
        Dictionary<string, int> bySemanticId,
        out string failure)
    {
        for (int index = 0; index < plan.settlements.Count; index++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[index];
            if (settlement != null &&
                !bySemanticId.ContainsKey(settlement.settlementId))
            {
                failure = "Semantic settlement has no V2 site: " +
                          settlement.settlementId;
                return false;
            }
        }

        for (int index = 0; index < plan.encampments.Count; index++)
        {
            GeneratedEncampmentRecord encampment = plan.encampments[index];
            if (encampment != null &&
                !bySemanticId.ContainsKey(encampment.encampmentId))
            {
                failure = "Semantic hostile site has no V2 site: " +
                          encampment.encampmentId;
                return false;
            }
        }

        failure = string.Empty;
        return true;
    }

    private static int CountPoints(List<YQBlueprintPointV2> points)
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

    private static bool HasRequiredFunction(
        YQSiteAnchorV2 site,
        YQAssetFunctionV2 required)
    {
        if (site?.requiredFunctions == null)
            return false;
        for (int index = 0;
             index < site.requiredFunctions.Count;
             index++)
        {
            if (site.requiredFunctions[index] == required)
                return true;
        }
        return false;
    }

    private static bool HasSiteTag(
        YQSiteAnchorV2 site,
        string expected)
    {
        if (site?.tags == null || string.IsNullOrWhiteSpace(expected))
            return false;
        for (int index = 0; index < site.tags.Count; index++)
        {
            if (string.Equals(
                    site.tags[index],
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // note: Materialization carries explicit functional booleans so downstream systems never need to reinterpret narrative names or descriptions.
        return false;
    }

    private static float ResolveForestDensity(YQRegionDomainV2 region)
    {
        float forest = region.moisture * 0.62f +
                       (1f - region.ruggedness) * 0.18f +
                       region.civilizationDensity * 0.08f;
        if (ContainsBiomeTag(region, "forest") ||
            ContainsBiomeTag(region, "woodland") ||
            ContainsBiomeTag(region, "weald"))
        {
            forest += 0.28f;
        }
        if (ContainsBiomeTag(region, "desert") ||
            ContainsBiomeTag(region, "badland") ||
            ContainsBiomeTag(region, "ash"))
        {
            forest -= 0.34f;
        }
        return Mathf.Clamp01(forest);
    }

    private static YQGeneratedWorldBiomeKind ResolveBiome(
        YQRegionDomainV2 region)
    {
        if (ContainsBiomeTag(region, "wetland") ||
            ContainsBiomeTag(region, "marsh") ||
            ContainsBiomeTag(region, "swamp"))
        {
            return YQGeneratedWorldBiomeKind.Wetland;
        }
        if (ContainsBiomeTag(region, "forest") ||
            ContainsBiomeTag(region, "woodland") ||
            ContainsBiomeTag(region, "weald"))
        {
            return YQGeneratedWorldBiomeKind.AncientWoodland;
        }
        if (ContainsBiomeTag(region, "highland") ||
            ContainsBiomeTag(region, "mountain") ||
            region.ruggedness >= 0.66f)
        {
            return YQGeneratedWorldBiomeKind.Highland;
        }
        if (ContainsBiomeTag(region, "moor") ||
            ContainsBiomeTag(region, "steppe") ||
            ContainsBiomeTag(region, "badland"))
        {
            return YQGeneratedWorldBiomeKind.Moorland;
        }
        return YQGeneratedWorldBiomeKind.TemperateLowland;
    }

    private static bool ContainsBiomeTag(
        YQRegionDomainV2 region,
        string expected)
    {
        if (region?.biomeTags == null)
            return false;
        for (int index = 0; index < region.biomeTags.Count; index++)
        {
            string tag = region.biomeTags[index];
            if (!string.IsNullOrWhiteSpace(tag) &&
                tag.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }
}

public static class YQSpatialMaterializationResolverV2
{
    private static GeneratedWorldPlanRecord cachedPlan;
    private static GeneratedSpatialWorldPlanV2Record cachedArtifact;
    private static string cachedHash = string.Empty;
    private static YQPreparedSpatialMaterializationV2 cachedPrepared;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cachedPlan = null;
        cachedArtifact = null;
        cachedHash = string.Empty;
        cachedPrepared = null;
    }

    public static bool TryGetPrepared(
        GeneratedWorldPlanRecord plan,
        out YQPreparedSpatialMaterializationV2 prepared,
        out string failure)
    {
        GeneratedSpatialWorldPlanV2Record artifact = plan?.spatialPlanV2;
        // note: Recompute the artifact fingerprint before reuse; a mutable blueprint cannot retain stale prepared authority by leaving its claimed hash unchanged.
        if (ReferenceEquals(cachedPlan, plan) &&
            ReferenceEquals(cachedArtifact, artifact) &&
            cachedPrepared != null &&
            HasCurrentAcceptanceEvidence(plan, artifact) &&
            TryComputeCurrentHash(artifact, out string currentHash) &&
            string.Equals(cachedHash, currentHash, StringComparison.Ordinal))
        {
            prepared = cachedPrepared;
            failure = string.Empty;
            return true;
        }

        string claimedHash = artifact?.contentHash ?? string.Empty;
        if (!YQSpatialMaterializationCompilerV2.TryPrepare(
                plan,
                out prepared,
                out failure))
        {
            return false;
        }

        GeneratedSpatialWorldPlanV2Record preparedArtifact =
            plan?.spatialPlanV2;
        if (!ReferenceEquals(artifact, preparedArtifact) ||
            !string.Equals(claimedHash, preparedArtifact?.contentHash,
                StringComparison.Ordinal) ||
            !HasCurrentAcceptanceEvidence(plan, preparedArtifact) ||
            !TryComputeCurrentHash(preparedArtifact,
                out string preparedHash))
        {
            // note: Do not cache a projection if its accepted source changed while the immutable runtime arrays were being constructed.
            prepared = null;
            failure = "The accepted V2 artifact changed while its runtime materialization was being prepared.";
            return false;
        }

        cachedPlan = plan;
        cachedArtifact = preparedArtifact;
        cachedHash = preparedHash;
        cachedPrepared = prepared;
        return true;
    }

    private static bool HasCurrentAcceptanceEvidence(
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialWorldPlanV2Record artifact)
    {
        if (plan == null || artifact == null ||
            artifact.acceptanceState !=
                GeneratedSpatialPlanAcceptanceState.Accepted ||
            artifact.validationErrors == null ||
            artifact.validationErrors.Count > 0 ||
            string.IsNullOrWhiteSpace(artifact.contentHash) ||
            !string.Equals(artifact.contentHash,
                artifact.validatedContentHash, StringComparison.Ordinal) ||
            !string.Equals(plan.worldSeed, artifact.worldSeed,
                StringComparison.Ordinal) ||
            plan.spatialPlan == null ||
            !string.Equals(plan.spatialPlan.semanticFingerprint,
                artifact.semanticFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(artifact.validationVersion,
                   GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion,
                   StringComparison.Ordinal) ||
               string.Equals(artifact.validationVersion,
                   GeneratedSpatialWorldPlanV2Record.LegacyValidationVersion,
                   StringComparison.Ordinal);
    }

    private static bool TryComputeCurrentHash(
        GeneratedSpatialWorldPlanV2Record artifact,
        out string hash)
    {
        hash = string.Empty;
        try
        {
            // note: Preserve full content validation on every lookup while avoiding a JSON serialization/deserialization of the entire blueprint just to hash it.
            hash = YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(artifact);
            return !string.IsNullOrWhiteSpace(hash) &&
                   string.Equals(hash, artifact.contentHash,
                       StringComparison.Ordinal);
        }
        catch (Exception)
        {
            // note: Malformed mutable content must miss the cache and flow through the normal validation diagnostic instead of throwing from the fast path.
            hash = string.Empty;
            return false;
        }
    }
}
