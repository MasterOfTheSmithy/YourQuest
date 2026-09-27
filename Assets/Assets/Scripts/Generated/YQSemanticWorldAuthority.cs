using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Versioned semantic authority for unloaded geography. It adapts accepted V1/V2
/// artifacts into one graph and derives untouched cell facts from stable absolute coordinates.
/// </summary>
public static class YQSemanticWorldAuthority
{
    public const string SchemaVersion = "semantic_world_v3";
    public const string GenerationVersion = "analytic_macro_e_v1";
    public const string CoordinateSpec = "signed_cell_128m_centered_origin_minus_512";
    public const string HashAlgorithmVersion = "fnv1a32_utf16_v1";
    public const int CellSizeMeters = 128;
    public const int QueryNeighborhoodRadiusCells = 2;
    public const int OpeningEnvelopeRadiusCells = 4;

    private const float WorldGridOrigin = -512f;
    private const int RegionSpanCells = 8;
    private const int BasinSpanCells = 16;
    private const int HugePoiSpacingCells = 30;
    private const int ContinentCount = 4;
    private const float SyntheticWorldCellRadius = 4096f;
    private const int RuntimeCellCoreCacheCapacity = 512;
    private static GeneratedSemanticWorldAuthorityRecord s_runtimeCellCoreCacheAuthority;
    private static readonly Dictionary<long, GeneratedSemanticCellPlanRecord> s_runtimeCellCoreCache =
        new Dictionary<long, GeneratedSemanticCellPlanRecord>();
    private static readonly Queue<KeyValuePair<long, GeneratedSemanticCellPlanRecord>> s_runtimeCellCoreCacheOrder =
        new Queue<KeyValuePair<long, GeneratedSemanticCellPlanRecord>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCellCoreCache()
    {
        // note: Keep only current-session derived cell projections when domain reload is disabled.
        s_runtimeCellCoreCacheAuthority = null;
        s_runtimeCellCoreCache.Clear();
        s_runtimeCellCoreCacheOrder.Clear();
    }

    public static GeneratedSemanticWorldAuthorityRecord Ensure(GeneratedWorldPlanRecord plan)
    {
        if (plan == null)
            return new GeneratedSemanticWorldAuthorityRecord();

        plan.EnsureCollections();
        string seed = SafeSeed(plan);
        string sourceFingerprint = BuildSourceFingerprint(plan);
        GeneratedSemanticWorldAuthorityRecord current = plan.semanticAuthority;
        int envelopeDiameter = OpeningEnvelopeRadiusCells * 2 + 1;
        int expectedEnvelope = envelopeDiameter * envelopeDiameter;
        if (current != null &&
            string.Equals(current.schemaVersion, SchemaVersion, StringComparison.Ordinal) &&
            string.Equals(current.generationVersion, GenerationVersion, StringComparison.Ordinal) &&
            string.Equals(current.worldSeed, seed, StringComparison.Ordinal) &&
            string.Equals(current.sourceSpatialFingerprint, sourceFingerprint, StringComparison.Ordinal) &&
            current.cellSizeMeters == CellSizeMeters &&
            current.hugePoiSpacingTargetCells == HugePoiSpacingCells &&
            current.lazyCandidateBudgetPerQuery == 9 &&
            Mathf.Abs(current.syntheticMinimumAccessScore - 0.18f) < 0.0001f &&
            string.Equals(current.syntheticFeaturePolicy, "seeded_spacing_geography_access_bounded", StringComparison.Ordinal) &&
            current.openingEnvelope != null && current.openingEnvelope.Count == expectedEnvelope &&
            current.siteReservations != null && current.routeGraph != null && current.waterNetworks != null &&
            current.acceptedOverrides != null)
        {
            current.EnsureCollections();
            // note: Migrate older accepted reservations in memory so every streamed member sector can identify the single physical owner without rerolling geography.
            NormalizeSiteOwnership(current);
            return current;
        }

        // note: Build the accepted graph before deriving cells so sites reserve footprints before routes or edge contracts are published.
        GeneratedSemanticWorldAuthorityRecord authority = new GeneratedSemanticWorldAuthorityRecord
        {
            schemaVersion = SchemaVersion,
            generationVersion = GenerationVersion,
            coordinateSpec = CoordinateSpec,
            hashAlgorithmVersion = HashAlgorithmVersion,
            worldSeed = seed,
            sourceSpatialFingerprint = sourceFingerprint,
            cellSizeMeters = CellSizeMeters,
            queryNeighborhoodRadiusCells = QueryNeighborhoodRadiusCells,
            openingEnvelopeRadiusCells = OpeningEnvelopeRadiusCells,
            hugePoiSpacingTargetCells = HugePoiSpacingCells,
            lazyCandidateBudgetPerQuery = 9,
            syntheticMinimumAccessScore = 0.18f,
            syntheticFeaturePolicy = "seeded_spacing_geography_access_bounded",
            betaDeferredFeatureKinds = new List<string> { "giant_city", "giant_dungeon", "global_political_simulation" }
        };
        // note: Capture persisted names and layout bindings before site adaptation so accepted presentation choices survive semantic migration.
        BuildAcceptedOverrides(plan, authority);
        BuildAcceptedGraph(plan, authority);
        for (int z = -OpeningEnvelopeRadiusCells; z <= OpeningEnvelopeRadiusCells; z++)
        for (int x = -OpeningEnvelopeRadiusCells; x <= OpeningEnvelopeRadiusCells; x++)
        {
            // note: Cell four, four is the logical origin cell for the authored -512 metre terrain.
            Vector2Int coordinate = new Vector2Int(4 + x, 4 + z);
            authority.openingEnvelope.Add(BuildCellWithEdges(plan, authority, coordinate));
        }
        authority.meaningfulFacts.Add(new GeneratedSemanticWorldFactRecord
        {
            factId = "world:semantic-authority",
            factKind = "semantic_authority",
            ownerId = "world",
            value = SchemaVersion + "|" + GenerationVersion,
            provenance = "generated_world_plan",
            accepted = true
        });
        authority.meaningfulFacts.Add(new GeneratedSemanticWorldFactRecord
        {
            factId = "world:huge-poi-policy",
            factKind = "provisional_spacing_target",
            ownerId = "world",
            value = HugePoiSpacingCells + "_cells|candidate_budget:9|geography_access_memory_bounded",
            provenance = "semantic_world_v2_policy",
            accepted = true
        });
        authority.meaningfulFacts.Add(new GeneratedSemanticWorldFactRecord
        {
            factId = "world:beta-deferred-feature-kinds",
            factKind = "beta_deferred_types",
            ownerId = "world",
            value = "giant_city|giant_dungeon|global_political_simulation",
            provenance = "semantic_world_v2_policy",
            accepted = true
        });
        // note: Normalize the newly compiled graph through the same path used by older saves before any cell projection is published.
        NormalizeSiteOwnership(authority);
        SortAuthority(authority);
        plan.semanticAuthority = authority;
        return authority;
    }

    public static GeneratedSemanticCellPlanRecord QueryCell(
        GeneratedWorldPlanRecord plan,
        Vector2Int coordinate)
    {
        GeneratedSemanticWorldAuthorityRecord authority = Ensure(plan);
        for (int index = 0; index < authority.openingEnvelope.Count; index++)
        {
            GeneratedSemanticCellPlanRecord cached = authority.openingEnvelope[index];
            if (cached != null && cached.cellX == coordinate.x && cached.cellZ == coordinate.y)
                return cached;
        }

        // note: Distant cells are pure deterministic derivations; they are not appended as a planet-sized mutable cache.
        return BuildCellWithEdges(plan, authority, coordinate);
    }

    public static List<GeneratedSemanticCellPlanRecord> QueryNeighborhood(
        GeneratedWorldPlanRecord plan,
        Vector2Int centre,
        int radius = QueryNeighborhoodRadiusCells)
    {
        int boundedRadius = Mathf.Clamp(radius, 0, QueryNeighborhoodRadiusCells);
        List<GeneratedSemanticCellPlanRecord> result = new List<GeneratedSemanticCellPlanRecord>();
        for (int z = centre.y - boundedRadius; z <= centre.y + boundedRadius; z++)
        for (int x = centre.x - boundedRadius; x <= centre.x + boundedRadius; x++)
            result.Add(QueryCell(plan, new Vector2Int(x, z)));
        return result;
    }

