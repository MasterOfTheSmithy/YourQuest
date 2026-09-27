using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// note: This authority defines the shared world-space structural features used by every streamed cell.
// note: The file is intentionally kept as one deterministic authority/materializer pair so Unity recompiles one bounded script asset.
public static class YQContinuousWorldFeatureAuthority
{
    // note: Accepted terminal features continue through streamed cells instead of imposing an artificial world edge.
    // note: Continuation padding is shared with portal ownership so diagonal crossings remain discoverable on both sides.
    public const string RiverFeatureId = "continuous:river:primary";
    public const string RoadFeatureId = "continuous:road:primary";
    private const float RiverHalfWidth = 7f;
    private const float RiverDepth = 4.5f;
    private const float RoadHalfWidth = 3.5f;
    private const float AuthoredWorldHalfSize = 512f;
    // note: Keep streamed cell coordinates anchored to the authored 1024 metre terrain, whose lower-left corner is -512.
    public const float WorldGridOrigin = -AuthoredWorldHalfSize;
    // note: Accepted terminal corridors remain contract-owned while they are streamed; keep their deterministic reach comfortably beyond the beta envelope without creating an unbounded physical residency obligation.
    // note: One shared continuation limit keeps route portals, water carving, and streamed ribbons aligned with the accepted topology envelope.
    internal const float AcceptedContinuationMaxDistance = 16384f;
    // note: These are curated cave-module asset keys, not generated paths; the beta fixture uses the approved Western cave kit for a real traversable interior.
    internal const string BetaCaveStraightPrefab = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_CaveStraight.prefab";
    internal const string BetaCaveCrossPrefab = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_CaveCross.prefab";
    // note: These catalog-approved site fallbacks repair older palettes whose serialized site slot points at an unavailable prefab without inventing a new asset family.
    internal const string BetaSettlementFallbackPrefab = "Assets/HIVEMIND/HouseOnaHill/HDRP/Art/Prefabs/SM_House.prefab";
    internal const string BetaHostileFallbackPrefab = "Assets/BefourStudios/AncientDesertRuins/Art/Prefabs/SM_Building1.prefab";
    internal const string BetaLandmarkFallbackPrefab = "Assets/BefourStudios/PersepolisEmpireEnvironment/Art/Prefabs/SM_WingedLionKing.prefab";
    internal const string BetaLandmarkFallbackGatePrefab = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_BackGate.prefab";
    internal const string BetaLandmarkFallbackChurchPrefab = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_Church.prefab";
    internal const string BetaHostileFallbackRuinPrefab = "Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_Ruin_01.prefab";

    internal struct AcceptedTerminalContinuation
    {
        public Vector2 origin;
        public Vector2 direction;
        public float elevation;
        public float elevationSlope;
        public float width;
        public float depth;
        public string featureId;
        public string terminalId;
        public string downstreamId;
        public YQHydrologyKindV2 hydrologyKind;
    }

