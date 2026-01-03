using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Builds the Director prompt (world snapshot + player snapshot + action/behavior ledgers + triggers).
/// Also determines allowed decisions based on computed evidence/triggers.
/// 
/// This intentionally avoids "EXAMPLE" blocks, which cause models to copy placeholder values ("string")
/// and re-introduce code fences. We instead give a strict schema contract.
/// </summary>
public class DirectorPromptBuilder : MonoBehaviour
{
    [Header("Refs (optional but recommended)")]
    public WorldStateManager worldStateManager;
    public PlayerStateManager playerStateManager;

    [Tooltip("Provides a compact ground-truth snapshot JSON string (and/or supporting info).")]
    public SituationSnapshotBuilder situationSnapshotBuilder;

    [Tooltip("Holds aggregated action counts/significance.")]
    public EventAccumulator eventAccumulator;

    [Header("Director Settings")]
    [TextArea(2, 10)]
    public string canonLedger = "Canon: Magic exists. The world responds to witnessed acts. Factions compete for relics.";

    [Tooltip("If world significance >= this, world changes become eligible.")]
    public float worldSignificanceThreshold = 4.0f;

    [Tooltip("If progression score normalized >= this, progression becomes eligible.")]
    [Range(0f, 1f)]
    public float progressionThreshold = 0.75f;

    [Tooltip("If true, we allow 'none' always. Recommended.")]
    public bool alwaysAllowNone = true;

    [Header("Output Budget")]
    [Tooltip("Trims long sections to help smaller models not melt. Still keeps full schema rules.")]
    public int maxNotesChars = 1200;

    [Header("Debug")]
    public bool logBuiltPrompt = false;

    // Cached last-computed gating info
    private TriggerSnapshot _lastTriggers;

