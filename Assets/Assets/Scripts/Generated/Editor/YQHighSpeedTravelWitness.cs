#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// note: Explicit assisted measurement owns only temporary input devices/settings and a runtime motor speed; production movement, readiness and persistence remain active.
internal static class YQHighSpeedTravelWitness
{
    private static YQInvestorPlayerMotor motor;
    private static YQPlayerFollowingSemanticChunkStreamer streamer;
    private static CharacterController capsule;
    private static YQHighSpeedContactWitness contacts;
    private static Keyboard keyboard, originalKeyboard;
    private static Mouse mouse, originalMouse;
    private static InputSettings originalSettings, settings;
    private static bool running, originalBackground;
    private static float originalSpeed;
    private static object world, player;
    private static string profile, directory;
    private static double started, previous, nextSnapshot, lastProgress;
    private static Vector3 previousPosition;
    private static Vector3[] targets;
    private static int phase, frame, ackFrames, blockStart;
    private static float bestDistance, distance, fastDistance;
    private static double fastSeconds;
    private static readonly List<object> Frames = new List<object>();
    private static readonly List<object> Snapshots = new List<object>();
    private static readonly List<ProfilerRecorder> Recorders = new List<ProfilerRecorder>();
    private static readonly string[] Metrics = { "Main Thread", "PlayerLoop", "GC.Collect", "GC.Alloc" };
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly FieldInfo Chunks = typeof(YQPlayerFollowingSemanticChunkStreamer).GetField("_chunks", Fields);
    private static readonly FieldInfo Publications = typeof(YQPlayerFollowingSemanticChunkStreamer).GetField("_frontierPublicationPhaseCalls", Fields);
    private static readonly MethodInfo Ready = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod("IsReadyForPlayerEntry", Fields);
    private static readonly MethodInfo VisualProgress = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod("DescribeLastVisualCoverageOwnerProgress", Fields);
    private static readonly MethodInfo TraversalTerrain = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod("TryGetTraversalTerrainAt", Fields);
    private static object Motion(string name) => typeof(YQInvestorPlayerMotor).GetProperty(name, Fields)?.GetValue(motor);
    private static int[] publicationStart;

