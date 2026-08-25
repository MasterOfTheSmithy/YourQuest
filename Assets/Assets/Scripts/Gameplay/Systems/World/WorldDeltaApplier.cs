using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

/// <summary>
/// Applies WorldDeltaDTO-like JSON with strong normalization and validation.
/// </summary>
public class WorldDeltaApplier : MonoBehaviour
{
    [Header("Confidence Gate")]
    [Range(0f, 1f)]
    public float minConfidence = 0.25f;

    [Header("Caps")]
    public int maxFlags = 18;
    public int maxFactions = 8;
    public int maxLocations = 12;
    public int maxRegionStyleChanges = 1;

    [Header("Refs")]
    public WorldStateManager worldStateManager;

    void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    public bool TryApply(string raw, out string error)
    {
        error = null;
        ResolveReferences();

        if (worldStateManager == null)
        {
            error = "WorldStateManager missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Empty response.";
            return false;
        }

        string json = ExtractFirstJsonObject(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "No JSON object found in response.";
            return false;
        }

        json = NormalizeLikelyJson(json);

        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch (Exception ex)
        {
            error = "Parse failed: " + ex.Message;
            return false;
        }

        NormalizeArrayField(root, "flags");
        NormalizeArrayField(root, "factions");
        NormalizeArrayField(root, "locations");
        NormalizeArrayField(root, "regionStyles");

        string rationale = (root.Value<string>("rationale") ?? "").Trim();
        float confidence = NormalizeConfidence(root["confidence"]);

        if (confidence < minConfidence)
        {
            error = $"Confidence {confidence:0.00} below min {minConfidence:0.00}.";
            return false;
        }

        var flags = SanitizeFlags(root["flags"] as JArray);
        var factions = SanitizeFactions(root["factions"] as JArray);
        var locations = SanitizeLocations(root["locations"] as JArray);
        var regionStyles = SanitizeRegionStyles(root["regionStyles"] as JArray);

        if (flags.Count > maxFlags) flags.RemoveRange(maxFlags, flags.Count - maxFlags);
        if (factions.Count > maxFactions) factions.RemoveRange(maxFactions, factions.Count - maxFactions);
        if (locations.Count > maxLocations) locations.RemoveRange(maxLocations, locations.Count - maxLocations);
        if (regionStyles.Count > maxRegionStyleChanges) regionStyles.RemoveRange(maxRegionStyleChanges, regionStyles.Count - maxRegionStyleChanges);

        if (flags.Count == 0 && factions.Count == 0 && locations.Count == 0 && regionStyles.Count == 0)
        {
            Debug.Log($"[WorldDeltaApplier] NO-OP delta (ignored): {rationale}");
            error = "No-op delta (empty ops).";
            return false;
        }

        ApplyToWorldState(flags, factions, locations, regionStyles, confidence, rationale);
        return true;
    }

    private void ApplyToWorldState(
        List<FlagOp> flags,
        List<FactionOp> factions,
        List<LocationOp> locations,
        List<RegionStyleOp> regionStyles,
        float confidence,
        string rationale)
    {
        var ws = worldStateManager.State; // ? your real API
        if (ws == null)
        {
            Debug.LogWarning("[WorldDeltaApplier] WorldState is null; cannot apply.");
            return;
        }

        foreach (var op in flags)
        {
            if (string.IsNullOrWhiteSpace(op.key)) continue;
            ws.ApplyFlagDelta(op.key, op.op, op.value);
        }

        foreach (var op in factions)
        {
            if (string.IsNullOrWhiteSpace(op.factionId)) continue;
            ws.ApplyFactionDelta(op.factionId, op.op, op.value, op.text);
        }

        foreach (var op in locations)
        {
            if (string.IsNullOrWhiteSpace(op.locationId)) continue;
            ws.ApplyLocationDelta(op.locationId, op.op, op.value, op.valueText, op.text);
        }

        for (int i = 0; i < regionStyles.Count; i++)
        {
            RegionStyleOp op = regionStyles[i];
            GeneratedWorldPlanRecord plan = ws.generatedWorldPlan;

            if (plan == null || plan.regions == null)
                continue;

            for (int regionIndex = 0; regionIndex < plan.regions.Count; regionIndex++)
            {
                GeneratedRegionRecord region = plan.regions[regionIndex];
                if (region == null || !string.Equals(region.regionId, op.regionId, StringComparison.OrdinalIgnoreCase))
                    continue;

                // note: Only an approved semantic key enters saved world state; Unity paths remain owned by the curated asset catalog.
                region.assetStyleKey = op.styleKey;
                region.assetStyleRationale = op.reason;
                region.assetPaletteId = string.Empty;
                break;
            }
        }

        ws.lastLLMRationale = rationale;
        ws.lastLLMConfidence = confidence;

        // note: Style-only deltas still advance persisted world revision metadata.
        ws.TouchNow();

        worldStateManager.Save(); // ? your real API

        Debug.Log($"[WorldDeltaApplier] Applied delta | conf={confidence:0.00} | {rationale}");
    }

