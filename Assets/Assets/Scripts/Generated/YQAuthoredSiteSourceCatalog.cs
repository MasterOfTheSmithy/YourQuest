using System;
using System.Collections.Generic;
using UnityEngine;

public enum YQAuthoredSiteSourceState
{
    DetectedPendingBuild = 0,
    CandidateBuilt = 1,
    Approved = 2,
    SourceChanged = 3,
    NeedsAuthoredScene = 4,
    SourceMissing = 5,
    BuildFailed = 6
}

[Serializable]
public sealed class YQAuthoredSiteSourceRecord
{
    public string kitId = string.Empty;
    public string displayName = string.Empty;
    public string assetRoot = string.Empty;
    public string selectedScenePath = string.Empty;
    public List<string> discoveredScenePaths = new List<string>();
    public YQAuthoredSiteKind siteKind = YQAuthoredSiteKind.Unknown;
    public bool forceUrpConversion;
    public string sourceSignature = string.Empty;
    public YQAuthoredSiteSourceState state =
        YQAuthoredSiteSourceState.DetectedPendingBuild;
    public string generatedPrefabPath = string.Empty;
    public string reviewScenePath = string.Empty;
    public string lastFailure = string.Empty;
}

public sealed class YQAuthoredSiteSourceCatalog : ScriptableObject
{
    [SerializeField]
    private List<YQAuthoredSiteSourceRecord> records =
        new List<YQAuthoredSiteSourceRecord>();

    public IReadOnlyList<YQAuthoredSiteSourceRecord> Records => records;

    public void ApplyDetection(
        IEnumerable<YQAuthoredSiteSourceRecord> detectedRecords)
    {
        // note: Discovery updates source facts while preserving review state until the selected authored source actually changes.
        Dictionary<string, YQAuthoredSiteSourceRecord> existingByRoot =
            new Dictionary<string, YQAuthoredSiteSourceRecord>(
                StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < records.Count; index++)
        {
            if (records[index] != null &&
                !string.IsNullOrWhiteSpace(records[index].assetRoot))
            {
                existingByRoot[records[index].assetRoot] = records[index];
            }
        }

        HashSet<string> detectedRoots =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (YQAuthoredSiteSourceRecord detected in
                 detectedRecords ?? Array.Empty<YQAuthoredSiteSourceRecord>())
        {
            if (detected == null || string.IsNullOrWhiteSpace(detected.assetRoot))
                continue;

            detectedRoots.Add(detected.assetRoot);

            if (!existingByRoot.TryGetValue(
                    detected.assetRoot,
                    out YQAuthoredSiteSourceRecord existing))
            {
                records.Add(detected);
                existingByRoot[detected.assetRoot] = detected;
                continue;
            }

            bool sourceChanged =
                !string.Equals(
                    existing.sourceSignature,
                    detected.sourceSignature,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    existing.selectedScenePath,
                    detected.selectedScenePath,
                    StringComparison.OrdinalIgnoreCase);

            existing.kitId = detected.kitId;
            existing.displayName = detected.displayName;
            existing.selectedScenePath = detected.selectedScenePath;
            existing.discoveredScenePaths =
                new List<string>(detected.discoveredScenePaths);
            existing.siteKind = detected.siteKind;
            existing.forceUrpConversion = detected.forceUrpConversion;
            existing.sourceSignature = detected.sourceSignature;

            if (string.IsNullOrWhiteSpace(existing.selectedScenePath))
            {
                existing.state = YQAuthoredSiteSourceState.NeedsAuthoredScene;
            }
            else if (sourceChanged &&
                     existing.state != YQAuthoredSiteSourceState.DetectedPendingBuild)
            {
                existing.state = YQAuthoredSiteSourceState.SourceChanged;
                existing.lastFailure = string.Empty;
            }
            else if (existing.state == YQAuthoredSiteSourceState.SourceMissing ||
                     existing.state == YQAuthoredSiteSourceState.NeedsAuthoredScene)
            {
                existing.state = YQAuthoredSiteSourceState.DetectedPendingBuild;
            }
        }

        for (int index = 0; index < records.Count; index++)
        {
            YQAuthoredSiteSourceRecord record = records[index];

            if (record != null && !detectedRoots.Contains(record.assetRoot))
            {
                record.state = YQAuthoredSiteSourceState.SourceMissing;
            }
        }
    }

    public void MarkCandidateBuilt(
        string sourceScenePath,
        string generatedPrefabPath,
        string reviewScenePath)
    {
        YQAuthoredSiteSourceRecord record = FindByScene(sourceScenePath);

        if (record == null)
            return;

        // note: Batch output remains a candidate until a person reviews the isolated scene and explicitly approves it.
        record.generatedPrefabPath = generatedPrefabPath ?? string.Empty;
        record.reviewScenePath = reviewScenePath ?? string.Empty;
        record.lastFailure = string.Empty;
        record.state = YQAuthoredSiteSourceState.CandidateBuilt;
    }

    public void MarkBuildFailed(string sourceScenePath, string failure)
    {
        YQAuthoredSiteSourceRecord record = FindByScene(sourceScenePath);

        if (record == null)
            return;

        record.lastFailure = failure ?? string.Empty;
        record.state = YQAuthoredSiteSourceState.BuildFailed;
    }

    private YQAuthoredSiteSourceRecord FindByScene(string sourceScenePath)
    {
        for (int index = 0; index < records.Count; index++)
        {
            if (records[index] != null &&
                string.Equals(
                    records[index].selectedScenePath,
                    sourceScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return records[index];
            }
        }

        return null;
    }
}
