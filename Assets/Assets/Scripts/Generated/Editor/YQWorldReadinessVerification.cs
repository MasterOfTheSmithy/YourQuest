using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: These detached fixtures exercise production entry points without selecting, saving, or regenerating a profile.
public static class YQWorldReadinessVerification
{
    [MenuItem("YourQuest/Verification/Verify Water Width and Material Reuse")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("World readiness fixtures require Edit Mode.");
        var report = new List<string> { "# World readiness: detached Edit fixtures",
            "utc=" + DateTime.UtcNow.ToString("O"),
            "runtimeMvid=" + typeof(YQContinuousWorldFeatureAuthority).Assembly.ManifestModule.ModuleVersionId };
        int checks = 0, failures = 0;
        void Check(bool ok, string label) { checks++; if (!ok) failures++; report.Add((ok ? "PASS " : "FAIL ") + label); }
        Scene scene = EditorSceneManager.NewPreviewScene();
        var owned = new List<UnityEngine.Object>();
        try
        {
            MethodInfo terminal = typeof(YQContinuousWorldFeatureAuthority).GetMethod(
                "TryApplyAcceptedWaterTerminalModifiers", BindingFlags.NonPublic | BindingFlags.Static);
            // note: Vary accepted widths, depths and uncarved hill height; sample both sides of the finite-to-continuation join.
            foreach (float width in new[] { 8f, 14f, 32f })
            foreach (float depth in new[] { 1f, 4.5f, 9f })
            foreach (float original in new[] { 0.5f, 0.9f })
            {
                var prepared = CreateRiver(width, depth);
                foreach (float fraction in new[] { -1f, -0.8f, 0f, 0.8f, 1f })
                {
                    float x = fraction * width * 0.5f;
                    bool influenced = YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterModifier(
                        prepared, x, 511f, original, 140f, out float finite);
                    object[] args = { prepared, x, 513f, original, 140f, 0f };
                    bool continued = (bool)terminal.Invoke(null, args);
                    float extended = (float)args[5];
                    string label = "width=" + width + " depth=" + depth + " base=" + original + " across=" + fraction;
                    Check(influenced && continued && finite < 0.4f && extended < 0.4f, "full wet width submerged " + label);
                    Check(Mathf.Abs(finite - extended) < 0.000001f, "finite/terminal cross-section agrees " + label);
                    if (fraction == 0f)
                        Check(Mathf.Abs((0.4f - extended) * 140f - depth) < 0.0001f, "accepted center depth " + label);
                }
                YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterModifier(prepared, width * 3f, 511f, original, 140f, out float dry);
                Check(dry == original, "distant terrain unchanged width=" + width);
            }

            Shader legacy = Shader.Find("Standard");
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp != null)
            {
                // note: Exercise production streamed mesh construction against a sloped, uneven terrain and variable accepted river widths.
                TerrainData groundData = new TerrainData { heightmapResolution = 65, size = new Vector3(128f, 140f, 128f) };
                owned.Add(groundData);
                float[,] heights = new float[65, 65];
                for (int z = 0; z < 65; z++) for (int x = 0; x < 65; x++)
                    heights[z, x] = 0.3f + x * 0.0015f + Mathf.Sin(z * 0.12f) * 0.03f;
                groundData.SetHeights(0, 0, heights);
                GameObject groundObject = Terrain.CreateTerrainGameObject(groundData);
                SceneManager.MoveGameObjectToScene(groundObject, scene);
                Terrain ground = groundObject.GetComponent<Terrain>();
                GameObject host = new GameObject("StreamedContactFixture");
                SceneManager.MoveGameObjectToScene(host, scene);
                Material surface = new Material(urp);
                owned.Add(surface);
                MethodInfo build = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("BuildRibbonSpans", BindingFlags.NonPublic | BindingFlags.Static);
                var points = new List<Vector3>();
                for (int z = 12; z <= 116; z += 8) points.Add(new Vector3(64f, 100f, z));
                int built = (int)build.Invoke(null, new object[] { host.transform, "Road", points, null, 8f, surface, false, 128f, null, ground });
                Check(built == 1, "terrain-following road emitted");
                Mesh roadMesh = host.transform.Find("Road_Span0").GetComponent<MeshFilter>().sharedMesh;
                foreach (Vector3 vertex in roadMesh.vertices)
                {
                    float offset = vertex.y - ground.SampleHeight(vertex);
                    Check(offset > 0f && offset < 0.15f, "streamed road vertex contacts sloped terrain");
                }
                var widths = new List<float> { 8f, 24f };
                points = new List<Vector3> { new Vector3(64f, 100f, 12f), new Vector3(64f, 100f, 116f) };
                built = (int)build.Invoke(null, new object[] { host.transform, "River", points, null, 8f, surface, true, 128f, widths, null });
                Check(built == 0, "unsampled long river rejects discontinuity");
                points = new List<Vector3> { new Vector3(64f, 100f, 12f), new Vector3(64f, 100f, 28f) };
                built = (int)build.Invoke(null, new object[] { host.transform, "RiverWidths", points, null, 8f, surface, true, 128f, widths, null });
                Mesh riverMesh = host.transform.Find("RiverWidths_Span0").GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = riverMesh.vertices;
                Check(built == 1 && Mathf.Abs(Vector3.Distance(vertices[0], vertices[1]) - 8f) < 0.001f &&
                    Mathf.Abs(Vector3.Distance(vertices[2], vertices[3]) - 24f) < 0.001f, "river retains per-point widths");
            }
            Check(legacy != null && urp != null, "fixture shaders available");
            if (legacy != null && urp != null)
            {
                var root = new GameObject("GeneratedStructure_Fixture");
                SceneManager.MoveGameObjectToScene(root, scene);
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "SM_Wall_Fixture";
                cube.transform.SetParent(root.transform, false);
                var source = new Material(legacy) { name = "Wood_Source", color = new Color(0.6f, 0.5f, 0.4f) };
                owned.Add(source);
                Renderer renderer = cube.GetComponent<Renderer>();
                renderer.sharedMaterial = source;
                int first = YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(root);
                Material repaired = renderer.sharedMaterial;
                int second = YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(root);
                Check(first > 0 && YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(repaired), "legacy structural source repaired");
                Check(second == 0 && renderer.sharedMaterial == repaired, "repeated structural repair reuses material");
                Check(source.shader == legacy, "source asset shader preserved");
            }

            // note: Inventory only the approved visible tree family, retaining exact shaders and slots for material diagnosis.
            var paths = (string[])typeof(YQGeneratedWorldEnvironment).GetField("ApprovedVisibleTreePrefabs",
                BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            foreach (string path in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(prefab != null, "approved tree exists " + path);
                if (prefab == null) continue;
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(instance, scene);
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                        report.Add("TREE_SOURCE " + path + " renderer=" + renderer.name + " material=" +
                            (material == null ? "<missing>" : material.name + " shader=" + material.shader?.name +
                             " usable=" + YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material)));
                YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(instance);
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Check(YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material), "repaired tree slot " + path + " / " + renderer.name);
                        if (material != null && material.name.IndexOf("needle", StringComparison.OrdinalIgnoreCase) >= 0)
                            Check(material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f &&
                                material.IsKeywordEnabled("_ALPHATEST_ON"), "pine needle alpha preserved " + path);
                    }
            }
        }
        catch (Exception ex) { failures++; report.Add("FAIL " + ex); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
        }
        report.Add("RESULT " + (failures == 0 ? "PASS" : "FAIL") + " checks=" + checks + " failures=" + failures);
        Directory.CreateDirectory("outputs/World_Readiness_20261002");
        string output = "outputs/World_Readiness_20261002/Fixtures_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".txt";
        File.WriteAllLines(output, report);
        Debug.Log("[YQWorldReadinessVerification] " + report[report.Count - 1] + " receipt=" + output);
    }

    [MenuItem("YourQuest/Verification/Audit Current Runtime Materials")]
    public static void AuditRuntime()
    {
        if (!EditorApplication.isPlaying) return;
        var report = new List<string> { "# Read-only runtime material snapshot", "utc=" + DateTime.UtcNow.ToString("O"),
            "runtimeMvid=" + typeof(YQContinuousWorldFeatureAuthority).Assembly.ManifestModule.ModuleVersionId,
            "presentationReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased,
            "loadingVisible=" + YQStartupLoadingScreen.IsVisible };
        int checkedSlots = 0, invalidSlots = 0;
        // note: A one-shot audit inspects active world renderers and all their LOD slots, without assigning or repairing anything.
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            // note: Billboards render their BillboardAsset material; inherited renderer slots are not their material contract.
            if (renderer is BillboardRenderer billboard)
            {
                checkedSlots++;
                if (billboard.billboard == null || !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(billboard.billboard.material))
                {
                    invalidSlots++;
                    report.Add("UNUSABLE billboard=" + renderer.name + " asset=" + AssetDatabase.GetAssetPath(billboard.billboard));
                }
                continue;
            }
            Material[] materials = renderer.sharedMaterials;
            // note: A missing required submesh slot must remain visible even when the serialized array is shorter than the mesh.
            int requiredSlots = YQRuntimeUrpMaterialRepair.ResolveRequiredMaterialSlotCount(renderer);
            for (int slot = 0; slot < requiredSlots; slot++)
            {
                Material material = slot < materials.Length ? materials[slot] : null;
                if (!YQRuntimeUrpMaterialRepair.IsMaterialSlotRequired(renderer, slot)) continue;
                checkedSlots++;
                if (YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material)) continue;
                invalidSlots++;
                string path = renderer.name;
                for (Transform ancestor = renderer.transform.parent; ancestor != null; ancestor = ancestor.parent)
                    path = ancestor.name + "/" + path;
                report.Add("UNUSABLE path=" + path + " slot=" + slot + " material=" +
                    (material == null ? "<missing>" : material.name + " shader=" + material.shader?.name +
                     " source=" + AssetDatabase.GetAssetPath(material)));
            }
        }
        foreach (Terrain terrain in Terrain.activeTerrains)
            report.Add("TERRAIN name=" + terrain.name + " position=" + terrain.transform.position + " trees=" +
                terrain.terrainData.treeInstanceCount + " detailPrototypes=" + terrain.terrainData.detailPrototypes.Length +
                " material=" + terrain.materialTemplate?.name + " shader=" + terrain.materialTemplate?.shader?.name);
        report.Add("checkedSlots=" + checkedSlots + " unusableSlots=" + invalidSlots);
        Directory.CreateDirectory("outputs/World_Readiness_20261002");
        string output = "outputs/World_Readiness_20261002/Runtime_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".txt";
        File.WriteAllLines(output, report);
        Debug.Log("[YQWorldReadinessVerification] Runtime snapshot receipt=" + output);
    }

    private static YQPreparedSpatialMaterializationV2 CreateRiver(float width, float depth)
    {
        // note: Reflection constructs the immutable projection only in this detached fixture; production still consumes validated accepted artifacts.
        object water = new YQSpatialMaterializationWaterV2 { hydrologyId = "fixture-river", kind = YQHydrologyKindV2.River,
            nominalWidth = width, nominalDepth = depth, waterLevelNormalized = 0.4f };
        typeof(YQSpatialMaterializationWaterV2).GetField("pointStart", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(water, 0);
        typeof(YQSpatialMaterializationWaterV2).GetField("pointCount", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(water, 2);
        return (YQPreparedSpatialMaterializationV2)Activator.CreateInstance(typeof(YQPreparedSpatialMaterializationV2),
            BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] {
                Array.Empty<YQSpatialMaterializationRegionV2>(), Array.Empty<YQSpatialMaterializationSiteV2>(),
                new[] { (YQSpatialMaterializationWaterV2)water },
                new[] { new YQSpatialMaterializationWaterPointV2 { x = 0f, z = 450f, width = width, waterSurfaceNormalized = 0.4f },
                        new YQSpatialMaterializationWaterPointV2 { x = 0f, z = 512f, width = width, waterSurfaceNormalized = 0.4f } },
                Array.Empty<YQSpatialMaterializationRouteV2>(), Array.Empty<YQSpatialMaterializationRoutePointV2>(),
                Array.Empty<YQSpatialMaterializationCrossingV2>(), new Dictionary<string, int>(),
                new Dictionary<string, int>(), new Dictionary<string, int>(), null }, null);
    }
}
