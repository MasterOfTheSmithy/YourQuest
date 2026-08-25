// Assets/Assets/Scripts/Tutorial/YourQuestTutorialVitals.cs
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YourQuestTutorialVitals : MonoBehaviour
{
    [Header("Runtime Vitals")]
    public int currentHealth;
    public int currentStamina;
    public int currentMana;

    [Header("Regen")]
    public float staminaPerSecond = 18f;
    public float manaPerSecond = 8f;
    public float combatRegenDelay = 1.5f;

    private float _lastSpendOrDamageTime;

    public int MaxHealth => Mathf.Max(1, PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state.stats.maxHealth : 100);
    public int MaxStamina => Mathf.Max(1, PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state.stats.maxStamina : 100);
    public int MaxMana => Mathf.Max(0, PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state.stats.maxMana : 50);

    private void Awake()
    {
        SyncFromStats(fullRefill: true);
    }

    private void Update()
    {
        int maxHealth = MaxHealth;
        int maxStamina = MaxStamina;
        int maxMana = MaxMana;

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
        currentMana = Mathf.Clamp(currentMana, 0, maxMana);

        if (Time.time - _lastSpendOrDamageTime >= combatRegenDelay)
        {
            if (currentStamina < maxStamina)
                currentStamina = Mathf.Min(maxStamina, currentStamina + Mathf.RoundToInt(staminaPerSecond * Time.deltaTime));

            if (currentMana < maxMana)
                currentMana = Mathf.Min(maxMana, currentMana + Mathf.RoundToInt(manaPerSecond * Time.deltaTime));
        }
    }

    public void SyncFromStats(bool fullRefill)
    {
        if (fullRefill || currentHealth <= 0) currentHealth = MaxHealth;
        if (fullRefill || currentStamina <= 0) currentStamina = MaxStamina;
        if (fullRefill || currentMana < 0) currentMana = MaxMana;
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
        currentStamina = Mathf.Clamp(currentStamina, 0, MaxStamina);
        currentMana = Mathf.Clamp(currentMana, 0, MaxMana);
    }

    public bool SpendStamina(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (currentStamina < amount) return false;
        currentStamina -= amount;
        _lastSpendOrDamageTime = Time.time;
        return true;
    }

    public bool SpendMana(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (currentMana < amount) return false;
        currentMana -= amount;
        _lastSpendOrDamageTime = Time.time;
        return true;
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0, amount), 0, MaxHealth);
    }

    public void RestoreAll()
    {
        currentHealth = MaxHealth;
        currentStamina = MaxStamina;
        currentMana = MaxMana;
    }

    public void TakeDamage(int amount)
    {
        amount = Mathf.Max(0, amount);
        currentHealth = Mathf.Clamp(currentHealth - amount, 0, MaxHealth);
        _lastSpendOrDamageTime = Time.time;

        if (currentHealth <= 0)
            HandleDeath();
    }

    private void HandleDeath()
    {
        currentHealth = MaxHealth;
        currentStamina = MaxStamina;
        currentMana = MaxMana;

        if (PlayerStateManager.Instance != null)
            PlayerStateManager.Instance.SetPosition(Vector3.zero);

        transform.position = new Vector3(0f, 2f, 0f);
    }
}
