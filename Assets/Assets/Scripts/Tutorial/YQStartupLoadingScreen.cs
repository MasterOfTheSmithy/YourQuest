using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQStartupLoadingScreen : MonoBehaviour
{
    private static YQStartupLoadingScreen s_instance;

    private const string GenerationTitle =
        "The Goddess Shapes Your World";

    private const string GenerationWaitNote =
        "The Goddess is picky. First generation could take upwards of 10 minutes...";

    private string _title =
        "YourQuest";

    private string _status =
        "Preparing...";

    private string _lastIssue =
        string.Empty;

    private float _progress;

    private int _warnings;

    private int _errors;

    private bool _currentGenerationLineWasGenerated;

    private float _nextGenerationLineSwapTime;

    private const float FallbackGenerationLineMinSeconds =
        0.35f;

    private const float FallbackGenerationLineMaxSeconds =
        0.75f;

    private const float GeneratedGenerationLineMinSeconds =
        1.5f;

    private const float GeneratedGenerationLineMaxSeconds =
        2f;

    private const int MaxGenerationTranscriptLines =
        4;

    private const float GenerationTranscriptBoxHeight =
        156f;

    private const float GenerationDiagnosticsBoxHeight =
        86f;

    [SerializeField]
    [Range(2f, 4f)]
    private float generationTypewriterWordsPerSecond =
        3f;

    private const float AverageTypewriterCharactersPerWord =
        6f;

    private const float TypewriterCommaPauseSeconds =
        0.10f;

    private const float TypewriterSentencePauseSeconds =
        0.24f;

    private const float TypewriterCompletedLineHoldSeconds =
        0.45f;

    private const int MaxRecentIssueLines =
        5;

    private const int MaxRecentDebugLines =
        3;

    private readonly List<string> _generationTranscript =
        new List<string>();

    private string _targetGenerationTranscript =
        string.Empty;

    private string _visibleGenerationTranscript =
        string.Empty;

    private int _visibleGenerationTranscriptCharacters;

    private float _nextGenerationCharacterTime;

    private int _lastGenerationTypewriterFrame =
        -1;

    private Vector2 _generationTranscriptScroll =
        Vector2.zero;

    private readonly HashSet<string> _generationTranscriptKeys =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _generationTranscriptCadenceKeys =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

    private readonly List<DiagnosticLine> _recentIssues =
        new List<DiagnosticLine>();

    private readonly List<DiagnosticLine> _recentDebugLines =
        new List<DiagnosticLine>();

    /*
     * Controls generation-only presentation such as the persistent
     * first-generation duration note.
     */
    private bool _generationMode;
    private bool _generationFailure;
    private Action _retryGeneration;
    private Action _returnToTitle;
    private bool _ordinaryLogStackTracesSuppressed;
    private StackTraceLogType _previousOrdinaryLogStackTraceType;

    private bool _finishingGenerationPresentation;

    private GUIStyle _titleStyle;

    private GUIStyle _statusStyle;

    private GUIStyle _smallStyle;

    private GUIStyle _generationNoteStyle;

    private GUIStyle _percentStyle;

    private GUIStyle _generationDialogueStyle;

    private GUIStyle _generationDialogueShadowStyle;

    private GUIStyle _generationPhaseStyle;

    private GUIStyle _thinkingStyle;

    private GUIStyle _compactDiagnosticsStyle;

    private Texture2D _pixel;

    private Texture2D _questBookTexture;

    private Texture2D _questScrollTexture;

    private struct DiagnosticLine
    {
        public LogType type;

        public string message;
    }

    public bool HasBootIssues =>
        _warnings > 0 ||
        _errors > 0;

    public static bool IsVisible =>
        s_instance != null &&
        s_instance.enabled;

    public static bool IsGenerationVisible =>
        s_instance != null &&
        s_instance.enabled &&
        // note: Generation loading screens still own input even after the world lock begins releasing.
        s_instance._generationMode;

    public static YQStartupLoadingScreen Current =>
        s_instance;

    // ============================================================
    // SHOW
    // ============================================================

    public static YQStartupLoadingScreen Show(
        string title,
        string status)
    {
        EnsureInstance();

        s_instance._generationMode =
            false;

        s_instance.RestoreOrdinaryLogStackTraces();

        s_instance.ClearGenerationTranscript();

        s_instance._title =
            string.IsNullOrWhiteSpace(
                title)
                ? "YourQuest"
                : title;

        s_instance.SetStage(
            status,
            0f);

        s_instance.enabled =
            true;

        return
            s_instance;
    }

    public static YQStartupLoadingScreen ShowGeneration(
        string status,
        float progress = 0f)
    {
        EnsureInstance();

        if (!s_instance._generationMode)
        {
            // note: A new generation presentation starts with a fresh thought transcript.
            s_instance.ClearGenerationTranscript();
        }

        s_instance._generationMode =
            true;

        s_instance.SuppressOrdinaryLogStackTraces();

        // note: Initial generation keeps the baked Goddess stage alive behind the transparent HUD instead of unloading it into a blank screen.
        YQTitleEnvironmentLoader.HoldForWorldGeneration();

        s_instance._title =
            GenerationTitle;

        s_instance.SetStage(
            status,
            progress,
            YQGoddessGenerationDialogue
                .LastSelectionWasGenerated);

        s_instance.enabled =
            true;

        return
            s_instance;
    }

    public static void SetGenerationStage(
        string status,
        float progress)
    {
        if (!YQGeneratedWorldRuntimeBuilder
                .IsInitialGenerationGameplayLocked)
        {
            // note: The full-screen Goddess presentation belongs only to initial world creation; later LLM work must remain non-blocking.
            DismissOrphanedGenerationPresentation();
            return;
        }

        ShowGeneration(
            status,
            progress);
    }

    public static void ShowGenerationFailure(
        string status,
        Action retryGeneration,
        Action returnToTitle)
    {
        YQStartupLoadingScreen screen = ShowGeneration(
            status,
            1f);

        if (screen == null)
            return;

        // note: A watchdog stop remains an interactive terminal state instead of impersonating an endlessly running loading screen.
        screen._generationFailure = true;
        screen._retryGeneration = retryGeneration;
        screen._returnToTitle = returnToTitle;
    }

    public static void ClearGenerationFailure()
    {
        if (s_instance == null)
            return;

        // note: Clear stale recovery callbacks before a fresh deterministic attempt resumes normal progress reporting.
        s_instance._generationFailure = false;
        s_instance._retryGeneration = null;
        s_instance._returnToTitle = null;
    }

    private static void DismissOrphanedGenerationPresentation()
    {
        if (s_instance == null ||
            !s_instance._generationMode ||
            s_instance._finishingGenerationPresentation)
        {
            return;
        }

        // note: Disable immediately before deferred destruction so an orphaned modal cannot consume another frame of input.
        s_instance.enabled =
            false;

        s_instance.gameObject.SetActive(
            false);

        Destroy(
            s_instance.gameObject);
    }

    private static void EnsureInstance()
    {
        if (s_instance != null)
            return;

        GameObject go =
            new GameObject(
                "YQStartupLoadingScreen");

        DontDestroyOnLoad(
            go);

        s_instance =
            go.AddComponent<
                YQStartupLoadingScreen>();
    }

    // ============================================================
    // STAGE
    // ============================================================

    public void SetStage(
        string status,
        float progress)
    {
        SetStage(
            status,
            progress,
            false);
    }

    public void SetStage(
        string status,
        float progress,
        bool generatedLine)
    {
        if (_generationMode &&
            string.IsNullOrWhiteSpace(
                status))
        {
            // note: Exhausted grab bags keep the existing transcript visible instead of falling back to bland filler.
            _progress =
                Mathf.Clamp01(
                    progress);

            return;
        }

        string nextStatus =
            string.IsNullOrWhiteSpace(
                status)
                ? "Preparing..."
                : status;

        _progress =
            Mathf.Clamp01(
                progress);

        if (_generationMode &&
            !string.Equals(
                _status,
                nextStatus,
                StringComparison.Ordinal))
        {
            string transcriptKey =
                NormalizeTranscriptKey(
                    nextStatus);

            if (_generationTranscriptKeys.Contains(
                    transcriptKey))
            {
                // note: Exact visible repeats are suppressed for the whole active generation screen.
                return;
            }

            bool holdCurrentLine =
                Time.unscaledTime <
                    _nextGenerationLineSwapTime ||
                IsGenerationTranscriptTyping();

            if (holdCurrentLine &&
                (_currentGenerationLineWasGenerated ||
                 !generatedLine))
            {
                // note: Progress may continue moving, but readable generated lines are not immediately overwritten by filler.
                return;
            }

            _currentGenerationLineWasGenerated =
                generatedLine;

            _nextGenerationLineSwapTime =
                Time.unscaledTime +
                UnityEngine.Random.Range(
                    generatedLine
                        ? GeneratedGenerationLineMinSeconds
                        : FallbackGenerationLineMinSeconds,
                    generatedLine
                        ? GeneratedGenerationLineMaxSeconds
                        : FallbackGenerationLineMaxSeconds);

            AddGenerationTranscriptLine(
                nextStatus,
                transcriptKey);

            SetGenerationTranscriptTarget(
                BuildGenerationTranscript());

            return;
        }

        if (_generationMode)
        {
            AddGenerationTranscriptLine(
                nextStatus,
                NormalizeTranscriptKey(
                    nextStatus));

            SetGenerationTranscriptTarget(
                BuildGenerationTranscript());

            return;
        }

        _status =
            nextStatus;
    }

    public IEnumerator FinishAndHide()
    {
        SetStage(
            HasBootIssues
                ? "Ready after startup checks"
                : "Ready",
            1f);

        yield return
            new WaitForSecondsRealtime(
                HasBootIssues
                    ? 1.1f
                    : 0.45f);

        Destroy(
            gameObject);
    }

    public IEnumerator FinishGenerationAndHide(
        float revealHoldSeconds = 0f)
    {
        // note: Mark the intentional final reveal so the unlocked-state safety guard does not cut off its closing line.
        _finishingGenerationPresentation =
            true;

        if (revealHoldSeconds > 0f)
        {
            // note: Let the final Goddess line remain readable before showing the handoff text.
            yield return
                new WaitForSecondsRealtime(
                    revealHoldSeconds);
        }

        SetStage(
            "Entering YourQuest...",
            1f);

        yield return
            new WaitForSecondsRealtime(
                0.45f);

        Destroy(
            gameObject);
    }

    // ============================================================
    // UNITY LIFECYCLE
    // ============================================================

    private void OnEnable()
    {
        Application.logMessageReceived -=
            OnLogMessage;

        Application.logMessageReceived +=
            OnLogMessage;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -=
            OnLogMessage;

        RestoreOrdinaryLogStackTraces();
    }

    private void OnDestroy()
    {
        bool wasGenerationPresentation = _generationMode;

        if (s_instance == this)
        {
            s_instance =
                null;
        }

        Application.logMessageReceived -=
            OnLogMessage;

        RestoreOrdinaryLogStackTraces();

        if (wasGenerationPresentation)
            YQTitleEnvironmentLoader.ReleaseWorldGeneration();

        if (_pixel != null)
        {
            Destroy(
                _pixel);

            _pixel =
                null;
        }
    }

    // ============================================================
    // LOGGING
    // ============================================================

    private void SuppressOrdinaryLogStackTraces()
    {
        if (_ordinaryLogStackTracesSuppressed)
            return;

        _previousOrdinaryLogStackTraceType =
            Application.GetStackTraceLogType(LogType.Log);
        Application.SetStackTraceLogType(
            LogType.Log,
            StackTraceLogType.None);
        _ordinaryLogStackTracesSuppressed = true;
        // note: Generation emits useful progress breadcrumbs, but capturing a full call stack for every ordinary line caused avoidable editor-side loading hitches; warnings and errors keep their stacks.
    }

    private void RestoreOrdinaryLogStackTraces()
    {
        if (!_ordinaryLogStackTracesSuppressed)
            return;

        Application.SetStackTraceLogType(
            LogType.Log,
            _previousOrdinaryLogStackTraceType);
        _ordinaryLogStackTracesSuppressed = false;
        // note: The project's original diagnostic policy resumes immediately after the generation presentation releases ownership.
    }

    private void OnLogMessage(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type ==
            LogType.Warning)
        {
            if (ShouldIgnoreStartupOverlayWarning(
                    condition))
            {
                return;
            }

            _warnings++;

            _lastIssue =
                condition;

            AddRecentIssue(
                type,
                condition);
        }
        else if (type ==
                      LogType.Error ||
                 type ==
                      LogType.Exception ||
                 type ==
                     LogType.Assert)
        {
            _errors++;

            _lastIssue =
                condition;

            AddRecentIssue(
                type,
                condition);
        }
        else if (type ==
                 LogType.Log &&
                 ShouldRetainStartupOverlayLog(
                     condition))
        {
            AddRecentDebugLine(
                condition);
        }
    }

    private void AddRecentIssue(
        LogType type,
        string condition)
    {
        if (string.IsNullOrWhiteSpace(
                condition))
        {
            return;
        }

        // note: Store only actionable diagnostics; normal progress logs would drown out the player-facing issue strip.
        _recentIssues.Add(
            new DiagnosticLine
            {
                type =
                    type,

                message =
                    Truncate(
                        CollapseWhitespace(
                            condition),
                        118)
            });

        while (_recentIssues.Count >
               MaxRecentIssueLines)
        {
            _recentIssues.RemoveAt(
                0);
        }
    }

    private void AddRecentDebugLine(
        string condition)
    {
        if (string.IsNullOrWhiteSpace(
                condition))
        {
            return;
        }

        // note: Keep a tiny window of normal startup logs so the debug strip has context without burying warnings.
        _recentDebugLines.Add(
            new DiagnosticLine
            {
                type =
                    LogType.Log,

                message =
                    Truncate(
                        CollapseWhitespace(
                            condition),
                        118)
            });

        while (_recentDebugLines.Count >
               MaxRecentDebugLines)
        {
            _recentDebugLines.RemoveAt(
                0);
        }
    }

    private static bool ShouldIgnoreStartupOverlayWarning(
        string condition)
    {
        if (string.IsNullOrWhiteSpace(
                condition))
        {
            return false;
        }

        // note: These editor/package warnings are visible in Unity Console but are not player-facing boot failures.
        return
            condition.StartsWith(
                "Cannot add menu item",
                StringComparison.Ordinal) ||
            condition.StartsWith(
                "Cannot add validate method",
                StringComparison.Ordinal) ||
            condition.StartsWith(
                "[YQGeneratedWorldEnvironment] Rejected oversized",
                StringComparison.Ordinal) ||
            condition.StartsWith(
                "[YQGeneratedWorldEnvironment] Further oversized wilderness rejection warnings suppressed",
                StringComparison.Ordinal) ||
            condition.StartsWith(
                "[YQWorldGenerationService] World LLM result rejected:",
                StringComparison.Ordinal);
    }

    private bool ShouldRetainStartupOverlayLog(
        string condition)
    {
        if (string.IsNullOrWhiteSpace(
                condition))
        {
            return false;
        }

        // note: During generation, normal logs are useful as breadcrumbs; outside it, keep only project startup lines.
        return
            _generationMode ||
            condition.StartsWith(
                "[YQ",
                StringComparison.Ordinal);
    }

    private void AddGenerationTranscriptLine(
        string line,
        string key)
    {
        if (string.IsNullOrWhiteSpace(
                line) ||
            string.IsNullOrWhiteSpace(
                key))
        {
            return;
        }

        if (!_generationTranscriptKeys.Add(
                key))
        {
            return;
        }

        string cadenceKey =
            BuildTranscriptCadenceKey(
                line);

        if (!string.IsNullOrWhiteSpace(
                cadenceKey) &&
            !_generationTranscriptCadenceKeys.Add(
                cadenceKey))
        {
            // note: Suppress "same sentence, different settlement" so the transcript feels authored instead of templated.
            return;
        }

        string prepared =
            line.Trim();

        // note: The full key set remains unbounded for the run; only transcript history count is trimmed.
        _generationTranscript.Add(
            prepared);

        while (_generationTranscript.Count >
               MaxGenerationTranscriptLines)
        {
            _generationTranscript.RemoveAt(
                0);
        }
    }

    private void ClearGenerationTranscript()
    {
        // note: Transcript memory is presentation-only and resets between loading screens.
        _generationTranscript.Clear();
        _generationTranscriptKeys.Clear();
        _generationTranscriptCadenceKeys.Clear();
        _targetGenerationTranscript =
            string.Empty;
        _visibleGenerationTranscript =
            string.Empty;
        _visibleGenerationTranscriptCharacters =
            0;
        _generationTranscriptScroll =
            Vector2.zero;
        _nextGenerationCharacterTime =
            Time.unscaledTime;

        _lastGenerationTypewriterFrame =
            -1;

        _currentGenerationLineWasGenerated =
            false;

        _nextGenerationLineSwapTime =
            0f;
    }

    private void SetGenerationTranscriptTarget(
        string transcript)
    {
        transcript =
            transcript ?? string.Empty;

        if (string.Equals(
                _targetGenerationTranscript,
                transcript,
                StringComparison.Ordinal))
        {
            return;
        }

        // note: Previous transcript lines remain visible while only the newest Goddess thought types character by character.
        int newestLineStart =
            transcript.LastIndexOf('\n') +
            1;

        _visibleGenerationTranscriptCharacters =
            Mathf.Min(
                transcript.Length,
                newestLineStart +
                (transcript.Length - newestLineStart >= 2
                    ? 2
                    : 0));

        _targetGenerationTranscript =
            transcript;

        _visibleGenerationTranscript =
            _targetGenerationTranscript.Substring(
                0,
                _visibleGenerationTranscriptCharacters);

        _nextGenerationCharacterTime =
            Time.unscaledTime;

        _lastGenerationTypewriterFrame =
            -1;
    }

    private void UpdateGenerationTypewriter()
    {
        if (_generationMode &&
            !IsGenerationTranscriptTyping() &&
            Time.unscaledTime >= _nextGenerationLineSwapTime &&
            YQGoddessGenerationDialogue.TryTakeBufferedLine(
                out string bufferedLine))
        {
            // note: Consume model-authored thoughts at readable cadence while the following LLM request owns the queue.
            SetStage(
                bufferedLine,
                _progress,
                true);
        }

        if (!_generationMode ||
            string.IsNullOrEmpty(
                _targetGenerationTranscript))
        {
            return;
        }

        if (_visibleGenerationTranscriptCharacters >=
            _targetGenerationTranscript.Length)
        {
            _visibleGenerationTranscript =
                _targetGenerationTranscript;

            return;
        }

        float now =
            Time.unscaledTime;

        if (Time.frameCount ==
                _lastGenerationTypewriterFrame ||
            now <
                _nextGenerationCharacterTime)
        {
            // note: OnGUI may run several times per frame; one rendered frame may reveal at most one character.
            return;
        }

        _visibleGenerationTranscriptCharacters =
            Mathf.Min(
                _targetGenerationTranscript.Length,
                _visibleGenerationTranscriptCharacters +
                1);

        _visibleGenerationTranscript =
            _targetGenerationTranscript.Substring(
                0,
                _visibleGenerationTranscriptCharacters);

        _lastGenerationTypewriterFrame =
            Time.frameCount;

        char revealed =
            _targetGenerationTranscript[
                _visibleGenerationTranscriptCharacters -
                1];

        float charactersPerSecond =
            Mathf.Clamp(
                generationTypewriterWordsPerSecond,
                2f,
                4f) *
            AverageTypewriterCharactersPerWord;

        float punctuationPause =
            revealed == '.' ||
            revealed == '!' ||
            revealed == '?'
                ? TypewriterSentencePauseSeconds
                : revealed == ',' ||
                  revealed == ';' ||
                  revealed == ':' ||
                  revealed == '\n'
                    ? TypewriterCommaPauseSeconds
                    : 0f;

        // note: A fixed words-per-second rail plus punctuation pauses keeps the Goddess readable without frame-rate-dependent bursts.
        _nextGenerationCharacterTime =
            now +
            1f /
            Mathf.Max(
                1f,
                charactersPerSecond) +
            punctuationPause;

        if (!IsGenerationTranscriptTyping())
        {
            _nextGenerationLineSwapTime =
                Mathf.Max(
                    _nextGenerationLineSwapTime,
                    now +
                    TypewriterCompletedLineHoldSeconds);
        }
    }

    private bool IsGenerationTranscriptTyping()
    {
        return
            _generationMode &&
            !string.IsNullOrEmpty(
                _targetGenerationTranscript) &&
            _visibleGenerationTranscriptCharacters <
                _targetGenerationTranscript.Length;
    }

    private string BuildGenerationTranscript()
    {
        if (_generationTranscript.Count == 0)
        {
            return
                _status;
        }

        StringBuilder builder =
            new StringBuilder();

        for (int i = 0;
             i < _generationTranscript.Count;
             i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            // note: A transcript reads as active thoughts, with the newest line visually marked at the bottom.
            builder.Append(
                i ==
                _generationTranscript.Count - 1
                    ? "> "
                    : "  ");

            builder.Append(
                _generationTranscript[i]);
        }

        return
            builder.ToString();
    }

    private static string NormalizeTranscriptKey(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        return
            value
                .Trim()
                .Replace(
                    "\r",
                    " ")
                .Replace(
                    "\n",
                    " ")
                .ToLowerInvariant();
    }

    private static string BuildTranscriptCadenceKey(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        if (ContainsCensorGlyphs(
                value))
        {
            // note: Censored Goddess lines carry unique block noise, so their repeated prefix should not trip cadence suppression.
            return string.Empty;
        }

        string normalized =
            NormalizeTranscriptKey(
                value);

        if (normalized.Contains(
                " is next:"))
        {
            return
                "is_next_colon";
        }

        if (normalized.Contains(
                " has trade around"))
        {
            return
                "has_trade_around";
        }

        if (normalized.Contains(
                " lists "))
        {
            return
                "lists_services";
        }

        if (normalized.Contains(
                " waits in "))
        {
            return
                "waits_in_region";
        }

        if (normalized.StartsWith(
                "next settlement",
                StringComparison.Ordinal) ||
            normalized.StartsWith(
                "next hostile",
                StringComparison.Ordinal))
        {
            return
                normalized.Substring(
                    0,
                    Mathf.Min(
                        normalized.Length,
                        28));
        }

        string[] words =
            normalized.Split(
                new[]
                {
                    ' ',
                    ',',
                    '.',
                    ';',
                    ':',
                    '-'
                },
                StringSplitOptions.RemoveEmptyEntries);

        if (words.Length < 5)
        {
            return string.Empty;
        }

        // note: The first two tokens are often a generated place/name; compare the sentence engine after that.
        return
            words[2] +
            "|" +
            words[3] +
            "|" +
            words[4];
    }

    private static bool ContainsCensorGlyphs(
        string value)
    {
        if (string.IsNullOrEmpty(
                value))
        {
            return false;
        }

        // note: Treat all Goddess corruption glyphs as intentional uniqueness marks in the active transcript.
        return
            value.IndexOf(
                '\u2588') >=
            0 ||
            value.IndexOf(
                '\u2593') >=
            0 ||
            value.IndexOf(
                '\u2592') >=
            0 ||
            value.IndexOf(
                '\u2591') >=
            0 ||
            value.IndexOf(
                '\u25A0') >=
            0 ||
            value.IndexOf(
                '\u25A1') >=
            0 ||
            value.IndexOf(
                '\u25CA') >=
            0 ||
            value.IndexOf(
                '\u2205') >=
            0;
    }

    // ============================================================
    // GUI
    // ============================================================

    private void OnGUI()
    {
        if (_generationMode &&
            !YQGeneratedWorldRuntimeBuilder
                .IsInitialGenerationGameplayLocked &&
            !_finishingGenerationPresentation)
        {
            // note: This catches a stale modal restored across script reloads even when no later system reports another generation stage.
            DismissOrphanedGenerationPresentation();
            return;
        }

        EnsureStyles();
        UpdateGenerationTypewriter();

        if (_generationMode)
        {
            DrawGenerationHud();
            return;
        }

        Rect screen =
            new Rect(
                0f,
                0f,
                Screen.width,
                Screen.height);

        DrawRect(
            screen,
            new Color(
                0.010f,
                0.030f,
                0.070f,
                0.98f));

        /*
         * Wider than the old 620px panel because Goddess dialogue now
         * intentionally contains longer, conversational sentences.
         */
        float availableWidth =
            Mathf.Max(
                320f,
                Screen.width -
                64f);

        float panelWidth =
            Mathf.Min(
                760f,
                availableWidth);

        float contentWidth =
            Mathf.Max(
                240f,
                panelWidth -
                60f);

        /*
         * Calculate actual text heights instead of assuming every
         * Goddess message fits inside a 30px rectangle.
         */
        float titleHeight =
            Mathf.Max(
                52f,
                _titleStyle.CalcHeight(
                    new GUIContent(
                        _title),
                    contentWidth));

        string statusDisplay =
            _generationMode
                ? string.IsNullOrWhiteSpace(
                    _visibleGenerationTranscript)
                    ? _status
                    : _visibleGenerationTranscript
                : _status;

        if (_generationMode &&
            statusDisplay.StartsWith(
                "Securing connection",
                StringComparison.Ordinal))
        {
            // note: This spinner is neutral connection UI and never enters the Goddess transcript history.
            string[] frames = { "|", "/", "-", "\\" };
            int frame = Mathf.FloorToInt(Time.unscaledTime * 6f) % frames.Length;
            statusDisplay = "Securing connection... " + frames[frame];
        }

        float statusHeight =
            _generationMode
                ? GenerationTranscriptBoxHeight
                : Mathf.Max(
                    58f,
                    _statusStyle.CalcHeight(
                        new GUIContent(
                            statusDisplay),
                        contentWidth));

        float noteHeight =
            0f;

        if (_generationMode)
        {
            noteHeight =
                Mathf.Max(
                    28f,
                    _generationNoteStyle
                        .CalcHeight(
                            new GUIContent(
                                GenerationWaitNote),
                            contentWidth));
        }

        string diagnostics =
            BuildDiagnosticsText();

        float diagnosticsHeight =
            _generationMode
                ? GenerationDiagnosticsBoxHeight
                : Mathf.Max(
                    22f,
                    _smallStyle.CalcHeight(
                        new GUIContent(
                            diagnostics),
                        contentWidth));

        /*
         * Vertical layout.
         *
         * Nothing uses a hardcoded 30px dialogue slot anymore.
         */
        const float topPadding =
            24f;

        const float bottomPadding =
            22f;

        const float titleToStatusGap =
            8f;

        const float statusToBarGap =
            17f;

        const float barHeight =
            14f;

        const float barToNoteGap =
            14f;

        const float noteToChecksGap =
            12f;

        float panelHeight =
            topPadding +
            titleHeight +
            titleToStatusGap +
            statusHeight +
            statusToBarGap +
            barHeight;

        if (_generationMode)
        {
            panelHeight +=
                barToNoteGap +
                noteHeight +
                noteToChecksGap;
        }
        else
        {
            panelHeight +=
                15f;
        }

        panelHeight +=
            diagnosticsHeight;

        panelHeight +=
            bottomPadding;

        /*
         * Keep the panel inside the screen on unusual resolutions.
         */
        float maximumPanelHeight =
            Mathf.Max(
                240f,
                Screen.height -
                40f);

        panelHeight =
            Mathf.Min(
                panelHeight,
                maximumPanelHeight);

        Rect panel =
            new Rect(
                (Screen.width -
                 panelWidth) *
                0.5f,

                (Screen.height -
                 panelHeight) *
                0.5f,

                panelWidth,
                panelHeight);

        DrawRect(
            panel,
            new Color(
                0.030f,
                0.095f,
                0.155f,
                0.92f));

        if (_generationMode)
        {
            DrawImportedGenerationArt(
                panel);
        }

        /*
         * Neon sky creation-line accent.
         */
        DrawRect(
            new Rect(
                panel.x,
                panel.y,
                panel.width,
                2f),
            new Color(
                0.40f,
                0.90f,
                1f,
                1f));

        float x =
            panel.x +
            30f;

        float y =
            panel.y +
            topPadding;

        GUI.Label(
            new Rect(
                x,
                y,
                contentWidth,
                titleHeight),
            _title,
            _titleStyle);

        y +=
            titleHeight +
            titleToStatusGap;

        /*
         * Main Goddess transcript.
         *
         * Fixed height prevents the loading UI from resizing while the
         * typewriter text is still arriving.
         */
        Rect transcriptRect =
            new Rect(
                x,
                y,
                contentWidth,
                statusHeight);

        if (_generationMode)
        {
            DrawRect(
                transcriptRect,
                new Color(
                    0.015f,
                    0.065f,
                    0.115f,
                    0.84f));

            DrawOutline(
                transcriptRect,
                new Color(
                    0.38f,
                    0.86f,
                    1f,
                    0.58f));
        }

        if (_generationMode)
        {
            DrawGenerationTranscript(
                transcriptRect,
                statusDisplay);
        }
        else
        {
            GUI.Label(
                transcriptRect,
                statusDisplay,
                _statusStyle);
        }

        y +=
            statusHeight +
            statusToBarGap;

        Rect barBack =
            new Rect(
                x,
                y,
                contentWidth,
                barHeight);

        DrawRect(
            barBack,
            new Color(
                0.030f,
                0.075f,
                0.115f,
                1f));

        DrawRect(
            new Rect(
                barBack.x,
                barBack.y,
                barBack.width *
                _progress,
                barBack.height),
            new Color(
                0.44f,
                0.88f,
                1f,
                1f));

        string percentText =
            Mathf.RoundToInt(
                Mathf.Clamp01(
                    _progress) *
                100f)
            .ToString() +
            "%";

        // note: The percent label makes long generation visibly alive instead of feeling frozen.
        GUI.Label(
            barBack,
            percentText,
            _percentStyle);

        y +=
            barHeight;

        /*
         * Generation-only explanatory note.
         *
         * It is deliberately separate from the Goddess dialogue so
         * SetGenerationStage() cannot overwrite it.
         */
        if (_generationMode)
        {
            y +=
                barToNoteGap;

            GUI.Label(
                new Rect(
                    x,
                    y,
                    contentWidth,
                    noteHeight),
                GenerationWaitNote,
                _generationNoteStyle);

            y +=
                noteHeight +
                noteToChecksGap;
        }
        else
        {
            y +=
                15f;
        }

        Rect diagnosticsRect =
            new Rect(
                x,
                y,
                contentWidth,
                diagnosticsHeight);

        if (_generationMode)
        {
            DrawRect(
                diagnosticsRect,
                new Color(
                    0.010f,
                    0.045f,
                    0.085f,
                    0.76f));

            DrawOutline(
                diagnosticsRect,
                new Color(
                    0.24f,
                    0.72f,
                    1f,
                    0.55f));
        }

        GUI.Label(
            diagnosticsRect,
            diagnostics,
            _smallStyle);
    }

    private void DrawGenerationHud()
    {
        float margin = Mathf.Clamp(Screen.width * 0.026f, 22f, 46f);
        float dialogueWidth = Mathf.Clamp(
            Screen.width * 0.48f,
            420f,
            760f);
        float dialogueHeight = Mathf.Clamp(
            Screen.height * 0.29f,
            150f,
            250f);
        string dialogue = string.IsNullOrWhiteSpace(
                _visibleGenerationTranscript)
            ? _status
            : _visibleGenerationTranscript;

        if (dialogue.StartsWith(
                "Securing connection",
                StringComparison.Ordinal))
        {
            string[] frames = { ".", "..", "..." };
            int frame = Mathf.FloorToInt(Time.unscaledTime * 2.4f) %
                frames.Length;
            dialogue = "Securing connection" + frames[frame];
        }

        Rect dialogueRect = new Rect(
            margin,
            margin,
            dialogueWidth,
            dialogueHeight);
        GUI.Label(
            new Rect(
                dialogueRect.x + 2f,
                dialogueRect.y + 2f,
                dialogueRect.width,
                dialogueRect.height),
            dialogue,
            _generationDialogueShadowStyle);
        // note: Goddess prose is drawn directly over the cinematic scene; there is deliberately no panel, border, or scroll-box chrome around it.
        GUI.Label(
            dialogueRect,
            dialogue,
            _generationDialogueStyle);

        DrawLlmThinkingIndicator(margin);

        float progressWidth = Mathf.Clamp(
            Screen.width * 0.30f,
            280f,
            470f);
        const float progressHeight = 7f;
        Rect progressRect = new Rect(
            margin,
            Screen.height - margin - 34f,
            progressWidth,
            progressHeight);
        GUI.Label(
            new Rect(
                progressRect.x,
                progressRect.y - 25f,
                progressRect.width,
                20f),
            "WORLD FORMATION  /  " +
            Mathf.RoundToInt(Mathf.Clamp01(_progress) * 100f) + "%",
            _generationPhaseStyle);
        DrawRect(
            progressRect,
            new Color(0.01f, 0.04f, 0.07f, 0.72f));
        DrawRect(
            new Rect(
                progressRect.x,
                progressRect.y,
                progressRect.width * Mathf.Clamp01(_progress),
                progressRect.height),
            new Color(0.72f, 0.96f, 1f, 0.96f));
        DrawRect(
            new Rect(
                progressRect.x,
                progressRect.y + progressRect.height + 8f,
                Mathf.Min(progressRect.width, 260f),
                1f),
            new Color(0.46f, 0.84f, 1f, 0.38f));

        string diagnostics = BuildDiagnosticsText();
        float diagnosticWidth = Mathf.Clamp(
            Screen.width * 0.235f,
            250f,
            350f);
        const float diagnosticHeight = 76f;
        Rect diagnosticsRect = new Rect(
            Screen.width - margin - diagnosticWidth,
            Screen.height - margin - diagnosticHeight,
            diagnosticWidth,
            diagnosticHeight);
        DrawRect(
            diagnosticsRect,
            new Color(0.005f, 0.025f, 0.045f, 0.58f));
        DrawOutline(
            diagnosticsRect,
            new Color(0.42f, 0.86f, 1f, 0.30f));
        GUI.Label(
            new Rect(
                diagnosticsRect.x + 9f,
                diagnosticsRect.y + 6f,
                diagnosticsRect.width - 18f,
                diagnosticsRect.height - 12f),
            diagnostics,
            _compactDiagnosticsStyle);

        if (_generationFailure)
            DrawGenerationFailureActions(margin);
    }

    private void DrawGenerationFailureActions(float margin)
    {
        const float buttonWidth = 170f;
        const float buttonHeight = 38f;
        const float buttonGap = 12f;
        float totalWidth = buttonWidth * 2f + buttonGap;
        float x = (Screen.width - totalWidth) * 0.5f;
        float y = Screen.height - margin - buttonHeight;

        // note: Recovery controls prove the presentation is responsive and let the player choose a clean retry or a safe return instead of waiting forever.
        if (GUI.Button(
                new Rect(x, y, buttonWidth, buttonHeight),
                "Retry generation"))
        {
            Action retry = _retryGeneration;
            ClearGenerationFailure();
            retry?.Invoke();
            return;
        }

        if (GUI.Button(
                new Rect(
                    x + buttonWidth + buttonGap,
                    y,
                    buttonWidth,
                    buttonHeight),
                "Return to title"))
        {
            Action returnToTitle = _returnToTitle;
            ClearGenerationFailure();
            returnToTitle?.Invoke();
        }
    }

    private void DrawLlmThinkingIndicator(float margin)
    {
        LLMClient client = LLMClient.Instance;
        bool thinking = client != null && client.IsBusy;
        float pulse = thinking
            ? 0.62f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.24f
            : 0.28f;
        string suffix = thinking
            ? new string(
                '.',
                1 + Mathf.FloorToInt(Time.unscaledTime * 2.2f) % 3)
            : string.Empty;
        string label = thinking
            ? "LOCAL MODEL  /  THINKING" + suffix
            : "LOCAL MODEL  /  STANDBY";
        Vector2 labelSize = _thinkingStyle.CalcSize(new GUIContent(label));
        Rect labelRect = new Rect(
            Screen.width - margin - labelSize.x,
            margin,
            labelSize.x,
            24f);
        Rect signalRect = new Rect(
            labelRect.x - 18f,
            labelRect.y + 7f,
            8f,
            8f);

        // note: This indicator reads the real local request queue, so it distinguishes model inference from deterministic world assembly.
        DrawRect(
            signalRect,
            thinking
                ? new Color(0.72f, 0.96f, 1f, pulse)
                : new Color(0.42f, 0.62f, 0.72f, pulse));
        GUI.Label(labelRect, label, _thinkingStyle);
    }

    private void DrawImportedGenerationArt(
        Rect panel)
    {
        if (_questBookTexture != null)
        {
            Rect bookRect =
                new Rect(
                    panel.xMax -
                    142f,
                    panel.y +
                    18f,
                    108f,
                    108f);

            // note: This uses curated imported 2D UI art as a faint creation-table watermark behind the loading text.
            DrawTintedTexture(
                bookRect,
                _questBookTexture,
                new Color(
                    0.62f,
                    0.92f,
                    1f,
                    0.16f));
        }

        if (_questScrollTexture != null)
        {
            Rect scrollRect =
                new Rect(
                    panel.x +
                    18f,
                    panel.yMax -
                    92f,
                    68f,
                    68f);

            // note: A second imported mark makes the diagnostics strip feel like part of the world UI, not raw console spill.
            DrawTintedTexture(
                scrollRect,
                _questScrollTexture,
                new Color(
                    0.78f,
                    0.96f,
                    1f,
                    0.12f));
        }
    }

    private void DrawGenerationTranscript(
        Rect transcriptRect,
        string statusDisplay)
    {
        Rect viewport =
            new Rect(
                transcriptRect.x +
                10f,
                transcriptRect.y +
                8f,
                Mathf.Max(
                    1f,
                    transcriptRect.width -
                    20f),
                Mathf.Max(
                    1f,
                    transcriptRect.height -
                    16f));

        float contentWidth =
            Mathf.Max(
                1f,
                viewport.width -
                10f);

        float contentHeight =
            Mathf.Max(
                viewport.height,
                _statusStyle.CalcHeight(
                    new GUIContent(
                        statusDisplay),
                    contentWidth) +
                6f);

        _generationTranscriptScroll.y =
            Mathf.Max(
                0f,
                contentHeight -
                viewport.height);

        // note: The visible transcript box stays fixed while the inner thought-stream scrolls to the newest typed text.
        _generationTranscriptScroll =
            GUI.BeginScrollView(
                viewport,
                _generationTranscriptScroll,
                new Rect(
                    0f,
                    0f,
                    contentWidth,
                    contentHeight),
                false,
                false);

        GUI.Label(
            new Rect(
                0f,
                0f,
                contentWidth,
                contentHeight),
            statusDisplay,
            _statusStyle);

        GUI.EndScrollView();
    }

    // ============================================================
    // STYLES
    // ============================================================

    private void EnsureStyles()
    {
        if (_pixel == null)
        {
            _pixel =
                new Texture2D(
                    1,
                    1,
                    TextureFormat.RGBA32,
                    false);

            _pixel.SetPixel(
                0,
                0,
                Color.white);

            _pixel.Apply();
        }

        LoadGenerationArtIfNeeded();

        if (_titleStyle != null)
            return;

        _titleStyle =
            new GUIStyle(
                GUI.skin.label)
            {
                fontSize =
                    34,

                fontStyle =
                    FontStyle.Bold,

                alignment =
                    TextAnchor.MiddleLeft,

                wordWrap =
                    true,

                clipping =
                    TextClipping.Overflow,

                stretchHeight =
                    true,

                padding =
                    new RectOffset(
                        0,
                        0,
                        5,
                        7)
            };

        _titleStyle.normal.textColor =
            new Color(
                0.90f,
                0.985f,
                1f,
                1f);

        _statusStyle =
            new GUIStyle(
                GUI.skin.label)
            {
                fontSize =
                    19,

                fontStyle =
                    FontStyle.Normal,

                alignment =
                    TextAnchor.UpperLeft,

                wordWrap =
                    true,

                // note: The transcript box is fixed-size during generation, so long lines must clip instead of resizing the UI.
                clipping =
                    TextClipping.Clip,

                stretchHeight =
                    true,

                /*
                 * Extra vertical padding is intentional.
                 *
                 * This prevents top/bottom glyph clipping on letters
                 * with tall ascenders or low descenders.
                 */
                padding =
                    new RectOffset(
                        0,
                        0,
                        7,
                        9)
            };

        _statusStyle.normal.textColor =
            new Color(
                0.82f,
                0.95f,
                1f,
                1f);

        _generationNoteStyle =
            new GUIStyle(
                GUI.skin.label)
            {
                fontSize =
                    14,

                fontStyle =
                    FontStyle.Italic,

                alignment =
                    TextAnchor.MiddleCenter,

                wordWrap =
                    true,

                clipping =
                    TextClipping.Overflow,

                stretchHeight =
                    true,

                padding =
                    new RectOffset(
                        4,
                        4,
                        3,
                        5)
            };

        _generationNoteStyle.normal.textColor =
            new Color(
                0.64f,
                0.88f,
                1f,
                0.92f);

        _smallStyle =
            new GUIStyle(
                GUI.skin.label)
            {
                fontSize =
                    13,

                alignment =
                    TextAnchor.UpperLeft,

                wordWrap =
                    true,

                // note: Diagnostics are deliberately capped to a strip under the transcript.
                clipping =
                    TextClipping.Clip,

                stretchHeight =
                    true,

                padding =
                    new RectOffset(
                        0,
                        0,
                        2,
                        3)
            };

        _smallStyle.normal.textColor =
            new Color(
                0.58f,
                0.80f,
                0.92f,
                1f);

        _percentStyle =
            new GUIStyle(
                GUI.skin.label)
            {
                fontSize =
                    13,

                fontStyle =
                    FontStyle.Bold,

                alignment =
                    TextAnchor.MiddleCenter,

                wordWrap =
                    false,

                clipping =
                    TextClipping.Clip
            };

        _percentStyle.normal.textColor =
            new Color(
                0.94f,
                0.99f,
                1f,
                1f);

        _generationDialogueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            clipping = TextClipping.Clip,
            padding = new RectOffset(0, 0, 0, 0)
        };
        _generationDialogueStyle.normal.textColor =
            new Color(0.91f, 0.98f, 1f, 0.98f);

        _generationDialogueShadowStyle =
            new GUIStyle(_generationDialogueStyle);
        _generationDialogueShadowStyle.normal.textColor =
            new Color(0f, 0.015f, 0.03f, 0.82f);

        _generationPhaseStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        _generationPhaseStyle.normal.textColor =
            new Color(0.76f, 0.94f, 1f, 0.94f);

        _thinkingStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        _thinkingStyle.normal.textColor =
            new Color(0.80f, 0.96f, 1f, 0.94f);

        _compactDiagnosticsStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 9,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            clipping = TextClipping.Clip,
            padding = new RectOffset(0, 0, 0, 0)
        };
        _compactDiagnosticsStyle.normal.textColor =
            new Color(0.62f, 0.80f, 0.88f, 0.90f);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private string BuildDiagnosticsText()
    {
        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            "Diagnostics: ");

        if (_errors > 0)
        {
            builder.Append(
                _errors);
            builder.Append(
                " error");
            builder.Append(
                _errors == 1
                    ? string.Empty
                    : "s");
            builder.Append(
                ", ");
        }

        builder.Append(
            _warnings);
        builder.Append(
            " warning");
        builder.Append(
            _warnings == 1
                ? string.Empty
                : "s");

        if (_recentIssues.Count == 0)
        {
            if (_recentDebugLines.Count == 0)
            {
                builder.Append(
                    _warnings == 0 &&
                    _errors == 0
                        ? (_generationMode
                            ? " | nominal"
                            : " | clean enough to stop squinting")
                        : " | no retained details");

                return
                    builder.ToString();
            }
        }

        int first =
            Mathf.Max(
                0,
                _recentIssues.Count -
                (_generationMode
                    ? 1
                    : MaxRecentIssueLines));

        for (int i = first;
             i < _recentIssues.Count;
             i++)
        {
            DiagnosticLine issue =
                _recentIssues[i];

            builder.AppendLine();

            // note: Compact labels make warnings/errors scannable without turning the loading screen into the Unity Console.
            builder.Append(
                issue.type ==
                LogType.Warning
                    ? "warn  | "
                    : "error | ");

            builder.Append(
                issue.message);
        }

        int debugStart =
            Mathf.Max(
                0,
                _recentDebugLines.Count -
                (_generationMode
                    ? 1
                    : MaxRecentDebugLines));

        for (int i = debugStart;
             i < _recentDebugLines.Count;
             i++)
        {
            DiagnosticLine line =
                _recentDebugLines[i];

            builder.AppendLine();

            // note: Normal startup breadcrumbs are labeled separately from warnings/errors so the strip stays readable.
            builder.Append(
                "debug | ");

            builder.Append(
                line.message);
        }

        return
            builder.ToString();
    }

    private void DrawRect(
        Rect rect,
        Color color)
    {
        Color previous =
            GUI.color;

        GUI.color =
            color;

        GUI.DrawTexture(
            rect,
            _pixel);

        GUI.color =
            previous;
    }

    private void DrawOutline(
        Rect rect,
        Color color)
    {
        // note: Four one-pixel fills are cheaper and safer than introducing a new texture or UI prefab during startup.
        DrawRect(
            new Rect(
                rect.x,
                rect.y,
                rect.width,
                1f),
            color);

        DrawRect(
            new Rect(
                rect.x,
                rect.yMax - 1f,
                rect.width,
                1f),
            color);

        DrawRect(
            new Rect(
                rect.x,
                rect.y,
                1f,
                rect.height),
            color);

        DrawRect(
            new Rect(
                rect.xMax - 1f,
                rect.y,
                1f,
                rect.height),
            color);
    }

    private void DrawTintedTexture(
        Rect rect,
        Texture texture,
        Color color)
    {
        if (texture == null)
            return;

        Color previous =
            GUI.color;

        GUI.color =
            color;

        GUI.DrawTexture(
            rect,
            texture,
            ScaleMode.ScaleToFit,
            true);

        GUI.color =
            previous;
    }

    private static string Truncate(
        string value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(
                value) ||
            value.Length <=
                maxLength)
        {
            return
                value ??
                string.Empty;
        }

        return
            value.Substring(
                0,
                Mathf.Max(
                    0,
                    maxLength -
                    3)) +
            "...";
    }

    private void LoadGenerationArtIfNeeded()
    {
        if (_questBookTexture != null &&
            _questScrollTexture != null)
        {
            return;
        }

        YQRuntime2DArtRegistry registry =
            YQRuntime2DArtRegistry.Load();

        if (registry == null)
            return;

        // note: Use curated imported UI textures during generation without spawning scene objects inside the fragile loading loop.
        if (_questBookTexture == null)
        {
            registry.TryGetTexture(
                "quest_book",
                out _questBookTexture);
        }

        if (_questScrollTexture == null)
        {
            registry.TryGetTexture(
                "quest_scroll",
                out _questScrollTexture);
        }
    }

    private static string CollapseWhitespace(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        StringBuilder builder =
            new StringBuilder(
                value.Length);

        bool previousWasWhitespace =
            false;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                value[i];

            if (char.IsWhiteSpace(
                    c))
            {
                if (previousWasWhitespace)
                    continue;

                builder.Append(
                    ' ');

                previousWasWhitespace =
                    true;

                continue;
            }

            builder.Append(
                c);

            previousWasWhitespace =
                false;
        }

        return
            builder
                .ToString()
                .Trim();
    }
}
