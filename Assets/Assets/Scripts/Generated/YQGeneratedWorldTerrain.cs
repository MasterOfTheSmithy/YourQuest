using System;
using System.Collections;
using UnityEngine;

public static class YQGeneratedWorldTerrain
{
    /*
     * IMPORTANT:
     * Changing this algorithm later can change old worlds even when
     * their worldSeed remains identical.
     *
     * Keep the version explicit so future save migration can choose
     * the correct terrain algorithm.
     */
    public const string TerrainGenerationVersion =
    "generated_terrain_v2";

    public const string RuntimeTerrainObjectName =
        "YQ_GENERATED_TERRAIN";

    public const float WorldSize =
        1024f;

    public const float TerrainHeight =
        140f;

    public const int HeightmapResolution =
        513;

    /*
     * Vey's hut is the single fixed narrative origin.
     *
     * The world is centered on this point, but everything outside the
     * origin site is derived from the persisted world seed.
     */
    public static readonly Vector3 OriginWorldPosition =
        Vector3.zero;

    private const float OriginPlateauRadius =
        30f;

    private const float OriginBlendRadius =
        52f;

    private const float BaseHeightNormalized =
    0.16f;

    private const float PrimaryNoiseScale =
        0.0019f;

    private const float SecondaryNoiseScale =
        0.0065f;

    private const float DetailNoiseScale =
        0.024f;

    private const float PrimaryAmplitude =
        0.22f;

    private const float SecondaryAmplitude =
        0.085f;

    private const float DetailAmplitude =
        0.022f;

    private const float StartupFrameBudgetSeconds =
        0.003f;

    private const int HeightmapUploadRowsPerFrame =
        16;

    public static IEnumerator BuildRoutine(
        Transform parent,
        string worldSeed,
        Action<Terrain> completed)
    {
        DestroyExisting();

        string safeSeed =
            string.IsNullOrWhiteSpace(worldSeed)
                ? "yourquest_default_world"
                : worldSeed.Trim();

        uint seedHash =
            StableHash32(
                TerrainGenerationVersion +
                "|" +
                safeSeed);

        float[,] heights =
            new float[
                HeightmapResolution,
                HeightmapResolution];

        // note: Height synthesis is deliberately row-budgeted so Goddess text, camera motion, and loading VFX receive a rendered frame during terrain creation.
        yield return GenerateHeightmapRoutine(
            seedHash,
            heights);

        TerrainData terrainData =
            new TerrainData();

        terrainData.name =
            "YQ_GeneratedTerrainData_" +
            ShortSeed(seedHash);

        terrainData.heightmapResolution =
            HeightmapResolution;

        terrainData.size =
            new Vector3(
                WorldSize,
                TerrainHeight,
                WorldSize);

        for (int startRow = 0;
             startRow < HeightmapResolution;
             startRow += HeightmapUploadRowsPerFrame)
        {
            int rowCount =
                Mathf.Min(
                    HeightmapUploadRowsPerFrame,
                    HeightmapResolution - startRow);

            float[,] strip =
                new float[
                    rowCount,
                    HeightmapResolution];

            for (int row = 0; row < rowCount; row++)
            {
                for (int column = 0;
                     column < HeightmapResolution;
                     column++)
                {
                    strip[row, column] =
                        heights[startRow + row, column];
                }
            }

            // note: Delayed strip uploads avoid one monolithic heightmap upload; the later construction prepass performs the single authoritative heightmap synchronization.
            terrainData.SetHeightsDelayLOD(
                0,
                startRow,
                strip);

            yield return null;
        }

        Terrain terrain =
            CreateTerrainObject(
                parent,
                terrainData);

        LogBuild(
            safeSeed,
            seedHash);

        completed?.Invoke(
            terrain);
    }

    public static Terrain Build(
        Transform parent,
        string worldSeed)
    {
        DestroyExisting();

        string safeSeed =
            string.IsNullOrWhiteSpace(worldSeed)
                ? "yourquest_default_world"
                : worldSeed.Trim();

        uint seedHash =
            StableHash32(
                TerrainGenerationVersion +
                "|" +
                safeSeed);

        float[,] heights =
            GenerateHeightmap(
                seedHash);

        TerrainData terrainData =
            new TerrainData();

        terrainData.name =
            "YQ_GeneratedTerrainData_" +
            ShortSeed(seedHash);

        terrainData.heightmapResolution =
            HeightmapResolution;

        terrainData.size =
            new Vector3(
                WorldSize,
                TerrainHeight,
                WorldSize);

        terrainData.SetHeights(
            0,
            0,
            heights);

        Terrain terrain =
            CreateTerrainObject(
                parent,
                terrainData);

        // note: Legacy synchronous callers retain their historical fully-published terrain contract; production startup uses BuildRoutine instead.
        terrain?.Flush();

        LogBuild(
            safeSeed,
            seedHash);

        return terrain;
    }

