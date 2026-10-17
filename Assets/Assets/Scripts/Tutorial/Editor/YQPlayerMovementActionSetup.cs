using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// note: Explicit authoring extends the accepted controller in place, preserving its GUID and existing action ownership.
public static class YQPlayerMovementActionSetup
{
    private const string ControllerPath = "Assets/Assets/Resources/Player/YQPlayer.controller";
    [MenuItem("YourQuest/Player Animation/Apply Movement and Combat Timing Pass")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply movement assets in Edit Mode.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || controller.layers.Length < 3) throw new InvalidOperationException("Existing production controller missing.");
        // note: Validate required imported clips before authoring so a missing source cannot leave half-installed action states.
        AnimationClip sword = YQAuthoredPlayerAnimations.Library(2, "Sword_Regular_A");
        AnimationClip heavy = YQAuthoredPlayerAnimations.Library(2, "Sword_Heavy_Combo");
        AnimationClip guard = YQAuthoredPlayerAnimations.Library(2, "Sword_Block");
        AnimationClip getUp = YQAuthoredPlayerAnimations.Library(2, "LayToIdle");
        AnimationClip slideStart = YQAuthoredPlayerAnimations.Library(2, "Slide_Start");
        AnimationClip slideLoop = YQAuthoredPlayerAnimations.Library(2, "Slide_Loop");
        AnimationClip slideEnd = YQAuthoredPlayerAnimations.Library(2, "Slide_Exit");
        AnimationClip jab = YQAuthoredPlayerAnimations.Library(1, "Punch_Jab");
        AnimationClip cross = YQAuthoredPlayerAnimations.Library(1, "Punch_Cross");
        Parameter(controller, "Blocking", AnimatorControllerParameterType.Bool);
        Parameter(controller, "PowerSliding", AnimatorControllerParameterType.Bool);
        Parameter(controller, "Recovering", AnimatorControllerParameterType.Bool);
        Parameter(controller, "ActionRate", AnimatorControllerParameterType.Float);
        Parameter(controller, "ActionPhase", AnimatorControllerParameterType.Float);
        Parameter(controller, "AttackLeft", AnimatorControllerParameterType.Bool);
        var actions = controller.layers[1].stateMachine;
        InstallAttack(controller, actions, "MeleeRight", sword, false, .42f);
        InstallAttack(controller, actions, "MeleeLeft", sword, true, .42f);
        InstallAttack(controller, actions, "PunchRight", cross, false, .4f);
        InstallAttack(controller, actions, "PunchLeft", jab, true, .4f);
        InstallAttack(controller, actions, "TwoHandAttack", heavy, false, .28f);
        InstallAttack(controller, actions, "RangedAttack", BowStroke(controller), false, .5f);
        State(actions, "RangedAttack").mirrorParameter = "AttackLeft";
        State(actions, "RangedAttack").mirrorParameterActive = true;
        AnimatorState guarded = State(actions, "Guard");
        guarded.motion = Normalize(controller, guard, "Guard hold (movement pass)", 1f, true);
        foreach (var old in guarded.transitions) guarded.RemoveTransition(old);
        var guardExit = Transition(guarded, State(actions, "Empty"), .08f);
        guardExit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Blocking");
        var body = controller.layers[0].stateMachine;
        AnimatorState start = State(body, "PowerSlideStart"), loop = State(body, "PowerSlide"), end = State(body, "PowerSlideEnd");
        start.motion = Normalize(controller, slideStart, "Power slide entry", .12f, false);
        loop.motion = Normalize(controller, slideLoop, "Power slide loop", .45f, true);
        end.motion = Normalize(controller, slideEnd, "Power slide exit", .18f, false);
        ReplaceExit(start, loop, true, 1f, .04f);
        ReplaceExit(loop, end, false, 0f, .06f).AddCondition(AnimatorConditionMode.IfNot, 0f, "PowerSliding");
        ReplaceExit(end, State(body, "Locomotion"), true, 1f, .08f);
        AnimatorState recovery = State(body, "GetUp");
        recovery.motion = Normalize(controller, getUp, "Get up (movement pass)", .65f, false);
        ReplaceExit(recovery, State(body, "Locomotion"), true, 1f, .08f);
        // note: Grounded slide/get-up cannot be replaced by generic locomotion routes until their accepted phase completes; death still interrupts.
        foreach (var entry in body.anyStateTransitions)
            if (entry.destinationState != null && entry.destinationState.name != "Death" && !entry.destinationState.name.StartsWith("PowerSlide", StringComparison.Ordinal))
                AddConditionOnce(entry, "PowerSliding", AnimatorConditionMode.IfNot);
        AnimatorState climb = State(body, "Climb");
        climb.motion = ClimbStroke(controller);
        State(body, "ClimbStart").motion = Normalize(controller, (AnimationClip)climb.motion, "Climb high entry (movement pass)", .18f, false);
        climb.speedParameter = "ClimbRate"; climb.speedParameterActive = true;
        var parameters = controller.parameters;
        foreach (var p in parameters) if (p.name == "ClimbCycleSeconds") p.defaultFloat = 1f;
        controller.parameters = parameters;
        foreach (var entry in body.anyStateTransitions)
            if (entry.destinationState != null && entry.destinationState.name != "Death") AddConditionOnce(entry, "Recovering", AnimatorConditionMode.IfNot);

