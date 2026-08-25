// Assets/Assets/Scripts/Generated/YQRuntime2DArtRegistry.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "YQRuntime2DArtRegistry", menuName = "YourQuest/Runtime 2D Art Registry")]
public sealed class YQRuntime2DArtRegistry : ScriptableObject
{
    private const string ResourcePath = "YQRuntime2DArtRegistry";

    [Serializable]
    public sealed class Entry
    {
        public string key;
        public string kind;
        public string assetPath;
        public string[] tags;
        public int weight = 1;
        public Texture2D texture;
    }

    public Entry[] entries = Array.Empty<Entry>();

    private Dictionary<string, Entry> entriesByKey;
    private Dictionary<string, List<Entry>> entriesByKind;
    private static YQRuntime2DArtRegistry cached;

    public static YQRuntime2DArtRegistry Load()
    {
        // note: The registry is optional at runtime; generated records can still carry stable keys before textures are wired into UI.
        if (cached == null)
            cached = Resources.Load<YQRuntime2DArtRegistry>(ResourcePath);

        return cached;
    }

    public bool TryGetEntry(string key, out Entry entry)
    {
        BuildIndexIfNeeded();
        if (string.IsNullOrWhiteSpace(key) || entriesByKey == null)
        {
            entry = null;
            return false;
        }

        return entriesByKey.TryGetValue(key, out entry);
    }

    public bool TryGetTexture(string key, out Texture2D texture)
    {
        if (TryGetEntry(key, out Entry entry) && entry != null && entry.texture != null)
        {
            texture = entry.texture;
            return true;
        }

        texture = null;
        return false;
    }

    public bool TryPickKey(string kind, string semanticText, string seed, string fallback, out string key)
    {
        BuildIndexIfNeeded();

        key = fallback ?? string.Empty;

        string normalizedKind = NormalizeToken(kind);
        if (string.IsNullOrWhiteSpace(normalizedKind) ||
            entriesByKind == null ||
            !entriesByKind.TryGetValue(normalizedKind, out List<Entry> candidates) ||
            candidates == null ||
            candidates.Count == 0)
        {
            return false;
        }

        string semantic = (semanticText ?? string.Empty).ToLowerInvariant();
        string stableSeed = seed ?? string.Empty;
        int bestScore = int.MinValue;
        Entry best = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            Entry candidate = candidates[i];
            if (candidate == null || candidate.texture == null || string.IsNullOrWhiteSpace(candidate.key))
                continue;

            int score = ScoreEntry(candidate, semantic) * 1000;
            score += Mathf.Max(1, candidate.weight);
            score += StablePositiveHash(stableSeed + "|" + candidate.key) % 997;

            if (score <= bestScore)
                continue;

            bestScore = score;
            best = candidate;
        }

        if (best == null)
            return false;

        // note: Generation persists the semantic key; UI resolves the Texture2D through this registry later.
        key = best.key;
        return true;
    }

    public static string BuildPromptBlock()
    {
        YQRuntime2DArtRegistry registry = Load();
        if (registry == null || registry.entries == null || registry.entries.Length == 0)
            return YQCurated2DArtCatalog.BuildPromptBlock();

        registry.BuildIndexIfNeeded();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("CURATED_2D_ART_LIBRARY");
        builder.AppendLine("Unity maps visual intent to curated art keys; do not invent asset paths.");
        builder.AppendLine("Supported visual categories: item_icon, class_badge, profession_badge, faction_badge, portrait, quest_ui, map_tile.");

        // note: Prompt lists compact key samples by kind while the full serialized registry remains available to runtime selection.
        AppendKindSummary(builder, registry, YQCurated2DArtCatalog.KindItemIcon, 14);
        AppendKindSummary(builder, registry, YQCurated2DArtCatalog.KindClassBadge, 12);
        AppendKindSummary(builder, registry, YQCurated2DArtCatalog.KindProfessionBadge, 10);
        AppendKindSummary(builder, registry, YQCurated2DArtCatalog.KindFactionBadge, 10);
        AppendKindSummary(builder, registry, YQCurated2DArtCatalog.KindPortrait, 14);
        AppendKindSummary(builder, registry, YQCurated2DArtCatalog.KindQuestUi, 10);
        AppendKindSummary(builder, registry, YQCurated2DArtCatalog.KindMapTile, 10);

        return builder.ToString();
    }

    private void OnEnable()
    {
        BuildIndexIfNeeded();
    }

    private void BuildIndexIfNeeded()
    {
        if (entriesByKey != null && entriesByKind != null)
            return;

        entriesByKey = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        entriesByKind = new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);
        if (entries == null)
            return;

        // note: Duplicate keys are ignored after the first valid curated entry to keep resolution deterministic.
        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key) || entriesByKey.ContainsKey(entry.key))
                continue;

            entriesByKey.Add(entry.key, entry);

            string kind = NormalizeToken(entry.kind);
            if (string.IsNullOrWhiteSpace(kind))
                continue;

            if (!entriesByKind.TryGetValue(kind, out List<Entry> kindEntries))
            {
                kindEntries = new List<Entry>();
                entriesByKind[kind] = kindEntries;
            }

            kindEntries.Add(entry);
        }
    }

    private static void AppendKindSummary(StringBuilder builder, YQRuntime2DArtRegistry registry, string kind, int maxKeys)
    {
        string normalizedKind = NormalizeToken(kind);
        if (builder == null ||
            registry == null ||
            registry.entriesByKind == null ||
            !registry.entriesByKind.TryGetValue(normalizedKind, out List<Entry> entries) ||
            entries == null ||
            entries.Count == 0)
        {
            return;
        }

        builder.Append("- ");
        builder.Append(kind);
        builder.Append(" keys available=");
        builder.Append(entries.Count);
        builder.Append("; examples=");

        int written = 0;
        for (int i = 0; i < entries.Count && written < maxKeys; i++)
        {
            Entry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            if (written > 0)
                builder.Append(", ");

            builder.Append(entry.key);
            written++;
        }

        builder.AppendLine();
    }

    private static int ScoreEntry(Entry entry, string semantic)
    {
        int score = 0;
        if (entry == null)
            return score;

        string haystack = ((entry.key ?? string.Empty) + " " + (entry.assetPath ?? string.Empty)).ToLowerInvariant();
        if (entry.tags != null)
        {
            for (int i = 0; i < entry.tags.Length; i++)
            {
                string tag = entry.tags[i];
                if (!string.IsNullOrWhiteSpace(tag))
                    haystack += " " + tag.ToLowerInvariant();
            }
        }

        string[] words = (semantic ?? string.Empty).Split(new[] { ' ', '_', '-', '/', ':', '.', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (word.Length >= 3 && haystack.Contains(word))
                score++;
        }

        return score;
    }

    private static int StablePositiveHash(string value)
    {
        unchecked
        {
            int hash = 23;
            string safe = value ?? string.Empty;
            for (int i = 0; i < safe.Length; i++)
                hash = hash * 31 + safe[i];

            return hash & 0x7fffffff;
        }
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        return new string(chars).Trim('_');
    }
}
