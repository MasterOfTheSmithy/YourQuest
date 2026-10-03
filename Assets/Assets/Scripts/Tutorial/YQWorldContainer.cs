using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQWorldContainer : MonoBehaviour
{
    public string entityId = "";
    public string displayName = "Storage";
    public string regionId = "region_unknown";
    public YQContainerType sourceType;
    [Min(1)] public int capacity = 16;
    [Min(0)] public int sourceLevel;
    public YQContainerAccess access;
    public string ownerId = "";
    public YQContainerRestock restock;
    public YQContainerContext contextHints;
    public string siteId = "", sitePurpose = "storage";

    public string EntityId => string.IsNullOrWhiteSpace(entityId) ? ResolveEntityId(transform) : entityId;
    public IYQInventory Inventory => new YQContainerInventoryView(WorldStateManager.Instance?.State, EntityId);
    public YQContainerRecord Record => YQContainerInventory.Find(WorldStateManager.Instance?.State, EntityId);

    public static string ResolveEntityId(Transform target)
    {
        EntityInfo entity = target.GetComponent<EntityInfo>();
        if (entity != null && !string.IsNullOrWhiteSpace(entity.entityId) && entity.entityId != "entity_unknown") return entity.entityId;
        // note: Authored sources without an ID get a scene/hierarchy identity, not an instance ID or opening-time GUID. Moving them requires an explicit stable ID.
        string path = "";
        for (Transform node = target; node != null; node = node.parent)
            path = node.name.Length + ":" + node.name + ":" + node.GetSiblingIndex() + "/" + path;
        return "storage:authored:" + YQStateContract.Sha256Hex(target.gameObject.scene.path + "|" + path);
    }

    public static YQContainerType InferStorageType(string authoredName)
    {
        // note: Legacy prefab metadata supplies only physical type; loot mechanics never interpret generated descriptions.
        string name = (authoredName ?? "").ToLowerInvariant();
        if (name.Contains("wardrobe")) return YQContainerType.Wardrobe;
        if (name.Contains("cupboard") || name.Contains("cabinet")) return YQContainerType.Cupboard;
        if (name.Contains("barrel")) return YQContainerType.Barrel;
        if (name.Contains("crate")) return YQContainerType.Crate;
        if (name.Contains("cache")) return YQContainerType.Cache;
        return YQContainerType.Chest;
    }

    public static bool PublishLive()
    {
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
        // note: Test-session inventories are part of the existing rollback snapshot and respect its normal-save barrier.
        if (YQDeveloperTestSession.Active) return YQDeveloperTestSession.CheckOwners(out _);
#endif
        return YQProfileSaveSystem.Instance != null && YQProfileSaveSystem.Instance.SaveActiveProfile();
    }

    public bool TryEnsureGenerated(bool dead, out string failure)
    {
        WorldState world = WorldStateManager.Instance?.State;
        PlayerState player = PlayerStateManager.Instance?.state;
        failure = "Canonical player/world owners are unavailable.";
        if (world == null || player == null) return false;
        YQContainerRecord current = YQContainerInventory.Find(world, EntityId);
        if (current != null && current.generated)
        {
            if (dead && !current.dead)
            {
                YQContainerRecord next = YQContainerInventory.Copy(current);
                next.dead = true; next.locked = false; next.revision++;
                next.hasCorpsePosition = true; next.corpseLogicalPosition = transform.position + player.renderOrigin;
                return YQContainerInventory.PublishRecord(world, player, next, PublishLive, out failure);
            }
            failure = ""; return current.profileId == player.playerId;
        }
        EntityInfo entity = GetComponent<EntityInfo>();
        YQLockpickableLoot lockOwner = GetComponent<YQLockpickableLoot>();
        YQContainerContext context = current?.context ?? YQContainerLoot.ResolveContext(world, EntityId, sourceType, regionId, sourceLevel, entity, siteId, sitePurpose, contextHints);
        YQContainerRecord generated = current == null ? new YQContainerRecord
        {
            entityId = EntityId, displayName = displayName, profileId = player.playerId, capacity = Mathf.Clamp(capacity, 1, 128),
            context = context, access = sourceType == YQContainerType.Hostile ? YQContainerAccess.CorpseOnly : access,
            ownerId = ownerId, restock = sourceType == YQContainerType.Hostile ? YQContainerRestock.Never : restock,
            dead = dead, locked = lockOwner != null && lockOwner.locked && !dead
        } : YQContainerInventory.Copy(current);
        // note: Legacy opened counters represent rewards already awarded, so migration starts empty and cannot duplicate them.
        if (current != null || !YQContainerLoot.TryImportLegacyOpened(generated, player))
        {
            try
            {
                if (!YQContainerLoot.Generate(generated, GeneratedRpgContentService.Instance, world, player, YQContainerLoot.Catalog, out failure)) return false;
            }
            catch (Exception exception) { failure = "Loot configuration failed: " + exception.Message; return false; }
        }
        generated.dead |= dead; generated.revision++;
        if (dead) { generated.hasCorpsePosition = true; generated.corpseLogicalPosition = transform.position + player.renderOrigin; }
        return YQContainerInventory.PublishRecord(world, player, generated, PublishLive, out failure);
    }

    public bool TryOpen(GameObject playerObject)
    {
        // note: Interaction range and living-hostile checks remain authoritative even if a component is configured incorrectly.
        YQInvestorEnemy hostile = GetComponentInParent<YQInvestorEnemy>();
        if (hostile != null && !hostile.IsDead) return Reject("Living hostile inventories cannot be opened.");
        if (playerObject == null || playerObject.GetComponent<YQInvestorPlayerMotor>()?.IsAuthoritative != true || Vector3.Distance(playerObject.transform.position, transform.position) > 5f)
            return Reject("Move closer to this storage.");
        YQLockpickableLoot lockOwner = GetComponent<YQLockpickableLoot>();
        if (lockOwner != null && (lockOwner.locked || lockOwner.mimic)) return Reject("Use this source's lock or mimic interaction.");
        if (sourceType == YQContainerType.Hostile && Record?.dead != true) return Reject("Only a corpse can be looted.");
        if (!TryEnsureGenerated(sourceType == YQContainerType.Hostile, out string failure)) return Reject(failure);
        if (!YQContainerInventory.CanAccess(Record, PlayerStateManager.Instance?.state, out failure)) return Reject(failure);
        return YQContainerUI.Open(this, playerObject);
    }

    private static bool Reject(string message) { GeneratedRpgContentService.Instance?.SetInventoryMessage(message); return false; }

    public static YQWorldContainer BindStorage(YQLockpickableLoot loot)
    {
        YQWorldContainer source = loot.GetComponent<YQWorldContainer>();
        if (source != null) return source;
        source = loot.gameObject.AddComponent<YQWorldContainer>();
        source.entityId = string.IsNullOrWhiteSpace(loot.persistentLootId) ? ResolveEntityId(loot.transform) : loot.persistentLootId;
        source.displayName = loot.displayName; source.regionId = loot.regionId; source.sourceType = loot.containerType;
        source.sourceLevel = loot.rewardLevelOverride;
        return source;
    }

    public static YQWorldContainer BindHostile(YQInvestorEnemy enemy)
    {
        YQWorldContainer source = enemy.GetComponent<YQWorldContainer>();
        if (source == null) source = enemy.gameObject.AddComponent<YQWorldContainer>();
        source.entityId = ResolveEntityId(enemy.transform); source.displayName = enemy.displayName;
        source.regionId = enemy.semanticRegionId; source.sourceType = YQContainerType.Hostile; source.access = YQContainerAccess.CorpseOnly;
        source.restock = YQContainerRestock.Never;
        source.contextHints ??= new YQContainerContext();
        source.contextHints.sourceRarity = enemy.rarity;
        if (Enum.TryParse(enemy.rarity, true, out YQLootRarity rarity))
            source.contextHints.encounterDifficulty = 1 + (int)rarity;
        return source;
    }

    public bool TryBindEquipment(InventoryItemRecord acceptedItem, out string failure)
    {
        // note: Adapters can attach an existing accepted equipped item before lazy bonus generation. They must relinquish any separate store after success.
        WorldState world = WorldStateManager.Instance?.State;
        PlayerState player = PlayerStateManager.Instance?.state;
        failure = "An accepted, compatible hostile item and ready canonical owners are required.";
        if (sourceType != YQContainerType.Hostile || acceptedItem == null || !acceptedItem.IsEquippable || world == null || player == null ||
            !YQContainerLoot.CompatibleAsset(GeneratedRpgContentService.Instance, acceptedItem, null)) return false;
        YQContainerRecord current = Record;
        if (current?.dead == true) return false;
        if (player.inventoryItems.Exists(i => i != null && i.itemId == acceptedItem.itemId)) return false;
        foreach (YQContainerRecord other in world.containers.Values)
            if (other != null && other.entityId != EntityId && other.contents.Exists(i => i != null && i.itemId == acceptedItem.itemId)) return false;
        YQContainerRecord next = current == null ? new YQContainerRecord
        {
            entityId = EntityId, displayName = displayName, profileId = player.playerId, capacity = capacity,
            access = YQContainerAccess.CorpseOnly,
            context = YQContainerLoot.ResolveContext(world, EntityId, sourceType, regionId, sourceLevel, GetComponent<EntityInfo>(), siteId, sitePurpose, contextHints)
        } : YQContainerInventory.Copy(current);
        if (!next.contents.Exists(i => i.itemId == acceptedItem.itemId)) next.contents.Add(YQContainerInventory.Copy(acceptedItem));
        next.equippedItemBySlot[acceptedItem.equipSlot] = acceptedItem.itemId; next.revision++;
        return YQContainerInventory.PublishRecord(world, player, next, PublishLive, out failure);
    }
}
