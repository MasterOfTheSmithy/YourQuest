using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Focused metadata, sampled-travel and freshly imported skin/clip fixtures do not certify ordinary gameplay or arbitrary blend/contact behavior.
public static class YQDotPatchVerification
{
    private const string Output = "outputs/DOT_Integration_20261003/Patches";
    internal static void Run(YQDotCreatureCatalog creatures, YQDotEquipmentCatalog equipment)
    {
        var checks = new List<string>(); int sampledFrames = 0;
        string root = YQDotEquipmentIntake.CraftingRoot;
        var correction = JObject.Parse(File.ReadAllText(YQDotAssetLayout.Resolve(root + "/Patches/1.0.1/CHANGED_FILES_PATCH_MANIFEST.json")));
        foreach (JObject mapping in (JArray)correction["corrected_visual_mappings"])
        {
            string id = (string)mapping["visual_id"];
            Require(equipment.TryGetAsset(id, out var entry) && entry.materialKey == (string)mapping["new_material_key"] &&
                (string)JObject.Parse(entry.sourceContractJson)["material_key"] == entry.materialKey, id + ": corrected ornament anatomy label", checks);
            Require(equipment.craftingMaterials.Any(m => m.materialKey == entry.materialKey && m.visualAssetIds.Contains(id)), id + ": corrected material-to-visual index", checks);
        }
        var examples = JArray.Parse(File.ReadAllText(YQDotAssetLayout.Resolve(root + "/Data/ornament_resolved_examples.v1_0_1.json")));
        Require(examples.Count == 8 && JArray.Parse(equipment.craftingOrnamentRecipeExamplesJson).Count == 8, "Eight corrected reference examples retained", checks);
        foreach (JObject example in examples)
        {
            Require(Math.Abs((double)example["elements"]["neutral"] - .1d) < .00001d, "Corrected reference palette retains ten percent neutral", checks);
            foreach (JObject visual in (JArray)example["visuals"])
                Require(equipment.TryGetAsset((string)visual["visual_id"], out var entry) && entry.materialKey == (string)visual["material_key"], (string)visual["visual_id"] + ": resolved example matches installed catalog", checks);
        }
        Require(equipment.craftingReleaseVersion == "1.0.1" && equipment.craftingSchemaVersion == "1.0.0", "Metadata release updated; structured schema preserved", checks);
        var animals = creatures.entries.Where(e => (e.kind == "wildlife" || e.species == "satyr") && e.motionProfile != null && e.motionProfile.IsValid).ToArray();
        Require(animals.Any(e => e.species == "deer" && Mathf.Abs(e.authoredWalkSpeed - .270493663f) < .000001f), "Repaired stag speed and complete approved travel curves", checks);
        foreach (var deer in animals)
        {
        Require(Mathf.Abs(deer.motionProfile.loopDistance.Last() / deer.motionProfile.loopDuration - deer.authoredWalkSpeed) < .00001f, deer.species + ": source loop speed matches travel", checks);
        Require(Mathf.Abs(deer.motionProfile.localForward.y) < .00001f && Mathf.Abs(deer.motionProfile.localForward.magnitude - 1f) < .00001f, deer.species + ": measured horizontal forward axis", checks);
        Quaternion orientation = Quaternion.LookRotation(Vector3.right) * Quaternion.Inverse(Quaternion.LookRotation(deer.motionProfile.localForward));
        Require(Vector3.Dot(orientation * deer.motionProfile.localForward, Vector3.right) > .9999f, deer.species + ": positive travel points toward muzzle; no vertical displacement", checks);
        foreach (int fps in new[] { 30, 60, 120 })
        {
            var gait = new YQDotGaitSampler(deer.motionProfile); float total = 0f, dt = 1f / fps;
            int startFrames = Mathf.RoundToInt(deer.motionProfile.startDuration * fps), loopFrames = Mathf.RoundToInt(deer.motionProfile.loopDuration * fps);
            for (int frame = 0; frame < startFrames + loopFrames * 2; frame++) total += gait.Advance(true, dt);
            for (int frame = 0; frame < Mathf.RoundToInt(deer.motionProfile.stopDuration * fps) + 2; frame++) total += gait.Advance(false, dt);
            float expected = deer.motionProfile.startDistance.Last() + 2f * deer.motionProfile.loopDistance.Last() + deer.motionProfile.stopDistance.Last();
            Require(!gait.IsActive && Mathf.Abs(total - expected) < .0001f && gait.Advance(false, dt) == 0f, fps + " fps: sampled start/two loops/stop exact travel and stationary idle", checks);
            // note: Exercise the wander owner's finite-route decision, so it cannot begin a loop that needs more room than the target provides and then slide through a stationary stop.
            var route = new YQDotGaitSampler(deer.motionProfile); float remaining = 1.5f; bool began = false, ended = false;
            for (int frame = 0; frame < fps * 30; frame++)
            {
                bool move = remaining > route.ContinuationDistance + .02f;
                float step = route.Advance(move, dt);
                Require(step <= remaining + .0001f, deer.assetId + " " + fps + " fps: complete gait stays within finite route", checks);
                remaining -= step; began |= route.IsActive;
                if (began && !route.IsActive) { ended = true; break; }
            }
            Require(ended && remaining >= -.0001f, deer.assetId + " " + fps + " fps: finite route reaches authored stop without clamping travel", checks);
        }
        Require(!new YQDotMotionProfile().IsValid, "Empty legacy profiles do not opt into sampled movement", checks);
        Scene scene = EditorSceneManager.NewPreviewScene(); GameObject actor = null; Mesh baked = null;
        try
        {
            actor = UnityEngine.Object.Instantiate(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(deer.prefabPath)); SceneManager.MoveGameObjectToScene(actor, scene);
            foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            baked = new Mesh();
            for (int lod = 0; lod < deer.sourcePaths.Length; lod++)
            {
                Transform model = actor.transform.Find("LOD" + lod);
                var animator = model.GetComponentInChildren<Animator>(true);
                var controller = (AnimatorController)animator.runtimeAnimatorController;
                Require(!animator.applyRootMotion && controller.layers[0].stateMachine.states.All(s => s.state.transitions.Length == 0), "LOD" + lod + ": explicit phase owner and in-place animation", checks);
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var skin in skins)
                    Require(skin.sharedMesh != null && skin.bones.Length > 0 && skin.bones.All(b => b != null) && skin.sharedMesh.bindposes.Length == skin.bones.Length, "LOD" + lod + ": current skin/bone/inverse-bind references", checks);
                foreach (string stateName in new[] { "DotIdle", "DotStart", "DotWalk", "DotStop" })
                {
                    var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == stateName).state;
                    var clip = state.motion as AnimationClip;
                    Require(clip != null && clip.length > 0f, "LOD" + lod + " " + stateName + ": own repaired clip", checks);
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                        Require(string.IsNullOrEmpty(binding.path) || animator.transform.Find(binding.path) != null, "LOD" + lod + " " + stateName + ": bound curve " + binding.path, checks);
                    for (int frame = 0; frame <= 30; frame++)
                    {
                        clip.SampleAnimation(animator.gameObject, clip.length * frame / 30f);
                        foreach (var skin in skins)
                        {
                            skin.BakeMesh(baked); Bounds bounds = baked.bounds;
                            Require(float.IsFinite(bounds.size.x) && float.IsFinite(bounds.size.y) && float.IsFinite(bounds.size.z) && bounds.size.sqrMagnitude > .00001f, "LOD" + lod + " " + stateName + ": finite evaluated skin frame " + frame, checks);
                        }
                        sampledFrames++;
                    }
                }
            }
        }
        finally
        {
            if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        YQDotCreaturePreview.RenderEntries(new[] { deer }, Output + "/StagPreview", true, new[] { "DotIdle", "DotStart", "DotWalk", "DotStop" });
        }
        VerifyCoverage(equipment, checks);
        var satyrs = animals.Where(e => e.species == "satyr").ToArray();
        if (satyrs.Length > 0) YQDotCreaturePreview.RenderEntries(satyrs, Output + "/SatyrPreview", false, new[] { "DotIdle", "DotWalk", "DotGreet" });
        File.WriteAllText(Output + "/focused-verification.json", JsonConvert.SerializeObject(new {
            status = "PASS", utc = DateTime.UtcNow, correctedMappings = 16, correctedExamples = examples.Count, sampledSkinFrames = sampledFrames, renderedWildlifePoses = animals.Sum(e => e.sourcePaths.Length * 4), animals = animals.Select(e => new { e.species, e.authoredWalkSpeed, forward = new[] { e.motionProfile.localForward.x, e.motionProfile.localForward.y, e.motionProfile.localForward.z } }),
            checks, evidence = "Fresh Unity Editor skin/bone/clip-curve references at all LODs, evaluated full-phase frames, sampled travel at three frame rates and material-label fixtures. Ordinary ground contact/physics/gameplay remains unverified."
        }, Formatting.Indented));
    }
    private static void VerifyCoverage(YQDotEquipmentCatalog catalog, List<string> checks)
    {
        // note: Resolve supplement provenance through the relocation map while retaining all original fit and coverage checks.
        var entries = catalog.entries.Where(e => YQDotAssetLayout.IsFromPack(e.sourcePath, YQDotEquipmentIntake.CoverageRoot)).ToArray();
        if (entries.Length == 0) return;
        Require(entries.Length == 16 && catalog.craftingCoverageSchemaVersion == "1.1.0", "Sixteen coverage bindings and separate 1.1 contract", checks);
        foreach (var entry in entries)
        {
            Require(!entry.generationEligible && entry.requiredMaterialKeys.Length >= 2 && catalog.TryValidateCraftingMaterials(entry.assetId, entry.requiredMaterialKeys, out _), entry.assetId + ": fitted admission gate and complete materials", checks);
            foreach (string omitted in entry.requiredMaterialKeys)
                Require(!catalog.TryValidateCraftingMaterials(entry.assetId, entry.requiredMaterialKeys.Where(k => k != omitted).ToArray(), out _), entry.assetId + ": missing " + omitted + " rejected", checks);
            var prefab = YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(entry.prefabPath);
            Require(prefab != null && prefab.GetComponentsInChildren<Animator>(true).Length == 0, entry.assetId + ": registry and presentation-only wrapper", checks);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                Require(renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported), entry.assetId + ": retained source materials", checks);
            if (entry.assembled) continue;
            var row = JObject.Parse(entry.sourceContractJson);
            Require(catalog.TryGetAsset((string)row["source_core_asset_id"], out var core), entry.assetId + ": original core available", checks);
            var corePrefab = YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(core.prefabPath);
            var bones = corePrefab.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
            var coreSkin = corePrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).First();
            var coreBinds = coreSkin.bones.Select((b, i) => new { b.name, matrix = coreSkin.sharedMesh.bindposes[i] }).ToDictionary(p => p.name, p => p.matrix);
            foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Require(skin.sharedMesh != null && skin.bones.Length == skin.sharedMesh.bindposes.Length, entry.assetId + ": complete inverse binds", checks);
                for (int i = 0; i < skin.bones.Length; i++)
                {
                    var bone = skin.bones[i]; Require(bone != null && bones.ContainsKey(bone.name) && coreBinds.ContainsKey(bone.name), entry.assetId + ": exact core bone " + i, checks);
                    Matrix4x4 a = skin.sharedMesh.bindposes[i], b = coreBinds[bone.name];
                    for (int k = 0; k < 16; k++) Require(Mathf.Abs(a[k] - b[k]) < .001f, entry.assetId + ": exact inverse bind " + i + "/" + k, checks);
                }
                var deliveredMorphs = ((JArray)row["morph_names"]).Values<string>().OrderBy(n => n).ToArray();
                var actualMorphs = Enumerable.Range(0, skin.sharedMesh.blendShapeCount).Select(skin.sharedMesh.GetBlendShapeName).OrderBy(n => n).ToArray();
                Require(deliveredMorphs.SequenceEqual(actualMorphs), entry.assetId + ": delivered morph names retained", checks);
            }
        }
        Require(YQDotEquipmentPreview.RenderEntries(entries.Where(e => e.assembled), Output + "/CoveragePreview"), "Eight authored coverage examples rendered", checks);
    }
    private static void Require(bool condition, string label, List<string> checks)
    { if (!condition) throw new InvalidDataException(label); checks.Add(label); }
}
