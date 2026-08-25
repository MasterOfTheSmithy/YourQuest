// Assets/Assets/Scripts/PrototypeBuilder/YQPrototypeEnemy.cs
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class YQPrototypeEnemy : MonoBehaviour
{
    public string semanticRegionId = "region_unknown";
    public string factionId = "wild_hollows";
    public int maxHealth = 40;
    public int touchDamage = 10;
    public float moveSpeed = 3.2f;
    public float chaseRange = 18f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1.0f;
    public int xpReward = 12;

    private int currentHealth;
    private float nextAttackTime;
    private Rigidbody rb;
    private Transform target;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        if (target == null) return;

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist > chaseRange) return;

        if (dist > attackRange)
        {
            Vector3 dir = to.normalized;
            rb.MovePosition(rb.position + dir * moveSpeed * Time.deltaTime);
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);
            return;
        }

        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            var vitals = target.GetComponent<YQPrototypePlayerVitals>();
            if (vitals != null)
                vitals.TakeDamage(touchDamage, gameObject);
        }
    }

    public void TakeDamage(int amount, GameObject source)
    {
        if (amount <= 0) return;
        currentHealth -= amount;
        if (currentHealth > 0) return;

        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.state.experience += xpReward;
            PlayerStateManager.Instance.state.Touch();
            if (PlayerStateManager.Instance.autosave)
                PlayerStateManager.Instance.Save();
        }

        if (ActionRecorder.Instance != null)
            ActionRecorder.Instance.RecordCombat(gameObject);

        Destroy(gameObject);
    }
}
