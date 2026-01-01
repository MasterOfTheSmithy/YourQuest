// WorldDeltaDTO.cs
using System;
using System.Collections.Generic;

[Serializable]
public class WorldDeltaDTO
{
    public string rationale;
    public float confidence;

    public List<FlagOpDTO> flags = new List<FlagOpDTO>();
    public List<FactionOpDTO> factions = new List<FactionOpDTO>();
    public List<LocationOpDTO> locations = new List<LocationOpDTO>();
}

[Serializable]
public class FlagOpDTO
{
    public string key;
    public string op;   // set/inc/dec
    public float value;
}

[Serializable]
public class FactionOpDTO
{
    public string factionId;
    public string name;
    public string op;      // "attitude_set" | "attitude_inc" | "status_set"
    public float value;    // for attitude ops
    public string text;    // for status_set (and optional narrative)
}

[Serializable]
public class LocationOpDTO
{
    public string locationId;
    public string name;
    public string regionId;
    public string op;      // "state_set" | "importance_set" | "importance_inc"
    public float value;    // numeric ops
    public string text;    // narrative hook OR state text depending on your prompting style
}
