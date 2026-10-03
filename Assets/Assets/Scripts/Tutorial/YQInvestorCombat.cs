// Assets/Assets/Scripts/Tutorial/YQInvestorCombat.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
// note: Read this frame's movement/camera pose, then deliver animation feedback before the visual driver's Update (150).
[DefaultExecutionOrder(100)]
public sealed class YQInvestorCombat : MonoBehaviour
{
    public float attackRange = 2.1f;
    public float attackRadius = 1.15f;
    public int lightAttackDamage = 20;
    public float attackCooldown = 0.28f;
    [Range(0f, 0.2f)] public float meleeInputBuffer = 0.12f;
    public float spellCooldown = 0.65f;
    public float interactRange = 2.55f;
    [Range(0.02f, 0.3f)] public float interactAimRadius = 0.14f;

    private readonly Collider[] _attackHits = new Collider[24];
    // note: Bounded reusable physics buffers keep melee obstruction checks allocation-free after setup.
    private readonly RaycastHit[] _meleeOcclusionHits = new RaycastHit[32];
    private readonly Collider[] _meleeOriginHits = new Collider[16];
    private readonly Collider[] _pulseHits = new Collider[32];
    private readonly RaycastHit[] _interactHits = new RaycastHit[16];
    private readonly HashSet<int> _damagedEnemies = new HashSet<int>();

    private float _nextAttackTime;
    private float _queuedAttackUntil = float.NegativeInfinity;
    private YQInvestorPlayerMotor _motor;
    private float _nextSpellTime;
    private YQInvestorVitals _vitals;
    private ActionRecorder _recorder;
    private YQPlayerEquipmentVisual _equipmentVisual;
    private Camera _viewCamera;
    private GeneratedRpgContentService _content;
    // note: One pending cast belongs to this player and the captured profile; no coroutine can outlive that ownership.
    private SkillRecord _castingSpell;
    private PlayerState _castingOwner;
    private YQSpellCircleVfx _castingCircles;
    private string _castingDescriptor;
    private string _castingResource;
    private float _castingResourceCost;
    private float _castStartedAt;
    private float _castDuration;
    private float _castCooldown;
    private int _castPower;

    public bool IsCasting => _castingSpell != null;
    public int CastingCircle => IsCasting ? YQSpellCircleRules.GetCircle(_castingSpell) : 0;
    public float CastDurationSeconds => IsCasting ? _castDuration : 0f;
    public float CastProgress => IsCasting ? Mathf.Clamp01((Time.time - _castStartedAt) / _castDuration) : 0f;
    // note: HUD availability reads this executor's real clocks; presentation never simulates a competing cooldown.
    public float AttackCooldownRemaining => Mathf.Max(0f, _nextAttackTime - Time.time);
    public float SpellCooldownRemaining => Mathf.Max(0f, _nextSpellTime - Time.time);

    private void Awake()
    {
        _motor = GetComponent<YQInvestorPlayerMotor>();
        _vitals = GetComponent<YQInvestorVitals>();
        _recorder = GetComponent<ActionRecorder>();
        _equipmentVisual = GetComponent<YQPlayerEquipmentVisual>();
        _viewCamera = Camera.main;
        _content = GeneratedRpgContentService.Instance;
    }

    private void Update()
    {
        // note: Bootstrap can add the motor after combat's Awake; resolve it lazily once, then reuse the authoritative controller.
        if (_motor == null)
            _motor = GetComponent<YQInvestorPlayerMotor>();
        if ((_motor != null && !_motor.IsAuthoritative) ||
            RuntimeModalUiBlocker.IsBlocked ||
            // note: A realtime cursor menu consumes clicks without acquiring the world's full-pause modal token.
            YourQuestTutorialMenuUI.CapturesPointerInput ||
            YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked ||
            (_vitals != null && _vitals.IsDead))
        {
            // note: Neither loading nor modal UI can queue an attack that fires when gameplay unlocks.
            _queuedAttackUntil = float.NegativeInfinity;
            CancelSpellCast();
            return;
        }

        AdvanceSpellCast();

        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            _queuedAttackUntil = Time.time + Mathf.Clamp(meleeInputBuffer, 0f, 0.2f);
        // note: Preserve at most one short-lived press near cooldown completion; holding the button does not create automatic attacks.
        if (Time.time <= _queuedAttackUntil && Time.time >= _nextAttackTime)
        {
            _queuedAttackUntil = float.NegativeInfinity;
            TryAttack();
        }
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            TryCastPulse();
        if (kb != null && kb.eKey.wasPressedThisFrame)
            TryInteract();
    }

