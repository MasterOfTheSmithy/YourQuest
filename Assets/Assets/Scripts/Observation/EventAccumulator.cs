// Assets/Assets/Scripts/Observation/EventAccumulator.cs

using System;
using System.Collections.Generic;
using UnityEngine;

public class EventAccumulator : MonoBehaviour
{
    public static EventAccumulator Instance { get; private set; }

    private readonly List<ActionEvent> actionEvents = new();
    // note: Occurrence IDs distinguish duplicate event objects and remain stable when the buffer is pruned.
    private readonly List<long> eventOccurrences = new();
    private long nextEventOccurrence;

    public sealed class EventWindow
    {
        internal readonly long[] occurrences;
        public readonly IReadOnlyList<ActionEvent> events;
        public readonly string fingerprint;
        internal EventWindow(long[] ids, List<ActionEvent> captured)
        {
            occurrences = ids;
            events = captured.AsReadOnly();
            fingerprint = YQRepairEpisode.Hash(string.Join(",", ids));
        }
    }

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

    public void RecordEvent(ActionEvent ev)
    {
        actionEvents.Add(ev);
        eventOccurrences.Add(++nextEventOccurrence);
    }
    public void AddEvent(ActionEvent ev) => RecordEvent(ev);

    public IReadOnlyList<ActionEvent> GetEvents() => actionEvents;

    /// <summary>
    /// Clears buffered action events after you’ve applied a progression decision,
    /// so you don’t double-award off the same evidence.
    /// </summary>
    public void ClearEvents()
    {
        actionEvents.Clear();
        eventOccurrences.Clear();
    }

    public EventWindow CaptureWindow(int maximum)
    {
        // note: Freeze values and exact occurrence IDs before inference, retaining ownership in this accumulator.
        int start = Math.Max(0, actionEvents.Count - Math.Max(1, maximum));
        List<ActionEvent> captured = new List<ActionEvent>(actionEvents.Count - start);
        long[] ids = new long[actionEvents.Count - start];
        for (int i = start; i < actionEvents.Count; i++)
        {
            captured.Add(actionEvents[i]?.CopyForRequest());
            ids[i - start] = eventOccurrences[i];
        }
        return new EventWindow(ids, captured);
    }

    public void AcknowledgeWindow(EventWindow window)
    {
        if (window == null) return;
        // note: Only captured occurrences are consumed; later arrivals and duplicate-object occurrences survive.
        HashSet<long> captured = new HashSet<long>(window.occurrences);
        for (int i = actionEvents.Count - 1; i >= 0; i--)
        {
            if (!captured.Contains(eventOccurrences[i])) continue;
            actionEvents.RemoveAt(i);
            eventOccurrences.RemoveAt(i);
        }
    }

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
            if (e == null) { actionEvents.RemoveAt(i); eventOccurrences.RemoveAt(i); removed++; continue; }
            if (e.UnixTime < cutoffUnix)
            {
                actionEvents.RemoveAt(i);
                eventOccurrences.RemoveAt(i);
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
