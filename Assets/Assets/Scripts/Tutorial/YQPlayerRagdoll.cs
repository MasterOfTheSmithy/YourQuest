using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQPlayerRagdoll : MonoBehaviour
{
    public bool IsSimulating { get; private set; }
    private YQInvestorPlayerMotor motor;
    private CharacterController capsule;
    private Animator animator;
    private Rigidbody[] bodies;
    private Collider[] shapes;
    private Vector3[] positions;
    private Quaternion[] rotations;
    private Vector3 hipsOffset, safePosition;
    private float started, settledAt, fallApex, downwardSpeed;
    private bool capsuleWasEnabled;
    private bool capsuleHandoffOwned;
    private Transform simulationRoot, simulationParent;
    private Vector3 simulationLocalPosition, simulationLocalScale;
    private Quaternion simulationLocalRotation;
    private int simulationSibling;
    private bool simulationRootDetached;
    private bool invalidPoseReported;
    private bool admissionHeld, recoveryAdmissionRejected;
    private Vector3[] heldLinear, heldAngular;
    private YQPlayerFollowingSemanticChunkStreamer admissionStreamer;
    private float nextAdmissionReport;
    private int invalidPoseReportCount;
    private Collider lastSupportCollider;
    private Vector3 lastSupportPoint;
    private int supportContactCount;
    private float maximumPhysicsStep = 8f;
    private readonly Collider[] clearance = new Collider[24];
    private readonly RaycastHit[] supportHits = new RaycastHit[24];
    private static readonly HumanBodyBones[] Bones = {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg };

    private bool EnsureRig()
    {
        if (bodies != null) return true;
        motor = GetComponent<YQInvestorPlayerMotor>();
        capsule = GetComponent<CharacterController>();
        animator = GetComponent<YQPlayerEquipmentVisual>()?.CharacterAnimator;
        if (animator == null || !animator.isHuman || capsule == null || animator.GetBoneTransform(HumanBodyBones.Hips) == null) return false;
        // note: Only the existing subordinate body may be detached; the canonical player/capsule and camera retain their ownership.
        simulationRoot = animator.transform;
        if (simulationRoot == transform || !simulationRoot.IsChildOf(transform)) return false;
        var list = new List<Rigidbody>();
        var colliders = new List<Collider>();
        var boneBodies = new Dictionary<Transform, Rigidbody>();
        // note: Build only project-owned transient physics on the canonical body; imported prefabs and skeleton assets remain untouched.
        foreach (HumanBodyBones key in Bones)
        {
            Transform bone = animator.GetBoneTransform(key);
            if (bone == null || !bone.IsChildOf(simulationRoot) || bone.GetComponent<Rigidbody>() != null) return false;
        }
        foreach (HumanBodyBones key in Bones)
        {
            Transform bone = animator.GetBoneTransform(key);
            if (bone == null) continue;
            Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.mass = key == HumanBodyBones.Hips ? 16f : 4f;
            body.linearDamping = .25f;
            body.angularDamping = 3f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            CapsuleCollider shape = bone.gameObject.AddComponent<CapsuleCollider>();
            bool torso = key == HumanBodyBones.Hips || key == HumanBodyBones.Spine;
            // note: Imported humanoid bones may be centimetre-scaled; collider dimensions must describe world metres.
            float boneScale = Mathf.Max(.0001f, Mathf.Max(Mathf.Abs(bone.lossyScale.x), Mathf.Abs(bone.lossyScale.y), Mathf.Abs(bone.lossyScale.z)));
            shape.radius = (key == HumanBodyBones.Head ? .12f : torso ? .16f : .065f) / boneScale;
            Transform child = bone.childCount > 0 ? bone.GetChild(0) : null;
            Vector3 axis = child != null ? bone.InverseTransformPoint(child.position) : Vector3.up * .2f;
            int direction = Mathf.Abs(axis.x) > Mathf.Abs(axis.y) ? 0 : 1;
            if (Mathf.Abs(axis.z) > Mathf.Max(Mathf.Abs(axis.x), Mathf.Abs(axis.y))) direction = 2;
            shape.direction = direction;
            shape.height = Mathf.Max(shape.radius * 2f, axis.magnitude);
            shape.center = axis * .4f;
            shape.enabled = false;
            list.Add(body); colliders.Add(shape); boneBodies.Add(bone, body);
            bone.gameObject.AddComponent<YQPlayerRagdollContact>().owner = this;
        }
        foreach (Rigidbody body in list)
        {
            if (body == list[0]) continue;
            Transform parent = body.transform.parent;
            while (parent != null && !boneBodies.ContainsKey(parent)) parent = parent.parent;
            if (parent == null) continue;
            CharacterJoint joint = body.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = boneBodies[parent];
            joint.enablePreprocessing = false;
            joint.enableProjection = true;
            joint.lowTwistLimit = new SoftJointLimit { limit = -25f };
            joint.highTwistLimit = new SoftJointLimit { limit = 25f };
            joint.swing1Limit = new SoftJointLimit { limit = 45f };
            joint.swing2Limit = new SoftJointLimit { limit = 30f };
        }
        bodies = list.ToArray(); shapes = colliders.ToArray();
        // note: Non-bone body colliders can resolve the existing actor too; reuse the bone relay if the Animator root itself owns a body.
        var rootContact = simulationRoot.GetComponent<YQPlayerRagdollContact>() ?? simulationRoot.gameObject.AddComponent<YQPlayerRagdollContact>();
        rootContact.owner = this;
        positions = new Vector3[bodies.Length]; rotations = new Quaternion[bodies.Length];
        hipsOffset = bodies[0].position - transform.position;
        return true;
    }

    public bool TryBegin(Vector3 velocity)
    {
        // note: Invalid generated/external impulses must never enter native skeletal physics.
        if (!Finite(velocity)) return false;
        if (IsSimulating || motor == null && GetComponent<YQInvestorPlayerMotor>() == null || !EnsureRig()) return false;
        if (!motor.IsAuthoritative || !capsule.enabled || motor.vitals != null && motor.vitals.IsDead) return false;
        if (simulationRoot == null || simulationRootDetached || !simulationRoot.IsChildOf(transform)) return false;
        // note: Animation/controller transforms can be newer than the kinematic native poses when automatic synchronization is disabled.
        Physics.SyncTransforms();
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null || shapes[i] == null || !bodies[i].transform.IsChildOf(simulationRoot)) return false;
            positions[i] = bodies[i].transform.position;
            rotations[i] = bodies[i].transform.rotation;
            if (!Finite(positions[i])) return false;
        }
        Collider[] owned = GetComponentsInChildren<Collider>(true);
        safePosition = transform.position;
        hipsOffset = positions[0] - transform.position;
        started = Time.time; settledAt = -1f;
        fallApex = transform.position.y; downwardSpeed = Mathf.Max(0f, -velocity.y);
        maximumPhysicsStep = Mathf.Max(8f, velocity.magnitude * Time.fixedDeltaTime * 4f);
        capsuleWasEnabled = capsule.enabled;
        capsuleHandoffOwned = true;
        animator.enabled = false;
        capsule.enabled = false;
        DetachSimulationRoot();
        invalidPoseReported = false;
        lastSupportCollider = null; supportContactCount = 0;
        // note: Only ragdoll shapes collide while active, and self/equipment collisions cannot inject artificial launch energy.
        for (int i = 0; i < bodies.Length; i++)
        {
            shapes[i].enabled = true;
            foreach (Collider own in owned) if (own != shapes[i]) Physics.IgnoreCollision(shapes[i], own, true);
            // note: Bind the captured animated pose once while kinematic; subsequent physical steps never rewrite active body poses to undo ancestor movement.
            bodies[i].position = positions[i]; bodies[i].rotation = rotations[i];
            bodies[i].isKinematic = false;
            bodies[i].linearVelocity = velocity;
            bodies[i].angularVelocity = Vector3.zero;
        }
        Physics.SyncTransforms();
        IsSimulating = true;
        // note: Install the existing streamer's physical frontier before any newly enabled bone can enter a native physics step.
        if (!PreparePhysicalAdmission()) HoldPhysicalAdmission();
        return true;
    }

    private void FixedUpdate()
    {
        if (!IsSimulating) return;
        // note: Modal pause and loss of ownership stop physics without completing or discarding a pending landing.
        bool suspended = RuntimeModalUiBlocker.IsBlocked || !motor.IsAuthoritative || YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked;
        foreach (Rigidbody body in bodies) if (body.isKinematic != (suspended || admissionHeld)) body.isKinematic = suspended || admissionHeld;
        if (suspended) return;
        Vector3 root = bodies[0].position - hipsOffset;
        Vector3 predicted = root + AdmissionVelocity(0) * Time.fixedDeltaTime;
        // note: A broken joint/root handoff previously sent huge coordinates and footprints into synchronous terrain creation. Retire the invalid simulation before querying streaming.
        bool validPose = IsPhysicsStepSafe(transform.position, root, predicted, maximumPhysicsStep);
        string invalidReason = validPose ? string.Empty : "root_or_predicted_step";
        int invalidBody = -1;
        float maximumBodySpan = Mathf.Max(3f, capsule.height * 2f);
        for (int i = 0; validPose && i < bodies.Length; i++)
        {
            validPose = bodies[i] != null && Finite(bodies[i].position) && Finite(AdmissionVelocity(i)) &&
                (bodies[i].position - bodies[0].position).sqrMagnitude <= maximumBodySpan * maximumBodySpan;
            if (!validPose) { invalidReason = "body_missing_nonfinite_or_joint_span"; invalidBody = i; }
        }
        if (!validPose)
        {
            // note: Name the first violated physics constraint before teardown destroys the collision handoff evidence.
            if (!invalidPoseReported && invalidPoseReportCount < 3)
            {
                invalidPoseReported = true;
                invalidPoseReportCount++;
                Debug.LogError("[YQPlayerRagdoll] INVALID POSE reason=" + invalidReason + " body=" + invalidBody +
                    " root=" + root + " predicted=" + predicted + " admitted=" + transform.position +
                    " stepBudget=" + maximumPhysicsStep + " detached=" + simulationRootDetached +
                    " capsuleEnabled=" + capsule.enabled + " capsuleCollisions=" + capsule.detectCollisions +
                    " hipsVelocity=" + bodies[0].linearVelocity + " hipsCollisions=" + bodies[0].detectCollisions +
                    " hipsShapeEnabled=" + shapes[0].enabled + " supportContacts=" + supportContactCount +
                    " support=" + (lastSupportCollider != null ? lastSupportCollider.name : "none") +
                    " supportPoint=" + lastSupportPoint);
            }
            ResetForTerrainRecovery();
            motor.ResetAfterFallRecovery();
            Debug.LogError("[YQPlayerRagdoll] Invalid physics pose retired before terrain admission. Last admitted root=" + transform.position);
            return;
        }
        // note: Static owned entry contacts let the native solver handle limbs, external impacts and tumbling. No normal streaming failure rewrites an active body's pose.
        if (!PreparePhysicalAdmission())
        {
            transform.position = root;
            fallApex = Mathf.Max(fallApex, root.y); downwardSpeed = Mathf.Max(downwardSpeed, -AdmissionVelocity(0).y);
            HoldPhysicalAdmission(); return;
        }
        ResumePhysicalAdmission();
        for (int i = 0; i < bodies.Length; i++) { positions[i] = bodies[i].position; rotations[i] = bodies[i].rotation; }
        transform.position = root;
        fallApex = Mathf.Max(fallApex, root.y);
        downwardSpeed = Mathf.Max(downwardSpeed, -bodies[0].linearVelocity.y);
        // note: The detached body is owned by native physics; following its hips moves only the canonical capsule/camera root.
        bool dead = motor.vitals != null && motor.vitals.IsDead;
        if (dead) return;
        if (bodies[0].linearVelocity.sqrMagnitude < 1f && Time.time - started > .65f)
        {
            if (settledAt < 0f) settledAt = Time.time;
            if (Time.time - settledAt > .3f && !motor.HasPendingFallDamage) TryRecover(false);
        }
        else settledAt = -1f;
    }

    private bool PreparePhysicalAdmission()
    {
        var streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        if (streamer == null || !streamer.isActiveAndEnabled) return admissionStreamer == null;
        if (admissionStreamer != null && admissionStreamer != streamer) admissionStreamer.ReleasePlayerPhysicsAdmission(this);
        admissionStreamer = streamer;
        float reach = maximumPhysicsStep;
        for (int i = 0; i < bodies.Length; i++) reach = Mathf.Max(reach, AdmissionVelocity(i).magnitude * Time.fixedDeltaTime);
        bool ready = streamer.TryPreparePlayerPhysicsAdmission(this, shapes, reach, out string failure);
        if (!ready && Time.unscaledTime >= nextAdmissionReport)
        { nextAdmissionReport = Time.unscaledTime + 10f; Debug.LogWarning("[YQPlayerRagdoll] Physics held at current pose: " + failure); }
        return ready;
    }

    private Vector3 AdmissionVelocity(int index) => admissionHeld ? heldLinear[index] : bodies[index].linearVelocity;

    private void HoldPhysicalAdmission()
    {
        // note: Retain the actual pose and pending fall/impact state. An unfinished frontier holds physics without rewinding bones or zeroing the momentum restored after readiness.
        if (!admissionHeld)
        {
            heldLinear ??= new Vector3[bodies.Length]; heldAngular ??= new Vector3[bodies.Length];
            for (int i = 0; i < bodies.Length; i++) { heldLinear[i] = bodies[i].linearVelocity; heldAngular[i] = bodies[i].angularVelocity; }
            admissionHeld = true;
        }
        foreach (var body in bodies) body.isKinematic = true;
    }

    private void ResumePhysicalAdmission()
    {
        if (!admissionHeld) return;
        for (int i = 0; i < bodies.Length; i++)
        { bodies[i].isKinematic = false; bodies[i].linearVelocity = heldLinear[i]; bodies[i].angularVelocity = heldAngular[i]; }
        admissionHeld = false;
    }

    public bool TryRecover(bool clutch)
    {
        if (!IsSimulating) return true;
        if (admissionHeld) return false;
        if (!clutch && motor.vitals != null && motor.vitals.IsDead) return false;
        Vector3 candidate = transform.position;
        bool clear = TryGroundedPosition(candidate, out candidate);
        // note: An unfinished standing-capsule footprint waits at the physical body; it must not recover by jumping back to the initial safe position.
        if (!clear && recoveryAdmissionRejected) return false;
        if (!clear) clear = TryGroundedPosition(safePosition, out candidate);
        if (!clear) return false;
        StopPhysics();
        transform.position = candidate;
        // note: Rebinding restores the canonical skeletal pose; the existing controller supplies get-up/roll recovery.
        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);
        RestoreCapsuleOwnership();
        motor.CompleteRagdollRecovery(clutch);
        return true;
    }

    public void ObserveContact(Collision contact)
    {
        if (!IsSimulating || IsOwnedTransform(contact.transform) || YQPhysicalChunkAdmissionBoundary.IsOwned(contact.collider)) return;
        for (int i = 0; i < contact.contactCount; i++)
        {
            if (contact.GetContact(i).normal.y < .5f) continue;
            lastSupportCollider = contact.collider; lastSupportPoint = contact.GetContact(i).point; supportContactCount++;
            // note: Child-body collisions report physical landings through the same vitals/clutch path exactly once per damaging contact.
            motor.ObserveRagdollLanding(Mathf.Max(0f, fallApex - transform.position.y), Mathf.Max(downwardSpeed, Mathf.Abs(contact.relativeVelocity.y)));
            fallApex = transform.position.y; downwardSpeed = 0f;
            return;
        }
    }

    private bool TryGroundedPosition(Vector3 requested, out Vector3 result)
    {
        recoveryAdmissionRejected = false;
        result = requested;
        int supports = Physics.RaycastNonAlloc(requested + Vector3.up * 2f, Vector3.down, supportHits, 5f, ~0, QueryTriggerInteraction.Ignore);
        if (supports == supportHits.Length) return false;
        RaycastHit support = default; float nearest = float.PositiveInfinity;
        for (int i = 0; i < supports; i++)
            if (supportHits[i].collider != null && !YQPhysicalChunkAdmissionBoundary.IsOwned(supportHits[i].collider) && !IsOwnedTransform(supportHits[i].transform) && supportHits[i].normal.y >= .5f && supportHits[i].distance < nearest)
            { support = supportHits[i]; nearest = support.distance; }
        if (float.IsPositiveInfinity(nearest)) return false;
        result.y = support.point.y + .04f - (capsule.center.y - capsule.height * .5f);
        float radius = capsule.radius;
        float height = Mathf.Max(capsule.height, radius * 2f);
        Vector3 center = result + capsule.center;
        var admission = YQPlayerFollowingSemanticChunkStreamer.Active;
        if (admission != null && admission.isActiveAndEnabled && !admission.TryValidatePlayerPhysicsBounds(
            new Bounds(center, new Vector3(radius * 2f, height, radius * 2f)), out _))
        { recoveryAdmissionRejected = true; return false; }
        int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * (height * .5f - radius),
            center - Vector3.up * (height * .5f - radius), radius - .02f, clearance, ~0, QueryTriggerInteraction.Ignore);
        if (count == clearance.Length) return false;
        for (int i = 0; i < count; i++) if (clearance[i] != null && !YQPhysicalChunkAdmissionBoundary.IsOwned(clearance[i]) && !IsOwnedTransform(clearance[i].transform) && clearance[i] != capsule) return false;
        var streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        return streamer == null || !streamer.isActiveAndEnabled || streamer.TryValidateCurrentTraversability(result, radius, capsule.skinWidth, out _);
    }

    public void StopPhysics()
    {
        if (admissionStreamer != null) admissionStreamer.ReleasePlayerPhysicsAdmission(this);
        admissionStreamer = null; admissionHeld = false;
        if (bodies == null) return;
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
            {
                if (!bodies[i].isKinematic) { bodies[i].linearVelocity = Vector3.zero; bodies[i].angularVelocity = Vector3.zero; }
                bodies[i].isKinematic = true;
            }
            if (shapes[i] != null) shapes[i].enabled = false;
        }
        RestoreSimulationRoot();
        IsSimulating = false;
    }

    private bool IsOwnedTransform(Transform candidate) => candidate != null &&
        (candidate.IsChildOf(transform) || simulationRootDetached && simulationRoot != null && candidate.IsChildOf(simulationRoot));

    internal YQInvestorPlayerMotor ResolveDetachedMotor(Transform candidate)
    {
        // note: A stale/foreign relay cannot bind an arbitrary collider to this player, and stopped simulations resolve through normal ancestry again.
        return IsSimulating && simulationRootDetached && capsuleHandoffOwned && motor != null && motor.IsAuthoritative &&
            simulationRoot != null && candidate != null && candidate.IsChildOf(simulationRoot) ? motor : null;
    }

    private void DetachSimulationRoot()
    {
        // note: Moving the player root or resetting its visual ancestors must not teleport active skeletal colliders out of their solver contacts.
        simulationParent = simulationRoot.parent;
        simulationSibling = simulationRoot.GetSiblingIndex();
        simulationLocalPosition = simulationRoot.localPosition;
        simulationLocalRotation = simulationRoot.localRotation;
        simulationLocalScale = simulationRoot.localScale;
        simulationRoot.SetParent(null, true);
        simulationRootDetached = true;
    }

    private void RestoreSimulationRoot()
    {
        if (!simulationRootDetached) return;
        simulationRootDetached = false;
        if (simulationRoot == null) return;
        if (simulationParent == null)
        {
            // note: A destroyed original owner cannot leave its transient runtime body as an orphan scene actor.
            Destroy(simulationRoot.gameObject);
            return;
        }
        simulationRoot.SetParent(simulationParent, false);
        simulationRoot.SetSiblingIndex(simulationSibling);
        simulationRoot.localPosition = simulationLocalPosition;
        simulationRoot.localRotation = simulationLocalRotation;
        simulationRoot.localScale = simulationLocalScale;
    }

    private void RestoreCapsuleOwnership()
    {
        // note: A failed rig admission or unrelated disable did not hand the capsule to physics and must not change its enabled state.
        if (!capsuleHandoffOwned) return;
        capsuleHandoffOwned = false;
        if (capsule != null) capsule.enabled = capsuleWasEnabled;
    }

    public void ResetForRespawn()
    {
        StopPhysics();
        // note: Shutdown can disable the canonical body before this callback; native animation cannot update an inactive object.
        if (animator != null)
        {
            animator.enabled = true;
            if (animator.gameObject.activeInHierarchy) { animator.Rebind(); animator.Update(0f); }
        }
        RestoreCapsuleOwnership();
        motor?.ResetMotionSample();
    }

    // note: Terrain recovery must retire all dynamic bones before moving the root; resetting only the walking motor leaves the old simulation able to drag it back underground.
    internal void ResetForTerrainRecovery()
    {
        if (IsSimulating) ResetForRespawn();
    }

    private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    internal static bool IsPhysicsStepSafe(Vector3 admitted, Vector3 root, Vector3 predicted, float maximumStep)
    {
        // note: Normal falls and explicitly accepted launch speeds fit their step budget; exponential coordinate growth and non-finite poses fail closed.
        return Finite(admitted) && Finite(root) && Finite(predicted) && maximumStep > 0f && !float.IsInfinity(maximumStep) &&
            (root - admitted).sqrMagnitude <= maximumStep * maximumStep && (predicted - admitted).sqrMagnitude <= maximumStep * maximumStep;
    }
    private void OnDisable() => ResetForRespawn();
    private void OnDestroy() => StopPhysics();
}

// note: Unity sends collision events to the body object; this relay keeps all gameplay decisions on the canonical motor.
public sealed class YQPlayerRagdollContact : MonoBehaviour
{
    public YQPlayerRagdoll owner;
    private void OnCollisionEnter(Collision contact) => owner?.ObserveContact(contact);
}
