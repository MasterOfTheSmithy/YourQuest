using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// note: Run the compiled production data/transaction code with disposable documents; no Unity session, mocked inventory owner or production saves.
public static class HeadlessContracts
{
    public static int Main(string[] args)
    {
        var results = new List<object>(); int failed = 0;
        Test("profile rules and rarity eligibility", () =>
        {
            var catalog = JsonConvert.DeserializeObject<YQLootProfileCatalog>(File.ReadAllText(Path.Combine(args[0], "Assets/Assets/Resources/YQContainerLootProfiles.json")));
            Need(YQContainerLoot.ValidateCatalog(catalog, out string failure), failure);
            Need(string.Join(",", Enum.GetNames(typeof(YQLootRarity))) == "Common,Uncommon,Rare,Epic,Unique,Legendary,Mythical", "Rarity order.");
            var context = new YQContainerContext { sourceType = YQContainerType.Wardrobe, sourceLevel = 12 };
            Need(YQContainerLoot.ResolveProfile(context, catalog).id == "household_clothing_v1", "Wardrobe profile.");
            context.sourceType = YQContainerType.Chest; context.tags.Add("encounter");
            Need(YQContainerLoot.ResolveProfile(context, catalog).id == "dungeon_reward_v1", "Dungeon profile.");
            context.tags.Add("loot:empty"); Need(YQContainerLoot.ResolveProfile(context, catalog).entries.Count == 0, "Empty profile.");
            catalog.profiles[0].rarityWeights[4] = 1;
            Need(!YQContainerLoot.ValidateCatalog(catalog, out _), "Unique random weight accepted.");
        });
        Test("canonical seed, low/high region and missing context", () =>
        {
            Setup(out WorldState world, out PlayerState player, out YQContainerRecord record);
            world.generatedWorldPlan.regions.Add(new GeneratedRegionRecord { regionId = "high", dangerTier = 12, biomeTags = new List<string> { "ice" } });
            YQContainerContext high = YQContainerLoot.ResolveContext(world, "high-source", YQContainerType.Chest, "high");
            Need(high.sourceLevel == 12 && high.element == "ice", "Region level/element.");
            YQContainerContext missing = YQContainerLoot.ResolveContext(world, "unknown-source", YQContainerType.Chest, "missing");
            Need(missing.sourceLevel == 1 && missing.fallbacks.Count > 0, "Missing fallback.");
            string seed = YQContainerInventory.Seed(record.context, 1);
            player.level = 99;
            Need(seed == YQContainerInventory.Seed(record.context, 1) && seed != YQContainerInventory.Seed(record.context, 2), "Stable seed.");
            record.context.entityId += "other";
            Need(seed != YQContainerInventory.Seed(record.context, 1), "Identity seed.");
        });
        Test("partial stack withdrawal and player deposit conservation", () =>
        {
            Setup(out WorldState world, out PlayerState player, out YQContainerRecord record);
            Need(Move(world, player, record, false, "stack", 3, () => true), "Withdrawal.");
            var held = player.inventoryItems[0]; record = world.containers[record.entityId];
            Need(held.quantity == 3 && record.contents[0].quantity == 5 && held.itemId != "stack", "Split identity/quantity.");
            Need(Move(world, player, record, true, held.itemId, 3, () => true), "Deposit.");
            Need(world.containers[record.entityId].contents[0].quantity == 8 && player.inventoryItems.Count == 0 && world.containers[record.entityId].hasPlayerDeposits, "Conservation.");
        });
        Test("strict stack compatibility and 99 stack limit", () =>
        {
            Setup(out WorldState world, out PlayerState player, out YQContainerRecord record);
            var held = YQContainerInventory.Copy(record.contents[0]); held.itemId = "held"; held.quantity = 99; player.inventoryItems.Add(held);
            var changed = YQContainerInventory.Copy(held); changed.healAmount = 1;
            Need(!YQContainerInventory.CanStack(held, changed), "Different stats merged.");
            player.inventoryCapacity = 1;
            Need(!Move(world, player, record, false, "stack", 1, () => true), "Full stack/capacity overflow.");
            Need(held.quantity == 99 && record.contents[0].quantity == 8, "Rejected mutation.");
        });
        Test("capacity, invalid quantities and stale transaction", () =>
        {
            Setup(out WorldState world, out PlayerState player, out YQContainerRecord record);
            Need(!Move(world, player, record, false, "stack", 9, () => true), "Underflow.");
            Need(!Move(world, player, record, false, "stack", 0, () => true), "Zero quantity.");
            Need(!YQContainerInventory.TryTransfer(world, player, record.entityId, false, "stack", 1, 9, player.stateRevision, () => true, out _), "Stale revision.");
        });
        Test("failure rollback covers items, currency, ledger, counters and revisions", () =>
        {
            Setup(out WorldState world, out PlayerState player, out YQContainerRecord record);
            string before = YQContainerInventory.Serialize(player), beforeWorld = YQContainerInventory.Serialize(world);
            Need(!Move(world, player, record, false, "stack", 1, () => false), "Failed publication succeeded.");
            Need(before == YQContainerInventory.Serialize(player) && beforeWorld == YQContainerInventory.Serialize(world), "Item rollback.");
            Need(!YQContainerInventory.TryTakeCurrency(world, player, record.entityId, record.revision, () => throw new IOException("injected"), out _), "Thrown publication succeeded.");
            Need(before == YQContainerInventory.Serialize(player) && beforeWorld == YQContainerInventory.Serialize(world), "Currency rollback.");
        });
        Test("access, lock and equipped item rules", () =>
        {
            Setup(out WorldState world, out PlayerState player, out YQContainerRecord record);
            record.locked = true; Need(!YQContainerInventory.CanAccess(record, player, out _), "Locked access."); record.locked = false;
            record.access = YQContainerAccess.OwnerOnly; record.ownerId = "other"; Need(!YQContainerInventory.CanAccess(record, player, out _), "Ownership.");
            record.access = YQContainerAccess.CorpseOnly; Need(!YQContainerInventory.CanAccess(record, player, out _), "Living hostile.");
            record.dead = true; Need(YQContainerInventory.CanAccess(record, player, out _), "Corpse access.");
            record.access = YQContainerAccess.Public;
            var equipped = Item("equipped", 1); equipped.stackable = false; equipped.itemType = "weapon"; equipped.equipSlot = "weapon";
            player.inventoryItems.Add(equipped); player.equippedItemBySlot["weapon"] = equipped.itemId;
            Need(!Move(world, player, record, true, equipped.itemId, 1, () => true), "Equipped deposit.");
        });
        Test("single equipped corpse item and no repeat loot", () =>
        {
            Setup(out WorldState world, out PlayerState player, out YQContainerRecord record);
            record.access = YQContainerAccess.CorpseOnly; record.context.sourceType = YQContainerType.Hostile; record.dead = true;
            var weapon = Item("weapon", 1); weapon.stackable = false; weapon.itemType = "weapon"; weapon.equipSlot = "weapon";
            record.contents.Clear(); record.contents.Add(weapon); record.equippedItemBySlot["weapon"] = weapon.itemId;
            Need(Move(world, player, record, false, weapon.itemId, 1, () => true), "Weapon transfer."); record = world.containers[record.entityId];
            Need(record.contents.Count == 0 && record.equippedItemBySlot.Count == 0 && player.inventoryItems[0].itemId == weapon.itemId, "Linked equipment conservation.");
            Need(!Move(world, player, record, false, weapon.itemId, 1, () => true), "Repeated loot.");
        });
        Test("cross-store identity validation", () =>
        {
            Setup(out WorldState world, out PlayerState player, out YQContainerRecord record);
            player.inventoryItems.Add(YQContainerInventory.Copy(record.contents[0]));
            Need(!YQContainerInventory.ValidateOwnership(world, player, null, player.inventoryItems, out _), "Duplicate item ownership.");
        });
        Test("paired disk commit, checksums, reload and streamed-view binding", () =>
        {
            Setup(out WorldState world, out PlayerState player, out YQContainerRecord record);
            string folder = Path.Combine(Path.GetTempPath(), "YourQuestContainerManagedFixture-" + Guid.NewGuid().ToString("N"));
            YQProfileTransactionReceipt receipt = null;
            bool Publish() => YQProfileCommitStore.TryCommit(folder, player.playerId, Path.Combine(folder, "active-player.json"), Path.Combine(folder, "active-world.json"), 1,
                YQContainerInventory.Serialize(player), YQContainerInventory.Serialize(world), null, out receipt);
            Need(Move(world, player, record, false, "stack", 2, Publish), "Paired disk commit.");
            Need(YQProfileCommitStore.TryReadCommit(folder, receipt.commitId, receipt.playerChecksum, receipt.worldChecksum, out string pp, out string wp, out string failure), failure);
            var settings = new JsonSerializerSettings { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
            PlayerState loadedPlayer = JsonConvert.DeserializeObject<PlayerState>(File.ReadAllText(pp), settings);
            WorldState loadedWorld = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(wp), settings);
            IYQInventory streamed = new YQContainerInventoryView(loadedWorld, record.entityId);
            Need(streamed.Contents[0].quantity == 6 && loadedPlayer.inventoryItems[0].quantity == 2 && loadedWorld.containers[record.entityId].generated, "Reload regenerated removed contents.");
        });
        Test("interrupted disk publication retains accepted paired revision", () =>
        {
            // note: Exercise the existing store's real failure points, not only a callback returning false before disk work.
            foreach (YQProfileCommitPoint point in new[] { YQProfileCommitPoint.PlayerProjectionPublished,
                YQProfileCommitPoint.WorldProjectionPublished, YQProfileCommitPoint.BeforePointerPublication })
            {
                Setup(out WorldState world, out PlayerState player, out YQContainerRecord record);
                string folder = Path.Combine(Path.GetTempPath(), "YourQuestContainerInterrupted-" + Guid.NewGuid().ToString("N"));
                string pp = Path.Combine(folder, "active-player.json"), wp = Path.Combine(folder, "active-world.json");
                Need(YQProfileCommitStore.TryCommit(folder, player.playerId, pp, wp, 1,
                    YQContainerInventory.Serialize(player), YQContainerInventory.Serialize(world), null, out YQProfileTransactionReceipt accepted), "Initial commit.");
                var manifest = new YQProfileSaveSystem.ProfileManifest();
                manifest.profiles.Add(new YQProfileSaveSystem.ProfileEntry { profileId = player.playerId });
                manifest = manifest.PrepareCommit(accepted);
                string before = JsonConvert.SerializeObject(manifest);
                bool Publish() => YQProfileCommitStore.TryCommit(folder, player.playerId, pp, wp, 2,
                    YQContainerInventory.Serialize(player), YQContainerInventory.Serialize(world),
                    reached => { if (reached == point) throw new IOException("Deliberate interrupted publication"); }, out _);
                Need(!Move(world, player, record, false, "stack", 2, Publish), "Interrupted transfer accepted.");
                Need(JsonConvert.SerializeObject(manifest) == before && player.inventoryItems.Count == 0 && world.containers[record.entityId].contents[0].quantity == 8, "Rejected state escaped rollback.");
                Need(YQProfileCommitStore.TryReadCommit(folder, manifest.activeCommitId, accepted.playerChecksum, accepted.worldChecksum,
                    out string acceptedPlayer, out string acceptedWorld, out string error), error);
                var loaded = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(acceptedWorld),
                    new JsonSerializerSettings { Converters = { new Vector3JsonConverter() } });
                Need(loaded.containers[record.entityId].contents[0].quantity == 8, "Unpublished projection became authoritative.");
            }
        });
        Test("commit pointer preparation preserves accepted manifest", () =>
        {
            var accepted = new YQProfileSaveSystem.ProfileManifest { activeProfileId = "fixture", activeCommitId = "old", activeRevision = 1 };
            accepted.profiles.Add(new YQProfileSaveSystem.ProfileEntry { profileId = "fixture", updatedUnix = 10 });
            string before = JsonConvert.SerializeObject(accepted);
            var receipt = new YQProfileTransactionReceipt { profileId = "fixture", commitId = "new", previousCommitId = "old", revision = 2, committedUnix = 20 };
            var candidate = accepted.PrepareCommit(receipt);
            Need(JsonConvert.SerializeObject(accepted) == before, "Preparing an unpublished pointer mutated accepted authority.");
            Need(candidate.activeCommitId == "new" && candidate.activeRevision == 2 && candidate.profiles[0].updatedUnix == 20 && candidate.commits.Count == 1,
                "Candidate lost paired revision metadata.");
        });
        Test("ordered schema migration preserves accepted canon", () =>
        {
            string json = "{\"schemaVersion\":7,\"canonLedger\":\"accepted\",\"generatedWorldPlan\":{\"worldSeed\":\"76603739\"}}";
            Need(YQStateMigrations.TryNormalizeWorldDocument(json, out string migrated, out _, out string failure), failure);
            JObject value = JObject.Parse(migrated);
            Need(value["schemaVersion"].Value<int>() == 8 && value["containers"] is JObject && value["canonLedger"].Value<string>() == "accepted", "Migration.");
            Need(value["generatedWorldPlan"]["worldSeed"].Value<string>() == "76603739", "World seed changed.");
        });
        File.WriteAllText(Path.Combine(args[0], "Docs/Container_Inventory_Managed_Receipt_2026-10-02.json"), JsonConvert.SerializeObject(new
        { generatedAtUtc = DateTime.UtcNow.ToString("O"), evidence = "MANAGED compiled production contracts; no Unity scene/asset/runtime execution", status = failed == 0 ? "PASS" : "FAIL", results }, Formatting.Indented));
        return failed == 0 ? 0 : 1;

