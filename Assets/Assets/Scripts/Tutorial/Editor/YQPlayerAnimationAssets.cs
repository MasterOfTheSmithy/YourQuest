using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Project-owned presentation assets reuse approved humanoid motion without editing imported models, avatars, or demo controllers.
[InitializeOnLoad]
public static class YQPlayerAnimationAssets
{
    public const string ControllerPath = "Assets/Assets/Resources/Player/YQPlayer.controller";
    private const string GenericPath = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Animations/Human Generic.FBX";
    private const string BarbarianPath = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Animations/Human Barbarian.FBX";
    private static readonly List<string> Checks = new List<string>();

    static YQPlayerAnimationAssets()
    {
        // note: Only create missing project assets after compilation in Edit Mode; subsequent reloads preserve their GUID and authored contents.
        EditorApplication.delayCall += CreateMissingAssets;
    }

    private static void CreateMissingAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = Build();
            // note: A missing controller is rebuilt with the authored library upgrade, retaining one creation path and the existing parameter contract.
            YQAuthoredPlayerAnimations.Apply();
        }
        EnsureSafeActionClips(controller);
    }

    private static void EnsureSafeActionClips(AnimatorController controller)
    {
        // note: Animation is presentation. Package cast events must never create demo projectiles, audio, or gameplay effects beside YourQuest's combat owner.
        bool changed = false;
        foreach (ChildAnimatorState child in controller.layers[1].stateMachine.states)
        {
            if (!(child.state.motion is AnimationClip source) || AssetDatabase.GetAssetPath(source) == ControllerPath)
                continue;
            // note: The project importer already strips events and configures authored loop/root settings; retain the casting hold loop.
            if (source.events.Length == 0 && AssetDatabase.GetAssetPath(source).StartsWith(YQAuthoredPlayerAnimations.LibraryDirectory, StringComparison.Ordinal))
                continue;
            var clip = new AnimationClip { name = child.state.name + " (approved motion)", frameRate = source.frameRate };
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
                if (binding.type == typeof(Animator))
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(source, binding));
            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.EnsureQuaternionContinuity();
            AssetDatabase.AddObjectToAsset(clip, controller);
            child.state.motion = clip;
            EditorUtility.SetDirty(child.state);
            changed = true;
        }
        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
        {
            if (child.state.name != "Death" || !(child.state.motion is AnimationClip death) || death.events.Length == 0)
                continue;
            AnimationUtility.SetAnimationEvents(death, Array.Empty<AnimationEvent>());
            EditorUtility.SetDirty(death);
            changed = true;
        }
        if (changed)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }

    private static AnimationClip Imported(string path, string name)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip clip && clip.name == name)
                return clip;
        throw new InvalidOperationException("Required approved player animation is missing: " + path + " / " + name);
    }

    private static AnimatorController Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Player animation assets must be built outside Play Mode.");
        AnimationClip idle = Imported(GenericPath, "Idle_Combat");
        AnimationClip walk = Imported(GenericPath, "Walk");
        AnimationClip run = Imported(GenericPath, "Run");
        AnimationClip back = Imported(GenericPath, "WalkBack");
        // note: Validate sources before creating anything; a missing asset cannot silently become an empty animation state.
        AnimationClip attackLeft = Imported(GenericPath, "Attack01L");
        AnimationClip attackRight = Imported(GenericPath, "Attack01R");
        AnimationClip cast = Imported(BarbarianPath, "CastSpell01");
        AnimationClip hit = Imported(GenericPath, "GotHit");
        AnimationClip death = Imported(GenericPath, "Death");
        AnimationClip grip = Imported(GenericPath, "Hand_Grip");
        string directory = Path.GetDirectoryName(ControllerPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(directory))
            AssetDatabase.CreateFolder("Assets/Assets/Resources", "Player");
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (string parameter in new[] { "MoveX", "MoveZ", "VerticalSpeed", "LocomotionRate" })
            controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
        foreach (string parameter in new[] { "Grounded", "Climbing", "Dashing", "Dead" })
            controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
        foreach (string parameter in new[] { "Jump", "Dash", "MeleeLeft", "MeleeRight", "Cast", "Hit", "Guard", "Channel", "Emote" })
            controller.AddParameter(parameter, AnimatorControllerParameterType.Trigger);
        var parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == "Grounded") parameters[i].defaultBool = true;
            if (parameters[i].name == "LocomotionRate") parameters[i].defaultFloat = 1f;
        }
        controller.parameters = parameters;

        AnimationClip left = PoseClip(controller, idle, "StrafeLeft", 0.8f, true, "strafe", -1f);
        AnimationClip right = PoseClip(controller, idle, "StrafeRight", 0.8f, true, "strafe", 1f);
        AnimationClip jump = PoseClip(controller, idle, "JumpRise", 0.35f, false, "jump");
        AnimationClip fall = PoseClip(controller, idle, "Fall", 0.8f, true, "fall");
        AnimationClip land = PoseClip(controller, idle, "Land", 0.14f, false, "land");
        AnimationClip dash = PoseClip(controller, idle, "Dash", 0.18f, false, "dash");
        AnimationClip climb = PoseClip(controller, idle, "Climb", 1.1f, true, "climb");
        AnimationClip guard = PoseClip(controller, idle, "Guard", 0.65f, false, "guard");
        AnimationClip channel = PoseClip(controller, idle, "Channel", 0.8f, false, "channel");
        AnimationClip emote = PoseClip(controller, idle, "Emote", 1.2f, false, "emote");

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        var locomotion = new BlendTree { name = "Directional locomotion", blendType = BlendTreeType.FreeformDirectional2D, blendParameter = "MoveX", blendParameterY = "MoveZ", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(locomotion, controller);
        locomotion.AddChild(idle, Vector2.zero);
        locomotion.AddChild(walk, new Vector2(0f, 0.6f));
        locomotion.AddChild(run, Vector2.up);
        locomotion.AddChild(back, new Vector2(0f, -0.6f));
        locomotion.AddChild(left, new Vector2(-0.6f, 0f));
        locomotion.AddChild(right, new Vector2(0.6f, 0f));
        AnimatorState moving = State(machine, "Locomotion", locomotion);
        moving.speedParameter = "LocomotionRate";
        moving.speedParameterActive = true;
        machine.defaultState = moving;
        AnimatorState rising = State(machine, "JumpRise", jump);
        AnimatorState falling = State(machine, "Fall", fall);
        AnimatorState landing = State(machine, "Land", land);
        AnimatorState dashing = State(machine, "Dash", dash);
        AnimatorState climbing = State(machine, "Climb", climb);
        // note: Imported demo death is configured to loop; a project copy holds the final pose until the existing vitals owner revives.
        var terminalDeath = UnityEngine.Object.Instantiate(death);
        terminalDeath.name = "Death (terminal)";
        var deathSettings = AnimationUtility.GetAnimationClipSettings(terminalDeath);
        deathSettings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(terminalDeath, deathSettings);
        AssetDatabase.AddObjectToAsset(terminalDeath, controller);
        AnimatorState dead = State(machine, "Death", terminalDeath);

        // note: Terminal state outranks traversal. Jump/fall follow physical contact, and dash never routes through an imported rolling jump.
        Condition(Any(machine, dead), "Dead", AnimatorConditionMode.If);
        AnimatorStateTransition enterClimb = Any(machine, climbing);
        Condition(enterClimb, "Climbing", AnimatorConditionMode.If);
        Alive(enterClimb);
        AnimatorStateTransition enterDash = Any(machine, dashing);
        Condition(enterDash, "Dash", AnimatorConditionMode.If);
        Alive(enterDash);
        AnimatorStateTransition enterJump = Any(machine, rising);
        Condition(enterJump, "Jump", AnimatorConditionMode.If);
        Alive(enterJump);
        AnimatorStateTransition enterFall = Any(machine, falling);
        Condition(enterFall, "Grounded", AnimatorConditionMode.IfNot);
        Condition(enterFall, "Climbing", AnimatorConditionMode.IfNot);
        Condition(enterFall, "Dashing", AnimatorConditionMode.IfNot);
        Condition(enterFall, "VerticalSpeed", AnimatorConditionMode.Less, 0f);
        Alive(enterFall);
        foreach (AnimatorState airborne in new[] { rising, falling })
            Condition(Transition(airborne, landing), "Grounded", AnimatorConditionMode.If);
        End(landing, moving, 0.9f);
        AnimatorStateTransition dashGround = Transition(dashing, moving);
        Condition(dashGround, "Dashing", AnimatorConditionMode.IfNot);
        Condition(dashGround, "Grounded", AnimatorConditionMode.If);
        dashGround.hasExitTime = true;
        dashGround.exitTime = 0.85f;
        AnimatorStateTransition climbGround = Transition(climbing, moving);
        Condition(climbGround, "Climbing", AnimatorConditionMode.IfNot);
        Condition(climbGround, "Grounded", AnimatorConditionMode.If);
        Condition(Transition(dead, moving), "Dead", AnimatorConditionMode.IfNot);

        // note: Masked combat keeps the authoritative gait/airborne legs playing while hands and torso perform accepted actions.
        AvatarMask upperBody = Mask(controller, "Upper body actions", AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
            AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK);
        controller.AddLayer("Actions");
        var layers = controller.layers;
        layers[1].defaultWeight = 1f;
        layers[1].avatarMask = upperBody;
        controller.layers = layers;
        AnimatorStateMachine actionMachine = layers[1].stateMachine;
        AnimatorState empty = State(actionMachine, "Empty", null);
        actionMachine.defaultState = empty;
        foreach (string interruption in new[] { "Dead", "Climbing", "Dashing" })
            Condition(Any(actionMachine, empty), interruption, AnimatorConditionMode.If);
        Action(actionMachine, empty, "MeleeLeft", attackLeft, 0.28f);
        Action(actionMachine, empty, "MeleeRight", attackRight, 0.28f);
        Action(actionMachine, empty, "Cast", cast, 0.6f);
        Action(actionMachine, empty, "Hit", hit, 0.24f);
        Action(actionMachine, empty, "Guard", guard, 0.65f);
        Action(actionMachine, empty, "Channel", channel, 0.8f);
        Action(actionMachine, empty, "Emote", emote, 1.2f);

        controller.AddLayer("Grip");
        layers = controller.layers;
        layers[2].defaultWeight = 1f;
        layers[2].avatarMask = Mask(controller, "Equipment grip", AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers);
        controller.layers = layers;
        layers[2].stateMachine.defaultState = State(layers[2].stateMachine, "Grip", grip);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[YourQuest Player Animation] Created project controller and ten missing humanoid motions; imported assets preserved.");
        return controller;
    }

    private static AnimationClip PoseClip(AnimatorController owner, AnimationClip basis, string name, float duration, bool loop, string pose, float side = 1f)
    {
        var clip = new AnimationClip { name = name, frameRate = 60f };
        // note: Start from the approved idle's humanoid pose/root orientation; author muscles rather than rig-specific bone paths.
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(basis))
        {
            if (binding.type != typeof(Animator)) continue;
            AnimationCurve source = AnimationUtility.GetEditorCurve(basis, binding);
            float value = source.Evaluate(0f);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, duration, value));
        }
        foreach (string muscle in HumanTrait.MuscleName)
        {
            var binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle);
            float baseline = AnimationUtility.GetEditorCurve(basis, binding)?.Evaluate(0f) ?? 0f;
            var keys = new Keyframe[17];
            for (int i = 0; i < keys.Length; i++)
            {
                float t = i / 16f;
                float offset = MuscleOffset(pose, muscle, t, side);
                keys[i] = new Keyframe(t * duration, Mathf.Clamp(baseline + offset, -1f, 1f));
            }
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) curve.SmoothTangents(i, 0f);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.EnsureQuaternionContinuity();
        AssetDatabase.AddObjectToAsset(clip, owner);
        return clip;
    }

    private static float MuscleOffset(string pose, string muscle, float t, float side)
    {
        bool left = muscle.StartsWith("Left ", StringComparison.Ordinal);
        float cycle = Mathf.Sin(t * Mathf.PI * 2f + (left ? 0f : Mathf.PI));
        float lift = Mathf.Max(0f, cycle);
        float envelope = Mathf.Sin(t * Mathf.PI);
        // note: Directional steps retain a forward-facing torso; air/dash poses contain no somersault or world displacement.
        switch (pose)
        {
            case "strafe":
                if (muscle.Contains("Upper Leg In-Out")) return side * (left ? -1f : 1f) * cycle * 0.28f;
                if (muscle.Contains("Lower Leg Stretch")) return -lift * 0.28f;
                if (muscle.Contains("Upper Leg Front-Back")) return lift * 0.08f;
                if (muscle.Contains("Arm Front-Back")) return -cycle * 0.06f;
                break;
            case "jump":
            case "fall":
                float air = pose == "jump" ? Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(t * 3f)) : 0.7f + 0.04f * cycle;
                if (muscle.Contains("Upper Leg Front-Back")) return (left ? 0.26f : 0.15f) * air;
                if (muscle.Contains("Lower Leg Stretch")) return -0.4f * air;
                if (muscle.Contains("Arm Down-Up")) return 0.14f * air;
                break;
            case "land":
            case "dash":
                if (muscle.Contains("Upper Leg Front-Back")) return 0.25f * envelope;
                if (muscle.Contains("Lower Leg Stretch")) return -0.42f * envelope;
                if (muscle == "Spine Front-Back") return 0.18f * envelope;
                if (pose == "dash" && muscle.Contains("Arm Front-Back")) return -0.2f * envelope;
                break;
            case "climb":
                if (muscle.Contains("Arm Down-Up")) return 0.45f + 0.22f * cycle;
                if (muscle.Contains("Arm Front-Back")) return 0.45f;
                if (muscle.Contains("Forearm Stretch")) return -0.22f * cycle;
                if (muscle.Contains("Upper Leg Front-Back")) return 0.24f + 0.22f * cycle;
                if (muscle.Contains("Lower Leg Stretch")) return -0.3f * lift;
                break;
            case "guard":
            case "channel":
                if (muscle.Contains("Arm Front-Back")) return 0.35f * envelope;
                if (muscle.Contains("Forearm Stretch")) return -0.35f * envelope;
                if (muscle.Contains("Arm Down-Up")) return (pose == "channel" ? 0.3f : 0.1f) * envelope;
                break;
            case "emote":
                if (!left && muscle.Contains("Arm Down-Up")) return 0.7f * envelope;
                if (!left && muscle.Contains("Forearm Stretch")) return -0.35f * envelope;
                if (!left && muscle.Contains("Hand In-Out")) return 0.3f * Mathf.Sin(t * Mathf.PI * 6f) * envelope;
                break;
        }
        return 0f;
    }

    private static AvatarMask Mask(AnimatorController owner, string name, params AvatarMaskBodyPart[] parts)
    {
        var mask = new AvatarMask { name = name };
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (var part in parts) mask.SetHumanoidBodyPartActive(part, true);
        AssetDatabase.AddObjectToAsset(mask, owner);
        return mask;
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion)
    {
        var state = machine.AddState(name);
        state.motion = motion;
        state.writeDefaultValues = false;
        return state;
    }

    private static AnimatorStateTransition Any(AnimatorStateMachine machine, AnimatorState destination)
    {
        var transition = machine.AddAnyStateTransition(destination);
        transition.canTransitionToSelf = false;
        transition.duration = 0.06f;
        transition.hasFixedDuration = true;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
        transition.orderedInterruption = true;
        return transition;
    }

    private static AnimatorStateTransition Transition(AnimatorState source, AnimatorState destination)
    {
        var transition = source.AddTransition(destination);
        transition.duration = 0.06f;
        transition.hasFixedDuration = true;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
        return transition;
    }

    private static void Condition(AnimatorStateTransition transition, string parameter, AnimatorConditionMode mode, float threshold = 0f) => transition.AddCondition(mode, threshold, parameter);
    private static void Alive(AnimatorStateTransition transition) => Condition(transition, "Dead", AnimatorConditionMode.IfNot);
    private static void End(AnimatorState source, AnimatorState destination, float exit)
    {
        var transition = Transition(source, destination);
        transition.hasExitTime = true;
        transition.exitTime = exit;
    }

    private static void Action(AnimatorStateMachine machine, AnimatorState empty, string name, AnimationClip clip, float seconds)
    {
        AnimatorState state = State(machine, name, clip);
        state.speed = Mathf.Max(0.01f, clip.length / seconds);
        var enter = Any(machine, state);
        enter.canTransitionToSelf = true;
        Condition(enter, name, AnimatorConditionMode.If);
        Alive(enter);
        Condition(enter, "Climbing", AnimatorConditionMode.IfNot);
        Condition(enter, "Dashing", AnimatorConditionMode.IfNot);
        End(state, empty, 0.92f);
    }

    [MenuItem("YourQuest/Player Animation/Verify Controller and Humanoid Motions %#F10")]
    public static void Verify()
    {
        Checks.Clear();
        GameObject model = null;
        Scene preview = default;
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Edit Mode verification after fresh compilation");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ?? Build();
            EnsureSafeActionClips(controller);
            Require(Resources.Load<RuntimeAnimatorController>(YQPlayerEquipmentVisual.PlayerAnimatorResourcePath) == controller, "controller is a runtime Resources asset");
            Require(controller.layers.Length == 3, "one base, masked actions, and finger grip layer");
            Require(controller.layers[1].avatarMask != null && !controller.layers[1].avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), "combat preserves leg locomotion");
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                if (asset is AnimationClip clip)
                {
                    Require(clip.humanMotion && clip.length > 0f, "generated humanoid motion: " + clip.name);
                    Require(clip.events.Length == 0, "presentation clip has no imported effect callbacks: " + clip.name);
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                        foreach (var key in AnimationUtility.GetEditorCurve(clip, binding).keys)
                            if (float.IsNaN(key.value) || float.IsInfinity(key.value)) throw new InvalidOperationException("Invalid animation curve: " + clip.name);
                }
            preview = EditorSceneManager.NewPreviewScene();
            model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GenericPath));
            SceneManager.MoveGameObjectToScene(model, preview);
            model.hideFlags = HideFlags.HideAndDontSave;
            Animator animator = model.GetComponent<Animator>();
            Require(animator != null && animator.isHuman, "approved humanoid rig binds the project controller");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            foreach (string state in new[] { "JumpRise", "Fall", "Land", "Dash", "DodgeRoll", "Climb", "Death" })
            {
                Require(animator.HasState(0, Animator.StringToHash("Base Layer." + state)), "base state: " + state);
                animator.SetBool("Dead", state == "Death");
                animator.SetBool("Climbing", state == "Climb");
                animator.SetBool("Grounded", state == "Land" || state == "Dash" || state == "DodgeRoll" || state == "Death");
                animator.SetBool("Dashing", state == "Dash" || state == "DodgeRoll");
                animator.SetBool("DodgeRolling", state == "DodgeRoll");
                animator.SetFloat("VerticalSpeed", state == "JumpRise" ? 4f : -4f);
                animator.Play("Base Layer." + state, 0, 0.15f);
                animator.Update(0f);
                Transform knee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Quaternion initial = knee.localRotation;
                animator.Play("Base Layer." + state, 0, 0.6f);
                animator.Update(0f);
                Require(Quaternion.Angle(initial, knee.localRotation) > 0.01f, "evaluated skeletal motion: " + state);
            }
            animator.SetBool("Dead", false);
            animator.SetBool("Climbing", false);
            animator.SetBool("Dashing", false);
            animator.SetBool("Grounded", true);
            animator.SetLayerWeight(1, 1f);
            foreach (string action in new[] { "MeleeLeft", "MeleeRight", "PunchLeft", "PunchRight", "Interact", "Pickup", "Consume", "Equip", "Unequip", "Cast", "Hit", "Guard", "Channel", "Emote" })
            {
                Require(animator.HasState(1, Animator.StringToHash("Actions." + action)), "action state: " + action);
                animator.SetTrigger(action);
                for (int i = 0; i < 8; i++) animator.Update(1f / 60f);
                Require(animator.GetCurrentAnimatorStateInfo(1).IsName("Actions." + action), "accepted action enters: " + action);
                // note: Native equipment gestures and authored emotes include a full recovery; do not truncate them to the old synthetic-action timeout.
                for (int i = 0; i < 360; i++) animator.Update(1f / 60f);
                Require(animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Empty"), "action returns without a stale trigger: " + action);
            }
            foreach (float x in new[] { -0.6f, 0.6f })
            {
                animator.SetFloat("MoveX", x);
                animator.SetFloat("MoveZ", 0f);
                animator.Play("Base Layer.Locomotion", 0, 0.05f);
                animator.Update(0f);
                Quaternion initial = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation;
                for (int i = 0; i < 15; i++) animator.Update(1f / 60f);
                Require(Quaternion.Angle(initial, animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation) > 0.01f, "directional strafe evaluates: " + x);
            }
            Require(model.transform.position.sqrMagnitude < 0.0001f, "animation never moves the player root");
            WriteReceipt("PASS", null);
            Debug.Log("[YourQuest Player Animation] Editor motion verification PASS.");
        }
        catch (Exception error)
        {
            WriteReceipt("FAIL", error.ToString());
            Debug.LogException(error);
        }
        finally
        {
            if (model != null) UnityEngine.Object.DestroyImmediate(model);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException(check);
        Checks.Add(check);
    }

    private static void WriteReceipt(string status, string error)
    {
        File.WriteAllText(Path.Combine(Application.dataPath, "../Docs/Player_Animation_Editor_Receipt_2026-10-01.json"),
            JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, mode = "Editor humanoid evaluation on a disposable preview rig; no profile or scene mutation", controller = ControllerPath, checks = Checks.ToArray(), error }, Formatting.Indented));
    }
}
