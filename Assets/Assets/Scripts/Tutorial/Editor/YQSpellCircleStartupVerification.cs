using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// note: Diagnose the saved-road prerequisite on detached data and disposable meshes; never select, normalize or publish an active profile.
public static class YQSpellCircleStartupVerification
{
    private const string ProfileId = "85e6507c43004c39af986702206a1d49";
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly List<string> Checks = new List<string>();

    [MenuItem("YourQuest/Spells/Verify Saved Road Prerequisite %#F6")]
    public static void Verify()
    {
        Checks.Clear();
        GameObject root = null;
        Material material = null;
        string status = "FAIL", failure = null, savedHash = null, seed = null;
        float overlapMeters = 0f;
        string path = Path.Combine(Application.persistentDataPath, "Profiles", ProfileId, "world_state.json");
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Compiled Edit Mode session");
            savedHash = Hash(path);
            WorldState detached = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(path));
            GeneratedWorldPlanRecord plan = detached?.generatedWorldPlan;
            seed = plan?.worldSeed;
            Require(seed == "622eff02", "Existing spell-bearing save has the recorded fixed seed");
            string acceptedHash = YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(plan.spatialPlanV2);
            Require(YQSpatialMaterializationCompilerV2.TryPrepare(plan, out var prepared, out string preparationFailure),
                "Accepted saved blueprint prepares without bypassing validation: " + preparationFailure);

            // note: Invoke the private production extractor in an Editor-only witness, preserving its public runtime contract.
            MethodInfo extract = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("TryGetAcceptedRoadPoints", PrivateStatic);
            object[] cell = Extract(extract, prepared, new Vector2Int(0, 8));
            var points = (List<Vector3>)cell[4];
            Require(points.Count >= 2, "The failed cell retains distinct road endpoints");
            overlapMeters = Vector2.Distance(new Vector2(points[0].x, points[0].z), new Vector2(points[points.Count - 1].x, points[points.Count - 1].z));
            Require(overlapMeters > 0.17f && overlapMeters < 0.25f, "The accepted short overlap remains about 0.209 metres");
            Require(Mathf.Approximately((float)cell[5], 7.5f), "The accepted road width remains 7.5 metres");

            // note: Extract neighbours in reverse order, then repeat the failed cell to check seam endpoints and traversal-order independence.
            var west = (List<Vector3>)Extract(extract, prepared, new Vector2Int(-1, 8))[4];
            var south = (List<Vector3>)Extract(extract, prepared, new Vector2Int(0, 7))[4];
            Require(Contains(west, points[0]), "The western neighbour shares the clipped start endpoint");
            Require(Contains(south, points[points.Count - 1]), "The southern neighbour shares the clipped end endpoint");
            var repeated = (List<Vector3>)Extract(extract, prepared, new Vector2Int(0, 8))[4];
            Require(repeated.Count == points.Count, "Cell traversal order preserves the sample count");
            for (int index = 0; index < points.Count; index++)
                if (repeated[index] != points[index]) throw new InvalidOperationException("Traversal order changed an accepted road sample.");
            Require(true, "Cell traversal order preserves every sample");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "Existing project shader is available for disposable geometry checks");
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            root = new GameObject("YQSpellSavedRoadVerification") { hideFlags = HideFlags.HideAndDontSave };
            MethodInfo build = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("BuildRibbonSpans", PrivateStatic);
            foreach (bool water in new[] { false, true })
            {
                string name = water ? "Water" : "Road";
                int built = (int)build.Invoke(null, new object[] { root.transform, name, points, cell[6], cell[5], material, water, 128f, cell[7] });
                Require(built == 1, name + " short overlap emits one physical mesh");
                Mesh mesh = root.transform.Find(name + "_Span0").GetComponent<MeshFilter>().sharedMesh;
                Require(mesh.vertexCount >= 4 && mesh.triangles.Length >= 6, name + " short overlap has physical triangles");
                foreach (Vector3 vertex in mesh.vertices)
                    if (float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z) ||
                        float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z))
                        throw new InvalidOperationException(name + " produced a non-finite vertex.");
                Require(true, name + " vertices remain finite");
            }

            // note: Exact duplicate source points remain a single vertex, so the repair cannot manufacture a road from zero-length data.
            var duplicatePoints = new List<Vector3>();
            var duplicateBreaks = new List<int>();
            MethodInfo append = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("AppendAcceptedSegmentPoints", PrivateStatic);
            append.Invoke(null, new object[] { duplicatePoints, 0f, 0f, 0f, 0f, 0f, 0f, -64f, 64f, -64f, 64f, 6f,
                -64f, 64f, -64f, 64f, 0f, 100f, 0.035f, duplicateBreaks, false, Vector2.zero, true });
            Require(duplicatePoints.Count == 1, "Zero-length source geometry stays degenerate");
            Require((int)build.Invoke(null, new object[] { root.transform, "Degenerate", duplicatePoints, duplicateBreaks, 7.5f, material, false, 128f, null }) == 0,
                "Zero-length geometry still fails physical publication");
            Require(YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(plan.spatialPlanV2) == acceptedHash,
                "The detached accepted blueprint hash remains unchanged");
            Require(Hash(path) == savedHash, "The saved world bytes remain unchanged");
            status = "PASS";
        }
        catch (Exception error) { failure = error.ToString(); }
        finally
        {
            // note: Destroy registered meshes before their Editor root so runtime ownership cleanup has no remaining resources to schedule.
            if (root != null)
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                    if (filter.sharedMesh != null) UnityEngine.Object.DestroyImmediate(filter.sharedMesh);
                UnityEngine.Object.DestroyImmediate(root);
            }
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
        string receipt = Path.GetFullPath("Docs/Spell_Circles_Startup_Prerequisite_Receipt_2026-10-02.json");
        File.WriteAllText(receipt, JsonConvert.SerializeObject(new
        {
            status, utc = DateTime.UtcNow.ToString("O"), mode = "Detached saved-world geometry in Unity Edit Mode; not a startup or casting PASS",
            profileId = ProfileId, seed, savedHash, overlapMeters, checks = Checks, failure,
            assemblyHash = Hash(typeof(WorldState).Assembly.Location),
            sourceHash = Hash("Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs")
        }, Formatting.Indented));
        if (status == "PASS") Debug.Log("[YourQuest Spell Circles] Saved-road prerequisite checks PASS: " + receipt);
        else Debug.LogError("[YourQuest Spell Circles] Saved-road prerequisite checks FAIL: " + failure);
    }

    private static object[] Extract(MethodInfo method, YQPreparedSpatialMaterializationV2 prepared, Vector2Int coordinate)
    {
        object[] arguments = { prepared, coordinate, 128f, null, null, 0f, null, null, null };
        if (!(bool)method.Invoke(null, arguments)) throw new InvalidOperationException("Accepted road extraction returned no span in " + coordinate);
        return arguments;
    }

    private static bool Contains(List<Vector3> points, Vector3 expected)
    {
        foreach (Vector3 point in points)
            if ((point - expected).sqrMagnitude < 0.000001f) return true;
        return false;
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        Checks.Add(label);
    }

    private static string Hash(string path)
    {
        using (SHA256 hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty).ToLowerInvariant();
    }
}
