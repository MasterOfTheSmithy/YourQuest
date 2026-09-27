// Assets/Assets/Scripts/Tutorial/YQInvestorPlayerMotor.cs
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(-100)]
public sealed class YQInvestorPlayerMotor : MonoBehaviour
{
    public static YQInvestorPlayerMotor ActiveMotor { get; private set; }

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
    public float walkSpeed = 6.8f;
    public float sprintSpeed = 11.25f;
    public float acceleration = 28f;
    public float deceleration = 34f;
    public float airControl = 0.55f;
    public float jumpHeight = 1.65f;
    public float gravity = -30f;
    public float fallGravityMultiplier = 1.45f;
    public float lowJumpGravityMultiplier = 1.85f;
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.14f;

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
    public float airDashSpeedMultiplier = 0.82f;

    [Header("Crouch")]
    public float crouchHeight = 1.12f;
    public float crouchSpeed = 3.7f;
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
    private bool _lastMoveRejectedByTraversalGate;

    private bool _generationMovementLocked;
    private bool _deactivatedDuplicate;
    private float _standingControllerHeight;
    private Vector3 _standingControllerCenter;
    public bool IsAuthoritative => !_deactivatedDuplicate && ActiveMotor == this;
    public bool IsCrouching => _isCrouching;
    // note: Animation reads the motor's grounded state, excluding the stale contact on a jump's launch frame.
    public bool IsGrounded => _controller != null && _controller.isGrounded && _verticalVelocity <= 0f;
    // note: Expose the production gate state so verification can distinguish an input-delivery failure from an intentional motor early return.
    public bool CanProcessMovementInput => isActiveAndEnabled && IsAuthoritative &&
        _controller != null && _controller.enabled && cameraPivot != null && playerCamera != null &&
        !YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked && !RuntimeModalUiBlocker.IsBlocked;
    public bool IsDashing => _dashTimeRemaining > 0f;
    // note: Verification and animation-adjacent observers can distinguish a dash that began and completed within one long frame from a rejected dash.
    public bool DashStartedThisFrame => _dashStartedThisFrame;
    public bool IsSprinting => _isSprinting;
    public Vector2 MoveInput => _moveInput;
    // note: Streaming uses the motor's already-computed planar velocity so lookahead follows CharacterController motion without duplicating movement state.
    public Vector3 PlanarVelocity => _planarVelocity;
    // note: Verification distinguishes requested speed from collision-resolved movement and names the contact that stopped the authoritative capsule.
    internal Vector3 LastRequestedMoveDisplacement => _lastRequestedMoveDisplacement;
    internal Vector3 LastActualMoveDisplacement => _lastActualMoveDisplacement;
    internal CollisionFlags LastMoveCollisionFlags => _lastMoveCollisionFlags;
    internal bool LastMoveRejectedByTraversalGate => _lastMoveRejectedByTraversalGate;
    internal Collider LastBlockingControllerCollider => _lastBlockingControllerCollider;
    internal Vector3 LastBlockingControllerContactPoint => _lastBlockingControllerContactPoint;
    internal Vector3 LastBlockingControllerContactNormal => _lastBlockingControllerContactNormal;
    internal int BlockingControllerContactCount => _blockingControllerContactCount;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
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

    private void Update()
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

        HandleMove(dt);

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
        if (Mouse.current == null)
            return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        if (delta.sqrMagnitude <= 0.000001f)
            return;

