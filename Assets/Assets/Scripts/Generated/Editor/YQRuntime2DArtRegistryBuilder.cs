// Assets/Assets/Scripts/Generated/Editor/YQRuntime2DArtRegistryBuilder.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class YQRuntime2DArtRegistryBuilder
{
    private const string RegistryFolder = "Assets/Assets/Resources";
    private const string RegistryPath = RegistryFolder + "/YQRuntime2DArtRegistry.asset";

    private static readonly string[] HumbleBundleRoots =
    {
        "Assets/HumbleBundleResources",
        "Assets/Assets/humblebundleresources",
        "Assets/Assets/HumbleBundleResources"
    };

    // note: Curated 2D registry rebuilding remains a recurring content-pipeline action and is grouped by the data it owns.
    [MenuItem("Tools/YourQuest/Content Pipeline/2D Art/Rebuild Curated Registry")]
    public static void RebuildRegistry()
    {
        if (!AssetDatabase.IsValidFolder(RegistryFolder))
            Directory.CreateDirectory(RegistryFolder);

        YQRuntime2DArtRegistry registry =
            AssetDatabase.LoadAssetAtPath<YQRuntime2DArtRegistry>(RegistryPath);

        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<YQRuntime2DArtRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
        }

        List<YQRuntime2DArtRegistry.Entry> curatedEntries = new List<YQRuntime2DArtRegistry.Entry>();
        HashSet<string> seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int missing = 0;

        // note: This builder only accepts hand-curated PNG texture paths from YQCurated2DArtCatalog.
        foreach (YQCurated2DArtEntry source in YQCurated2DArtCatalog.Entries)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(source.assetPath);
            if (texture == null)
            {
                missing++;
                Debug.LogWarning("[YQRuntime2DArtRegistryBuilder] Missing curated 2D art: " + source.key + " at " + source.assetPath);
                continue;
            }

            AddEntry(curatedEntries, seenKeys, seenPaths, new YQRuntime2DArtRegistry.Entry
            {
                key = source.key,
                kind = source.kind,
                assetPath = source.assetPath,
                tags = source.tags,
                weight = source.weight,
                texture = texture
            });
        }

        int discovered =
            AddDiscoveredHumbleBundleEntries(
                curatedEntries,
                seenKeys,
                seenPaths);

        registry.entries = curatedEntries.ToArray();
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[YQRuntime2DArtRegistryBuilder] Rebuilt curated 2D art registry: entries=" + curatedEntries.Count + ", discovered=" + discovered + ", missing=" + missing + ".");
    }

    private static int AddDiscoveredHumbleBundleEntries(
        List<YQRuntime2DArtRegistry.Entry> entries,
        HashSet<string> seenKeys,
        HashSet<string> seenPaths)
    {
        string[] roots =
            GetValidRoots(
                HumbleBundleRoots);

        if (roots.Length == 0)
            return 0;

        string[] guids =
            AssetDatabase.FindAssets(
                "t:Texture2D",
                roots);

        int added =
            0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[i]);

            if (!IsUsefulTexturePath(
                    path))
            {
                continue;
            }

            string kind =
                ResolveKind(
                    path);

            if (string.IsNullOrWhiteSpace(
                    kind))
            {
                continue;
            }

            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    path);

            if (texture == null)
                continue;

            // note: Discovered Humble art stays semantic; generation receives keys/kinds, never raw Unity paths.
            bool accepted =
                AddEntry(
                    entries,
                    seenKeys,
                    seenPaths,
                    new YQRuntime2DArtRegistry.Entry
                    {
                        key = BuildKey(
                            kind,
                            path),
                        kind = kind,
                        assetPath = path.Replace('\\', '/'),
                        tags = BuildTags(
                            path,
                            kind),
                        weight = ResolveWeight(
                            path,
                            kind),
                        texture = texture
                    });

            if (accepted)
                added++;
        }

        return added;
    }

    private static bool AddEntry(
        List<YQRuntime2DArtRegistry.Entry> entries,
        HashSet<string> seenKeys,
        HashSet<string> seenPaths,
        YQRuntime2DArtRegistry.Entry entry)
    {
        if (entries == null ||
            entry == null ||
            string.IsNullOrWhiteSpace(entry.key) ||
            string.IsNullOrWhiteSpace(entry.assetPath))
        {
            return false;
        }

        string path =
            entry.assetPath.Replace(
                '\\',
                '/');

        if (!seenKeys.Add(entry.key) ||
            !seenPaths.Add(path))
        {
            return false;
        }

        entry.assetPath =
            path;

        entries.Add(
            entry);

        return true;
    }

    private static string[] GetValidRoots(
        string[] roots)
    {
        List<string> valid =
            new List<string>();

        if (roots == null)
            return valid.ToArray();

        for (int i = 0; i < roots.Length; i++)
        {
            string root =
                roots[i];

            if (AssetDatabase.IsValidFolder(
                    root))
            {
                valid.Add(
                    root);
            }
        }

        return valid.ToArray();
    }

    private static bool IsUsefulTexturePath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string normalized =
            path.Replace('\\', '/');

        if (!normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
            !normalized.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
            !normalized.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) &&
            !normalized.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string search =
            NormalizeSearchText(
                normalized);

        // note: Template sheets and source art are useful to artists, but noisy inside runtime procedural selection.
        if (ContainsAny(
                search,
                "psd",
                "template",
                "carddesign",
                "sci ficardtemplate",
                "background",
                "backside"))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(
            ResolveKind(
                normalized));
    }

    private static string ResolveKind(
        string path)
    {
        string search =
            NormalizeSearchText(
                path);

        if (ContainsAny(search, "maptiles"))
            return YQCurated2DArtCatalog.KindMapTile;

        if (ContainsAny(search, "questjournal"))
            return YQCurated2DArtCatalog.KindQuestUi;

        if (ContainsAny(search, "clanshields", "fantasybanners"))
            return YQCurated2DArtCatalog.KindFactionBadge;

        if (ContainsAny(search, "rpgclassbadges", "racesbadges", "magicbadges", "fantasybadges"))
            return YQCurated2DArtCatalog.KindClassBadge;

        if (ContainsAny(search, "rpgprofessionalbadges"))
            return YQCurated2DArtCatalog.KindProfessionBadge;

        if (ContainsAny(search, "fantasycharacters", "fantasyanimeavatars", "steampunkanimeavatars", "mobsavataricons", "monstersavataricons", "creaturecards", "petscards"))
            return YQCurated2DArtCatalog.KindPortrait;

        if (ContainsAny(search, "coinsicons", "strategygameicons", "gametokens", "itemscards", "lootcards", "tcgcardspack", "fantasycardspack", "magiccardspack", "tabletoptokens"))
            return YQCurated2DArtCatalog.KindItemIcon;

        return string.Empty;
    }

    private static string BuildKey(
        string kind,
        string path)
    {
        string normalized =
            NormalizeSearchText(
                Path.ChangeExtension(
                    path.Replace('\\', '/'),
                    null));

        string[] parts =
            normalized.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

        string suffix =
            parts.Length == 0
                ? "asset"
                : string.Join("_", parts);

        return
            "hb_" +
            NormalizeToken(
                kind) +
            "_" +
            suffix;
    }

    private static string[] BuildTags(
        string path,
        string kind)
    {
        List<string> tags =
            new List<string>();

        AddTag(
            tags,
            kind);

        string normalized =
            NormalizeSearchText(
                path);

        string[] parts =
            normalized.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < parts.Length && i < 18; i++)
        {
            AddTag(
                tags,
                parts[i]);
        }

        return tags.ToArray();
    }

    private static int ResolveWeight(
        string path,
        string kind)
    {
        string search =
            NormalizeSearchText(
                path);

        if (ContainsAny(search, "png", "transparent", "icons", "badge", "avatar"))
            return 3;

        if (kind == YQCurated2DArtCatalog.KindMapTile ||
            kind == YQCurated2DArtCatalog.KindQuestUi)
        {
            return 2;
        }

        return 1;
    }

    private static void AddTag(
        List<string> tags,
        string value)
    {
        string tag =
            NormalizeToken(
                value);

        if (string.IsNullOrWhiteSpace(tag) ||
            tags.Contains(tag))
        {
            return;
        }

        tags.Add(tag);
    }

    private static string NormalizeSearchText(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Replace('\\', ' ')
            .Replace('/', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Replace('.', ' ')
            .ToLowerInvariant();
    }

    private static string NormalizeToken(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string lower =
            value.ToLowerInvariant();

        char[] chars =
            lower.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        return new string(chars).Trim('_');
    }

    private static bool ContainsAny(
        string search,
        params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(search) ||
            needles == null)
        {
            return false;
        }

        for (int i = 0; i < needles.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(needles[i]) &&
                search.Contains(needles[i]))
            {
                return true;
            }
        }

        return false;
    }
}
