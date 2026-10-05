using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: This focused Edit Mode receipt compares the existing absolute-coordinate terrain authority across seeds and query orders.
public static class YQContinuousTerrainDeterminismTests
{
    private const string PendingRequestPath =
        "Assets/Assets/EditorBuildRequests/RunContinuousTerrainDeterminismTests.request";

    private static readonly string[] Seeds =
    {
        "yq_continuous_seed_0001",
        "yq_continuous_seed_0002",
        "yq_continuous_seed_0003",
        "yq_continuous_seed_0004"
    };

    private static readonly Vector2[] SamplePoints = BuildSamplePoints();

    private static Vector2[] BuildSamplePoints()
    {
        // note: Cover signed axes, diagonals, the origin envelope and far frontier without selecting only favorable points.
        float[] coordinates = { -65536f, -32768f, -8192f, -2048f, -1024f, -768f, -512f, -128f,
            0f, 128f, 512f, 768f, 1024f, 2048f, 8192f, 32768f, 65536f };
        List<Vector2> points = new List<Vector2>(coordinates.Length * coordinates.Length + 6);
        for (int x = 0; x < coordinates.Length; x++)
            for (int z = 0; z < coordinates.Length; z++) points.Add(new Vector2(coordinates[x], coordinates[z]));
        points.AddRange(new[] { new Vector2(-2048f, -1536f), new Vector2(-1024f, 1024f),
            new Vector2(-768f, 768f), new Vector2(768f, -768f), new Vector2(1024f, 640f), new Vector2(1536f, 2048f) });
        return points.ToArray();
    }

    private static bool polling;
    private static double nextPoll;

    [InitializeOnLoadMethod]
    private static void QueueRequestedRun()
    {
        // note: Poll the disposable request from the Unity editor main thread so authority construction and receipt writing stay deterministic.
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

        // note: Consume the marker only at an Edit Mode boundary so the report cannot overlap a live TerrainData mutation.
        AssetDatabase.DeleteAsset(PendingRequestPath);
        polling = false;
        EditorApplication.update -= RunRequestedTests;
        RunFromMenu();
    }

