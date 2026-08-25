// Assets/Assets/Scripts/Tutorial/RuntimeModalUiBlocker.cs
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RuntimeModalUiBlocker : MonoBehaviour
{
    public static RuntimeModalUiBlocker Instance { get; private set; }

    public static bool IsMenuOpen => _menuOpen;
    public static bool IsDialogueOpen => _dialogueOpen;
    public static bool IsBlocked =>
        _manualBlockTokens.Count > 0 ||
        _menuOpen ||
        _dialogueOpen ||
        YQStartupLoadingScreen.IsGenerationVisible ||
        // note: The Goddess world-generation lock is gameplay-modal even after questionnaire UI is hidden.
        YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked;
    public static bool IsAnyModalOpen => IsBlocked;

    private static bool _menuOpen;
    private static bool _dialogueOpen;
    private static readonly HashSet<object> _manualBlockTokens = new HashSet<object>();
    private static readonly object _defaultToken = new object();
    private static float _previousTimeScale = 1f;

    [SerializeField] private bool pauseGameForMenus = true;
    [SerializeField] private bool pauseGameForDialogue = true;
    [SerializeField] private bool pauseGameForManualBlocks = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyPauseState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ResumeHard();
            Instance = null;
        }
    }

    public static void Acquire()
    {
        Acquire(_defaultToken);
    }

    public static void Acquire(object token)
    {
        if (token == null)
            token = _defaultToken;

        _manualBlockTokens.Add(token);
        Instance?.ApplyPauseState();
    }

    public static void Release()
    {
        Release(_defaultToken);
    }

    public static void Release(object token)
    {
        if (token == null)
            token = _defaultToken;

        _manualBlockTokens.Remove(token);
        Instance?.ApplyPauseState();
    }

    public static void SetMenuOpen(bool value)
    {
        if (_menuOpen == value)
            return;

        _menuOpen = value;
        Instance?.ApplyPauseState();
    }

    public static void SetDialogueOpen(bool value)
    {
        if (_dialogueOpen == value)
            return;

        _dialogueOpen = value;
        Instance?.ApplyPauseState();
    }

    public static void ClearAll()
    {
        _menuOpen = false;
        _dialogueOpen = false;
        _manualBlockTokens.Clear();
        Instance?.ApplyPauseState();
    }

    private void ApplyPauseState()
    {
        bool shouldPause =
            (pauseGameForMenus && _menuOpen) ||
            (pauseGameForDialogue && _dialogueOpen) ||
            (pauseGameForManualBlocks && _manualBlockTokens.Count > 0);

        if (shouldPause)
            Pause();
        else
            Unpause();
    }

    private static void Pause()
    {
        if (Time.timeScale > 0f)
            _previousTimeScale = Time.timeScale;

        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void Unpause()
    {
        if (_menuOpen || _dialogueOpen || _manualBlockTokens.Count > 0)
            return;

        Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void ResumeHard()
    {
        _menuOpen = false;
        _dialogueOpen = false;
        _manualBlockTokens.Clear();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
