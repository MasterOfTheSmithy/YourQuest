using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Explicit patch/organization operations preserve Unity GUIDs, approved wrapper keys, original pack-relative contracts and canonical saves.
[InitializeOnLoad]
public static class YQDotAssetMaintenance
{
    private const string Root = "Assets/Assets/GeneratedAssets/DOT Generated Assets";
    private const string Output = "outputs/DOT_Integration_20261003/Patches";
    private const string Request = "Assets/Assets/EditorBuildRequests/MaintainDotAssets.request";
    private const string AdditionalRequest = "Assets/Assets/EditorBuildRequests/IntegrateAdditionalDotAssets.request";
    private const string VerifyRequest = "Assets/Assets/EditorBuildRequests/VerifyDotPatches.request";
    private const string CreatureCatalogPath = "Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset";
    private const string EquipmentCatalogPath = "Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset";
    private static readonly KeyValuePair<string, string>[] Moves = {
        new KeyValuePair<string, string>(Root + "/Races", Root + "/NPCs/Races"),
        new KeyValuePair<string, string>(Root + "/Enemies", Root + "/Monsters"),
        new KeyValuePair<string, string>(Root + "/DOT Gen Modular Expansion", Root + "/Equipment/Modular"),
        new KeyValuePair<string, string>(Root + "/YourQuest_Complete_Rebuilt_GLB_Library_2026-10-02", Root + "/Equipment/Library")
    };
    private static double _next;
    private sealed class FileRecord { public string path, sha256, guid, category, kind; }

