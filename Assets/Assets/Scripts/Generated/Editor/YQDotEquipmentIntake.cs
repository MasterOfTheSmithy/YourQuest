using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Explicit incremental intake binds both supplied GLB libraries to approved prefab keys and existing registry shards. Accepted inventory/world records are untouched.
public static class YQDotEquipmentIntake
{
    private const string BaseRoot = "Assets/Assets/GeneratedAssets/DOT Generated Assets/Equipment/Library";
    private const string ExpansionRoot = "Assets/Assets/GeneratedAssets/DOT Generated Assets/Equipment/Modular";
    private const string PrefabRoot = "Assets/YourQuest DOT Equipment";
    private const string CatalogPath = "Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset";
    private const string ReceiptPath = "outputs/DOT_Integration_20261002/intake-receipt.json";
    internal const string CraftingRoot = "Assets/Assets/GeneratedAssets/DOT Generated Assets/Crafting/YourQuest_Monster_Material_Crafting_Starter_Kit_2026-10-03/MonsterCrafting";
    internal const string CraftingReceipt = "outputs/DOT_Integration_20261003/Crafting/intake.json";
    private static string _receiptPath = ReceiptPath;
    private static JObject _craftingMaterials;
    private static JObject _craftingFits;
    private static JArray _craftingRecipes;
    private sealed class Job { public YQDotEquipmentEntry entry; public string[] lods; }
    private static List<Job> _jobs;
    private static List<YQDotEquipmentEntry> _completed;
    private static Dictionary<string, List<YQRuntimeWorldAssetEntry>> _shards;
    private static int _index;
    private static double _nextTick;
    internal static bool IsRunning => _jobs != null;
    internal static bool IsCrafting => _jobs != null && _craftingMaterials != null;

    [MenuItem("YourQuest/DOT Assets/Rebind Moved Equipment Sources")]
    public static void RebindMovedSources()
    {
        if (_jobs != null || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Source rebinding requires idle Edit Mode.");
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Build the existing DOT equipment library first.");
        const string priorRoot = "Assets/Assets/GeneratedAssets/DOT Gen Modular Expansion";
        // note: Preflight every changed source before replacing metadata. Existing prefab/GUID bindings, inventory and production pools stay intact.
        var changes = new Dictionary<YQDotEquipmentEntry, string>();
        foreach (var entry in catalog.entries)
        {
            if (entry == null || entry.sourcePath == null || !entry.sourcePath.StartsWith(priorRoot + "/", StringComparison.Ordinal)) continue;
            string path = YQDotAssetLayout.Resolve(ExpansionRoot + entry.sourcePath.Substring(priorRoot.Length));
            if (!File.Exists(path)) throw new InvalidDataException("Moved equipment source is missing: " + path);
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actual, entry.sourceSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Moved equipment hash mismatch: " + path);
            }
            changes.Add(entry, path);
        }
        foreach (var pair in changes) pair.Key.sourcePath = pair.Value;
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        Debug.Log("[YQ DOT Intake] Rebound moved equipment sources: " + changes.Count);
    }

    [MenuItem("YourQuest/Player Animation/Build DOT Equipment Library")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("DOT intake requires stable Edit Mode.");
        if (_jobs != null) throw new InvalidOperationException("DOT intake is already running.");
        if (!YQRuntimeWorldAssetRegistry.Instance.UsesLazyResourceShards) throw new InvalidOperationException("Existing lazy registry shards are required; do not create a competing registry.");
        _receiptPath = ReceiptPath; _craftingMaterials = null;
        StartJobs(ReadJobs());
    }