    private void OnDisable()
    {
        // note: Disabling/re-enabling this player must never replay pending combat input.
        _queuedAttackUntil = float.NegativeInfinity;
        _damagedEnemies.Clear();
        CancelSpellCast();
    }

    private void TryAttack()
    {
        if (IsCasting || Time.time < _nextAttackTime)
            return;

        _nextAttackTime = Time.time + attackCooldown;
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        int equipmentBonus = Content != null ? Content.GetAttackBonus(state) : 0;
        int damage = lightAttackDamage + equipmentBonus + (state != null ? Mathf.Max(0, state.stats.attack / 4) : 0);

        // note: Measure reach from the player, not from a sphere already placed an entire weapon range ahead.
        Vector3 origin = transform.position + Vector3.up * (_motor != null && _motor.IsCrouching ? 0.75f : 1.1f);
        Camera aimCamera = _motor != null ? _motor.playerCamera : _viewCamera;
        Vector3 aimDirection = aimCamera != null ? aimCamera.transform.forward : transform.forward;
        float reach = Mathf.Max(0.1f, attackRange);
        float width = Mathf.Max(0.01f, attackRadius);
        int hitBufferCount = IsMeleeOriginClear(origin)
            ? Physics.OverlapSphereNonAlloc(origin, reach, _attackHits, ~0, QueryTriggerInteraction.Ignore)
            : 0;
        GameObject firstTarget = null;
        float nearestTargetDistanceSquared = float.PositiveInfinity;
        int hitCount = 0;
        // note: Imported characters often have several colliders; one swing applies one damage event per enemy, not per collider.
        _damagedEnemies.Clear();
        for (int i = 0; i < hitBufferCount; i++)
        {
            Collider c = _attackHits[i];
            if (c == null || IsOwnCollider(c))
                continue;
            YQInvestorEnemy enemy = c.GetComponentInParent<YQInvestorEnemy>();
            if (enemy == null || _damagedEnemies.Contains(enemy.GetInstanceID()))
                continue;

            // note: Collider surfaces, rather than pivots, determine reach for both large and close enemies.
            Vector3 targetPoint = c.ClosestPoint(origin);
            Vector3 toTarget = targetPoint - origin;
            float distanceSquared = toTarget.sqrMagnitude;
            float forwardDistance = Vector3.Dot(toTarget, aimDirection);
            float sidewaysSquared = Mathf.Max(0f, distanceSquared - forwardDistance * forwardDistance);
            if (distanceSquared > reach * reach || forwardDistance < 0f || sidewaysSquared > width * width ||
                !HasClearMeleePath(origin, targetPoint, enemy))
                continue;

            // note: Deduplicate only accepted hits; an occluded limb must not reject another exposed collider on the same enemy.
            _damagedEnemies.Add(enemy.GetInstanceID());
            if (distanceSquared < nearestTargetDistanceSquared)
            {
                nearestTargetDistanceSquared = distanceSquared;
                firstTarget = enemy.gameObject;
            }
            enemy.ReceiveHit(damage, gameObject);
            hitCount++;
        }

        _recorder?.RecordCombat(firstTarget);
        if (_equipmentVisual == null)
            _equipmentVisual = GetComponent<YQPlayerEquipmentVisual>();
        _equipmentVisual?.PlayMeleeFeedback();
        YQRuntimeAudioFeedback.PlayPlayerMelee(transform.position + transform.forward * 1.1f + Vector3.up * 1.1f, hitCount > 0);
        YQGeneratedRuntimeVfx.SpawnMeleeSwing(transform, BuildMeleeDescriptor(state), hitCount > 0);
        if (state != null)
        {
            state.AddLedgerLine(hitCount > 0 ? "The player landed a melee strike with equipped gear." : "The player swung and missed in live combat.");
            state.IncCounter(hitCount > 0 ? "combat:hit" : "combat:miss", 1f);
        }
    }

