using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// note: Pure policy for newly authored items; accepted inventory and container loot budgets are never rerolled here.
internal static class YQItemAuthoringPolicy
{
    internal const string PromptRules =
        "Name each item naturally (usually two to five words); identify its physical form in its name or description. " +
        "Describe its construction, use, or one player-specific detail in a concrete sentence. Avoid abstract word piles, " +
        "questionnaire/system commentary, and invented executable powers. Match names to slots; armor is not a weapon. " +
        "Preserve explicitly requested item names, forms and traits. Ordinary rarity is rolled by code: Rare 30%, Epic 5%, Legendary 1%. " +
        "Normally return empty rarityHint and intentEvidence. Override balance only for a specifically named/requested exceptional item " +
        "or an explicitly described extraordinary identity/trait (for example an existing deity or all-seeing eyes). " +
        "Do not label ordinary items epic or legendary in their names. For each matching item, set rarityHint to Common, Uncommon, Rare, Epic or Legendary and intentEvidence to a short exact quote " +
        "of the relevant player-authored statement. Ambition, worship, ordinary profession and generated lore do not authorize an override. " +
        "A special item does not upgrade unrelated gear. Never claim unsupported mechanics in descriptions.";

    private static readonly Regex Words = new Regex(@"[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*", RegexOptions.CultureInvariant);

