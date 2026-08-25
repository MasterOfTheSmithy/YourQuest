// Assets/Assets/Scripts/Dialogue/NpcDialogueAgent.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcDialogueAgent : MonoBehaviour
{
    [Header("Identity (fallbacks to EntityInfo)")]
    public string npcId = string.Empty;
    public string npcName = string.Empty;
    [TextArea(2, 5)] public string personaSummary = "A grounded in-world NPC with practical knowledge of the surrounding region.";
    public List<string> tagsOverride = new List<string>();
    [Min(4)] public int maxMemoryLines = 160;
    public bool persistTranscriptAcrossSessions = true;

    private EntityInfo _entityInfo;
    private NpcDialogueSession _session;
    private bool _sessionLoaded;
    private string _loadedSessionNpcId = string.Empty;

    public string NpcId => !string.IsNullOrWhiteSpace(npcId) ? npcId.Trim() : (_entityInfo != null ? _entityInfo.entityId : "npc_unknown");
    public string NpcName => !string.IsNullOrWhiteSpace(npcName) ? npcName.Trim() : (_entityInfo != null ? _entityInfo.displayName : "NPC");
    public string LastNpcLine { get; private set; }
    public bool IsThinking { get; private set; }
    public string TagsCsv => string.Join(", ", GetTags());

    // note: Presentation listens to the authoritative persisted session instead of guessing when an async reply changed it.
    public event Action TranscriptChanged;

    private void Awake()
    {
        ResolveIdentity();
        EnsureSessionLoaded();
    }

    private void Start()
    {
        ResolveIdentity();
        RebindSessionIfNeeded();
    }

    public void RefreshIdentityAndSession()
    {
        ResolveIdentity();
        RebindSessionIfNeeded();
    }

    public void SendPlayerMessage(string playerText, Action<string> onNpcReplyText)
    {
        if (string.IsNullOrWhiteSpace(playerText))
        {
            onNpcReplyText?.Invoke(null);
            return;
        }

        string trimmed = playerText.Trim();
        CommitPlayerLine(trimmed);
        IsThinking = true;

        DialogueThinkService service = DialogueThinkService.Instance;
        if (service == null)
        {
            CommitNpcLine(BuildFallback(trimmed));
            IsThinking = false;
            onNpcReplyText?.Invoke(LastNpcLine);
            return;
        }

        service.RequestNpcReply(this, trimmed, npcReply =>
        {
            IsThinking = false;
            string final = string.IsNullOrWhiteSpace(npcReply) ? null : npcReply.Trim();
            if (string.IsNullOrWhiteSpace(final))
                final = BuildFallback(trimmed);
            if (!string.IsNullOrWhiteSpace(final))
                CommitNpcLine(final);
            onNpcReplyText?.Invoke(LastNpcLine);
        });
    }

    public void CommitPlayerLine(string text)
    {
        AppendTurn("player", text);
    }

    public void CommitNpcLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        LastNpcLine = text.Trim();
        AppendTurn("npc", LastNpcLine);
    }

    public void ClearRecent()
    {
        EnsureSessionLoaded();
        _session.recentTurns.Clear();
        LastNpcLine = string.Empty;
        IsThinking = false;
        SaveSession();
        TranscriptChanged?.Invoke();
    }

    public string RenderRecentDialogue(int maxLines = 10)
    {
        List<DialogueTurn> turns = GetRecentTurnsSnapshot(maxLines);
        if (turns.Count == 0)
            return "<none>\n";

        StringBuilder sb = new StringBuilder(256);
        for (int i = 0; i < turns.Count; i++)
        {
            DialogueTurn turn = turns[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text))
                continue;

            sb.Append(string.IsNullOrWhiteSpace(turn.speaker) ? "npc" : turn.speaker);
            sb.Append(": ");
            sb.Append(turn.text);
            sb.Append('\n');
        }

        return sb.ToString();
    }

    public List<DialogueTurn> GetRecentTurnsSnapshot(int maxTurns = 24)
    {
        EnsureSessionLoaded();
        int take = Mathf.Clamp(maxTurns, 1, 256);
        List<DialogueTurn> result = new List<DialogueTurn>(take);
        int count = _session.recentTurns != null ? _session.recentTurns.Count : 0;
        int start = Mathf.Max(0, count - take);

        for (int i = start; i < count; i++)
        {
            DialogueTurn turn = _session.recentTurns[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text))
                continue;

            result.Add(new DialogueTurn
            {
                speaker = string.IsNullOrWhiteSpace(turn.speaker) ? "npc" : turn.speaker.Trim().ToLowerInvariant(),
                text = turn.text.Trim()
            });
        }

        return result;
    }

    public IReadOnlyList<DialogueTurn> GetRecentTurnsReadOnly()
    {
        EnsureSessionLoaded();
        return _session.recentTurns;
    }

    public List<string> GetRecentNpcLines(int maxLines)
    {
        return GetRecentBySpeaker("npc", maxLines);
    }

    public List<string> GetRecentPlayerLines(int maxLines)
    {
        return GetRecentBySpeaker("player", maxLines);
    }

    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        List<string> tags = GetTags();
        for (int i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public string GetPrimaryRoleLabel()
    {
        if (HasTag("merchant")) return "merchant";
        if (HasTag("warden") || HasTag("guard")) return "warden";
        if (HasTag("lorekeeper") || HasTag("scholar")) return "lorekeeper";
        if (HasTag("quest_giver") || HasTag("guide") || HasTag("mentor")) return "guide";
        if (HasTag("healer")) return "healer";
        return "local resident";
    }

    public string BuildPersonaBlock()
    {
        StringBuilder sb = new StringBuilder(640);
        sb.AppendLine("name: " + NpcName);
        sb.AppendLine("id: " + NpcId);
        sb.AppendLine("job: " + GetPrimaryRoleLabel());
        if (!string.IsNullOrWhiteSpace(personaSummary))
            sb.AppendLine("persona: " + personaSummary.Trim());

        List<string> tags = GetTags();
        sb.AppendLine("tags: " + (tags.Count > 0 ? string.Join(", ", tags) : "<none>"));

        if (_entityInfo != null)
        {
            sb.AppendLine("faction: " + Safe(_entityInfo.factionId, "none"));
            sb.AppendLine("hostility: " + _entityInfo.hostility);
            sb.AppendLine("level: " + _entityInfo.level);
        }

        sb.AppendLine("tone_rules: " + BuildToneRules());
        sb.AppendLine("identity_rules: Always speak as " + NpcName + ", the " + GetPrimaryRoleLabel() + ". Never sound like narration, UI, or an assistant.");
        return sb.ToString();
    }

    public string BuildFallback(string playerMessage)
    {
        string lower = (playerMessage ?? string.Empty).ToLowerInvariant();
        bool asksFarPlace = lower.Contains("nation") || lower.Contains("empire") || lower.Contains("capital") || lower.Contains("across the sea") || lower.Contains("far away");
        bool asksNearby = lower.Contains("town") || lower.Contains("village") || lower.Contains("road") || lower.Contains("forest") || lower.Contains("region") || lower.Contains("where");

        if (lower.Contains("who") || lower.Contains("name"))
            return "I'm " + NpcName + ". Around here, that name is enough.";
        if (lower.Contains("quest") || lower.Contains("job") || lower.Contains("work"))
            return HasTag("guide") || HasTag("quest_giver") ? "If you want work, prove you can survive the ground under your boots first." : "Work comes from people who trust you. I am not there yet.";
        if (asksFarPlace)
            return "That is beyond my roads. I can give you rumors, not truth.";
        if (asksNearby)
            return HasTag("scholar") || HasTag("lorekeeper") ? "The nearby paths are known. Ask me about this region and I can be precise." : "Close roads, I know. Distant banners, no.";
        if (lower.Contains("help") || lower.Contains("advice"))
            return HasTag("warden") || HasTag("rude") || HasTag("stern") ? "Keep your guard up and stop asking the world to be gentle." : "Start with what is in reach, then let the road teach you the rest.";
        if (HasTag("warden") || HasTag("rude") || HasTag("stern"))
            return "Ask plainly. I will answer what I actually know.";
        return "I heard you. Ask it closer to the ground we are standing on.";
    }

    private string BuildToneRules()
    {
        if (HasTag("warden") || HasTag("stern") || HasTag("rude"))
            return "Blunt, disciplined, short answers, no softness, no jokes.";
        if (HasTag("lorekeeper") || HasTag("scholar"))
            return "Precise, calm, grounded, informative without breaking character.";
        if (HasTag("guide") || HasTag("quest_giver") || HasTag("mentor"))
            return "Practical, directive, concise, always points toward action.";
        if (HasTag("merchant"))
            return "Transactional, sharp, practical, never poetic.";
        return "Grounded, human, concise, in-world.";
    }

    private void ResolveIdentity()
    {
        if (_entityInfo == null)
        {
            _entityInfo = GetComponent<EntityInfo>();
            if (_entityInfo == null)
                _entityInfo = GetComponentInChildren<EntityInfo>();
        }

        if (_entityInfo != null)
        {
            if (string.IsNullOrWhiteSpace(npcId) || string.Equals(npcId, "npc_unknown", StringComparison.OrdinalIgnoreCase))
                npcId = _entityInfo.entityId;
            if (string.IsNullOrWhiteSpace(npcName) || string.Equals(npcName, "NPC", StringComparison.OrdinalIgnoreCase))
                npcName = _entityInfo.displayName;
        }

        if (string.IsNullOrWhiteSpace(npcId))
            npcId = "npc_unknown";
        if (string.IsNullOrWhiteSpace(npcName))
            npcName = "NPC";
    }

    private void RebindSessionIfNeeded()
    {
        string currentId = NpcId;
        if (!_sessionLoaded)
            return;
        if (string.Equals(_loadedSessionNpcId, currentId, StringComparison.OrdinalIgnoreCase))
            return;

        _sessionLoaded = false;
        _session = null;
        LastNpcLine = string.Empty;
        EnsureSessionLoaded();
    }

    private void AppendTurn(string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        EnsureSessionLoaded();
        _session.npcEntityId = NpcId;
        _session.maxTurns = Mathf.Clamp(maxMemoryLines, 4, 256);
        _session.recentTurns ??= new List<DialogueTurn>(16);
        _session.recentTurns.Add(new DialogueTurn
        {
            speaker = string.IsNullOrWhiteSpace(speaker) ? "npc" : speaker.Trim().ToLowerInvariant(),
            text = text.Trim()
        });

        int overflow = _session.recentTurns.Count - _session.maxTurns;
        if (overflow > 0)
            _session.recentTurns.RemoveRange(0, overflow);

        SaveSession();
        TranscriptChanged?.Invoke();
    }

    private List<string> GetRecentBySpeaker(string speaker, int maxLines)
    {
        EnsureSessionLoaded();
        List<string> output = new List<string>(Mathf.Clamp(maxLines, 1, 32));
        if (_session.recentTurns == null)
            return output;

        for (int i = _session.recentTurns.Count - 1; i >= 0 && output.Count < maxLines; i--)
        {
            DialogueTurn turn = _session.recentTurns[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text))
                continue;
            if (string.Equals(turn.speaker, speaker, StringComparison.OrdinalIgnoreCase))
                output.Add(turn.text.Trim());
        }

        output.Reverse();
        return output;
    }

    private void EnsureSessionLoaded()
    {
        ResolveIdentity();
        string currentId = NpcId;
        if (_sessionLoaded && string.Equals(_loadedSessionNpcId, currentId, StringComparison.OrdinalIgnoreCase))
            return;

        _sessionLoaded = true;
        _loadedSessionNpcId = currentId;

        if (persistTranscriptAcrossSessions && NpcDialogueSessionStore.TryLoad(currentId, out NpcDialogueSession loaded) && loaded != null)
        {
            _session = loaded;
            _session.npcEntityId = currentId;
            _session.maxTurns = Mathf.Clamp(maxMemoryLines, 4, 256);
            _session.recentTurns ??= new List<DialogueTurn>(16);
        }
        else
        {
            _session = new NpcDialogueSession
            {
                npcEntityId = currentId,
                maxTurns = Mathf.Clamp(maxMemoryLines, 4, 256),
                recentTurns = new List<DialogueTurn>(16)
            };
        }

        LastNpcLine = string.Empty;
        for (int i = _session.recentTurns.Count - 1; i >= 0; i--)
        {
            DialogueTurn turn = _session.recentTurns[i];
            if (turn != null && string.Equals(turn.speaker, "npc", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(turn.text))
            {
                LastNpcLine = turn.text.Trim();
                break;
            }
        }
    }

    private void SaveSession()
    {
        if (!persistTranscriptAcrossSessions || _session == null)
            return;

        _session.npcEntityId = NpcId;
        _session.maxTurns = Mathf.Clamp(maxMemoryLines, 4, 256);
        _session.recentTurns ??= new List<DialogueTurn>(16);
        NpcDialogueSessionStore.TrySave(NpcId, _session);
    }

    private List<string> GetTags()
    {
        List<string> tags = new List<string>();

        if (tagsOverride != null)
        {
            for (int i = 0; i < tagsOverride.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(tagsOverride[i]))
                    tags.Add(tagsOverride[i].Trim());
            }
        }

        if (tags.Count == 0 && _entityInfo != null && _entityInfo.tags != null)
        {
            for (int i = 0; i < _entityInfo.tags.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(_entityInfo.tags[i]))
                    tags.Add(_entityInfo.tags[i].Trim());
            }
        }

        return tags;
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