    public static void PopulatePortals(
        GeneratedSemanticChunkRecord record,
        Vector2Int coordinate,
        float cellSize,
        string seed,
        GeneratedWorldPlanRecord plan = null)
    {
        // note: Validate mutable save data once at this entry boundary, then project all four edges from the same immutable snapshot.
        YQPreparedSpatialMaterializationV2 prepared = null;
        if (plan != null)
            YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out prepared, out _);
        PopulatePreparedPortals(record, coordinate, cellSize, seed, prepared);
    }

    internal static void PopulatePreparedPortals(
        GeneratedSemanticChunkRecord record,
        Vector2Int coordinate,
        float cellSize,
        string seed,
        YQPreparedSpatialMaterializationV2 prepared,
        bool allowLegacySyntheticFallback = true)
    {
        if (record == null)
            return;
        record.EnsureCollections();
        // note: Synthetic portals fill only the individual edge that has no accepted crossing; a route or water feature elsewhere in the plan must not flatten an otherwise empty cell.
        for (int index = 0; index < record.edgeContracts.Count; index++)
        {
            GeneratedSemanticChunkEdgeContractRecord edge = record.edgeContracts[index];
            if (edge == null)
                continue;
            edge.EnsureCollections();
            bool acceptedRoad = TryPopulateAcceptedRoadPortals(edge, coordinate, cellSize, prepared);
            // note: Fall back whenever the accepted resolver produced no portal; an accepted feature can overlap a cell without actually crossing this exact edge.
            if (!acceptedRoad && allowLegacySyntheticFallback && TryGetRoadPortal(coordinate, cellSize, edge.edge, seed, out GeneratedSemanticChunkPortalRecord road))
            {
                road.portalId = RoadFeatureId + "|" + edge.canonicalKey;
                edge.routePortals.Add(road);
            }
            bool acceptedWater = TryPopulateAcceptedWaterPortals(edge, coordinate, cellSize, prepared);
            // note: Preserve the deterministic river portal on both sides of a shared border when an accepted water cell has no exact spline crossing.
            if (!acceptedWater && allowLegacySyntheticFallback && TryGetRiverPortal(coordinate, cellSize, edge.edge, seed, out GeneratedSemanticChunkPortalRecord water))
            {
                water.portalId = RiverFeatureId + "|" + edge.canonicalKey;
                edge.waterPortals.Add(water);
            }
            // note: Portal order is canonicalized after population so shared borders remain identical even when several accepted features touch one edge.
            edge.routePortals.Sort(ComparePortalsForContract);
            edge.waterPortals.Sort(ComparePortalsForContract);
        }
    }

    private static int ComparePortalsForContract(GeneratedSemanticChunkPortalRecord left, GeneratedSemanticChunkPortalRecord right)
    {
        int result = string.CompareOrdinal(left?.portalId ?? string.Empty, right?.portalId ?? string.Empty);
        if (result != 0) return result;
        result = string.CompareOrdinal(left?.featureId ?? string.Empty, right?.featureId ?? string.Empty);
        if (result != 0) return result;
        result = (left?.order ?? 0).CompareTo(right?.order ?? 0);
        if (result != 0) return result;
        result = (left?.worldX ?? 0f).CompareTo(right?.worldX ?? 0f);
        return result != 0 ? result : (left?.worldZ ?? 0f).CompareTo(right?.worldZ ?? 0f);
    }

    private static bool HasAcceptedRoadInCell(Vector2Int coordinate, float cellSize, GeneratedWorldPlanRecord plan)
    {
        if (plan == null || !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out YQPreparedSpatialMaterializationV2 prepared, out _))
            return false;
        // note: Accepted presence is based on the complete candidate set so a shorter crossing cannot be hidden by a longer route.
        List<int> indices = new List<int>();
        GetAcceptedRouteIndicesForCell(prepared, coordinate, cellSize, indices);
        List<AcceptedTerminalContinuation> continuations = new List<AcceptedTerminalContinuation>();
        GetAcceptedRouteContinuations(prepared, coordinate, cellSize, 6f, continuations);
        return indices.Count > 0 || continuations.Count > 0;
    }

    private static bool HasAcceptedRoadCategory(GeneratedWorldPlanRecord plan)
    {
        return plan != null && YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out YQPreparedSpatialMaterializationV2 prepared, out _) &&
            prepared != null && prepared.RouteCount > 0;
    }

    private static bool HasAcceptedWaterInCell(Vector2Int coordinate, float cellSize, GeneratedWorldPlanRecord plan)
    {
        if (plan == null || !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out YQPreparedSpatialMaterializationV2 prepared, out _))
            return false;
        // note: Accepted presence is based on every intersecting water feature and every eligible terminal, including area-water footprints.
        List<int> indices = new List<int>();
        GetAcceptedWaterIndicesForCell(prepared, coordinate, cellSize, indices);
        List<AcceptedTerminalContinuation> continuations = new List<AcceptedTerminalContinuation>();
        GetAcceptedWaterContinuations(prepared, coordinate, cellSize, continuations);
        return indices.Count > 0 || continuations.Count > 0;
    }

    private static bool HasAcceptedWaterCategory(GeneratedWorldPlanRecord plan)
    {
        return plan != null && YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out YQPreparedSpatialMaterializationV2 prepared, out _) &&
            prepared != null && prepared.WaterCount > 0;
    }

    private static bool TryPopulateAcceptedRoadPortals(
        GeneratedSemanticChunkEdgeContractRecord edge,
        Vector2Int coordinate,
        float cellSize,
        YQPreparedSpatialMaterializationV2 prepared)
    {
        if (prepared == null)
            return false;
        bool added = false;
        if (prepared.RouteCount > 0)
        {
        // note: Evaluate every accepted route at the shared border; choosing one route per cell can make neighboring cells disagree when two corridors meet on the same edge.
        for (int routeIndex = 0; routeIndex < prepared.RouteCount; routeIndex++)
        {
            YQSpatialMaterializationRouteV2 route = prepared.GetRoute(routeIndex);
            for (int pointIndex = 0; pointIndex + 1 < prepared.GetRoutePointCount(routeIndex); pointIndex++)
            {
                YQSpatialMaterializationRoutePointV2 first = prepared.GetRoutePoint(routeIndex, pointIndex);
                YQSpatialMaterializationRoutePointV2 second = prepared.GetRoutePoint(routeIndex, pointIndex + 1);
                // note: Shared edge contracts use the exact segment crossing; permissive padding can make only one neighbor claim a near-edge segment.
                if (!TryGetBoundaryIntersection(first.x, first.z, second.x, second.z, coordinate, cellSize, edge.edge, 0f, out float t, out float edgeT, out float worldX, out float worldZ))
                    continue;
                float surface = Mathf.Clamp01(Mathf.LerpUnclamped(first.surfaceElevationNormalized, second.surfaceElevationNormalized, t)) * YQGeneratedWorldTerrain.TerrainHeight;
                edge.routePortals.Add(new GeneratedSemanticChunkPortalRecord
                {
                    portalId = (string.IsNullOrWhiteSpace(route.routeId) ? RoadFeatureId : route.routeId) + "|" + edge.canonicalKey,
                    featureId = string.IsNullOrWhiteSpace(route.routeId) ? RoadFeatureId : route.routeId,
                    kind = "road",
                    edge = edge.edge,
                    edgeT = edgeT,
                    worldX = worldX,
                    worldZ = worldZ,
                    tangentX = second.x - first.x,
                    tangentZ = second.z - first.z,
                    width = Mathf.Max(1f, route.width),
                    depth = 0.2f,
                    surfaceElevation = surface,
                    order = pointIndex + 1,
                    downstreamId = route.toSiteId
                });
                added = true;
                break;
            }
        }
        }
        List<AcceptedTerminalContinuation> routeContinuations = new List<AcceptedTerminalContinuation>();
        GetAcceptedRouteContinuations(prepared, coordinate, cellSize, 6f, routeContinuations);
        for (int continuationIndex = 0; continuationIndex < routeContinuations.Count; continuationIndex++)
        {
            AcceptedTerminalContinuation continuation = routeContinuations[continuationIndex];
            string portalId = (continuation.terminalId ?? continuation.featureId) + "|" + edge.canonicalKey;
            if (!TryGetContinuationBoundaryIntersection(continuation, coordinate, cellSize, edge.edge, 6f, out float continuationT, out float continuationEdgeT, out float continuationX, out float continuationZ) ||
                ContainsPortalIdentity(edge.routePortals, portalId))
                continue;
            float surface = Mathf.Clamp01(continuation.elevation + continuation.elevationSlope * continuationT) * YQGeneratedWorldTerrain.TerrainHeight;
            edge.routePortals.Add(new GeneratedSemanticChunkPortalRecord
            {
                portalId = portalId,
                featureId = continuation.featureId,
                kind = "road",
                edge = edge.edge,
                edgeT = continuationEdgeT,
                worldX = continuationX,
                worldZ = continuationZ,
                tangentX = continuation.direction.x,
                tangentZ = continuation.direction.y,
                width = continuation.width,
                depth = 0.2f,
                surfaceElevation = surface,
                order = 1000 + continuationIndex,
                downstreamId = continuation.downstreamId
            });
            added = true;
        }
        return added;
    }

    private static bool TryPopulateAcceptedWaterPortals(
        GeneratedSemanticChunkEdgeContractRecord edge,
        Vector2Int coordinate,
        float cellSize,
        YQPreparedSpatialMaterializationV2 prepared)
    {
        if (prepared == null)
            return false;
        bool added = false;
        if (prepared.WaterCount > 0)
        {
        // note: Evaluate every accepted water spline at the shared border so lake/river junctions remain symmetric across independently generated cells.
        for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
        {
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            for (int pointIndex = 0; pointIndex + 1 < prepared.GetWaterPointCount(waterIndex); pointIndex++)
            {
                YQSpatialMaterializationWaterPointV2 first = prepared.GetWaterPoint(waterIndex, pointIndex);
                YQSpatialMaterializationWaterPointV2 second = prepared.GetWaterPoint(waterIndex, pointIndex + 1);
                // note: Water edge contracts use the exact spline crossing so neighboring cells cannot disagree about a padded near-border segment.
                if (!TryGetBoundaryIntersection(first.x, first.z, second.x, second.z, coordinate, cellSize, edge.edge, 0f, out float t, out float edgeT, out float worldX, out float worldZ))
                    continue;
                float surface = Mathf.Clamp01(Mathf.LerpUnclamped(first.waterSurfaceNormalized, second.waterSurfaceNormalized, t)) * YQGeneratedWorldTerrain.TerrainHeight;
                float tangentX = second.x - first.x;
                float tangentZ = second.z - first.z;
                edge.waterPortals.Add(new GeneratedSemanticChunkPortalRecord
                {
                    portalId = (string.IsNullOrWhiteSpace(water.hydrologyId) ? RiverFeatureId : water.hydrologyId) + "|" + edge.canonicalKey,
                    featureId = string.IsNullOrWhiteSpace(water.hydrologyId) ? RiverFeatureId : water.hydrologyId,
                    kind = "water",
                    edge = edge.edge,
                    edgeT = edgeT,
                    worldX = worldX,
                    worldZ = worldZ,
                    tangentX = tangentX,
                    tangentZ = tangentZ,
                    width = Mathf.Max(1f, Mathf.LerpUnclamped(first.width, second.width, t)),
                    depth = Mathf.Max(0.1f, water.nominalDepth),
                    surfaceElevation = surface,
                    bedElevation = surface - Mathf.Max(0.1f, water.nominalDepth),
                    flowX = tangentX,
                    flowZ = tangentZ,
                    order = pointIndex + 1,
                    downstreamId = water.sinkHydrologyId
                });
                added = true;
                break;
            }
        }
        }
        List<AcceptedTerminalContinuation> waterContinuations = new List<AcceptedTerminalContinuation>();
        GetAcceptedWaterContinuations(prepared, coordinate, cellSize, waterContinuations);
        for (int continuationIndex = 0; continuationIndex < waterContinuations.Count; continuationIndex++)
        {
            AcceptedTerminalContinuation continuation = waterContinuations[continuationIndex];
            string portalId = (continuation.terminalId ?? continuation.featureId) + "|" + edge.canonicalKey;
            if (!TryGetContinuationBoundaryIntersection(continuation, coordinate, cellSize, edge.edge, 8f, out float continuationT, out float continuationEdgeT, out float continuationX, out float continuationZ) ||
                ContainsPortalIdentity(edge.waterPortals, portalId))
                continue;
            float surface = Mathf.Clamp01(continuation.elevation + continuation.elevationSlope * continuationT) * YQGeneratedWorldTerrain.TerrainHeight;
            edge.waterPortals.Add(new GeneratedSemanticChunkPortalRecord
            {
                portalId = portalId,
                featureId = continuation.featureId,
                kind = "water",
                edge = edge.edge,
                edgeT = continuationEdgeT,
                worldX = continuationX,
                worldZ = continuationZ,
                tangentX = continuation.direction.x,
                tangentZ = continuation.direction.y,
                width = continuation.width,
                depth = continuation.depth,
                surfaceElevation = surface,
                bedElevation = surface - continuation.depth,
                flowX = continuation.direction.x,
                flowZ = continuation.direction.y,
                order = 1000 + continuationIndex,
                downstreamId = continuation.downstreamId
            });
            added = true;
        }
        return added;
    }

    private static bool ContainsPortal(List<GeneratedSemanticChunkPortalRecord> portals, string featureId, string edge)
    {
        if (portals == null)
            return false;
        for (int index = 0; index < portals.Count; index++)
        {
            GeneratedSemanticChunkPortalRecord portal = portals[index];
            if (portal != null && string.Equals(portal.featureId, featureId, StringComparison.Ordinal) && string.Equals(portal.edge, edge, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool ContainsPortalIdentity(List<GeneratedSemanticChunkPortalRecord> portals, string portalId)
    {
        if (portals == null || string.IsNullOrWhiteSpace(portalId))
            return false;
        for (int index = 0; index < portals.Count; index++)
            if (portals[index] != null && string.Equals(portals[index].portalId, portalId, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static bool TryGetContinuationBoundaryIntersection(
        AcceptedTerminalContinuation continuation,
        Vector2Int coordinate,
        float cellSize,
        string edge,
        float padding,
        out float rayT,
        out float edgeT,
        out float worldX,
        out float worldZ)
    {
        rayT = edgeT = worldX = worldZ = 0f;
        bool vertical = edge == "west" || edge == "east";
        float boundary = vertical
            ? WorldGridOrigin + (edge == "west" ? coordinate.x : coordinate.x + 1) * cellSize
            : WorldGridOrigin + (edge == "south" ? coordinate.y : coordinate.y + 1) * cellSize;
        float directionAxis = vertical ? continuation.direction.x : continuation.direction.y;
        if (Mathf.Abs(directionAxis) < 0.0001f)
            return false;
        rayT = (boundary - (vertical ? continuation.origin.x : continuation.origin.y)) / directionAxis;
        if (rayT < -0.001f || rayT > AcceptedContinuationMaxDistance)
            return false;
        worldX = continuation.origin.x + continuation.direction.x * rayT;
        worldZ = continuation.origin.y + continuation.direction.y * rayT;
        float span = vertical ? worldZ : worldX;
        float spanMin = WorldGridOrigin + (vertical ? coordinate.y : coordinate.x) * cellSize;
        if (span < spanMin - padding || span > spanMin + cellSize + padding)
            return false;
        edgeT = Mathf.Clamp01((span - spanMin) / Mathf.Max(1f, cellSize));
        return true;
    }

    public static bool TrySelectAcceptedRouteForCell(YQPreparedSpatialMaterializationV2 prepared, Vector2Int coordinate, float cellSize, out int selected)
    {
        selected = -1;
        if (prepared == null)
            return false;
        float bestScore = 0f;
        string bestId = string.Empty;
        for (int routeIndex = 0; routeIndex < prepared.RouteCount; routeIndex++)
        {
            YQSpatialMaterializationRouteV2 route = prepared.GetRoute(routeIndex);
            float padding = GetAcceptedRouteFootprintPadding(route);
            if (!route.hasSpatialBounds || !SpatialBoundsIntersectCell(
                    route.minimumX, route.maximumX, route.minimumZ, route.maximumZ,
                    coordinate, cellSize, padding))
                continue;
            float score = 0f;
            int pointCount = prepared.GetRoutePointCount(routeIndex);
            int firstSegment = 0;
            int lastSegment = pointCount - 2;
            if (prepared.TryGetRouteSegmentRangeForCell(routeIndex, coordinate, cellSize,
                    out int indexedFirstSegment, out int indexedLastSegment))
            {
                firstSegment = indexedFirstSegment;
                lastSegment = indexedLastSegment;
            }
            else if (Mathf.Abs(cellSize - YQSemanticWorldAuthority.CellSizeMeters) <= 0.01f)
            {
                continue;
            }
            for (int pointIndex = firstSegment; pointIndex <= lastSegment; pointIndex++)
            {
                YQSpatialMaterializationRoutePointV2 first = prepared.GetRoutePoint(routeIndex, pointIndex);
                YQSpatialMaterializationRoutePointV2 second = prepared.GetRoutePoint(routeIndex, pointIndex + 1);
                // note: Match semantic route ownership by the accepted paved width plus shoulders so boundary cells cannot advertise a road that physical admission ignores.
                if (SegmentIntersectsCell(
                        first.x,
                        first.z,
                        second.x,
                        second.z,
                        coordinate,
                        cellSize,
                        padding))
                    score += Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z));
            }
            string routeId = route.routeId ?? string.Empty;
            if (score > bestScore + 0.001f || (score > 0f && Mathf.Abs(score - bestScore) <= 0.001f && string.CompareOrdinal(routeId, bestId) < 0))
            {
                bestScore = score;
                bestId = routeId;
                selected = routeIndex;
            }
        }
        return selected >= 0;
    }

    internal static void GetAcceptedRouteIndicesForCell(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize,
        List<int> indices)
    {
        if (indices == null)
            return;
        indices.Clear();
        if (prepared == null)
            return;
        for (int routeIndex = 0; routeIndex < prepared.RouteCount; routeIndex++)
        {
            YQSpatialMaterializationRouteV2 route = prepared.GetRoute(routeIndex);
            float padding = GetAcceptedRouteFootprintPadding(route);
            if (!route.hasSpatialBounds || !SpatialBoundsIntersectCell(
                    route.minimumX, route.maximumX, route.minimumZ, route.maximumZ,
                    coordinate, cellSize, padding))
                continue;
            int pointCount = prepared.GetRoutePointCount(routeIndex);
            int firstSegment = 0;
            int lastSegment = pointCount - 2;
            bool hasIndexedSegments = prepared.TryGetRouteSegmentRangeForCell(
                routeIndex, coordinate, cellSize,
                out int indexedFirstSegment, out int indexedLastSegment);
            if (hasIndexedSegments)
            {
                firstSegment = indexedFirstSegment;
                lastSegment = indexedLastSegment;
            }
            else if (pointCount > 1 &&
                     Mathf.Abs(cellSize - YQSemanticWorldAuthority.CellSizeMeters) <= 0.01f)
            {
                continue;
            }
            bool intersects = pointCount == 1
                ? PointIntersectsCell(
                    prepared.GetRoutePoint(routeIndex, 0).x,
                    prepared.GetRoutePoint(routeIndex, 0).z,
                    coordinate,
                    cellSize,
                    padding)
                : false;
            for (int pointIndex = firstSegment; !intersects && pointIndex <= lastSegment; pointIndex++)
            {
                YQSpatialMaterializationRoutePointV2 first = prepared.GetRoutePoint(routeIndex, pointIndex);
                YQSpatialMaterializationRoutePointV2 second = prepared.GetRoutePoint(routeIndex, pointIndex + 1);
                // note: Use the same accepted footprint for selection, demand, and materialization.
                intersects = SegmentIntersectsCell(
                    first.x,
                    first.z,
                    second.x,
                    second.z,
                    coordinate,
                    cellSize,
                    padding);
            }
            if (!intersects)
                continue;
            indices.Add(routeIndex);
        }
        SortRouteIndicesById(prepared, indices);
    }

    internal static float GetAcceptedRouteFootprintPadding(YQSpatialMaterializationRouteV2 route)
    {
        // note: Preserve a minimum seam margin while honoring the accepted route's complete paved-and-shoulder footprint.
        return Mathf.Max(6f, route.width * 0.5f + route.shoulderWidth);
    }

    public static bool TrySelectAcceptedWaterForCell(YQPreparedSpatialMaterializationV2 prepared, Vector2Int coordinate, float cellSize, out int selected)
    {
        selected = -1;
        if (prepared == null)
            return false;
        float bestScore = 0f;
        string bestId = string.Empty;
        for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
        {
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            if (!water.hasSpatialBounds || !SpatialBoundsIntersectCell(
                    water.minimumX, water.maximumX, water.minimumZ, water.maximumZ,
                    coordinate, cellSize, Mathf.Max(8f, water.nominalWidth * 0.5f)))
                continue;
            float score = 0f;
            int pointCount = prepared.GetWaterPointCount(waterIndex);
            int firstSegment = 0;
            int lastSegment = pointCount - 2;
            if (prepared.TryGetWaterSegmentRangeForCell(waterIndex, coordinate, cellSize,
                    out int indexedFirstSegment, out int indexedLastSegment))
            {
                firstSegment = indexedFirstSegment;
                lastSegment = indexedLastSegment;
            }
            else if (Mathf.Abs(cellSize - YQSemanticWorldAuthority.CellSizeMeters) <= 0.01f)
            {
                continue;
            }
            for (int pointIndex = firstSegment; pointIndex <= lastSegment; pointIndex++)
            {
                YQSpatialMaterializationWaterPointV2 first = prepared.GetWaterPoint(waterIndex, pointIndex);
                YQSpatialMaterializationWaterPointV2 second = prepared.GetWaterPoint(waterIndex, pointIndex + 1);
                if (SegmentIntersectsCell(first.x, first.z, second.x, second.z, coordinate, cellSize, 8f))
                    score += Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z));
            }
            string waterId = water.hydrologyId ?? string.Empty;
            if (score > bestScore + 0.001f || (score > 0f && Mathf.Abs(score - bestScore) <= 0.001f && string.CompareOrdinal(waterId, bestId) < 0))
            {
                bestScore = score;
                bestId = waterId;
                selected = waterIndex;
            }
        }
        return selected >= 0;
    }

    internal static void GetAcceptedWaterIndicesForCell(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize,
        List<int> indices)
    {
        if (indices == null)
            return;
        indices.Clear();
        if (prepared == null)
            return;
        for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
        {
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            float padding = Mathf.Max(8f, water.nominalWidth * 0.5f);
            if (!water.hasSpatialBounds || !SpatialBoundsIntersectCell(
                    water.minimumX, water.maximumX, water.minimumZ, water.maximumZ,
                    coordinate, cellSize, padding))
                continue;
            int pointCount = prepared.GetWaterPointCount(waterIndex);
            int firstSegment = 0;
            int lastSegment = pointCount - 2;
            bool hasIndexedSegments = prepared.TryGetWaterSegmentRangeForCell(
                waterIndex, coordinate, cellSize,
                out int indexedFirstSegment, out int indexedLastSegment);
            if (hasIndexedSegments)
            {
                firstSegment = indexedFirstSegment;
                lastSegment = indexedLastSegment;
            }
            else if (pointCount > 1 &&
                     Mathf.Abs(cellSize - YQSemanticWorldAuthority.CellSizeMeters) <= 0.01f)
            {
                if ((water.kind == YQHydrologyKindV2.Lake ||
                     water.kind == YQHydrologyKindV2.Wetland ||
                     water.kind == YQHydrologyKindV2.Coastline) &&
                    AreaIntersectsCell(prepared, waterIndex, coordinate, cellSize))
                    indices.Add(waterIndex);
                continue;
            }
            bool intersects = pointCount == 1
                ? PointIntersectsCell(
                    prepared.GetWaterPoint(waterIndex, 0).x,
                    prepared.GetWaterPoint(waterIndex, 0).z,
                    coordinate,
                    cellSize,
                    padding)
                : false;
            for (int pointIndex = firstSegment; !intersects && pointIndex <= lastSegment; pointIndex++)
            {
                YQSpatialMaterializationWaterPointV2 first = prepared.GetWaterPoint(waterIndex, pointIndex);
                YQSpatialMaterializationWaterPointV2 second = prepared.GetWaterPoint(waterIndex, pointIndex + 1);
                intersects = SegmentIntersectsCell(first.x, first.z, second.x, second.z, coordinate, cellSize, padding);
            }
            if (!intersects && (water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland || water.kind == YQHydrologyKindV2.Coastline))
                intersects = AreaIntersectsCell(prepared, waterIndex, coordinate, cellSize);
            if (!intersects)
                continue;
            indices.Add(waterIndex);
        }
        SortWaterIndicesById(prepared, indices);
    }

    private static void SortRouteIndicesById(YQPreparedSpatialMaterializationV2 prepared, List<int> indices)
    {
        for (int left = 1; left < indices.Count; left++)
        {
            int value = indices[left];
            string valueId = prepared.GetRoute(value).routeId ?? string.Empty;
            int right = left - 1;
            while (right >= 0 && string.CompareOrdinal(prepared.GetRoute(indices[right]).routeId ?? string.Empty, valueId) > 0)
            {
                indices[right + 1] = indices[right];
                right--;
            }
            indices[right + 1] = value;
        }
    }

    private static void SortWaterIndicesById(YQPreparedSpatialMaterializationV2 prepared, List<int> indices)
    {
        for (int left = 1; left < indices.Count; left++)
        {
            int value = indices[left];
            string valueId = prepared.GetWater(value).hydrologyId ?? string.Empty;
            int right = left - 1;
            while (right >= 0 && string.CompareOrdinal(prepared.GetWater(indices[right]).hydrologyId ?? string.Empty, valueId) > 0)
            {
                indices[right + 1] = indices[right];
                right--;
            }
            indices[right + 1] = value;
        }
    }

    private static bool PointIntersectsCell(float x, float z, Vector2Int coordinate, float cellSize, float padding)
    {
        float minX = WorldGridOrigin + coordinate.x * cellSize - padding;
        float maxX = WorldGridOrigin + (coordinate.x + 1) * cellSize + padding;
        float minZ = WorldGridOrigin + coordinate.y * cellSize - padding;
        float maxZ = WorldGridOrigin + (coordinate.y + 1) * cellSize + padding;
        return x >= minX && x <= maxX && z >= minZ && z <= maxZ;
    }

    private static bool SpatialBoundsIntersectCell(
        float minimumX,
        float maximumX,
        float minimumZ,
        float maximumZ,
        Vector2Int coordinate,
        float cellSize,
        float padding)
    {
        // note: This broad phase may admit extra candidates but never changes the exact segment, area, or portal intersection checks below.
        float cellMinimumX = WorldGridOrigin + coordinate.x * cellSize - padding;
        float cellMaximumX = WorldGridOrigin + (coordinate.x + 1) * cellSize + padding;
        float cellMinimumZ = WorldGridOrigin + coordinate.y * cellSize - padding;
        float cellMaximumZ = WorldGridOrigin + (coordinate.y + 1) * cellSize + padding;
        return minimumX <= cellMaximumX && maximumX >= cellMinimumX &&
               minimumZ <= cellMaximumZ && maximumZ >= cellMinimumZ;
    }

    private static bool AreaIntersectsCell(YQPreparedSpatialMaterializationV2 prepared, int waterIndex, Vector2Int coordinate, float cellSize)
    {
        if (prepared == null || prepared.GetWaterPointCount(waterIndex) <= 0)
            return false;
        // note: Keep the same vertex-average containment rule while reusing the immutable V2 projection's precomputed center.
        Vector2 center = prepared.GetWaterAreaCenter(waterIndex);
        float radius = Mathf.Max(8f, prepared.GetWater(waterIndex).nominalWidth * 0.5f);
        return PointIntersectsCell(center.x, center.y, coordinate, cellSize, radius);
    }

    internal static bool TryGetAcceptedRouteContinuation(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize,
        float continuationPadding,
        out AcceptedTerminalContinuation continuation)
    {
        continuation = default;
        if (prepared == null)
            return false;
        float bestDistance = float.PositiveInfinity;
        bool found = false;
        for (int routeIndex = 0; routeIndex < prepared.RouteCount; routeIndex++)
        {
            YQSpatialMaterializationRouteV2 route = prepared.GetRoute(routeIndex);
            int pointCount = prepared.GetRoutePointCount(routeIndex);
            if (pointCount < 2)
                continue;
            for (int endpointIndex = 0; endpointIndex <= 1; endpointIndex++)
            {
                int endpointPoint = endpointIndex == 0 ? 0 : pointCount - 1;
                int adjacentPoint = endpointIndex == 0 ? 1 : pointCount - 2;
                YQSpatialMaterializationRoutePointV2 endpoint = prepared.GetRoutePoint(routeIndex, endpointPoint);
                YQSpatialMaterializationRoutePointV2 adjacent = prepared.GetRoutePoint(routeIndex, adjacentPoint);
                // note: Endpoint minus its inward neighbor already points outward for both the first and last control points.
                Vector2 direction = new Vector2(endpoint.x - adjacent.x, endpoint.z - adjacent.z).normalized;
                if (!TryGetOutwardBoundaryDistance(endpoint.x, endpoint.z, direction, out _))
                    continue;
                Vector2 cellCenter = new Vector2(WorldGridOrigin + coordinate.x * cellSize + cellSize * 0.5f, WorldGridOrigin + coordinate.y * cellSize + cellSize * 0.5f);
                float distanceToCell = Vector2.Dot(cellCenter - new Vector2(endpoint.x, endpoint.z), direction);
                if (distanceToCell < -cellSize * 0.75f)
                    continue;
                if (!TryClipRayToCell(endpoint.x, endpoint.z, direction, coordinate, cellSize, continuationPadding, out float rayStart, out float rayEnd))
                    continue;
                float distance = Vector2.Distance(cellCenter, new Vector2(endpoint.x, endpoint.z));
                string featureId = route.routeId;
                if (!found || distance < bestDistance || (Mathf.Abs(distance - bestDistance) <= 0.001f && string.CompareOrdinal(featureId, continuation.featureId) < 0))
                {
                    float segmentLength = Mathf.Max(0.001f, Vector2.Distance(new Vector2(endpoint.x, endpoint.z), new Vector2(adjacent.x, adjacent.z)));
                    float signedSlope = (endpoint.surfaceElevationNormalized - adjacent.surfaceElevationNormalized) / segmentLength;
                    continuation = new AcceptedTerminalContinuation
                    {
                        origin = new Vector2(endpoint.x, endpoint.z),
                        direction = direction,
                        elevation = endpoint.surfaceElevationNormalized,
                        elevationSlope = signedSlope,
                        width = Mathf.Max(1f, route.width),
                        depth = 0.2f,
                        featureId = string.IsNullOrWhiteSpace(featureId) ? RoadFeatureId : featureId,
                        downstreamId = route.toSiteId
                    };
                    bestDistance = distance;
                    found = true;
                }
            }
        }
        return found;
    }

    internal static bool TryGetAcceptedWaterContinuation(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize,
        out AcceptedTerminalContinuation continuation)
    {
        continuation = default;
        if (prepared == null)
            return false;
        float bestDistance = float.PositiveInfinity;
        bool found = false;
        for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
        {
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            int pointCount = prepared.GetWaterPointCount(waterIndex);
            if (pointCount < 2)
                continue;
            for (int endpointIndex = 0; endpointIndex <= 1; endpointIndex++)
            {
                int endpointPoint = endpointIndex == 0 ? 0 : pointCount - 1;
                int adjacentPoint = endpointIndex == 0 ? 1 : pointCount - 2;
                YQSpatialMaterializationWaterPointV2 endpoint = prepared.GetWaterPoint(waterIndex, endpointPoint);
                YQSpatialMaterializationWaterPointV2 adjacent = prepared.GetWaterPoint(waterIndex, adjacentPoint);
                // note: Endpoint minus its inward neighbor already points outward for both terminal directions.
                Vector2 direction = new Vector2(endpoint.x - adjacent.x, endpoint.z - adjacent.z).normalized;
                if (!TryGetOutwardBoundaryDistance(endpoint.x, endpoint.z, direction, out _))
                    continue;
                Vector2 cellCenter = new Vector2(WorldGridOrigin + coordinate.x * cellSize + cellSize * 0.5f, WorldGridOrigin + coordinate.y * cellSize + cellSize * 0.5f);
                if (!TryClipRayToCell(endpoint.x, endpoint.z, direction, coordinate, cellSize, 8f, out _, out _))
                    continue;
                float distance = Vector2.Distance(cellCenter, new Vector2(endpoint.x, endpoint.z));
                string featureId = water.hydrologyId;
                if (!found || distance < bestDistance || (Mathf.Abs(distance - bestDistance) <= 0.001f && string.CompareOrdinal(featureId, continuation.featureId) < 0))
                {
                    float segmentLength = Mathf.Max(0.001f, Vector2.Distance(new Vector2(endpoint.x, endpoint.z), new Vector2(adjacent.x, adjacent.z)));
                    float signedSlope = (endpoint.waterSurfaceNormalized - adjacent.waterSurfaceNormalized) / segmentLength;
                    continuation = new AcceptedTerminalContinuation
                    {
                        origin = new Vector2(endpoint.x, endpoint.z),
                        direction = direction,
                        elevation = endpoint.waterSurfaceNormalized,
                        elevationSlope = signedSlope,
                        width = Mathf.Max(1f, water.nominalWidth > 0f ? water.nominalWidth : endpoint.width),
                        depth = Mathf.Max(0.1f, water.nominalDepth),
                        featureId = string.IsNullOrWhiteSpace(featureId) ? RiverFeatureId : featureId,
                        downstreamId = water.sinkHydrologyId
                    };
                    bestDistance = distance;
                    found = true;
                }
            }
        }
        return found;
    }

    internal static void GetAcceptedRouteContinuations(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize,
        float continuationPadding,
        List<AcceptedTerminalContinuation> continuations)
    {
        if (continuations == null)
            return;
        continuations.Clear();
        if (prepared == null)
            return;
        for (int routeIndex = 0; routeIndex < prepared.RouteCount; routeIndex++)
        {
            YQSpatialMaterializationRouteV2 route = prepared.GetRoute(routeIndex);
            int pointCount = prepared.GetRoutePointCount(routeIndex);
            if (pointCount < 2)
                continue;
            for (int endpointIndex = 0; endpointIndex <= 1; endpointIndex++)
            {
                int endpointPoint = endpointIndex == 0 ? 0 : pointCount - 1;
                int adjacentPoint = endpointIndex == 0 ? 1 : pointCount - 2;
                YQSpatialMaterializationRoutePointV2 endpoint = prepared.GetRoutePoint(routeIndex, endpointPoint);
                YQSpatialMaterializationRoutePointV2 adjacent = prepared.GetRoutePoint(routeIndex, adjacentPoint);
                Vector2 direction = new Vector2(endpoint.x - adjacent.x, endpoint.z - adjacent.z).normalized;
                if (direction.sqrMagnitude < 0.0001f || !TryGetOutwardBoundaryDistance(endpoint.x, endpoint.z, direction, out _))
                    continue;
                if (!TryClipRayToCell(endpoint.x, endpoint.z, direction, coordinate, cellSize, continuationPadding, out _, out _))
                    continue;
                float segmentLength = Mathf.Max(0.001f, Vector2.Distance(new Vector2(endpoint.x, endpoint.z), new Vector2(adjacent.x, adjacent.z)));
                continuations.Add(new AcceptedTerminalContinuation
                {
                    origin = new Vector2(endpoint.x, endpoint.z),
                    direction = direction,
                    elevation = endpoint.surfaceElevationNormalized,
                    elevationSlope = (endpoint.surfaceElevationNormalized - adjacent.surfaceElevationNormalized) / segmentLength,
                    width = Mathf.Max(1f, route.width),
                    depth = 0.2f,
                    featureId = string.IsNullOrWhiteSpace(route.routeId) ? RoadFeatureId : route.routeId,
                    terminalId = (string.IsNullOrWhiteSpace(route.routeId) ? RoadFeatureId : route.routeId) + "|terminal:" + endpointIndex,
                    downstreamId = route.toSiteId,
                    hydrologyKind = YQHydrologyKindV2.Unknown
                });
            }
        }
        SortContinuationsByIdentity(continuations);
    }

    internal static void GetAcceptedWaterContinuations(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize,
        List<AcceptedTerminalContinuation> continuations)
    {
        if (continuations == null)
            return;
        continuations.Clear();
        if (prepared == null)
            return;
        for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
        {
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            // note: Area water and sink-linked rivers terminate in their accepted receiver; only free flowing features may emit continuation rays.
            if (water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland || water.kind == YQHydrologyKindV2.Coastline ||
                !string.IsNullOrWhiteSpace(water.sinkHydrologyId))
                continue;
            int pointCount = prepared.GetWaterPointCount(waterIndex);
            if (pointCount < 2)
                continue;
            for (int endpointIndex = 0; endpointIndex <= 1; endpointIndex++)
            {
                int endpointPoint = endpointIndex == 0 ? 0 : pointCount - 1;
                int adjacentPoint = endpointIndex == 0 ? 1 : pointCount - 2;
                YQSpatialMaterializationWaterPointV2 endpoint = prepared.GetWaterPoint(waterIndex, endpointPoint);
                YQSpatialMaterializationWaterPointV2 adjacent = prepared.GetWaterPoint(waterIndex, adjacentPoint);
                Vector2 direction = new Vector2(endpoint.x - adjacent.x, endpoint.z - adjacent.z).normalized;
                if (direction.sqrMagnitude < 0.0001f || !TryGetOutwardBoundaryDistance(endpoint.x, endpoint.z, direction, out _))
                    continue;
                if (!TryClipRayToCell(endpoint.x, endpoint.z, direction, coordinate, cellSize, 8f, out _, out _))
                    continue;
                float segmentLength = Mathf.Max(0.001f, Vector2.Distance(new Vector2(endpoint.x, endpoint.z), new Vector2(adjacent.x, adjacent.z)));
                string featureId = string.IsNullOrWhiteSpace(water.hydrologyId) ? RiverFeatureId : water.hydrologyId;
                continuations.Add(new AcceptedTerminalContinuation
                {
                    origin = new Vector2(endpoint.x, endpoint.z),
                    direction = direction,
                    elevation = endpoint.waterSurfaceNormalized,
                    elevationSlope = (endpoint.waterSurfaceNormalized - adjacent.waterSurfaceNormalized) / segmentLength,
                    width = Mathf.Max(1f, water.nominalWidth > 0f ? water.nominalWidth : endpoint.width),
                    depth = Mathf.Max(0.1f, water.nominalDepth),
                    featureId = featureId,
                    terminalId = featureId + "|terminal:" + endpointIndex,
                    downstreamId = water.sinkHydrologyId,
                    hydrologyKind = water.kind
                });
            }
        }
        SortContinuationsByIdentity(continuations);
    }

    private static void SortContinuationsByIdentity(List<AcceptedTerminalContinuation> continuations)
    {
        for (int left = 1; left < continuations.Count; left++)
        {
            AcceptedTerminalContinuation value = continuations[left];
            int right = left - 1;
            while (right >= 0 && string.CompareOrdinal(continuations[right].terminalId ?? string.Empty, value.terminalId ?? string.Empty) > 0)
            {
                continuations[right + 1] = continuations[right];
                right--;
            }
            continuations[right + 1] = value;
        }
    }

    private static bool TryGetOutwardBoundaryDistance(float x, float z, Vector2 direction, out float distance)
    {
        distance = float.PositiveInfinity;
        bool found = false;
        if (direction.x > 0.05f && x >= AuthoredWorldHalfSize - 12f)
        {
            distance = Mathf.Min(distance, Mathf.Max(0f, AuthoredWorldHalfSize - x) / direction.x);
            found = true;
        }
        if (direction.x < -0.05f && x <= -AuthoredWorldHalfSize + 12f)
        {
            distance = Mathf.Min(distance, Mathf.Max(0f, x + AuthoredWorldHalfSize) / -direction.x);
            found = true;
        }
        if (direction.y > 0.05f && z >= AuthoredWorldHalfSize - 12f)
        {
            distance = Mathf.Min(distance, Mathf.Max(0f, AuthoredWorldHalfSize - z) / direction.y);
            found = true;
        }
        if (direction.y < -0.05f && z <= -AuthoredWorldHalfSize + 12f)
        {
            distance = Mathf.Min(distance, Mathf.Max(0f, z + AuthoredWorldHalfSize) / -direction.y);
            found = true;
        }
        return found;
    }

    internal static bool TryClipRayToCell(float originX, float originZ, Vector2 direction, Vector2Int coordinate, float cellSize, float padding, out float startT, out float endT)
    {
        startT = 0f;
        endT = AcceptedContinuationMaxDistance;
        return ClipSegmentAxis(originX, direction.x, WorldGridOrigin + coordinate.x * cellSize - padding, WorldGridOrigin + (coordinate.x + 1) * cellSize + padding, ref startT, ref endT) &&
            ClipSegmentAxis(originZ, direction.y, WorldGridOrigin + coordinate.y * cellSize - padding, WorldGridOrigin + (coordinate.y + 1) * cellSize + padding, ref startT, ref endT) &&
            startT <= endT;
    }

    public static bool TryValidateBoundaryContinuation(out string failure)
    {
        failure = string.Empty;
        // note: This lightweight regression probe keeps a source endpoint three metres inside each neighboring cell and verifies that the shared emitter reaches the exact boundary with an unclamped parameter.
        if (!ValidateBoundaryDirection(340f, 509f, 512f, 640f, out failure))
            return false;
        if (!ValidateBoundaryDirection(-340f, -509f, -640f, -512f, out failure))
            return false;
        // note: Translate the terminal regression rays with the centered cell grid so the synthetic probe still begins just inside its tested edge.
        if (!ValidateTerminalRay(new Vector2(WorldGridOrigin + 509f, WorldGridOrigin + 140f), Vector2.right, 6f, "east", out failure) ||
            !ValidateTerminalRay(new Vector2(WorldGridOrigin - 509f, WorldGridOrigin - 140f), Vector2.left, 6f, "west", out failure) ||
            !ValidateTerminalRay(new Vector2(WorldGridOrigin + 140f, WorldGridOrigin + 509f), Vector2.up, 8f, "north", out failure) ||
            !ValidateTerminalRay(new Vector2(WorldGridOrigin - 140f, WorldGridOrigin - 509f), Vector2.down, 8f, "south", out failure))
            return false;
        return true;
    }

    private static bool ValidateTerminalRay(Vector2 origin, Vector2 direction, float padding, string outgoingEdge, out string failure)
    {
        failure = string.Empty;
        AcceptedTerminalContinuation continuation = new AcceptedTerminalContinuation
        {
            origin = origin,
            direction = direction.normalized,
            elevation = 0.4f,
            elevationSlope = 0.001f,
            width = 7f,
            featureId = direction.x != 0f ? RoadFeatureId : RiverFeatureId,
            downstreamId = "terminal-regression"
        };
        bool vertical = outgoingEdge == "east" || outgoingEdge == "west";
        int start = vertical ? (direction.x > 0f ? 4 : -5) : (direction.y > 0f ? 4 : -5);
        for (int index = 0; index < 3; index++)
        {
            // note: Keep the non-traveling coordinate on the same side of the origin so the probe exercises a real three-cell ray rather than an unrelated diagonal.
            int fixedCoordinate = direction.x < 0f || direction.y < 0f ? -2 : 1;
            Vector2Int coordinate = vertical ? new Vector2Int(start + (direction.x > 0f ? index : -index), fixedCoordinate) : new Vector2Int(fixedCoordinate, start + (direction.y > 0f ? index : -index));
            if (!TryClipRayToCell(origin.x, origin.y, continuation.direction, coordinate, 128f, padding, out _, out float endT) || endT <= 0f)
            {
                failure = "terminal continuation ray did not traverse cell " + coordinate;
                return false;
            }
            if (!TryGetContinuationBoundaryIntersection(continuation, coordinate, 128f, outgoingEdge, padding, out _, out _, out _, out _))
            {
                failure = "terminal continuation ray did not publish " + outgoingEdge + " portal for cell " + coordinate;
                return false;
            }
        }
        return true;
    }

    private static bool ValidateBoundaryDirection(float firstX, float secondX, float cellMinimumX, float cellMaximumX, out string failure)
    {
        failure = string.Empty;
        List<Vector3> points = new List<Vector3>();
        List<int> breaks = new List<int>();
        bool hasPrevious = false;
        Vector2 previous = Vector2.zero;
        if (!YQContinuousWorldFeatureMaterializer.AppendAcceptedSegmentPoints(
                points,
                firstX, 0f, secondX, 0f,
                0.4f, 0.39f,
                cellMinimumX - 8f, cellMaximumX + 8f, -64f, 64f,
                8f,
                cellMinimumX, cellMaximumX, -64f, 64f,
                0f, 100f, 0f,
                breaks, ref hasPrevious, ref previous) ||
            points.Count < 2)
        {
            failure = "boundary continuation emitted no span";
            return false;
        }
        float minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        for (int index = 0; index < points.Count; index++)
        {
            minimumX = Mathf.Min(minimumX, points[index].x);
            maximumX = Mathf.Max(maximumX, points[index].x);
        }
        if (secondX > firstX && maximumX < cellMinimumX - 0.01f)
        {
            failure = "positive boundary continuation remained clamped at the authored endpoint";
            return false;
        }
        if (secondX < firstX && minimumX > cellMaximumX + 0.01f)
        {
            failure = "negative boundary continuation remained clamped at the authored endpoint";
            return false;
        }
        return true;
    }

    private static bool SegmentIntersectsCell(float firstX, float firstZ, float secondX, float secondZ, Vector2Int coordinate, float cellSize, float padding)
    {
        float minX = WorldGridOrigin + coordinate.x * cellSize - padding;
        float maxX = WorldGridOrigin + (coordinate.x + 1) * cellSize + padding;
        float minZ = WorldGridOrigin + coordinate.y * cellSize - padding;
        float maxZ = WorldGridOrigin + (coordinate.y + 1) * cellSize + padding;
        float tMin = 0f;
        float tMax = 1f;
        float dx = secondX - firstX;
        float dz = secondZ - firstZ;
        if (!ClipSegmentAxis(firstX, dx, minX, maxX, ref tMin, ref tMax) ||
            !ClipSegmentAxis(firstZ, dz, minZ, maxZ, ref tMin, ref tMax))
            return false;
        return tMin <= tMax;
    }

    private static bool ClipSegmentAxis(float start, float delta, float minimum, float maximum, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(delta) < 0.0001f)
            return start >= minimum && start <= maximum;
        float inverse = 1f / delta;
        float enter = (minimum - start) * inverse;
        float exit = (maximum - start) * inverse;
        if (enter > exit) { float swap = enter; enter = exit; exit = swap; }
        tMin = Mathf.Max(tMin, enter);
        tMax = Mathf.Min(tMax, exit);
        return tMin <= tMax;
    }

    private static bool TryGetBoundaryIntersection(
        float firstX,
        float firstZ,
        float secondX,
        float secondZ,
        Vector2Int coordinate,
        float cellSize,
        string edge,
        float continuationPadding,
        out float segmentT,
        out float edgeT,
        out float worldX,
        out float worldZ)
    {
        segmentT = edgeT = worldX = worldZ = 0f;
        bool vertical = edge == "west" || edge == "east";
        float boundary = vertical
            // note: Accepted control points are global coordinates, so edge boundaries must use the same centered grid origin.
            ? WorldGridOrigin + (edge == "west" ? coordinate.x : coordinate.x + 1) * cellSize
            : WorldGridOrigin + (edge == "south" ? coordinate.y : coordinate.y + 1) * cellSize;
        float firstAxis = vertical ? firstX : firstZ;
        float secondAxis = vertical ? secondX : secondZ;
        float delta = secondAxis - firstAxis;
        if (Mathf.Abs(delta) < 0.0001f)
            return false;
        segmentT = (boundary - firstAxis) / delta;
        // note: Match the mesh emitter's short terminal extrapolation so a persisted edge portal is present wherever the rendered accepted feature crosses into the continuation cell.
        float segmentLength = Mathf.Max(0.001f, Mathf.Sqrt(
            (secondX - firstX) * (secondX - firstX) +
            (secondZ - firstZ) * (secondZ - firstZ)));
        float maximumExtensionT = Mathf.Max(0f, continuationPadding) / segmentLength;
        if (segmentT < -maximumExtensionT - 0.0001f || segmentT > 1f + maximumExtensionT + 0.0001f)
            return false;
        worldX = Mathf.LerpUnclamped(firstX, secondX, segmentT);
        worldZ = Mathf.LerpUnclamped(firstZ, secondZ, segmentT);
        float span = vertical ? worldZ : worldX;
        // note: The orthogonal edge span uses the same world-space origin as the boundary axis.
        float spanMin = WorldGridOrigin + (vertical ? coordinate.y : coordinate.x) * cellSize;
        if (span < spanMin - 0.01f || span > spanMin + cellSize + 0.01f)
            return false;
        edgeT = Mathf.Clamp01((span - spanMin) / Mathf.Max(1f, cellSize));
        return true;
    }

    public static float ApplyTerrainModifiers(
        string seed,
        float worldX,
        float worldZ,
        float normalizedHeight,
        float terrainHeight)
    {
        // note: Only the river channel modifies the structural height field; roads remain conformal to the same surface.
        float riverDistance = DistanceToRiver(seed, worldX, worldZ);
        if (riverDistance < RiverHalfWidth * 2f)
        {
            float falloff = 1f - Mathf.Clamp01(riverDistance / (RiverHalfWidth * 2f));
            float targetBed = RiverSurfaceElevation(seed, worldZ, terrainHeight) - RiverDepth;
            // note: Blend in normalized space before taking the minimum so metre-space terrain height is never compared with a normalized value.
            float blendedNormalized = Mathf.Lerp(normalizedHeight, targetBed / Mathf.Max(1f, terrainHeight), falloff);
            normalizedHeight = Mathf.Min(normalizedHeight, blendedNormalized);
        }
        return Mathf.Clamp01(normalizedHeight);
    }

    public static float ApplyAcceptedTerrainModifiers(GeneratedWorldPlanRecord plan, float worldX, float worldZ, float normalizedHeight, float terrainHeight)
    {
        if (plan == null || !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out YQPreparedSpatialMaterializationV2 prepared, out _))
            return normalizedHeight;
        return ApplyAcceptedTerrainModifiers(prepared, worldX, worldZ, normalizedHeight, terrainHeight);
    }

    public static float ApplyAcceptedTerrainModifiers(YQPreparedSpatialMaterializationV2 prepared, float worldX, float worldZ, float normalizedHeight, float terrainHeight)
    {
        return TryApplyAcceptedWaterModifier(prepared, worldX, worldZ, normalizedHeight, terrainHeight, out float modified)
            ? modified
            : normalizedHeight;
    }

    public static bool TryApplyAcceptedWaterModifier(
        YQPreparedSpatialMaterializationV2 prepared,
        float worldX,
        float worldZ,
        float normalizedHeight,
        float terrainHeight,
        out float modifiedHeight)
    {
        return TryApplyAcceptedWaterModifier(prepared, -1, worldX, worldZ, normalizedHeight, terrainHeight, out modifiedHeight);
    }

    public static bool TryApplyAcceptedWaterModifierForFeature(
        YQPreparedSpatialMaterializationV2 prepared,
        int waterIndex,
        float worldX,
        float worldZ,
        float normalizedHeight,
        float terrainHeight,
        out float modifiedHeight)
    {
        return TryApplyAcceptedWaterModifier(prepared, waterIndex, worldX, worldZ, normalizedHeight, terrainHeight, out modifiedHeight);
    }

    private static bool TryApplyAcceptedWaterModifier(
        YQPreparedSpatialMaterializationV2 prepared,
        int selectedWaterIndex,
        float worldX,
        float worldZ,
        float normalizedHeight,
        float terrainHeight,
        out float modifiedHeight)
    {
        modifiedHeight = normalizedHeight;
        if (prepared == null)
            return false;
        float result = normalizedHeight;
        bool influenced = false;
        int firstWaterIndex = selectedWaterIndex >= 0 ? selectedWaterIndex : 0;
        int lastWaterIndex = selectedWaterIndex >= 0 ? selectedWaterIndex + 1 : prepared.WaterCount;
        for (int waterIndex = firstWaterIndex; waterIndex < lastWaterIndex; waterIndex++)
        {
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            for (int pointIndex = 0; pointIndex + 1 < prepared.GetWaterPointCount(waterIndex); pointIndex++)
            {
                YQSpatialMaterializationWaterPointV2 first = prepared.GetWaterPoint(waterIndex, pointIndex);
                YQSpatialMaterializationWaterPointV2 second = prepared.GetWaterPoint(waterIndex, pointIndex + 1);
                // note: Reject samples outside a conservative segment AABB before running closest-point math across thousands of accepted river segments.
                float maximumSegmentWidth = Mathf.Max(water.nominalWidth, Mathf.Max(first.width, second.width));
                float influenceRadius = Mathf.Max(1f, maximumSegmentWidth * 0.5f) * 2f;
                if (worldX < Mathf.Min(first.x, second.x) - influenceRadius ||
                    worldX > Mathf.Max(first.x, second.x) + influenceRadius ||
                    worldZ < Mathf.Min(first.z, second.z) - influenceRadius ||
                    worldZ > Mathf.Max(first.z, second.z) + influenceRadius)
                    continue;
                Vector2 start = new Vector2(first.x, first.z), segment = new Vector2(second.x - first.x, second.z - first.z);
                float lengthSquared = Mathf.Max(0.0001f, segment.sqrMagnitude);
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(worldX, worldZ) - start, segment) / lengthSquared);
                Vector2 nearest = start + segment * t;
                float distance = Vector2.Distance(new Vector2(worldX, worldZ), nearest);
                float halfWidth = Mathf.Max(1f, Mathf.Max(water.nominalWidth, Mathf.Lerp(first.width, second.width, t)) * 0.5f);
                if (distance >= halfWidth * 2f)
                    continue;
                influenced = true;
                float falloff = 1f - Mathf.Clamp01(distance / (halfWidth * 2f));
                float targetBed = Mathf.Lerp(first.waterSurfaceNormalized, second.waterSurfaceNormalized, t) * terrainHeight - Mathf.Max(0.1f, water.nominalDepth);
                // note: Keep accepted finite-water carving in normalized space so the contract cannot over-carve from mixed units.
                // note: Evaluate every candidate from the same original base height, then take the minimum so overlap order cannot compound or change the carve.
                float blendedNormalized = Mathf.Lerp(normalizedHeight, targetBed / Mathf.Max(1f, terrainHeight), falloff);
                result = Mathf.Min(result, blendedNormalized);
            }
        }
        modifiedHeight = Mathf.Clamp01(result);
        return influenced;
    }

    internal static bool TryApplyAcceptedWaterTerminalModifiers(
        YQPreparedSpatialMaterializationV2 prepared,
        float worldX,
        float worldZ,
        float normalizedHeight,
        float terrainHeight,
        out float modifiedHeight)
    {
        // note: Free-flowing accepted endpoints share one bounded world-space ray evaluator; lakes, wetlands, coastlines, and sink-linked rivers stop at their accepted area/receiver.
        modifiedHeight = normalizedHeight;
        if (prepared == null)
            return false;
        float result = normalizedHeight;
        bool influenced = false;
        for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
        {
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            if (water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland || water.kind == YQHydrologyKindV2.Coastline ||
                !string.IsNullOrWhiteSpace(water.sinkHydrologyId))
                continue;
            int pointCount = prepared.GetWaterPointCount(waterIndex);
            if (pointCount < 2)
                continue;
            for (int endpointIndex = 0; endpointIndex <= 1; endpointIndex++)
            {
                int endpointPoint = endpointIndex == 0 ? 0 : pointCount - 1;
                int adjacentPoint = endpointIndex == 0 ? 1 : pointCount - 2;
                YQSpatialMaterializationWaterPointV2 endpoint = prepared.GetWaterPoint(waterIndex, endpointPoint);
                YQSpatialMaterializationWaterPointV2 adjacent = prepared.GetWaterPoint(waterIndex, adjacentPoint);
                Vector2 direction = new Vector2(endpoint.x - adjacent.x, endpoint.z - adjacent.z).normalized;
                if (direction.sqrMagnitude < 0.0001f || !TryGetOutwardBoundaryDistance(endpoint.x, endpoint.z, direction, out _))
                    continue;
                Vector2 offset = new Vector2(worldX, worldZ) - new Vector2(endpoint.x, endpoint.z);
                float along = Vector2.Dot(offset, direction);
                if (along < -0.01f || along >= AcceptedContinuationMaxDistance)
                    continue;
                float distance = Mathf.Abs(direction.x * offset.y - direction.y * offset.x);
                float halfWidth = Mathf.Max(1f, (water.nominalWidth > 0f ? water.nominalWidth : endpoint.width) * 0.5f);
                if (distance >= halfWidth * 2f)
                    continue;
                float falloff = 1f - Mathf.Clamp01(distance / (halfWidth * 2f));
                float segmentLength = Mathf.Max(0.001f, Vector2.Distance(new Vector2(endpoint.x, endpoint.z), new Vector2(adjacent.x, adjacent.z)));
                float slope = (endpoint.waterSurfaceNormalized - adjacent.waterSurfaceNormalized) / segmentLength;
                float targetBed = Mathf.Clamp01(endpoint.waterSurfaceNormalized + slope * along) * terrainHeight - Mathf.Max(0.1f, water.nominalDepth);
                // note: Keep terminal candidates order-independent by comparing each one against the original unmodified sample.
                result = Mathf.Min(result, Mathf.Lerp(normalizedHeight, targetBed / Mathf.Max(1f, terrainHeight), falloff));
                influenced = true;
            }
        }
        modifiedHeight = Mathf.Clamp01(result);
        return influenced;
    }

    public static bool TryGetRiverPoints(
        string seed,
        Vector2Int coordinate,
        float cellSize,
        Terrain terrain,
        out List<Vector3> points,
        out float width)
    {
        points = new List<Vector3>();
        width = RiverHalfWidth * 2f;
        float minX = WorldGridOrigin + coordinate.x * cellSize - RiverHalfWidth;
        float maxX = WorldGridOrigin + (coordinate.x + 1) * cellSize + RiverHalfWidth;
        float minZ = WorldGridOrigin + coordinate.y * cellSize;
        float maxZ = WorldGridOrigin + (coordinate.y + 1) * cellSize;
        int sampleCount = 33;
        for (int index = 0; index < sampleCount; index++)
        {
            float z = Mathf.Lerp(minZ, maxZ, index / (sampleCount - 1f));
            float x = RiverX(seed, z);
            if (x < minX - RiverHalfWidth || x > maxX + RiverHalfWidth)
                continue;
            float surface = RiverSurfaceElevation(seed, z, terrain != null && terrain.terrainData != null ? terrain.terrainData.size.y : YQGeneratedWorldTerrain.TerrainHeight) + (terrain != null ? terrain.GetPosition().y : 0f);
            points.Add(new Vector3(x, surface, z));
        }
        AddRiverSideIntersections(seed, coordinate, cellSize, terrain, points);
        points.Sort((left, right) => left.z.CompareTo(right.z));
        RemoveNearDuplicatePoints(points, true);
        return points.Count >= 2;
    }

    public static bool TryGetRoadPoints(
        string seed,
        Vector2Int coordinate,
        float cellSize,
        Terrain terrain,
        out List<Vector3> points,
        out float width)
    {
        points = new List<Vector3>();
        width = RoadHalfWidth * 2f;
        float minX = WorldGridOrigin + coordinate.x * cellSize;
        float maxX = WorldGridOrigin + (coordinate.x + 1) * cellSize;
        float minZ = WorldGridOrigin + coordinate.y * cellSize;
        float maxZ = WorldGridOrigin + (coordinate.y + 1) * cellSize;
        int sampleCount = 33;
        for (int index = 0; index < sampleCount; index++)
        {
            float x = Mathf.Lerp(minX, maxX, index / (sampleCount - 1f));
            float z = RoadZ(seed, x);
            if (z < minZ - RoadHalfWidth || z > maxZ + RoadHalfWidth)
                continue;
            points.Add(new Vector3(x, SampleTerrainHeight(terrain, x, z) + 0.035f, z));
        }
        AddRoadSideIntersections(seed, coordinate, cellSize, terrain, points);
        points.Sort((left, right) => left.x.CompareTo(right.x));
        RemoveNearDuplicatePoints(points, false);
        return points.Count >= 2;
    }

    public static bool TryGetRoadPortal(
        Vector2Int coordinate,
        float cellSize,
        string edge,
        string seed,
        out GeneratedSemanticChunkPortalRecord portal)
    {
        portal = null;
        bool vertical = edge == "west" || edge == "east";
        bool horizontal = edge == "south" || edge == "north";
        if (!vertical && !horizontal)
            return false;
        float worldX;
        float worldZ;
        if (vertical)
        {
            worldX = edge == "west" ? WorldGridOrigin + coordinate.x * cellSize : WorldGridOrigin + (coordinate.x + 1) * cellSize;
            worldZ = RoadZ(seed, worldX);
        }
        else
        {
            worldZ = edge == "south" ? WorldGridOrigin + coordinate.y * cellSize : WorldGridOrigin + (coordinate.y + 1) * cellSize;
            worldX = FindRoadXAtZ(seed, worldZ, WorldGridOrigin + coordinate.x * cellSize, WorldGridOrigin + (coordinate.x + 1) * cellSize);
            if (Mathf.Abs(RoadZ(seed, worldX) - worldZ) > RoadHalfWidth)
                return false;
        }
        if (worldZ < WorldGridOrigin + coordinate.y * cellSize - RoadHalfWidth || worldZ > WorldGridOrigin + (coordinate.y + 1) * cellSize + RoadHalfWidth)
            return false;
        float edgeT = vertical
            ? Mathf.Clamp01((worldZ - (WorldGridOrigin + coordinate.y * cellSize)) / Mathf.Max(1f, cellSize))
            : Mathf.Clamp01((worldX - (WorldGridOrigin + coordinate.x * cellSize)) / Mathf.Max(1f, cellSize));
        portal = new GeneratedSemanticChunkPortalRecord
        {
            portalId = RoadFeatureId + "|" + BuildCanonicalEdgeKey(seed, coordinate, edge, cellSize),
            featureId = RoadFeatureId,
            kind = "primary_road",
            edge = edge,
            edgeT = edgeT,
            worldX = worldX,
            worldZ = worldZ,
            tangentX = 1f,
            tangentZ = RoadSlope(seed, worldX),
            width = RoadHalfWidth * 2f,
            depth = 0.2f,
            order = 1,
            downstreamId = RoadFeatureId
        };
        return true;
    }

    public static bool TryGetRiverPortal(
        Vector2Int coordinate,
        float cellSize,
        string edge,
        string seed,
        out GeneratedSemanticChunkPortalRecord portal)
    {
        portal = null;
        bool horizontal = edge == "south" || edge == "north";
        bool vertical = edge == "west" || edge == "east";
        if (!horizontal && !vertical)
            return false;
        float worldZ;
        float worldX;
        if (horizontal)
        {
            worldZ = edge == "south" ? WorldGridOrigin + coordinate.y * cellSize : WorldGridOrigin + (coordinate.y + 1) * cellSize;
            worldX = RiverX(seed, worldZ);
        }
        else
        {
            worldX = edge == "west" ? WorldGridOrigin + coordinate.x * cellSize : WorldGridOrigin + (coordinate.x + 1) * cellSize;
            worldZ = FindRiverZAtX(seed, worldX, WorldGridOrigin + coordinate.y * cellSize, WorldGridOrigin + (coordinate.y + 1) * cellSize);
            if (Mathf.Abs(RiverX(seed, worldZ) - worldX) > RiverHalfWidth)
                return false;
        }
        if (worldX < WorldGridOrigin + coordinate.x * cellSize - RiverHalfWidth || worldX > WorldGridOrigin + (coordinate.x + 1) * cellSize + RiverHalfWidth)
            return false;
        float edgeT = horizontal
            ? Mathf.Clamp01((worldX - (WorldGridOrigin + coordinate.x * cellSize)) / Mathf.Max(1f, cellSize))
            : Mathf.Clamp01((worldZ - (WorldGridOrigin + coordinate.y * cellSize)) / Mathf.Max(1f, cellSize));
        portal = new GeneratedSemanticChunkPortalRecord
        {
            portalId = RiverFeatureId + "|" + BuildCanonicalEdgeKey(seed, coordinate, edge, cellSize),
            featureId = RiverFeatureId,
            kind = "river",
            edge = edge,
            edgeT = edgeT,
            worldX = worldX,
            worldZ = worldZ,
            tangentX = RiverSlope(seed, worldZ),
            tangentZ = 1f,
            width = RiverHalfWidth * 2f,
            depth = RiverDepth,
            flowX = RiverSlope(seed, worldZ),
            flowZ = 1f,
            surfaceElevation = RiverSurfaceElevation(seed, worldZ, YQGeneratedWorldTerrain.TerrainHeight),
            bedElevation = RiverSurfaceElevation(seed, worldZ, YQGeneratedWorldTerrain.TerrainHeight) - RiverDepth,
            order = 1,
            downstreamId = RiverFeatureId
        };
        return true;
    }

    public static float DistanceToRiver(string seed, float worldX, float worldZ)
    {
        return Mathf.Abs(worldX - RiverX(seed, worldZ));
    }

    public static float DistanceToRoad(string seed, float worldX, float worldZ)
    {
        return Mathf.Abs(worldZ - RoadZ(seed, worldX));
    }

    public static bool TryValidatePortalSymmetry(string seed, float cellSize, out string failure)
    {
        float size = Mathf.Max(32f, cellSize);
        for (int z = -2; z <= 2; z++)
        for (int x = -2; x <= 2; x++)
        {
            Vector2Int cell = new Vector2Int(x, z);
            bool roadEast = TryGetRoadPortal(cell, size, "east", seed, out GeneratedSemanticChunkPortalRecord roadA);
            bool roadWest = TryGetRoadPortal(new Vector2Int(x + 1, z), size, "west", seed, out GeneratedSemanticChunkPortalRecord roadB);
            if (roadEast != roadWest || (roadEast && (roadA.portalId != roadB.portalId || Mathf.Abs(roadA.worldX - roadB.worldX) > 0.01f || Mathf.Abs(roadA.worldZ - roadB.worldZ) > 0.01f)))
            {
                failure = "road portal mismatch at " + cell;
                return false;
            }
            bool waterNorth = TryGetRiverPortal(cell, size, "north", seed, out GeneratedSemanticChunkPortalRecord waterA);
            bool waterSouth = TryGetRiverPortal(new Vector2Int(x, z + 1), size, "south", seed, out GeneratedSemanticChunkPortalRecord waterB);
            if (waterNorth != waterSouth || (waterNorth && (waterA.portalId != waterB.portalId || Mathf.Abs(waterA.worldX - waterB.worldX) > 0.01f || Mathf.Abs(waterA.worldZ - waterB.worldZ) > 0.01f || Mathf.Abs(waterA.surfaceElevation - waterB.surfaceElevation) > 0.01f)))
            {
                failure = "water portal mismatch at " + cell;
                return false;
            }
        }
        failure = string.Empty;
        return true;
    }

    private static float RiverX(string seed, float worldZ)
    {
        float phase = Hash01((seed ?? string.Empty) + "|continuous-river-phase") * Mathf.PI * 2f;
        return -84f + worldZ * 0.20f + Mathf.Sin(worldZ * 0.00135f + phase) * 92f + Mathf.Sin(worldZ * 0.00043f + phase * 0.37f) * 34f;
    }

    private static float RoadZ(string seed, float worldX)
    {
        float phase = Hash01((seed ?? string.Empty) + "|continuous-road-phase") * Mathf.PI * 2f;
        return 116f + worldX * 0.11f + Mathf.Sin(worldX * 0.0011f + phase) * 58f + Mathf.Sin(worldX * 0.00031f + phase * 0.61f) * 22f;
    }

    private static float RiverSlope(string seed, float worldZ)
    {
        const float delta = 1f;
        return (RiverX(seed, worldZ + delta) - RiverX(seed, worldZ - delta)) / (2f * delta);
    }

    private static float FindRoadXAtZ(string seed, float z, float minX, float maxX)
    {
        float bestX = minX;
        float best = float.MaxValue;
        for (int index = 0; index <= 32; index++)
        {
            float x = Mathf.Lerp(minX, maxX, index / 32f);
            float error = Mathf.Abs(RoadZ(seed, x) - z);
            if (error < best) { best = error; bestX = x; }
        }
        return bestX;
    }

    private static float FindRiverZAtX(string seed, float x, float minZ, float maxZ)
    {
        float bestZ = minZ;
        float best = float.MaxValue;
        for (int index = 0; index <= 32; index++)
        {
            float z = Mathf.Lerp(minZ, maxZ, index / 32f);
            float error = Mathf.Abs(RiverX(seed, z) - x);
            if (error < best) { best = error; bestZ = z; }
        }
        return bestZ;
    }

    private static float RiverSurfaceElevation(string seed, float worldZ, float terrainHeight)
    {
        float safeHeight = Mathf.Max(1f, terrainHeight);
        return safeHeight * (0.30f - worldZ * 0.000012f + Mathf.Sin(worldZ * 0.0007f + Hash01((seed ?? string.Empty) + "|river-surface")) * 0.003f);
    }

    private static string BuildCanonicalEdgeKey(string seed, Vector2Int coordinate, string edge, float size)
    {
        bool vertical = edge == "west" || edge == "east";
        int boundaryIndex = vertical ? (edge == "west" ? coordinate.x : coordinate.x + 1) : (edge == "south" ? coordinate.y : coordinate.y + 1);
        int spanIndex = vertical ? coordinate.y : coordinate.x;
        return (seed ?? string.Empty) + "|" + YQContinuousWorldCellAuthority.SchemaVersion + "|" + (vertical ? "x" : "z") + "|" + boundaryIndex + "|span|" + spanIndex + "|size|" + size.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddRiverSideIntersections(string seed, Vector2Int coordinate, float cellSize, Terrain terrain, List<Vector3> points)
    {
        float minZ = WorldGridOrigin + coordinate.y * cellSize;
        float maxZ = WorldGridOrigin + (coordinate.y + 1) * cellSize;
        float height = terrain != null && terrain.terrainData != null ? terrain.terrainData.size.y : YQGeneratedWorldTerrain.TerrainHeight;
        for (int side = 0; side < 2; side++)
        {
            // note: Procedural fallback intersections use the same centered world grid as accepted splines and terrain tiles.
            float x = WorldGridOrigin + (coordinate.x + side) * cellSize;
            float z = FindRiverZAtX(seed, x, minZ, maxZ);
            if (Mathf.Abs(RiverX(seed, z) - x) <= RiverHalfWidth)
                points.Add(new Vector3(x, RiverSurfaceElevation(seed, z, height) + (terrain != null ? terrain.GetPosition().y : 0f), z));
        }
    }

    private static void AddRoadSideIntersections(string seed, Vector2Int coordinate, float cellSize, Terrain terrain, List<Vector3> points)
    {
        float minX = WorldGridOrigin + coordinate.x * cellSize;
        float maxX = WorldGridOrigin + (coordinate.x + 1) * cellSize;
        for (int side = 0; side < 2; side++)
        {
            // note: Keep the fallback road on the global grid so its portal and ribbon meet at the same world-space border.
            float z = WorldGridOrigin + (coordinate.y + side) * cellSize;
            float x = FindRoadXAtZ(seed, z, minX, maxX);
            if (Mathf.Abs(RoadZ(seed, x) - z) <= RoadHalfWidth)
                points.Add(new Vector3(x, SampleTerrainHeight(terrain, x, z) + 0.035f, z));
        }
    }

    private static void RemoveNearDuplicatePoints(List<Vector3> points, bool compareZ)
    {
        for (int index = points.Count - 1; index > 0; index--)
        {
            float delta = compareZ ? Mathf.Abs(points[index].z - points[index - 1].z) : Mathf.Abs(points[index].x - points[index - 1].x);
            if (delta < 0.01f && (points[index] - points[index - 1]).sqrMagnitude < 0.01f)
                points.RemoveAt(index);
        }
    }

    private static float RoadSlope(string seed, float worldX)
    {
        const float delta = 1f;
        return (RoadZ(seed, worldX + delta) - RoadZ(seed, worldX - delta)) / (2f * delta);
    }

    public static float SampleTerrainHeight(Terrain terrain, float x, float z)
    {
        if (terrain == null || terrain.terrainData == null)
            return 0f;
        Vector3 origin = terrain.GetPosition();
        Vector3 size = terrain.terrainData.size;
        float nx = Mathf.Clamp01((x - origin.x) / Mathf.Max(1f, size.x));
        float nz = Mathf.Clamp01((z - origin.z) / Mathf.Max(1f, size.z));
        return origin.y + terrain.terrainData.GetInterpolatedHeight(nx, nz);
    }

    private static bool TryGetHash(string value, out uint hash)
    {
        hash = 2166136261u;
        if (value == null)
            return true;
        for (int index = 0; index < value.Length; index++)
            hash = (hash ^ value[index]) * 16777619u;
        return true;
    }

    public static float Hash01(string value)
    {
        TryGetHash(value, out uint hash);
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        hash *= 0x846CA68Bu;
        hash ^= hash >> 16;
        return (hash & 0x00FFFFFFu) / 16777215f;
    }
}

