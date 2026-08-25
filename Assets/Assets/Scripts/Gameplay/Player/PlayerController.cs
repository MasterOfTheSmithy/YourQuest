using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(
    typeof(Rigidbody),
    typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    public enum ViewMode
    {
        ThirdPerson,
        FirstPerson
    }

    // ============================================================
    // MODE
    // ============================================================

    [Header("Mode")]
    public ViewMode viewMode =
        ViewMode.ThirdPerson;

    public Key toggleViewKey =
        Key.C;

    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    public Transform cameraPivot;

    public Camera playerCamera;

    public ActionRecorder actionRecorder;

    // ============================================================
    // LOOK
    // ============================================================

    [Header("Look")]
    public float sensitivityX =
        0.12f;

    public float sensitivityY =
        0.10f;

    public float lookSmoothing =
        14f;

    public float pitchMin =
        -75f;

    public float pitchMax =
        80f;

    // ============================================================
    // MOVEMENT
    // ============================================================

    [Header("Movement")]
    public float walkSpeed =
        6f;

    public float sprintSpeed =
        8.5f;

    public float acceleration =
        35f;

    public float deceleration =
        45f;

    public float airAcceleration =
        12f;

    public float airMaxSpeed =
        6.5f;

    public float rotationSharpness =
        16f;

    // ============================================================
    // JUMP
    // ============================================================

    [Header("Jump")]
    public float jumpVelocity =
        6.5f;

    public float coyoteTime =
        0.12f;

    public float jumpBuffer =
        0.12f;

    public float fallGravityMultiplier =
        2.2f;

    public float lowJumpMultiplier =
        1.6f;

    // ============================================================
    // GROUNDING
    // ============================================================

    [Header("Grounding")]
    public LayerMask groundMask =
        ~0;

    public float groundProbeDistance =
        0.25f;

    public float maxGroundSlope =
        55f;

    public float stickToGroundForce =
        10f;

    // ============================================================
    // THIRD PERSON CAMERA
    // ============================================================

    [Header("Third Person Camera")]
    public float thirdPersonDistance =
        3.5f;

    public float thirdPersonHeight =
        1.6f;

    public float shoulderOffset =
        0.45f;

    public float cameraSharpness =
        18f;

    public float cameraCollisionRadius =
        0.18f;

    public float cameraCollisionPadding =
        0.05f;

    private readonly RaycastHit[] cameraCollisionHits =
        new RaycastHit[16];

    // ============================================================
    // FOV
    // ============================================================

    [Header("FOV")]
    public float firstPersonFov =
        82f;

    public float thirdPersonFov =
        70f;

    public float fovSharpness =
        8f;

    // ============================================================
    // PLAYER LOCOMOTION AUDIO
    // ============================================================

    [Header("Footstep Audio Guard")]
    public float footstepMinimumSpeed =
        0.25f;

    public float footstepAudioGuardInterval =
        0.25f;

    // ============================================================
    // COMPONENTS
    // ============================================================

    private Rigidbody rb;

    private CapsuleCollider capsule;

    /*
     * Compatibility only.
     *
     * Some older YourQuest player/bootstrap paths may still have a
     * CharacterController attached to the authoritative Player.
     *
     * Rigidbody FreezeAll does NOT prevent CharacterController.Move().
     * Therefore it must remain disabled throughout initial generation.
     */
    private CharacterController
        legacyCharacterController;

    private bool
        legacyCharacterControllerWasEnabled;

    private AudioSource[]
        playerAudioSources;

    private float
        nextPlayerAudioSourceRefreshTime;

    // ============================================================
    // INPUT STATE
    // ============================================================

    private Vector2 moveInput;

    private bool sprintHeld;

    private bool jumpPressed;

    private bool jumpHeld;

    // ============================================================
    // LOOK STATE
    // ============================================================

    private float yaw;

    private float pitch;

    private Vector2 smoothLook;

    private Vector2 lookVel;

    // ============================================================
    // GROUND STATE
    // ============================================================

    private bool grounded;

    private Vector3 groundNormal =
        Vector3.up;

    private float lastGroundedTime;

    private float lastJumpPressedTime =
        -999f;

    // ============================================================
    // AUDIO STATE
    // ============================================================

    private float nextPlayerAudioGuardTime;

    // ============================================================
    // INITIAL GENERATION GAMEPLAY LOCK
    // ============================================================

    private bool
        generationGameplayLockApplied;

    private RigidbodyConstraints
        constraintsBeforeGenerationLock =
            RigidbodyConstraints.FreezeRotation;

    private bool
        gravityBeforeGenerationLock =
            true;

    /*
     * Diagnostic only.
     *
     * Normal PlayerController gameplay always requires a dynamic
     * Rigidbody regardless of whatever state existed before generation.
     */
    private bool
        kinematicBeforeGenerationLock;

    /*
     * Prevent repeated stale-physics repair warnings.
     */
    private bool
        reportedPostGenerationPhysicsRepair;

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        rb =
            GetComponent<Rigidbody>();

        capsule =
            GetComponent<CapsuleCollider>();

        legacyCharacterController =
            GetComponent<CharacterController>();

        playerAudioSources =
            GetComponentsInChildren<AudioSource>(
                true);

        /*
         * This controller is velocity-driven.
         *
         * Normal gameplay requires a dynamic Rigidbody.
         */
        rb.isKinematic =
            false;

        rb.useGravity =
            true;

        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        rb.constraints =
            RigidbodyConstraints.FreezeRotation;

        if (!playerCamera)
        {
            playerCamera =
                Camera.main;
        }

        yaw =
            transform.eulerAngles.y;

        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible =
            false;
    }

    private void OnEnable()
    {
        if (rb == null)
        {
            rb =
                GetComponent<Rigidbody>();
        }

        if (capsule == null)
        {
            capsule =
                GetComponent<CapsuleCollider>();
        }

        if (legacyCharacterController == null)
        {
            legacyCharacterController =
                GetComponent<CharacterController>();
        }

        /*
         * Do NOT repair normal gameplay physics while initial
         * generation is active.
         *
         * If this component was disabled/re-enabled by origin VFX or
         * another tutorial system, immediately reassert the generation
         * lock instead.
         */
        if (IsInitialGenerationActuallyActive())
        {
            if (!generationGameplayLockApplied)
            {
                ApplyGenerationGameplayLock();
            }

            MaintainGenerationGameplayLock();

            return;
        }

        if (rb != null)
        {
            RepairNormalGameplayPhysics(
                false);
        }
    }

    private void Update()
    {
        bool generationLocked =
            SynchronizeGenerationGameplayLock();

        /*
         * Audio guarding intentionally continues while gameplay is
         * locked so a walk/footstep loop cannot continue underneath the
         * Goddess generation presentation.
         */
        GuardPlayerLocomotionAudio();

        if (generationLocked ||
            RuntimeModalUiBlocker.IsBlocked)
        {
            // note: Modal UI and Goddess generation screens must not leave buffered player input behind.
            ClearBufferedGameplayInput();
            return;
        }

        /*
         * Recover from stale physics state left by any previous
         * generation/presentation component.
         */
        RepairNormalGameplayPhysics(
            true);

        ReadInput();

        UpdateLook(
            Time.deltaTime);

        CheckGrounded();

        if (jumpPressed)
        {
            lastJumpPressedTime =
                Time.time;
        }

        if (Keyboard.current != null &&
            Keyboard.current[
                toggleViewKey]
                .wasPressedThisFrame)
        {
            viewMode =
                viewMode ==
                ViewMode.FirstPerson
                    ? ViewMode.ThirdPerson
                    : ViewMode.FirstPerson;
        }

        if (actionRecorder)
        {
            if (moveInput.sqrMagnitude >
                0.01f)
            {
                actionRecorder.RecordMove();
            }

            if (jumpPressed)
            {
                actionRecorder.RecordJump();
            }
        }
    }

    private void FixedUpdate()
    {
        bool generationLocked =
            SynchronizeGenerationGameplayLock();

        if (generationLocked ||
            RuntimeModalUiBlocker.IsBlocked)
        {
            if (generationLocked)
            {
                /*
                 * SynchronizeGenerationGameplayLock() already invokes
                 * MaintainGenerationGameplayLock(), but enforce once more
                 * at the physics boundary in case another script changed
                 * Rigidbody state between Update and FixedUpdate.
                 */
                MaintainGenerationGameplayLock();
            }

            // note: Menus/loading screens freeze player simulation without changing saved physics state.
            ClearBufferedGameplayInput();

            return;
        }

        RepairNormalGameplayPhysics(
            true);

        ApplyMovement(
            Time.fixedDeltaTime);

        ApplyJump();

        ApplyExtraGravity();
    }

    private void LateUpdate()
    {
        UpdateCamera(
            Time.deltaTime);
    }

    // ============================================================
    // INITIAL GENERATION LOCK
    // ============================================================

    private static bool
        IsInitialGenerationActuallyActive()
    {
        /*
         * Authoritative lifecycle lock.
         *
         * Once YQOriginGenerationService explicitly calls
         * BeginInitialGenerationGameplayLock(), this remains true until
         * YQGeneratedWorldRuntimeBuilder reaches INITIAL GENERATION READY.
         */
        if (YQGeneratedWorldRuntimeBuilder
            .IsInitialGenerationGameplayLocked)
        {
            return true;
        }

        /*
         * Defensive fallback.
         *
         * This should normally be redundant after explicit lifecycle
         * acquisition is installed in YQOriginGenerationService, but it
         * prevents gameplay from escaping if the builder lock has not
         * yet been observed during the same frame in which
         * InitialWorldGeneration begins.
         */
        LLMClient llm =
            LLMClient.Instance;

        return
            llm != null &&
            llm.IsExclusiveSequenceActive &&
            string.Equals(
                llm.ExclusiveSequenceOwner,
                "InitialWorldGeneration",
                System.StringComparison.Ordinal);
    }

    private bool
        SynchronizeGenerationGameplayLock()
    {
        bool shouldBeLocked =
            IsInitialGenerationActuallyActive();

        if (shouldBeLocked)
        {
            if (!generationGameplayLockApplied)
            {
                ApplyGenerationGameplayLock();
            }

            /*
             * CRITICAL:
             *
             * The initial implementation only froze the player once.
             *
             * Origin landing VFX/tutorial systems can later modify
             * Rigidbody state or re-enable a legacy CharacterController.
             *
             * Therefore the lock must be continuously asserted for the
             * entire initial-generation lifecycle.
             */
            MaintainGenerationGameplayLock();

            ClearBufferedGameplayInput();

            return true;
        }

        if (generationGameplayLockApplied)
        {
            ReleaseGenerationGameplayLock();
        }

        return false;
    }

    private void
        ApplyGenerationGameplayLock()
    {
        if (generationGameplayLockApplied ||
            rb == null)
        {
            return;
        }

        /*
         * Capture gameplay physics state exactly once.
         */
        constraintsBeforeGenerationLock =
            rb.constraints;

        gravityBeforeGenerationLock =
            rb.useGravity;

        kinematicBeforeGenerationLock =
            rb.isKinematic;

        /*
         * Never preserve an already-stale FreezeAll as the normal
         * gameplay state to restore later.
         */
        if (constraintsBeforeGenerationLock ==
            RigidbodyConstraints.FreezeAll)
        {
            constraintsBeforeGenerationLock =
                RigidbodyConstraints.FreezeRotation;
        }

        if (legacyCharacterController != null)
        {
            legacyCharacterControllerWasEnabled =
                legacyCharacterController.enabled;

            legacyCharacterController.enabled =
                false;
        }

        generationGameplayLockApplied =
            true;

        reportedPostGenerationPhysicsRepair =
            false;

        ClearBufferedGameplayInput();

        MaintainGenerationGameplayLock();
        MonoBehaviour[] behaviours =
    GetComponentsInChildren<MonoBehaviour>(
        true);

        Debug.Log(
            "[PlayerController] COMPONENTS ON LOCKED PLAYER:\n" +
            string.Join(
                "\n",
                System.Array.ConvertAll(
                    behaviours,
                    behaviour =>
                        behaviour != null
                            ? behaviour.GetType().FullName +
                              " | enabled=" +
                              behaviour.enabled +
                              " | object=" +
                              behaviour.gameObject.name
                            : "<missing>")));

        Debug.Log(
            "[PlayerController] " +
            "Initial-generation gameplay lock APPLIED. " +
            "Previous constraints=" +
            constraintsBeforeGenerationLock +
            " | previous gravity=" +
            gravityBeforeGenerationLock +
            " | previous kinematic=" +
            kinematicBeforeGenerationLock +
            " | legacy CharacterController=" +
            (legacyCharacterController != null
                ? legacyCharacterControllerWasEnabled
                    ? "enabled -> disabled"
                    : "already disabled"
                : "not present"));
    }

    /*
     * This method is deliberately idempotent.
     *
     * It is called throughout the entire generation lifecycle, not just
     * once. Any external component attempting to restore movement while
     * the Goddess is still generating is immediately overridden.
     */
    private void
        MaintainGenerationGameplayLock()
    {
        if (!generationGameplayLockApplied ||
            rb == null)
        {
            return;
        }

        /*
         * CharacterController.Move() completely bypasses Rigidbody
         * FreezeAll. Keep any legacy controller disabled until the final
         * generated world is ready.
         */
        if (legacyCharacterController != null &&
            legacyCharacterController.enabled)
        {
            legacyCharacterController.enabled =
                false;
        }

        /*
         * Keep the body dynamic because Unity does not allow velocity
         * writes against a kinematic Rigidbody.
         */
        if (rb.isKinematic)
        {
            rb.isKinematic =
                false;
        }

        if (rb.useGravity)
        {
            rb.useGravity =
                false;
        }

        if (rb.constraints !=
            RigidbodyConstraints.FreezeAll)
        {
            rb.constraints =
                RigidbodyConstraints.FreezeAll;
        }

        rb.linearVelocity =
            Vector3.zero;

        rb.angularVelocity =
            Vector3.zero;

        rb.Sleep();
    }

    private void
        ReleaseGenerationGameplayLock()
    {
        if (!generationGameplayLockApplied)
        {
            return;
        }

        /*
         * PlayerController movement requires a dynamic Rigidbody.
         */
        if (rb != null)
        {
            rb.isKinematic =
                false;

            RigidbodyConstraints
                restoredConstraints =
                    constraintsBeforeGenerationLock;

            if (restoredConstraints ==
                RigidbodyConstraints.FreezeAll)
            {
                restoredConstraints =
                    RigidbodyConstraints.FreezeRotation;
            }

            rb.constraints =
                restoredConstraints;

            rb.useGravity =
                gravityBeforeGenerationLock;

            /*
             * This controller relies on gravity for normal
             * grounding/jumping. Never restore stale generation state
             * with gravity disabled.
             */
            if (!rb.useGravity)
            {
                rb.useGravity =
                    true;
            }

            rb.linearVelocity =
                Vector3.zero;

            rb.angularVelocity =
                Vector3.zero;

            rb.WakeUp();
        }

        /*
         * Restore the legacy CharacterController only if it was enabled
         * before PlayerController acquired the generation lock.
         */
        if (legacyCharacterController != null)
        {
            legacyCharacterController.enabled =
                legacyCharacterControllerWasEnabled;
        }

        // note: Generated world transforms were integrated across many loading frames; a full-scene SyncTransforms here rescanned every streamed collider and caused a final reveal hitch. The next normal physics step publishes the restored player body.

        generationGameplayLockApplied =
            false;

        legacyCharacterControllerWasEnabled =
            false;

        ClearBufferedGameplayInput();

        grounded =
            false;

        groundNormal =
            Vector3.up;

        lastGroundedTime =
            -999f;

        lastJumpPressedTime =
            -999f;

        Debug.Log(
            "[PlayerController] " +
            "Initial-generation gameplay lock RELEASED. " +
            "Constraints=" +
            (rb != null
                ? rb.constraints.ToString()
                : "<missing Rigidbody>") +
            " | gravity=" +
            (rb != null
                ? rb.useGravity.ToString()
                : "<missing>") +
            " | kinematic=" +
            (rb != null
                ? rb.isKinematic.ToString()
                : "<missing>") +
            " | legacy CharacterController=" +
            (legacyCharacterController != null
                ? legacyCharacterController.enabled.ToString()
                : "<not present>") +
            " | timeScale=" +
            Time.timeScale);
    }

    // ============================================================
    // POST-GENERATION PHYSICS SELF-REPAIR
    // ============================================================

    private void RepairNormalGameplayPhysics(
        bool logRepair)
    {
        if (rb == null)
            return;

        /*
         * Never repair toward normal gameplay while generation owns
         * the player.
         */
        if (IsInitialGenerationActuallyActive())
        {
            if (generationGameplayLockApplied)
            {
                MaintainGenerationGameplayLock();
            }

            return;
        }

        bool repaired =
            false;

        if (rb.isKinematic)
        {
            rb.isKinematic =
                false;

            repaired =
                true;
        }

        if (rb.constraints ==
            RigidbodyConstraints.FreezeAll)
        {
            rb.constraints =
                RigidbodyConstraints.FreezeRotation;

            repaired =
                true;
        }

        if (!rb.useGravity)
        {
            rb.useGravity =
                true;

            repaired =
                true;
        }

        if (repaired)
        {
            rb.WakeUp();

            // note: Repair only changes the authoritative Rigidbody; waking it is sufficient and avoids a second full-world transform synchronization after generation.

            if (logRepair &&
                !reportedPostGenerationPhysicsRepair)
            {
                reportedPostGenerationPhysicsRepair =
                    true;

                Debug.LogWarning(
                    "[PlayerController] " +
                    "Repaired stale post-generation Rigidbody state. " +
                    "Constraints=" +
                    rb.constraints +
                    " | gravity=" +
                    rb.useGravity +
                    " | kinematic=" +
                    rb.isKinematic +
                    " | timeScale=" +
                    Time.timeScale);
            }
        }
    }

    // ============================================================
    // PLAYER LOCOMOTION AUDIO GUARD
    // ============================================================

    private void GuardPlayerLocomotionAudio()
    {
        if (Time.unscaledTime <
            nextPlayerAudioGuardTime)
        {
            return;
        }

        nextPlayerAudioGuardTime =
            Time.unscaledTime +
            Mathf.Max(
                0.05f,
                footstepAudioGuardInterval);

        if (playerAudioSources == null ||
            Time.unscaledTime >=
            nextPlayerAudioSourceRefreshTime)
        {
            // note: The visible imported avatar is added after Awake, so refresh the guard's source list periodically.
            playerAudioSources =
                GetComponentsInChildren<AudioSource>(
                    true);

            nextPlayerAudioSourceRefreshTime =
                Time.unscaledTime +
                3f;
        }

        /*
         * During generation, locomotion audio is never valid.
         *
         * During gameplay, locomotion audio requires actual physical
         * planar movement.
         */
        bool shouldSuppress =
            IsInitialGenerationActuallyActive() ||
            PlanarSpeed <
                Mathf.Max(
                    0.05f,
                    footstepMinimumSpeed);

        if (shouldSuppress)
        {
            // note: Imported demo scripts can spawn loose PlayClipAtPoint sources outside the player hierarchy.
            YQImportedDemoAudioFirewall
                .SweepTemporaryLocomotionOneShots();
        }

        if (!shouldSuppress)
            return;

        for (int i = 0;
             i < playerAudioSources.Length;
             i++)
        {
            AudioSource source =
                playerAudioSources[i];

            if (source == null ||
                !source.isPlaying)
            {
                continue;
            }

            string sourceName =
                NormalizeAudioName(
                    source.name);

            string objectName =
                source.gameObject != null
                    ? NormalizeAudioName(
                        source.gameObject.name)
                    : string.Empty;

            string clipName =
                source.clip != null
                    ? NormalizeAudioName(
                        source.clip.name)
                    : string.Empty;

            /*
             * PlayOneShot does not necessarily populate AudioSource.clip.
             *
             * Therefore inspect the source/object identity as well as the
             * assigned clip. This catches dedicated Footstep/Walking audio
             * sources even when animation events use PlayOneShot().
             */
            bool locomotionSource =
                IsLocomotionAudioName(
                    sourceName) ||
                IsLocomotionAudioName(
                    objectName) ||
                IsLocomotionAudioName(
                    clipName) ||
                // note: Some imported avatar/creature prefabs attach idle vox/growl loops instead of footstep-named clips.
                YQImportedDemoAudioFirewall
                    .IsImportedDemoAmbientOrVoxAudioSource(
                        source);

            if (!locomotionSource)
                continue;

            source.Stop();
        }
    }

    private static bool IsLocomotionAudioName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        return
            value.Contains(
                "footstep") ||
            value.Contains(
                "foot_step") ||
            value.Contains(
                "footsteps") ||
            value.Contains(
                "foot_steps") ||
            value.Contains(
                "wetfoot") ||
            value.Contains(
                "wet_foot") ||
            value.Contains(
                "walking") ||
            value.Contains(
                "walk_loop") ||
            value.Contains(
                "walkloop") ||
            value.Contains(
                "running") ||
            value.Contains(
                "run_loop") ||
            value.Contains(
                "runloop");
    }

    private static string NormalizeAudioName(
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
                    '-',
                    '_')
                .Replace(
                    ' ',
                    '_')
                .ToLowerInvariant();
    }

    // ============================================================
    // BUFFERED INPUT
    // ============================================================

    private void ClearBufferedGameplayInput()
    {
        moveInput =
            Vector2.zero;

        sprintHeld =
            false;

        jumpPressed =
            false;

        jumpHeld =
            false;

        lastJumpPressedTime =
            -999f;

        smoothLook =
            Vector2.zero;

        lookVel =
            Vector2.zero;
    }

    // ============================================================
    // PUBLIC MOVEMENT STATE
    // ============================================================

    public float PlanarSpeed
    {
        get
        {
            if (rb == null)
                return 0f;

            Vector3 velocity =
                rb.linearVelocity;

            velocity.y =
                0f;

            return
                velocity.magnitude;
        }
    }

    public bool CanPlayFootstepAudio
    {
        get
        {
            if (rb == null)
                return false;

            if (generationGameplayLockApplied ||
                IsInitialGenerationActuallyActive())
            {
                return false;
            }

            if (!grounded)
                return false;

            return
                PlanarSpeed >=
                Mathf.Max(
                    0.05f,
                    footstepMinimumSpeed);
        }
    }

    // ============================================================
    // INPUT
    // ============================================================

    private void ReadInput()
    {
        moveInput =
            Vector2.zero;

        sprintHeld =
            false;

        jumpPressed =
            false;

        jumpHeld =
            false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
            {
                moveInput.y +=
                    1f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                moveInput.y -=
                    1f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                moveInput.x +=
                    1f;
            }

            if (Keyboard.current.aKey.isPressed)
            {
                moveInput.x -=
                    1f;
            }

            sprintHeld =
                Keyboard.current
                    .leftShiftKey
                    .isPressed;

            jumpPressed =
                Keyboard.current
                    .spaceKey
                    .wasPressedThisFrame;

            jumpHeld =
                Keyboard.current
                    .spaceKey
                    .isPressed;
        }

        moveInput =
            Vector2.ClampMagnitude(
                moveInput,
                1f);
    }

    // ============================================================
    // LOOK
    // ============================================================

    private void UpdateLook(
        float dt)
    {
        if (Mouse.current == null ||
            cameraPivot == null)
        {
            return;
        }

        Vector2 raw =
            Mouse.current
                .delta
                .ReadValue();

        Vector2 target =
            new Vector2(
                raw.x *
                sensitivityX,

                raw.y *
                sensitivityY);

        smoothLook =
            Vector2.SmoothDamp(
                smoothLook,
                target,
                ref lookVel,
                1f /
                Mathf.Max(
                    1f,
                    lookSmoothing),
                Mathf.Infinity,
                dt);

        yaw +=
            smoothLook.x;

        pitch -=
            smoothLook.y;

        pitch =
            Mathf.Clamp(
                pitch,
                pitchMin,
                pitchMax);

        cameraPivot.rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f);

        if (viewMode ==
            ViewMode.FirstPerson)
        {
            transform.rotation =
                Quaternion.Euler(
                    0f,
                    yaw,
                    0f);
        }
    }

    // ============================================================
    // GROUND
    // ============================================================

    private void CheckGrounded()
    {
        if (capsule == null ||
            rb == null)
        {
            grounded =
                false;

            return;
        }

        Vector3 origin =
            transform.position +
            Vector3.up *
            0.1f;

        grounded =
            false;

        groundNormal =
            Vector3.up;

        if (Physics.SphereCast(
                origin,
                capsule.radius *
                0.95f,
                Vector3.down,
                out RaycastHit hit,
                groundProbeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            float slope =
                Vector3.Angle(
                    hit.normal,
                    Vector3.up);

            if (slope <=
                maxGroundSlope)
            {
                grounded =
                    true;

                groundNormal =
                    hit.normal;

                lastGroundedTime =
                    Time.time;

                if (!rb.isKinematic &&
                    rb.linearVelocity.y <=
                    0f)
                {
                    rb.AddForce(
                        Vector3.down *
                        stickToGroundForce,
                        ForceMode.Acceleration);
                }
            }
        }
    }

    // ============================================================
    // MOVEMENT
    // ============================================================

    private void ApplyMovement(
        float dt)
    {
        if (cameraPivot == null ||
            rb == null ||
            rb.isKinematic ||
            IsInitialGenerationActuallyActive())
        {
            return;
        }

        Vector3 forward =
            cameraPivot.forward;

        Vector3 right =
            cameraPivot.right;

        forward.y =
            0f;

        right.y =
            0f;

        forward.Normalize();

        right.Normalize();

        Vector3 wishDir =
            forward *
            moveInput.y +
            right *
            moveInput.x;

        if (wishDir.sqrMagnitude >
            1f)
        {
            wishDir.Normalize();
        }

        float targetSpeed =
            sprintHeld
                ? sprintSpeed
                : walkSpeed;

        Vector3 velocity =
            rb.linearVelocity;

        Vector3 lateral =
            new Vector3(
                velocity.x,
                0f,
                velocity.z);

        float accelerationToUse =
            grounded
                ? acceleration
                : airAcceleration;

        float maximumSpeed =
            grounded
                ? targetSpeed
                : airMaxSpeed;

        Vector3 desired =
            wishDir *
            maximumSpeed;

        float rate =
            wishDir.sqrMagnitude >
            0.0001f
                ? accelerationToUse
                : grounded
                    ? deceleration
                    : airAcceleration;

        lateral =
            Vector3.MoveTowards(
                lateral,
                desired,
                rate *
                dt);

        rb.linearVelocity =
            new Vector3(
                lateral.x,
                velocity.y,
                lateral.z);

        if (viewMode ==
                ViewMode.ThirdPerson &&
            wishDir.sqrMagnitude >
                0.01f)
        {
            Quaternion rotation =
                Quaternion.LookRotation(
                    wishDir);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    rotation,
                    1f -
                    Mathf.Exp(
                        -rotationSharpness *
                        dt));
        }
    }

    private void ApplyJump()
    {
        if (rb == null ||
            rb.isKinematic ||
            IsInitialGenerationActuallyActive())
        {
            return;
        }

        bool canJump =
            (Time.time -
             lastGroundedTime) <=
                coyoteTime &&
            (Time.time -
             lastJumpPressedTime) <=
                jumpBuffer;

        if (!canJump)
            return;

        lastJumpPressedTime =
            -999f;

        rb.linearVelocity =
            new Vector3(
                rb.linearVelocity.x,
                jumpVelocity,
                rb.linearVelocity.z);
    }

    private void ApplyExtraGravity()
    {
        if (grounded ||
            rb == null ||
            rb.isKinematic ||
            IsInitialGenerationActuallyActive())
        {
            return;
        }

        if (rb.linearVelocity.y <
            0f)
        {
            rb.AddForce(
                Physics.gravity *
                (fallGravityMultiplier -
                 1f),
                ForceMode.Acceleration);
        }
        else if (!jumpHeld)
        {
            rb.AddForce(
                Physics.gravity *
                (lowJumpMultiplier -
                 1f),
                ForceMode.Acceleration);
        }
    }

    // ============================================================
    // CAMERA
    // ============================================================

    private void UpdateCamera(
        float dt)
    {
        if (!playerCamera ||
            !cameraPivot)
        {
            return;
        }

        float targetFov =
            viewMode ==
            ViewMode.FirstPerson
                ? firstPersonFov
                : thirdPersonFov;

        playerCamera.fieldOfView =
            Mathf.Lerp(
                playerCamera.fieldOfView,
                targetFov,
                1f -
                Mathf.Exp(
                    -fovSharpness *
                    dt));

        if (viewMode ==
            ViewMode.FirstPerson)
        {
            playerCamera.transform
                .SetPositionAndRotation(
                    cameraPivot.position,
                    cameraPivot.rotation);

            return;
        }

        Vector3 pivot =
            cameraPivot.position +
            Vector3.up *
            thirdPersonHeight;

        Vector3 shoulder =
            cameraPivot.right *
            shoulderOffset;

        Vector3 desired =
            pivot +
            shoulder -
            cameraPivot.forward *
            thirdPersonDistance;

        Vector3 from =
            pivot +
            shoulder;

        Vector3 direction =
            desired -
            from;

        float distance =
            direction.magnitude;

        if (distance >
            0.0001f)
        {
            direction /=
                distance;
        }

        Vector3 finalPosition =
            desired;

        if (distance >
                0.0001f &&
            TryResolveCameraObstruction(
                from,
                direction,
                distance,
                out RaycastHit hit))
        {
            finalPosition =
                from +
                direction *
                Mathf.Max(
                    0f,
                    hit.distance -
                    cameraCollisionPadding);
        }

        playerCamera.transform.position =
            Vector3.Lerp(
                playerCamera.transform.position,
                finalPosition,
                1f -
                Mathf.Exp(
                    -cameraSharpness *
                    dt));

        Vector3 lookDirection =
            pivot -
            playerCamera.transform.position;

        if (lookDirection.sqrMagnitude <
            0.000001f)
        {
            return;
        }

        Quaternion lookRotation =
            Quaternion.LookRotation(
                lookDirection.normalized);

        playerCamera.transform.rotation =
            Quaternion.Slerp(
                playerCamera.transform.rotation,
                lookRotation,
                1f -
                Mathf.Exp(
                    -cameraSharpness *
                    dt));
    }

    private bool TryResolveCameraObstruction(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit nearestHit)
    {
        nearestHit = default;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            cameraCollisionRadius,
            direction,
            cameraCollisionHits,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore);
        float nearestDistance = float.MaxValue;
        bool found = false;

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit candidate = cameraCollisionHits[index];
            Collider collider = candidate.collider;

            if (collider == null ||
                collider.transform.IsChildOf(transform))
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            float largestDimension = Mathf.Max(
                bounds.size.x,
                Mathf.Max(bounds.size.y, bounds.size.z));
            bool importedEnclosure = !(collider is TerrainCollider) &&
                (largestDimension > 160f ||
                 (largestDimension > 20f && bounds.Contains(origin)));

            if (importedEnclosure)
            {
                // note: Authored packs sometimes ship one collider around a whole mountain, district, or room shell; it remains physical for gameplay but cannot pin the camera to the player's head from inside its bounds.
                continue;
            }

            if (candidate.distance >= nearestDistance)
                continue;

            nearestDistance = candidate.distance;
            nearestHit = candidate;
            found = true;
        }

        return found;
    }
}
