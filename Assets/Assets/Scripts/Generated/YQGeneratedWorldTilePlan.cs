using UnityEngine;

public enum YQGeneratedWorldBiomeKind
{
    TemperateLowland,
    AncientWoodland,
    Highland,
    Moorland,
    Wetland
}

public readonly struct YQGeneratedWorldTileProfile
{
    public readonly Vector2Int Coordinate;
    public readonly YQGeneratedWorldBiomeKind Biome;
    public readonly float ElevationBias;
    public readonly float Ruggedness;
    public readonly float Moisture;
    public readonly float ForestDensity;
    public readonly float MountainAffinity;
    public readonly float DrainageAffinity;

    internal YQGeneratedWorldTileProfile(
        Vector2Int coordinate,
        YQGeneratedWorldBiomeKind biome,
        float elevationBias,
        float ruggedness,
        float moisture,
        float forestDensity,
        float mountainAffinity,
        float drainageAffinity)
    {
        Coordinate = coordinate;
        Biome = biome;
        ElevationBias = elevationBias;
        Ruggedness = ruggedness;
        Moisture = moisture;
        ForestDensity = forestDensity;
        MountainAffinity = mountainAffinity;
        DrainageAffinity = drainageAffinity;
    }
}

public sealed class YQGeneratedWorldTilePlan
{
    // note: Eight deterministic 128-metre tiles provide useful regional variation while keeping the current 1024-metre world and all authored anchors intact.
    public const int TileCountPerAxis = 8;

    public const float TileWorldSize =
        YQGeneratedWorldTerrain.WorldSize /
        TileCountPerAxis;

    private const int NodeCount =
        TileCountPerAxis +
        1;

    private readonly NodeProfile[,] nodes =
        new NodeProfile[
            NodeCount,
            NodeCount];

    private readonly GeneratedSpatialWorldPlanRecord spatialPlan;

    private struct NodeProfile
    {
        public float uplift;
        public float ruggedness;
        public float moisture;
        public float forest;
    }

    public YQGeneratedWorldTilePlan(
        uint seedHash)
        : this(seedHash, null)
    {
    }

    public YQGeneratedWorldTilePlan(
        uint seedHash,
        GeneratedSpatialWorldPlanRecord semanticSpatialPlan)
    {
        // note: Seed noise supplies local variation while persisted region meaning controls the broad ecological target.
        spatialPlan = semanticSpatialPlan;
        NodeProfile[,] rawNodes =
            new NodeProfile[
                NodeCount,
                NodeCount];

        // note: Stable value nodes create broad authored-looking regions without relying on Unity's process-dependent string hashing.
        for (int z = 0;
             z < NodeCount;
             z++)
        {
            for (int x = 0;
                 x < NodeCount;
                 x++)
            {
                float normalizedX =
                    x /
                    (float)(NodeCount - 1);

                float normalizedZ =
                    z /
                    (float)(NodeCount - 1);

                float edgeDistance =
                    Mathf.Max(
                        Mathf.Abs(
                            normalizedX -
                            0.5f),
                        Mathf.Abs(
                            normalizedZ -
                            0.5f)) *
                    2f;

                float edgeUplift =
                    Smooth01(
                        Mathf.InverseLerp(
                            0.48f,
                            0.94f,
                            edgeDistance));

                float uplift =
                    Mathf.Clamp01(
                        Hash01(
                            seedHash,
                            x,
                            z,
                            0xA24BAED5u) *
                            0.72f +
                        edgeUplift *
                            0.42f);

                float moisture =
                    Mathf.Clamp01(
                        Hash01(
                            seedHash,
                            x,
                            z,
                            0x9FB21C65u) *
                            0.82f +
                        (1f - uplift) *
                            0.18f);

                float ruggedness =
                    Mathf.Clamp01(
                        Hash01(
                            seedHash,
                            x,
                            z,
                            0xC13FA9A9u) *
                            0.68f +
                        uplift *
                            0.32f);

                float forest =
                    Mathf.Clamp01(
                        Hash01(
                            seedHash,
                            x,
                            z,
                            0x91E10DA5u) *
                            0.55f +
                        moisture *
                            0.5f -
                        ruggedness *
                            0.16f);

                rawNodes[z, x] =
                    new NodeProfile
                    {
                        uplift = uplift,
                        ruggedness = ruggedness,
                        moisture = moisture,
                        forest = forest
                    };
            }
        }

        // note: A compact neighbor blur turns hash nodes into coherent multi-tile biome provinces rather than a checkerboard of unrelated cells.
        for (int z = 0;
             z < NodeCount;
             z++)
        {
            for (int x = 0;
                 x < NodeCount;
                 x++)
            {
                NodeProfile weighted =
                    Scale(
                        rawNodes[z, x],
                        4f);

                float totalWeight =
                    4f;

                AddNeighbor(
                    rawNodes,
                    x - 1,
                    z,
                    ref weighted,
                    ref totalWeight);

                AddNeighbor(
                    rawNodes,
                    x + 1,
                    z,
                    ref weighted,
                    ref totalWeight);

                AddNeighbor(
                    rawNodes,
                    x,
                    z - 1,
                    ref weighted,
                    ref totalWeight);

                AddNeighbor(
                    rawNodes,
                    x,
                    z + 1,
                    ref weighted,
                    ref totalWeight);

                nodes[z, x] =
                    Scale(
                        weighted,
                        1f /
                            totalWeight);
            }
        }
    }

