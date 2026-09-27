using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// note: First-kit authoring recipes produce reusable, source-linked cell content; generated worlds still choose semantic cells through the existing runtime compiler.
public static class YQCandidateInteriorBuilderV2
{
    private const string Folder = "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/V2EntranceCandidates";
    private const string ZoneId = "yq_viking_district_eastern_works";
    private const string PrefabPath = Folder + "/" + ZoneId + "_entrance_candidate.prefab";
    private const string ManifestPath = Folder + "/YQ_FurnishedEasternCandidate.asset";
    private const string FurnitureRoot = "YQ_V2_InteriorFurnishings";
    private const string SocketRoot = "YQ_V2_InteriorSockets";
    private const string DoorPath = "SM_House4_Door6";
    private const string Viking = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/";
    private const string ReportPath = "Logs/V2_CandidateInteriorReadiness.md";

    private static System.Collections.IEnumerator accessibilityRepair;
    // note: The editor request runner must not inspect/save the same candidate while a yielded repair still owns its preview contents.
    public static bool AccessibilityRepairRunning => accessibilityRepair != null;

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Review Independent Home")]
    public static void VerifyAndReviewIndependentHome()
    {
        // note: One editor invocation compiles once, runs the pending production-boundary regressions, then captures the candidate for visual inspection.
        YQProceduralSettlementLayoutVerification.VerifySettlementRepairBoundaries();
        YQProceduralSettlementLayoutVerification.VerifyAssemblyDatumAndSaveVoice();
        ReviewIndependentHome();
    }

    public static void ReviewIndependentHome()
    {
        // note: Preserve the original candidate review while allowing the street-connected production candidate to use the same renderer.
        RenderHomeReview(Folder + "/yq_accessible_house4_cell.prefab", "Logs/IndependentHomeReview", "Logs/YQIndependentHomeReview.txt");
    }

    public static void ReviewStreetConnectedHome()
    {
        // note: Review the exact prefab whose current functional contracts are measured, not an earlier extraction variant.
        VerifyHomeFunctionalAccess();
        RenderHomeReview(Folder + "/yq_street_connected_house4.prefab", "Logs/StreetConnectedHomeReview", "Logs/YQStreetConnectedHomeReview.txt");
    }

    public static void BuildStoneFoundationCandidate()
    {
        BuildFoundationCandidate(false);
    }

    public static void BuildTimberFoundationCandidate()
    {
        // note: Use complete braced frames with their native vertical proportions; buried posts meet the terrain instead of flattened scaffold geometry.
        BuildFoundationCandidate(true);
    }

