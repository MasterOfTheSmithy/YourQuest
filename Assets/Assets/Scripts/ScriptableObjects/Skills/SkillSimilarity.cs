using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class SkillSimilarity
{
    // Tune these based on feel:
    public const float STRONG_MATCH = 0.78f;
    public const float WEAK_MATCH = 0.68f;

    public static float Score(
        string aName, string aDesc, string aContext, string aEnv, string aType,
        string bName, string bDesc, string bContext, string bEnv, string bType
    )
    {
        float name = TokenJaccard(aName, bName);
        float desc = TokenJaccard(aDesc, bDesc);

        // Metadata boosts (keeps “Forest stealth” from upgrading “Dungeon fire” accidentally)
        float context = StringEqBoost(aContext, bContext, 0.10f);
        float env = StringEqBoost(aEnv, bEnv, 0.08f);
        float type = StringEqBoost(aType, bType, 0.12f);

        // Weighted blend
        float score = (name * 0.55f) + (desc * 0.35f) + context + env + type;

        // Clamp to [0..1]
        if (score < 0f) score = 0f;
        if (score > 1f) score = 1f;
        return score;
    }

    private static float StringEqBoost(string a, string b, float boost)
    {
        a = Normalize(a);
        b = Normalize(b);
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0f;
        return a == b ? boost : 0f;
    }

    private static float TokenJaccard(string a, string b)
    {
        var A = Tokenize(a);
        var B = Tokenize(b);

        if (A.Count == 0 && B.Count == 0) return 1f;
        if (A.Count == 0 || B.Count == 0) return 0f;

        int inter = 0;
        foreach (var t in A)
            if (B.Contains(t)) inter++;

        int union = A.Count + B.Count - inter;
        return union <= 0 ? 0f : (float)inter / union;
    }

    private static HashSet<string> Tokenize(string s)
    {
        s = Normalize(s);
        var set = new HashSet<string>();
        if (string.IsNullOrEmpty(s)) return set;

        foreach (Match m in Regex.Matches(s, @"[a-z0-9]+"))
        {
            string token = m.Value;

            // cheap stopword filter (keep it tiny)
            if (token is "the" or "a" or "an" or "and" or "or" or "of" or "to" or "in" or "on") continue;

            set.Add(token);
        }

        return set;
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        return s.Trim().ToLowerInvariant();
    }
}
