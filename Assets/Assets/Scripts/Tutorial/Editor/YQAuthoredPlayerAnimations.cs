using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Only newly added Quaternius animation models use this importer; approved existing model/import settings remain untouched.
public sealed class YQAuthoredAnimationImporter : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(YQAuthoredPlayerAnimations.LibraryDirectory, StringComparison.Ordinal)) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importCameras = false;
        importer.importLights = false;
    }

    private void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(YQAuthoredPlayerAnimations.LibraryDirectory, StringComparison.Ordinal)) return;
        var importer = (ModelImporter)assetImporter;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            // note: The motor remains the displacement owner. Looped authored gait/casting clips are retargeted in place.
            clip.loopTime = clip.name.EndsWith("_Loop", StringComparison.Ordinal) || clip.name.EndsWith("ClimbUp_1m", StringComparison.Ordinal);
            clip.loopPose = clip.loopTime;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            // note: These authored takes face opposite the production avatar when Original is retained. Humanoid body-forward gives all views the same facing.
            clip.keepOriginalOrientation = false;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            clip.events = Array.Empty<AnimationEvent>();
        }
        importer.clipAnimations = clips;
    }
}

// note: The upgrade retains the controller GUID, player contracts, imported body assets, and a single authoritative Animator.
public static class YQAuthoredPlayerAnimations
{
    public const string LibraryDirectory = "Assets/Assets/Art/PlayerAnimation/Quaternius/";
    public const string ControllerPath = "Assets/Assets/Resources/Player/YQPlayer.controller";
    public const string ArmsPath = "Assets/Assets/Resources/Player/YQFirstPersonArms.asset";
    public const string BodyPath = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Models/Bodies/Human Male (v4.1).FBX";
    private static readonly List<string> Checks = new List<string>();

