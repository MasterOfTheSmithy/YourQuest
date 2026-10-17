// Assets/Assets/Scripts/Tutorial/YQInvestorLootableCorpse.cs
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQInvestorLootableCorpse : MonoBehaviour
{
    public string DisplayName { get; private set; } = "Corpse";
    private InventoryItemRecord _item;
    private int _gold;
    private bool _looted;

    public void Initialize(string displayName, InventoryItemRecord item, int gold)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Corpse" : displayName;
        _item = item;
        _gold = Mathf.Max(0, gold);
    }

    public void TryLoot(GameObject looter)
    {
        if (_looted)
            return;

        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return;

        if (_item != null)
            psm.state.AddOrUpdateItem(_item, true);
        if (_gold > 0)
            psm.state.currency += _gold;

        string message = _item != null ? "Looted " + _item.displayName : "Loot recovered.";
        if (_gold > 0)
            message += " + " + _gold + " gold.";
        GeneratedRpgContentService.Instance?.SetInventoryMessage(message);

        psm.state.AddLedgerLine("The player looted the remains of " + DisplayName + ".");
        psm.state.IncCounter("loot:corpse", 1f);
        psm.Save();

        _looted = true;
        Destroy(gameObject);
    }
}
