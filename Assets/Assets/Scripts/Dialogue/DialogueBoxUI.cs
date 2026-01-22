// C:\Users\Garri\YourQuest\Assets\Assets\Scripts\Dialogue\DialogueBoxUI.cs
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

[DisallowMultipleComponent]
public sealed class DialogueBoxUI : MonoBehaviour
{
    [Header("Interaction")]
    public Transform playerRoot;
    public Camera viewCamera;

    [Tooltip("Layer(s) to search for NPC colliders.")]
    public LayerMask entityMask = ~0;

    [Tooltip("Layers that can block LOS. Exclude Player + NPC.")]
    public LayerMask occluderMask = ~0;

    [Range(0.5f, 12f)]
    public float talkRadius = 4f;

    [Tooltip("Where LOS ray starts (if null, uses camera position).")]
    public Transform losOrigin;

    [Header("Targeting Rules")]
    [Tooltip("If true, requires LOS (raycast against occluderMask).")]
    public bool requireLineOfSight = true;

    [Tooltip("If true, NPC must have a NpcDialogueAgent to be considered talkable.")]
    public bool requireDialogueAgent = true;

    [Header("Debug")]
    public bool debugLogs = true;

    [Header("UI")]
    [Range(10, 36)] public int fontSize = 18;
    [Range(8, 28)] public int headerFontSize = 20;

    [Tooltip("Font size for the large latest NPC reply panel above the transcript.")]
    [Range(10, 42)] public int npcResponseFontSize = 24;

    [Range(200, 900)] public int panelWidth = 720;
    [Range(200, 900)] public int panelHeight = 520;

    [Tooltip("Height for the large latest NPC reply panel.")]
    [Range(60, 240)] public int npcResponsePanelHeight = 130;

    [Header("Behavior")]
    public bool pauseTimeWhenOpen = false;
    public bool unlockCursorWhenOpen = true;

    [Header("Input Blocking While Open")]
    [Tooltip("Disables PlayerInput components under playerRoot while dialogue is open (stops move/jump/etc).")]
    public bool disablePlayerInputWhileOpen = true;

    [Tooltip("Also disables any MonoBehaviour named 'InputIntentRecorder' under playerRoot while open.")]
    public bool disableInputIntentRecorderWhileOpen = true;

    private Canvas _canvas;
    private GameObject _root;

    private TMP_Text _headerText;

    // Big panel above transcript
    private TMP_Text _npcResponseText;

    private TMP_Text _transcriptText;
    private ScrollRect _scrollRect;

    private TMP_InputField _inputField;
    private Button _sendButton;
    private Button _closeButton;

    private EntityInfo _activeEntity;
    private NpcDialogueAgent _activeAgent;

    private bool _isOpen;

    private bool _pausedByUs;
    private float _previousTimeScale = 1f;

    private CursorLockMode _prevCursorLock;
    private bool _prevCursorVisible;
    private bool _cursorCaptured;

    private PlayerInput[] _blockedPlayerInputs;
    private MonoBehaviour[] _blockedIntentRecorders;

    private void Reset()
    {
        viewCamera = Camera.main;
    }

    private void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (playerRoot == null) playerRoot = transform;

        EnsureEventSystem_InputSystem();
        BuildUI();

        _root.SetActive(false);
        _isOpen = false;

