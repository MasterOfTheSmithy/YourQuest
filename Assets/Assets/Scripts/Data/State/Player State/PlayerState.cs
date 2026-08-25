// Assets/Assets/Scripts/Data/State/Player State/PlayerState.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[Serializable]
public class PlayerState
{
    public int schemaVersion = 6;

    public string playerId = "player";
    public string displayName = "The Player";
    public string characterPronouns = "";
    public string characterBodyFrame = "";
    public string characterLifeDirection = "";
    public string characterVow = "";
    public string characterAppearanceSummary = "";
    public string characterCreationSeed = "";

    [JsonIgnore]
    public string playerName
    {
        get => displayName;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
                displayName = value.Trim();
        }
    }

    public int level = 1;
    public int experience = 0;

    [JsonIgnore]
    public int xp
    {
        get => experience;
        set => experience = Mathf.Max(0, value);
    }

    [JsonIgnore]
    public int xpToNext
    {
        get
        {
            int needed = GetXpRequiredForLevel(level);
            return Mathf.Max(0, needed - experience);
        }
    }

    public StatBlock stats = new StatBlock();

    public List<TitleRecord> titles = new List<TitleRecord>();
    public List<ClassRecord> classes = new List<ClassRecord>();
    public List<SkillRecord> skills = new List<SkillRecord>();
    public List<QuestRecord> quests = new List<QuestRecord>();
    public string activeQuestId = string.Empty;
    public List<InventoryItemRecord> inventoryItems = new List<InventoryItemRecord>();
    public List<PendingProgressionOfferRecord> pendingOffers = new List<PendingProgressionOfferRecord>();

    public Dictionary<string, string> equippedSkillBySlot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> equippedItemBySlot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, float> reputation = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    public string currentRegionId = "region_unknown";
    public string currentRegionName = "Unknown";
    public string currentScene = "";
    public Vector3 lastPosition = Vector3.zero;

    public string originQuestionnaireMode = string.Empty;
    public List<string> originQuestionnaireAnswers = new List<string>();
    public List<string> identityKeywords = new List<string>();
    public GeneratedOriginRecord generatedOrigin = new GeneratedOriginRecord();

    public long lastUpdatedUnix;
    public List<string> behaviorLedger = new List<string>();
    public Dictionary<string, float> behaviorCounters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    public long lastLedgerRollupUnix = 0;

    public long nextSkillEligibleUnix = 0;
    public long nextTitleEligibleUnix = 0;
    public long nextQuestEligibleUnix = 0;

    public float rewardBudget = 0f;
    public long rewardBudgetLastUpdateUnix = 0;

    public int currency = 0;

    public void EnsureCollections()
    {
        titles ??= new List<TitleRecord>();
        classes ??= new List<ClassRecord>();
        skills ??= new List<SkillRecord>();
        quests ??= new List<QuestRecord>();
        inventoryItems ??= new List<InventoryItemRecord>();
        pendingOffers ??= new List<PendingProgressionOfferRecord>();
        equippedSkillBySlot ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        equippedItemBySlot ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        reputation ??= new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        behaviorLedger ??= new List<string>();
        originQuestionnaireAnswers ??= new List<string>();
        identityKeywords ??= new List<string>();
        generatedOrigin ??= new GeneratedOriginRecord();
        behaviorCounters ??= new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        stats ??= new StatBlock();
        EnsureQuestObjectiveCollections();
        EnsureActiveQuestSelection();
    }

    private void EnsureQuestObjectiveCollections()
    {
        if (quests == null)
            return;

        for (int i = 0; i < quests.Count; i++)
        {
            if (quests[i] != null)
                quests[i].EnsureCollections();
        }
    }

    public void Touch()
    {
        lastUpdatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public void AddLedgerLine(string line, int maxLines = 80)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        EnsureCollections();

        string trimmed = line.Trim();
        if (behaviorLedger.Count > 0 && string.Equals(behaviorLedger[behaviorLedger.Count - 1], trimmed, StringComparison.OrdinalIgnoreCase))
            return;

        behaviorLedger.Add(trimmed);
        if (maxLines > 0 && behaviorLedger.Count > maxLines)
            behaviorLedger.RemoveRange(0, behaviorLedger.Count - maxLines);

        Touch();
    }

    public void IncCounter(string key, float amount = 1f)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        EnsureCollections();
        string normalized = key.Trim();
        behaviorCounters.TryGetValue(normalized, out float current);
        behaviorCounters[normalized] = current + amount;
        Touch();
    }

    public static int GetXpRequiredForLevel(int lvl)
    {
        lvl = Mathf.Max(1, lvl);
        return Mathf.RoundToInt(100f * Mathf.Pow(1.5f, lvl - 1));
    }

    public bool TryLevelUpIfReady(bool consumeXp = true)
    {
        int needed = GetXpRequiredForLevel(level);
        if (experience < needed)
            return false;

        level = Mathf.Max(1, level + 1);
        if (consumeXp)
            experience = Mathf.Max(0, experience - needed);

        Touch();
        return true;
    }

    public void AddXp(int amount)
    {
        experience = Mathf.Max(0, experience + Mathf.Max(0, amount));
        while (TryLevelUpIfReady(true)) { }
        Touch();
    }

    public void AwardTitle(string titleName, string description = "")
    {
        EnsureCollections();
        string key = NormalizeKey(titleName);
        if (string.IsNullOrWhiteSpace(key))
            return;

        for (int i = 0; i < titles.Count; i++)
        {
            TitleRecord existing = titles[i];
            if (existing == null)
                continue;

            if (NormalizeKey(existing.titleId) == key || NormalizeKey(existing.name) == key || ComputeLooseSimilarity(existing.name, titleName) >= 0.92f)
            {
                if (!string.IsNullOrWhiteSpace(description))
                    existing.description = description.Trim();
                existing.acquiredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Touch();
                return;
            }
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        titles.Add(new TitleRecord
        {
            titleId = Guid.NewGuid().ToString("N"),
            name = titleName.Trim(),
            description = (description ?? string.Empty).Trim(),
            earnedUnix = now,
            acquiredUnix = now
        });
        Touch();
    }

    public void AwardClass(string className, string description = "")
    {
        EnsureCollections();
        string key = NormalizeKey(className);
        if (string.IsNullOrWhiteSpace(key))
            return;

        for (int i = 0; i < classes.Count; i++)
        {
            ClassRecord existing = classes[i];
            if (existing == null)
                continue;

            if (NormalizeKey(existing.classId) == key || NormalizeKey(existing.name) == key || ComputeLooseSimilarity(existing.name, className) >= 0.9f)
            {
                if (!string.IsNullOrWhiteSpace(description))
                    existing.description = description.Trim();
                existing.unlockedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Touch();
                return;
            }
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        classes.Add(new ClassRecord
        {
            classId = Guid.NewGuid().ToString("N"),
            name = className.Trim(),
            description = (description ?? string.Empty).Trim(),
            unlockedUnix = now,
            acquiredUnix = now
        });
        Touch();
    }

    public void OfferQuest(
        string questName,
        string description = "",
        string[] tags = null,
        List<QuestObjectiveRecord> objectives = null,
        string payloadJson = "")
    {
        EnsureCollections();
        string key = NormalizeKey(questName);
        if (string.IsNullOrWhiteSpace(key))
            return;

        for (int i = 0; i < quests.Count; i++)
        {
            QuestRecord existing = quests[i];
            if (existing == null)
                continue;

            if (NormalizeKey(existing.questId) == key || NormalizeKey(existing.name) == key || ComputeLooseSimilarity(existing.name, questName) >= 0.9f)
            {
                if (!string.IsNullOrWhiteSpace(description))
                    existing.description = description.Trim();
                if (tags != null && tags.Length > 0)
                    existing.tags = tags;
                if (objectives != null && objectives.Count > 0)
                    existing.objectives = new List<QuestObjectiveRecord>(objectives);
                if (!string.IsNullOrWhiteSpace(payloadJson))
                    existing.payloadJson = payloadJson;
                existing.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (string.IsNullOrWhiteSpace(existing.status))
                    existing.status = "offer";
                if (!HasValidActiveQuest())
                    SetActiveQuest(existing.questId);
                Touch();
                return;
            }
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        QuestRecord created = new QuestRecord
        {
            questId = Guid.NewGuid().ToString("N"),
            name = questName.Trim(),
            description = (description ?? string.Empty).Trim(),
            status = "offer",
            tags = tags ?? Array.Empty<string>(),
            objectives = objectives != null
                ? new List<QuestObjectiveRecord>(objectives)
                : new List<QuestObjectiveRecord>(),
            payloadJson = payloadJson ?? string.Empty,
            createdUnix = now,
            updatedUnix = now
        };
        quests.Add(created);
        if (!HasValidActiveQuest())
            SetActiveQuest(created.questId);
        Touch();
    }

    public void UpsertTitle(TitleRecord record)
    {
        if (record == null)
            return;

        EnsureCollections();
        string key = !string.IsNullOrWhiteSpace(record.titleId) ? NormalizeKey(record.titleId) : NormalizeKey(record.name);
        if (string.IsNullOrWhiteSpace(key))
            return;

        for (int i = 0; i < titles.Count; i++)
        {
            TitleRecord existing = titles[i];
            if (existing == null)
                continue;

            if (NormalizeKey(existing.titleId) == key || NormalizeKey(existing.name) == key || ComputeLooseSimilarity(existing.name, record.name) >= 0.92f)
            {
                titles[i] = record;
                Touch();
                return;
            }
        }

        titles.Add(record);
        Touch();
    }

    public void UpsertClass(ClassRecord record)
    {
        if (record == null)
            return;

        EnsureCollections();
        string key = !string.IsNullOrWhiteSpace(record.classId) ? NormalizeKey(record.classId) : NormalizeKey(record.name);
        if (string.IsNullOrWhiteSpace(key))
            return;

        for (int i = 0; i < classes.Count; i++)
        {
            ClassRecord existing = classes[i];
            if (existing == null)
                continue;

            if (NormalizeKey(existing.classId) == key || NormalizeKey(existing.name) == key || ComputeLooseSimilarity(existing.name, record.name) >= 0.9f)
            {
                classes[i] = record;
                Touch();
                return;
            }
        }

        classes.Add(record);
        Touch();
    }

    public void UpsertQuest(QuestRecord record)
    {
        if (record == null)
            return;

        EnsureCollections();
        string key = !string.IsNullOrWhiteSpace(record.questId) ? NormalizeKey(record.questId) : NormalizeKey(record.name);
        if (string.IsNullOrWhiteSpace(key))
            return;

        for (int i = 0; i < quests.Count; i++)
        {
            QuestRecord existing = quests[i];
            if (existing == null)
                continue;

            if (NormalizeKey(existing.questId) == key || NormalizeKey(existing.name) == key || ComputeLooseSimilarity(existing.name, record.name) >= 0.9f)
            {
                quests[i] = record;
                if (string.IsNullOrWhiteSpace(activeQuestId) || string.Equals(activeQuestId, existing.questId, StringComparison.OrdinalIgnoreCase))
                    activeQuestId = record.questId;
                if (!HasValidActiveQuest())
                    EnsureActiveQuestSelection();
                Touch();
                return;
            }
        }

        quests.Add(record);
        if (!HasValidActiveQuest())
            SetActiveQuest(record.questId);
        Touch();
    }

    public QuestRecord GetActiveQuest()
    {
        EnsureCollections();
        if (!string.IsNullOrWhiteSpace(activeQuestId))
        {
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRecord quest = quests[i];
                if (quest != null && string.Equals(quest.questId, activeQuestId, StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsQuestComplete(quest))
                        return quest;

                    activeQuestId = string.Empty;
                    break;
                }
            }
        }

        EnsureActiveQuestSelection();
        if (string.IsNullOrWhiteSpace(activeQuestId))
            return null;

        for (int i = 0; i < quests.Count; i++)
        {
            QuestRecord quest = quests[i];
            if (quest != null && !IsQuestComplete(quest) && string.Equals(quest.questId, activeQuestId, StringComparison.OrdinalIgnoreCase))
                return quest;
        }

        return null;
    }

    public bool SetActiveQuest(string questId)
    {
        EnsureCollections();
        if (string.IsNullOrWhiteSpace(questId))
            return false;

        QuestRecord selected = null;
        for (int i = 0; i < quests.Count; i++)
        {
            QuestRecord quest = quests[i];
            if (quest == null)
                continue;
            if (string.Equals(quest.questId, questId, StringComparison.OrdinalIgnoreCase))
            {
                selected = quest;
                break;
            }
        }

        if (selected == null || IsQuestComplete(selected))
            return false;

        activeQuestId = selected.questId;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (int i = 0; i < quests.Count; i++)
        {
            QuestRecord quest = quests[i];
            if (quest == null || IsQuestComplete(quest))
                continue;

            if (string.Equals(quest.questId, activeQuestId, StringComparison.OrdinalIgnoreCase))
                quest.status = "active";
            else if (string.Equals(quest.status, "active", StringComparison.OrdinalIgnoreCase))
                quest.status = "offer";

            quest.updatedUnix = now;
        }

        Touch();
        return true;
    }

    public bool TryCompleteActiveQuest(out string message)
    {
        QuestRecord quest = GetActiveQuest();
        if (quest == null)
        {
            message = "No active quest to complete.";
            return false;
        }

        return TryCompleteQuest(quest.questId, out message);
    }

    public bool TryCompleteQuest(string questId, out string message)
    {
        message = "Quest not found.";
        EnsureCollections();
        if (string.IsNullOrWhiteSpace(questId))
            return false;

        QuestRecord selected = null;
        for (int i = 0; i < quests.Count; i++)
        {
            QuestRecord quest = quests[i];
            if (quest != null && string.Equals(quest.questId, questId, StringComparison.OrdinalIgnoreCase))
            {
                selected = quest;
                break;
            }
        }

        if (selected == null)
            return false;

        if (IsQuestComplete(selected) && selected.completedUnix > 0)
        {
            message = "Quest already complete.";
            return false;
        }

        int xpReward = ComputeQuestXpReward(selected);
        int goldReward = ComputeQuestGoldReward(selected);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        selected.status = "completed";
        selected.completedUnix = now;
        selected.updatedUnix = now;
        selected.rewardXp = xpReward;
        selected.rewardGold = goldReward;

        if (xpReward > 0)
            AddXp(xpReward);
        if (goldReward > 0)
            currency += goldReward;

        string questName = SafeTrim(selected.name, "Unnamed Quest");
        AddLedgerLine("Completed quest '" + questName + "'. Reward: " + xpReward + " XP, " + goldReward + " gold.");

        if (string.Equals(activeQuestId, selected.questId, StringComparison.OrdinalIgnoreCase))
        {
            activeQuestId = string.Empty;
            EnsureActiveQuestSelection();
        }

        Touch();
        message = "Quest complete: " + questName + ". Reward: +" + xpReward + " XP, +" + goldReward + " gold.";
        return true;
    }

    public SkillRecord FindSkillById(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId) || skills == null)
            return null;

        string key = NormalizeKey(skillId);
        for (int i = 0; i < skills.Count; i++)
        {
            SkillRecord skill = skills[i];
            if (skill == null)
                continue;
            if (NormalizeKey(skill.skillId) == key)
                return skill;
        }
        return null;
    }

    public SkillRecord FindSkillByName(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName) || skills == null)
            return null;

        string key = NormalizeKey(skillName);
        for (int i = 0; i < skills.Count; i++)
        {
            SkillRecord skill = skills[i];
            if (skill == null)
                continue;
            if (NormalizeKey(skill.name) == key)
                return skill;
        }
        return null;
    }

    public SkillRecord FindBestSkillMatch(string skillName, string description, string[] tags, float minScore = 0.65f)
    {
        if (skills == null || skills.Count == 0)
            return null;

        SkillRecord best = null;
        float bestScore = minScore;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillRecord skill = skills[i];
            if (skill == null)
                continue;

            float score = SkillSimilarity.Score(
                skillName,
                description,
                tags,
                skill.name,
                skill.description,
                BuildSkillTags(skill));

            if (score > bestScore)
            {
                bestScore = score;
                best = skill;
            }
        }

        return best;
    }

    public int FindHighestTierInFamily(string familyId)
    {
        if (string.IsNullOrWhiteSpace(familyId) || skills == null)
            return 0;

        string key = NormalizeKey(familyId);
        int bestTier = 0;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillRecord skill = skills[i];
            if (skill == null)
                continue;
            if (NormalizeKey(skill.familyId) != key)
                continue;
            if (skill.tier > bestTier)
                bestTier = skill.tier;
        }
        return bestTier;
    }

    public void SeedSkill(string skillName, string type, string hook)
    {
        EnsureCollections();
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        SkillRecord existing = FindSkillByName(skillName);
        if (existing != null)
        {
            existing.unlocked = true;
            if (!string.IsNullOrWhiteSpace(type))
                existing.type = type.Trim();
            if (!string.IsNullOrWhiteSpace(hook))
                existing.description = hook.Trim();
            existing.learnedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Touch();
            return;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        skills.Add(new SkillRecord
        {
            skillId = Guid.NewGuid().ToString("N"),
            familyId = Guid.NewGuid().ToString("N"),
            parentSkillId = null,
            rank = 1,
            unlocked = true,
            context = string.Empty,
            environment = string.Empty,
            learnedUnix = now,
            name = skillName.Trim(),
            type = string.IsNullOrWhiteSpace(type) ? "Unknown" : type.Trim(),
            tier = 1,
            description = (hook ?? string.Empty).Trim(),
            acquiredUnix = now,
            isSpell = false
        });
        Touch();
    }

    public void UpsertSkill(SkillRecord record)
    {
        if (record == null)
            return;

        EnsureCollections();
        string key = !string.IsNullOrWhiteSpace(record.skillId) ? NormalizeKey(record.skillId) : NormalizeKey(record.name);
        if (string.IsNullOrWhiteSpace(key))
            return;

        for (int i = 0; i < skills.Count; i++)
        {
            SkillRecord existing = skills[i];
            if (existing == null)
                continue;
            if (NormalizeKey(existing.skillId) == key || NormalizeKey(existing.name) == key || ComputeLooseSimilarity(existing.name, record.name) >= 0.94f)
            {
                skills[i] = record;
                Touch();
                return;
            }
        }

        skills.Add(record);
        Touch();
    }

    public InventoryItemRecord FindInventoryItemById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || inventoryItems == null)
            return null;

        string key = NormalizeKey(itemId);
        for (int i = 0; i < inventoryItems.Count; i++)
        {
            InventoryItemRecord item = inventoryItems[i];
            if (item == null)
                continue;
            if (NormalizeKey(item.itemId) == key)
                return item;
        }
        return null;
    }

    public void AddOrUpdateItem(InventoryItemRecord item, bool allowStacking = true)
    {
        if (item == null)
            return;

        EnsureCollections();

        if (string.IsNullOrWhiteSpace(item.itemId))
            item.itemId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(item.generatedAtUnixString))
            item.generatedAtUnixString = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        if (item.quantity < 1)
            item.quantity = 1;

        if (allowStacking && item.stackable)
        {
            string templateKey = NormalizeKey(item.templateId);
            for (int i = 0; i < inventoryItems.Count; i++)
            {
                InventoryItemRecord existing = inventoryItems[i];
                if (existing == null || !existing.stackable)
                    continue;
                if (NormalizeKey(existing.templateId) == templateKey && NormalizeKey(existing.itemType) == NormalizeKey(item.itemType))
                {
                    existing.quantity += item.quantity;
                    Touch();
                    return;
                }
            }
        }

        for (int i = 0; i < inventoryItems.Count; i++)
        {
            InventoryItemRecord existing = inventoryItems[i];
            if (existing == null)
                continue;
            if (NormalizeKey(existing.itemId) == NormalizeKey(item.itemId))
            {
                inventoryItems[i] = item;
                Touch();
                return;
            }
        }

        inventoryItems.Add(item);
        Touch();
    }

    public bool TryEquipItem(string itemId, out string message)
    {
        EnsureCollections();
        message = string.Empty;

        InventoryItemRecord item = FindInventoryItemById(itemId);
        if (item == null)
        {
            message = "Item not found.";
            return false;
        }

        if (!item.IsEquippable)
        {
            message = "Item is not equippable.";
            return false;
        }

        string slot = item.equipSlot;
        if (string.IsNullOrWhiteSpace(slot))
        {
            message = "Item has no equipment slot.";
            return false;
        }

        string normalizedSlot = NormalizeKey(slot);
        string previousWeaponId = equippedItemBySlot.TryGetValue("weapon", out string existingWeaponId) ? existingWeaponId : string.Empty;
        string previousOffhandId = equippedItemBySlot.TryGetValue("offhand", out string existingOffhandId) ? existingOffhandId : string.Empty;

        if (IsTwoHandedItem(item))
        {
            equippedItemBySlot["weapon"] = item.itemId;
            equippedItemBySlot["offhand"] = item.itemId;
            Touch();
            message = "Equipped " + item.displayName + " in both hands.";
            return true;
        }

        if (normalizedSlot == "weapon" && !string.IsNullOrWhiteSpace(previousWeaponId) && NormalizeKey(previousWeaponId) == NormalizeKey(previousOffhandId))
            equippedItemBySlot.Remove("offhand");
        else if (normalizedSlot == "offhand")
        {
            InventoryItemRecord weapon = GetEquippedItem("weapon");
            if (IsTwoHandedItem(weapon))
                equippedItemBySlot.Remove("weapon");
        }

        equippedItemBySlot[slot] = item.itemId;
        Touch();
        message = "Equipped " + item.displayName + " in " + slot + ".";
        return true;
    }

    private static bool IsTwoHandedItem(InventoryItemRecord item)
    {
        if (item == null)
            return false;

        string haystack = ((item.itemType ?? string.Empty) + " " +
                           (item.equipSlot ?? string.Empty) + " " +
                           (item.displayName ?? string.Empty) + " " +
                           (item.description ?? string.Empty) + " " +
                           (item.effectKey ?? string.Empty)).ToLowerInvariant();

        return haystack.Contains("two-handed")
            || haystack.Contains("two handed")
            || haystack.Contains("2h")
            || haystack.Contains("greatsword")
            || haystack.Contains("greataxe")
            || haystack.Contains("great axe")
            || haystack.Contains("longbow")
            || haystack.Contains("warbow")
            || haystack.Contains("polearm")
            || haystack.Contains("halberd")
            || haystack.Contains("staff");
    }

    public bool TryConsumeItem(string itemId, out InventoryItemRecord consumed)
    {
        consumed = null;
        EnsureCollections();
        InventoryItemRecord item = FindInventoryItemById(itemId);
        if (item == null || !item.IsConsumable)
            return false;

        consumed = item;
        item.quantity = Mathf.Max(0, item.quantity - 1);
        if (item.quantity <= 0)
        {
            for (int i = inventoryItems.Count - 1; i >= 0; i--)
            {
                if (inventoryItems[i] != null && NormalizeKey(inventoryItems[i].itemId) == NormalizeKey(itemId))
                    inventoryItems.RemoveAt(i);
            }
        }
        Touch();
        return true;
    }

    public InventoryItemRecord GetEquippedItem(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return null;
        EnsureCollections();
        if (!equippedItemBySlot.TryGetValue(slot, out string itemId))
            return null;
        return FindInventoryItemById(itemId);
    }

    public InventoryItemRecord FindFirstConsumable()
    {
        if (inventoryItems == null)
            return null;
        for (int i = 0; i < inventoryItems.Count; i++)
        {
            InventoryItemRecord item = inventoryItems[i];
            if (item != null && item.IsConsumable && item.quantity > 0)
                return item;
        }
        return null;
    }

    public PendingProgressionOfferRecord GetActiveOffer()
    {
        EnsureCollections();
        PendingProgressionOfferRecord best = null;
        long bestTime = long.MinValue;
        for (int i = 0; i < pendingOffers.Count; i++)
        {
            PendingProgressionOfferRecord offer = pendingOffers[i];
            if (offer == null || !offer.IsPending)
                continue;
            if (offer.offeredUnix >= bestTime)
            {
                best = offer;
                bestTime = offer.offeredUnix;
            }
        }
        return best;
    }

    public int GetPendingOfferCount()
    {
        EnsureCollections();
        int count = 0;
        for (int i = 0; i < pendingOffers.Count; i++)
        {
            if (pendingOffers[i] != null && pendingOffers[i].IsPending)
                count++;
        }
        return count;
    }

    public PendingProgressionOfferRecord QueueOrRefreshOffer(PendingProgressionOfferRecord incoming, float duplicateThreshold = 0.9f)
    {
        if (incoming == null)
            return null;

        EnsureCollections();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        incoming.offerId = string.IsNullOrWhiteSpace(incoming.offerId) ? Guid.NewGuid().ToString("N") : incoming.offerId.Trim();
        incoming.offerKind = SafeLower(incoming.offerKind);
        incoming.name = SafeTrim(incoming.name, incoming.offerKind + " offer");
        incoming.description = SafeTrim(incoming.description, string.Empty);
        incoming.offerState = string.IsNullOrWhiteSpace(incoming.offerState) ? "pending" : incoming.offerState.Trim().ToLowerInvariant();
        incoming.offeredUnix = incoming.offeredUnix <= 0 ? now : incoming.offeredUnix;
        incoming.updatedUnix = now;
        incoming.confidence = Mathf.Clamp01(incoming.confidence);

        for (int i = 0; i < pendingOffers.Count; i++)
        {
            PendingProgressionOfferRecord existing = pendingOffers[i];
            if (existing == null)
                continue;
            if (!string.Equals(existing.offerKind, incoming.offerKind, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!existing.IsPending)
                continue;

            float similarity = existing.ComputeSimilarity(incoming);
            bool sameUpgradeTarget = !string.IsNullOrWhiteSpace(existing.upgradeTargetId) && string.Equals(existing.upgradeTargetId, incoming.upgradeTargetId, StringComparison.OrdinalIgnoreCase);
            if (similarity >= duplicateThreshold || sameUpgradeTarget)
            {
                existing.MergeFrom(incoming);
                Touch();
                return existing;
            }
        }

        pendingOffers.Add(incoming);
        Touch();
        return incoming;
    }

    public bool AcceptOffer(string offerId, out string message)
    {
        message = "Offer not found.";
        PendingProgressionOfferRecord offer = FindOfferById(offerId);
        if (offer == null || !offer.IsPending)
            return false;

        bool applied = false;
        switch (SafeLower(offer.offerKind))
        {
            case "skill":
            case "spell":
                applied = AcceptSkillOffer(offer, out message);
                break;
            case "title":
                AwardTitle(offer.name, offer.description);
                message = "Title accepted: " + offer.name;
                applied = true;
                break;
            case "class":
                AwardClass(offer.name, offer.description);
                message = "Class accepted: " + offer.name;
                applied = true;
                break;
            case "quest":
                List<QuestObjectiveRecord> objectives = BuildQuestObjectivesFromOffer(offer);
                if (objectives.Count == 0)
                {
                    // note: Never convert quest prose into mechanics; malformed legacy/generated offers remain unapplied.
                    message = "Quest offer is missing a supported structured objective.";
                    applied = false;
                    break;
                }
                OfferQuest(
                    offer.name,
                    offer.description,
                    offer.tags ?? Array.Empty<string>(),
                    objectives,
                    offer.payloadJson);
                message = "Quest accepted: " + offer.name;
                applied = true;
                break;
            case "item":
                // note: LLM output supplies only item identity and semantic type; the content service binds approved gameplay assets.
                applied = AcceptItemOffer(offer, out message);
                break;
            default:
                message = "Unsupported offer kind: " + offer.offerKind;
                applied = false;
                break;
        }

        if (!applied)
            return false;

        offer.offerState = "accepted";
        offer.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        IncCounter("offer:accepted", 1f);
        AddLedgerLine("Accepted " + offer.offerKind + " offer: " + offer.name + ".");
        Touch();
        return true;
    }

    private static List<QuestObjectiveRecord> BuildQuestObjectivesFromOffer(
        PendingProgressionOfferRecord offer)
    {
        List<QuestObjectiveRecord> records = new List<QuestObjectiveRecord>(1);
        if (offer == null || string.IsNullOrWhiteSpace(offer.payloadJson))
            return records;

        try
        {
            JObject payload = JObject.Parse(offer.payloadJson);
            JObject objective = payload["objective"] as JObject;
            string type = objective?["type"]?.ToString().Trim().ToLowerInvariant();
            if (!IsSupportedQuestObjectiveType(type))
                return records;

            string targetId = objective["targetId"]?.ToString().Trim() ?? string.Empty;
            if ((type == "talk_to_npc" || type == "enter_region") &&
                string.IsNullOrWhiteSpace(targetId))
                return records;

            float requiredCount = 1f;
            if (float.TryParse(objective["requiredCount"]?.ToString(), out float parsedCount))
                requiredCount = Mathf.Clamp(parsedCount, 1f, 100f);

            records.Add(new QuestObjectiveRecord
            {
                objectiveId = Guid.NewGuid().ToString("N"),
                type = type,
                targetId = targetId,
                targetName = objective["targetName"]?.ToString().Trim() ?? string.Empty,
                counterKey = objective["counterKey"]?.ToString().Trim() ?? string.Empty,
                counterPrefix = objective["counterPrefix"]?.ToString().Trim() ?? string.Empty,
                requiredCount = requiredCount,
                description = objective["description"]?.ToString().Trim() ?? string.Empty
            });
        }
        catch (Exception)
        {
            // note: An invalid offer payload is rejected by the caller without mutating the authoritative save.
        }

        return records;
    }

    private static bool IsSupportedQuestObjectiveType(string type)
    {
        return type == "equip_item" ||
            type == "talk_to_npc" ||
            type == "cast_spell" ||
            type == "defeat_enemy" ||
            type == "loot_item" ||
            type == "pickup_item" ||
            type == "open_lock" ||
            type == "mimic_reveal" ||
            type == "use_shrine" ||
            type == "enter_region" ||
            type == "wait_seconds";
    }

    public bool DeclineOffer(string offerId, out string message)
    {
        message = "Offer not found.";
        PendingProgressionOfferRecord offer = FindOfferById(offerId);
        if (offer == null || !offer.IsPending)
            return false;

        offer.offerState = "declined";
        offer.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        IncCounter("offer:declined", 1f);
        AddLedgerLine("Declined " + offer.offerKind + " offer: " + offer.name + ".");
        Touch();
        message = "Declined " + offer.offerKind + " offer: " + offer.name;
        return true;
    }

    public PendingProgressionOfferRecord FindOfferById(string offerId)
    {
        if (string.IsNullOrWhiteSpace(offerId) || pendingOffers == null)
            return null;

        string key = NormalizeKey(offerId);
        for (int i = 0; i < pendingOffers.Count; i++)
        {
            PendingProgressionOfferRecord offer = pendingOffers[i];
            if (offer == null)
                continue;
            if (NormalizeKey(offer.offerId) == key)
                return offer;
        }

        return null;
    }

    private bool AcceptSkillOffer(PendingProgressionOfferRecord offer, out string message)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string type = string.IsNullOrWhiteSpace(offer.skillType) ? (offer.isSpell ? "spell" : "combat") : offer.skillType.Trim();
        string slot = offer.isSpell ? "spell" : "active";

        SkillRecord upgradeTarget = !string.IsNullOrWhiteSpace(offer.upgradeTargetId) ? FindSkillById(offer.upgradeTargetId) : null;
        SkillRecord existingExact = FindSkillByName(offer.name);
        SkillRecord record = existingExact ?? new SkillRecord
        {
            skillId = Guid.NewGuid().ToString("N"),
            familyId = upgradeTarget != null && !string.IsNullOrWhiteSpace(upgradeTarget.familyId)
                ? upgradeTarget.familyId
                : Guid.NewGuid().ToString("N"),
            parentSkillId = upgradeTarget != null ? upgradeTarget.skillId : null,
            rank = 1,
            unlocked = true,
            context = offer.context,
            environment = offer.environment,
            learnedUnix = now,
            acquiredUnix = now,
            tier = upgradeTarget != null ? Mathf.Max(upgradeTarget.tier + 1, offer.proposedTier) : Mathf.Max(1, offer.proposedTier)
        };

        if (existingExact != null)
        {
            record.familyId = string.IsNullOrWhiteSpace(record.familyId) ? (upgradeTarget != null ? upgradeTarget.familyId : Guid.NewGuid().ToString("N")) : record.familyId;
            if (upgradeTarget != null)
            {
                record.parentSkillId = upgradeTarget.skillId;
                record.tier = Mathf.Max(record.tier, upgradeTarget.tier + 1, offer.proposedTier);
            }
        }

        record.name = offer.name;
        record.type = type;
        record.description = offer.description;
        record.unlocked = true;
        record.context = offer.context;
        record.environment = offer.environment;
        record.learnedUnix = now;
        record.isSpell = offer.isSpell || string.Equals(type, "spell", StringComparison.OrdinalIgnoreCase);
        record.rank = Mathf.Max(1, record.rank);
        if (record.acquiredUnix <= 0)
            record.acquiredUnix = now;
        if (record.tier <= 0)
            record.tier = Mathf.Max(1, offer.proposedTier);

        UpsertSkill(record);

        if (upgradeTarget != null)
        {
            string currentlyEquipped = equippedSkillBySlot.TryGetValue(slot, out string equippedSkillId) ? equippedSkillId : string.Empty;
            if (string.IsNullOrWhiteSpace(currentlyEquipped) || string.Equals(currentlyEquipped, upgradeTarget.skillId, StringComparison.OrdinalIgnoreCase))
                equippedSkillBySlot[slot] = record.skillId;
            message = "Accepted skill upgrade: " + record.name + " (T" + record.tier + ")";
        }
        else
        {
            if (!equippedSkillBySlot.ContainsKey(slot) || string.IsNullOrWhiteSpace(equippedSkillBySlot[slot]))
                equippedSkillBySlot[slot] = record.skillId;
            message = (record.isSpell ? "Accepted spell: " : "Accepted skill: ") + record.name;
        }

        return true;
    }

    private bool AcceptItemOffer(PendingProgressionOfferRecord offer, out string message)
    {
        message = "Item service unavailable.";
        GeneratedRpgContentService service = GeneratedRpgContentService.Instance;
        if (service == null || offer == null)
            return false;

        string itemType = NormalizeOfferItemType(offer.skillType);
        if (string.IsNullOrWhiteSpace(itemType))
        {
            message = "Item offer has no supported item type.";
            return false;
        }

        // note: GenerateItem applies the curated library's compatible model, material, sound, and effect keys for this semantic type.
        InventoryItemRecord item = service.GenerateItem("offer:" + offer.offerId + ":" + itemType, Mathf.Max(1, level), itemType, itemType == "consumable");
        if (item == null)
        {
            message = "Item offer could not be materialized.";
            return false;
        }

        item.displayName = offer.name;
        item.description = offer.description;
        item.familyKey = "player_response:" + itemType;
        AddOrUpdateItem(item, true);
        if (item.IsEquippable && GetEquippedItem(item.equipSlot) == null)
            TryEquipItem(item.itemId, out _);

        message = "Item accepted: " + item.displayName;
        return true;
    }

    private static string NormalizeOfferItemType(string raw)
    {
        string type = SafeLower(raw);
        switch (type)
        {
            case "weapon": case "offhand": case "head": case "chest": case "gloves": case "legs":
            case "boots": case "belt": case "cloak": case "ring": case "earring": case "necklace":
            case "trinket": case "consumable":
                return type;
            default:
                return string.Empty;
        }
    }

    private void EnsureActiveQuestSelection()
    {
        if (quests == null || quests.Count == 0)
        {
            activeQuestId = string.Empty;
            return;
        }

        if (HasValidActiveQuest())
            return;

        QuestRecord selected = null;
        for (int i = 0; i < quests.Count; i++)
        {
            QuestRecord quest = quests[i];
            if (quest == null || IsQuestComplete(quest))
                continue;
            if (string.Equals(quest.status, "active", StringComparison.OrdinalIgnoreCase))
            {
                selected = quest;
                break;
            }
        }

        if (selected == null)
        {
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRecord quest = quests[i];
                if (quest == null || IsQuestComplete(quest))
                    continue;
                selected = quest;
                break;
            }
        }

        activeQuestId = selected != null ? selected.questId : string.Empty;
        if (selected != null && !string.Equals(selected.status, "active", StringComparison.OrdinalIgnoreCase))
            selected.status = "active";
    }

    private bool HasValidActiveQuest()
    {
        if (quests == null || quests.Count == 0 || string.IsNullOrWhiteSpace(activeQuestId))
            return false;

        for (int i = 0; i < quests.Count; i++)
        {
            QuestRecord quest = quests[i];
            if (quest == null || IsQuestComplete(quest))
                continue;
            if (string.Equals(quest.questId, activeQuestId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsQuestComplete(QuestRecord quest)
    {
        if (quest == null)
            return true;

        string status = quest.status ?? string.Empty;
        return status.Equals("complete", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("completed", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("failed", StringComparison.OrdinalIgnoreCase);
    }

    private int ComputeQuestXpReward(QuestRecord quest)
    {
        string text = BuildQuestRewardText(quest);
        int reward = Mathf.RoundToInt(50f + Mathf.Max(1, level) * 18f);
        if (ContainsAny(text, "several", "three", "3", "elite", "boss", "final", "dangerous"))
            reward += 25;
        if (ContainsAny(text, "recover", "retrieve", "proof", "return", "report"))
            reward += 15;
        return Mathf.Clamp(reward, 40, 500);
    }

    private int ComputeQuestGoldReward(QuestRecord quest)
    {
        string text = BuildQuestRewardText(quest);
        int reward = 18 + Mathf.Max(1, level) * 6;
        if (ContainsAny(text, "treasure", "chest", "bounty", "contract", "reward"))
            reward += 12;
        if (ContainsAny(text, "boss", "elite", "final"))
            reward += 16;
        return Mathf.Clamp(reward, 10, 300);
    }

    private static string BuildQuestRewardText(QuestRecord quest)
    {
        if (quest == null)
            return string.Empty;

        string tags = quest.tags != null ? string.Join(" ", quest.tags) : string.Empty;
        return ((quest.name ?? string.Empty) + " " + (quest.description ?? string.Empty) + " " + tags).ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(text) || needles == null)
            return false;

        for (int i = 0; i < needles.Length; i++)
        {
            string needle = needles[i];
            if (!string.IsNullOrWhiteSpace(needle) && text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static string[] BuildSkillTags(SkillRecord skill)
    {
        if (skill == null)
            return Array.Empty<string>();

        List<string> tags = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(skill.type))
            tags.Add(skill.type.Trim().ToLowerInvariant());
        if (skill.isSpell)
            tags.Add("spell");
        if (!string.IsNullOrWhiteSpace(skill.context))
            tags.Add(skill.context.Trim().ToLowerInvariant());
        return tags.ToArray();
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string SafeTrim(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string SafeLower(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static float ComputeLooseSimilarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return 0f;

        string[] ta = Tokenize(a);
        string[] tb = Tokenize(b);
        if (ta.Length == 0 || tb.Length == 0)
            return 0f;

        HashSet<string> setA = new HashSet<string>(ta);
        HashSet<string> setB = new HashSet<string>(tb);
        int inter = 0;
        foreach (string token in setA)
        {
            if (setB.Contains(token))
                inter++;
        }

        int union = setA.Count + setB.Count - inter;
        return union <= 0 ? 0f : (float)inter / union;
    }

    private static string[] Tokenize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        char[] sep =
        {
            ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '/', '\\', '-', '_', '(', ')', '[', ']', '{', '}', '"', '\''
        };
        return value.ToLowerInvariant().Split(sep, StringSplitOptions.RemoveEmptyEntries);
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
    [Range(0f, 1f)] public float critChance = 0.05f;
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
    public string classId;
    public long unlockedUnix;
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
    public int tier = 1;
    [TextArea(2, 6)] public string description;
    public long acquiredUnix;
    public bool isSpell;
    public string targetingMode;
    public string resourceType;
    public int resourceCost;
    public float cooldownSeconds;
    public string vfxFamily;
    public string animationIntent;
    public string payloadJson;
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
    public long completedUnix;
    public int rewardXp;
    public int rewardGold;
    public string generationSource;
    public string generatorPromptHash;
    public string payloadJson;
    public List<QuestObjectiveRecord> objectives = new List<QuestObjectiveRecord>();

    public void EnsureCollections()
    {
        objectives ??= new List<QuestObjectiveRecord>();
    }
}

[Serializable]
public class QuestObjectiveRecord
{
    public string objectiveId;
    public string type;
    public string targetId;
    public string targetName;
    public string counterKey;
    public string counterPrefix;
    public float requiredCount = 1f;
    public string description;
    public bool completed;
    public long completedUnix;
}

[Serializable]
public class GeneratedOriginRecord
{
    public string source;
    public string seed;
    public string mode;
    public string directionKey;
    public string stimulus;
    public string className;
    public string titleName;
    public string abilityName;
    public string abilityKind;
    public string questName;
    public string[] tags = Array.Empty<string>();
    public string rawJson;
    public long generatedUnix;
}

[Serializable]
public class InventoryItemRecord
{
    public string itemId;
    public string templateId;
    public string displayName;
    public string itemType;
    public string equipSlot;
    public string rarity;
    [TextArea(2, 6)] public string description;
    public int quantity = 1;
    public bool stackable;
    public int powerScore;
    public int attackBonus;
    public int defenseBonus;
    public int healthBonus;
    public int staminaBonus;
    public int manaBonus;
    public float moveSpeedBonus;
    public int healAmount;
    public int restoreStaminaAmount;
    public int restoreManaAmount;
    public string iconKey;
    public string prefabKey;
    public string effectKey;
    public string familyKey;
    public string generatedAtUnixString;

    [JsonIgnore]
    public bool IsConsumable => string.Equals(itemType, "consumable", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsEquippable => !string.IsNullOrWhiteSpace(equipSlot) && !IsConsumable;
}

[Serializable]
public class PendingProgressionOfferRecord
{
    public string offerId;
    public string offerKind;
    public string offerState = "pending";
    public string name;
    [TextArea(2, 6)] public string description;
    public float confidence;
    public string reason;
    public bool isUpgrade;
    public string upgradeTargetId;
    public string upgradeTargetName;
    public string familyId;
    public int proposedTier = 1;
    public bool isSpell;
    public string skillType;
    public string context;
    public string environment;
    public string[] tags = Array.Empty<string>();
    public string payloadJson;
    public long offeredUnix;
    public long updatedUnix;

    [JsonIgnore]
    public bool IsPending => string.Equals(offerState, "pending", StringComparison.OrdinalIgnoreCase);

    public float ComputeSimilarity(PendingProgressionOfferRecord other)
    {
        if (other == null)
            return 0f;

        if (!string.Equals(offerKind, other.offerKind, StringComparison.OrdinalIgnoreCase))
            return 0f;

        float nameScore = SkillSimilarity.Score(name, string.Empty, tags, other.name, string.Empty, other.tags);
        float descScore = SkillSimilarity.Score(string.Empty, description, tags, string.Empty, other.description, other.tags);
        float metaScore = 0f;
        if (!string.IsNullOrWhiteSpace(upgradeTargetId) && !string.IsNullOrWhiteSpace(other.upgradeTargetId) && string.Equals(upgradeTargetId, other.upgradeTargetId, StringComparison.OrdinalIgnoreCase))
            metaScore = 1f;
        else if (!string.IsNullOrWhiteSpace(skillType) && !string.IsNullOrWhiteSpace(other.skillType) && string.Equals(skillType, other.skillType, StringComparison.OrdinalIgnoreCase))
            metaScore = 0.35f;

        return Mathf.Clamp01(nameScore * 0.55f + descScore * 0.30f + metaScore * 0.15f);
    }

    public void MergeFrom(PendingProgressionOfferRecord other)
    {
        if (other == null)
            return;

        if (!string.IsNullOrWhiteSpace(other.name))
            name = other.name.Trim();
        if (!string.IsNullOrWhiteSpace(other.description))
            description = other.description.Trim();
        if (!string.IsNullOrWhiteSpace(other.reason))
            reason = other.reason.Trim();
        if (!string.IsNullOrWhiteSpace(other.upgradeTargetId))
            upgradeTargetId = other.upgradeTargetId.Trim();
        if (!string.IsNullOrWhiteSpace(other.upgradeTargetName))
            upgradeTargetName = other.upgradeTargetName.Trim();
        if (!string.IsNullOrWhiteSpace(other.familyId))
            familyId = other.familyId.Trim();
        if (!string.IsNullOrWhiteSpace(other.skillType))
            skillType = other.skillType.Trim();
        if (!string.IsNullOrWhiteSpace(other.context))
            context = other.context.Trim();
        if (!string.IsNullOrWhiteSpace(other.environment))
            environment = other.environment.Trim();
        if (!string.IsNullOrWhiteSpace(other.payloadJson))
            payloadJson = other.payloadJson;
        if (other.tags != null && other.tags.Length > 0)
            tags = other.tags;

        confidence = Mathf.Max(confidence, other.confidence);
        proposedTier = Mathf.Max(proposedTier, other.proposedTier);
        isUpgrade |= other.isUpgrade;
        isSpell |= other.isSpell;
        updatedUnix = Math.Max(updatedUnix, other.updatedUnix);
    }
}
