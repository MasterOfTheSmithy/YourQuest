using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

// note: Exercise actual compiled inventory transactions using disposable documents, never a live profile or Unity session.
public static class StackTransferContracts
{
    public static int Main(string[] args)
    {
        var results = new List<object>();
        int failed = 0;
        Test("withdraw into fragmented full backpack", () =>
        {
            var f = new Fixture(false, 8, 2, 95, 95);
            Need(f.Transfer(8), f.Message);
            f.Expect(0, 99, 99);
            Need(f.Published == 1 && f.Player.currency == 17, "Publication or currency changed.");
        });
        Test("deposit into fragmented full container", () =>
        {
            var f = new Fixture(true, 8, 2, 95, 95);
            Need(f.Transfer(8), f.Message);
            f.Expect(0, 99, 99);
            Need(f.Record.hasPlayerDeposits, "Deposit protection missing.");
        });
        Test("remainder uses one free slot and retains full source identity", () =>
        {
            var f = new Fixture(false, 12, 3, 95, 95);
            Need(f.Transfer(12), f.Message);
            f.Expect(0, 99, 99, 4);
            Need(f.Target[2].itemId == "source", "Full transfer lost source identity.");
        });
        Test("partial source remainder receives deterministic split identity", () =>
        {
            var f = new Fixture(true, 20, 3, 95, 95);
            string expected = "split:" + YQContainerInventory.StableKey("source|fixture|stack-profile|0");
            Need(f.Transfer(12), f.Message);
            f.Expect(8, 99, 99, 4);
            Need(f.Target[2].itemId == expected && f.Source[0].itemId == "source", "Split identity changed.");
        });
        Test("existing whole-stack merge preference is preserved", () =>
        {
            var f = new Fixture(false, 8, 2, 95, 80);
            Need(f.Transfer(8), f.Message);
            f.Expect(0, 95, 88);
        });
        Test("insufficient total capacity rejects without publication or mutation", () =>
        {
            foreach (bool deposit in new[] { false, true })
            {
                var f = new Fixture(deposit, 3, 2, 98, 98);
                string before = f.Snapshot();
                Need(!f.Transfer(3), "Overflow accepted.");
                Need(f.Published == 0 && f.Snapshot() == before, "Rejected transfer mutated state.");
            }
        });
        Test("different mechanics cannot contribute stack capacity", () =>
        {
            var f = new Fixture(false, 8, 2, 95, 95);
            f.Target[1].healAmount = 25;
            string before = f.Snapshot();
            Need(!f.Transfer(8), "Mechanically different items merged.");
            Need(f.Snapshot() == before && f.Published == 0, "Incompatible rejection mutated state.");
        });
        Test("different binding cannot contribute stack capacity", () =>
        {
            var f = new Fixture(true, 8, 2, 95, 95);
            f.Target[1].templateId = "different-template";
            string before = f.Snapshot();
            Need(!f.Transfer(8) && f.Snapshot() == before, "Different template merged.");
        });
        Test("failed or thrown paired publication rolls back every changed stack", () =>
        {
            foreach (bool deposit in new[] { false, true })
            foreach (bool throws in new[] { false, true })
            {
                var f = new Fixture(deposit, 20, 3, 95, 95);
                string before = f.Snapshot();
                Need(!f.Transfer(12, () => { if (throws) throw new IOException("injected failure"); return false; }), "Failed publication accepted.");
                Need(f.Published == 1, "Did not reach publication after planning fragmented stacks.");
                Need(f.Snapshot() == before, "Rollback lost items, identity, counters, timestamps or revisions.");
            }
        });
        Test("split identity collision is rejected atomically", () =>
        {
            var f = new Fixture(false, 20, 3, 95, 95);
            f.Target[0].itemId = "split:" + YQContainerInventory.StableKey("source|fixture|stack-profile|0");
            string before = f.Snapshot();
            Need(!f.Transfer(12) && f.Published == 0 && f.Snapshot() == before, "Split identity collision escaped.");
        });
        Test("stack limit and stale revision guards are retained", () =>
        {
            var f = new Fixture(true, 100, 3, 1, 1);
            string before = f.Snapshot();
            Need(!f.Transfer(100) && f.Snapshot() == before, "Per-transfer stack limit weakened.");
            Need(!YQContainerInventory.TryTransfer(f.World, f.Player, "fixture", true, "source", 1,
                -1, f.Player.stateRevision, () => true, out _), "Stale revision accepted.");
        });
        Test("nonstackable item identity and equipped deposit guard are retained", () =>
        {
            var f = new Fixture(true, 1, 1);
            f.Source[0].stackable = false;
            f.Source[0].itemType = "weapon"; f.Source[0].equipSlot = "weapon";
            f.Player.equippedItemBySlot["weapon"] = "source";
            Need(!f.Transfer(1), "Equipped item was deposited.");
            f.Player.equippedItemBySlot.Clear();
            Need(f.Transfer(1), f.Message);
            f.Expect(0, 1);
            Need(f.Target[0].itemId == "source", "Nonstackable identity changed.");
        });
        Test("roundtrip preserves merged inventory and prevents repeat withdrawal", () =>
        {
            var f = new Fixture(false, 8, 2, 95, 95);
            Need(f.Transfer(8), f.Message);
            var world = YQContainerInventory.Copy(f.World);
            var player = YQContainerInventory.Copy(f.Player);
            Need(world.containers["fixture"].contents.Count == 0 && player.inventoryItems[0].quantity == 99 &&
                 player.inventoryItems[1].quantity == 99, "Serialized state lost transfer.");
            Need(!YQContainerInventory.TryTransfer(world, player, "fixture", false, "source", 8,
                world.containers["fixture"].revision, player.stateRevision, () => true, out _), "Repeated loot accepted.");
        });
        Test("deterministic capacity and conservation matrix, 160 transactions", () =>
        {
            var random = new Random(73129);
            for (int i = 0; i < 160; i++)
            {
                bool deposit = i % 2 == 0;
                int a = random.Next(1, 100), b = random.Next(1, 100), quantity = random.Next(1, 100);
                int capacity = i % 3 == 0 ? 3 : 2;
                int sourceQuantity = Math.Min(99, quantity + 3);
                var f = new Fixture(deposit, sourceQuantity, capacity, a, b);
                string before = f.Snapshot();
                bool expected = quantity <= 198 - a - b + (capacity - 2) * 99;
                Need(f.Transfer(quantity) == expected, $"Capacity mismatch at case {i}: {a}+{b}, moving {quantity}, slots {capacity}: {f.Message}");
                if (!expected) { Need(f.Snapshot() == before && f.Published == 0, "Rejected matrix case mutated state."); continue; }
                int total = 0;
                foreach (var item in f.Source) total += item.quantity;
                foreach (var item in f.Target) { total += item.quantity; Need(item.quantity >= 1 && item.quantity <= 99, "Invalid stack."); }
                Need(total == a + b + sourceQuantity && f.Target.Count <= capacity, "Items or slots not conserved.");
                Need(YQContainerInventory.ValidateOwnership(f.World, f.Player, null, f.Player.inventoryItems, out string error), error);
            }
        });
        File.WriteAllText(Path.Combine(args[0], "results.json"), JsonConvert.SerializeObject(new
        {
            utc = DateTime.UtcNow.ToString("O"), status = failed == 0 ? "PASS" : "FAIL", failed, results,
            runtimeModule = typeof(YQContainerInventory).Module.ModuleVersionId.ToString(),
            evidence = "DETACHED_ACTUAL_COMPILED_INVENTORY_TRANSACTIONS; synthetic documents only; no Unity, UI or production-save claim"
        }, Formatting.Indented));
        return failed == 0 ? 0 : 1;

        void Test(string name, Action action)
        {
            try { action(); results.Add(new { name, status = "PASS" }); Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; results.Add(new { name, status = "FAIL", error = error.ToString() }); Console.WriteLine("FAIL " + name + ": " + error.Message); }
        }
    }

