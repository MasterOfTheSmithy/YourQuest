using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Profiling;
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

    internal readonly struct FrontierRouteConnection
    {
        internal readonly string identity;
        internal readonly Vector3 point;
        internal readonly float distance;
        internal readonly uint priority;

        internal FrontierRouteConnection(string identity, Vector3 point, float distance, uint priority)
        { this.identity = identity; this.point = point; this.distance = distance; this.priority = priority; }
    }

    internal static bool TryCollectFrontierRouteConnections(YQPreparedSpatialMaterializationV2 prepared,
        float x, float z, string seed, List<FrontierRouteConnection> connections, out string failure)
    {
        // note: Planning projects onto real finite roads and their permitted terminal rays; a bounded nearest set is independent of accepted-list traversal order.
        const int maximumConnections = 4;
        const int maximumSegmentChecks = 32768;
        const float maximumDistance = 1536f;
        failure = string.Empty;
        if (connections == null || prepared == null)
        { failure = "frontier network projection lacks an immutable prepared context"; return false; }
        connections.Clear();
        long checks = prepared.RouteCount * 2L;
        for (int index = 0; index < prepared.RouteCount; index++)
        {
            checks += prepared.GetRoutePointCount(index);
            if (checks > maximumSegmentChecks)
            { failure = "frontier network projection exceeds 32768 accepted segment checks"; return false; }
        }
        Vector2 target = new Vector2(x, z);
        for (int index = 0; index < prepared.RouteCount; index++)
        {
            var route = prepared.GetRoute(index);
            int count = prepared.GetRoutePointCount(index);
            for (int segment = 0; segment + 1 < count; segment++)
            {
                var first = prepared.GetRoutePoint(index, segment);
                var second = prepared.GetRoutePoint(index, segment + 1);
                Vector2 start = new Vector2(first.x, first.z);
                Vector2 delta = new Vector2(second.x - first.x, second.z - first.z);
                if (delta.sqrMagnitude < .0001f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(target - start, delta) / delta.sqrMagnitude);
                Vector2 position = start + delta * t;
                Add(route.routeId + "|segment:" + segment.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    position, Mathf.Lerp(first.surfaceElevationNormalized, second.surfaceElevationNormalized, t));
            }
            if (!route.permittedBoundaryContinuation || count < 2) continue;
            for (int end = 0; end < 2; end++)
            {
                var terminal = prepared.GetRoutePoint(index, end == 0 ? 0 : count - 1);
                var adjacent = prepared.GetRoutePoint(index, end == 0 ? 1 : count - 2);
                Vector2 start = new Vector2(terminal.x, terminal.z);
                Vector2 delta = start - new Vector2(adjacent.x, adjacent.z);
                float length = delta.magnitude;
                if (length < .01f) continue;
                Vector2 direction = delta / length;
                if (!TryGetOutwardBoundaryDistance(start.x, start.y, direction, out _)) continue;
                float along = Mathf.Clamp(Vector2.Dot(target - start, direction), 0f, AcceptedContinuationMaxDistance);
                Add(route.routeId + "|terminal:" + end.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    start + direction * along,
                    Mathf.Clamp01(terminal.surfaceElevationNormalized + along * (terminal.surfaceElevationNormalized - adjacent.surfaceElevationNormalized) / length));
            }
        }
        if (connections.Count == 0)
        { failure = "frontier frontage has no real accepted road within 1536 metres"; return false; }
        return true;

        void Add(string identity, Vector2 position, float elevation)
        {
            float distance = Vector2.Distance(target, position);
            if (distance > maximumDistance || float.IsNaN(distance) || float.IsInfinity(distance) ||
                float.IsNaN(elevation) || float.IsInfinity(elevation) || elevation < 0f || elevation > 1f ||
                Mathf.Abs(position.x) > 1048576f || Mathf.Abs(position.y) > 1048576f) return;
            uint priority = 2166136261u;
            string input = seed + "|frontier_physical_context_v1|network|" + identity;
            foreach (char character in input) priority = unchecked((priority ^ character) * 16777619u);
            var connection = new FrontierRouteConnection(identity, new Vector3(position.x, elevation, position.y), distance, priority);
            int insertion = 0;
            while (insertion < connections.Count && Compare(connections[insertion], connection) <= 0) insertion++;
            if (insertion >= maximumConnections) return;
            connections.Insert(insertion, connection);
            if (connections.Count > maximumConnections) connections.RemoveAt(maximumConnections);
        }
        int Compare(FrontierRouteConnection first, FrontierRouteConnection second)
        {
            int order = first.distance.CompareTo(second.distance);
            if (order != 0) return order;
            order = first.priority.CompareTo(second.priority);
            return order != 0 ? order : string.CompareOrdinal(first.identity, second.identity);
        }
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
            // note: Area demand uses the same fitted surface footprint as construction; a broad circle must not require water in dry ellipse corners.
            if (water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland || water.kind == YQHydrologyKindV2.Coastline)
            {
                if (AreaIntersectsCell(prepared, waterIndex, coordinate, cellSize)) indices.Add(waterIndex);
                continue;
            }
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
        // note: Native join coverage can extend one terrain triangle past a source ribbon; demand follows actual accepted water polygons.
        float nativeMinX = WorldGridOrigin + coordinate.x * cellSize, nativeMinZ = WorldGridOrigin + coordinate.y * cellSize;
        for (int join = 0; join < prepared.NativeJoinCount; join++)
        for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
            if (!indices.Contains(waterIndex) && prepared.GetNativeJoin(join).IntersectsChannelRectangle(
                waterIndex, nativeMinX, nativeMinZ, nativeMinX + cellSize, nativeMinZ + cellSize)) indices.Add(waterIndex);
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
        var connected = prepared.GetWater(waterIndex).connectedBasin;
        if (connected != null)
        {
            // note: Demand only cells with positive accepted wet area; dry islands and enclosing terrain do not request water publication.
            float x = WorldGridOrigin + coordinate.x * cellSize, z = WorldGridOrigin + coordinate.y * cellSize;
            if (connected.MaximumX < x || connected.MinimumX > x + cellSize || connected.MaximumZ < z || connected.MinimumZ > z + cellSize) return false;
            var vertices = new List<Vector2>(); var triangles = new List<int>();
            connected.AppendClippedTriangles(x, z, x + cellSize, z + cellSize, vertices, triangles);
            return triangles.Count > 0;
        }
        // note: Elongated lakes occupy their complete accepted ellipse, not only a nominal-width circle at their center.
        GetAcceptedAreaWaterFootprint(prepared, waterIndex, out Vector2 center, out Vector2 axis, out float major, out float minor);
        float minX = WorldGridOrigin + coordinate.x * cellSize, minZ = WorldGridOrigin + coordinate.y * cellSize;
        Vector2 side = new Vector2(-axis.y, axis.x);
        if (center.x >= minX && center.x <= minX + cellSize && center.y >= minZ && center.y <= minZ + cellSize) return true;
        // note: Transform the cell to ellipse space, then measure each edge against the unit disk without temporary arrays.
        for (int edge = 0; edge < 4; edge++)
        {
            Vector2 Corner(int i) => new Vector2(minX + (i == 1 || i == 2 ? cellSize : 0f), minZ + (i >= 2 ? cellSize : 0f));
            Vector2 Normalize(Vector2 p) { p -= center; return new Vector2(Vector2.Dot(p, axis) / major, Vector2.Dot(p, side) / minor); }
            Vector2 first = Normalize(Corner(edge)), second = Normalize(Corner((edge + 1) % 4));
            Vector2 delta = second - first;
            float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector2.Dot(first, delta) / delta.sqrMagnitude) : 0f;
            // note: The inscribed 96-segment mesh contains this disk, avoiding a demanded sub-texel sliver outside its rim chords.
            if ((first + delta * t).sqrMagnitude <= .9989f) return true;
        }
        return false;
    }

    internal static void GetAcceptedAreaWaterFootprint(YQPreparedSpatialMaterializationV2 prepared, int index,
        out Vector2 center, out Vector2 axis, out float major, out float minor)
    {
        // note: Share the canonical basin's fitted ellipse and 0.94 surface inset across terrain, demand, and mesh construction.
        var water = prepared.GetWater(index);
        int count = prepared.GetWaterPointCount(index);
        center = prepared.GetWaterAreaCenter(index);
        var first = prepared.GetWaterPoint(index, 0);
        var last = prepared.GetWaterPoint(index, count - 1);
        axis = new Vector2(last.x - first.x, last.z - first.z);
        axis = axis.sqrMagnitude < .0001f ? Vector2.right : axis.normalized;
        Vector2 side = new Vector2(-axis.y, axis.x);
        major = Mathf.Max(6f, water.nominalWidth * .5f);
        minor = Mathf.Max(5f, water.nominalWidth * .36f);
        for (int i = 0; i < count; i++)
        {
            var point = prepared.GetWaterPoint(index, i);
            Vector2 offset = new Vector2(point.x, point.z) - center;
            float radius = Mathf.Max(1f, Mathf.Max(water.nominalWidth, point.width) * .5f);
            major = Mathf.Max(major, Mathf.Abs(Vector2.Dot(offset, axis)) + radius);
            minor = Mathf.Max(minor, Mathf.Abs(Vector2.Dot(offset, side)) + radius);
        }
        major *= .94f; minor *= .94f;
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
            // note: Finite accepted frontier spurs connect to the network without inventing outward terminal roads.
            if (!route.permittedBoundaryContinuation) continue;
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

    internal static bool TryValidateAcceptedDryRouteSegment(YQPreparedSpatialMaterializationV2 prepared,
        Vector2 first, Vector2 second, float clearance, out string failure)
    {
        // note: Exact segment clearance prevents a thin accepted channel from disappearing between the sixteen-metre conformance probes.
        failure = string.Empty;
        if (prepared == null) { failure = "frontier dry route lacks an accepted water context"; return false; }
        for (int index = 0; index < prepared.WaterCount; index++)
        {
            var water = prepared.GetWater(index);
            int count = prepared.GetWaterPointCount(index);
            if (water.connectedBasin != null)
            {
                // note: Route clearance tests the actual contour, so a road on a dry island is possible while a chord crossing wet water is rejected.
                if (water.connectedBasin.SignedShoreDistance(first.x, first.y) < clearance ||
                    water.connectedBasin.SignedShoreDistance(second.x, second.y) < clearance)
                { failure = "frontier route enters connected basin: " + water.hydrologyId; return false; }
                for (int edge = 0; edge < water.connectedBasin.ShoreSegmentCount; edge++)
                {
                    water.connectedBasin.GetShoreSegment(edge, out Vector2 a, out Vector2 b);
                    if (FrontierSegmentDistanceSquared(first, second, a, b) < clearance * clearance)
                    { failure = "frontier route crosses connected shore: " + water.hydrologyId; return false; }
                }
                continue;
            }
            if (count > 0 && (water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland || water.kind == YQHydrologyKindV2.Coastline) &&
                !Clear(prepared.GetWaterAreaCenter(index), prepared.GetWaterAreaCenter(index), Mathf.Max(16f, water.nominalWidth), out failure)) return false;
            if (count == 1)
            {
                var point = prepared.GetWaterPoint(index, 0);
                if (!Clear(new Vector2(point.x, point.z), new Vector2(point.x, point.z), Mathf.Max(water.nominalWidth, point.width), out failure)) return false;
            }
            for (int point = 0; point + 1 < count; point++)
            {
                var a = prepared.GetWaterPoint(index, point); var b = prepared.GetWaterPoint(index, point + 1);
                if (!Clear(new Vector2(a.x, a.z), new Vector2(b.x, b.z), Mathf.Max(water.nominalWidth, Mathf.Max(a.width, b.width)), out failure)) return false;
            }
            if (count < 2 || water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland ||
                water.kind == YQHydrologyKindV2.Coastline || !string.IsNullOrWhiteSpace(water.sinkHydrologyId)) continue;
            for (int end = 0; end < 2; end++)
            {
                var terminal = prepared.GetWaterPoint(index, end == 0 ? 0 : count - 1);
                var adjacent = prepared.GetWaterPoint(index, end == 0 ? 1 : count - 2);
                Vector2 start = new Vector2(terminal.x, terminal.z);
                Vector2 direction = (start - new Vector2(adjacent.x, adjacent.z)).normalized;
                if (!TryGetOutwardBoundaryDistance(start.x, start.y, direction, out _)) continue;
                if (!Clear(start, start + direction * AcceptedContinuationMaxDistance, Mathf.Max(water.nominalWidth, terminal.width), out failure)) return false;
            }

            bool Clear(Vector2 start, Vector2 end, float width, out string reason)
            {
                double bank = Math.Sqrt(FrontierSegmentDistanceSquared(first, second, start, end)) - Mathf.Max(1f, width) * .5f;
                if (double.IsNaN(bank) || bank < clearance)
                { reason = "frontier route requires an unsupported accepted-water crossing or dry bank: " + water.hydrologyId; return false; }
                reason = string.Empty;
                return true;
            }
        }
        return true;
    }

    private static double FrontierSegmentDistanceSquared(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        double abX = (double)b.x - a.x, abZ = (double)b.y - a.y;
        double cdX = (double)d.x - c.x, cdZ = (double)d.y - c.y;
        double offsetX = (double)c.x - a.x, offsetZ = (double)c.y - a.y;
        double denominator = abX * cdZ - abZ * cdX;
        if (Math.Abs(denominator) > .00000001d)
        {
            double t = (offsetX * cdZ - offsetZ * cdX) / denominator;
            double u = (offsetX * abZ - offsetZ * abX) / denominator;
            if (t >= 0d && t <= 1d && u >= 0d && u <= 1d) return 0d;
        }
        return Math.Min(Math.Min(FrontierPointSegmentDistanceSquared(a, c, d), FrontierPointSegmentDistanceSquared(b, c, d)),
            Math.Min(FrontierPointSegmentDistanceSquared(c, a, b), FrontierPointSegmentDistanceSquared(d, a, b)));
    }

    private static double FrontierPointSegmentDistanceSquared(Vector2 point, Vector2 first, Vector2 second)
    {
        double dx = (double)second.x - first.x, dz = (double)second.y - first.y;
        double px = (double)point.x - first.x, pz = (double)point.y - first.y;
        double denominator = dx * dx + dz * dz;
        double t = denominator > .00000001d ? Math.Max(0d, Math.Min(1d, (px * dx + pz * dz) / denominator)) : 0d;
        px -= dx * t; pz -= dz * t;
        return px * px + pz * pz;
    }

    internal static bool TryMeasureAcceptedWaterDistance(YQPreparedSpatialMaterializationV2 prepared,
        float worldX, float worldZ, out string waterId, out float bankDistance, out bool insideWater)
    {
        waterId = string.Empty; bankDistance = float.PositiveInfinity; insideWater = false;
        if (prepared == null) return false;
        Vector2 sample = new Vector2(worldX, worldZ);
        string nearestId = string.Empty;
        float nearest = float.PositiveInfinity;
        bool wet = false;
        bool nativeChannels = prepared.NativeJoinCount > 0;
        // note: Version-7 finite channel exclusions measure the rendered union; distances inside are zero, as for connected lakes. Older accepted versions retain their radial interpretation.
        if (nativeChannels && prepared.TryMeasureMaterializedChannelDistance(worldX, worldZ, out int channelIndex, out float channelDistance))
        {
            nearest = channelDistance; nearestId = prepared.GetWater(channelIndex).hydrologyId;
            // note: One millimetre only absorbs float-coordinate subdivision roundoff at closed banks; it is below the world contract's centimetre tolerance.
            wet = channelDistance <= .001f;
        }
        // note: Measure the same finite channels and boundary-approved terminal rays that carve terrain, without generating cell records or searching Unity objects.
        for (int index = 0; index < prepared.WaterCount; index++)
        {
            var water = prepared.GetWater(index);
            int count = prepared.GetWaterPointCount(index);
            if (water.connectedBasin != null)
            {
                // note: Placement exclusions use the exact shared shore and retain dry island interiors.
                float signed = water.connectedBasin.SignedShoreDistance(worldX, worldZ);
                wet |= signed < 0f;
                if (Mathf.Max(0f, signed) < nearest) { nearest = Mathf.Max(0f, signed); nearestId = water.hydrologyId; }
                continue;
            }
            if (count > 0 && (water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland || water.kind == YQHydrologyKindV2.Coastline))
            {
                // note: Use the existing area-water reserve as well as its boundary spans; a site's dry circle cannot be admitted inside an accepted basin.
                Vector2 center = prepared.GetWaterAreaCenter(index);
                Measure(Vector2.Distance(sample, center), Mathf.Max(16f, water.nominalWidth), water.hydrologyId);
            }
            if (count == 1)
            {
                var point = prepared.GetWaterPoint(index, 0);
                Measure(Vector2.Distance(sample, new Vector2(point.x, point.z)), Mathf.Max(water.nominalWidth, point.width), water.hydrologyId);
            }
            bool indexedChannel = nativeChannels && count >= 2 && (water.kind == YQHydrologyKindV2.River || water.kind == YQHydrologyKindV2.Waterfall);
            for (int point = 0; !indexedChannel && point + 1 < count; point++)
            {
                var first = prepared.GetWaterPoint(index, point); var second = prepared.GetWaterPoint(index, point + 1);
                Vector2 start = new Vector2(first.x, first.z), delta = new Vector2(second.x - first.x, second.z - first.z);
                float t = delta.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(sample - start, delta) / delta.sqrMagnitude) : 0f;
                Measure(Vector2.Distance(sample, start + delta * t), Mathf.Max(water.nominalWidth, Mathf.Max(first.width, second.width)), water.hydrologyId);
            }
            if (count < 2 || water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland ||
                water.kind == YQHydrologyKindV2.Coastline || !string.IsNullOrWhiteSpace(water.sinkHydrologyId)) continue;
            for (int end = 0; end < 2; end++)
            {
                var terminal = prepared.GetWaterPoint(index, end == 0 ? 0 : count - 1);
                var adjacent = prepared.GetWaterPoint(index, end == 0 ? 1 : count - 2);
                Vector2 start = new Vector2(terminal.x, terminal.z);
                Vector2 direction = new Vector2(terminal.x - adjacent.x, terminal.z - adjacent.z).normalized;
                if (!TryGetOutwardBoundaryDistance(start.x, start.y, direction, out _)) continue;
                float along = Mathf.Clamp(Vector2.Dot(sample - start, direction), 0f, AcceptedContinuationMaxDistance);
                Measure(Vector2.Distance(sample, start + direction * along), Mathf.Max(water.nominalWidth, terminal.width), water.hydrologyId);
            }
        }
        waterId = nearestId; bankDistance = nearest; insideWater = wet;
        return !float.IsPositiveInfinity(nearest);

        void Measure(float distance, float width, string id)
        {
            float bank = distance - Mathf.Max(1f, width) * .5f;
            wet |= bank <= 0f;
            if (bank < nearest || bank == nearest && string.CompareOrdinal(id, nearestId) < 0)
            { nearest = bank; nearestId = id; }
        }
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
                        width = Mathf.Max(1f, Mathf.Max(water.nominalWidth, endpoint.width)),
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
            // note: Preserve opening boundary continuation while keeping published frontier access corridors finite.
            if (!route.permittedBoundaryContinuation) continue;
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
                    width = Mathf.Max(1f, Mathf.Max(water.nominalWidth, endpoint.width)),
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

    internal static bool TryGetOutwardBoundaryDistance(float x, float z, Vector2 direction, out float distance)
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
        out float modifiedHeight,
        bool wetOnly = false)
    {
        var envelope = new AcceptedWaterTerrainEnvelope();
        AccumulateAcceptedFiniteWater(prepared, selectedWaterIndex, worldX, worldZ, terrainHeight, wetOnly, ref envelope);
        modifiedHeight = envelope.Project(normalizedHeight);
        return envelope.Influenced;
    }

    private static void AccumulateAcceptedFiniteWater(YQPreparedSpatialMaterializationV2 prepared,
        int selectedWaterIndex, float worldX, float worldZ, float terrainHeight, bool wetOnly,
        ref AcceptedWaterTerrainEnvelope envelope)
    {
        if (prepared == null)
            return;
        int firstWaterIndex = selectedWaterIndex >= 0 ? selectedWaterIndex : 0;
        int lastWaterIndex = selectedWaterIndex >= 0 ? selectedWaterIndex + 1 : prepared.WaterCount;
        for (int waterIndex = firstWaterIndex; waterIndex < lastWaterIndex; waterIndex++)
        {
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            // note: Distant continuation terrain no longer samples the origin basin graph; accepted area water must carve its own shared submerged footprint.
            if (water.connectedBasin != null)
            {
                // note: Terrain modifiers retain the immutable height-source projection. Reapplying the rendered native interpolation here would change off-grid terrain and then change the next native water boundary.
                var sourceBasin = prepared.GetTerrainSourceBasin(waterIndex);
                if (sourceBasin.TrySampleBed(worldX, worldZ, out float bed, out bool inside) &&
                    (inside || (!wetOnly && sourceBasin.SignedShoreDistance(worldX, worldZ) <= AcceptedWaterBankPadding + 2f)))
                    envelope.AddConnectedBed(bed);
                continue;
            }
            if (water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland || water.kind == YQHydrologyKindV2.Coastline)
            {
                if (prepared.GetWaterPointCount(waterIndex) == 0) continue;
                GetAcceptedAreaWaterFootprint(prepared, waterIndex, out Vector2 center, out Vector2 axis, out float major, out float minor);
                Vector2 offset = new Vector2(worldX, worldZ) - center;
                float radius = Mathf.Min(major, minor);
                float u = Vector2.Dot(offset, axis) / major;
                float v = Vector2.Dot(offset, new Vector2(-axis.y, axis.x)) / minor;
                float distance = Mathf.Sqrt(u * u + v * v) * radius;
                if (wetOnly && distance > radius + AcceptedWaterBankPadding + 2f) continue;
                float surface = water.waterLevelNormalized > 0f ? water.waterLevelNormalized : prepared.GetWaterPoint(waterIndex, 0).waterSurfaceNormalized;
                envelope.Add(terrainHeight, surface, water.nominalDepth, radius, distance);
                continue;
            }
            float nearestClearance = float.PositiveInfinity, nearestDistance = 0f, nearestWidth = 0f, nearestSurface = 0f;
            if (prepared.UsesSharedChannelProjection && (water.kind == YQHydrologyKindV2.River || water.kind == YQHydrologyKindV2.Waterfall))
            {
                // note: New accepted domains share the finite sampler's exact footprint and receiving-level transition. Persisted older domains keep their historical centreline projection below.
                prepared.AccumulateAcceptedChannelEnvelope(waterIndex, worldX, worldZ, terrainHeight, wetOnly, ref envelope);
                continue;
            }
            for (int pointIndex = 0; pointIndex + 1 < prepared.GetWaterPointCount(waterIndex); pointIndex++)
            {
                YQSpatialMaterializationWaterPointV2 first = prepared.GetWaterPoint(waterIndex, pointIndex);
                YQSpatialMaterializationWaterPointV2 second = prepared.GetWaterPoint(waterIndex, pointIndex + 1);
                // note: Reject samples outside a conservative segment AABB before running closest-point math across thousands of accepted river segments.
                float maximumSegmentWidth = Mathf.Max(water.nominalWidth, Mathf.Max(first.width, second.width));
                float influenceRadius = Mathf.Max(1f, maximumSegmentWidth * 0.5f) + AcceptedWaterBankPadding + AcceptedTerrainTransitionDistance;
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
                if (distance >= halfWidth + AcceptedWaterBankPadding + AcceptedTerrainTransitionDistance)
                    continue;
                bool isCore = distance <= halfWidth + AcceptedWaterBankPadding + 2f;
                if (wetOnly && !isCore) continue;
                float clearance = distance - halfWidth;
                float surface = Mathf.Lerp(first.waterSurfaceNormalized, second.waterSurfaceNormalized, t);
                // note: Each accepted water contributes its nearest footprint once; segment count must not bias an overlapping bank's influence.
                if (clearance > nearestClearance || (clearance == nearestClearance && surface >= nearestSurface)) continue;
                nearestClearance = clearance; nearestDistance = distance; nearestWidth = halfWidth; nearestSurface = surface;
            }
            if (!float.IsPositiveInfinity(nearestClearance))
                envelope.Add(terrainHeight, nearestSurface, water.nominalDepth, nearestWidth, nearestDistance);
        }
    }

    internal static bool TryApplyAcceptedWaterTerminalModifiers(
        YQPreparedSpatialMaterializationV2 prepared,
        float worldX,
        float worldZ,
        float normalizedHeight,
        float terrainHeight,
        out float modifiedHeight)
    {
        return TryApplyAcceptedWaterTerminalProfile(prepared, worldX, worldZ, normalizedHeight, terrainHeight, out modifiedHeight, false);
    }

    internal static bool TryApplyAcceptedWaterCoreModifiers(YQPreparedSpatialMaterializationV2 prepared,
        float worldX, float worldZ, float originalHeight, float terrainHeight, out float modifiedHeight)
    {
        // note: Wet cores own the accepted channel datum before any wider dry terrain transition is considered.
        bool finite = TryApplyAcceptedWaterModifier(prepared, -1, worldX, worldZ, originalHeight, terrainHeight, out float first, true);
        bool terminal = TryApplyAcceptedWaterTerminalProfile(prepared, worldX, worldZ, originalHeight, terrainHeight, out float second, true);
        modifiedHeight = finite && terminal ? Mathf.Min(first, second) : finite ? first : second;
        return finite || terminal;
    }

    internal static bool TryApplyAcceptedWaterTerrainModifiers(YQPreparedSpatialMaterializationV2 prepared,
        float worldX, float worldZ, float originalHeight, float terrainHeight, out float modifiedHeight, bool includeFinite = true)
    {
        // note: Finite rivers and terminal continuations share one terrain envelope; independently taking their lower bank caused a cliff immediately outside the higher wet core.
        var envelope = new AcceptedWaterTerrainEnvelope();
        if (includeFinite) AccumulateAcceptedFiniteWater(prepared, -1, worldX, worldZ, terrainHeight, false, ref envelope);
        AccumulateAcceptedTerminalWater(prepared, worldX, worldZ, terrainHeight, false, ref envelope);
        modifiedHeight = envelope.Project(originalHeight);
        return envelope.Influenced;
    }

    private static bool TryApplyAcceptedWaterTerminalProfile(YQPreparedSpatialMaterializationV2 prepared,
        float worldX, float worldZ, float normalizedHeight, float terrainHeight, out float modifiedHeight, bool wetOnly)
    {
        var envelope = new AcceptedWaterTerrainEnvelope();
        AccumulateAcceptedTerminalWater(prepared, worldX, worldZ, terrainHeight, wetOnly, ref envelope);
        modifiedHeight = envelope.Project(normalizedHeight);
        return envelope.Influenced;
    }

    private static void AccumulateAcceptedTerminalWater(YQPreparedSpatialMaterializationV2 prepared,
        float worldX, float worldZ, float terrainHeight, bool wetOnly, ref AcceptedWaterTerrainEnvelope envelope)
    {
        // note: Free-flowing accepted endpoints share one bounded world-space ray evaluator; lakes, wetlands, coastlines, and sink-linked rivers stop at their accepted area/receiver.
        if (prepared == null)
            return;
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
                float halfWidth = Mathf.Max(1f, Mathf.Max(water.nominalWidth, endpoint.width) * 0.5f);
                if (distance >= halfWidth + AcceptedWaterBankPadding + AcceptedTerrainTransitionDistance)
                    continue;
                bool isCore = prepared.UsesCanonicalWetFootprint ? distance <= halfWidth + .001f : distance <= halfWidth + AcceptedWaterBankPadding + 2f;
                if (wetOnly && !isCore) continue;
                float segmentLength = Mathf.Max(0.001f, Vector2.Distance(new Vector2(endpoint.x, endpoint.z), new Vector2(adjacent.x, adjacent.z)));
                float slope = (endpoint.waterSurfaceNormalized - adjacent.waterSurfaceNormalized) / segmentLength;
                // note: Keep terminal candidates order-independent by comparing each one against the original unmodified sample.
                if (prepared.UsesCanonicalWetFootprint)
                    envelope.AddCanonicalChannel(terrainHeight, Mathf.Clamp01(endpoint.waterSurfaceNormalized + slope * along),
                        water.nominalDepth, halfWidth, distance, Mathf.Max(0f, distance - halfWidth));
                else
                    envelope.Add(terrainHeight,
                        Mathf.Clamp01(endpoint.waterSurfaceNormalized + slope * along),
                        water.nominalDepth, halfWidth, distance);
            }
        }
    }

    internal struct AcceptedWaterTerrainEnvelope
    {
        private bool wet;
        private float wetHeight;
        private double weightSum, lowerSum, upperSum;
        internal bool Influenced => wet || weightSum > 0d;
        internal bool HasWetCore => wet;

        internal void AddConnectedBed(float bed)
        {
            // note: Project an accepted basin's actual geological surface, including its dry shore, without recarving it as a circular channel.
            wetHeight = wet ? Mathf.Min(wetHeight, bed) : bed;
            wet = true;
        }

        internal void Add(float terrainHeight, float surface, float depth, float halfWidth, float distance)
        {
            float clearance = distance - halfWidth - AcceptedWaterBankPadding - 2f;
            if (clearance <= 0f)
            {
                float bed = SampleAcceptedWaterBed(terrainHeight, surface, depth, halfWidth, distance);
                wetHeight = wet ? Mathf.Min(wetHeight, bed) : bed;
                wet = true;
                return;
            }
            if (clearance >= AcceptedTerrainTransitionDistance - 2f) return;
            // note: Project into a distance-weighted interval, rather than blending successive height edits. The projection is idempotent and approaches each wet bank continuously, even beside a lower river.
            double weight = 1d / (clearance * clearance * clearance * clearance);
            float bank = surface + .2f / Mathf.Max(1f, terrainHeight);
            float allowance = clearance * .45f / Mathf.Max(1f, terrainHeight);
            weightSum += weight;
            lowerSum += weight * (bank - allowance);
            upperSum += weight * (bank + allowance);
        }

        internal void AddCanonicalChannel(float terrainHeight, float surface, float depth, float halfWidth, float distance, float outside)
        {
            // note: New drafts use actual ribbon coverage, including finite endpoint caps. Bed depth vanishes at lateral banks; native resampling gaps remain a separate physical verification gate.
            if (outside <= .001f)
            {
                float across = Mathf.Clamp01(distance / Mathf.Max(.1f, halfWidth));
                float bed = surface - Mathf.Max(.1f, depth) * (1f - across * across) / Mathf.Max(1f, terrainHeight);
                wetHeight = wet ? Mathf.Min(wetHeight, bed) : bed;
                wet = true;
                return;
            }
            if (outside >= AcceptedTerrainTransitionDistance) return;
            // note: Dry shoulders stay at or above the channel datum and meet the zero-depth bank continuously; their constrained interval remains idempotent.
            double weight = 1d / (outside * outside * outside * outside);
            float allowance = outside * .45f / Mathf.Max(1f, terrainHeight);
            weightSum += weight;
            lowerSum += weight * surface;
            upperSum += weight * (surface + allowance);
        }

        internal float Project(float original)
        {
            if (wet) return Mathf.Clamp01(wetHeight);
            return weightSum > 0d ? Mathf.Clamp01(Mathf.Clamp(original, (float)(lowerSum / weightSum), (float)(upperSum / weightSum))) : original;
        }
    }

    // note: Support the complete rendered width plus a heightmap-cell diagonal so interpolation cannot leave terrain crests inside the water ribbon.
    private const float AcceptedWaterBankPadding = YQGeneratedWorldTerrain.WorldSize /
        (YQGeneratedWorldTerrain.HeightmapResolution - 1f) * 1.414214f;

    // note: This is a terrain transition envelope, not a larger road or water footprint. Accepted dimensions and semantic masks stay unchanged.
    internal const float AcceptedTerrainTransitionDistance = 384f;

    internal static float SampleAcceptedRouteGrade(float originalHeight, float routeHeight,
        float terrainHeight, float halfWidth, float shoulderWidth, float distance)
    {
        // note: Cut and fill share one idempotent grade profile in the accepted sampler, continuation, and restored origin terrain.
        float core = Mathf.Max(1f, halfWidth + shoulderWidth);
        if (distance >= core + AcceptedTerrainTransitionDistance) return originalHeight;
        float allowance = Mathf.Max(0f, distance - core) * 0.45f / Mathf.Max(1f, terrainHeight);
        return Mathf.Clamp(originalHeight, routeHeight - allowance, routeHeight + allowance);
    }

    internal static float SampleAcceptedWaterCarve(float originalHeight, float terrainHeight,
        float surfaceNormalized, float depth, float halfWidth, float distance)
    {
        float wetRadius = halfWidth + AcceptedWaterBankPadding;
        // note: The bed stays submerged across the entire wet footprint; only the dry shoulder blends with the original mountain/hill height.
        float bed = SampleAcceptedWaterBed(terrainHeight, surfaceNormalized, depth, halfWidth, distance);
        if (distance >= wetRadius + AcceptedTerrainTransitionDistance) return originalHeight;
        // note: A bounded natural bank grade replaces the narrow cut/fill blend that made levees and trenches. Reapplying it cannot deepen a restored channel.
        float allowance = Mathf.Max(0f, distance - wetRadius - 2f) * 0.45f / Mathf.Max(1f, terrainHeight);
        return Mathf.Clamp01(Mathf.Clamp(originalHeight, bed - allowance, bed + allowance));
    }

    internal static float SampleAcceptedWaterBed(float terrainHeight, float surfaceNormalized,
        float depth, float halfWidth, float distance)
    {
        // note: Origin and continuation terrain share the submerged cross-section; terrain resolution padding protects the actual rendered banks.
        float crossSection = Mathf.Clamp01(distance / Mathf.Max(0.1f, halfWidth));
        // note: Preserve full centre depth, shallow rendered edges, and dry-bank support beyond heightmap interpolation padding.
        float shoreDepth = Mathf.Min(0.12f, Mathf.Max(0.1f, depth));
        float wetBed = -Mathf.Lerp(Mathf.Max(0.1f, depth), shoreDepth, crossSection * crossSection);
        float bankBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
            halfWidth + AcceptedWaterBankPadding, halfWidth + AcceptedWaterBankPadding + 2f, distance));
        return surfaceNormalized + Mathf.Lerp(wetBed, 0.2f, bankBlend) / Mathf.Max(1f, terrainHeight);
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
    // note: This static materializer owns every prepared biome task, including records removed before their Dispose callbacks finish.
    private static readonly YQOwnedWorkerLifetimeObservation BiomeWorkerLifetime = new YQOwnedWorkerLifetimeObservation(256);
    private static readonly object BiomeWorkerLifetimeOwner = new object();
    public static YQWorkerLifetimeSnapshot CaptureBiomeWorkerLifetime() => BiomeWorkerLifetime.Capture();

    // note: Attribute each accepted road or river mesh separately so a slow per-cell ribbon build can be sized without changing its geometry.
    private static ProfilerMarker RibbonMeshBuildMarker => RibbonProfiler.Marker;
    private static class RibbonProfiler
    {
        // note: Pure immutable geometry queries can initialize this owner on a terrain worker or detached verifier without initializing Unity profiling APIs.
        internal static readonly ProfilerMarker Marker = new ProfilerMarker("YQContinuousWorldFeatureMaterializer.BuildRibbon");
    }

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
                BiomeWorkerLifetime.Observe(BiomeWorkerLifetimeOwner, true, () => task.ContinueWith(_ => cancellation.Dispose(), TaskScheduler.Default));
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
                // note: The immutable accepted route projection also owns streamed terrain path grading.
                float roadWeight = Array.IndexOf(bindings, -1) >= 0 ? authority.SampleRoadPaintWeight(worldX, worldZ) : 0f;
                float total = 0f;
                for (int layer = 0; layer < layerCount; layer++)
                {
                    // note: A road layer cannot become the default grassland layer across an entire continuation tile.
                    float value = bindings[layer] < 0 ? roadWeight : (1f - roadWeight) * (bindings[layer] == 0 ? forest : bindings[layer] == 1 ? grassland : wetland);
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

        return BeginBiomeAlphamapPreparation(
            data.terrainLayers,
            data.alphamapResolution,
            authority,
            coordinate,
            cellSize);
    }

    // note: Let small provisional tiles reuse deterministic biome sampling without allocating TerrainData before their height sample finishes.
    internal static PreparedBiomeAlphamap BeginBiomeAlphamapPreparation(
        TerrainLayer[] layers,
        int alphamapResolution,
        YQContinuousWorldCellAuthority authority,
        Vector2Int coordinate,
        float cellSize)
    {
        if (layers == null || layers.Length == 0 || authority == null)
            return null;

        int resolution = Mathf.Max(1, alphamapResolution);
        int[] bindings = new int[layers.Length];
        for (int layer = 0; layer < layers.Length; layer++)
        {
            string name = layers[layer] != null ? (layers[layer].name ?? string.Empty).ToLowerInvariant() : string.Empty;
            bindings[layer] = name.EndsWith("_livedpath", StringComparison.Ordinal) ? -1 :
                name.Contains("wet") || name.Contains("marsh") || name.Contains("swamp") ? 2 :
                name.Contains("forest") || name.Contains("wood") || name.Contains("moss") ? 0 : 1;
        }

        CancellationTokenSource cancellation = new CancellationTokenSource();
        try
        {
            CancellationToken cancellationToken = cancellation.Token;
            Task<BiomeAlphamapBuildResult> task = BiomeWorkerLifetime.Observe(BiomeWorkerLifetimeOwner, false, () => Task.Run(
                () => BuildBiomeAlphamaps(authority, coordinate, cellSize, resolution, bindings, cancellationToken),
                cancellationToken));
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
            bindings[layer] = name.EndsWith("_livedpath", StringComparison.Ordinal) ? -1 : name.Contains("wet") || name.Contains("marsh") || name.Contains("swamp") ? 2 :
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
                // note: The immutable accepted route projection also owns streamed terrain path grading.
                float roadWeight = Array.IndexOf(bindings, -1) >= 0 ? authority.SampleRoadPaintWeight(worldX, worldZ) : 0f;
                    float total = 0f;
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        float value = bindings[layer] < 0 ? roadWeight : (1f - roadWeight) * (bindings[layer] == 0 ? forest : bindings[layer] == 1 ? grassland : wetland);
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
        return BuildCellRoutineCore(parent, terrain, plan, chunk, palette, registry, cellSize,
            materializeContinuationFeatures, completed, substageTelemetry, preparedResolved, preparedOverride, null);
    }

    // note: Adopted geometry retains its existing owner, while every missing accepted category completes through the canonical builder. A prop cannot certify a missing site, road or bridge.
    internal static IEnumerator BuildAcceptedWaterCompletionRoutine(
        Transform parent, Terrain terrain, GeneratedWorldPlanRecord plan, GeneratedSemanticChunkRecord chunk,
        GeneratedRegionAssetPaletteRecord palette, YQRuntimeWorldAssetRegistry registry, float cellSize,
        Action<int> completed, Action<string, float> substageTelemetry,
        Action<YQPreparedSpatialMaterializationV2> preparedResolved, YQPreparedSpatialMaterializationV2 preparedOverride,
        HashSet<string> retainedSiteIds = null)
    {
        return BuildCellRoutineCore(parent, terrain, plan, chunk, palette, registry, cellSize,
            true, completed, substageTelemetry, preparedResolved, preparedOverride, retainedSiteIds);
    }

    internal static IEnumerator BuildMissingAdoptedSitesRoutine(
        Transform parent, Terrain terrain, GeneratedWorldPlanRecord plan, GeneratedSemanticChunkRecord chunk,
        GeneratedRegionAssetPaletteRecord palette, YQRuntimeWorldAssetRegistry registry, float cellSize,
        YQPreparedSpatialMaterializationV2 prepared, HashSet<string> retainedSiteIds, Action<int> completed)
    {
        // note: Borrowed scenery and completed water do not certify absent accepted sites. Reuse the canonical site builder while retaining only explicitly owned, current physical site identities.
        int built = 0;
        List<string> materialized = null;
        Vector2Int coordinate = new Vector2Int(chunk.chunkX, chunk.chunkZ);
        // note: Most water cells own no site; avoid palette preparation and a new construction lane for those cells.
        if (!HasAcceptedSiteOwnerDemand(prepared, chunk, coordinate, cellSize))
        {
            completed?.Invoke(0);
            yield break;
        }
        yield return BuildAcceptedSiteRoutine(parent, terrain, plan, chunk, palette, registry, coordinate,
            cellSize, (count, ids) => { built = count; materialized = ids; }, prepared, retainedSiteIds);
        bool missing = HasMissingRequiredAcceptedSiteOwner(prepared, chunk, coordinate, cellSize, materialized);
        if (missing)
            Debug.LogError("[YQContinuousWorldFeatures] ADOPTED CELL SITE COMPLETION FAILED " + coordinate);
        completed?.Invoke(missing ? -1 : built);
    }

    private static IEnumerator BuildCellRoutineCore(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticChunkRecord chunk,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        float cellSize,
        bool materializeContinuationFeatures,
        Action<int> completed,
        Action<string, float> substageTelemetry,
        Action<YQPreparedSpatialMaterializationV2> preparedResolved,
        YQPreparedSpatialMaterializationV2 preparedOverride, HashSet<string> retainedSiteIds)
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
        // note: Retain each published hydrology owner independently; one missing lake or fall must not rebuild all tutorial water above its existing river.
        var retainedWaterIndices = new HashSet<int>();
        int retainedRiverObjects = 0, retainedAreaWaterObjects = 0;
        if (preparedWater != null)
        {
            var intersectingWater = new List<int>();
            YQContinuousWorldFeatureAuthority.GetAcceptedWaterIndicesForCell(preparedWater, cellCoordinate, cellSize, intersectingWater);
            foreach (int index in intersectingWater)
                if (HasPublishedAuthoredWaterCoverage(parent, terrain, preparedWater, cellCoordinate, cellSize, index))
                {
                    retainedWaterIndices.Add(index);
                    var kind = preparedWater.GetWater(index).kind;
                    if (kind == YQHydrologyKindV2.Lake || kind == YQHydrologyKindV2.Wetland || kind == YQHydrologyKindV2.Coastline)
                        retainedAreaWaterObjects++;
                    else retainedRiverObjects++;
                }
        }
        var riverBankOffsets = preparedWater != null && preparedWater.UsesCanonicalChannelBanks ? new List<Vector3>() : null;
        bool pendingRiver = TryGetAcceptedRiverPointsCore(preparedWater, cellCoordinate, cellSize, terrain,
            out List<Vector3> river, out float riverWidth, out List<int> riverBreaks, out List<float> riverPointWidths, retainedWaterIndices, riverBankOffsets);
        bool pendingNativeJoin = HasNativeChannelJoinDemand(preparedWater, terrain, retainedWaterIndices);
        bool hasAcceptedRiver = pendingRiver || pendingNativeJoin || retainedRiverObjects > 0;
        // note: Planned worlds materialize only authoritative water crossings; the legacy fallback remains isolated for unplanned editor callers.
        int riverObjects = retainedRiverObjects;
        if (!hasAcceptedRiver && allowLegacySyntheticFallback)
        {
            hasAcceptedRiver = YQContinuousWorldFeatureAuthority.TryGetRiverPoints(plan.worldSeed, new Vector2Int(chunk.chunkX, chunk.chunkZ), cellSize, terrain, out river, out riverWidth);
            pendingRiver = hasAcceptedRiver;
            riverBreaks = null;
            riverPointWidths = null;
        }
        RecordSubstage(substageTelemetry, "riverPointExtraction", substageStarted);
        // note: Separate accepted point extraction from mesh construction so a contract lookup cannot share one frame with a large ribbon build.
        yield return null;
        if (pendingRiver || pendingNativeJoin)
        {
            substageStarted = Time.realtimeSinceStartup;
            Material waterMaterial = FindMaterial(registry, "water", "river") ?? BuildFallbackWaterMaterial();
            RegisterCellResource(parent, waterMaterial);
            int newRiverObjects = 0, joinObjects = 0;
            // note: Streamed ribbons and native channel patches share the existing cooperative mesh kernel; no complete water rebuild monopolizes one gameplay frame.
            if (pendingRiver)
                yield return new MeasuredWaterRoutine(BuildRibbonSpansRoutine(parent, "ContinuousRiver_" + chunk.chunkX + "_" + chunk.chunkZ, river, riverBreaks, riverWidth, waterMaterial, true, cellSize, riverPointWidths, terrain, riverBankOffsets, preparedWater, value => newRiverObjects = value, true), substageTelemetry);
            yield return new MeasuredWaterRoutine(BuildNativeChannelJoinsRoutine(parent, preparedWater, terrain, waterMaterial, retainedWaterIndices, value => joinObjects = value, true), substageTelemetry);
            newRiverObjects += joinObjects;
            riverObjects += newRiverObjects;
            if (newRiverObjects > 0)
            {
                built += newRiverObjects;
                yield return null;
            }
        }
        bool hasAcceptedAreaWater = false;
        int expectedAreaWaterObjects = 0;
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
                    expectedAreaWaterObjects++;
                }
            }
            RecordSubstage(substageTelemetry, "waterCellIntersection", substageStarted);
        }
        int areaWaterObjects = retainedAreaWaterObjects;
        if (hasAcceptedAreaWater && retainedAreaWaterObjects < expectedAreaWaterObjects)
        {
            // note: Give the scheduler a frame boundary before the grid-based area-water mesh allocates vertices and normals.
            yield return null;
            substageStarted = Time.realtimeSinceStartup;
            Material areaWaterMaterial = FindMaterial(registry, "water", "lake", "wetland") ?? BuildFallbackWaterMaterial();
            if (areaWaterMaterial != null && areaWaterMaterial.HasProperty("_FlowSpeed"))
                areaWaterMaterial.SetFloat("_FlowSpeed", 0.012f);
            RegisterCellResource(parent, areaWaterMaterial);
            int newAreaWaterObjects = BuildAcceptedAreaWaterPatchesCore(
                parent,
                terrain,
                preparedWater,
                new Vector2Int(chunk.chunkX, chunk.chunkZ),
                cellSize,
                areaWaterMaterial, retainedWaterIndices);
            areaWaterObjects += newAreaWaterObjects;
            RecordSubstage(substageTelemetry, "areaWaterPatchBuild", substageStarted);
            if (newAreaWaterObjects > 0)
            {
                built += newAreaWaterObjects;
                yield return null;
            }
        }

        // note: A river cannot mask a missing lake clip; all accepted categories retain their independent completion gates.
        bool waterComplete = areaWaterObjects == expectedAreaWaterObjects &&
            (!acceptedWaterDemand || riverObjects + areaWaterObjects > 0);
        // note: Finish site grounding before road sampling so foundations cannot bury this cell's published access paths.
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
            preparedWater, retainedSiteIds);
        built += siteObjects;

        // note: Resolve owner-cell decks before extracting road ribbons so both presentations use the same accepted route interval.
        List<AcceptedCrossingSpan> crossingSpans = new List<AcceptedCrossingSpan>();
        yield return PrepareAcceptedCrossingSpansRoutine(preparedWater, terrain, cellCoordinate, cellSize, crossingSpans, substageTelemetry);
        // note: A retired terrain cancels its transaction; it must never publish success after an asynchronous bridge search.
        if (parent == null || terrain == null || terrain.terrainData == null) { completed?.Invoke(-1); yield break; }
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
        if (parent == null || terrain == null || terrain.terrainData == null) { completed?.Invoke(-1); yield break; }
        int roadObjects = 0;
        int crossingObjects = 0;
        // note: Terrain paint is already the production path surface. Verified painted routes count as realized, instead of drawing a second straight strip over the same geography.
        bool retainedRoadPaint = hasAcceptedRoad && YQGeneratedWorldEnvironment.TryCountPublishedLivedRoutePaint(
            plan, terrain, preparedWater, cellCoordinate, cellSize, out roadObjects);
        if (hasAcceptedRoad && !retainedRoadPaint)
        {
            substageStarted = Time.realtimeSinceStartup;
            Material roadMaterial = FindPaletteMaterial(palette, registry, YQWorldAssetCatalog.SlotPath, plan.worldSeed + "|continuous-road|" + chunk.chunkX + "|" + chunk.chunkZ);
            roadMaterial = roadMaterial ?? BuildFallbackRoadMaterial();
            BindGroundedRoadSurface(roadMaterial, terrain);
            RegisterCellResource(parent, roadMaterial);
            roadObjects = BuildRibbonSpans(parent, "ContinuousRoad_" + chunk.chunkX + "_" + chunk.chunkZ, road, roadBreaks, roadWidth, roadMaterial, false, cellSize, roadPointWidths, terrain);
            RecordSubstage(substageTelemetry, "roadRibbonBuild", substageStarted);
            if (roadObjects > 0)
            {
                built += roadObjects;
                yield return null;
            }
        }
        // note: A deck may replace every road sample in its owner cell; it must still build independently of the remaining ordinary ribbon.
        yield return BuildAcceptedCrossingsRoutine(parent, crossingSpans, registry, substageTelemetry, count => crossingObjects = count, terrain, preparedWater);
        built += crossingObjects;

        // note: Accepted feature categories publish independently; one successful site must not hide a missing route or water realization in the same cell.
        // note: A failed accepted category remains a hard publication failure even when an unrelated category produced a positive object count.
        if (!waterComplete ||
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

        // note: Site owners are checked independently so a road or water object cannot mask a missing accepted settlement, cave, hostile site, or POI.
        if (acceptedSiteDemand && siteObjects <= 0 && materializedSiteIds.Count == 0 && !HasExistingAcceptedSiteOwner(preparedWater, cellCoordinate, cellSize))
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

        // note: Completion counts verified retained coverage as well as new geometry. Painted roads and
        // authored water already passed the same category gates; reporting zero would reject those valid cells.
        built += retainedRiverObjects + retainedAreaWaterObjects + (retainedRoadPaint ? roadObjects : 0);
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
        // note: The index includes shoulders and seam margins. Physical mesh demand uses the accepted paved footprint, preserving the wider semantic reservations.
        foreach (int routeIndex in routeIndices)
        {
            var route = prepared.GetRoute(routeIndex);
            int first = 0, last = prepared.GetRoutePointCount(routeIndex) - 2;
            if (prepared.TryGetRouteSegmentRangeForCell(routeIndex, coordinate, cellSize, out int indexedFirst, out int indexedLast))
            { first = indexedFirst; last = indexedLast; }
            for (int index = first; index <= last; index++)
            {
                var a = prepared.GetRoutePoint(routeIndex, index);
                var b = prepared.GetRoutePoint(routeIndex, index + 1);
                if (RoadFootprintIntersectsCell(new Vector2(a.x, a.z), new Vector2(b.x, b.z), route.width, coordinate, cellSize))
                    return true;
            }
        }
        List<YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation> continuations =
            new List<YQContinuousWorldFeatureAuthority.AcceptedTerminalContinuation>();
        YQContinuousWorldFeatureAuthority.GetAcceptedRouteContinuations(prepared, coordinate, cellSize, 6f, continuations);
        foreach (var continuation in continuations)
            if (RoadFootprintIntersectsCell(continuation.origin,
                continuation.origin + continuation.direction * YQContinuousWorldFeatureAuthority.AcceptedContinuationMaxDistance,
                continuation.width, coordinate, cellSize))
                return true;
        return false;
    }

    private static bool RoadFootprintIntersectsCell(Vector2 first, Vector2 second, float width, Vector2Int coordinate, float cellSize)
    {
        // note: Four separating axes test the actual ribbon rectangle against the cell, including diagonal corner cases that padded bounds over-admit.
        Vector2 delta = second - first;
        float length = delta.magnitude;
        if (length < 0.001f || width <= 0f) return false;
        Vector2 direction = delta / length, side = new Vector2(-direction.y, direction.x);
        float halfCell = cellSize * 0.5f, halfWidth = width * 0.5f;
        Vector2 cellCenter = new Vector2(WorldGridOrigin + coordinate.x * cellSize + halfCell,
            WorldGridOrigin + coordinate.y * cellSize + halfCell);
        Vector2 distance = (first + second) * 0.5f - cellCenter;
        return Mathf.Abs(distance.x) < halfCell + Mathf.Abs(delta.x) * 0.5f + Mathf.Abs(side.x) * halfWidth &&
            Mathf.Abs(distance.y) < halfCell + Mathf.Abs(delta.y) * 0.5f + Mathf.Abs(side.y) * halfWidth &&
            Mathf.Abs(Vector2.Dot(distance, direction)) < length * 0.5f + halfCell * (Mathf.Abs(direction.x) + Mathf.Abs(direction.y)) &&
            Mathf.Abs(Vector2.Dot(distance, side)) < halfWidth + halfCell * (Mathf.Abs(side.x) + Mathf.Abs(side.y));
    }

    internal static bool HasAcceptedWaterDemand(
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
            if (!IsPhysicalRiverCrossing(crossing.kind))
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
                !(site.kind == YQSiteKindV2.HostileSite ||
                  site.kind == YQSiteKindV2.PointOfInterest ||
                  RequiresReviewedPhysicalSite(site)))
                continue;
            // note: Every accepted hostile site and POI needs its own physical success; another site in the same cell cannot hide a failed untagged owner.
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
        YQPreparedSpatialMaterializationV2 preparedOverride = null,
        HashSet<string> retainedSiteIds = null)
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

            // note: This receipt comes from the streamer's retained physical hierarchy, not feature metadata or a marker; missing sites still take their ordinary accepted materialization path.
            if (retainedSiteIds != null && retainedSiteIds.Contains(site.siteId))
            {
                materializedSiteIds.Add(site.siteId);
                continue;
            }

            // note: Only opening anchors retain the historical recovery paths; a continued site must replay its exact persisted reviewed provider.
            bool continuationSite = !IsBaseAcceptedSite(plan, site);
            if (continuationSite && !TryResolveAcceptedContinuationSiteContext(plan, site,
                    out _, out _, out string continuationFailure))
            {
                Debug.LogError("[YQContinuousWorldFeatures] ACCEPTED CONTINUATION REJECTED " + site.siteId + ": " + continuationFailure);
                continue;
            }

            string existingSiteLocationId = string.Empty;
            if (YQCompiledWorldSiteInstance.HasSite(site.sourceSemanticId))
                existingSiteLocationId = site.sourceSemanticId;
            else if (YQCompiledWorldSiteInstance.HasSite(site.siteId))
                existingSiteLocationId = site.siteId;

            if (!string.IsNullOrWhiteSpace(existingSiteLocationId))
            {
                // note: A prepared site root is only an owner registration; load it through its existing bounded stream instead of treating registration as physical publication.
                bool existingSiteLoaded = false;
                YQCompiledWorldSiteInstance.StagedSiteGeometryContext stagedGeometry = null;
                yield return YQCompiledWorldSiteInstance.EnsureSiteGeometryLoadedForStagingRoutine(
                    existingSiteLocationId, parent, plan,
                    (ready, context) => { existingSiteLoaded = ready; stagedGeometry = context; });
                if (continuationSite)
                {
                    // note: Registration or load success alone cannot admit a provider bound to a different saved kit, composition or region.
                    existingSiteLoaded = existingSiteLoaded &&
                        TryGetAcceptedContinuationLoadedProvider(plan, site, out _, out _, stagedGeometry);
                    if (existingSiteLoaded)
                    {
                        built++;
                        materializedSiteIds.Add(site.siteId);
                    }
                    else Debug.LogError("[YQContinuousWorldFeatures] ACCEPTED CONTINUATION PROVIDER REJECTED " + site.siteId);
                    continue;
                }
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

            if (continuationSite)
            {
                // note: A missing or rejected committed provider is a publication failure, never permission to substitute a palette shell or scatter site.
                Debug.LogError("[YQContinuousWorldFeatures] ACCEPTED CONTINUATION PHYSICAL MATERIALIZATION MISSING " + site.siteId);
                continue;
            }

            if (IsAcceptedTravelAnchor(site))
            {
                // note: These base anchors require the reviewed released landmark path; registry availability alone cannot approve an unreviewed fallback prefab.
                Debug.LogError("[YQContinuousWorldFeatures] ACCEPTED TRAVEL ANCHOR PHYSICAL MATERIALIZATION MISSING " + site.siteId);
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

        bool continuationSite = !IsBaseAcceptedSite(plan, site);
        GeneratedSpatialContinuationLocationV2Record continuationLocation = null;
        YQPreparedSpatialMaterializationV2 continuationPrepared = null;
        if (continuationSite)
        {
            // note: Continued sites consume their saved binding and layout through the compiled owner; ordinary selection cannot revise accepted content during streaming.
            if (!TryResolveAcceptedContinuationSiteContext(plan, site, out continuationLocation,
                    out continuationPrepared, out string continuationFailure))
            {
                Debug.LogError("[YQContinuousWorldFeatures] ACCEPTED CONTINUATION REPLAY REJECTED " + site.siteId + ": " + continuationFailure);
                yield break;
            }
            settlement = continuationLocation.settlement;
            encampment = continuationLocation.encampment;
            locationId = continuationLocation.anchor.sourceSemanticId;
            reviewedSite = YQCompiledWorldSiteBindingService.GetCachedSiteRecord(ContinuationKitId(continuationLocation));
        }

        if (!continuationSite && IsAcceptedCaveSite(site))
        {
            // note: Production cave geometry stays inside its accepted footprint; only an explicit diagnostic fixture requests the extended six-module chain.
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

        if (!continuationSite && site.kind == YQSiteKindV2.Settlement && settlement != null &&
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
        else if (!continuationSite && site.kind == YQSiteKindV2.HostileSite && encampment != null &&
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
        else if (!continuationSite && IsAcceptedCaveSite(site))
        {
            reviewedSite = FindReviewedSiteForFunctions(
                YQAuthoredSiteKind.Dungeon,
                new[]
                {
                    YQAssetFunctionV2.Transition,
                    YQAssetFunctionV2.Encounter,
                    YQAssetFunctionV2.Reward
                },
                site.siteId, palette?.styleKey, region?.assetStyleRationale);
        }

        if (!continuationSite && reviewedSite == null && site.kind == YQSiteKindV2.Settlement)
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
                site.siteId, palette?.styleKey, region?.assetStyleRationale);
            semanticTags = new[] { "poi", "civic", "residential", "service", "circulation" };
        }
        else if (!continuationSite && reviewedSite == null && site.kind == YQSiteKindV2.HostileSite)
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
                site.siteId, palette?.styleKey, region?.assetStyleRationale);
            semanticTags = new[] { "poi", "perimeter", "circulation", "encounter", "reward" };
        }
        else if (!continuationSite && reviewedSite == null && site.kind == YQSiteKindV2.PointOfInterest)
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
                site.siteId, palette?.styleKey, region?.assetStyleRationale);
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
                    site.siteId + "|poi_fallback", palette?.styleKey, region?.assetStyleRationale);
                semanticTags = new[] { "poi", "circulation", "service", "reward" };
            }
        }
        else if (!continuationSite && reviewedSite == null && IsAcceptedTravelAnchor(site))
        {
            // note: Base travel anchors retain their accepted NaturalFeature identity while their cultural/transition functions select a released exterior landmark.
            var requiredFunctions = new YQAssetFunctionV2[site.RequiredFunctions.Count];
            for (int index = 0; index < requiredFunctions.Length; index++)
                requiredFunctions[index] = site.RequiredFunctions[index];
            reviewedSite = FindReviewedSiteForFunctions(
                YQAuthoredSiteKind.Landmark, requiredFunctions, site.siteId,
                palette?.styleKey, region?.assetStyleRationale, site.reservedRadius);
            semanticTags = new[] { "cultural", "transition", "circulation" };
        }

        if (reviewedSite == null || string.IsNullOrWhiteSpace(reviewedSite.runtimeManifestResourceKey))
            yield break;

        string selectionSeed = ResolveAcceptedSiteSelectionSeed(plan, site, settlement, encampment);
        if (continuationSite && (string.IsNullOrWhiteSpace(selectionSeed) || selectionSeed != continuationLocation.compositionSeed))
            yield break;

        if (!continuationSite && IsAcceptedTravelAnchor(site))
        {
            // note: Measure the exact seeded selection before loading; a later catalog addition cannot silently exceed the unchanged accepted reservation.
            bool footprintValid = false;
            float footprintRadius = 0f;
            string footprintFailure = string.Empty;
            string preparedSelectionSeed = null;
            YQProceduralSettlementLayoutRecord preparedLayout = null;
            if (YQWorldGenerationArchitecture.UsesV2SpatialRuntime)
                yield return YQCompiledWorldSiteInstance.ResolveSemanticCompositionV2Routine(
                    reviewedSite, semanticTags, selectionSeed,
                    (valid, radius, composition, failure) =>
                    {
                        footprintValid = valid; footprintRadius = radius; footprintFailure = failure;
                        preparedSelectionSeed = composition.selectionSeed;
                        preparedLayout = YQProceduralSettlementLayout.Get(preparedSelectionSeed);
                    },
                    site.RequiredFunctions, null, site);
            else
                yield return YQCompiledWorldSiteInstance.ResolveSemanticSliceRadiusRoutine(
                    reviewedSite, semanticTags, selectionSeed,
                    (valid, radius) => { footprintValid = valid; footprintRadius = radius; });
            if (!footprintValid || float.IsNaN(footprintRadius) || float.IsInfinity(footprintRadius) ||
                footprintRadius <= 0f || footprintRadius > site.reservedRadius)
            {
                // note: A successfully prepared but oversized selection remains unpublished; retire only its exact pending cache entry, preserving committed layouts.
                if (!string.IsNullOrEmpty(preparedSelectionSeed))
                    YQProceduralSettlementLayout.DiscardPending(preparedSelectionSeed, preparedLayout);
                Debug.LogError("[YQContinuousWorldFeatures] ACCEPTED TRAVEL ANCHOR FOOTPRINT REJECTED " +
                    site.siteId + " radius=" + footprintRadius + " reservation=" + site.reservedRadius +
                    " reason=" + footprintFailure);
                yield break;
            }
        }

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
        if (continuationSite)
            yield return YQCompiledWorldSiteInstance.MaterializeAcceptedContinuationRoutine(
                root.transform, plan, continuationLocation, continuationPrepared, success => materialized = success);
        else
            yield return YQCompiledWorldSiteInstance.MaterializeSemanticSliceNowRoutine(
                root.transform, locationId, reviewedSite, semanticTags, selectionSeed, success => materialized = success);
        if (!materialized || continuationSite && !ContinuationReplayStillCurrent(
                plan, site, continuationLocation, continuationPrepared, selectionSeed, root))
        {
            UnityEngine.Object.Destroy(root);
            Debug.LogWarning(
                "[YQContinuousWorldFeatures] Accepted reviewed site was rejected: " + site.siteId);
            yield break;
        }

        // note: The site loader stages CompiledSiteContent under this root while the enclosing chunk is hidden; publish the site root's own activeSelf state before the chunk lifecycle pass exposes it.
        if (!continuationSite) root.SetActive(true);

        // note: Overlay identity is the accepted V2 site ID even when the reviewed provider is keyed by its source semantic record.
        YQStreamedFeatureOverlayTarget overlayTarget = root.GetComponent<YQStreamedFeatureOverlayTarget>() ??
            root.AddComponent<YQStreamedFeatureOverlayTarget>();
        overlayTarget.featureId = site.siteId;
        overlayTarget.objectId = root.name;
        if (!continuationSite && site.kind == YQSiteKindV2.Settlement &&
            (HasSiteTag(site, "beta:settlement") || HasSiteTag(site, "beta_fixture_settlement")))
        {
            // note: Bind the beta settlement's supported player service to the accepted site identity so purchases survive site streaming and save reloads.
            YQGeneratedSettlementService service = root.GetComponent<YQGeneratedSettlementService>() ??
                root.AddComponent<YQGeneratedSettlementService>();
            service.Configure(site.siteId, plan.worldSeed);
        }
        if (!continuationSite && site.kind == YQSiteKindV2.HostileSite &&
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
        var sectorLayout = root.GetComponent<YQCompiledWorldSiteInstance>()?.PreparedLayout;
        if (sectorLayout != null && sectorLayout.version >= 5)
        {
            // note: Draw the same accepted-sector frontages used by rigid-cell placement; central-to-member-centre ribbons would run through the new buildings.
            Material accessMaterial = FindMaterial(registry, "ground", "mud", "path") ?? BuildFallbackRoadMaterial();
            RegisterCellResource(root.transform, accessMaterial);
            for (int streetIndex = 0; streetIndex < sectorLayout.streets.Count; streetIndex++)
            {
                var street = sectorLayout.streets[streetIndex];
                int objects = BuildSampledSiteConnector(root.transform, "SectorStreet_" + streetIndex, terrain,
                    root.transform.TransformPoint(street.start), root.transform.TransformPoint(street.end), street.width, accessMaterial);
                if (objects <= 0) { connectorFailure = true; break; }
                materializedObjects += objects;
                yield return null;
                // note: Streets remain under the inactive root; repeat full saved-payload validation once immediately before publication.
                if (continuationSite && (root == null || WorldStateManager.Instance?.State?.generatedWorldPlan != plan))
                { connectorFailure = true; break; }
            }
        }
        else if (continuationSite && site.MemberFootprint.Count > 0)
        {
            // note: Committed continuation sectors cannot be recovered with centre-to-centre ribbons when their saved frontage layout is unavailable.
            connectorFailure = true;
        }
        else if (site.MemberFootprint.Count > 0)
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
        if (connectorFailure || continuationSite && !ContinuationReplayStillCurrent(
                plan, site, continuationLocation, continuationPrepared, selectionSeed, root))
        {
            // note: Do not publish a reviewed site root whose mandatory member sector cannot be reached by physical geometry.
            UnityEngine.Object.Destroy(root);
            completed?.Invoke(0);
            yield break;
        }
        // note: The exact continuation provider stays hidden until replay and all mandatory streets pass the current saved-context checks.
        if (continuationSite) root.SetActive(true);
        else if (encampment != null && ReferenceEquals(WorldStateManager.Instance?.State?.generatedWorldPlan, plan))
        {
            // note: Deferred base sites publish saved geometry only after every required connector succeeds; continued sites keep compositionLayout exclusively.
            string preparedSeed = root.GetComponent<YQCompiledWorldSiteInstance>()?.PreparedLayout?.seed;
            YQProceduralSettlementLayout.Commit(encampment, preparedSeed);
        }
        completed?.Invoke(materializedObjects);
    }

    private static bool IsBaseAcceptedSite(GeneratedWorldPlanRecord plan, YQSpatialMaterializationSiteV2 site)
    {
        var anchors = plan?.spatialPlanV2?.blueprint?.sites;
        if (anchors != null)
            foreach (var anchor in anchors)
                if (anchor != null && string.Equals(anchor.siteId, site.siteId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(anchor.sourceSemanticId, site.sourceSemanticId, StringComparison.OrdinalIgnoreCase))
                    return true;
        return false;
    }

    private static string ContinuationKitId(GeneratedSpatialContinuationLocationV2Record location) =>
        location?.settlement?.runtimeSiteKitId ?? location?.encampment?.runtimeSiteKitId ?? location?.poiRuntimeSiteKitId;

    private static bool TryResolveAcceptedContinuationSiteContext(GeneratedWorldPlanRecord plan,
        YQSpatialMaterializationSiteV2 site, out GeneratedSpatialContinuationLocationV2Record location,
        out YQPreparedSpatialMaterializationV2 prepared, out string failure)
    {
        location = null;
        prepared = null;
        failure = "Continued site has no unique persisted accepted identity.";
        var extension = plan?.spatialPlanV2?.acceptedContinuation;
        if (extension?.state != YQSpatialContinuationStateV2.Accepted || extension.locations == null)
            return false;
        foreach (var candidate in extension.locations)
        {
            var anchor = candidate?.anchor;
            if (anchor == null || !string.Equals(anchor.siteId, site.siteId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(anchor.sourceSemanticId, site.sourceSemanticId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (location != null) return false;
            location = candidate;
        }
        // note: Obtain the immutable query projection here; the immediately following replay owner performs full current checksums and binding/layout admission. Do not hash the entire world twice for this one lookup.
        if (location == null || !YQSpatialMaterializationResolverV2.TryGetQueryProjection(plan, out prepared, out failure) ||
            !YQCompiledWorldSiteInstance.TryValidateAcceptedContinuationReplay(plan, location, prepared, out var currentSite, out failure))
            return false;
        if (!ContinuationPreparedSiteMatches(site, currentSite))
        {
            failure = "Continued site no longer matches its current immutable spatial projection.";
            return false;
        }
        return true;
    }

    private static bool ContinuationPreparedSiteMatches(YQSpatialMaterializationSiteV2 site, YQSpatialMaterializationSiteV2 current)
    {
        // note: Identity plus central/member geometry binds this stream request to the actual measured projection rather than an ID-only claim.
        if (!string.Equals(site.siteId, current.siteId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(site.sourceSemanticId, current.sourceSemanticId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(site.parentRegionId, current.parentRegionId, StringComparison.OrdinalIgnoreCase) ||
            site.kind != current.kind || site.placementMode != current.placementMode || site.x != current.x || site.z != current.z ||
            site.headingDegrees != current.headingDegrees || site.reservedRadius != current.reservedRadius ||
            site.maximumSlopeDegrees != current.maximumSlopeDegrees || !site.terrainReserveReady || !current.terrainReserveReady ||
            site.surfaceElevationNormalized != current.surfaceElevationNormalized || site.frontageRouteId != current.frontageRouteId ||
            site.frontageX != current.frontageX || site.frontageZ != current.frontageZ || site.routeAccess != current.routeAccess ||
            site.RequiredFunctions.Count != current.RequiredFunctions.Count || site.MemberFootprint.Count != current.MemberFootprint.Count ||
            site.Tags.Count != current.Tags.Count)
            return false;
        for (int index = 0; index < site.RequiredFunctions.Count; index++)
            if (site.RequiredFunctions[index] != current.RequiredFunctions[index]) return false;
        for (int index = 0; index < site.Tags.Count; index++)
            if (site.Tags[index] != current.Tags[index]) return false;
        foreach (var member in site.MemberFootprint)
        {
            if (member == null) return false;
            bool found = false;
            foreach (var other in current.MemberFootprint)
                if (other != null && member.memberId == other.memberId && member.x == other.x && member.z == other.z &&
                    member.reservedRadius == other.reservedRadius && member.sectorIndex == other.sectorIndex)
                { found = true; break; }
            if (!found) return false;
        }
        return true;
    }

    private static bool ContinuationReplayStillCurrent(GeneratedWorldPlanRecord plan, YQSpatialMaterializationSiteV2 site,
        GeneratedSpatialContinuationLocationV2Record location, YQPreparedSpatialMaterializationV2 prepared,
        string selectionSeed, GameObject root)
    {
        if (root == null || !TryResolveAcceptedContinuationSiteContext(plan, site, out var currentLocation,
                out var currentPrepared, out _) || !ReferenceEquals(location, currentLocation) ||
            !ReferenceEquals(prepared, currentPrepared) || selectionSeed != currentLocation.compositionSeed)
            return false;
        var provider = root.GetComponent<YQCompiledWorldSiteInstance>();
        var savedLayout = location.settlement?.proceduralLayout ?? location.compositionLayout;
        return provider != null && provider.IsContinuationGeometryLoaded && savedLayout != null && ReferenceEquals(provider.PreparedLayout, savedLayout) &&
            string.Equals(provider.CanonicalRegionId, site.parentRegionId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetAcceptedContinuationLoadedProvider(GeneratedWorldPlanRecord plan, YQSpatialMaterializationSiteV2 site,
        out YQCompiledWorldSiteInstance provider, out string failure,
        YQCompiledWorldSiteInstance.StagedSiteGeometryContext stagedGeometry = null)
    {
        provider = null;
        if (!TryResolveAcceptedContinuationSiteContext(plan, site, out var location, out _, out failure)) return false;
        var savedLayout = location.settlement?.proceduralLayout ?? location.compositionLayout;
        bool geometryReady = stagedGeometry != null
            ? YQCompiledWorldSiteInstance.TryGetContinuationGeometryContext(location.anchor.sourceSemanticId,
                ContinuationKitId(location), location.compositionSeed, stagedGeometry, out provider)
            : YQCompiledWorldSiteInstance.TryGetContinuationGeometryContext(location.anchor.sourceSemanticId,
                ContinuationKitId(location), location.compositionSeed, out provider);
        if (!geometryReady || savedLayout == null ||
            !ReferenceEquals(provider.PreparedLayout, savedLayout) ||
            !string.Equals(provider.CanonicalRegionId, site.parentRegionId, StringComparison.OrdinalIgnoreCase))
        {
            failure = "Loaded continuation provider does not match its committed kit, seed, layout or region.";
            return false;
        }
        return true;
    }

    private static string ResolveAcceptedSiteSelectionSeed(
        GeneratedWorldPlanRecord plan,
        YQSpatialMaterializationSiteV2 site,
        GeneratedSettlementRecord settlement,
        GeneratedEncampmentRecord encampment)
    {
        if (!IsBaseAcceptedSite(plan, site))
        {
            // note: Missing/stale continuation identity has no legacy seed fallback; the accepted composition seed is the only replay authority.
            return TryResolveAcceptedContinuationSiteContext(plan, site, out var continuation, out _, out _)
                ? continuation.compositionSeed : string.Empty;
        }
        // note: Reuse the persisted procedural layout identity when available so streamed settlement sectors consume the exact accepted cell selection and street plan.
        if (settlement?.proceduralLayout != null &&
            !string.IsNullOrWhiteSpace(settlement.proceduralLayout.seed))
            return settlement.proceduralLayout.seed;
        // note: Base hostile sectors replay their committed source selection and parcel elevations, just like inhabited sites.
        if (!string.IsNullOrWhiteSpace(encampment?.proceduralLayout?.seed))
            return encampment.proceduralLayout.seed;
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
            if (!TryPlaceApprovedFallbackGrounded(instance, terrain, site, instanceSeed, builtInstances))
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
        string seed,
        IReadOnlyList<GameObject> occupied = null)
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
            for (int directionIndex = 0; directionIndex < (radiusIndex == 0 ? 1 : 8); directionIndex++)
            {
                float radius = radii[radiusIndex];
                float angle = angleOffset + radiusIndex * 1.61803399f + directionIndex * Mathf.PI * .25f;
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
                    // note: Grounding is necessary but cannot place two settlement buildings in the same footprint or outside their accepted reservation.
                    if (!YQGeneratedWorldTerrain.TryGetStableContactGeometry(instance, out Bounds bounds, out _)) continue;
                    float dx = Mathf.Abs(bounds.center.x - site.x) + bounds.extents.x;
                    float dz = Mathf.Abs(bounds.center.z - site.z) + bounds.extents.z;
                    if (dx * dx + dz * dz > site.reservedRadius * site.reservedRadius) continue;
                    bool intersects = false;
                    if (occupied != null) foreach (GameObject other in occupied)
                        if (other != null && YQGeneratedWorldTerrain.TryGetStableContactGeometry(other, out Bounds otherBounds, out _) &&
                            ArchitecturalFootprintsOverlap(bounds, otherBounds)) { intersects = true; break; }
                    if (!intersects) return true;
                }
            }
        }

        // note: Exhausted physical placement remains a failed site; the caller retires this instance and the per-site publication gate retains its required identity.
        Debug.LogWarning(
            "[YQContinuousWorldFeatures] Approved fallback rejected after grounded nonintersecting footprint attempts site=" +
            site.siteId);
        return false;
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
        int moduleCount = UsesFixtureCaveModules(site) ? 6 : Mathf.Clamp(Mathf.FloorToInt(site.reservedRadius * 1.4f / moduleDepth), 1, 2);
        first.transform.localPosition = new Vector3(0f, 0f, moduleDepth * (0.5f - moduleCount * 0.5f));
        first.transform.localRotation = Quaternion.identity;
        registry.ApplyMaterialOverrides(YQContinuousWorldFeatureAuthority.BetaCaveStraightPrefab, first, false);
        yield return YQRuntimeUrpMaterialRepair.RepairMaterialHierarchyRoutine(first, null);
        int built = 1;

        // note: Both ends remain open, giving the accepted entrance an actual traversable interior and a second exit rather than a capped prop.
        for (int index = 1; index < moduleCount; index++)
        {
            string prefabPath = index == 2 || index == 3
                ? YQContinuousWorldFeatureAuthority.BetaCaveCrossPrefab
                : YQContinuousWorldFeatureAuthority.BetaCaveStraightPrefab;
            GameObject prefab = index == 2 || index == 3 ? crossPrefab : straightPrefab;
            GameObject module = UnityEngine.Object.Instantiate(prefab, root.transform);
            module.name = "BetaCave_Module_" + index;
            module.transform.localPosition = new Vector3(0f, 0f, moduleDepth * (index + 0.5f - moduleCount * 0.5f));
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
        string seed,
        string preferredStyleKey = null,
        string styleTransitionReason = null,
        float maximumRadius = float.PositiveInfinity)
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
            // note: A bounded accepted anchor filters impossible whole-assembly footprints; callers without a bound retain their previous selection contract.
            if (!float.IsPositiveInfinity(maximumRadius) &&
                (float.IsNaN(maximumRadius) || maximumRadius <= 0f ||
                 float.IsNaN(candidate.authoredFootprintRadius) || float.IsInfinity(candidate.authoredFootprintRadius) ||
                 candidate.authoredFootprintRadius <= 0f || candidate.authoredFootprintRadius > maximumRadius))
                continue;
            // note: A visible streamed settlement/camp/landmark must have exterior placement evidence and obey the same palette transition contract as narrative sites.
            bool exterior = preferredKind == YQAuthoredSiteKind.Settlement || preferredKind == YQAuthoredSiteKind.Camp || preferredKind == YQAuthoredSiteKind.Landmark;
            if ((exterior && (!candidate.seamlessPlacementEligible || candidate.presentationMode != YQWorldSitePresentationMode.SeamlessExterior)) ||
                (!string.IsNullOrWhiteSpace(preferredStyleKey) && !YQWorldAssetCatalog.IsCoherentStyleTransition(preferredStyleKey, candidate.semanticStyleKey, styleTransitionReason)))
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

    private static bool UsesFixtureCaveModules(YQSpatialMaterializationSiteV2 site)
    {
        // note: Generated natural caves keep their bounded approved kit; only an explicit fixture tag selects the extended diagnostic module chain.
        return IsAcceptedCaveSite(site) && (HasSiteTag(site, "beta:cave") || HasSiteTag(site, "beta_fixture_cave"));
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
               IsAcceptedTravelAnchor(site) ||
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

    private static bool IsAcceptedTravelAnchor(YQSpatialMaterializationSiteV2 site)
    {
        if (site.kind != YQSiteKindV2.NaturalFeature) return false;
        // note: Both actual accepted tags are required; a suggestive name or a generic continuation alone does not select the base itinerary landmark contract.
        bool travelFixture = false, streamedAnchor = false;
        foreach (string tag in site.Tags)
        {
            travelFixture |= string.Equals(tag, "beta_travel_fixture", StringComparison.OrdinalIgnoreCase);
            streamedAnchor |= string.Equals(tag, "accepted_streamed_continuation", StringComparison.OrdinalIgnoreCase);
        }
        return travelFixture && streamedAnchor;
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

    internal static bool ArchitecturalFootprintsOverlap(Bounds first, Bounds second)
    {
        // note: A quarter-metre edge clearance permits close neighbors without allowing eaves/foundations to interpenetrate; terrain height cannot disguise overlap.
        const float clearance = .25f;
        return first.min.x < second.max.x + clearance && first.max.x + clearance > second.min.x &&
            first.min.z < second.max.z + clearance && first.max.z + clearance > second.min.z;
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
        // note: Rotate approved fallback variants by accepted identity rather than always choosing the first available ruin or landmark.
        int firstCandidate = Mathf.Min(candidates.Length - 1, Mathf.FloorToInt(
            YQContinuousWorldFeatureAuthority.Hash01(site.siteId + "|fallback-variety") * candidates.Length));
        for (int offset = 0; offset < candidates.Length; offset++)
        {
            string candidatePath = candidates[(firstCandidate + offset) % candidates.Length];
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
        // note: Accepted road contacts never move when the derived physical deck changes.
        public List<Vector3> routeContacts;
        public List<Vector2> fitRoute;
        public float fitStart, fitEnd;
        public bool naturalDeck;
        public AcceptedCrossingSpan bridge;
        public Vector2 anchor;
        public readonly List<List<Vector3>> bankConnections = new List<List<Vector3>>();
    }

    private sealed class AcceptedBridgeLayout
    {
        public bool building;
        public bool ready;
        public bool finalizing, finalized, materializationStarted;
        public int physicalRevision;
        public float baseY, height;
        public List<AcceptedCrossingSpan> spans;
        public Func<float, float, float> featureHeight;
        public float featureHeightScale;
        public IReadOnlyList<NativeBridgeEndpointApproach> endpointApproaches;
    }
    // note: This weak cache stores only derived geometry from one immutable accepted projection, never canonical records or Unity object ownership.
    private static readonly ConditionalWeakTable<YQPreparedSpatialMaterializationV2, AcceptedBridgeLayout> BridgeLayouts =
        new ConditionalWeakTable<YQPreparedSpatialMaterializationV2, AcceptedBridgeLayout>();
    internal const float MinimumBridgeClearance = 24f;

    private static Func<float, float, float> GetNativeBridgeHeightProjection(
        YQPreparedSpatialMaterializationV2 prepared, float height)
    {
        // note: One immutable finite-feature projection belongs to this existing weak layout cache. Bridges and their bank paths use the cell owner's finished road/water datum without capturing native objects or mutable saves.
        var layout = BridgeLayouts.GetValue(prepared, _ => new AcceptedBridgeLayout());
        if (layout.featureHeight == null || layout.featureHeightScale != height)
        {
            layout.featureHeight = YQContinuousWorldCellAuthority.CreateAcceptedFeatureHeightProjection(prepared, height);
            layout.featureHeightScale = height;
        }
        return layout.featureHeight;
    }

    internal static bool UsesNativeBridgeRoutes(YQPreparedSpatialMaterializationV2 prepared)
    {
        // note: Physical route corrections are version-pinned; saved bridge interpretations from earlier basin generations remain intact.
        for (int water = 0; water < prepared.WaterCount; water++)
            if (prepared.GetWater(water).connectedBasin?.Version >= YQPreparedConnectedBasinV2.NativeChannelReachVersion) return true;
        return false;
    }

    internal readonly struct NativeBridgeEndpointApproach
    {
        internal readonly int routeIndex;
        internal readonly Vector2 start, end;
        internal readonly float elevationNormalized;
        internal NativeBridgeEndpointApproach(int routeIndex, Vector2 start, Vector2 end, float elevation)
        { this.routeIndex = routeIndex; this.start = start; this.end = end; elevationNormalized = elevation; }
    }

    internal static IReadOnlyList<NativeBridgeEndpointApproach> GetNativeBridgeEndpointApproaches(YQPreparedSpatialMaterializationV2 prepared)
    {
        var layout = BridgeLayouts.GetValue(prepared, _ => new AcceptedBridgeLayout());
        // note: Every streamed terrain constructor reuses immutable hydrology-derived aprons. The weak prepared-owner cache never stores terrain objects and the lock protects simultaneous worker queries.
        lock (layout)
            return layout.endpointApproaches ??= BuildNativeBridgeEndpointApproaches(prepared).AsReadOnly();
    }

    private static List<NativeBridgeEndpointApproach> BuildNativeBridgeEndpointApproaches(YQPreparedSpatialMaterializationV2 prepared)
    {
        var result = new List<NativeBridgeEndpointApproach>();
        if (!UsesNativeBridgeRoutes(prepared)) return result;
        // note: Lazily project the immutable road network once for all endpoint queries in this constructor; an empty crossing set still does no road work.
        YQGeneratedWorldEnvironment.AcceptedRouteCenterlineQueries routeQueries = null;
        // note: An endpoint crossing needs finite landward earthworks as well as a deck. Derive these from hydrology before layout discovery, avoiding a dependency on the layout that the earthworks support.
        for (int routeIndex = 0; routeIndex < prepared.RouteCount; routeIndex++)
        {
            var route = prepared.GetRoute(routeIndex);
            List<Vector2> points = null;
            for (int endpoint = 0; endpoint < 2; endpoint++)
            {
                float requiredSpan = 0f;
                for (int crossingIndex = 0; crossingIndex < prepared.CrossingCount; crossingIndex++)
                {
                    var crossing = prepared.GetCrossing(crossingIndex);
                    if (crossing.routeId != route.routeId || !IsPhysicalRiverCrossing(crossing.kind)) continue;
                    points ??= (routeQueries ??= new YQGeneratedWorldEnvironment.AcceptedRouteCenterlineQueries(prepared)).GetRoute(routeIndex);
                    if (points.Count < 2) break;
                    Vector2 position = points[endpoint == 0 ? 0 : points.Count - 1];
                    float contact = Mathf.Max(16f, crossing.requiredSpan * 1.5f);
                    if ((new Vector2(crossing.x, crossing.z) - position).sqrMagnitude <= contact * contact)
                        requiredSpan = Mathf.Max(requiredSpan, crossing.requiredSpan);
                }
                if (requiredSpan <= 0f || points == null || points.Count < 2) continue;
                int first = endpoint == 0 ? 0 : points.Count - 1;
                int step = endpoint == 0 ? 1 : -1, adjacent = first + step;
                while (adjacent >= 0 && adjacent < points.Count && (points[first] - points[adjacent]).sqrMagnitude < .000001f) adjacent += step;
                if (adjacent < 0 || adjacent >= points.Count) continue;
                Vector2 origin = points[first], outward = (origin - points[adjacent]).normalized;
                Vector2 lateral = new Vector2(-outward.y, outward.x);
                int dryStations = 0;
                for (int station = 1; station <= 512; station++)
                {
                    Vector2 point = origin + outward * station;
                    bool dry = IsDryBridgeBank(prepared, point, route.width + 2f) && IsDryBridgeBank(prepared, point, route.width);
                    for (int rail = -2; dry && rail <= 2; rail++)
                    {
                        Vector2 sample = point + lateral * ((route.width + 2f) * .25f * rail);
                        dry = prepared.SampleTerrain(sample.x, sample.y).waterMask <= .05f;
                    }
                    dryStations = dry ? dryStations + 1 : 0;
                    if (dryStations < 3 || station < requiredSpan * .5f) continue;
                    // note: Keep a finite flat handoff beyond the first two dry metres. This is a local bridge apron, not a permitted boundary continuation or a new semantic road.
                    float length = Mathf.Min(512f, station + Mathf.Max(32f, 4f * (route.width + 2f)));
                    result.Add(new NativeBridgeEndpointApproach(routeIndex, origin, origin + outward * length,
                        prepared.SampleTerrain(origin.x, origin.y).elevationNormalized));
                    break;
                }
            }
        }
        return result;
    }

    internal static IEnumerator PrepareAcceptedBridgeRoutesRoutine(YQPreparedSpatialMaterializationV2 prepared, Terrain terrain)
    {
        if (UsesNativeBridgeRoutes(prepared)) yield return PrepareBridgeLayoutRoutine(prepared, terrain, _ => { });
    }

    internal static void VisitSharedBridgeBankPaths(YQPreparedSpatialMaterializationV2 prepared,
        Action<int, float, IReadOnlyList<Vector3>> visit)
    {
        if (!UsesNativeBridgeRoutes(prepared)) return;
        if (!BridgeLayouts.TryGetValue(prepared, out var layout) || !layout.ready)
            throw new InvalidOperationException("Accepted native bridge layout is not prepared.");
        foreach (var span in layout.spans)
            foreach (var connection in span.bankConnections) visit(span.routeIndex, span.width, connection);
    }

    internal static void ApplySharedBridgeRoute(YQPreparedSpatialMaterializationV2 prepared, int routeIndex, List<Vector2> points)
    {
        if (!UsesNativeBridgeRoutes(prepared)) return;
        if (!BridgeLayouts.TryGetValue(prepared, out var layout) || !layout.ready)
            throw new InvalidOperationException("Physical route validation requires the prepared native bridge layout.");
        var spans = new List<AcceptedCrossingSpan>();
        foreach (var span in layout.spans)
            if (span.routeIndex == routeIndex && (span.bridge != span || span.naturalDeck)) spans.Add(span);
        if (spans.Count == 0) return;
        // note: Replace only each derived shared crossing's original wet interval, then validate both complete dry connectors and the actual reused deck. No route or required crossing is omitted.
        float[] distances = new float[points.Count];
        for (int point = 1; point < points.Count; point++) distances[point] = distances[point - 1] + Vector2.Distance(points[point - 1], points[point]);
        float Along(Vector3 position)
        {
            Vector2 p = new Vector2(position.x, position.z); float best = float.PositiveInfinity, along = 0f;
            for (int point = 1; point < points.Count; point++)
            {
                Vector2 delta = points[point] - points[point - 1];
                float t = Mathf.Clamp01(Vector2.Dot(p - points[point - 1], delta) / Mathf.Max(.000001f, delta.sqrMagnitude));
                float squared = (p - points[point - 1] - delta * t).sqrMagnitude;
                if (squared < best) { best = squared; along = Mathf.Lerp(distances[point - 1], distances[point], t); }
            }
            return along;
        }
        spans.Sort((a, b) => { int order = a.startDistance.CompareTo(b.startDistance);
            return order != 0 ? order : string.CompareOrdinal(a.crossingId, b.crossingId); });
        var result = new List<Vector2>(); int original = 0; float consumed = -1f;
        void Add(Vector3 p) { var point = new Vector2(p.x, p.z); if (result.Count == 0 || (result[result.Count - 1] - point).sqrMagnitude > .000001f) result.Add(point); }
        for (int spanIndex = 0; spanIndex < spans.Count; spanIndex++)
        {
            var span = spans[spanIndex];
            var contacts = span.routeContacts ?? span.points;
            float start = Along(contacts[0]), end = Along(contacts[contacts.Count - 1]);
            if (start < consumed - .01f || end < start || span.bankConnections.Count != 2)
                throw new InvalidOperationException("Shared crossing route intervals are disconnected or overlap: " + span.crossingId);
            var exitSpan = span;
            // note: A river and its continuation can accept the same wet interval under distinct identities. Splice their union once through their common real deck, retaining every accepted crossing and both outer dry-bank paths.
            while (spanIndex + 1 < spans.Count)
            {
                var next = spans[spanIndex + 1];
                var nextContacts = next.routeContacts ?? next.points;
                float nextStart = Along(nextContacts[0]), nextEnd = Along(nextContacts[nextContacts.Count - 1]);
                if (nextStart > end + .01f) break;
                if (next.bridge != span.bridge || nextEnd < nextStart || next.bankConnections.Count != 2)
                    throw new InvalidOperationException("Overlapping shared crossing intervals require the same bridge and complete dry-bank paths: " + span.crossingId + " / " + next.crossingId);
                if (nextEnd > end) { end = nextEnd; exitSpan = next; }
                spanIndex++;
            }
            while (original < points.Count && distances[original] < start - .001f) { var p = points[original++]; Add(new Vector3(p.x, 0f, p.y)); }
            var first = span.bankConnections[0]; var last = exitSpan.bankConnections[1];
            foreach (var p in first) Add(p);
            var deck = span.bridge.points;
            int from = (first[first.Count - 1] - deck[0]).sqrMagnitude <= (first[first.Count - 1] - deck[deck.Count - 1]).sqrMagnitude ? 0 : deck.Count - 1;
            int to = (last[last.Count - 1] - deck[0]).sqrMagnitude <= (last[last.Count - 1] - deck[deck.Count - 1]).sqrMagnitude ? 0 : deck.Count - 1;
            int direction = from <= to ? 1 : -1;
            for (int point = from; ; point += direction) { Add(deck[point]); if (point == to) break; }
            for (int point = last.Count - 1; point >= 0; point--) Add(last[point]);
            while (original < points.Count && distances[original] <= end + .001f) original++;
            consumed = end;
        }
        while (original < points.Count) { var p = points[original++]; Add(new Vector3(p.x, 0f, p.y)); }
        points.Clear(); points.AddRange(result);
    }

    private static IEnumerator PrepareBridgeLayoutRoutine(YQPreparedSpatialMaterializationV2 prepared, Terrain terrain,
        Action<List<AcceptedCrossingSpan>> completed)
    {
        AcceptedBridgeLayout cache = BridgeLayouts.GetValue(prepared, _ => new AcceptedBridgeLayout());
        // note: Waiting on a shared derived bridge layout can outlive this streamed terrain owner. A retired cell supplies no layout and cannot touch its destroyed native terrain.
        while (cache.building || cache.finalizing)
        {
            if (terrain == null || terrain.terrainData == null) { completed(null); yield break; }
            yield return null;
        }
        if (terrain == null || terrain.terrainData == null) { completed(null); yield break; }
        float baseY = terrain.transform.position.y, height = terrain.terrainData.size.y;
        if (cache.ready && cache.baseY == baseY && cache.height == height)
        { completed(cache.spans); yield break; }
        cache.building = true;
        try
        {
            var spans = new List<AcceptedCrossingSpan>();
            for (int index = 0; index < prepared.CrossingCount; index++)
            {
                if (terrain == null || terrain.terrainData == null) { completed(null); yield break; }
                var crossing = prepared.GetCrossing(index);
                if (!IsPhysicalRiverCrossing(crossing.kind)) continue;
                if (!TryBuildCrossingSpan(prepared, crossing, terrain, out var span, out string failure))
                    throw new InvalidOperationException("Accepted crossing " + crossing.crossingId + ": " + failure);
                span.anchor = new Vector2(crossing.x, crossing.z);
                spans.Add(span);
                yield return null;
            }
            // note: Choose the widest deck first so later shared crossings never widen an already clearance-checked bridge; identity breaks ties independently of traversal.
            spans.Sort((a, b) => { int width = b.width.CompareTo(a.width); return width != 0 ? width : string.CompareOrdinal(a.crossingId, b.crossingId); });
            var bridges = new List<AcceptedCrossingSpan>();
            foreach (var span in spans)
            {
                AcceptedCrossingSpan shared = null;
                foreach (var bridge in bridges)
                    // note: Compare each deck at its authored width. Using the widest crossing in the whole plan over-deduplicated unrelated crossings and forced invalid shared-bank searches.
                    if (CrossingDecksConflict(span.points, span.width, bridge.points, bridge.width))
                    { shared = bridge; break; }
                span.bridge = shared ?? span;
                if (shared == null) bridges.Add(span);
                else
                {
                    // note: A conflicting crossing reuses one real deck through two proven dry-bank paths. Lake/tributary seams can put both route contacts on the same connected bank; they may converge on one deck entrance without inventing another wet crossing.
                    bool reversed = (span.points[0] - shared.points[shared.points.Count - 1]).sqrMagnitude <
                        (span.points[0] - shared.points[0]).sqrMagnitude;
                    bool connected = false;
                    var bankFailures = new List<string>();
                    for (int pairing = 0; pairing < 4 && !connected; pairing++)
                    {
                        span.bankConnections.Clear();
                        for (int side = 0; side < 2; side++)
                        {
                            Vector3 from = span.points[side == 0 ? 0 : span.points.Count - 1];
                            int destination = pairing >= 2 ? (pairing == 2 ? 0 : shared.points.Count - 1) :
                                ((side == 0) != (reversed ^ (pairing == 1)) ? 0 : shared.points.Count - 1);
                            List<Vector3> connection = null;
                            yield return BuildDryBankConnectionRoutine(prepared, from, shared.points[destination], span.width, baseY, height,
                                value => connection = value, reason => bankFailures.Add("pair=" + pairing + "; side=" + side + "; " + reason));
                            if (connection == null) break;
                            span.bankConnections.Add(connection);
                            yield return null;
                        }
                        connected = span.bankConnections.Count == 2;
                    }
                    if (!connected)
                        throw new InvalidOperationException("Accepted crossing " + span.crossingId +
                            " violates bridge clearance and has no dry-bank connection to " + shared.crossingId + "; " + string.Join(" | ", bankFailures));
                }
                yield return null;
            }
            cache.spans = spans; cache.baseY = baseY; cache.height = height; cache.ready = true;
            completed(spans);
        }
        finally { cache.building = false; }
    }

    internal static bool HasFinalizedBridgeLayout(YQPreparedSpatialMaterializationV2 prepared) =>
        prepared != null && BridgeLayouts.TryGetValue(prepared, out var layout) && layout.finalized;

    internal static int GetBridgePhysicalRevision(YQPreparedSpatialMaterializationV2 prepared) =>
        prepared != null && BridgeLayouts.TryGetValue(prepared, out var layout) ? layout.physicalRevision : 0;

    private static float BridgeHorizontalLength(IReadOnlyList<Vector3> points)
    {
        float length = 0f;
        for (int index = 1; index < points.Count; index++)
            length += Vector2.Distance(new Vector2(points[index - 1].x, points[index - 1].z), new Vector2(points[index].x, points[index].z));
        return length;
    }

    private static AcceptedCrossingSpan CopyBridgeSpan(AcceptedCrossingSpan source)
    {
        // note: Candidate work is private; cancellation and failed candidates leave the already accepted baseline object graph intact.
        var copy = new AcceptedCrossingSpan { crossingId = source.crossingId, routeIndex = source.routeIndex,
            startDistance = source.startDistance, endDistance = source.endDistance, width = source.width,
            points = source.points, routeContacts = source.routeContacts ?? source.points, anchor = source.anchor,
            fitRoute = source.fitRoute, fitStart = source.fitStart, fitEnd = source.fitEnd, naturalDeck = source.naturalDeck };
        copy.bankConnections.AddRange(source.bankConnections);
        return copy;
    }

    private static IEnumerator FinalizeNaturalBridgeLayoutRoutine(YQPreparedSpatialMaterializationV2 prepared,
        Terrain authored, Transform worldRoot)
    {
        if (!UsesNativeBridgeRoutes(prepared)) yield break;
        var cache = BridgeLayouts.GetValue(prepared, _ => new AcceptedBridgeLayout());
        while (cache.finalizing)
        {
            if (authored == null || authored.terrainData == null || worldRoot == null) yield break;
            yield return null;
        }
        if (cache.finalized) yield break;
        if (!cache.ready || authored == null || authored.terrainData == null || worldRoot == null)
            throw new InvalidOperationException("Natural bridge finalization needs its prepared layout and finished authored terrain.");
        // note: Never replace a deck already used by streaming or a previous physical publication. Its exact baseline remains the fallback.
        bool published = cache.materializationStarted;
        foreach (var span in cache.spans)
            published |= worldRoot.Find("AcceptedRiverCrossing_" + SafeSiteName(span.crossingId)) != null;
        if (published) { cache.finalized = true; yield break; }
        cache.finalizing = true;
        try
        {
            var staged = new List<AcceptedCrossingSpan>();
            var copies = new Dictionary<AcceptedCrossingSpan, AcceptedCrossingSpan>();
            foreach (var span in cache.spans) { var copy = CopyBridgeSpan(span); staged.Add(copy); copies.Add(span, copy); }
            foreach (var span in cache.spans) copies[span].bridge = copies[span.bridge ?? span];
            int chosen = 0;
            for (int index = 0; index < staged.Count; index++)
            {
                var baseline = staged[index];
                if (baseline.bridge != baseline) continue;
                // note: Shared deck families retain their proven topology in this bounded first search; no dependent route is silently dropped or rerouted.
                bool shared = false;
                foreach (var dependent in staged) if (dependent != baseline && dependent.bridge == baseline) { shared = true; break; }
                if (shared) continue;
                AcceptedCrossingSpan alternative = null;
                yield return FindNaturalBridgeRoutine(prepared, authored, baseline, staged, value => alternative = value);
                if (authored == null || authored.terrainData == null || worldRoot == null) yield break;
                if (alternative == null) continue;
                alternative.bridge = alternative; staged[index] = alternative; chosen++;
            }
            // note: One publication switches the complete derived layout and invalidates only its existing presentation caches; persisted IDs/hashes and terrain heights do not change.
            if (chosen > 0) { cache.spans = staged; cache.physicalRevision++; }
            cache.finalized = true;
            Debug.Log("[WORLDGEN BRIDGES] Final-terrain natural candidates accepted=" + chosen +
                "; remaining crossings retain their original accepted layout.");
        }
        finally { cache.finalizing = false; }
    }

    private static bool TryGetNaturalBridgeAxis(YQPreparedSpatialMaterializationV2 prepared,
        AcceptedCrossingSpan span, out Vector2 center, out Vector2 normal, out float requiredSpan, out int waterIndex)
    {
        center = default; normal = default; requiredSpan = 0f; waterIndex = -1;
        string waterId = null;
        for (int index = 0; index < prepared.CrossingCount; index++)
        {
            var crossing = prepared.GetCrossing(index);
            if (crossing.crossingId != span.crossingId) continue;
            requiredSpan = crossing.requiredSpan; waterId = crossing.hydrologyId; break;
        }
        for (int index = 0; index < prepared.WaterCount; index++)
            if (prepared.GetWater(index).hydrologyId == waterId) { waterIndex = index; break; }
        if (waterIndex < 0) return false;
        float nearest = float.PositiveInfinity;
        for (int point = 1; point < prepared.GetWaterPointCount(waterIndex); point++)
        {
            var first = prepared.GetWaterPoint(waterIndex, point - 1); var last = prepared.GetWaterPoint(waterIndex, point);
            Vector2 a = new Vector2(first.x, first.z), delta = new Vector2(last.x - first.x, last.z - first.z);
            if (delta.sqrMagnitude < .000001f) continue;
            Vector2 candidate = a + delta * Mathf.Clamp01(Vector2.Dot(span.anchor - a, delta) / delta.sqrMagnitude);
            float squared = (candidate - span.anchor).sqrMagnitude;
            if (squared >= nearest) continue;
            nearest = squared; center = candidate; normal = new Vector2(-delta.y, delta.x).normalized;
        }
        // note: A lake seam or a nearby different water feature cannot substitute for the exact accepted hydrology. Ambiguity retains the original crossing.
        var sample = prepared.SampleTerrain(center.x, center.y);
        float radius = Mathf.Max(16f, Mathf.Max(requiredSpan, prepared.GetWater(waterIndex).nominalWidth) * 1.5f);
        return nearest <= radius * radius && sample.waterFeatureIndex == waterIndex && sample.waterMask > .05f;
    }

    private static IEnumerator FindNaturalBridgeBankRoutine(YQPreparedSpatialMaterializationV2 prepared,
        Vector2 center, Vector2 direction, float width, float requiredSpan, Func<Vector3, float> ground,
        Action<Vector3?> completed)
    {
        // note: Keep the original half-span and three-station dry-bank requirements, but search a bounded straight candidate cooperatively.
        int dryStations = 0; float started = Time.realtimeSinceStartup;
        Vector2 side = new Vector2(-direction.y, direction.x);
        for (int station = 1; station <= 512; station++)
        {
            Vector2 point = center + direction * station;
            bool dry = IsDryBridgeBank(prepared, point, width + 2f) && IsDryBridgeBank(prepared, point, width);
            float centerHeight = ground(new Vector3(point.x, 0f, point.y));
            if (float.IsNaN(centerHeight) || float.IsInfinity(centerHeight)) dry = false;
            for (int rail = -2; dry && rail <= 2; rail++)
            {
                Vector2 p = point + side * ((width + 2f) * .25f * rail);
                var sample = prepared.SampleTerrain(p.x, p.y);
                float height = ground(new Vector3(p.x, 0f, p.y));
                dry = sample.waterMask <= .05f && !float.IsNaN(height) && !float.IsInfinity(height) && height - centerHeight <= .20f;
            }
            dryStations = dry ? dryStations + 1 : 0;
            if (dryStations >= 3 && station >= Mathf.Max(0f, requiredSpan * .5f))
            { completed(new Vector3(point.x, centerHeight + .20f, point.y)); yield break; }
            if (Time.realtimeSinceStartup - started >= YQGeneratedRiverBridge.CooperativeSliceSeconds)
            { yield return null; started = Time.realtimeSinceStartup; }
        }
        completed(null);
    }

    private static IEnumerator FindNaturalBridgeRoutine(YQPreparedSpatialMaterializationV2 prepared, Terrain authored,
        AcceptedCrossingSpan baseline, List<AcceptedCrossingSpan> allSpans, Action<AcceptedCrossingSpan> completed)
    {
        completed(null);
        if (authored == null || authored.terrainData == null) yield break;
        if (!TryGetNaturalBridgeAxis(prepared, baseline, out Vector2 center, out Vector2 normal, out float requiredSpan, out _)) yield break;
        if (!YQGeneratedWorldLayout.TryResolveDryGoddessSummit(prepared, out Vector3 summit)) yield break;
        Vector2 goddess = new Vector2(summit.x, summit.z);
        Vector3 origin = authored.transform.position, size = authored.terrainData.size;
        // note: This finalization pass can certify only already-finished native terrain. Unloaded off-road macro terrain is not replaced by the finite-feature projection.
        float Ground(Vector3 p) => authored == null || authored.terrainData == null ? float.NaN :
            p.x >= origin.x && p.x <= origin.x + size.x && p.z >= origin.z && p.z <= origin.z + size.z
                ? authored.SampleHeight(p) + origin.y : float.NaN;
        float baselineLength = BridgeHorizontalLength(baseline.points), bestLength = baselineLength - 2f, bestBanks = float.PositiveInfinity;
        AcceptedCrossingSpan best = null;
        // note: This finite canonical angular order finds a shorter feasible crossing, not a claimed global optimum. No random stream or traversal order is consumed.
        for (int directionIndex = -4; directionIndex <= 4; directionIndex++)
        {
            if (authored == null || authored.terrainData == null) yield break;
            float angle = directionIndex * 15f * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(normal.x * Mathf.Cos(angle) - normal.y * Mathf.Sin(angle), normal.x * Mathf.Sin(angle) + normal.y * Mathf.Cos(angle));
            var contacts = baseline.routeContacts ?? baseline.points;
            Vector2 forward = new Vector2(contacts[contacts.Count - 1].x - contacts[0].x, contacts[contacts.Count - 1].z - contacts[0].z);
            if (Vector2.Dot(forward, direction) < 0f) direction = -direction;
            Vector3? first = null, last = null;
            yield return FindNaturalBridgeBankRoutine(prepared, center, -direction, baseline.width, requiredSpan, Ground, value => first = value);
            yield return FindNaturalBridgeBankRoutine(prepared, center, direction, baseline.width, requiredSpan, Ground, value => last = value);
            if (authored == null || authored.terrainData == null) yield break;
            if (!first.HasValue || !last.HasValue || Vector3.Distance(first.Value, last.Value) >= bestLength) continue;
            Vector2 a = new Vector2(first.Value.x, first.Value.z), b = new Vector2(last.Value.x, last.Value.z);
            if (!YQGeneratedWorldLayout.IsOriginLandmarkRouteCorridorClear(a, b,
                YQGeneratedWorldLayout.GetNativeOriginTerrainSupportHalfWidth((baseline.width + 2f) * .5f), goddess)) continue;
            var candidate = CopyBridgeSpan(baseline); candidate.bankConnections.Clear(); candidate.points = new List<Vector3>();
            int stations = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
            for (int station = 0; station <= stations; station++)
            {
                Vector2 p = Vector2.Lerp(a, b, station / (float)stations); var sample = prepared.SampleTerrain(p.x, p.y);
                float y = Ground(new Vector3(p.x, 0f, p.y)) + .20f;
                if (sample.waterMask > .05f) y = Mathf.Max(y, origin.y + sample.waterSurfaceNormalized * size.y + .32f);
                candidate.points.Add(new Vector3(p.x, y, p.y));
            }
            bool connected = true;
            for (int side = 0; side < 2; side++)
            {
                List<Vector3> bank = null;
                yield return BuildDryBankConnectionRoutine(prepared, contacts[side == 0 ? 0 : contacts.Count - 1],
                    candidate.points[side == 0 ? 0 : candidate.points.Count - 1], candidate.width, origin.y, size.y, value => bank = value);
                bool traversable = false;
                if (bank != null) yield return CheckNaturalBankRoutine(bank, candidate.width,
                    prepared.GetRoute(candidate.routeIndex).maximumGradeDegrees, Ground,
                    p => prepared.SampleTerrain(p.x, p.z).waterMask <= .05f, value => traversable = value);
                if (!traversable) { connected = false; break; }
                candidate.bankConnections.Add(bank);
            }
            if (!connected) continue;
            if (authored == null || authored.terrainData == null) yield break;
            if (!NaturalBridgeFootprintInside(candidate.points, candidate.width, origin, size)) continue;
            BuildNaturalFitRoute(candidate);
            IReadOnlyList<Vector3> core = BuildNativeBridgeDeckProfile(candidate.points, Ground);
            core = ConstrainNativeBridgeDeckGrade(core, prepared.GetRoute(candidate.routeIndex).maximumGradeDegrees);
            List<Vector3> fitted = null;
            IEnumerator fit = FitNativeBridgeApproachesRoutine(candidate, prepared, authored, core, value => fitted = value);
            try
            {
                while (true)
                {
                    bool more;
                    try { more = fit.MoveNext(); }
                    catch (InvalidOperationException) { fitted = null; break; }
                    if (!more) break;
                    yield return fit.Current;
                }
            }
            finally { (fit as IDisposable)?.Dispose(); }
            if (fitted == null || !NaturalBridgeFootprintInside(fitted, candidate.width, origin, size) ||
                !NaturalBridgeGroundMatches(fitted, candidate.width, Ground)) continue;
            bool conflict = false;
            for (int point = 1; point < fitted.Count; point++)
                if (!YQGeneratedWorldLayout.IsOriginLandmarkRouteCorridorClear(
                    new Vector2(fitted[point - 1].x, fitted[point - 1].z), new Vector2(fitted[point].x, fitted[point].z),
                    YQGeneratedWorldLayout.GetNativeOriginTerrainSupportHalfWidth((candidate.width + 2f) * .5f), goddess))
                { conflict = true; break; }
            foreach (var other in allSpans)
                if (other != baseline && other.bridge == other && CrossingDecksConflict(fitted, candidate.width, other.points, other.width)) { conflict = true; break; }
            if (conflict) continue;
            var firstBank = TrimNaturalBank(candidate.bankConnections[0], fitted[0]);
            var lastBank = TrimNaturalBank(candidate.bankConnections[1], fitted[fitted.Count - 1]);
            if (firstBank == null || lastBank == null || BridgeHorizontalLength(firstBank) < .01f || BridgeHorizontalLength(lastBank) < .01f) continue;
            float length = BridgeHorizontalLength(fitted), banks = BridgeHorizontalLength(firstBank) + BridgeHorizontalLength(lastBank);
            if (length > bestLength || length == bestLength && banks >= bestBanks) continue;
            candidate.points = fitted; candidate.naturalDeck = true; candidate.fitRoute = null;
            candidate.bankConnections.Clear(); candidate.bankConnections.Add(firstBank); candidate.bankConnections.Add(lastBank);
            best = candidate; bestLength = length; bestBanks = banks;
            yield return null;
        }
        completed(best);
    }

    private static void BuildNaturalFitRoute(AcceptedCrossingSpan candidate)
    {
        // note: The strict fitter may use only the proven connectors, never extrapolate a shorter deck back into the old wet interval.
        var route = new List<Vector2>(); float along = 0f;
        void Add(Vector3 p) { var v = new Vector2(p.x, p.z); if (route.Count > 0) { float d = Vector2.Distance(route[route.Count - 1], v); if (d < .001f) return; along += d; } route.Add(v); }
        foreach (var p in candidate.bankConnections[0]) Add(p);
        candidate.fitStart = along;
        foreach (var p in candidate.points) Add(p);
        candidate.fitEnd = along;
        var last = candidate.bankConnections[1];
        for (int index = last.Count - 1; index >= 0; index--) Add(last[index]);
        candidate.fitRoute = route;
    }

    private static List<Vector3> TrimNaturalBank(IReadOnlyList<Vector3> source, Vector3 endpoint)
    {
        var result = new List<Vector3>(); result.Add(source[0]);
        Vector2 p = new Vector2(endpoint.x, endpoint.z);
        for (int index = 1; index < source.Count; index++)
        {
            Vector2 a = new Vector2(source[index - 1].x, source[index - 1].z), d = new Vector2(source[index].x - a.x, source[index].z - a.y);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.000001f, d.sqrMagnitude));
            if ((p - a - d * t).sqrMagnitude <= .0001f)
            { if ((endpoint - result[result.Count - 1]).sqrMagnitude > .000001f) result.Add(endpoint); return result; }
            result.Add(source[index]);
        }
        return null;
    }

    private static bool NaturalBridgeFootprintInside(IReadOnlyList<Vector3> points, float width, Vector3 origin, Vector3 size)
    {
        // note: The native tread bounds miters to four half-widths. This conservative rectangle keeps every fitted shoulder and miter on the same finished terrain datum.
        float radius = 4f * Mathf.Max(1.2f, (width + 2f) * .5f);
        foreach (var point in points)
            if (!IsFinitePoint(point) || point.x - radius < origin.x || point.x + radius > origin.x + size.x ||
                point.z - radius < origin.z || point.z + radius > origin.z + size.z) return false;
        return points.Count >= 2;
    }

    private static bool NaturalBridgeGroundMatches(IReadOnlyList<Vector3> profile, float width, Func<Vector3, float> ground)
    {
        if (profile == null || profile.Count < 2) return false;
        float firstGround = ground(profile[0]), lastGround = ground(profile[profile.Count - 1]);
        if (float.IsNaN(firstGround) || float.IsInfinity(firstGround) || float.IsNaN(lastGround) || float.IsInfinity(lastGround) ||
            profile[0].y + YQGeneratedRiverBridge.WalkSurfaceTopOffset - firstGround > .25f ||
            profile[profile.Count - 1].y + YQGeneratedRiverBridge.WalkSurfaceTopOffset - lastGround > .25f) return false;
        for (int index = 1; index < profile.Count; index++)
        {
            Vector3 first = profile[index - 1], last = profile[index], d = last - first; d.y = 0f;
            float intrusion = MeasureNativeBridgeSegmentFloor(first, last, width, p => ground(p) - Mathf.Lerp(first.y, last.y,
                Mathf.Clamp01(((p.x - first.x) * d.x + (p.z - first.z) * d.z) / Mathf.Max(.000001f, d.sqrMagnitude))));
            if (float.IsNaN(intrusion) || float.IsInfinity(intrusion) || intrusion > YQGeneratedRiverBridge.WalkSurfaceTopOffset - .024f) return false;
        }
        return true;
    }

    private static IEnumerator CheckNaturalBankRoutine(IReadOnlyList<Vector3> points, float width, float maximumGrade,
        Func<Vector3, float> ground, Func<Vector3, bool> dry, Action<bool> completed)
    {
        // note: Match the exact unworn grounded ribbon's 1m rows, width columns and triangle split. No terrain edits or raised substitute floor can make a steep connector pass.
        if (points == null || points.Count < 2 || width <= 0f || float.IsNaN(maximumGrade) ||
            float.IsInfinity(maximumGrade) || maximumGrade <= 0f || maximumGrade >= 90f)
        { completed(false); yield break; }
        float limit = Mathf.Cos(Mathf.Min(maximumGrade, 58f) * Mathf.Deg2Rad);
        bool Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            return IsFinitePoint(a) && IsFinitePoint(b) && IsFinitePoint(c) && dry(a) && dry(b) && dry(c) &&
                normal.sqrMagnitude > .00000001f && Mathf.Abs(normal.normalized.y) >= limit;
        }
        float started = Time.realtimeSinceStartup;
        for (int segment = 1; segment < points.Count; segment++)
        {
            Vector3 a = points[segment - 1], b = points[segment], delta = new Vector3(b.x - a.x, 0f, b.z - a.z);
            if (delta.sqrMagnitude < .0001f) continue;
            Vector3 side = Vector3.Cross(Vector3.up, delta.normalized);
            int rows = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude)), columns = Mathf.Clamp(Mathf.CeilToInt(width), 2, 64);
            Vector3 Point(float along, float across) { Vector3 p = Vector3.Lerp(a, b, along) + side * (width * (across - .5f)); p.y = ground(p) + .025f; return p; }
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                Vector3 al = Point(row / (float)rows, column / (float)columns), ar = Point(row / (float)rows, (column + 1f) / columns);
                Vector3 bl = Point((row + 1f) / rows, column / (float)columns), br = Point((row + 1f) / rows, (column + 1f) / columns);
                if (!Triangle(al, bl, ar) || !Triangle(ar, bl, br)) { completed(false); yield break; }
                if (Time.realtimeSinceStartup - started >= YQGeneratedRiverBridge.CooperativeSliceSeconds)
                { yield return null; started = Time.realtimeSinceStartup; }
            }
        }
        completed(true);
    }

    private static bool IsPhysicalRiverCrossing(YQRouteCrossingKindV2 kind) =>
        kind == YQRouteCrossingKindV2.Bridge || kind == YQRouteCrossingKindV2.Causeway || kind == YQRouteCrossingKindV2.Ford;

    private static bool IsDryBridgeBank(YQPreparedSpatialMaterializationV2 prepared, Vector2 point, float width)
    {
        // note: A shared approach can turn at a bank contact, so prove dry clearance in both horizontal axes as well as along the original deck rails.
        float radius = Mathf.Max(.5f, width * .5f);
        for (int i = 0; i < 5; i++)
        {
            Vector2 offset = i == 1 ? Vector2.right * radius : i == 2 ? Vector2.left * radius : i == 3 ? Vector2.up * radius : i == 4 ? Vector2.down * radius : Vector2.zero;
            if (prepared.SampleTerrain(point.x + offset.x, point.y + offset.y).waterMask > .05f) return false;
        }
        // note: Native bank discovery, shared routing and landing checks must retain the terrain nodes that support the full physical ribbon; archived interpretations stay unchanged.
        return !UsesNativeBridgeRoutes(prepared) || IsNativeBridgeRasterSupportDry(prepared, point, width);
    }

    private sealed class NativeBridgeRasterSupportCache
    {
        // note: Accepted query records are immutable. Keep recut-node facts only for their prepared owner's lifetime and bound retained memory.
        private readonly Dictionary<Vector2Int, bool> survivingNodes = new Dictionary<Vector2Int, bool>();
        internal bool Survives(YQPreparedSpatialMaterializationV2 prepared, Vector2Int node, float origin, float spacing)
        {
            lock (survivingNodes)
            {
                if (survivingNodes.TryGetValue(node, out bool surviving)) return surviving;
                var sample = prepared.SampleTerrain(origin + node.x * spacing, origin + node.y * spacing);
                // note: This is precisely the native channel restoration predicate; shallow shoreline nodes retain their existing support.
                surviving = !(sample.waterMask >= .9f && sample.elevationNormalized < sample.waterSurfaceNormalized);
                if (survivingNodes.Count < 65536) survivingNodes.Add(node, surviving);
                return surviving;
            }
        }
    }
    private static readonly ConditionalWeakTable<YQPreparedSpatialMaterializationV2, NativeBridgeRasterSupportCache> NativeBridgeRasterSupport =
        new ConditionalWeakTable<YQPreparedSpatialMaterializationV2, NativeBridgeRasterSupportCache>();

    private static bool IsNativeBridgeRasterSupportDry(YQPreparedSpatialMaterializationV2 prepared, Vector2 point, float width)
    {
        // note: A worn bank ribbon reaches at most .55 times its accepted width. The extra .5m covers gaps between the existing <=1m segment samples, proving the continuous ribbon's supporting cells.
        float radius = Mathf.Max(.5f, width * .55f) + .5f;
        float minimum = WorldGridOrigin, maximum = minimum + YQGeneratedWorldTerrain.WorldSize;
        if (point.x + radius < minimum || point.x - radius > maximum || point.y + radius < minimum || point.y - radius > maximum) return true;
        // note: Only the finite authored origin heightfield has this grid. Outside-origin streaming retains its canonical terrain authority; crossing footprints test only their overlapping origin cells.
        int lastCell = YQGeneratedWorldTerrain.HeightmapResolution - 2;
        float spacing = YQGeneratedWorldTerrain.WorldSize / (YQGeneratedWorldTerrain.HeightmapResolution - 1);
        int x0 = Mathf.Clamp(Mathf.FloorToInt((point.x - radius - minimum) / spacing), 0, lastCell);
        int x1 = Mathf.Clamp(Mathf.FloorToInt((point.x + radius - minimum) / spacing), 0, lastCell);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((point.y - radius - minimum) / spacing), 0, lastCell);
        int z1 = Mathf.Clamp(Mathf.FloorToInt((point.y + radius - minimum) / spacing), 0, lastCell);
        var cache = NativeBridgeRasterSupport.GetValue(prepared, _ => new NativeBridgeRasterSupportCache());
        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            float left = minimum + x * spacing, bottom = minimum + z * spacing;
            float dx = Mathf.Max(left - point.x, 0f, point.x - left - spacing);
            float dz = Mathf.Max(bottom - point.y, 0f, point.y - bottom - spacing);
            if (dx * dx + dz * dz > radius * radius) continue;
            if (!cache.Survives(prepared, new Vector2Int(x, z), minimum, spacing) ||
                !cache.Survives(prepared, new Vector2Int(x + 1, z), minimum, spacing) ||
                !cache.Survives(prepared, new Vector2Int(x, z + 1), minimum, spacing) ||
                !cache.Survives(prepared, new Vector2Int(x + 1, z + 1), minimum, spacing)) return false;
        }
        return true;
    }

    private static bool CrossingDecksConflict(IReadOnlyList<Vector3> first, float firstWidth,
        IReadOnlyList<Vector3> second, float secondWidth)
    {
        // note: Compare complete deck segments, not only anchors; crossed, curved and nearly parallel bridges all obey the same edge clearance.
        // note: The collision shoulder extends one metre beyond each deck edge; include both shoulders in physical clearance.
        float clearance = MinimumBridgeClearance + 2f + (firstWidth + secondWidth) * 0.5f;
        for (int i = 1; i < first.Count; i++)
        for (int j = 1; j < second.Count; j++)
            if (CrossingSegmentDistanceSquared(
                    new Vector2(first[i - 1].x, first[i - 1].z), new Vector2(first[i].x, first[i].z),
                    new Vector2(second[j - 1].x, second[j - 1].z), new Vector2(second[j].x, second[j].z)) < clearance * clearance)
                return true;
        return false;
    }

    private static bool CrossingDecksIntersect(IReadOnlyList<Vector3> first, IReadOnlyList<Vector3> second)
    {
        for (int i = 1; i < first.Count; i++)
        for (int j = 1; j < second.Count; j++)
            if (CrossingSegmentDistanceSquared(
                    new Vector2(first[i - 1].x, first[i - 1].z), new Vector2(first[i].x, first[i].z),
                    new Vector2(second[j - 1].x, second[j - 1].z), new Vector2(second[j].x, second[j].z)) <= 0.0001f)
                return true;
        return false;
    }

    private static float CrossingSegmentDistanceSquared(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        // note: Include true intersections and degenerate segments before comparing endpoint projections.
        Vector2 ab = b - a, cd = d - c;
        float cross = ab.x * cd.y - ab.y * cd.x;
        if (Mathf.Abs(cross) > .000001f)
        {
            Vector2 ac = c - a;
            float t = (ac.x * cd.y - ac.y * cd.x) / cross;
            float u = (ac.x * ab.y - ac.y * ab.x) / cross;
            if (t >= 0f && t <= 1f && u >= 0f && u <= 1f) return 0f;
        }
        float Point(Vector2 p, Vector2 x, Vector2 y)
        {
            Vector2 delta = y - x;
            float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(p - x, delta) / delta.sqrMagnitude) : 0f;
            return (p - x - delta * t).sqrMagnitude;
        }
        return Mathf.Min(Point(a, c, d), Point(b, c, d), Point(c, a, b), Point(d, a, b));
    }

    private static IEnumerator BuildDryBankConnectionRoutine(YQPreparedSpatialMaterializationV2 prepared, Vector3 from, Vector3 to,
        float width, float baseY, float height, Action<List<Vector3>> completed, Action<string> failed = null)
    {
        // note: Bounded deterministic A* follows dry terrain around curved banks. It cannot shortcut back through the river or invent a second crossing.
        float step = 2f;
        bool nativeRoutes = UsesNativeBridgeRoutes(prepared);
        Vector2 origin = new Vector2(from.x, from.z);
        Vector2 destination = new Vector2(to.x, to.z);
        Vector2 goddessCenter = default;
        if (nativeRoutes)
        {
            // note: Shrine placement may relocate its summit to preserve accepted water and routes. Resolve that same immutable anchor once per bank search instead of protecting the preferred position.
            if (!YQGeneratedWorldLayout.TryResolveDryGoddessSummit(prepared, out Vector3 summit))
            { failed?.Invoke("bank routing has no accepted dry origin shrine anchor"); completed(null); yield break; }
            goddessCenter = new Vector2(summit.x, summit.z);
        }
        // note: A protected endpoint cannot be repaired by bank routing; diagnose this pairing so the existing alternative-pairing search can try another entrance.
        if (nativeRoutes &&
            (!YQGeneratedWorldLayout.IsOriginLandmarkRouteCorridorClear(origin, origin,
                YQGeneratedWorldLayout.GetNativeOriginTerrainSupportHalfWidth(width * .5f), goddessCenter) ||
             !YQGeneratedWorldLayout.IsOriginLandmarkRouteCorridorClear(destination, destination,
                YQGeneratedWorldLayout.GetNativeOriginTerrainSupportHalfWidth(width * .5f), goddessCenter)))
        { failed?.Invoke("bank endpoint overlaps authored origin landmark reserve; from=" + origin + "; to=" + destination); completed(null); yield break; }
        Vector2Int goal = new Vector2Int(Mathf.RoundToInt((to.x - from.x) / step), Mathf.RoundToInt((to.z - from.z) / step));
        int margin = 48;
        int minX = Mathf.Min(0, goal.x) - margin, maxX = Mathf.Max(0, goal.x) + margin;
        int minZ = Mathf.Min(0, goal.y) - margin, maxZ = Mathf.Max(0, goal.y) + margin;
        // note: Coarsen only oversized native search grids; every candidate edge still receives the original 1m full-width dry/reserve checks.
        while (nativeRoutes && (long)(maxX - minX + 1) * (maxZ - minZ + 1) > 65536 && step < 8f)
        {
            step *= 2f;
            goal = new Vector2Int(Mathf.RoundToInt((to.x - from.x) / step), Mathf.RoundToInt((to.z - from.z) / step));
            margin = Mathf.CeilToInt(96f / step);
            minX = Mathf.Min(0, goal.x) - margin; maxX = Mathf.Max(0, goal.x) + margin;
            minZ = Mathf.Min(0, goal.y) - margin; maxZ = Mathf.Max(0, goal.y) + margin;
        }
        if ((long)(maxX - minX + 1) * (maxZ - minZ + 1) > 65536)
        { failed?.Invoke("bank search bounds exceed 65536 cells; from=" + origin + "; to=" + destination); completed(null); yield break; }
        // note: Neighbouring A* edges repeatedly sample the same immutable bank points. Cache exact coordinates only, with a bounded per-search lifetime; no quantization or route changes.
        var drySamples = new Dictionary<Vector2, bool>();
        bool Dry(Vector2 point)
        {
            if (drySamples.TryGetValue(point, out bool dry)) return dry;
            dry = IsDryBridgeBank(prepared, point, width);
            if (drySamples.Count < 65536) drySamples.Add(point, dry);
            return dry;
        }
        bool SegmentDry(Vector2 a, Vector2 b)
        {
            // note: A shared crossing replaces an already-detoured road, so every full-width connector edge must preserve both authored origin landmarks.
            if (nativeRoutes && !YQGeneratedWorldLayout.IsOriginLandmarkRouteCorridorClear(a, b,
                YQGeneratedWorldLayout.GetNativeOriginTerrainSupportHalfWidth(width * .5f), goddessCenter)) return false;
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
            for (int i = 0; i <= count; i++) if (!Dry(Vector2.Lerp(a, b, i / (float)count))) return false;
            return true;
        }
        Vector2 Point(Vector2Int cell) => origin + new Vector2(cell.x, cell.y) * step;
        var open = new SortedSet<(float score, float remaining, int x, int z)>();
        var cost = new Dictionary<Vector2Int, float>(); var previous = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();
        Vector2Int start = Vector2Int.zero; cost[start] = 0f; float initialDistance = Vector2.Distance(origin, destination);
        open.Add((initialDistance, initialDistance, 0, 0));
        Vector2Int[] directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down,
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) };
        float sliceStarted = Time.realtimeSinceStartup;
        while (open.Count > 0 && closed.Count < 16384)
        {
            // note: Bank routing shares the existing cooperative construction budget instead of becoming an indivisible startup search.
            if (Time.realtimeSinceStartup - sliceStarted >= YQGeneratedRiverBridge.CooperativeSliceSeconds)
            { yield return null; sliceStarted = Time.realtimeSinceStartup; }
            var entry = open.Min; open.Remove(entry); var cell = new Vector2Int(entry.x, entry.z);
            if (!closed.Add(cell)) continue;
            Vector2 point = Point(cell);
            if (Vector2.Distance(point, destination) <= step * 1.5f && SegmentDry(point, destination))
            {
                var reverse = new List<Vector2> { destination, point };
                while (previous.TryGetValue(cell, out var parent))
                {
                    // note: Long accepted connectors reconstruct cooperatively without changing their predecessor order.
                    if (Time.realtimeSinceStartup - sliceStarted >= YQGeneratedRiverBridge.CooperativeSliceSeconds)
                    { yield return null; sliceStarted = Time.realtimeSinceStartup; }
                    cell = parent; reverse.Add(Point(cell));
                }
                reverse.Reverse(); var connection = new List<Vector3>(reverse.Count);
                foreach (var p in reverse)
                {
                    // note: Per-point final elevation sampling obeys the same bank-search slice rather than hiding a long completion tail.
                    if (Time.realtimeSinceStartup - sliceStarted >= YQGeneratedRiverBridge.CooperativeSliceSeconds)
                    { yield return null; sliceStarted = Time.realtimeSinceStartup; }
                    connection.Add(new Vector3(p.x, baseY + prepared.SampleTerrain(p.x, p.y).elevationNormalized * height + .20f, p.y));
                }
                completed(connection); yield break;
            }
            foreach (var direction in directions)
            {
                Vector2Int next = cell + direction;
                if (next.x < minX || next.x > maxX || next.y < minZ || next.y > maxZ || closed.Contains(next)) continue;
                float nextCost = cost[cell] + direction.magnitude * step;
                if (cost.TryGetValue(next, out float known) && known <= nextCost) continue;
                // note: One expanded node can perform eight costly dry-corridor probes; yield between probes, retaining the exact search and tie order.
                if (Time.realtimeSinceStartup - sliceStarted >= YQGeneratedRiverBridge.CooperativeSliceSeconds)
                { yield return null; sliceStarted = Time.realtimeSinceStartup; }
                // note: An edge that cannot improve the known route needs no repeated terrain sampling; the search order and accepted costs are unchanged.
                if (!SegmentDry(point, Point(next))) continue;
                cost[next] = nextCost; previous[next] = cell;
                float remaining = Vector2.Distance(Point(next), destination);
                open.Add((nextCost + remaining, remaining, next.x, next.y));
            }
        }
        // note: A rejected connection identifies whether its deterministic search budget or actual dry topology blocked the accepted layout.
        failed?.Invoke("bank search exhausted; visited=" + closed.Count + "; frontier=" + open.Count + "; from=" + origin +
            "; to=" + destination + "; fromDry=" + Dry(origin) + "; toDry=" + Dry(destination));
        completed(null);
    }

    internal static IEnumerator BuildAcceptedOriginCrossingsRoutine(Transform worldRoot, Terrain terrain,
        YQPreparedSpatialMaterializationV2 prepared, YQRuntimeWorldAssetRegistry registry)
    {
        // note: Initial terrain setup and streaming consume this one accepted bridge topology. World-root ownership matches the existing bootstrap bridge lifetime and prevents per-cell copies.
        List<AcceptedCrossingSpan> spans = null;
        yield return PrepareBridgeLayoutRoutine(prepared, terrain, value => spans = value);
        // note: Startup can also retire its terrain while a shared layout is pending; cancellation never certifies an absent accepted deck.
        if (worldRoot == null || terrain == null || terrain.terrainData == null) yield break;
        if (spans == null) throw new InvalidOperationException("Accepted bridge layout was unavailable for a live origin terrain.");
        yield return FinalizeNaturalBridgeLayoutRoutine(prepared, terrain, worldRoot);
        spans = BridgeLayouts.GetValue(prepared, _ => new AcceptedBridgeLayout()).spans;
        yield return BuildAcceptedCrossingsRoutine(worldRoot, spans, registry, null, _ => { }, terrain, prepared, worldRoot);
    }

    private static IEnumerator PrepareAcceptedCrossingSpansRoutine(
        YQPreparedSpatialMaterializationV2 prepared, Terrain terrain, Vector2Int coordinate, float cellSize,
        List<AcceptedCrossingSpan> spans, Action<string, float> substageTelemetry)
    {
        if (prepared == null)
            yield break;
        List<AcceptedCrossingSpan> layout = null;
        yield return PrepareBridgeLayoutRoutine(prepared, terrain, value => layout = value);
        // note: A canceled owner returns no layout. A live owner still requires an actual accepted layout before its spans can be consumed.
        if (terrain == null || terrain.terrainData == null) yield break;
        if (layout == null) throw new InvalidOperationException("Accepted bridge layout was unavailable for a live streamed terrain.");
        float started = Time.realtimeSinceStartup;
        for (int index = 0; index < layout.Count; index++)
        {
            AcceptedCrossingSpan span = layout[index];
            // note: World-owned decks stay resident across cell boundaries. Every intersected cell can reuse their physical proof and remove its ordinary river-crossing ribbon.
            Vector2Int owner = new Vector2Int(
                Mathf.FloorToInt((span.anchor.x - WorldGridOrigin) / Mathf.Max(1f, cellSize)),
                Mathf.FloorToInt((span.anchor.y - WorldGridOrigin) / Mathf.Max(1f, cellSize)));
            float minX = WorldGridOrigin + coordinate.x * cellSize, minZ = WorldGridOrigin + coordinate.y * cellSize;
            bool intersects = owner == coordinate;
            bool Touches(IReadOnlyList<Vector3> path)
            {
                if (path == null) return false;
                for (int point = 1; point < path.Count; point++)
                    if (TryClipSegmentToBounds(path[point - 1].x, path[point - 1].z, path[point].x, path[point].z,
                        minX - span.width * .5f, minX + cellSize + span.width * .5f,
                        minZ - span.width * .5f, minZ + cellSize + span.width * .5f, out _, out _)) return true;
                return false;
            }
            // note: Removing the old wet ribbon and publishing both dry connectors are dependencies even in cells outside the shorter deck.
            intersects |= Touches(span.routeContacts) || Touches(span.points);
            foreach (var connection in span.bankConnections) intersects |= Touches(connection);
            if (intersects)
            {
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
        Action<string, float> substageTelemetry, Action<int> completed, Terrain contactTerrain,
        YQPreparedSpatialMaterializationV2 prepared, Transform sharedWorldRoot = null)
    {
        // note: Bridges keep the existing world-root lifetime from bootstrap; streamed cells reuse their exact accepted identity rather than generating a second deck under a chunk.
        Transform worldRoot = sharedWorldRoot != null ? sharedWorldRoot : parent.parent != null ? parent.parent : parent;
        int built = 0;
        for (int index = 0; index < spans.Count; index++)
        {
            AcceptedCrossingSpan span = spans[index];
            if (UsesNativeBridgeRoutes(prepared))
            {
                var layout = BridgeLayouts.GetValue(prepared, _ => new AcceptedBridgeLayout());
                while (layout.finalizing) yield return null;
                layout.materializationStarted = true;
                foreach (var current in layout.spans)
                    if (current.crossingId == span.crossingId) { span = current; break; }
            }
            AcceptedCrossingSpan bridge = span.bridge ?? span;
            yield return EnsureAcceptedCrossingRootRoutine(worldRoot, bridge, registry, contactTerrain, substageTelemetry, prepared);
            if (!ReferenceEquals(bridge, span))
                yield return EnsureAcceptedCrossingRootRoutine(worldRoot, span, registry, contactTerrain, substageTelemetry, prepared);
            built++;
            yield return null;
        }
        completed?.Invoke(built);
    }

    private static IEnumerator EnsureAcceptedCrossingRootRoutine(Transform worldRoot, AcceptedCrossingSpan span,
        YQRuntimeWorldAssetRegistry registry, Terrain contactTerrain, Action<string, float> telemetry,
        YQPreparedSpatialMaterializationV2 prepared)
    {
        string name = "AcceptedRiverCrossing_" + SafeSiteName(span.crossingId);
        Transform existing = worldRoot.Find(name);
        // note: A second worker may meet an in-flight construction; wait for its explicit completion receipt rather than cloning it.
        while (existing != null && existing.GetComponent<YQStreamedFeatureOverlayTarget>() == null)
        { yield return null; existing = worldRoot.Find(name); }
        if (existing != null)
        {
            // note: Identity reuse still requires the published physical geometry; an empty or mismatched root cannot count as a completed crossing.
            var receipt = existing.GetComponent<YQStreamedFeatureOverlayTarget>();
            if (receipt.featureId != span.crossingId || existing.GetComponentsInChildren<Renderer>(true).Length == 0 ||
                existing.GetComponentsInChildren<MeshCollider>(true).Length == 0)
                throw new InvalidOperationException("Accepted crossing geometry lost: " + span.crossingId);
            yield break;
        }

        var root = new GameObject(name); root.transform.SetParent(worldRoot, false);
        // note: World-owned crossings remain private until their complete visual and collision geometry is available.
        root.SetActive(false);
        bool complete = false;
        try
        {
            if (span.bridge == null || ReferenceEquals(span.bridge, span))
            {
                // note: Native channel layouts are prepared before road grading. Fit the derived deck between the finished banks instead of dipping its tread into the canonical riverbed; keep the cached horizontal layout and archived decks unchanged.
                IReadOnlyList<Vector3> deck = span.points;
                Func<Vector3, float> ground = point => contactTerrain.transform.position.y +
                    prepared.SampleTerrain(point.x, point.z).elevationNormalized * contactTerrain.terrainData.size.y;
                if (UsesNativeBridgeRoutes(prepared))
                {
                    // note: The fixed authored terrain is the finished datum inside the origin. Outside it, retain the accepted canonical datum instead of querying whichever neighboring tile happens to be loaded first.
                    Terrain authored = YQPlayerFollowingSemanticChunkStreamer.Active?.AuthoredTerrain ?? contactTerrain;
                    Vector3 origin = authored.transform.position, size = authored.terrainData.size;
                    var finishedHeight = GetNativeBridgeHeightProjection(prepared, size.y);
                    ground = point =>
                        point.x >= origin.x && point.x <= origin.x + size.x && point.z >= origin.z && point.z <= origin.z + size.z
                            ? authored.SampleHeight(point) + origin.y
                            : origin.y + finishedHeight(point.x, point.z) * size.y;
                    if (span.naturalDeck)
                    {
                        // note: This exact profile already passed the same fitter against final terrain; reject changed support instead of silently moving its frozen route endpoints.
                        if (!NaturalBridgeGroundMatches(span.points, span.width, ground))
                            throw new InvalidOperationException("Finalized natural bridge terrain changed: " + span.crossingId);
                    }
                    else
                    {
                    deck = BuildNativeBridgeDeckProfile(span.points, ground);
                    // note: A finished bank crest can sit between closely spaced route/bank samples. Raise neighboring tread samples into the accepted route's grade envelope rather than retaining a short drop that launches the capsule above its support reach.
                    deck = ConstrainNativeBridgeDeckGrade(deck, prepared.GetRoute(span.routeIndex).maximumGradeDegrees);
                    // note: The tread must also clear its full footprint and descend onto real dry approaches. A centre-only fit left a high entrance step and terrain protruding through the opposite bank.
                    yield return FitNativeBridgeApproachesRoutine(span, prepared, authored, deck, value => deck = value);
                    }
                }
                // note: Keep archived crossing geometry unchanged; native accepted roads publish the width-constrained profile through the same bridge owner and asset registry.
                yield return UsesNativeBridgeRoutes(prepared)?YQGeneratedRiverBridge.BuildNativeRoutine(root.transform,deck,span.width,registry,telemetry,ground):
                    YQGeneratedRiverBridge.BuildRoutine(root.transform,deck,span.width,registry,telemetry,ground);
            }
            if (span.bankConnections.Count > 0)
            {
                Material material = FindMaterial(registry, "road", "dirt", "path") ?? BuildFallbackRoadMaterial();
                // note: Native shared approaches use the same approved texture and soft verge as ordinary ground roads; their physical mesh remains unchanged.
                if (UsesNativeBridgeRoutes(prepared)) BindGroundedRoadSurface(material, contactTerrain);
                RegisterCellResource(root.transform, material);
                // note: World-owned bank paths can extend beyond the initial terrain. Sample their accepted surface there instead of clipping away the approach at that tile's edge.
                foreach (var connection in span.bankConnections)
                    if (BuildSharedBridgeBankPath(root.transform, connection, span.width, material, contactTerrain, prepared, span.naturalDeck) == null)
                        throw new InvalidOperationException("Shared bridge bank path has no physical ribbon: " + span.crossingId);
                yield return null;
            }
            if (root.GetComponentsInChildren<Renderer>(true).Length == 0 || root.GetComponentsInChildren<MeshCollider>(true).Length == 0)
                throw new InvalidOperationException("Accepted crossing has no complete visual/collision geometry: " + span.crossingId);
            var target = root.AddComponent<YQStreamedFeatureOverlayTarget>(); target.featureId = span.crossingId; target.objectId = name;
            root.SetActive(true);
            complete = true;
        }
        finally { if (!complete && root != null) UnityEngine.Object.Destroy(root); }
    }

    private static GameObject BuildSharedBridgeBankPath(Transform parent, List<Vector3> points, float width,
        Material material, Terrain terrain, YQPreparedSpatialMaterializationV2 prepared, bool exactFootprint = false)
    {
        // note: Keep one tessellated mesh/collider owner; the same accepted projection already supplies world-owned bridge elevations.
        float baseY = terrain.transform.position.y, height = terrain.terrainData.size.y;
        var finishedHeight = UsesNativeBridgeRoutes(prepared) ? GetNativeBridgeHeightProjection(prepared, height) : null;
        return BuildGroundedRoadRibbon(parent, "SharedBridgeBankPath", points, width, material, terrain, null,
            point => baseY + (finishedHeight != null ? finishedHeight(point.x, point.y) :
                prepared.SampleTerrain(point.x, point.y).elevationNormalized) * height, exactFootprint);
    }

    internal static List<Vector3> BuildNativeBridgeDeckProfile(IReadOnlyList<Vector3> source, Func<Vector3, float> sampleHeight)
    {
        // note: Bank interpolation removes the bed-following trough; finished terrain crests and accepted water clearance remain lower bounds everywhere along the deck. This projection never changes the accepted records or the shared layout cache.
        if (source == null || source.Count < 2 || sampleHeight == null)
            throw new InvalidOperationException("Native bridge needs two finished bank contacts.");
        float[] distances = new float[source.Count];
        for (int index = 1; index < source.Count; index++)
            distances[index] = distances[index - 1] + Vector2.Distance(new Vector2(source[index - 1].x, source[index - 1].z), new Vector2(source[index].x, source[index].z));
        float[] floors = new float[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            floors[index] = sampleHeight(source[index]) + .20f;
            if (float.IsNaN(floors[index]) || float.IsInfinity(floors[index]))
                throw new InvalidOperationException("Native bridge has invalid finished terrain at " + source[index]);
        }
        float start = floors[0], end = floors[floors.Length - 1];
        if (distances[distances.Length - 1] < .001f)
            throw new InvalidOperationException("Native bridge has invalid finished bank contacts.");
        var result = new List<Vector3>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            Vector3 point = source[index];
            point.y = Mathf.Max(point.y, Mathf.Max(floors[index], Mathf.Lerp(start, end, distances[index] / distances[distances.Length - 1])));
            result.Add(point);
        }
        return result;
    }

    internal static List<Vector3> ConstrainNativeBridgeDeckGrade(IReadOnlyList<Vector3> source, float maximumGradeDegrees)
    {
        // note: Two ordered sweeps form the minimal upward grade envelope along horizontal arclength. Terrain and accepted water clearance stay lower bounds, while source geometry/cache and traversal order remain unchanged.
        if (source == null || source.Count < 2 || float.IsNaN(maximumGradeDegrees) ||
            float.IsInfinity(maximumGradeDegrees) || maximumGradeDegrees <= 0f || maximumGradeDegrees >= 90f)
            throw new InvalidOperationException("Native bridge requires a finite accepted route grade.");
        float grade = Mathf.Tan(maximumGradeDegrees * Mathf.Deg2Rad);
        var result = new List<Vector3>(source.Count);
        var lengths = new float[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            Vector3 point = source[index];
            if (float.IsNaN(point.y) || float.IsInfinity(point.y))
                throw new InvalidOperationException("Native bridge has invalid finished tread height.");
            result.Add(point);
            if (index > 0)
                lengths[index] = Vector2.Distance(new Vector2(point.x, point.z), new Vector2(source[index - 1].x, source[index - 1].z));
        }
        for (int index = 1; index < result.Count; index++)
        {
            Vector3 point = result[index];
            point.y = Mathf.Max(point.y, result[index - 1].y - grade * lengths[index]);
            result[index] = point;
        }
        for (int index = result.Count - 2; index >= 0; index--)
        {
            Vector3 point = result[index];
            point.y = Mathf.Max(point.y, result[index + 1].y - grade * lengths[index + 1]);
            result[index] = point;
        }
        return result;
    }

    internal static float ResolveNativeBridgeEndpointApproachLimit(float terrainHeight, float width, float maximumGradeDegrees)
    {
        if (float.IsNaN(terrainHeight) || float.IsInfinity(terrainHeight) || terrainHeight <= 0f ||
            float.IsNaN(width) || float.IsInfinity(width) || width <= 0f ||
            float.IsNaN(maximumGradeDegrees) || float.IsInfinity(maximumGradeDegrees) || maximumGradeDegrees <= 0f || maximumGradeDegrees >= 90f)
            throw new InvalidOperationException("Native bridge approach extent requires finite terrain, width and grade.");
        // note: Terrain elevations are normalized to one height scale. Allow descent from that full height plus water clearance, and the bounded fourfold shoulder miter; retain the existing bank-discovery envelope as the minimum.
        float limit = Mathf.Max(512f, (terrainHeight + 1.5f) / Mathf.Tan(maximumGradeDegrees * Mathf.Deg2Rad) + 4f * (width + 2f));
        if (float.IsNaN(limit) || float.IsInfinity(limit))
            throw new InvalidOperationException("Native bridge approach extent is not finite.");
        return limit;
    }

    internal static float MeasureNativeBridgeSegmentFloor(Vector3 first, Vector3 last, float width,
        Func<Vector3, float> sampleHeight)
    {
        // note: Measure the complete shoulder corridor between retained route samples, including terrain crests off the centreline; sample order cannot affect this upper bound.
        Vector3 delta = last - first; delta.y = 0f;
        if (sampleHeight == null || !IsFinitePoint(first) || !IsFinitePoint(last) ||
            float.IsNaN(width) || float.IsInfinity(width) || width <= 0f || delta.sqrMagnitude < .000001f)
            throw new InvalidOperationException("Native bridge footprint requires a finite nonzero corridor.");
        Vector3 side = new Vector3(-delta.z, 0f, delta.x).normalized;
        float halfWidth = Mathf.Max(1.2f, width * .5f) + 1f;
        int lengthSteps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude * 2f));
        int widthSteps = Mathf.Max(1, Mathf.CeilToInt(halfWidth * 4f));
        float highest = float.NegativeInfinity;
        for (int along = 0; along <= lengthSteps; along++)
            for (int across = 0; across <= widthSteps; across++)
            {
                Vector3 point = Vector3.Lerp(first, last, (float)along / lengthSteps) +
                    side * Mathf.Lerp(-halfWidth, halfWidth, (float)across / widthSteps);
                float height = sampleHeight(point);
                if (float.IsNaN(height) || float.IsInfinity(height))
                    throw new InvalidOperationException("Native bridge footprint has invalid finished terrain at " + point);
                highest = Mathf.Max(highest, height);
            }
        return highest;
    }

    private static IEnumerator FitNativeBridgeApproachesRoutine(AcceptedCrossingSpan span,
        YQPreparedSpatialMaterializationV2 prepared, Terrain authored, IReadOnlyList<Vector3> core,
        Action<List<Vector3>> completed)
    {
        // note: Landward ramps follow the same accepted road projection and retain every bend. Only version-8 derived presentation changes; cached crossing intervals and accepted records remain untouched.
        List<Vector2> projected = span.fitRoute ?? YQGeneratedWorldEnvironment.GetAcceptedRouteCenterline(prepared, span.routeIndex);
        float fitStart = span.fitRoute != null ? span.fitStart : span.startDistance;
        float fitEnd = span.fitRoute != null ? span.fitEnd : span.endDistance;
        if (projected.Count < 2)
            throw new InvalidOperationException("Native bridge approaches require the accepted route projection.");
        var route = new Vector3[projected.Count]; var distances = new float[projected.Count];
        for (int index = 0; index < projected.Count; index++)
        {
            route[index] = new Vector3(projected[index].x, 0f, projected[index].y);
            if (index > 0) distances[index] = distances[index - 1] + Vector2.Distance(projected[index - 1], projected[index]);
        }
        Vector3 origin = authored.transform.position, size = authored.terrainData.size;
        var finishedHeight = GetNativeBridgeHeightProjection(prepared, size.y);
        // note: Grading is complete before fitting. Retry the same fixed datum without resampling unchanged core and shoulder coordinates; this cache belongs only to this fit invocation.
        var sampledHeights = new Dictionary<Vector2, float>();
        float Sample(Vector3 point)
        {
            // note: A cancelled candidate cannot resume a native sample on its retired terrain owner.
            if (authored == null || authored.terrainData == null)
                throw new InvalidOperationException("Native bridge terrain retired during fitting.");
            var key = new Vector2(point.x, point.z);
            if (sampledHeights.TryGetValue(key, out float height)) return height;
            height = point.x >= origin.x && point.x <= origin.x + size.x &&
                point.z >= origin.z && point.z <= origin.z + size.z ? authored.SampleHeight(point) + origin.y :
                origin.y + finishedHeight(point.x, point.z) * size.y;
            // note: A wet route approach becomes part of the bridge deck; keep the same water clearance while extending to dry land.
            YQSpatialTerrainSampleV2 terrainSample = prepared.SampleTerrain(point.x, point.z);
            if (terrainSample.waterFeatureIndex >= 0 && terrainSample.waterMask > .05f)
                height = Mathf.Max(height, origin.y + terrainSample.waterSurfaceNormalized * size.y + .32f);
            sampledHeights.Add(key, height);
            return height;
        }
        float started = Time.realtimeSinceStartup;
        string failure = "no settled dry approach";
        // note: Bank discovery proves a dry contact, not sufficient descent length. Bound its derived ramp by the accepted polyline and a finite terrain-height/grade budget without changing the accepted plan or bank-discovery limit.
        float endpointApproachLimit = ResolveNativeBridgeEndpointApproachLimit(size.y, span.width,
            prepared.GetRoute(span.routeIndex).maximumGradeDegrees);
        float startLimit = Mathf.Max(0f, fitStart + (span.fitRoute != null ? 0f : endpointApproachLimit));
        float endLimit = Mathf.Max(0f, distances[distances.Length - 1] - fitEnd + (span.fitRoute != null ? 0f : endpointApproachLimit));
        float startExtension = Mathf.Min(4f, startLimit), endExtension = Mathf.Min(4f, endLimit);
        while (true)
        {
            // note: Expose bounded candidate work without presenting a fitted or accepted bridge before its landing checks pass.
            YQStartupLoadingScreen.SetGenerationSubstep("Fitting bridge entrances: " + startExtension.ToString("0") +
                "m / " + endExtension.ToString("0") + "m (limits " + startLimit.ToString("0") + "m / " + endLimit.ToString("0") + "m)");
            var before = new List<float>(); var after = new List<float>();
            float start = fitStart - startExtension, end = fitEnd + endExtension;
            for (float along = start; along < fitStart - .001f; along += 2f) before.Add(along);
            for (float along = fitEnd + 2f; along < end - .001f; along += 2f) after.Add(along);
            after.Add(end);
            foreach (float along in distances)
                if (along > start && along < fitStart - .001f) before.Add(along);
                else if (along > fitEnd + .001f && along < end) after.Add(along);
            before.Sort(); after.Sort();
            var profile = new List<Vector3>();
            void AddApproach(float along)
            {
                Vector3 point = SampleCrossingRoute(route, distances, along, true);
                if (profile.Count > 0 && Vector2.Distance(new Vector2(point.x,point.z),
                    new Vector2(profile[profile.Count - 1].x,profile[profile.Count - 1].z)) < .001f) return;
                // note: Keep every wet route station on the raised deck; endpoint checks below require both ramps to land on a full-width dry bank.
                point.y = Sample(point) + .025f - YQGeneratedRiverBridge.WalkSurfaceTopOffset;
                profile.Add(point);
            }
            foreach (float along in before) AddApproach(along);
            int firstCore = profile.Count; profile.AddRange(core);
            foreach (float along in after) AddApproach(along);
            bool startDry = IsDryBridgeBank(prepared, new Vector2(profile[0].x, profile[0].z), span.width + 2f);
            bool endDry = IsDryBridgeBank(prepared, new Vector2(profile[profile.Count - 1].x, profile[profile.Count - 1].z), span.width + 2f);
            if (!startDry || !endDry)
            {
                failure = "bridge approach has not reached both full-width dry banks; startDry=" + startDry +
                    "; endDry=" + endDry + "; startExtension=" + startExtension.ToString("0.0") +
                    "; endExtension=" + endExtension.ToString("0.0");
                float dryStart = startDry ? startExtension : Mathf.Min(startLimit, startExtension + 4f);
                float dryEnd = endDry ? endExtension : Mathf.Min(endLimit, endExtension + 4f);
                if (!startDry && dryStart <= startExtension || !endDry && dryEnd <= endExtension)
                {
                    failure += "; accepted approach extent exhausted; startLimit=" + startLimit.ToString("0.0") +
                        "; endLimit=" + endLimit.ToString("0.0");
                    break;
                }
                startExtension = dryStart; endExtension = dryEnd;
                YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                yield return null; started = Time.realtimeSinceStartup;
                continue;
            }
            // note: A cropped endpoint may stop inside a true corner's full-width miter. Extend only that endpoint along the same accepted route before fitting; internal folds remain strict geometry failures.
            if (!YQNativeBridgeTread.HasEndpointMiterRunout(profile, span.width, out bool extendStart, out bool extendEnd))
            {
                float runoutStart = extendStart ? Mathf.Min(startLimit, startExtension + 4f) : startExtension;
                float runoutEnd = extendEnd ? Mathf.Min(endLimit, endExtension + 4f) : endExtension;
                failure = "endpoint bend has insufficient full-width miter runout; startExtension=" + startExtension.ToString("0.0") +
                    "; endExtension=" + endExtension.ToString("0.0") + "; extendStart=" + extendStart + "; extendEnd=" + extendEnd;
                if (extendStart && runoutStart <= startExtension || extendEnd && runoutEnd <= endExtension)
                {
                    failure += "; accepted approach extent exhausted; startLimit=" + startLimit.ToString("0.0") +
                        "; endLimit=" + endLimit.ToString("0.0");
                    break;
                }
                startExtension = runoutStart; endExtension = runoutEnd;
                YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                yield return null; started = Time.realtimeSinceStartup;
                continue;
            }
            var floors = new float[profile.Count];
            for (int index = 0; index < floors.Length; index++) floors[index] = profile[index].y;
            // note: Measure terrain relative to the sloped tread, then lift both ends by the required clearance. Assigning the segment's absolute crest to both ends created a step even on a valid linear uphill road.
            for (int index = 1; index < profile.Count; index++)
            {
                Vector3 first = profile[index - 1], last = profile[index], delta = last - first; delta.y = 0f;
                float lift = MeasureNativeBridgeSegmentFloor(first, last, span.width, point =>
                {
                    float t = Mathf.Clamp01(((point.x - first.x) * delta.x + (point.z - first.z) * delta.z) / delta.sqrMagnitude);
                    return Sample(point) - Mathf.Lerp(first.y, last.y, t);
                }) +
                    .025f - YQGeneratedRiverBridge.WalkSurfaceTopOffset;
                floors[index - 1] = Mathf.Max(floors[index - 1], first.y + lift); floors[index] = Mathf.Max(floors[index], last.y + lift);
                if (Time.realtimeSinceStartup - started >= YQGeneratedRiverBridge.CooperativeSliceSeconds)
                {
                    // note: Completed footprint measurements are real fitting progress; the watchdog still requires the unchanged final acceptance gates.
                    YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                    yield return null; started = Time.realtimeSinceStartup;
                }
            }
            for (int index = 0; index < profile.Count; index++)
            { Vector3 point = profile[index]; point.y = floors[index]; profile[index] = point; }
            profile = ConstrainNativeBridgeDeckGrade(profile, prepared.GetRoute(span.routeIndex).maximumGradeDegrees);
            // note: Triangle altitudes across the wider shoulder, rather than centreline distances, bind the native tread grade before the settled-bank check.
            try
            {
                profile = YQNativeBridgeTread.FitProfile(profile,span.width,prepared.GetRoute(span.routeIndex).maximumGradeDegrees);
            }
            catch (InvalidOperationException exception)
            {
                // note: Retain the geometry rejection and identify its owning crossing and bounded approach candidate.
                throw new InvalidOperationException("Native bridge tread rejected: " + span.crossingId +
                    "; startExtension=" + startExtension.ToString("0.0") + "; endExtension=" + endExtension.ToString("0.0") +
                    "; profileStations=" + profile.Count + "; " + exception.Message, exception);
            }
            float startStep = profile[0].y + YQGeneratedRiverBridge.WalkSurfaceTopOffset - Sample(profile[0]);
            float endStep = profile[profile.Count - 1].y + YQGeneratedRiverBridge.WalkSurfaceTopOffset - Sample(profile[profile.Count - 1]);
            if (startStep <= .25f && endStep <= .25f)
            {
                completed(profile); yield break;
            }
            failure = "dry approach has not settled onto the finished road; startStep=" + startStep.ToString("0.000") +
                "; endStep=" + endStep.ToString("0.000") + "; startExtension=" + startExtension.ToString("0.0") +
                "; endExtension=" + endExtension.ToString("0.0") + "; startApproachStations=" + firstCore;
            // note: Test the final fractional endpoint once, and stop when a failing side has no remaining geometry. A settled opposite side never moves solely to consume the search budget.
            float nextStart = startStep > .25f ? Mathf.Min(startLimit, startExtension + 4f) : startExtension;
            float nextEnd = endStep > .25f ? Mathf.Min(endLimit, endExtension + 4f) : endExtension;
            if (startStep > .25f && nextStart <= startExtension || endStep > .25f && nextEnd <= endExtension)
            {
                failure += "; accepted approach extent exhausted; startLimit=" + startLimit.ToString("0.0") +
                    "; endLimit=" + endLimit.ToString("0.0");
                break;
            }
            startExtension = nextStart; endExtension = nextEnd;
            YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
            yield return null; started = Time.realtimeSinceStartup;
        }
        throw new InvalidOperationException("Native bridge approaches rejected: " + span.crossingId + "; " + failure);
    }

    private static IEnumerator FitBridgeBankPathsToTerrainRoutine(Transform root, Terrain terrain)
    {
        // note: Derived bank routing chooses horizontal connectivity; actual published terrain owns every edge's height. Center-only canonical elevations created elevated, intersecting approach colliders on restored roads.
        float started = Time.realtimeSinceStartup;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) throw new InvalidOperationException("Bridge bank path has no mesh.");
            Vector3[] vertices = mesh.vertices;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 point = filter.transform.TransformPoint(vertices[index]);
                if (!YQGeneratedWorldTerrain.TrySamplePlacementHeight(terrain, point, null, out float height))
                    throw new InvalidOperationException("Bridge bank path has no accepted terrain sample at " + point);
                point.y = height + .025f;
                vertices[index] = filter.transform.InverseTransformPoint(point);
                if (Time.realtimeSinceStartup - started >= YQGeneratedRiverBridge.CooperativeSliceSeconds)
                { yield return null; started = Time.realtimeSinceStartup; }
            }
            mesh.vertices = vertices; mesh.RecalculateBounds(); mesh.RecalculateNormals();
            var collider = filter.GetComponent<MeshCollider>();
            if (collider == null) throw new InvalidOperationException("Bridge bank path has no collider.");
            collider.sharedMesh = null; collider.sharedMesh = mesh;
            yield return null;
            started = Time.realtimeSinceStartup;
        }
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
        List<Vector2> projectedRoute = UsesNativeBridgeRoutes(prepared)
            ? YQGeneratedWorldEnvironment.GetAcceptedRouteCenterline(prepared, routeIndex) : null;
        int count = projectedRoute?.Count ?? prepared.GetRoutePointCount(routeIndex);
        if (count < 2 || route.width <= 0f)
            return false;
        Vector3[] routePoints = new Vector3[count];
        float[] distances = new float[count];
        float bestDistanceSquared = float.PositiveInfinity;
        float crossingDistance = 0f;
        Vector2 crossingPoint = new Vector2(crossing.x, crossing.z);
        for (int index = 0; index < count; index++)
        {
            var point = projectedRoute == null
                ? new Vector2(prepared.GetRoutePoint(routeIndex, index).x, prepared.GetRoutePoint(routeIndex, index).z)
                : projectedRoute[index];
            routePoints[index] = new Vector3(point.x, projectedRoute == null ? prepared.GetRoutePoint(routeIndex, index).surfaceElevationNormalized : 0f, point.y);
            if (index == 0) continue;
            Vector2 first = new Vector2(routePoints[index - 1].x, routePoints[index - 1].z);
            Vector2 delta = point - first;
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
        {
            if (projectedRoute == null) { failure = "crossing anchor is outside its accepted route"; return false; }
            // note: Site gates and landmark detours move the physical corridor. First prove the persisted anchor still belongs to its original accepted route, then search the same referenced water around its nearest physical-route station.
            float acceptedDistance = float.PositiveInfinity;
            for (int point = 1; point < prepared.GetRoutePointCount(routeIndex); point++)
            {
                var a = prepared.GetRoutePoint(routeIndex, point - 1); var b = prepared.GetRoutePoint(routeIndex, point);
                Vector2 segmentStart = new Vector2(a.x, a.z), delta = new Vector2(b.x - a.x, b.z - a.z);
                float t = Mathf.Clamp01(Vector2.Dot(crossingPoint - segmentStart, delta) / Mathf.Max(.000001f, delta.sqrMagnitude));
                acceptedDistance = Mathf.Min(acceptedDistance, (crossingPoint - segmentStart - delta * t).sqrMagnitude);
            }
            if (acceptedDistance > searchRadius * searchRadius) { failure = "crossing anchor is outside its original accepted route"; return false; }
        }
        // note: Find the referenced wet interval near the accepted anchor before walking either direction to dry banks.
        float wetDistance = -1f;
        for (float offset = 0f; offset <= searchRadius && wetDistance < 0f; offset += 1f)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float along = Mathf.Clamp(crossingDistance + side * offset, 0f, distances[count - 1]);
                Vector3 point = SampleCrossingRoute(routePoints, distances, along, projectedRoute != null);
                YQSpatialTerrainSampleV2 sample = prepared.SampleTerrain(point.x, point.z);
                // note: Use the compiler's full accepted river/road/shoulder contact corridor. A crossing at a lake seam can be owned by this river even when the sampler selects the connected lake.
                float referencedDistanceSquared = DistanceToWaterFeatureSquared(
                    prepared,
                    waterIndex,
                    new Vector2(point.x, point.z), out float localWaterWidth);
                float contactRadius = Mathf.Max(prepared.GetWater(waterIndex).nominalWidth, localWaterWidth) * 0.5f +
                    route.width * 0.5f + route.shoulderWidth + 7f;
                bool referencedWaterContact = referencedDistanceSquared <= contactRadius * contactRadius;
                if ((sample.waterFeatureIndex == waterIndex && sample.waterMask > 0.05f) || referencedWaterContact)
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
        // note: Nominal width can understate a connected wet corridor; expand only a failed directional search to the hard limit while retaining the full-width dry-bank predicate.
        const float maximumApproachLimit = 512f;
        float approachLimit = Mathf.Min(maximumApproachLimit, Mathf.Max(32f, approachWaterWidth * 4f));
        // note: Native approach fitting proves the shoulder as well as the deck. Discover its banks with the same width so a narrow dry contact cannot permanently reject every landward extension; archived decks keep their existing contacts.
        float bankWidth = UsesNativeBridgeRoutes(prepared) ? route.width + 2f : route.width;
        Func<Vector3, Vector3, bool> settledBank = null;
        if (UsesNativeBridgeRoutes(prepared))
        {
            // note: Extrapolated contacts can reach an authored landmark even when the finite road detours around it. Keep searching until the full shoulder clears the same actual origin reserves as shared routing.
            if (!YQGeneratedWorldLayout.TryResolveDryGoddessSummit(prepared, out Vector3 summit))
            { failure = "native crossing has no accepted dry origin shrine anchor"; return false; }
            Vector2 goddessCenter = new Vector2(summit.x, summit.z);
            float heightScale = terrain.terrainData.size.y;
            var finishedHeight = GetNativeBridgeHeightProjection(prepared, heightScale);
            // note: Dryness alone can select a steep cross-slope or a short dry tongue. A native flat tread needs its real shoulder to meet the same finished road/water surface at the landing.
            settledBank = (point, tangent) =>
            {
                Vector2 bankPoint = new Vector2(point.x, point.z);
                if (!YQGeneratedWorldLayout.IsOriginLandmarkRouteCorridorClear(bankPoint, bankPoint,
                    YQGeneratedWorldLayout.GetNativeOriginTerrainSupportHalfWidth(bankWidth * .5f), goddessCenter)) return false;
                // note: A larger discrete cardinal stencil does not contain the narrower connector's sample positions. Prove both widths so a discovered bank is actually usable by its shared dry-bank path.
                if (!IsDryBridgeBank(prepared, new Vector2(point.x, point.z), route.width)) return false;
                Vector2 side = new Vector2(-tangent.z, tangent.x).normalized;
                float center = finishedHeight(point.x, point.z) * heightScale;
                if (float.IsNaN(center) || float.IsInfinity(center)) return false;
                for (int rail = -2; rail <= 2; rail++)
                {
                    Vector2 offset = side * (bankWidth * .25f * rail);
                    float sample = finishedHeight(point.x + offset.x, point.z + offset.y) * heightScale;
                    if (float.IsNaN(sample) || float.IsInfinity(sample) || sample - center > .20f) return false;
                }
                return true;
            };
        }
        bool startBankFound = TryFindCrossingBank(prepared, routePoints, distances, wetDistance, -1f,
            bankWidth, crossing.requiredSpan, approachLimit, maximumApproachLimit, out float start, settledBank);
        bool endBankFound = TryFindCrossingBank(prepared, routePoints, distances, wetDistance, 1f,
            bankWidth, crossing.requiredSpan, approachLimit, maximumApproachLimit, out float end, settledBank);
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
        var nativeHeight = UsesNativeBridgeRoutes(prepared) ? GetNativeBridgeHeightProjection(prepared, height) : null;
        foreach (float along in samples)
        {
            if (along - previous < 0.001f) continue;
            previous = along;
            Vector3 point = SampleCrossingRoute(routePoints, distances, along, nativeHeight != null);
            YQSpatialTerrainSampleV2 sample = prepared.SampleTerrain(point.x, point.z);
            // note: Ground approaches on the canonical surface; an elevated planning control is not physical support for a flying bank contact.
            float elevation = baseY + (nativeHeight != null ? nativeHeight(point.x, point.z) : sample.elevationNormalized) * height + 0.20f;
            if (sample.waterFeatureIndex >= 0 && sample.waterMask > 0.05f)
                elevation = Mathf.Max(elevation, baseY + sample.waterSurfaceNormalized * height + 0.32f);
            points.Add(new Vector3(point.x, elevation, point.z));
        }
        span = new AcceptedCrossingSpan
        {
            crossingId = crossing.crossingId, routeIndex = routeIndex, startDistance = start, endDistance = end,
            width = route.width, points = points, routeContacts = points
        };
        failure = null;
        return points.Count >= 2;
    }

    private static float DistanceToWaterFeatureSquared(
        YQPreparedSpatialMaterializationV2 prepared,
        int waterIndex,
        Vector2 point)
        => DistanceToWaterFeatureSquared(prepared, waterIndex, point, out _);

    private static float DistanceToWaterFeatureSquared(
        YQPreparedSpatialMaterializationV2 prepared,
        int waterIndex,
        Vector2 point,
        out float localWidth)
    {
        // note: Interpolate width at the nearest accepted water segment exactly as the planning crossing predicate does.
        localWidth = 0f;
        int pointCount = prepared.GetWaterPointCount(waterIndex);
        if (pointCount <= 0)
            return float.PositiveInfinity;

        YQSpatialMaterializationWaterPointV2 first = prepared.GetWaterPoint(waterIndex, 0);
        localWidth = first.width;
        Vector2 previous = new Vector2(first.x, first.z);
        float closestSquared = pointCount == 1 ? (point - previous).sqrMagnitude : float.PositiveInfinity;
        float previousWidth = first.width;
        for (int index = 1; index < pointCount; index++)
        {
            YQSpatialMaterializationWaterPointV2 currentRecord = prepared.GetWaterPoint(waterIndex, index);
            Vector2 current = new Vector2(currentRecord.x, currentRecord.z);
            Vector2 segment = current - previous;
            float denominator = segment.sqrMagnitude;
            float t = denominator > 0.0001f
                ? Mathf.Clamp01(Vector2.Dot(point - previous, segment) / denominator)
                : 0f;
            float squared = (point - (previous + segment * t)).sqrMagnitude;
            if (squared < closestSquared)
            {
                closestSquared = squared;
                localWidth = Mathf.Lerp(previousWidth, currentRecord.width, t);
            }
            previous = current;
            previousWidth = currentRecord.width;
        }
        return closestSquared;
    }

    private static Vector3 SampleCrossingRoute(Vector3[] points, float[] distances, float along, bool nativePrecision)
    {
        if (!nativePrecision) return SampleCrossingRoute(points, distances, along);
        // note: Round native samples once after double interpolation; float cancellation on long legs invented horizontal bends larger than the final coordinate's precision.
        int last = points.Length - 1;
        if (along < 0f || along > distances[last])
        {
            int endpoint = along < 0f ? 0 : last, neighbor = endpoint == 0 ? 1 : last - 1;
            int step = endpoint == 0 ? 1 : -1;
            while (neighbor >= 0 && neighbor <= last && Mathf.Abs(distances[neighbor] - distances[endpoint]) < .001f)
                neighbor += step;
            if (neighbor < 0 || neighbor > last) return points[endpoint];
            double dx = (double)points[endpoint].x - points[neighbor].x, dz = (double)points[endpoint].z - points[neighbor].z;
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (length == 0d) return points[endpoint];
            double extension = Math.Abs((double)along - distances[endpoint]) / length;
            return new Vector3((float)(points[endpoint].x + dx * extension), points[endpoint].y,
                (float)(points[endpoint].z + dz * extension));
        }
        int index = Array.BinarySearch(distances, along);
        if (index >= 0) return points[index];
        index = ~index;
        if (index <= 0) return points[0];
        if (index >= points.Length) return points[last];
        double t = ((double)along - distances[index - 1]) / Math.Max(.001d, (double)distances[index] - distances[index - 1]);
        Vector3 a = points[index - 1], b = points[index];
        return new Vector3((float)(a.x + ((double)b.x - a.x) * t), (float)(a.y + ((double)b.y - a.y) * t),
            (float)(a.z + ((double)b.z - a.z) * t));
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
        float approachLimit, float maximumApproachLimit, out float bank)
        => TryFindCrossingBank(prepared, points, distances, wetDistance, direction, width, requiredSpan,
            approachLimit, maximumApproachLimit, out bank, null);

    private static bool TryFindCrossingBank(YQPreparedSpatialMaterializationV2 prepared,
        Vector3[] points, float[] distances, float wetDistance, float direction, float width, float requiredSpan,
        float approachLimit, float maximumApproachLimit, out float bank, Func<Vector3, Vector3, bool> settledBank)
    {
        bank = wetDistance;
        float dryRun = 0f;
        bool previousDry = false;
        float total = distances[distances.Length - 1];
        float searchLimit = Mathf.Max(0f, approachLimit);
        float hardLimit = Mathf.Max(searchLimit, maximumApproachLimit);
        // note: A full-width native landing can lie beyond the old 512m horizon on an accepted road. Search at most one kilometre from the wet contact; tangent extension still obeys the independent endpoint limit.
        float nativeSearchReach = Mathf.Max(hardLimit, 1024f);
        // note: Search the requested direction first, then extend that same dry-run state once so connected banks outside nominal width remain discoverable.
        float minimum = -searchLimit;
        float maximum = total + searchLimit;
        // note: A qualified native landing search is bounded from its wet contact, not by the length of a multi-kilometre road. Archived bank discovery retains its existing endpoint bounds.
        if (settledBank != null) { minimum = Mathf.Max(minimum, wetDistance - nativeSearchReach); maximum = Mathf.Min(maximum, wetDistance + nativeSearchReach); }
        while (true)
        {
            bool withinBound = direction < 0f ? bank > minimum : bank < maximum;
            if (!withinBound)
            {
                if (searchLimit >= hardLimit || settledBank != null && Mathf.Abs(bank - wetDistance) >= nativeSearchReach)
                    return false;
                searchLimit = hardLimit;
                minimum = -searchLimit;
                maximum = total + searchLimit;
                if (settledBank != null) { minimum = Mathf.Max(minimum, wetDistance - nativeSearchReach); maximum = Mathf.Min(maximum, wetDistance + nativeSearchReach); }
                continue;
            }

            float next = Mathf.Clamp(bank + direction, minimum, maximum);
            bool nativePrecision = settledBank != null;
            Vector3 point = SampleCrossingRoute(points, distances, next, nativePrecision);
            Vector3 tangent = SampleCrossingRoute(points, distances, next + 0.1f, nativePrecision) -
                SampleCrossingRoute(points, distances, next - 0.1f, nativePrecision);
            Vector2 side = new Vector2(-tangent.z, tangent.x).normalized;
            bool dry = true;
            // note: Require two continuous dry metres across the deck width, not merely a dry center sample beside a diagonal bank.
            for (int lateral = -2; lateral <= 2; lateral++)
            {
                Vector2 offset = side * (width * 0.25f * lateral);
                YQSpatialTerrainSampleV2 sample = prepared.SampleTerrain(point.x + offset.x, point.z + offset.y);
                if (sample.waterFeatureIndex >= 0 && sample.waterMask > 0.05f) { dry = false; break; }
            }
            dry &= IsDryBridgeBank(prepared, new Vector2(point.x, point.z), width);
            if (dry && settledBank != null) dry = settledBank(point, tangent);
            dryRun = dry && previousDry ? dryRun + Mathf.Abs(next - bank) : 0f;
            previousDry = dry;
            bank = next;
            if (dryRun >= 2f && Mathf.Abs(bank - wetDistance) >= Mathf.Max(0f, requiredSpan * 0.5f))
                return true;
        }
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

    private sealed class AuthoredWaterCoverage
    {
        internal readonly Dictionary<string, Renderer[]> surfaces = new Dictionary<string, Renderer[]>(StringComparer.Ordinal);
    }
    private static readonly ConditionalWeakTable<Transform, AuthoredWaterCoverage> AuthoredWaterSurfaces = new ConditionalWeakTable<Transform, AuthoredWaterCoverage>();

    private static bool HasPublishedAuthoredWaterCoverage(Transform parent, Terrain terrain,
        YQPreparedSpatialMaterializationV2 prepared, Vector2Int coordinate, float cellSize, int onlyWaterIndex = -1)
    {
        if (prepared == null || parent == null || terrain == null) return false;
        var streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        if (streamer == null || streamer.AuthoredTerrain != terrain) return false;
        Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
        float x = WorldGridOrigin + coordinate.x * cellSize, z = WorldGridOrigin + coordinate.y * cellSize;
        if (x < origin.x || z < origin.z || x + cellSize > origin.x + size.x || z + cellSize > origin.z + size.z) return false;
        Transform world = parent.root;
        // note: Cache only authored physical water receipts. The weak key expires with this materialization; streamed ribbons can never satisfy their own retention check.
        AuthoredWaterCoverage coverage = AuthoredWaterSurfaces.GetValue(world, root =>
        {
            var result = new AuthoredWaterCoverage();
            foreach (var target in root.GetComponentsInChildren<YQStreamedFeatureOverlayTarget>(true))
            {
                var renderers = target.GetComponentsInChildren<Renderer>(true);
                var water = new List<Renderer>();
                foreach (var renderer in renderers)
                    if (renderer.name == "CompiledHydrologySurface" || renderer.name == "CuratedEllipticalWaterSurface") water.Add(renderer);
                if (water.Count > 0 && !string.IsNullOrEmpty(target.featureId)) result.surfaces[target.featureId] = water.ToArray();
            }
            return result;
        });
        var indices = new List<int>();
        if (onlyWaterIndex >= 0) indices.Add(onlyWaterIndex);
        else YQContinuousWorldFeatureAuthority.GetAcceptedWaterIndicesForCell(prepared, coordinate, cellSize, indices);
        if (indices.Count == 0) return false;
        foreach (int index in indices)
        {
            string hydrologyId = prepared.GetWater(index).hydrologyId;
            if (!coverage.surfaces.TryGetValue(hydrologyId, out var renderers) ||
                Array.TrueForAll(renderers, renderer => renderer == null))
            {
                // note: A first cell can bind before the authored water coroutine finishes. Refresh a missing identity instead of permanently caching that incomplete hierarchy.
                foreach (var target in world.GetComponentsInChildren<YQStreamedFeatureOverlayTarget>(true))
                {
                    if (target.featureId != hydrologyId) continue;
                    var published = new List<Renderer>();
                    foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                        if (renderer.name == "CompiledHydrologySurface" || renderer.name == "CuratedEllipticalWaterSurface") published.Add(renderer);
                    if (published.Count > 0) coverage.surfaces[hydrologyId] = published.ToArray();
                }
                if (!coverage.surfaces.TryGetValue(hydrologyId, out renderers)) return false;
            }
            bool present = false;
            foreach (var renderer in renderers)
            {
                // note: Render-only culling preserves authored physical coverage; it must not cause a second streamed river to be built over the retained owner.
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
                Bounds bounds = renderer.bounds;
                var filter = renderer.GetComponent<MeshFilter>();
                present |= filter != null && filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0 &&
                    bounds.max.x >= x && bounds.min.x <= x + cellSize && bounds.max.z >= z && bounds.min.z <= z + cellSize;
            }
            if (!present) return false;
        }
        return true;
    }

    private struct WaterClipVertex
    {
        internal Vector3 position;
        internal Vector2 uv;
        internal Color colour;
        internal static WaterClipVertex Lerp(WaterClipVertex a, WaterClipVertex b, float t) => new WaterClipVertex
        { position = Vector3.LerpUnclamped(a.position, b.position, t), uv = Vector2.LerpUnclamped(a.uv, b.uv, t), colour = Color.LerpUnclamped(a.colour, b.colour, t) };
    }

    internal static void ClipWaterSurfaceToTerrain(Mesh mesh, Transform meshTransform, Terrain terrain)
    {
        DrainRibbonRoutine(ClipWaterSurfaceToTerrainRoutine(mesh, meshTransform, terrain, false));
    }

    private static IEnumerator ClipWaterSurfaceToTerrainRoutine(Mesh mesh, Transform meshTransform, Terrain terrain, bool cooperative)
    {

        if (mesh == null || meshTransform == null || terrain == null || terrain.terrainData == null) yield break;
        // note: Exact shared boundaries prevent padded neighboring ribbons and origin water from drawing on top of one another; interpolate all shader attributes at each cut.
        Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
        Vector3[] source = mesh.vertices; int[] indices = mesh.triangles; Vector2[] sourceUV = mesh.uv; Color[] sourceColours = mesh.colors;
        // note: Most local water meshes already fit their owner; retain their shared vertices and avoid rebuilding those surfaces.
        bool contained = true;
        foreach (Vector3 local in source)
        {
            Vector3 world = meshTransform.TransformPoint(local);
            if (world.x < origin.x || world.x > origin.x + size.x || world.z < origin.z || world.z > origin.z + size.z)
            { contained = false; break; }
        }
        if (contained) yield break;
        var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var colours = new List<Color>(); var triangles = new List<int>();
        var polygon = new List<WaterClipVertex>(7); var clipped = new List<WaterClipVertex>(7);
        WaterClipVertex Read(int index) => new WaterClipVertex { position = meshTransform.TransformPoint(source[index]),
            uv = sourceUV.Length == source.Length ? sourceUV[index] : Vector2.zero,
            colour = sourceColours.Length == source.Length ? sourceColours[index] : new Color(0f, .5f, 1f, 1f) };
        double sliceStarted = Time.realtimeSinceStartupAsDouble;
        for (int triangle = 0; triangle + 2 < indices.Length; triangle += 3)
        {
            if (cooperative && (triangle & 63) == 0 && Time.realtimeSinceStartupAsDouble - sliceStarted >= .0015d)
            { yield return null; sliceStarted = Time.realtimeSinceStartupAsDouble; }
            polygon.Clear(); polygon.Add(Read(indices[triangle])); polygon.Add(Read(indices[triangle + 1])); polygon.Add(Read(indices[triangle + 2]));
            for (int edge = 0; edge < 4 && polygon.Count > 0; edge++)
            {
                clipped.Clear();
                float Distance(Vector3 point) => edge == 0 ? point.x - origin.x : edge == 1 ? origin.x + size.x - point.x : edge == 2 ? point.z - origin.z : origin.z + size.z - point.z;
                for (int i = 0; i < polygon.Count; i++)
                {
                    var current = polygon[i]; var previous = polygon[(i + polygon.Count - 1) % polygon.Count];
                    float cd = Distance(current.position), pd = Distance(previous.position);
                    if ((cd >= 0f) != (pd >= 0f)) clipped.Add(WaterClipVertex.Lerp(previous, current, pd / (pd - cd)));
                    if (cd >= 0f) clipped.Add(current);
                }
                var swap = polygon; polygon = clipped; clipped = swap;
            }
            if (polygon.Count < 3) continue;
            int start = vertices.Count;
            foreach (var vertex in polygon)
            {
                // note: Floating-point interpolation may leave a cut a few micrometres past the plane; publish exact closed-rectangle coordinates.
                Vector3 point = vertex.position;
                point.x = Mathf.Clamp(point.x, origin.x, origin.x + size.x);
                point.z = Mathf.Clamp(point.z, origin.z, origin.z + size.z);
                vertices.Add(meshTransform.InverseTransformPoint(point)); uvs.Add(vertex.uv); colours.Add(vertex.colour);
            }
            for (int i = 1; i + 1 < polygon.Count; i++) { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
        }
        mesh.Clear(); mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetColors(colours); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); mesh.RecalculateNormals();
    }

    private static int BuildAcceptedAreaWaterPatches(
        Transform parent,
        Terrain terrain,
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        float cellSize,
        Material material)
        => BuildAcceptedAreaWaterPatchesCore(parent, terrain, prepared, coordinate, cellSize, material, null);

    private static int BuildAcceptedAreaWaterPatchesCore(
        Transform parent, Terrain terrain, YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate, float cellSize, Material material, HashSet<int> retainedWaterIndices)
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
            if (retainedWaterIndices != null && retainedWaterIndices.Contains(waterIndex)) continue;
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            if (water.kind != YQHydrologyKindV2.Lake && water.kind != YQHydrologyKindV2.Wetland && water.kind != YQHydrologyKindV2.Coastline)
                continue;
            int pointCount = prepared.GetWaterPointCount(waterIndex);
            if (pointCount == 0)
                continue;
            // note: Clip a canonical ellipse to the cell; coarse square tiles both buried shore water and leaked beyond the accepted basin.
            YQContinuousWorldFeatureAuthority.GetAcceptedAreaWaterFootprint(prepared, waterIndex,
                out Vector2 center, out Vector2 longAxis, out float longRadius, out float shortRadius);
            Vector2 shortAxis = new Vector2(-longAxis.y, longAxis.x);
            float radius = Mathf.Max(longRadius, shortRadius);
            if (water.connectedBasin == null &&
                (center.x + radius < minX || center.x - radius > maxX || center.y + radius < minZ || center.y - radius > maxZ))
                continue;
            var first = prepared.GetWaterPoint(waterIndex, 0);
            float surfaceNormalized = water.waterLevelNormalized > 0f ? water.waterLevelNormalized : first.waterSurfaceNormalized;
            float surfaceY = terrain.transform.position.y + terrain.terrainData.size.y * Mathf.Clamp01(surfaceNormalized) + 0.005f;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            if (water.connectedBasin != null)
            {
                // note: Cell water clips the accepted component without reflooding, preserving dry holes and identical shared shoreline crossings.
                var positions = new List<Vector2>();
                water.connectedBasin.AppendClippedTriangles(minX, minZ, maxX, maxZ, positions, triangles);
                foreach (Vector2 position in positions)
                    vertices.Add(parent.InverseTransformPoint(new Vector3(position.x, surfaceY, position.y)));
            }
            var polygon = new List<Vector2>(8);
            var clipped = new List<Vector2>(8);
            const int segments = 96;
            Vector2 Rim(int i)
            {
                float angle = i * Mathf.PI * 2f / segments;
                return center + longAxis * (Mathf.Cos(angle) * longRadius) + shortAxis * (Mathf.Sin(angle) * shortRadius);
            }
            for (int segment = 0; water.connectedBasin == null && segment < segments; segment++)
            {
                polygon.Clear(); polygon.Add(center); polygon.Add(Rim(segment)); polygon.Add(Rim(segment + 1));
                for (int edge = 0; edge < 4; edge++)
                {
                    clipped.Clear();
                    float Distance(Vector2 point) => edge == 0 ? point.x - minX : edge == 1 ? maxX - point.x :
                        edge == 2 ? point.y - minZ : maxZ - point.y;
                    for (int i = 0; i < polygon.Count; i++)
                    {
                        Vector2 current = polygon[i], previous = polygon[(i + polygon.Count - 1) % polygon.Count];
                        float cd = Distance(current), pd = Distance(previous);
                        if ((cd >= 0f) != (pd >= 0f)) clipped.Add(Vector2.Lerp(previous, current, pd / (pd - cd)));
                        if (cd >= 0f) clipped.Add(current);
                    }
                    var swap = polygon; polygon = clipped; clipped = swap;
                }
                if (polygon.Count < 3) continue;
                int start = vertices.Count;
                foreach (Vector2 point in polygon)
                    vertices.Add(parent.InverseTransformPoint(new Vector3(point.x, surfaceY, point.y)));
                for (int i = 1; i + 1 < polygon.Count; i++)
                { triangles.Add(start); triangles.Add(start + i + 1); triangles.Add(start + i); }
            }
            if (water.connectedBasin != null ? triangles.Count == 0 : vertices.Count < 4)
                continue;
            Mesh mesh = new Mesh { name = "ContinuousAreaWaterMesh_" + waterIndex + "_" + coordinate.x + "_" + coordinate.y,
                indexFormat = water.connectedBasin != null ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
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
        => TryGetAcceptedRiverPointsCore(prepared, coordinate, cellSize, terrain, out points, out width, out spanBreaks, out pointWidths, null);

    private static bool TryGetAcceptedRiverPointsCore(YQPreparedSpatialMaterializationV2 prepared, Vector2Int coordinate,
        float cellSize, Terrain terrain, out List<Vector3> points, out float width, out List<int> spanBreaks,
        out List<float> pointWidths, HashSet<int> retainedWaterIndices, List<Vector3> bankOffsets = null)
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
            if (retainedWaterIndices != null && retainedWaterIndices.Contains(waterIndex)) continue;
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
                    // note: An internal bend near a cell edge must not become a full-cell extrapolated branch. Accepted terminal continuation is emitted separately below.
                    baseY, height, 0f, spanBreaks, ref hasPreviousSpan, ref previousSpanEnd, false);
            }
            CompleteAcceptedRibbonFeature(points, pointWidths, spanBreaks, before, water.nominalWidth);
            // note: Preserve accepted control-point width variation, not just one nominal width for the entire river identity.
            for (int emitted = before; emitted < points.Count; emitted++)
            {
                // note: The terrain and origin ribbon already resolve overlapping falls and receiving bodies through this sampler. Raw fall heights otherwise create a second swimmable sheet above the tutorial river.
                Vector3 resolvedPoint = points[emitted];
                var waterSample = prepared.SampleTerrain(resolvedPoint.x, resolvedPoint.z);
                if (waterSample.waterFeatureIndex >= 0)
                    resolvedPoint.y = baseY + waterSample.waterSurfaceNormalized * height + .005f;
                points[emitted] = resolvedPoint;
                float closest = float.PositiveInfinity;
                Vector3 acceptedOffset = Vector3.zero;
                Vector2 sample = new Vector2(points[emitted].x, points[emitted].z);
                for (int segmentIndex = firstSegment; segmentIndex <= lastSegment; segmentIndex++)
                {
                    var first = prepared.GetWaterPoint(waterIndex, segmentIndex);
                    var second = prepared.GetWaterPoint(waterIndex, segmentIndex + 1);
                    Vector2 start = new Vector2(first.x, first.z), delta = new Vector2(second.x - first.x, second.z - first.z);
                    float t = Mathf.Clamp01(Vector2.Dot(sample - start, delta) / Mathf.Max(0.0001f, delta.sqrMagnitude));
                    float distance = (sample - start - delta * t).sqrMagnitude;
                    if (distance >= closest) continue;
                    closest = distance;
                    pointWidths[emitted] = Mathf.Max(water.nominalWidth, Mathf.Lerp(first.width, second.width, t));
                    if (bankOffsets != null)
                        acceptedOffset = Vector3.Lerp(prepared.GetAcceptedChannelBankOffset(waterIndex, segmentIndex),
                            prepared.GetAcceptedChannelBankOffset(waterIndex, segmentIndex + 1), t);
                }
                // note: Carry the original accepted frame through clipping; the mesh must not derive a different corner from this cell's shortened span.
                bankOffsets?.Add(acceptedOffset);
            }
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
            // note: The existing terminal ray owns a straight constant-width frame independently of clipped cell points.
            if (bankOffsets != null)
                for (int emitted = before; emitted < points.Count; emitted++)
                    bankOffsets.Add(new Vector3(-continuation.direction.y, 0f, continuation.direction.x) * continuation.width * .5f);
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
            // note: Exclude the complete wet interval in every intersected cell; world-owned bridge residency is guaranteed before this cell publishes.
            List<AcceptedCrossingSpan> routeCrossings = new List<AcceptedCrossingSpan>();
            if (crossingSpans != null)
                for (int index = 0; index < crossingSpans.Count; index++)
                    if (crossingSpans[index].routeIndex == routeIndex) routeCrossings.Add(crossingSpans[index]);
            routeCrossings.Sort((left, right) => left.startDistance.CompareTo(right.startDistance));
            List<Vector2> physicalRoute = routeCrossings.Count > 0 && UsesNativeBridgeRoutes(prepared)
                ? YQGeneratedWorldEnvironment.GetAcceptedRouteCenterline(prepared, routeIndex) : null;
            if (physicalRoute != null) pointCount = physicalRoute.Count;
            float routeDistance = 0f;
            int firstSegment = 0;
            int lastSegment = pointCount - 2;
            if (physicalRoute == null && prepared.TryGetRouteSegmentRangeForCell(
                    routeIndex, coordinate, cellSize,
                    out int indexedFirstSegment, out int indexedLastSegment))
            {
                firstSegment = indexedFirstSegment;
                lastSegment = indexedLastSegment;
            }
            else if (physicalRoute == null && pointCount > 1 &&
                     Mathf.Abs(cellSize - YQSemanticWorldAuthority.CellSizeMeters) <= 0.01f)
            {
                continue;
            }
            for (int pointIndex = routeCrossings.Count > 0 ? 0 : firstSegment; pointIndex <= lastSegment; pointIndex++)
            {
                YQSpatialMaterializationRoutePointV2 first = prepared.GetRoutePoint(routeIndex, Mathf.Min(pointIndex, prepared.GetRoutePointCount(routeIndex) - 1));
                YQSpatialMaterializationRoutePointV2 second = prepared.GetRoutePoint(routeIndex, Mathf.Min(pointIndex + 1, prepared.GetRoutePointCount(routeIndex) - 1));
                if (physicalRoute != null)
                {
                    first.x = physicalRoute[pointIndex].x; first.z = physicalRoute[pointIndex].y;
                    second.x = physicalRoute[pointIndex + 1].x; second.z = physicalRoute[pointIndex + 1].y;
                    first.surfaceElevationNormalized = prepared.SampleTerrain(first.x, first.z).elevationNormalized;
                    second.surfaceElevationNormalized = prepared.SampleTerrain(second.x, second.z).elevationNormalized;
                }
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
            // note: A short cell overlap on a long source segment still needs both endpoints; world-space spacing below removes only physically duplicate vertices.
            if (!float.IsNaN(previousT) && t == previousT)
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

    private static int BuildRibbonSpans(Transform parent, string name, List<Vector3> points, List<int> spanBreaks,
        float width, Material material, bool water, float cellSize, IReadOnlyList<float> pointWidths = null, Terrain contactTerrain = null)
    {
        // note: Retain the historical private entry point used by existing publication callers and Editor contract probes; all spans still share one builder.
        return BuildRibbonSpansCore(parent, name, points, spanBreaks, width, material, water, cellSize, pointWidths, contactTerrain, null);
    }

    private static int BuildRibbonSpansCore(
        Transform parent,
        string name,
        List<Vector3> points,
        List<int> spanBreaks,
        float width,
        Material material,
        bool water,
        float cellSize,
        IReadOnlyList<float> pointWidths = null,
        Terrain contactTerrain = null,
        IReadOnlyList<Vector3> bankOffsets = null,
        YQPreparedSpatialMaterializationV2 nativeWater = null)
    {
        int built = 0;
        DrainRibbonRoutine(BuildRibbonSpansRoutine(parent, name, points, spanBreaks, width, material, water, cellSize, pointWidths, contactTerrain, bankOffsets, nativeWater, value => built = value, false));
        return built;
    }

    private static IEnumerator BuildRibbonSpansRoutine(
        Transform parent,
        string name,
        List<Vector3> points,
        List<int> spanBreaks,
        float width,
        Material material,
        bool water,
        float cellSize,
        IReadOnlyList<float> pointWidths,
        Terrain contactTerrain,
        IReadOnlyList<Vector3> bankOffsets,
        YQPreparedSpatialMaterializationV2 nativeWater, Action<int> completed, bool cooperative)
    {

        if (points == null || points.Count < 2)
            yield break;
        // note: A malformed width stream fails publication instead of borrowing a neighboring feature's appearance.
        if (pointWidths != null && pointWidths.Count != points.Count)
            yield break;
        if (bankOffsets != null && bankOffsets.Count != points.Count) yield break;
        int built = 0;
        float spanWidth = width;
        List<Vector3> span = new List<Vector3>();
        List<float> spanWidths = new List<float>();
        var spanOffsets = bankOffsets != null ? new List<Vector3>() : null;
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
                if (span.Count >= 2)
                {
                    GameObject ribbon = null;
                    yield return BuildRibbonCoreWithJoinsRoutine(parent, name + "_Span" + spanIndex, span, spanWidth, material, water, contactTerrain, spanWidths, spanOffsets, nativeWater, value => ribbon = value, cooperative);
                    if (ribbon != null) built++;
                }
                spanIndex++;
                span.Clear();
                spanWidths.Clear();
                spanOffsets?.Clear();
                while (nextBreak < (spanBreaks != null ? spanBreaks.Count : 0) && index >= spanBreaks[nextBreak])
                    nextBreak++;
            }
            if (span.Count == 0)
                spanWidth = pointWidths != null ? pointWidths[index] : width;
            if (span.Count == 0 || (span[span.Count - 1] - point).sqrMagnitude > 0.0004f)
            {
                span.Add(point);
                spanWidths.Add(pointWidths != null ? pointWidths[index] : width);
                spanOffsets?.Add(bankOffsets[index]);
            }
        }
        if (span.Count >= 2)
        {
            GameObject ribbon = null;
            yield return BuildRibbonCoreWithJoinsRoutine(parent, name + "_Span" + spanIndex, span, spanWidth, material, water, contactTerrain, spanWidths, spanOffsets, nativeWater, value => ribbon = value, cooperative);
            if (ribbon != null) built++;
        }
        completed?.Invoke(built);
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
            128f, null, terrain);
    }

    private static void DrainRibbonRoutine(IEnumerator routine)
    {
        // note: Only compatibility calls drain without frame yields; nested kernels retain their normal disposal and cancellation cleanup.
        var pending = new Stack<IEnumerator>(); pending.Push(routine);
        try
        {
            while (pending.Count > 0)
            {
                var current = pending.Peek();
                if (!current.MoveNext()) { pending.Pop(); (current as IDisposable)?.Dispose(); }
                else if (current.Current is IEnumerator nested) pending.Push(nested);
            }
        }
        finally { while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose(); }
    }

    private sealed class MeasuredWaterRoutine : IEnumerator, IDisposable
    {
        private readonly IEnumerator inner;
        private readonly Action<string, float> telemetry;
        internal MeasuredWaterRoutine(IEnumerator inner, Action<string, float> telemetry) { this.inner = inner; this.telemetry = telemetry; }
        // note: Wrap only nested work, preserving the streamer's scheduling and charging actual MoveNext CPU rather than time spent waiting between frames.
        public object Current => inner.Current is IEnumerator nested ? new MeasuredWaterRoutine(nested, telemetry) : inner.Current;
        public bool MoveNext()
        {
            float started = Time.realtimeSinceStartup;
            try { return inner.MoveNext(); }
            finally { telemetry?.Invoke("riverRibbonBuild", Mathf.Max(0f, Time.realtimeSinceStartup - started)); }
        }
        public void Reset() => throw new NotSupportedException();
        public void Dispose() => (inner as IDisposable)?.Dispose();
    }

    private static GameObject BuildRibbon(Transform parent, string name, List<Vector3> points, float width, Material material, bool water, Terrain contactTerrain = null, IReadOnlyList<float> widths = null, IReadOnlyList<Vector3> bankOffsets = null)
    {
        using (RibbonMeshBuildMarker.Auto())
            return BuildRibbonCore(parent, name, points, width, material, water, contactTerrain, widths, bankOffsets);
    }

    private static GameObject BuildRibbonCore(Transform parent, string name, List<Vector3> points, float width, Material material, bool water, Terrain contactTerrain, IReadOnlyList<float> widths, IReadOnlyList<Vector3> bankOffsets = null)
        => BuildRibbonCoreWithJoins(parent, name, points, width, material, water, contactTerrain, widths, bankOffsets, null);

    private static GameObject BuildRibbonWithJoins(Transform parent, string name, List<Vector3> points, float width, Material material, bool water, Terrain contactTerrain, IReadOnlyList<float> widths, IReadOnlyList<Vector3> bankOffsets, YQPreparedSpatialMaterializationV2 nativeWater)
    {
        // note: Keep the existing mesh-build profiler scope around the native replacement path as well as legacy ribbons.
        using (RibbonMeshBuildMarker.Auto())
            return BuildRibbonCoreWithJoins(parent, name, points, width, material, water, contactTerrain, widths, bankOffsets, nativeWater);
    }

    private static GameObject BuildRibbonCoreWithJoins(Transform parent, string name, List<Vector3> points, float width, Material material, bool water, Terrain contactTerrain, IReadOnlyList<float> widths, IReadOnlyList<Vector3> bankOffsets, YQPreparedSpatialMaterializationV2 nativeWater)
    {
        // note: Compatibility callers retain the exact geometry kernel; production streamed water advances that kernel through its existing publication iterator owner.
        GameObject result = null;
        DrainRibbonRoutine(BuildRibbonCoreWithJoinsRoutine(parent, name, points, width, material, water, contactTerrain, widths, bankOffsets, nativeWater, value => result = value, false));
        return result;
    }

    private static IEnumerator BuildRibbonCoreWithJoinsRoutine(Transform parent, string name, List<Vector3> points, float width, Material material, bool water, Terrain contactTerrain, IReadOnlyList<float> widths, IReadOnlyList<Vector3> bankOffsets, YQPreparedSpatialMaterializationV2 nativeWater, Action<GameObject> completed, bool cooperative)
    {
        Mesh mesh = null;
        GameObject root = null;
        bool delivered = false;
        try
        {
        if (points == null || points.Count < 2 || parent == null || material == null)
            yield break;
        if (bankOffsets != null && (bankOffsets.Count != points.Count || !water)) yield break;
        // note: Grounded roads need samples across their width as well as along the route; water retains its accepted level.
        if (!water && contactTerrain != null)
        { completed?.Invoke(BuildGroundedRoadRibbon(parent, name, points, width, material, contactTerrain, widths)); yield break; }
        if (!water && contactTerrain != null)
        {
            // note: Match the heightfield's two-metre detail rather than bridging terrain undulations with eight-metre route triangles.
            var sampled = new List<Vector3>();
            var sampledWidths = new List<float>();
            for (int segment = 0; segment + 1 < points.Count; segment++)
            {
                Vector3 first = points[segment], second = points[segment + 1];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z)) / 2f));
                for (int step = 0; step < steps; step++)
                {
                    float t = step / (float)steps;
                    sampled.Add(Vector3.Lerp(first, second, t));
                    sampledWidths.Add(widths == null ? width : Mathf.Lerp(widths[segment], widths[segment + 1], t));
                }
            }
            sampled.Add(points[points.Count - 1]);
            sampledWidths.Add(widths == null ? width : widths[widths.Count - 1]);
            points = sampled;
            widths = sampledWidths;
        }
        int count = points.Count;
        Vector3[] vertices = new Vector3[count * 2];
        Vector2[] uvs = new Vector2[count * 2];
        Color[] colours = water ? new Color[count * 2] : null;
        int[] triangles = new int[(count - 1) * 6];
        for (int index = 0; index < count; index++)
        {
            // note: Reject non-finite source points before they can poison a mesh; malformed persisted geometry must fail closed as a missing span.
            if (!IsFinitePoint(points[index]))
                yield break;
            Vector3 previousDirection = index > 0
                ? new Vector3(points[index].x - points[index - 1].x, 0f, points[index].z - points[index - 1].z).normalized
                : Vector3.zero;
            Vector3 nextDirection = index + 1 < count
                ? new Vector3(points[index + 1].x - points[index].x, 0f, points[index + 1].z - points[index].z).normalized
                : Vector3.zero;
            Vector3 direction = index == 0 ? nextDirection : index == count - 1 ? previousDirection : (previousDirection + nextDirection).normalized;
            if (direction.sqrMagnitude < 0.0001f)
                direction = nextDirection.sqrMagnitude >= 0.0001f ? nextDirection : previousDirection;
            float localWidth = widths != null ? widths[index] : width;
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized * localWidth * 0.5f;
            if (bankOffsets != null)
            {
                // note: New accepted finite and terminal banks arrive from their canonical frame, preserving exact edges at bends and cell borders.
                side = bankOffsets[index];
                if (!IsFinitePoint(side)) yield break;
            }
            else if (index > 0 && index + 1 < count && previousDirection.sqrMagnitude >= 0.0001f && nextDirection.sqrMagnitude >= 0.0001f)
            {
                // note: Use a capped miter at bends so a near-reversal cannot turn a normal path width into a long triangular spike.
                Vector3 miter = Vector3.Cross(Vector3.up, direction).normalized;
                float miterDenominator = Vector3.Dot(miter, Vector3.Cross(Vector3.up, nextDirection).normalized);
                if (Mathf.Abs(miterDenominator) > 0.2f)
                    side = miter * Mathf.Clamp((localWidth * 0.5f) / miterDenominator, -localWidth * 0.56f, localWidth * 0.56f);
            }
            // note: Source splines are authoritative world coordinates; convert them into the owning root's local space so a translated/scaled world root cannot displace the published ribbon.
            // note: World-space repeats keep approved road textures at a stable scale across chunks instead of stretching one sample along an entire ribbon.
            Vector3 left = points[index] - side;
            Vector3 right = points[index] + side;
            if (!water && contactTerrain != null)
            {
                // note: Share the origin path's actual terrain contact calculation. Each edge follows cross-slopes independently; crossing decks remain separately owned.
                if (YQGeneratedWorldEnvironment.TrySampleLivedPathSurface(contactTerrain, new Vector2(left.x, left.z), out Vector3 groundedLeft, out _)) left = groundedLeft;
                if (YQGeneratedWorldEnvironment.TrySampleLivedPathSurface(contactTerrain, new Vector2(right.x, right.z), out Vector3 groundedRight, out _)) right = groundedRight;
            }
            vertices[index * 2] = parent.InverseTransformPoint(left);
            vertices[index * 2 + 1] = parent.InverseTransformPoint(right);
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
            if (bankOffsets != null)
            {
                // note: The canonical frame has the opposite lateral orientation to the historical streamed miter; use the origin surface's upward winding.
                triangles[triangle] = vertex + 1; triangles[triangle + 1] = vertex + 2; triangles[triangle + 2] = vertex;
                triangles[triangle + 3] = vertex + 3; triangles[triangle + 4] = vertex + 2; triangles[triangle + 5] = vertex + 1;
            }
        }
        if (cooperative) yield return null;
        mesh = new Mesh { name = name + "_Mesh" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        if (water)
            mesh.colors = colours;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        if (water && nativeWater != null)
            for (int join = 0; join < nativeWater.NativeJoinCount; join++)
                yield return nativeWater.GetNativeJoin(join).ApplyToMeshRoutine(mesh, parent, contactTerrain, -1, false, null, cooperative);
        root = new GameObject(name);
        YQContinuousWorldOwnedResources resources = root.AddComponent<YQContinuousWorldOwnedResources>();
        resources.Register(mesh);
        root.transform.SetParent(parent, false);
        MeshFilter filter = root.AddComponent<MeshFilter>();
        MeshRenderer renderer = root.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (water)
        {
            yield return ClipWaterSurfaceToTerrainRoutine(mesh, root.transform, contactTerrain, cooperative);
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
        completed?.Invoke(root);
        delivered = true;

        }
        finally
        {
            // note: A cancelled cell cannot strand a native mesh before its owned-resource component is created.
            if (!delivered)
            {
                if (root != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root); }
                else if (mesh != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh); }
            }
        }
    }

    private static bool HasNativeChannelJoinDemand(YQPreparedSpatialMaterializationV2 prepared, Terrain terrain, HashSet<int> retained)
    {
        if (prepared == null || terrain == null || terrain.terrainData == null) return false;
        Vector3 minimum = terrain.transform.position, maximum = minimum + terrain.terrainData.size;
        for (int join = 0; join < prepared.NativeJoinCount; join++)
        for (int channel = 0; channel < prepared.WaterCount; channel++)
            if ((retained == null || !retained.Contains(channel)) && prepared.GetNativeJoin(join).IntersectsChannelRectangle(
                channel, minimum.x, minimum.z, maximum.x, maximum.z)) return true;
        return false;
    }

    private static int BuildNativeChannelJoins(Transform parent, YQPreparedSpatialMaterializationV2 prepared,
        Terrain terrain, Material material, HashSet<int> retained)
    {
        int built = 0;
        DrainRibbonRoutine(BuildNativeChannelJoinsRoutine(parent, prepared, terrain, material, retained, value => built = value, false));
        return built;
    }

    private static IEnumerator BuildNativeChannelJoinsRoutine(Transform parent, YQPreparedSpatialMaterializationV2 prepared,
        Terrain terrain, Material material, HashSet<int> retained, Action<int> completed, bool cooperative)
    {

        // note: Publish each accepted channel patch once per cell, independently of ribbon span breaks, before motion caches its vertices.
        if (parent == null || prepared == null || terrain == null || terrain.terrainData == null || material == null) yield break;
        Vector3 minimum = terrain.transform.position, maximum = minimum + terrain.terrainData.size;
        int built = 0;
        for (int join = 0; join < prepared.NativeJoinCount; join++)
        for (int channel = 0; channel < prepared.WaterCount; channel++)
        {
            var patch = prepared.GetNativeJoin(join);
            if ((retained != null && retained.Contains(channel)) || !patch.IntersectsChannelRectangle(
                channel, minimum.x, minimum.z, maximum.x, maximum.z)) continue;
            var root = new GameObject("ContinuousNativeJoin_" + join + "_" + prepared.GetWater(channel).hydrologyId);
            root.transform.SetParent(parent, false);
            var mesh = new Mesh { name = root.name + "_Mesh" };
            root.AddComponent<YQContinuousWorldOwnedResources>().Register(mesh);
            yield return patch.ApplyToMeshRoutine(mesh, root.transform, terrain, channel, true, null, cooperative);
            yield return ClipWaterSurfaceToTerrainRoutine(mesh, root.transform, terrain, cooperative);
            var filter = root.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            root.AddComponent<YQWaterSurfaceMotion>().Configure(filter, renderer, prepared.GetWater(channel).kind, 1.2f);
            var collider = root.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; collider.enabled = false;
            built++;
            if (cooperative) yield return null;
        }
        completed?.Invoke(built);
    }

    private static GameObject BuildGroundedRoadRibbon(Transform parent, string name, List<Vector3> points,
        float width, Material material, Terrain terrain, IReadOnlyList<float> widths, Func<Vector2, float> worldHeight = null,
        bool preserveAcceptedFootprint = false)
    {
        // note: Clip each tessellated triangle to its terrain owner instead of retaining ungrounded vertices beyond a cell edge.
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        var colours = new List<Color>();
        var triangles = new List<int>();
        var vertexByPosition = new Dictionary<Vector2, int>();
        var polygonIndices = new List<int>(7);
        Vector3 origin = terrain.GetPosition(), size = terrain.terrainData.size;
        Vector3 currentStart = Vector3.zero, currentEnd = Vector3.zero;
        float currentStartWidth = width, currentEndWidth = width;
        var polygon = new List<Vector3>(7);
        var clipped = new List<Vector3>(7);
        void Emit(Vector3 a, Vector3 b, Vector3 c)
        {
            polygon.Clear(); polygon.Add(a); polygon.Add(b); polygon.Add(c);
            for (int edge = 0; worldHeight == null && edge < 4; edge++)
            {
                clipped.Clear();
                float Distance(Vector3 point) => edge == 0 ? point.x - origin.x : edge == 1 ? origin.x + size.x - point.x :
                    edge == 2 ? point.z - origin.z : origin.z + size.z - point.z;
                for (int i = 0; i < polygon.Count; i++)
                {
                    Vector3 current = polygon[i], previous = polygon[(i + polygon.Count - 1) % polygon.Count];
                    float cd = Distance(current), pd = Distance(previous);
                    if ((cd >= 0f) != (pd >= 0f)) clipped.Add(Vector3.Lerp(previous, current, pd / (pd - cd)));
                    if (cd >= 0f) clipped.Add(current);
                }
                var swap = polygon; polygon = clipped; clipped = swap;
            }
            if (polygon.Count < 3) return;
            polygonIndices.Clear();
            foreach (Vector3 point in polygon)
            {
                Vector2 xz = worldHeight != null ? new Vector2(point.x, point.z) :
                    new Vector2(Mathf.Clamp(point.x, origin.x, origin.x + size.x), Mathf.Clamp(point.z, origin.z, origin.z + size.z));
                // note: Adjacent triangles reuse the exact same terrain sample and vertex; the former six copies each repeated native height/normal queries and tangent work.
                if (vertexByPosition.TryGetValue(xz, out int existingIndex)) { polygonIndices.Add(existingIndex); continue; }
                if (!YQGeneratedWorldEnvironment.TrySampleLivedPathSurface(terrain, xz, out Vector3 grounded, out Vector3 normal))
                {
                    if (worldHeight == null) return;
                    // note: Only world-owned accepted approaches have this sampler. Cell-owned roads retain their terrain clipping contract.
                    grounded = new Vector3(xz.x, worldHeight(xz) + .025f, xz.y);
                    normal = Vector3.up;
                    if (!IsFinitePoint(grounded)) throw new InvalidOperationException("Accepted bank surface is not finite at " + xz);
                }
                int vertexIndex = vertices.Count;
                vertexByPosition.Add(xz, vertexIndex); polygonIndices.Add(vertexIndex);
                vertices.Add(parent.InverseTransformPoint(grounded));
                uvs.Add(xz * 0.25f);
                // note: Clipped triangles have separate vertices. Use the terrain's continuous normals instead of introducing a lighting seam on every triangle.
                normals.Add(parent.InverseTransformDirection(normal).normalized);
                Vector2 delta = new Vector2(currentEnd.x - currentStart.x, currentEnd.z - currentStart.z);
                float t = Mathf.Clamp01(Vector2.Dot(xz - new Vector2(currentStart.x, currentStart.z), delta) / Mathf.Max(.0001f, delta.sqrMagnitude));
                float halfWidth = Mathf.Lerp(currentStartWidth, currentEndWidth, t) * .5f;
                float distance = Vector2.Distance(xz, new Vector2(currentStart.x, currentStart.z) + delta * t);
                float coverage = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfWidth * .68f, halfWidth, distance));
                colours.Add(new Color(1f, 1f, 1f, coverage));
            }
            for (int i = 1; i + 1 < polygon.Count; i++)
            { triangles.Add(polygonIndices[0]); triangles.Add(polygonIndices[i]); triangles.Add(polygonIndices[i + 1]); }
        }
        for (int segment = 0; segment + 1 < points.Count; segment++)
        {
            Vector3 a = points[segment], b = points[segment + 1];
            if (!IsFinitePoint(a) || !IsFinitePoint(b)) return null;
            Vector3 direction = new Vector3(b.x - a.x, 0f, b.z - a.z);
            if (direction.sqrMagnitude < 0.0001f) continue;
            Vector3 side = Vector3.Cross(Vector3.up, direction.normalized);
            float aw = widths != null ? widths[segment] : width, bw = widths != null ? widths[segment + 1] : width;
            currentStart = a; currentEnd = b; currentStartWidth = aw; currentEndWidth = bw;
            int rows = Mathf.Max(1, Mathf.CeilToInt(direction.magnitude));
            int columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(aw, bw)), 2, 64);
            // note: A fallback remains within its accepted footprint, with continuous world-space width and slight wear rather than an identical ruler-like strip.
            Vector3 Point(float along, float across)
            {
                Vector3 center = Vector3.Lerp(a, b, along);
                Vector2 xz = new Vector2(center.x, center.z);
                float localWidth = Mathf.Lerp(aw, bw, along);
                // note: Cosmetic wear must not erase a required cell's last accepted road sliver.
                if (preserveAcceptedFootprint) return center + side * (localWidth * (across - .5f));
                float offset = (Mathf.PerlinNoise(center.x * .043f + 17f, center.z * .043f + 31f) - .5f) * localWidth * .12f;
                return center + side * (offset + localWidth * YQGeneratedWorldEnvironment.LivedPathWidthScale(xz) * (across - .5f));
            }
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                Vector3 al = Point(row / (float)rows, column / (float)columns);
                Vector3 ar = Point(row / (float)rows, (column + 1f) / columns);
                Vector3 bl = Point((row + 1f) / rows, column / (float)columns);
                Vector3 br = Point((row + 1f) / rows, (column + 1f) / columns);
                Emit(al, bl, ar); Emit(ar, bl, br);
            }
        }
        // note: Retry only an empty worn ribbon with its original accepted width; keep the same route, terrain clipping and grounding gates.
        if (triangles.Count == 0)
            return preserveAcceptedFootprint ? null : BuildGroundedRoadRibbon(parent, name, points, width,
                material, terrain, widths, worldHeight, true);
        var mesh = new Mesh { name = name + "_Mesh", indexFormat = vertices.Count > 65535 ?
            UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colours);
        if (worldHeight == null) mesh.SetNormals(normals);
        else mesh.RecalculateNormals();
        // note: Authored ground materials contain normal maps, which require valid tangents on the generated ribbon.
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.AddComponent<YQContinuousWorldOwnedResources>().Register(mesh);
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        root.AddComponent<MeshCollider>().sharedMesh = mesh;
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
        // note: Missing path meshes may borrow opaque ground surfaces from the same accepted palette, never foliage cards or mixed rock/foliage atlases. Selection retains the catalog's stable weighted rule.
        if (reference == null && slot == YQWorldAssetCatalog.SlotPath && palette != null && registry != null)
        {
            var surfaces = new GeneratedRegionAssetPaletteRecord();
            foreach (var candidate in palette.terrainMaterials)
            {
                if (candidate == null) continue;
                string identity = ((candidate.assetPath ?? string.Empty) + " " + (candidate.assetKey ?? string.Empty)).ToLowerInvariant();
                if (identity.Contains("foliage") || identity.Contains("atlas") || identity.Contains("leaves") || identity.Contains("grass")) continue;
                if (!identity.Contains("ground") && !identity.Contains("mud") && !identity.Contains("dirt") && !identity.Contains("soil") && !identity.Contains("stone") && !identity.Contains("path")) continue;
                Material candidateMaterial = registry.ResolveMaterial(candidate.assetPath);
                if (candidateMaterial == null || candidateMaterial.mainTexture == null || candidateMaterial.renderQueue >= 2450 ||
                    (candidateMaterial.HasProperty("_AlphaClip") && candidateMaterial.GetFloat("_AlphaClip") > .5f)) continue;
                surfaces.terrainMaterials.Add(candidate);
            }
            reference = YQWorldAssetCatalog.PickAssetForSlot(surfaces, YQWorldAssetCatalog.SlotTerrain, seed + "|surface");
        }
        // note: Material references resolve through the canonical lazy registry, including preferred URP paths, before prefab-entry lookup.
        Material source = reference != null ? registry.ResolveMaterial(reference.assetPath) : null;
        Renderer sourceRenderer = null;
        if (source == null && reference != null)
        {
            GameObject sourcePrefab = registry.ResolvePrefab(reference.assetPath);
            sourceRenderer = sourcePrefab != null ? sourcePrefab.GetComponentInChildren<Renderer>(true) : null;
            source = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
        }
        if (source != null)
        {
            source = YQRuntimeUrpMaterialRepair.ResolveGeneratedSurfaceMaterial(source, sourceRenderer);
            if (source != null) return new Material(source) { hideFlags = HideFlags.DontSave };
        }
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

    private static void BindGroundedRoadSurface(Material roadMaterial, Terrain terrain)
    {
        // note: The approved packed-earth textures stay bound while the verge fades into the same underlying terrain.
        Shader vergeShader = Shader.Find("YourQuest/Grounded Path");
        if (vergeShader != null) roadMaterial.shader = vergeShader;
        // note: Reuse the approved packed-earth terrain texture on the ribbon instead of a prefab's foliage/stone atlas.
        foreach (TerrainLayer layer in terrain.terrainData.terrainLayers)
        {
            if (layer == null || !layer.name.EndsWith("_LivedPath", StringComparison.Ordinal) || layer.diffuseTexture == null) continue;
            if (roadMaterial.HasProperty("_BaseMap")) roadMaterial.SetTexture("_BaseMap", layer.diffuseTexture);
            roadMaterial.mainTexture = layer.diffuseTexture;
            roadMaterial.mainTextureScale = Vector2.one;
            if (roadMaterial.HasProperty("_BaseColor")) roadMaterial.SetColor("_BaseColor", new Color(.58f, .48f, .36f, 1f));
            if (roadMaterial.HasProperty("_BumpMap")) roadMaterial.SetTexture("_BumpMap", layer.normalMapTexture);
            break;
        }
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
            // note: Resolve only the matching metadata entry, keeping unrelated pack textures out of residency.
            Material material = entry.material != null ? entry.material : registry.ResolveMaterial(entry.assetPath);
            Renderer sourceRenderer = null;
            GameObject prefab = material == null ? registry.ResolvePrefab(entry.assetPath) : null;
            if (material == null && prefab != null)
            {
                // note: Root entries and lazy shards need their canonical prefab resolver before extracting the authored surface.
                sourceRenderer = prefab.GetComponentInChildren<Renderer>(true);
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