    /// <summary>
    /// DirectorThinkCycle calls this to build the full prompt for the LLM.
    /// </summary>
    public string BuildDirectorPrompt()
    {
        var sb = new StringBuilder(4096);

        // -------- World Snapshot --------
        AppendHeader(sb, "WORLD_SNAPSHOT");
        string worldName = "YourQuest";
        string regionId = TryGetRegionId(out string rid) ? rid : "region_unknown";
        string regionName = TryGetRegionName(out string rname) ? rname : "Unknown";

        sb.AppendLine($"World: {worldName}  Region: {regionId}");
        sb.AppendLine();

        AppendHeader(sb, "CANON_LEDGER");
        sb.AppendLine(string.IsNullOrWhiteSpace(canonLedger) ? "Canon: <unset>" : canonLedger.Trim());
        sb.AppendLine();

        // -------- Global Flags / Factions --------
        // If your WorldState has richer structures, wire them in here.
        AppendHeader(sb, "GLOBAL_FLAGS (top)");
        float tension = TryGetWorldTension(out float t) ? t : 0f;
        sb.AppendLine($"- tension = {tension:0.00}");
        sb.AppendLine();

        AppendHeader(sb, "FACTIONS (notable)");
        AppendFactions(sb);
        sb.AppendLine();

        AppendHeader(sb, "LOCATIONS (region-relevant)");
        AppendLocations(sb, regionId);
        sb.AppendLine();

        AppendHeader(sb, "NPCS (region-relevant)");
        AppendNPCs(sb, regionId);
        sb.AppendLine();

        // -------- Player Snapshot --------
        AppendHeader(sb, "PLAYER_SNAPSHOT");
        AppendPlayerSnapshot(sb, regionId);
        sb.AppendLine();

        // -------- Recent Actions / Situation Snapshot --------
        AppendHeader(sb, "RECENT_ACTIONS_SUMMARY");
        AppendSituationSnapshot(sb);
        sb.AppendLine();
        AppendRecentActionsSummary(sb);
        sb.AppendLine();

        // -------- Behavior Ledger --------
        AppendHeader(sb, "BEHAVIOR_LEDGER");
        AppendBehaviorLedger(sb);
        sb.AppendLine();

        // -------- Triggers / Gating --------
        _lastTriggers = ComputeTriggers(regionId);
        AppendHeader(sb, "TRIGGERS");
        sb.AppendLine($"- world_total_significance: {_lastTriggers.worldTotalSignificance:0.00} (threshold {worldSignificanceThreshold:0.00}) => candidate={_lastTriggers.worldCandidate}");
        sb.AppendLine($"- progression_score_raw: {_lastTriggers.progressionScoreRaw:0.00}");
        sb.AppendLine($"- progression_score_normalized: {_lastTriggers.progressionScoreNormalized:0.00} (threshold {progressionThreshold:0.00}) => candidate={_lastTriggers.progressionCandidate}");
        sb.AppendLine($"- progression_dominant_verb: {_lastTriggers.dominantVerb}");
        sb.AppendLine($"- progression_dominant_region: {_lastTriggers.dominantRegion}");
        sb.AppendLine();

        // -------- Task / Rules / Schema --------
        AppendHeader(sb, "TASK");
        sb.AppendLine("You are the System Director for an offline single-player RPG.");
        sb.AppendLine("Choose EXACTLY ONE decision: none | world | progression.");
        sb.AppendLine("Return ONLY one JSON object. No markdown. No code fences. No extra blocks.");
        sb.AppendLine();

        AppendHeader(sb, "HARD RULES");
        sb.AppendLine("- Output MUST be valid JSON.");
        sb.AppendLine("- Output MUST contain exactly these top-level keys: decision, confidence, reason, payload.");
        sb.AppendLine("- decision MUST be one of: \"none\", \"world\", \"progression\".");
        sb.AppendLine("- confidence MUST be a number 0.0 to 1.0.");
        sb.AppendLine("- reason MUST be a short string grounded in SITUATION_SNAPSHOT evidence.");
        sb.AppendLine("- payload MUST be an object ({}).");
        sb.AppendLine("- If decision=\"world\", payload MUST be {\"worldDelta\":{...}} exactly.");
        sb.AppendLine("- If decision=\"progression\", payload MUST be {\"progression\":{...}} exactly.");
        sb.AppendLine("- Do NOT include any keys not specified by the schema.");
        sb.AppendLine("- Do NOT include any example text, comments, or explanations outside JSON.");
        sb.AppendLine();

        AppendHeader(sb, "EVIDENCE RULES");
        sb.AppendLine("- Use SITUATION_SNAPSHOT as PRIMARY evidence.");
        sb.AppendLine("- If snapshot contradicts summaries/ledger, trust snapshot.");
        sb.AppendLine("- Keep changes proportional. No god-gifts.");
        sb.AppendLine();

        AppendHeader(sb, "DECISION GUIDANCE");
        sb.AppendLine("- Choose world when the world should react (mood/flags/spawns/notes).");
        sb.AppendLine("- Choose progression only when the player earned a skill/title/quest.");
        sb.AppendLine("- Choose none if nothing is justified.");
        sb.AppendLine();

        AppendHeader(sb, "OUTPUT_SCHEMA");
        sb.AppendLine("Return ONE JSON object matching ONE of the following shapes:");
        sb.AppendLine();
        sb.AppendLine("{\"decision\":\"none\",\"confidence\":0.0,\"reason\":\"...\",\"payload\":{}}");
        sb.AppendLine();
        sb.AppendLine("{\"decision\":\"world\",\"confidence\":0.0,\"reason\":\"...\",\"payload\":{\"worldDelta\":{\"sceneName\":\"...\",\"regionId\":\"...\",\"mood\":\"...\",\"flags\":{},\"spawns\":[],\"notes\":[]}}}");
        sb.AppendLine();
        sb.AppendLine("{\"decision\":\"progression\",\"confidence\":0.0,\"reason\":\"...\",\"payload\":{\"progression\":{\"decision\":\"skill|title|quest\",\"confidence\":0.0,\"reason\":\"...\",\"payload\":{}}}}");
        sb.AppendLine();
        sb.AppendLine("Additional progression constraints:");
        sb.AppendLine("- If progression.decision=\"skill\" => payload MUST include skillSeedName, skillType, hook.");
        sb.AppendLine("- If progression.decision=\"title\" => payload MUST include titleName (and only fields your game supports).");
        sb.AppendLine("- If progression.decision=\"quest\" => payload MUST include questId or questName (match your quest system).");
        sb.AppendLine();
        sb.AppendLine("Now output JSON only.");

        string prompt = sb.ToString();
        if (logBuiltPrompt) Debug.Log("[DirectorPromptBuilder] Built prompt:\n" + prompt);
        return prompt;
    }

