using System.Collections.Generic;
using UnityEngine;

public enum YQAuthoredSiteKind
{
    Unknown = 0,
    Settlement = 1,
    Dungeon = 2,
    Interior = 3,
    Landmark = 4,
    Camp = 5,
    Wilderness = 6,
    SciFiSite = 7
}

[DisallowMultipleComponent]
public sealed class YQWorldAuthoredSiteDescriptor : MonoBehaviour
{
    [SerializeField]
    private string semanticStyleKey = string.Empty;

    [SerializeField]
    private YQAuthoredSiteKind siteKind = YQAuthoredSiteKind.Unknown;

    [SerializeField]
    private string sourceScenePath = string.Empty;

    [SerializeField]
    private Vector3 authoredSourceOrigin;

    [SerializeField]
    private int sourceInstanceCount;

    [SerializeField]
    private int repairedMaterialSlotCount;

    [SerializeField]
    private int unresolvedMaterialSlotCount;

    [SerializeField]
    private Vector3 localBoundsCenter;

    [SerializeField]
    private Vector3 localBoundsSize;

    [SerializeField]
    private List<string> connectionSocketPaths = new List<string>();

    public string SemanticStyleKey => semanticStyleKey;
    public YQAuthoredSiteKind SiteKind => siteKind;
    public string SourceScenePath => sourceScenePath;
    public Vector3 AuthoredSourceOrigin => authoredSourceOrigin;
    public int SourceInstanceCount => sourceInstanceCount;
    public int RepairedMaterialSlotCount => repairedMaterialSlotCount;
    public int UnresolvedMaterialSlotCount => unresolvedMaterialSlotCount;
    public Vector3 LocalBoundsCenter => localBoundsCenter;
    public Vector3 LocalBoundsSize => localBoundsSize;
    public IReadOnlyList<string> ConnectionSocketPaths => connectionSocketPaths;

    public void Configure(
        string newSemanticStyleKey,
        YQAuthoredSiteKind newSiteKind,
        string newSourceScenePath,
        Vector3 newAuthoredSourceOrigin,
        int newSourceInstanceCount,
        int newRepairedMaterialSlotCount,
        int newUnresolvedMaterialSlotCount,
        Vector3 newLocalBoundsCenter,
        Vector3 newLocalBoundsSize,
        IEnumerable<string> newConnectionSocketPaths)
    {
        // note: This contract binds semantic world intent to reviewed authored geometry without exposing vendor asset paths to the LLM.
        semanticStyleKey = newSemanticStyleKey ?? string.Empty;
        siteKind = newSiteKind;
        sourceScenePath = newSourceScenePath ?? string.Empty;
        authoredSourceOrigin = newAuthoredSourceOrigin;
        sourceInstanceCount = Mathf.Max(0, newSourceInstanceCount);
        repairedMaterialSlotCount = Mathf.Max(0, newRepairedMaterialSlotCount);
        unresolvedMaterialSlotCount = Mathf.Max(0, newUnresolvedMaterialSlotCount);
        localBoundsCenter = newLocalBoundsCenter;
        localBoundsSize = newLocalBoundsSize;
        connectionSocketPaths = newConnectionSocketPaths != null
            ? new List<string>(newConnectionSocketPaths)
            : new List<string>();
    }

    private void OnDrawGizmosSelected()
    {
        // note: Gold site bounds distinguish complete authored locations from cyan district cells during review.
        Gizmos.color = new Color(1f, 0.72f, 0.12f, 0.75f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(localBoundsCenter, localBoundsSize);
        Gizmos.matrix = previousMatrix;
    }
}
