using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// note: These kinds are stable storage vocabulary; display labels and generated prose never select an identity.
public enum YQStableEntityKind
{
    PlayerProfile,
    Player,
    World,
    Continent,
    Region,
    Settlement,
    Site,
    Route,
    WaterFeature,
    Npc,
    Faction,
    Quest,
    Item,
    Content,
    Species,
    People,
    Origin
}

[Serializable]
public sealed class YQEntityIdentityRecord
{
    public string id;
    public YQStableEntityKind kind;
    public string parentId;
    public string immutableContentRef;
    public string contentHash;
    public string displayLabel;
    public List<string> legacyAliases = new List<string>();
    public long acceptedUnix;

    // note: Identity metadata is nullable-safe so schema-6 records can be upgraded without inventing future domain data.
    public void EnsureCollections()
    {
        legacyAliases ??= new List<string>();
    }
}

[Serializable]
public sealed class YQAcceptedContentReference
{
    public string contentId;
    public YQStableEntityKind kind;
    public string immutableSource;
    public string contentHash;
    // note: Accepted proposals retain provenance and the normalized payload so reloads reuse canonical content instead of regenerating it.
    public string schemaVersion;
    public string promptHash;
    public string generationHash;
    public string normalizedPayloadJson;
    public long acceptedRevision;
    public long acceptedUnix;
}

[Serializable]
public sealed class YQWorldIdentityRecord
{
    public string worldId;
    public string ownerProfileId;
    public string worldSeed = "yourquest_default_world";
    public string coordinateUnits = "meters";
    public string negativeFloorConvention = "floor(world / cellSize)";
    public float cellSize = 128f;
    public string generationVersion = "world-generation-v1";
    public string schemaVersion = "world-identity-v1";
    public string hashVersion = "sha256-v1";
    public string selectedSpatialArtifactId;
    public string selectedSpatialArtifactVersion;
    public Vector3 renderOrigin = Vector3.zero;
}

[Serializable]
public sealed class YQPlayerCollisionContract
{
    public string source = "CharacterController";
    public float radius;
    public float height;
    public Vector3 center;
    public float stepOffset;
    public float slopeLimit;
    public float supportedWalkSpeed;
    public float supportedSprintSpeed;
}

public static class YQPlayerContractCapture
{
    public static bool TryCapture(GameObject player, float supportedWalkSpeed, float supportedSprintSpeed, out YQPlayerCollisionContract contract)
    {
        contract = null;
        if (player == null) return false;
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null) return false;
        contract = new YQPlayerCollisionContract
        {
            radius = controller.radius,
            height = controller.height,
            center = controller.center,
            stepOffset = controller.stepOffset,
            slopeLimit = controller.slopeLimit,
            supportedWalkSpeed = Mathf.Max(0f, supportedWalkSpeed),
            supportedSprintSpeed = Mathf.Max(0f, supportedSprintSpeed)
        };
        return true;
    }
}

[Serializable]
public sealed class YQEventEnvelope
{
    public string eventId;
    public string eventType;
    public string actorId;
    public List<string> targetIds = new List<string>();
    public string logicalLocationId;
    public Vector3 logicalPosition;
    public string context;
    public string outcome;
    public long sessionSequence;
    public long stateRevision;
    public string mutationCommitKey;
    public long occurredUnix;

    // note: Envelope collections are repaired at the persistence boundary so event producers can remain small and typed.
    public void EnsureCollections()
    {
        targetIds ??= new List<string>();
    }
}

public interface IYQMutationReceipt
{
    string CommitKey { get; }
    long StateRevision { get; }
    bool Applied { get; }
    string Message { get; }
}

[Serializable]
public sealed class YQMutationReceipt : IYQMutationReceipt
{
    public string commitKey;
    public long stateRevision;
    public bool applied;
    public string message;
    public long appliedUnix;

    [JsonIgnore] public string CommitKey => commitKey;
    [JsonIgnore] public long StateRevision => stateRevision;
    [JsonIgnore] public bool Applied => applied;
    [JsonIgnore] public string Message => message;
}

[Serializable]
public sealed class YQProfileTransactionReceipt
{
    public string profileId;
    public string commitId;
    public int revision;
    public string playerChecksum;
    public string worldChecksum;
    public string previousCommitId;
    public bool published;
    public string failure;
    public long committedUnix;
    public List<YQProfileAuxiliaryDocumentRecord> auxiliaryDocuments = new List<YQProfileAuxiliaryDocumentRecord>();
}

