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
        24;

    private const int BaseRockScatterPerRegion =
        10;

    private const int BaseLandformsPerRegion =
        3;

    private const int BaseAmbientEncounterGroupsPerRegion =
        1;

    private const int BaseTreasurePerRegion =
        2;

    private const int MaxOversizedWildernessWarningLogs =
        8;

    private static int _oversizedWildernessWarningLogs;

    private const float WildernessRadiusMin =
        34f;

    private const float WildernessRadiusMax =
        215f;

    private const float LandformRadiusMin =
        68f;

    private const float LandformRadiusMax =
        220f;

    private const float SettlementClearRadius =
        34f;

    private const float SettlementLandformClearRadius =
        62f;

    private const float OriginClearRadius =
        64f;

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

        public int terrainLayerIndex;
    }

    private sealed class WildernessBuildStats
    {
        public int vegetation;

        public int rocks;

        public int caves;

        public int ambientEnemies;

        public int treasure;
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
        YQRuntimeWorldAssetRegistry registry)
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

        List<RegionSurface> surfaces =
            null;

        yield return ApplyRegionalTerrainLayersRoutine(
            terrain,
            plan,
            registry,
            result => surfaces = result);

        surfaces ??=
            new List<RegionSurface>();

        yield return null;

        Debug.Log(
            "[YQGeneratedWorldEnvironment] TERRAIN FOUNDATION READY\n" +
            "Terrain layers: " +
            (terrain.terrainData.terrainLayers != null
                ? terrain.terrainData.terrainLayers.Length
                : 0) +
            "\nRegion surface mappings: " + surfaces.Count +
            "\nRegions: " + plan.regions.Count +
            "\nTerrain hills/mountains stamped: " + landforms);
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

        // note: Wilderness families are committed across rendered frames instead of cloning every region's scenery in one loading-frame burst.
        yield return BuildRegionalWildernessRoutine(
            parent,
            terrain,
            plan,
            registry,
            result => stats = result);

        stats ??=
            new WildernessBuildStats();

        // note: Wilderness grounding uses terrain samples and renderer bounds; colliders can join the next normal physics step instead of forcing a full-scene loading synchronization.
        yield return null;

        Debug.Log(
            "[YQGeneratedWorldEnvironment] WILDERNESS READY\n" +
            "Vegetation spawned: " +
            stats.vegetation +
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
                4;

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

                ApplyTerrainLandformStamp(
                    terrain,
                    heights,
                    center,
                    radius,
                    amplitude,
                    axisRatio,
                    rotation);

                regionStamped++;

                stamped++;

                // note: One terrain landform is the largest indivisible authored unit; yield immediately afterward to preserve the loading presentation heartbeat.
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
                    38f +
                        danger *
                        1.5f,
                    68f +
                        danger *
                        2.2f,
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
                    8f +
                        danger *
                        0.7f,
                    18f +
                        danger *
                        1.25f,
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
                0.20f);
    }

    private static void ApplyTerrainLandformStamp(
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
            return;
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

            GeneratedAssetReferenceRecord terrainReference =
                FindResolvableTerrainMaterial(
                    palette,
                    registry);

            if (terrainReference == null)
            {
                Debug.LogWarning(
                    "[YQGeneratedWorldEnvironment] " +
                    "No runtime terrain material resolved for region: " +
                    region.displayName);

                continue;
            }

            string materialPath =
                YQRuntimeWorldAssetRegistry
                    .NormalizePath(
                        terrainReference.assetPath);

            int layerIndex;

            if (!layerByMaterialPath.TryGetValue(
                    materialPath,
                    out layerIndex))
            {
                Material material =
                    registry.ResolveMaterial(
                        materialPath);

                TerrainLayer layer =
                    CreateTerrainLayer(
                        material,
                        palette,
                        materialPath);

                if (layer == null)
                    continue;

                layerIndex =
                    layers.Count;

                layers.Add(
                    layer);

                layerByMaterialPath[
                    materialPath] =
                    layerIndex;
            }

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

                    terrainLayerIndex =
                        layerIndex
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
            surfaces,
            layers.Count);

        // note: Let the final alpha strip finish its render frame before terrain texture publication performs its unavoidable native flush.
        yield return null;
        terrain.Flush();
        yield return null;
        completed?.Invoke(surfaces);
    }

    private static GeneratedAssetReferenceRecord
        FindResolvableTerrainMaterial(
            GeneratedRegionAssetPaletteRecord palette,
            YQRuntimeWorldAssetRegistry registry)
    {
        if (palette == null ||
            palette.terrainMaterials == null)
        {
            return null;
        }

        for (int i = 0;
             i < palette.terrainMaterials.Count;
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

            Material material =
                registry.ResolveMaterial(
                    reference.assetPath);

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
                return reference;

            try
            {
                if (material.mainTexture
                    is Texture2D)
                {
                    return reference;
                }
            }
            catch
            {
            }
        }

        return null;
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
                    : material.name);

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

                float totalWeight =
                    0f;

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

                    float influence =
                        1f /
                        Mathf.Max(
                            100f,
                            distanceSquared);

                    influence *=
                        influence;

                    int layer =
                        surface
                            .terrainLayerIndex;

                    if (layer < 0 ||
                        layer >= layerCount)
                    {
                        continue;
                    }

                    weights[
                        y,
                        x,
                        layer] +=
                        influence;

                    totalWeight +=
                        influence;
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

    // ============================================================
    // WILDERNESS
    // ============================================================

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

            int rockTarget =
                ResolveRockTarget(
                    region,
                    palette);

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
                12,
                42);
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
                6,
                20);
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
                string.Equals(
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

        int attempts =
            targetCount *
            2;
        float frameStartedAt = Time.realtimeSinceStartup;

        for (int attempt = 0;
             attempt < attempts &&
             spawned < targetCount;
             attempt++)
        {
            string seed =
                plan.worldSeed +
                "|scatter|" +
                region.regionId +
                "|" +
                slot +
                "|" +
                attempt;

            if (!TryResolveWildernessPosition(
                    terrain,
                    plan,
                    regionCenter,
                    seed,
                    WildernessRadiusMin,
                    WildernessRadiusMax,
                    SettlementClearRadius,
                    OriginClearRadius,
                    16f,
                    out Vector3 position))
            {
                continue;
            }

            GeneratedAssetReferenceRecord reference =
                PickSmallScatterReference(
                    palette,
                    slot,
                    seed);

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

        // note: Reject giant marketplace set pieces before instantiation so first-world wilderness does not pay their spawn and teardown cost.
        return (vegetation &&
                (footprint > 20f || height > 32f)) ||
               (rock &&
                (footprint > 16f || height > 16f));
    }

    private static GeneratedAssetReferenceRecord
        PickSmallScatterReference(
            GeneratedRegionAssetPaletteRecord palette,
            string slot,
            string seed)
    {
        if (palette == null)
            return null;

        List<GeneratedAssetReferenceRecord> candidates =
            new List<
                GeneratedAssetReferenceRecord>();

        List<GeneratedAssetReferenceRecord> primary =
            YQWorldAssetCatalog
                .GetSlotList(
                    palette,
                    slot);

        AddValidSmallReferences(
            candidates,
            primary);

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
                        candidates.Add(
                            reference);
                    }
                }
            }
        }

        return
            PickWeightedReference(
                candidates,
                seed);
    }

    private static void AddValidSmallReferences(
        List<GeneratedAssetReferenceRecord> result,
        List<GeneratedAssetReferenceRecord> source)
    {
        if (result == null ||
            source == null)
        {
            return;
        }

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

            result.Add(
                reference);
        }
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

                if (!TryResolveAmbientMonsterPrefab(
                        registry,
                        source.family,
                        seed,
                        out YQRuntimeWorldAssetEntry entry,
                        out string resolvedCategory))
                {
                    continue;
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
                        source.family) +
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
                        source.family);

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
                    source,
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

        if (plan == null ||
            plan.encampments == null)
        {
            return result;
        }

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

            string family =
                SafeText(
                    encampment.monsterFamily,
                    string.Empty);

            if (string.IsNullOrWhiteSpace(
                    family))
            {
                continue;
            }

            bool duplicate =
                false;

            for (int existing = 0;
                 existing < result.Count;
                 existing++)
            {
                if (string.Equals(
                        result[existing]
                            .family,
                        family,
                        StringComparison.OrdinalIgnoreCase))
                {
                    duplicate =
                        true;

                    break;
                }
            }

            if (duplicate)
                continue;

            result.Add(
                new AmbientMonsterSource
                {
                    family =
                        family,

                    factionId =
                        SafeText(
                            encampment
                                .inhabitantFactionId,
                            "generated_wilderness")
                });
        }

        return result;
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
        out Vector3 position)
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

        position =
            candidate;

        return true;
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

        if (vegetation &&
            (footprint > 20f ||
             height > 32f))
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
            slot);

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
        string slot)
    {
        if (instance == null ||
            terrain == null)
        {
            return;
        }

        if (!TryGetWildernessBounds(
                instance,
                out Bounds bounds))
        {
            return;
        }

        float contactY =
            bounds.min.y;

        if (string.Equals(
                slot,
                YQWorldAssetCatalog
                    .SlotVegetation,
                StringComparison.OrdinalIgnoreCase) &&
            TryGetTreeTrunkBounds(
                instance,
                out Bounds trunkBounds))
        {
            contactY =
                trunkBounds.min.y;
        }

        Vector3 anchor =
            instance.transform.position;

        float terrainY =
            YQGeneratedWorldTerrain
                .SampleWorldHeight(
                    terrain,
                    anchor);

        const float GroundEmbed =
            0.05f;

        float correction =
            terrainY -
            contactY -
            GroundEmbed;

        Vector3 position =
            instance.transform.position;

        position.y +=
            correction;

        instance.transform.position =
            position;
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
            !TryGetWildernessBounds(
                instance,
                out Bounds bounds))
        {
            return;
        }

        Vector3 center =
            bounds.center;

        float sampleX =
            Mathf.Clamp(
                bounds.size.x *
                0.28f,
                1.5f,
                18f);

        float sampleZ =
            Mathf.Clamp(
                bounds.size.z *
                0.28f,
                1.5f,
                18f);

        Vector3[] points =
        {
            center,

            new Vector3(
                center.x +
                    sampleX,
                0f,
                center.z),

            new Vector3(
                center.x -
                    sampleX,
                0f,
                center.z),

            new Vector3(
                center.x,
                0f,
                center.z +
                    sampleZ),

            new Vector3(
                center.x,
                0f,
                center.z -
                    sampleZ)
        };

        float[] heights =
            new float[
                points.Length];

        int found =
            0;

        for (int i = 0;
             i < points.Length;
             i++)
        {
            if (!InsideTerrain(
                    terrain,
                    points[i]))
            {
                continue;
            }

            heights[
                found++] =
                YQGeneratedWorldTerrain
                    .SampleWorldHeight(
                        terrain,
                        points[i]);
        }

        if (found == 0)
            return;

        Array.Sort(
            heights,
            0,
            found);

        /*
         * Median contact avoids the old highest-corner lift.
         *
         * A small embed is intentional: buried geometry looks natural;
         * visible air under a cave/structure does not.
         */
        float contact =
            heights[
                found /
                2];

        float penetration =
            Mathf.Clamp(
                bounds.size.y *
                Mathf.Max(
                    0f,
                    penetrationRatio),
                0.03f,
                1.75f);

        float correction =
            contact -
            bounds.min.y -
            penetration;

        Vector3 position =
            instance.transform.position;

        position.y +=
            correction;

        instance.transform.position =
            position;
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

        float terrainY =
            YQGeneratedWorldTerrain
                .SampleWorldHeight(
                    terrain,
                    anchor);

        if (!TryGetWildernessBounds(
                root,
                out Bounds bounds))
        {
            Vector3 position =
                root.transform.position;

            position.y =
                terrainY;

            root.transform.position =
                position;

            return;
        }

        float correction =
            terrainY -
            bounds.min.y +
            0.02f;

        Vector3 rootPosition =
            root.transform.position;

        rootPosition.y +=
            correction;

        root.transform.position =
            rootPosition;
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