    public YQGeneratedWorldTileProfile Sample(
        float worldX,
        float worldZ)
    {
        float gridX =
            Mathf.Clamp(
                (worldX +
                    YQGeneratedWorldTerrain.WorldSize *
                        0.5f) /
                    TileWorldSize,
                0f,
                TileCountPerAxis);

        float gridZ =
            Mathf.Clamp(
                (worldZ +
                    YQGeneratedWorldTerrain.WorldSize *
                        0.5f) /
                    TileWorldSize,
                0f,
                TileCountPerAxis);

        int x0 =
            Mathf.Min(
                Mathf.FloorToInt(gridX),
                NodeCount - 1);

        int z0 =
            Mathf.Min(
                Mathf.FloorToInt(gridZ),
                NodeCount - 1);

        int x1 =
            Mathf.Min(
                x0 + 1,
                NodeCount - 1);

        int z1 =
            Mathf.Min(
                z0 + 1,
                NodeCount - 1);

        float blendX =
            Smooth01(
                gridX -
                x0);

        float blendZ =
            Smooth01(
                gridZ -
                z0);

        // note: Smooth bilinear sampling guarantees identical values on shared tile borders and avoids visible square biome seams in terrain and foliage.
        NodeProfile south =
            Lerp(
                nodes[z0, x0],
                nodes[z0, x1],
                blendX);

        NodeProfile north =
            Lerp(
                nodes[z1, x0],
                nodes[z1, x1],
                blendX);

        NodeProfile profile =
            Lerp(
                south,
                north,
                blendZ);

        ApplySemanticRegionalProfile(
            ref profile,
            worldX,
            worldZ);

        float mountainAffinity =
            Mathf.Clamp01(
                profile.uplift *
                    0.7f +
                profile.ruggedness *
                    0.48f -
                profile.moisture *
                    0.16f);

        float drainageAffinity =
            Mathf.Clamp01(
                profile.moisture *
                    0.72f +
                (1f - profile.uplift) *
                    0.28f);

        YQGeneratedWorldBiomeKind biome =
            ResolveBiome(
                profile,
                mountainAffinity,
                drainageAffinity);

        return
            new YQGeneratedWorldTileProfile(
                new Vector2Int(
                    Mathf.Clamp(
                        Mathf.FloorToInt(gridX),
                        0,
                        TileCountPerAxis - 1),
                    Mathf.Clamp(
                        Mathf.FloorToInt(gridZ),
                        0,
                        TileCountPerAxis - 1)),
                biome,
                profile.uplift *
                    2f -
                    1f,
                profile.ruggedness,
                profile.moisture,
                profile.forest,
                mountainAffinity,
                drainageAffinity);
    }

