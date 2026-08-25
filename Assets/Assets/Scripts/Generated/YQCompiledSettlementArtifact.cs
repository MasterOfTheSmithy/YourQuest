using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class YQCompiledRoadRecord
{
    public string stableRoadId = string.Empty;
    public string role = string.Empty;
    public float width;
    public List<Vector3> centerline = new List<Vector3>();
}

[Serializable]
public sealed class YQCompiledParcelPlacementRecord
{
    public string stableParcelId = string.Empty;
    public string roadId = string.Empty;
    public YQParcelFunction parcelFunction;
    public Vector3 position;
    public float yawDegrees;
    public Vector2 footprintSize;
}

public sealed class YQCompiledSettlementArtifact : ScriptableObject
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
    private List<YQCompiledRoadRecord> roads =
        new List<YQCompiledRoadRecord>();

    [SerializeField]
    private List<YQCompiledParcelPlacementRecord> parcelPlacements =
        new List<YQCompiledParcelPlacementRecord>();

    [SerializeField]
    private List<string> validationMessages =
        new List<string>();

    public string CompilerVersion => compilerVersion;
    public int WorldSeed => worldSeed;
    public string MorphologyId => morphologyId;
    public string KitId => kitId;
    public bool Valid => valid;
    public IReadOnlyList<YQCompiledRoadRecord> Roads => roads;
    public IReadOnlyList<YQCompiledParcelPlacementRecord> ParcelPlacements => parcelPlacements;
    public IReadOnlyList<string> ValidationMessages => validationMessages;

    public void Configure(
        string newCompilerVersion,
        int newWorldSeed,
        string newMorphologyId,
        string newKitId,
        bool newValid,
        IEnumerable<YQCompiledRoadRecord> newRoads,
        IEnumerable<YQCompiledParcelPlacementRecord> newParcelPlacements,
        IEnumerable<string> newValidationMessages)
    {
        // note: This accepted compiler artifact stores exact deterministic layout state; runtime loading never asks the LLM to reproduce coordinates.
        compilerVersion = newCompilerVersion ?? string.Empty;
        worldSeed = newWorldSeed;
        morphologyId = newMorphologyId ?? string.Empty;
        kitId = newKitId ?? string.Empty;
        valid = newValid;
        roads = newRoads != null
            ? new List<YQCompiledRoadRecord>(newRoads)
            : new List<YQCompiledRoadRecord>();
        parcelPlacements = newParcelPlacements != null
            ? new List<YQCompiledParcelPlacementRecord>(newParcelPlacements)
            : new List<YQCompiledParcelPlacementRecord>();
        validationMessages = newValidationMessages != null
            ? new List<string>(newValidationMessages)
            : new List<string>();
    }
}
