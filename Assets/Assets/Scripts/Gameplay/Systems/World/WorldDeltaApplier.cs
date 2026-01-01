// WorldDeltaApplier.cs
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
    public int maxFlags = 6;
    public int maxFactions = 4;
    public int maxLocations = 4;

    [Header("Refs")]
    public WorldStateManager worldStateManager;

    private void Awake()
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
        if (rationale.Length > 240) rationale = rationale.Substring(0, 240);

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
            error = "No-op delta (all ops empty after normalization).";
            return false;
        }

        try
        {
            foreach (var f in flags)
            {
                switch (f.op)
                {
                    case "set": worldStateManager.SetFlag(f.key, f.value); break;
                    case "inc": worldStateManager.IncFlag(f.key, f.value); break;
                    case "dec": worldStateManager.IncFlag(f.key, -f.value); break;
                }
            }

            foreach (var fa in factions)
            {
                switch (fa.op)
                {
                    case "attitude_set":
                        worldStateManager.SetFactionAttitude(fa.factionId, fa.value);
                        break;
                    case "attitude_inc":
                        worldStateManager.IncFactionAttitude(fa.factionId, fa.value);
                        break;
                    case "status_set":
                        // status_set uses text
                        worldStateManager.SetFactionStatus(fa.factionId, fa.text);
                        break;
                }
            }

            foreach (var lo in locations)
            {
                switch (lo.op)
                {
                    case "state_set":
                        worldStateManager.SetLocationState(lo.locationId, lo.valueText);
                        break;
                    case "importance_set":
                        worldStateManager.SetLocationImportance(lo.locationId, lo.value);
                        break;
                    case "importance_inc":
                        worldStateManager.IncLocationImportance(lo.locationId, lo.value);
                        break;
                }

                // Optional narrative text hook (if provided)
                if (!string.IsNullOrWhiteSpace(lo.text))
                    worldStateManager.SetLocationText(lo.locationId, lo.text);
            }

            worldStateManager.Save();

            Debug.Log($"[WorldDeltaApplier] Applied delta: {rationale}");
            return true;
        }
        catch (Exception ex)
        {
            error = "Apply failed: " + ex.Message;
            return false;
        }
    }

    private float NormalizeConfidence(JToken tok)
    {
        float c = 0f;

        if (tok == null) return 0f;

        if (tok.Type == JTokenType.Float || tok.Type == JTokenType.Integer)
            c = tok.Value<float>();
        else if (tok.Type == JTokenType.String)
            float.TryParse(tok.Value<string>(), out c);

        if (c > 1f && c <= 100f) c /= 100f;
        return Mathf.Clamp01(c);
    }

    private void NormalizeArrayField(JObject root, string field)
    {
        if (root[field] == null)
        {
            root[field] = new JArray();
            return;
        }

        if (root[field] is JObject obj)
        {
            root[field] = new JArray(obj);
            return;
        }

        if (root[field] is JArray) return;

        root[field] = new JArray();
    }

    private List<FlagOp> SanitizeFlags(JArray arr)
    {
        var list = new List<FlagOp>(8);
        if (arr == null) return list;

        for (int i = 0; i < arr.Count; i++)
        {
            var t = arr[i];
            if (t.Type == JTokenType.String) continue;
            if (t is not JObject o) continue;

            string key = (o.Value<string>("key") ?? "").Trim();
            string op = (o.Value<string>("op") ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(key)) continue;
            if (op != "set" && op != "inc" && op != "dec") continue;

            float value = ReadFloat(o["value"]);
            if (float.IsNaN(value) || float.IsInfinity(value)) continue;

            value = Mathf.Clamp(value, -1000f, 1000f);
            list.Add(new FlagOp { key = key, op = op, value = value });
        }

        return list;
    }

    private List<FactionOp> SanitizeFactions(JArray arr)
    {
        var list = new List<FactionOp>(6);
        if (arr == null) return list;

        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i] is not JObject o) continue;

            string factionId = (o.Value<string>("factionId") ?? "").Trim();
            string op = (o.Value<string>("op") ?? "").Trim().ToLowerInvariant();
            float value = ReadFloat(o["value"]);
            string text = (o.Value<string>("text") ?? "").Trim();

            if (string.IsNullOrWhiteSpace(factionId)) continue;
            if (op != "attitude_set" && op != "attitude_inc" && op != "status_set") continue;

            if (op == "status_set")
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                list.Add(new FactionOp { factionId = factionId, op = op, value = 0f, text = text });
                continue;
            }

            if (float.IsNaN(value) || float.IsInfinity(value)) continue;
            value = Mathf.Clamp(value, -1f, 1f);

            list.Add(new FactionOp { factionId = factionId, op = op, value = value, text = text });
        }

        return list;
    }

    private List<LocationOp> SanitizeLocations(JArray arr)
    {
        var list = new List<LocationOp>(6);
        if (arr == null) return list;

        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i] is not JObject o) continue;

            string locationId = (o.Value<string>("locationId") ?? "").Trim();
            string op = (o.Value<string>("op") ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(locationId)) continue;
            if (op != "state_set" && op != "importance_set" && op != "importance_inc") continue;

            string narrativeText = (o.Value<string>("text") ?? "").Trim();

            if (op == "state_set")
            {
                string valText = (o.Value<string>("value") ?? o.Value<string>("valueText") ?? "").Trim();
                if (string.IsNullOrWhiteSpace(valText)) continue;

                list.Add(new LocationOp
                {
                    locationId = locationId,
                    op = op,
                    value = 0f,
                    valueText = valText,
                    text = narrativeText
                });
                continue;
            }

            float value = ReadFloat(o["value"]);
            if (float.IsNaN(value) || float.IsInfinity(value)) continue;
            value = Mathf.Clamp(value, -1000f, 1000f);

            list.Add(new LocationOp
            {
                locationId = locationId,
                op = op,
                value = value,
                valueText = "",
                text = narrativeText
            });
        }

        return list;
    }

    private float ReadFloat(JToken tok)
    {
        if (tok == null) return 0f;
        if (tok.Type == JTokenType.Float || tok.Type == JTokenType.Integer) return tok.Value<float>();
        if (tok.Type == JTokenType.String && float.TryParse(tok.Value<string>(), out float v)) return v;
        return 0f;
    }

    private string ExtractFirstJsonObject(string s)
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
