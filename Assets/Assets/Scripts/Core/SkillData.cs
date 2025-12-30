using UnityEngine;

public enum SkillType { Passive, Active, Ultimate }

[CreateAssetMenu(fileName = "EmergentSkill", menuName = "Emergent/Emergent Skill")]
public class SkillData : ScriptableObject
{
    public string skillName;
    public string description;
    public SkillType type;
    public string context;
    public string environment;
}
