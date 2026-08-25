using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "YQDiscoveredWorldAssetCatalog",
    menuName = "YourQuest/Discovered World Asset Catalog")]
public sealed class YQDiscoveredWorldAssetCatalog : ScriptableObject
{
    private const string ResourcesAssetName =
        "YQDiscoveredWorldAssetCatalog";

    [SerializeField]
    private List<GeneratedAssetReferenceRecord> entries =
        new List<GeneratedAssetReferenceRecord>();

    private static YQDiscoveredWorldAssetCatalog _instance;

    public IReadOnlyList<GeneratedAssetReferenceRecord> Entries
    {
        get
        {
            return entries;
        }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeStatics()
    {
        _instance =
            null;
    }

    public static YQDiscoveredWorldAssetCatalog Instance
    {
        get
        {
            if (_instance == null)
            {
                // note: Runtime loads the editor-discovered semantic catalog from Resources; it never uses AssetDatabase.
                _instance =
                    Resources.Load<
                        YQDiscoveredWorldAssetCatalog>(
                            ResourcesAssetName);

                _instance?.EnsureCollections();
            }

            return _instance;
        }
    }

    public void SetEntries(
        List<GeneratedAssetReferenceRecord> newEntries)
    {
        entries =
            newEntries ??
            new List<GeneratedAssetReferenceRecord>();

        EnsureCollections();
    }

    public void EnsureCollections()
    {
        entries ??=
            new List<GeneratedAssetReferenceRecord>();

        for (int i = entries.Count - 1;
             i >= 0;
             i--)
        {
            GeneratedAssetReferenceRecord entry =
                entries[i];

            if (entry == null ||
                string.IsNullOrWhiteSpace(
                    entry.assetPath) ||
                string.IsNullOrWhiteSpace(
                    entry.slotTag))
            {
                entries.RemoveAt(
                    i);

                continue;
            }

            entry.EnsureCollections();
        }
    }

    public static void ClearCachedInstance()
    {
        _instance =
            null;
    }
}

public enum YQSpatialCompositionScale
{
    Unknown = 0,
    Atom = 1,
    Module = 2,
    Prop = 3,
    CompleteBuilding = 4,
    ParcelAssembly = 5,
    StreetAssembly = 6,
    DistrictAssembly = 7,
    InteriorAssembly = 8,
    Landmark = 9,
    CharacterOrCreature = 10
}

public enum YQAssetIntakeDisposition
{
    NeedsSpatialReview = 0,
    Candidate = 1,
    NeedsMaterialRepair = 2,
    MissingRenderer = 3,
    MissingScriptRepair = 4,
    EditorOrDemoOnly = 5,
    Quarantined = 6
}

public enum YQMaterialCompatibilityState
{
    Unknown = 0,
    VerifiedUrp = 1,
    NeedsReview = 2,
    LegacyPipeline = 3,
    UnsupportedShader = 4,
    MissingShader = 5,
    VerifiedUrpAdapter = 6
}

[Serializable]
public sealed class YQAssetKitManifest
{
    public string kitId;
    public string displayName;
    public string sourceRoot;
    public bool isFirstBenchmarkKit;
    public bool releaseEligible;
    public int totalDiscoveredAssetCount;
    public int prefabCount;
    public int materialCount;
    public int candidatePrefabCount;
    public int repairRequiredPrefabCount;
    public int spatialReviewPrefabCount;
    public int verifiedMaterialCount;
    public int materialReviewOrRepairCount;
    public List<string> genreTags = new List<string>();
    public List<string> environmentTags = new List<string>();
    public List<string> compatiblePrimaryKitIds = new List<string>();
    public List<string> compatibleAccentKitIds = new List<string>();
    public List<string> forbiddenKitIds = new List<string>();
    public List<string> validationIssues = new List<string>();