    [MenuItem("YourQuest/Player Animation/Build DOT Component Layouts")]
    public static void BeginParts()
    {
        if (_jobs != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("DOT component intake requires stable, unoccupied Edit Mode.");
        var jobs = new List<Job>();
        foreach (JObject row in JArray.Parse(ReadSourceText(BaseRoot + "/CATALOG.json")))
        {
            string relative = (string)row["exports"]?["parts"]?["path"];
            if (string.IsNullOrEmpty(relative)) continue;
            string root = File.Exists(YQDotAssetLayout.Resolve(ExpansionRoot + "/Consumable_Update/" + relative)) ? ExpansionRoot + "/Consumable_Update" : BaseRoot;
            string id = (string)row["export_name"] + "_Parts"; string family = (string)row["family"];
            ValidateName(id); ValidateName(family);
            string path = SafePath(root, relative);
            jobs.Add(new Job { lods = new[] { path }, entry = new YQDotEquipmentEntry {
                assetId = id, family = family, category = (string)row["category"], referenceBody = (string)row["reference_body"],
                sourcePath = path, prefabPath = PrefabRoot + "/" + family + "/" + id + ".prefab",
                sourceSha256 = root == BaseRoot ? (string)row["exports"]?["parts"]?["sha256"] : null,
                bodyMaskJson = row["fitting"]?["body_mask"]?.ToString(Formatting.None), sourceContractJson = row.ToString(Formatting.None)
            }});
        }
        _receiptPath = ReceiptPath; _craftingMaterials = null;
        StartJobs(jobs);
    }

    [MenuItem("YourQuest/DOT Assets/Build Monster Crafting Assets")]
    public static void BeginCrafting()
    {
        if (_jobs != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Crafting intake requires stable, unoccupied Edit Mode.");
        if (!YQRuntimeWorldAssetRegistry.Instance.UsesLazyResourceShards) throw new InvalidOperationException("Production lazy registry is required.");
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Build the base modular equipment catalog first.");
        var hashes = (JObject)JObject.Parse(ReadSourceText(CraftingRoot + "/FILE_CHECKSUMS.json"))["files"];
        // note: Verify delivered contracts before deriving approved bindings; exported geometry is never regenerated or fitted by bounds.
        foreach (string file in new[] { "material_catalog.v1.json", "fit_contracts.v1.json", "visual_manifest.v1.json", "recipe_examples.v1.json" })
            VerifyCraftingHash("Data/" + file, hashes);
        var materials = JObject.Parse(ReadSourceText(CraftingRoot + "/Data/material_catalog.v1.json"));
        var fits = JObject.Parse(ReadSourceText(CraftingRoot + "/Data/fit_contracts.v1.json"));
        var visuals = JObject.Parse(ReadSourceText(CraftingRoot + "/Data/visual_manifest.v1.json"));
        var recipes = JArray.Parse(ReadSourceText(CraftingRoot + "/Data/recipe_examples.v1.json"));
        foreach (var document in new[] { materials, fits, visuals })
            if ((string)document["schema_version"] != "1.0.0") throw new InvalidDataException("Unsupported crafting contract version.");
        foreach (JObject fit in (JArray)fits["fits"])
        {
            string id = (string)fit["asset_id"];
            if (!catalog.TryGetAsset(id, out var source) || source.compatibilityId != (string)fit["compatibility_id"])
                throw new InvalidDataException("Missing or incompatible crafting reference core/module: " + id);
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(source.sourcePath))
                if (!string.Equals(Hash(sha.ComputeHash(stream)), (string)fit["source_sha256"], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Crafting reference revision differs: " + id);
        }
        var families = JObject.Parse(ReadSourceText(ExpansionRoot + "/Weapons/weapon_manifest.json"))["families"];
        var materialKeys = new HashSet<string>(((JArray)materials["materials"]).Select(m => (string)m["material_key"]), StringComparer.Ordinal);
        var jobs = new List<Job>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (bool assembled in new[] { false, true })
        foreach (JObject row in (JArray)visuals[assembled ? "assemblies" : "visuals"])
        {
            string relative = (string)row["file"], id = assembled ? Path.GetFileNameWithoutExtension(relative) : (string)row["visual_id"];
            string family = (string)row["family"], category = (string)row["category"], slot = (string)row["slot"];
            ValidateName(id); ValidateName(family);
            if (!ids.Add(id)) throw new InvalidDataException("Duplicate crafting asset ID: " + id);
            if (category != "weapon" && category != "wearable") throw new InvalidDataException("Unsupported crafting visual category.");
            string materialKey = (string)row["material_key"];
            if (!assembled && !materialKeys.Contains(materialKey)) throw new InvalidDataException("Unknown crafting material: " + materialKey);
            string path = SafePath(CraftingRoot, relative); VerifyCraftingHash(relative, hashes);
            string[] required = assembled || slot == "surface_panel" ? Array.Empty<string>() : category == "weapon" ? families[family]?.ToObject<string[]>() : new[] { "core", "mount", "motif" };
            if (!assembled && slot != "surface_panel" && (required == null || required.Length != 3 || !required.Contains(slot)))
                throw new InvalidDataException("Unsupported crafting module interface: " + id);
            jobs.Add(new Job { lods = new[] { path }, entry = new YQDotEquipmentEntry {
                assetId = id, family = family, category = assembled && category == "weapon" ? "weapon_assembly" : category == "wearable" ? "Wearables" : category,
                referenceBody = (string)row["sex"], moduleSlot = slot, materialKey = materialKey, compatibilityId = (string)row["compatibility_id"],
                requiredModuleSlots = required, assembled = assembled, sourcePath = path, sourceSha256 = (string)hashes[relative],
                prefabPath = PrefabRoot + "/" + family + "/" + id + ".prefab", sourceContractJson = row.ToString(Formatting.None)
            }});
        }
        if (jobs.Count != 50 || materialKeys.Count != 11) throw new InvalidDataException("Incomplete crafting starter-kit delivery.");
        YQDotCraftingVerification.CapturePreservedBindings(catalog, ids);
        _receiptPath = CraftingReceipt; _craftingMaterials = materials; _craftingFits = fits; _craftingRecipes = recipes;
        StartJobs(jobs.OrderBy(j => j.entry.assetId, StringComparer.Ordinal).ToList());
    }

    private static string Hash(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    internal const string CoverageRoot = "Assets/Assets/GeneratedAssets/DOT Generated Assets/Crafting/YourQuest_Monster_Armor_Coverage_Supplement_1.1_2026-10-03/monster_crafting_armor_coverage";
    internal static void BuildArmorCoverage()
    {
        if (_jobs != null || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Coverage intake requires idle Edit Mode.");
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>(CatalogPath);
        var manifest = JObject.Parse(ReadSourceText(CoverageRoot + "/Data/visual_manifest.v1_1.json"));
        if ((string)manifest["schema_version"] != "1.1.0") throw new InvalidDataException("Unsupported armor coverage contract.");
        var hashes = (JObject)JObject.Parse(ReadSourceText(CoverageRoot + "/FILE_CHECKSUMS.json"))["files"];
        var jobs = new List<Job>();
        foreach (bool assembled in new[] { false, true })
        foreach (JObject row in (JArray)manifest[assembled ? "assemblies" : "visuals"])
        {
            string file = (string)row["file"], id = assembled ? Path.GetFileNameWithoutExtension(file) : (string)row["visual_id"];
            ValidateName(id);
            string path = SafePath(CoverageRoot, file), expected = (string)hashes[file];
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                if (string.IsNullOrEmpty(expected) || Hash(sha.ComputeHash(stream)) != expected) throw new InvalidDataException("Coverage checksum mismatch: " + file);
            if (!catalog.TryGetAsset((string)row["source_core_asset_id"], out var core) || core.compatibilityId != (string)row["compatibility_id"]) throw new InvalidDataException("Coverage core interface unavailable: " + id);
            string[] required = ((JArray)row[assembled ? "parts" : "required_material_keys"]).Values<string>().ToArray();
            if (required.Length < 2 || required.Any(k => !catalog.craftingMaterials.Any(m => m.materialKey == k))) throw new InvalidDataException("Coverage anatomical material unavailable: " + id);
            jobs.Add(new Job { lods = new[] { path }, entry = new YQDotEquipmentEntry {
                assetId = id, family = "chest_armor", category = "Wearables", referenceBody = (string)row["sex"], moduleSlot = assembled ? null : "surface_panel",
                materialKey = (string)row["material_key"], requiredMaterialKeys = required, compatibilityId = (string)row["compatibility_id"], assembled = assembled,
                sourcePath = path, sourceSha256 = expected, prefabPath = PrefabRoot + "/chest_armor/" + id + ".prefab", sourceContractJson = row.ToString(Formatting.None)
            }});
        }
        if (jobs.Count != 16) throw new InvalidDataException("Incomplete armor coverage supplement.");
        // note: Reuse the incremental equipment publisher, preserving all earlier records, schema-1.0 recipes, source cores and production pools.
        _craftingMaterials = null; _receiptPath = "outputs/DOT_Integration_20261003/Patches/coverage-intake.json";
        StartJobs(jobs);
        try
        {
            foreach (var job in jobs) { Build(job); _completed.Add(job.entry); _index++; }
            Publish();
            catalog.craftingCoverageSchemaVersion = "1.1.0";
            catalog.craftingCoverageRecipeExamplesJson = ReadSourceText(CoverageRoot + "/Data/recipe_examples.v1_1.json");
            foreach (var material in catalog.craftingMaterials)
                material.visualAssetIds = catalog.entries.Where(e => e.materialKey == material.materialKey || e.requiredMaterialKeys.Contains(material.materialKey)).Select(e => e.assetId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); Finish("PASS", null);
        }
        catch { Finish("FAIL", "Coverage publication failed; see maintenance diagnostics."); throw; }
    }
    private static void VerifyCraftingHash(string relative, JObject hashes)
    {
        string expected = (string)hashes[relative];
        if (string.IsNullOrEmpty(expected)) throw new InvalidDataException("Crafting checksum is missing: " + relative);
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(YQDotAssetLayout.Resolve(CraftingRoot + "/" + relative)))
            if (!string.Equals(Hash(sha.ComputeHash(stream)), expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Crafting checksum mismatch: " + relative);
    }

    private static void StartJobs(List<Job> jobs)
    {
        if (jobs.Count == 0) throw new InvalidOperationException("DOT intake contains no declared assets.");
        foreach (var job in jobs) EnsureFolder(Path.GetDirectoryName(job.entry.prefabPath).Replace('\\', '/'));
        _jobs = jobs;
        var ids = new HashSet<string>(jobs.Select(j => j.entry.assetId), StringComparer.Ordinal);
        var paths = new HashSet<string>(jobs.Select(j => j.entry.prefabPath), StringComparer.OrdinalIgnoreCase);
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>(CatalogPath);
        // note: Each intake replaces only its declared identities; component layouts and main-library reruns preserve one another.
        _completed = catalog != null ? catalog.entries.Where(e => !ids.Contains(e.assetId)).ToList() : new List<YQDotEquipmentEntry>();
        _shards = new Dictionary<string, List<YQRuntimeWorldAssetEntry>>(StringComparer.Ordinal);
        foreach (string shardPath in jobs.Select(j => "Assets/Assets/Resources/" + YQRuntimeWorldAssetRegistry.BuildShardResourcePath(j.entry.prefabPath) + ".asset").Distinct())
        {
            var shard = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(shardPath);
            _shards.Add(shardPath, shard != null ? shard.Entries.Where(e => !paths.Contains(e.assetPath)).ToList() : new List<YQRuntimeWorldAssetEntry>());
        }
        _index = 0; _nextTick = 0d;
        EditorApplication.update += Tick;
        WriteReceipt("RUNNING", null);
    }

    [MenuItem("YourQuest/Player Animation/Stop DOT Equipment Intake")]
    public static void Stop() => Finish("STOPPED", "Intake stopped explicitly; supplied files and completed prefabs are preserved.");

    [MenuItem("YourQuest/Player Animation/Repair DOT Special Weapon LOD Range")]
    public static void RepairSpecialLods()
    {
        if (_jobs != null || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("LOD repair requires idle Edit Mode.");
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Build the DOT catalog first.");
        int changed = 0;
        foreach (var entry in catalog.entries.Where(e => e.assembled && e.category == "Special_Weapons"))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(entry.prefabPath);
            try
            {
                LODGroup group = root.GetComponent<LODGroup>(); if (group == null) continue;
                LOD[] lods = group.GetLODs(); if (lods[lods.Length - 1].screenRelativeTransitionHeight <= .0151f) continue;
                // note: The supplied special weapons have only two real LODs; the final mesh must remain visible beyond the LOD1 transition.
                lods[lods.Length - 1].screenRelativeTransitionHeight = .015f; group.SetLODs(lods);
                PrefabUtility.SaveAsPrefabAsset(root, entry.prefabPath); changed++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("[YQ DOT Intake] Corrected special-weapon LOD culling: " + changed);
    }

    private static void Tick()
    {
        if (_jobs == null || EditorApplication.timeSinceStartup < _nextTick || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        _nextTick = EditorApplication.timeSinceStartup + .03d;
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Finish("STOPPED", "Play Mode began before intake finished."); return; }
        try
        {
            // note: One bounded prefab per Editor update keeps progress observable and avoids an uncancellable modal batch.
            Job job = _jobs[_index]; Build(job); _completed.Add(job.entry); _index++;
            if (_index % 20 == 0) { WriteReceipt("RUNNING", null); Debug.Log("[YQ DOT Intake] " + _index + "/" + _jobs.Count); }
            if (_index == _jobs.Count)
            {
                Publish();
                // note: Fitted armor is deliberately cataloged behind the existing admission gate; successful crafting asset intake is distinct from outfit certification.
                bool crafting = _craftingMaterials != null;
                Finish(crafting ? "PASS" : _completed.Any(e => !e.generationEligible && e.assembled) ? "PARTIAL" : "PASS", null);
                if (crafting) YQDotCraftingVerification.Run();
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish("FAIL", exception.ToString()); }
    }

    private static List<Job> ReadJobs()
    {
        var result = new List<Job>();
        foreach (JObject row in JArray.Parse(ReadSourceText(BaseRoot + "/CATALOG.json")))
        {
            string main = (string)row["exports"]?["main"]?["path"];
            if (string.IsNullOrEmpty(main)) throw new InvalidDataException("Canonical design has no main export.");
            // note: Revised consumables retain the same asset identity; select the supplied revised geometry without overwriting original GLBs or GUIDs.
            string revised = ExpansionRoot + "/Consumable_Update/" + main;
            string root = File.Exists(YQDotAssetLayout.Resolve(revised)) ? ExpansionRoot + "/Consumable_Update" : BaseRoot;
            var entry = new YQDotEquipmentEntry {
                assetId = (string)row["export_name"], family = (string)row["family"], category = (string)row["category"],
                referenceBody = (string)row["reference_body"], sourcePath = SafePath(root, main), assembled = true,
                partsSourcePath = (string)row["exports"]?["parts"]?["path"], bodyMaskJson = row["fitting"]?["body_mask"]?.ToString(Formatting.None),
                sourceSha256 = root == BaseRoot ? (string)row["exports"]?["main"]?["sha256"] : null,
                sourceContractJson = row.ToString(Formatting.None)
            };
            var lods = new List<string> { entry.sourcePath };
            foreach (string name in new[] { "lod1", "lod2" })
            { string path = (string)row["exports"]?[name]?["path"]; if (!string.IsNullOrEmpty(path)) lods.Add(SafePath(root, path)); }
            result.Add(new Job { entry = entry, lods = lods.ToArray() });
        }
        JObject weapons = JObject.Parse(ReadSourceText(ExpansionRoot + "/Weapons/weapon_manifest.json"));
        foreach (JObject row in (JArray)weapons["modules"]) result.Add(ModuleJob(row, weapons["families"]?[((string)row["family"])], false));
        foreach (JObject row in (JArray)weapons["assemblies"]) result.Add(ModuleJob(row, null, true));
        foreach (string manifest in new[] { "static_module_manifest.json", "male_module_manifest.json", "female_module_manifest.json" })
        {
            JObject source = JObject.Parse(ReadSourceText(ExpansionRoot + "/" + manifest));
            var byId = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (JObject row in (JArray)source["modules"])
            {
                var job = ModuleJob(row, new JArray("core", "mount", "motif"), false); result.Add(job);
                byId.Add(job.entry.assetId, row);
            }
            foreach (JObject example in (JArray)source["mixed_examples"])
            {
                string firstId = (string)((JArray)example["modules"])[0]; JObject core = byId[firstId];
                var copy = (JObject)example.DeepClone(); copy["family"] = core["family"]; copy["category"] = core["category"]; copy["sex"] = core["sex"];
                result.Add(ModuleJob(copy, null, true));
            }
        }
        // note: Every path is confined to one supplied folder; duplicate names cannot quietly replace another design.
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var job in result)
        {
            ValidateName(job.entry.assetId); ValidateName(job.entry.family);
            if (!ids.Add(job.entry.assetId)) throw new InvalidDataException("Duplicate DOT asset ID: " + job.entry.assetId);
            job.entry.prefabPath = PrefabRoot + "/" + job.entry.family + "/" + job.entry.assetId + ".prefab";
        }
        return result.OrderBy(j => j.entry.family, StringComparer.Ordinal).ThenBy(j => j.entry.assetId, StringComparer.Ordinal).ToList();
    }

    private static Job ModuleJob(JObject row, JToken required, bool assembled)
    {
        string file = (string)row["file"];
        var paths = new List<string>();
        if (!string.IsNullOrEmpty(file)) paths.Add(SafePath(ExpansionRoot, file));
        else foreach (JObject lod in (JArray)row["lods"]) paths.Add(SafePath(ExpansionRoot, (string)lod["path"]));
        string slot = (string)row["slot"];
        if (!assembled && string.IsNullOrEmpty(slot)) slot = "core";
        return new Job { lods = paths.ToArray(), entry = new YQDotEquipmentEntry {
            assetId = (string)row["asset_id"], family = (string)row["family"], category = (string)row["category"], referenceBody = (string)row["sex"],
            sourcePath = paths[0], moduleSlot = slot, compatibilityId = (string)row["compatibility_id"], style = (string)(row["style"] ?? row["visual_style"]),
            requiredModuleSlots = required?.ToObject<string[]>() ?? Array.Empty<string>(), assembled = assembled,
            sourceContractJson = row.ToString(Formatting.None)
        }};
    }

    private static string SafePath(string root, string relative)
    {
        string full = Path.GetFullPath(Path.Combine(root, relative ?? string.Empty));
        string allowed = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        // note: Reject traversal in the original contract before looking up its exact relocated source.
        if (!full.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || !full.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Missing or out-of-root DOT GLB: " + relative);
        string relocated = YQDotAssetLayout.Resolve(full);
        if (!File.Exists(relocated)) throw new InvalidDataException("Missing declared DOT GLB: " + relocated);
        return relocated;
    }

    // note: Resolve source locations without rewriting delivered contracts or rebuilding production asset bindings.
    private static string ReadSourceText(string path) => File.ReadAllText(YQDotAssetLayout.Resolve(path));
    internal static int VerifySourcePaths() => ReadJobs().Count;

    private static void ValidateName(string value)
    {
        // note: Manifest identities become owned asset filenames only after traversal and invalid-character rejection.
        if (string.IsNullOrWhiteSpace(value) || value.Contains("..") || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("Invalid DOT asset/family name: " + value);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void Build(Job job)
    {
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(job.entry.sourcePath))
        {
            string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            if (!string.IsNullOrEmpty(job.entry.sourceSha256) && !string.Equals(actual, job.entry.sourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Delivered source hash mismatch: " + job.entry.sourcePath);
            job.entry.sourceSha256 = actual;
        }
        Scene scene = EditorSceneManager.NewPreviewScene(); GameObject root = null;
        try
        {
            root = new GameObject(job.entry.assetId); SceneManager.MoveGameObjectToScene(root, scene);
            var lods = new List<LOD>(); bool skinned = false;
            for (int index = 0; index < job.lods.Length; index++)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(job.lods[index]);
                if (source == null)
                {
                    // note: Retry a previously failed GLB once after the importer dependency restart; surface a persistent error instead of looping.
                    AssetDatabase.ImportAsset(job.lods[index], ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    source = AssetDatabase.LoadAssetAtPath<GameObject>(job.lods[index]);
                }
                if (source == null) throw new InvalidDataException("GLB importer has not produced a model: " + job.lods[index]);
                GameObject child = UnityEngine.Object.Instantiate(source); SceneManager.MoveGameObjectToScene(child, scene);
                child.name = "LOD" + index; child.transform.SetParent(root.transform, false);
                foreach (Animator extra in child.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(extra);
                Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidDataException("GLB has no renderable model: " + job.lods[index]);
                foreach (Renderer renderer in renderers)
                {
                    skinned |= renderer is SkinnedMeshRenderer;
                    foreach (Material material in renderer.sharedMaterials)
                        if (material == null || material.shader == null || !material.shader.isSupported || material.shader.name.Contains("InternalError"))
                            throw new InvalidDataException("Unusable GLB material: " + job.lods[index]);
                }
                lods.Add(new LOD(index == job.lods.Length - 1 ? .015f : index == 0 ? .55f : .22f, renderers));
            }
            if (lods.Count > 1) { var group = root.AddComponent<LODGroup>(); group.SetLODs(lods.ToArray()); group.RecalculateBounds(); }
            // note: Save new project-owned wrappers around imported mesh/material references. Existing prefab GUIDs remain stable on a resumed intake.
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, job.entry.prefabPath);
            if (prefab == null) throw new InvalidOperationException("DOT prefab publication failed: " + job.entry.prefabPath);
            job.entry.generationEligible = job.entry.assembled && !skinned && string.IsNullOrEmpty(job.entry.referenceBody) && SupportedPool(job.entry) != null;
            job.entry.eligibilityReason = job.entry.generationEligible ? "Imported rigid assembly with valid materials and a supported item visual pool." :
                !job.entry.assembled ? string.IsNullOrEmpty(job.entry.moduleSlot) ? "Delivered component layout; preserve authored parts and transforms. Not a generic socket recipe." : "Compatible assembly module; select only through its declared interface." :
                skinned || !string.IsNullOrEmpty(job.entry.referenceBody) ? "Fitted garment requires shared skeleton, morph, body-mask and outfit verification before generation admission." : "No production item slot for this family.";
            string shard = "Assets/Assets/Resources/" + YQRuntimeWorldAssetRegistry.BuildShardResourcePath(job.entry.prefabPath) + ".asset";
            if (!_shards.TryGetValue(shard, out var entries)) _shards.Add(shard, entries = new List<YQRuntimeWorldAssetEntry>());
            entries.Add(new YQRuntimeWorldAssetEntry { assetPath = job.entry.prefabPath, prefab = prefab });
        }
        finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static string SupportedPool(YQDotEquipmentEntry entry)
    {
        if (entry.category == "Weapons" || entry.category == "Special_Weapons" || entry.category == "weapon_assembly") return entry.family == "arrow" ? null : "weaponPrefabKeys";
        if (entry.family == "shield") return "offhandPrefabKeys";
        if (entry.category == "Consumables") return "consumablePrefabKeys";
        if (entry.family == "ring") return "ringPrefabKeys";
        if (entry.family == "amulet") return "necklacePrefabKeys";
        if (entry.category == "Accessories") return "trinketPrefabKeys";
        return null;
    }

    private static void Publish()
    {
        // note: Preflight all production pools before publishing the owned catalog or registry shards.
        var library = AssetDatabase.LoadAssetAtPath<GeneratedRpgContentLibrary>("Assets/Assets/Resources/GeneratedRpgContentLibrary.asset");
        if (library == null) throw new InvalidOperationException("Production item library is missing.");
        var serialized = new SerializedObject(library);
        foreach (string pool in _completed.Where(e => e.generationEligible).Select(SupportedPool).Distinct())
            if (serialized.FindProperty(pool) == null) throw new InvalidOperationException("Missing production visual pool: " + pool);
        foreach (var pair in _shards)
        {
            EnsureFolder(Path.GetDirectoryName(pair.Key).Replace('\\', '/'));
            var registry = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(pair.Key);
            if (registry == null) { registry = ScriptableObject.CreateInstance<YQRuntimeWorldAssetRegistry>(); AssetDatabase.CreateAsset(registry, pair.Key); }
            // note: This shard prefix belongs exclusively to DOT equipment; the production root and all existing pack shards remain unchanged.
            registry.SetEntries(pair.Value); EditorUtility.SetDirty(registry);
        }
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>(CatalogPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<YQDotEquipmentCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
        catalog.entries = _completed; catalog.RebuildLookup(); EditorUtility.SetDirty(catalog);
        if (_craftingMaterials != null)
        {
            // note: Keep original harvest, quality and recipe contracts as queryable reference data. This publication cannot award loot or mutate inventory.
            catalog.craftingSchemaVersion = (string)_craftingMaterials["schema_version"];
            catalog.craftingFitContractsJson = _craftingFits.ToString(Formatting.None);
            catalog.craftingRecipeExamplesJson = _craftingRecipes.ToString(Formatting.None);
            catalog.craftingQualityPolicyJson = _craftingMaterials["quality_policy"].ToString(Formatting.None);
            catalog.craftingMaterials = ((JArray)_craftingMaterials["materials"]).OfType<JObject>().Select(row => new YQDotCraftingMaterial {
                materialKey = (string)row["material_key"], species = (string)row["species"], partType = (string)row["part_type"], displayName = (string)row["display_name"],
                sourceContractJson = row.ToString(Formatting.None), visualAssetIds = _completed.Where(e => e.materialKey == (string)row["material_key"]).Select(e => e.assetId).OrderBy(id => id, StringComparer.Ordinal).ToArray()
            }).ToList();
        }
        foreach (var group in _completed.Where(e => e.generationEligible).GroupBy(SupportedPool))
        {
            SerializedProperty array = serialized.FindProperty(group.Key);
            if (array == null || !array.isArray) throw new InvalidOperationException("Missing production visual pool: " + group.Key);
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < array.arraySize; i++) existing.Add(array.GetArrayElementAtIndex(i).stringValue);
            foreach (var entry in group) if (existing.Add(entry.prefabPath)) { int index = array.arraySize++; array.GetArrayElementAtIndex(index).stringValue = entry.prefabPath; }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
    }

    private static void Finish(string status, string error)
    { EditorApplication.update -= Tick; if (_jobs != null) WriteReceipt(status, error); _jobs = null; Debug.Log("[YQ DOT Intake] " + status + ": " + error); }
    private static void WriteReceipt(string status, string error)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_receiptPath));
        File.WriteAllText(_receiptPath, JsonConvert.SerializeObject(new { status, utc = DateTime.UtcNow, processed = _index, total = _jobs?.Count ?? 0,
            generationEligible = _completed?.Count(e => e.generationEligible) ?? 0, entries = _completed, error,
            evidence = "Explicit Editor GLB/prefab/material intake. Fitted garment admission and runtime behavior require their separate checks." }, Formatting.Indented));
    }
}
