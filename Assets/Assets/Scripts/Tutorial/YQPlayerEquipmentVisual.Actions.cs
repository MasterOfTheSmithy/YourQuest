using UnityEngine;

public sealed partial class YQPlayerEquipmentVisual
{
    private Vector3 _climbLeftContact, _climbRightContact;
    private int _leftContactCycle = int.MinValue, _rightContactCycle = int.MinValue;
    private Vector3 _climbLeftFoot, _climbRightFoot;
    private int _leftFootCycle = int.MinValue, _rightFootCycle = int.MinValue;
    public float twoHandGripSeparation = .18f;

    private void UpdateActionContacts()
    {
        if (_animator == null || !_animator.isHuman || _motor == null || !_motor.IsAuthoritative || _motor.IsRagdoll || !_animator.isActiveAndEnabled) return;
        if (_motor.IsClimbing)
        {
            // note: Stance hands retain world contact while swing hands seek their next hold; the mirrored view copies these same bones afterward.
            float phase = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            PlaceClimbingHand(true, phase, ref _climbLeftContact, ref _leftContactCycle);
            PlaceClimbingHand(false, phase + .5f, ref _climbRightContact, ref _rightContactCycle);
            PlaceClimbingFoot(true, phase + .5f, ref _climbLeftFoot, ref _leftFootCycle);
            PlaceClimbingFoot(false, phase, ref _climbRightFoot, ref _rightFootCycle);
            return;
        }
        _leftContactCycle = _rightContactCycle = int.MinValue;
        _leftFootCycle = _rightFootCycle = int.MinValue;
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        InventoryItemRecord item = state?.GetEquippedItem("weapon");
        if (item == null || state?.GetEquippedItem("offhand")?.itemId != item.itemId || _combat != null && (_combat.IsCasting || _combat.IsBlocking)) return;
        if (_combat != null && _combat.IsAttacking && YQInvestorCombat.ResolveAttackKind(state, item) == YQPlayerAttackKind.Ranged) return;
        Transform right = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        // note: One two-handed item keeps its existing primary grip; the support hand closes onto a bounded second grip on the same skeleton.
        if (right != null) ReachHand(true, right.position + right.up * Mathf.Clamp(twoHandGripSeparation, .08f, .3f));
    }

    private void PlaceClimbingHand(bool left, float phase, ref Vector3 contact, ref int previousCycle)
    {
        Transform hand = _animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        if (hand == null) return;
        int cycle = Mathf.FloorToInt(phase);
        Vector3 desired = transform.position + transform.up * (1.55f + Mathf.Sin(phase * Mathf.PI * 2f) * .15f) + transform.right * (left ? -.3f : .3f);
        if (cycle != previousCycle || (contact - hand.position).sqrMagnitude > .8f)
        {
            if (Physics.Raycast(desired - transform.forward * .1f, transform.forward, out RaycastHit wall, 1.3f, _motor.climbMask, QueryTriggerInteraction.Ignore) && !wall.transform.IsChildOf(transform))
            { contact = wall.point + wall.normal * .04f; previousCycle = cycle; }
            else return;
        }
        ReachHand(left, contact);
    }

    private void ReachHand(bool left, Vector3 target)
    {
        Transform upper = _animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        Transform fore = _animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        Transform hand = _animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        ReachLimb(left, upper, fore, hand, target);
    }

    private void PlaceClimbingFoot(bool left, float phase, ref Vector3 contact, ref int previousCycle)
    {
        Transform foot = _animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        if (foot == null) return;
        int cycle = Mathf.FloorToInt(phase);
        Vector3 desired = transform.position + transform.up * (.35f + Mathf.Sin(phase * Mathf.PI * 2f) * .18f) + transform.right * (left ? -.2f : .2f);
        if (cycle != previousCycle || (contact - foot.position).sqrMagnitude > .6f)
        {
            if (!Physics.Raycast(desired - transform.forward * .1f, transform.forward, out RaycastHit wall, 1.3f, _motor.climbMask, QueryTriggerInteraction.Ignore) || wall.transform.IsChildOf(transform)) return;
            contact = wall.point + wall.normal * .1f; previousCycle = cycle;
        }
        // note: Foot holds use the same bounded two-link correction as hands, preserving imported leg lengths.
        ReachLimb(left, _animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg),
            _animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg), foot, contact);
    }

    private void ReachLimb(bool left, Transform upper, Transform fore, Transform hand, Vector3 target)
    {
        if (upper == null || fore == null || hand == null) return;
        float a = Vector3.Distance(upper.position, fore.position), b = Vector3.Distance(fore.position, hand.position);
        Vector3 delta = target - upper.position;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
        if (delta.sqrMagnitude < .0001f || a < .001f || b < .001f) return;
        Vector3 direction = delta.normalized;
        Vector3 pole = Vector3.ProjectOnPlane((fore.position - upper.position).normalized + transform.right * (left ? -.35f : .35f) - transform.forward * .15f, direction).normalized;
        if (pole.sqrMagnitude < .01f) pole = Vector3.ProjectOnPlane(-transform.up, direction).normalized;
        float along = (a * a - b * b + distance * distance) / (2f * distance);
        Vector3 elbow = upper.position + direction * along + pole * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
        upper.rotation = Quaternion.FromToRotation(fore.position - upper.position, elbow - upper.position) * upper.rotation;
        Vector3 boundedTarget = upper.position + direction * distance;
        fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, boundedTarget - fore.position) * fore.rotation;
    }

    public void PlayAttackFeedback(YQPlayerAttackKind kind, bool left, float seconds, float contact01)
    {
        // note: Hand/family selection comes from the accepting combat owner, rather than alternating unrelated left/right visuals.
        _meleeSide = left ? -1f : 1f;
        _meleeKickUntil = Time.time + seconds;
        if (!_productionAnimation) { PlayMeleeFeedback(); return; }
        ClearProductionActionTriggers();
        string state = kind == YQPlayerAttackKind.TwoHanded ? "Actions.TwoHandAttack" : kind == YQPlayerAttackKind.Ranged ? "Actions.RangedAttack" :
            kind == YQPlayerAttackKind.Unarmed ? (left ? "Actions.PunchLeft" : "Actions.PunchRight") : (left ? "Actions.MeleeLeft" : "Actions.MeleeRight");
        if (_animator.HasState(1, Animator.StringToHash(state))) _animator.CrossFadeInFixedTime(state, .035f, 1, 0f);
        else TriggerFirstAnimator(kind == YQPlayerAttackKind.Unarmed ? left ? "PunchLeft" : "PunchRight" : left ? "MeleeLeft" : "MeleeRight");
        _requestedActionFrame = Time.frameCount;
        SetAnimatorBool("AttackLeft", left && kind != YQPlayerAttackKind.TwoHanded);
        SetAnimatorFloatImmediate("ActionRate", 1f / Mathf.Max(.1f, seconds));
    }

    public void PlayGuardFeedback()
    {
        if (!_productionAnimation) return;
        ClearProductionActionTriggers();
        TriggerFirstAnimator("Guard");
    }

    public void PlaySlideFeedback()
    {
        if (_animator != null && _animator.HasState(0, Animator.StringToHash("Base Layer.PowerSlideStart")))
            _animator.CrossFadeInFixedTime("Base Layer.PowerSlideStart", .08f, 0, 0f);
    }

    public void PlayGetUpFeedback()
    {
        if (_animator != null && _animator.HasState(0, Animator.StringToHash("Base Layer.GetUp")))
            _animator.CrossFadeInFixedTime("Base Layer.GetUp", .06f, 0, 0f);
    }
}
