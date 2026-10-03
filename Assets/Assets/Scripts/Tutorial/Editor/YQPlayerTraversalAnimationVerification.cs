using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Disposable body/arms samples and state-flow checks cannot certify ordinary gameplay or a mounted creature attachment.
public static class YQPlayerTraversalAnimationVerification
{
    [MenuItem("YourQuest/Player Animation/Verify Traversal Phases and Rider Motions")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Preview traversal in Edit Mode.");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../outputs/Player_Animation_20261002/TraversalPreview"));
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        var samples = new List<object>();
        string status = "FAIL", error = null;
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject actor = null, cameraObject = null, lightObject = null;
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(YQAuthoredPlayerAnimations.ControllerPath);
            actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(YQAuthoredPlayerAnimations.BodyPath));
            SceneManager.MoveGameObjectToScene(actor, scene);
            Bounds bounds = actor.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
            actor.transform.localScale *= 1.86f / bounds.size.y;
            YQRuntimeUrpMaterialRepair.RepairHierarchy(actor);
            Animator animator = actor.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Reset(animator);
            Require(animator.isHuman, "production body retargets controller", checks);
            cameraObject = new GameObject("Traversal preview camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .08f, .12f); camera.fieldOfView = 35f; camera.nearClipPlane = .01f;
            lightObject = new GameObject("Traversal preview light"); SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f); lightObject.AddComponent<Light>().type = LightType.Directional;
            camera.transform.position = new Vector3(2.6f, 1.35f, 3.4f); camera.transform.LookAt(new Vector3(0f, .95f, 0f));
            var viewObject = new GameObject("Traversal first person"); viewObject.transform.SetParent(camera.transform, false);
            var view = viewObject.AddComponent<YQFirstPersonArmsView>();
            Require(view.Initialize(animator, camera, AssetDatabase.LoadAssetAtPath<YQFirstPersonArmsAssets>(YQAuthoredPlayerAnimations.ArmsPath)), "same body supplies first person geometry", checks);
            Require(view.GetComponentsInChildren<Animator>().Length == 0, "no second Animator", checks);
            viewObject.SetActive(false);
            foreach (string name in new[] { "JumpRise", "Fall", "Land", "DashStart", "Dash", "DashRecover", "DodgeRollStart", "DodgeRoll", "DodgeRollRecover", "ClimbStart", "Climb", "ClimbRecover", "Mount", "MountedRide", "MountedFlight", "Dismount" })
            {
                Reset(animator); animator.SetBool("TraversalPose", true); animator.SetBool("Mounted", name.StartsWith("Mount", StringComparison.Ordinal));
                Vector3 first = Vector3.zero; float arc = 0f;
                for (int phase = 0; phase < 5; phase++)
                {
                    animator.Play("Base Layer." + name, 0, phase / 5f); animator.Update(0f);
                    Vector3 hand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                    if (phase == 0) first = hand; else arc = Mathf.Max(arc, Vector3.Distance(first, hand));
                    Require(float.IsFinite(hand.x) && float.IsFinite(hand.y) && float.IsFinite(hand.z), name + " finite pose " + phase, checks);
                    foreach (var info in animator.GetCurrentAnimatorClipInfo(0))
                        Require(info.clip.humanMotion && info.clip.events.Length == 0 && !info.clip.name.StartsWith("__preview__", StringComparison.Ordinal), name + " safe humanoid source " + phase, checks);
                    // note: Native rider clips carry saddle-relative body elevation. Frame the evaluated hips instead of cropping the rider out of the preview.
                    Vector3 centre = animator.GetBoneTransform(HumanBodyBones.Hips).position + Vector3.up * .15f;
                    camera.fieldOfView = 35f; camera.transform.position = centre + new Vector3(2.6f, .4f, 3.4f); camera.transform.LookAt(centre);
                    YQAuthoredPlayerAnimations.Render(camera, Path.Combine(output, name + "-body-" + phase + ".png"), actor);
                    // note: Hide only the disposable body renderers while the mirrored geometry is rendered at gameplay FOV.
                    foreach (var renderer in actor.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    viewObject.SetActive(true); camera.fieldOfView = 84f; camera.transform.position = new Vector3(0f, 1.6f, 0f); camera.transform.rotation = Quaternion.identity;
                    view.SynchronizePose();
                    YQAuthoredPlayerAnimations.Render(camera, Path.Combine(output, name + "-first-person-" + phase + ".png"), viewObject);
                    viewObject.SetActive(false); foreach (var renderer in actor.GetComponentsInChildren<Renderer>()) renderer.enabled = true;
                }
                samples.Add(new { state = name, rightHandArcMetres = arc });
            }
            Reset(animator); animator.SetBool("Dashing", true); animator.SetBool("DodgeRolling", true); animator.SetTrigger("DodgeRoll");
            var visited = new HashSet<string>();
            for (int tick = 0; tick < 90; tick++)
            {
                animator.SetBool("Dashing", tick < 36); animator.SetBool("DodgeRolling", tick < 36);
                animator.SetBool("TraversalPose", true); animator.Update(1f / 60f);
                foreach (string name in new[] { "DodgeRollStart", "DodgeRoll", "DodgeRollRecover", "Locomotion" })
                    if (animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + name)) visited.Add(name);
            }
            foreach (string name in new[] { "DodgeRollStart", "DodgeRoll", "DodgeRollRecover", "Locomotion" }) Require(visited.Contains(name), "accepted roll visits " + name, checks);
            Reset(animator); animator.SetBool("Dashing", true); animator.SetTrigger("Dash"); visited.Clear();
            for (int tick = 0; tick < 90; tick++)
            {
                animator.SetBool("Dashing", tick < 11); animator.SetBool("TraversalPose", true); animator.Update(1f / 60f);
                foreach (string name in new[] { "DashStart", "Dash", "DashRecover", "Locomotion" })
                    if (animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + name)) visited.Add(name);
            }
            foreach (string name in new[] { "DashStart", "Dash", "DashRecover", "Locomotion" }) Require(visited.Contains(name), "accepted dash visits " + name, checks);
            status = "PASS";
        }
        catch (Exception exception) { error = exception.ToString(); Debug.LogException(exception); }
        finally
        {
            if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
            EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllText(Path.Combine(output, "receipt.json"), JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, checks, samples, error, evidence = "Disposable retargeting, render and controller-flow fixture. Visual review and ordinary motor gameplay remain separate." }, Formatting.Indented));
        }
        Debug.Log("[YQPlayerTraversal] Preview " + status + ": " + output);
    }
    private static void Reset(Animator animator)
    {
        animator.Rebind(); animator.Update(0f); animator.SetBool("Grounded", true); animator.SetLayerWeight(1, 0f); animator.SetLayerWeight(2, 0f);
        animator.Play("Base Layer.Locomotion", 0, 0f); animator.Play("Actions.Empty", 1, 0f); animator.Update(0f);
    }
    private static void Require(bool condition, string message, List<string> checks)
    { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
}
