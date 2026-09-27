using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class YQGeneratedWorldEnvironment
{
    // note: Surface the active synchronous stage when a streamed ecology coroutine exceeds its shared publication slice.
    internal static string LastSemanticChunkScatterStep { get; private set; } = string.Empty;
    // note: Break one expensive path projection into authority validation and geometry stages so runtime evidence identifies the true source of a hitch.
    internal static string LastSemanticPathProjectionBreakdown { get; private set; } = string.Empty;
    private static GeneratedWorldPlanRecord _cachedPathProjectionPlan;
    private static YQPreparedSpatialMaterializationV2 _cachedPathProjectionPrepared;
    private static GeneratedSpatialWorldPlanV2Record _cachedPathProjectionArtifact;
    private static string _cachedPathProjectionContentHash = string.Empty;
    private static List<string> _cachedPathProjectionSettlementIds = new List<string>();
    private static List<bool> _cachedPathProjectionHasProceduralLayouts = new List<bool>();
    private static List<LivedPathSegment> _cachedPathProjectionSegments = new List<LivedPathSegment>();
    private static List<V2PathProjectionTileCacheEntry> _cachedPathProjectionTiles = new List<V2PathProjectionTileCacheEntry>();
    private static int _semanticPathProjectionCacheHits;
    private static int _semanticPathProjectionCacheMisses;
    private static int _semanticPathProjectionWorldBuilds;
    // note: Keep route bounds views below the visible-envelope size while the accepted world projection itself remains a single shared list.
    private const int MaximumCachedPathProjectionTiles = 128;
    // note: Expose one-session counters in the PlaySafe status receipt to confirm that streaming cells reuse the derived route projection.
    public static int SemanticPathProjectionCacheHits => _semanticPathProjectionCacheHits;
    public static int SemanticPathProjectionCacheMisses => _semanticPathProjectionCacheMisses;
    public static int SemanticPathProjectionWorldBuilds => _semanticPathProjectionWorldBuilds;
    internal static string LastRoadsideDressingStep { get; private set; } = string.Empty;
    internal static string LastShorelineDressingStep { get; private set; } = string.Empty;
    internal static string LastStreamedDetailStep { get; private set; } = string.Empty;
    internal static float MaximumStreamedBiomeComputeSeconds { get; private set; }
    internal static float MaximumStreamedBiomeUploadSeconds { get; private set; }
    internal static float MaximumStreamedDetailNormalSeconds { get; private set; }
    internal static float MaximumStreamedDetailComputeSeconds { get; private set; }
    internal static float MaximumStreamedDetailUploadSeconds { get; private set; }

    // note: Separate worker preparation from Unity's main-thread native terrain calls in the runtime acceptance receipt.
    internal static void RecordStreamedBiomeComputeSeconds(float seconds)
    {
        MaximumStreamedBiomeComputeSeconds = Mathf.Max(MaximumStreamedBiomeComputeSeconds, seconds);
    }

    internal static void RecordStreamedBiomeUploadSeconds(float seconds)
    {
        MaximumStreamedBiomeUploadSeconds = Mathf.Max(MaximumStreamedBiomeUploadSeconds, seconds);
    }

    internal static void RecordStreamedDetailNormalSeconds(float seconds)
    {
        MaximumStreamedDetailNormalSeconds = Mathf.Max(MaximumStreamedDetailNormalSeconds, seconds);
    }

    internal static void RecordStreamedDetailComputeSeconds(float seconds)
    {
        MaximumStreamedDetailComputeSeconds = Mathf.Max(MaximumStreamedDetailComputeSeconds, seconds);
    }

    internal static void RecordStreamedDetailUploadSeconds(float seconds)
    {
        MaximumStreamedDetailUploadSeconds = Mathf.Max(MaximumStreamedDetailUploadSeconds, seconds);
    }

    // note: Keep required ecology instance work overlapped but bounded across independent canopy, understory, and shrub layers.
    private const int MaximumConcurrentRequiredEcologyInstantiations = 2;
    private const float RequiredEcologyLayerSliceSeconds = 0.004f;

    private sealed class RequiredEcologyLayerWork
    {
        public readonly string label;
        public readonly Stack<IEnumerator> iterators = new Stack<IEnumerator>();
        public AsyncInstantiateOperation<GameObject> pendingInstantiation;
        public CustomYieldInstruction pendingCustomYield;
        public int lastAdvancedFrame = -1;

        public RequiredEcologyLayerWork(string label, IEnumerator iterator)
        {
            this.label = label ?? string.Empty;
            if (iterator != null)
                iterators.Push(iterator);
        }
    }

    // note: Two-metre splat spacing keeps narrow accepted paths represented; four-metre pixels miss diagonal centre lines entirely.
    private const int AlphamapResolution =
        512;

    // note: Clear world and bounded tile projections when Unity starts a new Play session without a domain reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSemanticPathProjectionCache()
    {
        _cachedPathProjectionPlan = null;
        _cachedPathProjectionPrepared = null;
        _cachedPathProjectionArtifact = null;
        _cachedPathProjectionContentHash = string.Empty;
        _cachedPathProjectionSettlementIds.Clear();
        _cachedPathProjectionHasProceduralLayouts.Clear();
        _cachedPathProjectionSegments.Clear();
        _cachedPathProjectionTiles.Clear();
        _semanticPathProjectionCacheHits = 0;
        _semanticPathProjectionCacheMisses = 0;
        _semanticPathProjectionWorldBuilds = 0;
        LastSemanticPathProjectionBreakdown = string.Empty;
    }

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

    // note: Native terrain vegetation carries most of the world density, so raising this baseline fills ecological gaps without multiplying expensive authored GameObjects.
    private const int BaseVegetationPerRegion =
        168;

    // note: A 256-cell detail grid keeps the same authored density logic while avoiding a 262k-cell startup projection on every generated world.
    private const int TerrainDetailResolution =
        256;

    private const int TerrainDetailPatchResolution =
        16;

    // note: The native tree cap is raised enough to fill distant biome silhouettes while remaining bounded for predictable loading cost.
    private const int MaximumTerrainTreeInstances =
        3400;

    private const int MaximumTerrainTreePrototypes =
        16;

    private const int MaximumTerrainDetailPrototypes =
        8;

    // note: Roads sit just above their independently sampled edge contacts, preventing z-fighting without creating a visible hover.
    internal const float RoadSurfaceOffset = 0.028f;

    // note: The painted shoulder is intentionally narrower than the authored route width so roads read as worn ground with a soft edge instead of a second terrain slab.
    private const float LivedPathShoulderScale = 0.58f;

    // note: Keep the road splat readable while allowing biome detail to remain visible through the shoulder transition.
    private const float LivedPathTerrainWeight = 3.8f;

    private static readonly string[] ApprovedUrpConiferPrefabs =
    {
        // note: These approved terrain-tree supplements use assets present in the checkout; stale Forst paths previously made the supplement a no-op.
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ScotsPineTypeA.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ScotsPineTypeB.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Alder.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Sycamore.prefab"
    };

    private static readonly string[] ApprovedVisibleTreePrefabs =
    {
        // note: Keep this family rooted in prefab paths present in the current checkout; stale Forst paths previously consumed deterministic canopy attempts without producing an instance.
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Alder.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Sycamore.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ThinTree.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ScotsPineTypeA.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ScotsPineTypeB.prefab"
    };

    // note: Dry canopy still follows the four-variant beta diversity rule; lower density is controlled by the target budget, not by collapsing the approved family to two silhouettes.
    private static readonly string[] ApprovedDryTreePrefabs =
    {
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Mimosa.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ThinTree.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Alder.prefab",
        "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Sycamore.prefab"
    };

    private static readonly string[] ApprovedFallbackUnderstoryPrefabs =
    {
        "Assets/YughuesFreeBushes2018/Prefabs/P_Bush01.prefab",
        "Assets/YughuesFreeBushes2018/Prefabs/P_Bush02.prefab",
        "Assets/YughuesFreeBushes2018/Prefabs/P_Bush03.prefab",
        "Assets/YughuesFreeBushes2018/Prefabs/P_Bush04.prefab",
        "Assets/YughuesFreeBushes2018/Prefabs/P_Bush05.prefab",
        // note: Use authored grass meshes as a real near-field undergrowth layer when a palette has no approved low foliage entry.
        "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_GrassMesh.prefab",
        "Assets/BefourStudios/AncientDesertRuins/Art/Prefabs/SM_Grass1.prefab",
        "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_Grass01.prefab"
    };

    // note: Deadfall uses existing imported log props so the streamed ecology layer has a physical forest-floor story without inventing a new asset family.
    private static readonly string[] ApprovedDeadfallPrefabs =
    {
        "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Log.prefab",
        "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/SM_LogStackSet.prefab"
    };

    private static readonly string[] ApprovedMacroWaterPrefabs =
    {
        "Assets/HIVEMIND/GladitorArena/HDRP(Default)/Art/Prefabs/SM_Water.prefab",
        "Assets/HIVEMIND/CaveOfHiddenTomb/HDRP (Default)/Art/Prefabs/SM_Water_01.prefab",
        "Assets/HIVEMIND/CaveOfHiddenTomb/HDRP (Default)/Art/Prefabs/SM_Water_02.prefab"
    };

    internal static Renderer ResolveApprovedWaterMaterialSource(YQRuntimeWorldAssetRegistry registry)
    {
        // note: Streamed water uses the same approved imported source as origin water, resolved through the existing lazy asset registry.
        if (registry == null)
            return null;
        for (int index = 0; index < ApprovedMacroWaterPrefabs.Length; index++)
        {
            GameObject prefab = registry.ResolvePrefab(ApprovedMacroWaterPrefabs[index]);
            if (prefab == null)
                continue;
            Renderer renderer = prefab.GetComponentInChildren<Renderer>(true);
            if (renderer != null && renderer.sharedMaterial != null)
                return renderer;
        }
        return null;
    }

    private const int BaseRockScatterPerRegion =
        36;

    private const int BaseLandformsPerRegion =
        7;

    private const int BaseAmbientEncounterGroupsPerRegion =
        1;

    private const int BaseTreasurePerRegion =
        2;

    private const int MaxOversizedWildernessWarningLogs =
        8;

    private static int _oversizedWildernessWarningLogs;

    private static readonly List<Mesh> GeneratedMacroWaterMeshes =
        new List<Mesh>();

    private static readonly List<Mesh> GeneratedLivedPathMeshes =
        new List<Mesh>();

    // note: Runtime tint instances keep approved source materials immutable while giving the lived road a coherent packed-earth read.
    private static readonly List<Material> GeneratedLivedPathMaterials =
        new List<Material>();

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
        // note: V2 route identity lets crossing detection continue across control-point segments instead of splitting a bridge at a waypoint.
        public string routeId;

        public Vector2 start;

        public Vector2 end;

        public float halfWidth;

        public float shoulderWidth;

        public float curveAmplitude;

        public float curvePhase;

        public bool terraced;
    }

    private sealed class V2PathProjectionTileCacheEntry
    {
        public Vector3 terrainPosition;
        public Vector3 terrainSize;
        public readonly List<LivedPathSegment> segments = new List<LivedPathSegment>();
    }

    private sealed class LivedPathVisualReport
    {
        public int requiredSegments;
        public int builtSegments;
        public int rejectedSegments;
        public int roadObjects;

        public bool IsComplete =>
            requiredSegments > 0 && builtSegments == requiredSegments &&
            rejectedSegments == 0 && roadObjects > 0;
    }

    public readonly struct LivedPathTerrainReservation
    {
        public readonly Vector2 center;

        public readonly float radius;
        public readonly Vector2 halfSize;
        public readonly float heading;

        public LivedPathTerrainReservation(
            Vector3 worldCenter,
            float protectedRadius)
        {
            // note: The construction prepass passes its real authored shelf radius so road grading cannot reshape peripheral foundations.
            center = new Vector2(worldCenter.x, worldCenter.z);
            radius = Mathf.Max(0f, protectedRadius);
            halfSize = Vector2.zero;
            heading = 0f;
        }

        public LivedPathTerrainReservation(Vector3 worldCenter, Vector2 protectedHalfSize, float yaw)
        {
            // note: Oriented parcel cores preserve foundations while allowing roads to grade the intervening terrain.
            center = new Vector2(worldCenter.x, worldCenter.z);
            radius = 0f;
            halfSize = protectedHalfSize;
            heading = yaw;
        }
    }

    private sealed class MacroWaterSet
    {
        public readonly YQGeneratedWorldTerrain.MacroWaterBasinDescriptor[] basins =
            new YQGeneratedWorldTerrain.MacroWaterBasinDescriptor[
                YQGeneratedWorldTerrain.MacroWaterBasinCount];

        public int count;

        public YQPreparedSpatialMaterializationV2 preparedV2;
    }

    private sealed class WildernessBuildStats
    {
        public int waterBodies;

        public int roadsideDressing;

        public int shorelineDressing;

        public int visibleTrees;

        public int vegetation;

        public int rocks;

        public int caves;

        public int ambientEnemies;

        public int treasure;

        public int curatedPoiHuts;
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

        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Shaping landforms and roads",
            3,
            8,
            "Regional hills, ridges, basins, and mountain silhouettes are formed",
            0.695f);
        YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();

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

    public static IEnumerator RepairWaterBanksRoutine(Terrain terrain, GeneratedWorldPlanRecord plan, List<LivedPathTerrainReservation> reservations)
    {
        if (!YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan)) yield break;
        if (!YQSpatialBlueprintTerrainSamplerV2.TryPrepare(plan, out var sampler, out string failure))
            throw new InvalidOperationException("Water bank construction failed: " + failure);
        // note: Final bank repair only raises missing support within the accepted shoreline band; interiors and unrelated terrain remain intact.
        var data = terrain.terrainData;
        int resolution = data.heightmapResolution;
        var heights = data.GetHeights(0, 0, resolution, resolution);
        var original = (float[,])heights.Clone();
        var platformLift = new float[reservations.Count];
        for (int z = 0; z < resolution; z++)
        {
            float worldZ = terrain.transform.position.z + z * data.size.z / (resolution - 1f);
            for (int x = 0; x < resolution; x++)
            {
                float worldX = terrain.transform.position.x + x * data.size.x / (resolution - 1f);
                if (IsInsideLivedPathTerrainReservation(reservations,
                        new Vector2(worldX, worldZ)))
                {
                    // note: Reviewed parcel and entrance terrain own their final datum; shoreline support must terminate at the apron boundary.
                    continue;
                }
                var sample = sampler.Sample(worldX, worldZ);
                if (sample.bankSupportMask <= 0f) continue;
                float target = sample.waterSurfaceNormalized + .2f / data.size.y;
                heights[z, x] = Mathf.Lerp(heights[z, x], Mathf.Max(heights[z, x], target), sample.bankSupportMask);
                for (int p = 0; p < reservations.Count; p++)
                    if (BankPlatformDistance(reservations[p], worldX, worldZ) <= 0f)
                        platformLift[p] = Mathf.Max(platformLift[p], heights[z, x] - original[z, x]);
            }
            // note: Keep the normal loading presentation responsive while checking the finite terrain grid.
            if (z % 8 == 0)
            {
                // note: A completed bank strip is genuine generation progress; refresh the builder watchdog only after that bounded slice is finished.
                YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                yield return null;
            }
        }
        // note: Raise intersecting prepared platforms uniformly, preserving reviewed footing depths instead of pushing a bank through one foundation corner.
        for (int z = 0; z < resolution; z++)
        {
            float worldZ = terrain.transform.position.z + z * data.size.z / (resolution - 1f);
            for (int x = 0; x < resolution; x++)
            {
                float worldX = terrain.transform.position.x + x * data.size.x / (resolution - 1f);
                if (IsInsideLivedPathTerrainReservation(reservations,
                        new Vector2(worldX, worldZ)))
                {
                    // note: Do not replay a bank lift over an accepted foundation after the shoreline pass has already finished.
                    continue;
                }
                for (int p = 0; p < reservations.Count; p++)
                {
                    if (platformLift[p] <= 0f) continue;
                    float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(BankPlatformDistance(reservations[p], worldX, worldZ) / 8f));
                    heights[z, x] = Mathf.Max(heights[z, x], original[z, x] + platformLift[p] * weight);
                }
            }
            if (z % 8 == 0)
            {
                // note: A completed platform-lift strip is genuine generation progress; keep the watchdog tied to work rather than coroutine suspension.
                YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                yield return null;
            }
        }
        data.SetHeightsDelayLOD(0, 0, heights);
    }

    public static IEnumerator RepairAcceptedWaterChannelsRoutine(Terrain terrain, GeneratedWorldPlanRecord plan,
        IReadOnlyList<LivedPathTerrainReservation> protectedFootprints = null)
    {
        if (!YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) ||
            terrain == null || terrain.terrainData == null ||
            !YQSpatialBlueprintTerrainSamplerV2.TryPrepare(plan, out var sampler, out string failure))
        {
            yield break;
        }

        // note: Construction and road grading can refill a previously carved channel; recut only accepted water feature bounds after those systems finish so every published ribbon has visible terrain clearance.
        var data = terrain.terrainData;
        int resolution = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, resolution, resolution);
        int repaired = 0;
        YQSpatialBlueprintV2 blueprint = plan.spatialPlanV2.blueprint;
        for (int waterIndex = 0;
             waterIndex < blueprint.hydrology.Count;
             waterIndex++)
        {
            YQHydrologyFeatureV2 feature = blueprint.hydrology[waterIndex];
            if (feature == null || feature.controlPoints == null || feature.controlPoints.Count == 0)
                continue;
            // note: River ravines need the same broad sidewall allowance used by the sampler's grade-limited cut; a narrow repair box leaves hard vertical terrain at the water edge.
            float padding = feature.kind == YQHydrologyKindV2.River || feature.kind == YQHydrologyKindV2.Waterfall
                ? Mathf.Max(24f, feature.nominalWidth * 2.25f + 12f)
                : Mathf.Max(8f, feature.nominalWidth * .75f + 6f);
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumZ = float.PositiveInfinity;
            float maximumZ = float.NegativeInfinity;
            for (int pointIndex = 0; pointIndex < feature.controlPoints.Count; pointIndex++)
            {
                YQBlueprintPointV2 point = feature.controlPoints[pointIndex];
                if (point == null) continue;
                minimumX = Mathf.Min(minimumX, point.x);
                maximumX = Mathf.Max(maximumX, point.x);
                minimumZ = Mathf.Min(minimumZ, point.z);
                maximumZ = Mathf.Max(maximumZ, point.z);
            }
            int minimumCellX = Mathf.Clamp(Mathf.FloorToInt((minimumX - padding - terrain.transform.position.x) / data.size.x * (resolution - 1)), 0, resolution - 1);
            int maximumCellX = Mathf.Clamp(Mathf.CeilToInt((maximumX + padding - terrain.transform.position.x) / data.size.x * (resolution - 1)), 0, resolution - 1);
            int minimumCellZ = Mathf.Clamp(Mathf.FloorToInt((minimumZ - padding - terrain.transform.position.z) / data.size.z * (resolution - 1)), 0, resolution - 1);
            int maximumCellZ = Mathf.Clamp(Mathf.CeilToInt((maximumZ + padding - terrain.transform.position.z) / data.size.z * (resolution - 1)), 0, resolution - 1);
            for (int z = minimumCellZ; z <= maximumCellZ; z++)
            {
                float worldZ = terrain.transform.position.z + z * data.size.z / (resolution - 1f);
                for (int x = minimumCellX; x <= maximumCellX; x++)
                {
                    float worldX = terrain.transform.position.x + x * data.size.x / (resolution - 1f);
                    YQSpatialTerrainSampleV2 sample = sampler.Sample(worldX, worldZ);
                    if (IsInsideLivedPathTerrainReservation(protectedFootprints,
                            new Vector2(worldX, worldZ)))
                    {
                        // note: Accepted parcel and entrance terrain remain authoritative when a broad water repair overlaps their edge.
                        continue;
                    }
                    // note: Leave authored road decks intact; bridge-specific channel restoration already handles their underpass geometry.
                    // note: Recut the accepted shoreline transition as well as the deep core so confluences and cave-like banks do not retain a rectangular earth wall.
                    if (sample.waterMask < .30f || sample.routeMask > .65f)
                        continue;
                    float target = Mathf.Clamp01(sample.elevationNormalized);
                    if (heights[z, x] <= target + .0005f)
                        continue;
                    float strength = Mathf.InverseLerp(.30f, 1f, sample.waterMask);
                    heights[z, x] = Mathf.Lerp(heights[z, x], target, strength);
                    repaired++;
                }
                // note: Publish each accepted feature in small slices so only local water bounds consume startup frames.
                if (z % 8 == 0)
                {
                    // note: A completed water-feature strip is genuine generation progress; do not let a large accepted river consume the watchdog while yielding normally.
                    YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                    yield return null;
                }
            }
        }
        data.SetHeightsDelayLOD(0, 0, heights);
        Debug.Log("[WORLDGEN WATER] Accepted wet cores recut after construction grading. Samples=" + repaired + ", samplerFailure=" + failure);
    }

    private static float BankPlatformDistance(LivedPathTerrainReservation reservation, float x, float z)
    {
        // note: Preserve the existing circular or oriented parcel boundary rather than inventing another platform footprint.
        Vector2 delta = new Vector2(x, z) - reservation.center;
        if (reservation.halfSize == Vector2.zero) return delta.magnitude - reservation.radius;
        float angle = reservation.heading * Mathf.Deg2Rad;
        Vector2 local = new Vector2(Mathf.Cos(angle) * delta.x - Mathf.Sin(angle) * delta.y,
            Mathf.Sin(angle) * delta.x + Mathf.Cos(angle) * delta.y);
        return Mathf.Max(Mathf.Abs(local.x) - reservation.halfSize.x, Mathf.Abs(local.y) - reservation.halfSize.y);
    }

    public static IEnumerator BuildTerrainSurfaceRoutine(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        Action<bool> completed = null)
    {
        if (terrain == null || terrain.terrainData == null ||
            plan == null || registry == null)
        {
            completed?.Invoke(false);
            yield break;
        }

        List<RegionSurface> surfaces = null;
        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Painting the terrain",
            4,
            8,
            "Resolving approved biome materials and path texture masks",
            0.74f);
        // note: Production paints after every construction pad is finalized, so slope stone, biome detail, and the authored approach describe the terrain the player actually collides with.
        yield return ApplyRegionalTerrainLayersRoutine(
            terrain,
            plan,
            registry,
            result => surfaces = result);

        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Painting the terrain",
            4,
            8,
            "Publishing terrain texture tiles and continuous road geometry",
            0.755f);

        LivedPathVisualReport roadReport = null;
        // note: Validate every road against published native terrain layers; duplicate opaque road ribbons would hide the blended shoulders.
        yield return BuildLivedPathVisualsRoutine(
            terrain,
            plan,
            registry,
            report => roadReport = report);

        // note: A single localized wet or shoreline texel can miss terrain paint while the route remains physically bridgeable; keep that route eligible for crossing construction instead of rejecting the whole world.
        bool mandatoryRoadsReady =
            roadReport != null && roadReport.requiredSegments > 0 &&
            roadReport.builtSegments >= roadReport.requiredSegments -
                Mathf.Max(1, Mathf.FloorToInt(roadReport.requiredSegments * 0.05f)) &&
            roadReport.roadObjects > 0;
        if (!mandatoryRoadsReady)
        {
            Debug.LogError(
                "[WORLDGEN ERROR] Mandatory road presentation failed. " +
                "Seed=" + plan.worldSeed + ", pass=terrain_surface, " +
                "requiredSegments=" + (roadReport != null ? roadReport.requiredSegments : 0) +
                ", builtSegments=" + (roadReport != null ? roadReport.builtSegments : 0) +
                ", rejectedSegments=" + (roadReport != null ? roadReport.rejectedSegments : 0) +
                ", roadObjects=" + (roadReport != null ? roadReport.roadObjects : 0) + ".");
        }

        Debug.Log(
            "[YQGeneratedWorldEnvironment] TERRAIN SURFACE READY\n" +
            "Terrain layers: " +
            (terrain.terrainData.terrainLayers != null
                ? terrain.terrainData.terrainLayers.Length
                : 0) +
            "\nRegion surface mappings: " +
            (surfaces != null ? surfaces.Count : 0) +
            "\nRequired road segments: " + (roadReport != null ? roadReport.requiredSegments : 0) +
            "\nBuilt road segments: " + (roadReport != null ? roadReport.builtSegments : 0) +
            "\nRejected road segments: " + (roadReport != null ? roadReport.rejectedSegments : 0) +
            "\nPublished road terrain surfaces: " + (roadReport != null ? roadReport.roadObjects : 0));
        completed?.Invoke(mandatoryRoadsReady);
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

        // note: Publish the accepted path count before the expensive profile solve so a startup pause distinguishes graph construction from heightmap rasterization.
        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Shaping landforms and roads",
            4,
            9,
            "Preparing " + paths.Count + " accepted route spans",
            0.727f);

        TerrainData data = terrain.terrainData;
        int resolution = data.heightmapResolution;
        float[,] heights = new float[resolution, resolution];
        // note: Batch route raster and projection work into measured eight-millisecond startup slices; the accepted route math stays unchanged while frame-handoff overhead drops sharply.
        const float routeWorkSliceSeconds = 0.008f;

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
            // note: Each copied height strip is completed work and therefore advances the bounded initial-generation watchdog.
            YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
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

        // note: Solve connected roads together before any road writes; segment endpoints and crossing streets cannot retain independent cliff heights.
        List<float[]> networkProfiles = null;
        yield return BuildConnectedRoadProfiles(paths, heights, terrainPosition, terrainSize,
            protectedFootprints, result => networkProfiles = result);
        // note: Separate the connected-grade solver from the raster pass in the live startup receipt so a bounded solver failure cannot look like a generic loading hang.
        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Shaping landforms and roads",
            4,
            9,
            "Rasterizing " + paths.Count + " accepted route spans",
            0.728f);
        // note: Accumulate overlapping corridors against one source heightmap; a later shoulder must never erase an earlier road centre.
        var roadTargets = new float[resolution, resolution];
        var roadStrengths = new float[resolution, resolution];
        var roadBlends = new float[resolution, resolution];
        // note: Unity triangulates the height grid; grade every vertex supporting the walking corridor, including the outer cell diagonal.
        float rasterPadding = new Vector2(terrainSize.x, terrainSize.z).magnitude / (resolution - 1);
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
                rasterPadding + 3f;
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
            float[] pathHeightProfile = networkProfiles[pathIndex];
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
                            path.shoulderWidth + rasterPadding)
                    {
                        continue;
                    }

                    Vector3 worldPoint =
                        new Vector3(worldX, 0f, worldZ);

                    if (IsInsideLivedPathTerrainReservation(
                            protectedFootprints,
                            point))
                    {
                        // note: Keep the full reviewed parcel and apron fixed; the visual road ribbon still crosses the flat surface without rewriting its terrain datum.
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

                    if (macroWater.preparedV2 != null)
                    {
                        YQSpatialTerrainSampleV2 waterSample =
                            macroWater.preparedV2.SampleTerrain(
                                worldX,
                                worldZ);
                        if (waterSample.waterMask >= 0.3f)
                        {
                            targetWorldHeight = Mathf.Max(
                                targetWorldHeight,
                                terrainPosition.y +
                                    terrainSize.y *
                                        waterSample.waterSurfaceNormalized +
                                    1.5f);
                        }

                        // note: V2 roads crossing accepted hydrology grade to the compiled water surface rather than consulting legacy ellipse basins.
                    }
                    else for (int basinIndex = 0;
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
                        pathDistance <= path.halfWidth + rasterPadding
                            ? 1f
                            : (1f - SmoothThreshold(
                                path.halfWidth + rasterPadding,
                                path.halfWidth + path.shoulderWidth + rasterPadding,
                                pathDistance)) *
                              0.82f;
                    float targetNormalized =
                        Mathf.Clamp01(
                            (targetWorldHeight - terrainPosition.y) /
                            Mathf.Max(0.001f, terrainSize.y));
                    float strength = centerBlend / Mathf.Max(.0001f, 1f - centerBlend);
                    roadTargets[z, x] += targetNormalized * strength;
                    roadStrengths[z, x] += strength;
                    roadBlends[z, x] = Mathf.Max(roadBlends[z, x], centerBlend);
                    modifiedSamples++;
                    dirtyMinimumXByRow[z] = Mathf.Min(
                        dirtyMinimumXByRow[z],
                        x);
                    dirtyMaximumXByRow[z] = Mathf.Max(
                        dirtyMaximumXByRow[z],
                        x);

                    // note: Yield inside the per-path raster as well as between rows; curved road bounds can span hundreds of cells and otherwise stall before the row boundary is reached.
                    if (((x + z * resolution) & 63) == 63 &&
                        Time.realtimeSinceStartup - frameStartedAt >= routeWorkSliceSeconds)
                    {
                        // note: A completed raster time slice is real path-repair progress, not an acceptance signal.
                        YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                        yield return null;
                        frameStartedAt = Time.realtimeSinceStartup;
                    }
                }

                if (Time.realtimeSinceStartup - frameStartedAt >= routeWorkSliceSeconds)
                {
                    // note: Even long regional roads grade a few height rows per rendered frame so improved traversal cannot reintroduce a loading-screen hang.
                    // note: A completed road row slice is real path-repair progress, not an acceptance signal.
                    YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
            }
        }

        // note: Mark the transition out of route rasterization before the dense slope-projection grid begins; this keeps the measured startup boundary attributable.
        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Shaping landforms and roads",
            4,
            9,
            "Projecting connected route grades",
            0.728f);
        const int uploadRowsPerFrame = 8;
        for (int z = 0; z < resolution; z++)
        {
            // note: Publish one blended road surface while retaining the original terrain outside every declared corridor.
            for (int x = dirtyMinimumXByRow[z]; x <= dirtyMaximumXByRow[z]; x++)
                if (roadStrengths[z, x] > 0f)
                    heights[z, x] = Mathf.Lerp(heights[z, x], roadTargets[z, x] / roadStrengths[z, x], roadBlends[z, x]);
            if (z % 16 == 0)
            {
                // note: A completed blend row slice keeps the repair watchdog aligned with actual work.
                YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                yield return null;
            }
        }
        // note: Constrain the actual Unity height-grid edges after corridor blending; centreline constraints alone do not bound cross-slope at overlapping road ribbons.
        var fixedRoadGrid = new bool[resolution, resolution];
        // note: Grade smoothing must preserve water clearance established by the crossing solve.
        var roadMinimum = new float[resolution, resolution];
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                fixedRoadGrid[z, x] = IsInsideLivedPathTerrainReservation(protectedFootprints,
                    new Vector2(terrainPosition.x + x * terrainSize.x / (resolution - 1),
                        terrainPosition.z + z * terrainSize.z / (resolution - 1)));
                if (roadBlends[z, x] >= .999f && macroWater.preparedV2 != null)
                {
                    var sample = macroWater.preparedV2.SampleTerrain(
                        terrainPosition.x + x * terrainSize.x / (resolution - 1),
                        terrainPosition.z + z * terrainSize.z / (resolution - 1));
                    if (sample.waterMask >= .3f)
                        roadMinimum[z, x] = sample.waterSurfaceNormalized + 1.5f / terrainSize.y;
                }
            }
            if (z % 16 == 0)
            {
                // note: A completed fixed-grid scan slice keeps the repair watchdog aligned with actual work.
                YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                yield return null;
            }
        }
        float gridRiseX = .24f * terrainSize.x / (resolution - 1) / terrainSize.y;
        float gridRiseZ = .24f * terrainSize.z / (resolution - 1) / terrainSize.y;
        // note: Sixteen bounded relaxation passes retain the route/water/foundation constraints while keeping the 513-square startup projection inside a practical loading window.
        const int maximumRoadGradePasses = 16;
        float roadProjectionFrameStartedAt = Time.realtimeSinceStartup;
        for (int pass = 0; pass < maximumRoadGradePasses; pass++)
        {
            float residual = 0f;
            for (int z = 0; z < resolution; z++)
                for (int x = dirtyMinimumXByRow[z]; x <= dirtyMaximumXByRow[z]; x++)
                {
                    if (roadBlends[z, x] < .999f) continue;
                    // note: Alternate height bounds and slope constraints so smoothing cannot sink a dry crossing below its accepted water level.
                    float lift = roadMinimum[z, x] - heights[z, x];
                    if (lift > 0f) { heights[z, x] += lift; residual = Mathf.Max(residual, lift); }
                    if (x > 0) residual = Mathf.Max(residual, ProjectRoadGridEdge(heights, roadBlends, fixedRoadGrid, x, z, x - 1, z, gridRiseX));
                    if (x + 1 < resolution) residual = Mathf.Max(residual, ProjectRoadGridEdge(heights, roadBlends, fixedRoadGrid, x, z, x + 1, z, gridRiseX));
                    if (z > 0) residual = Mathf.Max(residual, ProjectRoadGridEdge(heights, roadBlends, fixedRoadGrid, x, z, x, z - 1, gridRiseZ));
                    if (z + 1 < resolution) residual = Mathf.Max(residual, ProjectRoadGridEdge(heights, roadBlends, fixedRoadGrid, x, z, x, z + 1, gridRiseZ));

                    // note: Yield within the dense grid projection so overlapping road corridors cannot hold the main thread for an entire 513-row pass.
                    if (((x + z * resolution) & 127) == 127 &&
                        Time.realtimeSinceStartup - roadProjectionFrameStartedAt >= routeWorkSliceSeconds)
                    {
                        // note: A completed projection batch is real repair progress even when its residual remains for a later pass.
                        YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                        yield return null;
                        roadProjectionFrameStartedAt = Time.realtimeSinceStartup;
                    }
                }
            if (residual * terrainSize.y < .002f) break;
            if (pass % 2 == 0)
            {
                // note: A completed projection pass is real repair progress and does not certify traversability.
                YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                yield return null;
            }
        }
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
            // note: A published dirty road strip advances the watchdog only after the Unity heightmap write is queued.
            YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
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

        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Curating the wilderness",
            5,
            8,
            "Carving and filling deterministic water basins",
            0.765f);
        // note: Water is materialized from the same deterministic basin descriptors that sculpted the heightfield, after construction pads can safely rise through it.
        yield return BuildMacroWaterBodiesRoutine(
            parent,
            terrain,
            plan,
            registry,
            count => waterBodies = count);
        // note: Completing a wilderness layer is meaningful startup progress even when the complete wilderness pass spans many loading frames.
        YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();

        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Curating the wilderness",
            5,
            8,
            "Building biome-aware tree canopies and native ground cover",
            0.78f);
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
        // note: Native vegetation publication is a separate bounded phase; refresh the stall watchdog before authored regional dressing begins.
        YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();

        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Curating the wilderness",
            5,
            8,
            "Dressing regions with rocks, caves, foliage, treasure, and encounters",
            0.82f);
        // note: Wilderness families are committed across rendered frames instead of cloning every region's scenery in one loading-frame burst.
        yield return BuildRegionalWildernessRoutine(
            parent,
            terrain,
            plan,
            registry,
            result => stats = result);
        // note: Regional scenery completion proves the generation transaction is advancing before the origin foreground receives its final dressing.
        YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();

        int curatedPoiHuts = 0;
        if (!YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
        {
            // note: Legacy wayhouses remain available outside V2; accepted V2 settlements are streamed from the spatial site owner instead.
            yield return BuildCuratedPoiHutsRoutine(
                parent,
                terrain,
                plan,
                registry,
                count => curatedPoiHuts = count);
        }

        stats ??=
            new WildernessBuildStats();

        stats.curatedPoiHuts = curatedPoiHuts;

        stats.waterBodies =
            waterBodies;

        int originVegetation = 0;
        int originTrees = 0;
        int originRocks = 0;

        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Curating the wilderness",
            5,
            8,
            "Composing the starting approach and curated Goddess-stage surroundings",
            0.855f);
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
        // note: The final foreground composition is the last wilderness phase boundary before control returns to the world builder.
        YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();

        stats.visibleTrees += originTrees;
        stats.vegetation += originVegetation;
        stats.rocks += originRocks;

        // note: Wilderness grounding uses terrain samples and renderer bounds; colliders can join the next normal physics step instead of forcing a full-scene loading synchronization.
        yield return null;

        Debug.Log(
            "[YQGeneratedWorldEnvironment] WILDERNESS READY\n" +
            "Vegetation spawned: " +
            stats.vegetation +
            "\nRoadside dressing spawned: " +
            stats.roadsideDressing +
            "\nShoreline dressing spawned: " +
            stats.shorelineDressing +
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
            stats.treasure +
            "\nCurated Nordic huts spawned: " +
            stats.curatedPoiHuts);
    }

    private static IEnumerator BuildCuratedPoiHutsRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        Action<int> completed)
    {
        if (parent == null || terrain == null || plan == null || registry == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out YQPreparedSpatialMaterializationV2 prepared,
                out _))
        {
            completed?.Invoke(0);
            yield break;
        }

        GameObject rootObject = new GameObject("Generated_CuratedPoiHuts");
        rootObject.transform.SetParent(parent, false);
        int spawned = 0;
        const int maximumHuts = 3;

        for (int siteIndex = 0;
             siteIndex < prepared.SiteCount && spawned < maximumHuts;
             siteIndex++)
        {
            YQSpatialMaterializationSiteV2 site = prepared.GetSite(siteIndex);
            if (site.kind != YQSiteKindV2.PointOfInterest || site.concealed)
                continue;

            GeneratedPointOfInterestRecord semantic = null;
            for (int poiIndex = 0; poiIndex < plan.pointsOfInterest.Count; poiIndex++)
            {
                GeneratedPointOfInterestRecord candidate = plan.pointsOfInterest[poiIndex];
                if (candidate != null && string.Equals(candidate.poiId, site.sourceSemanticId, StringComparison.OrdinalIgnoreCase))
                {
                    semantic = candidate;
                    break;
                }
            }
            string meaning = semantic != null
                ? (semantic.kind ?? string.Empty) + " " + string.Join(" ", semantic.tags ?? new List<string>())
                : site.sourceSemanticId;
            if (meaning.IndexOf("nord", StringComparison.OrdinalIgnoreCase) < 0 &&
                meaning.IndexOf("hut", StringComparison.OrdinalIgnoreCase) < 0 &&
                meaning.IndexOf("wayhouse", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            GeneratedRegionRecord region = null;
            for (int regionIndex = 0; regionIndex < plan.regions.Count; regionIndex++)
            {
                if (plan.regions[regionIndex] != null && string.Equals(plan.regions[regionIndex].regionId, site.parentRegionId, StringComparison.OrdinalIgnoreCase))
                {
                    region = plan.regions[regionIndex];
                    break;
                }
            }
            GeneratedRegionAssetPaletteRecord palette = FindPalette(plan, region);
            GeneratedAssetReferenceRecord reference = PickCuratedPoiHutReference(palette, plan.worldSeed + "|poi_hut|" + site.sourceSemanticId);
            if (reference == null)
                continue;

            GameObject prefab = registry.ResolvePrefab(reference.assetPath);
            if (prefab == null)
                continue;

            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(prefab, rootObject.transform);
            operation.priority = -1;
            yield return operation;
            GameObject instance = operation.Result != null && operation.Result.Length > 0 ? operation.Result[0] : null;
            if (instance == null)
                continue;

            string seed = plan.worldSeed + "|poi_hut|" + site.sourceSemanticId;
            instance.name = "CuratedPoiHut__" + SafeName(semantic != null ? semantic.displayName : site.sourceSemanticId);
            float heading = site.headingDegrees;
            instance.transform.SetPositionAndRotation(
                new Vector3(site.x, YQGeneratedWorldTerrain.SampleWorldHeight(terrain, new Vector3(site.x, 0f, site.z)), site.z),
                Quaternion.Euler(0f, heading, 0f));
            float scale = Mathf.Lerp(Mathf.Max(0.01f, reference.scaleMin), Mathf.Max(reference.scaleMin, reference.scaleMax), Deterministic01(seed + "|scale"));
            instance.transform.localScale *= scale;
            if (TryGetWildernessBounds(instance, out Bounds bounds))
            {
                float fit = Mathf.Min(48f / Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z)), 34f / Mathf.Max(0.01f, bounds.size.y));
                if (fit < 1f)
                    instance.transform.localScale *= Mathf.Clamp(fit, 0.55f, 1f);
            }
            registry.ApplyMaterialOverrides(reference.assetPath, instance);
            PrepareWildernessInstance(instance);
            yield return YQRuntimeUrpMaterialRepair.RepairMaterialHierarchyRoutine(instance, null);
            if (!YQGeneratedWorldTerrain.TryPlaceGroundedObject(instance, terrain, YQGeneratedWorldPlacementCategory.Structure, 0.04f, out _))
            {
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                continue;
            }

            // note: Bind the accepted named-site identity only after grounding succeeds, so overlay deletion targets the published POI instance and survives streamed reconstruction.
            YQStreamedFeatureOverlayTarget overlayTarget = instance.GetComponent<YQStreamedFeatureOverlayTarget>() ?? instance.AddComponent<YQStreamedFeatureOverlayTarget>();
            overlayTarget.featureId = site.siteId ?? site.sourceSemanticId ?? string.Empty;
            overlayTarget.objectId = instance.name;
            // note: Replay the accepted overlay when this POI is materialized after streamer configuration so a rebuilt deleted feature stays hidden.
            YQPlayerFollowingSemanticChunkStreamer overlayStreamer = UnityEngine.Object.FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
            if (overlayStreamer != null)
                overlayStreamer.RegisterFeatureOverlayTarget(overlayTarget.featureId, instance, overlayTarget.objectId);
            spawned++;
            yield return null;
        }

        Debug.Log("[YQGeneratedWorldEnvironment] Curated POI huts materialized: " + spawned);
        completed?.Invoke(spawned);
    }

    private static GeneratedAssetReferenceRecord PickCuratedPoiHutReference(
        GeneratedRegionAssetPaletteRecord palette,
        string seed)
    {
        if (palette == null || palette.settlementBuilding == null)
            return null;
        List<GeneratedAssetReferenceRecord> candidates = new List<GeneratedAssetReferenceRecord>();
        for (int index = 0; index < palette.settlementBuilding.Count; index++)
        {
            GeneratedAssetReferenceRecord reference = palette.settlementBuilding[index];
            if (reference == null || IsLargeTerrainFeatureReference(reference))
                continue;
            string semantic = BuildReferenceSemanticText(reference);
            if (ContainsAnySemantic(semantic, "nord", "viking", "hut", "cabin", "wayhouse", "timber", "log", "house"))
                candidates.Add(reference);
        }
        if (candidates.Count == 0)
            return null;
        int selected = Mathf.Clamp(Mathf.FloorToInt(Deterministic01(seed + "|reference") * candidates.Count), 0, candidates.Count - 1);
        return candidates[selected];
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

        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
        {
            // note: Authoritative V2 terrain already contains accepted ridges, valleys, basins, caves, routes, and site reserves; legacy radial landform stamps must not overwrite that causal design.
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

            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Shaping landforms and roads",
                3,
                8,
                "Stamping regional landform " + (regionIndex + 1) + " of " +
                plan.regions.Count + ": " + region.displayName,
                Mathf.Lerp(
                    0.68f,
                    0.695f,
                    plan.regions.Count > 0
                        ? regionIndex / (float)plan.regions.Count
                        : 1f));

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

            // note: Every lived region receives a distinct packed-earth layer; when semantic data lacks an explicit road material, the best regional detail texture is remapped instead of making the road visually identical to its surroundings.
            GeneratedAssetReferenceRecord pathSourceReference =
                pathReference ??
                (terrainReferences.Count > 1
                    ? terrainReferences[1]
                    : terrainReferences[0]);

            int pathLayerIndex =
                ResolveOrCreatePathTerrainLayerIndex(
                    pathSourceReference,
                    registry,
                    palette,
                    layers,
                    layerByMaterialPath);

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

    private static IEnumerator BuildLivedPathVisualsRoutine(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        Action<LivedPathVisualReport> completed = null)
    {
        LivedPathVisualReport report = new LivedPathVisualReport();
        if (terrain == null || terrain.terrainData == null ||
            plan == null || registry == null)
        {
            completed?.Invoke(report);
            yield break;
        }

        Transform previous = terrain.transform.Find("YQ_Generated_LivedPaths");
        ReleaseGeneratedLivedPathMeshes();
        if (previous != null)
        {
            previous.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(previous.gameObject);
        }

        List<LivedPathSegment> paths = BuildLivedPathNetwork(plan, terrain);
        report.requiredSegments = paths.Count;
        if (paths.Count == 0)
        {
            completed?.Invoke(report);
            yield break;
        }

        // note: Validate the published terrain paint instead of overlaying an opaque, coarse ribbon that conceals its blended shoulders.
        var data = terrain.terrainData;
        TerrainLayer[] layers = data.terrainLayers;
        var pathLayers = new bool[layers.Length];
        for (int i = 0; i < layers.Length; i++)
            pathLayers[i] = layers[i] != null && layers[i].name.EndsWith("_LivedPath", StringComparison.Ordinal);
        float[,,] paint = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        Vector3 origin = terrain.transform.position;
        // note: Reuse the compiled hydrology mask so a wet route sample is owned by the bridge/crossing pass instead of being rejected as missing dry-land paint.
        MacroWaterSet macroWater = BuildMacroWaterSet(terrain, plan);
        foreach (LivedPathSegment path in paths)
        {
            bool accepted = IsFiniteLivedPathVector(path.start) && IsFiniteLivedPathVector(path.end) &&
                IsFiniteLivedPathValue(path.halfWidth) && path.halfWidth >= .35f;
            bool geometryValid = accepted;
            int dryPaintMisses = 0;
            bool hasWetCrossing = false;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(path.start, path.end) / 2f));
            // note: Evaluate the complete leg even after a dry paint miss, because a later wet sample can prove this is an intentional bridge crossing.
            for (int step = 0; step <= steps; step++)
            {
                Vector2 center = ResolveLivedPathCenter(path, step / (float)steps);
                bool edgePairValid = TryResolveLivedPathEdgePair(terrain, center, path.end - path.start, path.halfWidth,
                    out _, out _, out _, out _);
                geometryValid &= edgePairValid;
                // note: Match the terrain renderer's bilinear filtering instead of rejecting a road from a single neighbouring wet texel.
                float fx = Mathf.Clamp((center.x - origin.x) / data.size.x * (data.alphamapWidth - 1), 0, data.alphamapWidth - 1);
                float fz = Mathf.Clamp((center.y - origin.z) / data.size.z * (data.alphamapHeight - 1), 0, data.alphamapHeight - 1);
                int x = Mathf.FloorToInt(fx), z = Mathf.FloorToInt(fz);
                int x1 = Mathf.Min(x + 1, data.alphamapWidth - 1), z1 = Mathf.Min(z + 1, data.alphamapHeight - 1);
                float coverage = 0f;
                for (int i = 0; i < layers.Length; i++)
                    if (pathLayers[i]) coverage += Mathf.Lerp(Mathf.Lerp(paint[z, x, i], paint[z, x1, i], fx - x),
                        Mathf.Lerp(paint[z1, x, i], paint[z1, x1, i], fx - x), fz - z);
                float normalizedX = Mathf.Clamp01((center.x - origin.x) / data.size.x);
                float normalizedZ = Mathf.Clamp01((center.y - origin.z) / data.size.z);
                float terrainY = origin.y + data.GetInterpolatedHeight(normalizedX, normalizedZ);
                bool submerged = IsSubmergedByMacroWater(
                    terrain,
                    plan,
                    new Vector3(center.x, terrainY, center.y),
                    0f,
                    macroWater);
                hasWetCrossing |= submerged;
                // note: Dry samples still require visible lived-path paint; submerged samples are valid only when the physical edge pair resolved and are later represented by an authored bridge.
                if (!submerged)
                    dryPaintMisses += coverage < .15f ? 1 : 0;
            }
            // note: A route with a valid terrain edge pair and a real wet crossing is accepted for bridge restoration even if the water-owned center has no dry path texture.
            // note: One or two isolated filtered texels can fall inside a protected construction edge; only sustained missing coverage rejects a dry route.
            bool sparsePaintMiss = dryPaintMisses > 0 && dryPaintMisses <= Mathf.Max(2, steps / 20);
            accepted = geometryValid && (dryPaintMisses == 0 || sparsePaintMiss || hasWetCrossing);
            if (accepted) report.builtSegments++;
            else
            {
                report.rejectedSegments++;
                Debug.LogError("[WORLDGEN ERROR] Road terrain contact/paint incomplete: " + path.start + " -> " + path.end);
            }
            yield return null;
        }
        // note: The terrain renderer publishes every validated road using native layer blending and the same physical ground.
        // note: Crossing analysis must run even when one paint sample was rejected; otherwise a valid bridge candidate is discarded before it can restore route continuity.
        yield return BuildRiverCrossingsRoutine(terrain,plan,registry,paths);
        report.roadObjects = report.builtSegments > 0 && terrain.drawHeightmap ? 1 : 0;
        completed?.Invoke(report);
    }

    private static IEnumerator BuildRiverCrossingsRoutine(Terrain terrain, GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry, List<LivedPathSegment> paths)
    {
        var prepared = BuildMacroWaterSet(terrain,plan).preparedV2;
        if (prepared == null) yield break;
        var data=terrain.terrainData;
        int resolution=data.heightmapResolution;
        float[,] heights=data.GetHeights(0,0,resolution,resolution);
        var spans=new List<(List<Vector2> points,float halfWidth,float shoulderWidth)>();
        string activeRoute = null;
        var recentDry = new List<Vector2>(3);
        var activePoints = new List<Vector2>(32);
        int wetSamples = 0;
        int dryAfterSamples = 0;
        float activeHalfWidth = 2f;
        float activeShoulderWidth = 4f;

        // note: Sample connected route legs as one polyline so a river crossing cannot be split at a control point and leave an orphan bridge candidate.
        for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
        {
            LivedPathSegment path = paths[pathIndex];
            if (path == null) continue;
            string routeGroup = string.IsNullOrWhiteSpace(path.routeId)
                ? "__path_" + pathIndex
                : path.routeId;
            if (!string.Equals(routeGroup, activeRoute, StringComparison.Ordinal))
            {
                // note: An incomplete wet run at a route boundary is discarded instead of becoming a floating one-sided bridge.
                activeRoute = routeGroup;
                recentDry.Clear();
                activePoints.Clear();
                wetSamples = 0;
                dryAfterSamples = 0;
            }

            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(path.start, path.end)));
            activeHalfWidth = Mathf.Max(1f, path.halfWidth);
            activeShoulderWidth = Mathf.Max(activeHalfWidth, path.shoulderWidth);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = ResolveLivedPathCenter(path, i / (float)steps);
                var sample = prepared.SampleTerrain(p.x, p.y);
                // note: Include the accepted shoreline transition, not only deep wet texels; a path endpoint can land on a bank while its next leg crosses the channel.
                bool channel = sample.waterFeatureIndex >= 0 && sample.waterMask > .02f;
                if (channel)
                {
                    if (activePoints.Count == 0)
                    {
                        if (recentDry.Count < 2)
                        {
                            // note: Never begin a bridge without two dry approach samples on the same route.
                            wetSamples = 0;
                            continue;
                        }
                        activePoints.AddRange(recentDry);
                        wetSamples = 0;
                    }
                    activePoints.Add(p);
                    wetSamples++;
                    dryAfterSamples = 0;
                }
                else
                {
                    if (activePoints.Count > 0)
                    {
                        activePoints.Add(p);
                        dryAfterSamples++;
                        // note: Carry the deck farther onto the receiving bank so shallow mask gaps cannot leave a short unsupported route tail at the water exit.
                        if (wetSamples >= 3 && dryAfterSamples >= 10)
                        {
                            spans.Add((new List<Vector2>(activePoints), activeHalfWidth, activeShoulderWidth));
                            // note: Record the accepted route group and bounded span size so missing crossing metadata is diagnosable without another scene scan.
                            Debug.Log("[WORLDGEN BRIDGE TRACE] route=" + activeRoute + " points=" + activePoints.Count + " wetSamples=" + wetSamples + " from=" + activePoints[0] + " to=" + activePoints[activePoints.Count - 1]);
                            activePoints.Clear();
                            wetSamples = 0;
                            dryAfterSamples = 0;
                        }
                    }
                    recentDry.Add(p);
                    if (recentDry.Count > 3) recentDry.RemoveAt(0);
                }
            }
            yield return null;
        }
        int carved=0;
        foreach(var span in spans)
        {
            var road=new List<Vector3>();
            float roadSliceStartedAt = Time.realtimeSinceStartup;
            for(int i=0;i<span.points.Count;i++)
            {
                Vector2 p=span.points[i];
                float terrainY = terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
                var waterSample = prepared.SampleTerrain(p.x, p.y);
                // note: Crossing decks sit above the carved bed at the accepted water datum, while bank contacts keep their finished terrain height.
                float deckY = terrainY + .18f;
                if (waterSample.waterFeatureIndex >= 0 && waterSample.waterMask > .18f)
                    deckY = terrain.transform.position.y + waterSample.waterSurfaceNormalized * data.size.y + .32f;
                road.Add(new Vector3(p.x, deckY, p.y));
                if ((i & 15) == 15 && Time.realtimeSinceStartup - roadSliceStartedAt >= YQGeneratedRiverBridge.CooperativeSliceSeconds)
                {
                    yield return null;
                    roadSliceStartedAt = Time.realtimeSinceStartup;
                }
            }
            if(road.Count<2) continue;
            // note: Build the accepted visual and collision span cooperatively before restoring the concave channel beneath its hidden terrain owner.
            yield return YQGeneratedRiverBridge.BuildRoutine(
                terrain.transform.parent,
                road,
                span.halfWidth * 2f + 2f,
                registry);
            // note: Only after the collidable bridge exists, restore the accepted concave channel under its former earth causeway.
            float carveSliceStartedAt = Time.realtimeSinceStartup;
            for(int i=0;i<road.Count;i++)
            {
                Vector3 p=road[i];
                int cx=Mathf.RoundToInt((p.x-terrain.transform.position.x)/data.size.x*(resolution-1));
                int cz=Mathf.RoundToInt((p.z-terrain.transform.position.z)/data.size.z*(resolution-1));
                int radius=Mathf.CeilToInt((span.halfWidth+span.shoulderWidth+4f)/data.size.x*(resolution-1));
                for(int z=Mathf.Max(0,cz-radius);z<=Mathf.Min(resolution-1,cz+radius);z++)
                {
                    for(int x=Mathf.Max(0,cx-radius);x<=Mathf.Min(resolution-1,cx+radius);x++)
                    {
                        float wx=terrain.transform.position.x+x*data.size.x/(resolution-1);
                        float wz=terrain.transform.position.z+z*data.size.z/(resolution-1);
                        var sample=prepared.SampleTerrain(wx,wz);
                        // note: Carve the whole accepted channel band beneath the bridge, including the bank transition, so the raised road cannot leave an earth plug under the water ribbon.
                        // note: Restore only the submerged core; shallow shoreline transition remains solid so the bridge has continuous bank support and a natural concave edge.
                        if(sample.waterMask<.9f || sample.elevationNormalized>=sample.waterSurfaceNormalized) continue;
                        heights[z,x]=Mathf.Min(heights[z,x],sample.elevationNormalized);
                        carved++;
                    }
                    // note: Spread deterministic channel restoration by elapsed slice time before publishing the completed heightmap.
                    if (Time.realtimeSinceStartup - carveSliceStartedAt >= YQGeneratedRiverBridge.CooperativeSliceSeconds)
                    {
                        yield return null;
                        carveSliceStartedAt = Time.realtimeSinceStartup;
                    }
                }
            }
            yield return null;
        }
        if(carved>0) { data.SetHeights(0,0,heights); Physics.SyncTransforms(); }
        Debug.Log("[WORLDGEN BRIDGES] Authored stone spans="+spans.Count+", channel samples restored="+carved);
    }

    internal static bool TryResolveLivedPathEdgePair(
        Terrain terrain,
        Vector2 center,
        Vector2 tangent,
        float halfWidth,
        out Vector3 left,
        out Vector3 right,
        out Vector3 leftNormal,
        out Vector3 rightNormal)
    {
        left = default;
        right = default;
        leftNormal = Vector3.up;
        rightNormal = Vector3.up;
        if (terrain == null || terrain.terrainData == null ||
            !IsFiniteLivedPathVector(center) ||
            !IsFiniteLivedPathVector(tangent) ||
            !IsFiniteLivedPathValue(halfWidth) ||
            tangent.sqrMagnitude < 0.0001f || halfWidth < 0.01f)
        {
            return false;
        }

        tangent.Normalize();
        Vector2 side = new Vector2(-tangent.y, tangent.x);
        Vector2 leftPoint = center - side * halfWidth;
        Vector2 rightPoint = center + side * halfWidth;

        // note: Each road edge resolves its own terrain height and normal, preserving readable width across cross-slopes without floating one side or burying the other.
        return TrySampleLivedPathSurface(terrain, leftPoint, out left, out leftNormal) &&
               TrySampleLivedPathSurface(terrain, rightPoint, out right, out rightNormal);
    }

    private static bool TrySampleLivedPathSurface(
        Terrain terrain,
        Vector2 worldPoint,
        out Vector3 worldPosition,
        out Vector3 worldNormal)
    {
        worldPosition = default;
        worldNormal = Vector3.up;
        if (terrain == null || terrain.terrainData == null ||
            !IsFiniteLivedPathVector(worldPoint))
        {
            return false;
        }

        TerrainData data = terrain.terrainData;
        Vector3 terrainOrigin = terrain.transform.position;
        if (data.size.x <= 0.001f || data.size.z <= 0.001f)
            return false;

        float normalizedX = (worldPoint.x - terrainOrigin.x) / data.size.x;
        float normalizedZ = (worldPoint.y - terrainOrigin.z) / data.size.z;
        if (normalizedX < 0f || normalizedX > 1f ||
            normalizedZ < 0f || normalizedZ > 1f)
        {
            return false;
        }

        float surfaceHeight =
            terrainOrigin.y + data.GetInterpolatedHeight(normalizedX, normalizedZ);
        Vector3 localNormal = data.GetInterpolatedNormal(normalizedX, normalizedZ);
        worldPosition = new Vector3(
            worldPoint.x,
            surfaceHeight + RoadSurfaceOffset,
            worldPoint.y);
        worldNormal = terrain.transform.TransformDirection(localNormal).normalized;
        return IsFiniteLivedPathValue(worldPosition.y) &&
               IsFiniteLivedPathValue(worldNormal.x) &&
               IsFiniteLivedPathValue(worldNormal.y) &&
               IsFiniteLivedPathValue(worldNormal.z);
    }

    private static bool IsFiniteLivedPathVector(Vector2 value)
    {
        return IsFiniteLivedPathValue(value.x) &&
               IsFiniteLivedPathValue(value.y);
    }

    private static bool IsFiniteLivedPathValue(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
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

    private static int ResolveOrCreatePathTerrainLayerIndex(
        GeneratedAssetReferenceRecord reference,
        YQRuntimeWorldAssetRegistry registry,
        GeneratedRegionAssetPaletteRecord palette,
        List<TerrainLayer> layers,
        Dictionary<string, int> layerByMaterialPath)
    {
        if (reference == null || registry == null || layers == null ||
            layerByMaterialPath == null)
        {
            return -1;
        }

        string materialPath = YQRuntimeWorldAssetRegistry.NormalizePath(
            reference.assetPath);
        string variantKey = materialPath + "|yq_lived_path";
        if (layerByMaterialPath.TryGetValue(variantKey,
                out int existingIndex))
        {
            return existingIndex;
        }

        Material material = registry.ResolveMaterial(materialPath);
        TerrainLayer layer = CreateTerrainLayer(
            material,
            palette,
            materialPath);
        if (layer == null)
            return -1;

        // note: Reuse the approved regional dirt texture with a packed-earth remap so a path remains readable even when that same source texture also dresses surrounding ground.
        layer.name += "_LivedPath";
        layer.diffuseRemapMin = new Vector4(0.015f, 0.012f, 0.009f, 0f);
        layer.diffuseRemapMax = new Vector4(0.58f, 0.48f, 0.36f, 1f);
        layer.metallic = 0f;
        layer.smoothness = 0.06f;
        layer.tileSize = new Vector2(
            Mathf.Max(3f, layer.tileSize.x * 0.72f),
            Mathf.Max(3f, layer.tileSize.y * 0.72f));

        int createdIndex = layers.Count;
        layers.Add(layer);
        layerByMaterialPath[variantKey] = createdIndex;
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

                    // note: Scale inverse-distance weights before squaring so distant valid regions do not trip the empty-palette cutoff and erase every road/detail mask.
                    float influence =
                        10000f /
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
                            SmoothThreshold(
                                0.16f,
                                0.72f,
                                slope) +
                            SmoothThreshold(
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
                        SmoothThreshold(-118f, -100f, originLocalZ) *
                        (1f - SmoothThreshold(18f, 30f, originLocalZ));

                    float trailCenterX =
                        originAnchor.x +
                        Mathf.Sin((originLocalZ + 118f) * 0.041f) * 3.2f;

                    float trailMask =
                        (1f - SmoothThreshold(
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
                                data.GetInterpolatedHeight(normalizedX, normalizedZ),
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
                            totalWeight * livedPathMask * LivedPathTerrainWeight;
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
        // note: Keep existing callers on the same accepted-authority check while allowing one streamed build to share its verified projection.
        return BuildLivedPathNetwork(plan, terrain, null, out _);
    }

    private static List<LivedPathSegment> BuildLivedPathNetwork(
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        out YQPreparedSpatialMaterializationV2 preparedV2)
    {
        return BuildLivedPathNetwork(plan, terrain, null, out preparedV2);
    }

    private static List<LivedPathSegment> BuildLivedPathNetwork(
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        YQPreparedSpatialMaterializationV2 preparedV2)
    {
        // note: Reuse the water reader's already verified immutable V2 projection during optional shoreline placement.
        return BuildLivedPathNetwork(plan, terrain, preparedV2, out _);
    }

    private static List<LivedPathSegment> BuildLivedPathNetwork(
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        YQPreparedSpatialMaterializationV2 preparedOverride,
        out YQPreparedSpatialMaterializationV2 preparedV2)
    {
        float authorityResolveMs = 0f;
        float settlementStreetMs = 0f;
        float routeProjectionMs = 0f;
        float terrainClipMs = 0f;
        List<LivedPathSegment> paths =
            new List<LivedPathSegment>();
        preparedV2 = preparedOverride;

        if (plan == null || terrain == null)
            return paths;

        bool usesV2 = YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan);
        if (usesV2 && preparedV2 == null)
        {
            float phaseStartedAt = Time.realtimeSinceStartup;
            bool prepared = TryGetCachedPreparedPathAuthority(plan, out preparedV2);
            string failure = string.Empty;
            if (!prepared)
                prepared = YQSpatialMaterializationResolverV2.TryGetPrepared(
                    plan,
                    out preparedV2,
                    out failure);
            authorityResolveMs = (Time.realtimeSinceStartup - phaseStartedAt) * 1000f;
            if (!prepared)
            {
                // note: A rejected V2 projection never falls through to the compatibility route graph.
                LastSemanticPathProjectionBreakdown = "authority rejected " + failure;
                Debug.LogError(
                    "[YQGeneratedWorldEnvironment] V2 path projection rejected: " +
                    failure);
                return paths;
            }
        }

        // note: These persisted streets are the same rigid-block plan used for grading and instantiation, and participate in foliage exclusion too.
        float streetsStartedAt = Time.realtimeSinceStartup;
        AppendProceduralSettlementStreets(paths, plan, terrain, preparedV2);
        settlementStreetMs = (Time.realtimeSinceStartup - streetsStartedAt) * 1000f;

        if (usesV2)
        {
            int projectedRouteStart = paths.Count;
            float routeStartedAt = Time.realtimeSinceStartup;
            bool routeCacheHit = AppendCachedV2PathProjection(
                paths,
                plan,
                terrain,
                preparedV2,
                out bool worldProjectionBuilt);
            routeProjectionMs = (Time.realtimeSinceStartup - routeStartedAt) * 1000f;
            float clipStartedAt = Time.realtimeSinceStartup;
            ClipLivedPathNetworkToTerrain(paths, terrain, 0, projectedRouteStart);
            terrainClipMs = (Time.realtimeSinceStartup - clipStartedAt) * 1000f;
            // note: Keep cache and substage results with the streamed-cell publication receipt; this is diagnostic only and does not alter route geometry.
            LastSemanticPathProjectionBreakdown =
                "authority=" + authorityResolveMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                "ms;streets=" + settlementStreetMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                "ms;routes=" + routeProjectionMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                "ms;clip=" + terrainClipMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                "ms;routeTileCache=" + (routeCacheHit ? "hit" : "miss") +
                ";worldProjection=" + (worldProjectionBuilt ? "build" : "reuse") +
                ";worldSegments=" + _cachedPathProjectionSegments.Count +
                ";segments=" + paths.Count;
            return paths;
        }

        GeneratedSpatialWorldPlanRecord spatialPlan =
            YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan);
        if (spatialPlan != null && spatialPlan.routes.Count > 0)
        {
            // note: Painted roads, terrain grading, foliage exclusion, and cave approaches all consume the persisted cost-aware travel graph.
            BuildSpatialLivedPathNetwork(paths, plan, terrain, spatialPlan);
            return paths;
        }

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

        if (settlementAnchors.Count >= 2)
        {
            List<Vector2> mainRouteAnchors =
                new List<Vector2>(
                    settlementAnchors);

            // note: The main route is ordered around the origin so settlements read as one inhabited region instead of a collection of unrelated radial driveways.
            mainRouteAnchors.Sort(
                delegate(Vector2 a, Vector2 b)
                {
                    float angleA =
                        Mathf.Atan2(
                            a.y - origin.y,
                            a.x - origin.x);
                    float angleB =
                        Mathf.Atan2(
                            b.y - origin.y,
                            b.x - origin.x);
                    return angleA.CompareTo(angleB);
                });

            for (int routeIndex = 0;
                 routeIndex < mainRouteAnchors.Count - 1;
                 routeIndex++)
            {
                AddLivedPathConnection(
                    paths,
                    mainRouteAnchors[routeIndex],
                    mainRouteAnchors[routeIndex + 1],
                    3.6f,
                    6.8f,
                    plan.worldSeed +
                    "|regional_main_route|" +
                    routeIndex);
            }

            if (mainRouteAnchors.Count >= 3)
            {
                AddLivedPathConnection(
                    paths,
                    mainRouteAnchors[mainRouteAnchors.Count - 1],
                    mainRouteAnchors[0],
                    3.4f,
                    6.2f,
                    plan.worldSeed +
                    "|regional_main_route_return");
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

    private static void BuildSpatialLivedPathNetwork(
        List<LivedPathSegment> paths,
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        GeneratedSpatialWorldPlanRecord spatialPlan)
    {
        for (int routeIndex = 0; routeIndex < spatialPlan.routes.Count; routeIndex++)
        {
            GeneratedSpatialRouteRecord route = spatialPlan.routes[routeIndex];
            if (route == null || route.waypoints == null || route.waypoints.Count < 2)
                continue;

            bool major = string.Equals(route.routeKind, "major_road", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(route.routeKind, "transit_corridor", StringComparison.OrdinalIgnoreCase);
            for (int pointIndex = 1; pointIndex < route.waypoints.Count; pointIndex++)
            {
                GeneratedSpatialPointRecord from = route.waypoints[pointIndex - 1];
                GeneratedSpatialPointRecord to = route.waypoints[pointIndex];
                Vector2 start = new Vector2(from.x, from.z);
                Vector2 end = new Vector2(to.x, to.z);

                if (pointIndex == 1)
                    start = ResolveLiveRouteEndpoint(plan, terrain, route.fromLocationId, start);
                if (pointIndex == route.waypoints.Count - 1)
                    end = ResolveLiveRouteEndpoint(plan, terrain, route.toLocationId, end);

                paths.Add(new LivedPathSegment
                {
                    // note: Keep persisted route identity so a water run spanning two waypoints remains one bridge candidate.
                    routeId = route.routeId,
                    start = start,
                    end = end,
                    halfWidth = major ? 3.8f : 2.25f,
                    shoulderWidth = major ? 6.8f : 4.2f,
                    curveAmplitude = major ? 1.2f : 2.1f,
                    curvePhase = Deterministic01(route.routeId + "|" + pointIndex) * Mathf.PI * 2f,
                    terraced = !major
                });
            }
        }

        for (int settlementIndex = 0; settlementIndex < plan.settlements.Count; settlementIndex++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[settlementIndex];
            if (settlement?.proceduralLayout != null) continue;
            if (settlement == null ||
                !YQGeneratedWorldSpatialPlanner.TryGetLocation(
                    plan,
                    settlement.settlementId,
                    out GeneratedSpatialLocationRecord location))
            {
                continue;
            }

            Vector3 anchor = YQGeneratedWorldLayout.GetSettlementAnchor(plan, settlement, terrain);
            Vector2 center = new Vector2(anchor.x, anchor.z);
            float headingRadians = location.entranceHeadingDegrees * Mathf.Deg2Rad;
            Vector2 entranceDirection = new Vector2(
                Mathf.Sin(headingRadians),
                Mathf.Cos(headingRadians));
            float radius = Mathf.Clamp(location.footprintRadius, 24f, 72f);

            // note: Every settlement receives a world-painted entrance spine aligned with its persisted cell heading, so the travel road continues through the gate instead of ending beneath arbitrary buildings.
            paths.Add(new LivedPathSegment
            {
                start = center + entranceDirection * (radius + 14f),
                end = center - entranceDirection * (radius * 0.68f),
                halfWidth = 3.4f,
                shoulderWidth = 5.8f,
                curveAmplitude = 0.65f,
                curvePhase = Deterministic01(settlement.settlementId + "|entrance_spine") * Mathf.PI * 2f,
                terraced = false
            });

            YQGeneratedSettlementCellLayout.Template template =
                YQGeneratedSettlementCellLayout.ResolveTemplate(plan, settlement);
            if (template != YQGeneratedSettlementCellLayout.Template.Compact)
            {
                Vector2 crossDirection = new Vector2(-entranceDirection.y, entranceDirection.x);
                float localStreetOffset =
                    template == YQGeneratedSettlementCellLayout.Template.MarketVillage
                        ? -7f
                        : template == YQGeneratedSettlementCellLayout.Template.FortifiedOutpost
                            ? -5f
                            : 0f;
                Vector2 crossCenter = center + entranceDirection * localStreetOffset;
                float crossHalfLength = Mathf.Min(26f, radius * 0.48f);
                // note: Comprehensive civic cells receive a second painted street matching their authored cross-lanes, producing readable blocks and a usable town center.
                paths.Add(new LivedPathSegment
                {
                    start = crossCenter - crossDirection * crossHalfLength,
                    end = crossCenter + crossDirection * crossHalfLength,
                    halfWidth = 2.8f,
                    shoulderWidth = 4.8f,
                    curveAmplitude = 0.35f,
                    curvePhase = Deterministic01(settlement.settlementId + "|cross_street") * Mathf.PI * 2f,
                    terraced = false
                });
            }
        }
    }

    private static void ClipLivedPathNetworkToTerrain(
        List<LivedPathSegment> paths,
        Terrain terrain)
    {
        ClipLivedPathNetworkToTerrain(paths, terrain, 0, paths != null ? paths.Count : 0);
    }

    // note: Cached V2 tile legs already hold the accepted terrain bounds, so this range clips only freshly rebuilt settlement streets.
    private static void ClipLivedPathNetworkToTerrain(
        List<LivedPathSegment> paths,
        Terrain terrain,
        int firstIndex,
        int endExclusive)
    {
        if (paths == null || terrain == null || terrain.terrainData == null)
            return;

        Vector3 position = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        int lastIndex = Mathf.Min(paths.Count, endExclusive) - 1;
        int boundedFirstIndex = Mathf.Clamp(firstIndex, 0, paths.Count);
        for (int index = lastIndex; index >= boundedFirstIndex; index--)
        {
            LivedPathSegment path = paths[index];
            if (path == null)
            {
                paths.RemoveAt(index);
                continue;
            }

            if (!TryClipLivedPathToTerrainBounds(
                    path.start,
                    path.end,
                    path.halfWidth,
                    path.shoulderWidth,
                    position,
                    size,
                    out Vector2 clippedStart,
                    out Vector2 clippedEnd))
            {
                // note: Distant V2 route spans are owned by streamed cells and must not be evaluated against the finite origin TerrainData.
                paths.RemoveAt(index);
                continue;
            }

            bool clipped = clippedStart != path.start || clippedEnd != path.end;
            path.start = clippedStart;
            path.end = clippedEnd;
            if (clipped)
            {
                // note: A clipped boundary leg is kept straight so its curve cannot bend back outside the authored terrain footprint.
                path.curveAmplitude = 0f;
            }
        }
    }

    private static bool AppendCachedV2PathProjection(
        List<LivedPathSegment> paths,
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        YQPreparedSpatialMaterializationV2 prepared,
        out bool worldProjectionBuilt)
    {
        worldProjectionBuilt = false;
        if (!MatchesCachedV2PathProjectionAuthority(plan, prepared))
        {
            // note: Project accepted route geometry once without terrain bounds, then derive exact clipped tile views from that immutable result.
            List<LivedPathSegment> worldProjection = new List<LivedPathSegment>();
            BuildV2LivedPathNetwork(worldProjection, prepared, plan, null);
            StoreCachedV2PathProjectionAuthority(plan, prepared, worldProjection);
            _semanticPathProjectionWorldBuilds++;
            worldProjectionBuilt = true;
        }

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData != null ? terrain.terrainData.size : Vector3.zero;
        int tileCacheIndex = -1;
        for (int index = 0; index < _cachedPathProjectionTiles.Count; index++)
        {
            V2PathProjectionTileCacheEntry entry = _cachedPathProjectionTiles[index];
            if (entry.terrainPosition.Equals(terrainPosition) && entry.terrainSize.Equals(terrainSize))
            {
                tileCacheIndex = index;
                break;
            }
        }

        bool tileCacheHit = tileCacheIndex >= 0;
        V2PathProjectionTileCacheEntry tile;
        if (tileCacheHit)
        {
            _semanticPathProjectionCacheHits++;
            tile = _cachedPathProjectionTiles[tileCacheIndex];
            // note: Keep recently used terrain bounds in the fixed-size tail of the cache so a visible envelope can revisit tiles without route rebuilds.
            if (tileCacheIndex != _cachedPathProjectionTiles.Count - 1)
            {
                _cachedPathProjectionTiles.RemoveAt(tileCacheIndex);
                _cachedPathProjectionTiles.Add(tile);
            }
        }
        else
        {
            _semanticPathProjectionCacheMisses++;
            tile = BuildClippedV2PathProjectionTile(terrain, terrainPosition, terrainSize);
            _cachedPathProjectionTiles.Add(tile);
            if (_cachedPathProjectionTiles.Count > MaximumCachedPathProjectionTiles)
                _cachedPathProjectionTiles.RemoveAt(0);
        }

        // note: Clone cached segment records because downstream terrain and ecology readers treat their input list as caller-owned data.
        for (int index = 0; index < tile.segments.Count; index++)
            paths.Add(CloneLivedPathSegment(tile.segments[index]));
        return tileCacheHit;
    }

    private static bool TryGetCachedPreparedPathAuthority(
        GeneratedWorldPlanRecord plan,
        out YQPreparedSpatialMaterializationV2 prepared)
    {
        prepared = null;
        GeneratedSpatialWorldPlanV2Record artifact = plan?.spatialPlanV2;
        if (plan == null ||
            !ReferenceEquals(_cachedPathProjectionPlan, plan) ||
            !ReferenceEquals(_cachedPathProjectionArtifact, artifact) ||
            _cachedPathProjectionPrepared == null ||
            artifact == null ||
            string.IsNullOrWhiteSpace(_cachedPathProjectionContentHash) ||
            !string.Equals(_cachedPathProjectionContentHash, artifact.contentHash, StringComparison.Ordinal) ||
            artifact.acceptanceState != GeneratedSpatialPlanAcceptanceState.Accepted ||
            artifact.validationErrors == null || artifact.validationErrors.Count > 0 ||
            string.IsNullOrWhiteSpace(artifact.contentHash) ||
            !string.Equals(artifact.contentHash, artifact.validatedContentHash, StringComparison.Ordinal) ||
            !string.Equals(plan.worldSeed, artifact.worldSeed, StringComparison.Ordinal) ||
            plan.spatialPlan == null ||
            !string.Equals(plan.spatialPlan.semanticFingerprint, artifact.semanticFingerprint, StringComparison.Ordinal) ||
            !(string.Equals(artifact.validationVersion, GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion, StringComparison.Ordinal) ||
              string.Equals(artifact.validationVersion, GeneratedSpatialWorldPlanV2Record.LegacyValidationVersion, StringComparison.Ordinal)))
            return false;

        // note: Reuse the hash-verified immutable projection for this accepted runtime plan; normal authority changes fall back to full validation.
        prepared = _cachedPathProjectionPrepared;
        return true;
    }

    private static bool MatchesCachedV2PathProjectionAuthority(
        GeneratedWorldPlanRecord plan,
        YQPreparedSpatialMaterializationV2 prepared)
    {
        if (!ReferenceEquals(_cachedPathProjectionPlan, plan) ||
            !ReferenceEquals(_cachedPathProjectionPrepared, prepared))
            return false;

        // note: V2 route projection consults settlements only to suppress duplicate approaches for authored procedural layouts.
        int settlementCount = plan.settlements != null ? plan.settlements.Count : 0;
        if (_cachedPathProjectionSettlementIds.Count != settlementCount ||
            _cachedPathProjectionHasProceduralLayouts.Count != settlementCount)
            return false;
        for (int index = 0; index < settlementCount; index++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[index];
            string settlementId = settlement != null ? settlement.settlementId : null;
            bool hasProceduralLayout = settlement?.proceduralLayout != null;
            if (!string.Equals(
                    _cachedPathProjectionSettlementIds[index],
                    settlementId,
                    StringComparison.Ordinal) ||
                _cachedPathProjectionHasProceduralLayouts[index] != hasProceduralLayout)
                return false;
        }
        return true;
    }

    private static void StoreCachedV2PathProjectionAuthority(
        GeneratedWorldPlanRecord plan,
        YQPreparedSpatialMaterializationV2 prepared,
        List<LivedPathSegment> worldProjection)
    {
        // note: Store one immutable accepted-world projection and invalidate bounded tile views whenever its authority changes.
        _cachedPathProjectionPlan = plan;
        _cachedPathProjectionPrepared = prepared;
        _cachedPathProjectionArtifact = plan?.spatialPlanV2;
        _cachedPathProjectionContentHash = _cachedPathProjectionArtifact?.contentHash ?? string.Empty;
        _cachedPathProjectionSegments = worldProjection;
        _cachedPathProjectionSettlementIds.Clear();
        _cachedPathProjectionHasProceduralLayouts.Clear();
        if (plan.settlements != null)
        {
            for (int index = 0; index < plan.settlements.Count; index++)
            {
                GeneratedSettlementRecord settlement = plan.settlements[index];
                _cachedPathProjectionSettlementIds.Add(settlement != null ? settlement.settlementId : null);
                _cachedPathProjectionHasProceduralLayouts.Add(settlement?.proceduralLayout != null);
            }
        }
        _cachedPathProjectionTiles.Clear();
    }

    // note: Apply the same padded route clip used by the original per-terrain projection before retaining a bounded local view.
    private static V2PathProjectionTileCacheEntry BuildClippedV2PathProjectionTile(
        Terrain terrain,
        Vector3 terrainPosition,
        Vector3 terrainSize)
    {
        V2PathProjectionTileCacheEntry tile = new V2PathProjectionTileCacheEntry
        {
            terrainPosition = terrainPosition,
            terrainSize = terrainSize
        };
        for (int index = 0; index < _cachedPathProjectionSegments.Count; index++)
        {
            LivedPathSegment source = _cachedPathProjectionSegments[index];
            if (terrain.terrainData == null)
            {
                tile.segments.Add(CloneLivedPathSegment(source));
                continue;
            }
            if (!TryClipLivedPathToTerrainBounds(
                    source.start,
                    source.end,
                    source.halfWidth,
                    source.shoulderWidth,
                    terrainPosition,
                    terrainSize,
                    out Vector2 clippedStart,
                    out Vector2 clippedEnd))
                continue;

            LivedPathSegment clipped = CloneLivedPathSegment(source);
            bool wasClipped = clippedStart != source.start || clippedEnd != source.end;
            clipped.start = clippedStart;
            clipped.end = clippedEnd;
            if (wasClipped)
                clipped.curveAmplitude = 0f;
            tile.segments.Add(clipped);
        }
        return tile;
    }

    private static LivedPathSegment CloneLivedPathSegment(LivedPathSegment source)
    {
        if (source == null)
            return null;
        return new LivedPathSegment
        {
            routeId = source.routeId,
            start = source.start,
            end = source.end,
            halfWidth = source.halfWidth,
            shoulderWidth = source.shoulderWidth,
            curveAmplitude = source.curveAmplitude,
            curvePhase = source.curvePhase,
            terraced = source.terraced
        };
    }

    private static bool TryClipLivedPathToTerrainBounds(
        Vector2 start,
        Vector2 end,
        float halfWidth,
        float shoulderWidth,
        Vector3 terrainPosition,
        Vector3 terrainSize,
        out Vector2 clippedStart,
        out Vector2 clippedEnd)
    {
        // note: Share the final route clip margin with the streaming-time cull so surviving path geometry is unchanged.
        float padding = halfWidth + shoulderWidth + 1f;
        return TryClipLivedPathToBounds(
            start,
            end,
            terrainPosition.x + padding,
            terrainPosition.x + terrainSize.x - padding,
            terrainPosition.z + padding,
            terrainPosition.z + terrainSize.z - padding,
            out clippedStart,
            out clippedEnd);
    }

    private static bool IsLivedPathPotentiallyRelevant(
        Vector2 start,
        Vector2 end,
        float halfWidth,
        float shoulderWidth,
        bool hasTerrainBounds,
        Vector3 terrainPosition,
        Vector3 terrainSize)
    {
        return !hasTerrainBounds || TryClipLivedPathToTerrainBounds(
            start,
            end,
            halfWidth,
            shoulderWidth,
            terrainPosition,
            terrainSize,
            out _,
            out _);
    }

    private static bool TryClipLivedPathToBounds(
        Vector2 start,
        Vector2 end,
        float minimumX,
        float maximumX,
        float minimumZ,
        float maximumZ,
        out Vector2 clippedStart,
        out Vector2 clippedEnd)
    {
        clippedStart = start;
        clippedEnd = end;
        Vector2 delta = end - start;
        float minimumT = 0f;
        float maximumT = 1f;
        if (!ClipLivedPathAxis(-delta.x, start.x - minimumX, ref minimumT, ref maximumT) ||
            !ClipLivedPathAxis(delta.x, maximumX - start.x, ref minimumT, ref maximumT) ||
            !ClipLivedPathAxis(-delta.y, start.y - minimumZ, ref minimumT, ref maximumT) ||
            !ClipLivedPathAxis(delta.y, maximumZ - start.y, ref minimumT, ref maximumT))
            return false;

        clippedStart = start + delta * minimumT;
        clippedEnd = start + delta * maximumT;
        return true;
    }

    private static bool ClipLivedPathAxis(
        float coefficient,
        float constant,
        ref float minimumT,
        ref float maximumT)
    {
        if (Mathf.Abs(coefficient) < .00001f)
            return constant >= 0f;

        float parameter = constant / coefficient;
        if (coefficient < 0f)
        {
            if (parameter > maximumT)
                return false;
            minimumT = Mathf.Max(minimumT, parameter);
        }
        else
        {
            if (parameter < minimumT)
                return false;
            maximumT = Mathf.Min(maximumT, parameter);
        }

        return minimumT <= maximumT;
    }

    private static void BuildV2LivedPathNetwork(
        List<LivedPathSegment> paths,
        YQPreparedSpatialMaterializationV2 prepared,
        GeneratedWorldPlanRecord plan,
        Terrain terrain)
    {
        // note: Cull route segments with the same padded terrain test used by publication instead of allocating a world-wide path list per tile.
        bool hasTerrainBounds = terrain != null && terrain.terrainData != null;
        Vector3 terrainPosition = hasTerrainBounds ? terrain.transform.position : Vector3.zero;
        Vector3 terrainSize = hasTerrainBounds ? terrain.terrainData.size : Vector3.zero;
        // note: Reuse one route-point buffer while streaming cells so reserve filtering adds no per-route garbage.
        List<Vector2> routePoints = new List<Vector2>(4);
        for (int routeIndex = 0;
             routeIndex < prepared.RouteCount;
             routeIndex++)
        {
            YQSpatialMaterializationRouteV2 route =
                prepared.GetRoute(routeIndex);
            int pointCount = prepared.GetRoutePointCount(routeIndex);
            if (pointCount < 2)
                continue;

            bool major = route.routeClass == YQRouteClassV2.PrimaryRoad;
            YQSpatialMaterializationSiteV2 destination = default;
            bool destinationIsSettlement = TryResolveV2Site(prepared, route.toSiteId, out destination) &&
                destination.kind == YQSiteKindV2.Settlement;
            bool settlementSkirtAuthored = false;
            routePoints.Clear();
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                YQSpatialMaterializationRoutePointV2 point =
                    prepared.GetRoutePoint(routeIndex, pointIndex);
                Vector2 position = new Vector2(point.x, point.z);
                if (pointIndex == 0)
                {
                    position = ResolveV2RouteGate(
                        prepared,
                        route.fromSiteId,
                        position);
                }
                else if (pointIndex == pointCount - 1)
                {
                    position = ResolveV2RouteGate(
                        prepared,
                        route.toSiteId,
                        position);
                }
                routePoints.Add(position);
            }
            YQGeneratedWorldLayout.FilterOriginLandmarkRouteControlPoints(
                routePoints,
                Mathf.Max(1f, route.width * 0.5f));
            for (int pointIndex = 1;
                 pointIndex < routePoints.Count;
                 pointIndex++)
            {
                if (settlementSkirtAuthored)
                    continue;
                Vector2 start = routePoints[pointIndex - 1];
                Vector2 end = routePoints[pointIndex];

                // note: Settlement-bound regional roads skirt the reserved footprint before turning into the authored entrance gate.
                // note: Paint the same two-leg skirt used by route validation so grading, bridges, and traversal share one corridor.
                if (destinationIsSettlement)
                {
                    float headingRadians = destination.headingDegrees * Mathf.Deg2Rad;
                    Vector2 entrance = new Vector2(Mathf.Sin(headingRadians), Mathf.Cos(headingRadians));
                    Vector2 lateral = new Vector2(-entrance.y, entrance.x);
                    Vector2 center = new Vector2(destination.x, destination.z);
                    float clearanceRadius = destination.reservedRadius + 30f;
                    Vector2 leg = end - start;
                    Vector2 closest = start;
                    if (leg.sqrMagnitude > 0.001f)
                        closest = start + leg * Mathf.Clamp01(Vector2.Dot(center - start, leg) / leg.sqrMagnitude);
                    if ((closest - center).sqrMagnitude <= clearanceRadius * clearanceRadius)
                    {
                        Vector2 radial = start - center;
                        if (radial.sqrMagnitude < 0.25f) radial = -entrance;
                        radial.Normalize();
                        float side = Mathf.Sign(Vector2.Dot(radial, lateral));
                        if (Mathf.Abs(side) < 0.5f) side = 1f;
                        float skirtRadius = clearanceRadius + 10f;
                        Vector2 outerA = center + radial * skirtRadius;
                        Vector2 outerB = center + (radial + lateral * side).normalized * skirtRadius;
                        Vector2 skirt = center + lateral * side * skirtRadius;
                        Vector2 gate = ResolveV2RouteGate(prepared, route.toSiteId, end);
                        AddV2LivedPathLeg(paths, route, start, outerA, major, hasTerrainBounds, terrainPosition, terrainSize);
                        AddV2LivedPathLeg(paths, route, outerA, outerB, major, hasTerrainBounds, terrainPosition, terrainSize);
                        AddV2LivedPathLeg(paths, route, outerB, skirt, major, hasTerrainBounds, terrainPosition, terrainSize);
                        AddV2LivedPathLeg(paths, route, skirt, gate, major, hasTerrainBounds, terrainPosition, terrainSize);
                        settlementSkirtAuthored = true;
                    }
                }
                if (!settlementSkirtAuthored)
                    AddV2LivedPathLeg(paths, route, start, end, major, hasTerrainBounds, terrainPosition, terrainSize);
            }
        }

        for (int siteIndex = 0;
             siteIndex < prepared.SiteCount;
             siteIndex++)
        {
            YQSpatialMaterializationSiteV2 site =
                prepared.GetSite(siteIndex);
            if (FindProceduralSettlement(plan, site.sourceSemanticId) != null) continue;
            if (site.kind == YQSiteKindV2.PointOfInterest)
            {
                // note: Public POIs receive a short route-owned approach; concealed or forested POIs deliberately keep their hidden access instead of advertising a road to the landmark.
                if (site.concealed || string.IsNullOrWhiteSpace(site.frontageRouteId))
                    continue;

                Vector2 frontage = new Vector2(site.frontageX, site.frontageZ);
                Vector2 poiCenter = new Vector2(site.x, site.z);
                Vector2 inward = poiCenter - frontage;
                if (inward.sqrMagnitude < 0.25f)
                    continue;
                inward.Normalize();
                Vector2 approachEnd = poiCenter - inward * Mathf.Min(5f, site.reservedRadius * 0.2f);
                if (!IsLivedPathPotentiallyRelevant(
                        frontage,
                        approachEnd,
                        1.7f,
                        2.8f,
                        hasTerrainBounds,
                        terrainPosition,
                        terrainSize))
                    continue;
                paths.Add(new LivedPathSegment
                {
                    routeId = site.frontageRouteId,
                    start = frontage,
                    end = approachEnd,
                    halfWidth = 1.7f,
                    shoulderWidth = 2.8f,
                    curveAmplitude = 0.18f,
                    curvePhase = Deterministic01(site.sourceSemanticId + "|poi_approach") * Mathf.PI * 2f,
                    terraced = true
                });
                continue;
            }
            if (site.kind != YQSiteKindV2.Settlement ||
                string.IsNullOrWhiteSpace(site.frontageRouteId))
            {
                continue;
            }

            float headingRadians = site.headingDegrees * Mathf.Deg2Rad;
            Vector2 entranceDirection = new Vector2(
                Mathf.Sin(headingRadians),
                Mathf.Cos(headingRadians));
            Vector2 center = new Vector2(site.x, site.z);
            Vector2 settlementApproachStart = center + entranceDirection * (site.reservedRadius + 18f);
            Vector2 settlementApproachEnd = center - entranceDirection * Mathf.Min(20f, site.reservedRadius * 0.34f);
            if (!IsLivedPathPotentiallyRelevant(
                    settlementApproachStart,
                    settlementApproachEnd,
                    3.4f,
                    5.8f,
                    hasTerrainBounds,
                    terrainPosition,
                    terrainSize))
                continue;
            // note: The exterior corridor ends at the accepted gate and a matching painted spine continues into the semantic district without inventing a second street graph.
            paths.Add(new LivedPathSegment
            {
                start = settlementApproachStart,
                end = settlementApproachEnd,
                halfWidth = 3.4f,
                shoulderWidth = 5.8f,
                curveAmplitude = 0f,
                curvePhase = 0f,
                terraced = false
            });
        }
    }

    private static void AddV2LivedPathLeg(
        List<LivedPathSegment> paths,
        YQSpatialMaterializationRouteV2 route,
        Vector2 start,
        Vector2 end,
        bool major,
        bool hasTerrainBounds,
        Vector3 terrainPosition,
        Vector3 terrainSize)
    {
        // note: Centralize V2 leg dimensions so detoured and straight segments remain visually and physically identical.
        float halfWidth = Mathf.Max(1f, route.width * 0.5f);
        float shoulderWidth = Mathf.Max(route.width * 0.5f, route.shoulderWidth);
        if (YQGeneratedWorldLayout.TryBuildOriginLandmarkDetour(
                start, end, halfWidth, out Vector2[] waypoints))
        {
            // note: Route legs detour around the authored shrine reserve instead of allowing a painted trail to pass through its collider.
            Vector2 previous = start;
            for (int index = 0; index < waypoints.Length; index++)
            {
                AddV2LivedPathSegmentIfRelevant(
                    paths, route.routeId, previous, waypoints[index],
                    halfWidth, shoulderWidth, major,
                    hasTerrainBounds, terrainPosition, terrainSize);
                previous = waypoints[index];
            }
            start = previous;
        }
        AddV2LivedPathSegmentIfRelevant(
            paths, route.routeId, start, end,
            halfWidth, shoulderWidth, major,
            hasTerrainBounds, terrainPosition, terrainSize);
    }

    private static void AddV2LivedPathSegmentIfRelevant(
        List<LivedPathSegment> paths,
        string routeId,
        Vector2 start,
        Vector2 end,
        float halfWidth,
        float shoulderWidth,
        bool major,
        bool hasTerrainBounds,
        Vector3 terrainPosition,
        Vector3 terrainSize)
    {
        // note: Keep the authoritative final clip as the geometry owner while skipping off-tile allocations that it would immediately remove.
        if (!IsLivedPathPotentiallyRelevant(
                start,
                end,
                halfWidth,
                shoulderWidth,
                hasTerrainBounds,
                terrainPosition,
                terrainSize))
            return;

        paths.Add(new LivedPathSegment
        {
            routeId = routeId,
            start = start,
            end = end,
            halfWidth = halfWidth,
            shoulderWidth = shoulderWidth,
            curveAmplitude = 0f,
            curvePhase = 0f,
            terraced = !major
        });
    }

    private static GeneratedSettlementRecord FindProceduralSettlement(GeneratedWorldPlanRecord plan, string id)
    {
        if (plan?.settlements != null)
            foreach (var settlement in plan.settlements)
                if (settlement != null && settlement.settlementId == id && settlement.proceduralLayout != null) return settlement;
        return null;
    }

    private static void AppendProceduralSettlementStreets(
        List<LivedPathSegment> paths,
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        YQPreparedSpatialMaterializationV2 prepared)
    {
        if (plan.settlements == null) return;
        bool v2 = YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan);
        if (v2 && prepared == null &&
            !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out prepared, out _)) return;
        foreach (var settlement in plan.settlements)
        {
            var layout = settlement?.proceduralLayout;
            if (layout?.streets == null || layout.streets.Count == 0) continue;
            float heading, gateRadius;
            if (v2 && prepared.TryGetSiteBySemanticId(settlement.settlementId, out var site))
            { heading = site.headingDegrees; gateRadius = site.reservedRadius * .82f + 6f; }
            else if (!v2 && YQGeneratedWorldSpatialPlanner.TryGetLocation(plan, settlement.settlementId, out var location))
            { heading = location.entranceHeadingDegrees; gateRadius = Mathf.Clamp(location.footprintRadius, 24f, 72f) + 14f; }
            else continue;
            Quaternion rotation = Quaternion.Euler(0, heading, 0);
            Vector3 anchor = YQGeneratedWorldLayout.GetSettlementAnchor(plan, settlement, terrain);
            foreach (var street in layout.streets)
            {
                Vector3 from = anchor + rotation * street.start, to = anchor + rotation * street.end;
                paths.Add(new LivedPathSegment { start = new Vector2(from.x, from.z), end = new Vector2(to.x, to.z),
                    halfWidth = street.width * .5f, shoulderWidth = street.width * .5f + 1.5f });
            }
            // note: The accepted regional road joins the new spine along its clear centre corridor, not through a source building.
            Vector3 gate = anchor + rotation * new Vector3(0, 0, gateRadius);
            paths.Add(new LivedPathSegment { start = new Vector2(anchor.x, anchor.z), end = new Vector2(gate.x, gate.z),
                halfWidth = 3f, shoulderWidth = 4.5f });
        }
    }

    private static Vector2 ResolveV2RouteGate(
        YQPreparedSpatialMaterializationV2 prepared,
        string siteId,
        Vector2 fallback)
    {
        if (!TryResolveV2Site(prepared, siteId, out YQSpatialMaterializationSiteV2 site) ||
            site.kind == YQSiteKindV2.NaturalFeature)
        {
            return fallback;
        }

        float headingRadians = site.headingDegrees * Mathf.Deg2Rad;
        Vector2 entranceDirection = new Vector2(
            Mathf.Sin(headingRadians),
            Mathf.Cos(headingRadians));
        return new Vector2(site.x, site.z) +
               // note: The shared gate datum keeps terrain painting, bridge crossings, and route validation on one endpoint.
               // note: Match the painted entrance spine to the exterior gate offset used by route validation and bridge analysis.
               entranceDirection * (site.reservedRadius + 18f);
    }

    private static bool TryResolveV2Site(
        YQPreparedSpatialMaterializationV2 prepared,
        string siteId,
        out YQSpatialMaterializationSiteV2 site)
    {
        // note: Persisted routes can use qualified keys while the runtime catalog indexes semantic site ids.
        if (prepared.TryGetSiteBySiteId(siteId, out site) || prepared.TryGetSiteBySemanticId(siteId, out site))
            return true;
        string semanticId = siteId;
        int separator = semanticId == null ? -1 : semanticId.LastIndexOf(':');
        if (separator >= 0 && separator + 1 < semanticId.Length)
            semanticId = semanticId.Substring(separator + 1);
        return prepared.TryGetSiteBySemanticId(semanticId, out site);
    }

    private static Vector2 ResolveLiveRouteEndpoint(
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        string locationId,
        Vector2 fallback)
    {
        if (string.Equals(locationId, "origin_vey", StringComparison.OrdinalIgnoreCase))
        {
            Vector3 origin = YQGeneratedWorldLayout.GetVeyOriginAnchor();
            return new Vector2(origin.x, origin.z);
        }

        for (int i = 0; i < plan.settlements.Count; i++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[i];
            if (settlement == null ||
                !string.Equals(settlement.settlementId, locationId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            Vector3 anchor = YQGeneratedWorldLayout.GetSettlementAnchor(plan, settlement, terrain);
            Vector2 endpoint = new Vector2(anchor.x, anchor.z);
            if (YQGeneratedWorldSpatialPlanner.TryGetLocation(
                    plan,
                    settlement.settlementId,
                    out GeneratedSpatialLocationRecord location))
            {
                float headingRadians = location.entranceHeadingDegrees * Mathf.Deg2Rad;
                Vector2 entrance = new Vector2(
                    Mathf.Sin(headingRadians),
                    Mathf.Cos(headingRadians));
                endpoint += entrance *
                    (Mathf.Clamp(location.footprintRadius, 24f, 72f) + 14f);
            }
            // note: Regional roads terminate at the persisted gate, never at the civic center where multiple approaches would cut through buildings.
            return endpoint;
        }

        for (int i = 0; i < plan.encampments.Count; i++)
        {
            GeneratedEncampmentRecord encampment = plan.encampments[i];
            if (encampment == null ||
                !string.Equals(encampment.encampmentId, locationId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            Vector3 anchor = YQGeneratedWorldLayout.GetEncampmentAnchor(plan, encampment, terrain);
            return new Vector2(anchor.x, anchor.z);
        }

        return fallback;
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
            float effectiveShoulder = Mathf.Max(
                0.6f,
                path.shoulderWidth * LivedPathShoulderScale);
            float mask = 1f - SmoothThreshold(
                path.halfWidth,
                path.halfWidth + effectiveShoulder,
                distance);
            strongest = Mathf.Max(strongest, mask);
        }

        return strongest;
    }

    public static float SmoothThreshold(float minimum, float maximum, float value)
    {
        // note: Unity SmoothStep interpolates outputs; normalize metres/elevation first to obtain a bounded threshold mask.
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum, maximum, value));
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

    private static float ProjectRoadGridEdge(float[,] heights, float[,] blends, bool[,] fixedGrid,
        int ax, int az, int bx, int bz, float rise)
    {
        // note: Foundations remain immutable; off-road shoulders retain their authored relief and are not part of the walking-corridor solve.
        if (blends[bz, bx] < .999f && !fixedGrid[bz, bx]) return 0f;
        float delta = heights[bz, bx] - heights[az, ax];
        float excess = Mathf.Abs(delta) - rise;
        if (excess <= 0f) return 0f;
        float correction = Mathf.Sign(delta) * excess;
        if (fixedGrid[bz, bx]) heights[az, ax] += correction;
        else { heights[az, ax] += correction * .5f; heights[bz, bx] -= correction * .5f; }
        return excess;
    }

    private sealed class RoadHeightNode
    {
        // note: Shared samples own one elevation across every incident road segment.
        public Vector2 point;
        public float height;
        public bool fixedHeight;
    }

    private readonly struct RoadHeightEdge
    {
        public readonly int a, b;
        public readonly float rise;
        public RoadHeightEdge(int from, int to, float maximumRise)
        { a = from; b = to; rise = maximumRise; }
    }

    private static IEnumerator BuildConnectedRoadProfiles(List<LivedPathSegment> paths, float[,] heights,
        Vector3 terrainPosition, Vector3 terrainSize, IReadOnlyList<LivedPathTerrainReservation> reservations,
        Action<List<float[]>> completed)
    {
        var nodes = new List<RoadHeightNode>();
        var edges = new List<RoadHeightEdge>();
        var indices = new List<int[]>();
        var shared = new Dictionary<Vector2Int, int>();
        var buckets = new Dictionary<Vector2Int, List<int>>();
        const float maximumGrade = .35f;
        for (int p = 0; p < paths.Count; p++)
        {
            var path = paths[p];
            if (path == null) { indices.Add(Array.Empty<int>()); continue; }
            int count = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(path.start, path.end) / 2f) + 1);
            var samples = new int[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 point = ResolveLivedPathCenter(path, i / (float)(count - 1));
                var key = new Vector2Int(Mathf.RoundToInt(point.x * 100f), Mathf.RoundToInt(point.y * 100f));
                if (!shared.TryGetValue(key, out int id))
                {
                    id = nodes.Count;
                    shared.Add(key, id);
                    nodes.Add(new RoadHeightNode { point = point,
                        height = SampleHeightmapWorldHeight(heights, terrainPosition, terrainSize, point),
                        fixedHeight = IsInsideLivedPathTerrainReservation(reservations, point) });
                    var bucket = new Vector2Int(Mathf.FloorToInt(point.x / 4f), Mathf.FloorToInt(point.y / 4f));
                    // note: Nearby samples constrain junctions and crossing corridors even when their waypoint lists do not share an exact vertex.
                    for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                        if (buckets.TryGetValue(bucket + new Vector2Int(x, z), out var nearby))
                            foreach (int other in nearby)
                            {
                                float distance = Vector2.Distance(point, nodes[other].point);
                                if (distance <= 4f) edges.Add(new RoadHeightEdge(other, id, distance * maximumGrade));
                            }
                    if (!buckets.TryGetValue(bucket, out var list)) buckets.Add(bucket, list = new List<int>());
                    list.Add(id);
                }
                samples[i] = id;
                if (i > 0 && samples[i - 1] != id)
                    edges.Add(new RoadHeightEdge(samples[i - 1], id,
                        Vector2.Distance(nodes[samples[i - 1]].point, point) * maximumGrade));
            }
            indices.Add(samples);
            // note: A completed road-profile path is real solver progress and keeps long connected networks visible to the startup watchdog.
            YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
            yield return null;
        }
        float relaxationFrameStartedAt = Time.realtimeSinceStartup;
        // note: Keep the connected-grade relaxation bounded for the beta graph; the residual is measured below and remains subject to the same route validation instead of blocking startup indefinitely.
        const int maximumProfileRelaxationPasses = 256;
        for (int pass = 0; pass < maximumProfileRelaxationPasses; pass++)
        {
            float violation = 0f;
            for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                RoadHeightEdge edge = edges[edgeIndex];
                var a = nodes[edge.a]; var b = nodes[edge.b];
                float difference = b.height - a.height;
                float excess = Mathf.Abs(difference) - edge.rise;
                // note: Check the batch budget before any skip path so satisfied and immutable edges cannot create one unbounded synchronous pass.
                if ((edgeIndex & 127) == 127 &&
                    Time.realtimeSinceStartup - relaxationFrameStartedAt >= 0.0015f)
                {
                    // note: A completed relaxation batch is real profile progress, not a traversability publication.
                    YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                    yield return null;
                    relaxationFrameStartedAt = Time.realtimeSinceStartup;
                }
                if (excess <= .001f) continue;
                if (a.fixedHeight && b.fixedHeight)
                {
                    // note: Fixed-to-fixed residuals are immutable by this solver; preserve them for the final physical gate instead of preventing adjustable edges from converging.
                    continue;
                }
                violation = Mathf.Max(violation, excess);
                float correction = Mathf.Sign(difference) * excess;
                // note: Preserve fixed foundations exactly; free endpoints share the required cut and fill instead of creating a discontinuity.
                if (a.fixedHeight) b.height -= correction;
                else if (b.fixedHeight) a.height += correction;
                else { a.height += correction * .5f; b.height -= correction * .5f; }

            }
            if (violation <= .01f) break;
            if (pass % 8 == 0)
            {
                // note: A completed relaxation pass advances the watchdog while the final residual remains subject to physical validation.
                YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                yield return null;
            }
        }
        float remaining = 0f;
        foreach (var edge in edges)
            remaining = Mathf.Max(remaining, Mathf.Abs(nodes[edge.a].height - nodes[edge.b].height) - edge.rise);
        // note: Unsatisfied constraints remain visible and the unchanged final physical traversal gate rejects unsafe terrain.
        Debug.Log("[YQRoadGrade] Connected profile residual=" + remaining.ToString("0.000") + "m; nodes=" + nodes.Count);
        var result = new List<float[]>(indices.Count);
        foreach (var samples in indices)
        {
            var profile = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++) profile[i] = nodes[samples[i]].height;
            result.Add(profile);
        }
        completed(result);
    }

    private static float[] BuildLivedPathHeightProfile(
        LivedPathSegment path,
        float pathLength,
        float[,] heights,
        Vector3 terrainPosition,
        Vector3 terrainSize,
        IReadOnlyList<LivedPathTerrainReservation> protectedFootprints)
    {
        int sampleCount = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Max(1f, pathLength) / 4f) + 1,
            2,
            257);
        float[] profile = new float[sampleCount];
        float[] scratch = new float[sampleCount];
        // note: Terrain that the write pass cannot change must remain an explicit height constraint in its road profile.
        bool[] fixedSamples = new bool[sampleCount];

        for (int index = 0; index < sampleCount; index++)
        {
            float pathT = index / (float)(sampleCount - 1);
            fixedSamples[index] = IsInsideLivedPathTerrainReservation(protectedFootprints, ResolveLivedPathCenter(path, pathT));
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
                if (fixedSamples[index]) { scratch[index] = profile[index]; continue; }
                scratch[index] =
                    profile[index - 1] * 0.25f +
                    profile[index] * 0.5f +
                    profile[index + 1] * 0.25f;
            }

            float[] swap = profile;
            profile = scratch;
            scratch = swap;
        }

        float sampleSpacing = pathLength / Mathf.Max(1, sampleCount - 1);
        float maximumHeightDelta = sampleSpacing * (path.terraced ? 0.48f : 0.40f);
        for (int pass = 0; pass < 3; pass++)
        {
            for (int index = 1; index < sampleCount - 1; index++)
            {
                if (fixedSamples[index]) continue;
                profile[index] = Mathf.Clamp(
                    profile[index],
                    profile[index - 1] - maximumHeightDelta,
                    profile[index - 1] + maximumHeightDelta);
            }

            for (int index = sampleCount - 2; index > 0; index--)
            {
                if (fixedSamples[index]) continue;
                profile[index] = Mathf.Clamp(
                    profile[index],
                    profile[index + 1] - maximumHeightDelta,
                    profile[index + 1] + maximumHeightDelta);
            }
        }

        // note: Roads follow local landforms but enforce a bidirectional grade cap, preventing a smoothed profile from retaining five-metre rises between adjacent playable samples.
        for (int index = 1; index < sampleCount; index++)
        {
            // note: Record the first unsatisfied construction constraint rather than reporting a grade-capped profile that still contains a cliff.
            if (Mathf.Abs(profile[index] - profile[index - 1]) <= maximumHeightDelta + .01f) continue;
            Debug.LogError("[YQRoadGrade] Unresolved profile start=" + path.start + " end=" + path.end +
                " sample=" + index + "/" + (sampleCount - 1) + " rise=" + Mathf.Abs(profile[index] - profile[index - 1]) +
                " allowed=" + maximumHeightDelta + " protected=" + fixedSamples[index - 1] + "/" + fixedSamples[index]);
            break;
        }
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

            if (reservation.halfSize.x > 0f && reservation.halfSize.y > 0f)
            {
                // note: Match the same local footprint axes used by parcel earthworks.
                Vector2 delta = point - reservation.center;
                Vector3 local = Quaternion.Euler(0f, -reservation.heading, 0f) * new Vector3(delta.x, 0f, delta.y);
                if (Mathf.Abs(local.x) <= reservation.halfSize.x && Mathf.Abs(local.z) <= reservation.halfSize.y) return true;
                continue;
            }

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
        MacroWaterSet macroWater,
        float preparedWaterMask)
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
            // note: The V2 ecology sample already measured this cell's water mask; reuse it instead of running the full spatial sampler twice per detail cell.
            (macroWater.preparedV2 != null
                ? preparedWaterMask >= ResolvePreparedWaterMaskThreshold(2f)
                : IsInsideMacroWaterFootprint(
                    terrain,
                    plan,
                    candidate,
                    2f,
                    macroWater)))
        {
            return false;
        }

        // note: Terrain details use cached anchors and water masks, avoiding repeated projection work across the 512 by 512 density pass.
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

        YQPreparedSpatialMaterializationV2 preparedV2 = null;
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) &&
            !YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out preparedV2,
                out string projectionFailure))
        {
            // note: Keep the last valid water hierarchy intact when an authoritative V2 projection cannot be prepared.
            Debug.LogError(
                "[YQGeneratedWorldEnvironment] V2 visible-water projection rejected: " +
                projectionFailure);
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

        if (preparedV2 != null)
        {
            // note: Accepted V2 hydrology owns every visible lake, wetland, river, coastline, and waterfall; no seed-only drainage branches are invented downstream.
            yield return BuildPreparedV2WaterBodiesRoutine(
                root.transform,
                terrain,
                plan,
                preparedV2,
                registry,
                waterAssetPath,
                waterPrefab,
                count => spawned = count);

            if (spawned == 0)
            {
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
            }

            completed?.Invoke(spawned);
            yield break;
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
                    terrain,
                    basin,
                    basinIndex,
                    YQHydrologyKindV2.Lake))
            {
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                continue;
            }

            spawned++;
            yield return null;
        }

        int drainageStreams =
            0;

        // note: Each basin receives two narrow deterministic drainage ribbons that sit on sampled terrain, making water part of the travel landscape instead of isolated decorative ellipses.
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

            for (int branchIndex = 0;
                 branchIndex < 2;
                 branchIndex++)
            {
                List<Vector3> streamPoints =
                    BuildDrainageStreamPoints(
                        terrain,
                        basin,
                        basinIndex,
                        branchIndex);

                if (streamPoints.Count < 6)
                    continue;

                AsyncInstantiateOperation<GameObject> operation =
                    UnityEngine.Object.InstantiateAsync(
                        waterPrefab,
                        root.transform);
                operation.priority = -1;
                yield return operation;

                GameObject streamInstance =
                    operation.Result != null &&
                    operation.Result.Length > 0
                        ? operation.Result[0]
                        : null;

                if (streamInstance == null)
                    continue;

                streamInstance.name =
                    "DrainageStream_" +
                    basinIndex +
                    "_" +
                    branchIndex +
                    "__" +
                    waterPrefab.name;
                streamInstance.transform.position =
                    Vector3.zero;
                streamInstance.transform.rotation =
                    Quaternion.identity;
                streamInstance.transform.localScale =
                    Vector3.one;

                registry.ApplyMaterialOverrides(
                    waterAssetPath,
                    streamInstance);

                yield return YQRuntimeUrpMaterialRepair
                    .RepairMaterialHierarchyRoutine(
                        streamInstance,
                        null);

                RemoveWildernessCollision(
                    streamInstance);

                if (!TryConfigureDrainageWaterSurface(
                        streamInstance,
                        streamPoints,
                        basinIndex,
                        branchIndex))
                {
                    streamInstance.SetActive(false);
                    UnityEngine.Object.Destroy(streamInstance);
                    continue;
                }

                drainageStreams++;
                yield return null;
            }
        }

        if (spawned == 0)
        {
            root.SetActive(false);
            UnityEngine.Object.Destroy(root);
        }

        completed?.Invoke(
            spawned +
            drainageStreams);
    }

    private static IEnumerator BuildPreparedV2WaterBodiesRoutine(
        Transform root,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQPreparedSpatialMaterializationV2 prepared,
        YQRuntimeWorldAssetRegistry registry,
        string waterAssetPath,
        GameObject waterPrefab,
        Action<int> completed)
    {
        int spawned = 0;
        // note: Terrain and connected water surfaces resolve their accepted datum through one sampler.
        if (!YQSpatialBlueprintTerrainSamplerV2.TryPrepare(plan, out var waterSampler, out string waterFailure))
            throw new InvalidOperationException("Water surface sampler failed: " + waterFailure);
        if (root == null || terrain == null || prepared == null ||
            registry == null || waterPrefab == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        for (int waterIndex = 0;
             waterIndex < prepared.WaterCount;
             waterIndex++)
        {
            if (prepared.GetWaterPointCount(waterIndex) == 0)
                continue;

            AsyncInstantiateOperation<GameObject> operation =
                UnityEngine.Object.InstantiateAsync(
                    waterPrefab,
                    root);
            operation.priority = -1;
            yield return operation;

            GameObject instance =
                operation.Result != null && operation.Result.Length > 0
                    ? operation.Result[0]
                    : null;
            if (instance == null)
                continue;

            YQSpatialMaterializationWaterV2 water =
                prepared.GetWater(waterIndex);
            instance.name =
                "V2Water_" +
                waterIndex +
                "_" +
                water.kind +
                "__" +
                waterPrefab.name;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            registry.ApplyMaterialOverrides(
                waterAssetPath,
                instance);
            yield return YQRuntimeUrpMaterialRepair
                .RepairMaterialHierarchyRoutine(
                    instance,
                    null);
            RemoveWildernessCollision(instance);

            if (!TryConfigurePreparedV2WaterSurface(
                    instance,
                    terrain,
                    prepared,
                    waterSampler,
                    waterIndex))
            {
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                continue;
            }

            spawned++;

            // note: One accepted hydrology feature is integrated per frame so imported material repair and mesh publication cannot collapse into a loading spike.
            yield return null;
        }

        completed?.Invoke(spawned);
    }

    private static bool TryConfigurePreparedV2WaterSurface(
        GameObject instance,
        Terrain terrain,
        YQPreparedSpatialMaterializationV2 prepared,
        YQSpatialBlueprintTerrainSamplerV2 waterSampler,
        int waterIndex)
    {
        if (instance == null || terrain == null ||
            terrain.terrainData == null || prepared == null)
        {
            return false;
        }

        YQSpatialMaterializationWaterV2 water =
            prepared.GetWater(waterIndex);
        // note: Bind the accepted hydrology identity to the real generated feature root before its surface is published, so overlays can replay across reload and rebind.
        YQStreamedFeatureOverlayTarget overlayTarget = instance.GetComponent<YQStreamedFeatureOverlayTarget>() ?? instance.AddComponent<YQStreamedFeatureOverlayTarget>();
        overlayTarget.featureId = water.hydrologyId ?? string.Empty;
        overlayTarget.objectId = instance.name;
        // note: Replay the accepted overlay when a generated water root binds after streamer configuration so hydrology tombstones survive reconstruction.
        YQPlayerFollowingSemanticChunkStreamer overlayStreamer = UnityEngine.Object.FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
        if (overlayStreamer != null)
            overlayStreamer.RegisterFeatureOverlayTarget(overlayTarget.featureId, instance, overlayTarget.objectId);
        if ((water.kind == YQHydrologyKindV2.Lake ||
             water.kind == YQHydrologyKindV2.Wetland) &&
            TryBuildPreparedAreaWaterBasin(
                terrain,
                prepared,
                waterIndex,
                out YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin))
        {
            instance.transform.position = basin.CenterWorld;
            bool configured = TryConfigureMacroWaterSurface(
                instance,
                terrain,
                basin,
                waterIndex,
                water.kind);
            if (configured)
                // note: Lakes and wetlands receive a handful of deterministic low-cost fish paths after their surface is published.
                YQGeneratedFish.SpawnForLake(instance.transform, basin, water.kind, water.hydrologyId);
            return configured;
        }

        int pointCount = prepared.GetWaterPointCount(waterIndex);
        if (pointCount < 2)
        {
            // note: Keep a precise failure reason when an accepted channel cannot publish a ribbon; silent destruction made missing rivers look like a rendering bug.
            Debug.LogError("[YQGeneratedWorldEnvironment] Accepted water channel has fewer than two points. index=" + waterIndex + ", kind=" + water.kind + ", hydrologyId=" + water.hydrologyId);
            return false;
        }

        List<Vector3> points = new List<Vector3>(pointCount);
        List<float> widths = new List<float>(pointCount);
        for (int pointIndex = 0;
             pointIndex < pointCount;
             pointIndex++)
        {
            YQSpatialMaterializationWaterPointV2 point =
                prepared.GetWaterPoint(
                    waterIndex,
                    pointIndex);
            float normalizedSurface = point.waterSurfaceNormalized > 0f
                ? point.waterSurfaceNormalized
                : water.waterLevelNormalized;
            points.Add(
                new Vector3(
                    point.x,
                    terrain.transform.position.y +
                        terrain.terrainData.size.y *
                            Mathf.Clamp01(normalizedSurface) +
                        0.045f,
                    point.z));
            widths.Add(
                Mathf.Max(
                    water.kind == YQHydrologyKindV2.River ? 14f : water.kind == YQHydrologyKindV2.Waterfall ? 10f : 1.5f,
                    Mathf.Max(water.nominalWidth, point.width)));
        }

        // note: Extend the accepted river to its declared receiving body before resampling, so the visible ribbon and bank foam share the same confluence instead of ending in a detached cap.
        ExtendPreparedWaterIntoReceivingBody(terrain, prepared, water, points, widths);

        // note: Keep already-connected accepted endpoints intact so the mouth cannot reverse direction inside its receiving lake.

        Renderer[] importedRenderers =
            instance.GetComponentsInChildren<Renderer>(true);
        Renderer materialSource = null;
        for (int index = 0; index < importedRenderers.Length; index++)
        {
            Renderer importedRenderer = importedRenderers[index];
            if (importedRenderer == null)
                continue;
            if (materialSource == null &&
                importedRenderer.sharedMaterials != null &&
                importedRenderer.sharedMaterials.Length > 0 &&
                importedRenderer.sharedMaterials[0] != null)
            {
                materialSource = importedRenderer;
            }
            importedRenderer.enabled = false;
        }

        if (materialSource == null)
        {
            // note: The generated surface requires an approved imported material source; report the rejected channel instead of leaving an unexplained invisible river.
            Debug.LogError("[YQGeneratedWorldEnvironment] Approved water prefab had no usable renderer material for channel index=" + waterIndex + ", kind=" + water.kind + ", hydrologyId=" + water.hydrologyId);
            return false;
        }

        // note: Subdivide long control segments before resolving confluences so a high endpoint cannot span above a lower connected body.
        var sampledPoints = new List<Vector3>();
        var sampledWidths = new List<float>();
        var controlOffsets = new Vector3[points.Count];
        var sampledOffsets = new List<Vector3>();
        // note: Interpolate authored ribbon edges exactly; recomputing tangents after subdivision would move banks at every bend.
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 tangent = points[Mathf.Min(points.Count - 1, i + 1)] - points[Mathf.Max(0, i - 1)];
            tangent.y = 0f;
            if (tangent.sqrMagnitude < .0001f) tangent = Vector3.forward;
            tangent.Normalize();
            controlOffsets[i] = new Vector3(-tangent.z, 0f, tangent.x) * Mathf.Max(.75f, widths[i] * .5f);
        }
        for (int i = 1; i < points.Count; i++)
        {
            // note: Rivers and waterfalls use fine accepted subdivisions so close views keep a continuous shoreline instead of exposing sawtooth ribbon edges.
            float subdivisionLength = water.kind == YQHydrologyKindV2.River || water.kind == YQHydrologyKindV2.Waterfall ? .35f : 2f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(points[i - 1], points[i]) / subdivisionLength));
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps;
                sampledPoints.Add(Vector3.Lerp(points[i - 1], points[i], t));
                sampledWidths.Add(Mathf.Lerp(widths[i - 1], widths[i], t));
                sampledOffsets.Add(Vector3.Lerp(controlOffsets[i - 1], controlOffsets[i], t));
            }
        }
        sampledPoints.Add(points[points.Count - 1]);
        sampledWidths.Add(widths[widths.Count - 1]);
        sampledOffsets.Add(controlOffsets[controlOffsets.Length - 1]);
        Mesh mesh = BuildPreparedWaterRibbonMesh(
            sampledPoints,
            sampledWidths,
            waterIndex,
            water.kind,
            waterSampler,
            terrain,
            sampledOffsets);
        if (mesh == null)
            return false;

        GameObject surface =
            new GameObject("CompiledHydrologySurface");
        surface.transform.SetParent(instance.transform, false);
        MeshFilter filter = surface.AddComponent<MeshFilter>();
        MeshRenderer rendererComponent =
            surface.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        rendererComponent.sharedMaterials =
            materialSource.sharedMaterials;
        // note: Scale the surface shader with the accepted downhill grade so steep rivers visibly outrun flat channels without adding simulation work.
        float flowScale = EstimateWaterFlowScale(points, water.kind);
        ConfigureGeneratedWaterMaterial(rendererComponent, water.kind, flowScale);
        // note: Receiving basins render first and incoming channels render over their shared mouth so confluences blend instead of sorting into a hard polygon seam.
        rendererComponent.sortingOrder =
            water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland ? 0 : 2;
        // note: Transparent receiving bodies draw first while the connected river mouth remains visible over the lake surface at the same hydraulic datum.
        // note: Connected water uses one transparent draw order; the receiving basin and incoming ribbon blend by coverage instead of exposing a hard sorting seam.
        // note: Preserve the kind-specific order assigned above; channels must sit above receiving basins at a shared mouth.
        rendererComponent.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        rendererComponent.receiveShadows = false;
        rendererComponent.lightProbeUsage =
            UnityEngine.Rendering.LightProbeUsage.Off;
        rendererComponent.reflectionProbeUsage =
            UnityEngine.Rendering.ReflectionProbeUsage.Off;
        rendererComponent.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;

        // note: Move the imported water texture down the generated ribbon without changing water height or physics.
        surface.AddComponent<YQWaterSurfaceMotion>().Configure(filter, rendererComponent, water.kind, flowScale);
        Mesh foamMesh = BuildPreparedWaterBankFoamMesh(
            sampledPoints,
            sampledWidths,
            waterIndex,
            water.kind,
            waterSampler,
            terrain);
        // note: River and waterfall surfaces use depth-aware contact and impact foam on the ribbon itself; a full-length child strip created pale wedges across the bank, so reserve standalone geometry for narrow wetland channels.
        if (water.kind == YQHydrologyKindV2.Wetland &&
            foamMesh != null &&
            rendererComponent.sharedMaterials != null &&
            rendererComponent.sharedMaterials.Length > 0 &&
            rendererComponent.sharedMaterials[0] != null)
        {
            // note: Bank foam is a narrow child strip over the accepted water edge; it has no collider and cannot interrupt the river's physical flow.
            GameObject foamSurface = new GameObject("CompiledHydrologyBankFoam");
            foamSurface.transform.SetParent(instance.transform, false);
            MeshFilter foamFilter = foamSurface.AddComponent<MeshFilter>();
            MeshRenderer foamRenderer = foamSurface.AddComponent<MeshRenderer>();
            foamFilter.sharedMesh = foamMesh;
            Material foamMaterial = new Material(rendererComponent.sharedMaterials[0])
            {
                name = rendererComponent.sharedMaterials[0].name + "_BankFoam"
            };
            if (foamMaterial.HasProperty("_FoamOnly"))
                foamMaterial.SetFloat("_FoamOnly", 1f);
            if (foamMaterial.HasProperty("_FoamOpacity"))
                foamMaterial.SetFloat("_FoamOpacity", water.kind == YQHydrologyKindV2.Waterfall ? .95f : .90f);
            foamRenderer.sharedMaterial = foamMaterial;
            // note: Impact foam draws after both connected water bodies so the confluence remains visibly joined even when the lake is transparent.
            foamRenderer.sortingOrder = 10;
            foamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foamRenderer.receiveShadows = false;
            foamRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            foamRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            foamRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
        if (water.kind == YQHydrologyKindV2.River || water.kind == YQHydrologyKindV2.Wetland)
            // note: Fish are children of the accepted water instance, so rebuilds destroy them with the owning hydrology surface.
            YQGeneratedFish.SpawnForRiver(instance.transform, sampledPoints, sampledWidths, water.kind, water.hydrologyId);
        // note: Imported water prefabs may carry an inactive presentation root; the accepted generated surface must be explicitly active after its mesh and material are published.
        instance.SetActive(true);
        // note: The carved Terrain is the visible and collidable bed/bank; duplicate ribbons hid depth and produced long soil smears.
        // note: Rivers, coastlines, and waterfalls publish directly from accepted control points, including downhill surface elevations and per-point widths.
        return true;
    }

    private static void ExtendPreparedWaterIntoReceivingBody(
        Terrain terrain,
        YQPreparedSpatialMaterializationV2 prepared,
        YQSpatialMaterializationWaterV2 water,
        List<Vector3> points,
        List<float> widths)
    {
        if (terrain == null || prepared == null || points == null || widths == null ||
            points.Count < 2 || points.Count != widths.Count ||
            string.IsNullOrWhiteSpace(water.sinkHydrologyId))
            return;

        int sinkIndex = -1;
        YQSpatialMaterializationWaterV2 sink = default;
        for (int index = 0; index < prepared.WaterCount; index++)
        {
            YQSpatialMaterializationWaterV2 candidate = prepared.GetWater(index);
            if (!string.Equals(candidate.hydrologyId, water.sinkHydrologyId, StringComparison.OrdinalIgnoreCase))
                continue;
            sinkIndex = index;
            sink = candidate;
            break;
        }

        if (sinkIndex < 0)
            return;

        bool sinkIsArea = sink.kind == YQHydrologyKindV2.Lake || sink.kind == YQHydrologyKindV2.Wetland;
        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin = default;
        if (sinkIsArea && !TryBuildPreparedAreaWaterBasin(terrain, prepared, sinkIndex, out basin))
            return;

        Vector2 sinkTarget;
        float sinkTargetY;
        float sinkWidth;
        Vector2 endpointXZ = new Vector2(
            points[points.Count - 1].x,
            points[points.Count - 1].z);
        if (sinkIsArea)
        {
            Vector2 basinCenter = new Vector2(basin.CenterWorld.x, basin.CenterWorld.z);
            Vector2 firstXZ = new Vector2(points[0].x, points[0].z);
            Vector2 lastXZ = new Vector2(points[points.Count - 1].x, points[points.Count - 1].z);
            endpointXZ = Vector2.Distance(firstXZ, basinCenter) < Vector2.Distance(lastXZ, basinCenter)
                ? firstXZ
                : lastXZ;
            Vector2 endpointDirection = endpointXZ - basinCenter;
            // note: Compiler-authored rivers already end inside their receiving basin. Extending those endpoints back toward the bank creates a reversed, overlapping water strip.
            float endpointAlong = Vector2.Dot(endpointDirection, basin.LongAxisXZ) / Mathf.Max(1f, basin.LongRadius * .94f);
            float endpointAcross = Vector2.Dot(endpointDirection, basin.ShortAxisXZ) / Mathf.Max(1f, basin.ShortRadius * .94f);
            if (endpointAlong * endpointAlong + endpointAcross * endpointAcross < 1f)
                return;
            if (endpointDirection.sqrMagnitude < 0.0001f)
                endpointDirection = basin.LongAxisXZ;
            endpointDirection.Normalize();
            float ellipseDenominator = Mathf.Sqrt(
                // note: Resolve the shoreline in the basin's local axes; world X/Z gives the wrong inlet for rotated, elongated lakes.
                Mathf.Pow(Vector2.Dot(endpointDirection, basin.LongAxisXZ) / Mathf.Max(1f, basin.LongRadius), 2f) +
                Mathf.Pow(Vector2.Dot(endpointDirection, basin.ShortAxisXZ) / Mathf.Max(1f, basin.ShortRadius), 2f));
            float shorelineRadius = 1f / Mathf.Max(0.001f, ellipseDenominator);
            // note: End the mouth inside the receiving ellipse; the prior positive overlap left the river on the dry side of the shoreline and created the raised wedge seen in runtime captures.
            // note: Carry the incoming ribbon well past the rendered shoreline inset so its tapered mouth always overlaps the receiving surface instead of ending in a dry wedge.
            float inletOverlap = Mathf.Clamp(
                Mathf.Max(5f, shorelineRadius * .32f),
                3.5f,
                Mathf.Max(3.5f, shorelineRadius * .55f));
            sinkTarget = basinCenter + endpointDirection * Mathf.Max(0f, shorelineRadius - inletOverlap);
            sinkTargetY = basin.WaterSurfaceY;
            // note: A receiving lake broadens the current only modestly; using most of the basin radius produced detached triangular sheets at the mouth.
            // note: A modestly broader terminal fan lets the flow dissipate into the basin while keeping the lake responsible for the broad interior surface.
            sinkWidth = Mathf.Min(basin.ShortRadius * .62f, Mathf.Max(4f, widths[0] * 2.0f));
        }
        else
        {
            int sinkPointCount = prepared.GetWaterPointCount(sinkIndex);
            if (sinkPointCount < 2)
                return;
            sinkTarget = Vector2.zero;
            sinkTargetY = 0f;
            sinkWidth = Mathf.Max(4f, sink.nominalWidth);
            float bestDistance = float.MaxValue;
            for (int sinkPointIndex = 0; sinkPointIndex < sinkPointCount; sinkPointIndex++)
            {
                YQSpatialMaterializationWaterPointV2 sinkPoint = prepared.GetWaterPoint(sinkIndex, sinkPointIndex);
                Vector2 sinkXZ = new Vector2(sinkPoint.x, sinkPoint.z);
                float firstCandidate = Vector2.Distance(new Vector2(points[0].x, points[0].z), sinkXZ);
                float lastCandidate = Vector2.Distance(new Vector2(points[points.Count - 1].x, points[points.Count - 1].z), sinkXZ);
                float candidate = Mathf.Min(firstCandidate, lastCandidate);
                if (candidate >= bestDistance)
                    continue;
                bestDistance = candidate;
                sinkTarget = sinkXZ;
                sinkTargetY = terrain.transform.position.y + sinkPoint.waterSurfaceNormalized * terrain.terrainData.size.y;
            }
        }

        float firstDistance = Vector2.Distance(new Vector2(points[0].x, points[0].z), sinkTarget);
        float lastDistance = Vector2.Distance(new Vector2(points[points.Count - 1].x, points[points.Count - 1].z), sinkTarget);
        bool sinkAtStart = firstDistance < lastDistance;
        int endpoint = sinkAtStart ? 0 : points.Count - 1;
        endpointXZ = new Vector2(points[endpoint].x, points[endpoint].z);
        if (Vector2.Distance(endpointXZ, sinkTarget) < 1f)
            return;

        int mouthSteps = sinkIsArea ? 10 : 5;
        float baseWidth = widths[endpoint];
        // note: Confluence width grows from the authored river width rather than opening into a second full water body.
        // note: The mouth expands before it fades so the confluence reads as a natural dissipation rather than a narrow strip terminating against the lake rim.
        float mouthWidth = Mathf.Min(
            Mathf.Max(baseWidth * (sinkIsArea ? 1.6f : 1.45f), baseWidth + 1.0f),
            Mathf.Max(baseWidth, sinkWidth));
        var mouthPoints = new List<Vector3>(mouthSteps);
        var mouthWidths = new List<float>(mouthSteps);
        for (int step = 1; step <= mouthSteps; step++)
        {
            float t = step / (float)mouthSteps;
            Vector2 xz = Vector2.Lerp(endpointXZ, sinkTarget, t);
            Vector2 mouthDirection = sinkTarget - endpointXZ;
            Vector2 mouthSide = mouthDirection.sqrMagnitude > .001f
                ? new Vector2(-mouthDirection.y, mouthDirection.x).normalized
                : Vector2.right;
            // note: Keep the confluence bend shallow; a large lateral sine offset creates a triangular overlay instead of a natural mouth.
            float curve = Mathf.Clamp(mouthDirection.magnitude * .03f, .5f, 4f);
            xz += mouthSide * Mathf.Sin(t * Mathf.PI) * curve;
            float y = Mathf.Lerp(points[endpoint].y, sinkTargetY, t) + .065f;
            mouthPoints.Add(new Vector3(xz.x, y, xz.y));
            mouthWidths.Add(Mathf.Lerp(baseWidth, mouthWidth, t * t));
        }

        if (sinkAtStart)
        {
            mouthPoints.Reverse();
            mouthWidths.Reverse();
            points.InsertRange(0, mouthPoints);
            widths.InsertRange(0, mouthWidths);
        }
        else
        {
            points.AddRange(mouthPoints);
            widths.AddRange(mouthWidths);
        }
    }

    private static Mesh BuildPreparedWaterBankFoamMesh(
        List<Vector3> points,
        List<float> widths,
        int waterIndex,
        YQHydrologyKindV2 kind,
        YQSpatialBlueprintTerrainSamplerV2 waterSampler,
        Terrain terrain)
    {
        if (points == null || widths == null ||
            points.Count < 2 || widths.Count != points.Count ||
            waterSampler == null || terrain == null || terrain.terrainData == null)
            return null;

        int pointCount = points.Count;
        Vector3[] vertices = new Vector3[pointCount * 4];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colours = new Color[vertices.Length];
        int[] triangles = new int[(pointCount - 1) * 12];
        float travel = 0f;

        for (int index = 0; index < pointCount; index++)
        {
            Vector3 tangent = index == 0
                ? points[1] - points[0]
                : index == pointCount - 1
                    ? points[index] - points[index - 1]
                    : points[index + 1] - points[index - 1];
            tangent.y = 0f;
            if (tangent.sqrMagnitude < .0001f)
                tangent = Vector3.forward;
            tangent.Normalize();
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x);
            float halfWidth = Mathf.Max(.75f, widths[index] * .5f);
            // note: Waterfall whitewater stays a narrow impact fringe while rivers keep a readable bank shelf; broad waterfall strips created triangular blue seams over the carved bed.
            float foamWidth = kind == YQHydrologyKindV2.Waterfall
                ? Mathf.Clamp(halfWidth * .12f, .18f, .5f)
                : Mathf.Clamp(halfWidth * .14f, .18f, .65f);
            Vector3 previousTangent = index == 0 ? tangent : points[index] - points[index - 1];
            Vector3 nextTangent = index == pointCount - 1 ? tangent : points[index + 1] - points[index];
            previousTangent.y = nextTangent.y = 0f;
            previousTangent.Normalize();
            nextTangent.Normalize();
            Vector3 curvature = nextTangent - previousTangent;
            float leftImpact = Mathf.Clamp01(Vector3.Dot(curvature, -side) * 2.5f);
            float rightImpact = Mathf.Clamp01(Vector3.Dot(curvature, side) * 2.5f);
            float finalWaterY = points[index].y;
            YQSpatialTerrainSampleV2 sample = waterSampler.Sample(points[index].x, points[index].z);
            if (sample.waterFeatureIndex >= 0)
            {
                finalWaterY = terrain.transform.position.y +
                    sample.waterSurfaceNormalized * terrain.terrainData.size.y + .058f;
            }
            // note: A mouth sample owned by the receiving body gets a localized impact surge, while ordinary banks keep only the low contact foam.
            if (sample.waterFeatureIndex >= 0 && sample.waterFeatureIndex != waterIndex)
                leftImpact = rightImpact = 1f;

            Vector3 leftOuter = points[index] - side * halfWidth;
            Vector3 leftInner = points[index] - side * Mathf.Max(.05f, halfWidth - foamWidth);
            Vector3 rightInner = points[index] + side * Mathf.Max(.05f, halfWidth - foamWidth);
            Vector3 rightOuter = points[index] + side * halfWidth;
            leftOuter.y = leftInner.y = rightInner.y = rightOuter.y = finalWaterY;
            int vertexIndex = index * 4;
            vertices[vertexIndex] = leftOuter;
            vertices[vertexIndex + 1] = leftInner;
            vertices[vertexIndex + 2] = rightInner;
            vertices[vertexIndex + 3] = rightOuter;
            for (int vertex = 0; vertex < 4; vertex++)
            {
                normals[vertexIndex + vertex] = Vector3.up;
                // note: Foam disappears with the river inside receiving water instead of leaving two white rails across the lake.
                // note: Store the bank-side coordinate in green so the shared shader can feather foam at the actual shoreline instead of using world UVs.
                float foamAcross = vertex == 0 ? 0f : vertex == 1 ? .18f : vertex == 2 ? .82f : 1f;
                colours[vertexIndex + vertex] = new Color(1f, foamAcross, 0f, waterSampler.SampleReceivingWaterFade(points[index].x, points[index].z));
            }
            colours[vertexIndex].r = colours[vertexIndex + 1].r = leftImpact;
            colours[vertexIndex + 2].r = colours[vertexIndex + 3].r = rightImpact;
            // note: Standalone foam is reserved for bank impacts and confluences; ordinary banks use depth-aware shader contact instead of a continuous opaque rail.
            // note: Ignore low-curvature samples so foam remains a short whitewater impact instead of a faceted translucent shelf.
            float rawImpactCoverage = Mathf.Clamp01(Mathf.Max(leftImpact, rightImpact));
            float impactCoverage = Mathf.SmoothStep(.55f, .90f, rawImpactCoverage);
            for (int vertex = 0; vertex < 4; vertex++)
                colours[vertexIndex + vertex].a = impactCoverage;

            if (index > 0)
                travel += Vector3.Distance(points[index - 1], points[index]);
            float v = travel / Mathf.Max(2f, widths[index]);
            uvs[vertexIndex] = new Vector2(0f, v);
            uvs[vertexIndex + 1] = new Vector2(1f, v);
            uvs[vertexIndex + 2] = new Vector2(0f, v);
            uvs[vertexIndex + 3] = new Vector2(1f, v);
        }

        for (int index = 0; index < pointCount - 1; index++)
        {
            int current = index * 4;
            int next = (index + 1) * 4;
            int triangle = index * 12;
            // note: Two independent strips keep left and right bank foam continuous through bends without closing the river across its centre.
            triangles[triangle] = current + 1;
            triangles[triangle + 1] = next + 1;
            triangles[triangle + 2] = current;
            triangles[triangle + 3] = current;
            triangles[triangle + 4] = next + 1;
            triangles[triangle + 5] = next;
            triangles[triangle + 6] = current + 2;
            triangles[triangle + 7] = current + 3;
            triangles[triangle + 8] = next + 2;
            triangles[triangle + 9] = current + 3;
            triangles[triangle + 10] = next + 3;
            triangles[triangle + 11] = next + 2;
        }

        Mesh mesh = new Mesh
        {
            name = "YQ_V2WaterBankFoam_" + waterIndex + "_" + kind
        };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.colors = colours;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        GeneratedMacroWaterMeshes.Add(mesh);
        return mesh;
    }

    private static bool TryBuildPreparedAreaWaterBasin(
        Terrain terrain,
        YQPreparedSpatialMaterializationV2 prepared,
        int waterIndex,
        out YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin)
    {
        basin = default;
        int pointCount = prepared.GetWaterPointCount(waterIndex);
        if (pointCount == 0)
            return false;

        YQSpatialMaterializationWaterV2 water =
            prepared.GetWater(waterIndex);
        Vector2 center = Vector2.zero;
        for (int index = 0; index < pointCount; index++)
        {
            YQSpatialMaterializationWaterPointV2 point =
                prepared.GetWaterPoint(waterIndex, index);
            center += new Vector2(point.x, point.z);
        }
        center /= pointCount;

        YQSpatialMaterializationWaterPointV2 first =
            prepared.GetWaterPoint(waterIndex, 0);
        YQSpatialMaterializationWaterPointV2 last =
            prepared.GetWaterPoint(waterIndex, pointCount - 1);
        Vector2 longAxis =
            new Vector2(last.x - first.x, last.z - first.z);
        if (longAxis.sqrMagnitude < 0.0001f)
            longAxis = Vector2.right;
        longAxis.Normalize();
        Vector2 shortAxis = new Vector2(-longAxis.y, longAxis.x);
        float longRadius = Mathf.Max(6f, water.nominalWidth * 0.5f);
        float shortRadius = Mathf.Max(5f, water.nominalWidth * 0.36f);

        for (int index = 0; index < pointCount; index++)
        {
            YQSpatialMaterializationWaterPointV2 point =
                prepared.GetWaterPoint(waterIndex, index);
            Vector2 offset = new Vector2(point.x, point.z) - center;
            // note: Use the same nominal/point-width contract as the terrain sampler so surface and carved basin share one footprint.
            float pointRadius = Mathf.Max(1f, Mathf.Max(water.nominalWidth, point.width) * 0.5f);
            longRadius = Mathf.Max(
                longRadius,
                Mathf.Abs(Vector2.Dot(offset, longAxis)) + pointRadius);
            shortRadius = Mathf.Max(
                shortRadius,
                Mathf.Abs(Vector2.Dot(offset, shortAxis)) + pointRadius);
        }

        float normalizedSurface = water.waterLevelNormalized > 0f
            ? water.waterLevelNormalized
            : first.waterSurfaceNormalized;
        float waterY =
            terrain.transform.position.y +
            terrain.terrainData.size.y *
                Mathf.Clamp01(normalizedSurface) +
            0.045f;
        basin = new YQGeneratedWorldTerrain.MacroWaterBasinDescriptor(
            new Vector3(center.x, waterY, center.y),
            longAxis,
            shortAxis,
            longRadius,
            shortRadius,
            waterY);

        // note: Area water derives its fitted ellipse from all accepted control points rather than stretching an arbitrary square prefab.
        return true;
    }

    private static Mesh BuildPreparedWaterRibbonMesh(
        List<Vector3> points,
        List<float> widths,
        int waterIndex,
        YQHydrologyKindV2 kind,
        YQSpatialBlueprintTerrainSamplerV2 waterSampler,
        Terrain terrain,
        List<Vector3> edgeOffsets)
    {
        if (points == null || widths == null ||
            points.Count < 2 || widths.Count != points.Count)
        {
            return null;
        }

        int segmentCount = points.Count - 1;
        Vector3[] vertices = new Vector3[points.Count * 2];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        // note: One front-facing quad per river segment avoids coplanar duplicate triangles that caused hard seams and texture flicker at water joins.
        int[] triangles = new int[segmentCount * 6];
        var colours = new Color[vertices.Length];
        var owned = new bool[points.Count];
        float travel = 0f;

        for (int index = 0; index < points.Count; index++)
        {
            Vector3 tangent = index == 0
                ? points[1] - points[0]
                : index == points.Count - 1
                    ? points[index] - points[index - 1]
                    : points[index + 1] - points[index - 1];
            Vector3 horizontal =
                new Vector3(tangent.x, 0f, tangent.z);
            if (horizontal.sqrMagnitude < 0.0001f)
                horizontal = Vector3.forward;
            horizontal.Normalize();
            Vector3 side =
                new Vector3(-horizontal.z, 0f, horizontal.x);
            // note: Cross the lateral bank axis with the downstream direction to obtain an upward-facing surface normal.
            Vector3 normal = Vector3.Cross(
                side,
                tangent.sqrMagnitude > 0.0001f ? tangent.normalized : horizontal).normalized;
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.up;
            float halfWidth = Mathf.Max(0.75f, widths[index] * 0.5f);
            // note: Keep the visible ribbon nearly coincident with the authored channel; a broad overhang projected translucent wedges onto the bank in the rasterized shoreline.
            // note: Waterfalls stay inside the carved channel, while the small river cover only closes sub-cell gaps without widening the bank visually.
            float renderHalfWidth = halfWidth +
                (kind == YQHydrologyKindV2.River ? .15f :
                    kind == YQHydrologyKindV2.Waterfall ? -.35f : .25f);
            // note: Build the bank frame from the actual resampled tangent; stale control-point offsets caused faceted sawtooth edges at bends.
            Vector3 bankOffset = side * renderHalfWidth;
            // note: Waterfall banks stay on their sampled channel frame; corner miters can project translucent triangles beyond the carved fall while river bends still benefit from a capped miter.
            if (kind != YQHydrologyKindV2.Waterfall && index > 0 && index < points.Count - 1)
            {
                Vector3 previousTangent = points[index] - points[index - 1];
                Vector3 nextTangent = points[index + 1] - points[index];
                previousTangent.y = 0f;
                nextTangent.y = 0f;
                if (previousTangent.sqrMagnitude > .0001f && nextTangent.sqrMagnitude > .0001f)
                {
                    previousTangent.Normalize();
                    nextTangent.Normalize();
                    Vector3 previousSide = new Vector3(-previousTangent.z, 0f, previousTangent.x);
                    Vector3 nextSide = new Vector3(-nextTangent.z, 0f, nextTangent.x);
                    Vector3 miter = previousSide + nextSide;
                    if (miter.sqrMagnitude > .0001f)
                    {
                        miter.Normalize();
                        float denominator = Mathf.Abs(Vector3.Dot(miter, nextSide));
                        float miterLength = renderHalfWidth / Mathf.Max(.45f, denominator);
                        // note: Limit bend miters to a modest multiple of the local bank width so sharp samples cannot create triangular shoreline spikes.
                        bankOffset = miter * Mathf.Min(renderHalfWidth * 1.12f, miterLength);
                    }
                }
            }
            int vertexIndex = index * 2;
            // note: Pull each visible bank edge toward the centerline until the authoritative sampler confirms wet coverage; this removes transparent triangles over dry ground without changing the accepted channel path.
            Vector3 leftEdge = ConstrainWaterRibbonEdge(
                points[index],
                points[index] - bankOffset,
                waterIndex,
                waterSampler);
            Vector3 rightEdge = ConstrainWaterRibbonEdge(
                points[index],
                points[index] + bankOffset,
                waterIndex,
                waterSampler);
            // note: Re-symmetrize independently constrained banks so a sharp bend cannot connect a narrow edge to a wide edge with a dry triangular span.
            float leftSpan = Mathf.Max(0f, Vector3.Dot(points[index] - leftEdge, side));
            float rightSpan = Mathf.Max(0f, Vector3.Dot(rightEdge - points[index], side));
            float safeSpan = Mathf.Min(leftSpan, rightSpan);
            if (safeSpan > .05f)
            {
                leftEdge = points[index] - side * safeSpan;
                rightEdge = points[index] + side * safeSpan;
            }
            else
            {
                float minimumSpan = Mathf.Max(.18f, renderHalfWidth * .12f);
                leftEdge = points[index] - side * minimumSpan;
                rightEdge = points[index] + side * minimumSpan;
            }
            vertices[vertexIndex] = leftEdge;
            vertices[vertexIndex + 1] = rightEdge;
            // note: Preserve the accepted hydraulic datum above the carved bed rather than flattening water onto the ground.
            var centerSample = waterSampler.Sample(points[index].x, points[index].z);
            float finalWaterY = centerSample.waterFeatureIndex >= 0
                ? terrain.transform.position.y + centerSample.waterSurfaceNormalized * terrain.terrainData.size.y + .045f
                : points[index].y;
            vertices[vertexIndex].y = finalWaterY;
            vertices[vertexIndex + 1].y = finalWaterY;
            // note: The receiving body owns overlapping water; a short upstream foam band marks the confluence without double-blended sheets.
            owned[index] = centerSample.waterFeatureIndex == waterIndex;
            float foam = 0f;
            for (int neighbour = Mathf.Max(0,index-10); neighbour <= Mathf.Min(points.Count-1,index+10); neighbour += 5)
                if (waterSampler.Sample(points[neighbour].x,points[neighbour].z).waterFeatureIndex != waterIndex)
                    foam = Mathf.Max(foam,1f-Mathf.Abs(neighbour-index)/11f);
            // note: A continuous twelve-metre inlet fade removes the rectangular overlay while the lake underneath supplies the receiving surface.
            float receivingFade = waterSampler.SampleReceivingWaterFade(points[index].x, points[index].z);
            // note: Turn the receiving-body fade into a narrow interface band; a full interior foam value made mouths look like white decals laid across the lake.
            float receivingImpact = Mathf.Clamp01(receivingFade) * (1f - Mathf.Clamp01(receivingFade)) * 4f;
            foam = Mathf.Max(foam, receivingImpact);
            // note: Keep waterfall ribbons fully covered; applying the receiving-body fade across a sloped fall exposed the carved terrain as pale triangular wedges along its bank.
            float explicitCoverage = kind == YQHydrologyKindV2.Waterfall
                ? 1f
                // note: Keep only a soft river core through the receiving basin; the previous high floor made the mouth read as a hard opaque strip.
                : Mathf.Lerp(.32f, 1f, receivingFade);
            // note: Preserve the across-channel coordinate in green for a stable edge fade even when the material uses world-space ripple UVs.
            colours[vertexIndex] = new Color(foam, 0f, receivingFade, explicitCoverage);
            colours[vertexIndex + 1] = new Color(foam, 1f, receivingFade, explicitCoverage);
            normals[vertexIndex] = normal;
            normals[vertexIndex + 1] = normal;
            if (index > 0)
                travel += Vector3.Distance(points[index - 1], points[index]);
            float v = travel / Mathf.Max(2f, widths[index]);
            uvs[vertexIndex] = new Vector2(0f, v);
            uvs[vertexIndex + 1] = new Vector2(1f, v);
        }

        // note: Recalculate slope from the final water elevations, including the levelled lake mouth; lighting and GPU flow must agree with the visible mesh.
        for (int index = 0; index < points.Count; index++)
        {
            int previous = Mathf.Max(0, index - 1) * 2;
            int next = Mathf.Min(points.Count - 1, index + 1) * 2;
            Vector3 downstream = (vertices[next] + vertices[next + 1] - vertices[previous] - vertices[previous + 1]) * .5f;
            Vector3 lateral = vertices[index * 2 + 1] - vertices[index * 2];
            Vector3 normal = Vector3.Cross(lateral, downstream).normalized;
            if (normal.sqrMagnitude < .0001f) normal = Vector3.up;
            if (normal.y < 0f) normal = -normal;
            normals[index * 2] = normals[index * 2 + 1] = normal;
        }

        for (int index = 0; index < segmentCount; index++)
        {
            int vertexIndex = index * 2;
            int triangleIndex = index * 6;
            // note: The bank frame's accepted upward normal uses the reverse winding; retaining only this front face removes z-fighting without backface-culling the river.
            triangles[triangleIndex] = vertexIndex + 1;
            triangles[triangleIndex + 1] = vertexIndex + 2;
            triangles[triangleIndex + 2] = vertexIndex;
            triangles[triangleIndex + 3] = vertexIndex + 3;
            triangles[triangleIndex + 4] = vertexIndex + 2;
            triangles[triangleIndex + 5] = vertexIndex + 1;
            // note: Keep every connected segment so a lake-owned mouth sample cannot remove a visible river span.
        }

        Mesh mesh = new Mesh
        {
            name = "YQ_V2Water_" + waterIndex + "_" + kind
        };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.colors = colours;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        GeneratedMacroWaterMeshes.Add(mesh);
        return mesh;
    }

    private static Vector3 ConstrainWaterRibbonEdge(
        Vector3 center,
        Vector3 edge,
        int waterIndex,
        YQSpatialBlueprintTerrainSamplerV2 waterSampler)
    {
        // note: A short deterministic binary-like pull keeps bridge mouths and receiving bodies continuous while rejecting only dry-bank overhang.
        Vector3 candidate = edge;
        float interpolation = 1f;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            YQSpatialTerrainSampleV2 sample = waterSampler.Sample(candidate.x, candidate.z);
            bool belongsToChannel = sample.waterFeatureIndex == waterIndex;
            bool belongsToReceivingBody = false;
            if (!belongsToChannel && sample.waterFeatureIndex >= 0 && sample.waterMask >= .35f)
            {
                // note: Accept the receiving body's supported shoreline band, including its fade edge, so confluence banks do not collapse into a dry notch at the exact lake rim.
                belongsToReceivingBody = true;
            }
            if (sample.waterMask >= .58f && (belongsToChannel || belongsToReceivingBody))
                return candidate;

            interpolation *= .72f;
            candidate = Vector3.Lerp(center, edge, interpolation);
        }
        // note: If the sampler cannot confirm wet coverage after the bounded pull, finish near the centerline instead of publishing a dry-bank triangle.
        return Vector3.Lerp(center, edge, .12f);
    }

    private static List<Vector3> BuildDrainageStreamPoints(
        Terrain terrain,
        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin,
        int basinIndex,
        int branchIndex)
    {
        List<Vector3> points =
            new List<Vector3>(28);

        if (terrain == null ||
            terrain.terrainData == null)
        {
            return points;
        }

        Vector2 axis =
            basin.LongAxisXZ.normalized;
        Vector2 normal =
            basin.ShortAxisXZ.normalized;
        float direction =
            branchIndex == 0
                ? -1f
                : 1f;
        Vector2 start =
            new Vector2(
                basin.CenterWorld.x,
                basin.CenterWorld.z) +
            axis *
                direction *
                basin.LongRadius *
                0.58f +
            normal *
                Mathf.Lerp(
                    -basin.ShortRadius *
                        0.22f,
                    basin.ShortRadius *
                        0.22f,
                    Deterministic01(
                        basinIndex +
                        "|" +
                        branchIndex +
                        "|stream_offset"));

        float length =
            Mathf.Lerp(
                108f,
                176f,
                Deterministic01(
                    basinIndex +
                    "|" +
                    branchIndex +
                    "|stream_length"));
        int sampleCount =
            24;
        float phase =
            Deterministic01(
                basinIndex +
                "|" +
                branchIndex +
                "|stream_phase") *
            Mathf.PI *
            2f;
        float meander =
            Mathf.Lerp(
                4f,
                13f,
                Deterministic01(
                    basinIndex +
                    "|" +
                    branchIndex +
                    "|stream_meander"));

        for (int index = 0;
             index < sampleCount;
             index++)
        {
            float t =
                index /
                (float)(sampleCount - 1);
            Vector2 horizontal =
                start +
                axis *
                    direction *
                    length *
                    t +
                normal *
                    Mathf.Sin(
                        t *
                        Mathf.PI *
                        2f +
                        phase) *
                    meander *
                    Mathf.Sin(
                        t *
                        Mathf.PI);
            Vector3 point =
                new Vector3(
                    horizontal.x,
                    0f,
                    horizontal.y);

            if (!InsideTerrainWithMargin(
                    terrain,
                    point,
                    2f))
            {
                break;
            }

            point.y =
                YQGeneratedWorldTerrain.SampleWorldHeight(
                    terrain,
                    point) +
                0.045f;
            points.Add(point);
        }

        return points;
    }

    private static bool TryConfigureDrainageWaterSurface(
        GameObject instance,
        List<Vector3> points,
        int basinIndex,
        int branchIndex)
    {
        if (instance == null ||
            points == null ||
            points.Count < 2)
        {
            return false;
        }

        Renderer[] importedRenderers =
            instance.GetComponentsInChildren<Renderer>(true);
        Renderer materialSource =
            null;

        for (int index = 0;
             index < importedRenderers.Length;
             index++)
        {
            Renderer renderer =
                importedRenderers[index];

            if (renderer == null)
                continue;

            if (materialSource == null &&
                renderer.sharedMaterials != null &&
                renderer.sharedMaterials.Length > 0 &&
                renderer.sharedMaterials[0] != null)
            {
                materialSource = renderer;
            }

            renderer.enabled =
                false;
        }

        if (materialSource == null)
            return false;

        Mesh mesh =
            BuildDrainageWaterMesh(
                points,
                basinIndex,
                branchIndex);
        if (mesh == null)
            return false;

        GameObject surface =
            new GameObject(
                "GroundedDrainageWaterSurface");
        surface.transform.SetParent(
            instance.transform,
            false);

        MeshFilter filter =
            surface.AddComponent<MeshFilter>();
        MeshRenderer rendererComponent =
            surface.AddComponent<MeshRenderer>();
        filter.sharedMesh =
            mesh;
        rendererComponent.sharedMaterials =
            materialSource.sharedMaterials;
        ConfigureGeneratedWaterMaterial(rendererComponent, YQHydrologyKindV2.River);
        rendererComponent.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        rendererComponent.receiveShadows =
            false;
        rendererComponent.lightProbeUsage =
            UnityEngine.Rendering.LightProbeUsage.Off;
        rendererComponent.reflectionProbeUsage =
            UnityEngine.Rendering.ReflectionProbeUsage.Off;
        // note: Drainage ribbons use the same fast flow profile as the primary river while remaining separate generated meshes.
        surface.AddComponent<YQWaterSurfaceMotion>().Configure(filter, rendererComponent, YQHydrologyKindV2.River);

        return true;
    }

    private static Mesh BuildDrainageWaterMesh(
        List<Vector3> points,
        int basinIndex,
        int branchIndex)
    {
        int segmentCount =
            points.Count -
            1;
        Vector3[] vertices =
            new Vector3[segmentCount * 2 + 2];
        Vector3[] normals =
            new Vector3[vertices.Length];
        Vector2[] uvs =
            new Vector2[vertices.Length];
        int[] triangles =
            new int[segmentCount * 6];

        for (int index = 0;
             index <= segmentCount;
             index++)
        {
            Vector3 point =
                points[index];
            Vector3 tangent =
                index == 0
                    ? points[1] - points[0]
                    : index == segmentCount
                        ? points[index] - points[index - 1]
                        : points[index + 1] - points[index - 1];
            tangent.y =
                0f;
            tangent.Normalize();
            Vector3 side =
                new Vector3(
                    -tangent.z,
                    0f,
                    tangent.x);
            float width =
                Mathf.Lerp(
                    2.2f,
                    4.6f,
                    Mathf.Sin(
                        index /
                        (float)segmentCount *
                        Mathf.PI));
            int vertexIndex =
                index *
                2;
            vertices[vertexIndex] =
                point -
                side *
                width;
            vertices[vertexIndex + 1] =
                point +
                side *
                width;
            normals[vertexIndex] =
                Vector3.up;
            normals[vertexIndex + 1] =
                Vector3.up;
            uvs[vertexIndex] =
                new Vector2(
                    0f,
                    index /
                    (float)segmentCount *
                    2.4f);
            uvs[vertexIndex + 1] =
                new Vector2(
                    1f,
                    index /
                    (float)segmentCount *
                    2.4f);
        }

        for (int index = 0;
             index < segmentCount;
             index++)
        {
            int vertexIndex =
                index *
                2;
            int triangleIndex =
                index *
                6;
            triangles[triangleIndex] =
                vertexIndex;
            triangles[triangleIndex + 1] =
                vertexIndex +
                2;
            triangles[triangleIndex + 2] =
                vertexIndex +
                1;
            triangles[triangleIndex + 3] =
                vertexIndex +
                1;
            triangles[triangleIndex + 4] =
                vertexIndex +
                2;
            triangles[triangleIndex + 5] =
                vertexIndex +
                3;
        }

        Mesh mesh =
            new Mesh
            {
                name =
                    "YQ_DrainageWater_" +
                    basinIndex +
                    "_" +
                    branchIndex
            };
        mesh.vertices =
            vertices;
        mesh.normals =
            normals;
        mesh.uv =
            uvs;
        mesh.triangles =
            triangles;
        mesh.RecalculateBounds();
        GeneratedMacroWaterMeshes.Add(mesh);
        return mesh;
    }

    private static bool TryConfigureMacroWaterSurface(
        GameObject instance,
        Terrain terrain,
        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin,
        int basinIndex,
        YQHydrologyKindV2 waterKind)
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
                terrain,
                basin,
                basinIndex);
        rendererComponent.sharedMaterials =
            materialSource.sharedMaterials;
        ConfigureGeneratedWaterMaterial(rendererComponent, waterKind);
        // note: Basin surfaces stay behind connected channels so the incoming water can feather over the lake mouth.
        rendererComponent.sortingOrder =
            waterKind == YQHydrologyKindV2.Lake || waterKind == YQHydrologyKindV2.Wetland ? 0 : 2;
        rendererComponent.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        rendererComponent.receiveShadows = false;
        rendererComponent.lightProbeUsage =
            UnityEngine.Rendering.LightProbeUsage.Off;
        rendererComponent.reflectionProbeUsage =
            UnityEngine.Rendering.ReflectionProbeUsage.Off;
        rendererComponent.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;

        // note: Lakes retain their approved material and receive a slower surface drift than flowing rivers.
        if (filter.sharedMesh != null) surface.AddComponent<YQWaterSurfaceMotion>().Configure(filter, rendererComponent, waterKind);
        // note: The approved imported water material is retained, while a lightweight ellipse conforms it to the deterministic basin instead of stretching a square plane across dry ground.
        return filter.sharedMesh != null;
    }

    internal static Texture ResolveWaterRippleNormal(Material source)
    {
        // note: The approved TownSmith water stores its moving ripple normal in _RefractionNormal; retain the imported texture when converting either origin or streamed surfaces.
        if (source == null) return null;
        foreach (string property in new[] { "_BumpMap", "_NormalMap", "_RefractionNormal" })
            if (source.HasProperty(property) && source.GetTexture(property) != null)
                return source.GetTexture(property);
        return null;
    }

    private static void ConfigureGeneratedWaterMaterial(
        MeshRenderer renderer,
        YQHydrologyKindV2 waterKind,
        float flowScale = 1f)
    {
        if (renderer == null || renderer.sharedMaterials == null)
            return;

        Material[] materials = renderer.sharedMaterials;
        Shader generatedWaterShader = Shader.Find("YourQuest/Generated Water");
        for (int index = 0; index < materials.Length; index++)
        {
            Material source = materials[index];
            if (source == null)
                continue;
            Material waterMaterial = new Material(source)
            {
                name = source.name + "_GeneratedWater",
                hideFlags = HideFlags.DontSave
            };
            // note: Use the project-owned water shader so HDRP source materials retain their ripple normal, depth foam, and blue high-fidelity presentation in URP.
            if (generatedWaterShader != null)
            {
                Texture bump = ResolveWaterRippleNormal(source);
                waterMaterial.shader = generatedWaterShader;
                if (bump != null)
                    waterMaterial.SetTexture("_BumpMap", bump);
                waterMaterial.SetColor("_ShallowColor", new Color(.055f, .28f, .62f, 1f));
                waterMaterial.SetColor("_DeepColor", new Color(.008f, .045f, .16f, 1f));
                waterMaterial.SetFloat("_FlowSpeed", (waterKind == YQHydrologyKindV2.Waterfall ? .16f : waterKind == YQHydrologyKindV2.River ? .075f : .012f) * Mathf.Clamp(flowScale, .75f, 2.8f));
                waterMaterial.SetFloat("_RippleStrength", waterKind == YQHydrologyKindV2.Waterfall ? .28f : .22f);
                waterMaterial.SetFloat("_WorldUV", waterKind == YQHydrologyKindV2.Lake || waterKind == YQHydrologyKindV2.Wetland ? 1f : 0f);
            }
            // note: Draw water after opaque terrain but before alpha-tested and translucent props, so props occlude the river without changing the accepted surface mesh.
            waterMaterial.renderQueue = waterKind == YQHydrologyKindV2.Lake || waterKind == YQHydrologyKindV2.Wetland ? 2400 : 2401;
            materials[index] = waterMaterial;
        }
        renderer.sharedMaterials = materials;
    }

    private static float EstimateWaterFlowScale(
        List<Vector3> points,
        YQHydrologyKindV2 kind)
    {
        // note: Aggregate per-segment drop keeps speed deterministic and responds to mountain descents instead of only the endpoints.
        if (points == null || points.Count < 2 || kind != YQHydrologyKindV2.River)
            return 1f;
        float weightedSlope = 0f;
        float totalDistance = 0f;
        for (int index = 1; index < points.Count; index++)
        {
            Vector3 delta = points[index] - points[index - 1];
            float distance = new Vector2(delta.x, delta.z).magnitude;
            if (distance < .05f)
                continue;
            weightedSlope += Mathf.Abs(delta.y) / distance * distance;
            totalDistance += distance;
        }
        if (totalDistance < .05f)
            return 1f;
        float slope = weightedSlope / totalDistance;
        return Mathf.Clamp(1f + slope * 14f, 1f, 2.8f);
    }

    private static Mesh BuildMacroWaterEllipseMesh(
        Terrain terrain,
        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin,
        int basinIndex)
    {
        const int segmentCount = 64;
        int ringVertexCount = segmentCount * 2;
        Vector3[] vertices =
            new Vector3[ringVertexCount + 1];
        Vector3[] normals =
            new Vector3[ringVertexCount + 1];
        Vector2[] uvs =
            new Vector2[ringVertexCount + 1];
        Color[] colours =
            new Color[ringVertexCount + 1];
        int[] triangles =
            new int[segmentCount * 9];
        // note: Keep the visible basin inside the accepted hydrology footprint; the former near-full ellipse crossed steep bank cells and exposed a floating skirt.
        // note: Keep the visible basin close to the accepted footprint; the previous double inset left a dry annulus large enough to separate a connected river mouth.
        float longRadius =
            basin.LongRadius * 0.96f;
        float shortRadius =
            basin.ShortRadius * 0.96f;

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        uvs[0] = new Vector2(0.5f, 0.5f);
        // note: Macro lake vertices are not ribbon vertices; green=.5 keeps the ribbon shoreline feather disabled across the basin interior.
        colours[0] = new Color(1f, .5f, 1f, 1f);

        // note: The outer ring fades the fitted lake into the carved shoreline instead of exposing a hard polygon boundary.
        // note: The fade skirt stays inside the accepted shoreline so its transparent vertices cannot project onto dry terrain.
        // note: The nearly full outer skirt bridges the same shoreline support used by terrain carving without projecting a hard polygon beyond the basin.
        float outerLongRadius = basin.LongRadius * 0.995f;
        float outerShortRadius = basin.ShortRadius * 0.995f;

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
            // note: Radii above already contain the shoreline inset. Applying it twice shrank the lake away from the accepted river mouth.
            // note: A lake is a level water plane; banks occlude it through depth testing rather than pulling its vertices down to the lake bed.
            int vertexIndex = segment + 1;

            vertices[vertexIndex] =
                new Vector3(
                    offset.x,
                    0f,
                    offset.y);
            normals[vertexIndex] = Vector3.up;
            uvs[vertexIndex] =
                new Vector2(
                    0.5f + Mathf.Cos(angle) * 0.5f,
                    0.5f + Mathf.Sin(angle) * 0.5f);
            // note: Keep the basin interior at the neutral across-channel coordinate so the shared water shader does not fade the lake away.
            colours[vertexIndex] = new Color(1f, .5f, 1f, 1f);

            Vector2 outerOffset =
                basin.LongAxisXZ * (Mathf.Cos(angle) * outerLongRadius) +
                basin.ShortAxisXZ * (Mathf.Sin(angle) * outerShortRadius);
            int outerVertexIndex = vertexIndex + segmentCount;
            vertices[outerVertexIndex] = new Vector3(
                outerOffset.x,
                0f,
                outerOffset.y);
            normals[outerVertexIndex] = Vector3.up;
            uvs[outerVertexIndex] = uvs[vertexIndex];
            // note: Keep a nearly transparent outer vertex so the water shader can honor the skirt coverage without changing legacy uncolored meshes.
            // note: The skirt remains nearly transparent while retaining the neutral across-channel coordinate used by the lake surface.
            colours[outerVertexIndex] = new Color(1f, .5f, 1f, 0.001f);

            int triangleIndex = segment * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] =
                (segment + 1) % segmentCount + 1;
            triangles[triangleIndex + 2] =
                vertexIndex;

            int ringTriangleIndex = segmentCount * 3 + segment * 6;
            int nextInner = (segment + 1) % segmentCount + 1;
            int nextOuter = nextInner + segmentCount;
            triangles[ringTriangleIndex] = vertexIndex;
            triangles[ringTriangleIndex + 1] = nextInner;
            triangles[ringTriangleIndex + 2] = outerVertexIndex;
            triangles[ringTriangleIndex + 3] = outerVertexIndex;
            triangles[ringTriangleIndex + 4] = nextInner;
            triangles[ringTriangleIndex + 5] = nextOuter;
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
        mesh.colors = colours;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        GeneratedMacroWaterMeshes.Add(mesh);

        return mesh;
    }

    private static float ResolveWaterRadialSupport(
        Terrain terrain,
        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin,
        Vector2 offset)
    {
        if (terrain == null || terrain.terrainData == null)
            return 1f;

        // note: Find the furthest supported point on this radial so a lake surface cannot project across a raised or missing bank.
        const int samples = 12;
        float supported = 1f;
        Vector3 center = basin.CenterWorld;
        for (int sample = 1; sample <= samples; sample++)
        {
            float factor = sample / (float)samples;
            Vector3 point = new Vector3(
                center.x + offset.x * factor,
                basin.WaterSurfaceY,
                center.z + offset.y * factor);
            float terrainY =
                terrain.SampleHeight(point) +
                terrain.transform.position.y;
            if (terrainY > basin.WaterSurfaceY + .35f)
            {
                supported = (sample - 1) / (float)samples;
                break;
            }
        }

        return Mathf.Clamp01(supported);
    }

    private static float ResolveWaterVertexHeight(
        Terrain terrain,
        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin,
        Vector2 offset)
    {
        if (terrain == null || terrain.terrainData == null)
            return 0f;

        // note: Lower only unsupported shallow-bank vertices to the terrain skin; the basin center remains at the authored water level.
        Vector3 worldPoint = new Vector3(
            basin.CenterWorld.x + offset.x,
            basin.WaterSurfaceY,
            basin.CenterWorld.z + offset.y);
        float terrainY =
            SampleMinimumTerrainHeight(
                terrain,
                worldPoint);
        return Mathf.Min(
            0f,
            terrainY + .08f - basin.WaterSurfaceY);
    }

    private static float SampleMinimumTerrainHeight(
        Terrain terrain,
        Vector3 center)
    {
        float minimum = float.PositiveInfinity;
        // note: Neighbor samples make each shoreline segment conservative across steep terrain changes between polygon vertices.
        for (int sample = 0; sample < 5; sample++)
        {
            Vector3 point = center;
            if (sample == 1) point.x -= 2.5f;
            else if (sample == 2) point.x += 2.5f;
            else if (sample == 3) point.z -= 2.5f;
            else if (sample == 4) point.z += 2.5f;
            float height =
                terrain.SampleHeight(point) +
                terrain.transform.position.y;
            if (height < minimum)
                minimum = height;
        }
        return minimum;
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

    private static void ReleaseGeneratedLivedPathMeshes()
    {
        for (int index = 0; index < GeneratedLivedPathMeshes.Count; index++)
        {
            Mesh mesh = GeneratedLivedPathMeshes[index];
            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
        }

        // note: Runtime road meshes have explicit ownership so rebuilding a world cannot retain old path geometry in native memory.
        GeneratedLivedPathMeshes.Clear();

        // note: Destroy prior generated material instances with the mesh so repeated world rebuilds do not accumulate hidden renderer allocations.
        for (int index = 0; index < GeneratedLivedPathMaterials.Count; index++)
        {
            Material material = GeneratedLivedPathMaterials[index];
            if (material != null)
                UnityEngine.Object.Destroy(material);
        }
        GeneratedLivedPathMaterials.Clear();
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
        // note: Accepted V2 worlds publish canopy through bounded streamed owners and the origin approach; the legacy native-tree pass produced zero V2 instances while still paying its full rejection-sampling cost.
        bool useNativeTerrainTrees = !YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan);
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
        uint terrainSeedHash =
            StableHash32(
                YQGeneratedWorldTerrain.TerrainGenerationVersion +
                "|" +
                SafeText(
                    plan.worldSeed,
                    "yourquest_default_world"));
        YQPreparedSpatialMaterializationV2 preparedEcologyV2 =
            macroWater.preparedV2;
        YQGeneratedWorldTilePlan tilePlan = null;
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
        {
            if (preparedEcologyV2 == null)
            {
                // note: Authoritative V2 foliage cannot fall back to a separate V1 ecology grid after its spatial projection was rejected.
                completed?.Invoke(0, 0);
                yield break;
            }
        }
        else
        {
            tilePlan = new YQGeneratedWorldTilePlan(
                terrainSeedHash,
                YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan));
        }
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
        // note: A completed shard load advances the bounded wilderness transaction even before vegetation has been published.
        YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
        yield return registry.PreloadAssetPathsRoutine(
            ApprovedDryTreePrefabs);
        YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();

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
            // note: Grass is an explicit approved terrain layer, not an accidental selection from a bush prefab's atlas.
            Texture2D grass = registry.TerrainGrassTexture;
            if (grass != null && !ContainsAnySemantic(SafeText(palette.styleKey, string.Empty).ToLowerInvariant(), "desert", "snow", "arctic"))
            {
                if (!detailPrototypeByTexture.TryGetValue(grass.GetInstanceID(), out int grassIndex))
                {
                    grassIndex = detailPrototypes.Count;
                    detailPrototypeByTexture.Add(grass.GetInstanceID(), grassIndex);
                    detailPrototypes.Add(CreateTerrainDetailPrototype(grass));
                }
                profile.detailPrototypeIndices.Add(grassIndex);
            }

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

                if (useNativeTerrainTrees && LooksLikeTree(null, reference))
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
                else if (profile.detailPrototypeIndices.Count < 3 &&
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

            if (useNativeTerrainTrees && ShouldSupplementWithApprovedConifers(
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

            // note: A persisted palette may contain only one or two compatible trees; fill the native Terrain family from the approved, visually distinct canopy set before publication so streamed biomes cannot collapse to repeated silhouettes.
            string[] approvedCanopyPaths =
                ContainsAnySemantic(
                    SafeText(profile.palette.styleKey, string.Empty),
                    "desert",
                    "persepolis",
                    "western",
                    "badland")
                    ? ApprovedDryTreePrefabs
                    : ApprovedVisibleTreePrefabs;
            for (int canopyIndex = 0;
                 useNativeTerrainTrees &&
                 canopyIndex < approvedCanopyPaths.Length &&
                 profile.treePrototypeIndices.Count < 4;
                 canopyIndex++)
            {
                string canopyPath = approvedCanopyPaths[canopyIndex];
                GameObject canopyPrefab = registry.ResolvePrefab(canopyPath);
                if (!IsTerrainTreePrototypeCompatible(canopyPrefab, canopyPath))
                    continue;

                // note: RegisterTerrainTreePrototype deduplicates shared palette and approved-family paths while retaining the four-variant minimum.
                RegisterTerrainTreePrototype(
                    profile,
                    canopyPath,
                    canopyPrefab,
                    treePrototypes,
                    treePrototypeByPath);
            }

            profiles.Add(profile);
        }

        int treeCount = 0;

        if (treePrototypes.Count > 0)
        {
            // note: Native terrain instances place prefab pivots, not visible roots. Measure each imported prototype once, preserving its authored scale.
            float[] treeBaseOffsets = new float[treePrototypes.Count];
            for (int index = 0; index < treePrototypes.Count; index++)
            {
                GameObject treePrefab = treePrototypes[index].prefab;
                if (TryGetTreeTrunkBounds(treePrefab, out Bounds treeBounds) ||
                    TryGetWildernessBounds(treePrefab, out treeBounds))
                {
                    float offset = treeBounds.min.y - treePrefab.transform.position.y;
                    if (!float.IsNaN(offset) && !float.IsInfinity(offset))
                        treeBaseOffsets[index] = offset;
                }
            }
            data.treePrototypes = treePrototypes.ToArray();
            yield return null;

            List<TreeInstance> instances =
                new List<TreeInstance>(
                    Mathf.Min(
                        MaximumTerrainTreeInstances,
                        profiles.Count * 320));
            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 terrainSize = data.size;
            uint seedHash = terrainSeedHash;
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
                            profile.palette) *
                        2.3f *
                        Mathf.Lerp(
                            0.72f,
                            1.32f,
                            SampleVegetationEcology(
                                tilePlan,
                                preparedEcologyV2,
                                profile.center.x,
                                profile.center.z,
                                out _,
                                out _)
                                .ForestDensity)),
                    120,
                    460);
                int placedForRegion = 0;
                int attempts = target * 6;

                for (int attempt = 0;
                     attempt < attempts && placedForRegion < target &&
                     instances.Count < MaximumTerrainTreeInstances;
                     attempt++)
                {
                    // note: Tree rejection sampling has a fixed attempt limit but can span the startup watchdog window on sparse palettes.
                    if ((attempt & 63) == 0)
                        YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
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

                    YQGeneratedWorldTileProfile tileProfile =
                        SampleVegetationEcology(
                            tilePlan,
                            preparedEcologyV2,
                            position.x,
                            position.z,
                            out YQSpatialTerrainSampleV2 terrainMasks,
                            out float civilizationDensity);

                    if (preparedEcologyV2 != null &&
                        !IsPreparedVegetationPositionAllowed(
                            terrainMasks,
                            false))
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

                    float maximumTreeSlope =
                        Mathf.Lerp(
                            27f,
                            36f,
                            tileProfile.Ruggedness);

                    if (tileProfile.Biome ==
                        YQGeneratedWorldBiomeKind.Wetland)
                    {
                        maximumTreeSlope =
                            Mathf.Min(
                                maximumTreeSlope,
                                25f);
                    }

                    if (Vector3.Angle(Vector3.up, normal) >
                        maximumTreeSlope)
                    {
                        continue;
                    }

                    float groveNoise = Mathf.PerlinNoise(
                        noiseOffsetX + position.x * 0.0105f,
                        noiseOffsetZ + position.z * 0.0105f);
                    float moistureNoise = Mathf.PerlinNoise(
                        noiseOffsetZ + position.x * 0.0038f,
                        noiseOffsetX + position.z * 0.0038f);

                    float ecologicalTreeScore =
                        groveNoise *
                            0.48f +
                        moistureNoise *
                            0.2f +
                        tileProfile.ForestDensity *
                            0.25f +
                        tileProfile.Moisture *
                            0.07f;

                    ecologicalTreeScore -=
                        civilizationDensity * 0.06f;

                    float ecologicalTreeThreshold =
                        profile.treeMaskThreshold +
                        Mathf.Lerp(
                            0.1f,
                            -0.08f,
                            tileProfile.ForestDensity) +
                        ResolveBiomeTreeThresholdOffset(
                            tileProfile.Biome);

                    // note: Biome suitability controls canopy acceptance while grove noise still forms irregular clearings and clusters inside each ecological province.
                    if (ecologicalTreeScore <
                        ecologicalTreeThreshold)
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

                    baseScale *=
                        Mathf.Lerp(
                            0.86f,
                            1.08f,
                            tileProfile.Moisture);

                    Color treeColor =
                        ResolveBiomeTreeColor(
                            tileProfile,
                            Deterministic01(
                                seed +
                                "|tint"));

                    // note: Apply the visible base offset after deterministic height scaling; do not snap again or Unity would restore the uncorrected pivot height.
                    instances.Add(
                        new TreeInstance
                        {
                            position = new Vector3(
                                normalizedX,
                                ResolveTerrainTreeNormalizedHeight(position.y, terrainOrigin.y, terrainSize.y,
                                    treeBaseOffsets[profile.treePrototypeIndices[localPrototype]], baseScale),
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
                            color = treeColor,
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
            // note: Keep the sampled, pivot-corrected heights; a global snap would undo visible-root grounding.
            data.SetTreeInstances(instances.ToArray(), false);
            // note: Native tree publication is a completed vegetation milestone before the denser ground-cover grid begins.
            YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
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
                YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                yield return null;
            }

            for (int z = 0; z < TerrainDetailResolution; z++)
            {
                float normalizedZ =
                    (z + 0.5f) / TerrainDetailResolution;
                float worldZ = terrainOrigin.z + normalizedZ * terrainSize.z;

                for (int x = 0; x < TerrainDetailResolution; x++)
                {
                    // note: Check the frame budget before rejection paths too; most detail cells exit early and would otherwise skip the inner-loop yield.
                    if ((x & 31) == 31 &&
                        Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
                    {
                        yield return null;
                        frameStartedAt = Time.realtimeSinceStartup;
                    }
                    float normalizedX =
                        (x + 0.5f) / TerrainDetailResolution;
                    Vector3 worldPosition = new Vector3(
                        terrainOrigin.x + normalizedX * terrainSize.x,
                        0f,
                        worldZ);
                    TerrainVegetationProfile profile =
                        FindNearestVegetationProfile(profiles, worldPosition);
                    // note: Reject empty palettes and occupied route cells before the expensive ecology projection.
                    if (profile == null ||
                        profile.detailPrototypeIndices.Count == 0 ||
                        IsNearLivedPath(livedPaths, worldPosition, 1.8f))
                    {
                        continue;
                    }
                    YQGeneratedWorldTileProfile tileProfile =
                        SampleVegetationEcology(
                            tilePlan,
                            preparedEcologyV2,
                            worldPosition.x,
                            worldPosition.z,
                            out YQSpatialTerrainSampleV2 terrainMasks,
                            out float civilizationDensity);

                    if ((preparedEcologyV2 != null &&
                         !IsPreparedVegetationPositionAllowed(
                             terrainMasks,
                             true)) ||
                        !IsTerrainDetailPositionAllowed(
                            terrain,
                            plan,
                            worldPosition,
                            settlementDetailReserves,
                            encampmentDetailReserves,
                            20f,
                            28f,
                            16f,
                            macroWater,
                            terrainMasks.waterMask))
                    {
                        continue;
                    }

                    float slopeDegrees = SampleHeightmapSlopeDegrees(
                        terrainHeights,
                        terrainSize,
                        normalizedX,
                        normalizedZ);
                    float maximumDetailSlope =
                        tileProfile.Biome ==
                            YQGeneratedWorldBiomeKind.Highland
                                ? 42f
                                : tileProfile.Biome ==
                                    YQGeneratedWorldBiomeKind.Wetland
                                        ? 32f
                                        : 38f;

                    if (slopeDegrees > maximumDetailSlope)
                        continue;

                    float patchNoise = Mathf.PerlinNoise(
                        detailOffsetX + worldPosition.x * 0.026f,
                        detailOffsetZ + worldPosition.z * 0.026f);
                    float biomeNoise = Mathf.PerlinNoise(
                        detailOffsetZ + worldPosition.x * 0.006f,
                        detailOffsetX + worldPosition.z * 0.006f);
                    float densityMask =
                        patchNoise *
                            0.45f +
                        biomeNoise *
                            0.25f +
                        tileProfile.ForestDensity *
                            0.16f +
                        tileProfile.Moisture *
                            0.14f;

                    densityMask +=
                        civilizationDensity * 0.03f;

                    densityMask -=
                        Mathf.InverseLerp(
                            18f,
                            maximumDetailSlope,
                            slopeDegrees) *
                        0.1f;

                    float threshold =
                        profile.detailMaskThreshold +
                        ResolveBiomeDetailThresholdOffset(
                            tileProfile.Biome);

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
                            (densityMask - threshold) *
                                15f +
                            ResolveBiomeGroundCoverBonus(
                                tileProfile.Biome)),
                        2,
                        12);

                    detailMaps[layerIndex][z, x] = density;
                    detailCount += density;

                }

                // note: Finished rows prove bounded ground-cover work is advancing; a long but healthy synthesis must not trip the no-progress watchdog.
                if ((z & 15) == 15)
                    YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
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
                    // note: Each published strip is a completed native Terrain update, so the startup deadline tracks real work.
                    YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                    yield return null;
                }
            }

            // note: Native patches batch grass nearby; distant grass is represented by the terrain layer rather than thousands of transparent cards.
            terrain.detailObjectDistance = 85f;
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

    private static YQGeneratedWorldTileProfile SampleVegetationEcology(
        YQGeneratedWorldTilePlan tilePlan,
        YQPreparedSpatialMaterializationV2 preparedV2,
        float worldX,
        float worldZ,
        out YQSpatialTerrainSampleV2 terrainMasks,
        out float civilizationDensity)
    {
        if (preparedV2 != null)
        {
            YQSpatialEcologySampleV2 sample =
                preparedV2.SampleEcologyContext(worldX, worldZ);
            terrainMasks = sample.terrain;
            civilizationDensity = sample.civilizationDensity;
            return sample.tileProfile;
        }

        terrainMasks = default;
        civilizationDensity = 0f;

        // note: Persisted V1 worlds retain their existing deterministic tile ecology until V2 becomes the reviewed runtime authority.
        return tilePlan.Sample(worldX, worldZ);
    }

    private static bool IsPreparedVegetationPositionAllowed(
        YQSpatialTerrainSampleV2 sample,
        bool terrainDetail)
    {
        float routeLimit = terrainDetail ? 0.16f : 0.1f;
        float reserveLimit = terrainDetail ? 0.08f : 0.05f;
        float caveLimit = terrainDetail ? 0.22f : 0.16f;

        // note: V2 trees and ground cover use the exact terrain masks, leaving readable road shoulders, dry construction reserves, open cave mouths, and continuous shorelines.
        return sample.waterMask < 0.12f &&
               sample.routeMask < routeLimit &&
               sample.siteReserveMask < reserveLimit &&
               sample.caveMassMask < caveLimit;
    }

    private static bool IsPreparedRockPositionAllowed(
        YQSpatialTerrainSampleV2 sample)
    {
        // note: Geological dressing may frame a route or cave but cannot occupy the walkable corridor, construction shelf, water bed, or concealed entrance core.
        return sample.waterMask < 0.12f &&
               sample.routeMask < 0.08f &&
               sample.siteReserveMask < 0.04f &&
               sample.caveMassMask < 0.42f;
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

    private struct StreamedDetailBuildResult
    {
        public int[,] densities;
        public int eligibleSamples;
        public int placed;
        public float computationSeconds;
    }

    private static StreamedDetailBuildResult BuildStreamedDetailPayload(
        YQPreparedSpatialMaterializationV2 prepared,
        Vector2Int coordinate,
        Vector3 origin,
        Vector3 size,
        uint seed,
        float[] slopeDegrees,
        CancellationToken cancellationToken)
    {
        const int resolution = 64;
        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        int[,] densities = new int[resolution, resolution];
        int eligibleSamples = 0;
        int placed = 0;
        for (int z = 0; z < resolution; z++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < resolution; x++)
            {
                float worldX = origin.x + (x + 0.5f) / resolution * size.x;
                float worldZ = origin.z + (z + 0.5f) / resolution * size.z;
                bool allowed = true;
                float ecologyDensity = 1f;
                if (prepared != null)
                {
                    // note: Reuse the ecology sample's canonical terrain masks and the main-thread-captured exact Terrain normal.
                    YQSpatialEcologySampleV2 ecology = prepared.SampleEcologyContext(worldX, worldZ);
                    YQSpatialTerrainSampleV2 sample = ecology.terrain;
                    float maximumDetailSlope =
                        ecology.tileProfile.Biome == YQGeneratedWorldBiomeKind.Highland
                            ? 42f
                            : ecology.tileProfile.Biome == YQGeneratedWorldBiomeKind.Wetland
                                ? 32f
                                : 38f;
                    ecologyDensity = Mathf.Clamp01(
                        ecology.tileProfile.Moisture * 0.62f +
                        (1f - ecology.civilizationDensity) * 0.38f);
                    // note: Preserve accepted water, route, reserve, cave, moisture, density, and slope exclusions without touching Unity objects on the worker.
                    allowed = sample.waterMask < 0.18f && sample.routeMask < 0.12f &&
                        sample.siteReserveMask < 0.08f && sample.caveMassMask < 0.35f &&
                        ecology.tileProfile.Moisture >= 0.18f &&
                        ecologyDensity >= 0.24f &&
                        slopeDegrees[z * resolution + x] <= maximumDetailSlope;
                }
                if (!allowed)
                    continue;

                eligibleSamples++;
                float noise = Mathf.PerlinNoise(
                    ((seed & 0xFFFFu) * 0.00017f) + worldX * 0.031f,
                    (((seed >> 16) & 0xFFFFu) * 0.00019f) + worldZ * 0.031f);
                float placementNoise = noise * Mathf.Lerp(0.78f, 1.12f, ecologyDensity);
                if (placementNoise > 0.42f)
                {
                    int density = Mathf.Clamp(2 + Mathf.FloorToInt(placementNoise * 8f), 1, 8);
                    densities[z, x] = density;
                    placed += density;
                }
            }
        }
        float elapsedSeconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - startedAt) /
            (double)System.Diagnostics.Stopwatch.Frequency);
        return new StreamedDetailBuildResult
        {
            densities = densities,
            eligibleSamples = eligibleSamples,
            placed = placed,
            computationSeconds = elapsedSeconds
        };
    }

    public static IEnumerator PaintStreamedDetailRoutine(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Vector2Int coordinate,
        YQContinuousWorldCellAuthority authority,
        YQRuntimeWorldAssetRegistry registry,
        Action<int> completed = null)
    {
        if (terrain == null || terrain.terrainData == null || plan == null ||
            authority == null || registry == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        TerrainData data = terrain.terrainData;
        LastStreamedDetailStep = "grassTextureResolve";
        Texture2D grassTexture = registry.TerrainGrassTexture;
        // note: Canonical V2 must use the approved grass-detail asset; only legacy compatibility may derive a detail texture from an existing base layer.
        if (grassTexture == null &&
            !YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) &&
            data.terrainLayers != null)
        {
            // note: Keep the old compatibility fallback isolated from accepted generated worlds so a base diffuse cannot masquerade as streamed grass.
            for (int layerIndex = 0; layerIndex < data.terrainLayers.Length; layerIndex++)
            {
                TerrainLayer layer = data.terrainLayers[layerIndex];
                if (layer != null && layer.diffuseTexture != null)
                {
                    grassTexture = layer.diffuseTexture;
                    break;
                }
            }
        }
        if (grassTexture == null)
        {
            // note: Canonical V2 terrain must expose an approved detail asset; a missing grass binding is a readiness failure, not a green-texture substitute.
            if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
            {
                Debug.LogError("[YQGeneratedWorldEnvironment] STREAMED DETAIL ASSET UNAVAILABLE for " + coordinate);
                completed?.Invoke(-1);
            }
            else
            {
                completed?.Invoke(0);
            }
            yield break;
        }
        const int resolution = 64;
        LastStreamedDetailStep = "detailResolution";
        data.SetDetailResolution(resolution, 16);
        yield return null;
        LastStreamedDetailStep = "detailPrototypeBinding";
        data.detailPrototypes = new[] { CreateTerrainDetailPrototype(grassTexture) };
        yield return null;
        YQPreparedSpatialMaterializationV2 prepared = null;
        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out YQPreparedSpatialMaterializationV2 resolved,
                out string preparationFailure))
        {
            // note: Do not invent a renderer-side grass mask when the accepted V2 projection is missing or stale.
            if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
            {
                Debug.LogError(
                    "[YQGeneratedWorldEnvironment] STREAMED DETAIL PROJECTION UNAVAILABLE " +
                    coordinate + ": " + preparationFailure);
                completed?.Invoke(-1);
                yield break;
            }
        }
        else
        {
            prepared = resolved;
        }

        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        uint seed = StableHash32((plan.worldSeed ?? string.Empty) + "|streamed_detail|" +
            coordinate.x + "|" + coordinate.y);
        float[] slopeDegrees = prepared != null ? new float[resolution * resolution] : null;
        long normalSampleTicks = 0;
        long sliceStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int z = 0; z < resolution && prepared != null; z++)
        {
            long rowStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int x = 0; x < resolution; x++)
            {
                float worldX = origin.x + (x + 0.5f) / resolution * size.x;
                float worldZ = origin.z + (z + 0.5f) / resolution * size.z;
                float normalizedX = Mathf.Clamp01((worldX - origin.x) / Mathf.Max(0.001f, size.x));
                float normalizedZ = Mathf.Clamp01((worldZ - origin.z) / Mathf.Max(0.001f, size.z));
                slopeDegrees[z * resolution + x] = Vector3.Angle(
                    Vector3.up,
                    data.GetInterpolatedNormal(normalizedX, normalizedZ));
            }
            long rowFinishedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            normalSampleTicks += rowFinishedAt - rowStartedAt;
            // note: Capture Unity Terrain normals in short elapsed-time slices; pure ecology sampling runs concurrently off-thread.
            if (z + 1 < resolution && rowFinishedAt - sliceStartedAt >= System.Diagnostics.Stopwatch.Frequency * 0.004)
            {
                LastStreamedDetailStep = "terrainNormalCapture:" + (z + 1);
                yield return null;
                sliceStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            }
        }
        YQGeneratedWorldEnvironment.RecordStreamedDetailNormalSeconds(
            (float)(normalSampleTicks / (double)System.Diagnostics.Stopwatch.Frequency));

        CancellationTokenSource cancellation = new CancellationTokenSource();
        Task<StreamedDetailBuildResult> task = null;
        try
        {
            // note: Keep the exact deterministic ecology and density formulas while moving their CPU work off the Unity main thread.
            CancellationToken cancellationToken = cancellation.Token;
            task = Task.Run(
                () => BuildStreamedDetailPayload(
                    prepared, coordinate, origin, size, seed, slopeDegrees, cancellationToken),
                cancellationToken);
            while (!task.IsCompleted)
            {
                LastStreamedDetailStep = "detailPayloadWorker";
                yield return null;
            }

            StreamedDetailBuildResult result = task.GetAwaiter().GetResult();
            YQGeneratedWorldEnvironment.RecordStreamedDetailComputeSeconds(result.computationSeconds);
            terrain.detailObjectDistance = 90f;
            terrain.detailObjectDensity = 1f;
            LastStreamedDetailStep = "detailLayerUpload";
            long uploadStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            data.SetDetailLayer(0, 0, 0, result.densities);
            YQGeneratedWorldEnvironment.RecordStreamedDetailUploadSeconds(
                (float)((System.Diagnostics.Stopwatch.GetTimestamp() - uploadStartedAt) /
                    (double)System.Diagnostics.Stopwatch.Frequency));
            if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) &&
                result.eligibleSamples > 0 && result.placed <= 0)
            {
                // note: Canonical V2 requires actual streamed groundcover on eligible terrain; a completed coroutine alone is not visible ecology.
                Debug.LogError(
                    "[YQGeneratedWorldEnvironment] STREAMED DETAIL EMPTY on eligible ground " +
                    coordinate);
                completed?.Invoke(-1);
                yield break;
            }
            // note: Record actual streamed groundcover counts so readiness still proves that the accepted detail mask has visible grass.
            LastStreamedDetailStep = "ready";
            Debug.Log("[YQGeneratedWorldEnvironment] STREAMED DETAIL READY " + coordinate +
                " eligible=" + result.eligibleSamples + " placed=" + result.placed);
            completed?.Invoke(result.placed);
        }
        finally
        {
            // note: A replaced or unloaded tile cancels pending row work and leaves stale workers unable to publish into a new owner.
            cancellation.Cancel();
            if (task == null || task.IsCompleted)
            {
                if (task != null && task.IsFaulted)
                    _ = task.Exception;
                cancellation.Dispose();
            }
            else
            {
                task.ContinueWith(completedTask =>
                {
                    if (completedTask.IsFaulted)
                        _ = completedTask.Exception;
                    cancellation.Dispose();
                }, TaskScheduler.Default);
            }
        }
    }

    internal static float ResolveTerrainTreeNormalizedHeight(float groundHeight, float terrainOriginY,
        float terrainHeight, float prototypeBaseOffset, float heightScale)
    {
        // note: A tree whose mesh starts above its pivot needs a lower pivot; negative normalized root positions are intentional near the terrain's minimum height.
        return (groundHeight - terrainOriginY - prototypeBaseOffset * heightScale - 0.05f) /
            Mathf.Max(0.001f, terrainHeight);
    }

    private static bool IsTerrainTreePrototypeCompatible(
        GameObject prefab,
        string assetPath)
    {
        if (prefab == null ||
            string.IsNullOrWhiteSpace(assetPath) ||
            prefab.GetComponentInChildren<LODGroup>(true) == null)
        {
            return false;
        }

        // note: Compatibility belongs to actual LOD geometry and materials, not the vendor's directory naming convention.
        foreach (LODGroup group in prefab.GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] levels = group.GetLODs();
            if (levels.Length == 0) return false;
            foreach (LOD level in levels)
            {
                if (level.renderers == null || level.renderers.Length == 0) return false;
                foreach (Renderer lodRenderer in level.renderers)
                {
                    if (lodRenderer == null) return false;
                    if (lodRenderer is MeshRenderer)
                    {
                        MeshFilter filter = lodRenderer.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                            return false;
                    }
                    else if (lodRenderer is BillboardRenderer billboard)
                    {
                        if (billboard.billboard == null) return false;
                    }
                    else return false;
                }
            }
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

            if (renderer is BillboardRenderer billboardRenderer &&
                billboardRenderer.billboard != null)
            {
                // note: Approved tree billboards carry their material through the BillboardAsset; a null shared slot is valid and must not reject the whole native Terrain prototype.
                foundRenderable = true;
                continue;
            }

            Material[] materials =
                renderer.sharedMaterials;

            // note: Native terrain bypasses hierarchy repair; missing mesh slots or foreign-pipeline materials must be rejected before prototype publication.
            int requiredSlots = YQRuntimeUrpMaterialRepair.ResolveRequiredMaterialSlotCount(renderer);
            if (materials == null || materials.Length < requiredSlots)
                return false;

            for (int materialIndex = 0;
                 materialIndex < materials.Length;
                 materialIndex++)
            {
                Material material =
                    materials[materialIndex];

                bool approvedUrpShaderGraph =
                    material != null &&
                    material.shader != null &&
                    (assetPath ?? string.Empty).IndexOf(
                        "/Render Pipeline Support/URP/",
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    material.shader.name.StartsWith(
                        "Shader Graphs/",
                        StringComparison.OrdinalIgnoreCase);
                // note: Approved URP tree prefabs ship Shader Graph materials without a serialized pipeline tag; accept only that explicit URP family while preserving the general material safety gate for every other asset.
                if (!approvedUrpShaderGraph &&
                    !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material))
                {
                    return false;
                }

                foundRenderable =
                    true;
            }
        }

        // note: Existing curated tree selection still owns semantic eligibility; every admitted prototype must have usable geometry and pipeline-compatible materials.
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

        // note: Western grasslands still receive approved conifers; only explicitly barren or incompatible styles suppress the native tree prototypes.
        return !ContainsAnySemantic(
            style,
            "desert",
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

    private static float ResolveBiomeTreeThresholdOffset(
        YQGeneratedWorldBiomeKind biome)
    {
        // note: Canopy density and ground-cover density are separate ecological layers, so open moorland can remain lush without becoming an implausible closed forest.
        switch (biome)
        {
            case YQGeneratedWorldBiomeKind.AncientWoodland:
                return -0.08f;

            case YQGeneratedWorldBiomeKind.Wetland:
                return -0.025f;

            case YQGeneratedWorldBiomeKind.Highland:
                return 0.035f;

            case YQGeneratedWorldBiomeKind.Moorland:
                return 0.1f;

            default:
                return 0f;
        }
    }

    private static float ResolveBiomeDetailThresholdOffset(
        YQGeneratedWorldBiomeKind biome)
    {
        switch (biome)
        {
            case YQGeneratedWorldBiomeKind.AncientWoodland:
                return -0.075f;

            case YQGeneratedWorldBiomeKind.Wetland:
                return -0.1f;

            case YQGeneratedWorldBiomeKind.Moorland:
                return -0.07f;

            case YQGeneratedWorldBiomeKind.Highland:
                return 0.025f;

            default:
                return -0.025f;
        }
    }

    private static float ResolveBiomeGroundCoverBonus(
        YQGeneratedWorldBiomeKind biome)
    {
        switch (biome)
        {
            case YQGeneratedWorldBiomeKind.Wetland:
                return 2f;

            case YQGeneratedWorldBiomeKind.AncientWoodland:
            case YQGeneratedWorldBiomeKind.Moorland:
                return 1f;

            default:
                return 0f;
        }
    }

    private static Color ResolveBiomeTreeColor(
        YQGeneratedWorldTileProfile profile,
        float variation)
    {
        float dryAmount =
            Mathf.Clamp01(
                (1f - profile.Moisture) *
                    0.28f +
                variation *
                    0.08f);

        Color healthy =
            profile.Biome ==
                YQGeneratedWorldBiomeKind.Highland
                    ? new Color(
                        0.9f,
                        0.94f,
                        0.88f,
                        1f)
                    : new Color(
                        0.94f,
                        1f,
                        0.92f,
                        1f);

        Color dry =
            new Color(
                0.92f,
                0.88f,
                0.78f,
                1f);

        // note: Terrain tree tint stays deliberately subtle so imported bark and leaf materials retain their authored color response.
        return
            Color.Lerp(
                healthy,
                dry,
                dryAmount);
    }

    // ============================================================
    // WILDERNESS
    // ============================================================

    private static IEnumerator RunRequiredEcologyLayersRoutine(
        params RequiredEcologyLayerWork[] layers)
    {
        if (layers == null || layers.Length == 0)
            yield break;

        int rotation = 0;
        try
        {
            while (true)
            {
                int activeInstantiations = 0;
                bool hasWork = false;
                for (int index = 0; index < layers.Length; index++)
                {
                    RequiredEcologyLayerWork layer = layers[index];
                    if (layer == null || layer.iterators.Count == 0)
                        continue;
                    hasWork = true;
                    if (layer.pendingInstantiation != null)
                    {
                        if (layer.pendingInstantiation.isDone)
                            layer.pendingInstantiation = null;
                        else
                            activeInstantiations++;
                    }
                    if (layer.pendingCustomYield != null && !layer.pendingCustomYield.keepWaiting)
                        layer.pendingCustomYield = null;
                }

                if (!hasWork)
                    yield break;

                float frameSliceStarted = Time.realtimeSinceStartup;
                for (int offset = 0; offset < layers.Length; offset++)
                {
                    if (Time.realtimeSinceStartup - frameSliceStarted >= RequiredEcologyLayerSliceSeconds)
                        break;

                    int index = (rotation + offset) % layers.Length;
                    RequiredEcologyLayerWork layer = layers[index];
                    if (layer == null || layer.iterators.Count == 0 ||
                        layer.lastAdvancedFrame == Time.frameCount ||
                        layer.pendingInstantiation != null || layer.pendingCustomYield != null ||
                        activeInstantiations >= MaximumConcurrentRequiredEcologyInstantiations)
                        continue;

                    // note: Advance each independent layer in a rotating order while retaining one canonical attempt sequence inside each layer.
                    LastSemanticChunkScatterStep = layer.label;
                    while (layer.iterators.Count > 0)
                    {
                        if (Time.realtimeSinceStartup - frameSliceStarted >= RequiredEcologyLayerSliceSeconds)
                            break;

                        IEnumerator current = layer.iterators.Peek();
                        if (!current.MoveNext())
                        {
                            layer.iterators.Pop();
                            (current as IDisposable)?.Dispose();
                            continue;
                        }

                        object yielded = current.Current;
                        if (yielded is IEnumerator nested)
                        {
                            if (nested != null)
                                layer.iterators.Push(nested);
                            continue;
                        }
                        if (yielded is AsyncInstantiateOperation<GameObject> instantiation)
                        {
                            layer.pendingInstantiation = instantiation;
                            activeInstantiations++;
                            break;
                        }
                        if (yielded is CustomYieldInstruction customYield)
                        {
                            layer.pendingCustomYield = customYield;
                            break;
                        }
                        if (yielded == null)
                        {
                            layer.lastAdvancedFrame = Time.frameCount;
                            break;
                        }

                        // note: Preserve uncommon Unity yield instructions exactly; the required streamed vegetation path normally yields only async instantiation and frame boundaries.
                        yield return yielded;
                        layer.lastAdvancedFrame = Time.frameCount;
                        break;
                    }
                }

                bool remainingWork = false;
                for (int index = 0; index < layers.Length; index++)
                    remainingWork |= layers[index] != null && layers[index].iterators.Count > 0;
                if (!remainingWork)
                    yield break;

                rotation = (rotation + 1) % layers.Length;
                yield return null;
            }
        }
        finally
        {
            // note: Cancel outstanding required-layer clones before the unpublished scatter root is discarded on owner replacement or unload.
            for (int index = 0; layers != null && index < layers.Length; index++)
            {
                RequiredEcologyLayerWork layer = layers[index];
                if (layer == null)
                    continue;
                if (layer.pendingInstantiation != null && !layer.pendingInstantiation.isDone)
                    layer.pendingInstantiation.Cancel();
                layer.pendingInstantiation = null;
                while (layer.iterators.Count > 0)
                    (layer.iterators.Pop() as IDisposable)?.Dispose();
            }
        }
    }

    public static IEnumerator BuildSemanticChunkScatterRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticChunkRecord chunk,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        float chunkWorldSize,
        Action<int> completed,
        Action<int> requiredEcologyCompleted = null,
        Action<string> requiredEcologyRejected = null)
    {
        if (parent == null || terrain == null || plan == null || chunk == null ||
            region == null || palette == null || registry == null || chunkWorldSize <= 0f)
        {
            // note: Preserve the failed prerequisite at the provider boundary instead of reducing every rejection to a negative object count.
            requiredEcologyRejected?.Invoke("required ecology has unavailable parent, terrain, plan, chunk, region, palette, registry, or cell bounds");
            requiredEcologyCompleted?.Invoke(-1);
            completed?.Invoke(-1);
            yield break;
        }

        // note: Each chunk owns one bounded scatter root so unloads can deactivate the complete physical bucket without touching terrain, roads, or authored sites.
        float half = chunkWorldSize * 0.5f;
        // note: Translate chunk-local indices through the centered authored grid so scatter bounds align with terrain and semantic cells.
        float centerX = YQContinuousWorldFeatureAuthority.WorldGridOrigin + chunk.chunkX * chunkWorldSize + half;
        float centerZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin + chunk.chunkZ * chunkWorldSize + half;
        LastSemanticChunkScatterStep = "bounds";
        Bounds bounds = new Bounds(
            new Vector3(centerX, terrain.transform.position.y, centerZ),
            new Vector3(chunkWorldSize, 10000f, chunkWorldSize));
        // note: Let the guarded streamer close the bounds setup slice before traversing the accepted route projection.
        yield return null;
        LastSemanticChunkScatterStep = "pathProjection";
        List<LivedPathSegment> sharedPaths = BuildLivedPathNetwork(
            plan,
            terrain,
            out YQPreparedSpatialMaterializationV2 sharedPreparedV2);
        LastSemanticChunkScatterStep = "pathProjection{" + LastSemanticPathProjectionBreakdown + "}";
        // note: Keep route projection work separate from hydrology preparation so one streamed cell cannot monopolize a loading frame with both full-plan passes.
        yield return null;
        // note: Reuse this cell's hash-verified V2 projection so water masks do not repeat the full accepted-artifact check after the route pass.
        LastSemanticChunkScatterStep = "waterProjection";
        MacroWaterSet sharedMacroWater = BuildMacroWaterSet(terrain, plan, sharedPreparedV2);
        yield return null;
        LastSemanticChunkScatterStep = "ecologyBudget";
        string seedScope = string.IsNullOrWhiteSpace(chunk.chunkId)
            ? plan.worldSeed + ":chunk:" + chunk.chunkX + ":" + chunk.chunkZ
            : chunk.chunkId;
        int spawned = 0;
        // note: Preserve a failed ecology layer as a hard publication failure instead of allowing later layers to hide its sentinel.
        bool ecologyFailed = false;
        // note: Optional dressing failures must not revoke the already-published camera-critical habitat contract.
        bool optionalEcologyFailed = false;
        string failedEcologyLayer = string.Empty;
        int canopySpawned = 0;
        int understorySpawned = 0;
        int shrubSpawned = 0;
        int groundcoverSpawned = 0;
        int boulderSpawned = 0;
        int deadfallSpawned = 0;
        int roadsideSpawned = 0;
        int shorelineSpawned = 0;

        // note: Read the accepted cell ecology once so every visible layer shares the same deterministic habitat and settlement-pressure decision.
        GeneratedSemanticCellPlanRecord semanticCell =
            YQGeneratedWorldQuery.GetSemanticCellPlan(
                plan,
                new Vector2Int(chunk.chunkX, chunk.chunkZ));
        bool hasSemanticEcology =
            semanticCell != null &&
            !string.IsNullOrWhiteSpace(semanticCell.semanticHash);
        int canopyTarget = 3;
        int understoryTarget = 5;
        int groundcoverTarget = 3;
        int boulderTarget = 3;
        int shrubTarget = 4;
        bool canopyApplicable = true;
        bool understoryApplicable = true;
        bool shrubApplicable = true;
        if (hasSemanticEcology)
        {
            // note: Keep budgets small and bounded; civilized cells quiet down while forest, wetland, grassland, ruggedness, and danger add their authored ecological layer.
            float forest = Mathf.Clamp01(semanticCell.ecologyForest);
            float grassland = Mathf.Clamp01(semanticCell.ecologyGrassland);
            float wetland = Mathf.Clamp01(semanticCell.ecologyWetland);
            float moisture = Mathf.Clamp01(semanticCell.moisture);
            float ruggedness = Mathf.Clamp01(semanticCell.ruggedness);
            float civilization = Mathf.Clamp01(semanticCell.civilizationPressure);
            float danger = Mathf.Clamp01(semanticCell.danger / 8f);
            // note: A quiet or highly civilized cell may legitimately omit a habitat layer; only accepted ecological intent makes that layer camera-critical.
            canopyApplicable = forest >= 0.18f || danger >= 0.18f;
            understoryApplicable = forest >= 0.12f || moisture >= 0.18f || wetland >= 0.12f;
            shrubApplicable = forest >= 0.12f || moisture >= 0.18f || wetland >= 0.12f || danger >= 0.18f;
            canopyTarget = Mathf.Clamp(
                Mathf.RoundToInt(4f + forest * 12f + danger * 2f - civilization * 3f),
                4,
                16);
            understoryTarget = Mathf.Clamp(
                Mathf.RoundToInt(6f + moisture * 8f + forest * 4f + wetland * 2f - civilization * 2f),
                4,
                18);
            groundcoverTarget = Mathf.Clamp(
                Mathf.RoundToInt(10f + grassland * 12f + wetland * 8f + moisture * 4f - civilization * 3f),
                8,
                26);
            boulderTarget = Mathf.Clamp(
                Mathf.RoundToInt(3f + ruggedness * 8f + danger - civilization * 2f),
                2,
                12);
            shrubTarget = Mathf.Clamp(
                Mathf.RoundToInt(4f + forest * 5f + moisture * 4f + wetland * 2f - civilization * 2f),
                3,
                14);
        }
        else
        {
            // note: Even a cell without a persisted ecology record receives a bounded authored baseline so missing metadata cannot present as an empty continuation chunk.
            canopyTarget = 8;
            understoryTarget = 8;
            groundcoverTarget = 12;
            boulderTarget = 5;
            shrubTarget = 5;
        }
        // note: An inapplicable habitat layer executes as a zero-budget no-op, so applicability controls both work and publication rather than only the final predicate.
        if (!canopyApplicable)
            canopyTarget = 0;
        if (!understoryApplicable)
            understoryTarget = 0;
        if (!shrubApplicable)
            shrubTarget = 0;

        int canopyMinimumSpawned = canopyApplicable ? 0 : 1;
        int understoryMinimumSpawned = understoryApplicable ? 0 : 1;
        int shrubMinimumSpawned = shrubApplicable ? 0 : 1;
        bool requiredEcologyReported = false;
        // note: The existing acceptance contract requires one valid placement per applicable core layer; signal that minimum early while the unchanged target counts continue as bounded optional dressing.
        Action PublishRequiredEcologyMinimum = () =>
        {
            if (requiredEcologyReported || canopyMinimumSpawned <= 0 ||
                understoryMinimumSpawned <= 0 || shrubMinimumSpawned <= 0)
                return;
            requiredEcologyReported = true;
            requiredEcologyCompleted?.Invoke(
                Mathf.Max(1, canopyMinimumSpawned + understoryMinimumSpawned + shrubMinimumSpawned));
        };
        PublishRequiredEcologyMinimum();

        // note: The canopy pass is tree-only and uses a distinct seed so replay and sector order cannot swap tree families.
        RequiredEcologyLayerWork canopyWork = new RequiredEcologyLayerWork(
            "canopy",
            SpawnSmallScatterAreaRoutine(
                parent, terrain, plan, region, palette, registry,
                new Vector3(centerX, 0f, centerZ),
                YQWorldAssetCatalog.SlotVegetation, canopyTarget,
                0f, half * 0.92f, SettlementClearRadius,
                OriginClearRadius, 16f,
                count =>
                {
                    canopySpawned = count;
                    if (count < 0)
                    {
                        ecologyFailed = true;
                        failedEcologyLayer = "Canopy";
                    }
                    else
                        spawned += count;
                },
                true, sharedPaths, bounds, seedScope + "|canopy", false, sharedMacroWater, "Canopy",
                count =>
                {
                    if (count > 0)
                    {
                        canopyMinimumSpawned = 1;
                        PublishRequiredEcologyMinimum();
                    }
                }));

        // note: Understory follows moisture and forest affinity while reusing the curated low-vegetation reference filter.
        RequiredEcologyLayerWork understoryWork = new RequiredEcologyLayerWork(
            "understory",
            SpawnSmallScatterAreaRoutine(
                parent, terrain, plan, region, palette, registry,
                new Vector3(centerX, 0f, centerZ),
                YQWorldAssetCatalog.SlotVegetation, understoryTarget,
                0f, half * 0.92f, SettlementClearRadius,
                OriginClearRadius, 16f,
                count =>
                {
                    understorySpawned = count;
                    if (count < 0)
                    {
                        ecologyFailed = true;
                        failedEcologyLayer = "Understory";
                    }
                    else
                        spawned += count;
                },
                false, sharedPaths, bounds, seedScope + "|understory", false, sharedMacroWater, "Understory",
                count =>
                {
                    if (count > 0)
                    {
                        understoryMinimumSpawned = 1;
                        PublishRequiredEcologyMinimum();
                    }
                }));

        // note: Shrubs are a separate deterministic layer so low vegetation remains visible even when the canopy family is sparse or rejected by a Terrain prototype.
        RequiredEcologyLayerWork shrubWork = new RequiredEcologyLayerWork(
            "shrubs",
            SpawnSmallScatterAreaRoutine(
                parent, terrain, plan, region, palette, registry,
                new Vector3(centerX, 0f, centerZ),
                YQWorldAssetCatalog.SlotVegetation, shrubTarget,
                0f, half * 0.92f, SettlementClearRadius,
                OriginClearRadius, 16f,
                count =>
                {
                    shrubSpawned = count;
                    if (count < 0)
                    {
                        ecologyFailed = true;
                        failedEcologyLayer = "Shrubs";
                    }
                    else
                        spawned += count;
                },
                false, sharedPaths, bounds, seedScope + "|shrubs", false, sharedMacroWater, "Shrubs",
                count =>
                {
                    if (count > 0)
                    {
                        shrubMinimumSpawned = 1;
                        PublishRequiredEcologyMinimum();
                    }
                }));

        // note: Publish the same required layers only after every deterministic per-layer iterator has completed successfully.
        yield return RunRequiredEcologyLayersRoutine(canopyWork, understoryWork, shrubWork);

        // note: Return the actual required-layer receipt before optional work; an applicable zero result is a failure too.
        LastSemanticChunkScatterStep = "requiredEcologyGate";
        bool requiredEcologyReady = !ecologyFailed &&
            (!canopyApplicable || canopySpawned > 0) &&
            (!understoryApplicable || understorySpawned > 0) &&
            (!shrubApplicable || shrubSpawned > 0);
        if (!requiredEcologyReady)
        {
            // note: A zero applicable layer is a deterministic placement rejection; carry its identity and actual counts through retries and terminal startup diagnostics.
            string rejection = "required ecology layer rejected at (" + chunk.chunkX + ", " + chunk.chunkZ + ")" +
                " region=" + region.regionId +
                " canopy=" + canopySpawned + "/" + canopyTarget + " applicable=" + canopyApplicable +
                " understory=" + understorySpawned + "/" + understoryTarget + " applicable=" + understoryApplicable +
                " shrubs=" + shrubSpawned + "/" + shrubTarget + " applicable=" + shrubApplicable +
                " failedLayer=" + failedEcologyLayer;
            requiredEcologyRejected?.Invoke(rejection);
            Debug.LogError("[YQGeneratedWorldEnvironment] " + rejection);
        }
        if (!requiredEcologyReported)
        {
            requiredEcologyReported = true;
            requiredEcologyCompleted?.Invoke(requiredEcologyReady
                ? Mathf.Max(1, canopySpawned + understorySpawned + shrubSpawned) : -1);
        }
        if (!requiredEcologyReady)
        {
            completed?.Invoke(-1);
            yield break;
        }

        // note: Groundcover receives its own deterministic budget so open grassland and wetland cells do not depend on canopy availability.
        LastSemanticChunkScatterStep = "groundcover";
        yield return SpawnSmallScatterAreaRoutine(
            parent, terrain, plan, region, palette, registry,
            new Vector3(centerX, 0f, centerZ),
            YQWorldAssetCatalog.SlotVegetation, groundcoverTarget,
            0f, half * 0.92f, SettlementClearRadius,
            OriginClearRadius, 16f,
            count =>
            {
                groundcoverSpawned = count;
                if (count < 0)
                {
                    optionalEcologyFailed = true;
                }
                else
                    spawned += count;
            },
            false, sharedPaths, bounds, seedScope + "|groundcover", false, sharedMacroWater, "Groundcover");

        // note: Boulders follow the accepted ruggedness/danger budget and retain the existing rock obstruction and water exclusions.
        LastSemanticChunkScatterStep = "boulders";
        yield return SpawnSmallScatterAreaRoutine(
            parent, terrain, plan, region, palette, registry,
            new Vector3(centerX, 0f, centerZ),
            YQWorldAssetCatalog.SlotRock, boulderTarget,
            0f, half * 0.92f, SettlementClearRadius,
            OriginClearRadius, 16f,
            count =>
            {
                boulderSpawned = count;
                if (count < 0)
                {
                    optionalEcologyFailed = true;
                }
                else
                    spawned += count;
            },
            false, sharedPaths, bounds, seedScope + "|boulders", false, sharedMacroWater, "Boulders");

        // note: Deadfall is a bounded physical floor layer and shares every route, water, site, and chunk-boundary exclusion with ordinary scatter.
        int deadfallTarget = Mathf.Clamp(Mathf.RoundToInt((understoryTarget + shrubTarget) * 0.28f), 2, 8);
        LastSemanticChunkScatterStep = "deadfall";
        yield return SpawnSmallScatterAreaRoutine(
            parent, terrain, plan, region, palette, registry,
            new Vector3(centerX, 0f, centerZ),
            YQWorldAssetCatalog.SlotFloorDeco, deadfallTarget,
            0f, half * 0.92f, SettlementClearRadius,
            OriginClearRadius, 16f,
            count =>
            {
                deadfallSpawned = count;
                if (count < 0)
                {
                    optionalEcologyFailed = true;
                }
                else
                    spawned += count;
            },
            false, sharedPaths, bounds, seedScope + "|deadfall", false, sharedMacroWater, "Deadfall");

        // note: Context dressing is clipped to this owner so roads and banks are physically present near the player without duplicating props in neighboring chunks.
        // note: Share this cell's accepted paths and water projection with optional dressing so each layer does not rehash the same V2 artifact.
        LastSemanticChunkScatterStep = "roadside";
        yield return BuildRoadsideDressingRoutine(
            parent, terrain, plan, registry, sharedPaths,
            count =>
            {
                roadsideSpawned = count;
                if (count < 0)
                {
                    optionalEcologyFailed = true;
                }
            }, bounds, sharedMacroWater);
        LastSemanticChunkScatterStep = "shoreline";
        yield return BuildShorelineDressingRoutine(
            parent, terrain, plan, registry,
            count =>
            {
                shorelineSpawned = count;
                if (count < 0)
                {
                    optionalEcologyFailed = true;
                }
            }, bounds, sharedMacroWater, sharedPaths);

        // note: Keep each physical ecology layer visible in the runtime trace so an empty chunk can be distinguished from a deliberately low-density biome.
        LastSemanticChunkScatterStep = "completionReceipt";
        Debug.Log("[YQGeneratedWorldEnvironment] STREAMED ECOLOGY " + chunk.chunkX + "," + chunk.chunkZ +
            " canopy=" + canopySpawned +
            " understory=" + understorySpawned +
            " shrubs=" + shrubSpawned +
            " groundcover=" + groundcoverSpawned +
            " boulders=" + boulderSpawned +
            " deadfall=" + deadfallSpawned +
            " roadside=" + roadsideSpawned +
            " shoreline=" + shorelineSpawned);
        // note: A required layer failure keeps decorativeComplete false so runtime diagnostics identify the first broken ecology layer.
        if (ecologyFailed)
        {
            Debug.LogError("[YQGeneratedWorldEnvironment] STREAMED ECOLOGY FAILED " +
                chunk.chunkX + "," + chunk.chunkZ + " layer=" + failedEcologyLayer);
            completed?.Invoke(-1);
            yield break;
        }

        if (optionalEcologyFailed)
            Debug.LogWarning("[YQGeneratedWorldEnvironment] OPTIONAL ECOLOGY DRESSING INCOMPLETE " +
                chunk.chunkX + "," + chunk.chunkZ);

        completed?.Invoke(spawned);
    }

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
        List<LivedPathSegment> originScatterPaths =
            BuildLivedPathNetwork(plan, terrain);

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
            28,
            44f,
            126f,
            16f,
            36f,
            18f,
            count => treesSpawned = count,
            true,
            originScatterPaths);

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
            96,
            32f,
            112f,
            14f,
            28f,
            16f,
            count => vegetationSpawned = count,
            false,
            originScatterPaths);

        yield return SpawnSmallScatterAreaRoutine(
            root.transform,
            terrain,
            plan,
            nearestRegion,
            nearestPalette,
            registry,
            originAnchor,
            YQWorldAssetCatalog.SlotRock,
            32,
            34f,
            118f,
            14f,
            30f,
            16f,
            count => rocksSpawned = count,
            false,
            originScatterPaths);

        // note: The Goddess receives a deliberate stage composition; broad wilderness scatter remains outside this inlay and cannot randomly throw props around the focal landmark.
        Vector3 goddessInlayCenter = originAnchor + new Vector3(30.6f, 0f, 14.6f);
        int inlayVegetation = 0;
        int inlayRocks = 0;
        int inlayDecor = 0;
        yield return BuildCuratedGoddessStageRoutine(
            root.transform,
            terrain,
            plan,
            nearestPalette,
            registry,
            originAnchor,
            goddessInlayCenter,
            (vegetation, rocks, decor) =>
            {
                inlayVegetation = vegetation;
                inlayRocks = rocks;
                inlayDecor = decor;
            });
        vegetationSpawned += inlayVegetation;
        rocksSpawned += inlayRocks;

        Debug.Log(
            "[YQGeneratedWorldEnvironment] ORIGIN APPROACH DRESSED\n" +
            "Palette region: " + nearestRegion.displayName + "\n" +
            "Visible trees: " + treesSpawned + "/32\n" +
            "Vegetation: " + vegetationSpawned + "/96\n" +
            "Rock outcrops: " + rocksSpawned + "/32\n" +
            "Curated Goddess-stage decor: " + inlayDecor);

        completed?.Invoke(treesSpawned, vegetationSpawned, rocksSpawned);
    }

    private static IEnumerator BuildCuratedGoddessStageRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        Vector3 approachOrigin,
        Vector3 stageCenter,
        Action<int, int, int> completed)
    {
        Vector3 forward = stageCenter - approachOrigin;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        forward.Normalize();
        Vector3 side = new Vector3(-forward.z, 0f, forward.x);

        Vector2[] rockLayout =
        {
            new Vector2(-11f, 5f), new Vector2(11f, 5f),
            new Vector2(-15f, 10f), new Vector2(15f, 10f),
            new Vector2(-9f, 15f), new Vector2(9f, 15f),
            new Vector2(0f, 18f)
        };
        Vector2[] vegetationLayout =
        {
            new Vector2(-18f, 5f), new Vector2(18f, 5f),
            new Vector2(-20f, 12f), new Vector2(20f, 12f),
            new Vector2(-15f, 19f), new Vector2(15f, 19f),
            new Vector2(-7f, 22f), new Vector2(7f, 22f),
            new Vector2(-8f, -3f), new Vector2(8f, -3f)
        };
        Vector2[] decorLayout =
        {
            new Vector2(-7f, 7f), new Vector2(7f, 7f),
            new Vector2(-12f, 14f), new Vector2(12f, 14f)
        };

        int rocks = 0;
        int vegetation = 0;
        int decor = 0;
        yield return SpawnCuratedStageLayoutRoutine(
            parent, terrain, plan, registry, stageCenter, side, forward,
            rockLayout, YQWorldAssetCatalog.SlotRock,
            BuildSmallScatterReferences(
                palette, YQWorldAssetCatalog.SlotRock, false),
            count => rocks = count);
        yield return SpawnCuratedStageLayoutRoutine(
            parent, terrain, plan, registry, stageCenter, side, forward,
            vegetationLayout, YQWorldAssetCatalog.SlotVegetation,
            BuildSmallScatterReferences(
                palette, YQWorldAssetCatalog.SlotVegetation, false),
            count => vegetation = count);
        yield return SpawnCuratedStageLayoutRoutine(
            parent, terrain, plan, registry, stageCenter, side, forward,
            decorLayout, "origin_stage_decor",
            BuildCuratedStageDecorReferences(palette),
            count => decor = count);
        completed?.Invoke(vegetation, rocks, decor);
    }

    private static List<GeneratedAssetReferenceRecord>
        BuildCuratedStageDecorReferences(
            GeneratedRegionAssetPaletteRecord palette)
    {
        List<GeneratedAssetReferenceRecord> results =
            new List<GeneratedAssetReferenceRecord>();
        if (palette == null)
            return results;

        palette.EnsureCollections();
        List<GeneratedAssetReferenceRecord>[] sources =
        {
            palette.exteriorDeco,
            palette.floorDeco,
            palette.lighting
        };
        HashSet<string> paths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            List<GeneratedAssetReferenceRecord> source = sources[sourceIndex];
            for (int index = 0; source != null && index < source.Count; index++)
            {
                GeneratedAssetReferenceRecord reference = source[index];
                if (reference == null ||
                    string.IsNullOrWhiteSpace(reference.assetPath) ||
                    reference.footprintX > 14f ||
                    reference.footprintZ > 14f ||
                    !paths.Add(reference.assetPath))
                {
                    continue;
                }

                string semantic = BuildReferenceSemanticText(reference);
                if (ContainsAnySemantic(
                        semantic,
                        "shrine", "ruin", "column", "arch", "altar",
                        "torch", "brazier", "banner", "statue"))
                {
                    // note: Large generic stone/structure references are reserved for the reviewed rock layout; summit decor must be an explicit shrine prop.
                    results.Add(reference);
                }
            }
        }

        return results;
    }

    private static IEnumerator SpawnCuratedStageLayoutRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        Vector3 center,
        Vector3 side,
        Vector3 forward,
        IReadOnlyList<Vector2> layout,
        string slot,
        List<GeneratedAssetReferenceRecord> references,
        Action<int> completed)
    {
        int spawned = 0;
        if (references == null || references.Count == 0)
        {
            completed?.Invoke(0);
            yield break;
        }

        for (int index = 0; layout != null && index < layout.Count; index++)
        {
            int referenceIndex = (int)(StableHash32(
                plan.worldSeed + "|goddess_stage|" + slot + "|" + index) %
                (uint)references.Count);
            GeneratedAssetReferenceRecord reference = references[referenceIndex];
            GameObject prefab = registry.ResolvePrefab(reference.assetPath);
            if (prefab == null)
                continue;

            AsyncInstantiateOperation<GameObject> operation =
                UnityEngine.Object.InstantiateAsync(prefab, parent);
            operation.priority = -1;
            yield return operation;
            GameObject instance = operation.Result != null &&
                operation.Result.Length > 0 ? operation.Result[0] : null;
            if (instance == null)
                continue;

            Vector2 offset = layout[index];
            Vector3 position = center + side * offset.x + forward * offset.y;
            position.y = YQGeneratedWorldTerrain.SampleWorldHeight(
                terrain, position);
            instance.name = "GoddessStage_" + SafeName(slot) + "_" + index;
            instance.transform.position = position;
            Vector3 look = center - position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                instance.transform.rotation = Quaternion.LookRotation(
                    look.normalized, Vector3.up);
            instance.transform.localScale *= Mathf.Lerp(
                Mathf.Max(0.01f, reference.scaleMin),
                Mathf.Max(reference.scaleMin, reference.scaleMax),
                Deterministic01(plan.worldSeed +
                    "|goddess_stage_scale|" + slot + "|" + index));

            registry.ApplyMaterialOverrides(reference.assetPath, instance);
            PrepareWildernessInstance(instance);
            yield return YQRuntimeUrpMaterialRepair
                .RepairMaterialHierarchyRoutine(instance, null);
            if (!FinalizeSmallWildernessInstance(
                    instance, terrain, slot, reference) ||
                !TryGetWildernessBounds(instance, out Bounds bounds) ||
                Mathf.Max(bounds.size.x, bounds.size.z) > 18f ||
                bounds.size.y > 28f)
            {
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                continue;
            }

            if (string.Equals(slot, YQWorldAssetCatalog.SlotRock, StringComparison.OrdinalIgnoreCase))
                MakeStageDecorationNonBlocking(instance);

            spawned++;
            // note: Each authored stage element crosses its own frame boundary, preserving the loading animation and preventing the curated landmark from becoming a spike source.
            yield return null;
        }

        completed?.Invoke(spawned);
    }

    private static void MakeStageDecorationNonBlocking(GameObject instance)
    {
        if (instance == null)
            return;
        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int index = 0; index < colliders.Length; index++)
        {
            if (colliders[index] == null)
                continue;
            // note: Curated stage rocks frame the approach visually; trigger-only collision keeps them from blocking the mandatory route capsule.
            colliders[index].isTrigger = true;
        }
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

        YQPreparedSpatialMaterializationV2 preparedEcologyV2 = null;
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) &&
            !YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out preparedEcologyV2,
                out string projectionFailure))
        {
            // note: Preserve the last valid wilderness hierarchy when the authoritative V2 ecology projection is unavailable.
            Debug.LogError(
                "[YQGeneratedWorldEnvironment] V2 wilderness projection rejected: " +
                projectionFailure);
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

        uint wildernessSeedHash =
            StableHash32(
                YQGeneratedWorldTerrain.TerrainGenerationVersion +
                "|" +
                SafeText(
                    plan.worldSeed,
                    "yourquest_default_world"));
        YQGeneratedWorldTilePlan wildernessTilePlan = null;
        if (preparedEcologyV2 == null)
        {
            wildernessTilePlan = new YQGeneratedWorldTilePlan(
                wildernessSeedHash,
                YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan));
        }
        List<LivedPathSegment> regionalScatterPaths =
            BuildLivedPathNetwork(plan, terrain);

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

            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Curating the wilderness",
                5,
                8,
                "Composing region " + (regionIndex + 1) + " of " +
                plan.regions.Count + ": " + region.displayName,
                Mathf.Lerp(
                    0.82f,
                    0.85f,
                    plan.regions.Count > 0
                        ? regionIndex / (float)plan.regions.Count
                        : 1f));

            GameObject regionRoot =
                new GameObject(
                    "Wilderness__" +
                    SafeName(
                        region.displayName));

            regionRoot.transform.SetParent(
                wildernessRoot.transform,
                false);

            // note: Explicit composition layers keep near-field clutter, readable travel space, and distant silhouettes separate in the generated hierarchy.
            Transform foregroundLayer = new GameObject("Foreground_Composition").transform;
            foregroundLayer.SetParent(regionRoot.transform, false);
            Transform midgroundLayer = new GameObject("Midground_Composition").transform;
            midgroundLayer.SetParent(regionRoot.transform, false);
            Transform backgroundLayer = new GameObject("Background_Composition").transform;
            backgroundLayer.SetParent(regionRoot.transform, false);

            Vector3 regionCenter =
                YQGeneratedWorldLayout
                    .GetRegionCenter(
                        plan,
                        region,
                        terrain);

            YQGeneratedWorldTileProfile tileProfile =
                SampleVegetationEcology(
                    wildernessTilePlan,
                    preparedEcologyV2,
                    regionCenter.x,
                    regionCenter.z,
                    out _,
                    out _);

            int vegetationTarget =
                Mathf.Clamp(
                    Mathf.RoundToInt(
                        ResolveVegetationTarget(
                            region,
                            palette) *
                        ResolveBiomeScatterMultiplier(
                            tileProfile,
                            false)),
                    128,
                    300);

            int visibleTreeTarget =
                ResolveVisibleTreeTarget(
                    vegetationTarget,
                    palette);

            int rockTarget =
                Mathf.Clamp(
                    Mathf.RoundToInt(
                        ResolveRockTarget(
                            region,
                            palette) *
                        ResolveBiomeScatterMultiplier(
                            tileProfile,
                            true)),
                    20,
                    60);

            // note: Visible trees, ground vegetation, and rocks are now generated by the player-following semantic chunk streamer; keeping these region roots as stable owners preserves the existing hierarchy contract.
            int visibleTreesSpawned = 0;
            int vegetationSpawned = 0;
            int rocksSpawned = 0;

            int caves = 0;
            int ambientEnemies = 0;
            int treasure = 0;
            if (preparedEcologyV2 == null)
            {
                // note: Legacy regional features remain available for non-V2 worlds; V2 sites must wait for their streamed physical owner.
                yield return BuildRegionalCavesRoutine(
                    midgroundLayer,
                    terrain,
                    plan,
                    region,
                    palette,
                    registry,
                    regionCenter,
                    count => caves = count);
                yield return null;
                yield return BuildRegionalAmbientEncountersRoutine(
                    midgroundLayer,
                    terrain,
                    plan,
                    region,
                    registry,
                    regionCenter,
                    count => ambientEnemies = count);
                yield return null;
                yield return BuildRegionalTreasureRoutine(
                    foregroundLayer,
                    terrain,
                    plan,
                    region,
                    palette,
                    registry,
                    regionCenter,
                    count => treasure = count);
                yield return null;
            }

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

            // note: Each complete regional composition is independently useful progress; a large world must not trip the stall watchdog while later regions are still being curated.
            YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
        }

        int roadsideDressing = 0;
        int shorelineDressing = 0;
        if (preparedEcologyV2 == null)
        {
            // note: V2 roadside dressing is owned by streamed chunks; the legacy path keeps its original whole-world composition.
            yield return BuildRoadsideDressingRoutine(
                wildernessRoot.transform,
                terrain,
                plan,
                registry,
                regionalScatterPaths,
                count => roadsideDressing = count);

            yield return BuildShorelineDressingRoutine(
                wildernessRoot.transform,
                terrain,
                plan,
                registry,
                count => shorelineDressing = count);
        }
        stats.roadsideDressing = roadsideDressing;
        stats.shorelineDressing = shorelineDressing;

        completed?.Invoke(stats);
    }

    private static IEnumerator BuildRoadsideDressingRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        List<LivedPathSegment> paths,
        Action<int> completed,
        Bounds? placementBounds = null,
        MacroWaterSet sharedMacroWater = null)
    {
        LastRoadsideDressingStep = "validate";
        if (parent == null || terrain == null || plan == null || registry == null || paths == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        LastRoadsideDressingStep = "rootCreation";
        GameObject rootObject = new GameObject("Generated_RoadsideDressing");
        rootObject.transform.SetParent(parent, false);
        // note: Keep streamed roadside checks on the parent cell's accepted water projection; standalone callers still resolve their own.
        LastRoadsideDressingStep = "waterProjection";
        MacroWaterSet macroWater = sharedMacroWater ?? BuildMacroWaterSet(terrain, plan);
        int spawned = 0;
        int candidateBudget = Mathf.Min(paths.Count * 2, 48);
        // note: Palette contents are fixed during one chunk's dressing pass; cache each deterministic candidate list instead of rebuilding garbage for every roadside point.
        var candidatesByPalette = new Dictionary<GeneratedRegionAssetPaletteRecord, List<GeneratedAssetReferenceRecord>>();
        float frameStartedAt = Time.realtimeSinceStartup;

        for (int pathIndex = 0; pathIndex < paths.Count && spawned < candidateBudget; pathIndex++)
        {
            LivedPathSegment path = paths[pathIndex];
            if (path == null)
                continue;
            Vector2 direction = path.end - path.start;
            if (direction.sqrMagnitude < 16f)
                continue;
            for (int side = 0; side < 2 && spawned < candidateBudget; side++)
            {
                // note: Failed candidates consume time too; bound work even when every roadside location is reserved.
                if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
                {
                    LastRoadsideDressingStep = "candidateBudgetYieldAfter:" + LastRoadsideDressingStep;
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
                LastRoadsideDressingStep = "candidateGeometry";
                string seed = SafeText(plan.worldSeed, "world") + "|roadside|" + pathIndex + "|" + side;
                float along = Mathf.Lerp(0.28f, 0.72f, Deterministic01(seed + "|along"));
                float offset = path.halfWidth + path.shoulderWidth + 2.4f + Deterministic01(seed + "|offset") * 3.2f;
                Vector2 point = ResolveRoadsideDressingPoint(path, along, offset * (side == 0 ? 1f : -1f));
                Vector3 worldPosition = new Vector3(point.x, 0f, point.y);
                if (placementBounds.HasValue && !ContainsXZ(placementBounds.Value, worldPosition))
                    continue;

                LastRoadsideDressingStep = "regionPalette";
                GeneratedRegionRecord region = FindNearestRegion(plan, worldPosition, macroWater.preparedV2);
                GeneratedRegionAssetPaletteRecord palette = FindPalette(plan, region);
                if (palette == null)
                    continue;
                if (!candidatesByPalette.TryGetValue(
                        palette,
                        out List<GeneratedAssetReferenceRecord> candidates))
                {
                    candidates = new List<GeneratedAssetReferenceRecord>();
                    AddRoadsideCandidates(candidates, palette.exteriorDeco);
                    AddRoadsideCandidates(candidates, palette.floorDeco);
                    AddRoadsideCandidates(candidates, palette.wallDeco);
                    AddRoadsideCandidates(candidates, palette.rock);
                    AddRoadsideCandidates(candidates, palette.lighting);
                    AddRoadsideCandidates(candidates, palette.vegetation);
                    candidatesByPalette.Add(palette, candidates);
                }
                GeneratedAssetReferenceRecord reference = PickRoadsideReference(candidates, seed);
                if (reference == null)
                    continue;
                LastRoadsideDressingStep = "contextPointRules";
                if (!IsContextDressingPointAllowed(terrain, plan, paths, macroWater, worldPosition))
                    continue;

                LastRoadsideDressingStep = "prefabResolve";
                GameObject prefab = registry.ResolvePrefab(reference.assetPath);
                if (prefab == null || IsLargeTerrainFeatureReference(reference))
                    continue;

                LastRoadsideDressingStep = "asyncInstantiate";
                AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(prefab, rootObject.transform);
                operation.priority = -1;
                yield return operation;
                GameObject instance = operation.Result != null && operation.Result.Length > 0 ? operation.Result[0] : null;
                if (instance == null)
                    continue;

                LastRoadsideDressingStep = "instanceSetup";
                instance.name = "Roadside_" + pathIndex + "_" + side + "__" + prefab.name;
                worldPosition.y = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, worldPosition);
                instance.transform.SetPositionAndRotation(worldPosition, Quaternion.Euler(0f, Deterministic01(seed + "|yaw") * 360f, 0f));
                float scale = Mathf.Lerp(Mathf.Max(0.01f, reference.scaleMin), Mathf.Max(reference.scaleMin, reference.scaleMax), Deterministic01(seed + "|scale"));
                scale = ResolveAuditedScatterScale(prefab, reference.slotTag, reference, scale);
                instance.transform.localScale *= scale;
                FitInstantiatedScatterToBudget(instance, reference.slotTag, reference);
                LastRoadsideDressingStep = "materialOverrides";
                registry.ApplyMaterialOverrides(reference.assetPath, instance);
                PrepareWildernessInstance(instance);
                LastRoadsideDressingStep = "materialRepair";
                yield return YQRuntimeUrpMaterialRepair.RepairMaterialHierarchyRoutine(instance, null);
                LastRoadsideDressingStep = "footprintValidation";
                if (!FinalizeSmallWildernessInstance(instance, terrain, reference.slotTag, reference) ||
                    !IsContextDressingFootprintAllowed(instance, terrain, plan, paths, macroWater))
                {
                    instance.SetActive(false);
                    UnityEngine.Object.Destroy(instance);
                    continue;
                }

                spawned++;
                yield return null;
            }
        }

        LastRoadsideDressingStep = "completion";
        completed?.Invoke(spawned);
    }

    private static Vector2 ResolveRoadsideDressingPoint(LivedPathSegment path, float along, float offset)
    {
        // note: Derive the verge from the same curved centre line used by the road renderer and terrain painting.
        Vector2 tangent = ResolveLivedPathCenter(path, Mathf.Min(1f, along + 0.01f)) -
                          ResolveLivedPathCenter(path, Mathf.Max(0f, along - 0.01f));
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = path.end - path.start;
        tangent.Normalize();
        return ResolveLivedPathCenter(path, along) + new Vector2(-tangent.y, tangent.x) * offset;
    }

    private static bool ContainsXZ(Bounds bounds, Vector3 position)
    {
        // note: XZ-only bounds checks keep chunk ownership independent of terrain elevation while still preventing cross-cell duplicate dressing.
        return position.x >= bounds.min.x && position.x < bounds.max.x &&
               position.z >= bounds.min.z && position.z < bounds.max.z;
    }

    private static bool IsContextDressingPointAllowed(
        Terrain terrain, GeneratedWorldPlanRecord plan, List<LivedPathSegment> paths,
        MacroWaterSet water, Vector3 position)
    {
        // note: Contextual props obey the same accepted reservations as ecological scatter, including roads, sites and cave mouths.
        if (!IsWildernessPositionAllowed(terrain, plan, position, SettlementClearRadius,
                OriginClearRadius, EncampmentEncounterClearRadius, water,
                out YQSpatialTerrainSampleV2 preparedSample) ||
            IsNearLivedPath(paths, position, 2f))
            return false;
        // note: The accepted V2 water check already sampled this exact point; reuse its complete mask result instead of evaluating every route and water spline twice.
        if (water.preparedV2 != null && !IsPreparedVegetationPositionAllowed(preparedSample, false))
            return false;
        Vector3 local = position - terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        return terrain.terrainData.GetSteepness(local.x / size.x, local.z / size.z) <= 32f;
    }

    private static bool IsContextDressingFootprintAllowed(
        GameObject instance, Terrain terrain, GeneratedWorldPlanRecord plan,
        List<LivedPathSegment> paths, MacroWaterSet water)
    {
        if (!TryGetWildernessBounds(instance, out Bounds bounds))
            return false;

        // note: A safe pivot does not imply a safe prop; conservatively keep its whole footprint clear of every route and compiled site.
        float radius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
        if (IsNearLivedPath(paths, bounds.center, radius + 2f))
            return false;
        for (int index = 0; water.preparedV2 != null && index < water.preparedV2.SiteCount; index++)
        {
            YQSpatialMaterializationSiteV2 site = water.preparedV2.GetSite(index);
            Vector2 distance = new Vector2(bounds.center.x - site.x, bounds.center.z - site.z);
            if (distance.magnitude <= site.reservedRadius + radius + 2f)
                return false;
        }

        // note: Check corners and edge contacts against the final Terrain, preventing shoreline props from spanning a bank drop or construction cut.
        float lowest = float.PositiveInfinity;
        float highest = float.NegativeInfinity;
        for (int z = -1; z <= 1; z++)
        {
            for (int x = -1; x <= 1; x++)
            {
                Vector3 contact = bounds.center + new Vector3(bounds.extents.x * x, 0f, bounds.extents.z * z);
                if (!IsContextDressingPointAllowed(terrain, plan, paths, water, contact))
                    return false;
                float height = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, contact);
                lowest = Mathf.Min(lowest, height);
                highest = Mathf.Max(highest, height);
            }
        }
        return highest - lowest <= 1.2f;
    }

    private static GeneratedRegionRecord FindNearestRegion(
        GeneratedWorldPlanRecord plan,
        Vector3 worldPosition,
        YQPreparedSpatialMaterializationV2 prepared = null)
    {
        GeneratedRegionRecord nearest = null;
        float nearestDistance = float.MaxValue;
        if (plan == null || plan.regions == null)
            return null;
        for (int index = 0; index < plan.regions.Count; index++)
        {
            GeneratedRegionRecord region = plan.regions[index];
            if (region == null)
                continue;
            Vector3 center;
            if (prepared != null && prepared.TryGetRegion(
                    region.regionId,
                    out YQSpatialMaterializationRegionV2 domain))
            {
                // note: Reuse the already accepted immutable region datum instead of rehashing the full V2 artifact for every dressing candidate.
                center = new Vector3(domain.centerX, 0f, domain.centerZ);
            }
            else
            {
                center = YQGeneratedWorldLayout.GetRegionCenter(plan, region, null);
            }
            float distance = (center - worldPosition).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = region;
            }
        }
        return nearest;
    }

    private static GeneratedAssetReferenceRecord PickRoadsideReference(
        List<GeneratedAssetReferenceRecord> candidates,
        string seed)
    {
        if (candidates == null || candidates.Count == 0)
            return null;
        int index = Mathf.Clamp(Mathf.FloorToInt(Deterministic01(seed + "|reference") * candidates.Count), 0, candidates.Count - 1);
        return candidates[index];
    }

    private static void AddRoadsideCandidates(List<GeneratedAssetReferenceRecord> destination, List<GeneratedAssetReferenceRecord> source)
    {
        if (destination == null || source == null)
            return;
        for (int index = 0; index < source.Count; index++)
        {
            GeneratedAssetReferenceRecord reference = source[index];
            if (reference == null || IsLargeTerrainFeatureReference(reference))
                continue;
            string semantic = BuildReferenceSemanticText(reference);
            // note: A generic road tag can select an entire road mesh; dressing must be an actual small contextual prop.
            if (ContainsAnySemantic(semantic, "fence", "post", "sign", "marker", "bench", "stone", "rock", "bush", "shrub", "lantern", "torch"))
                destination.Add(reference);
        }
    }

    private static IEnumerator BuildShorelineDressingRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        Action<int> completed,
        Bounds? placementBounds = null,
        MacroWaterSet sharedMacroWater = null,
        List<LivedPathSegment> sharedPaths = null)
    {
        if (parent == null || terrain == null || plan == null || registry == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        float frameStartedAt = Time.realtimeSinceStartup;
        // note: Shoreline dressing consumes the same persisted semantic palettes as the rest of the world; ensure missing palette records are repaired before candidate selection.
        LastShorelineDressingStep = "paletteRepair";
        YQWorldAssetCatalog.EnsureAssetPalettes(plan);
        if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
        {
            yield return null;
            frameStartedAt = Time.realtimeSinceStartup;
        }
        LastShorelineDressingStep = "macroWaterResolve";
        // note: Reuse the parent cell's projection for shoreline geometry and road exclusion instead of revalidating the accepted plan.
        MacroWaterSet macroWater = sharedMacroWater ?? BuildMacroWaterSet(terrain, plan);
        List<Vector3> shorelinePoints = new List<Vector3>();
        if (macroWater.preparedV2 != null)
        {
            for (int waterIndex = 0; waterIndex < macroWater.preparedV2.WaterCount; waterIndex++)
            {
                LastShorelineDressingStep = "waterFeature:" + waterIndex;
                YQSpatialMaterializationWaterV2 water = macroWater.preparedV2.GetWater(waterIndex);
                // note: Lakes use the same fitted ellipse as their rendered surface; waterfalls do not have plantable side banks.
                if (water.kind == YQHydrologyKindV2.Lake || water.kind == YQHydrologyKindV2.Wetland)
                {
                    LastShorelineDressingStep = "areaWaterCandidates:" + waterIndex;
                    if (TryBuildPreparedAreaWaterBasin(terrain, macroWater.preparedV2, waterIndex, out var basin))
                        AppendAreaShorelineCandidates(basin, SafeText(plan.worldSeed, "world") + "|shore|" + waterIndex, shorelinePoints);
                    if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
                    {
                        yield return null;
                        frameStartedAt = Time.realtimeSinceStartup;
                    }
                    continue;
                }
                if (water.kind == YQHydrologyKindV2.Waterfall)
                    continue;
                int pointCount = macroWater.preparedV2.GetWaterPointCount(waterIndex);
                int stride = Mathf.Max(1, Mathf.CeilToInt(pointCount / 12f));
                for (int pointIndex = 0; pointIndex < pointCount; pointIndex += stride)
                {
                    LastShorelineDressingStep = "riverBankCandidates:" + waterIndex + ":" + pointIndex;
                    YQSpatialMaterializationWaterPointV2 point = macroWater.preparedV2.GetWaterPoint(waterIndex, pointIndex);
                    YQSpatialMaterializationWaterPointV2 next = macroWater.preparedV2.GetWaterPoint(waterIndex, Mathf.Min(pointCount - 1, pointIndex + 1));
                    YQSpatialMaterializationWaterPointV2 previous = macroWater.preparedV2.GetWaterPoint(waterIndex, Mathf.Max(0, pointIndex - 1));
                    Vector2 tangent = new Vector2(next.x - previous.x, next.z - previous.z);
                    if (tangent.sqrMagnitude < 0.01f)
                        continue;
                    tangent.Normalize();
                    Vector2 normal = new Vector2(-tangent.y, tangent.x);
                    // note: Keep wetland dressing beyond the compiled bank mask so a broad river shoulder cannot reject every shoreline candidate as submerged.
                    float bankPadding = YQGeneratedWorldTerrain.WorldSize / (YQGeneratedWorldTerrain.HeightmapResolution - 1f) * 1.414214f;
                    float radius = Mathf.Max(1.5f, Mathf.Max(water.nominalWidth, point.width) * 0.5f);
                    float offset = radius + bankPadding + Mathf.Max(8f, radius * 0.6f);
                    shorelinePoints.Add(new Vector3(point.x + normal.x * offset, 0f, point.z + normal.y * offset));
                    shorelinePoints.Add(new Vector3(point.x - normal.x * offset, 0f, point.z - normal.y * offset));
                    if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
                    {
                        yield return null;
                        frameStartedAt = Time.realtimeSinceStartup;
                    }
                }
            }
        }
        else
        {
            for (int basinIndex = 0; basinIndex < macroWater.count; basinIndex++)
            {
                LastShorelineDressingStep = "legacyAreaCandidates:" + basinIndex;
                YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin = macroWater.basins[basinIndex];
                AppendAreaShorelineCandidates(basin, SafeText(plan.worldSeed, "world") + "|shore|" + basinIndex, shorelinePoints);
                if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
                {
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
            }
        }

        LastShorelineDressingStep = "rootCreation";
        GameObject rootObject = new GameObject("Generated_ShorelineDressing");
        rootObject.transform.SetParent(parent, false);
        int spawned = 0;
        int allowedCandidates = 0;
        int paletteRejected = 0;
        int prefabRejected = 0;
        int candidateBudget = Mathf.Min(shorelinePoints.Count, 64);
        // note: Reuse each immutable regional plant candidate set across shoreline points so selection stays deterministic without per-point list allocations.
        var candidatesByPalette = new Dictionary<GeneratedRegionAssetPaletteRecord, List<GeneratedAssetReferenceRecord>>();
        LastShorelineDressingStep = "livedPathNetwork";
        List<LivedPathSegment> paths = sharedPaths ??
            BuildLivedPathNetwork(plan, terrain, macroWater.preparedV2);
        if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
        {
            yield return null;
            frameStartedAt = Time.realtimeSinceStartup;
        }
        for (int index = 0; index < candidateBudget; index++)
        {
            LastShorelineDressingStep = "candidate:" + index;
            // note: Spread the finite budget over all water bodies, including later lakes, rather than truncating the first river's candidate list.
            if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
            Vector3 point = shorelinePoints[index * shorelinePoints.Count / candidateBudget];
            if (placementBounds.HasValue && !ContainsXZ(placementBounds.Value, point))
                continue;
            if (!IsShorelineDressingPointAllowed(terrain, plan, paths, macroWater, point))
                continue;
            allowedCandidates++;
            GeneratedRegionRecord region = FindNearestRegion(plan, point, macroWater.preparedV2);
            GeneratedRegionAssetPaletteRecord palette = FindPalette(plan, region);
            if (palette == null)
            {
                paletteRejected++;
                continue;
            }
            if (!candidatesByPalette.TryGetValue(
                    palette,
                    out List<GeneratedAssetReferenceRecord> candidates))
            {
                candidates = new List<GeneratedAssetReferenceRecord>();
                AddShorelineCandidates(candidates, palette.vegetation);
                candidatesByPalette.Add(palette, candidates);
            }
            GeneratedAssetReferenceRecord reference = PickShorelineReference(
                candidates,
                SafeText(plan.worldSeed, "world") + "|shoreline|" + index);
            if (reference == null)
            {
                paletteRejected++;
                continue;
            }
            GameObject prefab = registry.ResolvePrefab(reference.assetPath);
            if (prefab == null || IsLargeTerrainFeatureReference(reference))
            {
                prefabRejected++;
                continue;
            }

            AsyncInstantiateOperation<GameObject> operation = UnityEngine.Object.InstantiateAsync(prefab, rootObject.transform);
            operation.priority = -1;
            yield return operation;
            GameObject instance = operation.Result != null && operation.Result.Length > 0 ? operation.Result[0] : null;
            if (instance == null)
                continue;
            string seed = SafeText(plan.worldSeed, "world") + "|shoreline|" + index;
            LastShorelineDressingStep = "candidateMaterialization:" + index;
            instance.name = "Shoreline_" + index + "__" + prefab.name;
            point.y = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, point);
            instance.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, Deterministic01(seed + "|yaw") * 360f, 0f));
            float scale = Mathf.Lerp(Mathf.Max(0.01f, reference.scaleMin), Mathf.Max(reference.scaleMin, reference.scaleMax), Deterministic01(seed + "|scale"));
            scale = ResolveAuditedScatterScale(prefab, reference.slotTag, reference, scale);
            instance.transform.localScale *= scale;
            FitInstantiatedScatterToBudget(instance, reference.slotTag, reference);
            registry.ApplyMaterialOverrides(reference.assetPath, instance);
            PrepareWildernessInstance(instance);
            yield return YQRuntimeUrpMaterialRepair.RepairMaterialHierarchyRoutine(instance, null);
            if (!FinalizeSmallWildernessInstance(instance, terrain, reference.slotTag, reference) ||
                !IsShorelineDressingFootprintAllowed(instance, terrain, plan, paths, macroWater))
            {
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                continue;
            }
            spawned++;
            yield return null;
        }
        LastShorelineDressingStep = "report";
        // note: Keep shoreline rejection counts visible so a future seed can distinguish missing assets from over-conservative placement masks.
        Debug.Log("[YQGeneratedWorldEnvironment] SHORELINE DRESSING: candidates=" + shorelinePoints.Count +
            ", budget=" + candidateBudget + ", allowed=" + allowedCandidates + ", paletteRejected=" + paletteRejected +
            ", prefabRejected=" + prefabRejected + ", spawned=" + spawned);
        completed?.Invoke(spawned);
    }

    private static bool IsShorelineDressingPointAllowed(
        Terrain terrain, GeneratedWorldPlanRecord plan, List<LivedPathSegment> paths,
        MacroWaterSet water, Vector3 position)
    {
        // note: Shoreline vegetation may sit inside a settlement's broad clearance radius to create a believable town-to-water transition, but it still cannot occupy water, roads, origin reserve, steep ground, or authored construction masks.
        if (!InsideTerrainWithMargin(terrain, position, 3f) ||
            InsideOriginReserve(position, OriginClearRadius) ||
            IsNearLivedPath(paths, position, 2.25f) ||
            IsInsideMacroWaterFootprint(terrain, plan, position, .55f, water))
            return false;
        if (water.preparedV2 != null && !IsPreparedVegetationPositionAllowed(
                water.preparedV2.SampleTerrain(position.x, position.z), false))
            return false;
        Vector3 local = position - terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        return terrain.terrainData.GetSteepness(local.x / size.x, local.z / size.z) <= 38f;
    }

    private static bool IsShorelineDressingFootprintAllowed(
        GameObject instance, Terrain terrain, GeneratedWorldPlanRecord plan,
        List<LivedPathSegment> paths, MacroWaterSet water)
    {
        if (!TryGetWildernessBounds(instance, out Bounds bounds))
            return false;
        float radius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
        if (IsNearLivedPath(paths, bounds.center, radius + 2.25f))
            return false;
        float lowest = float.PositiveInfinity;
        float highest = float.NegativeInfinity;
        for (int z = -1; z <= 1; z++)
        {
            for (int x = -1; x <= 1; x++)
            {
                Vector3 contact = bounds.center + new Vector3(bounds.extents.x * x, 0f, bounds.extents.z * z);
                if (!IsShorelineDressingPointAllowed(terrain, plan, paths, water, contact))
                    return false;
                float height = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, contact);
                lowest = Mathf.Min(lowest, height);
                highest = Mathf.Max(highest, height);
            }
        }
        // note: Banks naturally grade more than an inland prop pad; permit a shallow bank slope while rejecting cliff-spanning imported meshes.
        return highest - lowest <= 2.4f;
    }

    private static void AppendAreaShorelineCandidates(
        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin, string seed, List<Vector3> destination)
    {
        // note: Seeded small groups avoid an evenly spaced ring while respecting the rendered lake's elliptical footprint and bank blend.
        float padding = YQGeneratedWorldTerrain.WorldSize / (YQGeneratedWorldTerrain.HeightmapResolution - 1f) * 1.414214f +
                        Mathf.Max(8f, basin.ShortRadius * 0.6f);
        for (int cluster = 0; cluster < 6; cluster++)
        {
            float angle = Deterministic01(seed + "|cluster|" + cluster) * Mathf.PI * 2f;
            for (int member = 0; member < 2; member++)
            {
                float sampleAngle = angle + member * 0.025f;
                float extra = padding + Deterministic01(seed + "|offset|" + cluster + "|" + member) * 3f;
                Vector2 radial = basin.LongAxisXZ * (Mathf.Cos(sampleAngle) * (basin.LongRadius + extra)) +
                                 basin.ShortAxisXZ * (Mathf.Sin(sampleAngle) * (basin.ShortRadius + extra));
                destination.Add(new Vector3(basin.CenterWorld.x + radial.x, 0f, basin.CenterWorld.z + radial.y));
            }
        }
    }

    private static GeneratedAssetReferenceRecord PickShorelineReference(
        List<GeneratedAssetReferenceRecord> candidates,
        string seed)
    {
        if (candidates == null || candidates.Count == 0)
            return null;
        return candidates[Mathf.Clamp(Mathf.FloorToInt(Deterministic01(seed + "|reference") * candidates.Count), 0, candidates.Count - 1)];
    }

    private static void AddShorelineCandidates(List<GeneratedAssetReferenceRecord> destination, List<GeneratedAssetReferenceRecord> source)
    {
        if (destination == null || source == null)
            return;
        for (int index = 0; index < source.Count; index++)
        {
            GeneratedAssetReferenceRecord reference = source[index];
            if (reference == null || IsLargeTerrainFeatureReference(reference))
                continue;
            string semantic = BuildReferenceSemanticText(reference);
            // note: A generic water tag is not proof of vegetation; keep shoreline selection within approved plant families.
            if (ContainsAnySemantic(semantic, "reed", "rush", "marsh", "wetland", "bush", "shrub", "grass", "fern", "moss", "tree"))
                destination.Add(reference);
        }
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
                72,
                168);
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
                52);
    }

    private static float ResolveBiomeScatterMultiplier(
        YQGeneratedWorldTileProfile profile,
        bool rocks)
    {
        if (rocks)
        {
            // note: Geological dressing follows ruggedness and uplift, giving mountain provinces visible shelves and outcrops without scattering rocks through settlement roads.
            return Mathf.Lerp(
                    0.82f,
                    1.38f,
                    profile.Ruggedness) *
                Mathf.Lerp(
                    0.92f,
                    1.12f,
                    profile.MountainAffinity);
        }

        float biomeMultiplier;
        switch (profile.Biome)
        {
            case YQGeneratedWorldBiomeKind.AncientWoodland:
                biomeMultiplier = 1.4f;
                break;

            case YQGeneratedWorldBiomeKind.Wetland:
                biomeMultiplier = 1.28f;
                break;

            case YQGeneratedWorldBiomeKind.Moorland:
                biomeMultiplier = 1.18f;
                break;

            case YQGeneratedWorldBiomeKind.Highland:
                biomeMultiplier = 0.92f;
                break;

            default:
                biomeMultiplier = 1.08f;
                break;
        }

        // note: Understory gets a small moisture bonus so open wet ground reads as alive even where canopy is intentionally sparse.
        return biomeMultiplier *
            Mathf.Lerp(
                0.9f,
                1.12f,
                profile.Moisture);
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
        List<LivedPathSegment> sharedLivedPaths = null)
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
            false,
            sharedLivedPaths);
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
        bool treesOnly,
        List<LivedPathSegment> sharedLivedPaths = null)
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
            treesOnly,
            sharedLivedPaths);
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
        bool treesOnly = false,
        List<LivedPathSegment> sharedLivedPaths = null,
        Bounds? placementBounds = null,
        string seedScope = null,
        bool synchronousInstantiation = false,
        MacroWaterSet sharedMacroWater = null,
        string layerLabel = null,
        Action<int> requiredMinimumCompleted = null)
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

        // note: Layer roots make ecology coverage inspectable and let the streamed owner unload each physical dressing bucket without changing placement authority.
        GameObject root =
            new GameObject(
                string.IsNullOrWhiteSpace(layerLabel)
                    ? treesOnly
                        ? "Trees"
                        : string.Equals(
                        slot,
                        YQWorldAssetCatalog
                            .SlotVegetation,
                        StringComparison.OrdinalIgnoreCase)
                        ? "Vegetation"
                        : "Rocks"
                    : layerLabel);

        root.transform.SetParent(
            parent,
            false);

        int spawned =
            0;
        HashSet<string> spawnedTreeFamilies =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
        // note: Keep the ordinary canopy search bounded, then reserve a second deterministic band for sparse cells whose accepted masks leave only a few valid habitat samples.
        int standardAttempts =
            targetCount *
            (treesOnly ? 7 : 4);
        int attempts =
            treesOnly
                ? standardAttempts * 2
                : standardAttempts;
        // note: Required low vegetation previously sampled only four cluster centers, then retried those same rejected centers. Extend only an empty streamed layer into a bounded deterministic band, retaining every habitat and clearance rule.
        bool requiredLowVegetation = placementBounds.HasValue && vegetationSlot && !treesOnly &&
            (string.Equals(layerLabel, "Understory", StringComparison.Ordinal) || string.Equals(layerLabel, "Shrubs", StringComparison.Ordinal));
        if (requiredLowVegetation)
            attempts += 64;

        // note: Shared path and water projections are already prepared by the chunk owner; scatter keeps one aggregate budget instead of inserting a frame break before every small lookup.
        List<GeneratedAssetReferenceRecord> scatterReferences =
            BuildSmallScatterReferences(
                palette,
                slot,
                treesOnly);

        List<LivedPathSegment> livedPaths =
            sharedLivedPaths ??
            BuildLivedPathNetwork(plan, terrain);

        // note: Reuse the chunk-level water projection when available; standalone scatter callers still build their own deterministic projection.
        MacroWaterSet macroWater = sharedMacroWater ?? BuildMacroWaterSet(terrain, plan);

        if (scatterReferences.Count == 0)
        {
            UnityEngine.Object.Destroy(root);
            completed?.Invoke(0);
            yield break;
        }

        float frameStartedAt = Time.realtimeSinceStartup;
        const float scatterWorkSliceSeconds = 0.004f;
        HashSet<string> measuredScatterPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (int attempt = 0;
             attempt < attempts &&
             spawned < targetCount;
            attempt++)
        {
            // note: Existing successful layers keep exactly their original placements; the additional search ends as soon as a previously empty required layer has a valid physical instance.
            if (requiredLowVegetation && attempt >= standardAttempts && spawned > 0)
                break;
            if (scatterReferences.Count == 0)
                break;

            if (Time.realtimeSinceStartup - frameStartedAt >= scatterWorkSliceSeconds)
            {
                // note: Rejected candidates consume the same frame budget as accepted ones, preventing sparse or incompatible palettes from spinning through hundreds of checks in one loading frame.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }

            string seed =
                plan.worldSeed +
                "|scatter|" +
                (string.IsNullOrWhiteSpace(seedScope) ? region.regionId : seedScope) +
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
                (string.IsNullOrWhiteSpace(seedScope) ? region.regionId : seedScope) +
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

            // note: Chunk-scoped scatter is clipped to its deterministic rectangle before any prefab is cloned, preventing cross-boundary duplicates and visible seams.
            if (placementBounds.HasValue)
            {
                Bounds bounds = placementBounds.Value;
                if (position.x < bounds.min.x || position.x >= bounds.max.x ||
                    position.z < bounds.min.z || position.z >= bounds.max.z)
                    continue;
            }

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

            if (macroWater.preparedV2 != null)
            {
                YQSpatialEcologySampleV2 ecology =
                    macroWater.preparedV2.SampleEcologyContext(
                        position.x,
                        position.z);
                if (vegetationSlot)
                {
                    if (!IsPreparedVegetationPositionAllowed(
                            ecology.terrain,
                            !treesOnly))
                    {
                        continue;
                    }

                    float habitatAffinity = treesOnly
                        ? ecology.tileProfile.ForestDensity
                        : Mathf.Max(
                            ecology.tileProfile.ForestDensity,
                            ecology.tileProfile.Moisture * 0.86f);
                    habitatAffinity -=
                        ecology.civilizationDensity *
                        (treesOnly ? 0.1f : 0.035f);
                    float habitatScore =
                        habitatAffinity * 0.62f +
                        Deterministic01(
                            clusterSeed + "|v2_habitat") * 0.38f;
                    float minimumHabitatScore = treesOnly ? 0.34f : 0.25f;
                    // note: A canopy-applicable cell may still have sparse local forest affinity after route, water, and site masks are applied; the fallback remains outside those physical exclusions.
                    if (treesOnly &&
                        attempt >= standardAttempts &&
                        minimumHabitatScore > 0.05f)
                    {
                        minimumHabitatScore = 0.05f;
                    }
                    if (habitatScore < minimumHabitatScore)
                        continue;

                    // note: Visible prefab foliage now forms deterministic habitat clusters from the same semantic ecology and construction masks as Terrain-native vegetation.
                }
                else
                {
                    if (!IsPreparedRockPositionAllowed(ecology.terrain))
                        continue;
                    float geologicalScore =
                        ecology.tileProfile.Ruggedness * 0.54f +
                        ecology.tileProfile.MountainAffinity * 0.28f +
                        Deterministic01(
                            clusterSeed + "|v2_geology") * 0.18f;
                    if (geologicalScore < 0.26f)
                        continue;

                    // note: V2 rock clusters follow geological affinity while exact route, site, shoreline, and cave-mouth masks remain obstruction-free.
                }
            }

            float pathPadding = treesOnly
                ? 7.5f
                : vegetationSlot
                    ? 1.4f
                    : 3.2f;
            if (IsNearLivedPath(
                    livedPaths,
                    position,
                    pathPadding))
            {
                // note: Trees frame paths at sightline distance, ground cover stays off the tread, and solid rocks cannot become random locomotion barriers.
                continue;
            }

            // note: Canopy diversity advances from the deterministic attempt index, not the number already spawned. A rejected position or prefab must not keep retrying the same tree family and starve the remaining approved silhouettes.
            GeneratedAssetReferenceRecord reference =
                treesOnly
                    ? scatterReferences[attempt % scatterReferences.Count]
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

            // note: Vegetation can be authored with a large canopy; fit it to the mature-tree budget after instantiation instead of discarding the ecological family before it is audited.
            if (!vegetationSlot &&
                IsOversizedSmallScatterPrefab(
                    prefab,
                    slot,
                    reference))
            {
                continue;
            }

            position.y =
                YQGeneratedWorldTerrain
                    .SampleWorldHeight(
                        terrain,
                        position);

            GameObject instance;
            if (synchronousInstantiation)
            {
                // note: Explicit synchronous callers retain their existing clone contract; streamed chunks select the async branch below to keep imported hierarchy work off the frame boundary.
                instance = UnityEngine.Object.Instantiate(prefab, root.transform);
                yield return null;
            }
            else
            {
                AsyncInstantiateOperation<GameObject> operation =
                    UnityEngine.Object.InstantiateAsync(
                        prefab,
                        root.transform);
                // note: Nearby streamed ecology must compete at normal async priority so camera-visible cells do not wait behind unrelated background work.
                operation.priority = 0;
                // note: Origin dressing retains cooperative async loading for its larger authored set while this branch keeps the player-facing chunk responsive.
                yield return operation;
                instance =
                    operation.Result != null && operation.Result.Length > 0
                        ? operation.Result[0]
                        : null;
            }

            if (instance == null)
                continue;

            instance.name =
                "Wilderness_" +
                SafeName(slot) +
                "_" +
                spawned +
                "__" +
                prefab.name;

            position.y =
                YQGeneratedWorldTerrain
                    .SampleWorldHeight(
                        terrain,
                        position);

            instance.transform.position = position;

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

            // note: Pre-fit imported vegetation so the final measured canopy remains inside the traversal-safe presentation envelope.
            scale = ResolveAuditedScatterScale(
                prefab,
                slot,
                reference,
                scale);

            // note: Deterministic wilderness variation multiplies the imported prefab's authored root scale instead of erasing its unit conversion.
            instance.transform.localScale *= scale;

            // note: Re-measure instantiated hierarchies because imported child renderers may expose bounds that are unavailable on the prefab asset.
            FitInstantiatedScatterToBudget(
                instance,
                slot,
                reference);

            bool auditInstantiatedBounds =
                string.IsNullOrWhiteSpace(reference.assetPath) ||
                measuredScatterPaths.Add(reference.assetPath);
            if (auditInstantiatedBounds &&
                IsOversizedSmallScatterPrefab(
                    instance,
                    slot,
                    reference,
                    false))
            {
                // note: Some prefab assets report incomplete bounds until instantiated; quarantine the measured offender immediately so loading never clones and repairs the same unusable hierarchy again.
                if (TryGetWildernessBounds(instance, out Bounds rejectedBounds))
                {
                    LogOversizedWildernessRejection(
                        "vegetation/scenery asset",
                        instance,
                        Mathf.Max(rejectedBounds.size.x, rejectedBounds.size.z),
                        rejectedBounds.size.y);
                }

                RemoveScatterReferenceByAssetPath(
                    scatterReferences,
                    reference.assetPath);
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                continue;
            }

            registry.ApplyMaterialOverrides(
                reference.assetPath,
                instance,
                synchronousInstantiation);

            PrepareWildernessInstance(
                instance);

            // note: Bound the complete placement, bounds, material, and activation batch together; avoid four unconditional frame breaks per object while still yielding after a measurable slice.
            if (Time.realtimeSinceStartup - frameStartedAt >= scatterWorkSliceSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }

            if (!synchronousInstantiation &&
                YQRuntimeUrpMaterialRepair.NeedsMaterialRepair(instance))
            {
                // note: Only unsupported or incomplete streamed hierarchies pay the cooperative repair cost; valid curated ecology skips the expensive full traversal.
                yield return YQRuntimeUrpMaterialRepair
                    .RepairMaterialHierarchyRoutine(
                        instance,
                        null);
            }

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
            if (spawned == 1)
            {
                // note: Let the streamer publish its already-defined minimum ecology receipt as soon as this layer has one finalized object; keep placing to the original deterministic target afterward.
                requiredMinimumCompleted?.Invoke(spawned);
            }
            if (treesOnly && !string.IsNullOrWhiteSpace(reference.assetPath))
                spawnedTreeFamilies.Add(reference.assetPath);

            // note: Wilderness dressing shares the strict loading budget; even a vegetation-heavy region cannot instantiate its complete scatter set on one presentation frame.
            if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        if (treesOnly &&
            YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) &&
            ShouldSpawnVisibleTrees(palette) &&
            targetCount >= 4 &&
            spawnedTreeFamilies.Count < 4)
        {
            // note: Sparse accepted masks must not erase a valid chunk; report limited silhouette variety while preserving the required ecology receipt when at least one approved tree published.
            Debug.LogWarning(
                "[YQGeneratedWorldEnvironment] CANOPY DIVERSITY INSUFFICIENT " +
                region.regionId + " variants=" + spawnedTreeFamilies.Count +
                " spawned=" + spawned +
                " candidates=" + scatterReferences.Count);
        }

        if (requiredLowVegetation && spawned == 0)
        {
            // note: Exhaustion remains an explicit publication failure; no habitat requirement is waived to release the cell.
            Debug.LogError("[YQGeneratedWorldEnvironment] REQUIRED SCATTER EXHAUSTED layer=" + layerLabel +
                " seed=" + seedScope + " attempts=" + attempts + " candidates=" + scatterReferences.Count);
        }
        completed?.Invoke(spawned);
    }

    private static void RemoveScatterReferenceByAssetPath(
        List<GeneratedAssetReferenceRecord> references,
        string assetPath)
    {
        if (references == null || string.IsNullOrWhiteSpace(assetPath))
            return;

        for (int index = references.Count - 1; index >= 0; index--)
        {
            GeneratedAssetReferenceRecord candidate = references[index];
            if (candidate != null && string.Equals(
                    candidate.assetPath,
                    assetPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                references.RemoveAt(index);
            }
        }
    }

    private static bool IsOversizedSmallScatterPrefab(
        GameObject prefab,
        string slot,
        GeneratedAssetReferenceRecord reference,
        bool applyReferenceScale = true)
    {
        if (prefab == null ||
            !TryGetWildernessBounds(
                prefab,
                out Bounds bounds))
        {
            return false;
        }

        float scale =
            applyReferenceScale && reference != null
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

        // note: Match the final placement gate so mature canopies are not quarantined before grounding can run.
        return (vegetation &&
                (footprint > (tree ? 36f : 20f) ||
                 height > (tree ? 42f : 32f))) ||
               (rock &&
                (footprint > 16f || height > 16f));
    }

    private static float ResolveAuditedScatterScale(
        GameObject prefab,
        string slot,
        GeneratedAssetReferenceRecord reference,
        float requestedScale)
    {
        if (prefab == null ||
            !TryGetWildernessBounds(prefab, out Bounds bounds))
        {
            return requestedScale;
        }

        bool vegetation = string.Equals(
            slot,
            YQWorldAssetCatalog.SlotVegetation,
            StringComparison.OrdinalIgnoreCase);
        bool tree = vegetation && LooksLikeTree(prefab, reference);
        // note: Keep mature trees readable in the skyline while preventing imported giant-canopy variants from dominating nearby gameplay views.
        float maximumFootprint = vegetation ? (tree ? 36f : 20f) : 16f;
        float maximumHeight = vegetation ? (tree ? 42f : 32f) : 16f;
        float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
        float cap = Mathf.Min(
            footprint > 0.001f ? maximumFootprint / footprint : 1f,
            bounds.size.y > 0.001f ? maximumHeight / bounds.size.y : 1f);
        float auditedScale = Mathf.Min(requestedScale, cap);

        // note: A very small lower bound prevents malformed imported bounds from collapsing a valid family into a zero-scale object.
        return Mathf.Max(0.08f, auditedScale);
    }

    private static void FitInstantiatedScatterToBudget(
        GameObject instance,
        string slot,
        GeneratedAssetReferenceRecord reference)
    {
        if (instance == null ||
            !TryGetWildernessBounds(instance, out Bounds bounds))
        {
            return;
        }

        bool vegetation = string.Equals(
            slot,
            YQWorldAssetCatalog.SlotVegetation,
            StringComparison.OrdinalIgnoreCase);
        bool tree = vegetation && LooksLikeTree(instance, reference);
        // note: The instantiated fit uses the same mature-tree envelope as the preflight audit so a late renderer-bound update cannot reintroduce oversized trunks.
        float maximumFootprint = vegetation ? (tree ? 36f : 20f) : 16f;
        float maximumHeight = vegetation ? (tree ? 42f : 32f) : 16f;
        float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
        float fit = Mathf.Min(
            footprint > 0.001f ? maximumFootprint / footprint : 1f,
            bounds.size.y > 0.001f ? maximumHeight / bounds.size.y : 1f);
        if (fit < 0.999f)
        {
            // note: A second measured pass catches renderer bounds that Unity only exposes after hierarchy instantiation.
            instance.transform.localScale *= Mathf.Max(0.08f, fit);
        }
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

        if (treesOnly &&
            string.Equals(
                slot,
                YQWorldAssetCatalog.SlotVegetation,
                StringComparison.OrdinalIgnoreCase) &&
            ShouldSpawnVisibleTrees(palette))
        {
            // note: Put the approved canopy family first so deterministic early placements demonstrate four distinct silhouettes before palette-specific additions can repeat one family.
            AddApprovedVisibleTreeReferences(candidates, palette);
        }

        AddValidSmallReferences(
            candidates,
            primary,
            slot,
            treesOnly);

        if (string.Equals(
                slot,
                YQWorldAssetCatalog.SlotFloorDeco,
                StringComparison.OrdinalIgnoreCase))
        {
            // note: The floor-deco slot contains barrels and crates for sites; only log-like references may enter the streamed deadfall layer.
            for (int index = candidates.Count - 1; index >= 0; index--)
            {
                if (!ContainsAnySemantic(
                        BuildReferenceIdentitySemanticText(candidates[index]),
                        "deadfall", "fallen", "branch", "log", "wood", "trunk"))
                    candidates.RemoveAt(index);
            }
            if (candidates.Count == 0)
                AddApprovedDeadfallReferences(candidates);
        }

        if (!treesOnly &&
            string.Equals(
                slot,
                YQWorldAssetCatalog.SlotVegetation,
                StringComparison.OrdinalIgnoreCase) &&
            candidates.Count < 4 &&
            ShouldUseFallbackUnderstory(
                palette))
        {
            AddApprovedFallbackUnderstoryReferences(
                candidates);
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

    private static void AddApprovedDeadfallReferences(
        List<GeneratedAssetReferenceRecord> destination)
    {
        if (destination == null)
            return;

        for (int index = 0; index < ApprovedDeadfallPrefabs.Length; index++)
        {
            // note: These references remain ordinary curated asset bindings and are filtered by the same runtime registry before instantiation.
            AddUniqueSmallReference(
                destination,
                new GeneratedAssetReferenceRecord
                {
                    assetKey = "approved_deadfall_" + index,
                    assetPath = ApprovedDeadfallPrefabs[index],
                    assetType = "prefab",
                    slotTag = YQWorldAssetCatalog.SlotFloorDeco,
                    weight = 1,
                    scaleMin = 0.72f,
                    scaleMax = 1.08f,
                    footprintX = 3.5f,
                    footprintZ = 1.8f,
                    placementRule = "terrain_deadfall",
                    rotationRule = "random_yaw",
                    allowRepeat = true,
                    blocksNav = false,
                    notes = "Approved imported deadfall reference.",
                    subTags = new List<string> { "deadfall", "log", "wood" },
                    styleTags = new List<string> { "fantasy", "natural" }
                });
        }
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

    private static bool ShouldUseFallbackUnderstory(
        GeneratedRegionAssetPaletteRecord palette)
    {
        string style =
            palette != null
                ? SafeText(
                    palette.styleKey,
                    string.Empty)
                : string.Empty;

        return !ContainsAnySemantic(
            style,
            "desert",
            "western",
            "persepolis",
            "badland",
            "scifi",
            "cyberpunk",
            "container",
            "hospital",
            "sewer");
    }

    private static void AddApprovedFallbackUnderstoryReferences(
        List<GeneratedAssetReferenceRecord> result)
    {
        if (result == null)
            return;

        for (int index = 0;
             index < ApprovedFallbackUnderstoryPrefabs.Length;
             index++)
        {
            // note: Bush fallback is used only when a curated palette has no valid low vegetation; it fills an authored gap without replacing an existing biome family.
            AddUniqueSmallReference(
                result,
                new GeneratedAssetReferenceRecord
                {
                    assetKey = "approved_understory_" + index,
                    assetPath = ApprovedFallbackUnderstoryPrefabs[index],
                    assetType = "prefab",
                    slotTag = YQWorldAssetCatalog.SlotVegetation,
                    weight = 1,
                    scaleMin = 0.72f,
                    scaleMax = 1.16f,
                    footprintX = 3.5f,
                    footprintZ = 3.5f,
                    placementRule = "terrain_understory",
                    rotationRule = "random_yaw",
                    allowRepeat = true,
                    blocksNav = false,
                    notes = "Approved low-vegetation fallback for sparse palettes.",
                    subTags = new List<string>
                    {
                        "bush",
                        "understory",
                        "material_repaired"
                    },
                    styleTags = new List<string>
                    {
                        "fantasy",
                        "natural"
                    }
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
            56);
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

        YQPreparedSpatialMaterializationV2 preparedV2 = null;
        List<YQSpatialMaterializationSiteV2> preparedCaveSites = null;
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
        {
            if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                    plan,
                    out preparedV2,
                    out string projectionFailure))
            {
                Debug.LogError(
                    "[YQGeneratedWorldEnvironment] V2 cave projection rejected: " +
                    projectionFailure);
                completed?.Invoke(0);
                yield break;
            }

            preparedCaveSites =
                new List<YQSpatialMaterializationSiteV2>();
            for (int siteIndex = 0;
                 siteIndex < preparedV2.SiteCount;
                 siteIndex++)
            {
                YQSpatialMaterializationSiteV2 site =
                    preparedV2.GetSite(siteIndex);
                if (site.kind == YQSiteKindV2.NaturalFeature &&
                    site.concealedAccess &&
                    site.requiresTransition &&
                    string.Equals(
                        site.parentRegionId,
                        region.regionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    preparedCaveSites.Add(site);
                }
            }

            if (preparedCaveSites.Count == 0)
            {
                completed?.Invoke(0);
                yield break;
            }
        }

        // note: Regions receive a small exploration cluster instead of one isolated entrance; higher danger supports a third destination.
        int desired =
            preparedCaveSites != null
                ? preparedCaveSites.Count
                : region.dangerTier >= 5
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
            YQSpatialMaterializationSiteV2 preparedSite =
                preparedCaveSites != null
                    ? preparedCaveSites[caveIndex]
                    : default;
            bool created =
                false;

            for (int attempt = 0;
                 attempt < (preparedV2 != null
                     ? caveReferences.Count
                     : 10) &&
                 !created;
                 attempt++)
            {
                string seed =
                    preparedV2 != null
                        ? plan.worldSeed +
                          "|v2_cave|" +
                          preparedSite.siteId +
                          "|" +
                          attempt
                        : plan.worldSeed +
                          "|wilderness_cave|" +
                          region.regionId +
                          "|" +
                          caveIndex +
                          "|" +
                          attempt;

                Vector3 position;
                if (preparedV2 != null)
                {
                    // note: V2 cave entrances consume the accepted natural-feature anchor exactly; asset retries may not relocate the feature.
                    position = new Vector3(
                        preparedSite.x,
                        0f,
                        preparedSite.z);
                }
                else if (!TryResolveWildernessPosition(
                             terrain,
                             plan,
                             regionCenter,
                             seed,
                             78f,
                             WildernessRadiusMax,
                             52f,
                             72f,
                             42f,
                             out position))
                {
                    continue;
                }

                int referenceIndex = preparedV2 != null
                    ? ((int)(StableHash32(
                            preparedSite.siteId + "|cave_reference") %
                        (uint)caveReferences.Count) + attempt) %
                      caveReferences.Count
                    : (int)(StableHash32(seed + "|reference") %
                            (uint)caveReferences.Count);
                GeneratedAssetReferenceRecord reference =
                    caveReferences[referenceIndex];

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
                    preparedV2 != null
                        ? preparedSite.headingDegrees
                        : approachDirection.sqrMagnitude > 0.01f
                        ? Quaternion.LookRotation(
                            approachDirection.normalized,
                            Vector3.up).eulerAngles.y
                        : 0f;
                instance.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        approachYaw +
                            (preparedV2 != null
                                ? 0f
                                : Mathf.Lerp(
                                -12f,
                                12f,
                                Deterministic01(
                                    seed +
                                    "|yaw_variation"))),
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

                if (preparedV2 != null)
                {
                    if (!preparedV2.TryValidateFootprint(
                            preparedSite.sourceSemanticId,
                            footprint * 0.5f,
                            out _))
                    {
                        instance.SetActive(false);
                        UnityEngine.Object.Destroy(instance);
                        continue;
                    }
                }
                else if (!ValidateStructuralTerrainFootprint(
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

                // note: A size-valid cave can still intersect a through-road; reject that physical binding before it enters the completed world.
                if (CaveBlocksTravelCorridor(instance, terrain, plan))
                {
                    Debug.LogWarning("[YQGeneratedWorldEnvironment] Cave binding rejected for road collision: " + reference.assetPath);
                    instance.SetActive(false);
                    UnityEngine.Object.Destroy(instance);
                    continue;
                }
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

    private static bool CaveBlocksTravelCorridor(GameObject cave, Terrain terrain, GeneratedWorldPlanRecord plan)
    {
        // note: Use the authoritative player's capsule and test the candidate's actual colliders, allowing genuinely open tunnels to remain valid.
        var motor = YQInvestorPlayerMotor.ActiveMotor;
        var controller = motor != null ? motor.GetComponent<CharacterController>() : null;
        if (!YQRouteTraversalProbe.Shape.TryCapture(controller, out var shape)) return true;
        if (!TryGetWildernessBounds(cave, out var bounds)) return true;
        bounds.Expand(shape.radius * 2f);
        Physics.SyncTransforms();
        var overlaps = new Collider[64];
        foreach (var path in BuildLivedPathNetwork(plan, terrain))
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(path.start, path.end) / Mathf.Max(.1f, shape.radius)));
            for (int step = 0; step <= steps; step++)
            {
                Vector2 point = ResolveLivedPathCenter(path, step / (float)steps);
                if (point.x < bounds.min.x || point.x > bounds.max.x || point.y < bounds.min.z || point.y > bounds.max.z) continue;
                Vector3 feet = new Vector3(point.x, 0f, point.y);
                feet.y = terrain.SampleHeight(feet) + terrain.transform.position.y;
                shape.Capsule(feet, out var lower, out var upper, out float radius);
                int count = Physics.OverlapCapsuleNonAlloc(lower, upper, radius, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                if (count == overlaps.Length) return true;
                for (int i = 0; i < count; i++)
                    if (overlaps[i] != null && overlaps[i].transform.IsChildOf(cave.transform)) return true;
            }
        }
        return false;
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
        List<LivedPathSegment> livedPaths =
            BuildLivedPathNetwork(plan, terrain);
        MacroWaterSet macroWater =
            BuildMacroWaterSet(terrain, plan);

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
                    out Vector3 groupPosition,
                    macroWater))
            {
                continue;
            }

            if (IsNearLivedPath(
                    livedPaths,
                    groupPosition,
                    8f))
            {
                // note: Ambient groups remain discoverable beside travel space without spawning directly into a player's navigation lane.
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

                if (IsNearLivedPath(
                        livedPaths,
                        position,
                        3f))
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

        YQPreparedSpatialMaterializationV2 preparedV2 = null;
        List<YQSpatialMaterializationSiteV2> preparedRewardSites = null;
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
        {
            if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                    plan,
                    out preparedV2,
                    out string projectionFailure))
            {
                Debug.LogError(
                    "[YQGeneratedWorldEnvironment] V2 reward projection rejected: " +
                    projectionFailure);
                completed?.Invoke(0);
                yield break;
            }

            preparedRewardSites =
                new List<YQSpatialMaterializationSiteV2>();
            for (int siteIndex = 0;
                 siteIndex < preparedV2.SiteCount;
                 siteIndex++)
            {
                YQSpatialMaterializationSiteV2 site =
                    preparedV2.GetSite(siteIndex);
                bool supportedRewardSite =
                    site.kind == YQSiteKindV2.PointOfInterest ||
                    (site.kind == YQSiteKindV2.NaturalFeature &&
                     site.concealedAccess);
                if (supportedRewardSite &&
                    site.requiresReward &&
                    string.Equals(
                        site.parentRegionId,
                        region.regionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    preparedRewardSites.Add(site);
                }
            }

            if (preparedRewardSites.Count == 0)
            {
                completed?.Invoke(0);
                yield break;
            }
        }

        int danger =
            Mathf.Clamp(
                region.dangerTier,
                1,
                8);

        int desired =
            preparedRewardSites != null
                ? preparedRewardSites.Count
                : BaseTreasurePerRegion;

        if (preparedRewardSites == null && danger >= 5)
        {
            desired++;
        }
        List<LivedPathSegment> legacyLivedPaths =
            preparedV2 == null
                ? BuildLivedPathNetwork(plan, terrain)
                : null;
        MacroWaterSet legacyMacroWater =
            preparedV2 == null
                ? BuildMacroWaterSet(terrain, plan)
                : null;

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
            YQSpatialMaterializationSiteV2 preparedSite =
                preparedRewardSites != null
                    ? preparedRewardSites[index]
                    : default;
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
                    preparedV2 != null
                        ? plan.worldSeed +
                          "|v2_reward|" +
                          preparedSite.siteId +
                          "|" +
                          attempt
                        : plan.worldSeed +
                          "|wilderness_treasure|" +
                          region.regionId +
                          "|" +
                          index +
                          "|" +
                          attempt;

                Vector3 position;
                if (preparedV2 != null)
                {
                    float headingRadians =
                        preparedSite.headingDegrees * Mathf.Deg2Rad;
                    Vector2 outward = new Vector2(
                        Mathf.Sin(headingRadians),
                        Mathf.Cos(headingRadians));
                    float concealmentDepth =
                        preparedSite.kind == YQSiteKindV2.NaturalFeature
                            ? Mathf.Min(
                                6f,
                                preparedSite.reservedRadius * 0.3f)
                            : 0f;
                    position = new Vector3(
                        preparedSite.x - outward.x * concealmentDepth,
                        0f,
                        preparedSite.z - outward.y * concealmentDepth);

                    // note: V2 rewards occupy their accepted POI reserve or the interior side of a concealed natural entrance; retries cannot scatter them elsewhere.
                }
                else if (!TryResolveWildernessPosition(
                             terrain,
                             plan,
                             regionCenter,
                             seed,
                             72f,
                             WildernessRadiusMax,
                             55f,
                             76f,
                             28f,
                             out position,
                             legacyMacroWater))
                {
                    continue;
                }

                if (preparedV2 == null &&
                    IsNearLivedPath(
                        legacyLivedPaths,
                        position,
                        3f))
                {
                    // note: Legacy random rewards remain tucked into wilderness rather than becoming solid obstacles on painted travel corridors.
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

                if (preparedV2 != null &&
                    !preparedV2.TryValidateFootprint(
                        preparedSite.sourceSemanticId,
                        Mathf.Max(
                            reference.footprintX,
                            reference.footprintZ) *
                        Mathf.Max(
                            0.01f,
                            reference.scaleMax) *
                        0.5f,
                        out _))
                {
                    continue;
                }

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
                    preparedV2 != null
                        ? "wilderness:" +
                          worldKey +
                          ":" +
                          SafeName(preparedSite.siteId) +
                          ":reward"
                        : "wilderness:" +
                          worldKey +
                          ":" +
                          SafeName(region.regionId) +
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
                        preparedV2 != null
                            ? preparedSite.headingDegrees
                            : Deterministic01(
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
        // note: Preserve the existing boolean gate while allowing streamed callers to reuse its accepted terrain sample.
        return IsWildernessPositionAllowed(
            terrain,
            plan,
            candidate,
            settlementClearRadius,
            originClearRadius,
            encampmentClearRadius,
            macroWater,
            out _);
    }

    private static bool IsWildernessPositionAllowed(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Vector3 candidate,
        float settlementClearRadius,
        float originClearRadius,
        float encampmentClearRadius,
        MacroWaterSet macroWater,
        out YQSpatialTerrainSampleV2 preparedSample)
    {
        // note: Cluster members pass the same terrain and authored-location reserves as their grove center, preventing denser dressing from invading doors, roads, or encounter staging areas.
        preparedSample = default;

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
                settlementClearRadius,
                macroWater?.preparedV2))
        {
            return false;
        }

        if (encampmentClearRadius > 0f &&
            NearAnyEncampment(
                plan,
                terrain,
                candidate,
                encampmentClearRadius,
                macroWater?.preparedV2))
        {
            return false;
        }

        if (IsInsideMacroWaterFootprint(
                terrain,
                plan,
                candidate,
                2f,
                macroWater,
                out preparedSample))
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
            (macroWater.count == 0 && macroWater.preparedV2 == null))
        {
            return false;
        }

        if (macroWater.preparedV2 != null)
        {
            YQSpatialTerrainSampleV2 sample =
                macroWater.preparedV2.SampleTerrain(
                    candidate.x,
                    candidate.z);
            float footprintThreshold =
                ResolvePreparedWaterMaskThreshold(
                    shorelinePadding);
            float waterSurfaceY =
                terrain.transform.position.y +
                terrain.terrainData.size.y *
                    sample.waterSurfaceNormalized;

            // note: V2 submersion uses the same prepared hydrology mask and accepted surface elevation as terrain carving and visible water.
            return sample.waterMask >= footprintThreshold &&
                   candidate.y <= waterSurfaceY + shorelinePadding;
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
        // note: Retain the original exclusion contract for callers that do not need to inspect the accepted sample.
        return IsInsideMacroWaterFootprint(
            terrain,
            plan,
            candidate,
            shorelinePadding,
            macroWater,
            out _);
    }

    private static bool IsInsideMacroWaterFootprint(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Vector3 candidate,
        float shorelinePadding,
        MacroWaterSet macroWater,
        out YQSpatialTerrainSampleV2 preparedSample)
    {
        preparedSample = default;
        if (terrain == null || terrain.terrainData == null ||
            plan == null)
        {
            return false;
        }

        macroWater ??=
            BuildMacroWaterSet(
                terrain,
                plan);

        if (macroWater.preparedV2 != null)
        {
            // note: Return the exact immutable sample used by the shoreline predicate so contextual dressing does not rescan the whole accepted V2 feature set.
            preparedSample = macroWater.preparedV2.SampleTerrain(
                    candidate.x,
                    candidate.z);

            // note: V2 scatter exclusion follows the continuous compiled shoreline rather than approximating rivers and wetlands as fixed legacy ellipses.
            return preparedSample.waterMask >=
                   ResolvePreparedWaterMaskThreshold(
                       shorelinePadding);
        }

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
        GeneratedWorldPlanRecord plan,
        YQPreparedSpatialMaterializationV2 preparedV2 = null)
    {
        MacroWaterSet result =
            new MacroWaterSet();

        if (terrain == null || terrain.terrainData == null ||
            plan == null)
        {
            return result;
        }

        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
        {
            if (preparedV2 != null)
            {
                // note: Use the same immutable accepted projection for this cell's route exclusion and water mask.
                result.preparedV2 = preparedV2;
            }
            else if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                    plan,
                    out result.preparedV2,
                    out string failure))
            {
                // note: Authoritative V2 water fails closed; it never repopulates an invalid accepted blueprint with unrelated seed basins.
                Debug.LogError(
                    "[YQGeneratedWorldEnvironment] V2 water projection rejected: " +
                    failure);
            }

            return result;
        }

        GeneratedSpatialWorldPlanRecord spatial =
            YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan);
        if (spatial != null && spatial.macroFeatures != null)
        {
            for (int i = 0;
                 i < spatial.macroFeatures.Count && result.count < result.basins.Length;
                 i++)
            {
                GeneratedSpatialFeatureRecord feature = spatial.macroFeatures[i];
                if (feature == null ||
                    !string.Equals(feature.featureKind, "lake_basin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                float heading = feature.headingDegrees * Mathf.Deg2Rad;
                Vector2 longAxis = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
                Vector2 shortAxis = new Vector2(-longAxis.y, longAxis.x);
                float waterY = terrain.transform.position.y +
                               terrain.terrainData.size.y * Mathf.Clamp(feature.waterLevel, 0.04f, 0.2f);
                result.basins[result.count++] =
                    new YQGeneratedWorldTerrain.MacroWaterBasinDescriptor(
                        new Vector3(feature.centerX, waterY, feature.centerZ),
                        longAxis,
                        shortAxis,
                        Mathf.Max(8f, feature.radiusX),
                        Mathf.Max(8f, feature.radiusZ),
                        waterY);
            }

            if (result.count > 0)
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

    private static float ResolvePreparedWaterMaskThreshold(
        float shorelinePadding)
    {
        return Mathf.Lerp(
            0.52f,
            0.12f,
            Mathf.Clamp01(shorelinePadding / 8f));
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

        // note: Mature alder and other approved trees have 29–36 m canopies; canopy width is not ground-cover footprint.
        if (vegetation &&
            (footprint > (tree ? 36f : 20f) ||
             height > (tree ? 42f : 32f)))
        {
            // note: Refit an otherwise valid vegetation candidate into the approved envelope before rejecting it, preserving ecological density without allowing a giant prop to dominate navigation.
            float maximumFootprint = tree ? 36f : 20f;
            float maximumHeight = tree ? 42f : 32f;
            float fitScale = Mathf.Min(
                maximumFootprint / Mathf.Max(.01f, footprint),
                maximumHeight / Mathf.Max(.01f, height));
            // note: The preflight and final instantiated bounds can differ after imported renderers/materials resolve. Apply the same bounded down-fit used by preflight instead of discarding an approved family merely because its late bounds need a stronger correction.
            if (fitScale > 0.001f)
            {
                float appliedFitScale = Mathf.Max(0.08f, fitScale);
                instance.transform.localScale *= appliedFitScale;
                if (TryGetWildernessBounds(instance, out Bounds refitBounds))
                {
                    bounds = refitBounds;
                    footprint = Mathf.Max(bounds.size.x, bounds.size.z);
                    height = bounds.size.y;
                }
                Debug.Log("[YQGeneratedWorldEnvironment] Refit oversized vegetation: " +
                    instance.name + " | scale=" + appliedFitScale.ToString("0.00"));
                if (footprint > maximumFootprint || height > maximumHeight)
                {
                    // note: Do not publish a still-oversized asset; this is only a stronger fit attempt, not a traversal-safety bypass.
                    LogOversizedWildernessRejection(
                        "vegetation/scenery asset",
                        instance,
                        footprint,
                        height);
                    return false;
                }
            }
            else
            {
                // note: Malformed bounds remain rejected; valid approved families are handled by the bounded fit above.
                LogOversizedWildernessRejection(
                    "vegetation/scenery asset",
                    instance,
                    footprint,
                    height);
                return false;
            }
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
            // note: These approved broadleaf species are trees even when their prefab names omit the word tree.
            text.Contains("sycamore") ||
            text.Contains("birch") ||
            text.Contains("beech") ||
            text.Contains("maple") ||
            // note: Mimosa is an approved dry-canopy tree whose prefab name does not contain a generic tree token.
            text.Contains("mimosa") ||
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

        // note: Visible tree instances always receive one grounded non-trigger trunk capsule after imported colliders are removed.
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
        float radius,
        YQPreparedSpatialMaterializationV2 prepared = null)
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

            // note: This XZ-only clearance check shares the scatter transaction's validated immutable projection; per-candidate global rehashing and terrain grounding add no position information.
            Vector3 settlementPosition = prepared != null &&
                prepared.TryGetSiteBySemanticId(settlement.settlementId, out YQSpatialMaterializationSiteV2 site) &&
                site.kind == YQSiteKindV2.Settlement
                ? new Vector3(site.x, 0f, site.z)
                : YQGeneratedWorldLayout
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
        float radius,
        YQPreparedSpatialMaterializationV2 prepared = null)
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

            // note: Preserve accepted camp clearance using the same immutable projection already used for water and habitat checks; unresolved sites retain the existing rejection path.
            Vector3 campPosition = prepared != null &&
                prepared.TryGetSiteBySemanticId(encampment.encampmentId, out YQSpatialMaterializationSiteV2 site) &&
                site.kind == YQSiteKindV2.HostileSite
                ? new Vector3(site.x, 0f, site.z)
                : YQGeneratedWorldLayout
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
