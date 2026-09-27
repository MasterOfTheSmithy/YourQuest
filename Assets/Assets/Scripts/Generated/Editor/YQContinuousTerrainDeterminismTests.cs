using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

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

    private static readonly Vector2[] SamplePoints =
    {
        new Vector2(-2048f, -1536f),
        new Vector2(-1024f, 1024f),
        new Vector2(-768f, 768f),
        new Vector2(768f, -768f),
        new Vector2(1024f, 640f),
        new Vector2(1536f, 2048f)
    };

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
            "- seeds: " + Seeds.Length,
            "- samplePointsPerSeed: " + SamplePoints.Length
        };
        int failures = 0;
        HashSet<string> seedHashes = new HashSet<string>(StringComparer.Ordinal);
        for (int seedIndex = 0; seedIndex < Seeds.Length; seedIndex++)
        {
            string seed = Seeds[seedIndex];
            YQContinuousWorldCellAuthority first =
                new YQContinuousWorldCellAuthority(seed, null, null, null, 128f);
            YQContinuousWorldCellAuthority second =
                new YQContinuousWorldCellAuthority(seed, null, null, null, 128f);
            string firstHash = BuildSampleHash(first, SamplePoints);
            string reorderedHash = BuildSampleHash(first, Reverse(SamplePoints));
            string repeatedHash = BuildSampleHash(second, SamplePoints);
            bool hashPass = firstHash == reorderedHash && firstHash == repeatedHash;
            if (!hashPass)
                failures++;
            seedHashes.Add(firstHash);
            lines.Add("- seed[" + seedIndex + "]: " + seed + " hash=" + firstHash + " deterministic=" + (hashPass ? "PASS" : "FAIL"));
        }

        YQContinuousWorldCellAuthority borderAuthority =
            new YQContinuousWorldCellAuthority(Seeds[0], null, null, null, 128f);
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
        lines.Add("- negativeDiagonalFiniteOriginBorders: " + (borderPass ? "PASS" : "FAIL"));
        lines.Add("- maximumSharedBorderSampleDelta: " + maximumBorderDelta.ToString("R"));
        lines.Add("- distinctSeedHashes: " + seedHashes.Count);
        lines.Add("- failures: " + failures);
        lines.Add("- status: " + (failures == 0 ? "PASS" : "FAIL"));
        File.WriteAllLines(Path.GetFullPath("Logs/YQContinuousTerrainDeterminismTests.md"), lines);
        Debug.Log("[YQContinuousTerrainTests] Tested " + Seeds.Length + " seeds; failures=" + failures + ".");
    }

    private static string BuildSampleHash(YQContinuousWorldCellAuthority authority, Vector2[] points)
    {
        // note: Canonicalize coordinates before hashing so reordered queries test authority determinism rather than concatenation order.
        List<Vector2> orderedPoints = new List<Vector2>(points);
        orderedPoints.Sort((left, right) =>
        {
            int xComparison = left.x.CompareTo(right.x);
            return xComparison != 0 ? xComparison : left.y.CompareTo(right.y);
        });
        StringBuilder values = new StringBuilder();
        for (int index = 0; index < orderedPoints.Count; index++)
        {
            float height = authority.SampleHeightNormalizedOffMainThread(orderedPoints[index].x, orderedPoints[index].y);
            if (float.IsNaN(height) || float.IsInfinity(height))
                return "NON_FINITE";
            values.Append(BitConverter.SingleToInt32Bits(height)).Append('|');
        }
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
}
