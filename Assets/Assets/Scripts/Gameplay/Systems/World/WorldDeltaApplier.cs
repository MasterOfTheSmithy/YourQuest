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

    [Header("Refs")]
    public WorldStateManager worldStateManager;

    void Awake()
    {
        if (worldStateManager == null)
            worldStateManager = FindFirstObjectByType<WorldStateManager>();
    }

    public bool TryApply(string raw, out string error)
    {
        error = null;

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

        if (flags.Count > maxFlags) flags.RemoveRange(maxFlags, flags.Count - maxFlags);
        if (factions.Count > maxFactions) factions.RemoveRange(maxFactions, factions.Count - maxFactions);
        if (locations.Count > maxLocations) locations.RemoveRange(maxLocations, locations.Count - maxLocations);

        if (flags.Count == 0 && factions.Count == 0 && locations.Count == 0)
        {
            Debug.Log($"[WorldDeltaApplier] NO-OP delta (ignored): {rationale}");
            error = "No-op delta (empty ops).";
            return false;
        }

        ApplyToWorldState(flags, factions, locations, confidence, rationale);
        return true;
    }

    private void ApplyToWorldState(
        List<FlagOp> flags,
        List<FactionOp> factions,
        List<LocationOp> locations,
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

        ws.lastLLMRationale = rationale;
        ws.lastLLMConfidence = confidence;

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
            if (!IsOpValid(op)) continue;

            list.Add(new FlagOp { key = key, op = op, value = val });
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
            float val = SafeFloat(o["value"]);
            string text = (o.Value<string>("text") ?? "").Trim();

            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!IsOpValid(op)) continue;

            list.Add(new FactionOp { factionId = id, op = op, value = val, text = text });
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
            float val = SafeFloat(o["value"]);
            string valueText = (o.Value<string>("valueText") ?? "").Trim();
            string text = (o.Value<string>("text") ?? "").Trim();

            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!IsOpValid(op)) continue;

            list.Add(new LocationOp { locationId = id, op = op, value = val, valueText = valueText, text = text });
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

    private static bool IsOpValid(string op)
    {
        if (string.IsNullOrWhiteSpace(op)) return false;
        op = op.Trim().ToLowerInvariant();
        return op == "add" || op == "set" || op == "mul";
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
}
