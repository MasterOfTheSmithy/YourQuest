#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
    private const string BindingOwner = "YourQuest.GranularWorldAssetResources.v1";
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

    private sealed class PrefabBinding
    {
        public JObject manifestRow;
        public string path, entrySha256, sourcePath, sourceIdentity;
    }

    [MenuItem("YourQuest/Assets/Build Prefab Dependency Preloads")]
    public static void Rebuild()
    {
        RequireStableEditMode();
        if (!File.Exists(BindingManifestPath)) throw new InvalidOperationException("Build individual runtime asset bindings first.");
        string bindingSource = File.ReadAllText(BindingManifestPath);
        if (File.Exists(ManifestPath) && (string)JObject.Parse(File.ReadAllText(ManifestPath))["generator"] != Owner)
            throw new InvalidOperationException("Preserve the unowned prefab dependency manifest.");
        // note: Runtime keys survive GUID-preserving prefab moves. Resolve every conserved binding before preparing or publishing any descriptors.
        PrefabBinding[] prefabBindings = ReadPrefabBindings(bindingSource);

        var dependencies = new Dictionary<string, Dependency>(StringComparer.Ordinal);
        var files = new Dictionary<string, List<Dependency>>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<PrefabRow>(prefabBindings.Length);
        foreach (PrefabBinding binding in prefabBindings)
        {
            // note: Capture the physical source hash before discovery so a dependency change during preparation cannot publish a stale closure.
            string sourceHash = AssetDatabase.GetAssetDependencyHash(binding.sourcePath).ToString();
            List<Dependency> selected = ReadPrefabDependencies(binding.sourcePath, files);
            foreach (Dependency dependency in selected) dependencies[dependency.identity] = dependency;
            rows.Add(new PrefabRow
            {
                path = binding.path,
                entrySha256 = binding.entrySha256,
                sourceDependencyHash = sourceHash,
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
        for (int index = 0; index < prefabBindings.Length; index++)
        {
            // note: An unchanged manifest does not prove that its leaf or GUID-to-path mapping stayed unchanged during discovery.
            PrefabBinding original = prefabBindings[index];
            PrefabBinding current = ResolvePrefabBinding(original.manifestRow);
            if (current.sourcePath != original.sourcePath || current.sourceIdentity != original.sourceIdentity ||
                rows[index].sourceDependencyHash != AssetDatabase.GetAssetDependencyHash(current.sourcePath).ToString())
                throw new InvalidOperationException("Prefab source/dependencies changed during preparation: " + original.path);
        }

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

    private static PrefabBinding[] ReadPrefabBindings(string source)
    {
        JObject bindings = JObject.Parse(source);
        if ((int?)bindings["version"] != 1 || (string)bindings["generator"] != BindingOwner || !(bindings["assets"] is JArray assets))
            throw new InvalidOperationException("Unsupported runtime binding manifest owner/version.");
        // note: Malformed rows must not disappear from the completeness check when selecting prefab bindings from the mixed asset manifest.
        if (assets.Any(item => !(item is JObject row) || row["path"]?.Type != JTokenType.String ||
                string.IsNullOrWhiteSpace((string)row["path"])))
            throw new InvalidOperationException("Runtime binding manifest contains an invalid asset row/path.");
        var rows = assets.OfType<JObject>().Where(row =>
            ((string)row["path"] ?? string.Empty).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => (string)row["path"], StringComparer.OrdinalIgnoreCase).ToArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PrefabBinding>(rows.Length);
        foreach (JObject row in rows)
        {
            if (!seen.Add((string)row["path"]))
                throw new InvalidOperationException("Duplicate runtime prefab binding: " + (string)row["path"]);
            result.Add(ResolvePrefabBinding(row));
        }
        return result.ToArray();
    }

    private static PrefabBinding ResolvePrefabBinding(JObject binding)
    {
        string path = (string)binding["path"], resource = (string)binding["resource"];
        string entryHash = (string)binding["entrySha256"], leafHash = (string)binding["leafSha256"];
        if (!IsProjectAssetPath(path) || !IsSha256(entryHash) || !IsSha256(leafHash) ||
            resource != "YQWorldAssetLeaves/" + Hash(YQRuntimeWorldAssetRegistry.NormalizePath(path).ToLowerInvariant()))
            throw new InvalidOperationException("Invalid runtime prefab binding path/resource/hash: " + path);

        string leafPath = ResourcesFolder + "/" + resource + ".asset";
        if (!File.Exists(leafPath))
            throw new InvalidOperationException("Missing runtime leaf for '" + path + "': " + leafPath +
                ". Run YourQuest > Assets > Build Individual Runtime Asset Bindings (Preserve Legacy).");
        // note: Read the generator's conserved text instead of loading every leaf and its native prefab graph just to recover a persistent identity.
        string leaf = File.ReadAllText(leafPath).Replace("\r\n", "\n");
        if (Hash(leaf) != leafHash)
            throw new InvalidOperationException("Modified/stale runtime leaf for '" + path + "': " + leafPath);
        MatchCollection entries = Regex.Matches(leaf, @"(?m)^  - assetPath: (.+(?:\n {6}.+)*)\n");
        Match header = Regex.Match(leaf, @"(?m)^  entries:\n");
        Match end = Regex.Match(leaf, @"(?m)^  useLazyResourceShards:");
        if (entries.Count != 1 || !header.Success || header.Index + header.Length != entries[0].Index ||
            !end.Success || end.Index <= entries[0].Index ||
            Regex.Replace(entries[0].Groups[1].Value, @"\n {6}", " ") != path)
            throw new InvalidOperationException("Runtime leaf must contain exactly its canonical binding: " + path);
        string entry = leaf.Substring(entries[0].Index, end.Index - entries[0].Index);
        if (Hash(entry) != entryHash)
            throw new InvalidOperationException("Runtime leaf entry hash mismatch: " + path);

        // note: Only the entry's prefab reference is authoritative; the leaf header and material overrides contain unrelated GUIDs.
        MatchCollection references = Regex.Matches(entry,
            @"(?m)^    prefab: \{fileID: (-?\d+), guid: ([a-fA-F0-9]{32}), type: 3\}$");
        if (Regex.Matches(entry, @"(?m)^    prefab[ \t]*:").Count != 1 || references.Count != 1 ||
            !long.TryParse(references[0].Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long localId) ||
            localId == 0 || references[0].Groups[2].Value == "00000000000000000000000000000000")
            throw new InvalidOperationException("Runtime leaf requires one persistent prefab GUID/fileID: " + path);
        string guid = references[0].Groups[2].Value.ToLowerInvariant();
        string identity = guid + ":" + localId.ToString(CultureInfo.InvariantCulture);
        string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
        if (!IsProjectAssetPath(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException("Missing imported prefab source for '" + path + "' (GUID/fileID " + identity +
                "). Resolved source: " + (string.IsNullOrEmpty(sourcePath) ? "<not imported>" : sourcePath));
        if (!string.Equals(AssetDatabase.AssetPathToGUID(sourcePath, AssetPathToGUIDOptions.OnlyExistingAssets), guid,
                StringComparison.OrdinalIgnoreCase) || AssetDatabase.GetTypeFromPathAndFileID(sourcePath, localId) != typeof(GameObject))
            throw new InvalidOperationException("Imported prefab GUID/fileID identity mismatch for '" + path + "': " + sourcePath +
                " (expected " + identity + ").");
        return new PrefabBinding { manifestRow = binding, path = path, entrySha256 = entryHash,
            sourcePath = sourcePath, sourceIdentity = identity };
    }

    private static bool IsProjectAssetPath(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && path.StartsWith("Assets/", StringComparison.Ordinal) &&
            !path.Contains("..") && !path.Contains("\\") && !path.Contains("\r") && !path.Contains("\n");
    }

    private static bool IsSha256(string value)
    {
        return value != null && Regex.IsMatch(value, @"\A[a-f0-9]{64}\z");
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
        // note: Validation uses the same conserved GUID/fileID resolution as building; historical keys never become filesystem fallbacks.
        var expected = ReadPrefabBindings(File.ReadAllText(BindingManifestPath))
            .ToDictionary(binding => binding.path, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new Dictionary<string, List<Dependency>>(StringComparer.OrdinalIgnoreCase);
        foreach (JObject row in rows)
        {
            string path = (string)row["path"];
            if (path == null || !seen.Add(path) || !expected.TryGetValue(path, out PrefabBinding binding) || binding.entrySha256 != (string)row["entrySha256"] ||
                AssetDatabase.GetAssetDependencyHash(binding.sourcePath).ToString() != (string)row["sourceDependencyHash"])
                throw new InvalidOperationException("Missing/stale prefab dependency row: " + path);
            if (!(row["resources"] is JArray resources) || !ReadPrefabDependencies(binding.sourcePath, files).Select(item => item.resource)
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
