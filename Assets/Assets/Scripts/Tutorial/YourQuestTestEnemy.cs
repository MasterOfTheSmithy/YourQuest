// Assets/Assets/Scripts/Tutorial/YourQuestTestEnemy.cs
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(EntityInfo))]
public sealed class YourQuestTestEnemy : MonoBehaviour
{
    public int maxHealth = 50;
    public float moveSpeed = 3.8f;
    public int contactDamage = 10;
    public float aggroRange = 16f;
    public float attackRange = 1.6f;
    public float attackCooldown = 1.0f;
    public float leashRadius = 30f;

    private int currentHealth;
    private float nextAttackTime;
    private Vector3 spawnPoint;
    private Rigidbody rb;
    private EntityInfo info;

    private void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
        spawnPoint = transform.position;
        rb = GetComponent<Rigidbody>();
        info = GetComponent<EntityInfo>();
    }

    private void FixedUpdate()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        Vector3 toPlayer = player.transform.position - transform.position;
        float dist = toPlayer.magnitude;
        info.targetingPlayer = dist <= aggroRange;

        if (Vector3.Distance(transform.position, spawnPoint) > leashRadius)
        {
            Vector3 back = (spawnPoint - transform.position).normalized;
            rb.MovePosition(transform.position + back * moveSpeed * Time.fixedDeltaTime);
            return;
        }

        if (dist > aggroRange)
            return;

        if (dist > attackRange)
        {
            Vector3 dir = toPlayer.normalized;
            dir.y = 0f;
            rb.MovePosition(transform.position + dir * moveSpeed * Time.fixedDeltaTime);
            if (dir.sqrMagnitude > 0.0001f)
                rb.MoveRotation(Quaternion.LookRotation(dir));
            return;
        }

        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        var combat = player.GetComponent<YourQuestTestCombat>();
        if (combat != null)
            combat.ReceiveDamage(contactDamage);
    }

    public void ReceiveHit(int damage)
    {
        if (damage <= 0)
            return;

        currentHealth -= damage;
        if (currentHealth > 0)
            return;

        var psm = PlayerStateManager.Instance;
        if (psm != null)
            psm.GrantXp(8);

        Destroy(gameObject);
    }
}
