using System.Collections.Generic;
using System;
using UnityEngine;

public class PlayerProfile : MonoBehaviour
{
    // note: This component is a serialized compatibility adapter; PlayerStateManager owns live progression and equipment state.
    public PlayerState State => PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;

    [Header("Progression")]
    public Dictionary<string, int> skills = new();
    public HashSet<string> unlockedSkills = new();

    [Header("Equipment (by type)")]
    // Stores committed skillId equipped in each slot type.
    public Dictionary<SkillType, string> equippedSkillByType = new();

    public void AddSkill(string skillName)
    {
        PlayerState state = State;
        if (state != null)
        {
            state.EnsureCollections();
            if (state.FindSkillByName(skillName) == null)
                state.UpsertSkill(new SkillRecord { skillId = YQStateContract.StableId(YQStableEntityKind.Content, state.playerId + "|compat-skill", state.skills.Count), name = skillName, type = "compatibility", learnedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            return;
        }
        if (unlockedSkills.Contains(skillName)) return;

        unlockedSkills.Add(skillName);
        skills[skillName] = 1;
        Debug.Log($"[Profile] Learned skill: {skillName}");
    }

    public string GetEquippedSkillId(SkillType type)
    {
        PlayerState state = State;
        if (state != null)
        {
            state.EnsureCollections();
            return state.equippedSkillBySlot.TryGetValue(type.ToString(), out string canonicalId) ? canonicalId : null;
        }
        return equippedSkillByType.TryGetValue(type, out var id) ? id : null;
    }

    public void EquipSkill(SkillData skill)
    {
        if (skill == null) return;

        PlayerState state = State;
        if (state != null)
        {
            state.EnsureCollections();
            state.equippedSkillBySlot[skill.type.ToString()] = skill.skillId;
            state.Touch();
            return;
        }

        equippedSkillByType[skill.type] = skill.skillId;
        Debug.Log($"[Profile] Equipped {skill.skillName} (Tier {skill.tier}) in slot {skill.type}");
    }

    public void ReplaceEquippedSkill(SkillData newSkill)
    {
        // For now “replace” just means equip into that type slot.
        EquipSkill(newSkill);
    }
}

