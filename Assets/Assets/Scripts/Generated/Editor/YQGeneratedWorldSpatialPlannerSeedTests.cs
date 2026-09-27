using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class YQGeneratedWorldSpatialPlannerSeedTests
{
    private const string PendingRequestPath =
        "Assets/Assets/EditorBuildRequests/RunWorldGenSeedTests.request";

    private static readonly string[] Themes =
    {
        "high fantasy kingdom",
        "cyberpunk megacity",
        "zombie post-apocalypse",
        "noir city"
    };

    // note: Keep the deterministic stress matrix broad enough to exercise varied procedural themes and repeated seeds.
    private const int SeedsPerTheme = 32;

    private static bool _requestPolling;
    private static double _nextRequestPollTime;

    [InitializeOnLoadMethod]
    private static void QueueRequestedRun()
    {
        // note: A file request lets automated/headless engineering validate seeds without driving the Unity desktop.
        EditorApplication.delayCall += RunRequestedTests;
        if (!_requestPolling)
        {
            _requestPolling = true;
            EditorApplication.update += RunRequestedTests;
        }
    }

    private static void RunRequestedTests()
    {
        if (EditorApplication.timeSinceStartup < _nextRequestPollTime)
            return;

        _nextRequestPollTime =
            EditorApplication.timeSinceStartup + 1d;

        if (!File.Exists(Path.GetFullPath(PendingRequestPath)) ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        // note: Consume the request only at a stable AssetDatabase boundary, then run the complete deterministic matrix.
        AssetDatabase.DeleteAsset(PendingRequestPath);
        _requestPolling = false;
        EditorApplication.update -= RunRequestedTests;
        RunFromMenu();
    }

    [MenuItem("YourQuest/World Generation/Run Spatial Seed Torture Test")]
    public static void RunFromMenu()
    {
        int failures = RunTests(out int tested);
        Debug.Log(
            "[YQWorldGenSeedTests] Tested " + tested +
            " deterministic theme/seed combinations; failures=" + failures + ".");
    }

    public static void RunBatch()
    {
        int failures = RunTests(out int tested);
        Debug.Log(
            "[YQWorldGenSeedTests] Tested " + tested +
            " deterministic theme/seed combinations; failures=" + failures + ".");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static int RunTests(out int tested)
    {
        int failures = 0;
        tested = 0;
        for (int themeIndex = 0; themeIndex < Themes.Length; themeIndex++)
        {
            for (int seedIndex = 0; seedIndex < SeedsPerTheme; seedIndex++)
            {
                string seed = "yq_torture_" + themeIndex + "_" + seedIndex.ToString("D3");
                GeneratedWorldPlanRecord firstPlan = BuildPlan(seed, Themes[themeIndex]);
                GeneratedWorldPlanRecord secondPlan = BuildPlan(seed, Themes[themeIndex]);
                GeneratedSpatialWorldPlanRecord first = YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(firstPlan);
                GeneratedSpatialWorldPlanRecord second = YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(secondPlan);
                tested++;

                if (!Validate(firstPlan, first, second, out string failure))
                {
                    failures++;
                    Debug.LogError("[YQWorldGenSeedTests] " + seed + " / " + Themes[themeIndex] + ": " + failure);
                }
            }
        }
        return failures;
    }

    private static bool Validate(
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialWorldPlanRecord first,
        GeneratedSpatialWorldPlanRecord second,
        out string failure)
    {
        failure = string.Empty;
        int expectedLocations = 1 + plan.settlements.Count + plan.encampments.Count + plan.pointsOfInterest.Count;
        if (first.locations.Count != expectedLocations)
        {
            failure = "location count " + first.locations.Count + " != " + expectedLocations;
            return false;
        }
        if (first.routes.Count < plan.settlements.Count + plan.encampments.Count)
        {
            failure = "travel graph omitted required settlement/hostile connections";
            return false;
        }
        if (first.metrics.unreachableLocationCount != 0)
        {
            failure = first.metrics.unreachableLocationCount + " quest locations are unreachable";
            return false;
        }
        if (first.metrics.estimatedTraversableFraction < 0.52f)
        {
            failure = "estimated traversable area is only " + first.metrics.estimatedTraversableFraction;
            return false;
        }
        if (first.locations.Count != second.locations.Count || first.routes.Count != second.routes.Count)
        {
            failure = "same seed produced a different plan shape";
            return false;
        }
        for (int i = 0; i < first.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord a = first.locations[i];
            GeneratedSpatialLocationRecord b = second.locations[i];
            if (a.locationId != b.locationId ||
                !Mathf.Approximately(a.worldX, b.worldX) ||
                !Mathf.Approximately(a.worldZ, b.worldZ))
            {
                failure = "same seed changed location " + i;
                return false;
            }
            if (Mathf.Abs(a.worldX) + a.footprintRadius > 512f ||
                Mathf.Abs(a.worldZ) + a.footprintRadius > 512f)
            {
                failure = "location footprint escaped world bounds: " + a.locationId;
                return false;
            }
        }
        return true;
    }

    private static GeneratedWorldPlanRecord BuildPlan(string seed, string theme)
    {
        GeneratedWorldPlanRecord plan = new GeneratedWorldPlanRecord
        {
            worldSeed = seed,
            summary = theme + " world shaped by trade, danger, history, and geography",
            designNotes = "Test topology for " + theme
        };
        plan.EnsureCollections();
        for (int i = 0; i < 4; i++)
        {
            string regionId = "region_" + i;
            plan.regions.Add(new GeneratedRegionRecord
            {
                regionId = regionId,
                displayName = "Test Region " + i,
                dangerTier = i + 1,
                terrainProfile = i == 2 ? "mountain ridge and mineral valley" : "rolling river forest",
                climateProfile = i == 1 ? "wet temperate" : "seasonal",
                gameplayPremise = theme,
                assetStyleKey = theme.Replace(' ', '_')
            });
        }
        for (int i = 0; i < 5; i++)
        {
            plan.settlements.Add(new GeneratedSettlementRecord
            {
                settlementId = "settlement_" + i,
                regionId = "region_" + (i % 4),
                displayName = "Settlement " + i,
                kind = i == 0 ? "trade town" : i == 1 ? "fortress" : i == 2 ? "mining camp" : "village",
                approxPopulation = 18 + i * 17,
                deterministicSeed = seed + "|settlement|" + i
            });
        }
        for (int i = 0; i < 4; i++)
        {
            plan.encampments.Add(new GeneratedEncampmentRecord
            {
                encampmentId = "hostile_" + i,
                regionId = "region_" + i,
                displayName = "Hostile Site " + i,
                kind = i == 2 ? "cave" : "bandit checkpoint",
                threatTier = i + 1,
                deterministicSeed = seed + "|hostile|" + i
            });
        }
        for (int i = 0; i < 3; i++)
        {
            plan.pointsOfInterest.Add(new GeneratedPointOfInterestRecord
            {
                poiId = "poi_" + i,
                regionId = "region_" + i,
                displayName = "POI " + i,
                kind = i == 0 ? "ancient ruin" : i == 1 ? "mine" : "lakeside shrine",
                gameplayHook = i == 1 ? "resource dungeon" : "exploration"
            });
        }
        return plan;
    }
}
