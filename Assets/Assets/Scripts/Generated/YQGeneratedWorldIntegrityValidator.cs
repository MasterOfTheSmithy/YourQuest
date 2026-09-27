using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class YQGeneratedWorldIntegrityValidator
{
    private const float FrameBudgetSeconds = 0.0015f;
    private const int MaximumAutomaticMeshColliderTriangles = 30000;
    private static readonly Collider[] RouteClearanceBuffer = new Collider[32];

    public sealed class Report
    {
        public int renderers;
        public int walkableSurfaces;
        public int validColliders;
        public int enabledNonTriggerColliders;
        public int repairedColliders;
        public int missingColliders;
        public int disabledColliders;
        public int invalidLayers;
        public int repairedRendererBounds;
        public int repairedLodGroups;

        public bool IsValid => missingColliders == 0;
        public bool HasPlayableCollision =>
            validColliders > 0 || enabledNonTriggerColliders > 0;
    }

    public sealed class RouteReport
    {
        public int sampledPoints;
        public int missingGroundPoints;
        public int impassableHeightSteps;
        // note: Retained for report compatibility; route inspection no longer deletes collision by bounding-box size.
        public int repairedDecorativeBlockers;
        public int unresolvedStructuralBlockers;
        public int clearanceQuerySaturations;

        // note: Phase 0 measurements are independent diagnostics; uncertainty never silently becomes approval.
        public int measuredTraversalSamples;
        public int traversalIssueSamples;
        public bool traversalMeasurementComplete;

        // note: Missing evidence and recorded traversal failures cannot qualify a world as playable.
        public bool IsValid => sampledPoints > 0 &&
            missingGroundPoints == 0 && HasPlayableGrade &&
            unresolvedStructuralBlockers == 0 && clearanceQuerySaturations == 0 &&
            traversalMeasurementComplete && measuredTraversalSamples > 0 &&
            traversalIssueSamples == 0;
        // note: A terrain sample can jump across a curved or terraced segment while the authoritative capsule sweep remains clear; only a measured traversal failure should block world acceptance.
        public bool HasPlayableGrade => impassableHeightSteps == 0 ||
            (traversalMeasurementComplete && traversalIssueSamples == 0);
    }

    public static IEnumerator ValidateAndRepairRoutine(
        GameObject root,
        string worldSeed,
        string passName,
        Action<Report> completed)
    {
        Report report = new Report();
        if (root == null)
        {
            report.missingColliders = 1;
            completed?.Invoke(report);
            yield break;
        }

        yield return RepairLodGroupsRoutine(root, report);

        Camera gameplayCamera = Camera.main;
        int generatedVisibleLayer = Mathf.Clamp(root.layer, 0, 31);
        if (gameplayCamera != null &&
            (gameplayCamera.cullingMask & (1 << generatedVisibleLayer)) == 0)
        {
            generatedVisibleLayer = 0;
            gameplayCamera.cullingMask |= 1 << generatedVisibleLayer;
        }

        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(root.transform);
        float frameStartedAt = Time.realtimeSinceStartup;
        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            if (current == null)
                continue;

            for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                pending.Push(current.GetChild(childIndex));

            Collider localCollider = current.GetComponent<Collider>();
            if (localCollider != null && localCollider.enabled && !localCollider.isTrigger)
            {
                // note: This independent coverage count prevents a decorative-only imported cell with no recognized floor names from passing collision acceptance by vacuous success.
                report.enabledNonTriggerColliders++;
            }

            Renderer[] renderers = current.GetComponents<Renderer>();
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                    continue;
                report.renderers++;
                RepairRendererVisibility(
                    renderer,
                    report,
                    gameplayCamera,
                    generatedVisibleLayer);
                if (!IsWalkableSurface(renderer.gameObject))
                    continue;

                report.walkableSurfaces++;
                ValidateWalkableSurface(renderer, report);
            }

            if (Time.realtimeSinceStartup - frameStartedAt >= FrameBudgetSeconds)
            {
                // note: Integrity validation traverses imported hierarchies cooperatively so collision guarantees cannot reintroduce a loading-frame hang.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        Debug.Log(
            "[YQGeneratedWorldIntegrityValidator] PASS COMPLETE\n" +
            "Seed: " + (worldSeed ?? string.Empty) + "\n" +
            "Pass: " + (passName ?? string.Empty) + "\n" +
            "Renderers: " + report.renderers + "\n" +
            "Walkable surfaces: " + report.walkableSurfaces + "\n" +
            "Valid colliders: " + report.validColliders + "\n" +
            "Enabled non-trigger colliders: " + report.enabledNonTriggerColliders + "\n" +
            "Colliders repaired: " + report.repairedColliders + "\n" +
            "Missing colliders: " + report.missingColliders + "\n" +
            "Disabled colliders found: " + report.disabledColliders + "\n" +
            "Invalid collision layers repaired: " + report.invalidLayers + "\n" +
            "Renderer bounds repaired: " + report.repairedRendererBounds + "\n" +
            "LOD groups repaired: " + report.repairedLodGroups);
        completed?.Invoke(report);
    }

    public static IEnumerator ValidateCriticalGroundRoutine(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Action<int> completed)
    {
        int failures = 0;
        if (terrain == null || terrain.terrainData == null || plan == null)
        {
            completed?.Invoke(1);
            yield break;
        }

        List<Vector3> samples = new List<Vector3>
        {
            YQGeneratedWorldLayout.GetVeyOriginAnchor()
        };
        for (int i = 0; i < plan.settlements.Count; i++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[i];
            if (settlement != null)
            {
                Vector3 anchor = YQGeneratedWorldLayout.GetSettlementAnchor(
                    plan,
                    settlement,
                    terrain);
                if (!YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) ||
                    IsInsideTerrainBounds(terrain, anchor, 0f))
                {
                    samples.Add(anchor);
                }
                else
                {
                    // note: Accepted V2 settlements outside the finite origin terrain are validated when their streamed owner is admitted, not during startup ground checks.
                    Debug.Log(
                        "[YQGeneratedWorldIntegrityValidator] Deferred distant V2 settlement ground sample: " +
                        settlement.settlementId);
                }
            }
        }

        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 sample = samples[i];
            Vector3 rayOrigin = new Vector3(sample.x, terrain.transform.position.y + terrain.terrainData.size.y + 50f, sample.z);
            if (!Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out _,
                    terrain.terrainData.size.y + 120f,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Ignore))
            {
                failures++;
                Debug.LogError(
                    "[WORLDGEN ERROR] Critical playable anchor has no downward collision. " +
                    "Seed=" + plan.worldSeed + ", sample=" + i + ", position=" + sample + ".");
            }

            if ((i & 3) == 3)
                yield return null;
        }

        completed?.Invoke(failures);
    }

    public static IEnumerator ValidateRouteTraversalRoutine(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Action<RouteReport> completed)
    {
        RouteReport report = new RouteReport();
        if (terrain == null || terrain.terrainData == null || plan == null)
        {
            report.missingGroundPoints = 1;
            completed?.Invoke(report);
            yield break;
        }

        List<RouteSampleSegment> segments;
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
        {
            if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                    plan,
                    out YQPreparedSpatialMaterializationV2 prepared,
                    out string preparationFailure))
            {
                // note: Route acceptance never substitutes the unrelated V1 graph after a V2 world has been selected.
                report.missingGroundPoints = 1;
                Debug.LogError(
                    "[YQGeneratedWorldIntegrityValidator] V2 route validation input rejected: " +
                    preparationFailure);
                completed?.Invoke(report);
                yield break;
            }

            segments = BuildPhysicalRouteSegmentsV2(prepared);
        }
        else
        {
            GeneratedSpatialWorldPlanRecord spatial =
                YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan);
            if (spatial == null)
            {
                report.missingGroundPoints = 1;
                completed?.Invoke(report);
                yield break;
            }

            segments = BuildPhysicalRouteSegments(terrain, plan, spatial);
        }
        ReplaceProceduralSettlementRouteSegments(terrain, plan, segments);
        ClipRouteSegmentsToTerrain(terrain, segments);
        // note: Both measurement passes preserve the original physical route; neither may disable collision by size.
        yield return MeasurePlayerTraversalRoutine(terrain, segments, report);
        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        float rayHeight = terrainOrigin.y + terrainSize.y + 60f;
        float maximumRayDistance = terrainSize.y + 160f;
        HashSet<int> unresolvedBlockers = new HashSet<int>();
        float frameStartedAt = Time.realtimeSinceStartup;
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            RouteSampleSegment segment = segments[segmentIndex];
            int steps = Mathf.Max(
                1,
                Mathf.CeilToInt(Vector2.Distance(segment.start, segment.end) / 6f));
            float previousHeight = float.NaN;
            Collider previousSupportCollider = null;
            Vector2 previousPoint = default;
            for (int step = 0; step <= steps; step++)
            {
                float pathT = step / (float)steps;
                Vector2 point = segment.ResolveCenter(pathT);
                report.sampledPoints++;
                bool insideTerrain =
                    point.x >= terrainOrigin.x && point.x <= terrainOrigin.x + terrainSize.x &&
                    point.y >= terrainOrigin.z && point.y <= terrainOrigin.z + terrainSize.z;
                RaycastHit support = default;
                bool hasGround = insideTerrain && Physics.Raycast(
                    new Vector3(point.x, rayHeight, point.y),
                    Vector3.down,
                    out support,
                    maximumRayDistance,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Ignore);
                if (!hasGround)
                {
                    report.missingGroundPoints++;
                    Debug.LogError(
                        "[WORLDGEN ERROR] Travel route has no walkable ground. " +
                        "Seed=" + plan.worldSeed + ", route=" + segment.routeId + ", position=" + point + ".");
                }
                else
                {
                    // note: A bridge's collider owns walking elevation; the riverbed underneath is intentionally lower.
                    float height = support.point.y;
                    // note: Generated roads are painted terrain; ignore unrelated prop/building colliders that happen to sit under the vertical probe when measuring route grade.
                    if (!(support.collider is TerrainCollider) && !IsCertifiedBridgeCollider(support.collider))
                        height = terrain.SampleHeight(new Vector3(point.x, 0f, point.y)) + terrain.transform.position.y;
                    // note: Bridge deck transitions intentionally change support elevation over a short span; only reject large rises on ordinary terrain.
                    bool bridgeTransition = IsCertifiedBridgeCollider(support.collider) ||
                        IsCertifiedBridgeCollider(previousSupportCollider);
                    if (!float.IsNaN(previousHeight) && !bridgeTransition && Mathf.Abs(height - previousHeight) > 4.5f)
                    {
                        report.impassableHeightSteps++;
                        Debug.LogWarning(
                            "[WORLDGEN WARNING] Travel route contains an excessive terrain rise. " +
                            "Seed=" + plan.worldSeed + ", route=" + segment.routeId +
                            ", rise=" + Mathf.Abs(height - previousHeight).ToString("0.00") + "m, " +
                            "point=" + point + ", previousPoint=" + previousPoint + ", " +
                            "support=" + DescribeTraversalCollider(support.collider) + ", previous=" +
                            DescribeTraversalCollider(previousSupportCollider) + ".");
                    }
                    previousHeight = height;
                    previousSupportCollider = support.collider;
                    previousPoint = point;
                    InspectRouteClearance(
                        PhysicsSceneExtensions.GetPhysicsScene(terrain.gameObject.scene),
                        point,
                        height,
                        segment.routeId,
                        plan.worldSeed,
                        unresolvedBlockers,
                        report);
                }

                if (Time.realtimeSinceStartup - frameStartedAt >= FrameBudgetSeconds)
                {
                    // note: Route acceptance samples only a small time slice per frame so safety validation cannot become the loading hitch it is intended to prevent.
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
            }
        }

        Debug.Log(
            "[YQGeneratedWorldIntegrityValidator] ROUTE PASS COMPLETE\n" +
            "Seed: " + plan.worldSeed + "\n" +
            "Samples: " + report.sampledPoints + "\n" +
            "Missing ground: " + report.missingGroundPoints + "\n" +
            "Impassable height steps: " + report.impassableHeightSteps + "\n" +
            "Decorative blockers repaired: " + report.repairedDecorativeBlockers + "\n" +
            "Unresolved blockers reported: " + report.unresolvedStructuralBlockers + "\n" +
            "Incomplete clearance queries: " + report.clearanceQuerySaturations);
        completed?.Invoke(report);
    }

    private static void ClipRouteSegmentsToTerrain(
        Terrain terrain,
        List<RouteSampleSegment> segments)
    {
        if (terrain == null || terrain.terrainData == null || segments == null)
            return;

        Vector3 position = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        const float startupRouteBoundaryMargin = 8f;
        for (int index = segments.Count - 1; index >= 0; index--)
        {
            RouteSampleSegment segment = segments[index];
            if (!TryClipRouteToBounds(
                    segment.start,
                    segment.end,
                    position.x + startupRouteBoundaryMargin,
                    position.x + size.x - startupRouteBoundaryMargin,
                    position.z + startupRouteBoundaryMargin,
                    position.z + size.z - startupRouteBoundaryMargin,
                    out Vector2 start,
                    out Vector2 end))
            {
                // note: Distant V2 route spans are measured by streamed-cell validation and are not startup failures for the finite origin TerrainData.
                segments.RemoveAt(index);
                continue;
            }

            bool clipped = start != segment.start || end != segment.end;
            segments[index] = new RouteSampleSegment(
                segment.routeId,
                start,
                end,
                clipped ? 0f : segment.curveAmplitude,
                segment.curvePhase);
        }
    }

    private static bool TryClipRouteToBounds(
        Vector2 start,
        Vector2 end,
        float minimumX,
        float maximumX,
        float minimumZ,
        float maximumZ,
        out Vector2 clippedStart,
        out Vector2 clippedEnd)
    {
        clippedStart = start;
        clippedEnd = end;
        Vector2 delta = end - start;
        float minimumT = 0f;
        float maximumT = 1f;
        if (!ClipRouteAxis(-delta.x, start.x - minimumX, ref minimumT, ref maximumT) ||
            !ClipRouteAxis(delta.x, maximumX - start.x, ref minimumT, ref maximumT) ||
            !ClipRouteAxis(-delta.y, start.y - minimumZ, ref minimumT, ref maximumT) ||
            !ClipRouteAxis(delta.y, maximumZ - start.y, ref minimumT, ref maximumT))
            return false;

        clippedStart = start + delta * minimumT;
        clippedEnd = start + delta * maximumT;
        return true;
    }

    private static bool ClipRouteAxis(
        float coefficient,
        float constant,
        ref float minimumT,
        ref float maximumT)
    {
        if (Mathf.Abs(coefficient) < .00001f)
            return constant >= 0f;

        float parameter = constant / coefficient;
        if (coefficient < 0f)
        {
            if (parameter > maximumT)
                return false;
            minimumT = Mathf.Max(minimumT, parameter);
        }
        else
        {
            if (parameter < minimumT)
                return false;
            maximumT = Mathf.Min(maximumT, parameter);
        }

        return minimumT <= maximumT;
    }

    private static bool IsInsideTerrainBounds(
        Terrain terrain,
        Vector3 point,
        float margin)
    {
        if (terrain == null || terrain.terrainData == null)
            return false;

        Vector3 position = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        return point.x >= position.x + margin &&
            point.x <= position.x + size.x - margin &&
            point.z >= position.z + margin &&
            point.z <= position.z + size.z - margin;
    }

    private static void ReplaceProceduralSettlementRouteSegments(Terrain terrain, GeneratedWorldPlanRecord plan, List<RouteSampleSegment> segments)
    {
        // note: Verify the streets actually instantiated/painted for the saved block graph, not the obsolete donor-town spine.
        if (plan.settlements == null) return;
        bool v2 = YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan);
        YQPreparedSpatialMaterializationV2 prepared = null;
        if (v2 && !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out prepared, out _)) return;
        foreach (var settlement in plan.settlements)
        {
            var layout = settlement?.proceduralLayout;
            if (layout?.streets == null) continue;
            float heading, gate;
            string legacyPrefix;
            if (v2 && prepared.TryGetSiteBySemanticId(settlement.settlementId, out var site))
            { heading = site.headingDegrees; gate = site.reservedRadius * .82f + 6f; legacyPrefix = site.siteId; }
            else if (!v2 && YQGeneratedWorldSpatialPlanner.TryGetLocation(plan, settlement.settlementId, out var location))
            { heading = location.entranceHeadingDegrees; gate = Mathf.Clamp(location.footprintRadius, 24f, 72f) + 14f; legacyPrefix = settlement.settlementId; }
            else continue;
            for (int i = segments.Count - 1; i >= 0; i--)
                if (segments[i].routeId == legacyPrefix + "|entrance_spine" || segments[i].routeId == legacyPrefix + "|cross_street")
                    segments.RemoveAt(i);
            Vector3 anchor = YQGeneratedWorldLayout.GetSettlementAnchor(plan, settlement, terrain);
            Quaternion rotation = Quaternion.Euler(0, heading, 0);
            for (int i = 0; i < layout.streets.Count; i++)
            {
                var street = layout.streets[i];
                Vector3 from = anchor + rotation * street.start, to = anchor + rotation * street.end;
                segments.Add(new RouteSampleSegment(settlement.settlementId + "|cell_street|" + i,
                    new Vector2(from.x, from.z), new Vector2(to.x, to.z), 0, 0));
            }
            Vector3 entrance = anchor + rotation * new Vector3(0, 0, gate);
            segments.Add(new RouteSampleSegment(settlement.settlementId + "|cell_gate",
                new Vector2(anchor.x, anchor.z), new Vector2(entrance.x, entrance.z), 0, 0));
        }
    }

    private static IEnumerator MeasurePlayerTraversalRoutine(Terrain terrain, List<RouteSampleSegment> segments, RouteReport report)
    {
        YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;
        if (motor == null)
        {
            // note: Route acceptance needs the real authoritative capsule even when a verifier starts ahead of the title bootstrap.
            YourQuestTutorialAutoBootstrap.EnsureRuntimePlayerForWorldGeneration();
            motor = YQInvestorPlayerMotor.ActiveMotor;
        }
        // note: During the initial generation lock the scene player can exist before its static authority registration; resolve exactly one tagged Player motor without inventing a second traversal shape.
        if (motor == null && YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked)
        {
            YQInvestorPlayerMotor[] candidates =
                UnityEngine.Object.FindObjectsByType<YQInvestorPlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            YQInvestorPlayerMotor taggedCandidate = null;
            bool ambiguous = false;
            for (int index = 0; index < candidates.Length; index++)
            {
                YQInvestorPlayerMotor candidate = candidates[index];
                if (candidate == null ||
                    (!candidate.gameObject.CompareTag("Player") &&
                     !string.Equals(candidate.gameObject.name, "Player", StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (taggedCandidate != null)
                {
                    ambiguous = true;
                    break;
                }
                taggedCandidate = candidate;
            }
            if (!ambiguous)
                motor = taggedCandidate;
        }
        CharacterController controller = motor != null
            ? motor.GetComponent<CharacterController>() ?? motor.GetComponentInChildren<CharacterController>(true)
            : null;
        if (!YQRouteTraversalProbe.Shape.TryCapture(controller, out var shape))
        {
            Debug.LogWarning("[YQRouteTraversalProbe] Measurement unavailable: no supported authoritative player capsule. World acceptance blocked.");
            yield break;
        }
        var probe = new YQRouteTraversalProbe(
            PhysicsSceneExtensions.GetPhysicsScene(terrain.gameObject.scene), shape, motor.transform);
        float started = Time.realtimeSinceStartup;
        int loggedIssues = 0;
        // note: Bounded dense support sampling supplements continuous collision casts, without unbounded loading work.
        float spacing = Mathf.Clamp(shape.radius, .15f, .5f);
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            RouteSampleSegment segment = segments[segmentIndex];
            float lengthBound = Vector2.Distance(segment.start, segment.end) + Mathf.Abs(segment.curveAmplitude) * Mathf.PI * 3f;
            // note: Complete every finite route over bounded frames; the old global diagnostic cap left normal worlds permanently unqualified.
            if (float.IsNaN(lengthBound) || float.IsInfinity(lengthBound) || lengthBound < 0f || lengthBound / spacing > 100000f)
            {
                Debug.LogWarning("[YQRouteTraversalProbe] Invalid or excessive segment extent; coverage incomplete for " + segment.routeId);
                yield break;
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(lengthBound / spacing));
            bool hasPrevious = false;
            Vector3 previous = default;
            for (int step = 0; step <= steps; step++)
            {
                Vector2 point = segment.ResolveCenter(step / (float)steps);
                Vector3 expected = new Vector3(point.x, 0f, point.y);
                // note: The intended public route follows Terrain except at a certified bridge; a sky ray into a statue or doorway roof is not a walking-height authority.
                float supportHeight = terrain.SampleHeight(expected) + terrain.transform.position.y;
                float supportRayHeight = terrain.transform.position.y + terrain.terrainData.size.y + 60f;
                if (Physics.Raycast(new Vector3(point.x, supportRayHeight, point.y), Vector3.down,
                    out RaycastHit supportHit, terrain.terrainData.size.y + 160f,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore) &&
                    IsCertifiedBridgeCollider(supportHit.collider))
                    supportHeight = supportHit.point.y;
                expected.y = supportHeight;
                var measurement = probe.Measure(expected, hasPrevious, previous);
                report.measuredTraversalSamples++;
                YQRouteTraversalProbe.Issue acceptedIssues = measurement.issues;
                // note: Bridge walk surfaces intentionally overlap the solid bank at their approach seams; treat that certified support as one traversable surface instead of an obstacle.
                if (IsCertifiedBridgeTransition(measurement))
                {
                    acceptedIssues &= ~(YQRouteTraversalProbe.Issue.Occupied |
                        YQRouteTraversalProbe.Issue.SweptObstacle |
                        YQRouteTraversalProbe.Issue.AmbiguousSupportOverlap |
                        YQRouteTraversalProbe.Issue.UnsupportedFootprint |
                        // note: Bridge deck/ramp meshes can report a steep triangle normal even when the authored walk surface grade is valid.
                        YQRouteTraversalProbe.Issue.SteepSupport);
                }
                if (IsExpectedTerrainContact(measurement, terrain))
                {
                    // note: The terrain collider is the route's intended walking support; capsule contact with that same collider is not a separate obstacle.
                    acceptedIssues &= ~(YQRouteTraversalProbe.Issue.Occupied |
                        YQRouteTraversalProbe.Issue.SweptObstacle |
                        YQRouteTraversalProbe.Issue.AmbiguousSupportOverlap |
                        // note: Terrain height sampling can leave an inset footprint ray just outside the collider's finite step tolerance while the center support remains valid.
                        YQRouteTraversalProbe.Issue.UnsupportedFootprint |
                        // note: The accepted road profile owns the terrain grade; do not reject that same collider for a repaired shoulder triangle normal.
                        YQRouteTraversalProbe.Issue.SteepSupport);
                }
                // note: A regional route that terminates at a settlement may overlap the authored arrival envelope; keep those walls solid while allowing the gate to certify.
                if (IsExpectedSettlementArrival(segment.routeId, measurement.supportCollider) ||
                    IsExpectedSettlementArrival(segment.routeId, measurement.overlapCollider) ||
                    IsExpectedSettlementArrival(segment.routeId, measurement.sweepCollider))
                {
                    acceptedIssues &= ~(YQRouteTraversalProbe.Issue.Occupied |
                        YQRouteTraversalProbe.Issue.SweptObstacle |
                        YQRouteTraversalProbe.Issue.AmbiguousSupportOverlap |
                        YQRouteTraversalProbe.Issue.UnsupportedFootprint);
                }
                // note: Cell streets can terminate on a reviewed stair landing whose narrow mesh intentionally supports the centerline only; preserve all wall/obstacle evidence while accepting that authored entrance tread.
                if (IsExpectedSettlementEntranceSupport(segment.routeId, measurement.supportCollider) ||
                    IsExpectedSettlementEntranceSupport(segment.routeId, measurement.overlapCollider) ||
                    IsExpectedSettlementEntranceSupport(segment.routeId, measurement.sweepCollider))
                {
                    acceptedIssues &= ~YQRouteTraversalProbe.Issue.UnsupportedFootprint;
                }
                // note: Generated roadside dressing can touch the probe capsule at an approach seam; it is already classified as non-structural below, so apply the same clearance tolerance during the semantic traversal pass.
                if (IsGeneratedRouteDecoration(measurement.supportCollider) ||
                    IsGeneratedRouteDecoration(measurement.overlapCollider) ||
                    IsGeneratedRouteDecoration(measurement.sweepCollider))
                {
                    acceptedIssues &= ~(YQRouteTraversalProbe.Issue.Occupied |
                        YQRouteTraversalProbe.Issue.SweptObstacle |
                        YQRouteTraversalProbe.Issue.AmbiguousSupportOverlap |
                        YQRouteTraversalProbe.Issue.UnsupportedFootprint);
                }
                // note: Residents, the authoritative player, and other dynamic bodies may momentarily occupy a route sample without changing the route's structural walkability.
                if (IsDynamicTraversalCollider(measurement.supportCollider) ||
                    IsDynamicTraversalCollider(measurement.overlapCollider) ||
                    IsDynamicTraversalCollider(measurement.sweepCollider))
                {
                    acceptedIssues &= ~(YQRouteTraversalProbe.Issue.Occupied |
                        YQRouteTraversalProbe.Issue.SweptObstacle |
                        YQRouteTraversalProbe.Issue.AmbiguousSupportOverlap |
                        YQRouteTraversalProbe.Issue.UnsupportedFootprint);
                }
                if (acceptedIssues != YQRouteTraversalProbe.Issue.None)
                {
                    report.traversalIssueSamples++;
                    if (loggedIssues++ < 8)
                        Debug.LogWarning("[YQRouteTraversalProbe] Rejected route=" + segment.routeId +
                            " feet=" + measurement.feet + " issues=" + acceptedIssues +
                            " support=" + DescribeTraversalCollider(measurement.supportCollider) +
                            " overlap=" + DescribeTraversalCollider(measurement.overlapCollider) +
                            " nearestReturnedSweep=" + DescribeTraversalCollider(measurement.sweepCollider) +
                            " sweepPoint=" + measurement.sweepContactPoint + ". World acceptance blocked.");
                }
                previous = measurement.feet;
                hasPrevious = measurement.hasSupport;
                if (Time.realtimeSinceStartup - started >= FrameBudgetSeconds)
                {
                    yield return null;
                    started = Time.realtimeSinceStartup;
                }
            }
        }
        report.traversalMeasurementComplete = segments.Count > 0;
        Debug.Log("[YQRouteTraversalProbe] Traversal samples=" + report.measuredTraversalSamples +
            " issueSamples=" + report.traversalIssueSamples + " complete=" + report.traversalMeasurementComplete +
            " capsule=" + shape.radius + "m radius / " + shape.height + "m height. Not semantic route certification.");
    }

    private static bool IsCertifiedBridgeTransition(YQRouteTraversalProbe.Result measurement)
    {
        // note: Only the generated bridge's owned walk collider can authorize this seam normalization; arbitrary mesh names never qualify.
        return IsCertifiedBridgeCollider(measurement.supportCollider) ||
            IsCertifiedBridgeCollider(measurement.overlapCollider) ||
            IsCertifiedBridgeCollider(measurement.sweepCollider) ||
            IsCertifiedBridgeDressingCollider(measurement.supportCollider) ||
            IsCertifiedBridgeDressingCollider(measurement.overlapCollider) ||
            IsCertifiedBridgeDressingCollider(measurement.sweepCollider);
    }

    private static bool IsDynamicTraversalCollider(Collider collider)
    {
        // note: Dynamic entities are already excluded from hard clearance blockers, so use the same ownership boundary for capsule route measurements.
        return collider != null && (collider.GetComponentInParent<EntityInfo>() != null ||
            collider.GetComponentInParent<YQInvestorPlayerMotor>() != null ||
            collider.attachedRigidbody != null);
    }

    private static bool IsCertifiedBridgeCollider(Collider collider)
    {
        if (collider == null ||
            collider.GetComponentInParent<YQGeneratedRiverBridge>() == null)
            return false;

        // note: Visual bridge deck variants keep the same generated bridge owner and are all valid walk surfaces during deferred collider cleanup.
        string name = collider.gameObject.name;
        return name == "StoneBridgeWalkSurface" ||
            name == "StoneBridgeDeck" ||
            name == "WoodBridgeDeck" ||
            name == "DisheveledBridgeDeck" ||
            name == "RepairedBridgeDeck";
    }

    private static bool IsCertifiedBridgeDressingCollider(Collider collider)
    {
        if (collider == null ||
            collider.GetComponentInParent<YQGeneratedRiverBridge>() == null)
            return false;

        // note: Bridge rail posts are generated visual dressing, never a walk surface; their ownership only normalizes a stale/deferred collider hit while terrain or the deck remains the support authority.
        return collider.gameObject.name.IndexOf("BridgeRailPost", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsExpectedTerrainContact(
        YQRouteTraversalProbe.Result measurement,
        Terrain terrain)
    {
        if (terrain == null)
            return false;

        TerrainCollider terrainCollider =
            terrain.GetComponent<TerrainCollider>();
        // note: Terrain support only normalizes contact with that same terrain; a separate statue, wall, or landing collision must remain a traversal failure.
        return terrainCollider != null &&
            measurement.supportCollider == terrainCollider &&
            (measurement.overlapCollider == null || measurement.overlapCollider == terrainCollider) &&
            (measurement.sweepCollider == null || measurement.sweepCollider == terrainCollider);
    }

    private static string DescribeTraversalCollider(Collider collider)
    {
        // note: Format only the capped diagnostic messages; instance IDs disambiguate duplicate names within this session, not across saves.
        return collider == null ? "<none>" : HierarchyPath(collider.transform) +
            " [" + collider.GetType().Name + ", layer=" + collider.gameObject.layer +
            ", sessionId=" + collider.GetInstanceID() + "]";
    }

    private static List<RouteSampleSegment> BuildPhysicalRouteSegmentsV2(
        YQPreparedSpatialMaterializationV2 prepared)
    {
        List<RouteSampleSegment> segments = new List<RouteSampleSegment>();
        // note: Reuse the route-point scratch list across routes while keeping the validation projection aligned with terrain painting.
        List<Vector2> routePoints = new List<Vector2>(4);
        for (int routeIndex = 0;
             routeIndex < prepared.RouteCount;
             routeIndex++)
        {
            YQSpatialMaterializationRouteV2 route =
                prepared.GetRoute(routeIndex);
            int pointCount = prepared.GetRoutePointCount(routeIndex);
            YQSpatialMaterializationSiteV2 destination = default;
            bool destinationIsSettlement = TryResolveV2Site(prepared, route.toSiteId, out destination) &&
                destination.kind == YQSiteKindV2.Settlement;
            bool settlementSkirtAuthored = false;
            if (pointCount < 2)
                continue;
            routePoints.Clear();
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                YQSpatialMaterializationRoutePointV2 point =
                    prepared.GetRoutePoint(routeIndex, pointIndex);
                Vector2 position = new Vector2(point.x, point.z);
                if (pointIndex == 0)
                {
                    position = ResolveV2RouteGate(
                        prepared,
                        route.fromSiteId,
                        position);
                }
                else if (pointIndex == pointCount - 1)
                {
                    position = ResolveV2RouteGate(
                        prepared,
                        route.toSiteId,
                        position);
                }
                routePoints.Add(position);
            }
            YQGeneratedWorldLayout.FilterOriginLandmarkRouteControlPoints(
                routePoints,
                Mathf.Max(1f, route.width * 0.5f));
            for (int pointIndex = 1;
                 pointIndex < routePoints.Count;
                 pointIndex++)
            {
                if (settlementSkirtAuthored)
                    continue;
                Vector2 start = routePoints[pointIndex - 1];
                Vector2 end = routePoints[pointIndex];

                // note: Settlement-bound regional roads skirt the reserved footprint before turning into the authored entrance gate.
                // note: This keeps the final leg from cutting diagonally through a house while preserving the persisted route identity.
                if (destinationIsSettlement)
                {
                    float headingRadians = destination.headingDegrees * Mathf.Deg2Rad;
                    Vector2 entrance = new Vector2(Mathf.Sin(headingRadians), Mathf.Cos(headingRadians));
                    Vector2 lateral = new Vector2(-entrance.y, entrance.x);
                    Vector2 settlementCenter = new Vector2(destination.x, destination.z);
                    float clearanceRadius = destination.reservedRadius + 30f;
                    Vector2 closest = start + Vector2.ClampMagnitude(end - start, Vector2.Distance(start, end));
                    Vector2 leg = end - start;
                    if (leg.sqrMagnitude > 0.001f)
                        closest = start + leg * Mathf.Clamp01(Vector2.Dot(settlementCenter - start, leg) / leg.sqrMagnitude);
                    bool entersSettlement = (closest - settlementCenter).sqrMagnitude <= clearanceRadius * clearanceRadius;
                    if (entersSettlement)
                    {
                        Vector2 radial = start - settlementCenter;
                        if (radial.sqrMagnitude < 0.25f) radial = -entrance;
                        radial.Normalize();
                        float side = Mathf.Sign(Vector2.Dot(radial, lateral));
                        if (Mathf.Abs(side) < 0.5f) side = 1f;
                        float skirtRadius = clearanceRadius + 10f;
                        Vector2 outerA = settlementCenter + radial * skirtRadius;
                        Vector2 outerB = settlementCenter + (radial + lateral * side).normalized * skirtRadius;
                        Vector2 skirt = settlementCenter + lateral * side * skirtRadius;
                        Vector2 gate = ResolveV2RouteGate(prepared, route.toSiteId, end);
                        AddV2RouteSegment(segments, route.routeId, start, outerA, route.width * 0.5f);
                        AddV2RouteSegment(segments, route.routeId, outerA, outerB, route.width * 0.5f);
                        AddV2RouteSegment(segments, route.routeId, outerB, skirt, route.width * 0.5f);
                        AddV2RouteSegment(segments, route.routeId, skirt, gate, route.width * 0.5f);
                        settlementSkirtAuthored = true;
                    }
                }
                if (!settlementSkirtAuthored)
                {
                    // note: Straight legs remain unchanged when they stay outside the destination settlement reserve.
                    AddV2RouteSegment(segments, route.routeId, start, end, route.width * 0.5f);
                }
            }
        }

        for (int siteIndex = 0;
             siteIndex < prepared.SiteCount;
             siteIndex++)
        {
            YQSpatialMaterializationSiteV2 site = prepared.GetSite(siteIndex);
            if (site.kind == YQSiteKindV2.PointOfInterest)
            {
                // note: Validate the same public POI approach painted by the environment; concealed landmarks intentionally have no primary-route segment.
                if (site.concealed || string.IsNullOrWhiteSpace(site.frontageRouteId))
                    continue;
                Vector2 frontage = new Vector2(site.frontageX, site.frontageZ);
                Vector2 poiCenter = new Vector2(site.x, site.z);
                Vector2 inward = poiCenter - frontage;
                if (inward.sqrMagnitude < 0.25f)
                    continue;
                inward.Normalize();
                AddV2RouteSegment(
                    segments,
                    site.siteId + "|poi_approach",
                    frontage,
                    poiCenter - inward * Mathf.Min(5f, site.reservedRadius * 0.2f),
                    1.7f);
                continue;
            }
            if (site.kind != YQSiteKindV2.Settlement ||
                string.IsNullOrWhiteSpace(site.frontageRouteId))
            {
                continue;
            }

            float headingRadians = site.headingDegrees * Mathf.Deg2Rad;
            Vector2 entrance = new Vector2(
                Mathf.Sin(headingRadians),
                Mathf.Cos(headingRadians));
            Vector2 center = new Vector2(site.x, site.z);
            AddV2RouteSegment(
                segments,
                site.siteId + "|entrance_spine",
                // note: Validate from the outer pad shoulder so the final route sample does not jump from graded countryside onto a fixed settlement core.
                center + entrance * (site.reservedRadius + 18f),
                center - entrance *
                    Mathf.Min(20f, site.reservedRadius * 0.34f),
                3.4f);
        }

        return segments;
    }

    private static void AddV2RouteSegment(
        List<RouteSampleSegment> segments,
        string routeId,
        Vector2 start,
        Vector2 end,
        float corridorHalfWidth)
    {
        if (segments == null)
            return;
        if (YQGeneratedWorldLayout.TryBuildOriginLandmarkDetour(
                start, end, corridorHalfWidth, out Vector2[] waypoints))
        {
            // note: Route validation mirrors the painted detour around the authored shrine so acceptance samples the same physical corridor.
            Vector2 previous = start;
            for (int index = 0; index < waypoints.Length; index++)
            {
                segments.Add(new RouteSampleSegment(routeId, previous, waypoints[index], 0f, 0f));
                previous = waypoints[index];
            }
            start = previous;
        }
        segments.Add(new RouteSampleSegment(routeId, start, end, 0f, 0f));
    }

    private static Vector2 ResolveV2RouteGate(
        YQPreparedSpatialMaterializationV2 prepared,
        string siteId,
        Vector2 fallback)
    {
        if (!TryResolveV2Site(prepared, siteId, out YQSpatialMaterializationSiteV2 site) ||
            site.kind == YQSiteKindV2.NaturalFeature)
        {
            return fallback;
        }

        float headingRadians = site.headingDegrees * Mathf.Deg2Rad;
        Vector2 entrance = new Vector2(
            Mathf.Sin(headingRadians),
            Mathf.Cos(headingRadians));
        // note: The acceptance endpoint is the same reserved gate used by V2 road painting, not the obsolete V1 settlement entrance.
        return new Vector2(site.x, site.z) +
               // note: Keep the accepted gate on the outside edge of the construction reserve for a continuous approach grade.
               // note: The validation endpoint matches the runtime-painted entrance spine and its widened exterior approach.
               entrance * (site.reservedRadius + 18f);
    }

    private static bool TryResolveV2Site(
        YQPreparedSpatialMaterializationV2 prepared,
        string siteId,
        out YQSpatialMaterializationSiteV2 site)
    {
        // note: Persisted routes may carry a qualified site key while the materialization catalog indexes the semantic id.
        if (prepared.TryGetSiteBySiteId(siteId, out site) || prepared.TryGetSiteBySemanticId(siteId, out site))
            return true;
        string semanticId = siteId;
        int separator = semanticId == null ? -1 : semanticId.LastIndexOf(':');
        if (separator >= 0 && separator + 1 < semanticId.Length)
            semanticId = semanticId.Substring(separator + 1);
        return prepared.TryGetSiteBySemanticId(semanticId, out site);
    }

    private static List<RouteSampleSegment> BuildPhysicalRouteSegments(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialWorldPlanRecord spatial)
    {
        List<RouteSampleSegment> segments = new List<RouteSampleSegment>();
        for (int routeIndex = 0; routeIndex < spatial.routes.Count; routeIndex++)
        {
            GeneratedSpatialRouteRecord route = spatial.routes[routeIndex];
            if (route == null || route.waypoints == null || route.waypoints.Count < 2)
                continue;
            for (int waypointIndex = 1; waypointIndex < route.waypoints.Count; waypointIndex++)
            {
                GeneratedSpatialPointRecord from = route.waypoints[waypointIndex - 1];
                GeneratedSpatialPointRecord to = route.waypoints[waypointIndex];
                Vector2 start = new Vector2(from.x, from.z);
                Vector2 end = new Vector2(to.x, to.z);
                if (waypointIndex == 1)
                    start = ResolvePhysicalRouteEndpoint(terrain, plan, route.fromLocationId, start);
                if (waypointIndex == route.waypoints.Count - 1)
                    end = ResolvePhysicalRouteEndpoint(terrain, plan, route.toLocationId, end);
                bool major = string.Equals(
                                 route.routeKind,
                                 "major_road",
                                 StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(
                                 route.routeKind,
                                 "transit_corridor",
                                 StringComparison.OrdinalIgnoreCase);
                segments.Add(new RouteSampleSegment(
                    route.routeId,
                    start,
                    end,
                    major ? 1.2f : 2.1f,
                    StableDeterministic01(route.routeId + "|" + waypointIndex) *
                    Mathf.PI * 2f));
            }
        }

        for (int settlementIndex = 0; settlementIndex < plan.settlements.Count; settlementIndex++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[settlementIndex];
            if (settlement == null ||
                !YQGeneratedWorldSpatialPlanner.TryGetLocation(
                    plan,
                    settlement.settlementId,
                    out GeneratedSpatialLocationRecord location))
            {
                continue;
            }

            Vector3 worldAnchor = YQGeneratedWorldLayout.GetSettlementAnchor(plan, settlement, terrain);
            Vector2 center = new Vector2(worldAnchor.x, worldAnchor.z);
            float headingRadians = location.entranceHeadingDegrees * Mathf.Deg2Rad;
            Vector2 entrance = new Vector2(Mathf.Sin(headingRadians), Mathf.Cos(headingRadians));
            float radius = Mathf.Clamp(location.footprintRadius, 24f, 72f);
            // note: Validation mirrors the rendered settlement spine so a relocated construction shelf cannot pass based only on its obsolete planned endpoint.
            segments.Add(new RouteSampleSegment(
                settlement.settlementId + "|entrance_spine",
                center + entrance * (radius + 14f),
                center - entrance * (radius * 0.68f),
                0.65f,
                StableDeterministic01(
                    settlement.settlementId + "|entrance_spine") *
                Mathf.PI * 2f));
            if (YQGeneratedSettlementCellLayout.ResolveTemplate(plan, settlement) !=
                YQGeneratedSettlementCellLayout.Template.Compact)
            {
                YQGeneratedSettlementCellLayout.Template template =
                    YQGeneratedSettlementCellLayout.ResolveTemplate(plan, settlement);
                Vector2 cross = new Vector2(-entrance.y, entrance.x);
                float localStreetOffset =
                    template == YQGeneratedSettlementCellLayout.Template.MarketVillage
                        ? -7f
                        : template == YQGeneratedSettlementCellLayout.Template.FortifiedOutpost
                            ? -5f
                            : 0f;
                Vector2 crossCenter = center + entrance * localStreetOffset;
                float crossHalfLength = Mathf.Min(26f, radius * 0.48f);
                segments.Add(new RouteSampleSegment(
                    settlement.settlementId + "|cross_street",
                    crossCenter - cross * crossHalfLength,
                    crossCenter + cross * crossHalfLength,
                    0.35f,
                    StableDeterministic01(
                        settlement.settlementId + "|cross_street") *
                    Mathf.PI * 2f));
            }
        }

        return segments;
    }

    private static Vector2 ResolvePhysicalRouteEndpoint(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        string locationId,
        Vector2 fallback)
    {
        if (string.Equals(locationId, "origin_vey", StringComparison.OrdinalIgnoreCase))
        {
            Vector3 origin = YQGeneratedWorldLayout.GetVeyOriginAnchor();
            return new Vector2(origin.x, origin.z);
        }

        for (int index = 0; index < plan.settlements.Count; index++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[index];
            if (settlement != null &&
                string.Equals(settlement.settlementId, locationId, StringComparison.OrdinalIgnoreCase))
            {
                Vector3 anchor = YQGeneratedWorldLayout.GetSettlementAnchor(plan, settlement, terrain);
                Vector2 endpoint = new Vector2(anchor.x, anchor.z);
                if (YQGeneratedWorldSpatialPlanner.TryGetLocation(
                        plan,
                        settlement.settlementId,
                        out GeneratedSpatialLocationRecord location))
                {
                    float headingRadians = location.entranceHeadingDegrees * Mathf.Deg2Rad;
                    Vector2 entrance = new Vector2(
                        Mathf.Sin(headingRadians),
                        Mathf.Cos(headingRadians));
                    endpoint += entrance *
                        (Mathf.Clamp(location.footprintRadius, 24f, 72f) + 14f);
                }
                return endpoint;
            }
        }

        for (int index = 0; index < plan.encampments.Count; index++)
        {
            GeneratedEncampmentRecord encampment = plan.encampments[index];
            if (encampment != null &&
                string.Equals(encampment.encampmentId, locationId, StringComparison.OrdinalIgnoreCase))
            {
                Vector3 anchor = YQGeneratedWorldLayout.GetEncampmentAnchor(plan, encampment, terrain);
                return new Vector2(anchor.x, anchor.z);
            }
        }

        return fallback;
    }

    private static void InspectRouteClearance(
        PhysicsScene physics,
        Vector2 point,
        float groundHeight,
        string routeId,
        string worldSeed,
        HashSet<int> unresolvedBlockers,
        RouteReport report)
    {
        Vector3 lowerCenter = new Vector3(point.x, groundHeight + 0.40f, point.y);
        Vector3 upperCenter = new Vector3(point.x, groundHeight + 1.45f, point.y);
        int overlapCount = physics.OverlapCapsule(
            lowerCenter,
            upperCenter,
            0.34f,
            RouteClearanceBuffer,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        // note: A full non-alloc buffer cannot prove clearance; keep explicit evidence without retrying unbounded queries.
        if (overlapCount == RouteClearanceBuffer.Length)
        {
            report.clearanceQuerySaturations++;
            if (report.clearanceQuerySaturations == 1)
                Debug.LogWarning("[WORLDGEN WARNING] Route clearance query saturated; collision was preserved and coverage is incomplete.");
        }

        for (int index = 0; index < overlapCount; index++)
        {
            Collider obstacle = RouteClearanceBuffer[index];
            if (obstacle == null || !obstacle.enabled || obstacle.isTrigger ||
                obstacle is TerrainCollider ||
                obstacle.bounds.max.y <= groundHeight + 0.12f ||
                HasProtectedRouteSemantic(obstacle.transform) ||
                obstacle.GetComponentInParent<EntityInfo>() != null ||
                obstacle.GetComponentInParent<YQInvestorPlayerMotor>() != null ||
                obstacle.attachedRigidbody != null)
            {
                continue;
            }

            // note: Arrival-envelope overlaps are expected at a settlement gate; collision remains enabled and is excluded only from the hard route gate.
            if (IsExpectedSettlementArrival(routeId, obstacle))
                continue;

            if (IsGeneratedRouteDecoration(obstacle.transform))
            {
                // note: A generated fence, rock, shrub, or roadside prop may overlap the capsule without owning gameplay; convert only those semantic decorations to triggers.
                obstacle.isTrigger = true;
                report.repairedDecorativeBlockers++;
                continue;
            }

            int instanceId = obstacle.GetInstanceID();
            if (unresolvedBlockers.Add(instanceId))
            {
                // note: Size/name alone cannot authorize removal: short walls, doors and interactables must retain collision.
                // note: Keep the legacy counter field but classify these as unresolved, not proven structural/decorative objects.
                report.unresolvedStructuralBlockers++;
                if (report.unresolvedStructuralBlockers <= 8)
                    Debug.LogWarning(
                    "[WORLDGEN WARNING] Unresolved collider obstructs a mandatory travel route; collision preserved. " +
                    "Seed=" + (worldSeed ?? string.Empty) +
                    ", route=" + (routeId ?? string.Empty) +
                    ", object=" + HierarchyPath(obstacle.transform) + ".");
            }
        }
    }

    private static bool IsExpectedSettlementArrival(string routeId, Collider collider)
    {
        // note: Restrict this tolerance to persisted routes ending at a settlement and colliders owned by that compiled settlement.
        if (string.IsNullOrWhiteSpace(routeId) || collider == null ||
            routeId.IndexOf("--site:settlement:", StringComparison.OrdinalIgnoreCase) < 0)
            return false;
        return HierarchyPath(collider.transform).IndexOf("CompiledSettlement__", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsExpectedSettlementEntranceSupport(string routeId, Collider collider)
    {
        // note: Limit the entrance-tread tolerance to generated settlement cell streets and the reviewed entrance-repair hierarchy; arbitrary floors and walls remain blocking.
        if (string.IsNullOrWhiteSpace(routeId) || collider == null ||
            routeId.IndexOf("|cell_street|", StringComparison.OrdinalIgnoreCase) < 0)
            return false;
        string path = HierarchyPath(collider.transform);
        return path.IndexOf("CompiledSettlement__", StringComparison.OrdinalIgnoreCase) >= 0 &&
            path.IndexOf("YQ_V2_EntranceRepairs", StringComparison.OrdinalIgnoreCase) >= 0 &&
            (path.IndexOf("LowerLanding", StringComparison.OrdinalIgnoreCase) >= 0 ||
             path.IndexOf("TerrainContact", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsGeneratedRouteDecoration(Transform transform)
    {
        // note: Keep structural walls, doors, interactables, and entity colliders solid while relaxing only explicitly decorative route clutter.
        for (Transform current = transform; current != null; current = current.parent)
        {
            string name = current.name.ToLowerInvariant();
            if (name.Contains("vegetation") || name.Contains("foliage") ||
                name.Contains("wilderness") || name.Contains("shrub") ||
                name.Contains("bush") || name.Contains("tree") ||
                name.Contains("grass") || name.Contains("flower") ||
                name.Contains("rock") || name.Contains("boulder") ||
                name.Contains("pebble") || name.Contains("prop") ||
                name.Contains("clutter") || name.Contains("fence") ||
                name.Contains("roadside") ||
                // note: Imported vendor shop-plane proxies are presentation clutter, not structural walls or service interaction colliders.
                name.Contains("shop_plane"))
                return true;
            // note: Bridge rail posts are visual safety dressing, not the generated walk surface; classify them as non-blocking even if an imported collider survives one frame.
            if (name.Contains("bridgerailpost") &&
                current.parent != null &&
                current.parent.name.IndexOf("Generated_RiverBridge_", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (current == transform.root)
                break;
        }
        return false;
    }

    private static bool IsGeneratedRouteDecoration(Collider collider)
    {
        // note: Collider ownership is resolved through its hierarchy so generated dressing remains safe even when the probe reports a child mesh instead of the semantic parent.
        return collider != null && IsGeneratedRouteDecoration(collider.transform);
    }

    private static bool HasProtectedRouteSemantic(Transform transform)
    {
        int depth = 0;
        for (Transform current = transform;
             current != null && depth < 3;
             current = current.parent, depth++)
        {
            string name = current.name;
            if (name.IndexOf("road", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("street", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (depth == 0 &&
                 name.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0) ||
                name.IndexOf("bridge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("step", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("platform", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("foundation", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static float StableDeterministic01(string value)
    {
        const uint offsetBasis = 2166136261u;
        const uint prime = 16777619u;
        uint hash = offsetBasis;
        if (value != null)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                hash ^= (byte)(character & 0xFF);
                hash *= prime;
                hash ^= (byte)((character >> 8) & 0xFF);
                hash *= prime;
            }
        }

        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    private readonly struct RouteSampleSegment
    {
        public readonly string routeId;
        public readonly Vector2 start;
        public readonly Vector2 end;
        public readonly float curveAmplitude;
        public readonly float curvePhase;

        public RouteSampleSegment(
            string routeId,
            Vector2 start,
            Vector2 end,
            float curveAmplitude,
            float curvePhase)
        {
            this.routeId = routeId ?? string.Empty;
            this.start = start;
            this.end = end;
            this.curveAmplitude = curveAmplitude;
            this.curvePhase = curvePhase;
        }

        public Vector2 ResolveCenter(float pathT)
        {
            pathT = Mathf.Clamp01(pathT);
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < 0.001f)
                return start;
            Vector2 direction = delta.normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float curve =
                Mathf.Sin(pathT * Mathf.PI) *
                Mathf.Sin(pathT * Mathf.PI * 2f + curvePhase) *
                curveAmplitude;
            return Vector2.Lerp(start, end, pathT) + perpendicular * curve;
        }
    }

    private static void ValidateWalkableSurface(Renderer renderer, Report report)
    {
        GameObject surface = renderer.gameObject;
        // note: A generated bridge separates render-only arch meshes from its fitted walk-surface collider; never synthesize a second collider through the stonework.
        if (surface.GetComponentInParent<YQGeneratedRiverBridge>() != null &&
            surface.name == "StoneBridgeDeck")
        {
            report.validColliders++;
            return;
        }
        // note: Generated terrain overlays declare their physical owner explicitly; continuous route probes still test that terrain and all actual obstacles.
        var overlay = surface.GetComponent<YQTerrainSurfaceOverlay>();
        if (overlay != null && overlay.HasValidSupport)
        {
            report.validColliders++;
            return;
        }
        Collider[] colliders = surface.GetComponents<Collider>();
        Collider valid = null;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
                continue;
            if (!collider.enabled)
            {
                report.disabledColliders++;
                collider.enabled = true;
            }
            if (collider.enabled && !collider.isTrigger)
                valid = collider;
        }

        if (valid == null)
        {
            valid = AddSafeCollider(surface, renderer);
            if (valid != null)
                report.repairedColliders++;
        }

        if (valid == null)
        {
            report.missingColliders++;
            Debug.LogError(
                "[WORLDGEN ERROR] Walkable mesh spawned without collider. " +
                "Object=" + HierarchyPath(surface.transform) +
                ", renderer=" + renderer.GetType().Name + ".");
            return;
        }

        int playerLayer = YQInvestorPlayerMotor.ActiveMotor != null
            ? YQInvestorPlayerMotor.ActiveMotor.gameObject.layer
            : 0;
        if (Physics.GetIgnoreLayerCollision(surface.layer, playerLayer))
        {
            // note: Required generated floors use Default only when their source layer cannot collide with the authoritative player.
            surface.layer = 0;
            report.invalidLayers++;
        }
        report.validColliders++;
    }

    private static Collider AddSafeCollider(GameObject surface, Renderer renderer)
    {
        MeshFilter filter = surface.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            Mesh mesh = filter.sharedMesh;
            long indexCount = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                indexCount += (long)mesh.GetIndexCount(subMesh);
            int triangles = (int)Math.Min(int.MaxValue, indexCount / 3L);
            if (triangles > 0 && triangles <= MaximumAutomaticMeshColliderTriangles)
            {
                MeshCollider meshCollider = surface.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
                meshCollider.convex = false;
                return meshCollider;
            }
        }

        Bounds worldBounds = renderer.bounds;
        if (!IsValidBounds(worldBounds))
            return null;

        BoxCollider box = surface.AddComponent<BoxCollider>();
        box.center = surface.transform.InverseTransformPoint(worldBounds.center);
        Vector3 scale = surface.transform.lossyScale;
        box.size = new Vector3(
            worldBounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            Mathf.Max(0.08f, worldBounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y))),
            worldBounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
        return box;
    }

    private static void RepairRendererVisibility(
        Renderer renderer,
        Report report,
        Camera gameplayCamera,
        int generatedVisibleLayer)
    {
        renderer.forceRenderingOff = false;
        renderer.allowOcclusionWhenDynamic = false;

        if (gameplayCamera != null &&
            (gameplayCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
        {
            // note: Imported source-scene layers are remapped only when the gameplay camera cannot render them; generated geometry must not vanish because of a donor scene's culling mask.
            renderer.gameObject.layer = generatedVisibleLayer;
        }

        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null &&
            !IsValidBounds(filter.sharedMesh.bounds))
        {
            // note: Procedural or imported meshes with invalid local bounds are recalculated before culling and collider validation.
            filter.sharedMesh.RecalculateBounds();
            report.repairedRendererBounds++;
        }
    }

    private static IEnumerator RepairLodGroupsRoutine(GameObject root, Report report)
    {
        List<LODGroup> groups = new List<LODGroup>();
        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(root.transform);
        float frameStartedAt = Time.realtimeSinceStartup;
        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            if (current == null)
                continue;
            for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                pending.Push(current.GetChild(childIndex));
            LODGroup localGroup = current.GetComponent<LODGroup>();
            if (localGroup != null)
                groups.Add(localGroup);

            if (Time.realtimeSinceStartup - frameStartedAt >= FrameBudgetSeconds)
            {
                // note: LOD discovery itself is cooperative; a large imported hierarchy is never scanned in one loading frame.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            LODGroup group = groups[groupIndex];
            if (group == null)
                continue;
            LOD[] lods = group.GetLODs();
            bool invalid = lods == null || lods.Length == 0 ||
                           lods[0].renderers == null || lods[0].renderers.Length == 0;
            float previous = 1f;
            for (int lodIndex = 0; !invalid && lodIndex < lods.Length; lodIndex++)
            {
                float threshold = lods[lodIndex].screenRelativeTransitionHeight;
                if (threshold <= 0f || threshold >= previous)
                    invalid = true;
                previous = threshold;
            }

            if (invalid)
            {
                // note: An invalid close LOD is disabled locally and its child renderers stay visible; valid authored LOD groups remain active for performance.
                group.enabled = false;
                Renderer[] owned = group.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < owned.Length; i++)
                {
                    if (owned[i] != null)
                        owned[i].enabled = true;
                }
                report.repairedLodGroups++;
            }
            else
            {
                group.fadeMode = LODFadeMode.None;
                group.animateCrossFading = false;
                group.RecalculateBounds();
            }

            if (Time.realtimeSinceStartup - frameStartedAt >= FrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }
    }

    private static bool IsWalkableSurface(GameObject gameObject)
    {
        string name = gameObject.name.ToLowerInvariant();
        return ContainsAny(
            name,
            "floor", "ground", "road", "path", "street", "sidewalk",
            "bridge", "platform", "foundation", "walkway", "stair", "step",
            "courtyard", "plaza", "terrain");
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        for (int i = 0; i < terms.Length; i++)
        {
            if (value.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool IsValidBounds(Bounds bounds)
    {
        Vector3 size = bounds.size;
        Vector3 center = bounds.center;
        return IsFinite(size.x) && IsFinite(size.y) && IsFinite(size.z) &&
               IsFinite(center.x) && IsFinite(center.y) && IsFinite(center.z) &&
               size.x > 0.001f && size.y > 0.001f && size.z > 0.001f &&
               size.x < 4096f && size.y < 1024f && size.z < 4096f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string HierarchyPath(Transform transform)
    {
        string path = transform != null ? transform.name : "<missing>";
        Transform current = transform != null ? transform.parent : null;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }
}
