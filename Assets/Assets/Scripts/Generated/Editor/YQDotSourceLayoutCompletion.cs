using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: This explicit source-only operation finishes the installed layout without rebuilding or relocating user-arranged Unity creatures.
[InitializeOnLoad]
public static class YQDotSourceLayoutCompletion
{
    private const string Root = YQDotAssetLayout.Root;
    private const string Output = "outputs/DOT_Source_Completion_20261003";
    private const string Request = "Assets/Assets/EditorBuildRequests/CompleteDotSourceLayout.request";
    private const string Manifest = Root + "/Catalogs/DOT_ASSET_LAYOUT.json";
    private const string Index = Root + "/Catalogs/DOT_ASSET_INDEX.json";
    private const string CreatureCatalog = "Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset";
    private const string EquipmentCatalog = "Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset";
    private static double _next;

    static YQDotSourceLayoutCompletion() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.timeSinceStartup < _next || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        _next = EditorApplication.timeSinceStartup + .5d;
        if (!File.Exists(Request)) return;
        // note: Consume only the user's explicit migration request, with its sidecar, before executing the reviewed source plan.
        if (!AssetDatabase.DeleteAsset(Request)) { File.Delete(Request); if (File.Exists(Request + ".meta")) File.Delete(Request + ".meta"); }
        Complete();
    }

    [MenuItem("YourQuest/DOT Assets/Complete Source Categorization and Cleanup")]
    public static void Complete()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || YQDotEquipmentIntake.IsRunning)
            throw new InvalidOperationException("Source categorization requires stable, idle Edit Mode.");
        var completed = new List<JObject>();
        bool published = false;
        try
        {
            var plan = JObject.Parse(File.ReadAllText(Output + "/plan.json"));
            if ((string)plan["status"] != "PREFLIGHT_PASS" || ((JArray)plan["serializedCleanupReferences"]).Count != 0) throw new InvalidDataException("A reviewed, unreferenced source cleanup plan is required.");
            var rows = ((JArray)plan["rows"]).OfType<JObject>().ToArray();
            var moves = rows.Where(row => (string)row["path"] != (string)row["destination"]).ToArray();
            var mapping = moves.ToDictionary(row => (string)row["path"], row => (string)row["destination"], StringComparer.Ordinal);
            var layout = JObject.Parse(File.ReadAllText(Manifest));
            var creatures = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(CreatureCatalog);
            var equipment = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>(EquipmentCatalog);
            if (creatures == null || equipment == null) throw new InvalidDataException("Existing source catalogs are required.");
            var creatureSources = creatures.entries.ToDictionary(entry => entry, entry => entry.sourcePaths.ToArray());
            if (Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Count(file => !file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) != rows.Length) throw new InvalidDataException("Source tree changed after preflight.");
            foreach (var row in rows.Where(row => mapping.ContainsKey((string)row["path"]) || (bool)row["retire"] || (string)row["path"] == Manifest))
            {
                string source = (string)row["path"], destination = (string)row["destination"];
                Confine(source); Confine(destination);
                if (Hash(source) != (string)row["sha256"] || Hash(source + ".meta") != (string)row["metaSha256"] || source != destination && File.Exists(destination)) throw new InvalidDataException("Source revision/destination changed: " + source);
            }
            Directory.CreateDirectory(Output + "/Before");
            File.Copy(CreatureCatalog, Output + "/Before/creature-catalog.asset", true);
            File.Copy(EquipmentCatalog, Output + "/Before/equipment-catalog.asset", true);
            string[] changedDocuments = Directory.GetFiles(Root, "MATERIAL_INDEX.json", SearchOption.AllDirectories).Concat(new[] { Manifest, Index, Root + "/README.md" }).ToArray();
            var documentBackups = changedDocuments.ToDictionary(file => file, file => File.ReadAllBytes(file), StringComparer.Ordinal);
            foreach (var file in changedDocuments) File.WriteAllBytes(Output + "/Before/" + AssetDatabase.AssetPathToGUID(file) + ".json", documentBackups[file]);
            var importedBefore = moves.Where(row => ((string)row["path"]).EndsWith(".glb", StringComparison.OrdinalIgnoreCase)).ToDictionary(row => (string)row["destination"], row => Subassets((string)row["path"]));
            // note: Register destination folders before batching asset moves; folders created while asset editing is suspended are not yet valid MoveAsset parents.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var row in moves) Folder(Path.GetDirectoryName((string)row["destination"]).Replace('\\', '/'));
            Receipt("RUNNING", new { phase = "MOVING_SOURCE_FILES", files = moves.Length });
            EditorApplication.LockReloadAssemblies(); AssetDatabase.DisallowAutoRefresh(); AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var row in moves)
                {
                    string source = (string)row["path"], destination = (string)row["destination"];
                    string error = AssetDatabase.MoveAsset(source, destination);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    completed.Add(row);
                }
                var retired = ((JArray)plan["retired"]).OfType<JObject>().ToArray();
                foreach (var row in ((JArray)layout["files"]).OfType<JObject>().ToArray())
                {
                    string previous = (string)row["path"];
                    if (retired.Any(item => (string)item["path"] == previous)) { row.Remove(); continue; }
                    if (mapping.TryGetValue(previous, out string current)) { ((JArray)row["aliases"]).Add(previous); row["path"] = current; }
                }
                layout["retiredGeneratedArtifacts"] = new JArray(retired.Select(row => new JObject(row) { ["reason"] = "Superseded generated catalog; zero serialized references; complete live index replaces stale pack paths." }));
                layout["status"] = "COMPLETE"; layout["updatedUtc"] = DateTime.UtcNow;
                File.WriteAllText(Manifest, layout.ToString(Formatting.Indented)); YQDotAssetLayout.ReloadSourcePaths();
                foreach (var entry in creatures.entries) entry.sourcePaths = entry.sourcePaths.Select(YQDotAssetLayout.Resolve).ToArray();
                EditorUtility.SetDirty(creatures);
                foreach (string file in Directory.GetFiles(Root, "MATERIAL_INDEX.json", SearchOption.AllDirectories))
                {
                    var materialIndex = JObject.Parse(File.ReadAllText(file));
                    foreach (JObject material in (JArray)materialIndex["materials"]) material["sourcePath"] = YQDotAssetLayout.Resolve((string)material["sourcePath"]);
                    File.WriteAllText(file, materialIndex.ToString(Formatting.Indented));
                }
                File.WriteAllText(Root + "/README.md", "# DOT asset library\n\nNPCs are grouped by race, body type and declared modular slot. Enemies use type, species, archetype and modular slot; wildlife uses type and species. Equipment separates weapons, armor, clothing and accessories. Items contains consumables. Crafting separates harvested materials, modular parts and assembled outputs.\n\nEvery category owns its previews, editable sources, materials and documentation. Documentation separates manifests, schemas, recipes, validation, provenance, guides and supplied tools. Shared texture atlases and multi-category sources are stored once. Original embedded GLB materials and renderer assignments are preserved; Materials indexes point to the original material subassets.\n\nCatalogs/DOT_ASSET_INDEX.json lists every current payload except the index itself, including material indexes and this guide. Catalogs/DOT_ASSET_LAYOUT.json preserves exact original and prior path aliases, original hashes and GUIDs for delivered imports. Supplied contracts, licensing and validation retain their original bytes. The superseded generated index and unreferenced empty legacy folders are retired. Unity prefab folders and runtime keys are managed separately.\n");
                // note: Publish the coherent metadata revision before retiring the verified obsolete generated artifact; source assets remain recoverable on a failed move.
                published = true;
            }
            catch
            {
                if (!published)
                {
                    foreach (var row in completed.AsEnumerable().Reverse()) AssetDatabase.MoveAsset((string)row["destination"], (string)row["path"]);
                    foreach (var pair in documentBackups) File.WriteAllBytes(pair.Key, pair.Value);
                    foreach (var pair in creatureSources) pair.Key.sourcePaths = pair.Value;
                    YQDotAssetLayout.ReloadSourcePaths();
                }
                throw;
            }
            finally { AssetDatabase.StopAssetEditing(); AssetDatabase.AllowAutoRefresh(); EditorApplication.UnlockReloadAssemblies(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); AssetDatabase.SaveAssetIfDirty(creatures);
            foreach (JObject retired in (JArray)plan["retired"]) if (!AssetDatabase.DeleteAsset((string)retired["path"])) throw new IOException("Cannot retire superseded catalog: " + retired["path"]);
            var emptyFolders = ((JArray)plan["emptyFolders"]).OfType<JObject>().OrderByDescending(row => ((string)row["path"]).Length).ToArray();
            foreach (var row in emptyFolders)
            {
                string folder = (string)row["path"]; Confine(folder);
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                if (AssetDatabase.AssetPathToGUID(folder) != (string)row["guid"] || Directory.GetFileSystemEntries(folder).Length != 0) throw new InvalidDataException("Cleanup folder changed or is not empty: " + folder);
                if (!AssetDatabase.DeleteAsset(folder)) throw new IOException("Cannot retire empty folder: " + folder);
            }
            // note: Inventory all current payloads, including generated indexes and guides, rather than only the original imports.
            var currentFiles = Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Select(file => file.Replace('\\', '/')).Where(file => !file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && file != Index).OrderBy(file => file, StringComparer.Ordinal).ToArray();
            File.WriteAllText(Index, JsonConvert.SerializeObject(new { schema = "yourquest.dot-asset-index.v1", utc = DateTime.UtcNow, fileCount = currentFiles.Length, excludes = "This index itself", files = currentFiles.Select(file => new { path = file, sha256 = Hash(file), guid = AssetDatabase.AssetPathToGUID(file), category = file.Substring(Root.Length + 1).Split('/')[0] }) }, Formatting.Indented));
            AssetDatabase.ImportAsset(Index, ImportAssetOptions.ForceSynchronousImport);
            foreach (var row in rows.Where(row => !(bool)row["retire"])) if (AssetDatabase.AssetPathToGUID((string)row["destination"]) != (string)row["guid"]) throw new InvalidDataException("Source GUID changed: " + row["destination"]);
            foreach (var pair in importedBefore) if (JsonConvert.SerializeObject(Subassets(pair.Key)) != JsonConvert.SerializeObject(pair.Value)) throw new InvalidDataException("Imported source identity changed: " + pair.Key);
            if (YQDotCreatureIntake.VerifySourcePaths() == 0 || YQDotEquipmentIntake.VerifySourcePaths() == 0) throw new InvalidDataException("Empty relocated intake manifests.");
            Receipt("PASS", new { movedFiles = moves.Length, indexedPayloads = currentFiles.Length, removedEmptyFolders = emptyFolders.Length, retiredGeneratedIndexes = ((JArray)plan["retired"]).Count, originalImports = ((JArray)layout["files"]).Count, importedGlbChecks = importedBefore.Count, preservedSourceGuids = rows.Length - ((JArray)plan["retired"]).Count, editorVersion = Application.unityVersion, sourceSha256 = Hash("Assets/Assets/Scripts/Generated/Editor/YQDotSourceLayoutCompletion.cs"), evidence = "Fresh Unity editor source moves, imports, full-source GUID verification and intake manifest parsing; no prefab rebuilding or gameplay execution." });
        }
        catch (Exception exception) { Receipt("FAIL", new { error = exception.ToString(), published, completed = completed.Count }); Debug.LogException(exception); }
    }

    private static void Folder(string folder)
    {
        Confine(folder);
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        if (parent != Root) Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
    private static void Confine(string file)
    {
        if (!Path.GetFullPath(file).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Source operation escapes DOT root: " + file);
    }
    private static SortedDictionary<string, long> Subassets(string file)
    {
        var result = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(file)) if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string assetGuid, out long id)) result.Add(asset.GetType().FullName + "|" + asset.name + "|" + id, id);
        return result;
    }
    private static string Hash(string file)
    {
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static void Receipt(string status, object detail) => File.WriteAllText(Output + "/editor-verification.json", JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, detail }, Formatting.Indented));
}