public static class YQStateContract
{
    // note: Schema 8 adds profile-owned container inventories; accepted proposal provenance and world authority remain unchanged.
    public const int CurrentStateSchemaVersion = 8;
    public const int CurrentProfileManifestSchemaVersion = 2;
    public const string IdentitySchemaVersion = "stable-id-v1";
    public const string CoordinateSchemaVersion = "world-coordinate-v1";
    public const string GenerationVersion = "world-generation-v1";
    public const string HashVersion = "sha256-v1";
    // note: Keep feature mutation overlays independently versioned so older world saves can add the envelope without rerolling accepted geography.
    public const string FeatureOverlaySchemaVersion = "feature_overlay_v1";
    public const float DefaultCellSize = 128f;

    // note: SHA-256 gives deterministic IDs and checksums across machines without depending on runtime hash randomization.
    public static string StableId(YQStableEntityKind kind, string immutableSeed, int ordinal = 0)
    {
        string input = kind + "|" + (immutableSeed ?? string.Empty) + "|" + ordinal.ToString(CultureInfo.InvariantCulture);
        return "yq-" + kind.ToString().ToLowerInvariant() + "-" + Sha256Hex(input).Substring(0, 24);
    }

    // note: Legacy IDs use persisted structural position and creation time, never mutable display text.
    public static string LegacyId(YQStableEntityKind kind, string parentId, int ordinal, long createdUnix)
    {
        return StableId(kind, "legacy|" + (parentId ?? string.Empty) + "|" + createdUnix.ToString(CultureInfo.InvariantCulture), ordinal);
    }

    public static int CellIndex(float coordinate, float cellSize = DefaultCellSize)
    {
        return Mathf.FloorToInt(coordinate / Mathf.Max(0.001f, cellSize));
    }

    public static string Sha256Hex(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
                result.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }
}

public static class YQStateIdentity
{
    public static void EnsurePlayerState(PlayerState state)
    {
        if (state == null) return;
        state.identityRecords ??= new List<YQEntityIdentityRecord>();
        state.acceptedContent ??= new List<YQAcceptedContentReference>();
        EnsureIdentity(state.identityRecords, state.playerId, YQStableEntityKind.Player, string.Empty, state.displayName, 0);

        for (int index = 0; index < state.titles.Count; index++)
            if (state.titles[index] != null)
                EnsureValueId(ref state.titles[index].titleId, YQStableEntityKind.Content, state.playerId, index, state.titles[index].acquiredUnix, state.identityRecords, state.titles[index].name);
        for (int index = 0; index < state.classes.Count; index++)
            if (state.classes[index] != null)
                EnsureValueId(ref state.classes[index].classId, YQStableEntityKind.Content, state.playerId, index, state.classes[index].acquiredUnix, state.identityRecords, state.classes[index].name);
        for (int index = 0; index < state.skills.Count; index++)
            if (state.skills[index] != null)
                EnsureValueId(ref state.skills[index].skillId, YQStableEntityKind.Content, state.playerId, index, 0, state.identityRecords, state.skills[index].name);
        for (int index = 0; index < state.quests.Count; index++)
            if (state.quests[index] != null)
                EnsureValueId(ref state.quests[index].questId, YQStableEntityKind.Quest, state.playerId, index, state.quests[index].createdUnix, state.identityRecords, state.quests[index].name);
        for (int index = 0; index < state.inventoryItems.Count; index++)
            if (state.inventoryItems[index] != null)
                EnsureValueId(ref state.inventoryItems[index].itemId, YQStableEntityKind.Item, state.playerId, index, 0, state.identityRecords, state.inventoryItems[index].displayName);
        if (state.generatedOrigin != null)
        {
            if (string.IsNullOrWhiteSpace(state.generatedOrigin.originId))
                state.generatedOrigin.originId = YQStateContract.StableId(YQStableEntityKind.Origin, state.playerId + "|origin", 0);
            EnsureIdentity(state.identityRecords, state.generatedOrigin.originId, YQStableEntityKind.Origin, state.playerId, state.generatedOrigin.directionKey, 0);
        }

        for (int index = 0; index < state.identityRecords.Count; index++)
            state.identityRecords[index]?.EnsureCollections();
    }

