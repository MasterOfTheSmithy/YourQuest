// note: Defines the typed request and result contracts used by the release LLM pipeline.
using System;
using System.Collections.Generic;

public enum YQLlmRequestPriority
{
    Background = 0,
    PlayerFacing = 1,
    StartupExclusive = 2
}

public enum YQLlmTerminalOutcome
{
    AcceptedResponse = 0,
    Failed = 1,
    InvalidResponse = 2,
    Cancelled = 3,
    Superseded = 4,
    Evicted = 5
}

public sealed class YQLlmRequest
{
    // note: The prompt is immutable once queued so diagnostics and retries describe the same intended work.
    public string prompt;
    public string debugTag;
    public LLMGenerationCategory category = LLMGenerationCategory.Default;
    public YQLlmRequestPriority priority = YQLlmRequestPriority.Background;
    public bool requireJson;
    // note: Structured generators may supply their authoritative JSON shape so llama.cpp cannot emit incompatible field types or containers.
    public Dictionary<string, object> jsonSchema;
    // note: Domain parsers may repair optional presentation fields while still strictly validating canonical gameplay data.
    public bool deferJsonValidationToCaller;
    public bool disableTimeout;
    public string exclusiveOwner;
    // note: These optional stamps let callers pin a request to a profile/world snapshot; omitted values are captured at admission.
    public string profileId;
    public string worldId;
    public int generationEpoch = -1;
    public string ownerId;
    public long playerStateRevision = -1;
    public long worldStateRevision = -1;
    // note: Callers can omit a revision only when their proposal is independent of mutable state; profile, world, and epoch ownership remain mandatory.
    public bool bindPlayerStateRevision = true;
    public bool bindWorldStateRevision = true;
    public int maxRetries = -1;
    public Dictionary<string, object> optionsOverride;
    // note: Optional episode metadata bounds nested transport/domain repair without changing ordinary requests.
    public YQRepairEpisode repairEpisode;
    public bool repairVerification;
    public bool protectPrompt;
    public string parentRequestKey;
    public string requiredOllamaModelDigest;
    public Func<bool> ownerStillCurrent;
}

// note: Central schema factories keep transport constraints aligned with the compact canonical prompts without making llama.cpp aware of Unity domain types.
public static class YQLlmJsonSchema
{
    public static Dictionary<string, object> BuildOrigin()
    {
        Dictionary<string, object> ability = Object(
            new Dictionary<string, object>
            {
                { "name", String() },
                { "kind", String("skill", "spell") },
                { "type", String("combat", "movement", "utility", "craft", "social", "control") },
                { "description", String() },
                { "vfxFamily", String("physical", "fire", "frost", "storm", "poison", "heal", "shield", "shadow", "earth", "air", "arcane", "blood") }
            },
            "name", "kind", "type", "description", "vfxFamily");

        Dictionary<string, object> loadoutEntry = Object(
            new Dictionary<string, object>
            {
                { "slot", String("weapon", "offhand", "boots") },
                { "nameHint", String() },
                { "descriptionHint", String() }
            },
            "slot", "nameHint", "descriptionHint");

        return Object(
            new Dictionary<string, object>
            {
                { "source", String() },
                { "directionKey", String("merchant", "lumberjack", "hero", "demonlord", "arcanist", "warden", "wanderer", "stillness", "custom") },
                { "stimulus", String() },
                { "className", String() },
                { "titleName", String() },
                { "ability", ability },
                { "quest", Object(new Dictionary<string, object> { { "name", String() }, { "description", String() } }, "name", "description") },
                { "loadout", Array(loadoutEntry, 3, 3) },
                { "goddessVoice", BuildGoddessVoice(includeWorldFields: false, expectedLocationCount: 0) }
            },
            "source", "directionKey", "stimulus", "className", "titleName", "ability", "quest", "loadout", "goddessVoice");
    }

