#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Additive serialization of the existing registry bindings; vendor assets, legacy shards, GUIDs, and accepted paths remain unchanged.
[InitializeOnLoad]
public static class YQGranularWorldAssetResources
{
    private const string ResourcesFolder = "Assets/Assets/Resources";
    private const string RootPath = ResourcesFolder + "/YQRuntimeWorldAssetRegistry.asset";
    private const string ManifestPath = ResourcesFolder + "/YQWorldAssetManifest.json";
    private const string Request = "Assets/Assets/EditorBuildRequests/YQ_GRANULAR_ASSET_RESOURCES.request";
    private const string ReportFolder = "outputs/UnityMemoryRepair_20261008";
    private const string Owner = "YourQuest.GranularWorldAssetResources.v1";
    private static double nextCheck;

    private sealed class Leaf
    {
        public string path, resource, source, entrySha256, leafSha256;
        [JsonIgnore] public string text;
    }

    static YQGranularWorldAssetResources() { EditorApplication.update += Tick; }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        bool fresh = File.GetLastWriteTimeUtc(Request) > DateTime.UtcNow.AddMinutes(-2);
        File.Delete(Request);
        if (!fresh) return;
        try { RebuildFromSerializedShards(); }
        catch (Exception error)
        {
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(ReportFolder + "/BuildFailure.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    private static string Hash(string value)
    {
        using (SHA256 hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
    }

    [MenuItem("YourQuest/Assets/Build Individual Runtime Asset Bindings (Preserve Legacy)")]
    public static void RebuildFromSerializedShards()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build individual asset bindings outside Play.");
        var root = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(RootPath);
        if (root == null || !root.UsesLazyResourceShards)
            throw new InvalidOperationException("The existing lazy registry must be available before building individual bindings.");
        Directory.CreateDirectory(ReportFolder);
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var leaves = new Dictionary<string, Leaf>(StringComparer.OrdinalIgnoreCase);
        var oldLeaves = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(ManifestPath))
        {
            JObject previous = JObject.Parse(File.ReadAllText(ManifestPath));
            if ((string)previous["generator"] != Owner) throw new InvalidOperationException("Existing manifest is not owned by this builder.");
            foreach (JToken item in previous["assets"])
                oldLeaves.Add((string)item["resource"], (string)item["leafSha256"]);
        }
        // note: Read serialized references as text so rebuilding the index cannot load thousands of vendor meshes/textures into the Editor.
        foreach (string source in Directory.GetFiles(ResourcesFolder + "/YQWorldAssetShards", "*.asset").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string raw = File.ReadAllText(source).Replace("\r\n", "\n");
            sources.Add(source, Hash(raw));
            MatchCollection starts = Regex.Matches(raw, @"(?m)^  - assetPath: (.+(?:\n {6}.+)*)\n");
            if (starts.Count == 0) continue;
            int entriesStart = raw.IndexOf("  entries:\n", StringComparison.Ordinal);
            Match end = Regex.Match(raw, @"(?m)^  useLazyResourceShards:");
            if (entriesStart < 0 || !end.Success || end.Index < starts[starts.Count-1].Index)
                throw new InvalidOperationException("Unsupported registry serialization: " + source);
            string header = raw.Substring(0, entriesStart) + "  entries:\n";
            for (int index = 0; index < starts.Count; index++)
            {
                // note: Unity folds long plain YAML scalars onto six-space continuation lines; retain that exact path while copying the serialized block untouched.
                string path = Regex.Replace(starts[index].Groups[1].Value, @"\n {6}", " ");
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") || path.Contains("\r"))
                    throw new InvalidOperationException("Unsupported authored asset path in " + source);
                int stop = index + 1 < starts.Count ? starts[index+1].Index : end.Index;
                string entry = raw.Substring(starts[index].Index, stop - starts[index].Index);
                string key = Hash(YQRuntimeWorldAssetRegistry.NormalizePath(path).ToLowerInvariant());
                string name = "YQWorldAsset_" + key;
                string leafHeader = Regex.Replace(header, @"(?m)^  m_Name: .+$", "  m_Name: " + name);
                string text = leafHeader + entry + "  useLazyResourceShards: 0\n  useGranularResourceShards: 0\n  terrainGrassTexture: {fileID: 0}\n";
                var leaf = new Leaf { path = path, resource = "YQWorldAssetLeaves/" + key, source = source.Replace('\\','/'),
                    entrySha256 = Hash(entry), leafSha256 = Hash(text), text = text };
                if (leaves.ContainsKey(path)) throw new InvalidOperationException("Duplicate canonical asset binding: " + path);
                leaves.Add(path, leaf);
                string destination = ResourcesFolder + "/" + leaf.resource + ".asset";
                if (File.Exists(destination))
                {
                    string existing = Hash(File.ReadAllText(destination).Replace("\r\n", "\n"));
                    if (existing != leaf.leafSha256 && (!oldLeaves.TryGetValue(leaf.resource, out string acceptedHash) || acceptedHash != existing))
                        throw new InvalidOperationException("Preserve a modified or unowned binding: " + destination);
                }
            }
        }
        if (leaves.Count == 0) throw new InvalidOperationException("No serialized asset bindings found.");
        // note: Preflight all source and destination identities before writing; existing source/asset GUIDs are never rewritten or deleted.
        foreach (var source in sources)
            if (Hash(File.ReadAllText(source.Key).Replace("\r\n", "\n")) != source.Value)
                throw new InvalidOperationException("Registry changed during build: " + source.Key);
        File.WriteAllText(ReportFolder + "/RootBeforeGranular.asset.txt", File.ReadAllText(RootPath));
        int written = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            Directory.CreateDirectory(ResourcesFolder + "/YQWorldAssetLeaves");
            foreach (Leaf leaf in leaves.Values)
            {
                string destination = ResourcesFolder + "/" + leaf.resource + ".asset";
                if (!File.Exists(destination) || Hash(File.ReadAllText(destination).Replace("\r\n", "\n")) != leaf.leafSha256)
                {
                    File.WriteAllText(destination, leaf.text, new UTF8Encoding(false));
                    written++;
                }
                if (!File.Exists(destination + ".meta"))
                    File.WriteAllText(destination + ".meta", "fileFormatVersion: 2\nguid: " + Guid.NewGuid().ToString("N") + "\n", new UTF8Encoding(false));
            }
            var manifest = new { version = 1, generator = Owner, assets = leaves.Values.OrderBy(item => item.path, StringComparer.OrdinalIgnoreCase).ToArray() };
            File.WriteAllText(ManifestPath, JsonConvert.SerializeObject(manifest, Formatting.Indented), new UTF8Encoding(false));
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        // note: Activate only after every leaf is published and every original binding block is conserved byte-for-byte (apart from line endings).
        foreach (Leaf leaf in leaves.Values)
            if (Hash(File.ReadAllText(ResourcesFolder + "/" + leaf.resource + ".asset").Replace("\r\n", "\n")) != leaf.leafSha256)
                throw new InvalidOperationException("Published leaf verification failed: " + leaf.path);
        foreach (var source in sources)
            if (Hash(File.ReadAllText(source.Key).Replace("\r\n", "\n")) != source.Value)
                throw new InvalidOperationException("Legacy source preservation failed: " + source.Key);
        root.SetGranularResourceShards(true);
        EditorUtility.SetDirty(root);
        AssetDatabase.SaveAssetIfDirty(root);
        YQRuntimeWorldAssetRegistry.ClearCachedInstance();
        File.WriteAllText(ReportFolder + "/GranularBuildReceipt.json", JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow.ToString("O"), sourceShards = sources, entryCount = leaves.Count, filesWritten = written,
            legacySourcesPreserved = true, serializedBindingsPreserved = true, rootGuid = AssetDatabase.AssetPathToGUID(RootPath),
            manifestSha256 = Hash(File.ReadAllText(ManifestPath)), evidence = "Serialized binding conservation and Editor import; gameplay appearance still requires runtime validation."
        }, Formatting.Indented));
        Debug.Log("[YQGranularWorldAssetResources] Preserved " + leaves.Count + " bindings from " + sources.Count + " source shards; wrote " + written + " individual bindings.");
    }
}
#endif
