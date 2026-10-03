using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Disposable preview fixtures exercise the actual wilderness placement entry point without loading or saving a world.
public static class YQTreeGroundingVerification
{
    [MenuItem("YourQuest/Verification/Verify Tree Root Grounding")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Tree grounding fixtures require Edit Mode.");
        var report = new List<string> {
            "# Tree root grounding — detached Edit fixtures, not gameplay certification",
            "utc=" + DateTime.UtcNow.ToString("O"),
            "runtimeMvid=" + typeof(YQGeneratedWorldEnvironment).Assembly.ManifestModule.ModuleVersionId
        };
        var method = typeof(YQGeneratedWorldEnvironment).GetMethod("GroundWildernessInstance",
            BindingFlags.Static | BindingFlags.NonPublic);
        var player = PlayerStateManager.Instance;
        var world = WorldStateManager.Instance;
        Scene scene = EditorSceneManager.NewPreviewScene();
        TerrainData data = null;
        int checks = 0, failures = 0;
        void Check(bool passed, string label)
        {
            checks++;
            if (!passed) failures++;
            report.Add((passed ? "PASS " : "FAIL ") + label);
        }
        try
        {
            data = new TerrainData { heightmapResolution = 33, size = new Vector3(64f, 80f, 64f) };
            var terrainRoot = new GameObject("TreeContactFixtureTerrain", typeof(Terrain));
            SceneManager.MoveGameObjectToScene(terrainRoot, scene);
            Terrain terrain = terrainRoot.GetComponent<Terrain>();
            terrain.terrainData = data;
            terrainRoot.transform.position = new Vector3(100f, 17f, -200f);
            // note: Signed slopes, both axes and scaled/yawed trees expose canopy sampling and accidental shrub tilt.
            foreach (float slope in new[] { -0.4f, 0f, 0.4f })
            foreach (bool alongX in new[] { true, false })
            foreach (bool namedTrunk in new[] { true, false })
            foreach (float scale in new[] { 0.7f, 1.8f })
            {
                float[,] heights = new float[33, 33];
                for (int z = 0; z < 33; z++)
                    for (int x = 0; x < 33; x++)
                        heights[z, x] = 0.5f + slope * ((alongX ? x : z) / 32f - 0.5f) * 64f / 80f;
                data.SetHeights(0, 0, heights);
                var tree = new GameObject("Fixture_Pine_Tree");
                SceneManager.MoveGameObjectToScene(tree, scene);
                try
                {
                    var trunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    trunk.name = namedTrunk ? "Trunk" : "Combined_LOD0";
                    trunk.transform.SetParent(tree.transform, false);
                    trunk.transform.localPosition = new Vector3(0f, 7f, 0f);
                    trunk.transform.localScale = new Vector3(0.6f, 10f, 0.6f);
                    var crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    crown.name = "Crown";
                    crown.transform.SetParent(tree.transform, false);
                    crown.transform.localPosition = new Vector3(2f, 11f, 0f);
                    crown.transform.localScale = new Vector3(14f, 8f, 14f);
                    tree.transform.position = terrainRoot.transform.position + new Vector3(32f, 20f, 32f);
                    tree.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
                    tree.transform.localScale = Vector3.one * scale;
                    Quaternion rotation = tree.transform.rotation;
                    string label = "slope=" + slope + " alongX=" + alongX + " namedTrunk=" + namedTrunk + " scale=" + scale;
                    object result = method.Invoke(null, new object[] { tree, terrain, YQWorldAssetCatalog.SlotVegetation, null });
                    float ground = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, tree.transform.position);
                    float error = trunk.GetComponent<Renderer>().bounds.min.y - (ground - 0.05f);
                    Check(!(result is bool accepted) || accepted, "placement accepted " + label);
                    Check(Quaternion.Angle(rotation, tree.transform.rotation) < 0.001f, "upright/yaw preserved " + label);
                    Check(Mathf.Abs(error) < 0.005f, "root contact error=" + error.ToString("F5") + " " + label);
                    if (Mathf.Abs(error) >= 0.005f)
                    {
                        // note: Capture the exact geometry and root sample when the production entry point disagrees with its contact contract.
                        YQGeneratedWorldTerrain.TryGetStableContactGeometry(tree, out Bounds visible, out float bottom);
                        YQGeneratedWorldTerrain.TrySampleFootprintHeight(terrain, new Bounds(tree.transform.position, Vector3.zero),
                            out float rootHeight, out _, out _);
                        report.Add("DETAIL root=" + tree.transform.position.ToString("F5") + " ground=" + ground.ToString("F5") +
                            " rootSample=" + rootHeight.ToString("F5") + " visible=" + visible.ToString("F5") + " bottom=" + bottom.ToString("F5"));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(tree); }
            }
            // note: Invalid contacts must return rejection without moving an unsupported tree into view.
            var unsupported = new GameObject("Fixture_Pine_Tree");
            SceneManager.MoveGameObjectToScene(unsupported, scene);
            try
            {
                Check((bool)method.Invoke(null, new object[] { unsupported, terrain, YQWorldAssetCatalog.SlotVegetation, null }) == false,
                    "missing visible geometry rejected");
                var geometry = GameObject.CreatePrimitive(PrimitiveType.Cube);
                geometry.name = "Combined_LOD0";
                geometry.transform.SetParent(unsupported.transform, false);
                unsupported.transform.position = terrainRoot.transform.position + new Vector3(-10f, 20f, 32f);
                Vector3 before = unsupported.transform.position;
                Check((bool)method.Invoke(null, new object[] { unsupported, terrain, YQWorldAssetCatalog.SlotVegetation, null }) == false,
                    "root outside owner rejected");
                Check(unsupported.transform.position == before, "rejected root position unchanged");
                Check((bool)method.Invoke(null, new object[] { unsupported, null, YQWorldAssetCatalog.SlotVegetation, null }) == false,
                    "missing terrain rejected");
            }
            finally { UnityEngine.Object.DestroyImmediate(unsupported); }
            Check(ReferenceEquals(player, PlayerStateManager.Instance) && ReferenceEquals(world, WorldStateManager.Instance),
                "active owners unchanged");
        }
        catch (Exception exception)
        {
            failures++;
            report.Add("FAIL exception=" + exception);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
        report.Add("RESULT " + (failures == 0 ? "PASS" : "FAIL") + " checks=" + checks + " failures=" + failures);
        string output = "Logs/YQ_TreeGrounding_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".txt";
        File.WriteAllLines(output, report);
        Debug.Log("[YQTreeGroundingVerification] " + report[report.Count - 1] + " receipt=" + output);
    }
}
