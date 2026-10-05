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
    // note: Derived continuation ownership preserves the compiler's route modifier order; base route order and persisted route contracts stay unchanged.
    public string continuationOwnerSiteId;
    public string routeId;
    public string sourceSemanticRouteId;
    public string parentRegionId;
    public string fromSiteId;
    public string toSiteId;
    public YQRouteClassV2 routeClass;
    public float width;
    public float shoulderWidth;
    public float maximumGradeDegrees;
    // note: Opening corridors retain terminal continuation; accepted frontier access spurs remain finite.
    public bool permittedBoundaryContinuation;
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

// note: The continuation terrain owner supplies measured geometry/access; saved proof claims never populate this runtime evidence.
public readonly struct YQSpatialContinuationSiteSampleV2
{
    public readonly YQSpatialTerrainSampleV2 terrain;
    public readonly float slopeDegrees;
    public readonly string frontageRouteId;
    public readonly float frontageX;
    public readonly float frontageZ;
    public readonly float routeDistance;
    public readonly float routeAccess;
    public readonly string nearestWaterId;
    public readonly float nearestWaterDistance;
    public readonly float waterAccess;
    public readonly bool centerInsideWater;

    public YQSpatialContinuationSiteSampleV2(YQSpatialTerrainSampleV2 terrain, float slopeDegrees,
        string frontageRouteId, float frontageX, float frontageZ, float routeDistance, float routeAccess,
        string nearestWaterId, float nearestWaterDistance, float waterAccess, bool centerInsideWater)
    {
        this.terrain = terrain;
        this.slopeDegrees = slopeDegrees;
        this.frontageRouteId = frontageRouteId;
        this.frontageX = frontageX;
        this.frontageZ = frontageZ;
        this.routeDistance = routeDistance;
        this.routeAccess = routeAccess;
        this.nearestWaterId = nearestWaterId;
        this.nearestWaterDistance = nearestWaterDistance;
        this.waterAccess = waterAccess;
        this.centerInsideWater = centerInsideWater;
    }
}

