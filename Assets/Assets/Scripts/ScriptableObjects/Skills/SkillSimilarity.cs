using System;
using UnityEngine;

public static class SkillSimilarity
{
    // Explicit thresholds so other systems stop guessing
    public const float STRONG_MATCH = 0.78f;
    public const float WEAK_MATCH = 0.45f;

    public static float Score(
        string aName, string aDesc, string[] aTags,
        string bName, string bDesc, string[] bTags)
    {
        float name = Jaccard(Tokenize(aName), Tokenize(bName));
        float desc = Jaccard(Tokenize(aDesc), Tokenize(bDesc));
        float tags = Jaccard(TokenizeTags(aTags), TokenizeTags(bTags));

        float s = name * 0.45f + desc * 0.35f + tags * 0.20f;
        return Mathf.Clamp01(s);
    }

    private static string[] Tokenize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Array.Empty<string>();
        s = s.ToLowerInvariant();
        char[] sep = new[] {
            ' ', '\t', '\n', '\r', '.', ',', ';', ':',
            '!', '?', '/', '\\', '-', '_',
            '(', ')', '[', ']', '{', '}', '"'
        };
        return s.Split(sep, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string[] TokenizeTags(string[] tags)
    {
        if (tags == null || tags.Length == 0) return Array.Empty<string>();
        for (int i = 0; i < tags.Length; i++)
            if (tags[i] != null)
                tags[i] = tags[i].Trim().ToLowerInvariant();
        return tags;
    }

    private static float Jaccard(string[] a, string[] b)
    {
        if (a.Length == 0 && b.Length == 0) return 1f;
        if (a.Length == 0 || b.Length == 0) return 0f;

        var setA = new System.Collections.Generic.HashSet<string>(a);
        var setB = new System.Collections.Generic.HashSet<string>(b);

        int inter = 0;
        foreach (var x in setA)
            if (setB.Contains(x)) inter++;

        int union = setA.Count + setB.Count - inter;
        return union <= 0 ? 0f : (float)inter / union;
    }
}
