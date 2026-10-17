// Assets/Assets/Scripts/Tutorial/YQInvestorPlayerMotor.cs
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(-100)]
public sealed partial class YQInvestorPlayerMotor : MonoBehaviour
{
    // note: Attribute authoritative motor work independently from streamer scheduling and Unity's aggregate behaviour sample.
    private static readonly Unity.Profiling.ProfilerMarker MotorUpdateMarker =
        new Unity.Profiling.ProfilerMarker("YQInvestorPlayerMotor.Update");
    public static YQInvestorPlayerMotor ActiveMotor { get; private set; }

    public static YQInvestorPlayerMotor ResolveFromTransform(Transform target)
    {
        // note: Ragdoll temporarily detaches the existing body; its guarded relay still identifies the same authoritative actor for collision/effect queries.
        if (target == null) return null;
        var motor = target.GetComponentInParent<YQInvestorPlayerMotor>();
        if (motor != null) return motor.IsAuthoritative ? motor : null;
        var relay = target.GetComponentInParent<YQPlayerRagdollContact>();
        return relay != null && relay.owner != null ? relay.owner.ResolveDetachedMotor(target) : null;
    }

    public static YQInvestorPlayerMotor ResolveFromCollider(Collider target) =>
        target != null ? ResolveFromTransform(target.transform) : null;

    public Transform cameraPivot;
    public Camera playerCamera;
    public ActionRecorder actionRecorder;
    public YQInvestorVitals vitals;

    [Header("Look")]
    public float sensitivityX = 0.11f;
    public float sensitivityY = 0.10f;
    public float pitchMin = -78f;
    public float pitchMax = 82f;

    [Header("Move")]
    // note: Natural walking/running pace matches the approved human gait; legacy saved speed stats retain their existing progression baseline.
    public float walkSpeed = 7.5f;
    public float sprintSpeed = 10f;
    private const float SavedMoveSpeedBaseline = 6.8f;
    public float acceleration = 55f;
    public float deceleration = 75f;
    public float airControl = 0.55f;
    public float jumpHeight = 1.65f;
    public float gravity = -30f;
    public float fallGravityMultiplier = 1.45f;
    public float lowJumpGravityMultiplier = 1.85f;
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.14f;

    [Header("Water traversal")]
    public float swimSpeed = 2.4f;
    public float fastSwimSpeed = 3.6f;
    public float swimVerticalSpeed = 1.8f;
    public float breathCapacitySeconds = 60f;
    public float breathRecoveryPerSecond = 12f;
    public float drowningDamagePerSecond = 8f;
    private float _breathRemaining;
    private float _drowningElapsed;
    private bool _isSwimming;
    private bool _headUnderwater;
    private float _waterSurfaceHeight;
    public bool IsSwimming => _isSwimming;
    public bool IsHeadUnderwater => _headUnderwater;
    public float BreathRemainingSeconds => _breathRemaining;
    public float BreathCapacitySeconds => Mathf.Max(1f, breathCapacitySeconds);
    public float WaterSurfaceHeight => _waterSurfaceHeight;

    [Header("Traversal Assist")]
    public float blockedStepAssistHeight = 0.56f;
    public float blockedStepProbePadding = 0.20f;

    [Header("Stamina")]
    public float minSprintStamina = 3f;
    public float sprintBaseCostPerSecond = 7f;
    public float sprintFatigueCostPerSecond = 9f;
    public float sprintFatigueCapSeconds = 2.4f;
    public float sprintFatigueRecoveryPerSecond = 1.4f;
    public float jumpStaminaCost = 8f;
    public float dashStaminaCost = 24f;
    public float climbStaminaPerSecond = 4.25f;

    [Header("Dash / Roll")]
    public float dashDistance = 5.1f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.65f;
    // note: Grounded crouch-dash uses the same accepted traversal/cost path with enough time and a low capsule for the authored roll.
    public float rollDistance = 3.2f;
    public float rollDuration = 0.6f;
    private bool _dashIsRoll;
    public float airDashSpeedMultiplier = 0.82f;

    [Header("Crouch")]
    public float crouchHeight = 1.12f;
    public float crouchSpeed = 3.2f;
    public float crouchTransitionSharpness = 12f;
    public Vector3 crouchCameraPivotLocalPosition = new Vector3(0f, 1.10f, 0.02f);

    [Header("Climb")]
    public bool enableClimb = true;
    public float climbSpeed = 3.8f;
    public float climbSideSpeed = 2.8f;
    public float climbProbeDistance = 0.75f;
    public float climbProbeHeight = 1.05f;
    public float climbProbeRadiusScale = 0.78f;
    public float climbMinSurfaceAngle = 50f;
    public float climbMaxSurfaceAngle = 125f;
    public float climbStickSpeed = 1.2f;
    public LayerMask climbMask = ~0;

    [Header("Camera")]
    public bool firstPerson = true;
    public Vector3 cameraPivotLocalPosition = new Vector3(0f, 1.64f, 0.04f);
    public Vector3 firstPersonCameraLocalOffset = new Vector3(0f, 0.03f, 0.03f);
    public float thirdPersonDistance = 3.6f;
    public Vector3 thirdPersonShoulderOffset = new Vector3(0.55f, 0.10f, 0f);
    public Vector3 thirdPersonLookAtOffset = new Vector3(0.18f, 0.10f, 0.35f);
    public float thirdPersonPositionSharpness = 9.5f;
    public float thirdPersonRotationSharpness = 13.5f;
    public float cameraCollisionRadius = 0.2f;
    public LayerMask cameraCollisionMask = ~0;
    public float sprintFovBonus = 4f;
    public float dashFovBonus = 7f;
    public float climbFovBonus = 2.5f;
    public float headBobAmplitude = 0.035f;
    public float headBobFrequency = 10f;
    public float sprintHeadBobMultiplier = 1.45f;
    public float cameraSwayDegrees = 1.15f;
    public float dashRollDegrees = 3.25f;
    public float climbRollDegrees = 1.5f;
    public float cameraFeedbackSharpness = 12f;

    private CharacterController _controller;
    private Vector3 _planarVelocity;
    private Vector3 _dashDirection;
    private Vector3 _lastRequestedMoveDisplacement;
    private Vector3 _lastActualMoveDisplacement;
    private Vector3 _cameraBobLocalOffset;
    private Vector3 _thirdPersonCameraVelocity;
    private Vector2 _moveInput;
    // note: Autowalk supplies forward intent to this motor only; it is transient and never changes saved movement speeds.
    private bool _autoWalk;
    public bool IsAutoWalking => _autoWalk;
    private readonly RaycastHit[] _cameraHits = new RaycastHit[12];
    private readonly RaycastHit[] _stepProbeHits = new RaycastHit[8];
    // note: Reuse overlap storage when testing whether the crouched player can safely stand.
    private readonly Collider[] _standingClearanceHits = new Collider[24];
    private float _verticalVelocity;
    private float _yaw;
    private float _pitch;
    private float _nextDashTime;
    private float _dashTimeRemaining;
    private float _lastGroundedTime;
    private float _lastJumpPressedTime = -999f;
    private float _sprintFatigueSeconds;
    private float _headBobPhase;
    private float _cameraRoll;
    private bool _isSprinting;
    private bool _isClimbing;
    private bool _isCrouching;
    // note: Preserve a dash-start event for one motor tick so long frames cannot hide a valid dash that completed before the next observer sample.
    private bool _dashStartedThisFrame;
    // note: Retain the previous dash-key state so a delivered key edge remains reliable when InputSystem wasPressedThisFrame is unavailable during a background/editor frame.
    private bool _wasCrouching;
    private bool _dashStartedGrounded;
    private bool _lastFirstPerson;
    private Collider _lastBlockingControllerCollider;
    private Vector3 _lastBlockingControllerContactPoint;
    private Vector3 _lastBlockingControllerContactNormal;
    private int _blockingControllerContactCount;
    private CollisionFlags _lastMoveCollisionFlags;
    private int _animationGroundContactFrame = -1;
    private bool _lastMoveRejectedByTraversalGate;
    // note: A configured streamed world remains required after its component is disabled or destroyed; absence is not movement permission.
    private YQPlayerFollowingSemanticChunkStreamer _streamedMovementOwner;
    private WorldState _streamedMovementWorld;

