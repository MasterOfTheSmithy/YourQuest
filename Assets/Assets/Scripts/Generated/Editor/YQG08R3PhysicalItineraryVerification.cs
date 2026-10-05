#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// note: Explicit R3 diagnostics observe and drive the existing capsule through ordinary devices; accepted world records remain authoritative.
internal static class YQG08R3PhysicalItineraryVerification
{
    [Serializable]
    private sealed class Request
    {
        public string action = "snapshot";
        public string rowId;
        public string profileId;
        public string artifactHash;
        public string featureId;
        public Vector3[] waypoints;
        public float timeoutSeconds = 180f;
        public float tolerance = .65f;
        public bool sprint;
        public float viewYaw;
        public float viewPitch;
        public float viewSeconds = 3f;
    }

    private const string Root = "outputs/G08_R3_Physical_20261003";
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
    };
    private static readonly PropertyInfo BlockingContact = typeof(YQInvestorPlayerMotor).GetProperty(
        "LastBlockingControllerCollider", BindingFlags.Instance | BindingFlags.NonPublic);
    private static YQInvestorPlayerMotor motor;
    private static CharacterController controller;
    private static Keyboard keyboard, originalKeyboard;
    private static Mouse mouse, originalMouse;
    private static InputSettings originalSettings, testSettings;
    private static bool originalBackground;
    private static Vector3 originalPosition;
    private static Quaternion originalRotation;
    private static Request request;
    private static StreamWriter trace;
    private static StreamWriter supportTrace;
    private static readonly RaycastHit[] supportHits = new RaycastHit[64];
    private static string sessionDirectory, legDirectory, profileId, artifactHash;
    private static int waypointIndex, lastFrame, observedFrames, inputFrames, unsafeFrames;
    private static int probeWarningFrames, groundedProbeWarningFrames;
    private static double started, lastObservation, lastProgress, deadline;
    private static Vector3 previousPosition, progressPosition;
    private static float travelled, maximumFrame;
    private static bool running, protectedSession;
    private static int viewCount;
    private static double nextView;
    private static Vector3 observationPosition;
    private static bool restoring;
    private static double restoreDeadline;
    private static Action restorePose;
    private static Dictionary<string, string> persistedHashes;
    private static YQRouteTraversalProbe probe;
    private static YQRouteTraversalProbe.Shape shape;

    internal static void Dispatch(string command)
    {
        // note: Reject cross-profile commands and invalid geometry before acquiring input or changing transient test settings.
        try
        {
            Request next = JsonUtility.FromJson<Request>(command);
            if (next == null) throw new InvalidOperationException("R3 requires a structured command.");
            if (next.action == "restore") { Restore(); return; }
            RequireRuntime();
            if (next.action == "snapshot")
            {
                if (running) throw new InvalidOperationException("Read-only discovery requires an idle traversal observer.");
                Snapshot(NewDirectory("discovery")); return;
            }
            if (next.action == "hydrology-snapshot")
            {
                // note: A read-only census needs no rollback lease; retain exact live identity and keep it separate from protected traversal evidence.
                var livePlan = WorldStateManager.Instance?.State?.generatedWorldPlan;
                if (running || next.profileId != YQProfileSaveSystem.Instance?.ActiveProfileId ||
                    next.artifactHash != livePlan?.spatialPlanV2?.contentHash)
                    throw new InvalidOperationException("Read-only hydrology census requires the matching active profile and artifact.");
                string directory = NewDirectory("read_only_hydrology_census");
                Snapshot(directory);
                CaptureHydrology(directory);
                return;
            }
            if (next.action == "begin") { Begin(); return; }
            if (next.action == "hydrology")
            {
                // note: A protected read-only census correlates ordinary camera visibility with actual water geometry, without staging movement.
                if (!protectedSession || running || next.profileId != profileId || next.artifactHash != artifactHash)
                    throw new InvalidOperationException("Hydrology census requires the idle protected session and matching accepted identity.");
                string directory = NewDirectory("hydrology_census");
                Snapshot(directory);
                CaptureHydrology(directory);
                return;
            }
            if (next.action != "walk" && next.action != "observe") throw new InvalidOperationException("Unknown R3 action.");
            StartLeg(next);
        }
        catch (Exception exception)
        {
            if (running) FinishLeg("FAIL", exception.ToString());
            else if (testSettings != null) ReleaseInput();
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "Rejected_" + Stamp() + ".txt"), exception.ToString());
            Debug.LogError("[G08-R3] Request rejected: " + exception.Message);
        }
    }

    private static void RequireRuntime()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling ||
            SceneManager.GetActiveScene().path != "Assets/Assets/Scenes/YourQuest_PlaySafe.unity" ||
            !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased || Time.timeScale <= 0f)
            throw new InvalidOperationException("R3 requires released, unpaused production PlaySafe.");
        if (YQSemanticChunkRuntimeVerification.IsRunning)
            throw new InvalidOperationException("Another runtime verifier owns this session.");
    }

    private static void Begin()
    {
        if (protectedSession || running || YQDeveloperTestSession.Active)
            throw new InvalidOperationException("A protected test session already exists.");
        motor = YQInvestorPlayerMotor.ActiveMotor;
        controller = motor != null ? motor.GetComponent<CharacterController>() : null;
        if (motor == null || !motor.IsAuthoritative || !motor.CanProcessMovementInput ||
            !YQRouteTraversalProbe.Shape.TryCapture(controller, out shape))
            throw new InvalidOperationException("The authoritative capsule/input is not ready.");
        var plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out _, out string failure))
            throw new InvalidOperationException("Accepted V2 materialization unavailable: " + failure);
        sessionDirectory = NewDirectory("session");
        // note: Publish baseline identity and actual player view before the save barrier is acquired.
        Snapshot(sessionDirectory);
        if (!YQDeveloperTestSession.Begin(out string message)) throw new InvalidOperationException(message);
        protectedSession = true;
        profileId = YQProfileSaveSystem.Instance.ActiveProfileId;
        artifactHash = plan.spatialPlanV2.contentHash;
        originalPosition = motor.transform.position;
        originalRotation = motor.transform.rotation;
        var player = PlayerStateManager.Instance.state;
        Vector3 lastPosition = player.lastPosition, logicalPosition = player.logicalPosition, origin = player.renderOrigin;
        string originalScene = player.currentScene, originalRegion = player.currentRegionId, originalRegionName = player.currentRegionName;
        // note: The existing rollback buffer retains the live position by design; register this itinerary's explicit pose restoration separately.
        var poseFields = new Dictionary<FieldInfo, object>();
        foreach (string fieldName in new[] { "_yaw", "_pitch", "_verticalVelocity", "_planarVelocity" })
        {
            FieldInfo field = typeof(YQInvestorPlayerMotor).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) poseFields[field] = field.GetValue(motor);
        }
        restorePose = () =>
        {
            if (motor != null)
            {
                bool enabled = controller != null && controller.enabled;
                if (controller != null) controller.enabled = false;
                motor.transform.SetPositionAndRotation(originalPosition, originalRotation);
                foreach (var entry in poseFields) entry.Key.SetValue(motor, entry.Value);
                if (controller != null) controller.enabled = enabled;
            }
            player.lastPosition = lastPosition; player.logicalPosition = logicalPosition; player.renderOrigin = origin;
            player.currentScene = originalScene; player.currentRegionId = originalRegion; player.currentRegionName = originalRegionName;
        };
        YQDeveloperTestSession.OnRestore(restorePose);
        persistedHashes = PersistedHashes();
        File.WriteAllText(Path.Combine(sessionDirectory, "PersistedBefore.json"), JsonConvert.SerializeObject(persistedHashes, JsonSettings));
        probe = new YQRouteTraversalProbe(motor.gameObject.scene.GetPhysicsScene(), shape, motor.transform);
        EditorApplication.playModeStateChanged += OnPlayState;
        AssemblyReloadEvents.beforeAssemblyReload += Restore;
        EditorApplication.quitting += Restore;
        File.WriteAllText(Path.Combine(sessionDirectory, "Protected.txt"), "utc=" + DateTime.UtcNow.ToString("O") +
            "\nprofile=" + profileId + "\nartifact=" + artifactHash + "\n" + message);
        Debug.Log("[G08-R3] Protected physical itinerary ready: " + sessionDirectory);
    }

    private static void StartLeg(Request next)
    {
        if (!protectedSession || running || restoring || !YQDeveloperTestSession.CheckOwners(out _))
            throw new InvalidOperationException("Begin a protected R3 session before requesting one leg.");
        bool observation = next.action == "observe";
        if (next.profileId != profileId || next.artifactHash != artifactHash ||
            string.IsNullOrWhiteSpace(next.rowId) || (!observation && (next.waypoints == null || next.waypoints.Length == 0 ||
            next.waypoints.Length > 256)) || next.timeoutSeconds < 1f || next.timeoutSeconds > 300f ||
            next.tolerance < .15f || next.tolerance > 2f)
            throw new InvalidOperationException("Invalid identity, waypoint count, deadline or endpoint tolerance.");
        var plan = WorldStateManager.Instance.State.generatedWorldPlan;
        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string failure) ||
            plan.spatialPlanV2.contentHash != artifactHash)
            throw new InvalidOperationException("Accepted artifact changed: " + failure);
        // note: Named verification coordinates must stay inside the accepted site or route corridor; home observations use the measured starting capsule.
        if (observation)
        {
            // note: Stationary views use ordinary mouse input; they never substitute for a movement leg or move the camera independently of the player.
            if (!Finite(next.viewYaw) || !Finite(next.viewPitch) || next.viewYaw < -360f || next.viewYaw > 360f ||
                next.viewPitch < motor.pitchMin || next.viewPitch > motor.pitchMax || !Finite(next.viewSeconds) ||
                next.viewSeconds < 1f || next.viewSeconds > 10f || !BelongsToFeature(prepared, next.featureId, motor.transform.position))
                throw new InvalidOperationException("Invalid stationary view or accepted feature ownership.");
        }
        foreach (Vector3 point in next.waypoints ?? Array.Empty<Vector3>())
        {
            if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z) ||
                !BelongsToFeature(prepared, next.featureId, point))
                throw new InvalidOperationException("Waypoint lacks accepted feature/route ownership: " + next.featureId);
        }
        request = next;
        legDirectory = Path.Combine(sessionDirectory, Stamp() + "_" + SafeName(next.rowId));
        Directory.CreateDirectory(legDirectory);
        File.WriteAllText(Path.Combine(legDirectory, "Request.json"), JsonConvert.SerializeObject(next, JsonSettings));
        trace = new StreamWriter(Path.Combine(legDirectory, "Frames.tsv"));
        trace.WriteLine("frame\tutc\trealSeconds\tx\ty\tz\tinputX\tinputY\tvx\tvy\tvz\tgrounded\ttraversable\tprobeIssues\tsupport\tcontact\twaypoint\tfocused");
        // note: Extra warning rays are diagnostic work, never a performance witness or an alternative clearance verdict.
        supportTrace = new StreamWriter(Path.Combine(legDirectory, "SupportWarnings.jsonl"));
        AcquireInput();
        waypointIndex = observedFrames = inputFrames = unsafeFrames = 0;
        probeWarningFrames = groundedProbeWarningFrames = 0;
        lastFrame = -1; travelled = maximumFrame = 0f;
        started = lastObservation = lastProgress = Time.realtimeSinceStartupAsDouble;
        deadline = started + next.timeoutSeconds;
        previousPosition = progressPosition = motor.transform.position;
        observationPosition = previousPosition; viewCount = 0; nextView = started + 1d;
        running = true;
        InputSystem.onBeforeUpdate += QueueInput;
        InputSystem.onAfterUpdate += KeepDevicesCurrent;
        EditorApplication.update += Observe;
        CapturePlayerView(Path.Combine(legDirectory, "Start.png"));
        Debug.Log("[G08-R3] Physical leg started: " + next.rowId);
    }

    private static bool BelongsToFeature(YQPreparedSpatialMaterializationV2 prepared, string featureId, Vector3 point)
    {
        if (featureId == "observed-home") return Planar(point, originalPosition) <= 80f;
        for (int i = 0; i < prepared.SiteCount; i++)
        {
            var site = prepared.GetSite(i);
            if (site.siteId != featureId && site.sourceSemanticId != featureId) continue;
            if (Vector2.Distance(new Vector2(point.x, point.z), new Vector2(site.x, site.z)) <= site.reservedRadius + 16f) return true;
            foreach (var member in site.MemberFootprint)
                if (Vector2.Distance(new Vector2(point.x, point.z), new Vector2(member.x, member.z)) <= member.reservedRadius + 16f) return true;
        }
        for (int i = 0; i < prepared.RouteCount; i++)
        {
            var route = prepared.GetRoute(i);
            if (route.routeId != featureId && route.sourceSemanticRouteId != featureId) continue;
            for (int p = 0; p < prepared.GetRoutePointCount(i); p++)
            {
                var vertex = prepared.GetRoutePoint(i, p);
                if (Vector2.Distance(new Vector2(point.x, point.z), new Vector2(vertex.x, vertex.z)) <= route.width + route.shoulderWidth + 16f) return true;
                if (p > 0)
                {
                    // note: Accepted polylines can span kilometres between vertices; intermediate legs still belong to that same immutable corridor.
                    var previous = prepared.GetRoutePoint(i, p - 1);
                    Vector2 a = new Vector2(previous.x, previous.z), b = new Vector2(vertex.x, vertex.z);
                    Vector2 offset = new Vector2(point.x, point.z) - a, segment = b - a;
                    float t = segment.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(offset, segment) / segment.sqrMagnitude) : 0f;
                    if ((offset - segment * t).magnitude <= route.width + route.shoulderWidth + 16f) return true;
                }
            }
        }
        for (int i = 0; i < prepared.CrossingCount; i++)
        {
            var crossing = prepared.GetCrossing(i);
            if (crossing.crossingId == featureId && Vector2.Distance(new Vector2(point.x, point.z),
                new Vector2(crossing.x, crossing.z)) <= crossing.requiredSpan + 16f) return true;
        }
        return false;
    }

    private static void AcquireInput()
    {
        // note: Background routing is a private, reversible settings clone; physical devices and project assets are retained.
        originalSettings = InputSystem.settings;
        testSettings = UnityEngine.Object.Instantiate(originalSettings);
        testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = testSettings;
        originalBackground = Application.runInBackground; Application.runInBackground = true;
        originalKeyboard = Keyboard.current; originalMouse = Mouse.current;
        foreach (InputDevice device in InputSystem.devices)
        {
            if (device is Keyboard key && device.name == "YQG08R3Keyboard") keyboard = key;
            if (device is Mouse pointer && device.name == "YQG08R3Mouse") mouse = pointer;
        }
        if (keyboard == null) keyboard = InputSystem.AddDevice<Keyboard>("YQG08R3Keyboard");
        if (mouse == null) mouse = InputSystem.AddDevice<Mouse>("YQG08R3Mouse");
        InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse);
    }

    private static void QueueInput()
    {
        if (!running || motor == null || (request.action != "observe" && waypointIndex >= request.waypoints.Length) ||
            InputState.currentUpdateType != InputUpdateType.Dynamic) return;
        if (request.action == "observe")
        {
            // note: Bound relative look events while leaving locomotion keys released throughout the stationary motion sequence.
            float pitch = motor.cameraPivot != null ? Mathf.DeltaAngle(0f, motor.cameraPivot.localEulerAngles.x) : 0f;
            float yawDelta = Mathf.Clamp(Mathf.DeltaAngle(motor.transform.eulerAngles.y, request.viewYaw), -12f, 12f);
            float pitchDelta = Mathf.Clamp(request.viewPitch - pitch, -8f, 8f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(yawDelta / Mathf.Max(.001f, motor.sensitivityX),
                -pitchDelta / Mathf.Max(.001f, motor.sensitivityY)) });
            KeepDevicesCurrent(); return;
        }
        Vector3 delta = request.waypoints[waypointIndex] - motor.transform.position; delta.y = 0f;
        float angle = delta.sqrMagnitude > .001f ? Vector3.SignedAngle(motor.transform.forward, delta, Vector3.up) : 0f;
        // note: Aim with ordinary relative mouse events and walk with ordinary keys; no transform, speed or elapsed-time manipulation occurs in a leg.
        float look = Mathf.Clamp(angle, -12f, 12f) / Mathf.Max(.001f, motor.sensitivityX);
        bool advance = delta.magnitude > request.tolerance && Mathf.Abs(angle) < 35f;
        KeyboardState keys = advance ? (request.sprint ? new KeyboardState(Key.W, Key.LeftShift) : new KeyboardState(Key.W)) : new KeyboardState();
        InputSystem.QueueStateEvent(keyboard, keys);
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(look, 0f) });
        KeepDevicesCurrent();
    }

    private static void KeepDevicesCurrent()
    {
        if (keyboard != null && keyboard.added) keyboard.MakeCurrent();
        if (mouse != null && mouse.added) mouse.MakeCurrent();
    }

    private static void Observe()
    {
        if (!running || Time.frameCount == lastFrame) return;
        try
        {
            lastFrame = Time.frameCount;
            if (motor == null || !YQDeveloperTestSession.CheckOwners(out string ownerFailure))
                throw new InvalidOperationException("R3 owner changed.");
            double now = Time.realtimeSinceStartupAsDouble;
            maximumFrame = Mathf.Max(maximumFrame, (float)(now - lastObservation)); lastObservation = now;
            Vector3 position = motor.transform.position;
            travelled += Planar(previousPosition, position); previousPosition = position;
            if (Planar(progressPosition, position) > .15f) { progressPosition = position; lastProgress = now; }
            var streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
            bool traversable = streamer != null && streamer.TryValidateCurrentTraversability(position, controller.radius, controller.skinWidth, out _);
            var measurement = probe.Measure(position + shape.rootToFeet, false, default);
            observedFrames++; if (motor.MoveInput.sqrMagnitude > .1f) inputFrames++;
            if (!traversable) unsafeFrames++;
            // note: Terrain traversability and capsule support warnings are separate evidence; a zero legacy counter cannot clear probe warnings.
            if (measurement.issues != YQRouteTraversalProbe.Issue.None)
            {
                probeWarningFrames++;
                if (controller.isGrounded) groundedProbeWarningFrames++;
                CaptureSupportWarning(position, measurement);
            }
            Collider contact = BlockingContact?.GetValue(motor) as Collider;
            Vector3 velocity = controller.velocity;
            trace.WriteLine(string.Join("\t", lastFrame, DateTime.UtcNow.ToString("O"), F(now - started), F(position.x), F(position.y), F(position.z),
                F(motor.MoveInput.x), F(motor.MoveInput.y), F(velocity.x), F(velocity.y), F(velocity.z), controller.isGrounded,
                traversable, measurement.issues, PathOf(measurement.supportCollider), PathOf(contact), waypointIndex, Application.isFocused));
            if (observedFrames % 60 == 0) trace.Flush();
            if (!traversable) { FinishLeg("FAIL", "Current capsule traversability failed."); return; }
            if (request.action == "observe")
            {
                // note: Timestamp actual camera poses beside spaced captures so moving water can be distinguished from viewpoint motion.
                if (Planar(position, observationPosition) > .1f) { FinishLeg("FAIL", "Player translated during stationary observation."); return; }
                if (now >= nextView)
                {
                    Camera camera = motor.playerCamera;
                    string name = "View_" + viewCount.ToString("D2", CultureInfo.InvariantCulture) + ".png";
                    CapturePlayerView(Path.Combine(legDirectory, name));
                    File.AppendAllText(Path.Combine(legDirectory, "Views.tsv"), string.Join("\t", name, lastFrame, DateTime.UtcNow.ToString("O"),
                        F(now - started), F(camera.transform.position.x), F(camera.transform.position.y), F(camera.transform.position.z),
                        F(camera.transform.eulerAngles.x), F(camera.transform.eulerAngles.y), F(camera.transform.eulerAngles.z)) + "\n");
                    viewCount++; nextView = now + .5d;
                }
                if (now - started >= request.viewSeconds) FinishLeg("OBSERVED_VIEWS", "Stationary player-camera sequence captured; visual acceptance requires review.");
                return;
            }
            if (Planar(position, request.waypoints[waypointIndex]) <= request.tolerance)
            {
                CapturePlayerView(Path.Combine(legDirectory, "Waypoint_" + waypointIndex + ".png"));
                waypointIndex++; lastProgress = now; progressPosition = position;
                if (waypointIndex >= request.waypoints.Length) { FinishLeg(inputFrames > 0 ? "TRAVERSED_ENDPOINTS" : "NOT_VERIFIED", "Physical endpoints reached; row acceptance still requires evidence review."); return; }
            }
            if (now >= deadline) FinishLeg("FAIL", "Real-time leg deadline exceeded.");
            else if (now - lastProgress >= 8d) FinishLeg("FAIL", "No physical progress for eight seconds; contact recorded.");
        }
        catch (Exception exception) { FinishLeg("FAIL", exception.ToString()); }
    }

    private static void FinishLeg(string result, string reason)
    {
        if (!running) return;
        // note: Persist a terminal leg immediately, release every synthetic device event, and keep the snapshot protected for review or the next continuous leg.
        running = false;
        InputSystem.onBeforeUpdate -= QueueInput; InputSystem.onAfterUpdate -= KeepDevicesCurrent;
        EditorApplication.update -= Observe;
        trace?.Dispose(); trace = null;
        supportTrace?.Dispose(); supportTrace = null;
        ReleaseInput();
        var end = new { result, reason, finishedUtc = DateTime.UtcNow.ToString("O"), observedFrames, inputFrames, unsafeFrames,
            streamerTraversabilityFailures = unsafeFrames, probeWarningFrames, groundedProbeWarningFrames,
            capsuleClearanceReviewRequired = probeWarningFrames > 0,
            unsafeFramesDefinition = "Legacy counter: failed streamer traversability only; capsule support warnings are counted separately.",
            waypointsReached = waypointIndex, travelled, maximumObservedFrameSeconds = maximumFrame,
            stationaryViews = viewCount,
            position = motor != null ? motor.transform.position : Vector3.zero, profileId, artifactHash,
            evidence = "R3 physical diagnostic, not an R1/R2 performance certification", stateStillProtected = YQDeveloperTestSession.Active };
        File.WriteAllText(Path.Combine(legDirectory, "Result.json"), JsonConvert.SerializeObject(end, JsonSettings));
        Debug.Log("[G08-R3] " + request.rowId + " " + result + ": " + reason);
    }

    private static void ReleaseInput()
    {
        if (keyboard != null && keyboard.added) { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.DisableDevice(keyboard); }
        if (mouse != null && mouse.added) { InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.DisableDevice(mouse); }
        originalKeyboard?.MakeCurrent(); originalMouse?.MakeCurrent();
        if (testSettings != null && InputSystem.settings == testSettings) InputSystem.settings = originalSettings;
        if (testSettings != null) UnityEngine.Object.DestroyImmediate(testSettings);
        testSettings = null; Application.runInBackground = originalBackground;
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) Restore();
    }

    private static void Restore()
    {
        if (!protectedSession) return;
        FinishLeg("INTERRUPTED", "Session ended before this leg completed.");
        // note: Pose restoration is outside the measured traversal; it cannot contribute to distance, access, or endpoint evidence.
        restorePose?.Invoke();
        bool restored = YQDeveloperTestSession.RestoreForPlayModeExit(out string message);
        File.WriteAllText(Path.Combine(sessionDirectory, "Restored.txt"), "utc=" + DateTime.UtcNow.ToString("O") +
            "\nrestored=" + restored + "\npersistenceBarrierRetained=" + YQDeveloperConsoleGate.BlocksPersistence + "\n" + message);
        if (!restored)
        {
            // note: Generation must settle before the existing owner can restore its document. Retain the barrier and retry only this same live session for a bounded interval.
            if (!restoring && EditorApplication.isPlaying)
            {
                restoring = true; restoreDeadline = EditorApplication.timeSinceStartup + 60d;
                EditorApplication.update += RetryRestore;
            }
            Debug.LogWarning("[G08-R3] Restore deferred; original pose restored and save barrier retained: " + message);
            return;
        }
        restoring = false; EditorApplication.update -= RetryRestore;
        var after = PersistedHashes();
        bool unchanged = persistedHashes != null && after.Count == persistedHashes.Count;
        if (unchanged) foreach (var item in persistedHashes) unchanged &= after.TryGetValue(item.Key, out string hash) && item.Value == hash;
        File.WriteAllText(Path.Combine(sessionDirectory, "PersistedAfter.json"), JsonConvert.SerializeObject(new { unchanged, root = Application.persistentDataPath, hashes = after }, JsonSettings));
        protectedSession = false;
        EditorApplication.playModeStateChanged -= OnPlayState;
        AssemblyReloadEvents.beforeAssemblyReload -= Restore;
        EditorApplication.quitting -= Restore;
        motor = null; controller = null; probe = null; restorePose = null;
        Debug.Log("[G08-R3] Player snapshot restored; no save written by the witness.");
    }

    private static void RetryRestore()
    {
        if (!restoring) return;
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup >= restoreDeadline)
        {
            restoring = false; EditorApplication.update -= RetryRestore;
            Debug.LogWarning("[G08-R3] Bounded restoration wait ended; inspect the retained save barrier before exiting or starting another leg.");
            return;
        }
        if (LLMClient.Instance == null || !LLMClient.Instance.IsBusy) Restore();
    }

    private static Dictionary<string, string> PersistedHashes()
    {
        // note: Read only the active owner's files and pointer. Fingerprints prove preservation without exposing document contents or introducing a save store.
        var hashes = new Dictionary<string, string>();
        string root = Application.persistentDataPath;
        foreach (string relative in new[] { "player_state.json", "world_state.json", "Profiles/profiles_manifest.json" })
        {
            string path = Path.Combine(root, relative);
            hashes[relative] = File.Exists(path) ? Hash(path) : "<absent>";
        }
        var owner = YQProfileSaveSystem.Instance;
        if (owner != null && Guid.TryParseExact(owner.ActiveProfileId, "N", out _) &&
            Guid.TryParseExact(owner.ActiveCommitId, "N", out _))
        {
            // note: Fingerprint the existing owner's paired committed documents and backups as well as the working projections; no file is written here.
            string profile = Path.Combine("Profiles", owner.ActiveProfileId);
            string revision = Path.Combine(profile, "revisions", "r" + owner.ActiveRevision.ToString("D8", CultureInfo.InvariantCulture) + "-" + owner.ActiveCommitId);
            foreach (string folder in new[] { profile, revision })
                foreach (string file in new[] { "player_state.json", "world_state.json", "player_state.json.bak", "world_state.json.bak" })
                {
                    string relative = Path.Combine(folder, file), path = Path.Combine(root, relative);
                    hashes[relative] = File.Exists(path) ? Hash(path) : "<absent>";
                }
            string auxiliary = Path.Combine(root, revision, "auxiliary");
            if (Directory.Exists(auxiliary)) foreach (string path in Directory.GetFiles(auxiliary, "*.json", SearchOption.TopDirectoryOnly))
                hashes[Path.Combine(revision, "auxiliary", Path.GetFileName(path))] = Hash(path);
        }
        return hashes;
    }

    private static void Snapshot(string directory)
    {
        var world = WorldStateManager.Instance?.State;
        var plan = world?.generatedWorldPlan;
        var active = YQInvestorPlayerMotor.ActiveMotor;
        if (active == null || !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string failure))
            throw new InvalidOperationException("Accepted world/player projection unavailable.");
        var sites = new List<object>(); var routes = new List<object>(); var waters = new List<object>(); var crossings = new List<object>();
        for (int i = 0; i < prepared.SiteCount; i++)
        {
            var site = prepared.GetSite(i);
            sites.Add(new { site.siteId, site.sourceSemanticId, kind = site.kind.ToString(), site.x, site.z, site.reservedRadius,
                site.headingDegrees, site.frontageRouteId, site.frontageX, site.frontageZ, site.MemberFootprint, site.Tags, site.RequiredFunctions });
        }
        for (int i = 0; i < prepared.RouteCount; i++)
        {
            var route = prepared.GetRoute(i); var points = new List<YQSpatialMaterializationRoutePointV2>();
            for (int p = 0; p < prepared.GetRoutePointCount(i); p++) points.Add(prepared.GetRoutePoint(i, p));
            routes.Add(new { route.routeId, route.sourceSemanticRouteId, route.fromSiteId, route.toSiteId, route.width, route.shoulderWidth, points });
        }
        for (int i = 0; i < prepared.WaterCount; i++)
        {
            var water = prepared.GetWater(i); var points = new List<YQSpatialMaterializationWaterPointV2>();
            for (int p = 0; p < prepared.GetWaterPointCount(i); p++) points.Add(prepared.GetWaterPoint(i, p));
            waters.Add(new { water.hydrologyId, kind = water.kind.ToString(), water.nominalWidth, water.nominalDepth, points });
        }
        for (int i = 0; i < prepared.CrossingCount; i++) crossings.Add(prepared.GetCrossing(i));
        var owners = new List<object>();
        // note: Read the live accepted binding metadata so a normal Continue can prove that the new selector preserves old stable kit/version pairs.
        var runtimeSiteBindings = new List<object>();
        foreach (var settlement in plan.settlements)
            if (settlement != null) runtimeSiteBindings.Add(new { semanticId = settlement.settlementId, kind = "settlement",
                settlement.runtimeSiteKitId, settlement.runtimeSiteSemanticStyle, settlement.runtimeSiteBindingVersion });
        foreach (var encampment in plan.encampments)
            if (encampment != null) runtimeSiteBindings.Add(new { semanticId = encampment.encampmentId, kind = "encampment",
                encampment.runtimeSiteKitId, encampment.runtimeSiteSemanticStyle, encampment.runtimeSiteBindingVersion });
        foreach (var owner in UnityEngine.Object.FindObjectsByType<YQCompiledWorldSiteInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var entrances = new List<object>(); var sectors = new List<object>();
            // note: Discover names from the actual loaded approved assembly; names guide inspection and never substitute for physical access evidence.
            foreach (Transform child in owner.GetComponentsInChildren<Transform>(true))
            {
                string name = child.name.ToLowerInvariant();
                if (entrances.Count < 128 && (name.Contains("door") || name.Contains("entrance") || name.Contains("anchor")))
                    entrances.Add(new { path = PathOf(child), position = child.position, rotation = child.rotation, active = child.gameObject.activeInHierarchy });
                if (child.parent != null && child.parent.parent == owner.transform)
                    sectors.Add(new { path = PathOf(child), position = child.position, active = child.gameObject.activeInHierarchy });
            }
            // note: Separate real geometry, population and loader state so a missing actor never gets mistaken for a successful complete site.
            const BindingFlags ownerFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            owners.Add(new { path = PathOf(owner), position = owner.transform.position, loaded = owner.IsLoaded,
                rootActive = owner.isActiveAndEnabled,
                geometryLoaded = typeof(YQCompiledWorldSiteInstance).GetProperty("IsContinuationGeometryLoaded", ownerFlags)?.GetValue(owner),
                loading = typeof(YQCompiledWorldSiteInstance).GetField("loading", ownerFlags)?.GetValue(owner),
                unloading = typeof(YQCompiledWorldSiteInstance).GetField("unloading", ownerFlags)?.GetValue(owner),
                rejected = typeof(YQCompiledWorldSiteInstance).GetField("loadRejected", ownerFlags)?.GetValue(owner),
                failure = typeof(YQCompiledWorldSiteInstance).GetField("loadFailure", ownerFlags)?.GetValue(owner),
                pendingContent = typeof(YQCompiledWorldSiteInstance).GetField("pendingSiteContent", ownerFlags)?.GetValue(owner) is GameObject,
                populationReady = typeof(YQCompiledWorldSiteInstance).GetField("continuationPopulationReady", ownerFlags)?.GetValue(owner),
                populationInFlight = typeof(YQCompiledWorldSiteInstance).GetField("continuationPopulationInFlight", ownerFlags)?.GetValue(owner),
                semanticId = typeof(YQCompiledWorldSiteInstance).GetField("settlementId", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner),
                activeCellIds = typeof(YQCompiledWorldSiteInstance).GetField("activeCellIds", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner),
                renderers = owner.GetComponentsInChildren<Renderer>(false).Length, colliders = owner.GetComponentsInChildren<Collider>(false).Length,
                entrances, sectors });
        }
        var sources = new Dictionary<string, string>();
        foreach (string path in new[] {
            "Assets/Assets/Scripts/Generated/Editor/YQG08R3PhysicalItineraryVerification.cs",
            "Assets/Assets/Scripts/Generated/Editor/YQEditorAutoRefreshBootstrap.cs",
            "Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs",
            "Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs",
            "Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs",
            "Assets/Assets/Scripts/Generated/YQContinuousWorldCellAuthority.cs",
            "Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs",
            "Assets/Assets/Scripts/Generated/YQRuntimeWorldSiteCatalog.cs",
            "Assets/Assets/Scripts/Generated/YQGeneratedRiverBridge.cs",
            "Assets/Assets/Scripts/Generated/YQGeneratedWorldIntegrityValidator.cs",
            "Assets/Assets/Scripts/Generated/YQRouteTraversalProbe.cs",
            "Assets/Assets/Scripts/Tutorial/YQInvestorPlayerMotor.cs",
            "Assets/Assets/Scripts/Tutorial/YQDeveloperConsoleGate.cs",
            "Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs",
            "Assets/Assets/Scripts/Tutorial/YQDeveloperTestSession.cs" }) sources[path] = Hash(path);
        // note: Record the actual capsule and ordinary camera contracts alongside each diagnostic rather than inferring geometry from endpoints.
        CharacterController activeController = active.GetComponent<CharacterController>();
        bool capsuleMeasured = YQRouteTraversalProbe.Shape.TryCapture(activeController, out var actualShape);
        // note: Read-only collision helpers need the actual observer too; discovery must not depend on acquiring a mutating test session first.
        if (!capsuleMeasured || !active.IsAuthoritative) throw new InvalidOperationException("Authoritative observation capsule unavailable.");
        motor = active; controller = activeController; shape = actualShape;
        object capsule = capsuleMeasured ? new { actualShape.radius, actualShape.height, actualShape.skin, actualShape.step,
            actualShape.slope, actualShape.rootToFeet, center = activeController.center, scale = activeController.transform.lossyScale } : null;
        Camera activeCamera = active.playerCamera;
        object cameraEvidence = activeCamera != null ? new { activeCamera.name, activeCamera.enabled, activeCamera.fieldOfView,
            activeCamera.nearClipPlane, activeCamera.farClipPlane, activeCamera.cullingMask, activeCamera.pixelWidth, activeCamera.pixelHeight,
            position = activeCamera.transform.position, rotation = activeCamera.transform.rotation } : null;
        string activeProfileName = null;
        if (YQProfileSaveSystem.Instance != null)
            foreach (var profile in YQProfileSaveSystem.Instance.Profiles)
                if (profile != null && profile.profileId == YQProfileSaveSystem.Instance.ActiveProfileId)
                { activeProfileName = profile.displayName; break; }
        var snapshot = new { utc = DateTime.UtcNow.ToString("O"), scene = SceneManager.GetActiveScene().path,
            unityVersion = Application.unityVersion,
            hardware = new { SystemInfo.operatingSystem, SystemInfo.processorType, SystemInfo.systemMemorySize,
                SystemInfo.graphicsDeviceName, SystemInfo.graphicsDeviceVersion, SystemInfo.graphicsMemorySize },
            qualityLevel = QualitySettings.GetQualityLevel(), qualityNames = QualitySettings.names,
            renderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ?
                AssetDatabase.GetAssetPath(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline) : "Built-in",
            profileId = YQProfileSaveSystem.Instance?.ActiveProfileId, activeProfileName, commitId = YQProfileSaveSystem.Instance?.ActiveCommitId,
            profileRevision = YQProfileSaveSystem.Instance?.ActiveRevision, persistedHashes = PersistedHashes(),
            persistenceBlocked = YQDeveloperConsoleGate.BlocksPersistence,
            worldId = world.worldIdentity.worldId, seed = plan.worldSeed,
            artifactHash = plan.spatialPlanV2.contentHash, runtimeMvid = typeof(YQInvestorPlayerMotor).Assembly.ManifestModule.ModuleVersionId,
            runtimeAssemblyHash = Hash(typeof(YQInvestorPlayerMotor).Assembly.Location), editorMvid = typeof(YQG08R3PhysicalItineraryVerification).Assembly.ManifestModule.ModuleVersionId,
            editorAssemblyHash = Hash(typeof(YQG08R3PhysicalItineraryVerification).Assembly.Location), sources,
            playerPosition = active.transform.position, playerRotation = active.transform.rotation, active.walkSpeed, active.sprintSpeed,
            capsuleMeasured, capsule, cameraEvidence,
            sites, routes, waters, crossings, owners, runtimeSiteBindings, semanticReservations = plan.semanticAuthority?.siteReservations,
            semanticSchema = plan.semanticAuthority?.schemaVersion,
            evidence = "Runtime discovery; physical itinerary rows are not certified by this snapshot" };
        File.WriteAllText(Path.Combine(directory, "Discovery.json"), JsonConvert.SerializeObject(snapshot, JsonSettings));
        CaptureBridgeColliders(directory);
        CapturePlayerView(Path.Combine(directory, "PlayerView.png"));
        Debug.Log("[G08-R3] Runtime discovery: " + directory);
    }

    private static void CaptureHydrology(string directory)
    {
        // note: Names below are emitted by the existing water materializers; this diagnostic never discovers or approves new water topology.
        var observedPlayer = YQInvestorPlayerMotor.ActiveMotor;
        Camera camera = observedPlayer != null ? observedPlayer.playerCamera : null;
        if (camera == null) throw new InvalidOperationException("Ordinary player camera is unavailable.");
        Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(camera);
        YQPlayerFollowingSemanticChunkStreamer streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        List<object> surfaces = new List<object>();
        List<MeshFilter> waterFilters = new List<MeshFilter>();
        foreach (MeshFilter filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (filter.name.StartsWith("ContinuousRiver_", StringComparison.Ordinal) ||
                filter.name.StartsWith("ContinuousAreaWater_", StringComparison.Ordinal) || filter.name == "CompiledHydrologySurface" ||
                filter.name == "CuratedEllipticalWaterSurface") waterFilters.Add(filter);
        }
        waterFilters.Sort((left, right) => string.Compare(PathOf(left), PathOf(right), StringComparison.Ordinal));
        foreach (MeshFilter filter in waterFilters)
        {
            bool ribbon = filter.name.StartsWith("ContinuousRiver_", StringComparison.Ordinal);
            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer == null) continue;
            Mesh mesh = filter.sharedMesh;
            var materials = new List<object>();
            foreach (Material material in renderer.sharedMaterials)
                materials.Add(material == null ? new { missing = true, name = "", shader = "", supported = false, queue = 0 } :
                    new { missing = false, name = material.name, shader = material.shader != null ? material.shader.name : "",
                        supported = material.shader != null && material.shader.isSupported, queue = material.renderQueue });
            var samples = new List<object>();
            int wet = 0, dry = 0, unavailable = 0;
            float minimumDepth = float.PositiveInfinity, maximumDepth = float.NegativeInfinity;
            if (filter.gameObject.activeInHierarchy && mesh != null && mesh.isReadable)
            {
                // note: Measure actual ribbon center pairs or a bounded set of mesh triangle centroids for lakes/area water. Neither infers a hidden basin surface.
                Vector3[] vertices = mesh.vertices;
                var measuredPoints = new List<Vector3>();
                if (ribbon)
                    for (int index = 0; index + 1 < vertices.Length; index += 2)
                        measuredPoints.Add((vertices[index] + vertices[index + 1]) * .5f);
                else
                {
                    int[] triangles = mesh.triangles;
                    int stride = Mathf.Max(1, Mathf.CeilToInt(triangles.Length / (3f * 128f)));
                    for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3 * stride)
                        measuredPoints.Add((vertices[triangles[triangle]] + vertices[triangles[triangle + 1]] +
                            vertices[triangles[triangle + 2]]) / 3f);
                }
                foreach (Vector3 localPoint in measuredPoints)
                {
                    Vector3 point = filter.transform.TransformPoint(localPoint);
                    Terrain terrain = null;
                    bool ownerFound = streamer != null && streamer.TryGetGeneratedTerrainAt(point, out terrain) && terrain != null;
                    float? ground = ownerFound ? YQGeneratedWorldTerrain.SampleWorldHeight(terrain, point) : (float?)null;
                    float? depth = ground.HasValue ? point.y - ground.Value : (float?)null;
                    bool finite = depth.HasValue && !float.IsNaN(depth.Value) && !float.IsInfinity(depth.Value);
                    if (finite)
                    {
                        if (depth.Value > .1f) wet++; else dry++;
                        minimumDepth = Mathf.Min(minimumDepth, depth.Value);
                        maximumDepth = Mathf.Max(maximumDepth, depth.Value);
                    }
                    else unavailable++;
                    Vector3 viewport = camera.WorldToViewportPoint(point);
                    bool viewportCandidate = viewport.z >= camera.nearClipPlane && viewport.z <= camera.farClipPlane &&
                        viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
                    samples.Add(new { point, ownerFound, terrainName = terrain != null ? terrain.name : "", ground, depth,
                        depthFinite = finite, viewport, viewportCandidate });
                }
            }
            surfaces.Add(new { path = PathOf(filter), active = filter.gameObject.activeInHierarchy, renderer.enabled,
                renderer.forceRenderingOff, layer = filter.gameObject.layer,
                cameraLayerIncluded = (camera.cullingMask & (1 << filter.gameObject.layer)) != 0,
                boundsIntersectFrustum = GeometryUtility.TestPlanesAABB(frustum, renderer.bounds),
                boundsCenter = renderer.bounds.center, boundsSize = renderer.bounds.size,
                meshName = mesh != null ? mesh.name : "", vertexCount = mesh != null ? mesh.vertexCount : 0,
                readable = mesh != null && mesh.isReadable, ribbon, materials, wetCenterSamples = wet, dryCenterSamples = dry,
                unavailableCenterSamples = unavailable, minimumDepth = wet + dry > 0 ? minimumDepth : (float?)null,
                maximumDepth = wet + dry > 0 ? maximumDepth : (float?)null, samples });
        }
        var receipt = new { utc = DateTime.UtcNow.ToString("O"), evidence = "READ_ONLY_RUNTIME_WATER_CENSUS",
            verdict = "OBSERVED; not hydrology, appearance, physical traversal or performance acceptance",
            profileId = YQProfileSaveSystem.Instance?.ActiveProfileId,
            artifactHash = WorldStateManager.Instance?.State?.generatedWorldPlan?.spatialPlanV2?.contentHash,
            playerPosition = observedPlayer.transform.position,
            cameraPosition = camera.transform.position, cameraRotation = camera.transform.rotation,
            camera.fieldOfView, camera.nearClipPlane, camera.farClipPlane, camera.cullingMask,
            surfaceCount = surfaces.Count, surfaces,
            limits = "Only existing materializer names are examined. Frustum/viewport candidates do not prove visibility or occlusion. " +
                "Depth uses only published streamer terrain owners; no fallback terrain or fabricated ground. Area-water samples are bounded actual triangle centroids. " +
                "Complete build/world identity and accepted water projections are in adjacent Discovery.json. This explicit census is not a measured travel frame." };
        File.WriteAllText(Path.Combine(directory, "Hydrology.json"), JsonConvert.SerializeObject(receipt, JsonSettings));
        Debug.Log("[G08-R3] Read-only hydrology census: " + directory);
    }

    private static void CapturePlayerView(string path)
    {
        // note: Copy the real player's camera pose/frustum for evidence and release every native buffer; never reposition the player or alter streaming demand.
        Camera source = YQInvestorPlayerMotor.ActiveMotor?.playerCamera;
        if (source == null) throw new InvalidOperationException("Player camera unavailable.");
        var host = new GameObject("G08R3PlayerViewCapture") { hideFlags = HideFlags.HideAndDontSave };
        Camera camera = host.AddComponent<Camera>(); camera.CopyFrom(source); camera.enabled = false;
        host.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        int width = Mathf.Max(1, source.pixelWidth), height = Mathf.Max(1, source.pixelHeight);
        RenderTexture old = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D pixels = null;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(target);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void CaptureBridgeColliders(string directory)
    {
        // note: This idle census verifies actual enabled collision after production integrity repair, without changing the bridge or counting it as traversal.
        var bridges = new List<object>();
        int enabledRails = 0, enabledRenderDecks = 0, supportBoxes = 0, fittedMeshes = 0, continuousMeshes = 0;
        foreach (YQGeneratedRiverBridge bridge in UnityEngine.Object.FindObjectsByType<YQGeneratedRiverBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var colliders = new List<object>();
            foreach (Collider collider in bridge.GetComponentsInChildren<Collider>(true))
            {
                if (collider.GetComponentInParent<YQGeneratedRiverBridge>() != bridge) continue;
                bool active = collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger;
                MeshCollider mesh = collider as MeshCollider;
                if (active && collider.name == "BridgeRailPost") enabledRails++;
                if (active && (collider.name == "StoneBridgeDeck" || collider.name == "WoodBridgeDeck" ||
                    collider.name == "DisheveledBridgeDeck" || collider.name == "RepairedBridgeDeck")) enabledRenderDecks++;
                if (active && collider.name == "StoneBridgeWalkSurface" && collider is BoxCollider) supportBoxes++;
                if (active && mesh != null && mesh.sharedMesh != null)
                {
                    if (mesh.sharedMesh.name == "YQ_StoneBridgeWalkSurface") fittedMeshes++;
                    if (mesh.sharedMesh.name == "YQ_ContinuousBridgeSupport") continuousMeshes++;
                }
                colliders.Add(new { path = PathOf(collider), instanceId = collider.GetInstanceID(), type = collider.GetType().Name, active,
                    collider.enabled, collider.isTrigger, layer = collider.gameObject.layer,
                    mesh = mesh != null && mesh.sharedMesh != null ? mesh.sharedMesh.name : null,
                    geometry = CaptureOwnedBridgeGeometry(mesh) });
            }
            bridges.Add(new { path = PathOf(bridge), instanceId = bridge.GetInstanceID(), active = bridge.gameObject.activeInHierarchy, colliders });
        }
        File.WriteAllText(Path.Combine(directory, "BridgeColliders.json"), JsonConvert.SerializeObject(new
        {
            utc = DateTime.UtcNow.ToString("O"), diagnosticVersion = "bridge-collision-census-3-buffered-topology", bridges,
            enabledRails, enabledRenderDecks, supportBoxes, fittedMeshes, continuousMeshes,
            limits = "Read-only loaded-scene collision census. Discovery.json contains build/world identity. No visibility, completeness, clearance or performance verdict."
        }, JsonSettings));
    }

    private static object CaptureOwnedBridgeGeometry(MeshCollider collider)
    {
        // note: Read only the small generated collision meshes at an idle snapshot. World vertices retain bank/section geometry without exporting vendor meshes or affecting a measured leg.
        if (collider == null || collider.sharedMesh == null) return null;
        Mesh mesh = collider.sharedMesh;
        bool buffered = mesh.subMeshCount == 3;
        if ((mesh.name != "YQ_StoneBridgeWalkSurface" && mesh.name != "YQ_ContinuousBridgeSupport") ||
            !mesh.isReadable || mesh.vertexCount < 8 || mesh.vertexCount > (buffered ? 1024 : 112) || (!buffered && mesh.vertexCount % 4 != 0))
            return new { name = mesh.name, captured = false, reason = "Not a bounded readable project-generated bridge collision mesh." };
        Vector3[] vertices = mesh.vertices;
        for (int index = 0; index < vertices.Length; index++)
            vertices[index] = collider.transform.TransformPoint(vertices[index]);
        var banks = new List<object>();
        for (int bank = 0; !buffered && bank < 2; bank++)
        {
            int index = bank == 0 ? 0 : vertices.Length - 4;
            Vector3 center = (vertices[index] + vertices[index + 1]) * .5f;
            // note: The deeper bank ray is explanatory geometry evidence, never support admission or an acceptance override.
            banks.Add(new { bank, vertexIndex = index, center,
                centerRay = SupportRay("bank_center_" + bank, center, 2f, 12f) });
        }
        // note: The buffered union has no strip-section vertex order. Retain its explicit face groups and leave bank reconstruction unclaimed rather than interpreting unrelated vertices as terminal banks.
        var submeshes = new List<int[]>();
        for (int index = 0; index < mesh.subMeshCount; index++) submeshes.Add(mesh.GetTriangles(index));
        return new { name = mesh.name, captured = true, topology = buffered ? "buffered_union" : "section_strip", vertices, triangles = mesh.triangles, submeshes, banks,
            bankReconstructionAvailable = !buffered };
    }

    private static void CaptureSupportWarning(Vector3 position, YQRouteTraversalProbe.Result measurement)
    {
        // note: Retain the probe's unchanged result beside raw physics hits at its exact center, rim and footprint coordinates.
        Vector3 expected = position + shape.rootToFeet;
        float reach = Mathf.Max(.1f, shape.step + shape.skin + .05f);
        var rays = new List<object>();
        rays.Add(SupportRay("center", expected, reach, reach * 2f));
        float rimRadius = Mathf.Max(.001f, shape.radius - shape.skin);
        for (int sample = 0; sample < 8; sample++)
        {
            float angle = sample * Mathf.PI * .25f;
            Vector3 rim = expected + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * rimRadius;
            rays.Add(SupportRay("rim_" + sample, rim, reach, reach * 2f));
        }
        if (measurement.hasSupport)
        {
            float footprintRadius = shape.radius * .8f;
            for (int foot = 0; foot < 4; foot++)
            {
                Vector3 offset = foot < 2 ? Vector3.right * (foot == 0 ? footprintRadius : -footprintRadius) :
                    Vector3.forward * (foot == 2 ? footprintRadius : -footprintRadius);
                rays.Add(SupportRay("footprint_" + foot, measurement.feet + offset, reach, reach * 2f));
            }
        }
        // note: A deeper center ray explains a missed short support without allowing its hit to clear the original warning.
        rays.Add(SupportRay("supplemental_center_below", expected, reach, reach + 5f));
        var record = new
        {
            diagnosticVersion = "support-warning-rays-1", frame = Time.frameCount, utc = DateTime.UtcNow.ToString("O"),
            expectedFeet = expected, resolvedFeet = measurement.feet, measurement.hasSupport,
            issues = measurement.issues.ToString(), grounded = controller.isGrounded, velocity = controller.velocity,
            controllerBounds = new { controller.bounds.center, controller.bounds.size, controller.bounds.min },
            shape.radius, shape.height, shape.skin, shape.step, shape.slope, reach,
            support = PathOf(measurement.supportCollider), overlap = PathOf(measurement.overlapCollider), rays,
            evidence = "Supplemental read-only rays; original probe acceptance and motor behavior unchanged; no sweep measured."
        };
        var compact = new JsonSerializerSettings { Formatting = Formatting.None };
        foreach (var converter in JsonSettings.Converters) compact.Converters.Add(converter);
        supportTrace.WriteLine(JsonConvert.SerializeObject(record, compact));
        supportTrace.Flush();
    }

    private static object SupportRay(string name, Vector3 expected, float reach, float distance)
    {
        Vector3 origin = expected + Vector3.up * reach;
        int count = motor.gameObject.scene.GetPhysicsScene().Raycast(origin, Vector3.down, supportHits,
            distance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        var hits = new List<object>();
        for (int index = 0; index < count; index++)
        {
            RaycastHit hit = supportHits[index];
            Collider collider = hit.collider;
            bool player = collider == null || collider == controller || collider.transform.IsChildOf(motor.transform);
            bool layerIgnored = collider != null && Physics.GetIgnoreLayerCollision(controller.gameObject.layer, collider.gameObject.layer);
            bool pairIgnored = collider != null && Physics.GetIgnoreCollision(controller, collider);
            MeshCollider mesh = collider as MeshCollider;
            BoxCollider box = collider as BoxCollider;
            hits.Add(new
            {
                path = PathOf(collider), instanceId = collider != null ? collider.GetInstanceID() : 0,
                type = collider != null ? collider.GetType().Name : "<none>",
                hit.point, hit.normal, hit.distance, hit.triangleIndex,
                deltaY = hit.point.y - expected.y, slope = Vector3.Angle(hit.normal, Vector3.up),
                player, layerIgnored, pairIgnored, aboveReachableTop = hit.point.y > expected.y + shape.step + shape.skin,
                bounds = collider != null ? new { collider.bounds.center, collider.bounds.size } : null,
                transform = collider != null ? new { collider.transform.position, collider.transform.rotation, collider.transform.lossyScale } : null,
                boxGeometry = box != null ? new { box.center, box.size } : null,
                meshGeometry = mesh != null && mesh.sharedMesh != null ? new { mesh.sharedMesh.name, mesh.sharedMesh.vertexCount, mesh.convex } : null
            });
        }
        return new { name, expected, origin, distance, count, saturated = count == supportHits.Length, hits };
    }

    private static string NewDirectory(string name)
    {
        string path = Path.Combine(Root, Stamp() + "_" + name); Directory.CreateDirectory(path); return path;
    }
    private static string Stamp() => DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
    private static float Planar(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string SafeName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_'); return value;
    }
    private static string PathOf(Component component)
    {
        if (component == null) return "<none>";
        string path = component.name;
        for (Transform parent = component.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
        return path.Replace('\t', '_').Replace('\n', '_');
    }
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) using (var file = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", string.Empty).ToLowerInvariant();
    }
}
#endif
