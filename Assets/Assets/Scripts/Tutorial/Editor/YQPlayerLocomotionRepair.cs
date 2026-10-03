using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Repair only the existing controller's gait. Authored combat/traversal, its GUID, and the player's movement authority remain intact.
public static class YQPlayerLocomotionRepair
{
    private const string GenericPath = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Animations/Human Generic.FBX";
    private static readonly List<string> Checks = new List<string>();

    public static void ApplyToController(AnimatorController controller)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Repair locomotion in Edit Mode.");
        var state = Find(controller.layers[0].stateMachine, "Locomotion");
        var tree = (BlendTree)state.motion;
        AnimationClip idle = Approved(controller, "Idle");
        AnimationClip ready = Approved(controller, "Idle_Combat");
        AnimationClip walk = Approved(controller, "Walk");
        AnimationClip run = Approved(controller, "Run");
        AnimationClip back = Approved(controller, "WalkBack");
        SetDefault(controller, "FirstPersonPose", 0f);
        SetDefault(controller, "WalkFootSpeed", 1f);
        SetDefault(controller, "RunFootSpeed", 3f);
        SetDefault(controller, "BackFootSpeed", 1f);
        SetDefault(controller, "CrouchFootSpeed", 1f);

        BlendTree idleTree = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(YQAuthoredPlayerAnimations.ControllerPath))
            if (asset is BlendTree found && found.name == "Upright idle and first person ready") idleTree = found;
        if (idleTree == null)
        {
            idleTree = new BlendTree { name = "Upright idle and first person ready" };
            AssetDatabase.AddObjectToAsset(idleTree, controller);
        }
        // note: The native body pack supplies a relaxed idle and a true walk; the combat-ready overlay is reserved for first-person framing.
        idleTree.blendType = BlendTreeType.Simple1D;
        idleTree.blendParameter = "FirstPersonPose";
        idleTree.useAutomaticThresholds = false;
        idleTree.children = Array.Empty<ChildMotion>();
        idleTree.AddChild(idle, 0f);
        idleTree.AddChild(ready, 1f);
        tree.children = Array.Empty<ChildMotion>();
        tree.AddChild(idleTree, Vector2.zero);
        tree.AddChild(walk, new Vector2(0f, 0.6f));
        tree.AddChild(run, Vector2.up);
        tree.AddChild(back, new Vector2(0f, -0.6f));
        foreach (float sign in new[] { -1f, 1f })
        {
            tree.AddChild(walk, new Vector2(sign * 0.6f, 0f));
            tree.AddChild(run, new Vector2(sign, 0f));
            tree.AddChild(back, new Vector2(sign * 0.424264f, -0.424264f));
        }
        state.speed = 1f;
        state.speedParameter = "LocomotionRate";
        state.speedParameterActive = true;

        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject actor = null;
        try
        {
            Animator animator = CreateActor(controller, scene, out actor);
            // note: Measure stance-foot travel on the actual normalized body, instead of assuming a clip's nominal speed matches this rig's proportions.
            float walkFootSpeed = StanceSpeed(animator, new Vector2(0f, 0.6f), walk.length, false);
            float runFootSpeed = StanceSpeed(animator, Vector2.up, run.length, false);
            float backFootSpeed = StanceSpeed(animator, new Vector2(0f, -0.6f), back.length, true);
            float crouchFootSpeed = StanceSpeed(animator, new Vector2(0f, .3f), YQAuthoredPlayerAnimations.Library(1, "Crouch_Fwd_Loop").length, false, true);
            // note: Changing parameter defaults invalidates live preview bindings. Finish every sample before publishing the calibration.
            SetDefault(controller, "WalkFootSpeed", walkFootSpeed);
            SetDefault(controller, "RunFootSpeed", runFootSpeed);
            SetDefault(controller, "BackFootSpeed", backFootSpeed);
            SetDefault(controller, "CrouchFootSpeed", crouchFootSpeed);
        }
        finally
        {
            if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        EditorUtility.SetDirty(idleTree);
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("YourQuest/Player Animation/Repair and Verify Body Locomotion")]
    public static void Verify()
    {
        Checks.Clear();
        Scene scene = default;
        GameObject actor = null;
        GameObject cameraObject = null;
        GameObject lamp = null;
        var samples = new List<object>();
        string status = "FAIL";
        string error = null;
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(YQAuthoredPlayerAnimations.ControllerPath);
            ApplyToController(controller);
            scene = EditorSceneManager.NewPreviewScene();
            Animator animator = CreateActor(controller, scene, out actor);
            Require(animator.isHuman, "approved production body binds the repaired controller");
            Require(!animator.applyRootMotion, "motor retains displacement ownership");
            cameraObject = new GameObject("Locomotion preview camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            camera.transform.position = new Vector3(2.6f, 1.35f, 3.4f);
            camera.transform.LookAt(new Vector3(0f, 0.95f, 0f));
            camera.fieldOfView = 35f;
            lamp = new GameObject("Locomotion preview light");
            SceneManager.MoveGameObjectToScene(lamp, scene);
            lamp.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Player_Animation_Locomotion_Repair_2026-10-02"));
            Directory.CreateDirectory(output);
            // note: Record layer isolation alongside the rendered idle so a masked grip cannot silently overwrite relaxed arm motion.
            for (int isolated = 0; isolated < 3; isolated++)
            {
                animator.SetLayerWeight(1, isolated < 2 ? 1f : 0f);
                animator.SetLayerWeight(2, isolated == 0 ? 1f : 0f);
                Pose(animator, Vector2.zero, 0.25f);
                samples.Add(new { idleLayers = isolated, rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation.ToString("F4"), rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm).localRotation.ToString("F4") });
                YQAuthoredPlayerAnimations.Render(camera, Path.Combine(output, "idle-layers-" + isolated + ".png"), actor);
            }
            animator.SetLayerWeight(1, 0f);
            animator.SetLayerWeight(2, 0f);
            foreach (var gait in new[] { ("idle", Vector2.zero), ("walk", new Vector2(0f, 0.6f)), ("run", Vector2.up), ("back", new Vector2(0f, -0.6f)) })
            {
                float minimumFoot = float.PositiveInfinity;
                float maximumFoot = float.NegativeInfinity;
                float maximumLegAngle = 0f;
                Quaternion initial = Quaternion.identity;
                float lateralLeanSum = 0f;
                for (int i = 0; i < 32; i++)
                {
                    Pose(animator, gait.Item2, i / 32f);
                    // note: Signed cycle-average torso bank catches a straight sprint that permanently leans to one side.
                    Vector3 torso = actor.transform.InverseTransformDirection(animator.GetBoneTransform(HumanBodyBones.Head).position - animator.GetBoneTransform(HumanBodyBones.Hips).position);
                    lateralLeanSum += Mathf.Atan2(torso.x, torso.y) * Mathf.Rad2Deg;
                    Transform leg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    float z = actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position).z * actor.transform.lossyScale.z;
                    minimumFoot = Mathf.Min(minimumFoot, z);
                    maximumFoot = Mathf.Max(maximumFoot, z);
                    if (i == 0) initial = leg.localRotation;
                    maximumLegAngle = Mathf.Max(maximumLegAngle, Quaternion.Angle(initial, leg.localRotation));
                    if (i % 8 == 0) YQAuthoredPlayerAnimations.Render(camera, Path.Combine(output, gait.Item1 + "-" + i.ToString("D2") + ".png"), actor);
                }
                float meanLateralLean = lateralLeanSum / 32f;
                Require(Mathf.Abs(meanLateralLean) < 3f, gait.Item1 + " has no sustained sideways torso bank");
                samples.Add(new { gait = gait.Item1, meanLateralLeanDegrees = meanLateralLean });
                float footTravel = maximumFoot - minimumFoot;
                if (gait.Item1 != "idle")
                {
                    Require(footTravel > 0.25f, gait.Item1 + " has visible fore/aft foot travel above 25 cm");
                    Require(maximumLegAngle > 15f, gait.Item1 + " has a substantial upper-leg stride");
                }
                else
                {
                    Vector3 upright = animator.GetBoneTransform(HumanBodyBones.Head).position - animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    float lean = Vector3.Angle(upright, Vector3.up);
                    Require(lean < 12f, "relaxed idle keeps the torso upright");
                    samples.Add(new { gait = "idle", torsoLeanDegrees = lean });
                }
                samples.Add(new { gait = gait.Item1, footTravelMetres = footTravel, upperLegArcDegrees = maximumLegAngle });
            }
            samples.Add(new { walkFootSpeed = animator.GetFloat("WalkFootSpeed"), runFootSpeed = animator.GetFloat("RunFootSpeed"), backFootSpeed = animator.GetFloat("BackFootSpeed") });
            status = "PASS";
        }
        catch (Exception exception) { error = exception.ToString(); Debug.LogException(exception); }
        finally
        {
            if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (lamp != null) UnityEngine.Object.DestroyImmediate(lamp);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllText(Path.Combine(Application.dataPath, "../Docs/Player_Animation_Locomotion_Editor_Receipt_2026-10-02.json"),
                JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, evidence = "Editor sampling on the normalized approved production body; production motor and gear require the separate PlaySafe witness", checks = Checks, samples, error }, Formatting.Indented));
        }
        Debug.Log("[YourQuest Player Animation] Body locomotion verification " + status + ".");
    }

    private static Animator CreateActor(AnimatorController controller, Scene scene, out GameObject actor)
    {
        actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(YQAuthoredPlayerAnimations.BodyPath));
        SceneManager.MoveGameObjectToScene(actor, scene);
        Bounds bounds = default;
        bool found = false;
        foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>())
        {
            if (renderer is ParticleSystemRenderer) continue;
            if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
        }
        if (found && bounds.size.y > 0.1f) actor.transform.localScale *= 1.86f / bounds.size.y;
        // note: Preview the approved source textures through the project's existing render-pipeline adapter; imported materials stay untouched.
        YQRuntimeUrpMaterialRepair.RepairHierarchy(actor);
        var animator = actor.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);
        return animator;
    }

    private static void Pose(Animator animator, Vector2 motion, float phase, bool crouch = false)
    {
        animator.SetBool("Grounded", true);
        animator.SetFloat("MoveX", motion.x);
        animator.SetFloat("MoveZ", motion.y);
        animator.SetFloat("FirstPersonPose", 0f);
        animator.SetFloat("LocomotionRate", 1f);
        animator.SetFloat("CrouchRate", 1f);
        animator.SetBool("Crouching", crouch);
        animator.Play(crouch ? "Base Layer.Crouch" : "Base Layer.Locomotion", 0, phase);
        animator.Update(0f);
    }

    private static float StanceSpeed(Animator animator, Vector2 motion, float duration, bool backward, bool crouch = false)
    {
        const int count = 96;
        var feet = new Vector3[count];
        float floor = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Pose(animator, motion, i / (float)count, crouch);
            feet[i] = animator.transform.parent != null ? animator.transform.parent.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position) : animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            floor = Mathf.Min(floor, feet[i].y);
        }
        float travel = 0f;
        int contacts = 0;
        for (int i = 1; i < count; i++)
        {
            float delta = (feet[i].z - feet[i - 1].z) * (backward ? 1f : -1f);
            if (feet[i].y < floor + 0.08f && feet[i - 1].y < floor + 0.08f && delta > 0f)
            { travel += delta; contacts++; }
        }
        if (contacts < 3 || duration <= 0f) throw new InvalidOperationException("No measurable stance-foot phase in the approved gait.");
        return Mathf.Max(0.1f, travel / (contacts * duration / count));
    }

    private static AnimationClip Approved(AnimatorController controller, string name)
    {
        string clipName = name + " (body locomotion)";
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(YQAuthoredPlayerAnimations.ControllerPath))
            if (asset is AnimationClip previous && previous.name == clipName) return previous;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(GenericPath))
            if (asset is AnimationClip source && source.name == name)
            {
                var clip = UnityEngine.Object.Instantiate(source);
                clip.name = clipName;
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                AssetDatabase.AddObjectToAsset(clip, controller);
                return clip;
            }
        throw new InvalidOperationException("Required approved body motion is missing: " + name);
    }

    private static AnimatorState Find(AnimatorStateMachine machine, string name)
    {
        foreach (var child in machine.states) if (child.state.name == name) return child.state;
        throw new InvalidOperationException("Missing player state: " + name);
    }

    private static void SetDefault(AnimatorController controller, string name, float value)
    {
        bool found = false;
        foreach (var parameter in controller.parameters) found |= parameter.name == name;
        if (!found) controller.AddParameter(name, AnimatorControllerParameterType.Float);
        var parameters = controller.parameters;
        foreach (var parameter in parameters) if (parameter.name == name) parameter.defaultFloat = value;
        controller.parameters = parameters;
    }

    private static void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException(check);
        Checks.Add(check);
    }
}
