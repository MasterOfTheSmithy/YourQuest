using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class YQWorldGenerationAssetKitPaletteTests
{
    private static readonly string[] RepresentativeStyles =
    {
        "nordic_forest",
        "ancient_desert_ruins",
        "western_desert_town",
        "asian_dynasty",
        "persepolis_empire",
        "viking_rural",
        "victorian_mansion",
        "container_district",
        "bio_horror_scifi",
        "scifi_engineers_room",
        "hivemind_medieval_kingdom",
        "hivemind_cyberpunk_city",
        "hivemind_gladiator_arena",
        "hivemind_rural_town",
        "hivemind_modular_viking_village",
        "hivemind_town_smith",
        "hivemind_haunted_village",
        "hivemind_mystic_dungeon",
        "hivemind_mountain_temple",
        "hivemind_woodland_village",
        "hivemind_witch_house",
        "hivemind_cave_tomb",
        "hivemind_house_on_hill",
        "hivemind_villa_forge",
        "hivemind_horror_hospital",
        "hivemind_olympus_temple",
        "hivemind_military_camp",
        "hivemind_sewers",
        "hivemind_pirate_island",
        "hivemind_hallowed_depths",
        "hivemind_mountain_messenger"
    };

    [MenuItem("YourQuest/World Generation/Run Asset Kit Palette Contract")]
    public static void RunFromMenu()
    {
        int failures = RunTests(out int tested, out string report);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/YQWorldGenerationAssetKitPaletteReport.md", report);
        Debug.Log("[YQWorldGenAssetKit] Tested " + tested + " representative palettes; failures=" + failures + ".");
    }

    public static void RunBatch()
    {
        int failures = RunTests(out int tested, out string report);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/YQWorldGenerationAssetKitPaletteReport.md", report);
        Debug.Log("[YQWorldGenAssetKit] Tested " + tested + " representative palettes; failures=" + failures + ".");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    [MenuItem("YourQuest/World Generation/Run Exhaustive Generation-Ready Asset Sweep")]
    public static void RunExhaustiveGenerationReadySweep()
    {
        YQWorldAssetIntakeCatalog intake = Resources.Load<YQWorldAssetIntakeCatalog>("YQWorldAssetIntakeCatalog");
        YQRuntimeWorldAssetRegistry registry = YQRuntimeWorldAssetRegistry.Instance;
        int declared = 0;
        int shardBound = 0;
        int resolved = 0;
        int instantiated = 0;
        List<string> failures = new List<string>();
        Dictionary<string, YQRuntimeWorldAssetRegistry> shards = new Dictionary<string, YQRuntimeWorldAssetRegistry>(StringComparer.OrdinalIgnoreCase);
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (intake == null || intake.SpatialAssets == null || registry == null)
        {
            failures.Add("canonical intake catalog or runtime registry unavailable");
        }
        else
        {
            // note: Sweep every canonical generation-ready record with the same technical and filename gates used by inventory publication.
            for (int index = 0; index < intake.SpatialAssets.Count; index++)
            {
                YQSpatialAssetRecord record = intake.SpatialAssets[index];
                if (!IsGenerationReadyRecord(record))
                    continue;
                declared++;
                string path = (record.assetPath ?? string.Empty).Replace('\\', '/');
                string shardResourcePath = YQRuntimeWorldAssetRegistry.BuildShardResourcePath(path);
                if (!shards.TryGetValue(shardResourcePath, out YQRuntimeWorldAssetRegistry shard))
                {
                    shard = Resources.Load<YQRuntimeWorldAssetRegistry>(shardResourcePath);
                    shards[shardResourcePath] = shard;
                }

                YQRuntimeWorldAssetEntry entry = FindShardEntry(shard, path);
                if (entry == null || entry.prefab == null)
                {
                    AddSweepFailure(failures, path + " — missing prefab in " + shardResourcePath);
                    continue;
                }
                shardBound++;
                GameObject resolvedPrefab = registry.ResolvePrefab(path);
                if (resolvedPrefab == null)
                {
                    AddSweepFailure(failures, path + " — runtime registry resolve returned null");
                    continue;
                }
                resolved++;

                GameObject instance = null;
                try
                {
                    instance = PrefabUtility.InstantiatePrefab(resolvedPrefab) as GameObject;
                    if (instance == null)
                    {
                        AddSweepFailure(failures, path + " — instantiate returned null");
                        continue;
                    }
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (record.hasRenderer && renderers.Length == 0)
                        AddSweepFailure(failures, path + " — intake expects a renderer but none was instantiated");
                    bool valid = true;
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Renderer renderer = renderers[rendererIndex];
                        if (renderer == null || !IsFinite(renderer.bounds.extents) || renderer.bounds.extents.sqrMagnitude <= 0.000001f)
                        {
                            AddSweepFailure(failures, path + " — invalid renderer bounds");
                            valid = false;
                            break;
                        }
                        Material[] materials = renderer.sharedMaterials;
                        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                        {
                            if (materials[materialIndex] == null)
                            {
                                AddSweepFailure(failures, path + " — missing material slot " + materialIndex);
                                valid = false;
                                break;
                            }
                        }
                        if (!valid)
                            break;
                    }
                    if (valid)
                        instantiated++;
                }
                catch (Exception exception)
                {
                    AddSweepFailure(failures, path + " — instantiate exception: " + exception.Message);
                }
                finally
                {
                    if (instance != null)
                        UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        stopwatch.Stop();
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        report.AppendLine("# YourQuest Generation-Ready Asset Sweep");
        report.AppendLine();
        report.AppendLine("- declared generation-ready: " + declared);
        report.AppendLine("- shard-bound prefab: " + shardBound);
        report.AppendLine("- runtime registry resolved: " + resolved);
        report.AppendLine("- instantiated with valid bounds/materials: " + instantiated);
        report.AppendLine("- failures: " + failures.Count);
        report.AppendLine("- elapsed seconds: " + stopwatch.Elapsed.TotalSeconds.ToString("0.000"));
        for (int index = 0; index < failures.Count && index < 200; index++)
            report.AppendLine("- " + failures[index]);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/YQGenerationReadyAssetSweep.md", report.ToString());
        Debug.Log("[YQWorldGenAssetKit] Exhaustive generation-ready sweep declared=" + declared + " shardBound=" + shardBound + " resolved=" + resolved + " instantiated=" + instantiated + " failures=" + failures.Count + ".");
    }

    private static bool IsGenerationReadyRecord(YQSpatialAssetRecord record)
    {
        return YQRuntimeWorldAssetRegistryBuilder.IsGenerationReadySpatialAsset(record);
    }

    private static YQRuntimeWorldAssetEntry FindShardEntry(YQRuntimeWorldAssetRegistry shard, string path)
    {
        if (shard == null || shard.Entries == null)
            return null;
        for (int index = 0; index < shard.Entries.Count; index++)
        {
            YQRuntimeWorldAssetEntry entry = shard.Entries[index];
            if (entry != null && string.Equals((entry.assetPath ?? string.Empty).Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase))
                return entry;
        }
        return null;
    }

    private static void AddSweepFailure(List<string> failures, string message)
    {
        failures.Add(message);
    }

    private static int RunTests(out int tested, out string report)
    {
        tested = 0;
        int failures = 0;
        System.Text.StringBuilder output = new System.Text.StringBuilder();
        output.AppendLine("# YourQuest Asset Kit Palette Contract");
        output.AppendLine();

        for (int index = 0; index < RepresentativeStyles.Length; index++)
        {
            string style = RepresentativeStyles[index];
            GeneratedWorldPlanRecord plan = new GeneratedWorldPlanRecord
            {
                worldSeed = "asset_kit_palette_" + style,
                regions = new List<GeneratedRegionRecord>
                {
                    new GeneratedRegionRecord
                    {
                        regionId = "asset_kit_region_" + index,
                        displayName = style,
                        assetStyleKey = style,
                        assetStyleRationale = "Asset kit palette contract coverage."
                    }
                }
            };

            YQWorldAssetCatalog.EnsureAssetPalettes(plan);
            GeneratedRegionAssetPaletteRecord palette = plan.assetPalettes.Count > 0 ? plan.assetPalettes[0] : null;
            string failure = ValidatePalette(style, palette);
            if (string.IsNullOrWhiteSpace(failure))
                failure = ValidateRuntimeSelectionGate(palette, style);
            if (string.IsNullOrWhiteSpace(failure))
                failure = ValidateRuntimeInstantiation(palette, style);
            if (!string.IsNullOrWhiteSpace(failure))
                failures++;

            tested++;
            output.Append("- ").Append(style).Append(": ");
            output.Append(string.IsNullOrWhiteSpace(failure) ? "PASS" : "FAIL — " + failure);
            if (palette != null)
            {
                // note: A palette can pass style isolation but lack construction coverage; report that shortfall for later spatial review.
                output.Append(" (floor=").Append(palette.floor?.Count ?? 0)
                    .Append(", wall=").Append(palette.wall?.Count ?? 0)
                    .Append(", roof=").Append(palette.roof?.Count ?? 0)
                    .Append(", door=").Append(palette.door?.Count ?? 0)
                    .Append(", path=").Append(palette.path?.Count ?? 0)
                    .Append(", buildings=").Append(palette.settlementBuilding?.Count ?? 0)
                    .Append(")");
            }
            output.AppendLine();
        }

        report = output.ToString();
        return failures;
    }

    private static string ValidatePalette(string style, GeneratedRegionAssetPaletteRecord palette)
    {
        if (palette == null)
            return "palette was not created";

        if (!string.Equals(palette.styleKey, style, StringComparison.OrdinalIgnoreCase))
            return "palette style key changed during construction";

        if (palette.styleTags == null || palette.styleTags.Count == 0)
            return "palette has no style tags";

        if (ContainsForstConifer(palette.vegetation) &&
            !ContainsColdWoodlandStyle(style))
            return "cold/woodland conifer leaked into a warm or interior style";

        if (HasInvalidReference(palette))
            return "palette contains a blank asset reference or slot tag";

        // note: Prefab paths in construction and local dressing slots must belong to the region's owning pack; shared nature/loot are checked separately.
        string foreignPath = FindForeignPackAsset(palette);
        if (!string.IsNullOrEmpty(foreignPath))
            return "unreviewed cross-pack asset " + foreignPath;

        // note: Style isolation is insufficient if a settlement/interior palette loses every way to construct its local environment.
        if ((palette.floor?.Count ?? 0) == 0 &&
            (palette.wall?.Count ?? 0) == 0 &&
            (palette.path?.Count ?? 0) == 0 &&
            (palette.settlementBuilding?.Count ?? 0) == 0 &&
            !(string.Equals(style, "hivemind_mountain_messenger", StringComparison.OrdinalIgnoreCase) &&
              (palette.enemySite?.Count ?? 0) > 0))
            return "no local construction candidate";

        return string.Empty;
    }

    private static string ValidateRuntimeSelectionGate(
        GeneratedRegionAssetPaletteRecord palette,
        string style)
    {
        string[] slots =
        {
            YQWorldAssetCatalog.SlotFloor,
            YQWorldAssetCatalog.SlotWall,
            YQWorldAssetCatalog.SlotRoof,
            YQWorldAssetCatalog.SlotDoor,
            YQWorldAssetCatalog.SlotPath,
            YQWorldAssetCatalog.SlotSettlementBuilding,
            YQWorldAssetCatalog.SlotLargeStructure,
            YQWorldAssetCatalog.SlotFloorDeco,
            YQWorldAssetCatalog.SlotWallDeco,
            YQWorldAssetCatalog.SlotVegetation,
            YQWorldAssetCatalog.SlotRock,
            YQWorldAssetCatalog.SlotLighting,
            YQWorldAssetCatalog.SlotLootContainer,
            YQWorldAssetCatalog.SlotEnemySite,
            YQWorldAssetCatalog.SlotInteriorDeco,
            YQWorldAssetCatalog.SlotExteriorDeco
        };

        // note: Probe multiple deterministic rolls so a rejected discovered prefab cannot enter through a weighted-list edge case.
        for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
        {
            for (int seedIndex = 0; seedIndex < 32; seedIndex++)
            {
                GeneratedAssetReferenceRecord selected =
                    YQWorldAssetCatalog.PickAssetForSlot(
                        palette,
                        slots[slotIndex],
                        style + ":gate:" + seedIndex);
                if (selected != null &&
                    !YQWorldAssetCatalog.IsSpatiallyApprovedForRuntime(selected.assetPath))
                    return "runtime picker returned an unreviewed spatial asset in " + slots[slotIndex];
            }
        }

        return string.Empty;
    }

    private static string ValidateRuntimeInstantiation(
        GeneratedRegionAssetPaletteRecord palette,
        string style)
    {
        GameObject probeRoot = new GameObject("__YQ_PALETTE_RUNTIME_PROBE__");
        probeRoot.hideFlags = HideFlags.HideAndDontSave;
        string[] slots =
        {
            YQWorldAssetCatalog.SlotFloor, YQWorldAssetCatalog.SlotWall,
            YQWorldAssetCatalog.SlotRoof, YQWorldAssetCatalog.SlotDoor,
            YQWorldAssetCatalog.SlotPath, YQWorldAssetCatalog.SlotSettlementBuilding,
            YQWorldAssetCatalog.SlotLargeStructure, YQWorldAssetCatalog.SlotFloorDeco,
            YQWorldAssetCatalog.SlotWallDeco, YQWorldAssetCatalog.SlotVegetation,
            YQWorldAssetCatalog.SlotRock, YQWorldAssetCatalog.SlotLighting,
            YQWorldAssetCatalog.SlotLootContainer, YQWorldAssetCatalog.SlotEnemySite,
            YQWorldAssetCatalog.SlotInteriorDeco, YQWorldAssetCatalog.SlotExteriorDeco
        };

        try
        {
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                GeneratedAssetReferenceRecord selected = YQWorldAssetCatalog.PickAssetForSlot(
                    palette, slots[slotIndex], style + ":runtime:" + slotIndex);
                if (selected == null)
                    continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(selected.assetPath);
                if (prefab == null)
                    return "runtime prefab missing for " + slots[slotIndex] + ": " + selected.assetPath;
                GameObject instance = null;
                try
                {
                    instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (instance == null)
                        return "runtime instantiation returned null for " + selected.assetPath;
                    instance.transform.SetParent(probeRoot.transform, false);
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Renderer renderer = renderers[rendererIndex];
                        if (renderer == null || !IsFinite(renderer.bounds.extents) || renderer.bounds.extents.sqrMagnitude <= 0.000001f)
                            return "invalid renderer bounds for " + selected.assetPath;
                        Material[] materials = renderer.sharedMaterials;
                        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                            if (materials[materialIndex] == null)
                                return "missing material on " + selected.assetPath;
                    }
                }
                finally
                {
                    if (instance != null)
                        UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            return string.Empty;
        }
        catch (Exception exception)
        {
            return "runtime instantiation exception: " + exception.Message;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probeRoot);
        }
    }

    private static string FindForeignPackAsset(GeneratedRegionAssetPaletteRecord palette)
    {
        string owningPrefix = "Assets/" + (palette.architecturePack ?? string.Empty).Trim('/') + "/";
        List<List<GeneratedAssetReferenceRecord>> localSlots = new List<List<GeneratedAssetReferenceRecord>>
        {
            palette.floor, palette.wall, palette.roof, palette.door, palette.path,
            palette.settlementBuilding, palette.largeStructure, palette.floorDeco,
            palette.wallDeco, palette.vegetation, palette.rock, palette.lighting,
            palette.lootContainer, palette.enemySite, palette.interiorDeco, palette.exteriorDeco
        };
        string[] slotNames =
        {
            "floor", "wall", "roof", "door", "path", "settlement_building", "large_structure",
            "floor_deco", "wall_deco", "vegetation", "rock", "lighting", "loot_container",
            "enemy_site", "interior_deco", "exterior_deco"
        };

        for (int slotIndex = 0; slotIndex < localSlots.Count; slotIndex++)
        {
            List<GeneratedAssetReferenceRecord> records = localSlots[slotIndex];
            if (records == null)
                continue;

            for (int index = 0; index < records.Count; index++)
            {
                string path = (records[index]?.assetPath ?? string.Empty).Replace('\\', '/');
                if (!path.StartsWith(owningPrefix, StringComparison.OrdinalIgnoreCase) &&
                    !IsAllowedSharedReference(path, slotNames[slotIndex], palette.styleKey))
                    return slotNames[slotIndex] + ": " + path;
            }
        }

        return string.Empty;
    }

    // note: Explicit finite checks keep malformed imported bounds from entering the runtime palette probe without relying on newer Unity helper APIs.
    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static bool IsAllowedSharedReference(string path, string slot, string style)
    {
        // note: The contract verifies actual slot placement as well as source family, including vegetation and cave-site fallbacks.
        if (slot == "vegetation")
        {
            if (path.StartsWith("Assets/Tom's Terrain Tools/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Assets/YughuesFreeBushes2018/", StringComparison.OrdinalIgnoreCase))
                return true;
            return path.StartsWith("Assets/Forst/Conifers [BOTD]/", StringComparison.OrdinalIgnoreCase) &&
                   ContainsColdWoodlandStyle(style);
        }

        if (slot == "rock" && path.StartsWith("Assets/Tom's Terrain Tools/", StringComparison.OrdinalIgnoreCase))
            return true;
        if (slot == "loot_container" &&
            path.StartsWith("Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/", StringComparison.OrdinalIgnoreCase))
            return true;
        return slot == "settlement_building" && style == "hivemind_rural_town" &&
               path.StartsWith("Assets/HIVEMIND/TownSmith/HDRP(Default)/Art/Prefabs/Drag&Drops/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsForstConifer(List<GeneratedAssetReferenceRecord> records)
    {
        if (records == null)
            return false;

        for (int index = 0; index < records.Count; index++)
        {
            string path = records[index]?.assetPath ?? string.Empty;
            if (path.IndexOf("Assets/Forst/Conifers [BOTD]/", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool ContainsColdWoodlandStyle(string style)
    {
        string normalized = (style ?? string.Empty).ToLowerInvariant();
        return normalized.Contains("nordic") || normalized.Contains("viking") ||
               normalized.Contains("mountain") || normalized.Contains("hallowed") ||
               normalized.Contains("woodland");
    }

    private static bool HasInvalidReference(GeneratedRegionAssetPaletteRecord palette)
    {
        List<List<GeneratedAssetReferenceRecord>> slots = new List<List<GeneratedAssetReferenceRecord>>
        {
            palette.terrainMaterials, palette.floor, palette.wall, palette.roof, palette.door,
            palette.path, palette.settlementBuilding, palette.largeStructure, palette.floorDeco,
            palette.wallDeco, palette.vegetation, palette.rock, palette.lighting, palette.lootContainer,
            palette.enemySite, palette.interiorDeco, palette.exteriorDeco
        };

        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            List<GeneratedAssetReferenceRecord> records = slots[slotIndex];
            if (records == null)
                continue;

            for (int index = 0; index < records.Count; index++)
            {
                GeneratedAssetReferenceRecord record = records[index];
                if (record == null || string.IsNullOrWhiteSpace(record.assetPath) || string.IsNullOrWhiteSpace(record.slotTag))
                    return true;
            }
        }

        return false;
    }
}
// note: Automatic refresh verification touch for exhaustive runtime palette probes.
