using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "Emergent/Committed Skill")]
public class SkillData : ScriptableObject
{
    [Header("Stable Identity")]
    public string skillId; // GUID string

    [Header("Skill Family")]
    public string familyId;       // same for all tiers in a family
    [Min(1)] public int tier = 1; // Tier 1 = base, Tier 2+ = upgrades
    public string parentSkillId;  // previous tier’s skillId (optional)

    [Header("Skill Info")]
    public string skillName;

    [TextArea(4, 12)]
    public string description;

    [Header("Classification")]
    public SkillType type = SkillType.Active;

    [Header("Metadata")]
    public string context;
    public string environment;

    // Added: tags for balancing, synergy, future prerequisites, UI grouping, etc.
    public string[] tags;

    [Header("Progression")]
    [Min(1)] public int level = 1;
}
