using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Restore delivered texture alpha through existing imports; no race prefab, animation, shader or gameplay authority is replaced.
[InitializeOnLoad]
public static class YQDotScalpAlphaPatch
{
    private const string Root = YQDotAssetLayout.Root;
    private const string Output = "outputs/DOT_Integration_20261003/Scalp_Alpha_R1";
    private const string Request = "Assets/Assets/EditorBuildRequests/ApplyDotScalpAlphaR1.request";
    private const string Catalog = "Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset";
    private const string Layout = Root + "/Catalogs/DOT_ASSET_LAYOUT.json";
    private const string Index = Root + "/Catalogs/DOT_ASSET_INDEX.json";
    private static double _next;
    static YQDotScalpAlphaPatch() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.timeSinceStartup < _next || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        _next = EditorApplication.timeSinceStartup + .5d;
        if (!File.Exists(Request)) return;
        if (!AssetDatabase.DeleteAsset(Request)) File.Delete(Request);
        Apply();
    }

    private static void Apply()
    {
        var backups = new HashSet<string>(StringComparer.Ordinal); var created = new List<string>();
        EditorApplication.LockReloadAssemblies();
        try
        {
            var plan = JObject.Parse(File.ReadAllText(Output + "/plan.json"));
            if ((string)plan["status"] != "PREFLIGHT_PASS" || (string)JObject.Parse(File.ReadAllText(Output + "/source-contracts.json"))["status"] != "PASS") throw new InvalidDataException("Exact baseline and alpha-only preflight required.");
            var changes = ((JArray)plan["changes"]).OfType<JObject>().ToArray(); var additions = ((JArray)plan["additions"]).OfType<JObject>().ToArray();
            if (changes.Length != 41) throw new InvalidDataException("Expected forty-one replacements.");
            foreach (var row in changes)
            {
                string path = (string)row["destination"]; Confine(path);
                if (Hash(path) != (string)row["oldSha256"] || Hash((string)row["staged"]) != (string)row["newSha256"] || AssetDatabase.AssetPathToGUID(path) != (string)row["guid"]) throw new InvalidDataException("Baseline changed: " + path);
                Backup(path, backups); Backup(path + ".meta", backups);
            }
            foreach (var row in additions) { Confine((string)row["destination"]); if (File.Exists((string)row["destination"]) || Hash((string)row["staged"]) != (string)row["newSha256"]) throw new InvalidDataException("Addition changed after review."); }
            var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(Catalog);
            var paths = new HashSet<string>(changes.Where(r => (string)r["kind"] == "runtime_glb").Select(r => (string)r["destination"]), StringComparer.Ordinal);
            var entries = catalog.entries.Where(e => e.sourcePaths.Any(paths.Contains)).ToArray();
            if (entries.Length != 8 || entries.Any(e => e.kind == "module") || entries.Sum(e => e.sourcePaths.Length) != 32) throw new InvalidDataException("Expected eight existing bodies with four LODs each.");
            var identities = paths.ToDictionary(p => p, Subassets);
            var protectedFiles = entries.SelectMany(e => Directory.GetFiles(Path.GetDirectoryName(AssetDatabase.GetAssetPath(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(e.prefabPath))), "*", SearchOption.TopDirectoryOnly)).Select(p => p.Replace('\\', '/')).Distinct().ToDictionary(p => p, Hash);
            Backup(Catalog, backups); Backup(Catalog + ".meta", backups); Backup(Layout, backups); Backup(Index, backups);
            foreach (string path in protectedFiles.Keys) Backup(path, backups);
            File.WriteAllText(Output + "/import-before.json", JsonConvert.SerializeObject(identities, Formatting.Indented));
            Receipt("RUNNING", new { phase = "REIMPORTING_MATCHED_ALPHA_CORRECTION" });
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                foreach (var row in changes) File.Copy((string)row["staged"], (string)row["destination"], true);
                foreach (var row in additions) { string path = (string)row["destination"]; Directory.CreateDirectory(Path.GetDirectoryName(path)); File.Copy((string)row["staged"], path); created.Add(path); }
            }
            finally { AssetDatabase.AllowAutoRefresh(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in paths) if (JsonConvert.SerializeObject(Subassets(path)) != JsonConvert.SerializeObject(identities[path])) throw new InvalidDataException("Imported identity changed: " + path);
            File.WriteAllText(Output + "/import-after.json", JsonConvert.SerializeObject(paths.ToDictionary(p => p, Subassets), Formatting.Indented));
            var textures = paths.Select(VerifyTexture).ToArray();
            foreach (var entry in entries) entry.sourceHashes = entry.sourcePaths.Select(Hash).ToArray();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            PublishIndexes(plan, changes, additions);
            if (YQDotCreatureIntake.VerifySourcePaths() != catalog.entries.Count) throw new InvalidDataException("Corrective manifest path resolution failed.");
            foreach (var row in changes)
            {
                string path = (string)row["destination"];
                if (Hash(path) != (string)row["newSha256"] || AssetDatabase.AssetPathToGUID(path) != (string)row["guid"] || Hash(path + ".meta") != Hash(Output + "/Backup/" + path + ".meta")) throw new InvalidDataException("Payload/importer preservation failed: " + path);
                if (YQDotAssetLayout.ResolveSourceHash(path, (string)row["oldSha256"]) != (string)row["newSha256"]) throw new InvalidDataException("Original contract does not resolve the corrective hash.");
            }
            foreach (var file in protectedFiles) if (Hash(file.Key) != file.Value) throw new InvalidDataException("Existing prefab/controller/clip changed: " + file.Key);
            Receipt("RUNNING", new { phase = "RENDERING_ACTUAL_IMPORTED_LODS", textures });
            YQDotCreaturePreview.RenderEntries(entries, Output + "/Preview", true, new[] { "DotIdle", "DotStart", "DotWalk", "DotStop", "DotGreet" }, true);
            YQDotScalpAlphaReview.Render(entries, Output + "/AlphaReview");
            Receipt("PASS", new { revision = (string)plan["revision"], replacedFiles = 41, runtimeGlbs = 32, editableSources = 9, preservedPrefabControllerClipAndMetaFiles = protectedFiles.Count, textures, renderedPoses = 160, closeViews = 32, originalManifestsUnmodified = true, evidence = "Fresh Unity imports with exact GUID/subasset/meta and prefab/controller/clip preservation; GPU texture alpha readback, all body/LOD posed renders and contrasting-background head orbits. Production PlaySafe sorting, actual LOD transitions and player compatibility remain unverified." });
        }
        catch (Exception exception)
        {
            // note: Reject the operation by restoring only its backed-up files and removing its added documents; unrelated user work stays untouched.
            AssetDatabase.DisallowAutoRefresh();
            try { foreach (string path in backups) File.Copy(Output + "/Backup/" + path, path, true); foreach (string path in created) { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".meta")) File.Delete(path + ".meta"); } }
            finally { AssetDatabase.AllowAutoRefresh(); }
            YQDotAssetLayout.ReloadSourcePaths(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Receipt("FAIL_ROLLED_BACK", new { error = exception.ToString(), restoredFiles = backups.Count }); Debug.LogException(exception);
        }
        finally { EditorApplication.UnlockReloadAssemblies(); }
    }

    private static object VerifyTexture(string path)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        var texture = assets.OfType<Texture2D>().Single(t => t.name.IndexOf("CC0_ponytail", StringComparison.OrdinalIgnoreCase) >= 0 && t.name.IndexOf("albedo", StringComparison.OrdinalIgnoreCase) >= 0);
        var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true); var previous = RenderTexture.active;
        try
        {
            // note: Read actual imported GPU pixels without changing the source texture's readability/import settings.
            Graphics.Blit(texture, target); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
            var colors = pixels.GetPixels32(); int transparent = colors.Count(c => c.a == 0), opaque = colors.Count(c => c.a == 255), partial = colors.Length - transparent - opaque;
            if (transparent == 0 || partial == 0 || opaque == 0) throw new InvalidDataException("Missing restored texture alpha: " + path);
            var materials = assets.OfType<Material>().Where(m => Enumerable.Range(0, ShaderUtil.GetPropertyCount(m.shader)).Any(i => ShaderUtil.GetPropertyType(m.shader, i) == ShaderUtil.ShaderPropertyType.TexEnv && m.GetTexture(ShaderUtil.GetPropertyName(m.shader, i)) == texture)).ToArray();
            if (materials.Length == 0 || materials.Any(m => m.renderQueue < 2500 || !m.shader.isSupported)) throw new InvalidDataException("Hair material transparency contract failed: " + path);
            return new { path, texture = texture.name, texture.width, texture.height, transparent, partial, opaque, materials = materials.Select(m => new { m.name, shader = m.shader.name, m.renderQueue, keywords = m.shaderKeywords }) };
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(pixels); }
    }

    private static void PublishIndexes(JObject plan, JObject[] changes, JObject[] additions)
    {
        // note: Preserve baseline package hashes while publishing the explicitly reviewed installed revision and current inventory.
        var layout = JObject.Parse(File.ReadAllText(Layout));
        foreach (var row in changes) { var source = ((JArray)layout["files"]).OfType<JObject>().Single(f => (string)f["path"] == (string)row["destination"]); source["installedRevision"] = plan["revision"]; source["installedSha256"] = row["newSha256"]; }
        File.WriteAllText(Layout, layout.ToString(Formatting.Indented)); YQDotAssetLayout.ReloadSourcePaths(); AssetDatabase.ImportAsset(Layout, ImportAssetOptions.ForceSynchronousImport);
        var index = JObject.Parse(File.ReadAllText(Index)); var rows = (JArray)index["files"];
        foreach (string path in changes.Concat(additions).Select(r => (string)r["destination"]).Concat(new[] { Layout }))
        {
            var row = rows.OfType<JObject>().SingleOrDefault(r => (string)r["path"] == path); if (row == null) { row = new JObject(); rows.Add(row); }
            row["path"] = path; row["sha256"] = Hash(path); row["guid"] = AssetDatabase.AssetPathToGUID(path); row["category"] = path.Substring(Root.Length + 1).Split('/')[0];
        }
        index["files"] = new JArray(rows.OfType<JObject>().OrderBy(r => (string)r["path"], StringComparer.Ordinal)); index["fileCount"] = rows.Count; index["utc"] = DateTime.UtcNow;
        File.WriteAllText(Index, index.ToString(Formatting.Indented)); AssetDatabase.ImportAsset(Index, ImportAssetOptions.ForceSynchronousImport);
    }
    private static Dictionary<string, long> Subassets(string path) => AssetDatabase.LoadAllAssetsAtPath(path).Where(a => a != null).ToDictionary(a => a.GetType().FullName + "|" + a.name, a => { if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string guid, out long id)) throw new InvalidDataException("Missing imported identity."); return id; });
    private static string Hash(string path) { using (var algorithm = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    private static void Backup(string path, HashSet<string> files) { if (!File.Exists(path)) throw new FileNotFoundException(path); string target = Output + "/Backup/" + path; Directory.CreateDirectory(Path.GetDirectoryName(target)); if (File.Exists(target)) throw new InvalidOperationException("Prior backup exists; review before retry: " + path); File.Copy(path, target); files.Add(path); }
    private static void Confine(string path) { if (!path.StartsWith(Root + "/", StringComparison.Ordinal) || !Path.GetFullPath(path).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Patch destination escapes scope: " + path); }
    private static void Receipt(string status, object detail) => File.WriteAllText(Output + "/integration.json", JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, editorVersion = Application.unityVersion, detail }, Formatting.Indented));
}
