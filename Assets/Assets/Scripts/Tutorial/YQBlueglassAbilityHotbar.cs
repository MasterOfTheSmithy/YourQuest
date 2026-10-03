using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// note: This view edits the existing active/spell loadout through PlayerStateManager; it owns no ability execution or saved slots.
[DisallowMultipleComponent]
public sealed class YQBlueglassAbilityHotbar : MonoBehaviour
{
    private sealed class SlotView
    {
        public Button button;
        public TMP_Text name, status;
        public YQBlueglassMeter progress;
        public YQBlueglassControlFeedback feedback;
    }
    private readonly SlotView[] _slots = new SlotView[2];
    private readonly List<Button> _choices = new List<Button>();
    private RectTransform _root, _picker, _content;
    private CanvasGroup _input;
    private TMP_Text _hint, _pickerTitle, _empty;
    private PlayerState _owner;
    private YQInvestorPlayerMotor _motor;
    private YQInvestorCombat _combat;
    private YQInvestorVitals _vitals;
    private int _hoveredSlot = -1;
    private int _pickerHash;
    private float _nextRender, _leaveAt;
    private float _confirmationUntil;
    private string _confirmation;
    private bool _wasCasting;
    private float _lastAttackCooldown, _lastSpellCooldown;
    public bool PickerOpen => _picker != null && _picker.gameObject.activeSelf;
    public int PickerSlot => _hoveredSlot;
    public RectTransform PresentationRoot => _root;
    public Button GetSlotButton(int index) => index >= 0 && index < _slots.Length ? _slots[index].button : null;

