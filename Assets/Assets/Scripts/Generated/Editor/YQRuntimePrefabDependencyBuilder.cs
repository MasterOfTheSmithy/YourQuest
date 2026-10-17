#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// note: This explicit build operates on the complete local asset checkout. It never runs dependency discovery during Play or rewrites source/imported assets.
public static class YQRuntimePrefabDependencyBuilder
{
    private const string ResourcesFolder = "Assets/Assets/Resources";
    private const string BindingManifestPath = ResourcesFolder + "/YQWorldAssetManifest.json";
    private const string ManifestPath = ResourcesFolder + "/YQWorldPrefabDependencies.json";
    private const string Owner = "YourQuest.PrefabDependencyPreloads.v1";

    private sealed class Dependency
    {
        public string identity, resource;
        public int order;
        public UnityEngine.Object asset;
    }

    private sealed class PrefabRow
    {
        public string path, entrySha256, sourceDependencyHash;
        public List<string> resources;
    }

    [MenuItem("YourQuest/Assets/Build Prefab Dependency Preloads")]
    public static void Rebuild()
    {
        RequireStableEditMode();
        if (!File.Exists(BindingManifestPath)) throw new InvalidOperationException("Build individual runtime asset bindings first.");
        string bindingSource = File.ReadAllText(BindingManifestPath);
        JObject bindings = JObject.Parse(bindingSource);
        if ((int?)bindings["version"] != 1 || !(bindings["assets"] is JArray assets))
            throw new InvalidOperationException("Unsupported runtime binding manifest.");
        if (File.Exists(ManifestPath) && (string)JObject.Parse(File.ReadAllText(ManifestPath))["generator"] != Owner)
            throw new InvalidOperationException("Preserve the unowned prefab dependency manifest.");

        var prefabBindings = assets.OfType<JObject>().Where(row =>
            ((string)row["path"] ?? string.Empty).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => (string)row["path"], StringComparer.OrdinalIgnoreCase).ToArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JObject binding in prefabBindings)
        {
            string path = (string)binding["path"], resource = (string)binding["resource"];
            // note: Missing imported assets in a source-only checkout stop before any descriptor or manifest is published.
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") ||
                !seen.Add(path) || string.IsNullOrWhiteSpace((string)binding["entrySha256"]) ||
                string.IsNullOrWhiteSpace(resource) || !resource.StartsWith("YQWorldAssetLeaves/", StringComparison.Ordinal) || resource.Contains("..") ||
                !File.Exists(path) || !File.Exists(ResourcesFolder + "/" + resource + ".asset"))
                throw new InvalidOperationException("Complete imported source and runtime leaf required: " + path);
        }

        var dependencies = new Dictionary<string, Dependency>(StringComparer.Ordinal);
        var files = new Dictionary<string, List<Dependency>>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<PrefabRow>(prefabBindings.Length);
        foreach (JObject binding in prefabBindings)
        {
            string path = (string)binding["path"];
            List<Dependency> selected = ReadPrefabDependencies(path, files);
            foreach (Dependency dependency in selected) dependencies[dependency.identity] = dependency;
            rows.Add(new PrefabRow
            {
                path = path,
                entrySha256 = (string)binding["entrySha256"],
                sourceDependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
                // note: Clips precede controllers; source GUID/local-id ordering is independent of AssetDatabase enumeration and runtime traversal order.
                resources = selected.Select(item => item.resource).ToList()
            });
        }

        foreach (Dependency dependency in dependencies.Values)
        {
            string destination = ResourcesFolder + "/" + dependency.resource + ".asset";
            if (!File.Exists(destination)) continue;
            var existing = AssetDatabase.LoadAssetAtPath<YQRuntimePrefabDependency>(destination);
            if (existing == null || existing.sourceIdentity != dependency.identity || existing.asset != dependency.asset)
                throw new InvalidOperationException("Preserve a modified/unowned dependency descriptor: " + destination);
        }
        if (File.ReadAllText(BindingManifestPath) != bindingSource)
            throw new InvalidOperationException("Runtime binding manifest changed during dependency preparation.");
        foreach (PrefabRow row in rows)
            if (row.sourceDependencyHash != AssetDatabase.GetAssetDependencyHash(row.path).ToString())
                throw new InvalidOperationException("Prefab dependencies changed during preparation: " + row.path);

        if (!AssetDatabase.IsValidFolder(ResourcesFolder + "/YQWorldAssetDependencies"))
            AssetDatabase.CreateFolder(ResourcesFolder, "YQWorldAssetDependencies");
        foreach (Dependency dependency in dependencies.Values.OrderBy(item => item.identity, StringComparer.Ordinal))
        {
            string destination = ResourcesFolder + "/" + dependency.resource + ".asset";
            if (File.Exists(destination)) continue;
            var descriptor = ScriptableObject.CreateInstance<YQRuntimePrefabDependency>();
            descriptor.asset = dependency.asset;
            descriptor.sourceIdentity = dependency.identity;
            AssetDatabase.CreateAsset(descriptor, destination);
        }
        // note: Publish strings only after every referenced descriptor is durable. Existing descriptor/imported GUIDs remain untouched.
        var manifest = new { version = 1, generator = Owner, prefabs = rows };
        File.WriteAllText(ManifestPath, JsonConvert.SerializeObject(manifest, Formatting.Indented), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceSynchronousImport);
        YQRuntimeWorldAssetRegistry.ClearCachedInstance();
        ValidateCurrent();
        Debug.Log("[YQRuntimePrefabDependencyBuilder] Published " + rows.Count + " prefab dependency rows and " + dependencies.Count +
            " distinct animation/controller descriptors. This is Editor binding evidence; cold native activation and travel remain unqualified.");
    }

    private static List<Dependency> ReadAnimationDependencies(string path)
    {
        var result = new List<Dependency>();
        Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
        bool animationAsset = type != null && (typeof(AnimationClip).IsAssignableFrom(type) || typeof(RuntimeAnimatorController).IsAssignableFrom(type));
        bool modelAsset = AssetImporter.GetAtPath(path) is ModelImporter importer && importer.importAnimation;
        if (!animationAsset && !modelAsset) return result;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (!(asset is AnimationClip) && !(asset is RuntimeAnimatorController)) continue;
            if (asset.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId) || string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException("Persistent animation identity is missing: " + path);
            string identity = guid + ":" + localId.ToString(CultureInfo.InvariantCulture);
            result.Add(new Dependency { identity = identity, resource = "YQWorldAssetDependencies/" + Hash(identity),
                order = asset is AnimationClip ? 0 : 1, asset = asset });
        }
        return result;
    }

    private static List<Dependency> ReadPrefabDependencies(string path, Dictionary<string, List<Dependency>> files)
    {
        // note: The transitive imported closure includes every authored LOD controller, override controller and clip; unrelated prefab families are excluded.
        var selected = new Dictionary<string, Dependency>(StringComparer.Ordinal);
        foreach (string dependencyPath in AssetDatabase.GetDependencies(path, true))
        {
            if (!files.TryGetValue(dependencyPath, out List<Dependency> imported))
            {
                imported = ReadAnimationDependencies(dependencyPath);
                files.Add(dependencyPath, imported);
            }
            foreach (Dependency dependency in imported) selected[dependency.identity] = dependency;
        }
        return selected.Values.OrderBy(item => item.order).ThenBy(item => item.identity, StringComparer.Ordinal).ToList();
    }

    [MenuItem("YourQuest/Assets/Validate Prefab Dependency Preloads")]
    public static void ValidateCurrent()
    {
        RequireStableEditMode();
        if (!File.Exists(ManifestPath)) throw new InvalidOperationException("Prefab dependency metadata is absent; run Build Prefab Dependency Preloads.");
        JObject manifest = JObject.Parse(File.ReadAllText(ManifestPath));
        if ((int?)manifest["version"] != 1 || (string)manifest["generator"] != Owner || !(manifest["prefabs"] is JArray rows))
            throw new InvalidOperationException("Prefab dependency metadata has an unsupported owner/version.");
        JObject bindings = JObject.Parse(File.ReadAllText(BindingManifestPath));
        var expected = ((JArray)bindings["assets"]).OfType<JObject>()
            .Where(row => ((string)row["path"] ?? string.Empty).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(row => (string)row["path"], row => (string)row["entrySha256"], StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new Dictionary<string, List<Dependency>>(StringComparer.OrdinalIgnoreCase);
        foreach (JObject row in rows)
        {
            string path = (string)row["path"];
            if (path == null || !seen.Add(path) || !expected.TryGetValue(path, out string entryHash) || entryHash != (string)row["entrySha256"] ||
                !File.Exists(path) || AssetDatabase.GetAssetDependencyHash(path).ToString() != (string)row["sourceDependencyHash"])
                throw new InvalidOperationException("Missing/stale prefab dependency row: " + path);
            if (!(row["resources"] is JArray resources) || !ReadPrefabDependencies(path, files).Select(item => item.resource)
                    .SequenceEqual(resources.Select(value => (string)value), StringComparer.Ordinal))
                throw new InvalidOperationException("Prefab dependency closure/order differs from imported source: " + path);
            foreach (JToken value in resources)
            {
                string resource = (string)value;
                if (string.IsNullOrWhiteSpace(resource) || !resource.StartsWith("YQWorldAssetDependencies/", StringComparison.Ordinal) || resource.Contains(".."))
                    throw new InvalidOperationException("Invalid prefab dependency resource: " + path);
                var descriptor = AssetDatabase.LoadAssetAtPath<YQRuntimePrefabDependency>(ResourcesFolder + "/" + resource + ".asset");
                if (descriptor == null || descriptor.asset == null ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(descriptor.asset, out string guid, out long localId) ||
                    descriptor.sourceIdentity != guid + ":" + localId.ToString(CultureInfo.InvariantCulture) ||
                    resource != "YQWorldAssetDependencies/" + Hash(descriptor.sourceIdentity))
                    throw new InvalidOperationException("Prefab dependency descriptor identity mismatch: " + resource);
            }
        }
        if (seen.Count != expected.Count) throw new InvalidOperationException("Prefab dependency metadata does not cover the current runtime bindings.");
        Debug.Log("[YQRuntimePrefabDependencyBuilder] Dependency metadata matches " + seen.Count + " current prefab bindings and imported source hashes.");
    }

    internal static void ReportBindingRebuildRequirement()
    {
        // note: A registry rebuild may change dependency membership. It never silently runs an imported asset scan as an Editor side effect.
        Debug.LogWarning("[YQRuntimePrefabDependencyBuilder] Runtime bindings rebuilt. Run YourQuest > Assets > Build Prefab Dependency Preloads " +
            "before cold population/travel qualification; existing dependency rows are accepted only while their binding hashes match.");
    }

    private static void RequireStableEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Prefab dependency preparation/validation requires stable Edit mode.");
    }

    private static string Hash(string value)
    {
        using (SHA256 hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
    }
}

public sealed class YQRuntimePrefabDependencyBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var registry = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>("Assets/Assets/Resources/YQRuntimeWorldAssetRegistry.asset");
        if (registry == null || !registry.UsesGranularResourceShards) return;
        // note: Binding hashes alone cannot detect a changed controller/imported source. A player build must verify the exact current closure without rewriting assets.
        try { YQRuntimePrefabDependencyBuilder.ValidateCurrent(); }
        catch (Exception error)
        {
            throw new BuildFailedException("Prefab dependency preload metadata is not current. Run YourQuest > Assets > Build Prefab Dependency Preloads. " + error.Message);
        }
    }
}
#endif
