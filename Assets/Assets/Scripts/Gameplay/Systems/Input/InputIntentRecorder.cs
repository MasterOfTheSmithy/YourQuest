using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bridges Unity InputActions ? ActionRecorder as INTENT signals.
/// Works with PlayerInput notification behaviors:
/// - Send Messages / Broadcast Messages (uses On<ActionName> methods)
/// - Invoke Unity Events / Invoke C# Events (subscribes to InputAction callbacks)
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class InputIntentRecorder : MonoBehaviour
{
    private PlayerInput playerInput;
    private ActionRecorder recorder;

    // Cached actions (used in non-SendMessages modes)
    private InputAction move;
    private InputAction jump;
    private InputAction sprint;
    private InputAction crouch;
    private InputAction dodge;
    private InputAction attack;
    private InputAction aim;
    private InputAction interact;

    // Movement value cached for SendMessages mode (OnMove)
    private Vector2 moveValue;

    [Header("Thresholds")]
    [Tooltip("Movement magnitude required to count as 'moving'.")]
    public float moveStartThreshold = 0.15f;

    [Tooltip("Minimum seconds between repeated movement intent events.")]
    public float moveIntentCooldown = 0.6f;

    private bool wasMoving;
    private float lastMoveIntentTime;

    private bool usingSendMessages;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        recorder = ActionRecorder.Instance;

        usingSendMessages =
            playerInput.notificationBehavior == PlayerNotifications.SendMessages ||
            playerInput.notificationBehavior == PlayerNotifications.BroadcastMessages;
    }

    private void OnEnable()
    {
        // If using SendMessages/BroadcastMessages, Unity will call OnMove/OnJump/etc automatically.
        // Do NOT subscribe to actions in this mode (avoids double-recording).
        if (usingSendMessages) return;

        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogWarning("[InputIntentRecorder] PlayerInput/actions missing.");
            return;
        }

        var map = playerInput.actions;

        // These names must match your InputActions asset action names
        move = SafeGet(map, "Move");
        jump = SafeGet(map, "Jump");
        sprint = SafeGet(map, "Sprint");
        crouch = SafeGet(map, "Crouch");
        dodge = SafeGet(map, "Dodge");
        attack = SafeGet(map, "Attack");
        aim = SafeGet(map, "Aim");
        interact = SafeGet(map, "Interact");

        if (jump != null) jump.performed += OnJumpAction;
        if (sprint != null) sprint.performed += OnSprintAction;
        if (crouch != null) crouch.performed += OnCrouchAction;
        if (dodge != null) dodge.performed += OnDodgeAction;
        if (attack != null) attack.performed += OnAttackAction;
        if (aim != null)
        {
            aim.performed += OnAimStartAction;
            aim.canceled += OnAimEndAction;
        }
        if (interact != null) interact.performed += OnInteractAction;
    }

    private void OnDisable()
    {
        if (usingSendMessages) return;

        // Guard everything (prevents NullReferenceExceptions)
        if (jump != null) jump.performed -= OnJumpAction;
        if (sprint != null) sprint.performed -= OnSprintAction;
        if (crouch != null) crouch.performed -= OnCrouchAction;
        if (dodge != null) dodge.performed -= OnDodgeAction;
        if (attack != null) attack.performed -= OnAttackAction;

        if (aim != null)
        {
            aim.performed -= OnAimStartAction;
            aim.canceled -= OnAimEndAction;
        }

        if (interact != null) interact.performed -= OnInteractAction;
    }

    private void Update()
    {
        // note: Modal screens and initial generation should not generate movement/combat intent history.
        if (RuntimeModalUiBlocker.IsBlocked)
        {
            moveValue =
                Vector2.zero;

            wasMoving =
                false;

            return;
        }

        HandleMovementIntent();
    }

    private void HandleMovementIntent()
    {
        Vector2 v;

        if (usingSendMessages)
        {
            v = moveValue; // set by OnMove(InputValue)
        }
        else
        {
            if (move == null) return;
            v = move.ReadValue<Vector2>();
        }

        bool isMoving = v.magnitude >= moveStartThreshold;

        // Movement started
        if (isMoving && !wasMoving && Time.time - lastMoveIntentTime >= moveIntentCooldown)
        {
            recorder?.RecordMove();
            lastMoveIntentTime = Time.time;
        }

        // Movement stopped
        if (!isMoving && wasMoving)
        {
            recorder?.RecordMove(); // stop is meaningful
        }

        wasMoving = isMoving;
    }

    // -------------------------
    // Send Messages entry points
    // (Unity calls these when PlayerInput notificationBehavior = Send Messages)
    // -------------------------

    public void OnMove(InputValue value)
    {
        moveValue = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (RuntimeModalUiBlocker.IsBlocked)
            return;

        if (value.isPressed) recorder?.RecordJump();
    }

    public void OnSprint(InputValue value)
    {
        if (RuntimeModalUiBlocker.IsBlocked)
            return;

        if (value.isPressed) recorder?.RecordMove();
    }

    public void OnCrouch(InputValue value)
    {
        if (RuntimeModalUiBlocker.IsBlocked)
            return;

        if (value.isPressed) recorder?.RecordCrouch();
    }

    public void OnDodge(InputValue value)
    {
        if (RuntimeModalUiBlocker.IsBlocked)
            return;

        if (value.isPressed) recorder?.RecordDodge();
    }

    public void OnAttack(InputValue value)
    {
        if (RuntimeModalUiBlocker.IsBlocked)
            return;

        if (value.isPressed) recorder?.RecordCombat(null);
    }

    public void OnAim(InputValue value)
    {
        if (RuntimeModalUiBlocker.IsBlocked)
            return;

        // In SendMessages, you usually only get performed; treat both press/release as posture change.
        recorder?.RecordMove();
    }

    public void OnInteract(InputValue value)
    {
        if (RuntimeModalUiBlocker.IsBlocked)
            return;

        if (value.isPressed) recorder?.RecordInteract(null);
    }

    // -------------------------
    // InputAction subscription handlers
    // (used when notificationBehavior = Invoke Unity Events / Invoke C# Events)
    // -------------------------

    private void OnJumpAction(InputAction.CallbackContext ctx) { if (!RuntimeModalUiBlocker.IsBlocked) recorder?.RecordJump(); }
    private void OnSprintAction(InputAction.CallbackContext ctx) { if (!RuntimeModalUiBlocker.IsBlocked) recorder?.RecordMove(); }
    private void OnCrouchAction(InputAction.CallbackContext ctx) { if (!RuntimeModalUiBlocker.IsBlocked) recorder?.RecordCrouch(); }
    private void OnDodgeAction(InputAction.CallbackContext ctx) { if (!RuntimeModalUiBlocker.IsBlocked) recorder?.RecordDodge(); }
    private void OnAttackAction(InputAction.CallbackContext ctx) { if (!RuntimeModalUiBlocker.IsBlocked) recorder?.RecordCombat(null); }
    private void OnAimStartAction(InputAction.CallbackContext ctx) { if (!RuntimeModalUiBlocker.IsBlocked) recorder?.RecordMove(); }
    private void OnAimEndAction(InputAction.CallbackContext ctx) { if (!RuntimeModalUiBlocker.IsBlocked) recorder?.RecordMove(); }
    private void OnInteractAction(InputAction.CallbackContext ctx) { if (!RuntimeModalUiBlocker.IsBlocked) recorder?.RecordInteract(null); }

    private static InputAction SafeGet(InputActionAsset asset, string actionName)
    {
        try
        {
            return asset[actionName];
        }
        catch
        {
            Debug.LogWarning($"[InputIntentRecorder] InputAction '{actionName}' not found in asset.");
            return null;
        }
    }
}

