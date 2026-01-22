// C:\Users\Garri\YourQuest\Assets\Assets\Scripts\Dialogue\NpcDialogueRoleRuleset.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "NpcDialogueRoleRuleset", menuName = "Emergent/Dialogue Role Ruleset")]
public sealed class NpcDialogueRoleRuleset : ScriptableObject
{
    [Serializable]
    public sealed class RoleRule
    {
        [Tooltip("Role tag that may appear on EntityInfo.tags, e.g. merchant, guard, scholar, bandit")]
        public string roleTag = "commoner";

        [TextArea(3, 10)]
        [Tooltip("Hard constraints for this role. Example: 'Do not reveal faction secrets. Prefer short practical answers.'")]
        public string constraints;

        [TextArea(2, 8)]
        [Tooltip("Style guidance. Example: 'Blunt, clipped sentences. Uses local slang.'")]
        public string style;

        [Tooltip("Allowed action ids this role can propose (the system may ignore them). Example: open_shop, offer_quest")]
        public string[] allowedActions;
    }

    [Header("Fallback (used when no roleTag matches)")]
    public RoleRule fallback = new RoleRule
    {
        roleTag = "commoner",
        constraints = "Be grounded. Do not invent world-changing facts. If unsure, admit uncertainty.",
        style = "Natural conversational tone. Keep answers brief.",
        allowedActions = new[] { "none" }
    };

    [Header("Role Rules")]
    public RoleRule[] rules;

    private readonly Dictionary<string, RoleRule> _cache = new Dictionary<string, RoleRule>(StringComparer.OrdinalIgnoreCase);

    private void OnEnable() => RebuildCache();
    private void OnValidate() => RebuildCache();

    private void RebuildCache()
    {
        _cache.Clear();
        if (rules == null) return;

        for (int i = 0; i < rules.Length; i++)
        {
            var r = rules[i];
            if (r == null) continue;
            if (string.IsNullOrWhiteSpace(r.roleTag)) continue;
            _cache[r.roleTag.Trim()] = r;
        }
    }

    public RoleRule Resolve(string[] npcTags)
    {
        if (npcTags != null)
        {
            for (int i = 0; i < npcTags.Length; i++)
            {
                var t = npcTags[i];
                if (string.IsNullOrWhiteSpace(t)) continue;

                if (_cache.TryGetValue(t.Trim(), out var rule) && rule != null)
                    return rule;
            }
        }

        return fallback ?? new RoleRule { roleTag = "commoner", constraints = "", style = "", allowedActions = new[] { "none" } };
    }

    public static string RenderAllowedActions(string[] allowed)
    {
        if (allowed == null || allowed.Length == 0) return "none";
        var sb = new StringBuilder(128);
        for (int i = 0; i < allowed.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(string.IsNullOrWhiteSpace(allowed[i]) ? "none" : allowed[i].Trim());
        }
        return sb.ToString();
    }
}
