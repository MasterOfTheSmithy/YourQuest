// C:\Users\Garri\YourQuest\Assets\Assets\Scripts\Dialogue\DialogueThinkService.cs
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

public sealed class DialogueThinkService : MonoBehaviour
{
    public static DialogueThinkService Instance { get; private set; }

    [Header("Refs")]
    public SituationSnapshotBuilder situationSnapshotBuilder;

    [Header("Model Controls (Dialogue)")]
    [Tooltip("Lower temperature reduces rambling; too low can feel robotic.")]
    [Range(0.1f, 1.2f)] public float temperature = 0.85f;

    [Tooltip("Lower top_p reduces randomness; too low repeats.")]
    [Range(0.1f, 1.0f)] public float topP = 0.9f;

    [Tooltip("Encourages not repeating phrases.")]
    [Range(1.0f, 2.5f)] public float repeatPenalty = 1.25f;

    [Header("Repeat Guards")]
    [Tooltip("How many recent NPC lines to consider when rejecting repeats.")]
    [Range(2, 24)] public int recentNpcLinesToCheck = 10;

    [Tooltip("How many recent Player lines to consider for anti-echo.")]
    [Range(2, 24)] public int recentPlayerLinesToCheck = 10;

    [Tooltip("If similarity >= this, treat as repetition even if not exact match.")]
    [Range(0.5f, 1.0f)] public float similarityRejectThreshold = 0.86f;

    [Header("Safety / Behavior")]
    public bool logPrompt = true;
    public bool logRaw = true;

    [TextArea(3, 10)]
    public string[] forbiddenPhrases =
    {
        "I am sorry",
        "I do not have that information",
        "How may I assist you",
        "on this fine day",
        "Hello traveler",
        "I am here to help",
        "How can I help",
        "Greetings"
    };

    // Stronger than forbiddenPhrases: these are concept-stems that indicate assistant/therapy tone.
    // Applied only when the NPC is NOT tagged friendly/kind.
    [Header("Anti-Assistant Stems (tag-gated)")]
    public string[] forbiddenStemsUnlessFriendly =
    {
        "sorry", "apolog", "i apologize", "i'm sorry", "i am sorry",
        "assist", "help", "here to help", "how can i help", "how may i assist",
        "would you like to talk", "what's bothering you", "are you okay",
        "i cannot provide", "i'm afraid i cannot", "i do not have access",
        "as an ai", "as a language model"
    };

    [Serializable]
    private sealed class DialogueReply
    {
        public string npcText;
        public string action;
        public float confidence;
    }

    private static readonly string[] FallbackNonRepeatVariants_Rude =
    {
        "Try again—this time, in coherent words.",
        "Are you going to say something useful, or keep making noises?",
        "Speak plainly. I’m not paid to interpret your tantrums.",
        "You’re very loud for someone with so little to say.",
        "If you want my attention, earn it."
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (situationSnapshotBuilder == null)
            situationSnapshotBuilder = FindFirstObjectByType<SituationSnapshotBuilder>();
    }

    public void RequestNpcReply(NpcDialogueAgent agent, string playerMessage, Action<string> onNpcText)
    {
        if (agent == null)
        {
            onNpcText?.Invoke("<no agent>");
            return;
        }

        playerMessage ??= string.Empty;

        string prompt = BuildPrompt(agent, playerMessage);

        if (logPrompt)
            Debug.Log("[DialogueThinkService] PROMPT\n" + prompt);

        var opts = new Dictionary<string, object>
        {
            { "temperature", temperature },
            { "top_p", topP },
            { "repeat_penalty", repeatPenalty },
            { "repeat_last_n", 256 },
        };

        if (LLMClient.Instance == null)
        {
            string fallback = ChooseNonRepeatingFallback(agent, playerMessage);
            agent.CommitNpcLine(fallback);
            onNpcText?.Invoke(fallback);
            return;
        }

        LLMClient.Instance.Enqueue(prompt, raw =>
        {
            if (logRaw)
                Debug.Log($"[LLMClient] Raw (Dialogue:{agent.NpcId}):\n{(raw ?? "<null>")}");

            if (TryParseReply(raw, out var reply))
            {
                if (TryFinalize(agent, playerMessage, reply.npcText, out var final))
                {
                    agent.CommitNpcLine(final);
                    onNpcText?.Invoke(final);
                    return;
                }

                // One regen pass with explicit anti-echo/anti-repeat constraints + anti-assistant constraint.
                string regenPrompt = BuildRegeneratePrompt(agent, playerMessage, reply.npcText);
                if (logPrompt) Debug.Log("[DialogueThinkService] REGEN PROMPT\n" + regenPrompt);

                LLMClient.Instance.Enqueue(regenPrompt, raw2 =>
                {
                    if (logRaw)
                        Debug.Log($"[LLMClient] Raw (DialogueRegen:{agent.NpcId}):\n{(raw2 ?? "<null>")}");

                    if (TryParseReply(raw2, out var reply2) && TryFinalize(agent, playerMessage, reply2.npcText, out var final2))
                    {
                        agent.CommitNpcLine(final2);
                        onNpcText?.Invoke(final2);
                        return;
                    }

                    string fallback = ChooseNonRepeatingFallback(agent, playerMessage);
                    agent.CommitNpcLine(fallback);
                    onNpcText?.Invoke(fallback);

                }, debugTag: $"DialogueRegen:{agent.NpcId}", optionsOverride: opts);

                return;
            }

            string fallback0 = ChooseNonRepeatingFallback(agent, playerMessage);
            agent.CommitNpcLine(fallback0);
            onNpcText?.Invoke(fallback0);

        }, debugTag: $"Dialogue:{agent.NpcId}", optionsOverride: opts);
    }

