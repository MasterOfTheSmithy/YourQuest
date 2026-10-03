using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// note: Explicit authoring extends the existing player controller; imported clips, movement, inventory and save ownership stay intact.
public static class YQPlayerTraversalAnimationRepair
{
    private const string RiderPath = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Legacy (v2) Animations/Human_DragonRider.FBX";

    [MenuItem("YourQuest/Player Animation/Repair Traversal Phases and Rider Motions")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author player motions in Edit Mode.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(YQAuthoredPlayerAnimations.ControllerPath);
        // note: Reuse established calibration and action setup. Its final extension installs these phases after the older single-state mappings.
        YQPlayerLocomotionRepair.ApplyToController(controller);
        YQPlayerActionAnimationSetup.ApplyToController(controller);
        YQPlayerTraversalAnimationVerification.Run();
        Debug.Log("[YQPlayerTraversal] Traversal phases and native rider motions applied; visual verification remains required.");
    }

    public static void ApplyToController(AnimatorController controller)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || controller == null) throw new InvalidOperationException("A stable Edit-mode player controller is required.");
        // note: Validate every source before changing the controller, so missing native clips cannot leave a partially authored state graph.
        foreach (string native in new[] { "Mount", "DismountStart", "Idle", "Walk", "Run", "Fly" }) RiderSource(native);
        var requiredBody = controller.layers[0].stateMachine;
        foreach (string state in new[] { "Locomotion", "Crouch", "JumpRise", "Fall", "Land", "Dash", "Climb" })
        { bool found = false; foreach (var child in requiredBody.states) found |= child.state.name == state; if (!found) throw new InvalidOperationException("Required player state missing: " + state); }
        foreach (string name in new[] { "TraversalPose", "JumpTakingOff", "Mounted", "MountedFlying" }) Parameter(controller, name, AnimatorControllerParameterType.Bool);
        foreach (string name in new[] { "ClimbEnter", "Mount", "Dismount" }) Parameter(controller, name, AnimatorControllerParameterType.Trigger);
        foreach (string name in new[] { "DashRate", "ClimbRate", "CrouchFootSpeed", "ClimbCycleSeconds", "ClimbCycleMeters", "MountedSpeed" }) Parameter(controller, name, AnimatorControllerParameterType.Float);
        Default(controller, "DashRate", 1f / .18f);
        Default(controller, "ClimbRate", 1f);
        var body = controller.layers[0].stateMachine;
        AnimationClip rollSource = YQAuthoredPlayerAnimations.Library(1, "Roll");
        AnimationClip dashSource = YQAuthoredPlayerAnimations.Library(2, "Sword_Dash");
        AnimationClip climbSource = ClimbStroke();

        // note: A dodge is one accepted revolution, with anticipation and recovery; its rotating middle must never repeat indefinitely.
        Phase(body, controller, "DodgeRollStart", rollSource, 0f, .18f, false, "RollRate", .18f);
        Phase(body, controller, "DodgeRoll", rollSource, .18f, .74f, false, "RollRate", .56f);
        Phase(body, controller, "DodgeRollRecover", rollSource, .74f, 1f, false, "RollRate", .26f);
        RedirectEntry(body, "DodgeRoll", "DodgeRollStart");
        Chain(body, "DodgeRollStart", "DodgeRoll", 1f, .025f);
        Chain(body, "DodgeRoll", "DodgeRollRecover", 1f, .035f);
        Recover(body, "DodgeRollRecover", .98f, .10f);

        // note: The motor's short displacement window drives anticipation/travel; a separate authored tail is not squeezed into that window.
        Phase(body, controller, "DashStart", dashSource, 0f, .14f, false, "DashRate", .30f);
        Phase(body, controller, "Dash", dashSource, .14f, .55f, true, "DashRate", .70f);
        AnimatorState dashRecover = Phase(body, controller, "DashRecover", dashSource, .55f, 1f, false, null, 1f);
        dashRecover.speed = ((AnimationClip)dashRecover.motion).length / .28f;
        RedirectEntry(body, "Dash", "DashStart");
        Chain(body, "DashStart", "Dash", 1f, .025f);
        Clear(State(body, "Dash"));
        var dashEnd = Transition(State(body, "Dash"), State(body, "DashRecover"), .07f);
        dashEnd.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dashing");
        Recover(body, "DashRecover", .98f, .10f);

        // note: The available ClimbUp_1m is a vault. Reuse the established in-place climbing stroke for continuous walls; cross-fade entry and return to native idle.
        AnimatorState climb = Phase(body, controller, "Climb", climbSource, 0f, 1f, true, "ClimbRate", 1f);
        Phase(body, controller, "ClimbStart", climbSource, 0f, .12f, false, null, 1f).speed = 1f;
        Phase(body, controller, "ClimbRecover", NativeIdle(), 0f, .28f, false, null, 1f).speed = 1f;
        Default(controller, "ClimbCycleSeconds", ((AnimationClip)climb.motion).length);
        Default(controller, "ClimbCycleMeters", 1f);
        RedirectEntry(body, "Climb", "ClimbStart");
        foreach (var enter in body.anyStateTransitions)
            if (enter.destinationState == State(body, "ClimbStart"))
            {
                Conditions(enter);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "ClimbEnter");
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Climbing");
                enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            }
        Chain(body, "ClimbStart", "Climb", 1f, .08f);
        Clear(climb);
        var climbEnd = Transition(climb, State(body, "ClimbRecover"), .12f);
        climbEnd.AddCondition(AnimatorConditionMode.IfNot, 0f, "Climbing");
        Recover(body, "ClimbRecover", .95f, .12f);

        // note: Physical takeoff/contact remains authoritative. The authored landing gets readable follow-through instead of a seven-times-speed snap.
        var rise = State(body, "JumpRise");
        rise.speed = rise.motion.averageDuration / .32f;
        Clear(rise);
        var apex = Transition(rise, State(body, "Fall"), .12f);
        apex.hasExitTime = true; apex.exitTime = .82f;
        apex.AddCondition(AnimatorConditionMode.Less, .1f, "VerticalSpeed");
        var earlyLand = Transition(rise, State(body, "Land"), .09f);
        earlyLand.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        var land = State(body, "Land");
        land.speed = land.motion.averageDuration / .34f;
        Recover(body, "Land", .90f, .12f);

        // note: Rider clips are retargetable motions from the character's own pack. These states do not implement riding or move/attach the player.
        AnimatorState mount = State(body, "Mount"); mount.motion = Rider(controller, "Mount"); mount.speed = 1f;
        AnimatorState dismount = State(body, "Dismount"); dismount.motion = Rider(controller, "DismountStart"); dismount.speed = 1f;
        var ride = State(body, "MountedRide");
        BlendTree rideTree = ride.motion as BlendTree;
        if (rideTree == null) { rideTree = new BlendTree { name = "Native rider travel" }; AssetDatabase.AddObjectToAsset(rideTree, controller); }
        rideTree.blendType = BlendTreeType.Simple1D; rideTree.blendParameter = "MountedSpeed"; rideTree.useAutomaticThresholds = false;
        rideTree.children = Array.Empty<ChildMotion>();
        rideTree.AddChild(Rider(controller, "Idle", true), 0f);
        rideTree.AddChild(Rider(controller, "Walk", true), .5f);
        rideTree.AddChild(Rider(controller, "Run", true), 1f);
        ride.motion = rideTree;
        State(body, "MountedFlight").motion = Rider(controller, "Fly", true);
        Entry(body, mount, "Mount"); Entry(body, dismount, "Dismount");
        Chain(body, "Mount", "MountedRide", .97f, .16f);
        Clear(ride);
        var toFlight = Transition(ride, State(body, "MountedFlight"), .20f); toFlight.AddCondition(AnimatorConditionMode.If, 0f, "MountedFlying");
        Clear(State(body, "MountedFlight"));
        var toRide = Transition(State(body, "MountedFlight"), ride, .20f); toRide.AddCondition(AnimatorConditionMode.IfNot, 0f, "MountedFlying");
        Recover(body, "Dismount", .95f, .15f);

        // note: Any-state contact/crouch rules must not cut across a traversal recovery or restart the middle of a climb.
        foreach (var transition in body.anyStateTransitions)
        {
            string target = transition.destinationState != null ? transition.destinationState.name : string.Empty;
            if (target == "Crouch" || target == "Fall") AddCondition(transition, "TraversalPose", AnimatorConditionMode.IfNot);
            if (target == "Fall") AddCondition(transition, "JumpTakingOff", AnimatorConditionMode.IfNot);
            if (target == "JumpRise" || target == "Crouch" || target == "Fall") AddCondition(transition, "Mounted", AnimatorConditionMode.IfNot);
        }
        foreach (var child in body.states) EditorUtility.SetDirty(child.state);
        EditorUtility.SetDirty(body); EditorUtility.SetDirty(rideTree); EditorUtility.SetDirty(controller);
    }

    private static AnimatorState Phase(AnimatorStateMachine body, AnimatorController controller, string name, AnimationClip source, float start, float end, bool loop, string rate, float fraction)
    {
        AnimatorState state = State(body, name);
        AnimationClip clip = Segment(controller, source, "YQ_Traversal_" + name, start, end, loop);
        state.motion = clip; state.speed = rate == null ? 1f : clip.length / fraction;
        state.speedParameter = rate ?? string.Empty; state.speedParameterActive = rate != null;
        state.writeDefaultValues = false;
        return state;
    }

    private static AnimationClip Rider(AnimatorController controller, string name, bool loop = false)
    {
        return Segment(controller, RiderSource(name), "YQ_Rider_" + name, 0f, 1f, loop);
    }

    private static AnimationClip ClimbStroke()
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(YQAuthoredPlayerAnimations.ControllerPath))
            if (asset is AnimationClip clip && clip.name == "Climb" && clip.humanMotion) return clip;
        throw new InvalidOperationException("Established project-owned in-place climb stroke missing. Do not substitute a vault loop.");
    }

    private static AnimationClip NativeIdle()
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(YQAuthoredPlayerAnimations.ControllerPath))
            if (asset is AnimationClip clip && clip.name == "Idle (body locomotion)" && clip.humanMotion) return clip;
        throw new InvalidOperationException("Approved native idle missing.");
    }

    private static AnimationClip RiderSource(string name)
    {
        AnimationClip source = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(RiderPath))
            if (asset is AnimationClip clip && clip.name == name && clip.humanMotion) { source = clip; break; }
        if (source == null) throw new InvalidOperationException("Native humanoid rider motion missing: " + name);
        return source;
    }

    private static AnimationClip Segment(AnimatorController controller, AnimationClip source, string name, float start, float end, bool loop)
    {
        // note: Preserve imported curves/settings. Event-free project-owned segments have stable local IDs on repeated authoring.
        var clip = new AnimationClip { name = name, frameRate = source.frameRate };
        float offset = source.length * start, duration = source.length * (end - start);
        int samples = Mathf.Max(2, Mathf.CeilToInt(duration * source.frameRate));
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            if (binding.type != typeof(Animator)) continue;
            AnimationCurve original = AnimationUtility.GetEditorCurve(source, binding);
            var keys = new Keyframe[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float time = duration * i / samples, sourceTime = offset + time;
                float before = Mathf.Max(0f, sourceTime - .0005f), after = Mathf.Min(source.length, sourceTime + .0005f);
                float slope = after > before ? (original.Evaluate(after) - original.Evaluate(before)) / (after - before) : 0f;
                keys[i] = new Keyframe(time, original.Evaluate(sourceTime), slope, slope);
            }
            if (loop)
            {
                // note: A cropped stroke does not naturally close. Distribute its pose drift across the cycle and match boundary tangents.
                float drift = keys[samples].value - keys[0].value;
                for (int i = 0; i <= samples; i++)
                {
                    float t = i / (float)samples;
                    keys[i].value -= drift * Mathf.SmoothStep(0f, 1f, t);
                    float correction = drift * 6f * t * (1f - t) / duration;
                    keys[i].inTangent -= correction; keys[i].outTangent -= correction;
                }
                float seamSlope = (keys[0].outTangent + keys[samples].inTangent) * .5f;
                keys[0].inTangent = keys[0].outTangent = seamSlope;
                keys[samples].inTangent = keys[samples].outTangent = seamSlope;
            }
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
        }
        var settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.startTime = 0f; settings.stopTime = duration; settings.loopTime = loop; settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        clip.EnsureQuaternionContinuity();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(YQAuthoredPlayerAnimations.ControllerPath))
            if (asset is AnimationClip previous && previous.name == name)
            { EditorUtility.CopySerialized(clip, previous); UnityEngine.Object.DestroyImmediate(clip); EditorUtility.SetDirty(previous); return previous; }
        AssetDatabase.AddObjectToAsset(clip, controller);
        return clip;
    }

    private static void Entry(AnimatorStateMachine body, AnimatorState state, string trigger)
    {
        AnimatorStateTransition enter = null;
        foreach (var previous in body.anyStateTransitions) if (previous.destinationState == state) enter = previous;
        if (enter == null) enter = body.AddAnyStateTransition(state);
        Conditions(enter); Configure(enter, .12f);
        enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dashing");
        enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Climbing");
    }
    private static void RedirectEntry(AnimatorStateMachine body, string from, string to)
    {
        AnimatorStateTransition retained = null;
        foreach (var enter in body.anyStateTransitions)
            if (enter.destinationState != null && (enter.destinationState.name == from || enter.destinationState.name == to))
            {
                // note: Older explicit setup adds a central-state entry again; retain its fresh conditions and remove duplicate triggers.
                if (retained != null) body.RemoveAnyStateTransition(retained);
                retained = enter; enter.destinationState = State(body, to); enter.canTransitionToSelf = false; enter.duration = .06f;
            }
    }
    private static void Chain(AnimatorStateMachine body, string from, string to, float exit, float blend)
    { var source = State(body, from); Clear(source); var next = Transition(source, State(body, to), blend); next.hasExitTime = true; next.exitTime = exit; }
    private static void Recover(AnimatorStateMachine body, string name, float exit, float blend)
    {
        var state = State(body, name); Clear(state);
        var ground = Transition(state, State(body, "Locomotion"), blend); ground.hasExitTime = true; ground.exitTime = exit;
        ground.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        var air = Transition(state, State(body, "Fall"), .10f);
        air.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
        air.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dashing");
        air.AddCondition(AnimatorConditionMode.IfNot, 0f, "Climbing");
    }
    private static AnimatorStateTransition Transition(AnimatorState source, AnimatorState destination, float blend)
    { var next = source.AddTransition(destination); Configure(next, blend); return next; }
    private static void Configure(AnimatorStateTransition transition, float blend)
    { transition.duration = blend; transition.hasFixedDuration = true; transition.hasExitTime = false; transition.canTransitionToSelf = false; transition.interruptionSource = TransitionInterruptionSource.None; }
    private static void Clear(AnimatorState state) { foreach (var transition in state.transitions) state.RemoveTransition(transition); }
    private static void Conditions(AnimatorStateTransition transition) { foreach (var condition in transition.conditions) transition.RemoveCondition(condition); }
    private static void AddCondition(AnimatorStateTransition transition, string name, AnimatorConditionMode mode)
    { foreach (var condition in transition.conditions) if (condition.parameter == name) return; transition.AddCondition(mode, 0f, name); }
    private static AnimatorState State(AnimatorStateMachine body, string name)
    { foreach (var child in body.states) if (child.state.name == name) return child.state; var state = body.AddState(name); state.writeDefaultValues = false; return state; }
    private static void Parameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var parameter in controller.parameters) if (parameter.name == name)
        { if (parameter.type != type) throw new InvalidOperationException("Player parameter type mismatch: " + name); return; }
        controller.AddParameter(name, type);
    }
    private static void Default(AnimatorController controller, string name, float value)
    { var parameters = controller.parameters; foreach (var parameter in parameters) if (parameter.name == name) parameter.defaultFloat = value; controller.parameters = parameters; }
}