    private static void BuildFoundationCandidate(bool timber)
    {
        // note: Preserve the reviewed source and its signature; this new candidate adds same-kit stone support beneath the existing floor.
        string output = Folder + (timber ? "/yq_timber_foundation_house4.prefab" : "/yq_stone_foundation_house4.prefab");
        if (File.Exists(output)) throw new InvalidOperationException("Foundation candidate already exists; review it rather than overwrite authored work.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/yq_street_connected_house4.prefab");
        var stone = AssetDatabase.LoadAssetAtPath<GameObject>(Viking + (timber ? "SM_WoodenWatertank_Foundation.prefab" : "SM_StoneWall_PS5.prefab"));
        if (source == null || stone == null) throw new InvalidOperationException("Required source home or stone support is unavailable.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            Transform contact = RequirePath(root.transform, "YQ_V2_EntranceRepairs/SM_House4_Door6_StairApproach/TerrainContact");
            Bounds floor = default;
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // note: Only the two explicit ground-floor meshes own this foundation; stairs, furnishings and upper geometry do not enlarge it.
                if (renderer.name != "SM_House1_Floor_LOD0") continue;
                if (!found) { floor = renderer.bounds; found = true; }
                else floor.Encapsulate(renderer.bounds);
            }
            if (!found) throw new InvalidOperationException("Ground-floor geometry was not found.");
            float bottom = contact.position.y - .12f;
            float top = floor.min.y + .03f;
            if (top <= bottom || top - bottom > 1.5f) throw new InvalidOperationException("Foundation span requires a different construction recipe.");
            var supportRoot = new GameObject(timber ? "YQ_TimberFoundation" : "YQ_StoneFoundation").transform;
            supportRoot.SetParent(root.transform, false);
            // note: Four modestly fitted source pieces support the entire floor envelope, with small overlaps to avoid seams between stones.
            for (int z = 0; z < 2; z++)
                for (int x = 0; x < 2; x++)
                {
                    var piece = (GameObject)PrefabUtility.InstantiatePrefab(stone, scene);
                    piece.transform.SetParent(supportRoot, false);
                    Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) throw new InvalidOperationException("Stone source has no renderers.");
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Vector3 size = new Vector3(floor.size.x * .5f + .12f, top - bottom, floor.size.z * .5f + .12f);
                    // note: Timber posts extend below the ground datum at full height; only horizontal frame spacing is fitted to the floor.
                    if (timber) size.y = bounds.size.y;
                    piece.transform.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
                    bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Vector3 center = new Vector3(floor.min.x + floor.size.x * (.25f + .5f * x), timber ? top - size.y * .5f : (top + bottom) * .5f,
                        floor.min.z + floor.size.z * (.25f + .5f * z));
                    piece.transform.position += center - bounds.center;
                }
            if (PrefabUtility.SaveAsPrefabAsset(root, output) == null) throw new InvalidOperationException("Foundation candidate save failed.");
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        // note: Render the new geometry for inspection; creating it does not approve contracts or publish it to generated worlds.
        RenderHomeReview(output, timber ? "Logs/TimberFoundationHomeReview" : "Logs/StoneFoundationHomeReview",
            timber ? "Logs/YQTimberFoundationHomeReview.txt" : "Logs/YQStoneFoundationHomeReview.txt");
    }

    public static void VerifyStoneFoundationCoverage()
    {
        VerifyFoundationCoverage(false);
    }

    public static void VerifyTimberFoundationCoverage()
    {
        // note: Measure the frame's actual floor contacts; a beam foundation is not expected to cover every floor point like a slab.
        VerifyFoundationCoverage(true);
    }

    private static void VerifyFoundationCoverage(bool timber)
    {
        // note: Ray tests measure actual stone collision under the floor, not the fitted renderer envelopes that can hide holes in irregular assets.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + (timber ? "/yq_timber_foundation_house4.prefab" : "/yq_stone_foundation_house4.prefab"));
        if (prefab == null) throw new InvalidOperationException("Stone foundation candidate is missing.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var report = new StringBuilder(timber ? "# Timber foundation collision coverage\n\n" : "# Stone foundation collision coverage\n\n");
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Transform support = RequirePath(root.transform, timber ? "YQ_TimberFoundation" : "YQ_StoneFoundation");
            Transform contact = RequirePath(root.transform, "YQ_V2_EntranceRepairs/SM_House4_Door6_StairApproach/TerrainContact");
            Collider[] colliders = support.GetComponentsInChildren<Collider>(true);
            // note: Imported broad boxes can span empty frame space; compare them with transient exact LOD0 mesh probes without modifying source assets.
            var visibleColliders = new List<Collider>();
            foreach (MeshFilter filter in support.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.name.EndsWith("LOD0", StringComparison.Ordinal)) continue;
                var probeObject = new GameObject("VisibleFoundationProbe");
                probeObject.transform.SetParent(filter.transform, false);
                var probe = probeObject.AddComponent<MeshCollider>();
                probe.sharedMesh = filter.sharedMesh;
                visibleColliders.Add(probe);
            }
            Bounds floor = default;
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "SM_House1_Floor_LOD0")
                {
                    if (!found) { floor = renderer.bounds; found = true; }
                    else floor.Encapsulate(renderer.bounds);
                }
            if (!found) throw new InvalidOperationException("Missing ground floor.");
            Physics.SyncTransforms();
            int tested = 0, supported = 0, visibleSupported = 0;
            // note: Preserve spatial contact distribution; area totals alone cannot distinguish regular beam spans from a wholly unsupported edge.
            var contactMap = new StringBuilder("\nExact mesh contact map (0.4m grid, X across/Z down; # contact, . gap):\n```\n");
            for (float z = floor.min.z + .2f; z < floor.max.z; z += .4f)
            {
                for (float x = floor.min.x + .2f; x < floor.max.x; x += .4f)
                {
                    tested++;
                    float highest = float.NegativeInfinity;
                    var ray = new Ray(new Vector3(x, floor.min.y + .1f, z), Vector3.down);
                    foreach (Collider collider in colliders)
                        if (collider.enabled && !collider.isTrigger && collider.Raycast(ray, out RaycastHit hit, 2f))
                            highest = Mathf.Max(highest, hit.point.y);
                    if (highest >= floor.min.y - .08f) supported++;
                    else if (tested <= 30) report.AppendLine("Unsupported floor sample: " + x.ToString("F2") + ", " + z.ToString("F2"));
                    // note: Use the identical ray and tolerance for visual geometry so collision approximation cannot pass as beam contact evidence.
                    float meshHighest = float.NegativeInfinity;
                    foreach (Collider collider in visibleColliders)
                        if (collider.Raycast(ray, out RaycastHit meshHit, 2f)) meshHighest = Mathf.Max(meshHighest, meshHit.point.y);
                    if (meshHighest >= floor.min.y - .08f) visibleSupported++;
                    contactMap.Append(meshHighest >= floor.min.y - .08f ? '#' : '.');
                }
                contactMap.AppendLine();
            }
            contactMap.AppendLine("```");
            report.AppendLine("Source signature: " + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(prefab)));
            report.AppendLine("Foundation colliders: " + colliders.Length + "\nFloor contacts: " + supported + "/" + tested +
                "\nLanding datum: " + contact.position.y.ToString("F4"));
            report.AppendLine("Exact LOD0 mesh contacts: " + visibleSupported + "/" + tested + "; mesh probes=" + visibleColliders.Count);
            report.Append(contactMap);
            report.AppendLine(timber ? "Contact measurements only: beam spans and post-to-soil support require separate evidence. Not approved." : supported == tested && tested > 0 ?
                "Full sampled contact coverage. Structural and gameplay review still required." :
                "REJECTED as continuous foundation: actual geometry does not support the sampled floor footprint. Do not publish.");
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(timber ? "Logs/YQTimberFoundationCoverage.md" : "Logs/YQStoneFoundationCoverage.md", report.ToString());
        }
    }

    public static void ReviewFoundationSources()
    {
        // note: Inspect structural source geometry in isolation before another house-fitting recipe is attempted.
        foreach (string name in new[] { "SM_WoodenWatertank_Foundation", "SM_StrongholdWallBase", "SM_WoodenMiniWatchtower_Base" })
            RenderHomeReview(Viking + name + ".prefab", "Logs/FoundationSources/" + name,
                "Logs/FoundationSources/" + name + ".txt", false);
    }

    public static void ReviewHouseFloorSource()
    {
        // note: Inspect the existing floor's beam direction and underside before changing supporting frame placement.
        RenderHomeReview(Viking + "SM_House1_Floor.prefab", "Logs/HouseFloorSource",
            "Logs/HouseFloorSource.txt", false);
        // note: The underside view exposes any authored joists hidden by the planks in the top review.
        RenderHomeReview(Viking + "SM_House1_Floor.prefab", "Logs/HouseFloorUnderside",
            "Logs/HouseFloorUnderside.txt", false, false, 0f, -.6f);
    }

    public static void ReviewStructuralPosts()
    {
        // note: Review the kit's dedicated structural assets before using repurposed furniture frames as building supports.
        foreach (string name in new[] { "SM_Structure_ShadowPost", "SM_Structures_FlatPost" })
            RenderHomeReview(Viking + name + ".prefab", "Logs/StructuralSources/" + name,
                "Logs/StructuralSources/" + name + ".txt", false);
    }

    public static void ReviewIndividualTimber()
    {
        // note: Inspect a small representative sample of the kit's reusable timber pieces before authoring floor joists.
        foreach (string name in new[] { "SM_WoodSet_Trunks1", "SM_WoodSet_Planks1", "SM_WoodSet2_1" })
            RenderHomeReview(Viking + name + ".prefab", "Logs/StructuralSources/" + name,
                "Logs/StructuralSources/" + name + ".txt", false);
    }

    public static void BuildJoistedHomeCandidate()
    {
        // note: Build from the intact house, preserving the experimental water-tank candidate and all original prefab links.
        string output = Folder + "/yq_joisted_house4.prefab";
        if (File.Exists(output)) throw new InvalidOperationException("Joisted candidate already exists.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/yq_street_connected_house4.prefab");
        var pole = AssetDatabase.LoadAssetAtPath<GameObject>(Viking + "SM_WoodSet_Trunks1.prefab");
        if (source == null || pole == null) throw new InvalidOperationException("Missing reviewed source geometry.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            Bounds floor = default;
            bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "SM_House1_Floor_LOD0")
                {
                    if (!found) { floor = renderer.bounds; found = true; }
                    else floor.Encapsulate(renderer.bounds);
                }
            if (!found) throw new InvalidOperationException("Missing floor.");
            var support = new GameObject("YQ_JoistedFoundation").transform;
            support.SetParent(root.transform, false);
            // note: Reuse native timber cross-sections; fit only length along each member and align mesh bounds to its two endpoints.
            Action<string, Vector3, Vector3> member = (name, a, b) =>
            {
                var piece = (GameObject)PrefabUtility.InstantiatePrefab(pole, scene);
                piece.name = name;
                piece.transform.SetParent(support, false);
                var mesh = piece.GetComponentInChildren<MeshFilter>().sharedMesh;
                Bounds local = mesh.bounds;
                piece.transform.localScale = new Vector3(1f, Vector3.Distance(a, b) / local.size.y, 1f);
                piece.transform.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                piece.transform.position = (a + b) * .5f - piece.transform.TransformVector(local.center);
            };
            float joistY = floor.min.y - .13f;
            float beamY = joistY - .26f;
            float soilY = RequirePath(root.transform, "YQ_V2_EntranceRepairs/SM_House4_Door6_StairApproach/TerrainContact").position.y - .12f;
            int count = Mathf.CeilToInt(floor.size.z / .6f);
            for (int i = 0; i <= count; i++)
            {
                float z = Mathf.Lerp(floor.min.z + .12f, floor.max.z - .12f, i / (float)count);
                // note: Split long joists over the centre bearer so source lengths remain close to the original four-metre pole.
                for (int half = 0; half < 2; half++)
                    member("Joist_" + i + "_" + half,
                        new Vector3(half == 0 ? floor.min.x : floor.center.x - .1f, joistY, z),
                        new Vector3(half == 0 ? floor.center.x + .1f : floor.max.x, joistY, z));
            }
            for (int row = 0; row < 3; row++)
            {
                float x = Mathf.Lerp(floor.min.x + .2f, floor.max.x - .2f, row / 2f);
                for (int segment = 0; segment < 2; segment++)
                    member("Bearer_" + row + "_" + segment,
                        new Vector3(x, beamY, segment == 0 ? floor.min.z : floor.center.z - .1f),
                        new Vector3(x, beamY, segment == 0 ? floor.center.z + .1f : floor.max.z));
                for (int post = 0; post < 3; post++)
                {
                    float z = Mathf.Lerp(floor.min.z + .2f, floor.max.z - .2f, post / 2f);
                    member("Post_" + row + "_" + post, new Vector3(x, soilY - .3f, z), new Vector3(x, beamY, z));
                }
            }
            if (PrefabUtility.SaveAsPrefabAsset(root, output) == null) throw new InvalidOperationException("Candidate save failed.");
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        RenderHomeReview(output, "Logs/JoistedHomeReview", "Logs/YQJoistedHomeReview.txt", true, true, .12f);
    }

    public static void FitJoistedFrameToFloor()
    {
        // note: Fit the existing unpublished frame to actual plank undersides, preserving all relative frame joints and source prefab links.
        string path = Folder + "/yq_joisted_house4.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var probes = new List<GameObject>();
        try
        {
            var support = RequirePath(root.transform, "YQ_JoistedFoundation");
            var floors = new List<MeshCollider>();
            var joists = new List<MeshCollider>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                bool floor = filter.name == "SM_House1_Floor_LOD0";
                bool joist = filter.name.EndsWith("LOD0", StringComparison.Ordinal) &&
                    filter.transform.IsChildOf(support) && filter.transform.parent.name.StartsWith("Joist_", StringComparison.Ordinal);
                if (!floor && !joist) continue;
                var probe = new GameObject("TemporaryFloorFitProbe");
                probes.Add(probe);
                probe.transform.SetParent(filter.transform, false);
                var collider = probe.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                if (floor) floors.Add(collider); else joists.Add(collider);
            }
            Physics.SyncTransforms();
            float lift = 0f;
            int samples = 0;
            foreach (var joist in joists)
            {
                Bounds beam = joist.bounds;
                for (float x = beam.min.x + .2f; x < beam.max.x - .2f; x += .4f)
                {
                    float underside = float.PositiveInfinity;
                    foreach (var floor in floors)
                        if (floor.Raycast(new Ray(new Vector3(x, beam.min.y - .2f, beam.center.z), Vector3.up), out var hit, 1f))
                            underside = Mathf.Min(underside, hit.point.y);
                    if (float.IsPositiveInfinity(underside)) continue;
                    if (!joist.Raycast(new Ray(new Vector3(x, beam.max.y + .2f, beam.center.z), Vector3.down), out var top, 1f))
                        throw new InvalidOperationException("Missing joist surface during floor fitting.");
                    samples++;
                    lift = Mathf.Max(lift, underside - top.point.y);
                }
            }
            // note: A large adjustment indicates incompatible geometry; never consume the posts' soil embedding to hide that failure.
            if (samples == 0 || lift > .1f) throw new InvalidOperationException("Frame cannot be fitted within its support allowance.");
            support.position += Vector3.up * lift;
            foreach (var probe in probes) UnityEngine.Object.DestroyImmediate(probe);
            probes.Clear();
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("Frame fit save failed.");
            File.WriteAllText("Logs/YQJoistedFrameFit.txt", "Measured samples=" + samples + "; frame lift=" + lift + "m. Manifest remains pending/stale until requalification.");
        }
        finally
        {
            foreach (var probe in probes) if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
            PrefabUtility.UnloadPrefabContents(root);
        }
        // note: Reject the repaired candidate if either its exact support contacts or retained functional clearances fail.
        VerifyJoistedFrameContacts();
        VerifyJoistedHomeAccess();
    }

    public static void VerifyJoistedFrameContacts()
    {
        // note: Probe the actual LOD0 mesh at structural crossings; broad imported colliders cannot certify joints.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/yq_joisted_house4.prefab");
        if (prefab == null) throw new InvalidOperationException("Missing joisted candidate.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var report = new StringBuilder("# Joisted frame mesh joints\n\n");
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var support = RequirePath(root.transform, "YQ_JoistedFoundation");
            var members = new List<KeyValuePair<string, MeshCollider>>();
            foreach (Transform member in support)
            {
                foreach (var filter in member.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.name.EndsWith("LOD0", StringComparison.Ordinal)) continue;
                    var probe = new GameObject("ExactJointProbe");
                    probe.transform.SetParent(filter.transform, false);
                    var collider = probe.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    members.Add(new KeyValuePair<string, MeshCollider>(member.name, collider));
                    break;
                }
            }
            // note: Separate exact floor meshes from imported collision boxes when measuring the underside-to-joist joint.
            var floors = new List<MeshCollider>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name == "SM_House1_Floor_LOD0")
                {
                    var probe = new GameObject("ExactFloorProbe");
                    probe.transform.SetParent(filter.transform, false);
                    var collider = probe.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    floors.Add(collider);
                }
            Physics.SyncTransforms();
            int joints = 0, gaps = 0, soilFailures = 0, floorSamples = 0, floorGaps = 0, insufficientContacts = 0;
            float soil = RequirePath(root.transform, "YQ_V2_EntranceRepairs/SM_House4_Door6_StairApproach/TerrainContact").position.y - .12f;
            foreach (var upper in members)
            {
                if (upper.Key.StartsWith("Joist_", StringComparison.Ordinal))
                {
                    Bounds beam = upper.Value.bounds;
                    for (float x = beam.min.x + .2f; x < beam.max.x - .2f; x += .4f)
                    {
                        // note: Probe along each joist centreline; unsupported spaces between joists are intentional floor spans.
                        var upRay = new Ray(new Vector3(x, beam.min.y - .2f, beam.center.z), Vector3.up);
                        float underside = float.PositiveInfinity;
                        foreach (var floor in floors)
                            if (floor.Raycast(upRay, out var hit, 1f)) underside = Mathf.Min(underside, hit.point.y);
                        if (float.IsPositiveInfinity(underside)) continue;
                        floorSamples++;
                        if (!upper.Value.Raycast(new Ray(new Vector3(x, beam.max.y + .2f, beam.center.z), Vector3.down), out var beamHit, 1f) ||
                            underside - beamHit.point.y > .03f)
                        {
                            floorGaps++;
                            report.AppendLine("FLOOR GAP " + upper.Key + " x=" + x + " gap=" + (underside - beamHit.point.y));
                        }
                    }
                }
                if (upper.Key.StartsWith("Post_", StringComparison.Ordinal))
                {
                    // note: Only verifies embedding into the flat reviewed datum, not arbitrary sloping terrain.
                    if (upper.Value.bounds.min.y > soil || upper.Value.bounds.max.y <= soil) soilFailures++;
                    continue;
                }
                int contacts = 0;
                foreach (var lower in members)
                {
                    bool pair = upper.Key.StartsWith("Joist_", StringComparison.Ordinal) && lower.Key.StartsWith("Bearer_", StringComparison.Ordinal) ||
                        upper.Key.StartsWith("Bearer_", StringComparison.Ordinal) && lower.Key.StartsWith("Post_", StringComparison.Ordinal);
                    if (!pair) continue;
                    Bounds a = upper.Value.bounds, b = lower.Value.bounds;
                    float minX = Mathf.Max(a.min.x, b.min.x), maxX = Mathf.Min(a.max.x, b.max.x);
                    float minZ = Mathf.Max(a.min.z, b.min.z), maxZ = Mathf.Min(a.max.z, b.max.z);
                    if (maxX - minX < .02f || maxZ - minZ < .02f) continue;
                    float x = (minX + maxX) * .5f, z = (minZ + maxZ) * .5f;
                    bool underside = upper.Value.Raycast(new Ray(new Vector3(x, a.min.y - .2f, z), Vector3.up), out var topHit, a.size.y + .4f);
                    bool top = lower.Value.Raycast(new Ray(new Vector3(x, b.max.y + .2f, z), Vector3.down), out var bottomHit, b.size.y + .4f);
                    float gap = underside && top ? topHit.point.y - bottomHit.point.y : float.PositiveInfinity;
                    joints++;
                    if (gap > .03f) { gaps++; report.AppendLine("GAP " + upper.Key + " -> " + lower.Key + ": " + gap); }
                    else contacts++;
                }
                if (contacts < 2)
                {
                    insufficientContacts++;
                    report.AppendLine("INSUFFICIENT CONTACTS " + upper.Key + ": " + contacts);
                }
            }
            report.AppendLine("Source signature: " + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(prefab)));
            report.AppendLine("Members=" + members.Count + "; tested joints=" + joints + "; gaps=" + gaps + "; post/soil failures=" + soilFailures);
            report.AppendLine("Floor centreline samples=" + floorSamples + "; floor/joist gaps=" + floorGaps);
            // note: Missing probes and measured gaps must fail batch acceptance rather than returning a successful process result.
            bool passed = members.Count > 0 && floors.Count > 0 && joints > 0 && floorSamples > 0 &&
                gaps == 0 && soilFailures == 0 && floorGaps == 0 && insufficientContacts == 0;
            report.AppendLine(passed ? "PASS: sampled mesh contacts only." : "FAIL: missing or insufficient mesh contacts.");
            report.AppendLine("Bracing, unsampled surfaces, terrain variation and publication remain separate.");
            if (!passed) throw new InvalidOperationException("Joisted frame contact verification failed; see Logs/YQJoistedFrameContacts.md.");
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllText("Logs/YQJoistedFrameContacts.md", report.ToString());
        }
    }

    public static void VerifyTimberTerrainIntegration()
    {
        // note: Keep the original candidate regression available through the same terrain fixture.
        VerifyHomeTerrainIntegration(false);
    }

    public static void VerifyJoistedTerrainIntegration()
    {
        // note: Exercise repaired geometry without transferring approval to its stale production manifest.
        VerifyHomeTerrainIntegration(true);
    }

    private static void VerifyHomeTerrainIntegration(bool joisted)
    {
        // note: Exercise actual runtime grounding against real TerrainData without publishing or mutating candidate approvals.
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + "/YQ_TimberHomeCandidate.asset");
        if (manifest == null || manifest.Zones.Count != 1) throw new InvalidOperationException("Missing timber candidate.");
        var zone = manifest.Zones[0];
        if (manifest.SourceSignature != AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(zone.prefab)).ToString())
            throw new InvalidOperationException("Stale timber candidate.");
        var candidate = joisted ? AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/yq_joisted_house4.prefab") : zone.prefab;
        if (candidate == null) throw new InvalidOperationException("Missing terrain review candidate.");
        string reportPath = joisted ? "Logs/YQJoistedTerrainIntegration.md" : "Logs/YQTimberTerrainIntegration.md";
        var contract = JsonUtility.FromJson<YQReviewedCellFunctionContractV2>(JsonUtility.ToJson(zone.cellContractsV2[0]));
        foreach (var door in contract.doorBindings)
        {
            // note: Approval is only on a detached test record so the real runtime gate can be exercised.
            door.reviewState = YQSemanticSiteReviewState.Approved;
            door.terrainApproach.reviewState = YQSemanticSiteReviewState.Approved;
        }
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        TerrainData data = null;
        var report = new StringBuilder("# Home runtime terrain integration\n\n");
        report.AppendLine("Source signature: " + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(candidate)));
        try
        {
            data = new TerrainData { heightmapResolution = 129, size = new Vector3(128f, 20f, 128f) };
            var ground = Terrain.CreateTerrainGameObject(data);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(-64f, 5f, -64f);
            var terrain = ground.GetComponent<Terrain>();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(candidate, scene);
            int floatingPosts = 0;
            int foundationRejections = 0;
            foreach (float slope in new[] { 0f, .03f, -.03f })
            {
                // note: Vary the real heightfield independently of the assembly heading and elevation.
                var heights = new float[129, 129];
                for (int z = 0; z < 129; z++)
                    for (int x = 0; x < 129; x++) heights[z, x] = (4f + slope * (x - 64f)) / 20f;
                data.SetHeights(0, 0, heights);
                foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                {
                    root.transform.SetPositionAndRotation(new Vector3(0f, 5f, 0f), Quaternion.Euler(0f, yaw, 0f));
                    // note: The editor harness invokes the internal runtime boundary without widening its production API.
                    var grounding = typeof(YQCompiledWorldSiteInstance).GetMethod("TryResolveReviewedLandingDelta",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    var arguments = new object[] { root.transform, terrain, contract, 0f };
                    if (grounding == null || !(bool)grounding.Invoke(null, arguments))
                        throw new InvalidOperationException("Runtime landing grounding rejected slope=" + slope + " yaw=" + yaw);
                    float delta = (float)arguments[3];
                    root.transform.position += Vector3.up * delta;
                    Physics.SyncTransforms();
                    var approach = contract.doorBindings[0].terrainApproach;
                    Vector3 walking = root.transform.TransformPoint(approach.localStart);
                    float soil = terrain.SampleHeight(walking) + terrain.transform.position.y;
                    if (Mathf.Abs(walking.y - soil - approach.walkingSurfaceAboveTerrain) > .002f)
                        throw new InvalidOperationException("Runtime soil/walking datum mismatch.");
                    if (!YQTerrainApproachV2.TryValidateReviewedConnection(root.transform, approach, terrain, out string failure))
                        throw new InvalidOperationException("Runtime terrain connection rejected slope=" + slope + " yaw=" + yaw + ": " + failure);
                    report.AppendLine("PASS slope=" + slope + " yaw=" + yaw + " correction=" + delta);
                    if (joisted)
                    {
                        int caseFloating = 0;
                        // note: Exercise serialized support locations and allowances, changing approval only on a detached test copy.
                        var saved = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + "/YQ_JoistedHomeCandidate.asset");
                        if (saved == null || saved.SourceSignature != AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(candidate)).ToString())
                            throw new InvalidOperationException("Missing or stale saved foundation proposal.");
                        var reviewed = JsonUtility.FromJson<YQReviewedCellFunctionContractV2>(JsonUtility.ToJson(saved.Zones[0].cellContractsV2[0]));
                        reviewed.independentAssembly.reviewState = YQSemanticSiteReviewState.Approved;
                        reviewed.independentAssembly.foundationVerified = true;
                        foreach (var door in reviewed.doorBindings)
                        {
                            door.reviewState = YQSemanticSiteReviewState.Approved;
                            door.terrainApproach.reviewState = YQSemanticSiteReviewState.Approved;
                        }
                        // note: An entrance datum alone cannot prove the distant foundation touches the same sloping terrain.
                        foreach (Transform post in RequirePath(root.transform, "YQ_JoistedFoundation"))
                        {
                            if (!post.name.StartsWith("Post_", StringComparison.Ordinal)) continue;
                            foreach (var renderer in post.GetComponentsInChildren<Renderer>(true))
                            {
                                if (!renderer.name.EndsWith("LOD0", StringComparison.Ordinal)) continue;
                                Bounds bounds = renderer.bounds;
                                float postSoil = terrain.SampleHeight(bounds.center) + terrain.transform.position.y;
                                if (bounds.min.y > postSoil + .03f)
                                {
                                    floatingPosts++;
                                    caseFloating++;
                                    report.AppendLine("FLOATING POST slope=" + slope + " yaw=" + yaw + " member=" + post.name + " gap=" + (bounds.min.y - postSoil));
                                }
                            }
                        }
                        var foundationArguments = new object[] { root.transform, terrain, reviewed, 0f };
                        bool accepted = (bool)grounding.Invoke(null, foundationArguments);
                        if (accepted == (caseFloating > 0)) throw new InvalidOperationException("Runtime foundation acceptance differs from measured support.");
                        if (!accepted) foundationRejections++;
                        // note: Broken source identity must reject even when the visible placement is otherwise supported.
                        reviewed.independentAssembly.sourceSignature = "stale-fixture";
                        if ((bool)grounding.Invoke(null, new object[] { root.transform, terrain, reviewed, 0f }))
                            throw new InvalidOperationException("Stale foundation signature accepted.");
                    }
                }
            }
            // note: Missing ground must reject before any rigid correction, including holes inside an otherwise valid tile.
            var probe = typeof(YQCompiledWorldSiteInstance).GetMethod("TryResolveReviewedLandingDelta",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            root.transform.SetPositionAndRotation(new Vector3(0f, 5f, 0f), Quaternion.identity);
            var contact = root.transform.TransformPoint(contract.doorBindings[0].terrainApproach.localStart);
            int hx = Mathf.FloorToInt((contact.x - ground.transform.position.x) / data.size.x * data.holesResolution);
            int hz = Mathf.FloorToInt((contact.z - ground.transform.position.z) / data.size.z * data.holesResolution);
            data.SetHoles(hx, hz, new bool[,] { { false } });
            if ((bool)probe.Invoke(null, new object[] { root.transform, terrain, contract, 0f }))
                throw new InvalidOperationException("Grounding accepted a terrain hole.");
            data.SetHoles(hx, hz, new bool[,] { { true } });
            ground.transform.localScale = new Vector3(2f, 1f, 1f);
            if ((bool)probe.Invoke(null, new object[] { root.transform, terrain, contract, 0f }))
                throw new InvalidOperationException("Grounding accepted unsupported terrain scale.");
            ground.transform.localScale = Vector3.one;
            report.AppendLine("PASS terrain hole and unsupported scale rejected before placement.");
            if (joisted) report.AppendLine("PASS foundation gate matched measured support; rejected placements=" + foundationRejections + "; floating post observations=" + floatingPosts + "; stale signatures rejected.");
            report.AppendLine("\nIsolated runtime grounding/connection only; foundation, live traversal and production catalog publication remain unverified.");
            File.WriteAllText(reportPath, report.ToString());
        }
        catch (Exception exception)
        {
            // note: Preserve partial evidence and the actual rejected case instead of leaving a stale successful report.
            report.AppendLine("FAIL: " + exception.Message);
            File.WriteAllText(reportPath, report.ToString());
            throw;
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
    }

    public static void ReviewGroundedTimberHome()
    {
        // note: Actual TerrainData hides buried posts and exposes entrance-to-ground seams at the declared contact datum.
        VerifyTimberHomeAccess();
        string prefabPath = Folder + "/yq_timber_foundation_house4.prefab";
        string manifestPath = Folder + "/YQ_TimberHomeCandidate.asset";
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(manifestPath);
        if (manifest == null)
        {
            // note: Rebase pending metadata onto the new dependency boundary; no inherited approval is transferred to changed geometry.
            var source = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + "/YQ_StreetConnectedHomeCandidate.asset");
            if (source == null || source.Zones.Count != 1) throw new InvalidOperationException("Missing source home contract.");
            var zone = JsonUtility.FromJson<YQReviewedSemanticZoneRecord>(JsonUtility.ToJson(source.Zones[0]));
            string signature = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString();
            zone.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            foreach (var binding in zone.cellContractsV2)
            {
                binding.sourceSignature = signature;
                binding.reviewState = YQSemanticSiteReviewState.Pending;
                // note: Storage belongs to the changed candidate too; do not retain the donor assembly's dependency signature.
                foreach (var loot in binding.lootBindings)
                {
                    loot.sourceSignature = signature;
                    loot.reviewState = YQSemanticSiteReviewState.Pending;
                }
                foreach (var door in binding.doorBindings)
                {
                    door.reviewState = YQSemanticSiteReviewState.Pending;
                    // note: Measured top minimum 3.5839 versus walking datum 3.6795 requires 0.0956m; 0.12m leaves a small soil margin.
                    door.terrainApproach.walkingSurfaceAboveTerrain = .12f;
                    door.terrainApproach.reviewState = YQSemanticSiteReviewState.Pending;
                }
            }
            manifest = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
            manifest.ConfigureCandidate("timber_home_house4", source.SemanticStyleKey, signature, source.Topology,
                null, source.SourceInstanceCount + 4, new[] { zone });
            AssetDatabase.CreateAsset(manifest, manifestPath);
            AssetDatabase.SaveAssets();
        }
        if (manifest.SourceSignature != AssetDatabase.GetAssetDependencyHash(prefabPath).ToString())
            throw new InvalidOperationException("Timber candidate metadata is stale.");
        float clearance = manifest.Zones[0].cellContractsV2[0].doorBindings[0].terrainApproach.walkingSurfaceAboveTerrain;
        RenderHomeReview(prefabPath, "Logs/GroundedTimberHomeReview",
            "Logs/YQGroundedTimberHomeReview.txt", true, true, clearance);
    }

    private static void RenderHomeReview(string path, string captureFolder, string reportPath, bool requireTerrainContact = true, bool showTerrain = false, float soilClearance = 0f, float viewElevation = .4f)
    {
        // note: Render the actual isolated source-linked home for review, not the old whole-district candidate. No scene, source prefab or save is changed.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Independent cell review requires Edit mode.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Independent home candidate is missing.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        TerrainData reviewTerrain = null;
        RenderTexture previous = RenderTexture.active;
        var report = new StringBuilder("# Independent home render review\n\n");
        // note: Evidence records identify the actual loaded code, so later source edits cannot be confused with the version that produced these images.
        report.AppendLine("Review schema: foundation-contact-1\nCaptured UTC: " + DateTime.UtcNow.ToString("O") +
            "\nEditor module: " + typeof(YQCandidateInteriorBuilderV2).Module.ModuleVersionId +
            "\nRuntime module: " + typeof(YQGeneratedWorldRuntimeBuilder).Module.ModuleVersionId);
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            root.SetActive(true);
            YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(root);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds();
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found) throw new InvalidOperationException("Independent home has no visible geometry.");
            report.AppendLine("Source: " + path + "\nDependency hash: " + AssetDatabase.GetAssetDependencyHash(path));
            report.AppendLine("Bounds: " + bounds + "\nRenderers: " + renderers.Length);
            // note: A clear doorway does not prove the house meets terrain; measure structural bottoms against the actual lower landing datum.
            Transform contact = root.transform.Find("YQ_V2_EntranceRepairs/SM_House4_Door6_StairApproach/TerrainContact");
            if (contact == null && requireTerrainContact)
                throw new InvalidOperationException("Foundation review cannot resolve the authored TerrainContact; no contact evidence was collected.");
            if (showTerrain && contact != null)
            {
                // note: The isolated flat plot is a contact review fixture, not replacement art or a generated-world quality claim.
                reviewTerrain = new TerrainData { heightmapResolution = 65, size = new Vector3(64f, 10f, 64f) };
                var terrainObject = Terrain.CreateTerrainGameObject(reviewTerrain);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(terrainObject, scene);
                terrainObject.transform.position = new Vector3(bounds.center.x - 32f, contact.position.y - soilClearance, bounds.center.z - 32f);
                report.AppendLine("Declared walking-surface clearance above soil: " + soilClearance.ToString("F4"));
                // note: Diagnostics must use the same soil elevation as the rendered TerrainData, including the authored clearance.
                float soilHeight = terrainObject.transform.position.y;
                report.AppendLine("Grounded review: flat TerrainData surface Y=" + soilHeight.ToString("F4"));
                // note: Compare visible landing geometry with the collider-owned datum; a collision-clear approach can still be visibly buried.
                Transform landing = RequirePath(root.transform, "YQ_V2_EntranceRepairs/SM_House4_Door6_StairApproach/LowerLanding");
                int below = 0, total = 0;
                float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
                foreach (MeshFilter filter in landing.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || !filter.name.EndsWith("LOD0", StringComparison.Ordinal)) continue;
                    // note: Read-only mesh acquisition supports imported meshes with CPU read/write disabled and preserves import settings.
                    using (var meshData = Mesh.AcquireReadOnlyMeshData(filter.sharedMesh))
                    {
                        var vertices = new Unity.Collections.NativeArray<Vector3>(meshData[0].vertexCount, Unity.Collections.Allocator.Temp);
                        try
                        {
                            meshData[0].GetVertices(vertices);
                            foreach (Vector3 vertex in vertices)
                            {
                                float height = filter.transform.TransformPoint(vertex).y;
                                lowest = Mathf.Min(lowest, height);
                                highest = Mathf.Max(highest, height);
                                if (height < soilHeight) below++;
                                total++;
                            }
                        }
                        finally { vertices.Dispose(); }
                    }
                }
                report.AppendLine("Landing visible vertices below terrain: " + below + "/" + total +
                    "; world height range=" + lowest.ToString("F4") + ".." + highest.ToString("F4") +
                    ". Includes underside vertices; top-surface contact needs separate review.");
                // note: Sample the visible mesh itself; the imported box collider is the suspected mismatch and cannot validate its own top height.
                var surfaceHeights = new List<float>();
                foreach (MeshFilter filter in landing.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || !filter.name.EndsWith("LOD0", StringComparison.Ordinal)) continue;
                    var probeObject = new GameObject("LandingMeshContactProbe");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(probeObject, scene);
                    probeObject.transform.SetParent(filter.transform, false);
                    try
                    {
                        var probe = probeObject.AddComponent<MeshCollider>();
                        probe.sharedMesh = filter.sharedMesh;
                        Physics.SyncTransforms();
                        Bounds box = filter.GetComponent<Renderer>().bounds;
                        for (int z = 1; z < 10; z++)
                            for (int x = 1; x < 10; x++)
                            {
                                Vector3 start = new Vector3(Mathf.Lerp(box.min.x, box.max.x, x / 10f), box.max.y + 1f,
                                    Mathf.Lerp(box.min.z, box.max.z, z / 10f));
                                if (probe.Raycast(new Ray(start, Vector3.down), out RaycastHit hit, 2f)) surfaceHeights.Add(hit.point.y);
                            }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(probeObject); }
                }
                surfaceHeights.Sort();
                if (surfaceHeights.Count == 0) throw new InvalidOperationException("Landing visual mesh produced no top-surface evidence.");
                report.AppendLine("Landing top surface samples=" + surfaceHeights.Count + "; minimum=" + surfaceHeights[0].ToString("F4") +
                    "; median=" + surfaceHeights[surfaceHeights.Count / 2].ToString("F4") + "; maximum=" +
                    surfaceHeights[surfaceHeights.Count - 1].ToString("F4"));
            }
            if (contact != null)
            {
                report.AppendLine("\nTerrain contact world Y: " + contact.position.y.ToString("F4"));
                foreach (Renderer renderer in renderers)
                {
                    string name = renderer.name.ToLowerInvariant();
                    if (!renderer.enabled || (!name.Contains("floor") && !name.Contains("wall") &&
                        !name.Contains("pillar") && !name.Contains("post") && !name.Contains("pathway"))) continue;
                    string hierarchy = renderer.name;
                    for (Transform owner = renderer.transform.parent; owner != null && owner != root.transform; owner = owner.parent)
                        hierarchy = owner.name + "/" + hierarchy;
                    report.AppendLine("- Structural contact candidate " + hierarchy + " | bottom=" + renderer.bounds.min.y.ToString("F4") +
                        " | aboveLanding=" + (renderer.bounds.min.y - contact.position.y).ToString("F4") +
                        " | bounds=" + renderer.bounds);
                }
                report.AppendLine("These measurements identify missing support candidates; upper walls legitimately need not touch terrain.\n");
            }
            foreach (Transform child in root.transform)
                report.AppendLine("- " + child.name + " local=" + child.localPosition);
            // note: Measure a small set of same-kit foundation candidates before choosing support geometry; never substitute arbitrary primitives.
            foreach (string assetName in new[] { "SM_StoneWall_PS3", "SM_StoneWall_PS4", "SM_StoneWall_PS5", "SM_WoodenWatertank_Foundation" })
            {
                var supportPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Viking + assetName + ".prefab");
                if (supportPrefab == null) continue;
                var support = (GameObject)PrefabUtility.InstantiatePrefab(supportPrefab, scene);
                try
                {
                    Renderer[] parts = support.GetComponentsInChildren<Renderer>(true);
                    bool measured = false;
                    Bounds supportBounds = default;
                    foreach (Renderer part in parts)
                    {
                        if (!part.enabled) continue;
                        if (!measured) { supportBounds = part.bounds; measured = true; }
                        else supportBounds.Encapsulate(part.bounds);
                    }
                    report.AppendLine("Foundation source candidate " + assetName + " bounds=" + supportBounds);
                }
                finally { UnityEngine.Object.DestroyImmediate(support); }
            }
            var cameraObject = new GameObject("CellReviewCamera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.19f, .22f, .25f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = .05f;
            var sunObject = new GameObject("CellReviewLight", typeof(Light));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sunObject, scene);
            Light sun = sunObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            target = new RenderTexture(1280, 960, 24);
            camera.targetTexture = target;
            Directory.CreateDirectory(captureFolder);
            for (int index = 0; index < 4; index++)
            {
                Vector3 direction = Quaternion.Euler(0, 45 + index * 90, 0) * new Vector3(0, viewElevation, -1).normalized;
                float distance = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 2.3f;
                camera.farClipPlane = distance * 4 + 100;
                camera.transform.position = bounds.center + direction * distance;
                camera.transform.LookAt(bounds.center);
                sun.transform.rotation = camera.transform.rotation * Quaternion.Euler(20, -30, 0);
                camera.Render();
                RenderTexture.active = target;
                var capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try
                {
                    capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    capture.Apply();
                    File.WriteAllBytes(captureFolder + "/view-" + index + ".png", capture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(capture); }
            }
            report.AppendLine("\nFour exterior views captured. Images require inspection; capture is not approval.");
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            if (reviewTerrain != null) UnityEngine.Object.DestroyImmediate(reviewTerrain);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(reportPath, report.ToString());
        }
    }

    public static void PrepareIndependentHomeBindings()
    {
        // note: Extraction changes the dependency boundary: retained home providers are rebased, while deleted district doors and roads must not survive in the cell contract.
        const string prefabPath = Folder + "/yq_accessible_house4_cell.prefab";
        const string outputPath = Folder + "/YQ_IndependentHomeCandidate.asset";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var source = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + "/YQ_AccessibleEasternCandidate.asset");
        if (prefab == null || source == null || source.Zones.Count != 1 || source.ReleaseEligible)
            throw new InvalidOperationException("Expected the pending accessible source and isolated home.");
        if (AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(outputPath) != null)
            throw new InvalidOperationException("Independent home metadata already exists; preserve it for review.");
        string signature = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString();
        var zone = JsonUtility.FromJson<YQReviewedSemanticZoneRecord>(JsonUtility.ToJson(source.Zones[0]));
        zone.stableId = "yq_independent_home_house4";
        zone.displayName = "Independent furnished home";
        zone.prefab = prefab;
        zone.authoredSourceOrigin = Vector3.zero;
        Bounds bounds = VisibleBoundsInFrame(prefab.transform, prefab.transform);
        zone.localBoundsCenter = bounds.center;
        zone.localBoundsSize = bounds.size;
        zone.sourceInstanceCount = prefab.GetComponentsInChildren<Transform>(true).Length;
        zone.authoredBuildingCount = 1;
        zone.authoredDressingCount = 0;
        zone.streamingCellIds.Clear();
        zone.connectionSocketPaths.RemoveAll(path => string.IsNullOrWhiteSpace(path) || prefab.transform.Find(path) == null);
        foreach (var contract in zone.cellContractsV2)
        {
            contract.cellId = zone.stableId;
            contract.sourceSignature = signature;
            contract.reviewState = YQSemanticSiteReviewState.Pending;
            contract.reviewNote = "Isolated furnished home: exterior views inspected; terrain approach and independent functional review still required.";
            contract.curation.EnsureCollections();
            contract.curation.sockets.RemoveAll(socket => socket == null || string.IsNullOrWhiteSpace(socket.transformPath) ||
                prefab.transform.Find(socket.transformPath) == null);
            foreach (var socket in contract.curation.sockets)
            {
                Transform transform = prefab.transform.Find(socket.transformPath);
                socket.localPosition = prefab.transform.InverseTransformPoint(transform.position);
                socket.localRotation = Quaternion.Inverse(prefab.transform.rotation) * transform.rotation;
            }
            contract.doorBindings.RemoveAll(door => door == null || string.IsNullOrWhiteSpace(door.targetPath) ||
                prefab.transform.Find(door.targetPath) == null);
            foreach (var door in contract.doorBindings)
            {
                // note: Door bindings identify the retained nested source prefab; the enclosing cell contract owns the new dependency signature.
                door.reviewState = YQSemanticSiteReviewState.Pending;
            }
            contract.lootBindings.RemoveAll(loot => loot == null || string.IsNullOrWhiteSpace(loot.targetPath) ||
                prefab.transform.Find(loot.targetPath) == null);
            foreach (var loot in contract.lootBindings)
            {
                loot.sourceSignature = signature;
                loot.reviewState = YQSemanticSiteReviewState.Pending;
            }
        }
        var candidate = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        bool saved = false;
        try
        {
            candidate.ConfigureCandidate("independent_home_house4", source.SemanticStyleKey, signature, source.Topology,
                null, zone.sourceInstanceCount, new[] { zone });
            int doors = 0, loot = 0;
            foreach (var contract in zone.cellContractsV2) { doors += contract.doorBindings.Count; loot += contract.lootBindings.Count; }
            if (doors != 1 || loot != 1) throw new InvalidOperationException("Isolated home must retain exactly its own door and storage provider.");
            AssetDatabase.CreateAsset(candidate, outputPath);
            AssetDatabase.SaveAssetIfDirty(candidate);
            saved = true;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQIndependentHomeBindings.txt", "PASS: isolated bounds/source identity; one retained door and storage provider; removed absent district sockets/doors. Pending terrain connection and functional approval. Runtime catalog unchanged.");
        }
        finally { if (!saved) UnityEngine.Object.DestroyImmediate(candidate); }
    }

    public static void PrepareStreetConnectedHome()
    {
        // note: Keep the inspected source immutable; this source-linked variant adds only an explicit road socket at the physically tested lower landing.
        const string sourcePath = Folder + "/yq_accessible_house4_cell.prefab";
        const string prefabPath = Folder + "/yq_street_connected_house4.prefab";
        const string manifestPath = Folder + "/YQ_StreetConnectedHomeCandidate.asset";
        const string approachPath = "YQ_V2_EntranceRepairs/SM_House4_Door6_StairApproach";
        const string connectionPath = approachPath + "/StreetConnection";
        var source = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + "/YQ_IndependentHomeCandidate.asset");
        if (source == null || source.ReleaseEligible || source.Zones.Count != 1 || File.Exists(prefabPath) || File.Exists(manifestPath))
            throw new InvalidOperationException("Expected a pending isolated home and unused street-connected output paths.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath), scene);
            Transform door = RequirePath(root.transform, DoorPath);
            Transform contact = RequirePath(root.transform, approachPath + "/TerrainContact");
            var socket = new GameObject("StreetConnection").transform;
            socket.SetParent(RequirePath(root.transform, approachPath), false);
            socket.SetPositionAndRotation(contact.position, Quaternion.LookRotation(door.right, Vector3.up));
            var zone = JsonUtility.FromJson<YQReviewedSemanticZoneRecord>(JsonUtility.ToJson(source.Zones[0]));
            Vector3 entrance = root.transform.InverseTransformPoint(socket.position);
            Vector3 outward = root.transform.InverseTransformDirection(socket.forward);
            if (!YQProceduralSettlementLayout.IsExternalConnection(zone.localBoundsCenter, zone.localBoundsSize, entrance, outward))
                throw new InvalidOperationException("Tested landing does not satisfy the layout's external connection contract.");
            // note: Preserve the exact height of stairs above their terrain contact; source root zero is not the building's ground datum.
            zone.authoredSourceOrigin = Vector3.down * entrance.y;
            zone.connectionSocketPaths = new List<string> { connectionPath };
            if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                throw new InvalidOperationException("Could not save source-linked street-connected home.");
            zone.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            string signature = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString();
            foreach (var contract in zone.cellContractsV2)
            {
                contract.sourceSignature = signature;
                foreach (var loot in contract.lootBindings) loot.sourceSignature = signature;
                foreach (var binding in contract.doorBindings)
                    binding.terrainApproach = new YQTerrainApproachContractV2 {
                        // note: Recorded route evidence is real, but runtime use stays pending until complete cell/provider validation.
                        authoredRouteVerified = true, reviewState = YQSemanticSiteReviewState.Pending,
                        supportPath = approachPath + "/LowerLanding", localStart = entrance, localOutward = outward };
            }
            var candidate = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
            bool saved = false;
            try
            {
                candidate.ConfigureCandidate(source.KitId, source.SemanticStyleKey, signature, source.Topology, null,
                    zone.sourceInstanceCount, new[] { zone });
                // note: Exercise the real deterministic layout with the actual prefab portal, rather than another numeric stand-in.
                for (int index = 0; index < 32; index++)
                {
                    string seed = YQProceduralSettlementLayout.SeedPrefix + "home-street-review|" + index;
                    if (!YQProceduralSettlementLayout.TryResolve(candidate, new HashSet<string> { zone.stableId }, seed,
                            out var layout, out string failure) || layout == null || layout.cells.Count != 1 || layout.streets.Count == 0)
                        throw new InvalidOperationException("Real home street layout failed: " + failure);
                }
                AssetDatabase.CreateAsset(candidate, manifestPath);
                AssetDatabase.SaveAssetIfDirty(candidate);
                saved = true;
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/YQStreetConnectedHome.txt", "PASS: real lower-landing external socket, 32 production layout seeds, explicit contact datum and source-linked variant. Pending complete provider/terrain materialization verification; no runtime catalog activation.");
            }
            finally { if (!saved) UnityEngine.Object.DestroyImmediate(candidate); }
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static void VerifyStreetConnectedHomeProviders()
    {
        VerifyHomeProviders(false);
    }

    public static void VerifyTimberHomeProviders()
    {
        // note: Changed geometry and rebased storage evidence must pass the runtime binders independently of the donor house.
        VerifyHomeProviders(true);
    }

    public static void VerifyJoistedHomeProviders()
    {
        // note: Exercise actual door and loot ownership on the same repaired prefab proposed for primary construction.
        VerifyHomeProviders(true, true);
    }

    private static void VerifyHomeProviders(bool timber, bool joisted = false)
    {
        // note: Run the real runtime binders against translated/rotated inactive instances. Only disposable copies of pending evidence receive temporary test approval.
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + (joisted ? "/YQ_JoistedHomeCandidate.asset" : timber ? "/YQ_TimberHomeCandidate.asset" : "/YQ_StreetConnectedHomeCandidate.asset"));
        if (manifest == null || manifest.ReleaseEligible || manifest.Zones.Count != 1)
            throw new InvalidOperationException("Expected the pending street-connected home.");
        var zone = manifest.Zones[0];
        // note: Do not let stale candidate metadata inherit a successful test result from unchanged provider paths.
        if (manifest.SourceSignature != AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(zone.prefab)).ToString())
            throw new InvalidOperationException("Candidate dependency signature is stale.");
        var contract = zone.cellContractsV2[0];
        if (contract.doorBindings.Count != 1 || contract.lootBindings.Count != 1)
            throw new InvalidOperationException("Expected one door and storage provider.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var report = new StringBuilder(timber ? "# Timber home provider tests\n\n" : "# Street-connected home provider tests\n\n");
        try
        {
            for (int index = 0; index < 4; index++)
            {
                var staging = new GameObject("InactiveProviderReview");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(staging, scene);
                staging.SetActive(false);
                try
                {
                    var copy = (GameObject)PrefabUtility.InstantiatePrefab(zone.prefab, staging.transform);
                    copy.transform.SetPositionAndRotation(new Vector3(21, 7, -13), Quaternion.Euler(0, index * 90, 0));
                    var door = JsonUtility.FromJson<YQCellDoorBindingV2>(JsonUtility.ToJson(contract.doorBindings[0]));
                    var loot = JsonUtility.FromJson<YQCellLootBindingV2>(JsonUtility.ToJson(contract.lootBindings[0]));
                    door.reviewState = YQSemanticSiteReviewState.Approved;
                    loot.reviewState = YQSemanticSiteReviewState.Approved;
                    string location = "isolated-provider-fixture-" + index;
                    if (!YQCellDoorBindingsV2.TryBind(copy.transform, door, location, zone.stableId,
                            "Fixture door", "fixture-region", out string failure))
                        throw new InvalidOperationException("Door binder rejected: " + failure);
                    if (!YQCellDoorBindingsV2.TryBind(copy.transform, door, location, zone.stableId,
                            "Fixture door", "fixture-region", out failure))
                        throw new InvalidOperationException("Door binding was not idempotent: " + failure);
                    if (!YQCellLootBindingsV2.TryBind(copy.transform, loot, manifest.SourceSignature, location, zone.stableId,
                            "Fixture storage", "fixture-region", 1, out failure))
                        throw new InvalidOperationException("Storage binder rejected: " + failure);
                    if (!YQCellLootBindingsV2.TryBind(copy.transform, loot, manifest.SourceSignature, location, zone.stableId,
                            "Fixture storage", "fixture-region", 1, out failure))
                        throw new InvalidOperationException("Storage binding was not idempotent: " + failure);
                    Transform chest = RequirePath(copy.transform, loot.targetPath);
                    chest.GetComponent<YQLockpickableLoot>().SendMessage("Awake", SendMessageOptions.RequireReceiver);
                    var box = chest.GetComponent<BoxCollider>();
                    if (box.center != loot.colliderCenter || box.size != loot.colliderSize || chest.GetComponents<Collider>().Length != 1 ||
                        copy.GetComponentsInChildren<YQLockpickableDoor>(true).Length != 1 ||
                        copy.GetComponentsInChildren<YQLockpickableLoot>(true).Length != 1)
                        throw new InvalidOperationException("Provider setup changed collision or duplicated interaction ownership.");
                    // note: Negative probes ensure stale evidence and a conflicting location cannot silently hijack an existing provider.
                    if (YQCellDoorBindingsV2.TryBind(copy.transform, door, "different-location", zone.stableId,
                            "Fixture", "fixture-region", out _) ||
                        YQCellLootBindingsV2.TryBind(copy.transform, loot, "stale-source", location, zone.stableId,
                            "Fixture", "fixture-region", 1, out _))
                        throw new InvalidOperationException("Conflicting provider ownership or stale source evidence passed.");
                    // note: Test the production landing-datum solver with the real cell at each rotation; do not approve or mutate the persisted candidate contract.
                    var testContract = JsonUtility.FromJson<YQReviewedCellFunctionContractV2>(JsonUtility.ToJson(contract));
                    // note: Candidate qualification operates on a disposable review copy; version-one foundation evidence must be exercised, not bypassed.
                    if (joisted)
                    {
                        testContract.independentAssembly.reviewState = YQSemanticSiteReviewState.Approved;
                        testContract.independentAssembly.foundationVerified = true;
                    }
                    testContract.doorBindings[0].reviewState = YQSemanticSiteReviewState.Approved;
                    testContract.doorBindings[0].terrainApproach.reviewState = YQSemanticSiteReviewState.Approved;
                    var data = new TerrainData { heightmapResolution = 33, size = new Vector3(128, 140, 128) };
                    GameObject ground = null;
                    try
                    {
                        Vector3 landing = copy.transform.TransformPoint(testContract.doorBindings[0].terrainApproach.localStart);
                        ground = Terrain.CreateTerrainGameObject(data);
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
                        ground.transform.position = new Vector3(landing.x - 64, landing.y - .4f, landing.z - 64);
                        var solver = typeof(YQCompiledWorldSiteInstance).GetMethod("TryResolveReviewedLandingDelta",
                            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                        object[] arguments = { copy.transform, ground.GetComponent<Terrain>(), testContract, 0f };
                        // note: The declared soil clearance changes the expected rigid correction, not the door's walking datum.
                        float expectedCorrection = -.4f + testContract.doorBindings[0].terrainApproach.walkingSurfaceAboveTerrain;
                        if (!(bool)solver.Invoke(null, arguments) || Mathf.Abs((float)arguments[3] - expectedCorrection) > .001f)
                            throw new InvalidOperationException("Reviewed landing did not control the rigid cell datum.");
                        testContract.doorBindings[0].terrainApproach.supportPath = "missing-support";
                        arguments[3] = 0f;
                        if ((bool)solver.Invoke(null, arguments)) throw new InvalidOperationException("Missing landing support was accepted.");
                    }
                    finally
                    {
                        if (ground != null) UnityEngine.Object.DestroyImmediate(ground);
                        UnityEngine.Object.DestroyImmediate(data);
                    }
                    report.AppendLine("PASS yaw=" + index * 90 + ": door/storage binding, repeat binding, collider preservation, ownership/source rejection.");
                    report.AppendLine("PASS: explicit landing datum and missing-support rejection.");
                }
                finally { UnityEngine.Object.DestroyImmediate(staging); }
            }
            report.AppendLine("\nNo persistent contract approval, catalog activation, player interaction or save modification.");
        }
        catch (Exception error) { report.AppendLine("FAILED: " + error.Message); throw; }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(timber ? "Logs/YQTimberHomeProviders.txt" : "Logs/YQStreetConnectedHomeProviders.txt", report.ToString());
        }
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Home Functional Access")]
    public static void VerifyHomeFunctionalAccess()
    {
        VerifyHomeFunctionalAccess(null);
    }

    public static void VerifyTimberHomeAccess()
    {
        // note: The foundation changes the collision hierarchy; source-home results cannot certify the new prefab.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/yq_timber_foundation_house4.prefab");
        if (prefab == null) throw new InvalidOperationException("Timber foundation candidate is missing.");
        VerifyHomeFunctionalAccess(prefab);
    }

    public static void VerifyJoistedHomeAccess()
    {
        // note: New support members must not intrude into the retained doorway or functional interior clearances.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/yq_joisted_house4.prefab");
        if (prefab == null) throw new InvalidOperationException("Missing joisted candidate.");
        VerifyHomeFunctionalAccess(prefab, "Logs/YQJoistedHomeFunctionalAccess.md");
    }

    public static void RefreshJoistedHomeManifest()
    {
        // note: Re-review current joisted geometry before rebasing pending metadata; no approval flag or runtime catalog entry is created here.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/yq_joisted_house4.prefab");
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + "/YQ_JoistedHomeCandidate.asset");
        if (prefab == null || manifest == null || manifest.Zones.Count != 1)
            throw new InvalidOperationException("Missing joisted candidate or pending manifest.");
        VerifyHomeFunctionalAccess(prefab, "Logs/YQJoistedHomeFunctionalAccess.md", false);
        // note: Refresh the visual hierarchy evidence from the same current prefab before rebasing its pending source signature.
        RenderHomeReview(AssetDatabase.GetAssetPath(prefab), "Logs/JoistedHomeReviewCurrent",
            "Logs/YQJoistedHomeReviewCurrent.txt", true, true, .12f);
        string signature = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(prefab)).ToString();
        var zone = JsonUtility.FromJson<YQReviewedSemanticZoneRecord>(JsonUtility.ToJson(manifest.Zones[0]));
        zone.prefab = prefab;
        foreach (var contract in zone.cellContractsV2)
        {
            contract.sourceSignature = signature;
            contract.reviewState = YQSemanticSiteReviewState.Pending;
            contract.reviewNote = "Rebased to current joisted geometry; runtime qualification pending.";
            if (contract.independentAssembly != null)
            {
                contract.independentAssembly.sourceSignature = signature;
                contract.independentAssembly.reviewState = YQSemanticSiteReviewState.Pending;
            }
            foreach (var door in contract.doorBindings)
            {
                door.reviewState = YQSemanticSiteReviewState.Pending;
                if (door.terrainApproach != null) door.terrainApproach.reviewState = YQSemanticSiteReviewState.Pending;
            }
            foreach (var loot in contract.lootBindings)
            {
                loot.sourceSignature = signature;
                loot.reviewState = YQSemanticSiteReviewState.Pending;
            }
        }
        int instances = 0;
        foreach (var item in prefab.GetComponentsInChildren<Transform>(true))
            if (PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject)) instances++;
        zone.sourceInstanceCount = instances;
        manifest.ConfigureCandidate(manifest.KitId, manifest.SemanticStyleKey, signature, manifest.Topology,
            manifest.StreamingSite, instances, new[] { zone });
        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/YQJoistedHomeManifestRefresh.txt",
            "PASS: current joisted geometry re-reviewed and pending manifest rebased; signature=" + signature + "; instances=" + instances);
    }

    public static void RefreshTimberHomeManifest()
    {
        // note: Re-review the timber foundation dependency before rebasing its pending terrain contract; release eligibility remains unchanged.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/yq_timber_foundation_house4.prefab");
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + "/YQ_TimberHomeCandidate.asset");
        if (prefab == null || manifest == null || manifest.Zones.Count != 1)
            throw new InvalidOperationException("Missing timber candidate or pending manifest.");
        VerifyHomeFunctionalAccess(prefab, "Logs/YQTimberHomeFunctionalAccess.md", false);
        string signature = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(prefab)).ToString();
        var zone = JsonUtility.FromJson<YQReviewedSemanticZoneRecord>(JsonUtility.ToJson(manifest.Zones[0]));
        zone.prefab = prefab;
        foreach (var contract in zone.cellContractsV2)
        {
            contract.sourceSignature = signature;
            contract.reviewState = YQSemanticSiteReviewState.Pending;
            contract.reviewNote = "Rebased to current timber foundation geometry; runtime qualification pending.";
            if (contract.independentAssembly != null)
            {
                contract.independentAssembly.sourceSignature = signature;
                contract.independentAssembly.reviewState = YQSemanticSiteReviewState.Pending;
            }
            foreach (var door in contract.doorBindings)
            {
                door.reviewState = YQSemanticSiteReviewState.Pending;
                if (door.terrainApproach != null) door.terrainApproach.reviewState = YQSemanticSiteReviewState.Pending;
            }
            foreach (var loot in contract.lootBindings)
            {
                loot.sourceSignature = signature;
                loot.reviewState = YQSemanticSiteReviewState.Pending;
            }
        }
        int instances = 0;
        foreach (var item in prefab.GetComponentsInChildren<Transform>(true))
            if (PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject)) instances++;
        zone.sourceInstanceCount = instances;
        manifest.ConfigureCandidate(manifest.KitId, manifest.SemanticStyleKey, signature, manifest.Topology,
            manifest.StreamingSite, instances, new[] { zone });
        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/YQTimberHomeManifestRefresh.txt",
            "PASS: current timber geometry re-reviewed and pending manifest rebased; signature=" + signature + "; instances=" + instances);
    }

    public static void PrepareJoistedHomeManifest()
    {
        // note: Carry semantic bindings onto the changed geometry with fresh dependency identity, never inherited approval.
        VerifyJoistedHomeAccess();
        string path = Folder + "/YQ_JoistedHomeCandidate.asset";
        if (AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(path) != null)
            throw new InvalidOperationException("Joisted manifest already exists; preserve its review state.");
        var source = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + "/YQ_TimberHomeCandidate.asset");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/yq_joisted_house4.prefab");
        if (source == null || prefab == null || source.Zones.Count != 1) throw new InvalidOperationException("Missing candidate source.");
        string signature = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(prefab)).ToString();
        var zone = JsonUtility.FromJson<YQReviewedSemanticZoneRecord>(JsonUtility.ToJson(source.Zones[0]));
        zone.prefab = prefab;
        foreach (var contract in zone.cellContractsV2)
        {
            contract.sourceSignature = signature;
            contract.reviewState = YQSemanticSiteReviewState.Pending;
            contract.reviewNote = "Joisted support candidate; clearance probes passed. Structural and runtime qualification pending.";
            contract.independentAssembly = new YQIndependentAssemblyContract { sourceSignature = signature,
                reviewState = YQSemanticSiteReviewState.Pending };
            foreach (var door in contract.doorBindings)
            {
                door.reviewState = YQSemanticSiteReviewState.Pending;
                door.terrainApproach.reviewState = YQSemanticSiteReviewState.Pending;
            }
            foreach (var loot in contract.lootBindings)
            {
                loot.sourceSignature = signature;
                loot.reviewState = YQSemanticSiteReviewState.Pending;
            }
        }
        // note: Count actual nested prefab instances instead of copying the earlier foundation's four-piece count.
        int instances = 0;
        foreach (var item in prefab.GetComponentsInChildren<Transform>(true))
            if (PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject)) instances++;
        zone.sourceInstanceCount = instances;
        var manifest = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        manifest.ConfigureCandidate("joisted_home_house4", source.SemanticStyleKey, signature, source.Topology,
            null, instances, new[] { zone });
        AssetDatabase.CreateAsset(manifest, path);
        AssetDatabase.SaveAssets();
    }

    public static void VerifyTerrainCandidateCancellation()
    {
        // note: Preserve the focused cancellation entry point.
        VerifyTerrainCandidateOwnership(true);
    }

    public static void VerifyTerrainCandidateCompletion()
    {
        // note: The successful path must transfer a hidden terrain without replacing the live sentinel.
        VerifyTerrainCandidateOwnership(false);
    }

    private static void VerifyTerrainCandidateOwnership(bool cancel)
    {
        // note: Drive the real nested iterators to the first data upload, then dispose them as the production cancellation wrapper does.
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var stack = new Stack<System.Collections.IEnumerator>();
        var baseline = new HashSet<int>();
        foreach (var data in Resources.FindObjectsOfTypeAll<TerrainData>()) baseline.Add(data.GetInstanceID());
        TerrainData candidateData = null;
        var sentinelData = new TerrainData { heightmapResolution = 33 };
        try
        {
            var sentinel = Terrain.CreateTerrainGameObject(sentinelData);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sentinel, scene);
            sentinel.name = "YQ_GENERATED_TERRAIN";
            baseline.Add(sentinelData.GetInstanceID());
            var staging = new GameObject("IsolatedTerrainStaging");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(staging, scene);
            staging.SetActive(false);
            bool completed = false;
            Terrain received = null;
            int callbacks = 0;
            stack.Push(YQGeneratedWorldTerrain.BuildCandidateRoutine(staging.transform, null, terrain => { completed = true; received = terrain; callbacks++; }));
            int steps = 0;
            while (stack.Count > 0 && (!cancel || candidateData == null))
            {
                if (++steps > 100000) throw new InvalidOperationException("Candidate upload was not reached.");
                var iterator = stack.Peek();
                if (!iterator.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (iterator.Current is System.Collections.IEnumerator nested) { stack.Push(nested); continue; }
                foreach (var data in Resources.FindObjectsOfTypeAll<TerrainData>())
                    if (!baseline.Contains(data.GetInstanceID()) && data.name.StartsWith("YQ_GeneratedTerrainData_", StringComparison.Ordinal)) candidateData = data;
            }
            if (!cancel)
            {
                if (!completed || callbacks != 1 || received == null || received.terrainData == null ||
                    received.transform.parent != staging.transform || received.gameObject.activeInHierarchy || sentinel == null || sentinelData == null)
                    throw new InvalidOperationException("Candidate completion did not preserve hidden ownership and existing terrain.");
                candidateData = received.terrainData;
                File.WriteAllText("Logs/YQTerrainCandidateCompletion.txt", "PASS: completed real candidate, one ownership callback, hidden staging parent retained, existing terrain preserved.");
                return;
            }
            if (candidateData == null || completed) throw new InvalidOperationException("Cancellation fixture missed in-flight ownership.");
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            if (candidateData != null || sentinel == null || sentinelData == null || staging.transform.childCount != 0 || completed)
                throw new InvalidOperationException("Candidate cancellation leaked data or changed existing terrain.");
            File.WriteAllText("Logs/YQTerrainCandidateCancellation.txt", "PASS: cancelled real upload, untransferred TerrainData destroyed, existing terrain preserved, no completion callback.");
        }
        finally
        {
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            if (candidateData != null) UnityEngine.Object.DestroyImmediate(candidateData);
            if (sentinelData != null) UnityEngine.Object.DestroyImmediate(sentinelData);
        }
    }

    public static void VerifyGradedJoistedPlacement()
    {
        // note: Keep the existing isolated parcel regression alongside real street packing.
        VerifyGradedJoistedPlacement(false);
    }

    public static void VerifyGradedJoistedStreet()
    {
        // note: Pack multiple instances through the production layout builder before grading the shared site.
        VerifyGradedJoistedPlacement(true);
    }

    public static void VerifyProductionJoistedStreet()
    {
        // note: Qualify the active parcel version emitted by TryBuild; experimental earthwork tests remain separate.
        VerifyGradedJoistedPlacement(true, false);
    }

    public static void PublishQualifiedHomeLibrary()
    {
        // note: Publish only the existing measured home after its real geometry, providers and current terrain writer pass; donor candidate approvals stay untouched.
        VerifyJoistedFrameContacts();
        VerifyJoistedHomeAccess();
        VerifyJoistedHomeProviders();
        VerifyJoistedTerrainIntegration();
        VerifyProductionJoistedStreet();
        var source = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + "/YQ_JoistedHomeCandidate.asset");
        var sourceZone = source.Zones[0];
        var zone = JsonUtility.FromJson<YQReviewedSemanticZoneRecord>(JsonUtility.ToJson(sourceZone));
        zone.prefab = sourceZone.prefab;
        zone.districtFunction = YQDistrictFunction.Residential;
        zone.semanticTags = new List<string> { "residential", "service", "commerce", "circulation", "medieval", "viking", "poi" };
        var contract = zone.cellContractsV2[0];
        contract.reviewState = YQSemanticSiteReviewState.Approved;
        contract.reviewNote = "Qualified existing furnished home: structural contacts, door/loot binding, access clearances and 25 production street placements verified.";
        contract.independentAssembly.reviewState = YQSemanticSiteReviewState.Approved;
        contract.independentAssembly.completeStructuralDependencies = true;
        contract.independentAssembly.foundationVerified = true;
        foreach (var door in contract.doorBindings)
        {
            door.reviewState = YQSemanticSiteReviewState.Approved;
            door.terrainApproach.reviewState = YQSemanticSiteReviewState.Approved;
        }
        foreach (var loot in contract.lootBindings) loot.reviewState = YQSemanticSiteReviewState.Approved;
        var curation = contract.curation;
        curation.contractVersion = YQAssetCurationContractV2.SupportedContractVersion;
        curation.primaryFunction = YQAssetFunctionV2.Habitation;
        // note: The home supplies a verified interaction location for the existing generated resident/service owner, not a new vendor system.
        curation.secondaryFunctions = new List<YQAssetFunctionV2> { YQAssetFunctionV2.Service, YQAssetFunctionV2.Commerce, YQAssetFunctionV2.Circulation, YQAssetFunctionV2.Storage };
        curation.affordances = new List<YQAssetAffordanceV2> { YQAssetAffordanceV2.Entrance, YQAssetAffordanceV2.Sleeping, YQAssetAffordanceV2.TraversableSurface, YQAssetAffordanceV2.Storage };
        curation.supportMode = YQAssetSupportModeV2.Foundation;
        var entrance = contract.doorBindings[0];
        // note: Use the tested exterior approach and exact prefab transform, rather than an invented entrance point.
        string path = zone.connectionSocketPaths[0];
        var portal = zone.prefab.transform.Find(path);
        if (portal == null) throw new InvalidOperationException("Qualified home lost its street connection.");
        foreach (var kind in new[] { YQAssetSocketKindV2.Entrance, YQAssetSocketKindV2.Connection })
            curation.sockets.Add(new YQAssetSocketRecordV2
            {
                socketId = zone.stableId + ":qualified:" + kind,
                kind = kind, transformPath = path,
                localPosition = zone.prefab.transform.InverseTransformPoint(portal.position),
                localRotation = Quaternion.Inverse(zone.prefab.transform.rotation) * portal.rotation,
                clearanceSize = new Vector3(.76f, 1.8f, .76f)
            });
        const string kit = "qualified_viking_home";
        const string folder = "Assets/Assets/Resources/YQWorldSites/qualified_viking_home";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        string manifestPath = folder + "/YQRuntimeSemanticSite.asset";
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(manifestPath);
        bool create = manifest == null;
        if (create) manifest = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        manifest.ConfigureCandidate(kit, "medieval_viking_village", source.SourceSignature,
            YQSemanticExtractionTopology.SettlementDistricts, null, source.SourceInstanceCount, new[] { zone });
        manifest.MarkReleaseEligible();
        if (!YQSiteFunctionContractsV2.TryValidate(manifest, new[] { zone.stableId },
            new[] { YQAssetFunctionV2.Habitation, YQAssetFunctionV2.Circulation, YQAssetFunctionV2.Service, YQAssetFunctionV2.Commerce },
            YQWorldStructureUsagePolicy.FullyEnterable, out string failure))
            throw new InvalidOperationException("Qualified home contract rejected: " + failure);
        if (create) AssetDatabase.CreateAsset(manifest, manifestPath);
        else EditorUtility.SetDirty(manifest);
        var catalog = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldSiteCatalog>("Assets/Assets/Resources/YQRuntimeWorldSiteCatalog.asset");
        if (catalog == null) throw new InvalidOperationException("Runtime catalog missing.");
        var records = new List<YQRuntimeWorldSiteRecord>(catalog.Sites);
        records.RemoveAll(item => item.kitId == kit);
        records.Add(new YQRuntimeWorldSiteRecord
        {
            kitId = kit, semanticStyleKey = "medieval_viking_village", siteKind = YQAuthoredSiteKind.Settlement,
            topology = YQSemanticExtractionTopology.SettlementDistricts, presentationMode = YQWorldSitePresentationMode.SeamlessExterior,
            structureUsagePolicy = YQWorldStructureUsagePolicy.FullyEnterable, maximumEnterableStructures = 1,
            semanticTags = new List<string>(zone.semanticTags), runtimeManifestResourceKey = "YQWorldSites/" + kit + "/YQRuntimeSemanticSite",
            spatialMetadataVersion = YQRuntimeWorldSiteSpatialMetadataCompiler.MetadataVersion, spatiallyValidated = true, seamlessPlacementEligible = true,
            authoredFootprintCenter = zone.localBoundsCenter, authoredFootprintSize = zone.localBoundsSize,
            authoredFoundationY = zone.authoredSourceOrigin.y,
            authoredFootprintRadius = new Vector2(zone.localBoundsSize.x, zone.localBoundsSize.z).magnitude * .5f,
            activeCellCount = 1, activeInstanceCount = source.SourceInstanceCount, spatialSignature = source.SourceSignature
            , reviewedFunctionsV2 = new List<YQAssetFunctionV2> { YQAssetFunctionV2.Habitation, YQAssetFunctionV2.Service, YQAssetFunctionV2.Commerce, YQAssetFunctionV2.Circulation, YQAssetFunctionV2.Storage }
        });
        catalog.Configure(records);
        EditorUtility.SetDirty(catalog);
        SaveQualifiedSourceManifest(manifest);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/YQQualifiedHomePublication.txt", "PASS: qualified home published with source-matched habitation, service, commerce, circulation and storage evidence. No camp functions declared.");
    }

    public static void PublishQualifiedHomesteadEncounter()
    {
        // note: Reuse qualified physical content as an occupied homestead; actual cover collision and the existing loot/door providers must pass before declaring encounter functions.
        PublishQualifiedHomeLibrary();
        const string kit = "qualified_occupied_homestead";
        var source = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>("Assets/Assets/Resources/YQWorldSites/qualified_viking_home/YQRuntimeSemanticSite.asset");
        var zone = JsonUtility.FromJson<YQReviewedSemanticZoneRecord>(JsonUtility.ToJson(source.Zones[0]));
        zone.prefab = source.Zones[0].prefab;
        var contract = zone.cellContractsV2[0];
        var work = contract.curation.sockets.Find(socket => socket != null && socket.compatibilityKey == "work_access");
        if (work == null) throw new InvalidOperationException("The verified standing access point is missing.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(zone.prefab, scene);
            Vector3 standing = instance.transform.TransformPoint(work.localPosition);
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            int coveredDirections = 0;
            for (int index = 0; index < 8; index++)
            {
                // note: Count real solid architecture at torso height, never socket labels or renderer-only bounds.
                var ray = new Ray(standing + Vector3.up * 1.1f, Quaternion.Euler(0f, index * 45f, 0f) * Vector3.forward);
                bool covered = false;
                foreach (var collider in colliders)
                    if (collider.enabled && !collider.isTrigger && collider.bounds.size.y >= 1.2f &&
                        collider.Raycast(ray, out var hit, 8f) && hit.distance > .45f)
                    { covered = true; break; }
                if (covered) coveredDirections++;
            }
            if (coveredDirections < 2) throw new InvalidOperationException("Homestead has insufficient measured architectural cover: " + coveredDirections);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        zone.districtFunction = YQDistrictFunction.Defensive;
        zone.semanticTags = new List<string> { "encounter", "perimeter", "circulation", "poi", "medieval", "viking", "occupied_homestead" };
        contract.curation.primaryFunction = YQAssetFunctionV2.Encounter;
        contract.curation.secondaryFunctions = new List<YQAssetFunctionV2> { YQAssetFunctionV2.Reward, YQAssetFunctionV2.Security, YQAssetFunctionV2.Circulation, YQAssetFunctionV2.Storage };
        contract.curation.affordances.AddRange(new[] { YQAssetAffordanceV2.SpawnAnchor, YQAssetAffordanceV2.RewardAnchor, YQAssetAffordanceV2.Cover });
        foreach (var kind in new[] { YQAssetSocketKindV2.Spawn, YQAssetSocketKindV2.Cover })
        {
            var socket = JsonUtility.FromJson<YQAssetSocketRecordV2>(JsonUtility.ToJson(work));
            socket.kind = kind;
            socket.socketId = zone.stableId + ":homestead:" + kind;
            contract.curation.sockets.Add(socket);
        }
        const string folder = "Assets/Assets/Resources/YQWorldSites/qualified_occupied_homestead";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        string path = folder + "/YQRuntimeSemanticSite.asset";
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(path);
        bool create = manifest == null;
        if (create) manifest = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        manifest.ConfigureCandidate(kit, "medieval_viking_village", source.SourceSignature,
            YQSemanticExtractionTopology.CampZones, null, source.SourceInstanceCount, new[] { zone });
        manifest.MarkReleaseEligible();
        if (!YQSiteFunctionContractsV2.TryValidate(manifest, new[] { zone.stableId },
            new[] { YQAssetFunctionV2.Encounter, YQAssetFunctionV2.Reward, YQAssetFunctionV2.Security },
            YQWorldStructureUsagePolicy.FullyEnterable, out string failure)) throw new InvalidOperationException(failure);
        if (create) AssetDatabase.CreateAsset(manifest, path); else EditorUtility.SetDirty(manifest);
        var catalog = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldSiteCatalog>("Assets/Assets/Resources/YQRuntimeWorldSiteCatalog.asset");
        var record = JsonUtility.FromJson<YQRuntimeWorldSiteRecord>(JsonUtility.ToJson(catalog.FindByKitId("qualified_viking_home")));
        record.kitId = kit; record.siteKind = YQAuthoredSiteKind.Camp; record.topology = YQSemanticExtractionTopology.CampZones;
        record.semanticTags = zone.semanticTags;
        record.reviewedFunctionsV2 = new List<YQAssetFunctionV2> { YQAssetFunctionV2.Encounter, YQAssetFunctionV2.Reward, YQAssetFunctionV2.Security, YQAssetFunctionV2.Circulation, YQAssetFunctionV2.Storage };
        record.runtimeManifestResourceKey = "YQWorldSites/" + kit + "/YQRuntimeSemanticSite";
        var records = new List<YQRuntimeWorldSiteRecord>(catalog.Sites);
        records.RemoveAll(item => item.kitId == kit); records.Add(record);
        catalog.Configure(records); EditorUtility.SetDirty(catalog);
        SaveQualifiedSourceManifest(manifest);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/YQQualifiedHomesteadEncounter.txt", "PASS: existing qualified home, measured solid cover, standing access and real loot provider. Encounter catalog entry published; live spawn remains to verify.");
    }

    private static void SaveQualifiedSourceManifest(YQReviewedSemanticSiteManifest runtime)
    {
        // note: Catalog rebuilds consume this authoritative reviewed source; publishing only a Resources copy would discard the qualification on the next migration.
        string folder = "Assets/Assets/GeneratedAssets/WorldAssemblies/SemanticProfiles/" + runtime.KitId;
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        string path = folder + "/YQ_" + runtime.KitId + "_ReviewedSemanticSite.asset";
        var source = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(path);
        if (source == null)
        {
            source = UnityEngine.Object.Instantiate(runtime);
            AssetDatabase.CreateAsset(source, path);
        }
        else { EditorUtility.CopySerialized(runtime, source); EditorUtility.SetDirty(source); }
    }

    private static void VerifyGradedJoistedPlacement(bool streetLayout, bool experimentalEarthwork = true)
    {
        // note: Exercise the existing parcel writer and runtime placement on the same final heightfield using saved candidate contacts.
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + "/YQ_JoistedHomeCandidate.asset");
        if (manifest == null || manifest.Zones.Count != 1) throw new InvalidOperationException("Missing candidate.");
        var prefab = manifest.Zones[0].prefab;
        if (prefab == null || manifest.SourceSignature != AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(prefab)).ToString())
            throw new InvalidOperationException("Stale candidate.");
        var contract = JsonUtility.FromJson<YQReviewedCellFunctionContractV2>(JsonUtility.ToJson(manifest.Zones[0].cellContractsV2[0]));
        contract.independentAssembly.reviewState = YQSemanticSiteReviewState.Approved;
        contract.independentAssembly.foundationVerified = true;
        foreach (var door in contract.doorBindings)
        {
            door.reviewState = YQSemanticSiteReviewState.Approved;
            door.terrainApproach.reviewState = YQSemanticSiteReviewState.Approved;
        }
        var grade = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("GradeTerrainPad",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
            // note: Resolve the current terrain grading overload, including its optional shared spatial sampler parameter.
            new[] { typeof(Terrain), typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(YQProceduralSettlementLayoutRecord), typeof(float), typeof(YQSpatialBlueprintTerrainSamplerV2) }, null);
        var grounding = typeof(YQCompiledWorldSiteInstance).GetMethod("TryResolveReviewedLandingDelta",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var data = new TerrainData { heightmapResolution = 257, size = new Vector3(128f, 40f, 128f) };
        var report = new StringBuilder("# Graded house placement\n\nSignature: " + manifest.SourceSignature + "\n");
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            // note: Activate the review instance before collecting visible bounds so nested authored renderers are published into the same hierarchy used by the render review.
            root.SetActive(true);
            // note: Use the same material/hierarchy repair as production publication before measuring the visible placement envelope.
            YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(root);
            // note: Create temporary terrain only after the authored hierarchy is materialized, matching the successful visual review order.
            var ground = Terrain.CreateTerrainGameObject(data);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(-64f, 0f, -64f);
            var terrain = ground.GetComponent<Terrain>();
            Bounds footprint = default;
            bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!found) { footprint = renderer.bounds; found = true; }
                else footprint.Encapsulate(renderer.bounds);
            }
            if (!found || grade == null || grounding == null) throw new InvalidOperationException("Missing placement boundary.");
            // note: Grade with the serialized production envelope, rejecting a stale envelope rather than silently replacing it in the test.
            var savedFootprint = new Bounds(manifest.Zones[0].localBoundsCenter, manifest.Zones[0].localBoundsSize);
            if (savedFootprint.min.x > footprint.min.x + .03f || savedFootprint.max.x < footprint.max.x - .03f ||
                savedFootprint.min.z > footprint.min.z + .03f || savedFootprint.max.z < footprint.max.z - .03f)
                throw new InvalidOperationException("Saved footprint omits candidate geometry.");
            footprint = savedFootprint;
            Vector3 origin = new Vector3(footprint.center.x, 0f, footprint.center.z);
            foreach (float slope in new[] { -.1f, -.03f, 0f, .03f, .1f })
                foreach (float yaw in new[] { 0f, 37f, 90f, 180f, 270f })
                {
                    // note: Each case starts from a fresh slope; prior grading must not make later placements trivially pass.
                    var heights = new float[257, 257];
                    for (int z = 0; z < 257; z++)
                        for (int x = 0; x < 257; x++) heights[z, x] = (15f + slope * (x * .5f - 64f)) / 40f;
                    data.SetHeights(0, 0, heights);
                    // note: Compare the stored heightfield before/after grading, excluding Unity's initial height quantization.
                    float untouchedHeight = data.GetHeights(0, 0, 1, 1)[0, 0];
                    var layout = new YQProceduralSettlementLayoutRecord { earthworkVersion = 1, origin = origin };
                    layout.cells.Add(new YQProceduralCellPlacement { boundsCenter = footprint.center, boundsSize = footprint.size });
                    if (streetLayout)
                    {
                        // note: Use the saved external connection, not the interior doorway, as the street builder's portal.
                        var sourceRoot = prefab.transform;
                        var portal = RequirePath(sourceRoot, manifest.Zones[0].connectionSocketPaths[0]);
                        var inputs = new List<YQProceduralSettlementLayout.Cell>();
                        for (int i = 0; i < 2; i++) inputs.Add(new YQProceduralSettlementLayout.Cell
                        {
                            id = "fixture-home-" + i, center = footprint.center, size = footprint.size,
                            entrance = sourceRoot.InverseTransformPoint(portal.position),
                            outward = sourceRoot.InverseTransformDirection(portal.forward), datum = manifest.Zones[0].authoredSourceOrigin.y
                        });
                        if (!YQProceduralSettlementLayout.TryBuild(inputs, YQProceduralSettlementLayout.SeedPrefix + "graded-" + slope + "-" + yaw,
                            out layout, out string layoutFailure)) throw new InvalidOperationException(layoutFailure);
                        // note: Exercise the candidate surface version explicitly until its parcel feasibility handling is production-ready.
                        if (experimentalEarthwork) layout.earthworkVersion = 2;
                    }
                    float outer = streetLayout ? layout.radius + 18f : 35f;
                    // note: Pass the optional spatial sampler explicitly so the review harness resolves the current production overload after terrain API evolution.
                    if (!(bool)grade.Invoke(null, new object[] { terrain, Vector3.zero, streetLayout ? layout.radius : 20f, outer, float.NegativeInfinity, layout, yaw, null }))
                        throw new InvalidOperationException("Parcel grading rejected slope=" + slope + " yaw=" + yaw);
                    data.SyncHeightmap();
                    if (streetLayout)
                    {
                        // note: Reload accepted parcel data onto the identical base heightfield and compare every stored terrain sample.
                        var acceptedTerrain = data.GetHeights(0, 0, 257, 257);
                        string acceptedJson = Newtonsoft.Json.JsonConvert.SerializeObject(layout);
                        layout = Newtonsoft.Json.JsonConvert.DeserializeObject<YQProceduralSettlementLayoutRecord>(acceptedJson);
                        data.SetHeights(0, 0, heights);
                        // note: Reuse the same current overload during replay; the saved layout comparison must exercise identical grading inputs.
                        if (!(bool)grade.Invoke(null, new object[] { terrain, Vector3.zero, layout.radius, outer, float.NegativeInfinity, layout, yaw, null }))
                            throw new InvalidOperationException("Saved street layout rejected on reload.");
                        data.SyncHeightmap();
                        if (Newtonsoft.Json.JsonConvert.SerializeObject(layout) != acceptedJson)
                            throw new InvalidOperationException("Saved parcel authority changed on reload.");
                        var replayTerrain = data.GetHeights(0, 0, 257, 257);
                        for (int z = 0; z < 257; z++)
                            for (int x = 0; x < 257; x++)
                                if (acceptedTerrain[z, x] != replayTerrain[z, x])
                                    throw new InvalidOperationException("Terrain replay differs at " + x + "," + z);
                    }
                    Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                    if (streetLayout)
                    {
                        // note: Sample both road edges and centre on final terrain; connected plan lines alone do not prove traversable grades.
                        float maximumGrade = 0f;
                        float maximumCrossfall = 0f;
                        string steepest = string.Empty;
                        foreach (var street in layout.streets)
                        {
                            Vector3 start = rotation * (street.start - layout.origin);
                            Vector3 end = rotation * (street.end - layout.origin);
                            Vector3 direction = (end - start).normalized;
                            Vector3 lateral = Vector3.Cross(Vector3.up, direction);
                            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / .25f));
                            // note: Cross-road grade is independent of longitudinal walking grade; measure the final surface across its full width.
                            for (int step = 0; step <= steps; step++)
                            {
                                Vector3 centre = Vector3.Lerp(start, end, step / (float)steps);
                                float left = terrain.SampleHeight(centre - lateral * street.width * .5f);
                                float right = terrain.SampleHeight(centre + lateral * street.width * .5f);
                                maximumCrossfall = Mathf.Max(maximumCrossfall, Mathf.Atan2(Mathf.Abs(right - left), street.width) * Mathf.Rad2Deg);
                            }
                            foreach (float offset in new[] { -.5f, 0f, .5f })
                            {
                                Vector3 prior = start + lateral * street.width * offset;
                                float previousHeight = terrain.SampleHeight(prior);
                                for (int step = 1; step <= steps; step++)
                                {
                                    Vector3 point = Vector3.Lerp(start, end, step / (float)steps) + lateral * street.width * offset;
                                    float height = terrain.SampleHeight(point);
                                    float measuredGrade = Mathf.Atan2(Mathf.Abs(height - previousHeight), Vector3.Distance(point, prior)) * Mathf.Rad2Deg;
                                    if (measuredGrade > maximumGrade)
                                    {
                                        // note: Retain the exact failing interval and road identity so a terrain fix targets the discontinuity rather than its aggregate score.
                                        maximumGrade = measuredGrade;
                                        steepest = " road=" + start + " -> " + end + " offset=" + offset +
                                            " interval=" + prior + " -> " + point + " heights=" + previousHeight + " -> " + height;
                                    }
                                    previousHeight = height;
                                    prior = point;
                                }
                            }
                        }
                        report.AppendLine("Street maximum longitudinal grade=" + maximumGrade + " slope=" + slope + " yaw=" + yaw + steepest);
                        report.AppendLine("Street maximum full-width crossfall=" + maximumCrossfall);
                        if (maximumCrossfall > contract.doorBindings[0].terrainApproach.maximumCrossSlopeDegrees || maximumGrade > 24f)
                        {
                            // note: The generated main spine is the final segment for this two-cell layout. Exclude its width from available pad-transition space.
                            var spine = layout.streets[layout.streets.Count - 1];
                            float firstCore = layout.cells[0].boundsSize.x * .5f + 3f;
                            float secondCore = layout.cells[1].boundsSize.x * .5f + 3f;
                            float coreGap = Mathf.Abs(layout.cells[0].boundsCenter.x - layout.cells[1].boundsCenter.x) - firstCore - secondCore;
                            float transitionRun = Mathf.Max(0f, coreGap - spine.width);
                            float crossAllowance = Mathf.Tan(contract.doorBindings[0].terrainApproach.maximumCrossSlopeDegrees * Mathf.Deg2Rad) * spine.width;
                            float climbAllowance = Mathf.Tan(24f * Mathf.Deg2Rad) * transitionRun;
                            float requiredRise = Mathf.Abs(layout.parcelGroundHeights[0] - layout.parcelGroundHeights[1]);
                            report.AppendLine("Two-parcel transverse feasibility: transition run=" + transitionRun +
                                " road width=" + spine.width + " required rise=" + requiredRise +
                                " allowed rise=" + (crossAllowance + climbAllowance) +
                                " (optimistic bound; excludes junction plateaus and easing).");
                        }
                        if (maximumCrossfall > contract.doorBindings[0].terrainApproach.maximumCrossSlopeDegrees)
                            throw new InvalidOperationException("Street crossfall exceeds reviewed approach allowance.");
                        if (maximumGrade > 24f)
                            foreach (var parcel in layout.cells)
                                report.AppendLine("Parcel centre=" + parcel.boundsCenter + " size=" + parcel.boundsSize +
                                    " height=" + layout.parcelGroundHeights[layout.cells.IndexOf(parcel)]);
                        if (maximumGrade > 24f) throw new InvalidOperationException("Graded street exceeds 24 degree walking approach limit.");
                    }
                    foreach (var placement in layout.cells)
                    {
                    root.transform.SetPositionAndRotation(streetLayout ? rotation * (placement.position - layout.origin) : -(rotation * origin) + Vector3.up * 5f,
                        rotation * Quaternion.Euler(0f, streetLayout ? placement.yaw : 0f, 0f));
                    var args = new object[] { root.transform, terrain, contract, 0f };
                    if (!(bool)grounding.Invoke(null, args)) throw new InvalidOperationException("Graded foundation rejected slope=" + slope + " yaw=" + yaw);
                    root.transform.position += Vector3.up * (float)args[3];
                    if (!YQTerrainApproachV2.TryValidateReviewedConnection(root.transform, contract.doorBindings[0].terrainApproach, terrain, out string failure))
                        throw new InvalidOperationException("Graded entrance rejected: " + failure);
                    }
                    if (data.GetHeights(0, 0, 1, 1)[0, 0] != untouchedHeight)
                        throw new InvalidOperationException("Grading changed unowned terrain.");
                    report.AppendLine("PASS slope=" + slope + " yaw=" + yaw + " parcel=" + layout.parcelGroundHeights[0]);
                }
            report.AppendLine(streetLayout ? "PASS 25 two-house street layouts. Fixture instances, not catalog selection or production approval." :
                "PASS 25 graded placements. Isolated single parcel, not a generated settlement or production approval.");
        }
        catch (Exception exception) { report.AppendLine("FAIL " + exception.Message); throw; }
        finally
        {
            File.WriteAllText(streetLayout ? "Logs/YQGradedJoistedStreet.md" : "Logs/YQGradedJoistedPlacement.md", report.ToString());
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    public static void RefreshJoistedFoundationContacts()
    {
        // note: Refresh only the unpublished candidate; preserve the existing asset GUID and never transfer stale approvals.
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + "/YQ_JoistedHomeCandidate.asset");
        if (manifest == null || manifest.ReleaseEligible || manifest.Zones.Count != 1)
            throw new InvalidOperationException("Expected one unpublished joisted candidate zone.");
        var zone = JsonUtility.FromJson<YQReviewedSemanticZoneRecord>(JsonUtility.ToJson(manifest.Zones[0]));
        if (zone.prefab == null || zone.cellContractsV2.Count != 1) throw new InvalidOperationException("Missing candidate geometry or contract.");
        string signature = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(zone.prefab)).ToString();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(zone.prefab, scene);
            var contacts = new List<YQFoundationTerrainContact>();
            // note: Added supports change the complete envelope; update the same metadata that production packing and grading consume.
            Bounds envelope = default;
            bool hasEnvelope = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasEnvelope) { envelope = renderer.bounds; hasEnvelope = true; }
                else envelope.Encapsulate(renderer.bounds);
            }
            if (!hasEnvelope) throw new InvalidOperationException("Missing candidate envelope.");
            zone.localBoundsCenter = root.transform.InverseTransformPoint(envelope.center);
            zone.localBoundsSize = envelope.size;
            foreach (Transform post in RequirePath(root.transform, "YQ_JoistedFoundation"))
            {
                if (!post.name.StartsWith("Post_", StringComparison.Ordinal)) continue;
                foreach (var renderer in post.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.name.EndsWith("LOD0", StringComparison.Ordinal)) continue;
                    Bounds bounds = renderer.bounds;
                    // note: Keep soil below the top joint; the point is a pending geometric proposal, not an automatic structural approval.
                    contacts.Add(new YQFoundationTerrainContact
                    {
                        supportPath = "YQ_JoistedFoundation/" + post.name,
                        localBottom = root.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)),
                        minimumEmbedDepth = 0f, maximumEmbedDepth = bounds.size.y - .1f
                    });
                }
            }
            if (contacts.Count != 9 || contacts.Exists(item => item.maximumEmbedDepth <= 0f))
                throw new InvalidOperationException("Incomplete candidate support geometry.");
            var contract = zone.cellContractsV2[0];
            contract.sourceSignature = signature;
            contract.reviewState = YQSemanticSiteReviewState.Pending;
            contract.reviewNote = "Measured floor fit and pending terrain contacts; production qualification incomplete.";
            contract.independentAssembly = new YQIndependentAssemblyContract
            {
                sourceSignature = signature, terrainContactVersion = 1, terrainContacts = contacts,
                reviewState = YQSemanticSiteReviewState.Pending
            };
            foreach (var door in contract.doorBindings)
            {
                door.reviewState = YQSemanticSiteReviewState.Pending;
                if (door.terrainApproach != null) door.terrainApproach.reviewState = YQSemanticSiteReviewState.Pending;
            }
            foreach (var loot in contract.lootBindings)
            {
                loot.sourceSignature = signature;
                loot.reviewState = YQSemanticSiteReviewState.Pending;
            }
            manifest.ConfigureCandidate(manifest.KitId, manifest.SemanticStyleKey, signature, manifest.Topology,
                manifest.StreamingSite, manifest.SourceInstanceCount, new[] { zone });
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/YQJoistedFoundationMetadata.txt", "Pending contacts=" + contacts.Count + "; signature=" + signature + "; releaseEligible=" + manifest.ReleaseEligible);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void VerifyHomeFunctionalAccess(GameObject candidate, string reportOverride = null, bool requireCurrentSignature = true)
    {
        // note: Recheck the actual street-connected candidate's furnished interior at runtime rotations using the existing player-sized collision probes.
        string manifestPath = Folder + "/YQ_StreetConnectedHomeCandidate.asset";
        if (candidate != null)
        {
            // note: Candidate-specific reviews must compare each changed prefab with its own pending manifest, never with a sibling geometry's dependency signature.
            string candidatePath = AssetDatabase.GetAssetPath(candidate);
            if (candidatePath.EndsWith("/yq_timber_foundation_house4.prefab", StringComparison.OrdinalIgnoreCase))
                manifestPath = Folder + "/YQ_TimberHomeCandidate.asset";
            else if (candidatePath.EndsWith("/yq_joisted_house4.prefab", StringComparison.OrdinalIgnoreCase))
                manifestPath = Folder + "/YQ_JoistedHomeCandidate.asset";
        }
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(manifestPath);
        if (manifest == null || manifest.Zones.Count != 1 || manifest.Zones[0].prefab == null)
            throw new InvalidOperationException("Street-connected home candidate is missing.");
        var zone = manifest.Zones[0];
        if (requireCurrentSignature && AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(zone.prefab)).ToString() != manifest.SourceSignature)
            throw new InvalidOperationException("Home dependency signature changed; re-review current geometry.");
        var report = new StringBuilder("# Home functional access\n\nStanding capsule: 1.8m high, 0.38m radius; 0.3m step limit. Offline collision evidence, not live player-controller certification.\n\n");
        // note: Record exact geometry identity while reusing only the unchanged door and room measurement definitions.
        GameObject reviewedPrefab = candidate != null ? candidate : zone.prefab;
        report.AppendLine("Prefab: " + AssetDatabase.GetAssetPath(reviewedPrefab) + "\nSignature: " +
            AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(reviewedPrefab)));
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            for (int rotation = 0; rotation < 4; rotation++)
            {
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(reviewedPrefab, scene);
                try
                {
                    copy.transform.SetPositionAndRotation(new Vector3(21, 7, -13), Quaternion.Euler(0, rotation * 90, 0));
                    Transform door = RequirePath(copy.transform, DoorPath);
                    using (var review = new YQCellPassageReviewV2(copy))
                    {
                        var passage = review.Measure(DoorPath);
                        var swing = review.MeasureSwing(DoorPath, 86f);
                        var room = review.MeasureFloorRegion(DoorPath, new Rect(-6.3f, -3.3f, 5.6f, 3.3f), -.16f,
                            new Vector3(-.7f, -.16f, -.603f), .35f, FurnitureRoot);
                        report.AppendLine("## Yaw " + rotation * 90 + "\n\nDoor: " + passage.Describe() +
                            "\nSwing: " + swing.Describe() + "\nRoom: " + room.Describe());
                        if (!passage.Clear || !swing.Clear || !room.Ready || room.walkable < room.samples / 3)
                            throw new InvalidOperationException("Real home doorway, swing or furnished-room access failed at yaw " + rotation * 90);
                        // note: Starting the same connected-floor test at each service socket proves it belongs to the entry's connected free-floor component.
                        foreach (string name in new[] { "SleepingAccess", "StorageAccess", "WorkAccess" })
                        {
                            Transform socket = RequirePath(copy.transform, SocketRoot + "/" + name);
                            var access = review.MeasureFloorRegion(DoorPath, new Rect(-6.3f, -3.3f, 5.6f, 3.3f), -.16f,
                                door.InverseTransformPoint(socket.position), .35f, FurnitureRoot);
                            report.AppendLine(name + ": " + access.Describe());
                            if (!access.Ready || access.reachable != room.reachable)
                                throw new InvalidOperationException(name + " is not connected to the usable room at yaw " + rotation * 90);
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
            }
            if (candidate == null) VerifyStreetConnectedHomeProviders();
            report.AppendLine("\nPASS: furnished room, sleeping/storage/work access, doorway and swing at four rotations. " +
                (candidate == null ? "Source provider and landing-datum checks passed. " : "Candidate provider rebinding and foundation certification remain pending. ") +
                "No asset approval or live player certification.");
        }
        catch (Exception error) { report.AppendLine("\nFAILED: " + error.Message); throw; }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(reportOverride ?? (candidate != null ? "Logs/YQTimberHomeFunctionalAccess.md" : "Logs/YQHomeFunctionalAccess.md"), report.ToString());
        }
    }

    [MenuItem("Tools/YourQuest/Testing/Build Playable Home Traversal Review")]
    public static void BuildPlayableHomeTraversalReview()
    {
        BuildPlayableHomeTraversalReview(false);
    }

    public static void BuildPlayableTimberHomeTraversalReview()
    {
        // note: Keep the original review scene intact while providing a production-motor fixture for the modified candidate.
        BuildPlayableHomeTraversalReview(true);
    }

    private static void BuildPlayableHomeTraversalReview(bool timber)
    {
        // note: Verify the real candidate before creating a separate review scene; existing scenes and candidate assets remain unchanged.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before creating the isolated review scene.");
        if (timber) { VerifyTimberHomeAccess(); VerifyTimberHomeProviders(); }
        else VerifyHomeFunctionalAccess();
        string scenePath = timber ? YQHomeTraversalReview.TimberScenePath : YQHomeTraversalReview.ScenePath;
        string terrainPath = Folder + (timber ? "/YQ_TimberHomeTraversalReviewTerrain.asset" : "/YQ_HomeTraversalReviewTerrain.asset");
        if (File.Exists(scenePath) || File.Exists(terrainPath))
            throw new InvalidOperationException("Review artifacts already exist; preserve them instead of overwriting local review work.");
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(Folder + (timber ? "/YQ_TimberHomeCandidate.asset" : "/YQ_StreetConnectedHomeCandidate.asset"));
        var zone = manifest.Zones[0];
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        // note: A fresh batch editor has an unsaved empty startup scene; interactive editors must retain their current scene.
        bool emptyBatchScene = Application.isBatchMode && string.IsNullOrEmpty(previous.path) && previous.rootCount == 0;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, emptyBatchScene ?
                UnityEditor.SceneManagement.NewSceneMode.Single : UnityEditor.SceneManagement.NewSceneMode.Additive);
        try
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var staging = new GameObject("Reviewed home — candidate, not published");
            staging.SetActive(false);
            var home = (GameObject)PrefabUtility.InstantiatePrefab(zone.prefab, staging.transform);
            // note: The existing reviewed lower landing defines zero height; moving the whole assembly preserves the repaired stairs and furnishings.
            var contact = RequirePath(home.transform, "YQ_V2_EntranceRepairs/SM_House4_Door6_StairApproach/StreetConnection");
            home.transform.position -= contact.position;
            var doorBinding = JsonUtility.FromJson<YQCellDoorBindingV2>(JsonUtility.ToJson(zone.cellContractsV2[0].doorBindings[0]));
            doorBinding.reviewState = YQSemanticSiteReviewState.Approved;
            if (!YQCellDoorBindingsV2.TryBind(home.transform, doorBinding, "editor-home-review", zone.stableId,
                    "Review door", "editor-review", out string failure))
                throw new InvalidOperationException(failure);
            home.GetComponentInChildren<YQLockpickableDoor>(true).locked = false;
            staging.SetActive(true);

            // note: A neutral flat support surface isolates stair/threshold behavior; this fixture does not certify procedural terrain aesthetics or slope integration.
            var data = new TerrainData { heightmapResolution = 65, size = new Vector3(64, 8, 64) };
            AssetDatabase.CreateAsset(data, terrainPath);
            var ground = Terrain.CreateTerrainGameObject(data);
            ground.name = "Diagnostic terrain — flat baseline";
            // note: Match the candidate's reviewed soil datum instead of burying its visible lower landing in the fixture.
            float soilY = timber ? home.transform.TransformPoint(zone.cellContractsV2[0].doorBindings[0].terrainApproach.localStart).y -
                zone.cellContractsV2[0].doorBindings[0].terrainApproach.walkingSurfaceAboveTerrain : -.02f;
            ground.transform.position = new Vector3(-32, soilY, -32);

            var player = new GameObject("Player");
            player.tag = "Player";
            Vector3 outward = contact.forward;
            outward.y = 0; outward.Normalize();
            player.transform.SetPositionAndRotation(outward * 3f + Vector3.up * .1f,
                Quaternion.LookRotation(-outward, Vector3.up));
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .38f;
            controller.center = new Vector3(0, .9f, 0); controller.minMoveDistance = 0;
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false); pivot.localPosition = new Vector3(0, 1.64f, .04f);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = .03f;
            camera.transform.SetPositionAndRotation(pivot.position, player.transform.rotation);
            var motor = player.AddComponent<YQInvestorPlayerMotor>();
            motor.cameraPivot = pivot; motor.playerCamera = camera; motor.firstPerson = true;
            var harness = new GameObject("Home review instructions").AddComponent<YQHomeTraversalReview>();
            harness.player = player.transform; harness.view = camera;
            var sun = new GameObject("Review sunlight", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 2f;
            sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath))
                throw new InvalidOperationException("Could not save home traversal review scene.");
            File.WriteAllText(timber ? "Logs/YQPlayableTimberHomeReview.txt" : "Logs/YQPlayableHomeReview.txt", "CREATED: " + scenePath +
                "\nOpen this scene alone, then Play. One production YQInvestorPlayerMotor, no save managers, existing door provider. Flat terrain baseline only. Live traversal remains unverified.");
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        }
    }

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Extract Accessible Home Cell")]
    public static void ExtractAccessibleHomeCell()
    {
        // note: Extract one measured house from a disposable copy, retaining nested source modules and repaired collision; no vendor or existing candidate is edited.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Home extraction requires Edit mode.");
        const string sourcePath = Folder + "/yq_eastern_accessible_furnishing_candidate.prefab";
        const string outputPath = Folder + "/yq_accessible_house4_cell.prefab";
        const string reportPath = "Logs/YQIndependentHomeExtraction.txt";
        if (File.Exists(outputPath)) throw new InvalidOperationException("Independent home already exists; preserve it for review.");
        var report = new StringBuilder("# Independent accessible home extraction\n\n");
        GameObject contents = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            Transform door = RequirePath(contents.transform, DoorPath);
            var keep = new HashSet<Transform> { door,
                RequirePath(contents.transform, FurnitureRoot), RequirePath(contents.transform, SocketRoot) };
            string[] modules = { "SM_House4_FrontWall", "SM_House4_BackWall", "SM_House4_SideWall", "SM_House4_Roof" };
            var counts = new int[modules.Length];
            // note: Exact source identities plus the already measured room neighborhood choose the structure. A tag or a similarly named prop is not sufficient.
            foreach (Transform child in contents.transform)
            {
                GameObject original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
                if (original == null) continue;
                string source = AssetDatabase.GetAssetPath(original);
                int kind = Array.FindIndex(modules, module => source == Viking + module + ".prefab");
                if (kind < 0) continue;
                Bounds bounds = VisibleBoundsInFrame(child, door);
                Vector3 center = bounds.center;
                if (center.x < -8f || center.x > 1f || center.z < -5f || center.z > 2f) continue;
                keep.Add(child);
                counts[kind]++;
                report.AppendLine("Selected " + child.name + ": source=" + source + "; centre=" + center.ToString("F3"));
            }
            for (int index = 0; index < modules.Length; index++)
            {
                report.AppendLine(modules[index] + " count=" + counts[index]);
                int expected = index == 2 ? 2 : 1;
                if (counts[index] != expected)
                    throw new InvalidOperationException("House ownership is ambiguous: expected " + expected + " " + modules[index] + ". No cell saved.");
            }
            Transform repairs = RequirePath(contents.transform, "YQ_V2_EntranceRepairs");
            keep.Add(repairs);
            // note: Unpack only the district wrapper so the new prefab does not inherit a whole donor district; nested imported modules retain their prefab links.
            if (PrefabUtility.IsPartOfPrefabInstance(contents))
                PrefabUtility.UnpackPrefabInstance(contents, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            var removed = new List<GameObject>();
            foreach (Transform child in contents.transform)
                if (!keep.Contains(child)) removed.Add(child.gameObject);
            foreach (var item in removed) UnityEngine.Object.DestroyImmediate(item);
            removed.Clear();
            foreach (Transform child in repairs)
                if (!child.name.StartsWith(DoorPath + "_", StringComparison.Ordinal)) removed.Add(child.gameObject);
            foreach (var item in removed) UnityEngine.Object.DestroyImmediate(item);
            if (repairs.childCount == 0) throw new InvalidOperationException("The home's repaired floor/approach modules are missing.");

            using (var review = new YQCellPassageReviewV2(contents))
            {
                var room = review.MeasureFloorRegion(DoorPath, new Rect(-6.3f, -3.3f, 5.6f, 3.3f), -.16f,
                    new Vector3(-.7f, -.16f, -.603f), .35f, FurnitureRoot);
                report.AppendLine("\nIsolated room: " + room.Describe());
                if (!room.Ready || room.walkable < room.samples / 3 ||
                    !review.Measure(DoorPath).Clear || !review.MeasureSwing(DoorPath, 86f).Clear)
                    throw new InvalidOperationException("Isolation removed required support or regressed doorway/room clearance.");
                foreach (Transform socket in RequirePath(contents.transform, SocketRoot))
                    if (!review.MeasureFloorRegion(DoorPath, new Rect(-6.3f, -3.3f, 5.6f, 3.3f), -.16f,
                            door.InverseTransformPoint(socket.position), .35f, FurnitureRoot).Ready)
                        throw new InvalidOperationException("Isolated home access disconnected: " + socket.name);
            }
            int renderers = contents.GetComponentsInChildren<Renderer>(true).Length;
            report.AppendLine("\nIsolated renderers=" + renderers + "; top-level roots=" + contents.transform.childCount);
            if (renderers == 0 || renderers > 100)
                throw new InvalidOperationException("Extracted home is empty or still contains district-sized geometry.");
            contents.name = "YQ_Accessible_House4_Cell";
            if (PrefabUtility.SaveAsPrefabAsset(contents, outputPath) == null)
                throw new InvalidOperationException("Could not save independent home candidate.");
            report.AppendLine("\nSAVED: " + outputPath + "\nPending: external terrain contact, complete roof/wall visual review and source-matched functional contract. No runtime activation.");
        }
        catch (Exception exception)
        {
            report.AppendLine("\nFAILED: " + exception.Message);
            throw;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(reportPath, report.ToString());
        }
    }

    private static Bounds VisibleBoundsInFrame(Transform item, Transform frame)
    {
        // note: Measure renderer envelopes in the existing door frame without loading meshes or changing authored scales/materials.
        Bounds result = default;
        bool found = false;
        foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
        {
            Bounds bounds = renderer.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = frame.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                else result.Encapsulate(point);
            }
        }
        if (!found) throw new InvalidOperationException("Structural module has no rendered geometry: " + item.name);
        return result;
    }

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Prepare Accessible Candidate Bindings")]
    public static void PrepareAccessibleCandidateBindings()
    {
        // note: Rebase draft evidence onto the repaired prefab; never reuse old chest/access poses or promote the entire district from one successful room test.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Candidate binding review requires Edit mode.");
        const string repairedPath = Folder + "/yq_eastern_accessible_furnishing_candidate.prefab";
        const string outputPath = Folder + "/YQ_AccessibleEasternCandidate.asset";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(repairedPath);
        var source = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(ManifestPath);
        if (prefab == null || source == null || source.ReleaseEligible || source.Zones.Count != 1)
            throw new InvalidOperationException("Expected the existing single-zone pending home and repaired prefab.");
        if (AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(outputPath) != null)
            throw new InvalidOperationException("Accessible candidate metadata already exists; preserve it for review.");
        string signature = AssetDatabase.GetAssetDependencyHash(repairedPath).ToString();
        var zone = JsonUtility.FromJson<YQReviewedSemanticZoneRecord>(JsonUtility.ToJson(source.Zones[0]));
        zone.prefab = prefab;
        var report = new StringBuilder("# Accessible home binding and material review\n\n");
        report.AppendLine("Prefab: " + repairedPath + "\nDependency hash: " + signature);
        foreach (var contract in zone.cellContractsV2)
        {
            contract.sourceSignature = signature;
            contract.reviewState = YQSemanticSiteReviewState.Pending;
            contract.reviewNote = "Rebased onto physically accessible furniture candidate; district access, services and rendered review remain pending.";
            contract.curation.EnsureCollections();
            foreach (var socket in contract.curation.sockets)
            {
                if (socket == null) continue;
                Transform target = RequirePath(prefab.transform, socket.transformPath);
                socket.localPosition = prefab.transform.InverseTransformPoint(target.position);
                socket.localRotation = Quaternion.Inverse(prefab.transform.rotation) * target.rotation;
                // note: The repaired room used the real 0.38m-radius standing probe, not the earlier narrower draft clearance.
                if (socket.transformPath.StartsWith(SocketRoot + "/", StringComparison.Ordinal))
                    socket.clearanceSize = new Vector3(.76f, 1.8f, .76f);
            }
            foreach (var loot in contract.lootBindings)
            {
                if (loot == null) continue;
                Transform target = RequirePath(prefab.transform, loot.targetPath);
                BoxCollider box = target.GetComponent<BoxCollider>();
                if (box == null) throw new InvalidOperationException("Reviewed storage collider is missing.");
                loot.sourceSignature = signature;
                loot.reviewState = YQSemanticSiteReviewState.Pending;
                loot.sourceLocalPosition = target.localPosition;
                loot.sourceLocalRotation = target.localRotation;
                loot.sourceLocalScale = target.localScale;
                loot.colliderCenter = box.center;
                loot.colliderSize = box.size;
                var access = contract.curation.sockets.Find(socket => socket != null &&
                    socket.socketId == loot.accessSocket.socketId);
                if (access == null) throw new InvalidOperationException("Storage access socket is missing.");
                loot.accessSocket = JsonUtility.FromJson<YQAssetSocketRecordV2>(JsonUtility.ToJson(access));
            }
        }
        var candidate = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        bool saved = false;
        try
        {
            candidate.ConfigureCandidate(source.KitId, source.SemanticStyleKey, signature, source.Topology,
                null, source.SourceInstanceCount, new[] { zone });
            // note: Exercise the real storage provider on inactive disposable staging; permanent metadata remains pending regardless of the test outcome.
            AddStorageBindingEvidence(candidate, prefab, report);
            int missing = 0, unsupported = 0, rendererCount = 0;
            var materials = new HashSet<Material>();
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                rendererCount++;
                Material[] slots = renderer.sharedMaterials;
                // note: Unity's missing-component wrappers do not obey CLR null-conditional semantics; particles legitimately have no MeshFilter.
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
                int required = mesh != null ? mesh.subMeshCount : 1;
                if (slots.Length < required)
                {
                    missing += required - slots.Length;
                    report.AppendLine("Missing submesh material slots: " + AnimationUtility.CalculateTransformPath(renderer.transform, prefab.transform));
                }
                foreach (Material material in slots)
                {
                    if (material == null) { missing++; continue; }
                    if (!materials.Add(material)) continue;
                    if (material.shader == null || !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader")
                    {
                        unsupported++;
                        report.AppendLine("Unsupported shader: " + AssetDatabase.GetAssetPath(material));
                    }
                }
            }
            report.AppendLine("\nRenderers=" + rendererCount + "; distinct materials=" + materials.Count +
                "; missing slots=" + missing + "; unsupported shaders=" + unsupported + ".");
            // note: Evidence reports problems without masking them with replacement materials or treating shader support as rendered approval.
            report.AppendLine("Material presence/support is not visual, lighting, LOD or terrain verification. No runtime catalog changes or contract approvals.");
            if (signature != AssetDatabase.GetAssetDependencyHash(repairedPath).ToString())
                throw new InvalidOperationException("Repaired prefab changed while evidence was prepared.");
            AssetDatabase.CreateAsset(candidate, outputPath);
            AssetDatabase.SaveAssetIfDirty(candidate);
            saved = true;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQAccessibleCandidateBindings.txt", report.ToString());
        }
        finally { if (!saved) UnityEngine.Object.DestroyImmediate(candidate); }
    }

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Repair Candidate Interior Accessibility")]
    public static void StartAccessibilityRepair()
    {
        // note: Run bounded furniture trials outside gameplay, yielding between trials rather than blocking the editor with the full search.
        if (EditorApplication.isPlayingOrWillChangePlaymode || accessibilityRepair != null)
            throw new InvalidOperationException("Accessibility repair requires idle Edit mode.");
        accessibilityRepair = RepairAccessibilityRoutine();
        EditorApplication.update += TickAccessibilityRepair;
        AssemblyReloadEvents.beforeAssemblyReload += StopAccessibilityRepair;
    }

    private static void StopAccessibilityRepair()
    {
        // note: Domain reload, play-mode entry and exceptions all release preview contents and event ownership.
        EditorApplication.update -= TickAccessibilityRepair;
        AssemblyReloadEvents.beforeAssemblyReload -= StopAccessibilityRepair;
        (accessibilityRepair as IDisposable)?.Dispose();
        accessibilityRepair = null;
    }

    private static void TickAccessibilityRepair()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || accessibilityRepair == null || !accessibilityRepair.MoveNext())
                StopAccessibilityRepair();
        }
        catch (Exception exception)
        {
            StopAccessibilityRepair();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQInteriorAccessibilityRepair.txt", "FAILED: " + exception);
            Debug.LogException(exception);
        }
    }

    private static System.Collections.IEnumerator RepairAccessibilityRoutine()
    {
        const string output = Folder + "/yq_eastern_accessible_furnishing_candidate.prefab";
        const string reportPath = "Logs/YQInteriorAccessibilityRepair.txt";
        if (File.Exists(output))
            throw new InvalidOperationException("An accessibility candidate already exists; preserve it for review: " + output);
        string signature = AssetDatabase.GetAssetDependencyHash(PrefabPath).ToString();
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        var report = new StringBuilder("# Measured interior accessibility repair\n\n");
        try
        {
            Transform door = RequirePath(contents.transform, DoorPath);
            Transform chest = RequirePath(contents.transform, FurnitureRoot + "/Personal_Storage");
            Transform table = RequirePath(contents.transform, FurnitureRoot + "/Work_Table");
            Transform chair = RequirePath(contents.transform, FurnitureRoot + "/Work_Chair");
            Transform storageAccess = RequirePath(contents.transform, SocketRoot + "/StorageAccess");
            Transform workAccess = RequirePath(contents.transform, SocketRoot + "/WorkAccess");
            Transform furniture = RequirePath(contents.transform, FurnitureRoot);
            Transform sockets = RequirePath(contents.transform, SocketRoot);
            Transform[] moved = { chest, storageAccess, table, chair, workAccess };
            var originalPositions = new Vector3[moved.Length];
            for (int i = 0; i < moved.Length; i++) originalPositions[i] = moved[i].position;
            float[] storageShifts = { 0f, -.35f, -.6f, .45f };
            float[] workShifts = { 0f, -.25f, .35f };
            int trial = 0;
            foreach (float storageShift in storageShifts)
                foreach (float workShift in workShifts)
                {
                    // note: Change physical furniture and its access point together; never hide blockers, shrink the player or declare disconnected pockets acceptable.
                    for (int i = 0; i < moved.Length; i++)
                        moved[i].position = originalPositions[i] + door.TransformVector(
                            i < 2 ? new Vector3(storageShift, 0f, 0f) : new Vector3(0f, 0f, workShift));
                    bool ready;
                    using (var review = new YQCellPassageReviewV2(contents))
                    {
                        var room = review.MeasureFloorRegion(DoorPath, new Rect(-6.3f, -3.3f, 5.6f, 3.3f), -.16f,
                            new Vector3(-.7f, -.16f, -.603f), .35f, FurnitureRoot);
                        report.AppendLine("Trial " + (++trial) + ": storage=" + storageShift + ", work=" + workShift + ": " + room.Describe());
                        ready = room.Ready && room.walkable >= room.samples / 3;
                        if (ready)
                            foreach (Transform item in furniture)
                            {
                                Bounds bounds = MeasureInFrame(item.gameObject, door);
                                var support = review.MeasureFloorRegion(DoorPath,
                                    Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z), -.16f,
                                    new Vector3(-.7f, -.16f, -.603f), .3f, FurnitureRoot);
                                ready &= review.CheckObjectPenetration(FurnitureRoot + "/" + item.name).Length == 0 &&
                                    support.samples > 0 && support.supported == support.samples;
                            }
                        if (ready)
                            foreach (Transform socket in sockets)
                            {
                                Vector3 point = door.InverseTransformPoint(socket.position);
                                // note: Include the whole supported room and require each declared access point to connect to its entrance component.
                                var access = review.MeasureFloorRegion(DoorPath, new Rect(-6.3f, -3.3f, 5.6f, 3.3f), -.16f,
                                    point, .35f, FurnitureRoot);
                                ready &= access.Ready && access.reachable == room.reachable;
                            }
                        if (ready)
                            ready = review.Measure(DoorPath).Clear && review.MeasureSwing(DoorPath, 86f).Clear;
                    }
                    if (ready)
                    {
                        // note: Save a distinct project-owned candidate only if all physical checks pass and the source has not changed while trials yielded.
                        if (signature != AssetDatabase.GetAssetDependencyHash(PrefabPath).ToString() || File.Exists(output))
                            throw new InvalidOperationException("Candidate source or output changed during repair; no overwrite is permitted.");
                        if (PrefabUtility.SaveAsPrefabAsset(contents, output) == null)
                            throw new InvalidOperationException("Could not save the measured accessibility candidate.");
                        report.AppendLine("\nPHYSICAL CHECKS PASSED: " + output + "\nSource hash: " + signature +
                            "\nPending: material/terrain/runtime provider review. No catalog or approval flags changed.");
                        Directory.CreateDirectory("Logs");
                        File.WriteAllText(reportPath, report.ToString());
                        yield break;
                    }
                    yield return null;
                }
            report.AppendLine("\nNO PASSING ARRANGEMENT: original prefab and all runtime bindings preserved. Further room-layout repair is required.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(reportPath, report.ToString());
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    // note: Re-measure the existing candidate without invoking Build, saving assets or approving a contract.
    public static string InspectExistingCandidate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Candidate measurement requires Edit Mode.");
        GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (candidate == null)
            return "NOT MEASURED: existing eastern home candidate is missing at " + PrefabPath;
        var report = new StringBuilder("# Existing eastern home — read-only collision review\n\n");
        report.AppendLine("Source: " + PrefabPath);
        report.AppendLine("Standing capsule: 1.80m tall, 0.38m radius. Conservative 0.30m step and 45-degree support limits. No skin reduction.");
        using (var review = new YQCellPassageReviewV2(candidate))
        {
            report.AppendLine("\nDoor at source pose: " + review.Measure(DoorPath).Describe());
            // note: Reuse the original furnished-room footprint and entry point, not a new synthetic floor or guessed destination.
            var room = review.MeasureFloorRegion(DoorPath, new Rect(-6.3f, -3.3f, 5.6f, 3.3f), -0.16f,
                new Vector3(-0.7f, -0.16f, -0.603f), 0.35f, FurnitureRoot);
            report.AppendLine("\nFurnished interior: " + room.Describe());
        }
        report.AppendLine("\nNOT CERTIFIED: source-pose doorway and room evidence only. Door swing, resident sockets, external terrain/frontage, rendered materials and actual controller traversal still require verification. Existing approval flags were not changed.");
        return report.ToString();
    }

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Furnish Eastern Candidate Interior")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Candidate furnishing requires Edit Mode.");
        var source = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(YQSiteFunctionContractReviewV2.RuntimePath);
        YQReviewedSemanticZoneRecord sourceZone = null;
        if (source != null) foreach (var zone in source.Zones) if (zone != null && zone.stableId == ZoneId) sourceZone = zone;
        if (sourceZone == null || PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) != PrefabAssetType.Variant)
            throw new InvalidOperationException("The reviewed source zone and fitted eastern candidate are required.");
        StringBuilder report = new StringBuilder("# Furnished eastern candidate interior\n\n");
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform door = RequirePath(contents.transform, DoorPath);
            bool changed = false;
            Transform furniture = EnsureRoot(contents.transform, FurnitureRoot, ref changed);
            Transform sockets = EnsureRoot(contents.transform, SocketRoot, ref changed);
            // note: Explicit measured room coordinates preserve the main entrance aisle and place storage beside the sleeping area.
            Place(furniture, door, "Sleeping_Bed", "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Bed.prefab",
                new Vector2(-4.9f, -2.4f), ref changed, report);
            Place(furniture, door, "Work_Table", Viking + "SM_WoodentableSimple.prefab",
                new Vector2(-2.05f, -2.6f), ref changed, report);
            Place(furniture, door, "Work_Chair", Viking + "SM_WoodenMiniChair.prefab",
                new Vector2(-2.05f, -1.25f), ref changed, report);
            Place(furniture, door, "Personal_Storage", Viking + "SM_WoodenCurveChest_Closed.prefab",
                new Vector2(-5.6f, -0.5f), ref changed, report);
            PlaceSocket(sockets, door, "SleepingAccess", new Vector3(-4.4f, -0.16f, -0.65f), ref changed);
            PlaceSocket(sockets, door, "StorageAccess", new Vector3(-4.65f, -0.16f, -0.5f), ref changed);
            PlaceSocket(sockets, door, "WorkAccess", new Vector3(-1.05f, -0.16f, -2.45f), ref changed);

            using (var review = new YQCellPassageReviewV2(contents))
            {
                var room = review.MeasureFloorRegion(DoorPath, new Rect(-6.3f, -3.3f, 5.6f, 3.3f), -0.16f,
                    new Vector3(-0.7f, -0.16f, -0.603f), 0.35f, FurnitureRoot);
                report.AppendLine("\nFurnished room: " + room.Describe());
                if (!room.Ready || room.walkable < room.samples / 3)
                    throw new InvalidOperationException("Furnishings leave unsupported, disconnected or insufficient walkable space.");
                foreach (Transform item in furniture)
                {
                    string collisionFailure = review.CheckObjectPenetration(FurnitureRoot + "/" + item.name);
                    if (collisionFailure.Length != 0) throw new InvalidOperationException(collisionFailure);
                    Bounds footprint = MeasureInFrame(item.gameObject, door);
                    var support = review.MeasureFloorRegion(DoorPath,
                        Rect.MinMaxRect(footprint.min.x, footprint.min.z, footprint.max.x, footprint.max.z),
                        -0.16f, new Vector3(-0.7f, -0.16f, -0.603f), 0.3f, FurnitureRoot);
                    report.AppendLine("- " + item.name + " footprint support=" + support.supported + "/" + support.samples);
                    if (support.samples == 0 || support.supported != support.samples)
                        throw new InvalidOperationException("Furnishing extends beyond supported floor: " + item.name);
                }
                foreach (Transform socket in sockets)
                {
                    Vector3 local = door.InverseTransformPoint(socket.position);
                    var access = review.MeasureFloorRegion(DoorPath,
                        new Rect(local.x - 0.1f, local.z - 0.1f, 0.2f, 0.2f), local.y, local, 0.2f, FurnitureRoot);
                    report.AppendLine("- " + socket.name + ": " + access.Describe());
                    if (!access.Ready) throw new InvalidOperationException("Furnishing access is obstructed: " + socket.name);
                }
                foreach (var contract in sourceZone.cellContractsV2)
                    foreach (var binding in contract.doorBindings)
                    {
                        if (binding == null) continue;
                        float angle = binding.targetPath.StartsWith("SM_House2_", StringComparison.Ordinal) ? -86f : 86f;
                        if (!review.Measure(binding.targetPath).Clear || !review.MeasureSwing(binding.targetPath, angle).Clear)
                            throw new InvalidOperationException("Furnishing changed a reviewed entrance: " + binding.targetPath);
                    }
            }
            if (changed && PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath) == null)
                throw new InvalidOperationException("Could not save the furnished candidate.");
            BuildCandidateManifest(source, sourceZone, report);
            report.AppendLine("\nCandidate furnishings, access geometry and storage provider binding passed. Candidate remains pending: verify source materials visually, actual terrain approaches and remaining service providers before runtime promotion.");
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log("[YQCandidateInteriorV2] Furnished room and door bindings verified. " + ReportPath);
        }
        catch (Exception exception)
        {
            report.AppendLine("\nBuild stopped: " + exception.Message);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, report.ToString());
            throw;
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static Transform RequirePath(Transform root, string path)
    {
        if (!YQCellDoorBindingsV2.TryResolveUniquePath(root, path, out Transform found))
            throw new InvalidOperationException("Missing or ambiguous candidate path: " + path);
        return found;
    }

    private static Transform EnsureRoot(Transform root, string name, ref bool changed)
    {
        Transform existing = root.Find(name);
        if (existing != null) return RequirePath(root, name);
        Transform created = new GameObject(name).transform;
        created.SetParent(root, false);
        changed = true;
        return created;
    }

    private static void Place(Transform parent, Transform door, string name, string path, Vector2 center,
        ref bool changed, StringBuilder report)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) throw new InvalidOperationException("Missing approved library candidate: " + path);
        Transform existing = parent.Find(name);
        GameObject item = existing != null ? RequirePath(parent, name).gameObject :
            (GameObject)PrefabUtility.InstantiatePrefab(source, parent.gameObject.scene);
        if (existing == null)
        {
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.rotation = door.rotation;
            item.transform.localScale = source.transform.localScale;
            changed = true;
        }
        else if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(item) != source)
            throw new InvalidOperationException("Existing furnishing source changed: " + name);
        // note: Imported geometry and authored scale determine floor contact; no collider resizing or visual material replacement occurs.
        Bounds measured = MeasureInFrame(item, door);
        Vector3 delta = new Vector3(center.x - measured.center.x, -0.16f - measured.min.y, center.y - measured.center.z);
        if (existing != null && delta.sqrMagnitude > 0.00001f)
            throw new InvalidOperationException("Existing furnishing was moved; preserve it for review: " + name);
        if (existing == null) item.transform.position += door.TransformVector(delta);
        report.AppendLine("- " + name + ": " + path + "; measured size=" + measured.size.ToString("F3"));
    }

    private static Bounds MeasureInFrame(GameObject item, Transform frame)
    {
        bool any = false;
        Bounds bounds = default;
        foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterials.Length == 0)
                throw new InvalidOperationException("Furnishing lacks source materials: " + item.name);
            foreach (Material material in renderer.sharedMaterials)
                if (material == null) throw new InvalidOperationException("Missing source material: " + item.name);
            Bounds mesh = filter.sharedMesh.bounds;
            for (int index = 0; index < 8; index++)
            {
                Vector3 local = mesh.center + Vector3.Scale(mesh.extents, new Vector3(
                    (index & 1) == 0 ? -1 : 1, (index & 2) == 0 ? -1 : 1, (index & 4) == 0 ? -1 : 1));
                Vector3 point = frame.InverseTransformPoint(filter.transform.TransformPoint(local));
                if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                else bounds.Encapsulate(point);
            }
        }
        if (!any || item.GetComponentsInChildren<Collider>(true).Length == 0)
            throw new InvalidOperationException("Furnishing requires visible geometry and authored collision: " + item.name);
        return bounds;
    }

    private static void PlaceSocket(Transform root, Transform frame, string name, Vector3 point, ref bool changed)
    {
        Transform existing = root.Find(name);
        Vector3 position = frame.TransformPoint(point);
        if (existing != null)
        {
            if ((RequirePath(root, name).position - position).sqrMagnitude > 0.00001f)
                throw new InvalidOperationException("Existing access socket was moved: " + name);
            return;
        }
        Transform socket = new GameObject(name).transform;
        socket.SetParent(root, false);
        socket.SetPositionAndRotation(position, frame.rotation);
        changed = true;
    }

    private static void BuildCandidateManifest(YQReviewedSemanticSiteManifest source,
        YQReviewedSemanticZoneRecord sourceZone, StringBuilder report)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        string signature = AssetDatabase.GetAssetDependencyHash(PrefabPath).ToString();
        var candidate = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(ManifestPath);
        if (candidate != null)
        {
            if (candidate.SourceSignature != signature || candidate.ReleaseEligible)
                throw new InvalidOperationException("Existing candidate metadata differs; preserve it for explicit review.");
            bool changed = AddInteriorSocketEvidence(candidate, prefab);
            changed |= AddStorageBindingEvidence(candidate, prefab, report);
            // note: The pending source copy predates these four authored items; keep its per-zone cost consistent with the candidate manifest's total.
            foreach (var existingZone in candidate.Zones)
                if (existingZone.stableId == sourceZone.stableId && existingZone.sourceInstanceCount == sourceZone.sourceInstanceCount)
                {
                    existingZone.sourceInstanceCount += 4;
                    existingZone.authoredDressingCount += 4;
                    changed = true;
                }
            if (changed) { EditorUtility.SetDirty(candidate); AssetDatabase.SaveAssetIfDirty(candidate); }
            return;
        }
        // note: Clone only structured source metadata; the candidate points to its actual repaired prefab and exact dependency revision.
        var zone = JsonUtility.FromJson<YQReviewedSemanticZoneRecord>(JsonUtility.ToJson(sourceZone));
        zone.prefab = prefab;
        foreach (var contract in zone.cellContractsV2)
        {
            contract.reviewState = YQSemanticSiteReviewState.Pending;
            contract.sourceSignature = signature;
            contract.reviewNote = "Furnished candidate geometry verified; terrain access and functional providers remain pending.";
            foreach (var binding in contract.doorBindings)
            {
                Transform leaf = RequirePath(prefab.transform, binding.targetPath);
                binding.sourceLocalPosition = leaf.localPosition;
                binding.sourceLocalRotation = leaf.localRotation;
                binding.sourceLocalScale = leaf.localScale;
                binding.openEuler = new Vector3(0, binding.targetPath.StartsWith("SM_House2_", StringComparison.Ordinal) ? -86 : 86, 0);
                binding.passageVerified = true;
                binding.reviewState = YQSemanticSiteReviewState.Pending;
                binding.reviewNote = "Candidate passage and swing verified; terrain approach and visual review pending.";
                // note: Exercise the existing provider against an inactive disposable instance; no authored fixed persistence ID is baked into the prefab.
                var verified = JsonUtility.FromJson<YQCellDoorBindingV2>(JsonUtility.ToJson(binding));
                verified.reviewState = YQSemanticSiteReviewState.Approved;
                GameObject staging = new GameObject("DoorBindingReview");
                staging.SetActive(false);
                try
                {
                    GameObject copy = UnityEngine.Object.Instantiate(prefab, staging.transform);
                    if (!YQCellDoorBindingsV2.TryBind(copy.transform, verified, "candidate-review", zone.stableId,
                            "", "", out string failure)) throw new InvalidOperationException(failure);
                }
                finally { UnityEngine.Object.DestroyImmediate(staging); }
            }
        }
        candidate = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        candidate.ConfigureCandidate(source.KitId, source.SemanticStyleKey, signature, source.Topology,
            null, sourceZone.sourceInstanceCount + 4, new[] { zone });
        zone.sourceInstanceCount += 4;
        zone.authoredDressingCount += 4;
        AddInteriorSocketEvidence(candidate, prefab);
        AddStorageBindingEvidence(candidate, prefab, report);
        AssetDatabase.CreateAsset(candidate, ManifestPath);
        AssetDatabase.SaveAssetIfDirty(candidate);
        report.AppendLine("\nCreated pending candidate manifest with fitted door poses and verified opening directions: " + ManifestPath);
    }

    private static bool AddInteriorSocketEvidence(YQReviewedSemanticSiteManifest candidate, GameObject prefab)
    {
        bool changed = false;
        foreach (var zone in candidate.Zones)
            foreach (var contract in zone.cellContractsV2)
            {
                if (contract.reviewState != YQSemanticSiteReviewState.Pending)
                    throw new InvalidOperationException("Preserve reviewed cell metadata during candidate authoring.");
                contract.curation.EnsureCollections();
                foreach (string name in new[] { "SleepingAccess", "StorageAccess", "WorkAccess" })
                {
                    string path = SocketRoot + "/" + name;
                    Transform socket = RequirePath(prefab.transform, path);
                    string id = ZoneId + ":" + path;
                    Vector3 position = prefab.transform.InverseTransformPoint(socket.position);
                    Quaternion rotation = Quaternion.Inverse(prefab.transform.rotation) * socket.rotation;
                    var existing = contract.curation.sockets.Find(item => item != null && item.socketId == id);
                    if (existing != null)
                    {
                        if (existing.transformPath != path || (existing.localPosition - position).sqrMagnitude > 0.00001f ||
                            Quaternion.Angle(existing.localRotation, rotation) > 0.01f)
                            throw new InvalidOperationException("An existing furnishing socket was edited; preserve it.");
                        continue;
                    }
                    // note: Typed access evidence records measured geometry only. A work table or chest does not automatically become a runtime service/reward provider.
                    contract.curation.sockets.Add(new YQAssetSocketRecordV2
                    {
                        socketId = id, transformPath = path,
                        kind = name == "SleepingAccess" ? YQAssetSocketKindV2.Furnishing : YQAssetSocketKindV2.Interaction,
                        localPosition = position, localRotation = rotation,
                        clearanceSize = new Vector3(0.6f, 1.8f, 0.6f),
                        compatibilityKey = name == "SleepingAccess" ? "sleeping_access" : name == "StorageAccess" ? "storage_access" : "work_access"
                    });
                    changed = true;
                }
            }
        return changed;
    }

    private static bool AddStorageBindingEvidence(YQReviewedSemanticSiteManifest candidate, GameObject prefab, StringBuilder report)
    {
        const string bindingId = "personal_storage";
        string targetPath = FurnitureRoot + "/Personal_Storage";
        Transform target = RequirePath(prefab.transform, targetPath);
        BoxCollider collider = target.GetComponent<BoxCollider>();
        GameObject original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(target.gameObject);
        if (collider == null || original == null)
            throw new InvalidOperationException("Storage requires its source-linked chest and reviewed authored box.");
        bool changed = false;
        foreach (var zone in candidate.Zones)
            foreach (var contract in zone.cellContractsV2)
            {
                contract.lootBindings ??= new List<YQCellLootBindingV2>();
                var binding = contract.lootBindings.Find(item => item != null && item.bindingId == bindingId);
                if (binding == null)
                {
                    var socket = contract.curation.sockets.Find(item => item != null && item.transformPath == SocketRoot + "/StorageAccess");
                    if (socket == null) throw new InvalidOperationException("Measured storage access must exist before provider binding.");
                    binding = new YQCellLootBindingV2
                    {
                        bindingId = bindingId, targetPath = targetPath,
                        sourceSignature = candidate.SourceSignature,
                        sourcePrefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original)),
                        sourceLocalPosition = target.localPosition, sourceLocalRotation = target.localRotation,
                        sourceLocalScale = target.localScale, colliderCenter = collider.center, colliderSize = collider.size,
                        accessSocket = JsonUtility.FromJson<YQAssetSocketRecordV2>(JsonUtility.ToJson(socket)),
                        reviewState = YQSemanticSiteReviewState.Pending, purpose = YQCellLootPurposeV2.HouseholdStorage
                    };
                    contract.lootBindings.Add(binding);
                    changed = true;
                }
                // note: Candidate approval remains pending; exercise the production binder on an inactive disposable copy with an explicit temporary identity.
                var verified = JsonUtility.FromJson<YQCellLootBindingV2>(JsonUtility.ToJson(binding));
                verified.reviewState = YQSemanticSiteReviewState.Approved;
                GameObject staging = new GameObject("StorageBindingReview");
                staging.SetActive(false);
                try
                {
                    var copy = UnityEngine.Object.Instantiate(prefab, staging.transform);
                    if (!YQCellLootBindingsV2.TryBind(copy.transform, verified, candidate.SourceSignature,
                        "candidate-review", zone.stableId, "Candidate storage", "candidate-region", 1, out string failure))
                        throw new InvalidOperationException("Candidate storage binder rejected: " + failure);
                    Transform bound = RequirePath(copy.transform, targetPath);
                    BoxCollider boundBox = bound.GetComponent<BoxCollider>();
                    var provider = bound.GetComponent<YQLockpickableLoot>();
                    // note: Execute the real initialization path while staging stays inactive to verify no generic collision envelope is installed.
                    provider.SendMessage("Awake", SendMessageOptions.RequireReceiver);
                    if (boundBox.center != collider.center || boundBox.size != collider.size ||
                        bound.GetComponents<Collider>().Length != target.GetComponents<Collider>().Length)
                        throw new InvalidOperationException("Storage initialization changed the reviewed chest collision.");
                    report.AppendLine("\nStorage provider verified: stable site/cell/slot identity; authored collider retained; pending binding saved.");
                }
                finally { UnityEngine.Object.DestroyImmediate(staging); }
            }
        return changed;
    }

    public static void RunBatch()
    {
        try
        {
            int failures = YQWorldGenerationV2ContractTests.RunTests(out int tested);
            Debug.Log("[YQWorldGenV2Tests] Tested " + tested + " contracts; failures=" + failures + ".");
            if (failures != 0) { EditorApplication.Exit(1); return; }
            Build();
            string prefabSnapshot = File.ReadAllText(PrefabPath), manifestSnapshot = File.ReadAllText(ManifestPath);
            string report = File.ReadAllText(ReportPath);
            // note: Prior door/floor repair tools must remain safe and repeatable after furnishing is introduced.
            YQSiteFunctionContractReviewV2.CompleteCandidateRoomFloors();
            YQSiteFunctionContractReviewV2.RepairSmallHouseCandidateWallCollision();
            Build();
            if (prefabSnapshot != File.ReadAllText(PrefabPath) || manifestSnapshot != File.ReadAllText(ManifestPath))
                throw new InvalidOperationException("Repeated interior authoring changed saved assets.");
            File.WriteAllText(ReportPath, report + "\nRepeat verification: prefab and pending manifest are byte-for-byte unchanged.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
}