    private bool IsMeleeOriginClear(Vector3 origin)
    {
        // note: Rays do not reliably report a collider containing their origin; reject strikes starting inside solid scenery.
        int count = Physics.OverlapSphereNonAlloc(origin, 0.02f, _meleeOriginHits, ~0, QueryTriggerInteraction.Ignore);
        if (count == _meleeOriginHits.Length)
            return false;
        for (int i = 0; i < count; i++)
        {
            Collider obstacle = _meleeOriginHits[i];
            if (obstacle == null || IsOwnCollider(obstacle) || obstacle.GetComponentInParent<YQInvestorEnemy>() != null)
                continue;
            return false;
        }
        return true;
    }

    private bool HasClearMeleePath(Vector3 origin, Vector3 targetPoint, YQInvestorEnemy target)
    {
        Vector3 delta = targetPoint - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return true;

        // note: Inspect every returned hit because non-alloc casts are unordered; neither walls nor closed doors are damage-transparent.
        int count = Physics.RaycastNonAlloc(origin, delta / distance, _meleeOcclusionHits, distance,
            ~0, QueryTriggerInteraction.Ignore);
        if (count == _meleeOcclusionHits.Length)
            return false;
        for (int i = 0; i < count; i++)
        {
            Collider obstacle = _meleeOcclusionHits[i].collider;
            if (obstacle == null || IsOwnCollider(obstacle) || obstacle.GetComponentInParent<YQInvestorEnemy>() == target)
                continue;
            return false;
        }
        return true;
    }

    private void TryCastPulse()
    {
        if (IsCasting || Time.time < _nextSpellTime) return;
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        SkillRecord equippedSpell = ResolveEquippedSpell(state);
        if (equippedSpell == null)
        {
            Content?.SetInventoryMessage("No spell equipped.");
            return;
        }

        float cost = YQSpellCircleRules.GetResourceCost(equippedSpell);
        string resource = YQSpellCircleRules.GetResourceType(equippedSpell);
        if (_vitals == null || (resource == "stamina" ? _vitals.CurrentStamina : _vitals.CurrentMana) < cost)
        {
            Content?.SetInventoryMessage("Not enough " + resource + " to cast " + equippedSpell.name + ".");
            return;
        }

        // note: Freeze the mechanical inputs; resources, effects, counters and cooldown commit only at successful release.
        int equipmentBonus = Content != null ? Content.GetManaBonus(state) : 0;
        _castingSpell = equippedSpell;
        _castingOwner = state;
        _castingDescriptor = BuildSkillDescriptor(equippedSpell);
        _castingResource = resource;
        _castingResourceCost = cost;
        _castPower = YQSpellCircleRules.GetPower(equippedSpell, equipmentBonus);
        _castDuration = YQSpellCircleRules.GetCastSeconds(equippedSpell, equipmentBonus);
        _castCooldown = ResolveCooldown(equippedSpell, spellCooldown);
        _castStartedAt = Time.time;
        _castingCircles = YQGeneratedRuntimeVfx.SpawnSpellCircles(transform, _castingDescriptor, CastingCircle);
        if (_equipmentVisual == null)
            _equipmentVisual = GetComponent<YQPlayerEquipmentVisual>();
        _equipmentVisual?.PlayAbilityFeedback(equippedSpell.animationIntent);
    }

    private void AdvanceSpellCast()
    {
        if (!IsCasting) return;
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        // note: A profile switch, unequip or replacement cannot release a spell captured by the old owner.
        if (!ReferenceEquals(state, _castingOwner) || !ReferenceEquals(ResolveEquippedSpell(state), _castingSpell))
        {
            CancelSpellCast();
            return;
        }
        _castingCircles?.SetProgress(CastProgress);
        if (Time.time - _castStartedAt < _castDuration) return;
        if (!SpendAbilityResource(_castingResourceCost, _castingResource))
        {
            Content?.SetInventoryMessage("Spell interrupted: insufficient " + _castingResource + ".");
            CancelSpellCast();
            return;
        }

        SkillRecord releasedSpell = _castingSpell;
        string descriptor = _castingDescriptor;
        int power = _castPower;
        _nextSpellTime = Time.time + Mathf.Max(0.1f, _castCooldown);
        CancelSpellCast();
        ReleaseSpell(state, releasedSpell, descriptor, power);
    }

    private void CancelSpellCast()
    {
        if (_castingCircles != null)
        {
            // note: Hide immediately, including paused modal transitions, then release all transient presentation objects.
            _castingCircles.gameObject.SetActive(false);
            Destroy(_castingCircles.gameObject);
        }
        _castingCircles = null;
        _castingSpell = null;
        _castingOwner = null;
        _castingDescriptor = null;
    }

