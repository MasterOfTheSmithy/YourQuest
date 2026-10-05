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

// note: Disposable rendered poses verify imported skin/material presentation without modifying the user's scene, camera, actors or saved world.
public static class YQDotCreaturePreview
{
    [MenuItem("YourQuest/DOT Assets/Render Creature Binding Review")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Creature render review requires Edit Mode.");
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>("Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset");
        if (catalog == null) throw new InvalidOperationException("Build the creature catalog first.");
        RenderEntries(catalog.entries.Where(e => string.IsNullOrEmpty(e.moduleSlot)).GroupBy(e => e.species).Select(g => g.First()), "outputs/DOT_Integration_20261003/Preview");
    }

    public static void RenderEntries(IEnumerable<YQDotCreatureEntry> entries, string output, bool allLods = false, string[] poses = null, bool allowSourceGreet = false, bool bakeSkins = true)
    {
        // note: Reuse the existing pose renderer for all newly supplied NPC assemblies while preserving earlier review images and evidence.
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Creature render review requires Edit Mode.");
        Directory.CreateDirectory(output);
        Scene scene = EditorSceneManager.NewPreviewScene();
        var samples = new List<object>();
        GameObject actor = null, cameraObject = null, lightObject = null;
        var meshes = new List<Mesh>();
        try
        {
            cameraObject = new GameObject("DOT creature review camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .08f, .12f); camera.fieldOfView = 35f; camera.nearClipPlane = .01f;
            lightObject = new GameObject("DOT creature review light"); SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            foreach (var entry in entries)
                foreach (int level in Enumerable.Range(0, allLods ? entry.sourcePaths.Length : 1))
                foreach (string pose in poses ?? new[] { "DotIdle", "DotWalk" })
                {
                    actor = UnityEngine.Object.Instantiate(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(entry.prefabPath)); SceneManager.MoveGameObjectToScene(actor, scene);
                    // note: Remove LOD culling only on this disposable copy; a disabled LODGroup can retain stale renderer visibility during a synchronous render.
                    foreach (var group in actor.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(group);
                    for (int index = actor.transform.childCount - 1; index >= 0; index--)
                        if (actor.transform.GetChild(index).name != "LOD" + level) UnityEngine.Object.DestroyImmediate(actor.transform.GetChild(index).gameObject);
                    Transform model = actor.transform.Find("LOD" + level);
                    Animator animator = model.GetComponentInChildren<Animator>(true);
                    var controller = animator.runtimeAnimatorController as AnimatorController;
                    var state = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == pose).state;
                    var clip = state != null ? state.motion as AnimationClip : null;
                    // note: A material-only review can evaluate a delivered greeting absent from a historical controller without adding a state or rebuilding that controller.
                    if (clip == null && allowSourceGreet && pose == "DotGreet") clip = AssetDatabase.LoadAllAssetsAtPath(entry.sourcePaths[level]).OfType<AnimationClip>().Single(c => c.name.IndexOf("greet", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (clip == null) throw new InvalidDataException("Missing own preview clip: " + entry.assetId + " " + pose);
                    foreach (Animator other in actor.GetComponentsInChildren<Animator>(true)) other.enabled = false;
                    clip.SampleAnimation(animator.gameObject, clip.length * .4f);
                    // note: Bake the actual supplied skin after evaluating its own clip. No replacement geometry, texture or material is synthesized.
                    foreach (var skin in bakeSkins ? model.GetComponentsInChildren<SkinnedMeshRenderer>(true) : Array.Empty<SkinnedMeshRenderer>())
                    {
                        var mesh = new Mesh(); skin.BakeMesh(mesh); meshes.Add(mesh);
                        var baked = new GameObject("Evaluated creature skin"); baked.transform.SetParent(skin.transform, false);
                        baked.AddComponent<MeshFilter>().sharedMesh = mesh; baked.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials; skin.enabled = false;
                    }
                    Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || (!bakeSkins && r is SkinnedMeshRenderer)).ToArray();
                    if (renderers.Length == 0) throw new InvalidOperationException("Creature pose has no evaluated geometry: " + entry.assetId);
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) { renderer.enabled = true; renderer.forceRenderingOff = false; bounds.Encapsulate(renderer.bounds); }
                    Vector3 aim = bounds.center;
                    float distance = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) * 2.5f;
                    if (!float.IsFinite(distance) || distance < .001f) throw new InvalidOperationException("Invalid evaluated creature bounds: " + entry.assetId + " " + bounds);
                    camera.farClipPlane = Mathf.Max(1000f, distance * 4f);
                    camera.transform.position = aim + new Vector3(.65f, .3f, 1f).normalized * distance; camera.transform.LookAt(aim);
                    string image = output + "/" + entry.assetId + (allLods ? "_LOD" + level : "") + "_" + pose + ".png";
                    int pixels = Render(camera, image);
                    if (pixels < 100) throw new InvalidOperationException("Blank creature render: " + entry.assetId + "; bounds=" + bounds + "; renderers=" + renderers.Length);
                    samples.Add(new { entry.assetId, pose, image, visiblePixels = pixels });
                    UnityEngine.Object.DestroyImmediate(actor); actor = null;
                    foreach (Mesh mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh); meshes.Clear();
                }
            File.WriteAllText(output + "/receipt.json", JsonConvert.SerializeObject(new { status = "PASS", utc = DateTime.UtcNow, samples,
                evidence = "Editor renders of actual LOD0 skins/materials at supplied idle/walk poses. Live motion, world contact, combat timing and ordinary gameplay remain unverified." }, Formatting.Indented));
        }
        finally
        {
            if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            foreach (Mesh mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static int Render(Camera camera, string path)
    {
        var target = new RenderTexture(480, 480, 24); var texture = new Texture2D(480, 480, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 480, 480), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            Color32[] pixels = texture.GetPixels32(); Color32 background = pixels[0]; int visible = 0;
            foreach (Color32 pixel in pixels) if (Math.Abs(pixel.r - background.r) + Math.Abs(pixel.g - background.g) + Math.Abs(pixel.b - background.b) > 12) visible++;
            return visible;
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture); }
    }
}
