#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-20000)]
[DisallowMultipleComponent]
public sealed partial class YQDeveloperConsole : MonoBehaviour
{
    public static YQDeveloperConsole Instance { get; private set; }
    public static bool CapturesInput => Instance != null && (Instance._open || Instance._releasePending);
    [SerializeField] private Key toggleKey = Key.Backquote;
    private bool _open, _releasePending, _focus;
    private int _toggleFrame = -1;
    private string _input = "", _search = "", _completionPrefix;
    private string[] _completions = Array.Empty<string>();
    private int _completionIndex, _historyIndex;
    private string _draft = "";
    private Vector2 _scroll;
    private readonly List<string> _lines = new List<string>();
    private readonly List<string> _history = new List<string>();
    private readonly object _modalToken = new object();
    private YQDeveloperCommandRegistry _registry;
    private StreamWriter _audit;
    private GUIStyle _text, _entry, _panel, _header;
    private CursorLockMode _cursorLock;
    private bool _cursorVisible;
    private GameObject _selected;
    private readonly List<InputAction> _suspendedActions = new List<InputAction>();
    public bool IsOpen => _open;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        // note: Explicit isolated asset/traversal reviews must remain isolated; the console installs no gameplay owner.
        if (!YQDeveloperConsoleGate.Enabled || YQHomeTraversalReview.IsIsolatedReview || FindFirstObjectByType<YQDeveloperConsole>() != null) return;
        new GameObject("YourQuest Developer Console").AddComponent<YQDeveloperConsole>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        if (Enum.TryParse(PlayerPrefs.GetString("YourQuest.DeveloperConsole.ToggleKey", "Backquote"), out Key saved) && saved != Key.None && Keyboard.current?[saved] != null) toggleKey = saved;
        _registry = new YQDeveloperCommandRegistry();
        _registry.BindKey = key =>
        {
            toggleKey = key;
            PlayerPrefs.SetString("YourQuest.DeveloperConsole.ToggleKey", key.ToString());
        };
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "DeveloperConsole");
            Directory.CreateDirectory(folder);
            _audit = new StreamWriter(Path.Combine(folder, "commands-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".log"), true) { AutoFlush = true };
        }
        catch (Exception e) { Append("Audit file unavailable: " + e.Message + "; results still recorded in Unity log."); }
        _registry.Audit = WriteAudit;
        // note: Asynchronous generation results join the same searchable output and mutation audit as typed commands.
        _registry.Completed = result => { Append((result.Success ? "OK: " : "ERROR: ") + result.Text); _browserDirty = true; };
        Append("YourQuest developer console | help | Tab completion | Up/Down history | search output");
        Append("Target: player. Edits start a SESSION snapshot and block normal saves. test restore discards changes; test persist --confirm explicitly writes the active profile.");
        InputSystem.onAfterUpdate += ReadToggle;
        Application.quitting += OnQuitting;
    }

    private void ReadToggle()
    {
        // note: Acquire the existing modal barrier before any gameplay Update observes this input frame.
        if (!isActiveAndEnabled || !Application.isPlaying || Keyboard.current == null || _toggleFrame == Time.frameCount) return;
        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            _toggleFrame = Time.frameCount;
            if (_open) SetOpen(false);
            else if (!_releasePending && !RuntimeModalUiBlocker.IsBlocked && !YQTitleScreenUI.StartupPresentationActive) SetOpen(true);
        }
    }
    private void Update()
    {
        // note: Closing keystrokes/mouse clicks remain captured until released, so Escape/Enter/click cannot trigger gameplay on the closing frame.
        if (!_releasePending || (Keyboard.current?.anyKey.isPressed ?? false) || (Mouse.current?.leftButton.isPressed ?? false) || (Mouse.current?.rightButton.isPressed ?? false)) return;
        _releasePending = false;
        RuntimeModalUiBlocker.Release(_modalToken);
        foreach (InputAction action in _suspendedActions) if (action.actionMap?.asset != null) action.Enable();
        _suspendedActions.Clear();
        if (!RuntimeModalUiBlocker.IsBlocked) { Cursor.lockState = _cursorLock; Cursor.visible = _cursorVisible; }
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_selected);
        _selected = null;
    }
    public void SetOpen(bool open)
    {
        if (_open == open) return;
        _open = open;
        if (open)
        {
            // note: Close the non-modal ability picker before it can receive clicks underneath the console.
            if (YourQuestTutorialMenuUI.IsQuickOpenNow) FindFirstObjectByType<YourQuestTutorialMenuUI>()?.ForceCloseFromBootstrap();
            _cursorLock = Cursor.lockState; _cursorVisible = Cursor.visible;
            _selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            RuntimeModalUiBlocker.Acquire(_modalToken);
            // note: Suspend enabled gameplay actions individually, preserving the exact pre-console enabled set; UI actions remain available.
            foreach (PlayerInput playerInput in FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
                if (playerInput.actions != null)
                    foreach (InputActionMap map in playerInput.actions.actionMaps)
                        if (!string.Equals(map.name, "UI", StringComparison.OrdinalIgnoreCase))
                            foreach (InputAction action in map.actions)
                                if (action.enabled) { _suspendedActions.Add(action); action.Disable(); }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            _focus = true;
        }
        else _releasePending = true;
    }
    public YQDeveloperCommandResult Submit(string command)
    {
        Append("> " + command);
        var result = _registry.Execute(command);
        Append((result.Success ? "OK | " : "ERROR | ") + result.Text);
        if (!string.IsNullOrWhiteSpace(command))
        {
            if (_history.Count == 0 || _history[_history.Count - 1] != command) _history.Add(command);
            if (_history.Count > 100) _history.RemoveAt(0);
        }
        _historyIndex = _history.Count; _draft = ""; _input = ""; _completionPrefix = null; _focus = true;
        _browserDirty = true;
        return result;
    }
    private void Append(string line)
    {
        _lines.AddRange(line.Split('\n'));
        if (_lines.Count > 500) _lines.RemoveRange(0, _lines.Count - 500);
        _scroll.y = float.MaxValue;
    }
    private void WriteAudit(string text)
    {
        try { _audit?.WriteLine(text); }
        catch (Exception e) { Append("Audit write failed: " + e.Message); _audit?.Dispose(); _audit = null; }
    }
    private void OnGUI()
    {
        if (!_open) return;
        // note: The opening toggle is a control gesture and must not be inserted into search or the command input.
        if (_toggleFrame == Time.frameCount && (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp)) Event.current.Use();
        if (_text == null)
        {
            _text = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, fontSize = 14, normal = { textColor = new Color(.78f, .9f, 1f) } };
            _entry = new GUIStyle(GUI.skin.textField) { fontSize = 16, richText = false };
            _header = new GUIStyle(_text) { fontSize = 17, fontStyle = FontStyle.Bold };
            _panel = new GUIStyle(GUI.skin.box);
        }
        // note: Browsing gets enough space for names and hover details; the advanced prompt remains available below it.
        GUI.depth = -20000;
        Rect bounds = new Rect(8, 8, Screen.width - 16, Mathf.Max(120, Screen.height - 16));
        GUI.Box(bounds, GUIContent.none, _panel);
        GUILayout.BeginArea(new Rect(bounds.x + 12, bounds.y + 8, bounds.width - 24, bounds.height - 16));
        // note: Small Game views can scroll the whole panel so action controls and the advanced prompt are never clipped away.
        _consoleViewportScroll = GUILayout.BeginScrollView(_consoleViewportScroll);
        GUILayout.BeginHorizontal();
        GUILayout.Label(_registry.Target + (YQDeveloperTestSession.Active ? "  |  SESSION — SAVES BLOCKED" : "  |  INSPECT / SNAPSHOT READY"), _header);
        if (GUILayout.Button("Close [" + toggleKey + "]", GUILayout.Width(145))) SetOpen(false);
        GUILayout.EndHorizontal();
        DrawBrowserToolbar();
        float browserHeight = Mathf.Clamp(bounds.height - (Screen.width < 760 ? 290 : 235), 100, 470);
        if (_layoutShowBrowser) DrawBrowser(browserHeight);
        GUILayout.Label(_layoutShowAdvanced ? "RESULTS / ADVANCED COMMANDS" : "RESULTS", _header);
        GUILayout.BeginHorizontal(); GUILayout.Label("Search output", GUILayout.Width(105));
        _search = GUILayout.TextField(_search, 128, _entry);
        if (GUILayout.Button("Clear", GUILayout.Width(65))) { _lines.Clear(); _search = ""; }
        GUILayout.EndHorizontal();
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Max(70, bounds.height - (_layoutShowBrowser ? browserHeight : 0) - (Screen.width < 760 ? 290 : 235))));
        foreach (string line in _lines) if (_search.Length == 0 || line.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0) GUILayout.Label(line, _text);
        GUILayout.EndScrollView();
        HandleEditingKeys();
        if (_layoutShowAdvanced)
        {
        GUILayout.BeginHorizontal(); GUILayout.Label(">", _header, GUILayout.Width(18));
        GUI.SetNextControlName("YQConsoleCommand");
        string edited = GUILayout.TextField(_input, 1024, _entry);
        if (edited != _input) { _input = edited; _completionPrefix = null; }
        if (GUILayout.Button("Run", GUILayout.Width(65))) Submit(_input);
        GUILayout.EndHorizontal();
        if (_focus && Event.current.type == EventType.Repaint) { GUI.FocusControl("YQConsoleCommand"); _focus = false; }
        GUILayout.Label("Tab: autocomplete   •   Up / Down: history   •   help: all commands and ranges", _text);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
    private void HandleEditingKeys()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        if (e.keyCode == KeyCode.Escape) { SetOpen(false); e.Use(); return; }
        if (!_layoutShowAdvanced || GUI.GetNameOfFocusedControl() != "YQConsoleCommand") return;
        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Submit(_input); e.Use(); }
        else if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow)
        {
            if (_historyIndex == _history.Count) _draft = _input;
            _historyIndex = Mathf.Clamp(_historyIndex + (e.keyCode == KeyCode.UpArrow ? -1 : 1), 0, _history.Count);
            _input = _historyIndex == _history.Count ? _draft : _history[_historyIndex];
            _completionPrefix = null; e.Use(); _focus = true;
        }
        else if (e.keyCode == KeyCode.Tab)
        {
            if (_completionPrefix == null)
            {
                _completionPrefix = _input; _completions = _registry.Complete(_input); _completionIndex = 0;
                if (_completions.Length > 1) Append("Completions: " + string.Join(" | ", _completions));
            }
            if (_completions.Length > 0) _input = _completions[(_completionIndex++) % _completions.Length];
            e.Use(); _focus = true;
        }
    }
    private void OnQuitting()
    {
        // note: Quit is never implicit approval to persist test edits. Keep the save barrier closed even if restoration is unavailable.
        if (YQDeveloperTestSession.Active && !YQDeveloperTestSession.Restore(out string error)) Debug.LogWarning("[Developer Console] Quit restoration unavailable; save barrier remains closed: " + error);
    }
    private void OnApplicationQuit() => OnQuitting();
    private void OnDestroy()
    {
        if (Instance != this) return;
        InputSystem.onAfterUpdate -= ReadToggle; Application.quitting -= OnQuitting;
        RuntimeModalUiBlocker.Release(_modalToken);
        foreach (InputAction action in _suspendedActions) if (action.actionMap?.asset != null) action.Enable();
        _audit?.Dispose(); Instance = null;
    }

    private void OnDisable()
    {
        // note: A disabled console must not leave gameplay actions or a modal token suspended indefinitely.
        if (Instance != this) return;
        _open = false; _releasePending = false;
        RuntimeModalUiBlocker.Release(_modalToken);
        foreach (InputAction action in _suspendedActions) if (action.actionMap?.asset != null) action.Enable();
        _suspendedActions.Clear();
    }
}
#endif
