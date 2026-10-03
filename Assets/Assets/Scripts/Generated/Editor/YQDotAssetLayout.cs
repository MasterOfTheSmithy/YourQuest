using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Source locations are presentation metadata. Exact relocation aliases retain the delivered manifests and existing prefab, registry and save identities.
[InitializeOnLoad]
public static class YQDotAssetLayout
{
    internal const string Root = "Assets/Assets/GeneratedAssets/DOT Generated Assets";
    private const string Manifest = Root + "/Catalogs/DOT_ASSET_LAYOUT.json";
    private const string Output = "outputs/DOT_Asset_Layout_20261003";
    private const string Request = "Assets/Assets/EditorBuildRequests/OrganizeDotAssetTypes.request";
    private const string GroupRequest = "Assets/Assets/EditorBuildRequests/GroupDotCreatureTypes.request";
    private static Dictionary<string, string> _paths;
    private static Dictionary<string, string> _origins;
    private static double _next;

    static YQDotAssetLayout() { EditorApplication.update += Tick; }

    public static string Resolve(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        string normalized = Relative(path);
        LoadPaths();
        return _paths.TryGetValue(normalized, out var relocated) ? relocated : normalized;
    }

    public static bool IsFromPack(string path, string originalRoot)
    {
        LoadPaths();
        string normalized = Relative(path);
        if (_origins.TryGetValue(normalized, out var origin)) normalized = origin;
        return normalized.StartsWith(Relative(originalRoot) + "/", StringComparison.Ordinal);
    }

    private static string Relative(string path)
    {
        string full = Path.GetFullPath(path);
        string project = Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(project, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("DOT path escapes project: " + path);
        return full.Substring(project.Length).Replace('\\', '/');
    }

    private static void LoadPaths()
    {
        if (_paths != null) return;
        _paths = new Dictionary<string, string>(StringComparer.Ordinal);
        _origins = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(Manifest)) return;
        foreach (JObject row in (JArray)JObject.Parse(File.ReadAllText(Manifest))["files"])
        {
            string destination = (string)row["path"], origin = (string)row["origin"];
            Confine(destination);
            _paths.Add(origin, destination);
            _origins.Add(destination, origin);
            foreach (string alias in row["aliases"].Values<string>()) _paths.Add(alias, destination);
        }
    }

