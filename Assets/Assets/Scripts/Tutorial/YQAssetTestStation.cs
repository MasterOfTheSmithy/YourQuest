using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class YQAssetTestStation : MonoBehaviour
{
    public string stationName = "Test Station";
    public float interactRadius = 4f;
    public float cooldownSeconds = 0.35f;
    public AudioSource audioSource;
    public GameObject vfxPrefab;
    public Transform vfxSpawnPoint;
    public float vfxLifetime = 5f;
    public float vfxScale = 1f;
    public Renderer statusRenderer;
    public Color idleColor = new Color(0.18f, 0.22f, 0.26f, 1f);
    public Color readyColor = new Color(0.32f, 0.58f, 0.82f, 1f);
    public Color activeColor = new Color(0.56f, 0.95f, 0.72f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform _player;
    private MaterialPropertyBlock _block;
    private float _nextPlayerSearchTime;
    private float _nextTriggerTime;
    private float _activeUntil;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (statusRenderer == null)
            statusRenderer = GetComponentInChildren<Renderer>();

        _block = new MaterialPropertyBlock();
        RefreshStatus(false);
    }

    private void Update()
    {
        if (RuntimeModalUiBlocker.IsBlocked || YQInvestorDialogueUI.IsOpenNow || YourQuestTutorialMenuUI.IsOpenNow)
        {
            RefreshStatus(false);
            return;
        }

        RefreshPlayer();
        bool inRange = _player != null && Vector3.Distance(_player.position, transform.position) <= interactRadius;
        RefreshStatus(inRange);

        if (CanTrigger(inRange))
            Trigger();
    }

    public void Trigger()
    {
        if (Time.time < _nextTriggerTime)
            return;

        _nextTriggerTime = Time.time + Mathf.Max(0.05f, cooldownSeconds);
        _activeUntil = Time.time + 0.45f;

        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Stop();
            audioSource.time = 0f;
            audioSource.Play();
        }

        if (vfxPrefab != null)
        {
            Transform spawn = vfxSpawnPoint != null ? vfxSpawnPoint : transform;
            GameObject instance = Instantiate(vfxPrefab, spawn.position, spawn.rotation);
            instance.name = stationName + "_VFX_Burst";
            instance.transform.localScale *= Mathf.Max(0.05f, vfxScale);
            RepairRuntimeMaterials(instance);
            Destroy(instance, Mathf.Max(0.5f, vfxLifetime));
        }
    }

    private bool CanTrigger(bool inRange)
    {
        Keyboard kb = Keyboard.current;
        return inRange && kb != null && kb.eKey.wasPressedThisFrame;
    }

    private void RefreshPlayer()
    {
        if (_player != null)
            return;

        if (Time.time < _nextPlayerSearchTime)
            return;

        _nextPlayerSearchTime = Time.time + 0.5f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _player = player.transform;
    }

    private void RefreshStatus(bool inRange)
    {
        if (statusRenderer == null)
            return;

        Color color = Time.time < _activeUntil ? activeColor : (inRange ? readyColor : idleColor);
        statusRenderer.GetPropertyBlock(_block);
        _block.SetColor(BaseColorId, color);
        _block.SetColor(ColorId, color);
        statusRenderer.SetPropertyBlock(_block);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = readyColor;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }

    private static void RepairRuntimeMaterials(GameObject root)
    {
        if (root == null)
            return;

        YQRuntimeUrpMaterialRepair.RepairHierarchy(root);
        YQVisualStabilityDirector.StabilizeHierarchy(root);
    }
}
