using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// note: A bounded input witness exercises the real motor and presentation; it never loads a fixture, changes gear, or damages the accepted player.
public static class YQPlayerAnimationRuntimeVerification
{
    private static YQInvestorPlayerMotor _motor;
    private static YQPlayerEquipmentVisual _visual;
    private static Animator _animator;
    private static Keyboard _keyboard;
    private static Keyboard _previousKeyboard;
    private static readonly List<string> Checks = new List<string>();
    private static readonly List<object> Samples = new List<object>();
    private static int _phase;
    private static int _frame;
    private static float _phaseAt;
    private static double _deadline;
    private static bool _originalFirstPerson;
    private static bool _sawJump;
    private static bool _sawDash;
    private static bool _sawCrouch;
    private static bool _sawRoll;
    private static Vector3 _phasePosition;
    private static Vector3 _samplePosition;
    private static float _directedDistance;
    private static bool _directionWitnessed;
    private static object _directionSample;
    private static string _acceptedInventory;
    private static Quaternion _handRotation;
    private static Vector3 _handPosition;
    private static float _minimumFoot;
    private static float _maximumFoot;
    private static Quaternion _initialLeg;
    private static float _legArc;
    private static int _gaitFrames;
    private static int _locomotionFrames;
    private static float _lateralLeanSum;
    private static float _rollMinimumHandY;
    private static bool _rollOnly;
    private static Key[] _heldKeys = Array.Empty<Key>();

    [MenuItem("YourQuest/Player Animation/Run Bounded Motion Witness in Current Play Session %#F11")]
    public static void Run()
    {
        Begin(false);
    }

    [MenuItem("YourQuest/Player Animation/Verify Current First Person Dodge Roll")]
    public static void RunRollOnly()
    {
        Begin(true);
    }

