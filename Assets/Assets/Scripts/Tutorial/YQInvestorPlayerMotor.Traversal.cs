using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class YQInvestorPlayerMotor
{
    [Header("Momentum and impact")]
    public float powerSlideEntrySpeed = 15f;
    public float powerSlideExitSpeed = 8f;
    public float powerSlideTurnDegrees = 65f;
    public float powerSlideBrake = 12f;
    public float powerSlideDrag = 2.5f;
    public float safeFallHeight = 3f;
    public float fatalFallHeight = 22f;
    public float safeImpactSpeed = 12f;
    public float fatalImpactSpeed = 35f;
    public float ragdollImpactSpeed = 23f;
    [Range(.3f, .5f)] public float clutchWindowSeconds = .4f;
    public float extremeVelocityChange = 40f;
    public float extremeAcceleration = 250f;
    public float accelerationSampleSeconds = .12f;

    private YQPlayerHaptics _feedback;
    private YQPlayerRagdoll _ragdoll;
    private YQInvestorCombat _movementCombat;
    private PlayerState _speedBonusOwner;
    private float _speedBonusSampleAt, _cachedSpeedBonus, _pausedFallVelocity;
    private bool _fallVelocityPaused;
    private bool _isPowerSliding, _fallTracked, _frameLanded;
    private float _pendingImpactSpeed;
    private Vector3 _motionSamplePosition, _frameStart, _sampleVelocity, _supportVelocity;
    private float _fallApex, _frameImpactSpeed, _sampleElapsed, _getUpUntil;
    private float _lastClutchPress = float.NegativeInfinity;
    private float _pendingLandingAt, _pendingFallDamage, _preImpactHealth;
    private PlayerState _landingOwner;
    private YQPlayerActionAssets _contactAssets;
    private AudioSource _slideAudio;
    private bool _contactAssetsLoaded;
    private float _nextSlideContactAt;
    private bool _frameMovementHeld, _frameControllerMoveAdmitted, _frameIntegrationCaptured;
    private Vector3 _framePlanarBeforeIntegration;
    private float _frameVerticalBeforeIntegration, _frameDashBeforeIntegration;
    public Vector3 ResolvedVelocity { get; private set; }
    public bool IsPowerSliding => _isPowerSliding;
    public bool IsRagdoll => _ragdoll != null && _ragdoll.IsSimulating;
    public bool IsRecovering => Time.time < _getUpUntil;
    public bool HasPendingFallDamage => _pendingFallDamage > 0f;
    public float CameraFeedbackIntensity => _feedback != null ? _feedback.cameraFeedbackIntensity : .65f;

    private void AdvancePlanarVelocity(Vector3 target, float accel, bool grounded, float dt)
    {
        float speed = _planarVelocity.magnitude;
        bool hardTurn = target.sqrMagnitude < .01f || Vector3.Angle(_planarVelocity, target) >= powerSlideTurnDegrees;
        if (!_isPowerSliding && grounded && !IsDashing && speed >= powerSlideEntrySpeed && hardTurn)
        {
            _isPowerSliding = true;
            _feedback?.Pulse(.75f, .35f, .12f);
            GetComponent<YQPlayerEquipmentVisual>()?.PlaySlideFeedback();
            _nextSlideContactAt = 0f;
        }
        if (_isPowerSliding && (!grounded || speed <= powerSlideExitSpeed)) _isPowerSliding = false;
        if (!_isPowerSliding) { _planarVelocity = Vector3.MoveTowards(_planarVelocity, target, accel * dt); return; }

        // note: Solve dv/dt = -brake-drag*v analytically, then regain bounded steering as momentum falls.
        float nextSpeed = YQPlayerMotionRules.BrakeSpeed(speed, powerSlideBrake, powerSlideDrag, dt);
        Vector3 direction = speed > .001f ? _planarVelocity / speed : transform.forward;
        float steering = Mathf.InverseLerp(powerSlideEntrySpeed, powerSlideExitSpeed, nextSpeed);
        if (target.sqrMagnitude > .01f)
            direction = Vector3.RotateTowards(direction, target.normalized, (20f + 110f * steering) * Mathf.Deg2Rad * dt, 0f);
        _planarVelocity = direction * nextSpeed;
        _feedback?.Pulse(.4f * Mathf.Clamp01(nextSpeed / 25f), .12f, .05f);
        PlaySlideSurfaceContact(nextSpeed);
    }

    private void PlaySlideSurfaceContact(float speed)
    {
        if (Time.time < _nextSlideContactAt) return;
        _nextSlideContactAt = Time.time + .18f;
        if (!_contactAssetsLoaded)
        { _contactAssetsLoaded = true; _contactAssets = Resources.Load<YQPlayerActionAssets>("Player/YQPlayerActionAssets"); }
        if (_contactAssets == null) return;
        AudioClip clip = _contactAssets.gravelContact;
        // note: Approved surface-contact bindings provide feedback without runtime asset searches or placeholder sounds.
        if (Physics.Raycast(transform.position + Vector3.up * .15f, Vector3.down, out RaycastHit ground, .7f, ~0, QueryTriggerInteraction.Ignore) && ground.collider.sharedMaterial != null)
        {
            string surface = ground.collider.sharedMaterial.name.ToLowerInvariant();
            if (surface.Contains("wood")) clip = _contactAssets.woodContact;
            else if (surface.Contains("wet") || surface.Contains("water")) clip = _contactAssets.wetContact;
        }
        if (clip == null) return;
        if (_slideAudio == null)
        {
            _slideAudio = gameObject.AddComponent<AudioSource>();
            _slideAudio.playOnAwake = false; _slideAudio.spatialBlend = .65f;
            _slideAudio.dopplerLevel = 0f; _slideAudio.minDistance = 1f; _slideAudio.maxDistance = 20f;
        }
        _slideAudio.pitch = .85f;
        _slideAudio.PlayOneShot(clip, .25f * Mathf.Clamp01(speed / 25f));
    }

    private void StopSlideAudio() { if (_slideAudio != null) _slideAudio.Stop(); }

    private void BeginMotionFrame(float dt)
    {
        if (_movementCombat == null) _movementCombat = GetComponent<YQInvestorCombat>();
        if (_fallVelocityPaused) { _verticalVelocity = _pausedFallVelocity; _fallVelocityPaused = false; }
        // note: Position changes outside this motor are explicit repositioning, not physical velocity samples.
        if ((transform.position - _motionSamplePosition).sqrMagnitude > .0001f) ResetMotionSample();
        _frameStart = transform.position;
        _frameMovementHeld = _frameControllerMoveAdmitted = _frameIntegrationCaptured = false;
        _frameImpactSpeed = 0f;
        _frameLanded = false;
        _supportVelocity = Vector3.zero;
        if (!_fallTracked && !_controller.isGrounded && !_isSwimming && !_isClimbing)
        { _fallTracked = true; _fallApex = transform.position.y; }
        if (_fallTracked) _fallApex = Mathf.Max(_fallApex, transform.position.y);
    }

    private void CaptureMotionIntegrationState()
    {
        // note: Capture after accepted jump/dash impulses, before gravity, acceleration, or remaining dash time is integrated.
        if (_frameIntegrationCaptured) return;
        _frameIntegrationCaptured = true;
        _framePlanarBeforeIntegration = _planarVelocity;
        _frameVerticalBeforeIntegration = _verticalVelocity;
        _frameDashBeforeIntegration = _dashTimeRemaining;
    }

    private void EndMotionFrame(float dt)
    {
        if (dt <= .00001f) return;
        ResolvedVelocity = (transform.position - _frameStart) / dt;
        _motionSamplePosition = transform.position;
        if (_frameMovementHeld && !_frameControllerMoveAdmitted)
        {
            // note: An entirely denied step simulates no time. Preserve real momentum/fall history and omit this pause from physical acceleration samples.
            // note: If an earlier Move did collide and a later step-assist Move is denied, normal impact accounting below still observes that real collision.
            if (_frameIntegrationCaptured)
            {
                _planarVelocity = _framePlanarBeforeIntegration;
                _verticalVelocity = _frameVerticalBeforeIntegration;
                _dashTimeRemaining = _frameDashBeforeIntegration;
            }
            return;
        }
        if (_isSwimming || _isClimbing) _fallTracked = false;
        else if (_fallTracked && _frameLanded)
        {
            float height = Mathf.Max(0f, _fallApex - transform.position.y);
            _fallTracked = false;
            QueueLanding(height, Mathf.Max(0f, _frameImpactSpeed + _supportVelocity.y));
        }
        // note: Wall contacts discard blocked horizontal momentum instead of storing a spring that launches on release.
        if (!IsDashing && !_isClimbing && !_isSwimming)
            _planarVelocity = Vector3.ProjectOnPlane(ResolvedVelocity, Vector3.up);
        _sampleElapsed += dt;
        if (_sampleElapsed >= Mathf.Max(.04f, accelerationSampleSeconds))
        {
            float change = (ResolvedVelocity - _sampleVelocity).magnitude;
            if (!HasPendingFallDamage && !IsDashing && !IsRagdoll && change >= extremeVelocityChange && change / _sampleElapsed >= extremeAcceleration)
                EnterPhysicalRagdoll(ResolvedVelocity);
            _sampleVelocity = ResolvedVelocity;
            _sampleElapsed = 0f;
        }
    }

    private void QueueLanding(float height, float impactSpeed)
    {
        if (vitals == null || vitals.IsDead || HasPendingFallDamage) return;
        float damage = YQPlayerMotionRules.FallDamage(height, impactSpeed, safeFallHeight, fatalFallHeight,
            safeImpactSpeed, fatalImpactSpeed, vitals.MaximumHealth);
        _feedback?.Pulse(Mathf.Clamp01(impactSpeed / 40f), .2f, .12f);
        if (damage <= 0f) return;
        _pendingFallDamage = damage;
        _pendingImpactSpeed = impactSpeed;
        _pendingLandingAt = Time.time;
        _preImpactHealth = vitals.CurrentHealth;
        _landingOwner = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        // note: A fresh pre-contact crouch-roll receives the same opportunity as input during the post-contact grace period.
        if (TryResolveClutch()) return;
        if (impactSpeed >= ragdollImpactSpeed && !IsRagdoll) EnterPhysicalRagdoll(ResolvedVelocity);
        // note: Both damaging and lethal falls retain the same short post-impact timing opportunity.
    }

    private void ReadClutchInput()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        if ((kb != null && kb.qKey.wasPressedThisFrame && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)) ||
            (pad != null && pad.buttonEast.wasPressedThisFrame && pad.rightStickButton.isPressed)) _lastClutchPress = Time.time;
    }

    private bool TryResolveClutch()
    {
        if (!HasPendingFallDamage || vitals == null || vitals.IsDead ||
            !YQPlayerMotionRules.InClutchWindow(_pendingLandingAt, _lastClutchPress, Time.time, clutchWindowSeconds)) return false;
        if (IsRagdoll && !_ragdoll.TryRecover(true)) return false;
        ResolveFallDamage(true);
        // note: The survival action takes priority over dash cooldown; available stamina is spent without denying a valid clutch.
        if (!IsDodgeRolling)
        {
            vitals.SpendStamina(Mathf.Min(dashStaminaCost, vitals.CurrentStamina));
            _dashIsRoll = _dashStartedGrounded = _isCrouching = _wasCrouching = true;
            _dashDirection = transform.forward;
            _dashTimeRemaining = Mathf.Max(.05f, rollDuration);
            _nextDashTime = Time.time + dashCooldown;
            _dashStartedThisFrame = true;
            UpdateCrouchController(0f);
            actionRecorder?.RecordDodge();
            GetComponent<YQPlayerEquipmentVisual>()?.PlayRollFeedback();
        }
        _feedback?.Pulse(.3f, .9f, .2f);
        _lastClutchPress = float.NegativeInfinity;
        return true;
    }

    private void AdvancePendingLanding()
    {
        if (!HasPendingFallDamage) return;
        ReadClutchInput();
        if (TryResolveClutch()) return;
        if (Time.time - _pendingLandingAt > Mathf.Clamp(clutchWindowSeconds, .3f, .5f))
        {
            if (vitals != null && !vitals.IsDead && _pendingImpactSpeed >= ragdollImpactSpeed) EnterPhysicalRagdoll(ResolvedVelocity);
            ResolveFallDamage(false);
        }
    }

    private void ResolveFallDamage(bool clutch)
    {
        float damage = _pendingFallDamage;
        _pendingFallDamage = 0f;
        if (vitals == null || vitals.IsDead) return;
        PlayerState current = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (!ReferenceEquals(current, _landingOwner)) { _landingOwner = null; return; }
        if (clutch)
        {
            damage = YQPlayerMotionRules.ClutchDamage(damage, vitals.CurrentHealth, _preImpactHealth, vitals.MaximumHealth, Acrobatics(current));
            // note: Canonical behavior counters extend existing procedural progression without new save fields or a hardcoded skill offer.
            current?.IncCounter("skill:acrobatics:xp", 1f);
            current?.IncCounter("movement:clutch", 1f);
        }
        vitals.TakeDamage(damage);
        current?.IncCounter("movement:fall_damage", damage);
        _landingOwner = null;
    }

    public static float Acrobatics(PlayerState state)
    {
        if (state == null) return 0f;
        float level = state.behaviorCounters != null && state.behaviorCounters.TryGetValue("skill:acrobatics:xp", out float xp)
            ? Mathf.Clamp01(xp / 100f) : 0f;
        if (state.skills != null)
            foreach (SkillRecord skill in state.skills)
                if (skill != null && skill.unlocked && (string.Equals(skill.context, "acrobatics", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(skill.type, "acrobatics", StringComparison.OrdinalIgnoreCase)))
                    level = Mathf.Max(level, Mathf.Clamp01((skill.rank - 1f) / 19f));
        return level;
    }

    public void ApplyPhysicalImpulse(Vector3 deltaVelocity, bool protectsFromAcceleration = false)
    {
        // note: Structured executors may protect a launch explicitly; accepted impulses never bypass collision or terrain admission.
        if (!IsAuthoritative || !CanProcessMovementInput || !IsFinite(deltaVelocity)) return;
        Vector3 next = ResolvedVelocity + deltaVelocity;
        if (!protectsFromAcceleration && deltaVelocity.magnitude >= extremeVelocityChange &&
            deltaVelocity.magnitude / Mathf.Max(.01f, accelerationSampleSeconds) >= extremeAcceleration && EnterPhysicalRagdoll(next)) return;
        _planarVelocity += Vector3.ProjectOnPlane(deltaVelocity, Vector3.up);
        _verticalVelocity += deltaVelocity.y;
    }

    private static bool IsFinite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    private bool EnterPhysicalRagdoll(Vector3 velocity)
    {
        if (_ragdoll == null || !_ragdoll.TryBegin(velocity)) return false;
        _dashTimeRemaining = 0f;
        _isSprinting = _isClimbing = _isPowerSliding = false;
        StopSlideAudio();
        GetComponent<YQInvestorCombat>()?.InterruptActions();
        _feedback?.Pulse(.9f, .7f, .2f);
        return true;
    }

    public void ObserveRagdollLanding(float height, float impactSpeed)
    {
        // note: Physical launches use the same damage/clutch owner as controller-driven falls.
        if (IsRagdoll) QueueLanding(height, impactSpeed);
    }

    public void ResetTraversalForRespawn()
    {
        // note: A new life cannot inherit a buffered landing, recovery lock, roll, or impulse.
        _pendingFallDamage = 0f; _landingOwner = null;
        _lastClutchPress = float.NegativeInfinity;
        _getUpUntil = _dashTimeRemaining = 0f;
        _isPowerSliding = _fallVelocityPaused = false;
        _planarVelocity = Vector3.zero; _verticalVelocity = -2f;
        ResetMotionSample();
    }

    public void ResetMotionSample(bool resetFall = true)
    {
        _motionSamplePosition = transform.position;
        _sampleVelocity = ResolvedVelocity = Vector3.zero;
        _sampleElapsed = 0f;
        if (resetFall) _fallTracked = false;
    }

    // note: The existing fall-safety owner restores position; clear this motor's accumulated fall impulse before the next capsule step can drive it through the floor again.
    internal void ResetAfterFallRecovery()
    {
        _ragdoll?.ResetForTerrainRecovery();
        _planarVelocity = Vector3.zero;
        _verticalVelocity = -2f;
        _pausedFallVelocity = -2f;
        _supportVelocity = Vector3.zero;
        _isSwimming = _headUnderwater = false;
        ResetMotionSample();
    }

    public void CompleteRagdollRecovery(bool clutch)
    {
        _planarVelocity = Vector3.zero;
        _verticalVelocity = -2f;
        ResetMotionSample();
        _getUpUntil = clutch ? 0f : Time.time + .65f;
        if (!clutch) GetComponent<YQPlayerEquipmentVisual>()?.PlayGetUpFeedback();
    }

    private bool TryCompleteClimbLedge(float dt)
    {
        // note: Complete a climb through admitted capsule movement rather than teleporting across the lip or through an overhang.
        Vector3 probe = transform.position + Vector3.up * (_standingControllerHeight + .4f) + transform.forward * .65f;
        if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, 1.5f, climbMask, QueryTriggerInteraction.Ignore) || top.normal.y < .65f || top.transform.IsChildOf(transform)) return false;
        Vector3 target = top.point + Vector3.up * .06f;
        Vector3 center = target + _standingControllerCenter;
        float radius = _controller.radius;
        Vector3 offset = Vector3.up * Mathf.Max(0f, _standingControllerHeight * .5f - radius);
        int count = Physics.OverlapCapsuleNonAlloc(center - offset, center + offset, radius - .02f, _standingClearanceHits, ~0, QueryTriggerInteraction.Ignore);
        if (count == _standingClearanceHits.Length) return false;
        for (int i = 0; i < count; i++) if (_standingClearanceHits[i] != null && !_standingClearanceHits[i].transform.IsChildOf(transform) && _standingClearanceHits[i] != _controller) return false;
        Vector3 step = Vector3.ClampMagnitude(target - transform.position, climbSpeed * dt);
        CaptureMotionIntegrationState();
        MoveWithTraversalGate(step);
        _isClimbing = !_frameLanded;
        _verticalVelocity = 0f;
        return true;
    }

    private float ResolveEquipmentMoveBonus()
    {
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (!ReferenceEquals(state, _speedBonusOwner) || Time.time >= _speedBonusSampleAt)
        {
            _speedBonusOwner = state;
            _speedBonusSampleAt = Time.time + .15f;
            _cachedSpeedBonus = GeneratedRpgContentService.Instance != null ? GeneratedRpgContentService.Instance.GetMoveSpeedBonus(state) : 0f;
        }
        return _cachedSpeedBonus;
    }
}