    public static void EnsureWorldState(WorldState state)
    {
        if (state == null) return;
        state.identityRecords ??= new List<YQEntityIdentityRecord>();
        state.acceptedContent ??= new List<YQAcceptedContentReference>();
        state.worldIdentity ??= new YQWorldIdentityRecord();
        GeneratedWorldPlanRecord plan = state.generatedWorldPlan;
        string seed = !string.IsNullOrWhiteSpace(plan?.worldSeed) ? plan.worldSeed.Trim() : state.worldIdentity.worldSeed;
        if (string.IsNullOrWhiteSpace(state.worldIdentity.worldId) && !string.IsNullOrWhiteSpace(plan?.worldSeed) &&
            string.Equals(state.worldIdentity.worldSeed, "yourquest_default_world", StringComparison.OrdinalIgnoreCase))
            state.worldIdentity.worldSeed = plan.worldSeed.Trim();
        if (string.IsNullOrWhiteSpace(state.worldIdentity.worldSeed))
            state.worldIdentity.worldSeed = string.IsNullOrWhiteSpace(seed) ? "yourquest_default_world" : seed;
        if (string.IsNullOrWhiteSpace(state.worldIdentity.worldId))
            state.worldIdentity.worldId = YQStateContract.StableId(YQStableEntityKind.World, state.worldIdentity.worldSeed, 0);
        if (string.IsNullOrWhiteSpace(plan?.worldSeed))
        {
            if (plan != null) plan.worldSeed = state.worldIdentity.worldSeed;
        }
        state.worldIdentity.cellSize = Mathf.Max(0.001f, state.worldIdentity.cellSize);
        EnsureIdentity(state.identityRecords, state.worldIdentity.worldId, YQStableEntityKind.World, string.Empty, state.worldName, 0);

        if (plan != null)
        {
            if (string.IsNullOrWhiteSpace(state.worldIdentity.selectedSpatialArtifactId))
            {
                string spatialFingerprint = plan.spatialPlanV2 != null ? plan.spatialPlanV2.contentHash : plan.spatialPlan?.semanticFingerprint;
                if (!string.IsNullOrWhiteSpace(spatialFingerprint))
                    state.worldIdentity.selectedSpatialArtifactId = YQStateContract.StableId(YQStableEntityKind.Content, state.worldIdentity.worldId + "|spatial|" + spatialFingerprint, 0);
                state.worldIdentity.selectedSpatialArtifactVersion = plan.spatialPlanV2 != null ? plan.spatialPlanV2.generationVersion : plan.spatialPlan?.generationVersion;
            }
            for (int index = 0; index < plan.factions.Count; index++)
                if (plan.factions[index] != null)
                    EnsureValueId(ref plan.factions[index].factionId, YQStableEntityKind.Faction, state.worldIdentity.worldId, index, 0, state.identityRecords, plan.factions[index].displayName);
            for (int index = 0; index < plan.regions.Count; index++)
                if (plan.regions[index] != null)
                    EnsureValueId(ref plan.regions[index].regionId, YQStableEntityKind.Region, state.worldIdentity.worldId, index, 0, state.identityRecords, plan.regions[index].displayName);
            for (int index = 0; index < plan.settlements.Count; index++)
                if (plan.settlements[index] != null)
                    EnsureValueId(ref plan.settlements[index].settlementId, YQStableEntityKind.Settlement, state.worldIdentity.worldId, index, 0, state.identityRecords, plan.settlements[index].displayName);
            for (int index = 0; index < plan.routes.Count; index++)
                if (plan.routes[index] != null)
                    EnsureValueId(ref plan.routes[index].routeId, YQStableEntityKind.Route, state.worldIdentity.worldId, index, 0, state.identityRecords, plan.routes[index].routeKind);
            for (int index = 0; index < plan.encampments.Count; index++)
                if (plan.encampments[index] != null)
                    EnsureValueId(ref plan.encampments[index].encampmentId, YQStableEntityKind.Site, state.worldIdentity.worldId, index, 0, state.identityRecords, plan.encampments[index].displayName);
            for (int index = 0; index < plan.pointsOfInterest.Count; index++)
                if (plan.pointsOfInterest[index] != null)
                    EnsureValueId(ref plan.pointsOfInterest[index].poiId, YQStableEntityKind.Site, state.worldIdentity.worldId, index, 0, state.identityRecords, plan.pointsOfInterest[index].displayName);
            for (int index = 0; index < plan.worldQuestHooks.Count; index++)
                if (plan.worldQuestHooks[index] != null)
                    EnsureValueId(ref plan.worldQuestHooks[index].hookId, YQStableEntityKind.Quest, state.worldIdentity.worldId, index, 0, state.identityRecords, plan.worldQuestHooks[index].displayName);
            for (int index = 0; index < plan.notableObjects.Count; index++)
                if (plan.notableObjects[index] != null)
                    EnsureValueId(ref plan.notableObjects[index].objectId, YQStableEntityKind.Content, state.worldIdentity.worldId, index, 0, state.identityRecords, plan.notableObjects[index].displayName);
            for (int index = 0; index < plan.generatedNpcs.Count; index++)
                if (plan.generatedNpcs[index] != null)
                    EnsureValueId(ref plan.generatedNpcs[index].npcId, YQStableEntityKind.Npc, state.worldIdentity.worldId, index, 0, state.identityRecords, plan.generatedNpcs[index].displayName);

            // note: Register V1 spatial artifact references without replacing their accepted IDs or deriving from labels.
            if (plan.spatialPlan != null)
            {
                for (int index = 0; index < plan.spatialPlan.regions.Count; index++)
                    if (plan.spatialPlan.regions[index] != null)
                        EnsureValueId(ref plan.spatialPlan.regions[index].regionId, YQStableEntityKind.Region, state.worldIdentity.worldId, index, 0, state.identityRecords, plan.spatialPlan.regions[index].biome);
                for (int index = 0; index < plan.spatialPlan.locations.Count; index++)
                    if (plan.spatialPlan.locations[index] != null)
                        EnsureValueId(ref plan.spatialPlan.locations[index].locationId, YQStableEntityKind.Site, SpatialParent(plan.spatialPlan.locations[index].parentRegionId, state.worldIdentity.worldId), index, 0, state.identityRecords, plan.spatialPlan.locations[index].locationKind);
                for (int index = 0; index < plan.spatialPlan.routes.Count; index++)
                    if (plan.spatialPlan.routes[index] != null)
                        EnsureValueId(ref plan.spatialPlan.routes[index].routeId, YQStableEntityKind.Route, SpatialParent(plan.spatialPlan.routes[index].parentRegionId, state.worldIdentity.worldId), index, 0, state.identityRecords, plan.spatialPlan.routes[index].routeKind);
                for (int index = 0; index < plan.spatialPlan.macroFeatures.Count; index++)
                    if (plan.spatialPlan.macroFeatures[index] != null)
                        EnsureValueId(ref plan.spatialPlan.macroFeatures[index].featureId, IsWaterFeature(plan.spatialPlan.macroFeatures[index].featureKind) ? YQStableEntityKind.WaterFeature : YQStableEntityKind.Content, SpatialParent(plan.spatialPlan.macroFeatures[index].parentRegionId, state.worldIdentity.worldId), index, 0, state.identityRecords, plan.spatialPlan.macroFeatures[index].featureKind);
            }

            // note: Register accepted V2 topology IDs alongside the selected artifact so terrain, water, sites, and routes share one identity vocabulary.
            YQSpatialBlueprintV2 blueprint = plan.spatialPlanV2?.blueprint;
            if (blueprint != null)
            {
                for (int index = 0; index < blueprint.regions.Count; index++)
                    if (blueprint.regions[index] != null)
                        EnsureValueId(ref blueprint.regions[index].regionId, YQStableEntityKind.Region, state.worldIdentity.worldId, index, 0, state.identityRecords, string.Empty);
                for (int index = 0; index < blueprint.hydrology.Count; index++)
                    if (blueprint.hydrology[index] != null)
                        EnsureValueId(ref blueprint.hydrology[index].hydrologyId, YQStableEntityKind.WaterFeature, SpatialParent(blueprint.hydrology[index].parentRegionId, state.worldIdentity.worldId), index, 0, state.identityRecords, blueprint.hydrology[index].kind.ToString());
                for (int index = 0; index < blueprint.sites.Count; index++)
                    if (blueprint.sites[index] != null)
                        EnsureValueId(ref blueprint.sites[index].siteId, YQStableEntityKind.Site, SpatialParent(blueprint.sites[index].parentRegionId, state.worldIdentity.worldId), index, 0, state.identityRecords, blueprint.sites[index].kind.ToString());
                for (int index = 0; index < blueprint.routes.Count; index++)
                    if (blueprint.routes[index] != null)
                        EnsureValueId(ref blueprint.routes[index].routeId, YQStableEntityKind.Route, SpatialParent(blueprint.routes[index].parentRegionId, state.worldIdentity.worldId), index, 0, state.identityRecords, blueprint.routes[index].routeClass.ToString());
            }
        }
        for (int index = 0; index < state.factions.Count; index++)
            if (state.factions[index] != null)
                EnsureValueId(ref state.factions[index].factionId, YQStableEntityKind.Faction, state.worldIdentity.worldId, index, state.factions[index].createdUnix, state.identityRecords, state.factions[index].name);
        for (int index = 0; index < state.locations.Count; index++)
            if (state.locations[index] != null)
                EnsureValueId(ref state.locations[index].locationId, YQStableEntityKind.Site, state.worldIdentity.worldId, index, state.locations[index].createdUnix, state.identityRecords, state.locations[index].name);
        for (int index = 0; index < state.npcs.Count; index++)
            if (state.npcs[index] != null)
                EnsureValueId(ref state.npcs[index].npcId, YQStableEntityKind.Npc, state.worldIdentity.worldId, index, state.npcs[index].createdUnix, state.identityRecords, state.npcs[index].name);
        for (int index = 0; index < state.identityRecords.Count; index++)
            state.identityRecords[index]?.EnsureCollections();
    }

