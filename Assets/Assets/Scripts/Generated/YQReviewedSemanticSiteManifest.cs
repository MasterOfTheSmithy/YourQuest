using System;
using System.Collections.Generic;
using UnityEngine;

public enum YQSemanticSiteReviewState
{
    Pending = 0,
    Approved = 1,
    DeferredNeedsRepair = 2
}

[Serializable]
public sealed class YQReviewedSemanticZoneRecord
{
    public string stableId = string.Empty;
    public string displayName = string.Empty;
    public YQDistrictFunction districtFunction = YQDistrictFunction.Unknown;
    public GameObject prefab;
    public Vector3 authoredSourceOrigin;
    public Vector3 localBoundsCenter;
    public Vector3 localBoundsSize;
    public int sourceInstanceCount;
    public int authoredBuildingCount;
    public int authoredDressingCount;
    public List<string> semanticTags = new List<string>();
    public List<string> connectionSocketPaths = new List<string>();
    public List<string> streamingCellIds = new List<string>();
}

public sealed class YQReviewedSemanticSiteManifest : ScriptableObject
{
    [SerializeField]
    private string schemaVersion = "reviewed-semantic-site-1.0.0";

    [SerializeField]
    private string kitId = string.Empty;

    [SerializeField]
    private string semanticStyleKey = string.Empty;

    [SerializeField]
    private string sourceSignature = string.Empty;

    [SerializeField]
    private YQSemanticExtractionTopology topology =
        YQSemanticExtractionTopology.Unknown;

    [SerializeField]
    private YQAuthoredSiteStreamingManifest streamingSite;

    [SerializeField]
    private int sourceInstanceCount;

    [SerializeField]
    private bool releaseEligible;

    [SerializeField]
    private YQSemanticSiteReviewState reviewState =
        YQSemanticSiteReviewState.Pending;

    [SerializeField]
    private string reviewNote = string.Empty;

    [SerializeField]
    private List<YQReviewedSemanticZoneRecord> zones =
        new List<YQReviewedSemanticZoneRecord>();

    public string SchemaVersion => schemaVersion;
    public string KitId => kitId;
    public string SemanticStyleKey => semanticStyleKey;
    public string SourceSignature => sourceSignature;
    public YQSemanticExtractionTopology Topology => topology;
    public YQAuthoredSiteStreamingManifest StreamingSite => streamingSite;
    public int SourceInstanceCount => sourceInstanceCount;
    public bool ReleaseEligible => releaseEligible;
    public YQSemanticSiteReviewState ReviewState => releaseEligible
        ? YQSemanticSiteReviewState.Approved
        : reviewState;
    public string ReviewNote => reviewNote;
    public IReadOnlyList<YQReviewedSemanticZoneRecord> Zones => zones;

    public void Configure(
        string newKitId,
        string newSemanticStyleKey,
        int newSourceInstanceCount,
        IEnumerable<YQReviewedSemanticZoneRecord> newZones,
        bool newReleaseEligible)
    {
        // note: The persisted manifest is runtime authority after review; the LLM may select it semantically but cannot rewrite prefab paths or spatial contracts.
        kitId = newKitId ?? string.Empty;
        semanticStyleKey = newSemanticStyleKey ?? string.Empty;
        sourceSignature = string.Empty;
        topology = YQSemanticExtractionTopology.Unknown;
        streamingSite = null;
        sourceInstanceCount = Mathf.Max(0, newSourceInstanceCount);
        zones = newZones != null
            ? new List<YQReviewedSemanticZoneRecord>(newZones)
            : new List<YQReviewedSemanticZoneRecord>();
        releaseEligible = newReleaseEligible;
        reviewState = newReleaseEligible
            ? YQSemanticSiteReviewState.Approved
            : YQSemanticSiteReviewState.Pending;
        reviewNote = string.Empty;
    }

    public void ConfigureCandidate(
        string newKitId,
        string newSemanticStyleKey,
        string newSourceSignature,
        YQSemanticExtractionTopology newTopology,
        YQAuthoredSiteStreamingManifest newStreamingSite,
        int newSourceInstanceCount,
        IEnumerable<YQReviewedSemanticZoneRecord> newZones)
    {
        // note: Semantic candidates preserve approved streaming geometry and remain unavailable to runtime until a human reviews their topology and tags.
        kitId = newKitId ?? string.Empty;
        semanticStyleKey = newSemanticStyleKey ?? string.Empty;
        sourceSignature = newSourceSignature ?? string.Empty;
        topology = newTopology;
        streamingSite = newStreamingSite;
        sourceInstanceCount = Mathf.Max(0, newSourceInstanceCount);
        zones = newZones != null
            ? new List<YQReviewedSemanticZoneRecord>(newZones)
            : new List<YQReviewedSemanticZoneRecord>();
        reviewState = YQSemanticSiteReviewState.Pending;
        reviewNote = string.Empty;
        releaseEligible = false;
    }

    public void MarkReleaseEligible()
    {
        // note: Runtime eligibility is an explicit promotion step after geometry, coverage, source identity, and semantic outputs have all been reviewed.
        reviewState = YQSemanticSiteReviewState.Approved;
        reviewNote = string.Empty;
        releaseEligible = true;
    }

    public void DeferForRepair(string note)
    {
        // note: Deferred semantic candidates remain recoverable generated evidence but cannot be selected or spawned at runtime.
        reviewState = YQSemanticSiteReviewState.DeferredNeedsRepair;
        reviewNote = note ?? string.Empty;
        releaseEligible = false;
    }
}
