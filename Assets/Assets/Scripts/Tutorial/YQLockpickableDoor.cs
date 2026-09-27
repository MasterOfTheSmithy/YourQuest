using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQLockpickableDoor : MonoBehaviour
{
    public string displayName = "Locked Door";
    public string regionId = "origin_forest";
    public bool locked = true;
    [Range(0f, 1f)] public float lockDifficulty = 0.45f;
    public Vector3 openEuler = new Vector3(0f, 86f, 0f);

    private bool _opened;
    private Quaternion _closedRotation;
    private Collider[] _colliders;

    // note: Empty IDs preserve legacy doors; reviewed generated doors use the existing world save and retain their authored collision shape.
    [SerializeField] private string generatedDoorId = string.Empty;
    [SerializeField] private bool preserveAuthoredCollider;
    public string GeneratedDoorId => generatedDoorId;

    public void ConfigureGeneratedBinding(string doorId, string label, string locationRegionId,
        bool startsLocked, float difficulty, Vector3 reviewedOpenEuler)
    {
        if (gameObject.activeInHierarchy)
            throw new System.InvalidOperationException("Configure a generated door before activation.");
        generatedDoorId = doorId;
        preserveAuthoredCollider = true;
        displayName = label;
        regionId = locationRegionId;
        locked = startsLocked;
        lockDifficulty = difficulty;
        openEuler = reviewedOpenEuler;
    }

    private void Awake()
    {
        _closedRotation = transform.localRotation;
        if (preserveAuthoredCollider)
        {
            // note: Source doors may face local X rather than Z; axis-clamping their bounds made interaction colliders thicker or narrower than the actual leaf.
            _colliders = GetComponentsInChildren<Collider>(true);
        }
        else
        {
            BoxCollider box = YQInteractableColliderUtility.EnsureTightBox(
                gameObject,
                new Vector3(1.2f, 2.05f, 0.24f),
                new Vector3(0f, 1.02f, 0f),
                new Vector3(0.65f, 1.15f, 0.16f),
                new Vector3(2.05f, 2.25f, 0.48f));
            _colliders = box != null ? new Collider[] { box } : GetComponentsInChildren<Collider>(true);
        }
        if (YQCellDoorBindingsV2.WasOpened(WorldStateManager.Instance?.State, generatedDoorId))
            ApplyOpenPose();
    }

    public bool TryInteract(GameObject player)
    {
        if (_opened)
            return false;

        if (locked)
        {
            if (YQLockpickUi.TryBegin(this, player))
                return true;

            return ResolveImmediateLockpick(player);
        }

        return OpenUnlocked(player);
    }

    public bool CompleteLockpickFromUi(GameObject player, bool success)
    {
        if (_opened)
            return false;

        PlayerStateManager psm = PlayerStateManager.Instance;
        PlayerState state = psm != null ? psm.state : null;
        state?.EnsureCollections();

        if (!success)
        {
            state?.AddLedgerLine("The player's pick slipped inside " + displayName + ".");
            GeneratedRpgContentService.Instance?.SetInventoryMessage("Lockpick failed: " + displayName + ".");
            psm?.Save();
            return true;
        }

        locked = false;
        state?.IncCounter("lockpick:success", 1f);
        return OpenUnlocked(player, state, psm);
    }

    private bool ResolveImmediateLockpick(GameObject player)
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        PlayerState state = psm != null ? psm.state : null;
        state?.EnsureCollections();

        float finesse = state != null && state.behaviorCounters.TryGetValue("lockpick:attempt", out float attempts)
            ? Mathf.Min(0.25f, attempts * 0.02f)
            : 0f;
        float chance = lockDifficulty <= 0.12f ? 1f : Mathf.Clamp01(0.76f - lockDifficulty + finesse);
        state?.IncCounter("lockpick:attempt", 1f);
        if (Random.value > chance)
        {
            state?.AddLedgerLine("The player failed to pick " + displayName + ".");
            GeneratedRpgContentService.Instance?.SetInventoryMessage("Lockpick failed: " + displayName + ".");
            psm?.Save();
            return true;
        }

        locked = false;
        state?.IncCounter("lockpick:success", 1f);
        return OpenUnlocked(player, state, psm);
    }

    private bool OpenUnlocked(GameObject player)
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        PlayerState state = psm != null ? psm.state : null;
        state?.EnsureCollections();
        return OpenUnlocked(player, state, psm);
    }

    private bool OpenUnlocked(GameObject player, PlayerState state, PlayerStateManager psm)
    {
        if (_opened)
            return false;

        ApplyOpenPose();
        YQCellDoorBindingsV2.RecordOpened(WorldStateManager.Instance?.State, generatedDoorId);

        state?.AddLedgerLine("The player opened " + displayName + ".");
        state?.IncCounter("interact:door", 1f);
        YQRuntimeAudioFeedback.PlayChestOpen(transform.position);
        GeneratedRpgContentService.Instance?.SetInventoryMessage("Opened " + displayName + ".");
        psm?.Save();
        return true;
    }

    private void ApplyOpenPose()
    {
        // note: Restore streamed door state without replaying sounds, quest counters or lockpick rewards.
        _opened = true;
        locked = false;
        transform.localRotation = _closedRotation * Quaternion.Euler(openEuler);
        if (_colliders == null)
            return;
        for (int index = 0; index < _colliders.Length; index++)
            if (_colliders[index] != null)
                _colliders[index].enabled = false;
    }
}