    public static bool IsEligible(SkillRecord skill, int slot)
    {
        // note: Classification is the same typed spell contract used by the menu and combat; locked/passive records cannot become executable loadout choices.
        return skill != null && skill.unlocked && !string.IsNullOrWhiteSpace(skill.skillId) &&
            (slot == 0 || slot == 1) && !string.Equals(skill.type?.Trim(), "passive", StringComparison.OrdinalIgnoreCase) &&
            YQSpellCircleRules.IsSpell(skill) == (slot == 1);
    }
    public static string SlotKey(int slot) => slot == 0 ? "active" : "spell";
    private static SkillRecord Equipped(PlayerState state, int slot)
    {
        if (state?.equippedSkillBySlot == null || !state.equippedSkillBySlot.TryGetValue(SlotKey(slot), out string id)) return null;
        return state.FindSkillById(id);
    }
    private void Awake()
    {
        // note: The local overlay sits above the live branch menu, but its root is explicitly hidden behind every startup/full-pause gate.
        _root = Rect(transform, "AbilityHotbar", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 32), new Vector2(620, 90));
        var canvas = _root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true; canvas.sortingOrder = 5100;
        _root.gameObject.AddComponent<GraphicRaycaster>();
        _input = _root.gameObject.AddComponent<CanvasGroup>();
        _input.blocksRaycasts = false;
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            var slot = new SlotView(); _slots[i] = slot;
            slot.button = ButtonAt(_root, "HotbarSlot_" + i, new Vector2(i * 244, 0), new Vector2(232, 72));
            slot.feedback = slot.button.GetComponent<YQBlueglassControlFeedback>();
            var iconRect = Rect(slot.button.transform, "AbilityIcon", new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(48, 48));
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = YQBlueglassStyle.Sprite(i == 0 ? "Portrait_Crescent" : "Portrait_Crystal");
            icon.preserveAspect = true; icon.raycastTarget = false;
            Text(slot.button.transform, "Binding", i == 0 ? "LMB  /  ACTIVE" : "RMB  /  SPELL", 13, new Vector2(68, -8), new Vector2(156, 18));
            slot.name = Text(slot.button.transform, "AbilityName", "Empty", 20, new Vector2(68, -25), new Vector2(156, 25));
            slot.status = Text(slot.button.transform, "AbilityStatus", "", 13, new Vector2(68, -51), new Vector2(156, 18));
            var progress = Rect(slot.button.transform, "Availability", new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, 4), new Vector2(216, 3));
            slot.progress = progress.gameObject.AddComponent<YQBlueglassMeter>();
            slot.progress.color = YQUITheme.StreamBlue; slot.progress.raycastTarget = false;
            slot.button.onClick.AddListener(() => HoverSlot(index));
        }
        var dash = Rect(_root, "DashHint", new Vector2(0, 1), new Vector2(0, 1), new Vector2(496, 0), new Vector2(124, 72));
        Text(dash, "DashKey", "Q  /  DASH", 16, new Vector2(12, -10), new Vector2(110, 22));
        Text(dash, "DashDetail", "Movement", 14, new Vector2(12, -36), new Vector2(110, 22));
        _hint = Text(_root, "SwapHint", "Hold Alt to swap abilities", 14, new Vector2(0, -76), new Vector2(620, 20));
        _picker = Rect(_root, "AbilityPicker", new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 6), new Vector2(476, 150));
        var background = _picker.gameObject.AddComponent<Image>();
        YQBlueglassStyle.Panel(background);
        _pickerTitle = Text(_picker, "PickerTitle", "", 21, new Vector2(16, -12), new Vector2(444, 30));
        var viewport = Rect(_picker, "Viewport", new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -48), new Vector2(452, 90));
        var clip = viewport.gameObject.AddComponent<Image>(); clip.color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        _content = Rect(viewport, "Choices", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(452, 90));
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = _content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        _empty = Text(_content, "EmptyChoices", "No compatible abilities learned yet.", 18, new Vector2(8, -10), new Vector2(436, 64));
        _picker.gameObject.SetActive(false); _root.gameObject.SetActive(false);
    }
    private void LateUpdate()
    {
        bool visible = YourQuestTutorialAutoBootstrap.GameplayRuntimeReady && YourQuestTutorialAutoBootstrap.GameplayPresentationReleased &&
            !RuntimeModalUiBlocker.IsBlocked && !YQTitleScreenUI.StartupPresentationActive;
        if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
        if (!visible) { ClosePicker(); return; }
        PlayerState current = PlayerStateManager.Instance?.state;
        if (!ReferenceEquals(_owner, current)) { _owner = current; ClosePicker(); _confirmation=null; _nextRender = 0; }
        bool editing = YourQuestTutorialMenuUI.IsQuickOpenNow && Application.isFocused;
        _input.blocksRaycasts = editing;
        if (!editing) ClosePicker();
        else if (Mouse.current != null)
        {
            Vector2 pointer = Mouse.current.position.ReadValue();
            int hovered = -1;
            for (int i = 0; i < 2; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_slots[i].button.transform, pointer)) hovered = i;
            if (hovered >= 0) HoverSlot(hovered);
            // note: A short grace interval bridges the six-pixel gap; moving into the picker never collapses it under the cursor.
            else if (PickerOpen && RectTransformUtility.RectangleContainsScreenPoint(_picker, pointer)) _leaveAt = Time.unscaledTime + .25f;
            else if (PickerOpen && Time.unscaledTime > _leaveAt) ClosePicker();
        }
        if (Time.unscaledTime < _nextRender) return;
        _nextRender = Time.unscaledTime + .1f;
        Render();
        if (PickerOpen) RefreshPicker();
    }
    public void HoverSlot(int slot)
    {
        if (slot < 0 || slot > 1 || !YourQuestTutorialMenuUI.IsQuickOpenNow || !Application.isFocused || _owner == null) return;
        _leaveAt = Time.unscaledTime + .25f;
        if (_hoveredSlot == slot && PickerOpen) return;
        _hoveredSlot = slot; _pickerHash = int.MinValue;
        _picker.gameObject.SetActive(true);
        _slots[slot].feedback.Pulse(YQBlueglassCue.Hover, YQBlueglassFeedback.ControllerActive);
        RefreshPicker();
    }
    private void ClosePicker()
    {
        if (PickerOpen) { _picker.gameObject.SetActive(false); YQBlueglassFeedback.Request(YQBlueglassCue.Exit, YQBlueglassFeedback.ControllerActive); }
        _hoveredSlot = -1;
    }
    private void RefreshPicker()
    {
        int hash = _hoveredSlot;
        SkillRecord equipped = Equipped(_owner, _hoveredSlot);
        unchecked
        {
            foreach (SkillRecord skill in _owner.skills)
                if (IsEligible(skill, _hoveredSlot)) hash = hash * 31 + skill.skillId.GetHashCode() + (skill.name?.GetHashCode() ?? 0) + skill.rank + skill.resourceCost + skill.cooldownSeconds.GetHashCode();
            hash = hash * 31 + (equipped?.skillId.GetHashCode() ?? 0);
        }
        if (hash == _pickerHash) return;
        _pickerHash = hash;
        foreach (Button choice in _choices) { choice.gameObject.SetActive(false); Destroy(choice.gameObject); }
        _choices.Clear();
        _pickerTitle.text = _hoveredSlot == 0 ? "Swap active loadout" : "Swap equipped spell";
        foreach (SkillRecord skill in _owner.skills)
        {
            if (!IsEligible(skill, _hoveredSlot)) continue;
            string id = skill.skillId;
            var choice = ButtonAt(_content, "AbilityChoice", new Vector2(0, -_choices.Count * 56), new Vector2(452, 50));
            Text(choice.transform, "ChoiceName", Escape(skill.name), 19, new Vector2(12, -5), new Vector2(422, 25));
            // note: Cost, strength and wind-up use the exact circle rules and equipment bonus consumed by the existing caster.
            int bonus=GeneratedRpgContentService.Instance!=null ? GeneratedRpgContentService.Instance.GetManaBonus(_owner) : 0;
            string detail=_hoveredSlot==1 ? "Circle " + YQSpellCircleRules.GetCircle(skill) + "  /  " + YQSpellCircleRules.GetResourceCost(skill).ToString("0") + " " + YQSpellCircleRules.GetResourceType(skill) + "  /  " + YQSpellCircleRules.GetCastSeconds(skill,bonus).ToString("0.0") + "s  /  PWR " + YQSpellCircleRules.GetPower(skill,bonus) : "Rank " + Mathf.Max(1,skill.rank) + "  /  Active loadout";
            Text(choice.transform, "ChoiceDetail", detail, 13, new Vector2(12, -30), new Vector2(422, 18));
            YQBlueglassStyle.Button(choice, equipped?.skillId == id);
            choice.onClick.AddListener(() => Assign(id));
            // note: Rich previews use the existing menu formatter; only a deliberate choice still assigns a loadout.
            var menu=FindFirstObjectByType<YourQuestTutorialMenuUI>();
            choice.GetComponent<YQBlueglassControlFeedback>().HoverEntered=()=>menu?.PreviewAbility(choice,id);
            _choices.Add(choice);
        }
        _empty.gameObject.SetActive(_choices.Count == 0);
        float height = Mathf.Clamp(_choices.Count * 56, 80, 280);
        _picker.sizeDelta = new Vector2(476, height + 60);
        ((RectTransform)_content.parent).sizeDelta = new Vector2(452, height);
        _content.sizeDelta = new Vector2(452, Mathf.Max(height, _choices.Count * 56));
        _content.anchoredPosition = Vector2.zero;
    }
    private void Assign(string id)
    {
        // note: Revalidate against the current profile at click time; hover, stale choices and Alt release never mutate a saved loadout.
        var manager = PlayerStateManager.Instance;
        SkillRecord skill = manager?.state?.FindSkillById(id);
        if (!YourQuestTutorialMenuUI.IsQuickOpenNow || !ReferenceEquals(_owner, manager?.state) || !IsEligible(skill, _hoveredSlot))
        { if (_hoveredSlot >= 0) _slots[_hoveredSlot].feedback.Pulse(YQBlueglassCue.Reject); return; }
        if (Equipped(_owner, _hoveredSlot)?.skillId != id)
        {
            manager.EquipSkill(SlotKey(_hoveredSlot), id);
            // note: A deliberate swap publishes through the existing paired profile owner; hovering never schedules a save.
            bool saved=YQProfileSaveSystem.Instance!=null ? YQProfileSaveSystem.Instance.SaveActiveProfile() : manager.TrySave(out _);
            if (!saved)
            {
                _confirmation="Equipped for this session; saving failed."; _confirmationUntil=Time.unscaledTime+4;
                _slots[_hoveredSlot].feedback.Pulse(YQBlueglassCue.Reject);
                _nextRender=0; _pickerHash=int.MinValue; RefreshPicker(); return;
            }
        }
        _slots[_hoveredSlot].feedback.Pulse(YQBlueglassCue.Confirm, YQBlueglassFeedback.ControllerActive);
        _confirmation = "Equipped " + Escape(skill.name); _confirmationUntil = Time.unscaledTime + 2;
        _nextRender = 0; _pickerHash = int.MinValue;
        RefreshPicker();
    }
    private void Render()
    {
        YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;
        if (_motor != motor) { _motor = motor; _combat = motor != null ? motor.GetComponent<YQInvestorCombat>() : null; _vitals = motor != null ? motor.GetComponent<YQInvestorVitals>() : null; }
        _hint.text = Time.unscaledTime < _confirmationUntil && _confirmation!=null ? _confirmation : YourQuestTutorialMenuUI.IsQuickOpenNow ? "Hover a slot  /  Click an ability to equip" : "Hold Alt to swap abilities";
        for (int i = 0; i < 2; i++)
        {
            SkillRecord skill = Equipped(_owner, i);
            _slots[i].name.text = skill != null ? Escape(skill.name) : i == 0 ? "Basic attack" : "No spell equipped";
            float cooldown = i == 0 ? _combat?.AttackCooldownRemaining ?? 0 : _combat?.SpellCooldownRemaining ?? 0;
            bool casting = i == 1 && _combat != null && _combat.IsCasting;
            bool affordable = i == 0 || skill == null || _vitals != null &&
                (YQSpellCircleRules.GetResourceType(skill) == "stamina" ? _vitals.CurrentStamina : _vitals.CurrentMana) >= YQSpellCircleRules.GetResourceCost(skill);
            _slots[i].status.text = casting ? "CASTING " + Mathf.RoundToInt(_combat.CastProgress * 100) + "%" : cooldown > 0 ? cooldown.ToString("0.0") + "s" :
                !affordable ? "Insufficient resource" : i == 1 && skill != null ? YQSpellCircleRules.GetResourceCost(skill).ToString("0") + " " + YQSpellCircleRules.GetResourceType(skill) : skill == null && i == 1 ? "Alt / assign" : "Ready";
            _slots[i].status.color = affordable ? YQUITheme.Pearl : new Color(1f, .65f, .24f);
            float duration = i == 0 ? _combat?.attackCooldown ?? 1 : skill != null && skill.cooldownSeconds > 0 ? skill.cooldownSeconds : _combat?.spellCooldown ?? 1;
            _slots[i].progress.SetValue(casting ? _combat.CastProgress : 1 - Mathf.Clamp01(cooldown / Mathf.Max(.1f, duration)), 1);
            if (i == 1 && skill == null) _slots[i].progress.SetValue(0, 1);
            YQBlueglassStyle.Button(_slots[i].button, _hoveredSlot == i && PickerOpen);
            float previous = i == 0 ? _lastAttackCooldown : _lastSpellCooldown;
            if (cooldown > previous + .05f || casting && !_wasCasting) _slots[i].feedback.Pulse(YQBlueglassCue.Press);
            if (i == 0) _lastAttackCooldown = cooldown; else _lastSpellCooldown = cooldown;
        }
        _wasCasting = _combat != null && _combat.IsCasting;
    }
    private static string Escape(string value) => (value ?? "Ability").Replace("<", "\u2039").Replace(">", "\u203a");
    private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        result.SetParent(parent, false); result.anchorMin = result.anchorMax = anchor;
        result.pivot = pivot; result.anchoredPosition = position; result.sizeDelta = size;
        return result;
    }
    private static Button ButtonAt(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), position, size);
        var image = rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        YQBlueglassStyle.Button(button, false); return button;
    }
    private static TMP_Text Text(Transform parent, string name, string value, float size, Vector2 position, Vector2 dimensions)
    {
        var rect = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), position, dimensions);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        YQUITheme.ApplyText(text); text.fontSize = size; text.text = value; text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis; text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
    private void OnDisable() { ClosePicker(); if (_root != null) _root.gameObject.SetActive(false); }
}