    public static bool TryGetSite(
        GeneratedWorldPlanRecord plan,
        string siteId,
        out GeneratedSemanticSiteReservationRecord site)
    {
        site = null;
        if (string.IsNullOrWhiteSpace(siteId))
            return false;
        GeneratedSemanticWorldAuthorityRecord authority = Ensure(plan);
        for (int index = 0; index < authority.siteReservations.Count; index++)
        {
            GeneratedSemanticSiteReservationRecord candidate = authority.siteReservations[index];
            if (candidate != null && string.Equals(candidate.siteId, siteId, StringComparison.Ordinal))
            {
                site = candidate;
                return true;
            }
        }

        // note: Canonical V2 has no synthetic site lookup path; an unknown identity must remain unknown instead of becoming renderer-visible content.
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
            return false;

        // note: Synthetic site IDs encode their stable spacing block, allowing legacy unloaded lookup without retaining every untouched cell.
        const string prefix = "site:frontier:";
        if (!siteId.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        string[] parts = siteId.Substring(prefix.Length).Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int blockX) || !int.TryParse(parts[1], out int blockZ))
            return false;
        site = BuildSyntheticSite(plan, authority, blockX, blockZ);
        return site != null && string.Equals(site.siteId, siteId, StringComparison.Ordinal);
    }

    public static List<GeneratedSemanticSiteReservationRecord> GetSitesForCell(
        GeneratedWorldPlanRecord plan,
        Vector2Int coordinate)
    {
        GeneratedSemanticWorldAuthorityRecord authority = Ensure(plan);
        return GetSitesForCell(authority, plan, coordinate);
    }

    private static List<GeneratedSemanticSiteReservationRecord> GetSitesForCell(
        GeneratedSemanticWorldAuthorityRecord authority,
        GeneratedWorldPlanRecord plan,
        Vector2Int coordinate)
    {
        List<GeneratedSemanticSiteReservationRecord> result = new List<GeneratedSemanticSiteReservationRecord>();
        for (int index = 0; index < authority.siteReservations.Count; index++)
        {
            GeneratedSemanticSiteReservationRecord site = authority.siteReservations[index];
            if (site != null && SiteIntersectsCell(site, coordinate))
                result.Add(site);
        }
        // note: Canonical V2 cells are closed over persisted site identities; lazy synthetic POIs remain only for non-canonical compatibility callers.
        if (!YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
        {
            int blockX = FloorDiv(coordinate.x, HugePoiSpacingCells);
            int blockZ = FloorDiv(coordinate.y, HugePoiSpacingCells);
            int candidateBudget = Mathf.Clamp(authority.lazyCandidateBudgetPerQuery, 0, 9);
            int candidatesChecked = 0;
            for (int z = blockZ - 1; z <= blockZ + 1 && candidatesChecked < candidateBudget; z++)
            for (int x = blockX - 1; x <= blockX + 1 && candidatesChecked < candidateBudget; x++)
            {
                GeneratedSemanticSiteReservationRecord synthetic = BuildSyntheticSite(plan, authority, x, z);
                candidatesChecked++;
                // note: Accepted footprints exclude lazy candidates before publication, preventing a later-loaded synthetic site from colliding with a committed site.
                if (synthetic != null && !CandidateBlockedByReservation(authority, synthetic) && SiteIntersectsCell(synthetic, coordinate))
                    AddUniqueSite(result, synthetic);
            }
        }
        result.Sort(CompareSites);
        return result;
    }

    public static List<GeneratedSemanticWaterNetworkRecord> GetWaterForCell(
        GeneratedWorldPlanRecord plan,
        Vector2Int coordinate)
    {
        GeneratedSemanticWorldAuthorityRecord authority = Ensure(plan);
        return GetWaterForCell(authority, plan, coordinate);
    }

    private static List<GeneratedSemanticWaterNetworkRecord> GetWaterForCell(
        GeneratedSemanticWorldAuthorityRecord authority,
        GeneratedWorldPlanRecord plan,
        Vector2Int coordinate)
    {
        List<GeneratedSemanticWaterNetworkRecord> result = new List<GeneratedSemanticWaterNetworkRecord>();
        for (int index = 0; index < authority.waterNetworks.Count; index++)
        {
            GeneratedSemanticWaterNetworkRecord water = authority.waterNetworks[index];
            if (water != null && PolylineIntersectsCell(water.points, coordinate, 8f))
                result.Add(water);
        }

        // note: Canonical V2 water is closed over accepted hydrology; legacy basin candidates remain available only to compatibility callers.
        if (!YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
        {
            // note: Basin candidates are bounded by a nine-block halo and use one stable basin identity across every intersected cell.
            int blockX = FloorDiv(coordinate.x, BasinSpanCells);
            int blockZ = FloorDiv(coordinate.y, BasinSpanCells);
            for (int z = blockZ - 1; z <= blockZ + 1; z++)
            for (int x = blockX - 1; x <= blockX + 1; x++)
            {
                GeneratedSemanticWaterNetworkRecord synthetic = BuildSyntheticBasin(plan, authority, x, z);
                if (synthetic != null && PolylineIntersectsCell(synthetic.points, coordinate, 12f))
                    AddUniqueWater(result, synthetic);
            }
        }
        result.Sort(CompareWaters);
        return result;
    }

    public static string BuildSourceFingerprint(GeneratedWorldPlanRecord plan)
    {
        if (plan == null)
            return string.Empty;
        StringBuilder builder = new StringBuilder(SafeSeed(plan));
        builder.Append('|').Append(plan.schemaVersion ?? string.Empty);
        builder.Append('|').Append(plan.spatialPlanV2?.contentHash ?? string.Empty);
        // note: Preserve the persisted V1 source ordering as an explicit accepted-layout input; query order itself remains independent.
        AppendIds(builder, plan.regions, region => region?.regionId + ":" + region?.terrainProfile + ":" + region?.climateProfile);
        AppendIds(builder, plan.settlements, settlement => settlement?.settlementId + ":" + settlement?.kind + ":" + settlement?.displayName + ":" + settlement?.runtimeSiteKitId + ":" + settlement?.runtimeSiteBindingVersion + ":" + LayoutSignature(settlement?.proceduralLayout));
        AppendIds(builder, plan.encampments, encampment => encampment?.encampmentId + ":" + encampment?.kind + ":" + encampment?.displayName + ":" + encampment?.runtimeSiteKitId + ":" + encampment?.runtimeSiteBindingVersion + ":" + encampment?.layoutIntent);
        AppendIds(builder, plan.pointsOfInterest, poi => poi?.poiId + ":" + poi?.kind + ":" + poi?.displayName + ":" + poi?.visualStyleKey);
        return StableHex(builder.ToString());
    }

    public static string ComputeCellHash(GeneratedSemanticCellPlanRecord cell)
    {
        if (cell == null)
            return string.Empty;
        StringBuilder builder = new StringBuilder(cell.cellId);
        Append(builder, cell.ownerRegionId, cell.continentId, cell.landformRegime, cell.paletteContext, cell.localSeed);
        Append(builder, cell.elevationIntent, cell.ruggedness, cell.temperature, cell.moisture,
            cell.ecologyForest, cell.ecologyGrassland, cell.ecologyWetland, cell.civilizationPressure,
            cell.danger, cell.culturalInfluence, cell.densityMask);
        AppendSorted(builder, cell.featureIds);
        AppendSorted(builder, cell.siteIds);
        AppendSorted(builder, cell.routeIds);
        AppendSorted(builder, cell.waterIds);
        return StableHex(builder.ToString());
    }

    public static bool TryResolveOwnerRegion(
        GeneratedWorldPlanRecord plan,
        Vector2Int coordinate,
        out string ownerRegionId)
    {
        GeneratedSemanticCellPlanRecord cell = QueryCell(plan, coordinate);
        ownerRegionId = cell?.ownerRegionId;
        return !string.IsNullOrWhiteSpace(ownerRegionId);
    }

    public static void SampleMacroFields(
        string seed,
        float worldX,
        float worldZ,
        out float elevation,
        out float ruggedness,
        out float temperature,
        out float moisture,
        out float ecologyForest,
        out float ecologyGrassland,
        out float ecologyWetland,
        out float civilization,
        out float danger,
        out float culture)
    {
        string safeSeed = string.IsNullOrWhiteSpace(seed) ? "yourquest_default_world" : seed.Trim();
        float broad = ValueNoise(worldX * 0.0018f, worldZ * 0.0018f, safeSeed, "|landform");
        float ridge = ValueNoise(worldX * 0.0031f, worldZ * 0.0031f, safeSeed, "|ridge");
        float basin = ValueNoise(worldX * 0.0012f, worldZ * 0.0012f, safeSeed, "|basin");
        float moistureField = ValueNoise(worldX * 0.0015f, worldZ * 0.0015f, safeSeed, "|climate_moisture");
        float temperatureField = ValueNoise(worldX * 0.0011f, worldZ * 0.0011f, safeSeed, "|climate_temperature");
        float ecologyField = ValueNoise(worldX * 0.0042f, worldZ * 0.0042f, safeSeed, "|ecology");
        float cultureField = ValueNoise(worldX * 0.0017f, worldZ * 0.0017f, safeSeed, "|culture");
        float dangerField = ValueNoise(worldX * 0.0029f, worldZ * 0.0029f, safeSeed, "|danger");

        // note: Each domain has a distinct stable seed stream; ecology, culture, danger, and climate are not aliases for biome labels.
        ruggedness = Mathf.Clamp01(Mathf.Abs(ridge * 2f - 1f) * 0.62f + broad * 0.18f);
        elevation = Mathf.Clamp01(0.28f + broad * 0.18f + ruggedness * 0.24f - basin * 0.11f);
        temperature = Mathf.Clamp01(temperatureField * 0.82f + 0.09f);
        moisture = Mathf.Clamp01(moistureField * 0.82f + basin * 0.14f);
        float forest = Mathf.Clamp01((moisture * 0.72f + ecologyField * 0.28f) * (1f - temperature * 0.22f));
        float wetland = Mathf.Clamp01(moisture * 0.72f + (1f - elevation) * 0.2f + (1f - ecologyField) * 0.08f);
        float grassland = Mathf.Clamp01((1f - ruggedness) * 0.46f + (1f - wetland) * 0.34f + ecologyField * 0.2f);
        float total = Mathf.Max(0.001f, forest + grassland + wetland);
        ecologyForest = forest / total;
        ecologyGrassland = grassland / total;
        ecologyWetland = wetland / total;
        civilization = Mathf.Clamp01((1f - ruggedness) * 0.42f + cultureField * 0.36f + ecologyGrassland * 0.22f);
        danger = Mathf.Clamp01(dangerField * 0.58f + ruggedness * 0.27f + (1f - civilization) * 0.15f);
        culture = Mathf.Clamp01(cultureField * 0.8f + ecologyGrassland * 0.1f + (1f - danger) * 0.1f);
    }

    private static GeneratedSemanticCellPlanRecord BuildCellWithEdges(
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticWorldAuthorityRecord authority,
        Vector2Int coordinate)
    {
        // note: Clone the requested core before adding edge contracts so cached neighbors stay immutable.
        GeneratedSemanticCellPlanRecord cell = CloneCellCore(GetOrBuildCellCore(plan, authority, coordinate));
        string[] edges = { "west", "east", "south", "north" };
        for (int index = 0; index < edges.Length; index++)
        {
            Vector2Int neighbour = coordinate;
            if (edges[index] == "west") neighbour.x--;
            else if (edges[index] == "east") neighbour.x++;
            else if (edges[index] == "south") neighbour.y--;
            else neighbour.y++;
            GeneratedSemanticCellPlanRecord other = GetOrBuildCellCore(plan, authority, neighbour);
            cell.edgeContracts.Add(BuildBoundary(cell, other, coordinate, edges[index]));
        }
        cell.semanticHash = ComputeCellHash(cell);
        return cell;
    }

    private static GeneratedSemanticCellPlanRecord GetOrBuildCellCore(
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticWorldAuthorityRecord authority,
        Vector2Int coordinate)
    {
        if (!ReferenceEquals(s_runtimeCellCoreCacheAuthority, authority))
        {
            // note: Accepted authority identity is the cache lifetime boundary; replacing a world cannot reuse another world's derived cells.
            s_runtimeCellCoreCacheAuthority = authority;
            s_runtimeCellCoreCache.Clear();
            s_runtimeCellCoreCacheOrder.Clear();
        }

        long key = ((long)coordinate.x << 32) | (uint)coordinate.y;
        if (s_runtimeCellCoreCache.TryGetValue(key, out GeneratedSemanticCellPlanRecord cached))
            return cached;

        GeneratedSemanticCellPlanRecord created = BuildCellCore(plan, authority, coordinate);
        while (s_runtimeCellCoreCache.Count >= RuntimeCellCoreCacheCapacity &&
               s_runtimeCellCoreCacheOrder.Count > 0)
        {
            KeyValuePair<long, GeneratedSemanticCellPlanRecord> oldest = s_runtimeCellCoreCacheOrder.Dequeue();
            if (s_runtimeCellCoreCache.TryGetValue(oldest.Key, out GeneratedSemanticCellPlanRecord current) &&
                ReferenceEquals(current, oldest.Value))
                s_runtimeCellCoreCache.Remove(oldest.Key);
        }

        s_runtimeCellCoreCache[key] = created;
        s_runtimeCellCoreCacheOrder.Enqueue(new KeyValuePair<long, GeneratedSemanticCellPlanRecord>(key, created));
        return created;
    }

    private static GeneratedSemanticCellPlanRecord CloneCellCore(GeneratedSemanticCellPlanRecord source)
    {
        if (source == null)
            return new GeneratedSemanticCellPlanRecord();

        // note: Query callers receive an independently mutable cell even though neighboring authority facts are memoized.
        GeneratedSemanticCellPlanRecord clone = new GeneratedSemanticCellPlanRecord
        {
            cellId = source.cellId,
            cellX = source.cellX,
            cellZ = source.cellZ,
            ownerRegionId = source.ownerRegionId,
            continentId = source.continentId,
            landformRegime = source.landformRegime,
            elevationIntent = source.elevationIntent,
            ruggedness = source.ruggedness,
            temperature = source.temperature,
            moisture = source.moisture,
            ecologyForest = source.ecologyForest,
            ecologyGrassland = source.ecologyGrassland,
            ecologyWetland = source.ecologyWetland,
            civilizationPressure = source.civilizationPressure,
            danger = source.danger,
            culturalInfluence = source.culturalInfluence,
            paletteContext = source.paletteContext,
            densityMask = source.densityMask,
            localSeed = source.localSeed,
            routeConstraint = source.routeConstraint,
            waterConstraint = source.waterConstraint,
            semanticHash = source.semanticHash,
            featureIds = new List<string>(source.featureIds ?? new List<string>()),
            siteIds = new List<string>(source.siteIds ?? new List<string>()),
            routeIds = new List<string>(source.routeIds ?? new List<string>()),
            waterIds = new List<string>(source.waterIds ?? new List<string>()),
            edgeContracts = new List<GeneratedSemanticBoundaryRecord>()
        };
        for (int index = 0; source.edgeContracts != null && index < source.edgeContracts.Count; index++)
        {
            GeneratedSemanticBoundaryRecord edge = source.edgeContracts[index];
            if (edge == null)
            {
                clone.edgeContracts.Add(null);
                continue;
            }

            clone.edgeContracts.Add(new GeneratedSemanticBoundaryRecord
            {
                edge = edge.edge,
                canonicalKey = edge.canonicalKey,
                neighborCellId = edge.neighborCellId,
                contractHash = edge.contractHash,
                featureIds = new List<string>(edge.featureIds ?? new List<string>()),
                routeIds = new List<string>(edge.routeIds ?? new List<string>()),
                waterIds = new List<string>(edge.waterIds ?? new List<string>())
            });
        }
        return clone;
    }

    private static GeneratedSemanticCellPlanRecord BuildCellCore(
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticWorldAuthorityRecord authority,
        Vector2Int coordinate)
    {
        float centreX = WorldGridOrigin + coordinate.x * CellSizeMeters + CellSizeMeters * 0.5f;
        float centreZ = WorldGridOrigin + coordinate.y * CellSizeMeters + CellSizeMeters * 0.5f;
        SampleMacroFields(authority.worldSeed, centreX, centreZ, out float elevation, out float ruggedness,
            out float temperature, out float moisture, out float forest, out float grassland, out float wetland,
            out float civilization, out float danger, out float culture);
        ResolveContinent(authority.worldSeed, centreX, centreZ, out string continentId, out float continentMask);
        string ownerRegionId = ResolveRegion(plan, authority.worldSeed, coordinate, centreX, centreZ, continentId);
        string landform = ruggedness > 0.68f ? "ridge_system" : wetland > 0.47f ? "wetland_basin" :
            elevation < 0.32f ? "valley_plain" : "rolling_upland";
        GeneratedSemanticCellPlanRecord cell = new GeneratedSemanticCellPlanRecord
        {
            cellId = BuildCellId(coordinate),
            cellX = coordinate.x,
            cellZ = coordinate.y,
            ownerRegionId = ownerRegionId,
            continentId = continentId,
            landformRegime = landform,
            elevationIntent = elevation,
            ruggedness = ruggedness,
            temperature = temperature,
            moisture = moisture,
            ecologyForest = forest,
            ecologyGrassland = grassland,
            ecologyWetland = wetland,
            civilizationPressure = civilization,
            danger = danger,
            culturalInfluence = culture,
            paletteContext = BuildPaletteContext(continentId, landform, temperature, moisture),
            densityMask = Mathf.Clamp01(civilization * 0.55f + (1f - ruggedness) * 0.25f + forest * 0.2f),
            localSeed = authority.worldSeed + "|cell|" + coordinate.x + "|" + coordinate.y + "|" + GenerationVersion,
            routeConstraint = "no_route_synthesis",
            waterConstraint = "no_edge_water_roll"
        };
        cell.featureIds.Add("region:" + ownerRegionId);
        cell.featureIds.Add("landform:" + landform);
        if (!string.Equals(continentId, "continent:ocean", StringComparison.Ordinal))
        {
            cell.featureIds.Add(continentId);
            if (continentMask > 0.2f)
                cell.featureIds.Add("macro:connected_landmass");
        }
        List<GeneratedSemanticSiteReservationRecord> sites = GetSitesForCell(authority, plan, coordinate);
        for (int index = 0; index < sites.Count; index++)
            AddSortedId(cell.siteIds, sites[index]?.siteId);
        for (int index = 0; index < authority.routeGraph.Count; index++)
        {
            GeneratedSemanticRouteGraphRecord route = authority.routeGraph[index];
            if (route != null && PolylineIntersectsCell(route.points, coordinate, 4f))
                AddSortedId(cell.routeIds, route.routeId);
        }
        List<GeneratedSemanticWaterNetworkRecord> waters = GetWaterForCell(authority, plan, coordinate);
        for (int index = 0; index < waters.Count; index++)
            AddSortedId(cell.waterIds, waters[index]?.waterId);
        if (cell.routeIds.Count > 0)
            cell.routeConstraint = "accepted_destination_routes";
        else if (cell.siteIds.Count > 0)
            cell.routeConstraint = "permitted_terminal_only";
        if (cell.waterIds.Count > 0)
            cell.waterConstraint = "shared_basin_source_downstream_sink";
        return cell;
    }

    private static GeneratedSemanticBoundaryRecord BuildBoundary(
        GeneratedSemanticCellPlanRecord first,
        GeneratedSemanticCellPlanRecord second,
        Vector2Int coordinate,
        string edge)
    {
        List<string> features = Union(first.featureIds, second.featureIds);
        List<string> routes = Union(first.routeIds, second.routeIds);
        List<string> waters = Union(first.waterIds, second.waterIds);
        bool vertical = edge == "west" || edge == "east";
        int boundaryIndex = vertical ? (edge == "west" ? coordinate.x : coordinate.x + 1) : (edge == "south" ? coordinate.y : coordinate.y + 1);
        int spanIndex = vertical ? coordinate.y : coordinate.x;
        string canonicalKey = "boundary:" + (vertical ? "x" : "z") + ":" + boundaryIndex + ":span:" + spanIndex;
        // note: The hash excludes which side asked the question, so east/west and north/south lookups share one crossing contract.
        StringBuilder builder = new StringBuilder(canonicalKey);
        AppendSorted(builder, features);
        AppendSorted(builder, routes);
        AppendSorted(builder, waters);
        return new GeneratedSemanticBoundaryRecord
        {
            edge = edge,
            canonicalKey = canonicalKey,
            neighborCellId = second.cellId,
            featureIds = features,
            routeIds = routes,
            waterIds = waters,
            contractHash = StableHex(builder.ToString())
        };
    }

    private static void BuildAcceptedGraph(
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticWorldAuthorityRecord authority)
    {
        bool acceptedV2 = YQSpatialPlanVersionRouter.TryValidateAcceptedV2(plan, out _);
        if (acceptedV2)
            BuildV2Graph(plan, authority);
        else
            BuildV1Graph(plan, authority);
        SortAuthority(authority);
    }

    private static void BuildAcceptedOverrides(
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticWorldAuthorityRecord authority)
    {
        authority.acceptedOverrides.Clear();
        if (plan == null)
            return;

        // note: Copy accepted settlement presentation and layout choices into the semantic envelope before any cell is queried.
        for (int index = 0; index < plan.settlements.Count; index++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[index];
            if (settlement == null)
                continue;
            AddAcceptedOverride(
                authority,
                settlement.settlementId,
                settlement.displayName,
                string.IsNullOrWhiteSpace(settlement.runtimeSiteKitId)
                    ? settlement.proceduralLayout?.kitId
                    : settlement.runtimeSiteKitId,
                settlement.runtimeSiteBindingVersion,
                LayoutSignature(settlement.proceduralLayout),
                "persisted_generated_settlement");
        }

        // note: Encampment layout intent and site bindings are persisted as semantic inputs even when no physical sector is loaded.
        for (int index = 0; index < plan.encampments.Count; index++)
        {
            GeneratedEncampmentRecord encampment = plan.encampments[index];
            if (encampment == null)
                continue;
            AddAcceptedOverride(
                authority,
                encampment.encampmentId,
                encampment.displayName,
                string.IsNullOrWhiteSpace(encampment.runtimeSiteKitId)
                    ? encampment.layoutIntent
                    : encampment.runtimeSiteKitId,
                encampment.runtimeSiteBindingVersion,
                encampment.layoutIntent,
                "persisted_generated_encampment");
        }

        // note: POI names and approved visual intents are durable presentation choices, not cell-local random decorations.
        for (int index = 0; index < plan.pointsOfInterest.Count; index++)
        {
            GeneratedPointOfInterestRecord poi = plan.pointsOfInterest[index];
            if (poi == null)
                continue;
            AddAcceptedOverride(
                authority,
                poi.poiId,
                poi.displayName,
                poi.visualStyleKey,
                "poi_visual_style_v1",
                string.Empty,
                "persisted_generated_poi");
        }
    }

    private static void AddAcceptedOverride(
        GeneratedSemanticWorldAuthorityRecord authority,
        string objectId,
        string displayName,
        string layoutBindingId,
        string layoutBindingVersion,
        string layoutSignature,
        string provenance)
    {
        if (authority == null || string.IsNullOrWhiteSpace(objectId))
            return;
        for (int index = 0; index < authority.acceptedOverrides.Count; index++)
            if (authority.acceptedOverrides[index] != null &&
                string.Equals(authority.acceptedOverrides[index].objectId, objectId, StringComparison.Ordinal))
                return;
        authority.acceptedOverrides.Add(new GeneratedSemanticAcceptedOverrideRecord
        {
            objectId = objectId,
            displayName = displayName ?? string.Empty,
            layoutBindingId = layoutBindingId ?? string.Empty,
            layoutBindingVersion = layoutBindingVersion ?? string.Empty,
            layoutSignature = layoutSignature ?? string.Empty,
            provenance = provenance ?? string.Empty,
            accepted = true
        });
    }

    private static void ApplyAcceptedSiteMetadata(
        GeneratedSemanticWorldAuthorityRecord authority,
        GeneratedSemanticSiteReservationRecord site,
        params string[] candidateIds)
    {
        if (authority == null || site == null || candidateIds == null)
            return;
        for (int candidateIndex = 0; candidateIndex < candidateIds.Length; candidateIndex++)
        {
            string candidateId = candidateIds[candidateIndex];
            if (string.IsNullOrWhiteSpace(candidateId))
                continue;
            for (int index = 0; index < authority.acceptedOverrides.Count; index++)
            {
                GeneratedSemanticAcceptedOverrideRecord accepted = authority.acceptedOverrides[index];
                if (accepted == null || !string.Equals(accepted.objectId, candidateId, StringComparison.Ordinal))
                    continue;
                site.displayName = accepted.displayName;
                site.layoutBindingId = accepted.layoutBindingId;
                site.layoutBindingVersion = accepted.layoutBindingVersion;
                site.layoutSignature = accepted.layoutSignature;
                return;
            }
        }
    }

    private static string LayoutSignature(YQProceduralSettlementLayoutRecord layout)
    {
        return layout == null ? string.Empty : YQProceduralSettlementLayout.GeometrySignature(layout);
    }

    private static void BuildV1Graph(GeneratedWorldPlanRecord plan, GeneratedSemanticWorldAuthorityRecord authority)
    {
        GeneratedSpatialWorldPlanRecord spatial = YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan);
        for (int index = 0; index < spatial.locations.Count; index++)
        {
            GeneratedSpatialLocationRecord location = spatial.locations[index];
            if (location == null || string.IsNullOrWhiteSpace(location.locationId))
                continue;
            GeneratedSemanticSiteReservationRecord reservation = CreateSite(
                location.locationId, location.parentRegionId, location.locationKind, location.structuralArchetype,
                location.worldX, location.worldZ, Mathf.Max(12f, location.footprintRadius),
                location.entranceHeadingDegrees, true, "spatial_plan_v1");
            ApplyAcceptedSiteMetadata(authority, reservation, location.locationId);
            authority.siteReservations.Add(reservation);
        }
        for (int index = 0; index < spatial.routes.Count; index++)
        {
            GeneratedSpatialRouteRecord route = spatial.routes[index];
            if (route == null || string.IsNullOrWhiteSpace(route.routeId))
                continue;
            GeneratedSemanticRouteGraphRecord semantic = new GeneratedSemanticRouteGraphRecord
            {
                routeId = route.routeId,
                parentRouteId = route.routeId,
                ownerRegionId = route.parentRegionId,
                fromSiteId = route.fromLocationId,
                toSiteId = route.toLocationId,
                routeClass = route.routeKind,
                accepted = true,
                permittedBoundaryContinuation = false
            };
            for (int pointIndex = 0; pointIndex < route.waypoints.Count; pointIndex++)
                semantic.points.Add(new GeneratedSemanticRoutePointRecord { worldX = route.waypoints[pointIndex].x, worldZ = route.waypoints[pointIndex].z, cost = route.waypoints[pointIndex].cost });
            authority.routeGraph.Add(semantic);
        }
        for (int index = 0; index < spatial.macroFeatures.Count; index++)
        {
            GeneratedSpatialFeatureRecord feature = spatial.macroFeatures[index];
            if (feature == null || feature.featureKind != "lake_basin")
                continue;
            GeneratedSemanticWaterNetworkRecord water = new GeneratedSemanticWaterNetworkRecord
            {
                waterId = "water:basin:" + feature.featureId,
                basinId = "basin:" + feature.featureId,
                sourceId = "source:" + feature.featureId,
                downstreamWaterId = string.Empty,
                sinkId = "sink:" + feature.featureId,
                ownerRegionId = feature.parentRegionId,
                kind = "lake",
                surfaceElevation = feature.waterLevel,
                nominalWidth = Mathf.Max(feature.radiusX, feature.radiusZ) * 2f,
                nominalDepth = 2f,
                accepted = true,
                declaredSink = true
            };
            water.points.Add(new GeneratedSemanticRoutePointRecord { worldX = feature.centerX, worldZ = feature.centerZ });
            authority.waterNetworks.Add(water);
        }
    }

    private static void BuildV2Graph(GeneratedWorldPlanRecord plan, GeneratedSemanticWorldAuthorityRecord authority)
    {
        YQSpatialBlueprintV2 blueprint = plan.spatialPlanV2?.blueprint;
        if (blueprint == null)
            return;
        blueprint.EnsureCollections();
        for (int index = 0; index < blueprint.sites.Count; index++)
        {
            YQSiteAnchorV2 site = blueprint.sites[index];
            if (site == null || string.IsNullOrWhiteSpace(site.siteId))
                continue;
            GeneratedSemanticSiteReservationRecord reservation = CreateSite(
                site.siteId, site.parentRegionId, site.kind.ToString(), site.kind.ToString(), site.x, site.z,
                Mathf.Max(12f, site.reservedRadius), site.preferredHeadingDegrees, true, "accepted_spatial_v2");
            ApplyAcceptedSiteMetadata(authority, reservation, site.sourceSemanticId, site.siteId);
            authority.siteReservations.Add(reservation);
        }
        for (int index = 0; index < blueprint.routes.Count; index++)
        {
            YQRouteCorridorV2 route = blueprint.routes[index];
            if (route == null || string.IsNullOrWhiteSpace(route.routeId))
                continue;
            GeneratedSemanticRouteGraphRecord semantic = new GeneratedSemanticRouteGraphRecord
            {
                routeId = route.routeId,
                parentRouteId = string.IsNullOrWhiteSpace(route.sourceSemanticRouteId) ? route.routeId : route.sourceSemanticRouteId,
                ownerRegionId = route.parentRegionId,
                fromSiteId = route.fromSiteId,
                toSiteId = route.toSiteId,
                routeClass = route.routeClass.ToString(),
                accepted = true,
                permittedBoundaryContinuation = true
            };
            for (int pointIndex = 0; pointIndex < route.controlPoints.Count; pointIndex++)
            {
                YQBlueprintPointV2 point = route.controlPoints[pointIndex];
                semantic.points.Add(new GeneratedSemanticRoutePointRecord { worldX = point.x, worldZ = point.z, cost = pointIndex });
            }
            for (int crossingIndex = 0; crossingIndex < route.crossings.Count; crossingIndex++)
            {
                YQRouteCrossingV2 crossing = route.crossings[crossingIndex];
                semantic.crossings.Add(new GeneratedSemanticRouteCrossingRecord
                {
                    crossingId = crossing.crossingId,
                    waterId = crossing.hydrologyId,
                    crossingKind = crossing.kind.ToString(),
                    worldX = crossing.x,
                    worldZ = crossing.z,
                    requiredSpan = crossing.requiredSpan
                });
            }
            authority.routeGraph.Add(semantic);
        }
        for (int index = 0; index < blueprint.hydrology.Count; index++)
        {
            YQHydrologyFeatureV2 source = blueprint.hydrology[index];
            if (source == null || string.IsNullOrWhiteSpace(source.hydrologyId))
                continue;
            GeneratedSemanticWaterNetworkRecord water = new GeneratedSemanticWaterNetworkRecord
            {
                waterId = source.hydrologyId,
                basinId = "basin:" + (string.IsNullOrWhiteSpace(source.parentRegionId) ? source.hydrologyId : source.parentRegionId),
                sourceId = "source:" + source.hydrologyId,
                downstreamWaterId = source.sinkHydrologyId,
                sinkId = string.IsNullOrWhiteSpace(source.sinkHydrologyId) ? "sink:" + source.hydrologyId : source.sinkHydrologyId,
                ownerRegionId = source.parentRegionId,
                kind = source.kind.ToString(),
                surfaceElevation = source.waterLevelNormalized,
                nominalWidth = Mathf.Max(1f, source.nominalWidth),
                nominalDepth = Mathf.Max(0.1f, source.nominalDepth),
                accepted = true,
                declaredSink = string.IsNullOrWhiteSpace(source.sinkHydrologyId)
            };
            for (int pointIndex = 0; pointIndex < source.controlPoints.Count; pointIndex++)
            {
                YQBlueprintPointV2 point = source.controlPoints[pointIndex];
                water.points.Add(new GeneratedSemanticRoutePointRecord { worldX = point.x, worldZ = point.z, cost = pointIndex });
            }
            authority.waterNetworks.Add(water);
        }
        // note: Convert accepted route crossings into water-owned crossing records so bridge/f​​ord consumers share the same persistent water identity.
        AttachRouteCrossingsToWater(authority);
        for (int routeIndex = 0; routeIndex < authority.routeGraph.Count; routeIndex++)
        {
            GeneratedSemanticRouteGraphRecord route = authority.routeGraph[routeIndex];
            AttachRouteToSite(authority, route.fromSiteId, route.routeId);
            AttachRouteToSite(authority, route.toSiteId, route.routeId);
        }
    }

    private static void AttachRouteCrossingsToWater(GeneratedSemanticWorldAuthorityRecord authority)
    {
        for (int routeIndex = 0; routeIndex < authority.routeGraph.Count; routeIndex++)
        {
            GeneratedSemanticRouteGraphRecord route = authority.routeGraph[routeIndex];
            if (route?.crossings == null)
                continue;
            for (int crossingIndex = 0; crossingIndex < route.crossings.Count; crossingIndex++)
            {
                GeneratedSemanticRouteCrossingRecord crossing = route.crossings[crossingIndex];
                if (crossing == null || string.IsNullOrWhiteSpace(crossing.waterId))
                    continue;
                GeneratedSemanticWaterNetworkRecord water = null;
                for (int waterIndex = 0; waterIndex < authority.waterNetworks.Count; waterIndex++)
                {
                    if (authority.waterNetworks[waterIndex] != null &&
                        string.Equals(authority.waterNetworks[waterIndex].waterId, crossing.waterId, StringComparison.Ordinal))
                    {
                        water = authority.waterNetworks[waterIndex];
                        break;
                    }
                }
                if (water == null)
                    continue;
                bool duplicate = false;
                for (int existingIndex = 0; existingIndex < water.crossings.Count; existingIndex++)
                    duplicate |= water.crossings[existingIndex] != null && water.crossings[existingIndex].crossingId == crossing.crossingId;
                if (duplicate)
                    continue;
                ResolveFlow(water, out float flowX, out float flowZ);
                water.crossings.Add(new GeneratedSemanticWaterCrossingRecord
                {
                    crossingId = crossing.crossingId,
                    cellX = FloorDiv(Mathf.FloorToInt((crossing.worldX - WorldGridOrigin) / CellSizeMeters), 1),
                    cellZ = FloorDiv(Mathf.FloorToInt((crossing.worldZ - WorldGridOrigin) / CellSizeMeters), 1),
                    worldX = crossing.worldX,
                    worldZ = crossing.worldZ,
                    width = Mathf.Max(water.nominalWidth, crossing.requiredSpan),
                    surfaceElevation = water.surfaceElevation,
                    bedElevation = water.surfaceElevation - water.nominalDepth,
                    flowX = flowX,
                    flowZ = flowZ,
                    upstreamWaterId = water.sourceId,
                    downstreamWaterId = string.IsNullOrWhiteSpace(water.downstreamWaterId) ? water.sinkId : water.downstreamWaterId
                });
            }
        }
    }

    private static void ResolveFlow(
        GeneratedSemanticWaterNetworkRecord water,
        out float flowX,
        out float flowZ)
    {
        flowX = 0f;
        flowZ = 0f;
        if (water?.points == null || water.points.Count < 2)
            return;
        GeneratedSemanticRoutePointRecord first = water.points[0];
        GeneratedSemanticRoutePointRecord last = water.points[water.points.Count - 1];
        Vector2 direction = new Vector2(last.worldX - first.worldX, last.worldZ - first.worldZ);
        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();
            flowX = direction.x;
            flowZ = direction.y;
        }
    }

    private static GeneratedSemanticSiteReservationRecord CreateSite(
        string siteId, string regionId, string kind, string intent, float x, float z, float radius,
        float heading, bool accepted, string provenance)
    {
        float radians = heading * Mathf.Deg2Rad;
        GeneratedSemanticSiteReservationRecord site = new GeneratedSemanticSiteReservationRecord
        {
            siteId = siteId,
            ownerRegionId = regionId,
            ownerFeatureId = siteId,
            siteKind = kind ?? "site",
            structuralIntent = intent ?? kind ?? "site",
            worldX = x,
            worldZ = z,
            footprintRadius = radius,
            minimumExclusionRadius = radius + 18f,
            expansionRadius = radius * 1.35f,
            culturalIntent = "derived_from_accepted_semantic_site",
            immutable = true,
            accepted = accepted,
            provenance = provenance,
            accessScore = 1f,
            accessConstraint = accepted ? "accepted_layout_access" : string.Empty
        };
        site.memberCellIds = CellIdsForBounds(x - radius, x + radius, z - radius, z + radius);
        site.ownerCellId = OwnerCellIdForPosition(x, z);
        site.entrances.Add(new GeneratedSemanticEntranceRecord
        {
            entranceId = siteId + "|entrance|0",
            worldX = x + Mathf.Sin(radians) * radius,
            worldZ = z + Mathf.Cos(radians) * radius,
            headingDegrees = heading,
            permittedRouteId = string.Empty
        });
        return site;
    }

    public static bool IsOwnerCell(
        GeneratedSemanticSiteReservationRecord site,
        Vector2Int coordinate)
    {
        // note: Member-sector activation may inspect this stable predicate without reconstructing bounds or choosing a new owner from load order.
        return site != null &&
            string.Equals(site.ownerCellId, BuildCellId(coordinate), StringComparison.Ordinal);
    }

    private static void NormalizeSiteOwnership(
        GeneratedSemanticWorldAuthorityRecord authority)
    {
        if (authority?.siteReservations == null)
            return;
        for (int index = 0; index < authority.siteReservations.Count; index++)
        {
            GeneratedSemanticSiteReservationRecord site = authority.siteReservations[index];
            if (site == null)
                continue;
            site.EnsureCollections();
            if (site.memberCellIds.Count == 0 && site.footprintRadius > 0f)
            {
                // note: Reconstruct only missing derived footprint metadata; accepted anchors and radii remain untouched.
                site.memberCellIds = CellIdsForBounds(
                    site.worldX - site.footprintRadius,
                    site.worldX + site.footprintRadius,
                    site.worldZ - site.footprintRadius,
                    site.worldZ + site.footprintRadius);
            }
            if (string.IsNullOrWhiteSpace(site.ownerCellId))
                site.ownerCellId = OwnerCellIdForPosition(site.worldX, site.worldZ);
            site.memberCellIds.Sort(StringComparer.Ordinal);
        }
    }

    private static string OwnerCellIdForPosition(float worldX, float worldZ)
    {
        int cellX = Mathf.FloorToInt((worldX - WorldGridOrigin) / CellSizeMeters);
        int cellZ = Mathf.FloorToInt((worldZ - WorldGridOrigin) / CellSizeMeters);
        return BuildCellId(new Vector2Int(cellX, cellZ));
    }

    private static GeneratedSemanticSiteReservationRecord BuildSyntheticSite(
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticWorldAuthorityRecord authority,
        int blockX,
        int blockZ)
    {
        float offsetX = 0.16f + Hash01(authority.worldSeed + "|huge-poi|" + blockX + "|" + blockZ + "|x") * 0.68f;
        float offsetZ = 0.16f + Hash01(authority.worldSeed + "|huge-poi|" + blockX + "|" + blockZ + "|z") * 0.68f;
        int candidateX = blockX * HugePoiSpacingCells + Mathf.RoundToInt(offsetX * (HugePoiSpacingCells - 1));
        int candidateZ = blockZ * HugePoiSpacingCells + Mathf.RoundToInt(offsetZ * (HugePoiSpacingCells - 1));
        float worldX = WorldGridOrigin + candidateX * CellSizeMeters + CellSizeMeters * 0.5f;
        float worldZ = WorldGridOrigin + candidateZ * CellSizeMeters + CellSizeMeters * 0.5f;
        SampleMacroFields(authority.worldSeed, worldX, worldZ, out _, out float ruggedness, out _, out float moisture,
            out _, out _, out float wetland, out float civilization, out float danger, out _);
        ResolveContinent(authority.worldSeed, worldX, worldZ, out string continentId, out float continentMask);
        if (continentId == "continent:ocean" || continentMask < 0.18f || ruggedness > 0.9f || wetland > 0.84f)
            return null;
        float accessScore = Mathf.Clamp01(civilization * 0.7f + (1f - ruggedness) * 0.3f);
        if (accessScore < authority.syntheticMinimumAccessScore)
            return null;
        string siteId = "site:frontier:" + blockX + ":" + blockZ;
        string regionId = "region:" + continentId + ":" + FloorDiv(candidateX, RegionSpanCells) + ":" + FloorDiv(candidateZ, RegionSpanCells);
        string kind = danger > 0.64f ? "hostile_site" : civilization > 0.55f ? "settlement" : "landmark";
        float radius = kind == "settlement" ? 64f : 36f;
        GeneratedSemanticSiteReservationRecord result = CreateSite(siteId, regionId, kind, "seeded_spacing_target", worldX, worldZ, radius,
            Hash01(authority.worldSeed + "|huge-poi|" + blockX + "|" + blockZ + "|heading") * 360f, false, "lazy_seeded_candidate");
        result.ownerFeatureId = continentId;
        result.culturalIntent = "seeded_geography_access_candidate";
        result.accessScore = accessScore;
        result.accessConstraint = "minimum_access_score:" + authority.syntheticMinimumAccessScore.ToString("0.00", CultureInfo.InvariantCulture);
        return result;
    }

    private static bool CandidateBlockedByReservation(
        GeneratedSemanticWorldAuthorityRecord authority,
        GeneratedSemanticSiteReservationRecord candidate)
    {
        if (authority == null || candidate == null)
            return false;
        for (int index = 0; index < authority.siteReservations.Count; index++)
        {
            GeneratedSemanticSiteReservationRecord accepted = authority.siteReservations[index];
            if (accepted == null || accepted.siteId == candidate.siteId)
                continue;
            float exclusion = Mathf.Max(accepted.minimumExclusionRadius, accepted.footprintRadius) + candidate.footprintRadius;
            float distance = Vector2.Distance(
                new Vector2(accepted.worldX, accepted.worldZ),
                new Vector2(candidate.worldX, candidate.worldZ));
            if (distance < exclusion)
                return true;
        }
        return false;
    }

    private static GeneratedSemanticWaterNetworkRecord BuildSyntheticBasin(
        GeneratedWorldPlanRecord plan,
        GeneratedSemanticWorldAuthorityRecord authority,
        int blockX,
        int blockZ)
    {
        int candidateX = blockX * BasinSpanCells + Mathf.RoundToInt(Hash01(authority.worldSeed + "|basin|" + blockX + "|" + blockZ + "|x") * (BasinSpanCells - 1));
        int candidateZ = blockZ * BasinSpanCells + Mathf.RoundToInt(Hash01(authority.worldSeed + "|basin|" + blockX + "|" + blockZ + "|z") * (BasinSpanCells - 1));
        float worldX = WorldGridOrigin + candidateX * CellSizeMeters + CellSizeMeters * 0.5f;
        float worldZ = WorldGridOrigin + candidateZ * CellSizeMeters + CellSizeMeters * 0.5f;
        SampleMacroFields(authority.worldSeed, worldX, worldZ, out float elevation, out _, out _, out float moisture,
            out _, out _, out _, out _, out _, out _);
        ResolveContinent(authority.worldSeed, worldX, worldZ, out string continentId, out float continentMask);
        if (continentId == "continent:ocean" || continentMask < 0.12f || moisture < 0.64f)
            return null;
        float radius = CellSizeMeters * Mathf.Lerp(0.7f, 1.8f, Hash01(authority.worldSeed + "|basin|" + blockX + "|" + blockZ + "|radius"));
        GeneratedSemanticWaterNetworkRecord result = new GeneratedSemanticWaterNetworkRecord
        {
            waterId = "water:frontier_basin:" + blockX + ":" + blockZ,
            basinId = "basin:frontier:" + blockX + ":" + blockZ,
            sourceId = "source:frontier:" + blockX + ":" + blockZ,
            downstreamWaterId = string.Empty,
            sinkId = "sink:frontier:" + blockX + ":" + blockZ,
            ownerRegionId = "region:" + continentId + ":" + FloorDiv(candidateX, RegionSpanCells) + ":" + FloorDiv(candidateZ, RegionSpanCells),
            kind = moisture > 0.78f ? "wetland" : "lake",
            surfaceElevation = Mathf.Clamp01(elevation - 0.035f),
            nominalWidth = radius * 1.5f,
            nominalDepth = 1.5f,
            accepted = false,
            declaredSink = true
        };
        result.points.Add(new GeneratedSemanticRoutePointRecord { worldX = worldX, worldZ = worldZ });
        return result;
    }

    private static void SortAuthority(GeneratedSemanticWorldAuthorityRecord authority)
    {
        authority.openingEnvelope.Sort((a, b) => a.cellZ != b.cellZ ? a.cellZ.CompareTo(b.cellZ) : a.cellX.CompareTo(b.cellX));
        authority.siteReservations.Sort(CompareSites);
        authority.routeGraph.Sort((a, b) => string.CompareOrdinal(a?.routeId ?? string.Empty, b?.routeId ?? string.Empty));
        authority.waterNetworks.Sort(CompareWaters);
        authority.meaningfulFacts.Sort((a, b) => string.CompareOrdinal(a?.factId ?? string.Empty, b?.factId ?? string.Empty));
        authority.acceptedOverrides.Sort((a, b) => string.CompareOrdinal(a?.objectId ?? string.Empty, b?.objectId ?? string.Empty));
    }

    private static int CompareSites(GeneratedSemanticSiteReservationRecord a, GeneratedSemanticSiteReservationRecord b) => string.CompareOrdinal(a?.siteId ?? string.Empty, b?.siteId ?? string.Empty);
    private static int CompareWaters(GeneratedSemanticWaterNetworkRecord a, GeneratedSemanticWaterNetworkRecord b) => string.CompareOrdinal(a?.waterId ?? string.Empty, b?.waterId ?? string.Empty);

    private static void AttachRouteToSite(GeneratedSemanticWorldAuthorityRecord authority, string siteId, string routeId)
    {
        if (string.IsNullOrWhiteSpace(siteId) || string.IsNullOrWhiteSpace(routeId))
            return;
        for (int index = 0; index < authority.siteReservations.Count; index++)
        {
            GeneratedSemanticSiteReservationRecord site = authority.siteReservations[index];
            if (site != null && string.Equals(site.siteId, siteId, StringComparison.Ordinal))
            {
                if (!site.routeIds.Contains(routeId)) site.routeIds.Add(routeId);
                if (site.entrances.Count > 0 && string.IsNullOrWhiteSpace(site.entrances[0].permittedRouteId)) site.entrances[0].permittedRouteId = routeId;
                return;
            }
        }
    }

    private static string ResolveRegion(GeneratedWorldPlanRecord plan, string seed, Vector2Int coordinate, float x, float z, string continentId)
    {
        GeneratedSpatialWorldPlanRecord spatial = YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan);
        float best = float.PositiveInfinity;
        string bestId = string.Empty;
        for (int index = 0; index < spatial.regions.Count; index++)
        {
            GeneratedSpatialRegionRecord region = spatial.regions[index];
            if (region == null) continue;
            float distance = Vector2.SqrMagnitude(new Vector2(x - region.centerX, z - region.centerZ));
            if (distance < best) { best = distance; bestId = region.regionId; }
        }
        if (best <= 320f * 320f && !string.IsNullOrWhiteSpace(bestId))
            return bestId;
        return "region:" + continentId + ":" + FloorDiv(coordinate.x, RegionSpanCells) + ":" + FloorDiv(coordinate.y, RegionSpanCells);
    }

    private static void ResolveContinent(string seed, float x, float z, out string id, out float mask)
    {
        mask = 0f;
        int bestIndex = -1;
        for (int index = 0; index < ContinentCount; index++)
        {
            float angle = Hash01(seed + "|continent|" + index + "|angle") * 360f;
            float radius = 900f + Hash01(seed + "|continent|" + index + "|radius") * 900f;
            Vector2 centre = Rotate(Vector2.up * (450f + index * 480f), angle);
            Vector2 local = Rotate(new Vector2(x, z) - centre, -angle);
            float nx = local.x / radius;
            float nz = local.y / (radius * Mathf.Lerp(0.62f, 0.9f, Hash01(seed + "|continent|" + index + "|shape")));
            float candidate = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + nz * nz));
            if (candidate > mask) { mask = candidate; bestIndex = index; }
        }
        id = bestIndex < 0 ? "continent:ocean" : "continent:" + bestIndex.ToString("D2", CultureInfo.InvariantCulture);
    }

    private static string BuildPaletteContext(string continent, string landform, float temperature, float moisture)
    {
        string climate = temperature < 0.3f ? "cold" : temperature > 0.7f ? "warm" : "temperate";
        string wetness = moisture > 0.68f ? "wet" : moisture < 0.32f ? "dry" : "balanced";
        return continent + "|" + landform + "|" + climate + "|" + wetness;
    }

    private static GeneratedSemanticBoundaryRecord DummyBoundary() => new GeneratedSemanticBoundaryRecord();

    private static bool SiteIntersectsCell(GeneratedSemanticSiteReservationRecord site, Vector2Int coordinate)
    {
        if (site == null) return false;
        float minX = WorldGridOrigin + coordinate.x * CellSizeMeters;
        float minZ = WorldGridOrigin + coordinate.y * CellSizeMeters;
        return site.worldX + site.footprintRadius >= minX && site.worldX - site.footprintRadius <= minX + CellSizeMeters &&
               site.worldZ + site.footprintRadius >= minZ && site.worldZ - site.footprintRadius <= minZ + CellSizeMeters;
    }

    private static bool PolylineIntersectsCell(List<GeneratedSemanticRoutePointRecord> points, Vector2Int coordinate, float padding)
    {
        if (points == null || points.Count == 0) return false;
        float minX = WorldGridOrigin + coordinate.x * CellSizeMeters - padding;
        float maxX = minX + CellSizeMeters + padding * 2f;
        float minZ = WorldGridOrigin + coordinate.y * CellSizeMeters - padding;
        float maxZ = minZ + CellSizeMeters + padding * 2f;
        for (int index = 0; index < points.Count; index++)
        {
            GeneratedSemanticRoutePointRecord point = points[index];
            if (point != null && point.worldX >= minX && point.worldX <= maxX && point.worldZ >= minZ && point.worldZ <= maxZ)
                return true;
            if (index > 0 && SegmentIntersects(points[index - 1], point, minX, maxX, minZ, maxZ)) return true;
        }
        return false;
    }

    private static bool SegmentIntersects(GeneratedSemanticRoutePointRecord a, GeneratedSemanticRoutePointRecord b, float minX, float maxX, float minZ, float maxZ)
    {
        if (a == null || b == null) return false;
        return Mathf.Max(a.worldX, b.worldX) >= minX && Mathf.Min(a.worldX, b.worldX) <= maxX &&
               Mathf.Max(a.worldZ, b.worldZ) >= minZ && Mathf.Min(a.worldZ, b.worldZ) <= maxZ;
    }

    private static List<string> CellIdsForBounds(float minX, float maxX, float minZ, float maxZ)
    {
        int minCellX = FloorDiv(Mathf.FloorToInt((minX - WorldGridOrigin) / CellSizeMeters), 1);
        int maxCellX = FloorDiv(Mathf.FloorToInt((maxX - WorldGridOrigin) / CellSizeMeters), 1);
        int minCellZ = FloorDiv(Mathf.FloorToInt((minZ - WorldGridOrigin) / CellSizeMeters), 1);
        int maxCellZ = FloorDiv(Mathf.FloorToInt((maxZ - WorldGridOrigin) / CellSizeMeters), 1);
        List<string> result = new List<string>();
        for (int z = minCellZ; z <= maxCellZ; z++)
        for (int x = minCellX; x <= maxCellX; x++)
            result.Add(BuildCellId(new Vector2Int(x, z)));
        return result;
    }

    private static void AddUniqueSite(List<GeneratedSemanticSiteReservationRecord> values, GeneratedSemanticSiteReservationRecord site)
    {
        for (int index = 0; index < values.Count; index++) if (values[index] != null && values[index].siteId == site.siteId) return;
        values.Add(site);
    }
    private static void AddUniqueWater(List<GeneratedSemanticWaterNetworkRecord> values, GeneratedSemanticWaterNetworkRecord water)
    {
        for (int index = 0; index < values.Count; index++) if (values[index] != null && values[index].waterId == water.waterId) return;
        values.Add(water);
    }
    private static void AddSortedId(List<string> values, string value) { if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value)) { values.Add(value); values.Sort(StringComparer.Ordinal); } }
    private static List<string> Union(List<string> a, List<string> b) { List<string> result = new List<string>(); if (a != null) for (int i = 0; i < a.Count; i++) AddSortedId(result, a[i]); if (b != null) for (int i = 0; i < b.Count; i++) AddSortedId(result, b[i]); return result; }
    private static void AppendSorted(StringBuilder builder, List<string> values) { if (values == null) return; List<string> sorted = new List<string>(values); sorted.Sort(StringComparer.Ordinal); for (int i = 0; i < sorted.Count; i++) builder.Append('|').Append(sorted[i] ?? string.Empty); }
    private static void Append(StringBuilder builder, params object[] values) { for (int i = 0; i < values.Length; i++) builder.Append('|').Append(values[i] is float f ? f.ToString("R", CultureInfo.InvariantCulture) : values[i] ?? string.Empty); }
    private static void AppendIds<T>(StringBuilder builder, List<T> values, Func<T, string> selector) { if (values == null) return; for (int i = 0; i < values.Count; i++) builder.Append('|').Append(selector(values[i]) ?? string.Empty); }
    private static string BuildCellId(Vector2Int coordinate) => "cell:" + coordinate.x + ":" + coordinate.y;
    private static int FloorDiv(int value, int divisor) { int quotient = value / divisor; int remainder = value % divisor; return remainder < 0 ? quotient - 1 : quotient; }
    private static float ValueNoise(float x, float z, string seed, string domain)
    {
        // note: Hash the exact legacy character stream directly so dense streamed ecology sampling avoids dozens of temporary strings per terrain pixel.
        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);
        float tx = x - x0;
        float tz = z - z0;
        tx = tx * tx * (3f - 2f * tx);
        tz = tz * tz * (3f - 2f * tz);
        float a = Hash01(seed, domain, x0, z0);
        float b = Hash01(seed, domain, x0 + 1, z0);
        float c = Hash01(seed, domain, x0, z0 + 1);
        float d = Hash01(seed, domain, x0 + 1, z0 + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }
    private static float Hash01(string seed, string domain, int x, int z)
    {
        uint hash = 2166136261u;
        hash = AppendHash(hash, seed ?? string.Empty);
        hash = AppendHash(hash, domain ?? string.Empty);
        hash = AppendHash(hash, '|');
        hash = AppendHash(hash, x);
        hash = AppendHash(hash, '|');
        hash = AppendHash(hash, z);
        return (hash & 0x00FFFFFFu) / 16777215f;
    }
    private static uint AppendHash(uint hash, string value)
    {
        for (int index = 0; index < value.Length; index++)
            hash = AppendHash(hash, value[index]);
        return hash;
    }
    private static uint AppendHash(uint hash, char value)
    {
        unchecked { return (hash ^ value) * 16777619u; }
    }
    private static uint AppendHash(uint hash, int value)
    {
        if (value < 0)
        {
            string negativeSign = CultureInfo.CurrentCulture.NumberFormat.NegativeSign;
            for (int index = 0; index < negativeSign.Length; index++)
                hash = AppendHash(hash, negativeSign[index]);
            return AppendHashUnsigned(hash, (uint)(-(long)value));
        }
        return AppendHashUnsigned(hash, (uint)value);
    }
    private static uint AppendHashUnsigned(uint hash, uint value)
    {
        uint divisor = 1u;
        while (value / divisor >= 10u)
            divisor *= 10u;
        do
        {
            uint digit = value / divisor;
            hash = AppendHash(hash, (char)('0' + digit));
            value %= divisor;
            if (divisor == 1u)
                break;
            divisor /= 10u;
        }
        while (true);
        return hash;
    }
    private static Vector2 Rotate(Vector2 value, float degrees) { float r = degrees * Mathf.Deg2Rad; float s = Mathf.Sin(r); float c = Mathf.Cos(r); return new Vector2(value.x * c - value.y * s, value.x * s + value.y * c); }
    private static float Hash01(string value) { uint hash = 2166136261u; string safe = value ?? string.Empty; for (int i = 0; i < safe.Length; i++) { hash ^= safe[i]; hash *= 16777619u; } return (hash & 0x00FFFFFFu) / 16777215f; }
    private static string StableHex(string value) { uint hash = 2166136261u; string safe = value ?? string.Empty; for (int i = 0; i < safe.Length; i++) { hash ^= safe[i]; hash *= 16777619u; } return hash.ToString("x8", CultureInfo.InvariantCulture); }
    private static string SafeSeed(GeneratedWorldPlanRecord plan) => plan != null && !string.IsNullOrWhiteSpace(plan.worldSeed) ? plan.worldSeed.Trim() : "yourquest_default_world";
}
