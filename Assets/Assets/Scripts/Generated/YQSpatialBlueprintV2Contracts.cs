using System;
using System.Collections.Generic;

public enum YQTerrainFieldKindV2
{
    Unknown = 0,
    BaseSurface = 1,
    RidgeChain = 2,
    ValleyCorridor = 3,
    Basin = 4,
    Escarpment = 5,
    HillCluster = 6,
    CaveMass = 7,
    SiteReserve = 8
}

public enum YQHydrologyKindV2
{
    Unknown = 0,
    River = 1,
    Lake = 2,
    Wetland = 3,
    Coastline = 4,
    Waterfall = 5
}

public enum YQRouteClassV2
{
    Unknown = 0,
    PrimaryRoad = 1,
    SecondaryRoad = 2,
    Trail = 3,
    ServiceRoute = 4
}

public enum YQRouteCrossingKindV2
{
    Unknown = 0,
    Ford = 1,
    Bridge = 2,
    Causeway = 3,
    Ferry = 4
}

public enum YQSiteKindV2
{
    Unknown = 0,
    Origin = 1,
    Settlement = 2,
    HostileSite = 3,
    PointOfInterest = 4,
    NaturalFeature = 5
}

public enum YQSitePlacementModeV2
{
    Unknown = 0,
    RouteFrontage = 1,
    RouteJunction = 2,
    DefensibleEdge = 3,
    FeatureAdjacent = 4,
    WaterAdjacent = 5,
    Secluded = 6
}

public enum YQSpatialRelationshipKindV2
{
    Unknown = 0,
    Contains = 1,
    Adjacent = 2,
    FrontsRoute = 3,
    ConcealedBy = 4,
    Behind = 5,
    Overlooks = 6,
    FedBy = 7,
    Crosses = 8,
    Connects = 9
}

[Serializable]
public sealed class YQBlueprintPointV2
{
    public float x;
    public float z;
    public float normalizedElevation;
    public float width;
}

[Serializable]
public sealed class YQRegionDomainV2
{
    public string regionId = string.Empty;
    public float centerX;
    public float centerZ;
    public float radius;
    public float elevationBias;
    public float moisture;
    public float ruggedness;
    public float civilizationDensity;
    public float danger;
    public List<string> biomeTags = new List<string>();

    public void EnsureCollections()
    {
        biomeTags ??= new List<string>();
    }
}

[Serializable]
public sealed class YQTerrainFieldV2
{
    public string fieldId = string.Empty;
    public string parentRegionId = string.Empty;
    public YQTerrainFieldKindV2 kind = YQTerrainFieldKindV2.Unknown;
    public float strength;
    public float radius;
    public float falloff;
    public List<YQBlueprintPointV2> controlPoints =
        new List<YQBlueprintPointV2>();
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        controlPoints ??= new List<YQBlueprintPointV2>();
        tags ??= new List<string>();
    }
}

[Serializable]
public sealed class YQHydrologyFeatureV2
{
    public string hydrologyId = string.Empty;
    public string parentRegionId = string.Empty;
    public YQHydrologyKindV2 kind = YQHydrologyKindV2.Unknown;
    public string sourceTerrainFieldId = string.Empty;
    public string sinkHydrologyId = string.Empty;
    public float waterLevelNormalized;
    public float nominalWidth;
    public float nominalDepth;
    public List<YQBlueprintPointV2> controlPoints =
        new List<YQBlueprintPointV2>();
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        controlPoints ??= new List<YQBlueprintPointV2>();
        tags ??= new List<string>();
    }
}

[Serializable]
public sealed class YQRouteCrossingV2
{
    public string crossingId = string.Empty;
    public string hydrologyId = string.Empty;
    public YQRouteCrossingKindV2 kind = YQRouteCrossingKindV2.Unknown;
    public float x;
    public float z;
    public float requiredSpan;
}

[Serializable]
public sealed class YQRouteCorridorV2
{
    public string routeId = string.Empty;
    public string sourceSemanticRouteId = string.Empty;
    public string fromSiteId = string.Empty;
    public string toSiteId = string.Empty;
    public string parentRegionId = string.Empty;
    public YQRouteClassV2 routeClass = YQRouteClassV2.Unknown;
    public float width;
    public float shoulderWidth;
    public float maximumGradeDegrees;
    public List<YQBlueprintPointV2> controlPoints =
        new List<YQBlueprintPointV2>();
    public List<YQRouteCrossingV2> crossings =
        new List<YQRouteCrossingV2>();
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        controlPoints ??= new List<YQBlueprintPointV2>();
        crossings ??= new List<YQRouteCrossingV2>();
        tags ??= new List<string>();
    }
}

