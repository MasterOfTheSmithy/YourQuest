using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "YQStreetProfile",
    menuName = "YourQuest/World Generation/Street Profile")]
public sealed class YQStreetProfileDefinition : ScriptableObject
{
    [SerializeField]
    private string stableProfileId = string.Empty;

    [SerializeField]
    private string kitId = string.Empty;

    [SerializeField]
    private float carriagewayWidth = 6f;

    [SerializeField]
    private float vergeWidth = 1.5f;

    [SerializeField]
    private float minimumSegmentLength = 8f;

    [SerializeField]
    private float maximumSegmentLength = 24f;

    [SerializeField]
    private float maximumSlopeDegrees = 10f;

    [SerializeField]
    private float maximumTurnDegrees = 28f;

    [SerializeField]
    private List<string> compatibleParcelTags =
        new List<string>();

    public string StableProfileId => stableProfileId;
    public string KitId => kitId;
    public float CarriagewayWidth => carriagewayWidth;
    public float VergeWidth => vergeWidth;
    public float MinimumSegmentLength => minimumSegmentLength;
    public float MaximumSegmentLength => maximumSegmentLength;
    public float MaximumSlopeDegrees => maximumSlopeDegrees;
    public float MaximumTurnDegrees => maximumTurnDegrees;
    public IReadOnlyList<string> CompatibleParcelTags => compatibleParcelTags;

    public void Configure(
        string newStableProfileId,
        string newKitId,
        float newCarriagewayWidth,
        float newVergeWidth,
        float newMinimumSegmentLength,
        float newMaximumSegmentLength,
        float newMaximumSlopeDegrees,
        float newMaximumTurnDegrees,
        IEnumerable<string> newCompatibleParcelTags)
    {
        // note: A street profile constrains compiler geometry and remains stable regardless of the LLM-authored settlement name or lore.
        stableProfileId = newStableProfileId ?? string.Empty;
        kitId = newKitId ?? string.Empty;
        carriagewayWidth = Mathf.Max(2f, newCarriagewayWidth);
        vergeWidth = Mathf.Max(0f, newVergeWidth);
        minimumSegmentLength = Mathf.Max(2f, newMinimumSegmentLength);
        maximumSegmentLength = Mathf.Max(minimumSegmentLength, newMaximumSegmentLength);
        maximumSlopeDegrees = Mathf.Clamp(newMaximumSlopeDegrees, 0f, 45f);
        maximumTurnDegrees = Mathf.Clamp(newMaximumTurnDegrees, 0f, 90f);
        compatibleParcelTags = newCompatibleParcelTags != null
            ? new List<string>(newCompatibleParcelTags)
            : new List<string>();
    }
}
