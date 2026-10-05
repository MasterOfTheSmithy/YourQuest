#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;

// note: Keep the editor's global auto-refresh preference enabled so external script edits reach Unity without a manual Ctrl+R.
[InitializeOnLoad]
internal static class YQEditorAutoRefreshBootstrap
{
    // note: Measure editor callback and status publication work separately so frame-budget receipts can exclude neither from the observed editor session.
    private static readonly ProfilerMarker EditorRefreshUpdateMarker =
        new ProfilerMarker("YQEditorAutoRefreshBootstrap.ProcessQueuedRefresh");
    private static readonly ProfilerMarker PlayModeStatusWriteMarker =
        new ProfilerMarker("YQEditorAutoRefreshBootstrap.WritePlayModeStatus");
    private static FileSystemWatcher _scriptWatcher;
    private static volatile bool _refreshQueued;
    private const string PlayRefreshSuspendedKey = "YourQuest.EditorAutoRefresh.PlaySuspended";
    private const string ScriptWatchSuspendedKey = "YourQuest.EditorAutoRefresh.ScriptWatchSuspended";
    private const string PaletteProbeMarker = "Temp/YQ_PALETTE_RUNTIME_PROBE.request";
    private const string GenerationReadySweepMarker = "Temp/YQ_GENERATION_READY_SWEEP.request";
    private const string GenerationReadyPrefabAuditMarker = "Temp/YQ_GENERATION_READY_PREFAB_AUDIT.request";
    // note: A distinct start request is the only path allowed to enter Play Mode for unattended verification.
    private const string StreamerRuntimeStartMarker = "Temp/YQ_STREAMER_RUNTIME_START.request";
    private const string StreamerRuntimeTestMarker = "Temp/YQ_STREAMER_RUNTIME_TEST.request";
    // note: Dispatch the existing short speed witness without desktop input or the unrelated full acceptance suite.
    private const string FocusedSpeed260Marker = "Temp/YQ_SPEED260_FOCUSED.request";
    // note: Dispatch the existing isolated R1 recovery matrix through the same one-shot background marker path.
    private const string FocusedR1PublicationMarker = "Temp/YQ_R1_PUBLICATION_FOCUSED.request";
    // note: Dispatch the existing R1-plus-unload witness without requiring the visible Unity menu.
    private const string FocusedR1UnloadRevisitMarker = "Temp/YQ_R1_UNLOAD_REVISIT_FOCUSED.request";
    // note: R3 owns a distinct diagnostic request; it never dispatches the R1/R2 acceptance suite.
    private const string PhysicalR3Marker = "Temp/YQ_G08_R3_PHYSICAL.request";
    private const string EnvironmentBindingMarker = "Temp/YQ_ENVIRONMENT_BINDING_CONTRACT.request";
    private const string AssetLibraryEvidenceMarker = "Temp/YQ_ASSET_LIBRARY_EVIDENCE.request";
    private const string HydrologySegmentMarker = "Temp/YQ_HYDROLOGY_SEGMENT_CONTRACT.request";
    private const string BridgeOwnershipMarker = "Temp/YQ_BRIDGE_OWNERSHIP_CONTRACT.request";
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
    private const string TreeGroundingVerificationMarker = "Temp/YQ_TREE_GROUNDING_VERIFY.request";
    private const string WorldReadinessVerificationMarker = "Temp/YQ_WORLD_READINESS_VERIFY.request";
    private const string WorldReadinessAuditMarker = "Temp/YQ_WORLD_READINESS_AUDIT.request";
    private const string MaterialContractRepairMarker = "Temp/YQ_MATERIAL_CONTRACT_REPAIR.request";
    private const string AssetContractLibraryReviewMarker = "Temp/YQ_ASSET_CONTRACT_LIBRARY_REVIEW.request";
    private const string FocusedLibraryCurationMarker = "Temp/YQ_FOCUSED_LIBRARY_CURATION.request";
    private const string StreamedSiteOwnerAuditMarker = "Temp/YQ_STREAMED_SITE_OWNER_AUDIT.request";
    // note: Explicit assisted visits share the existing save barrier and wait for the selected profile's generation owners to become idle.
    private const string RemoteSiteVisitMarker = "Temp/YQ_REMOTE_SITE_VISIT.request";
    private const string LandscapeReviewMarker = "Temp/YQ_LANDSCAPE_REVIEW.request";
    private const string TerrainCohesionPreviewMarker = "Temp/YQ_TERRAIN_COHESION_PREVIEW.request";
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

