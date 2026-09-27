using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQTerrainApproachV2Tests
{
    public static string TestPlanningLimits()
    {
        // note: A reviewed step above soil must target soil once, while invalid clearances reject before sampling.
        var raised = new YQTerrainApproachContractV2 { walkingSurfaceAboveTerrain = .12f };
        int raisedCalls = 0;
        bool Soil(Vector3 point, out float height) { raisedCalls++; height = 0f; return true; }
        if (!YQTerrainApproachV2.TryPlan(Vector3.up * .12f, Vector3.forward, raised, Soil, out var raisedPlan, out _) ||
            raisedPlan.terrainWorkRequired || Mathf.Abs(raisedPlan.start.y) > .0001f)
            return "Reviewed walking/soil height distinction was lost or applied twice.";
        foreach (float invalid in new[] { -.01f, .16f, float.NaN, float.PositiveInfinity })
        {
            raised.walkingSurfaceAboveTerrain = invalid;
            raisedCalls = 0;
            if (YQTerrainApproachV2.TryPlan(Vector3.zero, Vector3.forward, raised, Soil, out _, out _) || raisedCalls != 0)
                return "Invalid walking clearance reached terrain sampling.";
        }
        YQTerrainApproachContractV2 settings = new YQTerrainApproachContractV2();
        int calls = 0;
        bool Flat(Vector3 p, out float y) { calls++; y = 0; return true; }
        if (!YQTerrainApproachV2.TryPlan(Vector3.zero, Vector3.forward, settings, Flat, out var connected, out string failure) || connected.terrainWorkRequired)
            return "Flat connection failed: " + failure;
        if (calls > 219) return "Terrain sampling exceeded its bounded budget.";
        if (!YQTerrainApproachV2.TryPlan(Vector3.zero, Vector3.forward, settings, Flat, out var repeated, out _) || repeated.end != connected.end)
            return "Approach planning was not deterministic.";
        // note: A gently curved, already-connected path must not be classified as missing terrain merely because it is not a straight ramp.
        bool Curved(Vector3 p, out float y) { y = 0.06f * p.z * p.z; return true; }
        if (!YQTerrainApproachV2.TryPlan(Vector3.zero, Vector3.forward, settings, Curved, out var natural, out _) || natural.terrainWorkRequired)
            return "Safe natural ground was unnecessarily scheduled for flattening.";
        // note: A legal transverse slope at the untouched join must be carried into bounded earthwork instead of being rejected as a flat landing mismatch.
        bool LegalCrossSlope(Vector3 p, out float y) { y = p.x * 0.1f; return true; }
        if (!YQTerrainApproachV2.TryPlan(Vector3.zero, Vector3.forward, settings, LegalCrossSlope, out var crossSlope, out string crossFailure) ||
            !crossSlope.terrainWorkRequired)
            return "A legal full-width join slope was rejected: " + crossFailure;
        bool ShallowGap(Vector3 p, out float y) { y = -0.4f; return true; }
        if (!YQTerrainApproachV2.TryPlan(Vector3.zero, Vector3.forward, settings, ShallowGap, out var graded, out _) ||
            !graded.terrainWorkRequired || graded.maximumFill < 0.39f)
            return "A feasible fill plan was confused with constructed ground.";
        bool Cliff(Vector3 p, out float y) { y = -4; return true; }
        if (YQTerrainApproachV2.TryPlan(Vector3.zero, Vector3.forward, settings, Cliff, out _, out _))
            return "Excessive fill or a cliff passed.";
        bool EdgeGap(Vector3 p, out float y) { y = Mathf.Abs(p.x) > 0.3f ? -3f : 0; return true; }
        if (YQTerrainApproachV2.TryPlan(Vector3.zero, Vector3.forward, settings, EdgeGap, out _, out _))
            return "Centre-only support passed a full-width connection.";
        bool Missing(Vector3 p, out float y) { y = 0; return p.z < 1f; }
        if (YQTerrainApproachV2.TryPlan(Vector3.zero, Vector3.forward, settings, Missing, out _, out _))
            return "Missing terrain coverage was treated as ground.";
        bool NonFinite(Vector3 p, out float y) { y = float.NaN; return true; }
        if (YQTerrainApproachV2.TryPlan(Vector3.zero, Vector3.forward, settings, NonFinite, out _, out _))
            return "Nonfinite terrain passed.";
        settings.maximumRun = 100;
        calls = 0;
        if (YQTerrainApproachV2.TryPlan(Vector3.zero, Vector3.forward, settings, Flat, out _, out _) || calls != 0)
            return "Invalid constraints sampled terrain before rejection.";
        return null;
    }

    public static string TestActualTerrainGate()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        TerrainData data = null;
        try
        {
            // note: These are controlled native Unity heightfield tests, not the user's persisted world or an implicit infinite plane.
            data = new TerrainData { heightmapResolution = 33, size = new Vector3(32, 8, 32) };
            float[,] heights = new float[33, 33];
            for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = 0.5f;
            data.SetHeights(0, 0, heights);
            GameObject ground = new GameObject("ReviewTerrain");
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(-16, -4, -16);
            Terrain terrain = ground.AddComponent<Terrain>();
            terrain.terrainData = data;
            if (YQTerrainApproachV2.TrySampleTerrain(null, Vector3.up * 9, out _) ||
                YQTerrainApproachV2.TrySampleTerrain(terrain, new Vector3(17, 0, 0), out _) ||
                !YQTerrainApproachV2.TrySampleTerrain(terrain, Vector3.zero, out float height) || Mathf.Abs(height) > 0.01f)
                return "Missing or out-of-tile terrain supplied false contact evidence.";
            GameObject cell = new GameObject("ReviewCell");
            SceneManager.MoveGameObjectToScene(cell, scene);
            GameObject landing = new GameObject("Landing");
            landing.transform.SetParent(cell.transform, false);
            landing.transform.localPosition = Vector3.down * 0.1f;
            BoxCollider box = landing.AddComponent<BoxCollider>();
            box.size = new Vector3(2, 0.2f, 2);
            YQTerrainApproachContractV2 contract = new YQTerrainApproachContractV2
            {
                reviewState = YQSemanticSiteReviewState.Approved,
                authoredRouteVerified = true,
                supportPath = "Landing",
                localStart = Vector3.zero
            };
            if (!YQTerrainApproachV2.TryValidateReviewedConnection(cell.transform, contract, terrain, out string failure))
                return "A reviewed native-terrain connection failed: " + failure;
            contract.reviewState = YQSemanticSiteReviewState.Pending;
            if (YQTerrainApproachV2.TryValidateReviewedConnection(cell.transform, contract, terrain, out _))
                return "A draft became runtime authority.";
            contract.reviewState = YQSemanticSiteReviewState.Approved;
            box.size = new Vector3(0.8f, 0.2f, 2);
            if (YQTerrainApproachV2.TryValidateReviewedConnection(cell.transform, contract, terrain, out _))
                return "A narrow landing supported an overhanging route width.";
            box.size = new Vector3(2, 0.2f, 2);
            terrain.transform.position += Vector3.down * 0.4f;
            if (YQTerrainApproachV2.TryValidateReviewedConnection(cell.transform, contract, terrain, out _))
                return "Feasible but unbuilt earthworks passed final publication.";
            terrain.transform.position += Vector3.up * 0.4f;
            bool[,] holes = new bool[data.holesResolution, data.holesResolution];
            for (int z = 0; z < holes.GetLength(0); z++) for (int x = 0; x < holes.GetLength(1); x++) holes[z, x] = true;
            holes[16, 16] = false;
            data.SetHoles(0, 0, holes);
            if (YQTerrainApproachV2.TrySampleTerrain(terrain, Vector3.zero, out _) ||
                YQTerrainApproachV2.TryValidateReviewedConnection(cell.transform, contract, terrain, out _))
                return "A terrain hole supplied support evidence.";
            return null;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
    }

    public static void WriteCandidateScenarios()
    {
        const string folder = "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/V2EntranceCandidates/";
        StringBuilder report = new StringBuilder("# V2 terrain-approach constraints\n\n");
        report.AppendLine("Actual candidate landing geometry tested against controlled surface-height scenarios. This is not placement into the saved world, terrain construction, obstacle/water validation or visual approval.");
        report.AppendLine("V2 publication now requires a reviewed authored route and an already-connected, full-width terrain handoff. Feasible cut/fill plans remain unbuilt work and cannot pass that gate.");
        report.AppendLine();
        AppendCandidate(folder + "yq_viking_district_central_village_entrance_candidate.prefab", "SM_House4_Door11", false, report);
        AppendCandidate(folder + "yq_viking_district_eastern_works_entrance_candidate.prefab", "SM_House4_Door6", false, report);
        AppendCandidate(folder + "yq_viking_district_eastern_works_entrance_candidate.prefab", "SM_House2_Door_64", true, report);
        report.AppendLine("Next: explicitly review endpoint sockets and construct or select terrain-compatible stairs/routes at final placement. These measurements grant no candidate or contract approval.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/V2_TerrainApproachReadiness.md", report.ToString());
        Debug.Log("[YQTerrainApproachV2Tests] Candidate scenario report written; active generator and candidate prefabs unchanged.");
    }

    private static void AppendCandidate(string path, string doorPath, bool small, StringBuilder report)
    {
        GameObject cell = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        string supportPath = "YQ_V2_EntranceRepairs/" + doorPath + (small ? "_Threshold" : "_SM_WoodenUpPathway_PathwaySection");
        if (cell == null || !YQCellDoorBindingsV2.TryResolveUniquePath(cell.transform, doorPath, out Transform door) ||
            !YQCellDoorBindingsV2.TryResolveUniquePath(cell.transform, supportPath, out Transform support))
            throw new InvalidOperationException("Missing candidate landing: " + path + " / " + supportPath);
        BoxCollider box = support.GetComponent<BoxCollider>();
        if (box == null) throw new InvalidOperationException("Candidate landing has no box collider.");
        // note: Derive an endpoint from the real landing top, inset from its outer edge; the result is scenario input, not an automatically approved socket.
        Vector3 normal = door.right;
        float far = float.NegativeInfinity;
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = support.TransformPoint(box.center + Vector3.Scale(box.size * 0.5f, new Vector3(x, 1, z)));
            far = Mathf.Max(far, Vector3.Dot(corner - door.position, normal));
        }
        Vector3 top = support.TransformPoint(box.center + Vector3.up * box.size.y * 0.5f);
        Vector3 start = door.position + normal * (far - 0.35f) + door.forward * -0.603f;
        start.y = top.y;
        Vector3 local = support.InverseTransformPoint(start);
        if (Mathf.Abs(local.x - box.center.x) > box.size.x * 0.5f || Mathf.Abs(local.z - box.center.z) > box.size.z * 0.5f)
            throw new InvalidOperationException("Measured scenario endpoint escaped the authored landing.");
        report.AppendLine("## " + doorPath + "\n");
        report.AppendLine("Measured candidate endpoint: " + cell.transform.InverseTransformPoint(start).ToString("F3") + ".");
        YQTerrainApproachContractV2 constraints = new YQTerrainApproachContractV2();
        foreach (float gap in new[] { 0f, 0.4f, 2f })
        {
            bool Surface(Vector3 point, out float y) { y = start.y - gap; return true; }
            bool fits = YQTerrainApproachV2.TryPlan(start, normal, constraints, Surface, out var plan, out string failure);
            report.AppendLine("- Surface " + gap.ToString("F1") + "m below landing: " +
                (fits ? (plan.terrainWorkRequired ? "requires constructed earthworks" : "height constraints fit; not approved") +
                    "; run=" + Vector3.Distance(plan.start, plan.end).ToString("F2") + "m; maximum fill=" + plan.maximumFill.ToString("F2") + "m" : "rejected — " + failure));
        }
        report.AppendLine();
    }

    public static void RunBatch()
    {
        try
        {
            int failures = YQWorldGenerationV2ContractTests.RunTests(out int tested);
            Debug.Log("[YQWorldGenV2Tests] Tested " + tested + " contracts; failures=" + failures + ".");
            if (failures == 0) WriteCandidateScenarios();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
