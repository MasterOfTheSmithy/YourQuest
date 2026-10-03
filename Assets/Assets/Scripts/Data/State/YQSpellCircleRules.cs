using System;
using UnityEngine;

// note: Spell circles reuse the saved tier value; accepted records, IDs and save schemas need no migration.
public static class YQSpellCircleRules
{
    public const int FirstCircle = 1;
    public const int LastCircle = 7;
    public const float LegacyManaCost = 15f;

    public static bool IsSpell(SkillRecord skill) => skill != null &&
        (skill.isSpell || string.Equals(skill.type, "spell", StringComparison.OrdinalIgnoreCase));

    public static bool IsSpell(PendingProgressionOfferRecord offer) => offer != null &&
        (offer.isSpell || string.Equals(offer.offerKind, "spell", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(offer.skillType, "spell", StringComparison.OrdinalIgnoreCase));

    public static int ClampCircle(int value) => Mathf.Clamp(value, FirstCircle, LastCircle);
    public static int GetCircle(SkillRecord spell) => ClampCircle(spell != null ? spell.tier : FirstCircle);

    public static string GetResourceType(SkillRecord spell) =>
        string.IsNullOrWhiteSpace(spell?.resourceType) ? "mana" : spell.resourceType.Trim().ToLowerInvariant();

    public static float GetResourceCost(SkillRecord spell)
    {
        // note: Old spells lack mechanical fields; retain their existing mana fallback, including explicit free casts.
        string resource = GetResourceType(spell);
        if (resource == "none" || resource == "free") return 0f;
        return spell != null && spell.resourceCost > 0 ? spell.resourceCost : LegacyManaCost;
    }

    public static int GetPower(SkillRecord spell, int equipmentManaBonus = 0)
    {
        // note: Circle and mastery strengthen supported mechanics; narrative names/descriptions never execute power.
        int rank = Mathf.Clamp(spell != null ? spell.rank : 1, 1, 100);
        int projectileBonus = string.Equals(spell?.targetingMode, "projectile", StringComparison.OrdinalIgnoreCase) ? 6 : 0;
        return 16 + (GetCircle(spell) - FirstCircle) * 4 + (rank - 1) * 2 +
            Mathf.Max(0, equipmentManaBonus / 10) + projectileBonus;
    }

    public static float GetCastSeconds(SkillRecord spell, int equipmentManaBonus = 0)
    {
        string resource = GetResourceType(spell);
        float manaCost = resource == "stamina" ? 0f : GetResourceCost(spell);
        return CalculateCastSeconds(GetCircle(spell), manaCost, GetPower(spell, equipmentManaBonus));
    }

    public static float CalculateCastSeconds(int circle, float manaCost, int strength)
    {
        // note: Bound malformed costs while retaining separate, monotonic contributions from circle, mana and delivered power.
        if (float.IsNaN(manaCost) || float.IsInfinity(manaCost)) manaCost = LegacyManaCost;
        return 0.2f + 0.24f * (ClampCircle(circle) - FirstCircle) +
            0.012f * Mathf.Clamp(manaCost, 0f, 300f) + 0.015f * Mathf.Clamp(strength, 1, 500);
    }
}