public readonly struct YQSpatialMaterializationContinuationPadV2
{
    public readonly string ownerSiteId;
    public readonly string sectorId;
    public readonly float x;
    public readonly float z;
    public readonly float reservedRadius;
    public readonly float elevationNormalized;
    public readonly float shoulderWidth;

    public YQSpatialMaterializationContinuationPadV2(string ownerSiteId, string sectorId, float x, float z,
        float reservedRadius, float elevationNormalized, float shoulderWidth)
    {
        this.ownerSiteId = ownerSiteId;
        this.sectorId = sectorId;
        this.x = x;
        this.z = z;
        this.reservedRadius = reservedRadius;
        this.elevationNormalized = elevationNormalized;
        this.shoulderWidth = shoulderWidth;
    }
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
    private readonly Dictionary<Vector2Int, int[]> siteIndicesByCell;
    private readonly int[] broadSiteIndices;
    private readonly YQSpatialMaterializationContinuationPadV2[] continuationPads;
    private const int MaximumIndexedSiteCells = 4096;
    private const long MaximumIndexedCellsPerFootprint = 64;

    public int RegionCount => regions.Length;
    public int SiteCount => sites.Length;
    public int WaterCount => waters.Length;
    public int RouteCount => routes.Length;
    public int CrossingCount => crossings.Length;
    // note: These derived identities invalidate extension projections without changing the accepted opening artifact's hash or schema.
    public string ContinuationFingerprint { get; }
    public long ContinuationRevision { get; }
    public int ContinuationPadCount => continuationPads.Length;
    public YQSpatialMaterializationContinuationPadV2 GetContinuationPad(int index) => continuationPads[index];

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
        YQSpatialBlueprintTerrainSamplerV2 terrainSampler,
        string continuationFingerprint = "",
        long continuationRevision = 0,
        YQSpatialMaterializationContinuationPadV2[] continuationPads = null)
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
        ContinuationFingerprint = continuationFingerprint ?? string.Empty;
        ContinuationRevision = continuationRevision;
        this.continuationPads = continuationPads != null
            ? (YQSpatialMaterializationContinuationPadV2[])continuationPads.Clone()
            : Array.Empty<YQSpatialMaterializationContinuationPadV2>();
        waterSegmentRangesByCell = BuildWaterSegmentRangesByCell();
        routeSegmentRangesByCell = BuildRouteSegmentRangesByCell();
        siteIndicesByCell = BuildSiteIndicesByCell(out broadSiteIndices);
    }

    internal YQPreparedSpatialMaterializationV2 WithAcceptedContinuation(
        YQSpatialMaterializationSiteV2[] addedSites, YQSpatialMaterializationRouteV2[] addedRoutes,
        YQSpatialMaterializationRoutePointV2[] addedPoints, YQSpatialMaterializationContinuationPadV2[] addedPads,
        string fingerprint, long revision)
    {
        // note: Share immutable opening arrays but detach every appended range and lookup; the base-only measurement context remains unchanged.
        YQSpatialMaterializationSiteV2[] unionSites = Concat(sites, addedSites);
        YQSpatialMaterializationRouteV2[] unionRoutes = Concat(routes, addedRoutes);
        for (int index = routes.Length; index < unionRoutes.Length; index++)
            unionRoutes[index].pointStart += routePoints.Length;
        var unionBySiteId = new Dictionary<string, int>(siteBySiteId, StringComparer.OrdinalIgnoreCase);
        var unionBySemanticId = new Dictionary<string, int>(siteBySemanticId, StringComparer.OrdinalIgnoreCase);
        for (int index = sites.Length; index < unionSites.Length; index++)
        {
            unionBySiteId.Add(unionSites[index].siteId, index);
            unionBySemanticId.Add(unionSites[index].sourceSemanticId, index);
        }
        return new YQPreparedSpatialMaterializationV2(regions, unionSites, waters, waterPoints,
            unionRoutes, Concat(routePoints, addedPoints), crossings, regionById, unionBySemanticId, unionBySiteId,
            terrainSampler, fingerprint, revision, Concat(continuationPads, addedPads));
    }

    private static T[] Concat<T>(T[] first, T[] second)
    {
        var union = new T[first.Length + second.Length];
        Array.Copy(first, union, first.Length);
        Array.Copy(second, 0, union, first.Length, second.Length);
        return union;
    }

    public void CollectSiteIndicesForCell(Vector2Int coordinate, float cellSize, List<int> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        if (float.IsNaN(cellSize) || float.IsInfinity(cellSize) || cellSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        destination.Clear();

        // note: Reuse the immutable accepted projection on the canonical grid; caller-owned scratch avoids allocations during demand rebuilds.
        if (cellSize == SegmentIndexCellSize)
        {
            if (siteIndicesByCell.TryGetValue(coordinate, out int[] indexed))
                destination.AddRange(indexed);
            for (int index = 0; index < broadSiteIndices.Length; index++)
            {
                int siteIndex = broadSiteIndices[index];
                if (!destination.Contains(siteIndex) && SiteFootprintIntersectsCell(sites[siteIndex], coordinate, cellSize))
                    destination.Add(siteIndex);
            }
        }
        else
        {
            // note: Preserve supported noncanonical cell sizes through exact queries rather than returning an incomplete index.
            for (int siteIndex = 0; siteIndex < sites.Length; siteIndex++)
                if (SiteFootprintIntersectsCell(sites[siteIndex], coordinate, cellSize))
                    destination.Add(siteIndex);
        }
        destination.Sort();
    }

    private Dictionary<Vector2Int, int[]> BuildSiteIndicesByCell(out int[] broadIndices)
    {
        Dictionary<Vector2Int, List<int>> building = new Dictionary<Vector2Int, List<int>>();
        HashSet<int> broad = new HashSet<int>();
        for (int siteIndex = 0; siteIndex < sites.Length; siteIndex++)
        {
            YQSpatialMaterializationSiteV2 site = sites[siteIndex];
            IndexSiteFootprint(building, broad, siteIndex, site.x, site.z, site.reservedRadius);
            for (int memberIndex = 0; memberIndex < site.MemberFootprint.Count; memberIndex++)
            {
                YQSiteMemberFootprintV2 member = site.MemberFootprint[memberIndex];
                if (member != null)
                    IndexSiteFootprint(building, broad, siteIndex, member.x, member.z, member.reservedRadius);
            }
        }
        Dictionary<Vector2Int, int[]> result = new Dictionary<Vector2Int, int[]>(building.Count);
        foreach (KeyValuePair<Vector2Int, List<int>> pair in building)
            result.Add(pair.Key, pair.Value.ToArray());
        List<int> broadList = new List<int>(broad);
        broadList.Sort();
        broadIndices = broadList.ToArray();
        return result;
    }

    private static void IndexSiteFootprint(
        Dictionary<Vector2Int, List<int>> building, HashSet<int> broad,
        int siteIndex, float x, float z, float radius)
    {
        float origin = YQContinuousWorldFeatureAuthority.WorldGridOrigin;
        int minimumX = Mathf.CeilToInt((x - radius - origin) / SegmentIndexCellSize) - 1;
        int maximumX = Mathf.FloorToInt((x + radius - origin) / SegmentIndexCellSize);
        int minimumZ = Mathf.CeilToInt((z - radius - origin) / SegmentIndexCellSize) - 1;
        int maximumZ = Mathf.FloorToInt((z + radius - origin) / SegmentIndexCellSize);
        long cells = ((long)maximumX - minimumX + 1L) * ((long)maximumZ - minimumZ + 1L);
        // note: Bound acceleration data without rejecting or truncating accepted geometry; broad sites retain exact intersection queries.
        if (cells > MaximumIndexedCellsPerFootprint)
        {
            broad.Add(siteIndex);
            return;
        }
        for (int cellZ = minimumZ; cellZ <= maximumZ; cellZ++)
        for (int cellX = minimumX; cellX <= maximumX; cellX++)
        {
            Vector2Int coordinate = new Vector2Int(cellX, cellZ);
            if (!building.TryGetValue(coordinate, out List<int> indices))
            {
                if (building.Count >= MaximumIndexedSiteCells)
                {
                    broad.Add(siteIndex);
                    continue;
                }
                indices = new List<int>();
                building.Add(coordinate, indices);
            }
            if (!indices.Contains(siteIndex))
                indices.Add(siteIndex);
        }
    }

    private static bool SiteFootprintIntersectsCell(
        YQSpatialMaterializationSiteV2 site, Vector2Int coordinate, float cellSize)
    {
        float minimumX = YQContinuousWorldFeatureAuthority.WorldGridOrigin + coordinate.x * cellSize;
        float minimumZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin + coordinate.y * cellSize;
        if (FootprintIntersectsCell(site.x, site.z, site.reservedRadius, minimumX, minimumZ, cellSize))
            return true;
        for (int memberIndex = 0; memberIndex < site.MemberFootprint.Count; memberIndex++)
        {
            YQSiteMemberFootprintV2 member = site.MemberFootprint[memberIndex];
            if (member != null && FootprintIntersectsCell(member.x, member.z, member.reservedRadius, minimumX, minimumZ, cellSize))
                return true;
        }
        return false;
    }

    private static bool FootprintIntersectsCell(
        float x, float z, float radius, float minimumX, float minimumZ, float cellSize) =>
        x + radius >= minimumX && x - radius <= minimumX + cellSize &&
        z + radius >= minimumZ && z - radius <= minimumZ + cellSize;

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
    // note: One presentation-scale contract feeds both the terrain sampler and the streamed mesh projection, without rewriting accepted blueprint values.
    internal static float ResolveWaterWidth(YQHydrologyKindV2 kind, float width) =>
        Mathf.Max(kind == YQHydrologyKindV2.River ? 14f : kind == YQHydrologyKindV2.Waterfall ? 10f : 1f, width);

    internal static float ResolveWaterDepth(YQHydrologyKindV2 kind, float depth) =>
        Mathf.Max(kind == YQHydrologyKindV2.River ? 4.5f : kind == YQHydrologyKindV2.Waterfall ? 3.5f : 0f, depth);

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
        if (!YQSpatialMaterializationResolverV2.TryGetAcceptedContinuationIdentity(plan,
                out var continuation, out string continuationFingerprint, out failure))
            return false;
        if (continuation != null)
        {
            YQSpatialContinuationBasicValidationResultV2 basic = YQSpatialContinuationValidatorV2.ValidateBasic(plan.spatialPlanV2);
            if (!basic.IsStructurallyValid)
            {
                failure = "Accepted V2 continuation is structurally invalid: " + basic.errors[0];
                return false;
            }
        }
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
                        width = Mathf.Max(ResolveWaterWidth(source.kind, source.nominalWidth), point.width)
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
                // note: Meshes, boundary continuation and terrain use the same effective channel dimensions; accepted records remain unchanged.
                nominalWidth = ResolveWaterWidth(source.kind, source.nominalWidth),
                nominalDepth = ResolveWaterDepth(source.kind, source.nominalDepth),
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
                parentRegionId = source.parentRegionId,
                fromSiteId = source.fromSiteId,
                toSiteId = source.toSiteId,
                routeClass = source.routeClass,
                width = source.width,
                shoulderWidth = source.shoulderWidth,
                maximumGradeDegrees = source.maximumGradeDegrees,
                permittedBoundaryContinuation = true,
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
                memberFootprint = CopyMemberFootprint(source.memberFootprint),
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
        if (continuation != null && !TryAppendAcceptedContinuation(plan, prepared, continuation,
                continuationFingerprint, out prepared, out failure))
            return false;
        failure = string.Empty;
        return true;
    }

    private static bool TryAppendAcceptedContinuation(GeneratedWorldPlanRecord plan,
        YQPreparedSpatialMaterializationV2 basePrepared, GeneratedSpatialContinuationV2Record continuation,
        string fingerprint, out YQPreparedSpatialMaterializationV2 prepared, out string failure)
    {
        prepared = null;
        failure = string.Empty;
        var locations = new List<GeneratedSpatialContinuationLocationV2Record>(continuation.locations);
        locations.Sort((first, second) => string.CompareOrdinal(first.anchor.siteId, second.anchor.siteId));
        var allSiteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allSemanticIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var routeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < basePrepared.SiteCount; index++)
        {
            var site = basePrepared.GetSite(index);
            allSiteIds.Add(site.siteId);
            if (!string.IsNullOrWhiteSpace(site.sourceSemanticId))
                allSemanticIds.Add(site.sourceSemanticId);
        }
        for (int index = 0; index < basePrepared.RouteCount; index++)
            routeIds.Add(basePrepared.GetRoute(index).routeId);
        foreach (var location in locations)
            if (!allSiteIds.Add(location.anchor.siteId) || !allSemanticIds.Add(location.anchor.sourceSemanticId))
            {
                failure = "Duplicate accepted continuation site or semantic identity: " + location.anchor.siteId;
                return false;
            }

        var sites = new List<YQSpatialMaterializationSiteV2>(locations.Count);
        var routes = new List<YQSpatialMaterializationRouteV2>();
        var points = new List<YQSpatialMaterializationRoutePointV2>();
        var pads = new List<YQSpatialMaterializationContinuationPadV2>();
        // note: Prove connectivity against earlier accepted geometry before adding this site's own roads. Saved enumeration/ID order cannot hide a rooted frontier parent or admit a disconnected cycle.
        var replayLocations = GetContinuationReplayOrder(locations);
        var replayPrepared = basePrepared;
        foreach (GeneratedSpatialContinuationLocationV2Record location in replayLocations)
        {
            YQSiteAnchorV2 source = location.anchor;
            if (!basePrepared.TryGetRegion(source.parentRegionId, out _))
            {
                failure = "Accepted continuation has no canonical materialization region: " + source.siteId;
                return false;
            }
            for (int index = 0; index < basePrepared.SiteCount + sites.Count; index++)
            {
                YQSpatialMaterializationSiteV2 other = index < basePrepared.SiteCount
                    ? basePrepared.GetSite(index) : sites[index - basePrepared.SiteCount];
                if (ContinuationFootprintsOverlap(source, other))
                {
                    failure = "Accepted continuation overlaps another owner's central/member exclusion: " + source.siteId + " / " + other.siteId;
                    return false;
                }
            }
            // note: Real terrain/access and cached reviewed-source functions are independently repeated; recorded claims cannot make either gate pass.
            if (!YQContinuousWorldCellAuthority.TryMeasureAcceptedContinuationSite(plan, replayPrepared, location,
                    out YQSpatialContinuationSiteSampleV2 sample, out failure) ||
                !YQCompiledWorldSiteInstance.TryValidateAcceptedContinuationFunctions(location, out failure))
                return false;
            bool reserveReady = source.kind == YQSiteKindV2.NaturalFeature || !source.requiresTerrainConformance ||
                sample.terrain.siteReserveMask >= 0.9f;
            if (!IsFinite(sample.terrain.elevationNormalized) || sample.terrain.elevationNormalized < 0f || sample.terrain.elevationNormalized > 1f ||
                !IsFinite(sample.slopeDegrees) || sample.slopeDegrees < 0f || sample.slopeDegrees > source.maximumSlopeDegrees + 0.001f ||
                !IsFinite(sample.routeAccess) || sample.routeAccess < 0f || sample.routeAccess > 1f ||
                !IsFinite(sample.waterAccess) || sample.waterAccess < 0f || sample.waterAccess > 1f ||
                float.IsNaN(sample.nearestWaterDistance) || sample.nearestWaterDistance < 0f ||
                source.requiresTerrainConformance && !reserveReady ||
                sample.routeAccess + 0.001f < source.minimumRouteAccess || sample.waterAccess + 0.001f < source.minimumWaterAccess ||
                source.kind == YQSiteKindV2.Settlement &&
                    (sample.centerInsideWater || sample.nearestWaterDistance + 0.001f < (source.minimumWaterAccess > 0.5f ? 2f : source.reservedRadius) ||
                     string.IsNullOrWhiteSpace(sample.frontageRouteId) || sample.routeAccess <= 0f))
            {
                failure = "Accepted continuation failed its measured reserve, slope, route, water or settlement connectivity contract: " + source.siteId;
                return false;
            }
            sites.Add(new YQSpatialMaterializationSiteV2 {
                siteId = source.siteId, sourceSemanticId = source.sourceSemanticId, parentRegionId = source.parentRegionId,
                kind = source.kind, placementMode = source.placementMode, x = source.x, z = source.z,
                surfaceElevationNormalized = sample.terrain.elevationNormalized,
                headingDegrees = Mathf.Repeat(source.preferredHeadingDegrees, 360f), reservedRadius = source.reservedRadius,
                maximumSlopeDegrees = source.maximumSlopeDegrees, frontageRouteId = sample.frontageRouteId,
                frontageX = sample.frontageX, frontageZ = sample.frontageZ, routeAccess = sample.routeAccess,
                nearestWaterId = sample.nearestWaterId, nearestWaterDistance = sample.nearestWaterDistance, waterAccess = sample.waterAccess,
                terrainReserveReady = reserveReady, concealed = source.hiddenFromPrimaryRoute,
                requiresReward = HasRequiredFunction(source, YQAssetFunctionV2.Reward),
                requiresEncounter = HasRequiredFunction(source, YQAssetFunctionV2.Encounter),
                requiresTransition = HasRequiredFunction(source, YQAssetFunctionV2.Transition),
                memberFootprint = CopyMemberFootprint(source.memberFootprint), tags = source.tags.ToArray(),
                requiredFunctions = source.requiredFunctions.ToArray(),
                concealedAccess = HasSiteTag(source, "concealed_access_candidate") ||
                    source.kind == YQSiteKindV2.NaturalFeature && source.hiddenFromPrimaryRoute && HasRequiredFunction(source, YQAssetFunctionV2.Transition)
            });
            foreach (GeneratedSpatialContinuationTerrainPadV2Record pad in location.physicalContext.terrainPads)
            {
                float x = source.x, z = source.z, radius = source.reservedRadius;
                if (pad.sectorId != source.siteId)
                    foreach (YQSiteMemberFootprintV2 member in source.memberFootprint)
                        if (member.memberId == pad.sectorId)
                        {
                            x = member.x; z = member.z; radius = member.reservedRadius;
                            break;
                        }
                pads.Add(new YQSpatialMaterializationContinuationPadV2(source.siteId, pad.sectorId, x, z, radius,
                    pad.elevationNormalized, pad.shoulderWidth));
            }

            var orderedRoutes = new List<YQRouteCorridorV2>(location.physicalContext.routes);
            orderedRoutes.Sort((first, second) => string.CompareOrdinal(first.routeId, second.routeId));
            foreach (YQRouteCorridorV2 route in orderedRoutes)
            {
                if (!routeIds.Add(route.routeId) ||
                    !string.IsNullOrWhiteSpace(route.fromSiteId) && !allSiteIds.Contains(route.fromSiteId) ||
                    !string.IsNullOrWhiteSpace(route.toSiteId) && !allSiteIds.Contains(route.toSiteId) || route.crossings.Count != 0)
                {
                    failure = "Accepted continuation route has a duplicate identity, unresolved endpoint or unsupported crossing: " + route.routeId;
                    return false;
                }
                int start = points.Count;
                float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
                // note: Preserve the accepted path's parallel point order and approved elevations; no resampling against the opening blueprint is allowed.
                foreach (YQBlueprintPointV2 point in route.controlPoints)
                {
                    if (!IsFinite(point.width) || point.width < 0f || point.width > 0f && Mathf.Abs(point.width - route.width) > 0.001f)
                    {
                        failure = "Accepted continuation route has an unmeasured variable point width: " + route.routeId;
                        return false;
                    }
                    points.Add(new YQSpatialMaterializationRoutePointV2 {
                        x = point.x, z = point.z, surfaceElevationNormalized = point.normalizedElevation,
                        width = route.width
                    });
                    minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
                    minZ = Mathf.Min(minZ, point.z); maxZ = Mathf.Max(maxZ, point.z);
                }
                routes.Add(new YQSpatialMaterializationRouteV2 {
                    continuationOwnerSiteId = source.siteId,
                    routeId = route.routeId, sourceSemanticRouteId = route.sourceSemanticRouteId, parentRegionId = route.parentRegionId,
                    fromSiteId = route.fromSiteId, toSiteId = route.toSiteId, routeClass = route.routeClass,
                    width = route.width, shoulderWidth = route.shoulderWidth, maximumGradeDegrees = route.maximumGradeDegrees,
                    permittedBoundaryContinuation = false, pointStart = start, pointCount = points.Count - start,
                    hasSpatialBounds = true, minimumX = minX, maximumX = maxX, minimumZ = minZ, maximumZ = maxZ
                });
            }
            replayPrepared = basePrepared.WithAcceptedContinuation(sites.ToArray(), routes.ToArray(), points.ToArray(),
                pads.ToArray(), fingerprint, location.revision);
        }
        foreach (GeneratedSpatialContinuationLocationV2Record location in locations)
            foreach (GeneratedSemanticEntranceRecord entrance in location.entrances)
                if (!string.IsNullOrWhiteSpace(entrance.permittedRouteId) && !routeIds.Contains(entrance.permittedRouteId))
                {
                    failure = "Accepted continuation entrance references an unknown route: " + entrance.entranceId;
                    return false;
                }
        // note: Rooted proof order is separate from the unchanged canonical projection order and parallel point ranges.
        points = CanonicalizeContinuationProjection(sites, routes, points);
        pads.Sort((first, second) => {
            int owner = string.CompareOrdinal(first.ownerSiteId, second.ownerSiteId);
            return owner != 0 ? owner : string.CompareOrdinal(first.sectorId, second.sectorId);
        });
        prepared = basePrepared.WithAcceptedContinuation(sites.ToArray(), routes.ToArray(), points.ToArray(), pads.ToArray(), fingerprint, continuation.revision);
        foreach (GeneratedSpatialContinuationLocationV2Record location in locations)
            if (!YQContinuousWorldCellAuthority.TryMeasureAcceptedContinuationSite(plan, prepared, location, out _, out failure))
            {
                // note: Individually admitted contexts must also remain valid under every other accepted finite route and terrain pad in the actual union.
                prepared = null;
                failure = "Accepted continuation final-union conformance failed: " + failure;
                return false;
            }
        return true;
    }

    private static List<GeneratedSpatialContinuationLocationV2Record> GetContinuationReplayOrder(
        List<GeneratedSpatialContinuationLocationV2Record> locations)
    {
        // note: Detach the proof sequence; accepted records and their serialized enumeration remain unchanged.
        var ordered = new List<GeneratedSpatialContinuationLocationV2Record>(locations);
        ordered.Sort((first, second) => {
            int revision = first.revision.CompareTo(second.revision);
            return revision != 0 ? revision : string.CompareOrdinal(first.anchor.siteId, second.anchor.siteId);
        });
        return ordered;
    }

    private static List<YQSpatialMaterializationRoutePointV2> CanonicalizeContinuationProjection(
        List<YQSpatialMaterializationSiteV2> sites, List<YQSpatialMaterializationRouteV2> routes,
        List<YQSpatialMaterializationRoutePointV2> points)
    {
        // note: Restore the established owner/route ordering while retaining every accepted point and each route's exact parallel range.
        sites.Sort((first, second) => string.CompareOrdinal(first.siteId, second.siteId));
        routes.Sort((first, second) => {
            int owner = string.CompareOrdinal(first.continuationOwnerSiteId, second.continuationOwnerSiteId);
            return owner != 0 ? owner : string.CompareOrdinal(first.routeId, second.routeId);
        });
        var canonicalPoints = new List<YQSpatialMaterializationRoutePointV2>(points.Count);
        for (int index = 0; index < routes.Count; index++)
        {
            var route = routes[index];
            int previousStart = route.pointStart;
            route.pointStart = canonicalPoints.Count;
            for (int point = 0; point < route.pointCount; point++) canonicalPoints.Add(points[previousStart + point]);
            routes[index] = route;
        }
        return canonicalPoints;
    }

    private static bool ContinuationFootprintsOverlap(YQSiteAnchorV2 added, YQSpatialMaterializationSiteV2 accepted)
    {
        // note: Exclude other owners conservatively as rectangles, while central/member overlap within the same accepted owner remains legal.
        for (int first = -1; first < added.memberFootprint.Count; first++)
        {
            YQSiteMemberFootprintV2 a = first < 0 ? null : added.memberFootprint[first];
            float x = a != null ? a.x : added.x, z = a != null ? a.z : added.z, radius = a != null ? a.reservedRadius : added.reservedRadius;
            for (int second = -1; second < accepted.MemberFootprint.Count; second++)
            {
                YQSiteMemberFootprintV2 b = second < 0 ? null : accepted.MemberFootprint[second];
                if (second >= 0 && b == null)
                    continue;
                float otherX = b != null ? b.x : accepted.x, otherZ = b != null ? b.z : accepted.z;
                float exclusion = radius + (b != null ? b.reservedRadius : accepted.reservedRadius) + 18f;
                if (Mathf.Abs(x - otherX) <= exclusion && Mathf.Abs(z - otherZ) <= exclusion)
                    return true;
            }
        }
        return false;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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

    private static YQSiteMemberFootprintV2[] CopyMemberFootprint(IReadOnlyList<YQSiteMemberFootprintV2> source)
    {
        // note: Detach mutable member records as well as the array so an accepted-source edit cannot change a frozen projection behind its spatial index.
        if (source == null || source.Count == 0)
            return Array.Empty<YQSiteMemberFootprintV2>();
        var copied = new YQSiteMemberFootprintV2[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            YQSiteMemberFootprintV2 member = source[index];
            if (member == null) continue;
            copied[index] = new YQSiteMemberFootprintV2 {
                memberId = member.memberId, x = member.x, z = member.z,
                reservedRadius = member.reservedRadius, sectorIndex = member.sectorIndex
            };
        }
        return copied;
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
    private static string cachedMemberFootprintHash = string.Empty;
    private static GeneratedSpatialContinuationV2Record cachedContinuation;
    private static string cachedContinuationFingerprint = string.Empty;
    private static YQPreparedSpatialMaterializationV2 cachedPrepared;

    private static readonly Newtonsoft.Json.JsonSerializerSettings ContinuationInstallationJsonSettings =
        new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.None,
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None,
            Converters = { new Vector2JsonConverter(), new Vector3JsonConverter(), new QuaternionJsonConverter() }
        };

    internal sealed class PreparedContinuationInstallation
    {
        private readonly WorldState world;
        private readonly WorldStateManager worldManager;
        private readonly GeneratedWorldPlanRecord plan;
        private readonly GeneratedSpatialWorldPlanV2Record artifact;
        private readonly YQSpatialBlueprintV2 blueprint;
        private readonly object semanticSource;
        private readonly object worldIdentity;
        private readonly string worldId;
        private readonly string profileId;
        private readonly int epoch;
        private readonly string baseIdentity;
        private readonly string baseHash;
        private readonly string memberHash;
        private readonly GeneratedSpatialContinuationV2Record prior;
        private readonly string priorFingerprint;
        private readonly string priorJson;
        private readonly string acceptedJson;

        internal YQPreparedSpatialMaterializationV2 Prepared { get; }
        // note: Callers may inspect a detached candidate, but cannot mutate the body bound to this compiled installation.
        internal GeneratedSpatialContinuationV2Record Candidate => DeserializeContinuation(acceptedJson);

        private PreparedContinuationInstallation(WorldState owner,
            YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction)
        {
            worldManager = WorldStateManager.Instance;
            if (owner == null || worldManager == null || !worldManager.isActiveAndEnabled ||
                !ReferenceEquals(worldManager.State, owner) || owner.worldIdentity == null ||
                string.IsNullOrWhiteSpace(owner.worldIdentity.worldId) ||
                string.IsNullOrWhiteSpace(owner.worldIdentity.ownerProfileId))
                throw new InvalidOperationException("Continuation staging requires the active identified world owner.");
            world = owner;
            plan = owner.generatedWorldPlan;
            artifact = plan?.spatialPlanV2;
            if (!HasCurrentAcceptanceEvidence(plan, artifact) || !TryComputeCurrentHash(artifact, out string actualHash))
                throw new InvalidOperationException("Continuation staging requires the unchanged accepted base artifact.");
            blueprint = artifact.blueprint;
            semanticSource = plan.spatialPlan;
            worldIdentity = owner.worldIdentity;
            worldId = owner.worldIdentity.worldId;
            profileId = owner.worldIdentity.ownerProfileId;
            epoch = YQServiceLifecycle.RequestEpoch;
            baseIdentity = BuildInstallationBaseIdentity(artifact);
            baseHash = actualHash;
            memberHash = YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(artifact);
            prior = artifact.acceptedContinuation;
            if (!TryGetAcceptedContinuationIdentity(plan, out _, out string fingerprint, out string failure))
                throw new InvalidOperationException(failure);
            priorFingerprint = fingerprint;
            priorJson = SerializeContinuation(prior);
            // note: Only the existing construction token can supply the actual compiler/population-validated immutable union.
            if (!construction.TryPrepareForPublication(owner, out var accepted, out var prepared, out failure) ||
                accepted == null || prepared == null)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(failure) ? "Continuation construction is unavailable." : failure);
            Prepared = prepared;
            acceptedJson = SerializeContinuation(accepted);
        }

        internal static bool TryCreate(WorldState world,
            YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
            out PreparedContinuationInstallation installation, out string failure)
        {
            installation = null;
            try
            {
                var staged = new PreparedContinuationInstallation(world, construction);
                if (!staged.TryValidateBeforePublication(world, out failure)) return false;
                installation = staged;
                return true;
            }
            catch (Exception exception)
            {
                failure = "Continuation projection staging failed: " + exception.Message;
                return false;
            }
        }

        internal bool TryValidateBeforePublication(WorldState owner, out string failure)
        {
            // note: No preparation path assigns the global cache or the live accepted envelope.
            if (!TryValidateOwner(owner, out failure)) return false;
            try
            {
                if (!ReferenceEquals(artifact.acceptedContinuation, prior) ||
                    !TryGetAcceptedContinuationIdentity(plan, out _, out string fingerprint, out failure) ||
                    !string.Equals(fingerprint, priorFingerprint, StringComparison.Ordinal) ||
                    !string.Equals(SerializeContinuation(prior), priorJson, StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(failure)) failure = "The prior accepted continuation changed before publication.";
                    return false;
                }
                var accepted = Candidate;
                if (accepted == null || accepted.state != YQSpatialContinuationStateV2.Accepted ||
                    accepted.parentSpatialContentHash != baseHash || accepted.parentMemberFootprintHash != memberHash ||
                    accepted.contentHash != accepted.validatedContentHash ||
                    accepted.contentHash != YQSpatialContinuationHasherV2.ComputeContentHash(accepted) ||
                    Prepared.ContinuationRevision != accepted.revision ||
                    !string.Equals(Prepared.ContinuationFingerprint, BuildContinuationFingerprint(accepted), StringComparison.Ordinal) ||
                    !string.Equals(SerializeContinuation(accepted), acceptedJson, StringComparison.Ordinal))
                {
                    failure = "The staged continuation body no longer matches its immutable compiled projection.";
                    return false;
                }
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                failure = "Continuation projection identity is unreadable: " + exception.Message;
                return false;
            }
        }

        internal bool TryInstallPublishedContinuation(WorldState owner, out string failure)
        {
            if (!TryValidateOwner(owner, out failure)) return false;
            try
            {
                if (!TryGetAcceptedContinuationIdentity(plan, out var published, out string fingerprint, out failure) ||
                    published == null || !string.Equals(SerializeContinuation(published), acceptedJson, StringComparison.Ordinal) ||
                    !string.Equals(fingerprint, Prepared.ContinuationFingerprint, StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(failure)) failure = "The published continuation differs from the staged compiled body.";
                    return false;
                }
                // note: The paired publisher already admitted providers/numeric geometry before its pointer; this handoff installs the exact prepared union without repeating those fallible gates.
                cachedPlan = plan;
                cachedArtifact = artifact;
                cachedHash = baseHash;
                cachedMemberFootprintHash = memberHash;
                cachedContinuation = published;
                cachedContinuationFingerprint = fingerprint;
                cachedPrepared = Prepared;
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                failure = "Published continuation cache handoff failed: " + exception.Message;
                return false;
            }
        }

        private bool TryValidateOwner(WorldState owner, out string failure)
        {
            failure = "Continuation projection belongs to a stale active world, base artifact or request epoch.";
            if (!ReferenceEquals(owner, world) || !ReferenceEquals(WorldStateManager.Instance, worldManager) ||
                worldManager == null || !worldManager.isActiveAndEnabled || !ReferenceEquals(worldManager.State, world) ||
                !ReferenceEquals(world.worldIdentity, worldIdentity) || world.worldIdentity?.worldId != worldId ||
                world.worldIdentity?.ownerProfileId != profileId || !YQServiceLifecycle.IsCurrent(epoch) ||
                !ReferenceEquals(world.generatedWorldPlan, plan) || !ReferenceEquals(plan?.spatialPlanV2, artifact) ||
                !ReferenceEquals(artifact?.blueprint, blueprint) || !ReferenceEquals(plan?.spatialPlan, semanticSource)) return false;
            try
            {
                if (!HasCurrentAcceptanceEvidence(plan, artifact) ||
                    !string.Equals(BuildInstallationBaseIdentity(artifact), baseIdentity, StringComparison.Ordinal) ||
                    !TryComputeCurrentHash(artifact, out string currentHash) ||
                    !string.Equals(currentHash, baseHash, StringComparison.Ordinal) ||
                    !string.Equals(YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(artifact), memberHash, StringComparison.Ordinal))
                    return false;
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                failure = "Continuation base identity is unreadable: " + exception.Message;
                return false;
            }
        }
    }

    internal static bool TryStageContinuationInstallation(WorldState world,
        YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        out PreparedContinuationInstallation installation, out string failure)
    {
        installation = null;
        failure = "Continuation projection staging requires the construction owner's acceptance token.";
        return construction != null && PreparedContinuationInstallation.TryCreate(world, construction, out installation, out failure);
    }

    internal static bool TryValidateBeforePublication(WorldState world,
        PreparedContinuationInstallation installation, out string failure)
    {
        failure = "Continuation projection installation is missing.";
        return installation != null && installation.TryValidateBeforePublication(world, out failure);
    }

    internal static bool TryInstallPublishedContinuation(WorldState world,
        PreparedContinuationInstallation installation, out string failure)
    {
        failure = "Continuation projection installation is missing.";
        return installation != null && installation.TryInstallPublishedContinuation(world, out failure);
    }

    private static string SerializeContinuation(GeneratedSpatialContinuationV2Record continuation) =>
        Newtonsoft.Json.JsonConvert.SerializeObject(continuation, ContinuationInstallationJsonSettings);

    private static GeneratedSpatialContinuationV2Record DeserializeContinuation(string json) =>
        Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(json, ContinuationInstallationJsonSettings);

    private static string BuildInstallationBaseIdentity(GeneratedSpatialWorldPlanV2Record artifact) =>
        string.Concat(artifact.schemaVersion, "|", artifact.generationVersion, "|", artifact.validationVersion,
            "|", artifact.acceptanceState.ToString(), "|", artifact.worldSeed, "|", artifact.semanticFingerprint,
            "|", artifact.contentHash, "|", artifact.validatedContentHash);

    private static string BuildContinuationFingerprint(GeneratedSpatialContinuationV2Record continuation) =>
        continuation.schemaVersion + "|" + continuation.revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + continuation.contentHash;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cachedPlan = null;
        cachedArtifact = null;
        cachedHash = string.Empty;
        cachedMemberFootprintHash = string.Empty;
        cachedContinuation = null;
        cachedContinuationFingerprint = string.Empty;
        cachedPrepared = null;
    }

    public static bool TryGetPrepared(
        GeneratedWorldPlanRecord plan,
        out YQPreparedSpatialMaterializationV2 prepared,
        out string failure)
    {
        GeneratedSpatialWorldPlanV2Record artifact = plan?.spatialPlanV2;
        prepared = null;
        if (!TryGetAcceptedContinuationIdentity(plan, out var continuation, out string continuationFingerprint, out failure))
            return false;
        // note: Recompute the artifact fingerprint before reuse; a mutable blueprint cannot retain stale prepared authority by leaving its claimed hash unchanged.
        if (ReferenceEquals(cachedPlan, plan) &&
            ReferenceEquals(cachedArtifact, artifact) &&
            ReferenceEquals(cachedContinuation, continuation) &&
            string.Equals(cachedContinuationFingerprint, continuationFingerprint, StringComparison.Ordinal) &&
            cachedPrepared != null &&
            HasCurrentAcceptanceEvidence(plan, artifact) &&
            TryComputeCurrentHash(artifact, out string currentHash) &&
            string.Equals(cachedHash, currentHash, StringComparison.Ordinal) &&
            string.Equals(cachedMemberFootprintHash,
                YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(artifact), StringComparison.Ordinal))
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
                out string preparedHash) ||
            !TryGetAcceptedContinuationIdentity(plan, out var preparedContinuation, out string preparedContinuationFingerprint, out _) ||
            !ReferenceEquals(continuation, preparedContinuation) ||
            !string.Equals(continuationFingerprint, preparedContinuationFingerprint, StringComparison.Ordinal))
        {
            // note: Do not cache a projection if its accepted source changed while the immutable runtime arrays were being constructed.
            prepared = null;
            failure = "The accepted V2 artifact changed while its runtime materialization was being prepared.";
            return false;
        }

        cachedPlan = plan;
        cachedArtifact = preparedArtifact;
        cachedHash = preparedHash;
        // note: Member sectors were absent from the historical accepted content hash; their derived index must still invalidate when their geometry changes.
        cachedMemberFootprintHash = YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(preparedArtifact);
        cachedContinuation = preparedContinuation;
        cachedContinuationFingerprint = preparedContinuationFingerprint;
        cachedPrepared = prepared;
        return true;
    }

    internal static bool TryGetAcceptedContinuationIdentity(GeneratedWorldPlanRecord plan,
        out GeneratedSpatialContinuationV2Record continuation, out string fingerprint, out string failure)
    {
        continuation = null;
        fingerprint = string.Empty;
        failure = string.Empty;
        GeneratedSpatialWorldPlanV2Record artifact = plan?.spatialPlanV2;
        GeneratedSpatialContinuationV2Record candidate = artifact?.acceptedContinuation;
        // note: Proposals never affect ordinary site queries or the base projection identity.
        if (candidate == null || candidate.state != YQSpatialContinuationStateV2.Accepted)
            return true;
        if (!HasCurrentAcceptanceEvidence(plan, artifact) ||
            candidate.schemaVersion != GeneratedSpatialContinuationV2Record.SupportedSchemaVersion ||
            candidate.generationVersion != GeneratedSpatialContinuationV2Record.SupportedGenerationVersion ||
            candidate.validationVersion != GeneratedSpatialContinuationV2Record.SupportedValidationVersion ||
            candidate.hashAlgorithm != GeneratedSpatialContinuationV2Record.SupportedHashAlgorithm ||
            candidate.worldSeed != artifact.worldSeed || candidate.parentSpatialContentHash != artifact.contentHash ||
            candidate.revision <= 0 || candidate.locations == null || candidate.locations.Count == 0 ||
            candidate.validationErrors == null || candidate.validationErrors.Count != 0 ||
            string.IsNullOrWhiteSpace(candidate.contentHash) || candidate.contentHash != candidate.validatedContentHash)
        {
            failure = "Accepted V2 continuation has an unsupported or stale parent-bound identity.";
            return false;
        }
        try
        {
            // note: Actual checksums reject unrevisioned mutable corruption; this bounded query cost needs profiling when accepted continuation grows.
            if (candidate.parentMemberFootprintHash != YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(artifact) ||
                candidate.contentHash != YQSpatialContinuationHasherV2.ComputeContentHash(candidate))
            {
                failure = "Accepted V2 continuation checksum or parent member footprint changed.";
                return false;
            }
        }
        catch (Exception exception)
        {
            failure = "Accepted V2 continuation checksum is unreadable: " + exception.Message;
            return false;
        }
        continuation = candidate;
        fingerprint = candidate.schemaVersion + "|" + candidate.revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + candidate.contentHash;
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
