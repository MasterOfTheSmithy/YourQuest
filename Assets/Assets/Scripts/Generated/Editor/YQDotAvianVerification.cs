using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Disposable Avian fixtures validate the approved profile, own rigs and LOD references through existing owners; no live scene or save is changed.
public static class YQDotAvianVerification
{
    internal static void Run(YQDotCreatureEntry[] bodies, string output)
    {
        var checks = new List<string>(); int frames = 0;
        var scene = EditorSceneManager.NewPreviewScene(); GameObject actor = null; var baked = new Mesh();
        try
        {
            foreach (var entry in bodies)
            {
                var profile = entry.motionProfile;
                Require(profile.IsValid && Mathf.Abs(entry.authoredWalkSpeed - .32f) < .000001f && Mathf.Abs(profile.loopDistance.Last() - .48f) < .000001f, entry.assetId + ": exact sampled profile and loop speed", checks);
                Require(Mathf.Abs(profile.localForward.y) < .00001f && Vector3.Dot(profile.localForward, Vector3.forward) > .999f, entry.assetId + ": imported jaw facing verifies positive horizontal forward", checks);
                foreach (int fps in new[] { 30, 60, 120 })
                {
                    var sampler = new YQDotGaitSampler(profile); float travel = 0f, dt = 1f / fps;
                    for (int i = 0; i < Mathf.RoundToInt((profile.startDuration + 2f * profile.loopDuration) * fps); i++) travel += sampler.Advance(true, dt);
                    for (int i = 0; i < Mathf.RoundToInt(profile.stopDuration * fps) + 2; i++) travel += sampler.Advance(false, dt);
                    Require(!sampler.IsActive && Mathf.Abs(travel - 1.2f) < .0001f && sampler.Advance(false, dt) == 0f, entry.assetId + ": start/two loops/stop travels 1.20m once at " + fps + "fps", checks);
                    var route = new YQDotGaitSampler(profile); float remaining = 1.5f; bool began = false, ended = false;
                    for (int i = 0; i < fps * 30; i++) { float delta = route.Advance(remaining > route.ContinuationDistance + .02f, dt); Require(delta <= remaining + .0001f, "Finite route never clamps an unfinished gait", checks); remaining -= delta; began |= route.IsActive; if (began && !route.IsActive) { ended = true; break; } }
                    Require(ended && remaining >= -.0001f, entry.assetId + ": route admission leaves room for authored stop", checks);
                }
                actor = UnityEngine.Object.Instantiate(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(entry.prefabPath)); SceneManager.MoveGameObjectToScene(actor, scene);
                var lods = actor.GetComponent<LODGroup>().GetLODs(); Require(lods.Length == 3 && lods[1].screenRelativeTransitionHeight <= .09f, entry.assetId + ": coarse LOD2 stays distant-only", checks);
                foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                for (int lod = 0; lod < 3; lod++)
                {
                    var model = actor.transform.Find("LOD" + lod); var animator = model.GetComponentInChildren<Animator>(true); var controller = (AnimatorController)animator.runtimeAnimatorController;
                    Require(!animator.applyRootMotion && controller.layers[0].stateMachine.states.All(s => s.state.transitions.Length == 0), entry.assetId + ": existing scalar phase owner, no duplicate root travel", checks);
                    var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    Require(skins.Length == 1 && skins[0].bones.Length == 77 && skins[0].bones.All(b => b != null) && skins[0].sharedMesh.bindposes.Length == 77 && skins[0].sharedMaterials.Length == 8, entry.assetId + " LOD" + lod + ": batched supplied body, eight original materials and complete own skeleton", checks);
                    foreach (string state in new[] { "DotIdle", "DotStart", "DotWalk", "DotStop", "DotGreet" })
                    {
                        var clip = controller.layers[0].stateMachine.states.Single(s => s.state.name == state).state.motion as AnimationClip;
                        Require(clip != null && clip.length > 0f, entry.assetId + " " + state + ": supplied own clip", checks);
                        foreach (var curve in AnimationUtility.GetCurveBindings(clip)) Require(string.IsNullOrEmpty(curve.path) || animator.transform.Find(curve.path) != null, entry.assetId + ": bound own curve " + curve.path, checks);
                        for (int i = 0; i <= 30; i++)
                        {
                            clip.SampleAnimation(animator.gameObject, clip.length * i / 30f); skins[0].BakeMesh(baked); var bounds = baked.bounds;
                            Require(float.IsFinite(bounds.size.x) && float.IsFinite(bounds.size.y) && float.IsFinite(bounds.size.z) && bounds.size.sqrMagnitude > .00001f && actor.transform.position == Vector3.zero, entry.assetId + " LOD" + lod + " " + state + ": finite evaluated skin, actor unchanged", checks); frames++;
                        }
                    }
                }
                UnityEngine.Object.DestroyImmediate(actor); actor = null;
            }
            File.WriteAllText(output + "/motion-verification.json", JsonConvert.SerializeObject(new { status = "PASS", utc = DateTime.UtcNow, evaluatedSkinFrames = frames, checks, evidence = "Editor-evaluated own skins/clips, finite-route and scalar travel at three frame rates, verified imported facing and distant-only LOD thresholds. Production terrain contact, physics and visible LOD transitions remain unverified." }, Formatting.Indented));
        }
        finally { if (actor != null) UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(baked); EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static void Require(bool condition, string label, List<string> checks) { if (!condition) throw new InvalidDataException(label); checks.Add(label); }
}
