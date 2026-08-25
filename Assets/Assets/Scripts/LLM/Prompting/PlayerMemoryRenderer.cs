// Assets/Assets/Scripts/LLM/Prompting/PlayerMemoryRenderer.cs
using System.Collections.Generic;
using System.Text;

public static class PlayerMemoryRenderer
{
    public static string Render(PlayerState s, int maxSkills = 10, int maxQuests = 6, int maxTitles = 6, int maxItems = 8)
    {
        if (s == null)
            return "PLAYER_SNAPSHOT\n<null>";

        s.EnsureCollections();

        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("PLAYER_SNAPSHOT");
        sb.AppendLine($"Name: {s.displayName}");
        sb.AppendLine($"Level: {s.level}  XP: {s.xp}/{(s.xp + s.xpToNext)} (to next {s.xpToNext})");
        sb.AppendLine($"Scene: {s.currentScene}  Region: {s.currentRegionId}");
        sb.AppendLine($"Pos: [{s.lastPosition.x:0.0}, {s.lastPosition.y:0.0}, {s.lastPosition.z:0.0}]");
        sb.AppendLine();

        StatBlock st = s.stats;
        if (st != null)
        {
            sb.AppendLine("STATS");
            sb.AppendLine($"STR {st.strength} | DEX {st.dexterity} | INT {st.intelligence} | VIT {st.vitality}");
            sb.AppendLine($"HP {st.maxHealth} | STA {st.maxStamina} | MANA {st.maxMana}");
            sb.AppendLine($"ATK {st.attack} | DEF {st.defense} | CRIT {st.critChance:0.00} | MS {st.moveSpeed:0.00}");
            sb.AppendLine();
        }

        sb.AppendLine("EQUIPPED_ITEMS");
        if (s.equippedItemBySlot.Count == 0)
            sb.AppendLine("<none>");
        else
        {
            foreach (KeyValuePair<string, string> kvp in s.equippedItemBySlot)
            {
                InventoryItemRecord item = s.FindInventoryItemById(kvp.Value);
                sb.AppendLine(item != null ? $"- {kvp.Key}: {item.displayName} ({item.itemType})" : $"- {kvp.Key}: {kvp.Value}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("EQUIPPED_SKILLS");
        if (s.equippedSkillBySlot.Count == 0)
            sb.AppendLine("<none>");
        else
        {
            foreach (KeyValuePair<string, string> kvp in s.equippedSkillBySlot)
            {
                SkillRecord skill = s.FindSkillById(kvp.Value);
                sb.AppendLine(skill != null ? $"- {kvp.Key}: {skill.name} ({skill.type})" : $"- {kvp.Key}: {kvp.Value}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("INVENTORY (top)");
        int itemCount = 0;
        for (int i = 0; i < s.inventoryItems.Count && itemCount < maxItems; i++)
        {
            InventoryItemRecord item = s.inventoryItems[i];
            if (item == null)
                continue;
            sb.AppendLine($"- {item.displayName} x{item.quantity} | {item.itemType} | {item.rarity}");
            itemCount++;
        }
        if (itemCount == 0)
            sb.AppendLine("<none>");
        sb.AppendLine();

        sb.AppendLine("SKILLS (top tiers)");
        HashSet<string> printedFamilies = new HashSet<string>();
        int skillCount = 0;
        for (int i = 0; i < s.skills.Count && skillCount < maxSkills; i++)
        {
            SkillRecord r = s.skills[i];
            if (r == null || !r.unlocked)
                continue;

            string family = string.IsNullOrWhiteSpace(r.familyId) ? "__" + r.skillId : r.familyId;
            if (printedFamilies.Contains(family))
                continue;
            int highestTier = string.IsNullOrWhiteSpace(r.familyId) ? r.tier : s.FindHighestTierInFamily(r.familyId);
            if (highestTier > 0 && r.tier != highestTier)
                continue;

            printedFamilies.Add(family);
            sb.AppendLine($"- {r.name} | Type {r.type} | Tier {r.tier}");
            skillCount++;
        }
        if (skillCount == 0)
            sb.AppendLine("<none>");
        sb.AppendLine();

        sb.AppendLine("TITLES (recent)");
        int titleCount = 0;
        for (int i = s.titles.Count - 1; i >= 0 && titleCount < maxTitles; i--)
        {
            TitleRecord title = s.titles[i];
            if (title == null)
                continue;
            sb.AppendLine($"- {title.name}: {title.description}");
            titleCount++;
        }
        if (titleCount == 0)
            sb.AppendLine("<none>");
        sb.AppendLine();

        sb.AppendLine("QUESTS (active/offers)");
        int questCount = 0;
        for (int i = s.quests.Count - 1; i >= 0 && questCount < maxQuests; i--)
        {
            QuestRecord q = s.quests[i];
            if (q == null)
                continue;
            string status = (q.status ?? string.Empty).Trim().ToLowerInvariant();
            if (status == "complete" || status == "completed" || status == "failed")
                continue;
            sb.AppendLine($"- [{q.status}] {q.name}: {q.description}");
            questCount++;
        }
        if (questCount == 0)
            sb.AppendLine("<none>");

        return sb.ToString();
    }
}
