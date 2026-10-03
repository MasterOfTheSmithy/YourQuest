using System;
using System.Collections.Generic;
using UnityEngine;

// note: Supplied asset identities and compatibility data are presentation metadata, not item mechanics or a second inventory authority.
[Serializable]
public sealed class YQDotEquipmentEntry
{
    public string assetId;
    public string family;
    public string category;
    public string referenceBody;
    public string moduleSlot;
    public string compatibilityId;
    public string style;
    public string materialKey;
    public string[] requiredMaterialKeys = Array.Empty<string>();
    public string[] requiredModuleSlots = Array.Empty<string>();
    public string sourcePath;
    public string prefabPath;
    public string partsSourcePath;
    public string sourceSha256;
    public string bodyMaskJson;
    public string sourceContractJson;
    public bool assembled;
    public bool generationEligible;
    public string eligibilityReason;
}

// note: Harvest identities and visual signatures are asset metadata; drop rules, quantities and item mechanics remain with the game's state owners.
[Serializable]
public sealed class YQDotCraftingMaterial
{
    public string materialKey;
    public string species;
    public string partType;
    public string displayName;
    public string[] visualAssetIds = Array.Empty<string>();
    public string sourceContractJson;
}

[CreateAssetMenu(menuName = "YourQuest/DOT Equipment Catalog")]
public sealed class YQDotEquipmentCatalog : ScriptableObject
{
    public List<YQDotEquipmentEntry> entries = new List<YQDotEquipmentEntry>();
    public List<YQDotCraftingMaterial> craftingMaterials = new List<YQDotCraftingMaterial>();
    public string craftingSchemaVersion;
    public string craftingFitContractsJson;
    public string craftingRecipeExamplesJson;
    public string craftingQualityPolicyJson;
    public string craftingReleaseVersion;
    public string craftingOrnamentRecipeExamplesJson;
    public string craftingCoverageSchemaVersion, craftingCoverageRecipeExamplesJson;
    private Dictionary<string, YQDotEquipmentEntry> _byId;
    private Dictionary<string, YQDotEquipmentEntry> _byPrefab;

