// Assets/Assets/Scripts/Tutorial/YQInvestorWorldPickup.cs
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQInvestorWorldPickup : MonoBehaviour
{
    public InventoryItemRecord item;
    public int gold;

    public string DisplayName => item != null ? item.displayName : (gold > 0 ? gold + " Gold" : "Pickup");

    public static bool TrySpawnForPlayer(InventoryItemRecord source, bool consumeFromInventory)
    {
        if (source == null)
            return false;

        GameObject player = GameObject.FindWithTag("Player");
        Vector3 position = player != null ? player.transform.position + player.transform.forward * 1.25f + Vector3.up * 0.5f : Vector3.up;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = source.displayName + " Pickup";
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.55f;
        Renderer renderer = go.GetComponent<Renderer>();
        YQInvestorRuntimeVisuals.SetRendererColor(renderer, source.IsConsumable ? new Color(0.30f, 0.80f, 0.50f, 1f) : new Color(0.80f, 0.72f, 0.38f, 1f));
        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.useGravity = true;

        YQInvestorWorldPickup pickup = go.AddComponent<YQInvestorWorldPickup>();
        pickup.item = CloneItem(source, quantityOverride: 1);

        if (consumeFromInventory)
        {
            PlayerStateManager psm = PlayerStateManager.Instance;
            if (psm != null && psm.state != null)
            {
                if (source.quantity > 1)
                    source.quantity--;
                else
                    psm.state.inventoryItems.Remove(source);

                if (!string.IsNullOrWhiteSpace(source.equipSlot) && psm.state.equippedItemBySlot.TryGetValue(source.equipSlot, out string equippedId) && equippedId == source.itemId)
                    psm.state.equippedItemBySlot.Remove(source.equipSlot);
            }
        }

        return true;
    }

    public void Initialize(InventoryItemRecord inventoryItem, int goldAmount)
    {
        item = inventoryItem;
        gold = goldAmount;
    }

    public void TryCollect(GameObject collector)
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return;

        if (item != null)
        {
            psm.state.AddOrUpdateItem(CloneItem(item, item.quantity), true);
            psm.state.IncCounter("pickup:item", Mathf.Max(1, item.quantity));
            if (!string.IsNullOrWhiteSpace(item.templateId))
                psm.state.IncCounter("pickup:item:" + SanitizeCounterKey(item.templateId), Mathf.Max(1, item.quantity));
            if (!string.IsNullOrWhiteSpace(item.displayName))
                psm.state.IncCounter("pickup:item:" + SanitizeCounterKey(item.displayName), Mathf.Max(1, item.quantity));
            psm.state.AddLedgerLine("The player picked up " + item.displayName + ".");
            GeneratedRpgContentService.Instance?.SetInventoryMessage("Picked up " + item.displayName + ".");
        }
        if (gold > 0)
        {
            psm.state.currency += gold;
            psm.state.IncCounter("pickup:gold", gold);
            GeneratedRpgContentService.Instance?.SetInventoryMessage("Picked up " + gold + " gold.");
        }

        YQRuntimeAudioFeedback.PlayPickup(transform.position);
        psm.Save();
        Destroy(gameObject);
    }

    private static InventoryItemRecord CloneItem(InventoryItemRecord src, int quantityOverride)
    {
        return new InventoryItemRecord
        {
            itemId = System.Guid.NewGuid().ToString("N"),
            templateId = src.templateId,
            displayName = src.displayName,
            itemType = src.itemType,
            equipSlot = src.equipSlot,
            rarity = src.rarity,
            description = src.description,
            quantity = Mathf.Max(1, quantityOverride),
            stackable = src.stackable,
            powerScore = src.powerScore,
            attackBonus = src.attackBonus,
            defenseBonus = src.defenseBonus,
            healthBonus = src.healthBonus,
            staminaBonus = src.staminaBonus,
            manaBonus = src.manaBonus,
            moveSpeedBonus = src.moveSpeedBonus,
            healAmount = src.healAmount,
            restoreStaminaAmount = src.restoreStaminaAmount,
            restoreManaAmount = src.restoreManaAmount,
            iconKey = src.iconKey,
            prefabKey = src.prefabKey,
            effectKey = src.effectKey,
            familyKey = src.familyKey,
            generatedAtUnixString = src.generatedAtUnixString
        };
    }

    private static string SanitizeCounterKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }
}
