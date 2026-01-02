using UnityEngine;

public enum SkillType { Passive, Active, Ultimate }

[CreateAssetMenu(fileName = "EmergentSkill", menuName = "Emergent/Draft Skill")]
public class EmergentSkill : ScriptableObject
{
    [Header("Draft Identity")]
    public string draftId;            // GUID string
    public string skillName;

    [TextArea(4, 12)]
    public string description;

    [Header("Classification")]
    public SkillType type = SkillType.Active;

    [Header("Metadata")]
    public string context;
    public string environment;

    // ? Added: lightweight tags for filtering + similarity + LLM grounding
    // Keep these short: ["combat","unarmed","training","forest","night"]
    public string[] contextTags;

    [Header("Draft State")]
    [Range(0f, 1f)] public float fitScore; // optional
    public bool committed;                // once converted into SkillData
    public string committedSkillId;       // optional link to committed asset
    public long createdUnix;              // optional

    [Header("Upgrade Candidate")]
    public bool isUpgradeCandidate;
    public string upgradeTargetSkillId;   // the committed skill it likely upgrades
    public float similarityScore;         // how similar it was (0.1)
}
