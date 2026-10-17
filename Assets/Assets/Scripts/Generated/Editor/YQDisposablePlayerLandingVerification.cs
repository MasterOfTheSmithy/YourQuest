#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Named disposable-profile probes submit one ordinary motor impulse, then observe existing collision/damage owners without moving or restoring the player.
public static class YQDisposablePlayerLandingVerification
{
    private static readonly FieldInfo SupportContacts = typeof(YQPlayerRagdoll).GetField("supportContactCount", BindingFlags.Instance | BindingFlags.NonPublic);
    // note: These exact owner queries only inspect readiness; the mutating admission/demand APIs must not be used by observation.
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo AdmissionRange = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod("TryAdmissionRange", PrivateInstance);
    private static readonly MethodInfo EntryReady = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod("IsReadyForPlayerEntry", PrivateInstance);
    private static readonly MethodInfo OwnsBox = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod("OwnsPhysicsAdmissionCollider", PrivateInstance);
    private static readonly FieldInfo ChunkOwners = StreamField("_chunks"), Epoch = StreamField("_configurationEpoch"),
        Boxes = StreamField("_physicsAdmissionBoxes"), Pool = StreamField("_physicsAdmissionPool"),
        AdmissionEpoch = StreamField("_physicsAdmissionEpoch"), AdmissionActor = StreamField("_physicsAdmissionActor");
    private static readonly FieldInfo Shapes = RagField("shapes"), Bodies = RagField("bodies"), Held = RagField("admissionHeld"),
        HeldLinear = RagField("heldLinear"), HeldAngular = RagField("heldAngular"), StepBudget = RagField("maximumPhysicsStep");
    private static FieldInfo StreamField(string name) => typeof(YQPlayerFollowingSemanticChunkStreamer).GetField(name, PrivateInstance);
    private static FieldInfo RagField(string name) => typeof(YQPlayerRagdoll).GetField(name, PrivateInstance);
    private static YQPlayerFollowingSemanticChunkStreamer observedStreamer;
    private static int observedEpoch, physicsSamples, unknownPhysicsSamples, unreadyPhysicsSamples, heldSamples, maximumOwnedBoxes;
    private static Vector3 submittedVector;
    private static YQInvestorPlayerMotor motor;
    private static CharacterController controller;
    private static YQGeneratedWorldPlayerFallSafety safety;
    private static string profile, artifact, mode;
    private static double started, nextSample, settledAt;
    private static float initialHealth, minimumHealth, maximumHeight, maximumDownSpeed, submittedImpulse;
    private static int initialRecoveries, contacts, contactsAtFirstAirborne, unknownMotionSamples;
    private static float launchFootHeight, maximumPrimaryBodyHeight;
    private static bool airborne, ragdollObserved, invalidPose;
    private static readonly JArray Samples = new JArray();
    private static readonly JArray Errors = new JArray();

    [MenuItem("YourQuest/Terrain Refactor/Run Disposable Low Landing Probe")]
    public static void Low() => Begin("low", 6f);

    [MenuItem("YourQuest/Terrain Refactor/Run Disposable Tumble Landing Probe")]
    // note: Normal released-jump gravity shortens an unheld launch; this impulse exceeds the unchanged tumble threshold on the ordinary descent.
    public static void Tumble() => Begin("tumble", 36f);

    [MenuItem("YourQuest/Terrain Refactor/Run Disposable Lateral Tumble Probe")]
    // note: Finite unprotected acceleration uses the existing tumble threshold; no chunks are made unavailable and no pose is prescribed.
    public static void LateralTumble() => Begin("lateral_tumble", new Vector3(45f, 36f, 0f), false);

    private static void Begin(string requestedMode, float impulse) => Begin(requestedMode, Vector3.up * impulse, true);

