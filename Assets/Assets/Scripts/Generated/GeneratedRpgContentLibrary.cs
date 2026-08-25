// Assets/Assets/Scripts/Generated/GeneratedRpgContentLibrary.cs
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GeneratedRpgContentLibrary", menuName = "YourQuest/Generated RPG Content Library")]
public sealed class GeneratedRpgContentLibrary : ScriptableObject
{
    [Header("Inventory Generation")]
    public string[] itemMaterials = { "Iron", "Steel", "Oak", "Ash", "Bone", "Obsidian", "Rune", "Traveler", "Ward", "Brass" };
    public string[] weaponBases = { "Blade", "Sword", "Axe", "Mace", "Dagger", "Spear", "Staff", "Bow", "Crossbow", "Scythe", "Throwing Axe" };
    public string[] armorBases = { "Cuirass", "Helm", "Gloves", "Boots", "Shield", "Belt", "Cloak" };
    public string[] trinketBases = { "Charm", "Ring", "Seal", "Focus", "Amulet", "Gem", "Bracer" };
    public string[] consumableBases = { "Tonic", "Phial", "Ration", "Draught", "Poultice", "Vial" };
    public string[] itemRarities = { "Common", "Uncommon", "Rare", "Epic" };
    // note: These keys resolve through the curated 2D art registry instead of raw Unity asset paths.
    public string[] iconKeys =
    {
        "item_weapon_blade",
        "item_weapon_axe",
        "item_weapon_dagger",
        "item_weapon_spear",
        "item_offhand_shield",
        "item_head_helmet",
        "item_trinket_ring",
        "item_consumable_potion",
        "item_consumable_food",
        "item_tool_pick",
        "item_tool_hammer",
        "item_currency_coin"
    };
    public string[] prefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Swords/Sword004/Prefab/Sword004.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Armor Objects/Helmets/Helmet003/Prefab/Helmet003.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Rings/Ring_1 1 New.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestSimpleSmall.prefab"
    };
    public string[] effectKeys =
    {
        "fx_placeholder_slash",
        "fx_placeholder_guard",
        "fx_placeholder_arcane",
        "fx_placeholder_restore",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Fire Light.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Power Heal.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Weakness.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particles/Particle Fireball 2 Small.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particles/Particle Electric Explosion.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particle Components/PComponent Air Explosion.prefab",
        // note: Effect hooks point to extracted prefabs only; installer packages are deleted after import.
        "Assets/HIVEMIND/RealisticBloodVFX/HDRP(Default)/RealisticBlood/Particle Systems/PS_Blood.prefab",
        "Assets/HIVEMIND/RealisticBloodVFX/HDRP(Default)/RealisticBlood/Particle Systems/PS_SplatterDirectional_01.prefab",
        "Assets/HIVEMIND/RealisticBloodVFX/HDRP(Default)/RealisticBlood/Particle Systems/PS__SplatterOmni_01.prefab"
    };

    [Header("Generated Identity Tokens")]
    public string[] titleQualifiers = { "Unbroken", "Wayfinder", "Oath-Keeper", "Keenhand", "Green-Witnessed", "Stormbound" };
    public string[] classArchetypes = { "Vanguard", "Scholar", "Binder", "Warden", "Pilgrim", "Harrier" };
    public string[] questVerbs = { "Prove", "Recover", "Survive", "Choose", "Stabilize", "Challenge" };
    public string[] questTargets = { "your first risk", "your broken rhythm", "your held ground", "your chosen road", "your oath", "your next threshold" };
    public string[] loreNouns = { "threshold", "stimulus", "oath", "lineage", "precursor", "Auralith" };
    public string[] spellElements = { "threshold", "ember", "aura", "storm", "stone", "echo" };

    [Header("Placeholder Asset Hooks")]
    public string defaultWeaponSlot = "weapon";
    public string defaultArmorSlot = "chest";
    public string defaultTrinketSlot = "trinket";
    public string defaultOffhandSlot = "offhand";

    [Header("Imported Item Prefabs")]
    public string[] weaponPrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Swords/Sword004/Prefab/Sword004.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Swords/Sword002/Prefab/Sword002.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Daggers/Dagger002/Prefab/Dagger002.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Axes/Axe004/Prefab/Axe004.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Spears/Spear003/Prefab/Spear003.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Staffs/Staff003/Prefab/Staff003.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Bows, Arrows & Crossbows/Bow001 & Bow002/Prefab/Bow001_002.prefab",
        // note: DarkFantasyWeapons expands generated equipment beyond the older Magic Pig sample set.
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Sword_1.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Sword_2.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Axe.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Mace.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Dagger.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Spear.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Staff.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Bow.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Crossbow.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Scythe.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_ThrowingAxe.prefab"
    };

    public string[] offhandPrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Armor Objects/Shields/Shield004/Prefab/Shield004.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Daggers/Dagger002/Prefab/Dagger002.prefab",
        // note: Shield variants are offhand-safe imported equipment.
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Shield_1.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Shield_2.prefab",
        "Assets/HIVEMIND/DarkFantasyWeapons/Weapons/HDRP/Prefabs/SM_Shield_3.prefab"
    };

    public string[] headPrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Armor Objects/Helmets/Helmet003/Prefab/Helmet003.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Barbute.prefab"
    };

    public string[] chestPrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Spaulder_Chest.prefab"
    };

    public string[] glovesPrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Glove_L.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Gauntlet_L.prefab"
    };

    public string[] legsPrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Trousers.prefab"
    };

    public string[] bootsPrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Boot_Left.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Sabatons_Left.prefab"
    };

    public string[] beltPrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Gladiator_Belt.prefab"
    };

    public string[] ringPrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Rings/Ring_1 1 New.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Rings/Ring_2 1 New.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Basic Rings/BasicRing001 New.prefab"
    };

    public string[] necklacePrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Amulets/Amulet_Chain A.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Amulets/Amulet_2 A.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Amulets/Amulet_3 A.prefab"
    };

    public string[] trinketPrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Gems/Gem_1 1 New.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Bracers/Bracer_1_L A New.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Bracers/Bracer_1_R A New.prefab"
    };

    [Header("Imported World Prefabs")]
    public string[] environmentPrefabKeys =
    {
        "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Floor.prefab",
        "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Wall_Standard.prefab",
        "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Wall_WindowsStandard.prefab",
        "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Bookshelf_BIG.prefab",
        "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Fireplace.prefab",
        "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_Building01.prefab",
        "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_MiniPavilionPlatform.prefab",
        "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_ChineseDragon_1.prefab"
    };

    public string[] chestInteractablePrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestSimpleSmall.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestOrnateMedium.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestMidrangeMedium.prefab"
    };

    public string[] mimicPrefabKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Mimics/MimicSimpleMedium.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Mimics/MimicOrnateMedium.prefab"
    };

    [Header("Imported Audio And VFX")]
    public string[] meleeAudioKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Battle/Sword/Sword_On_Wood/Impact/Sword_On_Wood_Impact_1.wav",
        "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Battle/Sword/Sword_On_Wood/Sword/Sword_On_Wood_Sword_1.wav"
    };

    public string[] magicAudioKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Electric/Hit/Electric_Hit_1_S.wav",
        "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Electric/Explosion/Electric_Explosion_1_S.wav",
        "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Electric/Warmup Short/Electric_Warmup_Short_1_S.wav",
        "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Water/Hit/Water_Hit_1_S.wav"
    };

    public string[] aoeEffectKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Fire Light.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Power Heal.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Weakness.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particles/Particle Electric Explosion.prefab",
        "Assets/HIVEMIND/RealisticBloodVFX/HDRP(Default)/RealisticBlood/Particle Systems/PS__SplatterOmni_02.prefab"
    };

    public string[] projectileEffectKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particles/Particle Fireball 2 Small.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particles/Particle Electric Explosion.prefab",
        "Assets/HIVEMIND/RealisticBloodVFX/HDRP(Default)/Commons/Props/Weapon/VFX_Knife.prefab",
        "Assets/HIVEMIND/RealisticBloodVFX/HDRP(Default)/Commons/Props/Weapon/VFX_Axe.prefab"
    };

    public string[] beamEffectKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Weakness.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particle Components/PComponent Air Explosion.prefab"
    };

    public string[] shieldEffectKeys =
    {
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Power Heal.prefab",
        "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particle Components/PComponent Air Explosion.prefab"
    };

    [Header("Balancing")]
    public int starterCurrency = 25;
    public int starterConsumableStacks = 3;
    public int baselineWeaponAttack = 4;
    public int baselineArmorDefense = 3;
    public int baselineTrinketMana = 10;
}