    private sealed class Fixture
    {
        internal readonly bool Deposit;
        internal readonly PlayerState Player;
        internal readonly WorldState World;
        internal int Published;
        internal string Message;
        internal YQContainerRecord Record => World.containers["fixture"];
        internal List<InventoryItemRecord> Source => Deposit ? Player.inventoryItems : Record.contents;
        internal List<InventoryItemRecord> Target => Deposit ? Record.contents : Player.inventoryItems;

        internal Fixture(bool deposit, int sourceQuantity, int capacity, params int[] held)
        {
            Deposit = deposit;
            Player = new PlayerState { playerId = "stack-profile", currency = 17 };
            Player.EnsureCollections();
            World = new WorldState(); World.EnsureCollections();
            World.containers["fixture"] = new YQContainerRecord
            {
                entityId = "fixture", profileId = Player.playerId, displayName = "Fixture", generated = true,
                context = new YQContainerContext { entityId = "fixture", sourceType = YQContainerType.Chest },
                capacity = deposit ? capacity : 16
            };
            Player.inventoryCapacity = deposit ? 16 : capacity;
            Source.Add(Item("source", sourceQuantity));
            for (int i = 0; i < held.Length; i++) Target.Add(Item("held-" + i, held[i]));
        }

        internal bool Transfer(int quantity, Func<bool> publish = null) => YQContainerInventory.TryTransfer(
            World, Player, "fixture", Deposit, "source", quantity, Record.revision, Player.stateRevision,
            () => { Published++; return publish == null || publish(); }, out Message);
        internal string Snapshot() => YQContainerInventory.Serialize(Player) + YQContainerInventory.Serialize(World);
        internal void Expect(int sourceQuantity, params int[] quantities)
        {
            Need(Source.Count == (sourceQuantity == 0 ? 0 : 1), "Source count incorrect.");
            if (sourceQuantity > 0) Need(Source[0].quantity == sourceQuantity, "Source quantity incorrect.");
            Need(Target.Count == quantities.Length, "Destination stack count incorrect.");
            for (int i = 0; i < quantities.Length; i++) Need(Target[i].quantity == quantities[i], "Destination quantity incorrect.");
            Need(YQContainerInventory.ValidateOwnership(World, Player, null, Player.inventoryItems, out string error), error);
        }
    }
    private static InventoryItemRecord Item(string id, int quantity) => new InventoryItemRecord
    { itemId = id, templateId = "fixture-supply", displayName = "Fixture Supply", itemType = "supply", rarity = "Common", stackable = true, quantity = quantity };
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
