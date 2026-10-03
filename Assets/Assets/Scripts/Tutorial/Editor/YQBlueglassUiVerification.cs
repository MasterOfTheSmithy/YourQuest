using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// note: Read-only UI observers and synthetic input exercise the real menu; they do not load profiles, equip gear, or regenerate content.
public static class YQBlueglassUiVerification
{
    private static YourQuestTutorialMenuUI _menu;
    private static Keyboard _keyboard;
    private static Mouse _mouse;
    private static int _branchWarmup;
    private static Keyboard _reviewKeyboard;
    private static double _reviewUntil;
    private static int _stage;
    private static int _nextFrame;
    private static double _deadline;
    private static int _idlePose;
    private static double _idleStarted;
    private static float _idleSampleAfter;
    private static float _settleAfter;
    private static bool _reducedMotion;
    private static int _motionSamples;
    private static bool _exitSample;
    private static readonly List<object> _motionEvidence=new List<object>();
    private static float _timeScale;
    private static bool _firstPerson;
    private static string _inventory;
    private static readonly List<string> _checks = new List<string>();
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("YourQuest/UI/Check Blueglass Assets")]
    public static void CompileAndCheckAssets()
    {
        // note: Malformed generated markup and authoring metadata must not alter a player-facing inspector.
        Require(YQBlueglassText.Description("Context: fixture\nA cloak against the cold.\nEffectKey=fx_internal")=="A cloak against the cold.","explicit metadata removed; flavour preserved");
        Require(YQBlueglassText.Description("The environment changes around you.")=="The environment changes around you.","ordinary lore is not keyword-filtered");
        Require(!YQBlueglassText.Escape("<size=0>hidden</size>").Contains("<"),"generated TMP markup cannot hide text");
        var bonus=new System.Text.StringBuilder();
        YQBlueglassText.Bonus(bonus,"Defense",-3);
        Require(bonus.ToString().Contains("-3") && bonus.ToString().Contains(YQBlueglassText.Negative),"negative modifiers retain sign and distinct color");
        // note: Unity reaches this entry point only after fresh script compilation succeeds.
        Sprite[] chrome = Resources.LoadAll<Sprite>("UI/Blueglass/BlueglassChrome");
        Sprite[] portraits = Resources.LoadAll<Sprite>("UI/Blueglass/ItemPortraits");
        Require(chrome.Length==16 && portraits.Length==12,"28 runtime sprites");
        foreach (Sprite sprite in chrome)
        {
            Require(sprite.texture != null && sprite.rect.width>0 && sprite.rect.height>0,"valid sprite: "+sprite.name);
            if (sprite.name.StartsWith("Navigation_") || sprite.name.StartsWith("Panel_") || sprite.name.StartsWith("Tile_") || sprite.name.StartsWith("Action_"))
                Require(sprite.border != Vector4.zero,"slice border: "+sprite.name);
        }
        string[] owners={"YQTitleScreenUI","YQStartupLoadingScreen","YQOriginQuestionnaireUI","YourQuestTutorialHud","YourQuestTutorialMenuUI","YourQuestPauseMenuUI","YQInvestorDialogueUI","YourQuestProgressionOfferUI","YQProfileMenuUI","YQLockpickUi"};
        foreach (string owner in owners)
            Require(typeof(YourQuestTutorialMenuUI).Assembly.GetType(owner)!=null,"required UI owner: "+owner);
        // note: Exercise the real meter's clipping contract without changing the active player's resources.
        var meterObject=new GameObject("Meter verification",typeof(RectTransform),typeof(YQBlueglassMeter));
        try
        {
            var meter=meterObject.GetComponent<YQBlueglassMeter>();
            foreach (float value in new[]{-10f,0f,50f,100f,150f})
            {
                meter.SetValue(value,100);
                Require(Mathf.Approximately(meter.Ratio,Mathf.Clamp01(value/100)),"meter ratio/clamp: "+value);
            }
            meter.SetValue(50,0);
            Require(meter.Ratio==0,"zero capacity meter");
        }
        finally { UnityEngine.Object.DestroyImmediate(meterObject); }
        // note: Detached records exercise loadout compatibility without accepting invented abilities into the user's save.
        var activeChoice=new SkillRecord {skillId="ui-active-contract",type="combat",unlocked=true};
        var spellChoice=new SkillRecord {skillId="ui-spell-contract",type="spell",isSpell=true,unlocked=true};
        Require(YQBlueglassAbilityHotbar.IsEligible(activeChoice,0) && !YQBlueglassAbilityHotbar.IsEligible(activeChoice,1),"active hotbar compatibility");
        Require(YQBlueglassAbilityHotbar.IsEligible(spellChoice,1) && !YQBlueglassAbilityHotbar.IsEligible(spellChoice,0),"spell hotbar compatibility");
        activeChoice.unlocked=false;
        Require(!YQBlueglassAbilityHotbar.IsEligible(activeChoice,0),"locked ability cannot be assigned");
        activeChoice.unlocked=true; activeChoice.type="passive";
        Require(!YQBlueglassAbilityHotbar.IsEligible(activeChoice,0) && !YQBlueglassAbilityHotbar.IsEligible(spellChoice,2),"passive/unsupported hotbar slots rejected");
        Write("UI_Blueglass_Compile_Receipt_2026-10-01.json",new {
            mode="Unity compilation and asset checks",status="PASS",utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,
            spriteCount=chrome.Length+portraits.Length,owners,
            componentCoverage=new[]{"button/nav","hover/focus/pressed/disabled","persistent selection","item portrait","equipment socket","inspector","inventory grid","scroll/long text","input field","dropdown","toggle","slider","status/progress meter","confirmation/rejection","input hints","preferences"},
            assemblyHash=HashFile("Library/ScriptAssemblies/Assembly-CSharp.dll"),
            runtime="NOT TESTED by this entry point",physicalHaptics="NOT TESTED"
        });
        Debug.Log("YQ_BLUEGLASS_COMPILE_ASSETS_PASS");
    }

    [MenuItem("YourQuest/UI/Verify Blueglass in Current Play Session")]
    public static void VerifyCurrentPlaySession()
    {
        Require(_reviewKeyboard==null,"end the quick mouse review before running the harness");
        Require(EditorApplication.isPlaying && !EditorApplication.isPaused,"active, unpaused Play Mode required");
        Require(!RuntimeModalUiBlocker.IsBlocked && !YQTitleScreenUI.StartupPresentationActive,"finish ordinary startup and close modals first");
        _menu=UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialMenuUI>();
        Require(_menu != null && PlayerStateManager.Instance?.state != null,"production menu and accepted player state");
        Require(!YourQuestTutorialMenuUI.IsOpenNow && !YourQuestTutorialMenuUI.IsQuickOpenNow,"begin in gameplay");
        Require(!FindField<RectTransform>(UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialHud>(),"_questBanner").gameObject.activeSelf,"quest banner hidden in ordinary gameplay");
        _checks.Clear();
        // note: This bounded presentation witness samples normal motion, then restores the user's accessibility preference on every completion path.
        _reducedMotion=YQBlueglassFeedback.ReducedMotion;
        YQBlueglassFeedback.ReducedMotion=false;
        _motionSamples=0; _exitSample=false; _motionEvidence.Clear();
        _inventory=InventorySnapshot();
        _timeScale=Time.timeScale;
        _firstPerson=YQInvestorPlayerMotor.ActiveMotor.firstPerson;
        _stage=0;
        _nextFrame=Time.frameCount+3;
        _settleAfter=Time.unscaledTime;
        _deadline=EditorApplication.timeSinceStartup+40;
        // note: A dedicated temporary keyboard leaves the user's physical device state untouched.
        _keyboard=InputSystem.AddDevice<Keyboard>("YQ_UI_Verification_Keyboard");
        _mouse=InputSystem.AddDevice<Mouse>("YQ_UI_Verification_Mouse");
        _branchWarmup=0;
        EditorApplication.update-=Tick;
        EditorApplication.update+=Tick;
        foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            if (window.GetType().Name=="GameView") { window.Focus(); break; }
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup>_deadline) { Finish("FAIL","Play session ended or UI witness timed out"); return; }
        try
        {
            // note: Sampling failures follow the same cleanup path as other witness failures, restoring input devices and motion preferences.
            SampleMotion();
            if((_stage==1 || _stage==3 || _stage==6) && Get<RectTransform>("_quickRoot").GetComponent<YQBlueglassQuickMenuMotion>().IsAnimating) return;
            if (Time.frameCount<_nextFrame || Time.unscaledTime<_settleAfter || (_stage==8 && Time.unscaledTime<_idleSampleAfter)) return;
            switch (_stage++)
            {
                case 0: Queue(Key.LeftAlt); break;
                case 1:
                    if(_branchWarmup<2)
                    {
                        if(_branchWarmup==0)
                        {
                            Require(Get<RectTransform>("_quickCategories").GetComponent<CanvasGroup>().alpha==0,"quick opens with root controls only");
                            MovePointer(Get<RectTransform>("_quickRoot").Find("QuickPlayer").GetComponent<Button>());
                        }
                        else MovePointer(Get<RectTransform>("_quickCategories").GetComponentInChildren<Button>());
                        _branchWarmup++; _stage--; _settleAfter=Time.unscaledTime+.42f;
                        break;
                    }
                    Require((int)typeof(YourQuestTutorialMenuUI).GetField("_quickDepth",Fields).GetValue(_menu)>=2,"pointer hover opens both branches through EventSystem");
                    Require(YourQuestTutorialMenuUI.IsQuickOpenNow,"Alt opens realtime UI");
                    Require(Mathf.Approximately(Time.timeScale,_timeScale) && !RuntimeModalUiBlocker.IsBlocked,"quick UI does not pause world");
                    Require(Cursor.visible && Cursor.lockState==CursorLockMode.None,"quick cursor access");
                    _checks.Add("Alt opens quick UI; cursor unlocked; simulation timescale unchanged");
                    VerifyHudAndMap();
                    Require(FindField<RectTransform>(UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialHud>(),"_questBanner").gameObject.activeInHierarchy,"quest banner appears while Alt is held");
                    Require(Get<RectTransform>("_quickRoot").GetComponent<YQBlueglassQuickMenuMotion>().Reveal==1,"quick branch settles after staged reveal");
                    VerifyHoverRefresh();
                    VerifyRaycast(Get<RectTransform>("_quickCategories").GetComponentInChildren<Button>());
                    Require(UnityEngine.Object.FindFirstObjectByType<YQBlueglassAbilityHotbar>().PresentationRoot.gameObject.activeInHierarchy,"live ability hotbar visible in quick UI");
                    Capture("quick-after.png");
                    break;
                case 2:
                    HoverLabel(Get<RectTransform>("_quickCategories"),"Skills");
                    break;
                case 3:
                    Require(Get<TMPro.TMP_Text>("_quickSummary").text.Length>0,"generated/empty skill detail is visible");
                    VerifyQuickRows();
                    VerifyHotbarPicker();
                    Capture("hotbar-picker-after.png");
                    break;
                case 4:
                    HoverLabel(Get<RectTransform>("_quickCategories"),"Equipment");
                    _checks.Add("quick root/branch navigation uses temporary mouse input through EventSystem");
                    break;
                case 5:
                    var button=Get<Button>("_quickPrimary");
                    var feedback=button.GetComponent<YQBlueglassControlFeedback>();
                    Require(feedback!=null,"control feedback installed");
                    feedback.Pulse(YQBlueglassCue.Reject);
                    Require(feedback.Surface!=null && feedback.Surface.FlashAmount>0,"native rejection visual exists");
                    Queue();
                    break;
                case 6:
                    Require(!YourQuestTutorialMenuUI.IsQuickOpenNow,"Alt release closes quick UI");
                    Require(Mathf.Approximately(Time.timeScale,_timeScale) && !RuntimeModalUiBlocker.IsBlocked,"Alt release preserves simulation");
                    Require(!Cursor.visible && Cursor.lockState==CursorLockMode.Locked,"cursor returns to gameplay");
                    Require(!UnityEngine.Object.FindFirstObjectByType<YQBlueglassAbilityHotbar>().PickerOpen,"Alt-up closes ability picker");
                    Require(!FindField<RectTransform>(UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialHud>(),"_questBanner").gameObject.activeSelf,"quest banner disappears on Alt release");
                    Require(!Get<RectTransform>("_quickRoot").gameObject.activeSelf,"quick retraction completes without leaving visible controls");
                    Require(_motionSamples==2 && _exitSample,"intermediate opening and closing frames observed");
                    _checks.Add("quest banner only during Alt; staged quick reveal and completed inert retraction");
                    _checks.Add("Alt-up closes quick UI and restores cursor without pausing");
                    Queue(Key.Escape);
                    break;
                case 7:
                    Require(YourQuestTutorialMenuUI.IsOpenNow && Mathf.Approximately(Time.timeScale,0),"Escape opens full pause through modal owner");
                    _checks.Add("Escape opens full menu; world timescale zero");
                    Require(!FindField<Canvas>(UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialHud>(),"_canvas").enabled,"full pause hides HUD");
                    Require(!UnityEngine.Object.FindFirstObjectByType<YQBlueglassAbilityHotbar>().PresentationRoot.gameObject.activeInHierarchy,"full pause hides hotbar overlay");
                    var portrait=_menu.GetComponentInChildren<YQBlueglassPlayerPortrait>();
                    Require(portrait!=null && portrait.VisibleParts>0,"portrait has authoritative player geometry");
                    Require(portrait.PresentationCamera.clearFlags==CameraClearFlags.SolidColor && portrait.PresentationCamera.backgroundColor.a==0,"portrait clear is transparent");
                    Require(portrait.PresentationCamera.cullingMask==(1<<31) && portrait.PresentationCamera.farClipPlane<20,"portrait isolates only nearby preview geometry");
                    Require(portrait.ViewModeText.text.Contains(YQInvestorPlayerMotor.ActiveMotor.firstPerson ? "FIRST PERSON" : "THIRD PERSON"),"portrait camera mode matches motor");
                    Require(Get<RectTransform>("_fullRoot").GetComponent<YQBlueglassPauseBackdrop>().Backdrop.activeInHierarchy,"full pause backdrop active");
                    VerifyRaycast(Get<RectTransform>("_fullRoot").GetComponentsInChildren<Button>().First(button=>button.name=="Inventory"));
                    VerifyPortraitAlpha(portrait.PresentationCamera.targetTexture);
                    VerifyPortraitTints(portrait);
                    _checks.Add("full HUD suppression; player-only transparent portrait; authoritative view mode label");
                    Require(!string.IsNullOrEmpty(portrait.IdleAnimationName),"approved idle clip bound to presentation rig");
                    _idlePose=portrait.PoseSignature; _idleStarted=portrait.IdlePlaybackTime; _idleSampleAfter=Time.unscaledTime+.65f;
                    Capture("pause-after.png");
                    Queue();
                    break;
                case 8:
                    var animatedPortrait=_menu.GetComponentInChildren<YQBlueglassPlayerPortrait>();
                    Require(Time.timeScale==0 && animatedPortrait.IdlePlaybackTime!=_idleStarted && animatedPortrait.PoseSignature!=_idlePose,"idle skeleton moves while gameplay remains paused");
                    _checks.Add("approved idle advances actual skeletal pose at timescale zero");
                    ClickLabel(Get<RectTransform>("_fullRoot"),"Inventory");
                    // note: Exercise the other presentation mode, then restore the user's camera before resuming.
                    YQInvestorPlayerMotor.ActiveMotor.ToggleCameraMode();
                    break;
                case 9:
                    Require(_menu.GetComponentInChildren<YQBlueglassPlayerPortrait>().ViewModeText.text.Contains(_firstPerson ? "THIRD PERSON" : "FIRST PERSON"),"portrait updates for the other camera mode");
                    VerifyPortraitAlpha(_menu.GetComponentInChildren<YQBlueglassPlayerPortrait>().PresentationCamera.targetTexture);
                    Capture("pause-inventory-after.png");
                    break;
                case 10:
                    YQInvestorPlayerMotor.ActiveMotor.ToggleCameraMode();
                    _checks.Add("visible transparent portrait in both camera modes; mode label follows motor; original camera restored");
                    Require(Get<RectTransform>("_listContent").GetComponent<YQBlueglassItemLayout>().Grid,"native carried-item grid");
                    Require(Get<RectTransform>("_equipmentContent").GetComponentsInChildren<Button>().Length==13,"all thirteen displayed equipment slots including cloak");
                    foreach (string section in new[]{"Skills","Classes","Quests","Stats","Equipment"})
                    {
                        ClickLabel(Get<RectTransform>("_fullRoot"),section);
                        Require(Get<RectTransform>("_equipmentPanel").gameObject.activeSelf==(section=="Equipment"),"character stage exclusive to inventory/equipment: "+section);
                    }
                    _checks.Add("inventory grid, 13 slots including cloak, skills/classes/titles/quests/stats navigation");
                    VerifyPolish();
                    break;
                case 11:
                    // note: The closing click remains consumed after modal release, preventing the combat owner from replaying it in this frame.
                    ClickLabel(Get<RectTransform>("_fullRoot"),"Resume");
                    Require(!YourQuestTutorialMenuUI.IsOpenNow && Time.timeScale>0 && YourQuestTutorialMenuUI.CapturesPointerInput,"Resume click consumed for closing frame");
                    _checks.Add("full-menu Resume consumes the closing-frame pointer input after modal release");
                    _menu.OpenFullMenu();
                    ClickLabel(Get<RectTransform>("_fullRoot"),"System");
                    break;
                case 12:
                    Require(YourQuestPauseMenuUI.IsOpenNow && !YourQuestTutorialMenuUI.IsOpenNow && Time.timeScale==0,"System handoff retains pause");
                    var pause=UnityEngine.Object.FindFirstObjectByType<YourQuestPauseMenuUI>();
                    var canvas=(Canvas)typeof(YourQuestPauseMenuUI).GetField("_canvas",Fields).GetValue(pause);
                    ClickLabel(canvas.transform,"Resume");
                    _checks.Add("System retains existing controls and Resume handoff");
                    break;
                case 13:
                    Require(!RuntimeModalUiBlocker.IsBlocked && Mathf.Approximately(Time.timeScale,_timeScale),"resume restores simulation");
                    Require(InventorySnapshot()==_inventory,"accepted inventory/loadout unchanged by UI witness");
                    _checks.Add("accepted inventory/loadout unchanged");
                    Finish("PASS",null);
                    return;
            }
            _nextFrame=Time.frameCount+4;
            // note: Screenshots and input witnesses sample settled presentation, including the bounded unscaled reveal/retract transitions.
            _settleAfter=Time.unscaledTime+.42f;
        }
        catch(Exception error) { Finish("FAIL",error.ToString()); }
    }
    private static T Get<T>(string name) where T:class => typeof(YourQuestTutorialMenuUI).GetField(name,Fields).GetValue(_menu) as T;
    [MenuItem("YourQuest/UI/Open Blueglass Pause for Mouse Review")]
    public static void OpenPauseForMouseReview()
    {
        EndQuickMouseReview();
        // note: A bounded editor entry releases the cursor for actual mouse review; subsequent clicks use ordinary UI raycasts.
        Require(EditorApplication.isPlaying && !RuntimeModalUiBlocker.IsBlocked && !YQTitleScreenUI.StartupPresentationActive,"begin in released gameplay");
        UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialMenuUI>().OpenFullMenu();
        ObservePointerReview();
        foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            if (window.GetType().Name=="GameView") { window.Focus(); break; }
    }
    [MenuItem("YourQuest/UI/Open Blueglass Quick Menu for Mouse Review")]
    public static void OpenQuickForMouseReview()
    {
        Require(EditorApplication.isPlaying && !EditorApplication.isPaused && !RuntimeModalUiBlocker.IsBlocked && !YQTitleScreenUI.StartupPresentationActive,"begin in released gameplay");
        Require(_keyboard==null && !YourQuestTutorialMenuUI.IsOpenNow,"close the full menu and finish the harness first");
        EndQuickMouseReview();
        // note: The desktop helper cannot hold a modifier across mouse actions. Only Alt is supplied here; hovering and choosing use the real mouse and EventSystem.
        _reviewKeyboard=InputSystem.AddDevice<Keyboard>("YQ_UI_Mouse_Review_Alt");
        _reviewUntil=EditorApplication.timeSinceStartup+75;
        ObservePointerReview();
        InputSystem.QueueStateEvent(_reviewKeyboard,new KeyboardState(Key.LeftAlt));
        EditorApplication.update+=PollQuickMouseReview;
        InputSystem.onAfterUpdate+=KeepReviewKeyboardCurrent;
        EditorApplication.playModeStateChanged+=ReviewPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload+=EndQuickMouseReview;
        foreach(EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            if(window.GetType().Name=="GameView") {window.Focus();break;}
        Debug.Log("YQ_BLUEGLASS_MOUSE_REVIEW: synthetic held Alt; subsequent pointer input is native; automatic cleanup in 75 seconds");
    }
    [MenuItem("YourQuest/UI/End Blueglass Quick Mouse Review")]
    public static void EndQuickMouseReview()
    {
        EditorApplication.update-=PollQuickMouseReview;
        InputSystem.onAfterUpdate-=KeepReviewKeyboardCurrent;
        EditorApplication.playModeStateChanged-=ReviewPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload-=EndQuickMouseReview;
        if(_reviewKeyboard!=null && _reviewKeyboard.added) InputSystem.RemoveDevice(_reviewKeyboard);
        _reviewKeyboard=null;
    }
    private static void PollQuickMouseReview()
    {
        if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup>=_reviewUntil) EndQuickMouseReview();
        // note: GameView focus can reset synthetic devices; keep only the bounded review's Alt state supplied after focus settles.
        else if(_reviewKeyboard!=null && _reviewKeyboard.added)
            InputSystem.QueueStateEvent(_reviewKeyboard,new KeyboardState(Key.LeftAlt));
    }
    private static void KeepReviewKeyboardCurrent()
    {
        // note: Native mouse events remain untouched; physical keyboard focus cannot replace the review's held modifier.
        if(_reviewKeyboard!=null && _reviewKeyboard.added) _reviewKeyboard.MakeCurrent();
    }
    private static void ReviewPlayModeChanged(PlayModeStateChange _) => EndQuickMouseReview();
    private static double _pointerReviewUntil;
    private static void ObservePointerReview()
    {
        // note: This optional witness records real dispatch, distinguishing failed hit targets from a successful listener-only fixture.
        YQBlueglassControlFeedback.PointerObserved=RecordPointer;
        _pointerReviewUntil=EditorApplication.timeSinceStartup+120;
        EditorApplication.update-=ObserveMouse;
        EditorApplication.update+=ObserveMouse;
    }
    private static bool _mouseWasDown;
    private static void ObserveMouse()
    {
        if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup>_pointerReviewUntil) { EditorApplication.update-=ObserveMouse; return; }
        bool down=Mouse.current?.leftButton.isPressed ?? false;
        if(down==_mouseWasDown) return;
        _mouseWasDown=down;
        var hits=new List<RaycastResult>();
        var data=new PointerEventData(EventSystem.current) {position=Mouse.current.position.ReadValue()};
        EventSystem.current?.RaycastAll(data,hits);
        var module=EventSystem.current?.currentInputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule;
        File.AppendAllText(Path.Combine(Application.dataPath,"../Docs/UI_Blueglass_Pointer_2026-10-02.jsonl"),JsonConvert.SerializeObject(new {
            utc=DateTime.UtcNow,phase=down ? "raw-down" : "raw-up",position=data.position.ToString(),
            module=module?.name,clickEnabled=module?.leftClick?.action?.enabled,hits=hits.Take(4).Select(hit=>hit.gameObject.name).ToArray()
        })+Environment.NewLine);
    }
    private static void RecordPointer(YQBlueglassControlFeedback control,string phase,PointerEventData data)
    {
        if(EditorApplication.timeSinceStartup>_pointerReviewUntil) { YQBlueglassControlFeedback.PointerObserved=null; return; }
        string path=control.name;
        for(Transform parent=control.transform.parent;parent!=null;parent=parent.parent) path=parent.name+"/"+path;
        var menu=UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialMenuUI>();
        File.AppendAllText(Path.Combine(Application.dataPath,"../Docs/UI_Blueglass_Pointer_2026-10-02.jsonl"),JsonConvert.SerializeObject(new {
            utc=DateTime.UtcNow,frame=Time.frameCount,phase,path,
            label=control.GetComponentInChildren<TMPro.TMP_Text>()?.text,
            hit=data.pointerCurrentRaycast.gameObject?.name,eligible=data.eligibleForClick,dragging=data.dragging,
            selected=menu!=null ? typeof(YourQuestTutorialMenuUI).GetField("_selectedKey",Fields).GetValue(menu) : null,
            tab=menu!=null ? typeof(YourQuestTutorialMenuUI).GetField("_activeTab",Fields).GetValue(menu)?.ToString() : null
        })+Environment.NewLine);
    }
    [MenuItem("YourQuest/UI/Capture Blueglass Screen")]
    private static void CaptureCurrentScreen()
    {
        Require(EditorApplication.isPlaying,"active Play Mode required");
        Capture("screen-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".png");
    }
    private static void ClickLabel(Transform root,string label)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            var text=button.transform.Find("Label")?.GetComponent<TMPro.TMP_Text>();
            if (text != null && text.text==label) { button.onClick.Invoke(); return; }
        }
        throw new InvalidOperationException("Missing native control: "+label);
    }
    private static void Queue(params Key[] keys) => InputSystem.QueueStateEvent(_keyboard,new KeyboardState(keys));
    private static string InventorySnapshot()
    {
        PlayerState state=PlayerStateManager.Instance.state;
        // note: Compare inventory identity/quantities and equipment maps; Unity Vector3 computed properties are not JSON state contracts.
        return JsonConvert.SerializeObject(new {items=state.inventoryItems.Select(item=>new {item.itemId,item.quantity,item.equipSlot}),state.equippedItemBySlot,state.equippedSkillBySlot});
    }
    private static T FindField<T>(object owner,string name) where T:class => owner.GetType().GetField(name,Fields).GetValue(owner) as T;
    private static void VerifyHudAndMap()
    {
        var hud=UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialHud>();
        var state=PlayerStateManager.Instance.state;
        var xp=FindField<YQBlueglassMeter>(hud,"_experienceFill");
        Require(Mathf.Approximately(xp.Ratio,Mathf.Clamp01((float)state.xp/PlayerState.GetXpRequiredForLevel(state.level))),"XP reads accepted progression");
        var vitals=FindField<YQInvestorVitals>(hud,"_vitals");
        Require(vitals!=null,"authoritative resource owner");
        foreach(var sample in new[]{("_healthFill","GetMaxHealth",vitals.CurrentHealth),("_staminaFill","GetMaxStamina",vitals.CurrentStamina),("_manaFill","GetMaxMana",vitals.CurrentMana)})
        {
            float maximum=(float)typeof(YQInvestorVitals).GetMethod(sample.Item2,Fields).Invoke(vitals,null);
            Require(Mathf.Abs(FindField<YQBlueglassMeter>(hud,sample.Item1).Ratio-Mathf.Clamp01(sample.Item3/maximum))<.025f,"meter follows live resource: "+sample.Item1);
        }
        // note: Quantity checks alone missed invisible geometry; require the actual CanvasRenderer mesh as well.
        foreach(string name in new[]{"_healthFill","_staminaFill","_manaFill","_experienceFill"})
        {
            var meter=FindField<YQBlueglassMeter>(hud,name);
            Require(meter!=null,"native resource meter: "+name);
            Mesh mesh=meter.canvasRenderer.GetMesh();
            Require(mesh!=null && mesh.vertexCount>=4,"rendered meter geometry: "+name);
        }
        Require(FindField<TMPro.TMP_Text>(hud,"_questNameText").text.Length>0,"quest tracker name");
        var map=YQGeneratedWorldMinimap.Instance;
        Require(map!=null,"production map owner");
        var camera=FindField<Camera>(map,"_mapCamera");
        Require(Mathf.Abs(camera.transform.position.y-YQInvestorPlayerMotor.ActiveMotor.transform.position.y-240f)<.1f,"map camera follows player altitude");
        var fog=FindField<RawImage>(map,"_fogImage");
        Require(fog.uvRect==new Rect(0,0,1,1),"finite fog atlas avoids clamped edge smear");
        // note: A text-only marker rendered as a missing-glyph box in the actual Game view.
        var questMarker=FindField<RectTransform>(map,"_questMarker");
        Require(questMarker.GetComponent<Image>()!=null && questMarker.GetComponent<TMPro.TMP_Text>()==null,"quest marker uses font-independent geometry");
        Require(questMarker.Find("QuestDiamondCenter").GetComponent<Image>()!=null,"quest marker has a contrasting center");
        _checks.Add("four native meters; accepted XP ratio; quest name; altitude-relative north-up map; finite discovery atlas; native quest diamond");
    }
    private static void VerifyHoverRefresh()
    {
        var category=Get<RectTransform>("_quickCategories").GetComponentInChildren<Button>();
        var feedback=category.GetComponent<YQBlueglassControlFeedback>();
        feedback.OnPointerEnter(new PointerEventData(EventSystem.current));
        var entries=Get<RectTransform>("_quickEntries");
        int first=entries.GetChild(0).GetInstanceID();
        typeof(YourQuestTutorialMenuUI).GetMethod("MarkDirty",Fields,null,new[]{typeof(bool)},null).Invoke(_menu,new object[]{true});
        Require(feedback.Hovered && feedback.Surface!=null && feedback.Surface.Hovered,"hover persists across presentation refresh");
        Require(entries.GetChild(0).GetInstanceID()==first,"live entries retain native control identity");
        feedback.OnPointerExit(new PointerEventData(EventSystem.current));
        _checks.Add("hover retains highlight across refresh; native quick-entry identity preserved");
    }
    private static void VerifyQuickRows()
    {
        // note: Deferred destruction previously let the quick Skills view copy stale inventory rows in the same rebuild.
        var expected=Get<RectTransform>("_listContent").GetComponentsInChildren<Button>()
            .Select(button=>button.transform.Find("Title")?.GetComponent<TMPro.TMP_Text>()?.text).ToArray();
        var actual=Get<RectTransform>("_quickEntries").GetComponentsInChildren<Button>()
            .Select(button=>button.transform.Find("Title")?.GetComponent<TMPro.TMP_Text>()?.text).ToArray();
        Require(expected.SequenceEqual(actual),"quick rows match the current accepted section without stale inventory entries");
        _checks.Add("quick Skills rows match the current section after deferred-destruction rebuild");
    }
    private static void HoverLabel(Transform root,string label)
    {
        foreach(Button button in root.GetComponentsInChildren<Button>(true))
            if(button.GetComponentInChildren<TMPro.TMP_Text>()?.text==label)
            { MovePointer(button); return; }
        throw new InvalidOperationException("Missing hover control: "+label);
    }
    private static void MovePointer(Button button)
    {
        // note: Pointer residence exercises production hover exit rules; no private branch timer is bypassed.
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)button.transform;
        Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        InputSystem.QueueStateEvent(_mouse,new MouseState {position=point});
        _mouse.MakeCurrent();
    }
    private static void VerifyPolish()
    {
        var scroll=Get<ScrollRect>("_detailScroll");
        Require(scroll.verticalScrollbar!=null && scroll.GetComponent<YQBlueglassScrollAffordance>()!=null,"inspector has scroll affordance");
        Require(Get<ScrollRect>("_listScroll").verticalScrollbar!=null,"inventory has scroll affordance");
        Require(Get<TMPro.TMP_Text>("_detailBodyText").overflowMode==TMPro.TextOverflowModes.Overflow,"long details flow into scroll content instead of ellipsis");
        var state=PlayerStateManager.Instance.state;
        var format=typeof(YourQuestTutorialMenuUI).GetMethod("RebuildDetails",Fields);
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition=.4f;
        float before=scroll.verticalNormalizedPosition;
        format.Invoke(_menu,new object[]{state,WorldStateManager.Instance?.State,GeneratedRpgContentService.Instance});
        Require(Mathf.Abs(scroll.verticalNormalizedPosition-before)<.01f,"state refresh preserves detail reading position");
        ClickLabel(Get<RectTransform>("_fullRoot"),"Stats");
        Require(Get<TMPro.TMP_Text>("_detailBodyText").text.Contains("Critical chance") && Get<TMPro.TMP_Text>("_detailBodyText").text.Contains("%"),"combat sheet displays critical chance as percent");
        _checks.Add("scrollbars; unclipped long-form detail; preserved reading position; typed stat percentages");
        Capture("polished-stats.png");
    }
    private static void VerifyHotbarPicker()
    {
        var hotbar=UnityEngine.Object.FindFirstObjectByType<YQBlueglassAbilityHotbar>();
        Require(hotbar!=null && hotbar.GetSlotButton(0)!=null && hotbar.GetSlotButton(1)!=null,"existing active/spell channels have hotbar controls");
        hotbar.HoverSlot(0);
        Require(hotbar.PickerOpen && hotbar.PickerSlot==0 && !RuntimeModalUiBlocker.IsBlocked && Time.timeScale>0,"Alt hover opens a non-pausing swap picker");
        string before=InventorySnapshot();
        foreach(Button choice in hotbar.PresentationRoot.GetComponentsInChildren<Button>())
            if(choice.name=="AbilityChoice" && choice.GetComponent<YQBlueglassControlFeedback>().Selected) { choice.onClick.Invoke(); break; }
        Require(before==InventorySnapshot(),"reselecting an equipped ability is idempotent");
        _checks.Add("live active/spell hotbar; Alt hover picker; current ability reselect is idempotent; world remains unpaused");
    }
    private static void VerifyRaycast(Button button)
    {
        // note: Listener invocation missed transparent hit targets culled by Unity; exercise the actual EventSystem raycast at the rendered control center.
        Canvas.ForceUpdateCanvases();
        Vector2 point=RectTransformUtility.WorldToScreenPoint(null,button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center));
        var hits=new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {position=point},hits);
        Require(hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),"native pointer raycast reaches "+button.name);
        _checks.Add("native EventSystem pointer raycast reaches "+button.name);
    }
    private static void SampleMotion()
    {
        // note: Observe real intermediate rendered frames; the witness never forces a reveal pose or changes the menu's animation clock.
        if(_menu==null) return;
        var root=Get<RectTransform>("_quickRoot");
        var motion=root.GetComponent<YQBlueglassQuickMenuMotion>();
        float reveal=motion.Reveal;
        if(_stage==1 && _motionSamples<2 && reveal>0 && reveal<1 && reveal>=(_motionSamples==0 ? .1f : .45f))
        {
            _motionSamples++;
            _motionEvidence.Add(new {direction="open",reveal,frame=Time.frameCount,entryHeight=((RectTransform)root.Find("QuickEntriesArea")).sizeDelta.y});
            Capture("quick-opening-"+_motionSamples+".png");
        }
        else if(_stage==6 && !_exitSample && reveal>0 && reveal<1)
        {
            _exitSample=true;
            _motionEvidence.Add(new {direction="close",reveal,frame=Time.frameCount,acceptsPointer=YourQuestTutorialMenuUI.IsQuickOpenNow});
            Capture("quick-closing.png");
        }
    }
    private static void Capture(string name)
    {
        string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/UI_Blueglass_Evidence_2026-10-01"));
        Directory.CreateDirectory(directory);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,name));
    }
    private static void VerifyPortraitTints(YQBlueglassPlayerPortrait portrait)
    {
        // note: Compare the actual source/view renderer overrides; sharing materials alone previously lost accepted equipment colours.
        var sourceProperties=new MaterialPropertyBlock();
        var viewProperties=new MaterialPropertyBlock();
        int tinted=0;
        foreach(object part in (System.Collections.IEnumerable)typeof(YQBlueglassPlayerPortrait).GetField("_parts",Fields).GetValue(portrait))
        {
            Renderer source=(Renderer)part.GetType().GetField("source").GetValue(part);
            Renderer view=(Renderer)part.GetType().GetField("view").GetValue(part);
            if(source==null || !source.HasPropertyBlock()) continue;
            source.GetPropertyBlock(sourceProperties); view.GetPropertyBlock(viewProperties);
            Require(sourceProperties.GetColor("_BaseColor").Equals(viewProperties.GetColor("_BaseColor")) && sourceProperties.GetColor("_Color").Equals(viewProperties.GetColor("_Color")),"portrait preserves runtime equipment tint: "+source.name);
            tinted++;
        }
        _checks.Add("portrait source/view runtime tint equality; "+tinted+" tinted renderers compared");
    }
    private static void VerifyPortraitAlpha(RenderTexture target)
    {
        // note: Camera settings alone cannot prove URP preserved alpha; inspect the actual rendered pixels once.
        var previous=RenderTexture.active;
        var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
            pixels.Apply();
            bool transparent=false,character=false;
            foreach(Color32 pixel in pixels.GetPixels32()) { transparent|=pixel.a<8; character|=pixel.a>240; }
            Require(transparent && character,"portrait contains rendered character and transparent background pixels");
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/UI_Blueglass_Evidence_2026-10-01"));
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory,YQInvestorPlayerMotor.ActiveMotor.firstPerson ? "portrait-first-person.png" : "portrait-third-person.png"),pixels.EncodeToPNG());
        }
        finally { RenderTexture.active=previous; UnityEngine.Object.Destroy(pixels); }
    }
    private static void Finish(string status,string error)
    {
        EditorApplication.update-=Tick;
        YQBlueglassFeedback.ReducedMotion=_reducedMotion;
        if (EditorApplication.isPlaying && YQInvestorPlayerMotor.ActiveMotor!=null && YQInvestorPlayerMotor.ActiveMotor.firstPerson!=_firstPerson)
            YQInvestorPlayerMotor.ActiveMotor.ToggleCameraMode();
        if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        _keyboard=null;
        if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
        _mouse=null;
        Write("UI_Blueglass_Runtime_Receipt_2026-10-01.json",new {
            mode="Production PlaySafe session; harness-assisted temporary keyboard/mouse devices and full-page listener invocation",
            status,error,utc=DateTime.UtcNow.ToString("O"),checks=_checks.ToArray(),scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            motionFrames=_motionEvidence.ToArray(),
            restoredReducedMotion=_reducedMotion,
            physicalGamepads=Gamepad.all.Count,physicalHaptics="NOT TESTED",ordinaryMouseFlow="Separate visible UI witness required",
            assemblyHash=HashFile("Library/ScriptAssemblies/Assembly-CSharp.dll")
        });
        if (status=="PASS") Debug.Log("YQ_BLUEGLASS_RUNTIME_HARNESS_PASS");
        else Debug.LogError("YQ_BLUEGLASS_RUNTIME_HARNESS_FAIL: "+error);
    }
    private static void Require(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Write(string name,object receipt) => File.WriteAllText(Path.Combine(Application.dataPath,"../Docs",name),JsonConvert.SerializeObject(receipt,Formatting.Indented));
    private static string HashFile(string path)
    {
        using var sha=SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-",string.Empty);
    }
}
