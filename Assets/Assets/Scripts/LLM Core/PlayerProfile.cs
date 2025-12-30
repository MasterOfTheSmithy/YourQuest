using System.Collections.Generic;
using UnityEngine;

public class PlayerProfile : MonoBehaviour
{
    public Dictionary<string, int> skills = new();
    public HashSet<string> unlockedSkills = new();

    public void AddSkill(string skillName)
    {
        if (unlockedSkills.Contains(skillName)) return;
        unlockedSkills.Add(skillName);
        skills[skillName] = 1;
        Debug.Log($"[Profile] Learned skill: {skillName}");
    }
}
