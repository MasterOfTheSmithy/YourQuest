#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// note: Keep the editor's global auto-refresh preference enabled so external script edits reach Unity without a manual Ctrl+R.
[InitializeOnLoad]
internal static class YQEditorAutoRefreshBootstrap
{
    private static FileSystemWatcher _scriptWatcher;
    private static volatile bool _refreshQueued;
    private const string PlayRefreshSuspendedKey = "YourQuest.EditorAutoRefresh.PlaySuspended";
    private const string PaletteProbeMarker = "Temp/YQ_PALETTE_RUNTIME_PROBE.request";
    private const string GenerationReadySweepMarker = "Temp/YQ_GENERATION_READY_SWEEP.request";
    private const string GenerationReadyPrefabAuditMarker = "Temp/YQ_GENERATION_READY_PREFAB_AUDIT.request";
    // note: A distinct start request is the only path allowed to enter Play Mode for unattended verification.
    private const string StreamerRuntimeStartMarker = "Temp/YQ_STREAMER_RUNTIME_START.request";
    private const string StreamerRuntimeTestMarker = "Temp/YQ_STREAMER_RUNTIME_TEST.request";
    private const string OrdinaryContinueMarker = "Temp/YQ_STARTUP_CONTINUE.request";
    // note: This marker selects the fixed beta profile through the real title flow for unattended baseline verification.
    private const string BetaFixtureStartMarker = "Temp/YQ_BETA_FIXTURE_START.request";
    // note: This marker performs the fixture's documented clean reset through the profile owner before the title flow reloads it.
    private const string BetaFixtureResetMarker = "Temp/YQ_BETA_FIXTURE_RESET.request";
    // note: This marker invokes the existing baseline menu method after PlaySafe is ready, keeping unattended capture on the old test path.
    private const string BetaBaselineRegressionMarker = "Temp/YQ_BETA_BASELINE_REGRESSION.request";
    // note: This marker invokes the existing profile menu's save operation so reload evidence is tied to a published revision.
    private const string BetaFixtureSaveMarker = "Temp/YQ_BETA_FIXTURE_SAVE.request";
    // note: This marker drives the existing title/origin owners for the second-profile isolation acceptance check.
    private const string BetaProfileIsolationMarker = "Temp/YQ_BETA_PROFILE_ISOLATION.request";
    // note: This marker selects persisted profile B before the user stops Play Mode for the reload acceptance check.
    private const string BetaProfileReloadPrepareMarker = "Temp/YQ_BETA_PROFILE_RELOAD_PREPARE.request";
    // note: This marker records the post-restart state without bypassing the normal profile bootstrap.
    private const string BetaProfileReloadVerifyMarker = "Temp/YQ_BETA_PROFILE_RELOAD_VERIFY.request";
    // note: This marker runs the scheduler/proposal pure contract receipt in the already-open editor without entering Play Mode.
    private const string LlmProposalPureRegressionMarker = "Temp/YQ_LLM_PROPOSAL_PURE_REGRESSION.request";
    // note: This marker runs the deterministic world-generation contract suite only after PlaySafe has returned to Edit Mode.
    private const string WorldGenerationV2ContractMarker = "Temp/YQ_WORLD_GENERATION_V2_CONTRACT.request";
    private const string BlueprintHashVerificationMarker = "Temp/YQ_BLUEPRINT_HASH_VERIFY.request";
    private const string RibbonWidthVerificationMarker = "Temp/YQ_RIBBON_WIDTH_VERIFY.request";
    private const string StreamingCpuCaptureMarker = "Temp/YQ_STREAMING_CPU_CAPTURE.request";
    private const string StreamedWaterCaptureMarker = "Temp/YQ_STREAMED_WATER_CAPTURE.request";
    // note: This marker runs the bounded four-seed absolute-coordinate terrain determinism receipt in Edit Mode.
    private const string ContinuousTerrainDeterminismMarker = "Assets/Assets/EditorBuildRequests/RunContinuousTerrainDeterminismTests.request";
    // note: This marker runs the disposable, non-mutating scheduler fixture only after the real PlaySafe title flow is complete.
    private const string LlmSchedulerRuntimeVerificationMarker = "Temp/YQ_LLM_SCHEDULER_RUNTIME.request";
    // note: This marker captures one observed PlaySafe frame after the accepted runtime presentation boundary.
    private const string BetaAssetBindingScreenshotMarker = "Temp/YQ_BETA_ASSET_BINDING_SCREENSHOT.request";
    // note: This marker republishes the beta manifest from the reviewed catalogs after editor compilation completes.
    private const string BetaAssetBindingManifestBuildMarker = "Temp/YQ_BETA_ASSET_BINDING_MANIFEST_BUILD.request";
    // note: This marker runs the focused beta binding certification against the published manifest and runtime registry.
    private const string BetaAssetBindingCertificationMarker = "Temp/YQ_BETA_ASSET_BINDING_CERTIFICATION.request";
    // note: The runtime verifier writes this marker after its terminal report so unattended acceptance always leaves Unity in Edit Mode.
    private const string StreamerRuntimeStopMarker = "Temp/YQ_STREAMER_RUNTIME_STOP.request";
    // note: Publish the editor's actual Play Mode state so external verification never infers it from stale reports or process presence.
    private const string StreamerRuntimePlayModeStatus = "Temp/YQ_STREAMER_PLAYMODE.status";
    private static double _nextPlayModeStatusWrite;
    private static string _lastStartupPresentationEvidence = string.Empty;
    private static string _lastStartupPresentationStateEvidence = string.Empty;
    private static int _lastActiveAudioListeners;

