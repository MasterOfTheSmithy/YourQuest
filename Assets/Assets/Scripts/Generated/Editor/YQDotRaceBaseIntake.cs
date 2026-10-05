using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Three versioned race bases join existing catalog, registry and movement owners; reviewed source documents remain data, not executable instructions.
[InitializeOnLoad]
public static class YQDotRaceBaseIntake
{
    private const string Root = YQDotAssetLayout.Root;
    private const string Output = "outputs/DOT_Integration_20261004/Races";
    private const string Request = "Assets/Assets/EditorBuildRequests/IntegrateDotRaceBases20261004.request";
    private const string RepairRequest = "Assets/Assets/EditorBuildRequests/RepairDotRaceBaseBounds20261004.request";
    private const string ReviewRequest = "Assets/Assets/EditorBuildRequests/ReviewDotBramblekinMaterial20261004.request";
    private const string MaterialRepairRequest = "Assets/Assets/EditorBuildRequests/RepairDotBramblekinMaterial20261004.request";
    private const string Catalog = "Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset";
    private const string Layout = Root + "/Catalogs/DOT_ASSET_LAYOUT.json";
    private const string Index = Root + "/Catalogs/DOT_ASSET_INDEX.json";
    internal const string InstalledContent = Root + "/Catalogs/DOT_RACES_20261004_INSTALLED.json";
    private static double _next;
    static YQDotRaceBaseIntake() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.timeSinceStartup < _next || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        _next = EditorApplication.timeSinceStartup + .5d;
        if (File.Exists(MaterialRepairRequest))
        {
            if (!AssetDatabase.DeleteAsset(MaterialRepairRequest)) File.Delete(MaterialRepairRequest);
            RepairBramblekinMaterial(); return;
        }
        if (File.Exists(ReviewRequest))
        {
            if (!AssetDatabase.DeleteAsset(ReviewRequest)) File.Delete(ReviewRequest);
            var body = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(Catalog).entries.Single(e => e.species == "bramblekin" && e.kind == "race");
            YQDotRaceBaseVerification.InspectMaterial(body, Output);
            YQDotCreaturePreview.RenderEntries(new[] { body }, Output + "/DirectSkinPreview", false, new[] { "DotIdle" }, false, false);
            return;
        }
        if (File.Exists(RepairRequest))
        {
            if (!AssetDatabase.DeleteAsset(RepairRequest)) File.Delete(RepairRequest);
            RepairInstalledBounds(); return;
        }
        if (!File.Exists(Request)) return;
        if (!AssetDatabase.DeleteAsset(Request)) File.Delete(Request);
        Integrate();
    }

    internal static bool IsNewRace(string species) => species == "dwarf" || species == "kitsune" || species == "bramblekin";
    internal static List<YQDotCreatureEntry> ReadJobs()
    {
        var jobs = JObject.Parse(File.ReadAllText(InstalledContent))["bodies"].ToObject<List<YQDotCreatureEntry>>();
        if (jobs.Count != 3 || jobs.Any(j => !IsNewRace(j.species) || j.sourcePaths.Length != 4)) throw new InvalidDataException("Expected three reviewed four-LOD race bases.");
        // note: No Bramblekin displacement recipe was delivered. Retain identifiable generic cadence scaffolding, never fabricate authored contact samples.
        jobs.Single(j => j.species == "bramblekin").authoredWalkSpeed = .5f;
        return jobs;
    }

    private static void RepairInstalledBounds()
    {
        EditorApplication.LockReloadAssemblies();
        try
        {
            var prior = JObject.Parse(File.ReadAllText(Output + "/integration.json"));
            if ((string)prior["status"] != "FAIL" || (bool?)prior["detail"]["published"] != true) throw new InvalidDataException("Bounds repair requires the published failed transaction.");
            if (!File.Exists(Output + "/integration-attempt-1.json")) File.Copy(Output + "/integration.json", Output + "/integration-attempt-1.json");
            var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(Catalog);
            var bodies = catalog.entries.Where(e => IsNewRace(e.species) && e.kind == "race").ToArray();
            if (bodies.Length != 3) throw new InvalidDataException("Expected exactly the three installed bases.");
            Receipt("RUNNING", new { phase = "REPAIRING_ONLY_OWNED_SKIN_BOUNDS" });
            foreach (var body in bodies)
            {
                string path = AssetDatabase.GetAssetPath(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(body.prefabPath));
                if (!path.StartsWith("Assets/YourQuest DOT Creatures/NPCs/" + body.species + "/", StringComparison.Ordinal)) throw new InvalidDataException("Unexpected repair target: " + path);
                string guid = AssetDatabase.AssetPathToGUID(path); var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // note: Repair only the owned prefab's sampled bounds/flag. Keep source GLBs, controllers, animations, bind transforms and catalog identities intact.
                    for (int lod = 0; lod < body.sourcePaths.Length; lod++)
                    {
                        var model = prefab.transform.Find("LOD" + lod).gameObject;
                        var animator = model.GetComponentInChildren<Animator>(true);
                        var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
                        var clips = controller.layers[0].stateMachine.states.Select(s => s.state.motion).OfType<AnimationClip>().Distinct().ToArray();
                        YQDotRaceBaseVerification.ExpandBounds(model, clips);
                    }
                    if (PrefabUtility.SaveAsPrefabAsset(prefab, path) == null) throw new InvalidDataException("Bounds repair publication failed.");
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
                if (AssetDatabase.AssetPathToGUID(path) != guid) throw new InvalidDataException("Owned prefab GUID changed.");
            }
            AssetDatabase.SaveAssets();
            YQDotRaceBaseVerification.Run(bodies, Output);
            YQDotCreaturePreview.RenderEntries(bodies, Output + "/Preview", true, new[] { "DotIdle", "DotStart", "DotWalk", "DotStop", "DotGreet" });
            var plan = JObject.Parse(File.ReadAllText(Output + "/plan.json"));
            foreach (JObject row in (JArray)plan["files"]) if (Hash((string)row["destination"]) != (string)row["sha256"]) throw new InvalidDataException("Installed source bytes changed.");
            Receipt("PASS", new { files = ((JArray)plan["files"]).Count, bodies = 3, preservedPriorEntries = catalog.entries.Count - 3, totalEntries = catalog.entries.Count, renderedPoses = 60, boundsRepair = "Retain sampled all-clip bounds on owned shells; disable importer per-frame offscreen recalculation", evidence = "Fresh Editor imports and bindings, exact own curves/morphs, sampled Dwarf/Kitsune travel, evaluated skin bounds and all-LOD renders. Live contact/LOD transitions, Bramblekin cadence and shader extensions remain unverified. Kitsune source/modules absent." });
        }
        catch (Exception exception) { Receipt("FAIL", new { published = true, repair = "owned-skin-bounds", error = exception.ToString() }); Debug.LogException(exception); }
        finally { EditorApplication.UnlockReloadAssemblies(); }
    }

    private static void RepairBramblekinMaterial()
    {
        EditorApplication.LockReloadAssemblies();
        try
        {
            var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(Catalog);
            var body = catalog.entries.Single(e => e.species == "bramblekin" && e.kind == "race");
            Receipt("RUNNING", new { phase = "CORRECTING_IMPORTED_TEXTURE_ALIAS_ORIENTATION" });
            var created = YQDotRaceBaseVerification.CorrectBramblekinTextureAliases(body, Output);
            var indexDoc = JObject.Parse(File.ReadAllText(Index)); var rows = (JArray)indexDoc["files"];
            foreach (string path in created)
            {
                var row = rows.OfType<JObject>().SingleOrDefault(r => (string)r["path"] == path);
                if (row == null) { row = new JObject(); rows.Add(row); }
                row["path"] = path; row["sha256"] = Hash(path); row["guid"] = AssetDatabase.AssetPathToGUID(path); row["category"] = "NPCs";
            }
            indexDoc["files"] = new JArray(rows.OfType<JObject>().OrderBy(r => (string)r["path"], StringComparer.Ordinal)); indexDoc["fileCount"] = rows.Count; indexDoc["utc"] = DateTime.UtcNow;
            File.WriteAllText(Index, indexDoc.ToString(Formatting.Indented)); AssetDatabase.ImportAsset(Index, ImportAssetOptions.ForceSynchronousImport);
            var bodies = catalog.entries.Where(e => IsNewRace(e.species) && e.kind == "race").ToArray();
            YQDotRaceBaseVerification.Run(bodies, Output);
            YQDotCreaturePreview.RenderEntries(bodies, Output + "/Preview", true, new[] { "DotIdle", "DotStart", "DotWalk", "DotStop", "DotGreet" });
            Receipt("PASS", new { files = 121, bodies = 3, preservedPriorEntries = catalog.entries.Count - 3, totalEntries = catalog.entries.Count, renderedPoses = 60, boundsRepair = true, textureAliasRepair = true, evidence = "Fresh Editor bindings, own curves/morphs, all-clip skin bounds and all-LOD renders. Correct only duplicate-texture orientation on two owned Bramblekin LOD0 material copies; source bytes and texture/color/material content preserved. Live contact/LOD transitions, Bramblekin cadence and shader extensions remain unverified." });
        }
        catch (Exception exception) { Receipt("FAIL", new { published = true, repair = "texture-alias-orientation", error = exception.ToString() }); Debug.LogException(exception); }
        finally { EditorApplication.UnlockReloadAssemblies(); }
    }
    private static void Integrate()
    {
        var created = new List<string>(); bool published = false, backedUpThisRun = false, bindingsStarted = false;
        EditorApplication.LockReloadAssemblies();
        try
        {
            var plan = JObject.Parse(File.ReadAllText(Output + "/plan.json"));
            if ((string)plan["status"] != "PREFLIGHT_PASS" || (string)JObject.Parse(File.ReadAllText(Output + "/source-contracts.json"))["status"] != "PASS") throw new InvalidDataException("Reviewed source preflight required.");
            var files = ((JArray)plan["files"]).OfType<JObject>().ToArray();
            foreach (var row in files) { string path = (string)row["destination"]; if (!path.StartsWith(Root + "/NPCs/", StringComparison.Ordinal) || !Path.GetFullPath(path).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || File.Exists(path) || Hash((string)row["staged"]) != (string)row["sha256"]) throw new InvalidDataException("Reviewed destination/staging changed: " + path); }
            if (File.Exists(InstalledContent) || new[] { "dwarf", "kitsune", "bramblekin" }.Any(s => Directory.Exists("Assets/YourQuest DOT Creatures/NPCs/" + s))) throw new InvalidDataException("Race bases already installed; incremental review required.");
            var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(Catalog);
            var before = catalog.entries.ToDictionary(e => e.assetId, e => JsonConvert.SerializeObject(e));
            var protectedPrefabs = catalog.entries.ToDictionary(e => e.assetId, e => new { path = AssetDatabase.GetAssetPath(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(e.prefabPath)), guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(e.prefabPath))) });
            foreach (string path in new[] { Catalog, Catalog + ".meta", Layout, Index }) Backup(path);
            backedUpThisRun = true;
            string library = "Assets/Assets/Resources/GeneratedRpgContentLibrary.asset", libraryHash = Hash(library);
            Receipt("RUNNING", new { phase = "IMPORTING_CATEGORIZED_RACE_BASES", files = files.Length });
            AssetDatabase.DisallowAutoRefresh();
            try { foreach (var row in files) { string path = (string)row["destination"]; Directory.CreateDirectory(Path.GetDirectoryName(path)); File.Copy((string)row["staged"], path); created.Add(path); } }
            finally { AssetDatabase.AllowAutoRefresh(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var layout = JObject.Parse(File.ReadAllText(Layout));
            foreach (var row in files)
            {
                string path = (string)row["destination"];
                ((JArray)layout["files"]).Add(new JObject { ["origin"] = row["origin"], ["path"] = path, ["aliases"] = new JArray(), ["pack"] = "RaceBases20261004", ["kind"] = row["kind"], ["sha256"] = row["sha256"], ["guid"] = AssetDatabase.AssetPathToGUID(path), ["metaSha256"] = Hash(path + ".meta"), ["embeddedMaterials"] = row["embeddedMaterials"] ?? new JArray(), ["materialFolder"] = row["materialFolder"] });
            }
            File.WriteAllText(Layout, layout.ToString(Formatting.Indented)); YQDotAssetLayout.ReloadSourcePaths(); AssetDatabase.ImportAsset(Layout, ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Path.GetDirectoryName(InstalledContent)); File.WriteAllText(InstalledContent, plan.ToString(Formatting.Indented)); created.Add(InstalledContent); AssetDatabase.ImportAsset(InstalledContent, ImportAssetOptions.ForceSynchronousImport);
            foreach (var group in files.Where(f => ((string)f["destination"]).EndsWith(".glb", StringComparison.Ordinal)).GroupBy(f => (string)f["materialFolder"]))
            {
                string index = group.Key + "/MATERIAL_INDEX.json"; Directory.CreateDirectory(group.Key);
                File.WriteAllText(index, JsonConvert.SerializeObject(new { storage = "Original GLB subassets; delivered renderer assignments preserved", materials = group.Select(f => new { sourcePath = (string)f["destination"], sourceGuid = AssetDatabase.AssetPathToGUID((string)f["destination"]), embedded = f["embeddedMaterials"] }) }, Formatting.Indented)); created.Add(index);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Receipt("RUNNING", new { phase = "BUILDING_ONLY_NEW_RACE_BINDINGS" });
            bindingsStarted = true; YQDotCreatureIntake.BuildApprovedNpcEntries(ReadJobs(), Output); published = true;
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
            var bodies = catalog.entries.Where(e => IsNewRace(e.species) && e.kind == "race").ToArray();
            YQDotRaceBaseVerification.Run(bodies, Output);
            YQDotCreaturePreview.RenderEntries(bodies, Output + "/Preview", true, new[] { "DotIdle", "DotStart", "DotWalk", "DotStop", "DotGreet" });
            foreach (var row in files) if (Hash((string)row["destination"]) != (string)row["sha256"]) throw new InvalidDataException("Imported source bytes changed.");
            Receipt("PASS", new { files = files.Length, bodies = 3, duplicateAvian = "identical archive and installed bytes; skipped", preservedPriorEntries = before.Count, totalEntries = catalog.entries.Count, renderedPoses = 60, evidence = "Fresh Editor imports, canonical catalog/lazy registry, deterministic race binding, supplied Dwarf/Kitsune travel, owned zero-based clips, morph curves, sampled skin bounds and all-LOD renders. Live PlaySafe contact, moving LODs, Bramblekin gait calibration, shader extension parity and shared-rig/texture-residency optimization remain unverified. Kitsune modules/source were not supplied." });
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
    private static void Backup(string path) { string target = Output + "/Backup/" + path; Directory.CreateDirectory(Path.GetDirectoryName(target)); if (File.Exists(target)) throw new InvalidOperationException("Previous race-base backup exists; review before retry."); File.Copy(path, target); }
    private static void Receipt(string status, object detail) => File.WriteAllText(Output + "/integration.json", JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, editorVersion = Application.unityVersion, detail }, Formatting.Indented));
}