    private void ReleaseSpell(PlayerState state, SkillRecord equippedSpell, string spellDescriptor, int power)
    {
        // note: Both supported delivery paths use the same power that determined this cast's wind-up.
        YQRuntimeAudioFeedback.PlaySpellCast(transform.position + transform.forward * 0.8f + Vector3.up * 1.1f);
        if (WantsProjectile(equippedSpell, spellDescriptor) && YQGeneratedRuntimeVfx.TrySpawnSpellProjectile(transform, spellDescriptor, power, gameObject))
        {
            if (state != null)
            {
                string spellName = equippedSpell != null ? equippedSpell.name : "an equipped spell";
                state.AddLedgerLine("The player launched " + spellName + " as a projectile.");
                state.IncCounter("cast:projectile", 1f);
            }
            return;
        }

        int hitBufferCount = Physics.OverlapSphereNonAlloc(transform.position, 5f, _pulseHits, ~0, QueryTriggerInteraction.Ignore);
        int affected = 0;
        // note: Pulse damage follows the same one-event-per-enemy rule while reusing the collection between casts.
        _damagedEnemies.Clear();
        for (int i = 0; i < hitBufferCount; i++)
        {
            YQInvestorEnemy enemy = _pulseHits[i] != null ? _pulseHits[i].GetComponentInParent<YQInvestorEnemy>() : null;
            if (enemy == null || !_damagedEnemies.Add(enemy.GetInstanceID()))
                continue;
            enemy.ReceiveHit(power, gameObject);
            affected++;
        }

        YQGeneratedRuntimeVfx.SpawnSpellPulse(transform, spellDescriptor, 5f);
        if (state != null)
        {
            state.AddLedgerLine("The player released an equipped spell pulse affecting nearby threats.");
            state.IncCounter("cast:pulse", Mathf.Max(1, affected));
        }
    }

    private void TryInteract()
    {
        if (_viewCamera == null)
            _viewCamera = Camera.main;
        if (_viewCamera == null)
            return;

        if (!TryFindInteractHit(out RaycastHit hit))
            return;

        if (hit.collider == null)
            return;

        YQGeneratedLandmarkResource landmarkResource = hit.collider.GetComponentInParent<YQGeneratedLandmarkResource>();
        if (landmarkResource != null)
        {
            // note: Landmark harvesting uses the authoritative interaction ray and save path, making the required resource site usable in normal travel.
            // note: Animate only accepted interactions; rejected or exhausted targets do not play a success gesture.
            if (landmarkResource.TryUse(gameObject)) _equipmentVisual?.PlayInteractionFeedback(true);
            _recorder?.RecordInteract(landmarkResource.gameObject);
            return;
        }

        YQGeneratedSettlementService settlementService = hit.collider.GetComponentInParent<YQGeneratedSettlementService>();
        if (settlementService != null)
        {
            // note: Settlement services consume the same authoritative interaction input as doors and pickups, so the beta itinerary has a real gameplay endpoint.
            // note: Animate only accepted interactions; rejected or exhausted targets do not play a success gesture.
            if (settlementService.TryUse(gameObject)) _equipmentVisual?.PlayInteractionFeedback(false);
            _recorder?.RecordInteract(settlementService.gameObject);
            return;
        }

        if (TryOpenDialogueFromCollider(hit.collider))
        {
            _equipmentVisual?.PlayInteractionFeedback();
            return;
        }

        YQLockpickableDoor door = hit.collider.GetComponentInParent<YQLockpickableDoor>();
        if (door != null)
        {
            // note: Animate only accepted interactions; rejected or exhausted targets do not play a success gesture.
            if (door.TryInteract(gameObject)) _equipmentVisual?.PlayInteractionFeedback(false);
            _recorder?.RecordInteract(door.gameObject);
            return;
        }

        YQLockpickableLoot lockpickable = hit.collider.GetComponentInParent<YQLockpickableLoot>();
        if (lockpickable != null)
        {
            // note: Animate only accepted interactions; rejected or exhausted targets do not play a success gesture.
            if (lockpickable.TryInteract(gameObject)) _equipmentVisual?.PlayInteractionFeedback(false);
            _recorder?.RecordInteract(lockpickable.gameObject);
            return;
        }

        YQInvestorWorldPickup pickup = hit.collider.GetComponentInParent<YQInvestorWorldPickup>();
        if (pickup != null)
        {
            pickup.TryCollect(gameObject);
            _recorder?.RecordInteract(pickup.gameObject);
            return;
        }

        YQInvestorLootableCorpse corpse = hit.collider.GetComponentInParent<YQInvestorLootableCorpse>();
        if (corpse != null)
        {
            corpse.TryLoot(gameObject);
            _recorder?.RecordInteract(corpse.gameObject);
            return;
        }

        // note: Physical storage adapters share inventory UI; locks, pickups and corpse-specific behavior retain their existing priority.
        YQWorldContainer storage = hit.collider.GetComponentInParent<YQWorldContainer>();
        if (storage != null)
        {
            if (storage.TryOpen(gameObject)) _equipmentVisual?.PlayInteractionFeedback(false);
            _recorder?.RecordInteract(storage.gameObject);
            return;
        }

        YQInvestorShrine shrine = hit.collider.GetComponentInParent<YQInvestorShrine>();
        if (shrine != null)
        {
            shrine.Interact(gameObject);
            _equipmentVisual?.PlayInteractionFeedback();
            _recorder?.RecordInteract(shrine.gameObject);
        }
    }