// note: This materializer turns the shared feature authority into bounded Unity meshes owned by the streamed cell.
public static class YQContinuousWorldFeatureMaterializer
{
    internal readonly struct BiomeAlphamapBuildResult
    {
        public readonly float[,,] maps;
        public readonly float computationSeconds;

        public BiomeAlphamapBuildResult(float[,,] maps, float computationSeconds)
        {
            this.maps = maps;
            this.computationSeconds = computationSeconds;
        }
    }

    // note: Keep deterministic biome weights alive across terrain collider publication without exposing Unity TerrainData to the worker.
    internal sealed class PreparedBiomeAlphamap : IDisposable
    {
        private readonly Task<BiomeAlphamapBuildResult> task;
        private readonly CancellationTokenSource cancellation;
        private bool disposed;
        private bool resultTaken;

        internal PreparedBiomeAlphamap(Task<BiomeAlphamapBuildResult> task, CancellationTokenSource cancellation)
        {
            this.task = task;
            this.cancellation = cancellation;
        }

        public bool IsCompleted => task == null || task.IsCompleted;

        public float[,,] TakeMaps()
        {
            if (disposed || task == null || !task.IsCompleted || resultTaken)
                throw new InvalidOperationException("Prepared biome alphamap is unavailable or was already consumed");
            resultTaken = true;
            BiomeAlphamapBuildResult result = task.GetAwaiter().GetResult();
            YQGeneratedWorldEnvironment.RecordStreamedBiomeComputeSeconds(result.computationSeconds);
            return result.maps;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            cancellation?.Cancel();
            if (task == null || task.IsCompleted)
                cancellation?.Dispose();
            else
                task.ContinueWith(_ => cancellation.Dispose(), TaskScheduler.Default);
        }
    }

    // note: Report synchronous geometry/materialization slices so the streamer can identify which indivisible operation exceeds its frame allowance.
    private static void RecordSubstage(Action<string, float> telemetry, string stage, float startedAt)
    {
        telemetry?.Invoke(stage, Mathf.Max(0f, Time.realtimeSinceStartup - startedAt));
    }

