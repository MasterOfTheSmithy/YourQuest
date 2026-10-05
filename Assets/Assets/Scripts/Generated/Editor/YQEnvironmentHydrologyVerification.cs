using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// note: These numeric cases reproduce the observed internal-bend extrapolation without loading assets or accepted saves.
public static class YQEnvironmentHydrologyVerification
{
    private static readonly MethodInfo Append = typeof(YQContinuousWorldFeatureMaterializer).GetMethod(
        "AppendAcceptedSegmentPoints", BindingFlags.Static | BindingFlags.NonPublic);

    [MenuItem("YourQuest/Verification/Run Hydrology Segment Contracts")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Hydrology segment contracts require stable Edit Mode.");
        var checks = new List<object>();
        int failures = 0;
        Action<string, bool> check = (name, passed) =>
        {
            checks.Add(new { name, verdict = passed ? "PASS" : "FAIL" });
            if (!passed) failures++;
        };
        try
        {
            // note: Coordinates and elevations follow the retained primary world's five-point continuation, but this is a synthetic numeric test.
            List<Vector3> northLegacy = Segment(182.125809f, 250f, 155.969833f, 0f, .46f, .36f, 128f, 256f, 256f, 384f, true);
            List<Vector3> north = Segment(182.125809f, 250f, 155.969833f, 0f, .46f, .36f, 128f, 256f, 256f, 384f, false);
            check("legacy permissive clipping reproduces the northern internal-bend spur", northLegacy.Exists(point => point.z >= 383.999f));
            check("bounded northern clipping retains overlap without extending past its accepted endpoint",
                north.Count >= 2 && north.TrueForAll(point => point.z <= 250.001f && point.z >= -.001f));
            List<Vector3> southLegacy = Segment(155.969833f, 0f, 116.006409f, -250f, .36f, .26f, 0f, 128f, -384f, -256f, true);
            List<Vector3> south = Segment(155.969833f, 0f, 116.006409f, -250f, .36f, .26f, 0f, 128f, -384f, -256f, false);
            check("legacy permissive clipping reproduces the southern internal-bend spur", southLegacy.Exists(point => point.z <= -383.999f));
            check("bounded southern clipping retains overlap without extending past its accepted endpoint",
                south.Count >= 2 && south.TrueForAll(point => point.z >= -250.001f && point.z <= .001f));
            List<Vector3> crossing = Segment(133.978973f, 508f, 182.125809f, 250f, .56f, .46f, 128f, 256f, 256f, 384f, false);
            check("actual intersecting segment retains both exact cell borders",
                crossing.Exists(point => Mathf.Abs(point.z - 256f) < .001f) && crossing.Exists(point => Mathf.Abs(point.z - 384f) < .001f));
            check("intersecting samples remain on the accepted segment and elevation gradient", crossing.Count >= 2 && crossing.TrueForAll(point =>
            {
                float t = (point.z - 508f) / (250f - 508f);
                return t >= -.00001f && t <= 1.00001f &&
                    Mathf.Abs(point.y - Mathf.LerpUnclamped(.56f, .46f, t) * 140f) < .0001f &&
                    Mathf.Abs(point.x - Mathf.LerpUnclamped(133.978973f, 182.125809f, t)) < .0001f;
            }));
        }
        catch (Exception exception)
        {
            failures++;
            checks.Add(new { name = "numeric segment execution", verdict = "FAIL", exception = exception.ToString() });
        }
        string directory = Path.Combine("outputs/G08_EnvironmentCohesion_20261003", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_hydrology_segments");
        Directory.CreateDirectory(directory);
        var receipt = new
        {
            utc = DateTime.UtcNow.ToString("O"), verifierVersion = "accepted-river-segment-bounds-1",
            evidenceLevel = "EDIT_MODE_SYNTHETIC_RIBBON_SEGMENT", verdict = failures == 0 ? "PASS" : "FAIL",
            runtimeVerified = false, unityVersion = Application.unityVersion,
            runtimeMvid = typeof(YQContinuousWorldFeatureMaterializer).Assembly.ManifestModule.ModuleVersionId.ToString(),
            editorMvid = typeof(YQEnvironmentHydrologyVerification).Assembly.ManifestModule.ModuleVersionId.ToString(),
            runtimeAssemblySha256 = Hash(typeof(YQContinuousWorldFeatureMaterializer).Assembly.Location),
            editorAssemblySha256 = Hash(typeof(YQEnvironmentHydrologyVerification).Assembly.Location),
            materializerSourceSha256 = Hash("Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs"),
            verifierSourceSha256 = Hash("Assets/Assets/Scripts/Generated/Editor/YQEnvironmentHydrologyVerification.cs"), checks,
            limits = "Numeric internal-bend clipping only. Caller integration, accepted terminal continuation, wetness, topology, terrain/collision and visual appearance need production evidence."
        };
        File.WriteAllText(Path.Combine(directory, "Receipt.json"), JsonConvert.SerializeObject(receipt, Formatting.Indented));
        Debug.Log("[YQHydrologySegments] " + receipt.verdict + "; " + directory);
    }

    private static List<Vector3> Segment(float ax, float az, float bx, float bz, float ay, float by,
        float minX, float maxX, float minZ, float maxZ, bool extend)
    {
        // note: Reflection invokes one existing internal numeric boundary; it supplies no alternate algorithm or Unity object.
        var points = new List<Vector3>();
        object[] arguments = { points, ax, az, bx, bz, ay, by, minX, maxX, minZ, maxZ, 8f,
            minX, maxX, minZ, maxZ, 0f, 140f, 0f, new List<int>(), false, Vector2.zero, extend };
        if (Append == null || !(bool)Append.Invoke(null, arguments)) throw new InvalidOperationException("Expected a clipped accepted segment.");
        return points;
    }

    private static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }
}
