// C:\Users\Garri\YourQuest\Assets\Assets\Scripts\Dialogue\NpcDialogueAgent.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcDialogueAgent : MonoBehaviour
{
    [Header("Identity (fallbacks to EntityInfo)")]
    public string npcId = "npc_unknown";
    public string npcName = "NPC";

    [Tooltip("Optional override tags. If empty, uses EntityInfo.tags.")]
    public List<string> tagsOverride = new List<string>();

    [Header("Runtime")]
    [Tooltip("Max lines kept in memory.")]
    public int maxMemoryLines = 24;

    private EntityInfo _entityInfo;

    private readonly List<(string speaker, string text)> _recent = new List<(string speaker, string text)>(32);

    public string NpcId => !string.IsNullOrWhiteSpace(npcId) ? npcId : (_entityInfo != null ? _entityInfo.entityId : "npc_unknown");
    public string NpcName => !string.IsNullOrWhiteSpace(npcName) ? npcName : (_entityInfo != null ? _entityInfo.displayName : "NPC");

    public string LastNpcLine { get; private set; }

    public string TagsCsv
    {
        get
        {
            var tags = GetTags();
            if (tags == null || tags.Count == 0) return "<none>";
            return string.Join(", ", tags);
        }
    }

    private void Awake()
    {
        _entityInfo = GetComponentInChildren<EntityInfo>();
        if (_entityInfo != null)
        {
            if (string.IsNullOrWhiteSpace(npcId)) npcId = _entityInfo.entityId;
            if (string.IsNullOrWhiteSpace(npcName)) npcName = _entityInfo.displayName;
        }
    }

    public void SendPlayerMessage(string playerText, Action<string> onNpcReplyText)
    {
        CommitPlayerLine(playerText);

        if (DialogueThinkService.Instance == null)
        {
            string fallback = BuildFallback(playerText);
            CommitNpcLine(fallback);
            onNpcReplyText?.Invoke(fallback);
            return;
        }

        DialogueThinkService.Instance.RequestNpcReply(this, playerText, onNpcReplyText);
    }

    public void CommitPlayerLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Append("player", text.Trim());
    }

    public void CommitNpcLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) text = "<no response>";
        text = text.Trim();

        LastNpcLine = text;
        Append("npc", text);
    }

    private void Append(string speaker, string text)
    {
        _recent.Add((speaker, text));
        while (_recent.Count > maxMemoryLines)
            _recent.RemoveAt(0);
    }

    public string RenderRecentDialogue(int maxLines = 10)
    {
        int take = Mathf.Clamp(maxLines, 1, 50);

        var sb = new StringBuilder(512);
        int start = Mathf.Max(0, _recent.Count - take);

        for (int i = start; i < _recent.Count; i++)
        {
            var (speaker, text) = _recent[i];
            sb.Append(speaker).Append(": ").Append(text).Append('\n');
        }

        if (_recent.Count == 0)
            sb.AppendLine("<none>");

        return sb.ToString();
    }

    private List<string> GetTags()
    {
        if (tagsOverride != null && tagsOverride.Count > 0)
            return tagsOverride;

        if (_entityInfo != null && _entityInfo.tags != null && _entityInfo.tags.Length > 0)
        {
            var list = new List<string>(_entityInfo.tags.Length);
            for (int i = 0; i < _entityInfo.tags.Length; i++)
            {
                var t = _entityInfo.tags[i];
                if (string.IsNullOrWhiteSpace(t)) continue;
                list.Add(t.Trim());
            }
            return list;
        }

        return null;
    }

    // -------- Hard fallbacks (keeps it from feeling “lobotomized” when model fails) --------

    public string BuildFallback(string playerMessage)
    {
        // Keep it rude/pompous by default if tags suggest it.
        bool rude = TagsCsv.IndexOf("rude", StringComparison.OrdinalIgnoreCase) >= 0
                    || TagsCsv.IndexOf("pompous", StringComparison.OrdinalIgnoreCase) >= 0;

        if (rude)
        {
            if (IsThreat(playerMessage))
                return "Try it, then. Or are you only brave in your own head?";
            return "Speak clearly, or don't speak at all.";
        }

        return "What is it?";
    }

    public string BuildAntiRepeatVariant(string playerMessage)
    {
        if (IsThreat(playerMessage))
            return "Keep posturing. It changes nothing—except how willing I am to tolerate you.";

        return "I already answered. Are you slow, or just pretending?";
    }

    public string BuildForbiddenPhraseRecovery(string playerMessage)
    {
        if (IsThreat(playerMessage))
            return "Watch your mouth. You're not intimidating—you're just loud.";

        return "I’m not your helper. Say what you want, properly.";
    }

    private bool IsThreat(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.ToLowerInvariant();
        return s.Contains("kill") || s.Contains("attack") || s.Contains("die") || s.Contains("hurt");
    }
}
