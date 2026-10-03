#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class YQContainerInventoryTests
{
    private const string Request = "Tools/ContainerVerification/Run.request";
    private const string Receipt = "Docs/Container_Inventory_Contract_Receipt_2026-10-02.json";
    [Serializable] private sealed class Result { public string check, status, detail; }

    static YQContainerInventoryTests()
    {
        // note: A named, explicit one-shot request runs detached contracts after source import; no perpetual polling or production saves.
        if (File.Exists(Request)) EditorApplication.delayCall += RunRequested;
    }

    private static void RunRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += RunRequested; return; }
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        Run();
    }

    [MenuItem("YourQuest/Verification/Run Container Inventory Contracts")]
    public static void Run()
    {
        var results = new List<Result>();
        GameObject host = new GameObject("Disposable Container Contract Fixture"); host.SetActive(false);
        GeneratedRpgContentService content = host.AddComponent<GeneratedRpgContentService>();
        content.library = Resources.Load<GeneratedRpgContentLibrary>("GeneratedRpgContentLibrary");
        try
        {
            Check("profile configuration and rarity order", () =>
            {
                Require(YQContainerLoot.ValidateCatalog(YQContainerLoot.Catalog, out string error), error);
                Require(string.Join(",", Enum.GetNames(typeof(YQLootRarity))) == "Common,Uncommon,Rare,Epic,Unique,Legendary,Mythical", "Rarity order changed.");
            });
            Check("wardrobe, cupboard, barrel, crate and cache semantics", () =>
            {
                foreach (YQContainerType type in new[] { YQContainerType.Wardrobe, YQContainerType.Cupboard, YQContainerType.Barrel, YQContainerType.Crate, YQContainerType.Cache })
                {
                    for (int i = 0; i < 12; i++)
                    {
                        Fixture(out WorldState world, out PlayerState player);
                        YQContainerRecord record = Source(world, player, type, "source-" + i);
                        Generate(record, world, player);
                        foreach (InventoryItemRecord item in record.contents)
                        {
                            if (type == YQContainerType.Wardrobe) Require(item.equipSlot == "legs" || item.equipSlot == "boots", "Non-clothing wardrobe outcome.");
                            if (type == YQContainerType.Cupboard || type == YQContainerType.Barrel || type == YQContainerType.Crate)
                                Require(item.IsConsumable || item.itemType == "supply", "Equipment in household provisions.");
                            Require(item.rarity != "Unique" && item.rarity != "Mythical", "Unrestricted authored rarity.");
                        }
                    }
                }
            });
            Check("low/high region level and reward profile", () =>
            {
                Fixture(out WorldState world, out PlayerState player);
                world.generatedWorldPlan.regions.Add(new GeneratedRegionRecord { regionId = "fixture_low", dangerTier = 1, biomeTags = new List<string> { "forest" } });
                world.generatedWorldPlan.regions.Add(new GeneratedRegionRecord { regionId = "fixture_high", dangerTier = 12, biomeTags = new List<string> { "ice" } });
                var low = Source(world, player, YQContainerType.Chest, "low", "fixture_low");
                var high = Source(world, player, YQContainerType.Chest, "high", "fixture_high"); high.context.tags.Add("encounter");
                Generate(low, world, player); Generate(high, world, player);
                Require(low.context.sourceLevel == 1 && high.context.sourceLevel == 12 && high.context.element == "ice", "Context resolution ignored region.");
                Require(high.lootProfileId == "dungeon_reward_v1" && low.lootProfileId == "regional_chest_v1", "Wrong danger profile.");
                Require(high.contents.Exists(i => i.equipSlot == "weapon" && i.attackBonus >= 12), "High-level mechanical reward missing.");
            });
            Check("seed and items deterministic independently of player", () =>
            {
                Fixture(out WorldState world, out PlayerState player);
                var one = Source(world, player, YQContainerType.Chest, "deterministic");
                var two = YQContainerInventory.Copy(one);
                Generate(one, world, player);
                player.level = 99; player.displayName = "Different player";
                Generate(two, world, player);
                Require(YQContainerInventory.Serialize(one.contents) == YQContainerInventory.Serialize(two.contents) && one.currency == two.currency && one.seed == two.seed, "Loot depends on player/open time.");
                two.context.entityId = "other";
                Require(YQContainerInventory.Seed(two.context, 1) != one.seed && YQContainerInventory.Seed(one.context, 2) != one.seed, "Entity/version not in seed.");
            });
            Check("transfer, partial stacks and strict compatibility", () =>
            {
                Fixture(out WorldState world, out PlayerState player);
                var record = Source(world, player, YQContainerType.Crate, "stack"); record.generated = true;
                record.contents.Add(Item("stack", 8)); world.containers[record.entityId] = record;
                Require(Transfer(world, player, record.entityId, false, "stack", 3, () => true), "Split transfer failed.");
                Require(player.inventoryItems.Single().quantity == 3 && world.containers[record.entityId].contents.Single().quantity == 5, "Quantity not conserved.");
                Require(player.inventoryItems[0].itemId != "stack", "Partial stack shares identity.");
                var altered = YQContainerInventory.Copy(player.inventoryItems[0]); altered.healAmount++;
                Require(!YQContainerInventory.CanStack(player.inventoryItems[0], altered), "Different mechanics merged.");
                Require(Transfer(world, player, record.entityId, true, player.inventoryItems[0].itemId, 3, () => true), "Deposit failed.");
                Require(world.containers[record.entityId].contents.Single().quantity == 8 && player.inventoryItems.Count == 0, "Merge not conserved.");
            });
            Check("capacity, stack limit, invalid quantity and stale revision", () =>
            {
                Fixture(out WorldState world, out PlayerState player);
                var record = Source(world, player, YQContainerType.Crate, "capacity"); record.generated = true;
                record.contents.Add(Item("source", 3)); world.containers[record.entityId] = record;
                player.inventoryCapacity = 1; player.inventoryItems.Add(Item("occupied", 99));
                Require(!Transfer(world, player, record.entityId, false, "source", 1, () => true), "Capacity exceeded.");
                Require(!Transfer(world, player, record.entityId, false, "source", 0, () => true), "Zero transfer accepted.");
                Require(!Transfer(world, player, record.entityId, false, "source", 4, () => true), "Source underflow accepted.");
                Require(!YQContainerInventory.TryTransfer(world, player, record.entityId, false, "source", 1, -1, player.stateRevision, () => true, out _), "Stale transfer accepted.");
                Require(record.contents[0].quantity == 3 && player.inventoryItems[0].quantity == 99, "Rejected transaction mutated state.");
            });
            Check("failed publication rolls back items, gold, counters and revisions", () =>
            {
                Fixture(out WorldState world, out PlayerState player);
                var record = Source(world, player, YQContainerType.Chest, "rollback"); record.generated = true; record.currency = 8;
                record.contents.Add(Item("rollback-item", 2)); world.containers[record.entityId] = record;
                string before = YQContainerInventory.Serialize(player), beforeWorld = YQContainerInventory.Serialize(world);
                Require(!Transfer(world, player, record.entityId, false, "rollback-item", 1, () => false), "Failed save reported success.");
                Require(before == YQContainerInventory.Serialize(player) && beforeWorld == YQContainerInventory.Serialize(world), "Failed item save did not roll back.");
                Require(!YQContainerInventory.TryTakeCurrency(world, player, record.entityId, record.revision, () => throw new IOException("fixture failure"), out _), "Exception save reported success.");
                Require(before == YQContainerInventory.Serialize(player) && beforeWorld == YQContainerInventory.Serialize(world), "Failed gold save did not roll back.");
            });
            Check("ownership, locks, living hostile and equipped player gates", () =>
            {
                Fixture(out WorldState world, out PlayerState player);
                var record = Source(world, player, YQContainerType.Chest, "access"); record.generated = true; record.locked = true;
                Require(!YQContainerInventory.CanAccess(record, player, out _), "Locked source opened."); record.locked = false;
                record.access = YQContainerAccess.OwnerOnly; record.ownerId = "another";
                Require(!YQContainerInventory.CanAccess(record, player, out _), "Owner-only source opened.");
                record.ownerId = player.playerId; Require(YQContainerInventory.CanAccess(record, player, out _), "Owner refused.");
                record.access = YQContainerAccess.CorpseOnly; Require(!YQContainerInventory.CanAccess(record, player, out _), "Living hostile opened.");
                record.context.sourceType = YQContainerType.Hostile; record.access = YQContainerAccess.Public;
                Require(!YQContainerInventory.CanAccess(record, player, out _), "Misconfigured living hostile opened.");
                record.context.sourceType = YQContainerType.Chest; record.access = YQContainerAccess.CorpseOnly;
                record.dead = true; Require(YQContainerInventory.CanAccess(record, player, out _), "Corpse refused.");
                record.access = YQContainerAccess.Public; world.containers[record.entityId] = record;
                InventoryItemRecord weapon = Item("equipped", 1); weapon.stackable = false; weapon.itemType = "weapon"; weapon.equipSlot = "weapon";
                player.inventoryItems.Add(weapon); player.equippedItemBySlot["weapon"] = weapon.itemId;
                Require(!Transfer(world, player, record.entityId, true, weapon.itemId, 1, () => true), "Equipped player item deposited.");
            });
            Check("hostile equipment references become single corpse loot", () =>
            {
                Fixture(out WorldState world, out PlayerState player);
                var record = Source(world, player, YQContainerType.Hostile, "bandit"); record.context.tags.Add("bandit");
                var entry = new YQLootEntry { id = "accepted-weapon", itemKind = "weapon" };
                InventoryItemRecord weapon = content.GenerateContainerItem("accepted-equipped", record.context, entry, YQLootRarity.Common);
                record.contents.Add(weapon); record.equippedItemBySlot["weapon"] = weapon.itemId;
                Generate(record, world, player); record.dead = true; world.containers[record.entityId] = record;
                Require(record.contents.Count(i => i.equipSlot == "weapon") == 1 && record.lootProfileId == "bandit_hostile_v1", "Equipped weapon duplicated by loot generation.");
                Require(Transfer(world, player, record.entityId, false, weapon.itemId, 1, () => true), "Weapon loot failed.");
                Require(player.inventoryItems.Count(i => i.itemId == weapon.itemId) == 1 && world.containers[record.entityId].equippedItemBySlot.Count == 0, "Linked equipment not removed.");
                Require(!Transfer(world, player, record.entityId, false, weapon.itemId, 1, () => true), "Same corpse weapon looted twice.");
                Require(!Transfer(world, player, record.entityId, true, weapon.itemId, 1, () => true), "Corpse accepted a deposit.");
            });
            Check("paired commit, reload, reopen and streamed view rebind", () =>
            {
                Fixture(out WorldState world, out PlayerState player);
                var record = Source(world, player, YQContainerType.Crate, "persist"); record.generated = true;
                record.contents.Add(Item("persisted-stack", 7)); world.containers[record.entityId] = record;
                string folder = Path.Combine(Path.GetTempPath(), "YourQuestContainerFixture-" + Guid.NewGuid().ToString("N"));
                YQProfileTransactionReceipt receipt = null;
                bool Publish() => YQProfileCommitStore.TryCommit(folder, player.playerId, Path.Combine(folder, "active-player.json"), Path.Combine(folder, "active-world.json"), 1,
                    YQContainerInventory.Serialize(player), YQContainerInventory.Serialize(world), null, out receipt);
                Require(Transfer(world, player, record.entityId, false, "persisted-stack", 2, Publish), "Paired fixture commit failed.");
                Require(YQProfileCommitStore.TryReadCommit(folder, receipt.commitId, receipt.playerChecksum, receipt.worldChecksum, out string playerPath, out string worldPath, out string failure), failure);
                PlayerState loadedPlayer = YQContainerInventory.Copy(JsonConvert.DeserializeObject<PlayerState>(File.ReadAllText(playerPath)));
                WorldState loadedWorld = YQContainerInventory.Copy(JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(worldPath), new JsonSerializerSettings { Converters = { new Vector3JsonConverter() } }));
                Require(loadedPlayer.inventoryItems.Single().quantity == 2 && loadedWorld.containers[record.entityId].contents.Single().quantity == 5, "Reload restored removed items.");
                IYQInventory firstView = new YQContainerInventoryView(loadedWorld, record.entityId), streamedView = new YQContainerInventoryView(loadedWorld, record.entityId);
                Require(firstView.Contents.Single().quantity == streamedView.Contents.Single().quantity && firstView.Revision == streamedView.Revision, "Rebinding rerolled state.");
                Require(!YQContainerLoot.Generate(loadedWorld.containers[record.entityId], content, loadedWorld, loadedPlayer, YQContainerLoot.Catalog, out _), "Reopening regenerated contents.");
                // note: Keep this disposable fixture directory for receipt inspection; no production save path is used.
            });
            Check("explicit restock, replay protection and deposited contents", () =>
            {
                Fixture(out WorldState world, out PlayerState player);
                var record = Source(world, player, YQContainerType.Barrel, "restock"); record.generated = true; record.restock = YQContainerRestock.ExplicitWhenEmpty;
                world.containers[record.entityId] = record;
                Require(YQContainerLoot.TryRestock(world, player, record.entityId, "supply-delivery-1", content, () => true, out string failure), failure);
                var restocked = world.containers[record.entityId]; Require(restocked.contents.Count > 0 && restocked.restockCycle == 1, "Explicit delivery did not refill.");
                restocked.contents.Clear(); restocked.currency = 0;
                Require(!YQContainerLoot.TryRestock(world, player, record.entityId, "supply-delivery-1", content, () => true, out _), "Restock event replayed.");
                restocked.hasPlayerDeposits = true;
                Require(!YQContainerLoot.TryRestock(world, player, record.entityId, "supply-delivery-2", content, () => true, out _), "Player deposits overwritten by restock.");
                restocked.restock = YQContainerRestock.Never; restocked.hasPlayerDeposits = false;
                Require(!YQContainerLoot.TryRestock(world, player, record.entityId, "supply-delivery-3", content, () => true, out _), "Never-restock source refilled.");
            });
            Check("missing metadata, empty loot and invalid modular combination", () =>
            {
                Fixture(out WorldState world, out PlayerState player);
                var record = Source(world, player, YQContainerType.Chest, "unknown");
                Require(record.context.sourceLevel == 1 && record.context.fallbacks.Count > 0, "Missing metadata has no fallback.");
                record.context.tags.Add("loot:empty"); Generate(record, world, player);
                Require(record.generated && record.contents.Count == 0 && record.currency == 0, "Empty loot not preserved.");
                InventoryItemRecord bad = content.GenerateContainerItem("invalid", record.context,
                    new YQLootEntry { id = "invalid", itemKind = "weapon", assetSemantic = "clothing_legs" }, YQLootRarity.Rare);
                Require(bad != null && bad.itemType == "supply" && !bad.IsEquippable && bad.rarity == "Common", "Invalid modular combination had no safe fallback.");
            });
            Check("schema 7 migration retains consumed rewards and accepted state", () =>
            {
                string json = "{\"schemaVersion\":7,\"generatedWorldPlan\":{\"worldSeed\":\"76603739\"},\"canonLedger\":\"accepted\"}";
                Require(YQStateMigrations.TryNormalizeWorldDocument(json, out string migrated, out _, out string failure), failure);
                var parsed = Newtonsoft.Json.Linq.JObject.Parse(migrated);
                Require(parsed["schemaVersion"].Value<int>() == 8 && parsed["containers"] is Newtonsoft.Json.Linq.JObject && parsed["canonLedger"].Value<string>() == "accepted" &&
                    parsed["generatedWorldPlan"]["worldSeed"].Value<string>() == "76603739", "Migration changed accepted canon.");
                Fixture(out WorldState world, out PlayerState player);
                var source = Source(world, player, YQContainerType.Chest, "spent"); player.IncCounter("loot:opened:spent", 1);
                Require(YQContainerLoot.TryImportLegacyOpened(source, player) && source.generated && source.legacyConsumed && source.contents.Count == 0 && source.currency == 0, "Spent receipt rerolled.");
                Require(!YQContainerLoot.Generate(source, content, world, player, YQContainerLoot.Catalog, out _), "Migrated spent storage regenerated.");
            });
            Check("authored Unique/Mythical eligibility and durable claims", () =>
            {
                foreach (YQLootRarity rarity in new[] { YQLootRarity.Unique, YQLootRarity.Mythical })
                {
                    Fixture(out WorldState world, out PlayerState player);
                    var source = Source(world, player, YQContainerType.Chest, "artifact-" + rarity); source.context.rarityBudget = 100;
                    var profile = new YQLootProfile { id = "authored-fixture", sourceTypes = new[] { YQContainerType.Chest }, minRolls = 0, maxRolls = 0 };
                    profile.entries.Add(new YQLootEntry { id = "artifact", itemKind = "weapon", guaranteed = true, authoredRarity = rarity,
                        authoredPolicyId = "fixture-" + rarity, requiredTags = new[] { "reward:authored" } });
                    var catalog = new YQLootProfileCatalog(); catalog.profiles.Add(profile);
                    Require(YQContainerLoot.ValidateCatalog(catalog, out string failure), failure);
                    Require(YQContainerLoot.Generate(source, content, world, player, catalog, out failure) && source.contents.Count == 0, "Ineligible authored artifact generated.");
                    source.generated = false; source.context.tags.Add("reward:authored");
                    Require(YQContainerLoot.Generate(source, content, world, player, catalog, out failure) && source.contents.Count == 1 && source.contents[0].rarity == rarity.ToString(), "Eligible authored slot missing.");
                    source.contents.Clear(); world.containers[source.entityId] = source;
                    var other = Source(world, player, YQContainerType.Chest, "other-" + rarity); other.context.rarityBudget = 100; other.context.tags.Add("reward:authored");
                    Require(YQContainerLoot.Generate(other, content, world, player, catalog, out failure) && other.contents.Count == 0, "Consumed authored artifact duplicated.");
                }
            });
            Check("typed NPC/site context and physical corpse view rebind", () =>
            {
                Fixture(out WorldState world, out PlayerState player);
                world.generatedWorldPlan.generatedNpcs.Add(new GeneratedNpcPlanRecord { npcId = "site-bandit", factionId = "fixture_bandits", role = "bandit_leader", encampmentId = "camp" });
                world.generatedWorldPlan.encampments.Add(new GeneratedEncampmentRecord { encampmentId = "camp", threatTier = 9, monsterFamily = "bandit" });
                var context = YQContainerLoot.ResolveContext(world, "site-bandit", YQContainerType.Hostile, "region_unknown");
                Require(context.siteId == "camp" && context.tags.Contains("bandit") && context.encounterDifficulty == 9 && context.factionId == "fixture_bandits", "Accepted site/NPC IDs not resolved.");
                GameObject parent = new GameObject("Disposable corpse streaming fixture"); parent.SetActive(false);
                try
                {
                    var source = Source(world, player, YQContainerType.Hostile, "corpse-view-fixture"); source.dead = source.generated = true; source.contents.Add(Item("corpse-stack", 1));
                    YQInvestorLootableCorpse.EnsurePersistedView(source, Vector3.zero, parent.transform);
                    YQInvestorLootableCorpse.EnsurePersistedView(source, Vector3.zero, parent.transform);
                    Require(parent.GetComponentsInChildren<YQInvestorLootableCorpse>(true).Length == 1, "Repeated stream rebind duplicated corpse view.");
                    UnityEngine.Object.DestroyImmediate(parent.transform.GetChild(0).gameObject);
                    YQInvestorLootableCorpse.EnsurePersistedView(source, Vector3.zero, parent.transform);
                    Require(parent.GetComponentInChildren<YQInvestorLootableCorpse>(true).EntityId == source.entityId && source.contents[0].quantity == 1, "Restored corpse changed canonical contents/ID.");
                }
                finally { UnityEngine.Object.DestroyImmediate(parent); }
            });
            Check("console inspection, regeneration gate and paired snapshot restoration", () =>
            {
                Require(!YQDeveloperTestSession.Active, "An existing test session must finish before this detached fixture.");
                Fixture(out WorldState world, out PlayerState player);
                var source = Source(world, player, YQContainerType.Crate, "explicit-test-container");
                source.generated = true; source.seed = YQContainerInventory.Seed(source.context, 1); source.generationVersion = 1;
                source.contents.Add(Item("snapshot-item", 4)); world.containers[source.entityId] = source;
                PlayerStateManager manager = host.AddComponent<PlayerStateManager>(); manager.state = player;
                WorldStateManager worldManager = host.AddComponent<WorldStateManager>(); worldManager.ReplaceState(world);
                var registry = new YQDeveloperCommandRegistry(manager, worldManager, null, null);
                Require(!registry.Execute("container inspect").Success, "Inspection implicitly selected a source.");
                Require(registry.Execute("container select explicit-test-container").Success, "Explicit selection failed.");
                YQDeveloperCommandResult inspection = registry.Execute("container inspect");
                Require(inspection.Success && inspection.Text.Contains(source.seed) && inspection.Text.Contains("snapshot-item"), "Inspector omitted seed/contents.");
                Require(!registry.Execute("container regenerate").Success && source.contents[0].quantity == 4, "Warning changed contents.");
                Require(!registry.Execute("container regenerate --replace").Success && !YQDeveloperTestSession.Active, "Replacement implicitly started a snapshot.");
                try
                {
                    Require(registry.Execute("test snapshot").Success, "Supported snapshot failed.");
                    Require(Transfer(world, player, source.entityId, false, "snapshot-item", 2, YQWorldContainer.PublishLive), "Session transfer failed.");
                    Require(registry.Execute("test restore").Success, "Supported restore failed.");
                    Require(player.inventoryItems.Count == 0 && world.containers[source.entityId].contents[0].quantity == 4,
                        "Snapshot failed to restore both owners, duplicating or losing items.");
                }
                finally { if (YQDeveloperTestSession.Active) YQDeveloperTestSession.Restore(out _); }
            });
            Check("reviewed storage identity/collider regression", () =>
            {
                Require(string.IsNullOrEmpty(YQCellLootBindingTestsV2.TestBinding()), "Existing reviewed binding regressed.");
                Require(string.IsNullOrEmpty(YQCellLootBindingTestsV2.TestRejections()), "Existing storage rejection regressed.");
            });
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
        var report = new
        {
            generatedAtUtc = DateTime.UtcNow.ToString("O"), evidence = "EDITOR DETACHED CONTRACT FIXTURES; NOT production PlaySafe flow",
            status = results.All(r => r.status == "PASS") ? "PASS" : "FAIL", results,
            notVerified = new[] { "ordinary visible PlaySafe interactions", "full production streaming/unload/reload", "production hostile animation/body equipment binding", "multiplayer/reconnect authority (not present)" }
        };
        File.WriteAllText(Receipt, JsonConvert.SerializeObject(report, Formatting.Indented));
        Debug.Log("[YQContainerInventoryTests] " + report.status + " " + results.Count(r => r.status == "PASS") + "/" + results.Count + "; " + Receipt);

        void Check(string name, Action test)
        {
            try { test(); results.Add(new Result { check = name, status = "PASS", detail = "Detached real-owner contract fixture." }); }
            catch (Exception exception) { results.Add(new Result { check = name, status = "FAIL", detail = exception.ToString() }); Debug.LogError("[YQContainerInventoryTests] " + name + ": " + exception.Message); }
        }
        void Generate(YQContainerRecord source, WorldState world, PlayerState player) => Require(YQContainerLoot.Generate(source, content, world, player, YQContainerLoot.Catalog, out string failure), failure);
    }

    private static void Fixture(out WorldState world, out PlayerState player)
    {
        player = new PlayerState { playerId = "container-fixture", currentRegionId = "region_unknown" }; player.EnsureCollections();
        world = new WorldState(); world.EnsureCollections(); world.generatedWorldPlan.worldSeed = "76603739";
    }

    private static YQContainerRecord Source(WorldState world, PlayerState player, YQContainerType type, string id, string region = "region_unknown") => new YQContainerRecord
    {
        entityId = id, profileId = player.playerId, displayName = "Fixture " + type,
        context = YQContainerLoot.ResolveContext(world, id, type, region),
        access = type == YQContainerType.Hostile ? YQContainerAccess.CorpseOnly : YQContainerAccess.Public
    };

    private static InventoryItemRecord Item(string id, int count) => new InventoryItemRecord
    { itemId = id, templateId = "fixture-stack", itemType = "supply", displayName = "Fixture Supplies", rarity = "Common", quantity = count, stackable = true };

    private static bool Transfer(WorldState world, PlayerState player, string id, bool deposit, string item, int quantity, Func<bool> publish) =>
        YQContainerInventory.TryTransfer(world, player, id, deposit, item, quantity, world.containers[id].revision, player.stateRevision, publish, out _);
    private static void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }
}
#endif