    // ---------- Normalization Helpers ----------

    private static void NormalizeArrayField(JObject root, string key)
    {
        if (root[key] == null) root[key] = new JArray();
        if (root[key].Type != JTokenType.Array) root[key] = new JArray(root[key]);
    }

    private static float NormalizeConfidence(JToken token)
    {
        if (token == null) return 0f;

        if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            return Mathf.Clamp01(token.Value<float>());

        if (token.Type == JTokenType.String)
        {
            string s = token.Value<string>().Trim();
            if (float.TryParse(s, out float f))
                return Mathf.Clamp01(f);
        }

        return 0f;
    }

    private static string NormalizeLikelyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;

        string s = json.Trim();

        if (s.Length > 0 && s[0] == '\uFEFF')
            s = s.Substring(1).TrimStart();

        if (s.StartsWith("{\\\""))
        {
            s = s.Replace("\\\"", "\"");
            s = s.Replace("\\/", "/");
        }

        if (s.StartsWith("\"{") && s.EndsWith("}\""))
        {
            s = s.Substring(1, s.Length - 2);
            s = s.Replace("\\\"", "\"").Replace("\\/", "/");
        }

        return s;
    }

    // ---------- Sanitizers ----------

    private List<FlagOp> SanitizeFlags(JArray arr)
    {
        var list = new List<FlagOp>();
        if (arr == null) return list;

        foreach (var it in arr)
        {
            if (it == null || it.Type != JTokenType.Object) continue;
            var o = (JObject)it;

            string key = (o.Value<string>("key") ?? "").Trim();
            string op = (o.Value<string>("op") ?? "").Trim();
            float val = SafeFloat(o["value"]);

            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!TryNormalizeNumericOp(op, val, out string normalizedOp, out float normalizedValue)) continue;

            list.Add(new FlagOp { key = key, op = normalizedOp, value = normalizedValue });
        }

        return list;
    }

    private List<FactionOp> SanitizeFactions(JArray arr)
    {
        var list = new List<FactionOp>();
        if (arr == null) return list;

        foreach (var it in arr)
        {
            if (it == null || it.Type != JTokenType.Object) continue;
            var o = (JObject)it;

            string id = (o.Value<string>("factionId") ?? "").Trim();
            string op = (o.Value<string>("op") ?? "").Trim();
            string text = SafeString(o["text"]);

            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!TryNormalizeFactionOp(op, o["value"], text, out string normalizedOp, out float normalizedValue, out string normalizedText)) continue;

            list.Add(new FactionOp { factionId = id, op = normalizedOp, value = normalizedValue, text = normalizedText });
        }

        return list;
    }

    private List<LocationOp> SanitizeLocations(JArray arr)
    {
        var list = new List<LocationOp>();
        if (arr == null) return list;

        foreach (var it in arr)
        {
            if (it == null || it.Type != JTokenType.Object) continue;
            var o = (JObject)it;

            string id = (o.Value<string>("locationId") ?? "").Trim();
            string op = (o.Value<string>("op") ?? "").Trim();
            string valueText = SafeString(o["valueText"]);
            string text = SafeString(o["text"]);

            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!TryNormalizeLocationOp(op, o["value"], valueText, text, out string normalizedOp, out float normalizedValue, out string normalizedValueText, out string normalizedText)) continue;

            list.Add(new LocationOp
            {
                locationId = id,
                op = normalizedOp,
                value = normalizedValue,
                valueText = normalizedValueText,
                text = normalizedText
            });
        }

        return list;
    }

    private List<RegionStyleOp> SanitizeRegionStyles(JArray arr)
    {
        List<RegionStyleOp> list = new List<RegionStyleOp>();
        if (arr == null)
            return list;

        foreach (JToken token in arr)
        {
            if (token is not JObject entry)
                continue;

            string regionId = SafeString(entry["regionId"]);
            string styleKey = SafeString(entry["styleKey"]).ToLowerInvariant();
            string reason = SafeString(entry["reason"]);

            if (string.IsNullOrWhiteSpace(regionId) ||
                !YQWorldAssetCatalog.IsSupportedStyleKey(styleKey))
            {
                continue;
            }

            GeneratedWorldPlanRecord activePlan =
                worldStateManager != null && worldStateManager.State != null
                    ? worldStateManager.State.generatedWorldPlan
                    : null;

            GeneratedRegionRecord activeRegion = null;
            if (activePlan != null && activePlan.regions != null)
            {
                for (int i = 0; i < activePlan.regions.Count; i++)
                {
                    GeneratedRegionRecord candidate = activePlan.regions[i];
                    if (candidate != null && string.Equals(candidate.regionId, regionId, StringComparison.OrdinalIgnoreCase))
                    {
                        activeRegion = candidate;
                        break;
                    }
                }
            }

            if (activeRegion == null ||
                string.Equals(activeRegion.assetStyleKey, styleKey, StringComparison.OrdinalIgnoreCase))
            {
                // note: Unknown regions and already-active styles are rejected before they can masquerade as an applied world mutation.
                continue;
            }

            if (!YQWorldAssetCatalog.IsCoherentStyleTransition(
                    activeRegion.assetStyleKey,
                    styleKey,
                    reason))
            {
                // note: Routine movement may alter tension and encounters, but cannot physically turn a surface town into a sewer, hospital, dungeon, or another genre.
                Debug.LogWarning(
                    "[WorldDeltaApplier] Rejected incoherent region style transition " +
                    activeRegion.assetStyleKey + " -> " + styleKey +
                    " for '" + regionId + "': " + reason);
                continue;
            }

            list.Add(new RegionStyleOp
            {
                regionId = regionId,
                styleKey = styleKey,
                reason = reason
            });
        }

        return list;
    }

    private static float SafeFloat(JToken t)
    {
        if (t == null) return 0f;
        if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return t.Value<float>();
        if (t.Type == JTokenType.String && float.TryParse(t.Value<string>(), out float f)) return f;
        return 0f;
    }

    private static string SafeString(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
            return string.Empty;

        string value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool TryNormalizeNumericOp(string op, float value, out string normalizedOp, out float normalizedValue)
    {
        normalizedOp = string.Empty;
        normalizedValue = value;

        switch (NormalizeOpToken(op))
        {
            case "add":
            case "inc":
            case "increase":
            case "delta":
                normalizedOp = "add";
                return true;
            case "dec":
            case "decrease":
            case "sub":
            case "subtract":
                normalizedOp = "add";
                normalizedValue = -value;
                return true;
            case "set":
            case "assign":
                normalizedOp = "set";
                return true;
            case "mul":
            case "multiply":
                normalizedOp = "mul";
                return true;
            default:
                return false;
        }
    }

    private static bool TryNormalizeFactionOp(
        string op,
        JToken valueToken,
        string text,
        out string normalizedOp,
        out float normalizedValue,
        out string normalizedText)
    {
        normalizedOp = string.Empty;
        normalizedValue = SafeFloat(valueToken);
        normalizedText = text;

        switch (NormalizeOpToken(op))
        {
            case "attitude_inc":
            case "attitude_add":
                normalizedOp = "add";
                return true;
            case "attitude_dec":
                normalizedOp = "add";
                normalizedValue = -normalizedValue;
                return true;
            case "attitude_set":
                normalizedOp = "set";
                return true;
            case "status_set":
                normalizedOp = "add";
                normalizedValue = 0f;
                if (string.IsNullOrWhiteSpace(normalizedText))
                    normalizedText = SafeString(valueToken);
                return !string.IsNullOrWhiteSpace(normalizedText);
            default:
                return TryNormalizeNumericOp(op, normalizedValue, out normalizedOp, out normalizedValue);
        }
    }

    private static bool TryNormalizeLocationOp(
        string op,
        JToken valueToken,
        string valueText,
        string text,
        out string normalizedOp,
        out float normalizedValue,
        out string normalizedValueText,
        out string normalizedText)
    {
        normalizedOp = string.Empty;
        normalizedValue = SafeFloat(valueToken);
        normalizedValueText = valueText;
        normalizedText = text;

        switch (NormalizeOpToken(op))
        {
            case "state_set":
                normalizedOp = "add";
                normalizedValue = 0f;
                if (string.IsNullOrWhiteSpace(normalizedValueText))
                    normalizedValueText = SafeString(valueToken);
                return !string.IsNullOrWhiteSpace(normalizedValueText);
            case "importance_inc":
            case "importance_add":
                normalizedOp = "add";
                return true;
            case "importance_dec":
                normalizedOp = "add";
                normalizedValue = -normalizedValue;
                return true;
            case "importance_set":
                normalizedOp = "set";
                return true;
            default:
                return TryNormalizeNumericOp(op, normalizedValue, out normalizedOp, out normalizedValue);
        }
    }

    private static string NormalizeOpToken(string op)
    {
        return string.IsNullOrWhiteSpace(op) ? string.Empty : op.Trim().ToLowerInvariant();
    }

    private void ResolveReferences()
    {
        if (worldStateManager == null)
            worldStateManager = WorldStateManager.Instance != null ? WorldStateManager.Instance : FindFirstObjectByType<WorldStateManager>();
    }

    // ---------- JSON Extraction ----------

    private static string ExtractFirstJsonObject(string s)
    {
        int start = s.IndexOf('{');
        if (start < 0) return null;

        int depth = 0;
        for (int i = start; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return s.Substring(start, i - start + 1);
            }
        }

        return null;
    }

    private struct FlagOp { public string key; public string op; public float value; }
    private struct FactionOp { public string factionId; public string op; public float value; public string text; }
    private struct LocationOp { public string locationId; public string op; public float value; public string valueText; public string text; }
    private struct RegionStyleOp { public string regionId; public string styleKey; public string reason; }
}