    private static Terrain CreateTerrainObject(
        Transform parent,
        TerrainData terrainData)
    {
        GameObject terrainObject =
            Terrain.CreateTerrainGameObject(
                terrainData);

        terrainObject.name =
            RuntimeTerrainObjectName;

        if (parent != null)
        {
            terrainObject.transform.SetParent(
                parent,
                false);
        }

        // note: Terrain coordinates begin at the lower-left corner, so the runtime object is centered around the deterministic world origin.
        terrainObject.transform.position =
            new Vector3(
                -WorldSize * 0.5f,
                0f,
                -WorldSize * 0.5f);

        Terrain terrain =
            terrainObject.GetComponent<Terrain>();

        if (terrain != null)
        {
            terrain.drawInstanced =
                true;

            terrain.heightmapPixelError =
                5f;

            terrain.basemapDistance =
                1000f;

            terrain.shadowCastingMode =
                UnityEngine.Rendering
                    .ShadowCastingMode.On;
        }

        TerrainCollider terrainCollider =
            terrainObject.GetComponent<
                TerrainCollider>();

        if (terrainCollider != null)
        {
            terrainCollider.terrainData =
                terrainData;
        }

        return terrain;
    }

    private static void LogBuild(
        string safeSeed,
        uint seedHash)
    {
        Debug.Log(
            "[YQGeneratedWorldTerrain] BUILT\n" +
            "Version: " +
            TerrainGenerationVersion +
            "\n" +
            "World seed: " +
            safeSeed +
            "\n" +
            "Terrain seed: " +
            seedHash.ToString("X8") +
            "\n" +
            "Size: " +
            WorldSize +
            " x " +
            WorldSize +
            "\n" +
            "Height: " +
            TerrainHeight +
            "\n" +
            "Heightmap: " +
            HeightmapResolution +
            "\n" +
            "Vey origin plateau radius: " +
            OriginPlateauRadius);
    }

