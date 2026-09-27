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

    // note: The V2 style contract is additive; version zero leaves every existing catalog record on the legacy descriptive-tag path.
    public YQKitStyleContractV2 styleV2 =
        new YQKitStyleContractV2();

    public void EnsureCollections()
    {
        genreTags ??= new List<string>();
        environmentTags ??= new List<string>();
        compatiblePrimaryKitIds ??= new List<string>();
        compatibleAccentKitIds ??= new List<string>();
        forbiddenKitIds ??= new List<string>();
        validationIssues ??= new List<string>();
        styleV2 ??= new YQKitStyleContractV2();
        styleV2.EnsureCollections();
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

    // note: Reviewed V2 spatial intelligence lives beside legacy inference so raw discovery remains useful evidence without becoming placement authority.
    public YQAssetCurationContractV2 curationV2 =
        new YQAssetCurationContractV2();

    public void EnsureCollections()
    {
        entranceSocketCandidates ??= new List<string>();
        connectionSocketCandidates ??= new List<string>();
        dressingSocketCandidates ??= new List<string>();
        semanticTags ??= new List<string>();
        validationIssues ??= new List<string>();
        curationV2 ??= new YQAssetCurationContractV2();
        curationV2.EnsureCollections();
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