    public void EnsureCollections()
    {
        genreTags ??= new List<string>();
        environmentTags ??= new List<string>();
        compatiblePrimaryKitIds ??= new List<string>();
        compatibleAccentKitIds ??= new List<string>();
        forbiddenKitIds ??= new List<string>();
        validationIssues ??= new List<string>();
    }
}

[Serializable]
public sealed class YQSpatialAssetRecord
{
    public string stableAssetId;
    public string sourceGuid;
    public string sourceAssetKey;
    public string assetPath;
    public string kitId;
    public string semanticRole;
    public YQSpatialCompositionScale compositionScale;
    public YQAssetIntakeDisposition disposition;
    public bool releaseEligible;
    public Vector3 localBoundsCenter;
    public Vector3 localBoundsSize;
    public Vector3 clearanceSize;
    public float footprintX;
    public float footprintZ;
    public float height;
    public Vector3 frontDirection = Vector3.forward;
    public bool frontDirectionAuthored;
    public bool spatialMetadataAuthored;
    public float allowedSlopeDegrees;
    public string foundationProfile;
    public string roadRelationship;
    public string navigationProfile;
    public bool hasRenderer;
    public int rendererCount;
    public int materialSlotCount;
    public int invalidMaterialSlotCount;
    public int materialReviewSlotCount;
    public bool hasCollider;
    public int colliderCount;
    public int lodGroupCount;
    public int missingScriptCount;
    public int estimatedRendererCost;
    public List<string> entranceSocketCandidates = new List<string>();
    public List<string> connectionSocketCandidates = new List<string>();
    public List<string> dressingSocketCandidates = new List<string>();
    public List<string> semanticTags = new List<string>();
    public List<string> validationIssues = new List<string>();

    public void EnsureCollections()
    {
        entranceSocketCandidates ??= new List<string>();
        connectionSocketCandidates ??= new List<string>();
        dressingSocketCandidates ??= new List<string>();
        semanticTags ??= new List<string>();
        validationIssues ??= new List<string>();
    }
}

[Serializable]
public sealed class YQMaterialAssetRecord
{
    public string stableAssetId;
    public string sourceGuid;
    public string assetPath;
    public string kitId;
    public string shaderName;
    public YQMaterialCompatibilityState compatibilityState;
    public string runtimeMaterialPath;
    public string compatibilityStrategy;
    public bool releaseEligible;
    public List<string> validationIssues = new List<string>();

    public void EnsureCollections()
    {
        validationIssues ??= new List<string>();
    }
}

[CreateAssetMenu(
    fileName = "YQWorldAssetIntakeCatalog",
    menuName = "YourQuest/AAA World Asset Intake Catalog")]
public sealed class YQWorldAssetIntakeCatalog : ScriptableObject
{
    public const string CurrentSchemaVersion =
        "world_asset_intake_v3";

    [SerializeField]
    private string schemaVersion =
        CurrentSchemaVersion;

    [SerializeField]
    private string scanScope =
        string.Empty;

    [SerializeField]
    private string generatedUtc =
        string.Empty;

    [SerializeField]
    private List<YQAssetKitManifest> kits =
        new List<YQAssetKitManifest>();

    [SerializeField]
    private List<YQSpatialAssetRecord> spatialAssets =
        new List<YQSpatialAssetRecord>();

    [SerializeField]
    private List<YQMaterialAssetRecord> materials =
        new List<YQMaterialAssetRecord>();

    public string SchemaVersion => schemaVersion;
    public string ScanScope => scanScope;
    public string GeneratedUtc => generatedUtc;
    public IReadOnlyList<YQAssetKitManifest> Kits => kits;
    public IReadOnlyList<YQSpatialAssetRecord> SpatialAssets => spatialAssets;
    public IReadOnlyList<YQMaterialAssetRecord> Materials => materials;

    public void SetRecords(
        string newScanScope,
        string newGeneratedUtc,
        List<YQAssetKitManifest> newKits,
        List<YQSpatialAssetRecord> newSpatialAssets,
        List<YQMaterialAssetRecord> newMaterials)
    {
        // note: One editor transaction replaces the complete intake snapshot so stale asset eligibility cannot survive a rescan.
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

        for (int i = 0; i < kits.Count; i++)
            kits[i]?.EnsureCollections();

        for (int i = 0; i < spatialAssets.Count; i++)
            spatialAssets[i]?.EnsureCollections();

        for (int i = 0; i < materials.Count; i++)
            materials[i]?.EnsureCollections();
    }

    public void RecalculateKitSpatialCounts()
    {
        EnsureCollections();

        Dictionary<string, YQAssetKitManifest> kitsById =
            new Dictionary<string, YQAssetKitManifest>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < kits.Count; i++)
        {
            YQAssetKitManifest kit =
                kits[i];

            if (kit == null ||
                string.IsNullOrWhiteSpace(kit.kitId))
            {
                continue;
            }

            kit.prefabCount = 0;
            kit.candidatePrefabCount = 0;
            kit.repairRequiredPrefabCount = 0;
            kit.spatialReviewPrefabCount = 0;

            kitsById[kit.kitId] =
                kit;
        }

        for (int i = 0; i < spatialAssets.Count; i++)
        {
            YQSpatialAssetRecord record =
                spatialAssets[i];

            if (record == null ||
                string.IsNullOrWhiteSpace(record.kitId) ||
                !kitsById.TryGetValue(
                    record.kitId,
                    out YQAssetKitManifest kit))
            {
                continue;
            }

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
