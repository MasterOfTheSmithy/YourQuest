// Assets/Assets/Scripts/PrototypeBuilder/YQPrototypePlayerVitals.cs
using UnityEngine;

[DisallowMultipleComponent]
public class YQPrototypePlayerVitals : MonoBehaviour
{
    [Header("Base")]
    public int maxHealth = 100;
    public int maxStamina = 100;
    public int maxMana = 60;

    [Header("Regen")]
    public float staminaRegenPerSecond = 18f;
    public float manaRegenPerSecond = 10f;
    public float outOfCombatHealthRegenPerSecond = 1.5f;
    public float combatTimeoutSeconds = 6f;

    [Header("Runtime")]
    public int currentHealth;
    public float currentStamina;
    public float currentMana;

    private Vector3 respawnPoint;
    private float lastDamageTime = -999f;
    private ActionRecorder recorder;

    public bool IsDead => currentHealth <= 0;
    public bool InCombat => Time.time - lastDamageTime <= combatTimeoutSeconds;

    private void Awake()
    {
        recorder = GetComponent<ActionRecorder>();
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 1, maxHealth);
        currentStamina = Mathf.Clamp(currentStamina <= 0f ? maxStamina : currentStamina, 0f, maxStamina);
        currentMana = Mathf.Clamp(currentMana <= 0f ? maxMana : currentMana, 0f, maxMana);
        respawnPoint = transform.position;
    }

    private void Update()
    {
        if (IsDead)
            return;

        currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenPerSecond * Time.deltaTime);
        currentMana = Mathf.Min(maxMana, currentMana + manaRegenPerSecond * Time.deltaTime);

        if (!InCombat && currentHealth < maxHealth)
            currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.CeilToInt(outOfCombatHealthRegenPerSecond * Time.deltaTime));
    }

    public void SetRespawnPoint(Vector3 point)
    {
        respawnPoint = point;
    }

    public bool SpendStamina(float amount)
    {
        if (amount <= 0f) return true;
        if (currentStamina < amount) return false;
        currentStamina -= amount;
        return true;
    }

    public bool SpendMana(float amount)
    {
        if (amount <= 0f) return true;
        if (currentMana < amount) return false;
        currentMana -= amount;
        return true;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    public void RestoreAll()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentMana = maxMana;
    }

    public void TakeDamage(int amount, GameObject source = null)
    {
        if (amount <= 0 || IsDead) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        lastDamageTime = Time.time;

        if (recorder != null)
            recorder.RecordCombat(source);

        if (currentHealth <= 0)
            Respawn();
    }

    public void Respawn()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentMana = maxMana;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = respawnPoint;

        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.state.lastPosition = respawnPoint;
            PlayerStateManager.Instance.state.Touch();
            if (PlayerStateManager.Instance.autosave)
                PlayerStateManager.Instance.Save();
        }
    }
}