    internal static string RollRarity(string seed)
    {
        // note: A dedicated salted, mixed hash avoids level-dependent uniform array picks and correlated sequential grants.
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in (seed ?? string.Empty) + "|item-rarity-v1") hash = (hash ^ c) * 16777619;
            hash ^= hash >> 16; hash *= 0x7feb352d; hash ^= hash >> 15; hash *= 0x846ca68b; hash ^= hash >> 16;
            return RarityForBucket((int)(hash % 10000));
        }
    }

    internal static string RarityForBucket(int bucket)
    {
        if (bucket < 0 || bucket >= 10000) throw new ArgumentOutOfRangeException(nameof(bucket));
        if (bucket < 100) return "Legendary";
        if (bucket < 600) return "Epic";
        if (bucket < 3600) return "Rare";
        if (bucket < 5600) return "Uncommon";
        return "Common";
    }

    internal static string ResolveRarity(string seed, string slot, string rarityHint, string intentEvidence, IEnumerable<string> authoredStatements)
    {
        string rarity = CanonicalRarity(rarityHint);
        string quote = Compact(intentEvidence);
        if (rarity != null && quote.Length >= 10 && quote.Length <= 800 && authoredStatements != null)
        {
            foreach (string statement in authoredStatements)
            {
                string source = Compact(statement);
                int at = source.IndexOf(quote, StringComparison.OrdinalIgnoreCase);
                if (at < 0) continue;
                // note: Include the preceding clause so quoting only "legendary sword" cannot strip "I do not own a".
                int start = at;
                while (start > 0 && at - start < 100 && ".!?;\n".IndexOf(source[start - 1]) < 0) start--;
                string evidence = source.Substring(start, at + quote.Length - start).ToLowerInvariant().Replace('’', '\'');
                if (HasDeniedOrAspirationalClaim(evidence)) continue;
                string itemEvidence = quote.ToLowerInvariant();
                bool concreteItem = MatchesSlot(slot, itemEvidence);
                bool explicitItem = concreteItem && HasWord(quote, rarity);
                bool exceptionalItem = concreteItem &&
                    (Regex.IsMatch(itemEvidence, @"\b(?:named|called)\s+[\p{L}\p{N}'’-]+") ||
                     HasWord(itemEvidence, "godforged", "divine", "immortal", "unbreakable", "omniscient") ||
                     HasPhrase(itemEvidence, "all-seeing", "all seeing"));
                bool extraordinaryIdentity =
                    Regex.IsMatch(evidence, @"\b(?:i am|i'm|my character is)\s+(?:\p{L}+,\s*)?(?:(?:the|a|an|legendary|ancient|divine|fallen|living|reborn|battle|death|war)\s+){0,5}(?:god|goddess|deity|demigod|immortal)\b") ||
                    HasPhrase(evidence, "my all-seeing eyes", "my all seeing eyes", "with all-seeing eyes", "with the all seeing eyes", "with all seeing eyes", "i have all-seeing eyes", "i have all seeing eyes", "my eyes are all-seeing");
                if (explicitItem || exceptionalItem || extraordinaryIdentity) return rarity;
            }
        }
        return RollRarity(seed);
    }

    internal static bool IsUsableHint(string slot, string name, string description)
        => string.IsNullOrEmpty(HintDiagnostic(slot, name, description));

    internal static string HintDiagnostic(string slot, string name, string description)
    {
        // note: Acceptance and repair feedback share one policy so a retry can locate the actual invalid field.
        string cleanName = Compact(name), cleanDescription = Compact(description);
        if (cleanName.Length < 2 || cleanName.Length > 90) return "nameHint must contain 2 to 90 characters after whitespace normalization";
        if (cleanDescription.Length < 12 || cleanDescription.Length > 260) return "descriptionHint must contain 12 to 260 characters after whitespace normalization";
        if (HasMalformedText(cleanName)) return "nameHint contains malformed text, markup or repeated words";
        if (HasMalformedText(cleanDescription)) return "descriptionHint contains malformed text, markup or repeated words";
        string text = (cleanName + " " + cleanDescription).ToLowerInvariant();
        if (HasPhrase(text, "placeholder", "generated item", "current build", "questionnaire", "player-facing", "fighting rhythm"))
            return "nameHint or descriptionHint contains placeholder or system commentary";
        return MatchesSlot(slot, text) ? string.Empty : "nameHint or descriptionHint must identify a supported physical equipment form matching this slot";
    }

    private static bool MatchesSlot(string slot, string text)
    {
        // note: Require a concrete form but allow poetic proper names and intentional unusual materials.
        switch ((slot ?? string.Empty).ToLowerInvariant())
        {
            // note: Greatsword is a concrete sword form; exact token matching does not recognize it as the token sword.
            case "weapon": return HasWord(text, "sword", "greatsword", "blade", "axe", "mace", "dagger", "spear", "staff", "bow", "crossbow", "scythe", "hammer", "weapon", "club", "sickle");
            case "offhand": return HasWord(text, "shield", "buckler", "focus", "tome", "book", "orb", "lantern", "ledger", "ward");
            case "head": return HasWord(text, "helm", "helmet", "hood", "hat", "crown", "mask", "circlet");
            case "chest": return HasWord(text, "armor", "armour", "cuirass", "coat", "robe", "mail", "vest", "harness", "breastplate", "tunic");
            case "gloves": return HasWord(text, "gloves", "gauntlets", "wraps");
            case "legs": return HasWord(text, "greaves", "trousers", "leggings", "pants", "legguards", "breeches");
            case "boots": return HasWord(text, "boots", "shoes", "sandals", "footwear");
            case "belt": return HasWord(text, "belt", "sash", "girdle");
            case "cloak": return HasWord(text, "cloak", "cape", "mantle");
            case "ring_left": case "ring_right": return HasWord(text, "ring", "band");
            case "earring_left": case "earring_right": return HasWord(text, "earring", "earrings", "stud");
            case "necklace": return HasWord(text, "necklace", "pendant", "amulet", "chain");
            case "trinket": return HasWord(text, "charm", "token", "talisman", "seal", "gem", "stone", "relic", "bracer", "amulet", "focus");
            case "consumable": return HasWord(text, "tonic", "potion", "draught", "vial", "phial", "ration", "poultice", "elixir");
            default: return false;
        }
    }

    internal static string Compact(string text) => string.IsNullOrWhiteSpace(text) ? string.Empty : Regex.Replace(text.Trim(), @"\s+", " ");

    private static string CanonicalRarity(string rarity)
    {
        foreach (string tier in new[] { "Common", "Uncommon", "Rare", "Epic", "Legendary" })
            if (string.Equals(tier, rarity?.Trim(), StringComparison.OrdinalIgnoreCase)) return tier;
        return null;
    }

    private static bool HasDeniedOrAspirationalClaim(string text) => HasPhrase(text,
        "not a", "not the", "no legendary", "no epic", "no rare", "don't own", "do not own", "don't have", "do not have",
        "without a", "without the", "wish to", "want to be", "want to become", "hope to", "one day", "someday", "pretend to", "worship", "hunt gods");

    private static bool HasWord(string text, params string[] candidates)
    {
        foreach (Match word in Words.Matches(text))
            foreach (string candidate in candidates)
                if (string.Equals(word.Value, candidate, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool HasPhrase(string text, params string[] phrases)
    {
        foreach (string phrase in phrases) if (text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static bool HasMalformedText(string text)
    {
        if (text.IndexOfAny(new[] { '<', '>', '{', '}', '[', ']', '\\' }) >= 0 || text.Contains("://")) return true;
        MatchCollection words = Words.Matches(text);
        if (words.Count == 0) return true;
        for (int i = 2; i < words.Count; i++)
            if (string.Equals(words[i].Value, words[i - 1].Value, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(words[i].Value, words[i - 2].Value, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
