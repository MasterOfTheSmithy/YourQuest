using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: The additive Avian release joins the canonical catalog/registry and sampled gait owner; authoring part banks never become competing complete actors.
[InitializeOnLoad]
public static class YQDotAvianIntake
{
    private const string Root = YQDotAssetLayout.Root;
    private const string SourceRoot = Root + "/NPCs/Avian/YourQuest_Avian_V9b/";
    private const string Output = "outputs/DOT_Integration_20261003/Avian_M2";
    private const string Request = "Assets/Assets/EditorBuildRequests/IntegrateDotAvianM2.request";
    private const string Catalog = "Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset";
    private const string Layout = Root + "/Catalogs/DOT_ASSET_LAYOUT.json";
    private const string Index = Root + "/Catalogs/DOT_ASSET_INDEX.json";
    internal const string InstalledContent = Root + "/NPCs/Avian/Documentation/Manifests/Avian_Installed_Content.json";
    private static double _next;
    static YQDotAvianIntake() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.timeSinceStartup < _next || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        _next = EditorApplication.timeSinceStartup + .5d;
        if (!File.Exists(Request)) return;
        if (!AssetDatabase.DeleteAsset(Request)) File.Delete(Request);
        Integrate();
    }

    internal static List<YQDotCreatureEntry> ReadJobs()
    {
        var installed = JObject.Parse(File.ReadAllText(InstalledContent));
        var sources = ((JArray)installed["files"]).OfType<JObject>().ToDictionary(r => (string)r["relative"], StringComparer.Ordinal);
        var build = JObject.Parse(File.ReadAllText(YQDotAssetLayout.Resolve(SourceRoot + "Validation/AvianV9b_terrestrial_build.json")));
        var clips = ((JArray)build["clips"]).OfType<JObject>().ToDictionary(c => (string)c["name"], StringComparer.Ordinal);
        var jobs = new List<YQDotCreatureEntry>();
        foreach (bool frost in new[] { false, true })
        {
            string[] paths = frost ? new[] { "ModularExpansion/Exports/AvianModularM1_Frost_Assembled_LOD0.glb", "ModularExpansion/M2/Exports/AvianModularM2_Frost_Assembled_LOD1.glb", "ModularExpansion/M2/Exports/AvianModularM2_Frost_Assembled_LOD2.glb" } : new[] { "Exports/AvianV9b_Terrestrial_LOD0.glb", "LODs/AvianV9b_Terrestrial_LOD1.glb", "LODs/AvianV9b_Terrestrial_LOD2.glb" };
            var body = Entry(frost ? "yq_avian_m2_frost" : "yq_avian_v9b_ordinary", "race", null, paths, sources);
            // note: Use the exact sampled displacement (.096 start/.144 stop), rather than the rounded travel summaries in the source prose/table.
            body.authoredWalkSpeed = (float)build["loop_speed_m_s"];
            body.motionProfile = new YQDotMotionProfile { startDuration = (float)clips["walk_start"]["duration_s"], loopDuration = (float)clips["walk_loop"]["duration_s"], stopDuration = (float)clips["walk_stop"]["duration_s"], startDistance = Distances(clips["walk_start"]), loopDistance = Distances(clips["walk_loop"]), stopDistance = Distances(clips["walk_stop"]) };
            if (!body.motionProfile.IsValid) throw new InvalidDataException("Invalid supplied Avian motion samples."); jobs.Add(body);
        }
        var parts = JObject.Parse(File.ReadAllText(YQDotAssetLayout.Resolve(SourceRoot + "ModularExpansion/M2/Contracts/AvianModularM2_Part_Socket_Contract.json")));
        const string bank = "ModularExpansion/Exports/AvianModularM1_Base_PartBank_LOD0.glb";
        foreach (JObject part in (JArray)parts["parts"])
        {
            var module = Entry("yq_avian_m2_base_" + ((string)part["id"]).Replace('.', '_'), "module", (string)part["id"], new[] { bank }, sources);
            module.sourceRendererNames = new[] { (string)sources[bank]["meshes"][(int)part["mesh_index"]]["node"] }; module.requiresSlotReplacement = true; jobs.Add(module);
        }
        string[] hardware = { "ModularExpansion/Exports/AvianModularM1_Elemental_Clasp_Parts.glb", "ModularExpansion/M2/Exports/AvianModularM2_Elemental_Clasp_Parts_LOD1.glb", "ModularExpansion/M2/Exports/AvianModularM2_Elemental_Clasp_Parts_LOD2.glb" };
        foreach (string id in new[] { "dark", "light", "frost" })
        {
            var module = Entry("yq_avian_m2_clasp_" + id, "module", "accessory.waist_hardware", hardware, sources);
            module.sourceRendererNames = hardware.Select(p => ((JArray)sources[p]["meshes"]).OfType<JObject>().Single(m => ((string)m["name"]).StartsWith("avian.elemental.clasp." + id, StringComparison.Ordinal))["node"].Value<string>()).ToArray();
            module.requiresSlotReplacement = true; jobs.Add(module);
        }
        if (jobs.Count != 25) throw new InvalidDataException("Expected two bodies, twenty base parts and three elemental replacements.");
        return jobs;
    }
    private static float[] Distances(JObject clip) => ((JArray)clip["samples"]).OfType<JObject>().Select(s => (float)s["forward_distance_m"]).ToArray();
    private static YQDotCreatureEntry Entry(string id, string kind, string slot, string[] paths, Dictionary<string, JObject> sources) => new YQDotCreatureEntry { assetId = id, kind = kind, species = "avian", category = "avian", bodyForm = "ordinary", compatibilityId = "corvid_terrestrial_v5", moduleSlot = slot, sourcePaths = paths.Select(p => YQDotAssetLayout.Resolve(SourceRoot + p)).ToArray(), sourceHashes = paths.Select(p => (string)sources[p]["sha256"]).ToArray() };

    private static void Integrate()
    {
        var created = new List<string>(); bool published = false, backedUpThisRun = false, bindingsStarted = false;
        EditorApplication.LockReloadAssemblies();
        try
        {
            var plan = JObject.Parse(File.ReadAllText(Output + "/plan.json"));
            if ((string)plan["status"] != "PREFLIGHT_PASS" || (string)JObject.Parse(File.ReadAllText(Output + "/source-contracts.json"))["status"] != "PASS") throw new InvalidDataException("Frozen dependency/source preflight required.");
            var files = ((JArray)plan["files"]).OfType<JObject>().ToArray();
            foreach (var row in files) { string path = (string)row["destination"]; if (!path.StartsWith(Root + "/NPCs/Avian/", StringComparison.Ordinal) || !Path.GetFullPath(path).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || File.Exists(path) || Hash((string)row["staged"]) != (string)row["sha256"]) throw new InvalidDataException("Reviewed destination/staging changed: " + path); }
            if (File.Exists(InstalledContent) || Directory.Exists("Assets/YourQuest DOT Creatures/NPCs/avian")) throw new InvalidDataException("Avian already installed; incremental review required.");
            var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(Catalog);
            var before = catalog.entries.ToDictionary(e => e.assetId, e => JsonConvert.SerializeObject(e));
            var protectedPrefabs = catalog.entries.ToDictionary(e => e.assetId, e => new { path = AssetDatabase.GetAssetPath(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(e.prefabPath)), guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(e.prefabPath))) });
            foreach (string path in new[] { Catalog, Catalog + ".meta", Layout, Index }) Backup(path);
            backedUpThisRun = true;
            string library = "Assets/Assets/Resources/GeneratedRpgContentLibrary.asset", libraryHash = Hash(library);
            Receipt("RUNNING", new { phase = "IMPORTING_CATEGORIZED_BASE_AND_ADDON", files = files.Length });
            AssetDatabase.DisallowAutoRefresh();
            try { foreach (var row in files) { string path = (string)row["destination"]; Directory.CreateDirectory(Path.GetDirectoryName(path)); File.Copy((string)row["staged"], path); created.Add(path); } }
            finally { AssetDatabase.AllowAutoRefresh(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var layout = JObject.Parse(File.ReadAllText(Layout));
            foreach (var row in files)
            {
                string path = (string)row["destination"];
                ((JArray)layout["files"]).Add(new JObject { ["origin"] = row["origin"], ["path"] = path, ["aliases"] = new JArray(), ["pack"] = "AvianV9bM2", ["kind"] = row["kind"], ["sha256"] = row["sha256"], ["guid"] = AssetDatabase.AssetPathToGUID(path), ["metaSha256"] = Hash(path + ".meta"), ["embeddedMaterials"] = row["embeddedMaterials"] ?? new JArray(), ["materialFolder"] = row["materialFolder"] });
            }
            File.WriteAllText(Layout, layout.ToString(Formatting.Indented)); YQDotAssetLayout.ReloadSourcePaths(); AssetDatabase.ImportAsset(Layout, ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Path.GetDirectoryName(InstalledContent)); File.WriteAllText(InstalledContent, plan.ToString(Formatting.Indented)); created.Add(InstalledContent); AssetDatabase.ImportAsset(InstalledContent, ImportAssetOptions.ForceSynchronousImport);
            foreach (var group in files.Where(f => ((string)f["destination"]).EndsWith(".glb", StringComparison.Ordinal)).GroupBy(f => (string)f["materialFolder"]))
            {
                string index = group.Key + "/MATERIAL_INDEX.json"; Directory.CreateDirectory(group.Key);
                File.WriteAllText(index, JsonConvert.SerializeObject(new { storage = "Original GLB subassets; delivered renderer assignments preserved", materials = group.Select(f => new { sourcePath = (string)f["destination"], sourceGuid = AssetDatabase.AssetPathToGUID((string)f["destination"]), embedded = f["embeddedMaterials"] }) }, Formatting.Indented)); created.Add(index);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Receipt("RUNNING", new { phase = "BUILDING_ONLY_NEW_AVIAN_BINDINGS" });
            bindingsStarted = true; YQDotCreatureIntake.BuildApprovedAvianEntries(ReadJobs(), Output); published = true;
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
            var bodies = catalog.entries.Where(e => e.species == "avian" && e.kind == "race").ToArray();
            YQDotAvianVerification.Run(bodies, Output);
            YQDotCreaturePreview.RenderEntries(bodies, Output + "/Preview", true, new[] { "DotIdle", "DotStart", "DotWalk", "DotStop", "DotGreet" });
            foreach (var row in files) if (Hash((string)row["destination"]) != (string)row["sha256"]) throw new InvalidDataException("Imported source bytes changed.");
            Receipt("PASS", new { files = files.Length, frozenDependencies = 6, bodies = 2, registeredReplacementParts = 23, preservedPriorEntries = before.Count, totalEntries = catalog.entries.Count, renderedPoses = 30, evidence = "Fresh Editor imports, canonical catalog/lazy registry, exact rest interfaces, replacement admission, deterministic Avian binding, sampled actor travel, evaluated skins and all-LOD own-clip renders. Live PlaySafe contact/LOD transitions remain unverified. Selective runtime triangle replacement and weighted palette application are retained as source contracts, not enabled by this intake." });
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
    private static void Backup(string path) { string target = Output + "/Backup/" + path; Directory.CreateDirectory(Path.GetDirectoryName(target)); if (File.Exists(target)) throw new InvalidOperationException("Previous Avian backup exists; review before retry."); File.Copy(path, target); }
    private static void Receipt(string status, object detail) => File.WriteAllText(Output + "/integration.json", JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, editorVersion = Application.unityVersion, detail }, Formatting.Indented));
}
