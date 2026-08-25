using System;
using System.Collections.Generic;
using UnityEngine;

public enum YQWorldAssemblyKind
{
    Unknown = 0,
    Building = 1,
    Parcel = 2,
    Street = 3,
    Landmark = 4,
    Interior = 5,
    Edge = 6,
    OpenSpace = 7,
    District = 8,
    Site = 9,
    StreamingCell = 10
}

public enum YQWorldAssemblyReviewState
{
    ExtractedCandidate = 0,
    NeedsRepair = 1,
    ApprovedGolden = 2,
    Rejected = 3
}

[DisallowMultipleComponent]
public sealed class YQWorldAssemblyDescriptor : MonoBehaviour
{
    [SerializeField]
    private string stableAssemblyId = string.Empty;

    [SerializeField]
    private string kitId = string.Empty;

    [SerializeField]
    private YQWorldAssemblyKind assemblyKind =
        YQWorldAssemblyKind.Unknown;

    [SerializeField]
    private YQWorldAssemblyReviewState reviewState =
        YQWorldAssemblyReviewState.ExtractedCandidate;

    [SerializeField]
    private string sourceFamily = string.Empty;

    [SerializeField]
    private string sourceCompositionSignature = string.Empty;

    [SerializeField]
    private int authoredOccurrenceCount = 1;

    [SerializeField]
    private Vector3 localBoundsCenter;

    [SerializeField]
    private Vector3 localBoundsSize;

    [SerializeField]
    private Vector3 clearanceSize;

    [SerializeField]
    private Vector3 frontDirection = Vector3.forward;

    [SerializeField]
    private string entranceSocketPath = string.Empty;

    [SerializeField]
    private List<string> connectionSocketPaths =
        new List<string>();

    [SerializeField]
    private bool releaseEligible;

    [SerializeField]
    private List<string> semanticTags =
        new List<string>();

    public string StableAssemblyId => stableAssemblyId;
    public string KitId => kitId;
    public YQWorldAssemblyKind AssemblyKind => assemblyKind;
    public YQWorldAssemblyReviewState ReviewState => reviewState;
    public string SourceFamily => sourceFamily;
    public string SourceCompositionSignature => sourceCompositionSignature;
    public int AuthoredOccurrenceCount => authoredOccurrenceCount;
    public Vector3 LocalBoundsCenter => localBoundsCenter;
    public Vector3 LocalBoundsSize => localBoundsSize;
    public Vector3 ClearanceSize => clearanceSize;
    public Vector3 FrontDirection => frontDirection;
    public string EntranceSocketPath => entranceSocketPath;
    public IReadOnlyList<string> ConnectionSocketPaths => connectionSocketPaths;
    public bool ReleaseEligible => releaseEligible;
    public IReadOnlyList<string> SemanticTags => semanticTags;

    public void ConfigureExtractedCandidate(
        string newStableAssemblyId,
        string newKitId,
        YQWorldAssemblyKind newAssemblyKind,
        string newSourceFamily,
        string newSourceCompositionSignature,
        int newAuthoredOccurrenceCount,
        Vector3 newLocalBoundsCenter,
        Vector3 newLocalBoundsSize,
        Vector3 newClearanceSize,
        Vector3 newFrontDirection,
        string newEntranceSocketPath,
        IEnumerable<string> newSemanticTags)
    {
        // note: Extracted assemblies remain non-release candidates until a designer approves their visual composition and spatial contract.
        stableAssemblyId = newStableAssemblyId ?? string.Empty;
        kitId = newKitId ?? string.Empty;
        assemblyKind = newAssemblyKind;
        reviewState = YQWorldAssemblyReviewState.ExtractedCandidate;
        sourceFamily = newSourceFamily ?? string.Empty;
        sourceCompositionSignature = newSourceCompositionSignature ?? string.Empty;
        authoredOccurrenceCount = Mathf.Max(1, newAuthoredOccurrenceCount);
        localBoundsCenter = newLocalBoundsCenter;
        localBoundsSize = newLocalBoundsSize;
        clearanceSize = newClearanceSize;
        frontDirection = newFrontDirection.sqrMagnitude > 0.0001f
            ? newFrontDirection.normalized
            : Vector3.forward;
        entranceSocketPath = newEntranceSocketPath ?? string.Empty;
        releaseEligible = false;
        semanticTags = newSemanticTags != null
            ? new List<string>(newSemanticTags)
            : new List<string>();
    }

    public void ConfigureConnectionSockets(
        IEnumerable<string> newConnectionSocketPaths)
    {
        // note: Connection sockets are curated assembly contracts used by deterministic layout solvers; the LLM never supplies scene paths directly.
        connectionSocketPaths = newConnectionSocketPaths != null
            ? new List<string>(newConnectionSocketPaths)
            : new List<string>();
    }

    public void MarkApprovedGolden()
    {
        // note: Approval is an explicit editor-authoring decision made only after visual review; runtime generation cannot promote unreviewed geometry.
        reviewState = YQWorldAssemblyReviewState.ApprovedGolden;
        releaseEligible = true;
    }
}
