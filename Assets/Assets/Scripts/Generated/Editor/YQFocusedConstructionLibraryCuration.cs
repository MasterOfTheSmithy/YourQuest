#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// note: Explicitly reviewed source selections gain measured construction contracts; arbitrary filename discovery never becomes release authority.
public static class YQFocusedConstructionLibraryCuration
{
    private const string Nordic = "Assets/BefourStudios/NordicVillage/Art/Prefabs/";
    private const string Viking = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/";
    private const string Chests = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/";

    [MenuItem("YourQuest/World Generation/Curate Reviewed Construction and Containers")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || YQUrpAssetConversionBatch.IsRunning) return;
        YQWorldAssetIntakeCatalog catalog = Resources.Load<YQWorldAssetIntakeCatalog>("YQWorldAssetIntakeCatalog");
        if (catalog == null) throw new InvalidOperationException("Canonical intake catalog unavailable.");
        var selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Nordic + "SM_GroundMesh.prefab"] = "floor", [Nordic + "SM_MudMesh.prefab"] = "floor", [Nordic + "SM_SingleTile.prefab"] = "floor",
            [Viking + "SM_House1_Floor.prefab"] = "floor", [Viking + "SM_StableWooden_Floor.prefab"] = "floor",
            [Nordic + "SM_ThatchRoof01.prefab"] = "roof", [Nordic + "SM_LogRoofGable01.prefab"] = "roof", [Nordic + "SM_RoofGableTall01.prefab"] = "roof",
            [Viking + "SM_House2_Roof.prefab"] = "roof", [Viking + "SM_RoofCot.prefab"] = "roof",
            [Nordic + "SM_Door01.prefab"] = "door", [Nordic + "SM_Door02.prefab"] = "door", [Viking + "SM_House2_Door.prefab"] = "door",
            [Nordic + "SM_WallTorch.prefab"] = "wall_deco", [Nordic + "SM_Shield.prefab"] = "wall_deco", [Viking + "SM_TorchWall.prefab"] = "wall_deco",
            [Chests + "ChestSimpleSmall.prefab"] = "loot_container", [Chests + "ChestSimpleMedium.prefab"] = "loot_container", [Chests + "ChestOrnateMedium.prefab"] = "loot_container"
        };
        string output = "outputs/Asset_Contracts_20261002/Curation_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(output);
        File.Copy(AssetDatabase.GetAssetPath(catalog), output + "/Intake_Before.asset");
        List<string> report = new List<string> { "evidence=measured Edit-mode source contracts; no accepted world or save changes" };
        foreach (var selection in selections)
        {
            YQSpatialAssetRecord record = catalog.SpatialAssets.FirstOrDefault(r => r != null && string.Equals(r.assetPath, selection.Key, StringComparison.OrdinalIgnoreCase));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(selection.Key);
            if (record == null || prefab == null || record.sourceGuid != AssetDatabase.AssetPathToGUID(selection.Key))
            { report.Add("FAIL|source_identity|" + selection.Key); continue; }
            if (record.releaseEligible && record.spatialMetadataAuthored && record.curationV2?.contractVersion == 2)
            { report.Add("PRESERVED|existing_review|" + selection.Key); continue; }
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            int invalid = renderers.Sum(r => r.sharedMaterials.Count(m => !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(m)));
            int scripts = prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            // note: Approval requires real geometry and freshly usable bindings. Unresolved sources remain withheld rather than receiving substitute materials.
            if (invalid != 0 || scripts != 0 || renderers.Length == 0 || record.localBoundsSize.sqrMagnitude <= 0.0001f)
            { report.Add("FAIL|source_contract|" + selection.Key + "|invalid=" + invalid + "|scripts=" + scripts); continue; }
            bool attachment = selection.Value == "roof" || selection.Value == "door" || selection.Value == "wall_deco";
            bool container = selection.Value == "loot_container";
            bool floor = selection.Value == "floor";
            // note: Intake bounds are accepted discovery measurements tied to the same GUID. Recheck the actual renderers before using their physical footprint.
            Bounds bounds = new Bounds(); bool initialized = false;
            foreach (Renderer renderer in renderers)
            {
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = prefab.transform.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; } else bounds.Encapsulate(point);
                }
            }
            if (!initialized || bounds.size.sqrMagnitude <= 0.0001f) { report.Add("FAIL|geometry|" + selection.Key); continue; }
            if (floor && prefab.GetComponentsInChildren<Collider>(true).Length == 0)
            { report.Add("FAIL|floor_collision|" + selection.Key); continue; }
            if (container && prefab.GetComponentsInChildren<Collider>(true).Length == 0)
            {
                // note: Physical chests need a source collision envelope, independent of transient runtime interaction wiring.
                string backup = Path.Combine(output, "Before", selection.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(selection.Key, backup);
                GameObject contents = PrefabUtility.LoadPrefabContents(selection.Key);
                try { BoxCollider collider = contents.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size; PrefabUtility.SaveAsPrefabAsset(contents, selection.Key); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(selection.Key);
            }
            record.EnsureCollections();
            // note: Preserve non-material diagnostics; this review cannot clear unrelated identity, script, or geometry failures.
            if (record.validationIssues.Any(v => !v.StartsWith("Material:", StringComparison.Ordinal)))
            { report.Add("FAIL|unresolved_diagnostic|" + selection.Key); continue; }
            record.validationIssues.RemoveAll(v => v.StartsWith("Material:", StringComparison.Ordinal));
            record.localBoundsCenter = bounds.center; record.localBoundsSize = bounds.size;
            record.footprintX = bounds.size.x; record.footprintZ = bounds.size.z; record.height = bounds.size.y;
            record.clearanceSize = bounds.size + new Vector3(0.5f, 0.25f, 0.5f);
            record.hasCollider = prefab.GetComponentsInChildren<Collider>(true).Length > 0;
            record.colliderCount = prefab.GetComponentsInChildren<Collider>(true).Length;
            record.invalidMaterialSlotCount = 0; record.materialReviewSlotCount = 0;
            record.semanticRole = selection.Value;
            record.spatialMetadataAuthored = true; record.allowedSlopeDegrees = 0f;
            record.foundationProfile = attachment ? "authored_attachment" : "level_support";
            record.roadRelationship = "keep_circulation_clear";
            record.navigationProfile = floor ? "authored_walkable_surface" : "solid_clearance";
            // note: Preserve the source axes for assembly placement; a thin door leaf faces along its measured normal.
            record.frontDirection = selection.Value == "door" && bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
            record.frontDirectionAuthored = true;
            record.curationV2 = new YQAssetCurationContractV2
            {
                contractVersion = 2,
                primaryRole = container ? YQAssetRoleV2.Container : selection.Value == "wall_deco" ? YQAssetRoleV2.Dressing : YQAssetRoleV2.StructuralModule,
                primaryFunction = container ? YQAssetFunctionV2.Storage : floor || selection.Value == "door" ? YQAssetFunctionV2.Circulation : YQAssetFunctionV2.Infrastructure,
                environments = new List<YQAssetEnvironmentV2> { YQAssetEnvironmentV2.Interior, YQAssetEnvironmentV2.Exterior },
                familyId = record.kitId, variantGroupId = record.stableAssetId,
                supportMode = attachment ? YQAssetSupportModeV2.SocketOnly : YQAssetSupportModeV2.Ground,
                supportLocalY = bounds.min.y,
                maximumSupportRelief = 0.05f,
                maximumEmbedDepth = 0.01f,
                maxUsesPerSite = container ? 4 : 32,
                minimumRepeatDistance = container ? 2f : 0f,
                supportPolygon = new List<Vector2> { new Vector2(bounds.min.x, bounds.min.z), new Vector2(bounds.min.x, bounds.max.z), new Vector2(bounds.max.x, bounds.max.z), new Vector2(bounds.max.x, bounds.min.z) }
            };
            // note: Construction pieces retain module identity while exposing their actual surface or portal role to typed placement contexts.
            if (floor) record.curationV2.secondaryRoles.Add(YQAssetRoleV2.Surface);
            if (selection.Value == "door") record.curationV2.secondaryRoles.Add(YQAssetRoleV2.Portal);
            if (container)
            {
                record.curationV2.affordances.Add(YQAssetAffordanceV2.Storage);
                record.curationV2.sockets.Add(new YQAssetSocketRecordV2
                { socketId = record.stableAssetId + "_interaction", kind = YQAssetSocketKindV2.Interaction, localPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.max.z + 0.75f), clearanceSize = new Vector3(0.8f, 1.8f, 0.8f), compatibilityKey = "container_access" });
            }
            if (floor) record.curationV2.affordances.Add(YQAssetAffordanceV2.TraversableSurface);
            if (attachment) record.curationV2.sockets.Add(new YQAssetSocketRecordV2
            { socketId = record.stableAssetId + "_attachment", kind = YQAssetSocketKindV2.Connection, localPosition = Vector3.zero, clearanceSize = record.clearanceSize, compatibilityKey = record.kitId + "_" + selection.Value });
            record.disposition = YQAssetIntakeDisposition.Candidate; record.releaseEligible = true;
            YQAssetKitManifest kit = catalog.Kits.FirstOrDefault(k => k != null && k.kitId == record.kitId);
            if (kit == null) { record.releaseEligible = false; report.Add("FAIL|missing_kit|" + selection.Key); continue; }
            // note: Only individually reviewed assets become eligible; enabling their existing kit does not approve its remaining discovered entries.
            kit.releaseEligible = true;
            YQAssetConstraintEvaluationV2 evaluation = YQAssetConstraintEvaluatorV2.Evaluate(kit, record, new YQAssetPlacementContextV2());
            if (!evaluation.Accepted)
            { record.releaseEligible = false; record.disposition = YQAssetIntakeDisposition.NeedsSpatialReview; report.Add("FAIL|curation_contract|" + selection.Key + "|" + string.Join(",", evaluation.Failures)); continue; }
            report.Add("CURATED|" + selection.Value + "|" + selection.Key + "|bounds=" + bounds + "|support=" + record.curationV2.supportMode);
        }
        catalog.RecalculateKitSpatialCounts(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        File.WriteAllLines(output + "/Review.txt", report);
        Debug.Log("[YQAssetContracts] Focused curation: " + output + "/Review.txt");
    }
}
#endif
