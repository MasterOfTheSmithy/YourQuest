// Assets/Assets/Scripts/Tutorial/YourQuestTestCombat.cs
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class YourQuestTestCombat : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth = 100;
    public int meleeDamage = 20;
    public float meleeRange = 2.25f;
    public float meleeRadius = 0.9f;
    public float attackCooldown = 0.3f;
    public float interactRange = 3f;

    private float nextAttackTime;
    private Camera viewCamera;
    private ActionRecorder recorder;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 1, maxHealth);
        recorder = GetComponent<ActionRecorder>();
    }

    private void Start()
    {
        viewCamera = Camera.main;
    }

    private void Update()
    {
        // note: Test combat shares the same input block so left click cannot escape startup locks.
        if (RuntimeModalUiBlocker.IsBlocked)
            return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryAttack();

        if (Keyboard.current != null)
        {
            if (Keyboard.current.fKey.wasPressedThisFrame)
                TryInteractShrine();

            if (Keyboard.current.tKey.wasPressedThisFrame)
                ForceWorldThink();

            if (Keyboard.current.yKey.wasPressedThisFrame)
                ForceProgressionThink();
        }
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        Vector3 center = transform.position + transform.forward * meleeRange + Vector3.up;
        Collider[] hits = Physics.OverlapSphere(center, meleeRadius, ~0, QueryTriggerInteraction.Ignore);
        bool landed = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
                continue;

            var enemy = hits[i].GetComponentInParent<YourQuestTestEnemy>();
            if (enemy == null)
                continue;

            enemy.ReceiveHit(meleeDamage);
            landed = true;
            recorder?.RecordCombat(enemy.gameObject);
        }

        if (!landed)
            recorder?.RecordCombat(null);
    }

    private void TryInteractShrine()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;
        if (viewCamera == null)
            return;

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Ignore))
            return;

        var shrine = hit.collider.GetComponentInParent<YourQuestTestShrine>();
        if (shrine != null)
            shrine.TryUse(gameObject);
    }

    private void ForceWorldThink()
    {
        var thinker = FindFirstObjectByType<LLMThinkCycle>();
        if (thinker == null)
            return;

        thinker.SendMessage("TryThink", SendMessageOptions.DontRequireReceiver);
    }

    private void ForceProgressionThink()
    {
        var thinker = FindFirstObjectByType<ProgressionThinkCycle>();
        if (thinker == null)
            return;

        thinker.SendMessage("TryThink", SendMessageOptions.DontRequireReceiver);
    }

    public void ReceiveDamage(int amount)
    {
        currentHealth = Mathf.Clamp(currentHealth - Mathf.Max(0, amount), 0, maxHealth);
        if (currentHealth > 0)
            return;

        currentHealth = maxHealth;
        transform.position = new Vector3(0f, 2f, 0f);
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = Vector3.zero;
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0, amount), 0, maxHealth);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * meleeRange + Vector3.up, meleeRadius);
    }
}