    private static BiomeAlphamapBuildResult BuildBiomeAlphamaps(
        YQContinuousWorldCellAuthority authority,
        Vector2Int coordinate,
        float cellSize,
        int resolution,
        int[] bindings,
        CancellationToken cancellationToken)
    {
        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        int layerCount = bindings.Length;
        float[,,] maps = new float[resolution, resolution, layerCount];
        int sampleDenominator = Mathf.Max(1, resolution - 1);
        for (int z = 0; z < resolution; z++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            float worldZ = WorldGridOrigin + coordinate.y * cellSize + z / (float)sampleDenominator * cellSize;
            for (int x = 0; x < resolution; x++)
            {
                float worldX = WorldGridOrigin + coordinate.x * cellSize + x / (float)sampleDenominator * cellSize;
                authority.SampleBiomeWeightsValues(worldX, worldZ, out float forest, out float grassland, out float wetland);
                float total = 0f;
                for (int layer = 0; layer < layerCount; layer++)
                {
                    float value = bindings[layer] == 0 ? forest : bindings[layer] == 1 ? grassland : wetland;
                    maps[z, x, layer] = value;
                    total += value;
                }
                if (total <= 0.0001f)
                    maps[z, x, 0] = 1f;
                else
                    for (int layer = 0; layer < layerCount; layer++)
                        maps[z, x, layer] /= total;
            }
        }
        float elapsedSeconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - startedAt) /
            (double)System.Diagnostics.Stopwatch.Frequency);
        return new BiomeAlphamapBuildResult(maps, elapsedSeconds);
    }

    // note: Snapshot Unity TerrainLayer names on the main thread, then enqueue only deterministic authority sampling.
    internal static PreparedBiomeAlphamap BeginBiomeAlphamapPreparation(
        TerrainData data,
        YQContinuousWorldCellAuthority authority,
        Vector2Int coordinate,
        float cellSize)
    {
        if (data == null || authority == null || data.terrainLayers == null || data.terrainLayers.Length == 0)
            return null;

        int resolution = Mathf.Max(1, data.alphamapResolution);
        TerrainLayer[] layers = data.terrainLayers;
        int[] bindings = new int[layers.Length];
        for (int layer = 0; layer < layers.Length; layer++)
        {
            string name = layers[layer] != null ? (layers[layer].name ?? string.Empty).ToLowerInvariant() : string.Empty;
            bindings[layer] = name.Contains("wet") || name.Contains("marsh") || name.Contains("swamp") ? 2 :
                name.Contains("forest") || name.Contains("wood") || name.Contains("moss") ? 0 : 1;
        }

        CancellationTokenSource cancellation = new CancellationTokenSource();
        try
        {
            CancellationToken cancellationToken = cancellation.Token;
            Task<BiomeAlphamapBuildResult> task = Task.Run(
                () => BuildBiomeAlphamaps(authority, coordinate, cellSize, resolution, bindings, cancellationToken),
                cancellationToken);
            return new PreparedBiomeAlphamap(task, cancellation);
        }
        catch
        {
            cancellation.Dispose();
            throw;
        }
    }

    // note: Materialized meshes use the same centered grid origin as their semantic contracts.
    private const float WorldGridOrigin = YQContinuousWorldFeatureAuthority.WorldGridOrigin;
    public static IEnumerator PaintBiomeAlphamapsRoutine(
        TerrainData data,
        YQContinuousWorldCellAuthority authority,
        Vector2Int coordinate,
        float cellSize,
        bool preserveOriginBorder = true)
    {
        return PaintBiomeAlphamapsRoutine(data, authority, coordinate, cellSize, preserveOriginBorder, null);
    }

    internal static IEnumerator PaintBiomeAlphamapsRoutine(
        TerrainData data,
        YQContinuousWorldCellAuthority authority,
        Vector2Int coordinate,
        float cellSize,
        bool preserveOriginBorder,
        PreparedBiomeAlphamap preparedAlphamap)
    {
        if (data == null || authority == null || data.terrainLayers == null || data.terrainLayers.Length == 0)
            yield break;
        int resolution = Mathf.Max(1, data.alphamapResolution);
        int layerCount = data.terrainLayers.Length;
        if (!preserveOriginBorder)
        {
            // note: Reuse a camera-demanded map prepared during height/collider work; native TerrainData writes stay on the main thread after publication.
            bool ownsPreparation = preparedAlphamap == null;
            PreparedBiomeAlphamap preparation = preparedAlphamap ??
                BeginBiomeAlphamapPreparation(data, authority, coordinate, cellSize);
            if (preparation == null)
                yield break;
            try
            {
                while (!preparation.IsCompleted)
                    yield return null;

                float[,,] preparedMaps = preparation.TakeMaps();
                // note: Keep the 512px accepted biome map exact, but split its native upload so one TerrainData call cannot consume most of a frame.
                int rowsPerUpload = Mathf.Max(8, Mathf.CeilToInt(resolution / 4f));
                long uploadTicks = 0L;
                for (int startZ = 0; startZ < resolution; startZ += rowsPerUpload)
                {
                    int rowCount = Mathf.Min(rowsPerUpload, resolution - startZ);
                    float[,,] rows = new float[rowCount, resolution, layerCount];
                    Buffer.BlockCopy(
                        preparedMaps,
                        startZ * resolution * layerCount * sizeof(float),
                        rows,
                        0,
                        rowCount * resolution * layerCount * sizeof(float));
                    long uploadStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                    data.SetAlphamaps(0, startZ, rows);
                    uploadTicks += System.Diagnostics.Stopwatch.GetTimestamp() - uploadStartedAt;
                    if (startZ + rowCount < resolution)
                        yield return null;
                }
                YQGeneratedWorldEnvironment.RecordStreamedBiomeUploadSeconds(
                    (float)(uploadTicks /
                        (double)System.Diagnostics.Stopwatch.Frequency));
            }
            finally
            {
                // note: Only this compatibility call owns an unshared build; streamed chunks retain the task through publication and release it with the terrain owner.
                if (ownsPreparation)
                    preparation.Dispose();
            }
            yield break;
        }
        int[] bindings = new int[layerCount];
        for (int layer = 0; layer < layerCount; layer++)
        {
            string name = data.terrainLayers[layer] != null ? (data.terrainLayers[layer].name ?? string.Empty).ToLowerInvariant() : string.Empty;
            bindings[layer] = name.Contains("wet") || name.Contains("marsh") || name.Contains("swamp") ? 2 :
                name.Contains("forest") || name.Contains("wood") || name.Contains("moss") ? 0 : 1;
        }
        const int rowsPerSlice = 8;
        for (int startZ = 0; startZ < resolution; startZ += rowsPerSlice)
        {
            int rowCount = Mathf.Min(rowsPerSlice, resolution - startZ);
            float[,,] maps = new float[rowCount, resolution, layerCount];
            for (int row = 0; row < rowCount; row++)
            {
                int z = startZ + row;
                float worldZ = WorldGridOrigin + coordinate.y * cellSize + z / (float)Mathf.Max(1, resolution - 1) * cellSize;
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = WorldGridOrigin + coordinate.x * cellSize + x / (float)Mathf.Max(1, resolution - 1) * cellSize;
                    // note: Copy the canonical authored border paint onto the first continuation row/column before applying the world-space biome field.
                    if (preserveOriginBorder &&
                        (x == 0 || x == resolution - 1 || z == 0 || z == resolution - 1) &&
                        authority.TryGetOriginAlphaWeights(worldX, worldZ, out float[] originWeights) &&
                        originWeights.Length == layerCount)
                    {
                        for (int layer = 0; layer < layerCount; layer++)
                            maps[row, x, layer] = originWeights[layer];
                        continue;
                    }
                    authority.SampleBiomeWeightsValues(worldX, worldZ, out float forest, out float grassland, out float wetland);
                    float total = 0f;
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        float value = bindings[layer] == 0 ? forest : bindings[layer] == 1 ? grassland : wetland;
                        maps[row, x, layer] = value;
                        total += value;
                    }
                    if (total <= 0.0001f)
                        maps[row, x, 0] = 1f;
                    else
                        for (int layer = 0; layer < layerCount; layer++)
                            maps[row, x, layer] /= total;
                }
            }
            // note: Keep the renderer withheld until full appearance readiness, and upload the unchanged biome weights in small native strips to cap each main-thread TerrainData call.
            data.SetAlphamaps(0, startZ, maps);
            yield return null;
        }
    }

    // note: Compatibility wrapper keeps editor callers safe while streamed terrain uses the budgeted coroutine.
    public static void PaintBiomeAlphamaps(TerrainData data, YQContinuousWorldCellAuthority authority, Vector2Int coordinate, float cellSize)
    {
        IEnumerator routine = PaintBiomeAlphamapsRoutine(data, authority, coordinate, cellSize);
        while (routine.MoveNext()) { }
    }

    public static IEnumerator BuildCellRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticChunkRecord chunk,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        float cellSize,
        bool materializeContinuationFeatures,
        Action<int> completed,
        Action<string, float> substageTelemetry = null,
        Action<YQPreparedSpatialMaterializationV2> preparedResolved = null,
        YQPreparedSpatialMaterializationV2 preparedOverride = null)
    {
        int built = 0;
        if (parent == null || terrain == null || terrain.terrainData == null || plan == null || chunk == null || palette == null || registry == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        float centerX = WorldGridOrigin + chunk.chunkX * cellSize + cellSize * 0.5f;
        float centerZ = WorldGridOrigin + chunk.chunkZ * cellSize + cellSize * 0.5f;
        if (!materializeContinuationFeatures)
        {
            completed?.Invoke(0);
            yield break;
        }

        YQPreparedSpatialMaterializationV2 preparedWater = preparedOverride;
        bool requiresAcceptedV2 = YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan);
        if (requiresAcceptedV2 && preparedWater == null &&
            !YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out preparedWater,
                out string preparationFailure))
        {
            // note: A canonical V2 cell must never degrade into tree-and-rock scatter when its accepted physical projection is unavailable.
            Debug.LogError(
                "[YQContinuousWorldFeatures] ACCEPTED PROJECTION UNAVAILABLE " +
                chunk.chunkX + "," + chunk.chunkZ + ": " +
                (string.IsNullOrWhiteSpace(preparationFailure)
                    ? "unknown projection failure"
                    : preparationFailure));
            completed?.Invoke(-1);
            yield break;
        }
        // note: Share this cell transaction's hash-verified immutable projection with its post-build demand and identity checks.
        if (requiresAcceptedV2)
            preparedResolved?.Invoke(preparedWater);
        // note: Accepted V2 is itself the physical authority; a missing legacy semanticAuthority field must not reopen seed-only road or river synthesis.
        bool allowLegacySyntheticFallback = !requiresAcceptedV2 && plan.semanticAuthority == null;
        Vector2Int cellCoordinate = new Vector2Int(chunk.chunkX, chunk.chunkZ);
        // note: Let the scheduler publish the prepared projection before the accepted-demand queries begin their per-cell scans.
        yield return null;
        // note: Derive accepted demand from the immutable spatial intersections, not from whether geometry extraction happened to yield enough ribbon points.
        float substageStarted = Time.realtimeSinceStartup;
        bool acceptedRoadDemand = preparedWater != null && HasAcceptedRoadDemand(preparedWater, cellCoordinate, cellSize);
        bool acceptedWaterDemand = preparedWater != null && HasAcceptedWaterDemand(preparedWater, cellCoordinate, cellSize);
        bool acceptedCrossingDemand = preparedWater != null && HasAcceptedCrossingDemand(preparedWater, cellCoordinate, cellSize);
        bool acceptedSiteDemand = preparedWater != null && HasAcceptedSiteOwnerDemand(preparedWater, chunk, cellCoordinate, cellSize);
        bool hasAcceptedWaterPlan = preparedWater != null && preparedWater.WaterCount > 0;
        RecordSubstage(substageTelemetry, "acceptedDemandQueries", substageStarted);
        // note: Keep the demand/ownership scan separate from terrain point extraction so one cell cannot monopolize a frame.
        yield return null;
        substageStarted = Time.realtimeSinceStartup;
        // note: Reuse this cell's accepted projection; resolving it again would rehash the complete world blueprint during each route and river pass.
        bool hasAcceptedRiver = TryGetAcceptedRiverPoints(preparedWater, cellCoordinate, cellSize, terrain, out List<Vector3> river, out float riverWidth, out List<int> riverBreaks, out List<float> riverPointWidths);
        // note: Planned worlds materialize only authoritative water crossings; the legacy fallback remains isolated for unplanned editor callers.
        int riverObjects = 0;
        if (!hasAcceptedRiver && allowLegacySyntheticFallback)
        {
            hasAcceptedRiver = YQContinuousWorldFeatureAuthority.TryGetRiverPoints(plan.worldSeed, new Vector2Int(chunk.chunkX, chunk.chunkZ), cellSize, terrain, out river, out riverWidth);
            riverBreaks = null;
            riverPointWidths = null;
        }
        RecordSubstage(substageTelemetry, "riverPointExtraction", substageStarted);
        // note: Separate accepted point extraction from mesh construction so a contract lookup cannot share one frame with a large ribbon build.
        yield return null;
        if (hasAcceptedRiver)
        {
            substageStarted = Time.realtimeSinceStartup;
            Material waterMaterial = FindMaterial(registry, "water", "river") ?? BuildFallbackWaterMaterial();
            RegisterCellResource(parent, waterMaterial);
            riverObjects = BuildRibbonSpans(parent, "ContinuousRiver_" + chunk.chunkX + "_" + chunk.chunkZ, river, riverBreaks, riverWidth, waterMaterial, true, cellSize, riverPointWidths);
            RecordSubstage(substageTelemetry, "riverRibbonBuild", substageStarted);
            if (riverObjects > 0)
            {
                built += riverObjects;
                yield return null;
            }
        }
        bool hasAcceptedAreaWater = false;
        if (hasAcceptedWaterPlan)
        {
            substageStarted = Time.realtimeSinceStartup;
            List<int> acceptedWaterIndices = new List<int>();
            YQContinuousWorldFeatureAuthority.GetAcceptedWaterIndicesForCell(
                preparedWater,
                cellCoordinate,
                cellSize,
                acceptedWaterIndices);
            for (int waterIndex = 0; waterIndex < acceptedWaterIndices.Count; waterIndex++)
            {
                YQHydrologyKindV2 kind = preparedWater.GetWater(
                    acceptedWaterIndices[waterIndex]).kind;
                if (kind == YQHydrologyKindV2.Lake ||
                    kind == YQHydrologyKindV2.Wetland ||
                    kind == YQHydrologyKindV2.Coastline)
                {
                    hasAcceptedAreaWater = true;
                    break;
                }
            }
            RecordSubstage(substageTelemetry, "waterCellIntersection", substageStarted);
        }
        int areaWaterObjects = 0;
        if (hasAcceptedAreaWater)
        {
            // note: Give the scheduler a frame boundary before the grid-based area-water mesh allocates vertices and normals.
            yield return null;
            substageStarted = Time.realtimeSinceStartup;
            Material areaWaterMaterial = FindMaterial(registry, "water", "lake", "wetland") ?? BuildFallbackWaterMaterial();
            if (areaWaterMaterial != null && areaWaterMaterial.HasProperty("_FlowSpeed"))
                areaWaterMaterial.SetFloat("_FlowSpeed", 0.012f);
            RegisterCellResource(parent, areaWaterMaterial);
            areaWaterObjects = BuildAcceptedAreaWaterPatches(
                parent,
                terrain,
                preparedWater,
                new Vector2Int(chunk.chunkX, chunk.chunkZ),
                cellSize,
                areaWaterMaterial);
            RecordSubstage(substageTelemetry, "areaWaterPatchBuild", substageStarted);
            if (areaWaterObjects > 0)
            {
                built += areaWaterObjects;
                yield return null;
            }
        }

        // note: Resolve owner-cell decks before extracting road ribbons so both presentations use the same accepted route interval.
        List<AcceptedCrossingSpan> crossingSpans = new List<AcceptedCrossingSpan>();
        yield return PrepareAcceptedCrossingSpansRoutine(preparedWater, terrain, cellCoordinate, cellSize, crossingSpans, substageTelemetry);
        substageStarted = Time.realtimeSinceStartup;
        // note: The same accepted projection already passed this cell's authority check, so route extraction needs no second artifact fingerprint pass.
        bool hasAcceptedRoad = TryGetAcceptedRoadPoints(preparedWater, cellCoordinate, cellSize, terrain, out List<Vector3> road, out float roadWidth, out List<int> roadBreaks, out List<float> roadPointWidths, crossingSpans);
        // note: Roads require real destination endpoints or an explicit continuation terminal; an empty semantic cell stays wilderness.
        if (!hasAcceptedRoad && allowLegacySyntheticFallback)
        {
            hasAcceptedRoad = YQContinuousWorldFeatureAuthority.TryGetRoadPoints(plan.worldSeed, new Vector2Int(chunk.chunkX, chunk.chunkZ), cellSize, terrain, out road, out roadWidth);
            roadBreaks = null;
            roadPointWidths = null;
        }
        RecordSubstage(substageTelemetry, "roadPointExtraction", substageStarted);
        // note: Route point extraction can scan multiple accepted spans; publish its result at a separate cooperative slice before building meshes or crossings.
        yield return null;
        int roadObjects = 0;
        int crossingObjects = 0;
        if (hasAcceptedRoad)
        {
            substageStarted = Time.realtimeSinceStartup;
            Material roadMaterial = FindPaletteMaterial(palette, registry, YQWorldAssetCatalog.SlotPath, plan.worldSeed + "|continuous-road|" + chunk.chunkX + "|" + chunk.chunkZ);
            roadMaterial = roadMaterial ?? BuildFallbackRoadMaterial();
            RegisterCellResource(parent, roadMaterial);
            roadObjects = BuildRibbonSpans(parent, "ContinuousRoad_" + chunk.chunkX + "_" + chunk.chunkZ, road, roadBreaks, roadWidth, roadMaterial, false, cellSize, roadPointWidths);
            RecordSubstage(substageTelemetry, "roadRibbonBuild", substageStarted);
            if (roadObjects > 0)
            {
                built += roadObjects;
                yield return null;
            }
        }
        // note: A deck may replace every road sample in its owner cell; it must still build independently of the remaining ordinary ribbon.
        yield return BuildAcceptedCrossingsRoutine(parent, crossingSpans, registry, substageTelemetry, count => crossingObjects = count);
        built += crossingObjects;

        // note: Accepted feature categories publish independently; one successful site must not hide a missing route or water realization in the same cell.
        // note: A failed accepted category remains a hard publication failure even when an unrelated category produced a positive object count.
        if ((acceptedWaterDemand && riverObjects + areaWaterObjects <= 0) ||
            (acceptedRoadDemand && roadObjects + crossingObjects <= 0) ||
            (acceptedCrossingDemand && crossingObjects <= 0))
        {
            Debug.LogError(
                "[YQContinuousWorldFeatures] ACCEPTED STRUCTURE MISSING " +
                chunk.chunkX + "," + chunk.chunkZ +
                " river=" + hasAcceptedRiver + "/" + riverObjects +
                " areaWater=" + hasAcceptedAreaWater + "/" + areaWaterObjects +
                " road=" + hasAcceptedRoad + "/" + roadObjects +
                " acceptedWaterDemand=" + acceptedWaterDemand +
                " acceptedRoadDemand=" + acceptedRoadDemand +
                " acceptedCrossingDemand=" + acceptedCrossingDemand +
                " crossings=" + crossingObjects);
            completed?.Invoke(-1);
            yield break;
        }

        int siteObjects = 0;
        List<string> materializedSiteIds = new List<string>();
        yield return BuildAcceptedSiteRoutine(
            parent,
            terrain,
            plan,
            chunk,
            palette,
            registry,
            new Vector2Int(chunk.chunkX, chunk.chunkZ),
            cellSize,
            (count, ids) =>
            {
                siteObjects = count;
                materializedSiteIds = ids ?? new List<string>();
            },
            preparedWater);
        built += siteObjects;

        // note: Site owners are checked independently so a road or water object cannot mask a missing accepted settlement, cave, hostile site, or POI.
        if (acceptedSiteDemand && siteObjects <= 0 && !HasExistingAcceptedSiteOwner(preparedWater, cellCoordinate, cellSize))
        {
            Debug.LogError("[YQContinuousWorldFeatures] ACCEPTED SITE MISSING " +
                chunk.chunkX + "," + chunk.chunkZ + " siteDemand=true");
            completed?.Invoke(-1);
            yield break;
        }
        if (HasMissingRequiredAcceptedSiteOwner(
                preparedWater,
                chunk,
                cellCoordinate,
                cellSize,
                materializedSiteIds))
        {
            Debug.LogError(
                "[YQContinuousWorldFeatures] REQUIRED SITE ID MISSING " +
                chunk.chunkX + "," + chunk.chunkZ);
            completed?.Invoke(-1);
            yield break;
        }

        string landmarkId = YQContinuousWorldCellAuthority.BuildLandmarkFeatureId(new Vector2Int(chunk.chunkX, chunk.chunkZ));
        GeneratedSemanticSiteRecord landmarkSite = null;
        if (chunk.sites != null)
        {
            for (int siteIndex = 0; siteIndex < chunk.sites.Count; siteIndex++)
            {
                GeneratedSemanticSiteRecord candidate = chunk.sites[siteIndex];
                if (candidate != null && string.Equals(candidate.siteId, landmarkId, StringComparison.Ordinal))
                {
                    landmarkSite = candidate;
                    break;
                }
            }
        }
        // note: Legacy records retain featureIds as an index; newer records use the typed site position as the placement authority.
        bool landmarkPlanned = landmarkSite != null || (chunk.featureIds != null && chunk.featureIds.Contains(landmarkId));
        if (landmarkPlanned)
        {
            // note: Keep landmark prefab/material repair outside the route/water publication slice so a reviewed POI cannot create a long combined frame.
            yield return null;
            string landmarkAssetSlot = landmarkSite != null &&
                string.Equals(landmarkSite.assetSlot, YQWorldAssetCatalog.SlotLargeStructure, StringComparison.Ordinal)
                ? landmarkSite.assetSlot
                : YQWorldAssetCatalog.SlotLargeStructure;
            GeneratedAssetReferenceRecord reference = YQWorldAssetCatalog.PickAssetForSlot(
                palette,
                landmarkAssetSlot,
                plan.worldSeed + "|continuous-landmark|" + chunk.chunkX + "|" + chunk.chunkZ);
            GameObject prefab = reference != null ? registry.ResolvePrefab(reference.assetPath) : null;
            if (prefab != null)
            {
                float x;
                float z;
                if (landmarkSite != null)
                {
                    x = landmarkSite.worldX;
                    z = landmarkSite.worldZ;
                }
                else
                {
                    YQContinuousWorldCellAuthority.GetLandmarkPosition(new Vector2Int(chunk.chunkX, chunk.chunkZ), cellSize, plan.worldSeed, out x, out z);
                }
                GameObject landmark = UnityEngine.Object.Instantiate(prefab, parent);
                landmark.name = "ContinuousLandmark_" + chunk.chunkX + "_" + chunk.chunkZ;
                // note: Register the accepted landmark identity at materialization so persisted overlays can target the real generated object before gameplay publication.
                YQStreamedFeatureOverlayTarget landmarkOverlayTarget = landmark.GetComponent<YQStreamedFeatureOverlayTarget>() ?? landmark.AddComponent<YQStreamedFeatureOverlayTarget>();
                landmarkOverlayTarget.featureId = landmarkId;
                landmarkOverlayTarget.objectId = landmark.name;
                // note: The deterministic landmark branch uses the same authoritative resource interaction as reviewed POI sites.
                YQGeneratedLandmarkResource resource = landmark.GetComponent<YQGeneratedLandmarkResource>() ??
                    landmark.AddComponent<YQGeneratedLandmarkResource>();
                resource.Configure(landmarkId, plan.worldSeed);
                landmark.transform.position = new Vector3(x, YQContinuousWorldFeatureAuthority.SampleTerrainHeight(terrain, x, z), z);
                PrepareWildernessLandmark(landmark);
                registry.ApplyMaterialOverrides(reference.assetPath, landmark);
                yield return YQRuntimeUrpMaterialRepair.RepairMaterialHierarchyRoutine(landmark, null);
                if (!YQGeneratedWorldTerrain.TryPlaceGroundedObject(landmark, terrain, YQGeneratedWorldPlacementCategory.Structure, 0.04f, out _))
                {
                    UnityEngine.Object.Destroy(landmark);
                }
                else
                {
                    built++;
                    yield return null;
                }
            }
        }

        if (built > 0)
            Debug.Log("[YQContinuousWorldFeatures] CELL " + chunk.chunkX + "," + chunk.chunkZ + " materialized=" + built);
        if (hasAcceptedRiver || hasAcceptedAreaWater || hasAcceptedRoad || siteObjects > 0 || landmarkPlanned)
        {
            // note: Positive structural telemetry distinguishes an intentionally quiet cell from a cell whose accepted identities were not published.
            Debug.Log("[YQContinuousWorldFeatures] ACCEPTED FEATURES " + chunk.chunkX + "," + chunk.chunkZ +
                " river=" + riverObjects + " areaWater=" + areaWaterObjects + " road=" + roadObjects +
                " crossings=" + crossingObjects + " sites=" + siteObjects + " landmark=" + landmarkPlanned);
        }
        completed?.Invoke(built);
    }

    public static bool HasAcceptedStructuralDemand(
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticChunkRecord chunk,
        Vector2Int coordinate,
        float cellSize,
        Terrain terrain,
        YQPreparedSpatialMaterializationV2 preparedOverride = null)
    {
        if (plan == null || chunk == null)
            return false;
        YQPreparedSpatialMaterializationV2 prepared = preparedOverride;
        if (prepared == null && !YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan, out prepared, out _))
            return false;

        // note: A route or water feature is mandatory physical demand even when point extraction later fails to produce a drawable ribbon.
        if (HasAcceptedRoadDemand(prepared, coordinate, cellSize) ||
            HasAcceptedWaterDemand(prepared, coordinate, cellSize))
            return true;

        // note: Only the canonical owner publishes site-wide geometry; member cells remain semantic footprint records.
        if (HasAcceptedSiteOwnerDemand(prepared, chunk, coordinate, cellSize))
            return true;

        // note: Crossings are independently required physical content even when route ribbon extraction has not yet produced a positive object count.
        return HasAcceptedCrossingDemand(prepared, coordinate, cellSize);
    }

    public static bool HasAcceptedStructuralIdentityWithoutProjection(
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticChunkRecord chunk,
        Vector2Int coordinate,
        float cellSize,
        out string featureId,
        YQPreparedSpatialMaterializationV2 preparedOverride = null)
    {
        featureId = string.Empty;
        if (plan == null || chunk == null || chunk.featureIds == null)
            return false;
        YQPreparedSpatialMaterializationV2 prepared = preparedOverride;
        if (prepared == null && !YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan, out prepared, out _))
            return false;

        // note: A persisted route identity must resolve to the same accepted V2 footprint used by materialization; otherwise tree/rock scatter would hide a broken route projection.
        for (int featureIndex = 0; featureIndex < chunk.featureIds.Count; featureIndex++)
        {
            string candidate = chunk.featureIds[featureIndex];
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            bool matchedRouteIdentity = false;
            bool matchedProjectedRoute = false;
            for (int routeIndex = 0; routeIndex < prepared.RouteCount; routeIndex++)
            {
                YQSpatialMaterializationRouteV2 route = prepared.GetRoute(routeIndex);
                if (!string.Equals(candidate, route.routeId, StringComparison.Ordinal) &&
                    !string.Equals(candidate, route.sourceSemanticRouteId, StringComparison.Ordinal))
                    continue;
                matchedRouteIdentity = true;
                List<int> routeIndices = new List<int>();
                YQContinuousWorldFeatureAuthority.GetAcceptedRouteIndicesForCell(prepared, coordinate, cellSize, routeIndices);
                if (routeIndices.Contains(routeIndex))
                    matchedProjectedRoute = true;
            }
            // note: A shared semantic route identity may name several split physical spans; reject only when none of its matching spans owns this cell.
            if (matchedRouteIdentity && !matchedProjectedRoute)
            {
                featureId = candidate;
                return true;
            }
            for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
            {
                YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
                if (!string.Equals(candidate, water.hydrologyId, StringComparison.Ordinal))
                    continue;
                List<int> waterIndices = new List<int>();
                YQContinuousWorldFeatureAuthority.GetAcceptedWaterIndicesForCell(prepared, coordinate, cellSize, waterIndices);
                if (!waterIndices.Contains(waterIndex))
                {
                    featureId = candidate;
                    return true;
                }
            }
        }
        return false;
    }

    private static bool HasAcceptedRoadDemand(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize)
    {
        if (prepared == null)
            return false;
        List<int> routeIndices = new List<int>();
        YQContinuousWorldFeatureAuthority.GetAcceptedRouteIndicesForCell(prepared, coordinate, cellSize, routeIndices);
        if (routeIndices.Count > 0)
            return true;
        List<YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation> continuations =
            new List<YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation>();
        YQContinuousWorldFeatureAuthority.GetAcceptedRouteContinuations(prepared, coordinate, cellSize, 6f, continuations);
        return continuations.Count > 0;
    }

    private static bool HasAcceptedWaterDemand(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize)
    {
        if (prepared == null)
            return false;
        List<int> waterIndices = new List<int>();
        YQContinuousWorldFeatureAuthority.GetAcceptedWaterIndicesForCell(prepared, coordinate, cellSize, waterIndices);
        if (waterIndices.Count > 0)
            return true;
        List<YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation> continuations =
            new List<YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation>();
        YQContinuousWorldFeatureAuthority.GetAcceptedWaterContinuations(prepared, coordinate, cellSize, continuations);
        return continuations.Count > 0;
    }

    private static bool HasAcceptedCrossingDemand(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize)
    {
        if (prepared == null || prepared.CrossingCount <= 0)
            return false;

        float safeCellSize = Mathf.Max(1f, cellSize);
        for (int index = 0; index < prepared.CrossingCount; index++)
        {
            YQSpatialMaterializationCrossingV2 crossing = prepared.GetCrossing(index);
            if (crossing.kind != YQRouteCrossingKindV2.Bridge &&
                crossing.kind != YQRouteCrossingKindV2.Causeway)
                continue;
            Vector2Int owner = new Vector2Int(
                Mathf.FloorToInt((crossing.x - WorldGridOrigin) / safeCellSize),
                Mathf.FloorToInt((crossing.z - WorldGridOrigin) / safeCellSize));
            if (owner == coordinate)
                return true;
        }
        return false;
    }

    private static bool HasAcceptedSiteOwnerDemand(
        YQPreparedSpatialMaterializationV2 prepared,
        GeneratedSemanticChunkRecord chunk,
        Vector2Int coordinate,
        float cellSize)
    {
        if (prepared == null || chunk == null)
            return false;
        for (int siteIndex = 0; siteIndex < prepared.SiteCount; siteIndex++)
        {
            YQSpatialMaterializationSiteV2 site = prepared.GetSite(siteIndex);
            if (site.kind == YQSiteKindV2.Origin || string.IsNullOrWhiteSpace(site.siteId))
                continue;
            // note: The accepted V2 projection is the owner-cell authority; transient featureIds remain diagnostic metadata and cannot suppress a valid site publication.
            Vector2Int owner = new Vector2Int(
                Mathf.FloorToInt((site.x - WorldGridOrigin) / Mathf.Max(1f, cellSize)),
                Mathf.FloorToInt((site.z - WorldGridOrigin) / Mathf.Max(1f, cellSize)));
            if (owner == coordinate)
                return true;
        }
        return false;
    }

    private static bool HasExistingAcceptedSiteOwner(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize)
    {
        if (prepared == null)
            return false;
        for (int siteIndex = 0; siteIndex < prepared.SiteCount; siteIndex++)
        {
            YQSpatialMaterializationSiteV2 site = prepared.GetSite(siteIndex);
            if (site.kind == YQSiteKindV2.Origin || string.IsNullOrWhiteSpace(site.siteId))
                continue;
            Vector2Int owner = new Vector2Int(
                Mathf.FloorToInt((site.x - WorldGridOrigin) / Mathf.Max(1f, cellSize)),
                Mathf.FloorToInt((site.z - WorldGridOrigin) / Mathf.Max(1f, cellSize)));
            if (owner == coordinate &&
                (YQCompiledWorldSiteInstance.IsSiteLoaded(site.sourceSemanticId) ||
                 YQCompiledWorldSiteInstance.IsSiteLoaded(site.siteId)))
                return true;
        }
        return false;
    }

    private static bool HasMissingRequiredAcceptedSiteOwner(
        YQPreparedSpatialMaterializationV2 prepared,
        GeneratedSemanticChunkRecord chunk,
        Vector2Int coordinate,
        float cellSize,
        List<string> materializedSiteIds)
    {
        if (prepared == null || chunk == null)
            return false;
        for (int siteIndex = 0; siteIndex < prepared.SiteCount; siteIndex++)
        {
            YQSpatialMaterializationSiteV2 site = prepared.GetSite(siteIndex);
            if (site.kind == YQSiteKindV2.Origin ||
                string.IsNullOrWhiteSpace(site.siteId) ||
                !RequiresReviewedPhysicalSite(site))
                continue;
            // note: Check the canonical owner coordinate directly so a freshly admitted chunk cannot publish a required site as empty while its derived index is still catching up.
            Vector2Int owner = new Vector2Int(
                Mathf.FloorToInt((site.x - WorldGridOrigin) / Mathf.Max(1f, cellSize)),
                Mathf.FloorToInt((site.z - WorldGridOrigin) / Mathf.Max(1f, cellSize)));
            bool alreadyLoaded =
                YQCompiledWorldSiteInstance.IsSiteLoaded(site.sourceSemanticId) ||
                YQCompiledWorldSiteInstance.IsSiteLoaded(site.siteId);
            if (owner == coordinate &&
                !alreadyLoaded &&
                (materializedSiteIds == null || !materializedSiteIds.Contains(site.siteId)))
                return true;
        }
        return false;
    }

    private static IEnumerator BuildAcceptedSiteRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticChunkRecord chunk,
        GeneratedRegionAssetPaletteRecord fallbackPalette,
        YQRuntimeWorldAssetRegistry registry,
        Vector2Int coordinate,
        float cellSize,
        Action<int, List<string>> completed,
        YQPreparedSpatialMaterializationV2 preparedOverride = null)
    {
        int built = 0;
        List<string> materializedSiteIds = new List<string>();
        YQPreparedSpatialMaterializationV2 prepared = preparedOverride;
        if (parent == null || terrain == null || terrain.terrainData == null || plan == null ||
            chunk == null || registry == null ||
            (prepared == null && !YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out prepared,
                out _)))
        {
            completed?.Invoke(0, materializedSiteIds);
            yield break;
        }

        // note: Repair missing persisted palette bindings before site publication so an accepted site never loses its approved prefab family merely because the runtime plan was loaded without its derived palette cache.
        YQWorldAssetCatalog.EnsureAssetPalettes(plan);

        // note: Accepted site projection and owner indexing are a separate scheduler slice so a large site catalog cannot hitch the first streamed cell publication.
        yield return null;
        for (int siteIndex = 0; siteIndex < prepared.SiteCount; siteIndex++)
        {
            YQSpatialMaterializationSiteV2 site = prepared.GetSite(siteIndex);
            if (site.kind == YQSiteKindV2.Origin || string.IsNullOrWhiteSpace(site.siteId))
                continue;

            // note: One accepted site reservation has one canonical owner cell; member cells retain the semantic footprint but never clone the site-wide structure.
            Vector2Int owner = new Vector2Int(
                Mathf.FloorToInt((site.x - WorldGridOrigin) / Mathf.Max(1f, cellSize)),
                Mathf.FloorToInt((site.z - WorldGridOrigin) / Mathf.Max(1f, cellSize)));
            // note: The accepted V2 site coordinate selects the one canonical owner; member-cell metadata never clones the site-wide structure.
            if (owner != coordinate)
                continue;

            string existingSiteLocationId = string.Empty;
            if (YQCompiledWorldSiteInstance.HasSite(site.sourceSemanticId))
                existingSiteLocationId = site.sourceSemanticId;
            else if (YQCompiledWorldSiteInstance.HasSite(site.siteId))
                existingSiteLocationId = site.siteId;

            if (!string.IsNullOrWhiteSpace(existingSiteLocationId))
            {
                // note: A prepared site root is only an owner registration; load it through its existing bounded stream instead of treating registration as physical publication.
                bool existingSiteLoaded = false;
                yield return YQCompiledWorldSiteInstance.EnsureSiteLoadedRoutine(
                    existingSiteLocationId,
                    ready => existingSiteLoaded = ready);
                if (existingSiteLoaded)
                {
                    built++;
                    materializedSiteIds.Add(site.siteId);
                }
                else
                {
                    Debug.LogError(
                        "[YQContinuousWorldFeatures] EXISTING SITE FAILED TO LOAD " +
                        site.siteId + " location=" + existingSiteLocationId +
                        "; trying the approved physical fallback.");
                }
                // note: A registered provider can be present but rejected by walkable-collision validation; only a loaded provider owns publication, so rejected providers fall through to the approved asset path below.
                if (existingSiteLoaded)
                    continue;
            }

            // note: Keep site-owner selection independent from reviewed prefab resolution and grounding work.
            yield return null;
            GeneratedRegionAssetPaletteRecord sitePalette = FindSitePalette(plan, site.parentRegionId) ?? fallbackPalette;
            sitePalette = FindPaletteWithApprovedSiteReference(
                plan,
                site,
                sitePalette,
                plan.worldSeed + "|site-palette|" + site.siteId);
            int reviewedSiteCount = 0;
            yield return MaterializeAcceptedReviewedSiteRoutine(
                parent,
                terrain,
                plan,
                site,
                sitePalette,
                registry,
                reviewedCount => reviewedSiteCount = reviewedCount);
            if (reviewedSiteCount > 0)
            {
                built += reviewedSiteCount;
                materializedSiteIds.Add(site.siteId);
                continue;
            }

            int approvedFallbackCount = 0;
            if (!IsAcceptedCaveSite(site))
            {
                // note: Every accepted non-cave site gets a second physical realization path; semantic site demand may not collapse to empty wilderness when its reviewed provider is unavailable.
                yield return MaterializeAcceptedApprovedAssetSiteFallbackRoutine(
                    parent,
                    terrain,
                    plan,
                    site,
                    sitePalette,
                    registry,
                    count => approvedFallbackCount = count);
            }
            if (approvedFallbackCount > 0)
            {
                built += approvedFallbackCount;
                materializedSiteIds.Add(site.siteId);
                continue;
            }

            // note: A required beta category remains a hard publication failure when neither a reviewed provider nor an approved palette asset can realize it.
            if (RequiresReviewedPhysicalSite(site))
            {
                if (site.kind != YQSiteKindV2.NaturalFeature)
                {
                    // note: Make the last-resort recovery kind-authoritative so stale beta tags cannot bypass an approved physical landmark, settlement, or hostile fallback after provider validation rejects the reviewed site.
                    int recoveryFallbackCount = 0;
                    yield return MaterializeAcceptedApprovedAssetSiteFallbackRoutine(
                        parent,
                        terrain,
                        plan,
                        site,
                        sitePalette,
                        registry,
                        count => recoveryFallbackCount = count);
                    if (recoveryFallbackCount > 0)
                    {
                        built += recoveryFallbackCount;
                        materializedSiteIds.Add(site.siteId);
                        continue;
                    }
                }
                Debug.LogError(
                    "[YQContinuousWorldFeatures] REQUIRED SITE REVIEW MISSING " +
                        site.siteId + " kind=" + site.kind);
                continue;
            }

            GeneratedAssetReferenceRecord reference = PickSiteReference(sitePalette, site, plan.worldSeed + "|site|" + site.siteId);
            if (reference == null || string.IsNullOrWhiteSpace(reference.assetPath))
            {
                Debug.LogError(
                    "[YQContinuousWorldFeatures] ACCEPTED SITE PHYSICAL MATERIALIZATION MISSING " +
                    site.siteId + " kind=" + site.kind);
                continue;
            }
            GameObject prefab = registry.ResolvePrefab(reference.assetPath);
            if (prefab == null)
            {
                Debug.LogError(
                    "[YQContinuousWorldFeatures] ACCEPTED SITE ASSET UNRESOLVED " +
                    site.siteId + " asset=" + reference.assetPath);
                continue;
            }

            int blockCount = site.kind == YQSiteKindV2.Settlement ? 3 : 1;
            int builtBeforeSite = built;
            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(prefab, parent);
                operation.priority = -1;
                yield return operation;
                GameObject instance = operation.Result != null && operation.Result.Length > 0
                    ? operation.Result[0]
                    : null;
                if (instance == null)
                    continue;

                string instanceSeed = plan.worldSeed + "|site|" + site.siteId + "|block|" + blockIndex;
                float angle = YQContinuousWorldFeatureAuthority.Hash01(instanceSeed + "|angle") * Mathf.PI * 2f;
                float distance = blockIndex == 0
                    ? 0f
                    : Mathf.Min(Mathf.Max(8f, site.reservedRadius * 0.32f), cellSize * 0.30f);
                Vector3 position = new Vector3(
                    site.x + Mathf.Cos(angle) * distance,
                    YQGeneratedWorldTerrain.SampleWorldHeight(terrain, new Vector3(site.x, 0f, site.z)),
                    site.z + Mathf.Sin(angle) * distance);
                instance.name = "ContinuousSite_" + SafeSiteName(site.siteId) + "_" + blockIndex;
                instance.transform.SetPositionAndRotation(
                    position,
                    Quaternion.Euler(0f, site.headingDegrees + YQContinuousWorldFeatureAuthority.Hash01(instanceSeed + "|yaw") * 20f - 10f, 0f));
                float scale = Mathf.Lerp(
                    Mathf.Max(0.05f, reference.scaleMin),
                    Mathf.Max(reference.scaleMin, reference.scaleMax),
                    YQContinuousWorldFeatureAuthority.Hash01(instanceSeed + "|scale"));
                instance.transform.localScale *= scale;
                registry.ApplyMaterialOverrides(reference.assetPath, instance, false);
                PrepareWildernessLandmark(instance);
                yield return YQRuntimeUrpMaterialRepair.RepairMaterialHierarchyRoutine(instance, null);
                if (!YQGeneratedWorldTerrain.TryPlaceGroundedObject(
                        instance,
                        terrain,
                        YQGeneratedWorldPlacementCategory.Structure,
                        0.04f,
                        out _))
                {
                    instance.SetActive(false);
                    UnityEngine.Object.Destroy(instance);
                    continue;
                }

                // note: Bind the accepted site ID only after grounding succeeds so overlays and reloads target the physical owner, not a failed prefab attempt.
                YQStreamedFeatureOverlayTarget overlayTarget = instance.GetComponent<YQStreamedFeatureOverlayTarget>() ??
                    instance.AddComponent<YQStreamedFeatureOverlayTarget>();
                overlayTarget.featureId = site.siteId;
                overlayTarget.objectId = instance.name;
                built++;
                yield return null;
            }
            if (built > builtBeforeSite && !materializedSiteIds.Contains(site.siteId))
                materializedSiteIds.Add(site.siteId);
        }

        if (built > 0)
            Debug.Log("[YQContinuousWorldFeatures] SITES " + coordinate + " materialized=" + built);
        completed?.Invoke(built, materializedSiteIds);
    }

    private static IEnumerator MaterializeAcceptedReviewedSiteRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQSpatialMaterializationSiteV2 site,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        Action<int> completed)
    {
        completed?.Invoke(0);
        if (parent == null || terrain == null || plan == null || string.IsNullOrWhiteSpace(site.siteId) || registry == null)
            yield break;

        YQRuntimeWorldSiteRecord reviewedSite = null;
        string locationId = site.sourceSemanticId;
        string[] semanticTags = new[] { "circulation", "encounter", "reward" };
        GeneratedSettlementRecord settlement = FindSettlement(plan, site.sourceSemanticId);
        GeneratedEncampmentRecord encampment = FindEncampment(plan, site.sourceSemanticId);
        GeneratedRegionRecord region = FindPlanRegion(plan, site.parentRegionId);
        bool bindingChanged;

        if (IsAcceptedCaveSite(site))
        {
            // note: The beta cave is a real authored module chain derived from the accepted cave anchor; it is not a marker or a readiness-only placeholder.
            int caveObjects = 0;
            yield return MaterializeBetaCaveRoutine(
                parent,
                terrain,
                site,
                plan,
                registry,
                count => caveObjects = count);
            if (caveObjects > 0)
            {
                completed?.Invoke(caveObjects);
                yield break;
            }
        }

        if (site.kind == YQSiteKindV2.Settlement && settlement != null &&
            YQCompiledWorldSiteBindingService.TryResolveSettlementSite(
                plan,
                settlement,
                region,
                palette,
                out reviewedSite,
                out bindingChanged,
                null,
                false))
        {
            locationId = settlement.settlementId;
            semanticTags = YQCompiledWorldSiteBindingService.BuildSettlementSemanticSliceTags(settlement);
        }
        else if (site.kind == YQSiteKindV2.HostileSite && encampment != null &&
                 YQCompiledWorldSiteBindingService.TryResolveEncampmentSite(
                     plan,
                     encampment,
                     region,
                     palette,
                     out reviewedSite,
                     out bindingChanged,
                     null,
                     false))
        {
            locationId = encampment.encampmentId;
            semanticTags = YQCompiledWorldSiteBindingService.BuildEncampmentSemanticSliceTags(encampment);
        }
        else if (IsAcceptedCaveSite(site))
        {
            reviewedSite = FindReviewedSiteForFunctions(
                YQAuthoredSiteKind.Dungeon,
                new[]
                {
                    YQAssetFunctionV2.Transition,
                    YQAssetFunctionV2.Encounter,
                    YQAssetFunctionV2.Reward
                },
                site.siteId);
        }

        if (reviewedSite == null && site.kind == YQSiteKindV2.Settlement)
        {
            // note: The deterministic beta settlement has no narrative record by design; its accepted V2 functions still select a reviewed service-capable site.
            reviewedSite = FindReviewedSiteForFunctions(
                YQAuthoredSiteKind.Settlement,
                new[]
                {
                    YQAssetFunctionV2.Habitation,
                    YQAssetFunctionV2.Circulation,
                    YQAssetFunctionV2.Service,
                    YQAssetFunctionV2.Commerce
                },
                site.siteId);
            semanticTags = new[] { "poi", "civic", "residential", "service", "circulation" };
        }
        else if (reviewedSite == null && site.kind == YQSiteKindV2.HostileSite)
        {
            // note: The beta hostile fixture remains data-driven through the reviewed camp/dungeon catalog rather than a generic marker prefab.
            reviewedSite = FindReviewedSiteForFunctions(
                YQAuthoredSiteKind.Camp,
                new[]
                {
                    YQAssetFunctionV2.Encounter,
                    YQAssetFunctionV2.Reward,
                    YQAssetFunctionV2.Security
                },
                site.siteId);
            semanticTags = new[] { "poi", "perimeter", "circulation", "encounter", "reward" };
        }
        else if (reviewedSite == null && site.kind == YQSiteKindV2.PointOfInterest)
        {
            // note: Landmark/resource fixtures use a reviewed landmark composition with an actual approachable footprint.
            reviewedSite = FindReviewedSiteForFunctions(
                YQAuthoredSiteKind.Landmark,
                new[]
                {
                    YQAssetFunctionV2.CulturalFocus,
                    YQAssetFunctionV2.Transition,
                    YQAssetFunctionV2.Reward
                },
                site.siteId);
            semanticTags = new[] { "poi", "circulation", "reward" };

            if (reviewedSite == null)
            {
                // note: Older catalogs may have no populated Landmark function metadata; reuse an approved service-capable site as a real POI instead of silently publishing wilderness.
                reviewedSite = FindReviewedSiteForFunctions(
                    YQAuthoredSiteKind.Settlement,
                    new[]
                    {
                        YQAssetFunctionV2.Habitation,
                        YQAssetFunctionV2.Circulation,
                        YQAssetFunctionV2.Service
                    },
                    site.siteId + "|poi_fallback");
                semanticTags = new[] { "poi", "circulation", "service", "reward" };
            }
        }

        if (reviewedSite == null || string.IsNullOrWhiteSpace(reviewedSite.runtimeManifestResourceKey))
            yield break;

        GameObject root = new GameObject("ContinuousReviewedSite_" + SafeSiteName(site.siteId));
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(
            new Vector3(
                site.x,
                YQGeneratedWorldTerrain.SampleWorldHeight(terrain, new Vector3(site.x, 0f, site.z)),
                site.z),
            Quaternion.Euler(0f, site.headingDegrees, 0f));
        root.SetActive(false);

        bool materialized = false;
        yield return YQCompiledWorldSiteInstance.MaterializeSemanticSliceNowRoutine(
            root.transform,
            locationId,
            reviewedSite,
            semanticTags,
            ResolveAcceptedSiteSelectionSeed(plan, site, settlement, encampment),
            success => materialized = success);
        if (!materialized)
        {
            UnityEngine.Object.Destroy(root);
            Debug.LogWarning(
                "[YQContinuousWorldFeatures] Accepted reviewed site was rejected: " + site.siteId);
            yield break;
        }

        // note: The site loader stages CompiledSiteContent under this root while the enclosing chunk is hidden; publish the site root's own activeSelf state before the chunk lifecycle pass exposes it.
        root.SetActive(true);

        // note: Overlay identity is the accepted V2 site ID even when the reviewed provider is keyed by its source semantic record.
        YQStreamedFeatureOverlayTarget overlayTarget = root.GetComponent<YQStreamedFeatureOverlayTarget>() ??
            root.AddComponent<YQStreamedFeatureOverlayTarget>();
        overlayTarget.featureId = site.siteId;
        overlayTarget.objectId = root.name;
        if (site.kind == YQSiteKindV2.Settlement &&
            (HasSiteTag(site, "beta:settlement") || HasSiteTag(site, "beta_fixture_settlement")))
        {
            // note: Bind the beta settlement's supported player service to the accepted site identity so purchases survive site streaming and save reloads.
            YQGeneratedSettlementService service = root.GetComponent<YQGeneratedSettlementService>() ??
                root.AddComponent<YQGeneratedSettlementService>();
            service.Configure(site.siteId, plan.worldSeed);
        }
        if (site.kind == YQSiteKindV2.HostileSite &&
            (HasSiteTag(site, "beta:hostile") || HasSiteTag(site, "beta_fixture_hostile")))
        {
            // note: V2 beta hostile fixtures do not have a narrative encampment record, so attach the existing proximity-gated combat owner to the accepted site root.
            YQInvestorEnemySpawner encounter = root.GetComponent<YQInvestorEnemySpawner>() ??
                root.AddComponent<YQInvestorEnemySpawner>();
            encounter.enemyCount = 2;
            encounter.factionId = "beta_hostile_site";
            encounter.semanticRegionId = site.siteId;
            encounter.enemyDisplayName = "Beta Ruin Marauder";
            encounter.deterministicSeed = plan.worldSeed + "|" + site.siteId + "|encounter";
            encounter.completedCounter = "kill:" + site.siteId;
            encounter.completedCounterMinimum = 1f;
            encounter.requireOriginComplete = true;
            encounter.requirePlayerNear = true;
            encounter.despawnWhenPlayerFar = true;
            encounter.playerActivationDistance = 34f;
            encounter.playerFarDespawnDistance = 58f;
            encounter.spawnRadius = 5f;
            encounter.PrimeSpawnGate();
            Debug.Log("[YQContinuousWorldFeatures] BETA HOSTILE ENCOUNTER " + site.siteId + " bound to accepted site root.");
        }
        if (site.kind == YQSiteKindV2.PointOfInterest)
        {
            // note: Landmark/resource sites expose one stable harvest owner tied to the accepted feature ID instead of a transient scene object.
            YQGeneratedLandmarkResource resource = root.GetComponent<YQGeneratedLandmarkResource>() ??
                root.AddComponent<YQGeneratedLandmarkResource>();
            resource.Configure(site.siteId, plan.worldSeed);
        }

        int materializedObjects = 1;
        bool connectorFailure = false;
        if (site.MemberFootprint.Count > 0)
        {
            // note: Member sectors share the reviewed root's identity; only their physical access ribbons are added, avoiding duplicate site-wide providers.
            Material accessMaterial = FindMaterial(registry, "ground", "mud", "path") ?? BuildFallbackRoadMaterial();
            RegisterCellResource(root.transform, accessMaterial);
            for (int memberIndex = 0; memberIndex < site.MemberFootprint.Count; memberIndex++)
            {
                YQSiteMemberFootprintV2 member = site.MemberFootprint[memberIndex];
                if (member == null)
                    continue;
                int connectorObjects = BuildSampledSiteConnector(
                    root.transform,
                    "MultiCellSiteAccess_" + SafeSiteName(member.memberId),
                    terrain,
                    new Vector3(site.x, 0f, site.z),
                    new Vector3(member.x, 0f, member.z),
                    4.5f,
                    accessMaterial);
                if (connectorObjects <= 0)
                {
                    connectorFailure = true;
                    Debug.LogError(
                        "[YQContinuousWorldFeatures] REQUIRED SITE CONNECTOR MISSING " +
                        site.siteId + " member=" + member.memberId);
                    break;
                }
                materializedObjects += connectorObjects;
            }
        }
        if (connectorFailure)
        {
            // note: Do not publish a reviewed site root whose mandatory member sector cannot be reached by physical geometry.
            UnityEngine.Object.Destroy(root);
            completed?.Invoke(0);
            yield break;
        }
        completed?.Invoke(materializedObjects);
    }

    private static string ResolveAcceptedSiteSelectionSeed(
        GeneratedWorldPlanRecord plan,
        YQSpatialMaterializationSiteV2 site,
        GeneratedSettlementRecord settlement,
        GeneratedEncampmentRecord encampment)
    {
        // note: Reuse the persisted procedural layout identity when available so streamed settlement sectors consume the exact accepted cell selection and street plan.
        if (settlement?.proceduralLayout != null &&
            !string.IsNullOrWhiteSpace(settlement.proceduralLayout.seed))
            return settlement.proceduralLayout.seed;
        // note: Fresh generated settlements commit their engine-owned seed before streaming; legacy records remain on their original deterministic selection.
        if (!string.IsNullOrWhiteSpace(settlement?.deterministicSeed))
            return settlement.deterministicSeed;
        if (!string.IsNullOrWhiteSpace(encampment?.deterministicSeed))
            return encampment.deterministicSeed;
        // note: Keep non-settlement reviewed sites keyed by their accepted feature identity without inventing a procedural layout.
        return (plan != null ? plan.worldSeed : string.Empty) + "|accepted-site-slice|" + site.siteId;
    }

    private static IEnumerator MaterializeAcceptedApprovedAssetSiteFallbackRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQSpatialMaterializationSiteV2 site,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        Action<int> completed)
    {
        completed?.Invoke(0);
        if (parent == null || terrain == null || plan == null || registry == null)
            yield break;

        // note: A rejected reviewed provider must not be selected again from the palette; beta-required sites use their distinct approved fallback family first.
        GeneratedAssetReferenceRecord reference = RequiresReviewedPhysicalSite(site)
            ? BuildApprovedSiteFallbackReference(site, registry)
            : PickSiteReference(
                palette,
                site,
                plan.worldSeed + "|approved-site-fallback|" + site.siteId);
        GameObject prefab = reference != null && !string.IsNullOrWhiteSpace(reference.assetPath)
            ? registry.ResolvePrefab(reference.assetPath)
            : null;
        if (prefab == null)
        {
            // note: Resolve a known approved catalog member when the persisted palette reference is stale or rejected by the runtime registry.
            reference = BuildApprovedSiteFallbackReference(site, registry);
            prefab = reference != null && !string.IsNullOrWhiteSpace(reference.assetPath)
                ? registry.ResolvePrefab(reference.assetPath)
                : null;
        }
        if (prefab == null)
        {
            Debug.LogWarning(
                "[YQContinuousWorldFeatures] Approved site fallback asset unavailable: " + site.siteId);
            yield break;
        }

        // note: Settlement fallback creates a small deterministic physical cluster; other beta sites use one anchored structure with their typed interaction owner.
        int blockCount = site.kind == YQSiteKindV2.Settlement ? 3 : 1;
        int built = 0;
        List<GameObject> builtInstances = new List<GameObject>();
        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(prefab, parent);
            operation.priority = -1;
            yield return operation;
            GameObject instance = operation.Result != null && operation.Result.Length > 0
                ? operation.Result[0]
                : null;
            if (instance == null)
                continue;

            string instanceSeed = plan.worldSeed + "|approved-site-fallback|" + site.siteId + "|block|" + blockIndex;
            float angle = blockIndex == 0
                ? 0f
                : YQContinuousWorldFeatureAuthority.Hash01(instanceSeed + "|angle") * Mathf.PI * 2f;
            float distance = blockIndex == 0
                ? 0f
                : Mathf.Min(Mathf.Max(8f, site.reservedRadius * 0.32f), 128f * 0.30f);
            Vector3 position = new Vector3(
                site.x + Mathf.Cos(angle) * distance,
                YQGeneratedWorldTerrain.SampleWorldHeight(terrain, new Vector3(site.x, 0f, site.z)),
                site.z + Mathf.Sin(angle) * distance);
            instance.name = "ApprovedSiteFallback_" + SafeSiteName(site.siteId) + "_" + blockIndex;
            instance.transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, site.headingDegrees + YQContinuousWorldFeatureAuthority.Hash01(instanceSeed + "|yaw") * 20f - 10f, 0f));
            if (reference != null)
            {
                float scale = Mathf.Lerp(
                    Mathf.Max(0.05f, reference.scaleMin),
                    Mathf.Max(reference.scaleMin, reference.scaleMax),
                    YQContinuousWorldFeatureAuthority.Hash01(instanceSeed + "|scale"));
                instance.transform.localScale *= scale;
                registry.ApplyMaterialOverrides(reference.assetPath, instance, false);
            }
            PrepareWildernessLandmark(instance);
            yield return YQRuntimeUrpMaterialRepair.RepairMaterialHierarchyRoutine(instance, null);
            // note: Search deterministic nearby ground before rejecting an approved fallback; a large prefab can straddle a steep edge even when the site cell itself is valid.
            if (!TryPlaceApprovedFallbackGrounded(instance, terrain, site, instanceSeed))
            {
                UnityEngine.Object.Destroy(instance);
                continue;
            }

            // note: The accepted V2 site ID is the durable overlay key for this approved fallback instance.
            YQStreamedFeatureOverlayTarget overlayTarget = instance.GetComponent<YQStreamedFeatureOverlayTarget>() ??
                instance.AddComponent<YQStreamedFeatureOverlayTarget>();
            overlayTarget.featureId = site.siteId;
            overlayTarget.objectId = instance.name;
            if (blockIndex == 0 && site.kind == YQSiteKindV2.Settlement)
            {
                YQGeneratedSettlementService service = instance.GetComponent<YQGeneratedSettlementService>() ??
                    instance.AddComponent<YQGeneratedSettlementService>();
                service.Configure(site.siteId, plan.worldSeed);
            }
            if (blockIndex == 0 && site.kind == YQSiteKindV2.HostileSite)
            {
                YQInvestorEnemySpawner encounter = instance.GetComponent<YQInvestorEnemySpawner>() ??
                    instance.AddComponent<YQInvestorEnemySpawner>();
                encounter.enemyCount = 2;
                encounter.factionId = "beta_hostile_site";
                encounter.semanticRegionId = site.siteId;
                encounter.enemyDisplayName = "Beta Ruin Marauder";
                encounter.deterministicSeed = plan.worldSeed + "|" + site.siteId + "|encounter";
                encounter.completedCounter = "kill:" + site.siteId;
                encounter.completedCounterMinimum = 1f;
                encounter.requireOriginComplete = true;
                encounter.requirePlayerNear = true;
                encounter.despawnWhenPlayerFar = true;
                encounter.playerActivationDistance = 34f;
                encounter.playerFarDespawnDistance = 58f;
                encounter.spawnRadius = 5f;
                encounter.PrimeSpawnGate();
            }
            if (blockIndex == 0 && site.kind == YQSiteKindV2.PointOfInterest)
            {
                YQGeneratedLandmarkResource resource = instance.GetComponent<YQGeneratedLandmarkResource>() ??
                    instance.AddComponent<YQGeneratedLandmarkResource>();
                resource.Configure(site.siteId, plan.worldSeed);
            }
            built++;
            builtInstances.Add(instance);
            yield return null;
        }

        if (built > 0 && site.MemberFootprint.Count > 0)
        {
            // note: Fallback sites retain the same mandatory member-sector access contract as reviewed sites instead of silently omitting their physical continuation.
            Material accessMaterial = FindMaterial(registry, "ground", "mud", "path") ?? BuildFallbackRoadMaterial();
            GameObject accessRoot = new GameObject("ApprovedSiteFallbackAccess_" + SafeSiteName(site.siteId));
            accessRoot.transform.SetParent(parent, false);
            RegisterCellResource(accessRoot.transform, accessMaterial);
            bool connectorFailure = false;
            for (int memberIndex = 0; memberIndex < site.MemberFootprint.Count; memberIndex++)
            {
                YQSiteMemberFootprintV2 member = site.MemberFootprint[memberIndex];
                if (member == null)
                    continue;
                int connectorObjects = BuildSampledSiteConnector(
                    accessRoot.transform,
                    "MultiCellSiteFallbackAccess_" + SafeSiteName(member.memberId),
                    terrain,
                    new Vector3(site.x, 0f, site.z),
                    new Vector3(member.x, 0f, member.z),
                    4.5f,
                    accessMaterial);
                if (connectorObjects <= 0)
                {
                    connectorFailure = true;
                    Debug.LogError(
                        "[YQContinuousWorldFeatures] REQUIRED FALLBACK SITE CONNECTOR MISSING " +
                        site.siteId + " member=" + member.memberId);
                    break;
                }
                built += connectorObjects;
            }
            if (connectorFailure)
            {
                // note: Remove the fallback instances with the failed access root so a partial site cannot be published as usable.
                UnityEngine.Object.Destroy(accessRoot);
                for (int instanceIndex = 0; instanceIndex < builtInstances.Count; instanceIndex++)
                    if (builtInstances[instanceIndex] != null)
                        UnityEngine.Object.Destroy(builtInstances[instanceIndex]);
                completed?.Invoke(0);
                yield break;
            }
        }

        if (built > 0)
            Debug.Log("[YQContinuousWorldFeatures] APPROVED ASSET SITE FALLBACK " + site.siteId + " objects=" + built);
        completed?.Invoke(built);
    }

    private static bool TryPlaceApprovedFallbackGrounded(
        GameObject instance,
        Terrain terrain,
        YQSpatialMaterializationSiteV2 site,
        string seed)
    {
        if (instance == null || terrain == null)
            return false;

        // note: Keep fallback placement deterministic while sampling the site anchor first and then a bounded ring of nearby valid ground.
        float[] radii = { 0f, 10f, 20f, 30f, 42f };
        float angleOffset = YQContinuousWorldFeatureAuthority.Hash01(seed + "|ground-angle") * Mathf.PI * 2f;
        for (int scalePass = 0; scalePass < 2; scalePass++)
        {
            if (scalePass == 1)
            {
                // note: A compact approved fallback preserves the landmark identity when the full source footprint cannot be supported by the authored terrain slope.
                instance.transform.localScale *= 0.4f;
            }

            for (int radiusIndex = 0; radiusIndex < radii.Length; radiusIndex++)
            {
                float radius = radii[radiusIndex];
                float angle = angleOffset + radiusIndex * 1.61803399f;
                Vector3 candidate = new Vector3(
                    site.x + Mathf.Cos(angle) * radius,
                    0f,
                    site.z + Mathf.Sin(angle) * radius);
                candidate.y = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, candidate);
                instance.transform.position = candidate;
                if (YQGeneratedWorldTerrain.TryPlaceGroundedObject(
                        instance,
                        terrain,
                        YQGeneratedWorldPlacementCategory.Structure,
                        0.04f,
                        out _))
                {
                    return true;
                }
            }
        }

        // note: An approved beta site must not strand its entire camera-visible cell when a large imported prefab cannot satisfy the strict structural footprint slope test.
        // note: Keep the deterministic terrain anchor and compact scale as the final physical contract; this preserves the accepted site identity while avoiding a failed/unloaded chunk.
        Vector3 anchor = new Vector3(
            site.x,
            YQGeneratedWorldTerrain.SampleWorldHeight(terrain, new Vector3(site.x, 0f, site.z)),
            site.z);
        instance.transform.position = anchor;
        instance.transform.localScale *= 0.25f;
        Debug.LogWarning(
            "[YQContinuousWorldFeatures] Approved fallback using terrain anchor after footprint grounding rejection site=" +
            site.siteId);
        return true;
    }

    private static IEnumerator MaterializeBetaCaveRoutine(
        Transform parent,
        Terrain terrain,
        YQSpatialMaterializationSiteV2 site,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        Action<int> completed)
    {
        completed?.Invoke(0);
        if (parent == null || terrain == null || plan == null || registry == null)
            yield break;

        GameObject straightPrefab = registry.ResolvePrefab(YQContinuousWorldFeatureAuthority.BetaCaveStraightPrefab);
        GameObject crossPrefab = registry.ResolvePrefab(YQContinuousWorldFeatureAuthority.BetaCaveCrossPrefab);
        if (straightPrefab == null || crossPrefab == null)
        {
            Debug.LogError("[YQContinuousWorldFeatures] Beta cave kit is not available in the runtime asset registry.");
            yield break;
        }

        GameObject root = new GameObject("ContinuousBetaCave_" + SafeSiteName(site.siteId));
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(
            new Vector3(
                site.x,
                YQGeneratedWorldTerrain.SampleWorldHeight(terrain, new Vector3(site.x, 0f, site.z)),
                site.z),
            Quaternion.Euler(0f, site.headingDegrees, 0f));

        YQStreamedFeatureOverlayTarget overlayTarget = root.AddComponent<YQStreamedFeatureOverlayTarget>();
        overlayTarget.featureId = site.siteId;
        overlayTarget.objectId = root.name;

        // note: Measure the first real module so the chain follows the imported snap length instead of inventing a world-unit scale.
        GameObject first = UnityEngine.Object.Instantiate(straightPrefab, root.transform);
        first.name = "BetaCave_Entrance";
        float moduleDepth = EstimateCaveModuleDepth(first);
        first.transform.localPosition = new Vector3(0f, 0f, moduleDepth * 0.5f);
        first.transform.localRotation = Quaternion.identity;
        registry.ApplyMaterialOverrides(YQContinuousWorldFeatureAuthority.BetaCaveStraightPrefab, first, false);
        yield return YQRuntimeUrpMaterialRepair.RepairMaterialHierarchyRoutine(first, null);
        int built = 1;

        // note: Both ends remain open, giving the accepted entrance an actual traversable interior and a second exit rather than a capped prop.
        const int moduleCount = 6;
        for (int index = 1; index < moduleCount; index++)
        {
            string prefabPath = index == 2 || index == 3
                ? YQContinuousWorldFeatureAuthority.BetaCaveCrossPrefab
                : YQContinuousWorldFeatureAuthority.BetaCaveStraightPrefab;
            GameObject prefab = index == 2 || index == 3 ? crossPrefab : straightPrefab;
            GameObject module = UnityEngine.Object.Instantiate(prefab, root.transform);
            module.name = "BetaCave_Module_" + index;
            module.transform.localPosition = new Vector3(0f, 0f, moduleDepth * (index + 0.5f));
            module.transform.localRotation = Quaternion.identity;
            registry.ApplyMaterialOverrides(prefabPath, module, false);
            yield return YQRuntimeUrpMaterialRepair.RepairMaterialHierarchyRoutine(module, null);
            built++;
            yield return null;
        }

        completed?.Invoke(built);
    }

    private static float EstimateCaveModuleDepth(GameObject module)
    {
        float depth = 0f;
        Renderer[] renderers = module != null
            ? module.GetComponentsInChildren<Renderer>(true)
            : Array.Empty<Renderer>();
        for (int index = 0; index < renderers.Length; index++)
            if (renderers[index] != null)
                depth = Mathf.Max(depth, renderers[index].bounds.size.z);
        return Mathf.Clamp(depth > 0.1f ? depth : 8f, 6f, 24f);
    }

    private static GeneratedSettlementRecord FindSettlement(
        GeneratedWorldPlanRecord plan,
        string sourceSemanticId)
    {
        if (plan?.settlements == null)
            return null;
        for (int index = 0; index < plan.settlements.Count; index++)
        {
            GeneratedSettlementRecord value = plan.settlements[index];
            if (value != null && (string.Equals(value.settlementId, sourceSemanticId, StringComparison.Ordinal) ||
                                  string.Equals(YQSpatialBlueprintDeterminismV2.SafeId(value.settlementId, string.Empty), sourceSemanticId, StringComparison.Ordinal)))
                return value;
        }
        return null;
    }

    private static GeneratedEncampmentRecord FindEncampment(
        GeneratedWorldPlanRecord plan,
        string sourceSemanticId)
    {
        if (plan?.encampments == null)
            return null;
        for (int index = 0; index < plan.encampments.Count; index++)
        {
            GeneratedEncampmentRecord value = plan.encampments[index];
            if (value != null && (string.Equals(value.encampmentId, sourceSemanticId, StringComparison.Ordinal) ||
                                  string.Equals(YQSpatialBlueprintDeterminismV2.SafeId(value.encampmentId, string.Empty), sourceSemanticId, StringComparison.Ordinal)))
                return value;
        }
        return null;
    }

    private static GeneratedRegionRecord FindPlanRegion(
        GeneratedWorldPlanRecord plan,
        string regionId)
    {
        if (plan?.regions == null)
            return null;
        for (int index = 0; index < plan.regions.Count; index++)
        {
            GeneratedRegionRecord region = plan.regions[index];
            if (region != null && string.Equals(region.regionId, regionId, StringComparison.OrdinalIgnoreCase))
                return region;
        }
        return null;
    }

    // note: Preflight and streamed materialization share this deterministic reviewed-catalog selector so a synthetic accepted site cannot pass one phase and fail the other.
    internal static YQRuntimeWorldSiteRecord FindReviewedSiteForFunctions(
        YQAuthoredSiteKind preferredKind,
        YQAssetFunctionV2[] requiredFunctions,
        string seed)
    {
        YQRuntimeWorldSiteCatalog catalog = Resources.Load<YQRuntimeWorldSiteCatalog>("YQRuntimeWorldSiteCatalog");
        if (catalog == null || catalog.Sites == null)
            return null;
        YQRuntimeWorldSiteRecord best = null;
        uint bestTie = uint.MaxValue;
        for (int index = 0; index < catalog.Sites.Count; index++)
        {
            YQRuntimeWorldSiteRecord candidate = catalog.Sites[index];
            if (candidate == null || !candidate.spatiallyValidated || string.IsNullOrWhiteSpace(candidate.runtimeManifestResourceKey) ||
                (candidate.siteKind != preferredKind && !(preferredKind == YQAuthoredSiteKind.Dungeon && candidate.siteKind == YQAuthoredSiteKind.Interior)))
                continue;
            bool supports = true;
            for (int functionIndex = 0; functionIndex < requiredFunctions.Length; functionIndex++)
                if (candidate.reviewedFunctionsV2 == null || !candidate.reviewedFunctionsV2.Contains(requiredFunctions[functionIndex]))
                    supports = false;
            if (!supports)
                continue;
            uint tie = (uint)(YQContinuousWorldFeatureAuthority.Hash01(
                (seed ?? string.Empty) + "|reviewed-site|" + candidate.kitId) * uint.MaxValue);
            if (best == null || tie < bestTie)
            {
                best = candidate;
                bestTie = tie;
            }
        }
        return best;
    }

    private static bool HasSiteTag(
        YQSpatialMaterializationSiteV2 site,
        string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return false;
        IReadOnlyList<string> acceptedTags = site.Tags;
        for (int index = 0; index < acceptedTags.Count; index++)
        {
            if (string.Equals(acceptedTags[index], expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        // note: Keep the stable-ID fallback for older prepared artifacts created before tags were carried into the runtime projection.
        return !string.IsNullOrWhiteSpace(site.siteId) &&
            site.siteId.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsAcceptedCaveSite(
        YQSpatialMaterializationSiteV2 site)
    {
        // note: Preserve the original waterfall-cave semantic identity because accepted saves predate the beta-prefixed fixture tag convention.
        bool acceptedWaterfallCave =
            string.Equals(site.siteId, "site:natural:waterfall_cave", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(site.sourceSemanticId, "generated:waterfall_cave", StringComparison.OrdinalIgnoreCase);
        // note: Require a natural-feature owner and a recognized stable cave identity before selecting the enterable module chain.
        if (site.kind != YQSiteKindV2.NaturalFeature)
            return false;
        return acceptedWaterfallCave ||
               HasSiteTag(site, "beta:cave") ||
               HasSiteTag(site, "beta_fixture_cave") ||
               (!string.IsNullOrWhiteSpace(site.siteId) &&
                site.siteId.IndexOf(":cave:", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool RequiresReviewedPhysicalSite(
        YQSpatialMaterializationSiteV2 site)
    {
        // note: These identifiers are the accepted beta itinerary contract, so missing reviewed functionality must remain a visible materialization failure.
        // note: Stable site IDs are authoritative for older prepared artifacts whose tag list may omit the beta category.
        string siteId = site.siteId ?? string.Empty;
        return IsAcceptedCaveSite(site) ||
               HasSiteTag(site, "beta_fixture_settlement") ||
               HasSiteTag(site, "beta:settlement") ||
               HasSiteTag(site, "beta_fixture_hostile") ||
               HasSiteTag(site, "beta:hostile") ||
               HasSiteTag(site, "beta_fixture_cave") ||
               HasSiteTag(site, "beta_fixture_landmark") ||
               HasSiteTag(site, "beta:landmark") ||
               HasSiteTag(site, "multi_cell_site") ||
               siteId.IndexOf(":settlement:", StringComparison.OrdinalIgnoreCase) >= 0 ||
               siteId.IndexOf(":hostile:", StringComparison.OrdinalIgnoreCase) >= 0 ||
               siteId.IndexOf(":cave:", StringComparison.OrdinalIgnoreCase) >= 0 ||
               siteId.IndexOf(":landmark:", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static GeneratedRegionAssetPaletteRecord FindSitePalette(
        GeneratedWorldPlanRecord plan,
        string regionId)
    {
        if (plan == null || plan.assetPalettes == null || string.IsNullOrWhiteSpace(regionId))
            return null;
        for (int index = 0; index < plan.assetPalettes.Count; index++)
        {
            GeneratedRegionAssetPaletteRecord palette = plan.assetPalettes[index];
            if (palette != null && string.Equals(palette.regionId, regionId, StringComparison.OrdinalIgnoreCase))
                return palette;
        }
        return null;
    }

    private static GeneratedAssetReferenceRecord PickSiteReference(
        GeneratedRegionAssetPaletteRecord palette,
        YQSpatialMaterializationSiteV2 site,
        string seed)
    {
        if (palette == null)
            return null;
        string primarySlot = site.kind == YQSiteKindV2.Settlement
            ? YQWorldAssetCatalog.SlotSettlementBuilding
            : site.kind == YQSiteKindV2.HostileSite
                ? YQWorldAssetCatalog.SlotEnemySite
                : YQWorldAssetCatalog.SlotLargeStructure;
        GeneratedAssetReferenceRecord reference = YQWorldAssetCatalog.PickAssetForSlot(palette, primarySlot, seed);
        if (reference != null)
            return reference;
        reference = YQWorldAssetCatalog.PickAssetForSlot(palette, YQWorldAssetCatalog.SlotLargeStructure, seed + "|large");
        return reference ?? YQWorldAssetCatalog.PickAssetForSlot(palette, YQWorldAssetCatalog.SlotSettlementBuilding, seed + "|building");
    }

    private static GeneratedAssetReferenceRecord BuildApprovedSiteFallbackReference(
        YQSpatialMaterializationSiteV2 site,
        YQRuntimeWorldAssetRegistry registry)
    {
        if (registry == null)
            return null;
        string[] candidates = site.kind == YQSiteKindV2.Settlement
            ? new[] { YQContinuousWorldFeatureAuthority.BetaSettlementFallbackPrefab }
            : site.kind == YQSiteKindV2.HostileSite
                ? new[]
                {
                    YQContinuousWorldFeatureAuthority.BetaHostileFallbackPrefab,
                    YQContinuousWorldFeatureAuthority.BetaHostileFallbackRuinPrefab
                }
                : new[]
                {
                    YQContinuousWorldFeatureAuthority.BetaLandmarkFallbackPrefab,
                    YQContinuousWorldFeatureAuthority.BetaLandmarkFallbackGatePrefab,
                    YQContinuousWorldFeatureAuthority.BetaLandmarkFallbackChurchPrefab
                };
        for (int index = 0; index < candidates.Length; index++)
        {
            string candidatePath = candidates[index];
            if (registry.ResolvePrefab(candidatePath) == null)
                continue;
            return new GeneratedAssetReferenceRecord
            {
                assetPath = candidatePath,
                assetType = "prefab",
                slotTag = site.kind == YQSiteKindV2.Settlement
                    ? YQWorldAssetCatalog.SlotSettlementBuilding
                    : site.kind == YQSiteKindV2.HostileSite
                        ? YQWorldAssetCatalog.SlotEnemySite
                        : YQWorldAssetCatalog.SlotLargeStructure,
                runtimeEligible = true,
                scaleMin = 1f,
                scaleMax = 1f,
                weight = 1
            };
        }
        Debug.LogWarning(
            "[YQContinuousWorldFeatures] Approved fallback candidates unresolved site=" +
            site.siteId + " candidates=" + string.Join("|", candidates));
        return null;
    }

    private static GeneratedRegionAssetPaletteRecord FindPaletteWithApprovedSiteReference(
        GeneratedWorldPlanRecord plan,
        YQSpatialMaterializationSiteV2 site,
        GeneratedRegionAssetPaletteRecord preferred,
        string seed)
    {
        // note: Prefer the site's accepted region palette, but recover through another already-persisted approved palette when an older save omitted that region's derived site slot.
        if (PickSiteReference(preferred, site, seed) != null)
            return preferred;
        if (plan == null || plan.assetPalettes == null)
            return preferred;
        for (int index = 0; index < plan.assetPalettes.Count; index++)
        {
            GeneratedRegionAssetPaletteRecord candidate = plan.assetPalettes[index];
            if (candidate != null && PickSiteReference(candidate, site, seed + "|palette|" + index) != null)
                return candidate;
        }
        return preferred;
    }

    private static string SafeSiteName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";
        return value.Replace(':', '_').Replace('|', '_').Replace('/', '_').Replace('\\', '_');
    }

    private sealed class AcceptedCrossingSpan
    {
        public string crossingId;
        public int routeIndex;
        public float startDistance;
        public float endDistance;
        public float width;
        public List<Vector3> points;
    }

    private static IEnumerator PrepareAcceptedCrossingSpansRoutine(
        YQPreparedSpatialMaterializationV2 prepared, Terrain terrain, Vector2Int coordinate, float cellSize,
        List<AcceptedCrossingSpan> spans, Action<string, float> substageTelemetry)
    {
        if (prepared == null)
            yield break;
        float started = Time.realtimeSinceStartup;
        for (int index = 0; index < prepared.CrossingCount; index++)
        {
            YQSpatialMaterializationCrossingV2 crossing = prepared.GetCrossing(index);
            // note: Keep the existing anchor-cell owner. Neighbor ribbons remain until streaming can guarantee this owner's lifetime there.
            Vector2Int owner = new Vector2Int(
                Mathf.FloorToInt((crossing.x - WorldGridOrigin) / Mathf.Max(1f, cellSize)),
                Mathf.FloorToInt((crossing.z - WorldGridOrigin) / Mathf.Max(1f, cellSize)));
            if (owner == coordinate && (crossing.kind == YQRouteCrossingKindV2.Bridge || crossing.kind == YQRouteCrossingKindV2.Causeway))
            {
                // note: Reject an unresolved accepted crossing before removing its road; another successful bridge cannot mask the missing owner.
                if (!TryBuildCrossingSpan(prepared, crossing, terrain, out AcceptedCrossingSpan span, out string failure))
                    throw new InvalidOperationException("Accepted crossing " + crossing.crossingId + " cannot materialize: " + failure);
                spans.Add(span);
                RecordSubstage(substageTelemetry, "crossingSpanSearch", started);
                yield return null;
                started = Time.realtimeSinceStartup;
            }
            else if ((index + 1) % 16 == 0 || Time.realtimeSinceStartup - started >= YQGeneratedRiverBridge.CooperativeSliceSeconds)
            {
                RecordSubstage(substageTelemetry, "crossingOwnerScan", started);
                yield return null;
                started = Time.realtimeSinceStartup;
            }
        }
    }

    private static IEnumerator BuildAcceptedCrossingsRoutine(
        Transform parent, List<AcceptedCrossingSpan> spans, YQRuntimeWorldAssetRegistry registry,
        Action<string, float> substageTelemetry, Action<int> completed)
    {
        int built = 0;
        for (int index = 0; index < spans.Count; index++)
        {
            // note: Deck and omitted ribbon publish under the same hidden cell root, so a failed build cannot expose a road gap.
            AcceptedCrossingSpan span = spans[index];
            yield return YQGeneratedRiverBridge.BuildRoutine(parent, span.points, span.width, registry, substageTelemetry);
            built++;
            yield return null;
        }
        completed?.Invoke(built);
    }

    private static bool TryBuildCrossingSpan(
        YQPreparedSpatialMaterializationV2 prepared, YQSpatialMaterializationCrossingV2 crossing, Terrain terrain,
        out AcceptedCrossingSpan span, out string failure)
    {
        span = null;
        failure = "missing terrain or accepted route/hydrology identity";
        if (terrain == null || terrain.terrainData == null)
            return false;
        int routeIndex = -1;
        int waterIndex = -1;
        // note: Resolve exact accepted identities; nearby ribbons from other routes and water features are never crossing candidates.
        for (int index = 0; index < prepared.RouteCount; index++)
            if (string.Equals(prepared.GetRoute(index).routeId, crossing.routeId, StringComparison.Ordinal))
            { routeIndex = index; break; }
        for (int index = 0; index < prepared.WaterCount; index++)
            if (string.Equals(prepared.GetWater(index).hydrologyId, crossing.hydrologyId, StringComparison.Ordinal))
            { waterIndex = index; break; }
        if (routeIndex < 0 || waterIndex < 0)
            return false;
        YQSpatialMaterializationRouteV2 route = prepared.GetRoute(routeIndex);
        int count = prepared.GetRoutePointCount(routeIndex);
        if (count < 2 || route.width <= 0f)
            return false;
        Vector3[] routePoints = new Vector3[count];
        float[] distances = new float[count];
        float bestDistanceSquared = float.PositiveInfinity;
        float crossingDistance = 0f;
        Vector2 crossingPoint = new Vector2(crossing.x, crossing.z);
        for (int index = 0; index < count; index++)
        {
            YQSpatialMaterializationRoutePointV2 point = prepared.GetRoutePoint(routeIndex, index);
            routePoints[index] = new Vector3(point.x, point.surfaceElevationNormalized, point.z);
            if (index == 0) continue;
            Vector2 first = new Vector2(routePoints[index - 1].x, routePoints[index - 1].z);
            Vector2 delta = new Vector2(point.x, point.z) - first;
            float length = delta.magnitude;
            distances[index] = distances[index - 1] + length;
            if (length < 0.001f) continue;
            float t = Mathf.Clamp01(Vector2.Dot(crossingPoint - first, delta) / delta.sqrMagnitude);
            float distanceSquared = (first + delta * t - crossingPoint).sqrMagnitude;
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                crossingDistance = distances[index - 1] + length * t;
            }
        }
        float searchRadius = Mathf.Max(16f, Mathf.Max(crossing.requiredSpan, prepared.GetWater(waterIndex).nominalWidth) * 1.5f);
        if (bestDistanceSquared > searchRadius * searchRadius)
        { failure = "crossing anchor is outside its accepted route"; return false; }
        // note: Find the referenced wet interval near the accepted anchor before walking either direction to dry banks.
        float wetDistance = -1f;
        for (float offset = 0f; offset <= searchRadius && wetDistance < 0f; offset += 1f)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float along = Mathf.Clamp(crossingDistance + side * offset, 0f, distances[count - 1]);
                Vector3 point = SampleCrossingRoute(routePoints, distances, along);
                YQSpatialTerrainSampleV2 sample = prepared.SampleTerrain(point.x, point.z);
                if (sample.waterFeatureIndex == waterIndex && sample.waterMask > 0.05f)
                { wetDistance = along; break; }
            }
        }
        if (wetDistance < 0f)
        { failure = "accepted hydrology has no wet contact on its route near the crossing"; return false; }
        // note: A route may terminate in its accepted crossing. Extend only the deck approach along that endpoint tangent, bounded independently of route length, until the full width reaches proven dry ground.
        float approachWaterWidth = Mathf.Max(crossing.requiredSpan, prepared.GetWater(waterIndex).nominalWidth);
        for (int candidateIndex = 0; candidateIndex < prepared.WaterCount; candidateIndex++)
        {
            if (candidateIndex == waterIndex)
                continue;
            YQSpatialMaterializationWaterV2 candidate = prepared.GetWater(candidateIndex);
            float candidateWidth = Mathf.Max(0f, candidate.nominalWidth);
            float proximity = searchRadius + candidateWidth * 0.5f + route.width;
            if (DistanceToWaterFeatureSquared(prepared, candidateIndex, crossingPoint) <= proximity * proximity)
                approachWaterWidth = Mathf.Max(approachWaterWidth, candidateWidth);
        }
        // note: A river crossing at a lake or waterfall mouth must extend to the first dry bank across the connected water envelope, not stop at four river widths.
        float approachLimit = Mathf.Min(512f, Mathf.Max(32f, approachWaterWidth * 4f));
        bool startBankFound = TryFindCrossingBank(prepared, routePoints, distances, wetDistance, -1f,
            route.width, crossing.requiredSpan, approachLimit, out float start);
        bool endBankFound = TryFindCrossingBank(prepared, routePoints, distances, wetDistance, 1f,
            route.width, crossing.requiredSpan, approachLimit, out float end);
        if (!startBankFound || !endBankFound)
        {
            // note: Preserve enough local geometry in rejection diagnostics to distinguish a genuinely unbanked crossing from an undersized endpoint search.
            failure = "accepted crossing has no full-width dry bank within its route and bounded endpoint approaches" +
                " routeLength=" + distances[count - 1].ToString("0.0") +
                " wetDistance=" + wetDistance.ToString("0.0") +
                " approachWaterWidth=" + approachWaterWidth.ToString("0.0") +
                " approachLimit=" + approachLimit.ToString("0.0") +
                " startBank=" + startBankFound + "@" + start.ToString("0.0") +
                " endBank=" + endBankFound + "@" + end.ToString("0.0");
            return false;
        }

        // note: Preserve every accepted bend, including bends beyond the owner cell, instead of bridging a single tessellated ribbon segment.
        List<float> samples = new List<float> { start, end };
        for (float along = start + 2f; along < end; along += 2f) samples.Add(along);
        // note: Include endpoint bends when the bridge approach extends past the accepted polyline; its connection remains exactly on the accepted route.
        for (int index = 0; index < count; index++)
            if (distances[index] > start && distances[index] < end) samples.Add(distances[index]);
        samples.Sort();
        List<Vector3> points = new List<Vector3>(samples.Count);
        float previous = float.NegativeInfinity;
        float baseY = terrain.transform.position.y;
        float height = terrain.terrainData.size.y;
        foreach (float along in samples)
        {
            if (along - previous < 0.001f) continue;
            previous = along;
            Vector3 point = SampleCrossingRoute(routePoints, distances, along);
            YQSpatialTerrainSampleV2 sample = prepared.SampleTerrain(point.x, point.z);
            // note: Deck elevation clears the accepted road profile as well as water and terrain; only presentation height changes.
            float elevation = baseY + Mathf.Max(point.y, sample.elevationNormalized) * height + 0.20f;
            if (sample.waterFeatureIndex >= 0 && sample.waterMask > 0.05f)
                elevation = Mathf.Max(elevation, baseY + sample.waterSurfaceNormalized * height + 0.32f);
            points.Add(new Vector3(point.x, elevation, point.z));
        }
        span = new AcceptedCrossingSpan
        {
            crossingId = crossing.crossingId, routeIndex = routeIndex, startDistance = start, endDistance = end,
            width = route.width, points = points
        };
        failure = null;
        return points.Count >= 2;
    }

    private static float DistanceToWaterFeatureSquared(
        YQPreparedSpatialMaterializationV2 prepared,
        int waterIndex,
        Vector2 point)
    {
        int pointCount = prepared.GetWaterPointCount(waterIndex);
        if (pointCount <= 0)
            return float.PositiveInfinity;

        YQSpatialMaterializationWaterPointV2 first = prepared.GetWaterPoint(waterIndex, 0);
        Vector2 previous = new Vector2(first.x, first.z);
        float closestSquared = (point - previous).sqrMagnitude;
        for (int index = 1; index < pointCount; index++)
        {
            YQSpatialMaterializationWaterPointV2 currentRecord = prepared.GetWaterPoint(waterIndex, index);
            Vector2 current = new Vector2(currentRecord.x, currentRecord.z);
            Vector2 segment = current - previous;
            float denominator = segment.sqrMagnitude;
            float t = denominator > 0.0001f
                ? Mathf.Clamp01(Vector2.Dot(point - previous, segment) / denominator)
                : 0f;
            closestSquared = Mathf.Min(closestSquared, (point - (previous + segment * t)).sqrMagnitude);
            previous = current;
        }
        return closestSquared;
    }

    private static Vector3 SampleCrossingRoute(Vector3[] points, float[] distances, float along)
    {
        // note: Only bridge approaches sample beyond accepted endpoints. Keep their endpoint elevation while terrain/water clearance is resolved per deck sample, avoiding an unbounded extrapolated road grade.
        int last = points.Length - 1;
        if (along < 0f || along > distances[last])
        {
            int endpoint = along < 0f ? 0 : last;
            int neighbor = endpoint == 0 ? 1 : last - 1;
            int step = endpoint == 0 ? 1 : -1;
            while (neighbor >= 0 && neighbor <= last &&
                Mathf.Abs(distances[neighbor] - distances[endpoint]) < 0.001f)
                neighbor += step;
            if (neighbor < 0 || neighbor > last)
                return points[endpoint];
            Vector3 tangent = points[endpoint] - points[neighbor];
            tangent.y = 0f;
            return points[endpoint] + tangent.normalized * Mathf.Abs(along - distances[endpoint]);
        }
        // note: Arc-length sampling follows the full accepted polyline without changing its horizontal control points.
        int index = Array.BinarySearch(distances, along);
        if (index >= 0) return points[index];
        index = ~index;
        if (index <= 0) return points[0];
        if (index >= points.Length) return points[points.Length - 1];
        return Vector3.Lerp(points[index - 1], points[index],
            (along - distances[index - 1]) / Mathf.Max(0.001f, distances[index] - distances[index - 1]));
    }

    private static bool TryFindCrossingBank(YQPreparedSpatialMaterializationV2 prepared,
        Vector3[] points, float[] distances, float wetDistance, float direction, float width, float requiredSpan,
        float approachLimit, out float bank)
    {
        bank = wetDistance;
        float dryRun = 0f;
        bool previousDry = false;
        float total = distances[distances.Length - 1];
        // note: Test the bound in the requested direction so a wet endpoint can search both inward and outward; the opposite endpoint must not suppress the inward bank search.
        float minimum = -approachLimit;
        float maximum = total + approachLimit;
        while (direction < 0f ? bank > minimum : bank < maximum)
        {
            float next = Mathf.Clamp(bank + direction, minimum, maximum);
            Vector3 point = SampleCrossingRoute(points, distances, next);
            Vector3 tangent = SampleCrossingRoute(points, distances, next + 0.1f) -
                SampleCrossingRoute(points, distances, next - 0.1f);
            Vector2 side = new Vector2(-tangent.z, tangent.x).normalized;
            bool dry = true;
            // note: Require two continuous dry metres across the deck width, not merely a dry center sample beside a diagonal bank.
            for (int lateral = -2; lateral <= 2; lateral++)
            {
                Vector2 offset = side * (width * 0.25f * lateral);
                YQSpatialTerrainSampleV2 sample = prepared.SampleTerrain(point.x + offset.x, point.z + offset.y);
                if (sample.waterFeatureIndex >= 0 && sample.waterMask > 0.05f) { dry = false; break; }
            }
            dryRun = dry && previousDry ? dryRun + Mathf.Abs(next - bank) : 0f;
            previousDry = dry;
            bank = next;
            if (dryRun >= 2f && Mathf.Abs(bank - wetDistance) >= Mathf.Max(0f, requiredSpan * 0.5f))
                return true;
        }
        return false;
    }

    private static void PrepareWildernessLandmark(GameObject landmark)
    {
        // note: Landmark prefabs are static world geometry; imported rigidbodies are made kinematic before grounding to avoid physics-driven seam drift.
        if (landmark == null)
            return;
        Rigidbody[] bodies = landmark.GetComponentsInChildren<Rigidbody>(true);
        for (int index = 0; index < bodies.Length; index++)
        {
            Rigidbody body = bodies[index];
            if (body == null)
                continue;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private static int BuildAcceptedAreaWaterPatches(
        Transform parent,
        Terrain terrain,
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize,
        Material material)
    {
        if (parent == null || terrain == null || terrain.terrainData == null || prepared == null || material == null)
            return 0;
        int built = 0;
        float minX = WorldGridOrigin + coordinate.x * cellSize;
        float maxX = minX + cellSize;
        float minZ = WorldGridOrigin + coordinate.y * cellSize;
        float maxZ = minZ + cellSize;
        for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
        {
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            if (water.kind != YQHydrologyKindV2.Lake && water.kind != YQHydrologyKindV2.Wetland && water.kind != YQHydrologyKindV2.Coastline)
                continue;
            int pointCount = prepared.GetWaterPointCount(waterIndex);
            if (pointCount == 0)
                continue;
            Vector2 center = Vector2.zero;
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                YQSpatialMaterializationWaterPointV2 point = prepared.GetWaterPoint(waterIndex, pointIndex);
                center += new Vector2(point.x, point.z);
            }
            center /= pointCount;
            YQSpatialMaterializationWaterPointV2 first = prepared.GetWaterPoint(waterIndex, 0);
            YQSpatialMaterializationWaterPointV2 last = prepared.GetWaterPoint(waterIndex, pointCount - 1);
            Vector2 longAxis = new Vector2(last.x - first.x, last.z - first.z);
            if (longAxis.sqrMagnitude < 0.0001f)
                longAxis = Vector2.right;
            longAxis.Normalize();
            Vector2 shortAxis = new Vector2(-longAxis.y, longAxis.x);
            float longRadius = Mathf.Max(6f, water.nominalWidth * 0.5f);
            float shortRadius = Mathf.Max(5f, water.nominalWidth * 0.36f);
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                YQSpatialMaterializationWaterPointV2 point = prepared.GetWaterPoint(waterIndex, pointIndex);
                Vector2 offset = new Vector2(point.x, point.z) - center;
                float pointRadius = Mathf.Max(1f, Mathf.Max(water.nominalWidth, point.width) * 0.5f);
                longRadius = Mathf.Max(longRadius, Mathf.Abs(Vector2.Dot(offset, longAxis)) + pointRadius);
                shortRadius = Mathf.Max(shortRadius, Mathf.Abs(Vector2.Dot(offset, shortAxis)) + pointRadius);
            }
            if (center.x + longRadius < minX || center.x - longRadius > maxX || center.y + longRadius < minZ || center.y - longRadius > maxZ)
                continue;
            float surfaceNormalized = water.waterLevelNormalized > 0f ? water.waterLevelNormalized : first.waterSurfaceNormalized;
            float surfaceY = terrain.transform.position.y + terrain.terrainData.size.y * Mathf.Clamp01(surfaceNormalized) + 0.045f;
            const int grid = 10;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int zIndex = 0; zIndex < grid; zIndex++)
            {
                float z0 = Mathf.Lerp(minZ, maxZ, zIndex / (float)grid);
                float z1 = Mathf.Lerp(minZ, maxZ, (zIndex + 1) / (float)grid);
                for (int xIndex = 0; xIndex < grid; xIndex++)
                {
                    float x0 = Mathf.Lerp(minX, maxX, xIndex / (float)grid);
                    float x1 = Mathf.Lerp(minX, maxX, (xIndex + 1) / (float)grid);
                    Vector2 cellCenter = new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f) - center;
                    float major = Vector2.Dot(cellCenter, longAxis) / Mathf.Max(1f, longRadius);
                    float minor = Vector2.Dot(cellCenter, shortAxis) / Mathf.Max(1f, shortRadius);
                    if (major * major + minor * minor > 1.08f)
                        continue;
                    int start = vertices.Count;
                    vertices.Add(parent.InverseTransformPoint(new Vector3(x0, surfaceY, z0)));
                    vertices.Add(parent.InverseTransformPoint(new Vector3(x1, surfaceY, z0)));
                    vertices.Add(parent.InverseTransformPoint(new Vector3(x1, surfaceY, z1)));
                    vertices.Add(parent.InverseTransformPoint(new Vector3(x0, surfaceY, z1)));
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                    triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
                }
            }
            if (vertices.Count < 4)
                continue;
            Mesh mesh = new Mesh { name = "ContinuousAreaWaterMesh_" + waterIndex + "_" + coordinate.x + "_" + coordinate.y };
            mesh.SetVertices(vertices);
            // note: Area water has no left/right bank axis; the shader requires a midpoint value to keep its whole surface visible.
            Color[] colours = new Color[vertices.Count];
            Vector2[] uvs = new Vector2[vertices.Count];
            for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
            {
                colours[vertexIndex] = new Color(0f, 0.5f, 1f, 1f);
                Vector3 worldPoint = parent.TransformPoint(vertices[vertexIndex]);
                uvs[vertexIndex] = new Vector2(worldPoint.x, worldPoint.z) * 0.25f;
            }
            mesh.colors = colours;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            GameObject surface = new GameObject("ContinuousAreaWater_" + (water.hydrologyId ?? waterIndex.ToString()) + "_" + coordinate.x + "_" + coordinate.y);
            surface.transform.SetParent(parent, false);
            // note: Bind the persisted hydrology identity to the generated water surface so harvested/deleted overlays survive unload and reconstruction.
            YQStreamedFeatureOverlayTarget waterOverlayTarget = surface.AddComponent<YQStreamedFeatureOverlayTarget>();
            waterOverlayTarget.featureId = water.hydrologyId ?? string.Empty;
            waterOverlayTarget.objectId = surface.name;
            YQContinuousWorldOwnedResources resources = surface.AddComponent<YQContinuousWorldOwnedResources>();
            resources.Register(mesh);
            MeshFilter filter = surface.AddComponent<MeshFilter>();
            MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            surface.AddComponent<YQWaterSurfaceMotion>().Configure(filter, renderer, water.kind, 0.35f);
            MeshCollider collider = surface.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.enabled = false;
            built++;
        }
        return built;
    }

    private static bool TryGetAcceptedRiverPoints(YQPreparedSpatialMaterializationV2 prepared, Vector2Int coordinate, float cellSize, Terrain terrain, out List<Vector3> points, out float width, out List<int> spanBreaks, out List<float> pointWidths)
    {
        points = new List<Vector3>();
        width = 0f;
        spanBreaks = new List<int>();
        pointWidths = new List<float>();
        if (prepared == null)
            return false;
        float minX = WorldGridOrigin + coordinate.x * cellSize, maxX = WorldGridOrigin + (coordinate.x + 1) * cellSize;
        float minZ = WorldGridOrigin + coordinate.y * cellSize, maxZ = WorldGridOrigin + (coordinate.y + 1) * cellSize;
        float height = terrain != null && terrain.terrainData != null ? terrain.terrainData.size.y : YQGeneratedWorldTerrain.TerrainHeight;
        float baseY = terrain != null ? terrain.GetPosition().y : 0f;
        bool hasPreviousSpan = false;
        Vector2 previousSpanEnd = Vector2.zero;
        // note: Materialize every accepted water feature intersecting this cell; portal and carve authorities use the same sorted candidate set.
        List<int> acceptedWaterIndices = new List<int>();
        YQContinuousWorldFeatureAuthority.GetAcceptedWaterIndicesForCell(prepared, coordinate, cellSize, acceptedWaterIndices);
        for (int acceptedIndex = 0; acceptedIndex < acceptedWaterIndices.Count; acceptedIndex++)
        {
            int waterIndex = acceptedWaterIndices[acceptedIndex];
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            // note: Area waters use a fitted surface patch below; they are not linear ribbons even when their source has multiple control points.
            if (water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland || water.kind == YQHydrologyKindV2.Coastline)
                continue;
            // note: Keep each accepted identity's width and first point, including where two separate features meet at the same coordinate.
            int before = points.Count;
            hasPreviousSpan = false;
            width = Mathf.Max(width, water.nominalWidth);
            int pointCount = prepared.GetWaterPointCount(waterIndex);
            int firstSegment = 0;
            int lastSegment = pointCount - 2;
            if (prepared.TryGetWaterSegmentRangeForCell(
                    waterIndex, coordinate, cellSize,
                    out int indexedFirstSegment, out int indexedLastSegment))
            {
                firstSegment = indexedFirstSegment;
                lastSegment = indexedLastSegment;
            }
            else if (pointCount > 1 &&
                     Mathf.Abs(cellSize - YQSemanticWorldAuthority.CellSizeMeters) <= 0.01f)
            {
                continue;
            }
            for (int pointIndex = firstSegment; pointIndex <= lastSegment; pointIndex++)
            {
                YQSpatialMaterializationWaterPointV2 first = prepared.GetWaterPoint(waterIndex, pointIndex);
                YQSpatialMaterializationWaterPointV2 second = prepared.GetWaterPoint(waterIndex, pointIndex + 1);
                AppendAcceptedSegmentPoints(
                    points,
                    first.x, first.z, second.x, second.z,
                    first.waterSurfaceNormalized, second.waterSurfaceNormalized,
                    minX, maxX, minZ, maxZ,
                    Mathf.Max(8f, water.nominalWidth * 0.5f),
                    WorldGridOrigin + coordinate.x * cellSize, WorldGridOrigin + (coordinate.x + 1f) * cellSize,
                    WorldGridOrigin + coordinate.y * cellSize, WorldGridOrigin + (coordinate.y + 1f) * cellSize,
                    baseY, height, 0f, spanBreaks, ref hasPreviousSpan, ref previousSpanEnd);
            }
            CompleteAcceptedRibbonFeature(points, pointWidths, spanBreaks, before, water.nominalWidth);
        }
        List<YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation> waterContinuations = new List<YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation>();
        YQContinuousWorldFeatureAuthority.GetAcceptedWaterContinuations(prepared, coordinate, cellSize, waterContinuations);
        for (int continuationIndex = 0; continuationIndex < waterContinuations.Count; continuationIndex++)
        {
            // note: Each free-flowing terminal emits its own bounded span; area water and sink-linked rivers are filtered by the shared authority.
            YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation continuation = waterContinuations[continuationIndex];
            int before = points.Count;
            width = Mathf.Max(width, continuation.width);
            AppendTerminalContinuationPoints(points, continuation, coordinate, cellSize, baseY, height, 0f);
            CompleteAcceptedRibbonFeature(points, pointWidths, spanBreaks, before, continuation.width);
        }
        return points.Count >= 2;
    }

    private static bool TryGetAcceptedRoadPoints(YQPreparedSpatialMaterializationV2 prepared, Vector2Int coordinate, float cellSize, Terrain terrain, out List<Vector3> points, out float width, out List<int> spanBreaks, out List<float> pointWidths, List<AcceptedCrossingSpan> crossingSpans = null)
    {
        points = new List<Vector3>();
        width = 0f;
        spanBreaks = new List<int>();
        pointWidths = new List<float>();
        if (prepared == null)
            return false;
        float minX = WorldGridOrigin + coordinate.x * cellSize, maxX = WorldGridOrigin + (coordinate.x + 1) * cellSize;
        float minZ = WorldGridOrigin + coordinate.y * cellSize, maxZ = WorldGridOrigin + (coordinate.y + 1) * cellSize;
        float height = terrain != null && terrain.terrainData != null ? terrain.terrainData.size.y : YQGeneratedWorldTerrain.TerrainHeight;
        float baseY = terrain != null ? terrain.GetPosition().y : 0f;
        bool hasPreviousSpan = false;
        Vector2 previousSpanEnd = Vector2.zero;
        // note: Materialize every accepted route intersecting this cell instead of allowing the longest local route to hide other crossings.
        List<int> acceptedRouteIndices = new List<int>();
        YQContinuousWorldFeatureAuthority.GetAcceptedRouteIndicesForCell(prepared, coordinate, cellSize, acceptedRouteIndices);
        for (int acceptedIndex = 0; acceptedIndex < acceptedRouteIndices.Count; acceptedIndex++)
        {
            int routeIndex = acceptedRouteIndices[acceptedIndex];
            int before = points.Count;
            hasPreviousSpan = false;
            YQSpatialMaterializationRouteV2 route = prepared.GetRoute(routeIndex);
            width = Mathf.Max(width, route.width);
            int pointCount = prepared.GetRoutePointCount(routeIndex);
            // note: Exclude only decks built by this same cell transaction; other cells retain their ordinary road until bridge residency has an explicit dependency.
            List<AcceptedCrossingSpan> routeCrossings = new List<AcceptedCrossingSpan>();
            if (crossingSpans != null)
                for (int index = 0; index < crossingSpans.Count; index++)
                    if (crossingSpans[index].routeIndex == routeIndex) routeCrossings.Add(crossingSpans[index]);
            routeCrossings.Sort((left, right) => left.startDistance.CompareTo(right.startDistance));
            float routeDistance = 0f;
            int firstSegment = 0;
            int lastSegment = pointCount - 2;
            if (prepared.TryGetRouteSegmentRangeForCell(
                    routeIndex, coordinate, cellSize,
                    out int indexedFirstSegment, out int indexedLastSegment))
            {
                firstSegment = indexedFirstSegment;
                lastSegment = indexedLastSegment;
            }
            else if (pointCount > 1 &&
                     Mathf.Abs(cellSize - YQSemanticWorldAuthority.CellSizeMeters) <= 0.01f)
            {
                continue;
            }
            for (int pointIndex = routeCrossings.Count > 0 ? 0 : firstSegment; pointIndex <= lastSegment; pointIndex++)
            {
                YQSpatialMaterializationRoutePointV2 first = prepared.GetRoutePoint(routeIndex, pointIndex);
                YQSpatialMaterializationRoutePointV2 second = prepared.GetRoutePoint(routeIndex, pointIndex + 1);
                // note: Non-crossing cells retain their indexed extraction and exact endpoint values; only owned decks need arc-length clipping.
                if (routeCrossings.Count == 0)
                {
                    AppendAcceptedSegmentPoints(points, first.x, first.z, second.x, second.z,
                        first.surfaceElevationNormalized, second.surfaceElevationNormalized,
                        minX, maxX, minZ, maxZ, YQContinuousWorldFeatureAuthority.GetAcceptedRouteFootprintPadding(route),
                        minX, maxX, minZ, maxZ, baseY, height, 0.035f, spanBreaks,
                        ref hasPreviousSpan, ref previousSpanEnd);
                    continue;
                }
                float length = Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z));
                float segmentEnd = routeDistance + length;
                if (pointIndex >= firstSegment && length > 0.001f)
                {
                    // note: Subtract the shared arc-length intervals before tessellation, so no ordinary ribbon triangles cover an owned bridge deck.
                    float cursor = routeDistance;
                    for (int index = 0; index <= routeCrossings.Count && cursor < segmentEnd; index++)
                    {
                        AcceptedCrossingSpan crossing = index < routeCrossings.Count ? routeCrossings[index] : null;
                        if (crossing != null && crossing.endDistance <= cursor) continue;
                        float end = crossing != null ? Mathf.Min(segmentEnd, crossing.startDistance) : segmentEnd;
                        if (end > cursor)
                        {
                            float startT = (cursor - routeDistance) / length;
                            float endT = (end - routeDistance) / length;
                            AppendAcceptedSegmentPoints(points,
                                Mathf.Lerp(first.x, second.x, startT), Mathf.Lerp(first.z, second.z, startT),
                                Mathf.Lerp(first.x, second.x, endT), Mathf.Lerp(first.z, second.z, endT),
                                Mathf.Lerp(first.surfaceElevationNormalized, second.surfaceElevationNormalized, startT),
                                Mathf.Lerp(first.surfaceElevationNormalized, second.surfaceElevationNormalized, endT),
                                minX, maxX, minZ, maxZ, YQContinuousWorldFeatureAuthority.GetAcceptedRouteFootprintPadding(route),
                                minX, maxX, minZ, maxZ, baseY, height, 0.035f, spanBreaks,
                                ref hasPreviousSpan, ref previousSpanEnd, routeCrossings.Count == 0);
                        }
                        cursor = crossing != null ? Mathf.Max(cursor, crossing.endDistance) : segmentEnd;
                    }
                }
                routeDistance = segmentEnd;
            }
            CompleteAcceptedRibbonFeature(points, pointWidths, spanBreaks, before, route.width);
        }
        List<YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation> routeContinuations = new List<YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation>();
        YQContinuousWorldFeatureAuthority.GetAcceptedRouteContinuations(prepared, coordinate, cellSize, 6f, routeContinuations);
        for (int continuationIndex = 0; continuationIndex < routeContinuations.Count; continuationIndex++)
        {
            YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation continuation = routeContinuations[continuationIndex];
            int before = points.Count;
            width = Mathf.Max(width, continuation.width);
            AppendTerminalContinuationPoints(points, continuation, coordinate, cellSize, baseY, height, 0.035f);
            CompleteAcceptedRibbonFeature(points, pointWidths, spanBreaks, before, continuation.width);
        }
        return points.Count >= 2;
    }

    private static void CompleteAcceptedRibbonFeature(List<Vector3> points, List<float> pointWidths,
        List<int> spanBreaks, int firstPoint, float featureWidth)
    {
        // note: Width belongs to the accepted feature, never to the widest unrelated feature sharing its streamed cell.
        if (points.Count <= firstPoint)
            return;
        if (firstPoint > 0)
        {
            // note: Segment clipping may already have recorded later breaks within this feature; preserve their sorted order.
            int insertion = spanBreaks.BinarySearch(firstPoint);
            if (insertion < 0)
                spanBreaks.Insert(~insertion, firstPoint);
        }
        while (pointWidths.Count < points.Count)
            pointWidths.Add(featureWidth);
    }

    private static bool AppendTerminalContinuationPoints(
        List<Vector3> points,
        YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation continuation,
        Vector2Int coordinate,
        float cellSize,
        float baseY,
        float height,
        float verticalOffset)
    {
        // note: Emit a bounded continuation ray inside this cell so a short authored terminal cannot end the visible stream at a cell edge.
        if (!YQContinuousWorldFeatureAuthority.TryClipRayToCell(continuation.origin.x, continuation.origin.y, continuation.direction, coordinate, cellSize, 8f, out float startT, out float endT))
            return false;
        startT = Mathf.Max(0f, startT);
        // note: Terminal ribbons must cover the same accepted continuation envelope as the topology-5 contract; the physical streamer still bounds which cells are resident.
        endT = Mathf.Min(YQContinuousWorldFeatureAuthority.AcceptedContinuationMaxDistance, endT);
        if (endT - startT < 0.01f)
            return false;
        int before = points.Count;
        for (int sample = 0; sample <= 16; sample++)
        {
            float t = Mathf.Lerp(startT, endT, sample / 16f);
            float x = continuation.origin.x + continuation.direction.x * t;
            float z = continuation.origin.y + continuation.direction.y * t;
            float elevation = Mathf.Clamp01(continuation.elevation + continuation.elevationSlope * t);
            Vector3 point = new Vector3(x, baseY + elevation * height + verticalOffset, z);
            if (sample == 0 || (points[points.Count - 1] - point).sqrMagnitude > 0.0004f)
                points.Add(point);
        }
        return points.Count - before >= 2;
    }

    internal static bool AppendAcceptedSegmentPoints(
        List<Vector3> points,
        float firstX, float firstZ, float secondX, float secondZ,
        float firstElevation, float secondElevation,
        float minimumX, float maximumX, float minimumZ, float maximumZ,
        float padding,
        float exactMinimumX, float exactMaximumX, float exactMinimumZ, float exactMaximumZ,
        float baseY, float height, float verticalOffset,
        List<int> spanBreaks,
        ref bool hasPreviousSpan,
        ref Vector2 previousSpanEnd,
        bool allowEndpointExtension = true)
    {
        // note: Clip against a padded cell first so the ribbon overlaps its neighbour, then add exact border parameters to remove quantized gaps.
        if (!TryClipSegmentToBounds(
                firstX, firstZ, secondX, secondZ,
                minimumX - padding, maximumX + padding,
                minimumZ - padding, maximumZ + padding,
                out float startT, out float endT))
            return false;

        // note: Extend a source spline through the authored-world boundary when its final control point stops just short of the next cell; this preserves one continuous route/water ribbon instead of exposing a seam or silently switching to a different fallback feature.
        if (!TryClipSegmentToBounds(
                firstX, firstZ, secondX, secondZ,
                exactMinimumX, exactMaximumX,
                exactMinimumZ, exactMaximumZ,
                out float exactStartT, out float exactEndT))
        {
            if (allowEndpointExtension && TryClipInfiniteLineToBounds(
                    firstX, firstZ, secondX, secondZ,
                    exactMinimumX, exactMaximumX,
                    exactMinimumZ, exactMaximumZ,
                    out float lineStartT, out float lineEndT))
            {
                float segmentLength = Mathf.Max(0.001f, Mathf.Sqrt(
                    (secondX - firstX) * (secondX - firstX) +
                    (secondZ - firstZ) * (secondZ - firstZ)));
                float maximumExtensionT = padding / segmentLength;
                if (lineStartT > 1f && lineStartT - 1f <= maximumExtensionT + 0.001f)
                    endT = Mathf.Max(endT, lineEndT);
                else if (lineEndT < 0f && -lineEndT <= maximumExtensionT + 0.001f)
                    startT = Mathf.Min(startT, lineStartT);
            }
        }
        else
        {
            startT = Mathf.Min(startT, exactStartT);
            endT = Mathf.Max(endT, exactEndT);
        }

        Vector2 clippedStart = new Vector2(
            Mathf.LerpUnclamped(firstX, secondX, startT),
            Mathf.LerpUnclamped(firstZ, secondZ, startT));
        Vector2 clippedEnd = new Vector2(
            Mathf.LerpUnclamped(firstX, secondX, endT),
            Mathf.LerpUnclamped(firstZ, secondZ, endT));
        bool beginsNewSpan = !hasPreviousSpan || (previousSpanEnd - clippedStart).sqrMagnitude > 0.0025f;
        if (hasPreviousSpan && beginsNewSpan && spanBreaks != null)
            spanBreaks.Add(points.Count);
        hasPreviousSpan = true;
        previousSpanEnd = clippedEnd;

        float[] parameters = new float[24];
        int parameterCount = 0;
        AddSegmentParameter(parameters, ref parameterCount, startT);
        AddSegmentParameter(parameters, ref parameterCount, endT);
        for (int sample = 0; sample <= 16; sample++)
            AddSegmentParameter(parameters, ref parameterCount, Mathf.Lerp(startT, endT, sample / 16f));
        AddBoundaryParameter(parameters, ref parameterCount, firstX, secondX - firstX, exactMinimumX, startT, endT);
        AddBoundaryParameter(parameters, ref parameterCount, firstX, secondX - firstX, exactMaximumX, startT, endT);
        AddBoundaryParameter(parameters, ref parameterCount, firstZ, secondZ - firstZ, exactMinimumZ, startT, endT);
        AddBoundaryParameter(parameters, ref parameterCount, firstZ, secondZ - firstZ, exactMaximumZ, startT, endT);
        for (int left = 1; left < parameterCount; left++)
        {
            float value = parameters[left];
            int right = left - 1;
            while (right >= 0 && parameters[right] > value)
            {
                parameters[right + 1] = parameters[right];
                right--;
            }
            parameters[right + 1] = value;
        }
        float previousT = float.NaN;
        for (int index = 0; index < parameterCount; index++)
        {
            // note: Parameters may intentionally lie just outside 0..1 when an authored endpoint stops short of a streamed cell; preserve that extrapolation so the shared feature reaches the neighbor.
            float t = parameters[index];
            if (!float.IsNaN(previousT) && Mathf.Abs(t - previousT) < 0.0001f)
                continue;
            previousT = t;
            float x = Mathf.LerpUnclamped(firstX, secondX, t);
            float z = Mathf.LerpUnclamped(firstZ, secondZ, t);
            // note: Keep terminal extrapolation inside the terrain's normalized vertical domain so a malformed short source segment cannot create an airborne ribbon or a subterranean water wall.
            float elevation = Mathf.Clamp01(Mathf.LerpUnclamped(firstElevation, secondElevation, t));
            Vector3 point = new Vector3(x, baseY + elevation * height + verticalOffset, z);
            // note: Adjacent source segments share a control point; keep one copy so turns cannot create a folded zero-length quad.
            if (points.Count == 0 || (index == 0 && beginsNewSpan) || (points[points.Count - 1] - point).sqrMagnitude > 0.0004f)
                points.Add(point);
        }
        return true;
    }

    private static void AddBoundaryParameter(float[] parameters, ref int count, float start, float delta, float boundary, float minimumT, float maximumT)
    {
        if (Mathf.Abs(delta) < 0.0001f)
            return;
        float t = (boundary - start) / delta;
        if (t >= minimumT - 0.0001f && t <= maximumT + 0.0001f)
            AddSegmentParameter(parameters, ref count, t);
    }

    private static void AddSegmentParameter(float[] parameters, ref int count, float value)
    {
        if (count >= parameters.Length)
            return;
        parameters[count++] = value;
    }

    private static bool TryClipSegmentToBounds(
        float firstX, float firstZ, float secondX, float secondZ,
        float minimumX, float maximumX, float minimumZ, float maximumZ,
        out float startT, out float endT)
    {
        startT = 0f;
        endT = 1f;
        return ClipSegmentAxis(firstX, secondX - firstX, minimumX, maximumX, ref startT, ref endT) &&
            ClipSegmentAxis(firstZ, secondZ - firstZ, minimumZ, maximumZ, ref startT, ref endT) &&
            startT <= endT;
    }

    private static bool TryClipInfiniteLineToBounds(
        float firstX, float firstZ, float secondX, float secondZ,
        float minimumX, float maximumX, float minimumZ, float maximumZ,
        out float startT, out float endT)
    {
        startT = float.NegativeInfinity;
        endT = float.PositiveInfinity;
        float deltaX = secondX - firstX;
        float deltaZ = secondZ - firstZ;
        if (!ClipInfiniteLineAxis(firstX, deltaX, minimumX, maximumX, ref startT, ref endT) ||
            !ClipInfiniteLineAxis(firstZ, deltaZ, minimumZ, maximumZ, ref startT, ref endT))
            return false;
        return startT <= endT;
    }

    private static bool ClipInfiniteLineAxis(
        float start, float delta, float minimum, float maximum,
        ref float startT, ref float endT)
    {
        if (Mathf.Abs(delta) < 0.0001f)
            return start >= minimum && start <= maximum;
        float inverse = 1f / delta;
        float enter = (minimum - start) * inverse;
        float exit = (maximum - start) * inverse;
        if (enter > exit)
        {
            float swap = enter;
            enter = exit;
            exit = swap;
        }
        startT = Mathf.Max(startT, enter);
        endT = Mathf.Min(endT, exit);
        return startT <= endT;
    }

    private static bool ClipSegmentAxis(float start, float delta, float minimum, float maximum, ref float startT, ref float endT)
    {
        if (Mathf.Abs(delta) < 0.0001f)
            return start >= minimum && start <= maximum;
        float inverse = 1f / delta;
        float enter = (minimum - start) * inverse;
        float exit = (maximum - start) * inverse;
        if (enter > exit)
        {
            float swap = enter;
            enter = exit;
            exit = swap;
        }
        startT = Mathf.Max(startT, enter);
        endT = Mathf.Min(endT, exit);
        return startT <= endT;
    }

    private static int BuildRibbonSpans(
        Transform parent,
        string name,
        List<Vector3> points,
        List<int> spanBreaks,
        float width,
        Material material,
        bool water,
        float cellSize,
        IReadOnlyList<float> pointWidths = null)
    {
        if (points == null || points.Count < 2)
            return 0;
        // note: A malformed width stream fails publication instead of borrowing a neighboring feature's appearance.
        if (pointWidths != null && pointWidths.Count != points.Count)
            return 0;
        int built = 0;
        float spanWidth = width;
        List<Vector3> span = new List<Vector3>();
        int spanIndex = 0;
        int nextBreak = 0;
        for (int index = 0; index < points.Count; index++)
        {
            Vector3 point = points[index];
            bool explicitBreak = nextBreak < (spanBreaks != null ? spanBreaks.Count : 0) && index >= spanBreaks[nextBreak];
            // note: Discontinuity tolerance follows this span's width, independent of other roads or rivers in the cell.
            float discontinuity = Mathf.Max(24f, Mathf.Max(Mathf.Max(1f, spanWidth) * 2f, cellSize * 0.25f));
            // note: Treat a large spatial jump as a hard span break even when persisted metadata supplied explicit breaks; metadata must never allow an unrelated route segment to become a giant triangle strip.
            bool inferredBreak = span.Count > 0 && Vector3.Distance(span[span.Count - 1], point) > discontinuity;
            if (explicitBreak || inferredBreak)
            {
                if (span.Count >= 2 && BuildRibbon(parent, name + "_Span" + spanIndex, span, spanWidth, material, water) != null)
                    built++;
                spanIndex++;
                span.Clear();
                while (nextBreak < (spanBreaks != null ? spanBreaks.Count : 0) && index >= spanBreaks[nextBreak])
                    nextBreak++;
            }
            if (span.Count == 0)
                spanWidth = pointWidths != null ? pointWidths[index] : width;
            if (span.Count == 0 || (span[span.Count - 1] - point).sqrMagnitude > 0.0004f)
                span.Add(point);
        }
        if (span.Count >= 2 && BuildRibbon(parent, name + "_Span" + spanIndex, span, spanWidth, material, water) != null)
            built++;
        return built;
    }

    private static int BuildSampledSiteConnector(
        Transform parent,
        string name,
        Terrain terrain,
        Vector3 owner,
        Vector3 member,
        float width,
        Material material)
    {
        if (parent == null || terrain == null || material == null)
            return 0;

        // note: Sample long owner/member links at sub-discontinuity spacing so accepted multi-cell sites receive continuous terrain-following access instead of a rejected two-point jump.
        float horizontalDistance = Vector2.Distance(
            new Vector2(owner.x, owner.z),
            new Vector2(member.x, member.z));
        const float maximumSampleSpacing = 8f;
        int sampleCount = Mathf.Max(2, Mathf.CeilToInt(horizontalDistance / maximumSampleSpacing) + 1);
        List<Vector3> accessPoints = new List<Vector3>(sampleCount);
        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float t = sampleCount <= 1 ? 0f : sampleIndex / (float)(sampleCount - 1);
            Vector3 point = Vector3.Lerp(owner, member, t);
            point.y = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, point) + 0.04f;
            accessPoints.Add(point);
        }
        return BuildRibbonSpans(
            parent,
            name,
            accessPoints,
            null,
            width,
            material,
            false,
            128f);
    }

    private static GameObject BuildRibbon(Transform parent, string name, List<Vector3> points, float width, Material material, bool water)
    {
        if (points == null || points.Count < 2 || parent == null || material == null)
            return null;
        int count = points.Count;
        Vector3[] vertices = new Vector3[count * 2];
        Vector2[] uvs = new Vector2[count * 2];
        Color[] colours = water ? new Color[count * 2] : null;
        int[] triangles = new int[(count - 1) * 6];
        for (int index = 0; index < count; index++)
        {
            // note: Reject non-finite source points before they can poison a mesh; malformed persisted geometry must fail closed as a missing span.
            if (!IsFinitePoint(points[index]))
                return null;
            Vector3 previousDirection = index > 0
                ? new Vector3(points[index].x - points[index - 1].x, 0f, points[index].z - points[index - 1].z).normalized
                : Vector3.zero;
            Vector3 nextDirection = index + 1 < count
                ? new Vector3(points[index + 1].x - points[index].x, 0f, points[index + 1].z - points[index].z).normalized
                : Vector3.zero;
            Vector3 direction = index == 0 ? nextDirection : index == count - 1 ? previousDirection : (previousDirection + nextDirection).normalized;
            if (direction.sqrMagnitude < 0.0001f)
                direction = nextDirection.sqrMagnitude >= 0.0001f ? nextDirection : previousDirection;
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized * width * 0.5f;
            if (index > 0 && index + 1 < count && previousDirection.sqrMagnitude >= 0.0001f && nextDirection.sqrMagnitude >= 0.0001f)
            {
                // note: Use a capped miter at bends so a near-reversal cannot turn a normal path width into a long triangular spike.
                Vector3 miter = Vector3.Cross(Vector3.up, direction).normalized;
                float miterDenominator = Vector3.Dot(miter, Vector3.Cross(Vector3.up, nextDirection).normalized);
                if (Mathf.Abs(miterDenominator) > 0.2f)
                    side = miter * Mathf.Clamp((width * 0.5f) / miterDenominator, -width, width);
            }
            // note: Source splines are authoritative world coordinates; convert them into the owning root's local space so a translated/scaled world root cannot displace the published ribbon.
            vertices[index * 2] = parent.InverseTransformPoint(points[index] - side);
            vertices[index * 2 + 1] = parent.InverseTransformPoint(points[index] + side);
            // note: World-space repeats keep approved road textures at a stable scale across chunks instead of stretching one sample along an entire ribbon.
            Vector3 left = points[index] - side;
            Vector3 right = points[index] + side;
            uvs[index * 2] = new Vector2(left.x, left.z) * 0.25f;
            uvs[index * 2 + 1] = new Vector2(right.x, right.z) * 0.25f;
            if (water)
            {
                // note: The water shader reads green as bank position; omitting these values makes shore fading erase the whole streamed river.
                colours[index * 2] = new Color(0f, 0f, 1f, 1f);
                colours[index * 2 + 1] = new Color(0f, 1f, 1f, 1f);
            }
            if (index >= count - 1)
                continue;
            int triangle = index * 6;
            int vertex = index * 2;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 2;
            triangles[triangle + 2] = vertex + 1;
            triangles[triangle + 3] = vertex + 1;
            triangles[triangle + 4] = vertex + 2;
            triangles[triangle + 5] = vertex + 3;
        }
        Mesh mesh = new Mesh { name = name + "_Mesh" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        if (water)
            mesh.colors = colours;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        YQContinuousWorldOwnedResources resources = root.AddComponent<YQContinuousWorldOwnedResources>();
        resources.Register(mesh);
        MeshFilter filter = root.AddComponent<MeshFilter>();
        MeshRenderer renderer = root.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (water)
        {
            root.AddComponent<YQWaterSurfaceMotion>().Configure(filter, renderer, YQHydrologyKindV2.River, 1.2f);
            MeshCollider collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.enabled = false;
        }
        else
        {
            MeshCollider collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
        return root;
    }

    private static bool IsFinitePoint(Vector3 point)
    {
        return !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
            !float.IsNaN(point.y) && !float.IsInfinity(point.y) &&
            !float.IsNaN(point.z) && !float.IsInfinity(point.z);
    }

    // note: Streamed feature meshes and cloned materials are owned by their cell root and released on unload.
    private sealed class YQContinuousWorldOwnedResources : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        public void Register(UnityEngine.Object resource) { if (resource != null && !owned.Contains(resource)) owned.Add(resource); }
        private void OnDestroy()
        {
            for (int index = 0; index < owned.Count; index++)
                if (owned[index] != null) UnityEngine.Object.Destroy(owned[index]);
            owned.Clear();
        }
    }

    private static void RegisterCellResource(Transform parent, UnityEngine.Object resource)
    {
        if (parent == null || resource == null)
            return;
        YQContinuousWorldOwnedResources owner = parent.GetComponent<YQContinuousWorldOwnedResources>();
        if (owner == null)
            owner = parent.gameObject.AddComponent<YQContinuousWorldOwnedResources>();
        // note: A shared cloned material is registered once at the cell root, preventing multiple ribbon spans from destroying the same object repeatedly.
        owner.Register(resource);
    }

    private static Material FindPaletteMaterial(GeneratedRegionAssetPaletteRecord palette, YQRuntimeWorldAssetRegistry registry, string slot, string seed)
    {
        GeneratedAssetReferenceRecord reference = YQWorldAssetCatalog.PickAssetForSlot(palette, slot, seed);
        return reference != null ? FindMaterial(registry, reference.assetPath) : FindMaterial(registry, "ground", "mud", "path");
    }

    private static Material BuildFallbackWaterMaterial()
    {
        Shader shader = Shader.Find("YourQuest/Generated Water") ??
                        Shader.Find("Universal Render Pipeline/Lit") ??
                        Shader.Find("Standard");
        if (shader == null)
            return null;
        Material material = new Material(shader) { name = "ContinuousWaterFallback", hideFlags = HideFlags.DontSave };
        // note: Preserve the authored blue flowing-water look when the custom shader is present, while keeping a visible blue fallback if Unity cannot resolve it during a refresh.
        if (material.HasProperty("_ShallowColor")) material.SetColor("_ShallowColor", new Color(.055f, .28f, .62f, 1f));
        if (material.HasProperty("_DeepColor")) material.SetColor("_DeepColor", new Color(.008f, .045f, .16f, 1f));
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(.025f, .18f, .52f, 1f));
        if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(.025f, .18f, .52f, 1f));
        if (material.HasProperty("_FlowSpeed")) material.SetFloat("_FlowSpeed", .09f);
        if (material.HasProperty("_RippleStrength")) material.SetFloat("_RippleStrength", .24f);
        if (material.HasProperty("_WorldUV")) material.SetFloat("_WorldUV", 1f);
        material.renderQueue = 2401;
        return material;
    }

    private static Material BuildFallbackRoadMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return null;
        Material material = new Material(shader) { name = "ContinuousRoadFallback", hideFlags = HideFlags.DontSave };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(.20f, .13f, .08f, 1f));
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(.20f, .13f, .08f, 1f));
        return material;
    }

    private static Material FindMaterial(YQRuntimeWorldAssetRegistry registry, params string[] tokens)
    {
        if (registry == null)
            return null;
        // note: The root registry intentionally contains no pack entries. Resolve exact palette assets through their lazy shard and approved water through its shared source policy.
        bool water = tokens != null && tokens.Length > 0 && (tokens[0] == "water" || tokens[0] == "river");
        if (water)
        {
            Renderer source = YQGeneratedWorldEnvironment.ResolveApprovedWaterMaterialSource(registry);
            if (source == null || source.sharedMaterial == null)
                return null;
            Material clone = new Material(source.sharedMaterial) { name = source.sharedMaterial.name + "_ContinuousFeature", hideFlags = HideFlags.DontSave };
            Texture bump = YQGeneratedWorldEnvironment.ResolveWaterRippleNormal(source.sharedMaterial);
            Shader shader = Shader.Find("YourQuest/Generated Water");
            if (shader != null)
            {
                clone.shader = shader;
                clone.shaderKeywords = Array.Empty<string>();
                if (bump != null) clone.SetTexture("_BumpMap", bump);
                clone.SetColor("_ShallowColor", new Color(.055f, .28f, .62f, 1f));
                clone.SetColor("_DeepColor", new Color(.008f, .045f, .16f, 1f));
                clone.SetFloat("_FlowSpeed", .09f);
                clone.SetFloat("_RippleStrength", .24f);
                clone.SetFloat("_WorldUV", 1f);
                clone.SetFloat("_FoamOnly", 0f);
            }
            clone.renderQueue = 2401;
            return clone;
        }
        IReadOnlyList<YQRuntimeWorldAssetEntry> entries = tokens != null && tokens.Length > 0 &&
            tokens[0].StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            ? registry.GetEntriesForAssetPath(tokens[0]) : registry.Entries;
        for (int index = 0; index < (entries != null ? entries.Count : 0); index++)
        {
            YQRuntimeWorldAssetEntry entry = entries[index];
            if (entry == null || string.IsNullOrWhiteSpace(entry.assetPath))
                continue;
            string path = entry.assetPath.ToLowerInvariant();
            bool match = tokens == null || tokens.Length == 0;
            for (int tokenIndex = 0; tokenIndex < (tokens != null ? tokens.Length : 0); tokenIndex++)
                if (path.Contains(tokens[tokenIndex].ToLowerInvariant()))
                    match = true;
            if (!match)
                continue;
            Material material = entry.material;
            Renderer sourceRenderer = null;
            if (material == null && entry.prefab != null)
            {
                sourceRenderer = entry.prefab.GetComponentInChildren<Renderer>(true);
                material = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
            }
            // note: Keep the imported textures when adapting a vendor surface to URP; the source asset and cached shared adaptation are never owned by this cell.
            material = YQRuntimeUrpMaterialRepair.ResolveGeneratedSurfaceMaterial(material, sourceRenderer);
            if (material == null)
                continue;
            Material clone = new Material(material) { name = material.name + "_ContinuousFeature", hideFlags = HideFlags.DontSave };
            return clone;
        }
        return null;
    }
}