    private bool _generationMovementLocked;
    private bool _deactivatedDuplicate;
    private float _standingControllerHeight;
    private Vector3 _standingControllerCenter;
    public bool IsAuthoritative => !_deactivatedDuplicate && ActiveMotor == this;
    public bool IsCrouching => _isCrouching;
    // note: Animation reads the motor's grounded state, excluding the stale contact on a jump's launch frame.
    public bool IsGrounded => _controller != null && _controller.isGrounded && _verticalVelocity <= 0f;
    // note: A later step-assist Move can clear isGrounded. Preserve this frame's real support contact for presentation only.
    public bool HasAnimationGroundContact => IsGrounded ||
        (_animationGroundContactFrame == Time.frameCount && _verticalVelocity <= 0f && !_isClimbing);
    // note: Expose the production gate state so verification can distinguish an input-delivery failure from an intentional motor early return.
    public bool CanProcessMovementInput => isActiveAndEnabled && IsAuthoritative &&
        _controller != null && _controller.enabled && cameraPivot != null && playerCamera != null &&
        !YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked && !RuntimeModalUiBlocker.IsBlocked;
    public bool IsDashing => _dashTimeRemaining > 0f;
    public bool IsDodgeRolling => IsDashing && _dashIsRoll;
    public float DodgeAnimationDuration => _dashIsRoll ? rollDuration : dashDuration;
    // note: Verification and animation-adjacent observers can distinguish a dash that began and completed within one long frame from a rejected dash.
    public bool DashStartedThisFrame => _dashStartedThisFrame;
    public bool IsSprinting => _isSprinting;
    // note: Presentation observes accepted traversal state; it never integrates a second movement simulation.
    public bool IsClimbing => _isClimbing;
    public float VerticalVelocity => _verticalVelocity;
    public Vector2 MoveInput => _moveInput;
    // note: Streaming uses the motor's already-computed planar velocity so lookahead follows CharacterController motion without duplicating movement state.
    public Vector3 PlanarVelocity => _planarVelocity;
    // note: Expose the exact simulation step consumed by movement so frame witnesses do not pair displacement with a later coroutine sample.
    internal float LastMovementDeltaTime { get; private set; }
    // note: Verification distinguishes requested speed from collision-resolved movement and names the contact that stopped the authoritative capsule.
    internal Vector3 LastRequestedMoveDisplacement => _lastRequestedMoveDisplacement;
    internal Vector3 LastActualMoveDisplacement => _lastActualMoveDisplacement;
    internal CollisionFlags LastMoveCollisionFlags => _lastMoveCollisionFlags;
    internal bool LastMoveRejectedByTraversalGate => _lastMoveRejectedByTraversalGate;
    internal Collider LastBlockingControllerCollider => _lastBlockingControllerCollider;
    internal Vector3 LastBlockingControllerContactPoint => _lastBlockingControllerContactPoint;
    internal Vector3 LastBlockingControllerContactNormal => _lastBlockingControllerContactNormal;
    internal int BlockingControllerContactCount => _blockingControllerContactCount;

    internal void BindStreamedMovementAdmission(YQPlayerFollowingSemanticChunkStreamer owner, WorldState world)
    {
        // note: Only the existing streamer's normal world bind replaces this transient admission dependency.
        if (owner == null || world == null) return;
        _streamedMovementOwner = owner;
        _streamedMovementWorld = world;
    }

    internal bool TryResolveStreamedMovementAdmission(out YQPlayerFollowingSemanticChunkStreamer owner)
    {
        owner = _streamedMovementOwner;
        WorldState currentWorld = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        if (_streamedMovementWorld != null && currentWorld != null &&
            !ReferenceEquals(currentWorld, _streamedMovementWorld))
        {
            // note: A newly selected nonstreamed world has no terrain dependency. A replacement generated world must bind its own streamer first.
            if (currentWorld.generatedWorldPlan != null) return false;
            owner = null;
            return true;
        }
        if (_streamedMovementWorld == null)
        {
            // note: Standalone authored scenes have no required streamer; an attachment in progress still closes admission until configured.
            owner = YQPlayerFollowingSemanticChunkStreamer.Active;
            if (owner == null) return true;
        }
        return owner != null && owner.IsPlayerMovementAdmissionAvailable(this);
    }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        // note: Transient feedback and physical presentation remain subordinate to this motor and the existing vitals.
        _feedback = GetComponent<YQPlayerHaptics>() ?? gameObject.AddComponent<YQPlayerHaptics>();
        _ragdoll = GetComponent<YQPlayerRagdoll>() ?? gameObject.AddComponent<YQPlayerRagdoll>();
        _motionSamplePosition = transform.position;
        _breathRemaining = BreathCapacitySeconds;
        // note: Imported steps and shallow terrain seams must be traversable by the authoritative player instead of acting like waist-high collision walls.
        _controller.stepOffset = Mathf.Max(_controller.stepOffset, 0.52f);
        _controller.slopeLimit = Mathf.Max(_controller.slopeLimit, 58f);
        _controller.skinWidth = Mathf.Max(_controller.skinWidth, 0.08f);
        _controller.minMoveDistance = 0f;
        _standingControllerHeight = Mathf.Max(0.1f, _controller.height);
        _standingControllerCenter = _controller.center;
        if (!TryClaimAuthority())
            return;

        if (playerCamera == null)
            playerCamera = Camera.main;

        AlignCameraPivot(0f);
        _lastFirstPerson = firstPerson;
        _lastGroundedTime = Time.time;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {
        // note: Capture the already-configured authoritative controller after all Awake methods have completed, without retuning gameplay.
        if (IsAuthoritative)
            CaptureCanonicalPlayerContract();
    }

    private void CaptureCanonicalPlayerContract()
    {
        // note: Persist the physical envelope and supported movement speeds as testable canonical state, not as a second player authority.
        PlayerStateManager manager = PlayerStateManager.Instance;
        if (manager == null || manager.state == null || !YQPlayerContractCapture.TryCapture(gameObject, walkSpeed, sprintSpeed, out YQPlayerCollisionContract contract))
            return;
        manager.state.playerCollisionContract = contract;
    }

