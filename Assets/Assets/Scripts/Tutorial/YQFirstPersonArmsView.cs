using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// note: This view mirrors the authoritative Animator after evaluation. It has no Animator, input, colliders, combat, or save ownership of its own.
[DefaultExecutionOrder(220)]
[DisallowMultipleComponent]
public sealed class YQFirstPersonArmsView : MonoBehaviour
{
    private readonly Dictionary<Transform, Transform> _bones = new Dictionary<Transform, Transform>();
    private Transform[] _sourceBones;
    private Transform[] _viewBones;
    private Transform _skeleton;
    private Animator _source;
    private Camera _camera;
    private Vector3 _restOffset;
    private Vector3 _poseOffset;
    private Transform _rightSourceHand;
    private Transform _leftSourceHand;
    private Vector3 _rightRestHand;
    private Vector3 _leftRestHand;
    private Transform _rightUpperArm;
    private Transform _leftUpperArm;
    private Transform _rightForearm;
    private Transform _leftForearm;
    private float _castFraming;
    private float _deathFraming;
    private float _attackFraming;
    private float _gestureFraming;
    private float _rollTuck;
    private float _dashBrace;
    private float _landingSettle;
    private Vector3 _previousCameraEuler;
    private Vector3 _lookSway;
    private float _equipAt;
    public Transform RightHand { get; private set; }
    public Transform LeftHand { get; private set; }
    public Transform RightGrip { get; private set; }
    public Transform LeftGrip { get; private set; }
    public int ArmRendererCount { get; private set; }

    public bool Initialize(Animator source, Camera camera, YQFirstPersonArmsAssets assets)
    {
        if (source == null || !source.isHuman || camera == null || assets == null) return false;
        _source = source;
        _camera = camera;
        _skeleton = new GameObject("FirstPerson_AnimatedArms").transform;
        _skeleton.SetParent(transform, false);
        _skeleton.localScale = source.transform.lossyScale;
        _bones[source.transform] = _skeleton;
        foreach (SkinnedMeshRenderer body in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh armMesh = assets.Resolve(body.sharedMesh);
            if (armMesh == null || !body.enabled || !body.gameObject.activeInHierarchy) continue;
            Transform meshTransform = CopyBone(body.transform);
            var skin = meshTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = armMesh;
            skin.sharedMaterials = body.sharedMaterials;
            var bones = new Transform[body.bones.Length];
            for (int i = 0; i < bones.Length; i++) bones[i] = CopyBone(body.bones[i]);
            skin.bones = bones;
            skin.rootBone = CopyBone(body.rootBone);
            skin.localBounds = body.localBounds;
            skin.updateWhenOffscreen = true;
            skin.shadowCastingMode = ShadowCastingMode.Off;
            skin.receiveShadows = false;
            // note: Compact arms omit facial shapes, so map retained body-shape weights by name rather than by their old numeric index.
            for (int i = 0; i < armMesh.blendShapeCount; i++)
            {
                int sourceShape = body.sharedMesh.GetBlendShapeIndex(armMesh.GetBlendShapeName(i));
                if (sourceShape >= 0) skin.SetBlendShapeWeight(i, body.GetBlendShapeWeight(sourceShape));
            }
            ArmRendererCount++;
        }
        if (ArmRendererCount == 0)
        {
            if (Application.isPlaying) Destroy(_skeleton.gameObject);
            else DestroyImmediate(_skeleton.gameObject);
            _bones.Clear();
            return false;
        }
        _rightSourceHand = source.GetBoneTransform(HumanBodyBones.RightHand);
        _leftSourceHand = source.GetBoneTransform(HumanBodyBones.LeftHand);
        RightHand = CopyBone(_rightSourceHand);
        LeftHand = CopyBone(_leftSourceHand);
        // note: Copy authored equipment sockets with the mirrored hand so both views use the same grip convention.
        Transform rightGrip = null;
        Transform leftGrip = null;
        foreach (Transform bone in _rightSourceHand.GetComponentsInChildren<Transform>(true))
            if (bone.name == "HandBoneR") rightGrip = bone;
        foreach (Transform bone in source.GetBoneTransform(HumanBodyBones.LeftLowerArm).GetComponentsInChildren<Transform>(true))
            if (bone.name == "ShieldBoneL") leftGrip = bone;
        RightGrip = rightGrip != null ? CopyBone(rightGrip) : RightHand;
        LeftGrip = leftGrip != null ? CopyBone(leftGrip) : LeftHand;
        _rightUpperArm = CopyBone(source.GetBoneTransform(HumanBodyBones.RightUpperArm));
        _leftUpperArm = CopyBone(source.GetBoneTransform(HumanBodyBones.LeftUpperArm));
        _rightForearm = CopyBone(source.GetBoneTransform(HumanBodyBones.RightLowerArm));
        _leftForearm = CopyBone(source.GetBoneTransform(HumanBodyBones.LeftLowerArm));
        // note: Use the approved ready pose, so switching view during a jump or strike cannot permanently recalibrate the hands around that transient pose.
        _rightRestHand = Vector3.Scale(assets.hasReadyPose ? assets.rightReadyHand : source.transform.InverseTransformPoint(_rightSourceHand.position), _skeleton.localScale);
        _leftRestHand = Vector3.Scale(assets.hasReadyPose ? assets.leftReadyHand : source.transform.InverseTransformPoint(_leftSourceHand.position), _skeleton.localScale);
        _sourceBones = new Transform[_bones.Count - 1];
        _viewBones = new Transform[_sourceBones.Length];
        int index = 0;
        foreach (var pair in _bones)
        {
            if (pair.Key == source.transform) continue;
            _sourceBones[index] = pair.Key;
            _viewBones[index++] = pair.Value;
        }
        // note: Frame the existing combat-ready hand pose once; attacks then retain their authored anticipation, strike, and recovery arcs.
        _restOffset = new Vector3(0f, -0.32f, 0.60f) - (_rightRestHand + _leftRestHand) * 0.5f;
        _previousCameraEuler = camera.transform.eulerAngles;
        NotifyEquipmentChanged();
        CopyPose();
        return true;
    }

