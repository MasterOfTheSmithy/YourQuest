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

    public string SchemaVersion => schemaVersion;
    public string ScanScope => scanScope;
    public string GeneratedUtc => generatedUtc;
    public IReadOnlyList<YQAssetKitManifest> Kits => kits;
    public IReadOnlyList<YQSpatialAssetRecord> SpatialAssets => spatialAssets;
    public IReadOnlyList<YQMaterialAssetRecord> Materials => materials;

    public void SetRecords(string newScanScope, string newGeneratedUtc,
        List<YQAssetKitManifest> newKits, List<YQSpatialAssetRecord> newSpatialAssets,
        List<YQMaterialAssetRecord> newMaterials)
    {
        // note: One editor transaction replaces the complete intake snapshot so stale eligibility cannot survive a rescan.
        schemaVersion = CurrentSchemaVersion;
        scanScope = newScanScope ?? string.Empty;
        generatedUtc = newGeneratedUtc ?? string.Empty;
        kits = newKits ?? new List<YQAssetKitManifest>();
        spatialAssets = newSpatialAssets ?? new List<YQSpatialAssetRecord>();
        materials = newMaterials ?? new List<YQMaterialAssetRecord>();
        EnsureCollections();
    }

    public void EnsureCollections()
    {
        kits ??= new List<YQAssetKitManifest>();
        spatialAssets ??= new List<YQSpatialAssetRecord>();
        materials ??= new List<YQMaterialAssetRecord>();
        for (int i = 0; i < kits.Count; i++) kits[i]?.EnsureCollections();
        for (int i = 0; i < spatialAssets.Count; i++) spatialAssets[i]?.EnsureCollections();
        for (int i = 0; i < materials.Count; i++) materials[i]?.EnsureCollections();
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