        void Test(string name, Action test)
        {
            try { test(); results.Add(new { check = name, status = "PASS", detail = "Compiled production contract." }); Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; results.Add(new { check = name, status = "FAIL", detail = error.ToString() }); Console.WriteLine("FAIL " + name + ": " + error.Message); }
        }
    }

    private static void Setup(out WorldState world, out PlayerState player, out YQContainerRecord record)
    {
        player = new PlayerState { playerId = "managed-container-fixture", currentRegionId = "region_unknown" }; player.EnsureCollections();
        world = new WorldState(); world.EnsureCollections(); world.generatedWorldPlan.worldSeed = "76603739";
        record = new YQContainerRecord { entityId = "fixture", displayName = "Fixture", profileId = player.playerId, generated = true, currency = 5,
            context = new YQContainerContext { entityId = "fixture", worldSeed = "76603739" } };
        record.contents.Add(Item("stack", 8)); world.containers[record.entityId] = record;
    }
    private static InventoryItemRecord Item(string id, int quantity) => new InventoryItemRecord
    { itemId = id, templateId = "fixture-stack", displayName = "Fixture Supply", itemType = "supply", rarity = "Common", stackable = true, quantity = quantity };
    private static bool Move(WorldState world, PlayerState player, YQContainerRecord record, bool deposit, string id, int quantity, Func<bool> publish) =>
        YQContainerInventory.TryTransfer(world, player, record.entityId, deposit, id, quantity, world.containers[record.entityId].revision, player.stateRevision, publish, out _);
    private static void Need(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }
}
