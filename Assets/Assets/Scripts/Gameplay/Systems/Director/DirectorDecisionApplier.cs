// FILE: Assets/Assets/Scripts/Gameplay/Systems/Director/DirectorDecisionApplier.cs

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Applies the Director LLM JSON decision to game state.
/// Now supports "allowedDecisions" gating so the LLM cannot force progression/world
/// when the ThinkCycle says it's not justified.
/// </summary>
public class DirectorDecisionApplier : MonoBehaviour
{
    [Header("Refs")]
    public BalanceConfig balanceConfig;

    [Header("World")]
    public WorldDeltaApplier worldDeltaApplier;

    [Header("Progression")]
    public ProgressionDecisionApplier progressionDecisionApplier;

    [Header("Debug")]
    public bool logRejects = true;

    public bool TryApplyDirectorJson(string raw, out string applied, out string reason)
    {
        // Backwards-compatible default: allow all.
        return TryApplyDirectorJson(raw, out applied, out reason, allowedDecisions: null);
    }

    /// <summary>
    /// allowedDecisions: null => allow all. Otherwise must include decision string.
    /// Valid strings: "none", "world", "progression"
    /// </summary>
    public bool TryApplyDirectorJson(string raw, out string applied, out string reason, HashSet<string> allowedDecisions)
    {
        applied = "none";
        reason = "";

        if (string.IsNullOrWhiteSpace(raw))
        {
            reason = "Empty response.";
            return false;
        }

        string cleaned = StripCodeFences(raw).Trim();

        if (!TryExtractFirstJsonObject(cleaned, out string jsonText))
        {
            reason = "Could not find a JSON object in response.";
            if (logRejects) Debug.LogWarning("[DirectorDecisionApplier] Reject: " + reason + "\nRAW:\n" + raw);
            return false;
        }

        JObject root;
        try
        {
            root = JObject.Parse(jsonText);
        }
        catch (Exception e)
        {
            reason = "JSON parse failed: " + e.Message;
            if (logRejects) Debug.LogWarning("[DirectorDecisionApplier] Reject: " + reason + "\nJSON:\n" + jsonText);
            return false;
        }

        string decision = ((string)root["decision"] ?? "none").Trim().ToLowerInvariant();

        // --- HARD GATE ---
        if (allowedDecisions != null && !allowedDecisions.Contains(decision))
        {
            reason = $"Decision '{decision}' is not allowed right now. Allowed: [{string.Join(", ", allowedDecisions)}]";
            if (logRejects) Debug.LogWarning("[DirectorDecisionApplier] Reject: " + reason + "\nJSON:\n" + jsonText);
            return false;
        }

        if (decision == "none")
        {
            applied = "none";
            reason = (string)root["reason"] ?? "none";
            return true;
        }

        if (decision == "world")
        {
            JObject payload = root["payload"] as JObject;
            JObject worldDelta = payload?["worldDelta"] as JObject;

            if (worldDelta == null)
            {
                reason = "decision=world but payload.worldDelta missing.";
                if (logRejects) Debug.LogWarning("[DirectorDecisionApplier] Reject: " + reason + "\nJSON:\n" + jsonText);
                return false;
            }

            // FIX #1: WorldDeltaApplier expects string (your error: JObject -> string)
            string worldDeltaJson = worldDelta.ToString(Formatting.None);

            // FIX #2: Definite assignment: do NOT declare out vars inside a short-circuit expression
            string wReason = "";
            bool ok = false;

            if (worldDeltaApplier != null)
            {
                ok = worldDeltaApplier.TryApply(worldDeltaJson, out wReason);
            }
            else
            {
                wReason = "WorldDeltaApplier ref is null.";
            }

            applied = "world";
            reason = ok ? wReason : ("World apply failed: " + wReason);
            return ok;
        }

        if (decision == "progression")
        {
            JObject payload = root["payload"] as JObject;
            JObject progression = payload?["progression"] as JObject;

            if (progression == null)
            {
                reason = "decision=progression but payload.progression missing.";
                if (logRejects) Debug.LogWarning("[DirectorDecisionApplier] Reject: " + reason + "\nJSON:\n" + jsonText);
                return false;
            }

            // Keep your pipeline consistent: progression applier takes raw JSON string
            string progJson = progression.ToString(Formatting.None);

            // FIX #3: Definite assignment again
            string progApplied = "progression";
            string pReason = "";
            bool ok = false;

            if (progressionDecisionApplier != null)
            {
                ok = progressionDecisionApplier.TryApply(progJson, out progApplied, out pReason);
            }
            else
            {
                pReason = "ProgressionDecisionApplier ref is null.";
            }

            applied = ok ? progApplied : "progression";
            reason = ok ? pReason : ("Progression apply failed: " + pReason);
            return ok;
        }

        reason = $"Unknown decision '{decision}'.";
        if (logRejects) Debug.LogWarning("[DirectorDecisionApplier] Reject: " + reason + "\nJSON:\n" + jsonText);
        return false;
    }

    private static string StripCodeFences(string s)
    {
        // Removes ```json ... ``` wrappers if present, without being fragile.
        // Also handles triple backticks without language.
        s = s.Replace("\r\n", "\n");

        if (s.Contains("```"))
        {
            // Remove all fence markers and language tags.
            // Example: ```json\n{...}\n```
            s = s.Replace("```json", "");
            s = s.Replace("```JSON", "");
            s = s.Replace("```", "");
        }

        return s;
    }

    private static bool TryExtractFirstJsonObject(string text, out string json)
    {
        json = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        int start = text.IndexOf('{');
        if (start < 0) return false;

        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inString = false; continue; }
                continue;
            }
            else
            {
                if (c == '"') { inString = true; continue; }

                if (c == '{') depth++;
                else if (c == '}') depth--;

                if (depth == 0)
                {
                    json = text.Substring(start, i - start + 1);
                    return true;
                }
            }
        }

        return false;
    }
}
