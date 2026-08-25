// Assets/Assets/Scripts/Dialogue/DialogueBoxUI.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DialogueBoxUI : MonoBehaviour
{
    [Header("Interaction")]
    public Transform playerRoot;
    public Camera viewCamera;
    public LayerMask entityMask = ~0;
    public LayerMask occluderMask = ~0;
    [Range(0.5f, 12f)] public float talkRadius = 4f;
    public Transform losOrigin;

    [Header("Targeting Rules")]
    public bool requireLineOfSight = true;
    public bool requireDialogueAgent = true;

    [Header("Debug")]
    public bool debugLogs = true;

    [Header("UI")]
    [Range(10, 36)] public int fontSize = 18;
    [Range(8, 28)] public int headerFontSize = 20;
    [Range(10, 42)] public int npcResponseFontSize = 24;
    [Range(200, 900)] public int panelWidth = 720;
    [Range(200, 900)] public int panelHeight = 520;
    [Range(60, 240)] public int npcResponsePanelHeight = 130;

    [Header("Behavior")]
    public bool pauseTimeWhenOpen = false;
    public bool unlockCursorWhenOpen = true;

    [Header("Input Blocking While Open")]
    public bool disablePlayerInputWhileOpen = true;
    public bool disableInputIntentRecorderWhileOpen = true;

    private Canvas _canvas;
    private CanvasScaler _canvasScaler;
    private GameObject _root;
    private TMP_Text _headerText;
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
        ResolveViewCamera();
        ResolvePlayerRoot();
    }

    private void Awake()
    {
        ResolveViewCamera();
        ResolvePlayerRoot();
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
            // note: Runtime NPCs can outlive this canvas; always release the transcript event when presentation is disabled.
            SetActiveAgent(null);
            RestoreTimeScaleIfNeeded();
            RestoreCursorIfNeeded();
            RestoreGameplayInputsIfNeeded();
        }
    }

    private void Update()
    {
        ResolveViewCamera();
        ResolvePlayerRoot();

        var kb = Keyboard.current;
        if (kb == null) return;

        if (!_isOpen)
        {
            if (kb.eKey.wasPressedThisFrame)
                TryOpenNearestNpc();
        }
        else
        {
            if (kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                TrySend();
        }
    }

    private void ResolvePlayerRoot()
    {
        if (playerRoot) return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerRoot = player.transform;
            return;
        }

        PlayerController controller = FindFirstObjectByType<PlayerController>();
        if (controller != null)
        {
            playerRoot = controller.transform;
            return;
        }

        CharacterController cc = FindFirstObjectByType<CharacterController>();
        if (cc != null)
            playerRoot = cc.transform;
    }

    private void ResolveViewCamera()
    {
        if (viewCamera) return;
        if (Camera.main != null)
        {
            viewCamera = Camera.main;
            return;
        }

        viewCamera = FindFirstObjectByType<Camera>();
    }

    private void TryOpenNearestNpc()
    {
        ResolvePlayerRoot();
        ResolveViewCamera();

        if (!playerRoot)
        {
            if (debugLogs) Debug.LogWarning("[DialogueBoxUI] Cannot open dialogue. playerRoot is null.");
            return;
        }

        EntityInfo nearest = FindNearestTalkableNpc(out string reason);
        if (nearest == null)
        {
            if (debugLogs) Debug.Log($"[DialogueBoxUI] No talk target. Reason: {reason}");
            return;
        }

        if (!nearest)
        {
            if (debugLogs) Debug.LogWarning("[DialogueBoxUI] Candidate was destroyed before open.");
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
        ResolvePlayerRoot();
        ResolveViewCamera();

        if (!playerRoot)
        {
            reason = "playerRoot null";
            return null;
        }

        Vector3 center = playerRoot.position;
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
            EntityInfo e = nearby[i];
            if (!e) continue;
            Transform candidateTransform = e.transform;
            if (!candidateTransform) continue;
            if (requireDialogueAgent && e.GetComponentInChildren<NpcDialogueAgent>() == null) continue;

            Vector3 target = candidateTransform.position;
            float dSq = (target - center).sqrMagnitude;
            if (dSq >= bestDistSq) continue;

            if (requireLineOfSight)
            {
                if (!HasLineOfSightToEntity(e, origin, target + Vector3.up * 1.4f, occluderMask, out RaycastHit hit))
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

    private bool HasLineOfSightToEntity(EntityInfo entity, Vector3 from, Vector3 to, LayerMask mask, out RaycastHit blockingHit)
    {
        blockingHit = default;
        if (!entity) return false;

        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.01f) return true;
        dir /= dist;

        RaycastHit[] hits = Physics.RaycastAll(from, dir, dist, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return true;
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform entityRoot = entity.transform;
        Transform player = playerRoot;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            Transform hitTransform = col.transform;
            if (hitTransform == null) continue;

            if (entityRoot && (hitTransform == entityRoot || hitTransform.IsChildOf(entityRoot)))
                continue;

            if (player && (hitTransform == player || hitTransform.IsChildOf(player)))
                continue;

            blockingHit = hits[i];
            return false;
        }

        return true;
    }

    private Vector3 GetLosOrigin()
    {
        if (losOrigin) return losOrigin.position;
        if (viewCamera) return viewCamera.transform.position;
        if (playerRoot) return playerRoot.position + Vector3.up * 1.6f;
        return Vector3.zero;
    }

    private void Open(EntityInfo entity, NpcDialogueAgent agent)
    {
        if (!entity) return;
        if (requireDialogueAgent && agent == null) return;

        _activeEntity = entity;
        SetActiveAgent(agent);
        _activeAgent?.RefreshIdentityAndSession();

        ResolvePlayerRoot();
        _isOpen = true;
        _root.SetActive(true);

        if (pauseTimeWhenOpen) PauseTime();
        if (unlockCursorWhenOpen) CaptureAndUnlockCursor();
        BlockGameplayInputsIfNeeded();

        _headerText.text = $"Talking to: {entity.displayName} ({entity.entityId})";
        string lastLine = agent != null && !string.IsNullOrWhiteSpace(agent.LastNpcLine) ? agent.LastNpcLine.Trim() : "Conversation ready.";
        _npcResponseText.text = $"<color=#FFD36F><b>{Escape(entity.displayName)}:</b></color>\n{Escape(lastLine)}";
        RebuildTranscriptFromAgent();
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
        SetActiveAgent(null);
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
        if (_sendButton != null) _sendButton.interactable = false;

        _activeAgent.SendPlayerMessage(playerText, npcReply =>
        {
            if (_sendButton != null) _sendButton.interactable = true;
            string npcName = _activeEntity != null ? _activeEntity.displayName : (_activeAgent != null ? _activeAgent.NpcName : "NPC");
            string reply = string.IsNullOrWhiteSpace(npcReply) ? "<no response>" : npcReply.Trim();
            _npcResponseText.text = $"<color=#FFD36F><b>{Escape(npcName)}:</b></color>\n{Escape(reply)}";
            RebuildTranscriptFromAgent();
            FocusInput();
        });
    }

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

    private void BlockGameplayInputsIfNeeded()
    {
        ResolvePlayerRoot();
        if (!playerRoot) return;

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

    private void AppendSystemLine(string s) => AppendLine($"<color=#AAAAAA>[system]</color> {Escape(s)}");
    private void AppendPlayerLine(string s) => AppendLine($"<color=#6FC1FF>[you]</color> {Escape(s)}");
    private void AppendNpcLine(string npcName, string s) => AppendLine($"<color=#FFD36F>[{Escape(npcName)}]</color> {Escape(s)}");

    private void AppendLine(string richTextLine)
    {
        if (_transcriptText == null) return;
        if (string.IsNullOrEmpty(_transcriptText.text)) _transcriptText.text = richTextLine;
        else _transcriptText.text += "\n" + richTextLine;
        if (_scrollRect != null) StartCoroutine(ScrollToBottomNextFrame());
    }

    private void RebuildTranscriptFromAgent()
    {
        if (_transcriptText == null)
            return;

        if (_activeAgent == null)
        {
            _transcriptText.text = "<color=#AAAAAA>[system]</color> No NPC transcript available.";
            if (_scrollRect != null) StartCoroutine(ScrollToBottomNextFrame());
            return;
        }

        List<DialogueTurn> turns = _activeAgent.GetRecentTurnsSnapshot(256);
        if (turns == null || turns.Count == 0)
        {
            _transcriptText.text = "<color=#AAAAAA>[system]</color> Conversation ready.";
            if (_scrollRect != null) StartCoroutine(ScrollToBottomNextFrame());
            return;
        }

        string npcName = _activeEntity != null ? _activeEntity.displayName : _activeAgent.NpcName;
        StringBuilder sb = new StringBuilder(4096);
        for (int i = 0; i < turns.Count; i++)
        {
            DialogueTurn turn = turns[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text))
                continue;

            string speaker = string.IsNullOrWhiteSpace(turn.speaker) ? "npc" : turn.speaker.Trim().ToLowerInvariant();
            sb.Append(speaker == "player"
                ? "<color=#6FC1FF>[you]</color> "
                : "<color=#FFD36F>[" + Escape(npcName) + "]</color> ");
            sb.Append(Escape(turn.text.Trim()));
            if (i < turns.Count - 1)
                sb.Append('\n');
        }

        _transcriptText.text = sb.ToString();
        if (_scrollRect != null) StartCoroutine(ScrollToBottomNextFrame());
    }

    private void SetActiveAgent(
        NpcDialogueAgent agent)
    {
        if (_activeAgent != null)
        {
            _activeAgent.TranscriptChanged -=
                HandleActiveTranscriptChanged;
        }

        _activeAgent =
            agent;

        if (_activeAgent != null)
        {
            // note: Both the committed player turn and asynchronous NPC reply rebuild from the persisted session, keeping the visible transcript authoritative.
            _activeAgent.TranscriptChanged +=
                HandleActiveTranscriptChanged;
        }
    }

    private void HandleActiveTranscriptChanged()
    {
        if (!_isOpen ||
            _activeAgent == null)
        {
            return;
        }

        RebuildTranscriptFromAgent();
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 0f;
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private void BuildUI()
    {
        _root = new GameObject("DialogueUI_Root");
        _root.transform.SetParent(transform, false);

        _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.pixelPerfect = false;

        _canvasScaler = _root.AddComponent<CanvasScaler>();
        // note: Dialogue uses the shared UI scale so it matches the tutorial and menu overlays.
        YQUITheme.ApplyCanvasScaler(_canvasScaler);

        _root.AddComponent<GraphicRaycaster>();

        GameObject panelGO = CreateUIObject("Panel", _root.transform);
        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = new Vector2(0f, 20f);
        panelRT.sizeDelta = new Vector2(panelWidth, panelHeight);

        Image panelImg = panelGO.AddComponent<Image>();
        YQUITheme.ApplyPanel(panelImg);

        VerticalLayoutGroup vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 12, 12);
        vlg.spacing = 10;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;

        GameObject headerRowGO = CreateUIObject("HeaderRow", panelRT);
        AddLayout(headerRowGO, 1f, -1f, 36f);
        HorizontalLayoutGroup hlg = headerRowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        _headerText = CreateTMPText("HeaderText", headerRowGO.transform, headerFontSize, FontStyles.Bold);
        _headerText.text = "Dialogue";
        _headerText.alignment = TextAlignmentOptions.MidlineLeft;
        _headerText.color = YQUITheme.Gold;
        AddLayout(_headerText.gameObject, 1f, -1f, 36f);

        _closeButton = CreateButton("CloseButton", headerRowGO.transform, "X", 36, Close, 90f);

        GameObject npcPanelGO = CreateUIObject("NpcResponsePanel", panelRT);
        // note: The NPC answer gets a softer inset panel so the active response is easier to scan.
        YQUITheme.ApplySoftPanel(npcPanelGO.AddComponent<Image>());
        AddLayout(npcPanelGO, 1f, -1f, npcResponsePanelHeight);

        GameObject npcInner = CreateUIObject("NpcResponseInner", npcPanelGO.transform);
        RectTransform npcInnerRT = npcInner.GetComponent<RectTransform>();
        npcInnerRT.anchorMin = Vector2.zero;
        npcInnerRT.anchorMax = Vector2.one;
        npcInnerRT.offsetMin = new Vector2(12f, 10f);
        npcInnerRT.offsetMax = new Vector2(-12f, -10f);

        _npcResponseText = CreateTMPText("NpcResponseText", npcInner.transform, npcResponseFontSize, FontStyles.Normal);
        _npcResponseText.textWrappingMode = TextWrappingModes.Normal;
        _npcResponseText.alignment = TextAlignmentOptions.TopLeft;
        _npcResponseText.margin = Vector4.zero;
        StretchToParent(_npcResponseText.rectTransform);

        GameObject scrollGO = CreateUIObject("TranscriptScroll", panelRT);
        YQUITheme.ApplySoftPanel(scrollGO.AddComponent<Image>());
        AddLayout(scrollGO, 1f, -1f, -1f);
        _scrollRect = scrollGO.AddComponent<ScrollRect>();
        _scrollRect.horizontal = false;

        GameObject viewportGO = CreateUIObject("Viewport", scrollGO.transform);
        RectTransform viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(10f, 10f);
        viewportRT.offsetMax = new Vector2(-10f, -10f);
        viewportGO.AddComponent<RectMask2D>();
        viewportGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        _scrollRect.viewport = viewportRT;

        GameObject contentGO = CreateUIObject("Content", viewportRT);
        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentVlg = contentGO.AddComponent<VerticalLayoutGroup>();
        contentVlg.childAlignment = TextAnchor.UpperLeft;
        contentVlg.childForceExpandHeight = false;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childControlHeight = true;
        contentVlg.childControlWidth = true;

        ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scrollRect.content = contentRT;

        _transcriptText = CreateTMPText("TranscriptText", contentGO.transform, fontSize, FontStyles.Normal);
        _transcriptText.text = string.Empty;
        _transcriptText.textWrappingMode = TextWrappingModes.Normal;
        _transcriptText.alignment = TextAlignmentOptions.TopLeft;
        _transcriptText.margin = Vector4.zero;
        StretchWidthForLayout(_transcriptText.rectTransform);
        AddLayout(_transcriptText.gameObject, 1f, -1f, -1f);

        GameObject inputRowGO = CreateUIObject("InputRow", panelRT);
        AddLayout(inputRowGO, 1f, -1f, 44f);
        HorizontalLayoutGroup inputHlg = inputRowGO.AddComponent<HorizontalLayoutGroup>();
        inputHlg.spacing = 10;
        inputHlg.childAlignment = TextAnchor.MiddleLeft;
        inputHlg.childForceExpandWidth = true;
        inputHlg.childForceExpandHeight = true;

        GameObject inputGO = CreateUIObject("InputField", inputRowGO.transform);
        Image inputImage = inputGO.AddComponent<Image>();
        inputImage.color = YQUITheme.Button;
        YQUITheme.AddFrame(inputGO);
        AddLayout(inputGO, 1f, -1f, 44f);

        _inputField = inputGO.AddComponent<TMP_InputField>();
        _inputField.lineType = TMP_InputField.LineType.SingleLine;
        _inputField.richText = false;

        TMP_Text placeholder = CreateTMPText("Placeholder", inputGO.transform, fontSize, FontStyles.Italic);
        placeholder.text = "Type message...";
        placeholder.color = YQUITheme.Muted;
        placeholder.margin = Vector4.zero;

        TMP_Text inputText = CreateTMPText("Text", inputGO.transform, fontSize, FontStyles.Normal);
        inputText.text = string.Empty;
        inputText.color = YQUITheme.Ink;
        inputText.margin = Vector4.zero;

        RectTransform phRT = placeholder.rectTransform;
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(12f, 8f); phRT.offsetMax = new Vector2(-12f, -8f);

        RectTransform itRT = inputText.rectTransform;
        itRT.anchorMin = Vector2.zero; itRT.anchorMax = Vector2.one;
        itRT.offsetMin = new Vector2(12f, 8f); itRT.offsetMax = new Vector2(-12f, -8f);

        _inputField.placeholder = placeholder;
        _inputField.textComponent = (TextMeshProUGUI)inputText;
        _sendButton = CreateButton("SendButton", inputRowGO.transform, "Send", 44, TrySend, 180f);
        _sendButton.interactable = true;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private TMP_Text CreateTMPText(string name, Transform parent, int size, FontStyles style)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.raycastTarget = false;
        // note: Runtime-created text shares wrapping and color defaults across all polished overlays.
        YQUITheme.ApplyText(t);
        return t;
    }

    private Button CreateButton(string name, Transform parent, string label, int height, Action onClick, float preferredWidth)
    {
        GameObject go = CreateUIObject(name, parent);
        AddLayout(go, 0f, preferredWidth, height);
        Image img = go.AddComponent<Image>();
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());
        // note: Buttons use one interaction palette so hover and press states feel consistent.
        YQUITheme.ApplyButton(btn);
        TMP_Text txt = CreateTMPText("Label", go.transform, fontSize, FontStyles.Bold);
        txt.text = label;
        txt.alignment = TextAlignmentOptions.Center;
        RectTransform txtRT = txt.rectTransform;
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
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
        EventSystem es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
        }

        StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
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
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = Mathf.Max(0f, flexibleWidth);
        if (preferredWidth >= 0f) le.preferredWidth = preferredWidth;
        if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
        le.flexibleHeight = preferredHeight < 0f ? 1f : 0f;
    }

    private static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void StretchWidthForLayout(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 0f);
        rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
        rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
    }
}