    [MenuItem("YourQuest/World Generation/Run Continuous Terrain Determinism Tests")]
    public static void RunFromMenu()
    {
        List<string> lines = new List<string>
        {
            "# YourQuest Continuous Terrain Determinism Tests",
            "- generatedUtc: " + DateTime.UtcNow.ToString("O"),
            "- evidenceLevel: EDIT_MODE_SYNTHETIC_TERRAIN_SAMPLER",
            "- scope: disposable sloped origin fixture; no accepted artifact, production materialization, paint, normals, collision or physical seam certification",
            "- runtimeMvid: " + typeof(YQContinuousWorldCellAuthority).Assembly.ManifestModule.ModuleVersionId,
            "- editorMvid: " + typeof(YQContinuousTerrainDeterminismTests).Assembly.ManifestModule.ModuleVersionId,
            "- unityVersion: " + Application.unityVersion,
            "- runtimeAssemblySha256: " + HashFile(typeof(YQContinuousWorldCellAuthority).Assembly.Location),
            "- editorAssemblySha256: " + HashFile(typeof(YQContinuousTerrainDeterminismTests).Assembly.Location),
            "- authoritySourceSha256: " + HashFile("Assets/Assets/Scripts/Generated/YQContinuousWorldCellAuthority.cs"),
            "- verifierSourceSha256: " + HashFile("Assets/Assets/Scripts/Generated/Editor/YQContinuousTerrainDeterminismTests.cs"),
            "- seeds: " + Seeds.Length,
            "- samplePointsPerSeed: " + SamplePoints.Length
        };
        int failures = 0;
        TerrainData fixtureData = null;
        Scene fixtureScene = default;
        try
        {
            // note: An isolated preview scene supplies required origin samples without editing the production scene or accepted world.
            fixtureScene = EditorSceneManager.NewPreviewScene();
            fixtureData = new TerrainData { heightmapResolution = 33,
                size = new Vector3(1024f, YQGeneratedWorldTerrain.TerrainHeight, 1024f), hideFlags = HideFlags.HideAndDontSave };
            float[,] fixtureHeights = new float[33, 33];
            for (int z = 0; z < 33; z++)
                for (int x = 0; x < 33; x++) fixtureHeights[z, x] = 0.3f + (x + z) * 0.001f;
            fixtureData.SetHeights(0, 0, fixtureHeights);
            GameObject fixtureObject = new GameObject("Terrain sampler fixture", typeof(Terrain)) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(fixtureObject, fixtureScene);
            fixtureObject.transform.position = new Vector3(-512f, 0f, -512f);
            Terrain fixtureTerrain = fixtureObject.GetComponent<Terrain>();
            fixtureTerrain.terrainData = fixtureData;
            fixtureTerrain.enabled = false;
            HashSet<string> seedHashes = new HashSet<string>(StringComparer.Ordinal);
            for (int seedIndex = 0; seedIndex < Seeds.Length; seedIndex++)
            {
                string seed = Seeds[seedIndex];
                YQContinuousWorldCellAuthority first =
                    new YQContinuousWorldCellAuthority(seed, fixtureTerrain, null, null, 128f);
                YQContinuousWorldCellAuthority second =
                    new YQContinuousWorldCellAuthority(seed, fixtureTerrain, null, null, 128f);
                YQContinuousWorldCellAuthority coldReverse =
                    new YQContinuousWorldCellAuthority(seed, fixtureTerrain, null, null, 128f);
                YQContinuousWorldCellAuthority coldShuffled =
                    new YQContinuousWorldCellAuthority(seed, fixtureTerrain, null, null, 128f);
                YQContinuousWorldCellAuthority parallel =
                    new YQContinuousWorldCellAuthority(seed, fixtureTerrain, null, null, 128f);
                string firstHash = BuildSampleHash(first, SamplePoints);
                string reorderedHash = BuildSampleHash(first, Reverse(SamplePoints));
                string repeatedHash = BuildSampleHash(second, SamplePoints);
                string coldReverseHash = BuildSampleHash(coldReverse, Reverse(SamplePoints));
                string coldShuffledHash = BuildSampleHash(coldShuffled, Shuffle(SamplePoints, seedIndex));
                string parallelHash = BuildSampleHash(parallel, Shuffle(SamplePoints, seedIndex + Seeds.Length), true);
                bool hashPass = firstHash != "NON_FINITE" && firstHash == reorderedHash && firstHash == repeatedHash &&
                    firstHash == coldReverseHash && firstHash == coldShuffledHash && firstHash == parallelHash;
                if (!hashPass)
                    failures++;
                seedHashes.Add(firstHash);
                lines.Add("- seed[" + seedIndex + "]: " + seed + " hash=" + firstHash + " deterministic=" + (hashPass ? "PASS" : "FAIL"));
            }

            YQContinuousWorldCellAuthority borderAuthority =
                new YQContinuousWorldCellAuthority(Seeds[0], fixtureTerrain, null, null, 128f);
            float maximumBorderDelta = 0f;
            int[] boundaryCells = { -10, -9, -8, 7, 8, 9 };
            float[] borderSpans = { -1024f, 1024f };
            for (int boundaryIndex = 0; boundaryIndex < boundaryCells.Length; boundaryIndex++)
            {
                int edgeIndex = boundaryCells[boundaryIndex];
                float leftCellEdge = edgeIndex * 128f + 128f;
                float rightCellEdge = (edgeIndex + 1) * 128f;
                for (int sampleIndex = 0; sampleIndex < borderSpans.Length; sampleIndex++)
                {
                    float span = borderSpans[sampleIndex];
                    // note: Compare both signed cell-coordinate constructions of each shared edge, including negative and diagonal-adjacent cells.
                    maximumBorderDelta = Mathf.Max(maximumBorderDelta,
                        Mathf.Abs(borderAuthority.SampleHeightNormalizedOffMainThread(leftCellEdge, span) -
                            borderAuthority.SampleHeightNormalizedOffMainThread(rightCellEdge, span)));
                    float lowerCellEdge = edgeIndex * 128f + 128f;
                    float upperCellEdge = (edgeIndex + 1) * 128f;
                    maximumBorderDelta = Mathf.Max(maximumBorderDelta,
                        Mathf.Abs(borderAuthority.SampleHeightNormalizedOffMainThread(span, lowerCellEdge) -
                            borderAuthority.SampleHeightNormalizedOffMainThread(span, upperCellEdge)));
                }
            }
            bool borderPass = maximumBorderDelta <= 0.000001f;
            if (!borderPass)
                failures++;
            lines.Add("- signedCoordinateConstructionIdentity: " + (borderPass ? "PASS" : "FAIL"));
            lines.Add("- coordinateIdentityLimit: these expressions address the same absolute point; this is not a comparison of independent cell realization paths");
            lines.Add("- maximumSharedBorderSampleDelta: " + maximumBorderDelta.ToString("R"));
            lines.Add("- distinctSeedHashes: " + seedHashes.Count);
            if (seedHashes.Count != Seeds.Length) failures++;
            lines.Add("- queryOrders: forward, warm reverse, fresh forward, cold reverse, cold deterministic shuffle, parallel shuffled");
        }
        catch (Exception exception)
        {
            // note: Preserve failed attempts as receipts even when a fixture or numeric query throws.
            failures++;
            lines.Add("- exception: " + exception);
        }
        finally
        {
            if (fixtureScene.IsValid()) EditorSceneManager.ClosePreviewScene(fixtureScene);
            if (fixtureData != null) UnityEngine.Object.DestroyImmediate(fixtureData);
        }
        lines.Add("- failures: " + failures);
        lines.Add("- status: " + (failures == 0 ? "PASS" : "FAIL"));
        // note: Keep historical evidence intact and write each focused attempt outside generated Unity caches.
        string receiptDirectory = Path.GetFullPath(Path.Combine("outputs/G08_EnvironmentCohesion_20261003",
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_terrain_sampler"));
        Directory.CreateDirectory(receiptDirectory);
        File.WriteAllLines(Path.Combine(receiptDirectory, "Receipt.md"), lines);
        Debug.Log("[YQContinuousTerrainTests] Tested " + Seeds.Length + " seeds; failures=" + failures + ".");
    }

    private static string BuildSampleHash(YQContinuousWorldCellAuthority authority, Vector2[] points, bool parallel = false)
    {
        // note: Sample in the caller's actual order first; sorting before sampling would never exercise a reordered authority query.
        float[] heights = new float[points.Length];
        if (parallel)
        {
            // note: Only the existing immutable off-main-thread numeric API runs on workers; all Unity object work stays in Edit Mode.
            Parallel.For(0, points.Length, index =>
                heights[index] = authority.SampleHeightNormalizedOffMainThread(points[index].x, points[index].y));
        }
        else
            for (int index = 0; index < points.Length; index++)
                heights[index] = authority.SampleHeightNormalizedOffMainThread(points[index].x, points[index].y);
        List<KeyValuePair<Vector2, float>> samples = new List<KeyValuePair<Vector2, float>>(points.Length);
        for (int index = 0; index < points.Length; index++)
        {
            float height = heights[index];
            if (float.IsNaN(height) || float.IsInfinity(height)) return "NON_FINITE";
            samples.Add(new KeyValuePair<Vector2, float>(points[index], height));
        }
        samples.Sort((left, right) =>
        {
            int xComparison = left.Key.x.CompareTo(right.Key.x);
            return xComparison != 0 ? xComparison : left.Key.y.CompareTo(right.Key.y);
        });
        StringBuilder values = new StringBuilder();
        for (int index = 0; index < samples.Count; index++)
            values.Append(BitConverter.SingleToInt32Bits(samples[index].Value)).Append('|');
        using (SHA256 sha = SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(values.ToString())));
    }

    private static Vector2[] Reverse(Vector2[] source)
    {
        Vector2[] reversed = new Vector2[source.Length];
        for (int index = 0; index < source.Length; index++)
            reversed[index] = source[source.Length - 1 - index];
        return reversed;
    }

    private static Vector2[] Shuffle(Vector2[] source, int seed)
    {
        // note: Fixture-local randomness varies only query order; it never consumes a production world random stream.
        Vector2[] shuffled = (Vector2[])source.Clone();
        System.Random random = new System.Random(seed);
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int swap = random.Next(i + 1);
            Vector2 saved = shuffled[i];
            shuffled[i] = shuffled[swap];
            shuffled[swap] = saved;
        }
        return shuffled;
    }

    private static string HashFile(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}
