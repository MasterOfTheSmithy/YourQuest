using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class YQLockpickableLoot : MonoBehaviour
{
    private const string MimicSmallControllerPath =
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/Animator Controllers/MimicDemoSmall.controller";

    private const string ChestLidControllerPath =
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/Animator Controllers/Lid Controller.controller";

    private const string DefaultMimicPrefabPath =
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Mimics/MimicSimpleSmall.prefab";

    // ============================================================
    // AUTHORED / GENERATED CONFIG
    // ============================================================

    public string displayName =
        "Locked Chest";

    public string regionId =
        "origin_forest";

    public bool locked =
        true;

    public bool mimic;

    public string revealedMimicPrefabPath =
        DefaultMimicPrefabPath;

    [Range(0f, 1f)]
    public float lockDifficulty =
        0.35f;

    public int gold =
        18;

    [Header("Generated Loot Persistence")]

    [Tooltip(
        "Stable generated-world identity for this loot object. " +
        "Leave empty for legacy/authored non-persistent chests.")]
    public string persistentLootId =
        string.Empty;

    [Tooltip(
        "Optional generated reward level. " +
        "0 uses the player's current level.")]
    [Min(0)]
    public int rewardLevelOverride;

    // ============================================================
    // RUNTIME STATE
    // ============================================================

    private bool _opened;

    private Renderer _renderer;

    private Animator _animator;

    private GameObject _revealedMimicVisual;

    private bool _persistentStateApplied;

    // ============================================================
    // PERSISTENCE KEYS
    // ============================================================

    private string OpenedStateKey
    {
        get
        {
            return
                string.IsNullOrWhiteSpace(
                    persistentLootId)
                    ? string.Empty
                    : "loot:opened:" +
                      persistentLootId.Trim();
        }
    }

    private string MimicRevealedStateKey
    {
        get
        {
            return
                string.IsNullOrWhiteSpace(
                    persistentLootId)
                    ? string.Empty
                    : "loot:mimic_revealed:" +
                      persistentLootId.Trim();
        }
    }

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        _renderer =
            GetComponentInChildren<
                Renderer>();

        YQInteractableColliderUtility
            .EnsureTightBox(
                gameObject,
                new Vector3(
                    1.25f,
                    0.9f,
                    0.9f),
                new Vector3(
                    0f,
                    0.45f,
                    0f),
                new Vector3(
                    0.72f,
                    0.48f,
                    0.62f),
                new Vector3(
                    1.45f,
                    1.08f,
                    1.18f));

        /*
         * Inspector-authored persistentLootId values are available
         * during Awake.
         *
         * Dynamically generated chests receive their ID later through
         * ConfigureGeneratedLoot(), which runs this synchronization again.
         */
        SynchronizePersistentState();

        PrimeVisualState();

        if (_opened)
        {
            RestoreSpentVisualState();
        }
    }

    // ============================================================
    // GENERATED-WORLD CONFIGURATION
    // ============================================================

    /*
     * This is the single configuration entry point generated-world
     * materializers should use.
     *
     * Example:
     *
     * chest.ConfigureGeneratedLoot(
     *     persistentId,
     *     regionId,
     *     "Bandit Cache",
     *     35,
     *     true,
     *     0.42f,
     *     false,
     *     4);
     */
    public void ConfigureGeneratedLoot(
        string persistentId,
        string generatedRegionId,
        string generatedDisplayName,
        int generatedGold,
        bool generatedLocked,
        float generatedLockDifficulty,
        bool generatedMimic,
        int generatedRewardLevel = 0)
    {
        persistentLootId =
            SafeText(
                persistentId,
                string.Empty);

        regionId =
            SafeText(
                generatedRegionId,
                regionId);

        displayName =
            SafeText(
                generatedDisplayName,
                displayName);

        gold =
            Mathf.Max(
                0,
                generatedGold);

        locked =
            generatedLocked;

        lockDifficulty =
            Mathf.Clamp01(
                generatedLockDifficulty);

        mimic =
            generatedMimic;

        rewardLevelOverride =
            Mathf.Max(
                0,
                generatedRewardLevel);

        /*
         * Awake has already run for an instantiated runtime prefab.
         *
         * The persistent identity therefore becomes authoritative here.
         */
        _persistentStateApplied =
            false;

        SynchronizePersistentState();

        PrimeVisualState();

        if (_opened)
        {
            RestoreSpentVisualState();
        }
    }

    // ============================================================
    // VISUAL STATE
    // ============================================================

    public void PrimeVisualState()
    {
        _renderer =
            _renderer != null
                ? _renderer
                : GetComponentInChildren<
                    Renderer>();

        ConfigureAnimator();
    }

    private void RestoreSpentVisualState()
    {
        /*
         * A previously consumed generated chest remains consumed.
         *
         * We intentionally do not award loot, play sounds, increment
         * counters or wake another mimic here.
         */
        if (mimic)
        {
            HideChestShellRenderers();

            DisableInteractionColliders();

            return;
        }

        ApplyOpenedChestTint();

        /*
         * Attempt to put an animated chest into/open toward its opened
         * visual state. Failure is harmless; persistence still works.
         */
        TriggerFirstAnimator(
            "openLid",
            "open");

        PlayAnimatorState(
            "open",
            "opened",
            "idle open");
    }

    // ============================================================
    // INTERACTION
    // ============================================================

    public bool TryInteract(
        GameObject player)
    {
        SynchronizePersistentState();

        if (_opened)
            return false;

        PlayerStateManager psm =
            PlayerStateManager.Instance;

        PlayerState state =
            psm != null
                ? psm.state
                : null;

        if (state != null)
        {
            state.EnsureCollections();
        }

        if (mimic)
        {
            WakeMimic(
                player);

            return true;
        }

        if (locked)
        {
            if (YQLockpickUi.TryBegin(
                    this,
                    player))
            {
                return true;
            }

            return
                ResolveImmediateLockpick(
                    player);
        }

        OpenChest(
            state,
            psm);

        return true;
    }

    public bool CompleteLockpickFromUi(
        GameObject player,
        bool success)
    {
        SynchronizePersistentState();

        if (_opened)
            return false;

        if (mimic)
        {
            WakeMimic(
                player);

            return true;
        }

        PlayerStateManager psm =
            PlayerStateManager.Instance;

        PlayerState state =
            psm != null
                ? psm.state
                : null;

        state?.EnsureCollections();

        if (!success)
        {
            state?.AddLedgerLine(
                "The player's pick slipped inside " +
                displayName +
                ".");

            GeneratedRpgContentService.Instance
                ?.SetInventoryMessage(
                    "Lockpick failed: " +
                    displayName +
                    ".");

            psm?.Save();

            return true;
        }

        locked =
            false;

        state?.IncCounter(
            "lockpick:success",
            1f);

        OpenChest(
            state,
            psm);

        return true;
    }

    // ============================================================
    // LOCKPICK
    // ============================================================

    private bool ResolveImmediateLockpick(
        GameObject player)
    {
        PlayerStateManager psm =
            PlayerStateManager.Instance;

        PlayerState state =
            psm != null
                ? psm.state
                : null;

        state?.EnsureCollections();

        float finesse =
            state != null &&
            state.behaviorCounters.TryGetValue(
                "lockpick:attempt",
                out float attempts)
                ? Mathf.Min(
                    0.2f,
                    attempts *
                    0.025f)
                : 0f;

        float chance =
            lockDifficulty <=
            0.12f
                ? 1f
                : Mathf.Clamp01(
                    0.72f -
                    lockDifficulty +
                    finesse);

        state?.IncCounter(
            "lockpick:attempt",
            1f);

        if (UnityEngine.Random.value >
            chance)
        {
            state?.AddLedgerLine(
                "The player failed to pick " +
                displayName +
                ".");

            GeneratedRpgContentService.Instance
                ?.SetInventoryMessage(
                    "Lockpick failed: " +
                    displayName +
                    ".");

            psm?.Save();

            return true;
        }

        locked =
            false;

        state?.IncCounter(
            "lockpick:success",
            1f);

        OpenChest(
            state,
            psm);

        return true;
    }
    
    // ============================================================
    // OPEN CHEST
    // ============================================================

    private void OpenChest(
        PlayerState state,
        PlayerStateManager psm)
    {
        if (_opened)
            return;

        /*
         * Mark first so even another interaction in this frame cannot
         * duplicate rewards.
         */
        _opened =
            true;

        MarkPersistentOpened(
            state);

        TriggerFirstAnimator(
            "openLid",
            "open");

        YQRuntimeAudioFeedback
            .PlayChestOpen(
                transform.position);

        int rewardLevel =
            ResolveRewardLevel(
                state);

        string rewardSeed =
            BuildRewardSeed();

        InventoryItemRecord item =
            GeneratedRpgContentService.Instance !=
            null
                ? GeneratedRpgContentService
                    .Instance
                    .GenerateItem(
                        rewardSeed,
                        rewardLevel,
                        null,
                        false)
                : null;

        if (state != null)
        {
            if (item != null)
            {
                state.AddOrUpdateItem(
                    item);
            }

            state.currency +=
                Mathf.Max(
                    0,
                    gold);

            state.IncCounter(
                "loot:chest",
                1f);

            state.AddLedgerLine(
                "The player opened " +
                displayName +
                ".");

            psm?.Save();
        }

        GeneratedRpgContentService.Instance
            ?.SetInventoryMessage(
                item != null
                    ? "Opened " +
                      displayName +
                      ": " +
                      item.displayName +
                      " and " +
                      gold +
                      " gold."
                    : gold > 0
                        ? "Opened " +
                          displayName +
                          ": " +
                          gold +
                          " gold."
                        : "Opened " +
                          displayName +
                          ".");

        ApplyOpenedChestTint();

        YQGeneratedRuntimeVfx
            .SpawnConsumableUse(
                transform,
                item);
    }

    private int ResolveRewardLevel(
        PlayerState state)
    {
        if (rewardLevelOverride >
            0)
        {
            return
                rewardLevelOverride;
        }

        return
            state != null
                ? Mathf.Max(
                    1,
                    state.level)
                : 1;
    }

    private string BuildRewardSeed()
    {
        if (!string.IsNullOrWhiteSpace(
                persistentLootId))
        {
            /*
             * Stable generated reward seed.
             *
             * Opening the same generated chest can never silently switch
             * to another item-generation identity between sessions.
             */
            return
                "generated_loot:" +
                persistentLootId.Trim() +
                ":reward";
        }

        /*
         * Preserve legacy authored chest behavior when no stable generated
         * identity has been assigned.
         */
        return
            regionId +
            ":" +
            displayName +
            ":chest";
    }

    // ============================================================
    // MIMIC
    // ============================================================

    private void WakeMimic(
        GameObject player)
    {
        if (_opened)
            return;

        _opened =
            true;

        PlayerStateManager psm =
            PlayerStateManager.Instance;

        PlayerState state =
            psm != null
                ? psm.state
                : null;

        state?.EnsureCollections();

        MarkPersistentOpened(
            state);

        MarkPersistentMimicRevealed(
            state);

        RevealMimicVisual();

        PrimeVisualState();

        PlayMimicWakeAnimation();

        YQRuntimeAudioFeedback
            .PlayMimicReveal(
                transform.position);

        GeneratedRpgContentService.Instance
            ?.SetInventoryMessage(
                displayName +
                " was a mimic.");

        if (state != null)
        {
            state.IncCounter(
                "mimic:revealed",
                1f);

            state.AddLedgerLine(
                "The player woke a mimic hidden inside " +
                displayName +
                ".");

            /*
             * Generated-world mimic revelation is persistent state.
             * Save it regardless of the autosave preference because
             * otherwise a manual world rebuild could recreate the chest.
             */
            if (!string.IsNullOrWhiteSpace(
                    persistentLootId))
            {
                psm?.Save();
            }
            else if (psm != null &&
                     psm.autosave)
            {
                psm.Save();
            }
        }

        EntityInfo info =
            gameObject.GetComponent<
                EntityInfo>();

        if (info == null)
        {
            info =
                gameObject.AddComponent<
                    EntityInfo>();
        }

        info.entityId =
            BuildMimicEntityId();

        info.displayName =
            displayName +
            " Mimic";

        info.hostility =
            Hostility.Hostile;

        info.factionId =
            "mimics";

        info.tags =
            new[]
            {
                "generated",
                "enemy",
                "mimic",
                "chest",
                NormalizeTag(
                    regionId),
                NormalizeTag(
                    persistentLootId)
            };

        Rigidbody rb =
            gameObject.GetComponent<
                Rigidbody>();

        if (rb == null)
        {
            rb =
                gameObject.AddComponent<
                    Rigidbody>();
        }

        /*
 * The chest can be kinematic while functioning as loot.
 * Once revealed as an enemy, make the ROOT Rigidbody dynamic
 * before touching velocity.
 */
        rb.isKinematic =
            false;

        rb.useGravity =
            false;

        rb.constraints =
            RigidbodyConstraints
                .FreezeRotation;

        rb.linearVelocity =
            Vector3.zero;

        rb.angularVelocity =
            Vector3.zero;

        if (gameObject.GetComponent<
                CapsuleCollider>() ==
            null)
        {
            CapsuleCollider capsule =
                gameObject.AddComponent<
                    CapsuleCollider>();

            capsule.height =
                1.35f;

            capsule.radius =
                0.58f;

            capsule.center =
                new Vector3(
                    0f,
                    0.7f,
                    0f);
        }

        YQInvestorEnemy enemy =
            gameObject.GetComponent<
                YQInvestorEnemy>();

        if (enemy == null)
        {
            enemy =
                gameObject.AddComponent<
                    YQInvestorEnemy>();
        }

        enemy.semanticRegionId =
            regionId;

        enemy.factionId =
            "mimics";

        enemy.displayName =
            displayName +
            " Mimic";

        int rewardLevel =
            ResolveRewardLevel(
                state);

        enemy.maxHealth =
            60f +
            rewardLevel *
            5f;

        enemy.moveSpeed =
            2.8f;

        enemy.attackDamage =
            10 +
            Mathf.Clamp(
                rewardLevel,
                1,
                12);

        enemy.goldDrop =
            Mathf.Max(
                8,
                gold);

        enemy.rarity =
    rewardLevel >= 6
        ? "rare"
        : "uncommon";

        /*
         * The Magic Pig mimic prefab IS the enemy visual.
         *
         * Never allow YQInvestorEnemy to generate its generic echo/wisp
         * presentation over the actual mimic.
         */
        enemy.useWispVisual =
            false;

        enemy.Initialize(
            null);

    }
    private string BuildMimicEntityId()
    {
        if (!string.IsNullOrWhiteSpace(
                persistentLootId))
        {
            return
                "generated_mimic_" +
                StableHash32(
                    persistentLootId)
                    .ToString("x8");
        }

        /*
         * Legacy authored chests did not possess a stable world identity.
         * Preserve their old runtime-only behavior.
         */
        return
            regionId +
            "_mimic_" +
            GetInstanceID();
    }

    // ============================================================
    // PERSISTENT STATE
    // ============================================================

    private void SynchronizePersistentState()
    {
        if (_persistentStateApplied &&
            _opened)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
                persistentLootId))
        {
            return;
        }

        PlayerStateManager psm =
            PlayerStateManager.Instance;

        PlayerState state =
            psm != null
                ? psm.state
                : null;

        if (state == null)
            return;

        state.EnsureCollections();

        _persistentStateApplied =
            true;

        if (HasPersistentCounter(
                state,
                OpenedStateKey))
        {
            _opened =
                true;
        }
    }

    private static bool HasPersistentCounter(
        PlayerState state,
        string key)
    {
        if (state == null ||
            string.IsNullOrWhiteSpace(
                key))
        {
            return false;
        }

        state.EnsureCollections();

        return
            state.behaviorCounters
                .TryGetValue(
                    key,
                    out float value) &&
            value >
            0.5f;
    }

    private void MarkPersistentOpened(
        PlayerState state)
    {
        if (state == null ||
            string.IsNullOrWhiteSpace(
                persistentLootId))
        {
            return;
        }

        SetPersistentCounterOnce(
            state,
            OpenedStateKey);
    }

    private void MarkPersistentMimicRevealed(
        PlayerState state)
    {
        if (state == null ||
            string.IsNullOrWhiteSpace(
                persistentLootId))
        {
            return;
        }

        SetPersistentCounterOnce(
            state,
            MimicRevealedStateKey);
    }

    private static void SetPersistentCounterOnce(
        PlayerState state,
        string key)
    {
        if (state == null ||
            string.IsNullOrWhiteSpace(
                key))
        {
            return;
        }

        state.EnsureCollections();

        if (state.behaviorCounters
                .TryGetValue(
                    key,
                    out float existing) &&
            existing >
            0.5f)
        {
            return;
        }

        state.IncCounter(
            key,
            1f);
    }

    // ============================================================
    // MIMIC VISUAL
    // ============================================================
    private void HideChestShellRenderers()
    {
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>(
                true);

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer renderer =
                renderers[i];

            if (renderer == null)
                continue;

            /*
             * Do not hide the newly spawned actual mimic visual.
             */
            if (_revealedMimicVisual != null &&
                renderer.transform.IsChildOf(
                    _revealedMimicVisual.transform))
            {
                continue;
            }

            renderer.enabled =
                false;
        }
    }
    private void RevealMimicVisual()
    {
        if (!mimic ||
            _revealedMimicVisual != null)
        {
            return;
        }

        GameObject prefab =
            ResolveMimicPrefab();

        if (prefab == null)
        {
            Debug.LogWarning(
                "[YQLockpickableLoot] " +
                "Could not resolve mimic prefab: " +
                revealedMimicPrefabPath);

            return;
        }

        HideChestShellRenderers();

        _revealedMimicVisual =
            Instantiate(
                prefab,
                transform);

        _revealedMimicVisual.name =
            "Revealed_MimicVisual";

        _revealedMimicVisual.transform
            .localPosition =
            Vector3.zero;

        _revealedMimicVisual.transform
            .localRotation =
            Quaternion.identity;

        _revealedMimicVisual.transform
            .localScale =
            Vector3.one;

        Rigidbody[] bodies =
            _revealedMimicVisual
                .GetComponentsInChildren<
                    Rigidbody>(
                        true);

        for (int i = 0;
             i < bodies.Length;
             i++)
        {
            if (bodies[i] != null)
            {
                /*
                 * Child mimic/ragdoll bodies are presentation only.
                 * Do not write velocity to an already-kinematic body.
                 */
                if (!bodies[i].isKinematic)
                {
                    bodies[i].linearVelocity =
                        Vector3.zero;

                    bodies[i].angularVelocity =
                        Vector3.zero;
                }

                bodies[i].useGravity =
                    false;

                bodies[i].isKinematic =
                    true;
            }
        }

        Collider[] colliders =
            _revealedMimicVisual
                .GetComponentsInChildren<
                    Collider>(
                        true);

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled =
                    false;
            }
        }

        NormalizeMimicVisual(
            _revealedMimicVisual,
            1.6f,
            1.85f);

        YQRuntimeUrpMaterialRepair
            .RepairHierarchy(
                _revealedMimicVisual);

        _animator =
            ResolveAnimator();
    }

    private GameObject ResolveMimicPrefab()
    {
        YQRuntimeWorldAssetRegistry registry =
            YQRuntimeWorldAssetRegistry.Instance;

        if (registry != null)
        {
            if (YQRuntimeCreatureAssetIndex
                .TryResolveMimic(
                    registry,
                    SafeText(
                        persistentLootId,
                        displayName),
                    out YQRuntimeWorldAssetEntry entry) &&
                entry != null &&
                entry.prefab != null)
            {
                Debug.Log(
                    "[YQLockpickableLoot] " +
                    "Using actual mimic prefab: " +
                    entry.assetPath);

                return entry.prefab;
            }

            /*
             * Exact-path fallback.
             */
            if (!string.IsNullOrWhiteSpace(
                    revealedMimicPrefabPath))
            {
                GameObject exact =
                    registry.ResolvePrefab(
                        revealedMimicPrefabPath);

                if (exact != null)
                    return exact;
            }
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(
                revealedMimicPrefabPath))
        {
            GameObject editorPrefab =
                AssetDatabase.LoadAssetAtPath<
                    GameObject>(
                        revealedMimicPrefabPath);

            if (editorPrefab != null)
                return editorPrefab;
        }
#endif

        Debug.LogError(
            "[YQLockpickableLoot] " +
            "ACTUAL MIMIC PREFAB NOT FOUND: " +
            revealedMimicPrefabPath);

        return null;
    }

    private void DisableInteractionColliders()
    {
        Collider[] colliders =
            GetComponentsInChildren<
                Collider>(
                    true);

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            Collider collider =
                colliders[i];

            if (collider == null ||
                collider.isTrigger)
            {
                continue;
            }

            collider.enabled =
                false;
        }
    }

    private static void NormalizeMimicVisual(
        GameObject visual,
        float maxFootprint,
        float maxHeight)
    {
        if (visual == null ||
            !TryGetRendererBounds(
                visual,
                out Bounds bounds))
        {
            return;
        }

        float width =
            Mathf.Max(
                bounds.size.x,
                bounds.size.z);

        float height =
            Mathf.Max(
                0.01f,
                bounds.size.y);

        float scaleFactor =
            Mathf.Min(
                1f,
                maxFootprint /
                    Mathf.Max(
                        0.01f,
                        width),
                maxHeight /
                    height);

        if (scaleFactor <
            0.999f)
        {
            visual.transform.localScale *=
                scaleFactor;

            if (!TryGetRendererBounds(
                    visual,
                    out bounds))
            {
                return;
            }
        }

        Vector3 position =
            visual.transform.position;

        position.x -=
            bounds.center.x -
            visual.transform.position.x;

        position.y -=
            bounds.min.y -
            visual.transform.position.y;

        position.z -=
            bounds.center.z -
            visual.transform.position.z;

        visual.transform.position =
            position;
    }

    private static bool TryGetRendererBounds(
        GameObject root,
        out Bounds bounds)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<
                Renderer>(
                    true);

        bounds =
            default;

        bool initialized =
            false;

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer renderer =
                renderers[i];

            if (renderer == null ||
                renderer is
                    ParticleSystemRenderer)
            {
                continue;
            }

            if (!initialized)
            {
                bounds =
                    renderer.bounds;

                initialized =
                    true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds);
            }
        }

        return initialized;
    }

    // ============================================================
    // OPEN CHEST VISUAL
    // ============================================================

    private void ApplyOpenedChestTint()
    {
        _renderer =
            _renderer != null
                ? _renderer
                : GetComponentInChildren<
                    Renderer>();

        if (_renderer == null)
            return;

        MaterialPropertyBlock block =
            new MaterialPropertyBlock();

        _renderer.GetPropertyBlock(
            block);

        block.SetColor(
            "_BaseColor",
            new Color(
                0.34f,
                0.26f,
                0.16f,
                1f));

        block.SetColor(
            "_Color",
            new Color(
                0.34f,
                0.26f,
                0.16f,
                1f));

        _renderer.SetPropertyBlock(
            block);
    }

    // ============================================================
    // ANIMATOR
    // ============================================================

    private void ConfigureAnimator()
    {
        _animator =
            ResolveAnimator();

        if (_animator == null)
            return;

        /*
         * Imported prefab animation is authoritative.
         *
         * Only supply our fallback controller when the prefab itself
         * has no controller.
         */
        if (_animator.runtimeAnimatorController ==
            null)
        {
            RuntimeAnimatorController controller =
                ResolveController(
                    mimic &&
                    _opened
                        ? MimicSmallControllerPath
                        : ChestLidControllerPath);

            if (controller != null)
            {
                _animator.runtimeAnimatorController =
                    controller;
            }
        }

        _animator.applyRootMotion =
            false;

        _animator.cullingMode =
            AnimatorCullingMode
                .AlwaysAnimate;

        _animator.enabled =
            _animator.runtimeAnimatorController !=
            null;
    }

    private RuntimeAnimatorController ResolveController(
        string assetPath)
    {
        /*
         * AnimatorController assets are not currently exposed through
         * YQRuntimeWorldAssetRegistry, so retain the existing editor path.
         *
         * Imported chest prefabs with their own controller continue to work
         * normally in player builds because we never erase that controller.
         */
#if UNITY_EDITOR
        RuntimeAnimatorController editorController =
            LoadController(
                assetPath);

        if (editorController != null)
        {
            return editorController;
        }
#endif

        return
            _animator != null
                ? _animator
                    .runtimeAnimatorController
                : null;
    }

    private Animator ResolveAnimator()
    {
        if (_revealedMimicVisual != null)
        {
            Animator mimicAnimator =
                ResolveAnimatorFrom(
                    _revealedMimicVisual);

            if (mimicAnimator != null)
            {
                return mimicAnimator;
            }
        }

        return
            ResolveAnimatorFrom(
                gameObject);
    }

    private static Animator ResolveAnimatorFrom(
        GameObject root)
    {
        if (root == null)
            return null;

        Animator[] animators =
            root.GetComponentsInChildren<
                Animator>(
                    true);

        Animator first =
            null;

        Animator firstWithController =
            null;

        for (int i = 0;
             i < animators.Length;
             i++)
        {
            Animator animator =
                animators[i];

            if (animator == null)
                continue;

            first ??=
                animator;

            if (firstWithController ==
                    null &&
                animator
                    .runtimeAnimatorController !=
                null)
            {
                firstWithController =
                    animator;
            }
        }

        return
            firstWithController != null
                ? firstWithController
                : first;
    }

    private void PlayMimicWakeAnimation()
    {
        if (_animator == null)
            return;

        /*
         * Keep the actual mimic prefab controller.
         *
         * Only use MimicDemoSmall as a fallback.
         */
        if (_animator.runtimeAnimatorController ==
            null)
        {
            RuntimeAnimatorController controller =
                ResolveController(
                    MimicSmallControllerPath);

            if (controller != null)
            {
                _animator.runtimeAnimatorController =
                    controller;
            }
        }

        _animator.applyRootMotion =
            false;

        _animator.cullingMode =
            AnimatorCullingMode
                .AlwaysAnimate;

        _animator.enabled =
            _animator
                .runtimeAnimatorController !=
            null;

        if (!_animator.enabled)
            return;

        _animator.speed =
            1f;

        _animator.Rebind();

        _animator.Update(
            0f);

        SetAnimatorFloat(
            "locomotion",
            0f);

        TriggerFirstAnimator(
            "open",
            "idleBreak");

        if (!PlayAnimatorState(
                "open",
                "idle break"))
        {
            _animator.Update(
                0.02f);
        }
    }

    private bool PlayAnimatorState(
        params string[] stateNames)
    {
        if (_animator == null ||
            !_animator.isActiveAndEnabled ||
            _animator.runtimeAnimatorController ==
                null ||
            stateNames == null)
        {
            return false;
        }

        const int layer =
            0;

        for (int i = 0;
             i < stateNames.Length;
             i++)
        {
            string stateName =
                stateNames[i];

            if (string.IsNullOrWhiteSpace(
                    stateName))
            {
                continue;
            }

            int shortHash =
                Animator.StringToHash(
                    stateName);

            if (_animator.HasState(
                    layer,
                    shortHash))
            {
                _animator.Play(
                    shortHash,
                    layer,
                    0f);

                _animator.Update(
                    0.02f);

                return true;
            }

            int fullHash =
                Animator.StringToHash(
                    "Base Layer." +
                    stateName);

            if (_animator.HasState(
                    layer,
                    fullHash))
            {
                _animator.Play(
                    fullHash,
                    layer,
                    0f);

                _animator.Update(
                    0.02f);

                return true;
            }
        }

        return false;
    }

    private void SetAnimatorFloat(
        string parameterName,
        float value)
    {
        if (_animator == null ||
            !_animator.isActiveAndEnabled ||
            _animator.runtimeAnimatorController ==
                null)
        {
            return;
        }

        if (!HasAnimatorParameter(
                parameterName,
                AnimatorControllerParameterType
                    .Float))
        {
            return;
        }

        _animator.SetFloat(
            parameterName,
            value);
    }

    private void TriggerFirstAnimator(
        params string[] parameterNames)
    {
        if (_animator == null ||
            !_animator.isActiveAndEnabled ||
            _animator.runtimeAnimatorController ==
                null ||
            parameterNames == null)
        {
            return;
        }

        for (int i = 0;
             i < parameterNames.Length;
             i++)
        {
            string parameterName =
                parameterNames[i];

            if (!HasAnimatorParameter(
                    parameterName,
                    AnimatorControllerParameterType
                        .Trigger))
            {
                continue;
            }

            _animator.SetTrigger(
                parameterName);

            return;
        }
    }

    private bool HasAnimatorParameter(
        string parameterName,
        AnimatorControllerParameterType type)
    {
        if (_animator == null ||
            string.IsNullOrWhiteSpace(
                parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters =
            _animator.parameters;

        for (int i = 0;
             i < parameters.Length;
             i++)
        {
            AnimatorControllerParameter parameter =
                parameters[i];

            if (parameter != null &&
                parameter.type ==
                    type &&
                string.Equals(
                    parameter.name,
                    parameterName,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private static RuntimeAnimatorController LoadController(
        string path)
    {
        return
            string.IsNullOrWhiteSpace(
                path)
                ? null
                : AssetDatabase
                    .LoadAssetAtPath<
                        RuntimeAnimatorController>(
                            path);
    }
#endif

    // ============================================================
    // DETERMINISM
    // ============================================================

    private static uint StableHash32(
        string value)
    {
        const uint offsetBasis =
            2166136261u;

        const uint prime =
            16777619u;

        uint hash =
            offsetBasis;

        if (value == null)
            return hash;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                value[i];

            hash ^=
                (byte)(
                    c &
                    0xFF);

            hash *=
                prime;

            hash ^=
                (byte)(
                    (c >> 8) &
                    0xFF);

            hash *=
                prime;
        }

        return hash;
    }

    // ============================================================
    // STRINGS
    // ============================================================

    private static string SafeText(
        string value,
        string fallback)
    {
        return
            string.IsNullOrWhiteSpace(
                value)
                ? fallback
                : value.Trim();
    }

    private static string NormalizeTag(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        char[] chars =
            value
                .Trim()
                .ToLowerInvariant()
                .ToCharArray();

        for (int i = 0;
             i < chars.Length;
             i++)
        {
            if (!char.IsLetterOrDigit(
                    chars[i]))
            {
                chars[i] =
                    '_';
            }
        }

        return
            new string(
                chars)
                .Trim('_');
    }
}