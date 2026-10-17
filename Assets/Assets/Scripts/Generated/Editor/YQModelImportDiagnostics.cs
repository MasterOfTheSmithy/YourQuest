using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

// note: Read only the explicitly requested importers; this never edits rigs, scenes or import settings.
[InitializeOnLoad]
public static class YQModelImportDiagnostics
{
    private const string Folder = "outputs/ModelImportSoftPatch";
    private static double next;
    static YQModelImportDiagnostics() { EditorApplication.update += Tick; }

    private static void Tick()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 1;
        string retry = Folder + "/retry-blender.request";
        if (File.Exists(retry))
        {
            File.Delete(retry);
            RetryBlender();
            return;
        }
        string request = Folder + "/inspect.request";
        if (!File.Exists(request)) return;
        File.Delete(request);
        Inspect();
    }

    [MenuItem("YourQuest/Diagnostics/Retry Requested Failed Blender Imports")]
    public static void RetryBlender()
    {
        // note: Retry only explicitly listed files with the observed missing-Blender failure; never rewrite source data or settings.
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var paths = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(Folder + "/retry-blender-paths.json"));
            foreach (string path in paths)
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") ||
                    !path.EndsWith(".blend", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Blender asset path required: " + path);
                var log = AssetImporter.GetImportLog(path);
                if (log == null || !log.logEntries.Any(e => (e.flags & ImportLogFlags.Error) != 0 &&
                    e.message.Contains("Blender could not be found")))
                    throw new InvalidOperationException("Expected missing-Blender precondition: " + path);
            }
            foreach (string path in paths)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Inspect();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Folder + "/retry-blender-failure.txt", ex.ToString());
            Debug.LogException(ex);
        }
    }

    [MenuItem("YourQuest/Diagnostics/Inspect Requested Model Imports")]
    public static void Inspect()
    {
        try
        {
            var paths = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(Folder + "/paths.json"));
            var rows = paths.Select(path =>
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains(".."))
                    throw new InvalidDataException("Asset path required: " + path);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var log = AssetImporter.GetImportLog(path);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var objects = AssetDatabase.LoadAllAssetsAtPath(path);
                return new
                {
                    path, guid = AssetDatabase.AssetPathToGUID(path),
                    animationType = importer?.animationType.ToString(),
                    avatarSetup = importer?.avatarSetup.ToString(),
                    sourceAvatar = importer == null ? null : AssetDatabase.GetAssetPath(importer.sourceAvatar),
                    messages = log?.logEntries.Select(e => new { flags = e.flags.ToString(), e.message }).ToArray(),
                    human = importer?.humanDescription.human.Select(h => new { h.humanName, h.boneName }).ToArray(),
                    transforms = model == null ? null : model.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray(),
                    // note: Read current prefab/model mesh bindings after narrow reference recovery; this never assigns meshes or changes importer state.
                    rendererMeshes = model == null ? null : model.GetComponentsInChildren<Renderer>(true).Select(DescribeRendererMesh).ToArray(),
                    assets = objects.Select(o =>
                    {
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long id);
                        return new { name = o.name, type = o.GetType().Name, id,
                            valid = o is Avatar a ? (bool?)a.isValid : null,
                            human = o is Avatar av ? (bool?)av.isHuman : null,
                            vertices = o is Mesh mesh ? (int?)mesh.vertexCount : null,
                            blendShapes = o is Mesh m ? (int?)m.blendShapeCount : null,
                            duration = o is AnimationClip c ? (float?)c.length : null };
                    }).ToArray()
                };
            }).ToArray();
            File.WriteAllText(Folder + "/inspection.json", JsonConvert.SerializeObject(new
            { utc = DateTime.UtcNow.ToString("O"), evidence = "Live Unity importer inspection; no gameplay run", rows }, Formatting.Indented));
            Debug.Log("[YQModelImportDiagnostics] Inspected " + rows.Length + " requested models.");
        }
        catch (Exception ex)
        {
            File.WriteAllText(Folder + "/inspection-failure.txt", ex.ToString());
            Debug.LogException(ex);
        }
    }

    private static object DescribeRendererMesh(Renderer renderer)
    {
        // note: Native asset GUID/local IDs distinguish a resolved original mesh from a stale serialized reference.
        Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh :
            renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
        string guid = null; long id = 0;
        if (mesh != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out guid, out id);
        return new { name = renderer.name, type = renderer.GetType().Name, activeSelf = renderer.gameObject.activeSelf,
            meshName = mesh != null ? mesh.name : null, meshGuid = guid, meshId = id,
            vertices = mesh != null ? (int?)mesh.vertexCount : null };
    }
}
