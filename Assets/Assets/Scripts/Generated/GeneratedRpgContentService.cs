// Assets/Assets/Scripts/Generated/GeneratedRpgContentService.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class GeneratedRpgContentService : MonoBehaviour
{
    public const string OriginCompletionCounter = "origin:questionnaire_complete";
    public static GeneratedRpgContentService Instance { get; private set; }

    [Header("Library")]
    public GeneratedRpgContentLibrary library;
    public bool allowPlaceholderGenerationWhenLlmUnavailable = true;

    [Header("Shortcuts")]
    public bool enableInventoryHotkeys = true;

    [Header("Loot")]
    [Range(0f, 1f)] public float enemyLootChance = 0.92f;
    [Range(0f, 1f)] public float bonusConsumableChance = 0.28f;
    public int baseGoldOnKillMin = 4;
    public int baseGoldOnKillMax = 12;

    public string LastInventoryMessage { get; private set; } = string.Empty;

    private static readonly string[] ArmorSlots = { "head", "chest", "gloves", "legs", "boots", "belt", "cloak" };
    private static readonly string[] AccessorySlots = { "ring_left", "ring_right", "earring_left", "earring_right", "necklace", "trinket" };
    private static readonly string[] BaselineEquipmentSlots =
    {
        "weapon", "offhand", "head", "chest", "gloves", "legs", "boots", "belt", "cloak",
        "ring_left", "ring_right", "earring_left", "earring_right", "necklace", "trinket"
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (library == null)
            library = Resources.Load<GeneratedRpgContentLibrary>("GeneratedRpgContentLibrary");
        if (library == null)
            library = ScriptableObject.CreateInstance<GeneratedRpgContentLibrary>();
    }

    private void Update()
    {
        if (!enableInventoryHotkeys || RuntimeModalUiBlocker.IsBlocked)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            UseFirstConsumable();
        if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            CycleEquipSlot("weapon");
        if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            CycleEquipSlot("chest");
        if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            CycleEquipSlot("ring_left");
    }

    public void SetInventoryMessage(string message)
    {
        LastInventoryMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
    }

    public void EnsureBaselineGeneratedState(PlayerState state, WorldState world)
    {
        if (state == null)
            return;

        state.EnsureCollections();
        world?.EnsureCollections();
        YQGeneratedContentCuration.CleanExistingState(state);

        bool originComplete = HasCompletedOrigin(state);
        if (originComplete && state.currency <= 0)
            state.currency = Mathf.Max(25, library != null ? library.starterCurrency : 25);

        RefineExistingGeneratedState(state);
        EnsureInventoryModelKeys(state);
        if (originComplete)
            EnsureBaselineWorld(world);
        RefineExistingGeneratedState(state);

        state.Touch();
        if (world != null)
            world.TouchNow();
    }

    public static bool HasCompletedOrigin(PlayerState state)
    {
        if (state == null)
            return false;

        state.EnsureCollections();
        return state.behaviorCounters.TryGetValue(OriginCompletionCounter, out float completed) && completed > 0f;
    }

    public List<InventoryItemRecord> GrantOriginStartingLoadout(PlayerState state, string directionKey, string stimulus, string[] tags, YQOriginGeneratedItemDto[] generatedLoadout = null)
    {
        List<InventoryItemRecord> granted = new List<InventoryItemRecord>();
        if (state == null)
            return granted;

        state.EnsureCollections();
        string direction = NormalizeOriginDirection(directionKey);
        string joinedTags = tags != null ? string.Join("|", tags) : string.Empty;
        string seed = "origin:" + state.playerId + ":" + direction + ":" + (stimulus ?? string.Empty) + ":" + joinedTags;
        string[] slots = ResolveOriginLoadoutSlots(direction, seed, generatedLoadout);
        int level = Mathf.Max(1, state.level);

        for (int i = 0; i < slots.Length; i++)
        {
            string slot = slots[i];
            YQOriginGeneratedItemDto hint = FindLoadoutHint(generatedLoadout, slot, i);
            InventoryItemRecord item = GenerateItem(seed + ":" + slot + ":" + i, level, slot, false);
            RefineOriginItem(item, direction, stimulus, seed, i, hint);
            state.AddOrUpdateItem(item, true);
            if (item.IsEquippable && state.TryEquipItem(item.itemId, out _))
                state.IncCounter("item:equip", 1f);
            granted.Add(item);
        }

        InventoryItemRecord consumable = GenerateItem(seed + ":first_recovery", level, "consumable", true);
        RefineOriginItem(consumable, direction, stimulus, seed, slots.Length, FindLoadoutHint(generatedLoadout, "consumable", slots.Length));
        consumable.quantity = Mathf.Max(consumable.quantity, 2);
        consumable.stackable = true;
        state.AddOrUpdateItem(consumable, true);
        granted.Add(consumable);

        state.currency = Mathf.Max(state.currency, ResolveOriginCurrency(direction, seed));
        state.IncCounter("origin:equipment_manifested", 1f);
        state.IncCounter("origin:loadout_items", granted.Count);
        state.AddLedgerLine("Starting equipment manifested from the player's stated life direction: " + direction + ".");
        EnsureInventoryModelKeys(state);
        return granted;
    }

    private static string NormalizeOriginDirection(string directionKey)
    {
        string direction = string.IsNullOrWhiteSpace(directionKey) ? "wayfinder" : directionKey.Trim().ToLowerInvariant();
        if (direction.Contains("merchant") || direction.Contains("trade"))
            return "merchant";
        if (direction.Contains("lumber") || direction.Contains("craft") || direction.Contains("wood") || direction.Contains("nature"))
            return "lumberjack";
        if (direction.Contains("demon") || direction.Contains("lord") || direction.Contains("shadow"))
            return "demonlord";
        if (direction.Contains("arcane") || direction.Contains("mage") || direction.Contains("spell"))
            return "arcanist";
        if (direction.Contains("guard") || direction.Contains("warden") || direction.Contains("mercy"))
            return "warden";
        if (direction.Contains("still") || direction.Contains("wait") || direction.Contains("patient"))
            return "stillness";
        if (direction.Contains("hero") || direction.Contains("martial") || direction.Contains("blade"))
            return "hero";
        if (direction.Contains("road") || direction.Contains("wander"))
            return "wanderer";
        return "wayfinder";
    }

    private static string[] ResolveOriginLoadoutSlots(string direction, string seed, YQOriginGeneratedItemDto[] generatedLoadout = null)
    {
        string[] generatedSlots = ReadGeneratedLoadoutSlots(generatedLoadout);
        if (generatedSlots.Length > 0)
            return generatedSlots;

        switch (NormalizeOriginDirection(direction))
        {
            case "merchant":
                return new[] { "weapon", "cloak", "ring_left", "trinket" };
            case "lumberjack":
                return new[] { "weapon", "gloves", "boots", "belt" };
            case "demonlord":
                return new[] { "weapon", "offhand", "cloak", "necklace" };
            case "arcanist":
                return new[] { "offhand", "cloak", "ring_left", "necklace" };
            case "warden":
                return new[] { "weapon", "offhand", "chest", "ring_left" };
            case "stillness":
                return new[] { "offhand", "chest", "trinket", "boots" };
            case "hero":
                return new[] { "weapon", "offhand", "chest", "boots" };
            case "wanderer":
                return new[] { "weapon", "cloak", "boots", "belt" };
            default:
                return Mathf.Abs(StableHash(seed)) % 2 == 0
                    ? new[] { "weapon", "cloak", "boots", "ring_left" }
                    : new[] { "weapon", "offhand", "gloves", "trinket" };
        }
    }

    private void RefineOriginItem(InventoryItemRecord item, string direction, string stimulus, string seed, int index, YQOriginGeneratedItemDto hint)
    {
        if (item == null)
            return;

        string normalized = NormalizeOriginDirection(direction);
        string motif = ResolveOriginMotif(normalized, seed, index);
        string slot = string.IsNullOrWhiteSpace(item.equipSlot) ? item.itemType : item.equipSlot;
        string baseName = BuildOriginItemBaseName(item, seed, index);
        string hintedName = hint != null ? CleanHint(hint.nameHint) : string.Empty;
        item.displayName = !string.IsNullOrWhiteSpace(hintedName)
            ? YQGeneratedContentCuration.CuratePlayerFacingName(PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null, "item", hintedName, "origin", false, stimulus)
            : motif + " " + baseName;
        string hintedDescription = hint != null ? CleanHint(hint.descriptionHint) : string.Empty;
        item.description = YQGeneratedContentCuration.CuratePlayerFacingDescription(
            PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null,
            "item",
            item.displayName,
            string.IsNullOrWhiteSpace(hintedDescription) ? "Manifested at the Goddess threshold when she interpreted the player's intended life direction instead of assigning a fixed tutorial kit." : hintedDescription,
            "origin",
            false,
            string.IsNullOrWhiteSpace(stimulus) ? normalized + " instinct" : stimulus);

        if (string.Equals(slot, "weapon", StringComparison.OrdinalIgnoreCase))
            item.attackBonus = Mathf.Max(item.attackBonus, ResolveOriginAttackBonus(normalized));
        if (string.Equals(slot, "offhand", StringComparison.OrdinalIgnoreCase))
            item.defenseBonus = Mathf.Max(item.defenseBonus, ResolveOriginDefenseBonus(normalized));
        if (item.IsConsumable)
            item.effectKey = string.IsNullOrWhiteSpace(item.effectKey) ? "fx_origin_recovery" : item.effectKey;

        item.familyKey = "origin:" + normalized + ":" + slot;
        item.powerScore = Mathf.Max(item.powerScore, item.attackBonus + item.defenseBonus + item.healthBonus + item.staminaBonus + item.manaBonus);
    }

    private static string[] ReadGeneratedLoadoutSlots(YQOriginGeneratedItemDto[] generatedLoadout)
    {
        if (generatedLoadout == null || generatedLoadout.Length == 0)
            return Array.Empty<string>();

        List<string> slots = new List<string>();
        for (int i = 0; i < generatedLoadout.Length && slots.Count < 6; i++)
        {
            string slot = NormalizeLoadoutSlot(generatedLoadout[i] != null ? generatedLoadout[i].slot : string.Empty);
            if (string.IsNullOrWhiteSpace(slot) || slot == "consumable")
                continue;
            bool duplicate = false;
            for (int s = 0; s < slots.Count; s++)
            {
                if (string.Equals(slots[s], slot, StringComparison.OrdinalIgnoreCase))
                {
                    duplicate = true;
                    break;
                }
            }
            if (!duplicate)
                slots.Add(slot);
        }

        return slots.ToArray();
    }

    private static YQOriginGeneratedItemDto FindLoadoutHint(YQOriginGeneratedItemDto[] generatedLoadout, string slot, int index)
    {
        if (generatedLoadout == null || generatedLoadout.Length == 0)
            return null;

        string normalizedSlot = NormalizeLoadoutSlot(slot);
        for (int i = 0; i < generatedLoadout.Length; i++)
        {
            YQOriginGeneratedItemDto hint = generatedLoadout[i];
            if (hint == null)
                continue;
            if (string.Equals(NormalizeLoadoutSlot(hint.slot), normalizedSlot, StringComparison.OrdinalIgnoreCase))
                return hint;
        }

        return index >= 0 && index < generatedLoadout.Length ? generatedLoadout[index] : null;
    }

    private static string NormalizeLoadoutSlot(string slot)
    {
        string clean = string.IsNullOrWhiteSpace(slot) ? string.Empty : slot.Trim().ToLowerInvariant().Replace(" ", "_");
        if (clean == "mainhand" || clean == "main_hand")
            return "weapon";
        if (clean == "shield" || clean == "focus")
            return "offhand";
        if (clean == "armor")
            return "chest";
        if (clean == "ring")
            return "ring_left";
        if (clean == "earring")
            return "earring_left";
        if (clean == "relic" || clean == "charm")
            return "trinket";
        if (clean == "potion" || clean == "tonic")
            return "consumable";
        if (clean == "weapon" || clean == "offhand" || clean == "head" || clean == "chest" || clean == "gloves" ||
            clean == "legs" || clean == "boots" || clean == "belt" || clean == "cloak" || clean == "ring_left" ||
            clean == "ring_right" || clean == "earring_left" || clean == "earring_right" || clean == "necklace" ||
            clean == "trinket" || clean == "consumable")
            return clean;
        return string.Empty;
    }

    private static string CleanHint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        string clean = value.Trim();
        return clean.Length <= 120 ? clean : clean.Substring(0, 120).TrimEnd();
    }

    private static string ResolveOriginMotif(string direction, string seed, int index)
    {
        switch (NormalizeOriginDirection(direction))
        {
            case "merchant": return Pick(new[] { "Keenhand", "Risk-Read", "Roadledger" }, "Keenhand", seed, 201 + index);
            case "lumberjack": return Pick(new[] { "First Green", "Auralith", "Rootwake" }, "First Green", seed, 211 + index);
            case "demonlord": return Pick(new[] { "Black-Crown", "Abyssal", "Sovereign" }, "Sovereign", seed, 221 + index);
            case "arcanist": return Pick(new[] { "Threshold", "Runebound", "Starsealed" }, "Threshold", seed, 231 + index);
            case "warden": return Pick(new[] { "Oathbound", "Mercyguard", "Shieldwake" }, "Oathbound", seed, 241 + index);
            case "stillness": return Pick(new[] { "Stone-Quiet", "Stillwake", "Unmoving" }, "Stone-Quiet", seed, 251 + index);
            case "hero": return Pick(new[] { "Linebreaker", "Dawnsworn", "First-Risk" }, "Linebreaker", seed, 261 + index);
            case "wanderer": return Pick(new[] { "Wayfarer", "Roadsalt", "Farstep" }, "Wayfarer", seed, 271 + index);
            default: return Pick(new[] { "Wayfinder", "Threshold", "First-Risk" }, "Wayfinder", seed, 281 + index);
        }
    }

    private static string BuildOriginItemBaseName(InventoryItemRecord item, string seed, int index)
    {
        if (item == null)
            return "Gear";

        string slot = string.IsNullOrWhiteSpace(item.equipSlot) ? item.itemType : item.equipSlot;
        switch ((slot ?? string.Empty).ToLowerInvariant())
        {
            case "weapon":
                // note: Origin item names mirror the imported weapon forms now available in the equipment library.
                return Pick(new[] { "Blade", "Sword", "Axe", "Mace", "Dagger", "Spear", "Staff", "Bow", "Crossbow", "Scythe", "Throwing Axe" }, "Blade", seed, 301 + index);
            case "offhand": return Pick(new[] { "Ward", "Focus", "Ledger", "Buckler", "Shield" }, "Ward", seed, 311 + index);
            case "head": return "Hood";
            case "chest": return Pick(new[] { "Coat", "Harness", "Mail" }, "Coat", seed, 321 + index);
            case "gloves": return "Gloves";
            case "legs": return "Greaves";
            case "boots": return "Boots";
            case "belt": return "Belt";
            case "cloak": return "Cloak";
            case "necklace": return "Necklace";
            case "trinket": return Pick(new[] { "Charm", "Token", "Stone", "Amulet", "Gem", "Bracer" }, "Charm", seed, 331 + index);
            case "ring_left":
            case "ring_right": return "Ring";
            default: return item.IsConsumable ? Pick(new[] { "Tonic", "Draught", "Vial" }, "Tonic", seed, 341 + index) : "Gear";
        }
    }

    private static int ResolveOriginAttackBonus(string direction)
    {
        switch (NormalizeOriginDirection(direction))
        {
            case "hero": return 8;
            case "demonlord": return 7;
            case "lumberjack": return 7;
            case "merchant": return 5;
            default: return 6;
        }
    }

    private static int ResolveOriginDefenseBonus(string direction)
    {
        switch (NormalizeOriginDirection(direction))
        {
            case "warden": return 6;
            case "stillness": return 5;
            case "hero": return 4;
            default: return 3;
        }
    }

    private static int ResolveOriginCurrency(string direction, string seed)
    {
        int baseAmount = NormalizeOriginDirection(direction) == "merchant" ? 42 : 18;
        return baseAmount + Mathf.Abs(StableHash(seed + ":coin")) % 17;
    }

    private void EnsureBaselineEquipment(PlayerState state)
    {
        int level = Mathf.Max(1, state.level);
        for (int i = 0; i < BaselineEquipmentSlots.Length; i++)
        {
            string slot = BaselineEquipmentSlots[i];
            if (HasInventoryItemForSlot(state, slot))
                continue;

            InventoryItemRecord item = GenerateItem("baseline:" + slot + ":" + state.playerId, level, slot, false);
            AddStarter(state, item);
        }

        if (state.FindFirstConsumable() == null)
        {
            InventoryItemRecord consumable = GenerateItem("baseline:consumable:" + state.playerId, level, "consumable", true);
            consumable.quantity = Mathf.Max(consumable.quantity, library != null ? library.starterConsumableStacks : 3);
            consumable.stackable = true;
            state.AddOrUpdateItem(consumable, true);
        }

        for (int i = 0; i < BaselineEquipmentSlots.Length; i++)
        {
            string slot = BaselineEquipmentSlots[i];
            if (state.GetEquippedItem(slot) != null)
                continue;

            InventoryItemRecord item = FindBestItemForSlot(state, slot);
            if (item != null)
                state.TryEquipItem(item.itemId, out _);
        }
    }

    private void EnsureBaselineSkills(PlayerState state)
    {
        if (allowPlaceholderGenerationWhenLlmUnavailable && FindFirstSkill(state, false) == null)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SkillRecord active = new SkillRecord
            {
                skillId = Guid.NewGuid().ToString("N"),
                familyId = "baseline_active_family",
                rank = 1,
                unlocked = true,
                context = "player_response:combat",
                environment = "player_profile",
                learnedUnix = now,
                acquiredUnix = now,
                name = "Linebreaker Strike",
                type = "combat",
                tier = 1,
                description = YQGeneratedContentCuration.CuratePlayerFacingDescription(
                    state,
                    "skill",
                    "Linebreaker Strike",
                    "A disciplined opening strike that turns your first forward pressure into range, timing, and control.",
                    "combat",
                    false,
                    "your habit of meeting danger head-on"),
                isSpell = false
            };

            state.UpsertSkill(active);
        }

        if (allowPlaceholderGenerationWhenLlmUnavailable && FindFirstSkill(state, true) == null)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string spellName = ResolveStarterSpellName(state);
            SkillRecord spell = new SkillRecord
            {
                skillId = Guid.NewGuid().ToString("N"),
                familyId = "baseline_spell_family",
                rank = 1,
                unlocked = true,
                context = "player_response:control",
                environment = "player_profile",
                learnedUnix = now,
                acquiredUnix = now,
                name = spellName,
                type = "spell",
                tier = 1,
                description = YQGeneratedContentCuration.CuratePlayerFacingDescription(
                    state,
                    "spell",
                    spellName,
                    "A short-range pulse keyed to your origin answers, released when a threat crowds your breathing room.",
                    "spell",
                    true,
                    "your instinct to answer pressure with shaped mana"),
                isSpell = true
            };

            state.UpsertSkill(spell);
        }

        if (!HasEquippedSkill(state, "active"))
        {
            SkillRecord active = FindFirstSkill(state, false);
            if (active != null)
                state.equippedSkillBySlot["active"] = active.skillId;
        }

        if (!HasEquippedSkill(state, "spell"))
        {
            SkillRecord spell = FindFirstSkill(state, true);
            if (spell != null)
                state.equippedSkillBySlot["spell"] = spell.skillId;
        }
    }

    private void EnsureBaselineIdentity(PlayerState state)
    {
        if (state.classes.Count == 0)
        {
            string className = BuildPlayerClassName(state);
            state.AwardClass(className, YQGeneratedContentCuration.CuratePlayerFacingDescription(
                state,
                "class",
                className,
                "A curated origin class shaped by your answers, repeated choices, and early survival habits.",
                "identity",
                false,
                "the pattern already visible in your first decisions"));
        }

        if (state.titles.Count == 0)
        {
            string qualifier = ResolvePlayerTitleQualifier(state);
            state.AwardTitle("The " + qualifier, YQGeneratedContentCuration.CuratePlayerFacingDescription(
                state,
                "title",
                "The " + qualifier,
                "A starting title earned from the way your first answers and choices keep repeating under pressure.",
                "identity",
                false,
                "your first repeated choices"));
        }
    }

    private void EnsureBaselineQuestState(PlayerState state)
    {
        EnsureBaselineQuest(
            state,
            "Wake at the Goddess Threshold",
            "Approach Archivist Vey beside the witch hut and Goddess statue, then prove one first action the Archive can measure. The Goddess opened the threshold; Vey records what you actually do with it.",
            "origin",
            "your first measured action after waking at the Goddess threshold",
            new[] { "origin", "goddess", "forest", "archivist", "tutorial" });

        EnsureBaselineQuest(
            state,
            "Read the Four Thresholds",
            "Stand at the four-road clearing and choose a first pressure: north guard, east advance, south survival, or west repositioning. The road is context; the outcome belongs to your response.",
            "exploration",
            "your decision about which pressure to face first",
            new[] { "origin", "exploration", "four_roads", "tutorial" });

        EnsureBaselineQuest(
            state,
            "Hold the Frostglass Oath",
            "Meet Warden Thorne on the north road, defeat a frost hostile, and claim the Frostglass Ward as proof that pressure made you steadier instead of louder.",
            "combat",
            "your guard, counter, or retreat under north-road pressure",
            new[] { "cardinal", "north", "frost", "warden", "combat", "item" });

        EnsureBaselineQuest(
            state,
            "Temper the Cinder Vow",
            "Meet Cinder Prefect Mael on the east road, survive an ember monster, and take the Cinder Trial Blade only if your aggression stays repeatable after impact.",
            "combat",
            "your repeated forward pressure after danger answers back",
            new[] { "cardinal", "east", "fire", "cinder", "combat", "weapon" });

        EnsureBaselineQuest(
            state,
            "Answer Auralith's Root",
            "Meet Root-Sibyl Ivara on the south road, face a living-terrain monster, and recover the Auralith Seed Charm. The First Green is ancient, but any skill born here must name your survival pattern.",
            "nature",
            "your survival instinct around living terrain",
            new[] { "cardinal", "south", "nature", "auralith", "combat", "trinket" });

        EnsureBaselineQuest(
            state,
            "Map the Tideglass Step",
            "Meet Tide Cartographer Sera on the west road, defeat a shore monster, and claim the Tideglass Step Boots as proof of how you recover when the floor changes.",
            "movement",
            "your repositioning and recovery when footing changes",
            new[] { "cardinal", "west", "water", "tide", "movement", "boots" });
    }

    private static void EnsureBaselineQuest(PlayerState state, string name, string description, string context, string stimulus, string[] tags)
    {
        if (state == null || string.IsNullOrWhiteSpace(name))
            return;

        string curatedDescription = YQGeneratedContentCuration.CuratePlayerFacingDescription(
            state,
            "quest",
            name,
            description,
            context,
            false,
            stimulus);
        string[] curatedTags = YQGeneratedContentCuration.BuildPlayerResponseTags(tags, context, false, name + " " + description + " " + stimulus);
        state.OfferQuest(name, curatedDescription, curatedTags);
    }

    private void EnsureBaselineOffers(PlayerState state)
    {
        if (state.GetPendingOfferCount() > 0)
            return;

        PendingProgressionOfferRecord offer = new PendingProgressionOfferRecord
        {
            offerId = Guid.NewGuid().ToString("N"),
            offerKind = "skill",
            offerState = "pending",
            name = "Linebreaker Recovery",
            description = YQGeneratedContentCuration.CuratePlayerFacingDescription(
                state,
                "skill",
                "Linebreaker Recovery",
                "A combat movement discipline that converts your hard advances into faster recovery after a strike or dodge.",
                "movement",
                false,
                "your early habit of pushing forward then needing room to reset"),
            confidence = 0.84f,
            reason = "Queued because your early movement and combat rhythm already show a recoverable advance pattern.",
            isUpgrade = false,
            proposedTier = 1,
            isSpell = false,
            skillType = "utility",
            context = "player_response:movement",
            environment = "player_profile",
            tags = YQGeneratedContentCuration.BuildPlayerResponseTags(new[] { "baseline", "tutorial" }, "movement", false, "linebreaker recovery"),
            offeredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        state.QueueOrRefreshOffer(offer);
    }

    private void EnsureBaselineWorld(WorldState world)
    {
        if (world == null)
            return;

        RemoveCanonLinesContaining(world, "no local NPCs", "quiet by design", "preview gate");
        world.AppendCanon("The player wakes between an ancient Goddess statue and Archivist Vey's witch hut after the Goddess opens the threshold between mortal instinct and the old system.");
        world.AppendCanon("Archivist Vey curates the tutorial as proof, not prophecy: every skill, spell, title, and quest should answer what the player actually does.");
        world.AppendCanon("The four cardinal roads are controlled trial frontiers: north tests guard, east tests advance, south tests survival under Auralith's living terrain, and west tests recovery when footing changes.");
        world.AppendCanon("Auralith, the First Green, is remembered as the old natural precursor behind root, thorn, poison, shelter, and living-terrain instincts.");
        world.AppendCanon("The Archive of First Roads names the player's response before it names any place; region names are pressure, not destiny.");

        EnsureFaction(world, "origin_archivists", 0.55f, "Archivist Vey's order records player evidence at the Goddess threshold.");
        EnsureFaction(world, "first_gate_wardens", 0.35f, "North-road wardens respect calm proof under frost pressure.");
        EnsureFaction(world, "cinder_vanguard", 0.05f, "The cinder vanguard respects repeatable courage more than bravado.");
        EnsureFaction(world, "auralith_keepers", 0.20f, "The keepers remember Auralith as a godlike precursor of living terrain.");
        EnsureFaction(world, "tide_cartographers", 0.20f, "The tide cartographers map people by how they recover when the world shifts.");
        EnsureFaction(world, "mimics", -0.35f, "Mimics wait around old caches and punish careless looters.");
        EnsureFaction(world, "frost_wilds", -0.25f, "Frost monsters pressure guard, timing, and retreat.");
        EnsureFaction(world, "ember_wilds", -0.30f, "Ember monsters punish greedy advances.");
        EnsureFaction(world, "verdant_wilds", -0.22f, "Living-terrain monsters test survival around root, spore, and thorn.");
        EnsureFaction(world, "tide_wilds", -0.22f, "Shore monsters test recovery and repositioning.");

        EnsureLocation(world, "origin_forest", 1f, "stable", "The Goddess statue and Vey's witch hut anchor the tutorial clearing.");
        EnsureLocation(world, "region_ice_north", 1f, "dangerous", "North Road: Frostglass Reach hardens into a guard and counter trial.");
        EnsureLocation(world, "region_fire_east", 1f, "contested", "East Road: Cinderfall Crucible burns away reckless aggression.");
        EnsureLocation(world, "region_jungle_south", 1f, "wild", "South Road: Auralith Root grows with ancient living-terrain pressure.");
        EnsureLocation(world, "region_water_west", 1f, "unstable", "West Road: Tideglass Step shifts under movement and recovery choices.");

        EnsureWorldNpc(world, "npc_archivist_01", "Archivist Vey", "origin_archivists", "origin_forest", "A warm but exact archivist who records the player's proven actions beside her witch hut and the Goddess statue.");
        EnsureWorldNpc(world, "npc_warden_01", "Warden Thorne", "first_gate_wardens", "region_ice_north", "A terse frost-road mentor who watches whether pressure makes the player guard, counter, or flee.");
        EnsureWorldNpc(world, "npc_cinder_01", "Cinder Prefect Mael", "cinder_vanguard", "region_fire_east", "A severe east-road mentor who only respects courage that stays repeatable after impact.");
        EnsureWorldNpc(world, "npc_root_sibyl_01", "Root-Sibyl Ivara", "auralith_keepers", "region_jungle_south", "A south-road sibyl who speaks for Auralith's ancient living-terrain memory without stealing the player's agency.");
        EnsureWorldNpc(world, "npc_tide_cartographer_01", "Tide Cartographer Sera", "tide_cartographers", "region_water_west", "A west-road cartographer who studies how the player recovers footing when the world changes.");
    }

    private static void RemoveCanonLinesContaining(WorldState world, params string[] needles)
    {
        if (world == null || string.IsNullOrWhiteSpace(world.canonLedger) || needles == null || needles.Length == 0)
            return;

        string[] split = world.canonLedger.Replace("\r", string.Empty).Split('\n');
        List<string> kept = new List<string>(split.Length);
        for (int i = 0; i < split.Length; i++)
        {
            string line = split[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            bool remove = false;
            for (int n = 0; n < needles.Length; n++)
            {
                if (!string.IsNullOrWhiteSpace(needles[n]) && line.IndexOf(needles[n], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    remove = true;
                    break;
                }
            }

            if (!remove)
                kept.Add(line.Trim());
        }

        world.canonLedger = string.Join("\n", kept);
    }

    private static void EnsureFaction(WorldState world, string factionId, float attitude, string status)
    {
        if (world == null || string.IsNullOrWhiteSpace(factionId))
            return;

        world.EnsureCollections();
        for (int i = 0; i < world.factions.Count; i++)
        {
            WorldState.FactionRecord record = world.factions[i];
            if (record != null && string.Equals(record.factionId, factionId, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(record.status))
                    record.status = status;
                return;
            }
        }

        world.ApplyFactionDelta(factionId, "set", attitude, status);
    }

    private static void EnsureLocation(WorldState world, string locationId, float importance, string state, string text)
    {
        if (world == null || string.IsNullOrWhiteSpace(locationId))
            return;

        world.EnsureCollections();
        for (int i = 0; i < world.locations.Count; i++)
        {
            WorldState.LocationRecord record = world.locations[i];
            if (record != null && string.Equals(record.locationId, locationId, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(record.state))
                    record.state = state;
                if (string.IsNullOrWhiteSpace(record.text))
                    record.text = text;
                return;
            }
        }

        world.ApplyLocationDelta(locationId, "set", importance, state, text);
    }

    private static void EnsureWorldNpc(WorldState world, string npcId, string name, string factionId, string locationId, string description)
    {
        if (world == null || string.IsNullOrWhiteSpace(npcId))
            return;

        world.EnsureCollections();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (int i = 0; i < world.npcs.Count; i++)
        {
            WorldState.NpcRecord record = world.npcs[i];
            if (record == null || !string.Equals(record.npcId, npcId, StringComparison.OrdinalIgnoreCase))
                continue;

            record.name = name;
            record.factionId = factionId;
            record.locationId = locationId;
            record.description = description;
            record.status = string.IsNullOrWhiteSpace(record.status) ? "available" : record.status;
            record.updatedUnix = now;
            return;
        }

        world.npcs.Add(new WorldState.NpcRecord
        {
            npcId = npcId,
            name = name,
            factionId = factionId,
            locationId = locationId,
            description = description,
            affinityToPlayer = 0f,
            status = "available",
            createdUnix = now,
            updatedUnix = now
        });
    }

    public void GrantEnemyLoot(YQInvestorEnemy enemy)
    {
        PlayerStateManager manager = PlayerStateManager.Instance;
        if (manager == null || manager.state == null)
            return;

        PlayerState state = manager.state;
        state.EnsureCollections();

        int gold = UnityEngine.Random.Range(baseGoldOnKillMin, baseGoldOnKillMax + 1) + Mathf.Max(0, state.level - 1);
        state.currency += gold;

        InventoryItemRecord mainDrop = null;
        InventoryItemRecord bonusDrop = null;
        string context = enemy != null
            ? enemy.semanticRegionId + ":" + enemy.displayName + ":loot:" + state.behaviorCounters.Count + ":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            : "enemy_loot:" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (UnityEngine.Random.value <= enemyLootChance)
        {
            mainDrop = GenerateItem(context, Mathf.Max(1, state.level), PickLootKind(enemy), false);
            state.AddOrUpdateItem(mainDrop, true);
        }

        if (UnityEngine.Random.value <= bonusConsumableChance)
        {
            bonusDrop = GenerateItem(context + ":bonus", Mathf.Max(1, state.level), "consumable", true);
            state.AddOrUpdateItem(bonusDrop, true);
        }

        state.AddLedgerLine("The player recovered spoils from a fallen foe.");
        state.IncCounter("loot:enemy", 1f);

        if (manager.autosave)
            manager.Save();

        if (mainDrop != null && bonusDrop != null)
            LastInventoryMessage = "Looted " + mainDrop.displayName + ", " + bonusDrop.displayName + ", and " + gold + " gold.";
        else if (mainDrop != null)
            LastInventoryMessage = "Looted " + mainDrop.displayName + " and " + gold + " gold.";
        else if (bonusDrop != null)
            LastInventoryMessage = "Looted " + bonusDrop.displayName + " and " + gold + " gold.";
        else
            LastInventoryMessage = "Recovered " + gold + " gold.";
    }

    public InventoryItemRecord GenerateItem(string contextKey, int level, string preferredKind = null, bool forceConsumable = false)
    {
        int effectiveLevel = Mathf.Max(1, level);
        string kind = forceConsumable ? "consumable" : ResolveItemKind(contextKey, preferredKind);
        string rarity = Pick(library != null ? library.itemRarities : null, "Common", contextKey, 2 + effectiveLevel);
        string material = Pick(library != null ? library.itemMaterials : null, "Traveler", contextKey, 0);
        PlayerState ownerState = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        string theme = PlayerFlavor(ownerState, contextKey, kind);

        InventoryItemRecord record = new InventoryItemRecord();
        record.itemId = Guid.NewGuid().ToString("N");
        record.templateId = BuildTemplateId(contextKey, kind, rarity, material);
        record.rarity = rarity;
        record.quantity = 1;
        record.generatedAtUnixString = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        record.iconKey = PickCuratedItemIconKey(record, kind, contextKey, "item_weapon_blade");
        record.prefabKey = Pick(library != null ? library.prefabKeys : null, "prefab_placeholder_weapon", contextKey, 17);
        record.effectKey = Pick(library != null ? library.effectKeys : null, "fx_placeholder_restore", contextKey, 23);

        switch (kind)
        {
            case "weapon":
                record.itemType = "weapon";
                record.equipSlot = "weapon";
                record.displayName = material + " " + theme + " " + Pick(library != null ? library.weaponBases : null, "Blade", contextKey, 1);
                record.description = "A main-hand weapon selected for your current fighting rhythm and level.";
                record.attackBonus = Mathf.Max(2, (library != null ? library.baselineWeaponAttack : 4) + effectiveLevel);
                record.powerScore = record.attackBonus;
                break;

            case "offhand":
                record.itemType = "offhand";
                record.equipSlot = "offhand";
                record.displayName = material + " " + theme + " Ward";
                record.description = "An offhand focus tuned to how you recover space and stabilize pressure.";
                record.defenseBonus = Mathf.Max(1, (library != null ? library.baselineArmorDefense : 3) + effectiveLevel / 2);
                record.manaBonus = 4 + effectiveLevel * 2;
                record.powerScore = record.defenseBonus + record.manaBonus;
                break;

            case "head":
            case "chest":
            case "gloves":
            case "legs":
            case "boots":
            case "belt":
            case "cloak":
                record.itemType = "armor";
                record.equipSlot = kind;
                record.displayName = material + " " + theme + " " + BuildArmorName(kind);
                record.description = "A fitted armor piece chosen for the way you are taking hits, moving, and surviving.";
                record.defenseBonus = Mathf.Max(1, (library != null ? library.baselineArmorDefense : 3) + effectiveLevel / 2 + ArmorSlotWeight(kind));
                record.healthBonus = (kind == "chest" || kind == "legs") ? effectiveLevel * 4 : effectiveLevel * 2;
                record.staminaBonus = (kind == "boots" || kind == "gloves" || kind == "belt") ? effectiveLevel * 2 : 0;
                record.moveSpeedBonus = kind == "boots" ? 0.01f * effectiveLevel : 0f;
                record.powerScore = record.defenseBonus + record.healthBonus + record.staminaBonus + Mathf.RoundToInt(record.moveSpeedBonus * 100f);
                break;

            case "ring":
            case "ring_left":
            case "ring_right":
            case "earring":
            case "earring_left":
            case "earring_right":
            case "necklace":
            case "trinket":
                string accessorySlot = ResolveAccessorySlot(kind, contextKey);
                record.itemType = "trinket";
                record.equipSlot = accessorySlot;
                record.displayName = material + " " + theme + " " + BuildAccessoryName(accessorySlot);
                record.description = "An accessory tuned to the utility, control, or sustain your current build keeps asking for.";
                record.manaBonus = accessorySlot == "necklace" ? Mathf.Max(6, (library != null ? library.baselineTrinketMana : 10) + effectiveLevel) : 2 + effectiveLevel;
                record.attackBonus = accessorySlot.StartsWith("ring", StringComparison.OrdinalIgnoreCase) ? 1 + effectiveLevel / 2 : 0;
                record.defenseBonus = accessorySlot.StartsWith("earring", StringComparison.OrdinalIgnoreCase) ? 1 + effectiveLevel / 2 : 0;
                record.staminaBonus = accessorySlot == "trinket" ? effectiveLevel * 2 : 0;
                record.powerScore = record.attackBonus + record.defenseBonus + record.manaBonus + record.staminaBonus;
                break;

            default:
                record.itemType = "consumable";
                record.equipSlot = string.Empty;
                record.stackable = true;
                record.displayName = theme + " " + Pick(library != null ? library.consumableBases : null, "Tonic", contextKey, 3);
                record.description = "A field consumable prepared for your next recovery window: health, stamina, and mana in one quick use.";
                record.healAmount = 18 + effectiveLevel * 4;
                record.restoreStaminaAmount = 12 + effectiveLevel * 3;
                record.restoreManaAmount = 10 + effectiveLevel * 3;
                record.quantity = library != null ? Mathf.Max(1, library.starterConsumableStacks) : 3;
                break;
        }

        ApplyTypedAssetKeys(record, contextKey);
        record.familyKey = kind + ":" + theme.ToLowerInvariant();
        ApplyElementalEffectKey(record);
        return record;
    }

    private void EnsureInventoryModelKeys(PlayerState state)
    {
        if (state == null || state.inventoryItems == null || library == null)
            return;

        bool changed = false;
        for (int i = 0; i < state.inventoryItems.Count; i++)
        {
            InventoryItemRecord item = state.inventoryItems[i];
            if (item == null)
                continue;

            string contextKey = "inventory_repair:" + i + ":" + (item.templateId ?? string.Empty) + ":" + (item.displayName ?? string.Empty);
            if (NeedsPrefabKeyRepair(item.prefabKey))
            {
                string prefabKey = ResolvePrefabKeyForItem(item, contextKey);
                if (!string.IsNullOrWhiteSpace(prefabKey) && !string.Equals(prefabKey, item.prefabKey, StringComparison.OrdinalIgnoreCase))
                {
                    item.prefabKey = prefabKey;
                    changed = true;
                }
            }

            if (NeedsEffectKeyRepair(item.effectKey))
            {
                string effectKey = ResolveEffectKeyForItem(item, contextKey);
                if (!string.IsNullOrWhiteSpace(effectKey) && !string.Equals(effectKey, item.effectKey, StringComparison.OrdinalIgnoreCase))
                {
                    item.effectKey = effectKey;
                    changed = true;
                }
            }

            string previousEffect = item.effectKey;
            string previousDescription = item.description;
            ApplyElementalEffectKey(item);
            changed |= !string.Equals(previousEffect, item.effectKey, StringComparison.OrdinalIgnoreCase) ||
                       !string.Equals(previousDescription, item.description, StringComparison.Ordinal);
        }

        if (changed)
            state.Touch();
    }

    private string ResolvePrefabKeyForItem(InventoryItemRecord item, string contextKey)
    {
        if (item == null || library == null)
            return item != null ? item.prefabKey : string.Empty;

        string type = (item.itemType ?? string.Empty).Trim().ToLowerInvariant();
        string slot = (item.equipSlot ?? string.Empty).Trim().ToLowerInvariant();

        if (type == "weapon" || slot == "weapon")
            return PickModelPrefab(library.weaponPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_weapon", contextKey, 17), contextKey, 117);
        if (type == "offhand" || slot == "offhand")
            return PickModelPrefab(library.offhandPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_offhand", contextKey, 17), contextKey, 127);
        if (type == "armor" || IsArmorSlot(slot))
            return PickAssetForArmorSlot(slot, contextKey);
        if (type == "trinket" || IsAccessorySlot(slot))
            return PickAssetForAccessorySlot(slot, contextKey);
        if (item.IsConsumable)
            return PickModelPrefab(library.trinketPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_consumable", contextKey, 17), contextKey, 133);

        return PickModelPrefab(library.trinketPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_item", contextKey, 17), contextKey, 199);
    }

    private string ResolveEffectKeyForItem(InventoryItemRecord item, string contextKey)
    {
        if (item == null || library == null)
            return item != null ? item.effectKey : string.Empty;

        string type = (item.itemType ?? string.Empty).Trim().ToLowerInvariant();
        string slot = (item.equipSlot ?? string.Empty).Trim().ToLowerInvariant();
        if (type == "weapon" || slot == "weapon")
            return PickRuntimeEffectKey(library.meleeAudioKeys, "fx_placeholder_slash", contextKey, 123);
        if (type == "offhand" || slot == "offhand")
            return PickRuntimeEffectKey(library.shieldEffectKeys, "fx_placeholder_guard", contextKey, 129);
        if (type == "armor" || IsArmorSlot(slot))
            return PickRuntimeEffectKey(library.effectKeys, "fx_placeholder_guard", contextKey, 23);
        if (type == "trinket" || IsAccessorySlot(slot))
            return PickRuntimeEffectKey(library.magicAudioKeys, "fx_placeholder_arcane", contextKey, 131);
        if (item.IsConsumable)
            return PickRuntimeEffectKey(library.magicAudioKeys, "fx_placeholder_restore", contextKey, 137);

        return PickRuntimeEffectKey(library.effectKeys, "fx_placeholder_arcane", contextKey, 23);
    }

    private void ApplyTypedAssetKeys(InventoryItemRecord record, string contextKey)
    {
        if (record == null || library == null)
            return;

        switch (record.itemType)
        {
            case "weapon":
                record.iconKey = PickCuratedItemIconKey(record, "weapon", contextKey, "item_weapon_blade");
                record.prefabKey = PickModelPrefab(library.weaponPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_weapon", contextKey, 17), contextKey, 117);
                record.effectKey = PickRuntimeEffectKey(library.meleeAudioKeys, PickRuntimeEffectKey(library.effectKeys, "fx_placeholder_slash", contextKey, 23), contextKey, 123);
                break;

            case "offhand":
                record.iconKey = PickCuratedItemIconKey(record, "offhand", contextKey, "item_offhand_shield");
                record.prefabKey = PickModelPrefab(library.offhandPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_offhand", contextKey, 17), contextKey, 127);
                record.effectKey = PickRuntimeEffectKey(library.shieldEffectKeys, PickRuntimeEffectKey(library.effectKeys, "fx_placeholder_guard", contextKey, 23), contextKey, 129);
                break;

            case "armor":
                record.iconKey = PickCuratedItemIconKey(record, "armor", contextKey, "item_head_helmet");
                record.prefabKey = PickAssetForArmorSlot(record.equipSlot, contextKey);
                record.effectKey = PickRuntimeEffectKey(library.effectKeys, "fx_placeholder_guard", contextKey, 23);
                break;

            case "trinket":
                record.iconKey = PickCuratedItemIconKey(record, "trinket", contextKey, "item_trinket_ring");
                record.prefabKey = PickAssetForAccessorySlot(record.equipSlot, contextKey);
                record.effectKey = PickRuntimeEffectKey(library.magicAudioKeys, PickRuntimeEffectKey(library.effectKeys, "fx_placeholder_arcane", contextKey, 23), contextKey, 131);
                break;

            default:
                record.iconKey = PickCuratedItemIconKey(record, "consumable", contextKey, "item_consumable_potion");
                record.prefabKey = PickModelPrefab(library.trinketPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_consumable", contextKey, 17), contextKey, 133);
                record.effectKey = PickRuntimeEffectKey(library.magicAudioKeys, PickRuntimeEffectKey(library.effectKeys, "fx_placeholder_restore", contextKey, 23), contextKey, 137);
                break;
        }
    }

    private string PickCuratedItemIconKey(InventoryItemRecord record, string semanticKind, string contextKey, string fallback)
    {
        string descriptor =
            (semanticKind ?? string.Empty) + " " +
            (record != null ? record.itemType : string.Empty) + " " +
            (record != null ? record.equipSlot : string.Empty) + " " +
            (record != null ? record.displayName : string.Empty) + " " +
            (record != null ? record.description : string.Empty);

        // note: Runtime registry entries include curated and discovered imported art while keeping LLM output on stable semantic keys.
        YQRuntime2DArtRegistry registry =
            YQRuntime2DArtRegistry.Load();

        if (registry != null &&
            registry.TryPickKey(
                YQCurated2DArtCatalog.KindItemIcon,
                descriptor,
                contextKey,
                fallback,
                out string registryKey) &&
            !string.IsNullOrWhiteSpace(
                registryKey))
        {
            return registryKey;
        }

        // note: Curated semantic keys keep generated items useful when the expanded runtime registry has not been rebuilt yet.
        string curatedKey =
            YQCurated2DArtCatalog.PickKey(
                YQCurated2DArtCatalog.KindItemIcon,
                descriptor,
                contextKey,
                fallback);

        if (!string.IsNullOrWhiteSpace(curatedKey))
            return curatedKey;

        return Pick(library != null ? library.iconKeys : null, fallback, contextKey, 13);
    }

    private void ApplyElementalEffectKey(InventoryItemRecord record)
    {
        if (record == null || library == null)
            return;

        string descriptor = ((record.displayName ?? string.Empty) + " " +
                             (record.description ?? string.Empty) + " " +
                             (record.familyKey ?? string.Empty)).ToLowerInvariant();

        if (descriptor.Contains("ember") || descriptor.Contains("fire") || descriptor.Contains("flame"))
        {
            record.effectKey = PickRuntimeEffectKey(library.aoeEffectKeys, record.effectKey, record.displayName, 211);
            if (record.itemType == "weapon")
                record.description = AppendEffectHint(record.description, "It carries a visible ember edge when equipped.");
        }
        else if (descriptor.Contains("arc") || descriptor.Contains("storm"))
        {
            record.effectKey = PickRuntimeEffectKey(library.projectileEffectKeys, record.effectKey, record.displayName, 213);
        }
        else if (descriptor.Contains("blood") || descriptor.Contains("bleed") || descriptor.Contains("gore"))
        {
            // note: Blood-themed generated equipment can now bind to extracted Hivemind blood VFX prefabs.
            record.effectKey = PickRuntimeEffectKey(library.effectKeys, record.effectKey, record.displayName, 215);
        }
        else if (record.IsConsumable)
        {
            record.effectKey = PickRuntimeEffectKey(library.shieldEffectKeys, record.effectKey, record.displayName, 217);
        }
    }

    private static string AppendEffectHint(string description, string hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return description;
        if (!string.IsNullOrWhiteSpace(description) && description.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
            return description;
        return string.IsNullOrWhiteSpace(description) ? hint : description.TrimEnd() + " " + hint;
    }

    private string PickAssetForArmorSlot(string slot, string contextKey)
    {
        string normalized = string.IsNullOrWhiteSpace(slot) ? string.Empty : slot.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "head":
                return PickModelPrefab(library.headPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_head", contextKey, 17), contextKey, 141);
            case "chest":
                return PickModelPrefab(library.chestPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_chest", contextKey, 17), contextKey, 143);
            case "gloves":
                return PickModelPrefab(library.glovesPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_gloves", contextKey, 17), contextKey, 145);
            case "legs":
                return PickModelPrefab(library.legsPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_legs", contextKey, 17), contextKey, 147);
            case "boots":
                return PickModelPrefab(library.bootsPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_boots", contextKey, 17), contextKey, 149);
            case "belt":
                return PickModelPrefab(library.beltPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_belt", contextKey, 17), contextKey, 151);
            case "cloak":
                return PickModelPrefab(library.trinketPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_cloak", contextKey, 17), contextKey, 153);
            default:
                return PickModelPrefab(library.prefabKeys, "prefab_placeholder_armor", contextKey, 17);
        }
    }

    private string PickAssetForAccessorySlot(string slot, string contextKey)
    {
        string normalized = string.IsNullOrWhiteSpace(slot) ? string.Empty : slot.Trim().ToLowerInvariant();
        if (normalized.StartsWith("ring", StringComparison.OrdinalIgnoreCase))
            return PickModelPrefab(library.ringPrefabKeys, PickModelPrefab(library.trinketPrefabKeys, "prefab_placeholder_ring", contextKey, 17), contextKey, 157);
        if (normalized == "necklace")
            return PickModelPrefab(library.necklacePrefabKeys, PickModelPrefab(library.trinketPrefabKeys, "prefab_placeholder_necklace", contextKey, 17), contextKey, 159);
        if (normalized.StartsWith("earring", StringComparison.OrdinalIgnoreCase))
            return PickModelPrefab(library.trinketPrefabKeys, PickModelPrefab(library.necklacePrefabKeys, "prefab_placeholder_earring", contextKey, 17), contextKey, 161);
        return PickModelPrefab(library.trinketPrefabKeys, PickModelPrefab(library.prefabKeys, "prefab_placeholder_trinket", contextKey, 17), contextKey, 163);
    }

    public bool UseSpecificConsumable(string itemId)
    {
        PlayerStateManager manager = PlayerStateManager.Instance;
        if (manager == null || manager.state == null)
            return false;

        if (!manager.state.TryConsumeItem(itemId, out InventoryItemRecord consumed) || consumed == null)
        {
            LastInventoryMessage = "No consumable available.";
            return false;
        }

        YQInvestorVitals vitals = FindFirstObjectByType<YQInvestorVitals>();
        if (vitals != null)
        {
            vitals.Heal(consumed.healAmount);
            vitals.RestoreStamina(consumed.restoreStaminaAmount);
            vitals.RestoreMana(consumed.restoreManaAmount);
            YQGeneratedRuntimeVfx.SpawnConsumableUse(vitals.transform, consumed);
        }

        manager.state.AddLedgerLine("The player used " + consumed.displayName + ".");
        manager.state.IncCounter("item:consume", 1f);
        if (manager.autosave)
            manager.Save();

        LastInventoryMessage = "Used " + consumed.displayName + ".";
        return true;
    }

    public bool UseFirstConsumable()
    {
        PlayerStateManager manager = PlayerStateManager.Instance;
        InventoryItemRecord first = manager != null && manager.state != null ? manager.state.FindFirstConsumable() : null;
        return first != null && UseSpecificConsumable(first.itemId);
    }

    public bool CycleEquipSlot(string slot)
    {
        PlayerStateManager manager = PlayerStateManager.Instance;
        if (manager == null || manager.state == null)
            return false;

        PlayerState state = manager.state;
        state.EnsureCollections();
        int currentIndex = -1;
        InventoryItemRecord current = state.GetEquippedItem(slot);
        string currentItemId = current != null ? current.itemId : string.Empty;
        for (int i = 0; i < state.inventoryItems.Count; i++)
        {
            InventoryItemRecord item = state.inventoryItems[i];
            if (item == null || !item.IsEquippable || !string.Equals(item.equipSlot, slot, StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(item.itemId, currentItemId, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        int start = currentIndex < 0 ? 0 : currentIndex + 1;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = pass == 0 ? start : 0; i < state.inventoryItems.Count; i++)
            {
                InventoryItemRecord item = state.inventoryItems[i];
                if (item == null || !item.IsEquippable || !string.Equals(item.equipSlot, slot, StringComparison.OrdinalIgnoreCase))
                    continue;

                state.TryEquipItem(item.itemId, out string message);
                state.IncCounter("item:equip", 1f);
                state.AddLedgerLine("The player equipped " + item.displayName + " from a quick slot.");
                if (manager.autosave)
                    manager.Save();
                LastInventoryMessage = message;
                return true;
            }
        }

        LastInventoryMessage = "No item available for slot " + slot + ".";
        return false;
    }

    public int GetAttackBonus(PlayerState state) => SumEquipped(state, item => item.attackBonus);
    public int GetDefenseBonus(PlayerState state) => SumEquipped(state, item => item.defenseBonus);
    public int GetHealthBonus(PlayerState state) => SumEquipped(state, item => item.healthBonus);
    public int GetStaminaBonus(PlayerState state) => SumEquipped(state, item => item.staminaBonus);
    public int GetManaBonus(PlayerState state) => SumEquipped(state, item => item.manaBonus);

    public float GetMoveSpeedBonus(PlayerState state)
    {
        if (state == null)
            return 0f;
        state.EnsureCollections();
        float total = 0f;
        HashSet<string> countedItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in state.equippedItemBySlot)
        {
            if (string.IsNullOrWhiteSpace(kvp.Value) || !countedItems.Add(kvp.Value))
                continue;

            InventoryItemRecord item = state.FindInventoryItemById(kvp.Value);
            if (item != null)
                total += item.moveSpeedBonus;
        }
        return total;
    }

    public int GetDerivedMaxHealth(PlayerState state) => Mathf.Max(1, (state?.stats?.maxHealth ?? 100) + GetHealthBonus(state));
    public int GetDerivedMaxStamina(PlayerState state) => Mathf.Max(1, (state?.stats?.maxStamina ?? 100) + GetStaminaBonus(state));
    public int GetDerivedMaxMana(PlayerState state) => Mathf.Max(1, (state?.stats?.maxMana ?? 50) + GetManaBonus(state));

    private void RefineExistingGeneratedState(PlayerState state)
    {
        if (state == null)
            return;

        state.EnsureCollections();
        bool changed = false;

        for (int i = 0; i < state.skills.Count; i++)
        {
            SkillRecord skill = state.skills[i];
            if (skill == null)
                continue;

            string previousName = skill.name;
            string previousDescription = skill.description;
            string previousContext = skill.context;
            string previousEnvironment = skill.environment;
            skill.name = YQGeneratedContentCuration.CuratePlayerFacingName(
                state,
                skill.isSpell ? "spell" : "skill",
                skill.name,
                skill.type,
                skill.isSpell);
            skill.description = YQGeneratedContentCuration.CuratePlayerFacingDescription(
                state,
                skill.isSpell ? "spell" : "skill",
                skill.name,
                skill.description,
                skill.type,
                skill.isSpell);
            if (string.IsNullOrWhiteSpace(skill.context) || skill.context.StartsWith("llm_seed", StringComparison.OrdinalIgnoreCase) || string.Equals(skill.context, "combat", StringComparison.OrdinalIgnoreCase) || string.Equals(skill.context, "control", StringComparison.OrdinalIgnoreCase))
                skill.context = "player_response:" + (string.IsNullOrWhiteSpace(skill.type) ? (skill.isSpell ? "spell" : "skill") : skill.type.Trim().ToLowerInvariant());
            if (string.IsNullOrWhiteSpace(skill.environment) || skill.environment.StartsWith("region_", StringComparison.OrdinalIgnoreCase) || string.Equals(skill.environment, state.currentRegionId, StringComparison.OrdinalIgnoreCase))
                skill.environment = "player_profile";

            changed |= !string.Equals(previousName, skill.name, StringComparison.Ordinal) ||
                       !string.Equals(previousDescription, skill.description, StringComparison.Ordinal) ||
                       !string.Equals(previousContext, skill.context, StringComparison.Ordinal) ||
                       !string.Equals(previousEnvironment, skill.environment, StringComparison.Ordinal);
        }

        for (int i = 0; i < state.pendingOffers.Count; i++)
        {
            PendingProgressionOfferRecord offer = state.pendingOffers[i];
            if (offer == null || !offer.IsPending)
                continue;

            string previousName = offer.name;
            string previousDescription = offer.description;
            string previousReason = offer.reason;
            string previousContext = offer.context;
            string previousEnvironment = offer.environment;
            string[] previousTags = offer.tags;
            bool isSpell = offer.isSpell || string.Equals(offer.offerKind, "spell", StringComparison.OrdinalIgnoreCase);
            string kind = isSpell ? "spell" : offer.offerKind;
            offer.name = YQGeneratedContentCuration.CuratePlayerFacingName(
                state,
                kind,
                offer.name,
                offer.skillType,
                isSpell);
            offer.description = YQGeneratedContentCuration.CuratePlayerFacingDescription(
                state,
                kind,
                offer.name,
                offer.description,
                offer.skillType,
                isSpell);
            offer.tags = YQGeneratedContentCuration.BuildPlayerResponseTags(offer.tags, offer.skillType, isSpell, offer.name + " " + offer.description);
            if (string.IsNullOrWhiteSpace(offer.reason) || offer.reason.IndexOf("investor prototype", StringComparison.OrdinalIgnoreCase) >= 0 || offer.reason.IndexOf("generated", StringComparison.OrdinalIgnoreCase) >= 0)
                offer.reason = "Queued because recent play shows a repeatable player stimulus, not because of the region name.";
            if ((kind == "skill" || kind == "spell") && (string.IsNullOrWhiteSpace(offer.context) || offer.context.StartsWith("llm_seed", StringComparison.OrdinalIgnoreCase)))
                offer.context = "player_response:" + (string.IsNullOrWhiteSpace(offer.skillType) ? kind : offer.skillType.Trim().ToLowerInvariant());
            if (string.IsNullOrWhiteSpace(offer.environment) || offer.environment.StartsWith("region_", StringComparison.OrdinalIgnoreCase) || string.Equals(offer.environment, state.currentRegionId, StringComparison.OrdinalIgnoreCase))
                offer.environment = "player_profile";

            changed |= !string.Equals(previousName, offer.name, StringComparison.Ordinal) ||
                       !string.Equals(previousDescription, offer.description, StringComparison.Ordinal) ||
                       !string.Equals(previousReason, offer.reason, StringComparison.Ordinal) ||
                       !string.Equals(previousContext, offer.context, StringComparison.Ordinal) ||
                       !string.Equals(previousEnvironment, offer.environment, StringComparison.Ordinal) ||
                       !SameTags(previousTags, offer.tags);
        }

        for (int i = 0; i < state.inventoryItems.Count; i++)
        {
            InventoryItemRecord item = state.inventoryItems[i];
            if (item == null)
                continue;

            string previousName = item.displayName;
            string previousDescription = item.description;
            string previousFamilyKey = item.familyKey;
            string theme = PlayerFlavor(state, (item.templateId ?? string.Empty) + " " + (item.displayName ?? string.Empty), string.IsNullOrWhiteSpace(item.equipSlot) ? item.itemType : item.equipSlot);
            item.displayName = RefineInventoryDisplayName(item.displayName, theme);
            item.description = RefineInventoryDescription(item, theme);
            if (string.IsNullOrWhiteSpace(item.familyKey) || HasOldRegionFlavor(item.familyKey))
                item.familyKey = (string.IsNullOrWhiteSpace(item.equipSlot) ? item.itemType : item.equipSlot) + ":" + theme.ToLowerInvariant();

            changed |= !string.Equals(previousName, item.displayName, StringComparison.Ordinal) ||
                       !string.Equals(previousDescription, item.description, StringComparison.Ordinal) ||
                       !string.Equals(previousFamilyKey, item.familyKey, StringComparison.Ordinal);
        }

        if (changed)
            state.Touch();
    }

    private string ResolveStarterSpellName(PlayerState state)
    {
        string identity = BuildPlayerIdentityText(state);
        if (ContainsAny(identity, "rune", "ward", "protect", "guard", "shield"))
            return "Threshold Ward";
        if (ContainsAny(identity, "forest", "tree", "wood", "jungle", "wild", "nature", "green"))
            return "Auralith's Green Pulse";
        if (ContainsAny(identity, "fire", "ember", "spark", "flame"))
            return "Forlorn Fireball";
        if (ContainsAny(identity, "storm", "lightning", "arc", "electric"))
            return "Stormstep Pulse";
        return "Threshold Pulse";
    }

    private string BuildPlayerClassName(PlayerState state)
    {
        string motif = ResolvePlayerMotif(state, state != null ? state.playerId : "player", "class");
        string archetype = Pick(library != null ? library.classArchetypes : null, "Warden", state != null ? state.playerId : "player", 5);
        return motif + " " + archetype;
    }

    private string ResolvePlayerTitleQualifier(PlayerState state)
    {
        string identity = BuildPlayerIdentityText(state);
        if (ContainsAny(identity, "protect", "guard", "mercy", "weaker"))
            return "Oath-Keeper";
        if (ContainsAny(identity, "merchant", "market", "coin", "debt", "trade"))
            return "Keenhand";
        if (ContainsAny(identity, "forest", "tree", "wood", "jungle", "wild", "nature", "green"))
            return "Green-Witnessed";
        if (ContainsAny(identity, "fight", "fist", "blade", "force", "rival"))
            return "Unbowed";
        return Pick(library != null ? library.titleQualifiers : null, "Wayfinder", state != null ? state.playerId : "player", 9);
    }

    private string PlayerFlavor(PlayerState state, string seed, string kind)
    {
        string combined = BuildPlayerIdentityText(state) + " " + (seed ?? string.Empty) + " " + (kind ?? string.Empty);
        if (ContainsAny(combined, "forest", "tree", "wood", "jungle", "wild", "nature", "green", "grass", "root", "leaf"))
            return "First Green";
        if (ContainsAny(combined, "protect", "guard", "shield", "mercy", "weaker"))
            return "Oathbound";
        if (ContainsAny(combined, "merchant", "market", "coin", "trade", "debt", "bargain"))
            return "Keenhand";
        if (ContainsAny(combined, "dash", "dodge", "road", "mobile", "speed"))
            return "Breakstep";
        if (ContainsAny(combined, "magic", "spell", "rune", "mana", "storm", "fire", "ice", "shadow", "light"))
            return "Threshold";
        if (ContainsAny(combined, "fight", "fist", "blade", "weapon", "force", "rival", "combat"))
            return "Linebreaker";
        return ResolvePlayerMotif(state, seed, kind);
    }

    private static string RefineInventoryDisplayName(string currentName, string theme)
    {
        string name = string.IsNullOrWhiteSpace(currentName) ? string.Empty : currentName.Trim();
        string cleanTheme = string.IsNullOrWhiteSpace(theme) ? "Linebreaker" : theme.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return cleanTheme + " Gear";

        string[] oldTokens = { "Whisperroot", "Frost", "Ember", "Verdant", "Tide", "Cinder", "Cinderfall", "Frostglass", "Tideglass" };
        for (int i = 0; i < oldTokens.Length; i++)
            name = ReplaceWholeWord(name, oldTokens[i], cleanTheme);

        return name;
    }

    private static string RefineInventoryDescription(InventoryItemRecord item, string theme)
    {
        if (item == null)
            return string.Empty;

        string kind = (item.itemType ?? string.Empty).Trim().ToLowerInvariant();
        string slot = (item.equipSlot ?? string.Empty).Trim().ToLowerInvariant();
        string description = item.description ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(description) && !LooksOldGeneratedInventoryDescription(description))
            return description.Trim();

        if (kind == "weapon" || slot == "weapon")
            return "A main-hand weapon selected for your current fighting rhythm and level.";
        if (kind == "offhand" || slot == "offhand")
            return "An offhand focus tuned to how you recover space and stabilize pressure.";
        if (kind == "armor" || IsArmorSlot(slot))
            return "A fitted armor piece chosen for the way you are taking hits, moving, and surviving.";
        if (kind == "trinket" || IsAccessorySlot(slot))
            return "An accessory tuned to the utility, control, or sustain your current build keeps asking for.";
        if (item.IsConsumable)
            return "A field consumable prepared for your next recovery window: health, stamina, and mana in one quick use.";

        return "A player-facing item tuned to the way your current build is taking shape.";
    }

    private static bool LooksOldGeneratedInventoryDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return true;

        string lower = description.ToLowerInvariant();
        return lower.Contains("generated") ||
               lower.Contains("region") ||
               lower.Contains("run seed") ||
               lower.Contains("level seed");
    }

    private static bool HasOldRegionFlavor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string lower = value.ToLowerInvariant();
        return lower.Contains("whisperroot") ||
               lower.Contains("frost") ||
               lower.Contains("ember") ||
               lower.Contains("verdant") ||
               lower.Contains("tide") ||
               lower.Contains("cinder");
    }

    private static string ReplaceWholeWord(string value, string oldToken, string newToken)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(oldToken) || string.IsNullOrWhiteSpace(newToken))
            return value;

        string[] parts = value.Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            string trimmed = parts[i].Trim(',', '.', ':', ';', '!', '?', '\'', '"');
            if (string.Equals(trimmed, oldToken, StringComparison.OrdinalIgnoreCase))
                parts[i] = parts[i].Replace(trimmed, newToken);
        }

        return string.Join(" ", parts);
    }

    private static string ResolvePlayerMotif(PlayerState state, string seed, string kind)
    {
        string combined = BuildPlayerIdentityText(state) + " " + (seed ?? string.Empty) + " " + (kind ?? string.Empty);
        if (ContainsAny(combined, "forest", "tree", "wood", "jungle", "wild", "nature", "green", "grass", "root", "leaf"))
            return "Greenhand";
        if (ContainsAny(combined, "protect", "guard", "shield", "mercy", "weaker"))
            return "Oathbound";
        if (ContainsAny(combined, "merchant", "market", "coin", "trade", "debt", "bargain"))
            return "Keenhand";
        if (ContainsAny(combined, "magic", "spell", "rune", "mana", "storm", "fire", "ice", "shadow", "light"))
            return "Threshold";
        if (ContainsAny(combined, "craft", "forge", "tool", "build", "honest"))
            return "Workborn";
        if (ContainsAny(combined, "dash", "dodge", "road", "mobile", "speed"))
            return "Breakstep";
        return "Linebreaker";
    }

    private static string BuildPlayerIdentityText(PlayerState state)
    {
        if (state == null)
            return string.Empty;

        state.EnsureCollections();
        List<string> parts = new List<string>(24);
        if (state.identityKeywords != null)
            parts.AddRange(state.identityKeywords);
        if (state.originQuestionnaireAnswers != null)
            parts.AddRange(state.originQuestionnaireAnswers);
        if (state.behaviorLedger != null)
        {
            int start = Mathf.Max(0, state.behaviorLedger.Count - 12);
            for (int i = start; i < state.behaviorLedger.Count; i++)
                parts.Add(state.behaviorLedger[i]);
        }

        return string.Join(" ", parts).ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(text) || terms == null)
            return false;

        string lower = text.ToLowerInvariant();
        for (int i = 0; i < terms.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(terms[i]) && lower.Contains(terms[i].ToLowerInvariant()))
                return true;
        }

        return false;
    }

    private static bool SameTags(string[] left, string[] right)
    {
        int leftCount = left != null ? left.Length : 0;
        int rightCount = right != null ? right.Length : 0;
        if (leftCount != rightCount)
            return false;

        for (int i = 0; i < leftCount; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private void AddStarter(PlayerState state, InventoryItemRecord item)
    {
        if (state == null || item == null)
            return;
        state.AddOrUpdateItem(item, true);
        if (item.IsEquippable && state.GetEquippedItem(item.equipSlot) == null)
            state.TryEquipItem(item.itemId, out _);
    }

    private bool HasInventoryItemForSlot(PlayerState state, string slot)
    {
        if (state == null || state.inventoryItems == null)
            return false;
        for (int i = 0; i < state.inventoryItems.Count; i++)
        {
            InventoryItemRecord item = state.inventoryItems[i];
            if (item != null && item.IsEquippable && string.Equals(item.equipSlot, slot, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool HasEquippedSkill(PlayerState state, string slot)
    {
        return state != null && state.equippedSkillBySlot != null
            && state.equippedSkillBySlot.TryGetValue(slot, out string skillId)
            && !string.IsNullOrWhiteSpace(skillId);
    }

    private static SkillRecord FindFirstSkill(PlayerState state, bool spell)
    {
        if (state == null || state.skills == null)
            return null;
        for (int i = 0; i < state.skills.Count; i++)
        {
            SkillRecord skill = state.skills[i];
            if (skill != null && skill.isSpell == spell)
                return skill;
        }
        return null;
    }

    private static InventoryItemRecord FindBestItemForSlot(PlayerState state, string slot)
    {
        if (state == null || state.inventoryItems == null)
            return null;

        InventoryItemRecord best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < state.inventoryItems.Count; i++)
        {
            InventoryItemRecord item = state.inventoryItems[i];
            if (item == null || !item.IsEquippable || !string.Equals(item.equipSlot, slot, StringComparison.OrdinalIgnoreCase))
                continue;
            if (item.powerScore > bestScore)
            {
                best = item;
                bestScore = item.powerScore;
            }
        }

        return best;
    }

    private int SumEquipped(PlayerState state, Func<InventoryItemRecord, int> selector)
    {
        if (state == null)
            return 0;
        state.EnsureCollections();
        int total = 0;
        HashSet<string> countedItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in state.equippedItemBySlot)
        {
            if (string.IsNullOrWhiteSpace(kvp.Value) || !countedItems.Add(kvp.Value))
                continue;

            InventoryItemRecord item = state.FindInventoryItemById(kvp.Value);
            if (item != null)
                total += selector(item);
        }
        return total;
    }

    private string PickLootKind(YQInvestorEnemy enemy)
    {
        string seed = enemy != null ? enemy.semanticRegionId + ":" + enemy.displayName + ":loot:" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : "enemy:loot";
        switch (Mathf.Abs(StableHash(seed)) % 10)
        {
            case 0: return "weapon";
            case 1: return "offhand";
            case 2: return "head";
            case 3: return "chest";
            case 4: return "gloves";
            case 5: return "legs";
            case 6: return "boots";
            case 7: return "ring";
            case 8: return "necklace";
            default: return "consumable";
        }
    }

    private string ResolveItemKind(string contextKey, string preferredKind)
    {
        if (!string.IsNullOrWhiteSpace(preferredKind))
        {
            string kind = preferredKind.Trim().ToLowerInvariant();
            if (kind == "armor")
                return ResolveArmorSlot(null, contextKey);
            if (kind == "trinket")
                return ResolveAccessorySlot(kind, contextKey);
            return kind;
        }

        switch (Mathf.Abs(StableHash(contextKey ?? "item")) % 12)
        {
            case 0: return "weapon";
            case 1: return "offhand";
            case 2: return "head";
            case 3: return "chest";
            case 4: return "gloves";
            case 5: return "legs";
            case 6: return "boots";
            case 7: return "belt";
            case 8: return "cloak";
            case 9: return "ring";
            case 10: return "necklace";
            default: return "consumable";
        }
    }

    private static string ResolveArmorSlot(string requestedKind, string seed)
    {
        if (!string.IsNullOrWhiteSpace(requestedKind) && requestedKind != "armor")
            return requestedKind.Trim().ToLowerInvariant();
        return ArmorSlots[Mathf.Abs(StableHash((seed ?? string.Empty) + ":armorSlot")) % ArmorSlots.Length];
    }

    private static string ResolveAccessorySlot(string requestedKind, string seed)
    {
        string kind = string.IsNullOrWhiteSpace(requestedKind) ? "trinket" : requestedKind.Trim().ToLowerInvariant();
        if (kind == "ring")
            return Mathf.Abs(StableHash((seed ?? string.Empty) + ":ring")) % 2 == 0 ? "ring_left" : "ring_right";
        if (kind == "earring")
            return Mathf.Abs(StableHash((seed ?? string.Empty) + ":earring")) % 2 == 0 ? "earring_left" : "earring_right";
        if (kind == "necklace")
            return "necklace";
        if (kind == "trinket")
            return AccessorySlots[Mathf.Abs(StableHash((seed ?? string.Empty) + ":accessory")) % AccessorySlots.Length];
        if (kind == "ring_left" || kind == "ring_right" || kind == "earring_left" || kind == "earring_right")
            return kind;
        return "trinket";
    }

    private static int ArmorSlotWeight(string slot)
    {
        switch (slot)
        {
            case "chest": return 2;
            case "legs": return 2;
            case "head": return 1;
            default: return 0;
        }
    }

    private static string BuildArmorName(string slot)
    {
        switch (slot)
        {
            case "head": return "Helm";
            case "chest": return "Cuirass";
            case "gloves": return "Gloves";
            case "legs": return "Greaves";
            case "boots": return "Boots";
            case "belt": return "Belt";
            case "cloak": return "Cloak";
            default: return "Armor";
        }
    }

    private static string BuildAccessoryName(string slot)
    {
        switch (slot)
        {
            case "ring_left":
            case "ring_right": return "Ring";
            case "earring_left":
            case "earring_right": return "Earring";
            case "necklace": return "Necklace";
            default: return "Focus";
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
                hash = hash * 31 + text[i];
            return hash;
        }
    }

    private static string Pick(string[] options, string fallback, string seed, int salt)
    {
        if (options == null || options.Length == 0)
            return fallback;
        int index = Mathf.Abs(StableHash((seed ?? string.Empty) + ":" + salt)) % options.Length;
        string result = options[index];
        return string.IsNullOrWhiteSpace(result) ? fallback : result.Trim();
    }

    private static string PickModelPrefab(string[] options, string fallback, string seed, int salt)
    {
        return PickUsableAsset(options, fallback, seed, salt, value => value.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
    }

    private static string PickRuntimeEffectKey(string[] options, string fallback, string seed, int salt)
    {
        return PickUsableAsset(options, fallback, seed, salt, value =>
            value.StartsWith("fx_", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
    }

    private static string PickUsableAsset(string[] options, string fallback, string seed, int salt, Predicate<string> isUsable)
    {
        if (options == null || options.Length == 0)
            return fallback;

        int start = Mathf.Abs(StableHash((seed ?? string.Empty) + ":" + salt)) % options.Length;
        for (int i = 0; i < options.Length; i++)
        {
            string value = options[(start + i) % options.Length];
            if (string.IsNullOrWhiteSpace(value))
                continue;

            value = value.Trim();
            if (isUsable == null || isUsable(value))
                return value;
        }

        return fallback;
    }

    private static bool NeedsPrefabKeyRepair(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string normalized = value.Trim();
        return normalized.StartsWith("prefab_placeholder", StringComparison.OrdinalIgnoreCase) ||
               !normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NeedsEffectKeyRepair(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string normalized = value.Trim();
        return normalized.StartsWith("fx_placeholder", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArmorSlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return false;
        for (int i = 0; i < ArmorSlots.Length; i++)
        {
            if (string.Equals(slot, ArmorSlots[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsAccessorySlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return false;
        for (int i = 0; i < AccessorySlots.Length; i++)
        {
            if (string.Equals(slot, AccessorySlots[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string BuildTemplateId(string contextKey, string kind, string rarity, string material)
    {
        return (kind + "_" + rarity + "_" + material + "_" + (contextKey ?? "default")).Replace(' ', '_').ToLowerInvariant();
    }

    private static string ToTitleCase(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        string[] pieces = raw.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < pieces.Length; i++)
        {
            string p = pieces[i].ToLowerInvariant();
            pieces[i] = char.ToUpperInvariant(p[0]) + p.Substring(1);
        }
        return string.Join(" ", pieces);
    }
}
