using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// note: Disposable pose and transition checks are read-only on controller assets, gameplay and saves. Rendered poses still require human review.
public static class YQPlayerActionAnimationVerification
{
    [MenuItem("YourQuest/Player Animation/Verify Every Action and Render Poses")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Verify action poses in Edit Mode.");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Player_Animation_Action_Previews_2026-10-02"));
        Directory.CreateDirectory(output);
        GameObject actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Models/Bodies/Human Male (v4.1).FBX"));
        Animator animator = actor.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Assets/Resources/Player/YQPlayer.controller");
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Bounds bounds = actor.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
        actor.transform.localScale *= 1.86f / bounds.size.y;
        var cameraObject = new GameObject("Authored motion camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(2.6f, 1.4f, 3.4f);
        camera.transform.LookAt(new Vector3(0f, .95f, 0f));
        camera.fieldOfView = 35f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.06f, .08f, .12f);
        var material = new Material(Shader.Find("Standard"));
        material.color = new Color(.62f, .4f, .3f);
        foreach (var skin in actor.GetComponentsInChildren<Renderer>())
        {
            var materials = skin.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            skin.sharedMaterials = materials;
        }
        var lightObject = new GameObject("Authored motion light");
        lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
        lightObject.AddComponent<Light>().type = LightType.Directional;
        var evidence = new List<string>();
        try
        {
            foreach (var gait in new[] { ("Idle", 0f), ("Walk", .6f), ("Run", 1f), ("Backward", -.6f), ("Crouch", .3f) })
            {
                Reset(animator);
                animator.SetFloat("MoveZ", gait.Item2);
                animator.SetBool("Crouching", gait.Item1 == "Crouch");
                string state = gait.Item1 == "Crouch" ? "Crouch" : "Locomotion";
                for (int frame = 0; frame < 4; frame++)
                {
                    animator.Play("Base Layer." + state, 0, .125f + .25f * frame);
                    animator.Update(0f);
                    Render(camera, actor, Path.Combine(output, gait.Item1 + "-" + frame + ".png"));
                }
                foreach (var clip in animator.GetCurrentAnimatorClipInfo(0))
                {
                    if (clip.clip.name.StartsWith("__preview__")) throw new InvalidOperationException("Hidden preview clip still bound");
                    evidence.Add(gait.Item1 + ": " + clip.clip.name + " length=" + clip.clip.length);
                }
            }
            // note: Compare the rig's own pack before choosing the final gait. The temporary override never changes the production controller asset.
            var sourceController = animator.runtimeAnimatorController;
            foreach (string nativeName in new[] { "Idle", "Walk", "Run" })
            {
                AnimationClip native = null;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Assets/Resources/Player/YQPlayer.controller"))
                    if (asset is AnimationClip candidate && candidate.name == nativeName + " (body locomotion)") native = candidate;
                if (native == null) throw new InvalidOperationException("Missing native gait comparison: " + nativeName);
                var temporary = new AnimatorOverrideController(sourceController);
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                temporary.GetOverrides(overrides);
                for (int i = 0; i < overrides.Count; i++) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, native);
                temporary.ApplyOverrides(overrides);
                animator.runtimeAnimatorController = temporary;
                Reset(animator);
                float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
                for (int frame = 0; frame < 32; frame++)
                {
                    animator.Play("Base Layer.Locomotion", 0, frame / 32f);
                    animator.Update(0f);
                    float z = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.z;
                    minZ = Mathf.Min(minZ, z);
                    maxZ = Mathf.Max(maxZ, z);
                    if (frame % 8 == 0) Render(camera, actor, Path.Combine(output, "Native" + nativeName + "-" + frame / 8 + ".png"));
                }
                evidence.Add("Native" + nativeName + ": length=" + native.length + "; foot span=" + (maxZ - minZ));
                animator.runtimeAnimatorController = sourceController;
                UnityEngine.Object.DestroyImmediate(temporary);
            }
            foreach (string name in new[] { "MeleeLeft", "MeleeRight", "PunchLeft", "PunchRight", "Interact", "Pickup", "Consume", "Equip", "Unequip", "Hit", "Guard", "Channel", "Emote" })
            {
                Reset(animator);
                animator.SetLayerWeight(1, 1f);
                animator.Play("Actions." + name, 1, 0f);
                animator.Update(0f);
                AnimationClip clip = animator.GetCurrentAnimatorClipInfo(1)[0].clip;
                if (clip.name.StartsWith("__preview__") || clip.events.Length > 0) throw new InvalidOperationException("Unsafe action source: " + name);
                Vector3 initial = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                float handArc = 0f;
                for (int frame = 0; frame < 4; frame++)
                {
                    animator.Play("Actions." + name, 1, .1f + .25f * frame);
                    animator.Update(0f);
                    handArc = Mathf.Max(handArc, Vector3.Distance(initial, animator.GetBoneTransform(HumanBodyBones.RightHand).position));
                    Render(camera, actor, Path.Combine(output, name + "-" + frame + ".png"));
                }
                Reset(animator);
                animator.SetLayerWeight(1, 1f);
                animator.SetTrigger(name);
                bool entered = false;
                for (int tick = 0; tick < 360; tick++)
                {
                    animator.Update(1f / 60f);
                    entered |= animator.GetCurrentAnimatorStateInfo(1).IsName("Actions." + name);
                }
                if (!entered || !animator.GetCurrentAnimatorStateInfo(1).IsName("Actions.Empty")) throw new InvalidOperationException("Action entry/recovery failed: " + name);
                evidence.Add(name + ": " + clip.name + "; length=" + clip.length + "; sampled right-hand arc=" + handArc + "; trigger/recovery=PASS");
            }
            Reset(animator);
            animator.SetBool("Dashing", true);
            animator.SetBool("DodgeRolling", true);
            animator.SetTrigger("DodgeRoll");
            for (int tick = 0; tick < 18; tick++) animator.Update(1f / 60f);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.DodgeRoll")) throw new InvalidOperationException("Crouch-dash roll transition failed: current=" + animator.GetCurrentAnimatorStateInfo(0).shortNameHash + " next=" + animator.GetNextAnimatorStateInfo(0).shortNameHash);
            for (int frame = 0; frame < 4; frame++)
            {
                animator.Play("Base Layer.DodgeRoll", 0, .1f + .25f * frame);
                animator.Update(0f);
                Render(camera, actor, Path.Combine(output, "DodgeRoll-" + frame + ".png"));
            }
            animator.SetBool("Dashing", false);
            animator.SetBool("DodgeRolling", false);
            for (int tick = 0; tick < 30; tick++) animator.Update(1f / 60f);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Locomotion")) throw new InvalidOperationException("Roll recovery failed");
            evidence.Add("DodgeRoll: authored full-body phases and physical-timer recovery=PASS");
            evidence.Add("Editor-only action coverage PASS. Production motor, gear and camera still require live verification.");
            File.WriteAllLines(Path.Combine(output, "evidence.txt"), evidence);
            Debug.Log("[Action coverage probe] PASS " + output);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(lightObject);
            UnityEngine.Object.DestroyImmediate(material);
        }
    }
    private static void Reset(Animator animator)
    {
        animator.Rebind();
        animator.Update(0f);
        animator.SetBool("Grounded", true);
        animator.SetLayerWeight(1, 0f);
        animator.SetLayerWeight(2, 0f);
        animator.SetFloat("LocomotionRate", 1f);
        animator.SetFloat("CrouchRate", 1f);
        animator.Play("Base Layer.Locomotion", 0, 0f);
        animator.Play("Actions.Empty", 1, 0f);
        animator.Update(0f);
    }
    private static void Render(Camera camera, GameObject actor, string path)
    {
        YQAuthoredPlayerAnimations.Render(camera, path, actor);
    }
}