    public static Dictionary<string, object> BuildWorld(int regionCount, int settlementCount, int encampmentCount)
    {
        Dictionary<string, object> region = Object(
            new Dictionary<string, object>
            {
                { "regionId", String() }, { "displayName", String() }, { "gridX", Integer() }, { "gridY", Integer() },
                { "terrainProfile", String() }, { "lore", String() }, { "assetStyleKey", String() }
            },
            "regionId", "displayName", "gridX", "gridY", "terrainProfile", "lore", "assetStyleKey");

        Dictionary<string, object> settlement = Object(
            new Dictionary<string, object>
            {
                { "settlementId", String() }, { "regionId", String() }, { "displayName", String() }, { "kind", String() },
                { "gridX", Integer() }, { "gridY", Integer() }, { "siteStyleIntent", String() }, { "marketBias", String() },
                { "serviceSlots", Array(String(), 1, 6) }, { "residentRoles", Array(String(), 1, 8) }
            },
            "settlementId", "regionId", "displayName", "kind", "gridX", "gridY", "siteStyleIntent", "marketBias", "serviceSlots", "residentRoles");

        Dictionary<string, object> encampment = Object(
            new Dictionary<string, object>
            {
                { "encampmentId", String() }, { "regionId", String() }, { "displayName", String() }, { "kind", String() },
                { "gridX", Integer() }, { "gridY", Integer() }, { "siteStyleIntent", String() }, { "inhabitantFactionId", String() },
                { "monsterFamily", String() }, { "layoutIntent", String() }
            },
            "encampmentId", "regionId", "displayName", "kind", "gridX", "gridY", "siteStyleIntent", "inhabitantFactionId", "monsterFamily", "layoutIntent");

        Dictionary<string, object> route = Object(
            new Dictionary<string, object>
            {
                { "routeId", String() }, { "fromRegionId", String() }, { "toRegionId", String() },
                { "routeKind", String() }, { "travelHook", String() }
            },
            "routeId", "fromRegionId", "toRegionId", "routeKind", "travelHook");

        return Object(
            new Dictionary<string, object>
            {
                { "schemaVersion", String("world_plan_v1") }, { "source", String() }, { "worldSeed", String() }, { "summary", String() },
                { "regions", Array(region, regionCount, regionCount) },
                { "settlements", Array(settlement, settlementCount, settlementCount) },
                { "encampments", Array(encampment, encampmentCount, encampmentCount) },
                { "routes", Array(route, 1, 64) },
                { "goddessVoice", BuildGoddessVoice(includeWorldFields: true, expectedLocationCount: settlementCount) }
            },
            "schemaVersion", "source", "worldSeed", "summary", "regions", "settlements", "encampments", "routes", "goddessVoice");
    }