    public void NotifyEquipmentChanged() => _equipAt = Time.time;
    // note: Editor previews can mirror a sampled pose without input injection or a second animation authority.
    public void SynchronizePose() { CopyPose(); FrameArms(true); }

    private Transform CopyBone(Transform source)
    {
        if (source == null) return null;
        if (_bones.TryGetValue(source, out Transform found)) return found;
        if (source == _source.transform.parent || !source.IsChildOf(_source.transform)) return _skeleton;
        Transform parent = CopyBone(source.parent);
        Transform copy = new GameObject(source.name).transform;
        copy.SetParent(parent, false);
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        _bones.Add(source, copy);
        return copy;
    }

    private void LateUpdate()
    {
        if (_source == null || _camera == null || _sourceBones == null) return;
        CopyPose();
        FrameArms(false);
        Vector3 cameraEuler = _camera.transform.eulerAngles;
        Vector3 target = new Vector3(Mathf.Clamp(Mathf.DeltaAngle(_previousCameraEuler.x, cameraEuler.x), -12f, 12f),
            Mathf.Clamp(Mathf.DeltaAngle(_previousCameraEuler.y, cameraEuler.y), -12f, 12f), 0f);
        _previousCameraEuler = cameraEuler;
        float dt = Time.deltaTime;
        _lookSway = Vector3.Lerp(_lookSway, target, 1f - Mathf.Exp(-14f * dt));
        // note: Camera motion adds only a small, damped viewmodel lag; it never changes aim, the authoritative skeleton, or movement.
        float equip = Mathf.Clamp01((Time.time - _equipAt) / 0.32f);
        equip = equip * equip * (3f - 2f * equip);
        _skeleton.localPosition = _poseOffset + new Vector3(-_lookSway.y * 0.0015f, _lookSway.x * 0.001f - (1f - equip) * 0.20f, -(1f - equip) * 0.08f);
        _skeleton.localRotation = Quaternion.Euler(_lookSway.x * -0.25f + (1f - equip) * 12f, _lookSway.y * -0.3f, _lookSway.y * -0.15f);
    }

