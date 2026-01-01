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
                draft.skillName, draft.description, draft.context, draft.environment, draft.type.ToString(),
                c.skillName, c.description, c.context, c.environment, c.type.ToString()
            );

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        if (best != null && bestScore >= strongMatchThreshold)
        {
            draft.isUpgradeCandidate = true;
            draft.upgradeTargetSkillId = best.skillId;
            draft.similarityScore = bestScore;

            upgradeCandidates.Add(draft);
            Debug.Log($"[EventAccumulator] Upgrade candidate: '{draft.skillName}' -> '{best.skillName}' (score {bestScore:0.00})");
        }
        else
        {
            draft.isUpgradeCandidate = false;
            draft.upgradeTargetSkillId = null;
            draft.similarityScore = bestScore;

            ghostSkills.Add(draft);
            Debug.Log($"[EventAccumulator] Ghost skill added: {draft.skillName} ({draft.type})");
        }
    }
    public void ClearEvents()
    {
        actionEvents.Clear();
    }

    public IReadOnlyList<EmergentSkill> GetGhostSkills() => ghostSkills;
    public IReadOnlyList<EmergentSkill> GetUpgradeCandidates() => upgradeCandidates;
    public IReadOnlyList<SkillData> GetCommittedSkills() => committedSkills;
    public IReadOnlyList<ActionEvent> GetEvents() => actionEvents;
}
