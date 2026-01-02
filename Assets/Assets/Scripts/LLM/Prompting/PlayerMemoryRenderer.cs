using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class PlayerMemoryRenderer
{
    public static string Render(PlayerState s, int maxSkills = 10, int maxQuests = 6, int maxTitles = 6)
    {
        if (s == null) return "PlayerSnapshot: <null>";

        var sb = new StringBuilder();
        sb.AppendLine("PLAYER_SNAPSHOT");
        sb.AppendLine($"Name: {s.displayName}");

        // xp is int; xpToNext is int; formatting with :0 is unnecessary (and can confuse)
        sb.AppendLine($"Level: {s.level}  XP: {s.xp}/{(s.xp + s.xpToNext)} (to next {s.xpToNext})");

        sb.AppendLine($"Scene: {s.currentScene}  Region: {s.currentRegionId}");

        // ? Vector3 is not indexable, use .x .y .z
        sb.AppendLine($"Pos: [{s.lastPosition.x:0.0}, {s.lastPosition.y:0.0}, {s.lastPosition.z:0.0}]");
        sb.AppendLine();

        // Stats
        var st = s.stats;
        if (st != null)
        {
            sb.AppendLine("STATS");
            sb.AppendLine($"STR {st.strength} | DEX {st.dexterity} | INT {st.intelligence} | VIT {st.vitality}");
            sb.AppendLine($"HP {st.maxHealth} | STA {st.maxStamina} | MANA {st.maxMana}");
            sb.AppendLine($"ATK {st.attack} | DEF {st.defense} | CRIT {st.critChance:0.00} | MS x{st.moveSpeed:0.00}");
            sb.AppendLine();
        }

        // Equipped
        sb.AppendLine("EQUIPPED_SKILLS");
        if (s.equippedSkillBySlot != null && s.equippedSkillBySlot.Count > 0)
        {
            foreach (var kv in s.equippedSkillBySlot)
            {
                var rec = s.FindSkillById(kv.Value);
                if (rec != null)
                    sb.AppendLine($"{kv.Key}: {rec.name} (Tier {rec.tier})");
                else
                    sb.AppendLine($"{kv.Key}: {kv.Value}");
            }
        }
        else sb.AppendLine("<none>");
        sb.AppendLine();

        // Skills (top tiers first)
        sb.AppendLine("SKILLS (top/highest tiers)");
        int added = 0;

        if (s.skills != null && s.skills.Count > 0)
        {
            // Track families we already printed so we don't list duplicates
            var printedFamilies = new HashSet<string>();

            for (int i = 0; i < s.skills.Count && added < maxSkills; i++)
            {
                var r = s.skills[i];
                if (r == null || !r.unlocked) continue;

                string fam = string.IsNullOrWhiteSpace(r.familyId) ? $"__nofam__:{r.skillId}" : r.familyId;

                // Only show highest tier per family
                int highestTier = s.FindHighestTierInFamily(r.familyId);
                if (highestTier > 0 && r.tier != highestTier) continue;

                // Only print one skill per family
                if (printedFamilies.Contains(fam)) continue;
                printedFamilies.Add(fam);

                sb.AppendLine($"- {r.name} | Type {r.type} | Tier {r.tier} | Family {Short(r.familyId)}");
                added++;
            }
        }

        if (added == 0) sb.AppendLine("<none>");
        sb.AppendLine();

        // Titles
        sb.AppendLine("TITLES (recent)");
        int tCount = 0;
        if (s.titles != null && s.titles.Count > 0)
        {
            for (int i = s.titles.Count - 1; i >= 0 && tCount < maxTitles; i--)
            {
                var t = s.titles[i];
                if (t == null) continue;
                sb.AppendLine($"- {t.name}: {t.description}");
                tCount++;
            }
        }
        if (tCount == 0) sb.AppendLine("<none>");
        sb.AppendLine();

        // Quests
        sb.AppendLine("QUESTS (active/offers)");
        int qCount = 0;
        if (s.quests != null && s.quests.Count > 0)
        {
            for (int i = s.quests.Count - 1; i >= 0 && qCount < maxQuests; i--)
            {
                var q = s.quests[i];
                if (q == null) continue;

                // Treat both "complete" and "completed" as done; same for failed
                string status = (q.status ?? "").Trim().ToLowerInvariant();
                if (status == "complete" || status == "completed" || status == "failed") continue;

                sb.AppendLine($"- [{q.status}] {q.name}: {q.description}");
                qCount++;
            }
        }
        if (qCount == 0) sb.AppendLine("<none>");

        return sb.ToString();
    }

    private static string Short(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "none";
        return s.Length <= 8 ? s : s.Substring(0, 8);
    }
}
