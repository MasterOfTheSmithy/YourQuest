// Assets/Assets/Scripts/Tutorial/YourQuestTutorialMenuUI.cs
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YourQuestTutorialMenuUI : MonoBehaviour
{
    public static bool IsOpenNow { get; private set; }

    private enum MenuTab
    {
        Inventory = 0,
        Skills = 1,
        Classes = 2,
        Quests = 3,
        Stats = 4
    }

    private readonly struct SlotDef
    {
        public readonly string SlotId;
        public readonly string Label;
        public readonly Vector2 Position;
        public readonly Vector2 Size;

        public SlotDef(string slotId, string label, float x, float y, float width, float height)
        {
            SlotId = slotId;
            Label = label;
            Position = new Vector2(x, y);
            Size = new Vector2(width, height);
        }
    }

    private const string ModalToken = "YourQuestTutorialMenuUI";

    private static readonly string[] TabLabels =
    {
        "Inventory",
        "Skills",
        "Classes",
        "Quests",
        "Stats"
    };

    private static readonly SlotDef[] EquipmentSlots =
    {
        new SlotDef("head", "Headpiece", 0f, -18f, 160f, 46f),
        new SlotDef("necklace", "Necklace", 0f, -72f, 170f, 38f),
        new SlotDef("chest", "Chest Piece", 0f, -122f, 190f, 60f),
        new SlotDef("offhand", "Left Hand", -176f, -136f, 130f, 62f),
        new SlotDef("weapon", "Right Hand", 176f, -136f, 130f, 62f),
        new SlotDef("gloves", "Gauntlets", 0f, -202f, 170f, 46f),
        new SlotDef("ring_left", "Ring L", -176f, -258f, 120f, 38f),
        new SlotDef("ring_right", "Ring R", 176f, -258f, 120f, 38f),
        new SlotDef("belt", "Belt", 0f, -258f, 170f, 38f),
        new SlotDef("legs", "Pants", 0f, -314f, 170f, 54f),
        new SlotDef("boots", "Boots", 0f, -384f, 170f, 46f),
        new SlotDef("trinket", "Charm", 0f, -444f, 170f, 40f)
    };

    private Canvas _canvas;
    private TMP_Text _titleText;
    private TMP_Text _subtitleText;
    private TMP_Text _detailTitleText;
    private TMP_Text _detailBodyText;
    private RawImage _detailIconImage;
    private Image _detailIconFrame;
    private TMP_Text _footerText;
    private TMP_Text _offerTitleText;
    private TMP_Text _offerBodyText;
    private RectTransform _equipmentPanel;
    private RectTransform _equipmentContent;
    private RectTransform _listContent;
    private RectTransform _offerPanel;
    private ScrollRect _listScroll;
    private ScrollRect _detailScroll;
    private readonly List<Button> _tabButtons = new List<Button>();
    private Button _primaryButton;
    private TMP_Text _primaryButtonText;
    private Button _secondaryButton;
    private TMP_Text _secondaryButtonText;
    private Button _acceptOfferButton;
    private Button _declineOfferButton;

    private MenuTab _activeTab;
    private bool _open;
    private bool _dirty = true;
    private int _lastStateHash = int.MinValue;
    private float _nextPollTime;
    private string _selectedKey = string.Empty;
    private string _statusMessage = string.Empty;

    private void Awake()
    {
        BuildUi();
        SetOpen(false);
    }


    public void ForceCloseFromBootstrap()
    {
        _open = false;
        IsOpenNow = false;
        _dirty = true;
        if (_canvas != null)
            _canvas.enabled = false;
        RuntimeModalUiBlocker.Release(ModalToken);
        RuntimeModalUiBlocker.SetMenuOpen(false);
    }

    private void OnDestroy()
    {
        if (_open)
        {
            RuntimeModalUiBlocker.Release(ModalToken);
            RuntimeModalUiBlocker.SetMenuOpen(false);
        }
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (!_open)
        {
            if (!RuntimeModalUiBlocker.IsBlocked && kb.tabKey.wasPressedThisFrame)
                SetOpen(true);
            return;
        }

        if (kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
        {
            SetOpen(false);
            return;
        }

        if (Time.unscaledTime >= _nextPollTime)
        {
            _nextPollTime = Time.unscaledTime + 0.35f;
            int hash = ComputeStateHash();
            if (hash != _lastStateHash)
            {
                _lastStateHash = hash;
                _dirty = true;
            }
        }

        if (_dirty)
            Render();
    }

    private void SetOpen(bool value)
    {
        if (_open == value && _canvas != null && _canvas.enabled == value)
            return;

        _open = value;
        IsOpenNow = value;
        if (_canvas != null)
            _canvas.enabled = value;

        if (value)
        {
            RuntimeModalUiBlocker.Acquire(ModalToken);
            RuntimeModalUiBlocker.SetMenuOpen(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _lastStateHash = int.MinValue;
            _dirty = true;
            Render();
        }
        else
        {
            RuntimeModalUiBlocker.Release(ModalToken);
            RuntimeModalUiBlocker.SetMenuOpen(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void MarkDirty(bool immediate = false)
    {
        _dirty = true;
        _lastStateHash = int.MinValue;
        if (immediate && _open)
            Render();
    }

    private int ComputeStateHash()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return 0;

        PlayerState state = psm.state;
        WorldState world = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (int)_activeTab;
            hash = hash * 31 + (state.level);
            hash = hash * 31 + state.xp;
            hash = hash * 31 + state.currency;
            hash = hash * 31 + state.inventoryItems.Count;
            hash = hash * 31 + state.skills.Count;
            hash = hash * 31 + state.classes.Count;
            hash = hash * 31 + state.titles.Count;
            hash = hash * 31 + state.quests.Count;
            hash = hash * 31 + (state.activeQuestId ?? string.Empty).GetHashCode();
            hash = hash * 31 + state.GetPendingOfferCount();
            hash = hash * 31 + state.behaviorLedger.Count;
            hash = hash * 31 + (state.currentRegionId ?? string.Empty).GetHashCode();
            hash = hash * 31 + (state.GetEquippedItem("weapon")?.itemId ?? string.Empty).GetHashCode();
            hash = hash * 31 + (state.GetEquippedItem("chest")?.itemId ?? string.Empty).GetHashCode();
            hash = hash * 31 + (state.GetEquippedItem("ring_left")?.itemId ?? string.Empty).GetHashCode();
            hash = hash * 31 + (state.equippedSkillBySlot.TryGetValue("active", out string active) ? active : string.Empty).GetHashCode();
            hash = hash * 31 + (state.equippedSkillBySlot.TryGetValue("spell", out string spell) ? spell : string.Empty).GetHashCode();
            if (world != null)
            {
                hash = hash * 31 + world.factions.Count;
                hash = hash * 31 + world.locations.Count;
                hash = hash * 31 + world.npcs.Count;
                hash = hash * 31 + world.currentRegionId.GetHashCode();
            }
            return hash;
        }
    }

    private void SetTab(MenuTab tab)
    {
        if (_activeTab == tab)
            return;

        _activeTab = tab;
        _selectedKey = string.Empty;
        _statusMessage = string.Empty;
        MarkDirty(true);
    }

    private void Render()
    {
        _dirty = false;

        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return;

        PlayerState state = psm.state;
        state.EnsureCollections();
        WorldState world = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        GeneratedRpgContentService content = GeneratedRpgContentService.Instance;

        _titleText.text = TabLabels[(int)_activeTab];
        _subtitleText.text = BuildSubtitle(state, world);
        _offerPanel.gameObject.SetActive(_activeTab == MenuTab.Inventory && state.GetActiveOffer() != null);

        RebuildEquipmentSection(state);
        RebuildList(state, world, content);
        RebuildDetails(state, world, content);
        RebuildOfferPanel(state);
        UpdateActionButtons(state);
        UpdateTabVisuals();
        _footerText.text = string.IsNullOrWhiteSpace(_statusMessage) ? BuildFooter(state, world) : _statusMessage;
    }

    private void RebuildEquipmentSection(PlayerState state)
    {
        ClearChildren(_equipmentContent);
        CreateEquipmentGuide(_equipmentContent);

        for (int i = 0; i < EquipmentSlots.Length; i++)
        {
            SlotDef slot = EquipmentSlots[i];
            InventoryItemRecord equipped = state.GetEquippedItem(slot.SlotId);
            string key = "slot:" + slot.SlotId;
            Button button = CreateEquipmentSlotButton(_equipmentContent, slot, equipped, equipped != null ? Trim(equipped.displayName, 20) : "Empty");
            SetButtonVisual(button, string.Equals(_selectedKey, key, StringComparison.OrdinalIgnoreCase));
            button.onClick.AddListener(() =>
            {
                if (_activeTab != MenuTab.Inventory)
                    _activeTab = MenuTab.Inventory;
                _selectedKey = key;
                MarkDirty(true);
            });
        }
    }

    private void RebuildList(PlayerState state, WorldState world, GeneratedRpgContentService content)
    {
        ClearChildren(_listContent);
        switch (_activeTab)
        {
            case MenuTab.Inventory:
                BuildInventoryList(state);
                break;
            case MenuTab.Skills:
                BuildSkillsList(state);
                break;
            case MenuTab.Classes:
                BuildClassesList(state);
                break;
            case MenuTab.Quests:
                BuildQuestsList(state);
                break;
            case MenuTab.Stats:
                BuildStatsList(state, world, content);
                break;
        }
    }

    private void BuildInventoryList(PlayerState state)
    {
        AddSectionHeader(_listContent, "Carried Items");
        InventoryItemRecord first = null;
        for (int i = 0; i < state.inventoryItems.Count; i++)
        {
            InventoryItemRecord item = state.inventoryItems[i];
            if (item == null)
                continue;
            if (first == null)
                first = item;

            string key = "item:" + item.itemId;
            string subtitle = item.IsConsumable
                ? ToTitle(item.itemType) + "  •  Qty " + Mathf.Max(1, item.quantity)
                : FormatSlotName(item.equipSlot) + "  •  " + ToTitle(item.rarity) + "  •  PWR " + item.powerScore;

            AddInventoryListButton(item, subtitle, key, () =>
            {
                _selectedKey = key;
                MarkDirty(true);
            });
        }

        if (state.inventoryItems.Count == 0)
            AddEmptyState("No carried items.");

        if (string.IsNullOrWhiteSpace(_selectedKey) && first != null)
            _selectedKey = "item:" + first.itemId;
    }

    private void BuildSkillsList(PlayerState state)
    {
        AddSectionHeader(_listContent, "Skills & Spells");
        SkillRecord first = null;
        for (int i = 0; i < state.skills.Count; i++)
        {
            SkillRecord skill = state.skills[i];
            if (skill == null)
                continue;
            if (first == null)
                first = skill;

            string key = "skill:" + skill.skillId;
            string subtitle = (skill.isSpell ? "Spell" : "Skill") + "  •  Tier " + Mathf.Max(1, skill.tier) + "  •  Rank " + Mathf.Max(1, skill.rank);
            AddListButton(skill.name, subtitle, key, () =>
            {
                _selectedKey = key;
                MarkDirty(true);
            });
        }

        if (state.skills.Count == 0)
            AddEmptyState("No skills learned yet.");
        if (string.IsNullOrWhiteSpace(_selectedKey) && first != null)
            _selectedKey = "skill:" + first.skillId;
    }

    private void BuildClassesList(PlayerState state)
    {
        AddSectionHeader(_listContent, "Classes");
        ClassRecord firstClass = null;
        for (int i = 0; i < state.classes.Count; i++)
        {
            ClassRecord record = state.classes[i];
            if (record == null)
                continue;
            if (firstClass == null)
                firstClass = record;
            string key = "class:" + record.classId;
            AddListButton(record.name, "Class", key, () =>
            {
                _selectedKey = key;
                MarkDirty(true);
            });
        }

        AddSectionHeader(_listContent, "Titles");
        TitleRecord firstTitle = null;
        for (int i = 0; i < state.titles.Count; i++)
        {
            TitleRecord record = state.titles[i];
            if (record == null)
                continue;
            if (firstTitle == null)
                firstTitle = record;
            string key = "title:" + record.titleId;
            AddListButton(record.name, "Title", key, () =>
            {
                _selectedKey = key;
                MarkDirty(true);
            });
        }

        if (state.classes.Count == 0 && state.titles.Count == 0)
            AddEmptyState("No class or title identities recorded yet.");
        if (string.IsNullOrWhiteSpace(_selectedKey))
        {
            if (firstClass != null) _selectedKey = "class:" + firstClass.classId;
            else if (firstTitle != null) _selectedKey = "title:" + firstTitle.titleId;
        }
    }

    private void BuildQuestsList(PlayerState state)
    {
        AddSectionHeader(_listContent, "Quest Log");
        QuestRecord first = state.GetActiveQuest();
        for (int i = 0; i < state.quests.Count; i++)
        {
            QuestRecord quest = state.quests[i];
            if (quest == null)
                continue;
            if (first == null)
                first = quest;
            string key = "quest:" + quest.questId;
            bool active = string.Equals(quest.questId, state.activeQuestId, StringComparison.OrdinalIgnoreCase);
            string subtitle = (active ? "Active  •  " : string.Empty) + ToTitle(quest.status);
            AddListButton(quest.name, subtitle, key, () =>
            {
                _selectedKey = key;
                MarkDirty(true);
            });
        }

        if (state.quests.Count == 0)
            AddEmptyState("No quests in log.");
        if (string.IsNullOrWhiteSpace(_selectedKey) && first != null)
            _selectedKey = "quest:" + first.questId;
    }

    private void BuildStatsList(PlayerState state, WorldState world, GeneratedRpgContentService content)
    {
        AddSectionHeader(_listContent, "Overview");
        AddListButton("Core Stats", "Base and derived combat values", "stats:core", () => { _selectedKey = "stats:core"; MarkDirty(true); });
        AddListButton("Loadout", "Equipped items and skills", "stats:loadout", () => { _selectedKey = "stats:loadout"; MarkDirty(true); });
        AddListButton("World State", "Region, tension, canon, factions", "stats:world", () => { _selectedKey = "stats:world"; MarkDirty(true); });
        AddListButton("Activity", "Behavior ledger and counters", "stats:activity", () => { _selectedKey = "stats:activity"; MarkDirty(true); });
        if (string.IsNullOrWhiteSpace(_selectedKey))
            _selectedKey = "stats:core";
    }

    private void RebuildDetails(PlayerState state, WorldState world, GeneratedRpgContentService content)
    {
        // note: Non-inventory tabs intentionally clear any previously selected item icon before writing their detail text.
        SetDetailIcon(null);

        switch (_activeTab)
        {
            case MenuTab.Inventory:
                BuildInventoryDetail(state);
                break;
            case MenuTab.Skills:
                BuildSkillDetail(state);
                break;
            case MenuTab.Classes:
                BuildClassDetail(state);
                break;
            case MenuTab.Quests:
                BuildQuestDetail(state);
                break;
            case MenuTab.Stats:
                BuildStatsDetail(state, world, content);
                break;
        }

        if (_detailScroll != null)
            _detailScroll.verticalNormalizedPosition = 1f;
    }

    private void BuildInventoryDetail(PlayerState state)
    {
        if (TrySelectedSlot(out string slotId))
        {
            InventoryItemRecord equipped = state.GetEquippedItem(slotId);
            _detailTitleText.text = FormatSlotName(slotId);
            StringBuilder sb = new StringBuilder(512);
            if (equipped == null)
            {
                SetDetailIcon(null);
                sb.AppendLine("Nothing equipped.");
                sb.AppendLine();
                sb.Append("Select a carried item with a matching slot to equip it.");
            }
            else
            {
                SetDetailIcon(equipped);
                sb.AppendLine("Equipped  " + equipped.displayName);
                sb.AppendLine("Type  " + ToTitle(equipped.itemType));
                sb.AppendLine("Rarity  " + ToTitle(equipped.rarity));
                AppendItemDetail(sb, equipped);
            }
            _detailBodyText.text = sb.ToString();
            return;
        }

        InventoryItemRecord item = GetSelectedItem(state);
        if (item == null)
        {
            SetDetailIcon(null);
            _detailTitleText.text = "Inventory";
            _detailBodyText.text = "Select an equipment slot or carried item to inspect it.";
            return;
        }

        SetDetailIcon(item);
        _detailTitleText.text = item.displayName;
        StringBuilder body = new StringBuilder(640);
        body.AppendLine("Type  " + ToTitle(item.itemType));
        if (!string.IsNullOrWhiteSpace(item.equipSlot))
            body.AppendLine("Slot  " + FormatSlotName(item.equipSlot));
        if (!string.IsNullOrWhiteSpace(item.rarity))
            body.AppendLine("Rarity  " + ToTitle(item.rarity));
        body.AppendLine("Quantity  " + Mathf.Max(1, item.quantity));
        body.AppendLine("Power  " + item.powerScore);
        AppendItemDetail(body, item);
        _detailBodyText.text = body.ToString();
    }

    private void BuildSkillDetail(PlayerState state)
    {
        SkillRecord selected = GetSelectedSkill(state);
        if (selected == null)
        {
            _detailTitleText.text = "Skills";
            _detailBodyText.text = "Learned skills and spells appear here.";
            return;
        }

        _detailTitleText.text = selected.name;
        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("Category  " + (selected.isSpell ? "Spell" : "Skill"));
        sb.AppendLine("Type  " + ToTitle(selected.type));
        sb.AppendLine("Tier  " + Mathf.Max(1, selected.tier));
        sb.AppendLine("Rank  " + Mathf.Max(1, selected.rank));
        if (!string.IsNullOrWhiteSpace(selected.context)) sb.AppendLine("Context  " + ToTitle(selected.context));
        if (!string.IsNullOrWhiteSpace(selected.environment)) sb.AppendLine("Environment  " + ToTitle(selected.environment));
        sb.AppendLine();
        sb.AppendLine(Safe(selected.description, "No description."));
        sb.AppendLine();
        sb.Append("Equipped Active  " + ResolveEquippedSkillName(state, "active") + "   •   Equipped Spell  " + ResolveEquippedSkillName(state, "spell"));
        _detailBodyText.text = sb.ToString();
    }

    private void BuildClassDetail(PlayerState state)
    {
        if (_selectedKey.StartsWith("class:", StringComparison.OrdinalIgnoreCase))
        {
            string id = _selectedKey.Substring(6);
            for (int i = 0; i < state.classes.Count; i++)
            {
                ClassRecord record = state.classes[i];
                if (record != null && string.Equals(record.classId, id, StringComparison.OrdinalIgnoreCase))
                {
                    _detailTitleText.text = record.name;
                    _detailBodyText.text = "Identity  Class\n\n" + Safe(record.description, "No description.");
                    return;
                }
            }
        }

        if (_selectedKey.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
        {
            string id = _selectedKey.Substring(6);
            for (int i = 0; i < state.titles.Count; i++)
            {
                TitleRecord record = state.titles[i];
                if (record != null && string.Equals(record.titleId, id, StringComparison.OrdinalIgnoreCase))
                {
                    _detailTitleText.text = record.name;
                    _detailBodyText.text = "Identity  Title\n\n" + Safe(record.description, "No description.");
                    return;
                }
            }
        }

        _detailTitleText.text = "Identity";
        _detailBodyText.text = "Select a class or title to inspect it.";
    }

    private void BuildQuestDetail(PlayerState state)
    {
        QuestRecord quest = GetSelectedQuest(state);
        if (quest == null)
        {
            _detailTitleText.text = "Quests";
            _detailBodyText.text = "Select a quest to inspect it.";
            return;
        }

        _detailTitleText.text = quest.name;
        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("Tracking  " + (string.Equals(quest.questId, state.activeQuestId, StringComparison.OrdinalIgnoreCase) ? "Active" : "Not active"));
        sb.AppendLine("Status  " + ToTitle(quest.status));
        if (quest.tags != null && quest.tags.Length > 0)
            sb.AppendLine("Tags  " + string.Join(", ", quest.tags));
        sb.AppendLine();
        sb.AppendLine(Safe(quest.description, "No description."));
        _detailBodyText.text = sb.ToString();
    }

    private void BuildStatsDetail(PlayerState state, WorldState world, GeneratedRpgContentService content)
    {
        if (string.IsNullOrWhiteSpace(_selectedKey))
            _selectedKey = "stats:core";

        _detailTitleText.text = "Stats";
        StringBuilder sb = new StringBuilder(1024);
        switch (_selectedKey)
        {
            case "stats:loadout":
                _detailTitleText.text = "Loadout";
                sb.AppendLine("Equipped Items");
                for (int i = 0; i < EquipmentSlots.Length; i++)
                {
                    SlotDef slot = EquipmentSlots[i];
                    sb.AppendLine(slot.Label + "  " + DescribeItem(state.GetEquippedItem(slot.SlotId)));
                }
                sb.AppendLine();
                sb.AppendLine("Equipped Skills");
                sb.AppendLine("Active  " + ResolveEquippedSkillName(state, "active"));
                sb.AppendLine("Spell  " + ResolveEquippedSkillName(state, "spell"));
                break;

            case "stats:world":
                _detailTitleText.text = "World State";
                sb.AppendLine("Region  " + Safe(state.currentRegionName, "Unknown") + " (" + Safe(state.currentRegionId, "region_unknown") + ")");
                if (world != null)
                {
                    sb.AppendLine("Tension  " + world.tension.ToString("0.00"));
                    sb.AppendLine("Factions  " + world.factions.Count);
                    sb.AppendLine("Locations  " + world.locations.Count);
                    sb.AppendLine("NPC Records  " + world.npcs.Count);
                    sb.AppendLine();
                    sb.AppendLine("Canon");
                    List<string> canon = world.GetCanonLines();
                    for (int i = Mathf.Max(0, canon.Count - 6); i < canon.Count; i++)
                        sb.AppendLine("• " + canon[i]);
                }
                break;

            case "stats:activity":
                _detailTitleText.text = "Activity";
                sb.AppendLine("Ledger");
                for (int i = Mathf.Max(0, state.behaviorLedger.Count - 12); i < state.behaviorLedger.Count; i++)
                    sb.AppendLine("• " + state.behaviorLedger[i]);
                sb.AppendLine();
                sb.AppendLine("Counters");
                foreach (KeyValuePair<string, float> kvp in state.behaviorCounters)
                    sb.AppendLine(kvp.Key + "  " + kvp.Value.ToString("0.##"));
                break;

            default:
                _detailTitleText.text = "Core Stats";
                int maxHealth = content != null ? content.GetDerivedMaxHealth(state) : state.stats.maxHealth;
                int maxStamina = content != null ? content.GetDerivedMaxStamina(state) : state.stats.maxStamina;
                int maxMana = content != null ? content.GetDerivedMaxMana(state) : state.stats.maxMana;
                sb.AppendLine("Level  " + state.level);
                sb.AppendLine("XP  " + state.xp + " / " + Mathf.Max(1, state.xp + state.xpToNext));
                sb.AppendLine("Attack  " + state.stats.attack + "   •   Derived  " + (state.stats.attack + (content != null ? content.GetAttackBonus(state) : 0)));
                sb.AppendLine("Defense  " + state.stats.defense + "   •   Derived  " + (state.stats.defense + (content != null ? content.GetDefenseBonus(state) : 0)));
                sb.AppendLine("Max Health  " + maxHealth);
                sb.AppendLine("Max Stamina  " + maxStamina);
                sb.AppendLine("Max Mana  " + maxMana);
                sb.AppendLine("Move Speed  " + (content != null ? content.GetMoveSpeedBonus(state) + state.stats.moveSpeed : state.stats.moveSpeed).ToString("0.00"));
                sb.AppendLine("Crit  " + state.stats.critChance.ToString("0.00"));
                sb.AppendLine();
                sb.AppendLine("Inventory  " + state.inventoryItems.Count + " items");
                sb.AppendLine("Skills  " + state.skills.Count + "   •   Classes  " + state.classes.Count + "   •   Titles  " + state.titles.Count + "   •   Quests  " + state.quests.Count);
                break;
        }
        _detailBodyText.text = sb.ToString();
    }

    private void RebuildOfferPanel(PlayerState state)
    {
        PendingProgressionOfferRecord offer = state.GetActiveOffer();
        bool visible = _activeTab == MenuTab.Inventory && offer != null;
        _offerPanel.gameObject.SetActive(visible);
        if (!visible)
            return;

        _offerTitleText.text = (offer.isUpgrade ? "Upgrade" : "Offer") + " • " + ToTitle(offer.offerKind) + " • " + offer.name;
        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine(Safe(offer.description, "No description."));
        sb.AppendLine();
        sb.Append("Confidence " + offer.confidence.ToString("0.00"));
        if (offer.proposedTier > 0)
            sb.Append("   •   Tier T" + offer.proposedTier);
        _offerBodyText.text = sb.ToString();
    }

    private void UpdateActionButtons(PlayerState state)
    {
        bool showPrimary = false;
        bool showSecondary = false;
        string primary = string.Empty;
        string secondary = string.Empty;

        if (_activeTab == MenuTab.Inventory)
        {
            if (TrySelectedSlot(out _))
            {
                showPrimary = true;
                primary = "Equip Best";
                showSecondary = state.GetEquippedItem(GetSelectedSlotId()) != null;
                secondary = "Clear Slot";
            }
            else
            {
                InventoryItemRecord item = GetSelectedItem(state);
                if (item != null)
                {
                    if (item.IsEquippable)
                    {
                        showPrimary = true;
                        primary = "Equip";
                    }
                    else if (item.IsConsumable)
                    {
                        showPrimary = true;
                        primary = "Use";
                    }
                }
            }
        }
        else if (_activeTab == MenuTab.Skills)
        {
            SkillRecord skill = GetSelectedSkill(state);
            if (skill != null)
            {
                showPrimary = true;
                primary = skill.isSpell ? "Equip Spell" : "Equip Active";
            }
        }
        else if (_activeTab == MenuTab.Quests)
        {
            QuestRecord quest = GetSelectedQuest(state);
            if (quest != null && !IsClosedQuest(quest) && !string.Equals(quest.questId, state.activeQuestId, StringComparison.OrdinalIgnoreCase))
            {
                showPrimary = true;
                primary = "Track Quest";
            }
        }

        _primaryButton.gameObject.SetActive(showPrimary);
        _secondaryButton.gameObject.SetActive(showSecondary);
        _primaryButtonText.text = primary;
        _secondaryButtonText.text = secondary;
    }

    private void OnPrimaryActionClicked()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return;

        PlayerState state = psm.state;
        GeneratedRpgContentService content = GeneratedRpgContentService.Instance;
        _statusMessage = string.Empty;

        if (_activeTab == MenuTab.Inventory)
        {
            if (TrySelectedSlot(out string slotId))
            {
                InventoryItemRecord best = FindBestForSlot(state, slotId);
                if (best != null && state.TryEquipItem(best.itemId, out string message))
                {
                    content?.SetInventoryMessage(message);
                    Persist();
                    _statusMessage = message;
                }
                else
                {
                    _statusMessage = "No compatible item found for that slot.";
                }
            }
            else
            {
                InventoryItemRecord item = GetSelectedItem(state);
                if (item != null)
                {
                    if (item.IsEquippable)
                    {
                        if (state.TryEquipItem(item.itemId, out string message))
                        {
                            content?.SetInventoryMessage(message);
                            Persist();
                            _statusMessage = message;
                        }
                    }
                    else if (item.IsConsumable)
                    {
                        if (content != null)
                        {
                            content.UseSpecificConsumable(item.itemId);
                            Persist();
                            _statusMessage = Safe(content.LastInventoryMessage, "Used item.");
                        }
                    }
                }
            }
        }
        else if (_activeTab == MenuTab.Skills)
        {
            SkillRecord skill = GetSelectedSkill(state);
            if (skill != null)
            {
                state.equippedSkillBySlot[skill.isSpell ? "spell" : "active"] = skill.skillId;
                Persist();
                _statusMessage = "Equipped " + skill.name + ".";
            }
        }
        else if (_activeTab == MenuTab.Quests)
        {
            QuestRecord quest = GetSelectedQuest(state);
            if (quest != null && state.SetActiveQuest(quest.questId))
            {
                Persist();
                _statusMessage = "Tracking " + quest.name + ".";
            }
        }

        MarkDirty(true);
    }

    private void OnSecondaryActionClicked()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return;

        if (_activeTab == MenuTab.Inventory && TrySelectedSlot(out string slotId))
        {
            string removedItemId = psm.state.equippedItemBySlot.TryGetValue(slotId, out string equippedId) ? equippedId : string.Empty;
            psm.state.equippedItemBySlot.Remove(slotId);
            if ((string.Equals(slotId, "weapon", StringComparison.OrdinalIgnoreCase) || string.Equals(slotId, "offhand", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(removedItemId))
            {
                string pairedSlot = string.Equals(slotId, "weapon", StringComparison.OrdinalIgnoreCase) ? "offhand" : "weapon";
                if (psm.state.equippedItemBySlot.TryGetValue(pairedSlot, out string pairedItemId) &&
                    string.Equals(pairedItemId, removedItemId, StringComparison.OrdinalIgnoreCase))
                {
                    psm.state.equippedItemBySlot.Remove(pairedSlot);
                }
            }
            Persist();
            _statusMessage = "Cleared " + FormatSlotName(slotId) + ".";
            MarkDirty(true);
        }
    }

    private void OnAcceptOfferClicked()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return;

        PendingProgressionOfferRecord offer = psm.state.GetActiveOffer();
        if (offer == null)
            return;

        string message;
        if (psm.state.AcceptOffer(offer.offerId, out message))
        {
            GeneratedRpgContentService.Instance?.SetInventoryMessage(message);
            Persist();
            _statusMessage = message;
            MarkDirty(true);
        }
    }

    private void OnDeclineOfferClicked()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return;

        PendingProgressionOfferRecord offer = psm.state.GetActiveOffer();
        if (offer == null)
            return;

        string message;
        if (psm.state.DeclineOffer(offer.offerId, out message))
        {
            GeneratedRpgContentService.Instance?.SetInventoryMessage(message);
            Persist();
            _statusMessage = message;
            MarkDirty(true);
        }
    }

    private void Persist()
    {
        PlayerStateManager.Instance?.Save();
        WorldStateManager.Instance?.Save();
    }

    private InventoryItemRecord GetSelectedItem(PlayerState state)
    {
        if (!_selectedKey.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
            return null;
        return state.FindInventoryItemById(_selectedKey.Substring(5));
    }

    private SkillRecord GetSelectedSkill(PlayerState state)
    {
        if (!_selectedKey.StartsWith("skill:", StringComparison.OrdinalIgnoreCase))
            return null;
        return state.FindSkillById(_selectedKey.Substring(6));
    }

    private QuestRecord GetSelectedQuest(PlayerState state)
    {
        if (!_selectedKey.StartsWith("quest:", StringComparison.OrdinalIgnoreCase))
            return null;
        string id = _selectedKey.Substring(6);
        for (int i = 0; i < state.quests.Count; i++)
        {
            QuestRecord quest = state.quests[i];
            if (quest != null && string.Equals(quest.questId, id, StringComparison.OrdinalIgnoreCase))
                return quest;
        }
        return null;
    }

    private bool TrySelectedSlot(out string slotId)
    {
        slotId = string.Empty;
        if (!_selectedKey.StartsWith("slot:", StringComparison.OrdinalIgnoreCase))
            return false;
        slotId = _selectedKey.Substring(5);
        return !string.IsNullOrWhiteSpace(slotId);
    }

    private string GetSelectedSlotId()
    {
        TrySelectedSlot(out string slotId);
        return slotId;
    }

    private InventoryItemRecord FindBestForSlot(PlayerState state, string slotId)
    {
        InventoryItemRecord best = null;
        int bestPower = int.MinValue;
        for (int i = 0; i < state.inventoryItems.Count; i++)
        {
            InventoryItemRecord item = state.inventoryItems[i];
            if (item == null || !item.IsEquippable)
                continue;
            if (!string.Equals(item.equipSlot, slotId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (item.powerScore > bestPower)
            {
                best = item;
                bestPower = item.powerScore;
            }
        }
        return best;
    }

    private string ResolveEquippedSkillName(PlayerState state, string slot)
    {
        if (state.equippedSkillBySlot == null || !state.equippedSkillBySlot.TryGetValue(slot, out string skillId))
            return "<none>";
        SkillRecord skill = state.FindSkillById(skillId);
        return skill != null ? skill.name : skillId;
    }

    private void AppendItemDetail(StringBuilder sb, InventoryItemRecord item)
    {
        if (item == null)
            return;
        if (item.attackBonus != 0) sb.AppendLine("Attack  " + item.attackBonus);
        if (item.defenseBonus != 0) sb.AppendLine("Defense  " + item.defenseBonus);
        if (item.healthBonus != 0) sb.AppendLine("Health  " + item.healthBonus);
        if (item.staminaBonus != 0) sb.AppendLine("Stamina  " + item.staminaBonus);
        if (item.manaBonus != 0) sb.AppendLine("Mana  " + item.manaBonus);
        if (Mathf.Abs(item.moveSpeedBonus) > 0.001f) sb.AppendLine("Move  " + item.moveSpeedBonus.ToString("+0.##;-0.##;0"));
        if (item.healAmount > 0) sb.AppendLine("Heal  " + item.healAmount);
        if (item.restoreStaminaAmount > 0) sb.AppendLine("Restore Stamina  " + item.restoreStaminaAmount);
        if (item.restoreManaAmount > 0) sb.AppendLine("Restore Mana  " + item.restoreManaAmount);
        sb.AppendLine();
        sb.Append(Safe(item.description, "No description."));
    }

    private string BuildSubtitle(PlayerState state, WorldState world)
    {
        return "Lvl " + state.level + "   •   Gold " + state.currency + "   •   Region " + Safe(state.currentRegionName, "Unknown") +
               (world != null ? "   •   Tension " + world.tension.ToString("0.00") : string.Empty);
    }

    private string BuildFooter(PlayerState state, WorldState world)
    {
        return "Tab open  •  Esc close  •  Pending Offers " + state.GetPendingOfferCount() +
               "  •  Equipped " + CountEquipped(state) + "/" + EquipmentSlots.Length +
               "  •  Active Quest " + Safe(state.GetActiveQuest()?.name, "<none>");
    }

    private static int CountEquipped(PlayerState state)
    {
        int count = 0;
        for (int i = 0; i < EquipmentSlots.Length; i++)
        {
            if (state.GetEquippedItem(EquipmentSlots[i].SlotId) != null)
                count++;
        }
        return count;
    }

    private static int CountByQuestStatus(PlayerState state, string status)
    {
        int count = 0;
        for (int i = 0; i < state.quests.Count; i++)
        {
            QuestRecord quest = state.quests[i];
            if (quest != null && string.Equals(quest.status, status, StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    private static bool IsClosedQuest(QuestRecord quest)
    {
        if (quest == null)
            return true;

        string status = quest.status ?? string.Empty;
        return status.Equals("complete", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("completed", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("failed", StringComparison.OrdinalIgnoreCase);
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("YourQuestTutorialMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        YQUITheme.ApplyCanvasScaler(scaler);

        RectTransform root = CreatePanel(canvasGo.transform, "Root", new Vector2(0.5f, 0.5f), new Vector2(1840f, 900f), Vector2.zero, YQUITheme.PanelSolid);
        AddOutline(root, new Color(0.68f, 0.61f, 0.42f, 0.42f));

        RectTransform header = CreatePanel(root, "Header", new Vector2(0.5f, 1f), new Vector2(1740f, 78f), new Vector2(0f, -10f), YQUITheme.PanelSoft);
        _titleText = CreateAbsoluteText(header, "Title", 28f, FontStyles.Bold, new Vector2(16f, -12f), new Vector2(900f, 30f));
        _titleText.color = YQUITheme.Gold;
        _subtitleText = CreateAbsoluteText(header, "Subtitle", 15f, FontStyles.Normal, new Vector2(18f, -44f), new Vector2(1100f, 24f));
        _subtitleText.color = YQUITheme.Muted;

        RectTransform tabs = CreatePanel(root, "Tabs", new Vector2(0f, 0.5f), new Vector2(180f, 760f), new Vector2(16f, -20f), YQUITheme.Panel);
        VerticalLayoutGroup tabsLayout = tabs.gameObject.AddComponent<VerticalLayoutGroup>();
        tabsLayout.padding = new RectOffset(10, 10, 16, 16);
        tabsLayout.spacing = 8f;
        tabsLayout.childControlHeight = false;
        tabsLayout.childControlWidth = true;
        tabsLayout.childForceExpandHeight = false;
        tabsLayout.childForceExpandWidth = true;

        for (int i = 0; i < TabLabels.Length; i++)
        {
            int idx = i;
            Button button = CreateNavButton(tabs, TabLabels[i]);
            button.onClick.AddListener(() => SetTab((MenuTab)idx));
            _tabButtons.Add(button);
        }

        _equipmentPanel = CreatePanel(root, "Equipment", new Vector2(0f, 0f), new Vector2(500f, 760f), new Vector2(212f, 82f), YQUITheme.Panel);
        AddOutline(_equipmentPanel, new Color(0f, 0f, 0f, 0.24f));
        VerticalLayoutGroup equipmentLayoutGroup = _equipmentPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        equipmentLayoutGroup.padding = new RectOffset(14, 14, 14, 14);
        equipmentLayoutGroup.spacing = 10f;
        equipmentLayoutGroup.childControlHeight = true;
        equipmentLayoutGroup.childControlWidth = true;
        equipmentLayoutGroup.childForceExpandHeight = false;
        equipmentLayoutGroup.childForceExpandWidth = true;
        TMP_Text equipHeader = CreateTextBlock(_equipmentPanel, 17f, FontStyles.Bold, new Color32(255, 240, 184, 255));
        equipHeader.text = "Equipped";
        LayoutElement equipHeaderLayout = equipHeader.gameObject.GetComponent<LayoutElement>();
        equipHeaderLayout.preferredHeight = 28f;
        GameObject equipGridObject = new GameObject("EquipmentBody", typeof(RectTransform), typeof(LayoutElement));
        equipGridObject.transform.SetParent(_equipmentPanel, false);
        LayoutElement equipBodyLayout = equipGridObject.GetComponent<LayoutElement>();
        equipBodyLayout.preferredHeight = 680f;
        equipBodyLayout.flexibleHeight = 1f;
        _equipmentContent = equipGridObject.GetComponent<RectTransform>();

        RectTransform center = CreatePanel(root, "Center", new Vector2(0f, 0f), new Vector2(500f, 760f), new Vector2(728f, 82f), YQUITheme.Panel);
        VerticalLayoutGroup centerLayout = center.gameObject.AddComponent<VerticalLayoutGroup>();
        centerLayout.padding = new RectOffset(14, 14, 14, 14);
        centerLayout.spacing = 12f;
        centerLayout.childControlHeight = true;
        centerLayout.childControlWidth = true;
        centerLayout.childForceExpandHeight = false;
        centerLayout.childForceExpandWidth = true;

        RectTransform listArea = CreatePanel(center, "ListArea", Vector2.zero, new Vector2(0f, 596f), Vector2.zero, new Color(0.03f, 0.04f, 0.05f, 0.70f));
        LayoutElement listLayout = listArea.gameObject.AddComponent<LayoutElement>();
        listLayout.flexibleHeight = 1f;
        _listScroll = BuildScroll(listArea, out _listContent);

        _offerPanel = CreatePanel(center, "OfferPanel", Vector2.zero, new Vector2(0f, 120f), Vector2.zero, YQUITheme.PanelSoft);
        LayoutElement offerLayout = _offerPanel.gameObject.AddComponent<LayoutElement>();
        offerLayout.preferredHeight = 120f;
        AddOutline(_offerPanel, new Color(0.68f, 0.61f, 0.42f, 0.28f));
        _offerTitleText = CreateAbsoluteText(_offerPanel, "OfferTitle", 16f, FontStyles.Bold, new Vector2(12f, -12f), new Vector2(320f, 22f));
        _offerBodyText = CreateAbsoluteText(_offerPanel, "OfferBody", 14f, FontStyles.Normal, new Vector2(12f, -40f), new Vector2(330f, 46f));
        _offerBodyText.textWrappingMode = TextWrappingModes.Normal;
        _acceptOfferButton = CreateButton(_offerPanel, "Accept", new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(110f, 34f), "Accept");
        _declineOfferButton = CreateButton(_offerPanel, "Decline", new Vector2(1f, 1f), new Vector2(-128f, -12f), new Vector2(110f, 34f), "Decline");
        _acceptOfferButton.onClick.AddListener(OnAcceptOfferClicked);
        _declineOfferButton.onClick.AddListener(OnDeclineOfferClicked);

        RectTransform detail = CreatePanel(root, "Detail", new Vector2(1f, 0f), new Vector2(560f, 760f), new Vector2(-16f, 82f), YQUITheme.Panel);
        AddOutline(detail, new Color(0f, 0f, 0f, 0.24f));
        _detailTitleText = CreateAbsoluteText(detail, "DetailTitle", 24f, FontStyles.Bold, new Vector2(16f, -14f), new Vector2(360f, 28f));
        _detailIconFrame = CreateIconFrame(detail, "DetailIconFrame", new Vector2(444f, -14f), new Vector2(84f, 84f));
        _detailIconImage = CreateRawIcon(_detailIconFrame.transform, "DetailIcon", new Vector2(6f, -6f), new Vector2(72f, 72f));
        RectTransform detailScrollRoot = CreatePanel(detail, "DetailScrollRoot", new Vector2(0f, 0f), new Vector2(528f, 588f), new Vector2(16f, 126f), new Color(0.02f, 0.03f, 0.04f, 0.70f));
        _detailScroll = BuildScroll(detailScrollRoot, out RectTransform detailContent);
        _detailBodyText = CreateTextStretch(detailContent, "DetailBody", 16f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _detailBodyText.textWrappingMode = TextWrappingModes.Normal;
        _detailBodyText.overflowMode = TextOverflowModes.Overflow;

        _primaryButton = CreateButton(detail, "PrimaryAction", new Vector2(0f, 0f), new Vector2(16f, 72f), new Vector2(184f, 42f), "Primary");
        _secondaryButton = CreateButton(detail, "SecondaryAction", new Vector2(0f, 0f), new Vector2(210f, 72f), new Vector2(184f, 42f), "Secondary");
        _primaryButton.onClick.AddListener(OnPrimaryActionClicked);
        _secondaryButton.onClick.AddListener(OnSecondaryActionClicked);
        _primaryButtonText = _primaryButton.GetComponentInChildren<TextMeshProUGUI>();
        _secondaryButtonText = _secondaryButton.GetComponentInChildren<TextMeshProUGUI>();

        RectTransform footer = CreatePanel(root, "Footer", new Vector2(0.5f, 0f), new Vector2(1820f, 44f), new Vector2(0f, 12f), YQUITheme.PanelSoft);
        _footerText = CreateAbsoluteText(footer, "FooterText", 14f, FontStyles.Normal, new Vector2(16f, -11f), new Vector2(1780f, 20f));
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static void AddOutline(RectTransform rt, Color color)
    {
        Outline outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private static TMP_Text CreateAbsoluteText(Transform parent, string name, float size, FontStyles style, Vector2 anchoredPosition, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = dimensions;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.TopLeft;
        YQUITheme.ApplyText(text);
        return text;
    }

    private static TMP_Text CreateTextBlock(Transform parent, float size, FontStyles style, Color color)
    {
        GameObject go = new GameObject("TextBlock", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = color;
        YQUITheme.ApplyText(text, color);
        return text;
    }

    private static TMP_Text CreateTextStretch(Transform parent, string name, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        YQUITheme.ApplyText(text);
        ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return text;
    }

    private Button CreateNavButton(Transform parent, string label)
    {
        Button button = CreateButton(parent, label, new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 54f), label);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 54f;
        return button;
    }

    private void AddListButton(string title, string subtitle, string key, Action onClick)
    {
        Button button = CreateListButton(_listContent, title, subtitle);
        SetButtonVisual(button, string.Equals(_selectedKey, key, StringComparison.OrdinalIgnoreCase));
        button.onClick.AddListener(() => onClick?.Invoke());
    }

    private void AddInventoryListButton(InventoryItemRecord item, string subtitle, string key, Action onClick)
    {
        Button button = CreateInventoryListButton(_listContent, item, subtitle);
        SetButtonVisual(button, string.Equals(_selectedKey, key, StringComparison.OrdinalIgnoreCase));
        button.onClick.AddListener(() => onClick?.Invoke());
    }

    private Button CreateListButton(Transform parent, string title, string subtitle)
    {
        GameObject root = new GameObject("ListButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredHeight = 62f;
        Button button = root.GetComponent<Button>();
        YQUITheme.ApplyButton(button);

        CreateAbsoluteText(root.transform, "Title", 16f, FontStyles.Bold, new Vector2(12f, -10f), new Vector2(450f, 20f)).text = title;
        TMP_Text sub = CreateAbsoluteText(root.transform, "Subtitle", 13f, FontStyles.Normal, new Vector2(12f, -34f), new Vector2(450f, 18f));
        sub.text = subtitle;
        sub.color = YQUITheme.Muted;
        return button;
    }

    private Button CreateInventoryListButton(Transform parent, InventoryItemRecord item, string subtitle)
    {
        GameObject root = new GameObject("InventoryListButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredHeight = 68f;
        Button button = root.GetComponent<Button>();
        YQUITheme.ApplyButton(button);

        Image frame = CreateIconFrame(root.transform, "IconFrame", new Vector2(10f, -10f), new Vector2(48f, 48f));
        RawImage icon = CreateRawIcon(frame.transform, "Icon", new Vector2(5f, -5f), new Vector2(38f, 38f));
        ApplyItemIcon(icon, frame, item);

        CreateAbsoluteText(root.transform, "Title", 16f, FontStyles.Bold, new Vector2(68f, -10f), new Vector2(382f, 20f)).text = item != null ? item.displayName : "Unknown Item";
        TMP_Text sub = CreateAbsoluteText(root.transform, "Subtitle", 13f, FontStyles.Normal, new Vector2(68f, -36f), new Vector2(382f, 18f));
        sub.text = subtitle;
        sub.color = YQUITheme.Muted;
        return button;
    }

    private Button CreateTileButton(Transform parent, string title, string subtitle)
    {
        GameObject root = new GameObject("TileButton", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Button button = root.GetComponent<Button>();
        YQUITheme.ApplyButton(button);
        CreateAbsoluteText(root.transform, "Title", 14f, FontStyles.Bold, new Vector2(10f, -8f), new Vector2(106f, 18f)).text = title;
        TMP_Text sub = CreateAbsoluteText(root.transform, "Subtitle", 12f, FontStyles.Normal, new Vector2(10f, -30f), new Vector2(106f, 26f));
        sub.text = subtitle;
        sub.textWrappingMode = TextWrappingModes.Normal;
        sub.color = YQUITheme.Muted;
        return button;
    }

    private Button CreateEquipmentSlotButton(Transform parent, SlotDef slot, InventoryItemRecord item, string subtitle)
    {
        GameObject root = new GameObject("EquipmentSlot_" + slot.SlotId, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = slot.Position;
        rt.sizeDelta = slot.Size;
        Button button = root.GetComponent<Button>();
        YQUITheme.ApplyButton(button);

        float iconSize = Mathf.Clamp(slot.Size.y - 14f, 22f, 34f);
        float textX = item != null ? iconSize + 14f : 8f;
        if (item != null)
        {
            Image frame = CreateIconFrame(root.transform, "IconFrame", new Vector2(7f, -7f), new Vector2(iconSize, iconSize));
            RawImage icon = CreateRawIcon(frame.transform, "Icon", new Vector2(3f, -3f), new Vector2(iconSize - 6f, iconSize - 6f));
            ApplyItemIcon(icon, frame, item);
        }

        TMP_Text title = CreateAbsoluteText(root.transform, "Title", 13f, FontStyles.Bold, new Vector2(textX, -7f), new Vector2(slot.Size.x - textX - 8f, 17f));
        title.text = slot.Label;
        title.alignment = item != null ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;

        TMP_Text sub = CreateAbsoluteText(root.transform, "Subtitle", 11f, FontStyles.Normal, new Vector2(textX, -28f), new Vector2(slot.Size.x - textX - 8f, slot.Size.y - 30f));
        sub.text = subtitle;
        sub.textWrappingMode = TextWrappingModes.Normal;
        sub.overflowMode = TextOverflowModes.Ellipsis;
        sub.alignment = item != null ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
        sub.color = YQUITheme.Muted;
        return button;
    }

    private static Image CreateIconFrame(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        Image frame = CreateUiImage(parent, name, anchoredPosition, size);
        frame.color = new Color(0.01f, 0.015f, 0.02f, 0.55f);
        AddOutline(frame.rectTransform, new Color(0.68f, 0.61f, 0.42f, 0.24f));
        return frame;
    }

    private static RawImage CreateRawIcon(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
        RawImage image = go.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.color = Color.white;
        return image;
    }

    private static Image CreateUiImage(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void SetDetailIcon(InventoryItemRecord item)
    {
        if (_detailIconImage == null || _detailIconFrame == null)
            return;

        ApplyItemIcon(_detailIconImage, _detailIconFrame, item);
    }

    private static void ApplyItemIcon(RawImage icon, Image frame, InventoryItemRecord item)
    {
        if (icon == null)
            return;

        Texture2D texture = ResolveItemIconTexture(item);
        bool hasTexture = texture != null;
        // note: Hide only the picture when no registry texture exists; the frame still reserves stable UI space.
        icon.texture = texture;
        icon.enabled = hasTexture;
        if (frame != null)
            frame.color = hasTexture ? new Color(0.01f, 0.015f, 0.02f, 0.72f) : new Color(0.01f, 0.015f, 0.02f, 0.28f);
    }

    private static Texture2D ResolveItemIconTexture(InventoryItemRecord item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.iconKey))
            return null;

        YQRuntime2DArtRegistry registry = YQRuntime2DArtRegistry.Load();
        if (registry != null && registry.TryGetTexture(item.iconKey, out Texture2D texture))
            return texture;

        return null;
    }

    private static void CreateEquipmentGuide(Transform parent)
    {
        Color guide = new Color(0.30f, 0.36f, 0.43f, 0.18f);
        CreateGuidePlate(parent, "Guide_Head", new Vector2(0f, -18f), new Vector2(86f, 46f), guide);
        CreateGuidePlate(parent, "Guide_Torso", new Vector2(0f, -118f), new Vector2(122f, 148f), guide);
        CreateGuidePlate(parent, "Guide_LeftArm", new Vector2(-142f, -132f), new Vector2(46f, 136f), guide);
        CreateGuidePlate(parent, "Guide_RightArm", new Vector2(142f, -132f), new Vector2(46f, 136f), guide);
        CreateGuidePlate(parent, "Guide_Legs", new Vector2(0f, -310f), new Vector2(108f, 154f), guide);
    }

    private static void CreateGuidePlate(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private Button CreateButton(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, string label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
        Button button = go.GetComponent<Button>();
        YQUITheme.ApplyButton(button);
        TMP_Text text = CreateAbsoluteText(go.transform, "Label", 16f, FontStyles.Bold, new Vector2(0f, 0f), size);
        RectTransform textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = Vector2.zero;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        text.text = label;
        YQUITheme.ApplyText(text, YQUITheme.Ink);
        return button;
    }

    private ScrollRect BuildScroll(RectTransform root, out RectTransform content)
    {
        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        RectTransform viewport = viewportGo.GetComponent<RectTransform>();
        viewport.SetParent(root, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(8f, 8f);
        viewport.offsetMax = new Vector2(-8f, -8f);
        viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content = contentGo.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, 0f);
        content.offsetMax = new Vector2(0f, 0f);
        VerticalLayoutGroup layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        return scroll;
    }

    private void UpdateTabVisuals()
    {
        for (int i = 0; i < _tabButtons.Count; i++)
            SetButtonVisual(_tabButtons[i], i == (int)_activeTab);
    }

    private static void SetButtonVisual(Button button, bool selected)
    {
        if (button == null)
            return;
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            // note: Selection state now uses the shared theme so tabs, lists, and equipment slots read consistently.
            YQUITheme.ApplyButton(button, selected);
        }
    }

    private void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void AddSectionHeader(Transform parent, string text)
    {
        TMP_Text header = CreateTextBlock(parent, 15f, FontStyles.Bold, new Color32(255, 240, 184, 255));
        header.text = text;
    }

    private void AddEmptyState(string text)
    {
        TMP_Text empty = CreateTextBlock(_listContent, 14f, FontStyles.Italic, new Color32(176, 186, 198, 255));
        empty.text = text;
    }

    private static string FormatSlotName(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return "Unslotted";
        return ToTitle(slot.Replace("_", " "));
    }

    private static string ToTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        string[] parts = value.Replace("_", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
                continue;
            parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
        }
        return string.Join(" ", parts);
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string DescribeItem(InventoryItemRecord item)
    {
        return item == null ? "<empty>" : item.displayName;
    }

    private static string Trim(string value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxChars)
            return value;
        // note: Runtime UI uses ASCII ellipsis so the default TMP fallback never enters a missing-glyph rebuild loop.
        return value.Substring(0, Mathf.Max(1, maxChars - 3)).TrimEnd() + "...";
    }
}
