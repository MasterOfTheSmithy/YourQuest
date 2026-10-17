using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum YQPlayerAttackKind { Unarmed, OneHanded, TwoHanded, Ranged }

public sealed partial class YQInvestorCombat
{
    [Header("Action timing and defense")]
    public float unarmedWindup = .14f, unarmedRecovery = .22f;
    public float oneHandWindup = .22f, oneHandRecovery = .28f;
    public float twoHandWindup = .34f, twoHandRecovery = .4f;
    public float rangedWindup = .38f, rangedRecovery = .28f;
    public float attackActiveSeconds = .1f;
    public float parryWindowSeconds = .16f;
    public float guardStaminaPerDamage = .65f;
    public float guardBreakSeconds = .65f;
    [Range(0f, 1f)] public float blockMitigation = .7f;
    public float defenseAngle = 65f;
    private PlayerState _attackOwner;
    private InventoryItemRecord _attackItem;
    private string _attackSlot;
    private float _attackStartedAt, _attackContactAt, _attackEndAt;
    private int _acceptedAttackDamage;
    private float _acceptedAttackRange;
    private bool _attackPending, _attackCommitted, _attackLeft;
    private YQPlayerAttackKind _attackKind;
    private float _guardStartedAt, _guardLockedUntil, _staggerUntil, _nextStaggerAt;
    private bool _guardWasHeld, _parryConsumed;
    public bool IsAttacking => _attackPending;
    public bool IsBlocking { get; private set; }
    public float AttackProgress => IsAttacking ? Mathf.InverseLerp(_attackStartedAt, _attackEndAt, Time.time) : 0f;
    public float AttackContactNormalized => IsAttacking ? Mathf.InverseLerp(_attackStartedAt, _attackEndAt, _attackContactAt) : .4f;
    public float AttackDuration => IsAttacking ? _attackEndAt - _attackStartedAt : .6f;
    public float AttackAnimationRate => IsAttacking ? 1f / Mathf.Max(.1f, AttackDuration) : 1f;
    public float AttackPosePhase => !IsAttacking ? 1f : Time.time <= _attackContactAt
        ? .4f * Mathf.InverseLerp(_attackStartedAt, _attackContactAt, Time.time)
        : Mathf.Lerp(.4f, 1f, Mathf.InverseLerp(_attackContactAt, _attackEndAt, Time.time));
    public float MovementScale => Time.time < _staggerUntil ? .45f : IsBlocking ? .55f : IsAttacking ? .8f : IsCasting ? .7f : 1f;

    public static YQPlayerAttackKind ResolveAttackKind(PlayerState state, InventoryItemRecord item)
    {
        if (item == null) return YQPlayerAttackKind.Unarmed;
        // note: Hand occupancy is authoritative for two-handed equipment; supported mechanical/approved asset families select ranged behavior.
        string type = (item.itemType ?? "").Trim().ToLowerInvariant();
        string family = (item.familyKey ?? "").Trim().ToLowerInvariant();
        string asset = (item.prefabKey ?? "").ToLowerInvariant();
        if (type == "bow" || type == "crossbow" || type == "ranged" || family == "bow" || family == "crossbow" ||
            family == "weapon:bow" || family == "weapon:crossbow" || asset.Contains("crossbow") || asset.Contains("_bow") || asset.Contains("bow_")) return YQPlayerAttackKind.Ranged;
        InventoryItemRecord right = state?.GetEquippedItem("weapon"), left = state?.GetEquippedItem("offhand");
        return right != null && left != null && string.Equals(right.itemId, left.itemId, StringComparison.OrdinalIgnoreCase)
            ? YQPlayerAttackKind.TwoHanded : YQPlayerAttackKind.OneHanded;
    }

