// Assets/Assets/Scripts/Tutorial/YQInvestorVitals.cs
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YQInvestorVitals : MonoBehaviour
{
    public float CurrentHealth { get; private set; }
    public float CurrentStamina { get; private set; }
    public float CurrentMana { get; private set; }
    public bool IsDead => CurrentHealth <= 0.01f;

    [Header("Recovery")]
    public float healthRegenPerSecond = 1.5f;
    public float staminaRegenPerSecond = 22f;
    public float activeStaminaRegenPerSecond = 4f;
    public float staminaSpendRegenDelay = 0.45f;
    public float manaRegenPerSecond = 8f;

    private bool _isSprinting;
    private bool _pendingRespawn;
    private float _staminaRegenBlockedUntil;
    private Canvas _deathCanvas;
    private TMP_Text _deathText;

    private void Awake()
    {
        CurrentHealth = GetMaxHealth();
        CurrentStamina = GetMaxStamina();
        CurrentMana = GetMaxMana();
        BuildDeathUi();
    }

    private void Update()
    {
        if (_pendingRespawn)
        {
            if (Keyboard.current != null && (Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                RespawnNow();
            return;
        }

        float maxHealth = GetMaxHealth();
        float maxStamina = GetMaxStamina();
        float maxMana = GetMaxMana();

        float staminaRegen = Time.time < _staminaRegenBlockedUntil
            ? 0f
            : (_isSprinting ? activeStaminaRegenPerSecond : staminaRegenPerSecond);

        CurrentHealth = Mathf.Clamp(CurrentHealth + healthRegenPerSecond * Time.deltaTime, 0f, maxHealth);
        CurrentStamina = Mathf.Clamp(CurrentStamina + staminaRegen * Time.deltaTime, 0f, maxStamina);
        CurrentMana = Mathf.Clamp(CurrentMana + manaRegenPerSecond * Time.deltaTime, 0f, maxMana);
    }

    public void SetSprinting(bool value)
    {
        _isSprinting = value;
    }

    public bool SpendStamina(float amount)
    {
        if (_pendingRespawn || CurrentStamina < amount)
            return false;

        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        if (amount > 0f)
            _staminaRegenBlockedUntil = Time.time + Mathf.Max(0f, staminaSpendRegenDelay);
        return true;
    }

    public bool SpendMana(float amount)
    {
        if (_pendingRespawn || CurrentMana < amount)
            return false;
        CurrentMana = Mathf.Max(0f, CurrentMana - amount);
        return true;
    }

    public void Heal(float amount)
    {
        if (_pendingRespawn)
            return;
        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0f, GetMaxHealth());
    }

    public void RestoreStamina(float amount)
    {
        if (_pendingRespawn)
            return;
        CurrentStamina = Mathf.Clamp(CurrentStamina + amount, 0f, GetMaxStamina());
    }

    public void RestoreMana(float amount)
    {
        if (_pendingRespawn)
            return;
        CurrentMana = Mathf.Clamp(CurrentMana + amount, 0f, GetMaxMana());
    }

    public void TakeDamage(float amount)
    {
        if (_pendingRespawn || amount <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        if (IsDead)
            EnterDeathState();
    }

    private void EnterDeathState()
    {
        if (_pendingRespawn)
            return;

        _pendingRespawn = true;
        RuntimeModalUiBlocker.Acquire(this);
        if (_deathCanvas != null)
            _deathCanvas.enabled = true;

        DropDeathLoot();

        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm != null && psm.state != null)
        {
            psm.state.AddLedgerLine("The player fell in combat and left behind salvage.");
            psm.state.IncCounter("death", 1f);
            psm.Save();
        }
    }

    private void RespawnNow()
    {
        _pendingRespawn = false;
        RuntimeModalUiBlocker.Release(this);
        if (_deathCanvas != null)
            _deathCanvas.enabled = false;

        CurrentHealth = GetMaxHealth();
        CurrentStamina = GetMaxStamina();
        CurrentMana = GetMaxMana();
        transform.position = new Vector3(0f, 1.25f, -8f);

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
            cc.Move(Vector3.zero);
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = Vector3.zero;
    }

    private void DropDeathLoot()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null || psm.state.inventoryItems == null || psm.state.inventoryItems.Count == 0)
            return;

        InventoryItemRecord dropped = null;
        for (int i = psm.state.inventoryItems.Count - 1; i >= 0; i--)
        {
            InventoryItemRecord item = psm.state.inventoryItems[i];
            if (item == null)
                continue;
            if (item.IsConsumable || item.IsEquippable)
            {
                dropped = item;
                break;
            }
        }

        if (dropped == null)
            return;

        YQInvestorWorldPickup.TrySpawnForPlayer(dropped, true);
    }

    private void BuildDeathUi()
    {
        GameObject canvasGo = new GameObject("DeathCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        _deathCanvas = canvasGo.GetComponent<Canvas>();
        _deathCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _deathCanvas.sortingOrder = 5300;
        _deathCanvas.enabled = false;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image dim = canvasGo.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.62f);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.SetParent(canvasGo.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(520f, 180f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.96f);
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(rt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(20f, 20f);
        textRt.offsetMax = new Vector2(-20f, -20f);
        _deathText = textGo.GetComponent<TextMeshProUGUI>();
        _deathText.fontSize = 28f;
        _deathText.alignment = TextAlignmentOptions.Center;
        _deathText.text = "You Died\n\nPress R or Enter to respawn at the hub.";
        _deathText.color = Color.white;
    }

    private float GetMaxHealth()
    {
        return GeneratedRpgContentService.Instance != null && PlayerStateManager.Instance != null
            ? GeneratedRpgContentService.Instance.GetDerivedMaxHealth(PlayerStateManager.Instance.state)
            : (PlayerStateManager.Instance != null ? Mathf.Max(1, PlayerStateManager.Instance.state.stats.maxHealth) : 100f);
    }

    private float GetMaxStamina()
    {
        return GeneratedRpgContentService.Instance != null && PlayerStateManager.Instance != null
            ? GeneratedRpgContentService.Instance.GetDerivedMaxStamina(PlayerStateManager.Instance.state)
            : (PlayerStateManager.Instance != null ? Mathf.Max(1, PlayerStateManager.Instance.state.stats.maxStamina) : 100f);
    }

    private float GetMaxMana()
    {
        return GeneratedRpgContentService.Instance != null && PlayerStateManager.Instance != null
            ? GeneratedRpgContentService.Instance.GetDerivedMaxMana(PlayerStateManager.Instance.state)
            : (PlayerStateManager.Instance != null ? Mathf.Max(1, PlayerStateManager.Instance.state.stats.maxMana) : 50f);
    }
}