    /// <summary>
    /// DirectorThinkCycle uses this to gate decisions before applying.
    /// null => allow all. Otherwise must contain allowed decisions.
    /// </summary>
    public HashSet<string> GetAllowedDecisions()
    {
        // If we haven't built yet, compute minimal triggers.
        if (_lastTriggers == null) _lastTriggers = ComputeTriggers(TryGetRegionId(out var rid) ? rid : "region_unknown");

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (alwaysAllowNone) allowed.Add("none");

        // Allow world/progression only if candidates.
        if (_lastTriggers.worldCandidate) allowed.Add("world");
        if (_lastTriggers.progressionCandidate) allowed.Add("progression");

        // If you're experimenting and want "allow all", return null instead.
        return allowed;
    }

    // =========================
    // Sections
    // =========================

    private void AppendPlayerSnapshot(StringBuilder sb, string regionId)
    {
        string playerName = TryGetPlayerName(out var pn) ? pn : "The Player";
        int level = TryGetPlayerLevel(out var lv) ? lv : 1;
        int xp = TryGetPlayerXP(out var xpVal) ? xpVal : 0;
        int xpToNext = TryGetPlayerXPToNext(out var xpNext) ? xpNext : 100;
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        Vector3 pos = TryGetPlayerPosition(out var p) ? p : Vector3.zero;

        sb.AppendLine($"Name: {playerName}");
        sb.AppendLine($"Level: {level}  XP: {xp}/{xpToNext} (to next {xpToNext})");
        sb.AppendLine($"Scene: {sceneName}  Region: {regionId}");
        sb.AppendLine($"Pos: [{pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}]");
        sb.AppendLine();

        AppendHeader(sb, "STATS");
        AppendStats(sb);
        sb.AppendLine();

        AppendHeader(sb, "EQUIPPED_SKILLS");
        AppendEquippedSkills(sb);
        sb.AppendLine();

        AppendHeader(sb, "SKILLS (top/highest tiers)");
        AppendTopSkills(sb);
        sb.AppendLine();

        AppendHeader(sb, "TITLES (recent)");
        AppendTitles(sb);
        sb.AppendLine();

        AppendHeader(sb, "QUESTS (active/offers)");
        AppendQuests(sb);
    }

    private void AppendSituationSnapshot(StringBuilder sb)
    {
        sb.AppendLine("SITUATION_SNAPSHOT (ground truth):");
        if (situationSnapshotBuilder != null)
        {
            // Prefer a method if you have one; fallback to reflection-safe ToString.
            // If your builder already stores the last JSON in a public property, wire it directly.
            string snap = TryGetSituationJson(out var sj) ? sj : null;
            if (!string.IsNullOrWhiteSpace(snap))
                sb.AppendLine(snap.Trim());
            else
                sb.AppendLine("{\"regId\":\"region_unknown\",\"regName\":\"Unknown\"}");
        }
        else
        {
            sb.AppendLine("{\"regId\":\"region_unknown\",\"regName\":\"Unknown\"}");
        }
        sb.AppendLine();
    }

