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
public sealed partial class YourQuestTutorialMenuUI : MonoBehaviour
{
    public static bool IsOpenNow { get; private set; }

    private enum MenuTab
    {
        Inventory = 0,
        Skills = 1,
        Classes = 2,
        Quests = 3,
        Stats = 4,
        Equipment = 5
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
        "Stats",
        "Equipment"
    };

    // note: Matching equipment columns group related controls around the same authoritative character preview.
    private static readonly SlotDef[] EquipmentSlots =
    {
        new SlotDef("head", "Headpiece", -360f, -226f, 240f, 68f),
        new SlotDef("necklace", "Necklace", 360f, -58f, 240f, 68f),
        new SlotDef("chest", "Chest armor", -360f, -310f, 240f, 68f),
        new SlotDef("offhand", "Off hand", -360f, -142f, 240f, 68f),
        new SlotDef("weapon", "Main hand", -360f, -58f, 240f, 68f),
        new SlotDef("gloves", "Gauntlets", -360f, -394f, 240f, 68f),
        new SlotDef("ring_left", "Ring L", 360f, -142f, 240f, 68f),
        new SlotDef("ring_right", "Ring R", 360f, -226f, 240f, 68f),
        new SlotDef("belt", "Belt", 360f, -310f, 240f, 68f),
        new SlotDef("legs", "Legs", -360f, -478f, 240f, 68f),
        new SlotDef("boots", "Boots", 360f, -394f, 240f, 68f),
        new SlotDef("trinket", "Charm", 360f, -478f, 240f, 68f),
        // note: Cloaks occupy their own accepted slot; exposing it also restores inspection, clearing and loadout counts.
        new SlotDef("cloak", "Cloak", 360f, -562f, 240f, 68f)
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
        BuildBlueglassPresentation();
        SetOpen(false);
    }


    public void ForceCloseFromBootstrap()
    {
        // note: Startup owns cursor/modal transitions; cancel both presentations without resetting another owner's state.
        CloseQuick(false);
        _open = false;
        IsOpenNow = false;
        _dirty = true;
        if (_canvas != null)
            _canvas.enabled = false;
        if (_fullRoot != null) _fullRoot.gameObject.SetActive(false);
        RuntimeModalUiBlocker.Release(ModalToken);
        RuntimeModalUiBlocker.SetMenuOpen(false);
    }

    private void OnDestroy()
    {
        CloseQuick(true);
        if (_open)
        {
            RuntimeModalUiBlocker.Release(ModalToken);
            RuntimeModalUiBlocker.SetMenuOpen(false);
        }
        IsOpenNow = false;
    }

    private void Update()
    {
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
        // note: Console history/completion/typing must never reach quick-menu or pause input even when a menu was already open.
        if (YQDeveloperConsole.CapturesInput) return;
#endif
        Keyboard kb = Keyboard.current;
        if (kb == null)
        {
            CloseQuick(true);
            return;
        }

        if (UpdateQuickInput(kb))
            return;

        if (ChangedThisFrame)
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

        // note: Live refresh must not destroy a row between a physical press and release.
        if (_dirty && !(Mouse.current?.leftButton.isPressed ?? false))
            Render();
    }

