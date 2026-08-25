// Assets/Assets/Scripts/Tutorial/YourQuestTutorialEnemy.cs
using UnityEngine;

[DisallowMultipleComponent]
public class YourQuestTutorialEnemy : MonoBehaviour
{
    public int maxHealth = 40;
    public float moveSpeed = 3.4f;
    public float aggroRange = 18f;
    public float attackRange = 1.6f;
    public float attackCooldown = 1.2f;
    public int attackDamage = 12;
    public string semanticRegionId = "region_unknown";

    private int _health;
    private float _nextAttack;
    private Transform _player;
    private YourQuestTutorialEnemySpawner _spawner;

    public void Initialize(YourQuestTutorialEnemySpawner spawner, string regionId)
    {
        _spawner = spawner;
        semanticRegionId = regionId;
        _health = maxHealth;
    }

    private void Awake()
    {
        _health = maxHealth;
        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.mass = 1f;
        }
    }

    private void Update()
    {
        if (_player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) _player = p.transform;
        }

        if (_player == null) return;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        if (dist > aggroRange) return;

        if (dist > attackRange)
        {
            Vector3 dir = toPlayer.normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
            return;
        }

        if (Time.time >= _nextAttack)
        {
            _nextAttack = Time.time + attackCooldown;
            var combat = _player.GetComponent<YourQuestTutorialCombat>();
            if (combat != null)
                combat.ReceiveDamage(attackDamage, gameObject);
        }
    }

    public void ReceiveHit(int amount, GameObject source)
    {
        _health -= Mathf.Max(1, amount);
        if (_health > 0) return;

        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.state.AddXp(25);
            PlayerStateManager.Instance.state.AddLedgerLine("The player defeated a hostile echo in " + semanticRegionId + ".");
            PlayerStateManager.Instance.state.IncCounter("kill:" + semanticRegionId, 1f);
            PlayerStateManager.Instance.Save();
        }

        if (WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.State.ApplyLocationDelta(semanticRegionId, "add", 0.05f, "contested", "A tutorial enemy fell in this region.");
            WorldStateManager.Instance.Save();
        }

        _spawner?.NotifyEnemyDied(this);
        Destroy(gameObject);
    }
}