    public static YQEntityIdentityRecord EnsureIdentity(List<YQEntityIdentityRecord> records, string id, YQStableEntityKind kind, string parentId, string label, long acceptedUnix)
    {
        if (records == null || string.IsNullOrWhiteSpace(id)) return null;
        for (int index = 0; index < records.Count; index++)
        {
            if (records[index] != null && string.Equals(records[index].id, id, StringComparison.OrdinalIgnoreCase))
                return records[index];
        }
        YQEntityIdentityRecord created = new YQEntityIdentityRecord
        {
            id = id.Trim(), kind = kind, parentId = parentId ?? string.Empty,
            displayLabel = label ?? string.Empty, acceptedUnix = acceptedUnix
        };
        records.Add(created);
        return created;
    }

    private static void EnsureValueId(ref string id, YQStableEntityKind kind, string parentId, int ordinal, long createdUnix, List<YQEntityIdentityRecord> records, string label)
    {
        if (string.IsNullOrWhiteSpace(id))
            id = YQStateContract.LegacyId(kind, parentId, ordinal, createdUnix);
        EnsureIdentity(records, id, kind, parentId, label, createdUnix);
    }

    private static string SpatialParent(string parentId, string worldId)
    {
        // note: Empty spatial parent references resolve to the persisted world identity; supplied region IDs remain untouched for validation.
        return string.IsNullOrWhiteSpace(parentId) || string.Equals(parentId, "world", StringComparison.OrdinalIgnoreCase) ? worldId : parentId.Trim();
    }

