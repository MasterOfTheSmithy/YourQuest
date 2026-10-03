using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Disposable previews inspect supplied geometry and materials without altering the gameplay scene, inventory or source assets.
public static class YQDotEquipmentPreview
{
    [MenuItem("YourQuest/Player Animation/Preview DOT Equipment Models")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("DOT preview requires Edit Mode.");
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>("Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset");
        if (catalog == null) throw new InvalidOperationException("Build the DOT catalog first.");
        var entries = new System.Collections.Generic.List<YQDotEquipmentEntry>();
        foreach (string family in new[] { "sword", "shield", "amulet", "ring", "phial", "ration", "helmet", "chest_armor", "boots", "cloak" })
            entries.Add(catalog.entries.FirstOrDefault(e => e.assembled && e.family == family && string.IsNullOrEmpty(e.referenceBody)) ?? catalog.entries.First(e => e.assembled && e.family == family));
        RenderEntries(entries, "outputs/DOT_Integration_20261002/Preview", false);
    }

    public static bool RenderEntries(System.Collections.Generic.IEnumerable<YQDotEquipmentEntry> entries, string output, bool useAssetId = true)
    {
        // note: Both intakes share the same disposable rendering path; crafting previews never replace production equipment or change the open scene.
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("DOT preview requires Edit Mode.");
        Directory.CreateDirectory(output);
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject cameraObject = null, lightObject = null, actor = null;
        var bakedMeshes = new System.Collections.Generic.List<Mesh>();
        var samples = new System.Collections.Generic.List<object>(); string error = null;
        try
        {
            cameraObject = new GameObject("DOT preview camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .08f, .12f); camera.fieldOfView = 35f; camera.nearClipPlane = .001f;
            lightObject = new GameObject("DOT preview light"); SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f); lightObject.AddComponent<Light>().type = LightType.Directional;
            foreach (var entry in entries)
            {
                string family = entry.family;
                actor = UnityEngine.Object.Instantiate(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(entry.prefabPath));
                SceneManager.MoveGameObjectToScene(actor, scene);
                // note: A disposable single-LOD render has no Editor update to refresh LOD visibility after pruning.
                foreach (var group in actor.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(group);
                // note: The CPU bake helper renders only the chosen LOD; discard other children in this disposable instance.
                for (int index = actor.transform.childCount - 1; index > 0; index--) UnityEngine.Object.DestroyImmediate(actor.transform.GetChild(index).gameObject);
                // note: Frame the evaluated CPU geometry, since imported skinned bounds can differ from a baked mesh's coordinate space.
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh = new Mesh(); skin.BakeMesh(mesh); bakedMeshes.Add(mesh);
                    var baked = new GameObject("Evaluated DOT preview geometry"); baked.transform.SetParent(skin.transform, false);
                    baked.AddComponent<MeshFilter>().sharedMesh = mesh;
                    baked.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                }
                var renderers = actor.transform.GetChild(0).GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Missing preview geometry: " + entry.assetId);
                foreach (var renderer in renderers) { renderer.enabled = true; renderer.forceRenderingOff = false; }
                Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                float extent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (!(extent > .0001f) || !float.IsFinite(extent)) throw new InvalidOperationException("Invalid DOT bounds: " + entry.assetId);
                camera.transform.position = bounds.center + new Vector3(.8f, .3f, 1f).normalized * extent * 2.2f;
                camera.transform.LookAt(bounds.center); camera.farClipPlane = Mathf.Max(10f, extent * 10f);
                string imagePath = Path.Combine(output, (useAssetId ? entry.assetId : family) + ".png");
                Render(camera, imagePath);
                int visiblePixels = CountVisiblePixels(imagePath);
                samples.Add(new { family, entry.assetId, entry.prefabPath, bounds = bounds.size.ToString(), visiblePixels, evidence = "Delivered model/material render; not fitted outfit or gameplay evidence." });
                if (visiblePixels < 100) throw new InvalidOperationException("Blank DOT preview: " + entry.assetId);
                UnityEngine.Object.DestroyImmediate(actor); actor = null;
                foreach (var mesh in bakedMeshes) UnityEngine.Object.DestroyImmediate(mesh); bakedMeshes.Clear();
            }
        }
        catch (Exception exception) { error = exception.ToString(); Debug.LogException(exception); }
        finally
        {
            if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            foreach (var mesh in bakedMeshes) UnityEngine.Object.DestroyImmediate(mesh);
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
            EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllText(Path.Combine(output, "receipt.json"), JsonConvert.SerializeObject(new { status = error == null ? "PASS" : "FAIL", utc = DateTime.UtcNow, samples, error }, Formatting.Indented));
        }
        return error == null;
    }

    private static void Render(Camera camera, string path)
    {
        // note: Render the already evaluated geometry without baking the same garment twice or changing supplied materials.
        var target = new RenderTexture(640, 640, 24);
        var texture = new Texture2D(640, 640, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static int CountVisiblePixels(string path)
    {
        // note: Image creation alone is insufficient evidence; compare geometry against the rendered background corner.
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Unreadable preview: " + path);
            var pixels = texture.GetPixels32(); Color32 background = pixels[0]; int count = 0;
            foreach (var pixel in pixels)
                if (Math.Abs(pixel.r - background.r) + Math.Abs(pixel.g - background.g) + Math.Abs(pixel.b - background.b) > 12) count++;
            return count;
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
}