    private void TryBeginAttack(bool left)
    {
        if (IsCasting || IsAttacking || IsBlocking || Time.time < _nextAttackTime || Time.time < _staggerUntil ||
            _motor != null && (_motor.IsRagdoll || _motor.IsRecovering || _motor.IsDodgeRolling)) return;
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        string slot = left ? "offhand" : "weapon";
        InventoryItemRecord item = state?.GetEquippedItem(slot);
        YQPlayerAttackKind kind = ResolveAttackKind(state, item);
        float windup = kind == YQPlayerAttackKind.Unarmed ? unarmedWindup : kind == YQPlayerAttackKind.TwoHanded ? twoHandWindup : kind == YQPlayerAttackKind.Ranged ? rangedWindup : oneHandWindup;
        float recovery = kind == YQPlayerAttackKind.Unarmed ? unarmedRecovery : kind == YQPlayerAttackKind.TwoHanded ? twoHandRecovery : kind == YQPlayerAttackKind.Ranged ? rangedRecovery : oneHandRecovery;
        if (_vitals != null && !_vitals.SpendStamina(kind == YQPlayerAttackKind.TwoHanded ? 12f : kind == YQPlayerAttackKind.Unarmed ? 3f : 6f)) return;
        _attackOwner = state; _attackItem = item; _attackSlot = slot;
        _attackLeft = left; _attackKind = kind;
        _attackPending = true; _attackCommitted = false;
        _attackStartedAt = Time.time;
        _attackContactAt = Time.time + Mathf.Max(.04f, windup);
        _attackEndAt = _attackContactAt + Mathf.Max(.02f, attackActiveSeconds) + Mathf.Max(.05f, recovery);
        _nextAttackTime = Mathf.Max(_attackEndAt, Time.time + attackCooldown);
        // note: Preserve non-weapon equipment bonuses while preventing the other hand's weapon from contributing to this strike.
        int equipmentBonus = GeneratedRpgContentService.Instance != null ? GeneratedRpgContentService.Instance.GetAttackBonus(state) : item?.attackBonus ?? 0;
        InventoryItemRecord otherHand = state?.GetEquippedItem(left ? "weapon" : "offhand");
        if (otherHand != null && otherHand.itemId != item?.itemId && GeneratedRpgContentService.Instance != null) equipmentBonus -= otherHand.attackBonus;
        _acceptedAttackDamage = lightAttackDamage + equipmentBonus + (state != null ? Mathf.Max(0, state.stats.attack / 4) : 0);
        _acceptedAttackRange = kind == YQPlayerAttackKind.Unarmed ? Mathf.Min(attackRange, 1.4f) : kind == YQPlayerAttackKind.TwoHanded ? attackRange * 1.2f : attackRange;
        if (_equipmentVisual == null) _equipmentVisual = GetComponent<YQPlayerEquipmentVisual>();
        _equipmentVisual?.PlayAttackFeedback(kind, left, AttackDuration, AttackContactNormalized);
    }

    private void AdvanceAttack()
    {
        if (!IsAttacking) return;
        PlayerState current = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (!ReferenceEquals(current, _attackOwner) || !ReferenceEquals(current?.GetEquippedItem(_attackSlot), _attackItem))
        { CancelAttack(); return; }
        if (!_attackCommitted && Time.time >= _attackContactAt)
        {
            _attackCommitted = true;
            if (_attackKind == YQPlayerAttackKind.Ranged)
            {
                Camera camera = _motor != null ? _motor.playerCamera : _viewCamera;
                Vector3 direction = camera != null ? camera.transform.forward : transform.forward;
                Vector3 origin = transform.position + Vector3.up * (_motor != null && _motor.IsCrouching ? .8f : 1.35f);
                if (IsMeleeOriginClear(origin))
                {
                    YQPlayerProjectile.Spawn(origin, direction, _acceptedAttackDamage, gameObject, current);
                    _recorder?.RecordCombat(null);
                    GetComponent<YQPlayerHaptics>()?.Pulse(.2f, .55f, .1f);
                    current?.IncCounter("combat:ranged_release", 1f);
                }
            }
            else CommitMeleeAttack();
        }
        if (Time.time >= _attackEndAt) CancelAttack();
    }

    private void CancelAttack()
    {
        _attackPending = _attackCommitted = false;
        _attackOwner = null; _attackItem = null; _attackSlot = null;
    }

    public void InterruptActions()
    {
        // note: Shared interruptions cancel pending commitment and buffered input, but never refund or replay committed effects.
        CancelAttack(); CancelSpellCast();
        IsBlocking = false;
        _queuedAttackUntil = float.NegativeInfinity;
    }

    public bool NotifyAcceptedDamage(float amount)
    {
        if (amount <= 0f) return false;
        CancelSpellCast();
        // note: Brief stagger immunity prevents repeated damage from keeping a living player permanently locked.
        if (Time.time < _nextStaggerAt) return false;
        CancelAttack(); IsBlocking = false;
        _staggerUntil = Time.time + Mathf.Clamp(amount / Mathf.Max(1f, _vitals != null ? _vitals.MaximumHealth : 100f), .1f, .3f);
        _nextStaggerAt = Time.time + .75f;
        return true;
    }