    public static void DestroyExisting()
    {
        GameObject existing =
            GameObject.Find(
                RuntimeTerrainObjectName);

        if (existing == null)
            return;

        Terrain terrain =
            existing.GetComponent<Terrain>();

        TerrainData terrainData =
            terrain != null
                ? terrain.terrainData
                : null;

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(
                existing);

            if (terrainData != null)
            {
                UnityEngine.Object.Destroy(
                    terrainData);
            }
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(
                existing);

            if (terrainData != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    terrainData);
            }
        }
    }

    public static float SampleWorldHeight(
        Terrain terrain,
        Vector3 worldPosition)
    {
        if (terrain == null ||
            terrain.terrainData == null)
        {
            return worldPosition.y;
        }

        return
            terrain.SampleHeight(
                worldPosition) +
            terrain.transform.position.y;
    }

    public static Vector3 GroundPoint(
        Terrain terrain,
        Vector3 worldPosition)
    {
        worldPosition.y =
            SampleWorldHeight(
                terrain,
                worldPosition);

        return worldPosition;
    }

    private static float[,] GenerateHeightmap(
        uint seedHash)
    {
        int resolution =
            HeightmapResolution;

        float[,] heights =
            new float[
                resolution,
                resolution];

        Vector2 primaryOffset =
            SeedOffset(
                seedHash,
                0xA53C9E17u);

        Vector2 secondaryOffset =
            SeedOffset(
                seedHash,
                0x71F42D89u);

        Vector2 detailOffset =
            SeedOffset(
                seedHash,
                0xC3195A47u);

        for (int z = 0;
             z < resolution;
             z++)
        {
            FillHeightmapRow(
                heights,
                z,
                primaryOffset,
                secondaryOffset,
                detailOffset);
        }

        return heights;
    }

    private static IEnumerator GenerateHeightmapRoutine(
        uint seedHash,
        float[,] heights)
    {
        Vector2 primaryOffset =
            SeedOffset(
                seedHash,
                0xA53C9E17u);

        Vector2 secondaryOffset =
            SeedOffset(
                seedHash,
                0x71F42D89u);

        Vector2 detailOffset =
            SeedOffset(
                seedHash,
                0xC3195A47u);

        float frameStartedAt =
            Time.realtimeSinceStartup;

        for (int z = 0;
             z < HeightmapResolution;
             z++)
        {
            FillHeightmapRow(
                heights,
                z,
                primaryOffset,
                secondaryOffset,
                detailOffset);

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StartupFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt =
                    Time.realtimeSinceStartup;
            }
        }
    }

    private static void FillHeightmapRow(
        float[,] heights,
        int z,
        Vector2 primaryOffset,
        Vector2 secondaryOffset,
        Vector2 detailOffset)
    {
        int resolution =
            HeightmapResolution;

        float normalizedZ =
            z /
            (float)(
                resolution -
                1);

        float worldZ =
            normalizedZ *
                WorldSize -
            WorldSize *
                0.5f;

        for (int x = 0;
             x < resolution;
             x++)
        {
            float normalizedX =
                x /
                (float)(
                    resolution -
                    1);

            float worldX =
                normalizedX *
                    WorldSize -
                WorldSize *
                    0.5f;

            float primary =
                FractalNoise(
                    worldX,
                    worldZ,
                    PrimaryNoiseScale,
                    primaryOffset,
                    4,
                    0.5f,
                    2f);

            float secondary =
                FractalNoise(
                    worldX,
                    worldZ,
                    SecondaryNoiseScale,
                    secondaryOffset,
                    3,
                    0.52f,
                    2.05f);

            float detail =
                Mathf.PerlinNoise(
                    worldX *
                        DetailNoiseScale +
                        detailOffset.x,
                    worldZ *
                        DetailNoiseScale +
                        detailOffset.y);

            primary =
                primary * 2f - 1f;

            secondary =
                secondary * 2f - 1f;

            detail =
                detail * 2f - 1f;

            float height =
                BaseHeightNormalized +
                primary *
                    PrimaryAmplitude +
                secondary *
                    SecondaryAmplitude +
                detail *
                    DetailAmplitude;

            float edgeDistanceX =
                Mathf.Abs(
                    normalizedX -
                    0.5f) *
                2f;

            float edgeDistanceZ =
                Mathf.Abs(
                    normalizedZ -
                    0.5f) *
                2f;

            float edgeDistance =
                Mathf.Max(
                    edgeDistanceX,
                    edgeDistanceZ);

            float edgeFalloff =
                Smooth01(
                    Mathf.InverseLerp(
                        0.72f,
                        1f,
                        edgeDistance));

            height -=
                edgeFalloff *
                0.11f;

            float originDistance =
                Mathf.Sqrt(
                    worldX * worldX +
                    worldZ * worldZ);

            float originBlend =
                Smooth01(
                    Mathf.InverseLerp(
                        OriginPlateauRadius,
                        OriginBlendRadius,
                        originDistance));

            heights[z, x] =
                Mathf.Clamp01(
                    Mathf.Lerp(
                        BaseHeightNormalized,
                        height,
                        originBlend));
        }
    }

    private static float FractalNoise(
        float worldX,
        float worldZ,
        float baseScale,
        Vector2 offset,
        int octaves,
        float persistence,
        float lacunarity)
    {
        float amplitude =
            1f;

        float frequency =
            1f;

        float sum =
            0f;

        float normalization =
            0f;

        for (int octave = 0;
             octave < octaves;
             octave++)
        {
            float sampleX =
                worldX *
                    baseScale *
                    frequency +
                offset.x;

            float sampleZ =
                worldZ *
                    baseScale *
                    frequency +
                offset.y;

            float value =
                Mathf.PerlinNoise(
                    sampleX,
                    sampleZ);

            sum +=
                value *
                amplitude;

            normalization +=
                amplitude;

            amplitude *=
                persistence;

            frequency *=
                lacunarity;
        }

        if (normalization <= 0f)
            return 0.5f;

        return
            sum /
            normalization;
    }

    private static Vector2 SeedOffset(
        uint seed,
        uint salt)
    {
        uint a =
            Mix(
                seed ^
                salt);

        uint b =
            Mix(
                seed ^
                RotateLeft(
                    salt,
                    13));

        float x =
            (a & 0x00FFFFFFu) /
            16777215f;

        float y =
            (b & 0x00FFFFFFu) /
            16777215f;

        /*
         * Large coordinate offsets ensure different world seeds do
         * not sample nearly identical portions of Perlin space.
         */
        return
            new Vector2(
                x * 10000f +
                    113.37f,
                y * 10000f +
                    719.91f);
    }

    private static uint StableHash32(
        string value)
    {
        /*
         * FNV-1a.
         *
         * Do NOT use string.GetHashCode() for world generation because
         * runtime/platform behavior is not a persistence contract.
         */
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

    private static uint RotateLeft(
        uint value,
        int count)
    {
        count &=
            31;

        return
            (value << count) |
            (value >>
                (32 - count));
    }

    private static float Smooth01(
        float value)
    {
        value =
            Mathf.Clamp01(
                value);

        return
            value *
            value *
            (3f -
                2f *
                value);
    }

    private static string ShortSeed(
        uint hash)
    {
        return
            hash.ToString(
                "X8");
    }
}
