#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

// note: Explicit diagnostics observe loaded gameplay or detached fixtures; the named startup capture invokes only the existing selected-save title action.
[InitializeOnLoad]
internal static class YQOriginLagVerification
{
    private const string Request = "Assets/Assets/EditorBuildRequests/OriginLag.request";
    private const string Output = "outputs/OriginLag_20261009";
    private const string UnprofiledSession = "YourQuest.OriginLag.UnprofiledSession";
    private const string UnprofiledBaseline = Output + "/unprofiled-gameplay-baseline.flag";
    private static double traceUntil;
    private static double traceMinimumMs = 1000d;
    private static int traceStartFrame;
    private static long traceStartMemory;
    private static bool traceExportPending;
    private static bool activationTraceArmed, activationTraceRunning, activationTraceCompleted;
    private static double activationTraceArmUntil;
    private static int activationTraceReturnNativeFrame;
    private static string activationTraceSiteId, activationTraceCompletedSiteId;
    private static string activationTraceRawPath;
    private static double startupStarted;
    private static string startupStage;
    private static readonly JArray startupStages = new JArray();

    private static void ObserveStartup()
    {
        // note: Explicitly armed wall-clock observation includes all loading frames, without the native profiler's memory overhead or any input/save mutation.
        if (!SessionState.GetBool("YQ.StartupTiming.Armed", false) || !Application.isPlaying) return;
        if (startupStarted <= 0d)
        {
            if (!YQStartupLoadingScreen.IsGenerationVisible) return;
            startupStarted = EditorApplication.timeSinceStartup;
        }
        bool released = YourQuestTutorialAutoBootstrap.GameplayPresentationReleased;
        string stage = released ? "Gameplay released" : YQStartupLoadingScreen.CurrentGenerationPhase + " / " + YQStartupLoadingScreen.CurrentGenerationSubstep;
        if (stage == startupStage) return;
        startupStage = stage;
        startupStages.Add(new JObject { ["stage"] = stage, ["elapsedSeconds"] = EditorApplication.timeSinceStartup - startupStarted,
            ["progress"] = YQStartupLoadingScreen.CurrentGenerationProgress, ["integrationMs"] = AsyncInstantiateOperation.GetIntegrationTimeMS() });
        Write("startup-timing", new { utc = DateTime.UtcNow, released, elapsedSeconds = EditorApplication.timeSinceStartup - startupStarted,
            stages = startupStages, workMilliseconds = YQGeneratedWorldRuntimeBuilder.StartupWorkMilliseconds,
            waitMilliseconds = YQGeneratedWorldRuntimeBuilder.StartupWaitMilliseconds,
            evidence = "Ordinary PlaySafe loading-screen observation; Continue does not certify fresh-world LLM generation" });
        if (released) SessionState.SetBool("YQ.StartupTiming.Armed", false);
    }
    static YQOriginLagVerification()
    {
        EditorApplication.update += Tick;
        // note: Domain reload must not lose the deadline while leaving native recording enabled.
        AssemblyReloadEvents.beforeAssemblyReload += StopOwnedTraceBeforeReload;
        EditorApplication.quitting += StopOwnedTraceBeforeReload;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                StopOwnedTraceBeforeReload();
            // note: Explicit cleanup persists only this project's unprofiled baseline across Editor restarts. Manual recording and bounded trace requests remain available; global Editor preferences are unchanged.
            if (state == PlayModeStateChange.EnteredPlayMode &&
                (SessionState.GetBool(UnprofiledSession, false) || File.Exists(UnprofiledBaseline)))
                ProfilerDriver.enabled = false;
        };
    }
    private static void StopOwnedTraceBeforeReload()
    {
        ClearActivationTraceHooks();
        if (traceUntil <= 0d && !traceExportPending) return;
        try { if (traceUntil > 0d) FinishOwnedTrace(); ExportOwnedTrace(); }
        catch (Exception error) { Debug.LogException(error); }
    }
    private static void Tick()
    {
        ObserveStartup();
        // note: Unity publishes the ending native frame after the callback that stopped recording. Export on the next callback so the triggering stall is not discarded.
        if (traceExportPending) ExportOwnedTrace();
        if (activationTraceArmed && (!Application.isPlaying || EditorApplication.timeSinceStartup >= activationTraceArmUntil))
        {
            ClearActivationTraceHooks();
            Write("population-activation-trace-arm-timeout", new { utc = DateTime.UtcNow, site = activationTraceSiteId,
                evidence = "No last resident reached the one-shot activation trace arm before its 90-second deadline." });
        }
        // note: Export the first long native frame immediately so a later quiet 300-frame ring cannot erase the hitch.
        if (traceUntil > 0d)
        {
            // note: Native profiler history can consume gigabytes even with deep profiling disabled. Own only a short capture with explicit frame, time and memory bounds.
            bool finished = !Application.isPlaying || EditorApplication.timeSinceStartup >= traceUntil ||
                ProfilerDriver.lastFrameIndex - traceStartFrame >= 119 ||
                UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() - traceStartMemory >= 256L * 1024 * 1024;
            if (activationTraceRunning)
                finished |= activationTraceCompleted && ProfilerDriver.lastFrameIndex > activationTraceReturnNativeFrame;
            else
                using (var frame = ProfilerDriver.GetRawFrameDataView(ProfilerDriver.lastFrameIndex, 0))
                    finished |= frame.valid && frame.frameTimeMs >= traceMinimumMs;
            if (finished) FinishOwnedTrace();
        }
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string mode;
        // note: An external one-shot request can still be finishing publication; defer a transient sharing violation rather than losing the diagnostic callback.
        try { mode = File.ReadAllText(Request).Trim(); }
        catch (IOException) { return; }
        bool startupCapture = mode == "startup-cpu" || mode == "startup-cpu-continue" || mode == "startup-trace" || mode == "startup-trace-continue" || mode == "whole-session";
        // note: Named terrain probes reuse their existing guarded diagnostic owners; direct dispatch keeps native Editor menus out of CPU measurements.
        bool terrainDiagnostic = mode == "terrain-east" || mode == "terrain-north" || mode == "terrain-west" || mode == "terrain-south" ||
            mode == "terrain-low" || mode == "terrain-tumble" || mode == "terrain-lateral" || mode == "terrain-npc" || mode == "terrain-trees" || mode == "terrain-draw";
        bool live = mode == "runtime" || mode == "publication" || mode == "conversation-runtime" || mode == "offer-intent-model" || mode == "dialogue-latency" || mode == "validation-cache-contracts" || mode == "admission-chain-contracts" || mode == "cooperative-geometry" || mode == "frontier-worker-contracts" || mode == "cpu" || mode == "cpu-minute" || mode == "cpu-streamer" || mode == "cpu-population" || mode == "trace-population-activation" || startupCapture || mode == "record" || mode == "record-stop" || mode == "trace" || mode == "trace-long";
        // note: Only the explicit startup timing probe can run before gameplay readiness; ordinary verification retains its production release gate.
        live |= terrainDiagnostic;
        live |= mode == "speed200" || mode == "speed200-stop";
        bool anyState = mode == "registered-coroutines" || mode == "profile-selection-state";
        if (!anyState && live && (!Application.isPlaying || (!startupCapture && !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased))) return;
        // note: The latency proof must encounter real background contention, rather than a convenient empty inference queue.
        if (mode == "dialogue-latency" && LLMClient.Instance?.ActiveRequestDebugTag != "FrontierLocationBrief") return;
        if (!anyState && !live && Application.isPlaying) return;
        AssetDatabase.DeleteAsset(Request);
        try
        {
            if (mode == "startup-timing") { SessionState.SetBool("YQ.StartupTiming.Armed", true); startupStarted = 0d; startupStage = null; startupStages.Clear(); }
            else if (mode == "frames") Frames();
            else if (mode == "activation-raw-metadata") InspectSavedActivationRawMetadata();
            else if (mode == "release-profiler-history") ReleaseProfilerHistory();
            else if (mode == "registered-coroutines")
            {
                // note: Discover existing native handles without starting recording or touching gameplay; the result can fund a later bounded recorder selection.
                var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
                Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
                Write("registered-coroutines", new { utc = DateTime.UtcNow, evidence = "READ_ONLY_NATIVE_REGISTERED_COROUTINE_HANDLES_NOT_TIMING_PROOF",
                    names = handles.Select(handle => Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(handle).Name)
                        .Where(name => name.Contains("[Coroutine: MoveNext]")).Distinct().OrderBy(name => name).ToArray() });
            }
            else if (mode == "allow-profiler-recording")
            {
                // note: Explicit opt-in restores automatic recording admission without starting a capture or changing global preferences.
                SessionState.EraseBool(UnprofiledSession);
                if (File.Exists(UnprofiledBaseline)) File.Delete(UnprofiledBaseline);
            }
            else if (mode == "runtime") Runtime();
            else if (mode == "publication") Publication();
            else if (mode == "dialogue-contracts") DialogueContracts();
            else if (mode == "conversation-contracts") ConversationContracts();
            else if (mode == "performance-contracts") PerformanceContracts();
            else if (mode == "conversation-runtime") ConversationRuntime();
            else if (mode == "offer-intent-model") OfferIntentModel();
            else if (mode == "dialogue-latency") DialogueLatency();
            else if (mode == "storage-contracts") StorageContracts();
            else if (mode == "world-repair-contracts") WorldRepairContracts();
            else if (mode == "candidate-cache-contracts") CandidateCacheContracts();
            else if (mode == "validation-cache-contracts") ValidationCacheContracts();
            else if (mode == "admission-chain-contracts") AdmissionChainContracts();
            else if (mode == "private-union-contracts") PrivateUnionContracts();
            else if (mode == "captured-copy-contracts") CapturedCopyContracts();
            else if (mode == "identity-screen-contracts") IdentityScreenContracts();
            else if (mode == "cooperative-geometry") YQCooperativeGeometryVerification.Run();
            else if (mode == "frontier-worker-contracts") YQCooperativeGeometryVerification.StartFrontierWorkerContracts();
            else if (mode == "cpu") YQWorldPresentationReview.CaptureStreamingCpu();
            else if (mode == "cpu-minute") YQWorldPresentationReview.CaptureStreamingCpuMinute();
            // note: Explicit owner attribution is read-only and waits for the same ordinary gameplay release gate.
            else if (mode == "cpu-streamer") YQWorldPresentationReview.CaptureStreamerAttribution();
            else if (mode == "cpu-population") YQWorldPresentationReview.CapturePopulationAttribution();
            else if (mode == "trace-population-activation") ArmPopulationActivationTrace();
            // note: Protected disposable input diagnostics never change the production motor or streamer source.
            else if (mode == "speed200") YQHighSpeedTravelWitness.Start();
            else if (mode == "speed200-stop") YQHighSpeedTravelWitness.Abort();
            else if (mode == "profile-selection-contracts") ProfileSelectionContracts();
            else if (mode == "profile-selection-state") ProfileSelectionState();
            else if (mode == "terrain-east") YQStreamedTravelVerification.BeginDesktopEast();
            else if (mode == "terrain-north") YQStreamedTravelVerification.BeginDesktopNorth();
            else if (mode == "terrain-west") YQStreamedTravelVerification.BeginDesktopWest();
            else if (mode == "terrain-south") YQStreamedTravelVerification.BeginDesktopSouth();
            else if (mode == "terrain-low") YQDisposablePlayerLandingVerification.Low();
            else if (mode == "terrain-tumble") YQDisposablePlayerLandingVerification.Tumble();
            else if (mode == "terrain-lateral") YQDisposablePlayerLandingVerification.LateralTumble();
            else if (mode == "terrain-npc") YQReviewedNpcFloorNativeCapture.CaptureNearby();
            else if (mode == "terrain-trees") YQTreeMaterialNativeCapture.CaptureNearby();
            else if (mode == "terrain-draw") YQSelectedFrameDrawNativeCapture.Capture();
            else if (mode == "startup-cpu") YQWorldPresentationReview.CaptureStartupCpu();
            else if (mode == "whole-session") YQWholeSessionFrameCapture.Start();
            else if (mode == "startup-trace-continue" || mode == "startup-cpu-continue")
            {
                // note: Arm before the ordinary save load in the same callback; separate requests can miss the synchronous title-transition hitch.
                if (YQTitleScreenUI.Instance == null || YQTitleScreenUI.StartupFlowComplete)
                    throw new InvalidOperationException("Startup capture requires the open title screen.");
                if (mode == "startup-cpu-continue") YQWorldPresentationReview.CaptureStartupCpu();
                else StartOwnedTrace(1000d);
                if (!YQTitleScreenUI.Instance.ContinueSelectedForDevelopmentVerification())
                    throw new InvalidOperationException("The ordinary selected-save action is not ready.");
            }
            else if (mode == "record" || mode == "trace" || mode == "trace-long" || mode == "startup-trace") StartOwnedTrace(mode == "trace-long" ? 5000d : 1000d);
            else if (mode == "record-stop") { if (traceUntil > 0d) FinishOwnedTrace(); else { ProfilerDriver.enabled = false; Frames(); } }
            // note: Preserve explicit and legacy empty contract requests; an unknown mode must never silently launch expensive unrelated fixtures on an older/newer diagnostic mismatch.
            else if (mode == "contracts" || mode.Length == 0) Contracts();
            else
            {
                Write("unsupported-mode", new { utc = DateTime.UtcNow, mode, passed = false, failure = "Unsupported OriginLag diagnostic mode." });
                Debug.LogWarning("[YQOriginLag] Unsupported diagnostic mode: " + mode);
            }
        }
        catch (Exception error) { Write(mode + "-error", new { error = error.ToString() }); Debug.LogException(error); }
    }
    private static void Write(string name, object value)
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, name + ".json"), JsonConvert.SerializeObject(value, Formatting.Indented));
    }

    private static void ProfileSelectionContracts()
    {
        // note: Detached selection fixtures prove the complete pointer changes together and rejected foreign revisions cannot partially alter it.
        var manifest = new YQProfileSaveSystem.ProfileManifest();
        foreach (string id in new[] { "a", "b", "legacy" }) manifest.profiles.Add(new YQProfileSaveSystem.ProfileEntry { profileId = id });
        var a = new YQProfileCommitRecord { profileId = "a", commitId = "a-revision", revision = 208, status = "complete" };
        var b = new YQProfileCommitRecord { profileId = "b", commitId = "b-revision", revision = 48, status = "complete" };
        manifest.commits.Add(a); manifest.commits.Add(b);
        var select = typeof(YQProfileSaveSystem.ProfileManifest).GetMethod("SelectLoadedProfile", BindingFlags.Instance | BindingFlags.NonPublic);
        var checks = new List<string>();
        void Check(string name, bool passed) { if (!passed) throw new InvalidOperationException(name); checks.Add(name); }
        bool Matches(YQProfileCommitRecord commit) => manifest.activeProfileId == commit.profileId && manifest.activeCommitId == commit.commitId && manifest.activeRevision == commit.revision;
        select.Invoke(manifest, new object[] { "b", b }); Check("first complete profile pointer selected", Matches(b));
        select.Invoke(manifest, new object[] { "a", a }); Check("switch installs selected208 rather than previous48", Matches(a));
        select.Invoke(manifest, new object[] { "b", b }); Check("reverse switch installs selected48 rather than previous208", Matches(b));
        string before = JsonConvert.SerializeObject(manifest);
        bool rejected = false;
        try { select.Invoke(manifest, new object[] { "a", b }); } catch (TargetInvocationException exception) { rejected = exception.InnerException is InvalidOperationException; }
        Check("foreign profile revision rejected", rejected);
        Check("foreign rejection leaves all manifest fields unchanged", before == JsonConvert.SerializeObject(manifest));
        rejected = false;
        try { select.Invoke(manifest, new object[] { "a", null }); } catch (TargetInvocationException exception) { rejected = exception.InnerException is InvalidOperationException; }
        Check("committed profile cannot silently downgrade to legacy", rejected && before == JsonConvert.SerializeObject(manifest));
        select.Invoke(manifest, new object[] { "legacy", null });
        Check("legacy selection clears foreign commit and revision", manifest.activeProfileId == "legacy" && manifest.activeCommitId == string.Empty && manifest.activeRevision == 0);
        Check("revision inventory and profile identities preserved", manifest.commits.Count == 2 && manifest.profiles.Count == 3 && ReferenceEquals(manifest.commits[0], a));
        Write("profile-selection-contracts", new { utc = DateTime.UtcNow, passed = true, checks,
            runtimeMvid = typeof(YQProfileSaveSystem).Assembly.ManifestModule.ModuleVersionId, evidence = "NATIVE_DETACHED_SELECTION_CONTRACTS_NO_SAVE_WRITES" });
    }

    private static void ProfileSelectionState()
    {
        // note: Observe the canonical loaded pointer and the existing complete owner/disk admission without selecting, saving or publishing anything.
        var owner = YQProfileSaveSystem.Instance;
        if (!Application.isPlaying || owner == null) throw new InvalidOperationException("An active native profile owner is required.");
        var manifest = (YQProfileSaveSystem.ProfileManifest)typeof(YQProfileSaveSystem).GetField("_manifest", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        var latest = manifest.commits.Where(item => item != null && item.profileId == manifest.activeProfileId && item.status == "complete").OrderByDescending(item => item.revision).FirstOrDefault();
        var args = new object[] { null, null, null, null };
        bool admitted = (bool)typeof(YQProfileSaveSystem).GetMethod("TryRequireContinuationOwner", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        Write("profile-selection-state", new { utc = DateTime.UtcNow, runtimeMvid = typeof(YQProfileSaveSystem).Assembly.ManifestModule.ModuleVersionId,
            manifest.activeProfileId, manifest.activeCommitId, manifest.activeRevision,
            latestCommitId = latest?.commitId, latestRevision = latest?.revision,
            matchesLatest = latest != null && manifest.activeCommitId == latest.commitId && manifest.activeRevision == latest.revision,
            fullOwnerAdmitted = admitted, failure = args[3], evidence = "READ_ONLY_NATIVE_CANONICAL_SELECTION_AND_COMPLETE_OWNER_ADMISSION_NO_SAVE_OR_PUBLICATION" });
    }

    private static void ValidationCacheContracts()
    {
        // note: Test the native validator on detached current-plan copies; no mutation reaches live state or the save owner.
        var livePlan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        var copyOwner = typeof(YQSpatialMaterializationCompilerV2).GetNestedType("StartupPreparation", BindingFlags.NonPublic);
        var copyMethod = copyOwner.GetMethod("CopySavedPlan", BindingFlags.Static | BindingFlags.NonPublic);
        GeneratedWorldPlanRecord Copy() => (GeneratedWorldPlanRecord)copyMethod.Invoke(null, new object[] { livePlan });
        string before = JsonUtility.ToJson(livePlan);
        var checks = new List<string>();
        void Check(string label, bool passed) { if (!passed) throw new InvalidOperationException(label); checks.Add(label); }
        var detached = Copy();
        Check("complete accepted gate passes", YQSpatialPlanVersionRouter.TryValidateAcceptedV2(detached, out _));
        var cacheField = typeof(YQSpatialPlanVersionRouter).GetField("acceptedValidationSnapshot", BindingFlags.Static | BindingFlags.NonPublic);
        object proof = cacheField.GetValue(null);
        Check("identical detached input reuses successful proof", YQSpatialPlanVersionRouter.TryValidateAcceptedV2(Copy(), out _) && ReferenceEquals(proof, cacheField.GetValue(null)));
        detached.spatialPlanV2.blueprint.regions[0].radius += 1f;
        Check("unrevisioned deep geometry mutation rejects", !YQSpatialPlanVersionRouter.TryValidateAcceptedV2(detached, out _));
        detached = Copy(); detached.worldSeed += "-changed";
        Check("enclosing seed mutation rejects", !YQSpatialPlanVersionRouter.TryValidateAcceptedV2(detached, out _));
        detached = Copy(); detached.spatialPlan.semanticFingerprint += "-changed";
        Check("enclosing semantic identity mutation rejects", !YQSpatialPlanVersionRouter.TryValidateAcceptedV2(detached, out _));
        detached = Copy(); detached.spatialPlanV2.validationVersion = "unsupported-cache-test";
        Check("unsupported validation contract rejects", !YQSpatialPlanVersionRouter.TryValidateAcceptedV2(detached, out _));
        detached = Copy(); detached.spatialPlanV2.validatedContentHash += "-changed";
        Check("claimed acceptance hash mutation rejects", !YQSpatialPlanVersionRouter.TryValidateAcceptedV2(detached, out _));
        // note: The persisted base without an extension is another valid complete envelope; alternating it with the continued parent must retain both successful full gates.
        Check("continued parent refreshes its successful proof", YQSpatialPlanVersionRouter.TryValidateAcceptedV2(Copy(), out _));
        proof = cacheField.GetValue(null);
        detached = Copy(); detached.spatialPlanV2.acceptedContinuation = null;
        Check("detached base-only parent passes complete gate", YQSpatialPlanVersionRouter.TryValidateAcceptedV2(detached, out _));
        object baseProof = cacheField.GetValue(null);
        var previousCacheField = typeof(YQSpatialPlanVersionRouter).GetField("previousAcceptedValidationSnapshot", BindingFlags.Static | BindingFlags.NonPublic);
        Check("continued and base parents coexist without recompiling", YQSpatialPlanVersionRouter.TryValidateAcceptedV2(Copy(), out _) &&
            ReferenceEquals(proof, cacheField.GetValue(null)) && ReferenceEquals(baseProof, previousCacheField.GetValue(null)));
        // note: Full continuation proof reuse must reject actual corruption and nullable variant changes even when the parent claims are unchanged.
        var complete = typeof(YQSpatialPlanVersionRouter).GetMethod("TryValidateAcceptedV2AndContinuation", BindingFlags.Static | BindingFlags.NonPublic);
        bool Full(GeneratedWorldPlanRecord value) { object[] args = { value, null, null }; return (bool)complete.Invoke(null, args); }
        detached = Copy();
        Check("complete base and continuation proof passes", Full(detached));
        proof = cacheField.GetValue(null);
        Check("exact complete proof reuses without recompiling", Full(Copy()) && ReferenceEquals(proof, cacheField.GetValue(null)));
        detached = Copy(); detached.spatialPlanV2.acceptedContinuation.locations[0].anchor.x += 1f;
        Check("deep continuation geometry mutation rejects", !Full(detached));
        detached = Copy(); detached.spatialPlanV2.acceptedContinuation.contentHash += "-corrupt";
        Check("continuation checksum claim mutation rejects", !Full(detached));
        detached = Copy();
        var changed = detached.spatialPlanV2.acceptedContinuation.locations[0];
        if (changed.settlement == null) changed.settlement = new GeneratedSettlementRecord();
        else changed.encampment = new GeneratedEncampmentRecord();
        Check("absent variant cannot alias an empty inline record", !Full(detached));
        Check("live saved plan unchanged", string.Equals(before, JsonUtility.ToJson(livePlan), StringComparison.Ordinal));
        Write("validation-cache-contracts", new { utc = DateTime.UtcNow, checks, passed = true,
            evidence = "NATIVE_FULL_VALIDATOR_DETACHED_CURRENT_PLAN_NO_SAVE_MUTATION" });
    }

    private static void AdmissionChainContracts()
    {
        // note: Exercise only detached parent records and prompt strings; no model request, accepted content or profile state is mutated by these controls.
        var livePlan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        if (livePlan == null) throw new InvalidOperationException("Current accepted plan is required.");
        string before = JsonUtility.ToJson(livePlan);
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var service = typeof(YQWorldGenerationService);
        var gate = service.GetMethod("TryAdmitFrontierBriefParent", flags);
        var oldPrompt = service.GetMethod("BuildFrontierLocationBriefPrompt", flags);
        var sharedPrompt = service.GetMethod("BuildFrontierLocationBriefPromptWithPrepared", flags);
        var schemaMethod = service.GetMethod("BuildFrontierLocationBriefSchema", flags);
        var copyOwner = typeof(YQSpatialMaterializationCompilerV2).GetNestedType("StartupPreparation", flags);
        var copyMethod = copyOwner.GetMethod("CopySavedPlan", flags);
        GeneratedWorldPlanRecord Copy() => (GeneratedWorldPlanRecord)copyMethod.Invoke(null, new object[] { livePlan });
        bool Admit(GeneratedWorldPlanRecord plan, YQPreparedSpatialMaterializationV2 expected, out YQPreparedSpatialMaterializationV2 prepared)
        {
            object[] args = { plan, expected, null };
            bool result = (bool)gate.Invoke(null, args); prepared = (YQPreparedSpatialMaterializationV2)args[2]; return result;
        }
        var checks = new List<string>();
        void Check(string label, bool passed) { if (!passed) throw new InvalidOperationException(label); checks.Add(label); }
        Check("public parent path performs complete admission", Admit(livePlan, null, out var currentPrepared));
        Check("expected current projection passes full admission", Admit(livePlan, currentPrepared, out var samePrepared) && ReferenceEquals(currentPrepared, samePrepared));
        var foreign = (YQPreparedSpatialMaterializationV2)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(YQPreparedSpatialMaterializationV2));
        Check("foreign expected identity rejects", !Admit(livePlan, foreign, out _));
        var instance = YQWorldGenerationService.Instance;
        int callbacks = 0;
        var internalEntry = service.GetMethod("TryRequestFrontierLocationBriefForPreparedParent", BindingFlags.Instance | BindingFlags.NonPublic);
        object[] nullExpectedArgs = { WorldStateManager.Instance.State, null, null,
            (Action<GeneratedSpatialContinuationLocationV2Record, string>)((_, __) => callbacks++), 0L };
        Check("internal null expected identity rejects once before scheduling", instance != null &&
            !(bool)internalEntry.Invoke(instance, nullExpectedArgs) && (long)nullExpectedArgs[4] == 0L && callbacks == 1);
        callbacks = 0;
        Check("public invalid world rejects once without scheduling", instance != null &&
            !instance.TryRequestFrontierLocationBrief(null, null, (_, __) => callbacks++, out long publicRequest) && publicRequest == 0 && callbacks == 1);
        var detached = Copy();
        Check("same detached base owner warms complete admission", Admit(detached, null, out _));
        detached.spatialPlanV2.blueprint.regions[0].radius += 1f;
        Check("same-owner deep base mutation rejects without expected-identity constraint", !Admit(detached, null, out _));
        detached = Copy();
        Check("same detached continuation owner warms complete admission", Admit(detached, null, out _));
        detached.spatialPlanV2.acceptedContinuation.locations[0].anchor.x += 1f;
        Check("same-owner deep continuation mutation rejects without expected-identity constraint", !Admit(detached, null, out _));
        var factions = new List<GeneratedFactionPlanRecord>(livePlan.factions);
        string[] ids = new string[factions.Count + 1]; ids[0] = string.Empty;
        for (int index = 0; index < factions.Count; index++) ids[index + 1] = factions[index].factionId;
        foreach (bool baseOnly in new[] { false, true })
        {
            var plan = Copy(); if (baseOnly) plan.spatialPlanV2.acceptedContinuation = null;
            Check((baseOnly ? "base-only" : "continued") + " detached complete admission", Admit(plan, null, out var projection));
            foreach (YQSiteKindV2 kind in new[] { YQSiteKindV2.Settlement, YQSiteKindV2.HostileSite, YQSiteKindV2.PointOfInterest })
            {
                var candidate = livePlan.spatialPlanV2.acceptedContinuation.locations.Find(value => value.anchor.kind == kind);
                if (candidate == null) throw new InvalidOperationException("Missing current-plan kind: " + kind);
                var region = plan.regions.Find(value => value.regionId == candidate.anchor.parentRegionId);
                var schema = (JObject)schemaMethod.Invoke(null, new object[] { kind, region.assetStyleKey, ids });
                string legacy = (string)oldPrompt.Invoke(null, new object[] { plan, region, factions, candidate, schema });
                string shared = (string)sharedPrompt.Invoke(null, new object[] { plan, region, factions, candidate, schema, projection });
                Check((baseOnly ? "base-only " : "continued ") + kind + " exact prompt parity", string.Equals(legacy, shared, StringComparison.Ordinal));
            }
        }
        Check("live accepted plan unchanged", string.Equals(before, JsonUtility.ToJson(livePlan), StringComparison.Ordinal));
        Write("admission-chain-contracts", new { utc = DateTime.UtcNow, runtimeMvid = typeof(YQWorldGenerationService).Assembly.ManifestModule.ModuleVersionId,
            evidence = "NATIVE_DETACHED_CURRENT_PARENT_AND_PROMPT_CONTROLS_NO_SAVE_OR_MODEL_MUTATION", passed = true, checks });
    }

    private static void PrivateUnionContracts()
    {
        // note: Differential checks use an explicitly supplied detached fixture; no active profile, world, provider or accepted save is mutated.
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var builder = typeof(YQGeneratedWorldRuntimeBuilder);
        var settings = (JsonSerializerSettings)builder.GetField("FrontierConstructionJsonSettings", flags).GetValue(null);
        var plan = JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText(Output + "/private-union-input.json"), settings);
        var previous = plan.spatialPlanV2.acceptedContinuation;
        var staged = JsonConvert.DeserializeObject<GeneratedSpatialContinuationLocationV2Record>(JsonConvert.SerializeObject(previous.locations[0], settings), settings);
        string previousJson = JsonConvert.SerializeObject(previous, settings), stagedJson = JsonConvert.SerializeObject(staged, settings);
        string footprint = YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(plan.spatialPlanV2);
        string proposal = staged.sourceContentHash;
        var build = builder.GetMethod("BuildFrontierAcceptedBody", flags);
        var checks = new List<string>();
        void Check(string name, bool result) { if (!result) throw new InvalidOperationException(name); checks.Add(name); }
        string Candidate(string prior, System.Threading.CancellationToken cancellation) => (string)build.Invoke(null,
            new object[] { prior, stagedJson, plan.worldSeed, plan.spatialPlanV2.contentHash, footprint, proposal, cancellation });
        string Legacy(string prior)
        {
            // note: Retain the previous exact synchronous algorithm as the differential oracle, including proof enumeration and accepted JSON rendering.
            var accepted = prior == null ? new GeneratedSpatialContinuationV2Record { worldSeed = plan.worldSeed,
                parentSpatialContentHash = plan.spatialPlanV2.contentHash, parentMemberFootprintHash = footprint }
                : JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(prior, settings);
            var location = JsonConvert.DeserializeObject<GeneratedSpatialContinuationLocationV2Record>(stagedJson, settings);
            accepted.revision = checked((prior == null ? 0 : JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(prior, settings).revision) + 1);
            location.revision = accepted.revision; location.state = YQSpatialContinuationStateV2.Accepted;
            location.contentHash = location.validatedContentHash = string.Empty; location.proofClaims.Clear();
            string payload = YQSpatialContinuationHasherV2.ComputeLocationPayloadHash(location);
            for (int kind = 1; kind <= 5; kind++) location.proofClaims.Add(new GeneratedSpatialContinuationProofV2Record {
                kind = (YQSpatialContinuationProofKindV2)kind, outcome = YQSpatialContinuationProofOutcomeV2.Passed,
                subjectSiteId = location.anchor.siteId, subjectPayloadHash = payload, ownerValidationVersion = "frontier_construction_owner_v1",
                evidenceId = location.contentId + ":" + kind, evidenceHash = kind == 1 ? proposal : YQStateContract.Sha256Hex(payload + "|" + kind) });
            location.contentHash = location.validatedContentHash = YQSpatialContinuationHasherV2.ComputeLocationContentHash(location);
            accepted.locations.Add(location); accepted.state = YQSpatialContinuationStateV2.Accepted;
            accepted.contentHash = accepted.validatedContentHash = YQSpatialContinuationHasherV2.ComputeContentHash(accepted);
            return JsonConvert.SerializeObject(accepted, settings);
        }
        var none = System.Threading.CancellationToken.None;
        string baseline = Legacy(previousJson), body = Candidate(previousJson, none);
        Check("continued union exact JSON and checksum parity", body == baseline);
        // note: Exercise the actual worker thread, not merely the pure helper on the Editor thread; its field serializer must remain safe on privately owned records.
        var threaded = System.Threading.Tasks.Task.Run(() => Candidate(previousJson, none));
        Check("real worker completes within detached diagnostic bound", threaded.Wait(TimeSpan.FromSeconds(10)));
        Check("real worker exact JSON and checksum parity", threaded.GetAwaiter().GetResult() == baseline);
        using (var cancellation = new System.Threading.CancellationTokenSource())
        using (var started = new System.Threading.ManualResetEventSlim())
        {
            var retiring = System.Threading.Tasks.Task.Run(() => { started.Set(); return Candidate(previousJson, cancellation.Token); });
            Check("retiring worker started", started.Wait(TimeSpan.FromSeconds(2)));
            System.Threading.Thread.Sleep(5);
            Check("retirement requested before worker completion", !retiring.IsCompleted);
            cancellation.Cancel(); bool rejected = false;
            try { if (!retiring.Wait(TimeSpan.FromSeconds(10))) throw new InvalidOperationException("Retiring worker exceeded diagnostic bound."); }
            catch (AggregateException error) when (error.Flatten().InnerExceptions.All(value => value.GetBaseException() is OperationCanceledException)) { rejected = true; }
            Check("started worker cancellation returns no accepted body", rejected);
        }
        Check("first union exact JSON and checksum parity", Candidate(null, none) == Legacy(null));
        var acceptedBody = JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(body, settings);
        Check("revision and proof claim enumeration preserved", acceptedBody.revision == previous.revision + 1 &&
            acceptedBody.locations.Last().proofClaims.Select(value => (int)value.kind).SequenceEqual(Enumerable.Range(1, 5)));
        Check("source previous and staged records are unchanged", previousJson == JsonConvert.SerializeObject(previous, settings) && stagedJson == JsonConvert.SerializeObject(staged, settings));
        previous.locations[0].anchor.x += 1f;
        Check("captured union independent of later previous mutation", Candidate(previousJson, none) == baseline);
        var compare = typeof(YQProfileCommitStore).GetMethod("JsonEqualsCaptured", flags);
        Check("exact previous-source guard rejects deep unrevisioned mutation", !(bool)compare.Invoke(null, new object[] { previous, previousJson, settings }));
        staged.anchor.x += 1f;
        Check("captured union independent of later staged mutation", Candidate(previousJson, none) == baseline);
        Check("exact staged-source guard rejects deep mutation", !(bool)compare.Invoke(null, new object[] { staged, stagedJson, settings }));
        using (var cancellation = new System.Threading.CancellationTokenSource())
        {
            cancellation.Cancel(); bool rejected = false;
            try { Candidate(previousJson, cancellation.Token); }
            catch (TargetInvocationException error) when (error.InnerException is OperationCanceledException) { rejected = true; }
            Check("cancelled preparation issues no body", rejected);
        }
        var first = JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(body, settings);
        first.locations[0].anchor.x += 1f;
        Check("independent decoded bodies cannot mutate retained body", Candidate(previousJson, none) == baseline);
        var tokenType = builder.GetNestedType("FrontierConstructionAcceptance", BindingFlags.Public);
        var issue = tokenType.GetMethod("IssueCaptured", flags | BindingFlags.Public);
        var detachedOwner = new WorldState { generatedWorldPlan = plan };
        bool badIssuer = false;
        try { issue.Invoke(null, new object[] { detachedOwner, "prior", body, new object() }); }
        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { badIssuer = true; }
        Check("foreign issuer cannot promote a captured worker body", badIssuer);
        object issuer = builder.GetField("FrontierConstructionIssuer", flags).GetValue(null);
        object token = issue.Invoke(null, new object[] { detachedOwner, "prior", body, issuer });
        object[] admission = { detachedOwner, null, null };
        Check("late body cannot admit a retired detached world", !(bool)tokenType.GetMethod("TryValidateForPublication", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(token, admission));
        Write("private-union-contracts", new { utc = DateTime.UtcNow, checks, passed = true, bodyCharacters = body.Length,
            runtimeMvid = builder.Module.ModuleVersionId, evidence = "NATIVE_EDITOR_DETACHED_CURRENT_FIXTURE_EXACT_BODY_MUTATION_CANCELLATION_ISSUER_CONTROLS_NO_PUBLICATION" });
    }

    private static void IdentityScreenContracts()
    {
        // note: Test the new rejection-only screen against the unchanged full identity API on detached saved inputs, with no model/world/profile mutation.
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var settings = (JsonSerializerSettings)typeof(YQGeneratedWorldRuntimeBuilder).GetField("FrontierConstructionJsonSettings", flags).GetValue(null);
        string input = File.ReadAllText(Output + "/private-union-input.json");
        GeneratedWorldPlanRecord Copy() => JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(input, settings);
        var resolver = typeof(YQSpatialMaterializationResolverV2);
        var full = resolver.GetMethod("TryGetAcceptedContinuationIdentity", flags);
        var screen = resolver.GetMethod("TryScreenAcceptedContinuationIdentityForPreparedRequest", flags);
        var serviceGate = typeof(YQWorldGenerationService).GetMethod("TryAdmitFrontierBriefParent", flags);
        bool Read(MethodInfo method, GeneratedWorldPlanRecord plan, out object[] values)
        { values = new object[] { plan, null, null, null }; return (bool)method.Invoke(null, values); }
        var checks = new List<string>();
        void Check(string label, bool passed) { if (!passed) throw new InvalidOperationException(label); checks.Add(label); }
        var plan = Copy();
        Check("valid continued full identity passes", Read(full, plan, out var fullValues));
        Check("screen valid continued reference/fingerprint equal full API", Read(screen, plan, out var screenValues) &&
            ReferenceEquals(fullValues[1], screenValues[1]) && (string)fullValues[2] == (string)screenValues[2]);
        foreach (bool absent in new[] { true, false })
        {
            plan = Copy(); if (absent) plan.spatialPlanV2.acceptedContinuation = null;
            else plan.spatialPlanV2.acceptedContinuation.state = YQSpatialContinuationStateV2.Staged;
            Check((absent ? "absent" : "nonaccepted") + " continuation keeps original empty identity semantics",
                Read(full, plan, out fullValues) && Read(screen, plan, out screenValues) &&
                fullValues[1] == null && screenValues[1] == null && (string)fullValues[2] == string.Empty && (string)screenValues[2] == string.Empty);
        }
        foreach (var mutation in new (string name, Action<GeneratedWorldPlanRecord> change)[] {
            ("schema", value => value.spatialPlanV2.acceptedContinuation.schemaVersion += "-changed"),
            ("generation", value => value.spatialPlanV2.acceptedContinuation.generationVersion = "unsupported-screen-test"),
            ("validation", value => value.spatialPlanV2.acceptedContinuation.validationVersion = "unsupported-screen-test"),
            ("algorithm", value => value.spatialPlanV2.acceptedContinuation.hashAlgorithm = "unsupported-screen-test"),
            ("seed", value => value.spatialPlanV2.acceptedContinuation.worldSeed += "-changed"),
            ("parent", value => value.spatialPlanV2.acceptedContinuation.parentSpatialContentHash += "-changed"),
            ("revision", value => value.spatialPlanV2.acceptedContinuation.revision = 0),
            ("null locations", value => value.spatialPlanV2.acceptedContinuation.locations = null),
            ("empty locations", value => value.spatialPlanV2.acceptedContinuation.locations.Clear()),
            ("null errors", value => value.spatialPlanV2.acceptedContinuation.validationErrors = null),
            ("errors", value => value.spatialPlanV2.acceptedContinuation.validationErrors.Add("screen-test")),
            ("empty hash", value => value.spatialPlanV2.acceptedContinuation.contentHash = string.Empty),
            ("hash claim", value => value.spatialPlanV2.acceptedContinuation.validatedContentHash += "-changed"),
            ("enclosing seed", value => value.worldSeed += "-changed"),
            ("enclosing semantic identity", value => value.spatialPlan.semanticFingerprint += "-changed") })
        {
            plan = Copy(); mutation.change(plan);
            Check(mutation.name + " cheap rejection/output/failure parity", !Read(full, plan, out fullValues) &&
                !Read(screen, plan, out screenValues) && fullValues[1] == null && screenValues[1] == null &&
                (string)fullValues[2] == string.Empty && (string)screenValues[2] == string.Empty && (string)fullValues[3] == (string)screenValues[3]);
        }
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "en-US", "fr-FR", "ar-SA" })
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
                plan = Copy(); var accepted = plan.spatialPlanV2.acceptedContinuation;
                Check(culture + " exact invariant fingerprint", Read(screen, plan, out screenValues) &&
                    (string)screenValues[2] == accepted.schemaVersion + "|" + accepted.revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + accepted.contentHash);
            }
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = originalCulture; }
        foreach (bool continuationMutation in new[] { false, true })
        {
            plan = Copy();
            Check((continuationMutation ? "continuation" : "base") + " exact same-object full proof warms", Read(full, plan, out _));
            if (continuationMutation) plan.spatialPlanV2.acceptedContinuation.locations[0].anchor.x += 1f;
            else plan.spatialPlanV2.blueprint.regions[0].radius += 1f;
            Check((continuationMutation ? "continuation" : "base") + " screen grants no deep acceptance", Read(screen, plan, out _));
            Check((continuationMutation ? "continuation" : "base") + " full API rejects deep mutation with null/empty outputs",
                !Read(full, plan, out fullValues) && fullValues[1] == null && (string)fullValues[2] == string.Empty);
            object[] gateValues = { plan, null, null };
            Check((continuationMutation ? "continuation" : "base") + " service complete admission independently rejects deep mutation",
                !(bool)serviceGate.Invoke(null, gateValues));
        }
        Write("identity-screen-contracts", new { utc = DateTime.UtcNow, passed = true, checks,
            runtimeMvid = typeof(YQGeneratedWorldRuntimeBuilder).Assembly.ManifestModule.ModuleVersionId,
            evidence = "NATIVE_EDITOR_DETACHED_SCREEN_FULL_IDENTITY_AND_SERVICE_DEEP_PROOF_PARITY_NO_MODEL_OR_PUBLICATION" });
    }

    private static void CapturedCopyContracts()
    {
        // note: Compare the original copy with the worker-decoded carrier on detached inputs only, including null branches and ownership transfer.
        var statics = BindingFlags.Static | BindingFlags.NonPublic;
        var instances = BindingFlags.Instance | BindingFlags.NonPublic;
        var builder = typeof(YQGeneratedWorldRuntimeBuilder);
        var settings = (JsonSerializerSettings)builder.GetField("FrontierConstructionJsonSettings", statics).GetValue(null);
        var plan = JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText(Output + "/private-union-input.json"), settings);
        string planJson = JsonConvert.SerializeObject(new { plan.worldSeed, plan.spatialPlan, plan.settlements, plan.encampments }, settings);
        string parentJson = JsonConvert.SerializeObject(plan.spatialPlanV2, settings);
        string acceptedJson = JsonConvert.SerializeObject(plan.spatialPlanV2.acceptedContinuation, settings);
        string before = JsonConvert.SerializeObject(plan, settings);
        var preparation = typeof(YQSpatialMaterializationCompilerV2).GetNestedType("StartupPreparation", BindingFlags.NonPublic);
        var carrier = preparation.GetNestedType("CapturedCompilerInputs", BindingFlags.NonPublic);
        var decode = carrier.GetMethod("Decode", statics); var take = carrier.GetMethod("Take", instances);
        var copy = preparation.GetMethod("CopyCompilerInputs", statics);
        var checks = new List<string>();
        void Check(string name, bool result) { if (!result) throw new InvalidOperationException(name); checks.Add(name); }
        object Decode(string accepted, System.Threading.CancellationToken cancellation) => decode.Invoke(null, new object[] { planJson, parentJson, accepted, cancellation });
        GeneratedWorldPlanRecord Take(object value) => (GeneratedWorldPlanRecord)take.Invoke(value, null);
        var none = System.Threading.CancellationToken.None;
        var original = (GeneratedWorldPlanRecord)copy.Invoke(null, new object[] { plan });
        original.spatialPlanV2.acceptedContinuation = JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(acceptedJson, settings);
        var work = System.Threading.Tasks.Task.Run(() => Decode(acceptedJson, none));
        Check("captured worker completes within diagnostic bound", work.Wait(TimeSpan.FromSeconds(10)));
        var owned = work.GetAwaiter().GetResult(); var actual = Take(owned);
        Check("exact original compiler copy JSON parity", JsonConvert.SerializeObject(original, settings) == JsonConvert.SerializeObject(actual, settings));
        Check("exact Unity source snapshot parity", JsonUtility.ToJson(original) == JsonUtility.ToJson(actual));
        Check("null variant identity parity", actual.spatialPlanV2.acceptedContinuation.locations.Zip(original.spatialPlanV2.acceptedContinuation.locations,
            (left, right) => (left.settlement == null) == (right.settlement == null) && (left.encampment == null) == (right.encampment == null) &&
                (left.pointOfInterest == null) == (right.pointOfInterest == null) && (left.compositionLayout == null) == (right.compositionLayout == null)).All(value => value));
        bool twice = false;
        try { Take(owned); } catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { twice = true; }
        Check("carrier rejects second consumption", twice);
        var independent = Take(Decode(acceptedJson, none));
        actual.spatialPlanV2.blueprint.regions[0].radius += 1f;
        Check("decoded bodies have no shared mutable blueprint", independent.spatialPlanV2.blueprint.regions[0].radius == original.spatialPlanV2.blueprint.regions[0].radius);
        Check("all current source records remain unchanged", before == JsonConvert.SerializeObject(plan, settings));
        plan.spatialPlanV2.acceptedContinuation.locations[0].anchor.x += 1f;
        var capturedAgain = Take(Decode(acceptedJson, none));
        Check("immutable captures ignore later source mutation", JsonUtility.ToJson(capturedAgain) == JsonUtility.ToJson(original));
        var absent = Take(Decode("null", none));
        Check("absent continuation remains absent", absent.spatialPlanV2.acceptedContinuation == null);
        Check("existing live overload remains available", preparation.GetMethod("PrepareRoutine", statics) != null);
        using (var cancellation = new System.Threading.CancellationTokenSource())
        {
            cancellation.Cancel(); bool rejected = false;
            try { Decode(acceptedJson, cancellation.Token); }
            catch (TargetInvocationException error) when (error.InnerException is OperationCanceledException) { rejected = true; }
            Check("precancelled decode issues no carrier", rejected);
        }
        using (var cancellation = new System.Threading.CancellationTokenSource())
        using (var started = new System.Threading.ManualResetEventSlim())
        {
            var retiring = System.Threading.Tasks.Task.Run(() => { started.Set(); return Decode(acceptedJson, cancellation.Token); });
            Check("copy worker started", started.Wait(TimeSpan.FromSeconds(2)));
            System.Threading.Thread.Sleep(5); Check("copy retirement requested before completion", !retiring.IsCompleted);
            cancellation.Cancel(); bool rejected = false;
            try { if (!retiring.Wait(TimeSpan.FromSeconds(10))) throw new InvalidOperationException("Retired copy exceeded diagnostic bound."); }
            catch (AggregateException error) when (error.Flatten().InnerExceptions.All(value => value.GetBaseException() is OperationCanceledException)) { rejected = true; }
            Check("started copy cancellation issues no carrier", rejected);
        }
        Write("captured-copy-contracts", new { utc = DateTime.UtcNow, checks, passed = true, runtimeMvid = builder.Module.ModuleVersionId,
            evidence = "NATIVE_EDITOR_DETACHED_EXACT_COMPILER_COPY_NULL_OWNERSHIP_CANCELLATION_CONTROLS_NO_PUBLICATION" });
    }

    private static void PerformanceContracts()
    {
        // note: Exercise process identity and acknowledgement admission without starting a model, changing a live profile or claiming runtime speed.
        var checks = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        try
        {
            var key = typeof(LLMClient).GetMethod("BuildLlamaResidencyKey", BindingFlags.Static | BindingFlags.NonPublic);
            var args = typeof(LlamaCppServerProcess).GetMethod("TryBuildArguments", BindingFlags.Static | BindingFlags.NonPublic);
            const string help = "--model --host --port --ctx-size --parallel --n-gpu-layers --fit --device --flash-attn --cache-type-k --cache-type-v --reasoning-budget --threads --threads-batch --batch-size --ubatch-size --prio --poll --no-cache-prompt --cache-ram --ctx-checkpoints --checkpoint-min-step --log-verbosity --no-webui --reasoning --no-cache-idle-slots --fit-target";
            object[] loading = { config, help, null, null, false }, playing = { config, help, null, null, true };
            Check((bool)args.Invoke(null, loading) && (bool)args.Invoke(null, playing), "Both placement policies admit valid launch arguments");
            Check(Equals(loading[2], playing[2]), "Reveal does not alter real engine arguments");
            string before = (string)key.Invoke(null, new object[] { config, false });
            Check(before == (string)key.Invoke(null, new object[] { config, true }), "Reveal reuses identical resident model");
            config.contextSizeTokens += 512;
            Check(before != (string)key.Invoke(null, new object[] { config, true }), "Actual context change still requires model transition");
            var idle = typeof(LlamaCppServerProcess).GetMethod("SlotsConfirmIdle", BindingFlags.Static | BindingFlags.NonPublic);
            bool Idle(string json) => (bool)idle.Invoke(null, new object[] { json });
            Check(Idle("[{\"id\":0,\"is_processing\":false}]"), "Owned single idle slot can acknowledge cancellation");
            foreach (string rejected in new[] { "[]", "{}", "null", "bad", "[{\"id\":0}]", "[{\"id\":0,\"is_processing\":true}]", "[{\"id\":0,\"is_processing\":\"false\"}]", "[{\"id\":1,\"is_processing\":false}]", "[{\"id\":0,\"is_processing\":true,\"is_processing\":false}]", "[{\"id\":0,\"is_processing\":false},{\"id\":1,\"is_processing\":false}]" })
                Check(!Idle(rejected), "Unproven cancellation requires fallback: " + rejected);
            // note: A real accepted saved plan stays detached; deep edits must invalidate the warmup proof even when persisted hashes/revisions are left unchanged.
            var settings = new JsonSerializerSettings { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
            var plan = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(Output + "/world-r98-input.json"), settings).generatedWorldPlan;
            var parent = plan.spatialPlanV2;
            var envelope = parent.acceptedContinuation;
            var warmup = typeof(YQCompiledWorldSiteInstance).GetMethod("TryValidateContinuationWarmupIdentity", BindingFlags.Static | BindingFlags.NonPublic);
            object[] admission = { plan, parent, envelope, null, null, null };
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Check((bool)warmup.Invoke(null, admission), "Full accepted fixture warmup validation: " + admission[4]);
            double fullValidationMs = timer.Elapsed.TotalMilliseconds;
            string snapshot = JsonUtility.ToJson(parent);
            object[] reuse = { plan, parent, envelope, null, null, snapshot };
            timer.Restart();
            Check((bool)warmup.Invoke(null, reuse) && Equals(admission[3], reuse[3]), "Exact validated contents reuse the same proof");
            double reuseMs = timer.Elapsed.TotalMilliseconds;
            float radius = parent.blueprint.regions[0].radius;
            parent.blueprint.regions[0].radius += 1f;
            Check(!(bool)warmup.Invoke(null, reuse), "Deep parent mutation cannot reuse warmup proof");
            parent.blueprint.regions[0].radius = radius;
            envelope.revision++;
            Check(!(bool)warmup.Invoke(null, reuse), "Changed continuation cannot reuse warmup proof");
            envelope.revision--;
            string seed = plan.worldSeed; plan.worldSeed += "_changed";
            Check(!(bool)warmup.Invoke(null, reuse), "Changed enclosing seed cannot reuse warmup proof");
            plan.worldSeed = seed;
            Check((bool)warmup.Invoke(null, reuse), "Restored original detached input remains reusable");
            // note: Compile a separate detached origin to exercise preflight proof admission and deep mutation rejection without installing a live world.
            var fixture = (GeneratedWorldPlanRecord)typeof(YQSemanticWorldAuthorityTests).GetMethod("BuildFixture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "76603739" });
            YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(fixture);
            Check(YQSpatialBlueprintCompilerV2.TryCompile(fixture, out var artifact, out string proofFailure), "Compile detached preflight fixture: " + proofFailure);
            fixture.spatialPlanV2 = artifact;
            Check(YQSpatialMaterializationResolverV2.TryGetPrepared(fixture, out var projection, out proofFailure), "Prepare detached preflight fixture: " + proofFailure);
            var proofType = typeof(YQCompiledWorldSiteInstance).GetNestedType("ContinuationReplayProof", BindingFlags.NonPublic);
            object[] createArgs = { fixture, projection, null };
            var proof = proofType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, createArgs);
            Check(proof != null, "Validated preflight proof created: " + createArgs[2]);
            var matches = proofType.GetMethod("Matches", BindingFlags.Instance | BindingFlags.NonPublic);
            bool Matches() => (bool)matches.Invoke(proof, new object[] { fixture, projection });
            Check(Matches(), "Unchanged preflight proof admitted");
            float fixtureRadius = artifact.blueprint.regions[0].radius;
            artifact.blueprint.regions[0].radius += 1f;
            Check(!Matches(), "Preflight rejects deep parent edits without a revision bump");
            artifact.blueprint.regions[0].radius = fixtureRadius;
            string fixtureSeed = fixture.worldSeed; fixture.worldSeed += "_changed";
            Check(!Matches(), "Preflight rejects changed enclosing seed");
            fixture.worldSeed = fixtureSeed;
            Check(!(bool)matches.Invoke(proof, new object[] { fixture, null }), "Preflight rejects another projection");
            Check(Matches(), "Restored exact preflight contents remain reusable");
            bool disposed = false;
            object marker = typeof(YQGeneratedWorldPerformanceDirector).GetField("LoadingWorkBoundaryToken", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var realWait = new WaitForSecondsRealtime(.01f);
            System.Collections.IEnumerator Work() { try { for (int i = 0; i < 10; i++) yield return marker; yield return null; yield return realWait; } finally { disposed = true; } }
            Exception runnerFailure = null;
            var runner = YQGeneratedWorldRuntimeBuilder.RunOwnedGenerationRoutine(Work(), error => runnerFailure = error);
            bool sawNull = false, sawWait = false, leakedMarker = false;
            while (runner.MoveNext()) { sawNull |= runner.Current == null; sawWait |= ReferenceEquals(runner.Current, realWait); leakedMarker |= ReferenceEquals(runner.Current, marker); }
            Check(disposed && runnerFailure == null && sawNull && sawWait && !leakedMarker, "Batching preserves explicit frame waits, custom waits and cleanup");
            Write("performance-contracts", new { utc = DateTime.UtcNow, passed = true, checks,
                fullValidationMs, reuseMs,
                runtimeMvid = typeof(LLMClient).Assembly.ManifestModule.ModuleVersionId, evidence = "Detached production argument, identity and idle acknowledgement contracts" });
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }

    private static void ConversationContracts()
    {
        // note: Exercise real state/objective/offer methods on detached records; this fixture never installs a profile or writes a game save.
        var checks = new List<string>();
        void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); checks.Add(label); }
        var player = new PlayerState(); player.EnsureCollections();
        var world = new WorldState(); world.EnsureCollections();
        world.npcs.Add(new WorldState.NpcRecord { npcId = "npc_test", name = "Ada", factionId = "faction_test", affinityToPlayer = .6f });
        var quest = new QuestRecord { questId = "speak_fixture", name = "Ask Ada", status = "active",
            objectives = new List<QuestObjectiveRecord> { new QuestObjectiveRecord { type = "talk_to_npc", targetId = "npc_test", requiredCount = 1 } } };
        Check(!YQQuestCompletionDirector.EvaluateObjectives(player, quest, out _), "Opening/no interaction does not complete talk");
        YQQuestCompletionDirector.RecordCompletedDialogue(player, "npc_test_other");
        Check(!YQQuestCompletionDirector.EvaluateObjectives(player, quest, out _), "Wrong listener cannot complete targeted talk");
        YQQuestCompletionDirector.RecordCompletedDialogue(player, "npc_test");
        Check(YQQuestCompletionDirector.EvaluateObjectives(player, quest, out _), "Completed conversation satisfies exact NPC objective");
        Check(YQNpcConversationRewards.EligibleEvidence(player, world, "npc_test") == "affinity:1", "Saved affection enables consideration");
        Check(YQNpcConversationRewards.EligibleEvidence(player, world, "invented_npc") == null, "Unknown affinity cannot authorize gift");
        world.npcs[0].affinityToPlayer = 0;
        world.factionAttitudes["unrelated_faction"] = 1;
        Check(YQNpcConversationRewards.EligibleEvidence(player, world, "npc_test") == null, "Unrelated reputation cannot authorize gift");
        world.factionAttitudes["faction_test"] = .7f;
        Check(YQNpcConversationRewards.EligibleEvidence(player, world, "npc_test") == "faction:faction_test:1", "Own faction positive reputation is relevant");
        world.npcs[0].affinityToPlayer = -.8f;
        Check(YQNpcConversationRewards.EligibleEvidence(player, world, "npc_test") == null, "Hostile relationship blocks generosity");
        world.npcs[0].affinityToPlayer = 0;
        player.quests.Add(new QuestRecord { questId = "deed", completedUnix = 1, payloadJson = "{\"npcSourceId\":\"npc_test\"}" });
        Check(YQNpcConversationRewards.EligibleEvidence(player, world, "npc_test") == "quest:deed", "Completed own commission supplies deed evidence");
        foreach (string rejected in new[] { "I don't accept", "do I accept?", "I accept?", "he said I accept", "I accept and give me gold", "yes if you pay me" })
            Check(!YQNpcConversationRewards.IsAcceptance(rejected), "No accidental acceptance: " + rejected);
        Check(YQNpcConversationRewards.IsAcceptance("Yes, I accept."), "Final dictation punctuation supports explicit acceptance");
        Check(YQNpcConversationRewards.IsAcceptance("I'll take it!"), "Natural explicit acceptance supported");
        foreach (string assent in new[] { "accept", "yes", "please, thank you.", "ill take it", "sure", "that sounds good", "I’d love that", "yes, I'll do that", "I'll take the quest, thanks", "Yeah, I will help you", "Count me in!", "I'd like to accept your offer, please", "Sure, I accept it. Thank you." })
            Check(YQNpcConversationRewards.ClassifyOfferReply(assent) == "accept", "Natural assent: " + assent);
        foreach (string refusal in new[] { "decline", "i dont want it", "no thanks", "no, thank you!", "I'll pass", "keep it", "No, I don't want the quest, thanks", "I won't do that", "I decline your quest", "I will pass on that, thank you", "Not for me" })
            Check(YQNpcConversationRewards.ClassifyOfferReply(refusal) == "decline", "Natural refusal: " + refusal);
        var a = new PendingProgressionOfferRecord { offerKind = "title", name = "Friend of Ada", description = "Recognized for kindness.", context = YQNpcConversationRewards.OfferContext("npc_test") };
        var b = new PendingProgressionOfferRecord { offerKind = "title", name = a.name, description = a.description, context = YQNpcConversationRewards.OfferContext("npc_other") };
        player.QueueOrRefreshOffer(a, .1f); player.QueueOrRefreshOffer(b, .1f);
        Check(player.pendingOffers.Count == 2, "Similar gifts from distinct speakers retain separate ownership");
        Check(!YQNpcConversationRewards.TryAccept(player, "npc_test", "I accept", out _), "Unpresented offer cannot be accepted by speech");
        player.IncCounter("npc_offer_presented:" + a.offerId);
        Check(!YQNpcConversationRewards.TryAccept(player, "npc_other", "I accept", out _), "Cannot accept another NPC's presented gift");
        Check(YQNpcConversationRewards.TryAccept(player, "npc_test", "I accept", out _) && a.offerState == "accepted", "Spoken acceptance uses existing title award path");
        Check(!YQNpcConversationRewards.TryAccept(player, "npc_test", "I accept", out _), "Repeated acceptance cannot grant twice");
        var classOffer = new PendingProgressionOfferRecord { offerKind = "class", name = "River Scout", description = "A practical wayfinding discipline.", context = YQNpcConversationRewards.OfferContext("npc_test") };
        player.QueueOrRefreshOffer(classOffer); player.IncCounter("npc_offer_presented:" + classOffer.offerId);
        Check(YQNpcConversationRewards.TryAccept(player, "npc_test", "I accept", out _) && classOffer.offerState == "accepted", "Spoken acceptance uses existing class award path");
        player.IncCounter("npc_offer_presented:" + b.offerId);
        Check(YQNpcConversationRewards.TryAccept(player, "npc_other", "no thanks", out _) && b.offerState == "declined", "Polite refusal closes only the speaker's offer");
        Check(!YQNpcConversationRewards.TryApplyDecision(player, "npc_test", b.offerId, "accept", out _), "Structured intent cannot revive or redirect a stale offer");
        Check(YQDialogueGrounding.ReadOfferDecision("{\"npcText\":\"Understood.\",\"action\":\"none\",\"confidence\":0.9,\"offerDecision\":\"decline\"}") == "decline", "Semantic paraphrase uses a closed typed intent");
        Check(!YQNpcConversationRewards.AllowsOfferDecision("I'll accept if you also give me gold"), "Conditional bargaining cannot mutate an offer despite model misclassification");
        Check(!YQNpcConversationRewards.AllowsOfferDecision("Perhaps, let me think about it"), "Deferred choice cannot mutate an offer");
        Check(!YQDialogueGrounding.TryValidateEnvelope("{\"npcText\":\"Understood.\",\"action\":\"none\",\"confidence\":0.9,\"offerDecision\":\"grant_gold\"}", out _), "Unsupported model intent rejected");
        foreach (string text in new[] { "Yes, if you pay me", "She said I'll take it", "No, I'll take it", "I accept and give me gold", "I'd like to know more", "Maybe I'll do that", "I'll take it?" })
            Check(YQNpcConversationRewards.ClassifyOfferReply(text) == "none", "Ambiguous/reported/mixed intent requires a full conversation: " + text);
        // note: The real toast and mutation adapter are exercised on detached state; no profile is installed, mutated or saved.
        var toastObject = new GameObject("DetachedVoiceOfferToast");
        var actorObject = new GameObject("DetachedVoiceOfferActor");
        var priorToast = YourQuestProgressionOfferUI.Instance;
        try
        {
            var toast = toastObject.AddComponent<YourQuestProgressionOfferUI>();
            var uiType = typeof(YourQuestProgressionOfferUI);
            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            if (uiType.GetField("_canvas", fields).GetValue(toast) == null) uiType.GetMethod("Awake", fields).Invoke(toast, null);
            var toastPlayer = new PlayerState(); toastPlayer.EnsureCollections();
            var shown = new PendingProgressionOfferRecord { offerKind = "quest", name = "Visible Commission", description = "Speak with Ada.", payloadJson = "{\"objective\":{\"type\":\"talk_to_npc\",\"targetId\":\"npc_test\",\"requiredCount\":1}}" };
            toastPlayer.QueueOrRefreshOffer(shown);
            uiType.GetField("_current", fields).SetValue(toast, shown);
            uiType.GetField("_presentedPlayer", fields).SetValue(toast, toastPlayer);
            var canvas = (Canvas)uiType.GetField("_canvas", fields).GetValue(toast); canvas.enabled = true;
            Check(YQNpcConversationRewards.PresentedOfferId(toastPlayer, "npc_test") == shown.offerId, "Visible unattributed offer binds to exact spoken choice");
            Check(toast.GetVisibleOffer(new PlayerState()) == null, "Toast cannot choose across players");
            canvas.enabled = false;
            Check(!YQNpcConversationRewards.TryAccept(toastPlayer, "npc_test", "yes", out _), "Hidden unattributed offer cannot be chosen");
            canvas.enabled = true;
            Check(YQNpcConversationRewards.TryAccept(toastPlayer, "npc_test", "yes, I'll do that", out string spokenReceipt) && shown.offerState == "accepted" && toastPlayer.quests.Count == 1, "Visible quest accepts through canonical objective path");
            Check(toast.GetVisibleOffer(toastPlayer) == null, "Resolved quest disappears from visible offer selection");
            Check(!YQNpcConversationRewards.TryAccept(toastPlayer, "npc_test", "yes", out _), "Resolved visible offer cannot grant twice");
            var actor = actorObject.AddComponent<NpcDialogueAgent>(); actor.npcId = "npc_test"; actor.personaSummary = "Irritable, gruff and impatient.";
            string happy = actor.BuildOfferDecisionReply(toastPlayer, shown.offerId, "accept", spokenReceipt);
            Check(happy.StartsWith("Good.") && happy.Contains(shown.name), "Successful choice receives personality reaction and actual receipt");
            var declined = new PendingProgressionOfferRecord { offerKind = "title", name = "Unwanted Distinction", description = "A distinction." }; toastPlayer.QueueOrRefreshOffer(declined);
            uiType.GetField("_current", fields).SetValue(toast, declined);
            Check(YQNpcConversationRewards.TryAccept(toastPlayer, "npc_test", "No, I don't want it, thanks", out spokenReceipt) && declined.offerState == "declined", "Visible progression offer closes on natural refusal");
            Check(actor.BuildOfferDecisionReply(toastPlayer, declined.offerId, "decline", spokenReceipt).StartsWith("Hmph."), "Irritable NPC reacts to actual refusal");
            Check(!YQNpcConversationRewards.CanSpeakForOffer(b, "npc_test"), "Visible NPC-owned offer cannot transfer source ownership");
            foreach (string line in new[] { "Give me a moment to think about that...", "Let me gather my thoughts.", "I'm thinking." })
                Check(DialogueThinkService.IsWaitingPlaceholder(line), "Standalone fake waiting speech rejected: " + line);
            Check(!DialogueThinkService.IsWaitingPlaceholder("I need a moment to think about whether I trust you."), "Contextual thinking remains valid dialogue");
            string last = null;
            for (int i = 0; i < 6; i++) { string line = actor.BuildFallback("Unclear words"); Check(line != last && !DialogueThinkService.IsWaitingPlaceholder(line), "Confusion varies without false waiting " + i); last = line; }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject); UnityEngine.Object.DestroyImmediate(toastObject);
            typeof(YourQuestProgressionOfferUI).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, priorToast);
        }
        Check(YQDialogueGrounding.CaptureObjective(new PlayerState()) == "No active quest.", "No stale director objective when no accepted active quest exists");
        var payload = JObject.Parse("{\"objective\":{\"type\":\"talk_to_npc\",\"targetId\":\"npc_test\",\"requiredCount\":1,\"description\":\"Speak with Ada\"}}");
        var questOffer = new PendingProgressionOfferRecord { offerKind = "quest", name = "A New Question", description = "Ask Ada about the river.", context = YQNpcConversationRewards.OfferContext("npc_other"), payloadJson = payload.ToString() };
        player.pendingOffers.Clear(); player.QueueOrRefreshOffer(questOffer); player.IncCounter("npc_offer_presented:" + questOffer.offerId);
        Check(YQNpcConversationRewards.TryAccept(player, "npc_other", "I accept", out _) && questOffer.offerState == "accepted", "Spoken acceptance uses existing structured quest path");
        QuestRecord newQuest = player.quests[player.quests.Count - 1];
        Check(!YQQuestCompletionDirector.EvaluateObjectives(player, newQuest, out _), "Earlier conversation cannot instantly finish a new commission");
        YQQuestCompletionDirector.RecordCompletedDialogue(player, "npc_test");
        Check(YQQuestCompletionDirector.EvaluateObjectives(player, newQuest, out _), "Subsequent conversation completes new commission");
        var proposal = new JObject { ["decision"] = "quest", ["payload"] = payload };
        var recipients = JArray.Parse("[{\"npcId\":\"npc_test\"}]");
        Check(YQNpcConversationRewards.ValidNpcProposal(proposal, "npc_other", recipients), "Known recipient accepted");
        Check(!YQNpcConversationRewards.ValidNpcProposal(proposal, "npc_test", recipients), "Self-talk gift farming quest rejected");
        Check(!YQNpcConversationRewards.ValidNpcProposal(proposal, "npc_other", new JArray()), "Invented destination rejected");
        string saved = JsonConvert.SerializeObject(player, new JsonSerializerSettings { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } });
        var restored = JsonConvert.DeserializeObject<PlayerState>(saved);
        Check(restored.pendingOffers[0].context == questOffer.context && restored.pendingOffers[0].offerState == "accepted", "Existing save fields retain provenance and accepted state");
        Write("conversation-contracts", new { passed = true, checks, utc = DateTime.UtcNow,
            evidence = "Detached production-method contracts; microphone, TTS and live item asset binding require runtime verification" });
    }

    private static void ConversationRuntime()
    {
        // note: Observe real dialogue/quest state while item binding targets a detached player, never the user's inventory or paired save.
        PlayerState live = PlayerStateManager.Instance?.state;
        var detached = new PlayerState(); detached.EnsureCollections();
        var item = new PendingProgressionOfferRecord { offerKind = "item", name = "Wayfarer's Keepsake",
            description = "A small keepsake carried for luck.", skillType = "trinket", context = YQNpcConversationRewards.OfferContext("fixture_npc") };
        detached.QueueOrRefreshOffer(item);
        bool accepted = detached.AcceptOffer(item.offerId, out string bindingMessage);
        var agents = new List<object>();
        foreach (NpcDialogueAgent agent in NpcDialogueAgent.ActiveAgents)
        {
            live.behaviorCounters.TryGetValue("dialogue:" + agent.NpcId, out float completedTalks);
            agents.Add(new { agent.NpcId, agent.NpcName, agent.IsThinking, agent.LastReplyWasFallback,
                agent.LastReplyStatus, agent.LastNpcLine, agent.LastReplyLatencySeconds, completedTalks,
                facts = JObject.Parse(YQNpcConversationRewards.CaptureFacts(live, WorldStateManager.Instance?.State, agent.NpcId)) });
            if (agents.Count >= 20) break;
        }
        Write("conversation-runtime-" + DateTime.UtcNow.ToString("HHmmss"), new { utc = DateTime.UtcNow,
            itemBindingPassed = accepted && item.offerState == "accepted", bindingMessage,
            agents, activeQuest = live.GetActiveQuest(),
            evidence = "Live observation plus item materialization into detached player; no microphone assertion or live inventory grant" });
    }
    private static object Point(Vector3 value) => new { value.x, value.y, value.z };
    private static void DialogueLatency()
    {
        // note: Invoke the production generator and fact checker only. This assisted measurement never commits a transcript, talk objective or reward and never plays audio.
        var agent = UnityEngine.Object.FindObjectsByType<NpcDialogueAgent>(FindObjectsSortMode.None).FirstOrDefault(a => !a.IsThinking);
        if (agent == null || DialogueThinkService.Instance == null) throw new InvalidOperationException("No idle dialogue owner is available.");
        string priorLine = agent.LastNpcLine;
        var cases = new[] { "What do you enjoy about your work?", "What sort of person earns your respect?", "Does this place ever get lonely?" };
        var results = new JArray();
        int index = 0;
        Action next = null;
        next = () => {
            if (!Application.isPlaying || agent == null || agent.IsThinking || agent.LastNpcLine != priorLine) return;
            if (index >= cases.Length) {
                Write("dialogue-latency", new { utc = DateTime.UtcNow, cases = results,
                    evidence = "Assisted production generator plus factual review under ordinary world load; no microphone, TTS or reward preparation" });
                return;
            }
            string question = cases[index++];
            string competing = LLMClient.Instance.ActiveRequestDebugTag;
            double started = EditorApplication.timeSinceStartup;
            DialogueThinkService.Instance.RequestNpcReply(agent, question, reply => {
                results.Add(new JObject { ["question"] = question, ["reply"] = reply,
                    ["elapsedSeconds"] = EditorApplication.timeSinceStartup - started,
                    ["competingRequest"] = competing, ["fallback"] = agent.LastReplyWasFallback });
                next();
            }, () => agent != null && !agent.IsThinking && agent.LastNpcLine == priorLine);
        };
        next();
    }
    private static void OfferIntentModel()
    {
        // note: Exercise the production prompt and resident model with detached offer facts. No speech, memory, inventory or profile is changed by these probes.
        var cases = new[] {
            ("I'd be honoured to receive it", "accept"), ("That would be lovely, thank you", "accept"),
            ("You are kind, but I must turn down your offer", "decline"), ("I would rather you kept it", "decline"),
            ("What does the title mean?", "none"), ("I'll accept if you also give me gold", "none"),
            ("Let me think about it", "none"), ("My friend said she would accept", "none") };
        string evidence = YQDialogueGrounding.Capture(
            "npcId=npc_probe; name=Ada; role=archivist; temperament=dry and thoughtful. " +
            "conversation_facts: {\"offers\":[{\"kind\":\"title\",\"name\":\"Friend of the Archive\",\"state\":\"pending\"}]}",
            "{}", "No active quest.", "Ada: I can offer you Friend of the Archive. Will you accept it?");
        var results = new JArray();
        int index = 0;
        Action next = null;
        next = () => {
            if (!Application.isPlaying || LLMClient.Instance == null) return;
            if (index >= cases.Length) {
                Write("offer-intent-model", new { utc = DateTime.UtcNow, cases = results,
                    passed = results.All(item => item.Value<bool>("passed")),
                    evidence = "Resident production dialogue model and scheduler with detached offer context; no microphone or live grant" });
                return;
            }
            var probe = cases[index++];
            double started = EditorApplication.timeSinceStartup;
            LLMClient.Instance.Submit(new YQLlmRequest {
                prompt = YQDialogueGrounding.BuildGeneratePrompt(evidence, probe.Item1, true),
                debugTag = "OfferIntentProbe", category = LLMGenerationCategory.Dialogue,
                priority = YQLlmRequestPriority.PlayerFacing, requireJson = true,
                jsonSchema = YQDialogueGrounding.ReplySchema(420, true), maxRetries = 0,
                bindPlayerStateRevision = false, bindWorldStateRevision = false,
                optionsOverride = new Dictionary<string, object> { { "num_predict", 160 }, { "temperature", .2f }, { "presence_penalty", 0f } }
            }, result => {
                string proposedDecision = YQDialogueGrounding.ReadOfferDecision(result.text);
                string decision = YQNpcConversationRewards.AllowsOfferDecision(probe.Item1) ? proposedDecision : "none";
                results.Add(new JObject { ["utterance"] = probe.Item1, ["expected"] = probe.Item2,
                    ["decision"] = decision, ["modelProposal"] = proposedDecision, ["passed"] = result.success && decision == probe.Item2,
                    ["queueSeconds"] = result.queueWaitSeconds, ["generationSeconds"] = result.generationSeconds,
                    ["elapsedSeconds"] = EditorApplication.timeSinceStartup - started,
                    ["response"] = result.text, ["error"] = result.error });
                next();
            });
        };
        next();
    }
    private static void ArmPopulationActivationTrace()
    {
        // note: Wait only for a newly staged continuation cast; never seize an existing native recording.
        if (ProfilerDriver.enabled || traceUntil > 0d || traceExportPending || activationTraceArmed)
            throw new InvalidOperationException("Native activation trace is already owned or recording is active.");
        activationTraceArmed = true;
        activationTraceArmUntil = EditorApplication.timeSinceStartup + 90d;
        activationTraceSiteId = activationTraceCompletedSiteId = null;
        YQPopulationWorkDiagnostics.ActivationTraceArmRequested += OnLastResidentStaged;
        Write("population-activation-trace-armed", new { utc = DateTime.UtcNow, deadlineSeconds = 90,
            evidence = "One-shot native trace waits for the final resident before a continuation population activation." });
    }
    private static void OnLastResidentStaged(string siteId)
    {
        if (!activationTraceArmed) return;
        activationTraceArmed = false;
        activationTraceSiteId = siteId;
        try
        {
            StartOwnedTrace(5000d);
            activationTraceRunning = true;
            YQPopulationWorkDiagnostics.ActivationTraceCompleted += OnPopulationActivationCompleted;
            Write("population-activation-trace-started", new { utc = DateTime.UtcNow, siteId,
                startFrame = traceStartFrame, evidence = "Recording started after final resident creation; first native frame can be partial." });
        }
        catch (Exception error)
        {
            ClearActivationTraceHooks();
            Write("population-activation-trace-error", new { utc = DateTime.UtcNow, siteId, error = error.ToString() });
        }
    }
    private static void OnPopulationActivationCompleted(string siteId)
    {
        if (!activationTraceRunning) return;
        activationTraceCompletedSiteId = siteId;
        activationTraceCompleted = true;
        activationTraceReturnNativeFrame = ProfilerDriver.lastFrameIndex;
    }
    private static void ClearActivationTraceHooks()
    {
        YQPopulationWorkDiagnostics.ActivationTraceArmRequested -= OnLastResidentStaged;
        YQPopulationWorkDiagnostics.ActivationTraceCompleted -= OnPopulationActivationCompleted;
        activationTraceArmed = false;
    }
    private static void StartOwnedTrace(double minimumMs)
    {
        // note: Never interrupt active recording. Preserve stopped history (including Unity's startup frames) before the explicitly requested bounded trace; a failed save leaves it intact.
        if (ProfilerDriver.enabled || traceUntil > 0d || traceExportPending)
            throw new InvalidOperationException("Native profiler recording is already owned or active; stop it before a bounded capture.");
        if (HasNativeFrames()) ReleaseProfilerHistory();
        traceMinimumMs = minimumMs;
        traceStartFrame = Math.Max(0, ProfilerDriver.lastFrameIndex + 1);
        traceStartMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        traceUntil = EditorApplication.timeSinceStartup + 15d;
        ProfilerDriver.enabled = true;
    }
    private static void FinishOwnedTrace()
    {
        ProfilerDriver.enabled = false;
        traceUntil = 0d;
        traceExportPending = true;
    }
    private static void ExportOwnedTrace()
    {
        // note: Preserve activation's complete bounded native history before the compact JSON export clears it.
        if (activationTraceRunning)
        {
            Directory.CreateDirectory(Output);
            activationTraceRawPath = Path.GetFullPath(Path.Combine(Output,
                "native-population-activation-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".data"));
            if (!HasNativeFrames() || !ProfilerDriver.SaveProfile(activationTraceRawPath))
                throw new IOException("Activation profiler history could not be preserved; nothing cleared.");
        }
        Frames();
        ProfilerDriver.ClearAllFrames();
        traceExportPending = false;
        ClearActivationTraceHooks();
        activationTraceRunning = activationTraceCompleted = false;
        activationTraceRawPath = null;
    }
    private static bool HasNativeFrames()
    {
        // note: Unity may report index zero for an empty history; validity, rather than a nonnegative index alone, establishes retained data.
        if (ProfilerDriver.lastFrameIndex < 0) return false;
        using (var frame = ProfilerDriver.GetRawFrameDataView(ProfilerDriver.lastFrameIndex, 0)) return frame.valid;
    }
    private static void ReleaseProfilerHistory()
    {
        // note: Preserve the diagnostic recording before releasing its native history; this explicit edit-mode operation never changes game data.
        ProfilerDriver.enabled = false;
        traceUntil = 0d;
        Directory.CreateDirectory(Output);
        string path = Path.GetFullPath(Path.Combine(Output, "preserved-profiler-" + DateTime.UtcNow.ToString("HHmmss") + ".data"));
        long before = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
        bool hasFrames = HasNativeFrames();
        if (hasFrames && !ProfilerDriver.SaveProfile(path)) throw new IOException("Profiler history could not be preserved; nothing cleared.");
        ProfilerDriver.ClearAllFrames();
        SessionState.SetBool(UnprofiledSession, true);
        File.WriteAllText(UnprofiledBaseline, "Explicit project-local unprofiled gameplay baseline; bounded trace or manual recording remains available.");
        Write("profiler-release", new { utc = DateTime.UtcNow, first, last, hasFrames, path,
            beforeAllocated = before, afterAllocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
            reserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong(),
            profilerBufferLimit = UnityEngine.Profiling.Profiler.maxUsedMemory });
    }
    private static void StorageContracts()
    {
        // note: Exercise the actual runtime factory and reload admission boundary without starting a model, changing a profile or querying the GPU.
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        var owner = new LlamaCppServerProcess();
        var requests = new List<LLMRuntimeConfig>();
        try
        {
            string root = @"C:\Ai\YourQuestRuntime\";
            var routes = new List<object>();
            foreach (LLMGenerationCategory role in Enum.GetValues(typeof(LLMGenerationCategory)))
            {
                var request = config.CreateRequestConfig(role);
                requests.Add(request);
                bool passed = config.enableRuntimeLlm && request.ggufModelPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                    request.llamaServerExecutablePath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(request.ggufModelPath) && File.Exists(request.llamaServerExecutablePath);
                routes.Add(new { role = role.ToString(), request.ggufModelPath, request.llamaServerExecutablePath, passed });
                if (!passed) throw new InvalidOperationException("A runtime role escaped the provisioned SSD runtime: " + role);
            }
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var required = typeof(LlamaCppServerProcess).GetMethod("RequiredReloadFreeMemory", flags);
            int measured = (int)required.Invoke(null, new object[] { 4460, 1278, 1536 });
            if (measured != 4846 || (int)required.Invoke(null, new object[] { 4460, 0, 1536 }) != 0)
                throw new InvalidOperationException("Reload measurement failed reserve or unknown telemetry handling.");
            var shape = typeof(LlamaCppServerProcess).GetMethod("LoadShape", flags);
            var learned = (Dictionary<string, int>)typeof(LlamaCppServerProcess).GetField("_reloadFreeMemoryMb", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
            learned[(string)shape.Invoke(null, new object[] { config })] = measured;
            var admit = typeof(LlamaCppServerProcess).GetMethod("CanAdmitGpuLoad", BindingFlags.Instance | BindingFlags.NonPublic);
            bool below = (bool)admit.Invoke(owner, new object[] { config, measured - 1 });
            bool enough = (bool)admit.Invoke(owner, new object[] { config, measured });
            if (below || !enough || config.targetGpuHeadroomMb != 1536 ||
                config.bestEffortSpeechModelSha256 != "3874209241c9a397e2f62cd3f70f80fd2dfbf0dfccb6838416bdb48a714e8630")
                throw new InvalidOperationException("Reload admission or existing qualification/reserve changed.");
            Write("storage-contracts-" + DateTime.UtcNow.ToString("HHmmss"), new { passed = true, routes, measuredReloadMinimumMb = measured, rejectsBelow = !below, acceptsAtBoundary = enough, config.bestEffortSpeechModelSha256 });
        }
        finally
        {
            owner.Dispose();
            foreach (var request in requests) UnityEngine.Object.DestroyImmediate(request);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }
    private static void WorldRepairContracts()
    {
        // note: Exercise the actual narrow repair and commit admission with detached JSON, without installing a world or altering presentation state.
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var parse = typeof(YQWorldGenerationService).GetMethod("TryParseRootIgnoringBrokenGoddessVoice", flags);
        var strip = typeof(YQWorldGenerationService).GetMethod("RemoveGeneratedRuntimeAuthority", flags);
        string raw = "{\"schemaVersion\":\"world_plan_v1\",\"source\":\"llm\",\"worldSeed\":\"repair-fixture\",\"summary\":\"A valley\",\"regions\":[],\"settlements\":[{\"runtimeSiteKitId\":\"forbidden\"}],\"encampments\":[],\"routes\":[],\"spatialPlanV2\":{\"forbidden\":true},\"goddessVoice\":{\"line\":broken}}";
        object[] arguments = { raw, null, null };
        bool repaired = (bool)parse.Invoke(null, arguments);
        if (!repaired) throw new InvalidOperationException(arguments[2] as string);
        var root = (JObject)arguments[1];
        root.Remove("goddessVoice"); strip.Invoke(null, new object[] { root });
        bool prepared = YQContentProposalBoundary.TryPrepare(root.ToString(Formatting.None), "llm_world_plan_v1", "fixture", "world_plan_v1",
            value => YQContentProposalBoundary.ValidateRequiredProperties(value, "schemaVersion", "source", "worldSeed", "summary", "regions", "settlements", "encampments", "routes"),
            null, value => value["regions"] is JArray && value["settlements"] is JArray && value["encampments"] is JArray && value["routes"] is JArray,
            out _, out string failure);
        bool stripped = root["spatialPlanV2"] == null && root["settlements"][0]["runtimeSiteKitId"] == null;
        bool duplicateRejected = !(bool)parse.Invoke(null, new object[] { "{\"worldSeed\":\"a\",\"worldSeed\":\"b\",\"goddessVoice\":{\"line\":broken}}", null, null });
        bool damagedCanonRejected = !(bool)parse.Invoke(null, new object[] { "{\"worldSeed\":broken,\"goddessVoice\":{\"line\":broken}}", null, null });
        if (!prepared || !stripped || !duplicateRejected || !damagedCanonRejected) throw new InvalidOperationException("Repair boundary regression: " + failure);
        Write("world-repair-contracts", new { passed = true, repaired, prepared, stripped, duplicateRejected, damagedCanonRejected,
            evidence = "Detached optional-voice repair and generic proposal admission only; not full generated-world acceptance or new-character proof", utc = DateTime.UtcNow });
    }
    private static void CandidateCacheContracts()
    {
        // note: A detached, genuinely compiled fixture checks every compiler-input branch for deep mutation without creating a profile or issuing a live admission token.
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var plan = (GeneratedWorldPlanRecord)typeof(YQSemanticWorldAuthorityTests).GetMethod("BuildFixture", flags).Invoke(null, new object[] { "76603739" });
        YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(plan, out var artifact, out string failure)) throw new InvalidOperationException(failure);
        plan.spatialPlanV2 = artifact;
        if (!YQSpatialMaterializationCompilerV2.TryPrepareCandidate(plan, artifact, out var projection, out failure)) throw new InvalidOperationException(failure);
        var type = typeof(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance);
        object token = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
        var key = type.GetMethod("CandidateCompilerInputsJson", flags);
        var matches = type.GetMethod("HasPreparedCandidate", BindingFlags.NonPublic | BindingFlags.Instance);
        type.GetField("preparedCandidateInputsJson", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(token, key.Invoke(null, new object[] { plan, artifact }));
        type.GetField("preparedProjection", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(token, projection);
        if (!(bool)matches.Invoke(token, new object[] { plan })) throw new InvalidOperationException("Identical compiled input was not reusable.");
        var settings = new JsonSerializerSettings { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
        var source = JObject.Parse(JsonConvert.SerializeObject(plan, settings));
        bool detachedCopyAccepted = (bool)matches.Invoke(token, new object[] { source.ToObject<GeneratedWorldPlanRecord>(JsonSerializer.Create(settings)) });
        if (!detachedCopyAccepted) throw new InvalidOperationException("Identical detached compiler inputs did not reuse proof.");
        var results = new List<object>();
        foreach (string field in new[] { "worldSeed", "spatialPlan", "settlements", "encampments", "spatialPlanV2" })
        {
            var changed = (JObject)source.DeepClone();
            if (field == "worldSeed") changed[field] = "changed";
            else if (field == "spatialPlanV2") changed[field]["blueprint"]["regions"][0]["radius"] = 1f;
            else changed[field] = JValue.CreateNull();
            var candidate = changed.ToObject<GeneratedWorldPlanRecord>(JsonSerializer.Create(settings));
            bool rejected = !(bool)matches.Invoke(token, new object[] { candidate });
            results.Add(new { field, rejected });
            if (!rejected) throw new InvalidOperationException("Changed compiler input reused: " + field);
        }
        type.GetField("preparedProjection", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(token, null);
        bool absentProofRejected = !(bool)matches.Invoke(token, new object[] { plan });
        if (!absentProofRejected) throw new InvalidOperationException("Absent compiler proof was accepted.");
        Write("candidate-cache-contracts", new { passed = true, seed = plan.worldSeed, results, absentProofRejected, detachedCopyAccepted, utc = DateTime.UtcNow,
            evidence = "Detached compiler proof and deep-input cache invalidation; not a live publication performance measurement" });
    }
    private static void DialogueContracts()
    {
        // note: Disposable editor actors verify shared outage flavour without persisting NPC claims or owning a model request.
        GameObject fixture = new GameObject("DialogueUnavailableFixture");
        try
        {
            var agent = fixture.AddComponent<NpcDialogueAgent>();
            agent.persistTranscriptAcrossSessions = false;
            var finish = typeof(DialogueThinkService).GetMethod("FinishUnavailable", BindingFlags.Static | BindingFlags.NonPublic);
            var rows = new List<object>();
            foreach (string role in new[] { "scholar", "merchant", "healer", "stern", "intelligent_creature" })
            {
                agent.npcName = "Fixture " + role;
                agent.tagsOverride = new List<string> { role };
                string error = "FullGpuUnavailable: freeMb=3292, reserveMb=1536";
                int callbacks = 0; string spoken = "unexpected";
                finish.Invoke(null, new object[] { agent, "What do you make of all this?", null, (Action<string>)(line => { callbacks++; spoken = line; }), error });
                string second = agent.BuildFallback("What do you make of all this?");
                bool passed = callbacks == 1 && !string.IsNullOrWhiteSpace(spoken) && spoken != second &&
                    spoken != YQDialogueGrounding.UnknownReply && agent.LastReplyWasFallback && string.IsNullOrEmpty(agent.LastNpcLine);
                rows.Add(new { role, error, passed, callbacks, spoken, second, status = agent.LastReplyStatus });
                if (!passed) throw new InvalidOperationException("Shared outage flavour failed variation, callback, or memory isolation.");
            }
            Write("dialogue-contracts-" + DateTime.UtcNow.ToString("HHmmss"), rows);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
    }
    private static bool Resolve(YQPreparedSpatialMaterializationV2 prepared, out Vector3 summit)
    {
        // note: Verification crosses the editor assembly boundary without exposing a new gameplay API.
        object[] arguments = { prepared, Vector3.zero };
        bool result = (bool)typeof(YQGeneratedWorldLayout).GetMethod("TryResolveDryGoddessSummit", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
        summit = (Vector3)arguments[1]; return result;
    }
    private static bool Measure(YQPreparedSpatialMaterializationV2 prepared, Vector3 point, out float bank, out bool wet)
    {
        object[] arguments = { prepared, point.x, point.z, null, 0f, false };
        bool result = (bool)typeof(YQContinuousWorldFeatureAuthority).GetMethod("TryMeasureAcceptedWaterDistance", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
        bank = (float)arguments[4]; wet = (bool)arguments[5]; return result;
    }
    private sealed class SavedActivationSample
    {
        public int index, depth, metadataCount;
        public string name;
        public double ms, childrenMs;
    }

    private static readonly MethodInfo SavedSampleMetadataTypeMethod =
        typeof(UnityEditor.Profiling.RawFrameDataView).GetMethod("GetSampleMetadataType", BindingFlags.Instance | BindingFlags.NonPublic);

    private static object ReadSavedSampleMetadata(UnityEditor.Profiling.RawFrameDataView view, int sample, int item)
    {
        // note: Unity 6 exposes the typed values publicly but keeps their discriminator internal; reflect only this bounded saved-profile probe.
        if (SavedSampleMetadataTypeMethod == null)
            throw new MissingMethodException("Unity RawFrameDataView.GetSampleMetadataType is unavailable.");
        var type = (Unity.Profiling.LowLevel.ProfilerMarkerDataType)
            SavedSampleMetadataTypeMethod.Invoke(view, new object[] { sample, item });
        object value = null;
        string objectName = null, objectType = null, assetPath = null, readError = null, rawHex = null;
        try
        {
            switch (type)
            {
                case Unity.Profiling.LowLevel.ProfilerMarkerDataType.InstanceId:
                    int id = view.GetSampleMetadataAsInt(sample, item);
                    value = id;
                    var obj = EditorUtility.EntityIdToObject(id);
                    if (obj != null)
                    {
                        objectName = obj.name;
                        objectType = obj.GetType().FullName;
                        assetPath = AssetDatabase.GetAssetPath(obj);
                    }
                    break;
                case Unity.Profiling.LowLevel.ProfilerMarkerDataType.Int32:
                case Unity.Profiling.LowLevel.ProfilerMarkerDataType.UInt32:
                    value = view.GetSampleMetadataAsInt(sample, item);
                    break;
                case Unity.Profiling.LowLevel.ProfilerMarkerDataType.Int64:
                case Unity.Profiling.LowLevel.ProfilerMarkerDataType.UInt64:
                    value = view.GetSampleMetadataAsLong(sample, item);
                    break;
                case Unity.Profiling.LowLevel.ProfilerMarkerDataType.Float:
                    value = view.GetSampleMetadataAsFloat(sample, item);
                    break;
                case Unity.Profiling.LowLevel.ProfilerMarkerDataType.Double:
                    value = view.GetSampleMetadataAsDouble(sample, item);
                    break;
                case Unity.Profiling.LowLevel.ProfilerMarkerDataType.String16:
                    value = view.GetSampleMetadataAsString(sample, item);
                    break;
                default:
                    // note: Retain both raw bytes and any API-rendered text for Unity's unnamed metadata type; neither is assigned asset semantics here.
                    try
                    {
                        rawHex = BitConverter.ToString(view.GetSampleMetadataAsSpan<byte>(sample, item).ToArray());
                    }
                    catch (Exception unsupported)
                    {
                        readError = "Raw byte accessor unavailable: " + unsupported.GetType().Name;
                    }
                    try { value = view.GetSampleMetadataAsString(sample, item); }
                    catch (Exception unsupported)
                    {
                        readError = (readError == null ? "" : readError + "; ") +
                            "String accessor unavailable: " + unsupported.GetType().Name;
                    }
                    if (readError == null) readError = "Unnamed metadata type; raw bytes and string accessor output are untyped.";
                    break;
            }
        }
        catch (Exception error) { readError = error.GetType().Name + ": " + error.Message; }
        return new { item, type = type.ToString(), value, rawHex, objectName, objectType, assetPath, readError };
    }

    private static void InspectSavedActivationRawMetadata()
    {
        // note: This explicit Edit-mode probe reopens one preserved native capture; it never records or changes gameplay.
        if (Application.isPlaying || ProfilerDriver.enabled || traceUntil > 0d || traceExportPending || activationTraceArmed)
            throw new InvalidOperationException("Saved activation inspection requires idle Edit mode and no active native recorder.");
        string path = Path.GetFullPath(Path.Combine(Output, "native-population-activation-20261016_213820_200.data"));
        if (!File.Exists(path)) throw new FileNotFoundException("Saved activation profile is missing.", path);
        if (HasNativeFrames()) ReleaseProfilerHistory();
        if (!ProfilerDriver.LoadProfile(path, false)) throw new IOException("Saved activation profile could not be opened.");
        var captures = new List<object>();
        int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
        for (int frame = first; frame <= last; frame++)
        using (var view = ProfilerDriver.GetRawFrameDataView(frame, 0))
        {
            if (!view.valid) continue;
            for (int sample = 0; sample < view.sampleCount; sample++)
            {
                if (view.GetSampleName(sample) != "YQ.Population.Activation") continue;
                int descendants = view.GetSampleChildrenCountRecursive(sample);
                int end = Math.Min(view.sampleCount, sample + descendants + 1);
                var rows = new List<SavedActivationSample>(end - sample);
                var parents = new Stack<(int end, SavedActivationSample node)>();
                for (int child = sample; child < end; child++)
                {
                    while (parents.Count > 0 && child >= parents.Peek().end) parents.Pop();
                    int childDescendants = view.GetSampleChildrenCountRecursive(child);
                    var row = new SavedActivationSample { index = child, depth = parents.Count,
                        name = view.GetSampleName(child), ms = view.GetSampleTimeMs(child),
                        metadataCount = view.GetSampleMetadataCount(child) };
                    if (parents.Count > 0) parents.Peek().node.childrenMs += row.ms;
                    rows.Add(row);
                    if (childDescendants > 0) parents.Push((child + childDescendants + 1, row));
                }
                var withMetadata = rows.Where(row => row.metadataCount > 0).OrderByDescending(row => row.ms).ToArray();
                var costly = rows.OrderByDescending(row => Math.Max(0d, row.ms - row.childrenMs)).Take(128).ToArray();
                captures.Add(new { frame, rootSample = sample, descendants, visited = rows.Count,
                    subtreeScanTruncated = rows.Count != descendants + 1,
                    metadataSampleCount = withMetadata.Length, retainedMetadataSamples = Math.Min(512, withMetadata.Length),
                    metadataSamplesTruncated = withMetadata.Length > 512,
                    metadataNameGroups = withMetadata.GroupBy(row => row.name)
                        .Select(group => new { name = group.Key, count = group.Count(), maxMs = group.Max(row => row.ms) })
                        .OrderByDescending(group => group.maxMs).ToArray(),
                    metadataSamples = withMetadata.Take(512).Select(row => new { row.index, row.depth, row.name, row.ms,
                        row.metadataCount, items = Enumerable.Range(0, row.metadataCount)
                            .Select(item => ReadSavedSampleMetadata(view, row.index, item)).ToArray() }).ToArray(),
                    topExclusive = costly.Select(row => new { row.index, row.depth, row.name, row.ms,
                        directChildrenMs = row.childrenMs, estimatedExclusiveMs = Math.Max(0d, row.ms - row.childrenMs),
                        row.metadataCount }).ToArray(),
                    negativeExclusiveCount = rows.Count(row => row.ms + 0.01d < row.childrenMs),
                    exclusiveNameGroups = rows.GroupBy(row => row.name)
                        .Select(group => new { name = group.Key, count = group.Count(),
                            inclusiveTotalMs = group.Sum(row => row.ms),
                            estimatedExclusiveTotalMs = group.Sum(row => Math.Max(0d, row.ms - row.childrenMs)) })
                        .OrderByDescending(group => group.estimatedExclusiveTotalMs).Take(100).ToArray() });
            }
        }
        Write("activation-raw-metadata", new { utc = DateTime.UtcNow, rawPath = path, first, last,
            evidence = "READ_ONLY_SAVED_NATIVE_PROFILE; direct-child subtraction estimates exclusive time on one thread; metadata IDs may not resolve after Play teardown",
            captures });
        ProfilerDriver.ClearAllFrames();
    }

    private static void Frames()
    {
        // note: Read the preceding bounded native recording after stopping Play; inclusive samples overlap and cannot be summed across parents.
        var frames = new List<object>();
        var activationSamples = new List<object>();
        int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
        for (int frame = Math.Max(first, last - 299); frame <= last; frame++)
        {
            using (var view = ProfilerDriver.GetRawFrameDataView(frame, 0))
            {
                if (!view.valid) continue;
                var costs = new Dictionary<string, double>();
                for (int sample = 0; sample < view.sampleCount; sample++)
                {
                    string name = view.GetSampleName(sample);
                    if (activationTraceRunning && name == "YQ.Population.Activation")
                    {
                        int descendants = view.GetSampleChildrenCountRecursive(sample);
                        int end = Math.Min(view.sampleCount, sample + descendants + 1);
                        // note: Scan every captured descendant, while retaining complete direct-child aggregates and only costly detail rows.
                        var directByName = new Dictionary<string, (int count, double totalMs, double maxMs)>(StringComparer.Ordinal);
                        var costlyByName = new Dictionary<string, (int count, double totalMs, double maxMs)>(StringComparer.Ordinal);
                        var costlyNodes = new List<(int index, int depth, string name, double ms, int directChildren)>();
                        var ancestorEnds = new Stack<int>();
                        int visited = 0, directVisited = 0, expectedDirect = 0, engineEnd = -1;
                        for (int child = sample; child < end; child++)
                        {
                            while (ancestorEnds.Count > 0 && child >= ancestorEnds.Peek()) ancestorEnds.Pop();
                            int depth = ancestorEnds.Count;
                            int childDescendants = view.GetSampleChildrenCountRecursive(child);
                            string childName = view.GetSampleName(child);
                            double childMs = view.GetSampleTimeMs(child);
                            visited++;
                            if (depth == 2 && childName == "GameObject.ActivateAwakeRecursively")
                            {
                                engineEnd = child + childDescendants + 1;
                                expectedDirect = view.GetSampleChildrenCount(child);
                            }
                            else if (engineEnd > child && depth == 3)
                            {
                                directVisited++;
                                directByName.TryGetValue(childName, out var value);
                                directByName[childName] = (value.count + 1, value.totalMs + childMs, Math.Max(value.maxMs, childMs));
                            }
                            if (depth >= 3 && childMs >= 0.1d)
                            {
                                costlyNodes.Add((child, depth, childName, childMs, view.GetSampleChildrenCount(child)));
                                costlyByName.TryGetValue(childName, out var value);
                                costlyByName[childName] = (value.count + 1, value.totalMs + childMs, Math.Max(value.maxMs, childMs));
                            }
                            if (childDescendants > 0) ancestorEnds.Push(child + childDescendants + 1);
                        }
                        activationSamples.Add(new { frame, rootSample = sample, descendants, visited,
                            subtreeScanTruncated = visited != descendants + 1, expectedDirect, directVisited,
                            directScanTruncated = directVisited != expectedDirect,
                            directNameGroups = directByName.OrderByDescending(p => p.Value.totalMs)
                                .Select(p => new { name = p.Key, p.Value.count, p.Value.totalMs, p.Value.maxMs }).ToArray(),
                            costlyNodeCount = costlyNodes.Count, retainedCostlyNodes = Math.Min(128, costlyNodes.Count),
                            costlyNodesTruncated = costlyNodes.Count > 128,
                            topCostlyNodes = costlyNodes.OrderByDescending(p => p.ms).Take(128)
                                .Select(p => new { p.index, p.depth, p.name, p.ms, p.directChildren }).ToArray(),
                            costlyNameGroups = costlyByName.Count, retainedCostlyNameGroups = Math.Min(100, costlyByName.Count),
                            costlyNameGroupsTruncated = costlyByName.Count > 100,
                            topCostlyNameGroups = costlyByName.OrderByDescending(p => p.Value.totalMs).Take(100)
                                .Select(p => new { name = p.Key, p.Value.count, p.Value.totalMs, p.Value.maxMs }).ToArray() });
                    }
                    double ms = view.GetSampleTimeMs(sample);
                    if (ms < 1d) continue;
                    costs.TryGetValue(name, out double total); costs[name] = total + ms;
                }
                frames.Add(new { frame, ms = view.frameTimeMs, costs = costs.OrderByDescending(p => p.Value).Take(35).ToArray() });
            }
        }
        Write("native-frames-" + DateTime.UtcNow.ToString("HHmmss"), new { evidence = "Stored native Play Mode profiler frames; deep profiling disabled; inclusive subtree durations overlap and first recorded frame may be partial",
            first, last, frames, activationTrace = activationTraceRunning ? new { armedSite = activationTraceSiteId,
                completedSite = activationTraceCompletedSiteId, callbackReturned = activationTraceCompleted,
                completionNativeFrameBeforeCommit = activationTraceReturnNativeFrame, rawProfilerPath = activationTraceRawPath,
                samples = activationSamples } : null });
    }
    private static void Contracts()
    {
        var build = typeof(YQSemanticWorldAuthorityTests).GetMethod("BuildFixture", BindingFlags.Static | BindingFlags.NonPublic);
        var results = new List<object>();
        foreach (string seed in new[] { "semantic-v2-318", "76603739", "origin-dry-2", "origin-dry-3", "saved-r98" })
        {
            var settings = new JsonSerializerSettings { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
            var plan = seed == "saved-r98" ? JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(Output + "/world-r98-input.json"), settings).generatedWorldPlan
                : (GeneratedWorldPlanRecord)build.Invoke(null, new object[] { seed });
            string reason;
            if (seed != "saved-r98")
            {
                YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
                if (!YQSpatialBlueprintCompilerV2.TryCompile(plan, out var artifact, out reason)) throw new InvalidOperationException(reason);
                plan.spatialPlanV2 = artifact;
            }
            var accepted = plan.spatialPlanV2;
            var frontierSamples = accepted.acceptedContinuation?.locations;
            // note: This detached save fixture checks opening placement only; its frontier providers are loaded and verified by the ordinary runtime path.
            if (seed == "saved-r98") accepted.acceptedContinuation = null;
            if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out reason)) throw new InvalidOperationException(reason);
            string hash = accepted.contentHash;
            bool found = Resolve(prepared, out Vector3 a);
            bool repeat = Resolve(prepared, out Vector3 b);
            bool measured = Measure(prepared, a, out float bank, out bool wet);
            bool passed = found && repeat && a == b && (!measured || !wet && bank >= 20f) && accepted.contentHash == hash;
            // note: The optimization must leave full admission sensitive to unrevisioned deep corruption on detached sources.
            var domain = accepted.blueprint.regions[0];
            float previousRadius = domain.radius;
            domain.radius += 1f;
            bool rejectedMutation = !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out _, out _);
            domain.radius = previousRadius;
            passed &= rejectedMutation && YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out _, out _);
            var originSites = Enumerable.Range(0, prepared.SiteCount).Select(index => prepared.GetSite(index))
                .Where(site => new Vector2(site.x, site.z).magnitude < site.reservedRadius + 100f)
                .Select(site => new { site.siteId, site.x, site.z, site.reservedRadius }).ToArray();
            results.Add(new { seed, passed, found, rejectedMutation, summit = Point(a), bank, wet, originSites, unchanged = accepted.contentHash == hash });
            if (seed == "saved-r98" && frontierSamples != null) WorkerContracts(plan, prepared, frontierSamples, settings);
            Write("contracts-latest", results);
            if (!passed) { Write("contracts", results); throw new InvalidOperationException("Dry origin fixture failed: " + seed); }
        }
        Write("contracts-" + DateTime.UtcNow.ToString("HHmmss"), results);
        Debug.Log("[YQOriginLag] Detached deterministic dry-origin contracts passed.");
    }
    private static void WorkerContracts(GeneratedWorldPlanRecord plan, YQPreparedSpatialMaterializationV2 prepared,
        List<GeneratedSpatialContinuationLocationV2Record> samples, JsonSerializerSettings settings)
    {
        // note: Compare real saved frontier geometry on detached copies; neither branch installs content or owns the live game.
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var builder = typeof(YQGeneratedWorldRuntimeBuilder);
        var copyPlan = builder.GetMethod("CopyFrontierPhysicalPlan", flags);
        var worker = builder.GetMethod("BuildFrontierPhysicalPlacementSnapshot", flags);
        var synchronous = builder.GetMethod("TryBuildFrontierPhysicalPlacement", flags);
        var results = new List<object>();
        foreach (var source in samples.Take(3))
        {
            var candidate = JsonConvert.DeserializeObject<GeneratedSpatialContinuationLocationV2Record>(JsonConvert.SerializeObject(source, settings), settings);
            candidate.state = YQSpatialContinuationStateV2.Staged;
            object[] arguments = { plan, prepared, candidate, (Func<bool>)(() => true), null, null, null };
            bool success = (bool)synchronous.Invoke(null, arguments);
            var copy = (GeneratedWorldPlanRecord)copyPlan.Invoke(null, new object[] { plan });
            object result = System.Threading.Tasks.Task.Run(() => worker.Invoke(null,
                new object[] { copy, prepared, candidate, System.Threading.CancellationToken.None })).GetAwaiter().GetResult();
            var type = result.GetType();
            bool same = success == (bool)type.GetField("success").GetValue(result) &&
                JsonConvert.SerializeObject(arguments[5], settings) == JsonConvert.SerializeObject(type.GetField("context").GetValue(result), settings) &&
                Equals(arguments[6], type.GetField("failure").GetValue(result));
            results.Add(new { source.contentId, success, same, failure = arguments[6] });
            if (!same) throw new InvalidOperationException("Worker changed deterministic frontier placement");
            using (var cancelled = new System.Threading.CancellationTokenSource())
            {
                cancelled.Cancel();
                var retired = worker.Invoke(null, new object[] { copy, prepared, candidate, cancelled.Token });
                if ((bool)type.GetField("success").GetValue(retired)) throw new InvalidOperationException("Cancelled geometry admitted a candidate");
            }
        }
        Write("worker-contracts-" + DateTime.UtcNow.ToString("HHmmss"), results);
    }
    private static void Runtime()
    {
        var plan = WorldStateManager.Instance.State.generatedWorldPlan;
        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string reason)) throw new InvalidOperationException(reason);
        GameObject shrine = GameObject.Find("Origin_Goddess_MessengerMountain");
        Transform statue = shrine != null ? shrine.transform.Find("SM_AngelStatue_Origin") : null;
        if (statue == null) throw new InvalidOperationException("Origin statue missing");
        Bounds bounds = default; bool first = true;
        foreach (var renderer in statue.GetComponentsInChildren<Renderer>())
        { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
        var builder = YQGeneratedWorldRuntimeBuilder.Instance;
        var terrain = (Terrain)typeof(YQGeneratedWorldRuntimeBuilder).GetField("_generatedTerrain", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(builder);
        Vector3 arrival = (Vector3)typeof(YQGeneratedWorldRuntimeBuilder).GetField("_generatedOriginSpawnOverride", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        GameObject vey = GameObject.Find("Archivist Vey");
        bool dry = true; float bankMinimum = float.PositiveInfinity;
        // note: Sample the measured statue base and arrival, not only the imported transform pivot.
        var samples = new List<Vector3> { arrival };
        for (int x = 0; x < 3; x++) for (int z = 0; z < 3; z++)
            samples.Add(new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, x * .5f), 0f, Mathf.Lerp(bounds.min.z, bounds.max.z, z * .5f)));
        if (vey != null) samples.Add(vey.transform.position);
        foreach (var point in samples)
        {
            if (Measure(prepared, point, out float bank, out bool wet))
            { dry &= !wet; bankMinimum = Mathf.Min(bankMinimum, bank); }
            if (YQWaterSurfaceMotion.TryGetWaterHeight(point, out float water)) dry &= water < YQGeneratedWorldTerrain.SampleWorldHeight(terrain, point) - .05f;
        }
        var motor = YQInvestorPlayerMotor.ActiveMotor;
        var published = typeof(YQSpatialMaterializationResolverV2).GetMethod("TryGetPublishedProjection", BindingFlags.NonPublic | BindingFlags.Static);
        object[] arguments = { plan, null };
        var queryWatch = System.Diagnostics.Stopwatch.StartNew();
        bool frozenQueries = true;
        for (int index = 0; index < 1000; index++) frozenQueries &= (bool)published.Invoke(null, arguments) && ReferenceEquals(prepared, arguments[1]);
        queryWatch.Stop();
        Write("runtime", new { evidence = "Live production origin measurement after ordinary Continue; legacy bootstrap arrival repaired", dry, bankMinimum,
            bounds = new { min = Point(bounds.min), max = Point(bounds.max) }, arrival = Point(arrival), vey = vey != null ? Point(vey.transform.position) : null,
            veyArrivalDistance = vey != null ? Vector3.Distance(vey.transform.position, arrival) : -1f,
            player = motor != null ? Point(motor.transform.position) : null,
            veyPlayerDistance = vey != null && motor != null ? Vector3.Distance(vey.transform.position, motor.transform.position) : -1f,
            frozenQueries, thousandQueriesMilliseconds = queryWatch.Elapsed.TotalMilliseconds,
            seed = plan.worldSeed, hash = plan.spatialPlanV2.contentHash, utc = DateTime.UtcNow });
        if (!dry || !frozenQueries || vey == null || motor == null || Vector3.Distance(vey.transform.position, arrival) > 4f ||
            Vector3.Distance(vey.transform.position, motor.transform.position) > 4f) throw new InvalidOperationException("Live dry origin or Vey/player proximity failed");
        Debug.Log("[YQOriginLag] Live dry statue footprint and Vey beside arrival passed.");
    }
    private static void Publication()
    {
        // note: Recheck an already accepted body without calling any publisher. Temporary tokens cannot alter the loaded world or its paired manifest.
        var world = WorldStateManager.Instance.State;
        var plan = world.generatedWorldPlan;
        var settings = new JsonSerializerSettings { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
        var accepted = JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(JsonConvert.SerializeObject(plan.spatialPlanV2.acceptedContinuation, settings), settings);
        if (accepted == null) throw new InvalidOperationException("Current save has no accepted frontier body");
        string fingerprint = accepted.schemaVersion + "|" + accepted.revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + accepted.contentHash;
        var type = typeof(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance);
        var constructor = type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        var token = constructor.Invoke(new object[] { world, fingerprint, accepted, null });
        var prepare = type.GetMethod("TryPrepareForPublication", BindingFlags.NonPublic | BindingFlags.Instance);
        object[] arguments = { world, null, null, null };
        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool first = (bool)prepare.Invoke(token, arguments);
        double firstMs = watch.Elapsed.TotalMilliseconds;
        if (!first) throw new InvalidOperationException(arguments[3] as string);
        object projection = arguments[2];
        watch.Restart();
        bool repeated = (bool)prepare.Invoke(token, arguments);
        double repeatMs = watch.Elapsed.TotalMilliseconds;
        bool same = repeated && ReferenceEquals(projection, arguments[2]);
        var retired = constructor.Invoke(new object[] { world, "stale-verification-token", accepted, null });
        bool rejectedStale = !(bool)prepare.Invoke(retired, new object[] { world, null, null, null });
        Write("publication-cache-" + DateTime.UtcNow.ToString("HHmmss"), new { first, same, rejectedStale, firstMs, repeatMs,
            locations = accepted.locations.Count, evidence = "Read-only live validation on already accepted content; no publication", utc = DateTime.UtcNow });
        if (!same || !rejectedStale) throw new InvalidOperationException("Publication cache ownership contract failed");
    }
}
#endif
