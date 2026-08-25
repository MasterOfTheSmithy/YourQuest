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

    private void Awake()
    {
        _closedRotation = transform.localRotation;
        BoxCollider box = YQInteractableColliderUtility.EnsureTightBox(
            gameObject,
            new Vector3(1.2f, 2.05f, 0.24f),
            new Vector3(0f, 1.02f, 0f),
            new Vector3(0.65f, 1.15f, 0.16f),
            new Vector3(2.05f, 2.25f, 0.48f));
        _colliders = box != null ? new Collider[] { box } : GetComponentsInChildren<Collider>(true);
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

        _opened = true;
        transform.localRotation = _closedRotation * Quaternion.Euler(openEuler);
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
                _colliders[i].enabled = false;
        }

        state?.AddLedgerLine("The player opened " + displayName + ".");
        state?.IncCounter("interact:door", 1f);
        YQRuntimeAudioFeedback.PlayChestOpen(transform.position);
        GeneratedRpgContentService.Instance?.SetInventoryMessage("Opened " + displayName + ".");
        psm?.Save();
        return true;
    }
}
