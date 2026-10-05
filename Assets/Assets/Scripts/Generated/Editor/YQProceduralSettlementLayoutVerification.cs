#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// note: Focused generation tests do not enter Play mode, import new packs or alter a user's saved world.
public static class YQProceduralSettlementLayoutVerification
{
    [MenuItem("Tools/YourQuest/Testing/Verify Foundation Contact Solver")]
    public static void VerifyFoundationContactSolver()
    {
        // note: Exercise the runtime solver without touching terrain, scene objects or the saved world.
        var method = typeof(YQCompiledWorldSiteInstance).GetMethod("TryResolveCompiledFoundationCorrection",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(List<Vector2>), typeof(float).MakeByRefType(), typeof(float).MakeByRefType() }, null);
        if (method == null) throw new InvalidOperationException("Foundation contact solver is missing.");
        object[] feasible = { new List<Vector2> { new Vector2(0f, 70f), new Vector2(.75f, 30f) }, 0f, 0f };
        if (!(bool)method.Invoke(null, feasible) || (float)feasible[2] < .999f ||
            (float)feasible[1] < .10f || (float)feasible[1] > .18f)
            throw new InvalidOperationException("A feasible asymmetric contact interval was rejected.");
        // note: Widely separated buildings must still fail; the fix changes the search, never the acceptance limits.
        object[] incompatible = { new List<Vector2> { new Vector2(0f, 50f), new Vector2(2f, 50f) }, 0f, 0f };
        if ((bool)method.Invoke(null, incompatible))
            throw new InvalidOperationException("Unsupported secondary structure was accepted.");
        object[] invalid = { new List<Vector2> { new Vector2(float.NaN, 1f) }, 0f, 0f };
        if ((bool)method.Invoke(null, invalid))
            throw new InvalidOperationException("Nonfinite contact was accepted.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/YQFoundationContactSolver.txt", "PASS: feasible asymmetric interval, incompatible structures and nonfinite contacts.");
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Parcel Terrain Writes")]
    public static void VerifyParcelTerrainWrites()
    {
        // note: Use real TerrainData in an isolated preview scene; never change the active world, terrain assets, or player save.
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var data = new TerrainData { heightmapResolution = 129, size = new Vector3(128f, 40f, 128f) };
        var report = new System.Text.StringBuilder("# Parcel terrain write verification\n\n");
        try
        {
            var terrainObject = Terrain.CreateTerrainGameObject(data);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(terrainObject, scene);
            terrainObject.transform.position = new Vector3(-64f, 12f, -64f);
            var terrain = terrainObject.GetComponent<Terrain>();
            var grade = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("GradeTerrainPad",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
                new[] { typeof(Terrain), typeof(Vector3), typeof(float), typeof(float), typeof(float),
                    typeof(YQProceduralSettlementLayoutRecord), typeof(float), typeof(YQSpatialBlueprintTerrainSamplerV2) }, null);
            Require(grade != null, "The production parcel grading overload is missing.");
            // note: Oblique footprints exercise real rotated terrain sampling rather than only axis-aligned quarter turns.
            foreach (float heading in new[] { 0f, 15f, 37f, 90f, 123f, 180f, 270f })
            {
                var source = new float[129, 129];
                for (int z = 0; z < 129; z++)
                    for (int x = 0; x < 129; x++) source[z, x] = .25f + x * .1f / 40f;
                data.SetHeights(0, 0, source);
                var layout = new YQProceduralSettlementLayoutRecord { earthworkVersion = 3 };
                layout.cells.Add(new YQProceduralCellPlacement { boundsCenter = new Vector3(-20f, 0f, 0f), boundsSize = Vector3.one * 4f });
                layout.cells.Add(new YQProceduralCellPlacement { boundsCenter = new Vector3(20f, 0f, 0f), boundsSize = Vector3.one * 4f });
                layout.streets.Add(new YQProceduralStreet { start = new Vector3(-20f, 0f, 0f), end = new Vector3(20f, 0f, 0f), width = 4f });
                Func<bool> apply = () => (bool)grade.Invoke(null, new object[] { terrain, Vector3.zero, 35f, 50f,
                    float.NegativeInfinity, layout, heading, null });
                Require(apply(), "Parcel grading rejected valid terrain.");
                data.SyncHeightmap();
                Require(layout.parcelGroundHeights.Count == 2, "Successful grading did not retain elevations.");
                for (int i = 0; i < 2; i++)
                {
                    Vector3 point = Quaternion.Euler(0f, heading, 0f) * layout.cells[i].boundsCenter;
                    Require(Mathf.Abs(terrain.SampleHeight(point) + 12f - layout.parcelGroundHeights[i]) < .02f,
                        "Actual terrain does not meet its saved parcel plane.");
                }
                Require(Mathf.Abs(data.GetHeights(64, 104, 1, 1)[0, 0] - source[104, 64]) < .0001f,
                    "Unowned terrain changed.");
                // note: Reapply the saved profile to a different base surface; accepted parcel levels must not drift on reload.
                layout = Newtonsoft.Json.JsonConvert.DeserializeObject<YQProceduralSettlementLayoutRecord>(
                    Newtonsoft.Json.JsonConvert.SerializeObject(layout));
                float saved = layout.parcelGroundHeights[0];
                for (int z = 0; z < 129; z++)
                    for (int x = 0; x < 129; x++) source[z, x] += .02f;
                data.SetHeights(0, 0, source);
                Require(apply(), "Reloaded profile rejected.");
                data.SyncHeightmap();
                Vector3 first = Quaternion.Euler(0f, heading, 0f) * layout.cells[0].boundsCenter;
                Require(Mathf.Abs(terrain.SampleHeight(first) + 12f - saved) < .02f, "Reloaded parcel elevation drifted.");
                // note: Unsupported versions must reject atomically, before any heightmap write.
                layout.earthworkVersion = 99;
                float before = data.GetHeights(64, 64, 1, 1)[0, 0];
                Require(!apply() && data.GetHeights(64, 64, 1, 1)[0, 0] == before, "Invalid profile changed terrain.");
                // note: Saved elevations must not bypass missing-ground rejection at a parcel's actual rotated position.
                layout.earthworkVersion = 3;
                int holeX = Mathf.FloorToInt(first.x + 64f), holeZ = Mathf.FloorToInt(first.z + 64f);
                data.SetHoles(holeX, holeZ, new bool[,] { { false } });
                Require(!apply() && data.GetHeights(64, 64, 1, 1)[0, 0] == before, "Terrain hole accepted or changed terrain.");
                data.SetHoles(holeX, holeZ, new bool[,] { { true } });
                Vector3 oldCenter = layout.cells[0].boundsCenter;
                layout.cells[0].boundsCenter = new Vector3(200f, 0f, 0f);
                Require(!apply() && data.GetHeights(64, 64, 1, 1)[0, 0] == before, "Out-of-tile parcel accepted or changed terrain.");
                layout.cells[0].boundsCenter = oldCenter;
                // note: The centre remains inside the tile while its five-metre core crosses the edge.
                layout.cells[0].boundsCenter = Quaternion.Euler(0f, -heading, 0f) * new Vector3(62f, 0f, 0f);
                Require(!apply() && data.GetHeights(64, 64, 1, 1)[0, 0] == before, "Partially out-of-tile footprint accepted.");
                layout.cells[0].boundsCenter = oldCenter;
                report.AppendLine("- PASS partial footprint boundary rejection at heading " + heading);
                report.AppendLine("- PASS missing-ground rejection: terrain hole and out-of-tile parcel at heading " + heading);
                report.AppendLine("- PASS heading " + heading + ": actual parcel planes, untouched ground, saved reapplication, invalid-version rejection.");
            }
            report.AppendLine("\nPASS. Isolated terrain application only; full-world visual acceptance remains separate.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQParcelTerrainWrites.md", report.ToString());
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Sector Terrain Prepass")]
    public static void VerifySectorTerrainPrepass()
    {
        // note: Execute the shared production prepass on a real heightmap; synthetic site records are never published to a profile.
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var data = new TerrainData { heightmapResolution = 257, size = new Vector3(1024f, 100f, 1024f) };
        var pendingField = typeof(YQProceduralSettlementLayout).GetField("pending",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var pending = (Dictionary<string, YQProceduralSettlementLayoutRecord>)pendingField.GetValue(null);
        var pendingBefore = new Dictionary<string, YQProceduralSettlementLayoutRecord>(pending);
        var report = new System.Text.StringBuilder("# Sector terrain prepass verification\n\nEvidence: DETACHED_EDITOR_TERRAIN_NOT_PRODUCTION_TRAVEL\n");
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Runtime assembly MVID: " + typeof(YQGeneratedWorldRuntimeBuilder).Module.ModuleVersionId);
        report.AppendLine("Editor assembly MVID: " + typeof(YQProceduralSettlementLayoutVerification).Module.ModuleVersionId);
        try
        {
            var terrainObject = Terrain.CreateTerrainGameObject(data);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(terrainObject, scene);
            terrainObject.transform.position = new Vector3(-512f, 12f, -512f);
            var terrain = terrainObject.GetComponent<Terrain>();
            var grade = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("TryGradePreparedCompositionTerrain",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Require(grade != null, "The production composition prepass is missing.");
            foreach (float heading in new[] { 0f, 37f, 90f, 123f })
            {
                object boxed = new YQSpatialMaterializationSiteV2 { siteId = "sector-terrain-owner", sourceSemanticId = "sector-terrain-semantic",
                    reservedRadius = 72f, headingDegrees = heading };
                typeof(YQSpatialMaterializationSiteV2).GetField("memberFootprint",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(boxed,
                    new[] { new YQSiteMemberFootprintV2 { memberId = "east", x = 384f, reservedRadius = 32f, sectorIndex = 1 },
                        new YQSiteMemberFootprintV2 { memberId = "west", x = -384f, reservedRadius = 32f, sectorIndex = 2 } });
                var site = (YQSpatialMaterializationSiteV2)boxed;
                var cells = new List<YQProceduralSettlementLayout.Cell>();
                foreach (string id in new[] { "assembly-a", "assembly-b", "assembly-c" })
                    cells.Add(new YQProceduralSettlementLayout.Cell { id = id, center = new Vector3(0f, 3f, 0f),
                        size = new Vector3(12f, 6f, 10f), entrance = new Vector3(0f, 0f, 5f), outward = Vector3.forward });
                string seed = YQProceduralSettlementLayout.BuildSectorSeed("sector-terrain-fixture", site);
                Require(YQProceduralSettlementLayout.TryBuildSectors(cells, seed, site, out var layout, out string failure), failure);
                Require(layout.earthworkVersion == 3 && layout.radius > site.reservedRadius, "New sector earthwork policy was not selected.");
                pending[seed] = layout;
                var constructor = typeof(YQPreparedSpatialMaterializationV2).GetConstructors(
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];
                var prepared = (YQPreparedSpatialMaterializationV2)constructor.Invoke(new object[] {
                    Array.Empty<YQSpatialMaterializationRegionV2>(), new[] { site }, Array.Empty<YQSpatialMaterializationWaterV2>(),
                    Array.Empty<YQSpatialMaterializationWaterPointV2>(), Array.Empty<YQSpatialMaterializationRouteV2>(), Array.Empty<YQSpatialMaterializationRoutePointV2>(),
                    Array.Empty<YQSpatialMaterializationCrossingV2>(), new Dictionary<string, int>(),
                    new Dictionary<string, int> { [site.sourceSemanticId] = 0 }, new Dictionary<string, int> { [site.siteId] = 0 }, null, "", 0L, null });
                var source = new float[257, 257];
                for (int z = 0; z < 257; z++)
                    for (int x = 0; x < 257; x++) source[z, x] = .25f + x * 4f * .01f / 100f;
                data.SetHeights(0, 0, source);
                bool Apply(YQSpatialBlueprintTerrainSamplerV2 sampler = null) => (bool)grade.Invoke(null, new object[] {
                    prepared, site.sourceSemanticId, seed, terrain, Vector3.zero, layout.radius + 3f, layout.radius + 54f,
                    float.NegativeInfinity, heading, sampler });
                Require(Apply(), "The production prepass rejected a valid 416m union within individual accepted reserves.");
                data.SyncHeightmap();
                Require(layout.parcelGroundHeights.Count == layout.cells.Count, "The prepass did not retain parcel elevations.");
                for (int i = 0; i < layout.cells.Count; i++)
                {
                    Vector3 point = Quaternion.Euler(0f, heading, 0f) * (layout.cells[i].boundsCenter - layout.origin);
                    Require(Mathf.Abs(terrain.SampleHeight(point) + 12f - layout.parcelGroundHeights[i]) < .025f, "A sector foundation misses its saved plane.");
                }
                Require(Mathf.Abs(data.GetHeights(128, 178, 1, 1)[0, 0] - source[178, 128]) < .0001f,
                    "The prepass graded the unowned gap inside the aggregate circle.");
                // note: Replay the saved profile against different relief through the same production prepass.
                string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(layout);
                layout = Newtonsoft.Json.JsonConvert.DeserializeObject<YQProceduralSettlementLayoutRecord>(serialized);
                pending[seed] = layout;
                float saved = layout.parcelGroundHeights[0];
                for (int z = 0; z < 257; z++) for (int x = 0; x < 257; x++) source[z, x] += .005f;
                data.SetHeights(0, 0, source);
                Require(Apply(), "Saved sector earthworks failed to replay.");
                data.SyncHeightmap();
                Vector3 foundation = Quaternion.Euler(0f, heading, 0f) * (layout.cells[0].boundsCenter - layout.origin);
                Require(Mathf.Abs(terrain.SampleHeight(foundation) + 12f - saved) < .025f, "Saved sector elevation drifted.");
                // note: Persist on the existing base hostile owner, then discard transient caches and resolve from a serialized world document.
                var hostile = new GeneratedEncampmentRecord { encampmentId = "base-hostile-fixture", deterministicSeed = "original-hostile-seed" };
                Require(!YQProceduralSettlementLayout.Commit(hostile, "missing-candidate") && hostile.proceduralLayout == null,
                    "Failed construction published a hostile layout.");
                Require(YQProceduralSettlementLayout.Commit(hostile, seed), "Successful hostile layout was not committed.");
                var savedPlan = new GeneratedWorldPlanRecord { encampments = new List<GeneratedEncampmentRecord> { hostile } };
                savedPlan = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(
                    Newtonsoft.Json.JsonConvert.SerializeObject(savedPlan));
                pending.Remove(seed);
                var lookup = typeof(YQProceduralSettlementLayout).GetMethod("FindCommittedLayout",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
                    new[] { typeof(GeneratedWorldPlanRecord), typeof(string) }, null);
                var restored = (YQProceduralSettlementLayoutRecord)lookup.Invoke(null, new object[] { savedPlan, seed });
                Require(restored != null && Newtonsoft.Json.JsonConvert.SerializeObject(restored) == serialized,
                    "Cache-free hostile replay changed selection, geometry or parcel elevations.");
                pending[seed] = layout;
                Require(!YQProceduralSettlementLayout.Commit(hostile, seed) && ReferenceEquals(hostile.proceduralLayout, layout),
                    "A committed hostile layout was overwritten.");
                // note: A route under a foundation must reject before any terrain or persisted elevation mutation.
                data.SetHeights(0, 0, source);
                var protectedFoundation = CreateRouteSampler(foundation - Vector3.forward * 12f, foundation + Vector3.forward * 12f);
                Require(!Apply(protectedFoundation), "A protected corridor underneath a foundation was accepted.");
                Require(Mathf.Abs(data.GetHeights(128, 128, 1, 1)[0, 0] - source[128, 128]) < .0001f &&
                    Newtonsoft.Json.JsonConvert.SerializeObject(layout) == serialized, "Rejected prepass changed terrain or accepted elevations.");
                layout.sectors[1].radius += 1f;
                Require(!Apply(), "The production prepass accepted a changed member reservation.");
                report.AppendLine("- PASS heading " + heading + ": member union, real foundations, untouched gap, saved replay, corridor and changed-reserve rejection.");
            }
            VerifyParcelTerrainWrites();
            report.AppendLine("\nPASS. Production travel, asset appearance and streaming performance remain separate acceptance paths.");
        }
        catch (Exception exception) { report.AppendLine("\nFAIL: " + exception); throw; }
        finally
        {
            pending.Clear(); foreach (var pair in pendingBefore) pending[pair.Key] = pair.Value;
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(data);
            Directory.CreateDirectory("outputs/G08_Environment_Repair_20261005");
            File.WriteAllText("outputs/G08_Environment_Repair_20261005/SectorTerrainPrepass.md", report.ToString());
        }
    }

    private static YQSpatialBlueprintTerrainSamplerV2 CreateRouteSampler(Vector3 start, Vector3 end)
    {
        // note: Build only detached numeric sampler arrays; this fixture never accepts an invented world artifact or route.
        var constructor = typeof(YQSpatialBlueprintTerrainSamplerV2).GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];
        var parameters = constructor.GetParameters();
        var arguments = new object[parameters.Length];
        object Record(Type type, params (string name, object value)[] values)
        {
            object record = Activator.CreateInstance(type);
            foreach (var value in values) type.GetField(value.name).SetValue(record, value.value);
            return record;
        }
        for (int i = 0; i < parameters.Length; i++)
        {
            Type element = parameters[i].ParameterType.GetElementType();
            if (parameters[i].Name == "routes")
            {
                Array routes = Array.CreateInstance(element, 1);
                routes.SetValue(Record(element, ("width", 4f), ("shoulderWidth", 2f), ("pointStart", 0), ("pointCount", 2)), 0);
                arguments[i] = routes;
            }
            else if (parameters[i].Name == "routePoints")
            {
                Array points = Array.CreateInstance(element, 2);
                points.SetValue(Record(element, ("x", start.x), ("z", start.z), ("elevation", .3f)), 0);
                points.SetValue(Record(element, ("x", end.x), ("z", end.z), ("elevation", .3f)), 1);
                arguments[i] = points;
            }
            else arguments[i] = Array.CreateInstance(element, 0);
        }
        return (YQSpatialBlueprintTerrainSamplerV2)constructor.Invoke(arguments);
    }

    public static void VerifyEldwealdBuildingGrounding()
    {
        // note: Exercise production grounding on real imported buildings, not proxy cubes; dispose every test object and leave source assets untouched.
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        TerrainData data = new TerrainData { heightmapResolution = 65, size = new Vector3(128f, 40f, 128f) };
        var report = new System.Text.StringBuilder("# Eldweald building grounding\n\n");
        try
        {
            var terrainObject = Terrain.CreateTerrainGameObject(data);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(terrainObject, scene);
            terrainObject.name = YQGeneratedWorldTerrain.RuntimeTerrainObjectName;
            terrainObject.transform.position = new Vector3(-64f, 12f, -64f);
            var terrain = terrainObject.GetComponent<Terrain>();
            var ground = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("GroundInstance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            for (int index = 1; index <= 4; index++)
                foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                {
                    string path = "Assets/BefourStudios/AncientDesertRuins/Art/Prefabs/SM_Building" + index + ".prefab";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Require(prefab != null, "Missing building " + path);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    try
                    {
                        instance.transform.position = new Vector3(7f, 17f, -11f);
                        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                        Physics.SyncTransforms();
                        ground.Invoke(null, new object[] { instance });
                        Require(YQGeneratedWorldTerrain.TryGetStableContactGeometry(instance, out Bounds bounds, out float bottom), "No structural contact geometry.");
                        float surface = terrain.SampleHeight(bounds.center) + terrain.transform.position.y;
                        float clearance = bottom - surface;
                        report.AppendLine("- Building " + index + ", yaw=" + yaw + ": structural clearance=" + clearance.ToString("F4") + "m");
                        Require(clearance <= 0.18f && clearance >= -0.65f, "Building " + index + " failed terrain contact: " + clearance);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(instance); }
                }
            // note: Test gradual and incompatible slopes against the same sampled footprint used by runtime grounding.
            foreach (float gradient in new[] { 0.03f, 0.30f })
            {
                var heights = new float[65, 65];
                for (int z = 0; z < 65; z++)
                    for (int x = 0; x < 65; x++) heights[z, x] = (x * 2f * gradient) / 40f;
                data.SetHeights(0, 0, heights);
                for (int index = 1; index <= 4; index++)
                    foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BefourStudios/AncientDesertRuins/Art/Prefabs/SM_Building" + index + ".prefab");
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                        try
                        {
                            instance.transform.position = new Vector3(7f, 30f, -11f);
                            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                            Vector3 before = instance.transform.position;
                            Require(YQGeneratedWorldTerrain.TryGetStableContactGeometry(instance, out Bounds bounds, out _), "Missing slope-test contact geometry.");
                            Require(YQGeneratedWorldTerrain.TrySampleFootprintHeight(terrain, bounds, out _, out float minimum, out float maximum), "Missing terrain samples.");
                            bool accepted = YQGeneratedWorldTerrain.TryPlaceGroundedObject(instance, terrain, YQGeneratedWorldPlacementCategory.Structure, 0.05f, out _);
                            Require(accepted == (maximum - minimum <= 0.83f), "Slope acceptance disagrees with available support interval.");
                            if (accepted)
                            {
                                YQGeneratedWorldTerrain.TryGetStableContactGeometry(instance, out _, out float bottom);
                                Require(bottom - minimum <= 0.1801f && maximum - bottom <= 0.6501f, "Accepted foundation floats or buries beyond limits.");
                            }
                            else Require(instance.transform.position == before, "Rejected structure was moved.");
                            report.AppendLine("- gradient=" + gradient + ", building=" + index + ", yaw=" + yaw + ", accepted=" + accepted);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(instance); }
                    }
            }
            report.AppendLine("\nPASS: real buildings on flat and sloped terrain; incompatible slopes rejected without moving the building. Streets, interiors and presentation remain unverified.");
        }
        catch (Exception exception)
        {
            report.AppendLine("\nFAILED: " + exception);
            throw;
        }
        finally
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQEldwealdBuildingGrounding.md", report.ToString());
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    public static void InspectEldwealdBuildingCandidates()
    {
        // note: Instantiate only the four registered source buildings in an isolated preview scene and run the production placement gate.
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var report = new System.Text.StringBuilder("# Eldweald whole-building candidates\n\n");
        var gate = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("ShouldRejectSettlementPlacement",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        try
        {
            // note: Build a disposable real palette through production curation, then exercise selection and runtime registry resolution without changing accepted world state.
            var plan = new GeneratedWorldPlanRecord
            {
                worldSeed = "eldweald-candidate-verification",
                regions = new List<GeneratedRegionRecord>
                {
                    new GeneratedRegionRecord { regionId = "desert_fixture", assetStyleKey = "ancient_desert_ruins" }
                }
            };
            YQWorldAssetCatalog.EnsureAssetPalettes(plan);
            var palette = plan.assetPalettes[0];
            var registry = YQRuntimeWorldAssetRegistry.Instance;
            Require(registry != null, "Runtime registry missing.");
            for (int seed = 0; seed < 32; seed++)
            {
                var selected = YQWorldAssetCatalog.PickAssetForSlot(palette, YQWorldAssetCatalog.SlotSettlementBuilding, "eldweald|" + seed);
                Require(selected != null, "Production palette returned no whole building.");
                Require(registry.ResolvePrefab(selected.assetPath) != null, "Registry cannot load " + selected.assetPath);
            }
            report.AppendLine("PASS: 32 real-palette selections resolve whole-building prefabs through the runtime registry.\n");
            for (int index = 1; index <= 4; index++)
            {
                string path = "Assets/BefourStudios/AncientDesertRuins/Art/Prefabs/SM_Building" + index + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(prefab != null, "Missing registered building: " + path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    bool initialized = false;
                    Bounds bounds = default;
                    foreach (var renderer in renderers)
                    {
                        if (!initialized) { bounds = renderer.bounds; initialized = true; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                    var reference = new GeneratedAssetReferenceRecord { assetPath = path, slotTag = YQWorldAssetCatalog.SlotSettlementBuilding };
                    object[] args = { reference, instance, string.Empty };
                    bool rejected = (bool)gate.Invoke(null, args);
                    report.AppendLine("- " + path + ": size=" + bounds.size.ToString("F3") +
                        "; scale=" + instance.transform.localScale.ToString("F3") + "; rejected=" + rejected + "; reason=" + args[2]);
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQEldwealdBuildingCandidates.md", report.ToString());
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [MenuItem("Tools/YourQuest/Testing/Capture Live World Placement")]
    public static void CaptureLiveWorldPlacement()
    {
        // note: Capture the existing failing world without regenerating, moving objects, repairing materials or touching the save.
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("Capture requires the generated world to be running in Play mode.");
        GameObject root = GameObject.Find("YQ_GENERATED_WORLD_RUNTIME");
        Require(root != null, "The live generated world root was not found.");
        Transform terrainRoot = root.transform.Find(YQGeneratedWorldTerrain.RuntimeTerrainObjectName);
        Terrain terrain = terrainRoot != null ? terrainRoot.GetComponent<Terrain>() : null;
        Require(terrain != null && terrain.terrainData != null, "The world's generated terrain is missing.");
        var report = new System.Text.StringBuilder("# Live world placement capture\n\n");
        report.AppendLine("Captured: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("World seed: " + WorldStateManager.Instance?.State?.generatedWorldPlan?.worldSeed);
        report.AppendLine("Terrain origin: " + terrain.transform.position.ToString("F3") + "; size: " + terrain.terrainData.size.ToString("F3"));
        report.AppendLine("\nRenderer clearance is a measurement, not a defect verdict: roofs, upper floors and suspended props can legitimately be above terrain. Paths identify the source of each measurement.\n");
        foreach (Transform site in root.transform)
        {
            if (!site.name.StartsWith("Compiled", StringComparison.OrdinalIgnoreCase) &&
                !site.name.StartsWith("Settlement", StringComparison.OrdinalIgnoreCase) &&
                !site.name.StartsWith("Origin", StringComparison.OrdinalIgnoreCase) &&
                site.name != "Generated_CompiledHostileSites") continue;
            report.AppendLine("## " + site.name + "\nRoot position: " + site.position.ToString("F3"));
            Renderer[] renderers = site.GetComponentsInChildren<Renderer>(true);
            // note: Include inactive staging geometry with its visibility state so rejected cells are not mistaken for published content.
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) continue;
                Bounds bounds = renderer.bounds;
                Vector3 point = bounds.center;
                Vector3 local = point - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                bool inside = local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z;
                float ground = inside ? terrain.SampleHeight(point) + terrain.transform.position.y : float.NaN;
                int broken = 0;
                foreach (Material material in renderer.sharedMaterials)
                    if (!YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material)) broken++;
                // note: Keep the report bounded to visible-height anomalies and material failures, retaining low structural contacts for comparison.
                float clearance = bounds.min.y - ground;
                if (inside && clearance >= -0.65f && clearance <= 0.18f && broken == 0) continue;
                string path = renderer.name;
                for (Transform parent = renderer.transform.parent; parent != null && parent != root.transform; parent = parent.parent)
                    path = parent.name + "/" + path;
                report.AppendLine("- " + path + " | active=" + renderer.gameObject.activeInHierarchy +
                    ", enabled=" + renderer.enabled + ", bottom=" + bounds.min.y.ToString("F3") +
                    ", terrain=" + ground.ToString("F3") + ", clearance=" + clearance.ToString("F3") +
                    ", size=" + bounds.size.ToString("F3") + ", invalidMaterials=" + broken);
                if (broken > 0)
                    foreach (Material material in renderer.sharedMaterials)
                        report.AppendLine("  material=" + (material != null ? material.name : "missing") +
                            ", shader=" + (material != null && material.shader != null ? material.shader.name : "missing"));
            }
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/YQLiveWorldPlacement.md", report.ToString());
        // note: The screenshot is captured on the next rendered frame; the report itself is complete immediately.
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/YQLiveWorldPlacement.png"));
        Debug.Log("[WORLDGEN] Captured live placement evidence: Logs/YQLiveWorldPlacement.md and next-frame screenshot.");
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Real Assembly Construction")]
    public static void VerifyRealAssemblyConstruction()
    {
        // note: Inspect exact published districts and the existing source-linked home; do not approve metadata or alter a player save.
        var report = new System.Text.StringBuilder("# Real assembly construction evidence\n\n");
        var districtManifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(
            "Assets/Assets/Resources/YQWorldSites/medieval_viking_village/YQRuntimeSemanticSite.asset");
        Require(districtManifest != null, "Published district manifest missing.");
        foreach (var zone in districtManifest.Zones)
        {
            if (zone?.prefab == null) continue;
            report.AppendLine("## " + zone.stableId);
            foreach (string path in zone.connectionSocketPaths)
            {
                Transform socket = zone.prefab.transform.Find(path);
                if (socket == null) { report.AppendLine("- Missing: " + path); continue; }
                Vector3 point = zone.prefab.transform.InverseTransformPoint(socket.position);
                Vector3 outward = zone.prefab.transform.InverseTransformDirection(socket.forward);
                bool eligible = YQProceduralSettlementLayout.IsExternalConnection(zone.localBoundsCenter,
                    zone.localBoundsSize, point, outward);
                // note: Report distance to the envelope only; projecting a socket to that edge does not prove a clear or supported approach.
                Vector3 relative = point - zone.localBoundsCenter;
                float xDistance = Mathf.Abs(outward.x) > .001f
                    ? (zone.localBoundsSize.x * .5f - Mathf.Sign(outward.x) * relative.x) / Mathf.Abs(outward.x)
                    : float.PositiveInfinity;
                float zDistance = Mathf.Abs(outward.z) > .001f
                    ? (zone.localBoundsSize.z * .5f - Mathf.Sign(outward.z) * relative.z) / Mathf.Abs(outward.z)
                    : float.PositiveInfinity;
                report.AppendLine("- " + path + ": external=" + eligible + "; local=" + point.ToString("F3") +
                    "; outward=" + outward.ToString("F3") + "; envelope approach distance=" +
                    Mathf.Min(xDistance, zDistance).ToString("F3") + "m (not traversability proof)");
            }
            report.AppendLine();
        }
        Directory.CreateDirectory("Logs");
        try
        {
            // note: Exercise the real published selector after filtering invalid frontage; success here proves geometry only, never habitation or service readiness.
            for (int index = 0; index < 32; index++)
            {
                string seed = YQProceduralSettlementLayout.SeedPrefix + "published-frontage|" + index;
                Require(YQCompiledWorldSiteInstance.TryBuildSemanticCompositionV2(districtManifest,
                    new[] { "residential" }, seed, out var composition, out string failure), failure);
                Require(composition.selectedIds.Length == 1 &&
                    composition.selectedIds[0] == "yq_viking_district_west_homestead",
                    "Fresh selection included a district without external frontage.");
                Require(!YQSiteFunctionContractsV2.TryValidate(districtManifest, composition.selectedIds,
                    new[] { YQAssetFunctionV2.Habitation, YQAssetFunctionV2.Service },
                    YQWorldStructureUsagePolicy.FullyEnterable, out _),
                    "Geometry filtering accidentally approved pending habitation/service evidence.");
            }
            report.AppendLine("PASS: 32 published-library selections exclude invalid frontage; pending functions remain rejected.");
            const string candidatePath = "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/V2EntranceCandidates/YQ_StreetConnectedHomeCandidate.asset";
            var home = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(candidatePath);
            Require(home != null && home.Zones.Count == 1, "Street-connected home candidate missing.");
            var zone = home.Zones[0];
            string currentSignature = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(zone.prefab)).ToString();
            report.AppendLine("## Source-linked home\n");
            report.AppendLine("Saved source signature: " + home.SourceSignature + "; current dependency hash: " + currentSignature);
            Require(currentSignature == home.SourceSignature, "Home candidate source signature is stale; review before publication.");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { zone.stableId };
            for (int index = 0; index < 32; index++)
            {
                string seed = YQProceduralSettlementLayout.SeedPrefix + "real-home|" + index;
                Require(YQProceduralSettlementLayout.TryResolve(home, ids, seed, out var layout, out string failure), failure);
                Require(layout != null && YQProceduralSettlementLayout.ValidateRecord(layout, out failure), failure);
            }
            report.AppendLine("PASS: 32 current-version layouts using the real home prefab and declared street entrance.");
            YQCandidateInteriorBuilderV2.VerifyStreetConnectedHomeProviders();
            report.AppendLine("PASS: existing door/storage ownership and landing-datum tests at four rotations.");
            report.AppendLine("Candidate remains unpublished. Complete functional, terrain and player traversal acceptance remains required.");
        }
        catch (Exception error)
        {
            report.AppendLine("FAILED: " + error.Message);
            throw;
        }
        finally { File.WriteAllText("Logs/YQRealAssemblyConstruction.md", report.ToString()); }
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Generation Construction Readiness")]
    public static void VerifyGenerationConstructionReadiness()
    {
        // note: A production readiness invocation must pass both engine fixtures and the real published library; a synthetic pass cannot hide asset failure.
        VerifyIndependentStreamingAssemblies();
        Verify();
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Independent Streaming Assemblies")]
    public static void VerifyIndependentStreamingAssemblies()
    {
        // note: Exercise actual selection, layout, transform application and resident binding using disposable reviewed-contract fixtures.
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var manifest = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        var streaming = ScriptableObject.CreateInstance<YQAuthoredSiteStreamingManifest>();
        try
        {
            var cells = new List<YQAuthoredSiteStreamingCellRecord>();
            var zones = new List<YQReviewedSemanticZoneRecord>();
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 4; i++)
            {
                var prefab = new GameObject("IndependentAssemblyFixture" + i);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prefab, scene);
                var door = new GameObject("ExternalConnection");
                door.transform.SetParent(prefab.transform, false);
                door.transform.localPosition = new Vector3(0, 0, 6);
                var cell = new YQAuthoredSiteStreamingCellRecord();
                cell.Configure("cell-" + i, prefab, new Vector3(i * 1000, 0, i * 1000),
                    new Vector3(0, 3, 0), new Vector3(10, 6, 12), 20, true, 0, 1);
                cells.Add(cell); selected.Add(cell.StableCellId);
                zones.Add(new YQReviewedSemanticZoneRecord
                {
                    stableId = "district-" + i, semanticTags = new List<string> { "residential" },
                    streamingCellIds = new List<string> { cell.StableCellId },
                    cellContractsV2 = new List<YQReviewedCellFunctionContractV2>
                    {
                        new YQReviewedCellFunctionContractV2
                        {
                            cellId = cell.StableCellId, sourceSignature = "fixture-source",
                            independentAssembly = new YQIndependentAssemblyContract
                            {
                                sourceSignature = "fixture-source", reviewState = YQSemanticSiteReviewState.Approved,
                                completeStructuralDependencies = true, foundationVerified = true,
                                externalConnectionPaths = new List<string> { "ExternalConnection" }
                            }
                        }
                    }
                });
            }
            streaming.ConfigureCandidate("fixture", default, "", "fixture-source", null, cells);
            manifest.ConfigureCandidate("fixture", "fixture", "fixture-source", default, streaming, 80, zones);
            manifest.MarkReleaseEligible();
            string seed = YQProceduralSettlementLayout.SeedPrefix + "streaming-fixture";
            Require(YQProceduralSettlementLayout.TryResolve(manifest, selected, seed, out var layout, out string failure), failure);
            Require(layout != null && layout.cells.Count == 4 && layout.version == 4,
                "Reviewed streaming assemblies did not enter the versioned block solver.");
            Require(YQProceduralSettlementLayout.ValidateRecord(layout, out failure), failure);
            Require(layout.radius < 210, "Donor-map distances leaked into generated geometry.");
            var instance = new GameObject("StreamedAssembly");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
            Require(YQProceduralSettlementLayout.ApplyPlacement(layout, "cell-0", instance.transform, layout.origin),
                "Runtime placement ignored an accepted assembly.");
            Require(YQProceduralSettlementLayout.TryPlacement(layout, "cell-0", out var expected), "Missing placement.");
            Require((instance.transform.localPosition - (expected.position - layout.origin)).sqrMagnitude < 0.0001f &&
                Quaternion.Angle(instance.transform.localRotation, Quaternion.Euler(0, expected.yaw, 0)) < 0.01f,
                "Streaming did not apply persisted position and yaw.");
            Require(YQProceduralSettlementLayout.TryResolveZonePlacement(layout, zones[0], seed, out var residentCell) &&
                residentCell.cellId == "cell-0", "Resident remained at donor district coordinates.");
            zones[0].streamingCellIds[0] = "CELL-0";
            Require(YQProceduralSettlementLayout.TryResolveZonePlacement(layout, zones[0], seed, out residentCell) &&
                residentCell.cellId == "cell-0", "Case-normalized source references lost their placed resident anchor.");
            var replay = JsonUtility.FromJson<YQProceduralSettlementLayoutRecord>(JsonUtility.ToJson(layout));
            Require(YQProceduralSettlementLayout.ApplyPlacement(replay, "cell-0", instance.transform, replay.origin) &&
                (instance.transform.localPosition - (expected.position - layout.origin)).sqrMagnitude < 0.0001f,
                "Saved assembly transforms did not round-trip.");
            Require(YQProceduralSettlementLayout.TryResolve(manifest, selected,
                YQProceduralSettlementLayout.PreviousSeedPrefix + "old", out var oldLayout, out failure) && oldLayout == null,
                "An existing v2 streaming seed changed its authored layout.");

            // note: The production selector must accept independent cells despite their disconnected donor positions; function approval remains a separate gate.
            Require(YQCompiledWorldSiteInstance.TryBuildSemanticCompositionV2(manifest,
                new[] { "residential" }, seed, out var composition, out failure), failure);
            Require(composition.selectedIds != null && composition.selectedIds.Length > 0,
                "The runtime selector did not use independently reviewed cells.");
            zones[0].cellContractsV2[0].independentAssembly.sourceSignature = "stale";
            Require(!YQProceduralSettlementLayout.TryResolve(manifest, selected, seed, out _, out failure),
                "Stale construction approval was accepted.");
            zones[0].cellContractsV2[0].independentAssembly.sourceSignature = "fixture-source";
            zones[0].cellContractsV2[0].independentAssembly.completeStructuralDependencies = false;
            Require(!YQProceduralSettlementLayout.TryResolve(manifest, selected, seed, out _, out failure),
                "A dependent fragment was moved independently.");
            zones[0].cellContractsV2[0].independentAssembly.completeStructuralDependencies = true;
            zones[0].cellContractsV2[0].independentAssembly.externalConnectionPaths[0] = "Missing";
            Require(!YQProceduralSettlementLayout.TryResolve(manifest, selected, seed, out _, out failure),
                "An assembly with a missing external portal was accepted.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQIndependentStreamingAssemblies.txt",
                "PASS: production selector, independent streamed block layout, persisted transform/yaw, resident anchoring, save round-trip, v2 compatibility, stale approval/dependency/missing portal rejection. Fixtures only; no production kit or rendered-world certification.");
            VerifyWorldConstructionEvidence();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(manifest);
            UnityEngine.Object.DestroyImmediate(streaming);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [MenuItem("Tools/YourQuest/Testing/Verify World Construction Evidence")]
    public static void VerifyWorldConstructionEvidence()
    {
        // note: Disposable metadata fixtures verify reporting without opening vendor prefabs or modifying an accepted save.
        var manifest = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        var streaming = ScriptableObject.CreateInstance<YQAuthoredSiteStreamingManifest>();
        try
        {
            var cell = new YQAuthoredSiteStreamingCellRecord();
            cell.Configure("cell", null, Vector3.zero, Vector3.zero, Vector3.one, 17);
            streaming.ConfigureCandidate("fixture", default, "", "fixture", null, new[] { cell });
            var zone = new YQReviewedSemanticZoneRecord { stableId = "cell", sourceInstanceCount = 17 };
            manifest.ConfigureCandidate("fixture", "fixture", "fixture", default, streaming, 17, new[] { zone });
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cell" };
            string seed = YQProceduralSettlementLayout.SeedPrefix + "construction-evidence";
            var report = YQWorldConstructionReport.Describe("town", manifest, selected, seed, null, 8);
            Require(report.mode == YQWorldConstructionMode.AuthoredStreamingSlice &&
                report.sourceInstances == 17 && report.requestedBlocks == 8 && !report.populationCapacityVerified,
                "An authored slice was mistaken for a population-scaled procedural town.");
            selected.Add("missing");
            Require(YQWorldConstructionReport.Describe("town", manifest, selected, seed, null, 8).mode ==
                YQWorldConstructionMode.Unresolved, "Missing source geometry was reported as resolved.");
            selected.Remove("missing");
            manifest.ConfigureCandidate("fixture", "fixture", "fixture", default, null, 17, new[] { zone });
            Require(YQWorldConstructionReport.Describe("town", manifest, selected, seed, null, 8).mode ==
                YQWorldConstructionMode.AuthoredZones, "Legacy authored zones lost their compatibility classification.");
            var inputs = new[] { new YQProceduralSettlementLayout.Cell { id = "cell",
                center = new Vector3(0, 3, 0), size = new Vector3(10, 6, 12),
                entrance = new Vector3(0, 0, 6), outward = Vector3.forward } };
            Require(YQProceduralSettlementLayout.TryBuild(inputs, seed, out var layout, out string failure), failure);
            layout.kitId = manifest.KitId;
            layout.sourceSignature = manifest.SourceSignature;
            report = YQWorldConstructionReport.Describe("town", manifest, selected, seed, layout, 8);
            Require(report.mode == YQWorldConstructionMode.ProceduralBlocks &&
                report.limitation.Contains("exceeds"), "Fitted layout concealed a block shortfall.");
            layout.sourceSignature = "stale";
            Require(YQWorldConstructionReport.Describe("town", manifest, selected, seed, layout, 8).mode ==
                YQWorldConstructionMode.Unresolved, "A stale layout was treated as current construction evidence.");
            Require(YQWorldConstructionReport.Describe("town", null, null, seed, null, 0).mode ==
                YQWorldConstructionMode.Unresolved, "Missing manifest was treated as complete.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQWorldConstructionEvidence.txt",
                "PASS: authored streaming/zone classification, missing-source rejection, procedural layout identity, block shortfall and stale-layout rejection. No rendered-world or population-capacity certification.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(manifest);
            UnityEngine.Object.DestroyImmediate(streaming);
        }
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Settlement Repair Boundaries")]
    public static void VerifySettlementRepairBoundaries()
    {
        // note: Test production selector/material boundaries using disposable records, not player saves or imported assets.
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var compatibility = typeof(YQRuntimeUrpMaterialRepair).GetMethod("IsPipelineCompatible", flags);
        // note: The live bed exposed a GPU-supported graph with no usable URP subshader tag.
        Require(!(bool)compatibility.Invoke(null, new object[] { "Shader Graphs/S_BasicTextured", "", true }),
            "Untagged graph bypassed URP compatibility.");
        Require((bool)compatibility.Invoke(null, new object[] { "Shader Graphs/S_BasicTextured", "", false }),
            "URP-only rule changed another render pipeline.");
        Require(!(bool)compatibility.Invoke(null, new object[] { "Shader Graphs/Vendor", "HDRenderPipeline", true }),
            "HDRP graph bypassed the URP compatibility gate.");
        Require(!(bool)compatibility.Invoke(null, new object[] { "HDRP/Lit", "", true }), "HDRP Lit bypassed compatibility.");
        Require((bool)compatibility.Invoke(null, new object[] { "Shader Graphs/Vendor", "UniversalPipeline", true }),
            "Compatible URP source graph was rejected.");
        Require((bool)compatibility.Invoke(null, new object[] { "TextMeshPro/Distance Field", "", true }),
            "Pipeline-neutral text was rejected.");

        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var streaming = ScriptableObject.CreateInstance<YQAuthoredSiteStreamingManifest>();
        var manifest = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        try
        {
            var prefab = new GameObject("DisposableCell");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(prefab, scene);
            var cell = new YQAuthoredSiteStreamingCellRecord();
            cell.Configure("a", prefab, Vector3.zero, Vector3.zero, new Vector3(8, 4, 8), 700);
            var zone = new YQReviewedSemanticZoneRecord { stableId = "district", sourceInstanceCount = 1,
                streamingCellIds = new List<string> { "a", "a" }, semanticTags = new List<string> { "civic" } };
            var cells = new Dictionary<string, YQAuthoredSiteStreamingCellRecord> { { "a", cell } };
            var measure = typeof(YQCompiledWorldSiteInstance).GetMethod("TryMeasureDefaultStreamingZone", flags);
            object[] arguments = { zone, cells, new HashSet<string>(), 0, new Bounds() };
            Require((bool)measure.Invoke(null, arguments) && (int)arguments[3] == 700,
                "Stale district count or duplicate references corrupted the real instance cost.");
            arguments = new object[] { zone, cells, new HashSet<string> { "a" }, 0, new Bounds() };
            Require((bool)measure.Invoke(null, arguments) && (int)arguments[3] == 0, "Shared cells were charged twice.");
            zone.streamingCellIds.Add("missing");
            arguments = new object[] { zone, cells, null, 0, new Bounds() };
            Require(!(bool)measure.Invoke(null, arguments), "A missing source cell published a partial district.");
            zone.streamingCellIds.Remove("missing");
            streaming.ConfigureCandidate("fixture", default, "", "fixture", prefab, new[] { cell });
            manifest.ConfigureCandidate("fixture", "fixture", "fixture", default, streaming, 1, new[] { zone });
            var select = typeof(YQCompiledWorldSiteInstance).GetMethod("BuildCuratedDefaultCellIds", flags);
            Require(((HashSet<string>)select.Invoke(null, new object[] { manifest })).Count == 0,
                "The smallest-district fallback bypassed the actual instance budget.");
            cell.Configure("a", prefab, Vector3.zero, Vector3.zero, new Vector3(8, 4, 8), 10);
            Require(((HashSet<string>)select.Invoke(null, new object[] { manifest })).SetEquals(new[] { "a" }),
                "A valid bounded fallback stopped loading.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQSettlementRepairBoundaries.txt",
                "PASS: HDRP graph rejection, URP/neutral shader preservation, actual/deduplicated instance cost, missing-cell rejection, hard fallback budget and valid bounded fallback. No visual settlement certification.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(manifest);
            UnityEngine.Object.DestroyImmediate(streaming);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Assembly Datum And Save Voice")]
    public static void VerifyAssemblyDatumAndSaveVoice()
    {
        // note: Exercise the actual grounding coroutine in a disposable preview scene; no saved world or authored scene is modified.
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(64, 16, 64) };
        try
        {
            var ground = Terrain.CreateTerrainGameObject(data);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
            var root = new GameObject("AssemblyDatumRegression");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(16, 0, 16);
            var walls = GameObject.CreatePrimitive(PrimitiveType.Cube);
            walls.name = "SM_Walls";
            walls.transform.SetParent(root.transform, false);
            walls.transform.localPosition = new Vector3(0, 3, 0);
            walls.transform.localScale = new Vector3(4, 2, 4);
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "SM_Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0, 6, 0);
            roof.transform.localScale = new Vector3(5, 1, 5);
            var roofLocal = roof.transform.localPosition;
            var wallsLocal = walls.transform.localPosition;
            int grounded = -1;
            var method = typeof(YQCompiledWorldSiteInstance).GetMethod("AlignCompiledCellsToTerrainRoutine",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var routine = (System.Collections.IEnumerator)method.Invoke(null, new object[] {
                root, ground.GetComponent<Terrain>(), false, new Action<int>(count => grounded = count), null });
            var stack = new Stack<System.Collections.IEnumerator>();
            stack.Push(routine);
            int ticks = 0;
            while (stack.Count > 0)
            {
                Require(++ticks < 10000, "Grounding coroutine did not terminate.");
                var current = stack.Peek();
                if (!current.MoveNext()) { stack.Pop(); continue; }
                if (current.Current is System.Collections.IEnumerator nested) stack.Push(nested);
            }
            Require(grounded == 2, "Shared authored assembly was rejected.");
            Require(roof.transform.localPosition == roofLocal && walls.transform.localPosition == wallsLocal,
                "Grounding separated the roof from its authored walls.");
            Require(Mathf.Abs(walls.GetComponent<Renderer>().bounds.min.y + 0.015f) < 0.04f,
                "Shared foundation did not reach the terrain.");
            // note: Distinct requests must carry distinct directions/seeds, while repeat calls for one save remain stable. This does not assert uniqueness of live model output.
            var seeds = new HashSet<int>();
            for (int index = 0; index < 128; index++)
            {
                string seed = "fresh-save-" + index;
                Require(seeds.Add(YQGoddessGenerationDialogue.VoiceSamplingSeed(seed)), "Test saves shared a sampling seed.");
                Require(YQGoddessGenerationDialogue.BuildSaveVoiceVariationContract(seed) ==
                    YQGoddessGenerationDialogue.BuildSaveVoiceVariationContract(seed), "Save voice direction was unstable.");
            }
            VerifyGoddessProseStyle();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQAssemblyDatumAndSaveVoice.txt",
                "PASS: production grounding preserves roof/wall relative transforms and reaches flat terrain; 128 distinct stable save seeds; existing prose regression passed. Not a complete settlement or live dialogue validation.");
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Goddess Prose Style")]
    public static void VerifyGoddessProseStyle()
    {
        // note: Validate presentation contracts and fallbacks independently of world generation or live model requests.
        string contract = YQGoddessGenerationDialogue.BuildWorldVoiceContract(1);
        Require(!contract.Contains("largely monotone") && !contract.Contains("does not perform frantic incompetence") &&
            contract.Contains("benevolent") && contract.Contains("high-strung") &&
            contract.Contains("sarcastically bratty") && contract.Contains("Do not make every field a grand proclamation"),
            "Conflicting goddess persona instructions remain active.");
        string[] accepted = {
            "Welcome, Mira. I know you have questions, but for now, I must work.",
            "This world is very fragile, but it is being made especially for you.",
            "I am doing my best to keep things together. This is harder than I expected." };
        foreach (string line in accepted)
            Require(YQGoddessGenerationDialogue.IsSpokenVoiceFieldAcceptable(line, 28), "Requested direct speech was rejected.");
        string[] rejected = {
            "The goddess watches the hills rise beyond the courtyard.",
            "I am instantiating your prefab and configuring its collider.",
            "I am preparing the JSON for your beginning.",
            "*She smiles nervously* I have made a world for you." };
        foreach (string line in rejected)
            Require(!YQGoddessGenerationDialogue.IsSpokenVoiceFieldAcceptable(line, 28), "Narration/engine chatter passed the speech gate.");
        Require(YQProductionSliceRegressionTests.TestGoddessProsePreservation() == null,
            "Existing concise generated dialogue was replaced unnecessarily.");
        var player = new PlayerState { displayName = "Mira", characterLifeDirection = "protecting her family" };
        var origin = YQGoddessGenerationDialogue.EnsureOriginVoice(null, player, null);
        Require(origin.completion.Contains("Mira") && origin.completion.Contains("protecting"), "Personal welcome was lost.");
        var worldA = new GeneratedWorldPlanRecord { worldSeed = "voice-a" };
        worldA.regions.Add(new GeneratedRegionRecord { displayName = "Alderbrook", terrainProfile = "rolling hills", climateProfile = "cool rain" });
        worldA.settlements.Add(new GeneratedSettlementRecord { settlementId = "settlement-a", displayName = "Mira's Reach" });
        var worldB = new GeneratedWorldPlanRecord { worldSeed = "voice-b" };
        worldB.regions.Add(new GeneratedRegionRecord { displayName = "Sunmere", terrainProfile = "open flats", climateProfile = "dry heat" });
        worldB.settlements.Add(new GeneratedSettlementRecord { settlementId = "settlement-b", displayName = "Sunmere Gate" });
        var exploratoryPlayer = new PlayerState { displayName = "Mira", characterLifeDirection = "following her curiosity" };
        var worldVoiceA = YQGoddessGenerationDialogue.EnsureWorldVoice(null, worldA, player);
        var worldVoiceB = YQGoddessGenerationDialogue.EnsureWorldVoice(null, worldB, exploratoryPlayer);
        Require(worldVoiceA.completion != worldVoiceB.completion && worldVoiceA.terrain != worldVoiceB.terrain,
            "World fallback prose ignored the save and player context.");
        string[] pools = { "WorldPlanFinished", "StableScaffold", "TerrainMaterialization", "SettlementMaterialization",
            "BuildingMaterialization", "EnvironmentMaterialization", "WorldPlanChanged", "TerminalFailure", "PopulationComplete", "FinalReveal",
            "DuplicateAnswerLines", "GenericAnswerLines", "ThoughtfulAnswerLines", "ThemeAnswerLines", "OriginStimulusLines",
            "SettlementPopulationCreating", "HostilePopulationCreating" };
        int checkedLines = 0;
        foreach (string pool in pools)
        {
            // note: Reflection only inspects private test data; the production selection path still uses typed fields.
            var lines = (string[])typeof(YQGoddessLoadingVoice).GetField(pool,
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            Require(lines.Length >= 3, "Insufficient fallback variety: " + pool);
            foreach (string line in lines)
            {
                Require(YQGoddessGenerationDialogue.IsSpokenVoiceFieldAcceptable(string.Format(line, "Alderbrook"), 6),
                    "Fallback violates shared speech contract: " + pool);
                Require(!line.StartsWith("Behold", StringComparison.Ordinal) && !line.Contains("upside down") && !line.Contains("overlapping"),
                    "Construction slapstick survived in main fallback pool: " + pool);
                checkedLines++;
            }
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/YQGoddessProseStyle.txt", "PASS: shared persona, requested prose, narration/technical rejection, generated-prose preservation, personalized welcome and " +
            checkedLines + " fallback lines. No live model or voice-performance verification.\n\nFallback welcome:\n" + origin.completion);
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Goddess Cell Intent Contract")]
    public static void VerifyGoddessCellIntentContract()
    {
        // note: Exercise generated JSON through normalization and actual selector inputs, without requesting model output or reading a player save.
        var town = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSettlementRecord>(
            "{\"cellRoleIntents\":[\" DEFENSE \",\"service\",\"defense\",\"Assets/evil.prefab\",\"invented_role\"]}");
        town.cellRoleIntents = YQCompiledWorldSiteBindingService.NormalizeCellRoleIntents(town.cellRoleIntents);
        Require(town.cellRoleIntents.Count == 2 && town.cellRoleIntents[0] == "defense" && town.cellRoleIntents[1] == "service",
            "Unsafe/duplicate cell roles survived normalization.");
        string[] tags = YQCompiledWorldSiteBindingService.BuildSettlementSemanticSliceTags(town);
        Require(tags[0] == "defense" && tags[1] == "service" && Array.IndexOf(tags, "residential") >= 0 &&
            Array.IndexOf(tags, "circulation") >= 0, "Generated priorities were discarded or displaced baseline roles.");
        var camp = new GeneratedEncampmentRecord { cellRoleIntents = new List<string> { "reward", "defense" } };
        Require(YQCompiledWorldSiteBindingService.BuildEncampmentSemanticSliceTags(camp)[0] == "reward", "Hostile cell priorities were discarded.");
        Require(YQCompiledWorldSiteBindingService.BuildSettlementSemanticSliceTags(new GeneratedSettlementRecord())[0] == "poi",
            "Legacy role ordering changed.");
        var replay = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSettlementRecord>(Newtonsoft.Json.JsonConvert.SerializeObject(town));
        Require(replay.cellRoleIntents.Count == 2 && replay.cellRoleIntents[0] == "defense" && replay.cellRoleIntents[1] == "service",
            "Generated cell priorities were not persisted.");
        Require(YQCompiledWorldSiteBindingService.NormalizeCellRoleIntents(YQCompiledWorldSiteBindingService.CellRoleVocabulary.Split(',')).Count == 6,
            "Role request budget is unbounded.");
        var root = Newtonsoft.Json.Linq.JObject.Parse("{\"SpatialPlanV2\":{},\"settlements\":[{\"RuntimeSiteKitId\":\"forced\",\"proceduralLayout\":{},\"cellRoleIntents\":[\"service\"]}],\"encampments\":[{\"runtimeSiteBindingVersion\":\"forged\",\"siteStyleIntent\":\"rural\"}]}");
        // note: Reflection is test-only access to the parser boundary, not a runtime behavior mechanism.
        typeof(YQWorldGenerationService).GetMethod("RemoveGeneratedRuntimeAuthority",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { root });
        Require(root["SpatialPlanV2"] == null && root["settlements"][0]["RuntimeSiteKitId"] == null &&
            root["settlements"][0]["proceduralLayout"] == null && root["encampments"][0]["runtimeSiteBindingVersion"] == null,
            "Model output retained engine-owned runtime authority.");
        Require(root["settlements"][0]["cellRoleIntents"] != null && (string)root["encampments"][0]["siteStyleIntent"] == "rural",
            "Sanitization removed valid semantic intent.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/YQGoddessCellIntentContract.txt",
            "PASS: generated JSON roles are normalized, bounded, persisted and consumed as settlement/hostile selector priorities; baseline/legacy roles preserved; model-supplied runtime authority removed. No live LLM or rendered-world verification.");
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Portal Selection")]
    public static void VerifyPortalSelection()
    {
        // note: A transient hierarchy exercises real Transform lookup without editing imported prefabs or entering Play mode.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Portal verification requires Edit mode.");
        GameObject root = new GameObject("Portal selection fixture");
        var manifest = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        try
        {
            Transform outside = new GameObject("Outside").transform;
            outside.SetParent(root.transform, false);
            outside.localPosition = new Vector3(0, 0, 10);
            Transform inset = new GameObject("Inset").transform;
            inset.SetParent(root.transform, false);
            inset.localPosition = new Vector3(0, 0, 4);
            var zone = new YQReviewedSemanticZoneRecord
            {
                stableId = "portal-fixture", prefab = root,
                localBoundsCenter = new Vector3(0, 3, 0), localBoundsSize = new Vector3(12, 6, 20),
                connectionSocketPaths = new List<string> { "Inset", "Outside", "", "Missing" }
            };
            manifest.Configure("portal-fixture", "fixture", 1, new[] { zone }, false);
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { zone.stableId };
            for (int seed = 0; seed < 64; seed++)
            {
                string key = YQProceduralSettlementLayout.SeedPrefix + "portal-fixture|" + seed;
                Require(YQProceduralSettlementLayout.TryResolve(manifest, selected, key, out var layout, out string failure), failure);
                Require(layout != null && layout.cells.Count == 1, "A valid declared entrance did not activate composition.");
                string first = YQProceduralSettlementLayout.GeometrySignature(layout);
                zone.connectionSocketPaths.Reverse();
                Require(YQProceduralSettlementLayout.TryResolve(manifest, selected, key, out var replay, out failure), failure);
                Require(first == YQProceduralSettlementLayout.GeometrySignature(replay), "Portal list order changed geometry.");
            }
            zone.connectionSocketPaths = new List<string> { "Inset", "Missing", "" };
            Require(!YQProceduralSettlementLayout.TryResolve(manifest, selected,
                YQProceduralSettlementLayout.SeedPrefix + "portal-fixture|invalid", out _, out _),
                "An all-invalid portal set fell back silently to authored placement.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQPortalSelectionVerification.txt",
                "PASS: 64 real-Transform portal selections, reordered replay, invalid/missing/empty path filtering and all-invalid rejection. Not a collision clearance certificate.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(manifest);
        }
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Site Binding Alternatives")]
    public static void VerifyBindingAlternatives()
    {
        // note: Test with an in-memory catalog and records only; never replace bindings in a loaded profile or run while gameplay owns the cache.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Binding verification requires Edit mode.");
        var cache = typeof(YQCompiledWorldSiteBindingService).GetField("catalog",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        object previous = cache.GetValue(null);
        var fixture = ScriptableObject.CreateInstance<YQRuntimeWorldSiteCatalog>();
        try
        {
            cache.SetValue(null, fixture);
            foreach (bool hostile in new[] { false, true })
            {
                var first = new YQRuntimeWorldSiteRecord { kitId = "first", semanticStyleKey = "rural",
                    spatiallyValidated = true, seamlessPlacementEligible = true,
                    presentationMode = YQWorldSitePresentationMode.SeamlessExterior,
                    siteKind = hostile ? YQAuthoredSiteKind.Camp : YQAuthoredSiteKind.Settlement };
                var second = new YQRuntimeWorldSiteRecord { kitId = "second", semanticStyleKey = "rural",
                    spatiallyValidated = true, seamlessPlacementEligible = true,
                    presentationMode = YQWorldSitePresentationMode.SeamlessExterior, siteKind = first.siteKind };
                fixture.Configure(new[] { first, second });
                var town = new GeneratedSettlementRecord { settlementId = "test", runtimeSiteKitId = "first",
                    runtimeSiteBindingVersion = YQCompiledWorldSiteBindingService.BindingVersion };
                var camp = new GeneratedEncampmentRecord { encampmentId = "test", runtimeSiteKitId = "first",
                    runtimeSiteBindingVersion = YQCompiledWorldSiteBindingService.BindingVersion };
                var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    bool found;
                    YQRuntimeWorldSiteRecord selected = null;
                    bool changed = false;
                    if (hostile)
                        found = YQCompiledWorldSiteBindingService.TryResolveEncampmentSite(null, camp, null, null,
                            out selected, out changed, excluded, false);
                    else
                        found = YQCompiledWorldSiteBindingService.TryResolveSettlementSite(null, town, null, null,
                            out selected, out changed, excluded, false);
                    Require(!changed && town.runtimeSiteKitId == "first" && camp.runtimeSiteKitId == "first",
                        "A selection-only probe changed persisted metadata.");
                    Require(found == (attempt < 2), "Excluded candidates were not exhausted correctly.");
                    if (attempt < 2)
                    {
                        Require(selected.kitId == (attempt == 0 ? "first" : "second"), "Binding order or saved preference changed.");
                        excluded.Add(selected.kitId);
                    }
                    else Require(selected == null, "Rejected prior binding leaked out as a successful alternative.");
                }
            }
            Debug.Log("PASS: settlement/camp alternatives, saved preference, excluded-kit exhaustion and non-mutating probes.");
            // note: Leave a focused result independently of the real-library layout fixture, which has its own known failure.
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQSiteBindingAlternativesVerification.txt",
                "PASS: settlement/camp alternatives, saved preference, excluded-kit exhaustion and non-mutating probes.");
        }
        finally
        {
            // note: Even a failing assertion restores the live catalog cache and releases the transient fixture.
            cache.SetValue(null, previous);
            UnityEngine.Object.DestroyImmediate(fixture);
        }
    }

    private const string Request = "Temp/YQProceduralSettlementLayoutVerification.request";
    private static double nextRequestCheck;
    private static bool processingRequest;
    [InitializeOnLoadMethod]
    private static void CheckRequest()
    {
        // note: Requests can arrive after domain reload or while Play mode is active. A single delayCall drops both cases permanently.
        EditorApplication.update -= PollRequests;
        EditorApplication.update += PollRequests;
        AssemblyReloadEvents.beforeAssemblyReload -= StopRequestPolling;
        AssemblyReloadEvents.beforeAssemblyReload += StopRequestPolling;
    }

    private static void StopRequestPolling()
    {
        // note: Explicitly release subscriptions before scripts reload; never register duplicate editor workers.
        EditorApplication.update -= PollRequests;
        AssemblyReloadEvents.beforeAssemblyReload -= StopRequestPolling;
    }

    private static IEnumerator ReloadAfterFreshWorldRequest()
    {
        float deadline = Time.realtimeSinceStartup + 95f;
        while (YQWorldGenerationService.Instance != null &&
               YQWorldGenerationService.Instance.IsRequestInFlight &&
               Time.realtimeSinceStartup < deadline)
        {
            // note: Keep the existing generation callback authoritative while the fresh profile remains visible in the editor.
            yield return null;
        }

        if (YQProfileSaveSystem.Instance != null)
            YQProfileSaveSystem.Instance.SaveActiveProfile();
        // note: Even a timed-out optional request reloads through the normal startup path; the verification log will expose whether authored V2 hydrology arrived.
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private static void PollRequests()
    {
        if (processingRequest || EditorApplication.timeSinceStartup < nextRequestCheck)
            return;
        nextRequestCheck = EditorApplication.timeSinceStartup + 1d;
        // note: Explicit local verification commands operate the existing editor and title flow; the opt-in new-world command is the sole exception and creates a separate verification profile.
        const string startupRequest = "Temp/YQStartupVerification.request";
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(startupRequest))
        {
            string command = File.ReadAllText(startupRequest).Trim();
            File.Delete(startupRequest);
            try
            {
                if (command == "stop") EditorApplication.isPlaying = false;
                else if (command == "refresh") AssetDatabase.Refresh();
                else if (command == "clear-isolated-start")
                {
                    // note: Remove any stale isolated traversal start scene before production Play mode bootstraps its save managers.
                    UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
                }
                else if (command == "open-startup" && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // note: Load the existing startup scene only when doing so cannot discard unsaved scene edits.
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                        throw new InvalidOperationException("Save the current scene before opening startup verification.");
                    // note: Clear a prior isolated traversal fixture so the next Play mode session restores the production save/bootstrap owners.
                    UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Assets/Scenes/YourQuest_PlaySafe.unity");
                }
                else if (command == "play" && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = true;
                else if (command == "new-world" && EditorApplication.isPlaying)
                {
                    if (YQProfileSaveSystem.Instance == null)
                        throw new InvalidOperationException("Profile save system is unavailable for a fresh world.");
                    // note: A fresh profile resets only the active verification journey, preserving the previous accepted save for manual comparison.
                    string profileId = YQProfileSaveSystem.Instance.CreateNewProfile("Dynamic World Verification");
                    if (string.IsNullOrWhiteSpace(profileId))
                        throw new InvalidOperationException("Fresh verification profile could not be created.");
                    PlayerState freshPlayer = PlayerStateManager.Instance != null
                        ? PlayerStateManager.Instance.state
                        : null;
                    WorldStateManager freshWorldManager = WorldStateManager.Instance;
                    if (freshPlayer == null || freshWorldManager == null || freshWorldManager.State == null ||
                        YQWorldGenerationService.Instance == null)
                        throw new InvalidOperationException("Fresh world state managers are unavailable.");
                    // note: Seed the new profile through the normal world-plan service before reload; the authored request is allowed to finish so V2 hydrology is present in this dynamic-world verification.
                    YQWorldGenerationService.Instance.RegenerateAfterOrigin(
                        freshPlayer,
                        freshWorldManager.State,
                        true);
                    freshWorldManager.Save();
                    // note: Reload only after the authored plan settles, preventing a new scene from racing the LLM callback and rebuilding the old fallback.
                    YQWorldGenerationService.Instance.StartCoroutine(
                        ReloadAfterFreshWorldRequest());
                }
                else if (command == "continue" && EditorApplication.isPlaying)
                {
                    // note: Use the same selected-profile action as the title button, preserving normal loading, validation and player ownership.
                    // note: Domain reloads can clear the title singleton while the existing scene object is already present; reacquire that same object instead of creating a second startup UI.
                    var title = YQTitleScreenUI.Instance != null
                        ? YQTitleScreenUI.Instance
                        : UnityEngine.Object.FindFirstObjectByType<YQTitleScreenUI>();
                    if (title == null || YQTitleScreenUI.StartupFlowComplete)
                        throw new InvalidOperationException("The title is not awaiting a selected save.");
                    // note: Reacquire the title's normal active-profile selection when the verification command arrives before its presentation finishes opening.
                    typeof(YQTitleScreenUI).GetMethod("RefreshSelectedProfile",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(title, null);
                    var action = typeof(YQTitleScreenUI).GetMethod("ContinueSelected",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (action == null) throw new InvalidOperationException("Title continue action is unavailable.");
                    action.Invoke(title, null);
                    // note: A domain reload can leave the title's private selection blank even though the persisted active profile is valid; verification may hand that same profile back through the normal completion callback without creating or replacing a save.
                    if (!YQTitleScreenUI.StartupFlowComplete &&
                        YQProfileSaveSystem.Instance != null &&
                        !string.IsNullOrWhiteSpace(YQProfileSaveSystem.Instance.ActiveProfileId))
                    {
                        string activeProfileId = YQProfileSaveSystem.Instance.ActiveProfileId;
                        if (YQProfileSaveSystem.Instance.LoadProfile(activeProfileId))
                        {
                            typeof(YQTitleScreenUI).GetMethod("CompleteStartupFlow",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(title, new object[] { "Loaded save." });
                        }
                    }
                }
                else if (command == "continue-active" && EditorApplication.isPlaying)
                {
                    // note: Verification can complete the normal title gate from the persisted active profile when the visual menu has not populated its selection after a domain reload.
                    var title = YQTitleScreenUI.Instance != null
                        ? YQTitleScreenUI.Instance
                        : UnityEngine.Object.FindFirstObjectByType<YQTitleScreenUI>();
                    var system = YQProfileSaveSystem.Instance;
                    string activeId = system != null ? system.ActiveProfileId : string.Empty;
                    if (string.IsNullOrWhiteSpace(activeId) && system != null && system.Profiles.Count > 0)
                        activeId = system.Profiles[system.Profiles.Count - 1].profileId;
                    if (title == null || system == null || string.IsNullOrWhiteSpace(activeId) || !system.LoadProfile(activeId))
                        throw new InvalidOperationException("No persisted active profile could be loaded.");
                    // note: Reopen the title component after a domain reload so its exit transition and startup gate update loop are active.
                    title.OpenAtStartup();
                    typeof(YQTitleScreenUI).GetMethod("CompleteStartupFlow",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(title, new object[] { "Loaded active save." });
                    // note: Complete the static gate directly for editor verification when a retained title instance does not tick its exit animation after a domain reload.
                    typeof(YQTitleScreenUI).GetProperty("StartupFlowComplete",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, true);
                }
                else if (command == "bootstrap-now" && EditorApplication.isPlaying)
                {
                    // note: Restart the existing production bootstrap after a retained editor session leaves its coroutine dormant; no alternate world path is created.
                    var bootstrap = UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialAutoBootstrap>();
                    if (bootstrap == null)
                        throw new InvalidOperationException("Production bootstrap is unavailable.");
                    typeof(YQTitleScreenUI).GetProperty("StartupFlowComplete",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, true);
                    // note: The normal Awake coroutine is already waiting on this gate; do not start a second bootstrap routine or duplicate managers.
                    var title = YQTitleScreenUI.Instance != null
                        ? YQTitleScreenUI.Instance
                        : UnityEngine.Object.FindFirstObjectByType<YQTitleScreenUI>();
                    title?.OpenAtStartup();
                    typeof(YQTitleScreenUI).GetProperty("StartupFlowComplete",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, true);
                }
                else if (command == "force-production-build" && EditorApplication.isPlaying)
                {
                    // note: Run the same production service wiring synchronously when editor title coroutines are stalled, preserving the authoritative player and save owners.
                    var bootstrap = UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialAutoBootstrap>();
                    var worldBuilder = YQGeneratedWorldRuntimeBuilder.Instance;
                    if (bootstrap == null || worldBuilder == null)
                        throw new InvalidOperationException("Production bootstrap or world builder is unavailable.");
                    string[] stages = { "EnsureGameplayServices", "EnsureRuntimeWorld", "EnsureGameplayPresentationServices", "EnsureRuntimeUi", "SeedData", "WireReferences" };
                    for (int index = 0; index < stages.Length; index++)
                        typeof(YourQuestTutorialAutoBootstrap).GetMethod(stages[index],
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(bootstrap, null);
                    typeof(YourQuestTutorialAutoBootstrap).GetProperty("GameplayRuntimeReady",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, true);
                    worldBuilder.BuildGeneratedWorld();
                }
                else if (command == "force-build" && EditorApplication.isPlaying)
                {
                    var runtimeBuilder = YQGeneratedWorldRuntimeBuilder.Instance;
                    if (runtimeBuilder == null)
                        throw new InvalidOperationException("Runtime world builder is unavailable.");
                    if (!YourQuestTutorialAutoBootstrap.GameplayRuntimeReady ||
                        GameObject.FindGameObjectWithTag("Player") == null)
                    {
                        // note: Manual verification must wait for the production bootstrap to publish its one authoritative player; otherwise the origin stage is falsely reported as broken by a bypassed startup sequence.
                        throw new InvalidOperationException("Production gameplay bootstrap is not ready; use continue and retry after the authoritative player is published.");
                    }
                    // note: Verification-only trigger invokes the normal production materialization path without bypassing validation or enabling fallback generation.
                    runtimeBuilder.BuildGeneratedWorld();
                }
                else if (command == "visual-audit" && EditorApplication.isPlaying) WriteVisualAudit();
                else if (command == "cancel-material-conversion" && !EditorApplication.isPlaying)
                {
                    // note: Expose a safe verification-only cancellation path so an already-running opt-in conversion can finish its current callback and stop without saving another batch.
                    typeof(YQUrpAssetConversionBatch).GetMethod("CancelConversion",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.Invoke(null, null);
                }
                // note: Reproduce confluence and bridge failures using fresh live-camera evidence and source-material diagnostics.
                else if (command == "water-bridge-review" && EditorApplication.isPlaying) YQWorldPresentationReview.CaptureWaterAndBridges();
                else if (command == "water-bridge-fixtures") YQWorldPresentationReview.VerifyWaterBridgeRegressionFixtures();
                else if (command == "capture-map" && EditorApplication.isPlaying)
                {
                    // note: Capture the active generated map and bounded placement evidence from the current playable world.
                    CaptureLiveWorldPlacement();
                }
                else if (command == "rebuild-registry" && !EditorApplication.isPlaying)
                {
                    // note: Rebuild runtime shards after eligibility changes so newly accepted human prefabs are available to procedural population.
                    YQRuntimeWorldAssetRegistryBuilder.RebuildDiscoveredRuntimeEligibility();
                }
            else if (command == "presentation-review" && EditorApplication.isPlaying) YQWorldPresentationReview.Capture();
            else if (command == "bridge-review") YQWorldPresentationReview.ReviewBridge();
            // note: Validate generated bridge seams in Play mode without consuming or modifying the accepted save.
            else if (command == "bridge-deck-check" && EditorApplication.isPlaying) YQWorldPresentationReview.VerifyGeneratedBridgeDeck();
                else if (command != "status") throw new InvalidOperationException("Unsupported startup verification command or editor state.");
                Directory.CreateDirectory("Logs");
                var builder = YQGeneratedWorldRuntimeBuilder.Instance;
                File.WriteAllText("Logs/YQStartupVerification.txt", "Command=" + command +
                    "\nPlaying=" + EditorApplication.isPlaying +
                    "\nPaused=" + EditorApplication.isPaused + "\nFrame=" + Time.frameCount +
                    "\nTitleComplete=" + YQTitleScreenUI.StartupFlowComplete +
                    "\nMaterialized=" + (builder != null && builder.HasMaterializedCurrentWorld) +
                    "\nRecoveryRequired=" + (builder != null && builder.InitialGenerationRecoveryRequired) +
                    "\nGameplayLocked=" + YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked +
                    "\nPresentationReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/YQStartupVerification.txt", "FAILED: " + exception);
            }
            return;
        }
        // note: Only inspect explicit request files while the editor is idle; no filesystem scans or gameplay work runs here.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            YQCandidateInteriorBuilderV2.AccessibilityRepairRunning)
            return;
        processingRequest = true;
        try
        {
            // note: Service explicit probe checks after an ordinary refresh too, without requiring an editor restart.
            if (RunRequest("Temp/YQRouteTraversalProbeVerification.request", "Logs/YQRouteTraversalProbeVerification.txt",
                    YQRouteTraversalProbeVerification.Verify)) return;
            if (RunRequest("Temp/YQRefreshJoistedHomeManifest.request", "Logs/YQJoistedHomeManifestRefresh.txt",
                    YQCandidateInteriorBuilderV2.RefreshJoistedHomeManifest)) return;
            if (RunRequest("Temp/YQRefreshTimberHomeManifest.request", "Logs/YQTimberHomeManifestRefresh.txt",
                    YQCandidateInteriorBuilderV2.RefreshTimberHomeManifest)) return;
            // note: Create the requested standalone traversal fixture only after this editor has compiled its runtime harness.
            if (RunRequest("Temp/YQPublishQualifiedEncounter.request", "Logs/YQQualifiedHomesteadEncounter.txt",
                    YQCandidateInteriorBuilderV2.PublishQualifiedHomesteadEncounter)) return;
            if (RunRequest("Temp/YQPublishQualifiedHome.request", "Logs/YQQualifiedHomePublication.txt",
                    YQCandidateInteriorBuilderV2.PublishQualifiedHomeLibrary)) return;
            if (RunRequest("Temp/YQPrimaryHomeQualification.request", "Logs/YQPrimaryHomeQualification.txt", () =>
                {
                    // note: Qualify the existing assembly through its real providers and production parcel placement; no approval metadata or live saves are changed.
                    YQCandidateInteriorBuilderV2.VerifyJoistedFrameContacts();
                    YQCandidateInteriorBuilderV2.VerifyJoistedHomeAccess();
                    YQCandidateInteriorBuilderV2.VerifyJoistedHomeProviders();
                    YQCandidateInteriorBuilderV2.VerifyJoistedTerrainIntegration();
                    YQCandidateInteriorBuilderV2.VerifyProductionJoistedStreet();
                    File.WriteAllText("Logs/YQPrimaryHomeQualification.txt", "PASS: existing home geometry, access, terrain contact and production street placement checks completed. Runtime release remains separate.");
                })) return;
            if (RunRequest("Temp/YQFoundationContactSolver.request", "Logs/YQFoundationContactSolver.txt",
                    VerifyFoundationContactSolver)) return;
            // note: Run only the current terrain-prepass regression without reopening waived legacy world gates.
            if (RunRequest("Temp/YQSectorTerrainPrepass.request", "outputs/G08_Environment_Repair_20261005/SectorTerrainPrepassFailure.txt",
                    VerifySectorTerrainPrepass)) return;
            if (RunRequest("Temp/YQParcelTerrainWrites.request", "Logs/YQParcelTerrainWritesFailure.txt",
                    VerifyParcelTerrainWrites)) return;
            if (RunRequest("Temp/YQStreetConnectedHomeReview.request", "Logs/YQStreetConnectedHomeReviewFailure.txt",
                    YQCandidateInteriorBuilderV2.ReviewStreetConnectedHome)) return;
            if (RunRequest("Temp/YQStoneFoundationHome.request", "Logs/YQStoneFoundationHomeFailure.txt",
                    YQCandidateInteriorBuilderV2.BuildStoneFoundationCandidate)) return;
            if (RunRequest("Temp/YQStoneFoundationCoverage.request", "Logs/YQStoneFoundationCoverageFailure.txt",
                    YQCandidateInteriorBuilderV2.VerifyStoneFoundationCoverage)) return;
            if (RunRequest("Temp/YQFoundationSources.request", "Logs/YQFoundationSourcesFailure.txt",
                    YQCandidateInteriorBuilderV2.ReviewFoundationSources)) return;
            if (RunRequest("Temp/YQTimberFoundationHome.request", "Logs/YQTimberFoundationHomeFailure.txt",
                    YQCandidateInteriorBuilderV2.BuildTimberFoundationCandidate)) return;
            if (RunRequest("Temp/YQTimberHomeAccess.request", "Logs/YQTimberHomeAccessFailure.txt",
                    YQCandidateInteriorBuilderV2.VerifyTimberHomeAccess)) return;
            if (RunRequest("Temp/YQGroundedTimberHome.request", "Logs/YQGroundedTimberHomeFailure.txt",
                    YQCandidateInteriorBuilderV2.ReviewGroundedTimberHome)) return;
            if (RunRequest("Temp/YQPlayableHomeReview.request", "Logs/YQPlayableHomeReviewFailure.txt",
                    YQCandidateInteriorBuilderV2.BuildPlayableHomeTraversalReview)) return;
            // note: Execute only this explicit functional review request after compilation and outside gameplay.
            if (RunRequest("Temp/YQHomeFunctionalAccess.request", "Logs/YQHomeFunctionalAccessFailure.txt",
                    YQCandidateInteriorBuilderV2.VerifyHomeFunctionalAccess)) return;
            // note: Run the bounded real-asset check in an already open editor without starting a competing Unity process or entering Play mode.
            if (RunRequest("Temp/YQRealAssemblyFrontageV3.request", "Logs/YQRealAssemblyConstructionFailure.txt",
                    VerifyRealAssemblyConstruction)) return;
            if (RunRequest("Temp/YQSettlementRepairBoundaries.request", "Logs/YQSettlementRepairBoundaries.txt",
                    VerifySettlementRepairBoundaries)) return;
            if (RunRequest("Temp/YQAssemblyDatumAndSaveVoice.request", "Logs/YQAssemblyDatumAndSaveVoice.txt",
                    VerifyAssemblyDatumAndSaveVoice)) return;
            // note: Finish geometry before dependent metadata if both requests are queued. At most one request is dispatched per tick.
            if (RunRequest("Temp/YQInteriorAccessibilityRepair.request", "Logs/YQInteriorAccessibilityRepair.txt",
                    YQCandidateInteriorBuilderV2.StartAccessibilityRepair)) return;
            if (RunRequest("Temp/YQAccessibleCandidateBindings.request", "Logs/YQAccessibleCandidateBindings.txt",
                    YQCandidateInteriorBuilderV2.PrepareAccessibleCandidateBindings)) return;
            if (RunRequest("Temp/YQSiteBindingAlternativesVerification.request", "Logs/YQSiteBindingAlternativesVerification.txt", () =>
                {
                    VerifyBindingAlternatives();
                    YQSemanticSiteProductionCompiler.VerifyClassificationIntegrity();
                    VerifyPortalSelection();
                })) return;
            if (RunRequest("Temp/YQSpatialBlueprintV2Tests.request", "Logs/YQSpatialBlueprintV2Tests.txt", () =>
                {
                    // note: Run the focused spatial contract set in the already loaded editor so natural-feature access is verified without starting Play Mode or touching a save.
                    int failures = YQSpatialBlueprintV2Tests.RunAll(out int tested);
                    File.WriteAllText("Logs/YQSpatialBlueprintV2Tests.txt",
                        "Tested=" + tested + "\nFailures=" + failures + "\n");
                    if (failures != 0)
                        throw new InvalidOperationException("Focused spatial blueprint contracts failed: " + failures);
                })) return;
            RunRequest(Request, "Logs/YQProceduralSettlementLayoutVerification.txt", Verify);
        }
        finally { processingRequest = false; }
    }

    private static void WriteVisualAudit()
    {
        // note: Inspect only the generated scene on explicit request; report water/roads above terrain and actual hut material state.
        var report = new System.Text.StringBuilder();
        var terrain = Terrain.activeTerrain;
        // note: The audit can arrive during a domain-reload handoff, so resolve the persisted plan defensively instead of dereferencing a transient singleton.
        var worldManager = WorldStateManager.Instance;
        var generatedPlan = worldManager != null && worldManager.State != null
            ? worldManager.State.generatedWorldPlan
            : null;
        YQSpatialBlueprintTerrainSamplerV2.TryPrepare(generatedPlan, out var sampler, out _);
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            string path = renderer.name;
            for (var parent = renderer.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            if (!path.Contains("YQ_GENERATED_WORLD_RUNTIME")) continue;
            bool water = path.Contains("Water") || path.Contains("LivedPath");
            bool origin = path.Contains("Origin_");
            if (!water && !origin) continue;
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            var b = renderer.bounds;
            float gap = terrain != null ? b.min.y - terrain.SampleHeight(b.center) - terrain.transform.position.y : 0f;
            report.AppendLine(path + " bounds=" + b + " centerGroundGap=" + gap + " scale=" + renderer.transform.lossyScale);
            if (water && terrain != null && renderer.TryGetComponent<MeshFilter>(out var waterFilter) && waterFilter.sharedMesh != null &&
                (renderer.name == "CompiledHydrologySurface" || renderer.name == "CuratedEllipticalWaterSurface"))
            {
                // note: Check the actual shoreline geometry against final terrain, including points between sparse river controls.
                var vertices = waterFilter.sharedMesh.vertices;
                float maxBankGap = float.NegativeInfinity;
                Vector3 worstPoint = Vector3.zero;
                int unsupported = 0, samples = 0;
                bool ribbon = renderer.name == "CompiledHydrologySurface";
                for (int i = ribbon ? 0 : 1; i < vertices.Length; i++)
                {
                    int next = ribbon ? i + 2 : (i + 1 < vertices.Length ? i + 1 : 1);
                    if (next >= vertices.Length) continue;
                    Vector3 a = renderer.transform.TransformPoint(vertices[i]);
                    Vector3 c = renderer.transform.TransformPoint(vertices[next]);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, c) / 2f));
                    for (int s = 0; s <= steps; s++)
                    {
                        Vector3 p = Vector3.Lerp(a, c, s / (float)steps);
                        // note: A river edge submerged inside its receiving lake is a confluence, not an exposed shoreline.
                        if (ribbon && sampler != null && int.TryParse(waterFilter.sharedMesh.name.Split('_')[2], out int bodyIndex) &&
                            sampler.Sample(p.x, p.z).waterFeatureIndex != bodyIndex) continue;
                        float bankGap = p.y - terrain.SampleHeight(p) - terrain.transform.position.y;
                        if (bankGap > maxBankGap) { maxBankGap = bankGap; worstPoint = p; }
                        if (bankGap > .35f) unsupported++;
                        samples++;
                    }
                }
                report.AppendLine("  SHORELINE samples=" + samples + " unsupported=" + unsupported + " maxGap=" + maxBankGap);
                if (sampler != null)
                {
                    // note: Compare preconstruction and final heights at the same failing point to locate which pass removes the bank.
                    var planned = sampler.Sample(worstPoint.x, worstPoint.z);
                    report.AppendLine("  WORST " + worstPoint + " plannedTerrain=" + planned.elevationNormalized * terrain.terrainData.size.y +
                        " water=" + planned.waterSurfaceNormalized * terrain.terrainData.size.y + " mask=" + planned.waterMask +
                        " road=" + planned.routeMask + " reserve=" + planned.siteReserveMask);
                }
            }
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                report.Append("  " + material.name + " shader=" + material.shader.name + " queue=" + material.renderQueue);
                foreach (string property in new[] { "_Surface", "_Mode", "_ZWrite", "_ZTest", "_Cull", "_SrcBlend", "_DstBlend" })
                    if (material.HasProperty(property)) report.Append(" " + property + "=" + material.GetFloat(property));
                report.AppendLine();
            }
        }
        File.WriteAllText("Logs/YQVisualAudit.txt", report.ToString());
    }

    private static bool RunRequest(string path, string reportPath, Action action)
    {
        if (!File.Exists(path)) return false;
        try
        {
            // note: Claim exactly once before execution; a failed action produces a report instead of being retried every second.
            File.Delete(path);
            // note: Do not issue apparently fresh evidence from an editor assembly older than the construction code under review.
            RequireCurrentReviewAssembly(typeof(YQCandidateInteriorBuilderV2),
                "Assets/Assets/Scripts/Generated/Editor/YQCandidateInteriorBuilderV2.cs");
            RequireCurrentReviewAssembly(typeof(YQGeneratedWorldRuntimeBuilder),
                "Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs");
            RequireCurrentReviewAssembly(typeof(YQProceduralSettlementLayout),
                "Assets/Assets/Scripts/Generated/YQProceduralSettlementLayout.cs");
            action();
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(reportPath, "FAILED: " + exception);
            Debug.LogException(exception);
        }
        return true;
    }

    private static void RequireCurrentReviewAssembly(Type owner, string sourcePath)
    {
        // note: This conservative timestamp guard requests a refresh after local edits; it never recompiles or interrupts gameplay itself.
        string assemblyPath = owner.Assembly.Location;
        if (!File.Exists(sourcePath) || !File.Exists(assemblyPath) ||
            File.GetLastWriteTimeUtc(sourcePath) > File.GetLastWriteTimeUtc(assemblyPath))
            throw new InvalidOperationException("Review requires Assets > Refresh: loaded assembly is older than " + sourcePath);
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Procedural Settlement Layouts")]
    public static void Verify()
    {
        try
        {
            VerifyDemand();
            var cells = new List<YQProceduralSettlementLayout.Cell>();
            for (int i = 0; i < 4; i++)
                cells.Add(new YQProceduralSettlementLayout.Cell { id = "cell-" + i,
                    center = new Vector3(0, 3, 0), size = new Vector3(10 + i * 2, 6, 12 + i * 3),
                    entrance = new Vector3(0, 0, (12 + i * 3) * .5f), outward = Vector3.forward });
            var signatures = new HashSet<string>();
            for (int seed = 0; seed < 32; seed++)
            {
                string key = YQProceduralSettlementLayout.SeedPrefix + "fixture-" + seed;
                Require(YQProceduralSettlementLayout.TryBuild(cells, key, out var layout, out string failure), failure);
                Require(YQProceduralSettlementLayout.ValidateRecord(layout, out failure), failure);
                VerifyLoop(layout);
                string original = JsonUtility.ToJson(layout);
                cells.Reverse();
                Require(YQProceduralSettlementLayout.TryBuild(cells, key, out var replay, out failure), failure);
                Require(original == JsonUtility.ToJson(replay), "Same seed changed when source enumeration changed.");
                Require(original == JsonUtility.ToJson(JsonUtility.FromJson<YQProceduralSettlementLayoutRecord>(original)), "Saved layout round-trip changed geometry.");
                for (int a = 0; a < layout.cells.Count; a++)
                {
                    var left = layout.cells[a];
                    // note: Independent XZ rectangle checks prove physical block separation, not merely signature variation.
                    for (int b = a + 1; b < layout.cells.Count; b++)
                    {
                        var right = layout.cells[b];
                        Require(Mathf.Abs(left.boundsCenter.x - right.boundsCenter.x) >= (left.boundsSize.x + right.boundsSize.x) * .5f ||
                            Mathf.Abs(left.boundsCenter.z - right.boundsCenter.z) >= (left.boundsSize.z + right.boundsSize.z) * .5f,
                            "Procedural blocks overlap.");
                    }
                    Require(Mathf.Abs(left.boundsCenter.x) > left.boundsSize.x * .5f + 2f, "Block intrudes into main street.");
                }
                signatures.Add(YQProceduralSettlementLayout.GeometrySignature(layout));
            }
            Require(signatures.Count >= 16, "Seed corpus did not produce materially different geometry.");
            var legacy = new GeneratedSettlementRecord { settlementId = "old", deterministicSeed = "existing-save-seed" };
            Require(!YQProceduralSettlementLayout.Enabled(legacy.deterministicSeed) && legacy.proceduralLayout == null,
                "Legacy save was implicitly opted into new geometry.");
            var fresh = new GeneratedWorldPlanRecord { settlements = new List<GeneratedSettlementRecord> { legacy } };
            YQProceduralSettlementLayout.EnableNewWorld(fresh);
            Require(YQProceduralSettlementLayout.Enabled(legacy.deterministicSeed), "Fresh-world entry did not enable active layout generation.");
            string once = legacy.deterministicSeed;
            YQProceduralSettlementLayout.EnableNewWorld(fresh);
            Require(legacy.deterministicSeed == once, "Version prefix duplicated.");

            // note: Resolve the real runtime manifest and its declared portals, not another synthetic replacement asset library.
            var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(
                "Assets/Assets/Resources/YQWorldSites/medieval_viking_village/YQRuntimeSemanticSite.asset");
            Require(manifest != null, "Runtime Viking semantic manifest is missing.");
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var zone in manifest.Zones)
            {
                // note: Shell-only districts without a frontage socket remain explicitly unavailable instead of being forced into a fake street connection.
                if (zone?.prefab != null && YQProceduralSettlementLayout.HasUsableExternalConnection(zone))
                    selected.Add(zone.stableId);
            }
            Require(selected.Count > 0, "Published runtime manifest has no reviewed street-frontage cell.");
            Require(YQProceduralSettlementLayout.TryResolve(manifest, selected,
                YQProceduralSettlementLayout.SeedPrefix + "real-library-fixture", out var actual, out string sourceFailure), sourceFailure);
            Require(actual != null && actual.cells.Count == selected.Count, "Real declared portals did not activate procedural composition.");
            Require(YQProceduralSettlementLayout.ValidateRecord(actual, out sourceFailure), sourceFailure);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQProceduralSettlementLayoutVerification.txt", "PASS: 32 deterministic seed layouts, " + signatures.Count +
                " distinct geometries; nonoverlap, street clearance, save round-trip, explicit fresh-world opt-in; real library cells=" + actual.cells.Count +
                ", radius=" + actual.radius + ". Physical/visual gameplay acceptance still requires a fresh-world test.");
            Debug.Log("[YQProceduralSettlementLayoutVerification] PASS.");
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQProceduralSettlementLayoutVerification.txt", "FAIL: " + error);
            Debug.LogException(error);
            // note: Batch verification must exit unsuccessfully when production geometry fails, rather than logging FAIL and returning success to automation.
            throw;
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void VerifyDemand()
    {
        // note: Exercise scale using explicit numerical state, including duplicates, missing values and bounded city requests.
        Require(YQProceduralSettlementLayout.DesiredBlockCount(30, null) == 1, "Small hamlet demand is incorrect.");
        Require(YQProceduralSettlementLayout.DesiredBlockCount(500, null) == 3, "Town population demand is incorrect.");
        Require(YQProceduralSettlementLayout.DesiredBlockCount(9000, null) == 8, "Large settlement demand must be bounded.");
        Require(YQProceduralSettlementLayout.DesiredBlockCount(30, new[] { "trade", " TRADE ", "", null }) == 1,
            "Duplicate/empty services inflated demand.");
        Require(YQProceduralSettlementLayout.DesiredBlockCount(30, new[] { "a", "b", "c", "d", "e" }) == 3,
            "Distinct service demand was ignored.");
        Require(YQProceduralSettlementLayout.ResolveTargetCount(1, 4, 8) == 4, "Mandatory providers were dropped.");
        Require(YQProceduralSettlementLayout.ResolveTargetCount(8, 2, 3) == 3, "Capacity overflow fabricated extra cells.");
    }

    public static void VerifyLoop(YQProceduralSettlementLayoutRecord layout)
    {
        // note: Prove the four ring edges meet and intersect both ends of the main spine, not just that extra streets exist.
        if (layout.version < 2 || layout.cells.Count < 4) return;
        int first = layout.cells.Count + 1;
        Require(layout.streets.Count == first + 4, "Large layout omitted its loop.");
        for (int i = 0; i < 4; i++)
        {
            var edge = layout.streets[first + i];
            var next = layout.streets[first + (i + 1) % 4];
            Require((edge.end - next.start).sqrMagnitude < .0001f, "Loop has a disconnected corner.");
        }
        var spine = layout.streets[layout.cells.Count];
        Require(Mathf.Abs(spine.start.z - layout.streets[first].start.z) < .001f &&
            Mathf.Abs(spine.end.z - layout.streets[first + 2].start.z) < .001f, "Loop does not join both ends of the spine.");
    }
}
#endif