    // note: Preserve Unity's Update callback while measuring the authoritative motor's complete per-frame cost.
    private void Update()
    {
        if (!IsAuthoritative)
            return;
        using (MotorUpdateMarker.Auto())
            UpdateAuthoritativeMotorFrame();
    }

    private void UpdateAuthoritativeMotorFrame()
    {
        if (!IsAuthoritative)
            return;

        if (cameraPivot == null ||
            playerCamera == null)
        {
            return;
        }

        float dt =
            Time.deltaTime;
        _dashStartedThisFrame = false;

        AlignCameraPivot(dt);

        /*
         * InitialWorldGeneration owns gameplay until the complete
         * generated world and canonical population are ready.
         *
         * This is the authoritative player motor, so locomotion must be
         * stopped HERE rather than in the legacy Rigidbody controller.
         */
        if (YQGeneratedWorldRuntimeBuilder
            .IsInitialGenerationGameplayLocked)
        {
            if (!_generationMovementLocked)
            {
                _generationMovementLocked =
                    true;

                Debug.Log(
                    "[YQInvestorPlayerMotor] " +
                    "Initial-generation movement lock APPLIED.");
            }

            // note: Initial generation may suspend locomotion, but it must never suspend the player's look input or camera presentation.
            HandleLook(dt);

            // note: Keep the gameplay camera following the live look pivot while generation owns only movement readiness; generation must never freeze camera presentation.
            HandleCamera(Time.unscaledDeltaTime);

            ClearInitialGenerationLocomotion(
                dt);

            return;
        }

        if (_generationMovementLocked)
        {
            _generationMovementLocked =
                false;

            Debug.Log(
                "[YQInvestorPlayerMotor] " +
                "Initial-generation movement lock RELEASED.");
        }

        if (RuntimeModalUiBlocker.IsBlocked)
        {
            // note: The generation overlay is not allowed to capture the camera; only ordinary menus and conversations suppress look input.
            if (YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked ||
                YQStartupLoadingScreen.IsGenerationVisible)
                HandleLook(dt);
            // note: Modal pause sets game delta to zero; presentation must still settle its FOV and pose before startup qualifies the first playable view.
            HandleCamera(Time.unscaledDeltaTime);

            // note: A menu or conversation discards buffered movement and dodge momentum instead of resuming an old dash when it closes.
            ClearInitialGenerationLocomotion(dt);
            return;
        }

        HandleLook(dt);

        // note: Ragdoll retains canonical player/camera identity and permits the post-impact clutch input.
        AdvancePendingLanding();
        if (_ragdoll != null && _ragdoll.IsSimulating)
        {
            ReadClutchInput();
            TryResolveClutch();
            HandleCamera(dt);
            return;
        }
        if (Time.time < _getUpUntil) { HandleCamera(dt); return; }

        // note: Pair displacement evidence with this motor Update's own time step.
        LastMovementDeltaTime = dt;
        BeginMotionFrame(dt);
        HandleMove(dt);
        EndMotionFrame(dt);

        AlignCameraPivot(dt);

        HandleCamera(dt);
    }

    private void ClearInitialGenerationLocomotion(
    float dt)
    {
        /*
         * Discard every locomotion state that could continue movement when
         * generation begins or cause buffered movement when it ends.
         */
        _moveInput =
            Vector2.zero;

        // note: Generation, menus and dialogue must cancel autowalk rather than resume it after closing.
        _autoWalk = false;
        // note: Pause/loading invalidates motion samples without erasing an unresolved landing transaction.
        if (Time.timeScale <= 0f && !_fallVelocityPaused)
        { _pausedFallVelocity = _verticalVelocity; _fallVelocityPaused = true; }
        ResetMotionSample(false);
        _isPowerSliding = false;
        StopSlideAudio();

        _planarVelocity =
            Vector3.zero;


        _verticalVelocity =
            0f;

        _dashDirection =
            Vector3.zero;

        _dashTimeRemaining =
            0f;

        _isSprinting =
            false;



        _isClimbing =
            false;

        _isCrouching =
            false;

        _lastJumpPressedTime =
            -999f;

        /*
         * Keep grounded/coyote state sane for the first gameplay frame
         * after generation finishes.
         */
        if (_controller != null &&
            _controller.isGrounded)
        {
            _lastGroundedTime =
                Time.time;
        }

        UpdateCrouchState(
            false,
            _controller != null &&
            _controller.isGrounded,
            dt);

        vitals?.SetSprinting(
            false);

        /*
         * CharacterController.Move() is deliberately NOT called.
         * Therefore the authoritative player cannot translate while the
         * generation lock is held.
         *
         * Camera presentation may continue to settle normally.
         */
        // note: Locked movement keeps presentation on the real clock so unpausing cannot expand a previously frozen camera beyond the qualified view.
        AlignCameraPivot(dt);
        HandleCamera(Time.unscaledDeltaTime);
    }
    private void HandleLook(float dt)
    {
        // note: Alt UI releases only camera/pointer input; the same motor still simulates locomotion and gravity.
        if (YourQuestTutorialMenuUI.CapturesPointerInput)
            return;
        Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        Vector2 stick = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;
        if (delta.sqrMagnitude <= 0.000001f && stick.sqrMagnitude <= .0001f)
            return;

        _yaw += delta.x * sensitivityX + stick.x * 150f * dt;
        _pitch -= delta.y * sensitivityY + stick.y * 120f * dt;
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
        // note: Looking during ragdoll changes only the upright camera; rotating the player root would drag simulated bones around it.
        if (IsRagdoll) { cameraPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f); return; }
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

