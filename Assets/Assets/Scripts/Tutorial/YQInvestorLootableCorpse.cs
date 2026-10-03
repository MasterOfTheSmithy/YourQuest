using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQInvestorLootableCorpse : MonoBehaviour
{
    // note: This registry contains only disposable scene views; accepted contents remain exclusively in WorldState.
    private static readonly Dictionary<string, YQInvestorLootableCorpse> Views = new Dictionary<string, YQInvestorLootableCorpse>();
    public string DisplayName { get; private set; } = "Corpse";
    public string EntityId { get; private set; }
    private InventoryItemRecord _legacyItem;
    private int _legacyGold;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetViews() { Views.Clear(); }

    public void Initialize(string displayName, InventoryItemRecord item, int gold)
    {
        // note: Preserve the old initializer for authored callers, importing its exact accepted item once through the shared record.
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Corpse" : displayName;
        EntityId = YQWorldContainer.ResolveEntityId(transform);
        _legacyItem = item; _legacyGold = Mathf.Max(0, gold);
        Bind();
    }

    public void Initialize(string displayName, string entityId)
    {
        DisplayName = displayName; EntityId = entityId;
        Bind(); Views[EntityId] = this;
    }

    private void Bind()
    {
        YQWorldContainer storage = GetComponent<YQWorldContainer>();
        if (storage == null) storage = gameObject.AddComponent<YQWorldContainer>();
        storage.entityId = EntityId; storage.displayName = DisplayName;
        storage.sourceType = YQContainerType.Hostile; storage.access = YQContainerAccess.CorpseOnly;
    }

    public void TryLoot(GameObject looter)
    {
        WorldState world = WorldStateManager.Instance?.State;
        PlayerState player = PlayerStateManager.Instance?.state;
        if (YQContainerInventory.Find(world, EntityId) == null)
        {
            if (world == null || player == null) return;
            var record = new YQContainerRecord
            {
                entityId = EntityId, displayName = DisplayName, profileId = player.playerId,
                context = YQContainerLoot.ResolveContext(world, EntityId, YQContainerType.Hostile, world.currentRegionId),
                generated = true, dead = true, access = YQContainerAccess.CorpseOnly,
                currency = _legacyGold, generationVersion = YQContainerInventory.GenerationVersion, lootProfileId = "legacy:accepted-corpse"
            };
            record.seed = YQContainerInventory.Seed(record.context, record.generationVersion);
            if (_legacyItem != null) record.contents.Add(YQContainerInventory.Copy(_legacyItem));
            if (!YQContainerInventory.PublishRecord(world, player, record, YQWorldContainer.PublishLive, out string failure))
            { GeneratedRpgContentService.Instance?.SetInventoryMessage(failure); return; }
        }
        // note: Remains stay available, including when empty; loot transfers and feedback use the same shared boundary as storage.
        GetComponent<YQWorldContainer>().TryOpen(looter);
    }

    private void OnDestroy()
    {
        if (EntityId != null && Views.TryGetValue(EntityId, out YQInvestorLootableCorpse view) && view == this) Views.Remove(EntityId);
    }

    public static void EnsurePersistedView(YQContainerRecord record, Vector3 position, Transform parent)
    {
        if (record == null || !record.dead || (record.contents.Count == 0 && record.currency == 0)) return;
        if (Views.TryGetValue(record.entityId, out YQInvestorLootableCorpse view) && view != null) return;
        // note: Restoring a corpse projects the same persisted inventory without kill rewards, item generation or a new identity.
        GameObject corpse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        corpse.name = record.displayName + " Remains"; corpse.transform.SetParent(parent, true);
        corpse.transform.position = position; corpse.transform.localScale = new Vector3(0.75f, 0.22f, 0.75f);
        YQInvestorRuntimeVisuals.SetRendererColor(corpse.GetComponent<Renderer>(), new Color(0.7f, 0.4f, 0.18f));
        Rigidbody body = corpse.AddComponent<Rigidbody>(); body.isKinematic = true;
        EntityInfo info = corpse.AddComponent<EntityInfo>(); info.entityId = record.entityId;
        info.displayName = record.displayName + " Remains"; info.hostility = Hostility.Neutral;
        info.factionId = "loot"; info.tags = new[] { "loot", "corpse", record.context.regionId };
        corpse.AddComponent<YQInvestorLootableCorpse>().Initialize(record.displayName, record.entityId);
    }
}
