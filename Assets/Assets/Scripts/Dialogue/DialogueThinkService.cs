// Assets/Assets/Scripts/Dialogue/DialogueThinkService.cs
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueThinkService : MonoBehaviour
{
    public static DialogueThinkService Instance { get; private set; }

    [Header("Refs")]
    public SituationSnapshotBuilder situationSnapshotBuilder;

    [Header("Dialogue Generation")]
    [Range(0.1f, 1.0f)] public float temperature = 0.45f;
    [Range(0.1f, 1.0f)] public float topP = 0.82f;
    [Range(1.0f, 2.5f)] public float repeatPenalty = 1.2f;
    [Range(40, 280)] public int maxReplyCharacters = 220;
    [Range(0, 3)] public int malformedReplyRepairAttempts = 2;

    [Header("Repeat Guards")]
    [Range(2, 24)] public int recentNpcLinesToCheck = 8;
    [Range(2, 24)] public int recentPlayerLinesToCheck = 8;
    [Range(0.5f, 1.0f)] public float similarityRejectThreshold = 0.88f;

    [Header("Debug")]
    public bool logPrompt = false;
    public bool logRaw = false;

    [TextArea(3, 10)]
    public string[] forbiddenPhrases =
    {
        "I am sorry",
        "I do not have that information",
        "How may I assist you",
        "I am here to help",
        "How can I help",
        "As an AI",
        "language model",
        "player",
        "game",
        "quest generator",
        "LLM",
        "invalid reply",
        "prompt",
        "system"
    };

    [Serializable]
    private sealed class DialogueReply
    {
        public string npcText = string.Empty;
        public string action = "none";
        public float confidence = 0f;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (situationSnapshotBuilder == null)
            situationSnapshotBuilder = FindFirstObjectByType<SituationSnapshotBuilder>();
    }

    public void RequestNpcReply(NpcDialogueAgent agent, string playerMessage, Action<string> onNpcText)
    {
        if (agent == null || string.IsNullOrWhiteSpace(playerMessage))
        {
            onNpcText?.Invoke(null);
            return;
        }

        if (LLMClient.Instance == null)
        {
            onNpcText?.Invoke(null);
            return;
        }

        string trimmedMessage = playerMessage.Trim();
        string prompt = BuildPrompt(agent, trimmedMessage);
        if (logPrompt)
            Debug.Log("[DialogueThinkService] PROMPT\n" + prompt);

        // note: Dialogue is player-facing work and therefore takes priority over background world reactions.
        LLMClient.Instance.Submit(new YQLlmRequest
        {
            prompt = prompt,
            debugTag = "Dialogue:" + agent.NpcId,
            category = LLMGenerationCategory.Dialogue,
            priority = YQLlmRequestPriority.PlayerFacing,
            optionsOverride = BuildOptions(false)
        }, result => HandleReply(agent, trimmedMessage, result.success ? result.text : null, onNpcText, 0));
    }

    private void HandleReply(NpcDialogueAgent agent, string playerMessage, string raw, Action<string> onNpcText, int attempt)
    {
        if (logRaw)
            Debug.Log("[DialogueThinkService] RAW\n" + (raw ?? "<null>"));

        if (TryFinalizeReply(agent, playerMessage, raw, out string final))
        {
            onNpcText?.Invoke(final);
            return;
        }

        if (attempt < malformedReplyRepairAttempts && LLMClient.Instance != null)
        {
            string repairPrompt = BuildRepairPrompt(agent, playerMessage, raw);
            if (logPrompt)
                Debug.Log("[DialogueThinkService] REPAIR PROMPT\n" + repairPrompt);

            // note: One bounded repair keeps malformed dialogue from consuming an unbounded player-facing queue.
            LLMClient.Instance.Submit(new YQLlmRequest
            {
                prompt = repairPrompt,
                debugTag = "DialogueRepair:" + agent.NpcId,
                category = LLMGenerationCategory.Dialogue,
                priority = YQLlmRequestPriority.PlayerFacing,
                optionsOverride = BuildOptions(true),
                maxRetries = 0
            }, result => HandleReply(agent, playerMessage, result.success ? result.text : null, onNpcText, attempt + 1));
            return;
        }

        onNpcText?.Invoke(null);
    }

    private Dictionary<string, object> BuildOptions(bool repair)
    {
        return new Dictionary<string, object>
        {
            { "temperature", repair ? Mathf.Min(0.28f, temperature) : temperature },
            { "top_p", repair ? Mathf.Min(0.72f, topP) : topP },
            { "repeat_penalty", repeatPenalty },
            { "repeat_last_n", 192 },
            { "num_predict", repair ? 72 : 96 },
            { "stop", new[] { "\n\nPLAYER_MESSAGE:", "\n\nRECENT_DIALOGUE:", "```" } }
        };
    }

    private string BuildPrompt(NpcDialogueAgent agent, string playerMessage)
    {
        StringBuilder sb = new StringBuilder(4096);
        string snapshot = situationSnapshotBuilder != null ? situationSnapshotBuilder.BuildSnapshot() : "{}";
        string recent = agent.RenderRecentDialogue(12).Trim();
        YQInvestorDirector director = FindFirstObjectByType<YQInvestorDirector>();
        string objective = director != null ? director.CurrentObjective : string.Empty;
        WorldStateManager wsm = WorldStateManager.Instance;
        string worldNote = wsm != null && wsm.State != null ? SafeLine(wsm.State.lastLLMRationale) : string.Empty;

        sb.AppendLine("You are writing exactly one spoken reply for a single NPC in a reactive fantasy RPG scene.");
        sb.AppendLine("Return JSON only: {\"npcText\":string,\"action\":\"none\",\"confidence\":0.0}");
        sb.AppendLine("Identity law:");
        sb.AppendLine("- You are ONLY this NPC. Do not narrate. Do not describe actions. Do not explain yourself.");
        sb.AppendLine("- The reply must sound like the NPC's actual job, temperament, faction, and local knowledge.");
        sb.AppendLine("- The NPC may be terse, suspicious, stern, scholarly, rude, practical, or directive depending on role tags.");
        sb.AppendLine("- Never speak like an assistant, tutorial, game system, or meta narrator.");
        sb.AppendLine("Hard rules:");
        sb.AppendLine("- Stay completely in character.");
        sb.AppendLine("- No stage directions. No narration. No speaker labels. No quotation marks around the spoken line.");
        sb.AppendLine("- Use 1 to 2 short sentences.");
        sb.AppendLine("- Answer the player's actual meaning, not just keywords.");
        sb.AppendLine("- The first sentence must directly answer or refuse the player's latest message.");
        sb.AppendLine("- If the NPC would not know something, refuse or redirect in character instead of sounding generic.");
        sb.AppendLine("- Local knowledge is strong inside this region and nearby roads, weaker a few towns away, and mostly rumor for distant nations.");
        sb.AppendLine("- Do not invent precise facts about distant cities, nations, rulers, or wars unless the snapshot or recent dialogue supports it.");
        sb.AppendLine("- Ground the reply in local place, recent dialogue, current tension, or the NPC's role.");
        sb.AppendLine("- The line must be specific enough that a human could tell which NPC said it.");
        sb.AppendLine();
        sb.AppendLine("NPC:");
        sb.Append(agent.BuildPersonaBlock());
        sb.AppendLine();
        sb.AppendLine("CURRENT_OBJECTIVE:");
        sb.AppendLine(string.IsNullOrWhiteSpace(objective) ? "<none>" : objective);
        sb.AppendLine();
        sb.AppendLine("WORLD_NOTE:");
        sb.AppendLine(string.IsNullOrWhiteSpace(worldNote) ? "<none>" : worldNote);
        sb.AppendLine();
        sb.AppendLine("SITUATION_SNAPSHOT:");
        sb.AppendLine(snapshot);
        sb.AppendLine();
        sb.AppendLine("RECENT_DIALOGUE:");
        sb.AppendLine(string.IsNullOrWhiteSpace(recent) ? "<none>" : recent);
        sb.AppendLine();
        sb.AppendLine("PLAYER_MESSAGE:");
        sb.AppendLine(playerMessage);
        sb.AppendLine();
        sb.AppendLine("JSON ONLY.");
        return sb.ToString();
    }

    private string BuildRepairPrompt(NpcDialogueAgent agent, string playerMessage, string raw)
    {
        StringBuilder sb = new StringBuilder(3072);
        sb.AppendLine("Repair the previous NPC dialogue output.");
        sb.AppendLine("Return JSON only: {\"npcText\":string,\"action\":\"none\",\"confidence\":0.0}");
        sb.AppendLine("Rules:");
        sb.AppendLine("- Keep exactly one spoken NPC reply.");
        sb.AppendLine("- The reply must directly answer or refuse the player's latest message.");
        sb.AppendLine("- No assistant language. No explanations. No code fences. No speaker labels.");
        sb.AppendLine("- Keep the voice strictly tied to this NPC's name, job, personality, and faction.");
        sb.AppendLine("- If the old output was malformed or meta, replace it with a concise valid in-world line.");
        sb.AppendLine("- Do not apologize.");
        sb.AppendLine();
        sb.AppendLine("NPC:");
        sb.Append(agent.BuildPersonaBlock());
        sb.AppendLine();
        sb.AppendLine("PLAYER_MESSAGE:");
        sb.AppendLine(playerMessage);
        sb.AppendLine();
        sb.AppendLine("BAD_OUTPUT:");
        sb.AppendLine(string.IsNullOrWhiteSpace(raw) ? "<null>" : raw);
        sb.AppendLine();
        sb.AppendLine("JSON ONLY.");
        return sb.ToString();
    }

    private bool TryFinalizeReply(NpcDialogueAgent agent, string playerMessage, string raw, out string final)
    {
        final = null;

        string candidate = null;
        DialogueReply parsed;
        if (TryParseReply(raw, out parsed))
        {
            candidate = parsed.npcText;
            if (parsed.confidence > 0f && parsed.confidence < 0.15f)
                return false;
        }
        else if (!string.IsNullOrWhiteSpace(raw))
        {
            candidate = ExtractLooseText(raw);
        }

        candidate = PostProcess(candidate);
        if (string.IsNullOrWhiteSpace(candidate))
            return false;
        if (ContainsForbiddenPhrase(candidate) || BreaksFourthWall(candidate) || IsTooGeneric(candidate))
            return false;
        if (IsRepeatOrEcho(agent, playerMessage, candidate))
            return false;

        final = candidate;
        return true;
    }

    private bool TryParseReply(string raw, out DialogueReply reply)
    {
        reply = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            string json = ExtractFirstJsonObject(raw);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            reply = JsonConvert.DeserializeObject<DialogueReply>(json);
            return reply != null && !string.IsNullOrWhiteSpace(reply.npcText);
        }
        catch
        {
            return false;
        }
    }

    private string PostProcess(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Trim();
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();

        if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
            text = text.Substring(1, text.Length - 2).Trim();

        text = StripStageDirections(text);
        text = CollapseWhitespace(text);

        if (text.Length > maxReplyCharacters)
            // note: LiberationSans SDF lacks the Unicode ellipsis glyph; ASCII avoids a warning/rebuild loop in transcript ScrollRects.
            text = text.Substring(0, maxReplyCharacters).TrimEnd() + "...";

        return text;
    }

    private bool IsRepeatOrEcho(NpcDialogueAgent agent, string playerMessage, string candidate)
    {
        string normCandidate = Normalize(candidate);
        if (string.IsNullOrWhiteSpace(normCandidate))
            return true;
        if (Normalize(playerMessage) == normCandidate)
            return true;

        List<string> recentNpc = agent.GetRecentNpcLines(recentNpcLinesToCheck);
        for (int i = 0; i < recentNpc.Count; i++)
        {
            string other = Normalize(recentNpc[i]);
            if (string.IsNullOrWhiteSpace(other))
                continue;
            if (other == normCandidate || SimilarityRatio(other, normCandidate) >= similarityRejectThreshold)
                return true;
        }

        List<string> recentPlayer = agent.GetRecentPlayerLines(recentPlayerLinesToCheck);
        for (int i = 0; i < recentPlayer.Count; i++)
        {
            string other = Normalize(recentPlayer[i]);
            if (string.IsNullOrWhiteSpace(other))
                continue;
            if (other == normCandidate || SimilarityRatio(other, normCandidate) >= 0.92f)
                return true;
        }

        return false;
    }

    private bool ContainsForbiddenPhrase(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || forbiddenPhrases == null)
            return false;

        for (int i = 0; i < forbiddenPhrases.Length; i++)
        {
            string phrase = forbiddenPhrases[i];
            if (!string.IsNullOrWhiteSpace(phrase) && text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool BreaksFourthWall(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string lower = text.ToLowerInvariant();
        return lower.Contains("in this game") ||
               lower.Contains("dialogue option") ||
               lower.Contains("quest giver") ||
               lower.Contains("language model") ||
               lower.Contains("llm") ||
               lower.Contains("invalid reply") ||
               lower.Contains("prompt") ||
               lower.Contains("system");
    }

    private static bool IsTooGeneric(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;

        string lower = text.Trim().ToLowerInvariant();
        return lower == "go on." ||
               lower == "go on" ||
               lower == "speak plainly." ||
               lower == "speak plainly" ||
               lower == "ask cleanly and i'll answer cleanly." ||
               lower == "talk like a person." ||
               lower == "then speak plainly.";
    }

    private static string ExtractLooseText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string trimmed = raw.Trim();
        trimmed = trimmed.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            return string.Empty;
        return trimmed;
    }

    private static string ExtractFirstJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        int start = raw.IndexOf('{');
        if (start < 0)
            return string.Empty;

        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = start; i < raw.Length; i++)
        {
            char c = raw[i];
            if (inString)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = false;
                    continue;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return raw.Substring(start, i - start + 1);
            }
        }

        return string.Empty;
    }

    private static string StripStageDirections(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Replace("*", string.Empty);

        while (true)
        {
            int open = text.IndexOf('[');
            int close = text.IndexOf(']');
            if (open < 0 || close <= open)
                break;
            text = text.Remove(open, close - open + 1);
        }

        while (true)
        {
            int open = text.IndexOf('(');
            int close = text.IndexOf(')');
            if (open < 0 || close <= open)
                break;
            text = text.Remove(open, close - open + 1);
        }

        return text.Trim();
    }

    private static string CollapseWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        StringBuilder sb = new StringBuilder(text.Length);
        bool lastSpace = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool isSpace = char.IsWhiteSpace(c);
            if (isSpace)
            {
                if (lastSpace)
                    continue;
                sb.Append(' ');
                lastSpace = true;
            }
            else
            {
                sb.Append(c);
                lastSpace = false;
            }
        }
        return sb.ToString().Trim();
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        return CollapseWhitespace(StripStageDirections(text)).ToLowerInvariant();
    }

    private static float SimilarityRatio(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return 0f;
        if (a == b)
            return 1f;

        int maxLen = Mathf.Max(a.Length, b.Length);
        if (maxLen <= 0)
            return 1f;

        int dist = LevenshteinDistance(a, b);
        return 1f - (float)dist / maxLen;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Mathf.Min(Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[a.Length, b.Length];
    }

    private static string SafeLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        value = value.Replace('\n', ' ').Replace('\r', ' ');
        return CollapseWhitespace(value);
    }
}
