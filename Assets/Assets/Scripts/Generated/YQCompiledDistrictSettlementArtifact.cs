using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class YQCompiledDistrictPlacementRecord
{
    public string stableDistrictId = string.Empty;
    public YQDistrictFunction districtFunction;
    public Vector3 position;
    public float yawDegrees;
    public Vector3 boundsSize;
    public int sourceInstanceCount;
}

public sealed class YQCompiledDistrictSettlementArtifact : ScriptableObject
{
    [SerializeField]
    private string compilerVersion = string.Empty;

    [SerializeField]
    private int worldSeed;

    [SerializeField]
    private string morphologyId = string.Empty;

    [SerializeField]
    private string kitId = string.Empty;

    [SerializeField]
    private bool valid;

    [SerializeField]
    private int preservedSourceInstanceCount;

    [SerializeField]
    private List<YQCompiledDistrictPlacementRecord> districtPlacements =
        new List<YQCompiledDistrictPlacementRecord>();

    [SerializeField]
    private List<string> validationMessages =
        new List<string>();

    public string CompilerVersion => compilerVersion;
    public int WorldSeed => worldSeed;
    public string MorphologyId => morphologyId;
    public string KitId => kitId;
    public bool Valid => valid;
    public int PreservedSourceInstanceCount => preservedSourceInstanceCount;
    public IReadOnlyList<YQCompiledDistrictPlacementRecord> DistrictPlacements => districtPlacements;
    public IReadOnlyList<string> ValidationMessages => validationMessages;

    public void Configure(
        string newCompilerVersion,
        int newWorldSeed,
        string newMorphologyId,
        string newKitId,
        bool newValid,
        int newPreservedSourceInstanceCount,
        IEnumerable<YQCompiledDistrictPlacementRecord> newDistrictPlacements,
        IEnumerable<string> newValidationMessages)
    {
        // note: The artifact persists exact accepted district transforms so runtime loading never depends on another LLM call or editor-only source scene.
        compilerVersion = newCompilerVersion ?? string.Empty;
        worldSeed = newWorldSeed;
        morphologyId = newMorphologyId ?? string.Empty;
        kitId = newKitId ?? string.Empty;
        valid = newValid;
        preservedSourceInstanceCount = Mathf.Max(0, newPreservedSourceInstanceCount);
        districtPlacements = newDistrictPlacements != null
            ? new List<YQCompiledDistrictPlacementRecord>(newDistrictPlacements)
            : new List<YQCompiledDistrictPlacementRecord>();
        validationMessages = newValidationMessages != null
            ? new List<string>(newValidationMessages)
            : new List<string>();
    }
}
