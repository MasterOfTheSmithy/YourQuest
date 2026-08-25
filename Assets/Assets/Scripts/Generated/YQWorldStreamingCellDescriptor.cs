using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQWorldStreamingCellDescriptor : MonoBehaviour
{
    [SerializeField]
    private string stableCellId = string.Empty;

    [SerializeField]
    private string parentStyleKey = string.Empty;

    [SerializeField]
    private Vector3 authoredSiteOffset;

    [SerializeField]
    private int sourceInstanceCount;

    public string StableCellId => stableCellId;
    public string ParentStyleKey => parentStyleKey;
    public Vector3 AuthoredSiteOffset => authoredSiteOffset;
    public int SourceInstanceCount => sourceInstanceCount;

    public void Configure(
        string newStableCellId,
        string newParentStyleKey,
        Vector3 newAuthoredSiteOffset,
        int newSourceInstanceCount)
    {
        // note: Runtime streamers use this deterministic identity and offset; no LLM output can alter child geometry or local placement.
        stableCellId = newStableCellId ?? string.Empty;
        parentStyleKey = newParentStyleKey ?? string.Empty;
        authoredSiteOffset = newAuthoredSiteOffset;
        sourceInstanceCount = Mathf.Max(0, newSourceInstanceCount);
    }
}
