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

// note: Cat intake uses only the four supplied loops and existing actor/visual owners, with original materials and texture aliases corrected on owned copies.
public static class YQDotCatVerification
{
    internal static void Configure(GameObject model, string source, string controllerPath)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
        if (clips.Length != 4 || !new[] { "Idle", "Alert", "Walk", "Trot" }.All(n => clips.Any(c => c.name == n))) throw new InvalidDataException("Expected the four approved Cat loops.");
        // note: CreateAsset can replace an animation's name with its filename; retain the source state names independently of owned asset names.
        var ownClips = clips.ToDictionary(c => c.name, c => YQDotRaceBaseVerification.NormalizeClip(c, controllerPath));
        clips = ownClips.Values.ToArray();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine; foreach (var state in machine.states) machine.RemoveState(state.state);
        controller.parameters = new[] { new AnimatorControllerParameter { name = "DotMoving", type = AnimatorControllerParameterType.Bool }, new AnimatorControllerParameter { name = "DotRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f } };
        foreach (var pair in ownClips)
        {
            var clip = pair.Value;
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            var state = machine.AddState("Dot" + pair.Key); state.motion = clip;
            if (pair.Key == "Idle") machine.defaultState = state;
            if (pair.Key == "Walk") { state.speedParameterActive = true; state.speedParameter = "DotRate"; }
        }
        var idle = machine.states.Single(s => s.state.name == "DotIdle").state; var walk = machine.states.Single(s => s.state.name == "DotWalk").state;
        // note: No fictitious start/stop animation or authored transition is added. Existing velocity presentation switches the approved idle/walk loops without root travel.
        var start = idle.AddTransition(walk); start.hasExitTime = false; start.duration = 0f; start.AddCondition(AnimatorConditionMode.If, 0f, "DotMoving");
        var stop = walk.AddTransition(idle); stop.hasExitTime = false; stop.duration = 0f; stop.AddCondition(AnimatorConditionMode.IfNot, 0f, "DotMoving");
        var animator = model.GetComponentInChildren<Animator>(true); if (animator == null) throw new InvalidDataException("Missing Cat Animator."); animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true); var reference = skins.SelectMany(s => s.sharedMaterials).Single(m => m.name == "Cat_tabby_coat_vertex_color");
        foreach (var skin in skins)
        {
            var materials = skin.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var original = materials[i]; if (!original.IsKeywordEnabled("_TEXTURE_TRANSFORM")) continue;
                if (reference.IsKeywordEnabled("_TEXTURE_TRANSFORM") || reference.GetTexture("baseColorTexture") != original.GetTexture("baseColorTexture")) throw new InvalidDataException("Unexpected Cat atlas orientation contract.");
                var corrected = new Material(original); corrected.name = original.name;
                corrected.SetTextureScale("baseColorTexture", reference.GetTextureScale("baseColorTexture")); corrected.SetTextureOffset("baseColorTexture", reference.GetTextureOffset("baseColorTexture"));
                corrected.SetVector("baseColorTexture_ST", reference.GetVector("baseColorTexture_ST")); corrected.SetVector("baseColorTexture_Rotation", reference.GetVector("baseColorTexture_Rotation")); corrected.SetFloat("baseColorTexture_texCoord", 0f); corrected.DisableKeyword("_TEXTURE_TRANSFORM");
                string folder = YQDotAssetLayout.Root + "/Wildlife/Cat/Body Types/Domestic Tabby/Materials/R8/Unity Texture Alias Corrections/" + model.name;
                Directory.CreateDirectory(folder); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); string path = folder + "/" + original.name + ".mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing == null) AssetDatabase.CreateAsset(corrected, path); else { EditorUtility.CopySerialized(corrected, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(corrected); corrected = existing; }
                materials[i] = corrected;
            }
            skin.sharedMaterials = materials;
        }
        YQDotRaceBaseVerification.ExpandBounds(model, clips); EditorUtility.SetDirty(controller);
    }

    internal static void Run(YQDotCreatureEntry[] bodies, string output)
    {
        var checks = new List<string>(); int frames = 0; var scene = EditorSceneManager.NewPreviewScene(); GameObject actor = null; var mesh = new Mesh();
        try
        {
            var entry = bodies.Single(); Require(entry.kind == "wildlife" && Mathf.Abs(entry.authoredWalkSpeed - .0911458333f) < .000001f, "Passive Cat and approved walk speed", checks);
            Require(YQDotCreatureCatalog.TryResolve(YQRuntimeWorldAssetRegistry.Instance, "cats", "wildlife", "cat-fixture", out _, out _) && !YQDotCreatureCatalog.TryResolve(YQRuntimeWorldAssetRegistry.Instance, "cat", "monster", "cat-fixture", out _, out _), "Explicit passive binding; no hostile alias", checks);
            actor = UnityEngine.Object.Instantiate(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(entry.prefabPath)); SceneManager.MoveGameObjectToScene(actor, scene);
            foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            Require(actor.GetComponent<LODGroup>().GetLODs().Length == 3, "Three supplied LODs", checks);
            for (int lod = 0; lod < 3; lod++)
            {
                var model = actor.transform.Find("LOD" + lod); var animator = model.GetComponentInChildren<Animator>(true); var controller = (AnimatorController)animator.runtimeAnimatorController; var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Require(!animator.applyRootMotion && skins.All(s => s.bones.Length == 25 && s.bones.All(b => b != null) && s.sharedMesh.bindposes.Length == 25), "Own Cat rig and in-place actor", checks);
                foreach (string state in new[] { "DotIdle", "DotAlert", "DotWalk", "DotTrot" })
                {
                    var clip = (AnimationClip)controller.layers[0].stateMachine.states.Single(s => s.state.name == state).state.motion;
                    float expected = state == "DotIdle" ? 3f : state == "DotAlert" ? 2f : state == "DotWalk" ? 1.2f : .8f;
                    Require(Mathf.Abs(clip.length - expected) < .00001f && AnimationUtility.GetAnimationClipSettings(clip).loopTime, "Exact own loop duration " + state, checks);
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip)) Require(string.IsNullOrEmpty(binding.path) || animator.transform.Find(binding.path) != null, "Bound own Cat curve", checks);
                    for (int frame = 0; frame <= Mathf.CeilToInt(clip.length * 30f); frame++)
                    {
                        clip.SampleAnimation(animator.gameObject, Mathf.Min(frame / 30f, clip.length));
                        foreach (var skin in skins) { skin.BakeMesh(mesh); Require(!skin.updateWhenOffscreen && float.IsFinite(mesh.bounds.size.sqrMagnitude) && mesh.bounds.size.sqrMagnitude > .000001f && skin.localBounds.Contains(mesh.bounds.min) && skin.localBounds.Contains(mesh.bounds.max), "Finite Cat skin within all-clip bounds", checks); frames++; }
                    }
                }
            }
            File.WriteAllText(output + "/motion-verification.json", JsonConvert.SerializeObject(new { status = "PASS", utc = DateTime.UtcNow, evaluatedSkinFrames = frames, checks, evidence = "Editor own-rig/loop/bounds fixtures. No start/stop clips invented; live terrain stance, clip transitions and LOD switches remain unverified." }, Formatting.Indented));
        }
        finally { if (actor != null) UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(mesh); EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static void Require(bool condition, string label, List<string> checks) { if (!condition) throw new InvalidDataException(label); checks.Add(label); }
}
