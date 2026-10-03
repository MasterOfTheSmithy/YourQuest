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
    private bool _showDiagnostics;
    private Vector2 _diagnosticsScroll;

    private string _generationPhase =
        "Preparing world generation";

    private string _generationSubstep =
        "Waiting for the first accepted world record";

    private int _generationStepIndex = 1;

    private int _generationStepCount = 1;

    private string _generationProgressLabel =
        "STEP 1 OF 1  /  PREPARING WORLD GENERATION  /  0%";

    private static readonly string[] ThinkingDotFrames = { ".", "..", "..." };

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
    private string _retryGenerationLabel = "Retry generation";
    private Action _returnToTitle;
    private bool _ordinaryLogStackTracesSuppressed;
    private StackTraceLogType _previousOrdinaryLogStackTraceType;

    private bool _finishingGenerationPresentation;

    private float _handoffBlackoutAlpha;

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

    // note: Expose the active generation phase to the beta control surface so a long build can be diagnosed without guessing from editor responsiveness.
    public static string CurrentGenerationPhase =>
        s_instance != null ? s_instance._generationPhase : string.Empty;

    // note: Expose the active generation substep alongside its phase so memory or throughput stalls identify the exact materialization operation.
    public static string CurrentGenerationSubstep =>
        s_instance != null ? s_instance._generationSubstep : string.Empty;

    // note: Publish the loading screen's truthful progress value for unattended fixture and regression evidence.
    public static float CurrentGenerationProgress =>
        s_instance != null ? s_instance._progress : 0f;

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

        // note: Initial generation may show progress over the world, but it must never hand camera ownership to the title/Goddess stage; the live gameplay camera remains free to look while generation catches up.
        YQTitleEnvironmentLoader.ReleaseWorldGeneration();

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

    public static void SetGenerationWorkStage(
        string phase,
        int stepIndex,
        int stepCount,
        string substep,
        float progress)
    {
        // note: Do not create a loading-screen object for ordinary post-startup builds solely to expose diagnostics.
        if (!YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked && s_instance == null)
            return;

        EnsureInstance();
        // note: Keep the truthful phase available to diagnostics even after the full-screen generation lock is released; only the visible HUD remains gated.
        s_instance._generationPhase = string.IsNullOrWhiteSpace(phase)
            ? "Forming the world"
            : phase.Trim();
        s_instance._generationSubstep = string.IsNullOrWhiteSpace(substep)
            ? "Working..."
            : substep.Trim();
        s_instance._generationStepCount = Mathf.Max(1, stepCount);
        s_instance._generationStepIndex = Mathf.Clamp(
            stepIndex,
            1,
            s_instance._generationStepCount);
        s_instance._progress = Mathf.Clamp01(progress);
        s_instance.RefreshGenerationProgressLabel();

        if (!YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked)
            return;

        // note: Mechanical progress is intentionally separate from Goddess dialogue so frequent truthful substeps never pollute or restart her thought transcript.
        s_instance.enabled = true;
    }

    public static void SetGenerationSubstep(string substep)
    {
        // note: Nested loaders may explain current work without changing their parent's phase number, percentage, or dialogue.
        if (!YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked || s_instance == null)
            return;
        s_instance._generationSubstep = string.IsNullOrWhiteSpace(substep) ? "Working..." : substep.Trim();
    }

    public static void ShowGenerationFailure(
        string status,
        Action retryGeneration,
        Action returnToTitle,
        string retryLabel = "Retry generation")
    {
        YQStartupLoadingScreen screen = ShowGeneration(
            status,
            1f);

        if (screen == null)
            return;

        // note: A watchdog stop remains an interactive terminal state instead of impersonating an endlessly running loading screen.
        screen._generationFailure = true;
        screen._retryGeneration = retryGeneration;
        // note: Camera-only recovery must not imply that accepted world content will be regenerated.
        screen._retryGenerationLabel = string.IsNullOrWhiteSpace(retryLabel) ? "Retry generation" : retryLabel;
        screen._returnToTitle = returnToTitle;
    }

    public static void ClearGenerationFailure()
    {
        if (s_instance == null)
            return;

        // note: Clear stale recovery callbacks before a fresh deterministic attempt resumes normal progress reporting.
        s_instance._generationFailure = false;
        s_instance._retryGeneration = null;
        s_instance._retryGenerationLabel = "Retry generation";
        s_instance._returnToTitle = null;
    }

    private static void DismissOrphanedGenerationPresentation()
    {
        if (s_instance == null ||
            !s_instance._generationMode ||
            s_instance._finishingGenerationPresentation ||
            (YourQuestTutorialAutoBootstrap.GameplayRuntimeReady &&
             !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased))
        {
            return;
        }

        // note: Disable immediately before deferred destruction so an orphaned modal cannot consume another frame of input.
        YQStartupLoadingScreen orphan = s_instance;
        orphan.enabled =
            false;

        orphan.gameObject.SetActive(
            false);

        // note: Clear the static owner before deferred destruction so bootstrap can create the ordinary loading screen instead of reusing this inactive generation view.
        s_instance = null;

        Destroy(
            orphan.gameObject);
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
            RefreshGenerationProgressLabel();

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
        RefreshGenerationProgressLabel();

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
        float revealHoldSeconds = 0f,
        Action revealGameplay = null,
        Func<bool> canRevealGameplay = null,
        Action handoffFailed = null)
    {
        // note: Validation is repeated after asynchronous boundaries; the original build may have been replaced while presentation waited.
        if (!CanContinueGenerationHandoff(canRevealGameplay))
            yield break;
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

        if (!CanContinueGenerationHandoff(canRevealGameplay))
            yield break;

        SetStage(
            "Entering YourQuest...",
            1f);

        if (!CanContinueGenerationHandoff(canRevealGameplay))
            yield break;
        SetGenerationWorkStage("Entering the world", 9, 9,
            "Preparing the surrounding world before player release", 0.99f);
        YQTitleEnvironmentLoader.ReleaseWorldGeneration();
        if (!CanContinueGenerationHandoff(canRevealGameplay))
            yield break;

        // note: Release the generation lock so bootstrap can finish streaming/camera checks, while retaining this presentation until actual player release.
        revealGameplay?.Invoke();

        // note: The title-stage handoff may destroy this presentation owner while its delayed coroutine is unwinding; do not dereference a destroyed Unity object.
        if (this == null || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
            yield break;
        Destroy(
            gameObject);
    }

    private bool CanContinueGenerationHandoff(Func<bool> canRevealGameplay)
    {
        if (canRevealGameplay == null || canRevealGameplay())
            return true;
        // note: Invalidated work returns to a visible loading state; it never hides the recovery UI or releases movement.
        CancelGenerationHandoff();
        return false;
    }

    public void CancelGenerationHandoff()
    {
        // note: Coroutine ownership stays with the builder; this only resets the presentation after that work is stopped or invalidated.
        _finishingGenerationPresentation = false;
        _handoffBlackoutAlpha = 0f;
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

        // note: A new loading transaction must never inherit a completed phase label from the previous world.
        _generationPhase = "Preparing world generation";
        _generationSubstep = "Waiting for the first accepted world record";
        _generationStepIndex = 1;
        _generationStepCount = 1;
        RefreshGenerationProgressLabel();
    }

    private void RefreshGenerationProgressLabel()
    {
        // note: Cache the composed label when progress changes so OnGUI does not allocate uppercase and percentage strings every loading frame.
        _generationProgressLabel =
            "STEP " + _generationStepIndex + " OF " + _generationStepCount +
            "  /  " + _generationPhase.ToUpperInvariant() + "  /  " +
            Mathf.RoundToInt(Mathf.Clamp01(_progress) * 100f) + "%";
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
            !_finishingGenerationPresentation &&
            !(YourQuestTutorialAutoBootstrap.GameplayRuntimeReady &&
              !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased))
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
            DrawHandoffBlackout();
            return;
        }

        // note: Match the title design scale while displaying authoritative boot progress, never a simulated timer.
        Matrix4x4 previous = GUI.matrix;
        float scale = Mathf.Max(.1f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        DrawRect(new Rect(0,0,width,height),new Color(.008f,.020f,.038f,.99f));
        float x = (width-720f)*.5f;
        float y = height*.36f;
        GUI.Label(new Rect(x,y-54,720,28), "YOUR JOURNEY  /  WORLD CONNECTION", _smallStyle);
        GUI.Label(new Rect(x,y-20,620,58), _title, _titleStyle);
        GUI.Label(new Rect(x,y+50,650,70), _status, _statusStyle);
        Rect track = new Rect(x,y+142,720,5);
        DrawRect(track,new Color(.16f,.29f,.39f,.6f));
        float progress = Mathf.Clamp01(_progress);
        DrawRect(new Rect(track.x,track.y,track.width*progress,track.height),new Color(.64f,.90f,1f));
        if (progress>0)
            DrawRect(new Rect(track.x+track.width*progress-2,track.y-3,2,11),Color.white);
        GUI.Label(new Rect(x+642,y+92,78,40),Mathf.RoundToInt(progress*100)+"%",_percentStyle);
        GUI.Label(new Rect(x,y+166,720,38), "Your journey opens when your surroundings are ready.",_smallStyle);
        // note: Retained errors stay discoverable without a permanent console overlay obscuring the world presentation.
        string detailsLabel = _errors>0 ? "Startup issue - details" : "Connection details";
        if (YQBlueglassStyle.GuiButton(new Rect(x,y+222,220,36),_showDiagnostics ? "Close details" : detailsLabel))
            _showDiagnostics=!_showDiagnostics;
        if (_showDiagnostics)
        {
            Rect details = new Rect(x,y+270,720,Mathf.Max(72,height-y-290));
            DrawDiagnostics(details);
        }
        GUI.matrix = previous;
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
            int frame = Mathf.FloorToInt(Time.unscaledTime * 2.4f) %
                ThinkingDotFrames.Length;
            dialogue = "Securing connection" + ThinkingDotFrames[frame];
        }

        Rect dialogueRect = new Rect(
            margin,
            margin,
            dialogueWidth,
            dialogueHeight);
        // note: The cinematic thought stream keeps full-height text and lets older thoughts leave through the top instead of compressing or clipping the newest line.
        DrawCinematicGenerationTranscript(
            dialogueRect,
            dialogue);

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
                progressRect.y - 46f,
                progressRect.width,
                20f),
            _generationProgressLabel,
            _generationPhaseStyle);
        GUI.Label(
            new Rect(
                progressRect.x,
                progressRect.y - 24f,
                progressRect.width,
                20f),
            _generationSubstep,
            _smallStyle);
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

        // note: Preserve generation dialogue and recovery while making technical details an explicit disclosure.
        float diagnosticWidth = Mathf.Clamp(Screen.width*.26f,250f,430f);
        Rect detailsButton = new Rect(Screen.width-margin-diagnosticWidth,Screen.height-margin-34,diagnosticWidth,34);
        if (YQBlueglassStyle.GuiButton(detailsButton,_showDiagnostics ? "Close details" : _errors>0 ? "Startup issue - details" : "Connection details"))
            _showDiagnostics=!_showDiagnostics;
        if (_showDiagnostics)
        {
            Rect diagnosticsRect = new Rect(detailsButton.x,detailsButton.y-120,diagnosticWidth,112);
            DrawRect(diagnosticsRect,new Color(.005f,.025f,.045f,.94f));
            DrawDiagnostics(new Rect(diagnosticsRect.x+10,diagnosticsRect.y+8,diagnosticsRect.width-20,diagnosticsRect.height-16));
        }
        if (_generationFailure)
            DrawGenerationFailureActions(margin);
    }

    private void DrawDiagnostics(Rect viewport)
    {
        // note: Keep every retained diagnostic accessible without expanding technical text over the loading composition.
        string details=BuildDiagnosticsText();
        float width=Mathf.Max(100,viewport.width-20);
        float height=Mathf.Max(viewport.height,_smallStyle.CalcHeight(new GUIContent(details),width));
        _diagnosticsScroll=GUI.BeginScrollView(viewport,_diagnosticsScroll,new Rect(0,0,width,height));
        GUI.Label(new Rect(0,0,width,height),details,_smallStyle);
        GUI.EndScrollView();
    }

    private void DrawHandoffBlackout()
    {
        if (_handoffBlackoutAlpha <= 0f)
            return;

        // note: One full-screen fill masks the camera swap without render textures, extra cameras, or a one-frame world flash.
        DrawRect(
            new Rect(0f, 0f, Screen.width, Screen.height),
            new Color(0f, 0f, 0f, _handoffBlackoutAlpha));
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
        if (YQBlueglassStyle.GuiButton(
                new Rect(x, y, buttonWidth, buttonHeight),
                _retryGenerationLabel))
        {
            Action retry = _retryGeneration;
            ClearGenerationFailure();
            retry?.Invoke();
            return;
        }

        if (YQBlueglassStyle.GuiButton(
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
        // note: Player-facing Goddess presentation describes the visible state without exposing the local inference implementation.
        string label = thinking
            ? "THINKING" + suffix
            : "STANDBY";
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

    private void DrawCinematicGenerationTranscript(
        Rect dialogueRect,
        string dialogue)
    {
        float contentHeight =
            Mathf.Max(
                dialogueRect.height,
                _generationDialogueStyle.CalcHeight(
                    new GUIContent(
                        dialogue),
                    dialogueRect.width) +
                4f);

        float contentY =
            Mathf.Min(
                0f,
                dialogueRect.height -
                contentHeight);

        // note: This clip window has no scroll chrome; bottom anchoring continuously moves completed thoughts off its top edge as new prose arrives.
        GUI.BeginGroup(
            dialogueRect);

        GUI.Label(
            new Rect(
                2f,
                contentY + 2f,
                dialogueRect.width,
                contentHeight),
            dialogue,
            _generationDialogueShadowStyle);

        GUI.Label(
            new Rect(
                0f,
                contentY,
                dialogueRect.width,
                contentHeight),
            dialogue,
            _generationDialogueStyle);

        GUI.EndGroup();
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
