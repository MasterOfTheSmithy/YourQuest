using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum YQGeneratedWorldPlacementCategory
{
    Automatic,
    Structure,
    Rock,
    Tree,
    Vegetation,
    Prop,
    Actor
}

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

    public static bool TryGetStableContactGeometry(
        GameObject root,
        out Bounds aggregateBounds,
        out float structuralBottom)
    {
        aggregateBounds =
            default;
        structuralBottom =
            0f;

        if (root == null)
            return false;

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true);
        List<Vector2> lowerBandSamples =
            new List<Vector2>();
        bool initialized =
            false;

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            Renderer renderer =
                renderers[index];

            if (!IsStableContactRenderer(
                    renderer))
            {
                continue;
            }

            Bounds bounds =
                renderer.bounds;

            if (!initialized)
            {
                aggregateBounds =
                    bounds;
                initialized =
                    true;
            }
            else
            {
                aggregateBounds.Encapsulate(
                    bounds);
            }
        }

        if (!initialized)
            return false;

        float lowerBandCeiling =
            aggregateBounds.min.y +
            Mathf.Min(
                8f,
                Mathf.Max(
                    0.6f,
                    aggregateBounds.size.y *
                        0.25f));
        float totalWeight =
            0f;

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            Renderer renderer =
                renderers[index];

            if (!IsStableContactRenderer(
                    renderer))
            {
                continue;
            }

            Bounds bounds =
                renderer.bounds;

            if (bounds.min.y >
                lowerBandCeiling)
            {
                continue;
            }

            float footprint =
                bounds.size.x *
                bounds.size.z;

            if (!IsFinite(footprint) ||
                footprint <= 0.0001f)
            {
                continue;
            }

            float weight =
                Mathf.Sqrt(footprint);
            lowerBandSamples.Add(
                new Vector2(
                    bounds.min.y,
                    weight));
            totalWeight +=
                weight;
        }

        if (lowerBandSamples.Count == 0 ||
            !IsFinite(totalWeight) ||
            totalWeight <= 0f)
        {
            structuralBottom =
                aggregateBounds.min.y;
            return true;
        }

        lowerBandSamples.Sort(
            (left, right) =>
                left.x.CompareTo(right.x));
        float targetWeight =
            totalWeight *
            0.5f;
        float accumulatedWeight =
            0f;
        structuralBottom =
            lowerBandSamples[0].x;

        for (int index = 0;
             index < lowerBandSamples.Count;
             index++)
        {
            accumulatedWeight +=
                lowerBandSamples[index].y;

            if (accumulatedWeight <
                targetWeight)
            {
                continue;
            }

            structuralBottom =
                lowerBandSamples[index].x;
            break;
        }

        return IsFinite(
            structuralBottom);
    }

    public static bool TrySampleFootprintHeight(
        Terrain terrain,
        Bounds bounds,
        out float contactHeight,
        out float minimumHeight,
        out float maximumHeight)
    {
        contactHeight =
            0f;
        minimumHeight =
            0f;
        maximumHeight =
            0f;

        if (terrain == null ||
            terrain.terrainData == null ||
            !IsFiniteVector(bounds.center) ||
            !IsFiniteVector(bounds.size))
        {
            return false;
        }

        float sampleX =
            Mathf.Clamp(
                bounds.extents.x *
                    0.72f,
                0f,
                24f);
        float sampleZ =
            Mathf.Clamp(
                bounds.extents.z *
                    0.72f,
                0f,
                24f);
        Vector3 center =
            bounds.center;
        Vector3[] points =
        {
            center,
            new Vector3(center.x + sampleX, center.y, center.z),
            new Vector3(center.x - sampleX, center.y, center.z),
            new Vector3(center.x, center.y, center.z + sampleZ),
            new Vector3(center.x, center.y, center.z - sampleZ),
            new Vector3(center.x + sampleX, center.y, center.z + sampleZ),
            new Vector3(center.x + sampleX, center.y, center.z - sampleZ),
            new Vector3(center.x - sampleX, center.y, center.z + sampleZ),
            new Vector3(center.x - sampleX, center.y, center.z - sampleZ)
        };
        float[] heights =
            new float[points.Length];
        int found =
            0;
        Vector3 terrainOrigin =
            terrain.transform.position;
        Vector3 terrainSize =
            terrain.terrainData.size;

        for (int index = 0;
             index < points.Length;
             index++)
        {
            Vector3 point =
                points[index];

            if (point.x < terrainOrigin.x ||
                point.x > terrainOrigin.x + terrainSize.x ||
                point.z < terrainOrigin.z ||
                point.z > terrainOrigin.z + terrainSize.z)
            {
                continue;
            }

            heights[found++] =
                SampleWorldHeight(
                    terrain,
                    point);
        }

        if (found == 0)
            return false;

        Array.Sort(
            heights,
            0,
            found);
        minimumHeight =
            heights[0];
        maximumHeight =
            heights[found - 1];
        // note: The upper-middle footprint sample favors slight natural embedding over a visible downslope air gap without lifting everything to one pathological corner.
        int contactIndex =
            Mathf.Clamp(
                Mathf.CeilToInt(
                    (found - 1) *
                    0.625f),
                0,
                found - 1);
        contactHeight =
            heights[contactIndex];
        return true;
    }

    public static bool TryGroundObject(
        GameObject root,
        Terrain terrain,
        float embedDepth,
        out float correction)
    {
        // note: Legacy callers still enter the universal placement authority; semantic inference keeps old spawn paths from bypassing category rules.
        return TryPlaceGroundedObject(
            root,
            terrain,
            YQGeneratedWorldPlacementCategory.Automatic,
            embedDepth,
            out correction);
    }

    public static bool TryPlaceGroundedObject(
        GameObject root,
        Terrain terrain,
        YQGeneratedWorldPlacementCategory category,
        float embedDepth,
        out float correction)
    {
        correction =
            0f;

        if (root == null ||
            terrain == null ||
            terrain.terrainData == null ||
            YQTerrainSupportComposer.IsExplicitlySuspended(root))
        {
            return false;
        }

        YQGeneratedWorldPlacementCategory resolvedCategory =
            ResolvePlacementCategory(
                root,
                category);

        // note: Only natural dressing may follow the terrain normal; buildings, trees, and actors remain upright so grounding never corrupts authored traversal or animation axes.
        AlignNaturalObjectToSurface(
            root,
            terrain,
            resolvedCategory);

        if (!TryGetStableContactGeometry(
                root,
                out Bounds bounds,
                out float structuralBottom))
        {
            return false;
        }

        if (!TrySampleFootprintHeight(
                terrain,
                bounds,
                out float terrainContact,
                out float minimumTerrain,
                out float maximumTerrain))
        {
            return false;
        }

        float footprint =
            Mathf.Max(
                bounds.size.x,
                bounds.size.z);
        float terrainVariation =
            Mathf.Max(
                0f,
                maximumTerrain - minimumTerrain);
        float categoryEmbed =
            ResolvePlacementEmbedDepth(
                resolvedCategory,
                bounds,
                terrainVariation,
                embedDepth);

        if (resolvedCategory !=
                YQGeneratedWorldPlacementCategory.Structure &&
            resolvedCategory !=
                YQGeneratedWorldPlacementCategory.Actor)
        {
            // note: Rocks, props, and low vegetation contact through their actual visible bottom; weighted structural bottoms are reserved for multi-part buildings and characters.
            structuralBottom =
                bounds.min.y;
        }

        if (resolvedCategory ==
                YQGeneratedWorldPlacementCategory.Structure &&
            terrainVariation >
                Mathf.Clamp(
                    footprint * 0.10f,
                    0.55f,
                    3.5f))
        {
            // note: A rigid structure on residual slope favors the footprint median plus bounded burial; this closes downhill air gaps without dragging the full building below the lowest sample.
            terrainContact =
                Mathf.Lerp(
                    minimumTerrain,
                    terrainContact,
                    0.72f);
        }

        correction =
            terrainContact -
            structuralBottom -
            categoryEmbed;

        if (!IsFinite(correction) ||
            Mathf.Abs(correction) > 128f)
        {
            correction =
                0f;
            return false;
        }

        if (Mathf.Abs(correction) <
            0.002f)
        {
            correction =
                0f;
            return true;
        }

        Vector3 position =
            root.transform.position;
        position.y +=
            correction;
        root.transform.position =
            position;

        Rigidbody body =
            root.GetComponent<Rigidbody>();

        if (body != null)
        {
            // note: Keep this object's physics pose aligned locally; never force a generated-world-wide transform synchronization for one grounding correction.
            body.position =
                position;
        }

        return true;
    }

    private static YQGeneratedWorldPlacementCategory
        ResolvePlacementCategory(
            GameObject root,
            YQGeneratedWorldPlacementCategory requested)
    {
        if (requested !=
            YQGeneratedWorldPlacementCategory.Automatic)
        {
            return requested;
        }

        string semantic =
            (root != null
                ? root.name
                : string.Empty)
            .ToLowerInvariant();

        if (ContainsAny(
                semantic,
                "building", "house", "hut", "cabin", "tower",
                "ruin", "cave", "structure", "bridge", "wall"))
        {
            return YQGeneratedWorldPlacementCategory.Structure;
        }

        if (ContainsAny(
                semantic,
                "tree", "trunk", "pine", "spruce", "birch"))
        {
            return YQGeneratedWorldPlacementCategory.Tree;
        }

        if (ContainsAny(
                semantic,
                "rock", "stone", "boulder", "cliff"))
        {
            return YQGeneratedWorldPlacementCategory.Rock;
        }

        if (ContainsAny(
                semantic,
                "grass", "fern", "weed", "bush", "shrub", "flower"))
        {
            return YQGeneratedWorldPlacementCategory.Vegetation;
        }

        if (ContainsAny(
                semantic,
                "npc", "enemy", "creature", "resident", "goddess"))
        {
            return YQGeneratedWorldPlacementCategory.Actor;
        }

        return YQGeneratedWorldPlacementCategory.Prop;
    }

    private static float ResolvePlacementEmbedDepth(
        YQGeneratedWorldPlacementCategory category,
        Bounds bounds,
        float terrainVariation,
        float requestedEmbedDepth)
    {
        float requested =
            Mathf.Max(
                0f,
                requestedEmbedDepth);

        switch (category)
        {
            case YQGeneratedWorldPlacementCategory.Structure:
                // note: Structural burial grows only enough to hide a residual foundation seam and remains capped against swallowed doors or stairs.
                return Mathf.Clamp(
                    Mathf.Max(
                        requested,
                        terrainVariation * 0.18f),
                    0.015f,
                    Mathf.Min(
                        0.75f,
                        Mathf.Max(
                            0.08f,
                            bounds.size.y * 0.055f)));

            case YQGeneratedWorldPlacementCategory.Rock:
                return Mathf.Clamp(
                    Mathf.Max(
                        requested,
                        bounds.size.y * 0.055f),
                    0.035f,
                    0.55f);

            case YQGeneratedWorldPlacementCategory.Tree:
                return Mathf.Clamp(
                    Mathf.Max(
                        requested,
                        0.045f),
                    0.025f,
                    0.18f);

            case YQGeneratedWorldPlacementCategory.Vegetation:
                return Mathf.Clamp(
                    Mathf.Max(
                        requested,
                        bounds.size.y * 0.025f),
                    0.025f,
                    0.22f);

            case YQGeneratedWorldPlacementCategory.Actor:
                return Mathf.Clamp(
                    requested,
                    0f,
                    0.025f);

            default:
                return Mathf.Clamp(
                    Mathf.Max(
                        requested,
                        bounds.size.y * 0.018f),
                    0.015f,
                    0.28f);
        }
    }

    private static void AlignNaturalObjectToSurface(
        GameObject root,
        Terrain terrain,
        YQGeneratedWorldPlacementCategory category)
    {
        float maximumTilt;

        switch (category)
        {
            case YQGeneratedWorldPlacementCategory.Rock:
                maximumTilt = 24f;
                break;

            case YQGeneratedWorldPlacementCategory.Vegetation:
                maximumTilt = 11f;
                break;

            default:
                return;
        }

        TerrainData data =
            terrain.terrainData;
        Vector3 terrainOrigin =
            terrain.transform.position;
        Vector3 terrainSize =
            data.size;
        Vector3 position =
            root.transform.position;
        float normalizedX =
            (position.x - terrainOrigin.x) /
            Mathf.Max(
                0.001f,
                terrainSize.x);
        float normalizedZ =
            (position.z - terrainOrigin.z) /
            Mathf.Max(
                0.001f,
                terrainSize.z);

        if (normalizedX < 0f ||
            normalizedX > 1f ||
            normalizedZ < 0f ||
            normalizedZ > 1f)
        {
            return;
        }

        Vector3 surfaceNormal =
            data.GetInterpolatedNormal(
                normalizedX,
                normalizedZ);
        float surfaceAngle =
            Vector3.Angle(
                Vector3.up,
                surfaceNormal);

        if (!IsFiniteVector(surfaceNormal) ||
            surfaceAngle <= 0.05f)
        {
            return;
        }

        float tiltRatio =
            Mathf.Clamp01(
                maximumTilt /
                surfaceAngle);
        Quaternion cappedTilt =
            Quaternion.Slerp(
                Quaternion.identity,
                Quaternion.FromToRotation(
                    Vector3.up,
                    surfaceNormal),
                tiltRatio);
        float authoredYaw =
            root.transform.eulerAngles.y;

        // note: Natural placement keeps deterministic yaw while applying only a bounded surface tilt, preventing rocks from floating on slopes without toppling props downhill.
        root.transform.rotation =
            cappedTilt *
            Quaternion.Euler(
                0f,
                authoredYaw,
                0f);
    }

    private static bool IsStableContactRenderer(
        Renderer renderer)
    {
        if (renderer == null ||
            !renderer.enabled ||
            renderer is ParticleSystemRenderer ||
            renderer is TrailRenderer ||
            renderer is LineRenderer)
        {
            return false;
        }

        string objectName =
            (renderer.name ?? string.Empty)
                .ToLowerInvariant();

        if (ContainsAny(
                objectName,
                "particle", "vfx", "decal", "mist", "fog",
                "cloud", "lightbeam", "flare", "preview", "gizmo"))
        {
            return false;
        }

        Bounds bounds =
            renderer.bounds;
        return IsFiniteVector(bounds.center) &&
            IsFiniteVector(bounds.size) &&
            bounds.size.x > 0.01f &&
            bounds.size.y > 0.005f &&
            bounds.size.z > 0.01f;
    }

    private static bool IsFiniteVector(
        Vector3 value)
    {
        return IsFinite(value.x) &&
            IsFinite(value.y) &&
            IsFinite(value.z);
    }

    private static bool IsFinite(
        float value)
    {
        return !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }

    private static bool ContainsAny(
        string value,
        params string[] tokens)
    {
        if (string.IsNullOrEmpty(value) ||
            tokens == null)
        {
            return false;
        }

        for (int index = 0;
             index < tokens.Length;
             index++)
        {
            if (!string.IsNullOrEmpty(tokens[index]) &&
                value.Contains(tokens[index]))
            {
                return true;
            }
        }

        return false;
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
