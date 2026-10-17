#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// note: Detached synthetic terrain checks never load or modify a saved world or authored scene.
[InitializeOnLoad]
public static class YQRoadFootprintVerification
{
    private const string Request = "Assets/Assets/EditorBuildRequests/YQ_ROAD_FOOTPRINT_VERIFY.request";
    static YQRoadFootprintVerification() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        bool fresh = File.GetLastWriteTimeUtc(Request) > DateTime.UtcNow.AddMinutes(-2);
        File.Delete(Request);
        if (fresh) Run();
    }
    public static void Run()
    {
        TerrainData data = null;
        GameObject terrainObject = null, parent = null;
        Material material = null;
        var checks = new List<object>();
        string failure = null;
        try
        {
            data = new TerrainData { heightmapResolution = 33, size = new Vector3(128, 20, 128) };
            terrainObject = Terrain.CreateTerrainGameObject(data);
            parent = new GameObject("Detached road footprint verification");
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var terrain = terrainObject.GetComponent<Terrain>();
            var method = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("BuildGroundedRoadRibbon", BindingFlags.NonPublic | BindingFlags.Static);
            // note: Both grazing edges require positive-area grounded geometry; an outside route must stay absent.
            foreach (float x in new[] { -1.999f, 129.999f, 64f, -2.1f })
            {
                var points = new List<Vector3> { new Vector3(x, 0, 10), new Vector3(x, 0, 20) };
                var root = (GameObject)method.Invoke(null, new object[] { parent.transform, "Road", points, 4f, material, terrain, null, null, false });
                bool expected = x != -2.1f;
                if ((root != null) != expected) throw new InvalidOperationException("Footprint presence mismatch at x=" + x);
                float area = 0f;
                if (root != null)
                {
                    var mesh = root.GetComponent<MeshFilter>().sharedMesh;
                    var vertices = mesh.vertices; var triangles = mesh.triangles;
                    foreach (var v in vertices)
                        if (v.x < 0 || v.x > 128 || v.z < 0 || v.z > 128 || float.IsNaN(v.y))
                            throw new InvalidOperationException("Road left its terrain owner.");
                    for (int i = 0; i < triangles.Length; i += 3)
                        area += Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).magnitude * .5f;
                    if (area <= 0f) throw new InvalidOperationException("Road has no positive surface area.");
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                checks.Add(new { x, expected, materialized = root != null, area });
            }
        }
        catch (Exception error) { failure = error.ToString(); }
        finally
        {
            if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
            if (terrainObject != null) UnityEngine.Object.DestroyImmediate(terrainObject);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
        string output = "outputs/LlmRequalification_20261008/RoadReadiness/FootprintChecks.json";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow, status = failure == null ? "PASS" : "FAIL", checks, failure,
            runtimeMvid = typeof(YQContinuousWorldFeatureMaterializer).Assembly.ManifestModule.ModuleVersionId,
            evidence = "Synthetic Editor mesh check, not gameplay proof" }, Formatting.Indented));
    }
}
#endif
