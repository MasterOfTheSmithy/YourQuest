using System;
using System.Collections.Generic;
using UnityEngine;

// note: The intake catalog has its own script asset so Unity can deserialize it independently from the discovered runtime catalog.
[CreateAssetMenu(
    fileName = "YQWorldAssetIntakeCatalog",
    menuName = "YourQuest/AAA World Asset Intake Catalog")]
public sealed class YQWorldAssetIntakeCatalog : ScriptableObject
{
    public const string CurrentSchemaVersion = "world_asset_intake_v3";

    [SerializeField] private string schemaVersion = CurrentSchemaVersion;
    [SerializeField] private string scanScope = string.Empty;
    [SerializeField] private string generatedUtc = string.Empty;
    [SerializeField] private List<YQAssetKitManifest> kits = new List<YQAssetKitManifest>();
    [SerializeField] private List<YQSpatialAssetRecord> spatialAssets = new List<YQSpatialAssetRecord>();
    [SerializeField] private List<YQMaterialAssetRecord> materials = new List<YQMaterialAssetRecord>();
    [SerializeField] private List<YQAssetLibraryEvidenceRecord> libraryEvidence = new List<YQAssetLibraryEvidenceRecord>();

    public string SchemaVersion => schemaVersion;
    public string ScanScope => scanScope;
    public string GeneratedUtc => generatedUtc;
    public IReadOnlyList<YQAssetKitManifest> Kits => kits;
    public IReadOnlyList<YQSpatialAssetRecord> SpatialAssets => spatialAssets;
    public IReadOnlyList<YQMaterialAssetRecord> Materials => materials;
    public IReadOnlyList<YQAssetLibraryEvidenceRecord> LibraryEvidence => libraryEvidence;

    public void SetRecords(string newScanScope, string newGeneratedUtc,
        List<YQAssetKitManifest> newKits, List<YQSpatialAssetRecord> newSpatialAssets,
        List<YQMaterialAssetRecord> newMaterials)
    {
        // note: Existing scan callers refresh technical records while retaining every documentary state and link.
        SetRecords(newScanScope, newGeneratedUtc, newKits, newSpatialAssets, newMaterials, null);
    }

    public void SetRecords(string newScanScope, string newGeneratedUtc,
        List<YQAssetKitManifest> newKits, List<YQSpatialAssetRecord> newSpatialAssets,
        List<YQMaterialAssetRecord> newMaterials, List<YQAssetLibraryEvidenceRecord> newLibraryEvidence)
    {
        // note: Evidence preservation uses source identity independently of eligibility; held and unreviewed records survive too.
        PreserveDocumentaryLinks(newKits, newSpatialAssets, newMaterials);
        // note: One editor transaction replaces the complete intake snapshot so stale eligibility cannot survive a rescan.
        schemaVersion = CurrentSchemaVersion;
        scanScope = newScanScope ?? string.Empty;
        generatedUtc = newGeneratedUtc ?? string.Empty;
        kits = newKits ?? new List<YQAssetKitManifest>();
        spatialAssets = newSpatialAssets ?? new List<YQSpatialAssetRecord>();
        materials = newMaterials ?? new List<YQMaterialAssetRecord>();
        if (newLibraryEvidence != null)
            libraryEvidence = newLibraryEvidence;
        EnsureCollections();
    }

    public void EnsureCollections()
    {
        kits ??= new List<YQAssetKitManifest>();
        spatialAssets ??= new List<YQSpatialAssetRecord>();
        materials ??= new List<YQMaterialAssetRecord>();
        libraryEvidence ??= new List<YQAssetLibraryEvidenceRecord>();
        for (int i = 0; i < kits.Count; i++) kits[i]?.EnsureCollections();
        for (int i = 0; i < spatialAssets.Count; i++) spatialAssets[i]?.EnsureCollections();
        for (int i = 0; i < materials.Count; i++) materials[i]?.EnsureCollections();
        for (int i = 0; i < libraryEvidence.Count; i++) libraryEvidence[i]?.EnsureCollections();
    }

