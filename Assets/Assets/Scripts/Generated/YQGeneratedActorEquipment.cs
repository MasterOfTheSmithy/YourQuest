using System;
using System.Collections.Generic;
using UnityEngine;

// note: This adapter owns presentation only. Items and equipped references remain in WorldState.containers.
[DisallowMultipleComponent]
public sealed class YQGeneratedActorEquipment : MonoBehaviour
{
    private const string LoadoutReceipt = "actor-loadout-v1";
    private string _entityId;
    private long _shownRevision = -1;
    private float _nextRefresh;
    private readonly List<GameObject> _visuals = new List<GameObject>();

    public static void Bind(EntityInfo actor, string regionId, string siteId, bool armed)
    {
        if (actor == null || string.IsNullOrWhiteSpace(actor.entityId) || FindHand(actor.gameObject, false) == null) return;
        WorldState world = WorldStateManager.Instance?.State;
        PlayerState player = PlayerStateManager.Instance?.state;
        GeneratedRpgContentService content = GeneratedRpgContentService.Instance;
        if (world == null || player == null || content?.library == null) return;
        bool hostile = actor.GetComponent<YQInvestorEnemy>() != null;
        YQContainerRecord record = YQContainerInventory.Find(world, actor.entityId);
        // note: Existing gear and looted/dead inventories are reused, never refilled by streaming or a changed catalog.
        if (record?.dead != true && !(record?.authoredClaimIds.Contains(LoadoutReceipt) ?? false))
        {
            YQContainerRecord next = record == null ? new YQContainerRecord
            {
                entityId = actor.entityId, displayName = actor.displayName, profileId = player.playerId,
                ownerId = actor.entityId, access = hostile ? YQContainerAccess.CorpseOnly : YQContainerAccess.OwnerOnly,
                restock = YQContainerRestock.Never,
                context = YQContainerLoot.ResolveContext(world, actor.entityId, hostile ? YQContainerType.Hostile : YQContainerType.Npc,
                    regionId, Mathf.Max(1, actor.level), actor, siteId, "character_equipment", null)
            } : YQContainerInventory.Copy(record);
            string seed = YQContainerInventory.Seed(next.context, YQContainerInventory.GenerationVersion);
            // note: The existing mechanical generator selects approved equipment. Civilians carry one item; guards/humanoids can carry a shield.
            var slots = new List<string> { "weapon" };
            if (armed && FindHand(actor.gameObject, true) != null) slots.Add("offhand");
            foreach (string slot in slots)
            {
                if (next.equippedItemBySlot.ContainsKey(slot)) continue;
                InventoryItemRecord item = content.GenerateContainerItem(seed + "|" + LoadoutReceipt + "|" + slot, next.context,
                    new YQLootEntry { id = slot, itemKind = slot }, YQLootRarity.Common);
                if (item == null || !item.IsEquippable || item.equipSlot != slot || ResolvePrefab(item.prefabKey) == null) continue;
                // note: Reuse an already accepted identity if present; equipment references exactly one owned item.
                if (!next.contents.Exists(existing => existing.itemId == item.itemId)) next.contents.Add(item);
                next.equippedItemBySlot[slot] = item.itemId;
            }
            if (next.equippedItemBySlot.Count == 0)
            { Debug.LogWarning("[YQActorEquipment] No approved compatible equipment for " + actor.entityId); return; }
            next.authoredClaimIds.Add(LoadoutReceipt);
            next.seed = seed; next.generationVersion = YQContainerInventory.GenerationVersion;
            if (!hostile) { next.generated = true; next.lootProfileId = LoadoutReceipt; }
            next.revision++;
            if (!YQContainerInventory.PublishRecord(world, player, next, YQWorldContainer.PublishLive, out string failure))
            { Debug.LogWarning("[YQActorEquipment] Equipment publication rejected for " + actor.entityId + ": " + failure); return; }
        }
        var visual = actor.GetComponent<YQGeneratedActorEquipment>();
        if (visual == null) visual = actor.gameObject.AddComponent<YQGeneratedActorEquipment>();
        visual._entityId = actor.entityId;
        visual.Refresh();
    }

    private void Update()
    {
        // note: Revision checks also remove gear taken from corpses without keeping a second item cache or scanning the scene.
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 1f;
        Refresh();
    }

    private void Refresh()
    {
        var record = YQContainerInventory.Find(WorldStateManager.Instance?.State, _entityId);
        if (record == null || record.revision == _shownRevision) return;
        _shownRevision = record.revision;
        foreach (GameObject visual in _visuals)
            if (visual != null) { visual.SetActive(false); Destroy(visual); }
        _visuals.Clear();
        foreach (var equipped in record.equippedItemBySlot)
        {
            InventoryItemRecord item = record.contents.Find(candidate => candidate.itemId == equipped.Value);
            if (item == null || (equipped.Key != "weapon" && equipped.Key != "offhand")) continue;
            Transform hand = FindHand(gameObject, equipped.Key == "offhand");
            GameObject prefab = ResolvePrefab(item.prefabKey);
            if (hand == null || prefab == null || prefab.GetComponentInChildren<YQInvestorPlayerMotor>(true) != null ||
                prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null || prefab.GetComponentInChildren<Animator>(true) != null) continue;
            GameObject model = Instantiate(prefab, hand, false);
            model.name = "Equipped__" + equipped.Key + "__" + item.itemId;
            // note: Approved visual prefabs cannot add their demo movement, collision or damage authority to the actor.
            foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true)) { script.enabled = false; Destroy(script); }
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) { collider.enabled = false; Destroy(collider); }
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; Destroy(body); }
            foreach (AudioSource audio in model.GetComponentsInChildren<AudioSource>(true)) { audio.enabled = false; Destroy(audio); }
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // note: Preserve authored grips; cap unusually large imported rigid gear using the actor's measured body height.
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                float maximum = equipped.Key == "offhand" ? .8f : 1.25f;
                if (size > maximum) model.transform.localScale *= maximum / size;
            }
            _visuals.Add(model);
        }
    }

    private static GameObject ResolvePrefab(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return null;
        var prefab = YQRuntimeWorldAssetRegistry.Instance?.ResolvePrefab(path);
#if UNITY_EDITOR
        if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
        return prefab;
    }

    private static Transform FindHand(GameObject actor, bool left)
    {
        // note: Prefer supplied grip transforms, then the actor's humanoid avatar; quadrupeds never receive guessed attachments.
        string[] names = left ? new[] { "ShieldBoneL", "HandBoneL", "Base HumanLPalm" } : new[] { "HandBoneR", "Base HumanRPalm" };
        foreach (string name in names)
            foreach (Transform bone in actor.GetComponentsInChildren<Transform>(true)) if (bone.name == name) return bone;
        foreach (Animator animator in actor.GetComponentsInChildren<Animator>(true))
            if (animator.isHuman && animator.avatar != null) return animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        return null;
    }
}
