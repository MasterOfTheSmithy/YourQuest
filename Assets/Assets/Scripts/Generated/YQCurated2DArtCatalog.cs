// Assets/Assets/Scripts/Generated/YQCurated2DArtCatalog.cs
using System;
using System.Collections.Generic;
using System.Text;

public readonly struct YQCurated2DArtEntry
{
    public readonly string key;
    public readonly string kind;
    public readonly string assetPath;
    public readonly string[] tags;
    public readonly int weight;

    public YQCurated2DArtEntry(string key, string kind, string assetPath, string tags, int weight = 1)
    {
        this.key = key;
        this.kind = kind;
        this.assetPath = assetPath;
        this.tags = SplitTags(tags);
        this.weight = Math.Max(1, weight);
    }

    private static string[] SplitTags(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }
}

public static class YQCurated2DArtCatalog
{
    public const string KindItemIcon = "item_icon";
    public const string KindClassBadge = "class_badge";
    public const string KindProfessionBadge = "profession_badge";
    public const string KindFactionBadge = "faction_badge";
    public const string KindPortrait = "portrait";
    public const string KindQuestUi = "quest_ui";
    public const string KindMapTile = "map_tile";

    private static readonly YQCurated2DArtEntry[] entries =
    {
        Entry("item_weapon_blade", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/sword.png", "weapon blade sword melee attack main hand"),
        Entry("item_weapon_axe", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/axe.png", "weapon axe melee strength chop lumberjack"),
        Entry("item_weapon_dagger", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/dagger.png", "weapon dagger rogue stealth precise dexterity"),
        Entry("item_weapon_spear", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/spear.png", "weapon spear reach hunter guard"),
        Entry("item_weapon_club", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/club.png", "weapon club blunt simple starter"),
        Entry("item_offhand_shield", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/shield.png", "offhand shield ward guard defense block"),
        Entry("item_head_helmet", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/helmet.png", "armor head helmet guard defense"),
        Entry("item_trinket_ring", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/ring.png", "trinket ring necklace charm focus magic mana"),
        Entry("item_consumable_potion", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/potion.png", "consumable tonic potion heal restore mana stamina"),
        Entry("item_consumable_food", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/bread.png", "consumable ration food bread heal stamina"),
        Entry("item_tool_pick", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/pick.png", "tool pick mining craft gather"),
        Entry("item_tool_hammer", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/hammer.png", "tool hammer craft forge repair"),
        Entry("item_container_bag", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/bag.png", "bag loot inventory travel"),
        Entry("item_container_chest", KindItemIcon, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/chest.png", "chest loot reward treasure"),
        Entry("item_currency_coin", KindItemIcon, "Assets/HumbleBundleResources/coinsicons_windows/coinsicons/coins/01_a.PNG", "currency coin gold merchant reward"),

        Entry("class_warrior", KindClassBadge, "Assets/HumbleBundleResources/rpgclassbadges_windows/rpgclassbadges/Badge_png/Badge_warrior.png", "warrior fighter weapon strength melee"),
        Entry("class_rogue", KindClassBadge, "Assets/HumbleBundleResources/rpgclassbadges_windows/rpgclassbadges/Badge_png/Badge_rogue.PNG", "rogue stealth dexterity trickster precise"),
        Entry("class_priest", KindClassBadge, "Assets/HumbleBundleResources/rpgclassbadges_windows/rpgclassbadges/Badge_png/Badge_priest.PNG", "priest heal faith light support"),
        Entry("class_paladin", KindClassBadge, "Assets/HumbleBundleResources/rpgclassbadges_windows/rpgclassbadges/Badge_png/Badge_paladin.PNG", "paladin shield oath holy guard"),
        Entry("class_necro", KindClassBadge, "Assets/HumbleBundleResources/rpgclassbadges_windows/rpgclassbadges/Badge_png/Badge_necro.png", "necro shadow death forbidden occult"),
        Entry("class_mage", KindClassBadge, "Assets/HumbleBundleResources/rpgclassbadges_windows/rpgclassbadges/Badge_png/Badge_mage.png", "mage spell arcane intelligence mana"),
        Entry("class_hunter", KindClassBadge, "Assets/HumbleBundleResources/rpgclassbadges_windows/rpgclassbadges/Badge_png/Badge_hunter.PNG", "hunter ranger bow wilderness tracking"),
        Entry("class_barbarian", KindClassBadge, "Assets/HumbleBundleResources/rpgclassbadges_windows/rpgclassbadges/Badge_png/Badge_barbarian.png", "barbarian rage strength wild"),
        Entry("class_assassin", KindClassBadge, "Assets/HumbleBundleResources/rpgclassbadges_windows/rpgclassbadges/Badge_png/Badge_assassin.PNG", "assassin rogue stealth strike"),

        Entry("profession_alchemy", KindProfessionBadge, "Assets/HumbleBundleResources/rpgprofessionalbadges_windows/rpgprofessionalbadges/RpgProfessionsBadges_png/alchemy.png", "alchemy potion reagent craft"),
        Entry("profession_carpentry", KindProfessionBadge, "Assets/HumbleBundleResources/rpgprofessionalbadges_windows/rpgprofessionalbadges/RpgProfessionsBadges_png/carpentry.PNG", "carpentry wood craft lumberjack"),
        Entry("profession_diplomacy", KindProfessionBadge, "Assets/HumbleBundleResources/rpgprofessionalbadges_windows/rpgprofessionalbadges/RpgProfessionsBadges_png/diplomacy.PNG", "diplomacy merchant social faction"),
        Entry("profession_farming", KindProfessionBadge, "Assets/HumbleBundleResources/rpgprofessionalbadges_windows/rpgprofessionalbadges/RpgProfessionsBadges_png/farming.png", "farming nature food harvest"),
        Entry("profession_fishing", KindProfessionBadge, "Assets/HumbleBundleResources/rpgprofessionalbadges_windows/rpgprofessionalbadges/RpgProfessionsBadges_png/fishing.PNG", "fishing water patience gather"),
        Entry("profession_hunting", KindProfessionBadge, "Assets/HumbleBundleResources/rpgprofessionalbadges_windows/rpgprofessionalbadges/RpgProfessionsBadges_png/hunting.PNG", "hunting tracker bow wilderness"),
        Entry("profession_mining", KindProfessionBadge, "Assets/HumbleBundleResources/rpgprofessionalbadges_windows/rpgprofessionalbadges/RpgProfessionsBadges_png/mining.png", "mining stone ore gather"),
        Entry("profession_tailoring", KindProfessionBadge, "Assets/HumbleBundleResources/rpgprofessionalbadges_windows/rpgprofessionalbadges/RpgProfessionsBadges_png/tailoring.png", "tailoring cloth armor craft"),

        Entry("faction_shield_blue_01", KindFactionBadge, "Assets/HumbleBundleResources/clanshields_windows/clanshields/ClanShields_png/b_01.png", "blue shield clan faction order"),
        Entry("faction_shield_blue_02", KindFactionBadge, "Assets/HumbleBundleResources/clanshields_windows/clanshields/ClanShields_png/b_02.png", "blue shield clan faction oath"),
        Entry("faction_shield_blue_03", KindFactionBadge, "Assets/HumbleBundleResources/clanshields_windows/clanshields/ClanShields_png/b_03.png", "blue shield clan faction merchant"),
        Entry("faction_banner_01", KindFactionBadge, "Assets/HumbleBundleResources/fantasybanners_windows/fantasybanners/Banners/Banner_01.png", "banner faction quest heraldry"),
        Entry("faction_banner_02", KindFactionBadge, "Assets/HumbleBundleResources/fantasybanners_windows/fantasybanners/Banners/Banner_02.png", "banner faction wild nature"),
        Entry("faction_banner_03", KindFactionBadge, "Assets/HumbleBundleResources/fantasybanners_windows/fantasybanners/Banners/Banner_03.png", "banner faction arcane mystery"),

        Entry("portrait_h_warrior_male", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_h_warrior_male.png", "human warrior male fighter"),
        Entry("portrait_h_warrior_female", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_h_warrior_female.png", "human warrior female fighter"),
        Entry("portrait_h_mage_male", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_h_mage_male.png", "human mage male scholar"),
        Entry("portrait_h_mage_female", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_h_mage_female.png", "human mage female scholar"),
        Entry("portrait_h_rogue_male", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_h_rogue_male.png", "human rogue male stealth"),
        Entry("portrait_h_rogue_female", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_h_rogue_female.png", "human rogue female stealth"),
        Entry("portrait_h_scout_male", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_h_scout_male.png", "human scout male hunter"),
        Entry("portrait_h_miner_male", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_h_miner_male.png", "human miner male worker"),
        Entry("portrait_hermit", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_hermit.png", "hermit elder wanderer npc"),
        Entry("portrait_forest_keeper", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_Forest_Keeper.png", "forest keeper nature npc"),
        Entry("portrait_elf_sentinel_male", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_elf_sentinel_male.png", "elf sentinel male ranger"),
        Entry("portrait_elf_sentinel_female", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_elf_sentinel_female.png", "elf sentinel female ranger"),
        Entry("portrait_dwarf_warrior_male", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_Dwarf_warrior_male.png", "dwarf warrior male"),
        Entry("portrait_dwarf_warrior_female", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_Dwarf_warrior_female.png", "dwarf warrior female"),
        Entry("portrait_dark_knight", KindPortrait, "Assets/HumbleBundleResources/mobsavataricons_windows/mobsavataricons/dark_knight_01.png", "dark knight armored enemy"),
        Entry("portrait_dragon", KindPortrait, "Assets/HumbleBundleResources/mobsavataricons_windows/mobsavataricons/dragon_01.png", "dragon boss fire"),
        Entry("portrait_spectral", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_ghost.png", "spectral spirit ghost undead"),
        Entry("portrait_lich", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_lich.png", "lich undead caster"),
        Entry("portrait_golem", KindPortrait, "Assets/HumbleBundleResources/fantasycharacters_windows/fantasycharacters/Tex_Metal_golem.png", "golem metal construct"),

        Entry("quest_book", KindQuestUi, "Assets/HumbleBundleResources/questjournal_windows/questjournal/book/book.png", "quest journal book"),
        Entry("quest_page", KindQuestUi, "Assets/HumbleBundleResources/questjournal_windows/questjournal/book/page.png", "quest journal page parchment"),
        Entry("quest_scroll", KindQuestUi, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/scroll.png", "quest scroll lore read"),
        Entry("quest_hourglass", KindQuestUi, "Assets/HumbleBundleResources/questjournal_windows/questjournal/illustrations/png/hourglass.png", "quest wait time stillness"),
        Entry("map_tile_nature_01", KindMapTile, "Assets/HumbleBundleResources/maptiles_windows/maptiles/nature/nature_tile_01.PNG", "map nature grass forest"),
        Entry("map_tile_autumn_01", KindMapTile, "Assets/HumbleBundleResources/maptiles_windows/maptiles/autumn/autumn_tile_01.png", "map autumn forest"),
        Entry("map_tile_desert_01", KindMapTile, "Assets/HumbleBundleResources/maptiles_windows/maptiles/desert/desert_tile_01.png", "map desert sand"),
        Entry("map_tile_ice_01", KindMapTile, "Assets/HumbleBundleResources/maptiles_windows/maptiles/ice/ice_tile_01.png", "map ice snow frost"),
        Entry("map_tile_lava_01", KindMapTile, "Assets/HumbleBundleResources/maptiles_windows/maptiles/lava/lava_tile_01.png", "map lava fire"),
        Entry("map_tile_water_01", KindMapTile, "Assets/HumbleBundleResources/maptiles_windows/maptiles/water/water_tile_01.png", "map water river lake")
    };

    public static IReadOnlyList<YQCurated2DArtEntry> Entries => entries;

    public static string[] InventoryIconKeys { get; } = BuildKeys(KindItemIcon);

    public static bool TryGetEntry(string key, out YQCurated2DArtEntry entry)
    {
        // note: Linear search keeps this tiny curated table dependency-free and editor-safe.
        for (int i = 0; i < entries.Length; i++)
        {
            if (string.Equals(entries[i].key, key, StringComparison.OrdinalIgnoreCase))
            {
                entry = entries[i];
                return true;
            }
        }

        entry = default;
        return false;
    }

    public static string PickKey(string kind, string semanticText, string seed, string fallback = "")
    {
        int bestScore = int.MinValue;
        string bestKey = string.Empty;
        string normalizedKind = kind ?? string.Empty;
        string normalizedSemantic = (semanticText ?? string.Empty).ToLowerInvariant();
        string normalizedSeed = seed ?? string.Empty;

        // note: Score entries by semantic tag hits, then use a deterministic hash tie-breaker for variety.
        for (int i = 0; i < entries.Length; i++)
        {
            YQCurated2DArtEntry candidate = entries[i];
            if (!string.Equals(candidate.kind, normalizedKind, StringComparison.OrdinalIgnoreCase))
                continue;

            int score = Score(candidate, normalizedSemantic);
            int tieBreaker = StablePositiveHash(normalizedSeed + ":" + candidate.key) % 997;
            int total = score * 1000 + tieBreaker + candidate.weight;
            if (total > bestScore)
            {
                bestScore = total;
                bestKey = candidate.key;
            }
        }

        return string.IsNullOrWhiteSpace(bestKey) ? fallback : bestKey;
    }

    public static string BuildPromptBlock()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("CURATED_2D_ART_LIBRARY");
        builder.AppendLine("Unity maps visual intent to curated art keys; do not invent asset paths.");
        builder.AppendLine("Supported visual categories: item_icon, class_badge, profession_badge, faction_badge, portrait, quest_ui, map_tile.");
        builder.Append("Useful item_icon keys: ");
        AppendKeys(builder, KindItemIcon, 10);
        builder.AppendLine();
        builder.Append("Useful class_badge keys: ");
        AppendKeys(builder, KindClassBadge, 9);
        builder.AppendLine();
        builder.Append("Useful profession_badge keys: ");
        AppendKeys(builder, KindProfessionBadge, 8);
        builder.AppendLine();
        builder.Append("Useful faction_badge keys: ");
        AppendKeys(builder, KindFactionBadge, 8);
        builder.AppendLine();
        builder.Append("Useful portrait keys: ");
        AppendKeys(builder, KindPortrait, 12);
        builder.AppendLine();
        builder.Append("Useful quest_ui keys: ");
        AppendKeys(builder, KindQuestUi, 8);
        builder.AppendLine();
        builder.Append("Useful map_tile keys: ");
        AppendKeys(builder, KindMapTile, 8);
        builder.AppendLine();
        return builder.ToString();
    }

    private static YQCurated2DArtEntry Entry(string key, string kind, string assetPath, string tags, int weight = 1)
    {
        return new YQCurated2DArtEntry(key, kind, assetPath, tags, weight);
    }

    private static string[] BuildKeys(string kind)
    {
        List<string> keys = new List<string>();
        for (int i = 0; i < entries.Length; i++)
        {
            if (string.Equals(entries[i].kind, kind, StringComparison.OrdinalIgnoreCase))
                keys.Add(entries[i].key);
        }

        return keys.ToArray();
    }

    private static int Score(YQCurated2DArtEntry entry, string semanticText)
    {
        if (string.IsNullOrWhiteSpace(semanticText))
            return 0;

        int score = 0;
        for (int i = 0; i < entry.tags.Length; i++)
        {
            if (semanticText.Contains(entry.tags[i]))
                score++;
        }

        return score;
    }

    private static void AppendKeys(StringBuilder builder, string kind, int maxCount)
    {
        int added = 0;
        for (int i = 0; i < entries.Length && added < maxCount; i++)
        {
            if (!string.Equals(entries[i].kind, kind, StringComparison.OrdinalIgnoreCase))
                continue;

            if (added > 0)
                builder.Append(", ");

            builder.Append(entries[i].key);
            added++;
        }
    }

    private static int StablePositiveHash(string value)
    {
        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];
            return hash & 0x7fffffff;
        }
    }
}
