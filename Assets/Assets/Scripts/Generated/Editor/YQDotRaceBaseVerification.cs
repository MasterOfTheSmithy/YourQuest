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

// note: New race fixtures evaluate supplied rigs, including Bramblekin's per-LOD morph curves, without touching the production scene or saves.
public static class YQDotRaceBaseVerification
{
    internal static string[] CorrectBramblekinTextureAliases(YQDotCreatureEntry body, string output)
    {
        string path = AssetDatabase.GetAssetPath(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(body.prefabPath));
        string folder = YQDotAssetLayout.Root + "/NPCs/Bramblekin/Body Types/Ordinary/Materials/I16/Unity Texture Alias Corrections";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var created = new List<string>(); var records = new List<object>(); var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var skins = prefab.transform.Find("LOD0").GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var reference = skins.SelectMany(s => s.sharedMaterials).Single(m => m.name == "Bramblekin_I02_Body_PBR");
            foreach (var skin in skins)
            {
                var materials = skin.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original.name != "Bramblekin_I02_Face_PBR" && original.name != "Bramblekin_I02_WetEye_PBR") continue;
                    // note: glTFast's reused texture indices lost the non-flipped orientation flag. The delivered head/body use the same PNG, sampler and untransformed UV0; clone only affected material bindings and copy the correctly imported shared-atlas orientation.
                    var corrected = new Material(original);
                    foreach (string property in new[] { "baseColorTexture", "normalTexture", "metallicRoughnessTexture" })
                    {
                        if (original.GetTexture(property) != reference.GetTexture(property)) throw new InvalidDataException("Expected the exact shared original atlas.");
                        corrected.SetTextureOffset(property, reference.GetTextureOffset(property)); corrected.SetTextureScale(property, reference.GetTextureScale(property));
                        corrected.SetVector(property + "_ST", reference.GetVector(property + "_ST")); corrected.SetVector(property + "_Rotation", reference.GetVector(property + "_Rotation")); corrected.SetFloat(property + "_texCoord", reference.GetFloat(property + "_texCoord"));
                    }
                    corrected.DisableKeyword("_TEXTURE_TRANSFORM"); corrected.name = original.name;
                    string materialPath = folder + "/" + original.name + ".mat";
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (existing == null) AssetDatabase.CreateAsset(corrected, materialPath);
                    else { EditorUtility.CopySerialized(corrected, existing); UnityEngine.Object.DestroyImmediate(corrected); corrected = existing; EditorUtility.SetDirty(existing); }
                    materials[i] = corrected; created.Add(materialPath);
                    records.Add(new { source = body.sourcePaths[0], originalMaterial = original.name, correctedMaterial = materialPath, correction = "Shared PNG/sampler UV0 orientation parity; no source texture, factors, shader or geometry replaced" });
                }
                skin.sharedMaterials = materials;
            }
            if (created.Count != 2) throw new InvalidDataException("Expected exactly two texture-alias corrections.");
            PrefabUtility.SaveAsPrefabAsset(prefab, path); AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        string receipt = folder + "/MATERIAL_ALIAS_CORRECTIONS.json";
        File.WriteAllText(receipt, JsonConvert.SerializeObject(new { status = "PASS", records }, Formatting.Indented)); AssetDatabase.ImportAsset(receipt, ImportAssetOptions.ForceSynchronousImport); created.Add(receipt);
        File.WriteAllText(output + "/texture-alias-correction.json", File.ReadAllText(receipt)); return created.ToArray();
    }
    internal static void InspectMaterial(YQDotCreatureEntry body, string output)
    {
        var scene = EditorSceneManager.NewPreviewScene(); var actor = UnityEngine.Object.Instantiate(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(body.prefabPath)); var mesh = new Mesh();
        SceneManager.MoveGameObjectToScene(actor, scene);
        try
        {
            var model = actor.transform.Find("LOD0"); var animator = model.GetComponentInChildren<Animator>(true); animator.enabled = false;
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            ((AnimationClip)controller.layers[0].stateMachine.states.Single(s => s.state.name == "DotIdle").state.motion).SampleAnimation(animator.gameObject, 1.6f);
            var details = new List<object>();
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.BakeMesh(mesh); var original = skin.sharedMesh;
                details.Add(new { skin.name, mesh = original.name, vertices = original.vertexCount, originalUv = original.uv.Length, bakedUv = mesh.uv.Length, uvExact = original.uv.SequenceEqual(mesh.uv), originalColors = original.colors.Length, bakedColors = mesh.colors.Length, colorsExact = original.colors.SequenceEqual(mesh.colors), materials = skin.sharedMaterials.Select(m => new { m.name, shader = m.shader.name, keywords = m.shaderKeywords, properties = Enumerable.Range(0, ShaderUtil.GetPropertyCount(m.shader)).Select(i => { string property = ShaderUtil.GetPropertyName(m.shader, i); var type = ShaderUtil.GetPropertyType(m.shader, i); return new { property, type = type.ToString(), value = type == ShaderUtil.ShaderPropertyType.TexEnv ? m.GetTexture(property)?.name : type == ShaderUtil.ShaderPropertyType.Color ? m.GetColor(property).ToString() : type == ShaderUtil.ShaderPropertyType.Float || type == ShaderUtil.ShaderPropertyType.Range ? m.GetFloat(property).ToString() : "" }; }) }) });
            }
            File.WriteAllText(output + "/bramblekin-material-diagnostic.json", JsonConvert.SerializeObject(details, Formatting.Indented));
        }
        finally { UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(mesh); EditorSceneManager.ClosePreviewScene(scene); }
    }
    internal static AnimationClip NormalizeClip(AnimationClip source, string controller)
    {
        var bindings = AnimationUtility.GetCurveBindings(source);
        float first = bindings.SelectMany(b => AnimationUtility.GetEditorCurve(source, b).keys).Min(k => k.time);
        var clip = UnityEngine.Object.Instantiate(source); clip.name = source.name;
        foreach (var binding in bindings)
        {
            var curve = AnimationUtility.GetEditorCurve(source, binding);
            curve.keys = curve.keys.Select(k => { k.time -= first; return k; }).ToArray();
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
            for (int i = 0; i < keys.Length; i++) keys[i].time -= first;
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        }
        var events = AnimationUtility.GetAnimationEvents(source); foreach (var e in events) e.time -= first; AnimationUtility.SetAnimationEvents(clip, events);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.startTime = 0f; settings.stopTime = clip.length; AnimationUtility.SetAnimationClipSettings(clip, settings);
        string path = controller.Replace(".controller", "_Own_" + source.name + ".anim");
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
        // note: A later approved rebuild keeps the existing animation GUID rather than replacing its serialized asset identity.
        EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(clip); return existing;
    }

    internal static void ExpandBounds(GameObject model, AnimationClip[] clips)
    {
        var animator = model.GetComponentInChildren<Animator>(true);
        var transforms = model.GetComponentsInChildren<Transform>(true);
        var rest = transforms.Select(t => (t.localPosition, t.localRotation, t.localScale)).ToArray();
        var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        // note: glTF imports opt into per-frame offscreen bounds recalculation. Disable that on owned shells so Unity retains the measured all-clip envelope instead of replacing it with a single-frame bound.
        foreach (var skin in skins) skin.updateWhenOffscreen = false;
        var weights = skins.Select(s => Enumerable.Range(0, s.sharedMesh.blendShapeCount).Select(s.GetBlendShapeWeight).ToArray()).ToArray();
        var bounds = skins.Select(s => s.localBounds).ToArray(); var mesh = new Mesh();
        try
        {
            animator.enabled = false;
            foreach (var clip in clips) for (int frame = 0; frame <= Mathf.CeilToInt(clip.length * 30f); frame++)
            {
                clip.SampleAnimation(animator.gameObject, Mathf.Min(frame / 30f, clip.length));
                for (int i = 0; i < skins.Length; i++) { skins[i].BakeMesh(mesh); bounds[i].Encapsulate(mesh.bounds); }
            }
            for (int i = 0; i < skins.Length; i++) { bounds[i].Expand(.02f); skins[i].localBounds = bounds[i]; }
        }
        finally
        {
            // note: Bounds sampling must not publish a greeting pose or facial weights as the prefab's new rest state.
            for (int i = 0; i < transforms.Length; i++) { transforms[i].localPosition = rest[i].localPosition; transforms[i].localRotation = rest[i].localRotation; transforms[i].localScale = rest[i].localScale; }
            for (int i = 0; i < skins.Length; i++) for (int j = 0; j < weights[i].Length; j++) skins[i].SetBlendShapeWeight(j, weights[i][j]);
            animator.enabled = true; UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    internal static void Run(YQDotCreatureEntry[] bodies, string output)
    {
        var checks = new List<string>(); int frames = 0, morphCurves = 0;
        var scene = EditorSceneManager.NewPreviewScene(); GameObject actor = null; var mesh = new Mesh();
        try
        {
            foreach (var entry in bodies)
            {
                if (entry.motionProfile != null && entry.motionProfile.IsValid)
                {
                    var p = entry.motionProfile;
                    Require(Mathf.Abs(p.loopDistance.Last() / p.loopDuration - entry.authoredWalkSpeed) < .00001f, "Exact source loop speed", checks);
                    Require(Mathf.Abs(p.localForward.y) < .00001f && Mathf.Abs(p.localForward.magnitude - 1f) < .00001f, "Measured imported horizontal facing", checks);
                    foreach (int fps in new[] { 30, 60, 120 })
                    {
                        var sampler = new YQDotGaitSampler(p); float travel = 0f;
                        for (int i = 0; i < Mathf.RoundToInt((p.startDuration + 2 * p.loopDuration) * fps); i++) travel += sampler.Advance(true, 1f / fps);
                        for (int i = 0; i < Mathf.RoundToInt(p.stopDuration * fps) + 2; i++) travel += sampler.Advance(false, 1f / fps);
                        Require(!sampler.IsActive && Mathf.Abs(travel - (p.startDistance.Last() + 2 * p.loopDistance.Last() + p.stopDistance.Last())) < .0001f, entry.species + ": sampled travel at " + fps + "fps", checks);
                    }
                }
                actor = UnityEngine.Object.Instantiate(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(entry.prefabPath)); SceneManager.MoveGameObjectToScene(actor, scene);
                Require(actor.GetComponent<LODGroup>().GetLODs().Length == 4, "Four supplied LODs", checks);
                foreach (var a in actor.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                for (int lod = 0; lod < 4; lod++)
                {
                    var model = actor.transform.Find("LOD" + lod); var animator = model.GetComponentInChildren<Animator>(true); var controller = (AnimatorController)animator.runtimeAnimatorController;
                    var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    int expected = entry.species == "dwarf" ? 166 : entry.species == "kitsune" ? 132 : 59;
                    Require(!animator.applyRootMotion && skins.All(s => s.bones.Length == expected && s.bones.All(b => b != null) && s.sharedMesh.bindposes.Length == expected), entry.species + ": own complete bind skeleton", checks);
                    foreach (string state in new[] { "DotIdle", "DotStart", "DotWalk", "DotStop", "DotGreet" })
                    {
                        var clip = (AnimationClip)controller.layers[0].stateMachine.states.Single(s => s.state.name == state).state.motion;
                        Require(clip.length > 0f, "Own " + state, checks);
                        if (entry.motionProfile != null && entry.motionProfile.IsValid && (state == "DotStart" || state == "DotWalk" || state == "DotStop"))
                        {
                            var p = entry.motionProfile; float duration = state == "DotStart" ? p.startDuration : state == "DotWalk" ? p.loopDuration : p.stopDuration;
                            Require(Mathf.Abs(clip.length - duration) < .00001f, entry.species + ": zero-based clip matches source travel duration", checks);
                        }
                        var bindings = AnimationUtility.GetCurveBindings(clip);
                        foreach (var binding in bindings)
                        {
                            var target = string.IsNullOrEmpty(binding.path) ? animator.transform : animator.transform.Find(binding.path);
                            Require(target != null, "Bound own animation path " + binding.path, checks);
                            if (binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                            { Require(target.GetComponent<SkinnedMeshRenderer>().sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring(11)) >= 0, "Named supplied morph", checks); morphCurves++; }
                        }
                        for (int i = 0; i <= Mathf.CeilToInt(clip.length * 30f); i++)
                        {
                            clip.SampleAnimation(animator.gameObject, Mathf.Min(i / 30f, clip.length));
                            foreach (var skin in skins)
                            {
                                skin.BakeMesh(mesh); var b = mesh.bounds;
                                Require(!skin.updateWhenOffscreen && float.IsFinite(b.size.sqrMagnitude) && b.size.sqrMagnitude > .000001f && skin.localBounds.Contains(b.min) && skin.localBounds.Contains(b.max), entry.species + " LOD" + lod + " " + state + " frame=" + i + " skin=" + skin.name + ": finite evaluated skin inside sampled bounds; baked=" + b + "; saved=" + skin.localBounds + "; offscreen=" + skin.updateWhenOffscreen, checks); frames++;
                            }
                        }
                    }
                }
                UnityEngine.Object.DestroyImmediate(actor); actor = null;
            }
            Require(morphCurves > 0, "Bramblekin authored cloth/facial morph animation retained", checks);
            File.WriteAllText(output + "/motion-verification.json", JsonConvert.SerializeObject(new { status = "PASS", utc = DateTime.UtcNow, evaluatedSkinFrames = frames, morphCurves, checks, evidence = "Disposable Editor skin/curve/bounds fixtures and sampled Dwarf/Kitsune travel. Bramblekin cadence calibration, live terrain contact, moving LOD transitions and shader extension parity remain unverified." }, Formatting.Indented));
        }
        finally { if (actor != null) UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(mesh); EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static void Require(bool condition, string label, List<string> checks) { if (!condition) throw new InvalidDataException(label); checks.Add(label); }
}
