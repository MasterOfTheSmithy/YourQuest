using System;
using System.Collections.Generic;
using UnityEngine;

public enum YQSemanticExtractionTopology
{
    Unknown = 0,
    SettlementDistricts = 1,
    DungeonRooms = 2,
    InteriorRooms = 3,
    LandmarkCampus = 4,
    CampZones = 5,
    WildernessRegions = 6,
    SciFiSectors = 7
}

[Serializable]
public sealed class YQSemanticExtractionProfile
{
    public string kitId = string.Empty;
    public string semanticStyleKey = string.Empty;
    public string displayName = string.Empty;
    public YQAuthoredSiteKind siteKind = YQAuthoredSiteKind.Unknown;
    public YQSemanticExtractionTopology topology =
        YQSemanticExtractionTopology.Unknown;
    public int minimumAssemblies = 1;
    public int maximumAssemblies = 1;
    public float targetHorizontalSpan = 64f;
    public float verticalLayerHeight = 8f;
    public float cohesiveLinkDistance = 12f;
    public bool authoredOverride;
    public bool requiresManualProfileReview = true;
    public YQWorldStructureUsagePolicy structureUsagePolicy =
        YQWorldStructureUsagePolicy.Unspecified;
    public int maximumEnterableStructures;
    public List<string> requiredSemanticOutputs = new List<string>();
}

public sealed class YQSemanticExtractionProfileCatalog : ScriptableObject
{
    [SerializeField]
    private string schemaVersion = "semantic-extraction-profile-1.0.0";

    [SerializeField]
    private List<YQSemanticExtractionProfile> profiles =
        new List<YQSemanticExtractionProfile>();

    public string SchemaVersion => schemaVersion;
    public IReadOnlyList<YQSemanticExtractionProfile> Profiles => profiles;

    public void Configure(IEnumerable<YQSemanticExtractionProfile> newProfiles)
    {
        // note: Profiles define deterministic curation constraints; generated narrative intent may select a profile but cannot alter its spatial safety contract.
        profiles = newProfiles != null
            ? new List<YQSemanticExtractionProfile>(newProfiles)
            : new List<YQSemanticExtractionProfile>();
    }

    public YQSemanticExtractionProfile Find(string kitId)
    {
        for (int index = 0; index < profiles.Count; index++)
        {
            YQSemanticExtractionProfile profile = profiles[index];

            if (profile != null && string.Equals(
                    profile.kitId,
                    kitId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }
        }

        return null;
    }
}
