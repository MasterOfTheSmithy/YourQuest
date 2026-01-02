using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public class PlayerState
{
    // Bump because we are adding fields expected by other systems.
    public int schemaVersion = 3;

    // Identity
    public string playerId = "player";
    public string displayName = "The Player";

    // Progression
    public int level = 1;

    // Keep existing saved field for backward compatibility
    public int experience = 0;

    // ? Alias: newer code expects "xp"
    // IMPORTANT: this must NOT be serialized/deserialized, or old files like "xp": "0.0" will break loads.
    [JsonIgnore]
    public int xp
    {
        get => experience;
        set => experience = Mathf.Max(0, value);
    }

    // ? Newer code expects xpToNext (derived)
    // IMPORTANT: must NOT be serialized/deserialized.
    [JsonIgnore]
    public int xpToNext
    {
        get
        {
            int needed = GetXpRequiredForLevel(level);
            int remaining = needed - experience;
            return Mathf.Max(0, remaining);
        }
    }

    // Core stats
    public StatBlock stats = new StatBlock();

    // Progression flavor
    public List<TitleRecord> titles = new List<TitleRecord>();
    public List<ClassRecord> classes = new List<ClassRecord>();

    // Skills (tiered families)
    public List<SkillRecord> skills = new List<SkillRecord>();

    // Quests
    public List<QuestRecord> quests = new List<QuestRecord>();

    // Equipment / loadout
    public Dictionary<string, string> equippedSkillBySlot = new Dictionary<string, string>();

    // Reputation / social
    public Dictionary<string, float> reputation = new Dictionary<string, float>();

    // Location
    public string currentRegionId = "region_unknown";
    public string currentRegionName = "Unknown";

    // Location (required by other systems)
    public string currentScene = "";
    public Vector3 lastPosition = Vector3.zero;

    // Time
    public long lastUpdatedUnix;

    // ---------------------------
    // Behavior Ledger (long-lived evidence)
    // ---------------------------

    [Tooltip("Short, aggregated lines of what the player has been doing over time. Used as evidence in prompts.")]
    public List<string> behaviorLedger = new List<string>();

    [Tooltip("Optional counters; useful for balancing. Keys like: verb:punch, region:library, craft:woodcutting, etc.")]
    public Dictionary<string, float> behaviorCounters = new Dictionary<string, float>();

    [Tooltip("Last time we rolled up short-term events into the ledger.")]
    public long lastLedgerRollupUnix = 0;

    // ---------------------------
    // Persistent cooldowns (earned pacing)
    // ---------------------------

    public long nextSkillEligibleUnix = 0;
    public long nextTitleEligibleUnix = 0;
    public long nextQuestEligibleUnix = 0;

    public float rewardBudget = 0f;
    public long rewardBudgetLastUpdateUnix = 0;

    public void Touch()
    {
        lastUpdatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    // ---------------------------
    // XP / Level helpers
    // ---------------------------

    public static int GetXpRequiredForLevel(int lvl)
    {
        lvl = Mathf.Max(1, lvl);
        float baseNeed = 100f;
        float growth = 1.5f;
        float needed = baseNeed * Mathf.Pow(growth, lvl - 1);
        return Mathf.RoundToInt(needed);
    }

    public bool TryLevelUpIfReady(bool consumeXp = true)
    {
        int needed = GetXpRequiredForLevel(level);
        if (experience < needed) return false;

        if (consumeXp) experience -= needed;
        level += 1;
        Touch();
        return true;
    }

    public void AddXp(int amount, bool autoLevel = true)
    {
        if (amount <= 0) return;
        experience += amount;

        if (autoLevel)
        {
            for (int i = 0; i < 50; i++)
            {
                if (!TryLevelUpIfReady(true)) break;
            }
        }

        Touch();
    }

    // ---------------------------
    // Ledger helpers
    // ---------------------------

    public void AddLedgerLine(string line, int maxLines = 60)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        line = line.Trim();

        behaviorLedger ??= new List<string>();
        behaviorLedger.Add(line);

        if (maxLines > 0 && behaviorLedger.Count > maxLines)
            behaviorLedger.RemoveRange(0, behaviorLedger.Count - maxLines);

        Touch();
    }

    public void IncCounter(string key, float delta)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        key = key.Trim().ToLowerInvariant();

        behaviorCounters ??= new Dictionary<string, float>();
        behaviorCounters.TryGetValue(key, out float cur);
        behaviorCounters[key] = cur + delta;
        Touch();
    }

    // ---------------------------
    // Titles
    // ---------------------------

    public bool HasTitle(string titleName)
    {
        if (string.IsNullOrWhiteSpace(titleName)) return false;
        titleName = titleName.Trim();

        if (titles == null) return false;

        for (int i = 0; i < titles.Count; i++)
            if (titles[i] != null && titles[i].name == titleName)
                return true;

        return false;
    }

    public void AwardTitle(string titleName, string description)
    {
        if (string.IsNullOrWhiteSpace(titleName)) return;
        if (HasTitle(titleName)) return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        titles ??= new List<TitleRecord>();
        titles.Add(new TitleRecord
        {
            titleId = Guid.NewGuid().ToString("N"),
            name = titleName.Trim(),
            description = (description ?? "").Trim(),
            acquiredUnix = now,
            earnedUnix = now
        });

        Touch();
    }

    // ---------------------------
    // Classes
    // ---------------------------

    public void AwardClass(string className, string description)
    {
        if (string.IsNullOrWhiteSpace(className)) return;

        classes ??= new List<ClassRecord>();

        for (int i = 0; i < classes.Count; i++)
            if (classes[i] != null && classes[i].name == className.Trim())
                return;

        classes.Add(new ClassRecord
        {
            name = className.Trim(),
            description = (description ?? "").Trim(),
            acquiredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        Touch();
    }

    // ---------------------------
    // Skills helpers
    // ---------------------------

    public SkillRecord FindSkillById(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId)) return null;
        if (skills == null) return null;

        for (int i = 0; i < skills.Count; i++)
        {
            var s = skills[i];
            if (s != null && s.skillId == skillId) return s;
        }
        return null;
    }

    public int FindHighestTierInFamily(string familyId)
    {
        if (string.IsNullOrWhiteSpace(familyId)) return 0;
        if (skills == null) return 0;

        int bestTier = 0;
        for (int i = 0; i < skills.Count; i++)
        {
            var s = skills[i];
            if (s == null) continue;

            if (!string.IsNullOrWhiteSpace(s.familyId) && s.familyId == familyId)
            {
                if (s.tier > bestTier) bestTier = s.tier;
            }
        }

        return bestTier;
    }

    public void SeedSkill(string skillName, string type, string hook)
    {
        if (string.IsNullOrWhiteSpace(skillName)) return;

        skills ??= new List<SkillRecord>();

        for (int i = 0; i < skills.Count; i++)
            if (skills[i] != null && skills[i].name == skillName.Trim())
                return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        skills.Add(new SkillRecord
        {
            name = skillName.Trim(),
            type = (type ?? "Unknown").Trim(),
            tier = 1,
            description = (hook ?? "").Trim(),

            skillId = Guid.NewGuid().ToString("N"),
            familyId = null,
            parentSkillId = null,

            rank = 1,
            unlocked = true,
            context = "",
            environment = "",

            acquiredUnix = now,
            learnedUnix = now
        });

        Touch();
    }

    public void UpsertSkill(SkillRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.skillId)) return;

        skills ??= new List<SkillRecord>();

        for (int i = 0; i < skills.Count; i++)
        {
            var existing = skills[i];
            if (existing != null && existing.skillId == record.skillId)
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
    public int vitality = 10;
    public int strength = 10;
    public int dexterity = 10;
    public int intelligence = 10;

    public int maxHealth = 100;
    public int maxStamina = 100;
    public int maxMana = 50;

    public int attack = 10;
    public int defense = 5;

    [Range(0f, 1f)]
    public float critChance = 0.05f;

    public float moveSpeed = 5f;
}

[Serializable]
public class TitleRecord
{
    public string titleId;
    public long earnedUnix;

    public string name;
    [TextArea(2, 6)] public string description;

    public long acquiredUnix;
}

[Serializable]
public class ClassRecord
{
    public string name;
    [TextArea(2, 6)] public string description;
    public long acquiredUnix;
}

[Serializable]
public class SkillRecord
{
    public string skillId;
    public string familyId;
    public string parentSkillId;

    public int rank = 1;
    public bool unlocked = true;

    public string context;
    public string environment;

    public long learnedUnix;

    public string name;
    public string type;
    public int tier;
    [TextArea(2, 6)] public string description;

    public long acquiredUnix;
}

[Serializable]
public class QuestRecord
{
    public string questId;

    public string name;
    [TextArea(2, 6)] public string description;

    public string status = "offer";
    public string[] tags = Array.Empty<string>();

    public long createdUnix;
    public long updatedUnix;
}