    private bool TryFindInteractHit(out RaycastHit bestHit)
    {
        bestHit = default;
        if (_viewCamera == null)
            return false;

        Transform cameraTransform = _viewCamera.transform;
        float cameraOffset = Vector3.Distance(cameraTransform.position, transform.position + Vector3.up * 1.25f);
        float rayDistance = interactRange + Mathf.Clamp(cameraOffset, 0f, 5.25f);
        return TryPickDirectHoveredInteractableHit(
            Physics.RaycastNonAlloc(cameraTransform.position, cameraTransform.forward, _interactHits, rayDistance, ~0, QueryTriggerInteraction.Ignore),
            out bestHit);
    }

    private bool TryPickDirectHoveredInteractableHit(int hitCount, out RaycastHit bestHit)
    {
        bestHit = default;
        RaycastHit nearest = default;
        float nearestDistance = float.MaxValue;
        int count = Mathf.Min(hitCount, _interactHits.Length);
        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = _interactHits[i];
            if (candidate.collider == null || IsOwnCollider(candidate.collider))
                continue;

            if (candidate.distance < nearestDistance)
            {
                nearestDistance = candidate.distance;
                nearest = candidate;
            }
        }

        if (nearest.collider == null || !IsInteractableCollider(nearest.collider))
            return false;
        if (!IsCloseEnoughToInteract(nearest.collider, nearest.point))
            return false;

