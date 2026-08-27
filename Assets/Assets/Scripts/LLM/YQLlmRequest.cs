// note: Defines the typed request and result contracts used by the release LLM pipeline.
using System;
using System.Collections.Generic;

public enum YQLlmRequestPriority
{
    Background = 0,
    PlayerFacing = 1,
    StartupExclusive = 2
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
    public int maxRetries = -1;
    public Dictionary<string, object> optionsOverride;
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
                { "goddessVoice", BuildGoddessVoice(includeWorldFields: false) }
            },
            "source", "directionKey", "stimulus", "className", "titleName", "ability", "quest", "loadout");
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
                { "goddessVoice", BuildGoddessVoice(includeWorldFields: true) }
            },
            "schemaVersion", "source", "worldSeed", "summary", "regions", "settlements", "encampments", "routes");
    }

    private static Dictionary<string, object> BuildGoddessVoice(bool includeWorldFields)
    {
        Dictionary<string, object> properties = new Dictionary<string, object>
        {
            { "completion", String() }, { "nextPrelude", String() }, { "ambientLines", Array(String(), 0, 8) }
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
                0,
                32);
        }

        return Object(properties);
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
}

public readonly struct YQLlmRequestResult
{
    public readonly long requestId;
    public readonly string debugTag;
    public readonly LLMGenerationCategory category;
    public readonly bool success;
    public readonly string text;
    public readonly string error;
    public readonly int attemptCount;
    public readonly float queueWaitSeconds;
    public readonly float generationSeconds;
    public readonly LLMCompiledPrompt compiledPrompt;

    // note: Capture response metadata with the content so callers can decide whether to accept, retry, or use fallback.
    public YQLlmRequestResult(
        long requestId,
        string debugTag,
        LLMGenerationCategory category,
        bool success,
        string text,
        string error,
        int attemptCount,
        float queueWaitSeconds,
        float generationSeconds,
        LLMCompiledPrompt compiledPrompt)
    {
        this.requestId = requestId;
        this.debugTag = debugTag ?? string.Empty;
        this.category = category;
        this.success = success;
        this.text = text;
        this.error = error ?? string.Empty;
        this.attemptCount = attemptCount;
        this.queueWaitSeconds = queueWaitSeconds;
        this.generationSeconds = generationSeconds;
        this.compiledPrompt = compiledPrompt;
    }
}
