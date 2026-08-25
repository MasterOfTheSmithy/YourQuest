using System;
using System.Collections.Generic;
using UnityEngine;

public enum YQWorldPackProductionState
{
    MissingSourceCandidate = 0,
    NeedsStreamingCompilation = 1,
    NeedsStreamingReview = 2,
    NeedsSemanticSegmentation = 3,
    RuntimeReady = 4,
    SourceChanged = 5,
    Blocked = 6,
    NeedsSemanticReview = 7
}

[Serializable]
public sealed class YQWorldPackProductionRecord
{
    public string kitId = string.Empty;
    public string displayName = string.Empty;
    public YQAuthoredSiteKind siteKind = YQAuthoredSiteKind.Unknown;
    public YQSemanticExtractionTopology topology =
        YQSemanticExtractionTopology.Unknown;
    public YQWorldPackProductionState state =
        YQWorldPackProductionState.MissingSourceCandidate;
    public string sourceSignature = string.Empty;
    public string sourcePrefabPath = string.Empty;
    public string streamingManifestPath = string.Empty;
    public string streamingReviewScenePath = string.Empty;
    public string semanticManifestPath = string.Empty;
    public string semanticReviewScenePath = string.Empty;
    public string nextAction = string.Empty;
}

public sealed class YQWorldPackProductionCatalog : ScriptableObject
{
    [SerializeField]
    private string schemaVersion = "world-pack-production-1.0.0";

    [SerializeField]
    private List<YQWorldPackProductionRecord> records =
        new List<YQWorldPackProductionRecord>();

    public string SchemaVersion => schemaVersion;
    public IReadOnlyList<YQWorldPackProductionRecord> Records => records;

    public void Configure(
        IEnumerable<YQWorldPackProductionRecord> newRecords)
    {
        // note: This catalog records editor-production readiness only; runtime selection reads approved manifests and never an unreviewed queue entry.
        records = newRecords != null
            ? new List<YQWorldPackProductionRecord>(newRecords)
            : new List<YQWorldPackProductionRecord>();
    }
}
