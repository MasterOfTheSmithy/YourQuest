using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class YQRuntimeCreatureAssetIndex
{
    public const string HumanMale =
        "human_male";

    public const string HumanFemale =
        "human_female";

    public const string HumanGeneric =
        "human";

    public const string HumanoidHostile =
        "humanoid_hostile";

    public const string RockMonster =
        "rock_monster";

    public const string WormMonster =
        "worm_monster";

    public const string Demon =
        "demon";

    public const string Dragon =
        "dragon";

    public const string PlantMonster =
        "plant_monster";

    public const string MushroomMonster =
        "mushroom_monster";

    public const string Mimic =
        "mimic";

    public const string Undead =
        "undead";

    public const string Spider =
        "spider";

    public const string Beast =
        "beast";

    public const string GenericMonster =
        "monster";

    public const string CreaturePackAnchorPath =
        "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Mimics/MimicSimpleSmall.prefab";

    private const string PreferredMimicPath =
        CreaturePackAnchorPath;

    private static readonly string[] MonsterFamilyOrder =
    {
        RockMonster,
        WormMonster,
        Demon,
        Dragon,
        PlantMonster,
        MushroomMonster,
        Undead,
        Spider,
        Beast,
        GenericMonster
    };

    // ============================================================
    // HUMAN RESOLUTION
    // ============================================================

    public static bool TryResolveHuman(
        YQRuntimeWorldAssetRegistry registry,
        bool female,
        string seed,
        out YQRuntimeWorldAssetEntry result,
        out string resolvedCategory)
    {
        result =
            null;

        resolvedCategory =
            female
                ? HumanFemale
                : HumanMale;

        IReadOnlyList<YQRuntimeWorldAssetEntry> entries =
            GetCreatureEntries(
                registry);

        if (entries == null)
        {
            return false;
        }

        string requested =
            female
                ? HumanFemale
                : HumanMale;

        List<YQRuntimeWorldAssetEntry> candidates =
            CollectCategory(
                registry,
                requested);

        /*
         * Prefer a generic human before crossing sex-specific pools.
         *
         * This keeps NPCs human even if an imported pack does not
         * expose male/female terminology cleanly.
         */
        if (candidates.Count == 0)
        {
            candidates =
                CollectCategory(
                    registry,
                    HumanGeneric);

            resolvedCategory =
                HumanGeneric;
        }

        if (candidates.Count == 0)
        {
            string opposite =
                female
                    ? HumanMale
                    : HumanFemale;

            candidates =
                CollectCategory(
                    registry,
                    opposite);

            resolvedCategory =
                opposite;
        }

        result =
            PickStable(
                candidates,
                seed +
                "|human");

        return
            result != null &&
            result.prefab != null;
    }

    // ============================================================
    // MONSTER RESOLUTION
    // ============================================================

    public static bool TryResolveMonster(
        YQRuntimeWorldAssetRegistry registry,
        string generatedFamily,
        string familySeed,
        string variantSeed,
        out YQRuntimeWorldAssetEntry result,
        out string resolvedCategory)
    {
        result =
            null;

        resolvedCategory =
            string.Empty;

        IReadOnlyList<YQRuntimeWorldAssetEntry> entries =
            GetCreatureEntries(
                registry);

        if (entries == null)
        {
            return false;
        }

        string normalizedFamily =
            NormalizeSemanticText(
                generatedFamily);

        string requestedCategory =
            ResolveRequestedMonsterCategory(
                normalizedFamily);

        bool humanoidSemanticRequest =
            requestedCategory ==
                HumanoidHostile;

        /*
         * First:
         * exact known monster family.
         */
        if (!string.IsNullOrWhiteSpace(
                requestedCategory) &&
            requestedCategory !=
                GenericMonster &&
            requestedCategory !=
                HumanoidHostile)
        {
            List<YQRuntimeWorldAssetEntry> exact =
                CollectCategory(
                    registry,
                    requestedCategory);

            if (exact.Count > 0)
            {
                result =
                    PickStable(
                        exact,
                        variantSeed +
                        "|" +
                        requestedCategory);

                resolvedCategory =
                    requestedCategory;

                return
                    result != null &&
                    result.prefab != null;
            }
        }

        /*
         * Second:
         * arbitrary generated-family terminology.
         *
         * This allows things such as goblin, slime, troll, orc, etc.
         * to match imported prefab names without requiring every family
         * to be explicitly hard-coded.
         */
        List<string> familyTerms =
            ExtractUsefulTerms(
                generatedFamily);

        List<YQRuntimeWorldAssetEntry> semanticMatches =
            new List<
                YQRuntimeWorldAssetEntry>();

        int bestScore =
            0;

        for (int i = 0;
             i < entries.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                entries[i];

            if (entry == null ||
     entry.prefab == null ||
     !IsCompleteMonsterPrefab(
         entry) ||
     !IsGenericProceduralSpawnSafe(
         entry))
            {
                continue;
            }
            string entryCategory =
                ClassifyEntry(
                    entry);

            if (!IsMonsterCategory(
                    entryCategory) &&
                !(humanoidSemanticRequest &&
                  IsHumanoidSemanticCandidateCategory(
                      entryCategory)))
            {
                continue;
            }

            string semantic =
                BuildEntrySemantic(
                    entry);

            int score =
                CountTermMatches(
                    semantic,
                    familyTerms);

            if (score <= 0)
                continue;

            if (score >
                bestScore)
            {
                bestScore =
                    score;

                semanticMatches.Clear();

                semanticMatches.Add(
                    entry);
            }
            else if (score ==
                     bestScore)
            {
                semanticMatches.Add(
                    entry);
            }
        }

        if (semanticMatches.Count > 0)
        {
            result =
                PickStable(
                    semanticMatches,
                    variantSeed +
                    "|semantic_monster");

            resolvedCategory =
                humanoidSemanticRequest
                    ? HumanoidHostile
                    : result != null
                        ? ClassifyEntry(
                            result)
                        : GenericMonster;

            return
                result != null &&
                result.prefab != null;
        }

        if (humanoidSemanticRequest)
        {
            bool female =
                Deterministic01(
                    familySeed +
                    "|hostile_gender") <
                0.38f;

            if (TryResolveHuman(
                    registry,
                    female,
                    variantSeed,
                    out result,
                    out _))
            {
                // note: A curated human visual is the deterministic safety fallback only after no authored goblin/orc/scavenger semantic match exists.
                resolvedCategory =
                    HumanoidHostile;

                return true;
            }

            // note: Humanoid-generated families may use the emergency gameplay fallback, but must never spill into an unrelated dragon, demon, or other monster family.
            return false;
        }

        // note: An unknown LLM family with no semantic asset match is intentionally rejected; hashing across unrelated physical species produced goblins as full-size dragons.
        return false;
    }

    private static bool IsHumanoidSemanticCandidateCategory(
        string category)
    {
        // note: An unclassified or generic exact-name prefab may be a real goblin/orc asset; explicitly classified non-humanoid monster families are never compatible.
        return
            string.IsNullOrWhiteSpace(
                category) ||
            category ==
                GenericMonster ||
            category ==
                HumanGeneric ||
            category ==
                HumanMale ||
            category ==
                HumanFemale ||
            category ==
                HumanoidHostile;
    }

    // ============================================================
    // MIMIC
    // ============================================================

    public static bool TryResolveMimic(
        YQRuntimeWorldAssetRegistry registry,
        string seed,
        out YQRuntimeWorldAssetEntry result)
    {
        result =
            null;

        if (registry == null)
            return false;

        /*
         * Known-good Magic Pig mimic gets absolute priority.
         */
        GameObject preferred =
            registry.ResolvePrefab(
                PreferredMimicPath);

        if (preferred != null)
        {
            result =
                FindExistingEntryByPath(
                    registry,
                    PreferredMimicPath);

            if (result == null)
            {
                result =
                    new YQRuntimeWorldAssetEntry
                    {
                        assetPath =
                            PreferredMimicPath,

                        prefab =
                            preferred
                    };
            }

            return true;
        }

        List<YQRuntimeWorldAssetEntry> candidates =
            CollectCategory(
                registry,
                Mimic);

        /*
         * Prefer a Simple Small mimic when one is available.
         */
        for (int i = 0;
             i < candidates.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                candidates[i];

            if (entry == null)
                continue;

            string semantic =
                BuildEntryLocalIdentitySemantic(
                    entry);

            if (ContainsToken(
                    semantic,
                    "mimic") &&
                ContainsToken(
                    semantic,
                    "simple") &&
                ContainsToken(
                    semantic,
                    "small"))
            {
                result =
                    entry;

                return true;
            }
        }

        result =
            PickStable(
                candidates,
                seed +
                "|mimic");

        return
            result != null &&
            result.prefab != null;
    }

    private static YQRuntimeWorldAssetEntry
        FindExistingEntryByPath(
            YQRuntimeWorldAssetRegistry registry,
            string path)
    {
        IReadOnlyList<YQRuntimeWorldAssetEntry> entries =
            GetCreatureEntries(
                registry);

        if (entries == null ||
            string.IsNullOrWhiteSpace(
                path))
        {
            return null;
        }

        string wanted =
            YQRuntimeWorldAssetRegistry
                .NormalizePath(
                    path);

        for (int i = 0;
             i < entries.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                entries[i];

            if (entry == null)
                continue;

            string existing =
                YQRuntimeWorldAssetRegistry
                    .NormalizePath(
                        entry.assetPath);

            if (string.Equals(
                    wanted,
                    existing,
                    StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    // ============================================================
    // CATEGORY COLLECTION
    // ============================================================

    private static List<YQRuntimeWorldAssetEntry>
     CollectCategory(
         YQRuntimeWorldAssetRegistry registry,
         string category)
    {
        List<YQRuntimeWorldAssetEntry> result =
            new List<YQRuntimeWorldAssetEntry>();

        IReadOnlyList<YQRuntimeWorldAssetEntry> entries =
            GetCreatureEntries(
                registry);

        if (entries == null)
        {
            return result;
        }

        for (int i = 0;
             i < entries.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                entries[i];

            if (entry == null ||
                entry.prefab == null)
            {
                continue;
            }

            string classified =
                ClassifyEntry(
                    entry);

            if (!string.Equals(
                    classified,
                    category,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            /*
             * Settlement residents must be COMPLETE humans.
             */
            if (IsHumanCategory(
                    category) &&
                !IsCompleteHumanPrefab(
                    entry))
            {
                continue;
            }

            /*
             * Mimics have their own dedicated implementation.
             */
            if (category ==
                Mimic)
            {
                result.Add(
                    entry);

                continue;
            }

            /*
             * Ordinary hostile candidates need actual visible animated
             * creature geometry.
             */
            if (IsMonsterCategory(
                    category))
            {
                if (!IsCompleteMonsterPrefab(
                        entry))
                {
                    continue;
                }

                if (!IsGenericProceduralSpawnSafe(
                        entry))
                {
                    continue;
                }
            }

            result.Add(
                entry);
        }

        return result;
    }

    private static IReadOnlyList<YQRuntimeWorldAssetEntry>
        GetCreatureEntries(
            YQRuntimeWorldAssetRegistry registry)
    {
        if (registry == null)
            return null;

        // note: All approved humans and monsters live in the dedicated Characters pack shard, loaded only when population materializes.
        return registry.GetEntriesForAssetPath(
            PreferredMimicPath);
    }

    private static List<string> GetAvailableMonsterFamilies(
    YQRuntimeWorldAssetRegistry registry)
    {
        List<string> result =
            new List<string>();

        if (GetCreatureEntries(
                registry) == null)
        {
            return result;
        }

        for (int i = 0;
             i < MonsterFamilyOrder.Length;
             i++)
        {
            string category =
                MonsterFamilyOrder[i];

            List<YQRuntimeWorldAssetEntry> entries =
                CollectCategory(
                    registry,
                    category);

            if (entries.Count > 0)
            {
                result.Add(
                    category);
            }
        }

        return result;
    }
    // ============================================================
    // CLASSIFICATION
    // ============================================================

    public static string ClassifyEntry(
        YQRuntimeWorldAssetEntry entry)
    {
        if (entry == null ||
            entry.prefab == null)
        {
            return string.Empty;
        }

        string semantic =
            BuildEntrySemantic(
                entry);

        string localSemantic =
            BuildEntryLocalIdentitySemantic(
                entry);

        /*
         * Mimic classification MUST use the local prefab identity.
         *
         * Full package paths contain:
         *
         * Characters/Mimics & Chests/
         *
         * which must never turn normal chests into mimics.
         */
        if (HasAny(
                localSemantic,
                "mimic",
                "mimics"))
        {
            return Mimic;
        }

        if (HasAny(
                semantic,
                "mushroom",
                "mushrooms",
                "shroom",
                "shrooms",
                "fungus",
                "fungal"))
        {
            return MushroomMonster;
        }

        if (HasAny(
                semantic,
                "dragon",
                "dragons",
                "drake",
                "drakes",
                "wyvern",
                "wyverns"))
        {
            return Dragon;
        }

        if (HasAny(
                semantic,
                "demon",
                "demons",
                "fiend",
                "fiends",
                "devil",
                "devils",
                "infernal"))
        {
            return Demon;
        }

        if (HasAny(
                semantic,
                "worm",
                "worms",
                "wyrm",
                "wyrms",
                "larva",
                "larvae",
                "grub",
                "grubs"))
        {
            return WormMonster;
        }

        if (HasAny(
                semantic,
                "golem",
                "golems",
                "rock monster",
                "stone monster",
                "rock creature",
                "stone creature") ||
            (HasAny(
                 semantic,
                 "rock",
                 "stone",
                 "earth") &&
             HasAny(
                 semantic,
                 "monster",
                 "creature",
                 "golem",
                 "elemental")))
        {
            return RockMonster;
        }

        if (HasAny(
                semantic,
                "plant monster",
                "plant creature",
                "vine creature",
                "treant",
                "ent") ||
            (HasAny(
                 semantic,
                 "plant",
                 "vine",
                 "thorn",
                 "flora") &&
             HasAny(
                 semantic,
                 "monster",
                 "creature",
                 "enemy")))
        {
            return PlantMonster;
        }

        if (HasAny(
                semantic,
                "skeleton",
                "skeletons",
                "undead",
                "zombie",
                "zombies",
                "ghoul",
                "ghouls"))
        {
            return Undead;
        }

        if (HasAny(
                semantic,
                "spider",
                "spiders",
                "arachnid",
                "arachnids"))
        {
            return Spider;
        }

        if (HasAny(
                semantic,
                "wolf",
                "wolves",
                "beast",
                "beasts",
                "hound",
                "hounds",
                "bear",
                "boar"))
        {
            return Beast;
        }

        bool female =
            HasAny(
                semantic,
                "female",
                "woman",
                "women");

        bool male =
            HasAny(
                semantic,
                "male",
                "man",
                "men");

        bool human =
            HasAny(
                semantic,
                "human",
                "humans",
                "civilian",
                "villager",
                "commoner",
                "people",
                "person",
                "character");

        if (human &&
            female &&
            !male)
        {
            return HumanFemale;
        }

        if (human &&
            male &&
            !female)
        {
            return HumanMale;
        }

        if (human)
        {
            return HumanGeneric;
        }

        if (HasAny(
                semantic,
                "monster",
                "monsters",
                "creature",
                "creatures"))
        {
            return GenericMonster;
        }

        return string.Empty;
    }

    private static string ResolveRequestedMonsterCategory(
        string semantic)
    {
        if (HasAny(
                semantic,
                "bandit",
                "raider",
                "brigand",
                "cultist",
                "cult",
                "outlaw",
                "mercenary",
                "pirate",
                "human",
                "humanoid",
                "soldier",
                "warrior",
                "guard",
                "scavenger",
                "marauder",
                "goblin",
                "orc",
                "kobold"))
        {
            // note: Humanoid fantasy labels bind to the curated human character pool until a compatible authored species exists; they never cross-fallback into giant monster silhouettes.
            return HumanoidHostile;
        }

        if (HasAny(
                semantic,
                "mushroom",
                "shroom",
                "fungus",
                "fungal"))
        {
            return MushroomMonster;
        }

        if (HasAny(
                semantic,
                "dragon",
                "drake",
                "wyvern"))
        {
            return Dragon;
        }

        if (HasAny(
                semantic,
                "demon",
                "fiend",
                "devil",
                "infernal"))
        {
            return Demon;
        }

        if (HasAny(
                semantic,
                "worm",
                "wyrm",
                "larva",
                "grub",
                "burrower"))
        {
            return WormMonster;
        }

        if (HasAny(
                semantic,
                "golem",
                "rock",
                "stone",
                "earth elemental"))
        {
            return RockMonster;
        }

        if (HasAny(
                semantic,
                "plant",
                "vine",
                "thorn",
                "flora",
                "treant"))
        {
            return PlantMonster;
        }

        if (HasAny(
                semantic,
                "mimic"))
        {
            return Mimic;
        }

        if (HasAny(
                semantic,
                "skeleton",
                "undead",
                "zombie",
                "ghoul"))
        {
            return Undead;
        }

        if (HasAny(
                semantic,
                "spider",
                "arachnid"))
        {
            return Spider;
        }

        if (HasAny(
                semantic,
                "wolf",
                "beast",
                "hound",
                "bear",
                "boar"))
        {
            return Beast;
        }

        return string.Empty;
    }

    private static bool IsMonsterCategory(
        string category)
    {
        return
            category ==
                RockMonster ||
            category ==
                WormMonster ||
            category ==
                Demon ||
            category ==
                Dragon ||
            category ==
                PlantMonster ||
            category ==
                MushroomMonster ||
            category ==
                Undead ||
            category ==
                Spider ||
            category ==
                Beast ||
            category ==
                GenericMonster;
    }

    // ============================================================
    // PREFAB VALIDATION
    // ============================================================
    private static bool HasEquipmentObjectMarker(
    GameObject prefab)
    {
        if (prefab == null)
            return false;

        Component[] components =
            prefab.GetComponentsInChildren<Component>(
                true);

        for (int i = 0;
             i < components.Length;
             i++)
        {
            Component component =
                components[i];

            if (component == null)
                continue;

            Type type =
                component.GetType();

            if (string.Equals(
                    type.FullName,
                    "InfinityPBR.EquipmentObject",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
    private static bool IsGenericProceduralSpawnSafe(
    YQRuntimeWorldAssetEntry entry)
    {
        if (entry == null ||
            entry.prefab == null)
        {
            return false;
        }

        string semantic =
            BuildEntrySemantic(
                entry);

        /*
         * Burrowing creatures require their own emergence controller.
         * Do not drive them with generic roaming/stronghold AI.
         */
        if (HasAny(
        semantic,
        "ground worm",
        "giant worm",
        "burrowing worm",
        "burrow worm",
        "burrower",
        "burrowing"))
        {
            return false;
        }

        return true;
    }
    private static bool IsHumanCategory(
    string category)
    {
        return
            category == HumanMale ||
            category == HumanFemale ||
            category == HumanGeneric ||
            category == HumanoidHostile;
    }

    private static bool IsCompleteHumanPrefab(
        YQRuntimeWorldAssetEntry entry)
    {
        if (entry == null ||
            entry.prefab == null)
        {
            return false;
        }

        GameObject prefab =
            entry.prefab;

        if (HasEquipmentObjectMarker(
        prefab))
        {
            return false;
        }


        /*
         * A usable generated NPC must be a COMPLETE animated character.
         *
         * Modular armor, weapons, hair, equipment, etc. commonly contain
         * SkinnedMeshRenderer components but no actual character Animator.
         */
        Animator animator =
            prefab.GetComponentInChildren<Animator>(
                true);

        if (animator == null)
            return false;

        if (!HasUsableRenderedGeometry(
                prefab))
        {
            return false;
        }

        string local =
            BuildEntryLocalIdentitySemantic(
                entry);

        /*
         * Explicit modular/item assets may never become NPC bodies.
         */
        if (HasAny(
                local,
                "sword",
                "axe",
                "bow",
                "shield",
                "weapon",
                "weapons",
                "helmet",
                "helm",
                "glove",
                "gloves",
                "boot",
                "boots",
                "shoe",
                "shoes",
                "hair",
                "beard",
                "armor",
                "armour",
                "shoulder",
                "pauldron",
                "belt",
                "cape",
                "cloak",
                "quiver",
                "arrow",
                "dagger",
                "staff",
                "item",
                "items",
                "prop",
                "props"))
        {
            return false;
        }

        return true;
    }

    private static bool IsCompleteMonsterPrefab(
        YQRuntimeWorldAssetEntry entry)
    {
        if (entry == null ||
            entry.prefab == null)
        {
            return false;
        }

        GameObject prefab =
            entry.prefab;

        if (HasEquipmentObjectMarker(
        prefab))
        {
            return false;
        }

        /*
         * Enemy candidates must contain actual visible geometry.
         *
         * Animator-only demo helpers, VFX roots and controller prefabs
         * are not physical enemies.
         */
        if (!HasUsableRenderedGeometry(
                prefab))
        {
            return false;
        }

        bool animated =
            prefab.GetComponentInChildren<Animator>(
                true) != null ||
            prefab.GetComponentInChildren<Animation>(
                true) != null;

        if (!animated)
            return false;

        string local =
            BuildEntryLocalIdentitySemantic(
                entry);

        if (HasAny(
                local,
                "weapon",
                "weapons",
                "sword",
                "axe",
                "bow",
                "shield",
                "projectile",
                "particle",
                "particles",
                "effect",
                "effects",
                "vfx",
                "demo",
                "preview",
                "icon",
                "controller"))
        {
            return false;
        }

        return true;
    }

    private static bool HasUsableRenderedGeometry(
        GameObject prefab)
    {
        if (prefab == null)
            return false;

        SkinnedMeshRenderer[] skinned =
            prefab.GetComponentsInChildren<SkinnedMeshRenderer>(
                true);

        for (int i = 0;
             i < skinned.Length;
             i++)
        {
            SkinnedMeshRenderer renderer =
                skinned[i];

            if (renderer == null ||
                renderer.sharedMesh == null)
            {
                continue;
            }

            Bounds bounds =
                renderer.sharedMesh.bounds;

            if (bounds.size.sqrMagnitude >
                0.0001f)
            {
                return true;
            }
        }

        MeshRenderer[] meshRenderers =
            prefab.GetComponentsInChildren<MeshRenderer>(
                true);

        for (int i = 0;
             i < meshRenderers.Length;
             i++)
        {
            MeshRenderer renderer =
                meshRenderers[i];

            if (renderer == null)
                continue;

            MeshFilter filter =
                renderer.GetComponent<MeshFilter>();

            if (filter != null &&
                filter.sharedMesh != null &&
                filter.sharedMesh.bounds.size.sqrMagnitude >
                0.0001f)
            {
                return true;
            }
        }

        return false;
    }
    private static bool IsCharacterLikePrefab(
        GameObject prefab)
    {
        if (prefab == null)
            return false;

        if (prefab.GetComponentInChildren<
                SkinnedMeshRenderer>(
                    true) != null)
        {
            return true;
        }

        if (prefab.GetComponentInChildren<
                Animator>(
                    true) != null)
        {
            return true;
        }

        if (prefab.GetComponentInChildren<
                Animation>(
                    true) != null)
        {
            return true;
        }

        return false;
    }

    private static YQRuntimeWorldAssetEntry PickStable(
        List<YQRuntimeWorldAssetEntry> candidates,
        string seed)
    {
        if (candidates == null ||
            candidates.Count == 0)
        {
            return null;
        }

        /*
         * Registry insertion order must not affect deterministic
         * selection.
         */
        candidates.Sort(
            (a, b) =>
                string.Compare(
                    YQRuntimeWorldAssetRegistry
                        .NormalizePath(
                            a != null
                                ? a.assetPath
                                : string.Empty),
                    YQRuntimeWorldAssetRegistry
                        .NormalizePath(
                            b != null
                                ? b.assetPath
                                : string.Empty),
                    StringComparison
                        .OrdinalIgnoreCase));

        int index =
            (int)(
                StableHash32(
                    seed) %
                (uint)candidates.Count);

        return
            candidates[
                index];
    }

    // ============================================================
    // SEMANTIC TEXT
    // ============================================================

    private static string BuildEntryLocalIdentitySemantic(
        YQRuntimeWorldAssetEntry entry)
    {
        if (entry == null)
            return string.Empty;

        string path =
            SafeText(
                entry.assetPath,
                string.Empty)
                .Replace(
                    '\\',
                    '/');

        string prefabName =
            entry.prefab != null
                ? entry.prefab.name
                : string.Empty;

        string leaf =
            string.Empty;

        string parent =
            string.Empty;

        string grandParent =
            string.Empty;

        if (!string.IsNullOrWhiteSpace(
                path))
        {
            string[] parts =
                path.Split(
                    new[]
                    {
                        '/'
                    },
                    StringSplitOptions
                        .RemoveEmptyEntries);

            if (parts.Length >= 1)
            {
                leaf =
                    parts[
                        parts.Length - 1];
            }

            if (parts.Length >= 2)
            {
                parent =
                    parts[
                        parts.Length - 2];
            }

            if (parts.Length >= 3)
            {
                grandParent =
                    parts[
                        parts.Length - 3];
            }
        }

        /*
         * Intentionally excludes the entire package path.
         *
         * This prevents:
         *
         * Magic Pig Games/
         * Characters/
         * Mimics & Chests/
         *
         * from classifying every chest as a mimic.
         */
        return
            NormalizeSemanticText(
                prefabName +
                " " +
                leaf +
                " " +
                parent +
                " " +
                grandParent);
    }

    private static string BuildEntrySemantic(
        YQRuntimeWorldAssetEntry entry)
    {
        if (entry == null)
            return string.Empty;

        return
            NormalizeSemanticText(
                SafeText(
                    entry.assetPath,
                    string.Empty) +
                " " +
                (entry.prefab != null
                    ? entry.prefab.name
                    : string.Empty));
    }

    private static string NormalizeSemanticText(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        StringBuilder sb =
            new StringBuilder(
                value.Length *
                2);

        char previous =
            '\0';

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                value[i];

            if (!char.IsLetterOrDigit(
                    c))
            {
                sb.Append(' ');

                previous =
                    c;

                continue;
            }

            if (char.IsUpper(c) &&
                i > 0 &&
                (char.IsLower(
                     previous) ||
                 char.IsDigit(
                     previous)))
            {
                sb.Append(' ');
            }

            sb.Append(
                char.ToLowerInvariant(
                    c));

            previous =
                c;
        }

        string[] parts =
            sb.ToString()
                .Split(
                    new[]
                    {
                        ' '
                    },
                    StringSplitOptions
                        .RemoveEmptyEntries);

        return
            " " +
            string.Join(
                " ",
                parts) +
            " ";
    }

    private static bool HasAny(
        string semantic,
        params string[] terms)
    {
        if (terms == null)
            return false;

        for (int i = 0;
             i < terms.Length;
             i++)
        {
            if (ContainsToken(
                    semantic,
                    terms[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsToken(
        string semantic,
        string term)
    {
        if (string.IsNullOrWhiteSpace(
                semantic) ||
            string.IsNullOrWhiteSpace(
                term))
        {
            return false;
        }

        string normalized =
            NormalizeSemanticText(
                term)
                .Trim();

        if (string.IsNullOrWhiteSpace(
                normalized))
        {
            return false;
        }

        /*
         * Exact token / phrase matching only.
         *
         * Critical:
         *
         * "female" must NOT match "male".
         *
         * CamelCase names are already normalized:
         *
         * HumanFemale
         * ->
         * human female
         */
        return
            semantic.Contains(
                " " +
                normalized +
                " ");
    }

    private static List<string> ExtractUsefulTerms(
        string value)
    {
        List<string> result =
            new List<string>();

        string normalized =
            NormalizeSemanticText(
                value);

        string[] parts =
            normalized.Split(
                new[]
                {
                    ' '
                },
                StringSplitOptions
                    .RemoveEmptyEntries);

        for (int i = 0;
             i < parts.Length;
             i++)
        {
            string part =
                parts[i];

            if (part.Length <
                3)
            {
                continue;
            }

            switch (part)
            {
                case "the":
                case "and":
                case "with":
                case "from":
                case "hostile":
                case "hostiles":
                case "enemy":
                case "enemies":
                case "stronghold":
                case "encampment":
                case "camp":
                case "clan":
                case "tribe":
                    continue;
            }

            AddUnique(
                result,
                part);
        }

        return result;
    }

    private static int CountTermMatches(
        string semantic,
        List<string> terms)
    {
        if (terms == null)
            return 0;

        int count =
            0;

        for (int i = 0;
             i < terms.Count;
             i++)
        {
            if (ContainsToken(
                    semantic,
                    terms[i]))
            {
                count++;
            }
        }

        return count;
    }

    // ============================================================
    // DETERMINISM
    // ============================================================

    private static float Deterministic01(
        string seed)
    {
        return
            (StableHash32(
                 seed) &
             0x00FFFFFFu) /
            16777215f;
    }

    private static uint StableHash32(
        string value)
    {
        const uint offsetBasis =
            2166136261u;

        const uint prime =
            16777619u;

        uint hash =
            offsetBasis;

        if (value == null)
            return hash;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                value[i];

            hash ^=
                (byte)(
                    c &
                    0xFF);

            hash *=
                prime;

            hash ^=
                (byte)(
                    (c >> 8) &
                    0xFF);

            hash *=
                prime;
        }

        return hash;
    }

    private static void AddUnique(
        List<string> values,
        string value)
    {
        if (values == null ||
            string.IsNullOrWhiteSpace(
                value))
        {
            return;
        }

        for (int i = 0;
             i < values.Count;
             i++)
        {
            if (string.Equals(
                    values[i],
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        values.Add(
            value);
    }

    private static string SafeText(
        string value,
        string fallback)
    {
        return
            string.IsNullOrWhiteSpace(
                value)
                ? fallback
                : value.Trim();
    }

    // ============================================================
    // EDITOR AUTO-INDEX
    // ============================================================

#if UNITY_EDITOR

    private static class EditorCreatureRegistrySynchronizer
    {
        [MenuItem(
            "YourQuest/Generated World/Rebuild Human + Monster Registry")]
        private static void RebuildFromMenu()
        {
            // note: Creature registry sync is intentionally manual so script compiles do not rescan all prefabs.
            Synchronize();
        }

        private static void Synchronize()
        {
            if (EditorApplication
                .isPlayingOrWillChangePlaymode)
            {
                return;
            }

            YQRuntimeWorldAssetRegistry registry =
                FindBestRegistry();

            if (registry == null)
            {
                Debug.LogWarning(
                    "[YQRuntimeCreatureAssetIndex] " +
                    "Could not locate YQRuntimeWorldAssetRegistry asset.");

                return;
            }

            List<YQRuntimeWorldAssetEntry> merged =
                new List<
                    YQRuntimeWorldAssetEntry>();

            HashSet<string> knownPaths =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            /*
             * Preserve existing environment/material registry entries.
             */
            int removedInvalidCreatureEntries =
    0;

            if (registry.Entries != null)
            {
                for (int i = 0;
                     i < registry.Entries.Count;
                     i++)
                {
                    YQRuntimeWorldAssetEntry existing =
                        registry.Entries[i];

                    if (existing == null)
                        continue;

                    string existingPath =
                        SafeText(
                            existing.assetPath,
                            string.Empty)
                            .Replace(
                                '\\',
                                '/');

                    string lowerExistingPath =
                        existingPath
                            .ToLowerInvariant();

                    /*
                     * Revalidate things living in actual character / monster trees.
                     *
                     * This removes stale modular equipment and helper prefabs that
                     * were indexed by the older permissive scanner.
                     */
                    bool creatureNamespace =
                        lowerExistingPath.Contains(
                            "/characters/") ||
                        lowerExistingPath.Contains(
                            "/character/") ||
                        lowerExistingPath.Contains(
                            "/monsters/") ||
                        lowerExistingPath.Contains(
                            "/monster/") ||
                        lowerExistingPath.Contains(
                            "/creatures/") ||
                        lowerExistingPath.Contains(
                            "/creature/");

                    /*
                     * Ordinary Magic Pig chests are legitimate WORLD assets even
                     * though they live beneath /Characters/.
                     */
                    bool ordinaryChest =
                        lowerExistingPath.Contains(
                            "/_prefabs/chests/");

                    if (creatureNamespace &&
                        !ordinaryChest &&
                        existing.prefab != null &&
                        !ShouldIndexPrefab(
                            existing.assetPath,
                            existing.prefab))

                    {
                        removedInvalidCreatureEntries++;

                        continue;
                    }


                    merged.Add(
                        existing);

                    string normalized =
                        YQRuntimeWorldAssetRegistry
                            .NormalizePath(
                                existing.assetPath);

                    if (!string.IsNullOrWhiteSpace(
                            normalized))
                    {
                        knownPaths.Add(
                            normalized);
                    }
                }
            
        }

            string[] prefabGuids =
                AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[]
                    {
                        "Assets"
                    });

            int added =
                0;

            for (int i = 0;
                 i < prefabGuids.Length;
                 i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        prefabGuids[i]);

                if (string.IsNullOrWhiteSpace(
                        path))
                {
                    continue;
                }

                string normalized =
                    YQRuntimeWorldAssetRegistry
                        .NormalizePath(
                            path);

                if (knownPaths.Contains(
                        normalized))
                {
                    continue;
                }

                GameObject prefab =
                    AssetDatabase
                        .LoadAssetAtPath<
                            GameObject>(
                                path);

                if (prefab == null ||
                    !ShouldIndexPrefab(
                        path,
                        prefab))
                {
                    continue;
                }

                merged.Add(
                    new YQRuntimeWorldAssetEntry
                    {
                        assetPath =
                            path,

                        prefab =
                            prefab
                    });

                knownPaths.Add(
                    normalized);

                added++;
            }

            /*
             * Guarantee the known actual Magic Pig mimic.
             */
            AddRequiredPrefab(
                merged,
                knownPaths,
                PreferredMimicPath,
                ref added);

            registry.SetEntries(
                merged);

            EditorUtility.SetDirty(
                registry);

            AssetDatabase.SaveAssets();

            YQRuntimeWorldAssetRegistry
                .ClearCachedInstance();
            Debug.Log(
    "[YQRuntimeCreatureAssetIndex] " +
    "Removed stale invalid creature entries: " +
    removedInvalidCreatureEntries);
            LogRegistrySummary(
                registry,
                added);
        }
        
        private static YQRuntimeWorldAssetRegistry
            FindBestRegistry()
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:YQRuntimeWorldAssetRegistry");

            YQRuntimeWorldAssetRegistry best =
                null;

            int bestScore =
                int.MinValue;

            for (int i = 0;
                 i < guids.Length;
                 i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                YQRuntimeWorldAssetRegistry candidate =
                    AssetDatabase
                        .LoadAssetAtPath<
                            YQRuntimeWorldAssetRegistry>(
                                path);

                if (candidate == null)
                    continue;

                int score =
                    candidate.Entries != null
                        ? candidate.Entries.Count
                        : 0;

                /*
                 * Prefer the canonical Resources registry by name.
                 */
                if (string.Equals(
                        candidate.name,
                        "YQRuntimeWorldAssetRegistry",
                        StringComparison.OrdinalIgnoreCase))
                {
                    score +=
                        1000000;
                }

                if (best == null ||
                    score >
                    bestScore)
                {
                    best =
                        candidate;

                    bestScore =
                        score;
                }
            }

            return best;
        }

        private static bool ShouldIndexPrefab(
            string path,
            GameObject prefab)
        {
            if (prefab == null ||
                string.IsNullOrWhiteSpace(
                    path))
            {
                return false;
            }

            if (!IsCharacterLikePrefab(
                    prefab))
            {
                return false;
            }
            if (HasEquipmentObjectMarker(
        prefab))
            {
                return false;
            }
            string lowerPath =
                path
                    .Replace(
                        '\\',
                        '/')
                    .ToLowerInvariant();

            string prefabSemantic =
                NormalizeSemanticText(
                    prefab.name);

            /*
             * ========================================================
             * CHESTS / MIMICS MUST BE CLASSIFIED FIRST
             * ========================================================
             *
             * Package path:
             *
             * Characters/Mimics & Chests/
             *
             * contains "Mimics" for BOTH chest and mimic prefabs.
             *
             * Therefore ordinary chest rejection must happen BEFORE
             * broad semantic package matching.
             */

            bool actualMimicPrefab =
                lowerPath.Contains(
                    "/_prefabs/mimics/") ||
                lowerPath.Contains(
                    "/mimics/") ||
                HasAny(
                    prefabSemantic,
                    "mimic",
                    "mimics");

            bool ordinaryChestPrefab =
                lowerPath.Contains(
                    "/_prefabs/chests/") ||
                lowerPath.Contains(
                    "/chests/");

            if (ordinaryChestPrefab &&
                !actualMimicPrefab)
            {
                return false;
            }

            if (actualMimicPrefab)
            {
                return true;
            }

            /*
             * Now broad semantic matching is safe.
             */
            string semantic =
                NormalizeSemanticText(
                    path +
                    " " +
                    prefab.name);

            /*
             * Explicit human terminology.
             */
            if (HasAny(
                    semantic,
                    "human",
                    "male",
                    "female",
                    "woman",
                    "women",
                    "man",
                    "men",
                    "civilian",
                    "villager",
                    "commoner"))
            {
                return true;
            }

            /*
             * Explicit monster terminology.
             */
            if (HasAny(
                    semantic,
                    "monster",
                    "creature",
                    "demon",
                    "dragon",
                    "drake",
                    "wyvern",
                    "worm",
                    "wyrm",
                    "golem",
                    "skeleton",
                    "undead",
                    "zombie",
                    "spider",
                    "beast",
                    "wolf",
                    "mushroom",
                    "fungus",
                    "shroom",
                    "plant",
                    "treant",
                    "orc",
                    "goblin",
                    "troll",
                    "slime"))
            {
                return true;
            }

            /*
             * Character packs often use vague prefab names such as:
             *
             * Character_01
             * Warrior_A
             * NPC_03
             *
             * while the useful clue is the directory.
             */
            bool characterDirectory =
                lowerPath.Contains(
                    "/characters/") ||
                lowerPath.Contains(
                    "/character/");

            bool monsterDirectory =
                lowerPath.Contains(
                    "/monsters/") ||
                lowerPath.Contains(
                    "/monster/") ||
                lowerPath.Contains(
                    "/creatures/") ||
                lowerPath.Contains(
                    "/creature/");

            if (monsterDirectory)
            {
                YQRuntimeWorldAssetEntry temporary =
                    new YQRuntimeWorldAssetEntry
                    {
                        assetPath =
                            path,

                        prefab =
                            prefab
                    };

                return
                    IsCompleteMonsterPrefab(
                        temporary);
            }

            if (characterDirectory)
            {
                YQRuntimeWorldAssetEntry temporary =
                    new YQRuntimeWorldAssetEntry
                    {
                        assetPath =
                            path,

                        prefab =
                            prefab
                    };

                /*
                 * Character-directory assets must resolve as either a complete
                 * human or a complete monster.
                 */
                string classified =
                    ClassifyEntry(
                        temporary);

                if (IsHumanCategory(
                        classified))
                {
                    return
                        IsCompleteHumanPrefab(
                            temporary);
                }

                if (IsMonsterCategory(
                        classified))
                {
                    return
                        IsCompleteMonsterPrefab(
                            temporary);
                }

                return false;
            }
        

            /*
             * Every code path now returns a bool.
             */
            return false;
        }
        private static bool HasEquipmentObjectMarker(
    GameObject prefab)
        {
            if (prefab == null)
                return false;

            Component[] components =
                prefab.GetComponentsInChildren<Component>(
                    true);

            for (int i = 0;
                 i < components.Length;
                 i++)
            {
                Component component =
                    components[i];

                if (component == null)
                    continue;

                Type type =
                    component.GetType();

                if (string.Equals(
                        type.FullName,
                        "InfinityPBR.EquipmentObject",
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        private static void AddRequiredPrefab(
            List<YQRuntimeWorldAssetEntry> entries,
            HashSet<string> knownPaths,
            string path,
            ref int added)
        {
            if (entries == null ||
                knownPaths == null ||
                string.IsNullOrWhiteSpace(
                    path))
            {
                return;
            }

            string normalized =
                YQRuntimeWorldAssetRegistry
                    .NormalizePath(
                        path);

            if (knownPaths.Contains(
                    normalized))
            {
                return;
            }

            GameObject prefab =
                AssetDatabase
                    .LoadAssetAtPath<
                        GameObject>(
                            path);

            if (prefab == null)
            {
                Debug.LogWarning(
                    "[YQRuntimeCreatureAssetIndex] " +
                    "Required prefab not found: " +
                    path);

                return;
            }

            entries.Add(
                new YQRuntimeWorldAssetEntry
                {
                    assetPath =
                        path,

                    prefab =
                        prefab
                });

            knownPaths.Add(
                normalized);

            added++;
        }

        private static void LogRegistrySummary(
            YQRuntimeWorldAssetRegistry registry,
            int added)
        {
            int male =
                0;

            int female =
                0;

            int human =
                0;

            int rock =
                0;

            int worm =
                0;

            int demon =
                0;

            int dragon =
                0;

            int plant =
                0;

            int mushroom =
                0;

            int mimic =
                0;

            int undead =
                0;

            int spider =
                0;

            int beast =
                0;

            int genericMonster =
                0;

            if (registry != null &&
                registry.Entries != null)
            {
                for (int i = 0;
                     i < registry.Entries.Count;
                     i++)
                {
                    string category =
                        ClassifyEntry(
                            registry.Entries[i]);

                    switch (category)
                    {
                        case HumanMale:
                            male++;
                            break;

                        case HumanFemale:
                            female++;
                            break;

                        case HumanGeneric:
                            human++;
                            break;

                        case RockMonster:
                            rock++;
                            break;

                        case WormMonster:
                            worm++;
                            break;

                        case Demon:
                            demon++;
                            break;

                        case Dragon:
                            dragon++;
                            break;

                        case PlantMonster:
                            plant++;
                            break;

                        case MushroomMonster:
                            mushroom++;
                            break;

                        case Mimic:
                            mimic++;
                            break;

                        case Undead:
                            undead++;
                            break;

                        case Spider:
                            spider++;
                            break;

                        case Beast:
                            beast++;
                            break;

                        case GenericMonster:
                            genericMonster++;
                            break;
                    }
                }
            }

            Debug.Log(
                "[YQRuntimeCreatureAssetIndex] REGISTRY SYNC\n" +
                "New creature/human prefabs added: " +
                added +
                "\nHuman male: " +
                male +
                "\nHuman female: " +
                female +
                "\nHuman generic: " +
                human +
                "\nRock monsters: " +
                rock +
                "\nWorm monsters: " +
                worm +
                "\nDemons: " +
                demon +
                "\nDragons: " +
                dragon +
                "\nPlant monsters: " +
                plant +
                "\nMushroom monsters: " +
                mushroom +
                "\nMimics: " +
                mimic +
                "\nUndead: " +
                undead +
                "\nSpiders: " +
                spider +
                "\nBeasts: " +
                beast +
                "\nGeneric monsters: " +
                genericMonster);
        }
    }

#endif
}