        // note: Let HandleCamera build the candidate camera from this pose before validation; validating here would submit the previous camera frustum and fail to admit the view the player actually requested.
    }

    private void HandleMove(float dt)
    {
        Keyboard kb = Keyboard.current;
        Vector2 move = Vector2.zero;
        bool sprintHeld = false;
        bool jumpPressed = false;
        bool jumpHeld = false;
        bool dashPressed = false;
        bool crouchHeld = false;

        if (kb != null)
        {
            if (kb.wKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed) move.y -= 1f;
            if (kb.dKey.isPressed) move.x += 1f;
            if (kb.aKey.isPressed) move.x -= 1f;
            sprintHeld = kb.leftShiftKey.isPressed;
            jumpPressed = kb.spaceKey.wasPressedThisFrame;
            jumpHeld = kb.spaceKey.isPressed;
            // note: Dash follows the production Input System press edge; verification must deliver real device events rather than add a second input contract.
            dashPressed = kb.qKey.wasPressedThisFrame;
            crouchHeld = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (kb.tKey.wasPressedThisFrame)
                ToggleCameraMode();

            // note: F8 toggles ordinary forward walking. Explicit locomotion or Escape takes control back immediately.
            if (kb.f8Key.wasPressedThisFrame)
                _autoWalk = !_autoWalk;
            if (move.sqrMagnitude > 0f || jumpPressed || dashPressed || crouchHeld || kb.escapeKey.wasPressedThisFrame)
                _autoWalk = false;
        }

        if (_autoWalk)
            move = Vector2.up;

        // note: Controller controls feed the same accepted actions and collision owner as keyboard input.
        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 stick = pad.leftStick.ReadValue();
            if (stick.sqrMagnitude > .01f) { move = stick; _autoWalk = false; }
            sprintHeld |= pad.leftStickButton.isPressed;
            jumpPressed |= pad.buttonSouth.wasPressedThisFrame;
            jumpHeld |= pad.buttonSouth.isPressed;
            dashPressed |= pad.buttonEast.wasPressedThisFrame;
            crouchHeld |= pad.rightStickButton.isPressed;
        }
        if (dashPressed && crouchHeld) _lastClutchPress = Time.time;
        TryResolveClutch();

        move = Vector2.ClampMagnitude(move, 1f);
        _moveInput = move;
        Vector3 wish = transform.forward * move.y + transform.right * move.x;
        // note: Deep-water movement remains in this single motor and uses the same streamed capsule admission/collision path.
        RefreshWaterContact();
        if (_isSwimming)
        {
            HandleSwimming(dt, move, sprintHeld, jumpHeld, crouchHeld);
            UpdateBreathing(dt);
            return;
        }
        UpdateBreathing(dt);
        // note: CharacterController grounding describes the previous Move; takeoff must not refresh coyote time while vertical velocity is still upward.
        bool grounded = _controller.isGrounded && _verticalVelocity <= 0f;
        if (grounded)
            _lastGroundedTime = Time.time;
        UpdateCrouchState(crouchHeld, grounded, dt);
        if (jumpPressed)
            _lastJumpPressedTime = Time.time;

        if (dashPressed && !(crouchHeld && !grounded) && _dashTimeRemaining <= 0f)
            TryStartDash(wish, grounded);

        if (_dashTimeRemaining > 0f)
        {
            _isCrouching = _dashIsRoll;
            UpdateCrouchController(dt);
            ApplyDash(dt);
            _isSprinting = false;
            _isClimbing = false;
            vitals?.SetSprinting(false);
            return;
        }

        if (TryClimb(dt, move, wish, jumpHeld))
        {
            _isSprinting = false;
            vitals?.SetSprinting(false);
            RecoverSprintFatigue(dt);
            return;
        }

        _isClimbing = false;
        // note: Equipment speed queries allocate internally; sample them briefly without caching accepted player/stat authority.
        float itemMoveBonus = ResolveEquipmentMoveBonus();
        float statMoveBonus = PlayerStateManager.Instance != null
            ? Mathf.Max(0f, PlayerStateManager.Instance.state.stats.moveSpeed - SavedMoveSpeedBaseline)
            : 0f;

        bool wantsSprint = !_isCrouching && sprintHeld && move.sqrMagnitude > 0.01f && move.y > 0.1f && HasStamina(minSprintStamina);
        _isSprinting = wantsSprint && TrySpendSprint(dt);
        if (!_isSprinting)
            RecoverSprintFatigue(dt);

        float baseSpeed = _isCrouching ? crouchSpeed : (_isSprinting ? sprintSpeed : walkSpeed);
        float targetSpeed = baseSpeed + itemMoveBonus + statMoveBonus;
        targetSpeed *= _movementCombat != null ? _movementCombat.MovementScale : 1f;
        Vector3 targetPlanar = wish * targetSpeed;
        float accel = grounded ? (move.sqrMagnitude > 0.01f ? acceleration : deceleration) : acceleration * airControl;
        bool jumped = TryConsumeBufferedJump(grounded);
        // note: Keep an accepted jump impulse, but commit time integration only when this frame actually submits controller movement.
        CaptureMotionIntegrationState();
        // note: High-speed turns brake world momentum before steering; ordinary stops stay crisp.
        AdvancePlanarVelocity(targetPlanar, accel, grounded, dt);
        if (!jumped)
            ApplyGravity(dt, grounded, jumpHeld);

        Vector3 requestedPlanarMotion = _planarVelocity * dt;
        Vector3 movementStart = transform.position;
        // note: Route normal movement through the same capsule gate used by dash, climb, and step assistance.
        CollisionFlags movementFlags = MoveWithTraversalGate(
            requestedPlanarMotion + Vector3.up * (_verticalVelocity * dt));
        // note: Stop upward integration on ceiling contact so low ceilings do not leave the player pressing upward for the rest of a jump.
        if ((movementFlags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            _verticalVelocity = 0f;


        if (grounded && !jumped &&
            (movementFlags & CollisionFlags.Sides) != 0)
        {
            Vector3 actualPlanarMotion = transform.position - movementStart;
            actualPlanarMotion.y = 0f;
            Vector3 requestedDirection = requestedPlanarMotion.sqrMagnitude > 0f
                ? requestedPlanarMotion.normalized
                : Vector3.zero;
            float completedDistance = Mathf.Max(
                0f,
                Vector3.Dot(actualPlanarMotion, requestedDirection));
            float requestedDistance = requestedPlanarMotion.magnitude;

            if (completedDistance < requestedDistance * 0.55f)
            {
                TryAssistBlockedStep(
                    requestedDirection *
                    Mathf.Max(0f, requestedDistance - completedDistance));
            }
        }

        if (actionRecorder != null && move.sqrMagnitude > 0.01f)
            actionRecorder.RecordMove();

        vitals?.SetSprinting(_isSprinting);
    }

    private void RefreshWaterContact()
    {
        Vector3 feet = transform.TransformPoint(_standingControllerCenter - Vector3.up * (_standingControllerHeight * .5f));
        bool found = YQWaterSurfaceMotion.TryGetWaterHeight(feet, out _waterSurfaceHeight);
        // note: Bridges and shallow wading retain grounded locomotion; the capsule must be substantially immersed before buoyancy takes over.
        _isSwimming = found && _waterSurfaceHeight - feet.y > (_isSwimming ? .65f : .95f);
        Vector3 mouth = transform.TransformPoint(_controller.center + Vector3.up * (_controller.height * .4f));
        _headUnderwater = found && mouth.y < _waterSurfaceHeight - .08f;
    }

    private void HandleSwimming(float dt, Vector2 move, bool fastHeld, bool riseHeld, bool diveHeld)
    {
        _isPowerSliding = false;
        StopSlideAudio();
        _isCrouching = false;
        _isClimbing = false;
        _dashTimeRemaining = 0f;
        _lastJumpPressedTime = -999f;
        UpdateCrouchController(dt);
        _isSprinting = fastHeld && move.sqrMagnitude > .01f && HasStamina(minSprintStamina) && (vitals == null || vitals.SpendStamina(4f * dt));
        vitals?.SetSprinting(_isSprinting);
        Vector3 forward = transform.forward;
        // note: Looking down while submerged directs the accepted swim stroke; Space surfaces and Control dives in either camera mode.
        if (_headUnderwater && cameraPivot != null) forward = cameraPivot.forward;
        Vector3 wish = Vector3.ClampMagnitude(forward * move.y + transform.right * move.x, 1f);
        float speed = _isSprinting ? fastSwimSpeed : swimSpeed;
        Vector3 target = wish * speed;
        target.y = 0f;
        CaptureMotionIntegrationState();
        _planarVelocity = Vector3.MoveTowards(_planarVelocity, target, 6f * dt);
        float desiredVertical = wish.y * speed + (riseHeld ? swimVerticalSpeed : 0f) - (diveHeld ? swimVerticalSpeed : 0f);
        if (!riseHeld && !diveHeld && Mathf.Abs(wish.y) < .05f)
        {
            Vector3 feet = transform.TransformPoint(_standingControllerCenter - Vector3.up * (_standingControllerHeight * .5f));
            float floatFeetHeight = _waterSurfaceHeight - _standingControllerHeight * .76f;
            desiredVertical = Mathf.Clamp((floatFeetHeight - feet.y) * 2f, -.7f, .85f);
        }
        _verticalVelocity = Mathf.MoveTowards(_verticalVelocity, desiredVertical, 5f * dt);
        // note: Ascending surfaces the player's mouth without launching a walk/fall/swim cycle; shallow-bank exit still follows actual capsule contact and immersion.
        Vector3 currentFeet = transform.TransformPoint(_standingControllerCenter - Vector3.up * (_standingControllerHeight * .5f));
        float maximumSurfaceFeet = _waterSurfaceHeight - _standingControllerHeight * .65f;
        if (_verticalVelocity > 0f)
            _verticalVelocity = Mathf.Min(_verticalVelocity, Mathf.Max(0f, maximumSurfaceFeet - currentFeet.y) / Mathf.Max(.001f, dt));
        CollisionFlags flags = MoveWithTraversalGate((_planarVelocity + Vector3.up * _verticalVelocity) * dt);
        if ((flags & (CollisionFlags.Above | CollisionFlags.Below)) != 0) _verticalVelocity = 0f;
        if (move.sqrMagnitude > .01f) actionRecorder?.RecordMove();
        RefreshWaterContact();
    }

    private void UpdateBreathing(float dt)
    {
        // note: Breath is transient traversal state; drowning calls the existing vitals/death owner without a second health or save document.
        if (vitals != null && vitals.IsDead) return;
        if (_headUnderwater)
        {
            float exhaustedTime = Mathf.Max(0f, dt - _breathRemaining);
            _breathRemaining = Mathf.Max(0f, _breathRemaining - dt);
            _drowningElapsed += exhaustedTime;
            if (_drowningElapsed >= 1f)
            {
                float ticks = Mathf.Floor(_drowningElapsed);
                _drowningElapsed -= ticks;
                vitals?.TakeDamage(Mathf.Max(0f, drowningDamagePerSecond) * ticks);
            }
        }
        else
        {
            _breathRemaining = Mathf.Min(BreathCapacitySeconds, _breathRemaining + Mathf.Max(0f, breathRecoveryPerSecond) * dt);
            _drowningElapsed = 0f;
        }
    }

    private void TryAssistBlockedStep(Vector3 requestedPlanarMotion)
    {
        requestedPlanarMotion.y = 0f;
        float requestedDistance = requestedPlanarMotion.magnitude;
        if (requestedDistance <= 0.001f || _controller == null)
            return;

        Vector3 direction = requestedPlanarMotion / requestedDistance;
        float assistHeight = Mathf.Clamp(
            Mathf.Max(_controller.stepOffset, blockedStepAssistHeight),
            0.30f,
            Mathf.Max(0.30f, _controller.height * 0.45f));
        float probeDistance = _controller.radius + requestedDistance +
            Mathf.Max(0.05f, blockedStepProbePadding);
        Vector3 upperProbeOrigin = transform.position +
            Vector3.up * (assistHeight + _controller.skinWidth + 0.08f);

        if (HasExternalStepProbeHit(
                upperProbeOrigin,
                direction,
                probeDistance))
        {
            return;
        }

        float startY = transform.position.y;
        MoveWithTraversalGate(Vector3.up * assistHeight);
        float actualRise = transform.position.y - startY;
        if (actualRise < assistHeight * 0.72f)
        {
            // note: A ceiling or overhang rejected the lift; settle back immediately and preserve the original blocking collision.
            MoveWithTraversalGate(Vector3.down * Mathf.Max(0f, actualRise));
            return;
        }

        // note: This fallback runs only after CharacterController reports a grounded side collision and the space above the obstacle is clear, allowing irregular imported stair risers without climbing walls.
        MoveWithTraversalGate(requestedPlanarMotion);
        MoveWithTraversalGate(Vector3.down * (actualRise + 0.10f));
    }

    private CollisionFlags MoveWithTraversalGate(Vector3 displacement)
    {
        if (_controller == null || !_controller.enabled) return CollisionFlags.None;
        bool admissionAvailable = TryResolveStreamedMovementAdmission(out YQPlayerFollowingSemanticChunkStreamer streamer);
        Vector3 movementStart = transform.position;
        Vector3 requestedDisplacement = displacement;
        _lastRequestedMoveDisplacement = requestedDisplacement;
        _lastActualMoveDisplacement = Vector3.zero;
        _lastMoveCollisionFlags = CollisionFlags.None;
        _lastMoveRejectedByTraversalGate = false;
        bool startedTraversable = admissionAvailable && streamer != null &&
            streamer.TryValidateCurrentTraversability(movementStart, _controller.radius, _controller.skinWidth, out _);
        if (!admissionAvailable || (streamer != null &&
            !streamer.TryConstrainMovement(
                transform.position,
                displacement,
                _controller.radius,
                _controller.skinWidth,
                out _)))
        {
            // note: Reject the entire controller step when its clearance sweep is blocked; CharacterController resolution can add sideways motion to both horizontal and vertical moves.
            _lastMoveRejectedByTraversalGate = true;
            _frameMovementHeld = true;
            return CollisionFlags.None;
        }

        // note: Only submitted physical movement contributes an impact sample; an admission hold is not a collision.
        _frameControllerMoveAdmitted = true;
        _frameImpactSpeed = Mathf.Max(_frameImpactSpeed, -_verticalVelocity);
        // note: Observe collision-resolved motion immediately, before another owner can change publication, without correcting or hiding the actual movement result.
        CollisionFlags flags = _controller.Move(displacement);
        if ((flags & CollisionFlags.Below) != 0) _animationGroundContactFrame = Time.frameCount;
        if ((flags & CollisionFlags.Below) != 0) _frameLanded = true;
        _lastMoveCollisionFlags = flags;
        _lastActualMoveDisplacement = transform.position - movementStart;
        if (startedTraversable && !streamer.TryValidateCurrentTraversability(
                transform.position, _controller.radius, _controller.skinWidth, out string failure))
        {
            Debug.LogError("[YQInvestorPlayerMotor] COLLISION LEFT CERTIFIED SWEEP start=" + movementStart.ToString("F4") +
                " requested=" + requestedDisplacement.ToString("F4") + " submitted=" + displacement.ToString("F4") +
                " actual=" + (transform.position - movementStart).ToString("F4") +
                " position=" + transform.position.ToString("F4") + " radius=" + _controller.radius +
                " skin=" + _controller.skinWidth + " step=" + _controller.stepOffset + " flags=" + flags + " failure=" + failure);
        }
        return flags;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit != null && hit.normal.y >= .45f)
            _supportVelocity = hit.rigidbody != null ? hit.rigidbody.GetPointVelocity(hit.point) : Vector3.zero;
        // note: Retain only side contacts opposing requested movement so runtime verification can identify physical blockers without allocating in the motor hot path.
        if (hit == null || hit.collider == null || hit.normal.y >= 0.65f ||
            Vector3.Dot(hit.moveDirection, hit.normal) >= -0.05f)
            return;

        _lastBlockingControllerCollider = hit.collider;
        _lastBlockingControllerContactPoint = hit.point;
        _lastBlockingControllerContactNormal = hit.normal;
        _blockingControllerContactCount++;
    }

    private bool HasExternalStepProbeHit(
        Vector3 origin,
        Vector3 direction,
        float distance)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            _stepProbeHits,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < hitCount; index++)
        {
            Collider hitCollider = _stepProbeHits[index].collider;
            if (hitCollider == null ||
                hitCollider.transform == transform ||
                hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool TrySpendSprint(float dt)
    {
        float nextFatigue = Mathf.Min(sprintFatigueCapSeconds, _sprintFatigueSeconds + dt);
        float drainPerSecond = sprintBaseCostPerSecond + nextFatigue * sprintFatigueCostPerSecond;
        if (!SpendStamina(drainPerSecond * dt))
            return false;

        _sprintFatigueSeconds = nextFatigue;
        return true;
    }

    private void RecoverSprintFatigue(float dt)
    {
        _sprintFatigueSeconds = Mathf.Max(0f, _sprintFatigueSeconds - sprintFatigueRecoveryPerSecond * dt);
    }

    private bool TryConsumeBufferedJump(bool grounded)
    {
        bool buffered = Time.time - _lastJumpPressedTime <= jumpBuffer;
        bool coyote = grounded || Time.time - _lastGroundedTime <= coyoteTime;
        if (!buffered || !coyote)
            return false;

        // note: A crouched jump cannot expand into a low ceiling or spend stamina while its standing shape is blocked.
        if (!HasStandingClearance())
            return false;

        _lastJumpPressedTime = -999f;
        if (!SpendStamina(jumpStaminaCost))
            return false;

        _isCrouching = false;
        _wasCrouching = false;
        UpdateCrouchController(0f);
        _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        // note: One grounded/coyote opportunity buys one jump; a second press in the grace window cannot buy an unintended air jump.
        _lastGroundedTime = float.NegativeInfinity;
        actionRecorder?.RecordJump();
        GetComponent<YQPlayerEquipmentVisual>()?.PlayJumpFeedback();
        return true;
    }

    private void ApplyGravity(float dt, bool grounded, bool jumpHeld)
    {
        if (grounded)
        {
            if (_verticalVelocity < -2f)
                _verticalVelocity = -2f;
            return;
        }

        float multiplier = _verticalVelocity < 0f
            ? fallGravityMultiplier
            : (jumpHeld ? 1f : lowJumpGravityMultiplier);
        _verticalVelocity += gravity * multiplier * dt;
    }

    private void TryStartDash(Vector3 wish, bool grounded)
    {
        if (Time.time < _nextDashTime)
            return;
        if (!SpendStamina(dashStaminaCost))
            return;

        Vector3 dir = wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;
        _dashDirection = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
        if (_dashDirection.sqrMagnitude < 0.01f)
            _dashDirection = transform.forward;

        _dashStartedGrounded = grounded;
        _dashIsRoll = grounded && _isCrouching;
        _dashTimeRemaining = Mathf.Max(0.05f, DodgeAnimationDuration);
        _dashStartedThisFrame = true;
        _nextDashTime = Time.time + dashCooldown;
        _isCrouching = _dashIsRoll;
        // note: Keep a roll low; ordinary dashes still use the existing checked standing shape.
        _wasCrouching = _isCrouching;
        UpdateCrouchController(0f);
        _planarVelocity = _dashDirection * sprintSpeed;
        actionRecorder?.RecordDodge();
        GetComponent<YQPlayerEquipmentVisual>()?.PlayRollFeedback();
    }

    private void UpdateCrouchState(bool crouchHeld, bool grounded, float dt)
    {
        bool targetCrouch = (_dashIsRoll && IsDashing) || (crouchHeld && grounded && _dashTimeRemaining <= 0f && !_isClimbing);
        _isCrouching = targetCrouch;
        // note: Resolve obstruction before publishing/recording the stance used by movement, camera, and animation.
        UpdateCrouchController(dt);
        if (_isCrouching && !_wasCrouching)
            actionRecorder?.RecordCrouch();
        _wasCrouching = _isCrouching;
    }

    private bool HasStandingClearance()
    {
        if (_controller == null)
            return false;
        float standingHeight = _standingControllerHeight > 0.1f ? _standingControllerHeight : 1.8f;
        if (_controller.height >= standingHeight - 0.001f)
            return true;

        // note: Sweep the upper sphere through the added headroom; unchanged feet/floor contacts must not prevent standing.
        Vector3 scale = transform.lossyScale;
        float radius = _controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(standingHeight * Mathf.Abs(scale.y), radius * 2f);
        float inset = Mathf.Min(0.02f, _controller.skinWidth * Mathf.Abs(scale.y));
        Vector3 center = transform.TransformPoint(_standingControllerCenter);
        Vector3 offset = transform.up * Mathf.Max(0f, height * 0.5f - radius);
        float currentHeight = Mathf.Max(_controller.height * Mathf.Abs(scale.y), radius * 2f);
        Vector3 currentTop = transform.TransformPoint(_controller.center) +
            transform.up * Mathf.Max(0f, currentHeight * 0.5f - radius);
        int hitCount = Physics.OverlapCapsuleNonAlloc(currentTop, center + offset,
            Mathf.Max(0.001f, radius - inset), _standingClearanceHits, Physics.AllLayers, QueryTriggerInteraction.Ignore);

        // note: A full buffer may have omitted a blocking collider; keep crouching instead of guessing it is clear.
        if (hitCount == _standingClearanceHits.Length)
            return false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider obstacle = _standingClearanceHits[i];
            if (obstacle == null || obstacle == _controller || obstacle.transform.IsChildOf(transform))
                continue;
            if (Physics.GetIgnoreLayerCollision(gameObject.layer, obstacle.gameObject.layer) ||
                Physics.GetIgnoreCollision(_controller, obstacle))
                continue;
            return false;
        }
        return true;
    }

    private void UpdateCrouchController(float dt)
    {
        if (_controller == null)
            return;

        float standingHeight = _standingControllerHeight > 0.1f ? _standingControllerHeight : 1.8f;
        Vector3 standingCenter = _standingControllerCenter;
        // note: All stand-up paths, including modal locks and dash continuation, share the same geometry gate.
        if (!_isCrouching && !HasStandingClearance())
            _isCrouching = true;
        float minimumHeight = Mathf.Min(standingHeight, Mathf.Max(0.72f, _controller.radius * 2f));
        float targetHeight = _isCrouching ? Mathf.Clamp(crouchHeight, minimumHeight, standingHeight) : standingHeight;
        Vector3 targetCenter = _isCrouching
            // note: Preserve the authored capsule bottom, even when its standing center is not half its height.
            ? standingCenter - Vector3.up * ((standingHeight - targetHeight) * 0.5f)
            : standingCenter;

        float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, crouchTransitionSharpness) * Mathf.Max(0f, dt));
        if (dt <= 0f)
            blend = 1f;
        float nextHeight = Mathf.Lerp(_controller.height, targetHeight, blend);
        Vector3 nextCenter = Vector3.Lerp(_controller.center, targetCenter, blend);
        // note: Avoid repeatedly rebuilding an unchanged controller shape after the stance has settled.
        if (Mathf.Abs(_controller.height - nextHeight) > 0.0001f ||
            (_controller.center - nextCenter).sqrMagnitude > 0.00000001f)
        {
            _controller.height = nextHeight;
            _controller.center = nextCenter;
        }
    }

    private void ApplyDash(float dt)
    {
        CaptureMotionIntegrationState();
        // note: The last frame spends only the remaining dash time, keeping travel distance stable across frame rates and occasional long frames.
        float dashStep = Mathf.Min(Mathf.Max(0f, dt), _dashTimeRemaining);
        _dashTimeRemaining = Mathf.Max(0f, _dashTimeRemaining - dashStep);
        float speed = (_dashIsRoll ? rollDistance : dashDistance) / Mathf.Max(0.05f, DodgeAnimationDuration);
        if (!_dashStartedGrounded)
            speed *= airDashSpeedMultiplier;

        if (!_controller.isGrounded)
            _verticalVelocity += gravity * 0.45f * dashStep;
        else if (_verticalVelocity < -1f)
            _verticalVelocity = -1f;

        CollisionFlags flags = MoveWithTraversalGate(
            (_dashDirection * speed + Vector3.up * _verticalVelocity) * dashStep);
        if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            _verticalVelocity = 0f;
    }

    private bool TryClimb(float dt, Vector2 move, Vector3 wish, bool jumpHeld)
    {
        if (!enableClimb || !jumpHeld || (!_isClimbing && move.y <= 0.1f))
            return false;
        if (!TryGetClimbSurface(transform.forward, out RaycastHit hit))
        {
            if (_isClimbing && move.y > .1f) return TryCompleteClimbLedge(dt);
            return false;
        }
        // note: Climbing must not unfold a crouched capsule into the underside of a ledge.
        if (!HasStandingClearance())
            return false;
        if (!SpendStamina(climbStaminaPerSecond * dt))
            return false;

        _isCrouching = false;
        _wasCrouching = false;
        UpdateCrouchController(0f);
        _isClimbing = true;
        _verticalVelocity = 0f;
        _planarVelocity = Vector3.zero;
        CaptureMotionIntegrationState();

        Vector3 side = Vector3.ProjectOnPlane(transform.right, hit.normal).normalized * (move.x * climbSideSpeed);
        // note: Once attached, neutral input holds and backward input descends while the same wall contact owns attachment.
        Vector3 climb = Vector3.up * climbSpeed * Mathf.Clamp(move.y, -1f, 1f);
        Vector3 stick = -hit.normal * climbStickSpeed;
        MoveWithTraversalGate((side + climb + stick) * dt);

        _lastGroundedTime = Time.time;
        actionRecorder?.RecordClimb();
        return true;
    }

    private bool TryGetClimbSurface(Vector3 wish, out RaycastHit hit)
    {
        Vector3 dir = wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;
        Vector3 origin = transform.position + Vector3.up * climbProbeHeight;
        float radius = _controller.radius * Mathf.Clamp(climbProbeRadiusScale, 0.2f, 1.2f);

        if (!Physics.SphereCast(origin, radius, dir, out hit, climbProbeDistance, climbMask, QueryTriggerInteraction.Ignore))
            return false;
        if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
            return false;

        float surfaceAngle = Vector3.Angle(hit.normal, Vector3.up);
        return surfaceAngle >= climbMinSurfaceAngle && surfaceAngle <= climbMaxSurfaceAngle;
    }


    private bool SpendStamina(float amount)
    {
        if (amount <= 0f)
            return true;
        return vitals == null || vitals.SpendStamina(amount);
    }

    private bool HasStamina(float amount)
    {
        return vitals == null || vitals.CurrentStamina >= amount;
    }

    private void HandleCamera(float dt)
    {
        bool modeChanged = _lastFirstPerson != firstPerson;
        if (modeChanged)
        {
            _thirdPersonCameraVelocity = Vector3.zero;
            _cameraBobLocalOffset = Vector3.zero;
            _cameraRoll = 0f;
            _lastFirstPerson = firstPerson;
        }

        float fovBonus = _dashTimeRemaining > 0f
            ? dashFovBonus
            : (_isClimbing ? climbFovBonus : (_isSprinting ? sprintFovBonus : 0f));

        if (firstPerson)
        {
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, 84f + fovBonus, dt * 10f);
            Quaternion feedbackRotation = cameraPivot.rotation * Quaternion.Euler(0f, 0f, UpdateCameraRoll(dt));
            Vector3 targetPosition = cameraPivot.TransformPoint(firstPersonCameraLocalOffset) + UpdateFirstPersonCameraOffset(dt);
            playerCamera.transform.SetPositionAndRotation(targetPosition, feedbackRotation);
            ValidateAndRememberCameraPose();
            return;
        }

        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, 72f + fovBonus, dt * 10f);
        Vector3 lookAt = GetThirdPersonLookAtPoint();
        Vector3 desired = ResolveThirdPersonCameraPosition(lookAt);
        float smoothTime = 1f / Mathf.Max(0.01f, thirdPersonPositionSharpness);
        playerCamera.transform.position = modeChanged
            ? desired
            : Vector3.SmoothDamp(playerCamera.transform.position, desired, ref _thirdPersonCameraVelocity, smoothTime, Mathf.Infinity, dt);

        Vector3 toLookAt = lookAt - playerCamera.transform.position;
        if (toLookAt.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toLookAt.normalized, Vector3.up);
            float rotationBlend = modeChanged ? 1f : 1f - Mathf.Exp(-Mathf.Max(0.01f, thirdPersonRotationSharpness) * dt);
            playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, targetRotation, rotationBlend);
        }
        ValidateAndRememberCameraPose();
    }

    private void ValidateAndRememberCameraPose()
    {
        if (!TryResolveStreamedMovementAdmission(out YQPlayerFollowingSemanticChunkStreamer streamer) || streamer == null)
            return;

        // note: Make the exact post-motion camera frustum receive same-authority ground before Unity renders; queued work remains responsible for full semantic presentation.
        if (!streamer.TryEnsureCurrentCameraGround(out _))
            streamer.RequestCameraViewAdmission();

        // note: This camera-frame probe must observe only; the follow-up request owns any changed-frustum admission.
        if (streamer.TryValidateCurrentVisualCoverage(out _))
        {
            return;
        }

        // note: Queue the candidate frustum while leaving the player's requested camera pose untouched; streaming must catch up to the view, never move the view back.
        streamer.RequestCameraViewAdmission();
    }

    public void ToggleCameraMode()
    {
        firstPerson = !firstPerson;
    }

    internal void FaceWorldDirection(Vector3 direction)
    {
        // note: Origin placement updates the motor's own look state so the first mouse movement cannot snap back to the bootstrap yaw.
        direction.y = 0f;
        if (direction.sqrMagnitude < .0001f) return;
        _yaw = Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles.y;
        _pitch = 0f;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
    }

    public static void ForceAuthority(YQInvestorPlayerMotor motor)
    {
        if (motor == null)
            return;

        if (ActiveMotor != null && ActiveMotor != motor)
            ActiveMotor.DeactivateDuplicatePlayer();

        ActiveMotor = motor;
        motor._deactivatedDuplicate = false;
        motor.enabled = true;
        if (!motor.gameObject.CompareTag("Player"))
            motor.gameObject.tag = "Player";
        DontDestroyOnLoad(motor.gameObject);
    }

    private bool TryClaimAuthority()
    {
        if (ActiveMotor == null || ActiveMotor == this)
        {
            ForceAuthority(this);
            return true;
        }

        if (IsBetterAuthorityCandidate(this, ActiveMotor))
        {
            ForceAuthority(this);
            return true;
        }

        DeactivateDuplicatePlayer();
        return false;
    }

    private static bool IsBetterAuthorityCandidate(YQInvestorPlayerMotor candidate, YQInvestorPlayerMotor current)
    {
        return ScoreAuthorityCandidate(candidate) > ScoreAuthorityCandidate(current);
    }

    private static int ScoreAuthorityCandidate(YQInvestorPlayerMotor motor)
    {
        if (motor == null)
            return int.MinValue;

        int score = 0;
        if (motor.enabled)
            score += 10;
        if (motor.gameObject.activeInHierarchy)
            score += 12;
        if (motor.gameObject.CompareTag("Player"))
            score += 18;
        if (string.Equals(motor.gameObject.name, "Player", System.StringComparison.OrdinalIgnoreCase))
            score += 12;
        if (motor.cameraPivot != null)
            score += 10;
        if (motor.playerCamera != null)
            score += 10;
        if (Camera.main != null && motor.playerCamera == Camera.main)
            score += 28;
        string name = motor.gameObject.name ?? string.Empty;
        if (name.IndexOf("duplicate", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("deprecated", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("test", System.StringComparison.OrdinalIgnoreCase) >= 0)
            score -= 50;
        return score;
    }

    private void DeactivateDuplicatePlayer()
    {
        _deactivatedDuplicate = true;
        if (ActiveMotor == this)
            ActiveMotor = null;

        try
        {
            if (gameObject.CompareTag("Player"))
                gameObject.tag = "Untagged";
        }
        catch { }

        YQInvestorCombat combat = GetComponent<YQInvestorCombat>();
        if (combat != null)
            combat.enabled = false;
        YQInvestorVitals duplicateVitals = GetComponent<YQInvestorVitals>();
        if (duplicateVitals != null)
            duplicateVitals.enabled = false;
        YQPlayerEquipmentVisual visual = GetComponent<YQPlayerEquipmentVisual>();
        if (visual != null)
            visual.enabled = false;

        enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (ActiveMotor == this)
            ActiveMotor = null;
    }

    // note: Re-enabling the motor or returning to the application must require a fresh autowalk toggle.
    private void OnDisable() { _autoWalk = false; StopSlideAudio(); }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        { _autoWalk = false; StopSlideAudio(); }
    }

    private void AlignCameraPivot(float dt)
    {
        if (cameraPivot == null || cameraPivot.parent != transform)
            return;

        Vector3 target = _isCrouching ? crouchCameraPivotLocalPosition : cameraPivotLocalPosition;
        if (dt <= 0f)
        {
            cameraPivot.localPosition = target;
            return;
        }

        float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, crouchTransitionSharpness) * dt);
        cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, target, blend);
    }

    private Vector3 GetThirdPersonLookAtPoint()
    {
        return cameraPivot.position +
               cameraPivot.right * thirdPersonLookAtOffset.x +
               Vector3.up * thirdPersonLookAtOffset.y +
               transform.forward * thirdPersonLookAtOffset.z;
    }

    private Vector3 ResolveThirdPersonCameraPosition(Vector3 lookAt)
    {
        Vector3 shoulder = cameraPivot.right * thirdPersonShoulderOffset.x +
                           Vector3.up * thirdPersonShoulderOffset.y +
                           transform.forward * thirdPersonShoulderOffset.z;
        Vector3 origin = cameraPivot.position + shoulder;
        Vector3 desired = origin - cameraPivot.forward * Mathf.Max(0.35f, thirdPersonDistance);
        Vector3 travel = desired - origin;
        float distance = travel.magnitude;
        if (distance <= 0.01f)
            return desired;

        Vector3 direction = travel / distance;
        int hitCount = Physics.SphereCastNonAlloc(origin, cameraCollisionRadius, direction, _cameraHits, distance, cameraCollisionMask, QueryTriggerInteraction.Ignore);
        float bestDistance = distance;
        for (int i = 0; i < hitCount && i < _cameraHits.Length; i++)
        {
            RaycastHit hit = _cameraHits[i];
            if (hit.collider == null)
                continue;
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;

            bestDistance = Mathf.Min(bestDistance, Mathf.Max(0.35f, hit.distance - 0.08f));
        }

        return origin + direction * bestDistance;
    }

    private Vector3 UpdateFirstPersonCameraOffset(float dt)
    {
        float planarSpeed = new Vector3(_planarVelocity.x, 0f, _planarVelocity.z).magnitude;
        bool movingOnGround = _controller.isGrounded && !_isClimbing && _dashTimeRemaining <= 0f && planarSpeed > 0.35f;
        float targetScale = 0f;

        if (movingOnGround)
        {
            float speed01 = Mathf.InverseLerp(walkSpeed * 0.45f, sprintSpeed, planarSpeed);
            targetScale = Mathf.Lerp(0.35f, 1f, speed01);
            if (_isSprinting)
                targetScale *= sprintHeadBobMultiplier;
            _headBobPhase += dt * headBobFrequency * Mathf.Lerp(0.75f, 1.35f, speed01);
        }

        Vector3 targetLocalOffset = Vector3.zero;
        if (targetScale > 0f)
        {
            float vertical = Mathf.Sin(_headBobPhase * 2f) * headBobAmplitude * targetScale;
            float horizontal = Mathf.Cos(_headBobPhase) * headBobAmplitude * 0.45f * targetScale;
            targetLocalOffset = new Vector3(horizontal, vertical, 0f);
        }

        float smoothing = 1f - Mathf.Exp(-cameraFeedbackSharpness * dt);
        _cameraBobLocalOffset = Vector3.Lerp(_cameraBobLocalOffset, targetLocalOffset, smoothing);
        return cameraPivot.rotation * _cameraBobLocalOffset * CameraFeedbackIntensity;
    }

    private float UpdateCameraRoll(float dt)
    {
        float targetRoll = -_moveInput.x * cameraSwayDegrees;
        if (_dashTimeRemaining > 0f)
        {
            float lateralDash = Vector3.Dot(_dashDirection, transform.right);
            targetRoll += -Mathf.Sign(lateralDash) * Mathf.Abs(lateralDash) * dashRollDegrees;
        }
        else if (_isClimbing)
        {
            targetRoll += Mathf.Sin(Time.time * 6f) * climbRollDegrees;
        }

        float smoothing = 1f - Mathf.Exp(-cameraFeedbackSharpness * dt);
        _cameraRoll = Mathf.Lerp(_cameraRoll, targetRoll, smoothing);
        return _cameraRoll * CameraFeedbackIntensity;
    }
}