    private static bool IsWaterFeature(string featureKind)
    {
        // note: V1 semantic intent selects only the stable vocabulary kind; the display label never participates in the ID hash.
        string normalized = (featureKind ?? string.Empty).Trim().ToLowerInvariant();
        return normalized.Contains("water") || normalized.Contains("river") || normalized.Contains("lake") || normalized.Contains("wetland") || normalized.Contains("coast");
    }
}

public sealed class YQIdentityValidationResult
{
    public readonly List<string> failures = new List<string>();
    public bool IsValid => failures.Count == 0;
}

public static class YQStateReferenceValidator
{
    public static YQIdentityValidationResult Validate(PlayerState player, WorldState world)
    {
        YQIdentityValidationResult result = new YQIdentityValidationResult();
        ValidateIdentityList("player", player?.identityRecords, result);
        ValidateIdentityList("world", world?.identityRecords, result);
        if (player != null && world != null)
        {
            HashSet<string> worldIds = BuildWorldIds(world);
            if (!string.IsNullOrWhiteSpace(player.currentRegionId) && player.currentRegionId != "region_unknown" && !worldIds.Contains(player.currentRegionId))
                result.failures.Add("Player currentRegionId has no world parent: " + player.currentRegionId);
        }
        if (world != null)
        {
            // note: Paired commits/load validation include container ownership and cross-store item identity conservation.
            if (player != null && !YQContainerInventory.ValidateOwnership(world, player, null, player.inventoryItems, out string containerFailure))
                result.failures.Add(containerFailure);
            HashSet<string> factionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> locationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < world.factions.Count; index++) if (world.factions[index] != null) factionIds.Add(world.factions[index].factionId ?? string.Empty);
            for (int index = 0; index < world.locations.Count; index++) if (world.locations[index] != null) locationIds.Add(world.locations[index].locationId ?? string.Empty);
            if (world.generatedWorldPlan != null)
            {
                for (int index = 0; index < world.generatedWorldPlan.factions.Count; index++) if (world.generatedWorldPlan.factions[index] != null) factionIds.Add(world.generatedWorldPlan.factions[index].factionId ?? string.Empty);
                for (int index = 0; index < world.generatedWorldPlan.settlements.Count; index++) if (world.generatedWorldPlan.settlements[index] != null) locationIds.Add(world.generatedWorldPlan.settlements[index].settlementId ?? string.Empty);
                for (int index = 0; index < world.generatedWorldPlan.encampments.Count; index++) if (world.generatedWorldPlan.encampments[index] != null) locationIds.Add(world.generatedWorldPlan.encampments[index].encampmentId ?? string.Empty);
                for (int index = 0; index < world.generatedWorldPlan.pointsOfInterest.Count; index++) if (world.generatedWorldPlan.pointsOfInterest[index] != null) locationIds.Add(world.generatedWorldPlan.pointsOfInterest[index].poiId ?? string.Empty);
            }
            for (int index = 0; index < world.npcs.Count; index++)
            {
                WorldState.NpcRecord npc = world.npcs[index];
                if (npc == null) continue;
                if (!string.IsNullOrWhiteSpace(npc.factionId) && !factionIds.Contains(npc.factionId)) result.failures.Add("NPC has missing faction: " + npc.npcId);
                if (!string.IsNullOrWhiteSpace(npc.locationId) && !locationIds.Contains(npc.locationId)) result.failures.Add("NPC has missing location: " + npc.npcId);
            }
        }
        return result;
    }

    private static HashSet<string> BuildWorldIds(WorldState world)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (world?.identityRecords != null) for (int index = 0; index < world.identityRecords.Count; index++) if (world.identityRecords[index] != null) ids.Add(world.identityRecords[index].id ?? string.Empty);
        return ids;
    }

    private static void ValidateIdentityList(string scope, List<YQEntityIdentityRecord> records, YQIdentityValidationResult result)
    {
        if (records == null) return;
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, YQStableEntityKind> kinds = new Dictionary<string, YQStableEntityKind>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < records.Count; index++)
        {
            YQEntityIdentityRecord record = records[index];
            if (record == null || string.IsNullOrWhiteSpace(record.id))
            {
                result.failures.Add(scope + " identity is empty at index " + index);
                continue;
            }
            string identityId = record.id.Trim();
            if (!ids.Add(identityId)) result.failures.Add(scope + " duplicate identity: " + identityId);
            else
            {
                kinds[identityId] = record.kind;
                parents[identityId] = record.parentId == null ? string.Empty : record.parentId.Trim();
            }
        }

        for (int index = 0; index < records.Count; index++)
        {
            YQEntityIdentityRecord record = records[index];
            if (record == null || string.IsNullOrWhiteSpace(record.id) || string.IsNullOrWhiteSpace(record.parentId))
                continue;

            string identityId = record.id.Trim();
            string parentId = record.parentId.Trim();
            if (string.Equals(identityId, parentId, StringComparison.OrdinalIgnoreCase))
            {
                result.failures.Add(scope + " identity cannot parent itself: " + identityId);
                continue;
            }

            bool parentResolved = kinds.TryGetValue(parentId, out YQStableEntityKind parentKind);
            if (!parentResolved)
            {
                // note: Preserve the legacy world alias while rejecting every other unresolved parent reference.
                if (!string.Equals(parentId, "world", StringComparison.OrdinalIgnoreCase))
                {
                    result.failures.Add(scope + " identity has missing parent: " + identityId + " -> " + parentId);
                    continue;
                }
                else
                {
                    parentKind = YQStableEntityKind.World;
                    parentResolved = true;
                }
            }

            if (parentResolved && !IsAllowedParent(record.kind, parentKind))
                result.failures.Add(scope + " identity has illegal parent kind: " + identityId + " (" + record.kind + " -> " + parentKind + ")");
            if (HasParentCycle(identityId, parents))
                result.failures.Add(scope + " identity has parent cycle: " + identityId);
        }
    }

    private static bool IsAllowedParent(YQStableEntityKind childKind, YQStableEntityKind parentKind)
    {
        // note: Parent vocabulary is deliberately small and extensible; future records add migrations instead of weakening existing links.
        switch (childKind)
        {
            case YQStableEntityKind.Player: return parentKind == YQStableEntityKind.PlayerProfile;
            case YQStableEntityKind.World: return false;
            case YQStableEntityKind.Continent: return parentKind == YQStableEntityKind.World;
            case YQStableEntityKind.Region: return parentKind == YQStableEntityKind.World || parentKind == YQStableEntityKind.Continent;
            case YQStableEntityKind.Settlement: return parentKind == YQStableEntityKind.World || parentKind == YQStableEntityKind.Continent || parentKind == YQStableEntityKind.Region;
            case YQStableEntityKind.Site: return parentKind == YQStableEntityKind.World || parentKind == YQStableEntityKind.Continent || parentKind == YQStableEntityKind.Region || parentKind == YQStableEntityKind.Settlement;
            case YQStableEntityKind.Route: return parentKind == YQStableEntityKind.World || parentKind == YQStableEntityKind.Region;
            case YQStableEntityKind.WaterFeature: return parentKind == YQStableEntityKind.World || parentKind == YQStableEntityKind.Region;
            case YQStableEntityKind.Npc: return parentKind == YQStableEntityKind.World || parentKind == YQStableEntityKind.Settlement || parentKind == YQStableEntityKind.Site || parentKind == YQStableEntityKind.Faction;
            case YQStableEntityKind.Faction: return parentKind == YQStableEntityKind.World || parentKind == YQStableEntityKind.Region;
            case YQStableEntityKind.Quest: return parentKind == YQStableEntityKind.Player || parentKind == YQStableEntityKind.World || parentKind == YQStableEntityKind.Settlement || parentKind == YQStableEntityKind.Site;
            case YQStableEntityKind.Item: return parentKind == YQStableEntityKind.Player || parentKind == YQStableEntityKind.World || parentKind == YQStableEntityKind.Settlement || parentKind == YQStableEntityKind.Site;
            case YQStableEntityKind.Content: return parentKind == YQStableEntityKind.Player || parentKind == YQStableEntityKind.World || parentKind == YQStableEntityKind.Settlement || parentKind == YQStableEntityKind.Site;
            case YQStableEntityKind.Species: return parentKind == YQStableEntityKind.World;
            case YQStableEntityKind.People: return parentKind == YQStableEntityKind.World || parentKind == YQStableEntityKind.Species;
            case YQStableEntityKind.Origin: return parentKind == YQStableEntityKind.Player;
            case YQStableEntityKind.PlayerProfile: return false;
            default: return false;
        }
    }

    private static bool HasParentCycle(string identityId, Dictionary<string, string> parents)
    {
        // note: Follow only persisted identity links so malformed cycles are reported without mutating the loaded snapshot.
        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { identityId };
        string current = identityId;
        while (!string.IsNullOrWhiteSpace(current) && !string.Equals(current, "world", StringComparison.OrdinalIgnoreCase))
        {
            if (!parents.TryGetValue(current, out string parentId) || string.IsNullOrWhiteSpace(parentId) || string.Equals(parentId, "world", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!visited.Add(parentId)) return true;
            current = parentId;
        }
        return false;
    }
}