    internal static void Start()
    {
        var profiles = YQProfileSaveSystem.Instance;
        motor = YQInvestorPlayerMotor.ActiveMotor;
        streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        string name = profiles?.FindProfile(profiles.ActiveProfileId)?.displayName;
        if (running || !Application.isPlaying || EditorApplication.isPaused ||
            !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ||
            name == null || !name.StartsWith("Terrain Apron Playtest ", StringComparison.Ordinal) ||
            YQDeveloperTestSession.Active || YQDeveloperConsoleGate.BlocksPersistence ||
            motor == null || !motor.IsAuthoritative || !motor.CanProcessMovementInput || motor.IsAutoWalking ||
            motor.IsRagdoll || motor.IsRecovering || motor.PlanarVelocity.sqrMagnitude > .25f ||
            streamer == null || Keyboard.current == null || Mouse.current == null ||
            !Keyboard.current.native || !Mouse.current.native)
            throw new InvalidOperationException("200 m/s witness requires stationary released disposable Terrain Apron Playtest, normal physical devices and no test lease.");
        capsule = motor.GetComponent<CharacterController>();
        if (capsule == null || !capsule.enabled || !capsule.detectCollisions ||
            !streamer.TryValidateCurrentTraversability(motor.transform.position, capsule.radius, capsule.skinWidth, out _))
            throw new InvalidOperationException("Current canonical capsule/ground is not traversable.");
        world = WorldStateManager.Instance.State; player = PlayerStateManager.Instance.state;
        profile = profiles.ActiveProfileId;
        directory = "outputs/WorldAuthoringReview_20261015/sol61/TravelRepair/Speed200_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(directory);
        Vector3 origin = LogicalPosition();
        // note: A six-kilometre rectangle exceeds the existing initial envelope and 600 m lead; all four legs use continuous motor input, including turns and the return visit.
        targets = new[] { origin + Vector3.right * 2000f, origin + Vector3.right * 2000f + Vector3.forward * 1000f,
            origin + Vector3.forward * 1000f, origin };
        phase = ackFrames = 0; distance = fastDistance = 0; fastSeconds = 0;
        Frames.Clear(); Snapshots.Clear(); Recorders.Clear();
        previousPosition = origin; frame = Time.frameCount;
        started = previous = lastProgress = nextSnapshot = EditorApplication.timeSinceStartup;
        bestDistance = Vector3.Distance(origin, targets[0]);
        blockStart = streamer.TraversalConstraintBlockCount;
        publicationStart = ((int[])Publications.GetValue(streamer)).ToArray();
        originalKeyboard = Keyboard.current; originalMouse = Mouse.current;
        originalSettings = InputSystem.settings; originalBackground = Application.runInBackground;
        originalSpeed = motor.walkSpeed;
        try
        {
            settings = UnityEngine.Object.Instantiate(originalSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings; Application.runInBackground = true;
            keyboard = InputSystem.AddDevice<Keyboard>("Sol61Speed200Witness");
            mouse = InputSystem.AddDevice<Mouse>("Sol61Speed200WitnessMouse");
            motor.walkSpeed = 200f;
            contacts = motor.gameObject.AddComponent<YQHighSpeedContactWitness>();
            foreach (string metric in Metrics) Recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Internal, metric, 16384,
                ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.SumAllSamplesInFrame));
            running = true;
            InputSystem.onBeforeUpdate += Input;
            InputSystem.onAfterUpdate += KeepCurrent;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayState;
            AssemblyReloadEvents.beforeAssemblyReload += Abort;
            EditorApplication.quitting += Abort;
        }
        catch { Finish("SETUP_FAILED"); throw; }
    }

    private static Vector3 LogicalPosition() => motor.transform.position + PlayerStateManager.Instance.state.renderOrigin;
    private static bool Current() => Application.isPlaying && !EditorApplication.isPaused && motor != null && streamer != null &&
        ReferenceEquals(world, WorldStateManager.Instance?.State) && ReferenceEquals(player, PlayerStateManager.Instance?.state) &&
        YQProfileSaveSystem.Instance?.ActiveProfileId == profile && InputSystem.settings == settings;

    private static void KeepCurrent()
    {
        if (!running) return;
        keyboard?.MakeCurrent(); mouse?.MakeCurrent();
    }

    private static void Input()
    {
        if (!running || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
        if (!Current() || (originalKeyboard != null && originalKeyboard.escapeKey.isPressed)) { Finish("ABORTED_CONTEXT_OR_ESCAPE"); return; }
        Vector3 delta = targets[phase] - LogicalPosition(); delta.y = 0f;
        float angle = Vector3.SignedAngle(motor.transform.forward, delta, Vector3.up);
        // note: Real W and mouse events feed the existing authoritative motor; no position, velocity, terrain, collision or readiness state is assigned.
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(Mathf.Clamp(angle, -12f, 12f) / Mathf.Max(.001f, motor.sensitivityX), 0f) });
        KeepCurrent();
    }

    private static void Tick()
    {
        if (!running) return;
        // note: An observer failure must release the temporary input just like a timeout or Play exit.
        try { TickCore(); }
        catch (Exception exception) { Debug.LogException(exception); Finish("ABORTED_OBSERVER_EXCEPTION"); }
    }

    private static void TickCore()
    {
        if (!running) return;
        if (!Current()) { Finish("ABORTED_CONTEXT"); return; }
        double now = EditorApplication.timeSinceStartup;
        if (now - started >= 120d || Frames.Count >= 16000) { Finish("BOUNDED_TIMEOUT"); return; }
        if (Time.frameCount == frame) return;
        frame = Time.frameCount;
        Vector3 position = LogicalPosition();
        double dt = now - previous;
        float travelled = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(previousPosition.x, previousPosition.z));
        float actualSpeed = dt > 0d ? travelled / (float)dt : 0f;
        float simulationDelta = (float)Motion("LastMovementDeltaTime");
        Vector3 motorDisplacement = (Vector3)Motion("LastActualMoveDisplacement");
        float motorSpeed = simulationDelta > 0f ? new Vector2(motorDisplacement.x, motorDisplacement.z).magnitude / simulationDelta : 0f;
        var terrainArgs = new object[] { motor.transform.position, null };
        bool terrainPresent = TraversalTerrain != null && (bool)TraversalTerrain.Invoke(streamer, terrainArgs);
        Terrain terrain = terrainPresent ? (Terrain)terrainArgs[1] : null;
        distance += travelled;
        if (motorSpeed >= 185f) { fastDistance += new Vector2(motorDisplacement.x, motorDisplacement.z).magnitude; fastSeconds += simulationDelta; }
        if (motor.MoveInput.y > .1f && Keyboard.current == keyboard) ackFrames++;
        Frames.Add(new { frame, elapsed = now - started, gapMs = dt * 1000d, phase, position, actualSpeed,
            simulationDelta, motorDisplacement, motorSpeed, motor.CanProcessMovementInput, motor.IsGrounded, motor.IsRagdoll, motor.IsRecovering,
            health = motor.vitals != null ? motor.vitals.CurrentHealth : -1f, dead = motor.vitals != null && motor.vitals.IsDead,
            terrain = terrain != null ? terrain.name : null, terrainY = terrain != null ? (float?)(terrain.SampleHeight(motor.transform.position) + terrain.transform.position.y) : null,
            capsuleFeetY = capsule.bounds.min.y, movementGateRejected = Motion("LastMoveRejectedByTraversalGate"),
            requestedPlanar = motor.PlanarVelocity, motorInput = motor.MoveInput, inputAcknowledged = Keyboard.current == keyboard && motor.MoveInput.y > .1f,
            collisionFlags = capsule.collisionFlags.ToString(), lastContact = contacts != null ? contacts.LastContact : null,
            traversalBlocks = streamer.TraversalConstraintBlockCount - blockStart });
        previous = now; previousPosition = position;
        if (motor.vitals != null && motor.vitals.IsDead) { Finish("PLAYER_DIED_CAUSE_REQUIRES_HEIGHT_AND_CONTACT_REVIEW"); return; }
        float remaining = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(targets[phase].x, targets[phase].z));
        if (remaining < bestDistance - 1f) { bestDistance = remaining; lastProgress = now; }
        if (now >= nextSnapshot) { nextSnapshot = now + 1d; Snapshot(now, position); }
        if (remaining < 24f)
        {
            phase++;
            if (phase >= targets.Length) { Finish("RECTANGLE_RETURNED"); return; }
            bestDistance = Vector3.Distance(position, targets[phase]); lastProgress = now;
        }
        if (now - lastProgress >= 20d) Finish("NO_PROGRESS_OBSTACLE_OR_READINESS_BOUNDARY");
    }

    private static object Cell(Vector2Int coordinate)
    {
        object chunk = ((IDictionary)Chunks.GetValue(streamer))[coordinate];
        if (chunk == null) return new { coordinate, owner = false, entryReady = false };
        object Field(string name) => chunk.GetType().GetField(name, Fields)?.GetValue(chunk);
        return new { coordinate, owner = true, entryReady = (bool)Ready.Invoke(streamer, new[] { (object)coordinate, chunk }),
            state = Field("state"), terrain = Field("terrainReadiness"), physical = Field("physicalRepresentation"),
            required = Field("requiredContentReady"), overlays = Field("overlayReady"), ecology = Field("requiredEcologyReady"),
            appearance = Field("appearanceReady"), activation = Field("activationComplete"), failure = Field("failureReason") };
    }

    private static void Snapshot(double now, Vector3 position)
    {
        var cells = new List<object>();
        float size = streamer.chunkWorldSize;
        Vector3 forward = motor.transform.forward; forward.y = 0f; forward.Normalize();
        // note: This is an observed heading probe, not permission to enter; the production capsule gate remains authoritative.
        for (int index = 0; index <= 8; index++)
        {
            Vector3 point = position + forward * size * index;
            cells.Add(Cell(new Vector2Int(Mathf.FloorToInt((point.x - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / size),
                Mathf.FloorToInt((point.z - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / size))));
        }
        Snapshots.Add(new { elapsed = now - started, phase, position, walkSpeed = motor.walkSpeed,
            streamer.QueuedChunkCount, streamer.PendingTerrainCollisionCount, streamer.PendingTerrainPaintCount,
            streamer.PhysicalChunkCount, streamer.PhysicalDemandCount, streamer.ContentPreemptionCount,
            streamer.LastForwardReadyLeadCells, streamer.MinimumForwardReadyLeadCells, streamer.P95TerrainReadinessSeconds,
            streamer.TerrainAdmissionDiagnostics, visualOwner = VisualProgress?.Invoke(streamer, null),
            blocked = Cell(streamer.LastTraversalConstraintCell), headingCells = cells,
            publicationCalls = ((int[])Publications.GetValue(streamer)).ToArray() });
    }

    private static object Distribution(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        return sorted.Length == 0 ? null : new { samples = sorted.Length, p50 = sorted[(sorted.Length - 1) / 2],
            p95 = sorted[(int)((sorted.Length - 1) * .95)], p99 = sorted[(int)((sorted.Length - 1) * .99)], max = sorted[sorted.Length - 1],
            over33 = sorted.Count(x => x > 33d), over50 = sorted.Count(x => x > 50d), over90 = sorted.Count(x => x > 90d), over100 = sorted.Count(x => x > 100d) };
    }

    internal static void Abort() => Finish("ABORTED_LIFETIME");
    private static void PlayState(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Abort(); }
    private static void Finish(string reason)
    {
        if (!running && directory == null) return;
        running = false;
        // note: Release devices/settings and the temporary speed on every exit before writing diagnostics; ordinary persistence was never blocked or replaced.
        InputSystem.onBeforeUpdate -= Input; InputSystem.onAfterUpdate -= KeepCurrent;
        EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayState;
        AssemblyReloadEvents.beforeAssemblyReload -= Abort; EditorApplication.quitting -= Abort;
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        keyboard = null; mouse = null;
        if (originalSettings != null) InputSystem.settings = originalSettings;
        originalKeyboard?.MakeCurrent(); originalMouse?.MakeCurrent();
        if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
        settings = null; Application.runInBackground = originalBackground;
        if (motor != null) motor.walkSpeed = originalSpeed;
        if (contacts != null) UnityEngine.Object.DestroyImmediate(contacts);
        contacts = null;
        var metrics = new Dictionary<string, object>();
        for (int index = 0; index < Recorders.Count; index++)
        {
            var recorder = Recorders[index];
            var samples = new List<ProfilerRecorderSample>();
            if (recorder.Valid) recorder.CopyTo(samples);
            recorder.Dispose();
            metrics[Metrics[index]] = new { available = samples.Count > 0,
                unit = Metrics[index] == "GC.Alloc" ? "ProfilerRecorderSample.Count events; raw allocation values omitted, not bytes" : "nanoseconds rendered as milliseconds",
                distributionMs = Metrics[index] == "GC.Alloc" ? null : Distribution(samples.Select(x => x.Value / 1000000d)), calls = samples.Sum(x => x.Count) };
        }
        Recorders.Clear();
        if (directory != null)
            File.WriteAllText(directory + "/Receipt.json", JsonConvert.SerializeObject(new { utc = DateTime.UtcNow, reason,
                evidence = "ASSISTED_EDITOR_REAL_INPUT_AUTHORITATIVE_MOTOR_TEMPORARY_200_WALK_SPEED_NO_TELEPORT_NO_GATE_BYPASS", profile,
                acceptance = "CAPTURED_NOT_AUTOMATIC_PASS; virtual keyboard/mouse; fast bucket >=185m/s is diagnostic only; inspect actual plateau and whole-frame limits",
                runtimeMvid = typeof(YQInvestorPlayerMotor).Assembly.ManifestModule.ModuleVersionId,
                elapsed = EditorApplication.timeSinceStartup - started, phase, ackFrames, distance, fastDistance, fastSeconds,
                publicationStart, publicationEnd = streamer != null ? Publications.GetValue(streamer) : null,
                blocks = streamer != null ? streamer.TraversalConstraintBlockCount - blockStart : -1,
                metrics, frames = Frames, snapshots = Snapshots,
                cleanup = new { devicesRemoved = keyboard == null && mouse == null, inputSettingsRestored = InputSystem.settings == originalSettings,
                    speedRestored = motor == null || motor.walkSpeed == originalSpeed } }, Formatting.Indented,
                new Vector2JsonConverter(), new Vector3JsonConverter()));
        directory = null;
    }
}

// note: An owned temporary listener identifies legitimate capsule contacts; it applies no force or movement and is removed with the witness.
internal sealed class YQHighSpeedContactWitness : MonoBehaviour
{
    internal string LastContact { get; private set; }
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider != null)
            LastContact = hit.collider.GetType().Name + ":" + hit.collider.name + ":parent=" + hit.collider.transform.parent?.name;
    }
}
#endif