    static YQEditorAutoRefreshBootstrap()
    {
        // note: Unity 6 uses kAutoRefreshMode, where zero disables automatic asset refresh and script compilation.
        if (EditorPrefs.GetInt("kAutoRefreshMode", 1) == 0)
        {
            EditorPrefs.SetInt("kAutoRefreshMode", 1);
            Debug.Log("[YQEditorAutoRefresh] Automatic asset and script refresh enabled.");
        }

        // note: File watching queues external C# edits even during Play; the editor imports them only after Play has stopped.
        string assetsPath = Path.GetFullPath("Assets");
        if (Directory.Exists(assetsPath))
        {
            _scriptWatcher = new FileSystemWatcher(assetsPath, "*.cs")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _scriptWatcher.Changed += QueueRefresh;
            _scriptWatcher.Created += QueueRefresh;
            _scriptWatcher.Renamed += QueueRefresh;
            _scriptWatcher.Deleted += QueueRefresh;
        }

        // note: The editor update performs Unity API calls on the main thread after the watcher signals an external source change.
        EditorApplication.update -= ProcessQueuedRefresh;
        EditorApplication.update += ProcessQueuedRefresh;
        // note: A manual Play Mode exit cancels a still-pending verifier request so the editor never starts Play Mode again by itself.
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        SetPlayRefreshSuspended(EditorApplication.isPlayingOrWillChangePlaymode);
        WritePlayModeStatus("Initialized");
    }