public sealed class YQMigrationResult
{
    public bool supported;
    public bool changed;
    public int fromVersion;
    public int toVersion;
    public string message;
}

public static class YQStateMigrations
{
    public static bool TryNormalizePlayerDocument(string json, out string normalizedJson, out YQMigrationResult result, out string failure)
    {
        return TryNormalizeDocument(json, true, out normalizedJson, out result, out failure);
    }

    public static bool TryNormalizeWorldDocument(string json, out string normalizedJson, out YQMigrationResult result, out string failure)
    {
        return TryNormalizeDocument(json, false, out normalizedJson, out result, out failure);
    }

    private static bool TryNormalizeDocument(string json, bool playerDocument, out string normalizedJson, out YQMigrationResult result, out string failure)
    {
        normalizedJson = string.Empty;
        failure = string.Empty;
        result = new YQMigrationResult { toVersion = YQStateContract.CurrentStateSchemaVersion };
        JObject document;
        try
        {
            document = JObject.Parse(json ?? string.Empty);
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            return false;
        }

        result.fromVersion = ReadSchemaVersion(document);
        if (result.fromVersion > YQStateContract.CurrentStateSchemaVersion)
        {
            result.message = "Unsupported future " + (playerDocument ? "player" : "world") + " schema " + result.fromVersion;
            failure = result.message;
            return false;
        }

        bool changed = false;
        // note: Each persisted schema boundary is visited in order; steps are idempotent and never invent domain records.
        for (int version = result.fromVersion; version < YQStateContract.CurrentStateSchemaVersion; version++)
            changed |= playerDocument ? ApplyPlayerMigrationStep(version, document) : ApplyWorldMigrationStep(version, document);

        if (document["schemaVersion"]?.Value<int>() != YQStateContract.CurrentStateSchemaVersion)
        {
            document["schemaVersion"] = YQStateContract.CurrentStateSchemaVersion;
            changed = true;
        }
        result.changed = changed;
        result.supported = true;
        result.message = changed ? "Persisted document normalized through ordered migrations." : "Persisted document already uses the current schema.";
        normalizedJson = document.ToString(Formatting.None);
        return true;
    }

