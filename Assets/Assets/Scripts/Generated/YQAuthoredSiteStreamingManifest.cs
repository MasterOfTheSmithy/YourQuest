using System;
using System.Collections.Generic;
using UnityEngine;

public enum YQWorldSitePresentationMode
{
    Unknown = 0,
    SeamlessExterior = 1,
    InteriorTransitionOnly = 2,
    SubterraneanTransitionOnly = 3
}

public enum YQWorldStructureUsagePolicy
{
    Unspecified = 0,
    FullyEnterable = 1,
    ExteriorShellsOnly = 2,
    SingleFurnishedPrimaryWithExteriorShells = 3
}

public enum YQStreamingSiteReviewState
{
    Pending = 0,
    Approved = 1,
    DeferredNeedsRepair = 2
}

[Serializable]
public sealed class YQAuthoredSiteStreamingCellRecord
{
    [SerializeField]
    private string stableCellId = string.Empty;

    [SerializeField]
    private GameObject cellPrefab;

    [SerializeField]
    private Vector3 authoredLocalPosition;

    [SerializeField]
    private Vector3 localBoundsCenter;

    [SerializeField]
    private Vector3 localBoundsSize;

    [SerializeField]
    private int sourceInstanceCount;

    [SerializeField]
    private bool hasStructuralFoundation;

    [SerializeField]
    private float authoredStructuralFoundationY;

    [SerializeField]
    private float structuralFoundationWeight;

    public string StableCellId => stableCellId;
    public GameObject CellPrefab => cellPrefab;
    public Vector3 AuthoredLocalPosition => authoredLocalPosition;
    public Vector3 LocalBoundsCenter => localBoundsCenter;
    public Vector3 LocalBoundsSize => localBoundsSize;
    public int SourceInstanceCount => sourceInstanceCount;
    public bool HasStructuralFoundation => hasStructuralFoundation;
    public float AuthoredStructuralFoundationY =>
        authoredStructuralFoundationY;
    public float StructuralFoundationWeight => structuralFoundationWeight;

    public void Configure(
        string newStableCellId,
        GameObject newCellPrefab,
        Vector3 newAuthoredLocalPosition,
        Vector3 newLocalBoundsCenter,
        Vector3 newLocalBoundsSize,
        int newSourceInstanceCount,
        bool newHasStructuralFoundation = false,
        float newAuthoredStructuralFoundationY = 0f,
        float newStructuralFoundationWeight = 0f)
    {
        // note: A cell records the original authored offset, allowing streaming to reconstruct the source location without procedural rearrangement.
        stableCellId = newStableCellId ?? string.Empty;
        cellPrefab = newCellPrefab;
        authoredLocalPosition = newAuthoredLocalPosition;
        localBoundsCenter = newLocalBoundsCenter;
        localBoundsSize = newLocalBoundsSize;
        sourceInstanceCount = Mathf.Max(0, newSourceInstanceCount);
        // note: Structural support metadata is measured while the authored cell is already open in the editor compiler; runtime can then ground dense cities without rescanning thousands of renderers.
        hasStructuralFoundation = newHasStructuralFoundation &&
            !float.IsNaN(newAuthoredStructuralFoundationY) &&
            !float.IsInfinity(newAuthoredStructuralFoundationY) &&
            !float.IsNaN(newStructuralFoundationWeight) &&
            !float.IsInfinity(newStructuralFoundationWeight) &&
            newStructuralFoundationWeight > 0f;
        authoredStructuralFoundationY = hasStructuralFoundation
            ? newAuthoredStructuralFoundationY
            : 0f;
        structuralFoundationWeight = hasStructuralFoundation
            ? newStructuralFoundationWeight
            : 0f;
    }
}

[CreateAssetMenu(
    fileName = "YQAuthoredSiteStreamingManifest",
    menuName = "YourQuest/World/Authored Site Streaming Manifest")]
public sealed class YQAuthoredSiteStreamingManifest : ScriptableObject
{
    [SerializeField]
    private string semanticStyleKey = string.Empty;

    [SerializeField]
    private YQAuthoredSiteKind siteKind = YQAuthoredSiteKind.Unknown;

    [SerializeField]
    private string sourceScenePath = string.Empty;

    [SerializeField]
    private string sourceSignature = string.Empty;

    [SerializeField]
    private GameObject sourceSitePrefab;

    [SerializeField]
    private List<YQAuthoredSiteStreamingCellRecord> cells =
        new List<YQAuthoredSiteStreamingCellRecord>();

    [SerializeField]
    private bool releaseEligible;

    [SerializeField]
    private YQStreamingSiteReviewState reviewState =
        YQStreamingSiteReviewState.Pending;

    [SerializeField]
    private string reviewNote = string.Empty;

    [SerializeField]
    private YQWorldSitePresentationMode presentationMode =
        YQWorldSitePresentationMode.Unknown;