[Serializable]
public sealed class YQSiteMemberFootprintV2
{
    // note: A member footprint is a persisted physical sector anchor, not a second site owner.
    public string memberId = string.Empty;
    public float x;
    public float z;
    public float reservedRadius = 24f;
    public int sectorIndex;
}

[Serializable]
public sealed class YQSiteAnchorV2
{
    public string siteId = string.Empty;
    public string sourceSemanticId = string.Empty;
    public string parentRegionId = string.Empty;
    public YQSiteKindV2 kind = YQSiteKindV2.Unknown;
    public YQSitePlacementModeV2 placementMode =
        YQSitePlacementModeV2.Unknown;
    public float x;
    public float z;
    public float preferredHeadingDegrees;
    public float reservedRadius;
    public float terrainSearchRadius;
    public float maximumSlopeDegrees;
    public float minimumRouteAccess;
    public float minimumWaterAccess;
    public bool requiresTerrainConformance = true;
    public bool hiddenFromPrimaryRoute;
    // note: Multi-cell sites retain one owner while explicitly recording the neighboring sectors that belong to the same accepted identity.
    public List<YQSiteMemberFootprintV2> memberFootprint =
        new List<YQSiteMemberFootprintV2>();
    public List<YQAssetFunctionV2> requiredFunctions =
        new List<YQAssetFunctionV2>();
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        memberFootprint ??= new List<YQSiteMemberFootprintV2>();
        requiredFunctions ??= new List<YQAssetFunctionV2>();
        tags ??= new List<string>();
    }
}

[Serializable]
public sealed class YQSpatialRelationshipV2
{
    public string relationshipId = string.Empty;
    public string subjectId = string.Empty;
    public string objectId = string.Empty;
    public YQSpatialRelationshipKindV2 kind =
        YQSpatialRelationshipKindV2.Unknown;
    public float minimumDistance;
    public float maximumDistance;
    public bool required = true;
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        tags ??= new List<string>();
    }
}

[Serializable]
public sealed class YQSpatialBlueprintMetricsV2
{
    public int regionCount;
    public int terrainFieldCount;
    public int hydrologyFeatureCount;
    public int siteCount;
    public int routeCount;
    public int crossingCount;
    public int relationshipCount;
    public int connectedSettlementCount;
    public float primaryRouteLength;
    public float reservedWorldFraction;
}

[Serializable]
public sealed class YQSpatialBlueprintV2
{
    public float worldSize = 1024f;
    public string originSiteId = string.Empty;
    public List<YQRegionDomainV2> regions =
        new List<YQRegionDomainV2>();
    public List<YQTerrainFieldV2> terrainFields =
        new List<YQTerrainFieldV2>();
    public List<YQHydrologyFeatureV2> hydrology =
        new List<YQHydrologyFeatureV2>();
    public List<YQSiteAnchorV2> sites =
        new List<YQSiteAnchorV2>();
    public List<YQRouteCorridorV2> routes =
        new List<YQRouteCorridorV2>();
    public List<YQSpatialRelationshipV2> relationships =
        new List<YQSpatialRelationshipV2>();
    public YQSpatialBlueprintMetricsV2 metrics =
        new YQSpatialBlueprintMetricsV2();

    public void EnsureCollections()
    {
        regions ??= new List<YQRegionDomainV2>();
        terrainFields ??= new List<YQTerrainFieldV2>();
        hydrology ??= new List<YQHydrologyFeatureV2>();
        sites ??= new List<YQSiteAnchorV2>();
        routes ??= new List<YQRouteCorridorV2>();
        relationships ??= new List<YQSpatialRelationshipV2>();
        metrics ??= new YQSpatialBlueprintMetricsV2();

        for (int index = 0; index < regions.Count; index++)
            regions[index]?.EnsureCollections();
        for (int index = 0; index < terrainFields.Count; index++)
            terrainFields[index]?.EnsureCollections();
        for (int index = 0; index < hydrology.Count; index++)
            hydrology[index]?.EnsureCollections();
        for (int index = 0; index < sites.Count; index++)
            sites[index]?.EnsureCollections();
        for (int index = 0; index < routes.Count; index++)
            routes[index]?.EnsureCollections();
        for (int index = 0; index < relationships.Count; index++)
            relationships[index]?.EnsureCollections();
    }
}