    private bool TryParseReply(string raw, out DialogueReply reply)
    {
        reply = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        try
        {
            string trimmed = raw.Trim();

            if (trimmed.StartsWith("{"))
            {
                reply = JsonConvert.DeserializeObject<DialogueReply>(trimmed);
                return reply != null && !string.IsNullOrWhiteSpace(reply.npcText);
            }

            int start = trimmed.IndexOf('{');
            int end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                string slice = trimmed.Substring(start, end - start + 1);
                reply = JsonConvert.DeserializeObject<DialogueReply>(slice);
                return reply != null && !string.IsNullOrWhiteSpace(reply.npcText);
            }
        }
        catch { }

        return false;
    }

    private bool TryFinalize(NpcDialogueAgent agent, string playerMessage, string npcText, out string final)
    {
        final = null;

        npcText = PostProcessBasic(agent, npcText, playerMessage);
        npcText = npcText.Trim();

        if (string.IsNullOrWhiteSpace(npcText))
            return false;

        GetRecentLines(agent, out List<string> recentNpc, out List<string> recentPlayer);

        if (IsRepeatOfRecent(npcText, recentNpc))
            return false;

        if (IsEchoOfPlayer(npcText, playerMessage, recentPlayer))
            return false;

        if (IsNearDuplicate(npcText, recentNpc) || IsNearDuplicate(npcText, recentPlayer))
            return false;

        if (npcText.Length > 260)
            npcText = npcText.Substring(0, 260).TrimEnd() + "…";

        final = npcText;
        return true;
    }

    private string PostProcessBasic(NpcDialogueAgent agent, string npcText, string playerMessage)
    {
        bool allowWarmth = IsFriendly(agent);

        if (string.IsNullOrWhiteSpace(npcText))
            npcText = agent.BuildFallback(playerMessage);

        // 1) Exact forbidden phrases (legacy)
        if (forbiddenPhrases != null)
        {
            for (int i = 0; i < forbiddenPhrases.Length; i++)
            {
                string fp = forbiddenPhrases[i];
                if (string.IsNullOrWhiteSpace(fp)) continue;

                if (npcText.IndexOf(fp, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    npcText = agent.BuildForbiddenPhraseRecovery(playerMessage);
                    break;
                }
            }
        }

        // 2) Stem/token filter (strong)
        if (!allowWarmth && forbiddenStemsUnlessFriendly != null && forbiddenStemsUnlessFriendly.Length > 0)
        {
            string lower = npcText.ToLowerInvariant();
            for (int i = 0; i < forbiddenStemsUnlessFriendly.Length; i++)
            {
                string stem = forbiddenStemsUnlessFriendly[i];
                if (string.IsNullOrWhiteSpace(stem)) continue;

                if (lower.Contains(stem.ToLowerInvariant()))
                {
                    // Replace with a rude in-character recovery. Don’t keep “help/therapy” tone.
                    npcText = agent.BuildForbiddenPhraseRecovery(playerMessage);
                    break;
                }
            }
        }

        // 3) Tag clamp: if rude/pompous, strip overly polite “customer service” wrappers
        if (!allowWarmth && IsRudePersona(agent))
        {
            npcText = StripPoliteWrappers(npcText);
        }

        return npcText;
    }

    private bool IsFriendly(NpcDialogueAgent agent)
    {
        // Only allow apologizing/comfort language if explicitly tagged.
        return TagsContains(agent, "friendly") || TagsContains(agent, "kind") || TagsContains(agent, "gentle");
    }

    private bool IsRudePersona(NpcDialogueAgent agent)
    {
        return TagsContains(agent, "rude") || TagsContains(agent, "pompous") || TagsContains(agent, "pratty") || TagsContains(agent, "noble");
    }

    private static string StripPoliteWrappers(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;

        // Keep this intentionally conservative; we’re not doing heavy NLP.
        // We just remove a few “assistant voice” openers that survive stem filters.
        string t = s.Trim();

        string[] openers =
        {
            "well,",
            "well ",
            "i beg your pardon,",
            "i beg your pardon",
            "pardon,",
            "pardon",
            "certainly,",
            "certainly",
            "of course,",
            "of course",
            "i believe",
            "i must admit",
            "would you like to",
            "feel free to"
        };

        string lower = t.ToLowerInvariant();
        for (int i = 0; i < openers.Length; i++)
        {
            string op = openers[i];
            if (lower.StartsWith(op))
            {
                // Remove the opener and trim punctuation/space.
                t = t.Substring(op.Length).TrimStart(' ', '-', '—', ':', ',', ';');
                break;
            }
        }

        // If stripping produced emptiness, don’t nuke the line.
        return string.IsNullOrWhiteSpace(t) ? s : t;
    }

    private bool IsRepeatOfRecent(string candidate, List<string> recentNpcLines)
    {
        if (recentNpcLines == null || recentNpcLines.Count == 0) return false;

        string cNorm = Normalize(candidate);
        for (int i = 0; i < recentNpcLines.Count; i++)
        {
            string r = recentNpcLines[i];
            if (string.IsNullOrWhiteSpace(r)) continue;

            string rNorm = Normalize(r);
            if (cNorm == rNorm) return true;

            float sim = SimilarityRatio(cNorm, rNorm);
            if (sim >= similarityRejectThreshold) return true;
        }

        return false;
    }

    private bool IsEchoOfPlayer(string candidate, string playerMessage, List<string> recentPlayerLines)
    {
        string cNorm = Normalize(candidate);
        string pNorm = Normalize(playerMessage);

        if (!string.IsNullOrWhiteSpace(pNorm) && cNorm == pNorm)
            return true;

        if (recentPlayerLines != null)
        {
            for (int i = 0; i < recentPlayerLines.Count; i++)
            {
                var r = recentPlayerLines[i];
                if (string.IsNullOrWhiteSpace(r)) continue;
                if (Normalize(r) == cNorm) return true;

                float sim = SimilarityRatio(cNorm, Normalize(r));
                if (sim >= 0.90f) return true;
            }
        }

        return false;
    }

    private bool IsNearDuplicate(string candidate, List<string> lines)
    {
        if (lines == null || lines.Count == 0) return false;

        string c = Normalize(candidate);
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (string.IsNullOrWhiteSpace(l)) continue;
            if (SimilarityRatio(c, Normalize(l)) >= 0.92f) return true;
        }
        return false;
    }

    private void GetRecentLines(NpcDialogueAgent agent, out List<string> recentNpc, out List<string> recentPlayer)
    {
        recentNpc = new List<string>(recentNpcLinesToCheck);
        recentPlayer = new List<string>(recentPlayerLinesToCheck);

        string block = agent != null ? agent.RenderRecentDialogue(maxLines: Mathf.Max(recentNpcLinesToCheck, recentPlayerLinesToCheck)) : null;
        if (string.IsNullOrWhiteSpace(block)) return;

        var lines = block.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string ln = lines[i].Trim();
            if (ln.StartsWith("npc:", StringComparison.OrdinalIgnoreCase))
            {
                string t = ln.Substring(4).Trim();
                if (!string.IsNullOrWhiteSpace(t))
                    recentNpc.Add(t);
            }
            else if (ln.StartsWith("player:", StringComparison.OrdinalIgnoreCase))
            {
                string t = ln.Substring(7).Trim();
                if (!string.IsNullOrWhiteSpace(t))
                    recentPlayer.Add(t);
            }
        }

        if (recentNpc.Count > recentNpcLinesToCheck)
            recentNpc.RemoveRange(0, recentNpc.Count - recentNpcLinesToCheck);

        if (recentPlayer.Count > recentPlayerLinesToCheck)
            recentPlayer.RemoveRange(0, recentPlayer.Count - recentPlayerLinesToCheck);
    }

    private string ChooseNonRepeatingFallback(NpcDialogueAgent agent, string playerMessage)
    {
        GetRecentLines(agent, out var recentNpc, out _);

        string a = agent.BuildFallback(playerMessage);
        if (!IsRepeatOfRecent(a, recentNpc))
            return a;

        bool rude = IsRudePersona(agent);

        var pool = rude ? FallbackNonRepeatVariants_Rude : new[] { "What is it?", "Speak.", "Go on." };

        for (int tries = 0; tries < pool.Length; tries++)
        {
            string candidate = pool[UnityEngine.Random.Range(0, pool.Length)];
            if (!IsRepeatOfRecent(candidate, recentNpc))
                return candidate;
        }

        return a + " Now.";
    }

    private static bool TagsContains(NpcDialogueAgent agent, string token)
    {
        if (agent == null || string.IsNullOrWhiteSpace(token)) return false;
        string csv = agent.TagsCsv ?? string.Empty;
        return csv.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string BuildPrompt(NpcDialogueAgent agent, string playerMessage)
    {
        var sb = new StringBuilder(4096);

        sb.AppendLine("SYSTEM");
        sb.AppendLine("You are NOT an assistant. You are an NPC in a game world.");
        sb.AppendLine("Stay in-character. Tags are BINDING behavioral constraints.");
        sb.AppendLine("Return ONLY a single JSON object matching the schema. No markdown. No code fences. No backticks.");
        sb.AppendLine();

        sb.AppendLine("HARD VOICE RULES");
        sb.AppendLine("- Speak like a person, not customer support.");
        sb.AppendLine("- No apologies. No therapy. No offers to help.");
        sb.AppendLine("- If you don't know something, be sharp and interrogate or redirect.");
        sb.AppendLine("- If the player is abusive/threatening, respond to THAT.");
        sb.AppendLine("- Never echo the player's message.");
        sb.AppendLine("- Do not greet politely by default.");
        sb.AppendLine();

        sb.AppendLine("NPC");
        sb.AppendLine($"- npc_id: {agent.NpcId}");
        sb.AppendLine($"- npc_name: {agent.NpcName}");
        sb.AppendLine($"- tags: {agent.TagsCsv}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(agent.LastNpcLine))
        {
            sb.AppendLine("ANTI_REPEAT (hard)");
            sb.AppendLine($"- Do NOT repeat the last NPC line verbatim: \"{agent.LastNpcLine.Replace("\"", "'")}\"");
            sb.AppendLine();
        }

        sb.AppendLine("EXAMPLES (match tone to tags)");
        sb.AppendLine("Player: Howdy, how are ya?");
        sb.AppendLine("NPC JSON: {\"npcText\":\"Mm. Save the cheer for someone who asked. What do you want?\",\"action\":\"none\",\"confidence\":0.85}");
        sb.AppendLine("Player: Do you know where the nearest shop is?");
        sb.AppendLine("NPC JSON: {\"npcText\":\"Not in the library, obviously. Try outside—unless walking frightens you.\",\"action\":\"give_hint\",\"confidence\":0.85}");
        sb.AppendLine("Player: Do you know any legends?");
        sb.AppendLine("NPC JSON: {\"npcText\":\"Legends? Here? Ask a librarian. I'm not your bedtime storyteller.\",\"action\":\"give_hint\",\"confidence\":0.8}");
        sb.AppendLine("Player: Goodbye");
        sb.AppendLine("NPC JSON: {\"npcText\":\"Finally. Try not to get lost on the way out.\",\"action\":\"end_convo\",\"confidence\":0.85}");
        sb.AppendLine();

        sb.AppendLine("WORLD_SNAPSHOT (ground truth)");
        sb.AppendLine(BuildWorldSnapshotBlock());
        sb.AppendLine();

        if (situationSnapshotBuilder != null)
        {
            try
            {
                string sit = situationSnapshotBuilder.BuildSnapshot();
                if (!string.IsNullOrWhiteSpace(sit))
                {
                    sb.AppendLine("SITUATION_SNAPSHOT (ground truth)");
                    sb.AppendLine(sit);
                    sb.AppendLine();
                }
            }
            catch { }
        }

        sb.AppendLine("RECENT_DIALOGUE (most recent last)");
        sb.Append(agent.RenderRecentDialogue(maxLines: 12));
        sb.AppendLine();

        sb.AppendLine("PLAYER_MESSAGE");
        sb.AppendLine(playerMessage ?? "");
        sb.AppendLine();

        sb.AppendLine("OUTPUT_SCHEMA (ONLY this JSON object)");
        sb.AppendLine("{");
        sb.AppendLine("  \"npcText\": \"string\",");
        sb.AppendLine("  \"action\": \"none\" | \"open_shop\" | \"offer_quest\" | \"end_convo\" | \"mark_hostile\" | \"give_hint\",");
        sb.AppendLine("  \"confidence\": 0.0");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string BuildRegeneratePrompt(NpcDialogueAgent agent, string playerMessage, string rejectedNpcText)
    {
        GetRecentLines(agent, out var recentNpc, out var recentPlayer);

        var sb = new StringBuilder(2048);
        sb.AppendLine("Return ONLY one JSON object with fields npcText, action, confidence.");
        sb.AppendLine("Hard rules:");
        sb.AppendLine("- npcText must be NEW. Do NOT repeat/paraphrase any recent NPC line.");
        sb.AppendLine("- Do NOT echo/paraphrase the player's message.");
        sb.AppendLine("- No apologies. No offers to help. No therapy tone.");
        sb.AppendLine("- Stay in-character per tags: " + agent.TagsCsv);
        sb.AppendLine();

        sb.AppendLine("Player message:");
        sb.AppendLine(playerMessage ?? "");
        sb.AppendLine();

        sb.AppendLine("Recent NPC lines (do NOT reuse):");
        for (int i = 0; i < recentNpc.Count; i++)
            sb.AppendLine("- " + recentNpc[i]);
        sb.AppendLine();

        sb.AppendLine("Recent Player lines (do NOT echo):");
        for (int i = 0; i < recentPlayer.Count; i++)
            sb.AppendLine("- " + recentPlayer[i]);
        sb.AppendLine();

        sb.AppendLine("Rejected candidate (do NOT reuse):");
        sb.AppendLine(rejectedNpcText ?? "<null>");
        sb.AppendLine();

        sb.AppendLine("Schema:");
        sb.AppendLine("{\"npcText\":\"string\",\"action\":\"none\"|\"open_shop\"|\"offer_quest\"|\"end_convo\"|\"mark_hostile\"|\"give_hint\",\"confidence\":0.0}");

        return sb.ToString();
    }

    private string BuildWorldSnapshotBlock()
    {
        string worldName = "YourQuest";
        string regionId = PlayerContext.Instance != null && !string.IsNullOrWhiteSpace(PlayerContext.Instance.SemanticRegionId)
            ? PlayerContext.Instance.SemanticRegionId
            : "region_unknown";

        var sb = new StringBuilder(512);
        sb.AppendLine($"World: {worldName}  Region: {regionId}");
        sb.AppendLine();
        sb.AppendLine("CANON_LEDGER");
        sb.AppendLine("<none>");
        sb.AppendLine();
        sb.AppendLine("NPCS (region-relevant)");
        sb.AppendLine("<none>");
        return sb.ToString();
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        s = s.Trim().ToLowerInvariant();

        var sb = new StringBuilder(s.Length);
        bool lastWasSpace = false;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasSpace = false;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
        }

        return sb.ToString().Trim();
    }

    private static float SimilarityRatio(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0f;
        if (a == b) return 1f;

        var at = a.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var bt = b.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (at.Length == 0 || bt.Length == 0) return 0f;

        int overlap = 0;
        var used = new bool[bt.Length];
        for (int i = 0; i < at.Length; i++)
        {
            for (int j = 0; j < bt.Length; j++)
            {
                if (used[j]) continue;
                if (at[i] == bt[j])
                {
                    used[j] = true;
                    overlap++;
                    break;
                }
            }
        }

        float prec = overlap / Mathf.Max(1f, at.Length);
        float rec = overlap / Mathf.Max(1f, bt.Length);
        float denom = prec + rec;
        if (denom <= 0.0001f) return 0f;
        return (2f * prec * rec) / denom;
    }
}