    private void CopyPose()
    {
        if (_sourceBones == null) return;
        for (int i = 0; i < _sourceBones.Length; i++)
        {
            if (_sourceBones[i] == null || _viewBones[i] == null) continue;
            _viewBones[i].localPosition = _sourceBones[i].localPosition;
            _viewBones[i].localRotation = _sourceBones[i].localRotation;
            _viewBones[i].localScale = _sourceBones[i].localScale;
        }
        _skeleton.localPosition = _restOffset;
        _skeleton.localRotation = Quaternion.identity;
        _poseOffset = _restOffset;
    }

    private void FrameArms(bool sampled)
    {
        if (_source == null || _rightSourceHand == null || _leftSourceHand == null) return;
        AnimatorStateInfo action = _source.GetCurrentAnimatorStateInfo(1);
        bool castingPose = action.IsName("Actions.Cast") || action.IsName("Actions.CastHold") ||
            action.IsName("Actions.CastRelease") || action.IsName("Actions.Channel");
        _castFraming = sampled ? (castingPose ? 1f : 0f) : Mathf.MoveTowards(_castFraming, castingPose ? 1f : 0f, Time.deltaTime * 9f);
        AnimatorStateInfo body = _source.GetCurrentAnimatorStateInfo(0);
        bool attack = action.IsName("Actions.MeleeLeft") || action.IsName("Actions.MeleeRight") ||
            action.IsName("Actions.MeleeLeftRecover") || action.IsName("Actions.MeleeRightRecover") ||
            action.IsName("Actions.PunchLeft") || action.IsName("Actions.PunchRight");
        bool gesture = action.IsName("Actions.Interact") || action.IsName("Actions.Pickup") || action.IsName("Actions.Consume");
        _attackFraming = BlendFraming(_attackFraming, attack ? _source.GetLayerWeight(1) : 0f, sampled, 18f);
        _gestureFraming = BlendFraming(_gestureFraming, gesture ? _source.GetLayerWeight(1) : 0f, sampled, 12f);
        // note: The accepted full-body roll supplies timing. Hands brace, tuck below view through inversion, then recover; the camera and aim never roll.
        float phase = Mathf.Clamp01(body.normalizedTime);
        // note: Map authored segments back to one action phase so the hands do not pop out between travel and recovery.
        bool rolling = body.IsName("Base Layer.DodgeRollStart") || body.IsName("Base Layer.DodgeRoll") || body.IsName("Base Layer.DodgeRollRecover");
        float rollPhase = body.IsName("Base Layer.DodgeRollStart") ? .18f * phase :
            body.IsName("Base Layer.DodgeRollRecover") ? .74f + .26f * phase : .18f + .56f * phase;
        float roll = rolling ? SmoothWindow(rollPhase, .04f, .24f, .58f, .96f) : 0f;
        _rollTuck = BlendFraming(_rollTuck, roll, sampled, 16f);
        bool dashing = body.IsName("Base Layer.DashStart") || body.IsName("Base Layer.Dash") || body.IsName("Base Layer.DashRecover");
        float dashPhase = body.IsName("Base Layer.DashStart") ? .14f * phase :
            body.IsName("Base Layer.DashRecover") ? .55f + .45f * phase : .14f + .41f * phase;
        _dashBrace = BlendFraming(_dashBrace, dashing ? Mathf.Sin(dashPhase * Mathf.PI) : 0f, sampled, 20f);
        _landingSettle = BlendFraming(_landingSettle, body.IsName("Base Layer.Land") ? Mathf.Sin(phase * Mathf.PI) : 0f, sampled, 16f);
        bool deathPose = body.IsName("Base Layer.Death");
        _deathFraming = sampled ? (deathPose ? 1f : 0f) : Mathf.MoveTowards(_deathFraming, deathPose ? 1f : 0f, Time.deltaTime * 4f);
        // note: Retarget authored hand arcs into camera reach. Continuous arms keep their shoulder cuts behind the camera; wrist/finger angles remain authored.
        RetargetForearm(_rightSourceHand, RightHand, _rightUpperArm, _rightForearm, _rightRestHand, 1f);
        RetargetForearm(_leftSourceHand, LeftHand, _leftUpperArm, _leftForearm, _leftRestHand, -1f);
    }


