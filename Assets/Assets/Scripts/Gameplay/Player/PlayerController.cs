using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    public enum ViewMode { ThirdPerson, FirstPerson }

    [Header("Mode")]
    public ViewMode viewMode = ViewMode.ThirdPerson;
    public Key toggleViewKey = Key.C;

    [Header("References")]
    public Transform cameraPivot;     // child of player (head height)
    public Camera playerCamera;       // NOT parented to player
    public ActionRecorder actionRecorder;

    [Header("Look")]
    public float sensitivityX = 0.12f;
    public float sensitivityY = 0.10f;
    public float lookSmoothing = 14f;
    public float pitchMin = -75f;
    public float pitchMax = 80f;

    [Header("Movement")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 8.5f;
    public float acceleration = 35f;
    public float deceleration = 45f;
    public float airAcceleration = 12f;
    public float airMaxSpeed = 6.5f;
    public float rotationSharpness = 16f;

    [Header("Jump")]
    public float jumpVelocity = 6.5f;
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;
    public float fallGravityMultiplier = 2.2f;
    public float lowJumpMultiplier = 1.6f;

    [Header("Grounding")]
    public LayerMask groundMask = ~0;
    public float groundProbeDistance = 0.25f;
    public float maxGroundSlope = 55f;
    public float stickToGroundForce = 10f;

    [Header("Third Person Camera")]
    public float thirdPersonDistance = 3.5f;
    public float thirdPersonHeight = 1.6f;
    public float shoulderOffset = 0.45f;
    public float cameraSharpness = 18f;
    public float cameraCollisionRadius = 0.18f;
    public float cameraCollisionPadding = 0.05f;

    [Header("FOV")]
    public float firstPersonFov = 82f;
    public float thirdPersonFov = 70f;
    public float fovSharpness = 8f;

    Rigidbody rb;
    CapsuleCollider capsule;

    Vector2 moveInput;
    bool sprintHeld;
    bool jumpPressed;
    bool jumpHeld;

    float yaw;
    float pitch;
    Vector2 smoothLook;
    Vector2 lookVel;

    bool grounded;
    Vector3 groundNormal = Vector3.up;
    float lastGroundedTime;
    float lastJumpPressedTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (!playerCamera) playerCamera = Camera.main;

        yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        ReadInput();
        UpdateLook(Time.deltaTime);
        CheckGrounded();

        if (jumpPressed)
            lastJumpPressedTime = Time.time;

        if (Keyboard.current != null && Keyboard.current[toggleViewKey].wasPressedThisFrame)
            viewMode = viewMode == ViewMode.FirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson;

        if (actionRecorder)
        {
            if (moveInput.sqrMagnitude > 0.01f) actionRecorder.RecordMove();
            if (jumpPressed) actionRecorder.RecordJump();
        }
    }

    void FixedUpdate()
    {
        ApplyMovement(Time.fixedDeltaTime);
        ApplyJump();
        ApplyExtraGravity();
    }

    void LateUpdate()
    {
        UpdateCamera(Time.deltaTime);
    }

    // ---------------- INPUT ----------------

    void ReadInput()
    {
        moveInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;

            sprintHeld = Keyboard.current.leftShiftKey.isPressed;
            jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
            jumpHeld = Keyboard.current.spaceKey.isPressed;
        }

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    // ---------------- LOOK ----------------

    void UpdateLook(float dt)
    {
        if (Mouse.current == null) return;

        Vector2 raw = Mouse.current.delta.ReadValue();
        Vector2 target = new(raw.x * sensitivityX, raw.y * sensitivityY);

        smoothLook = Vector2.SmoothDamp(
            smoothLook, target, ref lookVel,
            1f / Mathf.Max(1f, lookSmoothing), Mathf.Infinity, dt
        );

        yaw += smoothLook.x;
        pitch -= smoothLook.y;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);

        if (viewMode == ViewMode.FirstPerson)
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ---------------- GROUND ----------------

    void CheckGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        grounded = false;

        if (Physics.SphereCast(origin, capsule.radius * 0.95f, Vector3.down,
            out RaycastHit hit, groundProbeDistance, groundMask))
        {
            float slope = Vector3.Angle(hit.normal, Vector3.up);
            if (slope <= maxGroundSlope)
            {
                grounded = true;
                groundNormal = hit.normal;
                lastGroundedTime = Time.time;

                if (rb.linearVelocity.y <= 0f)
                    rb.AddForce(Vector3.down * stickToGroundForce, ForceMode.Acceleration);
            }
        }
    }

    // ---------------- MOVEMENT ----------------

    void ApplyMovement(float dt)
    {
        Vector3 forward = cameraPivot.forward;
        Vector3 right = cameraPivot.right;
        forward.y = right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 wishDir = (forward * moveInput.y + right * moveInput.x).normalized;
        float targetSpeed = sprintHeld ? sprintSpeed : walkSpeed;

        Vector3 vel = rb.linearVelocity;
        Vector3 lateral = new(vel.x, 0, vel.z);

        float accel = grounded ? acceleration : airAcceleration;
        float max = grounded ? targetSpeed : airMaxSpeed;

        Vector3 desired = wishDir * max;
        lateral = Vector3.MoveTowards(lateral, desired, accel * dt);

        rb.linearVelocity = new Vector3(lateral.x, vel.y, lateral.z);

        if (viewMode == ViewMode.ThirdPerson && wishDir.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(wishDir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, rot,
                1f - Mathf.Exp(-rotationSharpness * dt)
            );
        }
    }

    void ApplyJump()
    {
        bool canJump =
            (Time.time - lastGroundedTime) <= coyoteTime &&
            (Time.time - lastJumpPressedTime) <= jumpBuffer;

        if (!canJump) return;

        lastJumpPressedTime = -999;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpVelocity, rb.linearVelocity.z);
    }

    void ApplyExtraGravity()
    {
        if (grounded) return;

        if (rb.linearVelocity.y < 0)
            rb.AddForce(Physics.gravity * (fallGravityMultiplier - 1), ForceMode.Acceleration);
        else if (!jumpHeld)
            rb.AddForce(Physics.gravity * (lowJumpMultiplier - 1), ForceMode.Acceleration);
    }

    // ---------------- CAMERA ----------------

    void UpdateCamera(float dt)
    {
        if (!playerCamera || !cameraPivot) return;

        float targetFov = viewMode == ViewMode.FirstPerson ? firstPersonFov : thirdPersonFov;
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView, targetFov,
            1f - Mathf.Exp(-fovSharpness * dt)
        );

        if (viewMode == ViewMode.FirstPerson)
        {
            playerCamera.transform.SetPositionAndRotation(
                cameraPivot.position, cameraPivot.rotation
            );
            return;
        }

        Vector3 pivot = cameraPivot.position + Vector3.up * thirdPersonHeight;
        Vector3 shoulder = cameraPivot.right * shoulderOffset;
        Vector3 desired = pivot + shoulder - cameraPivot.forward * thirdPersonDistance;

        Vector3 from = pivot + shoulder;
        Vector3 dir = desired - from;
        float dist = dir.magnitude;
        dir.Normalize();

        Vector3 finalPos = desired;
        if (Physics.SphereCast(from, cameraCollisionRadius, dir, out RaycastHit hit, dist, groundMask))
            finalPos = from + dir * Mathf.Max(0, hit.distance - cameraCollisionPadding);

        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position, finalPos,
            1f - Mathf.Exp(-cameraSharpness * dt)
        );

        Quaternion lookRot = Quaternion.LookRotation(
            (pivot - playerCamera.transform.position).normalized
        );

        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation, lookRot,
            1f - Mathf.Exp(-cameraSharpness * dt)
        );
    }
}
