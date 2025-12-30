using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float acceleration = 10f;
    public float deceleration = 15f;

    [Header("Jump")]
    public float jumpForce = 5f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    [Header("Ground Check")]
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.2f;

    [Header("Camera")]
    public Transform cameraPivot;
    public Camera cam;
    public float mouseSensitivity = 2f;
    public float thirdPersonDistance = 5f;
    public float thirdPersonHeight = 3f;
    public float cameraSmoothSpeed = 10f;
    public bool isFirstPerson = false;

    private Rigidbody rb;
    private Vector2 moveInput;
    private float lastGroundedTime;
    private float lastJumpPressedTime;

    private float yaw;
    private float pitch;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        moveInput = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
        if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
        if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
        if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            lastJumpPressedTime = Time.time;

        if (Keyboard.current.cKey.wasPressedThisFrame)
            isFirstPerson = !isFirstPerson;

        bool isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
        if (isGrounded) lastGroundedTime = Time.time;

        bool canJump = (Time.time - lastGroundedTime <= coyoteTime) &&
                       (Time.time - lastJumpPressedTime <= jumpBufferTime);

        if (canJump)
        {
            Vector3 vel = rb.linearVelocity;
            vel.y = 0;
            rb.linearVelocity = vel;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            lastJumpPressedTime = -10f;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        yaw += mouseDelta.x;
        pitch -= mouseDelta.y;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void FixedUpdate()
    {
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 camForward = cameraPivot.forward; camForward.y = 0; camForward.Normalize();
        Vector3 camRight = cameraPivot.right; camRight.y = 0; camRight.Normalize();

        Vector3 moveDir = (camForward * inputDir.z + camRight * inputDir.x).normalized;
        Vector3 targetVelocity = moveDir * moveSpeed;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity.x, (targetVelocity.sqrMagnitude > 0 ? acceleration : deceleration) * Time.fixedDeltaTime);
        velocity.z = Mathf.MoveTowards(velocity.z, targetVelocity.z, (targetVelocity.sqrMagnitude > 0 ? acceleration : deceleration) * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);

        if (moveDir.sqrMagnitude > 0.01f && !isFirstPerson)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.fixedDeltaTime);
        }

        if (isFirstPerson)
        {
            cam.transform.position = cameraPivot.position;
            cam.transform.rotation = cameraPivot.rotation;
        }
        else
        {
            Vector3 desiredPos = transform.position - cameraPivot.forward * thirdPersonDistance + Vector3.up * thirdPersonHeight;
            cam.transform.position = Vector3.Lerp(cam.transform.position, desiredPos, cameraSmoothSpeed * Time.fixedDeltaTime);
            cam.transform.LookAt(transform.position + Vector3.up * 1.5f);
        }
    }
}
