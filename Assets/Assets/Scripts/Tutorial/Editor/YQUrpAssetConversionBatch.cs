using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// note: This editor-only utility converts imported material assets and prefab renderer slots in small update slices so Unity remains responsive.
[InitializeOnLoad]
internal static class YQUrpAssetConversionBatch
{
    private const string VariantFolder = "Assets/Assets/GeneratedAssets/MaterialVariants/URP";
    private const string MenuRoot = "Tools/YourQuest/Materials/";
    private const string SessionKey = "YQUrpAssetConversionBatch_v3";
    private const int MaterialsPerTick = 8;
    private const int PrefabsPerTick = 8;

    private enum Phase
    {
        Materials,
        SerializedBindings,
        Prefabs,
        Verify
    }

    private static readonly Dictionary<string, Material> VariantCache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> GeneratedVariantPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> InvalidMaterialPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static string[] materialGuids = Array.Empty<string>();
    private static string[] prefabGuids = Array.Empty<string>();
    private static int materialIndex;
    private static int prefabIndex;
    private static Phase phase;
    private static bool running;
    private static bool cancelRequested;
    private static double startedAt;
    private static int convertedMaterialCount;
    private static int updatedPrefabCount;
    private static readonly List<string> ContractRows = new List<string>();
    private static string receiptDirectory;
    private static double nextStatusWrite;
    private static int verificationIndex;
    private static int remainingInvalidSlots;
    private static int repairErrors;
    private static int serializedIndex;
    private static readonly Dictionary<string, string> SerializedBindings = new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly Regex MaterialArrays = new Regex(@"(?m)^[ \t]*m_Materials:\r?\n(?<slots>(?:[ \t]*-[^\r\n]*\r?\n)*)", RegexOptions.Compiled);
    private static readonly Regex MaterialReference = new Regex(@"\{fileID: (?<id>-?\d+), guid: (?<guid>[0-9a-f]{32}), type: \d+\}", RegexOptions.Compiled);
    private static readonly List<string> VerificationRows = new List<string>();
    internal static bool IsRunning => running;
    [Serializable]
    private sealed class Checkpoint
    {
        public bool active;
        public string[] materials, prefabs, bindingKeys, bindingValues, contracts, verification;
        public int material, serialized, prefab, verified, invalid, errors, variants, updated, stage;
        public double started;
        public string receipt;
    }

    static YQUrpAssetConversionBatch()
    {
        // note: Imported-material conversion is now opt-in so Unity cannot restart a CPU-heavy prefab batch during editor startup, Play Mode, or video capture.
        // note: Domain reloads resume only an explicitly started operation in this same Editor session; other chats may compile scripts while source repairs run.
        RestoreCheckpoint();
        AssemblyReloadEvents.beforeAssemblyReload += SaveCheckpoint;
        // note: Recover the orphaned modal left by older batches after their completed receipt, without restarting an asset operation.
        EditorApplication.delayCall += ClearCompletedBatchProgress;
    }

