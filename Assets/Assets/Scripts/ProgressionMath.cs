using System.Collections.Generic;
using UnityEngine;

public static class ProgressionMath
{
    public struct Result
    {
        public float score;
        public string dominantVerb;
        public string dominantRegionId;
        public bool hasVariety;
        public int totalEvents;
        public int dominantVerbCount;
    }

    public static Result Compute(
        IReadOnlyList<ActionEvent> recent,
        ProgressionBalanceConfig cfg,
        string fallbackRegionId = "region_unknown")
    {
        var r = new Result
        {
            score = 0f,
            dominantVerb = "unknown",
            dominantRegionId = fallbackRegionId,
            hasVariety = false,
            totalEvents = recent == null ? 0 : recent.Count,
            dominantVerbCount = 0
        };

        if (recent == null || recent.Count == 0 || cfg == null)
            return r;

        // Count verbs + sum significance
        var verbCounts = new Dictionary<string, int>(64);
        var verbSig = new Dictionary<string, float>(64);
        var regionCounts = new Dictionary<string, int>(32);

        float totalSig = 0f;

        for (int i = 0; i < recent.Count; i++)
        {
            var e = recent[i];
            if (e == null) continue;

            string v = string.IsNullOrWhiteSpace(e.Verb) ? "unknown" : e.Verb;
            string reg = string.IsNullOrWhiteSpace(e.RegionId) ? fallbackRegionId : e.RegionId;

            if (!verbCounts.ContainsKey(v)) { verbCounts[v] = 0; verbSig[v] = 0f; }
            verbCounts[v]++;

            float sig = Mathf.Max(0f, e.Significance);
            verbSig[v] += sig;
            totalSig += sig;

            if (!regionCounts.ContainsKey(reg)) regionCounts[reg] = 0;
            regionCounts[reg]++;
        }

        // Dominant verb
        string bestVerb = "unknown";
        int bestVerbCount = 0;
        float bestVerbSig = 0f;

        foreach (var kv in verbCounts)
        {
            var v = kv.Key;
            int c = kv.Value;
            float s = verbSig.TryGetValue(v, out var vs) ? vs : 0f;

            // favor significance, break ties by count
            float score = s + (c * 0.1f);
            if (score > bestVerbSig + (bestVerbCount * 0.1f))
            {
                bestVerb = v;
                bestVerbCount = c;
                bestVerbSig = s;
            }
        }

        // Dominant region
        string bestRegion = fallbackRegionId;
        int bestRegionCount = 0;
        foreach (var kv in regionCounts)
        {
            if (kv.Value > bestRegionCount)
            {
                bestRegion = kv.Key;
                bestRegionCount = kv.Value;
            }
        }

        bool hasVariety = verbCounts.Count >= 3;

        // Base score is total significance, scaled a bit by dominant behavior commitment
        float baseScore = totalSig * 1.0f;
        baseScore += bestVerbCount * 0.15f;

        // Diminishing returns for spam
        // If player just repeats one verb endlessly, the *first part* counts, then it starts to “normalize”.
        float spamPenalty = 0f;
        if (bestVerbCount > 1)
        {
            int repeats = bestVerbCount - 1;
            float p = cfg.repeatPenaltyPerSameVerb;

            if (bestVerbCount >= cfg.harshRepeatAfter)
                p = Mathf.Max(p, cfg.harshRepeatPenalty);

            spamPenalty = repeats * p;
        }

        float finalScore = Mathf.Max(0f, baseScore - spamPenalty);

        // Bonuses
        if (hasVariety)
            finalScore *= cfg.varietyBonusMultiplier;

        if (!string.IsNullOrWhiteSpace(bestRegion) && bestRegion.StartsWith("region_"))
            finalScore *= Mathf.Clamp(cfg.semanticRegionBonus, 0.95f, 1.03f);

        r.score = finalScore;
        r.dominantVerb = bestVerb;
        r.dominantRegionId = bestRegion;
        r.hasVariety = hasVariety;
        r.dominantVerbCount = bestVerbCount;

        return r;
    }
}
