using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// note: Explicit, repeatable updates extend the existing controller; they never create another player or execute gameplay from animation events.
public static class YQPlayerActionAnimationSetup
{
    private const string ControllerPath = "Assets/Assets/Resources/Player/YQPlayer.controller";
    private const string LibraryPath = "Assets/Assets/Art/PlayerAnimation/Quaternius/";
    private const string NativePath = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Animations/Human Generic.FBX";

    [MenuItem("YourQuest/Player Animation/Apply Complete Action Coverage")]
    public static void Apply()
    {
        ApplyToController(AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath));
        Debug.Log("[YourQuest Player Animation] Authored action coverage applied. Run pose and runtime verification before accepting the result.");
    }

    public static void ApplyToController(AnimatorController controller)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply action assets in Edit Mode.");
        if (controller == null || controller.layers.Length < 3) throw new InvalidOperationException("The established three-layer player controller is required.");
        EnsureLibraryImports();
        var layers = controller.layers;
        // note: An empty override state can retain a pose. Runtime raises this layer only while an accepted action is being presented.
        layers[1].defaultWeight = 0f;
        controller.layers = layers;
        foreach (var layer in layers) RepairReferences(layer.stateMachine);
        Parameter(controller, "DodgeRolling", AnimatorControllerParameterType.Bool);
        Parameter(controller, "RollRate", AnimatorControllerParameterType.Float);
        Parameter(controller, "DodgeRoll", AnimatorControllerParameterType.Trigger);
        var parameters = controller.parameters;
        foreach (var parameter in parameters) if (parameter.name == "RollRate") parameter.defaultFloat = 1f / .6f;
        controller.parameters = parameters;

        var body = layers[0].stateMachine;
        var roll = State(body, "DodgeRoll");
        roll.motion = Library(1, "Roll");
        roll.speed = roll.motion.averageDuration;
        roll.speedParameter = "RollRate";
        roll.speedParameterActive = true;
        var enter = Entry(body, roll);
        ResetConditions(enter);
        enter.AddCondition(AnimatorConditionMode.If, 0, "DodgeRoll");
        enter.AddCondition(AnimatorConditionMode.If, 0, "DodgeRolling");
        enter.AddCondition(AnimatorConditionMode.If, 0, "Dashing");
        Alive(enter);
        foreach (var transition in body.anyStateTransitions)
            if (transition.destinationState != null && transition.destinationState.name == "Dash")
            {
                bool exists = false;
                foreach (var condition in transition.conditions) exists |= condition.parameter == "DodgeRolling";
                if (!exists) transition.AddCondition(AnimatorConditionMode.IfNot, 0, "DodgeRolling");
            }
        foreach (var transition in roll.transitions) roll.RemoveTransition(transition);
        var recover = roll.AddTransition(State(body, "Locomotion"));
        Configure(recover);
        recover.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
        recover.AddCondition(AnimatorConditionMode.If, 0, "Grounded");

        var actions = layers[1].stateMachine;
        Action(controller, actions, "PunchLeft", Library(1, "Punch_Jab"));
        Action(controller, actions, "PunchRight", Library(1, "Punch_Cross"));
        Action(controller, actions, "Interact", Library(1, "Interact"));
        Action(controller, actions, "Pickup", Library(1, "PickUp_Table"));
        Action(controller, actions, "Consume", Library(2, "Consume"));
        Action(controller, actions, "Equip", Native(controller, "GrabWeaponR"));
        Action(controller, actions, "Unequip", Native(controller, "PutAwayWeaponR"));
        // note: Keep explicit re-authoring from restoring the older timer-truncated traversal states.
        YQPlayerTraversalAnimationRepair.ApplyToController(controller);
        foreach (var layer in layers)
            foreach (var child in layer.stateMachine.states) EditorUtility.SetDirty(child.state);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    public static void EnsureLibraryImports()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Refresh authored animation imports in Edit Mode.");
        // note: Reimport only the project-added libraries when their saved orientation predates the corrected adapter; native pack imports stay untouched.
        foreach (int library in new[] { 1, 2 })
        {
            string path = LibraryPath + "UAL" + library + "_Standard.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Authored library importer missing: " + path);
            bool stale = importer.clipAnimations.Length == 0;
            foreach (var clip in importer.clipAnimations) stale |= clip.keepOriginalOrientation;
            if (stale) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }

    private static void Action(AnimatorController controller, AnimatorStateMachine machine, string name, AnimationClip clip)
    {
        Parameter(controller, name, AnimatorControllerParameterType.Trigger);
        var state = State(machine, name);
        state.motion = clip;
        state.speed = 1f;
        var enter = Entry(machine, state);
        ResetConditions(enter);
        enter.AddCondition(AnimatorConditionMode.If, 0, name);
        enter.AddCondition(AnimatorConditionMode.IfNot, 0, "Dashing");
        Alive(enter);
        foreach (var previous in state.transitions) state.RemoveTransition(previous);
        var recover = state.AddTransition(State(machine, "Empty"));
        Configure(recover);
        recover.hasExitTime = true;
        recover.exitTime = .95f;
    }

    private static AnimationClip Library(int library, string name) => Clip(LibraryPath + "UAL" + library + "_Standard.fbx", name);
    private static AnimationClip Clip(string path, string name)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                (clip.name == name || clip.name.EndsWith("|" + name, StringComparison.Ordinal)) && clip.humanMotion) return clip;
        throw new InvalidOperationException("Approved humanoid clip missing: " + name + " at " + path);
    }
    private static AnimationClip Native(AnimatorController controller, string name)
    {
        string ownedName = "YQ_Action_" + name;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            if (asset is AnimationClip found && found.name == ownedName) return found;
        // note: Native pack events target its demo scripts. Strip events only on a project-owned copy, leaving the imported source intact.
        var copy = UnityEngine.Object.Instantiate(Clip(NativePath, name));
        copy.name = ownedName;
        AnimationUtility.SetAnimationEvents(copy, Array.Empty<AnimationEvent>());
        AssetDatabase.AddObjectToAsset(copy, controller);
        return copy;
    }
    private static void RepairReferences(AnimatorStateMachine machine)
    {
        foreach (var child in machine.states) child.state.motion = RepairMotion(child.state.motion);
        foreach (var child in machine.stateMachines) RepairReferences(child.stateMachine);
    }
    private static Motion RepairMotion(Motion motion)
    {
        if (motion is AnimationClip clip && clip.name.StartsWith("__preview__", StringComparison.Ordinal))
        {
            string path = AssetDatabase.GetAssetPath(clip);
            if (path.StartsWith(LibraryPath, StringComparison.Ordinal)) return Clip(path, clip.name.Substring("__preview__".Length));
        }
        if (motion is BlendTree tree)
        {
            var children = tree.children;
            for (int i = 0; i < children.Length; i++) children[i].motion = RepairMotion(children[i].motion);
            tree.children = children;
            EditorUtility.SetDirty(tree);
        }
        return motion;
    }
    private static AnimatorState State(AnimatorStateMachine machine, string name)
    {
        foreach (var child in machine.states) if (child.state.name == name) return child.state;
        var state = machine.AddState(name);
        state.writeDefaultValues = false;
        return state;
    }
    private static AnimatorStateTransition Entry(AnimatorStateMachine machine, AnimatorState state)
    {
        foreach (var transition in machine.anyStateTransitions) if (transition.destinationState == state) return transition;
        var added = machine.AddAnyStateTransition(state);
        Configure(added);
        return added;
    }
    private static void Configure(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = .08f;
        transition.canTransitionToSelf = false;
    }
    private static void ResetConditions(AnimatorStateTransition transition)
    {
        foreach (var condition in transition.conditions) transition.RemoveCondition(condition);
    }
    private static void Alive(AnimatorStateTransition transition)
    {
        transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
        transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Climbing");
    }
    private static void Parameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var parameter in controller.parameters)
            if (parameter.name == name)
            {
                if (parameter.type != type) throw new InvalidOperationException("Animator parameter type mismatch: " + name);
                return;
            }
        controller.AddParameter(name, type);
    }
}