    private static void ClearCompletedBatchProgress()
    {
        const string statusPath = "outputs/Asset_Contracts_20261002/Material_Repair.status";
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(statusPath))
            return;
        if (!File.ReadAllText(statusPath).StartsWith("state=completed", StringComparison.Ordinal))
            return;
        EditorUtility.ClearProgressBar();
        Debug.Log("[YQMaterialContracts] Cleared completed batch progress; no repair or import was restarted.");
    }

    [MenuItem(MenuRoot + "Convert Imported Materials to URP (Paced)", priority = 100)]
    private static void StartConversion()
    {
        // note: Serialized reference repairs must run only in stable Edit mode and never overlap a gameplay materialization transaction.
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        if (running)
        {
            Debug.Log("[YourQuest] URP material conversion is already running.");
            return;
        }

        EnsureVariantFolder();
        VariantCache.Clear();
        GeneratedVariantPaths.Clear();
        InvalidMaterialPaths.Clear();
        materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        Array.Sort(materialGuids, StringComparer.Ordinal);
        Array.Sort(prefabGuids, StringComparer.Ordinal);
        ContractRows.Clear();
        VerificationRows.Clear();
        verificationIndex = 0;
        remainingInvalidSlots = 0;
        repairErrors = 0;
        serializedIndex = 0;
        SerializedBindings.Clear();
        receiptDirectory = "outputs/Asset_Contracts_20261002/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(receiptDirectory);
        nextStatusWrite = 0;
        materialIndex = 0;
        prefabIndex = 0;
        phase = Phase.Materials;
        cancelRequested = false;
        running = true;
        startedAt = EditorApplication.timeSinceStartup;
        convertedMaterialCount = 0;
        updatedPrefabCount = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        SaveCheckpoint();
        Debug.Log($"[YourQuest] Started paced URP conversion: {materialGuids.Length} materials, {prefabGuids.Length} prefabs.");
    }

    // note: The existing explicit request dispatcher invokes the same menu operation; this is not an automatic import conversion.
    internal static void StartContractRepair() => StartConversion();

    [MenuItem(MenuRoot + "Cancel Paced URP Conversion", priority = 101)]
    private static void CancelConversion()
    {
        if (!running)
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        // note: Cancellation is cooperative so a prefab currently being saved can finish without leaving serialized data half-written.
        cancelRequested = true;
    }

    [MenuItem(MenuRoot + "Cancel Paced URP Conversion", validate = true)]
    private static bool ValidateCancelConversion()
    {
        Menu.SetChecked(MenuRoot + "Cancel Paced URP Conversion", running);
        return running;
    }

    private static void Tick()
    {
        if (!running)
            return;

        // note: A pending domain reload or import must finish before another serialized source asset is inspected or saved.
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        try
        {
            if (cancelRequested)
            {
                Finish("cancelled");
                return;
            }

            if (phase == Phase.Materials)
            {
                ProcessMaterials();
                if (materialIndex >= materialGuids.Length)
                    phase = Phase.SerializedBindings;
            }

            if (phase == Phase.SerializedBindings)
            {
                ProcessSerializedBindings();
                if (serializedIndex >= prefabGuids.Length)
                    phase = Phase.Prefabs;
            }

            if (phase == Phase.Prefabs)
            {
                ProcessPrefabs();
                if (prefabIndex >= prefabGuids.Length)
                    phase = Phase.Verify;
            }

            if (phase == Phase.Verify)
            {
                VerifyPrefabBindings();
                if (verificationIndex >= prefabGuids.Length)
                {
                    Finish(remainingInvalidSlots == 0 && repairErrors == 0 ? "completed" : "completed_with_unresolved_sources");
                    // note: Completion must not fall through and reopen the progress display that Finish just cleared.
                    return;
                }
            }

            UpdateProgress();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish("stopped after an error");
        }
    }

    private static void ProcessMaterials()
    {
        int processed = 0;
        while (materialIndex < materialGuids.Length && processed++ < MaterialsPerTick)
        {
            string path = AssetDatabase.GUIDToAssetPath(materialGuids[materialIndex++]);
            if (ShouldSkipPath(path))
                continue;

            Material source = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (source == null || YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(source))
                continue;

            InvalidMaterialPaths.Add(path);
            bool particleMaterial = LooksLikeParticleMaterial(path, source);
            Material replacement = CreateOrLoadVariant(source, path, particleMaterial);
            if (replacement != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string sourceGuid, out long sourceId) &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(replacement, out string targetGuid, out long targetId))
                SerializedBindings[sourceGuid + ":" + sourceId] = "{fileID: " + targetId + ", guid: " + targetGuid + ", type: 2}";
        }
    }

    private static void ProcessSerializedBindings()
    {
        // note: Exact known material GUID/local-ID substitutions preserve every other serialized byte. Null, model-subasset and billboard recovery still use the prefab API afterward.
        AssetDatabase.StartAssetEditing();
        try
        {
            // note: Submit the exact substitutions in one import transaction. Reimporting large site assemblies after each small group repeats the same dependency rebuild thousands of times.
            for (; serializedIndex < prefabGuids.Length;)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[serializedIndex++]);
                if (ShouldSkipPath(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                string before = File.ReadAllText(path);
                if (!before.StartsWith("%YAML", StringComparison.Ordinal)) continue;
                bool changed = false;
                string after = MaterialArrays.Replace(before, array =>
                {
                    string slots = array.Groups["slots"].Value;
                    string rebound = MaterialReference.Replace(slots, reference =>
                    {
                        string key = reference.Groups["guid"].Value + ":" + reference.Groups["id"].Value;
                        if (!SerializedBindings.TryGetValue(key, out string replacement) || replacement == reference.Value) return reference.Value;
                        changed = true;
                        ContractRows.Add("BOUND_SERIALIZED|" + path + "|source=" + key + "|replacement=" + replacement);
                        return replacement;
                    });
                    return array.Value.Substring(0, array.Value.Length - slots.Length) + rebound;
                });
                if (!changed) continue;
                string backup = Path.Combine(receiptDirectory, "Before", path);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(path, backup);
                byte[] bytes = File.ReadAllBytes(path);
                bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                File.WriteAllText(path, after, new UTF8Encoding(bom));
                AssetDatabase.ImportAsset(path);
                updatedPrefabCount++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
    }

    private static void ProcessPrefabs()
    {
        int processed = 0;
        while (prefabIndex < prefabGuids.Length && processed++ < PrefabsPerTick)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex++]);
            if (ShouldSkipPath(path))
                continue;
            if (!PrefabMayNeedRepair(path))
                continue;

            GameObject root = null;
            bool changed = false;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    continue;

                // note: Only exact persisted renderer-slot bindings can recover an absent material; an unrelated sibling is not a valid source contract.
                changed |= ApplyPersistedBindings(path, root);
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                    changed |= RepairRendererMaterials(renderers[i], path);

                if (changed)
                {
                    // note: Save only changed prefabs so imported package data and prefab overrides remain untouched when already valid.
                    string backup = Path.Combine(receiptDirectory, "Before", path);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new InvalidOperationException("Could not save repaired prefab: " + path);
                    updatedPrefabCount++;
                }
            }
            catch (Exception exception)
            {
                // note: One malformed imported asset must remain visible in the receipt without preventing repairs to the remaining library.
                ContractRows.Add("REPAIR_ERROR|" + path + "|" + exception.GetType().Name + "|" + exception.Message.Replace('\n', ' '));
                repairErrors++;
                Debug.LogError("[YQMaterialContracts] Could not repair " + path + ": " + exception);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static bool RepairRendererMaterials(Renderer renderer, string prefabPath)
    {
        if (renderer == null)
            return false;

        // note: Billboard materials live on the BillboardAsset rather than in the usual renderer slot array.
        bool billboardChanged = false;
        if (renderer is BillboardRenderer billboard && billboard.billboard != null && billboard.billboard.material != null && !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(billboard.billboard.material))
        {
            BillboardAsset sourceBillboard = billboard.billboard;
            Material sourceMaterial = sourceBillboard.material;
            Material replacement = CreateOrLoadVariant(sourceMaterial, AssetDatabase.GetAssetPath(sourceMaterial), false);
            string sourceBillboardPath = AssetDatabase.GetAssetPath(sourceBillboard);
            if (replacement != null && !string.IsNullOrWhiteSpace(sourceBillboardPath))
            {
                string adaptedPath = VariantFolder + "/Billboard_" + AssetDatabase.AssetPathToGUID(sourceBillboardPath) + "_" + sourceBillboard.GetInstanceID().ToString("X") + ".asset";
                // note: Subasset local IDs, unlike runtime instance IDs, keep the adapter stable across editor reloads.
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceBillboard, out string sourceGuid, out long localId))
                    adaptedPath = VariantFolder + "/Billboard_" + sourceGuid + "_" + localId + ".asset";
                BillboardAsset adapted = AssetDatabase.LoadAssetAtPath<BillboardAsset>(adaptedPath);
                if (adapted == null)
                {
                    adapted = UnityEngine.Object.Instantiate(sourceBillboard);
                    adapted.material = replacement;
                    AssetDatabase.CreateAsset(adapted, adaptedPath);
                }
                billboard.billboard = adapted;
                billboardChanged = true;
                ContractRows.Add("BOUND_BILLBOARD|" + prefabPath + "|source=" + sourceBillboardPath + "|replacement=" + adaptedPath);
            }
        }

        Material[] materials = renderer.sharedMaterials;
        int required = YQRuntimeUrpMaterialRepair.ResolveRequiredMaterialSlotCount(renderer);
        if (materials.Length < required) Array.Resize(ref materials, required);
        if (materials == null || materials.Length == 0)
            return billboardChanged;

        bool changed = billboardChanged;
        for (int slot = 0; slot < materials.Length; slot++)
        {
            if (!YQRuntimeUrpMaterialRepair.IsMaterialSlotRequired(renderer, slot)) continue;
            Material source = materials[slot];
            if (YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(source))
                continue;

            // note: A renderer sharing the exact imported mesh and slot layout provides provenance for a missing serialized slot.
            if (source == null) source = RecoverImportedMeshMaterial(renderer, slot, materials.Length);

            string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
            bool particleMaterial = renderer is ParticleSystemRenderer || LooksLikeParticleMaterial(sourcePath, source);
            Material replacement = null;
            if (source != null && !string.IsNullOrWhiteSpace(sourcePath))
                replacement = CreateOrLoadVariant(source, sourcePath, particleMaterial);
            if (replacement == null && YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(source))
                replacement = source;

            string rendererPath = UnityEditor.AnimationUtility.CalculateTransformPath(renderer.transform, renderer.transform.root);
            ContractRows.Add((replacement != null ? "BOUND" : "UNRESOLVED") + "|" + prefabPath + "|" + rendererPath + "|slot=" + slot + "|source=" + sourcePath + "|replacement=" + (replacement != null ? AssetDatabase.GetAssetPath(replacement) : "missing_source"));

            if (replacement != null && materials[slot] != replacement)
            {
                materials[slot] = replacement;
                changed = true;
            }
        }

        if (changed)
            renderer.sharedMaterials = materials;
        return changed;
    }

    private static Material RecoverImportedMeshMaterial(Renderer renderer, int slot, int slotCount)
    {
        Mesh mesh = GetRendererMesh(renderer);
        string meshPath = mesh != null ? AssetDatabase.GetAssetPath(mesh) : string.Empty;
        if (string.IsNullOrWhiteSpace(meshPath)) return null;
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
        if (model == null) return null;
        Material candidate = null;
        foreach (Renderer sourceRenderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Mesh sourceMesh = GetRendererMesh(sourceRenderer);
            Material[] sourceSlots = sourceRenderer.sharedMaterials;
            if (sourceMesh != mesh || sourceSlots.Length != slotCount || sourceSlots[slot] == null) continue;
            if (candidate != null && candidate != sourceSlots[slot]) return null;
            candidate = sourceSlots[slot];
        }
        return candidate;
    }

    private static Mesh GetRendererMesh(Renderer renderer)
    {
        // note: Unity's absent components are fake-null objects; the C# null-conditional operator does not protect their native property getters.
        if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private static bool ApplyPersistedBindings(string path, GameObject root)
    {
        string shardPath = "Assets/Assets/Resources/" + YQRuntimeWorldAssetRegistry.BuildShardResourcePath(path) + ".asset";
        YQRuntimeWorldAssetRegistry shard = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(shardPath);
        if (shard == null) return false;
        bool changed = false;
        foreach (YQRuntimeWorldAssetEntry entry in shard.Entries)
        {
            if (entry == null || !string.Equals(entry.assetPath, path, StringComparison.OrdinalIgnoreCase) || entry.materialOverrides == null) continue;
            foreach (YQRuntimeWorldMaterialOverride binding in entry.materialOverrides)
            {
                if (binding == null || !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(binding.replacementMaterial)) continue;
                Transform target = string.IsNullOrEmpty(binding.transformPath) ? root.transform : root.transform.Find(binding.transformPath);
                Renderer[] renderers = target != null ? target.GetComponents<Renderer>() : Array.Empty<Renderer>();
                if (binding.rendererIndex < 0 || binding.rendererIndex >= renderers.Length) continue;
                Renderer renderer = renderers[binding.rendererIndex];
                Material[] slots = renderer.sharedMaterials;
                if (binding.materialIndex < 0 || binding.materialIndex >= slots.Length || YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(slots[binding.materialIndex])) continue;
                if (!YQRuntimeUrpMaterialRepair.IsMaterialSlotRequired(renderer, binding.materialIndex)) continue;
                slots[binding.materialIndex] = binding.replacementMaterial;
                renderer.sharedMaterials = slots;
                changed = true;
                ContractRows.Add("BOUND_PERSISTED|" + path + "|" + binding.transformPath + "|slot=" + binding.materialIndex + "|replacement=" + AssetDatabase.GetAssetPath(binding.replacementMaterial));
            }
        }
        return changed;
    }

    private static bool PrefabMayNeedRepair(string prefabPath)
    {
        // note: Null slots have no dependency GUID, and nested prefabs can hide broken descendants from direct dependency filtering.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return false;
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is BillboardRenderer billboard && (billboard.billboard == null || !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(billboard.billboard.material))) return true;
            Material[] materials = renderer.sharedMaterials;
            int required = YQRuntimeUrpMaterialRepair.ResolveRequiredMaterialSlotCount(renderer);
            for (int slot = 0; slot < required; slot++)
                if (YQRuntimeUrpMaterialRepair.IsMaterialSlotRequired(renderer, slot) && !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(slot < materials.Length ? materials[slot] : null)) return true;
        }

        return false;
    }

    private static void VerifyPrefabBindings()
    {
        // note: Re-read every final source prefab without applying overrides or runtime repair; this proves the serialized contract itself.
        for (int count = 0; verificationIndex < prefabGuids.Length && count < PrefabsPerTick; count++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[verificationIndex++]);
            if (ShouldSkipPath(path)) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { VerificationRows.Add("MISSING_PREFAB|" + path); remainingInvalidSlots++; continue; }
            int slots = 0, invalid = 0;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is BillboardRenderer billboard)
                {
                    slots++;
                    if (billboard.billboard == null || !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(billboard.billboard.material)) invalid++;
                }
                Material[] materials = renderer.sharedMaterials;
                int required = YQRuntimeUrpMaterialRepair.ResolveRequiredMaterialSlotCount(renderer);
                for (int slot = 0; slot < required; slot++)
                {
                    if (!YQRuntimeUrpMaterialRepair.IsMaterialSlotRequired(renderer, slot)) continue;
                    slots++;
                    if (!YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(slot < materials.Length ? materials[slot] : null)) invalid++;
                }
            }
            remainingInvalidSlots += invalid;
            VerificationRows.Add((invalid == 0 ? "PASS" : "UNRESOLVED") + "|" + path + "|slots=" + slots + "|invalid=" + invalid);
        }
    }

    private static Material CreateOrLoadVariant(Material source, string sourcePath, bool particleMaterial)
    {
        if (source == null || string.IsNullOrWhiteSpace(sourcePath) || sourcePath.StartsWith(VariantFolder + "/", StringComparison.OrdinalIgnoreCase))
            return null;

        // note: Imported model submaterials and engine defaults share asset paths; their GUID/local-ID pair prevents assigning another surface's textures.
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long sourceId)) return null;
        string cacheKey = guid + ":" + sourceId + (particleMaterial ? "|particle" : "|lit");
        if (VariantCache.TryGetValue(cacheKey, out Material cached))
            return cached;

        // note: Prefer an installed vendor counterpart or source-GUID adapter before producing another source-derived material.
        if (sourcePath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) && YQWorldAssetIntakeBuilder.TryResolveVerifiedRuntimeMaterial(source, out Material verified) && YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(verified))
        {
            VariantCache[cacheKey] = verified;
            return verified;
        }

        string suffix = particleMaterial ? "_Particles" : "_Lit";
        string fileName = SanitizeFileName(source.name) + "__URP_" + guid + (sourceId == 2100000 ? string.Empty : "_" + sourceId) + suffix + ".mat";
        string outputPath = VariantFolder + "/" + fileName;
        Material variant = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
        if (variant == null)
        {
            variant = YQRuntimeUrpMaterialRepair.CreateEditorUrpMaterialVariant(source, null, particleMaterial);
            if (variant == null)
                return null;

            variant.name = fileName.Substring(0, fileName.Length - 4);
            AssetDatabase.CreateAsset(variant, outputPath);
            EditorUtility.SetDirty(variant);
            convertedMaterialCount++;
        }

        if (!YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(variant))
            return null;

        GeneratedVariantPaths.Add(outputPath);
        VariantCache[cacheKey] = variant;
        return variant;
    }

    private static void Finish(string state)
    {
        if (!running)
            return;

        running = false;
        EditorApplication.update -= Tick;
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        double seconds = EditorApplication.timeSinceStartup - startedAt;
        // note: Keep a durable per-slot receipt and untouched original prefab bytes for manual review of this explicitly requested repair.
        File.WriteAllLines(Path.Combine(receiptDirectory, "Material_Bindings.txt"), ContractRows);
        File.WriteAllLines(Path.Combine(receiptDirectory, "Final_Prefab_Contracts.txt"), VerificationRows);
        File.WriteAllText(Path.Combine(receiptDirectory, "Summary.txt"), $"state={state}\nutc={DateTime.UtcNow:O}\nmaterialVariants={convertedMaterialCount}\nupdatedPrefabs={updatedPrefabCount}\nmaterialsInspected={materialIndex}/{materialGuids.Length}\nprefabsInspected={prefabIndex}/{prefabGuids.Length}\nprefabsVerified={verificationIndex}/{prefabGuids.Length}\nremainingInvalidSlots={remainingInvalidSlots}\nelapsedSeconds={seconds:F1}\n");
        WriteStatus(state);
        Debug.Log($"[YourQuest] Paced URP conversion {state}: {convertedMaterialCount} material variants, {updatedPrefabCount} prefabs, {seconds:0.0}s.");
    }

    private static void UpdateProgress()
    {
        // note: Paced work reports durable progress without leaving a modal over an otherwise responsive Editor.
        if (!running)
            return;
        if (EditorApplication.timeSinceStartup >= nextStatusWrite)
        {
            nextStatusWrite = EditorApplication.timeSinceStartup + 1d;
            WriteStatus("running");
        }
    }

    private static void WriteStatus(string state)
    {
        SaveCheckpoint();
        Directory.CreateDirectory("outputs/Asset_Contracts_20261002");
        File.WriteAllText("outputs/Asset_Contracts_20261002/Material_Repair.status", $"state={state}\nphase={phase}\nmaterials={materialIndex}/{materialGuids.Length}\nprefabs={prefabIndex}/{prefabGuids.Length}\nverified={verificationIndex}/{prefabGuids.Length}\nremainingInvalidSlots={remainingInvalidSlots}\nvariants={convertedMaterialCount}\nupdatedPrefabs={updatedPrefabCount}\nreceipt={receiptDirectory}\nutc={DateTime.UtcNow:O}\n");
    }

    private static void SaveCheckpoint()
    {
        if (string.IsNullOrEmpty(receiptDirectory)) return;
        string path = Path.Combine(receiptDirectory, "Checkpoint.json");
        var keys = new List<string>(SerializedBindings.Keys);
        var values = new List<string>();
        foreach (string key in keys) values.Add(SerializedBindings[key]);
        Checkpoint checkpoint = new Checkpoint { active = running, materials = materialGuids, prefabs = prefabGuids,
            bindingKeys = keys.ToArray(), bindingValues = values.ToArray(), contracts = ContractRows.ToArray(), verification = VerificationRows.ToArray(),
            material = materialIndex, serialized = serializedIndex, prefab = prefabIndex, verified = verificationIndex,
            invalid = remainingInvalidSlots, errors = repairErrors, variants = convertedMaterialCount, updated = updatedPrefabCount,
            stage = (int)phase, started = startedAt, receipt = receiptDirectory };
        File.WriteAllText(path, JsonUtility.ToJson(checkpoint));
        SessionState.SetString(SessionKey, running ? path : string.Empty);
    }

    private static void RestoreCheckpoint()
    {
        string path = SessionState.GetString(SessionKey, string.Empty);
        if (running || string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        Checkpoint checkpoint = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(path));
        if (checkpoint == null || !checkpoint.active) return;
        materialGuids = checkpoint.materials; prefabGuids = checkpoint.prefabs;
        materialIndex = checkpoint.material; serializedIndex = checkpoint.serialized; prefabIndex = checkpoint.prefab; verificationIndex = checkpoint.verified;
        remainingInvalidSlots = checkpoint.invalid; repairErrors = checkpoint.errors; convertedMaterialCount = checkpoint.variants; updatedPrefabCount = checkpoint.updated;
        phase = (Phase)checkpoint.stage; startedAt = checkpoint.started; receiptDirectory = checkpoint.receipt;
        ContractRows.Clear(); ContractRows.AddRange(checkpoint.contracts);
        VerificationRows.Clear(); VerificationRows.AddRange(checkpoint.verification);
        SerializedBindings.Clear();
        for (int index = 0; index < checkpoint.bindingKeys.Length; index++) SerializedBindings.Add(checkpoint.bindingKeys[index], checkpoint.bindingValues[index]);
        running = true; nextStatusWrite = 0;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Debug.Log("[YQMaterialContracts] Resumed explicit source repair after compilation: " + receiptDirectory);
    }

    private static bool ShouldSkipPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Assets/_Recovery/", StringComparison.OrdinalIgnoreCase) || path.StartsWith(VariantFolder + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeParticleMaterial(string path, Material material)
    {
        string text = ((path ?? string.Empty) + " " + (material != null ? material.name : string.Empty)).ToLowerInvariant();
        return text.Contains("particle") || text.Contains("vfx") || text.Contains("trail") || text.Contains("line");
    }

    private static void EnsureVariantFolder()
    {
        string[] segments = VariantFolder.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }

    private static string SanitizeFileName(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "Material" : value;
        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            result = result.Replace(invalid.ToString(), string.Empty);
        return string.IsNullOrWhiteSpace(result) ? "Material" : result;
    }

}
// note: The pass remains menu-addressable for repeat imports after this session completes.
