// Assets/Assets/Scripts/Tutorial/YourQuestTutorialCombat.cs
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class YourQuestTutorialCombat : MonoBehaviour
{
    public float attackRange = 2.35f;
    public float attackRadius = 1.2f;
    public float attackDamage = 18f;
    public float attackCooldown = 0.28f;
    public float interactRange = 3.5f;
    public int maxHealth = 100;

    private float _nextAttack;
    private int _currentHealth;
    private ActionRecorder _recorder;
    private PlayerController _playerController;

    public int CurrentHealth => _currentHealth;

    private void Awake()
    {
        _currentHealth = maxHealth;
        _recorder = GetComponent<ActionRecorder>();
        _playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        // note: Direct mouse polling must respect menus and initial Goddess generation.
        if (RuntimeModalUiBlocker.IsBlocked)
            return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryAttack();

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            TryInteract();
    }

    public void ReceiveDamage(int amount, GameObject source)
    {
        _currentHealth = Mathf.Max(0, _currentHealth - Mathf.Max(0, amount));
        if (_currentHealth > 0) return;

        _currentHealth = maxHealth;
        transform.position = new Vector3(0f, 2f, 0f);

        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.state.AddLedgerLine("The player fell in battle and restarted at the tutorial hub.");
            PlayerStateManager.Instance.state.IncCounter("death:tutorial", 1f);
            PlayerStateManager.Instance.Save();
        }
    }

    private void TryAttack()
    {
        if (Time.time < _nextAttack) return;
        _nextAttack = Time.time + attackCooldown;

        Transform pivot = _playerController != null && _playerController.cameraPivot != null
            ? _playerController.cameraPivot
            : transform;

        Vector3 center = pivot.position + pivot.forward * attackRange;
        Collider[] hits = Physics.OverlapSphere(center, attackRadius, ~0, QueryTriggerInteraction.Ignore);

        bool hitAny = false;
        for (int i = 0; i < hits.Length; i++)
        {
            var enemy = hits[i].GetComponentInParent<YourQuestTutorialEnemy>();
            if (enemy == null) continue;

            enemy.ReceiveHit(Mathf.RoundToInt(attackDamage), gameObject);
            _recorder?.RecordCombat(enemy.gameObject);
            hitAny = true;
        }

        if (!hitAny && PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.state.AddLedgerLine("The player practiced a missed attack in live conditions.");
        }

        YQRuntimeAudioFeedback.PlayPlayerMelee(transform.position + transform.forward * 1.1f + Vector3.up * 1.1f, hitAny);
    }

    private void TryInteract()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Collide))
            return;

        var shrine = hit.collider.GetComponentInParent<YourQuestTutorialShrine>();
        if (shrine != null)
        {
            shrine.Interact(gameObject);
            _recorder?.RecordInteract(shrine.gameObject);
            return;
        }
    }
}
