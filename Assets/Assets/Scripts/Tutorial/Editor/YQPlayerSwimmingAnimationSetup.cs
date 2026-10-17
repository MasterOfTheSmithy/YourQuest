using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// note: Explicit editor authoring adds only water presentation to the existing GUID/controller; reload never changes accepted player assets.
public static class YQPlayerSwimmingAnimationSetup
{
    // note: This disposable preview fixture never invokes player startup, profile persistence, controller authoring, or death/loot behavior.
    public static string[] VerifyContracts()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Verify water contracts in Edit Mode.");
        var checks = new List<string>();
        Scene preview = EditorSceneManager.NewPreviewScene();
        Mesh mesh = null;
        YQWaterSurfaceMotion motion = null;
        YQInvestorPlayerMotor previousMotor = YQInvestorPlayerMotor.ActiveMotor;
        int previousBuckets = WaterBucketCount();
        try
        {
            GameObject surface = new GameObject("WaterContractPreview");
            SceneManager.MoveGameObjectToScene(surface, preview);
            mesh = new Mesh { name = "DisposableWaterContractQuad", vertices = new[] { new Vector3(-2f, 2f, -2f), new Vector3(2f, 2f, -2f), new Vector3(2f, 2f, 2f), new Vector3(-2f, 2f, 2f) }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
            MeshFilter filter = surface.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
            motion = surface.AddComponent<YQWaterSurfaceMotion>();
            motion.Configure(filter, renderer, YQHydrologyKindV2.River);
            Require(YQWaterSurfaceMotion.TryGetWaterHeight(Vector3.zero, out float height) && Mathf.Abs(height - 2f) < .001f, "registered water samples its actual horizontal triangles", checks);
            Require(!YQWaterSurfaceMotion.TryGetWaterHeight(new Vector3(4f, 0f, 4f), out _), "dry positions outside the exact water footprint stay dry", checks);
            renderer.enabled = false;
            Require(YQWaterSurfaceMotion.TryGetWaterHeight(Vector3.zero, out _), "render-only culling preserves physical water", checks);
            renderer.enabled = true;

            GameObject actor = new GameObject("DisposableWaterMotor");
            SceneManager.MoveGameObjectToScene(actor, preview);
            CharacterController controller = actor.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = .38f;
            controller.center = new Vector3(0f, .9f, 0f);
            YQInvestorVitals vitals = actor.AddComponent<YQInvestorVitals>();
            Write(vitals, "<CurrentHealth>k__BackingField", 100f);
            YQInvestorPlayerMotor motor = actor.AddComponent<YQInvestorPlayerMotor>();
            motor.vitals = vitals;
            Write(motor, "_controller", controller);
            Write(motor, "_standingControllerCenter", controller.center);
            Write(motor, "_standingControllerHeight", controller.height);
            Write(motor, "_breathRemaining", 60f);
            Invoke(motor, "RefreshWaterContact");
            Require(motor.IsSwimming && motor.IsHeadUnderwater, "deep immersion is accepted by the existing motor", checks);
            Invoke(motor, "UpdateBreathing", 0f);
            Require(Mathf.Abs(motor.BreathRemainingSeconds - 60f) < .001f && Mathf.Abs(vitals.CurrentHealth - 100f) < .001f, "zero simulation time consumes no breath or health", checks);
            Invoke(motor, "UpdateBreathing", 59f);
            Require(Mathf.Abs(motor.BreathRemainingSeconds - 1f) < .001f && Mathf.Abs(vitals.CurrentHealth - 100f) < .001f, "59 underwater seconds retain breath without drowning", checks);
            Invoke(motor, "UpdateBreathing", 1f);
            Require(motor.BreathRemainingSeconds == 0f && Mathf.Abs(vitals.CurrentHealth - 100f) < .001f, "full 60-second natural breath budget precedes damage", checks);
            Invoke(motor, "UpdateBreathing", 1f);
            Require(Mathf.Abs(vitals.CurrentHealth - 92f) < .001f, "drowning uses the existing vitals damage owner", checks);
            Write(motor, "_headUnderwater", false);
            Invoke(motor, "UpdateBreathing", 1f);
            Require(Mathf.Abs(motor.BreathRemainingSeconds - 12f) < .001f && Mathf.Abs(vitals.CurrentHealth - 92f) < .001f, "surfacing recovers oxygen and ends drowning ticks", checks);
            Invoke(motor, "UpdateBreathing", 20f);
            Require(Mathf.Abs(motor.BreathRemainingSeconds - 60f) < .001f, "oxygen recovery clamps to natural capacity", checks);

            motion.enabled = false;
            Invoke(motion, "OnDisable");
            Require(!YQWaterSurfaceMotion.TryGetWaterHeight(Vector3.zero, out _), "disabled streamed water unregisters", checks);
            motion.enabled = true;
            Invoke(motion, "OnEnable");
            Require(YQWaterSurfaceMotion.TryGetWaterHeight(Vector3.zero, out _), "reenabling restores the same water surface", checks);
            surface.transform.position = new Vector3(37f, 3f, -29f);
            motion.Configure(filter, renderer, YQHydrologyKindV2.Lake);
            Require(YQWaterSurfaceMotion.TryGetWaterHeight(new Vector3(37f, 0f, -29f), out height) && Mathf.Abs(height - 5f) < .001f && !YQWaterSurfaceMotion.TryGetWaterHeight(Vector3.zero, out _), "reconfiguration indexes final translated geometry and releases old cells", checks);
            // note: Ordinary MonoBehaviours have no running Awake lifecycle in an Edit Mode preview; exercise the real unload callback explicitly before disposing its scene object.
            Invoke(motion, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(surface);
            Require(!YQWaterSurfaceMotion.TryGetWaterHeight(new Vector3(37f, 0f, -29f), out _), "unloaded water leaves no physical volume", checks);
            Require(YQInvestorPlayerMotor.ActiveMotor == previousMotor, "preview preserves the live authoritative motor", checks);
        }
        finally
        {
            // note: Failed assertions must release the same preview-owned registration before scene disposal, preserving later checks and editor state.
            if (motion != null) Invoke(motion, "OnDestroy");
            EditorSceneManager.ClosePreviewScene(preview);
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
        }
        Require(WaterBucketCount() == previousBuckets, "preview teardown restores water query registry", checks);
        return checks.ToArray();
    }

    private static int WaterBucketCount() => ((System.Collections.IDictionary)typeof(YQWaterSurfaceMotion).GetField("QueryCells", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Count;
    private static void Write(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Require(bool passed, string check, List<string> checks)
    {
        if (!passed) throw new InvalidOperationException("Water contract failed: " + check);
        checks.Add(check);
    }

    [MenuItem("YourQuest/Player Animation/Apply Swimming Motions")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply swimming motions in Edit Mode.");
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(YQAuthoredPlayerAnimations.ControllerPath);
        if (controller == null) throw new InvalidOperationException("The authoritative player controller is required.");
        AnimationClip idle = YQAuthoredPlayerAnimations.Library(1, "Swim_Idle_Loop");
        AnimationClip forward = YQAuthoredPlayerAnimations.Library(1, "Swim_Fwd_Loop");
        if (idle == null || forward == null || !idle.humanMotion || !forward.humanMotion)
            throw new InvalidOperationException("Approved humanoid UAL1 swimming clips are required; no substitute gait was installed.");
        AnimatorStateMachine body = controller.layers[0].stateMachine;
        AnimatorState locomotion = Find(body, "Locomotion");
        if (locomotion == null) throw new InvalidOperationException("The accepted Locomotion state is missing.");
        AddParameter(controller, "Swimming", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "SwimSpeed", AnimatorControllerParameterType.Float);
        AddParameter(controller, "SwimRate", AnimatorControllerParameterType.Float);
        AnimatorState swim = Find(body, "Swim");
        if (swim == null) swim = body.AddState("Swim", new Vector3(850f, 550f));
        BlendTree tree = swim.motion as BlendTree;
        if (tree == null)
        {
            tree = new BlendTree { name = "YQ_SwimMotion", blendType = BlendTreeType.Simple1D, blendParameter = "SwimSpeed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            swim.motion = tree;
        }
        tree.children = new[] { new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f }, new ChildMotion { motion = forward, threshold = 1f, timeScale = 1f } };
        swim.speedParameter = "SwimRate";
        swim.speedParameterActive = true;
        swim.writeDefaultValues = false;
        // note: Existing airborne/traversal AnyState routes must not interrupt a submerged stroke; death remains the highest terminal condition.
        foreach (AnimatorStateTransition transition in body.anyStateTransitions)
        {
            if (transition.destinationState == null || transition.destinationState == swim || transition.destinationState.name == "Death") continue;
            bool guarded = false;
            foreach (AnimatorCondition condition in transition.conditions) guarded |= condition.parameter == "Swimming";
            if (!guarded) transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Swimming");
        }
        AnimatorStateTransition enter = null;
        foreach (AnimatorStateTransition transition in body.anyStateTransitions) if (transition.destinationState == swim) enter = transition;
        if (enter == null)
        {
            enter = body.AddAnyStateTransition(swim);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Swimming");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Mounted");
        }
        Configure(enter);
        enter.canTransitionToSelf = false;
        AnimatorStateTransition exit = null;
        foreach (AnimatorStateTransition transition in swim.transitions) if (transition.destinationState == locomotion) exit = transition;
        if (exit == null)
        {
            exit = swim.AddTransition(locomotion);
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Swimming");
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        }
        Configure(exit);
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(swim);
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[YQPlayerSwimmingAnimationSetup] Approved UAL1 swim/tread motions installed on the existing authoritative controller. Visual motion acceptance still requires PlaySafe observation in both views.");
    }

    private static AnimatorState Find(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states) if (child.state.name == name) return child.state;
        return null;
    }

    private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
            if (parameter.name == name)
            {
                if (parameter.type != type) throw new InvalidOperationException("Conflicting existing swimming parameter: " + name);
                return;
            }
        controller.AddParameter(name, type);
    }

    private static void Configure(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = .18f;
        transition.offset = 0f;
    }
}