    private static void Begin(string requestedMode, Vector3 impulse, bool protectsFromAcceleration)
    {
        if (motor != null) throw new InvalidOperationException("A disposable landing observation is already running.");
        var profiles = YQProfileSaveSystem.Instance;
        string name = profiles?.FindProfile(profiles.ActiveProfileId)?.displayName;
        var player = YQInvestorPlayerMotor.ActiveMotor;
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ||
            name == null || !name.StartsWith("Terrain Apron Playtest ", StringComparison.Ordinal) ||
            YQDeveloperTestSession.Active || player == null || !player.IsAuthoritative || !player.CanProcessMovementInput ||
            !player.IsGrounded || player.IsRagdoll || player.IsAutoWalking || player.PlanarVelocity.sqrMagnitude > .25f ||
            player.vitals == null || player.vitals.IsDead || SupportContacts == null)
            throw new InvalidOperationException("Landing probes require a released, stationary, grounded disposable terrain player with no protected test lease.");
        var capsule = player.GetComponent<CharacterController>();
        var streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        if (AdmissionRange == null || EntryReady == null || OwnsBox == null || ChunkOwners == null || Epoch == null ||
            Boxes == null || Pool == null || AdmissionEpoch == null || AdmissionActor == null || Shapes == null || Bodies == null ||
            Held == null || HeldLinear == null || HeldAngular == null || StepBudget == null)
            throw new InvalidOperationException("Exact physics/readiness observation contracts are unavailable.");
        if (!protectsFromAcceleration && (!Finite(impulse) || float.IsNaN(Time.fixedDeltaTime) || float.IsInfinity(Time.fixedDeltaTime) || Time.fixedDeltaTime <= 0f ||
            impulse.magnitude * Time.fixedDeltaTime > 2f || impulse.magnitude < player.extremeVelocityChange ||
            impulse.magnitude / Mathf.Max(.01f, player.accelerationSampleSeconds) < player.extremeAcceleration))
            throw new InvalidOperationException("Finite lateral probe requires current tumble thresholds and a nominal step of at most 2 metres.");
        Vector3 probeDelta = Vector3.right * .02f;
        if (capsule == null || !capsule.enabled || !capsule.detectCollisions || streamer == null ||
            !streamer.TryConstrainMovement(player.transform.position, probeDelta, capsule.radius, capsule.skinWidth, out var permitted) ||
            (permitted - probeDelta).sqrMagnitude > .000001f)
            throw new InvalidOperationException("Current streamed assets/collision are not ready for a physical landing probe.");

