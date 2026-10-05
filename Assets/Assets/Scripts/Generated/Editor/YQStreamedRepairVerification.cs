#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class YQStreamedRepairVerification
{
    public static void RunCombinedFromCommandLine()
    {
        // note: Reuse the existing focused stream contracts before the new save/gear fixtures; either failure keeps the process unsuccessful.
        int failures = (int)typeof(YQSemanticWorldAuthorityTests).GetMethod("RunConstructionContractSuites", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        if (failures != 0) { EditorApplication.Exit(1); return; }
        RunFromCommandLine();
    }

    public static void RunFromCommandLine()
    {
        // note: Read a detached copy of the current save; these checks never publish terrain, profiles or generated content.
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Idle batch Editor required.");
        var checks = new List<object>();
        int failures = 0;
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failures++; }
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        try
        {
            var settings = new JsonSerializerSettings { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
            string root = Application.persistentDataPath;
            string[] commandLine = Environment.GetCommandLineArgs();
            int snapshotArgument = Array.IndexOf(commandLine, "-yqRepairSnapshotFolder");
            if (snapshotArgument >= 0 && snapshotArgument + 1 < commandLine.Length) root = Path.GetFullPath(commandLine[snapshotArgument + 1]);
            var world = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(Path.Combine(root, "world_state.json")), settings);
            var player = JsonConvert.DeserializeObject<PlayerState>(File.ReadAllText(Path.Combine(root, "player_state.json")), settings);
            var plan = world.generatedWorldPlan;
            var contextHash = typeof(YQWorldGenerationService).GetMethod("BuildFrontierSemanticContextHash", flags);
            var factions = new List<string>();
            foreach (var faction in plan.factions) if (faction != null) factions.Add(faction.factionId);
            var region = plan.regions[0];
            object[] hashArgs = { plan, region.regionId, factions };
            string hash = (string)contextHash.Invoke(null, hashArgs);
            world.stateRevision++;
            Check("walking revision does not supersede unchanged site canon", hash == (string)contextHash.Invoke(null, hashArgs));
            string style = region.assetStyleKey;
            region.assetStyleKey += "_changed";
            Check("changed canonical region supersedes site inference", hash != (string)contextHash.Invoke(null, hashArgs));
            region.assetStyleKey = style;
            Check("restored canon has stable fingerprint", hash == (string)contextHash.Invoke(null, hashArgs));

            var batchType = typeof(YQGeneratedNpcPlanningService).GetNestedType("PopulationBatchTarget", BindingFlags.NonPublic);
            var batches = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(batchType));
            typeof(YQGeneratedNpcPlanningService).GetMethod("BuildBatchTargets", flags).Invoke(null, new object[] { plan, batches, 64, 64 });
            Check("all accepted base locations receive population batches", batches.Count == plan.settlements.Count + plan.encampments.Count);
            object target = batches[0];
            var expectedCount = typeof(YQGeneratedNpcPlanningService).GetMethod("GetExpectedNpcCountForTarget", flags);
            int fullCount = (int)expectedCount.Invoke(null, new[] { (object)plan, target });
            batchType.GetField("retainedNpcCount").SetValue(target, 1);
            Check("accepted residents reduce missing slots without being replaced", (int)expectedCount.Invoke(null, new[] { (object)plan, target }) == Mathf.Max(0, fullCount - 1));
            batchType.GetField("retainedNpcCount").SetValue(target, fullCount);
            Check("complete accepted population is never requested again", (int)expectedCount.Invoke(null, new[] { (object)plan, target }) == 0);

            var item = new InventoryItemRecord { itemId = "repair-equipment-fixture", templateId = "repair-fixture", itemType = "weapon", equipSlot = "weapon", quantity = 1 };
            var record = new YQContainerRecord { entityId = "repair-npc-fixture", profileId = player.playerId, ownerId = "repair-npc-fixture",
                access = YQContainerAccess.OwnerOnly, restock = YQContainerRestock.Never,
                context = new YQContainerContext { entityId = "repair-npc-fixture", sourceType = YQContainerType.Npc } };
            record.contents.Add(item); record.equippedItemBySlot["weapon"] = item.itemId;
            Check("NPC equipment uses one validated canonical owned item", YQContainerInventory.ValidateOwnership(world, player, record, player.inventoryItems, out _));
            Check("NPC equipment cannot be opened by the player", !YQContainerInventory.CanAccess(record, player, out _));
            var copy = YQContainerInventory.Copy(record);
            Check("save copy preserves NPC gear identity and slot reference", copy.contents.Count == 1 && copy.equippedItemBySlot["weapon"] == copy.contents[0].itemId);
            record.access = YQContainerAccess.Public;
            Check("misconfigured public NPC inventory rejects", !YQContainerInventory.ValidateOwnership(world, player, record, player.inventoryItems, out _));

            var equipmentFixture = new GameObject("Detached equipment generator fixture");
            try
            {
                var content = equipmentFixture.AddComponent<GeneratedRpgContentService>();
                content.library = Resources.Load<GeneratedRpgContentLibrary>("GeneratedRpgContentLibrary");
                int compatible = 0;
                for (int index = 0; index < 8; index++)
                {
                    var generated = content.GenerateContainerItem("repair-actor-gear-" + index, record.context,
                        new YQLootEntry { id = "weapon", itemKind = "weapon" }, YQLootRarity.Common);
                    if (generated.IsEquippable && generated.equipSlot == "weapon" && YQContainerLoot.CompatibleAsset(content, generated, null) &&
                        YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(generated.prefabKey) != null) compatible++;
                }
                Check("approved relocated equipment produces real weapons for eight actor seeds", compatible == 8);
            }
            finally { UnityEngine.Object.DestroyImmediate(equipmentFixture); }
            var familyVocabulary = (string[])typeof(YQWorldGenerationService).GetMethod("BuildFrontierMonsterFamilyVocabulary", flags).Invoke(null, null);
            Check("fresh hostile families are all bound to actual approved creatures", familyVocabulary.Length > 0 && Array.TrueForAll(familyVocabulary,
                family => YQRuntimeCreatureAssetIndex.TryResolveMonster(YQRuntimeWorldAssetRegistry.Instance, family, "repair", "repair", out _, out _)));

            if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string error)) throw new InvalidOperationException(error);
            var placement = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("TryBuildFrontierPhysicalPlacement", flags);
            var buildCandidate = typeof(YQSemanticWorldAuthority).GetMethod("TryBuildFrontierLocationCandidate", flags);
            var samples = new List<object>();
            int viable = 0, opportunities = 0;
            // note: Sample unvisited positions around the opening and the user's latest travel point, using the exact production admission path.
            Vector2[] centres = { new Vector2(0f, 0f), new Vector2(player.lastPosition.x, player.lastPosition.z) };
            foreach (Vector2 centre in centres)
            {
                int bx = Mathf.FloorToInt(centre.x / 512f), bz = Mathf.FloorToInt(centre.y / 512f);
                for (int ring = 3; ring <= 8; ring++)
                foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                {
                    int x = bx + direction.x * ring, z = bz + direction.y * ring;
                    object[] candidateArgs = { plan, x, z, region.regionId, null, null };
                    if (!(bool)buildCandidate.Invoke(null, candidateArgs)) continue;
                    var candidate = (GeneratedSpatialContinuationLocationV2Record)candidateArgs[4];
                    opportunities++;
                    object[] args = { plan, prepared, candidate, (Func<bool>)(() => true), null, null, null };
                    bool valid = (bool)placement.Invoke(null, args);
                    if (valid) viable++;
                    samples.Add(new { x, z, kind = candidate.anchor.kind.ToString(), valid, failure = args[6], anchor = valid ? ((GeneratedSpatialContinuationLocationV2Record)args[4]).anchor : candidate.anchor });
                }
            }
            Check("current saved world has valid frontier placements or accepted streamed sites", viable > 0 || (plan.spatialPlanV2.acceptedContinuation?.locations.Count ?? 0) > 0);
            Directory.CreateDirectory("outputs/StreamedRepair_20261005");
            File.WriteAllText("outputs/StreamedRepair_20261005/DetachedPlacement.json", JsonConvert.SerializeObject(new { seed = plan.worldSeed, opportunities, viable, samples }, Formatting.Indented, settings));
        }
        catch (Exception exception) { Debug.LogException(exception); Check(exception.GetBaseException().Message, false); }
        Directory.CreateDirectory("outputs/StreamedRepair_20261005");
        File.WriteAllText("outputs/StreamedRepair_20261005/RepairContracts.json", JsonConvert.SerializeObject(new { failures, checks }, Formatting.Indented));
        Debug.Log("[YQStreamedRepairVerification] failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
#endif
