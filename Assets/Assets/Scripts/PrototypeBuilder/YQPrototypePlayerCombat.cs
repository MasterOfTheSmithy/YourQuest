// Assets/Assets/Scripts/PrototypeBuilder/YQPrototypePlayerCombat.cs
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class YQPrototypePlayerCombat : MonoBehaviour
{
    [Header("Refs")]
    public Camera playerCamera;
    public ActionRecorder actionRecorder;
    public YQPrototypePlayerVitals vitals;

    [Header("Attack")]
    public float attackRange = 2.6f;
    public float attackRadius = 1.0f;
    public int attackDamage = 20;
    public float attackCooldown = 0.35f;
    public LayerMask hitMask = ~0;

    [Header("Skills")]
    public float healManaCost = 12f;
    public int healAmount = 25;
    public float shockwaveManaCost = 20f;
    public float shockwaveRadius = 5.5f;
    public int shockwaveDamage = 16;

    private float nextAttackTime;

    private void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (actionRecorder == null) actionRecorder = GetComponent<ActionRecorder>();
        if (vitals == null) vitals = GetComponent<YQPrototypePlayerVitals>();
    }

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryAttack();

        if (Keyboard.current != null)
        {
            if (Keyboard.current.rKey.wasPressedThisFrame)
                TryHeal();

            if (Keyboard.current.fKey.wasPressedThisFrame)
                TryShockwave();
        }
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + attackCooldown;

        Vector3 origin = playerCamera != null ? playerCamera.transform.position : transform.position + Vector3.up * 1.5f;
        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        Vector3 center = origin + forward * attackRange;

        Collider[] hits = Physics.OverlapSphere(center, attackRadius, hitMask, QueryTriggerInteraction.Ignore);
        bool landed = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var enemy = hits[i].GetComponentInParent<YQPrototypeEnemy>();
            if (enemy == null) continue;

            enemy.TakeDamage(attackDamage, gameObject);
            landed = true;
        }

        if (actionRecorder != null)
            actionRecorder.RecordCombat(landed ? gameObject : null);
    }

    private void TryHeal()
    {
        if (vitals == null) return;
        if (!vitals.SpendMana(healManaCost)) return;

        vitals.Heal(healAmount);
    }

    private void TryShockwave()
    {
        if (vitals == null) return;
        if (!vitals.SpendMana(shockwaveManaCost)) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, shockwaveRadius, hitMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var enemy = hits[i].GetComponentInParent<YQPrototypeEnemy>();
            if (enemy == null) continue;
            enemy.TakeDamage(shockwaveDamage, gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        Vector3 origin = cam != null ? cam.transform.position : transform.position + Vector3.up * 1.5f;
        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;
        Vector3 center = origin + forward * attackRange;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, attackRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, shockwaveRadius);
    }
}