    public static bool ValidateFactoryContracts(out string error)
    {
        // note: This validator exercises the same schema factories sent to llama.cpp without making a model request or entering Play Mode.
        Dictionary<string, object> origin = BuildOrigin();
        Dictionary<string, object> world = BuildWorld(4, 2, 3);

        if (!TryValidateNode(origin, "origin", out error) ||
            !TryValidateNode(world, "world", out error))
        {
            return false;
        }

        // note: Fixed collection sizes are gameplay contracts, so postflight verifies the factories preserve their requested deterministic counts.
        if (!TryValidateArrayBounds(world, "regions", 4, 4, out error) ||
            !TryValidateArrayBounds(world, "settlements", 2, 2, out error) ||
            !TryValidateArrayBounds(world, "encampments", 3, 3, out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static Dictionary<string, object> BuildGoddessVoice(
        bool includeWorldFields,
        int expectedLocationCount)
    {
        Dictionary<string, object> properties = new Dictionary<string, object>
        {
            { "completion", String() },
            { "nextPrelude", String() },
            // note: Structured minimums guarantee enough authored thoughts for the loading transcript instead of allowing an empty optional array.
            { "ambientLines", Array(String(), includeWorldFields ? 4 : 2, includeWorldFields ? 6 : 4) }
        };

        if (includeWorldFields)
        {
            properties["terrain"] = String();
            properties["environment"] = String();
            properties["populationPrelude"] = String();
            properties["populationMaterialization"] = String();
            properties["reveal"] = String();
            properties["locations"] = Array(
                Object(
                    new Dictionary<string, object>
                    {
                        { "locationId", String() }, { "settlementMaterialization", String() }, { "buildingMaterialization", String() }
                    },
                    "locationId", "settlementMaterialization", "buildingMaterialization"),
                expectedLocationCount,
                expectedLocationCount);
        }

        // note: Goddess prose is a required generated presentation contract now; strict grammar protects canonical JSON while preventing the small model from silently omitting every interesting line.
        return includeWorldFields
            ? Object(
                properties,
                "completion",
                "terrain",
                "environment",
                "populationPrelude",
                "populationMaterialization",
                "reveal",
                "ambientLines",
                "locations")
            : Object(
                properties,
                "completion",
                "nextPrelude",
                "ambientLines");
    }

    private static Dictionary<string, object> Object(Dictionary<string, object> properties, params string[] required)
    {
        Dictionary<string, object> schema = new Dictionary<string, object>
        {
            { "type", "object" }, { "properties", properties }, { "additionalProperties", false }
        };
        if (required != null && required.Length > 0)
            schema["required"] = required;
        return schema;
    }

    private static Dictionary<string, object> Array(object items, int minimum, int maximum)
    {
        return new Dictionary<string, object>
        {
            { "type", "array" }, { "items", items }, { "minItems", minimum }, { "maxItems", maximum }
        };
    }

    private static Dictionary<string, object> String(params string[] allowed)
    {
        Dictionary<string, object> schema = new Dictionary<string, object> { { "type", "string" } };
        if (allowed != null && allowed.Length > 0)
            schema["enum"] = allowed;
        return schema;
    }

    private static Dictionary<string, object> Integer()
    {
        return new Dictionary<string, object> { { "type", "integer" } };
    }

    private static bool TryValidateNode(
        Dictionary<string, object> node,
        string path,
        out string error)
    {
        if (node == null || !node.TryGetValue("type", out object typeValue) ||
            !(typeValue is string type))
        {
            error = path + ": schema node has no string type.";
            return false;
        }

        if (string.Equals(type, "object", StringComparison.Ordinal))
        {
            if (!node.TryGetValue("properties", out object propertiesValue) ||
                !(propertiesValue is Dictionary<string, object> properties))
            {
                error = path + ": object schema has no property map.";
                return false;
            }

            if (!node.TryGetValue("additionalProperties", out object additionalValue) ||
                !(additionalValue is bool additionalProperties) ||
                additionalProperties)
            {
                error = path + ": object schema must reject undeclared properties.";
                return false;
            }

            if (node.TryGetValue("required", out object requiredValue))
            {
                if (!(requiredValue is string[] required))
                {
                    error = path + ": required property list has the wrong type.";
                    return false;
                }

                for (int index = 0; index < required.Length; index++)
                {
                    if (string.IsNullOrWhiteSpace(required[index]) ||
                        !properties.ContainsKey(required[index]))
                    {
                        error = path + ": required property is missing from the declared property map: " + required[index];
                        return false;
                    }
                }
            }

            foreach (KeyValuePair<string, object> property in properties)
            {
                if (!(property.Value is Dictionary<string, object> child))
                {
                    error = path + "." + property.Key + ": property schema has the wrong type.";
                    return false;
                }

                if (!TryValidateNode(child, path + "." + property.Key, out error))
                    return false;
            }

            error = string.Empty;
            return true;
        }

        if (string.Equals(type, "array", StringComparison.Ordinal))
        {
            if (!node.TryGetValue("items", out object itemsValue) ||
                !(itemsValue is Dictionary<string, object> items) ||
                !node.TryGetValue("minItems", out object minimumValue) ||
                !(minimumValue is int minimum) ||
                !node.TryGetValue("maxItems", out object maximumValue) ||
                !(maximumValue is int maximum) ||
                minimum < 0 || maximum < minimum)
            {
                error = path + ": array schema has invalid items or bounds.";
                return false;
            }

            return TryValidateNode(items, path + "[]", out error);
        }

        if (string.Equals(type, "string", StringComparison.Ordinal) ||
            string.Equals(type, "integer", StringComparison.Ordinal))
        {
            error = string.Empty;
            return true;
        }

        error = path + ": unsupported schema type " + type + ".";
        return false;
    }

    private static bool TryValidateArrayBounds(
        Dictionary<string, object> root,
        string propertyName,
        int expectedMinimum,
        int expectedMaximum,
        out string error)
    {
        // note: Navigate only the known root property contract; structural validation above already proves every intermediate value is a schema dictionary.
        Dictionary<string, object> properties =
            (Dictionary<string, object>)root["properties"];
        Dictionary<string, object> array =
            (Dictionary<string, object>)properties[propertyName];
        int minimum = (int)array["minItems"];
        int maximum = (int)array["maxItems"];

        if (minimum != expectedMinimum || maximum != expectedMaximum)
        {
            error = "world." + propertyName +
                ": expected bounds " + expectedMinimum + ".." + expectedMaximum +
                " but found " + minimum + ".." + maximum + ".";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public readonly struct YQLlmRequestResult
{
    public readonly long requestId;
    public readonly string debugTag;
    public readonly LLMGenerationCategory category;
    public readonly bool success;
    public readonly YQLlmTerminalOutcome outcome;
    public readonly string text;
    public readonly string error;
    public readonly int attemptCount;
    public readonly string repairEpisodeKey;
    public readonly string repairRequestKey;
    public readonly float queueWaitSeconds;
    public readonly float generationSeconds;
    public readonly LLMCompiledPrompt compiledPrompt;
    public readonly string profileId;
    public readonly string worldId;
    public readonly int generationEpoch;
    public readonly string ownerId;
    public readonly long playerStateRevision;
    public readonly long worldStateRevision;

    // note: Capture response metadata with the content so callers can decide whether to accept, retry, or use fallback.
    public YQLlmRequestResult(
        long requestId,
        string debugTag,
        LLMGenerationCategory category,
        bool success,
        YQLlmTerminalOutcome outcome,
        string text,
        string error,
        int attemptCount,
        float queueWaitSeconds,
        float generationSeconds,
        LLMCompiledPrompt compiledPrompt,
        string profileId = null,
        string worldId = null,
        int generationEpoch = -1,
        string ownerId = null,
        long playerStateRevision = -1,
        long worldStateRevision = -1,
        string repairEpisodeKey = null,
        string repairRequestKey = null)
    {
        this.requestId = requestId;
        this.debugTag = debugTag ?? string.Empty;
        this.category = category;
        this.success = success;
        this.outcome = outcome;
        this.text = text;
        this.error = error ?? string.Empty;
        this.attemptCount = attemptCount;
        this.repairEpisodeKey = repairEpisodeKey ?? string.Empty;
        this.repairRequestKey = repairRequestKey ?? string.Empty;
        this.queueWaitSeconds = queueWaitSeconds;
        this.generationSeconds = generationSeconds;
        this.compiledPrompt = compiledPrompt;
        this.profileId = profileId ?? string.Empty;
        this.worldId = worldId ?? string.Empty;
        this.generationEpoch = generationEpoch;
        this.ownerId = ownerId ?? string.Empty;
        this.playerStateRevision = playerStateRevision;
        this.worldStateRevision = worldStateRevision;
    }
}
