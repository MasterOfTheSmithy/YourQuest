using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static partial class YQWorldAssetIntakeBuilder
{
    internal const string CoastalFoliageRoot = "Assets/HIVEMIND/PirateIsland/HDRP(Default)/Art/Prefabs/";
    internal const string SharedShrubPath = "Assets/YughuesFreeBushes2018/Prefabs/P_Bush01.prefab";
    private const string BindingManifest = "Assets/Assets/Resources/YQWorldAssetManifest.json";
    private const string DependencyManifest = "Assets/Assets/Resources/YQWorldPrefabDependencies.json";

    internal static bool IsRequiredEcologyRepairTarget(YQSpatialAssetRecord record)
    {
        string path = record?.assetPath ?? string.Empty;
        return path == SharedShrubPath || path.StartsWith(CoastalFoliageRoot, StringComparison.Ordinal) &&
            YQEcologySourcePolicy.ClassifyPath(path) == YQSmallFoliageKind.Understory;
    }

    internal static List<YQSpatialAssetRecord> StageRequiredEcologyRecords(
        IReadOnlyList<YQSpatialAssetRecord> originals, Func<YQSpatialAssetRecord, YQSpatialAssetRecord> refresh, Func<bool> cancelled)
    {
        // note: All target records are detached before validation. A cancelled or failed pass cannot publish a partially approved catalog.
        var staged = new List<YQSpatialAssetRecord>(originals.Count);
        foreach (YQSpatialAssetRecord original in originals)
        {
            if (cancelled?.Invoke() == true) throw new OperationCanceledException("Ecology requalification cancelled; catalog unchanged.");
            if (!IsRequiredEcologyRepairTarget(original)) { staged.Add(original); continue; }
            var detached = JsonUtility.FromJson<YQSpatialAssetRecord>(JsonUtility.ToJson(original));
            YQSpatialAssetRecord measured = refresh(detached);
            if (!YQEcologySourcePolicy.HasValidMeasuredSource(measured) ||
                !YQEcologySourcePolicy.TryClassifySmallFoliage(measured, out _) ||
                !YQEcologySourcePolicy.AllowsCollisionProfile(measured, false) ||
                measured.assetPath != original.assetPath || measured.sourceGuid != original.sourceGuid ||
                !measured.releaseEligible || measured.disposition != YQAssetIntakeDisposition.Candidate ||
                !measured.spatialMetadataAuthored || measured.curationV2 == null ||
                measured.curationV2.contractVersion != YQAssetCurationContractV2.SupportedContractVersion)
                throw new InvalidOperationException("Ecology source failed fresh qualification: " + original.assetPath);
            staged.Add(measured);
        }
        if (cancelled?.Invoke() == true) throw new OperationCanceledException("Ecology requalification cancelled; catalog unchanged.");
        return staged;
    }

    internal static bool HasExactEcologyBinding(YQSpatialAssetRecord record, out string reason)
    {
        reason = "missing or changed prefab GUID/fileID binding";
        if (record == null || string.IsNullOrWhiteSpace(record.sourceGuid) ||
            AssetDatabase.AssetPathToGUID(record.assetPath) != record.sourceGuid) return false;
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(record.assetPath);
        YQRuntimeWorldAssetRegistry registry = YQRuntimeWorldAssetRegistry.Instance;
        YQRuntimeWorldAssetEntry entry = registry != null && registry.ContainsPrefabPath(record.assetPath)
            ? registry.ResolveEntry(record.assetPath) : null;
        if (source == null || entry?.prefab != source) return false;
        reason = string.Empty;
        return true;
    }

    internal static Material ResolveBoundIntakeMaterial(string assetPath, Renderer renderer, int materialIndex)
    {
        Material original = renderer.sharedMaterials[materialIndex];
        // note: Intake validates the same exact authored override that runtime applies, including recovered missing-source slots. It never approves a null slot by inference.
        YQMaterialCompatibilityState originalState = EvaluateRuntimeReadyMaterial(original, out _, out _, out _);
        if (assetPath != SharedShrubPath &&
            (originalState == YQMaterialCompatibilityState.VerifiedUrp || originalState == YQMaterialCompatibilityState.VerifiedUrpAdapter)) return original;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        var registry = YQRuntimeWorldAssetRegistry.Instance;
        var entry = registry != null && registry.ContainsPrefabPath(assetPath) ? registry.ResolveEntry(assetPath) : null;
        if (prefab == null || entry?.prefab != prefab) return original;
        string transformPath = AnimationUtility.CalculateTransformPath(renderer.transform, prefab.transform);
        int rendererIndex = Array.IndexOf(renderer.GetComponents<Renderer>(), renderer);
        var replacements = entry.materialOverrides.Where(item => item != null && item.transformPath == transformPath &&
            item.rendererIndex == rendererIndex && item.materialIndex == materialIndex).ToList();
        return replacements.Count == 1 ? replacements[0].replacementMaterial : replacements.Count == 0 ? original : null;
    }

    private static bool RefreshDecorativeCandidateForApproval(YQWorldAssetIntakeCatalog catalog, YQSpatialAssetRecord record)
    {
        // note: Normal decorative approval uses fresh imported evidence too; stale scan flags cannot become a colliderless exemption.
        var kit = catalog.Kits.FirstOrDefault(item => item != null && item.kitId == record.kitId);
        if (kit == null || AssetDatabase.AssetPathToGUID(record.assetPath) != record.sourceGuid) return false;
        var semantic = YQDiscoveredWorldAssetCatalog.Instance?.Entries.FirstOrDefault(item => item != null && item.assetPath == record.assetPath);
        YQSpatialAssetRecord fresh = BuildSpatialAssetRecord(kit, record.sourceGuid, record.assetPath, semantic);
        if (!CanApproveDecorative(fresh, semantic?.blocksNav ?? true)) return false;
        if (YQEcologySourcePolicy.TryClassifySmallFoliage(fresh, out _))
        {
            if (semantic == null || !semantic.runtimeEligible || !HasExactEcologyBinding(fresh, out _)) return false;
            fresh.ecologySourceDependencyHash = AssetDatabase.GetAssetDependencyHash(fresh.assetPath).ToString();
        }
        fresh.libraryEvidenceIds = new List<string>(record.libraryEvidenceIds ?? new List<string>());
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), record);
        return true;
    }

    [MenuItem("YourQuest/Assets/Repair Required Ecology Sources")]
    public static void RepairRequiredEcologySources()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Required ecology repair requires stable Edit mode.");
        var catalog = AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(IntakeCatalogPath);
        var discovered = YQDiscoveredWorldAssetCatalog.Instance;
        if (catalog == null || discovered == null) throw new InvalidOperationException("Canonical intake/discovery catalog missing.");
        RequireCleanEcologyAsset(catalog);
        RequireCleanEcologyAsset(discovered);
        var transaction = new EcologyFileTransaction();
        int refreshedCount = 0;
        string shrubMaterialPath = "Assets/YughuesFreeBushes2018/Materials/M_Bush01.mat";
        string shrubMaterialHash = AssetDatabase.GetAssetDependencyHash(shrubMaterialPath).ToString();
        try
        {
            transaction.Track(IntakeCatalogPath);
            transaction.Track(AssetDatabase.GetAssetPath(discovered));
            // note: The existing neutral shrub is already an authored shared fallback. Only its source-preserving URP adapter and exact binding need repair.
            PrepareSharedShrubMaterial(transaction);
            YQRuntimeWorldAssetRegistry.ClearCachedInstance();
            var sourceHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            var runtimeEligibility = new Dictionary<string, bool>(StringComparer.Ordinal);
            var staged = StageRequiredEcologyRecords(catalog.SpatialAssets, old =>
            {
                if (!HasExactEcologyBinding(old, out string bindingFailure))
                    throw new InvalidOperationException(bindingFailure + ": " + old.assetPath);
                var kit = catalog.Kits.FirstOrDefault(item => item != null && item.kitId == old.kitId);
                var semantic = discovered.Entries.FirstOrDefault(item => item != null && item.assetPath == old.assetPath);
                if (kit == null || semantic == null || semantic.blocksNav ||
                    semantic.slotTag != YQWorldAssetCatalog.SlotVegetation)
                    throw new InvalidOperationException("Missing nonblocking vegetation source contract: " + old.assetPath);
                // note: Discovery eligibility is derived technical state, not independent approval. Recompute only these sources with the normal production validator.
                bool eligible = YQRuntimeWorldAssetRegistryBuilder.IsDiscoveredReferenceRuntimeEligible(old.assetPath, semantic.assetType, semantic.slotTag);
                if (!eligible) throw new InvalidOperationException("Fresh runtime source validation rejected: " + old.assetPath);
                runtimeEligibility.Add(old.assetPath, eligible);
                string hash = AssetDatabase.GetAssetDependencyHash(old.assetPath).ToString();
                if (!string.IsNullOrEmpty(old.ecologySourceDependencyHash) && old.ecologySourceDependencyHash != hash)
                    throw new InvalidOperationException("Previously qualified ecology source changed; review before reapproval: " + old.assetPath);
                YQSpatialAssetRecord measured = BuildSpatialAssetRecord(kit, old.sourceGuid, old.assetPath, semantic);
                if (!CanApproveDecorative(measured, semantic.blocksNav))
                    throw new InvalidOperationException("Fresh imported validation rejected " + old.assetPath + ": " + string.Join("; ", measured.validationIssues));
                // note: Preserve already valid authored approvals and documentary metadata; generate the standard review only for held foliage.
                if (old.releaseEligible && old.spatialMetadataAuthored && old.disposition == YQAssetIntakeDisposition.Candidate)
                    CopyAuthoredSpatialReview(measured, old);
                else ApplyBenchmarkDecorativeReview(measured);
                measured.libraryEvidenceIds = new List<string>(old.libraryEvidenceIds ?? new List<string>());
                measured.ecologySourceDependencyHash = hash;
                sourceHashes.Add(old.assetPath, hash);
                refreshedCount++;
                return measured;
            }, () => !Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar(
                "Required ecology source repair", "Validating imported foliage " + refreshedCount, refreshedCount / 11f));
            if (refreshedCount != 11) throw new InvalidOperationException("Expected the ten coastal foliage records and one shared shrub; found " + refreshedCount);
            foreach (var pair in sourceHashes)
                if (AssetDatabase.GetAssetDependencyHash(pair.Key).ToString() != pair.Value)
                    throw new InvalidOperationException("Ecology source changed during validation: " + pair.Key);
            if (AssetDatabase.GetAssetDependencyHash(shrubMaterialPath).ToString() != shrubMaterialHash)
                throw new InvalidOperationException("Retained shrub material changed during validation.");
            var discoveredEntries = discovered.Entries.Select(reference =>
            {
                if (reference == null || !runtimeEligibility.TryGetValue(reference.assetPath, out bool eligible)) return reference;
                var refreshed = JsonUtility.FromJson<GeneratedAssetReferenceRecord>(JsonUtility.ToJson(reference));
                refreshed.runtimeEligible = eligible;
                return refreshed;
            }).ToList();
            // note: This is the sole catalog publication edge. No scan, profile write, seed change, world regeneration or broad dependency rebuild occurs.
            var kits = catalog.Kits.Select(kit => JsonUtility.FromJson<YQAssetKitManifest>(JsonUtility.ToJson(kit))).ToList();
            foreach (var kit in kits)
                if (kit != null && staged.Any(record => IsRequiredEcologyRepairTarget(record) && record.kitId == kit.kitId))
                    kit.releaseEligible = true;
            catalog.SetRecords(catalog.ScanScope, DateTime.UtcNow.ToString("O"), kits, staged, catalog.Materials.ToList());
            catalog.RecalculateKitSpatialCounts();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            discovered.SetEntries(discoveredEntries);
            EditorUtility.SetDirty(discovered);
            AssetDatabase.SaveAssetIfDirty(discovered);
            YQDiscoveredWorldAssetCatalog.ClearCachedInstance();
            YQWorldAssetCatalog.InvalidateRuntimeIntakeCatalog();
            _reviewedSpatialIndexLoaded = false;
            _reviewedSpatialByPath = null;
            YQRuntimeWorldAssetRegistry.ClearCachedInstance();
            transaction.Complete();
            Debug.Log("[YQRequiredEcologyRepair] REPAIR COMPLETE qualified=" + refreshedCount +
                " coastalUnderstory=10 sharedShrubs=1 targetsUnchanged=true savesUnchanged=true backup=" + transaction.BackupDirectory);
        }
        catch
        {
            transaction.Rollback();
            YQWorldAssetCatalog.InvalidateRuntimeIntakeCatalog();
            _reviewedSpatialIndexLoaded = false;
            _reviewedSpatialByPath = null;
            YQRuntimeWorldAssetRegistry.ClearCachedInstance();
            YQDiscoveredWorldAssetCatalog.ClearCachedInstance();
            throw;
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void PrepareSharedShrubMaterial(EcologyFileTransaction transaction)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/YughuesFreeBushes2018/Materials/M_Bush01.mat");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedShrubPath);
        if (source == null || prefab == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) != "3bae03ff15e9f0749abaf52aaf5d1bff")
            throw new InvalidOperationException("Shared shrub source material identity is unavailable or changed.");
        RequireCleanEcologyAsset(source);
        RequireCleanEcologyAsset(prefab);
        Renderer[] shrubRenderers = prefab.GetComponentsInChildren<Renderer>(true);
        string serializedPrefab = ReadNormalized(SharedShrubPath);
        if (shrubRenderers.Length != 4 || shrubRenderers.Any(renderer => renderer.sharedMaterials.Length != 1) ||
            Regex.Matches(serializedPrefab, @"m_Materials:\n  - \{fileID: 2100000, guid: (?:465ea732ef1f3e044b316a1cb3cd20b9|3bae03ff15e9f0749abaf52aaf5d1bff), type: 2\}").Count != 4)
            throw new InvalidOperationException("Shared shrub LOD/material source layout changed; preserve it for review.");
        // note: The prefab can bind an earlier project adapter. Recover the authored cutout/specular surface from the retained Bush01 source without changing that imported binding.
        string[] sourceProperties = { "_MainTex", "_BumpMap", "_OcclusionMap", "_SpecGlossMap" };
        string[] sourceTextureGuids = { "94db8a6f822aa8245ae0a58f78870c2c", "2f1e681c00689bf429eece67c03d8635", "380b04d9a2f3007438f6937306ba8158", "01ac228eec7ceda4ba0395dea3410cef" };
        for (int index = 0; index < sourceProperties.Length; index++)
            if (source.GetTexture(sourceProperties[index]) == null ||
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source.GetTexture(sourceProperties[index]))) != sourceTextureGuids[index])
                throw new InvalidOperationException("Retained Bush01 source texture identity changed: " + sourceProperties[index]);
        string adapterPath = BuildMaterialAdapterPath(AssetDatabase.GetAssetPath(source));
        Material adapter = AssetDatabase.LoadAssetAtPath<Material>(adapterPath);
        if (adapter == null)
        {
            EnsureFolderPath(Path.GetDirectoryName(adapterPath).Replace('\\', '/'));
            transaction.Track(adapterPath);
            adapter = YQRuntimeUrpMaterialRepair.CreateEditorUrpLitMaterial(source, prefab.GetComponentInChildren<Renderer>(true));
            if (adapter == null) throw new InvalidOperationException("Could not convert the source shrub material to URP.");
            // note: Retain the source's cutout and specular workflow; generic foliage conversion uses a different alpha cutoff and omits Standard's specular map.
            adapter.SetFloat("_Cutoff", source.GetFloat("_Cutoff"));
            adapter.SetFloat("_WorkflowMode", 0f);
            adapter.EnableKeyword("_SPECULAR_SETUP");
            adapter.SetTexture("_SpecGlossMap", source.GetTexture("_SpecGlossMap"));
            adapter.SetTextureScale("_SpecGlossMap", source.GetTextureScale("_SpecGlossMap"));
            adapter.SetTextureOffset("_SpecGlossMap", source.GetTextureOffset("_SpecGlossMap"));
            adapter.SetColor("_SpecColor", source.GetColor("_SpecColor"));
            adapter.SetFloat("_Smoothness", source.GetFloat("_GlossMapScale"));
            adapter.EnableKeyword("_METALLICSPECGLOSSMAP");
            adapter.name = Path.GetFileNameWithoutExtension(adapterPath);
            AssetDatabase.CreateAsset(adapter, adapterPath);
        }
        ValidateShrubAdapter(source, adapter);
        PatchSharedShrubBinding(adapter, transaction);
    }

    internal static void ValidateShrubAdapter(Material source, Material adapter)
    {
        if (source == null || adapter == null || EvaluateMaterial(adapter, out _) != YQMaterialCompatibilityState.VerifiedUrp ||
            adapter.GetFloat("_AlphaClip") != 1f || adapter.GetFloat("_Cutoff") != source.GetFloat("_Cutoff") ||
            adapter.GetFloat("_WorkflowMode") != 0f || !adapter.IsKeywordEnabled("_SPECULAR_SETUP") ||
            !adapter.IsKeywordEnabled("_METALLICSPECGLOSSMAP"))
            throw new InvalidOperationException("Shared shrub adapter does not preserve the source cutout/specular surface.");
        foreach (var properties in new[] { new[] { "_MainTex", "_BaseMap" }, new[] { "_BumpMap", "_BumpMap" },
                     new[] { "_OcclusionMap", "_OcclusionMap" }, new[] { "_SpecGlossMap", "_SpecGlossMap" } })
            if (source.GetTexture(properties[0]) == null || adapter.GetTexture(properties[1]) != source.GetTexture(properties[0]) ||
                adapter.GetTextureScale(properties[1]) != source.GetTextureScale(properties[0]) ||
                adapter.GetTextureOffset(properties[1]) != source.GetTextureOffset(properties[0]))
                throw new InvalidOperationException("Shared shrub adapter texture/UV mismatch: " + properties[0]);
    }

    internal static bool TryBuildReviewedEcologyMaterialOverrides(GameObject prefab, string path, out List<YQRuntimeWorldMaterialOverride> overrides)
    {
        overrides = null;
        if (path != SharedShrubPath || prefab == null) return false;
        var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/YughuesFreeBushes2018/Materials/M_Bush01.mat");
        if (source == null) return false;
        var adapter = AssetDatabase.LoadAssetAtPath<Material>(BuildMaterialAdapterPath(AssetDatabase.GetAssetPath(source)));
        if (adapter == null) return false;
        ValidateShrubAdapter(source, adapter);
        // note: Rebuild only this reviewed material binding; source meshes, LODs and all unrelated overrides remain owned by their original systems.
        overrides = new List<YQRuntimeWorldMaterialOverride>();
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            string transformPath = AnimationUtility.CalculateTransformPath(renderer.transform, prefab.transform);
            int rendererIndex = Array.IndexOf(renderer.GetComponents<Renderer>(), renderer);
            for (int slot = 0; slot < renderer.sharedMaterials.Length; slot++)
                overrides.Add(new YQRuntimeWorldMaterialOverride
                { transformPath = transformPath, rendererIndex = rendererIndex, materialIndex = slot, replacementMaterial = adapter, replaceUsableSource = true });
        }
        return true;
    }

    private static void PatchSharedShrubBinding(Material adapter, EcologyFileTransaction transaction)
    {
        JObject manifest = JObject.Parse(File.ReadAllText(BindingManifest));
        if ((int?)manifest["version"] != 1 || (string)manifest["generator"] != "YourQuest.GranularWorldAssetResources.v1")
            throw new InvalidOperationException("Unsupported runtime binding manifest.");
        JObject row = manifest["assets"].OfType<JObject>().Single(item => (string)item["path"] == SharedShrubPath);
        string leafPath = "Assets/Assets/Resources/" + (string)row["resource"] + ".asset";
        string shardPath = (string)row["source"];
        string originalLeaf = ReadNormalized(leafPath);
        string originalEntry = ExtractEcologyEntry(originalLeaf, SharedShrubPath);
        if (EcologyHash(originalLeaf) != (string)row["leafSha256"] || EcologyHash(originalEntry) != (string)row["entrySha256"] ||
            ExtractEcologyEntry(ReadNormalized(shardPath), SharedShrubPath) != originalEntry)
            throw new InvalidOperationException("Shared shrub binding changed or is stale; preserve it for review.");
        // note: Disk rollback must never replace preexisting unsaved curation or material binding changes in memory.
        RequireCleanEcologyAsset(AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(leafPath));
        RequireCleanEcologyAsset(AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(shardPath));
        transaction.Track(leafPath); transaction.Track(shardPath); transaction.Track(BindingManifest); transaction.Track(DependencyManifest);
        foreach (string path in new[] { leafPath, shardPath })
        {
            var registry = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(path);
            var entry = registry?.Entries.SingleOrDefault(item => item.assetPath == SharedShrubPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedShrubPath);
            if (entry == null || entry.prefab != prefab) throw new InvalidOperationException("Changed shrub prefab binding: " + path);
            var bindings = new List<YQRuntimeWorldMaterialOverride>(entry.materialOverrides);
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                string transformPath = AnimationUtility.CalculateTransformPath(renderer.transform, prefab.transform);
                int rendererIndex = Array.IndexOf(renderer.GetComponents<Renderer>(), renderer);
                Material[] materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    string slotGuid = materials[slot] != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(materials[slot])) : string.Empty;
                    if (materials[slot] != null && slotGuid != "465ea732ef1f3e044b316a1cb3cd20b9" && slotGuid != "3bae03ff15e9f0749abaf52aaf5d1bff")
                        throw new InvalidOperationException("Unexpected shared shrub material slot; preserve source for review.");
                    var existing = bindings.SingleOrDefault(item => item.transformPath == transformPath && item.rendererIndex == rendererIndex && item.materialIndex == slot);
                    if (existing != null && existing.replacementMaterial != adapter)
                        throw new InvalidOperationException("Preserve a different authored shrub material override.");
                    if (existing != null) existing.replaceUsableSource = true;
                    if (existing == null) bindings.Add(new YQRuntimeWorldMaterialOverride
                    { transformPath = transformPath, rendererIndex = rendererIndex, materialIndex = slot, replacementMaterial = adapter, replaceUsableSource = true });
                }
            }
            entry.materialOverrides = bindings;
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
        }
        string leaf = ReadNormalized(leafPath);
        string entryText = ExtractEcologyEntry(leaf, SharedShrubPath);
        if (entryText != ExtractEcologyEntry(ReadNormalized(shardPath), SharedShrubPath)) throw new InvalidOperationException("Shrub leaf/shard bindings differ.");
        row["entrySha256"] = EcologyHash(entryText); row["leafSha256"] = EcologyHash(leaf);
        // note: Only this changed binding invalidates preload metadata; preserve every other row, including unrelated Mimic diagnostics.
        JObject dependencies = JObject.Parse(File.ReadAllText(DependencyManifest));
        if ((int?)dependencies["version"] != 1 || (string)dependencies["generator"] != "YourQuest.PrefabDependencyPreloads.v1")
            throw new InvalidOperationException("Unsupported prefab dependency manifest.");
        JObject dependency = dependencies["prefabs"].OfType<JObject>().Single(item => (string)item["path"] == SharedShrubPath);
        if (AssetDatabase.GetDependencies(SharedShrubPath, true).Any(path =>
                typeof(AnimationClip).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(path) ?? typeof(object)) ||
                typeof(RuntimeAnimatorController).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(path) ?? typeof(object))) ||
            !(dependency["resources"] is JArray resources) || resources.Count != 0)
            throw new InvalidOperationException("Shared shrub gained animation dependencies; scoped material repair cannot rewrite that closure.");
        dependency["entrySha256"] = row["entrySha256"].DeepClone();
        dependency["sourceDependencyHash"] = AssetDatabase.GetAssetDependencyHash(SharedShrubPath).ToString();
        File.WriteAllText(BindingManifest, manifest.ToString(Formatting.Indented), new UTF8Encoding(false));
        File.WriteAllText(DependencyManifest, dependencies.ToString(Formatting.Indented), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(BindingManifest, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(DependencyManifest, ImportAssetOptions.ForceSynchronousImport);
    }

    private static string ReadNormalized(string path) => File.ReadAllText(path).Replace("\r\n", "\n");
    private static void RequireCleanEcologyAsset(UnityEngine.Object asset)
    {
        if (asset == null || EditorUtility.IsDirty(asset))
            throw new InvalidOperationException("Save existing ecology asset edits before repair: " + AssetDatabase.GetAssetPath(asset));
    }
    private static string ExtractEcologyEntry(string text, string path)
    {
        Match match = Regex.Match(text, @"(?ms)^  - assetPath: " + Regex.Escape(path) + @"\n.*?(?=^  - assetPath: |^  useLazyResourceShards:)");
        if (!match.Success) throw new InvalidOperationException("Exact serialized ecology binding missing: " + path);
        return match.Value;
    }
    private static string EcologyHash(string text)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
    }

    private sealed class EcologyFileTransaction
    {
        private readonly Dictionary<string, byte[]> originals = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private bool complete;
        public string BackupDirectory { get; } = "outputs/EcologyStartupRepair/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");
        public void Track(string path)
        {
            // note: Only explicit project-owned files are backed up; rollback never discovers or deletes a directory tree.
            foreach (string candidate in new[] { path, path + ".meta" })
            {
                if (originals.ContainsKey(candidate)) continue;
                byte[] bytes = File.Exists(candidate) ? File.ReadAllBytes(candidate) : null;
                originals.Add(candidate, bytes);
                if (bytes == null) continue;
                string backup = BackupDirectory + "/" + candidate;
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.WriteAllBytes(backup, bytes);
            }
        }
        public void Complete() { complete = true; }
        public void Rollback()
        {
            if (complete) return;
            // note: Delete only newly created, explicitly tracked assets through AssetDatabase so cached dirty materials cannot resurrect them after rollback.
            foreach (var original in originals.Where(item => item.Value == null && !item.Key.EndsWith(".meta", StringComparison.Ordinal)))
                if (File.Exists(original.Key)) AssetDatabase.DeleteAsset(original.Key);
            foreach (var original in originals)
            {
                if (original.Value != null) File.WriteAllBytes(original.Key, original.Value);
                else if (File.Exists(original.Key)) File.Delete(original.Key);
            }
            foreach (string path in originals.Keys.Where(path => !path.EndsWith(".meta", StringComparison.Ordinal)))
                if (File.Exists(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
