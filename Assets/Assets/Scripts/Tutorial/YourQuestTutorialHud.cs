// Assets/Assets/Scripts/Tutorial/YourQuestTutorialHud.cs
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YourQuestTutorialHud : MonoBehaviour
{
    private Canvas _canvas;
    // note: Startup diagnostics observe the actual rendered HUD gate rather than inferring UI visibility from service readiness.
    public bool IsPresentationVisible => isActiveAndEnabled && _canvas != null && _canvas.isActiveAndEnabled;
    private TMP_Text _identityText;
    private TMP_Text _healthValueText;
    private TMP_Text _staminaValueText;
    private TMP_Text _manaValueText;
    private TMP_Text _objectiveBodyText;
    private TMP_Text _worldBodyText;
    private TMP_Text _characterBodyText;
    private TMP_Text _promptText;
    private TMP_Text _inventoryToastText;
    private YQBlueglassMeter _healthFill;
    private YQBlueglassMeter _staminaFill;
    private YQBlueglassMeter _manaFill;
    private YQBlueglassMeter _experienceFill;
    private TMP_Text _experienceValueText;
    private TMP_Text _questNameText;
    private Image _crosshairVertical;
    private Image _crosshairHorizontal;
    private RawImage _playerBadgeImage;
    private RawImage _questIconImage;
    private RectTransform _questBanner;

    private const float ReferenceRefreshInterval = 0.75f;
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
    private YQRuntime2DArtRegistry _artRegistry;
    private string _lastPlayerBadgeKey = string.Empty;

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

    private IEnumerator Start()
    {
        // note: Curated journal and class art is requested only after gameplay presentation, so decorative UI cannot compete with world construction or the Goddess loading animation.
        while (!YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
            yield return null;

        ResourceRequest request =
            Resources.LoadAsync<YQRuntime2DArtRegistry>(
                "YQRuntime2DArtRegistry");

        yield return request;

        _artRegistry =
            request.asset as
                YQRuntime2DArtRegistry;

        if (_artRegistry != null &&
            _questIconImage != null &&
            _artRegistry.TryGetTexture(
                "quest_scroll",
                out Texture2D questTexture))
        {
            _questIconImage.texture =
                questTexture;

            _questIconImage.enabled =
                true;
        }
    }

    private void LateUpdate()
    {
        bool gameplayHudVisible =
            YourQuestTutorialAutoBootstrap.GameplayRuntimeReady &&
            YourQuestTutorialAutoBootstrap.GameplayPresentationReleased &&
            !RuntimeModalUiBlocker.IsBlocked;
        if (_canvas != null && _canvas.enabled != gameplayHudVisible)
            _canvas.enabled = gameplayHudVisible;
        // note: The quest banner belongs to the held-Alt information layer; ordinary play retains an unobstructed world view.
        bool showQuest=gameplayHudVisible && YourQuestTutorialMenuUI.IsQuickOpenNow;
        if (_questBanner!=null && _questBanner.gameObject.activeSelf!=showQuest) _questBanner.gameObject.SetActive(showQuest);
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
        // note: Experience is the accepted level-relative counter, not a guessed cumulative threshold.
        SetBar(_experienceFill,_experienceValueText,state.xp,PlayerState.GetXpRequiredForLevel(state.level));
        _healthFill.color=currentHealth<=maxHealth*.25f ? new Color(1f,.55f,.30f) : YQUITheme.StreamBlue;

        string className =
            GetLatestClass(state);

        string titleName =
            GetLatestTitle(state);

        SetTextIfChanged(
            _identityText,
            "<color=#F1F9FF>" +
                Escape(state.displayName) +
                "</color>   <size=75%><color=#BCEAFF>LV " + state.level + "</color></size>\n" +
            "<size=68%><color=#B3D7E8>" +
                Escape(className) +
                "  \u00B7  " +
                Escape(titleName) +
                "</color></size>");

        UpdatePlayerBadge(
            state,
            className,
            titleName);

        QuestRecord activeQuest = state.GetActiveQuest();
        if (activeQuest != null)
        {
            // note: The tracker presents the next incomplete structured objective; journal prose never creates progress or controls.
            SetTextIfChanged(_questNameText,Escape(SafeLine(activeQuest.name)));
            string next=string.Empty;
            int completed=0;
            foreach (QuestObjectiveRecord objective in activeQuest.objectives)
            {
                if (objective==null) continue;
                if (objective.completed) completed++;
                else if (string.IsNullOrEmpty(next)) next=!string.IsNullOrWhiteSpace(objective.description) ? objective.description : objective.targetName;
            }
            if (string.IsNullOrWhiteSpace(next)) next=activeQuest.objectives.Count>0 && completed==activeQuest.objectives.Count ? "Objectives complete. Open the journal for the next step." : BuildQuestHint(activeQuest);
            SetTextIfChanged(_objectiveBodyText,Escape(SafeLine(next)));
            SetTextIfChanged(_worldBodyText,(activeQuest.objectives.Count>0 ? completed+" / "+activeQuest.objectives.Count+" objectives   \u00B7   " : string.Empty)+Escape(state.currentRegionName)+"   \u00B7   Alt \u2192 Journal");
        }
        else
        {
            string objective = _director != null ? _director.CurrentObjective : "Talk to the archivist and begin the tutorial loop.";
            SetTextIfChanged(_questNameText,"Journey");
            SetTextIfChanged(_objectiveBodyText,Escape(SafeLine(objective)));
            SetTextIfChanged(_worldBodyText,Escape(state.currentRegionName)+"   \u00B7   Alt \u2192 Journal");
        }

        SetTextIfChanged(_characterBodyText,"GOLD  "+state.currency+"   \u00B7   "+(YQInvestorPlayerMotor.ActiveMotor?.firstPerson==true ? "FIRST PERSON" : "THIRD PERSON"));

        string prompt = ResolveInteractionPrompt();
        bool promptVisible = !string.IsNullOrWhiteSpace(prompt) && !RuntimeModalUiBlocker.IsBlocked && !YourQuestTutorialMenuUI.CapturesPointerInput;
        _promptText.transform.parent.gameObject.SetActive(promptVisible);
        if (promptVisible)
            SetTextIfChanged(_promptText, prompt);

        bool showCrosshair = !RuntimeModalUiBlocker.IsBlocked && !YourQuestTutorialMenuUI.CapturesPointerInput;
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

    private void UpdatePlayerBadge(
        PlayerState state,
        string className,
        string titleName)
    {
        if (_artRegistry == null ||
            _playerBadgeImage == null ||
            state == null)
        {
            return;
        }

        string semantic =
            SafeLine(className) +
            " " +
            SafeLine(titleName) +
            " " +
            SafeLine(state.displayName);

        if (!_artRegistry.TryPickKey(
                YQCurated2DArtCatalog.KindClassBadge,
                semantic,
                SafeLine(state.displayName),
                "class_warrior",
                out string badgeKey) ||
            string.Equals(
                badgeKey,
                _lastPlayerBadgeKey,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_artRegistry.TryGetTexture(
                badgeKey,
                out Texture2D badgeTexture))
        {
            _lastPlayerBadgeKey =
                badgeKey;

            _playerBadgeImage.texture =
                badgeTexture;

            _playerBadgeImage.enabled =
                true;
        }

        // note: Generated class text chooses only a semantic registry key; the runtime registry remains the authority for the approved badge texture.
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

        // note: The open silhouette follows the accepted floating HUD: identity beside the emblem, resources on a shared rail, XP as a quiet underline.
        RectTransform vitalsPanel=CreatePanel(canvasGo.transform,"VitalsPanel",new Vector2(0,1),new Vector2(760,212),new Vector2(30,-28),Color.clear);
        RectTransform badgeFrame=CreatePanel(vitalsPanel,"ClassBadgeFrame",new Vector2(0,1),new Vector2(70,70),new Vector2(0,-2),Color.clear);
        _playerBadgeImage=CreateRawImage(badgeFrame,"ClassBadgeArt",new Vector2(4,4),new Vector2(-4,-4));
        _playerBadgeImage.enabled=false;
        _identityText=CreateText(vitalsPanel,"IdentityText",28,FontStyles.Bold,TextAlignmentOptions.TopLeft,new Vector2(90,-2),new Vector2(640,58));
        _identityText.overflowMode=TextOverflowModes.Ellipsis;
        _identityText.maxVisibleLines=2;
        ApplyFloatingTextContrast(_identityText);
        CreateBar(vitalsPanel,"HP",new Vector2(90,-68),YQUITheme.StreamBlue,out _healthFill,out _healthValueText,24f);
        CreateBar(vitalsPanel,"STA",new Vector2(90,-102),new Color(.35f,.70f,1f),out _staminaFill,out _staminaValueText,17f);
        CreateBar(vitalsPanel,"MP",new Vector2(90,-130),new Color(.46f,.55f,1f),out _manaFill,out _manaValueText,17f);
        CreateBar(vitalsPanel,"XP",new Vector2(90,-158),YQUITheme.Pearl,out _experienceFill,out _experienceValueText,5f);
        _characterBodyText=CreateText(vitalsPanel,"CharacterDetailsText",17,FontStyles.Normal,TextAlignmentOptions.TopLeft,new Vector2(90,-188),new Vector2(640,24));
        _characterBodyText.overflowMode=TextOverflowModes.Ellipsis;
        ApplyFloatingTextContrast(_characterBodyText);

        // note: The compact tracker contains the accepted name, next objective and progress; long narrative remains in the journal.
        RectTransform objectivePanel=CreatePanel(canvasGo.transform,"ObjectivePanel",new Vector2(1,1),new Vector2(590,224),new Vector2(-28,-28),YQUITheme.Panel);
        _questBanner=objectivePanel;
        objectivePanel.gameObject.SetActive(false);
        YQBlueglassStyle.Panel(objectivePanel.GetComponent<Image>());
        RectTransform questIconFrame=CreatePanel(objectivePanel,"QuestIconFrame",new Vector2(0,1),new Vector2(52,52),new Vector2(20,-22),YQUITheme.PanelSoft);
        _questIconImage=CreateRawImage(questIconFrame,"QuestIconArt",new Vector2(2,2),new Vector2(-2,-2));
        _questIconImage.enabled=false;
        _questNameText=CreateText(objectivePanel,"QuestName",25,FontStyles.Bold,TextAlignmentOptions.TopLeft,new Vector2(86,-20),new Vector2(482,60));
        _questNameText.overflowMode=TextOverflowModes.Ellipsis;
        _questNameText.textWrappingMode=TextWrappingModes.Normal;
        _questNameText.maxVisibleLines=2;
        CreateDivider(objectivePanel,new Vector2(22,-88),546,YQUITheme.GoldDim);
        _objectiveBodyText=CreateText(objectivePanel,"ObjectiveBodyText",23,FontStyles.Normal,TextAlignmentOptions.TopLeft,new Vector2(22,-106),new Vector2(546,72));
        SetTextWrapping(_objectiveBodyText);
        _objectiveBodyText.overflowMode=TextOverflowModes.Ellipsis;
        _objectiveBodyText.maxVisibleLines=2;
        _worldBodyText=CreateText(objectivePanel,"WorldBodyText",18,FontStyles.Normal,TextAlignmentOptions.TopLeft,new Vector2(22,-190),new Vector2(546,28));
        _worldBodyText.overflowMode=TextOverflowModes.Ellipsis;

        RectTransform promptPanel=CreatePanel(canvasGo.transform,"PromptPanel",new Vector2(.5f,.5f),new Vector2(560,56),new Vector2(0,-92),YQUITheme.PanelSoft);
        YQBlueglassStyle.Panel(promptPanel.GetComponent<Image>());
        _promptText=CreateTextStretch(promptPanel,"PromptText",23,FontStyles.Bold,TextAlignmentOptions.Center);
        _promptText.margin=new Vector4(12,4,12,4);
        RectTransform toastPanel=CreatePanel(canvasGo.transform,"InventoryToast",new Vector2(.5f,1),new Vector2(680,52),new Vector2(0,-250),YQUITheme.PanelSoft);
        YQBlueglassStyle.Panel(toastPanel.GetComponent<Image>());
        _inventoryToastText=CreateTextStretch(toastPanel,"InventoryToastText",21,FontStyles.Normal,TextAlignmentOptions.Center);
        _inventoryToastText.margin=new Vector4(12,4,12,4);

        RectTransform crosshairRoot = CreatePanel(canvasGo.transform, "CrosshairRoot", new Vector2(0.5f, 0.5f), new Vector2(24f, 24f), Vector2.zero, new Color(0f, 0f, 0f, 0f));
        _crosshairVertical = CreateCrosshairSegment(crosshairRoot, "CrosshairVertical", new Vector2(2f, 16f));
        _crosshairHorizontal = CreateCrosshairSegment(crosshairRoot, "CrosshairHorizontal", new Vector2(16f, 2f));
        // note: The hotbar observes this HUD's release gates and equips through the existing profile-owned loadout.
        canvasGo.AddComponent<YQBlueglassAbilityHotbar>();
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
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private static RawImage CreateRawImage(
        Transform parent,
        string objectName,
        Vector2 insetMin,
        Vector2 insetMax)
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(RawImage));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = insetMin;
        rt.offsetMax = insetMax;
        RawImage image = go.GetComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        return image;
    }

    private static void CreateDivider(
        Transform parent,
        Vector2 anchoredPosition,
        float width,
        Color color)
    {
        RectTransform divider = CreatePanel(
            parent,
            "RpgDivider",
            new Vector2(0f, 1f),
            new Vector2(width, 1f),
            anchoredPosition,
            color);
        divider.GetComponent<Image>().raycastTarget = false;

        // note: Static dividers establish the resource and objective hierarchy without animation, layout rebuilds, or per-frame decoration work.
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

    private static void CreateBar(Transform parent,string label,Vector2 pos,Color color,out YQBlueglassMeter meter,out TMP_Text value,float height=18f)
    {
        TMP_Text caption=CreateText(parent,label+"Label",18,FontStyles.Bold,TextAlignmentOptions.TopLeft,pos,new Vector2(46,28));
        caption.color=YQUITheme.Pearl;
        caption.text=label;
        ApplyFloatingTextContrast(caption);
        var go=new GameObject(label+"Meter",typeof(RectTransform),typeof(YQBlueglassMeter));
        var rect=go.GetComponent<RectTransform>();
        rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=pos+new Vector2(52,-3);
        rect.sizeDelta=new Vector2(420,height);
        meter=go.GetComponent<YQBlueglassMeter>();
        meter.color=color;
        meter.raycastTarget=false;
        value=CreateText(parent,label+"Value",20,FontStyles.Normal,TextAlignmentOptions.TopRight,pos+new Vector2(490,0),new Vector2(126,28));
        value.color=YQUITheme.Pearl;
        ApplyFloatingTextContrast(value);
    }

    private static void ApplyFloatingTextContrast(TMP_Text text)
    {
        // note: TMP creates a per-view material for these properties; imported shared font materials remain untouched.
        text.outlineColor=new Color32(5,16,30,240);
        text.outlineWidth=.18f;
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

    private static void SetBar(YQBlueglassMeter meter,TMP_Text text,float current,float maximum)
    {
        if (meter!=null) meter.SetValue(current,maximum);
        if (text!=null) SetTextIfChanged(text,Mathf.RoundToInt(Mathf.Clamp(current,0,Mathf.Max(0,maximum)))+" / "+Mathf.RoundToInt(Mathf.Max(0,maximum)));
    }

    private static void SetTextIfChanged(TMP_Text text,string value)
    {
        if (text!=null && text.text!=value) text.text=value;
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
