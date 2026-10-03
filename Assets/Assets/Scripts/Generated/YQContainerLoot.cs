using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public sealed class YQLootEntry
{
    public string id, itemKind, semanticName, assetSemantic, authoredPolicyId, authoredExceptionPolicyId;
    public int weight = 1, minQuantity = 1, maxQuantity = 1, minLevel = 1, maxLevel = 100;
    public bool guaranteed, bonusLoot;
    public string[] requiredTags = Array.Empty<string>();
    public string[] excludedTags = Array.Empty<string>();
    public YQLootRarity? authoredRarity;
}

[Serializable]
public sealed class YQLootProfile
{
    public string id;
    public YQContainerType[] sourceTypes = Array.Empty<YQContainerType>();
    public string[] requiredTags = Array.Empty<string>();
    public int priority, minLevel = 1, maxLevel = 100, minRolls = 1, maxRolls = 3, minGold, maxGold;
    public YQLootRarity maxRarity = YQLootRarity.Rare;
    public int[] rarityWeights = { 75, 20, 5, 0, 0, 0, 0 };
    public List<YQLootEntry> entries = new List<YQLootEntry>();
}

[Serializable]
public sealed class YQLootProfileCatalog
{
    public int generationVersion = YQContainerInventory.GenerationVersion;
    public List<YQLootProfile> profiles = new List<YQLootProfile>();
}

