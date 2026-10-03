using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// note: This modal is a view over canonical inventories; all mutations go through the shared atomic transaction boundary.
public sealed class YQContainerUI : MonoBehaviour
{
    private YQWorldContainer _source;
    private GameObject _player;
    private PlayerState _owner;
    private WorldState _world;
    private GameObject _canvas;
    private RectTransform _storageRows, _playerRows;
    private TMP_Text _title, _status;

    public static bool Open(YQWorldContainer source, GameObject player)
    {
        if (RuntimeModalUiBlocker.IsBlocked) return false;
        YQContainerUI ui = player.GetComponent<YQContainerUI>();
        if (ui == null) ui = player.AddComponent<YQContainerUI>();
        ui._source = source; ui._player = player; ui._owner = PlayerStateManager.Instance?.state; ui._world = WorldStateManager.Instance?.State;
        if (ui._canvas == null) ui.Build();
        ui._canvas.SetActive(true);
        RuntimeModalUiBlocker.Acquire(ui);
        ui.Refresh();
        return true;
    }

    private void Update()
    {
        if (_source == null || _owner != PlayerStateManager.Instance?.state || _world != WorldStateManager.Instance?.State ||
            Vector3.Distance(_player.transform.position, _source.transform.position) > 5f || Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            Close();
    }

    public void Close()
    {
        if (_canvas != null) _canvas.SetActive(false);
        _source = null;
        RuntimeModalUiBlocker.Release(this);
        enabled = false;
    }

    private void OnDisable() { RuntimeModalUiBlocker.Release(this); if (_canvas != null) _canvas.SetActive(false); }
    private void OnDestroy() { RuntimeModalUiBlocker.Release(this); if (_canvas != null) Destroy(_canvas); }

    private void Build()
    {
        _canvas = new GameObject("Container Inventory", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas.transform.SetParent(transform, false);
        _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.GetComponent<Canvas>().sortingOrder = 180;
        CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
        RectTransform panel = Rect("Storage Panel", _canvas.transform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f));
        Image background = panel.gameObject.AddComponent<Image>(); background.color = new Color(0.025f, 0.10f, 0.18f, 0.96f);
        _title = Text(panel, "Storage", new Vector2(0.03f, 0.88f), new Vector2(0.8f, 0.97f), 26);
        Button(panel, "Close", new Vector2(0.82f, 0.89f), new Vector2(0.97f, 0.97f), Close);
        Text(panel, "Stored items — select to take", new Vector2(0.03f, 0.81f), new Vector2(0.48f, 0.88f), 18);
        Text(panel, "Your inventory — select to store", new Vector2(0.52f, 0.81f), new Vector2(0.97f, 0.88f), 18);
        _storageRows = Scroll(panel, "Stored Items", new Vector2(0.03f, 0.15f), new Vector2(0.48f, 0.81f));
        _playerRows = Scroll(panel, "Player Items", new Vector2(0.52f, 0.15f), new Vector2(0.97f, 0.81f));
        _status = Text(panel, "Select an item to move one. Select its stack button to move the stack.", new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.13f), 18);
    }

    private void Refresh()
    {
        enabled = true;
        if (_source == null) { Close(); return; }
        YQContainerRecord record = _source.Record;
        if (!YQContainerInventory.CanAccess(record, _owner, out string failure)) { _status.text = failure; Close(); return; }
        Clear(_storageRows); Clear(_playerRows);
        IYQInventory storage = _source.Inventory, backpack = new YQPlayerInventoryView(_owner);
        _title.text = record.displayName + "  ·  " + storage.Contents.Count + "/" + storage.Capacity + " slots";
        long containerRevision = record.revision, playerRevision = _owner.stateRevision;
        if (record.currency > 0) Row(_storageRows, "Take " + record.currency + " gold", () =>
        {
            YQContainerInventory.TryTakeCurrency(_world, _owner, record.entityId, containerRevision, YQWorldContainer.PublishLive, out string message);
            ShowResult(message); Refresh();
        });
        if (storage.Contents.Count == 0 && record.currency == 0) Row(_storageRows, "Empty", null);
        AddRows(storage, _storageRows, false);
        AddRows(backpack, _playerRows, true);

        void AddRows(IYQInventory inventory, RectTransform rows, bool deposit)
        {
            foreach (InventoryItemRecord item in inventory.Contents)
            {
                bool equipped = deposit && _owner.equippedItemBySlot.ContainsValue(item.itemId);
                bool canDeposit = record.access != YQContainerAccess.CorpseOnly;
                string label = item.displayName + "  ·  " + item.rarity + "  ×" + item.quantity + (equipped ? " (equipped)" : "");
                Action move = equipped || (deposit && !canDeposit) ? null : () => Transfer(item, 1);
                Row(rows, label, move);
                if (item.quantity > 1 && move != null) Row(rows, "Move stack (up to " + Math.Min(item.quantity, YQContainerInventory.MaxStack) + ")", () => Transfer(item, Math.Min(item.quantity, YQContainerInventory.MaxStack)));
            }
            void Transfer(InventoryItemRecord item, int amount)
            {
                bool success = YQContainerInventory.TryTransfer(_world, _owner, record.entityId, deposit, item.itemId, amount,
                    containerRevision, playerRevision, YQWorldContainer.PublishLive, out string message);
                if (success)
                {
                    // note: Gameplay feedback follows committed transfers; quest counters were part of the same atomic transaction.
                    _player.GetComponent<YQPlayerEquipmentVisual>()?.PlayInteractionFeedback(!deposit);
                }
                ShowResult(message); Refresh();
            }
        }
    }

    private void ShowResult(string message) { _status.text = message; GeneratedRpgContentService.Instance?.SetInventoryMessage(message); }
    private static void Clear(RectTransform root)
    {
        // note: Deferred Unity destruction must remove old rows from layout immediately.
        for (int i = root.childCount - 1; i >= 0; i--) { GameObject row = root.GetChild(i).gameObject; row.SetActive(false); Destroy(row); }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TMP_Text Text(Transform parent, string text, Vector2 min, Vector2 max, int size)
    {
        RectTransform rect = Rect("Label", parent, min, max);
        TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size;
        label.color = new Color(0.87f, 0.96f, 1f); label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal; label.raycastTarget = false;
        return label;
    }

    private static void Button(Transform parent, string text, Vector2 min, Vector2 max, Action action)
    {
        RectTransform rect = Rect("Action", parent, min, max); rect.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.28f, 0.43f);
        UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.interactable = action != null; if (action != null) button.onClick.AddListener(() => action());
        Text(rect, text, new Vector2(0.025f, 0.05f), new Vector2(0.975f, 0.95f), 18);
    }

    private static RectTransform Scroll(Transform parent, string name, Vector2 min, Vector2 max)
    {
        RectTransform viewport = Rect(name, parent, min, max); viewport.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.07f, 0.12f, 0.7f);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform rows = Rect("Rows", viewport, new Vector2(0, 1), Vector2.one); rows.pivot = new Vector2(0.5f, 1);
        var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false; layout.childForceExpandWidth = true; layout.spacing = 6;
        rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = rows;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
        return rows;
    }

    private static void Row(RectTransform rows, string label, Action action)
    {
        int previous = rows.childCount;
        Button(rows, label, Vector2.zero, Vector2.one, action);
        var layout = rows.GetChild(previous).gameObject.AddComponent<LayoutElement>(); layout.minHeight = 66; layout.preferredHeight = 66;
    }
}
