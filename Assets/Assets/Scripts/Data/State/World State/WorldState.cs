// Assets/Assets/Scripts/Data/State/World State/WorldState.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorldState
{
    // note: New world saves use the schema that includes accepted proposal provenance fields.
    public int schemaVersion = YQStateContract.CurrentStateSchemaVersion;
    // note: WorldState owns canonical world identity, accepted content references, events, and monotonic mutations.
    public long stateRevision;
    public YQWorldIdentityRecord worldIdentity = new YQWorldIdentityRecord();
    public List<YQEntityIdentityRecord> identityRecords = new List<YQEntityIdentityRecord>();
    public List<YQAcceptedContentReference> acceptedContent = new List<YQAcceptedContentReference>();
    public List<YQEventEnvelope> eventLog = new List<YQEventEnvelope>();
    public List<string> appliedMutationCommitKeys = new List<string>();
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
        worldIdentity ??= new YQWorldIdentityRecord();
        identityRecords ??= new List<YQEntityIdentityRecord>();
        acceptedContent ??= new List<YQAcceptedContentReference>();
        eventLog ??= new List<YQEventEnvelope>();
        appliedMutationCommitKeys ??= new List<string>();
        for (int index = 0; index < eventLog.Count; index++) eventLog[index]?.EnsureCollections();
    }

    public void Touch(long unixNow)
    {
        lastUpdatedUnix = unixNow;
        stateRevision = Math.Max(0L, stateRevision) + 1L;
    }

    public void TouchNow()
    {
        Touch(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public bool TryApplyMutationCommit(string commitKey, long expectedRevision, out YQMutationReceipt receipt)
    {
        EnsureCollections();
        receipt = new YQMutationReceipt { commitKey = commitKey ?? string.Empty, stateRevision = stateRevision, appliedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        if (string.IsNullOrWhiteSpace(commitKey)) { receipt.message = "Mutation commit key is required."; return false; }
        if (appliedMutationCommitKeys.Contains(commitKey)) { receipt.message = "Mutation commit was already applied."; return false; }
        if (expectedRevision >= 0 && expectedRevision != stateRevision) { receipt.message = "Mutation result is stale for the current state revision."; return false; }
        appliedMutationCommitKeys.Add(commitKey);
        TouchNow();
        receipt.stateRevision = stateRevision;
        receipt.applied = true;
        receipt.message = "Mutation commit accepted.";
        return true;
    }

    public void AppendEventEnvelope(YQEventEnvelope envelope)
    {
        if (envelope == null) return;
        EnsureCollections();
        envelope.EnsureCollections();
        if (string.IsNullOrWhiteSpace(envelope.eventId)) envelope.eventId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(envelope.actorId)) envelope.actorId = worldIdentity != null ? worldIdentity.worldId : string.Empty;
        envelope.stateRevision = stateRevision;
        envelope.occurredUnix = envelope.occurredUnix > 0 ? envelope.occurredUnix : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        eventLog.Add(envelope);
        TouchNow();
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

    // note: The persisted spatial plan is the single coordinate authority shared by terrain, routes, sites, population, and quests.
    public GeneratedSpatialWorldPlanRecord spatialPlan =
        new GeneratedSpatialWorldPlanRecord();

    // note: The semantic authority stores only accepted graph facts and the eager opening envelope; untouched cells remain deterministic lazy queries.
    public GeneratedSemanticWorldAuthorityRecord semanticAuthority =
        new GeneratedSemanticWorldAuthorityRecord();

    // note: V2 is deliberately nullable; old saves stay purely V1 and a partial shadow artifact can never be mistaken for accepted spatial authority.
    public GeneratedSpatialWorldPlanV2Record spatialPlanV2;

    // note: Chunk records persist the semantic streaming frontier and meaningful per-chunk deltas without serializing unchanged generated decorations.
    public List<GeneratedSemanticChunkRecord> semanticChunks = new List<GeneratedSemanticChunkRecord>();

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

        spatialPlan ??=
            new GeneratedSpatialWorldPlanRecord();

        spatialPlan.EnsureCollections();

        semanticAuthority ??=
            new GeneratedSemanticWorldAuthorityRecord();

        semanticAuthority.EnsureCollections();

        if (spatialPlanV2 != null)
            spatialPlanV2.EnsureCollections();

        semanticChunks ??= new List<GeneratedSemanticChunkRecord>();
        for (int i = 0; i < semanticChunks.Count; i++)
            semanticChunks[i]?.EnsureCollections();

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
public sealed class GeneratedSemanticChunkRecord
{
    public string chunkId;
    public string worldSeed;
    public int chunkX;
    public int chunkZ;
    public string deterministicSeed;
    public string lifecycleState;
    public string parentRegionId;
    public string biome;
    public List<string> featureIds = new List<string>();
    public List<GeneratedSemanticSiteRecord> sites = new List<GeneratedSemanticSiteRecord>();
    public List<string> borderContracts = new List<string>();
    public string continuationSchemaVersion;
    public string seamFingerprint;
    public List<float> biomeWeights = new List<float>();
    public List<GeneratedSemanticChunkEdgeContractRecord> edgeContracts = new List<GeneratedSemanticChunkEdgeContractRecord>();
    public List<string> persistentDeltaIds = new List<string>();
    public long lastVisitedUnix;

    // note: Null-safe collection repair keeps older saves loadable while adding chunk persistence incrementally.
    public void EnsureCollections()
    {
        featureIds ??= new List<string>();
        sites ??= new List<GeneratedSemanticSiteRecord>();
        borderContracts ??= new List<string>();
        biomeWeights ??= new List<float>();
        edgeContracts ??= new List<GeneratedSemanticChunkEdgeContractRecord>();
        persistentDeltaIds ??= new List<string>();
        for (int i = 0; i < edgeContracts.Count; i++)
            edgeContracts[i]?.EnsureCollections();
    }
}

[Serializable]
public sealed class GeneratedSemanticSiteRecord
{
    public string siteId;
    public string semanticKind;
    public string assetSlot;
    public string biome;
    public string lifecycleState;
    public float worldX;
    public float worldZ;

    // note: These optional fields let streamed records carry the same immutable reservation identity as the unloaded semantic graph.
    public string ownerRegionId;
    public string ownerFeatureId;
    // note: Large sites publish one physical owner cell while member sectors retain the complete accepted footprint for deterministic activation.
    public string ownerCellId;
    public List<string> memberCellIds = new List<string>();
    public bool isOwnerCell;
    // note: Persist accepted naming and layout bindings on the streamed projection so unloading cannot erase site identity.
    public string displayName;
    public string layoutBindingId;
    public string layoutBindingVersion;
    public string layoutSignature;
    public float accessScore;
    public string accessConstraint;
    public float footprintRadius;
    public string entranceId;
    public float entranceX;
    public float entranceZ;
    public bool immutableReservation;
    public string provenance;
}

[Serializable]
public sealed class GeneratedSemanticWorldAuthorityRecord
{
    public string schemaVersion = "semantic_world_v3";
    // note: Overlay schema/version is persisted beside accepted graph facts so replay can reject stale mutation payloads deterministically.
    public string overlaySchemaVersion = YQStateContract.FeatureOverlaySchemaVersion;
    public long featureOverlayRevision;
    public string generationVersion = "analytic_macro_e_v1";
    public string coordinateSpec = "signed_cell_128m_centered_origin_minus_512";
    public string hashAlgorithmVersion = "fnv1a32_utf16_v1";
    public string worldSeed;
    public string sourceSpatialFingerprint;
    public int cellSizeMeters = 128;
    public int queryNeighborhoodRadiusCells = 2;
    public int openingEnvelopeRadiusCells = 4;
    // note: Persist the provisional huge-feature pacing and bounded lazy-query budget as explicit policy, not hidden placement math.
    public int hugePoiSpacingTargetCells = 30;
    public int lazyCandidateBudgetPerQuery = 9;
    public float syntheticMinimumAccessScore = 0.18f;
    public string syntheticFeaturePolicy = "seeded_spacing_geography_access_bounded";
    public List<string> betaDeferredFeatureKinds = new List<string>();
    public List<GeneratedSemanticCellPlanRecord> openingEnvelope =
        new List<GeneratedSemanticCellPlanRecord>();
    public List<GeneratedSemanticSiteReservationRecord> siteReservations =
        new List<GeneratedSemanticSiteReservationRecord>();
    public List<GeneratedSemanticRouteGraphRecord> routeGraph =
        new List<GeneratedSemanticRouteGraphRecord>();
    public List<GeneratedSemanticWaterNetworkRecord> waterNetworks =
        new List<GeneratedSemanticWaterNetworkRecord>();
    public List<GeneratedSemanticWorldFactRecord> meaningfulFacts =
        new List<GeneratedSemanticWorldFactRecord>();
    public List<GeneratedSemanticAcceptedOverrideRecord> acceptedOverrides =
        new List<GeneratedSemanticAcceptedOverrideRecord>();
    public List<GeneratedSemanticFeatureOverlayRecord> featureOverlays =
        new List<GeneratedSemanticFeatureOverlayRecord>();

    public void EnsureCollections()
    {
        // note: Null-safe migration keeps older world saves readable without fabricating accepted graph records.
        openingEnvelope ??= new List<GeneratedSemanticCellPlanRecord>();
        siteReservations ??= new List<GeneratedSemanticSiteReservationRecord>();
        routeGraph ??= new List<GeneratedSemanticRouteGraphRecord>();
        waterNetworks ??= new List<GeneratedSemanticWaterNetworkRecord>();
        meaningfulFacts ??= new List<GeneratedSemanticWorldFactRecord>();
        acceptedOverrides ??= new List<GeneratedSemanticAcceptedOverrideRecord>();
        featureOverlays ??= new List<GeneratedSemanticFeatureOverlayRecord>();
        if (string.IsNullOrWhiteSpace(overlaySchemaVersion))
            overlaySchemaVersion = YQStateContract.FeatureOverlaySchemaVersion;
        betaDeferredFeatureKinds ??= new List<string>();
        for (int i = 0; i < openingEnvelope.Count; i++)
            openingEnvelope[i]?.EnsureCollections();
        for (int i = 0; i < siteReservations.Count; i++)
            siteReservations[i]?.EnsureCollections();
        for (int i = 0; i < routeGraph.Count; i++)
            routeGraph[i]?.EnsureCollections();
        for (int i = 0; i < waterNetworks.Count; i++)
            waterNetworks[i]?.EnsureCollections();
        for (int i = 0; i < meaningfulFacts.Count; i++)
            meaningfulFacts[i]?.EnsureCollections();
        for (int i = 0; i < acceptedOverrides.Count; i++)
            acceptedOverrides[i]?.EnsureCollections();
        for (int i = 0; i < featureOverlays.Count; i++)
            featureOverlays[i]?.EnsureCollections();
    }
}

[Serializable]
public sealed class GeneratedSemanticCellPlanRecord
{
    public string cellId;
    public int cellX;
    public int cellZ;
    public string ownerRegionId;
    public string continentId;
    public string landformRegime;
    public float elevationIntent;
    public float ruggedness;
    public float temperature;
    public float moisture;
    public float ecologyForest;
    public float ecologyGrassland;
    public float ecologyWetland;
    public float civilizationPressure;
    public float danger;
    public float culturalInfluence;
    public string paletteContext;
    public float densityMask;
    public string localSeed;
    public string routeConstraint;
    public string waterConstraint;
    public string semanticHash;
    public List<string> featureIds = new List<string>();
    public List<string> siteIds = new List<string>();
    public List<string> routeIds = new List<string>();
    public List<string> waterIds = new List<string>();
    public List<GeneratedSemanticBoundaryRecord> edgeContracts =
        new List<GeneratedSemanticBoundaryRecord>();

    public void EnsureCollections()
    {
        // note: Cell collections are derived from stable IDs and stay sorted by the authority before persistence or hashing.
        featureIds ??= new List<string>();
        siteIds ??= new List<string>();
        routeIds ??= new List<string>();
        waterIds ??= new List<string>();
        edgeContracts ??= new List<GeneratedSemanticBoundaryRecord>();
        for (int i = 0; i < edgeContracts.Count; i++)
            edgeContracts[i]?.EnsureCollections();
    }
}

[Serializable]
public sealed class GeneratedSemanticBoundaryRecord
{
    public string edge;
    public string canonicalKey;
    public string neighborCellId;
    public string contractHash;
    public List<string> featureIds = new List<string>();
    public List<string> routeIds = new List<string>();
    public List<string> waterIds = new List<string>();

    public void EnsureCollections()
    {
        // note: Boundary identities are explicit so cell and edge queries cannot independently decide feature existence.
        featureIds ??= new List<string>();
        routeIds ??= new List<string>();
        waterIds ??= new List<string>();
    }
}

[Serializable]
public sealed class GeneratedSemanticSiteReservationRecord
{
    public string siteId;
    public string ownerRegionId;
    public string ownerFeatureId;
    // note: These accepted bindings remain authoritative while physical site sectors load and unload independently.
    public string displayName;
    public string layoutBindingId;
    public string layoutBindingVersion;
    public string layoutSignature;
    public float accessScore;
    public string accessConstraint;
    public string siteKind;
    public string structuralIntent;
    public float worldX;
    public float worldZ;
    public float footprintRadius;
    public float minimumExclusionRadius;
    public float expansionRadius;
    public string culturalIntent;
    public bool immutable;
    public bool accepted;
    public string provenance;
    // note: The reservation owns the deterministic sector that may publish the physical site root.
    public string ownerCellId;
    public List<string> memberCellIds = new List<string>();
    public List<GeneratedSemanticEntranceRecord> entrances =
        new List<GeneratedSemanticEntranceRecord>();
    public List<string> routeIds = new List<string>();

    public void EnsureCollections()
    {
        // note: Site reservations are complete before route compilation and never move when a member cell is loaded later.
        memberCellIds ??= new List<string>();
        entrances ??= new List<GeneratedSemanticEntranceRecord>();
        routeIds ??= new List<string>();
        for (int i = 0; i < entrances.Count; i++)
            entrances[i]?.EnsureCollections();
    }
}

[Serializable]
public sealed class GeneratedSemanticEntranceRecord
{
    public string entranceId;
    public float worldX;
    public float worldZ;
    public float headingDegrees;
    public string permittedRouteId;

    public void EnsureCollections()
    {
        // note: Entrance records are intentionally scalar and stable so route endpoints can be resolved without loading a site prefab.
    }
}

[Serializable]
public sealed class GeneratedSemanticRouteGraphRecord
{
    public string routeId;
    public string parentRouteId;
    public string ownerRegionId;
    public string fromSiteId;
    public string toSiteId;
    public string routeClass;
    public bool accepted;
    public bool permittedBoundaryContinuation;
    public List<GeneratedSemanticRoutePointRecord> points =
        new List<GeneratedSemanticRoutePointRecord>();
    public List<GeneratedSemanticRouteCrossingRecord> crossings =
        new List<GeneratedSemanticRouteCrossingRecord>();

    public void EnsureCollections()
    {
        // note: Route points and crossings are the only geometry consumed by unloaded route queries.
        points ??= new List<GeneratedSemanticRoutePointRecord>();
        crossings ??= new List<GeneratedSemanticRouteCrossingRecord>();
        for (int i = 0; i < crossings.Count; i++)
            crossings[i]?.EnsureCollections();
    }
}

[Serializable]
public sealed class GeneratedSemanticRoutePointRecord
{
    public float worldX;
    public float worldZ;
    public float cost;
}

[Serializable]
public sealed class GeneratedSemanticRouteCrossingRecord
{
    public string crossingId;
    public string waterId;
    public string crossingKind;
    public float worldX;
    public float worldZ;
    public float requiredSpan;

    public void EnsureCollections()
    {
        // note: Crossing ownership is explicit so bridge/ford realization can consume the same water contract later.
    }
}

[Serializable]
public sealed class GeneratedSemanticWaterNetworkRecord
{
    public string waterId;
    public string basinId;
    public string sourceId;
    public string downstreamWaterId;
    public string sinkId;
    public string ownerRegionId;
    public string kind;
    public float surfaceElevation;
    public float nominalWidth;
    public float nominalDepth;
    public bool accepted;
    public bool declaredSink;
    public List<GeneratedSemanticRoutePointRecord> points =
        new List<GeneratedSemanticRoutePointRecord>();
    public List<GeneratedSemanticWaterCrossingRecord> crossings =
        new List<GeneratedSemanticWaterCrossingRecord>();

    public void EnsureCollections()
    {
        // note: Water records carry source/downstream/sink identity and shared crossing samples as one graph contract.
        points ??= new List<GeneratedSemanticRoutePointRecord>();
        crossings ??= new List<GeneratedSemanticWaterCrossingRecord>();
    }
}

[Serializable]
public sealed class GeneratedSemanticWaterCrossingRecord
{
    public string crossingId;
    public int cellX;
    public int cellZ;
    public float worldX;
    public float worldZ;
    public float width;
    public float surfaceElevation;
    public float bedElevation;
    public float flowX;
    public float flowZ;
    public string upstreamWaterId;
    public string downstreamWaterId;
}

[Serializable]
public sealed class GeneratedSemanticWorldFactRecord
{
    public string factId;
    public string factKind;
    public string ownerId;
    public string value;
    public string provenance;
    public bool accepted;

    public void EnsureCollections()
    {
        // note: Meaningful facts are persisted with provenance; cache lifecycle and visit timestamps are excluded.
    }
}

[Serializable]
public sealed class GeneratedSemanticAcceptedOverrideRecord
{
    public string objectId;
    public string displayName;
    public string layoutBindingId;
    public string layoutBindingVersion;
    public string layoutSignature;
    public string provenance;
    public bool accepted;

    public void EnsureCollections()
    {
        // note: Accepted names and layout references are durable inputs, never regenerated from cell query order.
    }
}

[Serializable]
public sealed class GeneratedSemanticFeatureOverlayRecord
{
    public string schemaVersion = YQStateContract.FeatureOverlaySchemaVersion;
    public string featureId;
    public string ownerRegionId;
    public long revision;
    public string receiptId;
    public string mutationCommitKey;
    public string state;
    public bool tombstone;
    public string payload;
    public List<string> targetObjectIds = new List<string>();

    public void EnsureCollections()
    {
        // note: Normalize optional overlay fields at the save boundary so old records remain replayable and never require fabricated targets.
        if (string.IsNullOrWhiteSpace(schemaVersion))
            schemaVersion = YQStateContract.FeatureOverlaySchemaVersion;
        targetObjectIds ??= new List<string>();
        state ??= string.Empty;
        payload ??= string.Empty;
    }
}

[Serializable]
public sealed class GeneratedSemanticChunkEdgeContractRecord
{
    public string edge;
    public string canonicalKey;
    public string contractVersion;
    public int sampleCount;
    public List<float> terrainHeights = new List<float>();
    public List<float> biomeWeights = new List<float>();
    public List<GeneratedSemanticChunkPortalRecord> routePortals = new List<GeneratedSemanticChunkPortalRecord>();
    public List<GeneratedSemanticChunkPortalRecord> waterPortals = new List<GeneratedSemanticChunkPortalRecord>();
    public string contractFingerprint;

    // note: Keep typed edge collections null-safe so older saves can migrate without inventing portal data.
    public void EnsureCollections()
    {
        terrainHeights ??= new List<float>();
        biomeWeights ??= new List<float>();
        routePortals ??= new List<GeneratedSemanticChunkPortalRecord>();
        waterPortals ??= new List<GeneratedSemanticChunkPortalRecord>();
    }
}

[Serializable]
public sealed class GeneratedSemanticChunkPortalRecord
{
    public string portalId;
    public string featureId;
    public string kind;
    public string edge;
    public float edgeT;
    public float worldX;
    public float worldZ;
    public float tangentX;
    public float tangentZ;
    public float width;
    public float depth;
    public float surfaceElevation;
    public float bedElevation;
    public float flowX;
    public float flowZ;
    public int order;
    public string upstreamId;
    public string downstreamId;
}

[Serializable]
public class GeneratedSpatialWorldPlanRecord
{
    public string schemaVersion = "spatial_world_plan_v1";
    public string generationVersion;
    public string worldSeed;
    public string semanticFingerprint;
    public string structuralTheme;
    public float worldSize = 1024f;
    public List<GeneratedSpatialRegionRecord> regions =
        new List<GeneratedSpatialRegionRecord>();
    public List<GeneratedSpatialFeatureRecord> macroFeatures =
        new List<GeneratedSpatialFeatureRecord>();
    public List<GeneratedSpatialLocationRecord> locations =
        new List<GeneratedSpatialLocationRecord>();
    public List<GeneratedSpatialRouteRecord> routes =
        new List<GeneratedSpatialRouteRecord>();
    public GeneratedWorldGenerationMetricsRecord metrics =
        new GeneratedWorldGenerationMetricsRecord();

    public void EnsureCollections()
    {
        regions ??= new List<GeneratedSpatialRegionRecord>();
        macroFeatures ??= new List<GeneratedSpatialFeatureRecord>();
        locations ??= new List<GeneratedSpatialLocationRecord>();
        routes ??= new List<GeneratedSpatialRouteRecord>();
        metrics ??= new GeneratedWorldGenerationMetricsRecord();

        for (int i = 0; i < regions.Count; i++)
            regions[i]?.EnsureCollections();
        for (int i = 0; i < macroFeatures.Count; i++)
            macroFeatures[i]?.EnsureCollections();
        for (int i = 0; i < locations.Count; i++)
            locations[i]?.EnsureCollections();
        for (int i = 0; i < routes.Count; i++)
            routes[i]?.EnsureCollections();
    }
}

[Serializable]
public class GeneratedSpatialRegionRecord
{
    public string regionId;
    public string parentId = "world";
    public float centerX;
    public float centerZ;
    public float radius = 220f;
    public float elevationBias;
    public float moisture;
    public float ruggedness;
    public float civilizationDensity;
    public float danger;
    public string biome;
    public string terrainArchetype;
    public string transitionProfile;
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        tags ??= new List<string>();
    }
}

[Serializable]
public class GeneratedSpatialFeatureRecord
{
    public string featureId;
    public string parentRegionId;
    public string featureKind;
    public float centerX;
    public float centerZ;
    public float radiusX;
    public float radiusZ;
    public float headingDegrees;
    public float strength;
    public float waterLevel;
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        tags ??= new List<string>();
    }
}

[Serializable]
public class GeneratedSpatialLocationRecord
{
    public string locationId;
    public string parentRegionId;
    public string parentLocationId;
    public string locationKind;
    public string structuralArchetype;
    public float worldX;
    public float worldZ;
    public float footprintRadius;
    public float entranceHeadingDegrees;
    public float terrainSuitability;
    public float waterAccess;
    public float routeAccess;
    public float defensibility;
    public float finalScore;
    public bool questEligible = true;
    public bool requiresTerrainAdaptation;
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        tags ??= new List<string>();
    }
}

