using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public enum YQContainerType { Chest, Wardrobe, Cupboard, Barrel, Crate, Cache, Hostile }
public enum YQContainerAccess { Public, OwnerOnly, CorpseOnly }
public enum YQContainerRestock { Never, ExplicitWhenEmpty }
public enum YQLootRarity { Common, Uncommon, Rare, Epic, Unique, Legendary, Mythical }

[Serializable]
public sealed class YQContainerContext
{
    public string worldSeed, entityId, regionId, biome, factionId, culture, ownerRole, siteId, sitePurpose, element, theme, sourceRarity;
    public YQContainerType sourceType;
    public int sourceLevel = 1, progressionTier = 1, encounterDifficulty = 1, rarityBudget = 2, wealth;
    public float regionDanger;
    public List<string> tags = new List<string>();
    public List<string> fallbacks = new List<string>();
}

[Serializable]
public sealed class YQContainerRecord
{
    public string entityId, displayName, ownerId, profileId, seed, lootProfileId;
    public int capacity = 16, generationVersion, restockCycle, currency;
    public long revision;
    public bool generated, locked, dead, hasPlayerDeposits, legacyConsumed;
    public bool hasCorpsePosition;
    public UnityEngine.Vector3 corpseLogicalPosition;
    public YQContainerAccess access;
    public YQContainerRestock restock;
    public YQContainerContext context;
    public List<InventoryItemRecord> contents = new List<InventoryItemRecord>();
    // note: Equipment references the same contents, never an extra copy for corpse rewards.
    public Dictionary<string, string> equippedItemBySlot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<string> restockReceipts = new List<string>();
    public List<string> authoredClaimIds = new List<string>();
}

public interface IYQInventory
{
    string EntityId { get; }
    int Capacity { get; }
    IReadOnlyList<InventoryItemRecord> Contents { get; }
    long Revision { get; }
}

// note: Views resolve canonical documents on every use so reload, rollback and streaming cannot retain stale contents.
public sealed class YQPlayerInventoryView : IYQInventory
{
    private readonly PlayerState _state;
    public YQPlayerInventoryView(PlayerState state) { _state = state; }
    public string EntityId => _state.playerId;
    public int Capacity => _state.inventoryCapacity;
    public IReadOnlyList<InventoryItemRecord> Contents => _state.inventoryItems;
    public long Revision => _state.stateRevision;
}

public sealed class YQContainerInventoryView : IYQInventory
{
    private readonly WorldState _world;
    private readonly string _id;
    public YQContainerInventoryView(WorldState world, string id) { _world = world; _id = id; }
    private YQContainerRecord Record => YQContainerInventory.Find(_world, _id);
    public string EntityId => _id;
    public int Capacity => Record?.capacity ?? 0;
    public IReadOnlyList<InventoryItemRecord> Contents => Record?.contents;
    public long Revision => Record?.revision ?? -1;
}

public static class YQContainerInventory
{
    public const int GenerationVersion = 1;
    public const int MaxStack = 99;
    private static readonly JsonSerializerSettings CopySettings = new JsonSerializerSettings
    {
        // note: Use the owners' converters instead of serializing Unity vector convenience properties recursively.
        Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
    };

    public static YQContainerRecord Find(WorldState world, string id) =>
        world?.containers != null && id != null && world.containers.TryGetValue(id, out YQContainerRecord record) ? record : null;

    public static string StableKey(string input)
    {
        // note: Reuse the existing stable hashing authority without consuming canonical random streams.
        return YQStateContract.Sha256Hex(input ?? "");
    }