        AppendSystemLine("Press E near an NPC to talk.");
    }

    private void OnDisable()
    {
        if (_isOpen) Close();
        else
        {
            RestoreTimeScaleIfNeeded();
            RestoreCursorIfNeeded();
            RestoreGameplayInputsIfNeeded();
        }
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (!_isOpen)
        {
            if (kb.eKey.wasPressedThisFrame)
                TryOpenNearestNpc();
        }
        else
        {
            // Block “other keys doing other things” by disabling gameplay inputs.
            // Here we only handle dialogue controls.
            if (kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            // Enter sends even if the input loses focus; we re-focus.
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                TrySend();
        }
    }

    private void TryOpenNearestNpc()
    {
        if (debugLogs)
        {
            Debug.Log($"[DialogueBoxUI] E pressed. center={playerRoot.position} radius={talkRadius} entityMask={entityMask.value} occluderMask={occluderMask.value} requireLOS={requireLineOfSight}");
        }

        var nearest = FindNearestTalkableNpc(out string reason);
        if (nearest == null)
        {
            if (debugLogs) Debug.Log($"[DialogueBoxUI] No talk target. Reason: {reason}");
            return;
        }

        _activeEntity = nearest;
        _activeAgent = nearest.GetComponentInChildren<NpcDialogueAgent>();

        if (requireDialogueAgent && _activeAgent == null)
        {
            if (debugLogs) Debug.LogWarning($"[DialogueBoxUI] Found EntityInfo '{nearest.displayName}' but no NpcDialogueAgent on root/children.");
            return;
        }

        Open(_activeEntity, _activeAgent);
    }

    private EntityInfo FindNearestTalkableNpc(out string reason)
    {
        reason = "unknown";

        if (playerRoot == null)
        {
            reason = "playerRoot null";
            return null;
        }

        Vector3 center = playerRoot.position;

        // Uses your existing EntityIndex API.
        List<EntityInfo> nearby = EntityIndex.FindNearbyEntities(center, talkRadius, entityMask, maxResults: 24, requireEntityInfo: true);
        if (nearby == null || nearby.Count == 0)
        {
            reason = "No EntityInfo found in radius. Check entityMask, colliders, radius.";
            return null;
        }

        Vector3 origin = GetLosOrigin();

        EntityInfo best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < nearby.Count; i++)
        {
            var e = nearby[i];
            if (e == null) continue;

            if (requireDialogueAgent && e.GetComponentInChildren<NpcDialogueAgent>() == null)
                continue;

            Vector3 target = e.transform.position;
            float dSq = (target - center).sqrMagnitude;
            if (dSq >= bestDistSq) continue;

            if (requireLineOfSight)
            {
                if (!HasLineOfSight(origin, target + Vector3.up * 1.4f, occluderMask, out RaycastHit hit))
                {
                    if (debugLogs)
                    {
                        string hitName = hit.collider != null ? hit.collider.name : "<null>";
                        string hitLayer = hit.collider != null ? LayerMask.LayerToName(hit.collider.gameObject.layer) : "<null>";
                        Debug.Log($"[DialogueBoxUI] Candidate '{e.displayName}' blocked by '{hitName}' layer='{hitLayer}'");
                    }
                    continue;
                }
            }

            best = e;
            bestDistSq = dSq;
        }

        if (best == null)
        {
            reason = "All candidates filtered (LOS blocked or missing NpcDialogueAgent). Ensure occluderMask excludes Player/NPC.";
            return null;
        }

        reason = "ok";
        return best;
    }

    private static bool HasLineOfSight(Vector3 from, Vector3 to, LayerMask occluderMask, out RaycastHit hit)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.01f)
        {
            hit = default;
            return true;
        }

        dir /= dist;
        return !Physics.Raycast(from, dir, out hit, dist, occluderMask, QueryTriggerInteraction.Ignore);
    }

    private Vector3 GetLosOrigin()
    {
        if (losOrigin != null) return losOrigin.position;
        if (viewCamera != null) return viewCamera.transform.position;
        return playerRoot != null ? (playerRoot.position + Vector3.up * 1.6f) : Vector3.zero;
    }

    private void Open(EntityInfo entity, NpcDialogueAgent agent)
    {
        _isOpen = true;
        _root.SetActive(true);

        if (pauseTimeWhenOpen) PauseTime();
        if (unlockCursorWhenOpen) CaptureAndUnlockCursor();

        BlockGameplayInputsIfNeeded();

        _headerText.text = $"Talking to: {entity.displayName}  ({entity.entityId})";

        // Large top panel: latest NPC line / initial prompt
        _npcResponseText.text = $"<color=#FFD36F><b>{Escape(entity.displayName)}:</b></color>\n<i>Speak. Don’t waste my time.</i>";

        AppendSystemLine("Type your message and press Enter.");
        FocusInput();
    }

    private void Close()
    {
        _isOpen = false;
        _root.SetActive(false);

        RestoreTimeScaleIfNeeded();
        RestoreCursorIfNeeded();
        RestoreGameplayInputsIfNeeded();

        _activeEntity = null;
        _activeAgent = null;
    }

    private void TrySend()
    {
        if (!_isOpen) return;

        if (_activeAgent == null)
        {
            AppendSystemLine("NPC missing NpcDialogueAgent.");
            FocusInput();
            return;
        }

        string playerText = _inputField != null ? _inputField.text : null;
        if (string.IsNullOrWhiteSpace(playerText))
        {
            FocusInput();
            return;
        }

        playerText = playerText.Trim();

        if (_inputField != null) _inputField.text = string.Empty;

        AppendPlayerLine(playerText);
        FocusInput();

        // Prevent double click / Enter spam while request is pending
        if (_sendButton != null) _sendButton.interactable = false;

        _activeAgent.SendPlayerMessage(playerText, npcReply =>
        {
            if (_sendButton != null) _sendButton.interactable = true;

            string npcName = _activeEntity != null ? _activeEntity.displayName : (_activeAgent != null ? _activeAgent.NpcName : "NPC");
            string reply = string.IsNullOrWhiteSpace(npcReply) ? "<no response>" : npcReply.Trim();

            // Update the big top panel
            _npcResponseText.text = $"<color=#FFD36F><b>{Escape(npcName)}:</b></color>\n{Escape(reply)}";

            AppendNpcLine(npcName, reply);
            FocusInput();
        });
    }

    // ---- Time/Cursor ----

    private void PauseTime()
    {
        if (_pausedByUs) return;
        _previousTimeScale = Time.timeScale;
        if (_previousTimeScale < 0.0001f) _previousTimeScale = 1f;
        Time.timeScale = 0f;
        _pausedByUs = true;
    }

    private void RestoreTimeScaleIfNeeded()
    {
        if (!_pausedByUs) return;
        Time.timeScale = Mathf.Max(0.0001f, _previousTimeScale);
        _pausedByUs = false;
    }

    private void CaptureAndUnlockCursor()
    {
        if (_cursorCaptured) return;
        _prevCursorLock = Cursor.lockState;
        _prevCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _cursorCaptured = true;
    }

    private void RestoreCursorIfNeeded()
    {
        if (!_cursorCaptured) return;
        Cursor.lockState = _prevCursorLock;
        Cursor.visible = _prevCursorVisible;
        _cursorCaptured = false;
    }

    // ---- Gameplay input blocking ----

    private void BlockGameplayInputsIfNeeded()
    {
        if (playerRoot == null) return;

        if (disablePlayerInputWhileOpen)
        {
            _blockedPlayerInputs = playerRoot.GetComponentsInChildren<PlayerInput>(true);
            for (int i = 0; i < _blockedPlayerInputs.Length; i++)
            {
                var pi = _blockedPlayerInputs[i];
                if (pi == null) continue;
                pi.enabled = false;
            }
        }

        if (disableInputIntentRecorderWhileOpen)
        {
            // Avoid hard reference. Match by type name.
            var all = playerRoot.GetComponentsInChildren<MonoBehaviour>(true);
            var list = new List<MonoBehaviour>(8);
            for (int i = 0; i < all.Length; i++)
            {
                var mb = all[i];
                if (mb == null) continue;
                if (mb.GetType().Name == "InputIntentRecorder")
                {
                    mb.enabled = false;
                    list.Add(mb);
                }
            }
            _blockedIntentRecorders = list.Count > 0 ? list.ToArray() : null;
        }
    }

    private void RestoreGameplayInputsIfNeeded()
    {
        if (_blockedPlayerInputs != null)
        {
            for (int i = 0; i < _blockedPlayerInputs.Length; i++)
            {
                var pi = _blockedPlayerInputs[i];
                if (pi == null) continue;
                pi.enabled = true;
            }
            _blockedPlayerInputs = null;
        }

        if (_blockedIntentRecorders != null)
        {
            for (int i = 0; i < _blockedIntentRecorders.Length; i++)
            {
                var mb = _blockedIntentRecorders[i];
                if (mb == null) continue;
                mb.enabled = true;
            }
            _blockedIntentRecorders = null;
        }
    }

    // ---- Transcript ----

    private void AppendSystemLine(string s) => AppendLine($"<color=#AAAAAA>[system]</color> {Escape(s)}");
    private void AppendPlayerLine(string s) => AppendLine($"<color=#6FC1FF>[you]</color> {Escape(s)}");
    private void AppendNpcLine(string npcName, string s) => AppendLine($"<color=#FFD36F>[{Escape(npcName)}]</color> {Escape(s)}");

    private void AppendLine(string richTextLine)
    {
        if (_transcriptText == null) return;

        if (string.IsNullOrEmpty(_transcriptText.text))
            _transcriptText.text = richTextLine;
        else
            _transcriptText.text += "\n" + richTextLine;

        if (_scrollRect != null)
            StartCoroutine(ScrollToBottomNextFrame());
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 0f;
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("<", "‹").Replace(">", "›");
    }

    // ---- UI Build ----

    private void BuildUI()
    {
        _root = new GameObject("DialogueUI_Root");
        _root.transform.SetParent(transform, false);

        _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        _root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _root.AddComponent<GraphicRaycaster>();

        var panelGO = CreateUIObject("Panel", _root.transform);
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = new Vector2(0f, 20f);
        panelRT.sizeDelta = new Vector2(panelWidth, panelHeight);

        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.78f);

        var vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 12, 12);
        vlg.spacing = 10;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        // Header row
        var headerRowGO = CreateUIObject("HeaderRow", panelRT);
        AddLayout(headerRowGO, flexibleWidth: 1, preferredWidth: -1, preferredHeight: 36);

        var hlg = headerRowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        _headerText = CreateTMPText("HeaderText", headerRowGO.transform, headerFontSize, FontStyles.Bold);
        _headerText.text = "Dialogue";
        _headerText.alignment = TextAlignmentOptions.MidlineLeft;
        AddLayout(_headerText.gameObject, flexibleWidth: 1, preferredWidth: -1, preferredHeight: 36);

        _closeButton = CreateButton("CloseButton", headerRowGO.transform, "X", height: 36, onClick: Close, preferredWidth: 90);

        // Big NPC response panel
        var npcPanelGO = CreateUIObject("NpcResponsePanel", panelRT);
        npcPanelGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.07f);
        AddLayout(npcPanelGO, flexibleWidth: 1, preferredWidth: -1, preferredHeight: npcResponsePanelHeight);

        var npcInner = CreateUIObject("NpcResponseInner", npcPanelGO.transform);
        var npcInnerRT = npcInner.GetComponent<RectTransform>();
        npcInnerRT.anchorMin = Vector2.zero;
        npcInnerRT.anchorMax = Vector2.one;
        npcInnerRT.offsetMin = new Vector2(12, 10);
        npcInnerRT.offsetMax = new Vector2(-12, -10);

        _npcResponseText = CreateTMPText("NpcResponseText", npcInner.transform, npcResponseFontSize, FontStyles.Normal);
        _npcResponseText.enableWordWrapping = true;
        _npcResponseText.alignment = TextAlignmentOptions.TopLeft;
        _npcResponseText.margin = Vector4.zero;
        StretchToParent(_npcResponseText.rectTransform);

        // Transcript scroll
        var scrollGO = CreateUIObject("TranscriptScroll", panelRT);
        scrollGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
        AddLayout(scrollGO, flexibleWidth: 1, preferredWidth: -1, preferredHeight: -1); // flexible height: takes remaining

        _scrollRect = scrollGO.AddComponent<ScrollRect>();
        _scrollRect.horizontal = false;

        var viewportGO = CreateUIObject("Viewport", scrollGO.transform);
        var viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(10, 10);
        viewportRT.offsetMax = new Vector2(-10, -10);

        viewportGO.AddComponent<RectMask2D>();
        viewportGO.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        _scrollRect.viewport = viewportRT;

        var contentGO = CreateUIObject("Content", viewportRT);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0, 0);

        var contentVlg = contentGO.AddComponent<VerticalLayoutGroup>();
        contentVlg.childAlignment = TextAnchor.UpperLeft;
        contentVlg.childForceExpandHeight = false;
        contentVlg.childForceExpandWidth = true;

        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scrollRect.content = contentRT;

        _transcriptText = CreateTMPText("TranscriptText", contentGO.transform, fontSize, FontStyles.Normal);
        _transcriptText.text = "";
        _transcriptText.enableWordWrapping = true;
        _transcriptText.alignment = TextAlignmentOptions.TopLeft;
        _transcriptText.margin = Vector4.zero;

        // Critical: ensure full-width in layout so it doesn't crop the left side.
        StretchWidthForLayout(_transcriptText.rectTransform);
        AddLayout(_transcriptText.gameObject, flexibleWidth: 1, preferredWidth: -1, preferredHeight: -1);

        // Input row
        var inputRowGO = CreateUIObject("InputRow", panelRT);
        AddLayout(inputRowGO, flexibleWidth: 1, preferredWidth: -1, preferredHeight: 44);

        var inputHlg = inputRowGO.AddComponent<HorizontalLayoutGroup>();
        inputHlg.spacing = 10;
        inputHlg.childAlignment = TextAnchor.MiddleLeft;
        inputHlg.childForceExpandWidth = true;
        inputHlg.childForceExpandHeight = true;

        var inputGO = CreateUIObject("InputField", inputRowGO.transform);
        inputGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);
        AddLayout(inputGO, flexibleWidth: 1, preferredWidth: -1, preferredHeight: 44);

        _inputField = inputGO.AddComponent<TMP_InputField>();
        _inputField.lineType = TMP_InputField.LineType.SingleLine;
        _inputField.richText = false;

        var placeholder = CreateTMPText("Placeholder", inputGO.transform, fontSize, FontStyles.Italic);
        placeholder.text = "Type message…";
        placeholder.color = new Color(1f, 1f, 1f, 0.45f);
        placeholder.margin = Vector4.zero;

        var inputText = CreateTMPText("Text", inputGO.transform, fontSize, FontStyles.Normal);
        inputText.text = "";
        inputText.margin = Vector4.zero;

        var phRT = placeholder.rectTransform;
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(12, 8); phRT.offsetMax = new Vector2(-12, -8);

        var itRT = inputText.rectTransform;
        itRT.anchorMin = Vector2.zero; itRT.anchorMax = Vector2.one;
        itRT.offsetMin = new Vector2(12, 8); itRT.offsetMax = new Vector2(-12, -8);

        _inputField.placeholder = placeholder;
        _inputField.textComponent = inputText;

        _sendButton = CreateButton("SendButton", inputRowGO.transform, "Send", height: 44, onClick: TrySend, preferredWidth: 180);
        _sendButton.interactable = true;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private TMP_Text CreateTMPText(string name, Transform parent, int size, FontStyles style)
    {
        var go = CreateUIObject(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    private Button CreateButton(string name, Transform parent, string label, int height, Action onClick, float preferredWidth)
    {
        var go = CreateUIObject(name, parent);
        AddLayout(go, flexibleWidth: 0, preferredWidth: preferredWidth, preferredHeight: height);

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.14f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var txt = CreateTMPText("Label", go.transform, fontSize, FontStyles.Bold);
        txt.text = label;
        txt.alignment = TextAlignmentOptions.Center;

        var txtRT = txt.rectTransform;
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        return btn;
    }

    private void FocusInput()
    {
        if (_inputField == null) return;
        _inputField.ActivateInputField();
        _inputField.Select();
    }

    private static void EnsureEventSystem_InputSystem()
    {
        var es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
        }

        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(legacy);
#else
            Destroy(legacy);
#endif
        }

        if (es.GetComponent<InputSystemUIInputModule>() == null)
            es.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    private static void AddLayout(GameObject go, float flexibleWidth, float preferredWidth, float preferredHeight)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();

        le.flexibleWidth = Mathf.Max(0, flexibleWidth);

        // If preferredWidth/Height are negative, leave unset so layout can decide.
        if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;

        // Flexible height is used by Scroll panel; if preferredHeight is -1, we let it stretch.
        if (preferredHeight < 0)
            le.flexibleHeight = 1f;
        else
            le.flexibleHeight = 0f;
    }

    private static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Ensures TMP inside layout/content fills width and doesn't clip the left edge.
    private static void StretchWidthForLayout(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, 0);
        rt.offsetMin = new Vector2(0, rt.offsetMin.y);
        rt.offsetMax = new Vector2(0, rt.offsetMax.y);
    }
}
