using System;
using System.Collections.Generic;

public static class YQWorldAssetCatalog
{
    public const string SlotTerrain = "terrain_material";
    public const string SlotFloor = "floor";
    public const string SlotWall = "wall";
    public const string SlotRoof = "roof";
    public const string SlotDoor = "door";
    public const string SlotPath = "path";
    public const string SlotSettlementBuilding = "settlement_building";
    public const string SlotLargeStructure = "large_structure";
    public const string SlotFloorDeco = "floor_deco";
    public const string SlotWallDeco = "wall_deco";
    public const string SlotVegetation = "vegetation";
    public const string SlotRock = "rock";
    public const string SlotLighting = "lighting";
    public const string SlotLootContainer = "loot_container";
    public const string SlotEnemySite = "enemy_site";
    public const string SlotInteriorDeco = "interior_deco";
    public const string SlotExteriorDeco = "exterior_deco";

    private static readonly HashSet<string> SupportedStyleKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "nordic_forest", "viking_rural", "ancient_desert_ruins", "western_desert_town",
            "asian_dynasty", "persepolis_empire", "victorian_mansion", "container_district",
            "bio_horror_scifi", "scifi_engineers_room", "hivemind_medieval_kingdom",
            "hivemind_military_camp", "hivemind_gothic_cathedral", "hivemind_cyberpunk_city",
            "hivemind_gladiator_arena", "hivemind_rural_town", "hivemind_modular_viking_village",
            "hivemind_town_smith", "hivemind_haunted_village", "hivemind_mystic_dungeon",
            "hivemind_mountain_temple", "hivemind_woodland_village", "hivemind_witch_house",
            "hivemind_cave_tomb", "hivemind_house_on_hill", "hivemind_villa_forge",
            "hivemind_horror_hospital", "hivemind_olympus_temple", "hivemind_pirate_island",
            "hivemind_hallowed_depths", "hivemind_sewers", "hivemind_mountain_messenger"
        };

    private const string Nordic = "Assets/BefourStudios/NordicVillage/Art/Prefabs/";
    private const string Viking = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/";
    private const string DesertRuins = "Assets/BefourStudios/AncientDesertRuins/Art/Prefabs/";
    private const string Western = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/";
    private const string Asian = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/";
    private const string Persepolis = "Assets/BefourStudios/PersepolisEmpireEnvironment/Art/Prefabs/";
    private const string Victorian = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/";
    private const string Container = "Assets/BefourStudios/ContainerDistrict/Art/Prefabs/";
    private const string BioHorror = "Assets/BefourStudios/BioHorrorSciFiEnvironment/Art/Prefabs/";
    private const string SciFiEngineers = "Assets/BefourStudios/SciFiEngineersRoom/Art/Prefabs/";
    private const string Pirate = "Assets/HIVEMIND/PirateIsland/HDRP(Default)/Art/Prefabs/";
    private const string HivemindVikingComplete = "Assets/HIVEMIND/ModularVikingVillage/HDRP/Art/Prefabs/";
    private const string HivemindTownSmithComplete = "Assets/HIVEMIND/TownSmith/HDRP(Default)/Art/Prefabs/Drag&Drops/";
    private const string HivemindCyberpunkComplete = "Assets/HIVEMIND/CyberpunkCity/HDRP(Default)/Art/Prefabs/";
    private const string TomTrees = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/";
    private const string Bushes = "Assets/YughuesFreeBushes2018/Prefabs/";
    private const string Ground = "Assets/ADG_Textures/ground_vol1/";
    private const string Chests = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/";

    public static void EnsureAssetPalettes(GeneratedWorldPlanRecord plan)
    {
        if (plan == null)
            return;

        plan.EnsureCollections();
        RemoveNullPalettes(plan.assetPalettes);
        for (int i = 0; i < plan.regions.Count; i++)
        {
            GeneratedRegionRecord region = plan.regions[i];
            if (region == null)
                continue;

            GeneratedRegionAssetPaletteRecord previousPalette =
                FindPaletteForRegion(
                    plan.assetPalettes,
                    region.regionId);

            if (previousPalette != null &&
                !IsCoherentStyleTransition(
                    previousPalette.styleKey,
                    region.assetStyleKey,
                    region.assetStyleRationale))
            {
                // note: Repair already-persisted prototype deltas that turned towns into interior/genre kits in response to ordinary walking.
                UnityEngine.Debug.LogWarning(
                    "[YQWorldAssetCatalog] Restored coherent palette '" +
                    previousPalette.styleKey + "' for region '" + region.regionId +
                    "' after rejecting persisted transition to '" + region.assetStyleKey + "'.");
                region.assetStyleKey = previousPalette.styleKey;
                region.assetStyleRationale = "Restored the last accepted curated palette after an incoherent runtime transition.";
            }

            GeneratedRegionAssetPaletteRecord palette = BuildPaletteForRegion(region, plan.worldSeed);
            UpsertPalette(plan.assetPalettes, palette);
            region.assetPaletteId = palette.paletteId;
            region.assetStyleKey = palette.styleKey;
            region.assetStyleRationale = palette.rationale;
        }
    }

    public static GeneratedAssetReferenceRecord PickAssetForSlot(GeneratedRegionAssetPaletteRecord palette, string slotTag, string seed)
    {
        List<GeneratedAssetReferenceRecord> list = GetSlotList(palette, slotTag);
        if (list == null || list.Count == 0)
            return null;

        int totalWeight = 0;
        for (int i = 0; i < list.Count; i++)
        {
            GeneratedAssetReferenceRecord record = list[i];
            if (record != null &&
                IsAllowedWorldReferenceForSlot(
                    record,
                    slotTag))
            {
                totalWeight += Math.Max(1, record.weight);
            }
        }

        if (totalWeight <= 0)
            return null;

        int roll = PositiveHash((seed ?? string.Empty) + ":" + (slotTag ?? string.Empty)) % totalWeight;
        for (int i = 0; i < list.Count; i++)
        {
            GeneratedAssetReferenceRecord record = list[i];
            if (record == null ||
                !IsAllowedWorldReferenceForSlot(
                    record,
                    slotTag))
            {
                continue;
            }

            roll -= Math.Max(1, record.weight);
            if (roll < 0)
                return record;
        }

        for (int i = 0; i < list.Count; i++)
        {
            GeneratedAssetReferenceRecord record =
                list[i];

            if (record != null &&
                IsAllowedWorldReferenceForSlot(
                    record,
                    slotTag))
            {
                return record;
            }
        }

        return null;
    }

    public static List<GeneratedAssetReferenceRecord> GetSlotList(GeneratedRegionAssetPaletteRecord palette, string slotTag)
    {
        if (palette == null || string.IsNullOrWhiteSpace(slotTag))
            return null;

        switch (NormalizeKey(slotTag))
        {
            case SlotTerrain: return palette.terrainMaterials;
            case SlotFloor: return palette.floor;
            case SlotWall: return palette.wall;
            case SlotRoof: return palette.roof;
            case SlotDoor: return palette.door;
            case SlotPath: return palette.path;
            case SlotSettlementBuilding: return palette.settlementBuilding;
            case SlotLargeStructure: return palette.largeStructure;
            case SlotFloorDeco: return palette.floorDeco;
            case SlotWallDeco: return palette.wallDeco;
            case SlotVegetation: return palette.vegetation;
            case SlotRock: return palette.rock;
            case SlotLighting: return palette.lighting;
            case SlotLootContainer: return palette.lootContainer;
            case SlotEnemySite: return palette.enemySite;
            case SlotInteriorDeco: return palette.interiorDeco;
            case SlotExteriorDeco: return palette.exteriorDeco;
            default: return null;
        }
    }

    public static bool IsSupportedStyleKey(
        string styleKey)
    {
        return
            SupportedStyleKeys.Contains(
                NormalizeKey(
                    styleKey));
    }

    public static bool IsCoherentStyleTransition(string currentStyle, string nextStyle, string reason)
    {
        string current = NormalizeKey(currentStyle);
        string next = NormalizeKey(nextStyle);
        bool disruptive =
            ResolveStyleDomain(current) != ResolveStyleDomain(next) ||
            IsInteriorStyle(current) != IsInteriorStyle(next);

        if (!disruptive)
            return true;

        string evidence = (reason ?? string.Empty).Trim().ToLowerInvariant();
        return ContainsAny(evidence,
            "portal", "breach", "rupture", "convergence", "dimensional", "reality shift",
            "invasion", "occupation", "catastrophe", "disaster", "collapsed", "destroyed",
            "corruption", "curse", "ritual", "summoned", "transformed", "rebuilt",
            "construction", "flood", "terraformed", "time shift", "world event");
    }

    private static string ResolveStyleDomain(string style)
    {
        if (ContainsAny(style, "scifi", "cyberpunk", "bio_horror"))
            return "technology";
        if (ContainsAny(style, "container", "hospital", "military", "sewer"))
            return "industrial";
        return "fantasy";
    }

    private static bool IsInteriorStyle(string style)
    {
        return ContainsAny(style,
            "mansion", "room", "dungeon", "hospital", "sewer", "cave", "tomb",
            "depths", "witch_house", "house_on_hill");
    }

    private static GeneratedRegionAssetPaletteRecord FindPaletteForRegion(
        List<GeneratedRegionAssetPaletteRecord> palettes,
        string regionId)
    {
        if (palettes == null || string.IsNullOrWhiteSpace(regionId))
            return null;

        for (int i = 0; i < palettes.Count; i++)
        {
            GeneratedRegionAssetPaletteRecord palette = palettes[i];
            if (palette != null && string.Equals(palette.regionId, regionId, StringComparison.OrdinalIgnoreCase))
                return palette;
        }

        return null;
    }

    private static GeneratedRegionAssetPaletteRecord BuildPaletteForRegion(GeneratedRegionRecord region, string worldSeed)
    {
        string style = ResolveStyleKey(region);
        GeneratedRegionAssetPaletteRecord palette = NewPalette(region, style, worldSeed);

        switch (style)
        {
            case "ancient_desert_ruins":
                FillAncientDesertRuins(palette);
                break;
            case "western_desert_town":
                FillWesternDesertTown(palette);
                break;
            case "asian_dynasty":
                FillAsianDynasty(palette);
                break;
            case "persepolis_empire":
                FillPersepolis(palette);
                break;
            case "victorian_mansion":
                FillVictorianMansion(palette);
                break;
            case "container_district":
                FillContainerDistrict(palette);
                break;
            case "bio_horror_scifi":
                FillBioHorror(palette);
                break;
            case "scifi_engineers_room":
                FillSciFiEngineersRoom(palette);
                break;
            case "hivemind_medieval_kingdom":
                // note: Discovered Medieval Kingdom modules enrich this Viking fallback skeleton after registry rebuild.
                FillVikingRural(palette);
                break;
            case "hivemind_military_camp":
                // note: Military camp modules use industrial-style lanes until their imported props populate the palette.
                FillContainerDistrict(palette);
                break;
            case "hivemind_gothic_cathedral":
                // note: Cathedral regions start from mansion interiors so doors/rooms remain stable before discovered merges.
                FillVictorianMansion(palette);
                break;
            case "hivemind_cyberpunk_city":
                // note: Cyberpunk city regions fall back to container/industrial geometry with neon props layered by discovery.
                FillContainerDistrict(palette);
                break;
            case "hivemind_gladiator_arena":
                // note: Arena content uses imperial stone rules as a safe footprint baseline.
                FillPersepolis(palette);
                break;
            case "hivemind_rural_town":
            case "hivemind_modular_viking_village":
            case "hivemind_town_smith":
                // note: Rural, Viking, and smith packs extend grounded settlement palettes.
                FillVikingRural(palette);
                break;
            case "hivemind_haunted_village":
            case "hivemind_witch_house":
            case "hivemind_house_on_hill":
                // note: Haunted/cottage/manor styles prefer readable room props and moody exterior dressing.
                FillVictorianMansion(palette);
                break;
            case "hivemind_mystic_dungeon":
            case "hivemind_mountain_temple":
            case "hivemind_cave_tomb":
            case "hivemind_olympus_temple":
                // note: Dungeon, temple, cave, and Olympus kits start from modular stone ruin rules.
                FillAncientDesertRuins(palette);
                break;
            case "hivemind_woodland_village":
                // note: Woodland village assets share the organic outdoor layout baseline.
                FillNordicForest(palette);
                break;
            case "hivemind_horror_hospital":
                // note: Horror hospital rooms are modern interiors; industrial collision rules are safest.
                FillContainerDistrict(palette);
                break;
            case "hivemind_pirate_island":
                // note: Pirate Island owns a dimension-matched six-metre shack cell; Viking pieces remain fallback dressing only.
                FillVikingRural(palette);
                Add(palette.floor, Pirate + "SM_FloorWood6x6m_01.prefab", SlotFloor, "pirate", "shack6", "structural_floor");
                Add(palette.wall, Pirate + "SM_ShackSide6m_01.prefab", SlotWall, "pirate", "shack6", "side_wall");
                Add(palette.wall, Pirate + "SM_ShackFront6m_01.prefab", SlotWall, "pirate", "shack6", "front_wall");
                Add(palette.roof, Pirate + "SM_Roof6x6m_01.prefab", SlotRoof, "pirate", "shack6", "structural_roof");
                Add(palette.door, Pirate + "SM_DoorShack_01.prefab", SlotDoor, "pirate", "shack6", "door_leaf");
                Add(palette.exteriorDeco, Pirate + "SM_Awning_01.prefab", SlotExteriorDeco, "pirate", "market", "awning");
                Add(palette.floorDeco, Pirate + "SM_WoodCrate_01a.prefab", SlotFloorDeco, "pirate", "market", "crate");
                Add(palette.floorDeco, Pirate + "SM_WoodBarrel_01a.prefab", SlotFloorDeco, "pirate", "market", "barrel");
                break;
            case "hivemind_hallowed_depths":
                // note: Hallowed Depths is dungeon-first, so ruin cells are a closer fallback than village paths.
                FillAncientDesertRuins(palette);
                break;
            case "hivemind_sewers":
                // note: Sewer modules align best with industrial lane rules until discovered prefabs fill the palette.
                FillContainerDistrict(palette);
                break;
            case "hivemind_mountain_messenger":
                // note: Messenger mountain assets fall back to stone/ruin traversal pieces.
                FillAncientDesertRuins(palette);
                break;
            case "viking_rural":
                FillVikingRural(palette);
                break;
            default:
                FillNordicForest(palette);
                break;
        }

        AddCompatibleCompleteSettlementBuildings(
            palette,
            style);

        AddSharedUtilityAssets(palette);
        // note: Imported discovery may contribute small, verified dressing, but never redefine a settlement's structural kit.
        AddDiscoveredAssets(palette);
        AddPaletteRules(palette);
        palette.verboseInternals.Add("palette_seed=" + StableHex((worldSeed ?? string.Empty) + ":" + (region != null ? region.regionId : "region") + ":" + style));
        palette.verboseInternals.Add("slot_contract=floor/wall/path/large_structure build layout skeleton; floor_deco/wall_deco/exterior_deco add dressing only after collision-safe placement.");
        return palette;
    }

    private static void AddCompatibleCompleteSettlementBuildings(
        GeneratedRegionAssetPaletteRecord palette,
        string style)
    {
        if (palette == null)
            return;

        if (string.Equals(style, "nordic_forest", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(style, "viking_rural", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(style, "hivemind_modular_viking_village", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(style, "hivemind_woodland_village", StringComparison.OrdinalIgnoreCase))
        {
            // note: These five authored timber houses are the compatible complete-building layer for timber construction-kit palettes; the owning palette still supplies roads and dressing.
            Add(palette.settlementBuilding, HivemindVikingComplete + "SM_HouseBuilding_001_a.prefab", SlotSettlementBuilding, style, "timber", "complete_building", "residence");
            Add(palette.settlementBuilding, HivemindVikingComplete + "SM_HouseBuilding_001_b.prefab", SlotSettlementBuilding, style, "timber", "complete_building", "residence");
            Add(palette.settlementBuilding, HivemindVikingComplete + "SM_HouseBuilding_002_a.prefab", SlotSettlementBuilding, style, "timber", "complete_building", "service_house");
            Add(palette.settlementBuilding, HivemindVikingComplete + "SM_HouseBuilding_003_a.prefab", SlotSettlementBuilding, style, "timber", "complete_building", "civic_house");
            Add(palette.settlementBuilding, HivemindVikingComplete + "SM_HouseBuilding_003_b.prefab", SlotSettlementBuilding, style, "timber", "complete_building", "market_house");
        }

        if (string.Equals(style, "hivemind_town_smith", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(style, "hivemind_rural_town", StringComparison.OrdinalIgnoreCase))
        {
            // note: TownSmith drag-and-drop houses are complete curated cells shared only with its compatible rural-town family.
            Add(palette.settlementBuilding, HivemindTownSmithComplete + "PF_House01.prefab", SlotSettlementBuilding, style, "complete_building", "residence");
            Add(palette.settlementBuilding, HivemindTownSmithComplete + "PF_House02.prefab", SlotSettlementBuilding, style, "complete_building", "service_house");
            Add(palette.settlementBuilding, HivemindTownSmithComplete + "PF_House03.prefab", SlotSettlementBuilding, style, "complete_building", "market_house");
        }

        if (string.Equals(style, "hivemind_cyberpunk_city", StringComparison.OrdinalIgnoreCase))
        {
            // note: Cyberpunk settlements select complete merged blocks; individual cables, stairs, and façades remain decoration and never become lots.
            Add(palette.settlementBuilding, HivemindCyberpunkComplete + "SM_MERGED_BP_House_Shop_E3.prefab", SlotSettlementBuilding, style, "complete_building", "shop");
            Add(palette.settlementBuilding, HivemindCyberpunkComplete + "SM_MERGED_BP_House_Shop_E10.prefab", SlotSettlementBuilding, style, "complete_building", "shop");
            Add(palette.settlementBuilding, HivemindCyberpunkComplete + "SM_MERGED_BP_House_Shop_E11.prefab", SlotSettlementBuilding, style, "complete_building", "shop");
            Add(palette.settlementBuilding, HivemindCyberpunkComplete + "SM_MERGED_BP_House_Shop_E12.prefab", SlotSettlementBuilding, style, "complete_building", "shop");
            Add(palette.settlementBuilding, HivemindCyberpunkComplete + "SM_MERGED_BP_House_Shop_E14.prefab", SlotSettlementBuilding, style, "complete_building", "residence");
            Add(palette.settlementBuilding, HivemindCyberpunkComplete + "SM_MERGED_BP_House_Shop_E15.prefab", SlotSettlementBuilding, style, "complete_building", "residence");
            Add(palette.settlementBuilding, HivemindCyberpunkComplete + "SM_MERGED_BP_Building9.prefab", SlotSettlementBuilding, style, "complete_building", "civic_house");
            Add(palette.settlementBuilding, HivemindCyberpunkComplete + "SM_MERGED_BP_Building10_2.prefab", SlotSettlementBuilding, style, "complete_building", "service_house");
        }
    }

    private static GeneratedRegionAssetPaletteRecord NewPalette(GeneratedRegionRecord region, string style, string worldSeed)
    {
        string regionId = region != null && !string.IsNullOrWhiteSpace(region.regionId) ? region.regionId.Trim() : "region_unknown";
        GeneratedRegionAssetPaletteRecord palette = new GeneratedRegionAssetPaletteRecord
        {
            paletteId = "palette_" + regionId + "_" + StableHex((worldSeed ?? string.Empty) + ":" + regionId + ":" + style).Substring(0, 8),
            regionId = regionId,
            styleKey = style,
            architecturePack = ResolvePackName(style),
            terrainPack = "ADG_Textures ground_vol1",
            naturePack = ResolveNaturePack(style),
            settlementPack = ResolvePackName(style),
            encampmentPack = ResolveEncampmentPack(style),
            layoutRuleProfile = ResolveLayoutRuleProfile(style),
            mood = ResolveMood(style),
            rationale = BuildRationale(region, style)
        };
        palette.EnsureCollections();
        AddUnique(palette.styleTags, style);

        if (region != null)
        {
            AddUnique(palette.styleTags, NormalizeKey(region.terrainProfile));
            AddUnique(palette.styleTags, NormalizeKey(region.climateProfile));
            if (region.biomeTags != null)
            {
                for (int i = 0; i < region.biomeTags.Count; i++)
                    AddUnique(palette.styleTags, NormalizeKey(region.biomeTags[i]));
            }
        }

        return palette;
    }

    private static void FillNordicForest(GeneratedRegionAssetPaletteRecord p)
    {
        AddTerrain(p, GroundMat("ground5"), "moss", "forest", "soft_ground");
        AddTerrain(p, GroundMat("ground6"), "dirt", "forest", "trail");
        AddTerrain(p, GroundMat("ground7"), "stone", "forest", "rocky");
        Add(p.floor, P(Nordic, "SM_GroundMesh"), SlotFloor, "nordic", "forest", "ground");
        Add(p.floor, P(Nordic, "SM_MudMesh"), SlotFloor, "nordic", "mud", "path");
        Add(p.floor, P(Nordic, "SM_SingleTile"), SlotFloor, "nordic", "wood_tile", "interior");
        Add(p.wall, P(Nordic, "SM_LogWall01"), SlotWall, "nordic", "log", "exterior");
        Add(p.wall, P(Nordic, "SM_LogWallWindow"), SlotWall, "nordic", "log", "window");
        Add(p.wall, P(Nordic, "SM_WallTall01"), SlotWall, "nordic", "plaster", "tall");
        Add(p.wall, P(Nordic, "SM_Wall01"), SlotWall, "nordic", "plaster", "house_cell");
        Add(p.wall, P(Nordic, "SM_LogWallDoor"), SlotWall, "nordic", "log", "door_wall");
        Add(p.wall, P(Nordic, "SM_WallDoor"), SlotWall, "nordic", "plaster", "door_wall");
        Add(p.wall, P(Nordic, "SM_WallTallDoor"), SlotWall, "nordic", "plaster", "tall_door_wall");
        Add(p.roof, P(Nordic, "SM_ThatchRoof01"), SlotRoof, "nordic", "thatch");
        Add(p.roof, P(Nordic, "SM_LogRoofGable01"), SlotRoof, "nordic", "gable");
        Add(p.roof, P(Nordic, "SM_RoofGableTall01"), SlotRoof, "nordic", "tall_gable");
        Add(p.door, P(Nordic, "SM_LogWallDoor"), SlotDoor, "nordic", "log", "complete_front_wall");
        Add(p.door, P(Nordic, "SM_WallDoor"), SlotDoor, "nordic", "plaster", "complete_front_wall");
        Add(p.door, P(Nordic, "SM_WallTallDoor"), SlotDoor, "nordic", "plaster", "complete_front_wall");
        Add(p.path, P(Nordic, "SM_MudMesh"), SlotPath, "nordic", "dirt_path");
        Add(p.path, P(Viking, "SM_GroundPatch_2"), SlotPath, "viking", "ground_patch");

        // note: NordicVillage is a construction kit; leaving this list empty selects the modular house layout instead of treating a roof fragment as a house.

        Add(p.largeStructure, P(Nordic, "SM_BackGate"), SlotLargeStructure, "nordic", "gate");
        // note: Hostile sites use small modular anchors so camps do not look like copied settlement buildings.
        Add(p.enemySite, P(Nordic, "SM_BackGate"), SlotEnemySite, "nordic", "gate", "camp_anchor");
        Add(p.enemySite, P(Nordic, "SM_DefensiveWallSingle"), SlotEnemySite, "nordic", "palisade", "camp_perimeter");
        Add(p.enemySite, P(Nordic, "SM_Cart"), SlotEnemySite, "nordic", "cart", "camp_supplies");
        Add(p.enemySite, P(Nordic, "SM_FirePit"), SlotEnemySite, "nordic", "firepit", "camp_center");
        Add(p.enemySite, P(Nordic, "SM_WoodenCrate"), SlotEnemySite, "nordic", "crate", "camp_supplies");
        Add(p.floorDeco, P(Nordic, "SM_Barrel"), SlotFloorDeco, "nordic", "storage");
        Add(p.floorDeco, P(Nordic, "SM_WoodenCrate"), SlotFloorDeco, "nordic", "storage");
        Add(p.floorDeco, P(Nordic, "SM_Log"), SlotFloorDeco, "nordic", "woodcutting");
        Add(p.wallDeco, P(Nordic, "SM_WallTorch"), SlotWallDeco, "nordic", "torch");
        Add(p.wallDeco, P(Nordic, "SM_Shield"), SlotWallDeco, "nordic", "warrior");
        Add(p.vegetation, P(Nordic, "SM_Tree"), SlotVegetation, "nordic", "tree", "forest");
        Add(p.vegetation, P(Nordic, "SM_TreeNeedles01"), SlotVegetation, "nordic", "conifer");
        Add(p.vegetation, P(Nordic, "SM_Fern"), SlotVegetation, "nordic", "fern");
        Add(p.vegetation, Bushes + "P_Bush01.prefab", SlotVegetation, "bush", "forest");
        Add(p.rock, P(Nordic, "SM_Pebble01"), SlotRock, "nordic", "small_rock");
        Add(p.vegetation, TomTrees + "Alder.prefab", SlotVegetation, "tree", "temperate");
        Add(p.lighting, P(Nordic, "SM_FirePit"), SlotLighting, "nordic", "fire");
        Add(p.interiorDeco, P(Nordic, "SM_Shelf"), SlotInteriorDeco, "nordic", "storage");
        Add(p.exteriorDeco, P(Nordic, "SM_DefensiveWallSingle"), SlotExteriorDeco, "nordic", "palisade");
    }

    private static void FillVikingRural(GeneratedRegionAssetPaletteRecord p)
    {
        AddTerrain(p, GroundMat("ground6"), "dirt", "rural", "trail");
        AddTerrain(p, GroundMat("ground8"), "rock", "highland");
        Add(p.floor, P(Viking, "SM_House1_Floor"), SlotFloor, "viking", "house");
        Add(p.floor, P(Viking, "SM_StableWooden_Floor"), SlotFloor, "viking", "stable");
        Add(p.wall, P(Viking, "SM_House2_BackWall"), SlotWall, "viking", "wood");
        Add(p.wall, P(Viking, "SM_House2_SideWall"), SlotWall, "viking", "wood", "house2_side");
        Add(p.wall, P(Viking, "SM_House2_FrontWall"), SlotWall, "viking", "wood", "house2_front");
        Add(p.wall, P(Viking, "SM_StableWooden_SideWall"), SlotWall, "viking", "stable");
        Add(p.wall, P(Viking, "SM_StoneWall_PS3"), SlotWall, "viking", "stone");
        Add(p.roof, P(Viking, "SM_House2_Roof"), SlotRoof, "viking", "wood_roof");
        Add(p.roof, P(Viking, "SM_RoofCot"), SlotRoof, "viking", "shack");
        Add(p.door, P(Viking, "SM_House2_Door"), SlotDoor, "viking", "wood");
        Add(p.path, P(Viking, "SM_WoodenUpPathway_PathwaySection"), SlotPath, "viking", "wood_path");
        Add(p.path, P(Viking, "SM_MiniBridge_Body"), SlotPath, "viking", "bridge");

        // note: Viking lots use the floor, wall, roof, and door kit through the modular settlement builder when no complete prefab is registered.

        Add(p.largeStructure, P(Viking, "SM_WoodenMiniWatchtower_Body"), SlotLargeStructure, "viking", "watchtower");
        // note: Viking hostile sites combine a threat landmark with perimeter and supply modules.
        Add(p.enemySite, P(Viking, "SM_WoodenUGEntrance"), SlotEnemySite, "viking", "underground_entrance");
        Add(p.enemySite, P(Viking, "SM_WoodenMiniWatchtower_Body"), SlotEnemySite, "viking", "watchtower", "camp_anchor");
        Add(p.enemySite, P(Viking, "SM_WoodenFence_3section"), SlotEnemySite, "viking", "fence", "camp_perimeter");
        Add(p.enemySite, P(Viking, "SM_LogStackSet"), SlotEnemySite, "viking", "logs", "camp_supplies");
        Add(p.enemySite, P(Viking, "SM_WoodenBox"), SlotEnemySite, "viking", "box", "camp_supplies");
        Add(p.floorDeco, P(Viking, "SM_LogStackSet"), SlotFloorDeco, "viking", "logs");
        Add(p.floorDeco, P(Viking, "SM_WoodenBox"), SlotFloorDeco, "viking", "box");
        Add(p.floorDeco, P(Viking, "SM_Hay_Bale"), SlotFloorDeco, "viking", "farm");
        Add(p.wallDeco, P(Viking, "SM_TorchWall"), SlotWallDeco, "viking", "torch");
        Add(p.vegetation, P(Viking, "SM_Tree_02_Foliage"), SlotVegetation, "viking", "tree");
        Add(p.vegetation, P(Viking, "SM_Weed_1"), SlotVegetation, "viking", "weed");
        Add(p.rock, P(Viking, "SM_GroundRock"), SlotRock, "viking", "rock");
        // note: SM_TorchLong contains a broken script reference; the script-free authored fire structure supplies reliable Viking lighting instead.
        Add(p.lighting, P(Viking, "SM_CampWoodFireStructure"), SlotLighting, "viking", "fire", "campfire");
        Add(p.interiorDeco, P(Viking, "SM_VikingVase_1"), SlotInteriorDeco, "viking", "vase");
        Add(p.exteriorDeco, P(Viking, "SM_WoodenFence_3section"), SlotExteriorDeco, "viking", "fence");
    }

    private static void FillAncientDesertRuins(GeneratedRegionAssetPaletteRecord p)
    {
        AddTerrain(p, GroundMat("ground1"), "sand", "desert");
        AddTerrain(p, GroundMat("ground2"), "rocky_sand", "ruins");
        AddTerrain(p, GroundMat("ground3"), "dry_ground", "badland");
        Add(p.floor, P(DesertRuins, "SM_Floor1"), SlotFloor, "desert_ruin", "stone");
        Add(p.floor, P(DesertRuins, "SM_Sand"), SlotFloor, "desert_ruin", "sand");
        Add(p.wall, P(DesertRuins, "SM_Wall1"), SlotWall, "desert_ruin", "stone");
        Add(p.wall, P(DesertRuins, "SM_WallUpper1"), SlotWall, "desert_ruin", "upper_wall");
        Add(p.wall, P(DesertRuins, "SM_Corner1"), SlotWall, "desert_ruin", "corner");
        Add(p.roof, P(DesertRuins, "SM_Ruine1"), SlotRoof, "desert_ruin", "collapsed");
        Add(p.door, P(DesertRuins, "SM_DoorFrame"), SlotDoor, "desert_ruin", "frame");
        Add(p.path, P(DesertRuins, "SM_RockySand"), SlotPath, "desert_ruin", "sand_path");

        Add(
            p.settlementBuilding,
            P(DesertRuins, "SM_Building1"),
            SlotSettlementBuilding,
            "desert_ruin",
            "building");

        Add(
            p.settlementBuilding,
            P(DesertRuins, "SM_Building2"),
            SlotSettlementBuilding,
            "desert_ruin",
            "building");

        Add(
            p.settlementBuilding,
            P(DesertRuins, "SM_Building3"),
            SlotSettlementBuilding,
            "desert_ruin",
            "building");

        Add(
            p.settlementBuilding,
            P(DesertRuins, "SM_Building4"),
            SlotSettlementBuilding,
            "desert_ruin",
            "building");

        Add(p.largeStructure, P(DesertRuins, "SM_Building1"), SlotLargeStructure, "desert_ruin", "building");
        // note: Ruin encampments get broken fragments and worksite pieces instead of one repeated ruin prefab.
        Add(p.enemySite, P(DesertRuins, "SM_Ruine1"), SlotEnemySite, "desert_ruin", "broken_roof", "ruin_anchor");
        Add(p.enemySite, P(DesertRuins, "SM_Ruine4"), SlotEnemySite, "desert_ruin", "ruin_site");
        Add(p.enemySite, P(DesertRuins, "SM_Debris1"), SlotEnemySite, "desert_ruin", "debris", "cover");
        Add(p.enemySite, P(DesertRuins, "SM_PalletBricks"), SlotEnemySite, "desert_ruin", "bricks", "worksite");
        Add(p.enemySite, P(DesertRuins, "SM_Barb"), SlotEnemySite, "desert_ruin", "barb", "threat_accent");
        Add(p.floorDeco, P(DesertRuins, "SM_BricksSet1"), SlotFloorDeco, "desert_ruin", "rubble");
        Add(p.floorDeco, P(DesertRuins, "SM_BrokenVase1"), SlotFloorDeco, "desert_ruin", "vase");
        Add(p.wallDeco, P(DesertRuins, "SM_Barb"), SlotWallDeco, "desert_ruin", "barb");
        Add(p.vegetation, P(DesertRuins, "SM_Cactus01"), SlotVegetation, "desert", "cactus");
        Add(p.vegetation, P(DesertRuins, "SM_Grass1"), SlotVegetation, "desert", "dry_grass");
        Add(p.rock, P(DesertRuins, "SM_Mountain1"), SlotRock, "desert_ruin", "mountain");
        Add(p.rock, P(DesertRuins, "SM_Debris1"), SlotRock, "desert_ruin", "debris");
        Add(p.lighting, P(DesertRuins, "SM_WaterTank"), SlotLighting, "desert_ruin", "landmark");
        Add(p.interiorDeco, P(DesertRuins, "SM_Shelf1"), SlotInteriorDeco, "desert_ruin", "shelf");
        Add(p.exteriorDeco, P(DesertRuins, "SM_PalletBricks"), SlotExteriorDeco, "desert_ruin", "worksite");
    }

    private static void FillWesternDesertTown(GeneratedRegionAssetPaletteRecord p)
    {
        AddTerrain(p, GroundMat("ground1"), "sand", "western");
        AddTerrain(p, GroundMat("ground4"), "dust", "road");
        Add(p.floor, P(Western, "SM_Floor01"), SlotFloor, "western", "wood");
        Add(p.floor, P(Western, "SM_SoilMesh"), SlotFloor, "western", "soil");
        Add(p.wall, P(Western, "SM_CrossWalls01"), SlotWall, "western", "wood");
        Add(p.wall, P(Western, "SM_End01Walls01"), SlotWall, "western", "mine");
        Add(p.roof, P(Western, "SM_Roof01"), SlotRoof, "western", "roof");
        Add(p.door, P(Western, "SM_Door01"), SlotDoor, "western", "wood");
        Add(p.path, P(Western, "SM_RoadMesh01"), SlotPath, "western", "road");
        Add(p.path, P(Western, "SM_RoadTurnMesh"), SlotPath, "western", "road_turn");

        Add(
            p.settlementBuilding,
            P(Western, "SM_Church"),
            SlotSettlementBuilding,
            "western",
            "church",
            "building");

        Add(
            p.settlementBuilding,
            P(Western, "SM_Church_1"),
            SlotSettlementBuilding,
            "western",
            "church",
            "building");

        Add(p.largeStructure, P(Western, "SM_Church"), SlotLargeStructure, "western", "building");
        // note: Western hostile sites lean on cave, fence, mine wall, and supply modules for readable variety.
        Add(p.enemySite, P(Western, "SM_CaveStraight"), SlotEnemySite, "western", "cave");
        Add(p.enemySite, P(Western, "SM_End01Walls01"), SlotEnemySite, "western", "mine_wall", "camp_anchor");
        Add(p.enemySite, P(Western, "SM_CrossWalls01"), SlotEnemySite, "western", "cross_wall", "cover");
        Add(p.enemySite, P(Western, "SM_Fence01"), SlotEnemySite, "western", "fence", "camp_perimeter");
        Add(p.enemySite, P(Western, "SM_SackStack01"), SlotEnemySite, "western", "supplies");
        Add(p.enemySite, P(Western, "SM_Barrel1"), SlotEnemySite, "western", "barrel", "supplies");
        Add(p.floorDeco, P(Western, "SM_Barrel1"), SlotFloorDeco, "western", "barrel");
        Add(p.floorDeco, P(Western, "SM_SackStack01"), SlotFloorDeco, "western", "supplies");
        Add(p.wallDeco, P(Western, "SM_LanternHook"), SlotWallDeco, "western", "lantern");
        Add(p.vegetation, P(Western, "SM_Cactus01"), SlotVegetation, "western", "cactus");
        Add(p.vegetation, P(Western, "SM_Grass01"), SlotVegetation, "western", "dry_grass");
        Add(p.rock, P(Western, "SM_Rock01"), SlotRock, "western", "rock");
        Add(p.rock, P(Western, "SM_Mountain01"), SlotRock, "western", "mountain");
        Add(p.lighting, P(Western, "SM_LanternPost"), SlotLighting, "western", "lantern");
        Add(p.interiorDeco, P(Western, "SM_Chair01"), SlotInteriorDeco, "western", "chair");
        Add(p.exteriorDeco, P(Western, "SM_Fence01"), SlotExteriorDeco, "western", "fence");
    }

    private static void FillAsianDynasty(GeneratedRegionAssetPaletteRecord p)
    {
        AddTerrain(p, GroundMat("ground6"), "packed_earth", "asian_dynasty");
        AddTerrain(p, GroundMat("ground9"), "garden_ground", "asian_dynasty");
        Add(p.floor, P(Asian, "SM_FloorTile_1"), SlotFloor, "asian_dynasty", "tile");
        Add(p.floor, P(Asian, "SM_FloorSet_Pieces1"), SlotFloor, "asian_dynasty", "floor_piece");
        Add(p.wall, P(Asian, "SM_Wallset_1"), SlotWall, "asian_dynasty", "wall");
        Add(p.wall, P(Asian, "SM_Wallset_2"), SlotWall, "asian_dynasty", "wall");
        Add(p.roof, P(Asian, "SM_MiniPavilionRoof_1"), SlotRoof, "asian_dynasty", "pavilion");
        Add(p.door, P(Asian, "SM_Building01"), SlotDoor, "asian_dynasty", "building_entry");
        Add(p.path, P(Asian, "SM_StairSet_1"), SlotPath, "asian_dynasty", "stairs");

        Add(
            p.settlementBuilding,
            P(Asian, "SM_Building01"),
            SlotSettlementBuilding,
            "asian_dynasty",
            "building");

        Add(
            p.settlementBuilding,
            P(Asian, "SM_Building03"),
            SlotSettlementBuilding,
            "asian_dynasty",
            "building");

        Add(
            p.settlementBuilding,
            P(Asian, "SM_Building04"),
            SlotSettlementBuilding,
            "asian_dynasty",
            "building");

        Add(
            p.settlementBuilding,
            P(Asian, "SM_Building05"),
            SlotSettlementBuilding,
            "asian_dynasty",
            "building");

        Add(p.largeStructure, P(Asian, "SM_Building03"), SlotLargeStructure, "asian_dynasty", "building");
        Add(p.largeStructure, P(Asian, "SM_MiniPavilionPlatform"), SlotLargeStructure, "asian_dynasty", "pavilion");
        // note: Asian hostile sites use courtyard landmarks and exterior modules rather than fallback buildings.
        Add(p.enemySite, P(Asian, "SM_MiniPavilionPlatform"), SlotEnemySite, "asian_dynasty", "pavilion", "camp_anchor");
        Add(p.enemySite, P(Asian, "SM_StairSet_1"), SlotEnemySite, "asian_dynasty", "stairs", "approach");
        Add(p.enemySite, P(Asian, "SM_ExteriorSet_BellSet_Column"), SlotEnemySite, "asian_dynasty", "bell_column", "landmark");
        Add(p.enemySite, P(Asian, "SM_ExteriorSet_MiniFountain_Column"), SlotEnemySite, "asian_dynasty", "fountain_column", "landmark");
        Add(p.enemySite, P(Asian, "SM_CarriageSet_1"), SlotEnemySite, "asian_dynasty", "carriage", "supplies");
        Add(p.floorDeco, P(Asian, "SM_Bazaar_Props1"), SlotFloorDeco, "asian_dynasty", "bazaar");
        Add(p.floorDeco, P(Asian, "SM_StoneChairs_1"), SlotFloorDeco, "asian_dynasty", "stone_chair");
        Add(p.wallDeco, P(Asian, "SM_ChineseDragon_1"), SlotWallDeco, "asian_dynasty", "dragon");
        Add(p.vegetation, P(Asian, "SM_Tree_03"), SlotVegetation, "asian_dynasty", "tree");
        Add(p.vegetation, P(Asian, "SM_Tree_04"), SlotVegetation, "asian_dynasty", "tree");
        Add(p.rock, P(Asian, "SM_Buddhas_1"), SlotRock, "asian_dynasty", "statue");
        Add(p.lighting, P(Asian, "SM_Bazaar_Props10"), SlotLighting, "asian_dynasty", "bazaar_light");
        Add(p.interiorDeco, P(Asian, "SM_Bazaar_Props12"), SlotInteriorDeco, "asian_dynasty", "market");
        Add(p.exteriorDeco, P(Asian, "SM_CarriageSet_1"), SlotExteriorDeco, "asian_dynasty", "carriage");
    }

    private static void FillPersepolis(GeneratedRegionAssetPaletteRecord p)
    {
        AddTerrain(p, GroundMat("ground2"), "stone", "empire");
        AddTerrain(p, GroundMat("ground10"), "dry_stone", "empire");
        Add(p.floor, P(Persepolis, "SM_FloorSetCustom_Base"), SlotFloor, "persepolis", "stone");
        Add(p.floor, P(Persepolis, "SM_CircularPlatform"), SlotFloor, "persepolis", "platform");
        Add(p.wall, P(Persepolis, "SM_WallSideArc"), SlotWall, "persepolis", "arc");
        Add(p.wall, P(Persepolis, "SM_GateWall_L"), SlotWall, "persepolis", "gate");
        Add(p.roof, P(Persepolis, "SM_ColumnHeads_Homa"), SlotRoof, "persepolis", "capital");
        Add(p.door, P(Persepolis, "SM_GateWall_L"), SlotDoor, "persepolis", "gate");
        Add(p.path, P(Persepolis, "SM_StairStopFlat_L"), SlotPath, "persepolis", "stairs");
        Add(p.largeStructure, P(Persepolis, "SM_ColumnBody_Big"), SlotLargeStructure, "persepolis", "column");
        Add(p.largeStructure, P(Persepolis, "SM_WingedLionKing"), SlotLargeStructure, "persepolis", "statue");
        // note: Imperial hostile sites are assembled from gates, columns, platforms, fire basins, and rubble.
        Add(p.enemySite, P(Persepolis, "SM_GateWall_L"), SlotEnemySite, "persepolis", "gate", "camp_anchor");
        Add(p.enemySite, P(Persepolis, "SM_ColumnBody_Big"), SlotEnemySite, "persepolis", "column", "cover");
        Add(p.enemySite, P(Persepolis, "SM_CircularPlatform"), SlotEnemySite, "persepolis", "platform", "ritual_site");
        Add(p.enemySite, P(Persepolis, "SM_FireBasin"), SlotEnemySite, "persepolis", "fire_basin", "threat_accent");
        Add(p.enemySite, P(Persepolis, "SM_RockSet_1"), SlotEnemySite, "persepolis", "rock", "rubble");
        Add(p.floorDeco, P(Persepolis, "SM_PlinthSet_Flower"), SlotFloorDeco, "persepolis", "plinth");
        Add(p.wallDeco, P(Persepolis, "SM_Murals_Soldier1"), SlotWallDeco, "persepolis", "mural");
        Add(p.vegetation, P(Persepolis, "SM_FolliageProp_1"), SlotVegetation, "persepolis", "foliage");
        Add(p.vegetation, P(Persepolis, "SM_Grasspatch_1"), SlotVegetation, "persepolis", "grass");
        Add(p.rock, P(Persepolis, "SM_MountainPiece_01"), SlotRock, "persepolis", "mountain");
        Add(p.lighting, P(Persepolis, "SM_FireBasin"), SlotLighting, "persepolis", "fire_basin");
        Add(p.interiorDeco, P(Persepolis, "SM_SimplePlinth"), SlotInteriorDeco, "persepolis", "plinth");
        Add(p.exteriorDeco, P(Persepolis, "SM_RockSet_1"), SlotExteriorDeco, "persepolis", "rock");
    }

    private static void FillVictorianMansion(GeneratedRegionAssetPaletteRecord p)
    {
        AddTerrain(p, GroundMat("ground11"), "manor_ground", "victorian");
        Add(p.floor, P(Victorian, "SM_Floor"), SlotFloor, "victorian", "wood");
        Add(p.floor, P(Victorian, "SM_CarpetLong"), SlotFloor, "victorian", "carpet");
        Add(p.wall, P(Victorian, "SM_TillFrame_Single"), SlotWall, "victorian", "frame");
        Add(p.wall, P(Victorian, "SM_CornerSimple"), SlotWall, "victorian", "corner");
        Add(p.roof, P(Victorian, "SM_RoofTile"), SlotRoof, "victorian", "tile");
        Add(p.door, P(Victorian, "SM_DoorCarved"), SlotDoor, "victorian", "carved");
        Add(p.path, P(Victorian, "SM_Stairs"), SlotPath, "victorian", "stairs");
        Add(p.largeStructure, P(Victorian, "SM_Fireplace"), SlotLargeStructure, "victorian", "fireplace");
        Add(p.largeStructure, P(Victorian, "SM_Bookshelf_BIG"), SlotLargeStructure, "victorian", "bookshelf");
        // note: Mansion hostile sites become haunted room clusters instead of settlement-building fallbacks.
        Add(p.enemySite, P(Victorian, "SM_Fireplace"), SlotEnemySite, "victorian", "fireplace", "room_anchor");
        Add(p.enemySite, P(Victorian, "SM_Bookshelf_BIG"), SlotEnemySite, "victorian", "bookshelf", "cover");
        Add(p.enemySite, P(Victorian, "SM_Fence"), SlotEnemySite, "victorian", "fence", "perimeter");
        Add(p.enemySite, P(Victorian, "SM_LionStatue"), SlotEnemySite, "victorian", "statue", "landmark");
        Add(p.enemySite, P(Victorian, "SM_BookPile1"), SlotEnemySite, "victorian", "books", "clutter");
        Add(p.floorDeco, P(Victorian, "SM_TravelCase_Basic"), SlotFloorDeco, "victorian", "case");
        Add(p.floorDeco, P(Victorian, "SM_CabinetMedium"), SlotFloorDeco, "victorian", "cabinet");
        Add(p.wallDeco, P(Victorian, "SM_PaintingLong"), SlotWallDeco, "victorian", "painting");
        Add(p.wallDeco, P(Victorian, "SM_CurtainBig"), SlotWallDeco, "victorian", "curtain");
        Add(p.vegetation, P(Victorian, "SM_FlowerBowl"), SlotVegetation, "victorian", "flower");
        Add(p.rock, P(Victorian, "SM_LionStatue"), SlotRock, "victorian", "statue");
        Add(p.lighting, P(Victorian, "SM_MansionLamp"), SlotLighting, "victorian", "lamp");
        Add(p.interiorDeco, P(Victorian, "SM_BookPile1"), SlotInteriorDeco, "victorian", "books");
        Add(p.exteriorDeco, P(Victorian, "SM_Fence"), SlotExteriorDeco, "victorian", "fence");
    }

    private static void FillContainerDistrict(GeneratedRegionAssetPaletteRecord p)
    {
        AddTerrain(p, GroundMat("ground12"), "industrial_ground", "container");
        Add(p.floor, P(Container, "SM_2M_PanelWall01"), SlotFloor, "container", "panel");
        Add(p.wall, P(Container, "SM_4M_PanelWall01"), SlotWall, "container", "panel");
        Add(p.wall, P(Container, "SM_4M_WoodenWall01"), SlotWall, "container", "wood_panel");
        Add(p.roof, P(Container, "SM_4M_PanelWall02"), SlotRoof, "container", "panel");
        Add(p.door, P(Container, "SM_BulkheadLight"), SlotDoor, "container", "bulkhead");
        Add(p.path, P(Container, "SM_Barrier01"), SlotPath, "container", "barrier");
        Add(p.largeStructure, P(Container, "SM_BigWaterTank"), SlotLargeStructure, "container", "water_tank");
        // note: Container camps now get modular barriers, tanks, antennae, crates, and improvised fencing.
        Add(p.enemySite, P(Container, "SM_Barrier01"), SlotEnemySite, "container", "barrier", "perimeter");
        Add(p.enemySite, P(Container, "SM_Barrier02"), SlotEnemySite, "container", "barrier", "perimeter");
        Add(p.enemySite, P(Container, "SM_BigWaterTank"), SlotEnemySite, "container", "water_tank", "landmark");
        Add(p.enemySite, P(Container, "SM_Box01"), SlotEnemySite, "container", "box", "supplies");
        Add(p.enemySite, P(Container, "SM_Antenna01"), SlotEnemySite, "container", "antenna", "landmark");
        Add(p.enemySite, P(Container, "SM_BranchFence01"), SlotEnemySite, "container", "branch_fence", "perimeter");
        Add(p.floorDeco, P(Container, "SM_Box01"), SlotFloorDeco, "container", "box");
        Add(p.floorDeco, P(Container, "SM_Barrel01"), SlotFloorDeco, "container", "barrel");
        Add(p.wallDeco, P(Container, "SM_Antenna01"), SlotWallDeco, "container", "antenna");
        Add(p.vegetation, P(Container, "SM_BranchFence01"), SlotVegetation, "container", "scrub");
        Add(p.rock, P(Container, "SM_BranchLong01"), SlotRock, "container", "debris");
        Add(p.lighting, P(Container, "SM_BulkheadLight"), SlotLighting, "container", "light");
        Add(p.interiorDeco, P(Container, "SM_Bottle01"), SlotInteriorDeco, "container", "clutter");
        Add(p.exteriorDeco, P(Container, "SM_Barrier02"), SlotExteriorDeco, "container", "barrier");
    }

    private static void FillBioHorror(GeneratedRegionAssetPaletteRecord p)
    {
        AddTerrain(p, GroundMat("ground13"), "corrupt_ground", "bio_horror");
        Add(p.floor, P(BioHorror, "SM_ScifiTile"), SlotFloor, "bio_horror", "scifi_tile");
        Add(p.wall, P(BioHorror, "SM_ScifiFrame"), SlotWall, "bio_horror", "frame");
        Add(p.wall, P(BioHorror, "SM_StructureSet_1"), SlotWall, "bio_horror", "structure");
        Add(p.roof, P(BioHorror, "SM_HolderRoof"), SlotRoof, "bio_horror", "roof");
        Add(p.door, P(BioHorror, "SM_Door"), SlotDoor, "bio_horror", "door");
        Add(p.path, P(BioHorror, "SM_WallRamp"), SlotPath, "bio_horror", "ramp");

        Add(
            p.settlementBuilding,
            P(BioHorror, "SM_StructureSet_1"),
            SlotSettlementBuilding,
            "bio_horror",
            "structure");

        Add(
            p.settlementBuilding,
            P(BioHorror, "SM_StructureSet_2"),
            SlotSettlementBuilding,
            "bio_horror",
            "structure");

        Add(
            p.settlementBuilding,
            P(BioHorror, "SM_StructureSet_3"),
            SlotSettlementBuilding,
            "bio_horror",
            "structure");

        Add(
            p.settlementBuilding,
            P(BioHorror, "SM_StructureSet_4"),
            SlotSettlementBuilding,
            "bio_horror",
            "structure");

        Add(p.largeStructure, P(BioHorror, "SM_StructureSet_2"), SlotLargeStructure, "bio_horror", "structure");
        // note: Bio-horror sites use corruption modules and bone/cable accents as hostile architecture.
        Add(p.enemySite, P(BioHorror, "SM_BioMassSet_01_01"), SlotEnemySite, "bio_horror", "biomass");
        Add(p.enemySite, P(BioHorror, "SM_BioMassSet_02_01"), SlotEnemySite, "bio_horror", "biomass", "corruption");
        Add(p.enemySite, P(BioHorror, "SM_BonePile1"), SlotEnemySite, "bio_horror", "bones", "threat_accent");
        Add(p.enemySite, P(BioHorror, "SM_CornerPile_1"), SlotEnemySite, "bio_horror", "corner_pile", "cover");
        Add(p.enemySite, P(BioHorror, "SM_BigCables"), SlotEnemySite, "bio_horror", "cables", "perimeter");
        Add(p.enemySite, P(BioHorror, "SM_ConnectorDevice"), SlotEnemySite, "bio_horror", "device", "landmark");
        Add(p.floorDeco, P(BioHorror, "SM_BonePile1"), SlotFloorDeco, "bio_horror", "bones");
        Add(p.floorDeco, P(BioHorror, "SM_BioMassSet_02_01"), SlotFloorDeco, "bio_horror", "biomass");
        Add(p.wallDeco, P(BioHorror, "SM_WallDecor"), SlotWallDeco, "bio_horror", "wall_decor");
        Add(p.vegetation, P(BioHorror, "SM_ShroomSet_1"), SlotVegetation, "bio_horror", "shroom");
        Add(p.rock, P(BioHorror, "SM_CornerPile_1"), SlotRock, "bio_horror", "pile");
        Add(p.lighting, P(BioHorror, "SM_StandLight"), SlotLighting, "bio_horror", "stand_light");
        Add(p.lighting, P(BioHorror, "SM_WallLight"), SlotLighting, "bio_horror", "wall_light");
        Add(p.interiorDeco, P(BioHorror, "SM_ConnectorDevice"), SlotInteriorDeco, "bio_horror", "device");
        Add(p.exteriorDeco, P(BioHorror, "SM_BigCables"), SlotExteriorDeco, "bio_horror", "cables");
    }

    private static void FillSciFiEngineersRoom(GeneratedRegionAssetPaletteRecord p)
    {
        AddTerrain(p, GroundMat("ground12"), "clean_scifi", "engineers_room");
        AddTerrain(p, GroundMat("ground13"), "dark_panel", "engineers_room");
        Add(p.floor, P(SciFiEngineers, "SM_Carpet"), SlotFloor, "scifi_engineers_room", "floor");
        Add(p.floor, P(SciFiEngineers, "SM_Corridor_Shield"), SlotFloor, "scifi_engineers_room", "panel_floor");
        Add(p.wall, P(SciFiEngineers, "SM_Wall_1m"), SlotWall, "scifi_engineers_room", "wall");
        Add(p.wall, P(SciFiEngineers, "SM_Wall_2m"), SlotWall, "scifi_engineers_room", "wall");
        Add(p.wall, P(SciFiEngineers, "SM_Wall_Gen_Panel"), SlotWall, "scifi_engineers_room", "panel");
        Add(p.roof, P(SciFiEngineers, "SM_Wall_Gen_Shields_Big"), SlotRoof, "scifi_engineers_room", "ceiling_panel");
        Add(p.door, P(SciFiEngineers, "SM_Door_Border"), SlotDoor, "scifi_engineers_room", "door");
        Add(p.door, P(SciFiEngineers, "SM_Door_Control"), SlotDoor, "scifi_engineers_room", "door_control");
        Add(p.path, P(SciFiEngineers, "SM_Corridor_Shield"), SlotPath, "scifi_engineers_room", "corridor");
        Add(p.path, P(SciFiEngineers, "SM_Vent_Shield"), SlotPath, "scifi_engineers_room", "vent");

        // note: These modules make a readable clean sci-fi room palette distinct from the corrupted bio-horror pack.
        Add(p.settlementBuilding, P(SciFiEngineers, "SM_Wall_Gen"), SlotSettlementBuilding, "scifi_engineers_room", "module");
        Add(p.settlementBuilding, P(SciFiEngineers, "SM_Wall_Gen_Shields_Big"), SlotSettlementBuilding, "scifi_engineers_room", "shielded_module");
        Add(p.largeStructure, P(SciFiEngineers, "SM_Wall_Gen_Shields_Big"), SlotLargeStructure, "scifi_engineers_room", "shielded_module");
        Add(p.largeStructure, P(SciFiEngineers, "SM_Server_Box"), SlotLargeStructure, "scifi_engineers_room", "server_box");
        // note: Clean sci-fi hostile sites are built from server, tube, wall, vent, and workstation modules.
        Add(p.enemySite, P(SciFiEngineers, "SM_Server_Box"), SlotEnemySite, "scifi_engineers_room", "server_site");
        Add(p.enemySite, P(SciFiEngineers, "SM_Box_Tube_02"), SlotEnemySite, "scifi_engineers_room", "tube_site");
        Add(p.enemySite, P(SciFiEngineers, "SM_Box_Wall"), SlotEnemySite, "scifi_engineers_room", "box_wall", "cover");
        Add(p.enemySite, P(SciFiEngineers, "SM_Vent_Shield"), SlotEnemySite, "scifi_engineers_room", "vent", "approach");
        Add(p.enemySite, P(SciFiEngineers, "SM_Wall_Column"), SlotEnemySite, "scifi_engineers_room", "wall_column", "cover");
        Add(p.enemySite, P(SciFiEngineers, "SM_Wall_Gen_Shields"), SlotEnemySite, "scifi_engineers_room", "shielded_module", "perimeter");
        Add(p.enemySite, P(SciFiEngineers, "SM_KeyBD_Table"), SlotEnemySite, "scifi_engineers_room", "workstation", "landmark");
        Add(p.floorDeco, P(SciFiEngineers, "SM_Box_Tube_01"), SlotFloorDeco, "scifi_engineers_room", "tube_box");
        Add(p.floorDeco, P(SciFiEngineers, "SM_Tumba_Corner"), SlotFloorDeco, "scifi_engineers_room", "corner_unit");
        Add(p.wallDeco, P(SciFiEngineers, "SM_Monitor_Panel"), SlotWallDeco, "scifi_engineers_room", "monitor");
        Add(p.wallDeco, P(SciFiEngineers, "SM_Wall_Wires"), SlotWallDeco, "scifi_engineers_room", "wires");
        Add(p.rock, P(SciFiEngineers, "SM_RobbyCube"), SlotRock, "scifi_engineers_room", "machine_block");
        Add(p.lighting, P(SciFiEngineers, "SM_Lamp_Ceil"), SlotLighting, "scifi_engineers_room", "ceiling_light");
        Add(p.lighting, P(SciFiEngineers, "SM_Lamp_Table"), SlotLighting, "scifi_engineers_room", "table_light");
        Add(p.interiorDeco, P(SciFiEngineers, "SM_KeyBD_Table"), SlotInteriorDeco, "scifi_engineers_room", "workstation");
        Add(p.interiorDeco, P(SciFiEngineers, "SM_Server_Box"), SlotInteriorDeco, "scifi_engineers_room", "server_box");
        Add(p.exteriorDeco, P(SciFiEngineers, "SM_Tubes_Corner"), SlotExteriorDeco, "scifi_engineers_room", "tubes");
        Add(p.exteriorDeco, P(SciFiEngineers, "SM_Wall_Tubes_1"), SlotExteriorDeco, "scifi_engineers_room", "wall_tubes");
    }

    private static void AddSharedUtilityAssets(GeneratedRegionAssetPaletteRecord p)
    {
        Add(p.lootContainer, Chests + "ChestSimpleSmall.prefab", SlotLootContainer, "loot", "simple");
        Add(p.lootContainer, Chests + "ChestOrnateMedium.prefab", SlotLootContainer, "loot", "ornate");

        // note: Surface regions without a native cave entrance still expose a curated subterranean POI rather than silently losing cave gameplay.
        if (!ContainsSemanticReference(p.enemySite, "cave", "underground", "mine", "tunnel", "cavern"))
        {
            Add(p.enemySite, P(Viking, "SM_WoodenUGEntrance"), SlotEnemySite, "subterranean", "cave_entrance");
        }

        if (p.enemySite.Count == 0 && p.largeStructure.Count > 0)
        {
            for (int i = 0; i < p.largeStructure.Count && i < 3; i++)
                p.enemySite.Add(CloneAsSlot(p.largeStructure[i], SlotEnemySite, "enemy_site"));
        }

        if (p.exteriorDeco.Count == 0 && p.floorDeco.Count > 0)
            p.exteriorDeco.Add(CloneAsSlot(p.floorDeco[0], SlotExteriorDeco, "exterior"));
        if (p.interiorDeco.Count == 0 && p.floorDeco.Count > 0)
            p.interiorDeco.Add(CloneAsSlot(p.floorDeco[0], SlotInteriorDeco, "interior"));
    }

    private static bool ContainsSemanticReference(
        List<GeneratedAssetReferenceRecord> references,
        params string[] terms)
    {
        if (references == null || terms == null)
            return false;

        for (int i = 0;
             i < references.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                references[i];

            if (reference == null)
                continue;

            string semantic =
                NormalizeSearchText(
                    (reference.assetPath ?? string.Empty) +
                    " " +
                    (reference.assetKey ?? string.Empty));

            if (ContainsAny(semantic, terms))
                return true;
        }

        return false;
    }

    private static void AddDiscoveredAssets(GeneratedRegionAssetPaletteRecord p)
    {
        if (p == null)
            return;

        YQDiscoveredWorldAssetCatalog catalog =
            YQDiscoveredWorldAssetCatalog.Instance;

        if (catalog == null ||
            catalog.Entries == null)
        {
            return;
        }

        int added =
            0;

        for (int i = 0;
             i < catalog.Entries.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                catalog.Entries[i];

            if (reference == null)
                continue;

            reference.EnsureCollections();

            // note: Repair stale discovery catalogs before choosing a palette slot; complete authored prefabs must never remain scatterable decoration.
            string resolvedSlot =
                ResolveRuntimeDiscoveredSlot(
                    reference);

            if (!IsAllowedWorldReferenceForSlot(
                    reference,
                    resolvedSlot))
            {
                continue;
            }

            // note: A discovered asset may join the palette only when it belongs to this palette's owning pack family.
            if (!IsSafeDiscoveredAssetForPalette(
                    p,
                    reference))
            {
                continue;
            }

            if (!DiscoveredAssetMatchesPalette(
                    p,
                    reference))
            {
                continue;
            }

            List<GeneratedAssetReferenceRecord> slot =
                GetSlotList(
                    p,
                    resolvedSlot);

            if (slot == null ||
                ContainsAssetPath(
                    slot,
                    reference.assetPath))
            {
                continue;
            }

            GeneratedAssetReferenceRecord clone =
                CloneDiscoveredReference(
                    reference);

            ApplySlotContract(
                clone,
                resolvedSlot);

            // note: Discovered assets are cloned into the generated palette so runtime placement can mutate records safely.
            slot.Add(
                clone);

            added++;
        }

        if (added > 0)
        {
            AddUnique(
                p.verboseInternals,
                "discovered_asset_catalog_entries_merged=" +
                added);
        }
    }

    private static string ResolveRuntimeDiscoveredSlot(
        GeneratedAssetReferenceRecord reference)
    {
        if (reference == null)
            return string.Empty;

        // note: Older generated catalogs predate whole-building recognition; semantic validation safely promotes only complete structures, never walls or roof pieces.
        if (string.Equals(
                reference.assetType,
                "prefab",
                StringComparison.OrdinalIgnoreCase) &&
            IsAllowedWorldReferenceForSlot(
                reference,
                SlotSettlementBuilding))
        {
            return SlotSettlementBuilding;
        }

        return reference.slotTag;
    }

    private static void ApplySlotContract(
        GeneratedAssetReferenceRecord reference,
        string slot)
    {
        if (reference == null || string.IsNullOrWhiteSpace(slot))
            return;

        bool slotChanged =
            !string.Equals(
                reference.slotTag,
                slot,
                StringComparison.OrdinalIgnoreCase);

        reference.slotTag = slot;

        if (slotChanged ||
            string.Equals(
                slot,
                SlotSettlementBuilding,
                StringComparison.OrdinalIgnoreCase))
        {
            // note: A promoted building receives the complete-lot contract instead of retaining decoration scale, footprint, repetition, and placement rules.
            reference.weight = Math.Max(reference.weight, ResolveWeight(slot, reference.subTags != null ? reference.subTags.ToArray() : null));
            reference.scaleMin = ResolveScaleMin(slot);
            reference.scaleMax = ResolveScaleMax(slot);
            reference.footprintX = Math.Max(reference.footprintX, ResolveFootprint(slot));
            reference.footprintZ = Math.Max(reference.footprintZ, ResolveFootprint(slot));
            reference.placementRule = ResolvePlacementRule(slot);
            reference.rotationRule = ResolveRotationRule(slot);
            reference.allowRepeat = AllowsRepeat(slot);
            reference.blocksNav = BlocksNav(slot);
            AddUnique(reference.subTags, "curated_complete_building");
        }
    }

    private static bool IsSafeDiscoveredAssetForPalette(
        GeneratedRegionAssetPaletteRecord palette,
        GeneratedAssetReferenceRecord reference)
    {
        if (palette == null || reference == null)
            return false;

        string path = (reference.assetPath ?? string.Empty).Replace('\\', '/');
        string style = NormalizeKey(palette.styleKey);

        // note: Neutral terrain and loot can be shared, while architecture must always resolve to the selected kit.
        bool neutralDressing = path.IndexOf("Assets/Tom's Terrain Tools/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              path.IndexOf("Assets/YughuesFreeBushes2018/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              path.IndexOf("Assets/ADG_Textures/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              path.IndexOf("Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/", StringComparison.OrdinalIgnoreCase) >= 0;

        if (neutralDressing)
            return true;

        string pack = ResolvePackName(style).Replace('\\', '/');

        return !string.IsNullOrWhiteSpace(pack) &&
               path.IndexOf("Assets/" + pack + "/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool DiscoveredAssetMatchesPalette(
        GeneratedRegionAssetPaletteRecord palette,
        GeneratedAssetReferenceRecord reference)
    {
        if (palette == null ||
            reference == null ||
            reference.styleTags == null)
        {
            return false;
        }

        for (int i = 0;
             i < reference.styleTags.Count;
             i++)
        {
            string style =
                NormalizeKey(
                    reference.styleTags[i]);

            if (string.IsNullOrWhiteSpace(
                    style))
            {
                continue;
            }

            // note: Region metadata is descriptive and must not act as a cross-pack asset-selection wildcard.
            if (style == NormalizeKey(
                    palette.styleKey))
            {
                return true;
            }

            if (style == "all")
            {
                // note: Legacy discovered catalogs used 'all'; the preceding exact pack-path gate still prevents cross-palette mixing.
                return true;
            }
        }

        return false;
    }

    private static bool IsAllowedWorldReferenceForSlot(
        GeneratedAssetReferenceRecord reference,
        string requestedSlot)
    {
        if (reference == null)
            return false;

        string slot =
            NormalizeKey(
                string.IsNullOrWhiteSpace(
                    requestedSlot)
                    ? reference.slotTag
                    : requestedSlot);

        string semantic =
            NormalizeSearchText(
                (reference.assetPath ?? string.Empty) +
                " " +
                (reference.assetKey ?? string.Empty));

        string normalizedPath =
            (reference.assetPath ?? string.Empty).Replace('\\', '/');

        int lastSlash =
            normalizedPath.LastIndexOf('/');

        // note: Structural identity comes from the prefab filename; pack folders such as HouseOnAHill must not turn every contained prop into a building.
        string prefabSemantic =
            NormalizeSearchText(
                lastSlash >= 0
                    ? normalizedPath.Substring(lastSlash + 1)
                    : normalizedPath);

        if (reference.subTags != null)
        {
            semantic +=
                " " +
                NormalizeSearchText(
                    string.Join(
                        " ",
                        reference.subTags));
        }

        // note: Old registries/saves could mark actor/effect fragments as world sites; keep them out before selection.
        if (ContainsAny(
                semantic,
                "particle",
                "particles",
                "audio",
                "sound",
                "music",
                "animation",
                "animator",
                "controller",
                "demo",
                "showcase",
                "preview",
                "example",
                "editor",
                "ui",
                "icon",
                "weapon",
                "sword",
                "dagger",
                "axe",
                "bow",
                "staff",
                "armor",
                "helmet",
                "exported meshes",
                "no assigned materials",
                "characters demons",
                "characters devils",
                "characters dragons",
                "characters spiders",
                "characters rock monsters"))
        {
            return false;
        }

        if (slot == SlotSettlementBuilding)
        {
            // note: The current settlement builder places complete lots, not individual construction-kit modules.
            return ContainsAny(
                       prefabSemantic,
                       "house",
                       "hut",
                       "shack",
                       "building",
                       "church",
                       "saloon",
                       "stable",
                       "tower",
                       "barn",
                       "cabin",
                       "hall",
                       "tipi",
                       "teepee",
                       "lean too",
                       "leanto",
                       "tent") &&
                   !ContainsAny(
                       prefabSemantic,
                       "wall",
                       "corner",
                       "pillar",
                       "column",
                       "front",
                       "rear",
                       "back",
                       "side",
                       "base",
                       "body",
                       "section",
                       "roof",
                       "floor",
                       "door",
                       "window",
                       "frame",
                       "beam",
                       "trim",
                       "stairs",
                       "gate",
                       "fence",
                       "module",
                       "structure",
                       "kit",
                       "prop");
        }

        if (slot != SlotEnemySite)
            return true;

        return ContainsAny(
            semantic,
            "camp",
            "encampment",
            "outpost",
            "redoubt",
            "watchpost",
            "watchtower",
            "tower",
            "lair",
            "nest",
            "crypt",
            "cave",
            "mine",
            "ruin",
            "burrow",
            "shipwreck",
            "shrine",
            "gate",
            "barrier",
            "barricade",
            "fence",
            "wall",
            "palisade",
            "spike",
            "bone",
            "biomass",
            "structure",
            "server",
            "container",
            "tank",
            "cart",
            "crate",
            "box",
            "barrel",
            "fire",
            "debris",
            "rubble",
            "entrance");
    }

    private static GeneratedAssetReferenceRecord CloneDiscoveredReference(
        GeneratedAssetReferenceRecord source)
    {
        GeneratedAssetReferenceRecord clone =
            new GeneratedAssetReferenceRecord
            {
                assetKey = source != null ? source.assetKey : string.Empty,
                assetPath = source != null ? source.assetPath : string.Empty,
                assetType = source != null ? source.assetType : "prefab",
                slotTag = source != null ? source.slotTag : string.Empty,
                weight = source != null ? source.weight : 1,
                scaleMin = source != null ? source.scaleMin : 1f,
                scaleMax = source != null ? source.scaleMax : 1f,
                footprintX = source != null ? source.footprintX : 1f,
                footprintZ = source != null ? source.footprintZ : 1f,
                placementRule = source != null ? source.placementRule : string.Empty,
                rotationRule = source != null ? source.rotationRule : string.Empty,
                allowRepeat = source != null && source.allowRepeat,
                blocksNav = source != null && source.blocksNav,
                notes = source != null ? source.notes : string.Empty
            };

        clone.EnsureCollections();

        if (source != null)
        {
            CopyTags(
                source.subTags,
                clone.subTags);

            CopyTags(
                source.styleTags,
                clone.styleTags);
        }

        return clone;
    }

    private static bool ContainsAssetPath(
        List<GeneratedAssetReferenceRecord> list,
        string assetPath)
    {
        if (list == null ||
            string.IsNullOrWhiteSpace(
                assetPath))
        {
            return false;
        }

        string normalized =
            assetPath.Replace(
                '\\',
                '/');

        for (int i = 0;
             i < list.Count;
             i++)
        {
            GeneratedAssetReferenceRecord existing =
                list[i];

            if (existing == null)
                continue;

            if (string.Equals(
                    existing.assetPath != null
                        ? existing.assetPath.Replace(
                            '\\',
                            '/')
                        : string.Empty,
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddPaletteRules(GeneratedRegionAssetPaletteRecord p)
    {
        AddUnique(p.layoutRules, "Pick one styleKey per region and prefer assets from that palette for all structural slots.");
        AddUnique(p.layoutRules, "Place floor/path/large_structure before deco; deco must never decide the room footprint.");
        AddUnique(p.layoutRules, "Do not mix forbiddenStyleTags in the same room unless an LLM-authored transition/portal explicitly asks for it.");
        AddUnique(p.layoutRules, "Use floor_deco for clutter on walkable surfaces; use wall_deco only on validated walls.");
        AddUnique(p.layoutRules, "Enemy encampments reuse the region palette with one threat accent, not a totally unrelated prop set.");

        switch (p.styleKey)
        {
            case "victorian_mansion":
                AddUnique(p.forbiddenStyleTags, "desert_ruin");
                AddUnique(p.forbiddenStyleTags, "container");
                AddUnique(p.layoutRules, "Victorian interiors favor grid rooms, carpets centered on floors, paintings on full wall spans, and bookshelves along edges.");
                break;
            case "ancient_desert_ruins":
            case "persepolis_empire":
                AddUnique(p.forbiddenStyleTags, "victorian");
                AddUnique(p.forbiddenStyleTags, "scifi");
                AddUnique(p.layoutRules, "Ruin layouts favor broken walls, columns, sandy paths, rubble clusters, and partial cover.");
                break;
            case "container_district":
            case "bio_horror_scifi":
            case "scifi_engineers_room":
            case "hivemind_sewers":
            case "hivemind_cyberpunk_city":
            case "hivemind_military_camp":
            case "hivemind_horror_hospital":
                AddUnique(p.forbiddenStyleTags, "thatch");
                AddUnique(p.forbiddenStyleTags, "manor");
                AddUnique(p.layoutRules, "Industrial layouts favor panel modules, cables, barriers, lights, and controlled sightlines.");
                break;
            case "hivemind_medieval_kingdom":
            case "hivemind_rural_town":
            case "hivemind_modular_viking_village":
            case "hivemind_town_smith":
            case "hivemind_woodland_village":
                AddUnique(p.forbiddenStyleTags, "clean_scifi");
                AddUnique(p.forbiddenStyleTags, "hospital");
                AddUnique(p.layoutRules, "Grounded settlement layouts favor modular homes, shops, fences, tools, work yards, and readable footpaths.");
                break;
            case "hivemind_pirate_island":
                AddUnique(p.forbiddenStyleTags, "clean_scifi");
                AddUnique(p.forbiddenStyleTags, "mansion_carpet");
                AddUnique(p.layoutRules, "Pirate/coastal layouts favor docks, crates, cages, barrels, shore paths, ropes, and modular wood structures.");
                break;
            case "hivemind_gothic_cathedral":
            case "hivemind_haunted_village":
            case "hivemind_witch_house":
            case "hivemind_house_on_hill":
                AddUnique(p.forbiddenStyleTags, "clean_scifi");
                AddUnique(p.forbiddenStyleTags, "neon");
                AddUnique(p.layoutRules, "Haunted/cathedral layouts favor rooms, halls, arches, candles, yards, fences, and controlled spooky negative space.");
                break;
            case "hivemind_gladiator_arena":
            case "hivemind_olympus_temple":
                AddUnique(p.forbiddenStyleTags, "clean_scifi");
                AddUnique(p.forbiddenStyleTags, "hospital");
                AddUnique(p.layoutRules, "Arena/temple layouts favor rings, columns, stairs, platforms, sightlines, and ceremonial encounter spaces.");
                break;
            case "hivemind_hallowed_depths":
            case "hivemind_mountain_messenger":
            case "hivemind_mystic_dungeon":
            case "hivemind_mountain_temple":
            case "hivemind_cave_tomb":
                AddUnique(p.forbiddenStyleTags, "clean_scifi");
                AddUnique(p.forbiddenStyleTags, "mansion_carpet");
                AddUnique(p.layoutRules, "Dungeon/mountain layouts favor modular stone cells, gates, stairs, ruins, ledges, and readable encounter chambers.");
                break;
            default:
                AddUnique(p.forbiddenStyleTags, "scifi");
                AddUnique(p.forbiddenStyleTags, "mansion_carpet");
                AddUnique(p.layoutRules, "Forest/rural layouts favor dirt paths, log walls, wood roofs, vegetation clusters, and readable paths between buildings.");
                break;
        }
    }

    private static string ResolveStyleKey(GeneratedRegionRecord region)
    {
        string authoredStyle =
            region != null
                ? NormalizeKey(
                    region.assetStyleKey)
                : string.Empty;

        if (IsSupportedStyleKey(
                authoredStyle))
        {
            // note: A validated semantic style key is canonical LLM intent; keyword inference is only a fallback for older saves.
            return authoredStyle;
        }

        string text = BuildRegionText(region);

        // note: Each generated region selects exactly one authored asset family; narrative tags choose variety without blending families.
        if (ContainsAny(text, "hospital", "clinic", "medical ward", "operating room"))
            return "hivemind_horror_hospital";
        if (ContainsAny(text, "bio", "horror", "corrupt", "biomass", "flesh", "experiment"))
            return "bio_horror_scifi";
        if (ContainsAny(text, "cyberpunk", "neon", "megacity", "hologram", "street market"))
            return "hivemind_cyberpunk_city";
        if (ContainsAny(text, "military camp", "barracks", "war camp", "outpost", "checkpoint", "fortified camp"))
            return "hivemind_military_camp";
        if (ContainsAny(text, "medieval kingdom", "castle", "keep", "fortress", "kingdom", "battlement"))
            return "hivemind_medieval_kingdom";
        if (ContainsAny(text, "gothic", "cathedral", "chapel", "sanctum", "crypt church"))
            return "hivemind_gothic_cathedral";
        if (ContainsAny(text, "gladiator", "arena", "colosseum", "bloodsport"))
            return "hivemind_gladiator_arena";
        if (ContainsAny(text, "villa forge", "villa", "estate forge"))
            return "hivemind_villa_forge";
        if (ContainsAny(text, "rural town", "cottage town", "market town", "farm town"))
            return "hivemind_rural_town";
        if (ContainsAny(text, "town smith", "blacksmith", "forge", "smithy", "workshop"))
            return "hivemind_town_smith";
        if (ContainsAny(text, "haunted village", "haunted", "ghost village", "abandoned village"))
            return "hivemind_haunted_village";
        if (ContainsAny(text, "mystic dungeon", "ritual dungeon", "magic dungeon"))
            return "hivemind_mystic_dungeon";
        if (ContainsAny(text, "mountain temple", "temple peak", "high shrine"))
            return "hivemind_mountain_temple";
        if (ContainsAny(text, "modular viking village", "modular viking"))
            return "hivemind_modular_viking_village";
        if (ContainsAny(text, "woodland village", "tribal village", "forest camp", "woodland settlement"))
            return "hivemind_woodland_village";
        if (ContainsAny(text, "witch", "witch house", "coven", "hag", "occult cottage"))
            return "hivemind_witch_house";
        if (ContainsAny(text, "hidden tomb", "cave tomb", "buried tomb", "tomb cave"))
            return "hivemind_cave_tomb";
        if (ContainsAny(text, "house on a hill", "hill house", "lonely house", "hilltop manor"))
            return "hivemind_house_on_hill";
        if (ContainsAny(text, "olympus", "marble temple", "greek temple", "divine temple"))
            return "hivemind_olympus_temple";
        if (ContainsAny(text, "pirate", "island", "docks", "dock", "beach", "shipwreck", "coast", "coastal"))
            return "hivemind_pirate_island";
        if (ContainsAny(text, "hallowed", "depths", "dungeon", "catacomb", "undercrypt", "underground dungeon"))
            return "hivemind_hallowed_depths";
        if (ContainsAny(text, "sewer", "sewers", "cistern", "drain", "tunnel", "water channel"))
            return "hivemind_sewers";
        if (ContainsAny(text, "messenger", "mountain", "ancient mountain", "cliff path", "high pass"))
            return "hivemind_mountain_messenger";
        if (ContainsAny(text, "container", "district", "industrial", "scrap", "antenna", "panel"))
            return "container_district";
        if (ContainsAny(text, "engineer", "engineering", "server", "generator", "workstation", "laboratory", "lab", "clean sci", "ship room"))
            return "scifi_engineers_room";
        if (ContainsAny(text, "victorian", "mansion", "manor", "archive", "library", "book", "study", "noble"))
            return "victorian_mansion";
        if (ContainsAny(text, "asian", "dynasty", "pavilion", "bazaar", "jade", "dragon", "temple"))
            return "asian_dynasty";
        if (ContainsAny(text, "persepolis", "empire", "column", "palace", "mural", "plinth"))
            return "persepolis_empire";
        if (ContainsAny(text, "western", "saloon", "mine", "rail", "cactus", "badland"))
            return "western_desert_town";
        if (ContainsAny(text, "desert", "sand", "ruin", "ashfield", "dry", "crypt", "tomb"))
            return "ancient_desert_ruins";
        if (ContainsAny(text, "viking", "rural", "stable", "wooden", "highland", "farm", "hamlet"))
            return "viking_rural";

        // note: The default remains Nordic forest when a region's generated tags do not select a more specific authored family.
        return "nordic_forest";
    }

    private static string BuildRegionText(GeneratedRegionRecord region)
    {
        if (region == null)
            return string.Empty;

        string tags = region.biomeTags != null ? string.Join(" ", region.biomeTags) : string.Empty;
        return (region.displayName + " " + region.role + " " + region.scaleHint + " " + region.terrainProfile + " " + region.climateProfile + " " +
                region.playerPressure + " " + region.lore + " " + region.gameplayPremise + " " + region.traversalHook + " " + region.economyHook + " " +
                region.enemyPressureHook + " " + tags).ToLowerInvariant();
    }

    private static string ResolvePackName(string style)
    {
        switch (style)
        {
            case "ancient_desert_ruins": return "BefourStudios/AncientDesertRuins";
            case "western_desert_town": return "BefourStudios/WesternDesertTown";
            case "asian_dynasty": return "BefourStudios/AsianDynastyEnvironment";
            case "persepolis_empire": return "BefourStudios/PersepolisEmpireEnvironment";
            case "victorian_mansion": return "BefourStudios/VictorianMansionEnvironment";
            case "container_district": return "BefourStudios/ContainerDistrict";
            case "bio_horror_scifi": return "BefourStudios/BioHorrorSciFiEnvironment";
            case "scifi_engineers_room": return "BefourStudios/SciFiEngineersRoom";
            case "hivemind_medieval_kingdom": return "HIVEMIND/MedievalKingdom";
            case "hivemind_military_camp": return "HIVEMIND/MilitaryCamp";
            case "hivemind_gothic_cathedral": return "HIVEMIND/GothicCathedral";
            case "hivemind_cyberpunk_city": return "HIVEMIND/CyberpunkCity";
            case "hivemind_gladiator_arena": return "HIVEMIND/GladiatorArena";
            case "hivemind_rural_town": return "HIVEMIND/RuralTown";
            case "hivemind_modular_viking_village": return "HIVEMIND/ModularVikingVillage";
            case "hivemind_town_smith": return "HIVEMIND/TownSmith";
            case "hivemind_haunted_village": return "HIVEMIND/HauntedVillage";
            case "hivemind_mystic_dungeon": return "HIVEMIND/MysticDungeon";
            case "hivemind_mountain_temple": return "HIVEMIND/MountainTemple";
            case "hivemind_woodland_village": return "HIVEMIND/NativeAmericanVillage";
            case "hivemind_witch_house": return "HIVEMIND/WitchHouse";
            case "hivemind_cave_tomb": return "HIVEMIND/CaveOfHiddenTomb";
            case "hivemind_house_on_hill": return "HIVEMIND/HouseOnaHill";
            case "hivemind_villa_forge": return "HIVEMIND/VillaForge";
            case "hivemind_horror_hospital": return "HIVEMIND/HorrorHospital";
            case "hivemind_olympus_temple": return "HIVEMIND/OlympusTemple";
            case "hivemind_pirate_island": return "HIVEMIND/PirateIsland";
            case "hivemind_hallowed_depths": return "HIVEMIND/HallowedDepths";
            case "hivemind_sewers": return "HIVEMIND/TheSewers";
            case "hivemind_mountain_messenger": return "HIVEMIND/TheMessenger";
            case "viking_rural": return "BefourStudios/MedievalVikingVillage";
            default: return "BefourStudios/NordicVillage";
        }
    }

    private static string ResolveNaturePack(string style)
    {
        switch (style)
        {
            case "ancient_desert_ruins":
            case "western_desert_town":
                return ResolvePackName(style) + " vegetation";
            case "bio_horror_scifi":
                return "BioHorrorSciFiEnvironment shrooms/biomass";
            case "scifi_engineers_room":
                return "SciFiEngineersRoom clean interior props";
            case "hivemind_medieval_kingdom":
                return "MedievalKingdom castle, market, stone, wood, and town dressing";
            case "hivemind_military_camp":
                return "MilitaryCamp tents, barricades, vehicles, supplies, and fortified clutter";
            case "hivemind_gothic_cathedral":
                return "GothicCathedral arches, stained stone, pews, crypts, and sanctuary props";
            case "hivemind_cyberpunk_city":
                return "CyberpunkCity neon streets, shop fronts, balconies, cables, and dense urban props";
            case "hivemind_gladiator_arena":
                return "GladiatorArena stone seats, arena floors, columns, banners, and combat props";
            case "hivemind_rural_town":
                return "RuralTown cottages, fences, gardens, workshops, and village clutter";
            case "hivemind_modular_viking_village":
                return "ModularVikingVillage timber halls, shields, roofs, paths, and settlement modules";
            case "hivemind_town_smith":
                return "TownSmith forge, metalwork, shop props, shields, tools, and heat-heavy clutter";
            case "hivemind_haunted_village":
                return "HauntedVillage ruined homes, dead trees, fences, and anxious rural props";
            case "hivemind_mystic_dungeon":
                return "MysticDungeon ritual rooms, stone modules, gates, mosaics, and magic clutter";
            case "hivemind_mountain_temple":
                return "MountainTemple high stone, shrine paths, steps, rocks, and temple props";
            case "hivemind_woodland_village":
                return "Woodland village timber, camp structures, earth paths, and natural settlement dressing";
            case "hivemind_witch_house":
                return "WitchHouse cottage, occult props, crooked interiors, garden clutter, and candles";
            case "hivemind_cave_tomb":
                return "CaveOfHiddenTomb cave walls, tomb chambers, stone props, and buried ruin dressing";
            case "hivemind_house_on_hill":
                return "HouseOnaHill isolated manor rooms, hilltop exterior props, and mystery dressing";
            case "hivemind_villa_forge":
                return "VillaForge workshop, villa stone, forge props, tools, and estate craft clutter";
            case "hivemind_horror_hospital":
                return "HorrorHospital medical rooms, hallways, beds, equipment, and abandoned horror props";
            case "hivemind_olympus_temple":
                return "OlympusTemple marble, divine columns, pines, rocks, and shrine props";
            case "hivemind_pirate_island":
                return "PirateIsland coastal props, docks, ropes, crates, and shore dressing";
            case "hivemind_hallowed_depths":
                return "HallowedDepths dungeon props and underground stone dressing";
            case "hivemind_sewers":
                return "TheSewers pipes, wet concrete, drains, and utility clutter";
            case "hivemind_mountain_messenger":
                return "TheMessenger mountain stone, cliffs, and ancient path dressing";
            default:
                return "NordicVillage + Yughues bushes + Tom terrain trees";
        }
    }

    private static string ResolveEncampmentPack(string style)
    {
        switch (style)
        {
            case "western_desert_town": return "WesternDesertTown cave/mining modules";
            case "ancient_desert_ruins": return "AncientDesertRuins ruin modules";
            case "bio_horror_scifi": return "BioHorrorSciFi biomass structure modules";
            case "scifi_engineers_room": return "SciFiEngineersRoom generator/server modules";
            case "hivemind_medieval_kingdom": return "MedievalKingdom gates, walls, carts, market stalls, castle and town modules";
            case "hivemind_military_camp": return "MilitaryCamp tents, barricades, crates, vehicles, watch points, and supply modules";
            case "hivemind_gothic_cathedral": return "GothicCathedral crypts, pews, arches, altars, broken stone, and sanctuary modules";
            case "hivemind_cyberpunk_city": return "CyberpunkCity alley barriers, holograms, shop fronts, bridges, cables, and urban cover";
            case "hivemind_gladiator_arena": return "GladiatorArena arena gates, columns, seating, weapon racks, and combat-site props";
            case "hivemind_rural_town": return "RuralTown fences, sheds, carts, barrels, garden props, and small camp modules";
            case "hivemind_modular_viking_village": return "ModularVikingVillage palisades, halls, shields, campfires, paths, and timber modules";
            case "hivemind_town_smith": return "TownSmith anvils, forges, shields, tool racks, crates, and workshop modules";
            case "hivemind_haunted_village": return "HauntedVillage ruined homes, fences, dead trees, grave accents, and cursed camp modules";
            case "hivemind_mystic_dungeon": return "MysticDungeon gates, rooms, ritual props, stone cover, and dungeon modules";
            case "hivemind_mountain_temple": return "MountainTemple stairs, rocks, shrine gates, cliff props, and temple encounter modules";
            case "hivemind_woodland_village": return "Woodland village camp structures, fences, earth paths, fires, and natural cover";
            case "hivemind_witch_house": return "WitchHouse cottage props, occult clutter, fences, candles, and swampy encounter modules";
            case "hivemind_cave_tomb": return "CaveOfHiddenTomb cave chambers, tomb doors, stone cover, and buried-site modules";
            case "hivemind_house_on_hill": return "HouseOnaHill manor props, fences, hilltop ruins, and haunted room modules";
            case "hivemind_villa_forge": return "VillaForge workshop props, tools, stone, forge heat, and craft-site modules";
            case "hivemind_horror_hospital": return "HorrorHospital beds, medical equipment, hall modules, doors, and abandoned clinic cover";
            case "hivemind_olympus_temple": return "OlympusTemple columns, marble platforms, shrine props, rocks, and divine encounter modules";
            case "hivemind_pirate_island": return "PirateIsland docks, crates, cages, barrels, wreckage, and coastal camp modules";
            case "hivemind_hallowed_depths": return "HallowedDepths modular dungeon rooms, gates, stairs, and ritual clutter";
            case "hivemind_sewers": return "TheSewers pipes, grates, tunnel modules, wet debris, and utility rooms";
            case "hivemind_mountain_messenger": return "TheMessenger mountain ruins, cliff paths, and ancient traversal modules";
            default: return ResolvePackName(style);
        }
    }

    private static string ResolveLayoutRuleProfile(string style)
    {
        switch (style)
        {
            case "victorian_mansion": return "interior_room_grid";
            case "container_district": return "industrial_lane_grid";
            case "bio_horror_scifi": return "corrupted_lab_grid";
            case "scifi_engineers_room": return "clean_scifi_room_grid";
            case "hivemind_cyberpunk_city": return "dense_cyberpunk_block_grid";
            case "hivemind_military_camp": return "fortified_camp_grid";
            case "hivemind_medieval_kingdom": return "castle_town_cells";
            case "hivemind_gothic_cathedral": return "cathedral_crypt_cells";
            case "hivemind_gladiator_arena": return "arena_ring_cells";
            case "hivemind_rural_town":
            case "hivemind_modular_viking_village":
            case "hivemind_town_smith":
            case "hivemind_woodland_village": return "organic_settlement_paths";
            case "hivemind_haunted_village":
            case "hivemind_witch_house":
            case "hivemind_house_on_hill": return "haunted_room_and_yard_cells";
            case "hivemind_horror_hospital": return "abandoned_clinic_room_grid";
            case "hivemind_mystic_dungeon":
            case "hivemind_mountain_temple":
            case "hivemind_cave_tomb":
            case "hivemind_olympus_temple": return "modular_dungeon_cells";
            case "hivemind_pirate_island": return "coastal_dock_settlement_grid";
            case "hivemind_hallowed_depths": return "modular_dungeon_cells";
            case "hivemind_sewers": return "wet_tunnel_grid";
            case "hivemind_mountain_messenger": return "mountain_ruin_path_cells";
            case "ancient_desert_ruins":
            case "persepolis_empire": return "broken_ruin_cells";
            default: return "organic_settlement_paths";
        }
    }

    private static string ResolveMood(string style)
    {
        switch (style)
        {
            case "victorian_mansion": return "warm archive, formal rooms, dense interior props";
            case "container_district": return "hard industrial panels, barriers, cables";
            case "bio_horror_scifi": return "corrupted sci-fi, biomass, bones, cold lights";
            case "scifi_engineers_room": return "clean sci-fi engineering room, servers, monitors, vents, calm lights";
            case "hivemind_medieval_kingdom": return "medieval kingdom, castle stone, market wood, banners, lived-in streets";
            case "hivemind_military_camp": return "fortified military camp, tents, barricades, crates, vehicles, patrol lanes";
            case "hivemind_gothic_cathedral": return "gothic cathedral, arches, crypts, candles, stone sanctum, solemn threat";
            case "hivemind_cyberpunk_city": return "cyberpunk city, neon alleys, cables, shop fronts, dense urban clutter";
            case "hivemind_gladiator_arena": return "gladiator arena, stone rings, columns, banners, combat spectacle";
            case "hivemind_rural_town": return "rural town, cottages, gardens, fences, tools, workaday settlement texture";
            case "hivemind_modular_viking_village": return "modular Viking village, timber halls, shields, thatch, campfire paths";
            case "hivemind_town_smith": return "smith town, forge heat, anvils, tools, soot, practical craft clutter";
            case "hivemind_haunted_village": return "haunted village, ruined homes, dead yards, crooked fences, quiet dread";
            case "hivemind_mystic_dungeon": return "mystic dungeon, ritual stone, mosaics, doors, magic chamber logic";
            case "hivemind_mountain_temple": return "mountain temple, high paths, shrine stone, cliff air, ceremonial ruin";
            case "hivemind_woodland_village": return "woodland village, timber camps, earth paths, natural cover, fires";
            case "hivemind_witch_house": return "witch house, occult cottage, candles, crooked interiors, nervous garden";
            case "hivemind_cave_tomb": return "hidden cave tomb, stone chambers, buried doors, damp darkness";
            case "hivemind_house_on_hill": return "house on a hill, isolated manor, suspicious rooms, exposed yard";
            case "hivemind_villa_forge": return "villa forge, craft estate, tools, stone, heat, refined workshop clutter";
            case "hivemind_horror_hospital": return "horror hospital, abandoned medical rooms, beds, equipment, fluorescent dread";
            case "hivemind_olympus_temple": return "Olympus temple, marble columns, pines, shrine platforms, divine ruin";
            case "hivemind_pirate_island": return "coastal pirate modular kit, docks, ropes, crates, shore structures";
            case "hivemind_hallowed_depths": return "modular dungeon, stone chambers, gates, stairs, ritual clutter";
            case "hivemind_sewers": return "wet tunnels, concrete, pipes, grates, utility lights";
            case "hivemind_mountain_messenger": return "ancient mountain paths, cliff ruins, stone traversal";
            case "ancient_desert_ruins": return "dry stone ruins, sand, rubble, partial cover";
            case "western_desert_town": return "dust roads, wood structures, mine/cave silhouettes";
            case "asian_dynasty": return "tile floors, pavilion roofs, market props, trees";
            case "persepolis_empire": return "imperial columns, murals, plinths, fire basins";
            case "viking_rural": return "wood huts, stables, farms, bridges";
            default: return "forest village, logs, dirt paths, thatch, bushes";
        }
    }

    private static string BuildRationale(GeneratedRegionRecord region, string style)
    {
        string name = region != null ? region.displayName : "region";
        return "Asset palette '" + style + "' assigned to " + name + " from generated biome/terrain/lore tags. Structural and deco slots stay inside the same style family to avoid incoherent procedural layouts.";
    }

    private static void AddTerrain(GeneratedRegionAssetPaletteRecord p, string path, params string[] tags)
    {
        Add(p.terrainMaterials, path, SlotTerrain, tags);
    }

    private static void Add(List<GeneratedAssetReferenceRecord> list, string path, string slot, params string[] tags)
    {
        if (list == null || string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(slot))
            return;

        GeneratedAssetReferenceRecord record = new GeneratedAssetReferenceRecord
        {
            assetKey = NormalizeKey(path),
            assetPath = path.Replace('\\', '/'),
            assetType = path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ? "material" : "prefab",
            slotTag = slot,
            weight = ResolveWeight(slot, tags),
            scaleMin = ResolveScaleMin(slot),
            scaleMax = ResolveScaleMax(slot),
            footprintX = ResolveFootprint(slot),
            footprintZ = ResolveFootprint(slot),
            placementRule = ResolvePlacementRule(slot),
            rotationRule = ResolveRotationRule(slot),
            allowRepeat = AllowsRepeat(slot),
            blocksNav = BlocksNav(slot),
            notes = "Generated semantic asset mapping."
        };
        record.EnsureCollections();
        AddUnique(record.styleTags, tags != null && tags.Length > 0 ? tags[0] : string.Empty);
        if (tags != null)
        {
            for (int i = 0; i < tags.Length; i++)
                AddUnique(record.subTags, NormalizeKey(tags[i]));
        }
        list.Add(record);
    }

    private static GeneratedAssetReferenceRecord CloneAsSlot(GeneratedAssetReferenceRecord source, string slot, string extraTag)
    {
        GeneratedAssetReferenceRecord clone = new GeneratedAssetReferenceRecord
        {
            assetKey = source != null ? source.assetKey : string.Empty,
            assetPath = source != null ? source.assetPath : string.Empty,
            assetType = source != null ? source.assetType : "prefab",
            slotTag = slot,
            weight = source != null ? source.weight : 1,
            scaleMin = source != null ? source.scaleMin : 1f,
            scaleMax = source != null ? source.scaleMax : 1f,
            footprintX = source != null ? source.footprintX : 1f,
            footprintZ = source != null ? source.footprintZ : 1f,
            placementRule = ResolvePlacementRule(slot),
            rotationRule = ResolveRotationRule(slot),
            allowRepeat = AllowsRepeat(slot),
            blocksNav = BlocksNav(slot),
            notes = "Derived from " + (source != null ? source.slotTag : "source") + " for " + slot + "."
        };
        clone.EnsureCollections();
        if (source != null)
        {
            CopyTags(source.subTags, clone.subTags);
            CopyTags(source.styleTags, clone.styleTags);
        }
        AddUnique(clone.subTags, extraTag);
        return clone;
    }

    private static string P(string root, string name)
    {
        return root + name + ".prefab";
    }

    private static string GroundMat(string name)
    {
        return Ground + name + "/" + name + ".mat";
    }

    private static int ResolveWeight(string slot, string[] tags)
    {
        if (slot == SlotTerrain || slot == SlotFloor || slot == SlotWall || slot == SlotPath)
            return 6;
        if (slot == SlotSettlementBuilding ||
    slot == SlotLargeStructure ||
    slot == SlotEnemySite)
        {
            return 3;
        }
        return 2;
    }

    private static float ResolveScaleMin(string slot)
    {
        switch (slot)
        {
            case SlotVegetation: return 0.85f;
            case SlotRock: return 0.75f;
            case SlotFloorDeco:
            case SlotExteriorDeco: return 0.8f;
            default: return 1f;
        }
    }

    private static float ResolveScaleMax(string slot)
    {
        switch (slot)
        {
            case SlotVegetation: return 1.35f;
            case SlotRock: return 1.45f;
            case SlotFloorDeco:
            case SlotExteriorDeco: return 1.15f;
            default: return 1f;
        }
    }

    private static float ResolveFootprint(string slot)
    {
        switch (slot)
        {
            case SlotSettlementBuilding: return 8f;
            case SlotLargeStructure:
            case SlotEnemySite: return 5f;
            case SlotFloor:
            case SlotPath: return 3f;
            case SlotWall:
            case SlotRoof: return 2f;
            case SlotVegetation:
            case SlotRock: return 1.5f;
            default: return 0.8f;
        }
    }

    private static string ResolvePlacementRule(string slot)
    {
        switch (slot)
        {
            case SlotTerrain: return "terrain_layer_only";
            case SlotFloor:
            case SlotPath: return "snap_to_ground_grid";
            case SlotWall: return "snap_to_floor_edge";
            case SlotRoof: return "snap_above_matching_wall";
            case SlotDoor: return "replace_one_wall_segment";
            case SlotWallDeco: return "attach_to_valid_wall";
            case SlotVegetation:
            case SlotRock:
            case SlotExteriorDeco: return "ground_scatter_outside_walk_path";
            case SlotLighting: return "wall_or_ground_anchor_near_path";
            case SlotLootContainer: return "ground_anchor_clear_interaction";
            default: return "ground_anchor_clear_nav";
        }
    }

    private static string ResolveRotationRule(string slot)
    {
        switch (slot)
        {
            case SlotWall:
            case SlotDoor:
            case SlotWallDeco: return "align_to_wall_normal";
            case SlotFloor:
            case SlotPath:
            case SlotRoof:
            case SlotSettlementBuilding:
                return "grid_90";
            default: return "random_yaw";
        }
    }

    private static bool AllowsRepeat(string slot)
    {
        return slot == SlotTerrain || slot == SlotFloor || slot == SlotWall || slot == SlotPath || slot == SlotVegetation || slot == SlotRock;
    }

    private static bool BlocksNav(string slot)
    {
        return
    slot == SlotWall ||
    slot == SlotDoor ||
    slot == SlotSettlementBuilding ||
    slot == SlotLargeStructure ||
    slot == SlotRock ||
    slot == SlotEnemySite ||
    slot == SlotLootContainer;
    }

    private static void UpsertPalette(List<GeneratedRegionAssetPaletteRecord> palettes, GeneratedRegionAssetPaletteRecord palette)
    {
        if (palettes == null || palette == null)
            return;

        for (int i = 0; i < palettes.Count; i++)
        {
            GeneratedRegionAssetPaletteRecord existing = palettes[i];
            if (existing != null && string.Equals(existing.regionId, palette.regionId, StringComparison.OrdinalIgnoreCase))
            {
                palettes[i] = palette;
                return;
            }
        }
        palettes.Add(palette);
    }

    private static void RemoveNullPalettes(List<GeneratedRegionAssetPaletteRecord> palettes)
    {
        if (palettes == null)
            return;
        for (int i = palettes.Count - 1; i >= 0; i--)
        {
            if (palettes[i] == null)
                palettes.RemoveAt(i);
        }
    }

    private static bool ContainsAny(
    string text,
    params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            needles == null)
        {
            return false;
        }

        string haystack =
            " " + NormalizeSearchText(text) + " ";

        for (int i = 0; i < needles.Length; i++)
        {
            string needle =
                NormalizeSearchText(needles[i]);

            if (string.IsNullOrWhiteSpace(needle))
                continue;

            if (haystack.IndexOf(
                    " " + needle + " ",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeSearchText(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars =
            value
                .Trim()
                .ToLowerInvariant()
                .ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = ' ';
        }

        string normalized =
            new string(chars);

        string[] parts =
            normalized.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

        return string.Join(" ", parts);
    }

    private static void CopyTags(List<string> source, List<string> target)
    {
        if (source == null || target == null)
            return;
        for (int i = 0; i < source.Count; i++)
            AddUnique(target, source[i]);
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (list == null || string.IsNullOrWhiteSpace(value))
            return;
        string clean = value.Trim();
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], clean, StringComparison.OrdinalIgnoreCase))
                return;
        }
        list.Add(clean);
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string lower = value.Trim().ToLowerInvariant();
        char[] chars = lower.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (!char.IsLetterOrDigit(c))
                chars[i] = '_';
        }
        return new string(chars).Trim('_');
    }

    private static string StableHex(string text)
    {
        return PositiveHash(text).ToString("x8");
    }

    private static int PositiveHash(string text)
    {
        unchecked
        {
            int hash = 23;
            string value = text ?? string.Empty;
            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];
            return hash & 0x7fffffff;
        }
    }
}