    private static float BlendFraming(float current, float target, bool sampled, float speed)
    {
        return sampled ? target : Mathf.MoveTowards(current, target, Time.deltaTime * speed);
    }

    private static float SmoothWindow(float phase, float enterStart, float enterEnd, float exitStart, float exitEnd)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(enterStart, enterEnd, phase)) *
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(exitStart, exitEnd, phase)));
    }

    private void RetargetForearm(Transform sourceHand, Transform hand, Transform upperArm, Transform forearm, Vector3 rest, float side)
    {
        if (hand == null || upperArm == null || forearm == null) return;
        Vector3 current = Vector3.Scale(_source.transform.InverseTransformPoint(sourceHand.position), _skeleton.localScale);
        Vector3 delta = current - rest;
        float reach = Mathf.Lerp(1f, 0.35f, _castFraming);
        // note: A narrow idle envelope previously flattened authored strikes into small wrist motions. Preserve larger attack/reach arcs while keeping idle and casting composed.
        Vector3 gain = Vector3.Lerp(new Vector3(.45f, .28f, .25f), new Vector3(.82f, .62f, .72f), _attackFraming);
        gain = Vector3.Lerp(gain, new Vector3(.58f, .50f, .56f), _gestureFraming);
        Vector3 target = new Vector3(side * .20f, Mathf.Lerp(-.23f, -.18f, _castFraming), .42f) + Vector3.Scale(delta, gain) * reach;
        float activeReach = Mathf.Max(_attackFraming, _gestureFraming);
        target.x = Mathf.Clamp(target.x, Mathf.Lerp(-.34f, -.43f, activeReach), Mathf.Lerp(.34f, .43f, activeReach));
        target.y = Mathf.Clamp(target.y, Mathf.Lerp(-.30f, -.42f, activeReach), Mathf.Lerp(-.12f, .08f, activeReach));
        target.z = Mathf.Clamp(target.z, .30f, Mathf.Lerp(.55f, .76f, activeReach));
        target += new Vector3(side * .035f * _rollTuck, -.60f * _rollTuck - .09f * _dashBrace - .055f * _landingSettle, -.07f * _rollTuck);
        // note: The same terminal body state lowers first-person hands out of view, and revival raises them without creating a second death timer.
        target.y -= _deathFraming * 0.65f;
        Vector3 targetWorld = transform.TransformPoint(target);
        // note: Keep the shoulder cut behind the camera and solve both arm joints at their authored lengths. Forearm-only geometry exposed a severed elbow during extended strikes.
        Vector3 shoulder = transform.TransformPoint(new Vector3(side * .24f, -.30f, -.12f) +
            new Vector3(0f, -.60f * _rollTuck - .09f * _dashBrace - .055f * _landingSettle - .65f * _deathFraming, -.07f * _rollTuck));
        float upperLength = Vector3.Distance(upperArm.position, forearm.position);
        float lowerLength = Vector3.Distance(forearm.position, hand.position);
        if (upperLength < .001f || lowerLength < .001f) return;
        Vector3 reachDirection = (targetWorld - shoulder).normalized;
        float distance = Mathf.Clamp(Vector3.Distance(shoulder, targetWorld), Mathf.Abs(upperLength - lowerLength) + .001f, (upperLength + lowerLength) * .98f);
        targetWorld = shoulder + reachDirection * distance;
        Vector3 pole = transform.TransformPoint(new Vector3(side * .65f, -.65f, -.02f)) - shoulder;
        Vector3 bend = Vector3.ProjectOnPlane(pole, reachDirection).normalized;
        float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
        Vector3 elbow = shoulder + reachDirection * along + bend * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        upperArm.position = shoulder;
        upperArm.rotation = Quaternion.FromToRotation(forearm.position - upperArm.position, elbow - shoulder) * upperArm.rotation;
        // note: Authored wrist/finger angles and approved equipment sockets follow the solved arm without changing the body or aim.
        forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, targetWorld - forearm.position) * forearm.rotation;
    }
}
