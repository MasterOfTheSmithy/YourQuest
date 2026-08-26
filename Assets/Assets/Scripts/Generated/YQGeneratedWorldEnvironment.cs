using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class YQGeneratedWorldEnvironment
{
    private const int AlphamapResolution =
        256;

    /*
     * Wilderness is deliberately split into distinct physical layers.
     *
     * SMALL SCATTER
     * - trees
     * - bushes
     * - grass
     * - ferns
     * - ordinary rocks
     *
     * TERRAIN LANDFORMS
     * - hills
     * - mountain ridges
     *
     * POI STRUCTURES
     * - cave entrances
     *
     * ENCOUNTERS
     * - sparse ambient monsters
     * - wilderness treasure
     *
     * Giant marketplace mountain meshes are NOT ordinary scatter.
     * Broad landforms are stamped directly into Unity Terrain so the
     * TerrainCollider remains the authoritative physical surface.
     */

    private const int BaseVegetationPerRegion =
        96;

    private const int TerrainDetailResolution =
        256;

    private const int TerrainDetailPatchResolution =
        16;

    private const int MaximumTerrainTreeInstances =
        1600;

    private const int MaximumTerrainTreePrototypes =
        16;

    private const int MaximumTerrainDetailPrototypes =
        6;

    private static readonly string[] ApprovedUrpConiferPrefabs =
    {
        "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Tall BOTD URP.prefab",
        "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Medium BOTD URP.prefab",
        "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Small BOTD URP.prefab",
        "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Bare BOTD URP.prefab"
    };

    private static readonly string[] ApprovedVisibleTreePrefabs =
    {
        "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Tall BOTD URP.prefab",
        "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Medium BOTD URP.prefab",
        "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Small BOTD URP.prefab",
        "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Bare BOTD URP.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Alder.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Sycamore.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ThinTree.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ScotsPineTypeA.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ScotsPineTypeB.prefab"
    };

    private static readonly string[] ApprovedDryTreePrefabs =
    {
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Mimosa.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ThinTree.prefab"
    };

    private static readonly string[] ApprovedMacroWaterPrefabs =
    {
        "Assets/HIVEMIND/GladitorArena/HDRP(Default)/Art/Prefabs/SM_Water.prefab",
        "Assets/HIVEMIND/CaveOfHiddenTomb/HDRP (Default)/Art/Prefabs/SM_Water_01.prefab",
        "Assets/HIVEMIND/CaveOfHiddenTomb/HDRP (Default)/Art/Prefabs/SM_Water_02.prefab"
    };

    private const int BaseRockScatterPerRegion =
        24;

    private const int BaseLandformsPerRegion =
        5;

    private const int BaseAmbientEncounterGroupsPerRegion =
        1;

    private const int BaseTreasurePerRegion =
        2;

    private const int MaxOversizedWildernessWarningLogs =
        8;

    private static int _oversizedWildernessWarningLogs;

    private static readonly List<Mesh> GeneratedMacroWaterMeshes =
        new List<Mesh>();

    private const float WildernessRadiusMin =
        22f;

    private const float WildernessRadiusMax =
        255f;

    private const float LandformRadiusMin =
        68f;

    private const float LandformRadiusMax =
        220f;

    private const float SettlementClearRadius =
        22f;

    private const float SettlementLandformClearRadius =
        62f;

    private const float OriginClearRadius =
        36f;

    private const float OriginLandformClearRadius =
        82f;

    private const float EncampmentEncounterClearRadius =
        32f;

    private const float EncampmentLandformClearRadius =
        48f;

    private sealed class RegionSurface
    {
        public GeneratedRegionRecord region;

        public GeneratedRegionAssetPaletteRecord palette;

        public Vector3 center;

        public int baseLayerIndex = -1;

        public int detailLayerIndex = -1;

        public int rockLayerIndex = -1;

        public int pathLayerIndex = -1;
    }

    private sealed class LivedPathSegment
    {
        public Vector2 start;

        public Vector2 end;

        public float halfWidth;

        public float shoulderWidth;

        public float curveAmplitude;

        public float curvePhase;

        public bool terraced;
    }

    public readonly struct LivedPathTerrainReservation
    {
        public readonly Vector2 center;

        public readonly float radius;

        public LivedPathTerrainReservation(
            Vector3 worldCenter,
            float protectedRadius)
        {
            // note: The construction prepass passes its real authored shelf radius so road grading cannot reshape peripheral foundations.
            center = new Vector2(worldCenter.x, worldCenter.z);
            radius = Mathf.Max(0f, protectedRadius);
        }
    }

    private sealed class MacroWaterSet
    {
        public readonly YQGeneratedWorldTerrain.MacroWaterBasinDescriptor[] basins =
            new YQGeneratedWorldTerrain.MacroWaterBasinDescriptor[
                YQGeneratedWorldTerrain.MacroWaterBasinCount];

        public int count;
    }

    private sealed class WildernessBuildStats
    {
        public int waterBodies;

        public int visibleTrees;

        public int vegetation;

        public int rocks;

        public int caves;

        public int ambientEnemies;

        public int treasure;
    }

    private sealed class TerrainVegetationProfile
    {
        public GeneratedRegionRecord region;

        public GeneratedRegionAssetPaletteRecord palette;

        public Vector3 center;

        public float treeMaskThreshold;

        public float detailMaskThreshold;

        public readonly List<int> treePrototypeIndices =
            new List<int>();

        public readonly List<int> detailPrototypeIndices =
            new List<int>();
    }

    private sealed class AmbientMonsterSource
    {
        public string family =
            string.Empty;

        public string factionId =
            string.Empty;
    }

    // ============================================================
    // PUBLIC BUILD
    // ============================================================

    public static IEnumerator BuildRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry)
    {
        // note: Compatibility callers still receive the complete environment, while the production builder can insert deterministic site grading between the terrain and dressing phases.
        yield return BuildTerrainFoundationRoutine(
            terrain,
            plan,
            registry);
        yield return BuildWildernessRoutine(
            parent,
            terrain,
            plan,
            registry);
    }

    public static IEnumerator BuildTerrainFoundationRoutine(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        bool deferSurfacePaint = false)
    {
        if (terrain == null ||
            terrain.terrainData == null ||
            plan == null ||
            registry == null)
        {
            yield break;
        }

        plan.EnsureCollections();

        /*
         * Landforms are part of the physical terrain.
         *
         * Do this BEFORE settlements, population and wilderness objects
         * are positioned so every later system samples final terrain.
         */
        int landforms =
            0;

        // note: Regional height stamps are frame-budgeted so terrain authorship cannot freeze the Goddess thought stream.
        yield return BuildRegionalLandformsRoutine(
            terrain,
            plan,
            count => landforms = count);

        // note: Palette shifts and initial construction yield between terrain phases so one frame never owns the complete world build.
        yield return null;

        List<RegionSurface> surfaces = new List<RegionSurface>();

        if (!deferSurfacePaint)
        {
            yield return ApplyRegionalTerrainLayersRoutine(
                terrain,
                plan,
                registry,
                result => surfaces = result);

            surfaces ??= new List<RegionSurface>();
        }

        yield return null;

        Debug.Log(
            "[YQGeneratedWorldEnvironment] " +
            (deferSurfacePaint
                ? "TERRAIN GEOMETRY READY\n"
                : "TERRAIN FOUNDATION READY\n") +
            "Terrain layers: " +
            (terrain.terrainData.terrainLayers != null
                ? terrain.terrainData.terrainLayers.Length
                : 0) +
            "\nRegion surface mappings: " + surfaces.Count +
            "\nRegions: " + plan.regions.Count +
            "\nTerrain hills/mountains stamped: " + landforms);
    }

    public static IEnumerator BuildTerrainSurfaceRoutine(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry)
    {
        if (terrain == null || terrain.terrainData == null ||
            plan == null || registry == null)
        {
            yield break;
        }

        List<RegionSurface> surfaces = null;
        // note: Production paints after every construction pad is finalized, so slope stone, biome detail, and the authored approach describe the terrain the player actually collides with.
        yield return ApplyRegionalTerrainLayersRoutine(
            terrain,
            plan,
            registry,
            result => surfaces = result);

        Debug.Log(
            "[YQGeneratedWorldEnvironment] TERRAIN SURFACE READY\n" +
            "Terrain layers: " +
            (terrain.terrainData.terrainLayers != null
                ? terrain.terrainData.terrainLayers.Length
                : 0) +
            "\nRegion surface mappings: " +
            (surfaces != null ? surfaces.Count : 0));
    }

    public static IEnumerator RepairLivedPathTerrainRoutine(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        IReadOnlyList<LivedPathTerrainReservation> protectedFootprints = null)
    {
        if (terrain == null || terrain.terrainData == null ||
            plan == null)
        {
            yield break;
        }

        List<LivedPathSegment> paths =
            BuildLivedPathNetwork(
                plan,
                terrain);

        if (paths.Count == 0)
            yield break;

        TerrainData data = terrain.terrainData;
        int resolution = data.heightmapResolution;
        float[,] heights = new float[resolution, resolution];

        const int heightRowsPerRead = 16;
        for (int startRow = 0;
             startRow < resolution;
             startRow += heightRowsPerRead)
        {
            int rowCount = Mathf.Min(
                heightRowsPerRead,
                resolution - startRow);
            float[,] strip = data.GetHeights(
                0,
                startRow,
                resolution,
                rowCount);

            for (int row = 0; row < rowCount; row++)
            {
                for (int column = 0; column < resolution; column++)
                {
                    heights[startRow + row, column] = strip[row, column];
                }
            }

            // note: Path repair reads the authoritative delayed heightmap in small strips so its setup cannot hitch the Goddess loading presentation.
            yield return null;
        }

        Vector3 terrainPosition =
            terrain.transform.position;
        Vector3 terrainSize =
            data.size;
        MacroWaterSet macroWater =
            BuildMacroWaterSet(
                terrain,
                plan);
        List<LivedPathTerrainReservation> fallbackReservations = null;

        if (protectedFootprints == null || protectedFootprints.Count == 0)
        {
            fallbackReservations = new List<LivedPathTerrainReservation>
            {
                new LivedPathTerrainReservation(
                    YQGeneratedWorldLayout.GetVeyOriginAnchor(),
                    28f)
            };

            if (plan.settlements != null)
            {
                for (int index = 0; index < plan.settlements.Count; index++)
                {
                    GeneratedSettlementRecord settlement = plan.settlements[index];

                    if (settlement == null)
                        continue;

                    fallbackReservations.Add(
                        new LivedPathTerrainReservation(
                            YQGeneratedWorldLayout.GetSettlementAnchor(
                                plan,
                                settlement,
                                terrain),
                            12f));
                }
            }

            if (plan.encampments != null)
            {
                for (int index = 0; index < plan.encampments.Count; index++)
                {
                    GeneratedEncampmentRecord encampment = plan.encampments[index];

                    if (encampment == null)
                        continue;

                    fallbackReservations.Add(
                        new LivedPathTerrainReservation(
                            YQGeneratedWorldLayout.GetEncampmentAnchor(
                                plan,
                                encampment,
                                terrain),
                            10f));
                }
            }

            protectedFootprints = fallbackReservations;
        }

        int[] dirtyMinimumXByRow = new int[resolution];
        int[] dirtyMaximumXByRow = new int[resolution];

        for (int row = 0; row < resolution; row++)
        {
            dirtyMinimumXByRow[row] = resolution;
            dirtyMaximumXByRow[row] = -1;
        }

        // note: Authoritative construction footprints and dirty height bounds are cached before the cell loop, preventing roads from touching buildings or republishing untouched terrain.
        int modifiedSamples = 0;
        int terracedPaths = 0;
        float frameStartedAt =
            Time.realtimeSinceStartup;

        for (int pathIndex = 0;
             pathIndex < paths.Count;
             pathIndex++)
        {
            LivedPathSegment path = paths[pathIndex];

            if (path == null)
                continue;

            float outerWidth =
                path.halfWidth +
                path.shoulderWidth +
                3f;
            float minimumWorldX =
                Mathf.Min(path.start.x, path.end.x) -
                Mathf.Abs(path.curveAmplitude) -
                outerWidth;
            float maximumWorldX =
                Mathf.Max(path.start.x, path.end.x) +
                Mathf.Abs(path.curveAmplitude) +
                outerWidth;
            float minimumWorldZ =
                Mathf.Min(path.start.y, path.end.y) -
                Mathf.Abs(path.curveAmplitude) -
                outerWidth;
            float maximumWorldZ =
                Mathf.Max(path.start.y, path.end.y) +
                Mathf.Abs(path.curveAmplitude) +
                outerWidth;
            int minimumX = Mathf.Clamp(
                Mathf.FloorToInt(
                    (minimumWorldX - terrainPosition.x) /
                    terrainSize.x *
                    (resolution - 1)),
                0,
                resolution - 1);
            int maximumX = Mathf.Clamp(
                Mathf.CeilToInt(
                    (maximumWorldX - terrainPosition.x) /
                    terrainSize.x *
                    (resolution - 1)),
                0,
                resolution - 1);
            int minimumZ = Mathf.Clamp(
                Mathf.FloorToInt(
                    (minimumWorldZ - terrainPosition.z) /
                    terrainSize.z *
                    (resolution - 1)),
                0,
                resolution - 1);
            int maximumZ = Mathf.Clamp(
                Mathf.CeilToInt(
                    (maximumWorldZ - terrainPosition.z) /
                    terrainSize.z *
                    (resolution - 1)),
                0,
                resolution - 1);

            float pathLength =
                Mathf.Max(
                    1f,
                    Vector2.Distance(path.start, path.end));
            float[] pathHeightProfile = BuildLivedPathHeightProfile(
                path,
                pathLength,
                heights,
                terrainPosition,
                terrainSize);
            bool useTerraces = path.terraced;

            if (useTerraces)
                terracedPaths++;

            for (int z = minimumZ;
                 z <= maximumZ;
                 z++)
            {
                float worldZ =
                    terrainPosition.z +
                    z /
                    (float)(resolution - 1) *
                    terrainSize.z;

                for (int x = minimumX;
                     x <= maximumX;
                     x++)
                {
                    float worldX =
                        terrainPosition.x +
                        x /
                        (float)(resolution - 1) *
                        terrainSize.x;
                    Vector2 point =
                        new Vector2(worldX, worldZ);

                    if (!TryResolveLivedPathSample(
                            path,
                            point,
                            out float pathT,
                            out _,
                            out float pathDistance) ||
                        pathDistance >=
                            path.halfWidth +
                            path.shoulderWidth)
                    {
                        continue;
                    }

                    Vector3 worldPoint =
                        new Vector3(worldX, 0f, worldZ);

                    if (IsInsideLivedPathTerrainReservation(
                            protectedFootprints,
                            point))
                    {
                        // note: Central construction pads remain authoritative through their real outer shelf radius; grading begins only where authored foundations can no longer be disturbed.
                        continue;
                    }

                    float targetWorldHeight =
                        SampleLivedPathHeightProfile(
                            pathHeightProfile,
                            pathT);

                    if (useTerraces &&
                        pathT > 0.06f &&
                        pathT < 0.94f)
                    {
                        const float terrainStepRise = 0.55f;
                        targetWorldHeight =
                            Mathf.Round(
                                targetWorldHeight /
                                terrainStepRise) *
                            terrainStepRise;
                    }

                    for (int basinIndex = 0;
                         basinIndex < macroWater.count;
                         basinIndex++)
                    {
                        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin =
                            macroWater.basins[basinIndex];

                        if (basin.ContainsXZ(worldPoint, -4f))
                        {
                            // note: A regional road crossing a lake becomes a narrow raised ford/causeway instead of an impassable submerged texture stripe.
                            targetWorldHeight =
                                Mathf.Max(
                                    targetWorldHeight,
                                    basin.WaterSurfaceY +
                                        0.35f);
                        }
                    }

                    float centerBlend =
                        pathDistance <= path.halfWidth
                            ? 1f
                            : (1f - Mathf.SmoothStep(
                                path.halfWidth,
                                path.halfWidth + path.shoulderWidth,
                                pathDistance)) *
                              0.82f;
                    float targetNormalized =
                        Mathf.Clamp01(
                            (targetWorldHeight - terrainPosition.y) /
                            Mathf.Max(0.001f, terrainSize.y));
                    float previousHeight = heights[z, x];
                    float repairedHeight =
                        Mathf.Lerp(
                            previousHeight,
                            targetNormalized,
                            centerBlend);

                    if (Mathf.Abs(repairedHeight - previousHeight) <=
                        0.000001f)
                    {
                        continue;
                    }

                    heights[z, x] = repairedHeight;
                    modifiedSamples++;
                    dirtyMinimumXByRow[z] = Mathf.Min(
                        dirtyMinimumXByRow[z],
                        x);
                    dirtyMaximumXByRow[z] = Mathf.Max(
                        dirtyMaximumXByRow[z],
                        x);
                }

                if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
                {
                    // note: Even long regional roads grade a few height rows per rendered frame so improved traversal cannot reintroduce a loading-screen hang.
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
            }
        }

        const int uploadRowsPerFrame = 8;
        int nextDirtyRow = 0;

        while (nextDirtyRow < resolution)
        {
            while (nextDirtyRow < resolution &&
                   dirtyMaximumXByRow[nextDirtyRow] < 0)
            {
                nextDirtyRow++;
            }

            if (nextDirtyRow >= resolution)
                break;

            int startRow = nextDirtyRow;
            int endRow = startRow;
            int minimumDirtyX = dirtyMinimumXByRow[startRow];
            int maximumDirtyX = dirtyMaximumXByRow[startRow];

            while (endRow + 1 < resolution &&
                   endRow - startRow + 1 < uploadRowsPerFrame &&
                   dirtyMaximumXByRow[endRow + 1] >= 0)
            {
                endRow++;
                minimumDirtyX = Mathf.Min(
                    minimumDirtyX,
                    dirtyMinimumXByRow[endRow]);
                maximumDirtyX = Mathf.Max(
                    maximumDirtyX,
                    dirtyMaximumXByRow[endRow]);
            }

            int rowCount = endRow - startRow + 1;
            int width = maximumDirtyX - minimumDirtyX + 1;
            float[,] strip = new float[rowCount, width];

            for (int row = 0; row < rowCount; row++)
            {
                for (int column = 0;
                     column < width;
                     column++)
                {
                    strip[row, column] =
                        heights[
                            startRow + row,
                            minimumDirtyX + column];
                }
            }

            // note: Only dirty road bounds are republished, preventing a narrow path repair from uploading the entire 513-square terrain.
            data.SetHeightsDelayLOD(
                minimumDirtyX,
                startRow,
                strip);
            yield return null;
            nextDirtyRow = endRow + 1;
        }

        Debug.Log(
            "[YQGeneratedWorldEnvironment] LIVED PATH TERRAIN REPAIRED\n" +
            "Paths: " + paths.Count +
            "\nTerraced climbs: " + terracedPaths +
            "\nModified height samples: " + modifiedSamples);
    }

    public static IEnumerator BuildWildernessRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry)
    {
        if (parent == null ||
            terrain == null ||
            terrain.terrainData == null ||
            plan == null ||
            registry == null)
        {
            yield break;
        }

        plan.EnsureCollections();

        WildernessBuildStats stats =
            null;

        int terrainTrees = 0;
        int terrainDetails = 0;

        int waterBodies = 0;

        // note: Water is materialized from the same deterministic basin descriptors that sculpted the heightfield, after construction pads can safely rise through it.
        yield return BuildMacroWaterBodiesRoutine(
            parent,
            terrain,
            plan,
            registry,
            count => waterBodies = count);

        // note: Macro trees and ground-cover fields belong to Terrain so thousands of plants batch natively instead of becoming thousands of loading-time GameObjects.
        yield return BuildTerrainNativeVegetationRoutine(
            terrain,
            plan,
            registry,
            (trees, details) =>
            {
                terrainTrees = trees;
                terrainDetails = details;
            });

        // note: Wilderness families are committed across rendered frames instead of cloning every region's scenery in one loading-frame burst.
        yield return BuildRegionalWildernessRoutine(
            parent,
            terrain,
            plan,
            registry,
            result => stats = result);

        stats ??=
            new WildernessBuildStats();

        stats.waterBodies =
            waterBodies;

        int originVegetation = 0;
        int originTrees = 0;
        int originRocks = 0;

        // note: Regional scatter alone can leave the fixed tutorial threshold inside a mathematically valid but visually empty gap between region centers; a bounded local composition guarantees readable foreground and midground silhouettes.
        yield return BuildOriginApproachDressingRoutine(
            parent,
            terrain,
            plan,
            registry,
            (trees, vegetation, rocks) =>
            {
                originTrees = trees;
                originVegetation = vegetation;
                originRocks = rocks;
            });

        stats.visibleTrees += originTrees;
        stats.vegetation += originVegetation;
        stats.rocks += originRocks;

        // note: Wilderness grounding uses terrain samples and renderer bounds; colliders can join the next normal physics step instead of forcing a full-scene loading synchronization.
        yield return null;

        Debug.Log(
            "[YQGeneratedWorldEnvironment] WILDERNESS READY\n" +
            "Vegetation spawned: " +
            stats.vegetation +
            "\nMacro water bodies spawned: " +
            stats.waterBodies +
            "\nMaterial-safe visible trees spawned: " +
            stats.visibleTrees +
            "\nTerrain tree instances: " +
            terrainTrees +
            "\nTerrain detail placements: " +
            terrainDetails +
            "\nOrdinary rocks spawned: " +
            stats.rocks +
            "\nCave POIs spawned: " +
            stats.caves +
            "\nAmbient enemies spawned: " +
            stats.ambientEnemies +
            "\nWilderness treasure spawned: " +
            stats.treasure);
    }

    // ============================================================
    // PHYSICAL TERRAIN LANDFORMS
    // ============================================================

    private static IEnumerator BuildRegionalLandformsRoutine(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Action<int> completed)
    {
        if (terrain == null ||
            terrain.terrainData == null ||
            plan == null ||
            plan.regions == null ||
            plan.regions.Count == 0)
        {
            completed?.Invoke(0);
            yield break;
        }

        TerrainData data =
            terrain.terrainData;

        int resolution =
            data.heightmapResolution;

        if (resolution <= 1)
        {
            completed?.Invoke(0);
            yield break;
        }

        float[,] heights =
            data.GetHeights(
                0,
                0,
                resolution,
                resolution);

        int stamped =
            0;

        for (int regionIndex = 0;
             regionIndex < plan.regions.Count;
             regionIndex++)
        {
            GeneratedRegionRecord region =
                plan.regions[
                    regionIndex];

            if (region == null)
                continue;

            Vector3 regionCenter =
                YQGeneratedWorldLayout
                    .GetRegionCenter(
                        plan,
                        region,
                        terrain);

            int danger =
                Mathf.Clamp(
                    region.dangerTier,
                    0,
                    8);

            int target =
                BaseLandformsPerRegion +
                danger /
                2;

            /*
             * More attempts than targets because reserve zones and
             * terrain edges can reject candidates.
             */
            int attempts =
                target *
                8;

            int regionStamped =
                0;

            for (int attempt = 0;
                 attempt < attempts &&
                 regionStamped < target;
                 attempt++)
            {
                string seed =
                    plan.worldSeed +
                    "|terrain_landform|" +
                    region.regionId +
                    "|" +
                    attempt;

                float angle =
                    Deterministic01(
                        seed +
                        "|angle") *
                    Mathf.PI *
                    2f;

                float distance =
                    Mathf.Lerp(
                        LandformRadiusMin,
                        LandformRadiusMax,
                        Mathf.Sqrt(
                            Deterministic01(
                                seed +
                                "|distance")));

                Vector3 center =
                    new Vector3(
                        regionCenter.x +
                            Mathf.Cos(
                                angle) *
                            distance,
                        0f,
                        regionCenter.z +
                            Mathf.Sin(
                                angle) *
                            distance);

                bool mountain =
                    ShouldCreateMountainLandform(
                        region,
                        seed,
                        regionStamped);

                float radius =
                    ResolveLandformRadius(
                        region,
                        mountain,
                        seed);

                if (!InsideTerrainWithMargin(
                        terrain,
                        center,
                        radius +
                        6f))
                {
                    continue;
                }

                if (InsideOriginReserve(
                        center,
                        OriginLandformClearRadius))
                {
                    continue;
                }

                if (NearAnySettlement(
                        plan,
                        terrain,
                        center,
                        SettlementLandformClearRadius +
                        radius *
                        0.20f))
                {
                    continue;
                }

                if (NearAnyEncampment(
                        plan,
                        terrain,
                        center,
                        EncampmentLandformClearRadius +
                        radius *
                        0.15f))
                {
                    continue;
                }

                float amplitude =
                    ResolveLandformAmplitude(
                        terrain,
                        region,
                        mountain,
                        seed);

                float axisRatio =
                    Mathf.Lerp(
                        0.72f,
                        1.28f,
                        Deterministic01(
                            seed +
                            "|axis"));

                float rotation =
                    Deterministic01(
                        seed +
                        "|rotation") *
                    Mathf.PI *
                    2f;

                yield return ApplyTerrainLandformStampRoutine(
                    terrain,
                    heights,
                    center,
                    radius,
                    amplitude,
                    axisRatio,
                    rotation);

                regionStamped++;

                stamped++;

                // note: Separate completed landforms across frames so heightmap authorship and later terrain upload cannot combine into one presentation spike.
                yield return null;
            }

            Debug.Log(
                "[YQGeneratedWorldEnvironment] " +
                region.displayName +
                " terrain landforms: " +
                regionStamped +
                "/" +
                target);
        }

        const int uploadRowsPerFrame = 16;
        for (int startRow = 0;
             startRow < resolution;
             startRow += uploadRowsPerFrame)
        {
            int rowCount =
                Mathf.Min(
                    uploadRowsPerFrame,
                    resolution - startRow);

            float[,] strip =
                new float[
                    rowCount,
                    resolution];

            for (int row = 0; row < rowCount; row++)
            {
                for (int column = 0;
                     column < resolution;
                     column++)
                {
                    strip[row, column] =
                        heights[startRow + row, column];
                }
            }

            // note: Delayed strip writes are published by the later construction-terrain sync, avoiding one full-map upload stall here.
            data.SetHeightsDelayLOD(
                0,
                startRow,
                strip);
            yield return null;
        }

        completed?.Invoke(stamped);
    }

    private static bool ShouldCreateMountainLandform(
        GeneratedRegionRecord region,
        string seed,
        int index)
    {
        string text =
            BuildRegionSemanticText(
                region);

        float mountainChance =
            0.30f;

        if (ContainsAnySemantic(
                text,
                "mountain",
                "highland",
                "ridge",
                "rocky",
                "badland",
                "desert",
                "cliff",
                "crag",
                "volcanic"))
        {
            mountainChance =
                0.58f;
        }

        if (ContainsAnySemantic(
                text,
                "marsh",
                "swamp",
                "wetland",
                "plain"))
        {
            mountainChance *=
                0.45f;
        }

        /*
         * Guarantee occasional larger silhouettes even if generated
         * prose did not explicitly say "mountain".
         */
        if (index == 0 &&
            region != null &&
            region.dangerTier >= 3)
        {
            return true;
        }

        return
            Deterministic01(
                seed +
                "|mountain") <
            mountainChance;
    }

    private static float ResolveLandformRadius(
        GeneratedRegionRecord region,
        bool mountain,
        string seed)
    {
        int danger =
            region != null
                ? Mathf.Clamp(
                    region.dangerTier,
                    0,
                    8)
                : 0;

        if (mountain)
        {
            return
                Mathf.Lerp(
                    58f +
                        danger *
                        2.0f,
                    104f +
                        danger *
                        3.0f,
                    Deterministic01(
                        seed +
                        "|radius"));
        }

        return
            Mathf.Lerp(
                22f,
                46f +
                    danger,
                Deterministic01(
                    seed +
                    "|radius"));
    }

    private static float ResolveLandformAmplitude(
        Terrain terrain,
        GeneratedRegionRecord region,
        bool mountain,
        string seed)
    {
        if (terrain == null ||
            terrain.terrainData == null)
        {
            return 0f;
        }

        int danger =
            region != null
                ? Mathf.Clamp(
                    region.dangerTier,
                    0,
                    8)
                : 0;

        float amplitude;

        if (mountain)
        {
            amplitude =
                Mathf.Lerp(
                    18f +
                        danger *
                        1.1f,
                    42f +
                        danger *
                        2.0f,
                    Deterministic01(
                        seed +
                        "|height"));
        }
        else
        {
            amplitude =
                Mathf.Lerp(
                    2.5f,
                    7.5f +
                        danger *
                        0.5f,
                    Deterministic01(
                        seed +
                        "|height"));
        }

        /*
         * Never consume an unreasonable percentage of the terrain's
         * available vertical range.
         */
        return
            Mathf.Min(
                amplitude,
                terrain.terrainData.size.y *
                0.42f);
    }

    private static IEnumerator ApplyTerrainLandformStampRoutine(
        Terrain terrain,
        float[,] heights,
        Vector3 worldCenter,
        float radius,
        float amplitudeMeters,
        float axisRatio,
        float rotation)
    {
        if (terrain == null ||
            terrain.terrainData == null ||
            heights == null ||
            radius <= 0.1f ||
            amplitudeMeters <= 0.01f)
        {
            yield break;
        }

        TerrainData data =
            terrain.terrainData;

        int resolution =
            data.heightmapResolution;

        Vector3 terrainPosition =
            terrain.transform.position;

        Vector3 size =
            data.size;

        float normalizedX =
            (worldCenter.x -
             terrainPosition.x) /
            Mathf.Max(
                0.001f,
                size.x);

        float normalizedZ =
            (worldCenter.z -
             terrainPosition.z) /
            Mathf.Max(
                0.001f,
                size.z);

        int centerX =
            Mathf.RoundToInt(
                normalizedX *
                (resolution - 1));

        int centerZ =
            Mathf.RoundToInt(
                normalizedZ *
                (resolution - 1));

        int pixelRadiusX =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    radius /
                    size.x *
                    (resolution - 1) *
                    1.35f));

        int pixelRadiusZ =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    radius /
                    size.z *
                    (resolution - 1) *
                    1.35f));

        int minX =
            Mathf.Max(
                0,
                centerX -
                pixelRadiusX);

        int maxX =
            Mathf.Min(
                resolution - 1,
                centerX +
                pixelRadiusX);

        int minZ =
            Mathf.Max(
                0,
                centerZ -
                pixelRadiusZ);

        int maxZ =
            Mathf.Min(
                resolution - 1,
                centerZ +
                pixelRadiusZ);

        float cosine =
            Mathf.Cos(
                rotation);

        float sine =
            Mathf.Sin(
                rotation);

        float normalizedAmplitude =
            amplitudeMeters /
            Mathf.Max(
                0.001f,
                size.y);

        float frameStartedAt = Time.realtimeSinceStartup;

        for (int z = minZ;
             z <= maxZ;
             z++)
        {
            float worldZ =
                terrainPosition.z +
                z /
                (float)(
                    resolution - 1) *
                size.z;

            for (int x = minX;
                 x <= maxX;
                 x++)
            {
                float worldX =
                    terrainPosition.x +
                    x /
                    (float)(
                        resolution - 1) *
                    size.x;

                float dx =
                    worldX -
                    worldCenter.x;

                float dz =
                    worldZ -
                    worldCenter.z;

                float rotatedX =
                    dx *
                        cosine -
                    dz *
                        sine;

                float rotatedZ =
                    dx *
                        sine +
                    dz *
                        cosine;

                float scaledX =
                    rotatedX /
                    Mathf.Max(
                        0.2f,
                        axisRatio);

                float scaledZ =
                    rotatedZ *
                    Mathf.Max(
                        0.2f,
                        axisRatio);

                float distance =
                    Mathf.Sqrt(
                        scaledX *
                            scaledX +
                        scaledZ *
                            scaledZ);

                if (distance >=
                    radius)
                {
                    continue;
                }

                float t =
                    1f -
                    distance /
                    radius;

                /*
                 * Smooth hill profile.
                 *
                 * No vertical walls, holes or disconnected geometry.
                 */
                float smooth =
                    t *
                    t *
                    (3f -
                     2f *
                     t);

                /*
                 * Slightly sharpen the center without producing a
                 * needle-shaped peak.
                 */
                float shaped =
                    Mathf.Lerp(
                        smooth,
                        smooth *
                        smooth,
                        0.28f);

                heights[
                    z,
                    x] =
                    Mathf.Clamp01(
                        heights[
                            z,
                            x] +
                        normalizedAmplitude *
                        shaped);
            }

            if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
            {
                // note: Broad mountain silhouettes are authored a few height rows at a time so improved terrain cannot reintroduce a loading-screen hard frame.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }
    }

    // ============================================================
    // TERRAIN SURFACE
    // ============================================================

    private static IEnumerator
        ApplyRegionalTerrainLayersRoutine(
            Terrain terrain,
            GeneratedWorldPlanRecord plan,
            YQRuntimeWorldAssetRegistry registry,
            Action<List<RegionSurface>> completed)
    {
        List<RegionSurface> surfaces =
            new List<RegionSurface>();

        List<TerrainLayer> layers =
            new List<TerrainLayer>();

        Dictionary<string, int>
            layerByMaterialPath =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < plan.regions.Count;
             i++)
        {
            GeneratedRegionRecord region =
                plan.regions[i];

            if (region == null)
                continue;

            GeneratedRegionAssetPaletteRecord palette =
                FindPalette(
                    plan,
                    region);

            if (palette == null)
                continue;

            palette.EnsureCollections();

            List<GeneratedAssetReferenceRecord> terrainReferences =
                FindResolvableTerrainMaterials(
                    palette,
                    registry);

            if (terrainReferences.Count == 0)
            {
                Debug.LogWarning(
                    "[YQGeneratedWorldEnvironment] " +
                    "No runtime terrain material resolved for region: " +
                    region.displayName);

                continue;
            }

            int baseLayerIndex =
                ResolveOrCreateTerrainLayerIndex(
                    terrainReferences[0],
                    registry,
                    palette,
                    layers,
                    layerByMaterialPath);

            if (baseLayerIndex < 0)
                continue;

            int detailLayerIndex =
                terrainReferences.Count > 1
                    ? ResolveOrCreateTerrainLayerIndex(
                        terrainReferences[1],
                        registry,
                        palette,
                        layers,
                        layerByMaterialPath)
                    : baseLayerIndex;

            GeneratedAssetReferenceRecord rockReference =
                FindRockTerrainReference(
                    terrainReferences);

            int rockLayerIndex =
                rockReference != null
                    ? ResolveOrCreateTerrainLayerIndex(
                        rockReference,
                        registry,
                        palette,
                        layers,
                        layerByMaterialPath)
                    : detailLayerIndex;

            GeneratedAssetReferenceRecord pathReference =
                FindPathTerrainReference(
                    terrainReferences);

            int pathLayerIndex =
                pathReference != null
                    ? ResolveOrCreateTerrainLayerIndex(
                        pathReference,
                        registry,
                        palette,
                        layers,
                        layerByMaterialPath)
                    : detailLayerIndex;

            if (detailLayerIndex < 0)
                detailLayerIndex = baseLayerIndex;

            if (rockLayerIndex < 0)
                rockLayerIndex = detailLayerIndex;

            if (pathLayerIndex < 0)
                pathLayerIndex = detailLayerIndex;

            surfaces.Add(
                new RegionSurface
                {
                    region =
                        region,

                    palette =
                        palette,

                    center =
                        YQGeneratedWorldLayout
                            .GetRegionCenter(
                                plan,
                                region),

                    baseLayerIndex =
                        baseLayerIndex,

                    detailLayerIndex =
                        detailLayerIndex,

                    rockLayerIndex =
                        rockLayerIndex,

                    pathLayerIndex =
                        pathLayerIndex
                });
        }

        if (layers.Count == 0)
        {
            Debug.LogWarning(
                "[YQGeneratedWorldEnvironment] " +
                "No usable palette terrain textures were found. " +
                "Leaving Terrain layers unchanged.");

            completed?.Invoke(surfaces);
            yield break;
        }

        TerrainData data =
            terrain.terrainData;

        data.terrainLayers =
            layers.ToArray();

        data.alphamapResolution =
            AlphamapResolution;

        yield return PaintRegionalTerrainRoutine(
            terrain,
            plan,
            surfaces,
            layers.Count);

        // note: SetAlphamaps publishes each completed strip; avoiding a redundant global Terrain.Flush prevents a second full native refresh after the final strip.
        yield return null;
        completed?.Invoke(surfaces);
    }

    private static int ResolveOrCreateTerrainLayerIndex(
        GeneratedAssetReferenceRecord reference,
        YQRuntimeWorldAssetRegistry registry,
        GeneratedRegionAssetPaletteRecord palette,
        List<TerrainLayer> layers,
        Dictionary<string, int> layerByMaterialPath)
    {
        if (reference == null ||
            registry == null ||
            layers == null ||
            layerByMaterialPath == null)
        {
            return -1;
        }

        string materialPath =
            YQRuntimeWorldAssetRegistry.NormalizePath(
                reference.assetPath);

        if (layerByMaterialPath.TryGetValue(
                materialPath,
                out int existingIndex))
        {
            return existingIndex;
        }

        Material material =
            registry.ResolveMaterial(
                materialPath);

        TerrainLayer layer =
            CreateTerrainLayer(
                material,
                palette,
                materialPath);

        if (layer == null)
            return -1;

        int createdIndex =
            layers.Count;

        layers.Add(
            layer);

        layerByMaterialPath[materialPath] =
            createdIndex;

        return createdIndex;
    }

    private static GeneratedAssetReferenceRecord FindRockTerrainReference(
        List<GeneratedAssetReferenceRecord> references)
    {
        if (references == null || references.Count == 0)
            return null;

        for (int index = references.Count - 1;
             index >= 0;
             index--)
        {
            GeneratedAssetReferenceRecord reference =
                references[index];

            if (reference != null &&
                ContainsAnySemantic(
                    BuildReferenceSemanticText(reference),
                    "rock",
                    "stone",
                    "ridge",
                    "highland",
                    "gravel"))
            {
                return reference;
            }
        }

        return references.Count > 2
            ? references[2]
            : references[references.Count - 1];
    }

    private static GeneratedAssetReferenceRecord FindPathTerrainReference(
        List<GeneratedAssetReferenceRecord> references)
    {
        if (references == null || references.Count == 0)
            return null;

        for (int index = 0;
             index < references.Count;
             index++)
        {
            GeneratedAssetReferenceRecord reference =
                references[index];

            if (reference != null &&
                ContainsAnySemantic(
                    BuildReferenceSemanticText(reference),
                    "packed_earth",
                    "earth",
                    "dirt",
                    "dry_ground",
                    "dark_ground",
                    "gravel"))
            {
                return reference;
            }
        }

        // note: A lived-area path always reuses a curated regional surface; it never introduces a cross-biome material or generated placeholder.
        return references.Count > 1
            ? references[1]
            : references[0];
    }

    private static List<GeneratedAssetReferenceRecord>
        FindResolvableTerrainMaterials(
            GeneratedRegionAssetPaletteRecord palette,
            YQRuntimeWorldAssetRegistry registry)
    {
        List<GeneratedAssetReferenceRecord> result =
            new List<GeneratedAssetReferenceRecord>(8);

        if (palette == null ||
            palette.terrainMaterials == null)
        {
            return result;
        }

        HashSet<string> acceptedPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < palette.terrainMaterials.Count &&
             result.Count < 8;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                palette.terrainMaterials[i];

            if (reference == null ||
                string.IsNullOrWhiteSpace(
                    reference.assetPath))
            {
                continue;
            }

            string materialPath =
                YQRuntimeWorldAssetRegistry.NormalizePath(
                    reference.assetPath);

            if (!acceptedPaths.Add(materialPath))
                continue;

            Material material =
                registry.ResolveMaterial(
                    materialPath);

            if (material == null)
                continue;

            Texture2D diffuse =
                FindTexture(
                    material,
                    "_BaseMap",
                    "_MainTex",
                    "_BaseColorMap",
                    "_Albedo",
                    "_Diffuse");

            if (diffuse != null)
            {
                result.Add(reference);
                continue;
            }

            try
            {
                if (material.mainTexture
                    is Texture2D)
                {
                    result.Add(reference);
                }
            }
            catch
            {
            }
        }

        return result;
    }

    private static TerrainLayer CreateTerrainLayer(
        Material material,
        GeneratedRegionAssetPaletteRecord palette,
        string materialPath)
    {
        if (material == null)
            return null;

        Texture2D diffuse =
            FindTexture(
                material,
                "_BaseMap",
                "_MainTex",
                "_BaseColorMap",
                "_Albedo",
                "_Diffuse");

        if (diffuse == null)
        {
            try
            {
                diffuse =
                    material.mainTexture
                    as Texture2D;
            }
            catch
            {
            }
        }

        if (diffuse == null)
            return null;

        Texture2D normal =
            FindTexture(
                material,
                "_BumpMap",
                "_NormalMap",
                "_NormalTex");

        TerrainLayer layer =
            new TerrainLayer();

        layer.name =
            "YQ_RuntimeTerrain_" +
            SafeName(
                palette != null
                    ? palette.styleKey
                    : "surface") +
            "_" +
            SafeName(
                material.name);

        layer.hideFlags =
            HideFlags.DontSave;

        layer.diffuseTexture =
            diffuse;

        if (normal != null)
        {
            layer.normalMapTexture =
                normal;

            layer.normalScale =
                1f;
        }

        float tileSize =
            ResolveTerrainTileSize(
                palette);

        layer.tileSize =
            new Vector2(
                tileSize,
                tileSize);

        layer.tileOffset =
            Vector2.zero;

        return layer;
    }

    private static float ResolveTerrainTileSize(
        GeneratedRegionAssetPaletteRecord palette)
    {
        string style =
            palette != null
                ? palette.styleKey ??
                  string.Empty
                : string.Empty;

        style =
            style.ToLowerInvariant();

        if (style.Contains("desert"))
            return 18f;

        if (style.Contains("scifi") ||
            style.Contains("container"))
        {
            return 10f;
        }

        if (style.Contains("asian") ||
            style.Contains("persepolis"))
        {
            return 14f;
        }

        return 12f;
    }

    private static IEnumerator PaintRegionalTerrainRoutine(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        List<RegionSurface> surfaces,
        int layerCount)
    {
        if (terrain == null ||
            terrain.terrainData == null ||
            surfaces == null ||
            surfaces.Count == 0 ||
            layerCount <= 0)
        {
            yield break;
        }

        TerrainData data =
            terrain.terrainData;

        int resolution =
            data.alphamapResolution;

        float[,,] weights =
            new float[
                resolution,
                resolution,
                layerCount];

        Vector3 terrainPosition =
            terrain.transform.position;

        Vector3 terrainSize =
            data.size;

        Vector3 originAnchor =
            YQGeneratedWorldLayout.GetVeyOriginAnchor();

        List<LivedPathSegment> livedPaths =
            BuildLivedPathNetwork(
                plan,
                terrain);

        MacroWaterSet macroWater =
            BuildMacroWaterSet(
                terrain,
                plan);

        // note: One bounded height read supplies slope and elevation masks for every splat pixel; repeated native Terrain queries inside the 256x256 paint loop caused avoidable loading spikes.
        float[,] heightSamples =
            data.GetHeights(
                0,
                0,
                data.heightmapResolution,
                data.heightmapResolution);

        int heightResolution =
            data.heightmapResolution;

        float heightSpacingX =
            terrainSize.x /
            Mathf.Max(
                1,
                heightResolution - 1);

        float heightSpacingZ =
            terrainSize.z /
            Mathf.Max(
                1,
                heightResolution - 1);

        yield return null;

        float frameStartedAt =
            Time.realtimeSinceStartup;

        for (int y = 0;
             y < resolution;
             y++)
        {
            float normalizedZ =
                y /
                (float)(
                    resolution - 1);

            float worldZ =
                terrainPosition.z +
                normalizedZ *
                terrainSize.z;

            for (int x = 0;
                 x < resolution;
                 x++)
            {
                float normalizedX =
                    x /
                    (float)(
                        resolution - 1);

                float worldX =
                    terrainPosition.x +
                    normalizedX *
                        terrainSize.x;

                int heightX =
                    Mathf.Clamp(
                        Mathf.RoundToInt(
                            normalizedX *
                            (heightResolution - 1)),
                        0,
                        heightResolution - 1);

                int heightZ =
                    Mathf.Clamp(
                        Mathf.RoundToInt(
                            normalizedZ *
                            (heightResolution - 1)),
                        0,
                        heightResolution - 1);

                int previousHeightX =
                    Mathf.Max(
                        0,
                        heightX - 1);

                int nextHeightX =
                    Mathf.Min(
                        heightResolution - 1,
                        heightX + 1);

                int previousHeightZ =
                    Mathf.Max(
                        0,
                        heightZ - 1);

                int nextHeightZ =
                    Mathf.Min(
                        heightResolution - 1,
                        heightZ + 1);

                float slopeX =
                    (heightSamples[heightZ, nextHeightX] -
                     heightSamples[heightZ, previousHeightX]) *
                    terrainSize.y /
                    Mathf.Max(
                        heightSpacingX,
                        (nextHeightX - previousHeightX) *
                        heightSpacingX);

                float slopeZ =
                    (heightSamples[nextHeightZ, heightX] -
                     heightSamples[previousHeightZ, heightX]) *
                    terrainSize.y /
                    Mathf.Max(
                        heightSpacingZ,
                        (nextHeightZ - previousHeightZ) *
                        heightSpacingZ);

                float slope =
                    Mathf.Clamp01(
                        Mathf.Sqrt(
                            slopeX * slopeX +
                            slopeZ * slopeZ) /
                        1.15f);

                float elevation =
                    heightSamples[
                        heightZ,
                        heightX];

                float totalWeight =
                    0f;

                RegionSurface nearestSurface =
                    null;

                float nearestDistanceSquared =
                    float.PositiveInfinity;

                for (int regionIndex = 0;
                     regionIndex < surfaces.Count;
                     regionIndex++)
                {
                    RegionSurface surface =
                        surfaces[
                            regionIndex];

                    float dx =
                        worldX -
                        surface.center.x;

                    float dz =
                        worldZ -
                        surface.center.z;

                    float distanceSquared =
                        dx *
                            dx +
                        dz *
                            dz;

                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearestDistanceSquared = distanceSquared;
                        nearestSurface = surface;
                    }

                    float influence =
                        1f /
                        Mathf.Max(
                            100f,
                            distanceSquared);

                    influence *=
                        influence;

                    float detailNoise =
                        Mathf.PerlinNoise(
                            (worldX + surface.center.x * 0.37f) * 0.018f + 19.3f,
                            (worldZ + surface.center.z * 0.41f) * 0.018f + 7.1f);

                    float rockMask =
                        Mathf.Clamp01(
                            Mathf.SmoothStep(
                                0.16f,
                                0.72f,
                                slope) +
                            Mathf.SmoothStep(
                                0.34f,
                                0.78f,
                                elevation) *
                            0.38f);

                    float detailMask =
                        Mathf.Clamp01(
                            (0.20f +
                             detailNoise * 0.80f) *
                            (1f - rockMask * 0.72f));

                    float baseShare =
                        Mathf.Max(
                            0.08f,
                            1f -
                            detailMask * 0.48f -
                            rockMask * 0.82f);

                    float detailShare =
                        0.10f +
                        detailMask * 0.74f;

                    float rockShare =
                        0.04f +
                        rockMask * 1.45f;

                    float baseWeight =
                        influence *
                        baseShare;

                    float detailWeight =
                        influence *
                        detailShare;

                    float rockWeight =
                        influence *
                        rockShare;

                    if (surface.baseLayerIndex >= 0 &&
                        surface.baseLayerIndex < layerCount)
                    {
                        weights[y, x, surface.baseLayerIndex] +=
                            baseWeight;
                        totalWeight +=
                            baseWeight;
                    }

                    if (surface.detailLayerIndex >= 0 &&
                        surface.detailLayerIndex < layerCount)
                    {
                        weights[y, x, surface.detailLayerIndex] +=
                            detailWeight;
                        totalWeight +=
                            detailWeight;
                    }

                    if (surface.rockLayerIndex >= 0 &&
                        surface.rockLayerIndex < layerCount)
                    {
                        // note: Stone follows actual normalized slope/elevation while low-frequency detail breaks up broad flat color fields without cross-biome randomization.
                        weights[y, x, surface.rockLayerIndex] +=
                            rockWeight;
                        totalWeight +=
                            rockWeight;
                    }
                }

                float originLocalZ =
                    worldZ - originAnchor.z;

                if (totalWeight > 0.0000001f && nearestSurface != null &&
                    originLocalZ >= -118f && originLocalZ <= 30f)
                {
                    float longitudinalFade =
                        Mathf.SmoothStep(-118f, -100f, originLocalZ) *
                        (1f - Mathf.SmoothStep(18f, 30f, originLocalZ));

                    float trailCenterX =
                        originAnchor.x +
                        Mathf.Sin((originLocalZ + 118f) * 0.041f) * 3.2f;

                    float trailMask =
                        (1f - Mathf.SmoothStep(
                            3.2f,
                            8.5f,
                            Mathf.Abs(worldX - trailCenterX))) *
                        longitudinalFade;

                    if (trailMask > 0.001f &&
                        nearestSurface.detailLayerIndex >= 0 &&
                        nearestSurface.detailLayerIndex < layerCount)
                    {
                        // note: The fixed Goddess approach is a restrained biome-detail corridor painted into the authoritative terrain, never a floating road mesh or a cross-palette prop strip.
                        float trailWeight =
                            totalWeight * trailMask * 4.2f;

                        weights[y, x, nearestSurface.detailLayerIndex] +=
                            trailWeight;

                        totalWeight +=
                            trailWeight;
                    }
                }

                if (totalWeight > 0.0000001f &&
                    nearestSurface != null &&
                    nearestSurface.pathLayerIndex >= 0 &&
                    nearestSurface.pathLayerIndex < layerCount)
                {
                    float livedPathMask = ResolveLivedPathMask(
                        livedPaths,
                        new Vector2(worldX, worldZ));

                    Vector3 pathSurfacePoint =
                        new Vector3(
                            worldX,
                            terrain.transform.position.y +
                                elevation * terrainSize.y,
                            worldZ);

                    if (IsSubmergedByMacroWater(
                            terrain,
                            plan,
                            pathSurfacePoint,
                            0f,
                            macroWater))
                    {
                        // note: Roads stop at real shorelines and resume on raised construction shelves instead of painting a dry stripe across open water.
                        livedPathMask = 0f;
                    }

                    if (livedPathMask > 0.001f)
                    {
                        // note: Lived settlements and camps receive one readable packed-earth spine with soft natural shoulders instead of disconnected road props or a uniform city-wide decal.
                        float pathWeight =
                            totalWeight * livedPathMask * 6.5f;
                        weights[
                            y,
                            x,
                            nearestSurface.pathLayerIndex] +=
                            pathWeight;
                        totalWeight +=
                            pathWeight;
                    }
                }

                if (totalWeight <=
                    0.0000001f)
                {
                    weights[
                        y,
                        x,
                        0] =
                        1f;

                    continue;
                }

                for (int layer = 0;
                     layer < layerCount;
                     layer++)
                {
                    weights[
                        y,
                        x,
                        layer] /=
                        totalWeight;
                }
            }

            if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
            {
                // note: Regional material weighting yields often enough for the Goddess presentation to retain its visual heartbeat.
                yield return null;
                frameStartedAt =
                    Time.realtimeSinceStartup;
            }
        }

        const int uploadRowsPerFrame = 16;
        for (int startRow = 0;
             startRow < resolution;
             startRow += uploadRowsPerFrame)
        {
            int rowCount =
                Mathf.Min(
                    uploadRowsPerFrame,
                    resolution - startRow);

            float[,,] strip =
                new float[
                    rowCount,
                    resolution,
                    layerCount];

            for (int row = 0; row < rowCount; row++)
            {
                for (int column = 0; column < resolution; column++)
                {
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        strip[row, column, layer] =
                            weights[startRow + row, column, layer];
                    }
                }
            }

            // note: Alpha-map publication is striped instead of issuing one full terrain-texture upload on the presentation frame.
            data.SetAlphamaps(
                0,
                startRow,
                strip);
            yield return null;
        }
    }

    private static List<LivedPathSegment> BuildLivedPathNetwork(
        GeneratedWorldPlanRecord plan,
        Terrain terrain)
    {
        List<LivedPathSegment> paths =
            new List<LivedPathSegment>();

        if (plan == null || terrain == null)
            return paths;

        MacroWaterSet macroWater = BuildMacroWaterSet(terrain, plan);

        Vector2 origin = new Vector2(
            YQGeneratedWorldLayout.GetVeyOriginAnchor().x,
            YQGeneratedWorldLayout.GetVeyOriginAnchor().z);
        List<Vector2> settlementAnchors =
            new List<Vector2>();

        if (plan.settlements != null)
        {
            for (int index = 0;
                 index < plan.settlements.Count;
                 index++)
            {
                GeneratedSettlementRecord settlement =
                    plan.settlements[index];

                if (settlement == null)
                    continue;

                Vector3 worldAnchor =
                    YQGeneratedWorldLayout.GetSettlementAnchor(
                        plan,
                        settlement,
                        terrain);
                settlementAnchors.Add(
                    new Vector2(worldAnchor.x, worldAnchor.z));
            }
        }

        for (int index = 0;
             index < settlementAnchors.Count;
             index++)
        {
            Vector2 anchor = settlementAnchors[index];
            Vector2 target = origin;
            float nearestDistanceSquared =
                (anchor - origin).sqrMagnitude;

            for (int otherIndex = 0;
                 otherIndex < settlementAnchors.Count;
                 otherIndex++)
            {
                if (otherIndex == index)
                    continue;

                float distanceSquared =
                    (anchor - settlementAnchors[otherIndex]).sqrMagnitude;

                if (distanceSquared >= nearestDistanceSquared)
                    continue;

                target = settlementAnchors[otherIndex];
                nearestDistanceSquared = distanceSquared;
            }

            AddLivedPathApproach(
                paths,
                anchor,
                target,
                42f,
                118f,
                3.8f,
                6.5f,
                plan.worldSeed + "|settlement_path|" + index);

            if (nearestDistanceSquared > 90f * 90f)
            {
                // note: Lived settlements retain their local main street and also join a continuous regional road, so the world never degenerates into isolated painted islands.
                AddLivedPathConnection(
                    paths,
                    anchor,
                    target,
                    3.1f,
                    5.8f,
                    plan.worldSeed + "|settlement_connector|" + index);
            }
        }

        if (plan.encampments != null)
        {
            for (int index = 0;
                 index < plan.encampments.Count;
                 index++)
            {
                GeneratedEncampmentRecord encampment =
                    plan.encampments[index];

                if (encampment == null)
                    continue;

                Vector3 worldAnchor =
                    YQGeneratedWorldLayout.GetEncampmentAnchor(
                        plan,
                        encampment,
                        terrain);
                Vector2 anchor =
                    new Vector2(worldAnchor.x, worldAnchor.z);
                Vector2 target = origin;
                float nearestDistanceSquared =
                    (anchor - origin).sqrMagnitude;

                for (int settlementIndex = 0;
                     settlementIndex < settlementAnchors.Count;
                     settlementIndex++)
                {
                    float distanceSquared =
                        (anchor - settlementAnchors[settlementIndex]).sqrMagnitude;

                    if (distanceSquared >= nearestDistanceSquared)
                        continue;

                    target = settlementAnchors[settlementIndex];
                    nearestDistanceSquared = distanceSquared;
                }

                AddLivedPathApproach(
                    paths,
                    anchor,
                    target,
                    22f,
                    72f,
                    2.4f,
                    4.2f,
                    plan.worldSeed + "|encampment_path|" + index);

                if (nearestDistanceSquared > 82f * 82f)
                {
                    AddLivedPathConnection(
                        paths,
                        anchor,
                        target,
                        2.2f,
                        4f,
                        plan.worldSeed + "|encampment_connector|" + index);
                }
            }
        }

        if (plan.regions != null)
        {
            for (int regionIndex = 0;
                 regionIndex < plan.regions.Count;
                 regionIndex++)
            {
                GeneratedRegionRecord region =
                    plan.regions[regionIndex];
                GeneratedRegionAssetPaletteRecord palette =
                    region != null
                        ? FindPalette(plan, region)
                        : null;

                if (region == null ||
                    !PaletteHasCaveReference(palette))
                {
                    continue;
                }

                Vector3 regionCenterWorld =
                    YQGeneratedWorldLayout.GetRegionCenter(
                        plan,
                        region,
                        terrain);
                int desired =
                    region.dangerTier >= 5
                        ? 3
                        : region.dangerTier >= 3
                            ? 2
                            : 1;

                for (int caveIndex = 0;
                     caveIndex < desired;
                     caveIndex++)
                {
                    for (int attempt = 0;
                         attempt < 10;
                         attempt++)
                    {
                        string caveSeed =
                            plan.worldSeed +
                            "|wilderness_cave|" +
                            region.regionId +
                            "|" +
                            caveIndex +
                            "|" +
                            attempt;

                        if (!TryResolveWildernessPosition(
                                terrain,
                                plan,
                                regionCenterWorld,
                                caveSeed,
                                78f,
                                WildernessRadiusMax,
                                52f,
                                72f,
                                42f,
                                out Vector3 cavePosition,
                                macroWater))
                        {
                            continue;
                        }

                        // note: Cave entrances receive a narrow terraced approach aimed back toward regional travel space, creating readable climb paths instead of disconnected door meshes on slopes.
                        AddLivedPathApproach(
                            paths,
                            new Vector2(
                                cavePosition.x,
                                cavePosition.z),
                            new Vector2(
                                regionCenterWorld.x,
                                regionCenterWorld.z),
                            7f,
                            82f,
                            2.1f,
                            3.8f,
                            caveSeed + "|approach",
                            true);
                        break;
                    }
                }
            }
        }

        return paths;
    }

    private static void AddLivedPathApproach(
        List<LivedPathSegment> paths,
        Vector2 anchor,
        Vector2 target,
        float rearLength,
        float approachLength,
        float halfWidth,
        float shoulderWidth,
        string seed,
        bool terraced = false)
    {
        Vector2 direction = target - anchor;
        float distance = direction.magnitude;

        if (paths == null || distance < 0.1f)
            return;

        direction /= distance;
        float forwardLength = Mathf.Min(
            approachLength,
            distance * 0.48f);

        // note: Each lived location owns a continuous local spine aimed at its nearest hub; the route remains bounded instead of carving a straight road across the entire procedural world.
        paths.Add(
            new LivedPathSegment
            {
                start = anchor - direction * rearLength,
                end = anchor + direction * forwardLength,
                halfWidth = halfWidth,
                shoulderWidth = shoulderWidth,
                curveAmplitude = Mathf.Lerp(
                    1.4f,
                    4.5f,
                    Deterministic01(seed + "|curve")),
                curvePhase = Deterministic01(seed + "|phase") *
                    Mathf.PI * 2f,
                terraced = terraced
            });
    }

    private static void AddLivedPathConnection(
        List<LivedPathSegment> paths,
        Vector2 start,
        Vector2 end,
        float halfWidth,
        float shoulderWidth,
        string seed)
    {
        if (paths == null ||
            (end - start).sqrMagnitude < 1f)
        {
            return;
        }

        paths.Add(
            new LivedPathSegment
            {
                start = start,
                end = end,
                halfWidth = halfWidth,
                shoulderWidth = shoulderWidth,
                curveAmplitude = Mathf.Lerp(
                    6f,
                    19f,
                    Deterministic01(seed + "|curve")),
                curvePhase = Deterministic01(seed + "|phase") *
                    Mathf.PI * 2f,
                terraced = false
            });
    }

    private static bool PaletteHasCaveReference(
        GeneratedRegionAssetPaletteRecord palette)
    {
        if (palette == null || palette.enemySite == null)
            return false;

        for (int index = 0;
             index < palette.enemySite.Count;
             index++)
        {
            if (IsCaveReference(palette.enemySite[index]))
                return true;
        }

        return false;
    }

    private static float ResolveLivedPathMask(
        List<LivedPathSegment> paths,
        Vector2 point)
    {
        float strongest = 0f;

        for (int index = 0;
             paths != null && index < paths.Count;
             index++)
        {
            LivedPathSegment path = paths[index];
            float distance = DistanceToLivedPath(path, point);
            float mask = 1f - Mathf.SmoothStep(
                path.halfWidth,
                path.halfWidth + path.shoulderWidth,
                distance);
            strongest = Mathf.Max(strongest, mask);
        }

        return strongest;
    }

    private static bool IsNearLivedPath(
        List<LivedPathSegment> paths,
        Vector3 position,
        float padding)
    {
        Vector2 point = new Vector2(position.x, position.z);

        for (int index = 0;
             paths != null && index < paths.Count;
             index++)
        {
            LivedPathSegment path = paths[index];

            if (DistanceToLivedPath(path, point) <=
                path.halfWidth + Mathf.Max(0f, padding))
            {
                return true;
            }
        }

        return false;
    }

    private static float DistanceToLivedPath(
        LivedPathSegment path,
        Vector2 point)
    {
        return TryResolveLivedPathSample(
                path,
                point,
                out _,
                out _,
                out float distance)
            ? distance
            : float.PositiveInfinity;
    }

    private static bool TryResolveLivedPathSample(
        LivedPathSegment path,
        Vector2 point,
        out float pathT,
        out Vector2 center,
        out float distance)
    {
        pathT = 0f;
        center = Vector2.zero;
        distance = float.PositiveInfinity;

        if (path == null)
            return false;

        Vector2 delta = path.end - path.start;
        float lengthSquared = delta.sqrMagnitude;

        if (lengthSquared < 0.001f)
        {
            center = path.start;
            distance = Vector2.Distance(point, center);
            return true;
        }

        pathT = Mathf.Clamp01(
            Vector2.Dot(point - path.start, delta) /
            lengthSquared);
        center = ResolveLivedPathCenter(path, pathT);
        distance = Vector2.Distance(point, center);
        return true;
    }

    private static Vector2 ResolveLivedPathCenter(
        LivedPathSegment path,
        float pathT)
    {
        if (path == null)
            return Vector2.zero;

        pathT = Mathf.Clamp01(pathT);
        Vector2 delta = path.end - path.start;

        if (delta.sqrMagnitude < 0.001f)
            return path.start;

        Vector2 direction = delta.normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float curve =
            Mathf.Sin(pathT * Mathf.PI) *
            Mathf.Sin(
                pathT * Mathf.PI * 2f +
                path.curvePhase) *
            path.curveAmplitude;

        return Vector2.Lerp(path.start, path.end, pathT) +
            perpendicular * curve;
    }

    private static float[] BuildLivedPathHeightProfile(
        LivedPathSegment path,
        float pathLength,
        float[,] heights,
        Vector3 terrainPosition,
        Vector3 terrainSize)
    {
        int sampleCount = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Max(1f, pathLength) / 4f) + 1,
            2,
            257);
        float[] profile = new float[sampleCount];
        float[] scratch = new float[sampleCount];

        for (int index = 0; index < sampleCount; index++)
        {
            float pathT = index / (float)(sampleCount - 1);
            profile[index] = SampleHeightmapWorldHeight(
                heights,
                terrainPosition,
                terrainSize,
                ResolveLivedPathCenter(path, pathT));
        }

        for (int pass = 0; pass < 2; pass++)
        {
            scratch[0] = profile[0];
            scratch[sampleCount - 1] = profile[sampleCount - 1];

            for (int index = 1; index < sampleCount - 1; index++)
            {
                scratch[index] =
                    profile[index - 1] * 0.25f +
                    profile[index] * 0.5f +
                    profile[index + 1] * 0.25f;
            }

            float[] swap = profile;
            profile = scratch;
            scratch = swap;
        }

        // note: Roads derive elevation from a lightly smoothed local centerline profile, following hills and valleys instead of cutting a single endpoint-to-endpoint ramp through the world.
        return profile;
    }

    private static float SampleLivedPathHeightProfile(
        float[] profile,
        float pathT)
    {
        if (profile == null || profile.Length == 0)
            return 0f;

        if (profile.Length == 1)
            return profile[0];

        float sample = Mathf.Clamp01(pathT) * (profile.Length - 1);
        int lower = Mathf.FloorToInt(sample);
        int upper = Mathf.Min(profile.Length - 1, lower + 1);
        return Mathf.Lerp(profile[lower], profile[upper], sample - lower);
    }

    private static bool IsInsideLivedPathTerrainReservation(
        IReadOnlyList<LivedPathTerrainReservation> reservations,
        Vector2 point)
    {
        for (int index = 0;
             reservations != null && index < reservations.Count;
             index++)
        {
            LivedPathTerrainReservation reservation = reservations[index];

            if ((reservation.center - point).sqrMagnitude <=
                reservation.radius * reservation.radius)
            {
                return true;
            }
        }

        return false;
    }

    private static float SampleHeightmapWorldHeight(
        float[,] heights,
        Vector3 terrainPosition,
        Vector3 terrainSize,
        Vector2 worldPoint)
    {
        if (heights == null ||
            heights.GetLength(0) <= 1 ||
            heights.GetLength(1) <= 1)
        {
            return terrainPosition.y;
        }

        int height = heights.GetLength(0);
        int width = heights.GetLength(1);
        float sampleX = Mathf.Clamp01(
                (worldPoint.x - terrainPosition.x) /
                Mathf.Max(0.001f, terrainSize.x)) *
            (width - 1);
        float sampleZ = Mathf.Clamp01(
                (worldPoint.y - terrainPosition.z) /
                Mathf.Max(0.001f, terrainSize.z)) *
            (height - 1);
        int x0 = Mathf.FloorToInt(sampleX);
        int z0 = Mathf.FloorToInt(sampleZ);
        int x1 = Mathf.Min(width - 1, x0 + 1);
        int z1 = Mathf.Min(height - 1, z0 + 1);
        float tx = sampleX - x0;
        float tz = sampleZ - z0;
        float normalized = Mathf.Lerp(
            Mathf.Lerp(heights[z0, x0], heights[z0, x1], tx),
            Mathf.Lerp(heights[z1, x0], heights[z1, x1], tx),
            tz);

        return terrainPosition.y +
            normalized *
            terrainSize.y;
    }

    private static float SampleHeightmapSlopeDegrees(
        float[,] heights,
        Vector3 terrainSize,
        float normalizedX,
        float normalizedZ)
    {
        if (heights == null ||
            heights.GetLength(0) <= 1 ||
            heights.GetLength(1) <= 1)
        {
            return 0f;
        }

        int height = heights.GetLength(0);
        int width = heights.GetLength(1);
        int centerX = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Clamp01(normalizedX) * (width - 1)),
            0,
            width - 1);
        int centerZ = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Clamp01(normalizedZ) * (height - 1)),
            0,
            height - 1);
        int leftX = Mathf.Max(0, centerX - 1);
        int rightX = Mathf.Min(width - 1, centerX + 1);
        int lowerZ = Mathf.Max(0, centerZ - 1);
        int upperZ = Mathf.Min(height - 1, centerZ + 1);
        float horizontalDistanceX = Mathf.Max(
            0.001f,
            (rightX - leftX) * terrainSize.x / (width - 1));
        float horizontalDistanceZ = Mathf.Max(
            0.001f,
            (upperZ - lowerZ) * terrainSize.z / (height - 1));
        float gradientX =
            (heights[centerZ, rightX] - heights[centerZ, leftX]) *
            terrainSize.y /
            horizontalDistanceX;
        float gradientZ =
            (heights[upperZ, centerX] - heights[lowerZ, centerX]) *
            terrainSize.y /
            horizontalDistanceZ;

        // note: Managed central differences replace a native terrain-normal call for every foliage cell while preserving the same physical slope rejection.
        return Mathf.Atan(
                Mathf.Sqrt(
                    gradientX * gradientX +
                    gradientZ * gradientZ)) *
            Mathf.Rad2Deg;
    }

    private static bool IsTerrainDetailPositionAllowed(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Vector3 candidate,
        List<Vector2> settlementCenters,
        List<Vector2> encampmentCenters,
        float settlementClearRadius,
        float originClearRadius,
        float encampmentClearRadius,
        MacroWaterSet macroWater)
    {
        Vector2 horizontal = new Vector2(candidate.x, candidate.z);

        if (!InsideTerrainWithMargin(terrain, candidate, 3f) ||
            InsideOriginReserve(candidate, originClearRadius) ||
            IsNearAnyHorizontalPoint(
                settlementCenters,
                horizontal,
                settlementClearRadius) ||
            IsNearAnyHorizontalPoint(
                encampmentCenters,
                horizontal,
                encampmentClearRadius) ||
            IsInsideMacroWaterFootprint(
                terrain,
                plan,
                candidate,
                2f,
                macroWater))
        {
            return false;
        }

        // note: Terrain details use cached authored-site anchors and cached lake descriptors, avoiding terrain samples inside the 65k-cell density pass.
        return true;
    }

    private static bool IsNearAnyHorizontalPoint(
        List<Vector2> points,
        Vector2 candidate,
        float radius)
    {
        float radiusSquared =
            radius * radius;

        for (int index = 0;
             points != null && index < points.Count;
             index++)
        {
            if ((points[index] - candidate).sqrMagnitude <=
                radiusSquared)
            {
                return true;
            }
        }

        return false;
    }

    // ============================================================
    // MACRO WATER BODIES
    // ============================================================

    private static IEnumerator BuildMacroWaterBodiesRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        Action<int> completed)
    {
        if (parent == null || terrain == null ||
            terrain.terrainData == null || plan == null ||
            registry == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        Transform previous =
            parent.Find("Generated_WaterBodies");

        ReleaseGeneratedMacroWaterMeshes();

        if (previous != null)
        {
            previous.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(previous.gameObject);
        }

        string waterAssetPath = string.Empty;
        GameObject waterPrefab = null;

        // note: The approved water shard warms asynchronously before ResolvePrefab so first-lake materialization cannot hide a synchronous Resources load behind the Goddess animation.
        yield return registry.PreloadAssetPathsRoutine(
            ApprovedMacroWaterPrefabs);

        for (int index = 0;
             index < ApprovedMacroWaterPrefabs.Length;
             index++)
        {
            string candidatePath =
                ApprovedMacroWaterPrefabs[index];
            GameObject candidatePrefab =
                registry.ResolvePrefab(candidatePath);

            if (candidatePrefab == null)
                continue;

            waterAssetPath = candidatePath;
            waterPrefab = candidatePrefab;
            break;
        }

        if (waterPrefab == null)
        {
            Debug.LogWarning(
                "[YQGeneratedWorldEnvironment] No approved runtime water prefab resolved; sculpted basins remain dry rather than receiving a generated placeholder material.");
            completed?.Invoke(0);
            yield break;
        }

        GameObject root =
            new GameObject("Generated_WaterBodies");
        root.transform.SetParent(parent, false);
        int spawned = 0;

        for (int basinIndex = 0;
             basinIndex < YQGeneratedWorldTerrain.MacroWaterBasinCount;
             basinIndex++)
        {
            if (!YQGeneratedWorldTerrain.TryGetMacroWaterBasin(
                    plan.worldSeed,
                    terrain,
                    basinIndex,
                    out YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin))
            {
                continue;
            }

            AsyncInstantiateOperation<GameObject> operation =
                UnityEngine.Object.InstantiateAsync(
                    waterPrefab,
                    root.transform);
            operation.priority = -1;
            // note: Each lake integrates on its own frame even though only two exist, preserving the loading-screen presentation budget around imported material repair.
            yield return operation;

            GameObject instance =
                operation.Result != null && operation.Result.Length > 0
                    ? operation.Result[0]
                    : null;

            if (instance == null)
                continue;

            instance.name =
                "MacroWaterBasin_" +
                basinIndex +
                "__" +
                waterPrefab.name;
            instance.transform.position =
                basin.CenterWorld;
            instance.transform.rotation =
                Quaternion.identity;
            instance.transform.localScale =
                Vector3.one;

            registry.ApplyMaterialOverrides(
                waterAssetPath,
                instance);

            yield return YQRuntimeUrpMaterialRepair
                .RepairMaterialHierarchyRoutine(
                    instance,
                    null);

            RemoveWildernessCollision(instance);

            if (!TryConfigureMacroWaterSurface(
                    instance,
                    basin,
                    basinIndex))
            {
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                continue;
            }

            spawned++;
            yield return null;
        }

        if (spawned == 0)
        {
            root.SetActive(false);
            UnityEngine.Object.Destroy(root);
        }

        completed?.Invoke(spawned);
    }

    private static bool TryConfigureMacroWaterSurface(
        GameObject instance,
        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin,
        int basinIndex)
    {
        if (instance == null)
            return false;

        Renderer[] importedRenderers =
            instance.GetComponentsInChildren<Renderer>(true);
        Renderer materialSource = null;

        for (int index = 0;
             index < importedRenderers.Length;
             index++)
        {
            Renderer renderer = importedRenderers[index];

            if (renderer == null)
                continue;

            if (materialSource == null &&
                renderer.sharedMaterials != null &&
                renderer.sharedMaterials.Length > 0 &&
                renderer.sharedMaterials[0] != null)
            {
                materialSource = renderer;
            }

            renderer.enabled = false;
        }

        if (materialSource == null)
            return false;

        GameObject surface =
            new GameObject("CuratedEllipticalWaterSurface");
        surface.transform.SetParent(instance.transform, false);
        surface.transform.localPosition = Vector3.zero;
        surface.transform.localRotation = Quaternion.identity;
        surface.transform.localScale = Vector3.one;

        MeshFilter filter =
            surface.AddComponent<MeshFilter>();
        MeshRenderer rendererComponent =
            surface.AddComponent<MeshRenderer>();
        filter.sharedMesh =
            BuildMacroWaterEllipseMesh(
                basin,
                basinIndex);
        rendererComponent.sharedMaterials =
            materialSource.sharedMaterials;
        rendererComponent.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        rendererComponent.receiveShadows = false;
        rendererComponent.lightProbeUsage =
            UnityEngine.Rendering.LightProbeUsage.Off;
        rendererComponent.reflectionProbeUsage =
            UnityEngine.Rendering.ReflectionProbeUsage.Off;
        rendererComponent.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;

        // note: The approved imported water material is retained, while a lightweight ellipse conforms it to the deterministic basin instead of stretching a square plane across dry ground.
        return filter.sharedMesh != null;
    }

    private static Mesh BuildMacroWaterEllipseMesh(
        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin,
        int basinIndex)
    {
        const int segmentCount = 64;
        Vector3[] vertices =
            new Vector3[segmentCount + 1];
        Vector3[] normals =
            new Vector3[segmentCount + 1];
        Vector2[] uvs =
            new Vector2[segmentCount + 1];
        int[] triangles =
            new int[segmentCount * 3];
        float longRadius =
            basin.LongRadius * 0.94f;
        float shortRadius =
            basin.ShortRadius * 0.94f;

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int segment = 0;
             segment < segmentCount;
             segment++)
        {
            float angle =
                segment /
                (float)segmentCount *
                Mathf.PI *
                2f;
            float along =
                Mathf.Cos(angle) *
                longRadius;
            float across =
                Mathf.Sin(angle) *
                shortRadius;
            Vector2 offset =
                basin.LongAxisXZ * along +
                basin.ShortAxisXZ * across;
            int vertexIndex = segment + 1;

            vertices[vertexIndex] =
                new Vector3(offset.x, 0f, offset.y);
            normals[vertexIndex] = Vector3.up;
            uvs[vertexIndex] =
                new Vector2(
                    0.5f + Mathf.Cos(angle) * 0.5f,
                    0.5f + Mathf.Sin(angle) * 0.5f);

            int triangleIndex = segment * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] =
                (segment + 1) % segmentCount + 1;
            triangles[triangleIndex + 2] =
                vertexIndex;
        }

        Mesh mesh =
            new Mesh
            {
                name =
                    "YQ_MacroWaterEllipse_" +
                    basinIndex
            };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        GeneratedMacroWaterMeshes.Add(mesh);

        return mesh;
    }

    private static void ReleaseGeneratedMacroWaterMeshes()
    {
        for (int index = 0;
             index < GeneratedMacroWaterMeshes.Count;
             index++)
        {
            Mesh mesh = GeneratedMacroWaterMeshes[index];

            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
        }

        // note: Runtime-authored lake meshes have explicit ownership, preventing repeated world rebuilds from retaining orphaned native mesh memory.
        GeneratedMacroWaterMeshes.Clear();
    }

    // ============================================================
    // TERRAIN-NATIVE VEGETATION
    // ============================================================

    private static IEnumerator BuildTerrainNativeVegetationRoutine(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        Action<int, int> completed)
    {
        if (terrain == null || terrain.terrainData == null ||
            plan == null || registry == null ||
            plan.regions == null || plan.regions.Count == 0)
        {
            completed?.Invoke(0, 0);
            yield break;
        }

        TerrainData data = terrain.terrainData;
        List<TreePrototype> treePrototypes = new List<TreePrototype>();
        List<DetailPrototype> detailPrototypes = new List<DetailPrototype>();
        Dictionary<string, int> treePrototypeByPath =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<int, int> detailPrototypeByTexture =
            new Dictionary<int, int>();
        List<TerrainVegetationProfile> profiles =
            new List<TerrainVegetationProfile>();
        List<LivedPathSegment> livedPaths =
            BuildLivedPathNetwork(
                plan,
                terrain);
        MacroWaterSet macroWater =
            BuildMacroWaterSet(
                terrain,
                plan);
        List<Vector2> settlementDetailReserves =
            new List<Vector2>();
        List<Vector2> encampmentDetailReserves =
            new List<Vector2>();

        if (plan.settlements != null)
        {
            for (int index = 0; index < plan.settlements.Count; index++)
            {
                GeneratedSettlementRecord settlement = plan.settlements[index];

                if (settlement == null)
                    continue;

                Vector3 anchor = YQGeneratedWorldLayout.GetSettlementAnchor(
                    plan,
                    settlement,
                    terrain);
                settlementDetailReserves.Add(new Vector2(anchor.x, anchor.z));
            }
        }

        if (plan.encampments != null)
        {
            for (int index = 0; index < plan.encampments.Count; index++)
            {
                GeneratedEncampmentRecord encampment = plan.encampments[index];

                if (encampment == null)
                    continue;

                Vector3 anchor = YQGeneratedWorldLayout.GetEncampmentAnchor(
                    plan,
                    encampment,
                    terrain);
                encampmentDetailReserves.Add(new Vector2(anchor.x, anchor.z));
            }
        }

        // note: Construction anchors are cached once; the dense detail-cell loop performs only managed horizontal distance checks.
        float frameStartedAt = Time.realtimeSinceStartup;

        // note: Optional URP conifer shards warm cooperatively before prototype discovery; missing legacy shards fall back to the material-repaired visible-tree pass without a synchronous load.
        yield return registry.PreloadAssetPathsRoutine(
            ApprovedVisibleTreePrefabs);
        yield return registry.PreloadAssetPathsRoutine(
            ApprovedDryTreePrefabs);

        for (int regionIndex = 0;
             regionIndex < plan.regions.Count;
             regionIndex++)
        {
            GeneratedRegionRecord region = plan.regions[regionIndex];
            GeneratedRegionAssetPaletteRecord palette =
                region != null ? FindPalette(plan, region) : null;

            if (region == null || palette == null)
                continue;

            palette.EnsureCollections();
            TerrainVegetationProfile profile =
                new TerrainVegetationProfile
                {
                    region = region,
                    palette = palette,
                    center = YQGeneratedWorldLayout.GetRegionCenter(
                        plan,
                        region,
                        terrain),
                    treeMaskThreshold = ResolveTreeMaskThreshold(palette),
                    detailMaskThreshold = ResolveDetailMaskThreshold(palette)
                };

            for (int referenceIndex = 0;
                 referenceIndex < palette.vegetation.Count;
                 referenceIndex++)
            {
                GeneratedAssetReferenceRecord reference =
                    palette.vegetation[referenceIndex];

                if (reference == null ||
                    string.IsNullOrWhiteSpace(reference.assetPath))
                {
                    continue;
                }

                GameObject prefab = registry.ResolvePrefab(reference.assetPath);
                if (prefab == null)
                    continue;

                if (LooksLikeTree(null, reference))
                {
                    if (profile.treePrototypeIndices.Count >= 5 ||
                        IsOversizedSmallScatterPrefab(
                            prefab,
                            YQWorldAssetCatalog.SlotVegetation,
                            reference) ||
                        !IsTerrainTreePrototypeCompatible(
                            prefab,
                            reference.assetPath))
                    {
                        continue;
                    }

                    RegisterTerrainTreePrototype(
                        profile,
                        reference.assetPath,
                        prefab,
                        treePrototypes,
                        treePrototypeByPath);
                }
                else if (profile.detailPrototypeIndices.Count < 2 &&
                         LooksLikeTerrainDetailReference(reference) &&
                         TryResolveTerrainDetailTexture(
                             prefab,
                             out Texture2D detailTexture))
                {
                    int textureKey = detailTexture.GetInstanceID();

                    if (!detailPrototypeByTexture.TryGetValue(
                            textureKey,
                            out int detailIndex))
                    {
                        if (detailPrototypes.Count >= MaximumTerrainDetailPrototypes)
                            continue;

                        detailIndex = detailPrototypes.Count;
                        detailPrototypeByTexture.Add(textureKey, detailIndex);
                        detailPrototypes.Add(
                            CreateTerrainDetailPrototype(detailTexture));
                    }

                    AddUniqueIndex(profile.detailPrototypeIndices, detailIndex);
                }

                if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
                {
                    // note: Prototype discovery may touch lazy registry shards, so it yields before another imported vegetation family is inspected.
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
            }

            if (ShouldSupplementWithApprovedConifers(
                    profile.palette))
            {
                for (int coniferIndex = 0;
                     coniferIndex < ApprovedUrpConiferPrefabs.Length &&
                     profile.treePrototypeIndices.Count < 5;
                     coniferIndex++)
                {
                    string coniferPath =
                        ApprovedUrpConiferPrefabs[coniferIndex];
                    GameObject coniferPrefab =
                        registry.ResolvePrefab(
                            coniferPath);

                    if (!IsTerrainTreePrototypeCompatible(
                            coniferPrefab,
                            coniferPath))
                    {
                        continue;
                    }

                    // note: Persisted palettes predating this repair still receive the full approved URP conifer family at materialization time.
                    RegisterTerrainTreePrototype(
                        profile,
                        coniferPath,
                        coniferPrefab,
                        treePrototypes,
                        treePrototypeByPath);
                }
            }

            profiles.Add(profile);
        }

        int treeCount = 0;

        if (treePrototypes.Count > 0)
        {
            data.treePrototypes = treePrototypes.ToArray();
            yield return null;

            List<TreeInstance> instances =
                new List<TreeInstance>(
                    Mathf.Min(
                        MaximumTerrainTreeInstances,
                        profiles.Count * 320));
            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 terrainSize = data.size;
            uint seedHash = StableHash32(
                SafeText(plan.worldSeed, "yourquest_default_world"));
            float noiseOffsetX = (seedHash & 0xFFFFu) * 0.0137f;
            float noiseOffsetZ = ((seedHash >> 16) & 0xFFFFu) * 0.0173f;

            for (int profileIndex = 0;
                 profileIndex < profiles.Count &&
                 instances.Count < MaximumTerrainTreeInstances;
                 profileIndex++)
            {
                TerrainVegetationProfile profile = profiles[profileIndex];
                if (profile.treePrototypeIndices.Count == 0)
                    continue;

                int target = Mathf.Clamp(
                    Mathf.RoundToInt(
                        ResolveVegetationTarget(
                            profile.region,
                            profile.palette) * 2.3f),
                    120,
                    300);
                int placedForRegion = 0;
                int attempts = target * 6;

                for (int attempt = 0;
                     attempt < attempts && placedForRegion < target &&
                     instances.Count < MaximumTerrainTreeInstances;
                     attempt++)
                {
                    if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
                    {
                        // note: Rejected candidates consume budget too; yielding before evaluation prevents sparse biomes from spinning through thousands of failed samples in one loading frame.
                        yield return null;
                        frameStartedAt = Time.realtimeSinceStartup;
                    }

                    string seed = plan.worldSeed +
                        "|terrain_tree|" + profile.region.regionId +
                        "|" + attempt;

                    if (!TryResolveWildernessPosition(
                            terrain,
                            plan,
                            profile.center,
                            seed,
                            18f,
                            310f,
                            28f,
                            40f,
                            24f,
                            out Vector3 position,
                            macroWater))
                    {
                        continue;
                    }

                    if (IsNearLivedPath(
                            livedPaths,
                            position,
                            5.5f))
                    {
                        continue;
                    }

                    position.y = YQGeneratedWorldTerrain.SampleWorldHeight(
                        terrain,
                        position);
                    float normalizedX =
                        (position.x - terrainOrigin.x) /
                        Mathf.Max(0.001f, terrainSize.x);
                    float normalizedZ =
                        (position.z - terrainOrigin.z) /
                        Mathf.Max(0.001f, terrainSize.z);
                    Vector3 normal = data.GetInterpolatedNormal(
                        normalizedX,
                        normalizedZ);

                    if (Vector3.Angle(Vector3.up, normal) > 34f)
                        continue;

                    float groveNoise = Mathf.PerlinNoise(
                        noiseOffsetX + position.x * 0.0105f,
                        noiseOffsetZ + position.z * 0.0105f);
                    float moistureNoise = Mathf.PerlinNoise(
                        noiseOffsetZ + position.x * 0.0038f,
                        noiseOffsetX + position.z * 0.0038f);

                    if (groveNoise * 0.68f + moistureNoise * 0.32f <
                        profile.treeMaskThreshold)
                    {
                        continue;
                    }

                    int localPrototype = Mathf.Clamp(
                        Mathf.FloorToInt(
                            Deterministic01(seed + "|prototype") *
                            profile.treePrototypeIndices.Count),
                        0,
                        profile.treePrototypeIndices.Count - 1);
                    float baseScale = Mathf.Lerp(
                        0.82f,
                        1.22f,
                        Deterministic01(seed + "|height"));

                    // note: TreeInstance coordinates are terrain-normalized and snap to the final heightmap, eliminating prefab pivots and per-tree grounding work.
                    instances.Add(
                        new TreeInstance
                        {
                            position = new Vector3(
                                normalizedX,
                                Mathf.Clamp01(
                                    (position.y - terrainOrigin.y) /
                                    Mathf.Max(0.001f, terrainSize.y)),
                                normalizedZ),
                            prototypeIndex =
                                profile.treePrototypeIndices[localPrototype],
                            widthScale = baseScale * Mathf.Lerp(
                                0.88f,
                                1.08f,
                                Deterministic01(seed + "|width")),
                            heightScale = baseScale,
                            rotation = Deterministic01(seed + "|yaw") *
                                Mathf.PI * 2f,
                            color = Color.white,
                            lightmapColor = Color.white
                        });
                    placedForRegion++;

                    if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
                    {
                        // note: Ecological rejection sampling is frame-budgeted even though Terrain will batch the accepted tree instances at publication.
                        yield return null;
                        frameStartedAt = Time.realtimeSinceStartup;
                    }
                }
            }

            // note: One native Terrain publication replaces hundreds of managed object hierarchies and lets Unity own tree culling, billboards, and batching.
            // note: Instance Y values were already sampled from the final heightmap; disabling Unity's second global snap pass removes redundant native work during publication.
            data.SetTreeInstances(instances.ToArray(), false);
            treeCount = instances.Count;
            terrain.treeDistance = 520f;
            terrain.treeBillboardDistance = 135f;
            terrain.treeCrossFadeLength = 20f;
            terrain.treeMaximumFullLODCount = 72;
            yield return null;
        }
        else
        {
            data.treePrototypes = Array.Empty<TreePrototype>();
            data.treeInstances = Array.Empty<TreeInstance>();
        }

        int detailCount = 0;

        if (detailPrototypes.Count > 0)
        {
            data.SetDetailResolution(
                TerrainDetailResolution,
                TerrainDetailPatchResolution);
            data.detailPrototypes = detailPrototypes.ToArray();
            List<int[,]> detailMaps = new List<int[,]>(
                detailPrototypes.Count);

            for (int detailIndex = 0;
                 detailIndex < detailPrototypes.Count;
                 detailIndex++)
            {
                detailMaps.Add(
                    new int[TerrainDetailResolution, TerrainDetailResolution]);
            }

            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 terrainSize = data.size;
            uint detailSeedHash = StableHash32(
                plan.worldSeed + "|terrain_details");
            float detailOffsetX = (detailSeedHash & 0xFFFFu) * 0.0091f;
            float detailOffsetZ =
                ((detailSeedHash >> 16) & 0xFFFFu) * 0.0117f;
            int heightmapResolution = data.heightmapResolution;
            float[,] terrainHeights = new float[
                heightmapResolution,
                heightmapResolution];
            const int heightRowsPerRead = 16;

            for (int startRow = 0;
                 startRow < heightmapResolution;
                 startRow += heightRowsPerRead)
            {
                int rowCount = Mathf.Min(
                    heightRowsPerRead,
                    heightmapResolution - startRow);
                float[,] strip = data.GetHeights(
                    0,
                    startRow,
                    heightmapResolution,
                    rowCount);

                for (int row = 0; row < rowCount; row++)
                {
                    for (int column = 0;
                         column < heightmapResolution;
                         column++)
                    {
                        terrainHeights[startRow + row, column] =
                            strip[row, column];
                    }
                }

                // note: Cached slope input is read in small strips so ground-cover synthesis avoids both a monolithic height read and thousands of native normal queries.
                yield return null;
            }

            for (int z = 0; z < TerrainDetailResolution; z++)
            {
                float normalizedZ =
                    (z + 0.5f) / TerrainDetailResolution;
                float worldZ = terrainOrigin.z + normalizedZ * terrainSize.z;

                for (int x = 0; x < TerrainDetailResolution; x++)
                {
                    float normalizedX =
                        (x + 0.5f) / TerrainDetailResolution;
                    Vector3 worldPosition = new Vector3(
                        terrainOrigin.x + normalizedX * terrainSize.x,
                        0f,
                        worldZ);
                    TerrainVegetationProfile profile =
                        FindNearestVegetationProfile(profiles, worldPosition);

                    if (profile == null ||
                        profile.detailPrototypeIndices.Count == 0 ||
                        IsNearLivedPath(
                            livedPaths,
                            worldPosition,
                            1.8f) ||
                        !IsTerrainDetailPositionAllowed(
                            terrain,
                            plan,
                            worldPosition,
                            settlementDetailReserves,
                            encampmentDetailReserves,
                            20f,
                            28f,
                            16f,
                            macroWater))
                    {
                        continue;
                    }

                    float slopeDegrees = SampleHeightmapSlopeDegrees(
                        terrainHeights,
                        terrainSize,
                        normalizedX,
                        normalizedZ);
                    if (slopeDegrees > 38f)
                        continue;

                    float patchNoise = Mathf.PerlinNoise(
                        detailOffsetX + worldPosition.x * 0.026f,
                        detailOffsetZ + worldPosition.z * 0.026f);
                    float biomeNoise = Mathf.PerlinNoise(
                        detailOffsetZ + worldPosition.x * 0.006f,
                        detailOffsetX + worldPosition.z * 0.006f);
                    float densityMask =
                        patchNoise * 0.57f + biomeNoise * 0.43f;
                    float threshold = profile.detailMaskThreshold;

                    if (densityMask < threshold)
                        continue;

                    float cellChoice = ResolveGridCell01(
                        detailSeedHash,
                        x,
                        z);
                    int profileLayer = Mathf.Clamp(
                        Mathf.FloorToInt(
                            cellChoice *
                            profile.detailPrototypeIndices.Count),
                        0,
                        profile.detailPrototypeIndices.Count - 1);
                    int layerIndex =
                        profile.detailPrototypeIndices[profileLayer];
                    int density = Mathf.Clamp(
                        2 + Mathf.FloorToInt(
                            (densityMask - threshold) * 12f),
                        2,
                        6);

                    detailMaps[layerIndex][z, x] = density;
                    detailCount += density;
                }

                if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
                {
                    // note: Detail masks are synthesized incrementally so dense ground cover never blocks the Goddess presentation loop.
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
            }

            for (int detailIndex = 0;
                 detailIndex < detailMaps.Count;
                 detailIndex++)
            {
                const int detailRowsPerUpload = 16;

                for (int startRow = 0;
                     startRow < TerrainDetailResolution;
                     startRow += detailRowsPerUpload)
                {
                    int rowCount = Mathf.Min(
                        detailRowsPerUpload,
                        TerrainDetailResolution - startRow);
                    int[,] strip = new int[
                        rowCount,
                        TerrainDetailResolution];

                    for (int row = 0; row < rowCount; row++)
                    {
                        for (int column = 0;
                             column < TerrainDetailResolution;
                             column++)
                        {
                            strip[row, column] =
                                detailMaps[detailIndex][startRow + row, column];
                        }
                    }

                    // note: Detail density reaches Terrain in small row strips so no layer can force a full-map native upload onto one loading frame.
                    data.SetDetailLayer(
                        0,
                        startRow,
                        detailIndex,
                        strip);
                    yield return null;
                }
            }

            terrain.detailObjectDistance = 180f;
            terrain.detailObjectDensity = 1f;
        }
        else
        {
            data.detailPrototypes = Array.Empty<DetailPrototype>();
        }

        // note: Terrain setters already publish their own data; an explicit Flush here would synchronously force every vegetation and detail update onto one loading frame.
        yield return null;
        completed?.Invoke(treeCount, detailCount);
    }

    private static DetailPrototype CreateTerrainDetailPrototype(
        Texture2D texture)
    {
        // note: Existing vegetation art supplies the billboard texture; runtime code only describes a batched Terrain detail contract.
        return new DetailPrototype
        {
            prototypeTexture = texture,
            minWidth = 0.42f,
            maxWidth = 1.05f,
            minHeight = 0.35f,
            maxHeight = 0.95f,
            noiseSpread = 0.19f,
            healthyColor = new Color(0.72f, 0.82f, 0.64f, 1f),
            dryColor = new Color(0.54f, 0.48f, 0.35f, 1f),
            renderMode = DetailRenderMode.GrassBillboard,
            usePrototypeMesh = false
        };
    }

    private static bool IsTerrainTreePrototypeCompatible(
        GameObject prefab,
        string assetPath)
    {
        if (prefab == null ||
            string.IsNullOrWhiteSpace(assetPath) ||
            prefab.GetComponent<LODGroup>() == null ||
            assetPath.IndexOf(
                "/URP/",
                StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        Renderer[] renderers =
            prefab.GetComponentsInChildren<Renderer>(true);
        bool foundRenderable =
            false;

        for (int rendererIndex = 0;
             rendererIndex < renderers.Length;
             rendererIndex++)
        {
            Renderer renderer =
                renderers[rendererIndex];

            if (renderer == null ||
                renderer is ParticleSystemRenderer)
            {
                continue;
            }

            Material[] materials =
                renderer.sharedMaterials;

            for (int materialIndex = 0;
                 materialIndex < materials.Length;
                 materialIndex++)
            {
                Material material =
                    materials[materialIndex];

                if (material == null ||
                    material.shader == null ||
                    !material.shader.isSupported)
                {
                    return false;
                }

                foundRenderable =
                    true;
            }
        }

        // note: Terrain trees accept only an explicit URP LOD contract; legacy Tree Editor and arbitrary marketplace foliage can no longer enter the magenta billboard path.
        return foundRenderable;
    }

    private static void RegisterTerrainTreePrototype(
        TerrainVegetationProfile profile,
        string assetPath,
        GameObject prefab,
        List<TreePrototype> prototypes,
        Dictionary<string, int> prototypeByPath)
    {
        if (profile == null || prefab == null ||
            prototypes == null || prototypeByPath == null ||
            string.IsNullOrWhiteSpace(assetPath))
        {
            return;
        }

        if (!prototypeByPath.TryGetValue(
                assetPath,
                out int prototypeIndex))
        {
            if (prototypes.Count >= MaximumTerrainTreePrototypes)
                return;

            prototypeIndex = prototypes.Count;
            prototypeByPath.Add(assetPath, prototypeIndex);
            prototypes.Add(
                new TreePrototype
                {
                    prefab = prefab,
                    bendFactor = 0.18f,
                    navMeshLod = 1
                });
        }

        AddUniqueIndex(
            profile.treePrototypeIndices,
            prototypeIndex);
    }

    private static bool ShouldSupplementWithApprovedConifers(
        GeneratedRegionAssetPaletteRecord palette)
    {
        string style = palette != null
            ? SafeText(palette.styleKey, string.Empty).ToLowerInvariant()
            : string.Empty;

        return !ContainsAnySemantic(
            style,
            "desert",
            "western",
            "persepolis",
            "scifi",
            "cyberpunk",
            "container",
            "bio_horror",
            "hospital",
            "sewer",
            "pirate");
    }

    private static bool TryResolveTerrainDetailTexture(
        GameObject prefab,
        out Texture2D texture)
    {
        texture = null;
        if (prefab == null)
            return false;

        Renderer[] renderers =
            prefab.GetComponentsInChildren<Renderer>(true);

        for (int rendererIndex = 0;
             rendererIndex < renderers.Length;
             rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            Material[] materials = renderer.sharedMaterials;

            for (int materialIndex = 0;
                 materialIndex < materials.Length;
                 materialIndex++)
            {
                Material material = materials[materialIndex];
                // note: Shader Graph foliage commonly names its base texture `_BaseMap`; property-aware lookup avoids the noisy and invalid Material.mainTexture fallback.
                Texture2D candidate = FindTexture(
                    material,
                    "_BaseMap",
                    "_BaseColorMap",
                    "_MainTex",
                    "_Albedo",
                    "_Diffuse");

                if (candidate == null ||
                    candidate.width < 8 || candidate.height < 8)
                {
                    continue;
                }

                texture = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeTerrainDetailReference(
        GeneratedAssetReferenceRecord reference)
    {
        // note: Ground-cover eligibility follows the asset's identity rather than broad palette style tags, preventing props such as dinnerware from becoming Terrain detail textures.
        string semantic = BuildReferenceIdentitySemanticText(reference);

        return ContainsAnySemantic(
            semantic,
            "grass",
            "fern",
            "weed",
            "flower",
            "shrub",
            "bush");
    }

    private static void AddUniqueIndex(
        List<int> indices,
        int value)
    {
        if (indices != null && !indices.Contains(value))
        {
            // note: A region may reference the same shared tree or detail asset more than once; Terrain receives one stable local choice.
            indices.Add(value);
        }
    }

    private static TerrainVegetationProfile FindNearestVegetationProfile(
        List<TerrainVegetationProfile> profiles,
        Vector3 position)
    {
        TerrainVegetationProfile nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;

        for (int index = 0;
             profiles != null && index < profiles.Count;
             index++)
        {
            TerrainVegetationProfile candidate = profiles[index];
            if (candidate == null)
                continue;

            float distanceSquared =
                (new Vector2(candidate.center.x, candidate.center.z) -
                 new Vector2(position.x, position.z)).sqrMagnitude;

            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearest = candidate;
            nearestDistanceSquared = distanceSquared;
        }

        return nearest;
    }

    private static float ResolveTreeMaskThreshold(
        GeneratedRegionAssetPaletteRecord palette)
    {
        string style = palette != null
            ? SafeText(palette.styleKey, string.Empty).ToLowerInvariant()
            : string.Empty;

        if (ContainsAnySemantic(
                style,
                "desert",
                "persepolis",
                "western"))
        {
            return 0.60f;
        }

        if (ContainsAnySemantic(
                style,
                "nordic",
                "viking",
                "forest",
                "hallowed"))
        {
            return 0.39f;
        }

        return 0.46f;
    }

    private static float ResolveDetailMaskThreshold(
        GeneratedRegionAssetPaletteRecord palette)
    {
        string style = palette != null
            ? SafeText(palette.styleKey, string.Empty).ToLowerInvariant()
            : string.Empty;

        if (ContainsAnySemantic(
                style,
                "desert",
                "persepolis",
                "western"))
        {
            return 0.56f;
        }

        if (ContainsAnySemantic(
                style,
                "nordic",
                "viking",
                "forest",
                "hallowed"))
        {
            return 0.34f;
        }

        return 0.42f;
    }

    // ============================================================
    // WILDERNESS
    // ============================================================

    private static IEnumerator BuildOriginApproachDressingRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        Action<int, int, int> completed)
    {
        if (parent == null || terrain == null || plan == null ||
            registry == null || plan.regions == null ||
            plan.regions.Count == 0)
        {
            completed?.Invoke(0, 0, 0);
            yield break;
        }

        GeneratedRegionRecord nearestRegion = null;
        GeneratedRegionAssetPaletteRecord nearestPalette = null;
        Vector3 originAnchor = YQGeneratedWorldLayout.GetVeyOriginAnchor();
        float nearestDistanceSquared = float.PositiveInfinity;

        for (int index = 0; index < plan.regions.Count; index++)
        {
            GeneratedRegionRecord region = plan.regions[index];
            GeneratedRegionAssetPaletteRecord palette =
                region != null ? FindPalette(plan, region) : null;

            if (region == null || palette == null)
                continue;

            Vector3 regionCenter =
                YQGeneratedWorldLayout.GetRegionCenter(plan, region, terrain);
            float distanceSquared =
                (new Vector2(regionCenter.x, regionCenter.z) -
                 new Vector2(originAnchor.x, originAnchor.z)).sqrMagnitude;

            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearestDistanceSquared = distanceSquared;
            nearestRegion = region;
            nearestPalette = palette;
        }

        if (nearestRegion == null || nearestPalette == null)
        {
            completed?.Invoke(0, 0, 0);
            yield break;
        }

        Transform previous = parent.Find("Generated_OriginApproachDressing");
        if (previous != null)
        {
            previous.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(previous.gameObject);
        }

        GameObject root = new GameObject("Generated_OriginApproachDressing");
        root.transform.SetParent(parent, false);

        int treesSpawned = 0;
        int vegetationSpawned = 0;
        int rocksSpawned = 0;

        // note: A small material-repaired tree grove guarantees readable near-field silhouettes even when Unity rejects an imported prefab from Terrain's legacy tree renderer.
        yield return SpawnSmallScatterAreaRoutine(
            root.transform,
            terrain,
            plan,
            nearestRegion,
            nearestPalette,
            registry,
            originAnchor,
            YQWorldAssetCatalog.SlotVegetation,
            18,
            44f,
            126f,
            16f,
            36f,
            18f,
            count => treesSpawned = count,
            true);

        // note: Four-to-six-member palette clusters frame the authored approach outside its traversal reserve; they are deterministic set dressing, not global uniform noise.
        yield return SpawnSmallScatterAreaRoutine(
            root.transform,
            terrain,
            plan,
            nearestRegion,
            nearestPalette,
            registry,
            originAnchor,
            YQWorldAssetCatalog.SlotVegetation,
            72,
            32f,
            112f,
            14f,
            28f,
            16f,
            count => vegetationSpawned = count);

        yield return SpawnSmallScatterAreaRoutine(
            root.transform,
            terrain,
            plan,
            nearestRegion,
            nearestPalette,
            registry,
            originAnchor,
            YQWorldAssetCatalog.SlotRock,
            24,
            34f,
            118f,
            14f,
            30f,
            16f,
            count => rocksSpawned = count);

        Debug.Log(
            "[YQGeneratedWorldEnvironment] ORIGIN APPROACH DRESSED\n" +
            "Palette region: " + nearestRegion.displayName + "\n" +
            "Visible trees: " + treesSpawned + "/22\n" +
            "Vegetation: " + vegetationSpawned + "/72\n" +
            "Rock outcrops: " + rocksSpawned + "/24");

        completed?.Invoke(treesSpawned, vegetationSpawned, rocksSpawned);
    }

    private static IEnumerator
        BuildRegionalWildernessRoutine(
            Transform parent,
            Terrain terrain,
            GeneratedWorldPlanRecord plan,
            YQRuntimeWorldAssetRegistry registry,
            Action<WildernessBuildStats> completed)
    {
        WildernessBuildStats stats =
            new WildernessBuildStats();

        if (parent == null ||
            terrain == null ||
            plan == null ||
            registry == null)
        {
            completed?.Invoke(stats);
            yield break;
        }

        Transform previous =
            parent.Find(
                "Generated_Wilderness");

        if (previous != null)
        {
            previous.gameObject
                .SetActive(
                    false);

            UnityEngine.Object.Destroy(
                previous.gameObject);
        }

        GameObject wildernessRoot =
            new GameObject(
                "Generated_Wilderness");

        wildernessRoot.transform.SetParent(
            parent,
            false);

        for (int regionIndex = 0;
             regionIndex < plan.regions.Count;
             regionIndex++)
        {
            GeneratedRegionRecord region =
                plan.regions[
                    regionIndex];

            if (region == null)
                continue;

            GeneratedRegionAssetPaletteRecord palette =
                FindPalette(
                    plan,
                    region);

            if (palette == null)
                continue;

            palette.EnsureCollections();

            GameObject regionRoot =
                new GameObject(
                    "Wilderness__" +
                    SafeName(
                        region.displayName));

            regionRoot.transform.SetParent(
                wildernessRoot.transform,
                false);

            Vector3 regionCenter =
                YQGeneratedWorldLayout
                    .GetRegionCenter(
                        plan,
                        region,
                        terrain);

            int vegetationTarget =
                ResolveVegetationTarget(
                    region,
                    palette);

            int visibleTreeTarget =
                ResolveVisibleTreeTarget(
                    vegetationTarget,
                    palette);

            int rockTarget =
                ResolveRockTarget(
                    region,
                    palette);

            int visibleTreesSpawned = 0;
            yield return SpawnSmallScatterRoutine(
                    regionRoot.transform,
                    terrain,
                    plan,
                    region,
                    palette,
                    registry,
                    regionCenter,
                    YQWorldAssetCatalog
                        .SlotVegetation,
                    visibleTreeTarget,
                    count => visibleTreesSpawned = count,
                    true);

            yield return null;

            int vegetationSpawned = 0;
            yield return SpawnSmallScatterRoutine(
                    regionRoot.transform,
                    terrain,
                    plan,
                    region,
                    palette,
                    registry,
                    regionCenter,
                    YQWorldAssetCatalog
                        .SlotVegetation,
                    vegetationTarget,
                    count => vegetationSpawned = count);

            yield return null;

            int rocksSpawned = 0;
            yield return SpawnSmallScatterRoutine(
                    regionRoot.transform,
                    terrain,
                    plan,
                    region,
                    palette,
                    registry,
                    regionCenter,
                    YQWorldAssetCatalog
                        .SlotRock,
                    rockTarget,
                    count => rocksSpawned = count);

            yield return null;

            int caves = 0;
            yield return BuildRegionalCavesRoutine(
                    regionRoot.transform,
                    terrain,
                    plan,
                    region,
                    palette,
                    registry,
                    regionCenter,
                    count => caves = count);

            yield return null;

            int ambientEnemies = 0;
            yield return BuildRegionalAmbientEncountersRoutine(
                    regionRoot.transform,
                    terrain,
                    plan,
                    region,
                    registry,
                    regionCenter,
                    count => ambientEnemies = count);

            yield return null;

            int treasure = 0;
            yield return BuildRegionalTreasureRoutine(
                    regionRoot.transform,
                    terrain,
                    plan,
                    region,
                    palette,
                    registry,
                    regionCenter,
                    count => treasure = count);

            yield return null;

            stats.visibleTrees +=
                visibleTreesSpawned;

            stats.vegetation +=
                vegetationSpawned;

            stats.rocks +=
                rocksSpawned;

            stats.caves +=
                caves;

            stats.ambientEnemies +=
                ambientEnemies;

            stats.treasure +=
                treasure;

            Debug.Log(
                "[YQGeneratedWorldEnvironment] REGION WILDERNESS\n" +
                "Region: " +
                region.displayName +
                "\nVisible trees: " +
                visibleTreesSpawned +
                "/" +
                visibleTreeTarget +
                "\nVegetation: " +
                vegetationSpawned +
                "/" +
                vegetationTarget +
                "\nRocks: " +
                rocksSpawned +
                "/" +
                rockTarget +
                "\nCaves: " +
                caves +
                "\nAmbient enemies: " +
                ambientEnemies +
                "\nTreasure: " +
                treasure);
        }

        completed?.Invoke(stats);
    }

    private static int ResolveVegetationTarget(
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette)
    {
        string style =
            palette != null
                ? SafeText(
                    palette.styleKey,
                    string.Empty)
                    .ToLowerInvariant()
                : string.Empty;

        float multiplier =
            1f;

        if (style.Contains("nordic"))
            multiplier = 1.45f;
        else if (style.Contains("viking"))
            multiplier = 1.25f;
        else if (style.Contains("asian"))
            multiplier = 1.12f;
        else if (style.Contains("bio"))
            multiplier = 0.95f;
        else if (style.Contains("desert"))
            multiplier = 0.48f;
        else if (style.Contains("western"))
            multiplier = 0.42f;
        else if (style.Contains("persepolis"))
            multiplier = 0.62f;
        else if (style.Contains("container"))
            multiplier = 0.48f;
        else if (style.Contains("victorian"))
            multiplier = 0.72f;

        int danger =
            region != null
                ? Mathf.Clamp(
                    region.dangerTier,
                    0,
                    8)
                : 0;

        return
            Mathf.Clamp(
                Mathf.RoundToInt(
                    BaseVegetationPerRegion *
                    multiplier) +
                danger *
                2,
                48,
                128);
    }

    private static int ResolveRockTarget(
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette)
    {
        string style =
            palette != null
                ? SafeText(
                    palette.styleKey,
                    string.Empty)
                    .ToLowerInvariant()
                : string.Empty;

        float multiplier =
            1f;

        if (style.Contains("desert") ||
            style.Contains("western") ||
            style.Contains("persepolis"))
        {
            multiplier =
                1.55f;
        }
        else if (style.Contains("viking"))
        {
            multiplier =
                1.15f;
        }
        else if (style.Contains("container") ||
                 style.Contains("bio"))
        {
            multiplier =
                1.25f;
        }

        int danger =
            region != null
                ? Mathf.Clamp(
                    region.dangerTier,
                    0,
                    8)
                : 0;

        return
            Mathf.Clamp(
                Mathf.RoundToInt(
                    BaseRockScatterPerRegion *
                    multiplier) +
                danger,
                14,
                38);
    }

    private static IEnumerator SpawnSmallScatterRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        Vector3 regionCenter,
        string slot,
        int targetCount,
        Action<int> completed)
    {
        return SpawnSmallScatterAreaRoutine(
            parent,
            terrain,
            plan,
            region,
            palette,
            registry,
            regionCenter,
            slot,
            targetCount,
            WildernessRadiusMin,
            WildernessRadiusMax,
            SettlementClearRadius,
            OriginClearRadius,
            16f,
            completed,
            false);
    }

    private static IEnumerator SpawnSmallScatterRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        Vector3 regionCenter,
        string slot,
        int targetCount,
        Action<int> completed,
        bool treesOnly)
    {
        // note: Tree groves reuse the bounded asynchronous scatter pipeline but retain their own curated-reference filter and spacing contract.
        return SpawnSmallScatterAreaRoutine(
            parent,
            terrain,
            plan,
            region,
            palette,
            registry,
            regionCenter,
            slot,
            targetCount,
            WildernessRadiusMin,
            WildernessRadiusMax,
            SettlementClearRadius,
            OriginClearRadius,
            16f,
            completed,
            treesOnly);
    }

    private static IEnumerator SpawnSmallScatterAreaRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        Vector3 regionCenter,
        string slot,
        int targetCount,
        float minimumRadius,
        float maximumRadius,
        float settlementClearRadius,
        float originClearRadius,
        float encampmentClearRadius,
        Action<int> completed,
        bool treesOnly = false)
    {
        if (parent == null ||
            terrain == null ||
            plan == null ||
            region == null ||
            palette == null ||
            registry == null ||
            targetCount <= 0)
        {
            completed?.Invoke(0);
            yield break;
        }

        GameObject root =
            new GameObject(
                treesOnly
                    ? "Trees"
                    : string.Equals(
                    slot,
                    YQWorldAssetCatalog
                        .SlotVegetation,
                    StringComparison.OrdinalIgnoreCase)
                    ? "Vegetation"
                    : "Rocks");

        root.transform.SetParent(
            parent,
            false);

        int spawned =
            0;

        bool vegetationSlot =
            string.Equals(
                slot,
                YQWorldAssetCatalog.SlotVegetation,
                StringComparison.OrdinalIgnoreCase);
        int clusterSize =
            treesOnly
                ? 4
                : vegetationSlot
                ? 6
                : 3;
        int attempts =
            targetCount *
            (treesOnly ? 7 : 4);

        List<GeneratedAssetReferenceRecord> scatterReferences =
            BuildSmallScatterReferences(
                palette,
                slot,
                treesOnly);

        List<LivedPathSegment> livedPaths =
            vegetationSlot
                ? BuildLivedPathNetwork(plan, terrain)
                : null;
        MacroWaterSet macroWater =
            BuildMacroWaterSet(
                terrain,
                plan);

        if (scatterReferences.Count == 0)
        {
            UnityEngine.Object.Destroy(root);
            completed?.Invoke(0);
            yield break;
        }

        float frameStartedAt = Time.realtimeSinceStartup;

        for (int attempt = 0;
             attempt < attempts &&
             spawned < targetCount;
             attempt++)
        {
            if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
            {
                // note: Rejected candidates consume the same frame budget as accepted ones, preventing sparse or incompatible palettes from spinning through hundreds of checks in one loading frame.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }

            string seed =
                plan.worldSeed +
                "|scatter|" +
                region.regionId +
                "|" +
                slot +
                "|" +
                attempt;
            int clusterIndex =
                attempt /
                clusterSize;
            string clusterSeed =
                plan.worldSeed +
                "|scatter_cluster|" +
                region.regionId +
                "|" +
                slot +
                "|" +
                clusterIndex;

            if (!TryResolveWildernessPosition(
                    terrain,
                    plan,
                    regionCenter,
                    clusterSeed,
                    minimumRadius,
                    maximumRadius,
                    settlementClearRadius,
                    originClearRadius,
                    encampmentClearRadius,
                    out Vector3 clusterCenter,
                    macroWater))
            {
                continue;
            }

            // note: Region dressing grows in deterministic groves and rock outcrops instead of isolated uniform noise, preserving palette identity while filling traversal space coherently.
            Vector3 position =
                clusterCenter +
                ResolveRadialOffset(
                    seed + "|cluster_member",
                    vegetationSlot ? 1.5f : 0.8f,
                    vegetationSlot ? 11f : 6f);

            if (!IsWildernessPositionAllowed(
                    terrain,
                    plan,
                    position,
                    settlementClearRadius,
                    originClearRadius,
                    encampmentClearRadius,
                    macroWater))
            {
                continue;
            }

            if (vegetationSlot &&
                IsNearLivedPath(
                    livedPaths,
                    position,
                    treesOnly ? 7.5f : 1.4f))
            {
                // note: Dense foliage frames roads without occupying their walkable corridor; trees retain a wider sightline and canopy reserve.
                continue;
            }

            GeneratedAssetReferenceRecord reference =
                treesOnly
                    ? scatterReferences[
                        Mathf.Abs(
                            clusterIndex +
                            Mathf.FloorToInt(
                                Deterministic01(
                                    plan.worldSeed +
                                    "|tree_family_offset|" +
                                    region.regionId) *
                                scatterReferences.Count)) %
                        scatterReferences.Count]
                    : PickWeightedReference(
                        scatterReferences,
                        clusterSeed + "|palette");

            if (reference == null)
                continue;

            /*
             * Mountain/backdrop assets are never ordinary scatter.
             */
            if (IsLargeTerrainFeatureReference(
                    reference))
            {
                continue;
            }

            GameObject prefab =
                registry.ResolvePrefab(
                    reference.assetPath);

            if (prefab == null)
                continue;

            if (IsOversizedSmallScatterPrefab(
                    prefab,
                    slot,
                    reference))
            {
                continue;
            }

            AsyncInstantiateOperation<GameObject> operation =
                UnityEngine.Object.InstantiateAsync(
                    prefab,
                    root.transform);
            operation.priority = -1;
            // note: Even one unexpectedly dense imported scatter prefab must integrate asynchronously instead of consuming an entire Goddess frame.
            yield return operation;
            GameObject instance =
                operation.Result != null && operation.Result.Length > 0
                    ? operation.Result[0]
                    : null;

            if (instance == null)
                continue;

            instance.name =
                "Wilderness_" +
                SafeName(
                    slot) +
                "_" +
                spawned +
                "__" +
                prefab.name;

            position.y =
                YQGeneratedWorldTerrain
                    .SampleWorldHeight(
                        terrain,
                        position);

            instance.transform.position =
                position;

            instance.transform.rotation =
                Quaternion.Euler(
                    0f,
                    Deterministic01(
                        seed +
                        "|yaw") *
                    360f,
                    0f);

            float scale =
                Mathf.Lerp(
                    Mathf.Max(
                        0.01f,
                        reference.scaleMin),
                    Mathf.Max(
                        reference.scaleMin,
                        reference.scaleMax),
                    Deterministic01(
                        seed +
                        "|scale"));

            // note: Deterministic wilderness variation multiplies the imported prefab's authored root scale instead of erasing its unit conversion.
            instance.transform.localScale *=
                scale;

            registry.ApplyMaterialOverrides(
                reference.assetPath,
                instance);

            PrepareWildernessInstance(
                instance);

            // note: Imported scatter hierarchies repair cooperatively so an unexpectedly dense prefab cannot freeze the Goddess loading animation.
            yield return YQRuntimeUrpMaterialRepair
                .RepairMaterialHierarchyRoutine(
                    instance,
                    null);

            if (!FinalizeSmallWildernessInstance(
                    instance,
                    terrain,
                    slot,
                    reference))
            {
                instance.SetActive(
                    false);

                UnityEngine.Object.Destroy(
                    instance);

                continue;
            }

            spawned++;

            // note: Wilderness dressing shares the strict loading budget; even a vegetation-heavy region cannot instantiate its complete scatter set on one presentation frame.
            if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        completed?.Invoke(spawned);
    }

    private static bool IsOversizedSmallScatterPrefab(
        GameObject prefab,
        string slot,
        GeneratedAssetReferenceRecord reference)
    {
        if (prefab == null ||
            !TryGetWildernessBounds(
                prefab,
                out Bounds bounds))
        {
            return false;
        }

        float scale =
            reference != null
                ? Mathf.Max(
                    0.01f,
                    reference.scaleMin,
                    reference.scaleMax)
                : 1f;

        float footprint =
            Mathf.Max(
                bounds.size.x,
                bounds.size.z) *
            scale;

        float height =
            bounds.size.y *
            scale;

        bool vegetation =
            string.Equals(
                slot,
                YQWorldAssetCatalog.SlotVegetation,
                StringComparison.OrdinalIgnoreCase);

        bool rock =
            string.Equals(
                slot,
                YQWorldAssetCatalog.SlotRock,
                StringComparison.OrdinalIgnoreCase);
        bool tree =
            vegetation &&
            LooksLikeTree(
                prefab,
                reference);

        // note: Legitimate tall conifers receive a tree-specific envelope; giant scenery sets remain rejected before instantiation.
        return (vegetation &&
                (footprint > (tree ? 28f : 20f) ||
                 height > (tree ? 54f : 32f))) ||
               (rock &&
                (footprint > 16f || height > 16f));
    }

    private static List<GeneratedAssetReferenceRecord>
        BuildSmallScatterReferences(
            GeneratedRegionAssetPaletteRecord palette,
            string slot,
            bool treesOnly = false)
    {
        List<GeneratedAssetReferenceRecord> candidates =
            new List<GeneratedAssetReferenceRecord>();

        if (palette == null)
            return candidates;

        List<GeneratedAssetReferenceRecord> primary =
            YQWorldAssetCatalog
                .GetSlotList(
                    palette,
                    slot);

        AddValidSmallReferences(
            candidates,
            primary,
            slot,
            treesOnly);

        if (treesOnly &&
            string.Equals(
                slot,
                YQWorldAssetCatalog.SlotVegetation,
                StringComparison.OrdinalIgnoreCase) &&
            ShouldSpawnVisibleTrees(palette))
        {
            AddApprovedVisibleTreeReferences(
                candidates,
                palette);
        }

        /*
         * Some palettes only put giant mountains in SlotRock.
         *
         * If every primary rock was rejected, look through exterior
         * dressing for rubble/stone/rock assets before giving up.
         */
        if (candidates.Count == 0 &&
            string.Equals(
                slot,
                YQWorldAssetCatalog.SlotRock,
                StringComparison.OrdinalIgnoreCase))
        {
            if (palette.exteriorDeco != null)
            {
                for (int i = 0;
                     i < palette.exteriorDeco.Count;
                     i++)
                {
                    GeneratedAssetReferenceRecord reference =
                        palette.exteriorDeco[i];

                    if (reference == null ||
                        IsLargeTerrainFeatureReference(
                            reference))
                    {
                        continue;
                    }

                    string semantic =
                        BuildReferenceSemanticText(
                            reference);

                    if (ContainsAnySemantic(
                            semantic,
                            "rock",
                            "stone",
                            "rubble",
                            "debris",
                            "boulder"))
                    {
                        AddUniqueSmallReference(
                            candidates,
                            reference);
                    }
                }
            }
        }

        return candidates;
    }

    private static void AddValidSmallReferences(
        List<GeneratedAssetReferenceRecord> result,
        List<GeneratedAssetReferenceRecord> source,
        string slot,
        bool treesOnly)
    {
        if (result == null ||
            source == null)
        {
            return;
        }

        bool rockSlot =
            string.Equals(
                slot,
                YQWorldAssetCatalog.SlotRock,
                StringComparison.OrdinalIgnoreCase);
        bool vegetationSlot =
            string.Equals(
                slot,
                YQWorldAssetCatalog.SlotVegetation,
                StringComparison.OrdinalIgnoreCase);

        for (int i = 0;
             i < source.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                source[i];

            if (reference == null ||
                string.IsNullOrWhiteSpace(
                    reference.assetPath) ||
                IsLargeTerrainFeatureReference(
                    reference))
            {
                continue;
            }

            if (rockSlot &&
                !ContainsAnySemantic(
                    BuildReferenceIdentitySemanticText(reference),
                    "rock",
                    "stone",
                    "rubble",
                    "debris",
                    "boulder",
                    "pebble",
                    "gravel"))
            {
                // note: A palette slot label is not proof of physical identity; statues, branches, and machine cubes formerly appeared as wilderness rocks.
                continue;
            }

            bool treeReference =
                vegetationSlot &&
                LooksLikeTree(
                    null,
                    reference);

            if (vegetationSlot &&
                treeReference != treesOnly)
            {
                // note: Visible tree groves and low foliage use separate passes so a Terrain-prototype rejection can never erase every tree or let canopies crowd out ground cover.
                continue;
            }

            AddUniqueSmallReference(
                result,
                reference);
        }
    }

    private static void AddApprovedVisibleTreeReferences(
        List<GeneratedAssetReferenceRecord> result,
        GeneratedRegionAssetPaletteRecord palette)
    {
        if (result == null)
            return;

        string style =
            palette != null
                ? SafeText(
                    palette.styleKey,
                    string.Empty)
                : string.Empty;
        string[] approvedPaths =
            ContainsAnySemantic(
                style,
                "desert",
                "persepolis",
                "western",
                "badland")
                ? ApprovedDryTreePrefabs
                : ApprovedVisibleTreePrefabs;

        for (int index = 0;
             index < approvedPaths.Length;
             index++)
        {
            string assetPath = approvedPaths[index];
            bool conifer =
                assetPath.IndexOf(
                    "conifer",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetPath.IndexOf(
                    "pine",
                    StringComparison.OrdinalIgnoreCase) >= 0;

            // note: These are semantic runtime references to existing imported URP assets, not generated art or arbitrary LLM-selected paths.
            AddUniqueSmallReference(
                result,
                new GeneratedAssetReferenceRecord
                {
                    assetKey = "approved_visible_tree_" + index,
                    assetPath = assetPath,
                    assetType = "prefab",
                    slotTag = YQWorldAssetCatalog.SlotVegetation,
                    weight = 1,
                    scaleMin = 0.82f,
                    scaleMax = 1.18f,
                    footprintX = 6f,
                    footprintZ = 6f,
                    placementRule = "terrain_grove",
                    rotationRule = "random_yaw",
                    allowRepeat = true,
                    blocksNav = true,
                    notes = "Approved material-repaired visible tree fallback.",
                    subTags = new List<string>
                    {
                        "tree",
                        conifer ? "conifer" : "deciduous",
                        "material_repaired"
                    },
                    styleTags = new List<string> { "fantasy", "natural" }
                });
        }
    }

    private static int ResolveVisibleTreeTarget(
        int vegetationTarget,
        GeneratedRegionAssetPaletteRecord palette)
    {
        if (palette == null ||
            !ShouldSpawnVisibleTrees(palette))
        {
            return 0;
        }

        string style =
            SafeText(
                palette.styleKey,
                string.Empty);

        if (ContainsAnySemantic(
                style,
                "desert",
                "persepolis",
                "western",
                "badland"))
        {
            // note: Dry regions keep sparse, palette-compatible trees without importing a temperate conifer forest into their silhouette.
            return Mathf.Clamp(
                Mathf.RoundToInt(vegetationTarget * 0.18f),
                10,
                22);
        }

        // note: A bounded near-field grove layer guarantees presence and variety while Terrain instances remain responsible for distant forest density.
        return Mathf.Clamp(
            Mathf.RoundToInt(vegetationTarget * 0.34f),
            24,
            42);
    }

    private static bool ShouldSpawnVisibleTrees(
        GeneratedRegionAssetPaletteRecord palette)
    {
        string style =
            palette != null
                ? SafeText(
                    palette.styleKey,
                    string.Empty)
                    .ToLowerInvariant()
                : string.Empty;

        return !ContainsAnySemantic(
            style,
            "scifi",
            "cyberpunk",
            "container",
            "bio_horror",
            "hospital",
            "sewer",
            "military");
    }

    private static void AddUniqueSmallReference(
        List<GeneratedAssetReferenceRecord> result,
        GeneratedAssetReferenceRecord reference)
    {
        if (result == null || reference == null)
            return;

        for (int index = 0;
             index < result.Count;
             index++)
        {
            if (result[index] != null &&
                string.Equals(
                    result[index].assetPath,
                    reference.assetPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        result.Add(reference);
    }

    private static GeneratedAssetReferenceRecord
        PickWeightedReference(
            List<GeneratedAssetReferenceRecord> candidates,
            string seed)
    {
        if (candidates == null ||
            candidates.Count == 0)
        {
            return null;
        }

        int totalWeight =
            0;

        for (int i = 0;
             i < candidates.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                candidates[i];

            if (reference != null)
            {
                totalWeight +=
                    Mathf.Max(
                        1,
                        reference.weight);
            }
        }

        if (totalWeight <= 0)
        {
            return
                candidates[
                    (int)(
                        StableHash32(
                            seed) %
                        (uint)candidates.Count)];
        }

        int roll =
            (int)(
                StableHash32(
                    seed +
                    "|weighted") %
                (uint)totalWeight);

        for (int i = 0;
             i < candidates.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                candidates[i];

            if (reference == null)
                continue;

            roll -=
                Mathf.Max(
                    1,
                    reference.weight);

            if (roll < 0)
                return reference;
        }

        return candidates[0];
    }

    // ============================================================
    // CAVES
    // ============================================================

    private static IEnumerator BuildRegionalCavesRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        Vector3 regionCenter,
        Action<int> completed)
    {
        if (parent == null ||
            terrain == null ||
            plan == null ||
            region == null ||
            palette == null ||
            registry == null ||
            palette.enemySite == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        List<GeneratedAssetReferenceRecord> caveReferences =
            new List<
                GeneratedAssetReferenceRecord>();

        for (int i = 0;
             i < palette.enemySite.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                palette.enemySite[i];

            if (reference != null &&
                IsCaveReference(
                    reference))
            {
                caveReferences.Add(
                    reference);
            }
        }

        if (caveReferences.Count == 0)
        {
            completed?.Invoke(0);
            yield break;
        }

        // note: Regions receive a small exploration cluster instead of one isolated entrance; higher danger supports a third destination.
        int desired =
            region.dangerTier >= 5
                ? 3
                : region.dangerTier >= 3
                    ? 2
                    : 1;

        GameObject caveRoot =
            new GameObject(
                "Caves");

        caveRoot.transform.SetParent(
            parent,
            false);

        int spawned =
            0;

        for (int caveIndex = 0;
             caveIndex < desired;
             caveIndex++)
        {
            bool created =
                false;

            for (int attempt = 0;
                 attempt < 10 &&
                 !created;
                 attempt++)
            {
                string seed =
                    plan.worldSeed +
                    "|wilderness_cave|" +
                    region.regionId +
                    "|" +
                    caveIndex +
                    "|" +
                    attempt;

                if (!TryResolveWildernessPosition(
                        terrain,
                        plan,
                        regionCenter,
                        seed,
                        78f,
                        WildernessRadiusMax,
                        52f,
                        72f,
                        42f,
                        out Vector3 position))
                {
                    continue;
                }

                GeneratedAssetReferenceRecord reference =
                    caveReferences[
                        (int)(
                            StableHash32(
                                seed +
                                "|reference") %
                            (uint)caveReferences.Count)];

                GameObject prefab =
                    registry.ResolvePrefab(
                        reference.assetPath);

                if (prefab == null)
                    continue;

                List<Collider> temporarilyDisabledColliders =
                    DisableMalformedPrefabPrimitiveColliders(
                        prefab);

                GameObject instance =
                    null;

                try
                {
                    // note: Mirrored imported boxes remain disabled on the clone and are replaced by structural mesh collision below.
                    AsyncInstantiateOperation<GameObject> operation =
                        UnityEngine.Object.InstantiateAsync(
                        prefab,
                        caveRoot.transform);
                    operation.priority = -1;
                    yield return operation;
                    if (operation.Result != null && operation.Result.Length > 0)
                        instance = operation.Result[0];
                }
                finally
                {
                    RestorePrefabColliders(
                        temporarilyDisabledColliders);
                }

                if (instance == null)
                    continue;

                instance.name =
                    "WildernessCave_" +
                    caveIndex +
                    "__" +
                    prefab.name;

                position.y =
                    YQGeneratedWorldTerrain
                        .SampleWorldHeight(
                            terrain,
                            position);

                instance.transform.position =
                    position;

                Vector3 approachDirection =
                    regionCenter - position;
                approachDirection.y = 0f;
                float approachYaw =
                    approachDirection.sqrMagnitude > 0.01f
                        ? Quaternion.LookRotation(
                            approachDirection.normalized,
                            Vector3.up).eulerAngles.y
                        : 0f;
                instance.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        approachYaw +
                            Mathf.Lerp(
                                -12f,
                                12f,
                                Deterministic01(
                                    seed +
                                    "|yaw_variation")),
                        0f);
                // note: Cave mouths face their deterministic terraced approach with only a small natural yaw variation instead of rotating independently from the route.

                float scale =
                    Mathf.Lerp(
                        Mathf.Max(
                            0.01f,
                            reference.scaleMin),
                        Mathf.Max(
                            reference.scaleMin,
                            reference.scaleMax),
                        Deterministic01(
                            seed +
                            "|scale"));

                // note: Preserve authored cave-module root scale while applying deterministic variation.
                instance.transform.localScale *=
                    scale;

                registry.ApplyMaterialOverrides(
                    reference.assetPath,
                    instance);

                // note: Cave material traversal shares the loading-frame budget instead of turning async instantiation into a synchronous hierarchy spike.
                yield return YQRuntimeUrpMaterialRepair
                    .RepairMaterialHierarchyRoutine(
                        instance,
                        null);

                PrepareStaticStructure(
                    instance);

                if (!TryGetWildernessBounds(
                        instance,
                        out Bounds bounds))
                {
                    instance.SetActive(
                        false);

                    UnityEngine.Object.Destroy(
                        instance);

                    continue;
                }

                /*
                 * Reject absurd backdrop-sized cave modules.
                 */
                float footprint =
                    Mathf.Max(
                        bounds.size.x,
                        bounds.size.z);

                if (footprint >
                        78f ||
                    bounds.size.y >
                        55f)
                {
                    instance.SetActive(
                        false);

                    UnityEngine.Object.Destroy(
                        instance);

                    continue;
                }

                if (!ValidateStructuralTerrainFootprint(
                        terrain,
                        bounds))
                {
                    instance.SetActive(
                        false);

                    UnityEngine.Object.Destroy(
                        instance);

                    continue;
                }

                GroundStructuralFeature(
                    instance,
                    terrain,
                    0.12f);

                EnsureStaticStructuralCollision(
                    instance);

                created =
                    true;

                spawned++;
            }
        }

        if (spawned == 0)
        {
            caveRoot.SetActive(
                false);

            UnityEngine.Object.Destroy(
                caveRoot);
        }

        completed?.Invoke(spawned);
    }

    private static bool IsCaveReference(
        GeneratedAssetReferenceRecord reference)
    {
        if (reference == null)
            return false;

        string semantic =
            BuildReferenceSemanticText(
                reference);

        return
            ContainsAnySemantic(
                semantic,
                "cave",
                "underground",
                "mine",
                "tunnel",
                "cavern");
    }

    // ============================================================
    // AMBIENT OVERWORLD ENCOUNTERS
    // ============================================================

    private static IEnumerator BuildRegionalAmbientEncountersRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedRegionRecord region,
        YQRuntimeWorldAssetRegistry registry,
        Vector3 regionCenter,
        Action<int> completed)
    {
        if (parent == null ||
            terrain == null ||
            plan == null ||
            region == null ||
            registry == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        List<AmbientMonsterSource> sources =
            FindAmbientMonsterSources(
                plan,
                region.regionId);

        if (sources.Count == 0)
        {
            /*
             * No authored hostile family exists in this region.
             *
             * Do not invent a completely unrelated ecosystem.
             */
            completed?.Invoke(0);
            yield break;
        }

        int danger =
            Mathf.Clamp(
                region.dangerTier,
                0,
                8);

        int groups =
            Mathf.Clamp(
                BaseAmbientEncounterGroupsPerRegion +
                danger /
                3,
                2,
                6);

        GameObject encounterRoot =
            new GameObject(
                "Ambient_Encounters");

        encounterRoot.transform.SetParent(
            parent,
            false);

        int total =
            0;

        for (int groupIndex = 0;
             groupIndex < groups;
             groupIndex++)
        {
            string groupSeed =
                plan.worldSeed +
                "|ambient_group|" +
                region.regionId +
                "|" +
                groupIndex;

            if (!TryResolveWildernessPosition(
                    terrain,
                    plan,
                    regionCenter,
                    groupSeed,
                    72f,
                    WildernessRadiusMax,
                    62f,
                    82f,
                    EncampmentEncounterClearRadius,
                    out Vector3 groupPosition))
            {
                continue;
            }

            AmbientMonsterSource source =
                sources[
                    (int)(
                        StableHash32(
                            groupSeed +
                            "|family") %
                        (uint)sources.Count)];

            int maximumGroupSize =
                Mathf.Clamp(
                    1 +
                    danger /
                    2,
                    1,
                    3);

            int memberCount =
                1 +
                Mathf.FloorToInt(
                    Deterministic01(
                        groupSeed +
                        "|count") *
                    maximumGroupSize);

            memberCount =
                Mathf.Clamp(
                    memberCount,
                    1,
                    maximumGroupSize);

            for (int member = 0;
                 member < memberCount;
                 member++)
            {
                string seed =
                    groupSeed +
                    "|member|" +
                    member;

                AmbientMonsterSource resolvedSource =
                    source;
                if (!TryResolveAmbientMonsterPrefab(
                        registry,
                        source.family,
                        seed,
                        out YQRuntimeWorldAssetEntry entry,
                        out string resolvedCategory))
                {
                    // note: Unsupported generated species never become misleading capsules or arbitrary models; a verified curated spider is labeled and factioned as fallback wildlife instead.
                    if (!TryResolveAmbientMonsterPrefab(
                            registry,
                            "wilderness spider",
                            seed + "|wildlife_fallback",
                            out entry,
                            out resolvedCategory))
                    {
                        continue;
                    }

                    resolvedSource =
                        new AmbientMonsterSource
                        {
                            family = "wilderness spider",
                            factionId = "generated_wildlife"
                        };
                }

                Vector3 offset =
                    ResolveRadialOffset(
                        seed,
                        member == 0
                            ? 0f
                            : 2.5f,
                        member == 0
                            ? 0f
                            : 6f);

                Vector3 position =
                    groupPosition +
                    offset;

                if (!InsideTerrain(
                        terrain,
                        position))
                {
                    continue;
                }

                position.y =
                    YQGeneratedWorldTerrain
                        .SampleWorldHeight(
                            terrain,
                            position);

                AsyncInstantiateOperation<GameObject> operation =
                    UnityEngine.Object.InstantiateAsync(
                        entry.prefab,
                        encounterRoot.transform);
                operation.priority = -1;
                yield return operation;
                GameObject instance =
                    operation.Result != null && operation.Result.Length > 0
                        ? operation.Result[0]
                        : null;

                if (instance == null)
                    continue;

                instance.name =
                    "AmbientEnemy__" +
                    SafeName(
                        resolvedSource.family) +
                    "__" +
                    StableHash32(
                        seed)
                        .ToString("x8");

                instance.transform.position =
                    position;

                registry.ApplyMaterialOverrides(
                    entry.assetPath,
                    instance);

                // note: Dense creature prefabs repair over bounded frames before silhouette normalization and physics setup.
                yield return YQRuntimeUrpMaterialRepair
                    .RepairMaterialHierarchyRoutine(
                        instance,
                        null);

                float targetHeight =
                    ResolveMonsterTargetHeight(
                        resolvedSource.family);

                if (!TryNormalizeMonsterVisualEnvelope(
                        instance,
                        targetHeight,
                        resolvedCategory))
                {
                    // note: Pathological or semantically incompatible wilderness silhouettes are rejected before physics and AI make them player-facing.
                    UnityEngine.Object.Destroy(
                        instance);

                    continue;
                }

                PrepareAmbientEnemyPhysics(
                    instance);

                GroundCharacterToTerrain(
                    instance,
                    terrain,
                    position);

                ConfigureAmbientEnemy(
                    instance,
                    region,
                    resolvedSource,
                    seed);

                total++;
            }
        }

        if (total == 0)
        {
            encounterRoot.SetActive(
                false);

            UnityEngine.Object.Destroy(
                encounterRoot);
        }

        completed?.Invoke(total);
    }

    private static List<AmbientMonsterSource>
        FindAmbientMonsterSources(
            GeneratedWorldPlanRecord plan,
            string regionId)
    {
        List<AmbientMonsterSource> result =
            new List<
                AmbientMonsterSource>();

        if (plan == null)
        {
            return result;
        }

        if (plan.encampments != null)
        {
            for (int i = 0;
                 i < plan.encampments.Count;
                 i++)
            {
                GeneratedEncampmentRecord encampment =
                    plan.encampments[i];

                if (encampment == null ||
                    !string.Equals(
                        encampment.regionId,
                        regionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddAmbientMonsterSource(
                    result,
                    encampment);
            }
        }

        if (result.Count == 0 &&
            plan.encampments != null)
        {
            for (int i = 0;
                 i < plan.encampments.Count;
                 i++)
            {
                // note: A region without its own generated encounter may reuse a world-canonical family before any mechanical wildlife fallback is considered.
                AddAmbientMonsterSource(
                    result,
                    plan.encampments[i]);
            }
        }

        if (result.Count == 0)
        {
            // note: Baseline wildlife is mechanical fallback scaffolding, not generated canon; it exists only so incomplete or offline generation cannot leave every region lifeless.
            result.Add(
                new AmbientMonsterSource
                {
                    // note: The installed creature shard contains curated spider prefabs, so this baseline cannot silently target a nonexistent beast category.
                    family = "wilderness spider",
                    factionId = "generated_wildlife"
                });
        }

        return result;
    }

    private static void AddAmbientMonsterSource(
        List<AmbientMonsterSource> result,
        GeneratedEncampmentRecord encampment)
    {
        if (result == null || encampment == null)
            return;

        string family =
            SafeText(
                encampment.monsterFamily,
                string.Empty);

        if (string.IsNullOrWhiteSpace(family))
            return;

        for (int existing = 0;
             existing < result.Count;
             existing++)
        {
            if (string.Equals(
                    result[existing].family,
                    family,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        result.Add(
            new AmbientMonsterSource
            {
                family = family,
                factionId = SafeText(
                    encampment.inhabitantFactionId,
                    "generated_wilderness")
            });
    }

    private static bool TryResolveAmbientMonsterPrefab(
        YQRuntimeWorldAssetRegistry registry,
        string family,
        string seed,
        out YQRuntimeWorldAssetEntry result,
        out string resolvedCategory)
    {
        result =
            null;

        resolvedCategory =
            string.Empty;

        if (registry == null)
        {
            return false;
        }

        if (YQRuntimeCreatureAssetIndex.TryResolveMonster(
                registry,
                family,
                SafeText(
                    family,
                    seed),
                seed,
                out result,
                out resolvedCategory) &&
            result != null)
        {
            // note: Ambient encounters share the lazy creature shard and the same deterministic family-safety contract as settlement hostiles.
            return true;
        }

        if (registry.Entries == null)
            return false;

        string familySemantic =
            NormalizeSemanticText(
                family);

        List<string> wanted =
            ExtractSemanticTerms(
                family);

        /*
         * Add useful family aliases.
         */
        if (ContainsAnySemantic(
                familySemantic,
                "rock",
                "stone",
                "golem"))
        {
            AddUnique(
                wanted,
                "rock");

            AddUnique(
                wanted,
                "golem");

            AddUnique(
                wanted,
                "stone");
        }

        if (ContainsAnySemantic(
                familySemantic,
                "mushroom",
                "shroom",
                "fungus",
                "fungal"))
        {
            AddUnique(
                wanted,
                "mushroom");

            AddUnique(
                wanted,
                "fungus");

            AddUnique(
                wanted,
                "shroom");
        }

        if (ContainsAnySemantic(
                familySemantic,
                "worm",
                "wyrm",
                "larva",
                "grub"))
        {
            AddUnique(
                wanted,
                "worm");

            AddUnique(
                wanted,
                "wyrm");
        }

        if (ContainsAnySemantic(
                familySemantic,
                "demon",
                "fiend",
                "devil"))
        {
            AddUnique(
                wanted,
                "demon");

            AddUnique(
                wanted,
                "fiend");
        }

        if (ContainsAnySemantic(
                familySemantic,
                "dragon",
                "drake",
                "wyvern"))
        {
            AddUnique(
                wanted,
                "dragon");

            AddUnique(
                wanted,
                "drake");

            AddUnique(
                wanted,
                "wyvern");
        }

        if (ContainsAnySemantic(
                familySemantic,
                "plant",
                "vine",
                "thorn",
                "flora"))
        {
            AddUnique(
                wanted,
                "plant");

            AddUnique(
                wanted,
                "vine");
        }

        if (ContainsAnySemantic(
                familySemantic,
                "mimic"))
        {
            AddUnique(
                wanted,
                "mimic");
        }

        if (ContainsAnySemantic(
                familySemantic,
                "bandit",
                "raider",
                "brigand",
                "cultist",
                "cult",
                "human",
                "humanoid",
                "soldier",
                "warrior",
                "guard",
                "scavenger",
                "marauder",
                "goblin",
                "orc",
                "kobold"))
        {
            AddUnique(
                wanted,
                "human");

            AddUnique(
                wanted,
                "bandit");

            AddUnique(
                wanted,
                "raider");

            AddUnique(
                wanted,
                "cultist");

            AddUnique(
                wanted,
                "male");

            AddUnique(
                wanted,
                "female");
        }

        List<YQRuntimeWorldAssetEntry> best =
            new List<
                YQRuntimeWorldAssetEntry>();

        int bestScore =
            int.MinValue;

        for (int i = 0;
             i < registry.Entries.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                registry.Entries[i];

            if (entry == null ||
                entry.prefab == null ||
                !IsCharacterLikePrefab(
                    entry.prefab))
            {
                continue;
            }

            string semantic =
                NormalizeSemanticText(
                    SafeText(
                        entry.assetPath,
                        string.Empty) +
                    " " +
                    entry.prefab.name);

            /*
             * Prevent environmental statues, vegetation and architecture
             * from becoming enemies merely because their filename contains
             * "dragon", "mushroom", etc.
             */
            if (ContainsAnySemantic(
                    semantic,
                    "environment",
                    "architecture",
                    "building",
                    "structure",
                    "terrain",
                    "statue",
                    "decor",
                    "wall",
                    "floor"))
            {
                continue;
            }

            int matches =
                CountSemanticMatches(
                    semantic,
                    wanted);

            if (matches <= 0)
                continue;

            int score =
                matches *
                40;

            if (entry.prefab
                    .GetComponentInChildren<
                        SkinnedMeshRenderer>(
                            true) != null)
            {
                score +=
                    16;
            }

            if (entry.prefab
                    .GetComponentInChildren<
                        Animator>(
                            true) != null)
            {
                score +=
                    10;
            }

            if (ContainsAnySemantic(
                    semantic,
                    "monster",
                    "creature",
                    "character"))
            {
                score +=
                    8;
            }

            if (score >
                bestScore)
            {
                bestScore =
                    score;

                best.Clear();

                best.Add(
                    entry);
            }
            else if (score ==
                     bestScore)
            {
                best.Add(
                    entry);
            }
        }

        if (best.Count == 0)
        {
            return false;
        }

        result =
            best[
                (int)(
                    StableHash32(
                        seed +
                        "|monster_visual") %
                    (uint)best.Count)];

        resolvedCategory =
            YQRuntimeCreatureAssetIndex
                .ClassifyEntry(
                    result);

        if (IsHumanoidGeneratedFamily(
                familySemantic))
        {
            if (IsExplicitNonHumanoidMonsterCategory(
                    resolvedCategory))
            {
                // note: Legacy root-registry lookup may not cross a humanoid family into a dragon, demon, or other giant monster category.
                result =
                    null;

                resolvedCategory =
                    string.Empty;

                return false;
            }

            resolvedCategory =
                YQRuntimeCreatureAssetIndex
                    .HumanoidHostile;
        }

        return
            result != null &&
            result.prefab != null;
    }

    private static bool IsHumanoidGeneratedFamily(
        string normalizedSemantic)
    {
        return ContainsAnySemantic(
            normalizedSemantic,
            "bandit",
            "raider",
            "brigand",
            "cultist",
            "cult",
            "soldier",
            "mercenary",
            "outlaw",
            "pirate",
            "warrior",
            "human",
            "humanoid",
            "scavenger",
            "marauder",
            "goblin",
            "orc",
            "kobold");
    }

    private static bool IsHumanoidVisualCategory(
        string category)
    {
        return
            string.Equals(category, YQRuntimeCreatureAssetIndex.HumanoidHostile, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.HumanMale, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.HumanFemale, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.HumanGeneric, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExplicitNonHumanoidMonsterCategory(
        string category)
    {
        return
            string.Equals(category, YQRuntimeCreatureAssetIndex.Dragon, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.Demon, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.RockMonster, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.WormMonster, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.PlantMonster, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.MushroomMonster, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.Mimic, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.Undead, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.Spider, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.Beast, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCharacterLikePrefab(
        GameObject prefab)
    {
        if (prefab == null)
            return false;

        if (prefab.GetComponentInChildren<
                SkinnedMeshRenderer>(
                    true) != null)
        {
            return true;
        }

        if (prefab.GetComponentInChildren<
                Animator>(
                    true) != null)
        {
            return true;
        }

        if (prefab.GetComponentInChildren<
                Animation>(
                    true) != null)
        {
            return true;
        }

        return false;
    }

    private static void ConfigureAmbientEnemy(
        GameObject root,
        GeneratedRegionRecord region,
        AmbientMonsterSource source,
        string seed)
    {
        if (root == null ||
            region == null ||
            source == null)
        {
            return;
        }

        int tier =
            Mathf.Clamp(
                region.dangerTier,
                1,
                8);

        string displayName =
            BuildAmbientMonsterLabel(
                source.family,
                seed);

        EntityInfo info =
            root.GetComponent<
                EntityInfo>();

        if (info == null)
        {
            info =
                root.AddComponent<
                    EntityInfo>();
        }

        info.entityId =
            "ambient_" +
            StableHash32(
                seed +
                "|" +
                region.regionId)
                .ToString("x8");

        info.displayName =
            displayName;

        info.level =
            Mathf.Clamp(
                tier,
                1,
                12);

        info.factionId =
            source.factionId;

        info.hostility =
            Hostility.Hostile;

        info.isNotable =
            false;

        info.tags =
            new[]
            {
                "generated",
                "enemy",
                "hostile",
                "wilderness",
                NormalizeTag(
                    source.family),
                NormalizeTag(
                    region.regionId)
            };

        YQInvestorEnemy enemy =
            root.GetComponent<
                YQInvestorEnemy>();

        if (enemy == null)
        {
            enemy =
                root.AddComponent<
                    YQInvestorEnemy>();
        }

        enemy.semanticRegionId =
            region.regionId;

        enemy.factionId =
            source.factionId;

        enemy.displayName =
            displayName;

        enemy.maxHealth =
            40f +
            tier *
            15f;

        enemy.moveSpeed =
            Mathf.Clamp(
                3.0f +
                tier *
                0.11f,
                3f,
                5.0f);

        enemy.aggroRange =
            14f +
            tier *
            1.1f;

        enemy.attackRange =
            1.75f;

        enemy.attackCooldown =
            Mathf.Max(
                0.76f,
                1.22f -
                tier *
                0.035f);

        enemy.attackDamage =
            6 +
            tier *
            3;

        enemy.goldDrop =
            3 +
            tier *
            3;

        enemy.useWispVisual =
            false;

        enemy.rarity =
            tier >= 6
                ? "uncommon"
                : "common";

        string family =
            source.family
                .ToLowerInvariant();

        enemy.allowFlight =
            family.Contains("dragon") ||
            family.Contains("wyvern") ||
            family.Contains("wisp") ||
            family.Contains("bat") ||
            family.Contains("harpy") ||
            family.Contains("wing") ||
            family.Contains("flying");

        enemy.Initialize(
            null);

        // note: Ambient enemies receive their safety guard as each async wilderness spawn completes, preserving the streaming frame budget.
        YQGeneratedEnemyRuntimeSafety.EnsureAttached(
            enemy);
    }

    private static string BuildAmbientMonsterLabel(
        string family,
        string seed)
    {
        string safe =
            SafeText(
                family,
                "Wilderness Creature");

        int variant =
            (int)(
                StableHash32(
                    seed +
                    "|label") %
                4u);

        switch (variant)
        {
            case 0:
                return
                    safe +
                    " Stalker";

            case 1:
                return
                    safe +
                    " Hunter";

            case 2:
                return
                    safe +
                    " Prowler";

            default:
                return
                    safe;
        }
    }

    private static float ResolveMonsterTargetHeight(
        string family)
    {
        string text =
            NormalizeSemanticText(
                family);

        if (ContainsAnySemantic(
                text,
                "dragon",
                "drake",
                "wyvern"))
        {
            return 3.2f;
        }

        if (ContainsAnySemantic(
                text,
                "rock",
                "stone",
                "golem"))
        {
            return 2.35f;
        }

        if (ContainsAnySemantic(
                text,
                "demon",
                "fiend"))
        {
            return 2.05f;
        }

        if (ContainsAnySemantic(
                text,
                "worm",
                "grub",
                "larva"))
        {
            return 1.20f;
        }

        if (ContainsAnySemantic(
                text,
                "mushroom",
                "fungus",
                "shroom"))
        {
            return 1.45f;
        }

        if (ContainsAnySemantic(
                text,
                "mimic"))
        {
            return 1.10f;
        }

        return 1.85f;
    }

    private static void PrepareAmbientEnemyPhysics(
        GameObject root)
    {
        if (root == null)
            return;

        Rigidbody rootBody =
            root.GetComponent<
                Rigidbody>();

        Rigidbody[] bodies =
            root.GetComponentsInChildren<
                Rigidbody>(
                    true);

        for (int i = 0;
             i < bodies.Length;
             i++)
        {
            Rigidbody body =
                bodies[i];

            if (body == null ||
                body ==
                    rootBody)
            {
                continue;
            }

            if (!body.isKinematic)
            {
                // note: Clear only dynamic imported ragdoll bodies before parking them as kinematic children.
                body.linearVelocity =
                    Vector3.zero;

                body.angularVelocity =
                    Vector3.zero;
            }

            body.useGravity =
                false;

            body.isKinematic =
                true;
        }

        if (rootBody == null)
        {
            rootBody =
                root.AddComponent<
                    Rigidbody>();
        }

        // note: The root ambient enemy body is the one dynamic body allowed to receive velocity writes.
        rootBody.isKinematic =
            false;

        rootBody.linearVelocity =
            Vector3.zero;

        rootBody.angularVelocity =
            Vector3.zero;

        rootBody.useGravity =
            true;

        rootBody.constraints =
            RigidbodyConstraints
                .FreezeRotation;

        EnsureCharacterCollider(
            root);
    }

    // ============================================================
    // WILDERNESS TREASURE
    // ============================================================

    private static IEnumerator BuildRegionalTreasureRoutine(
    Transform parent,
    Terrain terrain,
    GeneratedWorldPlanRecord plan,
    GeneratedRegionRecord region,
    GeneratedRegionAssetPaletteRecord palette,
    YQRuntimeWorldAssetRegistry registry,
    Vector3 regionCenter,
    Action<int> completed)
    {
        if (parent == null ||
            terrain == null ||
            plan == null ||
            region == null ||
            palette == null ||
            registry == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        int danger =
            Mathf.Clamp(
                region.dangerTier,
                1,
                8);

        int desired =
            BaseTreasurePerRegion;

        if (danger >= 5)
        {
            desired++;
        }

        GameObject root =
            new GameObject(
                "Wilderness_Treasure");

        root.transform.SetParent(
            parent,
            false);

        string worldKey =
            StableHash32(
                plan.worldSeed)
                .ToString("x8");

        int spawned =
            0;

        for (int index = 0;
             index < desired;
             index++)
        {
            bool created =
                false;

            for (int attempt = 0;
                 attempt < 8 &&
                 !created;
                 attempt++)
            {
                /*
                 * IMPORTANT:
                 *
                 * Position-attempt number belongs only to placement.
                 *
                 * The permanent chest identity below uses the reward slot
                 * index, not the successful attempt number.
                 */
                string seed =
                    plan.worldSeed +
                    "|wilderness_treasure|" +
                    region.regionId +
                    "|" +
                    index +
                    "|" +
                    attempt;

                if (!TryResolveWildernessPosition(
                        terrain,
                        plan,
                        regionCenter,
                        seed,
                        72f,
                        WildernessRadiusMax,
                        55f,
                        76f,
                        28f,
                        out Vector3 position))
                {
                    continue;
                }

                GeneratedAssetReferenceRecord reference =
                    YQWorldAssetCatalog
                        .PickAssetForSlot(
                            palette,
                            YQWorldAssetCatalog
                                .SlotLootContainer,
                            seed);

                if (reference == null)
                    continue;

                GameObject prefab =
                    registry.ResolvePrefab(
                        reference.assetPath);

                if (prefab == null)
                    continue;

                AsyncInstantiateOperation<GameObject> operation =
                    UnityEngine.Object.InstantiateAsync(
                        prefab,
                        root.transform);
                operation.priority = -1;
                yield return operation;
                GameObject chest =
                    operation.Result != null && operation.Result.Length > 0
                        ? operation.Result[0]
                        : null;

                if (chest == null)
                    continue;

                string persistentId =
                    "wilderness:" +
                    worldKey +
                    ":" +
                    SafeName(
                        region.regionId) +
                    ":treasure:" +
                    index;

                chest.name =
                    "WildernessLoot__" +
                    StableHash32(
                        persistentId)
                        .ToString("x8") +
                    "__" +
                    prefab.name;

                position.y =
                    YQGeneratedWorldTerrain
                        .SampleWorldHeight(
                            terrain,
                            position);

                chest.transform.position =
                    position;

                chest.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        Deterministic01(
                            seed +
                            "|yaw") *
                        360f,
                        0f);

                float scale =
                    Mathf.Lerp(
                        Mathf.Max(
                            0.01f,
                            reference.scaleMin),
                        Mathf.Max(
                            reference.scaleMin,
                            reference.scaleMax),
                        Deterministic01(
                            seed +
                            "|scale"));

                // note: Preserve the loot prefab's authored root scale while applying deterministic variation.
                chest.transform.localScale *=
                    scale;

                registry.ApplyMaterialOverrides(
                    reference.assetPath,
                    chest);

                // note: Treasure hierarchies repair cooperatively before collision and grounding are finalized.
                yield return YQRuntimeUrpMaterialRepair
                    .RepairMaterialHierarchyRoutine(
                        chest,
                        null);

                PrepareStaticStructure(
                    chest);

                GroundStructuralFeature(
                    chest,
                    terrain,
                    0.03f);

                /*
                 * Wilderness reward balance.
                 */
                int bonusGold =
                    Mathf.FloorToInt(
                        Deterministic01(
                            seed +
                            "|gold") *
                        11f);

                int generatedGold =
                    8 +
                    danger *
                        5 +
                    bonusGold;

                float lockChance =
                    Mathf.Clamp01(
                        0.12f +
                        danger *
                            0.055f);

                bool generatedLocked =
                    Deterministic01(
                        seed +
                        "|locked") <
                    lockChance;

                float generatedDifficulty =
                    Mathf.Clamp(
                        0.14f +
                        danger *
                            0.055f +
                        Deterministic01(
                            seed +
                            "|difficulty") *
                            0.05f,
                        0.12f,
                        0.72f);

                /*
                 * Wilderness mimics are rare.
                 *
                 * Danger-8 regions reach roughly a 10% chance per cache.
                 */
                float mimicChance =
                    0.02f +
                    danger *
                        0.01f;

                bool generatedMimic =
                    Deterministic01(
                        seed +
                        "|mimic") <
                    mimicChance;

                int rewardLevel =
                    Mathf.Clamp(
                        danger,
                        1,
                        12);

                string rewardName =
                    region.displayName +
                    " Wilderness Cache";

                YQLockpickableLoot loot =
                    chest.GetComponent<
                        YQLockpickableLoot>();

                if (loot == null)
                {
                    loot =
                        chest.AddComponent<
                            YQLockpickableLoot>();
                }

                loot.ConfigureGeneratedLoot(
                    persistentId,
                    region.regionId,
                    rewardName,
                    generatedGold,
                    generatedLocked,
                    generatedDifficulty,
                    generatedMimic,
                    rewardLevel);

                EnsureSimpleSolidCollider(
                    chest);

                Debug.Log(
                    "[YQGeneratedWorldEnvironment] " +
                    "Generated wilderness treasure: " +
                    rewardName +
                    " | id=" +
                    persistentId +
                    " | gold=" +
                    generatedGold +
                    " | locked=" +
                    generatedLocked +
                    " | mimic=" +
                    generatedMimic);

                created =
                    true;

                spawned++;
            }
        }

        if (spawned == 0)
        {
            root.SetActive(
                false);

            UnityEngine.Object.Destroy(
                root);
        }

        completed?.Invoke(spawned);
    }

    // ============================================================
    // POSITION RESOLUTION
    // ============================================================

    private static bool TryResolveWildernessPosition(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Vector3 regionCenter,
        string seed,
        float minimumRadius,
        float maximumRadius,
        float settlementClearRadius,
        float originClearRadius,
        float encampmentClearRadius,
        out Vector3 position,
        MacroWaterSet macroWater = null)
    {
        position =
            Vector3.zero;

        float angle =
            Deterministic01(
                seed +
                "|angle") *
            Mathf.PI *
            2f;

        float radius =
            Mathf.Lerp(
                minimumRadius,
                maximumRadius,
                Mathf.Sqrt(
                    Deterministic01(
                        seed +
                        "|radius")));

        Vector3 candidate =
            new Vector3(
                regionCenter.x +
                    Mathf.Cos(angle) *
                    radius,
                0f,
                regionCenter.z +
                    Mathf.Sin(angle) *
                    radius);

        if (!IsWildernessPositionAllowed(
                terrain,
                plan,
                candidate,
                settlementClearRadius,
                originClearRadius,
                encampmentClearRadius,
                macroWater))
        {
            return false;
        }

        position =
            candidate;

        return true;
    }

    private static bool IsWildernessPositionAllowed(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Vector3 candidate,
        float settlementClearRadius,
        float originClearRadius,
        float encampmentClearRadius,
        MacroWaterSet macroWater = null)
    {
        // note: Cluster members pass the same terrain and authored-location reserves as their grove center, preventing denser dressing from invading doors, roads, or encounter staging areas.

        if (!InsideTerrainWithMargin(
                terrain,
                candidate,
                3f))
        {
            return false;
        }

        if (InsideOriginReserve(
                candidate,
                originClearRadius))
        {
            return false;
        }

        if (NearAnySettlement(
                plan,
                terrain,
                candidate,
                settlementClearRadius))
        {
            return false;
        }

        if (encampmentClearRadius > 0f &&
            NearAnyEncampment(
                plan,
                terrain,
                candidate,
                encampmentClearRadius))
        {
            return false;
        }

        if (IsInsideMacroWaterFootprint(
                terrain,
                plan,
                candidate,
                2f,
                macroWater))
        {
            // note: Trees, rocks, caves, encounters, and treasure share one shoreline test, preventing independent scatter passes from filling lake beds with dry-land content.
            return false;
        }

        return true;
    }

    private static bool IsSubmergedByMacroWater(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Vector3 candidate,
        float shorelinePadding,
        MacroWaterSet macroWater = null)
    {
        if (terrain == null || terrain.terrainData == null ||
            plan == null)
        {
            return false;
        }

        macroWater ??=
            BuildMacroWaterSet(
                terrain,
                plan);

        if (macroWater == null ||
            macroWater.count == 0)
        {
            return false;
        }

        for (int basinIndex = 0;
             basinIndex < macroWater.count;
             basinIndex++)
        {
            if (macroWater.basins[basinIndex]
                .IsBelowWaterSurface(
                    candidate,
                    shorelinePadding))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideMacroWaterFootprint(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Vector3 candidate,
        float shorelinePadding,
        MacroWaterSet macroWater = null)
    {
        if (terrain == null || terrain.terrainData == null ||
            plan == null)
        {
            return false;
        }

        macroWater ??=
            BuildMacroWaterSet(
                terrain,
                plan);

        for (int basinIndex = 0;
             basinIndex < macroWater.count;
             basinIndex++)
        {
            if (macroWater.basins[basinIndex]
                .ContainsXZ(
                    candidate,
                    shorelinePadding))
            {
                // note: Scatter rejection uses the cached basin footprint only; it avoids a native terrain-height query for every detail-map cell.
                return true;
            }
        }

        return false;
    }

    private static MacroWaterSet BuildMacroWaterSet(
        Terrain terrain,
        GeneratedWorldPlanRecord plan)
    {
        MacroWaterSet result =
            new MacroWaterSet();

        if (terrain == null || terrain.terrainData == null ||
            plan == null)
        {
            return result;
        }

        for (int basinIndex = 0;
             basinIndex < YQGeneratedWorldTerrain.MacroWaterBasinCount;
             basinIndex++)
        {
            if (!YQGeneratedWorldTerrain.TryGetMacroWaterBasin(
                    plan.worldSeed,
                    terrain,
                    basinIndex,
                    out YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin))
            {
                continue;
            }

            result.basins[result.count] = basin;
            result.count++;
        }

        // note: Hot terrain/detail loops cache both value descriptors once instead of re-hashing the world seed for every vegetation cell.
        return result;
    }

    private static Vector3 ResolveRadialOffset(
        string seed,
        float minRadius,
        float maxRadius)
    {
        if (maxRadius <=
            0.001f)
        {
            return Vector3.zero;
        }

        float angle =
            Deterministic01(
                seed +
                "|offset_angle") *
            Mathf.PI *
            2f;

        float radius =
            Mathf.Lerp(
                minRadius,
                maxRadius,
                Deterministic01(
                    seed +
                    "|offset_radius"));

        return
            new Vector3(
                Mathf.Cos(angle) *
                    radius,
                0f,
                Mathf.Sin(angle) *
                    radius);
    }

    // ============================================================
    // SMALL WILDERNESS PHYSICS
    // ============================================================

    private static void PrepareWildernessInstance(
        GameObject root)
    {
        if (root == null)
            return;

        Rigidbody[] bodies =
            root.GetComponentsInChildren<
                Rigidbody>(
                    true);

        for (int i = 0;
             i < bodies.Length;
             i++)
        {
            Rigidbody body =
                bodies[i];

            if (body == null)
                continue;

            if (!body.isKinematic)
            {
                // note: Dynamic imported wilderness props are zeroed before they become static presentation.
                body.linearVelocity =
                    Vector3.zero;

                body.angularVelocity =
                    Vector3.zero;
            }

            body.isKinematic =
                true;

            body.useGravity =
                false;
        }

        // note: Material traversal is owned by the surrounding spawn coroutine so this physics preparation remains bounded and allocation-light.
    }

    private static bool FinalizeSmallWildernessInstance(
        GameObject instance,
        Terrain terrain,
        string slot,
        GeneratedAssetReferenceRecord reference)
    {
        if (instance == null ||
            terrain == null)
        {
            return false;
        }

        if (!TryGetWildernessBounds(
                instance,
                out Bounds bounds))
        {
            return false;
        }

        bool vegetation =
            string.Equals(
                slot,
                YQWorldAssetCatalog
                    .SlotVegetation,
                StringComparison.OrdinalIgnoreCase);

        bool rock =
            string.Equals(
                slot,
                YQWorldAssetCatalog
                    .SlotRock,
                StringComparison.OrdinalIgnoreCase);

        float footprint =
            Mathf.Max(
                bounds.size.x,
                bounds.size.z);

        float height =
            bounds.size.y;

        if (IsLargeTerrainFeatureReference(
                reference))
        {
            return false;
        }

        if (rock &&
            (footprint > 16f ||
             height > 16f))
        {
            // note: Asset curation can reject many repeated scatter candidates in the first seconds of generation.
            LogOversizedWildernessRejection(
                "ordinary rock",
                instance,
                footprint,
                height);

            return false;
        }

        bool tree =
            vegetation &&
            LooksLikeTree(
                instance,
                reference);

        if (vegetation &&
            (footprint > (tree ? 28f : 20f) ||
             height > (tree ? 54f : 32f)))
        {
            // note: Keep the useful examples, then suppress repeats so startup logs do not swamp the editor.
            LogOversizedWildernessRejection(
                "vegetation/scenery asset",
                instance,
                footprint,
                height);

            return false;
        }

        /*
         * Small foliage uses deliberately simple traversal collision.
         */
        RemoveWildernessCollision(
            instance);

        GroundWildernessInstance(
            instance,
            terrain,
            slot,
            reference);

        if (!TryGetWildernessBounds(
                instance,
                out bounds))
        {
            return false;
        }

        if (vegetation)
        {
            if (LooksLikeTree(
                    instance,
                    reference))
            {
                AddTreeTrunkCollision(
                    instance,
                    terrain,
                    bounds);
            }
        }
        else if (rock)
        {
            AddTraversalSafeRockCollision(
                instance,
                terrain,
                bounds);
        }

        return true;
    }

    private static void LogOversizedWildernessRejection(
        string label,
        GameObject instance,
        float footprint,
        float height)
    {
        if (_oversizedWildernessWarningLogs >=
            MaxOversizedWildernessWarningLogs)
        {
            return;
        }

        _oversizedWildernessWarningLogs++;

        Debug.LogWarning(
            "[YQGeneratedWorldEnvironment] " +
            "Rejected oversized " +
            label +
            ": " +
            (instance != null
                ? instance.name
                : "<null>") +
            " | footprint=" +
            footprint.ToString("0.0") +
            " height=" +
            height.ToString("0.0"));

        if (_oversizedWildernessWarningLogs ==
            MaxOversizedWildernessWarningLogs)
        {
            // note: One final warning tells us the cap activated without logging every repeated bad candidate.
            Debug.LogWarning(
                "[YQGeneratedWorldEnvironment] " +
                "Further oversized wilderness rejection warnings suppressed for this domain reload.");
        }
    }

    private static void RemoveWildernessCollision(
        GameObject root)
    {
        if (root == null)
            return;

        Collider[] colliders =
            root.GetComponentsInChildren<
                Collider>(
                    true);

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            Collider collider =
                colliders[i];

            if (collider == null)
                continue;

            collider.enabled =
                false;

            UnityEngine.Object.Destroy(
                collider);
        }

        Rigidbody[] bodies =
            root.GetComponentsInChildren<
                Rigidbody>(
                    true);

        for (int i = 0;
             i < bodies.Length;
             i++)
        {
            Rigidbody body =
                bodies[i];

            if (body == null)
                continue;

            body.detectCollisions =
                false;

            body.isKinematic =
                true;

            UnityEngine.Object.Destroy(
                body);
        }
    }

    private static void GroundWildernessInstance(
        GameObject instance,
        Terrain terrain,
        string slot,
        GeneratedAssetReferenceRecord reference)
    {
        if (instance == null ||
            terrain == null ||
            YQTerrainSupportComposer.IsExplicitlySuspended(
                instance))
        {
            return;
        }

        if (string.Equals(
                slot,
                YQWorldAssetCatalog
                    .SlotVegetation,
                StringComparison.OrdinalIgnoreCase) &&
            LooksLikeTree(
                instance,
                reference) &&
            TryGetTreeTrunkBounds(
                instance,
                out Bounds trunkBounds))
        {
            if (YQGeneratedWorldTerrain.TrySampleFootprintHeight(
                    terrain,
                    trunkBounds,
                    out float trunkTerrainContact,
                    out _,
                    out _))
            {
                // note: Tree contact follows the visible trunk footprint rather than an arbitrary imported root pivot or the canopy bounds.
                Vector3 treePosition =
                    instance.transform.position;
                treePosition.y +=
                    trunkTerrainContact -
                    trunkBounds.min.y -
                    0.05f;
                instance.transform.position =
                    treePosition;

                // note: Tree trunks remain upright and use their root geometry as the explicit contact authority.
                return;
            }
        }

        YQGeneratedWorldPlacementCategory category =
            string.Equals(
                slot,
                YQWorldAssetCatalog.SlotRock,
                StringComparison.OrdinalIgnoreCase)
                ? YQGeneratedWorldPlacementCategory.Rock
                : string.Equals(
                    slot,
                    YQWorldAssetCatalog.SlotVegetation,
                    StringComparison.OrdinalIgnoreCase)
                    ? YQGeneratedWorldPlacementCategory.Vegetation
                    : YQGeneratedWorldPlacementCategory.Prop;

        // note: Ordinary wilderness props enter the same category-aware placement gate, giving rocks and low vegetation bounded natural tilt plus visible-bottom contact.
        YQGeneratedWorldTerrain.TryPlaceGroundedObject(
            instance,
            terrain,
            category,
            0.05f,
            out _);
    }

    // ============================================================
    // STRUCTURAL FEATURE GROUNDING / COLLISION
    // ============================================================

    private static void PrepareStaticStructure(
        GameObject root)
    {
        if (root == null)
            return;

        Rigidbody[] bodies =
            root.GetComponentsInChildren<
                Rigidbody>(
                    true);

        for (int i = 0;
             i < bodies.Length;
             i++)
        {
            Rigidbody body =
                bodies[i];

            if (body == null)
                continue;

            if (!body.isKinematic)
            {
                // note: Static structures may arrive already kinematic, so only dynamic bodies are zeroed.
                body.linearVelocity =
                    Vector3.zero;

                body.angularVelocity =
                    Vector3.zero;
            }

            body.useGravity =
                false;

            body.isKinematic =
                true;
        }
    }

    private static bool ValidateStructuralTerrainFootprint(
        Terrain terrain,
        Bounds bounds)
    {
        if (terrain == null)
            return false;

        float radiusX =
            Mathf.Clamp(
                bounds.size.x *
                0.30f,
                2f,
                20f);

        float radiusZ =
            Mathf.Clamp(
                bounds.size.z *
                0.30f,
                2f,
                20f);

        Vector3 center =
            bounds.center;

        Vector3[] points =
        {
            center,

            new Vector3(
                center.x +
                    radiusX,
                0f,
                center.z),

            new Vector3(
                center.x -
                    radiusX,
                0f,
                center.z),

            new Vector3(
                center.x,
                0f,
                center.z +
                    radiusZ),

            new Vector3(
                center.x,
                0f,
                center.z -
                    radiusZ)
        };

        float minimum =
            float.MaxValue;

        float maximum =
            float.MinValue;

        for (int i = 0;
             i < points.Length;
             i++)
        {
            if (!InsideTerrain(
                    terrain,
                    points[i]))
            {
                return false;
            }

            float y =
                YQGeneratedWorldTerrain
                    .SampleWorldHeight(
                        terrain,
                        points[i]);

            minimum =
                Mathf.Min(
                    minimum,
                    y);

            maximum =
                Mathf.Max(
                    maximum,
                    y);
        }

        float permittedVariation =
            Mathf.Max(
                4.5f,
                bounds.size.y *
                0.35f);

        return
            maximum -
            minimum <=
            permittedVariation;
    }

    private static void GroundStructuralFeature(
        GameObject instance,
        Terrain terrain,
        float penetrationRatio)
    {
        if (instance == null ||
            terrain == null ||
            !YQGeneratedWorldTerrain.TryGetStableContactGeometry(
                instance,
                out Bounds bounds,
                out _))
        {
            return;
        }

        float penetration =
            Mathf.Clamp(
                bounds.size.y *
                Mathf.Max(
                    0f,
                    penetrationRatio),
                0.03f,
                1.75f);

        // note: Caves and large wilderness structures share the same nine-point terrain contact contract as every other grounded generated asset.
        YQGeneratedWorldTerrain.TryPlaceGroundedObject(
            instance,
            terrain,
            YQGeneratedWorldPlacementCategory.Structure,
            penetration,
            out _);
    }

    private static void EnsureStaticStructuralCollision(
        GameObject root)
    {
        if (root == null)
            return;

        Collider[] existing =
            root.GetComponentsInChildren<
                Collider>(
                    true);

        bool hasSolid =
            false;

        for (int i = 0;
             i < existing.Length;
             i++)
        {
            Collider collider =
                existing[i];

            if (collider != null &&
                collider.enabled &&
                !collider.isTrigger)
            {
                hasSolid =
                    true;

                break;
            }
        }

        if (hasSolid)
            return;

        MeshFilter[] meshes =
            root.GetComponentsInChildren<
                MeshFilter>(
                    true);

        for (int i = 0;
             i < meshes.Length;
             i++)
        {
            MeshFilter filter =
                meshes[i];

            if (filter == null ||
                filter.sharedMesh == null)
            {
                continue;
            }

            MeshCollider existingMesh =
                filter.GetComponent<
                    MeshCollider>();

            if (existingMesh != null)
            {
                existingMesh.enabled =
                    true;

                existingMesh.isTrigger =
                    false;

                existingMesh.convex =
                    false;

                continue;
            }

            MeshCollider collider =
                filter.gameObject
                    .AddComponent<
                        MeshCollider>();

            collider.sharedMesh =
                filter.sharedMesh;

            collider.convex =
                false;

            collider.isTrigger =
                false;
        }
    }

    private static void EnsureSimpleSolidCollider(
        GameObject root)
    {
        if (root == null)
            return;

        Collider[] existing =
            root.GetComponentsInChildren<
                Collider>(
                    true);

        for (int i = 0;
             i < existing.Length;
             i++)
        {
            if (existing[i] != null &&
                existing[i].enabled &&
                !existing[i].isTrigger)
            {
                return;
            }
        }

        if (!TryGetWildernessBounds(
                root,
                out Bounds bounds))
        {
            return;
        }

        BoxCollider collider =
            root.AddComponent<
                BoxCollider>();

        collider.center =
            root.transform
                .InverseTransformPoint(
                    bounds.center);

        Vector3 scale =
            root.transform.lossyScale;

        collider.size =
            new Vector3(
                bounds.size.x /
                    Mathf.Max(
                        0.001f,
                        Mathf.Abs(
                            scale.x)),
                bounds.size.y /
                    Mathf.Max(
                        0.001f,
                        Mathf.Abs(
                            scale.y)),
                bounds.size.z /
                    Mathf.Max(
                        0.001f,
                        Mathf.Abs(
                            scale.z)));

        collider.isTrigger =
            false;
    }

    private static List<Collider> DisableMalformedPrefabPrimitiveColliders(
        GameObject prefab)
    {
        List<Collider> disabled =
            new List<Collider>();

        if (prefab == null)
            return disabled;

        Collider[] colliders =
            prefab.GetComponentsInChildren<Collider>(
                true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null ||
                !collider.enabled ||
                !(collider is BoxCollider ||
                  collider is SphereCollider ||
                  collider is CapsuleCollider))
            {
                continue;
            }

            bool negativeBoxSize =
                collider is BoxCollider box &&
                HasNegativeBoxColliderSize(
                    box);

            bool mirroredHierarchy =
                HasMirroredScaleInHierarchy(
                    collider.transform,
                    prefab.transform);

            if (!negativeBoxSize &&
                !mirroredHierarchy)
            {
                continue;
            }

            // note: Cloning with the vendor primitive disabled prevents Unity's negative-size warning; generated structural collision replaces it on the runtime instance.
            collider.enabled =
                false;

            disabled.Add(
                collider);
        }

        return disabled;
    }

    private static bool HasNegativeBoxColliderSize(
        BoxCollider collider)
    {
        if (collider == null)
            return false;

        Vector3 size =
            collider.size;

        return
            size.x < 0f ||
            size.y < 0f ||
            size.z < 0f;
    }

    private static bool HasMirroredScaleInHierarchy(
        Transform child,
        Transform root)
    {
        Transform current =
            child;

        while (current != null)
        {
            Vector3 localScale =
                current.localScale;

            // note: Unity rejects a primitive collider when any transform in its chain is mirrored, even if a second negative scale makes the final product positive.
            if (localScale.x < 0f ||
                localScale.y < 0f ||
                localScale.z < 0f)
            {
                return true;
            }

            if (current == root)
                break;

            current =
                current.parent;
        }

        return false;
    }

    private static void RestorePrefabColliders(
        List<Collider> colliders)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = true;
        }
    }

    // ============================================================
    // CHARACTER VISUAL PREP
    // ============================================================

    private static bool TryNormalizeMonsterVisualEnvelope(
        GameObject root,
        float targetHeight,
        string resolvedCategory)
    {
        if (root == null ||
            targetHeight <= 0f ||
            !TryGetWildernessBounds(
                root,
                out Bounds bounds))
        {
            return false;
        }

        float currentHeight =
            bounds.size.y;

        if (currentHeight <= 0.001f ||
            float.IsNaN(currentHeight) ||
            float.IsInfinity(currentHeight))
        {
            return false;
        }

        bool humanoid =
            IsHumanoidVisualCategory(
                resolvedCategory);

        float horizontalAspect =
            Mathf.Max(
                bounds.size.x,
                bounds.size.z) /
            currentHeight;

        if (humanoid &&
            horizontalAspect > 2.25f)
        {
            // note: A humanoid wilderness family with a winged or giant silhouette is rejected as a semantic mismatch.
            return false;
        }

        float maximumWidthFactor;
        float maximumDepthFactor;

        if (humanoid)
        {
            maximumWidthFactor = 1.25f;
            maximumDepthFactor = 0.95f;
        }
        else if (string.Equals(
                     resolvedCategory,
                     YQRuntimeCreatureAssetIndex.Dragon,
                     StringComparison.OrdinalIgnoreCase))
        {
            maximumWidthFactor = 5f;
            maximumDepthFactor = 4f;
        }
        else if (string.Equals(
                     resolvedCategory,
                     YQRuntimeCreatureAssetIndex.WormMonster,
                     StringComparison.OrdinalIgnoreCase))
        {
            maximumWidthFactor = 3.5f;
            maximumDepthFactor = 4f;
        }
        else if (string.Equals(
                     resolvedCategory,
                     YQRuntimeCreatureAssetIndex.Spider,
                     StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                     resolvedCategory,
                     YQRuntimeCreatureAssetIndex.Beast,
                     StringComparison.OrdinalIgnoreCase))
        {
            maximumWidthFactor = 3f;
            maximumDepthFactor = 3f;
        }
        else
        {
            maximumWidthFactor = 2.2f;
            maximumDepthFactor = 2.2f;
        }

        float maximumWidth =
            targetHeight *
            maximumWidthFactor;

        float maximumDepth =
            targetHeight *
            maximumDepthFactor;

        float multiplier =
            targetHeight /
            currentHeight;

        if (bounds.size.x > 0.001f)
        {
            multiplier =
                Mathf.Min(
                    multiplier,
                    maximumWidth /
                    bounds.size.x);
        }

        if (bounds.size.z > 0.001f)
        {
            multiplier =
                Mathf.Min(
                    multiplier,
                    maximumDepth /
                    bounds.size.z);
        }

        if (float.IsNaN(multiplier) ||
            float.IsInfinity(multiplier) ||
            multiplier <= 0f)
        {
            return false;
        }

        // note: Uniform fitting preserves authored proportions while enforcing a gameplay-safe height, width, and depth envelope.
        root.transform.localScale *=
            Mathf.Clamp(
                multiplier,
                0.01f,
                4.5f);

        if (!TryGetWildernessBounds(
                root,
                out Bounds fittedBounds))
        {
            return false;
        }

        const float EnvelopeTolerance = 1.08f;

        return
            fittedBounds.size.y <= targetHeight * EnvelopeTolerance &&
            fittedBounds.size.x <= maximumWidth * EnvelopeTolerance &&
            fittedBounds.size.z <= maximumDepth * EnvelopeTolerance;
    }

    private static void GroundCharacterToTerrain(
        GameObject root,
        Terrain terrain,
        Vector3 anchor)
    {
        if (root == null ||
            terrain == null)
        {
            return;
        }

        if (!YQGeneratedWorldTerrain.TryGetStableContactGeometry(
                root,
                out _,
                out _))
        {
            Vector3 position =
                root.transform.position;

            position.y =
                YQGeneratedWorldTerrain.SampleWorldHeight(
                    terrain,
                    anchor);

            root.transform.position =
                position;

            return;
        }

        // note: Grounded creatures use their visible lower band and the surrounding terrain footprint; the old positive offset deliberately left feet hovering.
        YQGeneratedWorldTerrain.TryPlaceGroundedObject(
            root,
            terrain,
            YQGeneratedWorldPlacementCategory.Actor,
            0.005f,
            out _);
    }

    private static void EnsureCharacterCollider(
        GameObject root)
    {
        if (root == null)
            return;

        Collider[] colliders =
            root.GetComponentsInChildren<
                Collider>(
                    true);

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            if (colliders[i] != null &&
                !colliders[i].isTrigger)
            {
                return;
            }
        }

        if (!TryGetWildernessBounds(
                root,
                out Bounds bounds))
        {
            CapsuleCollider fallback =
                root.AddComponent<
                    CapsuleCollider>();

            fallback.center =
                new Vector3(
                    0f,
                    0.9f,
                    0f);

            fallback.height =
                1.8f;

            fallback.radius =
                0.35f;

            return;
        }

        Vector3 scale =
            root.transform.lossyScale;

        float horizontalScale =
            Mathf.Max(
                0.001f,
                Mathf.Max(
                    Mathf.Abs(
                        scale.x),
                    Mathf.Abs(
                        scale.z)));

        float verticalScale =
            Mathf.Max(
                0.001f,
                Mathf.Abs(
                    scale.y));

        CapsuleCollider capsule =
            root.AddComponent<
                CapsuleCollider>();

        capsule.center =
            root.transform
                .InverseTransformPoint(
                    bounds.center);

        capsule.height =
            Mathf.Max(
                0.5f,
                bounds.size.y /
                verticalScale);

        capsule.radius =
            Mathf.Max(
                0.12f,
                Mathf.Min(
                    bounds.size.x,
                    bounds.size.z) *
                0.30f /
                horizontalScale);

        capsule.isTrigger =
            false;
    }

    // ============================================================
    // BOUNDS
    // ============================================================

    private static bool TryGetWildernessBounds(
        GameObject root,
        out Bounds bounds)
    {
        bounds =
            default;

        if (root == null)
            return false;

        Renderer[] renderers =
            root.GetComponentsInChildren<
                Renderer>(
                    true);

        bool initialized =
            false;

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer renderer =
                renderers[i];

            if (renderer == null ||
                renderer is
                    ParticleSystemRenderer)
            {
                continue;
            }

            Bounds rendererBounds =
                renderer.bounds;

            if (rendererBounds.size
                    .sqrMagnitude <=
                0.0001f)
            {
                continue;
            }

            if (!initialized)
            {
                bounds =
                    rendererBounds;

                initialized =
                    true;
            }
            else
            {
                bounds.Encapsulate(
                    rendererBounds);
            }
        }

        return initialized;
    }

    private static bool TryGetTreeTrunkBounds(
        GameObject root,
        out Bounds bounds)
    {
        bounds =
            default;

        if (root == null)
            return false;

        Renderer[] renderers =
            root.GetComponentsInChildren<
                Renderer>(
                    true);

        bool initialized =
            false;

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer renderer =
                renderers[i];

            if (renderer == null ||
                renderer is
                    ParticleSystemRenderer)
            {
                continue;
            }

            string name =
                renderer.name != null
                    ? renderer.name
                        .ToLowerInvariant()
                    : string.Empty;

            bool trunkLike =
                name.Contains("trunk") ||
                name.Contains("bark") ||
                name.Contains("stem") ||
                name.Contains("stump");

            if (!trunkLike)
                continue;

            if (!initialized)
            {
                bounds =
                    renderer.bounds;

                initialized =
                    true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds);
            }
        }

        return initialized;
    }

    // ============================================================
    // TREE / ROCK COLLISION
    // ============================================================

    private static bool LooksLikeTree(
        GameObject instance,
        GeneratedAssetReferenceRecord reference)
    {
        string text =
            string.Empty;

        if (instance != null)
        {
            text +=
                " " +
                instance.name;
        }

        if (reference != null)
        {
            text +=
                " " +
                reference.assetPath;

            if (reference.subTags != null)
            {
                for (int i = 0;
                     i < reference.subTags.Count;
                     i++)
                {
                    text +=
                        " " +
                        reference.subTags[i];
                }
            }
        }

        text =
            text.ToLowerInvariant();

        if (text.Contains("bush") ||
            text.Contains("grass") ||
            text.Contains("fern") ||
            text.Contains("weed") ||
            text.Contains("flower") ||
            text.Contains("shroom") ||
            text.Contains("mushroom"))
        {
            return false;
        }

        return
            text.Contains("tree") ||
            text.Contains("trunk") ||
            text.Contains("conifer") ||
            text.Contains("alder") ||
            text.Contains("pine") ||
            text.Contains("oak");
    }

    private static void AddTreeTrunkCollision(
        GameObject root,
        Terrain terrain,
        Bounds fullBounds)
    {
        if (root == null ||
            terrain == null)
        {
            return;
        }

        Bounds trunkBounds =
            fullBounds;

        if (TryGetTreeTrunkBounds(
                root,
                out Bounds detectedTrunk))
        {
            trunkBounds =
                detectedTrunk;
        }

        float worldRadius =
            Mathf.Clamp(
                Mathf.Min(
                    trunkBounds.size.x,
                    trunkBounds.size.z) *
                0.32f,
                0.22f,
                0.70f);

        float worldHeight =
            Mathf.Clamp(
                trunkBounds.size.y *
                0.55f,
                1.4f,
                4.5f);

        Vector3 trunkWorldCenter =
            trunkBounds.center;

        float terrainY =
            YQGeneratedWorldTerrain
                .SampleWorldHeight(
                    terrain,
                    trunkWorldCenter);

        trunkWorldCenter.y =
            terrainY +
            worldHeight *
                0.5f;

        Vector3 localCenter =
            root.transform
                .InverseTransformPoint(
                    trunkWorldCenter);

        Vector3 scale =
            root.transform.lossyScale;

        float horizontalScale =
            Mathf.Max(
                0.001f,
                Mathf.Max(
                    Mathf.Abs(
                        scale.x),
                    Mathf.Abs(
                        scale.z)));

        float verticalScale =
            Mathf.Max(
                0.001f,
                Mathf.Abs(
                    scale.y));

        CapsuleCollider collider =
            root.AddComponent<
                CapsuleCollider>();

        collider.direction =
            1;

        collider.center =
            localCenter;

        collider.radius =
            worldRadius /
            horizontalScale;

        collider.height =
            Mathf.Max(
                collider.radius *
                    2f,
                worldHeight /
                    verticalScale);

        collider.isTrigger =
            false;
    }

    private static void AddTraversalSafeRockCollision(
        GameObject root,
        Terrain terrain,
        Bounds bounds)
    {
        if (root == null ||
            terrain == null)
        {
            return;
        }

        float horizontalFootprint = Mathf.Max(
            bounds.size.x,
            bounds.size.z);
        if (bounds.size.y < 1.25f || horizontalFootprint < 1.8f)
        {
            // note: Ankle- and knee-height rocks are visual terrain dressing; giving them solid capsules created random locomotion barriers on otherwise walkable ground.
            return;
        }

        float horizontalSize =
            Mathf.Min(
                bounds.size.x,
                bounds.size.z);

        float worldRadius =
            Mathf.Clamp(
                horizontalSize *
                0.32f,
                0.16f,
                1.45f);

        float worldHeight =
            Mathf.Clamp(
                bounds.size.y *
                0.72f,
                worldRadius *
                    2f,
                3.0f);

        Vector3 worldCenter =
            bounds.center;

        float terrainY =
            YQGeneratedWorldTerrain
                .SampleWorldHeight(
                    terrain,
                    worldCenter);

        worldCenter.y =
            terrainY +
            worldHeight *
                0.5f;

        Vector3 localCenter =
            root.transform
                .InverseTransformPoint(
                    worldCenter);

        Vector3 scale =
            root.transform.lossyScale;

        float horizontalScale =
            Mathf.Max(
                0.001f,
                Mathf.Max(
                    Mathf.Abs(
                        scale.x),
                    Mathf.Abs(
                        scale.z)));

        float verticalScale =
            Mathf.Max(
                0.001f,
                Mathf.Abs(
                    scale.y));

        CapsuleCollider collider =
            root.AddComponent<
                CapsuleCollider>();

        collider.direction =
            1;

        collider.center =
            localCenter;

        collider.radius =
            worldRadius /
            horizontalScale;

        collider.height =
            Mathf.Max(
                collider.radius *
                    2f,
                worldHeight /
                    verticalScale);

        collider.isTrigger =
            false;
    }

    // ============================================================
    // LARGE/BACKDROP ASSET CLASSIFICATION
    // ============================================================

    /*
     * Public because YQGeneratedWorldRuntimeBuilder settlement-edge
     * vegetation also draws from SlotRock and must obey the same rule.
     */
    public static bool IsLargeTerrainFeatureReference(
        GeneratedAssetReferenceRecord reference)
    {
        if (reference == null)
            return false;

        string semantic =
            BuildReferenceSemanticText(
                reference);

        return
            ContainsAnySemantic(
                semantic,
                "mountain",
                "mountains",
                "mountainpiece",
                "backdrop",
                "vista",
                "distant",
                "massif",
                "mesa",
                "cliff");
    }

    private static string BuildReferenceSemanticText(
        GeneratedAssetReferenceRecord reference)
    {
        if (reference == null)
            return string.Empty;

        StringBuilder sb =
            new StringBuilder();

        sb.Append(
            SafeText(
                reference.assetPath,
                string.Empty));

        sb.Append(" ");

        sb.Append(
            SafeText(
                reference.slotTag,
                string.Empty));

        if (reference.styleTags != null)
        {
            for (int i = 0;
                 i < reference.styleTags.Count;
                 i++)
            {
                sb.Append(" ");

                sb.Append(
                    reference.styleTags[i]);
            }
        }

        if (reference.subTags != null)
        {
            for (int i = 0;
                 i < reference.subTags.Count;
                 i++)
            {
                sb.Append(" ");

                sb.Append(
                    reference.subTags[i]);
            }
        }

        return
            NormalizeSemanticText(
                sb.ToString());
    }

    private static string BuildReferenceIdentitySemanticText(
        GeneratedAssetReferenceRecord reference)
    {
        if (reference == null)
            return string.Empty;

        StringBuilder sb =
            new StringBuilder();

        sb.Append(
            SafeText(
                reference.assetPath,
                string.Empty));

        if (reference.styleTags != null)
        {
            for (int index = 0;
                 index < reference.styleTags.Count;
                 index++)
            {
                sb.Append(" ");
                sb.Append(reference.styleTags[index]);
            }
        }

        if (reference.subTags != null)
        {
            for (int index = 0;
                 index < reference.subTags.Count;
                 index++)
            {
                sb.Append(" ");
                sb.Append(reference.subTags[index]);
            }
        }

        // note: Identity checks deliberately exclude slotTag because a mistaken "rock" slot assignment must not make a statue or machine count as stone.
        return NormalizeSemanticText(
            sb.ToString());
    }

    // ============================================================
    // EXCLUSION / LOOKUPS
    // ============================================================

    private static bool NearAnySettlement(
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        Vector3 position,
        float radius)
    {
        if (plan == null ||
            plan.settlements == null)
        {
            return false;
        }

        float radiusSquared =
            radius *
            radius;

        for (int i = 0;
             i < plan.settlements.Count;
             i++)
        {
            GeneratedSettlementRecord settlement =
                plan.settlements[i];

            if (settlement == null)
                continue;

            Vector3 settlementPosition =
                YQGeneratedWorldLayout
                    .GetSettlementAnchor(
                        plan,
                        settlement,
                        terrain);

            float dx =
                position.x -
                settlementPosition.x;

            float dz =
                position.z -
                settlementPosition.z;

            if (dx *
                    dx +
                dz *
                    dz <=
                radiusSquared)
            {
                return true;
            }
        }

        return false;
    }

    private static bool NearAnyEncampment(
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        Vector3 position,
        float radius)
    {
        if (plan == null ||
            plan.encampments == null)
        {
            return false;
        }

        float radiusSquared =
            radius *
            radius;

        for (int i = 0;
             i < plan.encampments.Count;
             i++)
        {
            GeneratedEncampmentRecord encampment =
                plan.encampments[i];

            if (encampment == null)
                continue;

            Vector3 campPosition =
                YQGeneratedWorldLayout
                    .GetEncampmentAnchor(
                        plan,
                        encampment,
                        terrain);

            float dx =
                position.x -
                campPosition.x;

            float dz =
                position.z -
                campPosition.z;

            if (dx *
                    dx +
                dz *
                    dz <=
                radiusSquared)
            {
                return true;
            }
        }

        return false;
    }

    private static bool InsideOriginReserve(
        Vector3 position,
        float radius)
    {
        float distanceSquared =
            position.x *
                position.x +
            position.z *
                position.z;

        return
            distanceSquared <
            radius *
            radius;
    }

    private static bool InsideTerrain(
        Terrain terrain,
        Vector3 position)
    {
        return
            InsideTerrainWithMargin(
                terrain,
                position,
                0f);
    }

    private static bool InsideTerrainWithMargin(
        Terrain terrain,
        Vector3 position,
        float margin)
    {
        if (terrain == null ||
            terrain.terrainData == null)
        {
            return false;
        }

        Vector3 origin =
            terrain.transform.position;

        Vector3 size =
            terrain.terrainData.size;

        float safeMargin =
            Mathf.Max(
                0f,
                margin);

        return
            position.x >=
                origin.x +
                safeMargin &&
            position.x <=
                origin.x +
                size.x -
                safeMargin &&
            position.z >=
                origin.z +
                safeMargin &&
            position.z <=
                origin.z +
                size.z -
                safeMargin;
    }

    private static GeneratedRegionAssetPaletteRecord
        FindPalette(
            GeneratedWorldPlanRecord plan,
            GeneratedRegionRecord region)
    {
        if (plan == null ||
            region == null ||
            plan.assetPalettes == null)
        {
            return null;
        }

        for (int i = 0;
             i < plan.assetPalettes.Count;
             i++)
        {
            GeneratedRegionAssetPaletteRecord palette =
                plan.assetPalettes[i];

            if (palette == null)
                continue;

            if (!string.IsNullOrWhiteSpace(
                    region.assetPaletteId) &&
                string.Equals(
                    palette.paletteId,
                    region.assetPaletteId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return palette;
            }

            if (string.Equals(
                    palette.regionId,
                    region.regionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return palette;
            }
        }

        return null;
    }

    // ============================================================
    // MATERIAL TEXTURES
    // ============================================================

    private static Texture2D FindTexture(
        Material material,
        params string[] properties)
    {
        if (material == null ||
            properties == null)
        {
            return null;
        }

        for (int i = 0;
             i < properties.Length;
             i++)
        {
            string property =
                properties[i];

            if (string.IsNullOrWhiteSpace(
                    property) ||
                !material.HasProperty(
                    property))
            {
                continue;
            }

            try
            {
                Texture texture =
                    material.GetTexture(
                        property);

                if (texture is
                    Texture2D texture2D)
                {
                    return texture2D;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    // ============================================================
    // SEMANTIC TEXT
    // ============================================================

    private static string BuildRegionSemanticText(
        GeneratedRegionRecord region)
    {
        if (region == null)
            return string.Empty;

        StringBuilder sb =
            new StringBuilder();

        sb.Append(
            SafeText(
                region.displayName,
                string.Empty));

        sb.Append(" ");

        sb.Append(
            SafeText(
                region.role,
                string.Empty));

        sb.Append(" ");

        sb.Append(
            SafeText(
                region.terrainProfile,
                string.Empty));

        sb.Append(" ");

        sb.Append(
            SafeText(
                region.climateProfile,
                string.Empty));

        sb.Append(" ");

        sb.Append(
            SafeText(
                region.lore,
                string.Empty));

        sb.Append(" ");

        sb.Append(
            SafeText(
                region.playerPressure,
                string.Empty));

        return
            NormalizeSemanticText(
                sb.ToString());
    }

    private static string NormalizeSemanticText(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        StringBuilder sb =
            new StringBuilder(
                value.Length *
                2);

        char previous =
            '\0';

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                value[i];

            if (!char.IsLetterOrDigit(
                    c))
            {
                sb.Append(' ');

                previous =
                    c;

                continue;
            }

            if (char.IsUpper(c) &&
                i > 0 &&
                (char.IsLower(
                     previous) ||
                 char.IsDigit(
                     previous)))
            {
                sb.Append(' ');
            }

            sb.Append(
                char.ToLowerInvariant(
                    c));

            previous =
                c;
        }

        string[] pieces =
            sb.ToString()
                .Split(
                    new[]
                    {
                        ' '
                    },
                    StringSplitOptions
                        .RemoveEmptyEntries);

        return
            " " +
            string.Join(
                " ",
                pieces) +
            " ";
    }

    private static bool ContainsSemantic(
        string normalizedText,
        string term)
    {
        if (string.IsNullOrWhiteSpace(
                normalizedText) ||
            string.IsNullOrWhiteSpace(
                term))
        {
            return false;
        }

        string normalizedTerm =
            NormalizeSemanticText(
                term)
                .Trim();

        if (string.IsNullOrWhiteSpace(
                normalizedTerm))
        {
            return false;
        }

        if (normalizedText.Contains(
                " " +
                normalizedTerm +
                " "))
        {
            return true;
        }

        if (!normalizedTerm.Contains(" "))
        {
            if (normalizedText.Contains(
                    " " +
                    normalizedTerm +
                    "s "))
            {
                return true;
            }

            if (normalizedText.Contains(
                    " " +
                    normalizedTerm +
                    "es "))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAnySemantic(
        string normalizedText,
        params string[] terms)
    {
        if (terms == null)
            return false;

        for (int i = 0;
             i < terms.Length;
             i++)
        {
            if (ContainsSemantic(
                    normalizedText,
                    terms[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> ExtractSemanticTerms(
        string value)
    {
        List<string> result =
            new List<string>();

        string normalized =
            NormalizeSemanticText(
                value);

        string[] parts =
            normalized.Split(
                new[]
                {
                    ' '
                },
                StringSplitOptions
                    .RemoveEmptyEntries);

        for (int i = 0;
             i < parts.Length;
             i++)
        {
            string term =
                parts[i];

            if (term.Length <
                3)
            {
                continue;
            }

            switch (term)
            {
                case "the":
                case "and":
                case "from":
                case "with":
                case "of":
                case "hostile":
                case "enemy":
                case "enemies":
                    continue;
            }

            AddUnique(
                result,
                term);
        }

        return result;
    }

    private static int CountSemanticMatches(
        string normalizedText,
        List<string> terms)
    {
        if (terms == null)
            return 0;

        int count =
            0;

        for (int i = 0;
             i < terms.Count;
             i++)
        {
            if (ContainsSemantic(
                    normalizedText,
                    terms[i]))
            {
                count++;
            }
        }

        return count;
    }

    // ============================================================
    // DETERMINISM
    // ============================================================

    private static float Deterministic01(
        string seed)
    {
        uint hash =
            StableHash32(
                seed);

        return
            (hash &
                0x00FFFFFFu) /
            16777215f;
    }

    private static float ResolveGridCell01(
        uint seedHash,
        int x,
        int z)
    {
        uint hash = seedHash;
        hash ^= (uint)x * 0x9E3779B9u;
        hash ^= (uint)z * 0x85EBCA6Bu;
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        hash *= 0x846CA68Bu;
        hash ^= hash >> 16;

        // note: Numeric grid hashing preserves deterministic foliage selection without allocating one composite seed string for every accepted detail cell.
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    private static uint StableHash32(
        string value)
    {
        const uint offsetBasis =
            2166136261u;

        const uint prime =
            16777619u;

        uint hash =
            offsetBasis;

        if (value == null)
            return hash;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                value[i];

            hash ^=
                (byte)(
                    c &
                    0xFF);

            hash *=
                prime;

            hash ^=
                (byte)(
                    (c >> 8) &
                    0xFF);

            hash *=
                prime;
        }

        return hash;
    }

    // ============================================================
    // STRINGS
    // ============================================================

    private static void AddUnique(
        List<string> values,
        string value)
    {
        if (values == null ||
            string.IsNullOrWhiteSpace(
                value))
        {
            return;
        }

        for (int i = 0;
             i < values.Count;
             i++)
        {
            if (string.Equals(
                    values[i],
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        values.Add(
            value);
    }

    private static string NormalizeTag(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        char[] chars =
            value.Trim()
                .ToLowerInvariant()
                .ToCharArray();

        for (int i = 0;
             i < chars.Length;
             i++)
        {
            if (!char.IsLetterOrDigit(
                    chars[i]))
            {
                chars[i] =
                    '_';
            }
        }

        return
            new string(
                chars)
                .Trim('_');
    }

    private static string SafeName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return "Generated";
        }

        char[] chars =
            value.Trim()
                .ToCharArray();

        for (int i = 0;
             i < chars.Length;
             i++)
        {
            if (!char.IsLetterOrDigit(
                    chars[i]) &&
                chars[i] != '_' &&
                chars[i] != '-')
            {
                chars[i] =
                    '_';
            }
        }

        return
            new string(
                chars);
    }

    private static string SafeText(
        string value,
        string fallback)
    {
        return
            string.IsNullOrWhiteSpace(
                value)
                ? fallback
                : value.Trim();
    }
}