    public static string Seed(YQContainerContext context, int version, int cycle = 0) =>
        StableKey(Part(context.worldSeed) + Part(context.entityId) + Part(version.ToString(System.Globalization.CultureInfo.InvariantCulture)) + Part(cycle.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    private static string Part(string value) { value ??= ""; return value.Length + ":" + value; }
    public static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(Serialize(value), CopySettings);
    public static string Serialize(object value) => JsonConvert.SerializeObject(value, Formatting.Indented, CopySettings);

    public static bool CanAccess(YQContainerRecord record, PlayerState player, out string failure)
    {
        failure = "Inventory is unavailable.";
        if (record == null || player == null || !record.generated) return false;
        if (!string.Equals(record.profileId, player.playerId, StringComparison.Ordinal)) { failure = "Container belongs to another profile."; return false; }
        if (record.locked) { failure = "Storage is locked."; return false; }
        if ((record.access == YQContainerAccess.CorpseOnly || record.context?.sourceType == YQContainerType.Hostile) && !record.dead)
        { failure = "Living hostile inventories cannot be opened."; return false; }
        if (record.access == YQContainerAccess.OwnerOnly && !string.Equals(record.ownerId, player.playerId, StringComparison.Ordinal))
        { failure = "Owner permission required; theft is not authorized."; return false; }
        if (!Enum.IsDefined(typeof(YQContainerAccess), record.access)) { failure = "Unsupported access policy."; return false; }
        failure = "";
        return true;
    }

    public static bool CanStack(InventoryItemRecord first, InventoryItemRecord second)
    {
        if (first == null || second == null || !first.stackable || !second.stackable ||
            string.IsNullOrWhiteSpace(first.templateId) || !string.Equals(first.templateId, second.templateId, StringComparison.Ordinal)) return false;
        // note: Equal template names alone cannot merge items with different mechanical or asset bindings.
        JObject left = JObject.FromObject(first), right = JObject.FromObject(second);
        foreach (string key in new[] { "itemId", "quantity", "generatedAtUnixString" }) { left.Remove(key); right.Remove(key); }
        return JToken.DeepEquals(left, right);
    }

    public static bool ValidateContents(List<InventoryItemRecord> items, int capacity, out string failure)
    {
        failure = "Invalid inventory contents.";
        if (items == null || capacity < 0 || (capacity > 0 && items.Count > capacity)) return false;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (InventoryItemRecord item in items)
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || !ids.Add(item.itemId) || item.quantity < 1 ||
                (!item.stackable && item.quantity != 1) || string.IsNullOrWhiteSpace(item.itemType) || string.IsNullOrWhiteSpace(item.templateId)) return false;
        failure = "";
        return true;
    }

    public static bool TryTransfer(WorldState world, PlayerState player, string id, bool deposit, string itemId, int quantity,
        long expectedContainerRevision, long expectedPlayerRevision, Func<bool> publish, out string message)
    {
        YQContainerRecord current = Find(world, id);
        if (!CanAccess(current, player, out message)) return false;
        if (current.revision != expectedContainerRevision || player.stateRevision != expectedPlayerRevision)
        { message = "Inventory changed; refresh and retry."; return false; }
        if (!ValidateContents(current.contents, current.capacity, out message) || !ValidateContents(player.inventoryItems, 0, out message)) return false;
        if (deposit && current.access == YQContainerAccess.CorpseOnly) { message = "Corpse storage does not accept deposits."; return false; }
        if (deposit && player.equippedItemBySlot != null && player.equippedItemBySlot.ContainsValue(itemId))
        { message = "Unequip the item before depositing it."; return false; }
        YQContainerRecord next = Copy(current);
        List<InventoryItemRecord> backpack = Copy(player.inventoryItems);
        List<InventoryItemRecord> source = deposit ? backpack : next.contents, target = deposit ? next.contents : backpack;
        InventoryItemRecord item = source.Find(i => string.Equals(i.itemId, itemId, StringComparison.Ordinal));
        if (item == null || quantity <= 0 || quantity > item.quantity || (!item.stackable && quantity != 1))
        { message = "Invalid item or quantity."; return false; }
        if (!Move(source, target, item, quantity, deposit ? next.capacity : player.inventoryCapacity,
                id + "|" + player.playerId + "|" + current.revision, out message)) return false;
        if (deposit) next.hasPlayerDeposits = true;
        else
        {
            // note: Looting removes equipment references, keeping one physical item through death and transfer.
            var slots = new List<string>(next.equippedItemBySlot.Keys);
            foreach (string slot in slots) if (next.equippedItemBySlot[slot] == itemId) next.equippedItemBySlot.Remove(slot);
        }
        next.revision++;
        if (!Commit(world, player, current, next, backpack, player.currency, publish, out message, !deposit)) return false;
        message = (deposit ? "Stored " : "Took ") + item.displayName + " x" + quantity + ".";
        return true;
    }

    private static bool Move(List<InventoryItemRecord> source, List<InventoryItemRecord> target, InventoryItemRecord item,
        int quantity, int capacity, string splitKey, out string failure)
    {
        failure = "Item identity is already present in the destination.";
        if (target.Exists(i => string.Equals(i.itemId, item.itemId, StringComparison.OrdinalIgnoreCase))) return false;
        InventoryItemRecord merged = item.stackable ? target.Find(i => CanStack(i, item) && (long)i.quantity + quantity <= MaxStack) : null;
        if (merged == null)
        {
            if (capacity > 0 && target.Count >= capacity) { failure = "Destination is full."; return false; }
            if (quantity > MaxStack && item.stackable) { failure = "Transfer at most " + MaxStack + " per stack."; return false; }
            InventoryItemRecord moved = Copy(item);
            moved.quantity = quantity;
            if (quantity != item.quantity) moved.itemId = "split:" + StableKey(item.itemId + "|" + splitKey);
            if (target.Exists(i => string.Equals(i.itemId, moved.itemId, StringComparison.OrdinalIgnoreCase))) return false;
            target.Add(moved);
        }
        else merged.quantity += quantity;
        item.quantity -= quantity;
        if (item.quantity == 0) source.Remove(item);
        failure = "";
        return true;
    }

    public static bool TryTakeCurrency(WorldState world, PlayerState player, string id, long expectedRevision, Func<bool> publish, out string message)
    {
        YQContainerRecord current = Find(world, id);
        if (!CanAccess(current, player, out message)) return false;
        if (current.revision != expectedRevision || current.currency <= 0 || (long)player.currency + current.currency > int.MaxValue)
        { message = "Currency changed or cannot be held."; return false; }
        YQContainerRecord next = Copy(current);
        int amount = next.currency;
        next.currency = 0; next.revision++;
        if (!Commit(world, player, current, next, player.inventoryItems, player.currency + amount, publish, out message, true)) return false;
        message = "Took " + amount + " gold.";
        return true;
    }

    public static bool PublishRecord(WorldState world, PlayerState player, YQContainerRecord next, Func<bool> publish, out string message)
    {
        if (world == null || player == null || next == null || string.IsNullOrWhiteSpace(next.entityId) ||
            next.profileId != player.playerId || !ValidateContents(next.contents, next.capacity, out message))
        { message = "Container could not be validated."; return false; }
        return Commit(world, player, Find(world, next.entityId), next, player.inventoryItems, player.currency, publish, out message);
    }

    private static bool Commit(WorldState world, PlayerState player, YQContainerRecord old, YQContainerRecord next,
        List<InventoryItemRecord> backpack, int currency, Func<bool> publish, out string message, bool withdrawal = false)
    {
        if (!ValidateOwnership(world, player, next, backpack, out message)) return false;
        // note: Validate before mutation, publish both documents through the existing owner, and restore inventory state if publication fails.
        List<InventoryItemRecord> oldItems = player.inventoryItems;
        int oldCurrency = player.currency;
        long playerRevision = player.stateRevision, worldRevision = world.stateRevision;
        long playerUpdated = player.lastUpdatedUnix, worldUpdated = world.lastUpdatedUnix;
        Dictionary<string, float> oldCounters = player.behaviorCounters;
        List<string> oldLedger = player.behaviorLedger;
        world.containers ??= new Dictionary<string, YQContainerRecord>(StringComparer.Ordinal);
        world.containers[next.entityId] = next;
        player.inventoryItems = backpack; player.currency = currency;
        if (withdrawal)
        {
            player.behaviorCounters = oldCounters == null ? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) :
                new Dictionary<string, float>(oldCounters, StringComparer.OrdinalIgnoreCase);
            player.behaviorLedger = Copy(oldLedger) ?? new List<string>();
            player.IncCounter(next.access == YQContainerAccess.CorpseOnly ? "loot:corpse" : "loot:chest", 1);
            if (next.access == YQContainerAccess.CorpseOnly) player.IncCounter("loot:enemy", 1);
            player.AddLedgerLine("Recovered contents from " + next.displayName + ".");
        }
        player.Touch(); world.TouchNow();
        bool success = false;
        message = "Paired inventory save is unavailable.";
        try { success = publish != null && publish(); }
        catch (Exception exception) { message = "Paired inventory save failed: " + exception.Message; }
        if (success) { message = "Inventory transaction committed."; return true; }
        player.inventoryItems = oldItems; player.currency = oldCurrency;
        player.behaviorCounters = oldCounters; player.behaviorLedger = oldLedger;
        player.stateRevision = playerRevision; player.lastUpdatedUnix = playerUpdated;
        world.stateRevision = worldRevision; world.lastUpdatedUnix = worldUpdated;
        if (old == null) world.containers.Remove(next.entityId); else world.containers[next.entityId] = old;
        return false;
    }