    private static void Begin(bool rollOnly)
    {
        if (_keyboard != null || !EditorApplication.isPlaying ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "YourQuest_PlaySafe" ||
            !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased || RuntimeModalUiBlocker.IsBlocked)
        {
            Debug.LogWarning("[YourQuest Player Animation] Complete the PlaySafe title flow before running the motion witness.");
            return;
        }
        _motor = YQInvestorPlayerMotor.ActiveMotor;
        _visual = _motor != null ? _motor.GetComponent<YQPlayerEquipmentVisual>() : null;
        _animator = _visual != null ? _visual.CharacterAnimator : null;
        _originalFirstPerson = _motor != null && _motor.firstPerson;
        Checks.Clear();
        Samples.Clear();
        try
        {
            Require(_motor != null && _motor.CanProcessMovementInput && _animator != null, "one active production motor and animation rig");
            Require(_animator.runtimeAnimatorController == Resources.Load<RuntimeAnimatorController>(YQPlayerEquipmentVisual.PlayerAnimatorResourcePath),
                "production player binds the project controller (actual=" + (_animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "missing") + ")");
            _acceptedInventory = Inventory();
            _rollOnly = rollOnly;
            // note: The visible regression was third-person gait; run physical direction checks in that view before exercising first-person arms.
            _motor.firstPerson = false;
            _previousKeyboard = Keyboard.current;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _keyboard.MakeCurrent();
            InputSystem.onAfterUpdate += MaintainWitnessKeyboard;
            _phase = rollOnly ? 28 : 0;
            _frame = -1;
            _sawJump = _sawDash = _sawCrouch = _sawRoll = false;
            _rollMinimumHandY = float.PositiveInfinity;
            _phaseAt = Time.time;
            _phasePosition = _motor.transform.position;
            ResetDirectionSample();
            _deadline = EditorApplication.timeSinceStartup + 55d;
            EditorApplication.update += Tick;
            Queue();
        }
        catch (Exception error) { Finish("FAIL", error.ToString()); }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > _deadline)
        {
            Finish("BLOCKED", "Play session ended or the 55-second motion witness deadline expired.");
            return;
        }
        if (_frame == Time.frameCount) return;
        _frame = Time.frameCount;
        // note: Requeue bounded test input after focus changes; physical key-up events from invoking the menu must not steal the witness keyboard.
        Queue(_heldKeys);
        try
        {
            Require(_motor.IsAuthoritative && _animator.isActiveAndEnabled, "animator stays active on the authoritative player");
            if (_motor.IsGrounded && _animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.JumpRise") && Time.time - _phaseAt > 0.3f)
                throw new InvalidOperationException("The jump pose remained active after physical landing.");
            _sawJump |= !_motor.IsGrounded && _motor.VerticalVelocity > 0f;
            _sawDash |= _motor.IsDashing || _motor.DashStartedThisFrame;
            _sawCrouch |= _motor.IsCrouching;
            _sawRoll |= _motor.IsDodgeRolling && _animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.DodgeRoll");
            ObserveDirection();
            ObserveGait();
            // note: Observe the real first-person roll across frames, including its deepest tuck; a final-state-only test misses the animation entirely.
            if ((_phase == 27 || _phase == 30) && _visual.FirstPersonArms != null)
                _rollMinimumHandY = Mathf.Min(_rollMinimumHandY, _visual.FirstPersonArms.transform.InverseTransformPoint(_visual.FirstPersonArms.RightHand.position).y);
            float elapsed = Time.time - _phaseAt;
            // note: Stopping includes the real motor's deceleration plus animator damping; allow that settle time before asserting idle.
            float duration = _phase == 7 || _phase == 11 ? 1.1f : _phase == 9 ? 0.8f : 0.4f;
            if (_phase == 14 || _phase == 17) duration = 0.18f;
            if (_phase == 16 || _phase == 18) duration = 0.8f;
            if (_phase == 20) duration = .9f;
            if (_phase == 27 || _phase == 30) duration = 1.2f;
            if (_phase == 22 || _phase == 24 || _phase == 26) duration = 3f;
            if ((_phase >= 2 && _phase <= 5) || _phase == 10) duration = 1.2f;
            if (elapsed < duration) return;
            if (_phase >= 2 && _phase <= 5 && elapsed < 1.5f && (!_directionWitnessed || _directedDistance <= 0.1f))
                return;
            if (_phase == 11 && elapsed < 4f && (_motor.PlanarVelocity.sqrMagnitude >= 0.01f ||
                Mathf.Abs(_animator.GetFloat("MoveX")) + Mathf.Abs(_animator.GetFloat("MoveZ")) >= 0.04f))
                return;
            Samples.Add(new { phase = _phase, firstPerson = _motor.firstPerson, displacement = (_motor.transform.position - _phasePosition).ToString("F3"), moveX = _animator.GetFloat("MoveX"), moveZ = _animator.GetFloat("MoveZ"), directedDistance = _directedDistance, directionWitness = _directionSample, grounded = _motor.IsGrounded, crouching = _motor.IsCrouching, state = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash, normalizedTime = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime, gaitRate = _animator.GetFloat("LocomotionRate"), footTravel = _gaitFrames > 0 ? _maximumFoot - _minimumFoot : 0f, legArc = _legArc, gaitFrames = _gaitFrames, locomotionFrames = _locomotionFrames });
            switch (_phase)
            {
                case 0:
                    Require(Mathf.Abs(_animator.GetFloat("MoveX")) + Mathf.Abs(_animator.GetFloat("MoveZ")) < 0.04f, "stationary idle uses resolved motion");
                    Vector3 upright = _animator.GetBoneTransform(HumanBodyBones.Head).position - _animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    Require(Vector3.Angle(upright, Vector3.up) < 12f, "production third-person idle is upright");
                    foreach (Collider collider in _visual.CharacterPresentationRoot.GetComponentsInChildren<Collider>(true))
                        Require(!collider.enabled, "cosmetic hierarchy has no enabled colliders");
                    Require(_visual.CharacterPresentationRoot.GetComponentsInChildren<Animator>(true).Length == 1, "body and equipment use exactly one Animator");
                    CheckEquipmentBindings();
                    Queue();
                    break;
                case 1:
                    Require(!_motor.firstPerson && _visual.CharacterPresentationRoot.gameObject.activeInHierarchy, "third-person physical gait keeps the same live skeletal rig");
                    Queue(Key.W);
                    break;
                case 2:
                    CheckDirection("forward");
                    Queue(Key.S);
                    break;
                case 3:
                    CheckDirection("backward");
                    Queue(Key.A);
                    break;
                case 4:
                    CheckDirection("left strafe");
                    Queue(Key.D);
                    break;
                case 5:
                    CheckDirection("right strafe");
                    Queue(Key.LeftCtrl, Key.W);
                    break;
                case 6:
                    Require(_sawCrouch, "motor accepts crouch while the skeleton remains animated");
                    Queue(Key.Space);
                    break;
                case 7:
                    Require(_sawJump && _motor.IsGrounded, "accepted jump rises and returns to physical ground");
                    Require(!_animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Dash"), "ordinary jump never ends in a dash or roll");
                    Queue(Key.Q);
                    break;
                case 8:
                    Require(_sawDash, "accepted simple dash uses the existing motor");
                    Queue();
                    // note: This step calls only the presentation boundary, so combat targets, stamina, mana, and loot remain untouched by action checks.
                    _visual.PlayMeleeFeedback();
                    _visual.PlayAbilityFeedback("cast");
                    break;
                case 9:
                    Require(_animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Empty"), "replaced action returns without a stuck cast or queued melee");
                    Queue(Key.W, Key.LeftShift);
                    break;
                case 10:
                    Require(_motor.IsSprinting, "accepted sprint remains connected to locomotion");
                    CheckGait("sprint");
                    Require(Mathf.Abs(_lateralLeanSum / Mathf.Max(1, _gaitFrames)) < 3f, "straight sprint has no sustained sideways torso bank");
                    Samples.Add(new { sprintMeanLateralLeanDegrees = _lateralLeanSum / Mathf.Max(1, _gaitFrames) });
                    Queue();
                    break;
                case 11:
                    Require(_motor.PlanarVelocity.sqrMagnitude < 0.01f, "motor has completed sprint deceleration");
                    Require(Mathf.Abs(_animator.GetFloat("MoveX")) + Mathf.Abs(_animator.GetFloat("MoveZ")) < 0.04f, "release returns to idle after deceleration");
                    _motor.firstPerson = _originalFirstPerson;
                    Queue();
                    break;
                case 12:
                    Require(_motor.firstPerson == _originalFirstPerson && _animator.isActiveAndEnabled, "camera and active rig restored after both views");
                    Require(Inventory() == _acceptedInventory, "accepted inventory and equipment records preserved");
                    // note: First-person checks use the same source Animator and presentation callbacks; no gameplay attack or spell is committed.
                    _motor.firstPerson = true;
                    Queue();
                    break;
                case 13:
                    Require(_visual.FirstPersonArms != null && _visual.FirstPersonArms.ArmRendererCount > 0 && _visual.FirstPersonArms.gameObject.activeInHierarchy, "first-person arms bind the active production body mesh");
                    Require(_visual.FirstPersonArms.GetComponentsInChildren<Animator>(true).Length == 0, "first-person arms have no independent Animator");
                    Require(_visual.FirstPersonArms.GetComponentsInChildren<Collider>(true).Length == 0, "first-person arms have no gameplay colliders");
                    _handRotation = _visual.FirstPersonArms.RightHand.rotation;
                    _handPosition = _visual.FirstPersonArms.RightHand.position;
                    _visual.PlayMeleeFeedback();
                    break;
                case 14:
                    Require(Quaternion.Angle(_handRotation, _visual.FirstPersonArms.RightHand.rotation) > 2f || Vector3.Distance(_handPosition, _visual.FirstPersonArms.RightHand.position) > 0.01f, "authored melee changes the visible first-person hand pose");
                    _visual.PlayAbilityFeedback("cast");
                    break;
                case 15:
                    Require(_animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Cast") || _animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.CastRelease"), "first-person spell gesture uses the source action layer");
                    break;
                case 16:
                    Require(_animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Empty"), "first-person spell gesture recovers to ready hands");
                    _visual.PlayDamageFeedback(false);
                    break;
                case 17:
                    Require(_animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Hit"), "first-person damage reaction uses the existing presentation boundary");
                    break;
                case 18:
                    Require(_animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Empty"), "first-person damage reaction recovers");
                    _motor.firstPerson = _originalFirstPerson;
                    break;
                case 19:
                    Require(_motor.firstPerson == _originalFirstPerson && Inventory() == _acceptedInventory, "original view and accepted equipment restored after first-person checks");
                    _motor.firstPerson = false;
                    Queue(Key.LeftCtrl, Key.Q, Key.W);
                    break;
                case 20:
                    Require(_sawRoll, "accepted crouch-dash presents a full-body dodge roll");
                    Queue();
                    _motor.firstPerson = true;
                    _visual.PlayInteractionFeedback();
                    break;
                case 21:
                    Require(_animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Interact"), "accepted interaction callback presents a reach gesture");
                    break;
                case 22:
                    Require(_animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Empty"), "interaction gesture recovers");
                    _visual.PlayInteractionFeedback(true);
                    break;
                case 23:
                    Require(_animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Pickup"), "accepted pickup callback presents a reach gesture");
                    break;
                case 24:
                    Require(_animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Empty"), "pickup gesture recovers");
                    _visual.PlayConsumeFeedback();
                    break;
                case 25:
                    Require(_animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Consume"), "accepted consume callback presents the authored motion");
                    break;
                case 26:
                    Require(_animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Empty"), "consume gesture recovers");
                    Require(Inventory() == _acceptedInventory, "expanded presentation witness preserves inventory and equipment");
                    Queue(Key.LeftCtrl, Key.Q);
                    break;
                case 27:
                    Require(_rollMinimumHandY < -.65f, "accepted first-person dodge roll visibly tucks the hands");
                    Require(_visual.FirstPersonArms.transform.InverseTransformPoint(_visual.FirstPersonArms.RightHand.position).y > -.4f, "first-person dodge roll returns to ready hands");
                    Finish("PASS", null);
                    return;
                case 28:
                    // note: A roll-only witness begins at rest so a preceding long movement test cannot carry it into a cliff or obstruction.
                    Require(_motor.CanProcessMovementInput && _motor.IsGrounded, "roll witness starts on physical ground with gameplay input available");
                    _motor.firstPerson = true;
                    Queue(Key.LeftCtrl);
                    break;
                case 29:
                    Require(_motor.IsCrouching, "production motor accepts crouch before the roll press");
                    Require(_visual.FirstPersonArms != null, "first-person roll uses the active mirrored arms");
                    Queue(Key.LeftCtrl, Key.Q);
                    break;
                case 30:
                    Samples.Add(new { rollMinimumHandY = _rollMinimumHandY, rollObserved = _sawRoll, inputAvailable = _motor.CanProcessMovementInput, focused = Application.isFocused, crouchDelivered = _keyboard.leftCtrlKey.isPressed });
                    Require(_sawRoll, "production crouch-dash enters the authored full-body roll");
                    Require(_rollMinimumHandY < -.65f, "first-person roll tucks the hands through inversion");
                    Require(!_motor.IsDashing && _visual.FirstPersonArms.transform.InverseTransformPoint(_visual.FirstPersonArms.RightHand.position).y > -.4f, "accepted roll completes and first-person hands recover");
                    Require(Inventory() == _acceptedInventory, "roll witness preserves accepted inventory and equipment");
                    Finish("PASS", null);
                    return;
            }
            _phase++;
            _phaseAt = Time.time;
            _phasePosition = _motor.transform.position;
            ResetDirectionSample();
        }
        catch (Exception error) { Finish("FAIL", error.ToString()); }
    }

    private static void ResetDirectionSample()
    {
        _samplePosition = _motor.transform.position;
        _directedDistance = 0f;
        _directionWitnessed = false;
        _directionSample = null;
        _minimumFoot = float.PositiveInfinity;
        _maximumFoot = float.NegativeInfinity;
        _initialLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation;
        _legArc = 0f;
        _gaitFrames = _locomotionFrames = 0;
        _lateralLeanSum = 0f;
    }

    private static void ObserveDirection()
    {
        // note: Pair the signed animation value with real motion during the phase; a later collision correctly blends back to idle.
        Vector3 delta = _motor.transform.InverseTransformDirection(_motor.transform.position - _samplePosition);
        _samplePosition = _motor.transform.position;
        if (_phase < 2 || _phase > 5) return;
        bool lateral = _phase >= 4;
        float sign = _phase == 3 || _phase == 4 ? -1f : 1f;
        float distance = (lateral ? delta.x : delta.z) * sign;
        _directedDistance += Mathf.Max(0f, distance);
        float animation = _animator.GetFloat(lateral ? "MoveX" : "MoveZ");
        if (distance > 0.001f && animation * sign > 0.03f)
        {
            _directionWitnessed = true;
            _directionSample = new { frame = Time.frameCount, localDelta = delta.ToString("F3"), animation };
        }
    }

    private static void CheckDirection(string label)
    {
        Require(_directedDistance > 0.1f, "physical movement available: " + label);
        Require(_directionWitnessed, "resolved animation direction during physical motion: " + label);
        CheckGait(label);
    }

    private static void ObserveGait()
    {
        // note: Measure rendered skeletal movement during real resolved travel, rather than accepting a state-name-only animation test.
        if (!((_phase >= 2 && _phase <= 5) || _phase == 10) || _motor.PlanarVelocity.sqrMagnitude < 1f) return;
        _gaitFrames++;
        Vector3 torso = _animator.transform.InverseTransformDirection(_animator.GetBoneTransform(HumanBodyBones.Head).position - _animator.GetBoneTransform(HumanBodyBones.Hips).position);
        _lateralLeanSum += Mathf.Atan2(torso.x, torso.y) * Mathf.Rad2Deg;
        if (_animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Locomotion")) _locomotionFrames++;
        float foot = _animator.transform.InverseTransformPoint(_animator.GetBoneTransform(HumanBodyBones.LeftFoot).position).z * _animator.transform.lossyScale.z;
        _minimumFoot = Mathf.Min(_minimumFoot, foot);
        _maximumFoot = Mathf.Max(_maximumFoot, foot);
        _legArc = Mathf.Max(_legArc, Quaternion.Angle(_initialLeg, _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation));
    }

    private static void CheckGait(string label)
    {
        Require(_gaitFrames >= 5 && _locomotionFrames >= _gaitFrames * 0.75f, "continuous physical locomotion pose: " + label);
        Require(_maximumFoot - _minimumFoot > 0.25f && _legArc > 15f, "visible foot travel and leg stride during physical motion: " + label);
    }

    private static void Queue(params Key[] keys)
    {
        _heldKeys = keys;
        _keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
    }

    private static void MaintainWitnessKeyboard()
    {
        if (_keyboard != null && _keyboard.added) _keyboard.MakeCurrent();
    }

    private static void CheckEquipmentBindings()
    {
        // note: Native armor must render from canonical weighted bones, rather than its imported bind skeleton. This is read-only on the selected loadout.
        Transform gear = _animator.transform.Find("YQ_EquippedItemModels");
        Require(gear != null, "production equipment has one presentation root");
        int skins = 0;
        foreach (SkinnedMeshRenderer skin in gear.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.sharedMesh == null) continue;
            skins++;
            var used = new HashSet<int>();
            foreach (BoneWeight weight in skin.sharedMesh.boneWeights)
            {
                if (weight.weight0 > 0.001f) used.Add(weight.boneIndex0);
                if (weight.weight1 > 0.001f) used.Add(weight.boneIndex1);
                if (weight.weight2 > 0.001f) used.Add(weight.boneIndex2);
                if (weight.weight3 > 0.001f) used.Add(weight.boneIndex3);
            }
            Transform[] bones = skin.bones;
            foreach (int index in used)
                Require(index < bones.Length && bones[index] != null && !bones[index].IsChildOf(gear), "weighted armor bones follow the canonical body: " + skin.name);
        }
        var state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        string bootsPath = state != null ? state.GetEquippedItem("boots")?.prefabKey ?? "" : "";
        if (bootsPath.Contains("/Male Human Armor/") && (bootsPath.EndsWith("_Left.prefab") || bootsPath.EndsWith("_Right.prefab")))
            Require(gear.Find("Anchor_boots").GetComponentsInChildren<SkinnedMeshRenderer>(true).Length >= 2, "single equipped boot record renders the approved pair");
        Samples.Add(new { equipmentSkins = skins, cosmeticColliders = gear.GetComponentsInChildren<Collider>(true).Length });
    }

    private static string Inventory()
    {
        var state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        return JsonConvert.SerializeObject(new { inventory = state != null ? state.inventoryItems : null, equipment = state != null ? state.equippedItemBySlot : null });
    }

    private static void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException(check);
        // note: Keep the per-frame authority/animation check out of the receipt's unbounded hot path.
        if (Checks.Count == 0 || Checks[Checks.Count - 1] != check) Checks.Add(check);
    }

    private static void Finish(string status, string error)
    {
        EditorApplication.update -= Tick;
        InputSystem.onAfterUpdate -= MaintainWitnessKeyboard;
        if (_keyboard != null)
        {
            InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
        }
        _heldKeys = Array.Empty<Key>();
        if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
        if (_motor != null) _motor.firstPerson = _originalFirstPerson;
        string receipt = _rollOnly ? "Player_Animation_Roll_Runtime_Receipt_2026-10-02.json" : "Player_Animation_Runtime_Receipt_2026-10-02.json";
        File.WriteAllText(Path.Combine(Application.dataPath, "../Docs/" + receipt),
            JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, mode = "PlaySafe production motor with synthetic keyboard input; melee/cast/hit use presentation callbacks only; live gameplay damage, casting, death and climb are separate acceptance checks", checks = Checks.ToArray(), samples = Samples.ToArray(), error }, Formatting.Indented));
        if (status == "PASS") Debug.Log("[YourQuest Player Animation] Bounded production motion witness PASS.");
        else Debug.LogWarning("[YourQuest Player Animation] Motion witness " + status + ": " + error);
    }
}
