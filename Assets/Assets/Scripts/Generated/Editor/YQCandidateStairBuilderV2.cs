using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: This bounded first-kit authoring tool changes only the existing unregistered candidate variants, never vendor assets or active world placement.
public static class YQCandidateStairBuilderV2
{
    private const string CandidateFolder = "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/V2EntranceCandidates/";
    private const string VendorFolder = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/";
    private const string ReportPath = "Logs/V2_ConstructedApproaches.md";
    private const string StairName = "SM_WoodenHangStand_Stairs";
    private const string PadName = "SM_StrongholdWallBase_FloorPiece";

    private sealed class Profile
    {
        public GameObject prefab;
        public BoxCollider[] treads;
        public Vector3 highEdge;
        public float lowEdgeX;
    }

    public static void VerifyIndependentHomeApproach()
    {
        // note: Reuse the same full-size stair/terrain probes on the extracted home. A disposable prefab copy prevents test terrain or repair trials from modifying the cell.
        const string path = CandidateFolder + "yq_accessible_house4_cell.prefab";
        var report = new StringBuilder("# Isolated home terrain approach\n\n");
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool changed = false;
            bool ready = ConstructOne(contents, "SM_House4_Door6", MeasureImportedStairs(),
                AssetDatabase.LoadAssetAtPath<GameObject>(VendorFolder + PadName + ".prefab"), report, ref changed);
            if (changed) throw new InvalidOperationException("Isolated home required additional approach geometry; test changes were not saved.");
            if (!ready) throw new InvalidOperationException("Isolated home terrain approach failed; see measured report.");
            report.AppendLine("\nPASS: existing isolated home approach, production-resolution terrain contact, route clearance and protected cut/fill checks. No source edits or runtime approval.");
        }
        catch (Exception error) { report.AppendLine("\nFAILED: " + error.Message); throw; }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQIndependentHomeApproach.txt", report.ToString());
        }
    }

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Construct Candidate Stair Approaches")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before authoring candidate stairs.");
        Profile profile = MeasureImportedStairs();
        GameObject pad = AssetDatabase.LoadAssetAtPath<GameObject>(VendorFolder + PadName + ".prefab");
        if (pad == null || pad.GetComponent<BoxCollider>() == null) throw new InvalidOperationException("Imported lower-landing module is missing.");
        StringBuilder report = new StringBuilder("# Constructed V2 candidate approaches\n\n");
        report.AppendLine("Actual imported stair flights and lower landings, at authored scale. Collision tests include a temporary native Unity terrain tile at each lower landing's explicit contact height. That terrain is a controlled test, not the saved world and is never saved into a candidate.");
        report.AppendLine("No route is approved or activated. Final placement must meet the measured lower contact; a one-flight candidate is not a promise to span every possible terrain elevation.");
        report.AppendLine("Terrain fixtures use the production grid spacing (2m) and height range (140m). Local cut/fill and protected-feature rejection are tested on disposable terrain with explicit synthetic blueprint reservations. This does not verify the saved world's water, neighbors or visual appearance and grants no placement approval.");
        report.AppendLine();
        bool central = BuildCell("yq_viking_district_central_village", new[] { "SM_House4_Door11" }, profile, pad, report);
        bool eastern = BuildCell("yq_viking_district_eastern_works", new[] { "SM_House4_Door6", "SM_House2_Door_64" }, profile, pad, report);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ReportPath, report.ToString());
        if (!central || !eastern) throw new InvalidOperationException("A constructed approach failed; its candidate was not saved. See " + ReportPath);
        Debug.Log("[YQCandidateStairsV2] Constructed approaches verified. " + ReportPath);
    }

    private static Profile MeasureImportedStairs()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VendorFolder + StairName + ".prefab");
        BoxCollider[] treads = prefab != null ? prefab.GetComponents<BoxCollider>() : Array.Empty<BoxCollider>();
        if (treads.Length != 4) throw new InvalidOperationException("Stair source changed: expected four root tread colliders.");
        Array.Sort(treads, (a, b) => a.center.x.CompareTo(b.center.x));
        for (int index = 0; index < treads.Length; index++)
        {
            BoxCollider box = treads[index];
            if (!box.enabled || box.isTrigger || box.size.x < 0.28f || box.size.x > 0.4f ||
                box.size.y <= 0 || box.size.y > 0.1f || box.size.z < 1.7f)
                throw new InvalidOperationException("Stair tread source dimensions changed.");
            if (index > 0)
            {
                float drop = Top(treads[index - 1]).y - Top(box).y;
                if (drop < 0.15f || drop > 0.3f) throw new InvalidOperationException("Imported stair rise is not within reviewed bounds.");
            }
        }
        // note: This source descends along +X. Its four measured tread heights, not renderer bounds or an asset-name guess, define the flight.
        Vector3 high = Top(treads[0]);
        high.x -= treads[0].size.x * 0.5f;
        return new Profile { prefab = prefab, treads = treads, highEdge = high,
            lowEdgeX = treads[3].center.x + treads[3].size.x * 0.5f };
    }

    private static Vector3 Top(BoxCollider box) => box.center + Vector3.up * (box.size.y * 0.5f);

    private static bool BuildCell(string cellId, string[] doors, Profile profile, GameObject pad, StringBuilder report)
    {
        string path = CandidateFolder + cellId + "_entrance_candidate.prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null || PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant)
            throw new InvalidOperationException("Source-linked candidate missing: " + path);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            report.AppendLine("## " + cellId + "\n");
            bool allReady = true, changed = false;
            foreach (string door in doors)
                allReady &= ConstructOne(contents, door, profile, pad, report, ref changed);
            // note: Added outside geometry must not regress any existing reviewed doorway or repaired room.
            YQReviewedSemanticSiteManifest manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(YQSiteFunctionContractReviewV2.RuntimePath);
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(contents))
            {
                int clearDoors = 0;
                foreach (YQReviewedSemanticZoneRecord zone in manifest.Zones)
                {
                    if (zone.stableId != cellId) continue;
                    foreach (YQReviewedCellFunctionContractV2 contract in zone.cellContractsV2)
                        foreach (YQCellDoorBindingV2 binding in contract.doorBindings)
                            if (review.Measure(binding.targetPath).Clear) clearDoors++;
                            else allReady = false;
                }
                foreach (string door in doors)
                {
                    bool small = door == "SM_House2_Door_64";
                    float y = small ? -0.23335f : -0.16f;
                    var room = review.MeasureFloorRegion(door, small ? new Rect(-3.2f, -2, 2.6f, 2.9f) : new Rect(-6.3f, -3.3f, 5.6f, 3.3f),
                        y, new Vector3(small ? -0.6f : -0.7f, y, -0.603f));
                    allReady &= room.Ready;
                    report.AppendLine("- Room retained (`" + door + "`): " + room.Describe());
                }
                report.AppendLine("- Clear doorway probes retained: " + clearDoors + ".");
            }
            if (allReady && changed && PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                throw new InvalidOperationException("Could not save constructed candidate approach.");
            report.AppendLine(allReady ? "Candidate passed. Existing base prefab link and GUID preserved." : "Candidate failed; no changes to this prefab were saved.");
            report.AppendLine();
            return allReady;
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static bool ConstructOne(GameObject cell, string doorPath, Profile profile, GameObject pad,
        StringBuilder report, ref bool changed)
    {
        Transform door = Require(cell.transform, doorPath);
        Transform repairs = Require(cell.transform, "YQ_V2_EntranceRepairs");
        string landingName = doorPath + (doorPath == "SM_House2_Door_64" ? "_Threshold" : "_SM_WoodenUpPathway_PathwaySection");
        Transform upper = Require(repairs, landingName);
        BoxCollider upperBox = upper.GetComponent<BoxCollider>();
        if (upperBox == null) throw new InvalidOperationException("Candidate upper landing has no collider.");
        Vector3 top = upper.TransformPoint(Top(upperBox));
        float farX = float.NegativeInfinity;
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = upper.TransformPoint(upperBox.center + Vector3.Scale(upperBox.size * 0.5f, new Vector3(x, 1, z)));
            farX = Mathf.Max(farX, door.InverseTransformPoint(corner).x);
        }
        float centreZ = door.InverseTransformPoint(top).z;
        float upperFarX = farX;
        string groupName = doorPath + "_StairApproach";
        Transform existing = repairs.Find(groupName);
        // note: Bounded native-module trials avoid authored obstacles. The small-house extension can follow the left side of its existing stone terrace.
        bool smallHouse = doorPath == "SM_House2_Door_64";
        float[] offsets = smallHouse
            ? new[] { -0.603f, centreZ, (centreZ - 0.603f) * 0.5f, -0.603f, -1.9f }
            : new[] { -0.603f, centreZ, (centreZ - 0.603f) * 0.5f, -0.603f };
        for (int trial = 0; trial < (existing != null ? 1 : offsets.Length); trial++)
        {
            float offset = offsets[trial];
            farX = upperFarX;
            Transform group = existing;
            if (group == null)
            {
                group = new GameObject(groupName).transform;
                group.SetParent(repairs, false);
                if (trial >= 3)
                {
                    // note: Extend the raised deck with its real framed walkway module before descending; do not cut stairs through an existing platform or timber support pile.
                    GameObject bridgeSource = AssetDatabase.LoadAssetAtPath<GameObject>(VendorFolder + "SM_WoodenUpPathway_PathwaySection.prefab");
                    BoxCollider bridgeBox = bridgeSource != null ? bridgeSource.GetComponent<BoxCollider>() : null;
                    if (bridgeBox == null) throw new InvalidOperationException("Imported deck extension is missing.");
                    GameObject bridge = (GameObject)PrefabUtility.InstantiatePrefab(bridgeSource, cell.scene);
                    bridge.name = "UpperExtension";
                    bridge.transform.SetParent(group, false);
                    Quaternion rotation = smallHouse ? door.rotation : upper.rotation;
                    Vector3 centre = smallHouse
                        ? door.TransformPoint(new Vector3(farX - 0.1f + bridgeBox.size.x * 0.5f, 0, offset))
                        : top - door.forward * (upperBox.size.x - 0.06f);
                    centre.y = top.y;
                    bridge.transform.SetPositionAndRotation(centre - rotation * Top(bridgeBox), rotation);
                    offset = door.InverseTransformPoint(centre).z;
                    farX = float.NegativeInfinity;
                    for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = bridge.transform.TransformPoint(bridgeBox.center + Vector3.Scale(bridgeBox.size * 0.5f, new Vector3(x, 1, z)));
                        farX = Mathf.Max(farX, door.InverseTransformPoint(corner).x);
                    }
                }
                Vector3 highEdge = door.TransformPoint(new Vector3(farX - 0.04f, 0, offset));
                highEdge.y = top.y;
                GameObject stairs = (GameObject)PrefabUtility.InstantiatePrefab(profile.prefab, cell.scene);
                stairs.name = "ImportedStairs";
                stairs.transform.SetParent(group, false);
                stairs.transform.SetPositionAndRotation(highEdge - door.rotation * profile.highEdge, door.rotation);
                // note: Preserve the imported flight's scale and use a real visible stone lower landing; no collision-only ramp is created.
                BoxCollider padBox = pad.GetComponent<BoxCollider>();
                Vector3 padTop = stairs.transform.TransformPoint(new Vector3(profile.lowEdgeX - 0.02f + padBox.size.x * 0.5f, 0.2f, profile.highEdge.z));
                GameObject lower = (GameObject)PrefabUtility.InstantiatePrefab(pad, cell.scene);
                lower.name = "LowerLanding";
                lower.transform.SetParent(group, false);
                lower.transform.SetPositionAndRotation(padTop - door.rotation * Top(padBox), door.rotation);
                Transform contact = new GameObject("TerrainContact").transform;
                contact.SetParent(group, false);
                contact.SetPositionAndRotation(padTop + door.right * (padBox.size.x * 0.5f - 0.35f), door.rotation);
            }
            Transform flight = Require(group, "ImportedStairs");
            Transform lowerLanding = Require(group, "LowerLanding");
            Transform endpoint = Require(group, "TerrainContact");
            RequirePrefab(flight, StairName);
            RequirePrefab(lowerLanding, PadName);
            Transform extension = group.Find("UpperExtension");
            if (extension != null) RequirePrefab(extension, "SM_WoodenUpPathway_PathwaySection");
            List<Vector3> route = new List<Vector3>();
            Vector3 entry = door.TransformPoint(new Vector3(1.2f, 0, -0.603f));
            entry.y = top.y;
            route.Add(entry);
            Vector3 highCentre = flight.TransformPoint(Top(profile.treads[0]));
            if (smallHouse && extension != null)
            {
                // note: Turn sideways while still on the threshold/stone terrace, then follow the extension; a diagonal shortcut clips its unsupported outside corner.
                Vector3 turn = door.TransformPoint(new Vector3(1.2f, 0, door.InverseTransformPoint(highCentre).z));
                turn.y = top.y;
                route.Add(turn);
            }
            Vector3 alignment = highCentre - door.right * 0.6f;
            alignment.y = top.y;
            route.Add(alignment);
            foreach (BoxCollider tread in profile.treads) route.Add(flight.TransformPoint(Top(tread)));
            Vector3 lowerTop = lowerLanding.TransformPoint(Top(lowerLanding.GetComponent<BoxCollider>()));
            route.Add(lowerTop);
            route.Add(endpoint.position);
            route.Add(endpoint.position + door.right * 1.2f);

            TerrainData data = null;
            GameObject fixture = null;
            bool ready;
            try
            {
                // note: Match production height spacing instead of granting these candidates an unrealistically dense terrain mesh.
                data = new TerrainData { heightmapResolution = 33, size = new Vector3(64, YQGeneratedWorldTerrain.TerrainHeight, 64) };
                float[,] heights = new float[33, 33];
                for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = 0.5f;
                data.SetHeights(0, 0, heights);
                fixture = new GameObject("__TemporaryApproachTerrain");
                fixture.transform.SetParent(cell.transform, false);
                fixture.transform.position = endpoint.position - new Vector3(32, data.size.y * 0.5f, 32);
                Terrain terrain = fixture.AddComponent<Terrain>();
                terrain.terrainData = data;
                fixture.AddComponent<TerrainCollider>().terrainData = data;
                using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(cell))
                {
                    var traversal = review.MeasureSteppedRoute(route, door.forward, 1.2f);
                    ready = traversal.Ready;
                    report.AppendLine("- `" + doorPath + "` flight trial: " + traversal.Describe());
                }
                YQTerrainApproachContractV2 testContract = new YQTerrainApproachContractV2
                {
                    // note: Approval here belongs solely to the synthetic validation input. It is never persisted into the candidate or runtime manifest.
                    reviewState = YQSemanticSiteReviewState.Approved,
                    authoredRouteVerified = true,
                    supportPath = "YQ_V2_EntranceRepairs/" + groupName + "/LowerLanding",
                    localStart = cell.transform.InverseTransformPoint(endpoint.position),
                    localOutward = cell.transform.InverseTransformDirection(door.right)
                };
                bool contactFits = YQTerrainApproachV2.TryValidateReviewedConnection(cell.transform, testContract, terrain, out string failure);
                ready &= contactFits;
                report.AppendLine("  - Matching native-terrain contact: " + contactFits + (contactFits ? "" : "; " + failure));
                fixture.transform.position += Vector3.down * 0.4f;
                if (YQTerrainApproachV2.TryValidateReviewedConnection(cell.transform, testContract, terrain, out _))
                    throw new InvalidOperationException("An unmatched 40cm terrain drop passed final contact validation.");
                if (ready)
                {
                    // note: The baseline fixture explicitly has no external features; separate negative scenarios verify that reservations cannot be bypassed.
                    if (!YQTerrainRepairProtectionV2.TryCreate(new YQSpatialBlueprintV2(), null, Array.Empty<Bounds>(),
                            out var protection, out string protectionFailure)) throw new InvalidOperationException(protectionFailure);
                    // note: A missing-ground case must become actual bounded terrain geometry and pass both the final gate and the complete stair route again.
                    foreach (float gap in new[] { 0.4f, -0.4f })
                    {
                        data.SetHeights(0, 0, heights);
                        fixture.transform.position = endpoint.position - new Vector3(32, data.size.y * 0.5f + gap, 32);
                        string rejectionFailure = YQTerrainApproachConstructionReviewV2.TestProtectionRejectionsOnFixture(terrain, endpoint.position, door.right, testContract);
                        ready &= rejectionFailure == null;
                        report.AppendLine("  - Protected shoulder water / route / neighboring site / foundation: " +
                            (rejectionFailure == null ? "all rejected; heights unchanged" : rejectionFailure));
                        bool graded = YQTerrainApproachConstructionReviewV2.TryGradePreview(terrain, endpoint.position, door.right,
                            testContract, protection, out string gradeFailure);
                        bool joined = graded && YQTerrainApproachV2.TryValidateReviewedConnection(cell.transform, testContract, terrain, out gradeFailure);
                        using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(cell))
                        {
                            var repaired = review.MeasureSteppedRoute(route, door.forward, 1.2f);
                            ready &= joined && repaired.Ready;
                            report.AppendLine("  - Preview " + (gap > 0 ? "fill" : "cut") + " 40cm: constructed=" + graded + "; handoff=" + joined +
                                "; " + repaired.Describe() + (joined ? "" : "; " + gradeFailure));
                        }
                    }
                    report.AppendLine("  - Defined candidate terrain contact: " + testContract.localStart.ToString("F3") + ". Missing 40cm ground correctly rejected.");
                    report.AppendLine("  - Socket: `" + testContract.supportPath + "`; contact marker: `YQ_V2_EntranceRepairs/" + groupName + "/TerrainContact`. Review remains pending.");
                }
            }
            finally
            {
                if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
            }
            if (ready) { changed |= existing == null; return true; }
            if (existing != null) return false;
            // note: Discard only this newly-created unsuccessful trial. Imported assets, other candidate objects and source fences remain intact.
            UnityEngine.Object.DestroyImmediate(group.gameObject);
        }
        return false;
    }

    private static Transform Require(Transform parent, string path)
    {
        if (!YQCellDoorBindingsV2.TryResolveUniquePath(parent, path, out Transform found))
            throw new InvalidOperationException("Missing or ambiguous approach geometry: " + path);
        return found;
    }

    private static void RequirePrefab(Transform item, string name)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(item.gameObject);
        if (source == null || AssetDatabase.GetAssetPath(source) != VendorFolder + name + ".prefab" ||
            (item.localScale - Vector3.one).sqrMagnitude > 0.0001f)
            throw new InvalidOperationException("Approach asset identity or authored scale changed: " + item.name);
    }

    public static void RunBatch()
    {
        try
        {
            int failures = YQWorldGenerationV2ContractTests.RunTests(out int tested);
            Debug.Log("[YQWorldGenV2Tests] Tested " + tested + " contracts; failures=" + failures + ".");
            if (failures == 0) Build();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