    private void AdvanceGuard()
    {
        bool held = Keyboard.current != null && Keyboard.current.fKey.isPressed || Gamepad.current != null && Gamepad.current.leftTrigger.isPressed;
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        bool equipped = CanGuardWith(state, state?.GetEquippedItem("weapon")) || CanGuardWith(state, state?.GetEquippedItem("offhand"));
        bool accepted = held && equipped && !IsAttacking && !IsCasting && Time.time >= _guardLockedUntil && Time.time >= _staggerUntil &&
            (_vitals == null || _vitals.CurrentStamina > 0f);
        if (accepted && !_guardWasHeld)
        {
            _guardStartedAt = Time.time; _parryConsumed = false;
            if (_equipmentVisual == null) _equipmentVisual = GetComponent<YQPlayerEquipmentVisual>();
            _equipmentVisual?.PlayGuardFeedback();
        }
        IsBlocking = accepted;
        // note: A held guard cannot restart its parry window after stagger, recovery, weapon changes or guard break.
        _guardWasHeld = held;
    }

    private static bool CanGuardWith(PlayerState state, InventoryItemRecord item)
    {
        if (item == null || ResolveAttackKind(state, item) == YQPlayerAttackKind.Ranged) return false;
        // note: Cosmetic offhand objects and ranged weapons cannot silently become shields.
        string type = (item.itemType ?? "").Trim().ToLowerInvariant();
        string family = (item.familyKey ?? "").Trim().ToLowerInvariant();
        return type == "weapon" || type == "shield" || family == "shield" || family == "weapon:shield" ||
            family == "sword" || family == "dagger" || family == "axe" || family == "mace" || family == "spear" || family == "staff" || family.StartsWith("weapon:", StringComparison.Ordinal);
    }

    private int ResolveGuardDamage(int amount, GameObject source)
    {
        if (!IsBlocking || source == null) return amount;
        Vector3 toSource = Vector3.ProjectOnPlane(source.transform.position - transform.position, Vector3.up);
        if (toSource.sqrMagnitude < .001f || Vector3.Angle(transform.forward, toSource) > defenseAngle) return amount;
        if (!_parryConsumed && Time.time - _guardStartedAt <= Mathf.Clamp(parryWindowSeconds, .05f, .3f) && (_vitals == null || _vitals.SpendStamina(8f)))
        {
            _parryConsumed = true;
            source.GetComponentInParent<YQInvestorEnemy>()?.ReceiveParry(.6f);
            GetComponent<YQPlayerHaptics>()?.Pulse(.15f, .95f, .12f);
            PlayerStateManager.Instance?.state?.IncCounter("combat:parry", 1f);
            return 0;
        }
        _parryConsumed = true;
        float cost = amount * Mathf.Max(.05f, guardStaminaPerDamage);
        if (_vitals != null && !_vitals.SpendStamina(cost))
        {
            _vitals.SpendStamina(_vitals.CurrentStamina);
            IsBlocking = false; _guardLockedUntil = Time.time + guardBreakSeconds;
            _staggerUntil = _guardLockedUntil;
            return amount;
        }
        GetComponent<YQPlayerHaptics>()?.Pulse(.6f, .15f, .08f);
        PlayerStateManager.Instance?.state?.IncCounter("combat:block", 1f);
        return Mathf.Max(0, Mathf.CeilToInt(amount * (1f - blockMitigation)));
    }

    private void TryBeginActiveSkill()
    {
        // note: Active slots execute supported structured contracts; ability prose never selects a mechanic.
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        SkillRecord skill = ResolveEquippedSkill(state, "active");
        if (skill == null || !skill.unlocked || IsAttacking || IsCasting || IsBlocking || Time.time < _nextSpellTime || Time.time < _staggerUntil) return;
        string targeting = (skill.targetingMode ?? "").Trim().ToLowerInvariant();
        if (targeting != "projectile" && targeting != "pulse" && !(targeting.Length == 0 && YQSpellCircleRules.IsSpell(skill)))
        { Content?.SetInventoryMessage("This skill needs a supported projectile or pulse contract."); return; }
        BeginStructuredCast(state, skill);
    }

    private void BeginStructuredCast(PlayerState state, SkillRecord skill)
    {
        string resource = YQSpellCircleRules.GetResourceType(skill);
        float cost = YQSpellCircleRules.GetResourceCost(skill);
        if (_vitals == null || (resource == "stamina" ? _vitals.CurrentStamina : resource == "none" || resource == "free" ? float.MaxValue : _vitals.CurrentMana) < cost) return;
        _castingSpell = skill; _castingOwner = state; _castingSlot = "active";
        _castingDescriptor = BuildSkillDescriptor(skill); _castingResource = resource; _castingResourceCost = cost;
        _castPower = YQSpellCircleRules.GetPower(skill, 0);
        _castDuration = Mathf.Max(.15f, YQSpellCircleRules.GetCastSeconds(skill, 0));
        _castCooldown = ResolveCooldown(skill, spellCooldown); _castStartedAt = Time.time;
        _equipmentVisual?.PlayAbilityFeedback(skill.animationIntent);
    }
}
