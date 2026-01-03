// Assets/Assets/Scripts/Data/State/World State/WorldState.cs

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorldState
{
    public int schemaVersion = 4;

    // ? WorldMemoryRenderer expects this
    public string worldName = "YourQuest";

    // ? WorldStateManager expects these (and DirectorPromptBuilder reads them)
    public string canonLedger = "";
    public string currentRegionId = "region_unknown";
    public string currentRegionName = "Unknown";
    public float tension = 0f;

    // ? WorldDeltaApplier expects these to exist
    public string lastLLMRationale = "";
    public float lastLLMConfidence = 0f;

    // Simple global numeric state
    public Dictionary<string, float> globalFlags = new Dictionary<string, float>();

    // Old-simple models (keep them for compatibility with previous saves)
    public Dictionary<string, float> factionAttitudes = new Dictionary<string, float>();  // factionId -> attitude
    public Dictionary<string, string> locationStates = new Dictionary<string, string>(); // locationId -> "safe"/"ruined"
    public Dictionary<string, float> locationImportance = new Dictionary<string, float>(); // locationId -> importance

    // ? WorldMemoryRenderer expects these collections
    public List<FactionRecord> factions = new List<FactionRecord>();
    public List<LocationRecord> locations = new List<LocationRecord>();
    public List<NpcRecord> npcs = new List<NpcRecord>();

    public long lastUpdatedUnix;

    // ---------------------------------------------------
    // Records (keep them generous; renderers can pick fields)
    // ---------------------------------------------------

    [Serializable]
    public class FactionRecord
    {
        public string factionId;
        public string name;
        [TextArea(2, 8)] public string description;
        public string status;              // "rising", "fractured", "hostile", etc.
        public float attitudeToPlayer;     // -1..1
        public long createdUnix;
        public long updatedUnix;
    }

    [Serializable]
    public class LocationRecord
    {
        public string locationId;
        public string regionId;
        public string name;
        [TextArea(2, 8)] public string description;

        public string state;               // "calm", "unsafe", "ruined", etc.
        public float importance;           // 0..1 (or bigger if you want)
        [TextArea(2, 8)] public string text; // optional extra notes

        public long createdUnix;
        public long updatedUnix;
    }

    [Serializable]
    public class NpcRecord
    {
        public string npcId;
        public string name;
        [TextArea(2, 8)] public string description;

        public string factionId;
        public string locationId;

        public float affinityToPlayer;     // -1..1
        public string status;              // "alive", "missing", "dead"

        public long createdUnix;
        public long updatedUnix;
    }

    // ---------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------

    public void Touch(long unixNow)
    {
        lastUpdatedUnix = unixNow;
    }

    public void TouchNow()
    {
        lastUpdatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static WorldState CreateDefault()
    {
        return new WorldState
        {
            schemaVersion = 4,
            worldName = "YourQuest",
            canonLedger = "",
            currentRegionId = "region_unknown",
            currentRegionName = "Unknown",
            tension = 0f,
            lastLLMRationale = "",
            lastLLMConfidence = 0f,
            globalFlags = new Dictionary<string, float>(),
            factionAttitudes = new Dictionary<string, float>(),
            locationStates = new Dictionary<string, string>(),
            locationImportance = new Dictionary<string, float>(),
            factions = new List<FactionRecord>(),
            locations = new List<LocationRecord>(),
            npcs = new List<NpcRecord>(),
            lastUpdatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    // ---------------------------------------------------
    // Delta application helpers (WorldDeltaApplier expects these)
    // ---------------------------------------------------

    public void ApplyFlagDelta(string key, string op, float value, string text = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        key = key.Trim();
        op = (op ?? "").Trim().ToLowerInvariant();

        if (globalFlags == null) globalFlags = new Dictionary<string, float>();

        globalFlags.TryGetValue(key, out float cur);
        float next = cur;

        switch (op)
        {
            case "add": next = cur + value; break;
            case "set": next = value; break;
            case "mul": next = cur * value; break;
            default: return;
        }

        globalFlags[key] = next;

        // Optional small canon append
        if (!string.IsNullOrWhiteSpace(text))
            AppendCanon(text.Trim());

        TouchNow();
    }

    public void ApplyFactionDelta(string factionId, string op, float value, string text = null)
    {
        if (string.IsNullOrWhiteSpace(factionId)) return;
        factionId = factionId.Trim();
        op = (op ?? "").Trim().ToLowerInvariant();

        // Keep the dictionary model in sync
        if (factionAttitudes == null) factionAttitudes = new Dictionary<string, float>();
        factionAttitudes.TryGetValue(factionId, out float cur);
        float next = cur;

        switch (op)
        {
            case "add": next = cur + value; break;
            case "set": next = value; break;
            case "mul": next = cur * value; break;
            default: return;
        }

        next = Mathf.Clamp(next, -1f, 1f);
        factionAttitudes[factionId] = next;

        // Also update list model if present
        var f = GetOrCreateFaction(factionId);
        f.attitudeToPlayer = next;
        if (!string.IsNullOrWhiteSpace(text)) f.status = text.Trim();
        f.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        TouchNow();
    }

    public void ApplyLocationDelta(string locationId, string op, float value, string valueText = null, string text = null)
    {
        if (string.IsNullOrWhiteSpace(locationId)) return;
        locationId = locationId.Trim();
        op = (op ?? "").Trim().ToLowerInvariant();

        if (locationImportance == null) locationImportance = new Dictionary<string, float>();
        locationImportance.TryGetValue(locationId, out float cur);
        float next = cur;

        switch (op)
        {
            case "add": next = cur + value; break;
            case "set": next = value; break;
            case "mul": next = cur * value; break;
            default: return;
        }

        locationImportance[locationId] = next;

        if (!string.IsNullOrWhiteSpace(valueText))
        {
            locationStates ??= new Dictionary<string, string>();
            locationStates[locationId] = valueText.Trim();
        }

        var l = GetOrCreateLocation(locationId);
        l.importance = next;

        if (!string.IsNullOrWhiteSpace(valueText)) l.state = valueText.Trim();
        if (!string.IsNullOrWhiteSpace(text)) l.text = text.Trim();
        l.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        TouchNow();
    }

    // ---------------------------------------------------
    // Helpers
    // ---------------------------------------------------

    public void AppendCanon(string line, int maxLines = 50)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(canonLedger))
        {
            var existing = canonLedger.Replace("\r", "").Split('\n');
            foreach (var e in existing)
            {
                var t = (e ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(t)) lines.Add(t);
            }
        }

        lines.Add(line.Trim());

        if (maxLines > 0 && lines.Count > maxLines)
            lines.RemoveRange(0, lines.Count - maxLines);

        canonLedger = string.Join("\n", lines);
    }

    private FactionRecord GetOrCreateFaction(string factionId)
    {
        factions ??= new List<FactionRecord>();

        for (int i = 0; i < factions.Count; i++)
            if (factions[i] != null && factions[i].factionId == factionId)
                return factions[i];

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var created = new FactionRecord
        {
            factionId = factionId,
            name = factionId,
            description = "",
            status = "",
            attitudeToPlayer = 0f,
            createdUnix = now,
            updatedUnix = now
        };

        factions.Add(created);
        return created;
    }

    private LocationRecord GetOrCreateLocation(string locationId)
    {
        locations ??= new List<LocationRecord>();

        for (int i = 0; i < locations.Count; i++)
            if (locations[i] != null && locations[i].locationId == locationId)
                return locations[i];

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var created = new LocationRecord
        {
            locationId = locationId,
            regionId = currentRegionId,
            name = locationId,
            description = "",
            state = "",
            importance = 0f,
            text = "",
            createdUnix = now,
            updatedUnix = now
        };

        locations.Add(created);
        return created;
    }
}
