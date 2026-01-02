using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorldState
{
    public int schemaVersion = 1;

    public string worldId = "world";
    public string worldName = "YourQuest";

    [TextArea(3, 12)]
    public string canonLedger =
        "Canon: Magic exists.\nThe world responds to witnessed acts.\nFactions compete for relics.";

    public Dictionary<string, float> globalFlags = new Dictionary<string, float>();
    public List<FactionRecord> factions = new List<FactionRecord>();
    public List<LocationRecord> locations = new List<LocationRecord>();
    public List<NpcRecord> npcs = new List<NpcRecord>();
    public List<WorldQuestRecord> worldQuests = new List<WorldQuestRecord>();

    public string currentRegionId = "";

    public long lastUpdatedUnix;

    // ? Added: last LLM decision metadata (small, safe, helps debugging + tuning)
    [TextArea(2, 8)] public string lastLLMRationale = "";
    [Range(0f, 1f)] public float lastLLMConfidence = 0f;

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
            currentRegionId = "region_unknown",
            lastLLMRationale = "",
            lastLLMConfidence = 0f
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

    // =========================================================
    // ? Added: Delta application helpers used by WorldDeltaApplier
    // =========================================================

    public void ApplyFlagDelta(string key, string op, float value)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        key = key.Trim();

        if (globalFlags == null) globalFlags = new Dictionary<string, float>();
        globalFlags.TryGetValue(key, out float cur);

        float next = cur;
        op = (op ?? "").Trim().ToLowerInvariant();

        switch (op)
        {
            case "add": next = cur + value; break;
            case "set": next = value; break;
            case "mul": next = cur * value; break;
            default: return;
        }

        globalFlags[key] = next;
        Touch();
    }

    /// <summary>
    /// Applies a delta to a faction. Right now this drives attitude.
    /// Text (if present) is treated as a compact "status" override if meaningful.
    /// </summary>
    public void ApplyFactionDelta(string factionId, string op, float value, string text = null)
    {
        if (string.IsNullOrWhiteSpace(factionId)) return;
        var f = GetOrCreateFaction(factionId.Trim());
        if (f == null) return;

        float cur = f.attitudeToPlayer;
        float next = cur;
        op = (op ?? "").Trim().ToLowerInvariant();

        switch (op)
        {
            case "add": next = cur + value; break;
            case "set": next = value; break;
            case "mul": next = cur * value; break;
            default: return;
        }

        f.attitudeToPlayer = Mathf.Clamp(next, -1f, 1f);

        if (!string.IsNullOrWhiteSpace(text))
        {
            // Keep this short + state-like. Don’t dump prose in here.
            f.status = text.Trim();
        }

        Touch();
    }

    /// <summary>
    /// Applies a delta to a location. Numeric affects importance.
    /// valueText can optionally set state; text can update the location note.
    /// </summary>
    public void ApplyLocationDelta(string locationId, string op, float value, string valueText = null, string text = null)
    {
        if (string.IsNullOrWhiteSpace(locationId)) return;
        var l = GetOrCreateLocation(locationId.Trim());
        if (l == null) return;

        float cur = l.importance;
        float next = cur;
        op = (op ?? "").Trim().ToLowerInvariant();

        switch (op)
        {
            case "add": next = cur + value; break;
            case "set": next = value; break;
            case "mul": next = cur * value; break;
            default: return;
        }

        l.importance = Mathf.Clamp(next, -1000f, 1000f);

        if (!string.IsNullOrWhiteSpace(valueText))
            l.state = valueText.Trim();

        if (!string.IsNullOrWhiteSpace(text))
            l.text = text.Trim();

        Touch();
    }
}

[Serializable]
public class FactionRecord
{
    public string factionId;
    public string name;

    public float attitudeToPlayer = 0f;

    public Dictionary<string, float> flags = new Dictionary<string, float>();

    public string status = "active"; // active/hostile/allied/defeated
}

[Serializable]
public class LocationRecord
{
    public string locationId;
    public string name;
    public string regionId;

    public float importance = 0.2f;

    public string state = "normal";

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

    public float affinityToPlayer = 0f;

    public string status = "alive";
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