    private static int ReadSchemaVersion(JObject document)
    {
        if (document == null || document["schemaVersion"] == null)
            return 1;
        return document["schemaVersion"].Type == JTokenType.Integer && document["schemaVersion"].Value<int>() > 0
            ? document["schemaVersion"].Value<int>() : 1;
    }

    private static bool ApplyPlayerMigrationStep(int fromVersion, JObject document)
    {
        // note: Legacy aliases are converted before the typed model is created, preserving values that current JsonIgnore properties cannot read.
        switch (fromVersion)
        {
            case 1: return NormalizeLegacyPlayerProgression(document) | NormalizeLegacyPlayerPosition(document);
            case 2: return NormalizeLegacyPlayerFlags(document);
            case 3:
            case 4:
            case 5:
            case 6:
            case 7: return false;
            default: return false;
        }
    }

    private static bool ApplyWorldMigrationStep(int fromVersion, JObject document)
    {
        // note: Historical world schemas retained their wire names; ordered no-op boundaries preserve the payload without fabricating world content.
        switch (fromVersion)
        {
            case 1:
            case 2:
            case 3:
            case 4:
            case 5:
            case 6: return false;
            case 7:
                // note: Opened legacy rewards remain consumed; lazy source binding imports their existing receipts as empty storage.
                if (document["containers"] != null && document["containers"].Type != JTokenType.Null) return false;
                document["containers"] = new JObject();
                return true;
            default: return false;
        }
    }