    [SerializeField]
    private YQWorldStructureUsagePolicy structureUsagePolicy =
        YQWorldStructureUsagePolicy.Unspecified;

    [SerializeField]
    private int maximumEnterableStructures;

    public string SemanticStyleKey => semanticStyleKey;
    public YQAuthoredSiteKind SiteKind => siteKind;
    public string SourceScenePath => sourceScenePath;
    public string SourceSignature => sourceSignature;
    public GameObject SourceSitePrefab => sourceSitePrefab;
    public IReadOnlyList<YQAuthoredSiteStreamingCellRecord> Cells => cells;
    public bool ReleaseEligible => releaseEligible;
    public YQStreamingSiteReviewState ReviewState => reviewState;
    public string ReviewNote => reviewNote;
    public YQWorldSitePresentationMode PresentationMode =>
        presentationMode != YQWorldSitePresentationMode.Unknown
            ? presentationMode
            : ResolveDefaultPresentationMode(siteKind, semanticStyleKey);
    public bool RequiresCompleteExteriorShell =>
        PresentationMode == YQWorldSitePresentationMode.SeamlessExterior;
    public YQWorldStructureUsagePolicy StructureUsagePolicy =>
        structureUsagePolicy;
    public int MaximumEnterableStructures => maximumEnterableStructures;
    public bool SecondaryStructuresAreNonEnterableShells =>
        structureUsagePolicy ==
        YQWorldStructureUsagePolicy.SingleFurnishedPrimaryWithExteriorShells ||
        structureUsagePolicy ==
        YQWorldStructureUsagePolicy.ExteriorShellsOnly;

    public void ConfigureCandidate(
        string newSemanticStyleKey,
        YQAuthoredSiteKind newSiteKind,
        string newSourceScenePath,
        string newSourceSignature,
        GameObject newSourceSitePrefab,
        IEnumerable<YQAuthoredSiteStreamingCellRecord> newCells)
    {
        // note: Compiled manifests remain non-release candidates until their reconstructed review scene has passed visual inspection.
        semanticStyleKey = newSemanticStyleKey ?? string.Empty;
        siteKind = newSiteKind;
        sourceScenePath = newSourceScenePath ?? string.Empty;
        sourceSignature = newSourceSignature ?? string.Empty;
        sourceSitePrefab = newSourceSitePrefab;
        cells = newCells != null
            ? new List<YQAuthoredSiteStreamingCellRecord>(newCells)
            : new List<YQAuthoredSiteStreamingCellRecord>();
        presentationMode = ResolveDefaultPresentationMode(
            siteKind,
            semanticStyleKey);
        reviewState = YQStreamingSiteReviewState.Pending;
        reviewNote = string.Empty;
        releaseEligible = false;
    }

    public void ConfigurePresentationPolicy(
        YQWorldSitePresentationMode newPresentationMode)
    {
        // note: Presentation policy is reviewed deterministic metadata; it prevents interior shells from ever being exposed as seamless exterior world geometry.
        presentationMode = newPresentationMode !=
            YQWorldSitePresentationMode.Unknown
                ? newPresentationMode
                : ResolveDefaultPresentationMode(siteKind, semanticStyleKey);
    }

    public void ConfigureStructureUsagePolicy(
        YQWorldStructureUsagePolicy newPolicy,
        int newMaximumEnterableStructures)
    {
        // note: Structure usage separates visual shells from playable interiors so generation cannot assign quests, NPCs, loot, or entrances to façade-only buildings.
        structureUsagePolicy = newPolicy;
        maximumEnterableStructures = Mathf.Max(
            0,
            newMaximumEnterableStructures);
    }

    public void MarkReleaseEligible()
    {
        // note: Runtime selection is unlocked only by an explicit review action; compilation alone never promotes geometry.
        reviewState = YQStreamingSiteReviewState.Approved;
        reviewNote = string.Empty;
        releaseEligible = true;
    }

    public void DeferForRepair(string note)
    {
        // note: A visually rejected site remains persisted and repairable but cannot loop through the active review queue or enter runtime selection.
        reviewState = YQStreamingSiteReviewState.DeferredNeedsRepair;
        reviewNote = note ?? string.Empty;
        releaseEligible = false;
    }

    public static YQWorldSitePresentationMode ResolveDefaultPresentationMode(
        YQAuthoredSiteKind kind,
        string styleKey)
    {
        if (kind == YQAuthoredSiteKind.Interior ||
            string.Equals(
                styleKey,
                "sci_fi_engineers_room",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                styleKey,
                "scifi_engineers_room",
                StringComparison.OrdinalIgnoreCase))
        {
            return YQWorldSitePresentationMode.InteriorTransitionOnly;
        }

        if (kind == YQAuthoredSiteKind.Dungeon)
            return YQWorldSitePresentationMode.SubterraneanTransitionOnly;

        return YQWorldSitePresentationMode.SeamlessExterior;
    }
}
