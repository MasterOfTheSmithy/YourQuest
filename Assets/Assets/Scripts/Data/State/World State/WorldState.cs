// WorldState.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorldState
{
    public int schemaVersion = 1;

    // Stable-ish identifiers
    public string worldId = "world";
    public string worldName = "YourQuest";

    // Canon ledger (short, stable summary - NOT huge prose)
    // Keep this tight; it’s injected often.
    [TextArea(3, 12)]
    public string canonLedger =
        "Canon: Magic exists.\nThe world responds to witnessed acts.\nFactions compete for relics.";

    // Global numeric flags (economy, tension, corruption, etc.)
    public Dictionary<string, float> globalFlags = new Dictionary<string, float>();

    // Per-faction state
    public List<FactionRecord> factions = new List<FactionRecord>();

    // Known locations (discovered/important)
    public List<LocationRecord> locations = new List<LocationRecord>();

    // Known NPC cards (important only; don’t store every peasant)
    public List<NpcRecord> npcs = new List<NpcRecord>();

    // Global quest registry (optional; you may keep quests mostly on player)
    public List<WorldQuestRecord> worldQuests = new List<WorldQuestRecord>();

    // Locality: current “hot region” to help renderer choose what to include
    public string currentRegionId = "";

    public long lastUpdatedUnix;

    public void Touch()
    {
        lastUpdatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static WorldState CreateDefault()
    {
        var s = new WorldState
        {
            schemaVersion = 1,
            worldId = "world",
            worldName = "YourQuest",
            canonLedger =
                "Canon: Magic exists.\n" +
                "The world responds to witnessed acts.\n" +
                "Factions compete for relics.",
            globalFlags = new Dictionary<string, float>(),
            factions = new List<FactionRecord>(),
            locations = new List<LocationRecord>(),
            npcs = new List<NpcRecord>(),
            worldQuests = new List<WorldQuestRecord>(),
            currentRegionId = "region_unknown"
        };
        s.Touch();
        return s;
    }

    // ---------- Lookups / Upserts ----------

    public FactionRecord GetOrCreateFaction(string id, string name = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        id = id.Trim();

        for (int i = 0; i < factions.Count; i++)
        {
            var f = factions[i];
            if (f != null && f.factionId == id)
            {
                if (!string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(f.name))
                    f.name = name.Trim();
                return f;
            }
        }

        var created = new FactionRecord
        {
            factionId = id,
            name = string.IsNullOrWhiteSpace(name) ? id : name.Trim(),
            attitudeToPlayer = 0f,
            status = "active",
            flags = new Dictionary<string, float>()
        };

        factions.Add(created);
        Touch();
        return created;
    }

    public LocationRecord GetOrCreateLocation(string id, string name = null, string regionId = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        id = id.Trim();

        for (int i = 0; i < locations.Count; i++)
        {
            var l = locations[i];
            if (l != null && l.locationId == id)
            {
                if (!string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(l.name))
                    l.name = name.Trim();

                if (!string.IsNullOrWhiteSpace(regionId) && string.IsNullOrWhiteSpace(l.regionId))
                    l.regionId = regionId.Trim();

                return l;
            }
        }

        var created = new LocationRecord
        {
            locationId = id,
            name = string.IsNullOrWhiteSpace(name) ? id : name.Trim(),
            regionId = string.IsNullOrWhiteSpace(regionId) ? currentRegionId : regionId.Trim(),
            state = "normal",
            importance = 0.2f,
            text = "",
            flags = new Dictionary<string, float>()
        };

        locations.Add(created);
        Touch();
        return created;
    }

    public void SetFlag(string key, float value)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        key = key.Trim();
        globalFlags[key] = value;
        Touch();
    }

    public void IncFlag(string key, float delta)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        key = key.Trim();
        globalFlags.TryGetValue(key, out float cur);
        globalFlags[key] = cur + delta;
        Touch();
    }
}

[Serializable]
public class FactionRecord
{
    public string factionId;
    public string name;

    // -1..+1 is a good range for vibe
    public float attitudeToPlayer = 0f;

    // internal state flags for that faction (fear, wealth, suspicion)
    public Dictionary<string, float> flags = new Dictionary<string, float>();

    public string status = "active"; // active/hostile/allied/defeated
}

[Serializable]
public class LocationRecord
{
    public string locationId;
    public string name;
    public string regionId;

    // 0..1 "how known" helps curation
    public float importance = 0.2f;

    public string state = "normal"; // normal/raided/burning/abandoned

    // short note / narrative hook / last observation
    [TextArea(2, 6)]
    public string text = "";

    public Dictionary<string, float> flags = new Dictionary<string, float>();
}

[Serializable]
public class NpcRecord
{
    public string npcId;
    public string name;
    public string factionId;
    public string locationId;

    // Relationship to player -1..+1
    public float affinityToPlayer = 0f;

    public string status = "alive"; // alive/dead/missing
    public Dictionary<string, float> flags = new Dictionary<string, float>();
}

[Serializable]
public class WorldQuestRecord
{
    public string questId;
    public string name;
    public string description;
    public string status; // offer/active/complete/failed
    public string regionId;
    public string[] tags;
    public long updatedUnix;
}