    private void PreserveDocumentaryLinks(List<YQAssetKitManifest> newKits,
        List<YQSpatialAssetRecord> newSpatialAssets, List<YQMaterialAssetRecord> newMaterials)
    {
        // note: A GUID follows a moved asset; path fallback is reserved for records that have neither GUID nor stable ID.
        Dictionary<string, List<string>> kitLinks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<string>> assetLinks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (kits != null)
            for (int i = 0; i < kits.Count; i++)
                if (kits[i] != null && !string.IsNullOrWhiteSpace(kits[i].kitId))
                    AccumulateEvidenceIds(kitLinks, kits[i].kitId, kits[i].libraryEvidenceIds);
        if (spatialAssets != null)
            for (int i = 0; i < spatialAssets.Count; i++)
                if (spatialAssets[i] != null)
                    AccumulateEvidenceIds(assetLinks, DocumentaryAssetKey(spatialAssets[i].sourceGuid,
                        spatialAssets[i].stableAssetId, spatialAssets[i].assetPath), spatialAssets[i].libraryEvidenceIds);
        if (materials != null)
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null)
                    AccumulateEvidenceIds(assetLinks, DocumentaryAssetKey(materials[i].sourceGuid,
                        materials[i].stableAssetId, materials[i].assetPath), materials[i].libraryEvidenceIds);

        if (newKits != null)
            for (int i = 0; i < newKits.Count; i++)
                if (newKits[i] != null && !string.IsNullOrWhiteSpace(newKits[i].kitId) &&
                    kitLinks.TryGetValue(newKits[i].kitId, out List<string> kitIds))
                    newKits[i].libraryEvidenceIds = MergeEvidenceIds(newKits[i].libraryEvidenceIds, kitIds);
        if (newSpatialAssets != null)
            for (int i = 0; i < newSpatialAssets.Count; i++)
                if (newSpatialAssets[i] != null && assetLinks.TryGetValue(DocumentaryAssetKey(newSpatialAssets[i].sourceGuid,
                    newSpatialAssets[i].stableAssetId, newSpatialAssets[i].assetPath), out List<string> spatialIds))
                    newSpatialAssets[i].libraryEvidenceIds = MergeEvidenceIds(newSpatialAssets[i].libraryEvidenceIds, spatialIds);
        if (newMaterials != null)
            for (int i = 0; i < newMaterials.Count; i++)
                if (newMaterials[i] != null && assetLinks.TryGetValue(DocumentaryAssetKey(newMaterials[i].sourceGuid,
                    newMaterials[i].stableAssetId, newMaterials[i].assetPath), out List<string> materialIds))
                    newMaterials[i].libraryEvidenceIds = MergeEvidenceIds(newMaterials[i].libraryEvidenceIds, materialIds);
    }

    private static string DocumentaryAssetKey(string sourceGuid, string stableAssetId, string assetPath)
    {
        if (!string.IsNullOrWhiteSpace(sourceGuid)) return "guid:" + sourceGuid;
        if (!string.IsNullOrWhiteSpace(stableAssetId)) return "stable:" + stableAssetId;
        return "path:" + (assetPath ?? string.Empty).Replace('\\', '/');
    }

    private static void AccumulateEvidenceIds(Dictionary<string, List<string>> index, string key, List<string> ids)
    {
        if (string.IsNullOrWhiteSpace(key) || key == "path:") return;
        index.TryGetValue(key, out List<string> previous);
        index[key] = MergeEvidenceIds(previous, ids);
    }

    private static List<string> MergeEvidenceIds(List<string> first, List<string> second)
    {
        List<string> merged = first != null ? new List<string>(first) : new List<string>();
        if (second != null)
            for (int i = 0; i < second.Count; i++)
                if (!merged.Contains(second[i])) merged.Add(second[i]);
        return merged;
    }

    public void RecalculateKitSpatialCounts()
    {
        EnsureCollections();
        Dictionary<string, YQAssetKitManifest> kitsById = new Dictionary<string, YQAssetKitManifest>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < kits.Count; i++)
        {
            YQAssetKitManifest kit = kits[i];
            if (kit == null || string.IsNullOrWhiteSpace(kit.kitId)) continue;
            kit.prefabCount = 0;
            kit.candidatePrefabCount = 0;
            kit.repairRequiredPrefabCount = 0;
            kit.spatialReviewPrefabCount = 0;
            kitsById[kit.kitId] = kit;
        }
        for (int i = 0; i < spatialAssets.Count; i++)
        {
            YQSpatialAssetRecord record = spatialAssets[i];
            if (record == null || string.IsNullOrWhiteSpace(record.kitId) ||
                !kitsById.TryGetValue(record.kitId, out YQAssetKitManifest kit)) continue;
            kit.prefabCount++;
            switch (record.disposition)
            {
                case YQAssetIntakeDisposition.Candidate:
                    kit.candidatePrefabCount++;
                    break;
                case YQAssetIntakeDisposition.NeedsSpatialReview:
                    kit.spatialReviewPrefabCount++;
                    break;
                default:
                    kit.repairRequiredPrefabCount++;
                    break;
            }
        }
    }
}
