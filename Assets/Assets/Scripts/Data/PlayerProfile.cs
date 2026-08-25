using System.Collections.Generic;
using UnityEngine;

public class PlayerProfile : MonoBehaviour
{
    [Header("Progression")]
    public Dictionary<string, int> skills = new();
    public HashSet<string> unlockedSkills = new();

    [Header("Equipment (by type)")]
    // Stores committed skillId equipped in each slot type.
    public Dictionary<SkillType, string> equippedSkillByType = new();

    public void AddSkill(string skillName)
    {
        if (unlockedSkills.Contains(skillName)) return;

        unlockedSkills.Add(skillName);
        skills[skillName] = 1;
        Debug.Log($"[Profile] Learned skill: {skillName}");
    }

    public string GetEquippedSkillId(SkillType type)
    {
        return equippedSkillByType.TryGetValue(type, out var id) ? id : null;
    }

    public void EquipSkill(SkillData skill)
    {
        if (skill == null) return;

        equippedSkillByType[skill.type] = skill.skillId;
        Debug.Log($"[Profile] Equipped {skill.skillName} (Tier {skill.tier}) in slot {skill.type}");
    }

    public void ReplaceEquippedSkill(SkillData newSkill)
    {
        // For now “replace” just means equip into that type slot.
        EquipSkill(newSkill);
    }
}


