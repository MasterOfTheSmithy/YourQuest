using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// note: A reviewed, direct R2 corrective intake changes source bytes and owned loop curves without reconstructing user-arranged prefabs/controllers or changing actor ownership.
[InitializeOnLoad]
public static class YQDotWildlifeCorrectivePatch
{
    private const string Root = YQDotAssetLayout.Root;
    private const string Output = "outputs/DOT_Integration_20261003/Wildlife_R3_R4";
    private const string Request = "Assets/Assets/EditorBuildRequests/ApplyDotWildlifeR3R4.request";
    private const string Catalog = "Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset";
    private const string Layout = Root + "/Catalogs/DOT_ASSET_LAYOUT.json";
    private const string Index = Root + "/Catalogs/DOT_ASSET_INDEX.json";
    private static double _next;
    static YQDotWildlifeCorrectivePatch() { EditorApplication.update += Tick; }

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
        var backups = new HashSet<string>(StringComparer.Ordinal);
        var created = new List<string>();
        EditorApplication.LockReloadAssemblies();
        try
        {
            var plan = JObject.Parse(File.ReadAllText(Output + "/plan.json"));
            if ((string)plan["status"] != "PREFLIGHT_PASS" || (string)JObject.Parse(File.ReadAllText(Output + "/source-contracts.json"))["status"] != "PASS") throw new InvalidDataException("Exact source preflight required.");
            var changes = ((JArray)plan["changes"]).OfType<JObject>().ToArray();
            var additions = ((JArray)plan["additions"]).OfType<JObject>().ToArray();
            if (changes.Length != 15) throw new InvalidDataException("Expected fifteen replacements.");
            var catalog = AssetDatabase.LoadAssetAtPath<YQDotCreatureCatalog>(Catalog);
            var animals = catalog.entries.Where(e => new[] { "horse", "cow", "deer" }.Contains(e.species) && e.kind == "wildlife").ToArray();
            if (animals.Length != 3) throw new InvalidDataException("Expected existing three wildlife bindings.");
            foreach (var row in changes)
            {
                string path = (string)row["destination"]; Confine(path);
                if (Hash(path) != (string)row["oldSha256"] || Hash((string)row["staged"]) != (string)row["newSha256"] || AssetDatabase.AssetPathToGUID(path) != (string)row["guid"]) throw new InvalidDataException("Baseline changed after review: " + path);
                Backup(path, backups); Backup(path + ".meta", backups);
            }
            foreach (var row in additions)
            {
                string path = (string)row["destination"]; Confine(path);
                if (File.Exists(path) || Hash((string)row["staged"]) != (string)row["newSha256"]) throw new InvalidDataException("Addition changed after review: " + path);
            }
            Backup(Catalog, backups); Backup(Catalog + ".meta", backups); Backup(Layout, backups); Backup(Index, backups);
            var glbs = changes.Where(r => ((string)r["destination"]).EndsWith(".glb", StringComparison.Ordinal)).Select(r => (string)r["destination"]).ToArray();
            var identities = glbs.ToDictionary(p => p, Subassets);
            // note: Runtime keys can remain historical aliases after the user moves a prefab folder; follow the registry's serialized object, not an invented physical path.
            var prefabs = animals.ToDictionary(e => e.assetId, e => YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(e.prefabPath));
            if (prefabs.Values.Any(p => p == null)) throw new InvalidDataException("Missing existing registry prefab.");
            var protectedPaths = prefabs.Values.SelectMany(p => Directory.GetFiles(Path.GetDirectoryName(AssetDatabase.GetAssetPath(p)), "*", SearchOption.TopDirectoryOnly)).Select(p => p.Replace('\\', '/')).Distinct().ToArray();
            foreach (string path in protectedPaths) Backup(path, backups);
            var immutable = protectedPaths.Where(p => !p.EndsWith(".anim", StringComparison.Ordinal)).ToDictionary(p => p, Hash);
            string library = "Assets/Assets/Resources/GeneratedRpgContentLibrary.asset", libraryHash = Hash(library);
            File.WriteAllText(Output + "/import-before.json", JsonConvert.SerializeObject(identities, Formatting.Indented));
            Receipt("RUNNING", new { phase = "REPLACING_MATCHED_R2_FILES", files = changes.Length });
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                foreach (var row in changes) File.Copy((string)row["staged"], (string)row["destination"], true);
                foreach (var row in additions)
                {
                    string path = (string)row["destination"]; Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.Copy((string)row["staged"], path); created.Add(path);
                }
            }
            finally { AssetDatabase.AllowAutoRefresh(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in glbs) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in glbs) if (JsonConvert.SerializeObject(Subassets(path)) != JsonConvert.SerializeObject(identities[path])) throw new InvalidDataException("Imported identities changed: " + path);
            File.WriteAllText(Output + "/import-after.json", JsonConvert.SerializeObject(glbs.ToDictionary(p => p, Subassets), Formatting.Indented));
            var refreshedClips = new List<string>(); var events = new List<object>();
            foreach (var animal in animals)
            {
                var prefab = prefabs[animal.assetId];
                for (int lod = 0; lod < animal.sourcePaths.Length; lod++)
                {
                    var animator = prefab.transform.Find("LOD" + lod).GetComponentInChildren<Animator>(true);
                    var controller = animator.runtimeAnimatorController as AnimatorController;
                    if (controller == null || animator.applyRootMotion) throw new InvalidDataException("Existing in-place controller required.");
                    var imported = AssetDatabase.LoadAllAssetsAtPath(animal.sourcePaths[lod]).OfType<AnimationClip>().ToArray();
                    foreach (var state in controller.layers[0].stateMachine.states)
                    {
                        var clip = state.state.motion as AnimationClip;
                        if (clip == null) throw new InvalidDataException("Missing controller motion.");
                        var markers = AnimationUtility.GetAnimationEvents(clip);
                        if (markers.Length > 0) events.Add(new { animal.species, lod, state = state.state.name, functions = markers.Select(m => m.functionName) });
                        if (state.state.name != "DotIdle" && state.state.name != "DotWalk") continue;
                        string clipPath = AssetDatabase.GetAssetPath(clip);
                        if (!clipPath.EndsWith(".anim", StringComparison.Ordinal) || !backups.Contains(clipPath)) throw new InvalidDataException("Existing owned loop clip required: " + clipPath);
                        var source = imported.Single(c => state.state.name == "DotIdle" ? c.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 : c.name.IndexOf("walk_loop", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("locomotion_loop", StringComparison.OrdinalIgnoreCase) >= 0);
                        // note: Refresh delivered curves while retaining the existing asset identity, loop settings and user callbacks; imported start/stop Motion references remain untouched.
                        var settings = AnimationUtility.GetAnimationClipSettings(clip); string name = clip.name;
                        EditorUtility.CopySerialized(source, clip); clip.name = name; clip.legacy = false;
                        AnimationUtility.SetAnimationClipSettings(clip, settings); AnimationUtility.SetAnimationEvents(clip, markers);
                        EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip); refreshedClips.Add(clipPath);
                    }
                }
                animal.sourceHashes = animal.sourcePaths.Select(Hash).ToArray();
            }
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            PublishIndexes(changes, additions);
            foreach (var pair in immutable) if (Hash(pair.Key) != pair.Value) throw new InvalidDataException("Prefab/controller/importer changed: " + pair.Key);
            foreach (var row in changes)
            {
                string path = (string)row["destination"];
                if (Hash(path) != (string)row["newSha256"] || AssetDatabase.AssetPathToGUID(path) != (string)row["guid"] || Hash(path + ".meta") != Hash(Output + "/Backup/" + path + ".meta")) throw new InvalidDataException("Payload or importer preservation failed: " + path);
            }
            if (Hash(library) != libraryHash) throw new InvalidDataException("Unrelated production item pools changed.");
            Receipt("RUNNING", new { phase = "VERIFYING_IMPORTED_SKIN_CURVES_AND_TRAVEL" });
            YQDotPatchVerification.Run(catalog, null, true, Output);
            Receipt("PASS", new { revision = (string)plan["revision"], replacedFiles = 15, importedGlbs = 9, refreshedLoopClips = refreshedClips, sourceOnly = additions.Where(r => ((string)r["destination"]).StartsWith("SourceAssets/", StringComparison.Ordinal)).Select(r => (string)r["destination"]), preservedPrefabControllerAndMetaFiles = immutable.Count, animationEvents = events, hoofEventReview = events.Count == 0 ? "No installed animation callbacks; no event migration required." : "Existing callbacks retained; hoof-specific markers require review against the supplied contact tables.", evidence = "Fresh Unity Editor import identities, exact meta/prefab/controller preservation, evaluated skin/curve/travel and rendered-pose fixtures. Production PlaySafe contact, physics and LOD switching remain unverified." });
        }
        catch (Exception exception)
        {
            // note: Restore only this operation's exact backed-up files after a rejected import/fixture; user changes outside this patch are untouched.
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                foreach (string path in backups) File.Copy(Output + "/Backup/" + path, path, true);
                foreach (string path in created) { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".meta")) File.Delete(path + ".meta"); }
            }
            finally { AssetDatabase.AllowAutoRefresh(); }
            YQDotAssetLayout.ReloadSourcePaths(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Receipt("FAIL_ROLLED_BACK", new { error = exception.ToString(), restoredFiles = backups.Count }); Debug.LogException(exception);
        }
        finally { EditorApplication.UnlockReloadAssemblies(); }
    }

    private static void PublishIndexes(JObject[] changes, JObject[] additions)
    {
        // note: Layout hashes are historical intake evidence; record corrective revision hashes separately while the current inventory tracks exact installed bytes.
        var layout = JObject.Parse(File.ReadAllText(Layout));
        foreach (var row in changes)
        {
            var source = ((JArray)layout["files"]).OfType<JObject>().Single(f => (string)f["path"] == (string)row["destination"]);
            source["installedRevision"] = "UNGULATE-MOTION-R3-STAG-SKIN-R4-20261003"; source["installedSha256"] = row["newSha256"];
        }
        File.WriteAllText(Layout, layout.ToString(Formatting.Indented));
        AssetDatabase.ImportAsset(Layout, ImportAssetOptions.ForceSynchronousImport);
        var index = JObject.Parse(File.ReadAllText(Index)); var rows = (JArray)index["files"];
        var updated = changes.Concat(additions.Where(r => ((string)r["destination"]).StartsWith(Root + "/", StringComparison.Ordinal))).Select(r => (string)r["destination"]).Concat(new[] { Layout });
        foreach (string path in updated)
        {
            var row = rows.OfType<JObject>().SingleOrDefault(r => (string)r["path"] == path);
            if (row == null) { row = new JObject(); rows.Add(row); }
            row["path"] = path; row["sha256"] = Hash(path); row["guid"] = AssetDatabase.AssetPathToGUID(path); row["category"] = path.Substring(Root.Length + 1).Split('/')[0];
        }
        index["files"] = new JArray(rows.OfType<JObject>().OrderBy(r => (string)r["path"], StringComparer.Ordinal)); index["fileCount"] = rows.Count; index["utc"] = DateTime.UtcNow;
        File.WriteAllText(Index, index.ToString(Formatting.Indented)); AssetDatabase.ImportAsset(Index, ImportAssetOptions.ForceSynchronousImport);
    }
    private static Dictionary<string, long> Subassets(string path) => AssetDatabase.LoadAllAssetsAtPath(path).Where(a => a != null).ToDictionary(a => a.GetType().FullName + "|" + a.name, a => { if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string guid, out long id)) throw new InvalidDataException("Missing imported identity."); return id; });
    private static string Hash(string path) { using (var algorithm = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    private static void Backup(string path, HashSet<string> files) { if (!File.Exists(path)) throw new FileNotFoundException(path); string target = Output + "/Backup/" + path; Directory.CreateDirectory(Path.GetDirectoryName(target)); if (File.Exists(target)) throw new InvalidOperationException("Prior backup exists; review before retry: " + path); File.Copy(path, target); files.Add(path); }
    private static void Confine(string path) { string full = Path.GetFullPath(path), project = Path.GetFullPath(".") + Path.DirectorySeparatorChar; if (!full.StartsWith(project, StringComparison.OrdinalIgnoreCase) || (!path.StartsWith(Root + "/", StringComparison.Ordinal) && !path.StartsWith("SourceAssets/DOT/Wildlife/Stag/R4/", StringComparison.Ordinal))) throw new InvalidDataException("Patch destination escapes approved scope: " + path); }
    private static void Receipt(string status, object detail) => File.WriteAllText(Output + "/integration.json", JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, editorVersion = Application.unityVersion, detail }, Formatting.Indented));
}
