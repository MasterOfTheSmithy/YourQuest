using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerState
{
    public int schemaVersion = 1;

    // Identity
    public string playerId = "player";
    public string displayName = "The Player";

    // Progression
    public int level = 1;
    public float xp = 0f;
    public float xpToNext = 100f;

    // Core stats (expand later)
    public StatBlock stats = new StatBlock();

    // Progression flavor
    public List<TitleRecord> titles = new List<TitleRecord>();
    public List<ClassRecord> classes = new List<ClassRecord>();

    // Skills (tiered families)
    public List<SkillRecord> skills = new List<SkillRecord>();

    // Equipment / loadout
    public Dictionary<string, string> equippedSkillBySlot = new Dictionary<string, string>();
    // Example keys: "Active", "Passive", "Ultimate" (string to keep JSON simple)

    // Reputation / flags
    public Dictionary<string, float> reputation = new Dictionary<string, float>();
    public Dictionary<string, float> flags = new Dictionary<string, float>();

    // Quests
    public List<QuestRecord> quests = new List<QuestRecord>();

    // Last known location (optional but helpful for LLM)
    public string currentScene = "";
    public string currentRegionId = "";
    public float[] lastPosition = new float[3];

    // Convenience
    public long lastUpdatedUnix;

    public void Touch()
    {
        lastUpdatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public SkillRecord FindSkillById(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId)) return null;
        for (int i = 0; i < skills.Count; i++)
            if (skills[i] != null && skills[i].skillId == skillId)
                return skills[i];
        return null;
    }

    public SkillRecord FindHighestTierInFamily(string familyId)
    {
        SkillRecord best = null;
        if (string.IsNullOrWhiteSpace(familyId)) return null;

        for (int i = 0; i < skills.Count; i++)
        {
            var s = skills[i];
            if (s == null || s.familyId != familyId) continue;
            if (best == null || s.tier > best.tier) best = s;
        }
        return best;
    }

    public void UpsertSkill(SkillRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.skillId)) return;

        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] != null && skills[i].skillId == record.skillId)
            {
                skills[i] = record;
                Touch();
                return;
            }
        }

        skills.Add(record);
        Touch();
    }
}

[Serializable]
public class StatBlock
{
    // RPG base stats
    public int strength = 5;
    public int dexterity = 5;
    public int intelligence = 5;
    public int vitality = 5;

    // Derived-ish (keep it simple now; can compute later)
    public int maxHealth = 100;
    public int maxStamina = 100;
    public int maxMana = 50;

    public int attack = 10;
    public int defense = 5;

    public float critChance = 0.05f;
    public float moveSpeed = 1.0f; // multiplier
}

[Serializable]
public class SkillRecord
{
    public string skillId;
    public string familyId;
    public int tier;
    public string parentSkillId;

    public string name;
    public string description;
    public string type; // "Active"/"Passive"/"Ultimate" (string keeps JSON stable)

    public int rank = 1; // player rank/level within the skill, if you want
    public bool unlocked = true;

    public string context;
    public string environment;

    public long learnedUnix;
}

[Serializable]
public class TitleRecord
{
    public string titleId;
    public string name;
    public string description;
    public long earnedUnix;
}

[Serializable]
public class ClassRecord
{
    public string classId;
    public string name;
    public string description;

    public int classLevel = 1;
    public long chosenUnix;
}

[Serializable]
public class QuestRecord
{
    public string questId;
    public string name;
    public string description;
    public string status; // "offer" | "active" | "complete" | "failed"
    public string[] tags;
    public long updatedUnix;
}
