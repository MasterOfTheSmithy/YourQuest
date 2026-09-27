using System;
using System.Collections.Generic;
using UnityEngine;

public enum YQBetaAssetCapabilityKind
{
    PrimaryWorldFamily = 0,
    Transition = 1,
    Actor = 2,
    Equipment = 3,
    Water = 4,
    Effect = 5
}

[Serializable]
public sealed class YQBetaAssetCompatibilityMetadata
{
    public Vector3 recommendedScale = Vector3.one;
    public Vector3 footprintSize = Vector3.zero;
    public string groundDatum = string.Empty;
    public string pivotDatum = string.Empty;
    public int colliderCount;
    public string entranceProfile = string.Empty;
    public string interiorProfile = string.Empty;
    public string navigationProfile = string.Empty;
    public string cameraClearanceProfile = string.Empty;
    public string shaderMaterialProfile = string.Empty;
    public List<string> semanticCompatibility = new List<string>();

    public void EnsureCollections()
    {
        // note: Keep semantic compatibility metadata load-safe when older serialized manifests omit the new list.
        semanticCompatibility ??= new List<string>();
    }
}

[Serializable]
public sealed class YQBetaAssetCapabilityRecord
{
    public string capabilityId = string.Empty;
    public string semanticFamilyId = string.Empty;
    public string sourceCatalogId = string.Empty;
    public YQBetaAssetCapabilityKind kind;
    public bool releaseEligible;
    public bool runtimeVerified;
    public bool usesRuntimeAlternative;
    public YQBetaAssetCompatibilityMetadata compatibility =
        new YQBetaAssetCompatibilityMetadata();
    public List<string> stableAssetKeys = new List<string>();
    public List<string> assetPaths = new List<string>();
    public List<string> supportedTags = new List<string>();
    public List<string> notes = new List<string>();

    public void EnsureCollections()
    {
        // note: Normalize compatibility metadata before runtime consumers inspect a persisted capability.
        compatibility ??= new YQBetaAssetCompatibilityMetadata();
        compatibility.EnsureCollections();
        stableAssetKeys ??= new List<string>();
        assetPaths ??= new List<string>();
        supportedTags ??= new List<string>();
        notes ??= new List<string>();
    }
}

[Serializable]
public sealed class YQCharacterAppearanceCapabilityRecord
{
    public string capabilityId = string.Empty;
    public string actorFamilyId = string.Empty;
    public string rigId = string.Empty;
    public string headFamilyId = string.Empty;
    public string materialFamilyId = string.Empty;
    public bool releaseEligible;
    public bool runtimeVerified;
    public List<string> supportedMorphs = new List<string>();
    public List<string> notes = new List<string>();

    public void EnsureCollections()
    {
        supportedMorphs ??= new List<string>();
        notes ??= new List<string>();
    }
}

[Serializable]
public sealed class YQBetaMechanicCapabilityRecord
{
    public string capabilityId = string.Empty;
    public string mechanicKey = string.Empty;
    public string animationIntent = string.Empty;
    public string effectFamily = string.Empty;
    public string audioFamily = string.Empty;
    public bool implementationAvailable;
    public List<string> notes = new List<string>();

    public void EnsureCollections()
    {
        notes ??= new List<string>();
    }
}

[Serializable]
public sealed class YQBetaAssetMigrationFixture
{
    public string fromVersion = string.Empty;
    public string toVersion = string.Empty;
    public string stableBindingKey = string.Empty;
    public string expectedResolution = string.Empty;
    public bool replayOnly = true;
}

[CreateAssetMenu(
    fileName = "YQBetaAssetBindingManifest",
    menuName = "YourQuest/World/Beta Asset Binding Manifest")]
public sealed class YQBetaAssetBindingManifest : ScriptableObject
{
    public const string CurrentSchemaVersion = "beta-asset-binding-1.0.0";
    public const string CurrentBindingVersion = "beta-binding-1.0.0";
    public const string ResourcesName = "YQBetaAssetBindingManifest";

    [SerializeField] private string schemaVersion = CurrentSchemaVersion;
    [SerializeField] private string bindingVersion = CurrentBindingVersion;
    [SerializeField] private string primaryFamilyKey = string.Empty;
    [SerializeField] private string sourceIdentity = string.Empty;
    [SerializeField] private string generatedUtc = string.Empty;
    [SerializeField] private List<YQBetaAssetCapabilityRecord> capabilities =
        new List<YQBetaAssetCapabilityRecord>();
    [SerializeField] private List<YQCharacterAppearanceCapabilityRecord> appearanceCapabilities =
        new List<YQCharacterAppearanceCapabilityRecord>();
    [SerializeField] private List<YQBetaMechanicCapabilityRecord> mechanicCapabilities =
        new List<YQBetaMechanicCapabilityRecord>();
    [SerializeField] private List<YQBetaAssetMigrationFixture> migrationFixtures =
        new List<YQBetaAssetMigrationFixture>();

    private static YQBetaAssetBindingManifest _instance;