    public static bool ValidateOwnership(WorldState world, PlayerState player, YQContainerRecord replacement, List<InventoryItemRecord> backpack, out string failure)
    {
        failure = "Invalid container identity, ownership or equipped reference.";
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (InventoryItemRecord item in backpack ?? new List<InventoryItemRecord>())
            if (item != null && !string.IsNullOrWhiteSpace(item.itemId) && !ids.Add(item.itemId)) return false;
        if (world?.containers != null)
            foreach (KeyValuePair<string, YQContainerRecord> pair in world.containers)
            {
                if (replacement != null && pair.Key == replacement.entityId) continue;
                if (!Check(pair.Key, pair.Value)) return false;
            }
        if (replacement != null && !Check(replacement.entityId, replacement)) return false;
        failure = ""; return true;

        bool Check(string key, YQContainerRecord record)
        {
            if (record == null || key != record.entityId || record.profileId != player?.playerId || record.context == null ||
                record.context.entityId != record.entityId || record.capacity < 1 || record.capacity > 128 || record.currency < 0 ||
                record.revision < 0 || !Enum.IsDefined(typeof(YQContainerAccess), record.access) || !Enum.IsDefined(typeof(YQContainerRestock), record.restock) ||
                !Enum.IsDefined(typeof(YQContainerType), record.context.sourceType) || record.contents == null || record.contents.Count > record.capacity ||
                record.equippedItemBySlot == null || record.restockReceipts == null || record.authoredClaimIds == null ||
                (record.context.sourceType == YQContainerType.Hostile && (record.access != YQContainerAccess.CorpseOnly || record.restock != YQContainerRestock.Never))) return false;
            foreach (InventoryItemRecord item in record.contents)
                if (item == null || string.IsNullOrWhiteSpace(item.itemId) || !ids.Add(item.itemId) || item.quantity < 1 ||
                    (item.stackable ? item.quantity > MaxStack : item.quantity != 1) || string.IsNullOrWhiteSpace(item.templateId)) return false;
            foreach (KeyValuePair<string, string> equipped in record.equippedItemBySlot)
                if (!record.contents.Exists(i => i != null && i.itemId == equipped.Value && i.IsEquippable && i.equipSlot == equipped.Key)) return false;
            return true;
        }
    }

    public static bool TryEquipHostile(WorldState world, PlayerState player, string id, string itemId, Func<bool> publish, out string failure)
    {
        YQContainerRecord record = Find(world, id);
        failure = "Hostile equipment is unavailable.";
        if (record == null || !record.generated || record.dead || record.context.sourceType != YQContainerType.Hostile) return false;
        InventoryItemRecord item = record.contents.Find(i => i.itemId == itemId);
        if (item == null || !item.IsEquippable) return false;
        YQContainerRecord next = Copy(record);
        next.equippedItemBySlot[item.equipSlot] = item.itemId; next.revision++;
        return PublishRecord(world, player, next, publish, out failure);
    }
}
