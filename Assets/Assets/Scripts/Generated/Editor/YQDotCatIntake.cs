using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: The approved domestic Cat joins existing passive wildlife, source layout and movement owners; archive text remains data.
[InitializeOnLoad]
public static class YQDotCatIntake
{
    private const string Root = YQDotAssetLayout.Root;
    private const string Output = "outputs/DOT_Integration_20261004/Cat_R8";
    private const string Request = "Assets/Assets/EditorBuildRequests/IntegrateDotCatR8.request";
    private const string RepairRequest = "Assets/Assets/EditorBuildRequests/RepairDotCatR8Binding.request";
    private const string Catalog = "Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset";
    private const string Layout = Root + "/Catalogs/DOT_ASSET_LAYOUT.json";
    private const string Index = Root + "/Catalogs/DOT_ASSET_INDEX.json";
    internal const string InstalledContent = Root + "/Wildlife/Cat/Documentation/Manifests/R8/Cat_Installed_Content.json";
    private static double _next;
    static YQDotCatIntake() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.timeSinceStartup < _next || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        _next = EditorApplication.timeSinceStartup + .5d;
        if (File.Exists(RepairRequest)) { if (!AssetDatabase.DeleteAsset(RepairRequest)) File.Delete(RepairRequest); Integrate(true); return; }
        if (!File.Exists(Request)) return;
        if (!AssetDatabase.DeleteAsset(Request)) File.Delete(Request);
        Integrate();
    }

    internal static List<YQDotCreatureEntry> ReadJobs() => new List<YQDotCreatureEntry> { JObject.Parse(File.ReadAllText(InstalledContent))["entry"].ToObject<YQDotCreatureEntry>() };
    private static void Integrate(bool repair = false)
    {
        var created = new List<string>(); bool published = false, backedUpThisRun = false, bindingsStarted = false;
        EditorApplication.LockReloadAssemblies();
        try
        {
            var plan = JObject.Parse(File.ReadAllText(Output + "/plan.json"));
            if ((string)plan["status"] != "PREFLIGHT_PASS" || (string)JObject.Parse(File.ReadAllText(Output + "/source-contracts.json"))["status"] != "PASS") throw new InvalidDataException("Reviewed source preflight required.");
            var files = ((JArray)plan["files"]).OfType<JObject>().ToArray();
            foreach (var row in files) { string path = (string)row["destination"]; if (!path.StartsWith(Root + "/Wildlife/Cat/", StringComparison.Ordinal) || !Path.GetFullPath(path).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || (!repair && File.Exists(path)) || (repair && (!File.Exists(path) || Hash(path) != (string)row["sha256"])) || Hash((string)row["staged"]) != (string)row["sha256"]) throw new InvalidDataException("Reviewed destination/staging changed: " + path); }
            if (!repair && (File.Exists(InstalledContent) || Directory.Exists("Assets/YourQuest DOT Creatures/Wildlife/cat"))) throw new InvalidDataException("Cat already installed; incremental review required.");
            if (repair)
            {
                // note: Resume only the retained, unpublished Cat build; never repeat source intake or overwrite a published catalog transaction.
                var failed = JObject.Parse(File.ReadAllText(Output + "/integration.json"));
                if ((string)failed["status"] != "FAIL" || (bool?)failed["detail"]["bindingsStarted"] != true || (bool?)failed["detail"]["published"] != false || Hash(Catalog) != Hash(Output + "/Backup/" + Catalog)) throw new InvalidDataException("Expected unchanged catalog and unpublished Cat build.");
                if (!File.Exists(Output + "/integration-attempt-1.json")) File.Copy(Output + "/integration.json", Output + "/integration-attempt-1.json");
            }
            var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(Catalog);
            var before = catalog.entries.ToDictionary(e => e.assetId, e => JsonConvert.SerializeObject(e));
            var protectedPrefabs = catalog.entries.ToDictionary(e => e.assetId, e => new { path = AssetDatabase.GetAssetPath(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(e.prefabPath)), guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(e.prefabPath))) });
            if (!repair) foreach (string path in new[] { Catalog, Catalog + ".meta", Layout, Index }) Backup(path);
            backedUpThisRun = !repair;
            string library = "Assets/Assets/Resources/GeneratedRpgContentLibrary.asset", libraryHash = Hash(library);
            if (!repair)
            {
            Receipt("RUNNING", new { phase = "IMPORTING_CATEGORIZED_CAT_RELEASE", files = files.Length });
            AssetDatabase.DisallowAutoRefresh();
            try { foreach (var row in files) { string path = (string)row["destination"]; Directory.CreateDirectory(Path.GetDirectoryName(path)); File.Copy((string)row["staged"], path); created.Add(path); } }
            finally { AssetDatabase.AllowAutoRefresh(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var layout = JObject.Parse(File.ReadAllText(Layout));
            foreach (var row in files)
            {
                string path = (string)row["destination"];
                ((JArray)layout["files"]).Add(new JObject { ["origin"] = row["origin"], ["path"] = path, ["aliases"] = new JArray(), ["pack"] = "CatR8", ["kind"] = row["kind"], ["sha256"] = row["sha256"], ["guid"] = AssetDatabase.AssetPathToGUID(path), ["metaSha256"] = Hash(path + ".meta"), ["embeddedMaterials"] = row["embeddedMaterials"] ?? new JArray(), ["materialFolder"] = row["materialFolder"] });
            }
            File.WriteAllText(Layout, layout.ToString(Formatting.Indented)); YQDotAssetLayout.ReloadSourcePaths(); AssetDatabase.ImportAsset(Layout, ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Path.GetDirectoryName(InstalledContent)); File.WriteAllText(InstalledContent, plan.ToString(Formatting.Indented)); created.Add(InstalledContent); AssetDatabase.ImportAsset(InstalledContent, ImportAssetOptions.ForceSynchronousImport);
            foreach (var group in files.Where(f => ((string)f["destination"]).EndsWith(".glb", StringComparison.Ordinal)).GroupBy(f => (string)f["materialFolder"]))
            {
                string index = group.Key + "/MATERIAL_INDEX.json"; Directory.CreateDirectory(group.Key);
                File.WriteAllText(index, JsonConvert.SerializeObject(new { storage = "Original GLB subassets; delivered renderer assignments preserved", materials = group.Select(f => new { sourcePath = (string)f["destination"], sourceGuid = AssetDatabase.AssetPathToGUID((string)f["destination"]), embedded = f["embeddedMaterials"] }) }, Formatting.Indented)); created.Add(index);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            else
            {
                created.AddRange(files.Select(f => (string)f["destination"])); created.Add(InstalledContent);
                created.AddRange(files.Where(f => ((string)f["destination"]).EndsWith(".glb", StringComparison.Ordinal)).Select(f => (string)f["materialFolder"] + "/MATERIAL_INDEX.json").Distinct());
            }
            Receipt("RUNNING", new { phase = "BUILDING_ONLY_NEW_CAT_BINDING" });
            bindingsStarted = true; YQDotCreatureIntake.BuildApprovedNpcEntries(ReadJobs(), Output); published = true;
            // note: Include the owned atlas-orientation corrections in the categorized source index; original embedded texture/material data remains intact.
            created.AddRange(Directory.GetFiles(Root + "/Wildlife/Cat/Body Types/Domestic Tabby/Materials/R8", "*.mat", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')));
            foreach (var pair in before) if (JsonConvert.SerializeObject(catalog.entries.Single(e => e.assetId == pair.Key)) != pair.Value) throw new InvalidDataException("Existing catalog contract changed: " + pair.Key);
            foreach (var pair in protectedPrefabs) if (AssetDatabase.AssetPathToGUID(pair.Value.path) != pair.Value.guid) throw new InvalidDataException("Existing prefab GUID changed: " + pair.Key);
            if (Hash(library) != libraryHash) throw new InvalidDataException("Unrelated equipment generation pools changed.");
            var indexDoc = JObject.Parse(File.ReadAllText(Index)); var indexRows = (JArray)indexDoc["files"];
            foreach (string path in created.Concat(new[] { Layout }))
            {
                var row = indexRows.OfType<JObject>().SingleOrDefault(r => (string)r["path"] == path); if (row == null) { row = new JObject(); indexRows.Add(row); }
                row["path"] = path; row["sha256"] = Hash(path); row["guid"] = AssetDatabase.AssetPathToGUID(path); row["category"] = path.Substring(Root.Length + 1).Split('/')[0];
            }
            indexDoc["files"] = new JArray(indexRows.OfType<JObject>().OrderBy(r => (string)r["path"], StringComparer.Ordinal)); indexDoc["fileCount"] = indexRows.Count; indexDoc["utc"] = DateTime.UtcNow;
            File.WriteAllText(Index, indexDoc.ToString(Formatting.Indented)); AssetDatabase.ImportAsset(Index, ImportAssetOptions.ForceSynchronousImport);
            var bodies = catalog.entries.Where(e => e.species == "cat" && e.kind == "wildlife").ToArray();
            YQDotCatVerification.Run(bodies, Output);
            YQDotCreaturePreview.RenderEntries(bodies, Output + "/Preview", true, new[] { "DotIdle", "DotAlert", "DotWalk", "DotTrot" });
            foreach (var row in files) if (Hash((string)row["destination"]) != (string)row["sha256"]) throw new InvalidDataException("Imported source bytes changed.");
            Receipt("PASS", new { files = files.Length, bodies = 1, lods = 3, ownLoops = 4, preservedPriorEntries = before.Count, totalEntries = catalog.entries.Count, renderedPoses = 12, evidence = "Fresh Editor imports, canonical catalog/lazy registry, deterministic passive Cat binding, four original loops, source-matched walk cadence, evaluated skins/bounds and all-LOD renders. No start/stop clips invented. Live contact, unlike-clip transitions and moving LOD acceptance remain unverified." });
        }
        catch (Exception exception)
        {
            // note: A rejected repeat must never restore a previous run's backup. Once shells start building, retain their source data for bounded repair rather than orphaning new prefab references.
            if (backedUpThisRun && !bindingsStarted) { foreach (string path in new[] { Catalog, Catalog + ".meta", Layout, Index }) if (File.Exists(Output + "/Backup/" + path)) File.Copy(Output + "/Backup/" + path, path, true); foreach (string path in created) { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".meta")) File.Delete(path + ".meta"); } YQDotAssetLayout.ReloadSourcePaths(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); }
            Receipt("FAIL", new { published, bindingsStarted, backedUpThisRun, error = exception.ToString() }); Debug.LogException(exception);
        }
        finally { EditorApplication.UnlockReloadAssemblies(); }
    }
    private static string Hash(string path) { using (var algorithm = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    private static void Backup(string path) { string target = Output + "/Backup/" + path; Directory.CreateDirectory(Path.GetDirectoryName(target)); if (File.Exists(target)) throw new InvalidOperationException("Previous Cat backup exists; review before retry."); File.Copy(path, target); }
    private static void Receipt(string status, object detail) => File.WriteAllText(Output + "/integration.json", JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, editorVersion = Application.unityVersion, detail }, Formatting.Indented));
}