        bestHit = nearest;
        return true;
    }

    private bool IsOwnCollider(Collider collider)
    {
        if (collider == null)
            return false;

        Transform hit = collider.transform;
        return hit == transform || hit.IsChildOf(transform);
    }

    private bool IsCloseEnoughToInteract(Collider collider, Vector3 hitPoint)
    {
        Vector3 playerPoint = transform.position + Vector3.up * 1.0f;
        Vector3 targetPoint = hitPoint;
        if (collider != null)
            targetPoint = collider.ClosestPoint(playerPoint);

        float allowed = Mathf.Max(0.8f, interactRange + 0.25f);
        return (targetPoint - playerPoint).sqrMagnitude <= allowed * allowed;
    }

    private static bool IsInteractableCollider(Collider collider)
    {
        if (collider == null)
            return false;

        if (collider.GetComponentInParent<YQLockpickableDoor>() != null)
            return true;
        if (collider.GetComponentInParent<YQLockpickableLoot>() != null)
            return true;
        if (collider.GetComponentInParent<YQInvestorWorldPickup>() != null)
            return true;
        if (collider.GetComponentInParent<YQInvestorLootableCorpse>() != null)
            return true;
        if (collider.GetComponentInParent<YQWorldContainer>() != null)
            return true;
        if (collider.GetComponentInParent<YQInvestorShrine>() != null)
            return true;
        if (collider.GetComponentInParent<YQGeneratedLandmarkResource>() != null)
            return true;
        if (collider.GetComponentInParent<YQGeneratedSettlementService>() != null)
            return true;

        EntityInfo info = collider.GetComponentInParent<EntityInfo>();
        NpcDialogueAgent agent = collider.GetComponentInParent<NpcDialogueAgent>();
        return info != null && agent != null && info.hostility != Hostility.Hostile;
    }

    private bool TryOpenDialogueFromCollider(Collider source)
    {
        YQInvestorDialogueUI dialogue = YQInvestorDialogueUI.Instance;
        return dialogue != null && dialogue.TryOpenNpcFromCollider(source);
    }

    public void ReceiveDamage(int amount, GameObject source)
    {
        YQInvestorPlayerMotor motor = GetComponent<YQInvestorPlayerMotor>();
        if (motor != null && !motor.IsAuthoritative)
            return;

        // note: Accepted damage interrupts wind-up before the existing vitals/animation owners respond.
        if (amount > 0) CancelSpellCast();
        YQRuntimeAudioFeedback.PlayPlayerDamaged(transform.position + Vector3.up * 1.1f);
        if (_vitals != null)
            _vitals.TakeDamage(amount);

        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm != null && psm.state != null)
        {
            psm.state.AddLedgerLine("The player took damage from a hostile encounter.");
            psm.state.IncCounter("damage:taken", amount);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + transform.forward * attackRange + Vector3.up, attackRadius);
    }

    private GeneratedRpgContentService Content
    {
        get
        {
            if (_content == null)
                _content = GeneratedRpgContentService.Instance;
            return _content;
        }
    }

    private static SkillRecord ResolveEquippedSkill(PlayerState state, string slot)
    {
        if (state == null || state.equippedSkillBySlot == null)
            return null;
        if (!state.equippedSkillBySlot.TryGetValue(slot, out string skillId) || string.IsNullOrWhiteSpace(skillId))
            return null;
        return state.FindSkillById(skillId);
    }

    private static SkillRecord ResolveEquippedSpell(PlayerState state)
    {
        SkillRecord spell = ResolveEquippedSkill(state, "spell");
        return IsSpellRecord(spell) ? spell : null;
    }

    private static bool IsSpellRecord(SkillRecord skill)
    {
        return YQSpellCircleRules.IsSpell(skill);
    }

    private static string BuildSkillDescriptor(SkillRecord skill)
    {
        if (skill == null)
            return string.Empty;

        return (skill.name ?? string.Empty) + " " +
               (skill.type ?? string.Empty) + " " +
               (skill.description ?? string.Empty) + " " +
               (skill.context ?? string.Empty) + " " +
               (skill.targetingMode ?? string.Empty) + " " +
               (skill.vfxFamily ?? string.Empty) + " " +
               (skill.animationIntent ?? string.Empty);
    }

    private bool SpendAbilityResource(float amount, string resource)
    {
        if (_vitals == null)
            return false;

        if (amount <= 0f || resource == "none" || resource == "free")
            return true;
        if (resource == "stamina")
            return _vitals.SpendStamina(amount);
        return _vitals.SpendMana(amount);
    }

    private static float ResolveCooldown(SkillRecord skill, float fallback)
    {
        return skill != null && skill.cooldownSeconds > 0f ? skill.cooldownSeconds : fallback;
    }

    private static bool WantsProjectile(SkillRecord skill, string descriptor)
    {
        string targeting = skill != null ? (skill.targetingMode ?? string.Empty).Trim().ToLowerInvariant() : string.Empty;
        if (targeting == "pulse" || targeting == "self" || targeting == "buff")
            return false;
        if (targeting == "projectile")
            return true;
        return !string.IsNullOrWhiteSpace(descriptor);
    }

    private static string BuildMeleeDescriptor(PlayerState state)
    {
        InventoryItemRecord weapon = state != null ? state.GetEquippedItem("weapon") : null;
        SkillRecord activeSkill = ResolveEquippedSkill(state, "active");

        string weaponDescriptor = weapon != null
            ? (weapon.displayName ?? string.Empty) + " " +
              (weapon.description ?? string.Empty) + " " +
              (weapon.effectKey ?? string.Empty) + " " +
              (weapon.familyKey ?? string.Empty)
            : string.Empty;
        string skillDescriptor = activeSkill != null ? BuildSkillDescriptor(activeSkill) : string.Empty;
        string combined = (weaponDescriptor + " " + skillDescriptor).Trim();
        return string.IsNullOrWhiteSpace(combined) ? "melee strike" : combined;
    }
}

