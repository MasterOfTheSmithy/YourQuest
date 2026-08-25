using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQSemanticZoneReviewDescriptor : MonoBehaviour
{
    [SerializeField]
    private string stableZoneId = string.Empty;

    [SerializeField]
    private string sourceSignature = string.Empty;

    [SerializeField]
    private List<string> streamingCellIds = new List<string>();

    [SerializeField]
    private string stableCellId = string.Empty;

    public string StableZoneId => stableZoneId;
    public string SourceSignature => sourceSignature;
    public IReadOnlyList<string> StreamingCellIds => streamingCellIds;
    public string StableCellId => stableCellId;

    public void Configure(
        string newStableZoneId,
        string newSourceSignature,
        IEnumerable<string> newStreamingCellIds)
    {
        // note: Review proxies identify approved streaming cells without instantiating their renderer-heavy prefab hierarchies.
        stableZoneId = newStableZoneId ?? string.Empty;
        sourceSignature = newSourceSignature ?? string.Empty;
        streamingCellIds = newStreamingCellIds != null
            ? new List<string>(newStreamingCellIds)
            : new List<string>();
        stableCellId = string.Empty;
    }

    public void ConfigureCell(string newStableCellId)
    {
        // note: The same lightweight review contract identifies individual cell proxies without requiring another renderer or prefab hierarchy.
        stableZoneId = string.Empty;
        sourceSignature = string.Empty;
        streamingCellIds.Clear();
        stableCellId = newStableCellId ?? string.Empty;
    }
}