    private void OnEnable() => RebuildLookup();
    public void RebuildLookup()
    {
        // note: Duplicate identities are errors at intake; runtime lookup never rewrites the selected asset or persistent item record.
        _byId = new Dictionary<string, YQDotEquipmentEntry>(StringComparer.Ordinal);
        _byPrefab = new Dictionary<string, YQDotEquipmentEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.assetId)) continue;
            _byId.Add(entry.assetId, entry);
            if (!string.IsNullOrEmpty(entry.prefabPath)) _byPrefab.Add(entry.prefabPath, entry);
        }
    }

    public bool TryGetPrefab(string path, out YQDotEquipmentEntry entry)
    { if (_byPrefab == null) RebuildLookup(); return _byPrefab.TryGetValue(path ?? string.Empty, out entry); }

    public bool TryGetAsset(string id, out YQDotEquipmentEntry entry)
    { if (_byId == null) RebuildLookup(); return _byId.TryGetValue(id ?? string.Empty, out entry); }

    public bool TryGetCraftingMaterial(string species, string partType, out YQDotCraftingMaterial material)
    {
        // note: Resolve only supplied typed harvest identities; narrative text never selects executable behavior or arbitrary asset paths.
        foreach (var candidate in craftingMaterials)
            if (candidate != null && string.Equals(candidate.species, species, StringComparison.Ordinal) && string.Equals(candidate.partType, partType, StringComparison.Ordinal))
            { material = candidate; return true; }
        material = null; return false;
    }

    public bool TryValidateCraftingMaterials(string visualId, IReadOnlyList<string> availableKeys, out string reason)
    {
        // note: Compound surface signatures require every declared anatomical material; this query does not consume inventory or award mechanics.
        if (!TryGetAsset(visualId, out var entry) || availableKeys == null) { reason = "Unknown visual or absent material identities."; return false; }
        var required = entry.requiredMaterialKeys != null && entry.requiredMaterialKeys.Length > 0 ? entry.requiredMaterialKeys : new[] { entry.materialKey };
        foreach (string key in required)
        {
            bool present = false;
            for (int i = 0; i < availableKeys.Count; i++) if (availableKeys[i] == key) { present = true; break; }
            if (string.IsNullOrEmpty(key) || !present) { reason = "Missing required anatomical material: " + key; return false; }
        }
        reason = string.Empty; return true;
    }

    public bool TryValidateModules(IReadOnlyList<string> assetIds, out string reason)
    {
        reason = string.Empty;
        if (assetIds == null || assetIds.Count == 0) { reason = "No assembly modules supplied."; return false; }
        string family = null, body = null, category = null, surfaceInterface = null;
        string[] required = null;
        var slots = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in assetIds)
        {
            if (!TryGetAsset(id, out var entry) || string.IsNullOrEmpty(entry.moduleSlot) || entry.assembled)
            { reason = "Unknown module or complete assembly supplied as a part: " + id; return false; }
            // note: Unity serializes an absent rigid-body label as an empty string; it is the same interface before and after catalog reload.
            string entryBody = entry.referenceBody ?? string.Empty;
            if (family == null) { family = entry.family; body = entryBody; category = entry.category; required = entry.requiredModuleSlots; }
            if (entry.family != family || entryBody != body || entry.category != category || !slots.Add(entry.moduleSlot))
            { reason = "Modules cross a family/body boundary or repeat a slot."; return false; }
            if (entry.category != "weapon")
            {
                if (surfaceInterface == null) surfaceInterface = entry.compatibilityId;
                if (entry.compatibilityId != surfaceInterface) { reason = "Surface modules require the exact same core interface."; return false; }
            }
            else if (entry.compatibilityId != "yqmod1_" + family + "_" + entry.moduleSlot)
            { reason = "Weapon module does not declare its family-slot interface."; return false; }
        }
        // note: Enforce complete recipes; the caller cannot silently omit a required grip/head or reference core.
        if (required == null || required.Length != 3) { reason = "Assembly requires a validated three-slot interface."; return false; }
        foreach (string slot in required) if (!slots.Contains(slot)) { reason = "Missing assembly slot: " + slot; return false; }
        if (slots.Count != required.Length) { reason = "Unexpected assembly slot."; return false; }
        return true;
    }

    public static bool TryResolvePaletteRemainder(IReadOnlyList<float> weights, out float neutral, out string reason)
    {
        neutral = 1f; reason = string.Empty;
        if (weights == null) { reason = "No palette weights supplied."; return false; }
        double sum = 0d;
        foreach (float value in weights)
        { if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) { reason = "Palette weights must be finite and nonnegative."; return false; } sum += value; }
        if (sum > 1d + 0.000001d) { reason = "Palette weights exceed one."; return false; }
        neutral = Mathf.Max(0f, 1f - (float)sum);
        return true;
    }

    public bool TryCreateRigidAssembly(IReadOnlyList<string> assetIds, Transform parent, out GameObject assembly, out string reason)
    {
        assembly = null;
        if (!TryValidateModules(assetIds, out reason)) return false;
        var prefabs = new GameObject[assetIds.Count];
        for (int index = 0; index < assetIds.Count; index++)
        {
            TryGetAsset(assetIds[index], out var entry);
            if (!string.IsNullOrEmpty(entry.referenceBody)) { reason = "Fitted modules require verified garment binding."; return false; }
            prefabs[index] = YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(entry.prefabPath);
            if (prefabs[index] == null || prefabs[index].GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            { reason = "Rigid module reference unavailable or requires garment binding: " + entry.assetId; return false; }
        }
        // note: Delivered modules share a family-local hand/core frame. Keep identity placement and authored material roles; no second gameplay owner is created.
        assembly = new GameObject("DOT assembly"); assembly.transform.SetParent(parent, false);
        foreach (GameObject prefab in prefabs) Instantiate(prefab, assembly.transform, false);
        reason = string.Empty;
        return true;
    }
}