        // note: The existing emergency observer is required evidence; absence cannot be reported as zero recoveries.
        var observer = UnityEngine.Object.FindFirstObjectByType<YQGeneratedWorldPlayerFallSafety>();
        if (observer == null) throw new InvalidOperationException("The production fall-safety observer is unavailable.");
        motor = player; controller = capsule; safety = observer;
        observedStreamer = streamer; observedEpoch = (int)Epoch.GetValue(streamer);
        physicsSamples = unknownPhysicsSamples = unreadyPhysicsSamples = heldSamples = maximumOwnedBoxes = 0;
        profile = profiles.ActiveProfileId;
        artifact = WorldStateManager.Instance?.State?.generatedWorldPlan?.spatialPlanV2?.contentHash;
        mode = requestedMode;
        started = EditorApplication.timeSinceStartup; nextSample = started; settledAt = -1d;
        initialHealth = minimumHealth = player.vitals.CurrentHealth;
        maximumHeight = player.transform.position.y;
        maximumDownSpeed = 0f; submittedImpulse = impulse.y; submittedVector = impulse;
        initialRecoveries = observer.RecoveryCount; contacts = 0; contactsAtFirstAirborne = -1; unknownMotionSamples = 0;
        launchFootHeight = capsule.bounds.min.y; maximumPrimaryBodyHeight = float.NegativeInfinity;
        airborne = ragdollObserved = invalidPose = false;
        Samples.Clear(); Errors.Clear();
        Application.logMessageReceived += ObserveLog;
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Abort;
        EditorApplication.quitting += Abort;
        // note: Use the supported impulse executor; gravity, collision, damage thresholds, clutch and ragdoll admission remain production behavior.
        player.ApplyPhysicalImpulse(impulse, protectsFromAcceleration);
        Debug.Log("[YQDisposableLanding] Started " + requestedMode + " observation through the production motor.");
    }

    private static void ObserveLog(string message, string stack, LogType type)
    {
        if (message.Contains("[YQPlayerRagdoll] INVALID POSE") || message.Contains("Player fall-through recovered"))
        {
            invalidPose |= message.Contains("INVALID POSE");
            if (Errors.Count < 8) Errors.Add(message);
        }
    }

    private static void Tick()
    {
        // note: A diagnostic owner disappearing or throwing must unwind observation callbacks rather than strand an Editor update loop.
        try { ObserveTick(); }
        catch (Exception error)
        {
            if (Errors.Count < 8) Errors.Add(error.ToString());
            Finish("ABORTED_OBSERVATION_EXCEPTION");
            Debug.LogException(error);
        }
    }

    private static void ObserveTick()
    {
        if (motor == null) { Abort(); return; }
        if (controller == null || safety == null || motor.vitals == null)
        { Finish("ABORTED_OBSERVATION_OWNER_UNAVAILABLE"); return; }
        if (!EditorApplication.isPlaying || YQProfileSaveSystem.Instance?.ActiveProfileId != profile ||
            WorldStateManager.Instance?.State?.generatedWorldPlan?.spatialPlanV2?.contentHash != artifact || !motor.IsAuthoritative || observedStreamer == null ||
            YQPlayerFollowingSemanticChunkStreamer.Active != observedStreamer || (int)Epoch.GetValue(observedStreamer) != observedEpoch)
        { Finish("ABORTED_CONTEXT_CHANGED"); return; }
        double now = EditorApplication.timeSinceStartup;
        minimumHealth = Mathf.Min(minimumHealth, motor.vitals.CurrentHealth);
        maximumHeight = Mathf.Max(maximumHeight, motor.transform.position.y);
        ragdollObserved |= motor.IsRagdoll;
        var body = motor.GetComponent<YQPlayerRagdoll>();
        if (body != null && body.IsSimulating) contacts = Mathf.Max(contacts, (int)SupportContacts.GetValue(body));
        // note: The disabled motor capsule retains cached grounded/vertical values; native skeletal motion is the evidence while ragdoll owns physics.
        bool motionKnown = TryObserveMotion(body, out float verticalVelocity, out bool sampleAirborne,
            out Vector3 primaryPosition, out float lowestColliderY, out string motionSource);
        if (motionKnown)
        {
            maximumDownSpeed = Mathf.Max(maximumDownSpeed, -verticalVelocity);
            if (body != null && body.IsSimulating) maximumPrimaryBodyHeight = Mathf.Max(maximumPrimaryBodyHeight, primaryPosition.y);
            if (sampleAirborne && !airborne) contactsAtFirstAirborne = contacts;
            airborne |= sampleAirborne;
        }
        bool dead = motor.vitals.IsDead;
        if ((now >= nextSample || dead) && Samples.Count < 512)
        {
            nextSample = now + .05d;
            if (!motionKnown) unknownMotionSamples++;
            Vector3 position = motor.transform.position;
            Samples.Add(new JObject {
                ["seconds"] = now - started, ["position"] = new JArray(position.x, position.y, position.z),
                ["grounded"] = controller.enabled ? new JValue(motor.IsGrounded) : JValue.CreateNull(), ["ragdoll"] = motor.IsRagdoll,
                ["motorGroundedCached"] = motor.IsGrounded, ["motorVerticalVelocityCached"] = motor.VerticalVelocity,
                ["motionKnown"] = motionKnown, ["motionSource"] = motionSource, ["sampleAirborne"] = sampleAirborne,
                ["primaryBodyPosition"] = motionKnown && body != null && body.IsSimulating ? Vector(primaryPosition) : null,
                ["lowestRagdollColliderY"] = motionKnown && body != null && body.IsSimulating ? new JValue(lowestColliderY) : JValue.CreateNull(),
                ["capsuleEnabled"] = controller.enabled, ["capsuleCollisions"] = controller.detectCollisions,
                ["verticalVelocity"] = motionKnown ? new JValue(verticalVelocity) : JValue.CreateNull(), ["health"] = motor.vitals.CurrentHealth,
                ["pendingDamage"] = motor.HasPendingFallDamage, ["supportContacts"] = contacts,
                ["emergencyRecoveries"] = safety.RecoveryCount - initialRecoveries,
                ["physicsAdmission"] = ObservePhysics(body)
            });
        }
        // note: A lethal physical contact cannot recover to standing; finish that distinct observation without changing damage or restoring health.
        if (dead) { Finish(ObservedImpactAfterAirborne(airborne, contactsAtFirstAirborne, contacts)
            ? "OBSERVED_LETHAL_IMPACT" : "OBSERVED_DEATH_WITHOUT_POST_AIRBORNE_SUPPORT"); return; }
        bool settled = airborne && controller.enabled && motor.IsGrounded && !motor.IsRagdoll && !motor.IsRecovering && !motor.HasPendingFallDamage;
        if (settled && settledAt < 0d) settledAt = now;
        if (!settled) settledAt = -1d;
        if (settledAt >= 0d && now - settledAt >= .5d) Finish("OBSERVED_SETTLED");
        else if (now - started >= 20d) Finish("OBSERVATION_DEADLINE");
    }

    private static bool IsRagdollAboveLaunchSupport(float lowestColliderY, float launchPlaneY) =>
        !float.IsNaN(lowestColliderY) && !float.IsInfinity(lowestColliderY) &&
        !float.IsNaN(launchPlaneY) && !float.IsInfinity(launchPlaneY) && lowestColliderY > launchPlaneY + .25f;

    private static bool ObservedImpactAfterAirborne(bool observedAirborne, int firstAirContacts, int totalContacts) =>
        observedAirborne && firstAirContacts >= 0 && totalContacts > firstAirContacts;

    private static string ClassifyObservation(bool passed, string completion, bool dead, bool postAirborneContact, bool cleanEvidence) =>
        passed && !dead ? "PASS_ASSISTED_NATIVE_LANDING" :
        completion == "OBSERVED_LETHAL_IMPACT" && dead && postAirborneContact && cleanEvidence
            ? "OBSERVED_ASSISTED_LETHAL_IMPACT" : "INCOMPLETE_OR_FAILED";

    private static bool TryObserveMotion(YQPlayerRagdoll ragdoll, out float verticalVelocity, out bool sampleAirborne,
        out Vector3 primaryPosition, out float lowestColliderY, out string source)
    {
        verticalVelocity = 0f; sampleAirborne = false; primaryPosition = Vector3.zero; lowestColliderY = 0f;
        source = "UNAVAILABLE";
        if (ragdoll != null && ragdoll.IsSimulating)
        {
            var bodies = Bodies.GetValue(ragdoll) as Rigidbody[]; var shapes = Shapes.GetValue(ragdoll) as Collider[];
            if (bodies == null || shapes == null || bodies.Length == 0 || bodies.Length > 32 || bodies.Length != shapes.Length ||
                (YQPlayerRagdoll)AdmissionActor.GetValue(observedStreamer) != ragdoll ||
                (int)AdmissionEpoch.GetValue(observedStreamer) != observedEpoch || (bool)Held.GetValue(ragdoll)) return false;
            lowestColliderY = float.PositiveInfinity;
            for (int index = 0; index < bodies.Length; index++)
            {
                var body = bodies[index]; var shape = shapes[index];
                if (body == null || body.isKinematic || !body.detectCollisions || shape == null || shape.attachedRigidbody != body ||
                    !shape.enabled || !shape.gameObject.activeInHierarchy || !Finite(body.position) || !Finite(body.linearVelocity) ||
                    !Finite(shape.bounds.center) || !Finite(shape.bounds.size) || shape.bounds.size.sqrMagnitude <= 0f) return false;
                lowestColliderY = Mathf.Min(lowestColliderY, shape.bounds.min.y);
            }
            verticalVelocity = bodies[0].linearVelocity.y; primaryPosition = bodies[0].position;
            // note: This bounded launch witness measures every actual body shape above the captured launch support plane; it is not a terrain query or an instantaneous grounded-state authority.
            sampleAirborne = IsRagdollAboveLaunchSupport(lowestColliderY, launchFootHeight);
            source = "NATIVE_PRIMARY_RIGIDBODY; ALL_SHAPES_CLEAR_LAUNCH_SUPPORT_PLANE";
            return true;
        }
        if (motor.IsRagdoll || !controller.enabled || !controller.detectCollisions ||
            float.IsNaN(motor.VerticalVelocity) || float.IsInfinity(motor.VerticalVelocity)) return false;
        verticalVelocity = motor.VerticalVelocity; sampleAirborne = !motor.IsGrounded;
        source = "ACTIVE_AUTHORITATIVE_MOTOR_CAPSULE"; return true;
    }

    private static JArray Vector(Vector3 value) => new JArray(value.x, value.y, value.z);
    private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    // note: Actual detached collider bounds qualify against the same full-asset predicate, without preparing, retiring or admitting any cell.
    private static JObject ObservePhysics(YQPlayerRagdoll ragdoll)
    {
        var result = new JObject { ["simulating"] = ragdoll != null && ragdoll.IsSimulating,
            ["frontierContacts"] = "NOT_CAPTURED" };
        var boxes = (IDictionary)Boxes.GetValue(observedStreamer);
        var chunks = (IDictionary)ChunkOwners.GetValue(observedStreamer);
        int epoch = (int)AdmissionEpoch.GetValue(observedStreamer), live = 0;
        var boxRows = new JArray();
        foreach (DictionaryEntry pair in boxes)
        {
            if (boxRows.Count >= 256) throw new InvalidOperationException("Owned frontier observation exceeds its runtime bound.");
            var cell = (Vector2Int)pair.Key; var box = pair.Value as BoxCollider;
            bool owned = box != null && (bool)OwnsBox.Invoke(observedStreamer, new object[] { box, epoch, cell });
            bool ready = chunks.Contains(cell) && (bool)EntryReady.Invoke(observedStreamer, new object[] { cell, chunks[cell] });
            if (owned) live++;
            boxRows.Add(new JObject { ["cell"] = new JArray(cell.x, cell.y), ["colliderId"] = box != null ? box.GetInstanceID() : 0,
                ["ownedEnabled"] = owned, ["entryReadyNow"] = ready,
                ["boundsCenter"] = box != null ? Vector(box.bounds.center) : null, ["boundsSize"] = box != null ? Vector(box.bounds.size) : null });
        }
        maximumOwnedBoxes = Mathf.Max(maximumOwnedBoxes, live);
        result["liveOwnedBoxes"] = live; result["boxes"] = boxRows;
        result["poolCount"] = ((ICollection)Pool.GetValue(observedStreamer)).Count;
        result["admissionEpoch"] = epoch;
        result["admissionActorMatches"] = ragdoll != null && (YQPlayerRagdoll)AdmissionActor.GetValue(observedStreamer) == ragdoll;
        if (ragdoll == null || !ragdoll.IsSimulating) { result["boundsReadiness"] = "NOT_SIMULATING"; return result; }
        physicsSamples++;
        bool held = (bool)Held.GetValue(ragdoll); if (held) heldSamples++;
        result["momentumHeld"] = held; result["maximumPhysicsStep"] = (float)StepBudget.GetValue(ragdoll);
        var shapes = (Collider[])Shapes.GetValue(ragdoll); var bodies = (Rigidbody[])Bodies.GetValue(ragdoll);
        var linear = (Vector3[])HeldLinear.GetValue(ragdoll); var angular = (Vector3[])HeldAngular.GetValue(ragdoll);
        var rows = new JArray(); bool known = shapes != null && shapes.Length > 0 && shapes.Length <= 32 &&
            epoch == observedEpoch && (YQPlayerRagdoll)AdmissionActor.GetValue(observedStreamer) == ragdoll &&
            (!held || (linear != null && angular != null && linear.Length == shapes.Length && angular.Length == shapes.Length)), readyAll = true;
        if (known) for (int i = 0; i < shapes.Length; i++)
        {
            Collider shape = shapes[i]; Rigidbody body = bodies != null && i < bodies.Length ? bodies[i] : null;
            bool valid = shape != null && shape.enabled && shape.gameObject.activeInHierarchy && body != null &&
                Finite(shape.bounds.center) && Finite(shape.bounds.size) && shape.bounds.size.sqrMagnitude > 0f;
            var row = new JObject { ["index"] = i, ["validBounds"] = valid };
            if (valid)
            {
                Bounds bounds = shape.bounds;
                object[] args = { bounds, 0, 0, 0, 0 };
                bool range = (bool)AdmissionRange.Invoke(observedStreamer, args), ready = range; int cells = 0, missing = 0;
                if (range) for (int z = (int)args[3]; z <= (int)args[4]; z++) for (int x = (int)args[1]; x <= (int)args[2]; x++)
                {
                    var cell = new Vector2Int(x, z); cells++;
                    if (!chunks.Contains(cell) || !(bool)EntryReady.Invoke(observedStreamer, new object[] { cell, chunks[cell] })) { ready = false; missing++; }
                }
                row["colliderId"] = shape.GetInstanceID(); row["boundsCenter"] = Vector(bounds.center); row["boundsSize"] = Vector(bounds.size);
                row["entryReady"] = ready; row["cells"] = cells; row["unreadyCells"] = missing;
                row["linearVelocity"] = Vector(body.linearVelocity); row["angularVelocity"] = Vector(body.angularVelocity);
                row["kinematic"] = body.isKinematic; row["detectCollisions"] = body.detectCollisions; row["collisionMode"] = body.collisionDetectionMode.ToString();
                row["heldLinear"] = held && linear != null && i < linear.Length ? Vector(linear[i]) : null;
                row["heldAngular"] = held && angular != null && i < angular.Length ? Vector(angular[i]) : null;
                known &= range; readyAll &= ready;
            }
            else known = false;
            rows.Add(row);
        }
        if (!known) unknownPhysicsSamples++; else if (!readyAll) unreadyPhysicsSamples++;
        result["boundsReadiness"] = !known ? "UNAVAILABLE" : readyAll ? "FULL_ASSETS_READY" : "UNREADY_FOOTPRINT_OBSERVED";
        result["colliders"] = rows; return result;
    }

    private static void Abort()
    {
        if (profile != null) Finish("ABORTED_EDITOR_LIFECYCLE");
        else Cleanup();
    }

    private static void Finish(string completion)
    {
        try
        {
            bool current = motor != null && safety != null && controller != null;
            int recoveries = current ? safety.RecoveryCount - initialRecoveries : -1;
            float damage = initialHealth - minimumHealth;
            bool dead = current && motor.vitals != null && motor.vitals.IsDead;
            bool standing = current && !dead && controller.enabled && controller.detectCollisions && motor.IsGrounded && !motor.IsRagdoll;
            bool passed = completion == "OBSERVED_SETTLED" && airborne && standing && recoveries == 0 && !invalidPose && unknownMotionSamples == 0 &&
                (mode == "low" ? !ragdollObserved && damage <= .01f : ragdollObserved && contacts > 0 && damage > .01f) &&
                (mode != "lateral_tumble" || (physicsSamples > 0 && unknownPhysicsSamples == 0 && unreadyPhysicsSamples == 0));
            bool postAirborneContact = ObservedImpactAfterAirborne(airborne, contactsAtFirstAirborne, contacts);
            string status = ClassifyObservation(passed, completion, dead, postAirborneContact,
                recoveries == 0 && !invalidPose && unknownMotionSamples == 0 && physicsSamples > 0 && unknownPhysicsSamples == 0 && unreadyPhysicsSamples == 0);
            string folder = "outputs/TerrainRefactor_20261014_StreamingRetest/NativeLanding";
            Directory.CreateDirectory(folder);
            string path = folder + "/" + mode + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".json";
            File.WriteAllText(path, new JObject {
                ["utc"] = DateTime.UtcNow.ToString("O"), ["status"] = status,
                ["completion"] = completion, ["profile"] = profile, ["artifact"] = artifact, ["mode"] = mode,
                ["airborneObserved"] = airborne, ["ragdollObserved"] = ragdollObserved, ["supportContacts"] = contacts,
                ["contactsAtFirstAirborne"] = contactsAtFirstAirborne, ["postAirborneSupportContact"] = postAirborneContact,
                ["dead"] = dead, ["unknownMotionSamples"] = unknownMotionSamples, ["launchSupportPlaneY"] = launchFootHeight,
                ["maximumPrimaryBodyHeight"] = float.IsNegativeInfinity(maximumPrimaryBodyHeight) ? JValue.CreateNull() : new JValue(maximumPrimaryBodyHeight),
                ["ragdollAirborneEvidenceLimit"] = "ALL_OBSERVED_SHAPE_BOUNDS_ABOVE_LAUNCH_SUPPORT_PLANE; NOT_INSTANTANEOUS_TERRAIN_SUPPORT_QUERY",
                ["maximumHeight"] = maximumHeight, ["maximumHealthLoss"] = damage, ["standingRecovered"] = standing,
                ["submittedImpulse"] = submittedImpulse, ["submittedImpulseVector"] = Vector(submittedVector),
                ["configurationEpoch"] = observedEpoch, ["physicsSamples"] = physicsSamples,
                ["unknownPhysicsSamples"] = unknownPhysicsSamples, ["unreadyPhysicsSamples"] = unreadyPhysicsSamples,
                ["heldSamples"] = heldSamples, ["maximumOwnedFrontierBoxes"] = maximumOwnedBoxes,
                ["frontierEvidence"] = maximumOwnedBoxes == 0 ? "NO_FRONTIER_BOX_OBSERVED; FRONTIER_CONTACT_NOT_EXERCISED" :
                    "OWNED_FRONTIER_GEOMETRY_OBSERVED; CONTACT_NOT_CAPTURED",
                ["samplingLimit"] = "EDITOR_50MS_SAMPLES_NOT_EVERY_PHYSICS_SUBSTEP; NOT_ALL_SPEED_CERTIFICATION", ["maximumObservedDownSpeed"] = maximumDownSpeed,
                ["emergencyRecoveries"] = recoveries, ["invalidPose"] = invalidPose, ["errors"] = Errors,
                ["samples"] = Samples, ["evidence"] = "ASSISTED_PUBLIC_MOTOR_IMPULSE_AND_NATIVE_PHYSICS; NOT_ORDINARY_JUMP_OR_ALL_SPEED_CERTIFICATION"
            }.ToString());
            Debug.Log("[YQDisposableLanding] " + status + " receipt=" + path);
        }
        finally { Cleanup(); }
    }

    private static void Cleanup()
    {
        // note: Observation owns callbacks only; it never teleports, restores vitals, writes a save, or cancels gameplay physics.
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= ObserveLog;
        AssemblyReloadEvents.beforeAssemblyReload -= Abort;
        EditorApplication.quitting -= Abort;
        motor = null; controller = null; safety = null; observedStreamer = null; profile = artifact = mode = null;
    }
}
#endif