    private void AppendRecentActionsSummary(StringBuilder sb)
    {
        // If you have a real summary pipeline, wire it. Otherwise give a compact list from accumulator.
        if (eventAccumulator == null)
        {
            sb.AppendLine("RECENT_ACTIONS_SUMMARY:");
            sb.AppendLine("<none>");
            return;
        }

        sb.AppendLine("RECENT_ACTIONS_SUMMARY:");
        // You likely have something like eventAccumulator.GetSummaryLines()
        // We’ll do a conservative approach: ask it for a string if you have it, otherwise <wired later>.
        string summary = TryGetAccumulatorSummary(out var s) ? s : null;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            sb.AppendLine(summary.Trim());
        }
        else
        {
            sb.AppendLine("<wired later: EventAccumulator summary unavailable>");
        }
    }

    private void AppendBehaviorLedger(StringBuilder sb)
    {
        if (eventAccumulator == null)
        {
            sb.AppendLine("Behavior Ledger (most impactful):");
            sb.AppendLine("<none>");
            return;
        }

        sb.AppendLine("Behavior Ledger (most impactful):");
        string ledger = TryGetAccumulatorLedger(out var l) ? l : null;
        if (!string.IsNullOrWhiteSpace(ledger))
        {
            sb.AppendLine(ledger.Trim());
        }
        else
        {
            sb.AppendLine("<wired later: EventAccumulator ledger unavailable>");
        }
    }

    private void AppendFactions(StringBuilder sb)
    {
        // Wire real factions when your WorldState has them.
        // For now: if WorldStateManager has a method/property, pull it; else keep minimal.
        string factions = TryGetWorldFactionsBlock(out var fb) ? fb : null;
        if (!string.IsNullOrWhiteSpace(factions))
        {
            sb.AppendLine(factions.Trim());
        }
        else
        {
            sb.AppendLine("<none>");
        }
    }

    private void AppendLocations(StringBuilder sb, string regionId)
    {
        string locations = TryGetWorldLocationsBlock(regionId, out var lb) ? lb : null;
        if (!string.IsNullOrWhiteSpace(locations))
        {
            sb.AppendLine(locations.Trim());
        }
        else
        {
            sb.AppendLine("<none>");
        }
    }

    private void AppendNPCs(StringBuilder sb, string regionId)
    {
        string npcs = TryGetWorldNPCsBlock(regionId, out var nb) ? nb : null;
        if (!string.IsNullOrWhiteSpace(npcs))
        {
            sb.AppendLine(npcs.Trim());
        }
        else
        {
            sb.AppendLine("<none>");
        }
    }

    // =========================
    // Trigger computation
    // =========================

    [Serializable]
    private class TriggerSnapshot
    {
        public float worldTotalSignificance;
        public bool worldCandidate;

        public float progressionScoreRaw;
        public float progressionScoreNormalized;
        public bool progressionCandidate;

        public string dominantVerb = "unknown";
        public string dominantRegion = "region_unknown";
    }

    private TriggerSnapshot ComputeTriggers(string regionId)
    {
        var ts = new TriggerSnapshot();

        // World significance should come from eventAccumulator significance totals.
        // In your logs you have "world_total_significance".
        ts.worldTotalSignificance = TryGetWorldTotalSignificance(out var wts) ? wts : 0f;
        ts.worldCandidate = ts.worldTotalSignificance >= worldSignificanceThreshold;

        // Progression scoring is your own system. If you already compute it elsewhere, wire it.
        // For now, use accumulator heuristic: dominant verb with high "skill-earning" weight.
        // IMPORTANT: keep default as 0 so you don't accidentally allow progression.
        ts.progressionScoreRaw = TryGetProgressionScoreRaw(out var psr) ? psr : 0f;

        // Normalization (clamp 0..1). If you have a true normalization, plug it in here.
        ts.progressionScoreNormalized = Mathf.Clamp01(ts.progressionScoreRaw);
        ts.progressionCandidate = ts.progressionScoreNormalized >= progressionThreshold;

        ts.dominantVerb = TryGetDominantVerb(out var dv) ? dv : "unknown";
        ts.dominantRegion = regionId;

        return ts;
    }

    // =========================
    // Utilities
    // =========================

    private static void AppendHeader(StringBuilder sb, string title)
    {
        sb.AppendLine(title);
    }

    // =========================
    // Safe getters / wiring points
    // =========================

    private bool TryGetRegionId(out string regionId)
    {
        regionId = null;
        if (worldStateManager == null) return false;

        // If your WorldStateManager exposes a current region id, wire it here.
        // Example: worldStateManager.WorldState.currentRegionId
        try
        {
            var ws = worldStateManager.GetWorldState();
            if (ws != null && !string.IsNullOrWhiteSpace(ws.currentRegionId))
            {
                regionId = ws.currentRegionId;
                return true;
            }
        }
        catch { }
        return false;
    }

    private bool TryGetRegionName(out string regionName)
    {
        regionName = null;
        if (worldStateManager == null) return false;

        try
        {
            var ws = worldStateManager.GetWorldState();
            if (ws != null && !string.IsNullOrWhiteSpace(ws.currentRegionName))
            {
                regionName = ws.currentRegionName;
                return true;
            }
        }
        catch { }
        return false;
    }

    private bool TryGetWorldTension(out float tension)
    {
        tension = 0f;
        if (worldStateManager == null) return false;

        try
        {
            var ws = worldStateManager.GetWorldState();
            if (ws != null)
            {
                tension = ws.tension;
                return true;
            }
        }
        catch { }
        return false;
    }

    private bool TryGetPlayerName(out string name)
    {
        name = null;
        if (playerStateManager == null) return false;

        try
        {
            var ps = playerStateManager.GetPlayerState();
            if (ps != null && !string.IsNullOrWhiteSpace(ps.playerName))
            {
                name = ps.playerName;
                return true;
            }
        }
        catch { }
        return false;
    }

    private bool TryGetPlayerLevel(out int level)
    {
        level = 1;
        if (playerStateManager == null) return false;

        try
        {
            var ps = playerStateManager.GetPlayerState();
            if (ps != null)
            {
                level = ps.level;
                return true;
            }
        }
        catch { }
        return false;
    }

    private bool TryGetPlayerXP(out int xp)
    {
        xp = 0;
        if (playerStateManager == null) return false;

        try
        {
            var ps = playerStateManager.GetPlayerState();
            if (ps != null)
            {
                xp = ps.xp;
                return true;
            }
        }
        catch { }
        return false;
    }

    private bool TryGetPlayerXPToNext(out int xpToNext)
    {
        xpToNext = 100;
        if (playerStateManager == null) return false;

        try
        {
            var ps = playerStateManager.GetPlayerState();
            if (ps != null)
            {
                xpToNext = ps.xpToNext;
                return true;
            }
        }
        catch { }
        return false;
    }

    private bool TryGetPlayerPosition(out Vector3 pos)
    {
        pos = Vector3.zero;

        // If PlayerStateManager tracks Transform, wire it. Otherwise try to find a tagged Player.
        try
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                pos = go.transform.position;
                return true;
            }
        }
        catch { }

        return false;
    }

    private void AppendStats(StringBuilder sb)
    {
        // If you have real stats in PlayerState, wire them.
        // Keeping this stable so the prompt format doesn't thrash.
        sb.AppendLine("STR 10 | DEX 10 | INT 10 | VIT 10");
        sb.AppendLine("HP 100 | STA 100 | MANA 50");
        sb.AppendLine("ATK 10 | DEF 5 | CRIT 0.05 | MS x5.00");
    }

    private void AppendEquippedSkills(StringBuilder sb)
    {
        sb.AppendLine("<none>");
    }

    private void AppendTopSkills(StringBuilder sb)
    {
        sb.AppendLine("<none>");
    }

    private void AppendTitles(StringBuilder sb)
    {
        sb.AppendLine("<none>");
    }

    private void AppendQuests(StringBuilder sb)
    {
        sb.AppendLine("<none>");
    }

    private bool TryGetSituationJson(out string json)
    {
        json = null;
        if (situationSnapshotBuilder == null) return false;

        // If your builder has a method like BuildSnapshotJson(), use it.
        // We'll look for common patterns via reflection to avoid hard dependency.
        try
        {
            var t = situationSnapshotBuilder.GetType();

            var m = t.GetMethod("BuildSnapshotJson");
            if (m != null)
            {
                json = m.Invoke(situationSnapshotBuilder, null) as string;
                return !string.IsNullOrWhiteSpace(json);
            }

            var p = t.GetProperty("LastSnapshot");
            if (p != null)
            {
                json = p.GetValue(situationSnapshotBuilder) as string;
                return !string.IsNullOrWhiteSpace(json);
            }
        }
        catch { }

        return false;
    }

    private bool TryGetAccumulatorSummary(out string summary)
    {
        summary = null;
        if (eventAccumulator == null) return false;

        try
        {
            var t = eventAccumulator.GetType();

            var m = t.GetMethod("GetRecentSummary");
            if (m != null)
            {
                summary = m.Invoke(eventAccumulator, null) as string;
                return !string.IsNullOrWhiteSpace(summary);
            }

            var p = t.GetProperty("RecentSummary");
            if (p != null)
            {
                summary = p.GetValue(eventAccumulator) as string;
                return !string.IsNullOrWhiteSpace(summary);
            }
        }
        catch { }

        return false;
    }

    private bool TryGetAccumulatorLedger(out string ledger)
    {
        ledger = null;
        if (eventAccumulator == null) return false;

        try
        {
            var t = eventAccumulator.GetType();

            var m = t.GetMethod("GetBehaviorLedger");
            if (m != null)
            {
                ledger = m.Invoke(eventAccumulator, null) as string;
                return !string.IsNullOrWhiteSpace(ledger);
            }

            var p = t.GetProperty("BehaviorLedger");
            if (p != null)
            {
                ledger = p.GetValue(eventAccumulator) as string;
                return !string.IsNullOrWhiteSpace(ledger);
            }
        }
        catch { }

        return false;
    }

    private bool TryGetWorldTotalSignificance(out float sig)
    {
        sig = 0f;
        if (eventAccumulator == null) return false;

        try
        {
            var t = eventAccumulator.GetType();

            var m = t.GetMethod("GetWorldTotalSignificance");
            if (m != null)
            {
                object o = m.Invoke(eventAccumulator, null);
                if (o is float f) { sig = f; return true; }
                if (o is double d) { sig = (float)d; return true; }
            }

            var p = t.GetProperty("worldTotalSignificance");
            if (p != null)
            {
                object o = p.GetValue(eventAccumulator);
                if (o is float f) { sig = f; return true; }
                if (o is double d) { sig = (float)d; return true; }
            }
        }
        catch { }

        return false;
    }

    private bool TryGetProgressionScoreRaw(out float scoreRaw)
    {
        scoreRaw = 0f;
        if (eventAccumulator == null) return false;

        // If you already compute progression scoring elsewhere, expose it and wire it here.
        try
        {
            var t = eventAccumulator.GetType();

            var m = t.GetMethod("GetProgressionScoreRaw");
            if (m != null)
            {
                object o = m.Invoke(eventAccumulator, null);
                if (o is float f) { scoreRaw = f; return true; }
                if (o is double d) { scoreRaw = (float)d; return true; }
            }

            var p = t.GetProperty("progressionScoreRaw");
            if (p != null)
            {
                object o = p.GetValue(eventAccumulator);
                if (o is float f) { scoreRaw = f; return true; }
                if (o is double d) { scoreRaw = (float)d; return true; }
            }
        }
        catch { }

        return false;
    }

    private bool TryGetDominantVerb(out string verb)
    {
        verb = "unknown";
        if (eventAccumulator == null) return false;

        try
        {
            var t = eventAccumulator.GetType();

            var m = t.GetMethod("GetDominantVerb");
            if (m != null)
            {
                verb = m.Invoke(eventAccumulator, null) as string;
                return !string.IsNullOrWhiteSpace(verb);
            }

            var p = t.GetProperty("dominantVerb");
            if (p != null)
            {
                verb = p.GetValue(eventAccumulator) as string;
                return !string.IsNullOrWhiteSpace(verb);
            }
        }
        catch { }

        return false;
    }

    private bool TryGetWorldFactionsBlock(out string block)
    {
        block = null;
        if (worldStateManager == null) return false;

        try
        {
            var m = worldStateManager.GetType().GetMethod("GetFactionsPromptBlock");
            if (m != null)
            {
                block = m.Invoke(worldStateManager, null) as string;
                return !string.IsNullOrWhiteSpace(block);
            }
        }
        catch { }

        return false;
    }

    private bool TryGetWorldLocationsBlock(string regionId, out string block)
    {
        block = null;
        if (worldStateManager == null) return false;

        try
        {
            var m = worldStateManager.GetType().GetMethod("GetLocationsPromptBlock");
            if (m != null)
            {
                block = m.Invoke(worldStateManager, new object[] { regionId }) as string;
                return !string.IsNullOrWhiteSpace(block);
            }
        }
        catch { }

        return false;
    }

    private bool TryGetWorldNPCsBlock(string regionId, out string block)
    {
        block = null;
        if (worldStateManager == null) return false;

        try
        {
            var m = worldStateManager.GetType().GetMethod("GetNPCsPromptBlock");
            if (m != null)
            {
                block = m.Invoke(worldStateManager, new object[] { regionId }) as string;
                return !string.IsNullOrWhiteSpace(block);
            }
        }
        catch { }

        return false;
    }
}
