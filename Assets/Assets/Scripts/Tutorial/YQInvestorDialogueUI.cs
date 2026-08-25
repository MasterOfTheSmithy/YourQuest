// Assets/Assets/Scripts/Tutorial/YQInvestorDialogueUI.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YQInvestorDialogueUI : MonoBehaviour
{
    public static YQInvestorDialogueUI Instance { get; private set; }
    public static bool IsOpenNow { get; private set; }

    [Header("World Refs")]
    public Transform playerRoot;
    public Camera viewCamera;
    [Range(1f, 7f)] public float talkRadius = 2.65f;
    public bool requireLineOfSight = true;
    public LayerMask entityMask = ~0;
    public LayerMask occluderMask = ~0;

    [Header("UI")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public Vector2 windowSize = new Vector2(920f, 680f);

    private Canvas _canvas;
    private TMP_Text _headerText;
    private TMP_Text _subHeaderText;
    private TMP_Text _latestText;
    private TMP_Text _transcriptText;
    private TMP_InputField _inputField;
    private ScrollRect _scrollRect;
    private RectTransform _transcriptContent;
    private Button _sendButton;
    private CanvasGroup _thinkingGroup;
    private TMP_Text _thinkingText;

    private readonly List<DialogueTurn> _fallbackTurns = new List<DialogueTurn>(16);

    private bool _open;
    private bool _waitingOnReply;
    private Coroutine _thinkingRoutine;
    private Coroutine _focusRoutine;
    private EntityInfo _activeEntity;
    private NpcDialogueAgent _activeAgent;

    public string VisibleTranscriptText => _transcriptText != null ? _transcriptText.text : string.Empty;
    public int VisibleTranscriptTurnCount => _activeAgent != null ? _activeAgent.GetRecentTurnsSnapshot(256).Count : 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        NormalizeWindowSize();
        BuildUi();
        SetOpen(false);
    }


    public void ForceCloseFromBootstrap()
    {
        _waitingOnReply = false;
        StopThinkingVisual();
        UnbindActiveAgent();
        _activeEntity = null;
        SetOpen(false);
        RuntimeModalUiBlocker.SetDialogueOpen(false);
    }

    private void OnDestroy()
    {
        StopInputFocusRoutine();
        UnbindActiveAgent();

        if (Instance == this)
            Instance = null;

        if (_open)
            RuntimeModalUiBlocker.SetDialogueOpen(false);
    }

    private void Update()
    {
        ResolveRefs();

        Keyboard kb = Keyboard.current;

        if (!_open)
        {
            if (!RuntimeModalUiBlocker.IsBlocked && kb != null && kb.eKey.wasPressedThisFrame)
            {
                // note: Prefer the NPC under the crosshair, then recover with the nearest visible talkable NPC when decorative collision obscures the ray.
                if (!TryOpenTargetedNpc())
                    TryOpenNearestNpc();
            }
            return;
        }

        if (kb == null)
            return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (_waitingOnReply)
            return;

        bool submitPressed = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
        bool shiftHeld = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        if (submitPressed && !shiftHeld)
            Send();
    }

    private void ResolveRefs()
    {
        if (playerRoot == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                playerRoot = player.transform;
        }

        if (viewCamera == null)
            viewCamera = Camera.main;
    }

    public bool TryOpenTargetedNpc()
    {
        ResolveRefs();
        if (_open || RuntimeModalUiBlocker.IsBlocked || viewCamera == null)
            return false;

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        float cameraOffset = playerRoot != null
            ? Vector3.Distance(viewCamera.transform.position, playerRoot.position + Vector3.up * 1.25f)
            : 0f;
        float rayDistance = talkRadius + Mathf.Clamp(cameraOffset, 0f, 5.25f);
        RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, entityMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i].collider;
            if (hit == null)
                continue;
            if (playerRoot != null && (hit.transform == playerRoot || hit.transform.IsChildOf(playerRoot)))
                continue;

            EntityInfo info = hit.GetComponentInParent<EntityInfo>();
            NpcDialogueAgent agent = info != null ? info.GetComponentInParent<NpcDialogueAgent>() : null;
            if (!IsValidNpc(info, agent))
                continue;

            Open(info, agent);
            return true;
        }

        return false;
    }

    public bool TryOpenNpcFromCollider(Collider source)
    {
        ResolveRefs();
        if (_open || RuntimeModalUiBlocker.IsBlocked || source == null)
            return false;

        EntityInfo info = source.GetComponentInParent<EntityInfo>();
        NpcDialogueAgent agent = info != null ? info.GetComponentInParent<NpcDialogueAgent>() : null;
        if (!IsValidNpc(info, agent))
            return false;

        Open(info, agent);
        return true;
    }

    public bool OpenNpcForValidation(EntityInfo info, NpcDialogueAgent agent)
    {
        if (!IsValidNpc(info, agent))
            return false;

        // note: Validation scenes exercise the same canonical window without relying on a player raycast.
        Open(info, agent);
        return true;
    }

    public bool TryOpenNearestNpc()
    {
        ResolveRefs();
        if (_open ||
            RuntimeModalUiBlocker.IsBlocked ||
            playerRoot == null)
        {
            return false;
        }

        Collider[] nearby =
            Physics.OverlapSphere(
                playerRoot.position,
                Mathf.Max(
                    0.8f,
                    talkRadius + 0.25f),
                entityMask,
                QueryTriggerInteraction.Ignore);

        EntityInfo bestInfo =
            null;

        NpcDialogueAgent bestAgent =
            null;

        float bestScore =
            float.MaxValue;

        for (int i = 0; i < nearby.Length; i++)
        {
            Collider candidate =
                nearby[i];

            if (candidate == null ||
                (playerRoot != null &&
                 (candidate.transform == playerRoot ||
                  candidate.transform.IsChildOf(playerRoot))))
            {
                continue;
            }

            EntityInfo info =
                candidate.GetComponentInParent<EntityInfo>();

            NpcDialogueAgent agent =
                info != null
                    ? info.GetComponentInParent<NpcDialogueAgent>()
                    : null;

            if (!IsValidNpc(
                    info,
                    agent))
            {
                continue;
            }

            Vector3 offset =
                info.transform.position -
                playerRoot.position;

            float distanceScore =
                offset.sqrMagnitude;

            float facingPenalty =
                viewCamera != null &&
                Vector3.Dot(
                    viewCamera.transform.forward,
                    offset.normalized) <
                0.15f
                    ? 100f
                    : 0f;

            float score =
                distanceScore +
                facingPenalty;

            if (score >= bestScore)
                continue;

            bestScore =
                score;

            bestInfo =
                info;

            bestAgent =
                agent;
        }

        if (bestInfo == null ||
            bestAgent == null)
        {
            return false;
        }

        // note: A successful proximity recovery opens the same authoritative dialogue session and transcript as direct targeting.
        Open(
            bestInfo,
            bestAgent);

        return true;
    }

    private bool IsValidNpc(EntityInfo info, NpcDialogueAgent agent)
    {
        if (info == null || agent == null)
            return false;
        if (info.hostility == Hostility.Hostile)
            return false;
        if (playerRoot != null)
        {
            float allowed = Mathf.Max(0.8f, talkRadius + 0.25f);
            if ((info.transform.position - playerRoot.position).sqrMagnitude > allowed * allowed)
                return false;
        }
        if (requireLineOfSight && !HasLineOfSight(info.transform, GetLosOrigin(), info.transform.position + Vector3.up * 1.35f))
            return false;
        return true;
    }

    private bool HasLineOfSight(Transform targetRoot, Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.01f)
            return true;

        dir /= dist;
        RaycastHit[] hits = Physics.RaycastAll(from, dir, dist, occluderMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null)
                continue;

            Transform hit = col.transform;
            if (hit == null)
                continue;

            if (targetRoot == hit || hit.IsChildOf(targetRoot))
                return true;
            if (playerRoot != null && (hit == playerRoot || hit.IsChildOf(playerRoot)))
                continue;

            return false;
        }

        return true;
    }

    private Vector3 GetLosOrigin()
    {
        if (viewCamera != null)
            return viewCamera.transform.position;
        if (playerRoot != null)
            return playerRoot.position + Vector3.up * 1.6f;
        return Vector3.zero;
    }

    private void Open(EntityInfo entity, NpcDialogueAgent agent)
    {
        UnbindActiveAgent();
        _activeEntity = entity;
        _activeAgent = agent;
        _activeAgent.RefreshIdentityAndSession();
        _activeAgent.TranscriptChanged += OnTranscriptChanged;
        _fallbackTurns.Clear();
        _waitingOnReply = false;
        StopThinkingVisual();

        _headerText.text = ResolveNpcDisplayName();
        _subHeaderText.text = BuildSubHeader(entity, agent);
        _latestText.text = string.IsNullOrWhiteSpace(agent.LastNpcLine) ? "The NPC watches and waits." : Escape(agent.LastNpcLine.Trim());

        if (_inputField != null)
        {
            _inputField.text = string.Empty;
            _inputField.interactable = true;
            _inputField.gameObject.SetActive(true);
        }
        if (_sendButton != null)
            _sendButton.interactable = true;

        SetOpen(true);
        RuntimeModalUiBlocker.SetDialogueOpen(true);
        RebuildTranscript();
        FocusInput();

        YQInvestorDirector director = FindFirstObjectByType<YQInvestorDirector>();
        if (director != null && entity != null && !string.IsNullOrWhiteSpace(entity.entityId))
            director.NotifyDialogueOpened(entity.entityId);
    }

    private void Close()
    {
        _waitingOnReply = false;
        StopThinkingVisual();
        UnbindActiveAgent();
        _activeEntity = null;
        StopInputFocusRoutine();
        SetOpen(false);
        RuntimeModalUiBlocker.SetDialogueOpen(false);
    }

    private void UnbindActiveAgent()
    {
        if (_activeAgent != null)
            _activeAgent.TranscriptChanged -= OnTranscriptChanged;

        _activeAgent = null;
    }

    private void OnTranscriptChanged()
    {
        if (!_open || _activeAgent == null)
            return;

        // note: Both player and NPC turns redraw immediately from the saved session, including replies committed outside this UI callback.
        if (_latestText != null && !string.IsNullOrWhiteSpace(_activeAgent.LastNpcLine))
            _latestText.text = Escape(_activeAgent.LastNpcLine.Trim());

        RebuildTranscript();
    }

    private void Send()
    {
        if (!_open || _waitingOnReply || _activeAgent == null)
            return;

        string text = _inputField != null ? _inputField.text : null;
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = text.Trim();
        _inputField.text = string.Empty;
        _waitingOnReply = true;

        if (_sendButton != null)
            _sendButton.interactable = false;
        if (_inputField != null)
            _inputField.interactable = false;

        StartThinkingVisual();
        PushFallbackTurn("player", text);
        _activeAgent.SendPlayerMessage(text, OnNpcReply);
        RebuildTranscript();
        FocusInput();
    }

    private void OnNpcReply(string npcReply)
    {
        _waitingOnReply = false;

        if (_sendButton != null)
            _sendButton.interactable = true;
        if (_inputField != null)
            _inputField.interactable = true;

        StopThinkingVisual();

        if (!string.IsNullOrWhiteSpace(npcReply))
        {
            PushFallbackTurn("npc", npcReply.Trim());
            _latestText.text = Escape(npcReply.Trim());
        }
        else if (_activeAgent != null && !string.IsNullOrWhiteSpace(_activeAgent.LastNpcLine))
            _latestText.text = Escape(_activeAgent.LastNpcLine.Trim());
        else
            _latestText.text = "The NPC studies you in silence.";

        RebuildTranscript();
        FocusInput();
    }

    private void StartThinkingVisual()
    {
        StopThinkingVisual();
        if (_thinkingGroup != null)
            _thinkingGroup.alpha = 1f;
        if (_thinkingText != null)
            _thinkingText.text = "Thinking.";
        _thinkingRoutine = StartCoroutine(ThinkingLoop());
    }

    private void StopThinkingVisual()
    {
        if (_thinkingRoutine != null)
        {
            StopCoroutine(_thinkingRoutine);
            _thinkingRoutine = null;
        }

        if (_thinkingGroup != null)
            _thinkingGroup.alpha = 0f;
    }

    private IEnumerator ThinkingLoop()
    {
        string[] states = { "Thinking.", "Thinking..", "Thinking..." };
        int index = 0;
        while (true)
        {
            if (_thinkingText != null)
                _thinkingText.text = states[index];
            index = (index + 1) % states.Length;
            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    private void SetOpen(bool value)
    {
        _open = value;
        IsOpenNow = value;

        if (_canvas != null)
            _canvas.enabled = value;

        Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = value;
    }

    private void FocusInput()
    {
        if (_inputField == null)
            return;

        StopInputFocusRoutine();
        ApplyInputFocus();

        if (_open && isActiveAndEnabled)
            _focusRoutine = StartCoroutine(FocusInputNextFrame());
    }

    private IEnumerator FocusInputNextFrame()
    {
        yield return null;
        yield return null;
        ApplyInputFocus();
        _focusRoutine = null;
    }

    private void ApplyInputFocus()
    {
        if (_inputField == null || !_inputField.interactable || !_inputField.gameObject.activeInHierarchy)
            return;

        EventSystem.current?.SetSelectedGameObject(_inputField.gameObject);
        _inputField.Select();
        _inputField.ActivateInputField();
        _inputField.MoveTextEnd(false);
    }

    private void StopInputFocusRoutine()
    {
        if (_focusRoutine == null)
            return;

        StopCoroutine(_focusRoutine);
        _focusRoutine = null;
    }

    private string ResolveNpcDisplayName()
    {
        if (_activeAgent != null && !string.IsNullOrWhiteSpace(_activeAgent.NpcName))
            return _activeAgent.NpcName;
        if (_activeEntity != null && !string.IsNullOrWhiteSpace(_activeEntity.displayName))
            return _activeEntity.displayName;
        return "NPC";
    }

    private static string BuildSubHeader(EntityInfo entity, NpcDialogueAgent agent)
    {
        string faction = entity != null && !string.IsNullOrWhiteSpace(entity.factionId) ? entity.factionId : "unknown faction";
        string role = agent != null ? agent.GetPrimaryRoleLabel() : "resident";
        string tags = agent != null && !string.IsNullOrWhiteSpace(agent.TagsCsv) ? agent.TagsCsv : "<none>";
        return faction + "  •  " + role + "  •  " + tags;
    }

    private void RebuildTranscript()
    {
        if (_transcriptText == null)
            return;

        List<DialogueTurn> turns = BuildMergedTranscript();
        if (turns == null || turns.Count == 0)
        {
            _transcriptText.text = "<color=#8F98A4>[system]</color> Conversation ready.";
            RefreshTranscriptLayout();
            return;
        }

        string npcName = ResolveNpcDisplayName();
        StringBuilder sb = new StringBuilder(4096);
        for (int i = 0; i < turns.Count; i++)
        {
            DialogueTurn turn = turns[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text))
                continue;

            string speaker = string.IsNullOrWhiteSpace(turn.speaker) ? "npc" : turn.speaker.Trim().ToLowerInvariant();
            if (speaker == "player")
            {
                sb.Append("<color=#84CDFF>[you]</color> ");
                sb.Append(Escape(turn.text));
            }
            else
            {
                sb.Append("<color=#FFD67A>[");
                sb.Append(Escape(npcName));
                sb.Append("]</color> ");
                sb.Append(Escape(turn.text));
            }

            if (i < turns.Count - 1)
                sb.Append("\n\n");
        }

        if (_waitingOnReply)
            sb.Append("\n\n<color=#8F98A4>[system]</color> Thinking...");

        _transcriptText.text = sb.ToString();
        RefreshTranscriptLayout();
    }

    private List<DialogueTurn> BuildMergedTranscript()
    {
        List<DialogueTurn> merged = new List<DialogueTurn>(72);
        if (_activeAgent != null)
            AppendTranscriptTurns(merged, _activeAgent.GetRecentTurnsSnapshot(256));

        // note: The agent session is canonical; UI fallback turns are used only if persistence produced no readable history.
        if (merged.Count == 0 &&
            _fallbackTurns.Count > 0)
            AppendTranscriptTurns(merged, _fallbackTurns);

        return TrimTranscript(merged, 256);
    }

    private static void AppendTranscriptTurns(List<DialogueTurn> output, IList<DialogueTurn> source)
    {
        if (output == null || source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            DialogueTurn turn = source[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text))
                continue;

            string speaker = string.IsNullOrWhiteSpace(turn.speaker) ? "npc" : turn.speaker.Trim().ToLowerInvariant();
            string text = turn.text.Trim();

            // note: Repeated words are legitimate conversation turns and must remain visible in the transcript.
            output.Add(new DialogueTurn
            {
                speaker = speaker,
                text = text
            });
        }
    }

    private static bool HasRecentDuplicate(List<DialogueTurn> turns, string speaker, string text)
    {
        if (turns == null || string.IsNullOrWhiteSpace(text))
            return false;

        int start = Mathf.Max(0, turns.Count - 12);
        for (int i = turns.Count - 1; i >= start; i--)
        {
            DialogueTurn existing = turns[i];
            if (existing == null)
                continue;

            string existingSpeaker = string.IsNullOrWhiteSpace(existing.speaker) ? "npc" : existing.speaker.Trim().ToLowerInvariant();
            string existingText = string.IsNullOrWhiteSpace(existing.text) ? string.Empty : existing.text.Trim();
            if (string.Equals(existingSpeaker, speaker, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existingText, text, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static List<DialogueTurn> TrimTranscript(List<DialogueTurn> turns, int maxTurns)
    {
        if (turns == null)
            return new List<DialogueTurn>();

        int overflow = turns.Count - Mathf.Max(1, maxTurns);
        if (overflow > 0)
            turns.RemoveRange(0, overflow);
        return turns;
    }

    private void RefreshTranscriptLayout()
    {
        if (_transcriptContent == null || _transcriptText == null)
            return;

        float viewportHeight = _scrollRect != null && _scrollRect.viewport != null
            ? Mathf.Max(180f, _scrollRect.viewport.rect.height - 16f)
            : 246f;

        _transcriptText.ForceMeshUpdate();
        float preferredHeight = Mathf.Max(viewportHeight, _transcriptText.preferredHeight + 18f);
        RectTransform textRect = _transcriptText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(0f, preferredHeight);

        _transcriptContent.anchorMin = new Vector2(0f, 1f);
        _transcriptContent.anchorMax = new Vector2(1f, 1f);
        _transcriptContent.pivot = new Vector2(0.5f, 1f);
        _transcriptContent.anchoredPosition = Vector2.zero;
        _transcriptContent.sizeDelta = new Vector2(0f, preferredHeight);
        Canvas.ForceUpdateCanvases();
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private void PushFallbackTurn(string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _fallbackTurns.Add(new DialogueTurn
        {
            speaker = string.IsNullOrWhiteSpace(speaker) ? "npc" : speaker.Trim().ToLowerInvariant(),
            text = text.Trim()
        });

        int overflow = _fallbackTurns.Count - 64;
        if (overflow > 0)
            _fallbackTurns.RemoveRange(0, overflow);
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        yield return null;
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 0f;
    }

    private void BuildUi()
    {
        NormalizeWindowSize();
        GameObject canvasGo = new GameObject("YQInvestorDialogueCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5100;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform panel = CreatePanel(canvasGo.transform, "Window", new Vector2(0.5f, 0.5f), windowSize, Vector2.zero, new Color(0.045f, 0.05f, 0.06f, 0.98f));
        AddFrame(panel, new Color(0.68f, 0.61f, 0.42f, 0.50f));

        RectTransform header = CreatePanel(panel, "Header", new Vector2(0.5f, 1f), new Vector2(windowSize.x, 64f), Vector2.zero, new Color(0.08f, 0.09f, 0.11f, 1f));
        header.anchorMin = new Vector2(0.5f, 1f);
        header.anchorMax = new Vector2(0.5f, 1f);
        header.pivot = new Vector2(0.5f, 1f);

        _headerText = CreateText(header, "HeaderText", 25f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(18f, -11f), new Vector2(690f, 30f));
        _subHeaderText = CreateText(header, "SubHeaderText", 14f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(20f, -40f), new Vector2(780f, 20f));
        _subHeaderText.color = new Color32(176, 186, 198, 255);

        Button close = CreateButton(header, "Close", new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(96f, 34f), "Close");
        close.onClick.AddListener(Close);

        RectTransform latestPanel = CreatePanel(panel, "LatestPanel", new Vector2(0.5f, 1f), new Vector2(windowSize.x - 28f, 88f), new Vector2(0f, -76f), new Color(0.07f, 0.08f, 0.10f, 0.94f));
        latestPanel.anchorMin = new Vector2(0.5f, 1f);
        latestPanel.anchorMax = new Vector2(0.5f, 1f);
        latestPanel.pivot = new Vector2(0.5f, 1f);
        AddFrame(latestPanel, new Color(0f, 0f, 0f, 0.18f));

        TMP_Text latestHeader = CreateText(latestPanel, "LatestHeader", 14f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(16f, -12f), new Vector2(200f, 18f));
        latestHeader.text = "LATEST REPLY";
        latestHeader.color = new Color32(255, 240, 184, 255);

        _latestText = CreateText(latestPanel, "LatestText", 19f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(16f, -33f), new Vector2(windowSize.x - 80f, 48f));
        _latestText.textWrappingMode = TextWrappingModes.Normal;
        _latestText.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform transcriptRoot = CreatePanel(panel, "TranscriptRoot", new Vector2(0.5f, 1f), new Vector2(windowSize.x - 28f, 300f), new Vector2(0f, -174f), new Color(0.03f, 0.04f, 0.05f, 0.82f));
        AddFrame(transcriptRoot, new Color(0f, 0f, 0f, 0.18f));

        TMP_Text transcriptHeader = CreateText(transcriptRoot, "TranscriptHeader", 14f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(14f, -10f), new Vector2(240f, 18f));
        transcriptHeader.text = "TRANSCRIPT";
        transcriptHeader.color = new Color32(255, 240, 184, 255);

        RectTransform scrollRoot = CreatePanel(transcriptRoot, "TranscriptScrollRoot", new Vector2(0f, 0f), new Vector2(windowSize.x - 56f, 262f), new Vector2(14f, 14f), new Color(0f, 0f, 0f, 0f));
        scrollRoot.anchorMin = new Vector2(0f, 0f);
        scrollRoot.anchorMax = new Vector2(1f, 0f);
        scrollRoot.pivot = new Vector2(0f, 0f);
        scrollRoot.offsetMin = new Vector2(14f, 14f);
        scrollRoot.offsetMax = new Vector2(-14f, 0f);
        scrollRoot.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 32f, 254f);

        _scrollRect = BuildScrollRect(scrollRoot, out RectTransform viewport, out _transcriptContent);
        _transcriptText = CreateStretchText(_transcriptContent, "TranscriptText", 16f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _transcriptText.textWrappingMode = TextWrappingModes.Normal;
        _transcriptText.overflowMode = TextOverflowModes.Overflow;
        _transcriptText.richText = true;
        _transcriptText.margin = new Vector4(8f, 8f, 8f, 8f);
        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
        }

        RectTransform thinking = CreatePanel(panel, "Thinking", new Vector2(0.5f, 0f), new Vector2(windowSize.x - 28f, 28f), new Vector2(0f, 136f), new Color(0f, 0f, 0f, 0f));
        _thinkingGroup = thinking.gameObject.AddComponent<CanvasGroup>();
        _thinkingGroup.alpha = 0f;
        _thinkingText = CreateText(thinking, "ThinkingText", 14f, FontStyles.Italic, TextAlignmentOptions.TopLeft, new Vector2(10f, -4f), new Vector2(400f, 20f));
        _thinkingText.color = new Color32(176, 186, 198, 255);

        RectTransform inputPanel = CreatePanel(panel, "InputPanel", new Vector2(0.5f, 0f), new Vector2(windowSize.x - 28f, 126f), new Vector2(0f, 14f), new Color(0.08f, 0.095f, 0.12f, 0.99f));
        AddFrame(inputPanel, new Color(0.72f, 0.66f, 0.46f, 0.28f));

        TMP_Text inputHeader = CreateText(inputPanel, "InputHeader", 15f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(18f, -11f), new Vector2(300f, 20f));
        inputHeader.text = "TYPE YOUR REPLY";
        inputHeader.color = new Color32(255, 240, 184, 255);

        _inputField = CreateInputField(inputPanel, new Vector2(18f, -40f), new Vector2(windowSize.x - 206f, 72f));
        _sendButton = CreateButton(inputPanel, "Send", new Vector2(1f, 1f), new Vector2(-18f, -40f), new Vector2(148f, 72f), "Send");
        _sendButton.onClick.AddListener(Send);
    }

    private void NormalizeWindowSize()
    {
        windowSize = new Vector2(Mathf.Clamp(windowSize.x, 760f, 920f), Mathf.Clamp(windowSize.y, 560f, 680f));
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

    private static void AddFrame(RectTransform target, Color color)
    {
        Outline outline = target.gameObject.AddComponent<Outline>();
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
        text.color = Color.white;
        return text;
    }

    private static TMP_Text CreateStretchText(Transform parent, string name, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(0f, 0f);
        rt.offsetMax = new Vector2(0f, 0f);
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, string label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.18f, 0.20f, 0.24f, 1f);
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.70f, 0.64f, 0.44f, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text text = CreateStretchText(go.transform, "Label", 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        text.text = label;
        text.margin = new Vector4(8f, 8f, 8f, 8f);
        return go.GetComponent<Button>();
    }

    private ScrollRect BuildScrollRect(RectTransform parent, out RectTransform viewport, out RectTransform content)
    {
        GameObject scrollGo = new GameObject("ScrollRect", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.SetParent(parent, false);
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        // note: RectMask2D clips transcript geometry directly and avoids the transparent stencil-mask failure that hid valid persisted turns.
        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport = viewportGo.GetComponent<RectTransform>();
        viewport.SetParent(scrollRt, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        content = contentGo.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.offsetMin = new Vector2(0f, -246f);
        content.offsetMax = Vector2.zero;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        return scroll;
    }

    private TMP_InputField CreateInputField(Transform parent, Vector2 anchoredPosition, Vector2 dimensions)
    {
        GameObject root = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = dimensions;
        root.GetComponent<Image>().color = new Color(0.16f, 0.19f, 0.25f, 1f);
        AddFrame(rt, new Color(0.84f, 0.76f, 0.48f, 0.36f));

        GameObject viewportGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        RectTransform viewport = viewportGo.GetComponent<RectTransform>();
        viewport.SetParent(root.transform, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(18f, 12f);
        viewport.offsetMax = new Vector2(-18f, -12f);

        TMP_Text placeholder = CreateStretchText(viewport, "Placeholder", 19f, FontStyles.Italic, TextAlignmentOptions.TopLeft);
        placeholder.text = "Type anything you want to say...";
        placeholder.color = new Color32(166, 178, 194, 255);
        placeholder.margin = new Vector4(0f, 4f, 0f, 0f);

        TMP_Text text = CreateStretchText(viewport, "Text", 19f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        text.color = new Color32(244, 248, 255, 255);
        text.text = string.Empty;
        text.margin = new Vector4(0f, 4f, 0f, 0f);

        TMP_InputField input = root.GetComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = text as TextMeshProUGUI;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.richText = false;
        input.caretWidth = 3;
        input.characterLimit = 1200;
        input.selectionColor = new Color(0.48f, 0.78f, 1f, 0.42f);
        input.customCaretColor = true;
        input.caretColor = new Color32(255, 240, 184, 255);
        return input;
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
