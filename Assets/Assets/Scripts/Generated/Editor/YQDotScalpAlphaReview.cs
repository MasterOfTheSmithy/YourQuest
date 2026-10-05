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

// note: Contrasting head-orbit renders use disposable copies of the actual imported skin/materials; they provide bounded visual evidence, not production sorting certification.
public static class YQDotScalpAlphaReview
{
    internal static void Render(IEnumerable<YQDotCreatureEntry> entries, string output)
    {
        Directory.CreateDirectory(output); var scene = EditorSceneManager.NewPreviewScene();
        GameObject actor = null, cameraObject = null, lightObject = null, backdrop = null;
        var meshes = new List<Mesh>(); Material backdropMaterial = null;
        var samples = new List<object>();
        try
        {
            cameraObject = new GameObject("Alpha review camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.orthographic = true; camera.nearClipPlane = .01f; camera.farClipPlane = 100f;
            lightObject = new GameObject("Alpha review light"); SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f;
            // note: The contrasting object sits behind the head on each orbit, making holes and overlap visible without changing imported race materials.
            backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(backdrop, scene);
            var backdropShader = Shader.Find("Universal Render Pipeline/Unlit"); if (backdropShader == null) backdropShader = Shader.Find("Unlit/Color");
            backdropMaterial = new Material(backdropShader); backdropMaterial.color = new Color(.1f, .65f, .9f); backdrop.GetComponent<Renderer>().sharedMaterial = backdropMaterial;
            foreach (var entry in entries)
            {
                actor = UnityEngine.Object.Instantiate(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(entry.prefabPath)); SceneManager.MoveGameObjectToScene(actor, scene);
                foreach (var group in actor.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(group);
                for (int i = actor.transform.childCount - 1; i >= 0; i--) if (actor.transform.GetChild(i).name != "LOD0") UnityEngine.Object.DestroyImmediate(actor.transform.GetChild(i).gameObject);
                var model = actor.transform.Find("LOD0"); var animator = model.GetComponentInChildren<Animator>(true); animator.enabled = false;
                var controller = (AnimatorController)animator.runtimeAnimatorController;
                ((AnimationClip)controller.layers[0].stateMachine.states.Single(s => s.state.name == "DotIdle").state.motion).SampleAnimation(animator.gameObject, .4f);
                var head = model.GetComponentsInChildren<Transform>(true).Single(t => t.name.Equals("head", StringComparison.OrdinalIgnoreCase));
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh = new Mesh(); skin.BakeMesh(mesh); meshes.Add(mesh);
                    var baked = new GameObject("Evaluated alpha skin"); baked.transform.SetParent(skin.transform, false); baked.AddComponent<MeshFilter>().sharedMesh = mesh; baked.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials; skin.enabled = false;
                }
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true)) { renderer.enabled = true; renderer.forceRenderingOff = false; }
                for (int view = 0; view < 4; view++)
                {
                    Vector3 direction = Quaternion.Euler(0f, view * 90f + 20f, 0f) * Vector3.forward;
                    Vector3 aim = head.position + Vector3.up * .1f; camera.orthographicSize = .48f;
                    camera.backgroundColor = view % 2 == 0 ? new Color(.92f, .93f, .97f) : new Color(.06f, .08f, .12f);
                    camera.transform.position = aim + direction * 2.5f + Vector3.up * .08f; camera.transform.LookAt(aim);
                    light.transform.rotation = Quaternion.LookRotation(-direction + Vector3.down * .35f);
                    backdrop.transform.position = aim - direction * .7f; backdrop.transform.rotation = Quaternion.LookRotation(direction); backdrop.transform.localScale = new Vector3(.23f, .7f, .05f);
                    string path = output + "/" + entry.assetId + "_HeadOrbit" + view + ".png"; Capture(camera, path);
                    samples.Add(new { entry.assetId, view, path });
                }
                UnityEngine.Object.DestroyImmediate(actor); actor = null; foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh); meshes.Clear();
            }
            File.WriteAllText(output + "/receipt.json", JsonConvert.SerializeObject(new { status = "PASS", utc = DateTime.UtcNow, samples, evidence = "Actual LOD0 head skins and original materials at four orbit angles with light/dark backgrounds and contrasting object. Static previews only; production motion/sorting and camera LOD transitions remain unverified." }, Formatting.Indented));
        }
        finally
        {
            if (actor != null) UnityEngine.Object.DestroyImmediate(actor); foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject); if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject); if (backdrop != null) UnityEngine.Object.DestroyImmediate(backdrop); if (backdropMaterial != null) UnityEngine.Object.DestroyImmediate(backdropMaterial);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static void Capture(Camera camera, string path)
    {
        var target = RenderTexture.GetTemporary(640, 640, 24); var texture = new Texture2D(640, 640, TextureFormat.RGB24, false); var previous = RenderTexture.active;
        try { camera.targetTexture = target; camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
        finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(texture); }
    }
}