    private static void Tick()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.timeSinceStartup < _next || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        _next = EditorApplication.timeSinceStartup + .5d;
        if (!File.Exists(Request) && !File.Exists(GroupRequest)) return;
        // note: Consume only this explicit organization request by archiving it outside Assets; other editor operations remain untouched.
        Directory.CreateDirectory(Output);
        bool group = File.Exists(GroupRequest);
        string request = group ? GroupRequest : Request;
        string archivedRequest = Output + "/executed-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff") + ".request";
        File.Move(request, archivedRequest);
        if (File.Exists(request + ".meta")) File.Move(request + ".meta", archivedRequest + ".meta");
        if (group) GroupCreatureTypes(); else Organize();
    }

    [MenuItem("YourQuest/DOT Assets/Group Wildlife and Enemies by Type")]
    public static void GroupCreatureTypes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || YQDotEquipmentIntake.IsRunning)
            throw new InvalidOperationException("Creature grouping requires stable, idle Edit Mode.");
        // note: Folder moves change presentation taxonomy only. Species IDs, declared enemy categories, prefab keys and mechanical contracts remain unchanged.
        var moves = new Dictionary<string, string>(StringComparer.Ordinal) {
            [Root + "/Enemies/Cairnback"] = Root + "/Enemies/Rock Monsters/Cairnback",
            [Root + "/Enemies/Thornweaver"] = Root + "/Enemies/Spiders/Thornweaver",
            [Root + "/Enemies/Sporewarden"] = Root + "/Enemies/Mushroom Monsters/Sporewarden",
            [Root + "/Enemies/Maw"] = Root + "/Enemies/Worm Monsters/Maw",
            [Root + "/Enemies/Mimic"] = Root + "/Enemies/Mimics/Mimic",
            [Root + "/Wildlife/Cow"] = Root + "/Wildlife/Livestock/Cow",
            [Root + "/Wildlife/Horse"] = Root + "/Wildlife/Livestock/Horse",
            [Root + "/Wildlife/Rabbit"] = Root + "/Wildlife/Woodland Animals/Rabbit",
            [Root + "/Wildlife/Stag"] = Root + "/Wildlife/Woodland Animals/Stag"
        };
        var completed = new List<KeyValuePair<string, string>>();
        bool published = false;
        Directory.CreateDirectory(Output);
        try
        {
            var layout = JObject.Parse(File.ReadAllText(Manifest));
            var creatures = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>("Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset");
            var equipment = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>("Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset");
            File.Copy(AssetDatabase.GetAssetPath(creatures), Output + "/creature-catalog-before.asset", true);
            File.Copy(AssetDatabase.GetAssetPath(equipment), Output + "/equipment-catalog-before.asset", true);
            var folderGuids = moves.ToDictionary(m => m.Key, m => AssetDatabase.AssetPathToGUID(m.Key));
            var samples = creatures.entries.Where(e => e.kind != "module").GroupBy(e => e.species).Select(g => g.First()).ToArray();
            var beforeSubassets = samples.ToDictionary(e => e.assetId, e => Subassets(e.sourcePaths[0]));
            foreach (var move in moves)
            {
                Confine(move.Key); Confine(move.Value);
                if (!AssetDatabase.IsValidFolder(move.Key) || AssetDatabase.IsValidFolder(move.Value)) throw new InvalidDataException("Creature grouping source/destination changed: " + move.Key);
                string parent = Path.GetDirectoryName(move.Value).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), Path.GetFileName(parent));
            }
            WriteReceipt("RUNNING", new { phase = "GROUPING_CREATURE_TYPES", folders = moves.Count });
            EditorApplication.LockReloadAssemblies(); AssetDatabase.DisallowAutoRefresh(); AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var move in moves)
                {
                    string error = AssetDatabase.MoveAsset(move.Key, move.Value);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    completed.Add(move);
                }
                foreach (JObject row in (JArray)layout["files"])
                {
                    string previous = (string)row["path"], current = GroupPath(previous, moves);
                    if (previous != current) { ((JArray)row["aliases"]).Add(previous); row["path"] = current; }
                    row["materialFolder"] = GroupPath((string)row["materialFolder"], moves);
                }
                File.WriteAllText(Manifest, layout.ToString(Formatting.Indented));
                _paths = null; _origins = null;
                foreach (var entry in creatures.entries) entry.sourcePaths = entry.sourcePaths.Select(p => GroupPath(p, moves)).ToArray();
                foreach (string file in Directory.GetFiles(Root, "MATERIAL_INDEX.json", SearchOption.AllDirectories))
                {
                    var index = JObject.Parse(File.ReadAllText(file));
                    bool changed = false;
                    foreach (JObject material in (JArray)index["materials"])
                    {
                        string previous = (string)material["sourcePath"], current = GroupPath(previous, moves);
                        if (previous != current) { material["sourcePath"] = current; changed = true; }
                    }
                    if (changed) File.WriteAllText(file, index.ToString(Formatting.Indented));
                }
                var sourceIndex = JObject.Parse(File.ReadAllText(Root + "/Catalogs/DOT_ASSET_INDEX.json"));
                foreach (JObject file in (JArray)sourceIndex["files"]) file["path"] = GroupPath((string)file["path"], moves);
                File.WriteAllText(Root + "/Catalogs/DOT_ASSET_INDEX.json", sourceIndex.ToString(Formatting.Indented));
                string readme = Root + "/README.md";
                File.AppendAllText(readme, "\nEnemies are grouped by their declared rock monster, spider, mushroom monster, mimic and worm monster types, then species. Wildlife is grouped into Livestock (Cow, Horse) and Woodland Animals (Rabbit, Stag), then species. Shared resources remain stored once.\n");
                EditorUtility.SetDirty(creatures); published = true;
            }
            catch
            {
                if (!published) foreach (var move in completed.AsEnumerable().Reverse()) AssetDatabase.MoveAsset(move.Value, move.Key);
                throw;
            }
            finally { AssetDatabase.StopAssetEditing(); AssetDatabase.AllowAutoRefresh(); EditorApplication.UnlockReloadAssemblies(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); AssetDatabase.SaveAssets();
            foreach (var move in moves) if (AssetDatabase.AssetPathToGUID(move.Value) != folderGuids[move.Key]) throw new InvalidDataException("Creature folder GUID changed: " + move.Value);
            foreach (JObject row in (JArray)layout["files"])
                if (AssetDatabase.AssetPathToGUID((string)row["path"]) != (string)row["guid"]) throw new InvalidDataException("Source GUID changed: " + row["path"]);
            foreach (var sample in samples)
                if (JsonConvert.SerializeObject(Subassets(sample.sourcePaths[0])) != JsonConvert.SerializeObject(beforeSubassets[sample.assetId])) throw new InvalidDataException("Creature subasset identity changed: " + sample.assetId);
            VerifyIntakePaths();
            WriteReceipt("PASS", new { files = ((JArray)layout["files"]).Count, creatureBindings = creatures.entries.Count, equipmentBindings = equipment.entries.Count, preservedCreatureFolderGuids = moves.Count, importedSamples = samples.Length, editorVersion = Application.unityVersion, sourceSha256 = HashFile("Assets/Assets/Scripts/Generated/Editor/YQDotAssetLayout.cs"), evidence = "Fresh Editor folder moves, all source GUIDs, creature imported identities and intake path parsing. Species/mechanical contracts unchanged; gameplay was not exercised." });
        }
        catch (Exception exception) { WriteReceipt("FAIL", new { error = exception.ToString(), published, completed }); Debug.LogException(exception); }
    }

    private static string GroupPath(string path, Dictionary<string, string> moves)
    {
        foreach (var move in moves) if (path == move.Key || path.StartsWith(move.Key + "/", StringComparison.Ordinal)) return move.Value + path.Substring(move.Key.Length);
        return path;
    }

    [MenuItem("YourQuest/DOT Assets/Organize Source Assets by Type")]
    public static void Organize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || YQDotEquipmentIntake.IsRunning)
            throw new InvalidOperationException("Organization requires stable, idle Edit Mode.");
        Directory.CreateDirectory(Output);
        var stagedFolders = new List<string>();
        var movedFiles = new List<JObject>();
        var archivedFolders = new List<string>();
        bool published = false;
        try
        {
            var plan = JObject.Parse(File.ReadAllText(Output + "/plan.json"));
            if ((string)plan["status"] != "PREFLIGHT_PASS" || File.Exists(Manifest)) throw new InvalidDataException("A fresh, unapplied layout plan is required.");
            var files = ((JArray)plan["files"]).OfType<JObject>().ToArray();
            string[] topFolders = plan["topFolders"].Values<string>().ToArray();
            var creatures = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>("Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset");
            var equipment = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>("Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset");
            if (creatures == null || equipment == null) throw new InvalidDataException("Existing DOT catalogs are required.");
            var wrappers = creatures.entries.Select(e => e.prefabPath).Concat(equipment.entries.Select(e => e.prefabPath)).Distinct().ToArray();
            var protectedFiles = wrappers.SelectMany(p => new[] { p, p + ".meta" }).Concat(new[] { "Assets/Assets/Resources/GeneratedRpgContentLibrary.asset" }).ToDictionary(p => p, HashFile);
            var samples = files.Where(f => ((string)f["path"]).EndsWith(".glb", StringComparison.Ordinal)).GroupBy(f => (string)f["materialFolder"]).Select(g => g.First()).ToArray();
            var subassets = samples.ToDictionary(f => (string)f["origin"], f => Subassets((string)f["origin"]));
            int fileCount = Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Count(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
            if (fileCount != files.Length) throw new InvalidDataException("DOT content changed after preflight; rebuild the plan.");
            foreach (var row in files)
            {
                string origin = (string)row["origin"], destination = (string)row["path"];
                Confine(origin); Confine(destination);
                if (File.Exists(destination) || !File.Exists(origin + ".meta") || HashFile(origin) != (string)row["sha256"] || HashFile(origin + ".meta") != (string)row["metaSha256"])
                    throw new InvalidDataException("Source revision or destination changed: " + origin);
            }
            File.WriteAllText(Output + "/protected-files.json", JsonConvert.SerializeObject(protectedFiles, Formatting.Indented));
            File.Copy(AssetDatabase.GetAssetPath(creatures), Output + "/creature-catalog-before.asset", true);
            File.Copy(AssetDatabase.GetAssetPath(equipment), Output + "/equipment-catalog-before.asset", true);
            WriteReceipt("RUNNING", new { phase = "MOVING_FILES", files = files.Length });
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.DisallowAutoRefresh();
            AssetDatabase.StartAssetEditing();
            try
            {
                // note: Stage each complete source tree with its sidecar before distributing assets. Unity observes only the final GUID-preserving layout on refresh.
                Directory.CreateDirectory(Output + "/OriginalTree");
                foreach (string top in topFolders)
                {
                    MoveFolder(Root + "/" + top, Output + "/OriginalTree/" + top);
                    stagedFolders.Add(top);
                    if (File.Exists(Root + "/" + top + ".meta")) File.Move(Root + "/" + top + ".meta", Output + "/OriginalTree/" + top + ".meta");
                }
                foreach (var row in files)
                {
                    string staged = Output + "/OriginalTree/" + ((string)row["origin"]).Substring(Root.Length + 1), destination = (string)row["path"];
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Move(staged, destination);
                    movedFiles.Add(row);
                    File.Move(staged + ".meta", destination + ".meta");
                }
                // note: Preserve all existing folder GUIDs in a provenance area instead of deleting their sidecars while creating the new semantic folders.
                Directory.CreateDirectory(Root + "/Documentation/Source Folder Metadata");
                foreach (string top in topFolders)
                {
                    string archived = Root + "/Documentation/Source Folder Metadata/" + top;
                    MoveFolder(Output + "/OriginalTree/" + top, archived);
                    archivedFolders.Add(top);
                    if (File.Exists(Output + "/OriginalTree/" + top + ".meta")) File.Move(Output + "/OriginalTree/" + top + ".meta", archived + ".meta");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Manifest));
                File.WriteAllText(Manifest, plan.ToString(Formatting.Indented));
                WriteMaterialIndexes(files);
                File.WriteAllText(Root + "/Catalogs/DOT_ASSET_INDEX.json", JsonConvert.SerializeObject(new {
                    schema = "yourquest.dot-asset-index.v1", utc = DateTime.UtcNow, fileCount = files.Length,
                    categories = new[] { "NPCs", "Enemies", "Wildlife", "Equipment", "Items", "Crafting", "Documentation", "Catalogs" },
                    files = files.Select(f => new { path = (string)f["path"], sha256 = (string)f["sha256"], guid = (string)f["guid"], category = ((string)f["path"]).Substring(Root.Length + 1).Split('/')[0], kind = (string)f["kind"] })
                }, Formatting.Indented));
                File.WriteAllText(Root + "/README.md", "# DOT asset library\n\nNPCs are separated by race and body type; enemies by species, archetype and modular slot; wildlife by species; crafting by harvested material and output type. Equipment separates weapons, armor, clothing and accessories; Items contains consumables. Library, Modular and Revised exports retain their separate identities.\n\nEach Materials folder indexes the original embedded GLB materials. Separate texture atlases and source material definitions are stored with their owner; shared atlases are stored once under Shared. Material assignments and imported subasset identities are preserved.\n\nCatalogs/DOT_ASSET_LAYOUT.json maps every original and historical source path to its current file with its original SHA-256 and Unity GUID. Delivered manifests and supplied reference scripts retain their original bytes and pack-relative notation. Project intake and verification resolve those paths through YQDotAssetLayout. Unreferenced empty source folders are retired during the verified cleanup, with their GUIDs retained in the layout record.\n");
                _paths = null; _origins = null;
                foreach (var entry in creatures.entries) entry.sourcePaths = entry.sourcePaths.Select(Resolve).ToArray();
                foreach (var entry in equipment.entries)
                {
                    entry.sourcePath = Resolve(entry.sourcePath);
                    if (!string.IsNullOrEmpty(entry.partsSourcePath))
                    {
                        string parts = entry.partsSourcePath.StartsWith("Assets/", StringComparison.Ordinal) ? entry.partsSourcePath : Root + "/Equipment/Library/" + entry.partsSourcePath;
                        string resolved = Resolve(parts);
                        if (File.Exists(resolved)) entry.partsSourcePath = resolved;
                    }
                }
                EditorUtility.SetDirty(creatures); EditorUtility.SetDirty(equipment);
                published = true;
            }
            catch
            {
                // note: Reverse only completed file moves. Retain a recoverable staged tree and journal if a filesystem failure interrupts the transaction.
                foreach (string top in archivedFolders.AsEnumerable().Reverse())
                {
                    string archived = Root + "/Documentation/Source Folder Metadata/" + top;
                    MoveFolder(archived, Output + "/OriginalTree/" + top);
                    if (File.Exists(archived + ".meta")) File.Move(archived + ".meta", Output + "/OriginalTree/" + top + ".meta");
                }
                foreach (var row in movedFiles.AsEnumerable().Reverse())
                {
                    string destination = (string)row["path"], staged = Output + "/OriginalTree/" + ((string)row["origin"]).Substring(Root.Length + 1);
                    File.Move(destination, staged);
                    if (File.Exists(destination + ".meta")) File.Move(destination + ".meta", staged + ".meta");
                }
                throw;
            }
            finally { AssetDatabase.StopAssetEditing(); AssetDatabase.AllowAutoRefresh(); EditorApplication.UnlockReloadAssemblies(); }
            WriteReceipt("RUNNING", new { phase = "VERIFYING_IMPORTS", files = files.Length });
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            foreach (var pair in protectedFiles) if (HashFile(pair.Key) != pair.Value) throw new InvalidDataException("Protected binding changed: " + pair.Key);
            foreach (var row in files)
                if (AssetDatabase.AssetPathToGUID((string)row["path"]) != (string)row["guid"]) throw new InvalidDataException("Moved asset GUID changed: " + row["path"]);
            foreach (var sample in samples)
                if (JsonConvert.SerializeObject(Subassets((string)sample["path"])) != JsonConvert.SerializeObject(subassets[(string)sample["origin"]])) throw new InvalidDataException("Imported subasset identities changed: " + sample["path"]);
            VerifyIntakePaths();
            WriteReceipt("PASS", new { files = files.Length, creatureBindings = creatures.entries.Count, equipmentBindings = equipment.entries.Count, protectedFiles = protectedFiles.Count, importedSamples = samples.Length, editorVersion = Application.unityVersion, sourceSha256 = HashFile("Assets/Assets/Scripts/Generated/Editor/YQDotAssetLayout.cs"), evidence = "Fresh Editor execution, GUID preservation, representative imported subasset identity and declared intake-path verification. Gameplay was not exercised." });
        }
        catch (Exception exception)
        {
            WriteReceipt("FAIL", new { error = exception.ToString(), published, stagedFolders, completedFiles = movedFiles.Select(f => (string)f["path"]).ToArray() });
            Debug.LogException(exception);
        }
    }

    private static void VerifyIntakePaths()
    {
        // note: Exercise manifest parsing and confined source resolution without rebuilding wrappers, registries, pools or gameplay state.
        if (YQDotCreatureIntake.VerifySourcePaths() == 0 || YQDotEquipmentIntake.VerifySourcePaths() == 0) throw new InvalidDataException("Empty relocated intake.");
    }

    private static void WriteMaterialIndexes(IEnumerable<JObject> files)
    {
        foreach (var group in files.Where(f => f["embeddedMaterials"].Any()).GroupBy(f => (string)f["materialFolder"]))
        {
            Confine(group.Key); Directory.CreateDirectory(group.Key);
            File.WriteAllText(group.Key + "/MATERIAL_INDEX.json", JsonConvert.SerializeObject(new { storage = "Original GLB subassets; renderer assignments preserved", materials = group.Select(f => new { sourcePath = (string)f["path"], sourceGuid = (string)f["guid"], embedded = f["embeddedMaterials"] }) }, Formatting.Indented));
        }
    }

    private static void MoveFolder(string source, string destination)
    {
        // note: Check both absolute targets before moving a complete tree; staged and final locations must remain in this task's workspace boundaries.
        foreach (string candidate in new[] { source, destination })
        {
            string full = Path.GetFullPath(candidate);
            bool asset = full.StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            bool staging = full.StartsWith(Path.GetFullPath(Output) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (!asset && !staging) throw new InvalidDataException("Folder move escapes the DOT workspace: " + candidate);
        }
        Directory.Move(source, destination);
    }

    private static SortedDictionary<string, long> Subassets(string path)
    {
        var result = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id)) result.Add(asset.GetType().FullName + "|" + asset.name + "|" + id, id);
        return result;
    }

    private static string HashFile(string path)
    {
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static void Confine(string path)
    {
        if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Operation escapes DOT root: " + path);
    }

    private static void WriteReceipt(string status, object detail) => File.WriteAllText(Output + "/editor-verification.json", JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, detail }, Formatting.Indented));
}