        // note: Mono's managed watcher scans the entire asset tree; create it inactive so Play's domain reload never starts that background work.
        string assetsPath = Path.GetFullPath("Assets");
        if (Directory.Exists(assetsPath))
        {
            _scriptWatcher = new FileSystemWatcher(assetsPath, "*.cs")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = false
            };
            _scriptWatcher.Changed += QueueRefresh;
            _scriptWatcher.Created += QueueRefresh;
            _scriptWatcher.Renamed += QueueRefresh;
            _scriptWatcher.Deleted += QueueRefresh;
        }

        // note: The editor update performs Unity API calls on the main thread after the watcher signals an external source change.
        EditorApplication.update -= ProcessQueuedRefreshMeasured;
        EditorApplication.update += ProcessQueuedRefreshMeasured;
        // note: A manual Play Mode exit cancels a still-pending verifier request so the editor never starts Play Mode again by itself.
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        SetPlayRefreshSuspended(EditorApplication.isPlayingOrWillChangePlaymode);
        SetScriptWatchSuspended(EditorApplication.isPlayingOrWillChangePlaymode);
        // note: Release the old domain's watcher explicitly rather than relying on finalization after reload or Editor exit.
        AssemblyReloadEvents.beforeAssemblyReload -= DisposeScriptWatcher;
        AssemblyReloadEvents.beforeAssemblyReload += DisposeScriptWatcher;
        EditorApplication.quitting -= DisposeScriptWatcher;
        EditorApplication.quitting += DisposeScriptWatcher;
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

    [MenuItem("YourQuest/Verification/Run Focused G08 R1 Publication Recovery in Current Play Session")]
    private static void RunFocusedR1PublicationWitnessInCurrentPlaySession()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[YQEditorAutoRefresh] Enter Play Mode and complete normal startup before running the focused R1 publication witness.");
            return;
        }
        // note: Keep publication recovery independently runnable without spending time on unrelated speed and unload/revisit probes.
        if (!YQSemanticChunkRuntimeVerification.TryBeginFocusedR1PublicationWitnessFromCurrentPlaySession())
            Debug.Log("[YQEditorAutoRefresh] A terrain verifier is already running or the focused R1 witness could not start.");
    }

    [MenuItem("YourQuest/Verification/Run Focused G08 R1 Recovery and Unload Revisit in Current Play Session")]
    private static void RunFocusedR1UnloadRevisitWitnessInCurrentPlaySession()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[YQEditorAutoRefresh] Enter Play Mode and complete normal startup before running the focused R1 and unload/revisit witness.");
            return;
        }
        // note: Run current-source publication recovery before the independent physical unload/revisit lifecycle check.
        if (!YQSemanticChunkRuntimeVerification.TryBeginFocusedR1UnloadRevisitWitnessFromCurrentPlaySession())
            Debug.Log("[YQEditorAutoRefresh] A terrain verifier is already running or the focused R1 unload/revisit witness could not start.");
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
        {
            SetScriptWatchSuspended(true);
            SetPlayRefreshSuspended(true);
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SetPlayRefreshSuspended(false);
            SetScriptWatchSuspended(false);
        }
        // note: Record every Unity transition before handling request cleanup so automation can observe the real editor instance.
        WritePlayModeStatus(state.ToString());
        if (state != PlayModeStateChange.ExitingPlayMode)
            return;
        // note: A manual stop cancels both phases so no stale request can replay after the user leaves Play Mode.
        DeleteMarker(StreamerRuntimeStartMarker);
        DeleteMarker(StreamerRuntimeTestMarker);
        DeleteMarker(FocusedSpeed260Marker);
        DeleteMarker(FocusedR1PublicationMarker);
        DeleteMarker(FocusedR1UnloadRevisitMarker);
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

    private static void SetScriptWatchSuspended(bool suspended)
    {
        // note: Imports are already forbidden during Play; a single catch-up refresh on return detects edits made while watching was paused.
        bool wasSuspended = SessionState.GetBool(ScriptWatchSuspendedKey, false);
        if (_scriptWatcher != null && _scriptWatcher.EnableRaisingEvents == suspended)
            _scriptWatcher.EnableRaisingEvents = !suspended;
        SessionState.SetBool(ScriptWatchSuspendedKey, suspended);
        if (wasSuspended && !suspended) _refreshQueued = true;
    }

    private static void DisposeScriptWatcher()
    {
        // note: Drop only the editor's source watcher; queued imports, profile services, and runtime ownership are unchanged.
        FileSystemWatcher watcher = _scriptWatcher;
        _scriptWatcher = null;
        if (watcher == null) return;
        watcher.Changed -= QueueRefresh;
        watcher.Created -= QueueRefresh;
        watcher.Renamed -= QueueRefresh;
        watcher.Deleted -= QueueRefresh;
        watcher.Dispose();
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
        using (PlayModeStatusWriteMarker.Auto())
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
                // note: Read the actual Game focus before arming measurement; an occlusion-safe Editor screenshot alone does not establish application focus.
                "applicationFocused=" + Application.isFocused + "\n" +
                "focusedEditorWindow=" + (EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.GetType().FullName : "<none>") + "\n" +
                "paused=" + EditorApplication.isPaused + "\n" +
                "compiling=" + EditorApplication.isCompiling + "\n" +
                "updating=" + EditorApplication.isUpdating + "\n" +
                "refreshQueued=" + _refreshQueued + "\n" +
                "playRefreshSuspended=" + SessionState.GetBool(PlayRefreshSuspendedKey, false) + "\n" +
                "scriptWatchSuspended=" + SessionState.GetBool(ScriptWatchSuspendedKey, false) + "\n" +
                "scriptWatcherEnabled=" + (_scriptWatcher != null && _scriptWatcher.EnableRaisingEvents) + "\n" +
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
                "loadingVisible=" + YQStartupLoadingScreen.IsVisible + "\n" +
                "goddessLoadingVisible=" + YQStartupLoadingScreen.IsGenerationVisible + "\n" +
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

        // note: File.Exists allocates for every absent path on this Editor runtime. One nonrecursive directory check avoids polling all handlers on empty ticks without throttling requests.
        if (!HasPendingVerificationRequest("Temp", ContinuousTerrainDeterminismMarker))
            return;

        // note: Import pending source edits before analyzing; read frame-identified evidence only after the witness has left Play Mode.
        YQSemanticChunkRuntimeVerification.AnalyzeRequestedR2FrameTrace();

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

        if (File.Exists(RemoteSiteVisitMarker) && EditorApplication.isPlaying &&
            YourQuestTutorialAutoBootstrap.GameplayPresentationReleased &&
            YQAssetContractLibraryReview.CanBeginRemoteSiteProbe)
        {
            // note: The first line retains the profile guard; an optional accepted-site ID bounds a focused regression visit without changing the original all-site request.
            string[] request = File.ReadAllLines(RemoteSiteVisitMarker);
            if (request.Length > 0 && request[0].Trim() == YQProfileSaveSystem.Instance?.ActiveProfileId)
            {
                string requestedSiteId = request.Length > 1 ? request[1].Trim() : null;
                DeleteMarker(RemoteSiteVisitMarker);
                YQAssetContractLibraryReview.BeginRemoteSiteProbeForSite(requestedSiteId);
            }
        }

        // note: Render only currently loaded production geometry without moving the player or changing streaming demand.
        if (File.Exists(PhysicalR3Marker) && EditorApplication.isPlaying &&
            YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
        {
            string command = File.ReadAllText(PhysicalR3Marker);
            DeleteMarker(PhysicalR3Marker);
            YQG08R3PhysicalItineraryVerification.Dispatch(command);
        }

        if (File.Exists(LandscapeReviewMarker) && EditorApplication.isPlaying && YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
        {
            DeleteMarker(LandscapeReviewMarker);
            YQWorldPresentationReview.CaptureLandscapeRepairReview();
        }

        // note: This explicit read-only snapshot does not drive the player, regenerate content, or change readiness.
        if (File.Exists(WorldReadinessAuditMarker) && EditorApplication.isPlaying)
        {
            DeleteMarker(WorldReadinessAuditMarker);
            YQWorldReadinessVerification.AuditRuntime();
        }

        // note: Keep water/material fixtures in the existing explicit Edit-only dispatcher.
        if (File.Exists(FocusedLibraryCurationMarker) && !EditorApplication.isPlayingOrWillChangePlaymode && !YQUrpAssetConversionBatch.IsRunning)
        {
            // note: The explicit construction review changes only selected source intake contracts after material source repair finishes.
            DeleteMarker(FocusedLibraryCurationMarker);
            YQFocusedConstructionLibraryCuration.Run();
        }
        if (File.Exists(AssetContractLibraryReviewMarker) && !EditorApplication.isPlayingOrWillChangePlaymode && !YQUrpAssetConversionBatch.IsRunning)
        {
            DeleteMarker(AssetContractLibraryReviewMarker);
            YQAssetContractLibraryReview.Run();
        }
        if (File.Exists(StreamedSiteOwnerAuditMarker) && EditorApplication.isPlaying && YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
        {
            DeleteMarker(StreamedSiteOwnerAuditMarker);
            YQAssetContractLibraryReview.AuditPlay();
        }
        if (File.Exists(MaterialContractRepairMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Source binding repair is explicit and paced; it never runs from an import or a Play startup.
            DeleteMarker(MaterialContractRepairMarker);
            YQUrpAssetConversionBatch.StartContractRepair();
        }

        if (File.Exists(WorldReadinessVerificationMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            DeleteMarker(WorldReadinessVerificationMarker);
            YQWorldReadinessVerification.Run();
        }

        // note: Reuse the existing Edit-only request dispatcher instead of adding a persistent per-frame verification callback.
        if (File.Exists(TreeGroundingVerificationMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            DeleteMarker(TreeGroundingVerificationMarker);
            YQTreeGroundingVerification.Run();
        }

        // note: An explicit Edit-only request creates a separate design study without loading or changing an accepted world.
        if (File.Exists(TerrainCohesionPreviewMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            DeleteMarker(TerrainCohesionPreviewMarker);
            YQTerrainCohesionPreview.Build();
        }

        if (File.Exists(WorldGenerationV2ContractMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Consume the bounded contract request in Edit Mode so catalog order and binding compatibility are tested without mutating the live runtime.
            DeleteMarker(WorldGenerationV2ContractMarker);
            YQWorldGenerationV2ContractTests.RunFromMenu();
        }

        if (File.Exists(EnvironmentBindingMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: A focused Edit-only fixture checks catalog/query order without starting the broader world or runtime suites.
            DeleteMarker(EnvironmentBindingMarker);
            YQEnvironmentBindingVerification.Run();
        }

        if (File.Exists(AssetLibraryEvidenceMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Documentary contracts run on disposable Edit Mode objects without scanning or publishing production assets.
            DeleteMarker(AssetLibraryEvidenceMarker);
            YQAssetLibraryEvidenceVerification.Run();
        }

        if (File.Exists(BridgeOwnershipMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: This explicit request tests disposable collision ownership through the existing integrity routine, never a production profile.
            DeleteMarker(BridgeOwnershipMarker);
            YQEnvironmentBridgeVerification.Run();
        }

        if (File.Exists(HydrologySegmentMarker) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: Bounded numeric ribbon cases use the existing materializer; no scene, profile, palette or topology is published.
            DeleteMarker(HydrologySegmentMarker);
            YQEnvironmentHydrologyVerification.Run();
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
            // note: Empty legacy markers retain the current selection; a supplied profile GUID selects only that existing save through the ordinary title action.
            string requestedProfile = File.ReadAllText(OrdinaryContinueMarker).Trim();
            if (!string.IsNullOrEmpty(requestedProfile) && !Guid.TryParseExact(requestedProfile, "N", out _))
            {
                DeleteMarker(OrdinaryContinueMarker);
                Debug.LogError("[YQEditorAutoRefresh] Continue request rejected: expected an existing profile GUID or an empty marker.");
                return;
            }
            if (YQTitleScreenUI.StartupFlowComplete ||
                (string.IsNullOrEmpty(requestedProfile)
                    ? YQTitleScreenUI.Instance.ContinueSelectedForDevelopmentVerification()
                    : YQTitleScreenUI.Instance.ContinueExistingForDevelopmentVerification(requestedProfile)))
                DeleteMarker(OrdinaryContinueMarker);
        }

        // note: Existing full-suite and fixture requests retain priority; never start a probe before a pending profile mutation.
        if (File.Exists(FocusedSpeed260Marker) &&
            (File.Exists(StreamerRuntimeTestMarker) || File.Exists(BetaFixtureResetMarker) || File.Exists(BetaFixtureStartMarker)))
        {
            DeleteMarker(FocusedSpeed260Marker);
            Debug.LogWarning("[YQEditorAutoRefresh] Focused speed request rejected because a full verifier or fixture request is pending.");
        }

        // note: Keep focused recovery probes separate from a pending full suite or profile mutation.
        if (File.Exists(FocusedR1PublicationMarker) &&
            (File.Exists(StreamerRuntimeTestMarker) || File.Exists(BetaFixtureResetMarker) || File.Exists(BetaFixtureStartMarker)))
        {
            DeleteMarker(FocusedR1PublicationMarker);
            Debug.LogWarning("[YQEditorAutoRefresh] Focused R1 request rejected because a full verifier or fixture request is pending.");
        }

        if (File.Exists(FocusedR1UnloadRevisitMarker) &&
            (File.Exists(StreamerRuntimeTestMarker) || File.Exists(BetaFixtureResetMarker) || File.Exists(BetaFixtureStartMarker)))
        {
            DeleteMarker(FocusedR1UnloadRevisitMarker);
            Debug.LogWarning("[YQEditorAutoRefresh] Focused R1 unload/revisit request rejected because a full verifier or fixture request is pending.");
        }

        if (File.Exists(FocusedR1PublicationMarker) && EditorApplication.isPlaying &&
            YQTitleScreenUI.StartupFlowComplete && YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
        {
            // note: Reuse the focused R1 menu entry point only after ordinary startup has released gameplay.
            if (YQSemanticChunkRuntimeVerification.IsRunning)
            {
                DeleteMarker(FocusedR1PublicationMarker);
                Debug.LogWarning("[YQEditorAutoRefresh] Focused R1 request ignored because a verifier is already running.");
            }
            else if (YQSemanticChunkRuntimeVerification.TryBeginFocusedR1PublicationWitnessFromCurrentPlaySession())
            {
                DeleteMarker(FocusedR1PublicationMarker);
                Debug.Log("[YQEditorAutoRefresh] Focused R1 publication recovery marker dispatched.");
            }
        }

        if (File.Exists(FocusedR1UnloadRevisitMarker) && EditorApplication.isPlaying &&
            YQTitleScreenUI.StartupFlowComplete && YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
        {
            // note: Reuse the accepted-save PlaySafe session for the current-source recovery and physical unload/revisit receipt.
            if (YQSemanticChunkRuntimeVerification.IsRunning)
            {
                DeleteMarker(FocusedR1UnloadRevisitMarker);
                Debug.LogWarning("[YQEditorAutoRefresh] Focused R1 unload/revisit request ignored because a verifier is already running.");
            }
            else if (YQSemanticChunkRuntimeVerification.TryBeginFocusedR1UnloadRevisitWitnessFromCurrentPlaySession())
            {
                DeleteMarker(FocusedR1UnloadRevisitMarker);
                Debug.Log("[YQEditorAutoRefresh] Focused R1 unload/revisit marker dispatched.");
            }
        }

        if (File.Exists(FocusedSpeed260Marker) && EditorApplication.isPlaying &&
            YQTitleScreenUI.StartupFlowComplete && YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
        {
            // note: Consume one explicit request only after normal startup; reuse the menu's verifier entry point and ownership.
            if (YQSemanticChunkRuntimeVerification.IsRunning)
            {
                DeleteMarker(FocusedSpeed260Marker);
                Debug.LogWarning("[YQEditorAutoRefresh] Focused speed request ignored because a verifier is already running.");
            }
            else if (YQSemanticChunkRuntimeVerification.TryBeginFocusedSpeed260WitnessFromCurrentPlaySession())
            {
                DeleteMarker(FocusedSpeed260Marker);
                Debug.Log("[YQEditorAutoRefresh] Focused 260 speed witness marker dispatched.");
            }
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

    private static bool HasPendingVerificationRequest(string requestDirectory, string externalRequest)
    {
        // note: The terrain contract marker lives outside Temp. Preserve it and check current disk state each tick, with no watcher, cached result or verification-only fast path.
        if (File.Exists(externalRequest)) return true;
        if (!Directory.Exists(requestDirectory)) return false;
        try
        {
            using (var requests = Directory.EnumerateFiles(requestDirectory, "YQ_*.request", SearchOption.TopDirectoryOnly).GetEnumerator())
                return requests.MoveNext();
        }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
        // note: A failed census falls through to the original handlers rather than suppressing a request.
    }

    private static void ProcessQueuedRefreshMeasured()
    {
        using (EditorRefreshUpdateMarker.Auto())
            ProcessQueuedRefresh();
    }
}
#endif
// note: Automatic refresh verification touch for palette probe marker dispatch.
// note: Automatic refresh verification touch for focused runtime streamer rebuild and terminal Edit Mode handoff.
