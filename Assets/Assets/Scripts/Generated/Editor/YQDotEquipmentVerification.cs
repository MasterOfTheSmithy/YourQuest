using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Verify asset publication independently from garment fit or ordinary gameplay acceptance.
public static class YQDotEquipmentVerification
{
    [MenuItem("YourQuest/Player Animation/Verify DOT Equipment Library")]
    public static void Run()
    {
        var checks = new List<string>();
        string error = null;
        Scene preview = EditorSceneManager.NewPreviewScene();
        var catalog = AssetDatabase.LoadAssetAtPath<YQDotEquipmentCatalog>("Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset");
        try
        {
            Require(catalog != null && catalog.entries.Count > 0, "Published catalog", checks);
            catalog.RebuildLookup();
            foreach (var entry in catalog.entries)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
                Require(prefab != null, entry.assetId + ": prefab", checks);
                Require(prefab.GetComponentsInChildren<Animator>(true).Length == 0, entry.assetId + ": no competing Animator", checks);
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                Require(renderers.Length > 0 && renderers.All(r => r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported)), entry.assetId + ": materials", checks);
                string shardPath = "Assets/Assets/Resources/" + YQRuntimeWorldAssetRegistry.BuildShardResourcePath(entry.prefabPath) + ".asset";
                var shard = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(shardPath);
                // note: Inspect serialized shard entries directly; Editor fallback loading cannot conceal an absent build reference.
                Require(shard != null && shard.Entries.Any(e => e.assetPath == entry.prefabPath && e.prefab == prefab), entry.assetId + ": build reference", checks);
                Require(!string.IsNullOrWhiteSpace(entry.sourceContractJson), entry.assetId + ": supplied contract retained", checks);
                if (entry.generationEligible)
                    Require(entry.assembled && string.IsNullOrEmpty(entry.referenceBody) && prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0, entry.assetId + ": generation admission", checks);
            }
            // note: Surface panels extend an exact armor core and are intentionally outside the existing three-slot mount/motif recipe.
            foreach (var group in catalog.entries.Where(e => !e.assembled && !string.IsNullOrEmpty(e.moduleSlot) && e.requiredModuleSlots.Length == 3).GroupBy(e => e.family + "|" + e.category + "|" + e.referenceBody))
            {
                var recipe = group.GroupBy(e => e.moduleSlot).Select(g => g.First().assetId).ToArray();
                Require(catalog.TryValidateModules(recipe, out string reason), group.Key + ": complete module recipe " + reason, checks);
                Require(!catalog.TryValidateModules(recipe.Take(recipe.Length - 1).ToArray(), out _), group.Key + ": missing module rejected", checks);
                Require(!catalog.TryValidateModules(recipe.Concat(new[] { recipe[0] }).ToArray(), out _), group.Key + ": duplicate slot rejected", checks);
                if (group.All(e => string.IsNullOrEmpty(e.referenceBody)))
                {
                    var parent = new GameObject("DOT recipe fixture"); SceneManager.MoveGameObjectToScene(parent, preview);
                    try
                    {
                        Require(catalog.TryCreateRigidAssembly(recipe, parent.transform, out GameObject assembly, out reason), group.Key + ": runtime presentation assembly " + reason, checks);
                        Require(assembly.transform.childCount == 3 && assembly.GetComponentsInChildren<Animator>(true).Length == 0, group.Key + ": same frame and no Animator", checks);
                        foreach (Transform child in assembly.transform)
                            Require(child.localPosition == Vector3.zero && child.localRotation == Quaternion.identity && child.localScale == Vector3.one, group.Key + ": authored identity placement", checks);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(parent); }
                }
            }
            Require(YQDotEquipmentCatalog.TryResolvePaletteRemainder(new[] { .6f, .3f }, out float neutral, out _) && Mathf.Abs(neutral - .1f) < .00001f, "Weighted palette retains neutral remainder", checks);
            Require(!YQDotEquipmentCatalog.TryResolvePaletteRemainder(new[] { .8f, .5f }, out _, out _), "Invalid palette rejected", checks);
            var library = AssetDatabase.LoadAssetAtPath<GeneratedRpgContentLibrary>("Assets/Assets/Resources/GeneratedRpgContentLibrary.asset");
            var serialized = new SerializedObject(library);
            var admitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string pool in new[] { "weaponPrefabKeys", "offhandPrefabKeys", "consumablePrefabKeys", "ringPrefabKeys", "necklacePrefabKeys", "trinketPrefabKeys" })
            {
                SerializedProperty array = serialized.FindProperty(pool);
                Require(array != null && array.isArray, pool + ": exists", checks);
                for (int index = 0; index < array.arraySize; index++) admitted.Add(array.GetArrayElementAtIndex(index).stringValue);
            }
            foreach (var entry in catalog.entries)
                Require(admitted.Contains(entry.prefabPath) == entry.generationEligible, entry.assetId + ": pool membership matches admission", checks);
        }
        catch (Exception exception) { error = exception.ToString(); Debug.LogException(exception); }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        Directory.CreateDirectory("outputs/DOT_Integration_20261002");
        File.WriteAllText("outputs/DOT_Integration_20261002/verification-receipt.json", JsonConvert.SerializeObject(new {
            status = error == null ? "PASS" : "FAIL", utc = DateTime.UtcNow, checks, error,
            entries = catalog != null ? catalog.entries.Count : 0,
            generationEligible = catalog != null ? catalog.entries.Count(e => e.generationEligible) : 0,
            evidence = "Editor asset/reference/material/recipe contracts. Garment fit, motion and ordinary gameplay are separate unverified gates."
        }, Formatting.Indented));
        Debug.Log("[YQ DOT Verification] " + (error == null ? "PASS" : "FAIL") + " " + checks.Count + " checks.");
    }

    private static void Require(bool condition, string label, List<string> checks)
    { if (!condition) throw new InvalidOperationException(label); checks.Add(label); }
}
