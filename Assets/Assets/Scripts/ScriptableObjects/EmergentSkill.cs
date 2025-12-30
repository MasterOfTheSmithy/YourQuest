using UnityEngine;

[CreateAssetMenu(fileName = "NewEmergentSkill", menuName = "Emergent/Emergent Skill")]
public class EmergentSkill : ScriptableObject
{
    [Header("Skill Info")]
    public string skillName;
    [TextArea(4, 12)]
    public string description;
    [TextArea(2, 8)]
    public string morality;
    [TextArea(2, 8)]
    public string context;
    [TextArea(2, 8)]
    public string environment;

    [Header("Gameplay")]
    public float significanceMultiplier = 1f; // How fast progress counts toward next skill
    public string type; // Skill, Quest, Class, Title, etc.
}
