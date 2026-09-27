using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class YQSemanticWorldAuthorityTests
{
    // note: This focused runner records only the current imported semantic authority assembly and its V1 immutability checks.
    private const string PendingRequestPath =
        "Assets/Assets/EditorBuildRequests/RunSemanticWorldAuthorityTests.request";

    private static bool polling;
    private static double nextPoll;

    [InitializeOnLoadMethod]
    private static void QueueRequestedRun()
    {
        // note: A request marker keeps the fixture runnable in the existing Unity project owner without inventing a test scene.
        EditorApplication.delayCall += RunRequestedTests;
        if (polling)
            return;
        polling = true;
        EditorApplication.update += RunRequestedTests;
    }

    private static void RunRequestedTests()
    {
        if (EditorApplication.timeSinceStartup < nextPoll)
            return;
        nextPoll = EditorApplication.timeSinceStartup + 1d;
        if (!File.Exists(Path.GetFullPath(PendingRequestPath)) ||
            EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        // note: Consume the marker only after imports settle so the result describes one stable source/build identity.
        AssetDatabase.DeleteAsset(PendingRequestPath);
        polling = false;
        EditorApplication.update -= RunRequestedTests;
        RunFromMenu();
    }

    [MenuItem("YourQuest/World Generation/Run Semantic World Authority Tests")]
    public static void RunFromMenu()
    {
        int failures = RunTests(out int tested);
        WriteReport(tested, failures);
        Debug.Log("[YQSemanticWorldTests] Tested " + tested + " semantic contracts; failures=" + failures + ".");
    }

    public static void RunBatch()
    {
        int failures = RunTests(out int tested);
        Debug.Log("[YQSemanticWorldTests] Tested " + tested + " semantic contracts; failures=" + failures + ".");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static int RunTests(out int tested)
    {
        tested = 0;
        int failures = 0;
        Run("deterministic coordinates and order", TestDeterminismAndOrder, ref tested, ref failures);
        Run("shared boundary contracts", TestSharedBoundaries, ref tested, ref failures);
        Run("site reservations and route ownership", TestSitesRoutesAndOwnership, ref tested, ref failures);
        Run("water source/downstream/sink graph", TestWaterGraph, ref tested, ref failures);
        Run("empty cells do not synthesize routes", TestEmptyCellRoutePolicy, ref tested, ref failures);
        Run("accepted V2 adapter and opening envelope", TestAcceptedV2Adapter, ref tested, ref failures);
        return failures;
    }

    private static void Run(string name, Func<string> test, ref int tested, ref int failures)
    {
        tested++;
        string failure = test();
        if (string.IsNullOrWhiteSpace(failure))
            return;
        failures++;
        Debug.LogError("[YQSemanticWorldTests] " + name + ": " + failure);
    }

    private static void WriteReport(int tested, int failures)
    {
        // note: Persist the latest focused result separately from the rolling Unity editor log so acceptance evidence is unambiguous.
        string path = Path.GetFullPath("Logs/YQSemanticWorldAuthorityTests.md");
        File.WriteAllText(path,
            "# YourQuest Semantic World Authority Tests\n" +
            "- generatedUtc: " + DateTime.UtcNow.ToString("O") + "\n" +
            "- contracts: " + tested + "\n" +
            "- failures: " + failures + "\n" +
            "- status: " + (failures == 0 ? "PASS" : "FAIL") + "\n" +
            "- evidence: deterministic coordinate/order, shared boundaries, site ownership, accepted overrides, route endpoints, water graph/crossings, empty-cell policy, huge-POI policy, accepted V2 preservation, opening envelope, bounded neighborhood\n");
    }

    private static string TestDeterminismAndOrder()
    {
        GeneratedWorldPlanRecord firstPlan = BuildFixture("semantic-golden-042");
        GeneratedWorldPlanRecord secondPlan = BuildFixture("semantic-golden-042");
        Vector2Int[] coordinates =
        {
            new Vector2Int(4, 4), new Vector2Int(8, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, -1), new Vector2Int(-7, 9), new Vector2Int(41, -37)
        };
        for (int index = 0; index < coordinates.Length; index++)
        {
            GeneratedSemanticCellPlanRecord first = YQGeneratedWorldQuery.GetSemanticCellPlan(firstPlan, coordinates[index]);
            GeneratedSemanticCellPlanRecord second = YQGeneratedWorldQuery.GetSemanticCellPlan(secondPlan, coordinates[index]);
            if (first == null || second == null || first.semanticHash != second.semanticHash)
                return "same seed changed cell hash at " + coordinates[index];
            if (string.IsNullOrWhiteSpace(first.ownerRegionId) || string.IsNullOrWhiteSpace(first.continentId) ||
                string.IsNullOrWhiteSpace(first.landformRegime) || string.IsNullOrWhiteSpace(first.paletteContext) ||
                string.IsNullOrWhiteSpace(first.localSeed) || string.IsNullOrWhiteSpace(first.routeConstraint) ||
                string.IsNullOrWhiteSpace(first.waterConstraint) || first.featureIds.Count == 0)
                return "cell plan omitted a required semantic field at " + coordinates[index];
        }

        Vector2Int[] orderA = { new Vector2Int(41, -37), new Vector2Int(-1, 0), new Vector2Int(4, 4), new Vector2Int(0, -1) };
        Vector2Int[] orderB = { new Vector2Int(0, -1), new Vector2Int(4, 4), new Vector2Int(41, -37), new Vector2Int(-1, 0) };
        Dictionary<string, string> hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < orderA.Length; index++)
        {
            GeneratedSemanticCellPlanRecord cell = YQGeneratedWorldQuery.GetSemanticCellPlan(firstPlan, orderA[index]);
            hashes[cell.cellId] = cell.semanticHash;
        }
        for (int index = 0; index < orderB.Length; index++)
        {
            GeneratedSemanticCellPlanRecord cell = YQGeneratedWorldQuery.GetSemanticCellPlan(firstPlan, orderB[index]);
            if (!hashes.TryGetValue(cell.cellId, out string expected) || expected != cell.semanticHash)
                return "query order changed " + cell.cellId;
        }
        // note: Read the initialized immutable authority concurrently to prove parallel query order does not alter hashes or feature ownership.
        YQSemanticWorldAuthority.Ensure(firstPlan);
        string[] parallelHashes = new string[coordinates.Length];
        Parallel.For(0, coordinates.Length, index =>
        {
            parallelHashes[index] = YQGeneratedWorldQuery.GetSemanticCellPlan(firstPlan, coordinates[index]).semanticHash;
        });
        for (int index = 0; index < coordinates.Length; index++)
            if (parallelHashes[index] != YQGeneratedWorldQuery.GetSemanticCellPlan(firstPlan, coordinates[index]).semanticHash)
                return "parallel query changed " + coordinates[index];
        return string.Empty;
    }

    private static string TestSharedBoundaries()
    {
        GeneratedWorldPlanRecord plan = BuildFixture("semantic-boundary-019");
        Vector2Int[] coordinates = { new Vector2Int(4, 4), new Vector2Int(-1, 0), new Vector2Int(12, -9) };
        for (int index = 0; index < coordinates.Length; index++)
        {
            Vector2Int coordinate = coordinates[index];
            GeneratedSemanticCellPlanRecord cell = YQGeneratedWorldQuery.GetSemanticCellPlan(plan, coordinate);
            GeneratedSemanticCellPlanRecord east = YQGeneratedWorldQuery.GetSemanticCellPlan(plan, coordinate + Vector2Int.right);
            GeneratedSemanticCellPlanRecord north = YQGeneratedWorldQuery.GetSemanticCellPlan(plan, coordinate + Vector2Int.up);
            GeneratedSemanticBoundaryRecord eastContract = FindBoundary(cell, "east");
            GeneratedSemanticBoundaryRecord westContract = FindBoundary(east, "west");
            GeneratedSemanticBoundaryRecord northContract = FindBoundary(cell, "north");
            GeneratedSemanticBoundaryRecord southContract = FindBoundary(north, "south");
            if (eastContract == null || westContract == null || eastContract.canonicalKey != westContract.canonicalKey || eastContract.contractHash != westContract.contractHash)
                return "east/west boundary mismatch at " + coordinate;
            if (northContract == null || southContract == null || northContract.canonicalKey != southContract.canonicalKey || northContract.contractHash != southContract.contractHash)
                return "north/south boundary mismatch at " + coordinate;
        }
        return string.Empty;
    }

    private static string TestSitesRoutesAndOwnership()
    {
        GeneratedWorldPlanRecord plan = BuildFixture("semantic-sites-733");
        GeneratedSpatialWorldPlanRecord v1Before = YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
        string v1ArtifactBefore = JsonUtility.ToJson(v1Before);
        GeneratedSemanticWorldAuthorityRecord authority = YQSemanticWorldAuthority.Ensure(plan);
        if (authority.siteReservations.Count < 4 || authority.routeGraph.Count < 3)
            return "accepted site/route graph is incomplete";
        if (authority.acceptedOverrides.Count < 6)
            return "accepted names/layout bindings were not persisted in the semantic envelope";
        if (v1ArtifactBefore != JsonUtility.ToJson(plan.spatialPlan))
            return "semantic adaptation rewrote the accepted V1 spatial artifact";
        GeneratedSemanticAcceptedOverrideRecord settlementOverride = FindAcceptedOverride(authority, "fixture_settlement_0");
        if (settlementOverride == null || settlementOverride.displayName != "Fixture Gate" || settlementOverride.layoutBindingId != "fixture-town-kit")
            return "accepted settlement presentation binding was not persisted";
        // note: Add deterministic city/castle/dungeon large-site contracts to the isolated fixture so multi-cell ownership is tested independently of whichever small V1 locations the planner emits.
        GeneratedSemanticSiteReservationRecord largeSite = BuildLargeSiteFixtureReservation(
            "fixture:city:large", "city", "multi_cell_city_contract", 64f, 64f, "fixture:city:ingress");
        GeneratedSemanticSiteReservationRecord castleSite = BuildLargeSiteFixtureReservation(
            "fixture:castle:large", "castle", "multi_cell_castle_contract", 640f, -64f, string.Empty);
        GeneratedSemanticSiteReservationRecord dungeonSite = BuildLargeSiteFixtureReservation(
            "fixture:dungeon:large", "multi_cell_dungeon", "multi_cell_dungeon_contract", -192f, 640f, string.Empty);
        string connectedSiteId = authority.siteReservations[0].siteId;
        authority.siteReservations.Add(largeSite);
        authority.siteReservations.Add(castleSite);
        authority.siteReservations.Add(dungeonSite);
        largeSite.routeIds.Add("fixture:city:ingress");
            authority.routeGraph.Add(new GeneratedSemanticRouteGraphRecord
        {
            routeId = "fixture:city:ingress",
            parentRouteId = "fixture:city:ingress",
            ownerRegionId = largeSite.ownerRegionId,
            fromSiteId = connectedSiteId,
            toSiteId = largeSite.siteId,
            routeClass = "city_ingress",
            accepted = true,
            permittedBoundaryContinuation = false,
            points = new List<GeneratedSemanticRoutePointRecord>
            {
                new GeneratedSemanticRoutePointRecord { worldX = largeSite.worldX - largeSite.footprintRadius, worldZ = largeSite.worldZ },
                new GeneratedSemanticRoutePointRecord { worldX = largeSite.worldX, worldZ = largeSite.worldZ }
            }
        });
        // note: Keep the city/castle/dungeon contract kinds explicit so future materializers cannot silently collapse all large footprints into one settlement-only branch.
        if (castleSite.memberCellIds.Count < 9 || dungeonSite.memberCellIds.Count < 9 ||
            castleSite.siteKind != "castle" || dungeonSite.siteKind != "multi_cell_dungeon")
            return "large-site city/castle/dungeon contract coverage is incomplete";
        // note: Re-query the authority after adding the fixture reservation to prove normalization preserves its footprint and owner identity across a second access.
        GeneratedSemanticWorldAuthorityRecord normalizedAuthority = YQSemanticWorldAuthority.Ensure(plan);
        GeneratedSemanticSiteReservationRecord normalizedLargeSite = normalizedAuthority.siteReservations.Find(candidate => candidate != null && candidate.siteId == largeSite.siteId);
        if (normalizedLargeSite == null || normalizedLargeSite.memberCellIds.Count != largeSite.memberCellIds.Count ||
            normalizedLargeSite.ownerCellId != largeSite.ownerCellId)
            return "large-site reservation changed during authority normalization";
        // note: Confirm the representative city spans multiple sectors and exposes stable ingress/egress anchors inside its persisted bounds.
        if (largeSite.memberCellIds.Count < 9 || largeSite.entrances.Count != 2 ||
            largeSite.entrances[0].entranceId != largeSite.siteId + "|entrance|west" ||
            largeSite.entrances[1].entranceId != largeSite.siteId + "|entrance|east" ||
            Mathf.Abs(largeSite.entrances[0].worldX - (largeSite.worldX - largeSite.footprintRadius)) > 0.01f ||
            Mathf.Abs(largeSite.entrances[1].worldX - (largeSite.worldX + largeSite.footprintRadius)) > 0.01f)
            return "large-site ingress/egress contract is not stable";
        HashSet<string> owners = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < authority.siteReservations.Count; index++)
        {
            GeneratedSemanticSiteReservationRecord site = authority.siteReservations[index];
            if (site == null || string.IsNullOrWhiteSpace(site.siteId) || site.entrances.Count == 0 || site.memberCellIds.Count == 0)
                return "site reservation lacks immutable footprint or entrance: " + site?.siteId;
            // note: Require one persisted owner that is also inside the accepted member footprint; neighboring sectors may project the site but cannot claim publication ownership.
            if (string.IsNullOrWhiteSpace(site.ownerCellId) || !site.memberCellIds.Contains(site.ownerCellId))
                return "site reservation lacks a member-cell owner: " + site?.siteId;
            if (!owners.Add(site.ownerFeatureId))
                return "two reservations share an owner feature: " + site.ownerFeatureId;
            // note: Verify a reserved site can be discovered from its owning cell through the stable public query adapter.
            Vector2Int siteCell = new Vector2Int(
                Mathf.FloorToInt((site.worldX + 512f) / YQSemanticWorldAuthority.CellSizeMeters),
                Mathf.FloorToInt((site.worldZ + 512f) / YQSemanticWorldAuthority.CellSizeMeters));
            if (!YQSemanticWorldAuthority.IsOwnerCell(site, siteCell))
                return "site center does not resolve to its persisted owner cell: " + site.siteId;
            // note: Exercise the streamed projection at the owner boundary so a persisted footprint cannot be mistaken for physical publication in every member sector.
            YQContinuousWorldCellAuthority cellAuthority =
                new YQContinuousWorldCellAuthority(
                    plan.worldSeed,
                    null,
                    null,
                    plan,
                    YQSemanticWorldAuthority.CellSizeMeters);
            GeneratedSemanticChunkRecord ownerRecord =
                new GeneratedSemanticChunkRecord();
            cellAuthority.PopulateCellRecord(
                ownerRecord,
                siteCell,
                YQSemanticWorldAuthority.CellSizeMeters,
                plan);
            GeneratedSemanticSiteRecord projectedOwner =
                ownerRecord.sites.Find(
                    candidate => candidate != null && candidate.siteId == site.siteId);
            if (projectedOwner == null || !projectedOwner.isOwnerCell ||
                projectedOwner.ownerCellId != site.ownerCellId)
                return "owner sector did not publish the persisted site ownership projection: " + site.siteId;
            // note: The physical materializer consumes the cell identity index, so ownership projection is incomplete if the accepted site ID remains only in the nested site record.
            if (!ownerRecord.featureIds.Contains(site.siteId))
                return "owner sector omitted the accepted site identity index: " + site.siteId;
            if (site.memberCellIds.Count > 1)
            {
                string memberId = site.memberCellIds.Find(
                    candidate => !string.Equals(candidate, site.ownerCellId, StringComparison.Ordinal));
                if (TryParseCellId(memberId, out Vector2Int memberCell))
                {
                    GeneratedSemanticChunkRecord memberRecord =
                        new GeneratedSemanticChunkRecord();
                    cellAuthority.PopulateCellRecord(
                        memberRecord,
                        memberCell,
                        YQSemanticWorldAuthority.CellSizeMeters,
                        plan);
                    GeneratedSemanticSiteRecord projectedMember =
                        memberRecord.sites.Find(
                            candidate => candidate != null && candidate.siteId == site.siteId);
                    if (projectedMember == null || projectedMember.isOwnerCell ||
                        projectedMember.ownerCellId != site.ownerCellId)
                        return "non-owner member sector claimed site publication ownership: " + site.siteId;
                }
            }
            bool siteFoundInCell = false;
            List<GeneratedSemanticSiteReservationRecord> cellSites = YQGeneratedWorldQuery.GetSemanticSites(plan, siteCell);
            for (int siteIndex = 0; siteIndex < cellSites.Count; siteIndex++)
            {
                GeneratedSemanticSiteReservationRecord candidate = cellSites[siteIndex];
                siteFoundInCell |= candidate != null && candidate.siteId == site.siteId;
                if (candidate != null && !candidate.accepted && candidate.accessScore < authority.syntheticMinimumAccessScore)
                    return "lazy candidate violated access budget: " + candidate.siteId;
                if (candidate != null && candidate.siteId != site.siteId && !candidate.accepted &&
                    Vector2.Distance(new Vector2(candidate.worldX, candidate.worldZ), new Vector2(site.worldX, site.worldZ)) <
                    Mathf.Max(site.minimumExclusionRadius, site.footprintRadius) + candidate.footprintRadius)
                    return "lazy candidate violated accepted site exclusion: " + candidate.siteId;
            }
            if (!siteFoundInCell)
                return "site reservation was not returned by its owning cell query: " + site.siteId;
            if (!YQGeneratedWorldQuery.TryGetSemanticSite(plan, site.siteId, out GeneratedSemanticSiteReservationRecord resolved) || resolved.worldX != site.worldX)
                return "unloaded site lookup did not resolve: " + site.siteId;
        }
        // note: Visit every persisted member sector of the representative city and require exactly one physical owner projection with a shared footprint identity.
        YQContinuousWorldCellAuthority largeSiteCellAuthority =
            new YQContinuousWorldCellAuthority(plan.worldSeed, null, null, plan, YQSemanticWorldAuthority.CellSizeMeters);
        int largeSiteOwnerCount = 0;
        for (int memberIndex = 0; memberIndex < largeSite.memberCellIds.Count; memberIndex++)
        {
            if (!TryParseCellId(largeSite.memberCellIds[memberIndex], out Vector2Int memberCell))
                return "large-site member cell identity was not parseable";
            GeneratedSemanticChunkRecord memberRecord = new GeneratedSemanticChunkRecord();
            largeSiteCellAuthority.PopulateCellRecord(memberRecord, memberCell, YQSemanticWorldAuthority.CellSizeMeters, plan);
            GeneratedSemanticSiteRecord projected = memberRecord.sites.Find(candidate => candidate != null && candidate.siteId == largeSite.siteId);
            if (projected == null || projected.ownerCellId != largeSite.ownerCellId ||
                projected.memberCellIds.Count != largeSite.memberCellIds.Count)
                return "large-site member sector lost persisted footprint identity: " + largeSite.memberCellIds[memberIndex];
            if (projected.isOwnerCell)
                largeSiteOwnerCount++;
        }
        if (largeSiteOwnerCount != 1)
            return "large-site projected more than one physical owner sector";
        for (int index = 0; index < authority.routeGraph.Count; index++)
        {
            GeneratedSemanticRouteGraphRecord route = authority.routeGraph[index];
            if (route == null || route.points.Count < 2 || !YQGeneratedWorldQuery.TryGetSemanticSite(plan, route.fromSiteId, out _) || !YQGeneratedWorldQuery.TryGetSemanticSite(plan, route.toSiteId, out _))
                return "route endpoint did not resolve: " + route?.routeId;
        }
        return string.Empty;
    }

    private static GeneratedSemanticSiteReservationRecord BuildLargeSiteFixtureReservation(
        string siteId, string siteKind, string structuralIntent, float worldX, float worldZ, string routeId)
    {
        const float radius = 190f;
        // note: Use the same signed 128 metre grid formula as production ownership so this fixture proves the real sector contract instead of a test-only coordinate convention.
        int ownerX = Mathf.FloorToInt((worldX + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        int ownerZ = Mathf.FloorToInt((worldZ + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        List<string> memberCellIds = new List<string>();
        int minX = Mathf.FloorToInt((worldX - radius + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        int maxX = Mathf.FloorToInt((worldX + radius + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        int minZ = Mathf.FloorToInt((worldZ - radius + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        int maxZ = Mathf.FloorToInt((worldZ + radius + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
            memberCellIds.Add("cell:" + x + ":" + z);
        // note: Keep two opposite anchors on the accepted footprint boundary so future city materialization can bind ingress and egress without inventing a second site identity.
        return new GeneratedSemanticSiteReservationRecord
        {
            siteId = siteId,
            ownerRegionId = "fixture_region_0",
            ownerFeatureId = siteId,
            displayName = "Fixture Multi-Cell " + siteKind,
            layoutBindingId = "fixture-" + siteKind + "-kit",
            layoutBindingVersion = "fixture-" + siteKind + "-v1",
            accessScore = 1f,
            accessConstraint = "accepted_layout_access",
            siteKind = siteKind,
            structuralIntent = structuralIntent,
            worldX = worldX,
            worldZ = worldZ,
            footprintRadius = radius,
            minimumExclusionRadius = radius + 18f,
            expansionRadius = radius * 1.35f,
            culturalIntent = "fixture_large_site_contract",
            immutable = true,
            accepted = true,
            provenance = "semantic_authority_fixture",
            ownerCellId = "cell:" + ownerX + ":" + ownerZ,
            memberCellIds = memberCellIds,
            entrances = new List<GeneratedSemanticEntranceRecord>
            {
                new GeneratedSemanticEntranceRecord
                {
                    entranceId = siteId + "|entrance|west",
                    worldX = worldX - radius,
                    worldZ = worldZ,
                    headingDegrees = 270f,
                    permittedRouteId = routeId
                },
                new GeneratedSemanticEntranceRecord
                {
                    entranceId = siteId + "|entrance|east",
                    worldX = worldX + radius,
                    worldZ = worldZ,
                    headingDegrees = 90f,
                    permittedRouteId = string.Empty
                }
            }
        };
    }

    private static bool TryParseCellId(string cellId, out Vector2Int coordinate)
    {
        // note: Decode only the authority's explicit cell identity format; malformed persisted IDs remain test failures rather than guessed coordinates.
        coordinate = default;
        if (string.IsNullOrWhiteSpace(cellId))
            return false;
        string[] parts = cellId.Split(':');
        if (parts.Length != 3 || parts[0] != "cell" ||
            !int.TryParse(parts[1], out int x) ||
            !int.TryParse(parts[2], out int z))
            return false;
        coordinate = new Vector2Int(x, z);
        return true;
    }

    private static string TestWaterGraph()
    {
        GeneratedWorldPlanRecord plan = BuildFixture("semantic-water-511");
        GeneratedSemanticWorldAuthorityRecord authority = YQSemanticWorldAuthority.Ensure(plan);
        if (authority.waterNetworks.Count == 0)
            return "no accepted basin/water record was adapted";
        if (authority.hugePoiSpacingTargetCells != 30 || authority.lazyCandidateBudgetPerQuery != 9 ||
            authority.syntheticFeaturePolicy != "seeded_spacing_geography_access_bounded" || authority.betaDeferredFeatureKinds.Count == 0)
            return "huge-POI pacing and deferred-type policy was not persisted";
        for (int index = 0; index < authority.waterNetworks.Count; index++)
        {
            GeneratedSemanticWaterNetworkRecord water = authority.waterNetworks[index];
            if (water == null || string.IsNullOrWhiteSpace(water.waterId) || string.IsNullOrWhiteSpace(water.basinId) ||
                string.IsNullOrWhiteSpace(water.sourceId) || string.IsNullOrWhiteSpace(water.sinkId) || water.points.Count == 0)
                return "water record lacks basin/source/sink/geometry identity";
            if (!water.declaredSink && string.IsNullOrWhiteSpace(water.downstreamWaterId))
                return "water record has neither downstream link nor declared sink: " + water.waterId;
            // note: Verify a basin record is discoverable from a cell intersecting its persisted geometry.
            GeneratedSemanticRoutePointRecord firstPoint = water.points[0];
            Vector2Int waterCell = new Vector2Int(
                Mathf.FloorToInt((firstPoint.worldX + 512f) / YQSemanticWorldAuthority.CellSizeMeters),
                Mathf.FloorToInt((firstPoint.worldZ + 512f) / YQSemanticWorldAuthority.CellSizeMeters));
            bool waterFoundInCell = false;
            List<GeneratedSemanticWaterNetworkRecord> cellWater = YQGeneratedWorldQuery.GetSemanticWater(plan, waterCell);
            for (int waterIndex = 0; waterIndex < cellWater.Count; waterIndex++)
                waterFoundInCell |= cellWater[waterIndex] != null && cellWater[waterIndex].waterId == water.waterId;
            if (!waterFoundInCell)
                return "water record was not returned by an intersecting cell query: " + water.waterId;
        }
        return string.Empty;
    }

    private static string TestEmptyCellRoutePolicy()
    {
        GeneratedWorldPlanRecord plan = BuildFixture("semantic-empty-902");
        Vector2Int candidate = new Vector2Int(70, -71);
        for (int z = -80; z <= 80; z++)
        for (int x = -80; x <= 80; x++)
        {
            GeneratedSemanticCellPlanRecord cell = YQGeneratedWorldQuery.GetSemanticCellPlan(plan, new Vector2Int(x, z));
            if (cell.siteIds.Count == 0 && cell.routeIds.Count == 0 && cell.waterIds.Count == 0)
            {
                candidate = new Vector2Int(x, z);
                if (cell.routeConstraint != "no_route_synthesis")
                    return "empty cell published a route constraint: " + cell.cellId;
                break;
            }
        }
        GeneratedSemanticChunkRecord record = new GeneratedSemanticChunkRecord { chunkX = candidate.x, chunkZ = candidate.y };
        YQContinuousWorldCellAuthority authority = new YQContinuousWorldCellAuthority(plan.worldSeed, null, null, plan, 128f);
        authority.PopulateCellRecord(record, candidate, 128f, plan);
        for (int index = 0; index < record.edgeContracts.Count; index++)
            if (record.edgeContracts[index].routePortals.Count != 0)
                return "empty cell synthesized a route portal";
        return string.Empty;
    }

    private static string TestAcceptedV2Adapter()
    {
        GeneratedWorldPlanRecord plan = BuildFixture("semantic-v2-318");
        // note: The production V2 compiler consumes the persisted semantic fingerprint prepared by the V1-compatible spatial envelope.
        YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(plan, out GeneratedSpatialWorldPlanV2Record compiled, out string compileFailure))
            return "V2 fixture did not compile: " + compileFailure;
        string artifactBefore = JsonUtility.ToJson(compiled);
        plan.spatialPlanV2 = compiled;
        if (!YQSpatialPlanVersionRouter.TryValidateAcceptedV2(plan, out string validationFailure))
            return "compiled V2 fixture was not accepted: " + validationFailure;
        GeneratedSemanticWorldAuthorityRecord authority = YQSemanticWorldAuthority.Ensure(plan);
        if (authority.siteReservations.Count == 0 || authority.routeGraph.Count == 0 || authority.waterNetworks.Count == 0)
            return "accepted V2 adapter dropped a site, route, or water graph collection";
        // note: Exercise the exact streamed projection used by physical materialization so accepted V2 identities cannot remain trapped in typed semantic lists.
        YQContinuousWorldCellAuthority projectionAuthority =
            new YQContinuousWorldCellAuthority(plan.worldSeed, null, null, plan, 128f);
        bool projectedStructuralCell = false;
        for (int z = -24; z <= 24 && !projectedStructuralCell; z++)
        {
            for (int x = -24; x <= 24 && !projectedStructuralCell; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                GeneratedSemanticCellPlanRecord semanticCell = YQGeneratedWorldQuery.GetSemanticCellPlan(plan, coordinate);
                if (semanticCell == null || (semanticCell.siteIds.Count == 0 && semanticCell.routeIds.Count == 0 && semanticCell.waterIds.Count == 0))
                    continue;
                GeneratedSemanticChunkRecord projected = new GeneratedSemanticChunkRecord { chunkX = x, chunkZ = z };
                projectionAuthority.PopulateCellRecord(projected, coordinate, 128f, plan);
                for (int index = 0; index < semanticCell.siteIds.Count; index++)
                    if (!projected.featureIds.Contains(semanticCell.siteIds[index]))
                        return "accepted V2 site identity was lost during streamed projection: " + semanticCell.siteIds[index];
                for (int index = 0; index < semanticCell.routeIds.Count; index++)
                    if (!projected.featureIds.Contains(semanticCell.routeIds[index]))
                        return "accepted V2 route identity was lost during streamed projection: " + semanticCell.routeIds[index];
                for (int index = 0; index < semanticCell.waterIds.Count; index++)
                    if (!projected.featureIds.Contains(semanticCell.waterIds[index]))
                        return "accepted V2 water identity was lost during streamed projection: " + semanticCell.waterIds[index];
                projectedStructuralCell = true;
            }
        }
        if (!projectedStructuralCell)
            return "accepted V2 fixture exposed no structural cell for streamed projection";
        if (authority.openingEnvelope.Count != 81 || YQGeneratedWorldQuery.GetSemanticNeighborhood(plan, new Vector2Int(-17, 19), 99).Count != 25)
            return "opening envelope or bounded query neighborhood violated its contract";
        // note: Every accepted route crossing must also be represented by the water-owned crossing contract.
        for (int routeIndex = 0; routeIndex < authority.routeGraph.Count; routeIndex++)
        {
            GeneratedSemanticRouteGraphRecord route = authority.routeGraph[routeIndex];
            for (int crossingIndex = 0; crossingIndex < route.crossings.Count; crossingIndex++)
            {
                GeneratedSemanticRouteCrossingRecord crossing = route.crossings[crossingIndex];
                GeneratedSemanticWaterNetworkRecord water = authority.waterNetworks.Find(item => item != null && item.waterId == crossing.waterId);
                if (water == null || water.crossings.Find(item => item != null && item.crossingId == crossing.crossingId) == null)
                    return "accepted route crossing lacks a shared water crossing contract: " + crossing.crossingId;
            }
        }
        if (artifactBefore != JsonUtility.ToJson(plan.spatialPlanV2))
            return "semantic adaptation rewrote the accepted V2 artifact";
        return string.Empty;
    }

    private static GeneratedSemanticBoundaryRecord FindBoundary(GeneratedSemanticCellPlanRecord cell, string edge)
    {
        for (int index = 0; index < cell.edgeContracts.Count; index++)
            if (cell.edgeContracts[index] != null && cell.edgeContracts[index].edge == edge)
                return cell.edgeContracts[index];
        return null;
    }

    private static GeneratedSemanticAcceptedOverrideRecord FindAcceptedOverride(
        GeneratedSemanticWorldAuthorityRecord authority,
        string objectId)
    {
        for (int index = 0; index < authority.acceptedOverrides.Count; index++)
            if (authority.acceptedOverrides[index] != null && authority.acceptedOverrides[index].objectId == objectId)
                return authority.acceptedOverrides[index];
        return null;
    }

    private static GeneratedWorldPlanRecord BuildFixture(string seed)
    {
        GeneratedWorldPlanRecord plan = new GeneratedWorldPlanRecord { worldSeed = seed, summary = "high fantasy river kingdom", designNotes = "semantic authority fixture" };
        plan.EnsureCollections();
        for (int index = 0; index < 3; index++)
            plan.regions.Add(new GeneratedRegionRecord { regionId = "fixture_region_" + index, terrainProfile = index == 1 ? "mountain ridge" : "river forest", climateProfile = index == 2 ? "wet temperate" : "seasonal", gameplayPremise = "trade kingdom", biomeTags = new List<string> { index == 1 ? "highland" : "forest" } });
        for (int index = 0; index < 3; index++)
            plan.settlements.Add(new GeneratedSettlementRecord { settlementId = "fixture_settlement_" + index, regionId = "fixture_region_" + (index % 3), displayName = index == 0 ? "Fixture Gate" : "Fixture Village " + index, runtimeSiteKitId = index == 0 ? "fixture-town-kit" : string.Empty, runtimeSiteBindingVersion = index == 0 ? "fixture-site-bind-v1" : string.Empty, kind = index == 0 ? "trade town" : "village", approxPopulation = 35 + index * 20 });
        for (int index = 0; index < 2; index++)
            plan.encampments.Add(new GeneratedEncampmentRecord { encampmentId = "fixture_hostile_" + index, regionId = "fixture_region_" + index, displayName = "Fixture Hostile " + index, layoutIntent = index == 0 ? "checkpoint_layout" : "cave_layout", kind = index == 1 ? "cave" : "bandit checkpoint", threatTier = index + 2 });
        plan.pointsOfInterest.Add(new GeneratedPointOfInterestRecord { poiId = "fixture_poi_0", regionId = "fixture_region_2", displayName = "Fixture Shrine", visualStyleKey = "shrine_visual_v1", kind = "lakeside shrine", gameplayHook = "exploration" });
        return plan;
    }
}