        _yaw += delta.x * sensitivityX;
        _pitch -= delta.y * sensitivityY;
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
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
        }


        move = Vector2.ClampMagnitude(move, 1f);
        _moveInput = move;
        Vector3 wish = transform.forward * move.y + transform.right * move.x;
        // note: CharacterController grounding describes the previous Move; takeoff must not refresh coyote time while vertical velocity is still upward.
        bool grounded = _controller.isGrounded && _verticalVelocity <= 0f;
        if (grounded)
            _lastGroundedTime = Time.time;
        UpdateCrouchState(crouchHeld, grounded, dt);
        if (jumpPressed)
            _lastJumpPressedTime = Time.time;

        if (dashPressed)
            TryStartDash(wish, grounded);

        if (_dashTimeRemaining > 0f)
        {
            _isCrouching = false;
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
        float itemMoveBonus = GeneratedRpgContentService.Instance != null && PlayerStateManager.Instance != null
            ? GeneratedRpgContentService.Instance.GetMoveSpeedBonus(PlayerStateManager.Instance.state)
            : 0f;
        float statMoveBonus = PlayerStateManager.Instance != null
            ? Mathf.Max(0f, PlayerStateManager.Instance.state.stats.moveSpeed - walkSpeed)
            : 0f;

        bool wantsSprint = !_isCrouching && sprintHeld && move.sqrMagnitude > 0.01f && move.y > 0.1f && HasStamina(minSprintStamina);
        _isSprinting = wantsSprint && TrySpendSprint(dt);
        if (!_isSprinting)
            RecoverSprintFatigue(dt);

        float baseSpeed = _isCrouching ? crouchSpeed : (_isSprinting ? sprintSpeed : walkSpeed);
        float targetSpeed = baseSpeed + itemMoveBonus + statMoveBonus;
        Vector3 targetPlanar = wish * targetSpeed;
        float accel = grounded ? (move.sqrMagnitude > 0.01f ? acceleration : deceleration) : acceleration * airControl;
        _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetPlanar, accel * dt);

        bool jumped = TryConsumeBufferedJump(grounded);
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
        YQPlayerFollowingSemanticChunkStreamer streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        Vector3 movementStart = transform.position;
        Vector3 requestedDisplacement = displacement;
        _lastRequestedMoveDisplacement = requestedDisplacement;
        _lastActualMoveDisplacement = Vector3.zero;
        _lastMoveCollisionFlags = CollisionFlags.None;
        _lastMoveRejectedByTraversalGate = false;
        bool startedTraversable = streamer != null && streamer.isActiveAndEnabled &&
            streamer.TryValidateCurrentTraversability(movementStart, _controller.radius, _controller.skinWidth, out _);
        if (streamer != null && streamer.isActiveAndEnabled &&
            !streamer.TryConstrainMovement(
                transform.position,
                displacement,
                _controller.radius,
                _controller.skinWidth,
                out _))
        {
            // note: Reject the entire controller step when its clearance sweep is blocked; CharacterController resolution can add sideways motion to both horizontal and vertical moves.
            _lastMoveRejectedByTraversalGate = true;
            return CollisionFlags.None;
        }

        // note: Observe collision-resolved motion immediately, before another owner can change publication, without correcting or hiding the actual movement result.
        CollisionFlags flags = _controller.Move(displacement);
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
        _dashTimeRemaining = Mathf.Max(0.05f, dashDuration);
        _dashStartedThisFrame = true;
        _nextDashTime = Time.time + dashCooldown;
        _isCrouching = false;
        // note: Establish the checked standing shape before the dash moves, not gradually during its travel.
        _wasCrouching = false;
        UpdateCrouchController(0f);
        _planarVelocity = _dashDirection * sprintSpeed;
        actionRecorder?.RecordDodge();
        GetComponent<YQPlayerEquipmentVisual>()?.PlayRollFeedback();
    }

    private void UpdateCrouchState(bool crouchHeld, bool grounded, float dt)
    {
        bool targetCrouch = crouchHeld && grounded && _dashTimeRemaining <= 0f && !_isClimbing;
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
        // note: The last frame spends only the remaining dash time, keeping travel distance stable across frame rates and occasional long frames.
        float dashStep = Mathf.Min(Mathf.Max(0f, dt), _dashTimeRemaining);
        _dashTimeRemaining = Mathf.Max(0f, _dashTimeRemaining - dashStep);
        float speed = dashDistance / Mathf.Max(0.05f, dashDuration);
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
        if (!enableClimb || !jumpHeld || move.y <= 0.1f)
            return false;
        if (!TryGetClimbSurface(wish, out RaycastHit hit))
            return false;
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

        Vector3 side = Vector3.ProjectOnPlane(transform.right, hit.normal).normalized * (move.x * climbSideSpeed);
        Vector3 climb = Vector3.up * climbSpeed;
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
        YQPlayerFollowingSemanticChunkStreamer streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        if (streamer == null)
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
        return cameraPivot.rotation * _cameraBobLocalOffset;
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
        return _cameraRoll;
    }
}