public static class YQContainerLoot
{
    private static YQLootProfileCatalog _catalog;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCatalog() { _catalog = null; }
    public static readonly YQLootEntry FallbackEntry = new YQLootEntry
    { id = "local_supply", itemKind = "supply", semanticName = "Salvage", assetSemantic = "logical_supply" };
    private static readonly int[] RarityCosts = { 0, 1, 3, 6, 10, 12, 20 };
    private static readonly Dictionary<string, string> SemanticPrefabs = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "clothing_legs", "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Trousers.prefab" },
        { "clothing_boots", "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Boot_Left.prefab" },
        { "food", "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_RoundBread.prefab" },
        { "utensil", "Assets/BefourStudios/ContainerDistrict/Art/Prefabs/SM_Spoon.prefab" }
    };

    public static YQLootProfileCatalog Catalog
    {
        get
        {
            if (_catalog != null) return _catalog;
            TextAsset asset = Resources.Load<TextAsset>("YQContainerLootProfiles");
            if (asset == null) throw new InvalidOperationException("Missing YQContainerLootProfiles configuration.");
            var catalog = JsonConvert.DeserializeObject<YQLootProfileCatalog>(asset.text);
            if (!ValidateCatalog(catalog, out string failure)) throw new InvalidOperationException(failure);
            return _catalog = catalog;
        }
    }

    public static bool ValidateCatalog(YQLootProfileCatalog catalog, out string failure)
    {
        failure = "Invalid container loot profile configuration.";
        if (catalog == null || catalog.generationVersion != YQContainerInventory.GenerationVersion || catalog.profiles == null) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (YQLootProfile profile in catalog.profiles)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.id) || !ids.Add(profile.id) || profile.sourceTypes == null || profile.sourceTypes.Length == 0 ||
                profile.requiredTags == null || profile.entries == null || profile.minLevel < 1 || profile.maxLevel < profile.minLevel ||
                profile.minRolls < 0 || profile.maxRolls < profile.minRolls || profile.maxRolls > 32 || profile.minGold < 0 || profile.maxGold < profile.minGold ||
                profile.maxGold > 100000 || !Enum.IsDefined(typeof(YQLootRarity), profile.maxRarity) || profile.rarityWeights == null || profile.rarityWeights.Length != 7) return false;
            foreach (YQContainerType type in profile.sourceTypes) if (!Enum.IsDefined(typeof(YQContainerType), type)) return false;
            foreach (int weight in profile.rarityWeights) if (weight < 0 || weight > 100000) return false;
            if (profile.rarityWeights[4] != 0 || profile.rarityWeights[6] != 0) { failure = "Unique/Mythical cannot have random weights: " + profile.id; return false; }
            var entryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (YQLootEntry entry in profile.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !entryIds.Add(entry.id) || !SupportedKind(entry.itemKind) || entry.weight < 1 || entry.weight > 100000 ||
                    entry.minQuantity < 1 || entry.maxQuantity < entry.minQuantity || entry.maxQuantity > YQContainerInventory.MaxStack ||
                    entry.minLevel < 1 || entry.maxLevel < entry.minLevel || entry.requiredTags == null || entry.excludedTags == null ||
                    (!string.IsNullOrWhiteSpace(entry.assetSemantic) && entry.assetSemantic != "logical_supply" && !SemanticPrefabs.ContainsKey(entry.assetSemantic))) return false;
                if (entry.itemKind != "supply" && entry.itemKind != "consumable" && entry.maxQuantity != 1) return false;
                if (string.IsNullOrWhiteSpace(entry.authoredExceptionPolicyId))
                    foreach (YQContainerType source in profile.sourceTypes)
                    {
                        // note: Nonsensical profile combinations require a deliberate authored exception, never a permissive random fallback.
                        if (source == YQContainerType.Wardrobe && !((entry.itemKind == "legs" && entry.assetSemantic == "clothing_legs") ||
                            (entry.itemKind == "boots" && entry.assetSemantic == "clothing_boots"))) return false;
                        if ((source == YQContainerType.Cupboard || source == YQContainerType.Barrel || source == YQContainerType.Crate) &&
                            entry.itemKind != "consumable" && entry.itemKind != "supply") return false;
                        if (source == YQContainerType.Hostile && entry.itemKind != "supply" && entry.itemKind != "consumable" &&
                            Array.IndexOf(profile.requiredTags, "bandit") < 0 && Array.IndexOf(profile.requiredTags, "humanoid") < 0 &&
                            Array.IndexOf(entry.requiredTags, "humanoid") < 0) return false;
                    }
                if (entry.authoredRarity.HasValue && (!Enum.IsDefined(typeof(YQLootRarity), entry.authoredRarity.Value) ||
                    !entry.guaranteed || string.IsNullOrWhiteSpace(entry.authoredPolicyId) || entry.requiredTags.Length == 0))
                { failure = "Authored rarity requires a guaranteed slot, policy ID and eligibility tags: " + entry.id; return false; }
            }
        }
        failure = ""; return true;
    }

    private static bool SupportedKind(string kind) => Array.IndexOf(new[]
        { "weapon", "offhand", "head", "chest", "gloves", "legs", "boots", "belt", "ring_left", "necklace", "consumable", "supply" }, kind) >= 0;

    public static string SemanticPrefab(string semantic) => semantic != null && SemanticPrefabs.TryGetValue(semantic, out string path) ? path : null;

    public static bool TryImportLegacyOpened(YQContainerRecord source, PlayerState player)
    {
        // note: Import only absent physical storage; a spent legacy receipt is an empty accepted inventory, never a request to roll rewards again.
        if (source == null || source.generated || source.contents.Count != 0 || source.context.sourceType == YQContainerType.Hostile ||
            player?.behaviorCounters == null || !player.behaviorCounters.TryGetValue("loot:opened:" + source.entityId, out float consumed) || consumed <= 0) return false;
        source.generated = true; source.legacyConsumed = true; source.locked = false; source.currency = 0;
        source.generationVersion = YQContainerInventory.GenerationVersion;
        source.seed = YQContainerInventory.Seed(source.context, source.generationVersion);
        source.lootProfileId = "legacy:consumed";
        return true;
    }

    public static bool CompatibleAsset(GeneratedRpgContentService service, InventoryItemRecord item, string semantic)
    {
        if (item == null) return false;
        if (semantic == "logical_supply") return item.itemType == "supply" && !item.IsEquippable;
        string semanticPrefab = SemanticPrefab(semantic);
        if (semanticPrefab != null)
        {
            if (item.prefabKey != semanticPrefab ||
                (semantic == "clothing_legs" && item.equipSlot != "legs") || (semantic == "clothing_boots" && item.equipSlot != "boots") ||
                (semantic == "food" && !item.IsConsumable) || (semantic == "utensil" && item.itemType != "supply")) return false;
        }
        else if (item.IsEquippable)
        {
            GeneratedRpgContentLibrary library = service?.library;
            if (library == null) return false;
            string[] approved = item.equipSlot switch
            {
                "weapon" => library.weaponPrefabKeys, "offhand" => library.offhandPrefabKeys, "head" => library.headPrefabKeys,
                "chest" => library.chestPrefabKeys, "gloves" => library.glovesPrefabKeys, "legs" => library.legsPrefabKeys,
                "boots" => library.bootsPrefabKeys, "belt" => library.beltPrefabKeys,
                "ring_left" => library.ringPrefabKeys, "ring_right" => library.ringPrefabKeys, "necklace" => library.necklacePrefabKeys,
                _ => null
            };
            if (approved == null || Array.IndexOf(approved, item.prefabKey) < 0) return false;
        }
        if (string.IsNullOrWhiteSpace(item.prefabKey) || !item.prefabKey.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return false;
#if UNITY_EDITOR
        // note: Validate approved modular pieces against actual assets in the Editor; runtime uses the same curated semantic allowlist.
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(item.prefabKey);
        if (prefab == null || prefab.GetComponentInChildren<Renderer>(true) == null) return false;
#endif
        return true;
    }

    public static YQContainerContext ResolveContext(WorldState world, string id, YQContainerType type, string regionId,
        int level = 0, EntityInfo entity = null, string siteId = "", string sitePurpose = "", YQContainerContext hints = null)
    {
        // note: Optional explicit hints come from the source's authored contract, never from narrative prose or player progression at open time.
        YQContainerContext context = hints == null ? new YQContainerContext() : YQContainerInventory.Copy(hints);
        context.tags ??= new List<string>(); context.fallbacks ??= new List<string>();
        context.entityId = id; context.sourceType = type; context.regionId = string.IsNullOrWhiteSpace(regionId) ? "region_unknown" : regionId;
        context.worldSeed = world?.generatedWorldPlan?.worldSeed;
        if (string.IsNullOrWhiteSpace(context.worldSeed))
        { context.worldSeed = world?.worldIdentity?.worldSeed ?? "yourquest_default_world"; context.fallbacks.Add("world seed: canonical identity/default"); }
        GeneratedRegionRecord region = world?.generatedWorldPlan?.regions?.Find(r => r != null && r.regionId == context.regionId);
        if (region != null)
        {
            context.progressionTier = Math.Max(1, region.dangerTier);
            foreach (string tag in region.biomeTags ?? new List<string>()) AddTag(context, tag);
            context.biome = region.biomeTags?.Count > 0 ? region.biomeTags[0] : "temperate";
        }
        else { context.progressionTier = Math.Max(1, context.progressionTier); context.fallbacks.Add("region: temperate, tier 1 unless authored"); }
        GeneratedSpatialRegionRecord spatial = world?.generatedWorldPlan?.spatialPlan?.regions?.Find(r => r != null && r.regionId == context.regionId);
        if (spatial != null) { context.regionDanger = Mathf.Clamp01(spatial.danger); if (!string.IsNullOrWhiteSpace(spatial.biome)) context.biome = spatial.biome; }
        if (string.IsNullOrWhiteSpace(context.biome)) context.biome = "temperate";
        AddTag(context, context.biome);
        context.sourceLevel = Mathf.Clamp(level > 0 ? level : (entity != null ? entity.level : context.progressionTier), 1, 100);
        if (level <= 0 && entity == null) context.fallbacks.Add("source level: region tier, minimum 1");
        if (entity != null)
        {
            context.factionId = entity.factionId;
            foreach (string tag in entity.tags ?? Array.Empty<string>()) AddTag(context, tag);
            if (string.IsNullOrWhiteSpace(context.ownerRole))
                foreach (string role in new[] { "bandit", "guard", "scout", "mage", "merchant", "beast", "crawler" })
                    if (context.tags.Contains(role)) { context.ownerRole = role; break; }
        }
        if (!string.IsNullOrWhiteSpace(siteId)) context.siteId = siteId;
        if (!string.IsNullOrWhiteSpace(sitePurpose)) context.sitePurpose = sitePurpose;
        // note: Resolve accepted site/NPC relationships by existing IDs; lore and descriptive reward prose do not select mechanics.
        GeneratedNpcPlanRecord npc = world?.generatedWorldPlan?.generatedNpcs?.Find(n => n != null && n.npcId == id);
        if (npc != null)
        {
            context.siteId = !string.IsNullOrWhiteSpace(npc.encampmentId) ? npc.encampmentId : npc.settlementId;
            context.ownerRole = npc.role;
            if (!string.IsNullOrWhiteSpace(npc.factionId)) context.factionId = npc.factionId;
        }
        GeneratedEncampmentRecord camp = world?.generatedWorldPlan?.encampments?.Find(c => c != null &&
            (c.encampmentId == context.siteId || context.tags.Contains((c.encampmentId ?? "").ToLowerInvariant()) ||
                (!string.IsNullOrWhiteSpace(c.encampmentId) && id.IndexOf(":" + c.encampmentId + ":reward:", StringComparison.Ordinal) >= 0)));
        if (camp != null)
        {
            context.siteId = camp.encampmentId; context.sitePurpose = "encounter";
            context.encounterDifficulty = Math.Max(context.encounterDifficulty, camp.threatTier);
            if (string.IsNullOrWhiteSpace(context.factionId) || context.factionId == "none") context.factionId = camp.inhabitantFactionId;
            AddTag(context, camp.monsterFamily); AddTag(context, camp.kind); AddTag(context, "encounter");
        }
        GeneratedSettlementRecord settlement = world?.generatedWorldPlan?.settlements?.Find(s => s != null && s.settlementId == context.siteId);
        if (settlement != null && (string.IsNullOrWhiteSpace(context.factionId) || context.factionId == "none") && settlement.factionIds?.Count > 0)
            context.factionId = settlement.factionIds[0];
        context.factionId = string.IsNullOrWhiteSpace(context.factionId) ? "none" : context.factionId;
        context.culture = string.IsNullOrWhiteSpace(context.culture) ? "local" : context.culture;
        context.ownerRole = string.IsNullOrWhiteSpace(context.ownerRole) ? "unknown" : context.ownerRole;
        foreach (string role in context.ownerRole.ToLowerInvariant().Split('_'))
            if (Array.IndexOf(new[] { "bandit", "guard", "scout", "mage", "merchant", "beast", "crawler", "humanoid" }, role) >= 0) AddTag(context, role);
        context.sitePurpose = string.IsNullOrWhiteSpace(context.sitePurpose) ? "storage" : context.sitePurpose;
        context.encounterDifficulty = Math.Max(context.encounterDifficulty, context.progressionTier);
        context.wealth = Mathf.Clamp(context.wealth, 0, 10);
        context.rarityBudget = Mathf.Clamp(Math.Max(context.rarityBudget, context.sourceLevel / 2 + context.encounterDifficulty + context.wealth), 0, 100);
        AddTag(context, "region:" + context.regionId); AddTag(context, "faction:" + context.factionId); AddTag(context, context.ownerRole); AddTag(context, context.sitePurpose);
        if (context.element == null)
        {
            context.element = "neutral";
            foreach (string element in new[] { "fire", "ice", "water", "storm", "stone", "ember" })
                if (context.tags.Contains(element)) { context.element = element; break; }
        }
        if (string.IsNullOrWhiteSpace(context.theme)) context.theme = context.element;
        AddTag(context, "culture:" + context.culture); AddTag(context, "element:" + context.element);
        AddTag(context, "theme:" + context.theme); AddTag(context, "source_rarity:" + (context.sourceRarity ?? "common"));
        if (context.factionId.IndexOf("bandit", StringComparison.OrdinalIgnoreCase) >= 0 || context.tags.Contains("bandit")) AddTag(context, "bandit");
        context.fallbacks.Add("culture/role/site/element/wealth: local/unknown/storage/neutral/0 when absent");
        return context;
    }

    private static void AddTag(YQContainerContext context, string tag)
    { if (!string.IsNullOrWhiteSpace(tag) && !context.tags.Contains(tag.Trim().ToLowerInvariant())) context.tags.Add(tag.Trim().ToLowerInvariant()); }
    private static bool TagsMatch(List<string> context, string[] required, string[] excluded = null)
    {
        foreach (string tag in required ?? Array.Empty<string>()) if (!context.Contains(tag)) return false;
        foreach (string tag in excluded ?? Array.Empty<string>()) if (context.Contains(tag)) return false;
        return true;
    }

    public static YQLootProfile ResolveProfile(YQContainerContext context, YQLootProfileCatalog catalog)
    {
        YQLootProfile chosen = null;
        foreach (YQLootProfile profile in catalog.profiles)
            if (Array.IndexOf(profile.sourceTypes, context.sourceType) >= 0 && context.sourceLevel >= profile.minLevel && context.sourceLevel <= profile.maxLevel &&
                TagsMatch(context.tags, profile.requiredTags) && (chosen == null || profile.priority > chosen.priority ||
                    (profile.priority == chosen.priority && string.CompareOrdinal(profile.id, chosen.id) < 0))) chosen = profile;
        return chosen;
    }

    private sealed class RandomStream
    {
        private uint _state;
        public RandomStream(string seed) { _state = Convert.ToUInt32(seed.Substring(0, 8), 16); if (_state == 0) _state = 1; }
        public int Range(int min, int max)
        { unchecked { _state ^= _state << 13; _state ^= _state >> 17; _state ^= _state << 5; } return min + (int)(_state % (uint)(max - min + 1)); }
    }

    public static bool Generate(YQContainerRecord record, GeneratedRpgContentService content, WorldState world, PlayerState player,
        YQLootProfileCatalog catalog, out string failure)
    {
        failure = "Generation requires canonical context and the existing content service.";
        if (record == null || record.context == null || content == null || record.generated) return false;
        YQLootProfile profile = ResolveProfile(record.context, catalog);
        record.generationVersion = catalog.generationVersion;
        record.seed = YQContainerInventory.Seed(record.context, record.generationVersion, record.restockCycle);
        record.lootProfileId = profile?.id ?? "empty:missing-profile";
        var rng = new RandomStream(record.seed);
        var entries = new List<YQLootEntry>();
        if (profile != null)
            foreach (YQLootEntry entry in profile.entries)
                if (record.context.sourceLevel >= entry.minLevel && record.context.sourceLevel <= entry.maxLevel &&
                    TagsMatch(record.context.tags, entry.requiredTags, entry.excludedTags) &&
                    (entry.bonusLoot || !record.equippedItemBySlot.ContainsKey(entry.itemKind))) entries.Add(entry);
        int budget = record.context.rarityBudget, slot = 0;
        if (profile != null)
        {
            foreach (YQLootEntry entry in entries) if (entry.guaranteed) Add(entry);
            var choices = entries.FindAll(e => !e.guaranteed && !e.authoredRarity.HasValue);
            int rolls = rng.Range(profile.minRolls, profile.maxRolls);
            for (int i = 0; i < rolls && choices.Count > 0 && record.contents.Count < record.capacity; i++)
            {
                int total = 0; foreach (YQLootEntry entry in choices) total += entry.weight;
                int roll = rng.Range(1, total);
                foreach (YQLootEntry entry in choices) { roll -= entry.weight; if (roll <= 0) { Add(entry); break; } }
            }
            record.currency = record.context.sourceType == YQContainerType.Wardrobe || record.context.sourceType == YQContainerType.Cupboard ? 0 : rng.Range(profile.minGold, profile.maxGold);
        }
        record.generated = true;
        return YQContainerInventory.ValidateContents(record.contents, record.capacity, out failure);

        void Add(YQLootEntry entry)
        {
            if (record.contents.Count >= record.capacity) return;
            YQLootRarity rarity = entry.authoredRarity ?? RollRarity(profile, rng, budget);
            if (RarityCosts[(int)rarity] > budget) return;
            if ((rarity == YQLootRarity.Unique || rarity == YQLootRarity.Mythical) &&
                (string.IsNullOrWhiteSpace(entry.authoredPolicyId) || AlreadyClaimed(entry.authoredPolicyId, world, player, record))) return;
            string itemSeed = record.seed + "|" + entry.id + "|" + slot++;
            InventoryItemRecord item = content.GenerateContainerItem(itemSeed, record.context, entry, rarity);
            if (item == null) return;
            item.quantity = item.stackable ? rng.Range(entry.minQuantity, entry.maxQuantity) : 1;
            if (entry.authoredRarity.HasValue && item.rarity == rarity.ToString())
            { item.templateId = "authored:" + entry.authoredPolicyId; record.authoredClaimIds.Add(entry.authoredPolicyId); }
            budget -= RarityCosts[(int)rarity];
            record.contents.Add(item);
        }
    }

    private static bool AlreadyClaimed(string policy, WorldState world, PlayerState player, YQContainerRecord next)
    {
        if (next.authoredClaimIds.Contains(policy)) return true;
        // note: Durable source claims survive item removal/consumption; authored Unique/Mythical cannot reappear in another source.
        foreach (YQContainerRecord record in world.containers.Values) if (record?.authoredClaimIds?.Contains(policy) == true) return true;
        return player?.inventoryItems?.Exists(i => i != null && i.templateId == "authored:" + policy) == true;
    }

    private static YQLootRarity RollRarity(YQLootProfile profile, RandomStream rng, int budget)
    {
        int total = 0;
        for (int i = 0; i <= (int)profile.maxRarity; i++) if (i != 4 && i != 6 && RarityCosts[i] <= budget) total += profile.rarityWeights[i];
        if (total == 0) return YQLootRarity.Common;
        int roll = rng.Range(1, total);
        for (int i = 0; i <= (int)profile.maxRarity; i++)
            if (i != 4 && i != 6 && RarityCosts[i] <= budget) { roll -= profile.rarityWeights[i]; if (roll <= 0) return (YQLootRarity)i; }
        return YQLootRarity.Common;
    }

    public static bool TryRestock(WorldState world, PlayerState player, string id, string eventId, GeneratedRpgContentService content, Func<bool> publish, out string failure)
    {
        YQContainerRecord current = YQContainerInventory.Find(world, id);
        failure = "Restock requires an explicit policy, unique event ID, empty storage and no player deposits.";
        if (current == null || current.restock != YQContainerRestock.ExplicitWhenEmpty || !current.generated || current.context.sourceType == YQContainerType.Hostile ||
            current.hasPlayerDeposits || current.contents.Count != 0 || current.currency != 0 || string.IsNullOrWhiteSpace(eventId) || current.restockReceipts.Contains(eventId)) return false;
        YQContainerRecord next = YQContainerInventory.Copy(current);
        next.restockCycle++; next.revision++; next.generated = false;
        next.restockReceipts.Add(eventId);
        if (!Generate(next, content, world, player, Catalog, out failure)) return false;
        return YQContainerInventory.PublishRecord(world, player, next, publish, out failure);
    }
}