    private void ApplySemanticRegionalProfile(
        ref NodeProfile profile,
        float worldX,
        float worldZ)
    {
        if (spatialPlan == null || spatialPlan.regions == null || spatialPlan.regions.Count == 0)
            return;

        float totalWeight = 0f;
        float uplift = 0f;
        float ruggedness = 0f;
        float moisture = 0f;
        float forest = 0f;
        for (int i = 0; i < spatialPlan.regions.Count; i++)
        {
            GeneratedSpatialRegionRecord region = spatialPlan.regions[i];
            if (region == null)
                continue;
            float distance = Vector2.Distance(
                new Vector2(worldX, worldZ),
                new Vector2(region.centerX, region.centerZ));
            float weight = Mathf.Clamp01(1f - distance / Mathf.Max(1f, region.radius * 1.35f));
            weight = weight * weight * (3f - 2f * weight);
            if (weight <= 0f)
                continue;

            float regionalForest = Mathf.Clamp01(
                region.moisture * 0.78f +
                (1f - region.ruggedness) * 0.18f -
                region.civilizationDensity * 0.14f);
            if (string.Equals(spatialPlan.structuralTheme, "cyberpunk", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(spatialPlan.structuralTheme, "noir_city", System.StringComparison.OrdinalIgnoreCase))
            {
                regionalForest *= 0.28f;
            }

            uplift += Mathf.Clamp01(region.elevationBias) * weight;
            ruggedness += Mathf.Clamp01(region.ruggedness) * weight;
            moisture += Mathf.Clamp01(region.moisture) * weight;
            forest += regionalForest * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0.001f)
            return;

        float semanticBlend = Mathf.Clamp01(totalWeight * 0.52f);
        // note: Wide blended influence bands prevent hard biome seams while making theme and region descriptions structurally visible.
        profile.uplift = Mathf.Lerp(profile.uplift, uplift / totalWeight, semanticBlend);
        profile.ruggedness = Mathf.Lerp(profile.ruggedness, ruggedness / totalWeight, semanticBlend);
        profile.moisture = Mathf.Lerp(profile.moisture, moisture / totalWeight, semanticBlend);
        profile.forest = Mathf.Lerp(profile.forest, forest / totalWeight, semanticBlend);
    }

    public YQGeneratedWorldTileProfile GetTileProfile(
        int tileX,
        int tileZ)
    {
        int safeX =
            Mathf.Clamp(
                tileX,
                0,
                TileCountPerAxis - 1);

        int safeZ =
            Mathf.Clamp(
                tileZ,
                0,
                TileCountPerAxis - 1);

        // note: Tile-center profiles give future streaming and asset-binding passes one stable semantic record per geographical cell.
        return
            Sample(
                -YQGeneratedWorldTerrain.WorldSize *
                    0.5f +
                (safeX + 0.5f) *
                    TileWorldSize,
                -YQGeneratedWorldTerrain.WorldSize *
                    0.5f +
                (safeZ + 0.5f) *
                    TileWorldSize);
    }

    private static YQGeneratedWorldBiomeKind ResolveBiome(
        NodeProfile profile,
        float mountainAffinity,
        float drainageAffinity)
    {
        if (drainageAffinity >= 0.68f &&
            profile.uplift <= 0.52f)
        {
            return YQGeneratedWorldBiomeKind.Wetland;
        }

        if (mountainAffinity >= 0.63f)
            return YQGeneratedWorldBiomeKind.Highland;

        if (profile.forest >= 0.56f)
            return YQGeneratedWorldBiomeKind.AncientWoodland;

        if (profile.moisture <= 0.43f &&
            profile.ruggedness >= 0.5f)
        {
            return YQGeneratedWorldBiomeKind.Moorland;
        }

        return YQGeneratedWorldBiomeKind.TemperateLowland;
    }

    private static void AddNeighbor(
        NodeProfile[,] source,
        int x,
        int z,
        ref NodeProfile weighted,
        ref float totalWeight)
    {
        if (x < 0 ||
            x >= NodeCount ||
            z < 0 ||
            z >= NodeCount)
        {
            return;
        }

        weighted =
            Add(
                weighted,
                source[z, x]);

        totalWeight +=
            1f;
    }

    private static NodeProfile Add(
        NodeProfile a,
        NodeProfile b)
    {
        return
            new NodeProfile
            {
                uplift = a.uplift + b.uplift,
                ruggedness = a.ruggedness + b.ruggedness,
                moisture = a.moisture + b.moisture,
                forest = a.forest + b.forest
            };
    }

    private static NodeProfile Scale(
        NodeProfile value,
        float scale)
    {
        return
            new NodeProfile
            {
                uplift = value.uplift * scale,
                ruggedness = value.ruggedness * scale,
                moisture = value.moisture * scale,
                forest = value.forest * scale
            };
    }

    private static NodeProfile Lerp(
        NodeProfile a,
        NodeProfile b,
        float t)
    {
        return
            new NodeProfile
            {
                uplift = Mathf.Lerp(a.uplift, b.uplift, t),
                ruggedness = Mathf.Lerp(a.ruggedness, b.ruggedness, t),
                moisture = Mathf.Lerp(a.moisture, b.moisture, t),
                forest = Mathf.Lerp(a.forest, b.forest, t)
            };
    }

    private static float Smooth01(
        float value)
    {
        float clamped =
            Mathf.Clamp01(value);

        return
            clamped *
            clamped *
            (3f -
                2f *
                    clamped);
    }

    private static float Hash01(
        uint seed,
        int x,
        int z,
        uint salt)
    {
        uint value =
            seed ^
            salt;

        value ^=
            unchecked((uint)(x + 4096)) *
            0x9E3779B9u;

        value ^=
            unchecked((uint)(z + 4096)) *
            0x85EBCA6Bu;

        value =
            Mix(value);

        return
            (value & 0x00FFFFFFu) /
            16777215f;
    }

    private static uint Mix(
        uint value)
    {
        value ^=
            value >> 16;

        value *=
            0x7FEB352Du;

        value ^=
            value >> 15;

        value *=
            0x846CA68Bu;

        value ^=
            value >> 16;

        return value;
    }
}
