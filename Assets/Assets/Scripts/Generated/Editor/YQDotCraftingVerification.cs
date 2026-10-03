using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Explicit asset intake and disposable fixtures exercise existing equipment binding without introducing blacksmith, inventory or save ownership.
[InitializeOnLoad]
public static class YQDotCraftingVerification
{
    private const string Request = "Assets/Assets/EditorBuildRequests/BuildDotCrafting.request";
    private const string VerifyRequest = "Assets/Assets/EditorBuildRequests/VerifyDotCrafting.request";
    private const string Output = "outputs/DOT_Integration_20261003/Crafting";
    private const string CatalogPath = "Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset";
    private const string LibraryPath = "Assets/Assets/Resources/GeneratedRpgContentLibrary.asset";
    private static double _next;

    static YQDotCraftingVerification()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            // note: Resume only an explicitly running crafting request after compilation, preserving prefab GUIDs and other equipment entries.
            if (YQDotEquipmentIntake.IsCrafting) File.WriteAllText(Request, "Resume the requested monster crafting asset intake.");
        };
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < _next || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || YQDotEquipmentIntake.IsRunning) return;
        _next = EditorApplication.timeSinceStartup + .5d;
        if (!File.Exists(Request) && !File.Exists(VerifyRequest)) return;
        try
        {
            // note: A workspace request is a bounded fallback for installations without Unity MCP; never delete unrelated Editor requests.
            if (File.Exists(VerifyRequest)) { Consume(VerifyRequest); Run(); }
            else { Consume(Request); YQDotEquipmentIntake.BeginCrafting(); }
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/request-error.json", JsonConvert.SerializeObject(new { status = "FAIL", utc = DateTime.UtcNow, error = exception.ToString() }, Formatting.Indented));
            Debug.LogException(exception);
        }
    }

    private static void Consume(string path)
    {
        if (!AssetDatabase.DeleteAsset(path)) { File.Delete(path); if (File.Exists(path + ".meta")) File.Delete(path + ".meta"); }
    }

    private static string CanonicalEntryJson(string json)
    {
        // note: Treat Unity's null-to-empty string serialization as equivalent while retaining every declared field and all nonempty metadata.
        var row = JObject.Parse(json);
        foreach (var property in row.Properties()) if (property.Value.Type == JTokenType.Null) property.Value = string.Empty;
        return row.ToString(Formatting.None);
    }

    internal static void CapturePreservedBindings(YQDotEquipmentCatalog catalog, HashSet<string> replacedIds)
    {
        // note: Snapshot actual existing identities and pool membership before publication; verify preservation rather than relying on historical receipts.
        Directory.CreateDirectory(Output);
        var entries = catalog.entries.Where(e => !replacedIds.Contains(e.assetId)).Select(e => new {
            e.assetId, json = JsonConvert.SerializeObject(e), guid = AssetDatabase.AssetPathToGUID(e.prefabPath)
        });
        File.WriteAllText(Output + "/preserved-bindings.json", JsonConvert.SerializeObject(new { entries, pools = ReadPools() }, Formatting.Indented));
    }

    private static Dictionary<string, string[]> ReadPools()
    {
        var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<GeneratedRpgContentLibrary>(LibraryPath));
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (string pool in new[] { "weaponPrefabKeys", "offhandPrefabKeys", "consumablePrefabKeys", "ringPrefabKeys", "necklacePrefabKeys", "trinketPrefabKeys", "chestPrefabKeys" })
        {
            var array = serialized.FindProperty(pool);
            if (array == null || !array.isArray) throw new InvalidDataException("Missing production pool: " + pool);
            result.Add(pool, Enumerable.Range(0, array.arraySize).Select(i => array.GetArrayElementAtIndex(i).stringValue).ToArray());
        }
        return result;
    }

    [MenuItem("YourQuest/DOT Assets/Verify Monster Crafting Assets")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || YQDotEquipmentIntake.IsRunning) throw new InvalidOperationException("Crafting verification requires idle Edit Mode.");
        Directory.CreateDirectory(Output);
        var checks = new List<string>(); string error = null; int recipeFixtures = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var catalog = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>(CatalogPath); catalog.RebuildLookup();
            // note: Pack membership follows recorded provenance after locations are separated by material and equipment type.
            var entries = catalog.entries.Where(e => YQDotAssetLayout.IsFromPack(e.sourcePath, YQDotEquipmentIntake.CraftingRoot)).ToArray();
            var pools = ReadPools(); var admitted = new HashSet<string>(pools.Values.SelectMany(p => p), StringComparer.OrdinalIgnoreCase);
            Require(entries.Length == 50 && entries.Count(e => e.generationEligible) == 5, "50 registered assets; exactly five rigid weapons admitted", checks);
            Require(catalog.craftingSchemaVersion == "1.0.0" && catalog.craftingMaterials.Count == 11, "11 supplied harvest identities retained", checks);
            Require(JArray.Parse(catalog.craftingRecipeExamplesJson).Count == 7 && JObject.Parse(catalog.craftingFitContractsJson)["fits"].Count() == 17, "Seven reference recipes and 17 exact fit contracts retained", checks);
            foreach (var material in catalog.craftingMaterials)
            {
                Require(catalog.TryGetCraftingMaterial(material.species, material.partType, out var resolved) && resolved == material, material.materialKey + ": exact identity lookup", checks);
                Require(material.visualAssetIds.Length > 0 && material.visualAssetIds.All(id => catalog.TryGetAsset(id, out var e) && e.materialKey == material.materialKey), material.materialKey + ": approved visual IDs", checks);
            }
            Require(!catalog.TryGetCraftingMaterial("unknown", "slate_plate", out _), "Unknown harvest identity rejected", checks);
            foreach (var entry in entries)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
                Require(prefab != null && prefab.GetComponentsInChildren<Renderer>(true).Length > 0, entry.assetId + ": imported geometry", checks);
                Require(prefab.GetComponentsInChildren<Animator>(true).Length == 0, entry.assetId + ": presentation only", checks);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    Require(renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported && !m.shader.name.Contains("InternalError")), entry.assetId + ": authored materials", checks);
                string shardPath = "Assets/Assets/Resources/" + YQRuntimeWorldAssetRegistry.BuildShardResourcePath(entry.prefabPath) + ".asset";
                var shard = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(shardPath);
                Require(shard != null && shard.Entries.Any(e => e.assetPath == entry.prefabPath && e.prefab == prefab), entry.assetId + ": serialized build reference", checks);
                Require(admitted.Contains(entry.prefabPath) == entry.generationEligible, entry.assetId + ": admission matches production pools", checks);
                if (entry.referenceBody == "Male" || entry.referenceBody == "Female")
                    Require(!entry.generationEligible, entry.assetId + ": fitted armor gate preserved", checks);
            }
            // note: Each crafted weapon part is tested with the delivered neutral siblings through the real runtime assembly API.
            foreach (var entry in entries.Where(e => !e.assembled && e.category == "weapon"))
            {
                var recipe = entry.requiredModuleSlots.Select(slot => slot == entry.moduleSlot ? entry.assetId : "yqmod1_weapon_" + entry.family + "_" + slot + "_forged").ToArray();
                Require(catalog.TryValidateModules(recipe, out string reason), entry.assetId + ": exact mixed recipe " + reason, checks);
                Require(!catalog.TryValidateModules(recipe.Take(2).ToArray(), out _) && !catalog.TryValidateModules(recipe.Concat(new[] { recipe[0] }).ToArray(), out _), entry.assetId + ": incomplete and duplicate rejected", checks);
                var parent = new GameObject("Crafting binding fixture"); SceneManager.MoveGameObjectToScene(parent, scene);
                try
                {
                    Require(catalog.TryCreateRigidAssembly(recipe, parent.transform, out var assembly, out reason), entry.assetId + ": runtime rigid binding " + reason, checks);
                    Require(assembly.transform.childCount == 3, entry.assetId + ": complete geometry", checks);
                    foreach (Transform child in assembly.transform)
                        Require(child.localPosition == Vector3.zero && child.localRotation == Quaternion.identity && child.localScale == Vector3.one, entry.assetId + ": authored frame", checks);
                    recipeFixtures++;
                }
                finally { UnityEngine.Object.DestroyImmediate(parent); }
            }
            foreach (var group in entries.Where(e => !e.assembled && e.moduleSlot != "surface_panel" && !string.IsNullOrEmpty(e.referenceBody)).GroupBy(e => e.referenceBody + "|" + e.assetId.Split('_')[0]))
            {
                var first = group.First();
                var recipe = new[] { "yqmod1_wearables_chest_armor_" + first.referenceBody.ToLowerInvariant() + "_core" }.Concat(group.Select(e => e.assetId)).ToArray();
                Require(catalog.TryValidateModules(recipe, out string reason), group.Key + ": exact armor interface " + reason, checks);
                Require(!catalog.TryCreateRigidAssembly(recipe, null, out _, out _), group.Key + ": rigid binder rejects fitted skin", checks); recipeFixtures++;
            }
            foreach (var entry in entries.Where(e => e.moduleSlot == "surface_panel"))
                Require(entry.requiredModuleSlots.Length == 0 && !catalog.TryValidateModules(new[] { entry.assetId }, out _), entry.assetId + ": specialized surface extension is not a generic mount recipe", checks);
            Require(YQDotEquipmentCatalog.TryResolvePaletteRemainder(new[] { .6f, .3f }, out float neutral, out _) && Mathf.Abs(neutral - .1f) < .00001f, "Element palette preserves neutral remainder", checks);
            if (File.Exists(Output + "/preserved-bindings.json"))
            {
                var baseline = JObject.Parse(File.ReadAllText(Output + "/preserved-bindings.json"));
                foreach (JObject row in (JArray)baseline["entries"])
                {
                    Require(catalog.TryGetAsset((string)row["assetId"], out var entry) && CanonicalEntryJson(JsonConvert.SerializeObject(entry)) == CanonicalEntryJson((string)row["json"]) && AssetDatabase.AssetPathToGUID(entry.prefabPath) == (string)row["guid"], (string)row["assetId"] + ": prior identity metadata and GUID preserved", checks);
                }
                foreach (var pool in (JObject)baseline["pools"])
                    Require(((JArray)pool.Value).Values<string>().All(value => pools[pool.Key].Contains(value)), pool.Key + ": prior membership preserved", checks);
            }
            Require(YQDotEquipmentPreview.RenderEntries(entries.Where(e => e.assembled), Output + "/Preview"), "17 assembled models rendered with supplied materials", checks);
        }
        catch (Exception exception) { error = exception.ToString(); Debug.LogException(exception); }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        File.WriteAllText(Output + "/verification.json", JsonConvert.SerializeObject(new { status = error == null ? "PASS" : "FAIL", utc = DateTime.UtcNow, recipeFixtures, checks, error,
            evidence = "Fresh Unity Editor import/reference/material, exact-interface and disposable runtime binder fixtures. Fitted player armor, ordinary equipment gameplay and blacksmith transactions are not certified." }, Formatting.Indented));
        Debug.Log("[YQ DOT Crafting] " + (error == null ? "PASS" : "FAIL") + ": " + checks.Count + " checks.");
    }

    private static void Require(bool condition, string label, List<string> checks)
    { if (!condition) throw new InvalidDataException(label); checks.Add(label); }
}
