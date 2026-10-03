#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// note: This read-only review measures the real palette library and source materials, not the older discovery snapshot or repaired runtime instances.
public static class YQAssetContractLibraryReview
{
    private const string DirectoryPath = "outputs/Asset_Contracts_20261002";

    [MenuItem("YourQuest/World Generation/Review Source Contracts and Item Type Coverage")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || YQUrpAssetConversionBatch.IsRunning) return;
        StringBuilder report = new StringBuilder();
        report.AppendLine("YourQuest source asset and generation library review");
        report.AppendLine("utc=" + DateTime.UtcNow.ToString("O"));
        report.AppendLine("evidence=Edit mode source bindings; no instance repair or save mutation");
        string[] slots = typeof(YQWorldAssetCatalog).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name.StartsWith("Slot", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        // note: Read the existing authoritative style set for coverage only; no second style list participates in runtime selection.
        IEnumerable<string> configured = typeof(YQWorldAssetCatalog).GetField("SupportedStyleKeys", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as IEnumerable<string>;
        if (configured == null) throw new InvalidOperationException("Authoritative palette styles unavailable.");
        // note: The runtime's internal eligibility gate is reused by this editor-only observer without broadening its production API.
        MethodInfo gateMethod = typeof(YQWorldAssetCatalog).GetMethod("IsAllowedWorldReferenceForSlot", BindingFlags.Static | BindingFlags.NonPublic);
        if (gateMethod == null) throw new InvalidOperationException("Authoritative asset eligibility gate unavailable.");
        var referenceAllowed = (Func<GeneratedAssetReferenceRecord, string, bool>)Delegate.CreateDelegate(typeof(Func<GeneratedAssetReferenceRecord, string, bool>), gateMethod);
        Dictionary<string, HashSet<string>> bound = slots.ToDictionary(s => s, s => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        Dictionary<string, HashSet<string>> clean = slots.ToDictionary(s => s, s => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        int failures = 0;
        // note: A source used by many region palettes is inspected once, avoiding repeated expensive prefab dependency loads.
        Dictionary<string, int> sourceResults = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string style in configured.OrderBy(s => s, StringComparer.Ordinal))
        {
            GeneratedWorldPlanRecord plan = new GeneratedWorldPlanRecord
            {
                worldSeed = "asset-contract-review|" + style,
                regions = new List<GeneratedRegionRecord> { new GeneratedRegionRecord { regionId = "review-region", displayName = style, assetStyleKey = style, assetStyleRationale = "Source contract verification." } }
            };
            YQWorldAssetCatalog.EnsureAssetPalettes(plan);
            if (plan.assetPalettes.Count == 0) { failures++; report.AppendLine("FAIL|palette_missing|" + style); continue; }
            GeneratedRegionAssetPaletteRecord palette = plan.assetPalettes[0];
            foreach (string slot in slots)
            {
                List<GeneratedAssetReferenceRecord> candidates = YQWorldAssetCatalog.GetSlotList(palette, slot);
                foreach (GeneratedAssetReferenceRecord candidate in candidates ?? new List<GeneratedAssetReferenceRecord>())
                {
                    if (candidate == null || !referenceAllowed(candidate, slot)) continue;
                    string path = candidate.assetPath;
                    if (!sourceResults.TryGetValue(path, out int invalid))
                    {
                        UnityEngine.Object source = AssetDatabase.LoadMainAssetAtPath(path);
                        invalid = source == null ? -1 : CountInvalidSourceMaterials(source);
                        sourceResults.Add(path, invalid);
                    }
                    if (invalid < 0) { report.AppendLine("FAIL|missing_reference|" + slot + "|" + path); failures++; continue; }
                    bound[slot].Add(path);
                    if (invalid == 0) clean[slot].Add(path);
                    report.AppendLine((invalid == 0 ? "SOURCE_PASS" : "SOURCE_FAIL") + "|" + style + "|" + slot + "|" + path + "|invalidSlots=" + invalid);
                }
            }
        }
        report.AppendLine("ITEM_TYPE_COVERAGE");
        foreach (string slot in slots)
        {
            bool enough = clean[slot].Count >= 3;
            if (!enough) failures++;
            report.AppendLine((enough ? "PASS" : "FAIL") + "|" + slot + "|registered=" + bound[slot].Count + "|sourceClean=" + clean[slot].Count + "|required=3");
        }
        YQRuntimeWorldSiteCatalog sites = Resources.Load<YQRuntimeWorldSiteCatalog>("YQRuntimeWorldSiteCatalog");
        report.AppendLine("STREAMED_SITE_LIBRARY");
        if (sites == null) { failures++; report.AppendLine("FAIL|site_catalog_missing"); }
        else foreach (YQRuntimeWorldSiteRecord site in sites.Sites)
        {
            bool present = !string.IsNullOrEmpty(site.runtimeManifestResourceKey) && Resources.Load<YQReviewedSemanticSiteManifest>(site.runtimeManifestResourceKey) != null;
            report.AppendLine((site.spatiallyValidated && site.seamlessPlacementEligible && present ? "SITE_AVAILABLE" : "SITE_UNAVAILABLE") + "|" + site.kitId + "|kind=" + site.siteKind + "|style=" + site.semanticStyleKey + "|functions=" + string.Join(",", site.reviewedFunctionsV2 ?? new List<YQAssetFunctionV2>()));
        }
        report.AppendLine("RESULT " + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures);
        Directory.CreateDirectory(DirectoryPath);
        string output = DirectoryPath + "/Library_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".txt";
        File.WriteAllText(output, report.ToString());
        Debug.Log("[YQAssetContracts] Library review " + (failures == 0 ? "PASS" : "FAIL") + ": " + output);
    }

    private static int CountInvalidSourceMaterials(UnityEngine.Object source)
    {
        if (source is Material material) return YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material) ? 0 : 1;
        if (!(source is GameObject prefab)) return 1;
        int invalid = 0;
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is BillboardRenderer billboard && (billboard.billboard == null || !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(billboard.billboard.material))) invalid++;
            Material[] materials = renderer.sharedMaterials;
            int required = YQRuntimeUrpMaterialRepair.ResolveRequiredMaterialSlotCount(renderer);
            for (int slot = 0; slot < required; slot++)
                if (YQRuntimeUrpMaterialRepair.IsMaterialSlotRequired(renderer, slot) && !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(slot < materials.Length ? materials[slot] : null)) invalid++;
        }
        return invalid;
    }

    [MenuItem("YourQuest/World Generation/Audit Current Streamed Site Owners")]
    public static void AuditPlay()
    {
        if (!EditorApplication.isPlaying) return;
        // note: Observe existing prepared and loaded owners without requesting loads, moving the player, or mutating site identity.
        StringBuilder report = new StringBuilder();
        report.AppendLine("utc=" + DateTime.UtcNow.ToString("O"));
        report.AppendLine("released=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased);
        YQCompiledWorldSiteInstance[] owners = UnityEngine.Object.FindObjectsByType<YQCompiledWorldSiteInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (YQCompiledWorldSiteInstance owner in owners)
            report.AppendLine("SITE|" + owner.name + "|loaded=" + owner.IsLoaded + "|active=" + owner.gameObject.activeInHierarchy + "|position=" + owner.transform.position + "|renderers=" + owner.GetComponentsInChildren<Renderer>(true).Length);
        report.AppendLine("owners=" + owners.Length + "|loaded=" + owners.Count(o => o.IsLoaded));
        Directory.CreateDirectory(DirectoryPath);
        string output = DirectoryPath + "/Play_Sites_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".txt";
        File.WriteAllText(output, report.ToString());
        Debug.Log("[YQAssetContracts] Live site owner audit: " + output);
    }
}
#endif
