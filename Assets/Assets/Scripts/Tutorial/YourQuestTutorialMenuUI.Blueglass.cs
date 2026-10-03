using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed partial class YourQuestTutorialMenuUI
{
    private RectTransform _fullRoot;
    private RectTransform _quickRoot;
    private RectTransform _quickCategories;
    private RectTransform _quickEntries;
    private TMP_Text _quickSummary;
    private Button _quickPrimary;
    private Button _quickSecondary;
    private bool _quick;
    private YQBlueglassQuickMenuMotion _quickMotion;
    private bool _animateQuickBranch;
    private int _quickGroup;
    private int _builtQuickGroup=-1;
    private int _entryIndex;
    private int _quickDepth;
    private float _branchLeaveAt;
    private RectTransform _hoverWindow;
    private TMP_Text _hoverTitle;
    private TMP_Text _hoverBody;
    private RawImage _hoverImage;
    private ScrollRect _hoverScroll;
    private Button _hoverSource;
    private string _hoverKey;
    private float _hoverLeaveAt;
    private CursorLockMode _cursorBeforeQuick;
    private bool _cursorVisibleBeforeQuick;
    private static int _presentationChangeFrame = -1;
    private static int _pointerReleaseFrame = -1;
    public static bool IsQuickOpenNow { get; private set; }
    public static bool ChangedThisFrame => _presentationChangeFrame == Time.frameCount;
    private bool IsInventoryTab => _activeTab == MenuTab.Inventory || _activeTab == MenuTab.Equipment;
    private void ConfigureCarriedLayout()
    {
        // note: Reuse the existing scroll/content owner; only inventory switches from rows to native game-item tiles.
        _listContent.GetComponent<YQBlueglassItemLayout>().Grid = IsInventoryTab;
        // note: Character staging belongs only to carried gear; other pages use a readable record browser and full-height inspector.
        _equipmentPanel.gameObject.SetActive(IsInventoryTab);
        var center=(RectTransform)_fullRoot.Find("Center");
        var detail=(RectTransform)_fullRoot.Find("Detail");
        center.anchorMin=center.anchorMax=center.pivot=IsInventoryTab ? new Vector2(1,0) : Vector2.zero;
        center.anchoredPosition=IsInventoryTab ? new Vector2(-16,100) : new Vector2(50,100);
        center.sizeDelta=IsInventoryTab ? new Vector2(620,290) : new Vector2(670,610);
        detail.anchoredPosition=IsInventoryTab ? new Vector2(-16,410) : new Vector2(-16,100);
        detail.sizeDelta=IsInventoryTab ? new Vector2(620,300) : new Vector2(1050,610);
        ((RectTransform)_detailScroll.transform).sizeDelta=IsInventoryTab ? new Vector2(572,150) : new Vector2(1002,454);
        _detailTitleText.rectTransform.sizeDelta=IsInventoryTab ? new Vector2(475,64) : new Vector2(900,64);
        _detailIconFrame.rectTransform.anchoredPosition=new Vector2(IsInventoryTab ? 514 : 944,-14);
    }
    // note: A menu-closing click belongs to UI for the entire frame, even after its modal token has been released.
    public static bool CapturesPointerInput => IsQuickOpenNow || IsOpenNow || ChangedThisFrame || _pointerReleaseFrame == Time.frameCount ||
        (Application.isFocused && !RuntimeModalUiBlocker.IsBlocked && !YQTitleScreenUI.StartupPresentationActive &&
         Keyboard.current != null && (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed));

    public void OpenFullMenu()
    {
        // note: Public presentation entry point reuses the existing structured menu owner and its modal token.
        CloseQuick(false);
        _activeTab = MenuTab.Equipment;
        _selectedKey = string.Empty;
        SetOpen(true);
    }

    private bool UpdateQuickInput(Keyboard keyboard)
    {
        if (_quick && (!Application.isFocused || RuntimeModalUiBlocker.IsBlocked))
        {
            CloseQuick(true);
            return true;
        }
        bool held = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
        if (!_open && !_quick && held && Application.isFocused && !RuntimeModalUiBlocker.IsBlocked && !YQTitleScreenUI.StartupPresentationActive)
            OpenQuick();
        if (!_quick) return false;
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            OpenFullMenu();
            return true;
        }
        if (!held) { CloseQuick(true); return true; }
        // note: Live quantities/equipment refresh at the existing bounded poll rate while simulation remains active.
        if (Time.unscaledTime >= _nextPollTime)
        {
            _nextPollTime = Time.unscaledTime + .35f;
            int hash = ComputeStateHash();
            if (hash != _lastStateHash) { _lastStateHash = hash; _dirty = true; }
        }
        // note: Preserve native pointer ownership until release, even if live gameplay changes a displayed record.
        if (_dirty && !(Mouse.current?.leftButton.isPressed ?? false)) Render();
        return true;
    }

    private void OpenQuick()
    {
        _cursorBeforeQuick = Cursor.lockState;
        _cursorVisibleBeforeQuick = Cursor.visible;
        _quick = true;
        IsQuickOpenNow = true;
        _quickGroup = 0;
        _quickDepth=0;
        _quickMotion.SetDepth(0,true);
        HideRecordPreview();
        _activeTab = MenuTab.Equipment;
        _selectedKey = "slot:weapon";
        _statusMessage = string.Empty;
        _fullRoot.gameObject.SetActive(false);
        _quickRoot.gameObject.SetActive(true);
        _canvas.enabled = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _dirty = true;
        Render();
        _quickMotion.Open();
        YQBlueglassFeedback.Request(YQBlueglassCue.Screen, YQBlueglassFeedback.ControllerActive);
    }

    private void CloseQuick(bool restoreCursor)
    {
        HideRecordPreview();
        if (!_quick)
        {
            if (!restoreCursor || !isActiveAndEnabled || !Application.isFocused || RuntimeModalUiBlocker.IsBlocked) _quickMotion?.Close(FinishQuickClose,true);
            return;
        }
        _quick = false;
        IsQuickOpenNow = false;
        _pointerReleaseFrame = Time.frameCount;
        // note: Input/cursor restore immediately; the inert branch retracts briefly using the unscaled presentation clock.
        _quickMotion?.Close(FinishQuickClose,!restoreCursor || !isActiveAndEnabled || !Application.isFocused || RuntimeModalUiBlocker.IsBlocked);
        if (restoreCursor && !RuntimeModalUiBlocker.IsBlocked)
        {
            Cursor.lockState = _cursorBeforeQuick;
            Cursor.visible = _cursorVisibleBeforeQuick;
        }
        YQBlueglassFeedback.Request(YQBlueglassCue.Screen, YQBlueglassFeedback.ControllerActive);
    }
    private void FinishQuickClose()
    {
        if (_quickRoot!=null) _quickRoot.gameObject.SetActive(false);
        if (_canvas!=null && !_open) _canvas.enabled=false;
    }

    private void OnApplicationFocus(bool focused) { if (!focused) CloseQuick(true); }
    private void OnDisable()
    {
        // note: Disable/reload cannot strand a quick cursor or the full menu's modal token.
        CloseQuick(true);
        if (_open) SetOpen(false);
    }

    private void BuildBlueglassPresentation()
    {
        // note: Replace only this runtime-created layout component; one layout owner handles both grid and record-list views.
        DestroyImmediate(_listContent.GetComponent<VerticalLayoutGroup>());
        _listContent.gameObject.AddComponent<YQBlueglassItemLayout>();
        _fullRoot = (RectTransform)_canvas.transform.Find("Root");
        _fullRoot.GetComponent<Image>().color = Color.clear;
        var dim=CreatePanel(_canvas.transform,"Dim",new Vector2(.5f,.5f),new Vector2(2400,1600),Vector2.zero,new Color(.008f,.022f,.041f,.94f));
        dim.SetAsFirstSibling();
        dim.gameObject.SetActive(false);
        // note: The full composition owns its background; the live quick view remains unobstructed.
        _fullRoot.gameObject.AddComponent<YQBlueglassPauseBackdrop>().Backdrop=dim.gameObject;
        Outline outline = _fullRoot.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        RectTransform tabs = (RectTransform)_fullRoot.Find("Tabs");
        RectTransform header=(RectTransform)_fullRoot.Find("Header");
        header.sizeDelta=new Vector2(1740,96);
        _titleText.rectTransform.sizeDelta=new Vector2(1500,44);
        _titleText.fontSize=38;
        _subtitleText.rectTransform.anchoredPosition=new Vector2(18,-56);
        _subtitleText.fontSize=20;
        tabs.GetComponent<Image>().color = Color.clear;
        // note: One navigation row leaves a large, calm stage for the character and keeps all sections on the same baseline.
        DestroyImmediate(tabs.GetComponent<VerticalLayoutGroup>());
        tabs.anchorMin=tabs.anchorMax=tabs.pivot=new Vector2(0,1);
        tabs.anchoredPosition=new Vector2(50,-116);
        tabs.sizeDelta=new Vector2(1740,52);
        var navigation=tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        navigation.spacing=14;
        navigation.childControlWidth=navigation.childControlHeight=true;
        navigation.childForceExpandWidth=navigation.childForceExpandHeight=false;
        Button system = CreateNavButton(tabs, "System");
        system.onClick.AddListener(() =>
        {
            YourQuestPauseMenuUI pause = FindFirstObjectByType<YourQuestPauseMenuUI>();
            if (pause == null) return;
            pause.OpenSettings();
            SetOpen(false);
        });
        Button resume = CreateNavButton(tabs, "Resume");
        resume.onClick.AddListener(() => SetOpen(false));
        foreach(Button tab in tabs.GetComponentsInChildren<Button>())
        {
            var size=tab.GetComponent<LayoutElement>();
            size.preferredWidth=190; size.preferredHeight=52;
            tab.GetComponentInChildren<TMP_Text>().fontSize=22;
        }

        // note: Leave the world/actual avatar central; cluster inspection and carried items together on the right.
        _equipmentPanel.sizeDelta = new Vector2(1020, 610);
        _equipmentPanel.anchoredPosition = new Vector2(40, 100);
        DestroyImmediate(_equipmentPanel.GetComponent<VerticalLayoutGroup>());
        foreach(TMP_Text label in _equipmentPanel.GetComponentsInChildren<TMP_Text>()) label.gameObject.SetActive(false);
        _equipmentContent.anchorMin=_equipmentContent.anchorMax=_equipmentContent.pivot=new Vector2(0,1);
        _equipmentContent.anchoredPosition=new Vector2(0,-20);
        _equipmentContent.sizeDelta=new Vector2(1020,590);
        _equipmentPanel.GetComponent<Image>().color = Color.clear;
        var avatar = new GameObject("ActualPlayerPresentation", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement), typeof(YQBlueglassPlayerPortrait));
        avatar.transform.SetParent(_equipmentPanel, false);
        avatar.transform.SetAsFirstSibling();
        avatar.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform avatarRect = (RectTransform)avatar.transform;
        avatarRect.anchorMin=avatarRect.anchorMax=avatarRect.pivot=new Vector2(0,0);
        avatarRect.anchoredPosition=new Vector2(310,12);
        avatarRect.sizeDelta=new Vector2(400,600);
        var mode=CreateAbsoluteText(_equipmentPanel,"CameraMode",16,FontStyles.Normal,new Vector2(310,-590),new Vector2(550,24));
        mode.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
        avatar.GetComponent<YQBlueglassPlayerPortrait>().ViewModeText=mode;
        RectTransform center = (RectTransform)_fullRoot.Find("Center");
        center.anchorMin = center.anchorMax = center.pivot = new Vector2(1,0);
        center.sizeDelta = new Vector2(620, 290);
        center.anchoredPosition = new Vector2(-16, 100);
        RectTransform detail = (RectTransform)_fullRoot.Find("Detail");
        detail.sizeDelta = new Vector2(620, 300);
        detail.anchoredPosition = new Vector2(-16, 410);
        RectTransform detailScroll = (RectTransform)detail.Find("DetailScrollRoot");
        detailScroll.sizeDelta = new Vector2(572, 150);
        detailScroll.GetComponent<Image>().color=Color.clear;
        detailScroll.anchoredPosition=new Vector2(24,80);
        _detailBodyText.fontSize=20;
        _primaryButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(16,22);
        _secondaryButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(270,22);
        foreach (Button action in new[]{_primaryButton,_secondaryButton})
        {
            action.GetComponent<RectTransform>().sizeDelta=new Vector2(240,52);
            action.GetComponentInChildren<TMP_Text>().fontSize=22;
        }
        _footerText.fontSize=18;
        YQBlueglassStyle.Panel(header.GetComponent<Image>());
        YQBlueglassStyle.Panel(_fullRoot.Find("Footer").GetComponent<Image>());
        YQBlueglassStyle.Panel(center.GetComponent<Image>());
        _detailIconFrame.rectTransform.anchoredPosition = new Vector2(514, -14);
        _detailTitleText.rectTransform.sizeDelta = new Vector2(475, 64);
        _detailTitleText.textWrappingMode = TextWrappingModes.Normal;
        _detailTitleText.overflowMode = TextOverflowModes.Ellipsis;
        YQBlueglassStyle.Panel(detail.GetComponent<Image>());
        // note: The portrait sits behind slot controls; it renders the actual player, never a second gameplay character.
        foreach (Outline frame in _equipmentPanel.GetComponents<Outline>()) frame.enabled = false;

        // note: Begin below the Alt-only quest banner; the longest branch expands downward without overlapping it or the bottom hotbar.
        _quickRoot = CreatePanel(_canvas.transform, "QuickRoot", new Vector2(1,1), new Vector2(810,520), new Vector2(-40,-270), Color.clear);
        _quickRoot.GetComponent<Image>().raycastTarget=false;
        _quickMotion=_quickRoot.gameObject.AddComponent<YQBlueglassQuickMenuMotion>();
        Button player = CreateButton(_quickRoot, "QuickPlayer", new Vector2(0,1), new Vector2(0,-20), new Vector2(130,64), "Player");
        Button items = CreateButton(_quickRoot, "QuickItems", new Vector2(0,1), new Vector2(0,-100), new Vector2(130,64), "Items");
        Button journal = CreateButton(_quickRoot, "QuickJournal", new Vector2(0,1), new Vector2(0,-180), new Vector2(130,64), "Journal");
        BindBranchHover(player,()=>ChooseQuickRoot(0,MenuTab.Equipment));
        BindBranchHover(items,()=>ChooseQuickRoot(1,MenuTab.Inventory));
        BindBranchHover(journal,()=>ChooseQuickRoot(2,MenuTab.Quests));
        _quickCategories = CreatePanel(_quickRoot,"QuickCategories",new Vector2(0,1),new Vector2(215,320),new Vector2(155,-20),Color.clear);
        RectTransform entriesArea = CreatePanel(_quickRoot,"QuickEntriesArea",new Vector2(0,1),new Vector2(410,320),new Vector2(390,-20),YQUITheme.Panel);
        BuildScroll(entriesArea, out _quickEntries);
        RectTransform summary = CreatePanel(_quickRoot,"QuickSummary",new Vector2(0,0),new Vector2(640,105),new Vector2(155,87),YQUITheme.Panel);
        YQBlueglassStyle.Panel(summary.GetComponent<Image>());
        _quickSummary = CreateAbsoluteText(summary,"SelectedRecord",18,FontStyles.Normal,new Vector2(16,-12),new Vector2(608,81));
        _quickSummary.textWrappingMode = TextWrappingModes.Normal;
        _quickPrimary = CreateButton(_quickRoot,"QuickConfirm",new Vector2(0,0),new Vector2(155,25),new Vector2(240,48),"Equip");
        _quickSecondary = CreateButton(_quickRoot,"QuickSecondary",new Vector2(0,0),new Vector2(410,25),new Vector2(180,48),"Clear slot");
        _quickPrimary.onClick.AddListener(OnPrimaryActionClicked);
        _quickSecondary.onClick.AddListener(OnSecondaryActionClicked);
        foreach (Button rootButton in new[]{player,items,journal}) rootButton.GetComponentInChildren<TMP_Text>().fontSize=24;
        _quickSummary.fontSize=23;
        var hint = CreateAbsoluteText(_quickRoot,"InputHints",20,FontStyles.Normal,new Vector2(155,-503),new Vector2(640,28));
        hint.text = "Hold Alt \u00B7 Cursor     Release Alt \u00B7 Return     Esc \u00B7 Full pause";
        _quickRoot.gameObject.SetActive(false);
        BuildRecordPreview();
    }

    private void ChooseQuickRoot(int group, MenuTab tab)
    {
        if(!_quick) return;
        HideRecordPreview();
        _quickDepth=1;
        _quickMotion.SetDepth(1);
        if(_quickGroup==group && _activeTab==tab) return;
        _animateQuickBranch=true;
        _quickGroup = group;
        if (_activeTab == tab) { _selectedKey = string.Empty; MarkDirty(true); }
        else SetTab(tab);
    }
    private void RefreshQuickPresentation(PlayerState state)
    {
        if (!_quick) return;
        _quickMotion.PrepareLayout();
        // note: Retain native controls across refreshes so hovering, selected focus and scroll position survive a live state poll.
        _entryIndex=0;
        YQUITheme.ApplyButton(_quickRoot.Find("QuickPlayer").GetComponent<Button>(),_quickGroup==0);
        YQUITheme.ApplyButton(_quickRoot.Find("QuickItems").GetComponent<Button>(),_quickGroup==1);
        YQUITheme.ApplyButton(_quickRoot.Find("QuickJournal").GetComponent<Button>(),_quickGroup==2);
        MenuTab[] choices = _quickGroup == 0 ? new[] {MenuTab.Equipment,MenuTab.Skills,MenuTab.Classes,MenuTab.Stats} :
            _quickGroup == 1 ? new[] {MenuTab.Inventory} : new[] {MenuTab.Quests};
        if (_builtQuickGroup!=_quickGroup) { ClearChildren(_quickCategories); _builtQuickGroup=_quickGroup; }
        for (int i=0;i<choices.Length;i++)
        {
            MenuTab tab = choices[i];
            Button button=null;
            foreach (Button existing in _quickCategories.GetComponentsInChildren<Button>())
                if (existing.GetComponentInChildren<TMP_Text>().text==TabLabels[(int)tab]) { button=existing; break; }
            if (button==null) button=CreateButton(_quickCategories,"QuickCategory",new Vector2(0,1),new Vector2(0,-i*74),new Vector2(210,64),TabLabels[(int)tab]);
            button.GetComponentInChildren<TMP_Text>().fontSize=24;
            YQUITheme.ApplyButton(button,_activeTab==tab);
            button.onClick.RemoveAllListeners();
            BindBranchHover(button,()=>
            {
                if(!_quick) return;
                HideRecordPreview();
                _quickDepth=2;
                _quickMotion.SetDepth(2);
                if(_activeTab!=tab) { _animateQuickBranch=true; SetTab(tab); }
            });
        }
        if (_activeTab == MenuTab.Equipment)
        {
            foreach (SlotDef slot in EquipmentSlots)
            {
                string key = "slot:"+slot.SlotId;
                InventoryItemRecord item = state.GetEquippedItem(slot.SlotId);
                AddQuickEntry(slot.Label, item?.displayName ?? "Empty", key, ()=> { _selectedKey=key; MarkDirty(true); });
            }
        }
        else
        {
            // note: Forward the existing menu listeners; compact UI never implements another inventory/quest mutation path.
            for (int i=0;i<_listContent.childCount;i++)
            {
                var source = _listContent.GetChild(i).GetComponent<Button>();
                if (source == null) continue;
                var title = source.transform.Find("Title")?.GetComponent<TMP_Text>();
                var subtitle = source.transform.Find("Subtitle")?.GetComponent<TMP_Text>();
                Button copy = AddQuickEntry(title?.text ?? "Record",subtitle?.text ?? string.Empty,source.GetComponent<YQBlueglassControlFeedback>()?.RecordKey,()=>source.onClick.Invoke());
                YQUITheme.ApplyButton(copy,source.GetComponent<YQBlueglassControlFeedback>()?.Selected ?? false);
            }
        }
        while (_quickEntries.childCount>_entryIndex)
        {
            Transform stale=_quickEntries.GetChild(_quickEntries.childCount-1);
            stale.gameObject.SetActive(false);
            stale.SetParent(null,false);
            Destroy(stale.gameObject);
        }
        // note: A short list collapses to its real content; the selected record and actions stay directly beneath that branch.
        // note: Longer accepted lists unfold toward the available screen height while remaining scrollable; short lists retain their intentional compact grouping.
        float entriesHeight=Mathf.Min(Mathf.Max(108,_entryIndex*84f+24f),540f);
        var entriesArea=(RectTransform)_quickRoot.Find("QuickEntriesArea");
        entriesArea.sizeDelta=new Vector2(410,entriesHeight);
        var summary=(RectTransform)_quickSummary.transform.parent;
        summary.anchorMin=summary.anchorMax=summary.pivot=new Vector2(0,1);
        summary.sizeDelta=new Vector2(410,92);
        summary.anchoredPosition=new Vector2(390,-32-entriesHeight);
        _quickSummary.rectTransform.sizeDelta=new Vector2(378,68);
        foreach(Button action in new[]{_quickPrimary,_quickSecondary})
            action.GetComponent<RectTransform>().anchorMin=action.GetComponent<RectTransform>().anchorMax=action.GetComponent<RectTransform>().pivot=new Vector2(0,1);
        _quickPrimary.GetComponent<RectTransform>().anchoredPosition=new Vector2(390,-136-entriesHeight);
        _quickPrimary.GetComponent<RectTransform>().sizeDelta=new Vector2(228,46);
        _quickSecondary.GetComponent<RectTransform>().anchoredPosition=new Vector2(630,-136-entriesHeight);
        _quickSecondary.GetComponent<RectTransform>().sizeDelta=new Vector2(170,46);
        float bottom=Mathf.Max(320,entriesHeight+190);
        var inputHints=(RectTransform)_quickRoot.Find("InputHints");
        inputHints.anchoredPosition=new Vector2(390,-bottom);
        inputHints.sizeDelta=new Vector2(410,42);
        inputHints.GetComponent<TMP_Text>().fontSize=16;
        _quickRoot.sizeDelta=new Vector2(810,bottom+36);
        _quickMotion.CaptureLayout(entriesArea,_quickGroup,_animateQuickBranch);
        _animateQuickBranch=false;
        string firstLine = _detailBodyText.text ?? string.Empty;
        int line = firstLine.IndexOf('\n');
        if (line >= 0) firstLine = firstLine.Substring(0,line);
        _quickSummary.text = string.IsNullOrWhiteSpace(_statusMessage) ? (_entryIndex==0 ? "Nothing here yet." : _detailTitleText.text+"\n"+firstLine) : _statusMessage;
        // note: Rich hover inspection replaces the permanently pinned summary under the live branch.
        summary.gameObject.SetActive(false);
    }
    private Button AddQuickEntry(string title,string subtitle,string key,UnityEngine.Events.UnityAction select)
    {
        Button button=_entryIndex<_quickEntries.childCount ? _quickEntries.GetChild(_entryIndex).GetComponent<Button>() : null;
        if (button==null) button=CreateListButton(_quickEntries,title,subtitle);
        _entryIndex++;
        var heading=button.transform.Find("Title").GetComponent<TMP_Text>();
        var sub=button.transform.Find("Subtitle").GetComponent<TMP_Text>();
        heading.text=title;
        heading.fontSize=23;
        heading.rectTransform.sizeDelta=new Vector2(-24,30);
        sub.text=subtitle;
        sub.fontSize=20;
        sub.rectTransform.anchoredPosition=new Vector2(12,-38);
        sub.rectTransform.sizeDelta=new Vector2(-24,27);
        button.GetComponent<LayoutElement>().preferredHeight=76;
        YQUITheme.ApplyButton(button,!string.IsNullOrEmpty(key) && key==_selectedKey);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(()=>
        {
            select();
            _quickDepth=3;
            _quickMotion.SetDepth(3);
        });
        BindRecordHover(button,key);
        return button;
    }
    private void SyncQuickActions(bool primary,bool secondary,string primaryText,string secondaryText)
    {
        if (_quickPrimary == null) return;
        _quickPrimary.gameObject.SetActive(primary);
        _quickSecondary.gameObject.SetActive(secondary);
        _quickPrimary.GetComponentInChildren<TMP_Text>(true).text=primaryText;
        _quickSecondary.GetComponentInChildren<TMP_Text>(true).text=secondaryText;
        _quickPrimary.interactable = _primaryButton.interactable;
        // note: Live action availability updates the visual state immediately, before the cursor reaches the control.
        _quickSecondary.interactable = _secondaryButton.interactable;
        _quickPrimary.GetComponent<YQBlueglassControlFeedback>()?.RefreshHover();
        _quickSecondary.GetComponent<YQBlueglassControlFeedback>()?.RefreshHover();
    }
    private static bool IsItemEquipped(PlayerState state,string itemId)
    {
        foreach (string id in state.equippedItemBySlot.Values)
            if (string.Equals(id,itemId,StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    private void PulseAction(bool accepted)
    {
        // note: Confirmation follows an accepted mutation; rejection keeps the record and displays its actual reason.
        Button source = _quick ? _quickPrimary : _primaryButton;
        source?.GetComponent<YQBlueglassControlFeedback>()?.Pulse(accepted ? YQBlueglassCue.Confirm : YQBlueglassCue.Reject,YQBlueglassFeedback.ControllerActive);
    }

    private static void BindBranchHover(Button button,Action entered)
    {
        var feedback=button.GetComponent<YQBlueglassControlFeedback>();
        feedback.HoverEntered=entered;
    }
    private void BindRecordHover(Button button,string key)
    {
        var feedback=button.GetComponent<YQBlueglassControlFeedback>();
        feedback.RecordKey=key;
        feedback.HoverEntered=()=>ShowRecordPreview(button,key);
        feedback.HoverExited=()=> { if(_hoverSource==button) _hoverLeaveAt=Time.unscaledTime+.20f; };
    }
    private void BuildRecordPreview()
    {
        _fullRoot.gameObject.AddComponent<YQBlueglassPanelReveal>();
        // note: One transient inspector reuses accepted record formatting and artwork; it never equips, casts or commits state.
        _hoverWindow=CreatePanel(_canvas.transform,"RecordPreview",Vector2.zero,new Vector2(450,430),Vector2.zero,YQUITheme.Panel);
        var previewCanvas=_hoverWindow.gameObject.AddComponent<Canvas>();
        previewCanvas.overrideSorting=true;
        previewCanvas.sortingOrder=5200;
        _hoverWindow.gameObject.AddComponent<GraphicRaycaster>();
        _hoverWindow.gameObject.AddComponent<YQBlueglassPanelReveal>();
        YQBlueglassStyle.Panel(_hoverWindow.GetComponent<Image>());
        _hoverTitle=CreateAbsoluteText(_hoverWindow,"PreviewTitle",26,FontStyles.Bold,new Vector2(20,-20),new Vector2(316,68));
        _hoverTitle.textWrappingMode=TextWrappingModes.Normal;
        _hoverImage=CreateRawIcon(_hoverWindow,"PreviewImage",new Vector2(358,-20),new Vector2(72,72));
        var body=CreatePanel(_hoverWindow,"PreviewScroll",Vector2.zero,new Vector2(426,310),new Vector2(12,16),Color.clear);
        _hoverScroll=BuildScroll(body,out RectTransform content);
        _hoverBody=CreateTextStretch(content,"PreviewBody",21,FontStyles.Normal,TextAlignmentOptions.TopLeft);
        _hoverBody.textWrappingMode=TextWrappingModes.Normal;
        _hoverWindow.gameObject.SetActive(false);
    }
    private void ShowRecordPreview(Button source,string key)
    {
        if(string.IsNullOrEmpty(key) || (!_open && !_quick) || PlayerStateManager.Instance?.state==null) return;
        if(_hoverSource==source && _hoverKey==key && _hoverWindow.gameObject.activeSelf) { _hoverLeaveAt=0; return; }
        _hoverSource=source; _hoverKey=key; _hoverLeaveAt=0;
        var state=PlayerStateManager.Instance.state;
        string selected=_selectedKey;
        string detailKey=_lastDetailKey;
        float scroll=_detailScroll.verticalNormalizedPosition;
        // note: Borrow the existing presentation formatter temporarily; selection and action targets are restored before returning to input dispatch.
        try
        {
            _selectedKey=key;
            RebuildDetails(state,WorldStateManager.Instance?.State,GeneratedRpgContentService.Instance);
            _hoverTitle.text=_detailTitleText.text;
            _hoverBody.text=_detailBodyText.text;
            _hoverImage.texture=_detailIconImage.texture;
            _hoverImage.uvRect=_detailIconImage.uvRect;
            _hoverImage.enabled=_detailIconImage.enabled;
            if(!_hoverImage.enabled && key.StartsWith("skill:",StringComparison.Ordinal))
            {
                var skill=GetSelectedSkill(state);
                var sprite=YQBlueglassStyle.Sprite(YQSpellCircleRules.IsSpell(skill) ? "Portrait_Crystal" : "Portrait_Crescent");
                if(sprite!=null)
                {
                    _hoverImage.texture=sprite.texture;
                    Rect r=sprite.rect;
                    _hoverImage.uvRect=new Rect(r.x/sprite.texture.width,r.y/sprite.texture.height,r.width/sprite.texture.width,r.height/sprite.texture.height);
                    _hoverImage.enabled=true;
                }
            }
        }
        finally
        {
            _selectedKey=selected;
            RebuildDetails(state,WorldStateManager.Instance?.State,GeneratedRpgContentService.Instance);
            _detailScroll.verticalNormalizedPosition=scroll;
            _lastDetailKey=detailKey;
        }
        var canvasRect=(RectTransform)_canvas.transform;
        Vector3[] corners=new Vector3[4];
        ((RectTransform)source.transform).GetWorldCorners(corners);
        Vector2 bottom=canvasRect.InverseTransformPoint(corners[0]);
        Vector2 top=canvasRect.InverseTransformPoint(corners[2]);
        Rect bounds=canvasRect.rect;
        float x=bottom.x-466;
        if(x<bounds.xMin+16) x=top.x+16;
        float y=Mathf.Clamp(top.y-430,bounds.yMin+16,bounds.yMax-446);
        _hoverWindow.anchoredPosition=new Vector2(Mathf.Clamp(x,bounds.xMin+16,bounds.xMax-466)-bounds.xMin,y-bounds.yMin);
        _hoverWindow.gameObject.SetActive(true);
        _hoverTitle.rectTransform.sizeDelta=new Vector2(_hoverImage.enabled ? 316 : 410,68);
        _hoverWindow.SetAsLastSibling();
        _hoverScroll.verticalNormalizedPosition=1;
        if(_quick && source.transform.IsChildOf(_quickEntries))
        {
            // note: Previewing another record cannot expose actions for the previously selected record.
            _quickDepth=string.Equals(key,_selectedKey,StringComparison.OrdinalIgnoreCase) ? 3 : 2;
            _quickMotion.SetDepth(_quickDepth);
        }
    }
    public void PreviewAbility(Button source,string skillId)
    {
        // note: Hotbar choices share this read-only inspector without changing the menu's current page or its action target.
        MenuTab tab=_activeTab;
        _activeTab=MenuTab.Skills;
        try { ShowRecordPreview(source,"skill:"+skillId); }
        finally
        {
            _activeTab=tab;
            if(PlayerStateManager.Instance?.state!=null)
                RebuildDetails(PlayerStateManager.Instance.state,WorldStateManager.Instance?.State,GeneratedRpgContentService.Instance);
        }
    }
    private void HideRecordPreview()
    {
        if(_hoverWindow!=null) _hoverWindow.gameObject.SetActive(false);
        _hoverSource=null; _hoverKey=null; _hoverLeaveAt=0;
    }
    private static bool PointerInside(RectTransform rect,Vector2 point)
        => rect!=null && rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect,point,null);
    private void LateUpdate()
    {
        if(!_open && !_quick) { HideRecordPreview(); return; }
        var mouse=Mouse.current;
        if(mouse==null) return;
        Vector2 point=mouse.position.ReadValue();
        bool preview=PointerInside(_hoverWindow,point);
        bool source=_hoverSource!=null && PointerInside((RectTransform)_hoverSource.transform,point);
        if(_hoverWindow.gameObject.activeSelf)
        {
            if(preview || source) _hoverLeaveAt=0;
            else if(_hoverLeaveAt==0) _hoverLeaveAt=Time.unscaledTime+.20f;
            else if(Time.unscaledTime>=_hoverLeaveAt) HideRecordPreview();
        }
        if(!_quick || _quickDepth==0) return;
        // note: A short crossing grace spans the gaps between columns; clicking never latches a branch open.
        bool branch=preview;
        foreach(RectTransform child in _quickRoot)
        {
            if(child.name=="InputHints" || child.name=="QuickSummary") continue;
            var group=child.GetComponent<CanvasGroup>();
            if(group!=null && group.alpha>.1f && PointerInside(child,point)) { branch=true; break; }
        }
        if(branch) _branchLeaveAt=0;
        else if(_branchLeaveAt==0) _branchLeaveAt=Time.unscaledTime+.20f;
        else if(Time.unscaledTime>=_branchLeaveAt)
        {
            _quickDepth=0; _quickMotion.SetDepth(0); HideRecordPreview();
        }
    }
}