    private void SetOpen(bool value)
    {
        if (_open == value && _canvas != null && _canvas.enabled == value)
            return;

        _open = value;
        _presentationChangeFrame = Time.frameCount;
        IsOpenNow = value;
        if (_canvas != null)
            _canvas.enabled = value;

        if (value)
        {
            CloseQuick(false);
            _fullRoot.gameObject.SetActive(true);
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
            if (!RuntimeModalUiBlocker.IsBlocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (_fullRoot != null) _fullRoot.gameObject.SetActive(false);
        }
    }

    private void MarkDirty(bool immediate = false)
    {
        _dirty = true;
        _lastStateHash = int.MinValue;
        if (immediate && (_open || _quick))
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
            // note: Live quantities and every canonical equipment slot invalidate presentation without replacing stable quick controls.
            foreach (InventoryItemRecord item in state.inventoryItems)
                if (item!=null) { hash=hash*31+(item.itemId ?? string.Empty).GetHashCode(); hash=hash*31+item.quantity; }
            foreach (var equipped in state.equippedItemBySlot)
                hash=hash*31+(equipped.Key+":"+equipped.Value).GetHashCode();
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
        HideRecordPreview();
        _listScroll.StopMovement();
        _listScroll.verticalNormalizedPosition=1;
        YQBlueglassFeedback.Request(YQBlueglassCue.Screen, YQBlueglassFeedback.ControllerActive);
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
        _offerPanel.gameObject.SetActive(IsInventoryTab && state.GetActiveOffer() != null);

        if(IsInventoryTab) RebuildEquipmentSection(state);
        ConfigureCarriedLayout();
        RebuildList(state, world, content);
        RebuildDetails(state, world, content);
        RebuildOfferPanel(state);
        UpdateActionButtons(state);
        UpdateTabVisuals();
        _footerText.text = string.IsNullOrWhiteSpace(_statusMessage) ? BuildFooter(state, world) : _statusMessage;
        RefreshQuickPresentation(state);
        // note: Record the state actually rendered; an immediate selection must not trigger another full rebuild at the next poll.
        _lastStateHash = ComputeStateHash();
    }

    private void RebuildEquipmentSection(PlayerState state)
    {
        ClearChildren(_equipmentContent);
        // note: The linked character portrait replaces the old block-shaped body guide.

        for (int i = 0; i < EquipmentSlots.Length; i++)
        {
            SlotDef slot = EquipmentSlots[i];
            InventoryItemRecord equipped = state.GetEquippedItem(slot.SlotId);
            string key = "slot:" + slot.SlotId;
            Button button = CreateEquipmentSlotButton(_equipmentContent, slot, equipped, equipped != null ? Trim(equipped.displayName, 20) : "Empty");
            SetButtonVisual(button, string.Equals(_selectedKey, key, StringComparison.OrdinalIgnoreCase));
            BindRecordHover(button,key);
            button.onClick.AddListener(() =>
            {
                if (!IsInventoryTab)
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
            case MenuTab.Equipment:
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
        // note: Item portraits form a carried-item grid; detailed facts stay in the shared inspector.
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
                ? ToTitle(item.itemType) + "  \u00B7  Qty " + Mathf.Max(1, item.quantity)
                : FormatSlotName(item.equipSlot) + "  \u00B7  " + ToTitle(item.rarity) + "  \u00B7  PWR " + item.powerScore;

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
        AddSectionHeader(_listContent, "Skills");
        SkillRecord first = null;
        for (int i = 0; i < state.skills.Count; i++)
        {
            SkillRecord skill = state.skills[i];
            if (skill == null || YQSpellCircleRules.IsSpell(skill))
                continue;
            if (first == null)
                first = skill;

            AddSkillListButton(skill);
        }
        // note: Group only occupied spell circles, in ascending order, without reordering or rewriting accepted state.
        for (int circle = YQSpellCircleRules.FirstCircle; circle <= YQSpellCircleRules.LastCircle; circle++)
        {
            bool headingAdded = false;
            for (int i = 0; i < state.skills.Count; i++)
            {
                SkillRecord spell = state.skills[i];
                if (!YQSpellCircleRules.IsSpell(spell) || YQSpellCircleRules.GetCircle(spell) != circle) continue;
                if (!headingAdded)
                {
                    AddSectionHeader(_listContent, "Spells \u2022 Circle " + circle);
                    headingAdded = true;
                }
                if (first == null) first = spell;
                AddSkillListButton(spell);
            }
        }

        if (state.skills.Count == 0)
            AddEmptyState("No skills learned yet.");
        if (string.IsNullOrWhiteSpace(_selectedKey) && first != null)
            _selectedKey = "skill:" + first.skillId;
    }

    private void AddSkillListButton(SkillRecord skill)
    {
        // note: One classification rule drives list grouping, labels, details and casting, including legacy spell records.
        bool isSpell = YQSpellCircleRules.IsSpell(skill);
        string key = "skill:" + skill.skillId;
        string subtitle = (isSpell ? "Spell  \u2022  Circle " + YQSpellCircleRules.GetCircle(skill) :
            "Skill  \u2022  Tier " + Mathf.Max(1, skill.tier)) + "  \u2022  Rank " + Mathf.Max(1, skill.rank);
        AddListButton(skill.name, subtitle, key, () =>
        {
            _selectedKey = key;
            MarkDirty(true);
        });
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
            string subtitle = (active ? "Active  \u00B7  " : string.Empty) + ToTitle(quest.status);
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
        AddListButton("Combat", "Attributes with equipment applied", "stats:core", () => { _selectedKey = "stats:core"; MarkDirty(true); });
        AddListButton("Loadout", "Equipped items and skills", "stats:loadout", () => { _selectedKey = "stats:loadout"; MarkDirty(true); });
        AddListButton("Journey", "Your current region and tracked quest", "stats:world", () => { _selectedKey = "stats:world"; MarkDirty(true); });
        AddListButton("Progress", "Skills, titles, and quest progress", "stats:activity", () => { _selectedKey = "stats:activity"; MarkDirty(true); });
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
            case MenuTab.Equipment:
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

        // note: Polling accepted state must not pull a reader back to the top of a long description.
        if (_detailScroll != null && _lastDetailKey != _activeTab + ":" + _selectedKey)
        {
            _lastDetailKey = _activeTab + ":" + _selectedKey;
            _detailScroll.StopMovement();
            _detailScroll.verticalNormalizedPosition = 1f;
        }
    }

    private string _lastDetailKey;

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
                _detailTitleText.text=YQBlueglassText.Escape(equipped.displayName);
                AppendItemOverview(sb,equipped);
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
        _detailTitleText.text = YQBlueglassText.Escape(item.displayName);
        StringBuilder body = new StringBuilder(640);
        AppendItemOverview(body,item);
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

        _detailTitleText.text = YQBlueglassText.Escape(selected.name);
        StringBuilder sb = new StringBuilder(512);
        bool isSpell = YQSpellCircleRules.IsSpell(selected);
        if (isSpell)
        {
            // note: Keep the casting values visible together in the compact inspector instead of repeating the spell classification.
            sb.AppendLine("Spell  \u2022  Circle " + YQSpellCircleRules.GetCircle(selected) + "  \u2022  Rank " + Mathf.Max(1, selected.rank));
        }
        else
        {
            sb.AppendLine(YQBlueglassText.Tint("Skill  \u2022  " + YQBlueglassText.Escape(ToTitle(selected.type)), YQBlueglassText.Quiet));
            YQBlueglassText.Value(sb, "Tier / Rank", Mathf.Max(1, selected.tier) + " / " + Mathf.Max(1, selected.rank));
        }
        if (isSpell)
        {
            // note: The inspector displays the same mechanical cost, strength and duration used by live combat.
            int equipmentBonus = GeneratedRpgContentService.Instance != null ? GeneratedRpgContentService.Instance.GetManaBonus(state) : 0;
            YQBlueglassText.Value(sb, "Cost", YQSpellCircleRules.GetResourceCost(selected).ToString("0") + " " + ToTitle(YQSpellCircleRules.GetResourceType(selected)), YQBlueglassText.ResourceColor(YQSpellCircleRules.GetResourceType(selected)));
            YQBlueglassText.Value(sb, "Strength", YQSpellCircleRules.GetPower(selected, equipmentBonus).ToString());
            YQBlueglassText.Value(sb, "Cast time", YQSpellCircleRules.GetCastSeconds(selected, equipmentBonus).ToString("0.##") + " s");
        }
        // note: Inspection exposes supported mechanical fields directly, without inferring executable effects from prose.
        if(!isSpell && selected.resourceCost>0) YQBlueglassText.Value(sb,"Cost",selected.resourceCost + " " + ToTitle(selected.resourceType));
        if(selected.cooldownSeconds>0) YQBlueglassText.Value(sb,"Cooldown",selected.cooldownSeconds.ToString("0.#") + " s");
        if(!string.IsNullOrWhiteSpace(selected.targetingMode)) YQBlueglassText.Value(sb,"Target",ToTitle(selected.targetingMode));
        YQBlueglassText.Flavour(sb, selected.description);
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
                    _detailTitleText.text = YQBlueglassText.Escape(record.name);
                    _detailBodyText.text = YQBlueglassText.Tint("CLASS",YQBlueglassText.Quiet)+"\n\n"+YQBlueglassText.Description(record.description);
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
                    _detailTitleText.text = YQBlueglassText.Escape(record.name);
                    _detailBodyText.text = YQBlueglassText.Tint("TITLE",YQBlueglassText.Quiet)+"\n\n"+YQBlueglassText.Description(record.description);
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

        _detailTitleText.text = YQBlueglassText.Escape(quest.name);
        StringBuilder sb = new StringBuilder(512);
        YQBlueglassText.Value(sb,"Status",ToTitle(quest.status));
        if (string.Equals(quest.questId,state.activeQuestId,StringComparison.OrdinalIgnoreCase))
            sb.AppendLine(YQBlueglassText.Tint("Tracked quest",YQBlueglassText.Mana));
        // note: Objective completion is read from the accepted record; descriptive text never determines progress.
        if (quest.objectives != null && quest.objectives.Count > 0)
        {
            YQBlueglassText.Section(sb,"Objectives");
            foreach (QuestObjectiveRecord objective in quest.objectives)
            {
                if (objective == null) continue;
                string label=YQBlueglassText.Description(objective.description);
                if (string.IsNullOrWhiteSpace(label)) label=YQBlueglassText.Escape(Safe(objective.targetName,"Objective"));
                sb.AppendLine(YQBlueglassText.Tint((objective.completed ? "Complete  " : "\u2022  ")+label,
                    objective.completed ? YQBlueglassText.Positive : YQBlueglassText.Ink));
            }
        }
        if (quest.rewardXp > 0 || quest.rewardGold > 0)
        {
            YQBlueglassText.Section(sb,"Rewards");
            if(quest.rewardXp>0) YQBlueglassText.Value(sb,"Experience",quest.rewardXp.ToString("N0"));
            if(quest.rewardGold>0) YQBlueglassText.Value(sb,"Gold",quest.rewardGold.ToString("N0"),YQBlueglassText.Stamina);
        }
        YQBlueglassText.Flavour(sb,quest.description);
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
                // note: World internals are diagnostic data, not player discoveries or an omniscient journal.
                _detailTitleText.text = "Journey";
                YQBlueglassText.Value(sb,"Region",Safe(state.currentRegionName,"Uncharted"));
                YQBlueglassText.Section(sb,"Tracked quest");
                sb.AppendLine(YQBlueglassText.Escape(Safe(state.GetActiveQuest()?.name,"No quest tracked")));
                YQBlueglassText.Flavour(sb,state.GetActiveQuest()?.description);
                break;

            case "stats:activity":
                _detailTitleText.text = "Progress";
                YQBlueglassText.Value(sb,"Level",state.level.ToString());
                YQBlueglassText.Value(sb,"Next level",state.xpToNext.ToString("N0") + " XP remaining");
                YQBlueglassText.Section(sb,"Your path");
                YQBlueglassText.Value(sb,"Skills",state.skills.Count.ToString());
                YQBlueglassText.Value(sb,"Classes",state.classes.Count.ToString());
                YQBlueglassText.Value(sb,"Titles",state.titles.Count.ToString());
                YQBlueglassText.Value(sb,"Quests in journal",state.quests.Count.ToString());
                break;

            default:
                _detailTitleText.text = "Combat";
                int maxHealth = content != null ? content.GetDerivedMaxHealth(state) : state.stats.maxHealth;
                int maxStamina = content != null ? content.GetDerivedMaxStamina(state) : state.stats.maxStamina;
                int maxMana = content != null ? content.GetDerivedMaxMana(state) : state.stats.maxMana;
                YQBlueglassText.Value(sb,"Level",state.level.ToString());
                YQBlueglassText.Value(sb,"Experience",state.xp.ToString("N0") + " / " + Mathf.Max(1,state.xp+state.xpToNext).ToString("N0"));
                YQBlueglassText.Section(sb,"Combat attributes");
                YQBlueglassText.Value(sb,"Attack",(state.stats.attack+(content!=null ? content.GetAttackBonus(state) : 0)).ToString());
                YQBlueglassText.Value(sb,"Defense",(state.stats.defense+(content!=null ? content.GetDefenseBonus(state) : 0)).ToString());
                YQBlueglassText.Value(sb,"Critical chance",(state.stats.critChance*100f).ToString("0.#")+"%");
                // note: The legacy movement rating is not motor speed; show only the equipment modifier owned by this sheet.
                YQBlueglassText.Value(sb,"Equipment movement bonus",(content!=null ? content.GetMoveSpeedBonus(state) : 0).ToString("+0.##;-0.##;0")+" m/s");
                YQBlueglassText.Section(sb,"Resources");
                YQBlueglassText.Value(sb,"Maximum health",maxHealth.ToString(),YQBlueglassText.Health);
                YQBlueglassText.Value(sb,"Maximum stamina",maxStamina.ToString(),YQBlueglassText.Stamina);
                YQBlueglassText.Value(sb,"Maximum mana",maxMana.ToString(),YQBlueglassText.Mana);
                sb.AppendLine().Append(YQBlueglassText.Tint("Equipment bonuses included.",YQBlueglassText.Quiet));
                break;
        }
        _detailBodyText.text = sb.ToString();
    }

    private void RebuildOfferPanel(PlayerState state)
    {
        PendingProgressionOfferRecord offer = state.GetActiveOffer();
        bool visible = IsInventoryTab && offer != null;
        _offerPanel.gameObject.SetActive(visible);
        if (!visible)
            return;

        _offerTitleText.text = (offer.isUpgrade ? "Upgrade" : "Offer") + " \u00B7 " + ToTitle(offer.offerKind) + " \u00B7 " + offer.name;
        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine(YQBlueglassText.Description(offer.description));
        sb.AppendLine();
        if (offer.proposedTier > 0)
            sb.Append(YQSpellCircleRules.IsSpell(offer) ? "Circle " + YQSpellCircleRules.ClampCircle(offer.proposedTier) :
                "Tier " + offer.proposedTier);
        _offerBodyText.text = sb.ToString();
    }

    private void UpdateActionButtons(PlayerState state)
    {
        bool showPrimary = false;
        bool showSecondary = false;
        string primary = string.Empty;
        string secondary = string.Empty;

        if (IsInventoryTab)
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
                        primary = IsItemEquipped(state, item.itemId) ? "Equipped" : "Equip";
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
                primary = YQSpellCircleRules.IsSpell(skill) ? "Equip Spell" : "Equip Active";
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
        _primaryButton.interactable = primary != "Equipped";
        SyncQuickActions(showPrimary, showSecondary, primary, secondary);
    }

    private void OnPrimaryActionClicked()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return;

        PlayerState state = psm.state;
        GeneratedRpgContentService content = GeneratedRpgContentService.Instance;
        _statusMessage = string.Empty;
        bool accepted = false;

        if (IsInventoryTab)
        {
            if (TrySelectedSlot(out string slotId))
            {
                InventoryItemRecord best = FindBestForSlot(state, slotId);
                if (best != null && state.TryEquipItem(best.itemId, out string message))
                {
                    content?.SetInventoryMessage(message);
                    Persist();
                    _statusMessage = message;
                    accepted = true;
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
                            accepted = true;
                        }
                        else _statusMessage = message;
                    }
                    else if (item.IsConsumable)
                    {
                        if (content != null)
                        {
                            int quantityBeforeUse = item.quantity;
                            content.UseSpecificConsumable(item.itemId);
                            Persist();
                            _statusMessage = Safe(content.LastInventoryMessage, "Used item.");
                            accepted = !state.inventoryItems.Contains(item) || item.quantity < quantityBeforeUse;
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
                state.equippedSkillBySlot[YQSpellCircleRules.IsSpell(skill) ? "spell" : "active"] = skill.skillId;
                Persist();
                _statusMessage = "Equipped " + skill.name + ".";
                accepted = true;
            }
        }
        else if (_activeTab == MenuTab.Quests)
        {
            QuestRecord quest = GetSelectedQuest(state);
            if (quest != null && state.SetActiveQuest(quest.questId))
            {
                Persist();
                _statusMessage = "Tracking " + quest.name + ".";
                accepted = true;
            }
        }

        PulseAction(accepted);
        MarkDirty(true);
    }

    private void OnSecondaryActionClicked()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return;

        if (IsInventoryTab && TrySelectedSlot(out string slotId))
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
            PulseAction(true);
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
            PulseAction(true);
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
            PulseAction(true);
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

    private static void AppendItemOverview(StringBuilder body,InventoryItemRecord item)
    {
        // note: Structured item identity stays quiet; useful power/recovery receives the inspector's primary visual emphasis.
        body.Append(YQBlueglassText.Tint(YQBlueglassText.Escape(ToTitle(Safe(item.rarity,"common"))),YQBlueglassText.RarityColor(item.rarity)));
        string type=string.IsNullOrWhiteSpace(item.equipSlot) ? ToTitle(item.itemType) : FormatSlotName(item.equipSlot);
        body.Append("  \u2022  ").AppendLine(YQBlueglassText.Escape(type));
        if(item.quantity>1) YQBlueglassText.Value(body,"Quantity",item.quantity.ToString("N0"));
        if(item.powerScore>0) body.Append("<size=140%><b>").Append(item.powerScore).AppendLine("</b></size>  <size=85%>POWER</size>");
    }

    private void AppendItemDetail(StringBuilder sb, InventoryItemRecord item)
    {
        if (item == null)
            return;
        // note: Signed, labelled values separate equipment modifiers from on-use recovery and flavour.
        if(item.attackBonus!=0 || item.defenseBonus!=0 || item.healthBonus!=0 || item.staminaBonus!=0 || item.manaBonus!=0 || Mathf.Abs(item.moveSpeedBonus)>.001f)
            YQBlueglassText.Section(sb,"While equipped");
        YQBlueglassText.Bonus(sb,"Attack",item.attackBonus);
        YQBlueglassText.Bonus(sb,"Defense",item.defenseBonus);
        YQBlueglassText.Bonus(sb,"Maximum health",item.healthBonus);
        YQBlueglassText.Bonus(sb,"Maximum stamina",item.staminaBonus);
        YQBlueglassText.Bonus(sb,"Maximum mana",item.manaBonus);
        YQBlueglassText.Bonus(sb,"Movement speed",item.moveSpeedBonus);
        if(item.healAmount>0 || item.restoreStaminaAmount>0 || item.restoreManaAmount>0)
            YQBlueglassText.Section(sb,"On use");
        if(item.healAmount>0) YQBlueglassText.Value(sb,"Restore health",item.healAmount.ToString(),YQBlueglassText.Health);
        if(item.restoreStaminaAmount>0) YQBlueglassText.Value(sb,"Restore stamina",item.restoreStaminaAmount.ToString(),YQBlueglassText.Stamina);
        if(item.restoreManaAmount>0) YQBlueglassText.Value(sb,"Restore mana",item.restoreManaAmount.ToString(),YQBlueglassText.Mana);
        YQBlueglassText.Flavour(sb,item.description);
    }

    private string BuildSubtitle(PlayerState state, WorldState world)
    {
        return "Level " + state.level + "   \u00B7   " + state.currency.ToString("N0") + " Gold   \u00B7   " + YQBlueglassText.Escape(Safe(state.currentRegionName,"Uncharted"));
    }

    private string BuildFooter(PlayerState state, WorldState world)
    {
        return "Hover to inspect   \u00B7   Select for actions   \u00B7   Scroll to browse   \u00B7   Esc close";
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
        text.raycastTarget = false;
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
        text.raycastTarget = false;
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
        text.raycastTarget = false;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        YQUITheme.ApplyText(text);
        // note: Scroll-owned descriptions grow vertically; ellipsis must never conceal the last effect or objective.
        text.overflowMode = TextOverflowModes.Overflow;
        text.lineSpacing = 5f;
        text.paragraphSpacing = 8f;
        ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return text;
    }

    private Button CreateNavButton(Transform parent, string label)
    {
        Button button = CreateButton(parent, label, new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 54f), label);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 54f;
        button.GetComponentInChildren<TMP_Text>().fontSize=22;
        return button;
    }

    private void AddListButton(string title, string subtitle, string key, Action onClick)
    {
        Button button = CreateListButton(_listContent, title, subtitle);
        SetButtonVisual(button, string.Equals(_selectedKey, key, StringComparison.OrdinalIgnoreCase));
        button.onClick.AddListener(() => onClick?.Invoke());
        BindRecordHover(button,key);
    }

    private void AddInventoryListButton(InventoryItemRecord item, string subtitle, string key, Action onClick)
    {
        Button button = CreateInventoryListButton(_listContent, item, subtitle);
        SetButtonVisual(button, string.Equals(_selectedKey, key, StringComparison.OrdinalIgnoreCase));
        button.onClick.AddListener(() => onClick?.Invoke());
        BindRecordHover(button,key);
    }

    private Button CreateListButton(Transform parent, string title, string subtitle)
    {
        GameObject root = new GameObject("ListButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        LayoutElement layout = root.GetComponent<LayoutElement>();
        // note: Generated record names and descriptions remain readable at the game's reference scale.
        layout.preferredHeight = 78f;
        Button button = root.GetComponent<Button>();
        YQUITheme.ApplyButton(button);

        TMP_Text heading = CreateAbsoluteText(root.transform, "Title", 22f, FontStyles.Bold, new Vector2(12f, -10f), new Vector2(450f, 28f));
        heading.text = YQBlueglassText.Escape(title);
        heading.rectTransform.anchorMax = new Vector2(1,1);
        heading.rectTransform.sizeDelta = new Vector2(-24,28);
        TMP_Text sub = CreateAbsoluteText(root.transform, "Subtitle", 18f, FontStyles.Normal, new Vector2(12f, -42f), new Vector2(450f, 25f));
        sub.text = subtitle;
        sub.color = YQUITheme.Muted;
        sub.rectTransform.anchorMax = new Vector2(1,1);
        sub.rectTransform.sizeDelta = new Vector2(-24,25);
        return button;
    }

    private Button CreateInventoryListButton(Transform parent, InventoryItemRecord item, string subtitle)
    {
        GameObject root = new GameObject("InventoryListButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredHeight = 126f;
        Button button = root.GetComponent<Button>();
        YQUITheme.ApplyButton(button);

        Image frame = CreateIconFrame(root.transform, "IconFrame", new Vector2(12f, -12f), new Vector2(60f, 60f));
        RawImage icon = CreateRawIcon(frame.transform, "Icon", new Vector2(3f, -3f), new Vector2(54f, 54f));
        ApplyItemIcon(icon, frame, item);

        // note: Keep item names and mechanical subtitles legible without adding more decoration to the carried-item grid.
        TMP_Text title = CreateAbsoluteText(root.transform, "Title", 18f, FontStyles.Bold, new Vector2(72f, -12f), new Vector2(96f, 58f));
        title.text = item != null ? YQBlueglassText.Escape(item.displayName) : "Unknown item";
        title.rectTransform.anchorMax=new Vector2(1,1);
        title.rectTransform.sizeDelta=new Vector2(-88,58);
        title.textWrappingMode = TextWrappingModes.Normal;
        // note: Shrink only long generated names within the two-line tile heading rather than splitting ordinary names mid-word.
        title.enableAutoSizing=true;
        title.fontSizeMin=18f;
        title.fontSizeMax=20f;
        TMP_Text sub = CreateAbsoluteText(root.transform, "Subtitle", 18f, FontStyles.Normal, new Vector2(12f, -82f), new Vector2(156f, 32f));
        sub.text = subtitle;
        if(item!=null && ColorUtility.TryParseHtmlString("#"+YQBlueglassText.RarityColor(item.rarity),out Color rarity)) sub.color=rarity;
        sub.rectTransform.anchorMax=new Vector2(1,1);
        sub.rectTransform.sizeDelta=new Vector2(-24,32);
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

        // note: Keep the hit rectangle stable while a separate circular socket carries the slot artwork.
        root.GetComponent<Image>().sprite = null;
        root.GetComponent<Image>().color = Color.clear;
        Image socket = CreateUiImage(root.transform,"Socket",new Vector2(3,-6),new Vector2(54,54));
        button.targetGraphic = socket;
        YQUITheme.ApplyButton(button);
        float iconSize = 44f;
        float textX = 66f;
        if (item != null)
        {
            Image frame = CreateIconFrame(socket.transform, "IconFrame", new Vector2(5f, -5f), new Vector2(iconSize, iconSize));
            RawImage icon = CreateRawIcon(frame.transform, "Icon", new Vector2(3f, -3f), new Vector2(iconSize - 6f, iconSize - 6f));
            ApplyItemIcon(icon, frame, item);
        }

        TMP_Text title = CreateAbsoluteText(root.transform, "Title", 18f, FontStyles.Bold, new Vector2(textX, -4f), new Vector2(slot.Size.x - textX - 8f, 25f));
        title.text = slot.Label;
        title.alignment = TextAlignmentOptions.Left;

        TMP_Text sub = CreateAbsoluteText(root.transform, "Subtitle", 16f, FontStyles.Normal, new Vector2(textX, -30f), new Vector2(slot.Size.x - textX - 8f, slot.Size.y - 30f));
        sub.text = subtitle;
        sub.textWrappingMode = TextWrappingModes.Normal;
        sub.overflowMode = TextOverflowModes.Ellipsis;
        sub.alignment = TextAlignmentOptions.Left;
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
        Sprite fallback = texture == null ? YQBlueglassStyle.ItemPortrait(item) : null;
        if (fallback != null)
        {
            // note: Registry artwork wins; an explicit structured-family fallback reserves a visible item portrait.
            texture = fallback.texture;
            Rect rect = fallback.rect;
            icon.uvRect = new Rect(rect.x/texture.width,rect.y/texture.height,rect.width/texture.width,rect.height/texture.height);
        }
        else icon.uvRect = new Rect(0,0,1,1);
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
        YQBlueglassScrollAffordance.Attach(scroll);
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
        {
            // note: Detach pending destruction immediately: the quick view mirrors this child list in the same refresh.
            Transform stale=parent.GetChild(i);
            stale.gameObject.SetActive(false);
            stale.SetParent(null,false);
            Destroy(stale.gameObject);
        }
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
