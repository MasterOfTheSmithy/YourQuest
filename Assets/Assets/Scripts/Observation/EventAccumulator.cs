// Assets/Assets/Scripts/Observation/EventAccumulator.cs

using System;
using System.Collections.Generic;
using UnityEngine;

public class EventAccumulator : MonoBehaviour
{
    public static EventAccumulator Instance { get; private set; }

    private readonly List<ActionEvent> actionEvents = new();

    private readonly List<EmergentSkill> ghostSkills = new();       // normal drafts
    private readonly List<EmergentSkill> upgradeCandidates = new(); // drafts tied to an existing skill
    private readonly List<SkillData> committedSkills = new();       // committed (optional)

    [Header("Upgrade Matching")]
    [Range(0f, 1f)]
    public float strongMatchThreshold = SkillSimilarity.STRONG_MATCH;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RecordEvent(ActionEvent ev) => actionEvents.Add(ev);
    public void AddEvent(ActionEvent ev) => RecordEvent(ev);

    public IReadOnlyList<ActionEvent> GetEvents() => actionEvents;

    /// <summary>
    /// Clears buffered action events after you’ve applied a progression decision,
    /// so you don’t double-award off the same evidence.
    /// </summary>
    public void ClearEvents() => actionEvents.Clear();

    /// <summary>
    /// Returns skills that were committed/applied to the player.
    /// Used for upgrade matching / replacement offers.
    /// </summary>
    public IReadOnlyList<SkillData> GetCommittedSkills() => committedSkills;

    /// <summary>
    /// Removes events with UnixTime strictly less than cutoffUnix.
    /// Use this after rolling them into a long-term ledger.
    /// </summary>
    public int PruneEventsBeforeUnix(long cutoffUnix)
    {
        int removed = 0;

        // Remove from back (safe even if list order isn’t perfect)
        for (int i = actionEvents.Count - 1; i >= 0; i--)
        {
            var e = actionEvents[i];
            if (e == null) { actionEvents.RemoveAt(i); removed++; continue; }
            if (e.UnixTime < cutoffUnix)
            {
                actionEvents.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    public void AddCommittedSkill(SkillData skill)
    {
        if (skill == null) return;
        committedSkills.Add(skill);
        Debug.Log($"[EventAccumulator] Committed skill added: {skill.skillName} (Tier {skill.tier})");
    }

    /// <summary>
    /// Adds a draft as either:
    /// - a normal ghost skill, or
    /// - an upgrade candidate linked to an existing committed skill
    /// </summary>
    public void AddGhostSkillOrUpgradeCandidate(EmergentSkill draft)
    {
        if (draft == null) return;

        // Try match against committed skills
        SkillData best = null;
        float bestScore = 0f;

        for (int i = 0; i < committedSkills.Count; i++)
        {
            var c = committedSkills[i];
            if (c == null) continue;

            float score = SkillSimilarity.Score(
                draft.skillName, draft.description, draft.contextTags,
                c.skillName, c.description, c.tags
            );

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        if (best != null && bestScore >= strongMatchThreshold)
        {
            draft.upgradeTargetSkillId = best.skillId;
            upgradeCandidates.Add(draft);
            Debug.Log($"[EventAccumulator] Draft classified as UPGRADE candidate for '{best.skillName}' (score={bestScore:0.00})");
            return;
        }

        ghostSkills.Add(draft);
        Debug.Log($"[EventAccumulator] Draft stored as new ghost skill '{draft.skillName}' (score={bestScore:0.00})");
    }

    public IReadOnlyList<EmergentSkill> GetGhostSkills() => ghostSkills;
    public IReadOnlyList<EmergentSkill> GetUpgradeCandidates() => upgradeCandidates;

    public void ClearGhostSkills() => ghostSkills.Clear();
    public void ClearUpgradeCandidates() => upgradeCandidates.Clear();
}
