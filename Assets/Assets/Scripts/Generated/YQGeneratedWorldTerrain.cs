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
    // note: Version three intentionally opts regenerated worlds into the macro-landform layout; the version remains part of the persisted terrain seed contract.
    public const string TerrainGenerationVersion =
    "generated_terrain_v3_macro_landforms";

    public const string RuntimeTerrainObjectName =
        "YQ_GENERATED_TERRAIN";

    public const float WorldSize =
        1024f;

    public const float TerrainHeight =
        140f;

    public const int HeightmapResolution =
        513;

    public const int MacroWaterBasinCount =
        2;

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

    private const float ContinentalNoiseScale =
        0.00072f;

    private const float RollingHillNoiseScale =
        0.0026f;

    private const float MountainRegionNoiseScale =
        0.00115f;

    private const float MountainRidgeNoiseScale =
        0.0034f;

    private const float DetailNoiseScale =
        0.018f;

    private const float ContinentalAmplitude =
        0.052f;

    private const float RollingHillAmplitude =
        0.042f;

    private const float DetailAmplitude =
        0.007f;

    private const float PrimaryBasinLongRadius =
        118f;

    private const float PrimaryBasinShortRadius =
        76f;

    private const float SecondaryBasinLongRadius =
        94f;

    private const float SecondaryBasinShortRadius =
        62f;

    private const float BasinWaterSurfaceNormalized =
        0.112f;

    private const float StartupFrameBudgetSeconds =
        0.003f;

    private const int HeightmapUploadRowsPerFrame =
        16;

    private struct MacroLandformSettings
    {
        public Vector2 ValleyAxis;
        public Vector2 ValleyNormal;
        public float ValleyOffset;
        public Vector2 PrimaryBasinCenter;
        public Vector2 SecondaryBasinCenter;
    }

    public struct MacroWaterBasinDescriptor
    {
        public readonly Vector3 CenterWorld;
        public readonly Vector2 LongAxisXZ;
        public readonly Vector2 ShortAxisXZ;
        public readonly float LongRadius;
        public readonly float ShortRadius;
        public readonly float WaterSurfaceY;

        internal MacroWaterBasinDescriptor(
            Vector3 centerWorld,
            Vector2 longAxisXZ,
            Vector2 shortAxisXZ,
            float longRadius,
            float shortRadius,
            float waterSurfaceY)
        {
            CenterWorld =
                centerWorld;
            LongAxisXZ =
                longAxisXZ;
            ShortAxisXZ =
                shortAxisXZ;
            LongRadius =
                longRadius;
            ShortRadius =
                shortRadius;
            WaterSurfaceY =
                waterSurfaceY;
        }

        // note: Value-only containment lets foliage and shoreline passes share the sculpted ellipse without allocating temporary collections.
        public bool ContainsXZ(
            Vector3 worldPosition,
            float radiusPadding = 0f)
        {
            Vector2 displacement =
                new Vector2(
                    worldPosition.x -
                        CenterWorld.x,
                    worldPosition.z -
                        CenterWorld.z);

            float safeLongRadius =
                Mathf.Max(
                    1f,
                    LongRadius +
                        radiusPadding);

            float safeShortRadius =
                Mathf.Max(
                    1f,
                    ShortRadius +
                        radiusPadding);

            float along =
                Vector2.Dot(
                    displacement,
                    LongAxisXZ) /
                safeLongRadius;

            float across =
                Vector2.Dot(
                    displacement,
                    ShortAxisXZ) /
                safeShortRadius;

            return
                along * along +
                across * across <=
                1f;
        }

        public bool IsBelowWaterSurface(
            Vector3 worldPosition,
            float radiusPadding = 0f)
        {
            return
                worldPosition.y <=
                    WaterSurfaceY &&
                ContainsXZ(
                    worldPosition,
                    radiusPadding);
        }
    }

    public static bool TryGetMacroWaterBasin(
        string worldSeed,
        Terrain terrain,
        int basinIndex,
        out MacroWaterBasinDescriptor descriptor)
    {
        descriptor = default;

        if (terrain == null ||
            terrain.terrainData == null ||
            basinIndex < 0 ||
            basinIndex >= MacroWaterBasinCount)
        {
            return false;
        }

        string safeSeed =
            string.IsNullOrWhiteSpace(worldSeed)
                ? "yourquest_default_world"
                : worldSeed.Trim();
        uint seedHash = StableHash32(
            TerrainGenerationVersion +
            "|" +
            safeSeed);
        MacroLandformSettings settings =
            CreateMacroLandformSettings(seedHash);
        Vector2 basinCenter =
            basinIndex == 0
                ? settings.PrimaryBasinCenter
                : settings.SecondaryBasinCenter;
        float longRadius =
            basinIndex == 0
                ? PrimaryBasinLongRadius
                : SecondaryBasinLongRadius;
        float shortRadius =
            basinIndex == 0
                ? PrimaryBasinShortRadius
                : SecondaryBasinShortRadius;
        float waterSurfaceY =
            terrain.transform.position.y +
            terrain.terrainData.size.y *
            BasinWaterSurfaceNormalized;

        // note: Environment, settlement repair, and terrain synthesis all derive water from the same versioned seed contract, preventing visual planes from drifting away from their basins.
        descriptor =
            new MacroWaterBasinDescriptor(
                new Vector3(
                    terrain.transform.position.x +
                        terrain.terrainData.size.x * 0.5f +
                        basinCenter.x,
                    waterSurfaceY,
                    terrain.transform.position.z +
                        terrain.terrainData.size.z * 0.5f +
                        basinCenter.y),
                settings.ValleyAxis,
                settings.ValleyNormal,
                longRadius,
                shortRadius,
                waterSurfaceY);

        return true;
    }

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

        Vector2 continentalOffset =
            SeedOffset(
                seedHash,
                0xA53C9E17u);

        Vector2 rollingHillOffset =
            SeedOffset(
                seedHash,
                0x71F42D89u);

        Vector2 mountainRegionOffset =
            SeedOffset(
                seedHash,
                0x1B87D4A3u);

        Vector2 mountainRidgeOffset =
            SeedOffset(
                seedHash,
                0xE1458C29u);

        Vector2 detailOffset =
            SeedOffset(
                seedHash,
                0xC3195A47u);

        Vector2 valleyOffset =
            SeedOffset(
                seedHash,
                0x5F2A73D1u);

        MacroLandformSettings landforms =
            CreateMacroLandformSettings(
                seedHash);

        for (int z = 0;
             z < resolution;
             z++)
        {
            FillHeightmapRow(
                heights,
                z,
                continentalOffset,
                rollingHillOffset,
                mountainRegionOffset,
                mountainRidgeOffset,
                detailOffset,
                valleyOffset,
                landforms);
        }

        return heights;
    }

    private static IEnumerator GenerateHeightmapRoutine(
        uint seedHash,
        float[,] heights)
    {
        Vector2 continentalOffset =
            SeedOffset(
                seedHash,
                0xA53C9E17u);

        Vector2 rollingHillOffset =
            SeedOffset(
                seedHash,
                0x71F42D89u);

        Vector2 mountainRegionOffset =
            SeedOffset(
                seedHash,
                0x1B87D4A3u);

        Vector2 mountainRidgeOffset =
            SeedOffset(
                seedHash,
                0xE1458C29u);

        Vector2 detailOffset =
            SeedOffset(
                seedHash,
                0xC3195A47u);

        Vector2 valleyOffset =
            SeedOffset(
                seedHash,
                0x5F2A73D1u);

        MacroLandformSettings landforms =
            CreateMacroLandformSettings(
                seedHash);

        float frameStartedAt =
            Time.realtimeSinceStartup;

        // note: Splitting every heightmap row into narrow column blocks prevents a single noise-heavy 513-sample row from causing a loading-frame spike.
        const int columnChunkSize =
            32;

        for (int z = 0;
             z < HeightmapResolution;
             z++)
        {
            for (int startX = 0;
                 startX < HeightmapResolution;
                 startX += columnChunkSize)
            {
                int endXExclusive =
                    Mathf.Min(
                        HeightmapResolution,
                        startX +
                            columnChunkSize);

                FillHeightmapRow(
                    heights,
                    z,
                    continentalOffset,
                    rollingHillOffset,
                    mountainRegionOffset,
                    mountainRidgeOffset,
                    detailOffset,
                    valleyOffset,
                    landforms,
                    startX,
                    endXExclusive);

                if (Time.realtimeSinceStartup - frameStartedAt >=
                    StartupFrameBudgetSeconds)
                {
                    yield return null;
                    frameStartedAt =
                        Time.realtimeSinceStartup;
                }
            }
        }
    }

    private static void FillHeightmapRow(
        float[,] heights,
        int z,
        Vector2 continentalOffset,
        Vector2 rollingHillOffset,
        Vector2 mountainRegionOffset,
        Vector2 mountainRidgeOffset,
        Vector2 detailOffset,
        Vector2 valleyOffset,
        MacroLandformSettings landforms,
        int startXInclusive = 0,
        int endXExclusive = -1)
    {
        int resolution =
            HeightmapResolution;

        int safeStartX =
            Mathf.Clamp(
                startXInclusive,
                0,
                resolution);

        int safeEndX =
            endXExclusive < 0
                ? resolution
                : Mathf.Clamp(
                    endXExclusive,
                    safeStartX,
                    resolution);

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

        for (int x = safeStartX;
             x < safeEndX;
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

            // note: Continental lift and restrained rolling hills establish broad, traversable lowlands before sharper mountain structure is applied.
            float continental =
                SignedFractalNoise(
                    worldX,
                    worldZ,
                    ContinentalNoiseScale,
                    continentalOffset,
                    3,
                    0.54f,
                    2f);

            float rollingHills =
                SignedFractalNoise(
                    worldX,
                    worldZ,
                    RollingHillNoiseScale,
                    rollingHillOffset,
                    3,
                    0.5f,
                    2f);

            float detail =
                Mathf.PerlinNoise(
                    worldX *
                        DetailNoiseScale +
                        detailOffset.x,
                    worldZ *
                        DetailNoiseScale +
                        detailOffset.y);

            detail =
                detail * 2f - 1f;

            float alongValley =
                worldX *
                    landforms.ValleyAxis.x +
                worldZ *
                    landforms.ValleyAxis.y;

            float acrossValley =
                worldX *
                    landforms.ValleyNormal.x +
                worldZ *
                    landforms.ValleyNormal.y;

            float valleyMeander =
                (Mathf.PerlinNoise(
                    alongValley *
                        0.0019f +
                        valleyOffset.x,
                    valleyOffset.y) *
                    2f -
                    1f) *
                82f;

            float valleyDistance =
                Mathf.Abs(
                    acrossValley -
                    landforms.ValleyOffset -
                    valleyMeander);

            float valleyMask =
                1f -
                Smooth01(
                    Mathf.InverseLerp(
                        38f,
                        172f,
                        valleyDistance));

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

            float perimeterMountainBias =
                Smooth01(
                    Mathf.InverseLerp(
                        0.5f,
                        0.94f,
                        edgeDistance));

            float mountainRegion =
                Smooth01(
                    Mathf.InverseLerp(
                        0.5f,
                        0.69f,
                        FractalNoise(
                            worldX,
                            worldZ,
                            MountainRegionNoiseScale,
                            mountainRegionOffset,
                            2,
                            0.58f,
                            2f)));

            float ridgeShape =
                Mathf.Pow(
                    Smooth01(
                        Mathf.InverseLerp(
                            0.5f,
                            0.94f,
                            RidgedFractalNoise(
                                worldX,
                                worldZ,
                                MountainRidgeNoiseScale,
                                mountainRidgeOffset,
                                3,
                                0.52f,
                                2.08f))),
                    1.45f);

            float originDistance =
                Mathf.Sqrt(
                    worldX * worldX +
                    worldZ * worldZ);

            float originMountainRelease =
                Smooth01(
                    Mathf.InverseLerp(
                        OriginBlendRadius,
                        OriginBlendRadius +
                            115f,
                        originDistance));

            float mountainMask =
                Mathf.Clamp01(
                    mountainRegion *
                        0.82f +
                    perimeterMountainBias *
                        0.62f);

            mountainMask *=
                1f -
                valleyMask *
                    0.76f;

            mountainMask *=
                Mathf.Lerp(
                    0.08f,
                    1f,
                    originMountainRelease);

            float lowlandNoiseRetention =
                1f -
                mountainMask *
                    0.58f;

            float height =
                BaseHeightNormalized +
                continental *
                    ContinentalAmplitude +
                rollingHills *
                    RollingHillAmplitude *
                    lowlandNoiseRetention +
                detail *
                    DetailAmplitude *
                    lowlandNoiseRetention;

            // note: Regional masks keep mountain mass out of the main valley and origin approach while ridged noise supplies readable distant silhouettes instead of uniform bumps.
            height +=
                mountainMask *
                (0.038f +
                 ridgeShape *
                    0.265f);

            float valleyGrade =
                (Mathf.PerlinNoise(
                    alongValley *
                        0.00085f +
                        valleyOffset.y,
                    valleyOffset.x) *
                    2f -
                    1f) *
                0.011f;

            float valleyFloor =
                BaseHeightNormalized -
                0.032f +
                continental *
                    0.016f +
                valleyGrade;

            height =
                Mathf.Lerp(
                    height,
                    Mathf.Min(
                        height,
                        valleyFloor +
                            rollingHills *
                                0.006f),
                    valleyMask *
                        0.78f);

            Vector2 worldPoint =
                new Vector2(
                    worldX,
                    worldZ);

            float primaryBasinMask =
                EllipticalBasinMask(
                    worldPoint -
                        landforms.PrimaryBasinCenter,
                    landforms.ValleyAxis,
                    landforms.ValleyNormal,
                    118f,
                    76f);

            float secondaryBasinMask =
                EllipticalBasinMask(
                    worldPoint -
                        landforms.SecondaryBasinCenter,
                    landforms.ValleyAxis,
                    landforms.ValleyNormal,
                    94f,
                    62f);

            float basinMask =
                Mathf.Max(
                    primaryBasinMask,
                    secondaryBasinMask);

            float basinFloor =
                0.078f +
                detail *
                    0.0025f;

            // note: Wide, gently shelved depressions reserve deterministic lake or wetland beds without forcing a runtime water implementation into the terrain authority.
            height =
                Mathf.Lerp(
                    height,
                    Mathf.Min(
                        height,
                        basinFloor),
                    basinMask *
                        0.94f);

            float originBlend =
                Smooth01(
                    Mathf.InverseLerp(
                        OriginPlateauRadius,
                        OriginBlendRadius,
                        originDistance));

            heights[z, x] =
                Mathf.Clamp(
                    Mathf.Lerp(
                        BaseHeightNormalized,
                        height,
                        originBlend),
                    0.025f,
                    0.88f);
        }
    }

    private static MacroLandformSettings
        CreateMacroLandformSettings(
            uint seedHash)
    {
        float angle =
            Hash01(
                seedHash,
                0x86D41B59u) *
            Mathf.PI;

        Vector2 valleyAxis =
            new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle));

        Vector2 valleyNormal =
            new Vector2(
                -valleyAxis.y,
                valleyAxis.x);

        float valleyOffset =
            Mathf.Lerp(
                -62f,
                62f,
                Hash01(
                    seedHash,
                    0x3C6EF372u));

        float primaryAlong =
            Mathf.Lerp(
                205f,
                286f,
                Hash01(
                    seedHash,
                    0xDAA66D2Bu));

        float primaryAcross =
            Mathf.Lerp(
                -132f,
                132f,
                Hash01(
                    seedHash,
                    0x78DDE6E4u));

        float secondaryAlong =
            -Mathf.Lerp(
                196f,
                274f,
                Hash01(
                    seedHash,
                    0x1715609Du));

        float secondaryAcross =
            Mathf.Lerp(
                -126f,
                126f,
                Hash01(
                    seedHash,
                    0xB54CDA56u));

        // note: Basin centers are derived once per seed and remain far enough from the fixed origin to preserve Vey's authored plateau.
        return
            new MacroLandformSettings
            {
                ValleyAxis =
                    valleyAxis,
                ValleyNormal =
                    valleyNormal,
                ValleyOffset =
                    valleyOffset,
                PrimaryBasinCenter =
                    valleyAxis *
                        primaryAlong +
                    valleyNormal *
                        primaryAcross,
                SecondaryBasinCenter =
                    valleyAxis *
                        secondaryAlong +
                    valleyNormal *
                        secondaryAcross
            };
    }

    private static float EllipticalBasinMask(
        Vector2 displacement,
        Vector2 longAxis,
        Vector2 shortAxis,
        float longRadius,
        float shortRadius)
    {
        float along =
            Vector2.Dot(
                displacement,
                longAxis) /
            Mathf.Max(
                1f,
                longRadius);

        float across =
            Vector2.Dot(
                displacement,
                shortAxis) /
            Mathf.Max(
                1f,
                shortRadius);

        float ellipticalDistance =
            Mathf.Sqrt(
                along * along +
                across * across);

        return
            1f -
            Smooth01(
                Mathf.InverseLerp(
                    0.28f,
                    1f,
                    ellipticalDistance));
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

    private static float SignedFractalNoise(
        float worldX,
        float worldZ,
        float baseScale,
        Vector2 offset,
        int octaves,
        float persistence,
        float lacunarity)
    {
        return
            FractalNoise(
                worldX,
                worldZ,
                baseScale,
                offset,
                octaves,
                persistence,
                lacunarity) *
                2f -
            1f;
    }

    private static float RidgedFractalNoise(
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
            float noise =
                Mathf.PerlinNoise(
                    worldX *
                        baseScale *
                        frequency +
                        offset.x,
                    worldZ *
                        baseScale *
                        frequency +
                        offset.y);

            float ridge =
                1f -
                Mathf.Abs(
                    noise *
                        2f -
                    1f);

            sum +=
                ridge *
                ridge *
                amplitude;

            normalization +=
                amplitude;

            amplitude *=
                persistence;

            frequency *=
                lacunarity;
        }

        return
            normalization > 0f
                ? sum /
                    normalization
                : 0f;
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

    private static float Hash01(
        uint seed,
        uint salt)
    {
        uint value =
            Mix(
                seed ^
                salt);

        return
            (value & 0x00FFFFFFu) /
            16777215f;
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