    static YQDotAssetMaintenance() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.timeSinceStartup < _next || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        _next = EditorApplication.timeSinceStartup + .5d;
        if (File.Exists(VerifyRequest))
        {
            // note: Verify published bindings after a source-only layout change without rebuilding wrappers, rewriting indexes or reapplying any patch.
            if (!AssetDatabase.DeleteAsset(VerifyRequest)) File.Delete(VerifyRequest);
            try { YQDotPatchVerification.Run(AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(CreatureCatalogPath), AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>(EquipmentCatalogPath)); }
            catch (Exception exception) { Debug.LogException(exception); File.WriteAllText(Output + "/focused-verification.json", JsonConvert.SerializeObject(new { status = "FAIL", utc = DateTime.UtcNow, error = exception.ToString() }, Formatting.Indented)); }
            return;
        }
        if (File.Exists(AdditionalRequest))
        { if (!AssetDatabase.DeleteAsset(AdditionalRequest)) File.Delete(AdditionalRequest); RunAdditional(); return; }
        if (!File.Exists(Request)) return;
        if (!AssetDatabase.DeleteAsset(Request)) { File.Delete(Request); if (File.Exists(Request + ".meta")) File.Delete(Request + ".meta"); }
        Run();
    }

    private static void RunAdditional()
    {
        // note: A later user delivery is a separate bounded intake, avoiding reapplying successful wildlife patches or repeating the folder migration.
        EditorApplication.LockReloadAssemblies();
        try
        {
            WriteProgress("INTEGRATING_SATYR_AND_ARMOR_COVERAGE");
            var creatures = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(CreatureCatalogPath);
            var equipment = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>(EquipmentCatalogPath);
            string library = "Assets/Assets/Resources/GeneratedRpgContentLibrary.asset", libraryHash = HashFile(library);
            var previous = creatures.entries.Select(e => e.prefabPath).Concat(equipment.entries.Select(e => e.prefabPath)).Distinct().ToDictionary(p => p, AssetDatabase.AssetPathToGUID);
            if (creatures.entries.Count(e => e.species == "satyr") != 28) YQDotCreatureIntake.BuildSatyrs();
            if (equipment.entries.Count(e => e.sourcePath.StartsWith(YQDotEquipmentIntake.CoverageRoot + "/", StringComparison.Ordinal)) != 16) YQDotEquipmentIntake.BuildArmorCoverage();
            foreach (var pair in previous) if (AssetDatabase.AssetPathToGUID(pair.Key) != pair.Value) throw new InvalidDataException("Prior prefab GUID changed: " + pair.Key);
            if (HashFile(library) != libraryHash) throw new InvalidDataException("Prior production equipment pools changed.");
            YQDotPatchVerification.Run(creatures, equipment);
            string indexPath = Root + "/Catalogs/DOT_ASSET_INDEX.json";
            var index = JObject.Parse(File.ReadAllText(indexPath));
            var old = ((JArray)index["files"]).OfType<JObject>().ToDictionary(f => (string)f["path"]);
            var files = Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && p.Replace('\\', '/') != indexPath).Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            index["files"] = new JArray(files.Select(p => old.TryGetValue(p, out var row) ? row : JObject.FromObject(Record(p))));
            index["fileCount"] = files.Length; index["utc"] = DateTime.UtcNow;
            File.WriteAllText(indexPath, index.ToString(Formatting.Indented)); AssetDatabase.ImportAsset(indexPath, ImportAssetOptions.ForceSynchronousImport);
            var baseline = JsonConvert.DeserializeObject<FileRecord[]>(File.ReadAllText(Output + "/organization-before.json"));
            foreach (var record in baseline)
                if (HashFile(Map(record.path)) != record.sha256 || AssetDatabase.AssetPathToGUID(Map(record.path)) != record.guid) throw new InvalidDataException("Original source bytes/GUID changed: " + record.path);
            File.WriteAllText(Output + "/maintenance.json", JsonConvert.SerializeObject(new { status = "PASS", utc = DateTime.UtcNow,
                patchFiles = ((JArray)JObject.Parse(File.ReadAllText(Output + "/plan-next.json"))["changes"]).Count,
                categorizedFiles = files.Length, preservedImportedFiles = baseline.Length, preservedExistingWrapperGuids = previous.Count,
                productionItemPoolsUnchanged = true, cowMaterialMigration = Output + "/cow-reference-migration.json", moves = Moves,
                evidence = "Unity import/move operations, original source byte/GUID audit, catalog references, evaluated skin/clip/material/fit fixtures and rendered poses. Ordinary PlaySafe contact and fitted player outfit remain unverified."
            }, Formatting.Indented));
            File.WriteAllText(Output + "/additional-integration.json", JsonConvert.SerializeObject(new { status = "PASS", utc = DateTime.UtcNow, satyrEntries = 28, armorCoverageEntries = 16, preservedPriorPrefabGuids = previous.Count, indexedSourceFiles = files.Length,
                evidence = "Editor import/binding/skin/material/fit and rendered-pose fixtures. Ordinary PlaySafe motion and fitted player outfit remain unverified." }, Formatting.Indented));
        }
        catch (Exception exception)
        { Debug.LogException(exception); File.WriteAllText(Output + "/additional-integration.json", JsonConvert.SerializeObject(new { status = "FAIL", utc = DateTime.UtcNow, error = exception.ToString() }, Formatting.Indented)); }
        finally { EditorApplication.UnlockReloadAssemblies(); }
    }

    [MenuItem("YourQuest/DOT Assets/Apply Reviewed Patches and Organize All Files")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Asset maintenance requires idle Edit Mode.");
        Directory.CreateDirectory(Output); string error = null;
        // note: Synchronous imports and folder moves may queue a domain reload; finish preservation checks before permitting it.
        EditorApplication.LockReloadAssemblies();
        try
        {
            var plan = JObject.Parse(File.ReadAllText(Output + (File.Exists(Output + "/plan-next.json") ? "/plan-next.json" : "/plan.json")));
            if ((string)plan["status"] != "PREFLIGHT_PASS") throw new InvalidDataException("Patch preflight is required.");
            var changes = ((JArray)plan["changes"]).OfType<JObject>().ToArray();
            var creatureCatalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(CreatureCatalogPath);
            var equipmentCatalog = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>(EquipmentCatalogPath);
            if (creatureCatalog == null || equipmentCatalog == null) throw new InvalidOperationException("Existing catalogs are required.");
            string[] wrappers = creatureCatalog.entries.Select(e => e.prefabPath).Concat(equipmentCatalog.entries.Select(e => e.prefabPath)).Distinct().ToArray();
            var wrapperGuids = wrappers.ToDictionary(p => p, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            string libraryPath = "Assets/Assets/Resources/GeneratedRpgContentLibrary.asset", libraryHash = HashFile(libraryPath);
            string[] stagGlbs = changes.Where(c => ((string)c["destination"]).EndsWith(".glb", StringComparison.OrdinalIgnoreCase)).Select(c => (string)c["destination"]).ToArray();
            var oldSubassets = stagGlbs.ToDictionary(p => p, SnapshotSubassets, StringComparer.Ordinal);
            if (File.Exists(Output + "/import-baseline55.json"))
                oldSubassets = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, long>>>(File.ReadAllText(Output + "/import-baseline55.json"));
            File.WriteAllText(Output + "/stag-import-before.json", JsonConvert.SerializeObject(oldSubassets, Formatting.Indented));
            // note: Back up bytes, sidecars and affected wrapper/controller/catalog data before touching any installed revision.
            foreach (var change in changes)
            {
                string path = (string)change["destination"]; Confine(path);
                if (HashFile((string)change["staged"]) != (string)change["newSha256"]) throw new InvalidDataException("Staged bytes changed: " + path);
                string prior = File.Exists(path) ? HashFile(path) : null;
                if (prior != (string)change["oldSha256"]) throw new InvalidDataException("Installed revision changed after preflight: " + path);
                Backup(path); Backup(path + ".meta");
            }
            Backup(CreatureCatalogPath); Backup(CreatureCatalogPath + ".meta"); Backup(EquipmentCatalogPath); Backup(EquipmentCatalogPath + ".meta");
            foreach (string animal in new[] { "deer", "horse", "cow" })
                foreach (string path in Directory.GetFiles("Assets/YourQuest DOT Creatures/" + animal, "*", SearchOption.AllDirectories)) Backup(path);
            WriteProgress("APPLYING_PATCHES");
            AssetDatabase.DisallowAutoRefresh(); AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var change in changes)
                {
                    string path = (string)change["destination"];
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.Copy((string)change["staged"], path, true);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); AssetDatabase.AllowAutoRefresh(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in stagGlbs) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var newSubassets = stagGlbs.ToDictionary(p => p, SnapshotSubassets, StringComparer.Ordinal);
            File.WriteAllText(Output + "/stag-import-after.json", JsonConvert.SerializeObject(newSubassets, Formatting.Indented));
            // note: Shared meshes/animations must retain their generated IDs; otherwise external references require an explicit migration before publication.
            foreach (string path in stagGlbs)
            {
                var oldIds = oldSubassets[path]; var newIds = newSubassets[path];
                foreach (var pair in oldIds)
                    if (!newIds.TryGetValue(pair.Key, out long value) || value != pair.Value)
                    {
                        // note: This delivered cow revision renames its body material and replaces embedded normal/eye textures. Only the owned cow wrapper references them, as recorded by the GUID/reference audit.
                        bool cowMigration = Path.GetFileName(path).StartsWith("yq_wildlife_cow_LOD", StringComparison.Ordinal) &&
                            new[] { "UnityEngine.Material|yq_cow_Material", "UnityEngine.Texture2D|cow_normal", "UnityEngine.Texture2D|cow_source_cowEye_png" }.Contains(pair.Key) &&
                            File.Exists(Output + "/cow-reference-migration.json");
                        if (!cowMigration) throw new InvalidOperationException("Imported subasset identity changed: " + path + " " + pair.Key);
                    }
            }
            PublishCraftingCorrection(equipmentCatalog);
            WriteProgress("ORGANIZING_ALL_FILES");
            var before = Inventory();
            File.WriteAllText(Output + "/organization-before.json", JsonConvert.SerializeObject(before, Formatting.Indented));
            var directoryGuids = Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/'))
                .Where(p => p != Root + "/NPCs/Races" || !AssetDatabase.IsValidFolder(Root + "/Races"))
                .ToDictionary(p => p, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                foreach (var move in Moves)
                {
                    Confine(move.Key); Confine(move.Value); Folder(Path.GetDirectoryName(move.Value).Replace('\\', '/'));
                    string incoming = Root + "/NPCs/YourQuest_Satyr_Modular_Race_Kit_2026-10-03";
                    string satyr = move.Value + "/YourQuest_Satyr_Modular_Race_Kit_2026-10-03";
                    if (move.Key == Root + "/Races" && AssetDatabase.IsValidFolder(move.Key) && AssetDatabase.IsValidFolder(move.Value))
                    {
                        // note: The later new kit created a category parent before the original race folder moved. Temporarily relocate that new kit, preserving the original parent GUID.
                        if (!AssetDatabase.IsValidFolder(satyr) || Directory.GetDirectories(move.Value).Length != 1 || Directory.GetFiles(move.Value).Any(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))) throw new IOException("Unexpected existing race category contents.");
                        string temporary = AssetDatabase.MoveAsset(satyr, incoming); if (!string.IsNullOrEmpty(temporary)) throw new IOException(temporary);
                        if (Directory.GetDirectories(move.Value).Length != 0 || Directory.GetFiles(move.Value).Length != 0 || !AssetDatabase.DeleteAsset(move.Value)) throw new IOException("New empty race parent could not be removed.");
                    }
                    if (!Directory.Exists(move.Key) && Directory.Exists(move.Value)) continue;
                    if (!Directory.Exists(move.Key)) throw new InvalidOperationException("Category source is missing: " + move.Key);
                    if (Directory.Exists(move.Value)) throw new InvalidOperationException("Category destination already exists: " + move.Value);
                    string result = AssetDatabase.MoveAsset(move.Key, move.Value);
                    if (!string.IsNullOrEmpty(result)) throw new IOException(result);
                    if (move.Key == Root + "/Races" && AssetDatabase.IsValidFolder(incoming))
                    { string result2 = AssetDatabase.MoveAsset(incoming, satyr); if (!string.IsNullOrEmpty(result2)) throw new IOException(result2); }
                }
            }
            finally { AssetDatabase.AllowAutoRefresh(); }
            foreach (var entry in creatureCatalog.entries) entry.sourcePaths = entry.sourcePaths.Select(Map).ToArray();
            foreach (var entry in equipmentCatalog.entries) { entry.sourcePath = Map(entry.sourcePath); entry.partsSourcePath = Map(entry.partsSourcePath); }
            EditorUtility.SetDirty(creatureCatalog); EditorUtility.SetDirty(equipmentCatalog); AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var record in before)
            {
                string path = Map(record.path);
                if (!File.Exists(path) || HashFile(path) != record.sha256 || AssetDatabase.AssetPathToGUID(path) != record.guid)
                    throw new InvalidDataException("File bytes or GUID changed during categorization: " + record.path);
            }
            foreach (var pair in directoryGuids)
                if (AssetDatabase.AssetPathToGUID(Map(pair.Key)) != pair.Value) throw new InvalidDataException("Folder GUID changed: " + pair.Key);
            Folder(Root + "/Catalogs");
            // note: Index every current source file, including any newly imported pack documentation; sidecars remain paired with their files.
            var classified = Inventory().Where(r => !r.path.EndsWith("/DOT_ASSET_INDEX.json", StringComparison.Ordinal)).OrderBy(r => r.path, StringComparer.Ordinal).ToArray();
            File.WriteAllText(Root + "/Catalogs/DOT_ASSET_INDEX.json", JsonConvert.SerializeObject(new {
                schema = "yourquest.dot-asset-index.v1", utc = DateTime.UtcNow, fileCount = classified.Length,
                categories = new[] { "NPCs", "Monsters", "Wildlife", "Equipment", "Crafting", "Catalogs" }, files = classified,
                layout = "Pack-relative source, export, animation, texture, documentation and validation relationships retained; .meta sidecars move with their assets."
            }, Formatting.Indented));
            AssetDatabase.ImportAsset(Root + "/Catalogs/DOT_ASSET_INDEX.json", ImportAssetOptions.ForceSynchronousImport);
            WriteProgress("VERIFYING_UPDATED_BINDINGS");
            YQDotCreatureIntake.BuildRepairedWildlife(File.Exists(Output + "/plan-next.json") ? new[] { "deer", "horse", "cow" } : new[] { "deer" });
            if (Directory.Exists(Root + "/NPCs/Races/YourQuest_Satyr_Modular_Race_Kit_2026-10-03")) YQDotCreatureIntake.BuildSatyrs();
            if (Directory.Exists(YQDotEquipmentIntake.CoverageRoot)) YQDotEquipmentIntake.BuildArmorCoverage();
            foreach (var pair in wrapperGuids)
                if (AssetDatabase.AssetPathToGUID(pair.Key) != pair.Value) throw new InvalidOperationException("Production wrapper GUID changed: " + pair.Key);
            if (HashFile(libraryPath) != libraryHash) throw new InvalidOperationException("Production item pools changed during metadata/organization maintenance.");
            foreach (var entry in creatureCatalog.entries)
                foreach (string path in entry.sourcePaths) if (!File.Exists(path)) throw new InvalidDataException("Creature source reference missing: " + path);
            foreach (var entry in equipmentCatalog.entries) if (!File.Exists(entry.sourcePath)) throw new InvalidDataException("Equipment source reference missing: " + entry.sourcePath);
            YQDotPatchVerification.Run(creatureCatalog, equipmentCatalog);
            File.WriteAllText(Output + "/maintenance.json", JsonConvert.SerializeObject(new {
                status = "PASS", utc = DateTime.UtcNow, patchFiles = changes.Length, categorizedFiles = classified.Length,
                preservedWrapperGuids = wrapperGuids.Count, preservedImportedFiles = before.Length, preservedFolderGuids = directoryGuids.Count,
                moves = Moves, oldSubassets, newSubassets, productionItemPoolsUnchanged = true,
                evidence = "Unity asset moves and imports, source-byte/GUID/reference preservation and focused Editor fixtures. Ordinary PlaySafe movement/contact remains unverified."
            }, Formatting.Indented));
        }
        catch (Exception exception) { error = exception.ToString(); Debug.LogException(exception); }
        finally { EditorApplication.UnlockReloadAssemblies(); }
        if (error != null) File.WriteAllText(Output + "/maintenance.json", JsonConvert.SerializeObject(new { status = "FAIL", utc = DateTime.UtcNow, error, rollback = Output + "/Backup" }, Formatting.Indented));
    }

    private static FileRecord[] Inventory()
    {
        // note: Publish bounded progress during the comprehensive byte/GUID inventory so a slow import can be distinguished from an interrupted operation.
        var files = Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).ToArray();
        var records = new FileRecord[files.Length];
        for (int i = 0; i < files.Length; i++)
        { if (i % 100 == 0) WriteProgress("INVENTORY_" + i + "_OF_" + files.Length); records[i] = Record(files[i].Replace('\\', '/')); }
        return records;
    }

    private static void PublishCraftingCorrection(YQDotEquipmentCatalog catalog)
    {
        var manifest = JObject.Parse(File.ReadAllText(YQDotEquipmentIntake.CraftingRoot + "/Data/visual_manifest.v1.json"));
        var corrected = ((JArray)manifest["visuals"]).OfType<JObject>().ToDictionary(r => (string)r["visual_id"], StringComparer.Ordinal);
        foreach (var entry in catalog.entries)
        {
            if (!entry.sourcePath.StartsWith(YQDotEquipmentIntake.CraftingRoot + "/", StringComparison.Ordinal) || !corrected.TryGetValue(entry.assetId, out var row)) continue;
            entry.materialKey = (string)row["material_key"]; entry.sourceContractJson = row.ToString(Formatting.None);
        }
        foreach (var material in catalog.craftingMaterials)
            material.visualAssetIds = catalog.entries.Where(e => e.materialKey == material.materialKey).Select(e => e.assetId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        catalog.craftingReleaseVersion = "1.0.1";
        catalog.craftingOrnamentRecipeExamplesJson = File.ReadAllText(YQDotEquipmentIntake.CraftingRoot + "/Data/ornament_recipe_examples.v1_0_1.json");
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
    }

    private static Dictionary<string, long> SnapshotSubassets(string path)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id))
                result.Add(asset.GetType().FullName + "|" + asset.name, id);
        return result;
    }
    private static void Backup(string path)
    {
        if (!File.Exists(path)) return;
        string destination = Output + "/Backup/" + path;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (!File.Exists(destination)) File.Copy(path, destination);
    }
    private static string Map(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        const string incoming = Root + "/NPCs/YourQuest_Satyr_Modular_Race_Kit_2026-10-03";
        if (path == incoming || path.StartsWith(incoming + "/", StringComparison.Ordinal)) return Root + "/NPCs/Races/YourQuest_Satyr_Modular_Race_Kit_2026-10-03" + path.Substring(incoming.Length);
        foreach (var move in Moves)
            if (path == move.Key || path.StartsWith(move.Key + "/", StringComparison.Ordinal)) return move.Value + path.Substring(move.Key.Length);
        return path;
    }
    private static FileRecord Record(string path) => new FileRecord {
        path = path, sha256 = HashFile(path), guid = AssetDatabase.AssetPathToGUID(path),
        category = Map(path).Substring(Root.Length + 1).Split('/')[0], kind = Kind(path)
    };
    private static string Kind(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".glb") return path.Contains("/Modules/") ? "Modular mesh" : "Exported mesh and embedded animation/materials";
        if (extension == ".blend") return "Editable source";
        if (extension == ".png" || extension == ".jpg") return path.Contains("/Previews/") ? "Preview" : "Texture";
        if (extension == ".mp4") return "Motion proof";
        if (extension == ".py" || extension == ".cjs" || extension == ".cs") return "Supplied integration/reference tooling";
        if (path.Contains("/Validation/")) return "Validation evidence";
        if (path.Contains("/Licenses/") || path.Contains("/License/")) return "License and provenance";
        return extension == ".json" ? "Manifest or structured reference data" : "Documentation or reference data";
    }
    private static string HashFile(string path)
    {
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static void Confine(string path)
    {
        if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Asset operation escapes DOT root: " + path);
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Folder(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
    private static void WriteProgress(string phase)
    {
        File.WriteAllText(Output + "/maintenance.json", JsonConvert.SerializeObject(new { status = "RUNNING", phase, utc = DateTime.UtcNow }, Formatting.Indented));
    }
}
