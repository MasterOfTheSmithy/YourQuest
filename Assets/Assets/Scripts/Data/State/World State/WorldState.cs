// Assets/Assets/Scripts/Data/State/World State/WorldState.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorldState
{
    public int schemaVersion = 6;
    public string worldName = "YourQuest";
    public string canonLedger = "";
    public string currentRegionId = "region_unknown";
    public string currentRegionName = "Unknown";
    public float tension = 0f;
    public string lastLLMRationale = "";
    public float lastLLMConfidence = 0f;
    public Dictionary<string, float> globalFlags = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, float> factionAttitudes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> locationStates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, float> locationImportance = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    public List<FactionRecord> factions = new List<FactionRecord>();
    public List<LocationRecord> locations = new List<LocationRecord>();
    public List<NpcRecord> npcs = new List<NpcRecord>();
    public GeneratedWorldPlanRecord generatedWorldPlan = new GeneratedWorldPlanRecord();
    public long lastUpdatedUnix;

    [Serializable]
    public class FactionRecord
    {
        public string factionId;
        public string name;
        [TextArea(2, 8)] public string description;
        public string status;
        public float attitudeToPlayer;
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
        public string state;
        public float importance;
        [TextArea(2, 8)] public string text;
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
        public float affinityToPlayer;
        public string status;
        public long createdUnix;
        public long updatedUnix;
    }

    public void EnsureCollections()
    {
        globalFlags ??= new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        factionAttitudes ??= new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        locationStates ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        locationImportance ??= new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        factions ??= new List<FactionRecord>();
        locations ??= new List<LocationRecord>();
        npcs ??= new List<NpcRecord>();
        generatedWorldPlan ??= new GeneratedWorldPlanRecord();
        generatedWorldPlan.EnsureCollections();
        canonLedger ??= string.Empty;
    }

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
        WorldState state = new WorldState();
        state.EnsureCollections();
        state.TouchNow();
        return state;
    }

    public void ApplyFlagDelta(string key, string op, float value, string text = null)
    {
        EnsureCollections();
        if (string.IsNullOrWhiteSpace(key))
            return;

        string trimmedKey = key.Trim();
        string normalized = NormalizeMathOp(op);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        globalFlags.TryGetValue(trimmedKey, out float current);
        float next = ApplyMathOp(current, normalized, value);
        globalFlags[trimmedKey] = next;

        if (!string.IsNullOrWhiteSpace(text))
            AppendCanon(text.Trim());

        TouchNow();
    }

    public void ApplyFactionDelta(string factionId, string op, float value, string text = null)
    {
        EnsureCollections();
        if (string.IsNullOrWhiteSpace(factionId))
            return;

        string trimmedId = factionId.Trim();
        string normalized = NormalizeMathOp(op);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        factionAttitudes.TryGetValue(trimmedId, out float current);
        float next = Mathf.Clamp(ApplyMathOp(current, normalized, value), -1f, 1f);
        factionAttitudes[trimmedId] = next;

        FactionRecord record = GetOrCreateFaction(trimmedId);
        record.attitudeToPlayer = next;
        if (!string.IsNullOrWhiteSpace(text))
            record.status = text.Trim();
        record.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        TouchNow();
    }

    public void ApplyLocationDelta(string locationId, string op, float value, string valueText = null, string text = null)
    {
        EnsureCollections();
        if (string.IsNullOrWhiteSpace(locationId))
            return;

        string trimmedId = locationId.Trim();
        string normalized = NormalizeMathOp(op);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        locationImportance.TryGetValue(trimmedId, out float current);
        float next = ApplyMathOp(current, normalized, value);
        locationImportance[trimmedId] = next;

        if (!string.IsNullOrWhiteSpace(valueText))
            locationStates[trimmedId] = valueText.Trim();

        LocationRecord record = GetOrCreateLocation(trimmedId);
        record.importance = next;
        if (!string.IsNullOrWhiteSpace(valueText))
            record.state = valueText.Trim();
        if (!string.IsNullOrWhiteSpace(text))
            record.text = text.Trim();
        record.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        TouchNow();
    }

    public void AppendCanon(string line, int maxLines = 64)
    {
        EnsureCollections();
        if (string.IsNullOrWhiteSpace(line))
            return;

        string candidate = NormalizeCanon(line);
        if (string.IsNullOrWhiteSpace(candidate))
            return;

        List<string> lines = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(canonLedger))
        {
            string[] split = canonLedger.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < split.Length; i++)
            {
                string existing = NormalizeCanon(split[i]);
                if (string.IsNullOrWhiteSpace(existing))
                    continue;
                if (seen.Add(existing))
                    lines.Add(existing);
            }
        }

        if (seen.Add(candidate))
            lines.Add(candidate);
        else
        {
            // Move repeated canon to the end instead of duplicating it.
            lines.RemoveAll(v => string.Equals(v, candidate, StringComparison.OrdinalIgnoreCase));
            lines.Add(candidate);
        }

        if (maxLines > 0 && lines.Count > maxLines)
            lines.RemoveRange(0, lines.Count - maxLines);

        canonLedger = string.Join("\n", lines);
        TouchNow();
    }

    public float GetFactionAttitudeOrDefault(string factionId, float fallback = 0f)
    {
        EnsureCollections();
        if (string.IsNullOrWhiteSpace(factionId))
            return fallback;
        return factionAttitudes.TryGetValue(factionId.Trim(), out float value) ? value : fallback;
    }

    public float GetLocationImportanceOrDefault(string locationId, float fallback = 0f)
    {
        EnsureCollections();
        if (string.IsNullOrWhiteSpace(locationId))
            return fallback;
        return locationImportance.TryGetValue(locationId.Trim(), out float value) ? value : fallback;
    }

    public string GetLocationStateOrDefault(string locationId, string fallback = "")
    {
        EnsureCollections();
        if (string.IsNullOrWhiteSpace(locationId))
            return fallback;
        return locationStates.TryGetValue(locationId.Trim(), out string value) ? value : fallback;
    }

    public List<string> GetCanonLines()
    {
        EnsureCollections();
        List<string> results = new List<string>();
        if (string.IsNullOrWhiteSpace(canonLedger))
            return results;

        string[] split = canonLedger.Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < split.Length; i++)
        {
            string line = NormalizeCanon(split[i]);
            if (!string.IsNullOrWhiteSpace(line))
                results.Add(line);
        }
        return results;
    }

    private FactionRecord GetOrCreateFaction(string factionId)
    {
        EnsureCollections();
        for (int i = 0; i < factions.Count; i++)
        {
            FactionRecord record = factions[i];
            if (record != null && string.Equals(record.factionId, factionId, StringComparison.OrdinalIgnoreCase))
                return record;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        FactionRecord created = new FactionRecord
        {
            factionId = factionId,
            name = factionId,
            description = string.Empty,
            status = string.Empty,
            attitudeToPlayer = 0f,
            createdUnix = now,
            updatedUnix = now
        };
        factions.Add(created);
        return created;
    }

    private LocationRecord GetOrCreateLocation(string locationId)
    {
        EnsureCollections();
        for (int i = 0; i < locations.Count; i++)
        {
            LocationRecord record = locations[i];
            if (record != null && string.Equals(record.locationId, locationId, StringComparison.OrdinalIgnoreCase))
                return record;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        LocationRecord created = new LocationRecord
        {
            locationId = locationId,
            regionId = currentRegionId,
            name = locationId,
            description = string.Empty,
            state = string.Empty,
            importance = 0f,
            text = string.Empty,
            createdUnix = now,
            updatedUnix = now
        };
        locations.Add(created);
        return created;
    }

    private static float ApplyMathOp(float current, string op, float value)
    {
        switch (NormalizeMathOp(op))
        {
            case "add": return current + value;
            case "set": return value;
            case "mul": return current * value;
            default: return current;
        }
    }

    private static string NormalizeMathOp(string op)
    {
        string value = (op ?? string.Empty).Trim().ToLowerInvariant();
        switch (value)
        {
            case "add":
            case "inc":
            case "increase":
            case "delta":
                return "add";
            case "set":
            case "assign":
                return "set";
            case "mul":
            case "multiply":
                return "mul";
            default:
                return string.Empty;
        }
    }

    private static string NormalizeCanon(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}

[Serializable]
public class GeneratedWorldPlanRecord
{
    public string schemaVersion = "world_plan_v1";
    public string source;
    public string worldSeed;
    public string generatorPromptHash;
    public string promptBudgetPolicy;
    [TextArea(2, 8)] public string summary;
    [TextArea(2, 8)] public string designNotes;
    [TextArea(2, 12)] public string rawJson;
    public string generatedUnixString;
    public int targetPlayableHoursMin = 20;
    public int targetPlayableHoursMax = 50;
    public int maxPromptWorldLines = 22;
    public List<string> verboseInternals = new List<string>();
    public List<GeneratedRegionRecord> regions = new List<GeneratedRegionRecord>();
    public List<GeneratedSettlementRecord> settlements = new List<GeneratedSettlementRecord>();
    public List<GeneratedEncampmentRecord> encampments = new List<GeneratedEncampmentRecord>();
    public List<GeneratedWorldRouteRecord> routes = new List<GeneratedWorldRouteRecord>();
    public List<GeneratedFactionPlanRecord> factions =
    new List<GeneratedFactionPlanRecord>();

    // note: LLM-authored additive world details live beside the core geography so future systems can bind to them.
    public List<GeneratedPointOfInterestRecord> pointsOfInterest =
        new List<GeneratedPointOfInterestRecord>();

    public List<GeneratedWorldQuestHookRecord> worldQuestHooks =
        new List<GeneratedWorldQuestHookRecord>();

    public List<GeneratedNotableWorldObjectRecord> notableObjects =
        new List<GeneratedNotableWorldObjectRecord>();

    public List<GeneratedNpcPlanRecord> generatedNpcs =
        new List<GeneratedNpcPlanRecord>();

    public List<GeneratedRegionAssetPaletteRecord> assetPalettes =
        new List<GeneratedRegionAssetPaletteRecord>();

    public void EnsureCollections()
    {
        verboseInternals ??= new List<string>();
        regions ??= new List<GeneratedRegionRecord>();
        settlements ??= new List<GeneratedSettlementRecord>();
        encampments ??= new List<GeneratedEncampmentRecord>();
        routes ??= new List<GeneratedWorldRouteRecord>();
        factions ??=
    new List<GeneratedFactionPlanRecord>();

        pointsOfInterest ??=
            new List<GeneratedPointOfInterestRecord>();

        worldQuestHooks ??=
            new List<GeneratedWorldQuestHookRecord>();

        notableObjects ??=
            new List<GeneratedNotableWorldObjectRecord>();

        generatedNpcs ??=
            new List<GeneratedNpcPlanRecord>();

        assetPalettes ??=
            new List<GeneratedRegionAssetPaletteRecord>();

        for (int i = 0; i < regions.Count; i++)
            regions[i]?.EnsureCollections();
        for (int i = 0; i < settlements.Count; i++)
            settlements[i]?.EnsureCollections();
        for (int i = 0; i < encampments.Count; i++)
            encampments[i]?.EnsureCollections();
        for (int i = 0; i < routes.Count; i++)
            routes[i]?.EnsureCollections();
        for (int i = 0; i < factions.Count; i++)
            factions[i]?.EnsureCollections();

        for (int i = 0; i < pointsOfInterest.Count; i++)
            pointsOfInterest[i]?.EnsureCollections();

        for (int i = 0; i < worldQuestHooks.Count; i++)
            worldQuestHooks[i]?.EnsureCollections();

        for (int i = 0; i < notableObjects.Count; i++)
            notableObjects[i]?.EnsureCollections();

        for (int i = 0; i < generatedNpcs.Count; i++)
            generatedNpcs[i]?.EnsureCollections();

        for (int i = 0; i < assetPalettes.Count; i++)
            assetPalettes[i]?.EnsureCollections();
    }
}
[Serializable]
public class GeneratedNpcPlanRecord
{
    /*
     * Stable canonical identity.
     *
     * Once generated by Ollama and accepted into the world plan,
     * these values must not be regenerated during ordinary loads.
     */
    public string npcId;

    public string regionId;

    /*
     * Exactly one of settlementId / encampmentId will normally be set.
     */
    public string settlementId;
    public string encampmentId;

    public string factionId;

    /*
     * Ollama-generated proper name.
     */
    public string displayName;

    /*
     * Functional identity:
     * blacksmith, innkeeper, guard, farmer, scout,
     * bandit_leader, cultist, monster_champion, etc.
     */
    public string role;

    /*
     * Broad runtime grouping:
     *
     * resident
     * service
     * guard
     * notable
     * hostile
     * hostile_leader
     */
    public string archetype;

    /*
     * Compact characterization generated with the NPC.
     */
    public string ageBand;
    public string presentation;

    [TextArea(2, 6)]
    public string appearanceSummary;

    [TextArea(2, 6)]
    public string personality;

    public string speakingStyle;

    [TextArea(2, 6)]
    public string dailyRoutine;

    [TextArea(2, 6)]
    public string localKnowledge;

    [TextArea(2, 6)]
    public string privateConcern;

    /*
     * Runtime behavior flags.
     *
     * These are generated/normalized once and then persisted.
     */
    public bool notable;
    public bool merchant;
    public bool guard;
    public bool hostile;
    public bool boss;

    public List<string> tags =
        new List<string>();

    public List<string> verboseInternals =
        new List<string>();

    public void EnsureCollections()
    {
        tags ??=
            new List<string>();

        verboseInternals ??=
            new List<string>();
    }
}

[Serializable]
public class GeneratedRegionRecord
{
    public string regionId;
    public string displayName;
    public int regionIndex;
    public string role;
    public string scaleHint;
    public int dangerTier;
    public int gridX;
    public int gridY;
    public string deterministicSeed;
    public string terrainProfile;
    public string climateProfile;
    public string playerPressure;
    [TextArea(2, 8)] public string lore;
    [TextArea(2, 8)] public string gameplayPremise;
    public string traversalHook;
    public string economyHook;
    public string enemyPressureHook;
    public string assetPaletteId;
    public string assetStyleKey;
    public string assetStyleRationale;
    public List<string> biomeTags = new List<string>();
    public List<string> settlementIds = new List<string>();
    public List<string> encampmentIds = new List<string>();
    public List<string> landmarkIds = new List<string>();
    public List<string> verboseInternals = new List<string>();

    public void EnsureCollections()
    {
        biomeTags ??= new List<string>();
        settlementIds ??= new List<string>();
        encampmentIds ??= new List<string>();
        landmarkIds ??= new List<string>();
        verboseInternals ??= new List<string>();
    }
}

[Serializable]
public class GeneratedRegionAssetPaletteRecord
{
    public string paletteId;
    public string regionId;
    public string styleKey;
    public string architecturePack;
    public string terrainPack;
    public string naturePack;
    public string settlementPack;
    public string encampmentPack;
    public string layoutRuleProfile;
    public string mood;
    [TextArea(2, 8)] public string rationale;
    public List<string> styleTags = new List<string>();
    public List<string> forbiddenStyleTags = new List<string>();

    public List<GeneratedAssetReferenceRecord> terrainMaterials = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> floor = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> wall = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> roof = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> door = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> path = new List<GeneratedAssetReferenceRecord>();

    // Complete or near-complete structures suitable for recognizable
    // settlement lots. Kept separate from largeStructure because
    // largeStructure can contain gates, towers, statues, machinery,
    // fireplaces, bookshelves, and other non-building landmarks.
    public List<GeneratedAssetReferenceRecord> settlementBuilding =
        new List<GeneratedAssetReferenceRecord>();

    public List<GeneratedAssetReferenceRecord> largeStructure = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> floorDeco = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> wallDeco = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> vegetation = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> rock = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> lighting = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> lootContainer = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> enemySite = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> interiorDeco = new List<GeneratedAssetReferenceRecord>();
    public List<GeneratedAssetReferenceRecord> exteriorDeco = new List<GeneratedAssetReferenceRecord>();

    public List<string> layoutRules = new List<string>();
    public List<string> verboseInternals = new List<string>();

    public void EnsureCollections()
    {
        styleTags ??= new List<string>();
        forbiddenStyleTags ??= new List<string>();

        terrainMaterials ??= new List<GeneratedAssetReferenceRecord>();
        floor ??= new List<GeneratedAssetReferenceRecord>();
        wall ??= new List<GeneratedAssetReferenceRecord>();
        roof ??= new List<GeneratedAssetReferenceRecord>();
        door ??= new List<GeneratedAssetReferenceRecord>();
        path ??= new List<GeneratedAssetReferenceRecord>();

        settlementBuilding ??=
            new List<GeneratedAssetReferenceRecord>();

        largeStructure ??= new List<GeneratedAssetReferenceRecord>();
        floorDeco ??= new List<GeneratedAssetReferenceRecord>();
        wallDeco ??= new List<GeneratedAssetReferenceRecord>();
        vegetation ??= new List<GeneratedAssetReferenceRecord>();
        rock ??= new List<GeneratedAssetReferenceRecord>();
        lighting ??= new List<GeneratedAssetReferenceRecord>();
        lootContainer ??= new List<GeneratedAssetReferenceRecord>();
        enemySite ??= new List<GeneratedAssetReferenceRecord>();
        interiorDeco ??= new List<GeneratedAssetReferenceRecord>();
        exteriorDeco ??= new List<GeneratedAssetReferenceRecord>();

        layoutRules ??= new List<string>();
        verboseInternals ??= new List<string>();

        EnsureAssetList(terrainMaterials);
        EnsureAssetList(floor);
        EnsureAssetList(wall);
        EnsureAssetList(roof);
        EnsureAssetList(door);
        EnsureAssetList(path);

        EnsureAssetList(settlementBuilding);

        EnsureAssetList(largeStructure);
        EnsureAssetList(floorDeco);
        EnsureAssetList(wallDeco);
        EnsureAssetList(vegetation);
        EnsureAssetList(rock);
        EnsureAssetList(lighting);
        EnsureAssetList(lootContainer);
        EnsureAssetList(enemySite);
        EnsureAssetList(interiorDeco);
        EnsureAssetList(exteriorDeco);
    }

    private static void EnsureAssetList(
        List<GeneratedAssetReferenceRecord> records)
    {
        if (records == null)
            return;

        for (int i = 0; i < records.Count; i++)
            records[i]?.EnsureCollections();
    }
}

[Serializable]
public class GeneratedAssetReferenceRecord
{
    public string assetKey;
    public string assetPath;
    public string assetType;
    public string slotTag;
    public int weight = 1;
    public float scaleMin = 1f;
    public float scaleMax = 1f;
    public float footprintX = 1f;
    public float footprintZ = 1f;
    public string placementRule;
    public string rotationRule;
    public bool allowRepeat = true;
    public bool blocksNav;
    [TextArea(1, 4)] public string notes;
    public List<string> subTags = new List<string>();
    public List<string> styleTags = new List<string>();

    public void EnsureCollections()
    {
        subTags ??= new List<string>();
        styleTags ??= new List<string>();
    }
}

[Serializable]
public class GeneratedSettlementRecord
{
    public string settlementId;
    public string regionId;
    public string displayName;
    public string kind;
    public int approxPopulation;
    public string populationBand;
    public int gridX;
    public int gridY;
    public string deterministicSeed;
    public string siteStyleIntent;
    public string siteRoleIntent;
    public string runtimeSiteKitId;
    public string runtimeSiteSemanticStyle;
    public string runtimeSiteBindingVersion;
    public string securityProfile;
    public string marketBias;
    [TextArea(2, 8)] public string lore;
    [TextArea(2, 8)] public string dailyLoop;
    public List<string> serviceSlots = new List<string>();
    public List<string> residentRoles = new List<string>();
    public List<string> notableNpcIds = new List<string>();
    public List<string> factionIds = new List<string>();
    public List<string> questHookIds = new List<string>();
    public List<string> verboseInternals = new List<string>();

    public void EnsureCollections()
    {
        serviceSlots ??= new List<string>();
        residentRoles ??= new List<string>();
        notableNpcIds ??= new List<string>();
        factionIds ??= new List<string>();
        questHookIds ??= new List<string>();
        verboseInternals ??= new List<string>();
    }
}

[Serializable]
public class GeneratedEncampmentRecord
{
    public string encampmentId;
    public string regionId;
    public string displayName;
    public string kind;
    public int threatTier;
    public int gridX;
    public int gridY;
    public string deterministicSeed;
    public string siteStyleIntent;
    public string siteRoleIntent;
    public string runtimeSiteKitId;
    public string runtimeSiteSemanticStyle;
    public string runtimeSiteBindingVersion;
    public string inhabitantFactionId;
    public string monsterFamily;
    public string layoutIntent;
    public string stealthApproach;
    public string abilityProfile;
    public string surfacePresentation;
    public string bossIntent;
    public string rewardProfile;
    [TextArea(2, 8)] public string lore;
    public List<string> questHookIds = new List<string>();
    public List<string> verboseInternals = new List<string>();

    public void EnsureCollections()
    {
        questHookIds ??= new List<string>();
        verboseInternals ??= new List<string>();
    }
}

[Serializable]
public class GeneratedWorldRouteRecord
{
    public string routeId;
    public string fromRegionId;
    public string toRegionId;
    public string routeKind;
    public string travelHook;
    public string gateCondition;
    public List<string> riskTags = new List<string>();
    public List<string> landmarkIds = new List<string>();
    public List<string> verboseInternals = new List<string>();

    public void EnsureCollections()
    {
        riskTags ??= new List<string>();
        landmarkIds ??= new List<string>();
        verboseInternals ??= new List<string>();
    }
}

[Serializable]
public class GeneratedPointOfInterestRecord
{
    // note: POIs are generated as additive landmarks inside existing regions, not replacements for settlements.
    public string poiId;
    public string regionId;
    public string displayName;
    public string kind;
    public int gridX;
    public int gridY;
    public string deterministicSeed;
    [TextArea(2, 8)] public string lore;
    public string gameplayHook;
    public string visualStyleKey;
    public List<string> questHookIds = new List<string>();
    public List<string> landmarkIds = new List<string>();
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        questHookIds ??= new List<string>();
        landmarkIds ??= new List<string>();
        tags ??= new List<string>();
    }
}

[Serializable]
public class GeneratedWorldQuestHookRecord
{
    // note: World quest hooks are structured seeds; quest runtime can later promote them into full objectives.
    public string hookId;
    public string regionId;
    public string locationId;
    public string displayName;
    [TextArea(2, 8)] public string premise;
    public string objectiveIntent;
    public string rewardIntent;
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        tags ??= new List<string>();
    }
}

[Serializable]
public class GeneratedNotableWorldObjectRecord
{
    // note: Notable objects/items are lore and intent records until a loot/equipment system instantiates them.
    public string objectId;
    public string regionId;
    public string locationId;
    public string displayName;
    public string objectType;
    public string itemType;
    public string rarity;
    public string visualFamily;
    public string gameplayUse;
    [TextArea(2, 8)] public string lore;
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        tags ??= new List<string>();
    }
}

[Serializable]
public class GeneratedFactionPlanRecord
{
    public string factionId;
    public string displayName;
    public string factionKind;
    public string homeRegionId;
    public float attitudeToPlayer;
    public string motive;
    public string publicFace;
    public string relationToPlayer;
    public List<string> conflictTags = new List<string>();
    public List<string> verboseInternals = new List<string>();

    public void EnsureCollections()
    {
        conflictTags ??= new List<string>();
        verboseInternals ??= new List<string>();
    }
}