    [MenuItem("YourQuest/Player Animation/Apply Authored Motions and First Person Arms")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply player animation assets in Edit Mode.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException("The existing player animation controller is required.");
        YQPlayerActionAnimationSetup.EnsureLibraryImports();
        AddBool(controller, "Crouching");
        AddBool(controller, "Casting");
        bool hasCrouchRate = false;
        foreach (var parameter in controller.parameters) hasCrouchRate |= parameter.name == "CrouchRate";
        if (!hasCrouchRate) controller.AddParameter("CrouchRate", AnimatorControllerParameterType.Float);
        AnimatorStateMachine body = controller.layers[0].stateMachine;
        var locomotion = (BlendTree)Find(body, "Locomotion").motion;
        var children = locomotion.children;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].position == Vector2.zero || children[i].position.y < 0f) continue;
            // note: Lateral movement uses a real authored gait while the visual body turns towards resolved travel; there are no generated sideways muscle waves.
            children[i].motion = children[i].position.y > 0.9f ? Library(1, "Sprint_Loop") : Library(1, "Jog_Fwd_Loop");
        }
        locomotion.children = children;
        Set(body, "JumpRise", Library(1, "Jump_Start"));
        Set(body, "Fall", Library(1, "Jump_Loop"));
        Set(body, "Land", Library(1, "Jump_Land"));
        Set(body, "Dash", Library(2, "Sword_Dash"));
        Set(body, "Climb", Library(2, "ClimbUp_1m"));
        Set(body, "Death", Library(1, "Death01"));
        Find(body, "JumpRise").speed = Find(body, "JumpRise").motion.averageDuration / 0.35f;
        Find(body, "Land").speed = Find(body, "Land").motion.averageDuration / 0.18f;
        Find(body, "Dash").speed = Find(body, "Dash").motion.averageDuration / 0.18f;

        // note: Authored crouch replaces the leg-bending fallback. Airborne/death/dash priorities still come from the existing motor/vitals parameters.
        AnimatorState crouch = GetOrAdd(body, "Crouch");
        crouch.speedParameter = "CrouchRate";
        crouch.speedParameterActive = true;
        if (!(crouch.motion is BlendTree))
        {
            var tree = new BlendTree { name = "Authored crouch", blendType = BlendTreeType.FreeformDirectional2D, blendParameter = "MoveX", blendParameterY = "MoveZ", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(Library(1, "Crouch_Idle_Loop"), Vector2.zero);
            foreach (Vector2 direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right }) tree.AddChild(Library(1, "Crouch_Fwd_Loop"), direction * 0.3f);
            crouch.motion = tree;
            var enter = body.AddAnyStateTransition(crouch);
            Configure(enter);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Crouching");
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dashing");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Climbing");
            var leave = crouch.AddTransition(Find(body, "Locomotion"));
            Configure(leave);
            leave.AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouching");
        }
        var actions = controller.layers[1].stateMachine;
        bool hasCancel = false;
        foreach (var parameter in controller.parameters) hasCancel |= parameter.name == "CancelCast";
        if (!hasCancel)
        {
            controller.AddParameter("CancelCast", AnimatorControllerParameterType.Trigger);
            var cancel = actions.AddAnyStateTransition(Find(actions, "Empty"));
            Configure(cancel);
            cancel.AddCondition(AnimatorConditionMode.If, 0f, "CancelCast");
            cancel.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        }
        Set(actions, "MeleeLeft", Library(2, "Sword_Regular_A"));
        Set(actions, "MeleeRight", Library(2, "Sword_Regular_B"));
        Set(actions, "Hit", Library(1, "Hit_Chest"));
        Set(actions, "Guard", Library(2, "Sword_Block"));
        Set(actions, "Channel", Library(1, "Spell_Simple_Shoot"));
        Set(actions, "Emote", Library(2, "Yes"));
        // note: Preserve readable anticipation and recovery rather than compressing whole imported attacks into a 0.28-second twitch.
        Find(actions, "MeleeLeft").speed = 1.35f;
        Find(actions, "MeleeRight").speed = 1.35f;
        Find(actions, "Hit").speed = 1.3f;
        Find(actions, "Guard").speed = 1f;
        Find(actions, "Channel").speed = 1f;
        Find(actions, "Emote").speed = 1f;
        AddMeleeRecovery(actions, "MeleeLeft", Library(2, "Sword_Regular_A_Rec"));
        AddMeleeRecovery(actions, "MeleeRight", Library(2, "Sword_Regular_B_Rec"));
        AnimatorState cast = Find(actions, "Cast");
        cast.motion = Library(1, "Spell_Simple_Enter");
        cast.speed = cast.motion.averageDuration / 0.22f;
        AnimatorState hold = GetOrAdd(actions, "CastHold");
        hold.motion = Library(1, "Spell_Simple_Idle_Loop");
        AnimatorState release = GetOrAdd(actions, "CastRelease");
        release.motion = Library(1, "Spell_Simple_Shoot");
        release.speed = release.motion.averageDuration / 0.3f;
        if (cast.transitions.Length == 1 && cast.transitions[0].destinationState.name == "Empty")
        {
            foreach (var previous in cast.transitions) cast.RemoveTransition(previous);
            var charging = cast.AddTransition(hold);
            Configure(charging);
            charging.hasExitTime = true;
            charging.exitTime = 0.92f;
            charging.AddCondition(AnimatorConditionMode.If, 0f, "Casting");
            var immediate = cast.AddTransition(release);
            Configure(immediate);
            immediate.hasExitTime = true;
            immediate.exitTime = 0.92f;
            immediate.AddCondition(AnimatorConditionMode.IfNot, 0f, "Casting");
            var finish = hold.AddTransition(release);
            Configure(finish);
            finish.AddCondition(AnimatorConditionMode.IfNot, 0f, "Casting");
            var recover = release.AddTransition(Find(actions, "Empty"));
            Configure(recover);
            recover.hasExitTime = true;
            recover.exitTime = 0.95f;
        }
        YQPlayerLocomotionRepair.ApplyToController(controller);
        YQPlayerActionAnimationSetup.ApplyToController(controller);
        BakeArms();
        EditorUtility.SetDirty(locomotion);
        EditorUtility.SetDirty(controller);
        foreach (var layer in controller.layers)
            foreach (var child in layer.stateMachine.states) EditorUtility.SetDirty(child.state);
        AssetDatabase.SaveAssets();
        Debug.Log("[YourQuest Player Animation] Authored motions and arm meshes prepared; body assets and controller GUID preserved.");
    }

    private static void BakeArms()
    {
        var assets = AssetDatabase.LoadAssetAtPath<YQFirstPersonArmsAssets>(ArmsPath);
        if (assets != null && assets.geometryVersion == 5 && assets.armMeshes != null && assets.armMeshes.Length > 0)
        {
            EnsureReadyPose(assets);
            return;
        }
        if (assets == null)
        {
            assets = ScriptableObject.CreateInstance<YQFirstPersonArmsAssets>();
            AssetDatabase.CreateAsset(assets, ArmsPath);
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath);
        if (model == null) throw new InvalidOperationException("Approved male body source is unavailable.");
        var sources = new List<Mesh>();
        var meshes = new List<Mesh>();
        foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh source = skin.sharedMesh;
            if (source == null || sources.Contains(source)) continue;
            // note: Retain the upper arms so extended first-person actions have continuous elbow geometry; shoulder cuts stay behind the camera.
            Animator bodyAnimator = model.GetComponent<Animator>();
            Transform rightUpper = bodyAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform leftUpper = bodyAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var armBones = new HashSet<int>();
            for (int i = 0; i < skin.bones.Length; i++)
            {
                string name = skin.bones[i] != null ? skin.bones[i].name : "";
                if (skin.bones[i] == rightUpper || skin.bones[i] == leftUpper || name.Contains("Forearm") || name.Contains("Palm") || name.Contains("Finger") || name.Contains("Thumb")) armBones.Add(i);
            }
            BoneWeight[] weights = source.boneWeights;
            var keep = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                float armWeight = (armBones.Contains(w.boneIndex0) ? w.weight0 : 0f) + (armBones.Contains(w.boneIndex1) ? w.weight1 : 0f) +
                    (armBones.Contains(w.boneIndex2) ? w.weight2 : 0f) + (armBones.Contains(w.boneIndex3) ? w.weight3 : 0f);
                keep[i] = armWeight >= 0.85f;
            }
            var triangles = new List<int>[source.subMeshCount];
            int count = 0;
            for (int sub = 0; sub < triangles.Length; sub++)
            {
                triangles[sub] = new List<int>();
                int[] indices = source.GetTriangles(sub);
                for (int i = 0; i < indices.Length; i += 3)
                    if (keep[indices[i]] && keep[indices[i + 1]] && keep[indices[i + 2]])
                    { triangles[sub].Add(indices[i]); triangles[sub].Add(indices[i + 1]); triangles[sub].Add(indices[i + 2]); count++; }
            }
            if (count < 20) continue;
            Mesh arms = CompactArms(source, triangles);
            // note: Update the owned derived mesh in place to retain its subasset reference and the resource GUID.
            Mesh previous = assets.Resolve(source);
            if (previous != null)
            {
                EditorUtility.CopySerialized(arms, previous);
                UnityEngine.Object.DestroyImmediate(arms);
                arms = previous;
                EditorUtility.SetDirty(arms);
            }
            else AssetDatabase.AddObjectToAsset(arms, assets);
            sources.Add(source);
            meshes.Add(arms);
            Debug.Log("[YourQuest Arms] " + source.name + " / " + count + " arm triangles / " + arms.vertexCount + " vertices / " + arms.blendShapeCount + " relevant shapes / " + AssetDatabase.GetAssetPath(source));
        }
        if (meshes.Count == 0) throw new InvalidOperationException("No approved arm geometry passed the skin-weight boundary.");
        assets.sourceMeshes = sources.ToArray();
        assets.armMeshes = meshes.ToArray();
        assets.geometryVersion = 5;
        EnsureReadyPose(assets);
        EditorUtility.SetDirty(assets);
    }

    private static Mesh CompactArms(Mesh source, List<int>[] triangles)
    {
        // note: Keep only vertices referenced by arm triangles. Full-body/face vertices must not inflate the first-person render or serialized resource.
        var used = new List<int>();
        var remap = new Dictionary<int, int>();
        foreach (var sub in triangles)
            for (int i = 0; i < sub.Count; i++)
            {
                int index = sub[i];
                if (!remap.TryGetValue(index, out int mapped))
                {
                    mapped = used.Count;
                    used.Add(index);
                    remap.Add(index, mapped);
                }
                sub[i] = mapped;
            }
        var arms = new Mesh { name = source.name + " (first person arms)", indexFormat = used.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        arms.vertices = SelectVertices(source.vertices, used);
        if (source.normals.Length > 0) arms.normals = SelectVertices(source.normals, used);
        if (source.tangents.Length > 0) arms.tangents = SelectVertices(source.tangents, used);
        if (source.colors32.Length > 0) arms.colors32 = SelectVertices(source.colors32, used);
        arms.boneWeights = SelectVertices(source.boneWeights, used);
        arms.bindposes = source.bindposes;
        for (int channel = 0; channel < 8; channel++)
        {
            var uv = new List<Vector4>();
            source.GetUVs(channel, uv);
            if (uv.Count == source.vertexCount) arms.SetUVs(channel, SelectVertices(uv.ToArray(), used));
        }
        arms.subMeshCount = triangles.Length;
        for (int sub = 0; sub < triangles.Length; sub++) arms.SetTriangles(triangles[sub], sub);
        var positions = new Vector3[source.vertexCount];
        var normals = new Vector3[source.vertexCount];
        var tangents = new Vector3[source.vertexCount];
        for (int shape = 0; shape < source.blendShapeCount; shape++)
        {
            bool affectsArms = false;
            for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape) && !affectsArms; frame++)
            {
                source.GetBlendShapeFrameVertices(shape, frame, positions, normals, tangents);
                foreach (int index in used)
                    // note: Ignore exporter noise below ten micrometers of position or 0.001 of a unit normal; retain every meaningful arm deformation.
                    if (positions[index].sqrMagnitude > 1e-10f || normals[index].sqrMagnitude > 1e-6f || tangents[index].sqrMagnitude > 1e-6f)
                    { affectsArms = true; break; }
            }
            if (!affectsArms) continue;
            // note: Relevant body-shape adjustments retain their names and frame weights; facial shapes with no arm influence are excluded.
            for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
            {
                source.GetBlendShapeFrameVertices(shape, frame, positions, normals, tangents);
                arms.AddBlendShapeFrame(source.GetBlendShapeName(shape), source.GetBlendShapeFrameWeight(shape, frame),
                    SelectVertices(positions, used), SelectVertices(normals, used), SelectVertices(tangents, used));
            }
        }
        arms.RecalculateBounds();
        return arms;
    }

    private static T[] SelectVertices<T>(T[] source, List<int> used)
    {
        var selected = new T[used.Count];
        for (int i = 0; i < selected.Length; i++) selected[i] = source[used[i]];
        return selected;
    }

    private static void EnsureReadyPose(YQFirstPersonArmsAssets assets)
    {
        if (assets.hasReadyPose) return;
        // note: Bake only presentation calibration on a disposable approved body, preserving the live player's pose and state.
        var sample = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath));
        try
        {
            var animator = sample.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.SetBool("Grounded", true);
            animator.Play("Base Layer.Locomotion", 0, 0f);
            animator.Play("Actions.Empty", 1, 0f);
            animator.Update(0.001f);
            assets.rightReadyHand = animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightHand).position);
            assets.leftReadyHand = animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
            assets.hasReadyPose = true;
            EditorUtility.SetDirty(assets);
        }
        finally { UnityEngine.Object.DestroyImmediate(sample); }
    }

    internal static AnimationClip Library(int library, string name) => Clip(LibraryDirectory + "UAL" + library + "_Standard.fbx", name);
    private static AnimationClip Clip(string path, string name)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                (clip.name == name || clip.name.EndsWith("|" + name, StringComparison.Ordinal)) && clip.humanMotion && clip.events.Length == 0) return clip;
        throw new InvalidOperationException("Required authored humanoid motion unavailable: " + name + " in " + path);
    }
    private static AnimatorState Find(AnimatorStateMachine machine, string name)
    {
        foreach (var state in machine.states) if (state.state.name == name) return state.state;
        throw new InvalidOperationException("Player state is missing: " + name);
    }
    private static AnimatorState GetOrAdd(AnimatorStateMachine machine, string name)
    {
        foreach (var state in machine.states) if (state.state.name == name) return state.state;
        var created = machine.AddState(name);
        created.writeDefaultValues = false;
        return created;
    }
    private static void Set(AnimatorStateMachine machine, string name, Motion motion) => Find(machine, name).motion = motion;
    private static void AddBool(AnimatorController controller, string name)
    {
        foreach (var parameter in controller.parameters) if (parameter.name == name) return;
        controller.AddParameter(name, AnimatorControllerParameterType.Bool);
    }
    private static void Configure(AnimatorStateTransition transition)
    {
        transition.canTransitionToSelf = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.10f;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
    }

    private static void AddMeleeRecovery(AnimatorStateMachine actions, string strikeName, AnimationClip motion)
    {
        // note: Each swing continues through its authored recovery before locomotion resumes; a new accepted action may still interrupt it.
        AnimatorState strike = Find(actions, strikeName);
        AnimatorState recover = GetOrAdd(actions, strikeName + "Recover");
        recover.motion = motion;
        recover.speed = motion.length / 0.24f;
        if (strike.transitions.Length == 1 && strike.transitions[0].destinationState.name == "Empty")
        {
            strike.RemoveTransition(strike.transitions[0]);
            var follow = strike.AddTransition(recover);
            Configure(follow);
            follow.hasExitTime = true;
            follow.exitTime = 0.95f;
            var finish = recover.AddTransition(Find(actions, "Empty"));
            Configure(finish);
            finish.hasExitTime = true;
            finish.exitTime = 0.95f;
        }
    }

    // note: Separate-project batch verification prepares assets while the production Editor is owned by another live test.
    public static void PrepareBatch()
    {
        try
        {
            // note: Verify a fresh import with the saved GUID/local-ID metadata before publishing controller references into the shared project.
            AssetDatabase.ImportAsset(LibraryDirectory + "UAL1_Standard.fbx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(LibraryDirectory + "UAL2_Standard.fbx", ImportAssetOptions.ForceUpdate);
            Apply();
            VerifyAndRender();
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [MenuItem("YourQuest/Player Animation/Verify and Preview Authored Motions")]
    public static void VerifyAndRender()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Verify prepared assets outside Play Mode.");
        Checks.Clear();
        Scene preview = EditorSceneManager.NewPreviewScene();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../AnimationPreview"));
        Directory.CreateDirectory(output);
        GameObject actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath));
        GameObject cameraObject = new GameObject("Animation preview camera");
        GameObject lamp = new GameObject("Animation preview light");
        // note: Disposable preview objects stay out of the user's open scene and cannot introduce prefab overrides or unsaved scene edits.
        SceneManager.MoveGameObjectToScene(actor, preview);
        SceneManager.MoveGameObjectToScene(cameraObject, preview);
        SceneManager.MoveGameObjectToScene(lamp, preview);
        Material previewSkin = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = new Color(0.62f, 0.38f, 0.28f) };
        Color previousAmbient = RenderSettings.ambientLight;
        try
        {
            Animator animator = actor.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            Require(animator.isHuman, "approved avatar retargets authored clips");
            foreach (Renderer skin in actor.GetComponentsInChildren<Renderer>(true))
            {
                var materials = skin.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = previewSkin;
                skin.sharedMaterials = materials;
            }
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.01f;
            camera.scene = preview;
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lamp.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
            foreach (string state in new[] { "JumpRise", "Fall", "Land", "Dash", "DodgeRoll", "Climb", "Death" })
            {
                Require(animator.HasState(0, Animator.StringToHash("Base Layer." + state)), "authored base state: " + state);
                animator.SetBool("Dead", state == "Death");
                animator.SetBool("Dashing", state == "Dash" || state == "DodgeRoll");
                animator.SetBool("DodgeRolling", state == "DodgeRoll");
                animator.SetBool("Climbing", state == "Climb");
                animator.SetBool("Grounded", state == "Land" || state == "Death" || state == "Dash" || state == "DodgeRoll");
                animator.Play("Base Layer." + state, 0, 0.4f);
                animator.Update(0f);
                camera.transform.position = new Vector3(2.4f, 1.35f, 3.4f);
                camera.transform.LookAt(new Vector3(0f, 1f, 0f));
                Render(camera, Path.Combine(output, "body-" + state + ".png"), actor);
            }
            animator.SetBool("Dead", false);
            animator.SetBool("Dashing", false);
            animator.SetBool("Climbing", false);
            animator.SetBool("Grounded", true);
            animator.Play("Base Layer.Locomotion", 0, 0f);
            animator.Update(0f);
            var viewObject = new GameObject("First person preview");
            viewObject.transform.SetParent(camera.transform, false);
            var view = viewObject.AddComponent<YQFirstPersonArmsView>();
            Require(view.Initialize(animator, camera, AssetDatabase.LoadAssetAtPath<YQFirstPersonArmsAssets>(ArmsPath)), "first-person arm geometry uses the existing skeleton and skinning");
            Require(view.GetComponentsInChildren<Animator>().Length == 0, "first-person view introduces no second Animator");
            Require(view.GetComponentsInChildren<Collider>().Length == 0, "first-person view introduces no colliders");
            Require(AssetDatabase.LoadAssetAtPath<YQFirstPersonArmsAssets>(ArmsPath).hasReadyPose, "first-person framing uses a stable ready-pose reference across camera toggles");
            actor.transform.position = new Vector3(0f, -10f, 0f);
            camera.fieldOfView = 84f;
            animator.SetLayerWeight(1, 1f);
            Vector3 previousHand = Vector3.zero;
            bool actionMoved = false;
            foreach (string state in new[] { "Empty", "MeleeLeft", "MeleeRight", "PunchLeft", "PunchRight", "Interact", "Pickup", "Consume", "Equip", "Unequip", "Cast", "CastHold", "CastRelease", "Guard" })
            {
                animator.SetLayerWeight(1, state == "Empty" ? 0f : 1f);
                animator.SetBool("Casting", state == "CastHold");
                animator.SetLayerWeight(2, state.StartsWith("Cast", StringComparison.Ordinal) || state.StartsWith("Punch", StringComparison.Ordinal) || state == "Empty" || state == "Interact" || state == "Pickup" || state == "Consume" ? 0f : 1f);
                animator.Play("Actions." + state, 1, 0.4f);
                animator.Update(0.001f);
                Vector3 hand = animator.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                actionMoved |= state != "Empty" && Vector3.Distance(hand, previousHand) > 0.03f;
                previousHand = hand;
                Debug.Log("[YourQuest Preview] " + state + " / " + animator.GetCurrentAnimatorStateInfo(1).shortNameHash + " / weight " + animator.GetLayerWeight(1) + " / hand " + hand.ToString("F3"));
                view.SynchronizePose();
                camera.transform.position = new Vector3(0f, 1.6f, 0f);
                camera.transform.rotation = Quaternion.identity;
                Render(camera, Path.Combine(output, "first-person-" + state + ".png"), viewObject);
            }
            Require(actionMoved, "sampled first-person actions move the source hand bones");
            // note: Review the full strike and roll, not a single attractive frame. This checks visible reach and recovery without creating a second action owner.
            foreach (string state in new[] { "MeleeLeft", "MeleeRight", "PunchLeft", "PunchRight", "Interact", "Pickup", "Consume", "DodgeRoll" })
            {
                bool roll = state == "DodgeRoll";
                animator.SetBool("Casting", false);
                animator.SetBool("Grounded", true);
                animator.SetBool("Dashing", roll);
                animator.SetBool("DodgeRolling", roll);
                animator.SetLayerWeight(1, roll ? 0f : 1f);
                animator.SetLayerWeight(2, state.StartsWith("Melee", StringComparison.Ordinal) ? 1f : 0f);
                Vector3 firstRight = Vector3.zero;
                Vector3 firstLeft = Vector3.zero;
                float reachArc = 0f;
                float minimumHandY = float.PositiveInfinity;
                float finalHandY = 0f;
                for (int phase = 0; phase < 9; phase++)
                {
                    animator.Play(roll ? "Base Layer.DodgeRoll" : "Base Layer.Locomotion", 0, roll ? phase / 8f : 0f);
                    animator.Play(roll ? "Actions.Empty" : "Actions." + state, 1, phase / 8f);
                    animator.Update(0f);
                    view.SynchronizePose();
                    Vector3 right = camera.transform.InverseTransformPoint(view.RightHand.position);
                    Vector3 left = camera.transform.InverseTransformPoint(view.LeftHand.position);
                    if (phase == 0) { firstRight = right; firstLeft = left; }
                    reachArc = Mathf.Max(reachArc, Vector3.Distance(right, firstRight), Vector3.Distance(left, firstLeft));
                    minimumHandY = Mathf.Min(minimumHandY, right.y);
                    finalHandY = right.y;
                    if (phase % 2 == 0) Render(camera, Path.Combine(output, "first-person-" + state + "-phase-" + phase + ".png"), viewObject);
                }
                Require(reachArc > .08f, state + " has a camera-space hand arc above 8 cm (" + reachArc.ToString("F3") + " m)");
                if (roll) Require(minimumHandY < -.65f && finalHandY > -.4f, "first-person roll tucks hands through inversion and restores them on exit");
            }
            animator.SetBool("Dashing", false);
            animator.SetBool("DodgeRolling", false);
            animator.SetLayerWeight(1, 1f);
            animator.Play("Base Layer.Locomotion", 0, 0f);

            animator.SetBool("Casting", true);
            animator.SetTrigger("Cast");
            Evaluate(animator, 1f);
            Require(animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.CastHold"), "casting pose holds until the observed combat timer ends");
            animator.SetBool("Casting", false);
            Evaluate(animator, 0.7f);
            Require(animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Empty"), "cast release recovers without a stuck charging pose");
            animator.SetBool("Casting", true);
            animator.SetTrigger("Cast");
            Evaluate(animator, 1f);
            animator.SetBool("Casting", false);
            animator.SetTrigger("CancelCast");
            Evaluate(animator, 0.2f);
            Require(animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Empty"), "interrupted casting returns without a release gesture");
            animator.SetBool("Dead", true);
            animator.Play("Base Layer.Death", 0, 0.8f);
            animator.Update(0.001f);
            view.SynchronizePose();
            Require(camera.transform.InverseTransformPoint(view.RightHand.position).y < -0.7f, "terminal death lowers first-person hands out of view");
            animator.SetBool("Dead", false);
            animator.Play("Base Layer.Locomotion", 0, 0f);
            animator.Update(0.001f);
            view.SynchronizePose();
            Require(camera.transform.InverseTransformPoint(view.RightHand.position).y > -0.4f, "revival restores first-person ready hands");
            // note: Review traversal in first person as well as third person; the camera remains stable while the same body's authored pose drives its hands.
            foreach (string state in new[] { "JumpRise", "Fall", "Land", "Dash", "DodgeRoll", "Crouch", "Climb" })
            {
                animator.SetLayerWeight(1, 0f);
                animator.SetBool("Dashing", state == "Dash" || state == "DodgeRoll");
                animator.SetBool("DodgeRolling", state == "DodgeRoll");
                animator.SetBool("Climbing", state == "Climb");
                animator.SetBool("Crouching", state == "Crouch");
                animator.SetBool("Grounded", state == "Land" || state == "Dash" || state == "DodgeRoll" || state == "Crouch");
                animator.Play("Base Layer." + state, 0, .4f);
                animator.Update(0f);
                view.SynchronizePose();
                Render(camera, Path.Combine(output, "first-person-" + state + ".png"), viewObject);
            }
            animator.SetBool("Dashing", false);
            animator.SetBool("DodgeRolling", false);
            animator.SetBool("Climbing", false);
            animator.SetBool("Crouching", false);
            animator.SetBool("Grounded", true);
            animator.SetLayerWeight(1, 1f);
            animator.Play("Base Layer.Locomotion", 0, 0f);
            // note: Render an isolated motion sequence for visual review; this never starts gameplay or changes profile state.
            string frames = Path.Combine(output, "first-person-motion");
            Directory.CreateDirectory(frames);
            animator.SetBool("Casting", false);
            animator.Play("Actions.Empty", 1, 0f);
            animator.Update(0f);
            for (int frame = 0; frame < 120; frame++)
            {
                if (frame == 12) animator.SetTrigger("MeleeLeft");
                if (frame == 42) { animator.SetBool("Casting", true); animator.SetTrigger("Cast"); }
                if (frame == 87) animator.SetBool("Casting", false);
                animator.Update(1f / 30f);
                AnimatorStateInfo action = animator.GetCurrentAnimatorStateInfo(1);
                bool spell = action.IsName("Actions.Cast") || action.IsName("Actions.CastHold") || action.IsName("Actions.CastRelease");
                animator.SetLayerWeight(2, spell ? 0f : 1f);
                view.SynchronizePose();
                Render(camera, Path.Combine(frames, frame.ToString("D3") + ".png"), viewObject);
            }
            UnityEngine.Object.DestroyImmediate(viewObject);
            Require(actor.transform.position == new Vector3(0f, -10f, 0f), "animation leaves root displacement with the motor");
            File.WriteAllText(Path.Combine(output, "receipt.json"), JsonUtility.ToJson(new Receipt { status = "PASS", utc = DateTime.UtcNow.ToString("O"), checks = Checks.ToArray(), mode = "isolated Editor asset evaluation and rendered previews; no production gameplay or saves" }, true));
        }
        finally
        {
            RenderSettings.ambientLight = previousAmbient;
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(lamp);
            UnityEngine.Object.DestroyImmediate(previewSkin);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    internal static void Render(Camera camera, string path, GameObject root)
    {
        // note: Multiple poses are sampled in one editor frame. CPU baking avoids reuse of the previous GPU skinning pose.
        var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
        var bakedObjects = new List<GameObject>();
        var bakedMeshes = new List<Mesh>();
        var texture = new RenderTexture(640, 640, 24);
        var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            foreach (var skin in skins)
            {
                if (!skin.enabled) continue;
                var mesh = new Mesh();
                skin.BakeMesh(mesh);
                var baked = new GameObject("Sampled preview mesh");
                baked.transform.SetParent(skin.transform, false);
                baked.AddComponent<MeshFilter>().sharedMesh = mesh;
                baked.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                bakedObjects.Add(baked);
                bakedMeshes.Add(mesh);
                skin.enabled = false;
            }
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            foreach (var baked in bakedObjects) UnityEngine.Object.DestroyImmediate(baked);
            foreach (var mesh in bakedMeshes) UnityEngine.Object.DestroyImmediate(mesh);
            foreach (var skin in skins) skin.enabled = true;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
    private static void Require(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException(check);
        Checks.Add(check);
    }
    private static void Evaluate(Animator animator, float seconds)
    {
        for (float time = 0f; time < seconds; time += 0.01f) animator.Update(0.01f);
    }
    [Serializable] private sealed class Receipt { public string status; public string utc; public string mode; public string[] checks; }
}
