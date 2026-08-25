// Assets/Assets/Scripts/Tutorial/YourQuestTutorialHud.cs
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YourQuestTutorialHud : MonoBehaviour
{
    private Canvas _canvas;
    private TMP_Text _identityText;
    private TMP_Text _healthValueText;
    private TMP_Text _staminaValueText;
    private TMP_Text _manaValueText;
    private TMP_Text _objectiveBodyText;
    private TMP_Text _worldBodyText;
    private TMP_Text _promptText;
    private TMP_Text _inventoryToastText;
    private Image _healthFill;
    private Image _staminaFill;
    private Image _manaFill;
    private Image _crosshairVertical;
    private Image _crosshairHorizontal;

    private const float ReferenceRefreshInterval = 0.75f;
    private readonly StringBuilder _worldBuilder = new StringBuilder(512);
    private readonly RaycastHit[] _interactionHits = new RaycastHit[16];

    private GeneratedRpgContentService _content;
    private WorldStateManager _worldStateManager;
    private YQInvestorVitals _vitals;
    private YQInvestorDirector _director;
    private Camera _viewCamera;
    private GameObject _player;
    private float _nextReferenceRefreshTime;
    private float _nextPromptProbeTime;
    private float _nextRenderTime;
    private string _cachedPrompt = string.Empty;

    [Header("Performance")]
    [Tooltip("The HUD builds rich-text strings, so refresh at a responsive bounded cadence instead of allocating every rendered frame.")]
    [Range(0.033f, 0.25f)] public float renderIntervalSeconds = 0.1f;

    private string _lastInventoryMessage = string.Empty;
    private float _lastInventoryMessageTime = -999f;

    private void Awake()
    {
        BuildUi();
        ResolveRuntimeReferences();
    }

    private void LateUpdate()
    {
        bool gameplayHudVisible =
            YourQuestTutorialAutoBootstrap.GameplayRuntimeReady &&
            YourQuestTutorialAutoBootstrap.GameplayPresentationReleased;
        if (_canvas != null && _canvas.enabled != gameplayHudVisible)
            _canvas.enabled = gameplayHudVisible;
        if (!gameplayHudVisible)
            return;

        // note: Gameplay HUD rendering and reference probes stay dormant behind the title presentation instead of leaking through its menu.
        if (Time.unscaledTime >= _nextReferenceRefreshTime)
            ResolveRuntimeReferences();

        if (Time.unscaledTime < _nextRenderTime)
            return;

        // note: UI state does not need a 60+ Hz rebuild; this prevents per-frame rich-text allocation during play.
        _nextRenderTime = Time.unscaledTime + Mathf.Max(0.033f, renderIntervalSeconds);
        Render();
    }

    private void Render()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return;

        PlayerState state = psm.state;
        state.EnsureCollections();

        int maxHealth = _content != null ? _content.GetDerivedMaxHealth(state) : Mathf.Max(1, state.stats.maxHealth);
        int maxStamina = _content != null ? _content.GetDerivedMaxStamina(state) : Mathf.Max(1, state.stats.maxStamina);
        int maxMana = _content != null ? _content.GetDerivedMaxMana(state) : Mathf.Max(1, state.stats.maxMana);

        float currentHealth = _vitals != null ? _vitals.CurrentHealth : maxHealth;
        float currentStamina = _vitals != null ? _vitals.CurrentStamina : maxStamina;
        float currentMana = _vitals != null ? _vitals.CurrentMana : maxMana;

        SetBar(_healthFill, _healthValueText, currentHealth, maxHealth);
        SetBar(_staminaFill, _staminaValueText, currentStamina, maxStamina);
        SetBar(_manaFill, _manaValueText, currentMana, maxMana);

        SetTextIfChanged(_identityText,
            "<color=#F5E8B0>" + Escape(state.displayName) + "</color>\n" +
            "Lvl " + state.level + "   XP " + state.xp + "/" + Mathf.Max(1, state.xp + state.xpToNext) + "   Gold " + state.currency + "\n" +
            "Region  " + Escape(state.currentRegionName));

        QuestRecord activeQuest = state.GetActiveQuest();
        if (activeQuest != null)
        {
            string questBody = "<color=#FFF1B8>" + Escape(SafeLine(activeQuest.name)) + "</color>";
            string detail = SafeLine(activeQuest.description);
            if (!string.IsNullOrWhiteSpace(detail))
                questBody += "\n<size=72%><color=#D6DDE8>" + Escape(detail) + "</color></size>";
            string hint = BuildQuestHint(activeQuest);
            if (!string.IsNullOrWhiteSpace(hint))
                questBody += "\n<size=70%><color=#A7FFCF>Next: " + Escape(hint) + "</color></size>";
            SetTextIfChanged(_objectiveBodyText, questBody);
        }
        else
        {
            string objective = _director != null ? _director.CurrentObjective : "Talk to the archivist and begin the tutorial loop.";
            SetTextIfChanged(_objectiveBodyText, "<color=#FFF1B8>" + Escape(SafeLine(objective)) + "</color>");
        }

        StringBuilder worldBuilder = _worldBuilder;
        worldBuilder.Clear();
        string latestNote = _director != null ? _director.LastDirectorMessage : string.Empty;
        string tension = string.Empty;
        if (_worldStateManager != null && _worldStateManager.State != null)
        {
            tension = _worldStateManager.State.tension.ToString("0.00");
            if (string.IsNullOrWhiteSpace(latestNote))
                latestNote = _worldStateManager.State.lastLLMRationale;
        }

        worldBuilder.AppendLine("Offers  " + state.GetPendingOfferCount() + (string.IsNullOrWhiteSpace(tension) ? string.Empty : "     Tension  " + tension));
        worldBuilder.AppendLine("Class  " + Escape(GetLatestClass(state)));
        worldBuilder.AppendLine("Title  " + Escape(GetLatestTitle(state)));
        worldBuilder.AppendLine("Quest  " + Escape(activeQuest != null ? activeQuest.name : GetLatestQuest(state)));
        worldBuilder.AppendLine("Gear  " + Escape(DescribeItem(state.GetEquippedItem("weapon"))) + "  |  " + Escape(DescribeItem(state.GetEquippedItem("chest"))));
        worldBuilder.AppendLine("Relics  " + Escape(DescribeItem(state.GetEquippedItem("ring_left"))) + "  |  " + Escape(DescribeItem(state.GetEquippedItem("boots"))));
        worldBuilder.Append("Note  " + Escape(Truncate(SafeLine(latestNote), 160)));
        SetTextIfChanged(_worldBodyText, worldBuilder.ToString());

        string prompt = ResolveInteractionPrompt();
        bool promptVisible = !string.IsNullOrWhiteSpace(prompt) && !RuntimeModalUiBlocker.IsBlocked;
        _promptText.transform.parent.gameObject.SetActive(promptVisible);
        if (promptVisible)
            SetTextIfChanged(_promptText, prompt);

        bool showCrosshair = !RuntimeModalUiBlocker.IsBlocked;
        if (_crosshairHorizontal != null) _crosshairHorizontal.enabled = showCrosshair;
        if (_crosshairVertical != null) _crosshairVertical.enabled = showCrosshair;

        string inventoryMessage = _content != null ? _content.LastInventoryMessage : string.Empty;
        if (!string.IsNullOrWhiteSpace(inventoryMessage) && inventoryMessage != _lastInventoryMessage)
        {
            _lastInventoryMessage = inventoryMessage;
            _lastInventoryMessageTime = Time.unscaledTime;
        }

        bool showToast = !string.IsNullOrWhiteSpace(_lastInventoryMessage) && Time.unscaledTime - _lastInventoryMessageTime <= 4f;
        _inventoryToastText.transform.parent.gameObject.SetActive(showToast);
        if (showToast)
            SetTextIfChanged(_inventoryToastText, Escape(_lastInventoryMessage));
    }

    private static string BuildQuestHint(QuestRecord quest)
    {
        if (quest == null)
            return string.Empty;

        switch (quest.questId)
        {
            case "tutorial_01_talk_archivist":
                return "Face Vey and press E.";
            case "tutorial_02_claim_training_kit":
                return "Pick up the bench items, then press 2.";
            case "tutorial_03_restore_at_shrine":
                return "Activate the blue shrine beside the path.";
            case "tutorial_04_open_practice_lock":
                return "Use E on the practice gate or locked cache.";
            case "tutorial_05_wake_mimic":
                return "Open the quiet side cache.";
            case "tutorial_06_cast_spell":
                return "Right click at the focus stone.";
            case "tutorial_07_defeat_and_loot":
                return "Fight an echo, then loot the residue.";
            case "tutorial_08_choose_offer":
                return "Press R to accept or F to decline.";
            case "tutorial_09_cross_snow_gate":
                return "Walk through the north snow gate.";
            case "tutorial_10_report_warden":
                return "Talk to Warden Thorne at the gate.";
            default:
                return BuildFallbackQuestHint(quest);
        }
    }

    private static string BuildFallbackQuestHint(QuestRecord quest)
    {
        string text = ((quest.name ?? string.Empty) + " " + (quest.description ?? string.Empty)).ToLowerInvariant();
        if (HasQuestTag(quest, "dialogue") || ContainsAny(text, "talk", "speak", "report"))
            return "Find the marked person and start dialogue.";
        if (HasQuestTag(quest, "pickup") || ContainsAny(text, "pick up", "pickup", "claim"))
            return "Follow the marker and collect the target.";
        if (HasQuestTag(quest, "equip") || ContainsAny(text, "equip", "gear", "weapon"))
            return "Open your gear rhythm with the hotkeys.";
        if (HasQuestTag(quest, "shrine") || ContainsAny(text, "shrine", "restore", "recover"))
            return "Use the marked shrine or recovery point.";
        if (HasQuestTag(quest, "lockpick") || ContainsAny(text, "lock", "locked", "lockpick"))
            return "Use E on the marked lock.";
        if (HasQuestTag(quest, "mimic") || ContainsAny(text, "mimic", "too-quiet", "too quiet"))
            return "Open the suspicious cache.";
        if (HasQuestTag(quest, "spell") || ContainsAny(text, "spell", "cast", "mana"))
            return "Cast with right click at the marked target.";
        if (HasQuestTag(quest, "combat") || ContainsAny(text, "defeat", "fight", "enemy", "hostile"))
            return "Engage the marked hostile.";
        if (HasQuestTag(quest, "loot") || ContainsAny(text, "loot", "corpse", "residue"))
            return "Loot the marked remains or cache.";
        if (HasQuestTag(quest, "offer") || ContainsAny(text, "offer", "accept", "decline"))
            return "Choose the pending offer.";
        if (HasQuestTag(quest, "region") || ContainsAny(text, "region", "gate", "road"))
            return "Cross into the marked area.";

        return string.Empty;
    }

    private static bool HasQuestTag(QuestRecord quest, string expected)
    {
        if (quest == null || quest.tags == null || string.IsNullOrWhiteSpace(expected))
            return false;

        string expectedLower = expected.ToLowerInvariant();
        for (int i = 0; i < quest.tags.Length; i++)
        {
            string tag = quest.tags[i];
            if (!string.IsNullOrWhiteSpace(tag) && tag.Trim().ToLowerInvariant() == expectedLower)
                return true;
        }

        return false;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(text) || needles == null)
            return false;

        for (int i = 0; i < needles.Length; i++)
        {
            string needle = needles[i];
            if (!string.IsNullOrWhiteSpace(needle) && text.Contains(needle.ToLowerInvariant()))
                return true;
        }

        return false;
    }

    private void ResolveRuntimeReferences()
    {
        _nextReferenceRefreshTime = Time.unscaledTime + ReferenceRefreshInterval;

        if (_content == null)
            _content = GeneratedRpgContentService.Instance;
        if (_worldStateManager == null)
            _worldStateManager = WorldStateManager.Instance;
        if (_vitals == null)
            _vitals = FindFirstObjectByType<YQInvestorVitals>();
        if (_director == null)
            _director = FindFirstObjectByType<YQInvestorDirector>();
        if (_viewCamera == null)
            _viewCamera = Camera.main;
        if (_player == null)
            _player = GameObject.FindWithTag("Player");
    }

    private string BuildInteractionPrompt()
    {
        if (_viewCamera == null || _player == null)
            return string.Empty;

        Transform cameraTransform = _viewCamera.transform;
        string prompt = BuildPromptFromLookProbe(
            Physics.RaycastNonAlloc(cameraTransform.position, cameraTransform.forward, _interactionHits, 3.35f, ~0, QueryTriggerInteraction.Ignore));
        if (!string.IsNullOrWhiteSpace(prompt))
            return prompt;

        return BuildPromptFromLookProbe(
            Physics.SphereCastNonAlloc(cameraTransform.position, 0.14f, cameraTransform.forward, _interactionHits, 3.15f, ~0, QueryTriggerInteraction.Ignore));
    }

    private string ResolveInteractionPrompt()
    {
        if (Time.unscaledTime >= _nextPromptProbeTime)
        {
            _nextPromptProbeTime = Time.unscaledTime + 0.08f;
            _cachedPrompt = BuildInteractionPrompt();
        }

        return _cachedPrompt;
    }

    private string BuildPromptFromLookProbe(int hitCount)
    {
        float bestDistance = float.MaxValue;
        string best = string.Empty;
        int count = Mathf.Min(hitCount, _interactionHits.Length);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _interactionHits[i];
            if (hit.collider == null)
                continue;

            string prompt = BuildPromptFromCollider(hit.collider);
            if (string.IsNullOrWhiteSpace(prompt))
                continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                best = prompt;
            }
        }

        return best;
    }

    private string BuildPromptFromCollider(Collider col)
    {
        if (col == null)
            return string.Empty;

        YQInvestorWorldPickup pickup = col.GetComponentInParent<YQInvestorWorldPickup>();
        if (pickup != null)
            return "<color=#A7FFCF>E</color> Pick Up " + Escape(pickup.DisplayName);

        YQInvestorLootableCorpse corpse = col.GetComponentInParent<YQInvestorLootableCorpse>();
        if (corpse != null)
            return "<color=#A7FFCF>E</color> Loot " + Escape(corpse.DisplayName);

        YQInvestorShrine shrine = col.GetComponentInParent<YQInvestorShrine>();
        if (shrine != null)
            return "<color=#A7FFCF>E</color> Activate " + Escape(shrine.gameObject.name);

        YQLockpickableDoor door = col.GetComponentInParent<YQLockpickableDoor>();
        if (door != null)
            return "<color=#A7FFCF>E</color> " + (door.locked ? "Pick Lock: " : "Open ") + Escape(door.displayName);

        YQLockpickableLoot loot = col.GetComponentInParent<YQLockpickableLoot>();
        if (loot != null)
            return "<color=#A7FFCF>E</color> " + (loot.locked && !loot.mimic ? "Pick Lock: " : "Open ") + Escape(loot.displayName);

        EntityInfo info = col.GetComponentInParent<EntityInfo>();
        NpcDialogueAgent agent = col.GetComponentInParent<NpcDialogueAgent>();
        if (info != null && agent != null && info.hostility != Hostility.Hostile)
            return "<color=#A7FFCF>E</color> Talk to " + Escape(info.displayName);

        return string.Empty;
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("YourQuestTutorialHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 4200;
        // note: A newly constructed canvas defaults visible; suppress it synchronously so it cannot flash for one frame behind the Goddess camera before LateUpdate evaluates the release gate.
        _canvas.enabled = false;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        YQUITheme.ApplyCanvasScaler(scaler);

        RectTransform vitalsPanel = CreatePanel(canvasGo.transform, "VitalsPanel", new Vector2(0f, 0f), new Vector2(548f, 228f), new Vector2(42f, 34f), YQUITheme.Panel);
        AddFrame(vitalsPanel, new Color(0.66f, 0.61f, 0.42f, 0.42f));
        _identityText = CreateText(vitalsPanel, "IdentityText", 21f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(22f, -18f), new Vector2(492f, 70f));
        CreateBar(vitalsPanel, "Health", new Vector2(22f, -102f), new Color(0.76f, 0.18f, 0.2f, 1f), out _healthFill, out _healthValueText);
        CreateBar(vitalsPanel, "Stamina", new Vector2(22f, -146f), new Color(0.2f, 0.68f, 0.28f, 1f), out _staminaFill, out _staminaValueText);
        CreateBar(vitalsPanel, "Mana", new Vector2(22f, -190f), new Color(0.2f, 0.42f, 0.88f, 1f), out _manaFill, out _manaValueText);

        RectTransform objectivePanel = CreatePanel(canvasGo.transform, "ObjectivePanel", new Vector2(1f, 1f), new Vector2(560f, 320f), new Vector2(-26f, -26f), YQUITheme.Panel);
        objectivePanel.gameObject.AddComponent<RectMask2D>();
        AddFrame(objectivePanel, new Color(0.66f, 0.61f, 0.42f, 0.42f));
        TMP_Text objectiveTitle = CreateText(objectivePanel, "ObjectiveTitleText", 18f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(18f, -18f), new Vector2(524f, 28f));
        objectiveTitle.text = "TRIAL STEP";
        _objectiveBodyText = CreateText(objectivePanel, "ObjectiveBodyText", 18f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(18f, -54f), new Vector2(524f, 92f));
        SetTextWrapping(_objectiveBodyText);
        _objectiveBodyText.overflowMode = TextOverflowModes.Ellipsis;
        _objectiveBodyText.maxVisibleLines = 4;
        _worldBodyText = CreateText(objectivePanel, "WorldBodyText", 12.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(18f, -156f), new Vector2(524f, 140f));
        SetTextWrapping(_worldBodyText);
        _worldBodyText.overflowMode = TextOverflowModes.Ellipsis;
        _worldBodyText.maxVisibleLines = 8;

        RectTransform promptPanel = CreatePanel(canvasGo.transform, "PromptPanel", new Vector2(0.5f, 0.5f), new Vector2(520f, 54f), new Vector2(0f, -92f), YQUITheme.PanelSoft);
        AddFrame(promptPanel, new Color(0.66f, 0.61f, 0.42f, 0.35f));
        _promptText = CreateTextStretch(promptPanel, "PromptText", 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        _promptText.margin = new Vector4(12f, 4f, 12f, 4f);

        RectTransform toastPanel = CreatePanel(canvasGo.transform, "InventoryToast", new Vector2(0.5f, 1f), new Vector2(760f, 42f), new Vector2(0f, -18f), YQUITheme.PanelSoft);
        AddFrame(toastPanel, new Color(0.66f, 0.61f, 0.42f, 0.22f));
        _inventoryToastText = CreateTextStretch(toastPanel, "InventoryToastText", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
        _inventoryToastText.color = new Color32(233, 236, 241, 255);
        _inventoryToastText.margin = new Vector4(12f, 4f, 12f, 4f);

        RectTransform crosshairRoot = CreatePanel(canvasGo.transform, "CrosshairRoot", new Vector2(0.5f, 0.5f), new Vector2(24f, 24f), Vector2.zero, new Color(0f, 0f, 0f, 0f));
        _crosshairVertical = CreateCrosshairSegment(crosshairRoot, "CrosshairVertical", new Vector2(2f, 16f));
        _crosshairHorizontal = CreateCrosshairSegment(crosshairRoot, "CrosshairHorizontal", new Vector2(16f, 2f));
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

    private static void AddFrame(RectTransform rt, Color color)
    {
        Outline outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private static TMP_Text CreateText(Transform parent, string name, float size, FontStyles style, TextAlignmentOptions alignment, Vector2 anchoredPosition, Vector2 dimensions)
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
        text.alignment = alignment;
        YQUITheme.ApplyText(text);
        return text;
    }

    private static TMP_Text CreateTextStretch(Transform parent, string name, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        YQUITheme.ApplyText(text);
        return text;
    }

    private static void SetTextWrapping(TMP_Text text)
    {
        if (text == null)
            return;

        text.textWrappingMode = TextWrappingModes.Normal;
    }

    private static void CreateBar(Transform parent, string label, Vector2 anchoredPos, Color fillColor, out Image fillImage, out TMP_Text valueText)
    {
        TMP_Text labelText = CreateText(parent, label + "Label", 15f, FontStyles.Bold, TextAlignmentOptions.TopLeft, anchoredPos, new Vector2(110f, 18f));
        labelText.color = YQUITheme.Muted;
        labelText.text = label;

        RectTransform frame = CreatePanel(parent, label + "Frame", new Vector2(0f, 1f), new Vector2(320f, 22f), anchoredPos + new Vector2(114f, -2f), new Color(0.095f, 0.11f, 0.135f, 1f));
        RectTransform fill = CreatePanel(frame, label + "Fill", new Vector2(0f, 0.5f), new Vector2(320f, 22f), new Vector2(0f, 0f), fillColor);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fillImage = fill.GetComponent<Image>();

        valueText = CreateText(parent, label + "Value", 14f, FontStyles.Normal, TextAlignmentOptions.TopRight, anchoredPos + new Vector2(444f, 0f), new Vector2(78f, 18f));
    }

    private static Image CreateCrosshairSegment(Transform parent, string name, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.85f);
        return img;
    }

    private static void SetBar(Image fill, TMP_Text valueText, float current, float max)
    {
        if (fill != null)
        {
            RectTransform rt = fill.rectTransform;
            rt.sizeDelta = new Vector2(320f * Mathf.Clamp01(max <= 0f ? 0f : current / max), 22f);
        }

        if (valueText != null)
            SetTextIfChanged(valueText, Mathf.RoundToInt(current) + " / " + Mathf.RoundToInt(max));
    }

    private static void SetTextIfChanged(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

    private static string GetLatestClass(PlayerState state)
    {
        if (state.classes == null || state.classes.Count == 0)
            return "<none>";
        ClassRecord record = state.classes[state.classes.Count - 1];
        return record == null ? "<none>" : record.name;
    }

    private static string GetLatestTitle(PlayerState state)
    {
        if (state.titles == null || state.titles.Count == 0)
            return "<none>";
        TitleRecord record = state.titles[state.titles.Count - 1];
        return record == null ? "<none>" : record.name;
    }

    private static string GetLatestQuest(PlayerState state)
    {
        if (state.quests == null || state.quests.Count == 0)
            return "<none>";
        QuestRecord record = state.quests[state.quests.Count - 1];
        return record == null ? "<none>" : record.name;
    }

    private static string DescribeItem(InventoryItemRecord item)
    {
        return item == null ? "<empty>" : item.displayName;
    }

    private static string SafeLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return value.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value) || maxChars <= 0)
            return string.Empty;
        value = value.Trim();
        if (value.Length <= maxChars)
            return value;
        return value.Substring(0, Mathf.Max(0, maxChars - 3)).TrimEnd() + "...";
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