[Serializable]
public class GeneratedSpatialRouteRecord
{
    public string routeId;
    public string fromLocationId;
    public string toLocationId;
    public string parentRegionId;
    public string routeKind;
    public float estimatedCost;
    public float estimatedLength;
    public bool requiresBridge;
    public List<GeneratedSpatialPointRecord> waypoints =
        new List<GeneratedSpatialPointRecord>();
    public List<string> tags = new List<string>();

    public void EnsureCollections()
    {
        waypoints ??= new List<GeneratedSpatialPointRecord>();
        tags ??= new List<string>();
    }
}

[Serializable]
public class GeneratedSpatialPointRecord
{
    public float x;
    public float z;
    public float cost;
}

[Serializable]
public class GeneratedWorldGenerationMetricsRecord
{
    public int candidateCount;
    public int rejectedCandidateCount;
    public int repairedLocationCount;
    public int unreachableLocationCount;
    public int regionCount;
    public int settlementCount;
    public int hostileSiteCount;
    public int routeCount;
    public float averageSettlementSeparation;
    public float totalRouteLength;
    public float estimatedTraversableFraction;
    public string validationSummary;
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
    // note: Newly discovered records default to usable for backward compatibility; intake writes false for assets that fail runtime safety checks.
    public bool runtimeEligible = true;
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
    // note: Persist accepted cell transforms and streets; legacy saves leave this null and retain authored placement.
    public YQProceduralSettlementLayoutRecord proceduralLayout;
    // note: Engine-normalized physical scale request for fresh worlds; zero preserves the older selector's behavior.
    public int proceduralBlockTarget;
    // note: New plans opt into evidence-weighted cell selection; absent fields preserve the deterministic choices of older saves.
    public bool preferMeasuredStructuralCells;
    // note: Persist normalized generated cell-role priorities; absent values keep legacy selection behavior.
    public List<string> cellRoleIntents = new List<string>();
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
    // note: Semantic requests influence approved cell selection, never direct prefab identities or placement authority.
    public List<string> cellRoleIntents = new List<string>();
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
// note: Automatic refresh verification touch for persisted semantic chunk records.