    private static bool NormalizeLegacyPlayerProgression(JObject document)
    {
        if (document == null || document["experience"] != null || document["xp"] == null ||
            (document["xp"].Type != JTokenType.Float && document["xp"].Type != JTokenType.Integer))
            return false;
        float legacyXp = Mathf.Max(0f, document["xp"].Value<float>());
        document["experience"] = Mathf.FloorToInt(legacyXp);
        return true;
    }

    private static bool NormalizeLegacyPlayerPosition(JObject document)
    {
        if (document == null || !(document["lastPosition"] is JArray legacyPosition) || legacyPosition.Count < 3)
            return false;
        JObject position = new JObject
        {
            ["x"] = legacyPosition[0]?.Value<float>() ?? 0f,
            ["y"] = legacyPosition[1]?.Value<float>() ?? 0f,
            ["z"] = legacyPosition[2]?.Value<float>() ?? 0f
        };
        document["lastPosition"] = position;
        if (document["logicalPosition"] == null)
            document["logicalPosition"] = position.DeepClone();
        return true;
    }

    private static bool NormalizeLegacyPlayerFlags(JObject document)
    {
        if (document == null || !(document["flags"] is JObject flags))
            return false;
        JObject counters = document["behaviorCounters"] as JObject;
        if (counters == null)
        {
            document["behaviorCounters"] = flags.DeepClone();
            return true;
        }
        bool changed = false;
        foreach (JProperty flag in flags.Properties())
        {
            if (counters[flag.Name] == null)
            {
                counters[flag.Name] = flag.Value.DeepClone();
                changed = true;
            }
        }
        return changed;
    }

    public static bool TryMigrate(PlayerState state, out YQMigrationResult result)
    {
        result = new YQMigrationResult { toVersion = YQStateContract.CurrentStateSchemaVersion };
        if (state == null) { result.message = "Player record is null."; return false; }
        result.fromVersion = state.schemaVersion <= 0 ? 1 : state.schemaVersion;
        if (result.fromVersion > YQStateContract.CurrentStateSchemaVersion) { result.message = "Unsupported future player schema " + result.fromVersion; return false; }
        state.EnsureCollections();
        YQStateIdentity.EnsurePlayerState(state);
        result.changed = state.schemaVersion != YQStateContract.CurrentStateSchemaVersion;
        state.schemaVersion = YQStateContract.CurrentStateSchemaVersion;
        result.supported = true;
        result.message = result.changed ? "Player schema migrated in memory." : "Player schema already current.";
        return true;
    }

    public static bool TryMigrate(WorldState state, out YQMigrationResult result)
    {
        result = new YQMigrationResult { toVersion = YQStateContract.CurrentStateSchemaVersion };
        if (state == null) { result.message = "World record is null."; return false; }
        result.fromVersion = state.schemaVersion <= 0 ? 1 : state.schemaVersion;
        if (result.fromVersion > YQStateContract.CurrentStateSchemaVersion) { result.message = "Unsupported future world schema " + result.fromVersion; return false; }
        state.EnsureCollections();
        YQStateIdentity.EnsureWorldState(state);
        result.changed = state.schemaVersion != YQStateContract.CurrentStateSchemaVersion;
        state.schemaVersion = YQStateContract.CurrentStateSchemaVersion;
        result.supported = true;
        result.message = result.changed ? "World schema migrated in memory." : "World schema already current.";
        return true;
    }
}

public static class YQServiceLifecycle
{
    private static readonly List<Action> Teardowns = new List<Action>();
    private static int _requestEpoch;
    public static int RequestEpoch => _requestEpoch;

    public static int BeginProfileSession(string profileId)
    {
        _requestEpoch++;
        for (int index = Teardowns.Count - 1; index >= 0; index--)
            try { Teardowns[index]?.Invoke(); } catch (Exception exception) { Debug.LogWarning("[YQServiceLifecycle] Teardown failed: " + exception.Message); }
        return _requestEpoch;
    }

    public static void RegisterTeardown(Action teardown)
    {
        if (teardown != null && !Teardowns.Contains(teardown)) Teardowns.Add(teardown);
    }

    public static void UnregisterTeardown(Action teardown)
    {
        if (teardown != null) Teardowns.Remove(teardown);
    }

    public static bool IsCurrent(int epoch) => epoch == _requestEpoch;

    public static void ResetAll()
    {
        _requestEpoch++;
        Teardowns.Clear();
    }
}
