using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public sealed class YQPlayerEquipmentVisual : MonoBehaviour
{
    private const string HumanMalePrefabPath = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters/Human Male (v4.1.1).prefab";
    private const string HumanAnimatorControllerPath = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Demo Files/Human (Male & Female).controller";
    private const float ThirdPersonAvatarHeight = 1.86f;
    private const float AvatarGroundClearance = 0.015f;
    private const float FirstPersonWeaponMaximumSize = 0.29f;
    private const float FirstPersonOffhandMaximumSize = 0.24f;
    private static readonly string[] BaseTextureProperties = { "_BaseMap", "_MainTex", "_Albedo", "_BaseColorMap", "_DiffuseMap", "_ColorMap" };

    private readonly struct SlotVisual
    {
        public readonly string Slot;
        public readonly Vector3 Position;
        public readonly Vector3 Euler;
        public readonly float MaxSize;

        public SlotVisual(string slot, Vector3 position, Vector3 euler, float maxSize)
        {
            Slot = slot;
            Position = position;
            Euler = euler;
            MaxSize = maxSize;
        }
    }

    private static readonly SlotVisual[] Slots =
    {
        new SlotVisual("weapon", new Vector3(0.34f, 1.02f, 0.12f), new Vector3(12f, 4f, 84f), 0.46f),
        new SlotVisual("offhand", new Vector3(-0.34f, 1.00f, 0.12f), new Vector3(12f, -14f, -70f), 0.36f),
        new SlotVisual("head", new Vector3(0f, 1.67f, 0.01f), Vector3.zero, 0.36f),
        new SlotVisual("chest", new Vector3(0f, 1.08f, 0.02f), Vector3.zero, 0.58f),
        new SlotVisual("gloves", new Vector3(0.34f, 0.95f, 0.03f), new Vector3(0f, 0f, 12f), 0.24f),
        new SlotVisual("belt", new Vector3(0f, 0.84f, 0.02f), Vector3.zero, 0.42f),
        new SlotVisual("legs", new Vector3(0f, 0.58f, 0.01f), Vector3.zero, 0.48f),
        new SlotVisual("boots", new Vector3(0.15f, 0.13f, 0.04f), Vector3.zero, 0.24f),
        new SlotVisual("necklace", new Vector3(0f, 1.39f, 0.10f), new Vector3(0f, 0f, 0f), 0.22f),
        new SlotVisual("ring_left", new Vector3(-0.41f, 0.96f, 0.17f), Vector3.zero, 0.09f),
        new SlotVisual("ring_right", new Vector3(0.41f, 0.96f, 0.17f), Vector3.zero, 0.09f),
        new SlotVisual("trinket", new Vector3(0f, 0.96f, -0.14f), Vector3.zero, 0.18f)
    };

    private static readonly string[] NativeWearableSlots = { "head", "chest", "gloves", "belt", "legs", "boots", "cloak" };

    public float pollIntervalSeconds = 0.35f;
    public bool allowImportedAvatarInPlay = true;
    public bool allowImportedEquipmentInPlay = true;

    private Transform _visualRig;
    private Transform _avatarRoot;
    private Transform _equipmentRoot;
    private Transform _firstPersonRoot;
    private Transform _firstPersonWeaponAnchor;
    private Transform _firstPersonOffhandAnchor;
    private Vector3 _firstPersonWeaponBasePosition;
    private Vector3 _firstPersonOffhandBasePosition;
    private Quaternion _firstPersonWeaponBaseRotation = Quaternion.identity;
    private Quaternion _firstPersonOffhandBaseRotation = Quaternion.identity;
    private readonly Dictionary<string, Transform> _anchors = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Transform> _avatarBoneMap = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
    private CharacterController _controller;
    private YQInvestorPlayerMotor _motor;
    private Animator _animator;
    private string _lastSignature = string.Empty;
    private string _lastFirstPersonSignature = string.Empty;
    private float _nextPollTime;
    private float _meleeKickUntil;
    private float _castKickUntil;
    private float _castAnimatorEndAt;
    private float _rollKickUntil;
    private float _jumpKickUntil;
    private float _meleeSide = 1f;
    private float _crouchBlend;
    private string _pendingCastAnimatorEndTrigger = string.Empty;
    private float _nextThirdPersonVisibilityRepairTime;
    private float _nextGhostVisualSweepTime;
    private Vector3 _rigBaseLocalPosition;
    private bool _legacyVisualCleaned;
    private bool _lastDashVisualState;
    private bool _strayVisualCleanupPerformed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallOnPlayer()
    {
        EnsureVisualOnActivePlayer();
        EnsureRepairLoop();
    }

    internal static void RepairActivePlayerVisualBinding()
    {
        YQPlayerEquipmentVisual visual = EnsureVisualOnActivePlayer();
        if (visual == null)
            return;

        visual.BindToActiveMotor();
        if (!visual.IsLocalFirstPerson())
            visual.ForceThirdPersonVisibleNow();
    }

    private static YQPlayerEquipmentVisual EnsureVisualOnActivePlayer()
    {
        GameObject player = YQInvestorPlayerMotor.ActiveMotor != null
            ? YQInvestorPlayerMotor.ActiveMotor.gameObject
            : GameObject.FindWithTag("Player");
        if (player == null)
            return null;

        YQPlayerEquipmentVisual visual = player.GetComponent<YQPlayerEquipmentVisual>();
        if (visual == null)
            visual = player.AddComponent<YQPlayerEquipmentVisual>();

        visual.enabled = true;
        return visual;
    }

    private static void EnsureRepairLoop()
    {
        if (FindFirstObjectByType<YQPlayerVisualAuthorityRepair>() != null)
            return;

        GameObject repair = new GameObject("__YQ_PlayerVisualAuthorityRepair");
        DontDestroyOnLoad(repair);
        repair.hideFlags = HideFlags.DontSave;
        repair.AddComponent<YQPlayerVisualAuthorityRepair>();
    }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _motor = GetComponent<YQInvestorPlayerMotor>();
        CleanupLegacyPrimitiveVisual();
        EnsureVisualRig();
        EnsureAvatar();
        RebuildEquipment();
    }

    private void Update()
    {
        if (_motor == null)
            _motor = GetComponent<YQInvestorPlayerMotor>();
        if (_motor != null && !_motor.IsAuthoritative)
        {
            if (_visualRig != null)
                _visualRig.gameObject.SetActive(false);
            if (_firstPersonRoot != null)
                _firstPersonRoot.gameObject.SetActive(false);
            return;
        }

        if (!_legacyVisualCleaned)
            CleanupLegacyPrimitiveVisual();
        EnsureVisualRig();
        LockVisualRigToPlayer();
        EnsureAvatar();
        CleanupStrayPlayerVisualsOnce();
        EnsureAvatarHasSaneLocalAnchor();
        bool firstPerson = IsLocalFirstPerson();
        SweepGhostPlayerVisuals();
        ApplyPerspectiveVisibility(firstPerson);
        UpdateFirstPersonEquipment(firstPerson);
        AnimateRig(firstPerson);
        FlushPendingCastAnimatorEnd();

        if (Time.unscaledTime < _nextPollTime)
            return;

        _nextPollTime = Time.unscaledTime + Mathf.Max(0.1f, pollIntervalSeconds);
        string signature = BuildEquipmentSignature();
        if (string.Equals(signature, _lastSignature, StringComparison.OrdinalIgnoreCase))
            return;

        _lastSignature = signature;
        RebuildEquipment();
    }

    public void PlayMeleeFeedback()
    {
        _meleeKickUntil = Time.time + 0.22f;
        _meleeSide = _meleeSide >= 0f ? -1f : 1f;
        if (!TriggerFirstAnimator(
                _meleeSide > 0f ? "attack1Right" : "attack1Left",
                _meleeSide > 0f ? "attack2Right" : "attack2Left",
                "attack1Right",
                "attack1Left",
                "Attack",
                "attack",
                "Attack1",
                "attack1",
                "Slash",
                "slash",
                "Melee",
                "melee"))
        {
            PlayFirstAnimatorState("attack1Right", "attack1Left", "Attack", "attack", "Slash", "slash");
        }
    }

    public void PlayCastFeedback()
    {
        _castKickUntil = Time.time + 0.28f;
        if (TriggerFirstAnimator("bCast"))
        {
            QueueCastAnimatorEnd("bCastEnd", 0.32f);
            return;
        }

        if (TriggerFirstAnimator("cast1"))
            QueueCastAnimatorEnd("cast1End", 0.32f);
    }

    public void PlayRollFeedback()
    {
        _rollKickUntil = Time.time + 0.20f;
        TriggerFirstAnimator("Dash", "dash");
    }

    public void PlayJumpFeedback()
    {
        _jumpKickUntil = Time.time + 0.28f;
    }

    private void QueueCastAnimatorEnd(string triggerName, float delaySeconds)
    {
        if (string.IsNullOrWhiteSpace(triggerName))
            return;

        _pendingCastAnimatorEndTrigger = triggerName;
        _castAnimatorEndAt = Time.time + Mathf.Max(0.05f, delaySeconds);
    }

    private void FlushPendingCastAnimatorEnd()
    {
        if (string.IsNullOrWhiteSpace(_pendingCastAnimatorEndTrigger))
            return;
        if (Time.time < _castAnimatorEndAt)
            return;
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
            return;

        string triggerName = _pendingCastAnimatorEndTrigger;
        _pendingCastAnimatorEndTrigger = string.Empty;
        _castAnimatorEndAt = 0f;

        if (!TriggerFirstAnimator(triggerName))
            PlayFirstAnimatorState("Locomotion", "Idle_Combat", "Idle");
    }

    private void EnsureVisualRig()
    {
        if (_visualRig != null)
            return;

        Transform existing = transform.Find("YQ_PlayerVisualRig");
        if (existing != null)
            _visualRig = existing;
        else
        {
            GameObject rig = new GameObject("YQ_PlayerVisualRig");
            rig.transform.SetParent(transform, false);
            _visualRig = rig.transform;
        }

        _visualRig.localPosition = Vector3.zero;
        _visualRig.localRotation = Quaternion.identity;
        _visualRig.localScale = Vector3.one;
        _rigBaseLocalPosition = _visualRig.localPosition;
        CleanupLegacyPrimitiveVisual();
    }

    private void BindToActiveMotor()
    {
        if (YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.gameObject == gameObject)
            _motor = YQInvestorPlayerMotor.ActiveMotor;
        else if (_motor == null)
            _motor = GetComponent<YQInvestorPlayerMotor>();
    }

    private void EnsureAvatar()
    {
        if (_avatarRoot != null)
            return;

        Transform existing = _visualRig != null ? _visualRig.Find("YQ_HumanMaleAvatar") : null;
        if (existing != null)
        {
            _avatarRoot = existing;
            CleanupDuplicateRigAvatars();
            ConfigureAvatar(existing.gameObject);
            return;
        }

        GameObject prefab = CanUseImportedAvatar() ? LoadPrefab(HumanMalePrefabPath) : null;
        if (prefab == null)
        {
            Debug.LogError("[YourQuest] Missing animated male player avatar prefab: " + HumanMalePrefabPath);
            return;
        }

        GameObject avatar = Instantiate(prefab, _visualRig);

        avatar.name = "YQ_HumanMaleAvatar";
        _avatarRoot = avatar.transform;
        _avatarRoot.localPosition = Vector3.zero;
        _avatarRoot.localRotation = Quaternion.identity;
        _avatarRoot.localScale = Vector3.one;
        CleanupDuplicateRigAvatars();
        ConfigureAvatar(avatar);
    }

    private void ConfigureAvatar(GameObject avatar)
    {
        if (avatar == null)
            return;

        CleanupGeneratedBodyFallbacks(avatar.transform);
        DisableImportedAvatarDemoMagic(avatar.transform);
        PrepareVisualInstance(avatar, true);
        StripImportedAvatarAudio(avatar);
        ActivateLikelyAvatarBodyRenderers(avatar.transform);
        NormalizeAvatar(avatar);

        Animator animator = avatar.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            RuntimeAnimatorController controller = CanUseImportedAvatar() ? LoadAnimatorController(HumanAnimatorControllerPath) : null;
            if (controller != null && animator.runtimeAnimatorController == null)
                animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.keepAnimatorStateOnDisable = true;
            _animator = animator;
            ResetAnimatorToIdle();
        }

        EnsureAnimationEventReceivers(avatar);
        YQRuntimeUrpMaterialRepair.RepairHierarchy(avatar);
    }

    private void RebuildEquipment()
    {
        if (_visualRig == null)
            return;

        if (_equipmentRoot != null)
            Destroy(_equipmentRoot.gameObject);

        Transform equipmentParent = _avatarRoot != null ? _avatarRoot : _visualRig;
        GameObject root = new GameObject("YQ_EquippedItemModels");
        root.transform.SetParent(equipmentParent, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        _equipmentRoot = root.transform;
        _anchors.Clear();

        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (state == null)
            return;

        state.EnsureCollections();
        string weaponId = state.GetEquippedItem("weapon")?.itemId ?? string.Empty;

        for (int i = 0; i < Slots.Length; i++)
        {
            SlotVisual slot = Slots[i];
            InventoryItemRecord item = state.GetEquippedItem(slot.Slot);
            if (item == null)
                continue;
            if (string.Equals(slot.Slot, "offhand", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(weaponId) &&
                string.Equals(item.itemId, weaponId, StringComparison.OrdinalIgnoreCase))
                continue;

            GameObject itemVisual = CreateItemVisual(item, slot);
            bool nativeWearable = ShouldUseNativeWearableFit(item, slot);
            Transform anchor = CreateAnchor(slot, nativeWearable);
            itemVisual.transform.SetParent(anchor, false);
            itemVisual.transform.localPosition = Vector3.zero;
            itemVisual.transform.localRotation = Quaternion.identity;
            itemVisual.transform.localScale = Vector3.one;
            PrepareVisualInstance(itemVisual);
            YQRuntimeUrpMaterialRepair.RepairHierarchy(itemVisual);
            if (nativeWearable)
            {
                NormalizeNativeWearableInstance(itemVisual);
                BindNativeWearableToAvatar(itemVisual);
            }
            else
            {
                NormalizeEquippedInstance(itemVisual, anchor, slot.MaxSize);
            }
            ApplyItemTint(itemVisual, item);

            if (LooksMagical(item))
                AddMagicLoop(itemVisual.transform, ResolveItemColor(item));
        }
    }

    private Transform CreateAnchor(SlotVisual slot, bool nativeWearable)
    {
        if (_anchors.TryGetValue(slot.Slot, out Transform existing) && existing != null)
            return existing;

        GameObject anchor = new GameObject("Anchor_" + slot.Slot);
        anchor.transform.SetParent(_equipmentRoot, false);
        anchor.transform.localPosition = nativeWearable ? Vector3.zero : slot.Position;
        anchor.transform.localRotation = nativeWearable ? Quaternion.identity : Quaternion.Euler(slot.Euler);
        anchor.transform.localScale = Vector3.one;
        if (!nativeWearable)
            AttachBoneFollower(anchor.transform, slot.Slot);
        _anchors[slot.Slot] = anchor.transform;
        return anchor.transform;
    }

    private void AttachBoneFollower(Transform anchor, string slot)
    {
        Transform target = ResolveSlotBone(slot);
        if (anchor == null || target == null)
            return;

        YQEquipmentBoneFollower follower = anchor.gameObject.GetComponent<YQEquipmentBoneFollower>();
        if (follower == null)
            follower = anchor.gameObject.AddComponent<YQEquipmentBoneFollower>();
        if (TryGetDirectHandAttachment(slot, out Vector3 localPosition, out Quaternion localRotation))
            follower.Bind(target, localPosition, localRotation);
        else
            follower.Bind(target, anchor);
    }

    private static bool TryGetDirectHandAttachment(string slot, out Vector3 localPosition, out Quaternion localRotation)
    {
        localPosition = Vector3.zero;
        localRotation = Quaternion.identity;
        if (string.IsNullOrWhiteSpace(slot))
            return false;

        if (string.Equals(slot, "weapon", StringComparison.OrdinalIgnoreCase))
        {
            localPosition = new Vector3(0.035f, 0.015f, 0.055f);
            localRotation = Quaternion.Euler(8f, 4f, 88f);
            return true;
        }

        if (string.Equals(slot, "offhand", StringComparison.OrdinalIgnoreCase))
        {
            localPosition = new Vector3(-0.025f, 0.005f, 0.045f);
            localRotation = Quaternion.Euler(8f, -12f, -72f);
            return true;
        }

        return false;
    }

    private Transform ResolveSlotBone(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return null;

        string normalized = slot.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "weapon":
            case "ring_right":
                return FirstBone(HumanBodyBones.RightHand, "HandBoneR", "Base HumanRPalm", "Base HumanRForearm1");
            case "offhand":
            case "ring_left":
                return FirstBone(HumanBodyBones.LeftHand, "ShieldBoneL", "HandBoneL", "Base HumanLPalm", "Base HumanLForearm1");
            case "head":
                return FirstBone(HumanBodyBones.Head, "Base HumanHead", "Base HumanNeck");
            case "necklace":
                return FirstBone(HumanBodyBones.Neck, "Base HumanNeck", "Base HumanHead");
            case "chest":
            case "cloak":
                return FirstBone(HumanBodyBones.Chest, "Base HumanRibcage", "Base HumanSpine3", "Base HumanSpine2");
            case "gloves":
                return FirstBone(HumanBodyBones.LeftHand, "HandBoneL", "Base HumanLPalm", "Base HumanLForearm1");
            case "boots":
                return FirstBone(HumanBodyBones.LeftFoot, "Base HumanLFoot", "Base HumanLCalf");
            case "belt":
            case "legs":
            case "trinket":
                return FirstBone(HumanBodyBones.Hips, "Base HumanPelvis", "Base HumanSpine1");
            default:
                return FirstBone(HumanBodyBones.Chest, "Base HumanRibcage", "Base HumanSpine2");
        }
    }

    private Transform FirstBone(HumanBodyBones humanoidBone, params string[] fallbackNames)
    {
        Transform bone = GetHumanoidBone(humanoidBone);
        if (bone != null)
            return bone;

        RebuildAvatarBoneMap();
        if (fallbackNames == null)
            return null;

        for (int i = 0; i < fallbackNames.Length; i++)
        {
            string name = fallbackNames[i];
            if (!string.IsNullOrWhiteSpace(name) && _avatarBoneMap.TryGetValue(name, out bone) && bone != null)
                return bone;
        }

        return null;
    }

    private Transform GetHumanoidBone(HumanBodyBones bone)
    {
        if (_animator == null || !_animator.isHuman)
            return null;

        try
        {
            return _animator.GetBoneTransform(bone);
        }
        catch
        {
            return null;
        }
    }

    private void RebuildAvatarBoneMap()
    {
        _avatarBoneMap.Clear();
        if (_avatarRoot == null)
            return;

        Transform[] bones = _avatarRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];
            if (bone == null || string.IsNullOrWhiteSpace(bone.name))
                continue;
            if (!_avatarBoneMap.ContainsKey(bone.name))
                _avatarBoneMap.Add(bone.name, bone);
        }
    }

    private void BindNativeWearableToAvatar(GameObject wearable)
    {
        if (wearable == null || _avatarRoot == null)
            return;

        RebuildAvatarBoneMap();
        SkinnedMeshRenderer[] skinnedRenderers = wearable.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer skinned = skinnedRenderers[i];
            if (skinned == null)
                continue;

            Transform[] sourceBones = skinned.bones;
            if (sourceBones == null || sourceBones.Length == 0)
                continue;

            bool reboundAny = false;
            Transform[] reboundBones = new Transform[sourceBones.Length];
            for (int b = 0; b < sourceBones.Length; b++)
            {
                Transform sourceBone = sourceBones[b];
                Transform reboundBone = null;
                if (sourceBone != null)
                    _avatarBoneMap.TryGetValue(sourceBone.name, out reboundBone);

                reboundBones[b] = reboundBone != null ? reboundBone : sourceBone;
                reboundAny |= reboundBone != null;
            }

            if (!reboundAny)
                continue;

            skinned.bones = reboundBones;
            if (skinned.rootBone != null && _avatarBoneMap.TryGetValue(skinned.rootBone.name, out Transform rootBone) && rootBone != null)
                skinned.rootBone = rootBone;
            else if (_avatarBoneMap.TryGetValue("BoneRoot", out rootBone) && rootBone != null)
                skinned.rootBone = rootBone;

            skinned.updateWhenOffscreen = true;
            skinned.forceRenderingOff = false;
            skinned.enabled = true;
        }
    }

    private void LockVisualRigToPlayer()
    {
        if (_visualRig == null)
            return;

        if (_visualRig.parent != transform)
            _visualRig.SetParent(transform, false);

        _rigBaseLocalPosition = Vector3.zero;
        _visualRig.localPosition = Vector3.zero;
        _visualRig.localRotation = Quaternion.identity;
        _visualRig.localScale = Vector3.one;
    }

    private void UpdateFirstPersonEquipment(bool firstPerson)
    {
        if (!firstPerson)
            return;

        EnsureFirstPersonRoot();
        if (_firstPersonRoot == null)
            return;

        string signature = BuildFirstPersonSignature();
        if (string.Equals(signature, _lastFirstPersonSignature, StringComparison.OrdinalIgnoreCase))
            return;

        _lastFirstPersonSignature = signature;
        for (int i = _firstPersonRoot.childCount - 1; i >= 0; i--)
            Destroy(_firstPersonRoot.GetChild(i).gameObject);
        _firstPersonWeaponAnchor = null;
        _firstPersonOffhandAnchor = null;

        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (state == null)
            return;

        InventoryItemRecord weapon = state.GetEquippedItem("weapon");
        // note: First-person equipment has its own conservative presentation scale so imported world-sized weapons never obscure the aiming lane.
        AddFirstPersonItem(weapon, "weapon", new Vector3(0.52f, -0.48f, 0.94f), new Vector3(4f, -12f, 76f), FirstPersonWeaponMaximumSize);

        InventoryItemRecord offhand = state.GetEquippedItem("offhand");
        if (offhand != null && weapon != null && string.Equals(offhand.itemId, weapon.itemId, StringComparison.OrdinalIgnoreCase))
            return;
        AddFirstPersonItem(offhand, "offhand", new Vector3(-0.44f, -0.47f, 0.96f), new Vector3(8f, 18f, -68f), FirstPersonOffhandMaximumSize);
    }

    private void AddFirstPersonItem(InventoryItemRecord item, string slotName, Vector3 localPosition, Vector3 euler, float maxSize)
    {
        if (item == null || _firstPersonRoot == null)
            return;

        SlotVisual slot = new SlotVisual(slotName, localPosition, euler, maxSize);
        GameObject anchorGo = new GameObject("FP_" + slotName + "_Anchor");
        anchorGo.transform.SetParent(_firstPersonRoot, false);
        anchorGo.transform.localPosition = localPosition;
        anchorGo.transform.localRotation = Quaternion.Euler(euler);
        anchorGo.transform.localScale = Vector3.one;

        GameObject itemVisual = CreateItemVisual(item, slot);
        itemVisual.name = "FP_" + slotName + "_" + (string.IsNullOrWhiteSpace(item.displayName) ? "Item" : item.displayName);
        itemVisual.transform.SetParent(anchorGo.transform, false);
        itemVisual.transform.localPosition = Vector3.zero;
        itemVisual.transform.localRotation = Quaternion.identity;
        itemVisual.transform.localScale = Vector3.one;
        PrepareVisualInstance(itemVisual);
        YQRuntimeUrpMaterialRepair.RepairHierarchy(itemVisual);
        NormalizeEquippedInstance(itemVisual, itemVisual.transform, maxSize);
        ApplyItemTint(itemVisual, item);

        if (LooksMagical(item))
            AddMagicLoop(itemVisual.transform, ResolveItemColor(item));

        // note: Imported skinned/LOD equipment can report incomplete bounds on its creation frame; reapply the screen-space size contract after Unity has initialized those renderers.
        StartCoroutine(StabilizeFirstPersonItemScaleRoutine(
            itemVisual,
            maxSize));

        if (string.Equals(slotName, "weapon", StringComparison.OrdinalIgnoreCase))
        {
            _firstPersonWeaponAnchor = anchorGo.transform;
            _firstPersonWeaponBasePosition = localPosition;
            _firstPersonWeaponBaseRotation = anchorGo.transform.localRotation;
        }
        else if (string.Equals(slotName, "offhand", StringComparison.OrdinalIgnoreCase))
        {
            _firstPersonOffhandAnchor = anchorGo.transform;
            _firstPersonOffhandBasePosition = localPosition;
            _firstPersonOffhandBaseRotation = anchorGo.transform.localRotation;
        }
    }

    private static IEnumerator StabilizeFirstPersonItemScaleRoutine(
        GameObject itemVisual,
        float maximumSize)
    {
        yield return null;

        if (itemVisual != null && itemVisual.activeInHierarchy)
            NormalizeEquippedInstance(itemVisual, itemVisual.transform, maximumSize);

        // note: A second late sample catches animated bounds that become valid only after the first skinning update without adding a permanent per-frame scan.
        yield return new WaitForEndOfFrame();

        if (itemVisual != null && itemVisual.activeInHierarchy)
            NormalizeEquippedInstance(itemVisual, itemVisual.transform, maximumSize);
    }

    private GameObject CreateItemVisual(InventoryItemRecord item, SlotVisual slot)
    {
        string prefabPath = item != null ? item.prefabKey : string.Empty;
        GameObject prefab = CanUseImportedEquipment() &&
                            !string.IsNullOrWhiteSpace(prefabPath) &&
                            !IsPlayerOrFullCharacterPrefabPath(prefabPath)
            ? LoadPrefab(prefabPath)
            : null;
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab);
            if (!LooksLikeFullHumanoidCharacterVisual(instance))
            {
                RepairMissingRendererMaterials(instance, ResolveItemColor(item));
                return instance;
            }

            DisableRenderers(instance.transform);
            DestroyUnityObject(instance);
        }

        return CreateFallbackItem(slot, item);
    }

    private void SweepGhostPlayerVisuals()
    {
        if (Application.isPlaying && Time.unscaledTime < _nextGhostVisualSweepTime)
            return;

        // note: Ghost cleanup is a recovery audit, not a presentation update; keep it off the sub-second gameplay hot path.
        _nextGhostVisualSweepTime = Time.unscaledTime + 3f;
        CleanupStrayPlayerVisuals();

        Camera camera = _motor != null && _motor.playerCamera != null ? _motor.playerCamera : Camera.main;
        CleanupCameraPlayerVisuals(camera);

        bool removed = false;
        removed |= CleanupFullCharacterEquipmentVisuals(_firstPersonRoot);
        removed |= CleanupFullCharacterEquipmentVisuals(_equipmentRoot);
        if (!removed)
            return;

        _lastSignature = string.Empty;
        _lastFirstPersonSignature = string.Empty;
        _firstPersonWeaponAnchor = null;
        _firstPersonOffhandAnchor = null;
    }

    private static bool CleanupFullCharacterEquipmentVisuals(Transform root)
    {
        if (root == null)
            return false;

        bool removed = false;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == null || !LooksLikeFullHumanoidCharacterVisual(child.gameObject))
                continue;

            DisableRenderers(child);
            DestroyUnityObject(child.gameObject);
            removed = true;
        }

        return removed;
    }

    private static bool IsPlayerOrFullCharacterPrefabPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string normalized = path.Replace('\\', '/');
        if (string.Equals(normalized, HumanMalePrefabPath, StringComparison.OrdinalIgnoreCase))
            return true;

        return normalized.IndexOf("/Human - Humans/_Prefabs/Characters/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool LooksLikeFullHumanoidCharacterVisual(GameObject root)
    {
        if (root == null)
            return false;
        if (root.GetComponentInChildren<YQInvestorPlayerMotor>(true) != null)
            return true;

        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return false;

        return CountFullHumanVisualMarkers(root.transform) >= 2;
    }

    private static int CountFullHumanVisualMarkers(Transform root)
    {
        if (root == null)
            return 0;

        int markers = 0;
        Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < nodes.Length; i++)
        {
            Transform node = nodes[i];
            if (node == null)
                continue;

            string name = node.name ?? string.Empty;
            if (name.IndexOf("Base HumanPelvis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Base HumanRibcage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Base HumanHead", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.StartsWith("HumanMaleHair", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Magic Spawn Pos", StringComparison.OrdinalIgnoreCase))
            {
                markers++;
            }
        }

        return markers;
    }

    private static void RepairMissingRendererMaterials(GameObject root, Color fallbackColor)
    {
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : null;
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            Material material = renderer.sharedMaterial;
            if (material != null &&
                material.shader != null &&
                material.shader.name.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            YQInvestorRuntimeVisuals.SetRendererColor(renderer, fallbackColor);
        }
    }

    private string BuildEquipmentSignature()
    {
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (state == null)
            return string.Empty;

        state.EnsureCollections();
        System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        for (int i = 0; i < Slots.Length; i++)
        {
            InventoryItemRecord item = state.GetEquippedItem(Slots[i].Slot);
            if (item == null)
                continue;

            sb.Append(Slots[i].Slot).Append('=')
              .Append(item.itemId).Append(':')
              .Append(item.prefabKey).Append(':')
              .Append(item.effectKey).Append('|');
        }

        return sb.ToString();
    }

    private void AnimateRig(bool firstPerson)
    {
        bool crouching = _motor != null && _motor.IsCrouching;
        bool dashing = _motor != null && _motor.IsDashing;
        bool sprinting = _motor != null && _motor.IsSprinting;
        float speed = ResolveVisualMoveSpeed(dashing, sprinting, out bool moving);
        if (dashing && !_lastDashVisualState)
            PlayRollFeedback();
        _lastDashVisualState = dashing;
        _crouchBlend = Mathf.MoveTowards(_crouchBlend, crouching ? 1f : 0f, Time.deltaTime * 7.5f);
        UpdateAnimator(speed, moving, sprinting, crouching, dashing);

        if (firstPerson)
        {
            AnimateFirstPersonRoot(speed);
            return;
        }

        if (_visualRig == null)
            return;

        float bob = Mathf.Sin(Time.time * Mathf.Lerp(2.2f, 10.5f, speed)) * 0.025f * speed;
        float sway = Mathf.Sin(Time.time * Mathf.Lerp(1.6f, 7.5f, speed)) * 1.8f * speed;
        float lean = Mathf.Lerp(0f, 5.5f, speed);
        float meleeKick = Mathf.Clamp01((_meleeKickUntil - Time.time) / 0.22f);
        float castKick = Mathf.Clamp01((_castKickUntil - Time.time) / 0.28f);
        float rollKick = Mathf.Clamp01((_rollKickUntil - Time.time) / 0.20f);
        float jumpKick = Mathf.Clamp01((_jumpKickUntil - Time.time) / 0.28f);
        float meleeCurve = Mathf.Sin((1f - meleeKick) * Mathf.PI);
        float castCurve = Mathf.Sin((1f - castKick) * Mathf.PI);
        float rollCurve = rollKick > 0f ? Mathf.Sin((1f - rollKick) * Mathf.PI) : 0f;
        float jumpCurve = jumpKick > 0f ? Mathf.Sin((1f - jumpKick) * Mathf.PI) : 0f;
        _visualRig.localPosition = _rigBaseLocalPosition + new Vector3(
            0.035f * _meleeSide * meleeCurve,
            bob - 0.01f * rollCurve + 0.08f * jumpCurve - 0.03f * _crouchBlend,
            0.055f * castCurve - 0.06f * meleeCurve + 0.18f * rollCurve);
        _visualRig.localRotation = Quaternion.Euler(
            lean - 7f * meleeCurve - 3f * castCurve - 14f * rollCurve - 10f * jumpCurve - 15f * _crouchBlend,
            9f * _meleeSide * meleeCurve,
            -sway - 11f * _meleeSide * meleeCurve + 5f * castCurve);
        _visualRig.localScale = new Vector3(
            Mathf.Lerp(1f, 1.04f, _crouchBlend),
            Mathf.Lerp(1f, 0.72f, _crouchBlend),
            Mathf.Lerp(1f, 1.04f, _crouchBlend));
    }

    private void AnimateFirstPersonRoot(float speed)
    {
        if (_firstPersonRoot == null)
            return;

        float walkBob = Mathf.Sin(Time.time * Mathf.Lerp(2f, 9.5f, speed)) * 0.018f * speed;
        float walkSway = Mathf.Cos(Time.time * Mathf.Lerp(2f, 8.5f, speed)) * 0.022f * speed;
        float meleeRemaining = Mathf.Clamp01((_meleeKickUntil - Time.time) / 0.22f);
        float castRemaining = Mathf.Clamp01((_castKickUntil - Time.time) / 0.28f);
        float meleeT = 1f - meleeRemaining;
        float castT = 1f - castRemaining;
        float meleeArc = meleeRemaining > 0f ? Mathf.Sin(meleeT * Mathf.PI) : 0f;
        float meleeRecover = meleeRemaining > 0f ? Mathf.Sin(meleeT * Mathf.PI * 0.5f) : 0f;
        float castArc = castRemaining > 0f ? Mathf.Sin(castT * Mathf.PI) : 0f;
        float rollRemaining = Mathf.Clamp01((_rollKickUntil - Time.time) / 0.20f);
        float jumpRemaining = Mathf.Clamp01((_jumpKickUntil - Time.time) / 0.28f);
        float rollArc = rollRemaining > 0f ? Mathf.Sin((1f - rollRemaining) * Mathf.PI) : 0f;
        float jumpArc = jumpRemaining > 0f ? Mathf.Sin((1f - jumpRemaining) * Mathf.PI) : 0f;
        Vector3 recoil = new Vector3(0.02f * castArc, -0.012f * meleeRecover - 0.10f * _crouchBlend - 0.012f * rollArc + 0.035f * jumpArc, -0.08f * Mathf.Max(meleeRecover, castArc) + 0.075f * rollArc);

        _firstPersonRoot.localPosition = new Vector3(walkSway, walkBob, 0f) + recoil;
        _firstPersonRoot.localRotation = Quaternion.Euler(
            -2f * meleeRecover - 2f * castArc - 2.5f * rollArc - 5f * jumpArc - 2f * _crouchBlend,
            2.5f * Mathf.Sin(Time.time * 5.7f) * speed,
            -1.5f * meleeRecover + 4f * castArc);

        AnimateFirstPersonAnchor(
            _firstPersonWeaponAnchor,
            _firstPersonWeaponBasePosition,
            _firstPersonWeaponBaseRotation,
            true,
            speed,
            walkSway,
            walkBob,
            meleeArc,
            meleeRecover,
            castArc,
            rollArc);
        AnimateFirstPersonAnchor(
            _firstPersonOffhandAnchor,
            _firstPersonOffhandBasePosition,
            _firstPersonOffhandBaseRotation,
            false,
            speed,
            walkSway,
            walkBob,
            meleeArc,
            meleeRecover,
            castArc,
            rollArc);
    }

    private void AnimateFirstPersonAnchor(
        Transform anchor,
        Vector3 basePosition,
        Quaternion baseRotation,
        bool weapon,
        float speed,
        float walkSway,
        float walkBob,
        float meleeArc,
        float meleeRecover,
        float castArc,
        float rollArc)
    {
        if (anchor == null)
            return;

        float side = _meleeSide >= 0f ? 1f : -1f;
        Vector3 walk = new Vector3(walkSway * (weapon ? 0.55f : -0.38f), walkBob * 0.45f, 0f);
        Vector3 attack = weapon
            ? new Vector3(0.18f * side * meleeArc, -0.065f * meleeArc, -0.26f * meleeRecover)
            : new Vector3(-0.05f * side * meleeArc, -0.025f * meleeArc, -0.08f * meleeRecover);
        Vector3 cast = weapon
            ? new Vector3(0.035f, 0.035f, 0.08f) * castArc
            : new Vector3(-0.025f, 0.03f, 0.05f) * castArc;
        Vector3 roll = weapon
            ? new Vector3(0.025f * side, -0.018f, 0.20f) * rollArc
            : new Vector3(-0.012f * side, -0.012f, 0.10f) * rollArc;

        anchor.localPosition = basePosition + walk + attack + cast + roll;
        Quaternion attackRotation = weapon
            ? Quaternion.Euler(-24f * meleeArc - 12f * meleeRecover, 52f * side * meleeArc, -84f * side * meleeArc + 8f * castArc)
            : Quaternion.Euler(-7f * meleeArc, -18f * side * meleeArc, 24f * side * meleeArc + 6f * castArc);
        Quaternion castRotation = Quaternion.Euler(-7f * castArc, 10f * castArc, weapon ? 8f * castArc : -8f * castArc);
        Quaternion rollRotation = weapon
            ? Quaternion.Euler(-8f * rollArc, 5f * side * rollArc, -10f * side * rollArc)
            : Quaternion.Euler(-3f * rollArc, -3f * side * rollArc, 5f * side * rollArc);
        Quaternion idleRotation = Quaternion.Euler(Mathf.Sin(Time.time * 4.2f) * 1.2f * speed, 0f, Mathf.Cos(Time.time * 3.8f) * 1.4f * speed);
        anchor.localRotation = baseRotation * idleRotation * attackRotation * castRotation * rollRotation;
    }

    private float ResolveVisualMoveSpeed(bool dashing, bool sprinting, out bool moving)
    {
        moving = false;
        if (dashing)
        {
            moving = true;
            return 1f;
        }

        if (_motor != null)
        {
            float inputAmount = Mathf.Clamp01(_motor.MoveInput.magnitude);
            moving = inputAmount > 0.05f;
            if (!moving)
                return 0f;

            return sprinting
                ? Mathf.Lerp(0.78f, 1f, inputAmount)
                : Mathf.Lerp(0.44f, 0.68f, inputAmount);
        }

        Vector3 velocity = _controller != null ? _controller.velocity : Vector3.zero;
        velocity.y = 0f;
        float speed = Mathf.Clamp01(velocity.magnitude / 7.5f);
        moving = speed > 0.05f;
        return moving ? speed : 0f;
    }

    private void UpdateAnimator(float speed01, bool moving, bool sprinting, bool crouching, bool dashing)
    {
        if (_animator == null || !_animator.isActiveAndEnabled)
            return;

        float locomotion = 1f;
        if (moving || dashing)
        {
            bool movingBackward = _motor != null && _motor.MoveInput.y < -0.12f;
            locomotion = movingBackward && !dashing ? 0f : (dashing || sprinting ? 3f : 2f);
        }
        SetAnimatorFloat("locomotion", locomotion);
        SetAnimatorFloat("Locomotion", locomotion);
        SetAnimatorFloat("Speed", Mathf.Clamp01(speed01));
        SetAnimatorFloat("speed", Mathf.Clamp01(speed01));
        SetAnimatorFloat("MoveSpeed", Mathf.Clamp01(speed01));
        SetAnimatorFloat("moveSpeed", Mathf.Clamp01(speed01));
        SetAnimatorFloat("Forward", moving ? 1f : 0f);
        SetAnimatorFloat("forward", moving ? 1f : 0f);
        SetAnimatorBool("Moving", moving);
        SetAnimatorBool("moving", moving);
        SetAnimatorBool("IsMoving", moving);
        SetAnimatorBool("isMoving", moving);
        SetAnimatorBool("Crouch", crouching);
        SetAnimatorBool("crouch", crouching);
        SetAnimatorBool("Crouching", crouching);
        SetAnimatorBool("isCrouching", crouching);
        SetAnimatorBool("Dashing", dashing);
        SetAnimatorBool("isDashing", dashing);
        bool grounded = _controller == null || _controller.isGrounded;
        SetAnimatorBool("Grounded", grounded);
        SetAnimatorBool("grounded", grounded);
        SetAnimatorBool("IsGrounded", grounded);
        SetAnimatorBool("isGrounded", grounded);
    }

    private void CleanupLegacyPrimitiveVisual()
    {
        bool foundLegacy = false;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child == _visualRig)
                continue;
            if (!IsLegacyPrimitivePlayerChild(child) &&
                !string.Equals(child.name, "Visual", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(child.name, "Player_Visual", StringComparison.OrdinalIgnoreCase))
                continue;

            foundLegacy = true;
            Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] != null)
                    renderers[r].enabled = false;
            }

            DestroyUnityObject(child.gameObject);
        }

        if (!foundLegacy)
            _legacyVisualCleaned = true;
    }

    private void CleanupStrayPlayerVisualsOnce()
    {
        if (_strayVisualCleanupPerformed)
            return;

        _strayVisualCleanupPerformed = true;
        CleanupStrayPlayerVisuals();
    }

    private void CleanupStrayPlayerVisuals()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child == _visualRig || child == _firstPersonRoot)
                continue;
            if (string.Equals(child.name, "CameraPivot", StringComparison.OrdinalIgnoreCase) ||
                child.GetComponent<Camera>() != null ||
                child.GetComponent<AudioListener>() != null)
                continue;
            if (!IsRemovableStrayPlayerVisual(child))
                continue;

            DisableRenderers(child);
            DestroyUnityObject(child.gameObject);
        }

        CleanupDuplicateRigAvatars();
    }

    private void CleanupDuplicateRigAvatars()
    {
        if (_visualRig == null)
            return;

        Transform keep = _avatarRoot;
        if (keep == null)
            keep = _visualRig.Find("YQ_HumanMaleAvatar");

        for (int i = _visualRig.childCount - 1; i >= 0; i--)
        {
            Transform child = _visualRig.GetChild(i);
            if (child == null || child == keep || child == _equipmentRoot)
                continue;
            if (!IsDuplicateAvatar(child) && !LooksLikePlayerVisualLeak(child))
                continue;

            DisableRenderers(child);
            DestroyUnityObject(child.gameObject);
        }
    }

    private static void CleanupGeneratedBodyFallbacks(Transform root)
    {
        if (root == null)
            return;

        Transform proxy = root.Find("YQ_VisibleBodyProxy");
        if (proxy != null)
        {
            DisableRenderers(proxy);
            DestroyUnityObject(proxy.gameObject);
        }

        Transform directBody = root.Find("YQ_ThirdPersonVisiblePlayerModel");
        if (directBody != null)
        {
            DisableRenderers(directBody);
            DestroyUnityObject(directBody.gameObject);
        }
    }

    private static void DisableImportedAvatarDemoMagic(Transform root)
    {
        if (root == null)
            return;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == root)
                continue;

            if (LooksLikeImportedAvatarDemoMagic(child.name))
            {
                ParticleSystem[] particles = child.GetComponentsInChildren<ParticleSystem>(true);
                for (int p = 0; p < particles.Length; p++)
                {
                    if (particles[p] == null)
                        continue;
                    particles[p].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particles[p].Clear(true);
                }

                child.gameObject.SetActive(false);
            }
        }
    }

    private static bool LooksLikeImportedAvatarDemoMagic(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        return objectName.StartsWith("Magic Spawn", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("PComponent ", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("Particle ", StringComparison.OrdinalIgnoreCase) ||
               objectName.IndexOf("Magic Vampiric", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("Vampiric Curse", StringComparison.OrdinalIgnoreCase) >= 0 ||
               string.Equals(objectName, "Point light", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsRemovableStrayPlayerVisual(Transform child)
    {
        if (child == null || child == _avatarRoot)
            return false;

        return IsDuplicateAvatar(child) || LooksLikePlayerVisualLeak(child);
    }

    private bool IsDuplicateAvatar(Transform child)
    {
        if (child == null || _avatarRoot == null || child == _avatarRoot)
            return false;

        string name = child.name ?? string.Empty;
        return name.StartsWith("YQ_HumanMaleAvatar", StringComparison.OrdinalIgnoreCase);
    }

    private static void DisableRenderers(Transform root)
    {
        SetRenderersEnabled(root, false);
    }

    private static void SetRenderersEnabled(Transform root, bool enabled)
    {
        if (root == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    private static bool LooksLikePlayerVisualLeak(Transform child)
    {
        if (child == null)
            return false;

        string name = child.name ?? string.Empty;
        if (name.IndexOf("YQ_PlayerVisualRig", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("YQ_EquippedItemModels", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("YQ_FirstPersonEquipmentRoot", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("YQ_ThirdPersonVisiblePlayerModel", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("YQ_VisibleBodyProxy", StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(name, "Visual", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Player_Visual", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "PlayerBody", StringComparison.OrdinalIgnoreCase))
            return true;

        bool namedDuplicate = name.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0;
        bool namedPlayerVisual =
            name.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("avatar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("visual", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0;
        if (namedDuplicate && namedPlayerVisual)
            return true;

        return IsLegacyPrimitivePlayerChildStatic(child);
    }

    private static void NormalizeAvatar(GameObject avatar)
    {
        if (avatar == null)
            return;

        Transform anchor = avatar.transform.parent;
        avatar.transform.localRotation = Quaternion.identity;
        Bounds bounds;
        if (!TryGetBounds(avatar, out bounds))
            return;

        float height = Mathf.Max(0.1f, bounds.size.y);
        float scale = Mathf.Clamp(ThirdPersonAvatarHeight / height, 0.12f, 3.2f);
        avatar.transform.localScale *= scale;

        if (!TryGetBounds(avatar, out bounds))
            return;

        if (anchor == null)
            return;

        Vector3 bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        Vector3 bottomLocal = anchor.InverseTransformPoint(bottomCenter);
        Vector3 targetLocal = new Vector3(0f, AvatarGroundClearance, 0f);
        avatar.transform.localPosition += targetLocal - bottomLocal;
        avatar.transform.localRotation = Quaternion.identity;
    }

    private void EnsureAvatarHasSaneLocalAnchor()
    {
        if (_avatarRoot == null)
            return;

        Vector3 local = _avatarRoot.localPosition;
        if (IsFinite(local) &&
            Mathf.Abs(local.x) <= 0.65f &&
            local.y >= -0.20f &&
            local.y <= 0.45f &&
            Mathf.Abs(local.z) <= 0.65f)
            return;

        _avatarRoot.localPosition = Vector3.zero;
        _avatarRoot.localRotation = Quaternion.identity;
        NormalizeAvatar(_avatarRoot.gameObject);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void NormalizeEquippedInstance(GameObject instance, Transform anchor, float maxSize)
    {
        Bounds bounds;
        if (!TryGetBounds(instance, out bounds))
            return;

        float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (largest > 0.01f)
        {
            float scale = Mathf.Clamp(maxSize / largest, 0.04f, 2.25f);
            instance.transform.localScale *= scale;
        }

        if (TryGetBounds(instance, out bounds))
            instance.transform.position += anchor.position - bounds.center;
    }

    private static void NormalizeNativeWearableInstance(GameObject instance)
    {
        if (instance == null)
            return;

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
    }

    private static bool ShouldUseNativeWearableFit(InventoryItemRecord item, SlotVisual slot)
    {
        if (item == null || string.IsNullOrWhiteSpace(slot.Slot))
            return false;
        if (!ContainsSlot(NativeWearableSlots, slot.Slot))
            return false;

        string path = item.prefabKey ?? string.Empty;
        return path.IndexOf("/Human - Humans/_Prefabs/Male Human Armor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("\\Human - Humans\\_Prefabs\\Male Human Armor\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("/Human Wardrobe Male/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("\\Human Wardrobe Male\\", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsSlot(string[] slots, string slot)
    {
        if (slots == null || string.IsNullOrWhiteSpace(slot))
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (string.Equals(slots[i], slot, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void EnsureAnimationEventReceivers(
    GameObject root)
    {
        if (root == null)
            return;

        Animator[] animators =
            root.GetComponentsInChildren<
                Animator>(
                    true);

        if (animators == null ||
            animators.Length == 0)
        {
            return;
        }

        Animator primary =
            null;

        /*
         * Prefer the actual root Animator.
         */
        for (int i = 0;
             i < animators.Length;
             i++)
        {
            Animator animator =
                animators[i];

            if (animator == null)
                continue;

            if (animator.transform ==
                root.transform)
            {
                primary =
                    animator;

                break;
            }
        }

        /*
         * Otherwise prefer a real humanoid Animator with an Avatar.
         */
        if (primary == null)
        {
            for (int i = 0;
                 i < animators.Length;
                 i++)
            {
                Animator animator =
                    animators[i];

                if (animator == null)
                    continue;

                if (animator.avatar != null)
                {
                    primary =
                        animator;

                    break;
                }
            }
        }

        if (primary == null)
        {
            primary =
                animators[0];
        }

        /*
         * Remove YQ receivers previously installed on wardrobe/hair/etc.
         *
         * Their Animators may remain active for visual/skinning purposes.
         * Only their duplicate audio receivers are removed.
         */
        YQAnimationEventAudioReceiver[] receivers =
            root.GetComponentsInChildren<
                YQAnimationEventAudioReceiver>(
                    true);

        for (int i = 0;
             i < receivers.Length;
             i++)
        {
            YQAnimationEventAudioReceiver receiver =
                receivers[i];

            if (receiver == null ||
                receiver.gameObject ==
                primary.gameObject)
            {
                continue;
            }

            receiver.enabled =
                false;

            UnityEngine.Object.Destroy(
                receiver);
        }

        YQAnimationEventAudioReceiver primaryReceiver =
            primary.GetComponent<
                YQAnimationEventAudioReceiver>();

        if (primaryReceiver == null)
        {
            primaryReceiver =
                primary.gameObject
                    .AddComponent<
                        YQAnimationEventAudioReceiver>();
        }

        primaryReceiver.enabled =
            true;
    }

    private static void StripImportedAvatarAudio(
        GameObject root)
    {
        if (root == null)
            return;

        /*
         * Imported human prefabs include demo audio banks such as
         * Wet Footsteps and a package SFB_AudioManager that plays them
         * through AudioSource.PlayClipAtPoint().
         *
         * The player avatar is only a visual shell. YourQuest owns
         * locomotion audio through YQAnimationEventAudioReceiver, which
         * validates actual player movement before playing anything.
         */
        AudioSource[] audioSources =
            root.GetComponentsInChildren<AudioSource>(
                true);

        for (int i = 0;
             i < audioSources.Length;
             i++)
        {
            AudioSource source =
                audioSources[i];

            if (source == null)
                continue;

            source.Stop();
            source.playOnAwake =
                false;
            source.loop =
                false;
            source.enabled =
                false;

            DestroyUnityObject(
                source);
        }

        MonoBehaviour[] behaviours =
            root.GetComponentsInChildren<MonoBehaviour>(
                true);

        for (int i = 0;
             i < behaviours.Length;
             i++)
        {
            MonoBehaviour behaviour =
                behaviours[i];

            if (behaviour == null)
                continue;

            if (!IsImportedAvatarAudioBehaviour(
                    behaviour))
            {
                continue;
            }

            behaviour.enabled =
                false;

            DestroyUnityObject(
                behaviour);
        }
    }

    private static bool IsImportedAvatarAudioBehaviour(
        MonoBehaviour behaviour)
    {
        if (behaviour == null)
            return false;

        Type type =
            behaviour.GetType();

        string typeName =
            type != null
                ? type.Name
                : string.Empty;

        return
            string.Equals(
                typeName,
                "SFB_AudioManager",
                StringComparison.Ordinal) ||
            string.Equals(
                typeName,
                "LPDemoHumanoid",
                StringComparison.Ordinal) ||
            string.Equals(
                typeName,
                "PlayAudioClipOnAwake",
                StringComparison.Ordinal);
    }

    private static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(root.transform.position, Vector3.zero);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
                bounds.Encapsulate(renderer.bounds);
        }

        return found;
    }

    private static void PrepareVisualInstance(
    GameObject root,
    bool preserveBehaviours = false)
    {
        if (root == null)
            return;

        Collider[] colliders =
            root.GetComponentsInChildren<Collider>(true);

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            Collider collider =
                colliders[i];

            if (collider != null)
                collider.enabled = false;
        }

        Rigidbody[] bodies =
            root.GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0;
             i < bodies.Length;
             i++)
        {
            Rigidbody body =
                bodies[i];

            if (body == null)
                continue;

            body.isKinematic = true;
        }

        /*
         * Imported equipment is VISUAL ONLY.
         *
         * It must not retain its own animation-event audio receiver or AudioSource.
         * Otherwise boots, trousers, armor, weapons, hair, etc. can independently
         * react to imported AnimationEvents and create PlayOneShot storms.
         *
         * The actual player avatar uses preserveBehaviours=true and receives its
         * single authoritative YQAnimationEventAudioReceiver later in
         * ConfigureAvatar().
         */
        if (!preserveBehaviours)
        {
            YQAnimationEventAudioReceiver[] audioReceivers =
                root.GetComponentsInChildren<
                    YQAnimationEventAudioReceiver>(
                        true);

            for (int i = 0;
                 i < audioReceivers.Length;
                 i++)
            {
                YQAnimationEventAudioReceiver receiver =
                    audioReceivers[i];

                if (receiver == null)
                    continue;

                receiver.enabled = false;

                DestroyUnityObject(
                    receiver);
            }

            /*
             * Strip AudioSources inherited from cosmetic imported equipment.
             *
             * Equipment instantiated by this class does not own gameplay audio;
             * weapon/combat audio should be driven by the gameplay system, not
             * by arbitrary imported prefab AnimationEvents.
             */
            AudioSource[] audioSources =
                root.GetComponentsInChildren<AudioSource>(
                    true);

            for (int i = 0;
                 i < audioSources.Length;
                 i++)
            {
                AudioSource source =
                    audioSources[i];

                if (source == null)
                    continue;

                source.Stop();

                source.enabled = false;

                DestroyUnityObject(
                    source);
            }

            MonoBehaviour[] behaviours =
                root.GetComponentsInChildren<
                    MonoBehaviour>(
                        true);

            for (int i = 0;
                 i < behaviours.Length;
                 i++)
            {
                MonoBehaviour behaviour =
                    behaviours[i];

                if (behaviour == null)
                    continue;

                if (behaviour.GetType() ==
                    typeof(YQPlayerEquipmentVisual))
                {
                    continue;
                }

                behaviour.enabled = false;
            }
        }

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true);

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer renderer =
                renderers[i];

            if (renderer == null)
                continue;

            renderer.shadowCastingMode =
                ShadowCastingMode.On;

            renderer.receiveShadows =
                true;
        }
    }

    private bool IsLegacyPrimitivePlayerChild(Transform child)
    {
        if (child == null || child == _visualRig || child == _firstPersonRoot)
            return false;
        if (string.Equals(child.name, "CameraPivot", StringComparison.OrdinalIgnoreCase))
            return false;
        if (child.GetComponentInChildren<Animator>(true) != null)
            return false;

        string name = child.name ?? string.Empty;
        bool legacyName = name.IndexOf("capsule", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          string.Equals(name, "Body", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(name, "PlayerBody", StringComparison.OrdinalIgnoreCase) ||
                          name.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0;
        return legacyName && child.GetComponentInChildren<Renderer>(true) != null && child.GetComponentInChildren<MeshFilter>(true) != null;
    }

    private static bool IsLegacyPrimitivePlayerChildStatic(Transform child)
    {
        if (child == null)
            return false;

        string name = child.name ?? string.Empty;
        bool legacyName = name.IndexOf("capsule", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          string.Equals(name, "Body", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(name, "PlayerBody", StringComparison.OrdinalIgnoreCase) ||
                          name.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0;
        return legacyName && child.GetComponentInChildren<Renderer>(true) != null && child.GetComponentInChildren<MeshFilter>(true) != null;
    }

    private static void ApplyItemTint(GameObject root, InventoryItemRecord item)
    {
        Color color = ResolveItemColor(item);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;
            if (!ShouldTintRenderer(root, renderer))
                continue;

            renderer.SetPropertyBlock(block);
        }
    }

    private static bool ShouldTintRenderer(GameObject root, Renderer renderer)
    {
        if (root != null && root.name.StartsWith("Fallback_", StringComparison.OrdinalIgnoreCase))
            return true;
        if (renderer == null || renderer is ParticleSystemRenderer)
            return true;

        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
            return true;
        for (int i = 0; i < materials.Length; i++)
        {
            if (HasUsableBaseTexture(materials[i]))
                return false;
        }

        return true;
    }

    private static bool HasUsableBaseTexture(Material material)
    {
        if (material == null)
            return false;

        for (int i = 0; i < BaseTextureProperties.Length; i++)
        {
            string propertyName = BaseTextureProperties[i];
            if (!material.HasProperty(propertyName))
                continue;
            if (material.GetTexture(propertyName) != null)
                return true;
        }

        try
        {
            return material.mainTexture != null;
        }
        catch
        {
            return false;
        }
    }

    private static Color ResolveItemColor(InventoryItemRecord item)
    {
        string descriptor = BuildDescriptor(item).ToLowerInvariant();
        if (descriptor.Contains("fire") || descriptor.Contains("flame") || descriptor.Contains("ember"))
            return new Color(1f, 0.48f, 0.18f, 1f);
        if (descriptor.Contains("arc") || descriptor.Contains("storm") || descriptor.Contains("lightning") || descriptor.Contains("electric"))
            return new Color(0.42f, 0.76f, 1f, 1f);
        if (descriptor.Contains("poison") || descriptor.Contains("venom"))
            return new Color(0.56f, 1f, 0.34f, 1f);
        if (descriptor.Contains("heal") || descriptor.Contains("holy") || descriptor.Contains("restore"))
            return new Color(0.70f, 1f, 0.76f, 1f);

        string rarity = item != null ? (item.rarity ?? string.Empty).Trim().ToLowerInvariant() : string.Empty;
        switch (rarity)
        {
            case "epic": return new Color(1f, 0.76f, 0.28f, 1f);
            case "rare": return new Color(0.72f, 0.56f, 1f, 1f);
            case "uncommon": return new Color(0.48f, 0.86f, 0.72f, 1f);
            default: return new Color(0.82f, 0.84f, 0.82f, 1f);
        }
    }

    private static bool LooksMagical(InventoryItemRecord item)
    {
        string descriptor = BuildDescriptor(item).ToLowerInvariant();
        if (descriptor.Contains("magic") || descriptor.Contains("enchanted") || descriptor.Contains("rune") ||
            descriptor.Contains("fire") || descriptor.Contains("flame") || descriptor.Contains("ember") ||
            descriptor.Contains("arc") || descriptor.Contains("storm") || descriptor.Contains("holy") ||
            descriptor.Contains("frost") || descriptor.Contains("poison") || descriptor.Contains("venom"))
            return true;

        string rarity = item != null ? (item.rarity ?? string.Empty).Trim().ToLowerInvariant() : string.Empty;
        return rarity == "rare" || rarity == "epic";
    }

    private static string BuildDescriptor(InventoryItemRecord item)
    {
        if (item == null)
            return string.Empty;

        return (item.displayName ?? string.Empty) + " " +
               (item.description ?? string.Empty) + " " +
               (item.effectKey ?? string.Empty) + " " +
               (item.familyKey ?? string.Empty);
    }

    private static void AddMagicLoop(Transform parent, Color color)
    {
        GameObject root = new GameObject("MagicItemAura");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;
        ParticleSystem ps = root.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1.6f;
        main.startLifetime = 0.7f;
        main.startSpeed = 0.055f;
        main.startSize = 0.026f;
        main.startColor = new Color(color.r, color.g, color.b, 0.20f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 1.5f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.material = YQGeneratedRuntimeVfx.CreateParticleMaterial(color);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 0.055f;
        light.range = 0.72f;
        light.shadows = LightShadows.None;
        ps.Play();
    }

    private static GameObject CreateFallbackItem(SlotVisual slot, InventoryItemRecord item)
    {
        if (!ShouldRenderFallbackItem(slot))
            return new GameObject("MissingVisual_" + slot.Slot);

        PrimitiveType type = slot.Slot == "weapon" ? PrimitiveType.Cube :
            slot.Slot == "offhand" ? PrimitiveType.Cylinder :
            slot.Slot == "head" || slot.Slot.StartsWith("ring", StringComparison.OrdinalIgnoreCase) ? PrimitiveType.Sphere :
            PrimitiveType.Cube;

        GameObject root = GameObject.CreatePrimitive(type);
        root.name = "Fallback_" + slot.Slot;
        Collider collider = root.GetComponent<Collider>();
        if (collider != null)
            DestroyUnityObject(collider);

        switch (slot.Slot)
        {
            case "weapon":
                root.transform.localScale = new Vector3(0.08f, 0.82f, 0.08f);
                break;
            case "offhand":
                root.transform.localScale = new Vector3(0.38f, 0.06f, 0.38f);
                break;
            case "head":
                root.transform.localScale = new Vector3(0.42f, 0.22f, 0.42f);
                break;
            default:
                root.transform.localScale = Vector3.one * Mathf.Max(0.12f, slot.MaxSize * 0.5f);
                break;
        }

        YQInvestorRuntimeVisuals.SetRendererColor(root.GetComponent<Renderer>(), ResolveItemColor(item));
        return root;
    }

    private static bool ShouldRenderFallbackItem(SlotVisual slot)
    {
        return string.Equals(slot.Slot, "weapon", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(slot.Slot, "offhand", StringComparison.OrdinalIgnoreCase);
    }

    private static void CreatePrimitivePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            DestroyUnityObject(collider);
        }
        YQInvestorRuntimeVisuals.SetRendererColor(part.GetComponent<Renderer>(), color);
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private bool IsLocalFirstPerson()
    {
        if (_motor == null)
            _motor = GetComponent<YQInvestorPlayerMotor>();
        return _motor != null && _motor.firstPerson && _motor.playerCamera != null;
    }

    private void ApplyPerspectiveVisibility(bool firstPerson)
    {
        if (_visualRig != null)
            _visualRig.gameObject.SetActive(!firstPerson);

        if (!firstPerson)
        {
            ForceThirdPersonVisibleNow();
            EnsureThirdPersonAvatarVisible();
        }

        if (firstPerson)
            EnsureFirstPersonRoot();
        else if (_firstPersonRoot != null)
            _firstPersonRoot.gameObject.SetActive(false);
    }

    private void ForceThirdPersonVisibleNow()
    {
        EnsureVisualRig();
        LockVisualRigToPlayer();
        EnsureAvatar();
        EnsureCameraCanRenderPlayerVisuals();

        if (_visualRig != null)
            _visualRig.gameObject.SetActive(true);
        if (_avatarRoot != null)
            _avatarRoot.gameObject.SetActive(true);
    }

    private void EnsureCameraCanRenderPlayerVisuals()
    {
        Camera camera = _motor != null && _motor.playerCamera != null ? _motor.playerCamera : Camera.main;
        if (camera == null)
            return;

        int layer = Mathf.Clamp(gameObject.layer, 0, 31);
        camera.cullingMask |= 1 << layer;
    }

    private void EnsureThirdPersonAvatarVisible()
    {
        if (_visualRig == null)
            return;

        if (_avatarRoot == null)
            EnsureAvatar();
        if (_avatarRoot == null)
            return;

        _visualRig.gameObject.SetActive(true);
        _avatarRoot.gameObject.SetActive(true);

        if (Application.isPlaying && Time.unscaledTime < _nextThirdPersonVisibilityRepairTime)
            return;
        // note: A healthy avatar does not need full hierarchy and renderer repair several times per second.
        _nextThirdPersonVisibilityRepairTime = Time.unscaledTime + 2f;

        ForceRenderableState(_avatarRoot, gameObject.layer);
        DisableImportedAvatarDemoMagic(_avatarRoot);
        ActivateLikelyAvatarBodyRenderers(_avatarRoot);
        if (HasVisibleAvatarBodyRenderer())
            return;

        RebuildAvatarWithGuaranteedBody();
    }

    private void RebuildAvatarWithGuaranteedBody()
    {
        if (_visualRig == null)
            return;

        if (_avatarRoot != null)
        {
            _avatarRoot.gameObject.name = "YQ_HumanMaleAvatar_ReplacedInvisible";
            _avatarRoot.gameObject.SetActive(false);
            DestroyUnityObject(_avatarRoot.gameObject);
            _avatarRoot = null;
            _animator = null;
        }

        GameObject prefab = CanUseImportedAvatar() ? LoadPrefab(HumanMalePrefabPath) : null;
        if (prefab == null)
        {
            Debug.LogError("[YourQuest] Missing animated male player avatar prefab: " + HumanMalePrefabPath);
            return;
        }

        GameObject avatar = Instantiate(prefab, _visualRig);

        avatar.name = "YQ_HumanMaleAvatar";
        _avatarRoot = avatar.transform;
        _avatarRoot.localPosition = Vector3.zero;
        _avatarRoot.localRotation = Quaternion.identity;
        _avatarRoot.localScale = Vector3.one;
        ConfigureAvatar(avatar);
        ForceRenderableState(_avatarRoot, gameObject.layer);
        EnsureAvatarHasSaneLocalAnchor();

        if (!HasVisibleAvatarBodyRenderer())
            Debug.LogError("[YourQuest] Animated male player avatar loaded, but no active body renderer was found on " + HumanMalePrefabPath);

        RebuildEquipment();
    }

    private bool HasVisibleAvatarBodyRenderer()
    {
        if (_avatarRoot == null)
            return false;

        Renderer[] renderers = _avatarRoot.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy)
                continue;
            if (_equipmentRoot != null && renderer.transform.IsChildOf(_equipmentRoot))
                continue;
            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                return true;
        }

        return false;
    }

    private static void ForceRenderableState(Transform root, int layer)
    {
        if (root == null)
            return;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null)
                children[i].gameObject.layer = layer;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
            {
                skinned.updateWhenOffscreen = true;
                if (skinned.localBounds.size.sqrMagnitude < 0.01f)
                    skinned.localBounds = new Bounds(Vector3.up * 0.9f, new Vector3(1.2f, 2.1f, 1.2f));
            }
        }
    }

    private static void ActivateLikelyAvatarBodyRenderers(Transform root)
    {
        if (root == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            string name = renderer.gameObject.name ?? string.Empty;
            bool coreBody =
                string.Equals(name, "Body", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Head", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("HumanMaleHair", StringComparison.OrdinalIgnoreCase);
            if (!coreBody)
                continue;

            renderer.gameObject.SetActive(true);
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
                skinned.updateWhenOffscreen = true;
        }
    }

    private void EnsureFirstPersonRoot()
    {
        Camera camera = _motor != null ? _motor.playerCamera : Camera.main;
        if (camera == null)
            return;

        bool rootChanged = false;
        if (_firstPersonRoot == null)
        {
            Transform existing = camera.transform.Find("YQ_FirstPersonEquipmentRoot");
            if (existing != null)
                _firstPersonRoot = existing;
            else
            {
                GameObject root = new GameObject("YQ_FirstPersonEquipmentRoot");
                root.transform.SetParent(camera.transform, false);
                _firstPersonRoot = root.transform;
            }
            rootChanged = true;
        }
        else if (_firstPersonRoot.parent != camera.transform)
        {
            _firstPersonRoot.SetParent(camera.transform, false);
            rootChanged = true;
        }

        _firstPersonRoot.gameObject.SetActive(true);
        _firstPersonRoot.localScale = Vector3.one;
        if (rootChanged)
            CleanupCameraPlayerVisuals(camera);
    }

    private void CleanupCameraPlayerVisuals(Camera camera)
    {
        if (camera == null)
            return;

        Transform cameraTransform = camera.transform;
        for (int i = cameraTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = cameraTransform.GetChild(i);
            if (child == null || child == _firstPersonRoot)
                continue;
            if (!LooksLikePlayerVisualLeak(child))
                continue;

            DisableRenderers(child);
            DestroyUnityObject(child.gameObject);
        }
    }

    private string BuildFirstPersonSignature()
    {
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (state == null)
            return string.Empty;

        InventoryItemRecord weapon = state.GetEquippedItem("weapon");
        InventoryItemRecord offhand = state.GetEquippedItem("offhand");
        return BuildItemSignature("weapon", weapon) + "|" + BuildItemSignature("offhand", offhand);
    }

    private static string BuildItemSignature(string slot, InventoryItemRecord item)
    {
        if (item == null)
            return slot + "=none";
        return slot + "=" + item.itemId + ":" + item.prefabKey + ":" + item.effectKey;
    }

    private bool TriggerFirstAnimator(params string[] parameterNames)
    {
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null || parameterNames == null)
            return false;

        for (int i = 0; i < parameterNames.Length; i++)
        {
            string parameterName = parameterNames[i];
            if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
                continue;

            _animator.ResetTrigger(parameterName);
            _animator.SetTrigger(parameterName);
            return true;
        }

        return false;
    }

    private bool PlayFirstAnimatorState(params string[] stateNames)
    {
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null || stateNames == null)
            return false;

        const int layer = 0;
        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[i];
            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            int shortHash = Animator.StringToHash(stateName);
            if (_animator.HasState(layer, shortHash))
            {
                _animator.Play(shortHash, layer, 0f);
                return true;
            }

            int fullHash = Animator.StringToHash("Base Layer." + stateName);
            if (_animator.HasState(layer, fullHash))
            {
                _animator.Play(fullHash, layer, 0f);
                return true;
            }
        }

        return false;
    }

    private void SetAnimatorFloat(string parameterName, float value)
    {
        if (_animator == null || !HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
            return;
        _animator.SetFloat(parameterName, value, 0.12f, Time.deltaTime);
    }

    private void SetAnimatorFloatImmediate(string parameterName, float value)
    {
        if (_animator == null || !HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
            return;
        _animator.SetFloat(parameterName, value);
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (_animator == null || !HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
            return;
        _animator.SetBool(parameterName, value);
    }

    private void ResetAnimatorToIdle()
    {
        if (_animator == null)
            return;

        SetAnimatorFloatImmediate("locomotion", 1f);
        SetAnimatorFloatImmediate("Locomotion", 1f);
        SetAnimatorFloatImmediate("Speed", 0f);
        SetAnimatorFloatImmediate("speed", 0f);
        SetAnimatorFloatImmediate("MoveSpeed", 0f);
        SetAnimatorFloatImmediate("moveSpeed", 0f);
        SetAnimatorFloatImmediate("Forward", 0f);
        SetAnimatorFloatImmediate("forward", 0f);
        SetAnimatorBool("Moving", false);
        SetAnimatorBool("moving", false);
        SetAnimatorBool("IsMoving", false);
        SetAnimatorBool("isMoving", false);
        if (_animator.runtimeAnimatorController != null)
            _animator.Update(0f);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = _animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == type && string.Equals(parameter.name, parameterName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static GameObject LoadPrefab(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            return null;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }

    private bool CanUseImportedAvatar()
    {
        return !Application.isPlaying || allowImportedAvatarInPlay;
    }

    private bool CanUseImportedEquipment()
    {
        return !Application.isPlaying || allowImportedEquipmentInPlay;
    }

    private static RuntimeAnimatorController LoadAnimatorController(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
#else
        return null;
#endif
    }
}

internal sealed class YQPlayerVisualAuthorityRepair : MonoBehaviour
{
    private void LateUpdate()
    {
        YQPlayerEquipmentVisual.RepairActivePlayerVisualBinding();
    }
}

internal sealed class YQEquipmentBoneFollower : MonoBehaviour
{
    private Transform _target;
    private Vector3 _targetLocalPosition;
    private Quaternion _targetLocalRotation = Quaternion.identity;

    public void Bind(Transform target, Transform current)
    {
        _target = target;
        if (_target == null || current == null)
            return;

        _targetLocalPosition = _target.InverseTransformPoint(current.position);
        _targetLocalRotation = Quaternion.Inverse(_target.rotation) * current.rotation;
        LateUpdate();
    }

    public void Bind(Transform target, Vector3 targetLocalPosition, Quaternion targetLocalRotation)
    {
        _target = target;
        _targetLocalPosition = targetLocalPosition;
        _targetLocalRotation = targetLocalRotation;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (_target == null)
            return;

        transform.position = _target.TransformPoint(_targetLocalPosition);
        transform.rotation = _target.rotation * _targetLocalRotation;
    }
}