        var assets = AssetDatabase.LoadAssetAtPath<YQPlayerActionAssets>("Assets/Assets/Resources/Player/YQPlayerActionAssets.asset");
        if (assets == null)
        { assets = ScriptableObject.CreateInstance<YQPlayerActionAssets>(); AssetDatabase.CreateAsset(assets, "Assets/Assets/Resources/Player/YQPlayerActionAssets.asset"); }
        assets.arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Bows, Arrows & Crossbows/Arrow001/Prefab/Arrow001.prefab");
        if (assets.arrowPrefab == null) throw new InvalidOperationException("Approved arrow prefab missing.");
        string audio = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - SHARED FILES/Sound Effects/";
        assets.gravelContact = AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "RunningGravel_1.wav");
        assets.woodContact = AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "Wood_Footstep_1.wav");
        assets.wetContact = AssetDatabase.LoadAssetAtPath<AudioClip>(audio + "WetFootsteps_1.wav");
        EditorUtility.SetDirty(assets); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(assets); AssetDatabase.SaveAssetIfDirty(controller);
        Debug.Log("[YQMovementPass] Applied timing, guard, slide, climb, get-up and approved arrow binding. Visual/runtime acceptance remains required.");
    }

    private static void InstallAttack(AnimatorController controller, AnimatorStateMachine machine, string name, AnimationClip source, bool mirror, float sourceContact)
    {
        AnimatorState state = State(machine, name);
        state.motion = Normalize(controller, source, name + " timed (movement pass)", 1f, false, sourceContact);
        state.mirror = mirror; state.writeDefaultValues = false;
        state.timeParameter = "ActionPhase"; state.timeParameterActive = true;
        foreach (var old in state.transitions) state.RemoveTransition(old);
        var exit = Transition(state, State(machine, "Empty"), .06f);
        exit.AddCondition(AnimatorConditionMode.Greater, .999f, "ActionPhase");
    }

    private static AnimationClip Normalize(AnimatorController controller, AnimationClip source, string name, float duration, bool loop, float contact = .4f)
    {
        var clip = new AnimationClip { name = name, frameRate = 60f };
        int samples = Mathf.CeilToInt(duration * 60f);
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            if (binding.type != typeof(Animator) || binding.propertyName.StartsWith("Root", StringComparison.Ordinal) || binding.propertyName.StartsWith("Motion", StringComparison.Ordinal)) continue;
            AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
            var keys = new Keyframe[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float authored = t <= .4f ? Mathf.Lerp(0f, contact, t / .4f) : Mathf.Lerp(contact, 1f, (t - .4f) / .6f);
                keys[i] = new Keyframe(t * duration, curve.Evaluate(authored * source.length));
            }
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
        }
        var settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.startTime = 0f; settings.stopTime = duration; settings.loopTime = loop; settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return Store(controller, clip);
    }

    private static AnimationClip ClimbStroke(AnimatorController controller)
    {
        // note: Author an in-place high-hand alternating reach; wall-contact corrections occur on the same canonical bones at runtime.
        var clip = new AnimationClip { name = "Climb high contact (movement pass)", frameRate = 60f };
        Muscle(clip, "Left Arm Down-Up", .6f, .88f, .6f, .32f, .6f);
        Muscle(clip, "Right Arm Down-Up", .6f, .32f, .6f, .88f, .6f);
        Muscle(clip, "Left Arm Front-Back", -.25f, -.3f, -.25f, -.15f, -.25f);
        Muscle(clip, "Right Arm Front-Back", -.25f, -.15f, -.25f, -.3f, -.25f);
        Muscle(clip, "Left Forearm Stretch", -.1f, .45f, -.1f, -.55f, -.1f);
        Muscle(clip, "Right Forearm Stretch", -.1f, -.55f, -.1f, .45f, -.1f);
        Muscle(clip, "Left Upper Leg Front-Back", .1f, .45f, .1f, -.1f, .1f);
        Muscle(clip, "Right Upper Leg Front-Back", .1f, -.1f, .1f, .45f, .1f);
        Muscle(clip, "Left Lower Leg Stretch", -.15f, -.65f, -.15f, .1f, -.15f);
        Muscle(clip, "Right Lower Leg Stretch", -.15f, .1f, -.15f, -.65f, -.15f);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; settings.stopTime = 1f;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return Store(controller, clip);
    }

    private static AnimationClip BowStroke(AnimatorController controller)
    {
        // note: Supported ranged weapons get an authored draw/release on the production humanoid rather than a spell gesture.
        var clip = new AnimationClip { name = "Ranged draw release (movement pass)", frameRate = 60f };
        Muscle(clip, "Left Arm Down-Up", .15f, .4f, .42f, .3f, .15f);
        Muscle(clip, "Right Arm Down-Up", .15f, .35f, .4f, .25f, .15f);
        Muscle(clip, "Left Forearm Stretch", .1f, -.45f, -.7f, -.05f, .1f);
        Muscle(clip, "Right Forearm Stretch", .1f, .7f, .8f, .6f, .1f);
        return Store(controller, clip);
    }

    private static void Muscle(AnimationClip clip, string property, params float[] values)
    {
        if (Array.IndexOf(HumanTrait.MuscleName, property) < 0) throw new InvalidOperationException("Unknown humanoid muscle: " + property);
        var keys = new Keyframe[values.Length];
        for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(i / (float)(keys.Length - 1), values[i]);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), new AnimationCurve(keys));
    }
    private static AnimationClip Store(AnimatorController controller, AnimationClip clip)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            if (asset is AnimationClip old && old.name == clip.name)
            { EditorUtility.CopySerialized(clip, old); UnityEngine.Object.DestroyImmediate(clip); EditorUtility.SetDirty(old); return old; }
        AssetDatabase.AddObjectToAsset(clip, controller); return clip;
    }
    private static AnimatorState State(AnimatorStateMachine machine, string name)
    { foreach (var entry in machine.states) if (entry.state.name == name) return entry.state;
      var result = machine.AddState(name); result.writeDefaultValues = false; return result; }
    private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float seconds)
    { var t = from.AddTransition(to); t.hasFixedDuration = true; t.duration = seconds; t.hasExitTime = false; return t; }
    private static AnimatorStateTransition ReplaceExit(AnimatorState from, AnimatorState to, bool exitTime, float time, float seconds)
    { foreach (var old in from.transitions) from.RemoveTransition(old); var t = Transition(from, to, seconds); t.hasExitTime = exitTime; t.exitTime = time; return t; }
    private static void Parameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    { foreach (var p in controller.parameters) if (p.name == name) { if (p.type != type) throw new InvalidOperationException("Parameter mismatch: " + name); return; } controller.AddParameter(name, type); }
    private static void AddConditionOnce(AnimatorStateTransition transition, string name, AnimatorConditionMode mode)
    { foreach (var c in transition.conditions) if (c.parameter == name) return; transition.AddCondition(mode, 0f, name); }
}
