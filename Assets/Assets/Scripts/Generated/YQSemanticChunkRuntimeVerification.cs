using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Unity.Profiling;
using UnityEngine;

// note: This bounded probe exercises the live player-following streamer only when explicitly armed by the editor marker.
// note: Run after the streamer camera pass so live-view assertions describe the pose immediately before rendering.
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class YQSemanticChunkRuntimeVerification : MonoBehaviour
{
    // note: Keep the expensive traversal acceptance probe explicitly marker/menu gated so normal Play Mode never launches the chunk-stream test.
    private const bool RuntimeVerificationEnabled = true;
    // note: The probe validates destination-cell ground publication independently from the broader decoration frontier.
    // note: Full authored materialization can legitimately exceed four minutes while Unity streams reviewed sites; keep the probe alive until that transaction settles.
    private const float StartupTimeoutSeconds = 600f;
    private const float MovementTimeoutSeconds = 45f;
    // note: Initial coverage can pass before the bounded retained ring has admitted a non-view recovery owner; wait for that real owner instead of weakening the fault-injection witness.
    private const double RecoveryWitnessTimeoutSeconds = 90.0;
    // note: Streamed ecology is real Unity work and can exceed the short movement timeout; recovery stages use one bounded wall-clock budget instead of timing out a live publisher mid-transaction.
    private const float PublicationRecoveryTimeoutSeconds = 300f;
    // note: The injected appearance failure should exhaust four short retries promptly; fail this liveness probe without waiting through the unrelated publication watchdog.
    private const float AppearanceRetryExhaustionTimeoutSeconds = 30f;
    // note: A held appearance worker should be observable promptly on a reused physical owner; keep the lag witness bounded so a lost scheduler lease cannot idle for five minutes.
    private const float AppearanceLagRecoveryTimeoutSeconds = 30f;
    // note: Natural unload travel uses its own wall-clock watchdog because real production movement may need several minutes to clear the retained ring.
    private const float UnloadRevisitTimeoutSeconds = 600f;
    // note: Recovery traversal changes ordinary motor direction after a bounded wall-clock stall so an unrelated blocked edge cannot monopolize the whole gate.
    private const float RecoveryNoProgressTimeoutSeconds = 8f;
    // note: A short physical trail lets the unload witness retrace narrow, obstacle-aware production-motor movement without cutting across a traversable cell.
    private const float ReturnWaypointSampleDistanceMeters = 5f;
    // note: Allow a stalled return waypoint enough collision-resolved lateral steps to route around a wide prop while remaining inside its ready owner.
    private const int SameOwnerReturnBypassMaxAttempts = 8;
    private const float SameOwnerReturnBypassStepMeters = 3f;
    private const float ActiveTerrainBarrierObservationTimeoutSeconds = 30f;
    // note: Full-world atomic persistence is deferred from Update and gets its own bounded commit budget instead of inflating the per-frame streaming budget.
    private const float PersistenceCommitBudgetSeconds = 0.10f;
    // note: Allow a small wall-clock sampling margin for editor timer jitter while retaining the raw measured peak in the report.
    private const float StreamingMeasurementToleranceSeconds = 0.010f;
    // note: Run the 150/300 m/s, reversal, diagonal, deadline, and frame-budget probes in the production acceptance pass so skipped stress cannot be reported as completion.
    private static readonly bool EnforceG07ThroughputGates = true;
    // note: Use normal camera-relative movement keys to route around a blocked frontier without teleporting or changing streamer scheduling policy.
    private static readonly UnityEngine.InputSystem.Key[] RecoveryCardinalDirections =
    {
        UnityEngine.InputSystem.Key.W,
        UnityEngine.InputSystem.Key.D,
        UnityEngine.InputSystem.Key.S,
        UnityEngine.InputSystem.Key.A
    };
    private static bool _started;
    private static bool _completed;
    // note: A bounded motor-only run can qualify the changed 260 m/s witness without entering unrelated world and recovery phases.
    private static bool _focusedSpeed260MotorWitnessOnly;
    // note: Preserve the first nested coroutine failure so the terminal report names the failed traversal phase instead of only saying FAIL.
    private static string _lastNestedFailure = string.Empty;
    private double _nextLivenessReportAt;
    private bool _startupPredicateReported;
    private Stack<IEnumerator> _verificationIterators;
    // note: Append only new phase text so asynchronous heartbeat evidence is never overwritten by an older report snapshot.
    private static StringBuilder _activeReport;
    private static int _writtenReportCharacters;
    private static bool _terminalReportWritten;
    // note: Keep the verifier's explicitly queued keyboard current after native input processing so the production motor reads the event that was actually injected.
    private static UnityEngine.InputSystem.Keyboard _verificationKeyboard;
    private static bool _verificationKeyboardHooked;
    // note: Clone editor input routing only for the marker-gated run so focus changes cannot silently discard virtual keyboard events.
    private static UnityEngine.InputSystem.InputSettings _verificationOriginalInputSettings;
    private static UnityEngine.InputSystem.InputSettings _verificationTestInputSettings;

    public static bool IsRunning => _started;
    public static bool HasCompleted => _completed;

    public static void Begin()
    {
        BeginInternal(false);
    }

    private static void BeginInternal(bool focusedSpeed260MotorWitnessOnly)
    {
        if (!RuntimeVerificationEnabled || _started)
            return;
        _focusedSpeed260MotorWitnessOnly = focusedSpeed260MotorWitnessOnly;
        _started = true;
        _completed = false;
        _lastNestedFailure = string.Empty;
        _activeReport = null;
        _writtenReportCharacters = 0;
        _terminalReportWritten = false;
        GameObject root = new GameObject("YQ_SemanticChunkRuntimeVerification");
        DontDestroyOnLoad(root);
        root.AddComponent<YQSemanticChunkRuntimeVerification>();
    }

    public static bool TryBeginFromCurrentPlaySession()
    {
        // note: Refuse to launch from Edit Mode or duplicate an active probe; the menu is an explicit current-session entry point.
        if (!RuntimeVerificationEnabled || !Application.isPlaying || _started)
            return false;
        BeginInternal(false);
        return true;
    }

    public static bool TryBeginFocusedSpeed260WitnessFromCurrentPlaySession()
    {
        // note: Run only the production motor matrix and its abrupt 260 m/s boundary witness before attempting long world or recovery checks.
        if (!RuntimeVerificationEnabled || !Application.isPlaying || _started)
            return false;
        BeginInternal(true);
        return true;
    }

    public static void RestartForEditor()
    {
        // note: Ignore duplicate editor dispatches while the current probe is active so a lingering file event cannot destroy its terminal receipt.
        if (!RuntimeVerificationEnabled || _started)
            return;
        // note: Replace only the completed unattended probe so a new marker can exercise the current authored scene after a code reload.
        YQSemanticChunkRuntimeVerification[] existing =
            FindObjectsByType<YQSemanticChunkRuntimeVerification>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < existing.Length; index++)
            Destroy(existing[index].gameObject);
        _started = false;
        Begin();
    }

    private void OnDestroy()
    {
        // note: Unwind nested probes on manual stop as well as exceptions, releasing injected keys and publication holds in their finally blocks.
        if (!_completed)
        {
            _lastNestedFailure = "ABORTED_BEFORE_TERMINAL_REPORT";
            DisposeVerificationIterators();
            if (!_terminalReportWritten)
                AppendVerifierStatusToReport(_lastNestedFailure);
        }
        // note: Keep completion observable until the next explicit Begin resets this session.
        ReleaseVerificationKeyboardHook();
        _started = false;
    }

    private void Update()
    {
        if (!_completed && Time.realtimeSinceStartupAsDouble >= _nextLivenessReportAt)
        {
            // note: A low-rate heartbeat proves the verifier GameObject remains alive while its startup predicate is being evaluated.
            _nextLivenessReportAt = Time.realtimeSinceStartupAsDouble + 2.0;
            YQPlayerFollowingSemanticChunkStreamer liveStreamer = FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
            // note: Include only bounded scheduler counters so a stuck settle wait reveals ownership state without serializing streamed objects.
            string coverageFailure = string.Empty;
            bool requiredCoverageReady = liveStreamer != null && liveStreamer.TryValidateRequiredCoverage(out coverageFailure);
            string visualCoverageFailure = string.Empty;
            bool visualCoverageReady = liveStreamer != null && liveStreamer.TryValidateVisualCoverage(out visualCoverageFailure);
            YQInvestorPlayerMotor[] liveMotors = FindObjectsByType<YQInvestorPlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform livePlayer = YQInvestorPlayerMotor.ActiveMotor != null ? YQInvestorPlayerMotor.ActiveMotor.transform : null;
            AppendVerifierStatusToReport("HEARTBEAT" +
                (liveStreamer == null
                    ? string.Empty
                    : " queue=" + liveStreamer.QueuedChunkCount +
                      ",pendingTerrain=" + liveStreamer.PendingTerrainCollisionCount +
                      ",pendingPaint=" + liveStreamer.PendingTerrainPaintCount +
                      ",generating=" + liveStreamer.IsGenerating +
                      ",lifecycle=" + liveStreamer.HasPendingLifecycleWork +
                      ",active=" + liveStreamer.ActiveChunkCount +
                      ",retained=" + liveStreamer.RetainedChunkCount +
                      ",physical=" + liveStreamer.PhysicalChunkCount +
                      ",terrainAdmission=" + liveStreamer.TerrainAdmissionDiagnostics +
                      ",semantic=" + liveStreamer.SemanticChunkCount +
                      ",coverage=" + requiredCoverageReady +
                      ",coverageFailure=" + coverageFailure +
                      ",visualCoverage=" + visualCoverageReady +
                      ",visualCoverageFailure=" + visualCoverageFailure +
                      ",motors=" + liveMotors.Length +
                      ",playerPosition=" + (livePlayer != null ? livePlayer.position.ToString() : "<null>")));
            if (!_startupPredicateReported && Time.realtimeSinceStartupAsDouble >= 5.0)
            {
                _startupPredicateReported = true;
                YQPlayerFollowingSemanticChunkStreamer streamer = FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
                WorldStateManager manager = WorldStateManager.Instance;
                YQGeneratedWorldRuntimeBuilder builder = YQGeneratedWorldRuntimeBuilder.Instance ??
                    FindFirstObjectByType<YQGeneratedWorldRuntimeBuilder>(FindObjectsInactive.Include);
                bool planPresent = manager != null && manager.State != null && manager.State.generatedWorldPlan != null &&
                    manager.State.generatedWorldPlan.semanticChunks != null && manager.State.generatedWorldPlan.semanticChunks.Count > 0;
                // note: Snapshot every readiness predicate from the live verifier component so a coroutine sentinel cannot conceal which production owner is absent.
                AppendVerifierStatusToReport("STARTUP_PREDICATE streamer=" + (streamer != null) +
                    ",manager=" + (manager != null) +
                    ",player=" + (YQInvestorPlayerMotor.ActiveMotor != null) +
                    ",authoritativePlayer=" + (YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.IsAuthoritative) +
                    ",builder=" + (builder != null) +
                    ",plan=" + planPresent +
                    ",materialized=" + (builder != null && builder.HasMaterializedCurrentWorld) +
                    ",recoveryRequired=" + (builder != null && builder.InitialGenerationRecoveryRequired) +
                    ",gameplayLocked=" + YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked +
                    ",presentationReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased);
            }
        }
    }

    private void LateUpdate()
    {
        // note: Inspect after the production motor's Update; queue input for the next normal Input System update without manually advancing its clock.
        if (!_completed)
            AdvanceVerificationOneFrame();
    }

    private void DisposeVerificationIterators()
    {
        // note: One failing cleanup must not prevent outer finally blocks from releasing fault injection or flushing the partial report.
        while (_verificationIterators != null && _verificationIterators.Count > 0)
        {
            try { (_verificationIterators.Pop() as IDisposable)?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    private void Start()
    {
        // note: Keep nested verification waits under a main-thread frame driver so every yielded phase advances deterministically and remains observable.
        _verificationIterators = new Stack<IEnumerator>();
        _verificationIterators.Push(RunVerification());
    }

    private void AdvanceVerificationOneFrame()
    {
        if (_verificationIterators == null)
            return;
        while (_verificationIterators.Count > 0)
        {
            IEnumerator current = _verificationIterators.Peek();
            object yielded;
            try
            {
                if (!current.MoveNext())
                {
                    (current as IDisposable)?.Dispose();
                    _verificationIterators.Pop();
                    continue;
                }
                yielded = current.Current;
            }
            catch (Exception exception)
            {
                // note: Nested exceptions precede root disposal, while a root exception runs its finally before escaping MoveNext; persist the reason in both cases.
                string failureText = exception.GetType().Name + ": " + exception.Message;
                _lastNestedFailure = failureText;
                Debug.LogError("[YQSemanticChunkRuntimeVerification] " + exception);
                AppendNestedFailureToReport(failureText);
                DisposeVerificationIterators();
                // note: The root coroutine was disposed with the failed nested iterator, so finalize the report explicitly instead of relying on its finally block.
                _started = false;
                _completed = true;
                return;
            }
            if (yielded is IEnumerator nested)
            {
                _verificationIterators.Push(nested);
                continue;
            }
            return;
        }
        Destroy(gameObject);
    }

    private IEnumerator RunVerification()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("# YourQuest Semantic Chunk Runtime Verification");
        report.AppendLine("- verificationScope: " + (_focusedSpeed260MotorWitnessOnly
            ? "focused production motor matrix and abrupt speed-260 boundary witness; remaining R1/G08 rows not run"
            : "G08-R1 publication recovery with R2 motor and unload/revisit witnesses"));
        report.AppendLine("- startedUtc: " + DateTime.UtcNow.ToString("O"));
        AppendExecutionIdentity(report);
        YQPlayerFollowingSemanticChunkStreamer streamer = null;
        Transform player = null;
        YQInvestorPlayerMotor motor = null;
        CharacterController characterController = null;
        WorldStateManager manager = null;
        YQGeneratedWorldRuntimeBuilder builder = null;
        WorldState testWorld = null;
        Vector3 startingPosition = Vector3.zero;
        bool success = false;
        bool recoveryCasesVerified = false;
        bool unloadRevisitPassed = false;
        bool unloadRevisitInputInconclusive = false;
        bool verificationInconclusive = false;
        bool postMotorPublicationWorkContractFailed = false;
        string postMotorPublicationWorkContractFailure = string.Empty;
        string postMotorVisualCoverageFailure = string.Empty;
        string overlayFeatureId = string.Empty;
        string overlayTargetObjectId = string.Empty;

        try
        {
            // note: Persist an immediate diagnostic state so an explicit menu run is observable while normal startup is still pending.
            report.AppendLine("- status: WAITING_FOR_STARTUP");
            VerifyMacroFieldHashParity(report);
            WriteReport(report, false, false);
            double startupDeadline = Time.realtimeSinceStartupAsDouble + StartupTimeoutSeconds;
            bool startupDiagnosticWritten = false;
            double nextStartupPredicateTraceAt = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < startupDeadline)
            {
                streamer = FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
                manager = WorldStateManager.Instance;
                player = ResolveAuthoritativePlayer();
                testWorld = manager != null ? manager.State : testWorld;
                // note: Include inactive scene objects so the authored builder is recognized before its first Awake/Update frame.
                YQGeneratedWorldRuntimeBuilder sceneBuilder =
                    FindFirstObjectByType<YQGeneratedWorldRuntimeBuilder>(FindObjectsInactive.Include);
                builder = YQGeneratedWorldRuntimeBuilder.Instance ?? sceneBuilder;
                bool livePlanUnavailable = testWorld == null || testWorld.generatedWorldPlan == null ||
                    testWorld.generatedWorldPlan.semanticChunks == null || testWorld.generatedWorldPlan.semanticChunks.Count == 0;
                bool productionReady = streamer != null && manager != null && builder != null &&
                    !livePlanUnavailable && builder.HasMaterializedCurrentWorld &&
                    !builder.InitialGenerationRecoveryRequired &&
                    !YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked &&
                    YourQuestTutorialAutoBootstrap.GameplayPresentationReleased &&
                    player != null;
                if (!productionReady && Time.realtimeSinceStartupAsDouble >= nextStartupPredicateTraceAt)
                {
                    // note: Persist the exact in-process startup references so a status-writer mismatch identifies the blocking predicate without waiting for timeout.
                    nextStartupPredicateTraceAt = Time.realtimeSinceStartupAsDouble + 2.0;
                    report.AppendLine("- startupPredicateTrace: streamer=" + (streamer != null) +
                        ",manager=" + (manager != null) +
                        ",player=" + (player != null) +
                        ",builder=" + (builder != null) +
                        ",plan=" + !livePlanUnavailable +
                        ",materialized=" + (builder != null && builder.HasMaterializedCurrentWorld) +
                        ",recoveryRequired=" + (builder != null && builder.InitialGenerationRecoveryRequired) +
                        ",gameplayLocked=" + YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked +
                        ",presentationReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased);
                    WriteReport(report, false, false);
                }
                if (!startupDiagnosticWritten && Time.realtimeSinceStartupAsDouble >= startupDeadline - StartupTimeoutSeconds + 5.0)
                {
                    // note: Capture the first live readiness mismatch so a stalled unattended probe identifies ownership state without waiting for the full timeout.
                    YQInvestorPlayerMotor[] liveMotors = FindObjectsByType<YQInvestorPlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    report.AppendLine("- startupDiagnostic: streamer=" + (streamer != null) +
                        ", manager=" + (manager != null) +
                        ", player=" + (player != null) +
                        ", builder=" + (builder != null) +
                        ", plan=" + !livePlanUnavailable +
                        ", materialized=" + (builder != null && builder.HasMaterializedCurrentWorld) +
                        ", recoveryRequired=" + (builder != null && builder.InitialGenerationRecoveryRequired) +
                        ", gameplayLocked=" + YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked +
                        ", presentationReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased +
                        ", activeMotor=" + (YQInvestorPlayerMotor.ActiveMotor != null) +
                        ", activeMotorAuthoritative=" + (YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.IsAuthoritative) +
                        ", motorCount=" + liveMotors.Length);
                    WriteReport(report, false, false);
                    startupDiagnosticWritten = true;
                }
                // note: Begin only after the accepted world is fully materialized, gameplay is released, and the authoritative player owns movement; no rebuild or synthetic fixture can produce a PASS.
                if (productionReady)
                    break;
                yield return null;
            }

            if (streamer == null || testWorld == null || player == null || builder == null ||
                !builder.HasMaterializedCurrentWorld || builder.InitialGenerationRecoveryRequired ||
                YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked ||
                !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
            {
                // note: Preserve the production-owner state when startup times out so an incomplete authored build cannot be mistaken for a streaming edge.
                report.AppendLine("- liveStartupFailure: builderPresent=" + (builder != null) +
                    " builderMaterialized=" + (builder != null && builder.HasMaterializedCurrentWorld) +
                    " recoveryRequired=" + (builder != null && builder.InitialGenerationRecoveryRequired) +
                    " gameplayLocked=" + YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked);
                report.AppendLine("- liveStartupFailurePredicates: streamer=" + (streamer != null) +
                    ", world=" + (testWorld != null) +
                    ", player=" + (player != null) +
                    ", builder=" + (builder != null) +
                    ", plan=" + (testWorld != null && testWorld.generatedWorldPlan != null) +
                    ", materialized=" + (builder != null && builder.HasMaterializedCurrentWorld) +
                    ", gameplayReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased);
                throw new InvalidOperationException("live streamer, test world, or authoritative player did not become available");
            }
            report.AppendLine("- sourceMode: live-authored-world");
            report.AppendLine("- status: STARTUP_READY");
            // note: Prove a selected profile's accepted terrain snapshot was reused when its persisted identity matches the loaded world.
            YQProfileSaveSystem profileSave = YQProfileSaveSystem.Instance;
            AppendWorldIdentity(report, manager.State, profileSave);
            string loadedTerrainSnapshotJson = string.Empty;
            bool loadedTerrainSnapshotDocument = profileSave != null &&
                profileSave.TryGetLoadedAuxiliaryDocument(
                    YQGeneratedWorldTerrain.ProfileTerrainSnapshotDocumentId,
                    out loadedTerrainSnapshotJson);
            YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord loadedTerrainSnapshot = null;
            if (loadedTerrainSnapshotDocument && !string.IsNullOrWhiteSpace(loadedTerrainSnapshotJson))
            {
                try
                {
                    loadedTerrainSnapshot = JsonUtility.FromJson<YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord>(
                        loadedTerrainSnapshotJson);
                }
                catch (ArgumentException)
                {
                    // note: A malformed optional snapshot is reported as unavailable and cannot be mistaken for an accepted restore.
                }
            }
            WorldState startupWorld = manager.State;
            GeneratedWorldPlanRecord startupPlan = startupWorld != null
                ? startupWorld.generatedWorldPlan
                : null;
            bool loadedTerrainSnapshotMatchesWorld = loadedTerrainSnapshot != null &&
                loadedTerrainSnapshot.schemaVersion == 1 && loadedTerrainSnapshot.hasTerrain &&
                profileSave != null && startupWorld != null && startupWorld.worldIdentity != null && startupPlan != null &&
                string.Equals(loadedTerrainSnapshot.ownerProfileId, profileSave.ActiveProfileId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(loadedTerrainSnapshot.worldId, startupWorld.worldIdentity.worldId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(loadedTerrainSnapshot.worldSeed, startupPlan.worldSeed, StringComparison.Ordinal);
            bool profileTerrainRestoreSucceeded = builder.LastProfileTerrainRestoreSucceeded;
            if (loadedTerrainSnapshotMatchesWorld && !profileTerrainRestoreSucceeded)
            {
                report.AppendLine("- acceptedSaveTerrainStartup: FAIL reason=" +
                    (string.IsNullOrWhiteSpace(builder.LastProfileTerrainRestoreFailureReason)
                        ? "matching saved terrain was not restored"
                        : builder.LastProfileTerrainRestoreFailureReason));
                WriteReport(report, false, false);
                throw new InvalidOperationException("selected profile's matching terrain snapshot was not restored");
            }
            report.AppendLine("- acceptedSaveTerrainStartup: " +
                (loadedTerrainSnapshotMatchesWorld && profileTerrainRestoreSucceeded
                    ? "PASS"
                    : loadedTerrainSnapshot != null && loadedTerrainSnapshot.hasTerrain
                        ? "NOT_APPLICABLE_SNAPSHOT_DOES_NOT_MATCH_ACTIVE_WORLD"
                        : "NOT_APPLICABLE_NO_ACCEPTED_TERRAIN_SNAPSHOT"));
            report.AppendLine("- profileTerrainSnapshotMatchesWorld: " + loadedTerrainSnapshotMatchesWorld);
            report.AppendLine("- profileTerrainRestoreSucceeded: " + profileTerrainRestoreSucceeded);
            // note: Flush readiness before any synchronous preflight work so a stalled acceptance helper cannot hide that production startup already succeeded.
            WriteReport(report, false, false);
            yield return null;

            // note: Validate canonical procedural connectors before movement so a shared border cannot hide asymmetric route or water records.
            if (!YQContinuousWorldFeatureAuthority.TryValidatePortalSymmetry(testWorld.generatedWorldPlan.worldSeed, streamer.chunkWorldSize, out string portalFailure))
                throw new InvalidOperationException("continuation portal symmetry failed: " + portalFailure);
            report.AppendLine("- continuationPortalSymmetry: PASS");
            if (!YQContinuousWorldFeatureAuthority.TryValidateBoundaryContinuation(out string boundaryContinuationFailure))
            {
                // note: Preserve the focused continuation failure in the report so a failed unattended probe identifies the exact direction/cell instead of only reporting FAIL.
                report.AppendLine("- authoredFeatureBoundaryContinuation: FAIL: " + boundaryContinuationFailure);
                throw new InvalidOperationException("authored feature boundary continuation failed: " + boundaryContinuationFailure);
            }
            report.AppendLine("- authoredFeatureBoundaryContinuation: PASS");
            // note: Yield after connector validation so the editor remains responsive before the first terrain refresh and queue-settle wait.
            yield return null;

            motor = player.GetComponentInChildren<YQInvestorPlayerMotor>(true);
            characterController = player.GetComponentInChildren<CharacterController>(true);
            startingPosition = player.position;
            if (motor == null || !motor.enabled || !motor.IsAuthoritative)
                throw new InvalidOperationException("authoritative production motor must already be enabled for physical traversal verification");
            if (characterController == null || !characterController.enabled)
                throw new InvalidOperationException("authoritative CharacterController is unavailable for physical traversal verification");

            // note: Observe readiness at production handoff; a verifier-only settling period would conceal what the player actually sees.
            report.AppendLine("- status: INITIAL_FRONTIER_OBSERVATION");
            // note: Compare the observed production camera with the bootstrap release receipt before input begins.
            report.AppendLine("- initialCamera: fov=" + motor.playerCamera.fieldOfView +
                " position=" + motor.playerCamera.transform.position + " rotation=" + motor.playerCamera.transform.eulerAngles +
                " timeScale=" + Time.timeScale);
            WriteReport(report, false, false);
            if (!streamer.TryValidateRequiredCoverage(out string initialCoverageFailure))
                throw new InvalidOperationException("initial required chunk coverage failed: " + initialCoverageFailure);
            report.AppendLine("- initialRequiredCoverage: PASS");
            // note: The production handoff must already include a complete one-cell preparation ring; this is a startup contract, not a verifier-only settling delay.
            if (!streamer.TryValidatePreparedViewEnvelope(out string initialPreparedViewFailure))
                throw new InvalidOperationException("initial prepared view envelope failed: " + initialPreparedViewFailure);
            report.AppendLine("- initialPreparedViewEnvelope: PASS radius=" + streamer.VisualPreparationRadius);
            if (!streamer.TryValidateVisualCoverage(out string initialVisualCoverageFailure))
                throw new InvalidOperationException("initial visual coverage failed: " + initialVisualCoverageFailure);
            report.AppendLine("- initialVisualCoverage: PASS radius=" + streamer.GuaranteedVisualRadius +
                " prewarm=" + streamer.VisualPrewarmRadius +
                " distance=" + streamer.GuaranteedVisualDistanceMeters.ToString("0", CultureInfo.InvariantCulture) + "m");
            Vector2Int originChunk = streamer.CurrentChunk;

            // note: Record the production handoff preconditions before input is injected, separating a disabled motor from an untraversable starting capsule.
            report.AppendLine("- productionMotorPreflight: enabledBeforeVerifier=" + motor.enabled +
                ", authoritative=" + motor.IsAuthoritative +
                ", cameraPivot=" + (motor.cameraPivot != null) +
                ", playerCamera=" + (motor.playerCamera != null) +
                ", controller=" + (characterController != null && characterController.enabled) +
                ", modalBlocked=" + RuntimeModalUiBlocker.IsBlocked +
                ", timeScale=" + Time.timeScale.ToString("0.###", CultureInfo.InvariantCulture));
            if (!streamer.TryValidateCurrentTraversability(
                    player.position,
                    characterController.radius,
                    characterController.skinWidth,
                    out string initialTraversabilityFailure))
                throw new InvalidOperationException("initial traversability failed: " + initialTraversabilityFailure);
            report.AppendLine("- initialTraversability: PASS");

            // note: Keep the authoritative motor enabled throughout the probe so queue waits and static checks cannot freeze or replace gameplay ownership.
            bool productionMotorMatrixPassed = false;
            bool productionMotorInputInconclusive = false;
            yield return VerifyProductionMotorTraversal(
                streamer,
                motor,
                player,
                characterController,
                report,
                (passed, inputInconclusive) =>
                {
                    productionMotorMatrixPassed = passed;
                    productionMotorInputInconclusive = inputInconclusive;
                });
            if (_focusedSpeed260MotorWitnessOnly)
            {
                // note: End after the persisted motor/boundary measurements so a failed short witness does not spend minutes in unrelated acceptance phases.
                report.AppendLine("- focusedProductionMotorWitness: " +
                    (productionMotorInputInconclusive ? "INCONCLUSIVE" : productionMotorMatrixPassed ? "PASS" : "FAIL"));
                report.AppendLine("- remainingR1AndG08Rows: NOT_RUN_FOCUSED_SCOPE");
                verificationInconclusive = productionMotorInputInconclusive;
                if (productionMotorMatrixPassed || productionMotorInputInconclusive)
                {
                    success = true;
                    report.AppendLine("- result: " + (productionMotorInputInconclusive ? "INCONCLUSIVE" : "PASS"));
                }
                yield break;
            }
            if (productionMotorInputInconclusive)
            {
                // note: Keep publication recovery evidence independent from an unavailable background input device; this is not a locomotion pass.
                report.AppendLine("- productionMotorTraversal: INPUT_DELIVERY_INCONCLUSIVE");
                WriteReport(report, false, false);
            }
            else if (!productionMotorMatrixPassed)
            {
                // note: Keep independent publication recovery evidence runnable after a real motor or frame-budget failure; the final acceptance still fails on this saved result.
                report.AppendLine("- productionMotorTraversal: FAIL per-gate evidence recorded; continuing independent publication recovery checks");
                WriteReport(report, false, false);
            }

            if (!productionMotorInputInconclusive)
            {
                bool postMotorVisualCoveragePassed = streamer.TryValidateVisualCoverage(out postMotorVisualCoverageFailure);
                if (postMotorVisualCoveragePassed)
                {
                    postMotorPublicationWorkContractFailed = false;
                    report.AppendLine("- postMotorVisualCoverage: PASS");
                    report.AppendLine("- postMotorPublicationWorkContract: PASS incompleteVisibleChunks=0");
                }
                else
                {
                    // note: Keep the immediate coverage miss visible in the report while distinguishing live recoverable publication from a stranded visible stage.
                    bool publicationWorkContractPassed = streamer.TryValidateVisiblePublicationWorkContracts(
                        out int incompleteVisibleChunks,
                        out string publicationWorkContractFailure);
                    postMotorPublicationWorkContractFailed = !publicationWorkContractPassed;
                    postMotorPublicationWorkContractFailure = publicationWorkContractFailure;
                    report.AppendLine("- postMotorVisualCoverage: INCOMPLETE " + postMotorVisualCoverageFailure);
                    report.AppendLine("- postMotorPublicationWorkContract: " +
                        (publicationWorkContractPassed ? "PASS" : "FAIL") +
                        " incompleteVisibleChunks=" + incompleteVisibleChunks +
                        (publicationWorkContractPassed ? string.Empty : " failure=" + publicationWorkContractFailure));
                }
            }
            else
                report.AppendLine("- postMotorVisualCoverage: NOT_RUN_INPUT_DELIVERY_INCONCLUSIVE");
            WriteReport(report, false, false);

            if (!TryValidateAdjacentContracts(testWorld.generatedWorldPlan, originChunk, Mathf.Max(1, streamer.generationRadius), out string contractFailure, out float contractDelta))
                throw new InvalidOperationException("semantic edge contract continuity failed: " + contractFailure);
            report.AppendLine("- adjacentEdgeContracts: PASS");
            report.AppendLine("- adjacentEdgeMaximumContractFloatDelta: " + contractDelta.ToString("0.000000", CultureInfo.InvariantCulture));
            int originSemanticCount = testWorld.generatedWorldPlan.semanticChunks.Count;
            report.AppendLine("- originChunk: " + originChunk);
            report.AppendLine("- originSemanticCount: " + originSemanticCount);

            // note: Probe the shared world-space biome field for a real neighboring transition so palette continuity is validated beyond alphamap normalization.
            Terrain biomeTerrain = streamer.AuthoredTerrain;
            YQContinuousWorldCellAuthority biomeAuthority = new YQContinuousWorldCellAuthority(
                testWorld.generatedWorldPlan.worldSeed,
                biomeTerrain,
                null,
                testWorld.generatedWorldPlan,
                streamer.chunkWorldSize);
            if (!TryValidateBiomeTransition(biomeAuthority, biomeTerrain, out string biomeFailure, out float biomeDelta))
                throw new InvalidOperationException("biome transition continuity failed: " + biomeFailure);
            report.AppendLine("- biomeTransition: PASS");
            report.AppendLine("- biomeTransitionMaximumEightMetreWeightDelta: " + biomeDelta.ToString("0.000", CultureInfo.InvariantCulture));
            if (!TryValidateGenerationOrderIndependence(
                    testWorld.generatedWorldPlan,
                    biomeAuthority,
                    streamer.chunkWorldSize,
                    out string orderFailure))
                throw new InvalidOperationException("generation-order independence failed: " + orderFailure);
            report.AppendLine("- generationOrderIndependence: PASS");
            if (!TryValidateDistantProceduralSynthesis(
                    testWorld.generatedWorldPlan,
                    biomeAuthority,
                    biomeTerrain,
                    streamer.chunkWorldSize,
                    out string distantSynthesisFailure,
                    out float minimumDistantRelief,
                    out float maximumDistantRelief,
                    out int distantBiomeCount,
                    out int distantRoadCells,
                    out int distantRiverCells,
                    out int distantLandmarkCells))
                throw new InvalidOperationException("distant procedural synthesis failed: " + distantSynthesisFailure);
            report.AppendLine("- distantProceduralSynthesis: PASS");
            report.AppendLine("- distantReliefRangeMetres: " + minimumDistantRelief.ToString("0.00", CultureInfo.InvariantCulture) + ".." + maximumDistantRelief.ToString("0.00", CultureInfo.InvariantCulture));
            report.AppendLine("- distantRegionalBiomeCount: " + distantBiomeCount);
            report.AppendLine("- distantRoadCells: " + distantRoadCells);
            report.AppendLine("- distantRiverCells: " + distantRiverCells);
            report.AppendLine("- distantIndependentLandmarkCells: " + distantLandmarkCells);
            if (!TryValidateCanonicalChunkIdentity(testWorld.generatedWorldPlan, out string identityFailure))
                throw new InvalidOperationException("canonical chunk identity failed: " + identityFailure);
            report.AppendLine("- canonicalChunkIdentity: PASS");

            Terrain verificationTerrain = streamer.AuthoredTerrain;
            if (verificationTerrain != null && verificationTerrain.terrainData != null)
            {
                Vector3 edgeAttempt = player.position;
                // note: Stand inside the first continuation cell so the raycast validates its collider surface instead of Unity's numerically fragile shared border.
                edgeAttempt.x = verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x + streamer.chunkWorldSize * 0.5f + 0.1f;
                // note: Keep the edge probe inside the authored terrain's longitudinal span so it selects a true east neighbor rather than a non-overlapping diagonal tile from a persisted far position.
                edgeAttempt.z = verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z * 0.5f;
                float halfCell = streamer.chunkWorldSize * 0.5f;
                Vector3 originCornerProbe = new Vector3(
                    verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x + halfCell,
                    startingPosition.y,
                    verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z + halfCell);
                Vector3[] continuationProbePositions =
                {
                    edgeAttempt,
                    new Vector3(verificationTerrain.transform.position.x - halfCell - 0.1f, startingPosition.y, startingPosition.z),
                    new Vector3(startingPosition.x, startingPosition.y, verificationTerrain.transform.position.z - halfCell - 0.1f),
                    new Vector3(startingPosition.x, startingPosition.y, verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z + halfCell + 0.1f),
                    originCornerProbe,
                    new Vector3(originCornerProbe.x, startingPosition.y, verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z - halfCell),
                    new Vector3(verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x - halfCell, startingPosition.y, originCornerProbe.z)
                };
                Terrain extension = null;
                int readyPhysicalNeighborCount = 0;
                for (int probeIndex = 0; probeIndex < continuationProbePositions.Length; probeIndex++)
                {
                    bool ready = TryGetReadyStreamedTerrainAt(
                        streamer,
                        verificationTerrain,
                        continuationProbePositions[probeIndex],
                        out Terrain candidateTerrain);
                    if (probeIndex == 0)
                        extension = candidateTerrain;
                    if (ready)
                        readyPhysicalNeighborCount++;
                }
                bool physicalEnvelopeReady = readyPhysicalNeighborCount == continuationProbePositions.Length;
                if (physicalEnvelopeReady)
                {
                if (edgeAttempt.x <= verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x)
                    throw new InvalidOperationException("player-following terrain did not continue beyond the authored terrain tile");
                if (extension == null || extension == verificationTerrain || extension.GetComponent<TerrainCollider>() == null || !extension.GetComponent<TerrainCollider>().enabled)
                    throw new InvalidOperationException("extended chunk has no enabled terrain collider");
                // note: Confirm the streamed TerrainData contract before comparing samples; a matching edge on incompatible resolutions is still an invalid continuation.
                float originSpacing = Mathf.Max(
                    verificationTerrain.terrainData.size.x / Mathf.Max(1f, verificationTerrain.terrainData.heightmapResolution - 1f),
                    verificationTerrain.terrainData.size.z / Mathf.Max(1f, verificationTerrain.terrainData.heightmapResolution - 1f));
                float extensionSpacing = Mathf.Max(
                    extension.terrainData.size.x / Mathf.Max(1f, extension.terrainData.heightmapResolution - 1f),
                    extension.terrainData.size.z / Mathf.Max(1f, extension.terrainData.heightmapResolution - 1f));
                if (Mathf.Abs(extension.terrainData.size.x - streamer.chunkWorldSize) > 0.01f ||
                    Mathf.Abs(extension.terrainData.size.z - streamer.chunkWorldSize) > 0.01f ||
                    Mathf.Abs(extensionSpacing - originSpacing) > 0.01f)
                    throw new InvalidOperationException("extended chunk TerrainData is not using the origin-compatible spacing and cell size");
                float maximumSharedEdgeDelta = 0f;
                float sharedX = verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x;
                float overlapMinZ = Mathf.Max(verificationTerrain.transform.position.z, extension.transform.position.z);
                float overlapMaxZ = Mathf.Min(verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z, extension.transform.position.z + extension.terrainData.size.z);
                for (int sampleZ = 0; sampleZ <= 16; sampleZ++)
                {
                    float worldZ = Mathf.Lerp(overlapMinZ, overlapMaxZ, sampleZ / 16f);
                    float originHeight = verificationTerrain.SampleHeight(new Vector3(sharedX, 0f, worldZ));
                    float extensionHeight = extension.SampleHeight(new Vector3(sharedX, 0f, worldZ));
                    maximumSharedEdgeDelta = Mathf.Max(maximumSharedEdgeDelta, Mathf.Abs(originHeight - extensionHeight));
                }
                report.AppendLine("- eastSharedEdgeMaximumDelta: " + maximumSharedEdgeDelta.ToString("0.000"));
                if (maximumSharedEdgeDelta > 0.25f)
                {
                    // note: Record the canonical and continuation transforms plus the seam samples before rejecting a terrain mismatch.
                    Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Seam detail canonical=" + verificationTerrain.transform.position + " size=" + verificationTerrain.terrainData.size + " continuation=" + extension.transform.position + " size=" + extension.terrainData.size + " sharedX=" + sharedX + " originY=" + verificationTerrain.SampleHeight(new Vector3(sharedX, 0f, overlapMinZ)) + " extensionY=" + extension.SampleHeight(new Vector3(sharedX, 0f, overlapMinZ)));
                    throw new InvalidOperationException("origin-to-continuation shared edge diverged by " + maximumSharedEdgeDelta.ToString("0.000") + " metres");
                }
                Physics.SyncTransforms();
                if (!Physics.Raycast(new Vector3(edgeAttempt.x, 300f, edgeAttempt.z), Vector3.down, out RaycastHit edgeHit, 600f))
                {
                    TerrainCollider extensionCollider = extension.GetComponent<TerrainCollider>();
                    Vector3 extensionOrigin = extension.transform.position;
                    Vector3 extensionSize = extension.terrainData.size;
                    float sampledProbeHeight = extension.SampleHeight(edgeAttempt);
                    RaycastHit directColliderRaycast = default;
                    bool directColliderHit = extensionCollider != null && extensionCollider.Raycast(
                        new Ray(new Vector3(player.position.x, 300f, player.position.z), Vector3.down),
                        out directColliderRaycast,
                        600f);
                    report.AppendLine("- edgeProbeFailurePosition: " + edgeAttempt);
                    report.AppendLine("- edgeProbeTerrainBounds: " + extensionOrigin + " size=" + extensionSize);
                    report.AppendLine("- edgeProbeSampleHeight: " + sampledProbeHeight.ToString("0.000", CultureInfo.InvariantCulture));
                    report.AppendLine("- edgeProbeColliderState: enabled=" + (extensionCollider != null && extensionCollider.enabled) + " active=" + extension.gameObject.activeInHierarchy + " layer=" + extension.gameObject.layer);
                    report.AppendLine("- edgeProbeColliderBounds: " + (extensionCollider != null ? extensionCollider.bounds.ToString() : "<null>"));
                    report.AppendLine("- edgeProbeColliderDataMatch: " + (extensionCollider != null && extensionCollider.terrainData == extension.terrainData));
                    report.AppendLine("- edgeProbeDirectColliderHit: " + directColliderHit + (directColliderHit ? " y=" + directColliderRaycast.point.y.ToString("0.000", CultureInfo.InvariantCulture) : string.Empty));
                    throw new InvalidOperationException("extended chunk terrain did not produce a raycastable ground surface");
                }
                float extensionMinimumHeight = float.MaxValue;
                float extensionMaximumHeight = float.MinValue;
                for (int sampleZ = 0; sampleZ <= 4; sampleZ++)
                {
                    for (int sampleX = 0; sampleX <= 4; sampleX++)
                    {
                        float sampleXWorld = extension.transform.position.x + extension.terrainData.size.x * sampleX / 4f;
                        float sampleZWorld = extension.transform.position.z + extension.terrainData.size.z * sampleZ / 4f;
                        float height = extension.SampleHeight(new Vector3(sampleXWorld, 0f, sampleZWorld));
                        extensionMinimumHeight = Mathf.Min(extensionMinimumHeight, height);
                        extensionMaximumHeight = Mathf.Max(extensionMaximumHeight, height);
                    }
                }
                report.AppendLine("- sampledExtensionHeightRange: " + extensionMinimumHeight.ToString("0.000") + ".." + extensionMaximumHeight.ToString("0.000"));
                if (extensionMaximumHeight - extensionMinimumHeight < 0.05f)
                    throw new InvalidOperationException("continuation terrain was effectively flat across the sampled extension tile");
                report.AppendLine("- extendedTerrainPositionX: " + edgeAttempt.x.ToString("0.00"));
                report.AppendLine("- extendedTerrainCollider: enabled");
                report.AppendLine("- extendedTerrainRaycastY: " + edgeHit.point.y.ToString("0.00"));
                report.AppendLine("- extendedTerrainHeightRange: " + extensionMinimumHeight.ToString("0.00") + ".." + extensionMaximumHeight.ToString("0.00"));
                if (extension.terrainData.terrainLayers == null || extension.terrainData.terrainLayers.Length < 3)
                    throw new InvalidOperationException("extended terrain did not inherit the required biome layers");
                // note: Inspect only an already traversable extension; the continuation probe must never wait for paint or create background demand.
                if (streamer.IsTerrainPaintPending(extension))
                    throw new InvalidOperationException("published continuation Terrain still has a pending appearance pass");
                float[,,] alphaSample = extension.terrainData.GetAlphamaps(0, 0, 1, 1);
                float alphaTotal = 0f;
                for (int layer = 0; layer < alphaSample.GetLength(2); layer++)
                {
                    float value = alphaSample[0, 0, layer];
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        throw new InvalidOperationException("extended terrain alphamap contains a non-finite value");
                    alphaTotal += value;
                }
                report.AppendLine("- extendedTerrainAlphaSampleTotal: " + alphaTotal.ToString("0.000"));
                // note: Unity stores terrain alphamaps in quantized texture channels, so the readback can drift a few percent while remaining correctly normalized for rendering.
                if (alphaTotal < 0.97f || alphaTotal > 1.03f)
                    throw new InvalidOperationException("extended terrain alphamap is not normalized");
                if (!TrySampleAlphaBoundaryDelta(verificationTerrain, extension, out float alphaBoundaryDelta))
                    throw new InvalidOperationException("published continuation Terrain had no readable biome boundary sample");
                report.AppendLine("- originExtensionAlphaBoundaryMaximumDelta: " + alphaBoundaryDelta.ToString("0.000", CultureInfo.InvariantCulture));
                if (alphaBoundaryDelta > 0.08f)
                {
                    // note: Preserve both edge texels and layer identities when a seam fails so an authored normalization or layer-order mismatch is distinguishable from a stale paint coroutine.
                    report.AppendLine("- alphaBoundaryDiagnostics: " + DescribeAlphaBoundary(verificationTerrain, extension));
                    throw new InvalidOperationException("origin and extension biome alphamaps diverged at their shared edge");
                }
                string[] remainingEdges = { "west", "south", "north" };
                for (int edgeIndex = 0; edgeIndex < remainingEdges.Length; edgeIndex++)
                {
                    string edge = remainingEdges[edgeIndex];
                    Vector3 remainingEdgeAttempt = startingPosition;
                    if (edge == "west")
                        remainingEdgeAttempt.x = verificationTerrain.transform.position.x - streamer.chunkWorldSize * 0.5f - 0.1f;
                    else if (edge == "south")
                        remainingEdgeAttempt.z = verificationTerrain.transform.position.z - streamer.chunkWorldSize * 0.5f - 0.1f;
                    else
                        remainingEdgeAttempt.z = verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z + streamer.chunkWorldSize * 0.5f + 0.1f;
                    Terrain edgeExtension = FindTerrainAt(remainingEdgeAttempt);
                    if (edgeExtension == null || edgeExtension == verificationTerrain)
                        throw new InvalidOperationException("streamed continuation terrain was not published after crossing the " + edge + " authored edge");
                    if (edgeExtension == null || edgeExtension == verificationTerrain || edgeExtension.GetComponent<TerrainCollider>() == null || !edgeExtension.GetComponent<TerrainCollider>().enabled)
                        throw new InvalidOperationException("" + edge + " continuation has no enabled terrain collider");
                    float edgeDelta = SampleSharedEdgeDelta(verificationTerrain, edgeExtension, edge);
                    report.AppendLine("- " + edge + "SharedEdgeMaximumDelta: " + edgeDelta.ToString("0.000"));
                    if (edgeDelta > 0.25f)
                        throw new InvalidOperationException(edge + " origin-to-continuation shared edge diverged by " + edgeDelta.ToString("0.000") + " metres");
                }

                // note: Exercise the first diagonal continuation explicitly; corner-only contact must use the world-space authority instead of copying an origin edge across the tile.
                Vector3 diagonalAttempt = startingPosition;
                diagonalAttempt.x = verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x + streamer.chunkWorldSize * 0.5f;
                diagonalAttempt.z = verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z + streamer.chunkWorldSize * 0.5f;
                Terrain diagonal = FindTerrainAt(diagonalAttempt);
                Terrain eastCandidate = FindTerrainAt(
                    new Vector3(diagonalAttempt.x, 0f, verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z - streamer.chunkWorldSize * 0.5f));
                Terrain northCandidate = FindTerrainAt(
                    new Vector3(verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x - streamer.chunkWorldSize * 0.5f, 0f, diagonalAttempt.z));
                if (diagonal == null || diagonal == verificationTerrain ||
                    eastCandidate == null || eastCandidate == verificationTerrain ||
                    northCandidate == null || northCandidate == verificationTerrain ||
                    diagonal.GetComponent<TerrainCollider>() == null || !diagonal.GetComponent<TerrainCollider>().enabled ||
                    eastCandidate.GetComponent<TerrainCollider>() == null || !eastCandidate.GetComponent<TerrainCollider>().enabled ||
                    northCandidate.GetComponent<TerrainCollider>() == null || !northCandidate.GetComponent<TerrainCollider>().enabled)
                    throw new InvalidOperationException("diagonal continuation terrain was not published after crossing the authored corner");
                if (diagonal == null || diagonal == verificationTerrain || diagonal.GetComponent<TerrainCollider>() == null || !diagonal.GetComponent<TerrainCollider>().enabled)
                    throw new InvalidOperationException("diagonal continuation has no enabled terrain collider");
                Vector3 originCorner = new Vector3(
                    verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x,
                    0f,
                    verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z);
                Terrain eastContinuation = FindTerrainAt(originCorner + new Vector3(halfCell, 0f, -halfCell));
                Terrain northContinuation = FindTerrainAt(originCorner + new Vector3(-halfCell, 0f, halfCell));
                if (eastContinuation == null || northContinuation == null)
                    throw new InvalidOperationException("diagonal seam probe could not resolve both cardinal continuation tiles");
                float eastCornerHeight = eastContinuation.SampleHeight(originCorner);
                float northCornerHeight = northContinuation.SampleHeight(originCorner);
                float cardinalCornerDelta = Mathf.Abs(eastCornerHeight - northCornerHeight);
                float diagonalCornerHeight = diagonal.SampleHeight(originCorner);
                float diagonalCornerDelta = Mathf.Max(
                    Mathf.Abs(diagonalCornerHeight - eastCornerHeight),
                    Mathf.Abs(diagonalCornerHeight - northCornerHeight));
                float maximumCornerDelta = Mathf.Max(cardinalCornerDelta, diagonalCornerDelta);
                report.AppendLine("- diagonalCornerMaximumDelta: " + maximumCornerDelta.ToString("0.000"));
                if (maximumCornerDelta > 0.25f)
                    throw new InvalidOperationException("diagonal continuation corner diverged by " + maximumCornerDelta.ToString("0.000") + " metres");
                report.AppendLine("- diagonalContinuation: PASS");
                }
                else
                {
                    if (!TryValidateCanonicalContinuationBoundarySamples(
                            streamer,
                            biomeAuthority,
                            verificationTerrain,
                            continuationProbePositions,
                            out int sampleCount,
                            out int demandedWorkContracts,
                            out string continuationFailure))
                        throw new InvalidOperationException("unmaterialized continuation authority failed: " + continuationFailure);
                    report.AppendLine("- originEdgePhysicalEnvelope: NOT_FULLY_PUBLISHED readyPhysicalNeighbors=" +
                        readyPhysicalNeighborCount + "/" + continuationProbePositions.Length);
                    report.AppendLine("- canonicalContinuationBoundarySamples: PASS samples=" + sampleCount +
                        " demandedWorkContracts=" + demandedWorkContracts);
                }
            }

            // note: Capture the initial continuation Terrain envelope before real motor travel can legitimately unload distant origin-edge owners.
            bool recoveryInputDeliveryInconclusive = false;
            yield return VerifyPublicationRecoveryMatrix(
                streamer,
                motor,
                player,
                characterController,
                report,
                (value, inputInconclusive, unloadPassed, unloadInputInconclusive) =>
                {
                    recoveryCasesVerified = value;
                    recoveryInputDeliveryInconclusive = inputInconclusive;
                    unloadRevisitPassed = unloadPassed;
                    unloadRevisitInputInconclusive = unloadInputInconclusive;
                });
            verificationInconclusive |= productionMotorInputInconclusive ||
                recoveryInputDeliveryInconclusive || unloadRevisitInputInconclusive;
            report.AppendLine("- r1PublicationRecoveryGate: " +
                (!recoveryCasesVerified ? "FAIL" : recoveryInputDeliveryInconclusive ? "INCONCLUSIVE" : "PASS"));
            WriteReport(report, false, false);
            if (!recoveryCasesVerified)
                throw new InvalidOperationException("R1 publication/recovery gates failed; inspect their individual gate rows");

            // note: Keep the R1 probe on the production motor path; synthetic teleports cannot prove continuous traversal or bounded publication.
            report.AppendLine("- productionMotorCoverage: OBSERVED_ONLY");
            report.AppendLine("- nearChunk: " + streamer.CurrentChunk);
            report.AppendLine("- nearSemanticCount: " + testWorld.generatedWorldPlan.semanticChunks.Count);
            int typedSiteCount = 0;
            for (int recordIndex = 0; recordIndex < testWorld.generatedWorldPlan.semanticChunks.Count; recordIndex++)
                typedSiteCount += testWorld.generatedWorldPlan.semanticChunks[recordIndex]?.sites?.Count ?? 0;
            report.AppendLine("- typedContinuationSiteCount: " + typedSiteCount);
            if (typedSiteCount < 2)
                throw new InvalidOperationException("multi-cell continuation did not produce intentionally distributed typed sites");
            report.AppendLine("- syntheticTeleportTraversal: NOT_RUN_PRODUCTION_MOTOR_REQUIRED");

            // note: R1 does not synthesize a terrain-edge start or ground correction; the production motor matrix above is the only locomotion subject.
            float normalSpeed = motor != null ? Mathf.Max(1f, motor.walkSpeed) : 6.8f;
            // note: Normal locomotion is already measured through the enabled production motor matrix; do not duplicate it with a verifier-owned CharacterController.Move loop.
            report.AppendLine("- normalTraversal: OBSERVED_PRODUCTION_MOTOR_MATRIX speed=" +
                normalSpeed.ToString("0.0", CultureInfo.InvariantCulture));
            WriteReport(report, false, false);

            // note: Accept the 260 m/s clause only from measured production-motor input segments; the remaining G07 speed thresholds stay separate.
            report.AppendLine("- speed260ConstantInputSegmentsGate: " + (productionMotorInputInconclusive
                ? "INPUT_DELIVERY_INCONCLUSIVE"
                : productionMotorMatrixPassed ? "PASS_PRODUCTION_MOTOR" : "FAIL_PRODUCTION_MOTOR"));
            report.AppendLine("- speed150StraightGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            report.AppendLine("- speed300StraightGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            report.AppendLine("- speed300DirectionChangeGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            report.AppendLine("- speed150DiagonalGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            report.AppendLine("- speed300DeadlineGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            report.AppendLine("- streamingFrameBudgetGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            WriteReport(report, false, false);

            // note: Keep higher-speed diagonal/300 m/s qualification separate; the focused request here is the sudden 260 m/s straight traversal.
            WriteReport(report, false, false);

            // note: Persist the live stream timings collected while the authoritative motor matrix and initial visual envelope were active.
            report.AppendLine("- terrainBuildLastSeconds: " + streamer.LastTerrainBuildSeconds.ToString("0.000"));
            report.AppendLine("- terrainBuildMaximumSeconds: " + streamer.MaximumTerrainBuildSeconds.ToString("0.000"));
            report.AppendLine("- terrainPaintLastSeconds: " + streamer.LastTerrainPaintSeconds.ToString("0.000"));
            report.AppendLine("- terrainPaintMaximumSeconds: " + streamer.MaximumTerrainPaintSeconds.ToString("0.000"));
            // note: Report both the final rolling window and run-wide production during motor-active seconds so idle cleanup cannot mask sustained output.
            report.AppendLine("- terrainReadyCellsPerSecondLastWindow: " + streamer.TerrainReadyCellsPerSecond.ToString("0.00"));
            report.AppendLine("- terrainReadyCellsPerSecondAverageWhileMoving: " + streamer.TerrainReadyAverageCellsPerSecond.ToString("0.00"));
            report.AppendLine("- terrainReadyCellsPerSecondMaximumWhileMoving: " + streamer.TerrainReadyMaximumCellsPerSecond.ToString("0.00"));
            report.AppendLine("- terrainReadyForwardCellCount: " + streamer.TerrainReadyForwardCellCount);
            report.AppendLine("- maximumTerrainQueueDepth: " + streamer.MaximumTerrainQueueDepth);
            report.AppendLine("- maximumConcurrentTerrainPreparations: " + streamer.MaximumConcurrentTerrainPreparations);
            report.AppendLine("- duplicateTerrainRequests: " + streamer.DuplicateTerrainRequestCount);
            report.AppendLine("- missedTerrainDeadlines: " + streamer.MissedTerrainDeadlineCount);
            report.AppendLine("- minimumForwardReadyLeadCells: " + streamer.MinimumForwardReadyLeadCells);
            report.AppendLine("- lastForwardReadyLeadCells: " + streamer.LastForwardReadyLeadCells);
            // note: Capture the measured synchronous streamer slice so the acceptance report exposes the worst normal update cost after warmup.
            report.AppendLine("- streamingWorkLastSeconds: " + streamer.LastStreamingWorkSeconds.ToString("0.000"));
            report.AppendLine("- streamingWorkMaximumSeconds: " + streamer.MaximumStreamingWorkSeconds.ToString("0.000"));
            // note: Coroutine slice timings include resumed terrain and content stages that do not execute inside Update itself.
            report.AppendLine("- terrainSliceMaximumSeconds: " + streamer.MaximumTerrainSliceSeconds.ToString("0.000"));
            report.AppendLine("- terrainSliceMaximumPhase: " + streamer.MaximumTerrainSlicePhase);
            report.AppendLine("- contentSliceLastSeconds: " + streamer.LastContentSliceSeconds.ToString("0.000"));
            report.AppendLine("- contentSliceMaximumSeconds: " + streamer.MaximumContentSliceSeconds.ToString("0.000"));
            report.AppendLine("- terrainPaintSliceMaximumSeconds: " + streamer.MaximumTerrainPaintSliceSeconds.ToString("0.000"));
            report.AppendLine("- streamedBiomeComputeMaximumSeconds: " + YQGeneratedWorldEnvironment.MaximumStreamedBiomeComputeSeconds.ToString("0.000"));
            report.AppendLine("- streamedBiomeUploadMaximumSeconds: " + YQGeneratedWorldEnvironment.MaximumStreamedBiomeUploadSeconds.ToString("0.000"));
            report.AppendLine("- streamedDetailNormalMaximumSeconds: " + YQGeneratedWorldEnvironment.MaximumStreamedDetailNormalSeconds.ToString("0.000"));
            report.AppendLine("- streamedDetailComputeMaximumSeconds: " + YQGeneratedWorldEnvironment.MaximumStreamedDetailComputeSeconds.ToString("0.000"));
            report.AppendLine("- streamedDetailUploadMaximumSeconds: " + YQGeneratedWorldEnvironment.MaximumStreamedDetailUploadSeconds.ToString("0.000"));
            // note: Keep aggregate and indivisible publication high-water marks in the receipt so a small inner-slice maximum cannot hide a combined stall.
            report.AppendLine("- maximumAggregateFrameWorkSeconds: " + streamer.MaximumAggregateFrameWorkSeconds.ToString("0.000"));
            report.AppendLine("- maximumAggregateFrameWorkStage: " + streamer.MaximumAggregateFrameWorkStage);
            report.AppendLine("- maximumPublicationSliceSeconds: " + streamer.MaximumPublicationSliceSeconds.ToString("0.000"));
            report.AppendLine("- maximumPublicationSliceStage: " + streamer.MaximumPublicationSliceStage);
            report.AppendLine("- maximumMaterializationSubstageSeconds: " + streamer.MaximumMaterializationSubstageSeconds.ToString("0.000"));
            report.AppendLine("- maximumMaterializationSubstage: " + streamer.MaximumMaterializationSubstage);
            // note: Keep per-frame terrain/content/frontier work separate from the deferred full-world persistence transaction.
            float maximumSynchronousFrontierWork = streamer.MaximumStreamingWorkSeconds;
            float maximumRuntimeStreamingSlice = Mathf.Max(
                streamer.MaximumTerrainSliceSeconds,
                streamer.MaximumTerrainPaintSliceSeconds,
                streamer.MaximumContentSliceSeconds);
            maximumRuntimeStreamingSlice = Mathf.Max(maximumRuntimeStreamingSlice, maximumSynchronousFrontierWork);
            report.AppendLine("- semanticSaveMaximumSeconds: " + streamer.MaximumSemanticSaveSeconds.ToString("0.000"));
            report.AppendLine("- persistenceCommitBudgetSeconds: " + PersistenceCommitBudgetSeconds.ToString("0.000"));
            report.AppendLine("- streamingMeasurementToleranceSeconds: " + StreamingMeasurementToleranceSeconds.ToString("0.000"));
            report.AppendLine("- streamingFrameBudgetSeconds: " + YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds.ToString("0.000"));
            report.AppendLine("- maximumMeasuredStreamingSliceSeconds: " + maximumRuntimeStreamingSlice.ToString("0.000"));
            report.AppendLine("- maximumSynchronousFrontierWorkSeconds: " + maximumSynchronousFrontierWork.ToString("0.000"));
            report.AppendLine("- maximumStreamingWorkStage: " + streamer.MaximumStreamingStage + " (" + streamer.MaximumStreamingStageSeconds.ToString("0.000", CultureInfo.InvariantCulture) + ")");
            if (EnforceG07ThroughputGates && maximumRuntimeStreamingSlice > YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds + StreamingMeasurementToleranceSeconds)
            {
                // note: Keep the measured hitch visible while allowing persistence verification to prove whether the state boundary itself is sound.
                report.AppendLine("- fullAcceptanceFrameBudgetDiagnostic: DEFERRED_G07: " + maximumRuntimeStreamingSlice.ToString("0.000"));
            }
            if (streamer.MaximumSemanticSaveSeconds > PersistenceCommitBudgetSeconds)
            {
                // note: Keep the separately scoped G02 timing visible without making it fail this R1 publication and recovery matrix.
                report.AppendLine("- persistenceCommitBudgetGate: DEFERRED_G02: " + streamer.MaximumSemanticSaveSeconds.ToString("0.000"));
            }

            // note: Check detached semantic serialization without mutating accepted overlays, saving the live profile, or replacing production owners.
            GeneratedWorldPlanRecord observedPlan = manager.State.generatedWorldPlan;
            string observedSignature = BuildChunkSignature(observedPlan);
            GeneratedWorldPlanRecord roundTrippedPlan = JsonUtility.FromJson<GeneratedWorldPlanRecord>(JsonUtility.ToJson(observedPlan));
            if (roundTrippedPlan == null || roundTrippedPlan.semanticChunks == null ||
                observedPlan.semanticChunks.Count != roundTrippedPlan.semanticChunks.Count ||
                !string.Equals(observedSignature, BuildChunkSignature(roundTrippedPlan), StringComparison.Ordinal))
                throw new InvalidOperationException("semantic chunk records changed across detached serialization");
            report.AppendLine("- semanticPlanSerializationRoundTrip: PASS serializer=UnityJson semanticCount=" + roundTrippedPlan.semanticChunks.Count);
            report.AppendLine("- liveFeatureOverlayMutation: NOT_RUN_R1_STATE_PRESERVATION");
            report.AppendLine("- liveSaveReloadManualRebind: NOT_RUN_R1_STATE_PRESERVATION; runtime rematerialization is covered by unload/revisit");
            WriteReport(report, false, false);
            if (postMotorPublicationWorkContractFailed)
                throw new InvalidOperationException("post-movement visible publication contains a stranded stage: " + postMotorPublicationWorkContractFailure);
            // note: Recheck all current demand after recovery and real traversal; an initial snapshot cannot certify the final publication owners.
            bool finalPublicationWorkPassed = streamer.TryValidateDemandedPublicationWorkContracts(
                out int finalDemandedChunks, out string finalPublicationWorkFailure);
            report.AppendLine("- finalDemandedPublicationWorkContract: " + (finalPublicationWorkPassed ? "PASS" : "FAIL") +
                " demandedChunks=" + finalDemandedChunks +
                (finalPublicationWorkPassed ? string.Empty : " failure=" + finalPublicationWorkFailure));
            WriteReport(report, false, false);
            if (!finalPublicationWorkPassed)
                throw new InvalidOperationException("final demanded publication contains a stranded stage: " + finalPublicationWorkFailure);
            if (!productionMotorMatrixPassed && !productionMotorInputInconclusive)
                throw new InvalidOperationException("production motor traversal matrix failed; inspect the per-gate movement and frame evidence above");
            if (!unloadRevisitPassed && !unloadRevisitInputInconclusive)
                throw new InvalidOperationException("R2 unload/revisit gate failed; inspect the candidate, unload, and republish rows above");
            report.AppendLine("- result: " + (verificationInconclusive ? "INCONCLUSIVE" : "PASS"));
            success = true;
        }
        finally
        {
            if (!success)
            {
                // note: A failure line turns an unattended report into a repair target while retaining the raw Unity console stack trace.
                if (!string.IsNullOrWhiteSpace(_lastNestedFailure))
                    report.AppendLine("- failure: " + _lastNestedFailure.Replace('\r', ' ').Replace('\n', ' '));
                report.AppendLine("- result: FAIL");
            }
            WriteReport(report, success, inconclusive: verificationInconclusive);
            ReleaseVerificationKeyboardHook();
            _lastNestedFailure = string.Empty;
            _started = false;
            _completed = true;
        }
    }

    private static IEnumerator VerifyProductionMotorTraversal(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        YQInvestorPlayerMotor motor,
        Transform player,
        CharacterController characterController,
        StringBuilder report,
        Action<bool, bool> result)
    {
        // note: Drive the existing production input and motor update path through the editor's real keyboard device so normal movement is not replaced by verifier velocity.
        UnityEngine.InputSystem.Keyboard keyboard = null;
        bool walkObserved = false;
        bool sprintObserved = false;
        bool dashActivationObserved = false;
        bool reversalObserved = false;
        bool walkEligibilityObserved = false;
        bool sprintEligibilityObserved = false;
        bool dashEligibilityObserved = false;
        bool reversalEligibilityObserved = false;
        bool walkGroundedObserved = false;
        bool sprintGroundedObserved = false;
        bool dashGroundedObserved = false;
        bool reversalGroundedObserved = false;
        int walkFrames = 0;
        int sprintFrames = 0;
        int dashFrames = 0;
        int reversalFrames = 0;
        float originalWalkSpeed = motor.walkSpeed;
        float originalSprintSpeed = motor.sprintSpeed;
        float originalAcceleration = motor.acceleration;
        float originalDeceleration = motor.deceleration;
        ProfilerRecorder mainThreadTimeRecorder = default;
        double deadline = Time.realtimeSinceStartupAsDouble + 20.0;
        try
        {
            // note: Use one named virtual keyboard for the probe so a physical/background keyboard cannot replace the device read by the production motor.
            keyboard = AcquireVerificationKeyboard();
            if (keyboard == null)
                throw new InvalidOperationException("production motor verification could not acquire a keyboard device");
            keyboard.MakeCurrent();
            // note: Record the live production eligibility and grounding state without waiting or correcting the fixture; the matrix must expose a bad startup state.
            report.AppendLine("- productionMotorEligibility: " + motor.CanProcessMovementInput +
                ", groundedAtInputStart=" + motor.IsGrounded +
                ", moveInput=" + motor.MoveInput.ToString("0.000", CultureInfo.InvariantCulture));
            WriteReport(report, false, false);
            // note: Prove that the queued event was consumed by a real Input System update before attributing a zero-distance result to the production motor.
            uint inputUpdateBeforeAcknowledgement = UnityEngine.InputSystem.LowLevel.InputState.updateCount;
            QueueKeyboardState(
                keyboard,
                new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
            bool inputDeliveryAcknowledged = false;
            double inputAcknowledgementDeadline = Time.realtimeSinceStartupAsDouble + 0.25;
            while (Time.realtimeSinceStartupAsDouble < inputAcknowledgementDeadline)
            {
                yield return null;
                UnityEngine.InputSystem.Keyboard currentKeyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard.added && keyboard.enabled &&
                    UnityEngine.InputSystem.LowLevel.InputState.updateCount != inputUpdateBeforeAcknowledgement &&
                    currentKeyboard == keyboard && keyboard.wKey.isPressed)
                {
                    inputDeliveryAcknowledged = true;
                    break;
                }
            }
            report.AppendLine("- productionInputDelivery: " +
                (inputDeliveryAcknowledged ? "ACK" : "INPUT_DELIVERY_INCONCLUSIVE") +
                ",updateBefore=" + inputUpdateBeforeAcknowledgement +
                ",updateAfter=" + UnityEngine.InputSystem.LowLevel.InputState.updateCount +
                ",deviceAdded=" + keyboard.added +
                ",deviceEnabled=" + keyboard.enabled +
                ",deviceCurrent=" + (UnityEngine.InputSystem.Keyboard.current == keyboard) +
                ",applicationFocused=" + Application.isFocused);
            AppendInjectedKeyboardDiagnostics(report, keyboard, "acknowledgement");
            WriteReport(report, false, false);
            if (!inputDeliveryAcknowledged)
            {
                // note: Do not report a false locomotion failure when the harness cannot demonstrate delivery through Unity's normal input scheduler.
                result(false, true);
                yield break;
            }

            // note: Start with forward walk input so the real motor proves ordinary collision-supported movement before sprint and dash are requested.
            Vector3 walkStartPosition = player.position;
            double phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 1.25)
            {
                // note: Reassert the injected device before each production frame so Unity cannot restore a different current keyboard during title-to-gameplay handoff.
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
                yield return null;
                if (walkFrames == 0)
                    AppendInjectedKeyboardDiagnostics(report, keyboard, "walk");
                ValidateProductionMotorFrame(streamer, player, characterController, "walk", report);
                walkFrames++;
                walkEligibilityObserved |= motor.CanProcessMovementInput;
                walkGroundedObserved |= motor.IsGrounded;
                walkObserved |= motor.MoveInput.y > 0.1f && motor.IsGrounded;
            }
            float walkPlanarDistance = PlanarDistance(walkStartPosition, player.position);
            walkObserved &= walkPlanarDistance > 0.02f;
            report.AppendLine("- productionMotorWalkEvidence: eligible=" + walkEligibilityObserved +
                ", elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - phaseStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                ", grounded=" + walkGroundedObserved + ", planarDistance=" + walkPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", finalMoveInput=" + motor.MoveInput.ToString("0.000", CultureInfo.InvariantCulture));
            report.AppendLine("- productionMotorWalk: " + (walkObserved ? "PASS" : "FAIL"));
            // note: Persist each production-input phase immediately so an interrupted probe retains the last truthful observation.
            WriteReport(report, false, false);

            // note: Hold W plus the production sprint key so stamina, acceleration, CharacterController.Move, and sprint state all remain motor-owned.
            Vector3 sprintStartPosition = player.position;
            phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 1.75)
            {
                // note: Keep the real production keyboard owner current while sprint input crosses multiple Unity frames.
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(
                        UnityEngine.InputSystem.Key.W,
                        UnityEngine.InputSystem.Key.LeftShift));
                yield return null;
                ValidateProductionMotorFrame(streamer, player, characterController, "run", report);
                sprintFrames++;
                sprintEligibilityObserved |= motor.CanProcessMovementInput;
                sprintGroundedObserved |= motor.IsGrounded;
                sprintObserved |= motor.IsSprinting && motor.MoveInput.y > 0.1f && motor.IsGrounded;
            }
            float sprintPlanarDistance = PlanarDistance(sprintStartPosition, player.position);
            sprintObserved &= sprintPlanarDistance > 0.02f;
            report.AppendLine("- productionMotorRunEvidence: eligible=" + sprintEligibilityObserved +
                ", elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - phaseStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                ", grounded=" + sprintGroundedObserved + ", planarDistance=" + sprintPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", finalMoveInput=" + motor.MoveInput.ToString("0.000", CultureInfo.InvariantCulture));
            report.AppendLine("- productionMotorRun: " + (sprintObserved ? "PASS" : "FAIL"));
            // note: Flush the sprint result before dash setup can fail or the editor session can be stopped.
            WriteReport(report, false, false);

            // note: Establish a real key-up boundary before pulsing Q so the production motor observes the same wasPressedThisFrame transition as a player.
            // note: Do not refill stamina or otherwise repair the live actor between phases; dash must use the production resource state.
            QueueKeyboardState(
                keyboard,
                new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;
            // note: Dash away from the known Witch House frontage so authored collision tests dash movement instead of a blocked test lane.
            int dashSideContactsBefore = motor.BlockingControllerContactCount;
            Vector3 dashStartPosition = player.position;
            double dashInputStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - dashInputStarted < 0.10)
            {
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(
                        UnityEngine.InputSystem.Key.S,
                        UnityEngine.InputSystem.Key.Q));
                yield return null;
                if (dashFrames == 0)
                    AppendInjectedKeyboardDiagnostics(report, keyboard, "dash");
                ValidateProductionMotorFrame(streamer, player, characterController, "dash-start", report);
                dashFrames++;
                dashEligibilityObserved |= motor.CanProcessMovementInput;
                dashGroundedObserved |= motor.IsGrounded;
                dashActivationObserved |= motor.IsDashing || motor.DashStartedThisFrame;
            }
            // note: Release Q while retaining the same clear-lane direction so the production motor completes the dash without retriggering it.
            phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 0.55)
            {
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.S));
                yield return null;
                ValidateProductionMotorFrame(streamer, player, characterController, "dash-release", report);
                dashFrames++;
                dashEligibilityObserved |= motor.CanProcessMovementInput;
                dashGroundedObserved |= motor.IsGrounded;
                dashActivationObserved |= motor.IsDashing || motor.DashStartedThisFrame;
            }
            float dashPlanarDistance = PlanarDistance(dashStartPosition, player.position);
            bool dashObserved = dashActivationObserved && dashPlanarDistance > 0.02f;
            report.AppendLine("- productionMotorDashEvidence: eligible=" + dashEligibilityObserved +
                ", activated=" + dashActivationObserved +
                ", elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - dashInputStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                ", grounded=" + dashGroundedObserved + ", planarDistance=" + dashPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", finalMoveInput=" + motor.MoveInput.ToString("0.000", CultureInfo.InvariantCulture));
            AppendProductionMovementDiagnostics(report, motor, "dash", dashSideContactsBefore);
            report.AppendLine("- productionMotorDash: " + (dashObserved ? "PASS" : "FAIL"));
            // note: Flush the dash result before reversal input begins so the matrix remains auditable after partial execution.
            WriteReport(report, false, false);

            // note: Reverse through the same motor input contract so direction changes are proven independently of the high-speed diagnostic harness.
            Vector3 reversalStartPosition = player.position;
            phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 1.25)
            {
                // note: Reassert the injected device for reversal so the production motor cannot silently read a different keyboard after dash cleanup.
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.S));
                yield return null;
                ValidateProductionMotorFrame(streamer, player, characterController, "reversal", report);
                reversalFrames++;
                reversalEligibilityObserved |= motor.CanProcessMovementInput;
                reversalGroundedObserved |= motor.IsGrounded;
                reversalObserved |= motor.MoveInput.y < -0.1f && motor.IsGrounded;
            }
            float reversalPlanarDistance = PlanarDistance(reversalStartPosition, player.position);
            reversalObserved &= reversalPlanarDistance > 0.02f;
            report.AppendLine("- productionMotorReversalEvidence: eligible=" + reversalEligibilityObserved +
                ", elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - phaseStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                ", grounded=" + reversalGroundedObserved + ", planarDistance=" + reversalPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", finalMoveInput=" + motor.MoveInput.ToString("0.000", CultureInfo.InvariantCulture));
            report.AppendLine("- productionMotorReversal: " + (reversalObserved ? "PASS" : "FAIL"));
            // note: Exercise diagonal camera-relative movement through the same live keyboard and motor path, validating every resulting capsule position.
            int traversalBlocksBeforeDiagonal = streamer.TraversalConstraintBlockCount;
            Vector3 diagonalStartPosition = player.position;
            bool diagonalInputObserved = false;
            bool diagonalGroundedObserved = false;
            int diagonalFrames = 0;
            phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 0.85)
            {
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(
                        UnityEngine.InputSystem.Key.W,
                        UnityEngine.InputSystem.Key.D));
                yield return null;
                ValidateProductionMotorFrame(streamer, player, characterController, "diagonal", report);
                diagonalFrames++;
                diagonalInputObserved |= motor.MoveInput.x > 0.1f && motor.MoveInput.y > 0.1f;
                diagonalGroundedObserved |= motor.IsGrounded;
            }
            float diagonalPlanarDistance = PlanarDistance(diagonalStartPosition, player.position);
            int diagonalTraversalBlocks = streamer.TraversalConstraintBlockCount - traversalBlocksBeforeDiagonal;
            bool diagonalObserved = diagonalInputObserved && diagonalGroundedObserved &&
                diagonalPlanarDistance > 0.02f && diagonalTraversalBlocks == 0;
            report.AppendLine("- productionMotorDiagonalEvidence: inputObserved=" + diagonalInputObserved +
                ", grounded=" + diagonalGroundedObserved + ", planarDistance=" + diagonalPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", traversalBlocks=" + diagonalTraversalBlocks + ", frames=" + diagonalFrames);
            report.AppendLine("- productionMotorDiagonal: " + (diagonalObserved ? "PASS" : "FAIL"));
            WriteReport(report, false, false);

            // note: Rapidly rotate through all four diagonal quadrants on the authoritative motor to catch turn-envelope gaps and swept-capsule corner leaks.
            int traversalBlocksBeforeTurns = streamer.TraversalConstraintBlockCount;
            Vector3 rapidTurnStartPosition = player.position;
            int rapidTurnFrames = 0;
            int rapidTurnInputsObserved = 0;
            bool rapidTurnGroundedObserved = false;
            for (int turn = 0; turn < 4; turn++)
            {
                UnityEngine.InputSystem.Key forwardKey = turn < 2
                    ? UnityEngine.InputSystem.Key.W
                    : UnityEngine.InputSystem.Key.S;
                UnityEngine.InputSystem.Key strafeKey = turn == 0 || turn == 3
                    ? UnityEngine.InputSystem.Key.D
                    : UnityEngine.InputSystem.Key.A;
                float expectedXSign = strafeKey == UnityEngine.InputSystem.Key.D ? 1f : -1f;
                float expectedYSign = forwardKey == UnityEngine.InputSystem.Key.W ? 1f : -1f;
                bool turnInputObserved = false;
                phaseStarted = Time.realtimeSinceStartupAsDouble;
                while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 0.35)
                {
                    QueueKeyboardState(
                        keyboard,
                        new UnityEngine.InputSystem.LowLevel.KeyboardState(forwardKey, strafeKey));
                    yield return null;
                    ValidateProductionMotorFrame(streamer, player, characterController, "rapid-turn-" + turn, report);
                    rapidTurnFrames++;
                    turnInputObserved |= motor.MoveInput.x * expectedXSign > 0.1f && motor.MoveInput.y * expectedYSign > 0.1f;
                    rapidTurnGroundedObserved |= motor.IsGrounded;
                }
                if (turnInputObserved)
                    rapidTurnInputsObserved++;
            }
            float rapidTurnPlanarDistance = PlanarDistance(rapidTurnStartPosition, player.position);
            int rapidTurnTraversalBlocks = streamer.TraversalConstraintBlockCount - traversalBlocksBeforeTurns;
            bool rapidTurnsObserved = rapidTurnInputsObserved == 4 && rapidTurnGroundedObserved &&
                rapidTurnPlanarDistance > 0.02f && rapidTurnTraversalBlocks == 0;
            report.AppendLine("- productionMotorRapidTurnsEvidence: inputTransitions=" + rapidTurnInputsObserved +
                "/4, grounded=" + rapidTurnGroundedObserved + ", planarDistance=" + rapidTurnPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", traversalBlocks=" + rapidTurnTraversalBlocks + ", frames=" + rapidTurnFrames);
            report.AppendLine("- productionMotorRapidTurns: " + (rapidTurnsObserved ? "PASS" : "FAIL"));

            // note: Stage on the flat authored origin pad with the production motor; this isolates streaming from the generated terrain rim outside the spawn plateau.
            int boundaryApproachFallbackAttemptsBefore = streamer.SynchronousGroundFallbackAttemptCount;
            int boundaryApproachFallbacksBefore = streamer.SynchronousGroundFallbackCount;
            int boundaryApproachTraversalBlocksBefore = streamer.TraversalConstraintBlockCount;
            // note: Stop far enough inside the source cell to absorb the last 6 m/s frame before the 260 m/s sample crosses the seam.
            float boundaryApproachTargetMeters = Mathf.Max(
                0.75f,
                Mathf.Max(1f, motor.walkSpeed) * Mathf.Max(Time.deltaTime, Time.unscaledDeltaTime) * 2f);
            motor.walkSpeed = 6f;
            motor.sprintSpeed = 6f;
            motor.acceleration = Mathf.Max(originalAcceleration, 1000000f);
            motor.deceleration = Mathf.Max(originalDeceleration, 1000000f);
            Vector3 origin = YQGeneratedWorldTerrain.OriginWorldPosition;
            // note: Stage at the east edge of the original flat-pad cell so the 260 m/s transition crosses the adjacent seam away from the waterfall cave.
            float probeChunkSize = Mathf.Max(32f, streamer.chunkWorldSize);
            Vector2Int initialProbeCell = ChunkCoordinateAt(
                new Vector3(origin.x + 2f, player.position.y, origin.z + 22f), probeChunkSize);
            float eastProbeBoundary = YQContinuousWorldFeatureAuthority.WorldGridOrigin +
                (initialProbeCell.x + 1) * probeChunkSize;
            float stagingInset = Mathf.Max(2f, boundaryApproachTargetMeters * 2f);
            Vector3 highSpeedStagingPoint = new Vector3(
                eastProbeBoundary - stagingInset,
                player.position.y,
                origin.z + 22f);
            double stagingStartedAt = Time.realtimeSinceStartupAsDouble;
            double stagingDeadline = Math.Min(deadline, stagingStartedAt + 25.0d);
            Vector3 stagingStartPosition = player.position;
            float stagingDistance = PlanarDistance(player.position, highSpeedStagingPoint);
            bool stagingInputObserved = stagingDistance <= 0.8f;
            int stagingFrames = 0;
            int stagingNoProgressFrames = 0;
            while (stagingDistance > 0.8f && Time.realtimeSinceStartupAsDouble < stagingDeadline &&
                stagingNoProgressFrames < 8)
            {
                Vector3 stagingDirection = highSpeedStagingPoint - player.position;
                stagingDirection.y = 0f;
                UnityEngine.InputSystem.LowLevel.KeyboardState stagingInput =
                    KeyboardStateForMotorDirection(motor, stagingDirection, out Vector2 expectedStagingInput);
                Vector3 stagingFrameStart = player.position;
                QueueKeyboardState(keyboard, stagingInput);
                yield return null;
                ValidateProductionMotorFrame(streamer, player, characterController, "speed-260-safe-staging", report);
                stagingFrames++;
                stagingInputObserved |= IsExpectedMotorInput(motor.MoveInput, expectedStagingInput);
                float nextStagingDistance = PlanarDistance(player.position, highSpeedStagingPoint);
                stagingNoProgressFrames = PlanarDistance(stagingFrameStart, player.position) <= 0.01f
                    ? stagingNoProgressFrames + 1
                    : 0;
                stagingDistance = nextStagingDistance;
            }
            bool stagingReady = stagingDistance <= 0.8f;
            report.AppendLine("- productionMotorSpeed260Staging: ready=" + stagingReady +
                ", inputObserved=" + stagingInputObserved +
                ", start=" + stagingStartPosition.ToString("F2", CultureInfo.InvariantCulture) +
                ", target=" + highSpeedStagingPoint.ToString("F2", CultureInfo.InvariantCulture) +
                ", final=" + player.position.ToString("F2", CultureInfo.InvariantCulture) +
                ", distanceMeters=" + stagingDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - stagingStartedAt).ToString("0.000", CultureInfo.InvariantCulture) +
                ", frames=" + stagingFrames);

            // note: Cross east from the clear origin-pad lane so only the adjacent publication seam, not the authored cave collider, limits the probe.
            Vector3 desiredBoundaryDirection = Vector3.right;
            UnityEngine.InputSystem.LowLevel.KeyboardState speedProbeInputState =
                KeyboardStateForMotorDirection(motor, desiredBoundaryDirection, out Vector2 speedProbeInputAxes);
            Vector3 boostTravelDirection = MotorWorldPlanarDirection(motor, speedProbeInputAxes);
            bool boundaryApproachInputObserved = stagingReady && stagingInputObserved;
            int boundaryApproachFrames = 0;
            float initialBoundaryGapMeters = float.PositiveInfinity;
            float finalBoundaryGapMeters = float.PositiveInfinity;
            Vector2Int boundaryApproachSourceCell = default;
            Vector2Int boundaryApproachTargetCell = default;
            Vector3 boundaryPoint = player.position;
            if (stagingReady && TryGetNextChunkBoundary(
                    player.position,
                    boostTravelDirection,
                    streamer.chunkWorldSize,
                    out initialBoundaryGapMeters,
                    out boundaryApproachSourceCell,
                    out boundaryApproachTargetCell))
            {
                boundaryPoint = player.position + boostTravelDirection * initialBoundaryGapMeters;
                double boundaryApproachBudgetSeconds = Math.Min(
                    8.0d,
                    Math.Max(2.0d, (initialBoundaryGapMeters - boundaryApproachTargetMeters) / Mathf.Max(1f, motor.walkSpeed) + 1.0d));
                double boundaryApproachDeadline = Math.Min(
                    deadline,
                    Time.realtimeSinceStartupAsDouble + boundaryApproachBudgetSeconds);
                while (Time.realtimeSinceStartupAsDouble < boundaryApproachDeadline &&
                    finalBoundaryGapMeters > boundaryApproachTargetMeters &&
                    ChunkCoordinateAt(player.position, streamer.chunkWorldSize) == boundaryApproachSourceCell)
                {
                    finalBoundaryGapMeters = Mathf.Abs(Vector3.Dot(boundaryPoint - player.position, boostTravelDirection));
                    if (finalBoundaryGapMeters <= boundaryApproachTargetMeters)
                        break;

                    // note: Let the production motor advance at the editor's real frame pace; boundary setup does not alter simulation timing or ground the player artificially.
                    QueueKeyboardState(keyboard, speedProbeInputState);
                    yield return null;
                    ValidateProductionMotorFrame(streamer, player, characterController, "speed-260-boundary-approach", report);
                    boundaryApproachFrames++;
                    boundaryApproachInputObserved |= IsExpectedMotorInput(motor.MoveInput, speedProbeInputAxes);
                }
                finalBoundaryGapMeters = Mathf.Abs(Vector3.Dot(boundaryPoint - player.position, boostTravelDirection));
            }
            Vector2Int boundaryApproachFinalCell = ChunkCoordinateAt(player.position, streamer.chunkWorldSize);
            bool boundaryApproachReady = stagingReady &&
                boundaryApproachFinalCell == boundaryApproachSourceCell &&
                finalBoundaryGapMeters <= boundaryApproachTargetMeters;
            int boundaryApproachFallbackAttempts =
                streamer.SynchronousGroundFallbackAttemptCount - boundaryApproachFallbackAttemptsBefore;
            int boundaryApproachFallbacks = streamer.SynchronousGroundFallbackCount - boundaryApproachFallbacksBefore;
            int boundaryApproachTraversalBlocks =
                streamer.TraversalConstraintBlockCount - boundaryApproachTraversalBlocksBefore;
            bool boundaryApproachClean = boundaryApproachFallbackAttempts == 0 && boundaryApproachTraversalBlocks == 0;
            report.AppendLine("- productionMotorSpeed260BoundaryApproach: ready=" + boundaryApproachReady +
                ", clean=" + boundaryApproachClean +
                ", inputObserved=" + boundaryApproachInputObserved +
                ", sourceCell=" + boundaryApproachSourceCell +
                ", targetCell=" + boundaryApproachTargetCell +
                ", finalCell=" + boundaryApproachFinalCell +
                ", inputAxes=" + speedProbeInputAxes.ToString("0.000", CultureInfo.InvariantCulture) +
                ", initialGapMeters=" + initialBoundaryGapMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                ", finalGapMeters=" + finalBoundaryGapMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                ", targetGapMeters=" + boundaryApproachTargetMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                ", fallbackAttempts=" + boundaryApproachFallbackAttempts +
                ", successfulFallbacks=" + boundaryApproachFallbacks +
                ", traversalBlocks=" + boundaryApproachTraversalBlocks +
                ", frames=" + boundaryApproachFrames);

            // note: Measure an ordinary 6 m/s frame followed by the 260 m/s boost without changing Unity's frame timing.
            UnityEngine.InputSystem.LowLevel.KeyboardState lowSpeedDirection = speedProbeInputState;
            int synchronousGroundFallbackAttemptsBeforeSpeedProbe = streamer.SynchronousGroundFallbackAttemptCount;
            int synchronousGroundFallbacksBeforeSpeedProbe = streamer.SynchronousGroundFallbackCount;
            int traversalBlocksBeforeHighSpeed = streamer.TraversalConstraintBlockCount;
            Vector3 preBoostStartPosition = player.position;
            double preBoostStartedAt = Time.realtimeSinceStartupAsDouble;
            int preBoostWarmupFrames = 0;
            bool preBoostLowSpeedInputObserved = false;
            QueueKeyboardState(keyboard, lowSpeedDirection);
            yield return null;
            preBoostWarmupFrames = 1;
            float preBoostPlanarDistance = PlanarDistance(preBoostStartPosition, player.position);
            double preBoostWarmupSeconds = Time.realtimeSinceStartupAsDouble - preBoostStartedAt;
            float preBoostWallPlanarSpeed = preBoostPlanarDistance / (float)Math.Max(0.000001d, preBoostWarmupSeconds);
            float preBoostUnityDeltaTime = Time.deltaTime;
            float preBoostUnityPlanarSpeed = preBoostPlanarDistance / Mathf.Max(0.000001f, preBoostUnityDeltaTime);
            float preBoostRequestedPlanarSpeed = motor.PlanarVelocity.magnitude;
            preBoostLowSpeedInputObserved = IsExpectedMotorInput(motor.MoveInput, speedProbeInputAxes);
            boostTravelDirection = MotorWorldPlanarDirection(motor, speedProbeInputAxes);
            Vector2Int firstBoostStartCell = ChunkCoordinateAt(player.position, streamer.chunkWorldSize);
            // note: Project the adjacent-cell witness from the latest real frame duration; the following movement remains unmodified by the verifier.
            float projectedFirstBoostFrameSeconds = Mathf.Max(Time.deltaTime, Time.unscaledDeltaTime);
            Vector2Int firstBoostMinimumTargetCell = ChunkCoordinateAt(
                player.position + boostTravelDirection * (240f * projectedFirstBoostFrameSeconds),
                streamer.chunkWorldSize);
            Vector2Int firstBoostMaximumTargetCell = ChunkCoordinateAt(
                player.position + boostTravelDirection * (260f * projectedFirstBoostFrameSeconds),
                streamer.chunkWorldSize);
            Vector2Int firstBoostEndCell = firstBoostStartCell;
            bool firstBoostMinimumTargetCellReady = streamer.TryGetPublicationSnapshot(
                    firstBoostMinimumTargetCell,
                    out YQSemanticChunkPublicationSnapshot firstBoostMinimumTargetSnapshot) &&
                firstBoostMinimumTargetSnapshot.physicalRepresentation && firstBoostMinimumTargetSnapshot.traversable &&
                firstBoostMinimumTargetSnapshot.terrainReadiness >= YQTerrainReadinessState.CollisionReady;
            bool firstBoostMaximumTargetCellReady = streamer.TryGetPublicationSnapshot(
                    firstBoostMaximumTargetCell,
                    out YQSemanticChunkPublicationSnapshot firstBoostMaximumTargetSnapshot) &&
                firstBoostMaximumTargetSnapshot.physicalRepresentation && firstBoostMaximumTargetSnapshot.traversable &&
                firstBoostMaximumTargetSnapshot.terrainReadiness >= YQTerrainReadinessState.CollisionReady;
            bool firstBoostTargetCellReady = firstBoostMaximumTargetCell != firstBoostStartCell &&
                firstBoostMinimumTargetCellReady && firstBoostMaximumTargetCellReady;
            motor.walkSpeed = 260f;
            motor.sprintSpeed = 260f;
            motor.acceleration = Mathf.Max(originalAcceleration, 1000000f);
            motor.deceleration = Mathf.Max(originalDeceleration, 1000000f);
            // note: Sample Unity's completed Main Thread metric alongside wall-frame time to separate game work from editor/GPU pacing.
            try
            {
                mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 128);
            }
            catch (Exception)
            {
                // note: Keep the movement acceptance probe usable when a player/editor build does not expose the optional profiler counter.
                mainThreadTimeRecorder = default;
            }
            int highSpeedSideContactsBefore = motor.BlockingControllerContactCount;
            Vector3 highSpeedStartPosition = player.position;
            bool highSpeedInputObserved = false;
            bool highSpeedGroundedObserved = false;
            bool groundViewCoverageMaintained = true;
            string firstGroundViewFailure = string.Empty;
            bool visibleViewCoverageMaintained = true;
            string firstVisibleViewFailure = string.Empty;
            bool highSpeedTurnBufferMaintained = true;
            string firstTurnBufferFailure = string.Empty;
            // note: Keep the transition frame explicit so a short speed-up hiccup cannot disappear inside the later distance total.
            float firstBoostFrameSeconds = 0f;
            float firstBoostFramePlanarDistance = 0f;
            float firstBoostFrameWallPlanarSpeed = 0f;
            float firstBoostFrameUnityPlanarSpeed = 0f;
            float firstBoostFrameRequestedPlanarSpeed = 0f;
            float firstBoostFrameUnityDeltaTime = 0f;
            bool firstBoostFrameInputObserved = false;
            bool firstBoostFrameDirectionObserved = false;
            bool firstBoostFrameCrossedChunkBoundary = false;
            bool firstBoostFrameGroundCoverage = false;
            bool firstBoostFrameVisibleCoverage = false;
            bool firstBoostFrameTurnBufferCoverage = false;
            float maximumHighSpeedObserved = 0f;
            float maximumHighSpeedWallObserved = 0f;
            float maximumRequestedHighSpeed = 0f;
            float maximumHighSpeedFrameSeconds = 0f;
            float minimumHighSpeedUnityDeltaTime = float.MaxValue;
            float maximumHighSpeedUnityDeltaTime = 0f;
            float maximumHighSpeedMainThreadSeconds = 0f;
            float worstWallFrameMainThreadSeconds = 0f;
            float worstWallFrameStreamingWorkSeconds = 0f;
            float worstWallFrameStreamingStageSeconds = 0f;
            string worstWallFrameStreamingStage = string.Empty;
            int worstWallFrameUnityFrame = -1;
            int worstWallFrameStreamerFrame = -1;
            float profilerPeakWallFrameSeconds = 0f;
            float profilerPeakStreamingWorkSeconds = 0f;
            float profilerPeakStreamingStageSeconds = 0f;
            string profilerPeakStreamingStage = string.Empty;
            int profilerPeakUnityFrame = -1;
            int profilerPeakStreamerFrame = -1;
            int profilerPeakGen0Collections = 0;
            int profilerPeakGen1Collections = 0;
            int profilerPeakGen2Collections = 0;
            int worstFrameGen0Collections = 0;
            int worstFrameGen1Collections = 0;
            int worstFrameGen2Collections = 0;
            long maximumHighSpeedAllocatedBytesPerFrame = 0;
            long worstWallFrameAllocatedBytes = 0;
            long profilerPeakAllocatedBytes = 0;
            int highSpeedFrames = 0;
            float highSpeedTravelDistance = 0f;
            float highSpeedConstantInputSegmentDistance = 0f;
            int highSpeedTransitionInputMask = 0;
            int highSpeedTransitionDisplacementMask = 0;
            const double highSpeedProbeDurationSeconds = 2.45d;
            Vector3[] highSpeedOrbitDirections =
            {
                new Vector3(-1f, 0f, -1f), Vector3.back,
                new Vector3(1f, 0f, -1f), Vector3.right,
                new Vector3(1f, 0f, 1f), Vector3.forward,
                new Vector3(-1f, 0f, 1f), Vector3.left
            };
            bool highSpeedLoopCaptureWritten = false;
            // note: Measure a fixed real-time travel window; frame-count limits would under-travel when Unity renders millisecond frames.
            phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline &&
                Time.realtimeSinceStartupAsDouble - phaseStarted < highSpeedProbeDurationSeconds)
            {
                // note: Advance one heading per completed input frame so a long editor frame cannot skip a required direction class.
                int transitionIndex = highSpeedFrames == 0
                    ? -1
                    : (highSpeedFrames - 1) % highSpeedOrbitDirections.Length;
                Vector2 expectedHighSpeedInput = speedProbeInputAxes;
                UnityEngine.InputSystem.LowLevel.KeyboardState highSpeedInput = speedProbeInputState;
                if (transitionIndex >= 0)
                    highSpeedInput = KeyboardStateForMotorDirection(
                        motor,
                        highSpeedOrbitDirections[transitionIndex],
                        out expectedHighSpeedInput);
                // note: Measure each real input frame and capture GC deltas so a boundary hitch cannot be hidden by the 260 m/s distance total.
                Vector3 highSpeedFrameStartPosition = player.position;
                double highSpeedFrameStarted = Time.realtimeSinceStartupAsDouble;
                long allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
                int gen0CollectionsBefore = GC.CollectionCount(0);
                int gen1CollectionsBefore = GC.CollectionCount(1);
                int gen2CollectionsBefore = GC.CollectionCount(2);
                QueueKeyboardState(keyboard, highSpeedInput);
                yield return null;
                Vector3 highSpeedFrameEndPosition = player.position;
                double highSpeedFrameEnded = Time.realtimeSinceStartupAsDouble;
                float highSpeedFrameSeconds = (float)Math.Max(0d, highSpeedFrameEnded - highSpeedFrameStarted);
                float highSpeedUnityDeltaTime = Time.deltaTime;
                minimumHighSpeedUnityDeltaTime = Mathf.Min(minimumHighSpeedUnityDeltaTime, highSpeedUnityDeltaTime);
                maximumHighSpeedUnityDeltaTime = Mathf.Max(maximumHighSpeedUnityDeltaTime, highSpeedUnityDeltaTime);
                long highSpeedAllocatedBytes = Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore);
                maximumHighSpeedAllocatedBytesPerFrame = Math.Max(
                    maximumHighSpeedAllocatedBytesPerFrame,
                    highSpeedAllocatedBytes);
                float highSpeedMainThreadSeconds = mainThreadTimeRecorder.Valid && mainThreadTimeRecorder.Count > 0
                    ? (float)Math.Max(0d, mainThreadTimeRecorder.LastValue * 1e-9d)
                    : 0f;
                if (mainThreadTimeRecorder.Valid && highSpeedMainThreadSeconds > maximumHighSpeedMainThreadSeconds)
                {
                    maximumHighSpeedMainThreadSeconds = highSpeedMainThreadSeconds;
                    // note: Match the profiler peak to this Unity frame's wall interval and streamer stage to detect a one-frame recorder offset.
                    profilerPeakWallFrameSeconds = highSpeedFrameSeconds;
                    profilerPeakStreamingWorkSeconds = streamer.LastStreamingWorkSeconds;
                    profilerPeakStreamingStageSeconds = streamer.LastStreamingWorstStageSeconds;
                    profilerPeakStreamingStage = streamer.LastStreamingWorstStage;
                    profilerPeakUnityFrame = Time.frameCount;
                    profilerPeakStreamerFrame = streamer.LastStreamingWorkFrameCount;
                    profilerPeakGen0Collections = Math.Max(0, GC.CollectionCount(0) - gen0CollectionsBefore);
                    profilerPeakGen1Collections = Math.Max(0, GC.CollectionCount(1) - gen1CollectionsBefore);
                    profilerPeakGen2Collections = Math.Max(0, GC.CollectionCount(2) - gen2CollectionsBefore);
                    profilerPeakAllocatedBytes = highSpeedAllocatedBytes;
                }
                if (highSpeedFrameSeconds >= maximumHighSpeedFrameSeconds)
                {
                    maximumHighSpeedFrameSeconds = highSpeedFrameSeconds;
                    worstFrameGen0Collections = Math.Max(0, GC.CollectionCount(0) - gen0CollectionsBefore);
                    worstFrameGen1Collections = Math.Max(0, GC.CollectionCount(1) - gen1CollectionsBefore);
                    worstFrameGen2Collections = Math.Max(0, GC.CollectionCount(2) - gen2CollectionsBefore);
                    worstWallFrameAllocatedBytes = highSpeedAllocatedBytes;
                    // note: Associate streamer CPU and its longest stage with the same measured wall frame instead of comparing unrelated high-water marks.
                    worstWallFrameMainThreadSeconds = highSpeedMainThreadSeconds;
                    worstWallFrameStreamingWorkSeconds = streamer.LastStreamingWorkSeconds;
                    worstWallFrameStreamingStageSeconds = streamer.LastStreamingWorstStageSeconds;
                    worstWallFrameStreamingStage = streamer.LastStreamingWorstStage;
                    worstWallFrameUnityFrame = Time.frameCount;
                    worstWallFrameStreamerFrame = streamer.LastStreamingWorkFrameCount;
                }
                ValidateProductionMotorFrame(streamer, player, characterController, "speed-260", report);
                highSpeedFrames++;
                Vector3 frameTravelDelta = highSpeedFrameEndPosition - highSpeedFrameStartPosition;
                Vector2 frameTravelPlanar = new Vector2(frameTravelDelta.x, frameTravelDelta.z);
                float frameTravelDistance = frameTravelPlanar.magnitude;
                highSpeedTravelDistance += frameTravelDistance;
                // note: Use monotonic wall time for actual travel speed; Unity deltaTime remains diagnostic because it may be scaled or overridden.
                float observedWallPlanarSpeed = frameTravelDistance / Mathf.Max(0.000001f, highSpeedFrameSeconds);
                float observedUnityPlanarSpeed = frameTravelDistance / Mathf.Max(0.000001f, highSpeedUnityDeltaTime);
                // note: The motor integrates displacement with Time.deltaTime; retain wall speed separately so frame pacing cannot distort its movement-speed contract.
                maximumHighSpeedObserved = Mathf.Max(
                    maximumHighSpeedObserved,
                    observedUnityPlanarSpeed);
                maximumHighSpeedWallObserved = Mathf.Max(maximumHighSpeedWallObserved, observedWallPlanarSpeed);
                maximumRequestedHighSpeed = Mathf.Max(maximumRequestedHighSpeed, motor.PlanarVelocity.magnitude);
                if (highSpeedFrames == 1)
                {
                    firstBoostFrameSeconds = highSpeedFrameSeconds;
                    firstBoostFramePlanarDistance = frameTravelDistance;
                    firstBoostFrameWallPlanarSpeed = observedWallPlanarSpeed;
                    firstBoostFrameUnityPlanarSpeed = observedUnityPlanarSpeed;
                    firstBoostFrameRequestedPlanarSpeed = motor.PlanarVelocity.magnitude;
                    firstBoostFrameUnityDeltaTime = highSpeedUnityDeltaTime;
                    firstBoostFrameInputObserved = IsExpectedMotorInput(motor.MoveInput, speedProbeInputAxes);
                    Vector3 firstBoostRequestedDirection =
                        motor.transform.forward * motor.MoveInput.y +
                        motor.transform.right * motor.MoveInput.x;
                    Vector2 firstBoostRequestedPlanar = new Vector2(
                        firstBoostRequestedDirection.x,
                        firstBoostRequestedDirection.z);
                    firstBoostFrameDirectionObserved = firstBoostFrameInputObserved &&
                        firstBoostFrameUnityPlanarSpeed >= 240f &&
                        firstBoostRequestedPlanar.sqrMagnitude > 0.0001f &&
                        Vector2.Dot(frameTravelPlanar.normalized, firstBoostRequestedPlanar.normalized) >= 0.9f;
                    firstBoostEndCell = ChunkCoordinateAt(player.position, streamer.chunkWorldSize);
                    firstBoostFrameCrossedChunkBoundary = firstBoostEndCell != firstBoostStartCell &&
                        (firstBoostEndCell == firstBoostMinimumTargetCell ||
                            firstBoostEndCell == firstBoostMaximumTargetCell);
                }
                // note: Sum real distances over constant-input legs of the safe loop; each segment remains an ordinary straight motor displacement.
                highSpeedConstantInputSegmentDistance += frameTravelDistance;
                highSpeedInputObserved |= motor.MoveInput.y < -0.1f;
                highSpeedGroundedObserved |= motor.IsGrounded;
                if (!highSpeedLoopCaptureWritten && transitionIndex >= 0)
                {
                    // note: Capture the collision owner at the first real high-speed turn before later loop segments can change the contact.
                    AppendProductionMovementDiagnostics(report, motor, "speed260-loop", highSpeedSideContactsBefore);
                    // note: Defer the full report flush until the benchmark ends so synchronous file I/O cannot manufacture its own frame-budget failure.
                    highSpeedLoopCaptureWritten = true;
                }
                if (transitionIndex >= 0)
                {
                    bool transitionInputObserved = IsExpectedMotorInput(motor.MoveInput, expectedHighSpeedInput);
                    int transitionMaskBit = HighSpeedTransitionMaskBit(expectedHighSpeedInput);
                    if (transitionInputObserved && transitionMaskBit >= 0)
                    {
                        highSpeedTransitionInputMask |= 1 << transitionMaskBit;
                        Vector3 requestedTransitionDirection =
                            motor.transform.forward * motor.MoveInput.y +
                            motor.transform.right * motor.MoveInput.x;
                        Vector2 requestedTransitionPlanar = new Vector2(
                            requestedTransitionDirection.x,
                            requestedTransitionDirection.z);
                        float actualTransitionUnitySpeed = frameTravelPlanar.magnitude /
                            Mathf.Max(0.000001f, highSpeedUnityDeltaTime);
                        if (actualTransitionUnitySpeed >= 240f &&
                            requestedTransitionPlanar.sqrMagnitude > 0.0001f &&
                            Vector2.Dot(frameTravelPlanar.normalized, requestedTransitionPlanar.normalized) >= 0.9f)
                            highSpeedTransitionDisplacementMask |= 1 << transitionMaskBit;
                    }
                }
                if (groundViewCoverageMaintained &&
                    !streamer.TryValidateGroundViewCoverage(out firstGroundViewFailure))
                    groundViewCoverageMaintained = false;
                if (visibleViewCoverageMaintained &&
                    !streamer.TryValidateCurrentVisualCoverage(out firstVisibleViewFailure))
                    visibleViewCoverageMaintained = false;
                if (highSpeedTurnBufferMaintained &&
                    !streamer.TryValidateHighSpeedTurnBufferCoverage(out firstTurnBufferFailure))
                    highSpeedTurnBufferMaintained = false;
                if (highSpeedFrames == 1)
                {
                    firstBoostFrameGroundCoverage = groundViewCoverageMaintained;
                    firstBoostFrameVisibleCoverage = visibleViewCoverageMaintained;
                    firstBoostFrameTurnBufferCoverage = highSpeedTurnBufferMaintained;
                }
            }
            // note: Keep observing after key release because the authoritative motor retains momentum; the visible terrain contract lasts until that real velocity settles.
            float highSpeedCoastStartSpeed = motor.PlanarVelocity.magnitude;
            Vector3 highSpeedCoastStartPosition = player.position;
            int highSpeedCoastFrames = 0;
            int highSpeedCoastTraversalBlocksBefore = streamer.TraversalConstraintBlockCount;
            int highSpeedCoastFallbackAttemptsBefore = streamer.SynchronousGroundFallbackAttemptCount;
            double highSpeedCoastStartedAt = Time.realtimeSinceStartupAsDouble;
            float maximumHighSpeedCoastFrameSeconds = 0f;
            float maximumHighSpeedCoastMainThreadSeconds = 0f;
            float worstHighSpeedCoastStreamingWorkSeconds = 0f;
            float worstHighSpeedCoastStreamingStageSeconds = 0f;
            string worstHighSpeedCoastStreamingStage = string.Empty;
            int worstHighSpeedCoastUnityFrame = -1;
            long worstHighSpeedCoastAllocatedBytes = 0L;
            int worstHighSpeedCoastGen0Collections = 0;
            int worstHighSpeedCoastGen1Collections = 0;
            int worstHighSpeedCoastGen2Collections = 0;
            const double highSpeedCoastTimeoutSeconds = 12d;
            bool highSpeedCoastInputReleased = true;
            bool highSpeedCoastGroundCoverageMaintained = true;
            bool highSpeedCoastVisibleCoverageMaintained = true;
            bool highSpeedCoastStartGrounded = motor.IsGrounded;
            string firstHighSpeedCoastGroundFailure = string.Empty;
            string firstHighSpeedCoastVisibleFailure = string.Empty;
            // note: Restore both ground braking and airborne control before release; HandleMove uses acceleration times air control while airborne.
            motor.acceleration = originalAcceleration;
            motor.deceleration = originalDeceleration;
            QueueKeyboardState(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            while (Time.realtimeSinceStartupAsDouble - highSpeedCoastStartedAt < highSpeedCoastTimeoutSeconds &&
                   motor.PlanarVelocity.sqrMagnitude > 4f)
            {
                // note: Measure coast frame duration, streamer attribution, allocation, and GC on the same sample as coverage after input release.
                double coastFrameStartedAt = Time.realtimeSinceStartupAsDouble;
                long coastAllocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
                int coastGen0CollectionsBefore = GC.CollectionCount(0);
                int coastGen1CollectionsBefore = GC.CollectionCount(1);
                int coastGen2CollectionsBefore = GC.CollectionCount(2);
                yield return null;
                highSpeedCoastFrames++;
                float coastFrameSeconds = (float)Math.Max(
                    0d,
                    Time.realtimeSinceStartupAsDouble - coastFrameStartedAt);
                long coastAllocatedBytes = Math.Max(
                    0L,
                    GC.GetAllocatedBytesForCurrentThread() - coastAllocatedBytesBefore);
                float coastMainThreadSeconds = mainThreadTimeRecorder.Valid && mainThreadTimeRecorder.Count > 0
                    ? (float)Math.Max(0d, mainThreadTimeRecorder.LastValue * 1e-9d)
                    : 0f;
                if (coastFrameSeconds >= maximumHighSpeedCoastFrameSeconds)
                {
                    maximumHighSpeedCoastFrameSeconds = coastFrameSeconds;
                    maximumHighSpeedCoastMainThreadSeconds = coastMainThreadSeconds;
                    worstHighSpeedCoastStreamingWorkSeconds = streamer.LastStreamingWorkSeconds;
                    worstHighSpeedCoastStreamingStageSeconds = streamer.LastStreamingWorstStageSeconds;
                    worstHighSpeedCoastStreamingStage = streamer.LastStreamingWorstStage;
                    worstHighSpeedCoastUnityFrame = Time.frameCount;
                    worstHighSpeedCoastAllocatedBytes = coastAllocatedBytes;
                    worstHighSpeedCoastGen0Collections = Math.Max(0, GC.CollectionCount(0) - coastGen0CollectionsBefore);
                    worstHighSpeedCoastGen1Collections = Math.Max(0, GC.CollectionCount(1) - coastGen1CollectionsBefore);
                    worstHighSpeedCoastGen2Collections = Math.Max(0, GC.CollectionCount(2) - coastGen2CollectionsBefore);
                }
                if (motor.MoveInput.sqrMagnitude > 0.0001f)
                    highSpeedCoastInputReleased = false;
                if (highSpeedCoastGroundCoverageMaintained &&
                    !streamer.TryValidateGroundViewCoverage(out firstHighSpeedCoastGroundFailure))
                    highSpeedCoastGroundCoverageMaintained = false;
                if (highSpeedCoastVisibleCoverageMaintained &&
                    !streamer.TryValidateCurrentVisualCoverage(out firstHighSpeedCoastVisibleFailure))
                    highSpeedCoastVisibleCoverageMaintained = false;
            }
            bool highSpeedCoastSettled = motor.PlanarVelocity.magnitude <= 2f;
            float highSpeedCoastElapsedSeconds = (float)(Time.realtimeSinceStartupAsDouble - highSpeedCoastStartedAt);
            float highSpeedCoastDistance = PlanarDistance(highSpeedCoastStartPosition, player.position);
            int highSpeedCoastTraversalBlocks = streamer.TraversalConstraintBlockCount - highSpeedCoastTraversalBlocksBefore;
            int highSpeedCoastFallbackAttempts = streamer.SynchronousGroundFallbackAttemptCount - highSpeedCoastFallbackAttemptsBefore;
            bool highSpeedCoastFrameBudgetPassed = highSpeedCoastFrames > 0 &&
                maximumHighSpeedCoastFrameSeconds <= YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds;
            bool highSpeedCoastObserved = highSpeedCoastStartSpeed >= 240f && highSpeedCoastFrames > 1 &&
                highSpeedCoastElapsedSeconds >= 1f && highSpeedCoastDistance >= 1f &&
                highSpeedCoastInputReleased && highSpeedCoastSettled &&
                highSpeedCoastGroundCoverageMaintained && highSpeedCoastVisibleCoverageMaintained &&
                highSpeedCoastTraversalBlocks == 0 && highSpeedCoastFallbackAttempts == 0 &&
                highSpeedCoastFrameBudgetPassed;

            float highSpeedPlanarDistance = PlanarDistance(highSpeedStartPosition, player.position);
            int highSpeedTraversalBlocks = streamer.TraversalConstraintBlockCount - traversalBlocksBeforeHighSpeed;
            int highSpeedSynchronousGroundFallbacks =
                streamer.SynchronousGroundFallbackCount - synchronousGroundFallbacksBeforeSpeedProbe;
            int highSpeedSynchronousGroundFallbackAttempts =
                streamer.SynchronousGroundFallbackAttemptCount - synchronousGroundFallbackAttemptsBeforeSpeedProbe;
            bool highSpeedNoSynchronousGroundFallbacks = highSpeedSynchronousGroundFallbackAttempts == 0;
            bool realElapsedTransitionObserved = preBoostWarmupSeconds > 0d && firstBoostFrameSeconds > 0f &&
                preBoostUnityPlanarSpeed >= 4f && preBoostUnityPlanarSpeed <= 12f &&
                firstBoostFrameUnityPlanarSpeed >= 240f &&
                firstBoostFrameUnityPlanarSpeed <= firstBoostFrameRequestedPlanarSpeed + 20f;
            bool highSpeedBoundaryObserved = boundaryApproachReady && boundaryApproachClean && firstBoostTargetCellReady &&
                firstBoostFrameCrossedChunkBoundary;
            bool highSpeedAbruptTransitionObserved =
                preBoostLowSpeedInputObserved && preBoostRequestedPlanarSpeed >= 4f && preBoostRequestedPlanarSpeed <= 12f &&
                firstBoostFrameInputObserved && firstBoostFrameRequestedPlanarSpeed >= 240f &&
                firstBoostFrameUnityPlanarSpeed >= 240f && firstBoostFrameDirectionObserved &&
                firstBoostFrameGroundCoverage && firstBoostFrameVisibleCoverage && firstBoostFrameTurnBufferCoverage &&
                highSpeedBoundaryObserved &&
                realElapsedTransitionObserved;
            bool highSpeedConstantInputSegmentsObserved = highSpeedInputObserved && highSpeedGroundedObserved &&
                groundViewCoverageMaintained && visibleViewCoverageMaintained && highSpeedTurnBufferMaintained &&
                maximumHighSpeedObserved >= 240f &&
                highSpeedAbruptTransitionObserved &&
                highSpeedConstantInputSegmentDistance >= 250f && highSpeedTravelDistance >= 300f && highSpeedTraversalBlocks == 0 &&
                highSpeedNoSynchronousGroundFallbacks &&
                highSpeedTransitionInputMask == 0b111111 && highSpeedTransitionDisplacementMask == 0b111111;
            bool highSpeedObserved = highSpeedConstantInputSegmentsObserved && highSpeedCoastObserved;
            bool highSpeedFrameBudgetPassed = highSpeedFrames > 0 &&
                maximumHighSpeedFrameSeconds <= YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds &&
                highSpeedCoastFrameBudgetPassed;
            report.AppendLine("- productionMotorSpeed260AbruptTransition: configuredWalkSpeed=6.0, sameHeadingWarmup=true, frames=" + preBoostWarmupFrames +
                ", seconds=" + preBoostWarmupSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", planarDistance=" + preBoostPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", lowSpeedWallPlanarSpeed=" + preBoostWallPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", lowSpeedUnityDeltaTime=" + preBoostUnityDeltaTime.ToString("0.000000", CultureInfo.InvariantCulture) +
                ", lowSpeedUnityPlanarSpeed=" + preBoostUnityPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", lowSpeedInputObserved=" + preBoostLowSpeedInputObserved +
                ", lowSpeedRequestedPlanarSpeed=" + preBoostRequestedPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", boundaryApproachReady=" + boundaryApproachReady +
                ", boundaryGapMeters=" + finalBoundaryGapMeters.ToString("0.000", CultureInfo.InvariantCulture));
            report.AppendLine("- productionMotorSpeed260FirstBoostFrame: wallSeconds=" + firstBoostFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", planarDistance=" + firstBoostFramePlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", wallPlanarSpeed=" + firstBoostFrameWallPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", unityPlanarSpeed=" + firstBoostFrameUnityPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", unityDeltaTime=" + firstBoostFrameUnityDeltaTime.ToString("0.000000", CultureInfo.InvariantCulture) +
                ", requestedPlanarSpeed=" + firstBoostFrameRequestedPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", boostInputObserved=" + firstBoostFrameInputObserved +
                ", boostDirectionObserved=" + firstBoostFrameDirectionObserved +
                ", reachedNearMaximumSpeed=" + (firstBoostFrameRequestedPlanarSpeed >= 240f) +
                ", groundCoverage=" + firstBoostFrameGroundCoverage +
                ", visibleCoverage=" + firstBoostFrameVisibleCoverage +
                ", turnBufferCoverage=" + firstBoostFrameTurnBufferCoverage +
                ", startCell=" + firstBoostStartCell +
                ", minimumProjectedTargetCell=" + firstBoostMinimumTargetCell +
                ", maximumProjectedTargetCell=" + firstBoostMaximumTargetCell +
                ", expectedTargetReady=" + firstBoostTargetCellReady +
                ", actualEndCell=" + firstBoostEndCell +
                ", crossedCellBoundary=" + firstBoostFrameCrossedChunkBoundary);
            report.AppendLine("- speed260RealElapsedTransitionGate: " +
                (realElapsedTransitionObserved
                    ? "PASS"
                    : "FAIL lowWallSeconds=" + preBoostWarmupSeconds.ToString("0.000000", CultureInfo.InvariantCulture) +
                      " lowUnitySpeed=" + preBoostUnityPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostWallSeconds=" + firstBoostFrameSeconds.ToString("0.000000", CultureInfo.InvariantCulture) +
                      " boostUnitySpeed=" + firstBoostFrameUnityPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostWallSpeed=" + firstBoostFrameWallPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostRequestedSpeed=" + firstBoostFrameRequestedPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture)));
            report.AppendLine("- speed260AbruptTransitionGate: " +
                (highSpeedAbruptTransitionObserved
                    ? "PASS"
                    : "FAIL lowSpeedInput=" + preBoostLowSpeedInputObserved +
                      " lowSpeed=" + preBoostRequestedPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostInput=" + firstBoostFrameInputObserved +
                      " boostRequested=" + firstBoostFrameRequestedPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostActualUnitySpeed=" + firstBoostFrameUnityPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostWallSpeed=" + firstBoostFrameWallPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostDirection=" + firstBoostFrameDirectionObserved +
                      " ground=" + firstBoostFrameGroundCoverage +
                      " visible=" + firstBoostFrameVisibleCoverage +
                      " turnBuffer=" + firstBoostFrameTurnBufferCoverage));
            report.AppendLine("- speed260BoundaryCrossingGate: " +
                (highSpeedBoundaryObserved
                    ? "PASS"
                    : "FAIL approachReady=" + boundaryApproachReady +
                      " approachClean=" + boundaryApproachClean +
                      " gapMeters=" + finalBoundaryGapMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                      " targetReady=" + firstBoostTargetCellReady +
                      " startCell=" + firstBoostStartCell +
                      " minimumTargetCell=" + firstBoostMinimumTargetCell +
                      " maximumTargetCell=" + firstBoostMaximumTargetCell +
                      " actualEndCell=" + firstBoostEndCell +
                      " crossed=" + firstBoostFrameCrossedChunkBoundary));
            report.AppendLine("- speed260SynchronousFallbackGate: " +
                (highSpeedFrames > 0 && highSpeedNoSynchronousGroundFallbacks
                    ? "PASS"
                    : "FAIL frames=" + highSpeedFrames + " fallbackAttempts=" + highSpeedSynchronousGroundFallbackAttempts +
                      " successfulPublications=" + highSpeedSynchronousGroundFallbacks));
            report.AppendLine("- productionMotorSpeed260Evidence: inputObserved=" + highSpeedInputObserved +
                ", grounded=" + highSpeedGroundedObserved +
                ", trajectory=origin-safe-eight-direction-loop" +
                ", groundViewCoverageEveryFrame=" + groundViewCoverageMaintained +
                (groundViewCoverageMaintained ? string.Empty : ", firstGroundViewFailure=" + firstGroundViewFailure) +
                ", fullyLoadedVisibleViewEveryFrame=" + visibleViewCoverageMaintained +
                (visibleViewCoverageMaintained ? string.Empty : ", firstVisibleViewFailure=" + firstVisibleViewFailure) +
                ", allDirectionTurnBufferEveryFrame=" + highSpeedTurnBufferMaintained +
                (highSpeedTurnBufferMaintained ? string.Empty : ", firstTurnBufferFailure=" + firstTurnBufferFailure) +
                ", maximumActualPlanarSpeed=" + maximumHighSpeedObserved.ToString("0.0", CultureInfo.InvariantCulture) +
                ", maximumWallPlanarSpeed=" + maximumHighSpeedWallObserved.ToString("0.0", CultureInfo.InvariantCulture) +
                ", maximumRequestedPlanarSpeed=" + maximumRequestedHighSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", observedAboveRequestedPlus20=" + (maximumHighSpeedObserved > maximumRequestedHighSpeed + 20f) +
                ", planarDistance=" + highSpeedPlanarDistance.ToString("0.0", CultureInfo.InvariantCulture) +
                ", constantInputSegmentDistance=" + highSpeedConstantInputSegmentDistance.ToString("0.0", CultureInfo.InvariantCulture) +
                ", traveledDistance=" + highSpeedTravelDistance.ToString("0.0", CultureInfo.InvariantCulture) +
                ", traversalBlocks=" + highSpeedTraversalBlocks + ", transitionInputMask=" + highSpeedTransitionInputMask +
                ", transitionDisplacementMask=" + highSpeedTransitionDisplacementMask +
                ", synchronousGroundFallbackAttempts=" + highSpeedSynchronousGroundFallbackAttempts +
                ", synchronousGroundFallbacks=" + highSpeedSynchronousGroundFallbacks +
                ", frames=" + highSpeedFrames +
                ", unityDeltaTimeRange=" + (minimumHighSpeedUnityDeltaTime == float.MaxValue ? 0f : minimumHighSpeedUnityDeltaTime).ToString("0.000000", CultureInfo.InvariantCulture) +
                ".." + maximumHighSpeedUnityDeltaTime.ToString("0.000000", CultureInfo.InvariantCulture) +
                ", maximumFrameSeconds=" + maximumHighSpeedFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", maximumMainThreadSeconds=" + (mainThreadTimeRecorder.Valid
                    ? maximumHighSpeedMainThreadSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                    : "unavailable") +
                ", worstWallFrameMainThreadSeconds=" + (mainThreadTimeRecorder.Valid
                    ? worstWallFrameMainThreadSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                    : "unavailable") +
                ", worstWallFrameStreamingWorkSeconds=" + worstWallFrameStreamingWorkSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstWallFrameStreamingStage=" + worstWallFrameStreamingStage +
                ", worstWallFrameStreamingStageSeconds=" + worstWallFrameStreamingStageSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstWallFrameUnityFrame=" + worstWallFrameUnityFrame +
                ", worstWallFrameStreamerFrame=" + worstWallFrameStreamerFrame +
                ", profilerPeakWallFrameSeconds=" + profilerPeakWallFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", profilerPeakStreamingWorkSeconds=" + profilerPeakStreamingWorkSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", profilerPeakStreamingStage=" + profilerPeakStreamingStage +
                ", profilerPeakStreamingStageSeconds=" + profilerPeakStreamingStageSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", profilerPeakUnityFrame=" + profilerPeakUnityFrame +
                ", profilerPeakStreamerFrame=" + profilerPeakStreamerFrame +
                ", profilerPeakGcCollections=" + profilerPeakGen0Collections + "/" + profilerPeakGen1Collections + "/" + profilerPeakGen2Collections +
                ", profilerPeakAllocatedBytes=" + profilerPeakAllocatedBytes +
                ", worstWallFrameAllocatedBytes=" + worstWallFrameAllocatedBytes +
                ", maximumAllocatedBytesPerFrame=" + maximumHighSpeedAllocatedBytesPerFrame +
                ", worstFrameGcCollections=" + worstFrameGen0Collections + "/" + worstFrameGen1Collections + "/" + worstFrameGen2Collections +
                ", provisionalGround=" + streamer.ProvisionalGroundDiagnostics);
            report.AppendLine("- productionMotorSpeed260CoastEvidence: startSpeed=" + highSpeedCoastStartSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", frames=" + highSpeedCoastFrames +
                ", elapsedSeconds=" + highSpeedCoastElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", travelDistance=" + highSpeedCoastDistance.ToString("0.0", CultureInfo.InvariantCulture) +
                ", finalSpeed=" + motor.PlanarVelocity.magnitude.ToString("0.0", CultureInfo.InvariantCulture) +
                ", startGrounded=" + highSpeedCoastStartGrounded +
                ", restoredAcceleration=" + originalAcceleration.ToString("0.0", CultureInfo.InvariantCulture) +
                ", restoredDeceleration=" + originalDeceleration.ToString("0.0", CultureInfo.InvariantCulture) +
                ", inputReleased=" + highSpeedCoastInputReleased +
                ", settled=" + highSpeedCoastSettled +
                ", groundCoverageEveryFrame=" + highSpeedCoastGroundCoverageMaintained +
                (highSpeedCoastGroundCoverageMaintained ? string.Empty : ", firstGroundFailure=" + firstHighSpeedCoastGroundFailure) +
                ", fullyLoadedVisibleViewEveryFrame=" + highSpeedCoastVisibleCoverageMaintained +
                (highSpeedCoastVisibleCoverageMaintained ? string.Empty : ", firstVisibleFailure=" + firstHighSpeedCoastVisibleFailure) +
                ", maximumFrameSeconds=" + maximumHighSpeedCoastFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", maximumMainThreadSeconds=" + (mainThreadTimeRecorder.Valid
                    ? maximumHighSpeedCoastMainThreadSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                    : "unavailable") +
                ", worstFrameStreamingWorkSeconds=" + worstHighSpeedCoastStreamingWorkSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstFrameStreamingStage=" + worstHighSpeedCoastStreamingStage +
                ", worstFrameStreamingStageSeconds=" + worstHighSpeedCoastStreamingStageSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstFrameUnityFrame=" + worstHighSpeedCoastUnityFrame +
                ", worstFrameAllocatedBytes=" + worstHighSpeedCoastAllocatedBytes +
                ", worstFrameGcCollections=" + worstHighSpeedCoastGen0Collections + "/" + worstHighSpeedCoastGen1Collections + "/" + worstHighSpeedCoastGen2Collections +
                ", traversalBlocks=" + highSpeedCoastTraversalBlocks +
                ", synchronousGroundFallbackAttempts=" + highSpeedCoastFallbackAttempts);
            AppendProductionMovementDiagnostics(report, motor, "speed260-final", highSpeedSideContactsBefore);
            // note: Capture all current physical and reservation-owned cells after frame measurements so census formatting cannot inflate the 260 m/s budget.
            report.AppendLine("- physicalOwnerCapacityCensus: " + streamer.CapturePhysicalOwnerCapacityCensus());
            report.AppendLine("- speed260ConstantInputSegmentsGate: " + (highSpeedConstantInputSegmentsObserved ? "PASS" : "FAIL"));
            report.AppendLine("- speed260DirectionChangeGate: " +
                (highSpeedConstantInputSegmentsObserved && highSpeedTransitionInputMask == 0b111111 && highSpeedTransitionDisplacementMask == 0b111111
                    ? "PASS"
                    : "FAIL inputMask=" + highSpeedTransitionInputMask + " displacementMask=" + highSpeedTransitionDisplacementMask));
            report.AppendLine("- speed260VisibleViewGate: " +
                (highSpeedFrames > 0 && visibleViewCoverageMaintained
                    ? "PASS"
                    : "FAIL frames=" + highSpeedFrames + " " + firstVisibleViewFailure));
            report.AppendLine("- speed260TurnBufferGate: " +
                (highSpeedFrames > 0 && highSpeedTurnBufferMaintained
                    ? "PASS"
                    : "FAIL frames=" + highSpeedFrames + " " + firstTurnBufferFailure));
            report.AppendLine("- speed260CoastCoverageGate: " +
                (highSpeedCoastObserved
                    ? "PASS"
                    : "FAIL startSpeed=" + highSpeedCoastStartSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " frames=" + highSpeedCoastFrames +
                      " elapsedSeconds=" + highSpeedCoastElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                      " travelDistance=" + highSpeedCoastDistance.ToString("0.0", CultureInfo.InvariantCulture) +
                      " inputReleased=" + highSpeedCoastInputReleased +
                      " settled=" + highSpeedCoastSettled +
                      " ground=" + highSpeedCoastGroundCoverageMaintained +
                      " visible=" + highSpeedCoastVisibleCoverageMaintained +
                      " traversalBlocks=" + highSpeedCoastTraversalBlocks +
                      " synchronousFallbackAttempts=" + highSpeedCoastFallbackAttempts));
            report.AppendLine("- speed260CoastFrameBudgetGate: " +
                (highSpeedCoastFrameBudgetPassed
                    ? "PASS maximumFrameSeconds=" + maximumHighSpeedCoastFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                    : "FAIL frames=" + highSpeedCoastFrames +
                      " maximumFrameSeconds=" + maximumHighSpeedCoastFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                      " budgetSeconds=" + YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds.ToString("0.000", CultureInfo.InvariantCulture)));
            report.AppendLine("- speed260FrameBudgetGate: " +
                (highSpeedFrameBudgetPassed
                    ? "PASS"
                    : "FAIL inputFrames=" + highSpeedFrames +
                      " inputMaximumFrameSeconds=" + maximumHighSpeedFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                      " coastFrames=" + highSpeedCoastFrames +
                      " coastMaximumFrameSeconds=" + maximumHighSpeedCoastFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture)));

            report.AppendLine("- productionMotorInputFrames: walk=" + walkFrames + ",run=" + sprintFrames + ",dash=" + dashFrames +
                ",reversal=" + reversalFrames + ",diagonal=" + diagonalFrames + ",rapidTurns=" + rapidTurnFrames +
                ",speed260=" + highSpeedFrames);
            // note: Persist the complete production motor matrix before returning control to the verifier.
            WriteReport(report, false, false);
            result(walkObserved && sprintObserved && dashObserved && reversalObserved && diagonalObserved && rapidTurnsObserved &&
                highSpeedObserved && highSpeedFrameBudgetPassed, false);
        }
        finally
        {
            // note: Runtime stress values are probe-only and never become a serialized player-speed change.
            if (motor != null)
            {
                motor.walkSpeed = originalWalkSpeed;
                motor.sprintSpeed = originalSprintSpeed;
                motor.acceleration = originalAcceleration;
                motor.deceleration = originalDeceleration;
            }
            // note: Release the keys without removing the user's physical device or mutating global Input System ownership.
            if (keyboard != null)
            {
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState());
            }
            if (mainThreadTimeRecorder.Valid)
                mainThreadTimeRecorder.Dispose();
        }
    }

    private static IEnumerator VerifyPublicationRecoveryMatrix(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        YQInvestorPlayerMotor motor,
        Transform player,
        CharacterController characterController,
        StringBuilder report,
        Action<bool, bool, bool, bool> result)
    {
        UnityEngine.InputSystem.Keyboard keyboard = null;
        HashSet<Vector2Int> usedCoordinates = new HashSet<Vector2Int>();
        bool allPassed = true;
        bool recoveryInputDeliveryInconclusive = false;
        bool unloadRevisitPassed = false;
        bool unloadRevisitInputInconclusive = false;
        try
        {
            // note: Reuse the existing keyboard for every recovery case, creating one only when the editor exposes no keyboard device.
            keyboard = AcquireVerificationKeyboard();
            if (keyboard == null)
                throw new InvalidOperationException("publication recovery verification could not acquire a keyboard device");
            keyboard.MakeCurrent();

            Vector2Int cancellationCoordinate = default;
            bool recoveryWitnessReady = false;
            double recoveryWitnessStarted = Time.realtimeSinceStartupAsDouble;
            double recoveryWitnessDeadline = recoveryWitnessStarted + RecoveryWitnessTimeoutSeconds;
            while (Time.realtimeSinceStartupAsDouble < recoveryWitnessDeadline)
            {
                if (TryFindRecoveryCoordinate(streamer, usedCoordinates, out cancellationCoordinate))
                {
                    recoveryWitnessReady = true;
                    break;
                }
                // note: Wait only for a real retained production owner to become available; this does not settle the initial frontier or certify any movement phase.
                yield return null;
            }
            if (!recoveryWitnessReady)
                throw new InvalidOperationException("no streamed recovery coordinate was available");
            report.AppendLine("- recoveryWitnessReady: PASS elapsedSeconds=" +
                (Time.realtimeSinceStartupAsDouble - recoveryWitnessStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                " coordinate=" + cancellationCoordinate);
            WriteReport(report, false, false);
            YQSemanticChunkPublicationSnapshot cancellationBefore;
            if (!streamer.TryGetPublicationSnapshot(cancellationCoordinate, out cancellationBefore))
                throw new InvalidOperationException("cancellation witness disappeared before replacement");
            int firstReplacementVersion;
            int secondReplacementVersion;
            bool replacementStarted = streamer.TryReplacePublicationOwnerForVerification(
                cancellationCoordinate,
                out firstReplacementVersion,
                out secondReplacementVersion);
            if (replacementStarted)
                usedCoordinates.Add(cancellationCoordinate);
            bool workObserved = false;
            YQSemanticChunkPublicationSnapshot cancellationSnapshot = default;
            yield return WaitForPublicationSnapshot(
                streamer,
                cancellationCoordinate,
                snapshot =>
                    (snapshot.terrainWorkActive && snapshot.terrainWorkId != 0) ||
                    (snapshot.contentWorkActive && snapshot.contentWorkId != 0) ||
                    (snapshot.appearanceWorkActive && snapshot.appearanceWorkVersion != 0),
                30f,
                (matched, snapshot) =>
                {
                    workObserved = matched;
                    cancellationSnapshot = snapshot;
                });
            int retiredVersion = cancellationBefore.publicationVersion;
            int retiredOwnerEpoch = cancellationBefore.ownerEpoch;
            int replacementAfterWorkVersion = 0;
            int replacementPublishedVersion = 0;
            bool replacementAfterWork = false;
            // note: A terrain handoff may temporarily own the coordinate; retry the same production replacement API until that handoff releases or the bounded real-time window expires.
            double replacementDeadline = Time.realtimeSinceStartupAsDouble + PublicationRecoveryTimeoutSeconds;
            while (workObserved && !replacementAfterWork && Time.realtimeSinceStartupAsDouble < replacementDeadline)
            {
                replacementAfterWork = streamer.TryReplacePublicationOwnerForVerification(
                    cancellationCoordinate,
                    out replacementAfterWorkVersion,
                    out replacementPublishedVersion);
                if (!replacementAfterWork)
                    yield return null;
            }
            bool replacementCompleted = false;
            YQSemanticChunkPublicationSnapshot replacementFinalSnapshot = default;
            yield return WaitForPublicationSnapshot(
                streamer,
                cancellationCoordinate,
                // note: Replacement publication owns the exact version returned by the streamer; later versions are also valid after a rebuild.
                snapshot => snapshot.traversable && snapshot.publicationVersion >= replacementPublishedVersion,
                 PublicationRecoveryTimeoutSeconds,
                (matched, snapshot) =>
                {
                    replacementCompleted = matched;
                    replacementFinalSnapshot = snapshot;
                });
            // note: The second output is the newly published owner version; compare that version with the retired work.
            bool cancellationPassed = replacementStarted && replacementAfterWork &&
                replacementPublishedVersion > retiredVersion && replacementCompleted &&
                streamer.TryGetPublicationSnapshot(cancellationCoordinate, out YQSemanticChunkPublicationSnapshot cancellationAfter) &&
                cancellationAfter.ownerEpoch != retiredOwnerEpoch;
            report.AppendLine("- cancellationReplacementGate: " + (cancellationPassed ? "PASS" : "FAIL") +
                " initialVersion=" + firstReplacementVersion +
                " replacementVersion=" + replacementPublishedVersion +
                " workObserved=" + workObserved +
                " retiredVersion=" + retiredVersion +
                " retiredOwnerEpoch=" + retiredOwnerEpoch +
                 " replacementAfterWorkVersion=" + replacementAfterWorkVersion +
                 " replacementStarted=" + replacementStarted +
                 " replacementAfterWork=" + replacementAfterWork +
                 " replacementCompleted=" + replacementCompleted +
                 " finalVersion=" + replacementFinalSnapshot.publicationVersion +
                 " finalState=" + replacementFinalSnapshot.lifecycle +
                 " finalTerrain=" + replacementFinalSnapshot.terrainReadiness +
                 " finalContent=" + replacementFinalSnapshot.requiredContentReady +
                 " finalOverlay=" + replacementFinalSnapshot.overlayReady +
                 " finalAppearance=" + replacementFinalSnapshot.appearanceReady +
                 " finalEcology=" + replacementFinalSnapshot.requiredEcologyReady +
                 " finalActivation=" + replacementFinalSnapshot.activationComplete +
                 " finalTerrainWork=" + replacementFinalSnapshot.terrainWorkActive +
                 " finalContentWork=" + replacementFinalSnapshot.contentWorkActive +
                 " finalContentQueued=" + replacementFinalSnapshot.contentWorkQueued +
                 " finalAppearanceWork=" + replacementFinalSnapshot.appearanceWorkActive +
                 " finalEcologyWork=" + replacementFinalSnapshot.ecologyWorkActive +
                 " finalLifecycleWork=" + replacementFinalSnapshot.lifecycleWorkPending +
                 " finalFailure=" + replacementFinalSnapshot.failureReason +
                 " replacementOwnerEpochChanged=" + (replacementStarted && streamer.TryGetPublicationSnapshot(cancellationCoordinate, out YQSemanticChunkPublicationSnapshot replacementSnapshot) && replacementSnapshot.ownerEpoch != retiredOwnerEpoch));
            WriteReport(report, false, false);
            allPassed &= cancellationPassed;
            streamer.EndPublicationVerificationScenario(cancellationCoordinate);

            Vector2Int retryCoordinate = new Vector2Int(int.MinValue, int.MinValue);
            bool retryOwnerPrepared = false;
            double retryOwnerStartedAt = Time.realtimeSinceStartupAsDouble;
            yield return PrepareAppearanceRetryOwner(
                streamer,
                usedCoordinates,
                retryOwnerStartedAt + RecoveryWitnessTimeoutSeconds,
                (prepared, coordinate) =>
                {
                    retryOwnerPrepared = prepared;
                    retryCoordinate = coordinate;
                });
            if (!retryOwnerPrepared)
                throw new InvalidOperationException("no retry recovery coordinate became available after " +
                    (Time.realtimeSinceStartupAsDouble - retryOwnerStartedAt).ToString("0.000", CultureInfo.InvariantCulture) + "s");
            bool retryPrepared = streamer.BeginPublicationVerificationScenario(
                    retryCoordinate,
                    false,
                    false,
                    false,
                    YQPlayerFollowingSemanticChunkStreamer.MaximumTerrainAppearanceRetries + 1,
                    0);
            bool retryExhausted = false;
            YQSemanticChunkPublicationSnapshot retrySnapshot = default;
            double retryStartedAt = Time.realtimeSinceStartupAsDouble;
            yield return WaitForPublicationSnapshot(
                streamer,
                retryCoordinate,
                snapshot => snapshot.hasExplicitFailure &&
                    snapshot.failureReason.IndexOf("appearance", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    snapshot.failureReason.IndexOf("InvalidOperationException", StringComparison.Ordinal) >= 0 &&
                    snapshot.appearanceRetryCount == YQPlayerFollowingSemanticChunkStreamer.MaximumTerrainAppearanceRetries &&
                    !snapshot.appearanceWorkActive && !snapshot.appearanceRetryScheduled,
                 AppearanceRetryExhaustionTimeoutSeconds,
                (matched, snapshot) =>
                {
                    retryExhausted = matched;
                    retrySnapshot = snapshot;
                });
            streamer.EndPublicationVerificationScenario(retryCoordinate);
            report.AppendLine("- retryExhaustionGate: " + (retryPrepared && retryExhausted ? "PASS" : "FAIL") +
                " prepared=" + retryPrepared +
                " matched=" + retryExhausted +
                " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - retryStartedAt).ToString("0.000", CultureInfo.InvariantCulture) +
                " timeoutSeconds=" + AppearanceRetryExhaustionTimeoutSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " coordinate=" + retryCoordinate +
                " version=" + retrySnapshot.publicationVersion +
                " retryCount=" + retrySnapshot.appearanceRetryCount +
                " appearanceWork=" + retrySnapshot.appearanceWorkActive +
                " appearanceRetryScheduled=" + retrySnapshot.appearanceRetryScheduled +
                " explicitFailure=" + retrySnapshot.hasExplicitFailure +
                " failureReason=" + retrySnapshot.failureReason);
            WriteReport(report, false, false);
            allPassed &= retryPrepared && retryExhausted;
            streamer.EndPublicationVerificationScenario(retryCoordinate);

            Vector2Int lagCoordinate = new Vector2Int(int.MinValue, int.MinValue);
            bool lagOwnerPrepared = false;
            double lagOwnerStartedAt = Time.realtimeSinceStartupAsDouble;
            yield return PrepareAppearanceRetryOwner(
                streamer,
                usedCoordinates,
                lagOwnerStartedAt + RecoveryWitnessTimeoutSeconds,
                (prepared, coordinate) =>
                {
                    lagOwnerPrepared = prepared;
                    lagCoordinate = coordinate;
                });
            if (!lagOwnerPrepared)
                throw new InvalidOperationException("no lag recovery coordinate became available after " +
                    (Time.realtimeSinceStartupAsDouble - lagOwnerStartedAt).ToString("0.000", CultureInfo.InvariantCulture) + "s");
            bool lagPrepared = streamer.BeginPublicationVerificationScenario(
                    lagCoordinate,
                    false,
                    true,
                    false,
                    0,
                    0);
            bool lagHeld = false;
            YQSemanticChunkPublicationSnapshot lagHoldSnapshot = default;
            double lagHoldStartedAt = Time.realtimeSinceStartupAsDouble;
            yield return WaitForPublicationSnapshot(
                streamer,
                lagCoordinate,
                snapshot => snapshot.appearanceWorkActive,
                AppearanceLagRecoveryTimeoutSeconds,
                (matched, snapshot) =>
                {
                    lagHeld = matched;
                    lagHoldSnapshot = snapshot;
                });
            double lagHoldElapsedSeconds = Time.realtimeSinceStartupAsDouble - lagHoldStartedAt;
            bool motorStayedValidDuringLag = true;
            bool lagInputObserved = false;
            bool lagMoved = false;
            yield return DriveProductionInput(
                keyboard,
                streamer,
                motor,
                player,
                characterController,
                report,
                0.75f,
                UnityEngine.InputSystem.Key.W,
                value => motorStayedValidDuringLag &= value,
                (inputObserved, moved, valid) =>
                {
                    lagInputObserved |= inputObserved;
                    lagMoved |= moved;
                });
            streamer.ReleasePublicationVerificationStage(lagCoordinate, YQPublicationVerificationStage.Appearance);
            bool lagReleased = false;
            YQSemanticChunkPublicationSnapshot lagReleaseSnapshot = default;
            double lagReleaseStartedAt = Time.realtimeSinceStartupAsDouble;
            yield return WaitForPublicationSnapshot(
                streamer,
                lagCoordinate,
                snapshot => snapshot.appearanceReady,
                AppearanceLagRecoveryTimeoutSeconds,
                (matched, snapshot) =>
                {
                    lagReleased = matched;
                    lagReleaseSnapshot = snapshot;
                });
            streamer.EndPublicationVerificationScenario(lagCoordinate);
            bool lagPassed = lagPrepared && lagHeld && motorStayedValidDuringLag && lagReleased;
            bool lagInconclusive = !lagInputObserved && lagPassed;
            report.AppendLine("- lagInjectionGate: " + (lagInconclusive ? "INCONCLUSIVE" : lagPassed ? "PASS" : "FAIL") +
                " prepared=" + lagPrepared +
                " held=" + lagHeld +
                " motorValid=" + motorStayedValidDuringLag +
                " released=" + lagReleased +
                " holdElapsedSeconds=" + lagHoldElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " holdTimeoutSeconds=" + AppearanceLagRecoveryTimeoutSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " releaseElapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - lagReleaseStartedAt).ToString("0.000", CultureInfo.InvariantCulture) +
                " holdPublicationVersion=" + lagHoldSnapshot.publicationVersion +
                " holdAppearanceWork=" + lagHoldSnapshot.appearanceWorkActive +
                " holdFailure=" + lagHoldSnapshot.failureReason +
                " releasePublicationVersion=" + lagReleaseSnapshot.publicationVersion +
                " releaseAppearanceReady=" + lagReleaseSnapshot.appearanceReady +
                " releaseFailure=" + lagReleaseSnapshot.failureReason +
                " inputObserved=" + lagInputObserved +
                " moved=" + lagMoved +
                " coordinate=" + lagCoordinate);
            WriteReport(report, false, false);
            allPassed &= lagInconclusive || lagPassed;
            recoveryInputDeliveryInconclusive |= lagInconclusive;

            Vector2Int groundCoordinate = new Vector2Int(int.MinValue, int.MinValue);
            // note: Create ordinary replacement work explicitly; a fully drained terrain queue is a valid production state and must not make this fault probe disappear.
            bool groundWitnessPrepared = TryReplaceRecoveryOwner(streamer, usedCoordinates,
                out groundCoordinate, out int groundRetiredVersion, out int groundReplacementVersion);
            report.AppendLine("- missingGroundWitness: " + (groundWitnessPrepared ? "PASS" : "FAIL") +
                " coordinate=" + groundCoordinate + " retiredVersion=" + groundRetiredVersion +
                " replacementVersion=" + groundReplacementVersion);
            WriteReport(report, false, false);
            bool groundPrepared = false;
            long groundWorkIdAtStart = 0;
            int groundOwnerEpoch = 0;
            string groundPhaseAtStart = string.Empty;
            double groundBarrierDeadline = Time.realtimeSinceStartupAsDouble + PublicationRecoveryTimeoutSeconds;
            while (groundWitnessPrepared && !groundPrepared && Time.realtimeSinceStartupAsDouble < groundBarrierDeadline)
            {
                // note: Attach only when this replacement reaches its real collider boundary through the unchanged production queue.
                if (streamer.TryGetPublicationSnapshot(groundCoordinate, out YQSemanticChunkPublicationSnapshot candidate) &&
                    candidate.terrainWorkId != 0 && string.Equals(candidate.terrainWorkPhase, "colliderSync", StringComparison.Ordinal))
                {
                    groundPrepared = streamer.TryBeginActiveTerrainCollisionVerificationScenario(
                        out Vector2Int heldCoordinate, out groundWorkIdAtStart, out groundPhaseAtStart);
                    if (groundPrepared && (heldCoordinate != groundCoordinate || groundWorkIdAtStart != candidate.terrainWorkId))
                    {
                        streamer.EndPublicationVerificationScenario(heldCoordinate);
                        groundPrepared = false;
                    }
                    if (groundPrepared)
                        groundOwnerEpoch = candidate.ownerEpoch;
                }
                if (!groundPrepared)
                    yield return null;
            }
            report.AppendLine("- missingGroundBarrier: " + (groundPrepared ? "PASS" : "FAIL") +
                " coordinate=" + groundCoordinate + " workId=" + groundWorkIdAtStart + " ownerEpoch=" + groundOwnerEpoch);
            WriteReport(report, false, false);
            bool groundHeld = false;
            long groundWorkIdAtHold = 0;
            bool groundQueuedAtHold = false;
            string groundPhaseAtHold = string.Empty;
            float groundElapsedAtHold = 0f;
            if (groundPrepared)
            {
                // note: Require the same active owner to prove that the hold is at the real collision publication boundary.
                yield return WaitForPublicationSnapshot(
                    streamer,
                    groundCoordinate,
                    snapshot => snapshot.terrainCollisionPublicationHeld && snapshot.terrainWorkId == groundWorkIdAtStart &&
                        snapshot.ownerEpoch == groundOwnerEpoch,
                    ActiveTerrainBarrierObservationTimeoutSeconds,
                    (matched, snapshot) =>
                    {
                        groundHeld = matched;
                        groundWorkIdAtHold = snapshot.terrainWorkId;
                        groundQueuedAtHold = snapshot.terrainWorkQueued;
                        groundPhaseAtHold = snapshot.terrainWorkPhase ?? string.Empty;
                        groundElapsedAtHold = snapshot.terrainWorkElapsedSeconds;
                    });
            }
            report.AppendLine("- missingGroundHeld: " + (groundHeld ? "PASS" : "FAIL") + " workId=" + groundWorkIdAtHold);
            WriteReport(report, false, false);
            bool motorStayedValidBeforeGroundRelease = true;
            bool groundInputObserved = false;
            bool groundMoved = false;
            bool groundViewCoverageDuringMovement = true;
            if (groundHeld)
            {
                // note: Verify live ground coverage while ordinary movement may preempt and replace the held terrain worker.
                yield return DriveProductionInput(
                    keyboard,
                    streamer,
                    motor,
                    player,
                    characterController,
                    report,
                    0.75f,
                    UnityEngine.InputSystem.Key.S,
                    value => motorStayedValidBeforeGroundRelease &= value,
                    (inputObserved, moved, valid) =>
                    {
                        groundInputObserved |= inputObserved;
                        groundMoved |= moved;
                    },
                    frameSafety: () =>
                    {
                        bool covered = streamer.TryValidateGroundViewCoverage(out _);
                        groundViewCoverageDuringMovement &= covered;
                        return covered;
                    });
            }
            YQSemanticChunkPublicationSnapshot heldGroundSnapshot = default;
            bool groundStillUnpublished = groundHeld && streamer.TryGetPublicationSnapshot(groundCoordinate, out heldGroundSnapshot) &&
                // note: Input can legitimately preempt terrain work; only the same held owner proves the missing-ground observation remained active.
                heldGroundSnapshot.terrainCollisionPublicationHeld && heldGroundSnapshot.terrainWorkId == groundWorkIdAtStart &&
                heldGroundSnapshot.ownerEpoch == groundOwnerEpoch &&
                heldGroundSnapshot.terrainReadiness < YQTerrainReadinessState.CollisionReady &&
                !heldGroundSnapshot.traversable;
            if (groundPrepared)
                streamer.ReleasePublicationVerificationStage(groundCoordinate, YQPublicationVerificationStage.TerrainCollision);
            bool groundReleased = false;
            YQSemanticChunkPublicationSnapshot groundReleaseSnapshot = default;
            if (groundPrepared)
            {
                yield return WaitForPublicationSnapshot(
                    streamer,
                    groundCoordinate,
                    snapshot => snapshot.terrainReadiness >= YQTerrainReadinessState.CollisionReady &&
                        snapshot.ownerEpoch >= groundOwnerEpoch,
                    ActiveTerrainBarrierObservationTimeoutSeconds,
                    (matched, snapshot) =>
                    {
                        groundReleased = matched;
                        groundReleaseSnapshot = snapshot;
                    });
                streamer.EndPublicationVerificationScenario(groundCoordinate);
                usedCoordinates.Add(groundCoordinate);
            }
            report.AppendLine("- missingGroundReleased: " + (groundReleased ? "PASS" : "FAIL") + " ownerEpoch=" + groundReleaseSnapshot.ownerEpoch);
            WriteReport(report, false, false);
            bool groundPassed = groundPrepared && groundHeld && (groundStillUnpublished || groundViewCoverageDuringMovement) &&
                motorStayedValidBeforeGroundRelease && groundReleased;
            bool groundInconclusive = !groundInputObserved && groundPassed;
            report.AppendLine("- missingGroundGate: " + (groundInconclusive ? "INCONCLUSIVE" : groundPassed ? "PASS" : "FAIL") +
                " prepared=" + groundPrepared +
                " held=" + groundHeld +
                " stillUnpublished=" + groundStillUnpublished +
                " groundViewCoverageEveryFrame=" + groundViewCoverageDuringMovement +
                " motorValid=" + motorStayedValidBeforeGroundRelease +
                " released=" + groundReleased +
                " holdWorkId=" + groundWorkIdAtHold +
                " holdPhase=" + groundPhaseAtHold +
                " queuedAtHold=" + groundQueuedAtHold +
                " releaseWorkId=" + groundReleaseSnapshot.terrainWorkId +
                " releasePhase=" + (groundReleaseSnapshot.terrainWorkPhase ?? string.Empty) +
                " releaseElapsedSeconds=" + groundReleaseSnapshot.terrainWorkElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " workActive=" + groundReleaseSnapshot.terrainWorkActive +
                " workQueued=" + groundReleaseSnapshot.terrainWorkQueued +
                " retryScheduled=" + groundReleaseSnapshot.terrainRetryScheduled +
                " retryCount=" + groundReleaseSnapshot.terrainRetryCount +
                " explicitFailure=" + groundReleaseSnapshot.hasExplicitFailure +
                " failureReason=" + (groundReleaseSnapshot.failureReason ?? string.Empty) +
                " startWorkId=" + groundWorkIdAtStart +
                " startPhase=" + groundPhaseAtStart +
                " holdElapsedSeconds=" + groundElapsedAtHold.ToString("0.000", CultureInfo.InvariantCulture) +
                " inputObserved=" + groundInputObserved +
                " moved=" + groundMoved +
                " coordinate=" + groundCoordinate);
            WriteReport(report, false, false);
            allPassed &= groundInconclusive || groundPassed;
            recoveryInputDeliveryInconclusive |= groundInconclusive;

            Vector2Int orderAppearanceFirst = new Vector2Int(int.MinValue, int.MinValue);
            Vector2Int orderEcologyFirst = new Vector2Int(int.MinValue, int.MinValue);
            int orderAppearancePreviousVersion = 0;
            int orderAppearanceReplacementVersion = 0;
            int orderEcologyPreviousVersion = 0;
            int orderEcologyReplacementVersion = 0;
            double orderWitnessStarted = Time.realtimeSinceStartupAsDouble;
            double orderWitnessDeadline = orderWitnessStarted + RecoveryWitnessTimeoutSeconds;
            bool appearanceOrderPrepared = false;
            bool ecologyOrderPrepared = false;
            yield return PrepareReplacementScenarioOwner(
                streamer,
                usedCoordinates,
                true,
                true,
                orderWitnessDeadline,
                (prepared, coordinate, previousVersion, replacementVersion) =>
                {
                    appearanceOrderPrepared = prepared;
                    orderAppearanceFirst = coordinate;
                    orderAppearancePreviousVersion = previousVersion;
                    orderAppearanceReplacementVersion = replacementVersion;
                });
            if (!appearanceOrderPrepared)
                throw new InvalidOperationException("appearance-first completion-order owner was unavailable after " +
                    (Time.realtimeSinceStartupAsDouble - orderWitnessStarted).ToString("0.000", CultureInfo.InvariantCulture) + "s");
            // note: Keep appearance held until ecology publishes so this witness actually proves ecology cannot publish a traversable cell first.
            yield return PrepareReplacementScenarioOwner(
                streamer,
                usedCoordinates,
                true,
                true,
                orderWitnessDeadline,
                (prepared, coordinate, previousVersion, replacementVersion) =>
                {
                    ecologyOrderPrepared = prepared;
                    orderEcologyFirst = coordinate;
                    orderEcologyPreviousVersion = previousVersion;
                    orderEcologyReplacementVersion = replacementVersion;
                });
            if (!ecologyOrderPrepared)
                throw new InvalidOperationException("ecology-first completion-order owner was unavailable after " +
                    (Time.realtimeSinceStartupAsDouble - orderWitnessStarted).ToString("0.000", CultureInfo.InvariantCulture) + "s");
            bool orderPrepared = appearanceOrderPrepared && ecologyOrderPrepared;
            // note: Persist each observed order boundary before advancing; a timeout or interrupted run must retain its partial evidence.
            report.AppendLine("- completionOrderPrepared: " + (orderPrepared ? "PASS" : "FAIL") +
                " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - orderWitnessStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                " appearanceVersion=" + orderAppearancePreviousVersion + "->" + orderAppearanceReplacementVersion +
                " ecologyVersion=" + orderEcologyPreviousVersion + "->" + orderEcologyReplacementVersion);
            WriteReport(report, false, false);
            bool firstAppearanceStarted = false;
            bool firstAppearanceReady = false;
            bool firstEcologyReady = false;
            yield return ObservePublicationPhase(report, "appearanceFirst.worker", streamer, orderAppearanceFirst, orderPrepared, snapshot => snapshot.appearanceWorkActive, matched => firstAppearanceStarted = matched);
            streamer.ReleasePublicationVerificationStage(orderAppearanceFirst, YQPublicationVerificationStage.Appearance);
            yield return ObservePublicationPhase(report, "appearanceFirst.appearance", streamer, orderAppearanceFirst, firstAppearanceStarted, snapshot => snapshot.appearanceReady, matched => firstAppearanceReady = matched);
            YQSemanticChunkPublicationSnapshot appearanceFirstSnapshot;
            // note: Collision traversability is independent of semantic appearance and ecology publication, so this witness checks the held stage directly.
            bool appearanceFirstRemainedBlocked = streamer.TryGetPublicationSnapshot(orderAppearanceFirst, out appearanceFirstSnapshot) &&
                appearanceFirstSnapshot.appearanceReady && !appearanceFirstSnapshot.requiredEcologyReady;
            report.AppendLine("- appearanceFirst.blockedUntilEcology: " + (firstAppearanceReady && appearanceFirstRemainedBlocked ? "PASS" : "FAIL"));
            WriteReport(report, false, false);
            streamer.ReleasePublicationVerificationStage(orderAppearanceFirst, YQPublicationVerificationStage.RequiredEcology);
            yield return ObservePublicationPhase(report, "appearanceFirst.complete", streamer, orderAppearanceFirst, firstAppearanceReady, snapshot => snapshot.requiredEcologyReady, matched => firstEcologyReady = matched);

            bool secondEcologyStarted = false;
            bool secondEcologyReady = false;
            yield return ObservePublicationPhase(report, "ecologyFirst.worker", streamer, orderEcologyFirst, orderPrepared, snapshot => snapshot.ecologyWorkActive, matched => secondEcologyStarted = matched);
            streamer.ReleasePublicationVerificationStage(orderEcologyFirst, YQPublicationVerificationStage.RequiredEcology);
            yield return ObservePublicationPhase(report, "ecologyFirst.ecology", streamer, orderEcologyFirst, secondEcologyStarted, snapshot => snapshot.requiredEcologyReady, matched => secondEcologyReady = matched);
            YQSemanticChunkPublicationSnapshot ecologyFirstSnapshot;
            bool ecologyFirstRemainedBlocked = streamer.TryGetPublicationSnapshot(orderEcologyFirst, out ecologyFirstSnapshot) &&
                ecologyFirstSnapshot.requiredEcologyReady && !ecologyFirstSnapshot.appearanceReady;
            report.AppendLine("- ecologyFirst.blockedUntilAppearance: " + (secondEcologyReady && ecologyFirstRemainedBlocked ? "PASS" : "FAIL"));
            WriteReport(report, false, false);
            streamer.ReleasePublicationVerificationStage(orderEcologyFirst, YQPublicationVerificationStage.Appearance);
            // note: Completion-order verification ends when both held publication stages are ready; collision coverage is asserted by separate movement gates.
            bool secondAppearanceReady = false;
            yield return ObservePublicationPhase(report, "ecologyFirst.complete", streamer, orderEcologyFirst, secondEcologyReady, snapshot => snapshot.appearanceReady, matched => secondAppearanceReady = matched);
            streamer.EndPublicationVerificationScenario(orderAppearanceFirst);
            streamer.EndPublicationVerificationScenario(orderEcologyFirst);
            bool orderPassed = orderPrepared && firstAppearanceReady && appearanceFirstRemainedBlocked && firstEcologyReady &&
                secondEcologyReady && ecologyFirstRemainedBlocked && secondAppearanceReady;
            report.AppendLine("- completionOrderGate: " + (orderPassed ? "PASS" : "FAIL") +
                " prepared=" + orderPrepared +
                " firstAppearanceReady=" + firstAppearanceReady +
                " appearanceBlocked=" + appearanceFirstRemainedBlocked +
                " firstEcologyReady=" + firstEcologyReady +
                " secondEcologyReady=" + secondEcologyReady +
                " ecologyBlocked=" + ecologyFirstRemainedBlocked +
                " secondAppearanceReady=" + secondAppearanceReady +
                " appearanceCoordinate=" + orderAppearanceFirst +
                " ecologyCoordinate=" + orderEcologyFirst);
            WriteReport(report, false, false);
            allPassed &= orderPassed;

            // note: Track a live non-authored terrain owner through natural unload and reverse traversal so the gate never mistakes a distant scene-owned prop for a revisit.
            Vector2Int revisitCoordinate = streamer.CurrentChunk;
            YQSemanticChunkPublicationSnapshot revisitStartSnapshot = default;
            bool revisitCandidateCaptured = false;
            bool captureStoppedAtInvalidTraversal = false;
            bool unloadMotorValid = true;
            bool captureInputObserved = false;
            bool captureMoved = false;
            string captureTraversalFailure = string.Empty;
            Vector2Int lastCaptureCoordinate = revisitCoordinate;
            int captureCandidateAttempts = 0;
            int captureCandidatesScanned = 0;
            int captureRejectedNoOwner = 0;
            int captureRejectedAuthored = 0;
            int captureRejectedExplicitFailure = 0;
            int captureRejectedPhysical = 0;
            int captureRejectedTraversability = 0;
            int captureRejectedTerrain = 0;
            int captureRejectedSiteLease = 0;
            int captureRejectedVerificationPin = 0;
            string captureCandidateRouteFailure = string.Empty;
            double captureStartedAt = Time.realtimeSinceStartupAsDouble;
            Vector3 lastCapturePosition = player.position;
            Vector3 captureApproachHeading = Vector3.zero;
            Func<bool> captureCurrentStreamedOwner = () =>
            {
                Vector2Int currentCoordinate = streamer.CurrentChunk;
                // note: Capture only a streamed cell without a site or verification lease so this lifecycle witness is expected to unload naturally after the player leaves.
                if (!streamer.TryGetPublicationSnapshot(currentCoordinate, out YQSemanticChunkPublicationSnapshot snapshot) ||
                    snapshot.authoredTerrain || snapshot.hasExplicitFailure || !snapshot.physicalRepresentation || !snapshot.traversable ||
                    snapshot.terrainReadiness < YQTerrainReadinessState.CollisionReady ||
                    snapshot.siteTerrainDependency || snapshot.siteTerrainHandoffPending ||
                    snapshot.publicationVerificationPinned)
                    return false;
                revisitCoordinate = currentCoordinate;
                revisitStartSnapshot = snapshot;
                revisitCandidateCaptured = true;
                return true;
            };

            // note: Snapshot a small stable neighborhood first so the probe walks to a real published owner instead of zigzagging indefinitely in one empty cell.
            const int captureCandidateSearchRadius = 2;
            const int maximumCaptureCandidates = 8;
            const double captureCandidateTimeoutSeconds = 90d;
            List<Vector2Int> captureCandidates = new List<Vector2Int>(maximumCaptureCandidates);
            List<float> captureCandidateDistances = new List<float>(maximumCaptureCandidates);
            float captureChunkSize = Mathf.Max(32f, streamer.chunkWorldSize);
            for (int ring = 0; ring <= captureCandidateSearchRadius; ring++)
            {
                for (int offsetX = -ring; offsetX <= ring; offsetX++)
                for (int offsetZ = -ring; offsetZ <= ring; offsetZ++)
                {
                    if (Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetZ)) != ring)
                        continue;

                    Vector2Int coordinate = streamer.CurrentChunk + new Vector2Int(offsetX, offsetZ);
                    captureCandidatesScanned++;
                    if (!streamer.TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot snapshot))
                    {
                        captureRejectedNoOwner++;
                        continue;
                    }
                    if (snapshot.authoredTerrain)
                    {
                        captureRejectedAuthored++;
                        continue;
                    }
                    if (snapshot.hasExplicitFailure)
                    {
                        captureRejectedExplicitFailure++;
                        continue;
                    }
                    if (!snapshot.physicalRepresentation)
                    {
                        captureRejectedPhysical++;
                        continue;
                    }
                    if (!snapshot.traversable)
                    {
                        captureRejectedTraversability++;
                        continue;
                    }
                    if (snapshot.terrainReadiness < YQTerrainReadinessState.CollisionReady)
                    {
                        captureRejectedTerrain++;
                        continue;
                    }
                    if (snapshot.siteTerrainDependency || snapshot.siteTerrainHandoffPending)
                    {
                        captureRejectedSiteLease++;
                        continue;
                    }
                    if (snapshot.publicationVerificationPinned)
                    {
                        captureRejectedVerificationPin++;
                        continue;
                    }

                    float centerX = YQContinuousWorldFeatureAuthority.WorldGridOrigin +
                        (coordinate.x + 0.5f) * captureChunkSize;
                    float centerZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin +
                        (coordinate.y + 0.5f) * captureChunkSize;
                    float dx = centerX - player.position.x;
                    float dz = centerZ - player.position.z;
                    float distanceSquared = dx * dx + dz * dz;
                    int insertAt = 0;
                    while (insertAt < captureCandidateDistances.Count &&
                           captureCandidateDistances[insertAt] <= distanceSquared)
                        insertAt++;
                    if (insertAt >= maximumCaptureCandidates)
                        continue;
                    captureCandidates.Insert(insertAt, coordinate);
                    captureCandidateDistances.Insert(insertAt, distanceSquared);
                    if (captureCandidates.Count > maximumCaptureCandidates)
                    {
                        captureCandidates.RemoveAt(captureCandidates.Count - 1);
                        captureCandidateDistances.RemoveAt(captureCandidateDistances.Count - 1);
                    }
                }
            }

            // note: Try nearest published owners under a shared wall-clock deadline; every leg still uses the production motor and rejects an invalid traversal immediately.
            if (captureCandidates.Count == 0)
            {
                captureCandidateRouteFailure = "no eligible published owner within cell radius " + captureCandidateSearchRadius +
                    "; rejected noOwner=" + captureRejectedNoOwner +
                    ",authored=" + captureRejectedAuthored +
                    ",explicitFailure=" + captureRejectedExplicitFailure +
                    ",physical=" + captureRejectedPhysical +
                    ",traversable=" + captureRejectedTraversability +
                    ",terrain=" + captureRejectedTerrain +
                    ",siteLease=" + captureRejectedSiteLease +
                    ",verificationPin=" + captureRejectedVerificationPin;
                report.AppendLine("- unloadRevisitCandidateScan: FAIL scanned=" + captureCandidatesScanned +
                    " radius=" + captureCandidateSearchRadius +
                    " eligible=0 reason=" + captureCandidateRouteFailure);
                WriteReport(report, false, false);
            }
            else
            {
                report.AppendLine("- unloadRevisitCandidateScan: PASS scanned=" + captureCandidatesScanned +
                    " radius=" + captureCandidateSearchRadius +
                    " eligible=" + captureCandidates.Count +
                    " rejectedNoOwner=" + captureRejectedNoOwner +
                    " rejectedAuthored=" + captureRejectedAuthored +
                    " rejectedExplicitFailure=" + captureRejectedExplicitFailure +
                    " rejectedPhysical=" + captureRejectedPhysical +
                    " rejectedTraversability=" + captureRejectedTraversability +
                    " rejectedTerrain=" + captureRejectedTerrain +
                    " rejectedSiteLease=" + captureRejectedSiteLease +
                    " rejectedVerificationPin=" + captureRejectedVerificationPin);
                WriteReport(report, false, false);
            }
            double captureDeadline = captureStartedAt + Math.Min(PublicationRecoveryTimeoutSeconds, captureCandidateTimeoutSeconds);
            while (!captureCurrentStreamedOwner() && !captureStoppedAtInvalidTraversal &&
                   captureCandidateAttempts < captureCandidates.Count &&
                   Time.realtimeSinceStartupAsDouble < captureDeadline)
            {
                Vector2Int candidateCoordinate = captureCandidates[captureCandidateAttempts++];
                if (candidateCoordinate == streamer.CurrentChunk && captureCurrentStreamedOwner())
                    break;

                float targetX = YQContinuousWorldFeatureAuthority.WorldGridOrigin +
                    (candidateCoordinate.x + 0.5f) * captureChunkSize;
                float targetZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin +
                    (candidateCoordinate.y + 0.5f) * captureChunkSize;
                Vector3 targetDelta = new Vector3(targetX - player.position.x, 0f, targetZ - player.position.z);
                Vector3 targetDirection = targetDelta.sqrMagnitude > 0.0001f ? targetDelta.normalized : Vector3.zero;
                KeyboardStateForMotorDirection(motor, targetDirection, out Vector2 targetMoveInput);
                ResolveMovementKeys(targetMoveInput, out UnityEngine.InputSystem.Key captureDirection,
                    out UnityEngine.InputSystem.Key captureSecondaryDirection);
                if (captureDirection == UnityEngine.InputSystem.Key.None)
                {
                    captureCandidateRouteFailure = "candidate heading resolved to no motor input coordinate=" + candidateCoordinate;
                    report.AppendLine("- unloadRevisitCandidateAttempt: index=" + captureCandidateAttempts +
                        " coordinate=" + candidateCoordinate +
                        " reached=False inputObserved=False moved=False reason=" + captureCandidateRouteFailure);
                    WriteReport(report, false, false);
                    continue;
                }

                float targetDistance = PlanarDistance(player.position, new Vector3(targetX, player.position.y, targetZ));
                float captureRouteSeconds = Mathf.Clamp(targetDistance / Mathf.Max(1f, motor.walkSpeed) * 1.75f + 3f, 3f, 90f);
                captureRouteSeconds = Mathf.Min(captureRouteSeconds,
                    Mathf.Max(0.05f, (float)(captureDeadline - Time.realtimeSinceStartupAsDouble)));
                string candidateArrivalFailure = string.Empty;
                bool candidateInputObserved = false;
                bool candidateMoved = false;
                Func<bool> stopAtCandidate = () =>
                {
                    lastCaptureCoordinate = streamer.CurrentChunk;
                    if (lastCaptureCoordinate == candidateCoordinate)
                    {
                        if (captureCurrentStreamedOwner())
                            return true;
                        candidateArrivalFailure = "owner no longer met publication filters at arrival coordinate=" + candidateCoordinate;
                        return true;
                    }

                    bool valid = streamer.TryValidateCurrentTraversability(
                        player.position,
                        characterController.radius,
                        characterController.skinWidth,
                        out captureTraversalFailure);
                    captureStoppedAtInvalidTraversal |= !valid;
                    if (!valid)
                    {
                        candidateArrivalFailure = "invalid traversal before candidate coordinate=" + candidateCoordinate + " reason=" + captureTraversalFailure;
                        return true;
                    }
                    return false;
                };
                yield return DriveProductionInput(
                    keyboard,
                    streamer,
                    motor,
                    player,
                    characterController,
                    report,
                    captureRouteSeconds,
                    captureDirection,
                    value => unloadMotorValid &= value,
                    (inputObserved, moved, valid) =>
                    {
                        captureInputObserved |= inputObserved;
                        captureMoved |= moved;
                        candidateInputObserved |= inputObserved;
                        candidateMoved |= moved;
                    },
                    stopAtCandidate,
                    captureSecondaryDirection,
                    noProgressTimeoutSeconds: RecoveryNoProgressTimeoutSeconds,
                    positionObserved: position =>
                    {
                        // note: Preserve the physical approach heading across input release so entering a cell does not reverse the unload route toward its entry edge.
                        Vector3 observedStep = position - lastCapturePosition;
                        observedStep.y = 0f;
                        if (observedStep.sqrMagnitude > 0.0001f)
                            captureApproachHeading = observedStep.normalized;
                        lastCapturePosition = position;
                    });
                if (revisitCandidateCaptured)
                    captureCandidateRouteFailure = string.Empty;
                else
                    captureCandidateRouteFailure = string.IsNullOrWhiteSpace(candidateArrivalFailure)
                        ? "candidate route ended before published owner was reached coordinate=" + candidateCoordinate
                        : candidateArrivalFailure;
                report.AppendLine("- unloadRevisitCandidateAttempt: index=" + captureCandidateAttempts +
                    " coordinate=" + candidateCoordinate +
                    " reached=" + revisitCandidateCaptured +
                    " inputObserved=" + candidateInputObserved +
                    " moved=" + candidateMoved +
                    " currentChunk=" + streamer.CurrentChunk +
                    " reason=" + captureCandidateRouteFailure +
                    " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - captureStartedAt).ToString("0.000", CultureInfo.InvariantCulture));
                WriteReport(report, false, false);
            }
            float captureElapsedSeconds = (float)(Time.realtimeSinceStartupAsDouble - captureStartedAt);
            report.AppendLine("- unloadRevisitCandidate: " + (revisitCandidateCaptured ? "PASS" : "FAIL") +
                " coordinate=" + revisitCoordinate +
                " authored=" + revisitStartSnapshot.authoredTerrain +
                " physical=" + revisitStartSnapshot.physicalRepresentation +
                " traversable=" + revisitStartSnapshot.traversable +
                " terrain=" + revisitStartSnapshot.terrainReadiness +
                " publicationVersion=" + revisitStartSnapshot.publicationVersion +
                " ownerEpoch=" + revisitStartSnapshot.ownerEpoch +
                " siteDependency=" + revisitStartSnapshot.siteTerrainDependency +
                " siteHandoff=" + revisitStartSnapshot.siteTerrainHandoffPending +
                " verificationPin=" + revisitStartSnapshot.publicationVerificationPinned +
                " captureInputObserved=" + captureInputObserved +
                " captureMoved=" + captureMoved +
                " candidateAttempts=" + captureCandidateAttempts +
                " candidateScanRadius=" + captureCandidateSearchRadius +
                " candidatesScanned=" + captureCandidatesScanned +
                " eligibleCandidates=" + captureCandidates.Count +
                " rejectedNoOwner=" + captureRejectedNoOwner +
                " rejectedAuthored=" + captureRejectedAuthored +
                " rejectedExplicitFailure=" + captureRejectedExplicitFailure +
                " rejectedPhysical=" + captureRejectedPhysical +
                " rejectedTraversability=" + captureRejectedTraversability +
                " rejectedTerrain=" + captureRejectedTerrain +
                " rejectedSiteLease=" + captureRejectedSiteLease +
                " rejectedVerificationPin=" + captureRejectedVerificationPin +
                " candidateRouteFailure=" + captureCandidateRouteFailure +
                " currentChunk=" + lastCaptureCoordinate +
                " traversalBlocks=" + streamer.TraversalConstraintBlockCount +
                " traversalBlockCell=" + streamer.LastTraversalConstraintCell +
                " traversalFailure=" + captureTraversalFailure +
                " stoppedAtInvalidTraversal=" + captureStoppedAtInvalidTraversal +
                " elapsedSeconds=" + captureElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            WriteReport(report, false, false);

            int estimatedUnloadRadius = Mathf.Max(
                streamer.unloadRadius,
                Mathf.Max(streamer.generationRadius,
                    Mathf.Max(streamer.visualRadius,
                        Mathf.Max(streamer.visualPrewarmRadius, streamer.visualRetentionRadius))));
            float unloadTravelDistance = (estimatedUnloadRadius + 1.5f) * streamer.chunkWorldSize;
            // note: Real obstacle bypasses lengthen this motor-only route beyond its straight-line estimate; let the observed unload or traversal guard finish it within the existing bounded allowance.
            float unloadLegTimeoutSeconds = UnloadRevisitTimeoutSeconds;
            bool outboundInputObserved = false;
            bool outboundMoved = false;
            bool outboundStoppedAtInvalidTraversal = false;
            bool outboundRouteFailed = false;
            bool outsideRetentionBoundaryReached = false;
            bool outboundUsingLateralBypass = false;
            bool outboundBoundaryClearancePending = false;
            int lateralBypassOriginCell = int.MinValue;
            int lateralBypassSide = 1;
            int lateralBypassFailedSides = 0;
            bool unloadedObserved = false;
            YQSemanticChunkPublicationSnapshot unloadedSnapshot = default;
            int outboundDirectionAttempts = 0;
            double outboundStartedAt = Time.realtimeSinceStartupAsDouble;
            Vector3 unloadDestination = player.position;
            // note: Retain only successful physical positions so the return follows the same safe corridor in reverse.
            List<Vector3> outboundReturnWaypoints = new List<Vector3>(64);
            // note: Keep motor-observed samples close enough that reverse navigation preserves the actual outbound corridor through cell-local obstacles.
            Action<Vector3> captureReturnWaypointSample = position =>
            {
                if (outboundReturnWaypoints.Count == 0 ||
                    PlanarDistance(outboundReturnWaypoints[outboundReturnWaypoints.Count - 1], position) >= ReturnWaypointSampleDistanceMeters)
                    AppendReturnWaypoint(outboundReturnWaypoints, position);
            };
            if (revisitCandidateCaptured)
            {
                AppendReturnWaypoint(outboundReturnWaypoints, player.position);
                // note: Choose a world-space destination beyond the measured lifecycle radius so detours cannot end a long drive close to the witness.
                Vector3 revisitTargetForUnload = new Vector3(
                    YQContinuousWorldFeatureAuthority.WorldGridOrigin + (revisitCoordinate.x + 0.5f) * streamer.chunkWorldSize,
                    player.position.y,
                    YQContinuousWorldFeatureAuthority.WorldGridOrigin + (revisitCoordinate.y + 0.5f) * streamer.chunkWorldSize);
                // note: Continue the observed approach into the streamed world; the offset from the cell center points backward immediately after entry.
                Vector3 initialOutwardDirection = captureApproachHeading.sqrMagnitude > 0.01f
                    ? captureApproachHeading
                    : motor.PlanarVelocity;
                initialOutwardDirection.y = 0f;
                if (initialOutwardDirection.sqrMagnitude < 0.01f)
                    initialOutwardDirection = player.forward;
                initialOutwardDirection.y = 0f;
                initialOutwardDirection.Normalize();
                // note: A cardinal target clears the Chebyshev retention boundary with the shortest truthful player traversal.
                if (Mathf.Abs(initialOutwardDirection.x) >= Mathf.Abs(initialOutwardDirection.z))
                    initialOutwardDirection = new Vector3(Mathf.Sign(initialOutwardDirection.x), 0f, 0f);
                else
                    initialOutwardDirection = new Vector3(0f, 0f, Mathf.Sign(initialOutwardDirection.z));
                Vector3 lateralBypassDirection = Vector3.Cross(Vector3.up, initialOutwardDirection);
                unloadDestination = revisitTargetForUnload +
                    initialOutwardDirection * unloadTravelDistance;
                report.AppendLine("- unloadRevisitRoutePlan: approachHeading=" + captureApproachHeading.ToString("F3") +
                    " outwardHeading=" + initialOutwardDirection.ToString("F3") +
                    " destination=" + unloadDestination.ToString("F3") +
                    " retentionRadius=" + estimatedUnloadRadius);
                WriteReport(report, false, false);
                Func<bool> stopAfterNaturalUnload = () =>
                {
                    if (streamer.TryGetPublicationSnapshot(revisitCoordinate, out YQSemanticChunkPublicationSnapshot snapshot) &&
                        snapshot.lifecycle == YQSemanticChunkLifecycle.Unloaded &&
                        !snapshot.traversable &&
                        snapshot.publicationVersion > revisitStartSnapshot.publicationVersion)
                    {
                        unloadedSnapshot = snapshot;
                        unloadedObserved = true;
                        return true;
                    }
                    bool beyondUnloadRadius = false;
                    bool hasActiveRetentionDemand = false;
                    if (streamer.TryGetPublicationSnapshot(revisitCoordinate, out YQSemanticChunkPublicationSnapshot retentionSnapshot))
                    {
                        // note: Keep the real route running while camera, speed-runway, and site leases still legitimately retain the witness.
                        beyondUnloadRadius = retentionSnapshot.currentChunkDistance >
                            Mathf.Max(estimatedUnloadRadius, retentionSnapshot.effectiveUnloadRadius);
                        hasActiveRetentionDemand = retentionSnapshot.demanded ||
                            retentionSnapshot.canonicalPreparationDemanded ||
                            retentionSnapshot.predictedViewDemanded ||
                            retentionSnapshot.provisionalGroundPrefetchDemanded ||
                            retentionSnapshot.provisionalGroundTurnBufferDemanded ||
                            retentionSnapshot.siteTerrainDependency ||
                            retentionSnapshot.siteTerrainHandoffPending ||
                            retentionSnapshot.publicationVerificationPinned;
                    }
                    outsideRetentionBoundaryReached = beyondUnloadRadius && !hasActiveRetentionDemand;
                    bool valid = streamer.TryValidateCurrentTraversability(
                        player.position,
                        characterController.radius,
                        characterController.skinWidth,
                    out _);
                    outboundStoppedAtInvalidTraversal |= !valid;
                    return !valid || outsideRetentionBoundaryReached;
                };
                double outboundDeadline = outboundStartedAt + unloadLegTimeoutSeconds;
                while (!unloadedObserved &&
                       !outboundStoppedAtInvalidTraversal &&
                       !outboundRouteFailed &&
                       !outsideRetentionBoundaryReached &&
                       Time.realtimeSinceStartupAsDouble < outboundDeadline)
                {
                    if (outboundBoundaryClearancePending)
                    {
                        outboundBoundaryClearancePending = false;
                        Vector3 clearanceWorldDirection = -initialOutwardDirection;
                        Vector3 clearanceForward = player.forward;
                        clearanceForward.y = 0f;
                        clearanceForward = clearanceForward.sqrMagnitude > 0.0001f ? clearanceForward.normalized : Vector3.forward;
                        Vector3 clearanceRight = player.right;
                        clearanceRight.y = 0f;
                        clearanceRight = clearanceRight.sqrMagnitude > 0.0001f ? clearanceRight.normalized : Vector3.right;
                        Vector2 clearanceMoveInput = new Vector2(
                            Vector3.Dot(clearanceWorldDirection, clearanceRight),
                            Vector3.Dot(clearanceWorldDirection, clearanceForward));
                        ResolveMovementKeys(clearanceMoveInput, out UnityEngine.InputSystem.Key clearanceDirection,
                            out UnityEngine.InputSystem.Key clearanceSecondaryDirection);
                        Vector3 clearanceStartPosition = player.position;
                        Vector2Int clearanceStartChunk = streamer.CurrentChunk;
                        bool clearanceInputObserved = false;
                        bool clearanceMoved = false;
                        double clearanceStartedAt = Time.realtimeSinceStartupAsDouble;
                        // note: Step the real motor farther inside the published cell before a lateral bypass; measured edge drift can exceed capsule clearance during a long strafe.
                        yield return DriveProductionInput(
                            keyboard,
                            streamer,
                            motor,
                            player,
                            characterController,
                            report,
                            2f,
                            clearanceDirection,
                            value => unloadMotorValid &= value,
                            (inputObserved, moved, valid) =>
                            {
                                outboundInputObserved |= inputObserved;
                                outboundMoved |= moved;
                                clearanceInputObserved |= inputObserved;
                                clearanceMoved |= moved;
                            },
                            stopAfterNaturalUnload,
                            clearanceSecondaryDirection,
                            noProgressTimeoutSeconds: 2f,
                            positionObserved: captureReturnWaypointSample);
                        if (clearanceMoved)
                            AppendReturnWaypoint(outboundReturnWaypoints, player.position);
                        bool clearanceValid = streamer.TryValidateCurrentTraversability(
                            player.position,
                            characterController.radius,
                            characterController.skinWidth,
                            out _);
                        outboundStoppedAtInvalidTraversal |= !clearanceValid;
                        report.AppendLine("- unloadRevisitBoundaryClearance: input=" + clearanceDirection +
                            (clearanceSecondaryDirection == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + clearanceSecondaryDirection) +
                            " startChunk=" + clearanceStartChunk +
                            " currentChunk=" + streamer.CurrentChunk +
                            " planarDistanceMeters=" + PlanarDistance(clearanceStartPosition, player.position).ToString("0.000", CultureInfo.InvariantCulture) +
                            " inputObserved=" + clearanceInputObserved +
                            " moved=" + clearanceMoved +
                            " motorValid=" + clearanceValid +
                            " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - clearanceStartedAt).ToString("0.000", CultureInfo.InvariantCulture));
                        WriteReport(report, false, false);
                        continue;
                    }

                    // note: Use short real-input legs so each movement sample can either confirm progress or trigger a persistent side bypass around an unpublished view cell.
                    Vector2Int chunkBeforeLeg = streamer.CurrentChunk;
                    int lateralCellBeforeLeg = Mathf.Abs(initialOutwardDirection.x) > 0.5f
                        ? chunkBeforeLeg.y
                        : chunkBeforeLeg.x;
                    int traversalBlocksBeforeLeg = streamer.TraversalConstraintBlockCount;
                    Vector3 positionBeforeLeg = player.position;
                    bool lateralBypassThisLeg = outboundUsingLateralBypass;
                    int lateralBypassSideForLeg = lateralBypassThisLeg ? lateralBypassSide : 0;
                    Vector3 desiredOutboundWorldDirection = outboundUsingLateralBypass
                        ? lateralBypassDirection * lateralBypassSide
                        : initialOutwardDirection;
                    Vector3 outboundForward = player.forward;
                    outboundForward.y = 0f;
                    outboundForward = outboundForward.sqrMagnitude > 0.0001f ? outboundForward.normalized : Vector3.forward;
                    Vector3 outboundRight = player.right;
                    outboundRight.y = 0f;
                    outboundRight = outboundRight.sqrMagnitude > 0.0001f ? outboundRight.normalized : Vector3.right;
                    Vector2 outboundMoveInput = new Vector2(
                        Vector3.Dot(desiredOutboundWorldDirection, outboundRight),
                        Vector3.Dot(desiredOutboundWorldDirection, outboundForward));
                    ResolveMovementKeys(outboundMoveInput, out UnityEngine.InputSystem.Key outboundDirection,
                        out UnityEngine.InputSystem.Key outboundSecondaryDirection);
                    float remainingOutboundSeconds = Mathf.Max(0.05f,
                        (float)(outboundDeadline - Time.realtimeSinceStartupAsDouble));
                    float inputLegSeconds = Mathf.Min(remainingOutboundSeconds, RecoveryNoProgressTimeoutSeconds);
                    outboundDirectionAttempts++;
                    double outboundLegStartedAt = Time.realtimeSinceStartupAsDouble;
                    yield return DriveProductionInput(
                        keyboard,
                        streamer,
                        motor,
                        player,
                        characterController,
                        report,
                        inputLegSeconds,
                        outboundDirection,
                        value => unloadMotorValid &= value,
                        (inputObserved, moved, valid) =>
                        {
                            outboundInputObserved |= inputObserved;
                            outboundMoved |= moved;
                        },
                        stopAfterNaturalUnload,
                        outboundSecondaryDirection,
                        noProgressTimeoutSeconds: RecoveryNoProgressTimeoutSeconds,
                        positionObserved: captureReturnWaypointSample);

                    float measuredLegDistance = PlanarDistance(positionBeforeLeg, player.position);
                    if (measuredLegDistance > 0.25f)
                        AppendReturnWaypoint(outboundReturnWaypoints, player.position);
                    Vector2Int chunkAfterLeg = streamer.CurrentChunk;
                    int lateralCellAfterLeg = Mathf.Abs(initialOutwardDirection.x) > 0.5f
                        ? chunkAfterLeg.y
                        : chunkAfterLeg.x;
                    int traversalBlocksAdded = streamer.TraversalConstraintBlockCount - traversalBlocksBeforeLeg;
                    if (outboundUsingLateralBypass)
                    {
                        if (lateralCellAfterLeg != lateralBypassOriginCell)
                        {
                            outboundUsingLateralBypass = false;
                            lateralBypassFailedSides = 0;
                            report.AppendLine("- unloadRevisitBypassCleared: chunk=" + chunkAfterLeg +
                                " lateralCell=" + lateralCellAfterLeg + " legDistanceMeters=" +
                                measuredLegDistance.ToString("0.000", CultureInfo.InvariantCulture));
                            WriteReport(report, false, false);
                        }
                        else if (measuredLegDistance < 0.5f)
                        {
                            lateralBypassFailedSides++;
                            if (lateralBypassFailedSides >= 2)
                            {
                                outboundRouteFailed = true;
                                report.AppendLine("- unloadRevisitNoClearRoute: chunk=" + chunkAfterLeg +
                                    " blockCell=" + streamer.LastTraversalConstraintCell +
                                    " traversalBlocksAdded=" + traversalBlocksAdded +
                                    " attemptedBothLateralSides=true");
                            }
                            else
                            {
                                lateralBypassSide *= -1;
                                lateralBypassOriginCell = lateralCellAfterLeg;
                                outboundBoundaryClearancePending = true;
                                report.AppendLine("- unloadRevisitBypassSwitch: chunk=" + chunkAfterLeg +
                                    " blockCell=" + streamer.LastTraversalConstraintCell +
                                    " traversalBlocksAdded=" + traversalBlocksAdded +
                                    " side=" + lateralBypassSide);
                            }
                            WriteReport(report, false, false);
                        }
                    }
                    else if (measuredLegDistance < 0.5f)
                    {
                        // note: Carry the last successful lateral side across blocked outward attempts so each new edge continues around the barrier instead of reversing into the previous cell.
                        outboundUsingLateralBypass = true;
                        outboundBoundaryClearancePending = true;
                        lateralBypassOriginCell = lateralCellBeforeLeg;
                        lateralBypassFailedSides = 0;
                        report.AppendLine("- unloadRevisitBypassStart: chunk=" + chunkBeforeLeg +
                            " blockCell=" + streamer.LastTraversalConstraintCell +
                            " traversalBlocksAdded=" + traversalBlocksAdded +
                            " side=" + lateralBypassSide +
                            " sideAxis=" + (Mathf.Abs(initialOutwardDirection.x) > 0.5f ? "z" : "x"));
                        WriteReport(report, false, false);
                    }
                    report.AppendLine("- unloadRevisitRouteLeg: leg=" + outboundDirectionAttempts +
                        " mode=" + (lateralBypassThisLeg ? "lateral-bypass" : "outward") +
                        " bypassSide=" + lateralBypassSideForLeg +
                        " input=" + outboundDirection +
                        (outboundSecondaryDirection == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + outboundSecondaryDirection) +
                        " startChunk=" + chunkBeforeLeg +
                        " currentChunk=" + chunkAfterLeg +
                        " startPosition=" + positionBeforeLeg.ToString("0.000", CultureInfo.InvariantCulture) +
                        " currentPosition=" + player.position.ToString("0.000", CultureInfo.InvariantCulture) +
                        " planarDistanceMeters=" + measuredLegDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                        " targetDistanceRemainingMeters=" + PlanarDistance(player.position, unloadDestination).ToString("0.000", CultureInfo.InvariantCulture) +
                        " traversalBlocksAdded=" + traversalBlocksAdded +
                        " lastBlockCell=" + streamer.LastTraversalConstraintCell +
                        " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - outboundLegStartedAt).ToString("0.000", CultureInfo.InvariantCulture) +
                        " outsideRetentionBoundary=" + outsideRetentionBoundaryReached);
                    WriteReport(report, false, false);
                }
                if (outsideRetentionBoundaryReached && !unloadedObserved)
                {
                    // note: Stop the motor at the true retention boundary, then observe the production lifecycle under its own bounded wall-clock receipt window.
                    bool lifecycleUnloadCompleted = false;
                    double lifecycleWaitStartedAt = Time.realtimeSinceStartupAsDouble;
                    yield return WaitForPublicationSnapshot(
                        streamer,
                        revisitCoordinate,
                        snapshot => snapshot.lifecycle == YQSemanticChunkLifecycle.Unloaded &&
                            !snapshot.traversable &&
                            snapshot.publicationVersion > revisitStartSnapshot.publicationVersion,
                        PublicationRecoveryTimeoutSeconds,
                        (matched, snapshot) =>
                        {
                            lifecycleUnloadCompleted = matched;
                            unloadedSnapshot = snapshot;
                        });
                    unloadedObserved = lifecycleUnloadCompleted;
                    report.AppendLine("- unloadRevisitLifecycleReceipt: " + (lifecycleUnloadCompleted ? "PASS" : "FAIL") +
                        " coordinate=" + revisitCoordinate +
                        " capturedVersion=" + revisitStartSnapshot.publicationVersion +
                        " observedVersion=" + unloadedSnapshot.publicationVersion +
                        " ownerEpoch=" + unloadedSnapshot.ownerEpoch +
                        " lifecycle=" + unloadedSnapshot.lifecycle +
                        " traversable=" + unloadedSnapshot.traversable +
                        " activationWorkActive=" + unloadedSnapshot.activationWorkActive +
                        " streamerLifecycleWorkPending=" + unloadedSnapshot.lifecycleWorkPending +
                        " retainedBy{demanded=" + unloadedSnapshot.demanded +
                        ",hardView=" + unloadedSnapshot.hardViewDemanded +
                        ",guaranteedView=" + unloadedSnapshot.guaranteedViewDemanded +
                        ",preparation=" + unloadedSnapshot.canonicalPreparationDemanded +
                        ",predicted=" + unloadedSnapshot.predictedViewDemanded +
                        ",groundPrefetch=" + unloadedSnapshot.provisionalGroundPrefetchDemanded +
                        ",turnBuffer=" + unloadedSnapshot.provisionalGroundTurnBufferDemanded +
                        ",site=" + unloadedSnapshot.siteTerrainDependency +
                        ",siteHandoff=" + unloadedSnapshot.siteTerrainHandoffPending +
                        ",verificationPin=" + unloadedSnapshot.publicationVerificationPinned +
                        ",distance=" + unloadedSnapshot.currentChunkDistance +
                        ",unloadRadius=" + unloadedSnapshot.effectiveUnloadRadius + "}" +
                        " terrainWorkActive=" + unloadedSnapshot.terrainWorkActive +
                        " contentWorkActive=" + unloadedSnapshot.contentWorkActive +
                        " appearanceWorkActive=" + unloadedSnapshot.appearanceWorkActive +
                        " ecologyWorkActive=" + unloadedSnapshot.ecologyWorkActive +
                        " failure=" + (unloadedSnapshot.failureReason ?? string.Empty) +
                        " timeoutSeconds=" + PublicationRecoveryTimeoutSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                        " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - lifecycleWaitStartedAt).ToString("0.000", CultureInfo.InvariantCulture));
                    WriteReport(report, false, false);
                }
                if (!unloadedObserved && streamer.TryGetPublicationSnapshot(revisitCoordinate, out YQSemanticChunkPublicationSnapshot finalOutboundSnapshot))
                {
                    unloadedSnapshot = finalOutboundSnapshot;
                    unloadedObserved = finalOutboundSnapshot.lifecycle == YQSemanticChunkLifecycle.Unloaded &&
                        !finalOutboundSnapshot.traversable &&
                        finalOutboundSnapshot.publicationVersion > revisitStartSnapshot.publicationVersion;
                }
            }
            float outboundElapsedSeconds = (float)(Time.realtimeSinceStartupAsDouble - outboundStartedAt);
            report.AppendLine("- unloadRevisitUnload: " + (unloadedObserved ? "PASS" : "FAIL") +
                " coordinate=" + revisitCoordinate +
                " capturedVersion=" + revisitStartSnapshot.publicationVersion +
                " unloadedVersion=" + unloadedSnapshot.publicationVersion +
                " unloadedOwnerEpoch=" + unloadedSnapshot.ownerEpoch +
                " lifecycle=" + unloadedSnapshot.lifecycle +
                " physical=" + unloadedSnapshot.physicalRepresentation +
                " inputObserved=" + outboundInputObserved +
                " moved=" + outboundMoved +
                " motorValid=" + unloadMotorValid +
                " currentChunk=" + streamer.CurrentChunk +
                " currentPosition=" + player.position +
                " outsideRetentionBoundary=" + outsideRetentionBoundaryReached +
                " routeFailed=" + outboundRouteFailed +
                " witnessChunkDistance=" + Mathf.Max(Mathf.Abs(streamer.CurrentChunk.x - revisitCoordinate.x),
                    Mathf.Abs(streamer.CurrentChunk.y - revisitCoordinate.y)) +
                " targetDistanceRemainingMeters=" + (revisitCandidateCaptured && player != null
                    ? Vector2.Distance(new Vector2(player.position.x, player.position.z),
                        new Vector2(unloadDestination.x, unloadDestination.z)).ToString("0.000", CultureInfo.InvariantCulture)
                    : "n/a") +
                " stoppedAtInvalidTraversal=" + outboundStoppedAtInvalidTraversal +
                " timeoutSeconds=" + unloadLegTimeoutSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " elapsedSeconds=" + outboundElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            WriteReport(report, false, false);

            Vector3 revisitTarget = new Vector3(
                YQContinuousWorldFeatureAuthority.WorldGridOrigin + (revisitCoordinate.x + 0.5f) * streamer.chunkWorldSize,
                player.position.y,
                YQContinuousWorldFeatureAuthority.WorldGridOrigin + (revisitCoordinate.y + 0.5f) * streamer.chunkWorldSize);
            Vector3 returnDirection = revisitTarget - player.position;
            returnDirection.y = 0f;
            Vector3 motorForward = player.forward;
            motorForward.y = 0f;
            motorForward = motorForward.sqrMagnitude > 0.0001f ? motorForward.normalized : Vector3.forward;
            Vector3 motorRight = player.right;
            motorRight.y = 0f;
            motorRight = motorRight.sqrMagnitude > 0.0001f ? motorRight.normalized : Vector3.right;
            Vector2 returnMoveInput = returnDirection.sqrMagnitude > 0.0001f
                ? new Vector2(Vector3.Dot(returnDirection.normalized, motorRight), Vector3.Dot(returnDirection.normalized, motorForward))
                : Vector2.zero;
            ResolveMovementKeys(returnMoveInput, out UnityEngine.InputSystem.Key returnPrimaryKey, out UnityEngine.InputSystem.Key returnSecondaryKey);
            float returnDistance = returnDirection.magnitude;
            // note: Allow enough genuine motor time to return from beyond the unload radius and enter the original cell, not just trigger its preparation ring.
            float returnTimeoutSeconds = Mathf.Clamp(
                returnDistance / Mathf.Max(1f, motor.walkSpeed * 0.35f) + 90f,
                120f,
                UnloadRevisitTimeoutSeconds);
            bool returnInputObserved = false;
            bool returnMoved = false;
            bool returnStoppedAtInvalidTraversal = false;
            bool returnRouteFailed = false;
            string returnRouteFailure = string.Empty;
            int returnWaypointsCompleted = 0;
            int returnLegCount = 0;
            bool revisited = false;
            YQSemanticChunkPublicationSnapshot revisitedSnapshot = default;
            double returnStartedAt = Time.realtimeSinceStartupAsDouble;
            if (unloadedObserved)
            {
                report.AppendLine("- unloadRevisitReturnStart: PENDING coordinate=" + revisitCoordinate +
                    " targetDistanceMeters=" + returnDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                    " input=" + returnPrimaryKey + (returnSecondaryKey == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + returnSecondaryKey) +
                    " reverseWaypoints=" + outboundReturnWaypoints.Count +
                    " physicalTraceStepMeters=" + ReturnWaypointSampleDistanceMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                    " timeoutSeconds=" + returnTimeoutSeconds.ToString("0.000", CultureInfo.InvariantCulture));
                WriteReport(report, false, false);
                Func<bool> revisitedReady = () =>
                {
                    if (!streamer.TryGetPublicationSnapshot(revisitCoordinate, out YQSemanticChunkPublicationSnapshot snapshot) ||
                        !snapshot.demanded || !snapshot.traversable ||
                        snapshot.publicationVersion <= unloadedSnapshot.publicationVersion ||
                        streamer.CurrentChunk != revisitCoordinate)
                        return false;
                    revisitedSnapshot = snapshot;
                    revisited = true;
                    return true;
                };
                Func<bool> stopReturn = () =>
                {
                    if (revisitedReady())
                        return true;
                    bool valid = streamer.TryValidateCurrentTraversability(
                        player.position,
                        characterController.radius,
                        characterController.skinWidth,
                        out _);
                    returnStoppedAtInvalidTraversal |= !valid;
                    return !valid;
                };
                if (outboundReturnWaypoints.Count < 2)
                {
                    returnRouteFailed = true;
                    returnRouteFailure = "outbound route did not retain enough physical waypoints to retrace";
                }
                else
                {
                    // note: Retrace only positions reached by the real outbound motor so a fixed diagonal cannot drive into a different incomplete cell.
                    double returnDeadline = returnStartedAt + returnTimeoutSeconds;
                    for (int waypointIndex = outboundReturnWaypoints.Count - 2;
                         waypointIndex >= 0 && !revisited && !returnRouteFailed;
                         waypointIndex--)
                    {
                        Vector3 waypoint = outboundReturnWaypoints[waypointIndex];
                        Vector2Int waypointCoordinate = ChunkCoordinateAt(waypoint, streamer.chunkWorldSize);
                        int waypointBypassAttempts = 0;
                        int waypointBypassPreflightFailures = 0;
                        Vector3 waypointBypassAxis = Vector3.zero;
                        // note: Keep a successful physical sidestep on one side so repeated retries accumulate clearance around the same obstacle.
                        float waypointBypassSide = 1f;
                        bool waypointReached = PlanarDistance(player.position, waypoint) <= 1.5f;
                        while (!waypointReached && !revisited && !returnRouteFailed &&
                               Time.realtimeSinceStartupAsDouble < returnDeadline)
                        {
                            Vector3 waypointDirection = waypoint - player.position;
                            waypointDirection.y = 0f;
                            if (waypointDirection.sqrMagnitude <= 0.0001f)
                            {
                                waypointReached = true;
                                break;
                            }
                            Vector3 waypointForward = player.forward;
                            waypointForward.y = 0f;
                            waypointForward = waypointForward.sqrMagnitude > 0.0001f ? waypointForward.normalized : Vector3.forward;
                            Vector3 waypointRight = player.right;
                            waypointRight.y = 0f;
                            waypointRight = waypointRight.sqrMagnitude > 0.0001f ? waypointRight.normalized : Vector3.right;
                            // note: Approach one dominant world axis at a time and give the rendered input pump enough wall time to move toward a physical waypoint.
                            Vector3 waypointAxisDirection = Mathf.Abs(waypointDirection.x) >= Mathf.Abs(waypointDirection.z)
                                ? new Vector3(Mathf.Sign(waypointDirection.x), 0f, 0f)
                                : new Vector3(0f, 0f, Mathf.Sign(waypointDirection.z));
                            Vector2 waypointInput = new Vector2(
                                Vector3.Dot(waypointAxisDirection, waypointRight),
                                Vector3.Dot(waypointAxisDirection, waypointForward));
                            ResolveMovementKeys(waypointInput,
                                out UnityEngine.InputSystem.Key waypointPrimaryKey,
                                out UnityEngine.InputSystem.Key waypointSecondaryKey);
                            float waypointAxisDistance = Mathf.Max(
                                Mathf.Abs(waypointDirection.x),
                                Mathf.Abs(waypointDirection.z));
                            float waypointLegSeconds = Mathf.Min(
                                RecoveryNoProgressTimeoutSeconds,
                                Mathf.Min(
                                    Mathf.Max(0.75f, waypointAxisDistance / Mathf.Max(1f, motor.walkSpeed) * 0.7f),
                                    Mathf.Max(0.05f, (float)(returnDeadline - Time.realtimeSinceStartupAsDouble))));
                            Vector3 waypointLegStartPosition = player.position;
                            float waypointDistanceBeforeLeg = PlanarDistance(waypointLegStartPosition, waypoint);
                            double waypointLegStartedAt = Time.realtimeSinceStartupAsDouble;
                            bool legReachedWaypoint = false;
                            yield return DriveProductionInput(
                                keyboard,
                                streamer,
                                motor,
                                player,
                                characterController,
                                report,
                                waypointLegSeconds,
                                waypointPrimaryKey,
                                value => unloadMotorValid &= value,
                                (inputObserved, moved, valid) =>
                                {
                                    returnInputObserved |= inputObserved;
                                    returnMoved |= moved;
                                },
                                () =>
                                {
                                    if (revisitedReady())
                                        return true;
                                    if (PlanarDistance(player.position, waypoint) <= 1.5f)
                                    {
                                        legReachedWaypoint = true;
                                        return true;
                                    }
                                    return stopReturn();
                                },
                                waypointSecondaryKey,
                                noProgressTimeoutSeconds: RecoveryNoProgressTimeoutSeconds);
                            returnLegCount++;
                            float waypointLegDistance = PlanarDistance(waypointLegStartPosition, player.position);
                            float waypointDistanceAfterLeg = PlanarDistance(player.position, waypoint);
                            float waypointApproachProgress = waypointDistanceBeforeLeg - waypointDistanceAfterLeg;
                            waypointReached = legReachedWaypoint || waypointDistanceAfterLeg <= 1.5f;
                            if (revisited || waypointReached)
                            {
                                report.AppendLine("- unloadRevisitReturnLeg: PASS waypoint=" + waypointIndex +
                                    " coordinate=" + waypointCoordinate +
                                    " currentChunk=" + streamer.CurrentChunk +
                                    " input=" + waypointPrimaryKey +
                                    (waypointSecondaryKey == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + waypointSecondaryKey) +
                                    " planarDistanceMeters=" + waypointLegDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetDistanceRemainingMeters=" + waypointDistanceAfterLeg.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetDistanceProgressMeters=" + waypointApproachProgress.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - waypointLegStartedAt).ToString("0.000", CultureInfo.InvariantCulture));
                                WriteReport(report, false, false);
                                break;
                            }
                            if (returnStoppedAtInvalidTraversal)
                            {
                                returnRouteFailed = true;
                                returnRouteFailure = "invalid current traversability while returning through waypoint " + waypointCoordinate;
                            }
                            // note: Small physical steps count as progress when they reduce distance to the saved route point; only a stalled approach enters publication diagnosis.
                            else if (waypointApproachProgress <= 0.05f)
                            {
                                bool hasWaypointSnapshot = streamer.TryGetPublicationSnapshot(
                                    waypointCoordinate,
                                    out YQSemanticChunkPublicationSnapshot waypointSnapshot);
                                string workContractFailure = string.Empty;
                                bool workContractValid = hasWaypointSnapshot && waypointSnapshot.demanded &&
                                    !waypointSnapshot.traversable &&
                                    streamer.TryValidatePublicationWorkContract(waypointCoordinate, out workContractFailure);
                                if (hasWaypointSnapshot && waypointSnapshot.hasExplicitFailure)
                                {
                                    returnRouteFailed = true;
                                    returnRouteFailure = "return waypoint has explicit publication failure " +
                                        waypointCoordinate + ": " + waypointSnapshot.failureReason;
                                }
                                else if (workContractValid)
                                {
                                    bool waypointPublicationReady = false;
                                    YQSemanticChunkPublicationSnapshot waypointPublicationSnapshot = waypointSnapshot;
                                    float remainingReturnSeconds = Mathf.Max(0f,
                                        (float)(returnDeadline - Time.realtimeSinceStartupAsDouble));
                                    float publicationWaitSeconds = Mathf.Min(PublicationRecoveryTimeoutSeconds, remainingReturnSeconds);
                                    if (publicationWaitSeconds >= 0.05f)
                                    {
                                        yield return WaitForPublicationSnapshot(
                                            streamer,
                                            waypointCoordinate,
                                            snapshot => snapshot.traversable &&
                                                (waypointCoordinate != revisitCoordinate ||
                                                 snapshot.publicationVersion > unloadedSnapshot.publicationVersion),
                                            publicationWaitSeconds,
                                            (matched, snapshot) =>
                                            {
                                                waypointPublicationReady = matched;
                                                waypointPublicationSnapshot = snapshot;
                                            });
                                    }
                                    report.AppendLine("- unloadRevisitReturnPublicationWait: " + (waypointPublicationReady ? "PASS" : "FAIL") +
                                        " coordinate=" + waypointCoordinate +
                                        " demanded=" + waypointSnapshot.demanded +
                                        " terrainWork=" + waypointPublicationSnapshot.terrainWorkActive +
                                        " terrainQueued=" + waypointPublicationSnapshot.terrainWorkQueued +
                                        " contentWork=" + waypointPublicationSnapshot.contentWorkActive +
                                        " contentQueued=" + waypointPublicationSnapshot.contentWorkQueued +
                                        " appearanceWork=" + waypointPublicationSnapshot.appearanceWorkActive +
                                        " ecologyWork=" + waypointPublicationSnapshot.ecologyWorkActive +
                                        " activationWork=" + waypointPublicationSnapshot.activationWorkActive +
                                        " failure=" + (waypointPublicationSnapshot.failureReason ?? workContractFailure) +
                                        " timeoutSeconds=" + publicationWaitSeconds.ToString("0.000", CultureInfo.InvariantCulture));
                                    WriteReport(report, false, false);
                                    if (!waypointPublicationReady)
                                    {
                                        returnRouteFailed = true;
                                        returnRouteFailure = "demanded return waypoint publication did not complete " + waypointCoordinate;
                                    }
                                }
                                else if (hasWaypointSnapshot && waypointSnapshot.traversable)
                                {
                                    if (streamer.CurrentChunk != waypointCoordinate ||
                                        waypointBypassAttempts >= SameOwnerReturnBypassMaxAttempts)
                                    {
                                        returnRouteFailed = true;
                                        returnRouteFailure = streamer.CurrentChunk != waypointCoordinate
                                            ? "stalled return waypoint is no longer inside its traversable owner " + waypointCoordinate
                                            : "bounded same-owner return bypass attempts were exhausted at " + waypointCoordinate;
                                    }
                                    else
                                    {
                                        // note: Try a bounded physical sidestep only when both the player and blocked point belong to the same ready chunk.
                                        if (waypointBypassAxis.sqrMagnitude <= 0.0001f)
                                        {
                                            Vector3 initialBypassDirection = waypoint - player.position;
                                            initialBypassDirection.y = 0f;
                                            if (initialBypassDirection.sqrMagnitude > 0.0001f)
                                                waypointBypassAxis = Vector3.Cross(Vector3.up, initialBypassDirection.normalized).normalized;
                                        }

                                        if (waypointBypassAxis.sqrMagnitude <= 0.0001f)
                                        {
                                            returnRouteFailed = true;
                                            returnRouteFailure = "could not derive a horizontal same-owner bypass direction for " + waypointCoordinate;
                                        }
                                        else
                                        {
                                            int bypassAttempt = waypointBypassAttempts++;
                                            float bypassSide = waypointBypassSide;
                                            Vector3 bypassDirection = waypointBypassAxis * bypassSide;
                                            float bypassSeconds = Mathf.Min(1f,
                                                Mathf.Max(0.75f, SameOwnerReturnBypassStepMeters / Mathf.Max(1f, motor.walkSpeed)));
                                            Vector3 bypassForward = player.forward;
                                            bypassForward.y = 0f;
                                            bypassForward = bypassForward.sqrMagnitude > 0.0001f ? bypassForward.normalized : Vector3.forward;
                                            Vector3 bypassRight = player.right;
                                            bypassRight.y = 0f;
                                            bypassRight = bypassRight.sqrMagnitude > 0.0001f ? bypassRight.normalized : Vector3.right;
                                            Vector2 bypassInput = new Vector2(
                                                Vector3.Dot(bypassDirection, bypassRight),
                                                Vector3.Dot(bypassDirection, bypassForward));
                                            ResolveMovementKeys(bypassInput,
                                                out UnityEngine.InputSystem.Key bypassPrimaryKey,
                                                out UnityEngine.InputSystem.Key bypassSecondaryKey);
                                            Vector2 resolvedBypassInput = ResolveKeyMoveInput(bypassPrimaryKey, bypassSecondaryKey);
                                            Vector3 resolvedBypassDirection = player.forward * resolvedBypassInput.y +
                                                player.right * resolvedBypassInput.x;
                                            resolvedBypassDirection.y = 0f;
                                            resolvedBypassDirection = resolvedBypassDirection.sqrMagnitude > 0.0001f
                                                ? resolvedBypassDirection.normalized
                                                : bypassDirection;
                                            float bypassTargetSpeed = Mathf.Max(motor.walkSpeed, motor.sprintSpeed);
                                            PlayerState bypassPlayerState = PlayerStateManager.Instance != null
                                                ? PlayerStateManager.Instance.state
                                                : null;
                                            if (bypassPlayerState != null)
                                            {
                                                float statMoveBonus = Mathf.Max(0f, bypassPlayerState.stats.moveSpeed - motor.walkSpeed);
                                                float itemMoveBonus = GeneratedRpgContentService.Instance != null
                                                    ? GeneratedRpgContentService.Instance.GetMoveSpeedBonus(bypassPlayerState)
                                                    : 0f;
                                                bypassTargetSpeed += statMoveBonus + itemMoveBonus;
                                            }
                                            Vector3 bypassInitialVelocity = motor.PlanarVelocity;
                                            bypassInitialVelocity.y = 0f;
                                            Vector3 bypassTargetVelocity = resolvedBypassDirection * bypassTargetSpeed;
                                            float bypassAcceleration = motor.IsGrounded
                                                ? motor.acceleration
                                                : motor.acceleration * motor.airControl;
                                            Vector3 bypassExpectedDisplacement = EstimatePlanarMotorDisplacement(
                                                bypassInitialVelocity,
                                                bypassTargetVelocity,
                                                bypassAcceleration,
                                                bypassSeconds);
                                            Vector3 bypassTarget = player.position + bypassExpectedDisplacement;
                                            float bypassTravelMeters = bypassExpectedDisplacement.magnitude;
                                            Vector3 bypassExpectedEnd = bypassTarget;

                                            // note: Bound the same-owner input along its actual lateral sweep, then pad only for the capsule and controller step resolution.
                                            float bypassSafetyMargin = characterController.radius + characterController.skinWidth +
                                                characterController.stepOffset + 0.03f;
                                            float bypassChunkSize = Mathf.Max(1f, streamer.chunkWorldSize);
                                            bool bypassPreflightSafe = Vector3.Dot(resolvedBypassDirection, bypassDirection) > 0.5f;
                                            // note: Check the acceleration-shaped real-motor path, including residual velocity, because quantized camera-relative keys do not exactly follow the requested lateral vector.
                                            for (int sample = 0; sample <= 8 && bypassPreflightSafe; sample++)
                                            {
                                                float sampleSeconds = bypassSeconds * (sample / 8f);
                                                Vector3 samplePosition = player.position + EstimatePlanarMotorDisplacement(
                                                    bypassInitialVelocity,
                                                    bypassTargetVelocity,
                                                    bypassAcceleration,
                                                    sampleSeconds);
                                                bypassPreflightSafe = HasChunkClearanceAt(
                                                    samplePosition,
                                                    waypointCoordinate,
                                                    bypassChunkSize,
                                                    bypassSafetyMargin);
                                            }
                                            if (!bypassPreflightSafe)
                                            {
                                                report.AppendLine("- unloadRevisitReturnBypass: RETRY waypoint=" + waypointIndex +
                                                    " coordinate=" + waypointCoordinate +
                                                    " attempt=" + (bypassAttempt + 1) + "/" + SameOwnerReturnBypassMaxAttempts +
                                                    " side=" + (bypassSide > 0f ? "left" : "right") +
                                                    " expectedTravelMeters=" + bypassTravelMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                                                    " projectedVelocity=" + bypassInitialVelocity.ToString("F2") +
                                                    " projectedDirection=" + resolvedBypassDirection.ToString("F2") +
                                                    " preflight=outside-owner-clearance currentChunk=" + streamer.CurrentChunk);
                                                WriteReport(report, false, false);
                                                waypointBypassSide *= -1f;
                                                waypointBypassPreflightFailures++;
                                                if (waypointBypassPreflightFailures >= 2 ||
                                                    waypointBypassAttempts >= SameOwnerReturnBypassMaxAttempts)
                                                {
                                                    returnRouteFailed = true;
                                                    returnRouteFailure = "neither same-owner return bypass side had safe lateral clearance at " + waypointCoordinate;
                                                }
                                                else
                                                {
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                waypointBypassPreflightFailures = 0;
                                                Vector3 bypassStartPosition = player.position;
                                                bool bypassInputObserved = false;
                                                bool bypassMoved = false;
                                                bool bypassFramesValid = true;
                                                bool bypassTargetReached = false;
                                                double bypassStartedAt = Time.realtimeSinceStartupAsDouble;
                                                yield return DriveProductionInput(
                                                    keyboard,
                                                    streamer,
                                                    motor,
                                                    player,
                                                    characterController,
                                                    report,
                                                    bypassSeconds,
                                                    bypassPrimaryKey,
                                                    value =>
                                                    {
                                                        bypassFramesValid &= value;
                                                        unloadMotorValid &= value;
                                                    },
                                                    (inputObserved, moved, valid) =>
                                                    {
                                                        bypassInputObserved |= inputObserved;
                                                        bypassMoved |= moved;
                                                        bypassFramesValid &= valid;
                                                        returnInputObserved |= inputObserved;
                                                        returnMoved |= moved;
                                                    },
                                                    () =>
                                                    {
                                                        if (revisitedReady())
                                                            return true;
                                                        if (streamer.CurrentChunk != waypointCoordinate)
                                                            return true;
                                                        if (PlanarDistance(player.position, bypassTarget) <= 0.75f)
                                                        {
                                                            bypassTargetReached = true;
                                                            return true;
                                                        }
                                                        return stopReturn();
                                                    },
                                                    bypassSecondaryKey,
                                                    noProgressTimeoutSeconds: RecoveryNoProgressTimeoutSeconds);
                                                returnLegCount++;
                                                bool bypassStayedInOwner = streamer.CurrentChunk == waypointCoordinate &&
                                                    ChunkCoordinateAt(player.position, bypassChunkSize) == waypointCoordinate;
                                                bool bypassAccepted = bypassFramesValid && bypassStayedInOwner && !returnStoppedAtInvalidTraversal;
                                                float bypassDistance = PlanarDistance(bypassStartPosition, player.position);
                                                Vector3 bypassActualDelta = player.position - bypassStartPosition;
                                                bypassActualDelta.y = 0f;
                                                float bypassLateralProgress = Vector3.Dot(bypassActualDelta, bypassDirection);
                                                report.AppendLine("- unloadRevisitReturnBypass: " +
                                                    (bypassAccepted && bypassMoved ? "PASS" : bypassAccepted ? "RETRY" : "FAIL") +
                                                    " waypoint=" + waypointIndex +
                                                    " coordinate=" + waypointCoordinate +
                                                    " attempt=" + (bypassAttempt + 1) + "/" + SameOwnerReturnBypassMaxAttempts +
                                                    " side=" + (bypassSide > 0f ? "left" : "right") +
                                                    " input=" + bypassPrimaryKey +
                                                    (bypassSecondaryKey == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + bypassSecondaryKey) +
                                                    " inputObserved=" + bypassInputObserved +
                                                    " moved=" + bypassMoved +
                                                    " targetReached=" + bypassTargetReached +
                                                    " lateralProgressMeters=" + bypassLateralProgress.ToString("0.000", CultureInfo.InvariantCulture) +
                                                    " planarDistanceMeters=" + bypassDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                                                    " currentChunk=" + streamer.CurrentChunk +
                                                    " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - bypassStartedAt).ToString("0.000", CultureInfo.InvariantCulture));
                                                WriteReport(report, false, false);
                                                if (!bypassAccepted)
                                                {
                                                    returnRouteFailed = true;
                                                    returnRouteFailure = "same-owner return bypass violated motor validity or chunk ownership at " + waypointCoordinate;
                                                }
                                                else if (waypointBypassAttempts >= SameOwnerReturnBypassMaxAttempts && !bypassMoved)
                                                {
                                                    returnRouteFailed = true;
                                                    returnRouteFailure = "production motor made no progress during bounded same-owner bypass attempts at " + waypointCoordinate;
                                                }
                                                else
                                                {
                                                    // note: Reverse only when collision-resolved motion failed to advance along the chosen same-owner sidestep.
                                                    if (bypassLateralProgress <= 0.5f)
                                                        waypointBypassSide *= -1f;
                                                    continue;
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    returnRouteFailed = true;
                                    returnRouteFailure = waypointSnapshot.demanded
                                        ? "demanded return waypoint has no live work, bounded retry, or publication failure " +
                                            waypointCoordinate + ": " + workContractFailure
                                        : "motor made no progress toward return waypoint " + waypointCoordinate +
                                            " without a demanded publication owner";
                                }
                            }
                            if (returnRouteFailed)
                            {
                                report.AppendLine("- unloadRevisitReturnLeg: FAIL waypoint=" + waypointIndex +
                                    " coordinate=" + waypointCoordinate +
                                    " currentChunk=" + streamer.CurrentChunk +
                                    " startPosition=" + waypointLegStartPosition.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetPosition=" + waypoint.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " input=" + waypointPrimaryKey +
                                    (waypointSecondaryKey == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + waypointSecondaryKey) +
                                    " planarDistanceMeters=" + waypointLegDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetDistanceBeforeMeters=" + waypointDistanceBeforeLeg.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetDistanceAfterMeters=" + waypointDistanceAfterLeg.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetDistanceProgressMeters=" + waypointApproachProgress.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " reason=" + returnRouteFailure);
                                WriteReport(report, false, false);
                                break;
                            }
                            if (Time.realtimeSinceStartupAsDouble >= returnDeadline && !waypointReached)
                            {
                                returnRouteFailed = true;
                                returnRouteFailure = "return wall-clock deadline expired at waypoint " + waypointCoordinate;
                            }
                        }
                        if (waypointReached && !revisited && !returnRouteFailed)
                            returnWaypointsCompleted++;
                        if (!waypointReached && !revisited && !returnRouteFailed)
                        {
                            returnRouteFailed = true;
                            returnRouteFailure = "return wall-clock deadline expired at waypoint " + waypointCoordinate;
                        }
                    }
                    if (!revisited && !returnRouteFailed &&
                        Time.realtimeSinceStartupAsDouble >= returnDeadline)
                    {
                        returnRouteFailed = true;
                        returnRouteFailure = "return wall-clock deadline expired before exact-cell revisit";
                    }
                }
                if (!revisited && streamer.TryGetPublicationSnapshot(revisitCoordinate, out YQSemanticChunkPublicationSnapshot finalRevisitSnapshot))
                {
                    revisitedSnapshot = finalRevisitSnapshot;
                    revisited = finalRevisitSnapshot.demanded && finalRevisitSnapshot.traversable &&
                        finalRevisitSnapshot.publicationVersion > unloadedSnapshot.publicationVersion &&
                        streamer.CurrentChunk == revisitCoordinate;
                }
            }
            float returnElapsedSeconds = (float)(Time.realtimeSinceStartupAsDouble - returnStartedAt);
            // note: Missing input evidence cannot downgrade an observed traversal safety failure to an inconclusive result.
            bool unloadTraversalValid = unloadMotorValid && !captureStoppedAtInvalidTraversal &&
                !outboundStoppedAtInvalidTraversal && !returnStoppedAtInvalidTraversal;
            bool unloadInputInconclusive = unloadedObserved && unloadTraversalValid &&
                (!outboundInputObserved || !returnInputObserved);
            bool unloadPassed = revisitCandidateCaptured && unloadedObserved && revisited &&
                outboundInputObserved && outboundMoved && returnInputObserved && returnMoved &&
                unloadTraversalValid && !returnRouteFailed;
            // note: Unload/revisit belongs to R2 streaming acceptance, so its verdict stays independent from R1 recovery.
            unloadRevisitPassed = unloadPassed;
            unloadRevisitInputInconclusive = unloadInputInconclusive;
            report.AppendLine("- unloadRevisitGate: " + (unloadInputInconclusive ? "INCONCLUSIVE" : unloadPassed ? "PASS" : "FAIL") +
                " candidateCaptured=" + revisitCandidateCaptured +
                " unloaded=" + unloadedObserved +
                " revisited=" + revisited +
                " outboundInputObserved=" + outboundInputObserved +
                " outboundMoved=" + outboundMoved +
                " returnInputObserved=" + returnInputObserved +
                " returnMoved=" + returnMoved +
                " returnRouteFailed=" + returnRouteFailed +
                " returnRouteFailure=" + returnRouteFailure +
                " returnWaypointsCompleted=" + returnWaypointsCompleted + "/" + outboundReturnWaypoints.Count +
                " returnLegCount=" + returnLegCount +
                " motorValid=" + unloadMotorValid +
                " coordinate=" + revisitCoordinate +
                " capturedVersion=" + revisitStartSnapshot.publicationVersion +
                " unloadedVersion=" + unloadedSnapshot.publicationVersion +
                " revisitedVersion=" + revisitedSnapshot.publicationVersion +
                " capturedOwnerEpoch=" + revisitStartSnapshot.ownerEpoch +
                " unloadedOwnerEpoch=" + unloadedSnapshot.ownerEpoch +
                " revisitedOwnerEpoch=" + revisitedSnapshot.ownerEpoch +
                " revisitCurrentChunk=" + streamer.CurrentChunk +
                " revisitedAtCandidate=" + (streamer.CurrentChunk == revisitCoordinate) +
                " revisitPosition=" + player.position.ToString("0.000", CultureInfo.InvariantCulture) +
                " revisitTargetDistanceMeters=" + PlanarDistance(player.position, revisitTarget).ToString("0.000", CultureInfo.InvariantCulture) +
                " stoppedAtInvalidTraversal=" + (captureStoppedAtInvalidTraversal || outboundStoppedAtInvalidTraversal || returnStoppedAtInvalidTraversal) +
                " captureElapsedSeconds=" + captureElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " outboundElapsedSeconds=" + outboundElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " returnElapsedSeconds=" + returnElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            WriteReport(report, false, false);
        }
        finally
        {
            // note: Interrupted matrices must release every injected hold, including a phase whose assertion threw before its normal cleanup.
            foreach (Vector2Int coordinate in usedCoordinates)
                if (streamer != null)
                    streamer.EndPublicationVerificationScenario(coordinate);
            if (keyboard != null)
            {
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState());
            }
        }
        result(allPassed, recoveryInputDeliveryInconclusive, unloadRevisitPassed, unloadRevisitInputInconclusive);
    }

    private static bool TryReplaceRecoveryOwner(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        out Vector2Int coordinate,
        out int previousPublicationVersion,
        out int replacementPublicationVersion)
    {
        previousPublicationVersion = 0;
        replacementPublicationVersion = 0;
        HashSet<Vector2Int> attemptedCoordinates = new HashSet<Vector2Int>();
        // note: Selection and replacement are one bounded operation because the live terrain worker can change between snapshots.
        for (int attempt = 0; attempt < 64; attempt++)
        {
            if (!TryFindRecoveryCoordinate(streamer, usedCoordinates, attemptedCoordinates, true, out Vector2Int candidate))
                break;
            attemptedCoordinates.Add(candidate);
            if (streamer.TryReplacePublicationOwnerForVerification(
                    candidate,
                    out previousPublicationVersion,
                    out replacementPublicationVersion))
            {
                usedCoordinates.Add(candidate);
                coordinate = candidate;
                return true;
            }
        }
        coordinate = new Vector2Int(int.MinValue, int.MinValue);
        return false;
    }

    private static IEnumerator PrepareAppearanceRetryOwner(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        double deadline,
        Action<bool, Vector2Int> result)
    {
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            if (TryPrepareAppearanceRetryOwner(streamer, usedCoordinates, out Vector2Int coordinate))
            {
                result(true, coordinate);
                yield break;
            }
            // note: Let ordinary production streaming finish a retained owner between bounded candidate sweeps instead of failing on one transiently busy frame.
            yield return null;
        }

        result(false, new Vector2Int(int.MinValue, int.MinValue));
    }

    private static bool TryPrepareAppearanceRetryOwner(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        out Vector2Int coordinate)
    {
        // note: Select a completed retained owner, then invalidate only appearance so the retry case does not wait behind a fresh terrain/content admission.
        HashSet<Vector2Int> attemptedCoordinates = new HashSet<Vector2Int>();
        for (int attempt = 0; attempt < 64; attempt++)
        {
            if (!TryFindRecoveryCoordinate(streamer, usedCoordinates, attemptedCoordinates, false, out Vector2Int candidate))
                break;
            attemptedCoordinates.Add(candidate);
            if (streamer.TryPrepareAppearanceRetryVerification(candidate))
            {
                usedCoordinates.Add(candidate);
                coordinate = candidate;
                return true;
            }
        }
        coordinate = new Vector2Int(int.MinValue, int.MinValue);
        return false;
    }

    private static IEnumerator PrepareReplacementScenarioOwner(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        bool holdAppearance,
        bool holdRequiredEcology,
        double deadline,
        Action<bool, Vector2Int, int, int> result)
    {
        HashSet<Vector2Int> attemptedCoordinates = new HashSet<Vector2Int>();
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                if (!TryFindRecoveryCoordinate(streamer, usedCoordinates, attemptedCoordinates, true, out Vector2Int candidate))
                    break;
                attemptedCoordinates.Add(candidate);
                if (!streamer.TryReplacePublicationOwnerForVerification(
                        candidate,
                        out int previousPublicationVersion,
                        out int replacementPublicationVersion))
                    continue;

                // note: Install the completion-order hold in the same frame as owner replacement so an async publication cannot pass the boundary before the probe starts.
                bool prepared = streamer.BeginPublicationVerificationScenario(
                    candidate,
                    false,
                    holdAppearance,
                    holdRequiredEcology,
                    0,
                    0);
                usedCoordinates.Add(candidate);
                if (!prepared)
                {
                    streamer.EndPublicationVerificationScenario(candidate);
                    continue;
                }

                result(true, candidate, previousPublicationVersion, replacementPublicationVersion);
                yield break;
            }

            // note: Retry the bounded retained-owner search after one production frame; transient terrain handoffs can reject every safe candidate in the current snapshot.
            attemptedCoordinates.Clear();
            yield return null;
        }

        result(false, new Vector2Int(int.MinValue, int.MinValue), 0, 0);
    }

    private static bool TryFindRecoveryCoordinate(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        out Vector2Int coordinate)
    {
        return TryFindRecoveryCoordinate(streamer, usedCoordinates, null, false, out coordinate);
    }

    private static bool TryFindRecoveryCoordinate(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        HashSet<Vector2Int> attemptedCoordinates,
        out Vector2Int coordinate)
    {
        return TryFindRecoveryCoordinate(streamer, usedCoordinates, attemptedCoordinates, false, out coordinate);
    }

    private static bool TryFindRecoveryCoordinate(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        HashSet<Vector2Int> attemptedCoordinates,
        bool allowPreparationOwners,
        out Vector2Int coordinate)
    {
        Vector2Int current = streamer.CurrentChunk;
        // note: Prefer nearby streamed owners so recovery publication competes with the player's real demand envelope rather than an unrelated retained ring.
        Vector2Int[] localCandidates =
        {
            current + new Vector2Int(1, 0), current + new Vector2Int(-1, 0),
            current + new Vector2Int(0, 1), current + new Vector2Int(0, -1),
            current + new Vector2Int(1, 1), current + new Vector2Int(-1, 1),
            current + new Vector2Int(1, -1), current + new Vector2Int(-1, -1),
            current + new Vector2Int(2, 0), current + new Vector2Int(-2, 0),
            current + new Vector2Int(0, 2), current + new Vector2Int(0, -2),
            current + new Vector2Int(3, 0), current + new Vector2Int(-3, 0),
            current + new Vector2Int(0, 3), current + new Vector2Int(0, -3),
            current + new Vector2Int(5, 0), current + new Vector2Int(-5, 0),
            current + new Vector2Int(0, 5), current + new Vector2Int(0, -5),
            current + new Vector2Int(4, 4), current + new Vector2Int(-4, 4),
            current + new Vector2Int(4, -4), current + new Vector2Int(-4, -4)
        };
        List<Vector2Int> candidates = new List<Vector2Int>(localCandidates.Length + 5);
        YQInvestorPlayerMotor liveMotor = YQInvestorPlayerMotor.ActiveMotor;
        Vector3 planarVelocity = liveMotor != null ? liveMotor.PlanarVelocity : Vector3.zero;
        Vector2 velocity = new Vector2(planarVelocity.x, planarVelocity.z);
        if (velocity.magnitude > 65f)
        {
            Vector2 direction = velocity.normalized;
            Vector2Int travelCell = new Vector2Int(
                Mathf.Abs(direction.x) >= 0.35f ? (direction.x >= 0f ? 1 : -1) : 0,
                Mathf.Abs(direction.y) >= 0.35f ? (direction.y >= 0f ? 1 : -1) : 0);
            // note: The real motor keeps coasting after the probe releases input; choose a demanded owner ahead of that measured travel so cancellation remains observable.
            for (int distance = 5; distance >= 2; distance--)
                candidates.Add(current + travelCell * distance);
        }
        candidates.AddRange(localCandidates);
        // note: Recovery fault injection must stay outside the current camera contract; the streamer separately rejects predicted and emergency-turn owners.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                Vector2Int candidate = candidates[index];
                if (usedCoordinates.Contains(candidate) ||
                    (attemptedCoordinates != null && attemptedCoordinates.Contains(candidate)) ||
                    candidate == current)
                    continue;
                if (!streamer.TryGetChunkDiagnostics(candidate, out _, out _) ||
                    !streamer.TryGetPublicationSnapshot(candidate, out YQSemanticChunkPublicationSnapshot snapshot) ||
                    snapshot.lifecycle == YQSemanticChunkLifecycle.Unseen || snapshot.authoredTerrain ||
                    (!allowPreparationOwners && snapshot.hardViewDemanded) || !snapshot.demanded)
                    continue;

                int distance = Mathf.Max(
                    Mathf.Abs(candidate.x - current.x),
                    Mathf.Abs(candidate.y - current.y));
                bool isolatedPhysicalOwner = snapshot.physicalRepresentation &&
                    !snapshot.guaranteedViewDemanded && distance >= 2 &&
                    distance <= streamer.VisualRetentionRadius;
                if (pass == 0 && !isolatedPhysicalOwner)
                    continue;
                // note: Probe outside the current camera contract; a non-visible preparation owner is safe when the production API confirms it has no near-term deadline.
                if (pass == 1 && (!snapshot.physicalRepresentation ||
                    (!allowPreparationOwners && snapshot.hardViewDemanded) || snapshot.guaranteedViewDemanded))
                    continue;
                coordinate = candidate;
                return true;
            }
        }
        // note: If the explicit witnesses were consumed by earlier recovery probes, search the complete bounded retention ring before declaring the matrix unable to continue.
        int retention = Mathf.Max(1, streamer.VisualRetentionRadius);
        for (int x = -retention; x <= retention; x++)
        {
            for (int z = -retention; z <= retention; z++)
            {
                Vector2Int candidate = current + new Vector2Int(x, z);
                if (usedCoordinates.Contains(candidate) ||
                    (attemptedCoordinates != null && attemptedCoordinates.Contains(candidate)) ||
                    candidate == current)
                    continue;
                if (!streamer.TryGetPublicationSnapshot(candidate, out YQSemanticChunkPublicationSnapshot snapshot) ||
                    !snapshot.physicalRepresentation || (!allowPreparationOwners && snapshot.hardViewDemanded) ||
                    snapshot.guaranteedViewDemanded || snapshot.authoredTerrain)
                    continue;
                coordinate = candidate;
                return true;
            }
        }
        coordinate = new Vector2Int(int.MinValue, int.MinValue);
        return false;
    }

    private static IEnumerator ObservePublicationPhase(
        StringBuilder report,
        string phase,
        YQPlayerFollowingSemanticChunkStreamer streamer,
        Vector2Int coordinate,
        bool prerequisiteMet,
        Func<YQSemanticChunkPublicationSnapshot, bool> predicate,
        Action<bool> result)
    {
        // note: Skipping a dependent observation preserves failure, without spending another deadline on an order that was never established.
        if (!prerequisiteMet)
        {
            report.AppendLine("- " + phase + ": SKIPPED prerequisite failed coordinate=" + coordinate);
            WriteReport(report, false, false);
            result(false);
            yield break;
        }
        report.AppendLine("- " + phase + ": STARTED coordinate=" + coordinate);
        WriteReport(report, false, false);
        bool completed = false;
        bool matched = false;
        YQSemanticChunkPublicationSnapshot observed = default;
        double started = Time.realtimeSinceStartupAsDouble;
        try
        {
            yield return WaitForPublicationSnapshot(streamer, coordinate, predicate, PublicationRecoveryTimeoutSeconds,
                (success, snapshot) => { completed = true; matched = success; observed = snapshot; });
            result(matched);
        }
        finally
        {
            // note: Iterator disposal also records an interrupted phase before the enclosing matrix writes its terminal receipt.
            report.AppendLine("- " + phase + ": " + (!completed ? "ABORTED" : matched ? "PASS" : "FAIL") +
                " coordinate=" + coordinate +
                " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - started).ToString("0.000", CultureInfo.InvariantCulture) +
                " explicitFailure=" + observed.hasExplicitFailure + " reason=" + (observed.failureReason ?? string.Empty));
            WriteReport(report, false, false);
        }
    }

    private static IEnumerator WaitForPublicationSnapshot(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        Vector2Int coordinate,
        Func<YQSemanticChunkPublicationSnapshot, bool> predicate,
        float timeoutSeconds,
        Action<bool, YQSemanticChunkPublicationSnapshot> result)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + Mathf.Max(0.1f, timeoutSeconds);
        YQSemanticChunkPublicationSnapshot lastSnapshot = default;
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            if (streamer.TryGetPublicationSnapshot(coordinate, out lastSnapshot) && predicate(lastSnapshot))
            {
                result(true, lastSnapshot);
                yield break;
            }
            yield return null;
        }
        result(false, lastSnapshot);
    }

    private static IEnumerator DriveProductionInput(
        UnityEngine.InputSystem.Keyboard keyboard,
        YQPlayerFollowingSemanticChunkStreamer streamer,
        YQInvestorPlayerMotor motor,
        Transform player,
        CharacterController characterController,
        StringBuilder report,
        float seconds,
        UnityEngine.InputSystem.Key key,
        Action<bool> validFrame,
        Action<bool, bool, bool> evidence = null,
        Func<bool> stopWhen = null,
        UnityEngine.InputSystem.Key secondaryKey = UnityEngine.InputSystem.Key.None,
        float noProgressTimeoutSeconds = 0f,
        Action<Vector3> positionObserved = null,
        Func<bool> frameSafety = null)
    {
        double started = Time.realtimeSinceStartupAsDouble;
        double deadline = started + Mathf.Max(0.05f, seconds);
        double lastFrame = started;
        double maximumFrameSeconds = 0.0;
        Vector3 startPosition = player.position;
        Vector3 lastProgressPosition = startPosition;
        double lastProgressAt = started;
        int observedFrames = 0;
        bool inputObserved = false;
        bool allFramesValid = true;
        bool stoppedForNoProgress = false;
        try
        {
            while (Time.realtimeSinceStartupAsDouble < deadline && (stopWhen == null || !stopWhen()))
            {
                // note: Fault probes deliver ordinary movement input while production alone owns acceleration, collision, camera and scheduling.
                UnityEngine.InputSystem.LowLevel.KeyboardState inputState = secondaryKey == UnityEngine.InputSystem.Key.None
                    ? new UnityEngine.InputSystem.LowLevel.KeyboardState(key)
                    : new UnityEngine.InputSystem.LowLevel.KeyboardState(key, secondaryKey);
                QueueKeyboardState(keyboard, inputState);
                yield return null;
                positionObserved?.Invoke(player.position);
                double now = Time.realtimeSinceStartupAsDouble;
                maximumFrameSeconds = Math.Max(maximumFrameSeconds, now - lastFrame);
                lastFrame = now;
                observedFrames++;
                Vector2 requestedInput = ResolveKeyMoveInput(key, secondaryKey);
                inputObserved |= motor.CanProcessMovementInput && requestedInput.sqrMagnitude > 0.01f &&
                    Vector2.Dot(motor.MoveInput, requestedInput) > 0.5f;
                bool valid = streamer.TryValidateCurrentTraversability(
                    player.position, characterController.radius, characterController.skinWidth, out string failure) &&
                    (frameSafety == null || frameSafety());
                if (!valid && allFramesValid)
                {
                    report.AppendLine("- recoveryMotorFailure: " + failure);
                    WriteReport(report, false, false);
                }
                allFramesValid &= valid;
                if (PlanarDistance(lastProgressPosition, player.position) > 0.05f)
                {
                    lastProgressPosition = player.position;
                    lastProgressAt = now;
                }
                if (noProgressTimeoutSeconds > 0f && now - lastProgressAt >= noProgressTimeoutSeconds)
                {
                    stoppedForNoProgress = true;
                    break;
                }
            }
        }
        finally
        {
            // note: Always release input on normal return, assertion failure, cancellation and manual Play Mode exit.
            QueueKeyboardState(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            float distance = PlanarDistance(startPosition, player.position);
            bool moved = observedFrames > 0 && inputObserved && distance > 0.02f;
            report.AppendLine("- recoveryMotorEvidence: keys=" + key +
                (secondaryKey == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + secondaryKey) + ",frames=" + observedFrames +
                ",inputObserved=" + inputObserved + ",planarDistance=" + distance.ToString("0.000", CultureInfo.InvariantCulture) +
                ",elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - started).ToString("0.000", CultureInfo.InvariantCulture) +
                ",maximumFrameSeconds=" + maximumFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ",stoppedForNoProgress=" + stoppedForNoProgress +
                ",traversalBlocks=" + streamer.TraversalConstraintBlockCount +
                ",traversalBlockCell=" + streamer.LastTraversalConstraintCell +
                ",valid=" + allFramesValid + ",moved=" + moved);
            WriteReport(report, false, false);
            // note: Safety validity is independent from whether the editor delivered a usable movement key; unavailable focus is reported separately as inconclusive.
            validFrame(allFramesValid);
            evidence?.Invoke(inputObserved, moved, allFramesValid);
        }
    }

    private static void ResolveMovementKeys(
        Vector2 desiredMoveInput,
        out UnityEngine.InputSystem.Key primaryKey,
        out UnityEngine.InputSystem.Key secondaryKey)
    {
        desiredMoveInput = Vector2.ClampMagnitude(desiredMoveInput, 1f);
        float forwardMagnitude = Mathf.Abs(desiredMoveInput.y);
        float lateralMagnitude = Mathf.Abs(desiredMoveInput.x);
        UnityEngine.InputSystem.Key forwardKey = desiredMoveInput.y >= 0f
            ? UnityEngine.InputSystem.Key.W
            : UnityEngine.InputSystem.Key.S;
        UnityEngine.InputSystem.Key lateralKey = desiredMoveInput.x >= 0f
            ? UnityEngine.InputSystem.Key.D
            : UnityEngine.InputSystem.Key.A;

        // note: Use the production motor's ordinary camera-relative keys to point toward the unloaded owner without changing the camera or player transform.
        if (forwardMagnitude >= lateralMagnitude)
        {
            primaryKey = forwardKey;
            secondaryKey = lateralMagnitude > 0.1f ? lateralKey : UnityEngine.InputSystem.Key.None;
        }
        else
        {
            primaryKey = lateralKey;
            secondaryKey = forwardMagnitude > 0.1f ? forwardKey : UnityEngine.InputSystem.Key.None;
        }
    }

    private static Vector2 ResolveKeyMoveInput(
        UnityEngine.InputSystem.Key primaryKey,
        UnityEngine.InputSystem.Key secondaryKey)
    {
        Vector2 input = ResolveKeyMoveInput(primaryKey);
        if (secondaryKey != UnityEngine.InputSystem.Key.None)
            input += ResolveKeyMoveInput(secondaryKey);
        return Vector2.ClampMagnitude(input, 1f);
    }

    private static Vector2 ResolveKeyMoveInput(UnityEngine.InputSystem.Key key)
    {
        if (key == UnityEngine.InputSystem.Key.W)
            return Vector2.up;
        if (key == UnityEngine.InputSystem.Key.S)
            return Vector2.down;
        if (key == UnityEngine.InputSystem.Key.D)
            return Vector2.right;
        if (key == UnityEngine.InputSystem.Key.A)
            return Vector2.left;
        return Vector2.zero;
    }

    private static void AppendProductionMovementDiagnostics(
        StringBuilder report,
        YQInvestorPlayerMotor motor,
        string phase,
        int blockingContactsBefore)
    {
        // note: Pair the motor request with its collision-resolved displacement and the exact side-contact owner for this movement phase.
        int newBlockingContacts = Math.Max(0, motor.BlockingControllerContactCount - blockingContactsBefore);
        Collider contact = newBlockingContacts > 0 ? motor.LastBlockingControllerCollider : null;
        string contactOwner = "<none this phase>";
        if (contact != null)
        {
            Bounds bounds = contact.bounds;
            contactOwner = DescribeColliderHierarchy(contact.transform) +
                ",instance=" + contact.GetInstanceID() +
                ",layer=" + LayerMask.LayerToName(contact.gameObject.layer) +
                ",tag=" + contact.tag +
                ",boundsCenter=" + bounds.center.ToString("F3") +
                ",boundsSize=" + bounds.size.ToString("F3");
        }

        report.AppendLine("- productionMotorMovementDiagnostics: phase=" + phase +
            ",position=" + motor.transform.position.ToString("F3") +
            ",forward=" + motor.transform.forward.ToString("F3") +
            ",input=" + motor.MoveInput.ToString("F3") +
            ",requestedVelocity=" + motor.PlanarVelocity.ToString("F3") +
            ",lastRequestedMove=" + motor.LastRequestedMoveDisplacement.ToString("F3") +
            ",lastActualMove=" + motor.LastActualMoveDisplacement.ToString("F3") +
            ",collisionFlags=" + motor.LastMoveCollisionFlags +
            ",readinessGateRejected=" + motor.LastMoveRejectedByTraversalGate +
            ",blockingSideContacts=" + newBlockingContacts +
            ",blockingContact=" + contactOwner +
            ",contactPoint=" + motor.LastBlockingControllerContactPoint.ToString("F3") +
            ",contactNormal=" + motor.LastBlockingControllerContactNormal.ToString("F3"));
    }

    private static string DescribeColliderHierarchy(Transform node)
    {
        // note: Build a stable human-readable owner path only when the bounded verification report is written.
        if (node == null)
            return "<none>";
        StringBuilder path = new StringBuilder(node.name);
        while (node.parent != null)
        {
            node = node.parent;
            path.Insert(0, node.name + "/");
        }
        return path.ToString();
    }

    private static void ValidateProductionMotorFrame(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        Transform player,
        CharacterController characterController,
        string phase,
        StringBuilder report)
    {
        if (streamer == null || player == null || characterController == null)
            throw new InvalidOperationException("production motor " + phase + " lost its authoritative owner");
        if (streamer.TryValidateCurrentTraversability(
                player.position,
                characterController.radius,
                characterController.skinWidth,
                out string failure))
            return;
        // note: Observe the real motor after its own Update without refreshing or reprioritizing the scheduler from the verifier.
        report.AppendLine("- productionMotor" + phase + "FailurePosition: " +
            player.position.ToString("0.000", CultureInfo.InvariantCulture));
        report.AppendLine("- productionMotor" + phase + "Failure: " + failure);
        WriteReport(report, false, false);
        throw new InvalidOperationException("production motor " + phase + " entered unready traversal space: " + failure);
    }

    private static void VerifyMacroFieldHashParity(StringBuilder report)
    {
        // note: Compare optimized world-field hashes with the prior string-concatenation implementation across signed coordinates and normalized seeds.
        string[] seeds = { "06c4f7d8", "beta-origin-v1", "   ", "  deterministic seed  " };
        Vector2[] points =
        {
            Vector2.zero,
            new Vector2(1234.5f, -678.25f),
            new Vector2(-1024.5f, -64.25f),
            new Vector2(17f, 3000f)
        };
        int sampleCount = 0;
        float maximumDelta = 0f;
        for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
        {
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                float elevation;
                float ruggedness;
                float temperature;
                float moisture;
                float forest;
                float grassland;
                float wetland;
                float civilization;
                float danger;
                float culture;
                YQSemanticWorldAuthority.SampleMacroFields(
                    seeds[seedIndex], points[pointIndex].x, points[pointIndex].y,
                    out elevation, out ruggedness, out temperature, out moisture,
                    out forest, out grassland, out wetland, out civilization, out danger, out culture);
                float[] actual = { elevation, ruggedness, temperature, moisture, forest, grassland, wetland, civilization, danger, culture };
                float[] reference = SampleMacroFieldsReference(seeds[seedIndex], points[pointIndex].x, points[pointIndex].y);
                for (int field = 0; field < actual.Length; field++)
                {
                    float delta = Mathf.Abs(actual[field] - reference[field]);
                    maximumDelta = Mathf.Max(maximumDelta, delta);
                    if (actual[field] != reference[field])
                        throw new InvalidOperationException("optimized macro-field hash changed output at seedIndex=" + seedIndex +
                            " pointIndex=" + pointIndex + " field=" + field + " delta=" + delta.ToString("R", CultureInfo.InvariantCulture));
                }
                sampleCount++;
            }
        }
        report.AppendLine("- macroFieldHashParity: PASS seedCoordinateSamples=" + sampleCount +
            " maximumDelta=" + maximumDelta.ToString("0.000000", CultureInfo.InvariantCulture));
    }

    private static float[] SampleMacroFieldsReference(string seed, float worldX, float worldZ)
    {
        string safeSeed = string.IsNullOrWhiteSpace(seed) ? "yourquest_default_world" : seed.Trim();
        float broad = ValueNoiseReference(worldX * 0.0018f, worldZ * 0.0018f, safeSeed + "|landform");
        float ridge = ValueNoiseReference(worldX * 0.0031f, worldZ * 0.0031f, safeSeed + "|ridge");
        float basin = ValueNoiseReference(worldX * 0.0012f, worldZ * 0.0012f, safeSeed + "|basin");
        float moistureField = ValueNoiseReference(worldX * 0.0015f, worldZ * 0.0015f, safeSeed + "|climate_moisture");
        float temperatureField = ValueNoiseReference(worldX * 0.0011f, worldZ * 0.0011f, safeSeed + "|climate_temperature");
        float ecologyField = ValueNoiseReference(worldX * 0.0042f, worldZ * 0.0042f, safeSeed + "|ecology");
        float cultureField = ValueNoiseReference(worldX * 0.0017f, worldZ * 0.0017f, safeSeed + "|culture");
        float dangerField = ValueNoiseReference(worldX * 0.0029f, worldZ * 0.0029f, safeSeed + "|danger");
        float ruggedness = Mathf.Clamp01(Mathf.Abs(ridge * 2f - 1f) * 0.62f + broad * 0.18f);
        float elevation = Mathf.Clamp01(0.28f + broad * 0.18f + ruggedness * 0.24f - basin * 0.11f);
        float temperature = Mathf.Clamp01(temperatureField * 0.82f + 0.09f);
        float moisture = Mathf.Clamp01(moistureField * 0.82f + basin * 0.14f);
        float forest = Mathf.Clamp01((moisture * 0.72f + ecologyField * 0.28f) * (1f - temperature * 0.22f));
        float wetland = Mathf.Clamp01(moisture * 0.72f + (1f - elevation) * 0.2f + (1f - ecologyField) * 0.08f);
        float grassland = Mathf.Clamp01((1f - ruggedness) * 0.46f + (1f - wetland) * 0.34f + ecologyField * 0.2f);
        float total = Mathf.Max(0.001f, forest + grassland + wetland);
        float ecologyForest = forest / total;
        float ecologyGrassland = grassland / total;
        float ecologyWetland = wetland / total;
        float civilization = Mathf.Clamp01((1f - ruggedness) * 0.42f + cultureField * 0.36f + ecologyGrassland * 0.22f);
        float danger = Mathf.Clamp01(dangerField * 0.58f + ruggedness * 0.27f + (1f - civilization) * 0.15f);
        float culture = Mathf.Clamp01(cultureField * 0.8f + ecologyGrassland * 0.1f + (1f - danger) * 0.1f);
        return new[] { elevation, ruggedness, temperature, moisture, ecologyForest, ecologyGrassland, ecologyWetland, civilization, danger, culture };
    }

    private static float ValueNoiseReference(float x, float z, string seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);
        float tx = x - x0;
        float tz = z - z0;
        tx = tx * tx * (3f - 2f * tx);
        tz = tz * tz * (3f - 2f * tz);
        float a = Hash01Reference(seed + "|" + x0 + "|" + z0);
        float b = Hash01Reference(seed + "|" + (x0 + 1) + "|" + z0);
        float c = Hash01Reference(seed + "|" + x0 + "|" + (z0 + 1));
        float d = Hash01Reference(seed + "|" + (x0 + 1) + "|" + (z0 + 1));
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }

    private static float Hash01Reference(string value)
    {
        uint hash = 2166136261u;
        string safe = value ?? string.Empty;
        for (int index = 0; index < safe.Length; index++)
        {
            unchecked
            {
                hash ^= safe[index];
                hash *= 16777619u;
            }
        }
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    private static UnityEngine.InputSystem.LowLevel.KeyboardState KeyboardStateForMotorDirection(
        YQInvestorPlayerMotor motor,
        Vector3 worldDirection,
        out Vector2 expectedMoveInput)
    {
        expectedMoveInput = Vector2.zero;
        if (motor == null)
            return new UnityEngine.InputSystem.LowLevel.KeyboardState();

        // note: Convert a world-space test heading to ordinary WASD relative to the authoritative motor's current basis.
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f)
            return new UnityEngine.InputSystem.LowLevel.KeyboardState();
        worldDirection.Normalize();
        Vector3 forward = motor.transform.forward;
        Vector3 right = motor.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        float localX = Vector3.Dot(worldDirection, right);
        float localY = Vector3.Dot(worldDirection, forward);
        int xSign = localX >= 0.25f ? 1 : localX <= -0.25f ? -1 : 0;
        int ySign = localY >= 0.25f ? 1 : localY <= -0.25f ? -1 : 0;
        expectedMoveInput = Vector2.ClampMagnitude(new Vector2(xSign, ySign), 1f);

        UnityEngine.InputSystem.Key horizontal = xSign < 0
            ? UnityEngine.InputSystem.Key.A
            : xSign > 0 ? UnityEngine.InputSystem.Key.D : UnityEngine.InputSystem.Key.None;
        UnityEngine.InputSystem.Key vertical = ySign < 0
            ? UnityEngine.InputSystem.Key.S
            : ySign > 0 ? UnityEngine.InputSystem.Key.W : UnityEngine.InputSystem.Key.None;
        if (horizontal != UnityEngine.InputSystem.Key.None && vertical != UnityEngine.InputSystem.Key.None)
            return new UnityEngine.InputSystem.LowLevel.KeyboardState(horizontal, vertical);
        if (horizontal != UnityEngine.InputSystem.Key.None)
            return new UnityEngine.InputSystem.LowLevel.KeyboardState(horizontal);
        if (vertical != UnityEngine.InputSystem.Key.None)
            return new UnityEngine.InputSystem.LowLevel.KeyboardState(vertical);
        return new UnityEngine.InputSystem.LowLevel.KeyboardState();
    }

    private static Vector3 MotorWorldPlanarDirection(YQInvestorPlayerMotor motor, Vector2 moveInput)
    {
        if (motor == null)
            return Vector3.forward;
        Vector3 direction = motor.transform.forward * moveInput.y + motor.transform.right * moveInput.x;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    private static bool IsExpectedMotorInput(Vector2 actualInput, Vector2 expectedInput)
    {
        // note: Compare the motor's consumed axes with the keyboard event sent for the same real Input System frame.
        return expectedInput.sqrMagnitude > 0.5f && Vector2.Distance(actualInput, expectedInput) <= 0.15f;
    }

    private static int HighSpeedTransitionMaskBit(Vector2 expectedInput)
    {
        // note: Preserve the six signed transition classes while the speed lane follows an origin-safe eight-direction loop.
        if (expectedInput.x > 0.1f && expectedInput.y > 0.1f) return 0;
        if (expectedInput.x > 0.1f && expectedInput.y < -0.1f) return 1;
        if (expectedInput.x < -0.1f && expectedInput.y < -0.1f) return 2;
        if (expectedInput.x < -0.1f && expectedInput.y > 0.1f) return 3;
        if (Mathf.Abs(expectedInput.x) <= 0.1f && expectedInput.y < -0.1f) return 4;
        if (Mathf.Abs(expectedInput.x) <= 0.1f && expectedInput.y > 0.1f) return 5;
        return -1;
    }

    private static void QueueKeyboardState(
        UnityEngine.InputSystem.Keyboard keyboard,
        UnityEngine.InputSystem.LowLevel.KeyboardState state)
    {
        if (keyboard == null)
            return;
        // note: Queue a normal device event for the next Dynamic Input System update so wasPressedThisFrame observes a real edge instead of a late direct-buffer mutation.
        if (!keyboard.enabled)
            UnityEngine.InputSystem.InputSystem.EnableDevice(keyboard);
        keyboard.MakeCurrent();
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, state);
        // note: Keep the named test device current after queuing so a physical keyboard cannot silently replace the device read by the next motor frame.
        keyboard.MakeCurrent();
    }

    private static void AppendInjectedKeyboardDiagnostics(
        StringBuilder report,
        UnityEngine.InputSystem.Keyboard injectedKeyboard,
        string phase)
    {
        // note: Capture the live Input System state at the first post-event motor sample so a failed probe distinguishes event delivery from locomotion rejection.
        UnityEngine.InputSystem.Keyboard currentKeyboard = UnityEngine.InputSystem.Keyboard.current;
        report.AppendLine("- productionInputDiagnostics: phase=" + phase +
            ",injected=" + (injectedKeyboard != null ? injectedKeyboard.name : "<null>") +
            ",injectedAdded=" + (injectedKeyboard != null && injectedKeyboard.added) +
            ",injectedW=" + (injectedKeyboard != null && injectedKeyboard.wKey.isPressed) +
            ",injectedQ=" + (injectedKeyboard != null && injectedKeyboard.qKey.isPressed) +
            ",current=" + (currentKeyboard != null ? currentKeyboard.name : "<null>") +
            ",currentW=" + (currentKeyboard != null && currentKeyboard.wKey.isPressed) +
            ",currentQ=" + (currentKeyboard != null && currentKeyboard.qKey.isPressed) +
            ",updateType=" + UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType +
            ",updateCount=" + UnityEngine.InputSystem.LowLevel.InputState.updateCount);
        WriteReport(report, false, false);
    }

    private static UnityEngine.InputSystem.Keyboard AcquireVerificationKeyboard()
    {
        ConfigureVerificationInputRouting();
        // note: Reuse the session-scoped probe device when Unity has already created it; never remove devices during Play because that can invalidate InputSystem global state.
        foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
        {
            if (device is UnityEngine.InputSystem.Keyboard existing &&
                string.Equals(existing.name, "YQRuntimeVerificationKeyboard", StringComparison.Ordinal))
            {
                HookVerificationKeyboard(existing);
                // note: A prior failed probe may have left the private virtual device disabled after focus loss; restore only this probe device.
                if (!existing.enabled)
                    UnityEngine.InputSystem.InputSystem.EnableDevice(existing);
                return existing;
            }
        }
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>(
            "YQRuntimeVerificationKeyboard");
        HookVerificationKeyboard(keyboard);
        // note: Keep the verifier's private virtual device enabled when the editor loses focus; never enable or mutate the user's physical devices.
        if (!keyboard.enabled)
            UnityEngine.InputSystem.InputSystem.EnableDevice(keyboard);
        return keyboard;
    }

    private static void HookVerificationKeyboard(UnityEngine.InputSystem.Keyboard keyboard)
    {
        _verificationKeyboard = keyboard;
        if (_verificationKeyboardHooked)
            return;
        // note: Reassert current-device ownership after all queued/native events are processed, before the next production motor Update.
        UnityEngine.InputSystem.InputSystem.onAfterUpdate += KeepVerificationKeyboardCurrent;
        _verificationKeyboardHooked = true;
    }

    private static void KeepVerificationKeyboardCurrent()
    {
        if (_verificationKeyboard != null && _verificationKeyboard.added)
            _verificationKeyboard.MakeCurrent();
    }

    private static void ReleaseVerificationKeyboardHook()
    {
        if (_verificationKeyboardHooked)
            UnityEngine.InputSystem.InputSystem.onAfterUpdate -= KeepVerificationKeyboardCurrent;
        _verificationKeyboardHooked = false;
        _verificationKeyboard = null;
        // note: Restore the exact pre-verification settings object before destroying the temporary routing copy.
        if (_verificationOriginalInputSettings != null)
            UnityEngine.InputSystem.InputSystem.settings = _verificationOriginalInputSettings;
        if (_verificationTestInputSettings != null)
        {
            UnityEngine.Object.Destroy(_verificationTestInputSettings);
            _verificationTestInputSettings = null;
        }
        _verificationOriginalInputSettings = null;
    }

    private static void ConfigureVerificationInputRouting()
    {
        if (_verificationTestInputSettings != null &&
            UnityEngine.InputSystem.InputSystem.settings == _verificationTestInputSettings)
            return;

        // note: A private settings clone routes test-device events while unfocused without persisting changes to the project's Input System asset.
        _verificationOriginalInputSettings = UnityEngine.InputSystem.InputSystem.settings;
        if (_verificationOriginalInputSettings == null)
            return;
        _verificationTestInputSettings = UnityEngine.Object.Instantiate(_verificationOriginalInputSettings);
        _verificationTestInputSettings.name = "YQ Runtime Verification Input Settings";
        _verificationTestInputSettings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        _verificationTestInputSettings.editorInputBehaviorInPlayMode =
            UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        UnityEngine.InputSystem.InputSystem.settings = _verificationTestInputSettings;
    }

    private static float PlanarDistance(Vector3 from, Vector3 to)
    {
        // note: Measure only horizontal displacement so falling/ground correction cannot masquerade as locomotion progress.
        Vector2 delta = new Vector2(to.x - from.x, to.z - from.z);
        return delta.magnitude;
    }

    private static Vector3 EstimatePlanarMotorDisplacement(
        Vector3 initialVelocity,
        Vector3 targetVelocity,
        float acceleration,
        float seconds)
    {
        initialVelocity.y = 0f;
        targetVelocity.y = 0f;
        float duration = Mathf.Max(0f, seconds);
        Vector3 velocityDelta = targetVelocity - initialVelocity;
        float velocityChange = velocityDelta.magnitude;
        float accelerationRate = Mathf.Max(0.01f, acceleration);
        if (velocityChange <= 0.0001f)
            return initialVelocity * duration;

        Vector3 accelerationDirection = velocityDelta / velocityChange;
        float accelerationDuration = velocityChange / accelerationRate;
        if (duration <= accelerationDuration)
            return initialVelocity * duration + accelerationDirection * (0.5f * accelerationRate * duration * duration);

        float acceleratedDistanceTime = accelerationDuration;
        return initialVelocity * acceleratedDistanceTime +
            accelerationDirection * (0.5f * accelerationRate * acceleratedDistanceTime * acceleratedDistanceTime) +
            targetVelocity * (duration - acceleratedDistanceTime);
    }

    private static bool HasChunkClearanceAt(
        Vector3 center,
        Vector2Int owner,
        float chunkSize,
        float clearanceMeters)
    {
        // note: Check the controller's padded footprint at every projected path sample, not only the requested endpoint.
        Vector3 rightClearance = Vector3.right * Mathf.Max(0f, clearanceMeters);
        Vector3 forwardClearance = Vector3.forward * Mathf.Max(0f, clearanceMeters);
        return ChunkCoordinateAt(center, chunkSize) == owner &&
            ChunkCoordinateAt(center + rightClearance, chunkSize) == owner &&
            ChunkCoordinateAt(center - rightClearance, chunkSize) == owner &&
            ChunkCoordinateAt(center + forwardClearance, chunkSize) == owner &&
            ChunkCoordinateAt(center - forwardClearance, chunkSize) == owner;
    }

    private static void AppendReturnWaypoint(List<Vector3> waypoints, Vector3 position)
    {
        // note: Store only physical motor positions, not a synthesized path, and suppress tiny clearance duplicates.
        if (waypoints == null ||
            (waypoints.Count > 0 && PlanarDistance(waypoints[waypoints.Count - 1], position) <= 0.25f))
            return;
        waypoints.Add(position);
    }

    private static Vector2Int ChunkCoordinateAt(Vector3 position, float chunkSize)
    {
        // note: Map a world waypoint back through the same absolute grid origin used by streamed terrain ownership.
        float size = Mathf.Max(1f, chunkSize);
        return new Vector2Int(
            Mathf.FloorToInt((position.x - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / size),
            Mathf.FloorToInt((position.z - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / size));
    }

    private static bool TryGetNextChunkBoundary(
        Vector3 position,
        Vector3 travelDirection,
        float chunkSize,
        out float distanceMeters,
        out Vector2Int sourceCell,
        out Vector2Int targetCell)
    {
        // note: Align the runtime motor with the first absolute world-cell edge on its actual planar path, including negative coordinates.
        float size = Mathf.Max(32f, chunkSize);
        Vector2 direction = new Vector2(travelDirection.x, travelDirection.z);
        sourceCell = ChunkCoordinateAt(position, size);
        targetCell = sourceCell;
        distanceMeters = float.PositiveInfinity;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;
        direction.Normalize();

        if (direction.x > 0.0001f)
        {
            float boundaryX = YQContinuousWorldFeatureAuthority.WorldGridOrigin + (sourceCell.x + 1) * size;
            distanceMeters = Mathf.Max(0f, (boundaryX - position.x) / direction.x);
            targetCell.x++;
        }
        else if (direction.x < -0.0001f)
        {
            float boundaryX = YQContinuousWorldFeatureAuthority.WorldGridOrigin + sourceCell.x * size;
            distanceMeters = Mathf.Max(0f, (boundaryX - position.x) / direction.x);
            targetCell.x--;
        }

        if (direction.y > 0.0001f)
        {
            float boundaryZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin + (sourceCell.y + 1) * size;
            float candidateDistance = Mathf.Max(0f, (boundaryZ - position.z) / direction.y);
            if (candidateDistance < distanceMeters)
            {
                distanceMeters = candidateDistance;
                targetCell = sourceCell + Vector2Int.up;
            }
        }
        else if (direction.y < -0.0001f)
        {
            float boundaryZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin + sourceCell.y * size;
            float candidateDistance = Mathf.Max(0f, (boundaryZ - position.z) / direction.y);
            if (candidateDistance < distanceMeters)
            {
                distanceMeters = candidateDistance;
                targetCell = sourceCell + Vector2Int.down;
            }
        }

        return !float.IsInfinity(distanceMeters) && !float.IsNaN(distanceMeters);
    }

    private static Transform ResolveAuthoritativePlayer()
    {
        YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;
        if (motor != null && motor.IsAuthoritative)
            return motor.transform;
        // note: A tagged object without the authoritative motor is not a valid production traversal subject and must never satisfy this probe.
        return null;
    }

    private static float SampleSharedEdgeDelta(Terrain origin, Terrain extension, string edge)
    {
        // note: Compare the exact shared border at multiple points across the overlap so a single matching corner cannot hide a seam.
        if (origin == null || extension == null || origin.terrainData == null || extension.terrainData == null)
            return float.PositiveInfinity;
        bool vertical = edge == "east" || edge == "west";
        float boundary = vertical
            ? (edge == "east" ? origin.transform.position.x + origin.terrainData.size.x : origin.transform.position.x)
            : (edge == "north" ? origin.transform.position.z + origin.terrainData.size.z : origin.transform.position.z);
        float overlapMin = vertical
            ? Mathf.Max(origin.transform.position.z, extension.transform.position.z)
            : Mathf.Max(origin.transform.position.x, extension.transform.position.x);
        float overlapMax = vertical
            ? Mathf.Min(origin.transform.position.z + origin.terrainData.size.z, extension.transform.position.z + extension.terrainData.size.z)
            : Mathf.Min(origin.transform.position.x + origin.terrainData.size.x, extension.transform.position.x + extension.terrainData.size.x);
        if (overlapMax < overlapMin)
            return float.PositiveInfinity;
        float maximum = 0f;
        for (int sample = 0; sample <= 32; sample++)
        {
            float along = Mathf.Lerp(overlapMin, overlapMax, sample / 32f);
            Vector3 point = vertical ? new Vector3(boundary, 0f, along) : new Vector3(along, 0f, boundary);
            float originHeight = origin.SampleHeight(point);
            float extensionHeight = extension.SampleHeight(point);
            maximum = Mathf.Max(maximum, Mathf.Abs(originHeight - extensionHeight));
        }
        return maximum;
    }

    private static bool TryGetReadyStreamedTerrainAt(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        Terrain authoredTerrain,
        Vector3 position,
        out Terrain terrain)
    {
        terrain = FindTerrainAt(position);
        if (streamer == null || terrain == null || terrain == authoredTerrain ||
            !terrain.enabled || !terrain.gameObject.activeInHierarchy)
            return false;
        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null || !collider.enabled)
            return false;

        float chunkSize = Mathf.Max(1f, streamer.chunkWorldSize);
        Vector2Int coordinate = new Vector2Int(
            Mathf.FloorToInt((position.x - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize),
            Mathf.FloorToInt((position.z - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize));
        return streamer.TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot snapshot) &&
            !snapshot.authoredTerrain && snapshot.physicalRepresentation &&
            snapshot.terrainReadiness >= YQTerrainReadinessState.CollisionReady && snapshot.traversable;
    }

    private static bool TryValidateCanonicalContinuationBoundarySamples(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        YQContinuousWorldCellAuthority authority,
        Terrain originTerrain,
        Vector3[] publicationProbePositions,
        out int sampleCount,
        out int demandedWorkContracts,
        out string failure)
    {
        sampleCount = 0;
        demandedWorkContracts = 0;
        failure = string.Empty;
        if (streamer == null || authority == null || originTerrain == null || originTerrain.terrainData == null ||
            publicationProbePositions == null)
        {
            failure = "canonical continuation sample owner is missing";
            return false;
        }

        // note: Probe the canonical height authority at each unmaterialized edge without admitting Terrain owners just for verification.
        Vector3 origin = originTerrain.transform.position;
        Vector3 size = originTerrain.terrainData.size;
        float inset = Mathf.Clamp(Mathf.Max(0.05f, streamer.chunkWorldSize * 0.005f), 0.05f, 0.5f);
        float minimumX = origin.x;
        float maximumX = origin.x + size.x;
        float minimumZ = origin.z;
        float maximumZ = origin.z + size.z;
        float centerX = (minimumX + maximumX) * 0.5f;
        float centerZ = (minimumZ + maximumZ) * 0.5f;
        Vector2[] samplePositions =
        {
            new Vector2(minimumX + inset, centerZ), new Vector2(minimumX - inset, centerZ),
            new Vector2(maximumX - inset, centerZ), new Vector2(maximumX + inset, centerZ),
            new Vector2(centerX, minimumZ + inset), new Vector2(centerX, minimumZ - inset),
            new Vector2(centerX, maximumZ - inset), new Vector2(centerX, maximumZ + inset),
            new Vector2(minimumX - inset, minimumZ - inset), new Vector2(minimumX - inset, maximumZ + inset),
            new Vector2(maximumX + inset, minimumZ - inset), new Vector2(maximumX + inset, maximumZ + inset)
        };
        HashSet<Vector2Int> checkedDemandedOwners = new HashSet<Vector2Int>();
        for (int index = 0; index < samplePositions.Length; index++)
        {
            Vector2 point = samplePositions[index];
            float height = authority.SampleHeightNormalized(point.x, point.y);
            if (float.IsNaN(height) || float.IsInfinity(height))
            {
                failure = "canonical continuation height was non-finite at " + point;
                return false;
            }
            sampleCount++;

            float chunkSize = Mathf.Max(1f, streamer.chunkWorldSize);
            Vector2Int coordinate = new Vector2Int(
                Mathf.FloorToInt((point.x - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize),
                Mathf.FloorToInt((point.y - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize));
            if (!checkedDemandedOwners.Add(coordinate))
                continue;
            bool hasSnapshot = streamer.TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot snapshot);
            if (!snapshot.demanded || (hasSnapshot && snapshot.traversable))
                continue;
            if (!streamer.TryValidatePublicationWorkContract(coordinate, out failure))
                return false;
            demandedWorkContracts++;
        }

        // note: Check the exact physical probe owners too; the authored terrain size need not align with the continuous cell grid.
        for (int index = 0; index < publicationProbePositions.Length; index++)
        {
            Vector3 point = publicationProbePositions[index];
            float chunkSize = Mathf.Max(1f, streamer.chunkWorldSize);
            Vector2Int coordinate = new Vector2Int(
                Mathf.FloorToInt((point.x - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize),
                Mathf.FloorToInt((point.z - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize));
            if (!checkedDemandedOwners.Add(coordinate))
                continue;
            bool hasSnapshot = streamer.TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot snapshot);
            if (!snapshot.demanded || (hasSnapshot && snapshot.traversable))
                continue;
            if (!streamer.TryValidatePublicationWorkContract(coordinate, out failure))
                return false;
            demandedWorkContracts++;
        }
        return true;
    }

    private static Terrain FindTerrainAt(Vector3 position, Transform preferredRoot = null)
    {
        Terrain[] terrains = preferredRoot != null
            ? preferredRoot.GetComponentsInChildren<Terrain>(true)
            : FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        Terrain inclusiveFallback = null;
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            // note: Use the same half-open border convention as ChunkFor so a shared edge resolves to one canonical tile instead of whichever Terrain Unity enumerates first.
            if (position.x >= origin.x && position.x < origin.x + size.x &&
                position.z >= origin.z && position.z < origin.z + size.z)
                return terrain;
            if (inclusiveFallback == null && position.x >= origin.x && position.x <= origin.x + size.x &&
                position.z >= origin.z && position.z <= origin.z + size.z)
                inclusiveFallback = terrain;
        }
        return inclusiveFallback;
    }

    private static bool TryGetTerrainSurfaceY(Vector3 position, out float surfaceY)
    {
        Terrain terrain = FindTerrainAt(position);
        if (terrain == null || terrain.terrainData == null)
        {
            surfaceY = 0f;
            return false;
        }

        // note: Use the same world-space terrain authority as current-cell validation, preserving the probe's player-to-surface offset across streamed tiles.
        surfaceY = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, position);
        return !float.IsNaN(surfaceY) && !float.IsInfinity(surfaceY);
    }

    private static bool IsTerrainReadyForSettle(YQPlayerFollowingSemanticChunkStreamer streamer, bool requireRequiredCoverage, out string terrainFailure)
    {
        // note: Initial/far/rebound checkpoints require the full acceptance neighborhood; ordinary movement only needs its current published tile.
        if (streamer == null)
        {
            terrainFailure = "streamer is null";
            return false;
        }
        return requireRequiredCoverage
            ? streamer.TryValidateRequiredCoverage(out terrainFailure)
            : streamer.TryValidateCurrentTerrain(out terrainFailure);
    }

    private static IEnumerator WaitForCondition(Func<bool> condition, float timeout, Action<bool> result)
    {
        // note: Observation deadlines use real elapsed time, including stalls, rather than a scaled or synthetic simulation clock.
        double deadline = Time.realtimeSinceStartupAsDouble + timeout;
        while (Time.realtimeSinceStartupAsDouble < deadline && !condition())
            yield return null;
        result(condition());
    }

    private static YQStreamedFeatureOverlayTarget FindActiveFeatureOverlayTarget()
    {
        YQStreamedFeatureOverlayTarget[] targets =
            FindObjectsByType<YQStreamedFeatureOverlayTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        YQStreamedFeatureOverlayTarget persistedFallback = null;
        YQStreamedFeatureOverlayTarget preferredOrigin = null;
        YQStreamedFeatureOverlayTarget nearestActive = null;
        float nearestActiveDistanceSquared = float.MaxValue;
        Transform activePlayer = YQInvestorPlayerMotor.ActiveMotor != null
            ? YQInvestorPlayerMotor.ActiveMotor.transform
            : null;
        for (int index = 0; index < targets.Length; index++)
        {
            YQStreamedFeatureOverlayTarget target = targets[index];
            if (target == null || string.IsNullOrWhiteSpace(target.featureId))
                continue;
            if (target.gameObject.activeInHierarchy)
            {
                // note: Prefer the pinned origin feature so save/reload/rebind validates one deterministic, always-published target instead of a distance-dependent site.
                if (string.Equals(target.featureId, "origin_vey_witch_house", StringComparison.OrdinalIgnoreCase))
                    preferredOrigin = target;
                else
                {
                    // note: Prefer the nearest active generated feature so the rebind proof stays inside the current published frontier rather than choosing an unloaded distant provider.
                    float distanceSquared = activePlayer != null
                        ? (target.transform.position - activePlayer.position).sqrMagnitude
                        : 0f;
                    if (nearestActive == null || distanceSquared < nearestActiveDistanceSquared)
                    {
                        nearestActive = target;
                        nearestActiveDistanceSquared = distanceSquared;
                    }
                }
                continue;
            }
            // note: Repeated verification may intentionally leave the prior real target tombstoned; reuse it so the next idempotent overlay round trip remains production-backed.
            persistedFallback ??= target;
        }
        return preferredOrigin ?? nearestActive ?? persistedFallback;
    }

    private static YQStreamedFeatureOverlayTarget FindFeatureOverlayTarget(string featureId)
    {
        if (string.IsNullOrWhiteSpace(featureId))
            return null;
        YQStreamedFeatureOverlayTarget[] targets =
            FindObjectsByType<YQStreamedFeatureOverlayTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        YQStreamedFeatureOverlayTarget activeMatch = null;
        for (int index = 0; index < targets.Length; index++)
        {
            YQStreamedFeatureOverlayTarget target = targets[index];
            if (target != null && string.Equals(target.featureId, featureId, StringComparison.Ordinal))
            {
                // note: Prefer an inactive matching target so a durable tombstone is validated on the rebuilt feature instead of being masked by an active duplicate during rebind.
                if (!target.gameObject.activeSelf)
                    return target;
                activeMatch ??= target;
            }
        }
        return activeMatch;
    }

    private static string BuildChunkSignature(GeneratedWorldPlanRecord plan)
    {
        if (plan == null || plan.semanticChunks == null)
            return string.Empty;
        StringBuilder signature = new StringBuilder();
        for (int i = 0; i < plan.semanticChunks.Count; i++)
        {
            GeneratedSemanticChunkRecord record = plan.semanticChunks[i];
            if (record == null)
                continue;
            // note: Persist every deterministic continuation contract so save/reload verification catches lost terrain, biome, route, or water authority rather than only matching chunk IDs.
            signature.Append(record.chunkId).Append('|').Append(record.worldSeed).Append('|')
                .Append(record.chunkX).Append('|').Append(record.chunkZ).Append('|')
                .Append(record.deterministicSeed).Append('|').Append(record.lifecycleState).Append('|')
                .Append(record.parentRegionId).Append('|').Append(record.biome).Append('|')
                .Append(record.continuationSchemaVersion).Append('|').Append(record.seamFingerprint).Append('|')
                .Append(record.lastVisitedUnix).Append('|');
            AppendStringList(signature, record.featureIds);
            if (record.sites != null)
            {
                for (int siteIndex = 0; siteIndex < record.sites.Count; siteIndex++)
                {
                    GeneratedSemanticSiteRecord site = record.sites[siteIndex];
                    if (site == null)
                    {
                        signature.Append("<null-site>;");
                        continue;
                    }
                    signature.Append(site.siteId).Append('|').Append(site.semanticKind).Append('|')
                        .Append(site.assetSlot).Append('|').Append(site.biome).Append('|')
                        .Append(site.lifecycleState).Append('|')
                        .Append(site.ownerCellId).Append('|').Append(site.isOwnerCell).Append('|')
                        .Append(site.worldX.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(site.worldZ.ToString("R", CultureInfo.InvariantCulture)).Append('|');
                    AppendStringList(signature, site.memberCellIds);
                    signature.Append(';');
                }
            }
            AppendStringList(signature, record.borderContracts);
            AppendFloatList(signature, record.biomeWeights);
            AppendStringList(signature, record.persistentDeltaIds);
            if (record.edgeContracts != null)
            {
                for (int edgeIndex = 0; edgeIndex < record.edgeContracts.Count; edgeIndex++)
                {
                    GeneratedSemanticChunkEdgeContractRecord edge = record.edgeContracts[edgeIndex];
                    if (edge == null)
                    {
                        signature.Append("<null-edge>;");
                        continue;
                    }
                    signature.Append(edge.edge).Append('|').Append(edge.canonicalKey).Append('|')
                        .Append(edge.contractVersion).Append('|').Append(edge.sampleCount).Append('|')
                        .Append(edge.contractFingerprint).Append('|');
                    AppendFloatList(signature, edge.terrainHeights);
                    AppendFloatList(signature, edge.biomeWeights);
                    AppendPortalList(signature, edge.routePortals);
                    AppendPortalList(signature, edge.waterPortals);
                }
            }
            signature.Append(';');
        }
        return signature.ToString();
    }

    private static bool TryValidateBiomeTransition(
        YQContinuousWorldCellAuthority authority,
        Terrain terrain,
        out string failure,
        out float maximumDelta)
    {
        failure = string.Empty;
        maximumDelta = 0f;
        if (authority == null || terrain == null || terrain.terrainData == null)
        {
            failure = "biome authority or terrain is unavailable";
            return false;
        }
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        // note: Macro biome fields intentionally transition over hundreds of metres, so inspect a broad world-space window instead of only the authored tile.
        float minimumX = origin.x - 4096f;
        float maximumX = origin.x + 4096f;
        float minimumZ = origin.z - 4096f;
        float maximumZ = origin.z + 4096f;
        bool foundDifferentDominants = false;
        for (float z = minimumZ; z <= maximumZ && !foundDifferentDominants; z += 64f)
        {
            for (float x = minimumX; x <= maximumX - 32f; x += 32f)
            {
                string firstBiome = authority.ResolveDominantBiome(x, z);
                string secondBiome = authority.ResolveDominantBiome(x + 32f, z);
                if (string.Equals(firstBiome, secondBiome, StringComparison.Ordinal))
                    continue;
                foundDifferentDominants = true;
                List<float> firstWeights = authority.SampleBiomeWeights(x + 8f, z);
                List<float> secondWeights = authority.SampleBiomeWeights(x + 24f, z);
                for (int index = 0; index < Mathf.Min(firstWeights.Count, secondWeights.Count); index++)
                    maximumDelta = Mathf.Max(maximumDelta, Mathf.Abs(firstWeights[index] - secondWeights[index]));
                break;
            }
        }
        if (!foundDifferentDominants)
        {
            failure = "the probe region did not contain two distinct dominant biome influences";
            return false;
        }
        if (maximumDelta > 0.30f)
        {
            failure = "biome weights changed too sharply across an eight-metre sample";
            return false;
        }
        return true;
    }

    private static bool TrySampleAlphaBoundaryDelta(Terrain origin, Terrain extension, out float delta)
    {
        delta = float.PositiveInfinity;
        if (origin == null || extension == null || origin.terrainData == null || extension.terrainData == null ||
            origin.terrainData.alphamapResolution < 1 || extension.terrainData.alphamapResolution < 1)
            return false;
        int originX = origin.terrainData.alphamapResolution - 1;
        int extensionZ = Mathf.Clamp(extension.terrainData.alphamapResolution / 2, 0, extension.terrainData.alphamapResolution - 1);
        // note: Map the sampled extension row through world space so an edge comparison uses the same physical Z on both terrains.
        float worldZ = extension.transform.position.z + extension.terrainData.size.z * extensionZ / (float)Mathf.Max(1, extension.terrainData.alphamapResolution - 1);
        int originZ = Mathf.Clamp(
            Mathf.RoundToInt((worldZ - origin.transform.position.z) / Mathf.Max(1f, origin.terrainData.size.z) * Mathf.Max(1, origin.terrainData.alphamapResolution - 1)),
            0,
            origin.terrainData.alphamapResolution - 1);
        float[,,] originAlpha = origin.terrainData.GetAlphamaps(originX, originZ, 1, 1);
        float[,,] extensionAlpha = extension.terrainData.GetAlphamaps(0, extensionZ, 1, 1);
        int layerCount = Mathf.Min(originAlpha.GetLength(2), extensionAlpha.GetLength(2));
        if (layerCount == 0)
            return false;
        delta = 0f;
        for (int layer = 0; layer < layerCount; layer++)
            delta = Mathf.Max(delta, Mathf.Abs(originAlpha[0, 0, layer] - extensionAlpha[0, 0, layer]));
        return delta <= 0.08f;
    }

    private static string DescribeAlphaBoundary(Terrain origin, Terrain extension)
    {
        if (origin == null || extension == null || origin.terrainData == null || extension.terrainData == null)
            return "terrain-data-unavailable";
        int originResolution = Mathf.Max(1, origin.terrainData.alphamapResolution);
        int extensionResolution = Mathf.Max(1, extension.terrainData.alphamapResolution);
        int extensionZ = Mathf.Clamp(extensionResolution / 2, 0, extensionResolution - 1);
        float worldZ = extension.transform.position.z + extension.terrainData.size.z * extensionZ / (float)Mathf.Max(1, extensionResolution - 1);
        int originZ = Mathf.Clamp(Mathf.RoundToInt((worldZ - origin.transform.position.z) / Mathf.Max(1f, origin.terrainData.size.z) * (originResolution - 1)), 0, originResolution - 1);
        float[,,] originAlpha = origin.terrainData.GetAlphamaps(originResolution - 1, originZ, 1, 1);
        float[,,] extensionAlpha = extension.terrainData.GetAlphamaps(0, extensionZ, 1, 1);
        int layerCount = Mathf.Min(originAlpha.GetLength(2), extensionAlpha.GetLength(2));
        float originTotal = 0f;
        float extensionTotal = 0f;
        StringBuilder layers = new StringBuilder();
        for (int layer = 0; layer < layerCount; layer++)
        {
            originTotal += originAlpha[0, 0, layer];
            extensionTotal += extensionAlpha[0, 0, layer];
            if (layer > 0)
                layers.Append('|');
            layers.Append(layer.ToString(CultureInfo.InvariantCulture));
            layers.Append('=');
            layers.Append(originAlpha[0, 0, layer].ToString("0.000", CultureInfo.InvariantCulture));
            layers.Append("/");
            layers.Append(extensionAlpha[0, 0, layer].ToString("0.000", CultureInfo.InvariantCulture));
        }
        return "worldZ=" + worldZ.ToString("0.000", CultureInfo.InvariantCulture) +
            ";originTotal=" + originTotal.ToString("0.000", CultureInfo.InvariantCulture) +
            ";extensionTotal=" + extensionTotal.ToString("0.000", CultureInfo.InvariantCulture) +
            ";layers=" + layers;
    }

    private static bool TryValidateGenerationOrderIndependence(
        GeneratedWorldPlanRecord plan,
        YQContinuousWorldCellAuthority authority,
        float cellSize,
        out string failure)
    {
        failure = string.Empty;
        if (plan == null || authority == null)
        {
            failure = "world plan or continuation authority is unavailable";
            return false;
        }
        Vector2Int[] firstOrder =
        {
            // note: Exercise the first external cardinal ring, negative-origin ring, and diagonal corners around the authored 0..7 grid.
            new Vector2Int(8, 0), new Vector2Int(0, 8),
            new Vector2Int(-1, 0), new Vector2Int(0, -1),
            new Vector2Int(8, 8), new Vector2Int(-1, 8),
            new Vector2Int(8, -1), new Vector2Int(-1, -1),
            // note: Include a second-ring east cell so order independence is tested beyond the immediate seam.
            new Vector2Int(9, 0)
        };
        Vector2Int[] secondOrder =
        {
            new Vector2Int(9, 0), new Vector2Int(-1, -1),
            new Vector2Int(8, -1), new Vector2Int(-1, 8),
            new Vector2Int(8, 8), new Vector2Int(0, -1),
            new Vector2Int(-1, 0), new Vector2Int(0, 8),
            new Vector2Int(8, 0)
        };
        Dictionary<Vector2Int, string> signatures = new Dictionary<Vector2Int, string>();
        for (int index = 0; index < firstOrder.Length; index++)
        {
            GeneratedSemanticChunkRecord record = BuildOrderProbeRecord(firstOrder[index]);
            authority.PopulateCellRecord(record, firstOrder[index], cellSize, plan);
            signatures[firstOrder[index]] = BuildChunkSignature(new GeneratedWorldPlanRecord
            {
                worldSeed = plan.worldSeed,
                semanticChunks = new List<GeneratedSemanticChunkRecord> { record }
            });
        }
        for (int index = 0; index < secondOrder.Length; index++)
        {
            GeneratedSemanticChunkRecord record = BuildOrderProbeRecord(secondOrder[index]);
            authority.PopulateCellRecord(record, secondOrder[index], cellSize, plan);
            string signature = BuildChunkSignature(new GeneratedWorldPlanRecord
            {
                worldSeed = plan.worldSeed,
                semanticChunks = new List<GeneratedSemanticChunkRecord> { record }
            });
            if (!signatures.TryGetValue(secondOrder[index], out string expected) ||
                !string.Equals(expected, signature, StringComparison.Ordinal))
            {
                failure = "cell " + secondOrder[index] + " changed when generated in a different order";
                return false;
            }
        }
        return true;
    }

    private static bool TryValidateDistantProceduralSynthesis(
        GeneratedWorldPlanRecord plan,
        YQContinuousWorldCellAuthority authority,
        Terrain terrain,
        float cellSize,
        out string failure,
        out float minimumRelief,
        out float maximumRelief,
        out int biomeCount,
        out int roadCellCount,
        out int riverCellCount,
        out int landmarkCellCount)
    {
        failure = string.Empty;
        minimumRelief = float.MaxValue;
        maximumRelief = 0f;
        biomeCount = 0;
        roadCellCount = 0;
        riverCellCount = 0;
        landmarkCellCount = 0;
        if (plan == null || authority == null)
        {
            failure = "world plan or continuation authority is unavailable";
            return false;
        }

        // note: Sample several independent cells well beyond the authored 1024 metre terrain, including cardinal, diagonal, and regional-anchor coordinates.
        Vector2Int[] samples =
        {
            new Vector2Int(12, 2), new Vector2Int(16, 2), new Vector2Int(20, 2), new Vector2Int(24, 2),
            new Vector2Int(-8, 2), new Vector2Int(2, -8), new Vector2Int(12, 12), new Vector2Int(17, 12),
            new Vector2Int(22, 12), new Vector2Int(17, 17)
        };
        HashSet<string> biomes = new HashSet<string>(StringComparer.Ordinal);
        int reliefCells = 0;
        float terrainHeight = terrain != null && terrain.terrainData != null
            ? Mathf.Max(1f, terrain.terrainData.size.y)
            : YQGeneratedWorldTerrain.TerrainHeight;
        for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
        {
            Vector2Int coordinate = samples[sampleIndex];
            float cellMinimum = float.MaxValue;
            float cellMaximum = float.MinValue;
            float minX = YQContinuousWorldFeatureAuthority.WorldGridOrigin + coordinate.x * cellSize;
            float minZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin + coordinate.y * cellSize;
            for (int z = 0; z <= 4; z++)
            {
                for (int x = 0; x <= 4; x++)
                {
                    float worldX = minX + cellSize * x / 4f;
                    float worldZ = minZ + cellSize * z / 4f;
                    float height = authority.SampleHeightNormalized(worldX, worldZ) * terrainHeight;
                    cellMinimum = Mathf.Min(cellMinimum, height);
                    cellMaximum = Mathf.Max(cellMaximum, height);
                }
            }
            float relief = cellMaximum - cellMinimum;
            minimumRelief = Mathf.Min(minimumRelief, relief);
            maximumRelief = Mathf.Max(maximumRelief, relief);
            if (relief >= 1f)
                reliefCells++;
            biomes.Add(authority.ResolveDominantBiome(minX + cellSize * 0.5f, minZ + cellSize * 0.5f));

            // note: Generate a detached semantic record twice so local feature identity is proven without mutating the accepted world plan.
            GeneratedSemanticChunkRecord first = BuildOrderProbeRecord(coordinate);
            GeneratedSemanticChunkRecord second = BuildOrderProbeRecord(coordinate);
            authority.PopulateCellRecord(first, coordinate, cellSize, plan);
            authority.PopulateCellRecord(second, coordinate, cellSize, plan);
            if (!string.Equals(BuildChunkSignature(new GeneratedWorldPlanRecord
                {
                    worldSeed = plan.worldSeed,
                    semanticChunks = new List<GeneratedSemanticChunkRecord> { first }
                }), BuildChunkSignature(new GeneratedWorldPlanRecord
                {
                    worldSeed = plan.worldSeed,
                    semanticChunks = new List<GeneratedSemanticChunkRecord> { second }
                }), StringComparison.Ordinal))
            {
                failure = "far cell " + coordinate + " changed between identical local synthesis calls";
                return false;
            }
            if (YQContinuousWorldFeatureAuthority.TryGetRoadPoints(plan.worldSeed, coordinate, cellSize, terrain, out _, out _))
                roadCellCount++;
            if (YQContinuousWorldFeatureAuthority.TryGetRiverPoints(plan.worldSeed, coordinate, cellSize, terrain, out _, out _))
                riverCellCount++;
            string landmarkId = YQContinuousWorldCellAuthority.BuildLandmarkFeatureId(coordinate);
            if (first.featureIds.Contains(landmarkId) && first.sites.Exists(site => site != null && string.Equals(site.siteId, landmarkId, StringComparison.Ordinal)))
                landmarkCellCount++;
        }

        // note: Sweep a bounded far-cell lattice for corridor intersections because a fixed sample cell may legitimately be quiet wilderness.
        roadCellCount = 0;
        riverCellCount = 0;
        for (int z = -24; z <= 24; z += 4)
        {
            for (int x = -24; x <= 24; x += 4)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                if (YQContinuousWorldFeatureAuthority.TryGetRoadPoints(plan.worldSeed, coordinate, cellSize, terrain, out _, out _))
                    roadCellCount++;
                if (YQContinuousWorldFeatureAuthority.TryGetRiverPoints(plan.worldSeed, coordinate, cellSize, terrain, out _, out _))
                    riverCellCount++;
            }
        }
        biomeCount = biomes.Count;
        if (reliefCells < 2)
        {
            failure = "distant cells did not produce independent terrain relief";
            return false;
        }
        if (biomeCount < 2)
        {
            failure = "distant cells did not produce distinct regional biome identity";
            return false;
        }
        if (roadCellCount == 0 || riverCellCount == 0)
        {
            failure = "distant local structural synthesis did not produce both road and river candidates";
            return false;
        }
        // note: Accepted V2 plans may intentionally leave distant cells as empty wilderness; only the legacy fallback requires a synthetic landmark witness.
        bool acceptedSemanticPlan = plan.spatialPlanV2 != null;
        if (landmarkCellCount == 0 && !acceptedSemanticPlan)
        {
            failure = "distant local synthesis did not originate an independent landmark";
            return false;
        }
        return true;
    }

    private static bool TryValidateCanonicalChunkIdentity(GeneratedWorldPlanRecord plan, out string failure)
    {
        failure = string.Empty;
        if (plan == null || plan.semanticChunks == null)
        {
            failure = "semantic chunk plan is unavailable";
            return false;
        }
        HashSet<string> coordinates = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < plan.semanticChunks.Count; index++)
        {
            GeneratedSemanticChunkRecord record = plan.semanticChunks[index];
            if (record == null)
                continue;
            string coordinate = record.chunkX + ":" + record.chunkZ;
            if (!coordinates.Add(coordinate))
            {
                failure = "duplicate coordinate " + coordinate;
                return false;
            }
            if (string.IsNullOrWhiteSpace(record.chunkId) || !ids.Add(record.chunkId))
            {
                failure = "duplicate or missing chunk identity at " + coordinate;
                return false;
            }
        }
        return true;
    }

    private static GeneratedSemanticChunkRecord BuildOrderProbeRecord(Vector2Int coordinate)
    {
        // note: Order probes use fresh records so no previous cell mutation can make the comparison pass accidentally.
        return new GeneratedSemanticChunkRecord
        {
            chunkId = "order-probe:" + coordinate.x + ":" + coordinate.y,
            chunkX = coordinate.x,
            chunkZ = coordinate.y,
            lifecycleState = YQSemanticChunkLifecycle.SemanticallyPlanned.ToString()
        };
    }

    private static bool TryValidateAdjacentContracts(
        GeneratedWorldPlanRecord plan,
        Vector2Int centre,
        int radius,
        out string failure,
        out float maximumHeightDelta)
    {
        failure = string.Empty;
        maximumHeightDelta = 0f;
        if (plan == null || plan.semanticChunks == null)
        {
            failure = "semantic chunk plan is unavailable";
            return false;
        }
        for (int z = -radius; z <= radius; z++)
        for (int x = -radius; x <= radius; x++)
        {
            Vector2Int current = new Vector2Int(centre.x + x, centre.y + z);
            if (!TryFindChunkRecord(plan, current, out GeneratedSemanticChunkRecord currentRecord))
            {
                failure = "missing semantic record " + current;
                return false;
            }
            if (x < radius && TryFindChunkRecord(plan, new Vector2Int(current.x + 1, current.y), out GeneratedSemanticChunkRecord eastRecord))
            {
                if (!CompareAdjacentEdges(currentRecord, "east", eastRecord, "west", ref maximumHeightDelta, out failure))
                    return false;
            }
            if (z < radius && TryFindChunkRecord(plan, new Vector2Int(current.x, current.y + 1), out GeneratedSemanticChunkRecord northRecord))
            {
                if (!CompareAdjacentEdges(currentRecord, "north", northRecord, "south", ref maximumHeightDelta, out failure))
                    return false;
            }
        }
        return true;
    }

    private static bool TryFindChunkRecord(GeneratedWorldPlanRecord plan, Vector2Int coordinate, out GeneratedSemanticChunkRecord record)
    {
        if (plan?.semanticChunks != null)
            for (int index = 0; index < plan.semanticChunks.Count; index++)
            {
                GeneratedSemanticChunkRecord candidate = plan.semanticChunks[index];
                if (candidate != null && candidate.chunkX == coordinate.x && candidate.chunkZ == coordinate.y)
                {
                    record = candidate;
                    return true;
                }
            }
        record = null;
        return false;
    }

    private static bool CompareAdjacentEdges(
        GeneratedSemanticChunkRecord firstRecord,
        string firstEdgeName,
        GeneratedSemanticChunkRecord secondRecord,
        string secondEdgeName,
        ref float maximumHeightDelta,
        out string failure)
    {
        failure = string.Empty;
        GeneratedSemanticChunkEdgeContractRecord first = FindEdge(firstRecord, firstEdgeName);
        GeneratedSemanticChunkEdgeContractRecord second = FindEdge(secondRecord, secondEdgeName);
        if (first == null || second == null)
        {
            failure = "missing shared edge contract between " + firstRecord.chunkId + " and " + secondRecord.chunkId;
            return false;
        }
        if (!string.Equals(first.canonicalKey, second.canonicalKey, StringComparison.Ordinal) ||
            !string.Equals(first.contractVersion, second.contractVersion, StringComparison.Ordinal) ||
            first.sampleCount != second.sampleCount)
        {
            failure = "shared edge identity mismatch between " + firstRecord.chunkId + " and " + secondRecord.chunkId;
            return false;
        }
        if (!CompareFloatLists(first.terrainHeights, second.terrainHeights, 0.0001f, ref maximumHeightDelta) ||
            !CompareFloatLists(first.biomeWeights, second.biomeWeights, 0.0001f, ref maximumHeightDelta))
        {
            failure = "shared edge samples diverged between " + firstRecord.chunkId + " and " + secondRecord.chunkId;
            return false;
        }
        if (!ComparePortals(first.routePortals, second.routePortals, firstEdgeName, secondEdgeName) ||
            !ComparePortals(first.waterPortals, second.waterPortals, firstEdgeName, secondEdgeName))
        {
            failure = "shared edge portals diverged between " + firstRecord.chunkId + " and " + secondRecord.chunkId;
            return false;
        }
        return true;
    }

    private static GeneratedSemanticChunkEdgeContractRecord FindEdge(GeneratedSemanticChunkRecord record, string edgeName)
    {
        if (record?.edgeContracts == null)
            return null;
        for (int index = 0; index < record.edgeContracts.Count; index++)
        {
            GeneratedSemanticChunkEdgeContractRecord edge = record.edgeContracts[index];
            if (edge != null && string.Equals(edge.edge, edgeName, StringComparison.OrdinalIgnoreCase))
                return edge;
        }
        return null;
    }

    private static bool CompareFloatLists(List<float> first, List<float> second, float tolerance, ref float maximumDelta)
    {
        if (first == null || second == null || first.Count != second.Count)
            return false;
        for (int index = 0; index < first.Count; index++)
        {
            if (float.IsNaN(first[index]) || float.IsInfinity(first[index]) ||
                float.IsNaN(second[index]) || float.IsInfinity(second[index]))
                return false;
            float delta = Mathf.Abs(first[index] - second[index]);
            maximumDelta = Mathf.Max(maximumDelta, delta);
            if (delta > tolerance)
                return false;
        }
        return true;
    }

    private static bool ComparePortals(
        List<GeneratedSemanticChunkPortalRecord> first,
        List<GeneratedSemanticChunkPortalRecord> second,
        string firstEdgeName,
        string secondEdgeName)
    {
        if (first == null || second == null || first.Count != second.Count)
        {
            // note: Keep the shared-border failure actionable by recording portal counts from both sides.
            Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Portal count mismatch " + firstEdgeName + "/" + secondEdgeName + " left=" + (first != null ? first.Count : -1) + " right=" + (second != null ? second.Count : -1));
            // note: Include the first portal payload so a one-sided boundary crossing can be repaired at the shared feature resolver.
            if (first != null && first.Count > 0 && first[0] != null)
                Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Left portal detail id=" + first[0].portalId + " feature=" + first[0].featureId + " edgeT=" + first[0].edgeT + " pos=" + first[0].worldX + "," + first[0].worldZ + " order=" + first[0].order);
            if (second != null && second.Count > 0 && second[0] != null)
                Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Right portal detail id=" + second[0].portalId + " feature=" + second[0].featureId + " edgeT=" + second[0].edgeT + " pos=" + second[0].worldX + "," + second[0].worldZ + " order=" + second[0].order);
            return false;
        }
        for (int index = 0; index < first.Count; index++)
        {
            GeneratedSemanticChunkPortalRecord left = first[index];
            GeneratedSemanticChunkPortalRecord right = second[index];
            if (left == null || right == null)
            {
                if (left != right)
                {
                    Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Portal mismatch index=" + index + " left=" + (left != null ? left.portalId : "<null>") + " right=" + (right != null ? right.portalId : "<null>"));
                    return false;
                }
                continue;
            }
            if (!string.Equals(left.portalId, right.portalId, StringComparison.Ordinal) ||
                !string.Equals(left.featureId, right.featureId, StringComparison.Ordinal) ||
                !string.Equals(left.kind, right.kind, StringComparison.Ordinal) ||
                !string.Equals(left.edge, firstEdgeName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(right.edge, secondEdgeName, StringComparison.OrdinalIgnoreCase) ||
                !PortalFloatsFinite(left) || !PortalFloatsFinite(right) ||
                Mathf.Abs(left.edgeT - right.edgeT) > 0.0001f ||
                Mathf.Abs(left.worldX - right.worldX) > 0.0001f ||
                Mathf.Abs(left.worldZ - right.worldZ) > 0.0001f ||
                Mathf.Abs(left.tangentX - right.tangentX) > 0.0001f ||
                Mathf.Abs(left.tangentZ - right.tangentZ) > 0.0001f ||
                Mathf.Abs(left.width - right.width) > 0.0001f ||
                Mathf.Abs(left.depth - right.depth) > 0.0001f ||
                Mathf.Abs(left.surfaceElevation - right.surfaceElevation) > 0.0001f ||
                Mathf.Abs(left.bedElevation - right.bedElevation) > 0.0001f ||
                Mathf.Abs(left.flowX - right.flowX) > 0.0001f ||
                Mathf.Abs(left.flowZ - right.flowZ) > 0.0001f ||
                left.order != right.order ||
                !string.Equals(left.upstreamId, right.upstreamId, StringComparison.Ordinal) ||
                !string.Equals(left.downstreamId, right.downstreamId, StringComparison.Ordinal))
            {
                Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Portal mismatch index=" + index + " left=" + left.portalId + " right=" + right.portalId + " leftPos=" + left.worldX + "," + left.worldZ + " rightPos=" + right.worldX + "," + right.worldZ);
                return false;
            }
        }
        return true;
    }

    private static bool PortalFloatsFinite(GeneratedSemanticChunkPortalRecord portal)
    {
        return portal != null &&
            !float.IsNaN(portal.edgeT) && !float.IsInfinity(portal.edgeT) &&
            !float.IsNaN(portal.worldX) && !float.IsInfinity(portal.worldX) &&
            !float.IsNaN(portal.worldZ) && !float.IsInfinity(portal.worldZ) &&
            !float.IsNaN(portal.tangentX) && !float.IsInfinity(portal.tangentX) &&
            !float.IsNaN(portal.tangentZ) && !float.IsInfinity(portal.tangentZ) &&
            !float.IsNaN(portal.width) && !float.IsInfinity(portal.width) &&
            !float.IsNaN(portal.depth) && !float.IsInfinity(portal.depth) &&
            !float.IsNaN(portal.surfaceElevation) && !float.IsInfinity(portal.surfaceElevation) &&
            !float.IsNaN(portal.bedElevation) && !float.IsInfinity(portal.bedElevation) &&
            !float.IsNaN(portal.flowX) && !float.IsInfinity(portal.flowX) &&
            !float.IsNaN(portal.flowZ) && !float.IsInfinity(portal.flowZ);
    }

    private static void AppendStringList(StringBuilder signature, System.Collections.Generic.List<string> values)
    {
        signature.Append('[');
        if (values != null)
            for (int index = 0; index < values.Count; index++)
                signature.Append(values[index] ?? string.Empty).Append('\u001f');
        signature.Append(']');
    }

    private static void AppendFloatList(StringBuilder signature, System.Collections.Generic.List<float> values)
    {
        signature.Append('[');
        if (values != null)
            for (int index = 0; index < values.Count; index++)
                signature.Append(values[index].ToString("R", CultureInfo.InvariantCulture)).Append('\u001f');
        signature.Append(']');
    }

    private static void AppendPortalList(StringBuilder signature, System.Collections.Generic.List<GeneratedSemanticChunkPortalRecord> portals)
    {
        signature.Append('[');
        if (portals != null)
            for (int index = 0; index < portals.Count; index++)
            {
                GeneratedSemanticChunkPortalRecord portal = portals[index];
                if (portal == null)
                {
                    signature.Append("<null-portal>");
                }
                else
                {
                    signature.Append(portal.portalId).Append('|').Append(portal.featureId).Append('|')
                        .Append(portal.kind).Append('|').Append(portal.edge).Append('|')
                        .Append(portal.edgeT.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.worldX.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.worldZ.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.tangentX.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.tangentZ.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.width.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.depth.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.surfaceElevation.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.bedElevation.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.flowX.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.flowZ.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.order).Append('|').Append(portal.upstreamId).Append('|').Append(portal.downstreamId);
                }
                signature.Append('\u001e');
            }
        signature.Append(']');
    }

    private static string CurrentReportFileName()
    {
        return _focusedSpeed260MotorWitnessOnly
            ? "G08_R2_Speed260MotorWitness.md"
            : "YQSemanticChunkRuntimeVerification.md";
    }

    private static void AppendNestedFailureToReport(string failureText)
    {
        // note: Preserve a yielded gate exception and publish a complete terminal failure when the frame driver had to dispose the root iterator.
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(failureText))
            return;
        string reportFileName = CurrentReportFileName();
        string reportPath = Path.Combine(projectRoot, "Logs", reportFileName);
        if (!File.Exists(reportPath))
            return;
        string normalizedFailure = failureText.Replace('\r', ' ').Replace('\n', ' ');
        // note: A root iterator exception may already have published FAIL from its finally; append its missing reason without duplicating terminal fields.
        string terminalFields = _terminalReportWritten ? string.Empty :
            "- result: FAIL" + Environment.NewLine +
            "- finishedUtc: " + DateTime.UtcNow.ToString("O") + Environment.NewLine;
        File.AppendAllText(reportPath, "- failure: " + normalizedFailure + Environment.NewLine + terminalFields);
        _terminalReportWritten = true;
        // note: Request Edit Mode only after the failure evidence is durable, matching the normal terminal report contract.
        File.WriteAllText(Path.Combine(projectRoot, "Temp", "YQ_STREAMER_RUNTIME_STOP.request"), "fail");
        Debug.LogError("[YQSemanticChunkRuntimeVerification] FAIL report written to Logs/" + reportFileName + ": " + normalizedFailure);
    }

    private static void AppendVerifierStatusToReport(string status)
    {
        // note: Keep verifier liveness evidence in the same report as traversal results so a sentinel-only run identifies whether the host survived its first frame.
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(status))
            return;
        string reportPath = Path.Combine(projectRoot, "Logs", CurrentReportFileName());
        if (File.Exists(reportPath))
            File.AppendAllText(reportPath, "- verifierStatus: " + status + Environment.NewLine);
    }

    private static void AppendExecutionIdentity(StringBuilder report)
    {
        // note: Record the loaded verifier assembly separately from source files so a stale Unity compile cannot be mistaken for current disk source.
        Assembly assembly = typeof(YQSemanticChunkRuntimeVerification).Assembly;
        string assemblyPath = string.Empty;
        try
        {
            assemblyPath = assembly.Location;
        }
        catch (NotSupportedException)
        {
            // note: Some player backends do not expose a physical assembly path; the module identity remains useful.
        }
        report.AppendLine("- executingAssembly: name=" + ReportValue(assembly.GetName().Name) +
            " mvid=" + assembly.ManifestModule.ModuleVersionId.ToString("D") +
            " path=" + ReportValue(assemblyPath));
        AppendFileSha256(report, "executingAssemblySha256", assemblyPath);

        string generatedSourceDirectory = Path.Combine(Application.dataPath, "Assets", "Scripts", "Generated");
        AppendFileSha256(report, "verifierSourceSha256",
            Path.Combine(generatedSourceDirectory, "YQSemanticChunkRuntimeVerification.cs"));
        AppendFileSha256(report, "streamerSourceSha256",
            Path.Combine(generatedSourceDirectory, "YQPlayerFollowingSemanticChunkStreamer.cs"));
    }

    private static void AppendWorldIdentity(StringBuilder report, WorldState world, YQProfileSaveSystem profileSave)
    {
        // note: Bind later runtime measurements to the live paired profile/world save and report router selection without mutating either authority.
        YQWorldIdentityRecord identity = world != null ? world.worldIdentity : null;
        GeneratedWorldPlanRecord plan = world != null ? world.generatedWorldPlan : null;
        string activeProfileId = profileSave != null ? profileSave.ActiveProfileId : string.Empty;
        string ownerProfileId = identity != null ? identity.ownerProfileId : string.Empty;
        bool ownerMatchesActiveProfile = !string.IsNullOrWhiteSpace(activeProfileId) &&
            string.Equals(ownerProfileId, activeProfileId, StringComparison.OrdinalIgnoreCase);
        report.AppendLine("- profileWorldIdentity: activeProfileId=" + ReportValue(activeProfileId) +
            " ownerProfileId=" + ReportValue(ownerProfileId) +
            " ownerMatchesActiveProfile=" + ownerMatchesActiveProfile);
        report.AppendLine("- worldPlanIdentity: stateSchema=" + (world != null ? world.schemaVersion.ToString(CultureInfo.InvariantCulture) : "<missing>") +
            " planSchema=" + ReportValue(plan != null ? plan.schemaVersion : string.Empty) +
            " worldId=" + ReportValue(identity != null ? identity.worldId : string.Empty) +
            " worldSeed=" + ReportValue(plan != null ? plan.worldSeed : string.Empty));

        YQSpatialPlanAuthority authority;
        string routeReason;
        bool routeResolved = YQSpatialPlanVersionRouter.TryResolveActive(plan, out authority, out routeReason);
        report.AppendLine("- activeSpatialPlanResolution: resolved=" + routeResolved +
            " authority=" + (routeResolved ? authority.ToString() : "None") +
            " reason=" + ReportValue(routeReason));
        report.AppendLine("- persistedSpatialArtifactIdentity: id=" + ReportValue(identity != null ? identity.selectedSpatialArtifactId : string.Empty) +
            " version=" + ReportValue(identity != null ? identity.selectedSpatialArtifactVersion : string.Empty));

        GeneratedSpatialWorldPlanRecord v1 = plan != null ? plan.spatialPlan : null;
        GeneratedSpatialWorldPlanV2Record v2 = plan != null ? plan.spatialPlanV2 : null;
        report.AppendLine("- persistedV1Artifact: schema=" + ReportValue(v1 != null ? v1.schemaVersion : string.Empty) +
            " generation=" + ReportValue(v1 != null ? v1.generationVersion : string.Empty) +
            " seed=" + ReportValue(v1 != null ? v1.worldSeed : string.Empty) +
            " semanticFingerprint=" + ReportValue(v1 != null ? v1.semanticFingerprint : string.Empty));
        report.AppendLine("- acceptedV2Artifact: state=" + (v2 != null ? v2.acceptanceState.ToString() : "<missing>") +
            " schema=" + ReportValue(v2 != null ? v2.schemaVersion : string.Empty) +
            " generation=" + ReportValue(v2 != null ? v2.generationVersion : string.Empty) +
            " validation=" + ReportValue(v2 != null ? v2.validationVersion : string.Empty) +
            " seed=" + ReportValue(v2 != null ? v2.worldSeed : string.Empty) +
            " contentHash=" + ReportValue(v2 != null ? v2.contentHash : string.Empty) +
            " validatedContentHash=" + ReportValue(v2 != null ? v2.validatedContentHash : string.Empty));
    }

    private static void AppendFileSha256(StringBuilder report, string fieldName, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            report.AppendLine("- " + fieldName + ": unavailable");
            return;
        }

        try
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
                report.AppendLine("- " + fieldName + ": path=" + ReportValue(path) + " sha256=" + hash);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                           exception is CryptographicException || exception is ArgumentException ||
                                           exception is NotSupportedException || exception is System.Security.SecurityException)
        {
            // note: Provenance capture is diagnostic-only and must never change the runtime verification verdict.
            report.AppendLine("- " + fieldName + ": unavailable reason=" + exception.GetType().Name);
        }
    }

    private static string ReportValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";
        return value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
    }

    private static void WriteReport(StringBuilder report, bool success, bool terminal = true, bool inconclusive = false)
    {
        if (_terminalReportWritten)
            return;
        if (terminal)
            report.AppendLine("- finishedUtc: " + DateTime.UtcNow.ToString("O"));
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            return;
        string logsDirectory = Path.Combine(projectRoot, "Logs");
        Directory.CreateDirectory(logsDirectory);
        string reportFileName = CurrentReportFileName();
        string reportPath = Path.Combine(logsDirectory, reportFileName);
        if (!ReferenceEquals(_activeReport, report))
        {
            // note: A newly armed run starts a new receipt; subsequent phase flushes append only their new evidence.
            File.WriteAllText(reportPath, report.ToString());
            _activeReport = report;
        }
        else if (report.Length > _writtenReportCharacters)
        {
            File.AppendAllText(reportPath, report.ToString(_writtenReportCharacters, report.Length - _writtenReportCharacters));
        }
        _writtenReportCharacters = report.Length;
        if (terminal)
        {
            _terminalReportWritten = true;
            // note: Keep the report, editor stop receipt, and Console aligned; completing the matrix with missing input evidence is inconclusive.
            string outcome = !success ? "FAIL" : inconclusive ? "INCONCLUSIVE" : "PASS";
            File.WriteAllText(Path.Combine(projectRoot, "Temp", "YQ_STREAMER_RUNTIME_STOP.request"), outcome.ToLowerInvariant());
            Debug.Log("[YQSemanticChunkRuntimeVerification] " + outcome + " report written to Logs/" + reportFileName);
        }
    }
}