public static class YQRuntimeAudioFeedback
{
    private const string SwordImpactPath = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Battle/Sword/Sword_On_Wood/Impact/Sword_On_Wood_Impact_1.wav";
    private const string GenericWhooshPath = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Generic/Whoosh/Generic_Whoosh_2_S.wav";
    private const string ElectricImpactPath = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Electric/Explosion/Electric_Explosion_1_S.wav";
    private const string WaterHitPath = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Water/Hit/Water_Hit_1_S.wav";
    private const string FireWarmupPath = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Fire/Warmup Short/Fire_Warmup_Short_1_S.wav";
    private const string MagicWhooshPath = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Generic/Whoosh/Generic_Whoosh_4_S.wav";
    private const string LockpickEnterPath = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/Audio & Mixer/Lock Pick Enter Lock_1.wav";
    private const string LockpickClickPath = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/Audio & Mixer/Lock pick click1.wav";
    private const string LockpickJigglePath = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/Audio & Mixer/Lock pick jiggle - 2.0 - Tension.wav";
    private const string LockpickBreakPath = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/Audio & Mixer/Lock pick break_1.wav";
    private const string LockOpenPath = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/Audio & Mixer/Lock Open_1.wav";

    private static readonly Dictionary<string, AudioClip> s_clipCache = new Dictionary<string, AudioClip>(System.StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float> s_nextAllowedByLabel = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
    private static float s_nextGlobalAudioTime;

    public static void PlayPlayerMelee(Vector3 position, bool hit)
    {
        PlayClipAt(hit ? SwordImpactPath : GenericWhooshPath, position, hit ? 0.72f : 0.45f, hit ? Random.Range(0.94f, 1.06f) : Random.Range(0.88f, 1.12f), "Melee");
    }

    public static void PlaySpellCast(Vector3 position)
    {
        PlayClipAt(LoadClip(FireWarmupPath) != null ? FireWarmupPath : MagicWhooshPath, position, 0.58f, Random.Range(0.92f, 1.08f), "SpellCast");
    }

    public static void PlayEnemyAttack(Vector3 position)
    {
        PlayClipAt(GenericWhooshPath, position, 0.48f, Random.Range(0.78f, 0.96f), "EnemyAttack");
    }

    public static void PlayEnemyHit(Vector3 position)
    {
        PlayClipAt(WaterHitPath, position, 0.42f, Random.Range(0.86f, 1.06f), "EnemyHit");
    }

    public static void PlayEnemyDeath(Vector3 position)
    {
        PlayClipAt(ElectricImpactPath, position, 0.58f, Random.Range(0.82f, 0.98f), "EnemyDeath");
    }

    public static void PlayPlayerDamaged(Vector3 position)
    {
        PlayClipAt(WaterHitPath, position, 0.32f, Random.Range(0.92f, 1.08f), "PlayerDamaged");
    }

    public static void PlayChestOpen(Vector3 position)
    {
        PlayClipAt(LoadClip(LockOpenPath) != null ? LockOpenPath : SwordImpactPath, position + Vector3.up * 0.55f, 0.38f, Random.Range(0.86f, 1.04f), "ChestOpen");
    }

    public static void PlayLockpickStart(Vector3 position)
    {
        PlayClipAt(LockpickEnterPath, position + Vector3.up * 0.65f, 0.42f, Random.Range(0.96f, 1.06f), "LockpickStart");
    }

    public static void PlayLockpickClick(Vector3 position)
    {
        PlayClipAt(LockpickClickPath, position + Vector3.up * 0.65f, 0.28f, Random.Range(0.92f, 1.12f), "LockpickClick");
    }

    public static void PlayLockpickTension(Vector3 position)
    {
        PlayClipAt(LockpickJigglePath, position + Vector3.up * 0.65f, 0.22f, Random.Range(0.88f, 1.05f), "LockpickTension");
    }

    public static void PlayLockpickBreak(Vector3 position)
    {
        PlayClipAt(LockpickBreakPath, position + Vector3.up * 0.65f, 0.40f, Random.Range(0.92f, 1.06f), "LockpickBreak");
    }

    public static void PlayMimicReveal(Vector3 position)
    {
        PlayClipAt(ElectricImpactPath, position + Vector3.up * 0.75f, 0.62f, Random.Range(0.72f, 0.88f), "MimicReveal");
    }

    public static void PlayPickup(Vector3 position)
    {
        PlayClipAt(MagicWhooshPath, position + Vector3.up * 0.45f, 0.30f, Random.Range(1.05f, 1.20f), "Pickup");
    }

    public static void PlayQuestComplete(Vector3 position)
    {
        PlayClipAt(ElectricImpactPath, position + Vector3.up * 1.25f, 0.44f, Random.Range(1.06f, 1.18f), "QuestComplete");
    }

    public static void PlayOriginManifest(Vector3 position)
    {
        PlayClipAt(LoadClip(ElectricImpactPath) != null ? ElectricImpactPath : MagicWhooshPath, position + Vector3.up * 0.85f, 0.50f, Random.Range(0.96f, 1.08f), "OriginManifest");
    }

    private static void PlayClipAt(string clipPath, Vector3 position, float volume, float pitch, string label)
    {
        if (!CanPlayLabel(label))
            return;

        AudioClip clip = LoadClip(clipPath);
        if (clip == null)
            return;

        GameObject go = new GameObject("YQ_Audio_" + label);
        go.transform.position = position;
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Mathf.Clamp(pitch, 0.5f, 1.5f);
        source.ignoreListenerPause = true;
        source.spatialBlend = 0.72f;
        source.dopplerLevel = 0f;
        source.priority = 172;
        source.minDistance = 1.4f;
        source.maxDistance = 26f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.Play();
        Object.Destroy(go, Mathf.Max(0.25f, clip.length / Mathf.Max(0.1f, Mathf.Abs(source.pitch))) + 0.35f);
    }

    private static bool CanPlayLabel(string label)
    {
        float now = Time.unscaledTime;
        if (now < s_nextGlobalAudioTime)
            return false;

        label = string.IsNullOrWhiteSpace(label) ? "Audio" : label.Trim();
        if (s_nextAllowedByLabel.TryGetValue(label, out float nextAllowed) && now < nextAllowed)
            return false;

        float interval = ResolveMinimumInterval(label);
        s_nextAllowedByLabel[label] = now + interval;
        s_nextGlobalAudioTime = now + 0.018f;
        return true;
    }

    private static float ResolveMinimumInterval(string label)
    {
        string normalized = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim().ToLowerInvariant();
        if (normalized.Contains("lockpickclick"))
            return 0.035f;
        if (normalized.Contains("lockpick"))
            return 0.10f;
        if (normalized.Contains("enemy"))
            return 0.14f;
        if (normalized.Contains("spell"))
            return 0.12f;
        if (normalized.Contains("melee"))
            return 0.08f;
        if (normalized.Contains("pickup"))
            return 0.08f;
        return 0.10f;
    }

    private static AudioClip LoadClip(string clipPath)
    {
#if UNITY_EDITOR
        string normalizedPath = string.IsNullOrWhiteSpace(clipPath) ? string.Empty : clipPath.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
            return null;
        if (s_clipCache.TryGetValue(normalizedPath, out AudioClip cached))
            return cached;

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(normalizedPath);
        if (clip != null)
        {
            s_clipCache[normalizedPath] = clip;
            return clip;
        }

        string wantedLeaf = GetPathLeaf(normalizedPath);
        string wantedName = StripExtension(wantedLeaf);
        if (string.IsNullOrWhiteSpace(wantedName))
        {
            s_clipCache[normalizedPath] = null;
            return null;
        }

        string[] guids = AssetDatabase.FindAssets(wantedName + " t:AudioClip");
        for (int i = 0; i < guids.Length; i++)
        {
            string foundPath = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
            if (!string.Equals(GetPathLeaf(foundPath), wantedLeaf, System.StringComparison.OrdinalIgnoreCase))
                continue;

            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(foundPath);
            if (clip != null)
            {
                s_clipCache[normalizedPath] = clip;
                return clip;
            }
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string foundPath = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
            if (foundPath.IndexOf(wantedName, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(foundPath);
            if (clip != null)
            {
                s_clipCache[normalizedPath] = clip;
                return clip;
            }
        }

        s_clipCache[normalizedPath] = null;
        return null;
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    private static string GetPathLeaf(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path.Substring(slash + 1) : path;
    }

    private static string StripExtension(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        int dot = value.LastIndexOf('.');
        return dot > 0 ? value.Substring(0, dot) : value;
    }
#endif
}