    public string SchemaVersion => schemaVersion;
    public string BindingVersion => bindingVersion;
    public string PrimaryFamilyKey => primaryFamilyKey;
    public string SourceIdentity => sourceIdentity;
    public string GeneratedUtc => generatedUtc;
    public IReadOnlyList<YQBetaAssetCapabilityRecord> Capabilities => capabilities;
    public IReadOnlyList<YQCharacterAppearanceCapabilityRecord> AppearanceCapabilities => appearanceCapabilities;
    public IReadOnlyList<YQBetaMechanicCapabilityRecord> MechanicCapabilities => mechanicCapabilities;
    public IReadOnlyList<YQBetaAssetMigrationFixture> MigrationFixtures => migrationFixtures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeStatics()
    {
        _instance = null;
    }

    public static YQBetaAssetBindingManifest Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<YQBetaAssetBindingManifest>(ResourcesName);
            _instance?.EnsureCollections();
            return _instance;
        }
    }

    public void Configure(
        string newPrimaryFamilyKey,
        string newSourceIdentity,
        string newGeneratedUtc,
        IEnumerable<YQBetaAssetCapabilityRecord> newCapabilities,
        IEnumerable<YQCharacterAppearanceCapabilityRecord> newAppearanceCapabilities,
        IEnumerable<YQBetaMechanicCapabilityRecord> newMechanicCapabilities,
        IEnumerable<YQBetaAssetMigrationFixture> newMigrationFixtures)
    {
        // note: One editor transaction publishes the complete reviewed binding view so runtime cannot observe mixed catalog generations.
        schemaVersion = CurrentSchemaVersion;
        bindingVersion = CurrentBindingVersion;
        primaryFamilyKey = newPrimaryFamilyKey ?? string.Empty;
        sourceIdentity = newSourceIdentity ?? string.Empty;
        generatedUtc = newGeneratedUtc ?? string.Empty;
        capabilities = newCapabilities != null
            ? new List<YQBetaAssetCapabilityRecord>(newCapabilities)
            : new List<YQBetaAssetCapabilityRecord>();
        appearanceCapabilities = newAppearanceCapabilities != null
            ? new List<YQCharacterAppearanceCapabilityRecord>(newAppearanceCapabilities)
            : new List<YQCharacterAppearanceCapabilityRecord>();
        mechanicCapabilities = newMechanicCapabilities != null
            ? new List<YQBetaMechanicCapabilityRecord>(newMechanicCapabilities)
            : new List<YQBetaMechanicCapabilityRecord>();
        migrationFixtures = newMigrationFixtures != null
            ? new List<YQBetaAssetMigrationFixture>(newMigrationFixtures)
            : new List<YQBetaAssetMigrationFixture>();
        EnsureCollections();
    }

    public bool TryGetCapability(string capabilityId, out YQBetaAssetCapabilityRecord result)
    {
        // note: Resolve a reviewed asset capability by stable semantic ID without exposing list order to consumers.
        EnsureCollections();
        for (int index = 0; index < capabilities.Count; index++)
        {
            YQBetaAssetCapabilityRecord candidate = capabilities[index];
            if (candidate != null && string.Equals(candidate.capabilityId, capabilityId, StringComparison.OrdinalIgnoreCase))
            {
                result = candidate;
                return true;
            }
        }

        result = null;
        return false;
    }

    public bool TryGetAppearanceCapability(
        string capabilityId,
        out YQCharacterAppearanceCapabilityRecord result)
    {
        // note: Resolve character presentation capabilities through the same persisted manifest contract.
        EnsureCollections();
        for (int index = 0; index < appearanceCapabilities.Count; index++)
        {
            YQCharacterAppearanceCapabilityRecord candidate = appearanceCapabilities[index];
            if (candidate != null && string.Equals(candidate.capabilityId, capabilityId, StringComparison.OrdinalIgnoreCase))
            {
                result = candidate;
                return true;
            }
        }

        result = null;
        return false;
    }

    public bool TryGetMechanicCapability(
        string capabilityId,
        out YQBetaMechanicCapabilityRecord result)
    {
        // note: Resolve supported mechanic and effect families without allowing generated text to select behavior.
        EnsureCollections();
        for (int index = 0; index < mechanicCapabilities.Count; index++)
        {
            YQBetaMechanicCapabilityRecord candidate = mechanicCapabilities[index];
            if (candidate != null && string.Equals(candidate.capabilityId, capabilityId, StringComparison.OrdinalIgnoreCase))
            {
                result = candidate;
                return true;
            }
        }

        result = null;
        return false;
    }

    public void EnsureCollections()
    {
        capabilities ??= new List<YQBetaAssetCapabilityRecord>();
        appearanceCapabilities ??= new List<YQCharacterAppearanceCapabilityRecord>();
        mechanicCapabilities ??= new List<YQBetaMechanicCapabilityRecord>();
        migrationFixtures ??= new List<YQBetaAssetMigrationFixture>();
        for (int index = 0; index < capabilities.Count; index++) capabilities[index]?.EnsureCollections();
        for (int index = 0; index < appearanceCapabilities.Count; index++) appearanceCapabilities[index]?.EnsureCollections();
        for (int index = 0; index < mechanicCapabilities.Count; index++) mechanicCapabilities[index]?.EnsureCollections();
    }

    public static bool SupportsSchema(string candidate)
    {
        return string.Equals(candidate, CurrentSchemaVersion, StringComparison.Ordinal) ||
               string.Equals(candidate, "beta-asset-binding-0.9.0", StringComparison.Ordinal);
    }
}
