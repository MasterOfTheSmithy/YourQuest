#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// note: Explicit assisted runtime witness: normal Continue, real keyboard events and the authoritative motor; no teleport or forced generation.
[InitializeOnLoad]
public static class YQStreamedTravelVerification
{
    private const string KeyPrefix = "YQStreamedTravel.";
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static InputSettings originalSettings, testSettings;
    private static bool moveHeld;
    private static YQInvestorPlayerMotor inputMotor;
    private static Vector3 desiredDirection;
    private static double nextReport;
    private static double nextTick;
    private static readonly HashSet<string> Visited = new HashSet<string>();
    private static readonly FieldInfo SiteId = typeof(YQCompiledWorldSiteInstance).GetField("settlementId", BindingFlags.NonPublic | BindingFlags.Instance);

    static YQStreamedTravelVerification() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    public static void RunFromCommandLine()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Idle owned batch Editor required.");
        EditorSceneManager.OpenScene("Assets/Assets/Scenes/YourQuest_PlaySafe.unity", OpenSceneMode.Single);
        var manifest = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(Application.persistentDataPath, "Profiles/profiles_manifest.json")));
        SessionState.SetString(KeyPrefix + "profile", (string)manifest["activeProfileId"]);
        SessionState.SetString(KeyPrefix + "started", DateTime.UtcNow.ToString("O"));
        SessionState.SetBool(KeyPrefix + "running", true);
        SessionState.SetBool(KeyPrefix + "ending", false);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!Application.isBatchMode || !SessionState.GetBool(KeyPrefix + "running", false) || EditorApplication.isCompiling) return;
        if (SessionState.GetBool(KeyPrefix + "ending", false))
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(KeyPrefix + "running", false);
            EditorApplication.Exit(SessionState.GetBool(KeyPrefix + "success", false) ? 0 : 1);
            return;
        }
        if (!EditorApplication.isPlaying) return;
        if (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(KeyPrefix + "started", ""), null, System.Globalization.DateTimeStyles.RoundtripKind) > TimeSpan.FromMinutes(12))
        { Finish(false, "Twelve minute assisted traversal bound elapsed; inspect partial receipts."); return; }
        // note: Observation stays at five hertz; held keyboard state continues between samples.
        if (EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .2d;
        if (!YQTitleScreenUI.StartupFlowComplete && YQTitleScreenUI.Instance != null)
            YQTitleScreenUI.Instance.ContinueExistingForDevelopmentVerification(SessionState.GetString(KeyPrefix + "profile", ""));
        if (!YourQuestTutorialAutoBootstrap.GameplayPresentationReleased) { Report("WAITING_FOR_CONTINUE", null); return; }
        var motor = UnityEngine.Object.FindFirstObjectByType<YQInvestorPlayerMotor>();
        var world = WorldStateManager.Instance?.State;
        var player = PlayerStateManager.Instance?.state;
        if (motor == null || !motor.IsAuthoritative || world == null || player == null) return;
        if (keyboard == null)
        {
            // note: A headless batch has no focused Game view; only this owned fixture ignores focus so its real device events reach the motor.
            Application.runInBackground = true;
            originalSettings = InputSystem.settings;
            testSettings = UnityEngine.Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>("StreamedTravelWitness");
            mouse = InputSystem.AddDevice<Mouse>("StreamedTravelWitnessMouse");
            InputSystem.onBeforeUpdate += QueueInput;
            InputSystem.onAfterUpdate += KeepDevicesCurrent;
        }
        inputMotor = motor;
        if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
        keyboard.MakeCurrent();
        var locations = world.generatedWorldPlan?.spatialPlanV2?.acceptedContinuation?.locations;
        GeneratedSpatialContinuationLocationV2Record destination = null;
        float nearest = float.PositiveInfinity;
        Vector3 logical = motor.transform.position + player.renderOrigin;
        if (locations != null)
            foreach (var location in locations)
            {
                string kind = location.anchor.kind.ToString();
                if (Visited.Contains(kind)) continue;
                float distance = Vector2.Distance(new Vector2(logical.x, logical.z), new Vector2(location.anchor.x, location.anchor.z));
                if (distance < nearest) { nearest = distance; destination = location; }
            }
        int npcs = 0, enemies = 0, gear = 0;
        bool loaded = false;
        if (destination != null)
        {
            foreach (var owner in UnityEngine.Object.FindObjectsByType<YQCompiledWorldSiteInstance>(FindObjectsSortMode.None))
                if ((string)SiteId.GetValue(owner) == destination.anchor.sourceSemanticId && owner.IsLoaded)
                {
                    int rendered = 0, colliders = 0;
                    foreach (Renderer renderer in owner.GetComponentsInChildren<Renderer>(false)) if (renderer.enabled) rendered++;
                    foreach (Collider collider in owner.GetComponentsInChildren<Collider>(false)) if (collider.enabled && !collider.isTrigger) colliders++;
                    loaded = rendered > 0 && colliders > 0;
                }
            foreach (var actor in UnityEngine.Object.FindObjectsByType<EntityInfo>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(actor.transform.position, motor.transform.position) > 200f) continue;
                if (actor.GetComponent<NpcDialogueAgent>() != null) npcs++;
                if (actor.GetComponent<YQInvestorEnemy>() != null) enemies++;
                var inventory = YQContainerInventory.Find(world, actor.entityId);
                if (inventory?.equippedItemBySlot.Count > 0 && actor.GetComponent<YQGeneratedActorEquipment>() != null)
                    foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(false))
                        if (renderer.enabled && IsEquipmentRenderer(renderer.transform, actor.transform)) { gear++; break; }
            }
            string kind = destination.anchor.kind.ToString();
            bool population = kind == "Settlement" ? npcs > 0 && gear > 0 : kind != "HostileSite" || enemies > 0;
            if (nearest < 180f && loaded && population) { Visited.Add(kind); Debug.Log("[YQStreamedTravel] WALKED_TO_" + kind); }
            if (!Visited.Contains(kind) && nearest > 45f)
            {
                Vector3 direction = new Vector3(destination.anchor.x - logical.x, 0f, destination.anchor.z - logical.z);
                desiredDirection = direction;
                // note: The existing G08 device routing supplies real mouse turns and keys; speed, physics and movement gates stay production-owned.
                SetMovementKeys(true);
            }
            else SetMovementKeys(false);
        }
        else SetMovementKeys(false);
        Report(destination == null ? "WAITING_FOR_ACCEPTED_SITE" : "WALKING", new { logical, nearest, loaded, npcs, enemies, gear,
            target = destination?.anchor.sourceSemanticId, visited = new List<string>(Visited), accepted = locations?.Count ?? 0,
            moveInput = motor.MoveInput.ToString(), inputGate = motor.CanProcessMovementInput, keyboardEnabled = keyboard.enabled,
            currentKeyboard = Keyboard.current == keyboard, timeScale = Time.timeScale });
        if (Visited.Count >= 3) Finish(true, "Motor traversal encountered all three streamed site categories.");
    }

    private static void SetMovementKeys(bool moving)
    {
        moveHeld = moving;
    }

    private static void QueueInput()
    {
        if (desktopRunning) { DesktopQueueInput(); return; }
        if (keyboard == null || mouse == null || inputMotor == null || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
        float angle = desiredDirection.sqrMagnitude > .001f ? Vector3.SignedAngle(inputMotor.transform.forward, desiredDirection, Vector3.up) : 0f;
        InputSystem.QueueStateEvent(keyboard, moveHeld ? new KeyboardState(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift) : new KeyboardState());
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(Mathf.Clamp(angle, -12f, 12f) / Mathf.Max(.001f, inputMotor.sensitivityX), 0f) });
        KeepDevicesCurrent();
    }

    private static void KeepDevicesCurrent()
    {
        keyboard?.MakeCurrent(); mouse?.MakeCurrent();
    }

    private static bool IsEquipmentRenderer(Transform node, Transform actor)
    {
        for (; node != null && node != actor; node = node.parent)
            if (node.name.StartsWith("Equipped__", StringComparison.Ordinal)) return true;
        return false;
    }

    private static void Report(string status, object witness)
    {
        if (EditorApplication.timeSinceStartup < nextReport) return;
        nextReport = EditorApplication.timeSinceStartup + 15d;
        Directory.CreateDirectory("outputs/StreamedRepair_20261005");
        File.AppendAllText("outputs/StreamedRepair_20261005/TravelWitness.jsonl", JsonConvert.SerializeObject(new { utc = DateTime.UtcNow, status, witness },
            new JsonSerializerSettings { Converters = { new Vector3JsonConverter() } }) + Environment.NewLine);
        Debug.Log("[YQStreamedTravel] " + status + " " + JsonConvert.SerializeObject(witness, new JsonSerializerSettings { Converters = { new Vector3JsonConverter() } }));
    }

    private static void Finish(bool success, string reason)
    {
        Directory.CreateDirectory("outputs/StreamedRepair_20261005");
        InputSystem.onBeforeUpdate -= QueueInput; InputSystem.onAfterUpdate -= KeepDevicesCurrent;
        if (keyboard != null) { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.RemoveDevice(keyboard); keyboard = null; }
        if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
        if (originalSettings != null) InputSystem.settings = originalSettings;
        if (testSettings != null) UnityEngine.Object.DestroyImmediate(testSettings);
        File.WriteAllText("outputs/StreamedRepair_20261005/TravelResult.json", JsonConvert.SerializeObject(new { success, reason, visited = new List<string>(Visited) }, Formatting.Indented));
        SessionState.SetBool(KeyPrefix + "success", success);
        SessionState.SetBool(KeyPrefix + "ending", true);
        EditorApplication.isPlaying = false;
    }

    // note: Desktop observation is an explicit bounded input leg on a disposable profile, never a route certificate or a second motor.
    private static bool desktopRunning, desktopFinishing, desktopOriginalBackground, desktopInputCaptured;
    private static Keyboard desktopOriginalKeyboard;
    private static Mouse desktopOriginalMouse;
    private static YQPlayerFollowingSemanticChunkStreamer desktopStreamer;
    private static YQGeneratedWorldPlayerFallSafety desktopSafety;
    private static CharacterController desktopController;
    private static object desktopWorld, desktopPlayer, desktopSeed;
    private static string desktopScene;
    private static string desktopProfile, desktopArtifact, desktopDirectory, desktopAbort;
    private static Vector3 desktopTarget;
    private static double desktopStarted, desktopDeadline, desktopLastProgress, desktopNextSample;
    private static float desktopBestDistance;
    private static int desktopInitialRecoveries, desktopInputFrames, desktopMotorInputFrames, desktopLastObservedFrame;
    private static readonly Newtonsoft.Json.Linq.JArray DesktopSamples = new Newtonsoft.Json.Linq.JArray();
    private static readonly Newtonsoft.Json.Linq.JArray DesktopErrors = new Newtonsoft.Json.Linq.JArray();
    private static readonly MethodInfo DesktopVisualCoverage = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod(
        "TryValidateCurrentVisualCoverage", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo DesktopNeighborReadiness = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod(
        "CountReadyOccupiedNeighbors", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo DesktopEntryReadiness = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod(
        "IsReadyForPlayerEntry", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo DesktopRuntimeChunk = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod(
        "GetRuntimeChunk", BindingFlags.Instance | BindingFlags.NonPublic);

    [MenuItem("YourQuest/Terrain Refactor/Begin Disposable Assisted Forward Travel")]
    public static void BeginDesktopForward()
    {
        var motor = YQInvestorPlayerMotor.ActiveMotor;
        var player = PlayerStateManager.Instance?.state;
        if (motor == null || player == null) throw new InvalidOperationException("Active player is unavailable.");
        Vector3 direction = motor.transform.forward; direction.y = 0f;
        BeginDesktopTarget(motor.transform.position + player.renderOrigin + direction.normalized * 256f, 180f);
    }

    [MenuItem("YourQuest/Terrain Refactor/Begin Disposable Assisted East Travel")]
    public static void BeginDesktopEast() => BeginDesktopAxis(Vector3.right);

    [MenuItem("YourQuest/Terrain Refactor/Begin Disposable Assisted North Travel")]
    public static void BeginDesktopNorth() => BeginDesktopAxis(Vector3.forward);

    // note: Reverse legs use the identical guarded input path to revisit loaded areas without a teleport or a second travel owner.
    [MenuItem("YourQuest/Terrain Refactor/Begin Disposable Assisted West Travel")]
    public static void BeginDesktopWest() => BeginDesktopAxis(Vector3.left);

    [MenuItem("YourQuest/Terrain Refactor/Begin Disposable Assisted South Travel")]
    public static void BeginDesktopSouth() => BeginDesktopAxis(Vector3.back);

    private static void BeginDesktopAxis(Vector3 direction)
    {
        // note: Explicit cardinal legs share the same finite-target input/motor path, admission guards and cleanup.
        var motor = YQInvestorPlayerMotor.ActiveMotor;
        var player = PlayerStateManager.Instance?.state;
        if (motor == null || player == null) throw new InvalidOperationException("Active player is unavailable.");
        BeginDesktopTarget(motor.transform.position + player.renderOrigin + direction * 256f, 180f);
    }

    private static string DesktopBoundDiagnosticText(string value, int limit = 256) =>
        value == null || value.Length <= limit ? value : value.Substring(0, limit);

    private static object DesktopDeviceMetadata(InputDevice device)
    {
        if (device == null) return null;
        var description = device.description;
        return new { name = DesktopBoundDiagnosticText(device.name), device.deviceId, device.native, device.added, device.enabled,
            layout = DesktopBoundDiagnosticText(device.layout), currentKeyboard = device == Keyboard.current,
            currentMouse = device == Mouse.current, description = new {
                interfaceName = DesktopBoundDiagnosticText(description.interfaceName),
                deviceClass = DesktopBoundDiagnosticText(description.deviceClass), manufacturer = DesktopBoundDiagnosticText(description.manufacturer),
                product = DesktopBoundDiagnosticText(description.product),
                version = DesktopBoundDiagnosticText(description.version) } };
    }

    private static void DesktopWriteAdmissionRejection(string name, YQInvestorPlayerMotor motor)
    {
        // note: Diagnose only the named disposable profile, before any private device/settings changes; preserve the original rejection even if receipt I/O fails.
        if (!IsDesktopDisposableProfile(name)) return;
        try
        {
            var predicates = new Dictionary<string, bool> {
                ["noDesktopRun"] = !desktopRunning, ["noCapturedInput"] = !desktopInputCaptured, ["noOwnedKeyboard"] = keyboard == null,
                ["desktopMode"] = !Application.isBatchMode, ["noBatchRun"] = !SessionState.GetBool(KeyPrefix + "running", false),
                ["playing"] = EditorApplication.isPlaying, ["notPaused"] = !EditorApplication.isPaused,
                ["gameplayReleased"] = YourQuestTutorialAutoBootstrap.GameplayPresentationReleased,
                ["namedDisposableProfile"] = IsDesktopDisposableProfile(name), ["noProtectedTestLease"] = !YQDeveloperTestSession.Active,
                ["noPersistenceBarrier"] = !YQDeveloperConsoleGate.BlocksPersistence, ["motorPresent"] = motor != null,
                ["authoritativeMotor"] = motor != null && motor.IsAuthoritative,
                ["canProcessMovementInput"] = motor != null && motor.CanProcessMovementInput,
                ["groundedOrSwimming"] = motor != null && (motor.IsGrounded || motor.IsSwimming), ["notRagdoll"] = motor != null && !motor.IsRagdoll,
                ["notRecovering"] = motor != null && !motor.IsRecovering, ["notAutoWalking"] = motor != null && !motor.IsAutoWalking,
                ["stationaryPlanarVelocity"] = motor != null && motor.PlanarVelocity.sqrMagnitude <= .25f,
                ["noMovementInput"] = motor != null && motor.MoveInput.sqrMagnitude <= .001f,
                ["vitalsPresent"] = motor != null && motor.vitals != null, ["alive"] = motor != null && motor.vitals != null && !motor.vitals.IsDead,
                ["worldPlanPresent"] = WorldStateManager.Instance?.State?.generatedWorldPlan != null,
                ["playerStatePresent"] = PlayerStateManager.Instance?.state != null,
                ["visibleReadinessApi"] = DesktopVisualCoverage != null, ["neighborReadinessApi"] = DesktopNeighborReadiness != null,
                ["entryReadinessApi"] = DesktopEntryReadiness != null, ["chunkReadinessApi"] = DesktopRuntimeChunk != null,
                ["keyboardPresent"] = Keyboard.current != null, ["mousePresent"] = Mouse.current != null,
                ["keyboardNative"] = Keyboard.current != null && Keyboard.current.native,
                ["mouseNative"] = Mouse.current != null && Mouse.current.native,
                ["keyboardEnabled"] = Keyboard.current != null && Keyboard.current.enabled,
                ["mouseEnabled"] = Mouse.current != null && Mouse.current.enabled
            };
            var failed = new List<string>();
            foreach (var predicate in predicates) if (!predicate.Value) failed.Add(predicate.Key);
            var devices = new List<object>(); int deviceCount = 0;
            foreach (InputDevice device in InputSystem.devices)
                if (device is Keyboard || device is Mouse)
                {
                    deviceCount++;
                    if (devices.Count < 64) devices.Add(DesktopDeviceMetadata(device));
                }
            var capsule = motor != null ? motor.GetComponent<CharacterController>() : null;
            var plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
            string directory = "outputs/TerrainRefactor_20261014_StreamingRetest/AssistedDesktopTravel";
            Directory.CreateDirectory(directory);
            // note: One bounded latest receipt avoids an unbounded rejection log; capability strings and device count have explicit limits.
            File.WriteAllText(Path.Combine(directory, "AdmissionGuardLatest.json"), JsonConvert.SerializeObject(new {
                evidence = "READ_ONLY_ADMISSION_REJECTION; no assisted run or input mutation occurred", utc = DateTime.UtcNow.ToString("O"),
                profileId = YQProfileSaveSystem.Instance?.ActiveProfileId, profileName = DesktopBoundDiagnosticText(name),
                worldSeed = plan?.worldSeed, artifactHash = plan?.spatialPlanV2?.contentHash,
                scene = motor != null ? motor.gameObject.scene.path : null, predicates, failedPredicates = failed,
                currentKeyboard = DesktopDeviceMetadata(Keyboard.current), currentMouse = DesktopDeviceMetadata(Mouse.current),
                keyboardMouseDeviceCount = deviceCount, deviceMetadataTruncated = deviceCount > 64, devices,
                context = new { Application.isFocused, Application.runInBackground, Time.timeScale,
                    modalBlocked = RuntimeModalUiBlocker.IsBlocked, pointerCaptured = YourQuestTutorialMenuUI.CapturesPointerInput,
                    initialGenerationGameplayLocked = YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked,
                    settingsPresent = InputSystem.settings != null,
                    backgroundBehavior = InputSystem.settings != null ? InputSystem.settings.backgroundBehavior.ToString() : null,
                    editorInputBehavior = InputSystem.settings != null ? InputSystem.settings.editorInputBehaviorInPlayMode.ToString() : null,
                    motorActiveEnabled = motor != null && motor.isActiveAndEnabled,
                    planarVelocity = motor != null ? motor.PlanarVelocity : Vector3.zero,
                    movementInput = motor != null ? motor.MoveInput.ToString() : null,
                    verticalVelocity = motor != null ? (float?)motor.VerticalVelocity : null,
                    capsulePresent = capsule != null, capsuleEnabled = capsule != null && capsule.enabled,
                    capsuleDetectCollisions = capsule != null && capsule.detectCollisions,
                    cameraPivotPresent = motor != null && motor.cameraPivot != null, playerCameraPresent = motor != null && motor.playerCamera != null }
            }, Formatting.Indented, new Vector3JsonConverter()));
            Debug.LogWarning("[YQStreamedTravel] Admission rejected: " + string.Join(", ", failed) + "; read-only details: " + directory + "/AdmissionGuardLatest.json");
        }
        catch (Exception exception) { Debug.LogWarning("[YQStreamedTravel] Admission receipt failed: " + exception.Message); }
    }

    public static void BeginDesktopTarget(Vector3 logicalTarget, float timeoutSeconds = 180f)
    {
        var profiles = YQProfileSaveSystem.Instance;
        var motor = YQInvestorPlayerMotor.ActiveMotor;
        var world = WorldStateManager.Instance?.State;
        var player = PlayerStateManager.Instance?.state;
        string name = profiles?.FindProfile(profiles.ActiveProfileId)?.displayName;
        if (desktopRunning || desktopInputCaptured || keyboard != null || Application.isBatchMode ||
            SessionState.GetBool(KeyPrefix + "running", false) || !EditorApplication.isPlaying || EditorApplication.isPaused ||
            !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased || !IsDesktopDisposableProfile(name) ||
            YQDeveloperTestSession.Active || YQDeveloperConsoleGate.BlocksPersistence || motor == null || !motor.IsAuthoritative ||
            // note: Stationary swimming is an ordinary movement state; retain all production readiness and physical-input guards for water traversal.
            !motor.CanProcessMovementInput || !(motor.IsGrounded || motor.IsSwimming) || motor.IsRagdoll || motor.IsRecovering || motor.IsAutoWalking ||
            motor.PlanarVelocity.sqrMagnitude > .25f || motor.MoveInput.sqrMagnitude > .001f || motor.vitals == null || motor.vitals.IsDead ||
            world?.generatedWorldPlan == null || player == null || DesktopVisualCoverage == null || DesktopNeighborReadiness == null ||
            DesktopEntryReadiness == null || DesktopRuntimeChunk == null || Keyboard.current == null || Mouse.current == null ||
            !Keyboard.current.native || !Mouse.current.native || !Keyboard.current.enabled || !Mouse.current.enabled)
        {
            DesktopWriteAdmissionRejection(name, motor);
            throw new InvalidOperationException("Desktop travel requires a stationary released Terrain Apron Playtest player, physical input devices and no protected test lease.");
        }
        float distance = DesktopPlanarDistance(logicalTarget, motor.transform.position + player.renderOrigin);
        if (!IsDesktopLegBounded(logicalTarget.x, logicalTarget.y, logicalTarget.z, distance, timeoutSeconds))
            throw new InvalidOperationException("Specify a finite target 2-512 metres away and a 1-180 second deadline.");
        desktopStreamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        desktopSafety = UnityEngine.Object.FindFirstObjectByType<YQGeneratedWorldPlayerFallSafety>();
        desktopController = motor.GetComponent<CharacterController>();
        if (desktopStreamer == null || desktopSafety == null || desktopController == null || !desktopController.enabled ||
            !desktopController.detectCollisions || !desktopStreamer.TryValidateCurrentTraversability(motor.transform.position,
                desktopController.radius, desktopController.skinWidth, out _))
            throw new InvalidOperationException("Current production capsule/streamer/fall-safety evidence is unavailable or unready.");

        inputMotor = motor; desktopWorld = world; desktopPlayer = player;
        desktopSeed = world.generatedWorldPlan?.worldSeed; desktopScene = motor.gameObject.scene.path;
        desktopProfile = profiles.ActiveProfileId; desktopArtifact = world.generatedWorldPlan?.spatialPlanV2?.contentHash;
        desktopTarget = logicalTarget; desktopBestDistance = distance;
        desktopStarted = desktopLastProgress = desktopNextSample = EditorApplication.timeSinceStartup;
        desktopDeadline = desktopStarted + timeoutSeconds; desktopInitialRecoveries = desktopSafety.RecoveryCount;
        desktopInputFrames = desktopMotorInputFrames = 0; desktopLastObservedFrame = -1; desktopAbort = null; DesktopSamples.Clear(); DesktopErrors.Clear();
        desktopDirectory = Path.Combine("outputs/TerrainRefactor_20261014_StreamingRetest/AssistedDesktopTravel/Runs",
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(desktopDirectory);
        try
        {
            // note: Capture exact owners before creating private devices; originals are never disabled, reset, removed or rebound.
            originalSettings = InputSystem.settings; desktopOriginalBackground = Application.runInBackground;
            desktopOriginalKeyboard = Keyboard.current; desktopOriginalMouse = Mouse.current; desktopInputCaptured = true;
            testSettings = UnityEngine.Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings; Application.runInBackground = true;
            keyboard = InputSystem.AddDevice<Keyboard>("StreamedDesktopTravelWitness");
            mouse = InputSystem.AddDevice<Mouse>("StreamedDesktopTravelWitnessMouse");
            desktopRunning = true;
            InputSystem.onBeforeUpdate += QueueInput; InputSystem.onAfterUpdate += DesktopAfterInput;
            InputSystem.onEvent += DesktopPhysicalAbort;
            EditorApplication.update += DesktopTick; EditorApplication.playModeStateChanged += DesktopPlayState;
            AssemblyReloadEvents.beforeAssemblyReload += DesktopLifetimeAbort; EditorApplication.quitting += DesktopLifetimeAbort;
            Application.logMessageReceived += DesktopObserveLog;
            DesktopCapture();
            Debug.Log("[YQStreamedTravel] ASSISTED desktop leg started; original keyboard Escape cancels. Target is a finite direction, not a certified route.");
        }
        catch { DesktopFinish("ABORTED_SETUP_EXCEPTION"); throw; }
    }

    [MenuItem("YourQuest/Terrain Refactor/Cancel Disposable Assisted Travel")]
    public static void CancelDesktop() => DesktopFinish("ABORTED_USER_CANCEL");

    private static bool IsDesktopDisposableProfile(string name) => name != null &&
        name.StartsWith("Terrain Apron Playtest ", StringComparison.Ordinal);

    private static bool IsDesktopLegBounded(float x, float y, float z, float distance, float seconds) =>
        !float.IsNaN(x) && !float.IsInfinity(x) && !float.IsNaN(y) && !float.IsInfinity(y) &&
        !float.IsNaN(z) && !float.IsInfinity(z) && distance >= 2f && distance <= 512f && seconds >= 1f && seconds <= 180f;

    private static float DesktopPlanarDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    private static string DesktopTerminalReason(bool contextValid, bool abort, bool recovered, float distance, int inputFrames,
        double now, double deadline, double lastProgress)
    {
        if (abort) return "ABORTED_REQUESTED";
        if (!contextValid) return "ABORTED_CONTEXT_CHANGED";
        if (recovered) return "FAILED_FALL_SAFETY_RECOVERY";
        if (distance <= 1.5f) return inputFrames > 0 ? "ASSISTED_TARGET_REACHED" : "NOT_VERIFIED_NO_INPUT";
        if (now >= deadline) return "INCOMPLETE_DEADLINE";
        if (now - lastProgress >= 20d) return "INCOMPLETE_NO_TARGET_PROGRESS_OBSTACLE_OR_BOUNDARY";
        return null;
    }

    private static bool DesktopCapturedLocationMatches(object seed, string scene, object currentSeed, string currentScene) =>
        seed != null && !string.IsNullOrEmpty(scene) && Equals(seed, currentSeed) && string.Equals(scene, currentScene, StringComparison.Ordinal);

    private static bool DesktopContextValid() => EditorApplication.isPlaying && !EditorApplication.isPaused &&
        YourQuestTutorialAutoBootstrap.GameplayPresentationReleased && !YQDeveloperTestSession.Active && !YQDeveloperConsoleGate.BlocksPersistence &&
        YQProfileSaveSystem.Instance?.ActiveProfileId == desktopProfile && ReferenceEquals(desktopWorld, WorldStateManager.Instance?.State) &&
        ReferenceEquals(desktopPlayer, PlayerStateManager.Instance?.state) &&
        WorldStateManager.Instance?.State?.generatedWorldPlan?.spatialPlanV2?.contentHash == desktopArtifact &&
        inputMotor != null && DesktopCapturedLocationMatches(desktopSeed, desktopScene,
            WorldStateManager.Instance?.State?.generatedWorldPlan?.worldSeed, inputMotor.gameObject.scene.path) &&
        inputMotor.IsAuthoritative && inputMotor.CanProcessMovementInput && inputMotor.vitals != null && !inputMotor.vitals.IsDead &&
        desktopStreamer != null && desktopStreamer == YQPlayerFollowingSemanticChunkStreamer.Active && desktopSafety != null &&
        desktopController != null && desktopController.detectCollisions && InputSystem.settings == testSettings &&
        keyboard != null && keyboard.added && keyboard.enabled && mouse != null && mouse.added && mouse.enabled &&
        desktopOriginalKeyboard != null && desktopOriginalKeyboard.added && desktopOriginalKeyboard.enabled &&
        desktopOriginalMouse != null && desktopOriginalMouse.added && desktopOriginalMouse.enabled;

    private static void DesktopQueueInput()
    {
        if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
        if (!DesktopContextValid()) desktopAbort = desktopAbort ?? "ABORTED_CONTEXT_CHANGED";
        if (EditorApplication.timeSinceStartup >= desktopDeadline) desktopAbort = desktopAbort ?? "INCOMPLETE_DEADLINE";
        if (desktopOriginalKeyboard != null && desktopOriginalKeyboard.escapeKey.isPressed) desktopAbort = "ABORTED_PHYSICAL_ESCAPE";
        if (keyboard == null || mouse == null || !keyboard.added || !mouse.added) return;
        Vector3 logical = inputMotor != null && PlayerStateManager.Instance?.state != null
            ? inputMotor.transform.position + PlayerStateManager.Instance.state.renderOrigin : desktopTarget;
        Vector3 delta = desktopTarget - logical; delta.y = 0f;
        float angle = inputMotor != null && delta.sqrMagnitude > .001f ? Vector3.SignedAngle(inputMotor.transform.forward, delta, Vector3.up) : 0f;
        bool move = desktopAbort == null && delta.magnitude > 1.5f && Mathf.Abs(angle) < 35f && !inputMotor.IsRagdoll && !inputMotor.IsRecovering;
        InputSystem.QueueStateEvent(keyboard, move ? new KeyboardState(Key.W) : new KeyboardState());
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = desktopAbort == null
            ? new Vector2(Mathf.Clamp(angle, -12f, 12f) / Mathf.Max(.001f, inputMotor.sensitivityX), 0f) : Vector2.zero });
        if (move) desktopInputFrames++;
        if (desktopAbort == null) KeepDevicesCurrent();
    }

    private static void DesktopPhysicalAbort(InputEventPtr evt, InputDevice device)
    {
        // note: Observe the retained physical device, even while the synthetic keyboard is current. Never consume the user's event.
        if (desktopRunning && device == desktopOriginalKeyboard && (evt.IsA<StateEvent>() || evt.IsA<DeltaStateEvent>()) &&
            desktopOriginalKeyboard.escapeKey.ReadValueFromEvent(evt, out float value) && value > .5f)
            desktopAbort = "ABORTED_PHYSICAL_ESCAPE";
    }

    private static void DesktopAfterInput()
    {
        if (!desktopRunning) return;
        if (desktopAbort == null) KeepDevicesCurrent();
        else
        {
            if (keyboard != null && keyboard.added) InputSystem.ResetDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.ResetDevice(mouse);
        }
    }

    private static void DesktopTick()
    {
        if (!desktopRunning) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            bool current = DesktopContextValid();
            if (current && Time.frameCount != desktopLastObservedFrame)
            {
                desktopLastObservedFrame = Time.frameCount;
                if (inputMotor.MoveInput.sqrMagnitude > .01f) desktopMotorInputFrames++;
            }
            float distance = current ? DesktopPlanarDistance(desktopTarget, inputMotor.transform.position + PlayerStateManager.Instance.state.renderOrigin) : float.PositiveInfinity;
            if (distance < desktopBestDistance - .5f) { desktopBestDistance = distance; desktopLastProgress = now; }
            string terminal = DesktopTerminalReason(current, desktopAbort != null, desktopSafety != null &&
                desktopSafety.RecoveryCount != desktopInitialRecoveries, distance, desktopMotorInputFrames, now, desktopDeadline, desktopLastProgress);
            if (now >= desktopNextSample) { desktopNextSample = now + 1d; DesktopCapture(); }
            if (terminal != null) DesktopFinish(desktopAbort ?? terminal);
        }
        catch (Exception exception) { DesktopErrors.Add(exception.ToString()); DesktopFinish("ABORTED_OBSERVER_EXCEPTION"); }
    }

    private static bool DesktopReadOccupiedReadiness(Vector2Int occupied, out int readyNine)
    {
        // note: Query the production all-asset entry predicates; publication flags alone omit shared-site actor and activation dependencies.
        readyNine = (int)DesktopNeighborReadiness.Invoke(desktopStreamer, null);
        object owner = DesktopRuntimeChunk.Invoke(desktopStreamer, new object[] { occupied });
        return owner != null && (bool)DesktopEntryReadiness.Invoke(desktopStreamer, new object[] { occupied, owner });
    }

    private static void DesktopCapture()
    {
        if (inputMotor == null || desktopStreamer == null || desktopController == null || desktopSafety == null) return;
        var neighborhood = new List<object>();
        Vector2Int occupied = desktopStreamer.CurrentChunk;
        for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
        {
            Vector2Int cell = occupied + new Vector2Int(x, z);
            bool known = desktopStreamer.TryGetPublicationSnapshot(cell, out var snapshot);
            neighborhood.Add(new { occupied = x == 0 && z == 0, known, snapshot });
        }
        bool occupiedAllAssetReady = DesktopReadOccupiedReadiness(occupied, out int occupiedNeighborhoodAllAssetReady);
        bool capsuleReady = desktopStreamer.TryValidateCurrentTraversability(inputMotor.transform.position,
            desktopController.radius, desktopController.skinWidth, out string capsuleFailure);
        // note: This existing query does not refresh demand; stage snapshots are not substituted for full entry or visible readiness.
        object[] arguments = { null };
        bool visibleReady = (bool)DesktopVisualCoverage.Invoke(desktopStreamer, arguments);
        var sample = Newtonsoft.Json.Linq.JObject.FromObject(new { utc = DateTime.UtcNow.ToString("O"),
            seconds = EditorApplication.timeSinceStartup - desktopStarted, frame = Time.frameCount,
            realFrameSeconds = Time.unscaledDeltaTime, renderPosition = inputMotor.transform.position,
            logicalPosition = inputMotor.transform.position + PlayerStateManager.Instance.state.renderOrigin,
            target = desktopTarget, moveInput = inputMotor.MoveInput.ToString(), velocity = inputMotor.PlanarVelocity,
            verticalVelocity = inputMotor.VerticalVelocity, inputGate = inputMotor.CanProcessMovementInput,
            grounded = inputMotor.IsGrounded, swimming = inputMotor.IsSwimming, ragdoll = inputMotor.IsRagdoll, health = inputMotor.vitals.CurrentHealth,
            recoveryCount = desktopSafety.RecoveryCount, recoveryDelta = desktopSafety.RecoveryCount - desktopInitialRecoveries,
            capsuleReady, capsuleFailure, occupiedAllAssetReady, occupiedNeighborhoodAllAssetReady, occupiedNeighborhoodTotal = 9,
            visibleReady, visibleFailure = arguments[0], neighborhood,
            failedChunks = desktopStreamer.FailedChunkCount, syntheticKeyboardCurrent = Keyboard.current == keyboard,
            syntheticMouseCurrent = Mouse.current == mouse, inputUpdatesQueued = desktopInputFrames, motorInputFramesObserved = desktopMotorInputFrames },
            JsonSerializer.Create(new JsonSerializerSettings { Converters = { new Vector3JsonConverter() } }));
        DesktopSamples.Add(sample);
        File.AppendAllText(Path.Combine(desktopDirectory, "Samples.jsonl"), sample.ToString(Formatting.None) + Environment.NewLine);
    }

    private static void DesktopObserveLog(string message, string stack, LogType type)
    {
        if (DesktopErrors.Count < 32 && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert ||
            message.Contains("Player fall-through recovered"))) DesktopErrors.Add(message + "\n" + stack);
    }

    private static void DesktopPlayState(PlayModeStateChange state)
    { if (state == PlayModeStateChange.ExitingPlayMode) DesktopLifetimeAbort(); }
    private static void DesktopLifetimeAbort() => DesktopFinish("ABORTED_EDITOR_LIFECYCLE");

    private static void DesktopFinish(string reason)
    {
        if (desktopFinishing || (!desktopRunning && !desktopInputCaptured)) return;
        desktopFinishing = true; desktopRunning = false;
        try
        {
            DesktopReleaseInput();
            File.WriteAllText(Path.Combine(desktopDirectory, "Result.json"), JsonConvert.SerializeObject(new {
                evidence = "ASSISTED native production input/motor observation; not ordinary input, route, bridge or performance certification",
                reason, profileId = desktopProfile, artifactHash = desktopArtifact, worldSeed = desktopSeed, scene = desktopScene,
                seconds = EditorApplication.timeSinceStartup - desktopStarted, target = desktopTarget,
                inputUpdatesQueued = desktopInputFrames, motorInputFramesObserved = desktopMotorInputFrames, samples = DesktopSamples.Count, errors = DesktopErrors,
                settingsRestored = InputSystem.settings == originalSettings,
                originalKeyboardCurrent = Keyboard.current == desktopOriginalKeyboard,
                originalMouseCurrent = Mouse.current == desktopOriginalMouse,
                runInBackgroundRestored = Application.runInBackground == desktopOriginalBackground
            }, Formatting.Indented, new Vector3JsonConverter()));
            Debug.Log("[YQStreamedTravel] ASSISTED desktop leg ended: " + reason + "; evidence: " + desktopDirectory);
        }
        finally { DesktopClearContext(); desktopFinishing = false; }
    }

    private static void DesktopClearContext()
    {
        // note: The terminal receipt is already written; a stopped observer must not retain the previous profile graph or native wrappers.
        inputMotor = null; desktopWorld = null; desktopPlayer = null; desktopSeed = null;
        desktopStreamer = null; desktopSafety = null; desktopController = null;
        desktopOriginalKeyboard = null; desktopOriginalMouse = null; originalSettings = null; testSettings = null;
        keyboard = null; mouse = null;
        desktopProfile = null; desktopArtifact = null; desktopScene = null; desktopDirectory = null; desktopAbort = null;
        DesktopSamples.Clear(); DesktopErrors.Clear();
    }

    private static void DesktopReleaseInput()
    {
        // note: Remove only owned handlers/devices on every exit, including partial setup; preserve a newer external settings owner on interference.
        InputSystem.onBeforeUpdate -= QueueInput; InputSystem.onAfterUpdate -= DesktopAfterInput; InputSystem.onEvent -= DesktopPhysicalAbort;
        EditorApplication.update -= DesktopTick; EditorApplication.playModeStateChanged -= DesktopPlayState;
        AssemblyReloadEvents.beforeAssemblyReload -= DesktopLifetimeAbort; EditorApplication.quitting -= DesktopLifetimeAbort;
        Application.logMessageReceived -= DesktopObserveLog;
        if (!desktopInputCaptured) return;
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        keyboard = null; mouse = null;
        if (desktopOriginalKeyboard != null && desktopOriginalKeyboard.added) desktopOriginalKeyboard.MakeCurrent();
        if (desktopOriginalMouse != null && desktopOriginalMouse.added) desktopOriginalMouse.MakeCurrent();
        if (testSettings != null && InputSystem.settings == testSettings) InputSystem.settings = originalSettings;
        if (testSettings != null) UnityEngine.Object.DestroyImmediate(testSettings);
        testSettings = null; Application.runInBackground = desktopOriginalBackground; desktopInputCaptured = false;
    }

}
#endif
