using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQTitleScreenUI : MonoBehaviour
{
    private const float IntroTransitionSeconds = 1.05f;
    private const float ExitTransitionSeconds = 0.52f;
    private const float ViewTransitionSeconds = 0.32f;

    public static YQTitleScreenUI Instance { get; private set; }
    public static bool StartupGateActive { get; private set; }
    public static bool StartupFlowComplete { get; private set; }
    public static bool CanOpenOriginQuestionnaire => !StartupGateActive || StartupFlowComplete;
    public static bool StartupPresentationActive => StartupGateActive && !StartupFlowComplete;

    private enum ViewMode
    {
        Saves,
        CharacterCreation
    }

    private static readonly string[] PronounOptions = { "they/them", "he/him", "she/her", "custom" };
    private static readonly string[] BodyFrameOptions = { "masculine", "feminine", "androgynous", "custom" };

    private readonly object _modalToken = new object();
    private ViewMode _view = ViewMode.Saves;
    private Vector2 _saveScroll;
    private Vector2 _characterScroll;
    private string _selectedProfileId = string.Empty;
    private string _confirmDeleteProfileId = string.Empty;
    private string _status = string.Empty;
    private string _newName = string.Empty;
    private string _customPronouns = string.Empty;
    private string _customBodyFrame = string.Empty;
    private string _lifeDirection = string.Empty;
    private string _vow = string.Empty;
    private string _appearance = string.Empty;
    private int _pronounIndex;
    private int _bodyFrameIndex;
    private bool _open;
    private bool _closing;
    private bool _pendingStartupCompletion;
    private bool _hoverSeen;
    private float _openedAt;
    private float _exitStartedAt;
    private float _viewTransitionStartedAt;
    private float _renderOpacity = 1f;
    private string _hoveredControl = string.Empty;
    private GUIStyle _titleStyle;
    private GUIStyle _subtitleStyle;
    private GUIStyle _eyebrowStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _bodyStyle;
    private GUIStyle _smallStyle;
    private GUIStyle _fieldLabelStyle;
    private GUIStyle _segmentLabelStyle;
    private GUIStyle _fieldStyle;
    private GUIStyle _textAreaStyle;
    private GUIStyle _buttonLabelStyle;
    private GUIStyle _saveNameStyle;
    private GUIStyle _invisibleButtonStyle;
    private Texture2D _titleLogo;
    private Texture2D _pixel;
    private Texture2D _fieldBackground;
    private Texture2D _fieldFocusedBackground;

    public static void PrepareStartupGate()
    {
        StartupGateActive = true;
        StartupFlowComplete = false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        enabled = false;
    }

    private void OnDestroy()
    {
        if (_open)
            RuntimeModalUiBlocker.Release(_modalToken);
        // note: The additive title stage must never survive destruction of the UI that owns its presentation lifetime.
        YQTitleEnvironmentLoader.Hide();
        if (Instance == this)
            Instance = null;
        if (_pixel != null)
            Destroy(_pixel);
        if (_fieldBackground != null)
            Destroy(_fieldBackground);
        if (_fieldFocusedBackground != null)
            Destroy(_fieldFocusedBackground);
    }

    public void OpenAtStartup()
    {
        PrepareStartupGate();
        RefreshSelectedProfile();
        ResetCharacterDefaults();
        _confirmDeleteProfileId = string.Empty;
        _status = string.Empty;
        _view = HasAnyProfile() ? ViewMode.Saves : ViewMode.CharacterCreation;
        SetOpen(true);
    }

    private void SetOpen(bool value)
    {
        if (_open == value)
            return;

        _open = value;
        enabled = value;
        if (value)
        {
            _closing = false;
            _pendingStartupCompletion = false;
            _openedAt = Time.unscaledTime;
            _viewTransitionStartedAt = _openedAt;
            // note: The menu owns the 3D title stage; gameplay systems remain in the original scene and are never duplicated.
            YQTitleEnvironmentLoader.Show();
            RuntimeModalUiBlocker.Acquire(_modalToken);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            if (StartupFlowComplete &&
                !YourQuestTutorialAutoBootstrap.GameplayRuntimeReady)
            {
                // note: Continue/Create hands the existing shrine camera to the Goddess questionnaire instead of exposing or constructing gameplay.
                YQTitleEnvironmentLoader.HoldForOriginQuestionnaire();
            }
            else
            {
                YQTitleEnvironmentLoader.Hide();
            }
            RuntimeModalUiBlocker.Release(_modalToken);
        }
    }

    private void Update()
    {
        if (!_closing ||
            Time.unscaledTime - _exitStartedAt < ExitTransitionSeconds)
        {
            return;
        }

        // note: The startup gate resolves only after the visible and audible title exit completes, preventing the questionnaire from popping through it.
        if (_pendingStartupCompletion)
            StartupFlowComplete = true;
        _closing = false;
        SetOpen(false);
    }

    private void OnGUI()
    {
        if (!_open)
            return;

        EnsureStyles();
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldGuiColor = GUI.color;
        // note: Scale from the 1280x720 design surface without an upper clamp so 1440p/4K menus keep the same readable screen proportion.
        float scale = Mathf.Max(0.56f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        float intro = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01((Time.unscaledTime - _openedAt) /
                IntroTransitionSeconds));
        float exit = _closing
            ? 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01((Time.unscaledTime - _exitStartedAt) /
                    ExitTransitionSeconds))
            : 1f;
        _renderOpacity = intro * exit;
        GUI.color = new Color(1f, 1f, 1f, _renderOpacity);
        _hoverSeen = false;

        // note: A neutral cinematic grade replaces the former bright data-grid wash and lets the baked shrine remain the title's visual subject.
        DrawRect(new Rect(0f, 0f, width, height), new Color(0.001f, 0.006f, 0.016f, 0.30f));
        DrawRect(new Rect(0f, height * 0.72f, width, height * 0.28f), new Color(0.004f, 0.018f, 0.035f, 0.24f));
        DrawDataStreamBackdrop(width, height);

        float margin = 56f;
        Rect left = new Rect(margin, 54f, 380f, height - 108f);
        Rect right = new Rect(width - 64f - 470f, 48f, 470f, height - 96f);
        if (width < 1060f)
        {
            left = new Rect(margin, 54f, width - margin * 2f, 190f);
            right = new Rect(margin, 260f, width - margin * 2f, height - 318f);
        }

        // note: Separate hero and interaction motion creates a restrained cinematic reveal without moving the authored 3D camera stage.
        left.x -= (1f - intro) * 34f;
        float viewEase = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01((Time.unscaledTime - _viewTransitionStartedAt) /
                ViewTransitionSeconds));
        right.x += (1f - Mathf.Min(intro, viewEase)) * 38f;

        DrawHero(left);
        if (_view == ViewMode.CharacterCreation)
            DrawCharacterCreation(right);
        else
            DrawSaveSelect(right);

        if (Event.current.type == EventType.Repaint && !_hoverSeen)
            _hoveredControl = string.Empty;

        GUI.matrix = oldMatrix;
        GUI.color = oldGuiColor;
    }

    private void DrawHero(Rect rect)
    {
        GUI.Label(new Rect(rect.x + 2f, rect.y, rect.width - 4f, 22f), "A PROCEDURAL ROLE-PLAYING WORLD", _eyebrowStyle);
        if (_titleLogo != null)
        {
            // note: The production wordmark is pre-lit art in the UI layer, keeping it crisp and independent from the title camera's depth effects.
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _renderOpacity);
            GUI.DrawTexture(
                new Rect(rect.x, rect.y + 24f, Mathf.Min(390f, rect.width - 8f), 108f),
                _titleLogo,
                ScaleMode.ScaleToFit,
                true);
            GUI.color = previousColor;
        }
        else
        {
            // note: A clean text fallback keeps startup usable if the optional title art has not imported yet.
            GUI.Label(new Rect(rect.x, rect.y + 24f, rect.width, 86f), "YourQuest", _titleStyle);
        }
        GUI.Label(new Rect(rect.x + 2f, rect.y + 132f, rect.width - 6f, 38f), "The world answers the player first.", _subtitleStyle);

        Rect line = new Rect(rect.x + 2f, rect.y + 174f, Mathf.Min(246f, rect.width - 4f), 1.5f);
        DrawRect(line, new Color(0.72f, 0.90f, 0.98f, 0.68f));

        GUI.Label(new Rect(rect.x + 2f, rect.y + 194f, rect.width - 24f, 84f),
            "Choose an existing journey or offer the goddess the first shape of a new one.",
            _bodyStyle);

        string active = YQProfileSaveSystem.Instance != null && !string.IsNullOrWhiteSpace(YQProfileSaveSystem.Instance.ActiveProfileId)
            ? "Active save ready"
            : "No save selected";
        DrawRect(new Rect(rect.x + 2f, rect.yMax - 38f, 22f, 1f), new Color(0.66f, 0.90f, 0.98f, 0.56f));
        GUI.Label(new Rect(rect.x + 32f, rect.yMax - 52f, rect.width - 40f, 28f), active.ToUpperInvariant(), _eyebrowStyle);
    }

    private void DrawSaveSelect(Rect rect)
    {
        DrawPanel(rect, new Color(0.002f, 0.010f, 0.024f, 0.66f));
        GUI.Label(new Rect(rect.x + 26f, rect.y + 22f, rect.width - 52f, 32f), "Choose Save", _sectionStyle);

        YQProfileSaveSystem system = YQProfileSaveSystem.Instance;
        if (system == null)
        {
            GUI.Label(new Rect(rect.x + 26f, rect.y + 76f, rect.width - 52f, 80f), "Profile system unavailable.", _bodyStyle);
            return;
        }

        Rect listRect = new Rect(rect.x + 24f, rect.y + 72f, rect.width - 48f, rect.height - 186f);
        DrawRect(listRect, new Color(0.005f, 0.055f, 0.12f, 0.54f));

        if (system.Profiles.Count == 0)
        {
            GUI.Label(new Rect(listRect.x + 18f, listRect.y + 18f, listRect.width - 36f, 90f), "No saves yet. Create a character to begin.", _bodyStyle);
        }
        else
        {
            Rect content = new Rect(0f, 0f, listRect.width - 22f, Mathf.Max(listRect.height, system.Profiles.Count * 82f + 8f));
            _saveScroll = GUI.BeginScrollView(listRect, _saveScroll, content, false, true);
            for (int i = 0; i < system.Profiles.Count; i++)
            {
                YQProfileSaveSystem.ProfileEntry entry = system.Profiles[i];
                if (entry == null)
                    continue;
                DrawSaveRow(new Rect(8f, 8f + i * 82f, content.width - 16f, 72f), entry, system.ActiveProfileId);
            }
            GUI.EndScrollView();
        }

        float buttonY = rect.yMax - 78f;
        bool canLoad = !string.IsNullOrWhiteSpace(_selectedProfileId);
        if (DrawButton(new Rect(rect.x + 24f, buttonY, 120f, 48f), "Continue", canLoad))
            ContinueSelected();
        if (DrawButton(new Rect(rect.x + 154f, buttonY, 120f, 48f), "New Journey", true))
        {
            SwitchView(ViewMode.CharacterCreation);
            ResetCharacterDefaults();
            _status = string.Empty;
        }
        if (DrawButton(new Rect(rect.x + 284f, buttonY, 88f, 48f), DeleteLabel(), canLoad))
            DeleteSelected();
        if (DrawButton(new Rect(rect.xMax - 94f, buttonY, 70f, 48f), "Quit", true))
            Quit();

        if (!string.IsNullOrWhiteSpace(_status))
            GUI.Label(new Rect(rect.x + 26f, buttonY - 28f, rect.width - 52f, 22f), _status, _smallStyle);
    }

    private void DrawSaveRow(Rect rect, YQProfileSaveSystem.ProfileEntry entry, string activeProfileId)
    {
        bool selected = string.Equals(entry.profileId, _selectedProfileId, StringComparison.OrdinalIgnoreCase);
        bool active = string.Equals(entry.profileId, activeProfileId, StringComparison.OrdinalIgnoreCase);
        DrawRect(rect, selected ? new Color(0.08f, 0.34f, 0.50f, 0.82f) : new Color(0.025f, 0.15f, 0.25f, 0.66f));
        DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), active ? new Color(0.68f, 0.94f, 1f, 1f) : new Color(0.22f, 0.48f, 0.62f, 0.72f));

        string name = string.IsNullOrWhiteSpace(entry.displayName) ? "Unnamed Save" : entry.displayName;
        GUI.Label(new Rect(rect.x + 18f, rect.y + 10f, rect.width - 36f, 26f), name, _saveNameStyle);
        GUI.Label(new Rect(rect.x + 18f, rect.y + 40f, rect.width - 36f, 22f), "Updated " + FormatUnix(entry.updatedUnix) + (active ? "  |  active" : string.Empty), _smallStyle);

        RegisterHover("save:" + entry.profileId, rect, true);
        if (CanInteract && GUI.Button(rect, GUIContent.none, _invisibleButtonStyle))
        {
            YQTitleEnvironmentLoader.PlayUiConfirm();
            _selectedProfileId = entry.profileId;
            _confirmDeleteProfileId = string.Empty;
            _status = string.Empty;
        }
    }

    private void DrawCharacterCreation(Rect rect)
    {
        DrawPanel(rect, new Color(0.002f, 0.010f, 0.024f, 0.66f));
        GUI.Label(new Rect(rect.x + 28f, rect.y + 22f, rect.width - 56f, 38f), "Begin a New Journey", _sectionStyle);
        GUI.Label(new Rect(rect.x + 28f, rect.y + 62f, rect.width - 56f, 34f), "Give the goddess only the outline. She will author what awakens beneath it.", _smallStyle);

        float customHeight = (_pronounIndex == PronounOptions.Length - 1 ? 44f : 0f) +
            (_bodyFrameIndex == BodyFrameOptions.Length - 1 ? 44f : 0f);
        Rect viewport = new Rect(rect.x + 24f, rect.y + 108f, rect.width - 48f, rect.height - 202f);
        // note: The base form fits exactly without invoking Unity's default scrollbar; custom identity fields add scrollable height only when selected.
        Rect content = new Rect(0f, 0f, viewport.width - (customHeight > 0f ? 18f : 0f), 420f + customHeight);
        _characterScroll = GUI.BeginScrollView(viewport, _characterScroll, content, false, content.height > viewport.height);

        float x = 4f;
        float y = 2f;
        float w = content.width - 8f;

        DrawFieldLabel(x, y, "Name");
        Rect nameRect = new Rect(x, y + 20f, w, 40f);
        _newName = GUI.TextField(nameRect, _newName, 40, _fieldStyle);
        DrawFieldUnderline(nameRect);
        y += 68f;

        DrawFieldLabel(x, y, "Pronouns");
        _pronounIndex = DrawSegmentedSelector(new Rect(x, y + 20f, w, 38f), PronounOptions, _pronounIndex, "pronouns");
        y += 64f;
        if (_pronounIndex == PronounOptions.Length - 1)
        {
            Rect customRect = new Rect(x, y, w, 38f);
            _customPronouns = GUI.TextField(customRect, _customPronouns, 32, _fieldStyle);
            DrawFieldUnderline(customRect);
            y += 44f;
        }

        DrawFieldLabel(x, y, "Body frame");
        _bodyFrameIndex = DrawSegmentedSelector(new Rect(x, y + 20f, w, 38f), BodyFrameOptions, _bodyFrameIndex, "body");
        y += 64f;
        if (_bodyFrameIndex == BodyFrameOptions.Length - 1)
        {
            Rect customRect = new Rect(x, y, w, 38f);
            _customBodyFrame = GUI.TextField(customRect, _customBodyFrame, 40, _fieldStyle);
            DrawFieldUnderline(customRect);
            y += 44f;
        }

        DrawFieldLabel(x, y, "Life direction");
        Rect directionRect = new Rect(x, y + 20f, w, 40f);
        _lifeDirection = GUI.TextField(directionRect, _lifeDirection, 90, _fieldStyle);
        DrawFieldUnderline(directionRect);
        y += 68f;

        DrawFieldLabel(x, y, "Vow or first impulse");
        Rect vowRect = new Rect(x, y + 20f, w, 62f);
        _vow = GUI.TextArea(vowRect, _vow, 220, _textAreaStyle);
        DrawFieldUnderline(vowRect);
        y += 90f;

        DrawFieldLabel(x, y, "Appearance notes");
        Rect appearanceRect = new Rect(x, y + 20f, w, 40f);
        _appearance = GUI.TextField(appearanceRect, _appearance, 160, _fieldStyle);
        DrawFieldUnderline(appearanceRect);
        GUI.EndScrollView();

        float buttonY = rect.yMax - 76f;
        if (DrawButton(new Rect(rect.x + 24f, buttonY, 138f, 48f), "Meet the Goddess", true))
            CreateCharacter();
        if (HasAnyProfile() && DrawButton(new Rect(rect.x + 174f, buttonY, 96f, 48f), "Back", true))
        {
            SwitchView(ViewMode.Saves);
            _status = string.Empty;
        }
        if (DrawButton(new Rect(rect.xMax - 94f, buttonY, 70f, 48f), "Quit", true))
            Quit();

        if (!string.IsNullOrWhiteSpace(_status))
            GUI.Label(new Rect(rect.x + 26f, buttonY - 28f, rect.width - 52f, 22f), _status, _smallStyle);
    }

    private void DrawFieldLabel(float x, float y, string value)
    {
        GUI.Label(new Rect(x, y, 300f, 18f), value.ToUpperInvariant(), _fieldLabelStyle);
    }

    private void DrawFieldUnderline(Rect rect)
    {
        // note: A pearl baseline gives editable fields definition without default Unity beveled chrome.
        DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.54f, 0.80f, 0.90f, 0.34f));
    }

    private int DrawSegmentedSelector(Rect rect, string[] options, int selectedIndex, string controlPrefix)
    {
        if (options == null || options.Length == 0)
            return selectedIndex;

        const float gap = 4f;
        float width = (rect.width - gap * (options.Length - 1)) / options.Length;
        for (int index = 0; index < options.Length; index++)
        {
            Rect item = new Rect(rect.x + index * (width + gap), rect.y, width, rect.height);
            bool selected = index == selectedIndex;
            bool hovered = CanInteract && item.Contains(Event.current.mousePosition);
            DrawRect(item, selected
                ? new Color(0.035f, 0.105f, 0.135f, 0.90f)
                : hovered
                    ? new Color(0.018f, 0.060f, 0.085f, 0.84f)
                    : new Color(0.003f, 0.014f, 0.030f, 0.76f));
            DrawRect(new Rect(item.x, item.yMax - 1f, item.width, selected ? 2f : 1f),
                new Color(0.62f, 0.86f, 0.94f, selected ? 0.78f : 0.28f));
            GUI.Label(item, options[index], _segmentLabelStyle);
            RegisterHover(controlPrefix + ":" + index, item, CanInteract);
            if (CanInteract && GUI.Button(item, GUIContent.none, _invisibleButtonStyle))
            {
                YQTitleEnvironmentLoader.PlayUiConfirm();
                selectedIndex = index;
            }
        }

        return selectedIndex;
    }

    private void ContinueSelected()
    {
        if (string.IsNullOrWhiteSpace(_selectedProfileId))
            return;

        YQProfileSaveSystem system = YQProfileSaveSystem.Instance;
        bool ok = system != null && system.LoadProfile(_selectedProfileId);
        if (!ok)
        {
            _status = "Could not load that save.";
            return;
        }

        CompleteStartupFlow("Loaded save.");
    }

    private void CreateCharacter()
    {
        YQProfileSaveSystem system = YQProfileSaveSystem.Instance;
        if (system == null)
        {
            _status = "Profile system unavailable.";
            return;
        }

        string name = CleanOrFallback(_newName, "New Adventurer");
        string pronouns = ResolvePronouns();
        string bodyFrame = ResolveBodyFrame();
        string lifeDirection = CleanOrFallback(_lifeDirection, "undecided road");
        string vow = CleanOrFallback(_vow, "I will answer what happens to me.");
        string appearance = CleanOrFallback(_appearance, string.Empty);

        string profileId = system.CreateNewProfile(name, pronouns, bodyFrame, lifeDirection, vow, appearance);
        if (string.IsNullOrWhiteSpace(profileId))
        {
            _status = "Could not create save.";
            return;
        }

        _selectedProfileId = profileId;
        CompleteStartupFlow("Created " + name + ".");
    }

    private void DeleteSelected()
    {
        if (string.IsNullOrWhiteSpace(_selectedProfileId))
            return;

        YQProfileSaveSystem system = YQProfileSaveSystem.Instance;
        if (system == null)
            return;

        if (!string.Equals(_confirmDeleteProfileId, _selectedProfileId, StringComparison.OrdinalIgnoreCase))
        {
            _confirmDeleteProfileId = _selectedProfileId;
            _status = "Press Delete again to remove the selected save.";
            return;
        }

        bool ok = system.DeleteProfile(_selectedProfileId);
        _confirmDeleteProfileId = string.Empty;
        _selectedProfileId = string.Empty;
        RefreshSelectedProfile();
        _status = ok ? "Save deleted." : "Delete failed.";
        if (!HasAnyProfile())
            SwitchView(ViewMode.CharacterCreation);
    }

    private void CompleteStartupFlow(string message)
    {
        _status = message;
        StartupGateActive = true;
        _pendingStartupCompletion = true;
        BeginExitTransition();
    }

    private bool CanInteract => !_closing &&
        Time.unscaledTime - _openedAt >= 0.38f;

    private void BeginExitTransition()
    {
        if (_closing)
            return;

        // note: Confirmation audio and glass-panel fade complete before ownership passes to the Goddess questionnaire.
        _closing = true;
        _exitStartedAt = Time.unscaledTime;
    }

    private void SwitchView(ViewMode next)
    {
        if (_view == next)
            return;

        _view = next;
        _viewTransitionStartedAt = Time.unscaledTime;
    }

    private void RefreshSelectedProfile()
    {
        YQProfileSaveSystem system = YQProfileSaveSystem.Instance;
        if (system == null || system.Profiles.Count == 0)
        {
            _selectedProfileId = string.Empty;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_selectedProfileId) && system.FindProfile(_selectedProfileId) != null)
            return;

        if (!string.IsNullOrWhiteSpace(system.ActiveProfileId) && system.FindProfile(system.ActiveProfileId) != null)
        {
            _selectedProfileId = system.ActiveProfileId;
            return;
        }

        _selectedProfileId = system.Profiles[0] != null ? system.Profiles[0].profileId : string.Empty;
    }

    private bool HasAnyProfile()
    {
        return YQProfileSaveSystem.Instance != null && YQProfileSaveSystem.Instance.Profiles.Count > 0;
    }

    private string DeleteLabel()
    {
        return string.Equals(_confirmDeleteProfileId, _selectedProfileId, StringComparison.OrdinalIgnoreCase) ? "Confirm" : "Delete";
    }

    private void ResetCharacterDefaults()
    {
        _characterScroll = Vector2.zero;
        _newName = string.Empty;
        _customPronouns = string.Empty;
        _customBodyFrame = string.Empty;
        _lifeDirection = string.Empty;
        _vow = string.Empty;
        _appearance = string.Empty;
        _pronounIndex = 1;
        _bodyFrameIndex = 0;
    }

    private string ResolvePronouns()
    {
        if (_pronounIndex == PronounOptions.Length - 1)
            return CleanOrFallback(_customPronouns, "they/them");
        return PronounOptions[Mathf.Clamp(_pronounIndex, 0, PronounOptions.Length - 1)];
    }

    private string ResolveBodyFrame()
    {
        if (_bodyFrameIndex == BodyFrameOptions.Length - 1)
            return CleanOrFallback(_customBodyFrame, "custom");
        return BodyFrameOptions[Mathf.Clamp(_bodyFrameIndex, 0, BodyFrameOptions.Length - 1)];
    }

    private static string CleanOrFallback(string value, string fallback)
    {
        string clean = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
    }

    private static string FormatUnix(long unix)
    {
        if (unix <= 0)
            return "unknown";
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime.ToString("MMM d, yyyy h:mm tt");
        }
        catch
        {
            return "unknown";
        }
    }

    private bool DrawButton(Rect rect, string label, bool enabledButton)
    {
        bool interactive = enabledButton && CanInteract;
        bool hovered = interactive && rect.Contains(Event.current.mousePosition);
        RegisterHover("button:" + label, rect, interactive);
        Color color = interactive
            ? hovered
                ? new Color(0.025f, 0.105f, 0.145f, 0.90f)
                : new Color(0.004f, 0.030f, 0.060f, 0.82f)
            : new Color(0.003f, 0.012f, 0.024f, 0.54f);
        DrawRect(rect, color);
        DrawRect(new Rect(rect.x, rect.yMax - (hovered ? 2f : 1f), rect.width, hovered ? 2f : 1f), interactive ? new Color(0.72f, 0.92f, 0.98f, 0.78f) : new Color(0.20f, 0.34f, 0.42f, 0.42f));
        GUI.Label(rect, label, _buttonLabelStyle);
        bool clicked = interactive &&
            GUI.Button(rect, GUIContent.none, _invisibleButtonStyle);
        if (clicked)
            YQTitleEnvironmentLoader.PlayUiConfirm();
        return clicked;
    }

    private void RegisterHover(string controlId, Rect rect, bool interactive)
    {
        if (!interactive || !rect.Contains(Event.current.mousePosition))
            return;

        _hoverSeen = true;
        if (Event.current.type != EventType.Repaint ||
            string.Equals(_hoveredControl, controlId,
                StringComparison.Ordinal))
        {
            return;
        }

        _hoveredControl = controlId;
        YQTitleEnvironmentLoader.PlayUiHover();
    }

    private void DrawPanel(Rect rect, Color color)
    {
        DrawRect(rect, color);
        DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0.72f, 0.92f, 0.98f, 0.52f));
        DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.32f, 0.58f, 0.68f, 0.22f));
        DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), new Color(0.42f, 0.70f, 0.80f, 0.24f));
        // note: One short return rail preserves the data-stream motif without the former debug telemetry ruler.
        DrawRect(new Rect(rect.xMax - 36f, rect.yMax - 1f, 36f, 1f), new Color(0.68f, 0.90f, 0.96f, 0.38f));
    }

    private void DrawDataStreamBackdrop(float width, float height)
    {
        // note: Three slow lower-third traces imply a living data stream without covering the title scene in a debug grid.
        float drift = Mathf.Repeat(Time.unscaledTime * 9f, 180f);
        for (int index = 0; index < 3; index++)
        {
            float y = height - 32f - index * 13f;
            float start = Mathf.Repeat(index * 310f + drift, width + 180f) - 180f;
            DrawRect(new Rect(start, y, 118f + index * 26f, 1f), new Color(0.62f, 0.88f, 0.96f, 0.08f));
        }
    }

    private void DrawRect(Rect rect, Color color)
    {
        Color old = GUI.color;
        color.a *= _renderOpacity;
        GUI.color = color;
        GUI.DrawTexture(rect, _pixel);
        GUI.color = old;
    }

    private void EnsureStyles()
    {
        if (_titleLogo == null)
        {
            // note: Resources provides one stable project-owned binding for the generated wordmark in editor and player builds.
            _titleLogo = Resources.Load<Texture2D>("UI/YQ_YourQuestLogo");
        }

        if (_pixel == null)
        {
            _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        if (_titleStyle != null)
            return;

        _fieldBackground = CreateUiTexture(new Color(0.002f, 0.012f, 0.028f, 0.82f));
        _fieldFocusedBackground = CreateUiTexture(new Color(0.012f, 0.055f, 0.080f, 0.92f));

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 62,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.96f, 0.98f, 1f, 1f) }
        };
        _subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            normal = { textColor = new Color(0.72f, 0.90f, 0.98f, 1f) }
        };
        _eyebrowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.58f, 0.78f, 0.86f, 0.88f) }
        };
        _sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.95f, 0.98f, 1f, 1f) }
        };
        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            wordWrap = true,
            normal = { textColor = new Color(0.86f, 0.94f, 0.99f, 1f) }
        };
        _smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = new Color(0.66f, 0.84f, 0.94f, 1f) }
        };
        _fieldLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.54f, 0.74f, 0.82f, 0.92f) }
        };
        _segmentLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            normal = { textColor = new Color(0.90f, 0.96f, 0.98f, 1f) }
        };
        _saveNameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip,
            normal = { textColor = new Color(0.96f, 0.98f, 1f, 1f) }
        };
        _fieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 16,
            padding = new RectOffset(12, 12, 8, 6),
            normal = { textColor = new Color(0.96f, 0.98f, 1f, 1f), background = _fieldBackground },
            focused = { textColor = Color.white, background = _fieldFocusedBackground }
        };
        _textAreaStyle = new GUIStyle(GUI.skin.textArea)
        {
            fontSize = 16,
            wordWrap = true,
            padding = new RectOffset(12, 12, 8, 8),
            normal = { textColor = new Color(0.96f, 0.98f, 1f, 1f), background = _fieldBackground },
            focused = { textColor = Color.white, background = _fieldFocusedBackground }
        };
        _buttonLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.96f, 0.98f, 1f, 1f) }
        };
        _invisibleButtonStyle = new GUIStyle(GUIStyle.none);
    }

    private static Texture2D CreateUiTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply(false, true);
        return texture;
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