    [MenuItem("YourQuest/Verification/Run Terrain Acceptance in Current Play Session")]
    private static void RunTerrainAcceptanceInCurrentPlaySession()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[YQEditorAutoRefresh] Enter Play Mode and complete normal startup before running terrain acceptance.");
            return;
        }
        // note: Start only from an explicit menu action in an already-running Play session; never change Play Mode state here.
        if (!YQSemanticChunkRuntimeVerification.TryBeginFromCurrentPlaySession())
            Debug.Log("[YQEditorAutoRefresh] Terrain acceptance is already running or could not start in the current Play session.");
    }

    [MenuItem("YourQuest/Verification/Run Focused 260 Speed Boundary Witness in Current Play Session")]
    private static void RunFocusedSpeed260WitnessInCurrentPlaySession()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[YQEditorAutoRefresh] Enter Play Mode and complete normal startup before running the focused speed witness.");
            return;
        }
        // note: Keep the first 260 m/s runtime check bounded and separate from the full G08/R1 acceptance suite.
        if (!YQSemanticChunkRuntimeVerification.TryBeginFocusedSpeed260WitnessFromCurrentPlaySession())
            Debug.Log("[YQEditorAutoRefresh] A terrain verifier is already running or the focused speed witness could not start.");
    }

    [MenuItem("YourQuest/Verification/Continue Selected Journey in Current Play Session")]
    private static void ContinueSelectedJourneyInCurrentPlaySession()
    {
        // note: This named verification action invokes the ordinary title controller; it never starts Play Mode or substitutes a canonical save.
        if (EditorApplication.isPlaying && YQTitleScreenUI.Instance != null)
            YQTitleScreenUI.Instance.ContinueSelectedForDevelopmentVerification();
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        // note: Balance the native auto-refresh hold across Play's domain reload; external imports resume only after returning to Edit Mode.
        if (state == PlayModeStateChange.ExitingEditMode)
            SetPlayRefreshSuspended(true);
        else if (state == PlayModeStateChange.EnteredEditMode)
            SetPlayRefreshSuspended(false);
        // note: Record every Unity transition before handling request cleanup so automation can observe the real editor instance.
        WritePlayModeStatus(state.ToString());
        if (state != PlayModeStateChange.ExitingPlayMode)
            return;
        // note: A manual stop cancels both phases so no stale request can replay after the user leaves Play Mode.
        DeleteMarker(StreamerRuntimeStartMarker);
        DeleteMarker(StreamerRuntimeTestMarker);
        DeleteMarker(OrdinaryContinueMarker);
    }

    private static void SetPlayRefreshSuspended(bool suspended)
    {
        // note: SessionState survives domain reloads so each native Disallow call has exactly one matching Allow call.
        if (SessionState.GetBool(PlayRefreshSuspendedKey, false) == suspended)
            return;
        if (suspended)
            AssetDatabase.DisallowAutoRefresh();
        else
            AssetDatabase.AllowAutoRefresh();
        SessionState.SetBool(PlayRefreshSuspendedKey, suspended);
    }

    private static void DeleteMarker(string marker)
    {
        if (!File.Exists(marker))
            return;
        try
        {
            // note: Consume a request safely when Unity has released any transient file handle.
            File.Delete(marker);
        }
        catch (IOException)
        {
            // note: Defer a locked marker to the next editor transition instead of disrupting Unity.
        }
    }

    private static void WritePlayModeStatus(string transition)
    {
        try
        {
            // note: Keep the status payload small, timestamped, and atomically replaceable by the editor main thread.
            string path = Path.GetFullPath(StreamerRuntimePlayModeStatus);
            YQGeneratedWorldRuntimeBuilder builder = YQGeneratedWorldRuntimeBuilder.Instance;
            YQProfileSaveSystem profileSystem = YQProfileSaveSystem.Instance;
            bool profileTerrainSnapshotLoaded = profileSystem != null &&
                profileSystem.TryGetLoadedAuxiliaryDocument(
                    YQGeneratedWorldTerrain.ProfileTerrainSnapshotDocumentId, out _);
            WorldState world = WorldStateManager.Instance != null
                ? WorldStateManager.Instance.State
                : null;
            GeneratedWorldPlanRecord plan = world != null
                ? world.generatedWorldPlan
                : null;
            // note: Observe manual New Journey/Continue without driving input, changing profiles, or altering startup readiness.
            YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;
            YQPlayerFollowingSemanticChunkStreamer streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
            YourQuestTutorialHud hud = EditorApplication.isPlaying
                // note: Qualify Unity's object search because System.Object is also imported in this bootstrap.
                ? UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialHud>() : null;
            string presentationStateEvidence =
                "released=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased +
                ";loading=" + YQStartupLoadingScreen.IsVisible +
                ";hud=" + (hud != null && hud.IsPresentationVisible) +
                ";input=" + (motor != null && motor.CanProcessMovementInput) +
                ";camera=" + (motor != null && motor.playerCamera != null && motor.playerCamera.isActiveAndEnabled);
            if (EditorApplication.isPlaying &&
                !string.Equals(presentationStateEvidence, _lastStartupPresentationStateEvidence, StringComparison.Ordinal))
            {
                // note: Count scene listeners only when the presentation contract changes; an allocating full-scene search every heartbeat distorts streamed-play performance measurements.
                int observedActiveAudioListeners = 0;
                foreach (AudioListener listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (listener.isActiveAndEnabled)
                        observedActiveAudioListeners++;
                _lastActiveAudioListeners = observedActiveAudioListeners;
                _lastStartupPresentationStateEvidence = presentationStateEvidence;
            }
            int activeAudioListeners = EditorApplication.isPlaying ? _lastActiveAudioListeners : 0;
            string presentationEvidence = presentationStateEvidence + ";listeners=" + activeAudioListeners;
            if (EditorApplication.isPlaying && presentationEvidence != _lastStartupPresentationEvidence)
            {
                // note: Retain only state transitions in the Console so a later manual stop preserves the observed handoff evidence without per-frame logging.
                Debug.Log("[YQStartupPresentation] " + presentationEvidence + " utc=" + System.DateTime.UtcNow.ToString("O"));
                _lastStartupPresentationEvidence = presentationEvidence;
            }
            // note: Publish the authoritative build predicates that explain a startup wait without exposing private coroutine state or guessing from process presence.
            string payload = "transition=" + transition + "\n" +
                "playing=" + EditorApplication.isPlaying + "\n" +
                "playingOrWillChange=" + EditorApplication.isPlayingOrWillChangePlaymode + "\n" +
                "paused=" + EditorApplication.isPaused + "\n" +
                "compiling=" + EditorApplication.isCompiling + "\n" +
                "updating=" + EditorApplication.isUpdating + "\n" +
                "refreshQueued=" + _refreshQueued + "\n" +
                "playRefreshSuspended=" + SessionState.GetBool(PlayRefreshSuspendedKey, false) + "\n" +
                "titleGateActive=" + YQTitleScreenUI.StartupGateActive + "\n" +
                "titleFlowComplete=" + YQTitleScreenUI.StartupFlowComplete + "\n" +
                "gameplayRuntimeReady=" + YourQuestTutorialAutoBootstrap.GameplayRuntimeReady + "\n" +
                "gameplayPresentationReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased + "\n" +
                "gameplayPresentationWaitReason=" + YourQuestTutorialAutoBootstrap.GameplayPresentationWaitReason + "\n" +
                "gameplayHudVisible=" + (hud != null && hud.IsPresentationVisible) + "\n" +
                "gameplayInputReady=" + (motor != null && motor.CanProcessMovementInput) + "\n" +
                "gameplayCameraActive=" + (motor != null && motor.playerCamera != null && motor.playerCamera.isActiveAndEnabled) + "\n" +
                "activeAudioListeners=" + activeAudioListeners + "\n" +
                "maximumContentSliceSeconds=" + (streamer != null ? streamer.MaximumContentSliceSeconds.ToString("0.000") : "0") + "\n" +
                "maximumContentSliceStage=" + (streamer != null ? streamer.MaximumContentSliceStage : string.Empty) + "\n" +
                "maximumMaterializationSubstageSeconds=" + (streamer != null ? streamer.MaximumMaterializationSubstageSeconds.ToString("0.000") : "0") + "\n" +
                "maximumMaterializationSubstage=" + (streamer != null ? streamer.MaximumMaterializationSubstage : string.Empty) + "\n" +
                "maximumAggregateFrameWorkSeconds=" + (streamer != null ? streamer.MaximumAggregateFrameWorkSeconds.ToString("0.000") : "0") + "\n" +
                "maximumAggregateFrameWorkStage=" + (streamer != null ? streamer.MaximumAggregateFrameWorkStage : string.Empty) + "\n" +
                "maximumPublicationSliceSeconds=" + (streamer != null ? streamer.MaximumPublicationSliceSeconds.ToString("0.000") : "0") + "\n" +
                "maximumPublicationSliceStage=" + (streamer != null ? streamer.MaximumPublicationSliceStage : string.Empty) + "\n" +
                "asyncInstantiateIntegrationBudgetMs=" + UnityEngine.AsyncInstantiateOperation.GetIntegrationTimeMS().ToString("0.###") + "\n" +
                // note: Include the bounded derived-route cache counters so captured startup and travel runs prove reuse under the real streamer.
                "semanticPathProjectionCacheHits=" + YQGeneratedWorldEnvironment.SemanticPathProjectionCacheHits.ToString() + "\n" +
                "semanticPathProjectionCacheMisses=" + YQGeneratedWorldEnvironment.SemanticPathProjectionCacheMisses.ToString() + "\n" +
                "semanticPathProjectionWorldBuilds=" + YQGeneratedWorldEnvironment.SemanticPathProjectionWorldBuilds.ToString() + "\n" +
                "aggregateBudgetDeferredSlices=" + (streamer != null ? streamer.AggregateBudgetDeferredSlices.ToString() : "0") + "\n" +
                "physicalDemandCount=" + (streamer != null ? streamer.PhysicalDemandCount.ToString() : "0") + "\n" +
                "physicalDemandOverflowCount=" + (streamer != null ? streamer.PhysicalDemandOverflowCount.ToString() : "0") + "\n" +
                // note: Expose the authoritative player predicate alongside the builder gate so a verifier wait distinguishes missing player ownership from missing world materialization.
                "activeMotorPresent=" + (YQInvestorPlayerMotor.ActiveMotor != null) + "\n" +
                "activeMotorAuthoritative=" + (YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.IsAuthoritative) + "\n" +
                "startupLoadingVisible=" + YQStartupLoadingScreen.IsVisible + "\n" +
                "startupGenerationVisible=" + YQStartupLoadingScreen.IsGenerationVisible + "\n" +
                "generationPhase=" + YQStartupLoadingScreen.CurrentGenerationPhase + "\n" +
                "generationSubstep=" + YQStartupLoadingScreen.CurrentGenerationSubstep + "\n" +
                "generationProgress=" + YQStartupLoadingScreen.CurrentGenerationProgress.ToString("0.000") + "\n" +
                "builderPresent=" + (builder != null) + "\n" +
                "builderInProgress=" + (builder != null && builder.IsMaterializationInProgress) + "\n" +
                "builderMaterialized=" + (builder != null && builder.HasMaterializedCurrentWorld) + "\n" +
                "builderFailed=" + (builder != null && builder.HasMaterializationFailed) + "\n" +
                "builderDecision=" + (builder != null ? builder.LastMaterializationDecision : string.Empty) + "\n" +
                "profileTerrainSnapshotLoaded=" + profileTerrainSnapshotLoaded + "\n" +
                "profileTerrainRestoreSucceeded=" + (builder != null && builder.LastProfileTerrainRestoreSucceeded) + "\n" +
                "profileTerrainRestoreFailureReason=" + (builder != null ? builder.LastProfileTerrainRestoreFailureReason : string.Empty) + "\n" +
                "builderRecoveryRequired=" + (builder != null && builder.InitialGenerationRecoveryRequired) + "\n" +
                "initialGenerationLocked=" + YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked + "\n" +
                "worldPlanPresent=" + (plan != null) + "\n" +
                "worldPlanSeed=" + (plan != null ? plan.worldSeed : string.Empty) + "\n" +
                "worldPlanSettlements=" + (plan != null && plan.settlements != null ? plan.settlements.Count.ToString() : "0") + "\n" +
                "worldPlanSemanticChunks=" + (plan != null && plan.semanticChunks != null ? plan.semanticChunks.Count.ToString() : "0") + "\n" +
                "utc=" + System.DateTime.UtcNow.ToString("O") + "\n";
            File.WriteAllText(path, payload);
        }
        catch (IOException)
        {
            // note: A transient file lock must not interfere with Play Mode or script compilation.
        }
    }

    private static void QueueRefresh(object sender, FileSystemEventArgs args)
    {
        _refreshQueued = true;
    }

    private static void ProcessQueuedRefresh()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (EditorApplication.timeSinceStartup >= _nextPlayModeStatusWrite)
        {
            // note: Keep startup status responsive while reducing synchronous editor file writes during active Play frames.
            _nextPlayModeStatusWrite = EditorApplication.timeSinceStartup + (EditorApplication.isPlaying ? 2d : 0.5d);
            WritePlayModeStatus("Heartbeat");
        }

        if (_refreshQueued && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            _refreshQueued = false;
            // note: The asset import owns compilation. A second unconditional compilation request can outlive this refresh and reload a later Play session.
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            // note: Never dispatch a Play request in the same tick that begins importing script changes.
            return;
        }

        // note: A marker lets unattended verification exercise editor-only runtime probes without a second Unity process or a manual menu click.
        if (File.Exists(PaletteProbeMarker))
        {
            File.Delete(PaletteProbeMarker);
            YQWorldGenerationAssetKitPaletteTests.RunFromMenu();
        }

        if (File.Exists(LlmProposalPureRegressionMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Consume one explicit request and preserve the existing regression owner so skipped runtime checks remain visible as NOT_YET_TESTABLE.
            DeleteMarker(LlmProposalPureRegressionMarker);
            YQProductionBaselineRegression.RunProductionRegression();
        }

        if (File.Exists(WorldGenerationV2ContractMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Consume the bounded contract request in Edit Mode so catalog order and binding compatibility are tested without mutating the live runtime.
            DeleteMarker(WorldGenerationV2ContractMarker);
            YQWorldGenerationV2ContractTests.RunFromMenu();
        }

        if (File.Exists(BlueprintHashVerificationMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Run the focused readonly hash receipt only after the edited assemblies have finished compiling.
            DeleteMarker(BlueprintHashVerificationMarker);
            YQSpatialBlueprintV2Tests.VerifyReadonlyBlueprintHash();
        }

        if (File.Exists(RibbonWidthVerificationMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Verify disposable road and water geometry without changing any accepted world or scene.
            DeleteMarker(RibbonWidthVerificationMarker);
            YQSpatialBlueprintV2Tests.VerifyStreamedRibbonWidths();
        }

        if (File.Exists(ContinuousTerrainDeterminismMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Consume the focused receipt request at the same stable boundary as the other deterministic editor checks.
            DeleteMarker(ContinuousTerrainDeterminismMarker);
            YQContinuousTerrainDeterminismTests.RunFromMenu();
        }

        if (File.Exists(StreamingCpuCaptureMarker) && EditorApplication.isPlaying && YourQuestTutorialAutoBootstrap.GameplayPresentationReleased && Time.timeScale > 0f)
        {
            // note: Capture only a normally released journey; the profiler grants no loading or movement privileges.
            DeleteMarker(StreamingCpuCaptureMarker);
            YQWorldPresentationReview.CaptureStreamingCpu();
        }

        if (File.Exists(StreamedWaterCaptureMarker) && EditorApplication.isPlaying && YourQuestTutorialAutoBootstrap.GameplayPresentationReleased && Time.timeScale > 0f)
        {
            // note: Record both origin and streamed water in the live scene without changing fidelity settings or the gameplay camera.
            DeleteMarker(StreamedWaterCaptureMarker);
            YQWorldPresentationReview.CaptureLiveStreamedWaterAppearance();
        }

        if (File.Exists(LlmSchedulerRuntimeVerificationMarker) && EditorApplication.isPlaying &&
            YQTitleScreenUI.StartupFlowComplete && YourQuestTutorialAutoBootstrap.GameplayPresentationReleased &&
            !YQLlmSchedulerRuntimeVerification.IsRunning && LLMClient.Instance != null)
        {
            // note: Consume the runtime request only after the verifier host is confirmed live at the accepted playable boundary.
            if (YQLlmSchedulerRuntimeVerification.RunFromPlaySafe())
                DeleteMarker(LlmSchedulerRuntimeVerificationMarker);
        }

        if (File.Exists(BetaAssetBindingScreenshotMarker) && EditorApplication.isPlaying &&
            YQTitleScreenUI.StartupFlowComplete && YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
        {
            // note: Capture only after title and presentation gates are released so the receipt represents the playable world, not a loading screen.
            DeleteMarker(BetaAssetBindingScreenshotMarker);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/YQBetaAssetBindingPlaySafe.png"));
        }

        if (File.Exists(BetaAssetBindingManifestBuildMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Consume the editor-only publication request so the serialized manifest remains derived from reviewed source catalogs.
            DeleteMarker(BetaAssetBindingManifestBuildMarker);
            YQBetaAssetBindingManifestBuilder.BuildFromMenu();
        }

        if (File.Exists(BetaAssetBindingCertificationMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Consume the focused certification request after publication so the receipt observes the current serialized manifest.
            DeleteMarker(BetaAssetBindingCertificationMarker);
            YQBetaAssetBindingCertification.RunFromMenu();
        }

        if (File.Exists(GenerationReadySweepMarker))
        {
            File.Delete(GenerationReadySweepMarker);
            YQWorldGenerationAssetKitPaletteTests.RunExhaustiveGenerationReadySweep();
        }

        if (File.Exists(GenerationReadyPrefabAuditMarker) && !EditorApplication.isPlaying && !YQGenerationReadyPrefabRuntimeAudit.IsRunning)
        {
            File.Delete(GenerationReadyPrefabAuditMarker);
            YQGenerationReadyPrefabRuntimeAudit.RunFromMenu();
        }

        if (File.Exists(StreamerRuntimeStartMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Start only from an explicit one-shot request; ordinary editor stops never create this marker.
            if (File.GetLastWriteTimeUtc(StreamerRuntimeStartMarker) < System.DateTime.UtcNow.AddMinutes(-2))
            {
                DeleteMarker(StreamerRuntimeStartMarker);
                return;
            }
            DeleteMarker(StreamerRuntimeStartMarker);
            if (File.Exists(StreamerRuntimeStartMarker))
                return;
            Debug.Log("[YQEditorAutoRefresh] Explicit terrain verification start request dispatched.");
            EditorApplication.isPlaying = true;
            return;
        }

        if (File.Exists(StreamerRuntimeStopMarker) && EditorApplication.isPlaying)
        {
            // note: Handle an explicit stop request before any startup gate can defer marker processing.
            File.Delete(StreamerRuntimeStopMarker);
            EditorApplication.isPlaying = false;
            return;
        }

        if (File.Exists(OrdinaryContinueMarker) && EditorApplication.isPlaying &&
            YQTitleScreenUI.Instance != null)
        {
            // note: The explicit request waits only for the title's normal interaction delay, then drives the same selected-save action as the named menu.
            if (YQTitleScreenUI.StartupFlowComplete ||
                YQTitleScreenUI.Instance.ContinueSelectedForDevelopmentVerification())
                DeleteMarker(OrdinaryContinueMarker);
        }

        if (File.Exists(StreamerRuntimeTestMarker) && EditorApplication.isPlaying &&
            !File.Exists(BetaFixtureResetMarker) && !File.Exists(BetaFixtureStartMarker))
        {
            if (YQSemanticChunkRuntimeVerification.HasCompleted)
            {
                // note: Consume a stale request after completion so one accepted receipt remains stable until a new explicit play session.
                DeleteMarker(StreamerRuntimeTestMarker);
                return;
            }
            // note: Unity may still hold the marker during an asset refresh; leave it for the next editor tick instead of aborting the dispatch.
            try
            {
                File.Delete(StreamerRuntimeTestMarker);
            }
            catch (IOException)
            {
                return;
            }
            // note: Dispatch only after fixture setup has had a heartbeat to establish the canonical profile and world.
            if (EditorApplication.isPaused)
            {
                // note: An explicit regression request owns its temporary unpause so the live coroutine can receive frames.
                EditorApplication.isPaused = false;
            }
            Debug.Log("[YQEditorAutoRefresh] Streamer runtime verification marker dispatched.");
            YQSemanticChunkRuntimeVerification.RestartForEditor();
        }

        if (File.Exists(BetaFixtureResetMarker) && EditorApplication.isPlaying)
        {
            // note: Consume reset only while runtime services exist; the profile owner keeps destructive scope limited to beta-dev-canonical.
            try
            {
                File.Delete(BetaFixtureResetMarker);
            }
            catch (IOException)
            {
                return;
            }

            YQProfileSaveSystem profile = YQProfileSaveSystem.Instance;
            if (profile == null || !profile.EnsureCanonicalDevelopmentProfile(true))
                Debug.LogError("[YQEditorAutoRefresh] Canonical beta fixture reset failed.");
            else
                Debug.Log("[YQEditorAutoRefresh] Canonical beta fixture reset completed.");
        }

        if (File.Exists(BetaFixtureStartMarker) && EditorApplication.isPlaying)
        {
            // note: Consume the fixture request only after Play Mode owns the profile services and title UI.
            try
            {
                File.Delete(BetaFixtureStartMarker);
            }
            catch (IOException)
            {
                return;
            }

            YQTitleScreenUI title = YQTitleScreenUI.Instance;
            if (title == null || !title.CompleteCanonicalDevelopmentStartup())
                Debug.LogError("[YQEditorAutoRefresh] Canonical beta fixture startup request failed.");
            else
                Debug.Log("[YQEditorAutoRefresh] Canonical beta fixture startup request completed.");
        }

        if (File.Exists(BetaBaselineRegressionMarker) && EditorApplication.isPlaying &&
            !File.Exists(BetaFixtureResetMarker) && !File.Exists(BetaFixtureStartMarker))
        {
            // note: Consume only the one-shot capture request; the regression method itself owns pure and live receipt creation.
            try
            {
                File.Delete(BetaBaselineRegressionMarker);
            }
            catch (IOException)
            {
                return;
            }

            Debug.Log("[YQEditorAutoRefresh] Existing beta baseline regression request dispatched.");
            YQProductionBaselineRegression.RunProductionRegression();
        }

        if (File.Exists(BetaFixtureSaveMarker) && EditorApplication.isPlaying &&
            !File.Exists(BetaFixtureResetMarker) && !File.Exists(BetaFixtureStartMarker))
        {
            // note: Consume only the one-shot save request; the profile owner performs the real paired transaction and receipt publication.
            try
            {
                File.Delete(BetaFixtureSaveMarker);
            }
            catch (IOException)
            {
                return;
            }

            YQProfileSaveSystem profile = YQProfileSaveSystem.Instance;
            if (profile == null || !profile.SaveActiveProfile())
                Debug.LogError("[YQEditorAutoRefresh] Canonical beta fixture save request failed.");
            else
                Debug.Log("[YQEditorAutoRefresh] Canonical beta fixture save request completed.");
        }

        if (File.Exists(BetaProfileIsolationMarker) && EditorApplication.isPlaying &&
            !File.Exists(BetaFixtureResetMarker) && !File.Exists(BetaFixtureStartMarker))
        {
            // note: Consume only the one-shot isolation request; the existing title, origin, and profile owners perform all state transitions.
            try
            {
                File.Delete(BetaProfileIsolationMarker);
            }
            catch (IOException)
            {
                return;
            }

            Debug.Log("[YQEditorAutoRefresh] Existing profile isolation request dispatched.");
            YQProductionBaselineRegression.RunProfileBOriginRegression();
        }

        if (File.Exists(BetaProfileReloadPrepareMarker) && EditorApplication.isPlaying &&
            !File.Exists(BetaFixtureResetMarker) && !File.Exists(BetaFixtureStartMarker))
        {
            // note: Consume the one-shot preparation request; the existing profile and title owners select profile B before restart.
            try
            {
                File.Delete(BetaProfileReloadPrepareMarker);
            }
            catch (IOException)
            {
                return;
            }

            Debug.Log("[YQEditorAutoRefresh] Existing profile reload preparation dispatched.");
            YQProductionBaselineRegression.PrepareProfileBForReload();
        }

        if (File.Exists(BetaProfileReloadVerifyMarker) && EditorApplication.isPlaying &&
            !File.Exists(BetaFixtureResetMarker) && !File.Exists(BetaFixtureStartMarker))
        {
            // note: Consume the one-shot verification request only after the restarted PlaySafe runtime is ready.
            try
            {
                File.Delete(BetaProfileReloadVerifyMarker);
            }
            catch (IOException)
            {
                return;
            }

            Debug.Log("[YQEditorAutoRefresh] Existing profile reload verification dispatched.");
            YQProductionBaselineRegression.VerifyProfileBReload();
        }

    }
}
#endif
// note: Automatic refresh verification touch for palette probe marker dispatch.
// note: Automatic refresh verification touch for focused runtime streamer rebuild and terminal Edit Mode handoff.
